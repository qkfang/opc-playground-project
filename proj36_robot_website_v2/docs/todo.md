# proj36 — TODO (Cogsworth Robotics 2.0)

## Build (toad)
- [x] Scaffold `proj36_robot_website_v2/{apps/web,bicep,docs,scripts}` + package.json + README
- [x] Docs: solution.md / task.md / todo.md
- [x] `index.html` shell: head, skip link, sticky header + nav + theme toggle, footer
- [x] `styles.css` design system (light/dark tokens, layout, buttons, responsive, reduced-motion)
- [x] Hero + upgraded animated CSS robot mascot + count-up stat badges
- [x] Robots showcase: 4 cards + filter chips (All/Home/Lab/Outdoor/Air)
- [x] "Build your bot" configurator (model + add-ons → live price/battery)
- [x] Features grid + How it works steps
- [x] Specs expanded table (8×4)
- [x] Testimonials + FAQ accordion
- [x] Contact form (client validation + success)
- [x] `js/main.js` (theme, nav, reveal, active-link, counters, filter, configurator, FAQ, form, back-to-top)
- [x] `assets/favicon.svg` + `staticwebapp.config.json`
- [x] `scripts/serve.mjs` + `scripts/smoke.mjs`
- [x] `bicep/main.bicep` + infra/deploy GitHub Actions workflows
- [x] Dev-test: `node scripts/smoke.mjs` (all pass)
- [x] Browser verification (desktop + mobile) + screenshots to media/outbound
- [x] Update proj36.md + PROJECT-LOG.md; commit + push
- [x] Handoff to toadette (QA)

## QA (toadette)
- [ ] Independent smoke + browser verification (fresh checkout)
- [ ] Verdict PASS/FAIL → on PASS mandatory handoff to yoshi

## Deploy (yoshi)
- [ ] Provision SWA (infra workflow) + deploy apps/web (deploy workflow)
- [ ] Verify live + screenshot → report to toadcaptain
