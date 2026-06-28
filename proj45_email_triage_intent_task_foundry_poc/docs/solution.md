# proj45 — Email Triage + Intent + Task Agent Enterprise POC (Foundry, .NET)

**Product name:** Relay Desk — Inbound Email Orchestration
**Repo folder:** `proj45_email_triage_intent_task_foundry_poc`
**Stack:** .NET 10 (ASP.NET Core Razor Pages SPA shell) + Microsoft Foundry prompt-agents (Microsoft Agent Framework, in-process `AIProjectClient.AsAIAgent`) with a deterministic offline fallback so the demo always runs. Mock D365 MCP tool layer.

## Why this POC

Enterprises receive a high volume of inbound email into shared service mailboxes (support@, service@, sales@). Relay Desk demonstrates an **agentic inbound-email orchestration pipeline** that watches a mailbox, extracts and classifies each message, decides the customer **intent**, routes ambiguous/low-confidence cases to a **human review queue**, and then has a **task agent** look up the customer in (mock) **Dynamics 365** via MCP tools and carry out the appropriate downstream operation — finishing with an **outcome** record that includes a full audit trail and an agent decision timeline.

This reuses the proven proj41/proj44 Foundry enterprise pattern (per-stage agent, offline fallback, lenient JSON, live readiness probe, Standard App Service + keyless Foundry via managed identity) but is a clearly distinct **email triage + intent orchestration** demo.

## Five visible stages (one routable page each)

| # | Page | Foundry agent | Responsibility |
|---|------|---------------|----------------|
| 1 | **Email** | `relay-triage-extraction-agent` (extraction half) | Mailbox watch surface. Shows the mock inbox; ingest one message or the whole mailbox. Extracts sender, subject, structured fields, entities, and a normalized email record. |
| 2 | **Triage** | `relay-triage-extraction-agent` (classification half) | Classifies category, urgency, sentiment, language, spam/risk; computes a triage confidence and SLA. |
| 3 | **Intent** | `relay-intent-router-agent` | Decides the customer's **purpose** (e.g. Billing Dispute, Cancellation, Tech Support, Sales Lead, Complaint…), an intent confidence, and **routes uncertain/ambiguous cases to a human review queue** with a reason. Surfaces decision cards. |
| 4 | **Task (D365 MCP)** | `relay-task-execution-agent` | Calls **mock D365 MCP tools** to look up customer/account/contact/opportunity/service context for the detected intent, then **simulates the downstream operation** (e.g. open a case, raise a credit memo, create a callback, flag churn). Shows MCP tool-call cards. |
| 5 | **Outcome** | `relay-outcome-reporter-agent` | Produces the final status, customer-facing summary/draft reply, the **audit trail**, and an **agent timeline** with confidence/decision cards. |

Each page renders the **exact agent instruction set** used for that stage (transparency requirement).

## Architecture

```
SPA shell (Razor /Index) → 5 client views (Email/Triage/Intent/Task/Outcome) routed by hash (#email …)
        │  fetch JSON
        ▼
Minimal API  /api/*
        │
        ▼
IEmailPipeline ──► FoundryEmailPipeline (live)  ─per-stage fallback─►  OfflineEmailPipeline (deterministic)
        │                                                                     ▲
        │  stage 4 (Task) calls ─────────────────────────────────────────────┘
        ▼
ID365McpServer (mock MCP)  ◄──  MCP tool catalog (customer.search, account.get, opportunity.list,
                                 service.cases.list, case.create, creditmemo.raise, callback.create,
                                 churn.flag)  — returns from an in-memory D365 dataset.
```

- **MailboxWatchService** — serves the mock inbox (`Data/seed-emails.json`, fallback set built in). Simulates "watch" by exposing unread items and a per-item ingest.
- **CaseStore** — in-memory + JSON journal of processed `EmailCase` items (survives restart via `Storage__DataDirectory`).
- **ID365McpServer / MockD365McpServer** — mock MCP server exposing a **tool catalog** + `InvokeAsync(tool, args)`; backed by `Data/seed-d365.json`. Every call is recorded as an `McpToolCall` for the Task page cards + audit trail.
- **HumanReviewQueue** — holds cases whose intent confidence is below threshold or flagged ambiguous; the Intent page lets a reviewer resolve them.
- **Trace** — every stage appends an `AgentStep` (stage, agent, engine foundry|offline, confidence, decision, durationMs) → renders the enterprise **agent timeline**.

## Foundry agent design (per page, explicit instructions)

1. **Extraction** (Email): read raw email → JSON `{from, fromName, subject, channel, language, entities[], orderRefs[], accountHints[], normalizedBody, extractionConfidence}`.
2. **Triage** (Triage): → JSON `{category, subCategory, urgency P1..P4, sentiment, spamRisk, riskFlags[], slaHours, triageConfidence, rationale}`.
3. **Intent router** (Intent): → JSON `{intent, intentConfidence, intentBand, alternativeIntents[], requiresHuman bool, humanReason, suggestedQueue, rationale}`. Below-threshold confidence ⇒ `requiresHuman=true`.
4. **Task execution** (Task): given intent + customer context produced from MCP lookups → JSON `{plannedTool, operation, operationArgs, customerSummary, expectedEffect, riskLevel, requiresApproval, rationale}`. The pipeline then executes the chosen mock MCP operation and records the result.
5. **Outcome reporter** (Outcome): → JSON `{finalStatus, customerReplyDraft, executiveSummary, auditTrail[], nextActions[], slaMet}`.

All agents return **only** a single JSON object; numbers are raw; lenient JSON parsing tolerates LLM quirks; any failure falls back to the deterministic offline stage and is recorded.

## Mock D365 MCP tool catalog

| Tool | Args | Returns |
|------|------|---------|
| `customer.search` | `{query}` | matching customers (id, name, tier, email) |
| `account.get` | `{accountId}` | account profile (industry, ARR, owner, status) |
| `contact.get` | `{accountId}` | primary contacts |
| `opportunity.list` | `{accountId}` | open opportunities (stage, amount) |
| `service.cases.list` | `{accountId}` | existing service cases |
| `case.create` | `{accountId, title, priority}` | new case id |
| `creditmemo.raise` | `{accountId, amount, reason}` | credit memo id |
| `callback.create` | `{accountId, when, topic}` | callback id |
| `churn.flag` | `{accountId, severity, reason}` | churn signal id |

## Health & diagnostics

- `GET /api/health` — static engine mode (offline | configured | misconfigured).
- `GET /api/health/foundry` — **active** live agent round-trip probe → `live | fallback | error | offline` (used by the deploy smoke gate).

## Non-goals / MVP caveats

- No real mailbox (Graph) connection — mailbox is mocked. No real D365 — MCP/data are mocked. In-memory + JSON journal (no external DB). No auth. Single-tenant demo.
