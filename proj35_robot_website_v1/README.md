# Cogsworth Robotics — Simple Robot Website v1 (proj35)

A polished but simple, **pure static** robot-themed marketing/landing site.
No build step, no framework, no dependencies — just HTML, CSS, and a little vanilla JS
for progressive enhancement. Ships as static assets to Azure Static Web Apps.

- **project_id:** proj35
- **project_code:** proj35_robot_website_v1
- **Brand (fictional):** Cogsworth Robotics — friendly home & lab robots (Helpa, Labbie, Rover-X)

## What's here

```
proj35_robot_website_v1/
  apps/web/
    index.html                # single-page site (hero, robots, features, how, specs, contact, footer)
    styles.css                # design system + responsive layout + CSS robot mascot/avatars
    js/main.js                # mobile nav, scroll-reveal, active nav link, smooth-scroll, form validation
    assets/favicon.svg        # vector robot favicon (no binary assets)
    staticwebapp.config.json  # SWA routing / SPA fallback
  scripts/
    serve.mjs                 # zero-dep local static server
    smoke.mjs                 # headless static smoke checks (structure / links / sections / config)
  bicep/main.bicep            # Azure Static Web App (Free) in rg-playground-01
  docs/                       # solution.md, task.md, todo.md
```

## Run locally

```bash
# from this folder
node scripts/serve.mjs           # then open http://127.0.0.1:4175/
node scripts/smoke.mjs           # headless static checks (exit 0 = pass)
# or via npm
npm run serve
npm run smoke
```

There is **no build step** — the contents of `apps/web/` are the deployable site.

## Features

- Sticky top navigation with smooth-scroll anchors, **active-section highlight**, and an
  accessible **mobile menu** (hamburger, `aria-expanded`, closes on link/Escape).
- Hero with an **animated pure-CSS robot mascot** (blink, float, status lights).
- **Meet the bots** — 3 robot cards (Helpa / Labbie / Rover-X) with CSS avatars + quick stats.
- **Features** grid (6), **How it works** (3 steps), **Specs** comparison table.
- **Contact** form with client-side validation (required + email pattern) and an inline
  success message. No backend — submission is acknowledged locally (MVP).
- Mobile-first responsive layout; respects `prefers-reduced-motion`; visible focus styles;
  the page is fully readable with JavaScript disabled (progressive enhancement).

## Accessibility

- Semantic landmarks (`header`/`nav`/`main`/`section`/`footer`), skip link, labelled form
  fields, `aria-invalid` on errors, `role="status"` success, keyboard-friendly focus management.

## Deploy (yoshi, after QA PASS)

Mandatory after QA PASS. Azure Static Web App (Free) in `rg-playground-01`:

1. `proj35_robot_website_v1_infra.yml` — `az deployment group create` on `bicep/main.bicep`.
2. `proj35_robot_website_v1_deploy.yml` — resolves the SWA deploy token via `az`, uploads
   `apps/web` with `skip_app_build: true` (no repo integration), then smoke-checks the live URL.

## Out of scope (MVP)

No backend/API/DB, no auth, no real form submission/email, no CMS or multi-page routing,
no binary image/audio assets (robots & icons are CSS/SVG).
