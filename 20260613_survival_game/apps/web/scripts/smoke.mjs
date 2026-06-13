// Headless smoke test for the survival game.
// A game has no HTTP API to curl, so we verify the ENGINE directly by simulating
// real play deterministically. Run with Node 24 type-stripping:
//   npx tsx scripts/smoke.mjs
// (npm run smoke wires this up.)

import { SurvivalGame } from "../lib/engine";
import { MODES, MODE_LIST, getMode } from "../lib/modes";

let pass = 0;
let fail = 0;
const failures = [];

function check(name, cond) {
  if (cond) {
    pass++;
    console.log(`  ok  - ${name}`);
  } else {
    fail++;
    failures.push(name);
    console.log(`  FAIL- ${name}`);
  }
}

// Deterministic RNG so runs are reproducible.
function makeRng(seed) {
  let s = seed >>> 0;
  return () => {
    s = (s * 1664525 + 1013904223) >>> 0;
    return s / 0x100000000;
  };
}

const W = 880;
const H = 560;

// Simulate a game with no player input (player stays put) for a number of seconds.
function simulateIdle(mode, seconds, seed = 12345, dt = 1 / 60) {
  const g = new SurvivalGame(W, H, mode, makeRng(seed));
  g.start();
  let t = 0;
  let maxEnemies = 0;
  while (t < seconds && g.status === "running") {
    g.update(dt);
    if (g.enemies.length > maxEnemies) maxEnemies = g.enemies.length;
    t += dt;
  }
  return { game: g, snap: g.snapshot(), maxEnemies };
}

// Simulate a game where the player actively flees toward the safest corner.
function simulateEvasive(mode, seconds, seed = 999, dt = 1 / 60) {
  const g = new SurvivalGame(W, H, mode, makeRng(seed));
  g.start();
  let t = 0;
  while (t < seconds && g.status === "running") {
    // Steer away from the nearest enemy.
    let nearest = null;
    let nd = Infinity;
    for (const e of g.enemies) {
      const d = Math.hypot(e.x - g.player.x, e.y - g.player.y);
      if (d < nd) {
        nd = d;
        nearest = e;
      }
    }
    if (nearest) {
      const away = { x: g.player.x * 2 - nearest.x, y: g.player.y * 2 - nearest.y };
      g.setInput({ pointer: away });
    }
    g.update(dt);
    t += dt;
  }
  return { game: g, snap: g.snapshot() };
}

console.log("Last Stand — survival engine smoke test\n");

// --- Mode definitions ---
console.log("Modes:");
check("three modes defined", MODE_LIST.length === 3);
check("classic mode exists", !!MODES.classic);
check("blitz mode exists", !!MODES.blitz);
check("nightfall mode exists", !!MODES.nightfall);
check("getMode returns null for unknown", getMode("nope") === null);
check("only nightfall limits vision", MODES.nightfall.visionRadius !== null && MODES.classic.visionRadius === null && MODES.blitz.visionRadius === null);
check("blitz spawns faster than classic", MODES.blitz.baseSpawnIntervalSec < MODES.classic.baseSpawnIntervalSec);
check("blitz enemies faster than classic", MODES.blitz.baseEnemySpeed > MODES.classic.baseEnemySpeed);
check("riskier modes reward more", MODES.blitz.scoreMultiplier > MODES.classic.scoreMultiplier && MODES.nightfall.scoreMultiplier > MODES.classic.scoreMultiplier);

// --- Engine lifecycle ---
console.log("\nLifecycle:");
{
  const g = new SurvivalGame(W, H, MODES.classic, makeRng(1));
  check("starts in 'ready' status", g.status === "ready");
  g.start();
  check("running after start()", g.status === "running");
  check("starts with mode lives", g.snapshot().lives === MODES.classic.startingLives);
  check("starts at wave 0", g.snapshot().wave === 0);
  check("player centered at start", g.player.x === W / 2 && g.player.y === H / 2);
  check("no enemies at the very start", g.enemies.length === 0);
}

// --- Core survival loop: enemies spawn, waves escalate, time accrues, score rises ---
// Use an evasive player so the run lasts long enough to observe the loop
// (an idle player gets swarmed and dies almost immediately, which is by design).
console.log("\nSurvival loop (evasive 12s, classic):");
{
  const { snap } = simulateEvasive(MODES.classic, 12, 2024);
  check("time survived advanced", snap.timeSurvived > 8);
  check("enemies spawned over time", snap.enemyCount > 0 || snap.status === "gameover");
  check("score increased", snap.score > 0);
  check("wave is tracked", snap.wave >= 0);
}

// --- Waves clearly escalate over a longer run ---
// Wave escalation is purely time-driven, so isolate it from combat balance by
// giving the player plenty of lives (clone the mode) and steering evasively.
console.log("\nWave escalation (time-driven, classic with survivability):");
{
  const tough = { ...MODES.classic, startingLives: 50 };
  const { snap } = simulateEvasive(tough, 40, 7);
  // Classic wave interval is 15s; ~40s of play should advance the wave counter.
  check("wave counter advances over ~40s", snap.wave >= 2);
  check("survived a long run with extra lives", snap.timeSurvived > 30);
}

// --- Wave escalation is deterministic from elapsed time (unit-level) ---
console.log("\nWave escalation (unit, no enemies in the way):");
{
  // Drive update with a player that has huge lives and sits in a corner; even if
  // hit, 999 lives guarantees we observe several wave ticks.
  const m = { ...MODES.classic, startingLives: 999 };
  const g = new SurvivalGame(W, H, m, makeRng(123));
  g.start();
  const interval = m.waveIntervalSec;
  const steps = Math.ceil((interval * 3.5) / (1 / 60));
  for (let i = 0; i < steps; i++) g.update(1 / 60);
  check("after 3.5 wave-intervals, wave >= 3", g.snapshot().wave >= 3);
}

// --- Player movement actually moves the player ---
console.log("\nMovement:");
{
  const g = new SurvivalGame(W, H, MODES.classic, makeRng(3));
  g.start();
  const x0 = g.player.x;
  g.setInput({ right: true });
  for (let i = 0; i < 30; i++) g.update(1 / 60);
  check("moving right increases x", g.player.x > x0);
  g.setInput({ right: false, left: true });
  const x1 = g.player.x;
  for (let i = 0; i < 30; i++) g.update(1 / 60);
  check("moving left decreases x", g.player.x < x1);
  // Bounds clamp.
  g.setInput({ left: true });
  for (let i = 0; i < 600; i++) g.update(1 / 60);
  check("player clamped to arena (x >= radius)", g.player.x >= g.player.radius - 0.001);
}

// --- Standing still in a swarm leads to game over (collisions + life loss work) ---
console.log("\nDeath by collision (idle, blitz):");
{
  const { snap } = simulateIdle(MODES.blitz, 60, 42);
  check("idle player eventually loses all lives", snap.lives <= 0);
  check("status is gameover after death", snap.status === "gameover");
  check("recorded a positive final score", snap.score > 0);
}

// --- Update is a no-op once game over (no negative lives / runaway state) ---
console.log("\nPost-gameover safety:");
{
  const g = new SurvivalGame(W, H, MODES.blitz, makeRng(5));
  g.start();
  let guard = 0;
  while (g.status === "running" && guard < 100000) {
    g.update(1 / 60);
    guard++;
  }
  const livesAtDeath = g.snapshot().lives;
  const timeAtDeath = g.snapshot().timeSurvived;
  for (let i = 0; i < 100; i++) g.update(1 / 60);
  check("lives don't change after gameover", g.snapshot().lives === livesAtDeath);
  check("time doesn't advance after gameover", g.snapshot().timeSurvived === timeAtDeath);
}

// --- Modes behave differently: blitz kills an idle player faster than classic ---
console.log("\nMode difference (idle survival time):");
{
  const classic = simulateIdle(MODES.classic, 120, 314);
  const blitz = simulateIdle(MODES.blitz, 120, 314);
  // Both should die when idle within 120s; blitz should die no later than classic.
  check("idle dies in classic within 120s", classic.snap.status === "gameover");
  check("idle dies in blitz within 120s", blitz.snap.status === "gameover");
  check(
    "blitz idle survival <= classic idle survival",
    blitz.snap.timeSurvived <= classic.snap.timeSurvived + 0.001,
  );
}

// --- An evasive player survives meaningfully longer than an idle one ---
console.log("\nSkill matters (evasive vs idle, classic 15s):");
{
  const idle = simulateIdle(MODES.classic, 15, 555);
  const evasive = simulateEvasive(MODES.classic, 15, 555);
  check(
    "evasive player survives >= idle player",
    evasive.snap.timeSurvived >= idle.snap.timeSurvived - 0.001,
  );
}

// --- Determinism: same seed => same outcome ---
console.log("\nDeterminism:");
{
  const a = simulateIdle(MODES.classic, 20, 1234);
  const b = simulateIdle(MODES.classic, 20, 1234);
  check("same seed yields same score", Math.floor(a.snap.score) === Math.floor(b.snap.score));
  check("same seed yields same survival time", Math.abs(a.snap.timeSurvived - b.snap.timeSurvived) < 1e-9);
}

console.log(`\n${pass} passed, ${fail} failed`);
if (fail > 0) {
  console.log("Failures:\n - " + failures.join("\n - "));
  console.log("SMOKE FAILED");
  process.exit(1);
}
console.log("SMOKE PASSED: all checks green");
