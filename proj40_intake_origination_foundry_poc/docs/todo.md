# proj40 — TODO

## Build (toad)
- [x] Scaffold project structure (apps/web, bicep, docs, scripts, tests, samples)
- [x] Domain models: InboundEmail, Lead/Account/Opportunity, Triage, LeadResearch, Report, IntakeCase, AgentTrace
- [x] Deterministic offline pipeline (extraction → triage → research → report) with heuristic NLP
- [x] Foundry prompt-agent pipeline with per-stage offline fallback
- [x] CaseStore (in-memory + JSON journal) + MailboxService (seed mailbox)
- [x] Minimal API endpoints + health + OpenAPI
- [x] Enterprise UI: sidebar console, 5 flow views, pipeline stepper, agent trace timeline
- [x] Local build (0 warnings) + smoke test (run-demo → 7 cases) + browser verification of all 5 views
- [x] Bicep (main + monitoring/storage/keyvault/foundry/appservice) + RBAC
- [x] GitHub Actions: infra + deploy workflows
- [x] Integration test (WebApplicationFactory) for offline pipeline
- [x] Update shared-context/projects/proj40.md + PROJECT-LOG.md
- [x] Handoff to QA (toadette)

## QA (toadette)
- [ ] Local build + run
- [ ] Functional pass of all 5 surfaces + run-demo
- [ ] Confirm offline fallback + health endpoint
- [ ] Screenshots
- [ ] On PASS → handoff to DevOps (yoshi)

## Deploy (yoshi)
- [ ] Deploy infra (Bicep) to rg-playground-01
- [ ] Deploy app (zip) to App Service
- [ ] Verify live + screenshots
- [ ] Report to orchestrator
