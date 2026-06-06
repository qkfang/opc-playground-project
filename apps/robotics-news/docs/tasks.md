# Robotics News — Tasks

Status legend: [ ] todo, [~] in progress, [x] done

## Design
- [x] Write design doc (`docs/design.md`)
- [x] Confirm stack for SWA frontend (plain HTML/CSS/JS)

## Backend (.NET API)
- [x] Scaffold API project
- [x] Add `/api/news` endpoint returning RSS-backed normalized data
- [x] Add `/api/news/robotics` endpoint (`?count=N`) returning RSS-backed normalized data
- [x] Add health endpoints `/health` and `/healthz`
- [x] Add minimal config for allowed origins (prod)

## Frontend (SWA)
- [x] Scaffold SWA frontend app
- [x] Implement news list UI + refresh
- [x] Wire to API base URL via env var
- [ ] Local dev instructions

## Infra (Azure)
- [x] Bicep: resource group target `rg-playgound-01`
- [x] Create App Service Plan + Web App for API
- [x] Create Static Web App for frontend
- [x] App settings: API base URL for SWA build; CORS origins for API

## CI/CD
- [x] GitHub Actions: build/test API
- [x] GitHub Actions: deploy API to Web App
- [x] GitHub Actions: build/deploy SWA

## Validation
- [x] Local smoke test (API + frontend)
- [ ] Deployed smoke test + screenshots
