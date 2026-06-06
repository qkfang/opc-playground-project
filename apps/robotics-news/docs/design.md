# Robotics News — Design

## Goal
A simple robotics news site.

- Frontend: Azure Static Web Apps (SWA)
- Backend: .NET Web API on Azure App Service (Web App)
- Data: robotics RSS feeds aggregated by the API

## UX / UI
Single page:
- Header: "Fruit Robotics News" with fruit emoji decorations (🍓🤖🍊)
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
- Supports `limit` query string (default 20, max 50).
- Invalid or non-positive `limit` values fall back to the default of 20.

### `GET /api/news/robotics`
Alias with `count` parameter (default 20, max 50).
- Supports `count` query string.

### `GET /healthz` (and `/health`)
Returns a simple health payload.

Response (200):
```json
[
  {
    "title": "...",
    "url": "https://...",
    "source": "Mock Robotics Daily",
    "publishedAt": "2026-06-05T07:12:00Z"
  }
]
```

CORS: allow SWA origin(s) (dev: allow all).

## Backend
- ASP.NET Core Web API
- `INewsService` abstraction
- `RssNewsService` fetches RSS feeds and caches normalized items for 5 minutes (configurable via `NewsFeeds__CacheDurationMinutes`)
- `GET /health` and `GET /healthz` return a simple health payload
- RSS feeds configured via `NewsFeeds__FeedUrls` app settings (default: The Robot Report, Robotics & Automation News)

## Frontend
- Plain HTML/CSS/JS frontend hosted on SWA
- Reads API base URL from `config.js`, which is written from `ROBOTICS_NEWS_API_BASE_URL` during deployment
- Production points to the Azure Web App base URL

## Deployment (target)
- SWA: deploy from GitHub Actions
- Web App: deploy from GitHub Actions
- Both workflows authenticate with `azure/login@v1` using `AZURE_CREDENTIALS`
- Infra: Bicep provisions resources in `rg-playground-01`

## Later extensions
- Replace mock service with RSS aggregation
- Paging / caching
- Search/filter by tags
