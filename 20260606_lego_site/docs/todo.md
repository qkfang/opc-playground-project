# TODO - 20260606_lego_site

- [x] (T1) Confirm stack & scope in docs (SWA + Functions + Cosmos)
- [x] (T2) Define API surface + data model (Sets, Listings)
- [x] (T3) Copilot task: scaffold frontend (Next.js) + UI pages — PR #50
- [x] (T4) Copilot task: scaffold backend (Azure Functions API) + Cosmos/mock store — PR #52
- [x] (T5) Copilot task: wire frontend to API + auth-gated listing CRUD — PR #54
- [x] (T6) Local smoke test — backend 3/3 tests pass, frontend build + lint clean
- [x] (T7) Add infra (Bicep) + GitHub Actions deploy (SWA token, Function App)
- [x] (T8) Deploy to Azure rg-playground-01 and validate

## Validation
- Live frontend: `https://blue-meadow-0f3902100.7.azurestaticapps.net`
- Live backend: `https://lego20260606-func-t4kkdmb53srxc.azurewebsites.net`
- Checked on 2026-06-07:
  - `GET /api/sets` returns `200 OK`
  - Frontend `/`, `/sets`, and `/marketplace` return `200 OK`
- Workflow fixes applied:
  - backend deploy package creation no longer depends on `zip` being present inside `azure/cli`
  - infra workflow now falls back to existing LEGO SWA/Function resources when Bicep re-run hits the storage-policy provisioning edge case

## Notes
- First cut uses the **mock (in-memory) data store** — Cosmos is provisioned only when
  `deployCosmos=true`. Backend selects Cosmos only if `COSMOS_CONNECTION_STRING` +
  `COSMOS_DATABASE_NAME` + `COSMOS_CONTAINER_NAME` are all set; otherwise mock.
- Architecture (Option A): Next.js SSR frontend on SWA + separate Azure Functions backend.
  SWA app setting `BACKEND_API_BASE_URL` points the frontend proxy at the Function App.
- Workflows: `20260606_lego_site_infra.yml` (Bicep) and `20260606_lego_site_deploy.yml`
  (backend zip-deploy + frontend SWA token deploy). Both use `azure/login@v1` + AZURE_CREDENTIALS.
