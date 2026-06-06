# opc-project-1 — Robotics GitHub Pages Site

A robotics-themed static site built with **vanilla HTML/CSS/JS** for **GitHub Pages**.

## Overview

The site is organized into the four requested sections:

- **Home** — hero section, intro copy, and quick links
- **Projects** — rendered project cards with tags, status, and links
- **Updates** — reverse-chronological dated log
- **Contact** — social links plus a small no-backend email form

## Screenshots

![Desktop preview](assets/screenshot-desktop.png)

<details>
<summary>Mobile preview</summary>

![Mobile preview](assets/screenshot-mobile.png)
</details>

## Editing content

### Update projects and updates

Edit `site-data.js`:

- `projects` controls the cards shown in the **Projects** section
- `updates` controls the reverse-chronological entries shown in **Updates**

Each project supports:

- `title`
- `description`
- `tags`
- `status`
- `links`

Each update supports:

- `date`
- `title`
- `body`
- `highlights`

### Update home/contact copy

Edit `index.html` for:

- hero text and quick-link copy
- contact links

### Update styling/behavior

- `styles.css` controls the layout and visual theme
- `script.js` controls rendering, nav highlighting, telemetry demo, and the email-form behavior (including the contact recipient constant)

## Run locally

### Option A: Open the file

Open `index.html` in your browser.

### Option B (recommended): Run a tiny local server

```bash
python -m http.server 8000
```

Then visit: <http://localhost:8000>

## GitHub Pages configuration

This site is designed to deploy directly from the repository root.

1. Go to **Settings → Pages**
2. Under **Build and deployment** choose:
   - **Source:** Deploy from a branch
   - **Branch:** `main`
   - **Folder:** `/(root)`
3. Save to publish the site

The published home page is `index.html`, so visiting the Pages URL lands on the **Home** section by default.

## Visual verification

Install dependencies and regenerate the desktop/mobile previews with:

```bash
npm install
npx playwright install chromium
node scripts/take-screenshots.mjs
```
