# proj39 — Todo / Status

## Done
- [x] Scaffold project structure (`apps/web`, `bicep`, `tests`, `scripts`, `docs`, `samples`).
- [x] Domain models: InboundEmail, Account, Lead, Opportunity, Triage, Research, Report, OriginationCase.
- [x] Offline engine: extraction + triage + research + report (deterministic, auditable).
- [x] Foundry engine: 4-stage prompt-agent pipeline with offline fallback.
- [x] Services: mock inbox store, origination case service (local persistence).
- [x] Minimal API: emails, cases/process, cases, report download, health.
- [x] Razor UI: mock inbox → run pipeline → live multi-agent result + compose-your-own.
- [x] Mock data: 5 inbound emails (hot enterprise, warm RFP, cold SMB, building-business-case, spam).
- [x] Bicep infra: App Service, Foundry, Storage, Key Vault, monitoring + managed-identity RBAC.
- [x] GitHub Actions: infra + deploy workflows.
- [x] Tests: 12 (unit + API integration) — all passing.
- [x] Smoke script: 8 end-to-end checks — passing.
- [x] Docs + README + sample origination studies. (UI screenshots captured at QA stage.)

## Possible future enhancements (not required for POC)
- [ ] Real mailbox connector (Microsoft Graph / Logic Apps) to replace the mock inbox.
- [ ] Real research connectors (web/company data) for demand signals.
- [ ] Blob persistence + history view; export study as PDF/DOCX.
- [ ] Foundry file-search / vector store for attachment grounding.
- [ ] Human-in-the-loop approve/route actions writing back to a real CRM.
