# opc-project-1 — Robotics Website

A simple, fast, one-page robotics project website (HTML/CSS/JS) designed to deploy cleanly on **GitHub Pages**.

## What’s inside

- `index.html` — landing page + sections (About, Robots, Demos, Team, Updates, Contact)
- `styles.css` — modern, responsive styling
- `script.js` — small interactive telemetry demo + updates timeline

## Run locally

Just open `index.html` in a browser.

Optional (recommended): run a tiny local server so assets load consistently.

```bash
python -m http.server 8000
```

Then visit: <http://localhost:8000>

## Deploy on GitHub Pages

1. Go to **Settings → Pages**
2. Under **Build and deployment** choose:
   - **Source:** Deploy from a branch
   - **Branch:** `main`
   - **Folder:** `/(root)`
3. Save — Pages will publish the site.

## Customize

Search for placeholders in `index.html` (names, links, email, robot specs, demo videos) and replace with your real content.
