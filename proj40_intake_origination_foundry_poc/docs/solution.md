# proj40 — Enterprise Intake & Origination Agents POC (Microsoft Foundry)

**project_id:** proj40
**project_code:** proj40_intake_origination_foundry_poc
**App:** `Northwind Revenue Origination Console` — .NET 10 ASP.NET Core (Razor Pages + minimal API)
**Pattern:** Microsoft Foundry prompt-agents (Microsoft Agent Framework) with a deterministic offline fallback.

## 1. Problem & business context

Large enterprises receive inbound demand (sales enquiries, RFPs, partner referrals) through many front doors.
Revenue Operations must turn each raw inbound email into structured CRM records, triage and route it within SLA,
research the account, and brief the seller — fast and consistently. This POC demonstrates an **agentic
intake & origination pipeline** that automates that flow end-to-end.

This is intentionally **different from prior POCs** (e.g. proj37 cost-estimator): different domain (revenue
origination, not cloud costing), different entities (Lead/Account/Opportunity), different UX (an operations
console with 5 workflow surfaces), and a 4-stage multi-agent pipeline with an audit trace.

## 2. The origination pipeline (4 agents)

```
Inbound email  →  [1] Extraction agent  →  Lead / Account / Opportunity (+confidence, missing fields)
               →  [2] Triage agent      →  classification, lead score, priority, SLA, routing, risk flags
               →  [3] Lead Mgmt agent   →  firmographic research + inbound demand signals + talking points
               →  [4] Report agent      →  executive origination brief / study
```

Each stage is produced by a **Microsoft Foundry prompt agent** (`AIProjectClient.AsAIAgent(...)`, returns JSON)
or, when Foundry is not configured/unavailable, by the **deterministic offline pipeline**. Every stage is
recorded as an `AgentTrace` step (agent, summary, duration) for an auditable timeline.

## 3. Pages / flows (UI surfaces)

| Surface | Purpose |
| --- | --- |
| **Intake Inbox** | Mock inbound mailbox (the trigger source) + a trigger console to process a selected email or paste an ad-hoc one; recent-cases table. |
| **Extracted Records** | Lead / Account / Opportunity entity cards with extraction confidence + "needs enrichment" flags. |
| **Triage & Routing** | Lead-score gauge, priority + SLA, classification, routing queue, recommended action, rationale, risk flags. |
| **Lead Research** | Account overview, likely initiatives, buying-intent gauge + stage, categorised demand signals, talking points. |
| **Origination Brief** | Executive study: summary, highlights, sections, recommendations, next-best-action. |

A pipeline **stepper** and an **agent trace timeline** appear across the detail surfaces.

## 4. Architecture

- **Frontend + backend:** single ASP.NET Core app (Razor Pages shell + vanilla JS SPA, minimal-API JSON backend).
- **Engine selection:** `IIntakePipeline` resolved at startup → `FoundryIntakePipeline` when configured, else `OfflineIntakePipeline`. The Foundry pipeline also falls back per-stage to offline on any error.
- **Persistence:** `CaseStore` keeps recent cases in memory and best-effort persists a JSON journal to `Storage:LocalDataFolder` (→ `/home/site/data` on App Service, writeable under RunFromPackage).
- **Mock mailbox:** `MailboxService` seeds from `Data/seed-mailbox.json` (7 varied enterprise scenarios incl. a spam/disqualify case) with a built-in fallback.
- **Telemetry:** Application Insights when `APPLICATIONINSIGHTS_CONNECTION_STRING` is present.
- **OpenAPI:** `/openapi/v1.json` (usable as a Foundry Agent Service OpenAPI tool).

## 5. API

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/health` | Liveness + active engine (foundry/offline). |
| GET | `/api/inbox` | Mock inbound mailbox. |
| GET | `/api/cases` | Processed cases (most recent first). |
| GET | `/api/cases/{caseId}` | Full case (records, triage, research, report, trace). |
| POST | `/api/cases/from-inbox/{emailId}` | Run the pipeline for a mailbox email. |
| POST | `/api/cases` | Run the pipeline against an ad-hoc email payload. |
| POST | `/api/cases/run-demo` | Process the whole inbox (demo/CI/smoke). |
| DELETE | `/api/cases` | Clear the case journal (demo reset). |

## 6. Foundry integration

- Package: `Microsoft.Agents.AI` + `Microsoft.Agents.AI.Foundry` (hosted in-process agent pattern).
- Auth: `DefaultAzureCredential` (managed identity in Azure; keyless / `disableLocalAuth`).
- Config (`Foundry` section / `Foundry__*` env): `Enabled`, `ProjectEndpoint`, `ModelDeploymentName`, `AgentName`.
- When `Enabled=false` or no endpoint → offline engine (the app is always demoable).

## 7. Infrastructure (Bicep → `rg-playground-01`)

`bicep/main.bicep` + modules: `monitoring`, `storage`, `keyvault`, `foundry` (AI Services account + project + model deployment), `appservice` (Linux .NET 10, Standard SKU `S1`), plus managed-identity RBAC (Storage Blob Data Contributor, Key Vault Secrets User, Cognitive Services User + OpenAI User). `baseName = proj40`.

## 8. CI/CD (GitHub Actions, manual dispatch)

- `proj40_intake_origination_infra.yml` — what-if + deploy Bicep.
- `proj40_intake_origination_deploy.yml` — build, publish, zip-deploy to App Service, health smoke test.

## 9. Offline heuristics (so the POC is believable without Foundry)

Extraction uses regex + keyword dictionaries: title/seniority, company (signature label → suffix tokens →
domain prettify), industry classification, country→region, segment/headcount, product interest, timeline,
budget, competitor detection, ARR estimate. Triage = firmographic fit + intent scoring → priority/SLA/routing.
Research synthesises plausible (clearly-labelled) demand signals seeded deterministically from the account.

## 10. Status

- [x] .NET app implemented, builds clean (0 warnings/errors), runs locally.
- [x] All 5 surfaces verified in browser; full demo inbox processed (7 cases).
- [x] Bicep + 2 workflows authored.
- [x] Integration test (WebApplicationFactory) for the offline pipeline.
- [ ] QA validation (toadette).
- [ ] Azure deployment (yoshi) after QA PASS.
