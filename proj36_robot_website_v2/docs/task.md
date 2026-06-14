# proj36 — Task Breakdown (Cogsworth Robotics 2.0)

Build split into 3 logical components (kept small + verifiable):

## Task 1 — Structure, design system & shell
- Project folders (apps/web, bicep, docs, scripts) + package.json + README.
- `index.html` skeleton: head (title/description/theme-color/favicon), skip link, sticky
  header with brand + primary nav + **theme toggle**, `<main>`, footer.
- `styles.css` design system: `:root` light/dark tokens via `[data-theme]`, layout container,
  buttons, typography, responsive breakpoints (640/880), reduced-motion, scroll-reveal classes,
  scroll-progress bar, back-to-top.
- Acceptance: page loads, nav + theme toggle present, no console errors.

## Task 2 — Content sections (the v2 substance)
- **Hero** with upgraded animated CSS robot mascot + count-up stat badges + 2 CTAs.
- **Robots** showcase: 4 cards (Helpa/Labbie/Rover-X/**Aero**) + **filter chips** (All/Home/Lab/Outdoor/Air).
- **Build your bot** configurator: model select + 3 toggle add-ons → live **price + battery** estimate.
- **Features** grid (6 tiles), **How it works** (3 steps).
- **Specs** expanded comparison table (8 specs × 4 robots).
- **Testimonials** (3 quote cards), **FAQ** accessible accordion (5 items).
- **Contact** client-validated form (name/email/message) + inline success + footer.
- Acceptance: all sections + ids present, anchors resolve, content matches smoke expectations.

## Task 3 — Behaviour, scripts, infra & verification
- `js/main.js`: theme toggle (persist + system default), mobile nav, scroll-reveal +
  active-link (IntersectionObserver), count-up animation, robot filter, configurator math,
  FAQ accordion, contact validation, back-to-top + scroll progress, footer year.
- `scripts/serve.mjs` (zero-dep static server) + `scripts/smoke.mjs` (headless asserts).
- `staticwebapp.config.json` (SPA fallback + 404 + mime).
- `bicep/main.bicep` + `proj36_..._infra.yml` + `proj36_..._deploy.yml` for yoshi.
- Acceptance: `node scripts/smoke.mjs` all pass; browser checks green; screenshots captured.

## Owners / flow
- toad: Tasks 1–3 (build + dev-test) → handoff to toadette.
- toadette: independent QA → on PASS, mandatory handoff to yoshi.
- yoshi: deploy to Azure SWA + verify live.
