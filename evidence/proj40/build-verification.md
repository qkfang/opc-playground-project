# proj40 — Build verification evidence (toad)

Commit: 94bdcdf (opc-playground-project, main)
Date: 2026-06-16 20:20

## Build
dotnet build -c Release  =>  0 warnings, 0 errors (.NET 10.0.300, net10.0)

## Tests
dotnet test -c Release  =>  5 passed / 0 failed
- Health_returns_offline_engine
- Inbox_is_seeded
- Strategic_bank_email_scores_P1_with_full_extraction
- Spam_email_is_disqualified
- Run_demo_processes_whole_inbox

## Smoke (POST /api/cases/run-demo, offline engine)
GlobalBank Corp        | New Business/P1 | score 100 | ARR 1,200,000
Meridian Health        | New Business/P2 | score 64
BrightRetail Pty       | New Business/P3 | score 48
Contoso Manufacturing  | Renewal/P2      | score 74  | ARR 750,000
Nimbus Ai (SMB)        | New Business/P3 | score 24  | ARR 35,000
Gov Agency             | New Business/P2 | score 72
Cheap Seo Now          | Spam/Disqualified/P3 | score 5  (correctly disqualified)

## Browser verification (http://localhost:5240)
All 5 surfaces render correctly:
- Intake Inbox (mock mailbox + trigger console + recent-cases table)
- Extracted Records (Lead/Account/Opportunity entity cards + confidence + enrichment flags)
- Triage & Routing (score gauge, P1 pill + SLA, routing queue, rationale, competitor risk flag)
- Lead Research (account overview, initiatives, intent gauge, 4 demand signals, talking points)
- Origination Brief (exec summary, highlights, sections, recommendations, next-best-action)
Pipeline stepper + agent-trace timeline present across detail surfaces.

## Infra
az bicep build --file bicep/main.bicep => exit 0 (clean).
App Service Standard S1, Foundry (AI Services + project + gpt-4o), Storage MI-only, KV RBAC, monitoring, 4 MI role assignments.

## Engine
Runs 'offline' by default (Foundry__Enabled=false). Live Foundry path active when Foundry__Enabled=true + Foundry__ProjectEndpoint set; per-stage offline fallback on any error.
