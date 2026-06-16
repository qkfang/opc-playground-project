# proj41 — Sentinel Underwriting: Submission Desk (Solution)

**Enterprise Intake & Origination agents POC for commercial P&C insurance underwriting.**

A .NET 10 Azure Web App that turns inbound broker **submission emails** into structured underwriting
records, an appetite/triage decision, exposure research, and an executive **Underwriting Risk Study** —
using **Microsoft Foundry prompt agents** with a deterministic **offline fallback** so the demo always runs.

This is deliberately a *different business domain, UX and workflow* from proj40 (which was a generic B2B
revenue-origination console). Here the lens is **commercial insurance underwriting origination**.

## Domain mapping (intake/origination entities)

| Generic entity | proj41 meaning |
| --- | --- |
| Lead | **Producer** — the broker/agent who submitted the risk (name, brokerage, tier, appointed) |
| Account | **Insured** — the company seeking coverage (industry/SIC, TIV, locations, revenue, years trading) |
| Opportunity | **Risk Submission** — the line of business + coverage (limit, deductible, premium, effective date, incumbents) |

## The multi-agent pipeline

Inbound submission → **4 agents**, each surfaced with an audit trace:

1. **Submission Intake Agent** → `ExtractedRecords` (Producer / Insured / Risk Submission + confidence + missing-for-underwriting list).
2. **Appetite & Triage Agent** → `AppetiteDecision`: appetite class (*In Appetite / Refer to Underwriter / Out of Appetite / Decline*), **risk score** (hazard-weighted) + **fit score**, priority/SLA, routing queue + desk, referral triggers, risk flags, declination.
3. **Risk Research Agent** → `LeadResearch`: account overview, binding-intent score, and categorised inbound **exposure/demand signals** (CatastropheExposure, LossHistory, IndustryHazard, FinancialStress, Regulatory, Growth) + recommended questions to the broker.
4. **Underwriting Study Agent** → `UnderwritingStudy`: executive summary, overall recommendation (*Bind / Quote with conditions / Refer / Decline*), indicated premium + pricing rationale, key risk flags, recommended conditions, exclusions, sections, and next actions.

### Foundry + offline fallback

- **`FoundryUnderwritingPipeline`** uses the Microsoft Agent Framework (`AIProjectClient.AsAIAgent(model, instructions, name)` + `agent.RunAsync`) with `DefaultAzureCredential`. Each stage is a grounded JSON prompt agent.
- **Per-stage fallback**: any failure (missing config, auth, timeout, malformed JSON) falls back to the deterministic offline result for that stage and records the reason in the trace.
- **`OfflineUnderwritingPipeline`** is heuristic NLP (regex + keyword dictionaries): producer/insured/submission extraction, hazard classification, appetite & risk/fit scoring, deterministic exposure signals, pricing by rate-on-line, and the study narrative. Runs with **no Azure** so the POC is always demoable.
- Engine selection: offline by default (`Foundry:Enabled=false`); live Foundry when `Foundry:Enabled=true` + `Foundry:ProjectEndpoint` set (post-deploy).

## UX — the Submission Desk (5 surfaces)

Single-page app (Razor Pages shell + vanilla JS + minimal API + OpenAPI), dark "underwriting desk" theme:

1. **Submission Desk** — mock broker mailbox (trigger source) + ad-hoc trigger console + submission queue.
2. **Risk Records** — Producer / Insured / Risk-Submission entity cards with confidence bars + missing-info chips.
3. **Appetite & Triage** — risk + fit gauges, routing, referral triggers, risk flags, rationale.
4. **Exposure Research** — account overview, binding-intent gauge, exposure/demand-signal cards, broker questions.
5. **Underwriting Study** — executive recommendation banner + indicated premium, pricing rationale, risk flags, conditions/exclusions, sections, next actions.

Plus a pipeline stepper and an agent-trace timeline (per-stage engine + timing).

## API

`GET /api/health` · `GET /api/inbox` · `GET /api/cases` · `GET /api/cases/{id}` ·
`POST /api/cases/from-inbox/{emailId}` · `POST /api/cases` (ad-hoc) · `POST /api/cases/run-demo` ·
`DELETE /api/cases` · OpenAPI at `/openapi/v1.json`.

## Azure infrastructure (`bicep/`)

`main.bicep` + modules (monitoring / storage / keyvault / foundry / appservice), `baseName=proj41`:

- **App Service** — Linux, `DOTNETCORE|10.0`, **Standard `S1`** (repo policy), HTTPS-only, system-assigned identity, `WEBSITE_RUN_FROM_PACKAGE`.
- **Microsoft Foundry** — AI Services account + project + `gpt-4o` GlobalStandard deployment.
- **Storage** — `allowSharedKeyAccess: false` (**managed identity only**), blob container `submissions`.
- **Key Vault** — RBAC authorization.
- **Monitoring** — Log Analytics + Application Insights.
- **RBAC** — MI role assignments: Storage Blob Data Contributor, Key Vault Secrets User, Cognitive Services User + OpenAI User.

## CI/CD (`.github/workflows/`, also mirrored at repo root)

- `proj41_intake_origination_infra.yml` — what-if + `az deployment group create` of the bicep.
- `proj41_intake_origination_deploy.yml` — build/publish/zip-deploy (RunFromPackage) + `/api/health` smoke.
- Resource group `rg-playground-01`, region `australiaeast`, `AZURE_CREDENTIALS` secret.

## Local verification (toad)

- `dotnet build -c Release` → **0 warnings / 0 errors** (.NET 10.0.300).
- `dotnet test -c Release` → **6/6 pass** (WebApplicationFactory: health=offline, inbox seeded, property extraction + high-limit referral, cyber classification, prohibited-class out-of-appetite, run-demo).
- `POST /api/cases/run-demo` → 7/7 seed submissions into believable, differentiated cases (Atlas Steel → Refer/P2 $382.5K; Big Bang Fireworks → Out of Appetite/Decline; SEO spam → Decline).
- Browser-verified all 5 surfaces render correctly with full Atlas Steel case.
- `az bicep build` → clean.

## Notes / risks

- Live Foundry path requires model quota (`gpt-4o` GlobalStandard) in the target region; until deployed the app runs offline.
- Storage uses managed identity only (no shared keys). App Service is Standard `S1` per policy.
- Exposure/demand signals are synthesised and clearly labelled as analyst hypotheses (not real-time data feeds).
