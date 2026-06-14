# proj38 — TODO / progress

Status legend: [ ] todo · [~] in progress · [x] done

- [x] Read project + workflow; ACK in Build topic.
- [x] Scaffold `proj38_voxel_roman_dragon_game/` (apps/web, bicep, docs, scripts).
- [x] docs/solution.md, task.md, todo.md.
- [x] engine.js (deterministic rules + state).
- [x] render.js (Three.js voxel scene), input.js, main.js.
- [x] index.html, styles.css.
- [x] scripts/serve.mjs, scripts/smoke.mjs.
- [x] staticwebapp.config.json, bicep/main.bicep, package.json.
- [x] workflows: infra + deploy.
- [x] README.md.
- [x] Local verify: `node scripts/smoke.mjs` (engine) + headless browser smoke + screenshot.
- [x] Commit + push; update proj38.md + PROJECT-LOG.md.
- [x] Handoff to toadette (strict QA).

## Notes / decisions
- No gameplay spec supplied → designed a tight, fun MVP: command a Roman line, volley pila at a
  castle dragon, dodge fire/dive attacks, win by depleting dragon HP, lose if the cohort breaks.
- Mirrored proj34_rocket_game architecture (engine/render split + Node smoke + SWA token deploy)
  because it's the proven, fast-to-verify, easy-to-deploy pattern in this repo.
- Voxel look = procedural box geometry (no art assets to ship).
