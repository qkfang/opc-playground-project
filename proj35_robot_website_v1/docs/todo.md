# TODO — proj35 Simple Robot Website v1

project_id: proj35
project_code: proj35_robot_website_v1

Legend: [x] done · [~] in progress · [ ] pending

## Setup
- [x] Create project folder structure (apps/web, bicep, docs, scripts)
- [x] Write solution.md (design + scope)
- [x] Write task.md (work breakdown)

## Build
- [x] index.html — semantic structure + all sections (hero, robots, features, how, specs, contact, footer)
- [x] styles.css — design system (CSS variables), responsive layout, CSS robot mascot/avatars
- [x] js/main.js — progressive enhancement (mobile nav, scroll-reveal, active nav link, form UX, year)
- [x] assets/favicon.svg — vector robot favicon
- [x] staticwebapp.config.json — SWA fallback/routing
- [x] package.json + README.md

## Infra / Deploy scaffolding (for yoshi)
- [x] bicep/main.bicep — Azure Static Web App (Free) in rg-playground-01
- [x] .github/workflows/proj35_robot_website_v1_infra.yml
- [x] .github/workflows/proj35_robot_website_v1_deploy.yml

## Verify (Coder)
- [x] scripts/smoke.mjs — headless static checks
- [x] scripts/serve.mjs — local static server
- [x] Run smoke test (expect PASS)
- [x] Browser verification + screenshot
- [x] Cross-check requirements vs solution.md

## Handoff
- [x] Update proj35.md (Development + Test evidence)
- [x] Append PROJECT-LOG.md
- [x] Commit + push to repo
- [x] Post build summary in Build topic
- [x] Strict sessions-handoff to toadette (QA)
