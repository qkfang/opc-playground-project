# Star Ascent — Browser Rocket Game (proj34)

A polished, self-contained **browser rocket arcade game**. Pilot a rocket up through an
asteroid field: dodge and shoot the rocks, survive, climb the levels, and beat your high score.

- **project_id:** proj34
- **project_code:** `20260614_rocket_game`
- **Stack:** pure static site — HTML5 Canvas + vanilla ES-module JavaScript. **No framework, no dependencies, no build step.**

## Play locally

```bash
# from this folder (20260614_rocket_game)
node scripts/serve.mjs 4173
# open http://127.0.0.1:4173/
```

Or open `apps/web/index.html` via any static server.

## Controls

| Key | Action |
| --- | --- |
| ← → ↑ ↓ or W A S D | Move the rocket |
| Space | Shoot |
| P | Pause / resume |
| Enter | Start / Restart |

Touch controls appear automatically on coarse-pointer devices.

## Features (MVP)

- Rocket movement with keyboard (and touch), clamped to the play area.
- Asteroids spawn from the top and descend; spawn rate & speed scale with level.
- Shooting destroys asteroids (score + particle burst).
- Scoring: survival over time + points per asteroid; **level** ramps difficulty every 100 pts.
- **3 lives**; collision costs a life and grants brief invulnerability (i-frames).
- **Game Over** overlay with final score + best; **Restart** flow.
- Best score persisted in `localStorage` (`star-ascent-best`).
- Starfield parallax, neon arcade styling, responsive canvas.

## Tests

```bash
node scripts/smoke.mjs    # headless engine smoke test (40 assertions)
```

The core rules live in `apps/web/game/engine.js` as a **pure, DOM-free module** so they can be
verified headlessly (lifecycle, movement+bounds, spawning, collisions, lives + i-frames,
game-over freeze, level progression, determinism). `apps/web/game/main.js` is the browser glue
(canvas render, input, RAF loop, localStorage).

## Structure

```
20260614_rocket_game/
  apps/web/
    index.html
    styles.css
    game/engine.js   # pure engine (testable)
    game/main.js      # browser glue (canvas/input/loop)
    staticwebapp.config.json
  scripts/
    smoke.mjs         # headless engine smoke test
    serve.mjs         # zero-dep local static server
  bicep/main.bicep    # Azure Static Web App (Free)
  docs/               # solution.md, todo.md, task.md
```

## Deployment

Client-only static site → **Azure Static Web Apps (Free)** in `rg-playground-01`.
Provision with `bicep/main.bicep`, then deploy via GitHub Actions:

- `.github/workflows/20260614_rocket_game_infra.yml` — provisions the SWA.
- `.github/workflows/20260614_rocket_game_deploy.yml` — uploads `apps/web` directly
  (`skip_app_build: true`, SWA deploy token from `az` — no repo integration).

No build is required; the deploy uploads the static `apps/web` folder as-is.
