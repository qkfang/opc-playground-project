# Tasks — Fruit site + Robotics news

## Infra
- [ ] Create resource group usage plan (rg-playground-01)
- [ ] Create Azure Static Web App
- [ ] Create Azure Web App (App Service) for .NET API
- [ ] Configure CORS (allow SWA origin)
- [ ] Add app settings (NEWS_FEED_URL, CACHE_SECONDS)

## Backend
- [ ] Scaffold ASP.NET Core Minimal API
- [ ] Implement `/api/news/robotics`
- [ ] Add basic caching + timeout + error handling
- [ ] Add `/healthz`
- [ ] Add unit-ish smoke test or simple curl script

## Frontend
- [ ] Scaffold static site
- [ ] Fruit UI pages/sections
- [ ] Robotics news page that calls API and renders list
- [ ] Friendly loading/error states

## CI/CD
- [ ] GitHub Action: deploy backend to Web App
- [ ] GitHub Action: deploy frontend to SWA using deployment token

## Verification
- [ ] Local run: frontend loads news from local API
- [ ] Deployed: SWA can call Web App API and shows latest news
