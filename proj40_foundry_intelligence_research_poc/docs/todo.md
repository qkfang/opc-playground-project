# Todo — proj40 Foundry Intelligence & Research POC

## Done (build, toad)
- [x] Domain model for the intelligence pipeline (email + document → entities → insights → sources → brief → report email).
- [x] Mock inbox with attached customer documents (RFP, briefing note, incident report, spam).
- [x] Mocked internal + external source corpus, keyed by entity.
- [x] Deterministic offline engine (all 5 stages) + automatic engine selection.
- [x] Live Foundry prompt-agent engine (grounded JSON stages) with offline fallback.
- [x] Minimal API + OpenAPI; case persistence to App_Data (Blob-ready).
- [x] Multi-tab Razor UI (Inbox → Entities → Insights → Sources → Research → Report email) + compose + cases.
- [x] xUnit tests (7 unit + 8 API) — 15/15 passing.
- [x] Local smoke script (9 checks) — all passing.
- [x] Bicep infra (App Service + Foundry + Storage + Key Vault + monitoring, managed-identity RBAC) — `az bicep build` clean.
- [x] GitHub Actions infra + deploy workflows.
- [x] README + docs + sample outputs. (UI screenshots captured at QA stage.)
- [x] Fixed during browser e2e: modal `[hidden]` CSS override; document word-count serialization.

## Next (QA, toadette)
- [ ] Independently verify build/tests/smoke + browser e2e across the 5 tabs.
- [ ] Capture an app screenshot for the record.
- [ ] On PASS → hand to yoshi for the mandatory Azure deploy.

## Possible future enhancements
- [ ] Replace the mock `SourceCorpus` with real connectors (CRM API, news API, Azure AI Search).
- [ ] Persist cases to Azure Blob (the `Storage:Mode=blob` seam already exists).
- [ ] Real document parsing (PDF/DOCX) for genuine attachments.
- [ ] Push the generated report email through a real mail connector (Graph / ACS Email).
