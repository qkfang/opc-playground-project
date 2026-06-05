# Robotics News (SWA + .NET Web App) — Design

## Goal
A simple robotics website that shows a list of “robotics news” items.

- **Frontend**: Azure Static Web Apps (plain HTML/CSS/JS)
- **Backend**: ASP.NET Core Web API (.NET 10) on Azure Web App
- **Data**: mocked RSS articles stored as JSON in the API (no external feeds/keys)

## User Experience (MVP)
- Page: **Robotics News**
- On load:
  - Calls the backend API endpoint `GET /api/news`
  - Renders a list of articles (title, source, published date, short summary)
  - Each item links to `url` in a new tab
- Simple status:
  - Loading state
  - Error state

## API Contract
### `GET /api/news`
Returns a JSON array of article objects.

Article shape:
```json
{
  "id": "string",
  "title": "string",
  "source": "string",
  "url": "https://...",
  "publishedAt": "2026-06-05T00:00:00Z",
  "summary": "string",
  "tags": ["string"]
}
```

Notes:
- Backend enables CORS for the SWA origin.
- Versioning not required for MVP.

## Repo Layout (proposed)
- `apps/robotics-news/`
  - `frontend/` (SWA content)
  - `RoboticsNews.Api/` (.NET 10 Web API)

## Local Dev
- API:
  - `dotnet run` in `apps/robotics-news/RoboticsNews.Api`
  - Default: `http://localhost:5000`
- Frontend:
  - static files; can use `npx http-server` or similar
  - configure API base URL via a small `config.js` (or query param)

## Azure Deployment (later steps)
- **Azure Web App**: deploy the .NET API
- **Azure Static Web Apps**: deploy the static frontend
- Configure frontend to call API via:
  - a build-time constant (GitHub Actions env), or
  - `config.json`/`config.js` served with the site and edited per environment.
