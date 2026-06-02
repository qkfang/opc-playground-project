# opc-project-1 — Robotics Website

A fast, multi-page robotics project website built with **vanilla HTML/CSS/JS**, deployed on **GitHub Pages**.

## Overview

This repo contains a polished four-page site for a robotics team, lab, or project:

| Page | File | Purpose |
|------|------|---------|
| **Home** | `index.html` | Hero section, project overview, quick-links |
| **Projects** | `projects.html` | Project cards with tags, status, and links |
| **Updates** | `updates.html` | Reverse-chronological changelog |
| **Contact** | `contact.html` | Email links + a static contact form |

- Responsive layout and modern dark styling (robotics aesthetic)
- Interactive telemetry demo on the Home page
- All content stored in **`site-data.js`** — one file to update everything

## Screenshots

![Desktop preview](assets/screenshot-desktop.png)

<details>
<summary>Mobile preview</summary>

![Mobile preview](assets/screenshot-mobile.png)
</details>

## Features

- **Fast + lightweight:** no framework, minimal JavaScript
- **Responsive:** looks good on desktop and mobile
- **Easy to edit:** update all projects and updates in `site-data.js`
- **GitHub Pages friendly:** deploy from `main` with a couple clicks

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
3. Save — Pages will publish the site at `https://<user>.github.io/opc-project-1/`

## How to edit content

### Add or edit a project

Open **`site-data.js`** and edit the `PROJECTS` array:

```js
var PROJECTS = [
  {
    title: "My Robot",
    description: "Short description of what this project does.",
    tags: ["ROS 2", "Navigation"],      // shown as pill badges
    status: "active",                   // active | in-progress | experimental | archived
    links: [
      { label: "GitHub", href: "https://github.com/..." },
      { label: "Docs",   href: "https://..." }
    ]
  },
  // …more entries
];
```

### Add or edit an update entry

In the same **`site-data.js`** file, edit the `UPDATES` array (newest first):

```js
var UPDATES = [
  {
    date: "2026-06-01",        // ISO date string — shown verbatim
    title: "Short headline",
    body:  "One-paragraph description of what happened."
  },
  // …more entries
];
```

## Customization checklist

- [ ] Project/team name + tagline in `index.html`
- [ ] About section copy in `index.html`
- [ ] Projects list in `site-data.js`
- [ ] Updates log in `site-data.js`
- [ ] Contact email in `contact.html` (replace `you@example.com`)
- [ ] Update colors/typography in `styles.css`
- [ ] Replace the favicon (`assets/favicon.svg`) and/or add images in `assets/`

