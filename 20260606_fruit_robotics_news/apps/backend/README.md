# 20260606_fruit_robotics_news backend

ASP.NET Core Web API endpoint:

- `GET /api/news/robotics?count=10`

## Robotics feed source

This API fetches robotics news from these public feeds:

- `https://www.therobotreport.com/feed/`
- `https://news.google.com/rss/search?q=robotics`

## Environment variables

- `ALLOWED_ORIGINS`: comma-separated CORS allow-list (for SWA origin), for example:
  - `http://localhost:4280,https://<your-swa-domain>`

## Run locally

```bash
cd 20260606_fruit_robotics_news/apps/backend
dotnet restore
dotnet build
ALLOWED_ORIGINS=http://localhost:4280 dotnet run
```

Then call:

```bash
curl "http://localhost:5123/api/news/robotics?count=10"
```
