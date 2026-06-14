# Legions vs the Castle Dragon ⚔️🐉 (proj38)

A fun, visually readable **voxel browser game**: command a line of **Roman legionaries** defending
a **castle** courtyard against a fire-breathing **dragon** perched on the keep. Time your **pila
(javelin) volleys**, dodge the dragon's fire-breath sweeps and dive-bombs, and break the beast
before it breaks your cohort.

Built as a **100% client-side static site** — plain HTML/CSS/JS ES modules + **Three.js** (pinned
CDN via import map). **No build step.** Deploys to a single **Azure Static Web App**.

![Legions vs the Castle Dragon](../../media/outbound/proj38-voxel-roman-dragon-game.png)

## Play

- **← / →** or **A / D** — move your cohort's aim along the line
- **Space** / **click** — throw a **pila volley** (rate-limited)
- **P** — pause · **R** — restart · **Enter/Space** on the title — start
- Mouse: move to aim, click to throw · Touch: on-screen buttons (mobile)

**Win:** bring the dragon's HP to 0 (the keep is saved).
**Lose:** your cohort's strength reaches 0 (the line breaks).
The dragon **enrages** below 50% HP (faster, more frequent attacks) and makes a **final stand**
below 25%. Downed legionaries rally back after a short delay, so long fights stay winnable.
Best score is saved in `localStorage`.

## Architecture

The game logic is fully **decoupled from rendering** so it can be tested headless:

- `apps/web/game/engine.js` — deterministic game state + all rules. **No DOM / no Three.js.**
  Seeded PRNG → reproducible. This is what the smoke test exercises.
- `apps/web/game/render.js` — Three.js **voxel** scene built from box meshes; reads engine state.
- `apps/web/game/input.js` — keyboard / pointer / touch → input struct.
- `apps/web/game/main.js` — wires it together, runs the rAF loop, HUD, overlays, persistence.
- `apps/web/index.html`, `styles.css` — canvas host, HUD, overlays, Roman red/gold theme.

```
proj38_voxel_roman_dragon_game/
├─ apps/web/            # the game (static; deploy this folder)
│  ├─ index.html  styles.css  staticwebapp.config.json
│  └─ game/ engine.js  render.js  input.js  main.js
├─ bicep/main.bicep     # Azure Static Web App (Free)
├─ scripts/ serve.mjs   # zero-dep local server
│           smoke.mjs   # headless engine smoke test (Node, zero deps)
└─ docs/ solution.md  task.md  todo.md
```

## Develop & verify

```bash
# headless engine smoke test (no browser needed) — exit 0 = pass
node scripts/smoke.mjs        # or: npm run smoke

# play locally
node scripts/serve.mjs        # or: npm run serve  → http://127.0.0.1:4173/
```

No dependencies to install — `serve.mjs`/`smoke.mjs` are zero-dep Node; Three.js loads from CDN.

## Deploy (Azure Static Web Apps, token-based)

1. **Infra:** run the `proj38_voxel_roman_dragon_game_infra` GitHub Action (baseName `proj38`)
   → creates `proj38-swa-<suffix>` in `rg-playground-01`.
2. **Deploy:** run `proj38_voxel_roman_dragon_game_deploy` → uploads `apps/web` with
   `skip_app_build: true` (pure static) and smoke-checks the live URL.

## Credits

Procedural voxel art (everything is boxes), original gameplay. Theme: SPQR vs. a very large lizard.
