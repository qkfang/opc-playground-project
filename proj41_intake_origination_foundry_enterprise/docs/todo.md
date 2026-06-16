# proj41 — TODO / status

## Done (toad)
- [x] Scaffold `proj41_intake_origination_foundry_enterprise` (.NET 10 web app).
- [x] Domain models (Producer / Insured / Risk Submission / Appetite / Exposure signals / Underwriting Study / trace).
- [x] Offline underwriting pipeline (extraction, appetite/triage, exposure research, risk study) — deterministic.
- [x] Foundry prompt-agent pipeline (`AsAIAgent` + `RunAsync`) with per-stage offline fallback.
- [x] Submission Desk SPA — 5 surfaces + pipeline stepper + agent trace.
- [x] Seed broker mailbox (7 varied scenarios incl. prohibited class + spam).
- [x] Minimal API + OpenAPI.
- [x] Bicep (App Service Standard S1 + Foundry + Storage MI-only + KV RBAC + monitoring + RBAC).
- [x] 2 GitHub workflows (infra + deploy) at project folder and repo root.
- [x] 6 integration tests (WebApplicationFactory) — all passing.
- [x] Local verification: build clean, tests green, run-demo + browser smoke, bicep clean.

## Pending
- [ ] QA (toadette): functional pass of all 5 surfaces + run-demo, tests, screenshots, PASS/FAIL.
- [ ] Deploy (yoshi): `proj41_intake_origination_infra` then `proj41_intake_origination_deploy`; verify live `/api/health` + sample submission. **Mandatory after QA PASS.**

## Known notes
- Offline by default; live Foundry once deployed with `Foundry__Enabled=true`.
- Exposure signals are synthesised analyst hypotheses (clearly labelled), not live data.
