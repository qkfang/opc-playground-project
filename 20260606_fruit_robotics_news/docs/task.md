# 20260606_fruit_robotics_news — Tasks

We will implement via GitHub Copilot coding agent as 3 issues (one per component).

## Task 1 — Infra (Bicep + workflows)
**Goal:** Provision Azure resources in `rg-playground-01` and add GitHub Actions workflows.

### Scope
- Create Bicep templates under `20260606_fruit_robotics_news/bicep`
  - App Service Plan (Linux) + Web App for API
  - Static Web App
- Add GitHub Action `20260606_fruit_robotics_news_infra.yml`
  - Uses `azure/login@v1` with `AZURE_CREDENTIALS`
  - Deploys Bicep to `rg-playground-01`
- Document how to get SWA deployment token via `az` and store in GH secret (e.g. `SWA_DEPLOYMENT_TOKEN_20260606_FRUIT_ROBOTICS_NEWS`)

### Acceptance criteria
- Workflow runs and deploys/updates infra successfully
- Outputs (or logs) include API hostname and SWA resource name

## Task 2 — Backend (.NET API)
**Goal:** Provide `/api/news` that returns a robotics news list from an RSS/Atom feed.

### Scope
- Create .NET Web API project under `20260606_fruit_robotics_news/apps/backend`
- Implement endpoint `GET /api/news`
  - Fetch RSS/Atom from configurable `NEWS_FEED_URL`
  - Parse items; map to DTO
  - Cache for ~5 minutes
  - Basic error handling
- Configure CORS for frontend origin(s)
- Add GitHub Action `20260606_fruit_robotics_news_api.yml`
  - Build + publish
  - Deploy to Web App

### Acceptance criteria
- `GET /api/news` returns JSON with at least title+url
- Runs locally
- Deploy workflow succeeds

## Task 3 — Frontend (SWA)
**Goal:** Fruit-themed site that displays robotics news.

### Scope
- Create Vite+React app under `20260606_fruit_robotics_news/apps/frontend`
- UI
  - Fruit header/branding
  - News list with link
  - Refresh button
  - Loading + error state
- Config
  - `VITE_API_BASE_URL` env var
- Add GitHub Action `20260606_fruit_robotics_news_frontend.yml`
  - Build
  - Deploy to SWA using deployment token secret (no repo integration)

### Acceptance criteria
- Frontend loads and displays items from API
- Deployed SWA calls deployed API
