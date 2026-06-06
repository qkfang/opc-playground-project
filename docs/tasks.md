# Tasks — Lego Robotics News

## Build
- [ ] Create solution structure:
  - `apps/frontend/` static site (HTML/CSS/JS)
  - `apps/api/` .NET minimal API
- [ ] Implement API:
  - Feed list config
  - RSS/Atom fetch + parse
  - Normalize + dedupe + sort
  - In-memory cache
  - `GET /api/news`
- [ ] Implement frontend:
  - Fetch from API base URL
  - Render cards
  - Refresh button
  - Loading/error states

## Local verification
- [ ] Run API locally, hit `GET /api/news`
- [ ] Run frontend locally (simple static server), ensure it calls API

## Azure infra
- [ ] Create SWA
- [ ] Create Web App (Linux) for .NET 8
- [ ] Configure CORS on API to allow SWA origin

## CI/CD
- [ ] GitHub Action: deploy frontend to SWA via deployment token (no repo integration)
- [ ] GitHub Action: build+deploy .NET API to Web App

## Validate
- [ ] Deployed API endpoint works
- [ ] Deployed SWA page loads and shows items
- [ ] Screenshot evidence
