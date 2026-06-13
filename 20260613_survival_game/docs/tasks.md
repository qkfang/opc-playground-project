# Last Stand — Tasks

Task breakdown for `build-web-survival-game-20260613-1644` (proj17).

## Scaffolding

- [x] Create project folder `20260613_survival_game` with `docs/` and `apps/web/`.
- [x] `apps/web` config: `package.json` (Next 16, smoke script with type-stripping),
      `next.config.ts`, `tsconfig.json` (`@/*` alias), `postcss.config.mjs`,
      `app/globals.css` (dark theme), `.gitignore`.

## Game engine (pure TS, headless-testable)

- [x] `lib/modes.ts` — data-driven mode table (Classic / Blitz / Nightfall) with
      distinct spawn rates, enemy speeds, lives, vision, and score multipliers.
- [x] `lib/engine.ts` — `SurvivalGame`: state, injectable RNG, `start()`,
      `setInput()`, `update(dt)` (movement, spawning, wave escalation, homing
      enemies, separation, collisions + i-frames, orb pickups, scoring),
      `snapshot()`, and an optional `render(ctx)` (grid, entities, glow, Nightfall
      darkness mask).
- [x] `lib/format.ts` — time + score formatting.

## Frontend / UI

- [x] `components/GameClient.tsx` — canvas, requestAnimationFrame loop, keyboard +
      pointer input, pause, HUD (time/score/wave/lives), mode-select menu,
      game-over screen with stats + best score, restart / change mode.
- [x] `app/layout.tsx` — shell + metadata.
- [x] `app/page.tsx` — title, mode overview cards, how-to-play, embeds the game.

## Verification

- [x] `scripts/smoke.mjs` — headless engine simulation test.
- [x] `npm install` succeeds.
- [x] `npm run build` succeeds.
- [x] `npm run smoke` passes (all checks green).
- [x] Browser check: load, pick mode, play, HUD updates, Nightfall mask, game-over
      → restart.

## Docs

- [x] `docs/design.md` (gameplay loop, controls, mode differences, state model,
      architecture, MVP assumptions).
- [x] `docs/tasks.md` (this file).
- [x] `README.md`.
- [x] Update `shared-context/projects/proj17.md` with notes + evidence.

## Handoff

- [x] Post DONE in Build topic.
- [x] Strict QA handoff to toadette.

## Deployment (post-QA, if approved)

- [ ] Not requested in proj17. Standard Node host or static export if approved
      after QA.
