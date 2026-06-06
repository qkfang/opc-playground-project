# Design — Fruit site + Robotics news

## Goal
A simple “fruit” themed website (Azure Static Web Apps) that also shows a **fresh list of robotics news** fetched from a **.NET API** running on **Azure App Service (Web App)**.

## Architecture
- **Frontend:** Azure Static Web Apps (SWA)
  - Static UI (simple HTML/JS or lightweight framework)
  - Calls backend via HTTPS to fetch news JSON
- **Backend:** ASP.NET Core Minimal API on Azure Web App
  - Endpoint fetches robotics news from a public source (RSS or JSON API)
  - Normalizes into a simple JSON shape for the frontend

## Frontend (SWA)
Pages:
- Home: Fruit hero section + CTA
- Fruit list: static list (Apple, Banana, Orange, etc.)
- Robotics news: renders latest items from API

API call:
- `GET {API_BASE_URL}/api/news/robotics?count=10`

Config:
- `VITE_API_BASE_URL` (if using Vite) or `API_BASE_URL` injected at build time

## Backend (.NET API)
Minimal API endpoints:
- `GET /healthz` → 200 OK
- `GET /api/news/robotics?count=10` → JSON list

News item model:
```json
{
  "title": "...",
  "url": "...",
  "source": "...",
  "publishedAt": "2026-06-06T01:23:45Z"
}
```

Implementation idea:
- Use `HttpClient` to fetch an RSS feed
- Parse with `System.ServiceModel.Syndication` (RSS/Atom)
- Cache in-memory for ~2-5 minutes to reduce upstream calls

## Deployment
- SWA: GitHub Actions with **deployment token** (no repo integration)
- Web App: GitHub Actions using `azure/login@v1` and `azure/webapps-deploy@v3`
- Both deployed into `rg-playground-01`

## Open questions (need Dan to confirm)
1) Which robotics news source should we use?
   - Option A (recommended): RSS feed (simple + reliable)
   - Option B: NewsAPI-style JSON (often needs an API key)
2) Any preference for frontend stack?
   - Option A: Plain HTML/CSS/JS (fastest)
   - Option B: React + Vite (still simple)
