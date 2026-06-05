# Robotics News — Tasks

Status legend: [ ] todo, [~] in progress, [x] done

## Design
- [x] Write design doc (`docs/design.md`)
- [x] Confirm stack for SWA frontend (React/Vite recommended)

## Backend (.NET API)
- [x] Scaffold API project
- [x] Add `/api/news` endpoint returning mock JSON
- [ ] Add health endpoint `/healthz`
- [ ] Add minimal config for allowed origins (prod)

## Frontend (SWA)
- [x] Scaffold SWA frontend app
- [x] Implement news list UI + refresh
- [x] Wire to API base URL via env var
- [x] Local dev instructions

## Infra (Azure)
- [ ] Bicep: resource group target `rg-playgound-01`
- [ ] Create App Service Plan + Web App for API
- [ ] Create Static Web App for frontend
- [ ] App settings: API base URL for SWA build; CORS origins for API

## CI/CD
- [ ] GitHub Actions: build/test API
- [ ] GitHub Actions: deploy API to Web App
- [ ] GitHub Actions: build/deploy SWA

## Validation
- [ ] Local smoke test (API + frontend)
- [ ] Deployed smoke test + screenshots
