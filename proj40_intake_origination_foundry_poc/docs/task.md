# proj40 — Task breakdown

## Component 1 — Domain + agents (backend)
- Domain model for the origination pipeline (entities + case + trace).
- Offline deterministic pipeline (heuristic extraction/triage/research/report).
- Foundry prompt-agent pipeline (4 JSON prompt calls) with offline fallback.
- CaseStore + MailboxService.
- Minimal API + health + OpenAPI.
Status: DONE.

## Component 2 — Enterprise UX (frontend)
- Sidebar console shell, pipeline stepper, engine pill.
- 5 flow views: Inbox/trigger, Extracted Records, Triage, Lead Research, Origination Brief.
- Agent trace timeline; recent-cases table; toast/UX polish.
Status: DONE.

## Component 3 — Infra + CI/CD
- Bicep main + modules (monitoring, storage, keyvault, foundry, appservice) + RBAC.
- GitHub Actions: infra (what-if+deploy) + deploy (build/publish/zip/smoke).
- Standard SKU app service plan; baseName=proj40.
Status: DONE (deployment executed by yoshi after QA PASS).

## Component 4 — Verification
- Local build (Release) clean.
- Smoke: POST /api/cases/run-demo → 7 cases; health = offline.
- Browser: all 5 surfaces verified.
- Integration test (WebApplicationFactory).
Status: DONE.
