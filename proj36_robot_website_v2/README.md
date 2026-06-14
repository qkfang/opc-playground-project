# Cogsworth Robotics 2.0 — proj36 (simple robot website v2)

A polished, fully client-side **robot website v2** and a clear evolution of proj35 (v1).
No framework, no build step, no backend — just static `HTML/CSS/JS` that ships straight to
Azure Static Web Apps.

## What's new in 2.0 (vs proj35 v1)
- **Dark / light theme toggle** (persisted, respects `prefers-color-scheme`).
- **4th robot — Aero** (sky scout drone) + **filter chips** (All / Home / Lab / Outdoor / Air).
- **"Build your bot" configurator** — pick a model + add-ons, see **live price + battery**.
- **Animated count-up** hero stats, upgraded CSS robot mascot (scanning visor), **testimonials**,
  accessible **FAQ accordion**, expanded **8×4 specs** table, scroll-progress bar + back-to-top.

## Run locally
```bash
cd proj36_robot_website_v2
node scripts/serve.mjs        # http://127.0.0.1:4176/
node scripts/smoke.mjs        # headless static checks (exit 0 = pass)
```
No dependencies required (Node only, for the dev server + smoke test). The site itself is plain
static files under `apps/web/`.

## Structure
```
apps/web/        index.html, styles.css, js/main.js, assets/favicon.svg, staticwebapp.config.json
bicep/           main.bicep (Azure Static Web App, Free, rg-playground-01; baseName=proj36)
docs/            solution.md, task.md, todo.md
scripts/         serve.mjs (zero-dep server), smoke.mjs (headless smoke test)
```

## Deploy (Azure Static Web Apps — handled by yoshi after QA PASS)
Two `workflow_dispatch` GitHub Actions (token-based, **no repo integration**):
1. `proj36_robot_website_v2_infra.yml` — provisions the SWA via Bicep.
2. `proj36_robot_website_v2_deploy.yml` — uploads `apps/web` (`skip_app_build: true`) + live smoke.

## Notes
- Progressive enhancement: the page is fully readable/usable with JavaScript disabled.
- Configurator + contact form are **demo-only** — no data leaves the browser.
