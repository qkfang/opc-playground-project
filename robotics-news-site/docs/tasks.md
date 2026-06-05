# Robotics News site — tasks

Status legend: [ ] todo, [~] in progress, [x] done

## 0) Repo scaffolding
- [ ] Add `robotics-news-site/` project folder
- [ ] Add docs (`docs/design.md`, `docs/tasks.md`)

## 1) Backend API (.NET Web API)
- [ ] Create `apps/api` ASP.NET Core Web API project
- [ ] Implement `GET /api/news?limit=` returning normalized list
- [ ] RSS fetch + parse
- [ ] Add 5-min in-memory cache
- [ ] Add health endpoint `GET /health`
- [ ] Add CORS configuration (allow SWA origin via config)
- [ ] Add unit test for parsing (optional MVP)

## 2) Frontend (Azure Static Web Apps)
- [ ] Create `apps/web` (React+Vite)
- [ ] UI: header + list + refresh
- [ ] Wire API base URL via env var
- [ ] Basic loading/error states

## 3) Infra + CI/CD
- [ ] Bicep: resource group `rg-playgound-01` (existing) + resources for:
  - [ ] App Service plan + Web App for API
  - [ ] SWA resource
- [ ] GitHub Actions: deploy API to Web App
- [ ] GitHub Actions: deploy frontend to SWA
- [ ] Configure SWA env var for API base URL

## 4) Validation
- [ ] Local smoke: run API + web
- [ ] Deployed smoke: load site, confirm list loads
- [ ] Capture screenshots
