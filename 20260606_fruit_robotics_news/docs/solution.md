# 20260606_fruit_robotics_news — Solution

## Goal
Build a simple **fruit-themed website** (frontend) that shows a **list of robotics news** pulled from a **.NET API**.

## Architecture
- **Frontend:** Azure Static Web Apps (SWA)
  - A simple web UI (React or plain HTML/JS) that calls the backend API.
- **Backend:** Azure App Service (Web App) running **ASP.NET Core Web API**
  - Exposes `GET /api/news/robotics` which returns a normalized list of items.
  - Fetches robotics news from an RSS/Atom source (or other public feed), maps to a simple DTO, and returns JSON.

## Data source (assumption)
Use a public RSS feed for robotics news. Candidate sources (finalize during implementation):
- The Robot Report (RSS)
- IEEE Spectrum Robotics (RSS)
- ROS Discourse / Robotics category (if available)

## API contract (draft)
`GET /api/news/robotics?count=10`

Response:
```json
{
  "source": "<string>",
  "items": [
    {
      "title": "...",
      "url": "...",
      "published": "2026-06-06T00:00:00Z",
      "summary": "..."
    }
  ]
}
```

## Frontend UX (draft)
- Fruit header/branding (simple styling)
- “Refresh” button
- List of news cards (title link, published date, short summary)
- Loading + error states

## Deployment
- Infra via GitHub Actions:
  - SWA created and deployed with **deployment token** (no repo integration).
  - Web App created and deployed from workflow.
- Target resource group: `rg-playground-01`

## Non-goals
- Auth
- Database
- Advanced caching

## Open questions
- Preferred frontend stack: React vs vanilla HTML/JS?
- Preferred robotics news source (RSS URL)?
