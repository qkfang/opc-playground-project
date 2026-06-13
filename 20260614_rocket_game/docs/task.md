# Tasks — proj34 Star Ascent (20260614_rocket_game)

Build broken into 3 cohesive components (per App Dev Workflow Step 4).

## T1 — Game Engine (pure, headless-testable)
**File:** `apps/web/game/engine.js`
- Logical world (480x720), seeded RNG for determinism.
- `createGame`, `reset`, `update(state, dtMs, input)`, `fire`, helper geometry.
- Rocket movement from input, clamped to bounds.
- Asteroid spawning on a level-scaled timer; descent with per-level speed.
- Bullets: spawn (rate-limited), travel up, cull off-screen.
- Collisions: bullet×asteroid (destroy both, score, particles), rocket×asteroid (lose life, i-frames).
- Scoring: survival tick + destroy points; level = floor(score/LEVEL_STEP)+1.
- Game over when lives = 0; frozen until reset.
**Done when:** engine is DOM-free and importable in Node.

## T2 — Browser Shell + Rendering + Input
**Files:** `apps/web/index.html`, `apps/web/styles.css`, `apps/web/game/main.js`
- Canvas + HUD (score / level / lives / best) + Start/Pause/Game Over overlays.
- `requestAnimationFrame` loop with fixed-timestep accumulator feeding `engine.update`.
- Keyboard input (arrows/WASD move, Space shoot, P pause, Enter start/restart).
- Render starfield parallax, rocket, asteroids, bullets, particle bursts, invulnerability flash.
- Persist best score in `localStorage` (`star-ascent-best`).
- Responsive canvas scaling to viewport.
**Done when:** game is fully playable in a browser from static files (no build).

## T3 — Headless Smoke Test
**File:** `scripts/smoke.mjs` (Node, zero deps; run via `node scripts/smoke.mjs`)
Assertions:
1. Fresh game: status `ready`, 3 lives, score 0, level 1.
2. Start → status `playing`.
3. Move right increases rocket x; clamped at right bound (never exits world).
4. Move left clamped at left bound.
5. Asteroids spawn over time at level 1.
6. `fire` creates a bullet; rate-limit prevents spam within cooldown.
7. Bullet hits asteroid → both removed, score increases, particle created.
8. Rocket×asteroid → lives decrease, i-frames active, no second hit during i-frames.
9. Lives → 0 sets status `gameover`; subsequent `update` does not change score/lives (frozen).
10. Level increases as score crosses LEVEL_STEP.
11. Determinism: two games, same seed + same input/step sequence ⇒ identical score, lives, entity counts.
**Done when:** all assertions pass, script prints `SMOKE PASSED`.

## Deploy scaffolding (for yoshi; not run by toad)
- `bicep/main.bicep` — SWA (Free) in rg-playground-01.
- `apps/web/staticwebapp.config.json` — SPA fallback + 404.
- `.github/workflows/20260614_rocket_game_infra.yml` — provision SWA.
- `.github/workflows/20260614_rocket_game_deploy.yml` — upload `apps/web` via SWA token (skip_app_build).
