# opc-project-1 — Robotics Website

A multi-page robotics project website built with **vanilla HTML/CSS/JS**, designed to deploy cleanly on **GitHub Pages**.

## Overview

The site has four pages:

| Page | File | Purpose |
|------|------|---------|
| **Home** | `index.html` | Hero section, about cards, quick-links |
| **Projects** | `projects.html` | Project cards with tags, status badges, and links |
| **Updates** | `updates.html` | Reverse-chronological build log with dates |
| **Contact** | `contact.html` | Direct links + a no-backend contact form |

## Screenshots

![Desktop preview](assets/screenshot-desktop.png)

<details>
<summary>Mobile preview</summary>

![Mobile preview](assets/screenshot-mobile.png)
</details>

## Features

- **Fast + lightweight:** no framework, minimal JavaScript
- **Responsive:** tested on desktop, tablet, and mobile viewports
- **Easy to edit:** update content directly in the HTML files
- **GitHub Pages friendly:** deploy from `main` with a couple of clicks

## Run locally

### Option A: Open the file

Open `index.html` in your browser.

### Option B (recommended): Run a tiny local server

```bash
python -m http.server 8000
```

Then visit: <http://localhost:8000>

## Deploy (GitHub Pages)

1. Go to **Settings → Pages**
2. Under **Build and deployment** choose:
   - **Source:** Deploy from a branch
   - **Branch:** `main`
   - **Folder:** `/(root)`
3. Save — Pages will publish the site at `https://<your-username>.github.io/opc-project-1/`

## How to edit content

### Home page (`index.html`)

- **Tagline / hero copy** — edit the `<h1>` and `<p class="lead">` inside `<section class="hero">`
- **About cards** — update the three `<article class="card">` elements inside `#about`
- **Quick-links** — the four `<a class="quick-link">` anchors in the `.quick-links` div
- **Stats** — change the numbers in `script.js` (the `setText("statRuns", …)` calls in `boot()`)
- **Telemetry demo** — adjust simulation logic in `runTelemetryDemo()` in `script.js`

### Projects page (`projects.html`)

Each project is an `<article class="project-card">` block inside `.project-grid`. To add a project:

1. Copy an existing `<article class="project-card">…</article>` block.
2. Update:
   - `<h2 class="project-card__title">` — project name
   - `<span class="status status--active">` — status badge (`status--active`, `status--in-progress`, or `status--complete`)
   - `<p class="project-card__desc">` — short description
   - `<span class="tag">` elements — technology tags
   - `<a>` links inside `.project-card__links` — repo, demo, paper

### Updates page (`updates.html`)

Each entry is an `<article class="update">` block inside `.timeline`. To add an update:

1. Copy an existing `<article class="update">…</article>` block.
2. Insert it **at the top** of the `.timeline` div (newest first).
3. Set:
   - `<div class="update__date">` — date in `YYYY-MM-DD` format
   - `<div class="update__title">` — short title
   - `<p class="update__body">` — description paragraph

### Contact page (`contact.html`)

- **Email / social links** — update the `<a class="contact-channel">` anchors and their `href` values
- **Form action** — change `action="mailto:you@example.com"` on the `<form>` to your real email
- **Backend form** — replace the `mailto:` action with a service like [Formspree](https://formspree.io/) or [Netlify Forms](https://www.netlify.com/products/forms/) if you want submissions delivered without opening the email client

### Colors and typography (`styles.css`)

All design tokens are CSS custom properties at the top of `styles.css`:

```css
:root {
  --bg: #070A12;        /* page background */
  --accent: #3B82F6;    /* primary accent (blue) */
  --accent2: #60A5FA;   /* lighter accent */
  /* … */
}
```

### Favicon (`assets/favicon.svg`)

Replace `assets/favicon.svg` with your own SVG logo.

## Optional tweaks

- [ ] Replace placeholder email and social links in `contact.html`
- [ ] Update project entries in `projects.html`
- [ ] Add more update entries in `updates.html`
- [ ] Update the team / stats on the home page
- [ ] Swap `assets/favicon.svg` for your real icon
- [ ] Update colors/typography in `styles.css`

