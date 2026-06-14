# Last Stand — Design

## Overview

Last Stand is an MVP single-player **browser survival game** with multiple distinct
modes. You control a marker in a top-down arena; enemies stream in from the edges
and home toward you. You survive by moving, dodging, and grabbing score orbs while
the difficulty escalates in waves. The run ends when you lose all your lives.

It is a client-side game built on a single Next.js (App Router) app. The gameplay
runs entirely in the browser on an HTML5 Canvas — no backend, no network calls,
no install.

## Goals (MVP)

- A replayable survival loop: spawn → escalate → die → restart.
- Clear, responsive controls (keyboard + mouse/touch).
- **Multiple modes with genuinely different rules/difficulty** (the headline
  requirement), not just cosmetic skins.
- On-screen HUD (time, score, wave, lives), pause, game-over with stats, restart.
- Per-mode best score persisted locally.

## Non-goals (MVP)

- No multiplayer (explicitly out of scope; not essential).
- No accounts/auth, no server, no database, no leaderboard service.
- No audio assets / sprite art required — rendering is procedural (shapes + glow).

## Gameplay loop

1. Player picks a mode from the menu.
2. The arena starts empty; the player spawns in the centre.
3. Enemies spawn from the arena edges on a timer and move toward the player.
4. Every `waveIntervalSec`, the **wave** advances: enemies get faster and spawn
   more frequently (down to a floor).
5. Score accrues continuously from time survived (scaled by wave and the mode's
   score multiplier); score **orbs** appear periodically for bonus points.
6. Touching an enemy costs a life and grants ~1s of invulnerability (with a small
   knockback so you aren't instantly re-hit). At 0 lives → **game over**.
7. Game-over screen shows score / time / wave / best, with Play again and Change
   mode.

## Controls

- **Move:** `WASD` or arrow keys (8-directional, normalised so diagonals aren't
  faster).
- **Mouse/touch:** hold to steer the player toward the cursor (keyboard takes
  priority when both are active).
- **Pause:** `P` (or the on-screen button).

## Modes (the core "different modes" feature)

All three share the same core loop but differ in concrete, tuned parameters:

| Mode | Twist | Lives | Enemy speed | Spawn rate | Vision | Score × |
| --- | --- | --- | --- | --- | --- | --- |
| **Classic** | Steady escalating waves | 3 | baseline | baseline | full | ×1.0 |
| **Blitz** | Faster, meaner, relentless | 2 | higher + ramps faster | much faster, shorter wave timer | full | ×1.6 |
| **Nightfall** | Survive what you can't see | 3 | slightly higher | baseline-ish | **limited light radius (darkness mask)** | ×1.4 |

- **Classic** is the reference experience.
- **Blitz** compresses everything: shorter `waveIntervalSec`, smaller spawn
  interval and floor, faster base enemy speed and per-wave ramp, fewer lives,
  higher cap on concurrent enemies — and a higher reward multiplier.
- **Nightfall** keeps pacing close to Classic but renders a radial darkness mask so
  the player can only see within a `visionRadius` light circle — a survival-horror
  visibility constraint that changes how you play, not just how it looks.

Mode parameters live in `lib/modes.ts` as a single typed table, so modes are
data-driven and easy to tune or extend.

## State & scoring model

- **Player:** position, radius, lives, invulnerability timer.
- **Enemies:** position, radius, speed, colour, id; homing toward the player with
  soft separation so they don't perfectly stack.
- **Orbs:** position, radius, id; picked up on contact for bonus score.
- **Run state:** status (`ready` / `running` / `gameover`), timeSurvived, wave,
  kills, score, plus spawn/orb/wave timers.
- **Score:** `+= dt * 10 * scoreMultiplier * (1 + wave*0.1)` each tick, plus orb
  bonuses (`25 * scoreMultiplier`).
- **Best score:** stored per mode in `localStorage` (`survival-best-scores`).

## Technical architecture

```
apps/web (Next.js 16, App Router, TypeScript, Tailwind v4)
├── app/
│   ├── layout.tsx          # html shell + metadata
│   ├── page.tsx            # server component: title, mode overview, how-to, embeds game
│   └── globals.css
├── components/
│   └── GameClient.tsx      # "use client": canvas, rAF loop, input, menu/HUD/game-over UI
├── lib/
│   ├── engine.ts           # framework-agnostic game engine (pure TS)
│   ├── modes.ts            # mode definitions (data-driven)
│   └── format.ts           # time/score formatting
└── scripts/
    └── smoke.mjs           # headless engine simulation test
```

- **Engine is decoupled from React/DOM.** `SurvivalGame.update(dt)` is pure logic
  (no DOM access) so it can be simulated headless in tests; `render(ctx)` is the
  only DOM touch and is never called during headless runs. The engine takes an
  injectable RNG for deterministic testing.
- **Rendering** is procedural Canvas 2D (shapes + glow + grid + Nightfall darkness
  mask) — no image/audio assets needed.
- **Fixed-timestep safety:** `dt` is clamped so a stalled tab can't tunnel
  collisions.
- The game page is otherwise a static page; only `GameClient` is client-side.

## Verification plan

- `npm run build` succeeds (static page + client bundle).
- `npm run smoke` (headless engine simulation) passes: mode definitions are
  distinct, lifecycle works, the survival loop spawns/escalates/scores, movement
  + bounds work, collisions cause life loss and game over, post-gameover state is
  frozen, modes differ measurably (idle dies faster in Blitz), skill matters
  (evasive outlives idle), and the engine is deterministic for a fixed seed.
- Browser check: the page loads, the menu lets you pick a mode, gameplay runs and
  renders, the HUD updates, Nightfall shows the darkness mask, and game-over →
  restart works.

## Deployment assumptions (not requested yet)

- Standard Node Next.js host: `npm ci` → `npm run build` → `npm run start`,
  `PORT`-aware, no env/secrets/services. Or fully static export, since the app is
  client-only. See README for run instructions.
