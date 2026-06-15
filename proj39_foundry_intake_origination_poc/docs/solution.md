# proj39 — Solution Design

## Goal

A real, demoable **Intake & Origination** POC for B2B sales, built as a **.NET 10 Azure web app** that
uses a **Microsoft Foundry prompt agent** for the reasoning steps, with a deterministic offline engine
so it always runs.

## Pipeline (multi-agent)

The pipeline runs four reasoning stages against a single inbound email, orchestrated by
`IOriginationEngine`:

1. **Extraction** — inbound email → `Account` + `Lead` + `Opportunity` records (+ confidence, missing
   fields). Heuristics: company from signature/domain, industry keywords, employee/revenue bands,
   seniority + decision-maker detection, **budget-vs-revenue–aware** deal value, timeline, drivers.
2. **Triage / classification** — weighted, fully transparent scoring (budget 0-30, authority 0-20,
   need 0-20, timeline 0-15, fit 0-15) → **Hot / Warm / Cold / Spam** with routing + SLA. Spam gate runs
   first (prize/fee/scam heuristics + suspicious TLDs).
3. **Lead research + demand signals** — *Lead Management Agent*: company overview, inbound demand
   signals (strength-rated, sourced), talking points, competitors, fit assessment, recommended actions.
4. **Report / study** — *Report Agent*: executive summary + structured sections + markdown, with a
   disposition (Pursue / Nurture / Disqualify).

Each stage records an `AgentStepLog` (agent, summary, engine, duration) surfaced in the UI.

## Engines

- **`OfflineOriginationEngine`** (always available): deterministic rules/regex/heuristics. Split into
  `*.Extraction.cs` and `*.Pipeline.cs` (triage/research/report). Rule tables (industry keywords,
  scoring weights) are editable in place.
- **`FoundryOriginationEngine`** (live): `AIProjectClient.AsAIAgent(...)` creates an ephemeral
  Responses-API prompt agent; each stage is a grounded JSON call with a strict schema. **Falls back to
  the offline engine on any failure**, recording the reason, so the POC is never blocked.

Engine selection is automatic from config (`Foundry:Enabled` + `ProjectEndpoint`).

## Web app

- **Razor Pages** shell + **vanilla JS** that drives everything through the **minimal API**.
- Three-panel UX: mock inbox → email reader + trigger → live multi-agent result (records, triage
  factors with bars, demand signals, downloadable study). Plus a **compose-your-own** inbound email modal.
- **OpenAPI** document served at `/openapi/v1.json` (usable as a Foundry Agent Service OpenAPI tool).

## Azure footprint (bicep)

App Service (Linux, .NET 10, `S1`), Microsoft Foundry (AI Services account + project + `gpt-4o`),
Storage (keyless, blob), Key Vault (RBAC), Log Analytics + App Insights. The web app's
**system-assigned managed identity** gets least-privilege RBAC: Storage Blob Data Contributor, Key Vault
Secrets User, Cognitive Services User + OpenAI User. `httpsOnly`, TLS 1.2, FTPS disabled,
`disableLocalAuth` on Foundry, `allowSharedKeyAccess=false` on Storage.

## Persistence

`OriginationCaseService` writes each case to `App_Data` (local) or `/home/site/data` (App Service) and
keeps an in-memory index. Blob persistence can be layered later via `StorageOptions`.

## Safe-fallback / mock strategy

No real mailbox or external research source was provided, so:
- Inbound emails are **mocked** (`Data/mock-emails.json`, 5 realistic scenarios incl. a spam sample).
- Demand-signal "research" is synthesised from the email's stated pains + clearly-marked sector
  intelligence — extensible to real connectors without changing the pipeline shape.
- The whole pipeline runs offline; the live Foundry path is additive.

## Testing

- 6 unit tests (offline engine: extraction, employee band, Hot triage, spam quarantine, full pipeline,
  spam disposition).
- 6 API integration tests (`WebApplicationFactory`): health, seeded emails, known-email pipeline,
  ad-hoc pipeline, 404, markdown report download.
- `scripts/smoke.ps1`: 8 end-to-end checks against a running instance.
