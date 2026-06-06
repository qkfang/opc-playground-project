# 20260606_fruit_robotics_news — Solution

## Goal
Build a simple **Fruit** themed website that shows a list of **robotics news**.

- **Frontend**: Azure Static Web Apps (SWA)
- **Backend**: .NET Web API hosted on **Azure App Service (Web App)**

## User experience
- Home page with a simple Fruit theme
- "Latest Robotics News" list
  - Title (link to source)
  - Source + published date (if available)
  - Short summary/description (if available)
- Refresh button
- Basic error/loading states

## Architecture
Browser (SWA Frontend) → calls .NET API (App Service) → API fetches robotics news from an RSS/Atom feed (server-side).

### Why RSS/Atom
Simplest reliable way to fetch "news" without needing API keys.

## Data source (assumption)
Use one public robotics RSS feed (can be swapped later). Example candidates:
- IEEE Spectrum Robotics RSS
- Robotics Business Review RSS
- Google News RSS query for robotics

(We will pick one feed URL during implementation; it will be configurable via app settings.)

## Backend API
### Endpoints
- `GET /api/news`
  - Returns JSON list of items

### Response shape (proposed)
```json
{
  "items": [
    {
      "title": "...",
      "url": "https://...",
      "source": "IEEE Spectrum",
      "published": "2026-06-06T03:12:00Z",
      "summary": "..."
    }
  ]
}
```

### Backend responsibilities
- Fetch RSS/Atom feed
- Parse items
- Map to stable JSON DTO
- Cache responses briefly (e.g., 5 minutes) to avoid rate limits
- Enable CORS for the SWA frontend origin

## Frontend
- Static SPA (recommended: React + Vite, simple UI)
- Configured with API base URL (environment-based)
- Calls backend and renders list

## Deployment
- Infra via Bicep into `rg-playground-01`
  - App Service Plan + Web App for API
  - Static Web App for frontend
- GitHub Actions
  - `{project_id}_infra.yml` for infra deploy
  - `{project_id}_api.yml` for API build+deploy
  - `{project_id}_frontend.yml` for SWA build+deploy via **deployment token**

## Non-goals
- Auth
- User accounts
- Complex CMS
- DR/HA

## Open questions (can decide later)
- Final RSS feed URL (we will default to one and make it configurable)
- Branding details (colors/logo)
