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

---

## Feature update (2026-06-14): Feedback form + API + in-memory DB

Requested follow-up: *"add a feedback form and save results to API and to an in-memory db"*.
This adds the site's **first real backend** while staying on the same Azure Static Web App
(SWA managed Azure Functions — no separate hosting, still Free tier).

### What was added
- **New `apps/api` Azure Functions app (Node v4 programming model, `@azure/functions` v4):**
  - `POST /api/feedback` — validates `{ name, email, rating?, message }`, saves to an
    **in-memory store**, returns `201` with the created entry (email masked) + running total.
    Returns `400` with per-field errors on invalid input.
  - `GET /api/feedback?limit=N` — returns stored feedback **newest-first** (emails masked) + count.
  - `GET /api/health` — liveness + current store size.
  - **In-memory "DB"**: a module-level array in `src/store.js` (process-local; resets when the
    Functions host restarts — by design, no external DB). Validation, id generation, rating
    clamping (1–5), and email masking live there and are unit-tested (`node --test`, 6/6).
- **Frontend `#feedback` section** (in `apps/web/index.html` + `js/main.js` + `styles.css`):
  - Form (Name / Email / Rating select / Your feedback) that **POSTs JSON to `/api/feedback`**
    via `fetch`, shows inline success, clears on success, and surfaces server-side field errors.
  - **Live "Recent feedback" list** populated from `GET /api/feedback` on load and after each
    submit (newest first, star rating, masked email, timestamp, count badge).
  - Client-side validation mirrors the API; **graceful degradation** if `fetch`/API is unavailable.
  - Nav + footer get a **Feedback** link; theme-aware styling; accessible (`aria-live`, labelled fields).

### Deployment impact
- `proj36_robot_website_v2_deploy.yml` now passes `api_location: proj36_robot_website_v2/apps/api`
  to `Azure/static-web-apps-deploy@v1` (managed Functions build) and adds a live API smoke step
  (POST then GET `/api/feedback`). Infra workflow + SWA resource are unchanged (managed Functions
  are included with SWA Free). bicep header comment updated to reflect the API + in-memory store.

### Acceptance (feedback feature)
- `GET /api/health` → 200; `POST /api/feedback` valid → 201 (masked email + total);
  invalid → 400 with field errors; `GET /api/feedback` → 200, newest-first, masked emails.
- Browser: submitting the form saves and immediately shows the entry in the live list; empty
  submit shows field errors and does **not** call the API; zero console errors.
