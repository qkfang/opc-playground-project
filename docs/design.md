# Design — Lego Robotics News (SWA frontend + .NET API backend)

## Goal
A simple “Lego Robotics News” website:
- Frontend: Azure Static Web Apps (SWA) serving a small web UI.
- Backend: .NET API hosted on Azure Web App.
- Feature: fetch and display a list of robotics-related news items (title, source, date, link).

## Assumptions (can change)
- No auth/login.
- Use public news feeds (RSS/Atom or public JSON endpoints). No paid APIs.
- Keep it simple: 1 page, 1 API endpoint.

## User experience
### Pages
- `/` Home: header + short description + “Refresh” button.
- News list:
  - Title (clickable)
  - Source name
  - Published date (local)
  - Short snippet (optional)

### UI style
- “Lego-ish” look: bright primary colors, blocky card style.
- Responsive, mobile-friendly.

## Backend API
### Endpoints
- `GET /api/news?limit=30`
  - Returns normalized list of news items.

### Response shape (v1)
```json
{
  "items": [
    {
      "id": "string",
      "title": "string",
      "url": "https://...",
      "source": "string",
      "publishedAt": "2026-06-06T00:00:00Z",
      "summary": "string"
    }
  ],
  "generatedAt": "2026-06-06T00:00:00Z"
}
```

### Data sources
Start with 3–6 robotics news sources. Prefer:
- Robotics Business Review (RSS)
- IEEE Spectrum Robotics (RSS)
- The Robot Report (RSS)
- NASA Robotics (if available)
- arXiv robotics category (if useful)

(We’ll validate actual feed URLs during implementation.)

### Normalization + caching
- Parse RSS/Atom -> normalize.
- Cache aggregated results in memory for ~10 minutes to avoid hammering feeds.
- Sort by `publishedAt` desc.
- Deduplicate by canonical URL.

## Deployment architecture
- Azure Static Web Apps: hosts static frontend.
- Azure Web App (Linux): hosts .NET API.
- Frontend calls API via `API_BASE_URL` env/config.

## Configuration
- Frontend config via build-time env var (or a small `config.json` served by SWA).
- API has no secrets.

## Observability
- Basic structured logs in API.
- (Optional) App Insights later.
