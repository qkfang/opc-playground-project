// Headless smoke test for the Star Ascent engine — Node, zero deps.
// Run: node scripts/smoke.mjs   (exit 0 = pass, 1 = fail)
import {
  createGame, reset, start, togglePause, fire, update,
  CONFIG, WORLD, levelForScore, isInvulnerable,
} from "../apps/web/game/engine.js";

let passed = 0;
let failed = 0;
const fails = [];
function ok(cond, msg) {
  if (cond) { passed++; }
  else { failed++; fails.push(msg); console.error("  ✗ FAIL:", msg); }
}
function section(name) { console.log(`\n# ${name}`); }

// Helper: step the game by stepMs in `chunks` of dt, with constant input.
function run(state, totalMs, dtMs, input = {}) {
  let t = 0;
  while (t < totalMs) {
    const step = Math.min(dtMs, totalMs - t);
    update(state, step, input);
    t += step;
  }
}

// 1. Fresh game state
section("1. Fresh game");
let g = createGame({ seed: 42 });
ok(g.status === "ready", "fresh status is 'ready'");
ok(g.lives === CONFIG.lives, `fresh lives === ${CONFIG.lives}`);
ok(g.score === 0 && g.displayScore === 0, "fresh score is 0");
ok(g.level === 1, "fresh level is 1");
ok(g.asteroids.length === 0 && g.bullets.length === 0, "fresh has no entities");

// 2. Start
section("2. Start");
start(g);
ok(g.status === "playing", "after start status is 'playing'");

// update is a no-op unless playing
section("2b. Update frozen unless playing");
let ready = createGame({ seed: 1 });
const beforeScore = ready.score;
update(ready, 1000, {});
ok(ready.score === beforeScore && ready.status === "ready", "update no-op while 'ready'");

// 3. Move right + right-bound clamp
section("3. Movement + bounds");
let mv = createGame({ seed: 7 }); start(mv);
const x0 = mv.rocket.x;
run(mv, 100, 16, { right: true });
ok(mv.rocket.x > x0, "moving right increases rocket.x");
run(mv, 5000, 16, { right: true }); // push hard into wall
ok(mv.rocket.x <= WORLD.w - mv.rocket.w + 1e-6, "rocket clamped at right bound");
ok(mv.rocket.x + mv.rocket.w <= WORLD.w + 1e-6, "rocket never exits right edge");

// 4. Move left + left-bound clamp
let mvl = createGame({ seed: 8 }); start(mvl);
run(mvl, 5000, 16, { left: true });
ok(mvl.rocket.x >= -1e-6, "rocket clamped at left bound (>=0)");

// vertical clamp
let mvu = createGame({ seed: 9 }); start(mvu);
run(mvu, 5000, 16, { up: true });
ok(mvu.rocket.y >= WORLD.h * 0.45 - 1e-6, "rocket clamped to lower play area (up)");
run(mvu, 5000, 16, { down: true });
ok(mvu.rocket.y + mvu.rocket.h <= WORLD.h + 1e-6, "rocket clamped at bottom (down)");

// 5. Asteroids spawn over time
section("5. Asteroid spawning");
let sp = createGame({ seed: 123 }); start(sp);
run(sp, 4000, 16, {}); // several spawn intervals at level 1
ok(sp.asteroids.length > 0, "asteroids spawn over time");

// 6. Fire creates a bullet; rate-limited
section("6. Fire + cooldown");
let fg = createGame({ seed: 5 }); start(fg);
const fired1 = fire(fg);
ok(fired1 === true && fg.bullets.length === 1, "fire creates a bullet");
const fired2 = fire(fg); // immediate second shot blocked by cooldown
ok(fired2 === false && fg.bullets.length === 1, "fire rate-limited within cooldown");
run(fg, CONFIG.bullet.cooldownMs + 20, 16, {}); // let cooldown elapse over real frames
const fired3 = fire(fg);
ok(fired3 === true && fg.bullets.length >= 2, "fire allowed after cooldown elapses");

// 7. Bullet destroys asteroid → both removed, score up, particle created
section("7. Bullet x asteroid collision");
let bc = createGame({ seed: 1 }); start(bc);
// Place a controlled asteroid directly above a bullet and step a tiny dt.
bc.asteroids.push({ id: 9001, x: bc.rocket.x + bc.rocket.w / 2, y: bc.rocket.y - 60, r: 20, vx: 0, vy: 0, spin: 0, rot: 0 });
bc.bullets.push({ id: 9002, x: bc.rocket.x + bc.rocket.w / 2 - 2, y: bc.rocket.y - 60, w: CONFIG.bullet.w, h: CONFIG.bullet.h });
const scoreBefore = bc.score;
const partBefore = bc.particles.length;
update(bc, 16, {});
ok(!bc.asteroids.some((a) => a.id === 9001), "asteroid removed after bullet hit");
ok(!bc.bullets.some((b) => b.id === 9002), "bullet removed after hit");
ok(bc.score >= scoreBefore + CONFIG.scoring.asteroidPoints, "score increased by asteroid points");
ok(bc.particles.length > partBefore, "explosion particles created");
ok(bc.asteroidsDestroyed === 1, "asteroidsDestroyed incremented");

// 8. Rocket x asteroid → lose life + i-frames, no second hit during i-frames
section("8. Rocket x asteroid + invulnerability");
let rc = createGame({ seed: 2 }); start(rc);
const livesBefore = rc.lives;
// Drop an asteroid right on the rocket.
rc.asteroids.push({ id: 8001, x: rc.rocket.x + rc.rocket.w / 2, y: rc.rocket.y + rc.rocket.h / 2, r: 20, vx: 0, vy: 0, spin: 0, rot: 0 });
update(rc, 16, {});
ok(rc.lives === livesBefore - 1, "rocket hit costs exactly one life");
ok(isInvulnerable(rc), "i-frames active after a hit");
// Second overlapping asteroid during i-frames must NOT cost a life.
rc.asteroids.push({ id: 8002, x: rc.rocket.x + rc.rocket.w / 2, y: rc.rocket.y + rc.rocket.h / 2, r: 20, vx: 0, vy: 0, spin: 0, rot: 0 });
const livesMid = rc.lives;
update(rc, 16, {});
ok(rc.lives === livesMid, "no extra life lost while invulnerable");

// 9. Lives -> 0 => gameover, then frozen
section("9. Game over + freeze");
let go = createGame({ seed: 3 }); start(go);
go.lives = 1;
go.invulnMs = 0;
go.asteroids = [{ id: 7001, x: go.rocket.x + go.rocket.w / 2, y: go.rocket.y + go.rocket.h / 2, r: 22, vx: 0, vy: 0, spin: 0, rot: 0 }];
update(go, 16, {});
ok(go.status === "gameover" && go.lives === 0, "lives 0 -> status gameover");
const frozenScore = go.score;
const frozenLives = go.lives;
run(go, 2000, 16, { left: true, right: true });
ok(go.score === frozenScore && go.lives === frozenLives, "no score/life change after gameover (frozen)");
ok(go.status === "gameover", "stays gameover");

// 10. Level progression
section("10. Level progression");
ok(levelForScore(0) === 1, "level 1 at score 0");
ok(levelForScore(CONFIG.scoring.levelStep) === 2, "level 2 at levelStep");
ok(levelForScore(CONFIG.scoring.levelStep * 3) === 4, "level 4 at 3x levelStep");
let lv = createGame({ seed: 11 }); start(lv);
lv.score = CONFIG.scoring.levelStep + 5; // simulate accumulated score
update(lv, 16, {});
ok(lv.level >= 2, "engine recomputes level from score during update");

// 11. Determinism: same seed + same input/steps => identical outcome
section("11. Determinism");
function play(seed) {
  const s = createGame({ seed }); start(s);
  // deterministic scripted input pattern over a few seconds
  for (let i = 0; i < 300; i++) {
    const input = { left: i % 4 === 0, right: i % 3 === 0, up: i % 7 === 0 };
    if (i % 9 === 0) fire(s);
    update(s, 16, input);
  }
  return s;
}
const A = play(2026);
const B = play(2026);
ok(A.displayScore === B.displayScore, `determinism: scores match (${A.displayScore} == ${B.displayScore})`);
ok(A.lives === B.lives, "determinism: lives match");
ok(A.asteroids.length === B.asteroids.length, "determinism: asteroid count matches");
ok(A.bullets.length === B.bullets.length, "determinism: bullet count matches");
ok(A.asteroidsDestroyed === B.asteroidsDestroyed, "determinism: destroyed count matches");
// different seed should (very likely) differ in entity layout
const C = play(99);
ok(
  A.asteroids.length !== C.asteroids.length ||
  A.asteroids.some((a, i) => !C.asteroids[i] || Math.abs(a.x - C.asteroids[i].x) > 0.001),
  "different seed yields different asteroid field"
);

// reset returns to a clean ready state
section("12. Reset");
let rs = play(5); reset(rs, { seed: 5, bestScore: 50 });
ok(rs.status === "ready" && rs.lives === CONFIG.lives && rs.score === 0, "reset -> clean ready state");
ok(rs.bestScore === 50, "reset preserves provided bestScore");

// ---- Report ----
console.log(`\n----------------------------------------`);
console.log(`Star Ascent engine smoke: ${passed} passed, ${failed} failed`);
if (failed > 0) {
  console.log("FAILURES:");
  for (const f of fails) console.log("  - " + f);
  console.log("SMOKE FAILED");
  process.exit(1);
} else {
  console.log("SMOKE PASSED");
  process.exit(0);
}
