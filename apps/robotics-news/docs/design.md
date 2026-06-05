# Robotics News — Design

## Goal
A simple robotics news site.

- Frontend: Azure Static Web Apps (SWA)
- Backend: .NET Web API on Azure App Service (Web App)
- Data: mocked JSON (for now)

## UX / UI
Single page:
- Header: “Robotics News”
- Button: “Refresh”
- States: loading, error, empty
- List of cards/rows:
  - Title (link)
  - Source + published time
  - Summary (optional)
  - Tags chips

## API
### `GET /api/news`
Returns latest robotics news items.

Response (200):
```json
[
  {
    "id": "rn_0001",
    "title": "...",
    "url": "https://...",
    "source": "Mock Robotics Daily",
    "publishedAt": "2026-06-05T07:12:00Z",
    "summary": "...",
    "tags": ["humanoid", "computer-vision"]
  }
]
```

CORS: allow SWA origin(s) (dev: allow all).

## Backend
- ASP.NET Core Web API
- `INewsService` abstraction
- `MockNewsService` returns in-memory list

## Frontend
- Minimal SPA (likely React + Vite) hosted on SWA
- Reads `VITE_API_BASE_URL` in local dev
- Production points to Web App base URL.

## Deployment (target)
- SWA: deploy from GitHub Actions
- Web App: deploy from GitHub Actions
- Infra: Bicep to `rg-playgound-01` using `sp-playgound-01` (per workflow)

## Later extensions
- Replace mock service with RSS aggregation
- Paging / caching
- Search/filter by tags
