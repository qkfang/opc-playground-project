# proj38 — Tasks

Build order (kept to 3 meaningful components + verify/deploy-prep):

## T1 — Deterministic game engine (`apps/web/game/engine.js`)
- [x] State machine: `ready → playing → (won|lost)`, pause flag.
- [x] World model: line of N legionaries (cohort), aim cursor, dragon (pos/HP/state), pila, fire zones, particles.
- [x] Input handling: move aim left/right with clamp; throw volley with cooldown.
- [x] Dragon AI: hover/circle, telegraphed fire-breath sweep, dive-bomb a legionary, enrage ramp by HP.
- [x] Combat: pila vs dragon → HP down + score; dragon attacks → cohort strength down (i-frames per soldier).
- [x] Win (dragon HP 0) / Lose (cohort strength 0); score + time bonus; best score in/out.
- [x] Deterministic PRNG (seeded) so smoke tests are reproducible.
- [x] Pure JS, no DOM/three import → headless-testable.

## T2 — Voxel renderer + UI (`render.js`, `input.js`, `main.js`, `index.html`, `styles.css`)
- [x] Three.js scene via import map (pinned CDN): camera, lights, sky, ground courtyard.
- [x] Voxel castle (walls, crenellations, keep, towers, banners, torches) from box meshes.
- [x] Voxel legionaries (body/helmet/red crest/shield/spear) instanced along the line; highlight aim.
- [x] Voxel dragon (body, neck, head, wings, tail, glowing eyes) animated by engine state.
- [x] Pila projectiles, fire-breath telegraph + flames, hit/explosion particles.
- [x] HUD: dragon HP bar, cohort strength, score, best, level/enrage, controls hint.
- [x] Title / win / lose overlays with Restart; pause overlay.
- [x] Input: keyboard + pointer aim/throw + on-screen touch buttons.
- [x] Persistence: best score in `localStorage`.

## T3 — Scripts + infra + docs
- [x] `scripts/serve.mjs` (zero-dep static server), `scripts/smoke.mjs` (engine smoke).
- [x] `apps/web/staticwebapp.config.json` (routing + mime).
- [x] `bicep/main.bicep` (Azure Static Web App, Free), `package.json`.
- [x] `.github/workflows/proj38_voxel_roman_dragon_game_infra.yml` + `..._deploy.yml`.
- [x] `README.md`, this `task.md`, `todo.md`, `solution.md`.

## Verify
- [x] `node scripts/smoke.mjs` → all assertions pass (exit 0).
- [x] `node scripts/serve.mjs` + headless browser load → canvas present, no console errors, screenshot captured.

## Handoff
- [x] Update `shared-context/projects/proj38.md` with evidence; append `PROJECT-LOG.md`.
- [x] Commit + push to `main`; strict QA handoff to toadette.
