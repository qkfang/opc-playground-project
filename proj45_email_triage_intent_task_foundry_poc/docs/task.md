# proj45 — Task Breakdown

## Component A — Domain + mock data
- [x] Domain models: `IncomingEmail`, `EmailExtraction`, `TriageResult`, `IntentDecision`, `TaskPlan`/`TaskExecution`, `OutcomeReport`, `McpToolCall`, `AgentStep`, `EmailCase`, options.
- [x] `Data/seed-emails.json` — diverse mock mailbox (billing dispute, cancellation/churn, tech support, sales lead, complaint, ambiguous/low-confidence, spam).
- [x] `Data/seed-d365.json` — mock D365 customers/accounts/contacts/opportunities/service cases.

## Component B — Services + agents
- [x] `MailboxWatchService` (seed loader + fallback).
- [x] `MockD365McpServer` (tool catalog + `InvokeAsync`, records `McpToolCall`).
- [x] `OfflineEmailPipeline` — 5 deterministic stages (extract → triage → intent → task(MCP) → outcome) with heuristics + human-queue routing.
- [x] `FoundryEmailPipeline` — 5 Foundry prompt-agent stages, per-stage offline fallback, lenient JSON, live probe.
- [x] `HumanReviewQueue` + `CaseStore` (JSON journal).

## Component C — Web UI + API
- [x] `Program.cs` — DI, pipeline selection, minimal API (`/api/inbox`, `/api/mcp/tools`, `/api/cases`, `/api/cases/from-inbox/{id}`, `/api/cases` ad-hoc, `/api/cases/run-demo`, `/api/queue`, `/api/queue/{id}/resolve`, `/api/health`, `/api/health/foundry`).
- [x] Razor SPA shell `Pages/Index.cshtml` with 5 hash-routed views.
- [x] `wwwroot/css/site.css` enterprise theme; `wwwroot/js/app.js` views + decision/confidence cards + agent timeline + MCP tool cards + per-page agent instructions panel.
- [x] `Error.cshtml`.

## Component D — Infra + CI
- [x] `bicep/main.bicep` + modules (appservice, foundry, storage, keyvault, monitoring) + `main.bicepparam` (baseName=proj45).
- [x] `.github/workflows/proj45_email_triage_infra.yml` + `proj45_email_triage_deploy.yml`.

## Component E — Tests + verification
- [x] `tests/` — offline pipeline tests + API endpoint test (WebApplicationFactory).
- [x] `scripts/smoke.ps1` — health + run-demo + queue smoke.
- [x] Local `dotnet build` / `dotnet test` / `az bicep build` / browser screenshot.
