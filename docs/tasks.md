# Robotics News — Tasks

## 0. Repo / Docs
- [ ] Add `docs/design.md`
- [ ] Add `docs/tasks.md`

## 1. Backend: RoboticsNews.Api (.NET 10)
- [ ] Scaffold ASP.NET Core Web API project
- [ ] Add model for `NewsArticle`
- [ ] Add mocked JSON data file (or inline seed)
- [ ] Implement `GET /api/news`
- [ ] Enable CORS (allow SWA origin; permissive for local dev)
- [ ] Add basic health endpoint `GET /health` (optional)

## 2. Frontend: Azure Static Web Apps (plain)
- [ ] Create `apps/robotics-news/frontend/index.html`
- [ ] Create `styles.css` + minimal layout
- [ ] Create `app.js` to fetch from API and render list
- [ ] Add loading + error UI

## 3. Verification
- [ ] `dotnet build` + `dotnet test` (if any)
- [ ] Local smoke: run API and load frontend; confirm list renders

## 4. Azure wiring (next)
- [ ] Add GitHub Actions workflows (SWA + Web App)
- [ ] Add app settings: API URL for frontend, CORS origin for backend
