# proj38 — Legions vs the Castle Dragon (voxel browser game)

## Summary

A fun, visually readable **voxel** browser game: you command a line of **Roman legionaries**
defending a **castle** courtyard against a fire-breathing **dragon** perched on the keep. Hold
the line, time your **pila (javelin) volleys**, dodge fire breath and dive-bombs, and bring the
dragon's health to zero before it overruns your cohort.

Theme is delivered with a chunky low-poly / voxel aesthetic (everything is built from boxes):
sandstone castle walls + crenellations, a red-crested testudo of legionaries, a green scaled
dragon with wings and glowing eyes, banners, torches, and a parallax sky.

## Tech choice

- **100% client-side static site** — plain HTML + CSS + ES modules. **No build step.**
- **Three.js** (pinned CDN, via import map) for the voxel 3D scene + camera + lighting.
- **Decoupled, deterministic engine** (`game/engine.js`) holds *all* game state + rules and is
  pure JS with **no DOM/Three dependency**, so it can be unit/smoke-tested headless in Node.
- **Renderer** (`game/render.js`) reads engine state each frame and draws voxel meshes; it never
  owns game rules. **Input** (`game/input.js`) maps keyboard/pointer to an input struct.
- Deploys as a single **Azure Static Web App** (Free), token-based GitHub Actions deploy — same
  proven pattern as proj34_rocket_game.

This mirrors the repo's established browser-game architecture (engine/render split + Node smoke),
which keeps QA fast (`node scripts/smoke.mjs`) and deployment trivial (static upload, no build).

## Gameplay

- **Setting:** a castle courtyard. Camera looks across the courtyard toward the keep where the
  dragon sits/flies. Your legionaries stand on the near rampart line.
- **You control** a *cohort cursor* (the highlighted legionary / aim point) that slides left↔right
  along the line. You command the **whole line to throw pila** in a volley toward the dragon.
- **Dragon AI:** circles/hovers over the keep, periodically **breathes fire** (a telegraphed
  ground sweep you must move out of) and **dive-bombs** a legionary (knocks one out for a while).
  As its health drops it gets **enraged**: faster, more frequent attacks (difficulty ramp).
- **Combat:** pila that connect reduce dragon HP. Big hits when the dragon is low/hovering close.
- **Score:** points per hit + a survival/time bonus; **best score** persisted in `localStorage`.
- **Win:** dragon HP → 0 (the keep is saved). **Lose:** your cohort strength → 0 (line breaks).
- **Restart loop:** clear win/lose overlay with **Restart** (R / button) → clean ready state.

## Controls

- **← / →** or **A / D** — move the cohort aim along the line.
- **Space / click** — throw a **pila volley** (rate-limited cooldown).
- **P** — pause/resume. **R** — restart. **Enter/Space** on the title — start.
- Mouse: move pointer to aim, click to throw. Touch: on-screen buttons (mobile-friendly).

## Files

```
proj38_voxel_roman_dragon_game/
  apps/web/
    index.html                 # canvas host, HUD, overlays, import map for three
    styles.css                 # voxel-flavoured UI theme (Roman red/gold)
    staticwebapp.config.json   # SWA routing + mime
    game/
      engine.js                # deterministic game state + rules (NO DOM/three) — testable
      render.js                # Three.js voxel scene built from engine state
      input.js                 # keyboard/pointer/touch -> input struct
      main.js                  # wires engine + render + input; rAF loop; HUD; persistence
  bicep/main.bicep             # Azure Static Web App (Free)
  scripts/
    serve.mjs                  # zero-dep static server for local play
    smoke.mjs                  # headless engine smoke test (Node, zero deps)
  docs/{solution,task,todo}.md
  README.md
  package.json                 # scripts: serve, smoke/test
```

## Verification (smallest meaningful checks)

1. `node scripts/smoke.mjs` — headless engine assertions (state machine, movement/clamp, volley +
   cooldown, hit → HP down + score up, dragon attack → cohort loss, win at HP 0, lose at strength 0,
   difficulty ramp, determinism by seed, reset). Exit 0 = pass.
2. `node scripts/serve.mjs` + load `/` in a headless browser — assert canvas present, no console
   errors, title correct, `game/engine.js` reachable; capture a gameplay screenshot.

## Out of scope (kept tight)

- No backend/API/DB, no accounts, no multiplayer, no audio asset pipeline (optional WebAudio
  blips only), no art asset files (everything is procedural voxel geometry).
