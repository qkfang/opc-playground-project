# TODO — proj34 Star Ascent (20260614_rocket_game)

Tracks build progress for the browser rocket game MVP. Owner: toad.

## Build (toad)
- [x] Read proj34.md + WORKFLOW-BUILD + skills
- [x] Create project structure (apps/web, bicep, docs, scripts)
- [x] Write solution.md / todo.md / task.md
- [x] T1 — Pure game engine (`apps/web/game/engine.js`): state, update, collisions, scoring, lives, levels, determinism
- [x] T2 — Browser shell + render + input (`index.html`, `styles.css`, `game/main.js`): canvas, HUD, overlays, controls, localStorage best score
- [x] T3 — Headless smoke test (`scripts/smoke.mjs`) covering engine rules
- [x] Deploy scaffolding for yoshi: `bicep/main.bicep`, `staticwebapp.config.json`, GitHub Actions infra+deploy yml
- [x] Local verify: `node scripts/smoke.mjs` green
- [x] Local verify: browser play-test + screenshot
- [x] Update proj34.md (Development + Test evidence), append PROJECT-LOG
- [x] Commit to repo
- [x] Post BUILT summary in Build topic; hand off to toadette (QA)

## QA (toadette) — after handoff
- [ ] Re-run smoke test
- [ ] Browser play-test (controls, threats, scoring, lives, game over/restart, best-score persistence)
- [ ] Screenshot
- [ ] Verdict PASS/FAIL in Build topic
- [ ] On PASS → hand off to yoshi (mandatory deploy)

## Deploy (yoshi) — mandatory after QA PASS
- [ ] Provision SWA via bicep in rg-playground-01
- [ ] Deploy `apps/web` static assets via GitHub Actions (SWA token)
- [ ] Verify live URL + screenshot; report to orchestrator
