# Robotics News site — design

## Goal
A simple Robotics-focused website.

- Frontend: **Azure Static Web Apps (SWA)**
- Backend API: **.NET Web API** hosted on **Azure App Service (Web App)**
- Function: Fetch and display a list of robotics news items.

## User experience (MVP)
- Home page with:
  - Header + short intro
  - “Latest robotics news” list
  - Refresh button
  - Loading + error states
  - Each item shows: title, source, published date, short summary (optional), and link to full article

## Data source
Option A (recommended, simplest): **RSS** feed(s) from reputable robotics news sources.
- Pros: no API keys, stable, easy to parse.
- Cons: varies by publisher.

Option B: News API provider.
- Pros: consistent schema.
- Cons: API keys + quotas.

Assumption for MVP: Use **RSS** and start with **The Robot Report** RSS (or similar). Make the feed list configurable.

## Architecture
- `apps/web` (SWA): static SPA (likely React + Vite) that calls the backend.
- `apps/api` (App Service): ASP.NET Core Web API.

Flow:
1. Browser loads SWA frontend.
2. Frontend calls `GET /api/news` on the Web App.
3. API fetches RSS, normalizes into `NewsItem[]`, returns JSON.

## API contract
### GET /api/news
Query params:
- `limit` (optional, default 20, max 50)

Response: `200 OK`
```json
[
  {
    "title": "...",
    "url": "https://...",
    "source": "The Robot Report",
    "publishedAt": "2026-06-05T00:00:00Z"
  }
]
```

Errors:
- `502` if upstream RSS fetch/parsing fails.

## Backend design (.NET)
- Minimal API or Controller-based (either ok). MVP: Minimal API.
- Service:
  - `IRoboticsNewsService.GetLatestAsync(limit)`
  - Implementation fetches RSS via `HttpClient`, parses using `System.ServiceModel.Syndication` (or CodeHollow.FeedReader).
- Caching:
  - In-memory cache for ~5 minutes to avoid hammering upstream.
- CORS:
  - Allow SWA origin.

## Frontend design
- Simple responsive layout.
- Calls backend via environment variable:
  - `VITE_API_BASE_URL` (e.g., `https://<webapp>.azurewebsites.net`)
- Render list + link out.

## Deployment
- GitHub Actions:
  - SWA deploy workflow.
  - Web App build + deploy workflow using `azure/login@v1` and publish artifact.

## Observability
- Basic structured logs.
- App Service default logging.

## Security
- Public read-only endpoint.
- Consider rate limit later.
