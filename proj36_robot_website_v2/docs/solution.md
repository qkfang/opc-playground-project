# proj36 — Cogsworth Robotics 2.0 (simple robot website v2) — Solution Design

## Goal
Build a simple, polished, browser-based **robot website v2** that is a clear evolution of
proj35 (Cogsworth Robotics v1). Lightweight MVP, fast to build and verify, deployable to
Azure Static Web Apps after QA PASS.

## What changed vs v1 (the "v2" story)
v1 was a clean single-page brochure (hero + 3 robots + features + how + specs + contact).
v2 keeps that solid base and **levels it up** with real interactivity and stronger polish:

| Area | v1 | v2 (this build) |
| --- | --- | --- |
| Brand | "Cogsworth Robotics" | "Cogsworth Robotics **2.0**" — refreshed identity + "v2" badge |
| Theme | Dark only | **Dark/Light theme toggle**, persisted in `localStorage`, respects `prefers-color-scheme` |
| Robots | 3 static cards | **4 robots** (adds **Aero** drone) + **filter chips** (All / Home / Lab / Outdoor / Air) |
| Stats | Static text badges | **Animated count-up** stat counters (trigger on scroll) |
| Hero bot | Simple CSS mascot | Upgraded mascot with **scanning eye sweep**, antenna pulse, parallax-lite float |
| New: Configurator | — | **"Build your bot"** interactive picker — choose model + 3 add-ons, see **live price + battery estimate** |
| New: Testimonials | — | 3 quote cards (social proof) |
| New: FAQ | — | Accessible **accordion** (`<details>`-free, button + aria-expanded), 5 Q&A |
| Specs | 6×3 table | Expanded **8-spec × 4-robot** comparison table |
| Nav | 6 links | Updated nav incl. **Build** + theme toggle + sticky shrink-on-scroll |
| UX extras | — | **Back-to-top** button, scroll progress bar, refined motion + focus states |
| Contact | Client-validated form | Same robust client-side validation + success (kept, no backend) |

Net effect: same "no build step, no backend" simplicity, but it *reads and feels* like a 2.0.

## Scope (in / out)
**In:** single-page static site, fully client-side, responsive, accessible, progressive
enhancement (usable with JS off), deploy scaffolding for Azure SWA.
**Out:** real backend/API, database, auth, payments, e-commerce checkout, multi-page routing,
analytics. The configurator and contact form are **demo-only** (no data leaves the browser).

## Tech choices
- **Pure static site**: `apps/web/index.html` + `styles.css` + `js/main.js`. **No framework, no
  build step, no dependencies** (mirrors proj35/proj34 — ships straight to Azure SWA Free).
- Vanilla ES5-safe JS in an IIFE for progressive enhancement.
- Pure CSS/SVG for all visuals (robot mascot, avatars, icons) — **no binary image assets** except
  a vector `favicon.svg`.
- Design system via CSS custom properties (`:root` + `[data-theme]`), mobile-first with
  breakpoints at 640 / 880 px, `prefers-reduced-motion` honored.

## Structure
```
proj36_robot_website_v2/
  apps/web/
    index.html
    styles.css
    js/main.js
    assets/favicon.svg
    staticwebapp.config.json
  bicep/main.bicep                 # Azure Static Web App (Free), rg-playground-01
  docs/{solution,task,todo}.md
  scripts/{serve,smoke}.mjs        # zero-dep local server + headless smoke test
  package.json
  README.md
.github/workflows/
  proj36_robot_website_v2_infra.yml   # token-based SWA provision (no repo integration)
  proj36_robot_website_v2_deploy.yml  # upload apps/web, skip_app_build, live smoke
```

## Deployment (handled by yoshi after QA PASS — MANDATORY)
- Azure Static Web Apps, **Free** tier, in `rg-playground-01`.
- baseName = `proj36` (drives resource names + workflow prefix).
- Token-based deploy via GitHub Actions (NO repo integration), `skip_app_build: true`.

## Acceptance (high level)
- Home returns 200 with a `<title>` mentioning the brand + "2.0".
- All primary sections render; nav anchors resolve; theme toggle works + persists.
- 4 robot cards + filter chips work; configurator updates live price/battery; FAQ accordion
  expands/collapses accessibly; counters animate; contact form validates + shows success.
- Responsive (mobile hamburger + single-column reflow, no horizontal overflow); zero console errors.
