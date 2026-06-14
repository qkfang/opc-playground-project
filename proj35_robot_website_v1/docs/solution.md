# Solution Design — proj35 Simple Robot Website v1 ("Cogsworth Robotics")

project_id: proj35
project_code: proj35_robot_website_v1
owner: toad (Coder)

## 1. Goal

A polished but simple **browser-based robot website v1** — a friendly robotics-themed
marketing/landing site that runs entirely client-side, builds/verifies quickly, and
deploys to Azure Static Web Apps after QA PASS.

## 2. Concept — "Cogsworth Robotics"

A clean, friendly robotics brand landing page for a fictional company that makes helpful
home & lab robots. Bright, modern, approachable — not a game, not a store. Just a crisp v1
single-page site with clear sections and navigation a human can scan in seconds.

- **Theme:** friendly robotics — soft gradients, rounded cards, an animated robot mascot
  drawn in pure CSS/SVG (no image assets, no dependencies).
- **Tone:** approachable, confident, simple.

## 3. MVP Feature Checklist (maps to requirements in proj35.md)

| Requirement | Implementation |
| --- | --- |
| Landing page | Single-page site (`index.html`) with a strong hero section |
| Robot-themed sections | Hero, "Meet the bots" showcase (3 robot cards), Features, How-it-works, Specs, CTA/Contact |
| Clear navigation | Sticky top nav with smooth-scroll anchor links + active-section highlight; mobile hamburger menu |
| Responsive browser layout | Mobile-first CSS, fl/grid layouts that reflow; tested at narrow + wide widths |
| Clean v1 presentation | Cohesive design system (CSS variables), consistent spacing/typography, accessible markup |
| Deployment-ready | Pure static assets + `staticwebapp.config.json`; bicep + GitHub Actions for yoshi |

### Additional polish (still MVP-tight)
- An **animated CSS robot mascot** in the hero (blinking eyes, subtle float) — pure CSS, no JS required for it.
- **Scroll-reveal** animations on sections via `IntersectionObserver` (graceful: content visible even if JS disabled).
- **Active nav link** highlighting based on scroll position (`IntersectionObserver`).
- **Mobile nav** toggle (accessible button, `aria-expanded`).
- A tiny **contact form** that validates client-side and shows an inline success message
  (no backend — MVP; submission is intercepted and acknowledged locally).
- Footer with year + brand. Respects `prefers-reduced-motion`.

## 4. Tech Choices

- **Pure static site**: `index.html` + `styles.css` + a small `js/main.js` (progressive enhancement).
  **No build step, no framework, no dependencies.**
  - Rationale: smallest meaningful, fastest-to-verify, polished v1; ships as static assets to
    Azure Static Web Apps exactly like proj34 (rocket game) but it's a content site, not a game.
- **Semantic HTML5** (`<header> <nav> <main> <section> <footer>`), accessible landmarks + labels.
- **CSS** with custom properties for the design system; flexbox + grid for layout; no preprocessor.
- **Vanilla JS** only for progressive enhancement (mobile nav, scroll-reveal, active link, form UX).
  The page is fully readable/usable with JS disabled.

## 5. Architecture

```
apps/web/
  index.html               # the whole site (single page, anchored sections)
  styles.css               # design system + responsive layout
  js/
    main.js                # progressive enhancement (nav, reveal, active link, form)
  assets/
    favicon.svg            # inline-friendly robot favicon (vector, no binary)
  staticwebapp.config.json # SWA routing/fallback (for yoshi deploy)
scripts/
  serve.mjs                # zero-dep local static server (browser verification)
  smoke.mjs                # headless static checks (Node, zero deps) — structure/links/sections
bicep/
  main.bicep               # Azure Static Web App (Free) — mirrors proj34 infra
docs/
  solution.md, todo.md, task.md
```

## 6. Page Sections (in order)

1. **Header / Nav** — brand mark + links: Home, Robots, Features, How it works, Specs, Contact.
2. **Hero** — headline, subcopy, two CTAs (primary "Meet the bots" → #robots, secondary "Contact"),
   animated CSS robot mascot.
3. **Robots showcase** (`#robots`) — 3 cards: *Helpa* (home helper), *Labbie* (lab assistant),
   *Rover-X* (outdoor explorer). Each: CSS robot avatar, name, one-liner, 3 quick stats.
4. **Features** (`#features`) — grid of 6 feature tiles (icon + title + blurb) e.g. Safe by design,
   All-day battery, Voice ready, Easy setup, Smart sensors, OTA updates.
5. **How it works** (`#how`) — 3 numbered steps: Unbox → Pair → Go.
6. **Specs** (`#specs`) — a clean comparison table of the 3 robots (battery, sensors, range, weight).
7. **Contact / CTA** (`#contact`) — short pitch + a validated contact form (name/email/message)
   with inline success state (local only).
8. **Footer** — brand, small print, © year (year filled by JS, static fallback present).

## 7. Out of Scope (MVP)

- No backend/API/DB, no auth, no real form submission/email, no CMS, no multi-page routing.
- No binary image/audio assets (robots & icons are CSS/SVG to stay dependency-free + fast to verify).

## 8. Verification Plan (Coder local checks — smallest meaningful)

1. `node scripts/smoke.mjs` — headless static assertions: required files exist; `index.html` contains
   all section ids and nav anchors that resolve to those ids; title/lang/viewport/favicon present;
   referenced `styles.css` / `js/main.js` exist; no obviously broken internal `#anchors`.
2. Browser verification (served statically via `scripts/serve.mjs`): page renders; nav smooth-scrolls
   to each section; active link updates; mobile menu toggles at narrow width; contact form validates
   (empty → error, valid → inline success); responsive at mobile + desktop widths. Screenshot captured.

## 9. Deployment (yoshi, mandatory after QA PASS)

- Azure Static Web App (Free) in `rg-playground-01` via `bicep/main.bicep`.
- GitHub Actions: build = none (static); upload `apps/web` directly with `skip_app_build: true`,
  SWA deploy token from `az` (no repo integration), mirroring the proj34 workflow.
