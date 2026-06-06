# 20260606_fruit_robotics_news — Tasks

## T1 — Backend (.NET Web API)
**Goal:** Provide `GET /api/news/robotics` returning a simple JSON list.

Deliverables:
- ASP.NET Core Web API project in `20260606_fruit_robotics_news/apps/backend`
- Endpoint `GET /api/news/robotics?count=10`
- Fetch robotics news from an RSS/Atom feed using `HttpClient`
- Basic in-memory cache (e.g., 5 minutes) to avoid hammering the feed
- CORS configured to allow SWA origin
- Unit/light test for parser (optional)

Verification:
- `dotnet test` (if tests exist)
- `dotnet run` and `curl` endpoint returns valid JSON

## T2 — Frontend (SWA)
**Goal:** Fruit-themed site that shows robotics news and refreshes.

Deliverables:
- Frontend app in `20260606_fruit_robotics_news/apps/frontend`
- Calls backend API URL from env/config
- UI: header, refresh button, list of items, loading/error

Verification:
- `npm test` (if any)
- `npm run build`

## T3 — CI/CD + Infra
**Goal:** Deploy SWA + Web App via GitHub Actions.

Deliverables:
- Bicep in `20260606_fruit_robotics_news/bicep` to provision:
  - App Service Plan + Web App
  - Azure Static Web App
- GitHub Actions workflows:
  - `20260606_fruit_robotics_news_infra.yml`
  - `20260606_fruit_robotics_news_deploy_backend.yml`
  - `20260606_fruit_robotics_news_deploy_frontend.yml` (uses SWA deployment token)

Verification:
- Workflows succeed

## T4 — Deploy + Smoke test
- Deploy infra to `rg-playground-01`
- Deploy backend then frontend
- Validate site loads and refresh works
- Capture screenshots
