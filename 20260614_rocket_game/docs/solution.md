# Solution Design — proj34 Browser Rocket Game ("Star Ascent")

project_id: proj34
project_code: 20260614_rocket_game
owner: toad (Coder)

## 1. Goal

A polished, self-contained **browser rocket game MVP** that runs entirely client-side,
builds/verifies quickly, and deploys to Azure Static Web Apps after QA PASS.

## 2. Game Concept — "Star Ascent"

A vertical arcade space shooter. You pilot a rocket climbing through an asteroid field.
Dodge and shoot incoming asteroids, survive as long as possible, and rack up a high score.

- **Theme:** clean arcade sci-fi (starfield background, neon rocket, glowing projectiles).
- **Perspective:** top-down vertical scroller; rocket near the bottom, threats descend from the top.

## 3. MVP Feature Checklist (maps to requirements in proj34.md)

| Requirement | Implementation |
| --- | --- |
| Rocket controls | Arrow keys / WASD move the rocket left/right/up/down within bounds |
| Obstacles / enemy threats | Asteroids spawn at the top and descend at increasing rate/speed |
| Scoring / progression | Score increases over time + per asteroid destroyed; difficulty ramps by level |
| Lives / health | 3 lives; collision with an asteroid costs a life + brief invulnerability |
| Game over / restart flow | Game Over overlay with final score + best score; restart with a key/button |
| Responsive keyboard controls | Smooth per-frame input handling; Space to shoot; P to pause; Enter to start/restart |

### Additional polish (still MVP-tight)
- Shooting: Space fires projectiles that destroy asteroids (+score, particle burst).
- Levels: every N points the level increases → faster/denser asteroids (progression).
- Best score persisted in `localStorage` (`star-ascent-best`).
- Pause/resume (P), Start and Game Over overlays, on-screen HUD (score / level / lives / best).
- Particle explosion effects, screen-relative starfield parallax.
- Mobile-friendly: canvas scales to viewport; on-screen note for desktop keyboard play.

## 4. Tech Choices

- **Pure static site**: single `index.html` + `styles.css` + ES module game scripts. **No build step, no framework, no dependencies.**
  - Rationale: smallest meaningful, fastest-to-verify MVP; ships as static assets to Azure Static Web Apps exactly like the survival game (proj17), but without a Next.js build.
- **HTML5 Canvas 2D** for rendering; `requestAnimationFrame` game loop with fixed-timestep update.
- Game logic split into a **pure, headless-testable engine module** (`game/engine.js`) so core rules
  can be verified with a Node smoke test (no browser needed) — mirrors the survival game's headless smoke approach.
- `game/main.js` wires the engine to Canvas + DOM + input (browser-only).

## 5. Architecture

```
apps/web/
  index.html            # game shell: canvas, HUD, overlays
  styles.css            # arcade sci-fi styling
  game/
    engine.js           # PURE game state + rules (no DOM) — unit/smoke testable
    main.js             # browser glue: canvas render, input, RAF loop, localStorage
  staticwebapp.config.json  # SWA routing/fallback (for yoshi deploy)
scripts/
  smoke.mjs             # headless engine smoke test (Node, zero deps)
bicep/
  main.bicep            # Azure Static Web App (Free) — mirrors survival game infra
docs/
  solution.md, todo.md, task.md
```

## 6. Core Engine Rules (engine.js — deterministic, testable)

- World is a fixed logical size (e.g. 480 x 720); rendering scales to canvas.
- `createGame(opts)` → state with rng seed for deterministic tests.
- `update(state, dtMs, input)`:
  - Move rocket from input (clamped to bounds).
  - Advance asteroids; spawn on a timer that tightens with level.
  - Advance bullets; cull off-screen.
  - Collisions: bullet×asteroid → destroy both (+score, particles); rocket×asteroid → lose life,
    remove asteroid, grant invulnerability window (no double-hit during i-frames).
  - Score: +1/sec survival baseline + points per asteroid destroyed; level = floor(score / LEVEL_STEP)+1.
  - Lives reach 0 → `state.status = 'gameover'`; further updates are frozen until reset.
- `fire(state)` spawns a bullet (rate-limited).
- Determinism: same seed + same input sequence ⇒ identical state (asserted in smoke test).

## 7. Controls

| Key | Action |
| --- | --- |
| ← / → / A / D | Move left / right |
| ↑ / ↓ / W / S | Move up / down |
| Space | Shoot |
| P | Pause / resume |
| Enter | Start / Restart |

## 8. Out of Scope (MVP)

- No backend/API/DB, no auth, no multiplayer, no server-side leaderboard (best score is local only).
- No audio assets (kept silent to stay dependency-free and fast to verify; can be a follow-up).

## 9. Verification Plan (Coder local checks — smallest meaningful)

1. `node scripts/smoke.mjs` — headless engine assertions (lifecycle, movement+bounds, spawn,
   bullet/asteroid collision, life loss + i-frames, game over freeze, level progression, determinism).
2. Browser play-test (served statically): start → move → shoot → destroy asteroid → take a hit →
   game over → restart; HUD + overlays update; best score persists across reload. Screenshot captured.

## 10. Deployment (yoshi, mandatory after QA PASS)

- Azure Static Web App (Free) in `rg-playground-01` via `bicep/main.bicep`.
- GitHub Actions: build = none (static); upload `apps/web` directly with `skip_app_build: true`,
  SWA deploy token from `az` (no repo integration), mirroring the survival game workflow.
