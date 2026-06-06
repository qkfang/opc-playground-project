# 20260606_fruit_robotics_news backend

ASP.NET Core Web API endpoint:

- `GET /api/news/robotics?count=10`

## Robotics feed source

This API fetches robotics news from **The Robot Report RSS feed**:

- `https://www.therobotreport.com/feed/`

## Environment variables

- `ALLOWED_ORIGINS`: comma-separated CORS allow-list (for SWA origin), for example:
  - `http://localhost:4280,https://<your-swa-domain>`

## Run locally

```bash
cd /tmp/workspace/qkfang/opc-project-1/20260606_fruit_robotics_news/apps/backend
dotnet restore
dotnet build
ALLOWED_ORIGINS=http://localhost:4280 dotnet run
```

Then call:

```bash
curl "http://localhost:5123/api/news/robotics?count=10"
```
