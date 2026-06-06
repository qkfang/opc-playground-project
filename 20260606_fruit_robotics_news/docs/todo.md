# 20260606_fruit_robotics_news — TODO

## T0 — Repo setup & docs
- [ ] Create project folders (apps/bicep/docs/scripts)
- [ ] Write solution.md (this)
- [ ] Write task.md + tasks split for Copilot
- [ ] Commit docs + structure

## T1 — Infra (Bicep + GitHub Actions)
- [ ] Bicep: App Service plan + Web App for .NET API
- [ ] Bicep: Static Web App (no repo integration) + output deployment token retrieval notes
- [ ] GitHub Action: 20260606_fruit_robotics_news_infra.yml

## T2 — Backend (.NET API)
- [ ] Create .NET Web API project
- [ ] Implement /api/news (RSS fetch + parse + cache)
- [ ] CORS config
- [ ] GitHub Action: 20260606_fruit_robotics_news_api.yml

## T3 — Frontend (SWA)
- [ ] Create Vite+React app (fruit theme)
- [ ] Fetch news from API; list + refresh
- [ ] GitHub Action: 20260606_fruit_robotics_news_frontend.yml (deploy via SWA token)

## T4 — Validate
- [ ] Local smoke test (frontend + api)
- [ ] Deploy via Actions
- [ ] Verify endpoints and UI
- [ ] Screenshots
