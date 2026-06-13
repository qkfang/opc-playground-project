# Last Stand 🎮

A fast, replayable single-player **browser survival game** with multiple distinct
modes. Survive in a top-down arena as homing enemies stream in from every side and
the difficulty escalates in waves. Built as a client-side HTML5 Canvas game inside
a single Next.js app — no backend, no install.

## Modes

- **Classic** — steady, escalating waves. The reference survival experience.
- **Blitz** — faster enemies, faster spawns, shorter wave timer, fewer lives.
  High intensity, higher score multiplier.
- **Nightfall** — a survival-horror twist: darkness limits your vision to a small
  light radius around you.

## Controls

- **Move:** WASD / arrow keys, or hold the mouse / touch to steer toward the cursor.
- **Pause:** `P`.
- **Goal:** survive as long as possible. Score rises with time, waves, and score
  orbs; touching an enemy costs a life (with brief invulnerability after a hit).

## Run locally

```bash
cd apps/web
npm install
npm run dev          # http://localhost:3000
# or a production build:
npm run build
npm run start
```

Then open the page and pick a mode.

## Headless smoke test

A game has no HTTP API, so the smoke test verifies the **engine** directly by
simulating real play (deterministic RNG):

```bash
cd apps/web
npm run smoke        # tsx scripts/smoke.mjs
```

It checks: mode definitions are distinct, lifecycle (ready → running → gameover),
the survival loop (spawning, wave escalation, scoring), movement + arena bounds,
collisions causing life loss and game over, frozen post-gameover state, measurable
mode differences (idle dies faster in Blitz), that skill matters (evasive outlives
idle), and determinism for a fixed seed.

## Architecture

- `lib/engine.ts` — framework-agnostic `SurvivalGame` (pure logic; `update(dt)`
  never touches the DOM, so it runs headless; `render(ctx)` is the only DOM touch).
- `lib/modes.ts` — data-driven mode table (easy to tune/extend).
- `components/GameClient.tsx` — `"use client"` canvas + game loop + input + UI.
- `app/page.tsx` — server page that frames the game with mode/how-to info.

See `docs/design.md` for the full design.

## Notes

- Single-player, no multiplayer, no accounts, no network. Best score per mode is
  stored in `localStorage`.
- The app builds with the standard Next.js toolchain. The smoke test runs via
  `tsx` (a devDependency) so it can execute the TypeScript engine directly.
