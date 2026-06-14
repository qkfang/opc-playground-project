// Headless smoke test for the "Legions vs the Castle Dragon" engine — Node, zero deps.
// Run: node scripts/smoke.mjs   (exit 0 = pass, 1 = fail)
import {
  createGame, reset, start, togglePause, fire, update, snapshot,
  CONFIG, WORLD, levelForState, isEnraged, aliveCount, enrageFraction,
} from "../apps/web/game/engine.js";

let passed = 0;
let failed = 0;
const fails = [];
function ok(cond, msg) {
  if (cond) passed++;
  else { failed++; fails.push(msg); console.error("  x FAIL:", msg); }
}
function section(name) { console.log(`\n# ${name}`); }

// Step the engine by totalMs in dtMs chunks with constant input.
function run(state, totalMs, dtMs, input = {}) {
  let t = 0;
  while (t < totalMs) {
    const step = Math.min(dtMs, totalMs - t);
    update(state, step, input);
    t += step;
  }
}

// 1. Fresh state
section("1. Fresh game");
let g = createGame({ seed: 42 });
ok(g.status === "ready", "fresh status is 'ready'");
ok(g.dragon.hp === CONFIG.dragon.maxHp, `dragon hp === ${CONFIG.dragon.maxHp}`);
ok(g.strength === CONFIG.cohort.strength, `cohort strength === ${CONFIG.cohort.strength}`);
ok(g.cohort.length === CONFIG.cohort.count, `cohort has ${CONFIG.cohort.count} soldiers`);
ok(g.score === 0 && g.pila.length === 0, "fresh score 0, no pila");
ok(aliveCount(g) === CONFIG.cohort.count, "all soldiers alive at start");

// 2. Start + frozen-unless-playing
section("2. Start / freeze");
start(g);
ok(g.status === "playing", "after start status is 'playing'");
let ready = createGame({ seed: 1 });
const beforeScore = ready.score;
update(ready, 1000, {});
ok(ready.score === beforeScore && ready.status === "ready", "update no-op while 'ready'");

// 3. Aim movement + clamp
section("3. Aim movement + bounds");
let mv = createGame({ seed: 7 }); start(mv);
const x0 = mv.aim.x;
run(mv, 200, 16, { right: true });
ok(mv.aim.x > x0, "moving right increases aim.x");
run(mv, 5000, 16, { right: true });
ok(mv.aim.x <= WORLD.maxX + 1e-6, "aim clamped at right bound");
run(mv, 8000, 16, { left: true });
ok(mv.aim.x >= WORLD.minX - 1e-6, "aim clamped at left bound");

// 4. Volley fire + cooldown
section("4. Volley + cooldown");
let fg = createGame({ seed: 5 }); start(fg);
const fired1 = fire(fg);
ok(fired1 === true && fg.pila.length === CONFIG.pila.perVolley, `fire launches ${CONFIG.pila.perVolley} pila`);
const fired2 = fire(fg);
ok(fired2 === false && fg.pila.length === CONFIG.pila.perVolley, "fire rate-limited within cooldown");
run(fg, CONFIG.pila.cooldownMs + 40, 16, {});
const fired3 = fire(fg);
ok(fired3 === true && fg.pila.length >= CONFIG.pila.perVolley + 1, "fire allowed after cooldown");

// 5. Pila that connects → dragon HP down + score up
section("5. Pila x dragon hit");
let hc = createGame({ seed: 1 }); start(hc);
hc.dragon.x = 0;
hc.dragon.dir = 0; // hold still for the test
const hpBefore = hc.dragon.hp;
const scoreBefore = hc.score;
// place a pila essentially on the dragon, about to land
hc.pila.push({ id: 99001, x: hc.dragon.x, t: 0.999, vt: CONFIG.pila.speed });
update(hc, 16, {});
ok(hc.dragon.hp < hpBefore, "dragon hp decreases on a connecting pila");
ok(hc.score >= scoreBefore + CONFIG.scoring.hitPoints, "score increases by hit points");
ok(hc.hits === 1, "hit counter incremented");
ok(!hc.pila.some((p) => p.id === 99001), "pila consumed after landing");

// 5b. Pila that misses (far in lane) → no damage
let miss = createGame({ seed: 2 }); start(miss);
miss.dragon.x = 0; miss.dragon.dir = 0;
const hpM = miss.dragon.hp;
miss.pila.push({ id: 99002, x: WORLD.maxX, t: 0.999, vt: CONFIG.pila.speed }); // dragon at 0, pila at far edge
update(miss, 16, {});
ok(miss.dragon.hp === hpM, "off-lane pila does no damage");

// 6. Fire breath downs soldiers standing in the lane
section("6. Fire breath hazard");
let fb = createGame({ seed: 3 }); start(fb);
fb.dragon.dir = 0;
fb.dragon.breathCd = 1e9; fb.dragon.diveCd = 1e9; // isolate: no autonomous dragon attacks this tick
const target = fb.cohort[3];
const strBefore = fb.strength;
// narrow zone over exactly one soldier (soldier spacing is 2.5 lane units)
fb.fireZones.push({ id: 91001, x: target.x, halfWidth: 0.6, ttlMs: 300, active: true });
update(fb, 16, {});
ok(target.down === true, "soldier in fire lane is downed");
ok(fb.strength === strBefore - 1, "narrow fire downs exactly one -> strength drops by 1");
ok(fb.soldiersLost === 1, "soldiersLost incremented by 1");

// 6b. a WIDE fire sweep hits multiple soldiers (real breath halfWidth spans >1 soldier)
let fw = createGame({ seed: 33 }); start(fw);
fw.dragon.breathCd = 1e9; fw.dragon.diveCd = 1e9;
const strW = fw.strength;
fw.fireZones.push({ id: 91003, x: fw.cohort[3].x, halfWidth: CONFIG.dragon.breathWidth, ttlMs: 300, active: true });
update(fw, 16, {});
ok(strW - fw.strength >= 2, "wide fire sweep downs multiple soldiers");

// 6c. already-down soldier is not counted again
const strAfter = fb.strength;
fb.fireZones.push({ id: 91002, x: target.x, halfWidth: 0.6, ttlMs: 300, active: true });
update(fb, 16, {});
ok(fb.strength === strAfter, "already-down soldier not counted again");

// 7. Downed soldier revives after reviveMs (long fights stay winnable)
section("7. Soldier revive");
let rv = createGame({ seed: 8 }); start(rv);
rv.dragon.dir = 0;
rv.dragon.breathCd = 1e9; rv.dragon.diveCd = 1e9; // isolate from autonomous attacks
const sol = rv.cohort[0];
rv.fireZones.push({ id: 92001, x: sol.x, halfWidth: 0.5, ttlMs: 50, active: true });
update(rv, 16, {});
ok(sol.down === true, "soldier downed");
const strDown = rv.strength;
run(rv, CONFIG.cohort.reviveMs + 200, 32, {});
ok(sol.down === false, "soldier revived after reviveMs");
ok(rv.strength === strDown + 1, "strength recovers on revive");

// 8. Win when dragon hp hits 0
section("8. Win condition");
let win = createGame({ seed: 4 }); start(win);
win.dragon.x = 0; win.dragon.dir = 0;
win.dragon.hp = CONFIG.pila.damage; // one good hit will finish it
win.pila.push({ id: 93001, x: 0, t: 0.999, vt: CONFIG.pila.speed });
update(win, 16, {});
ok(win.dragon.hp === 0, "dragon hp reaches 0");
ok(win.status === "won", "status becomes 'won'");
ok(win.score >= CONFIG.scoring.killBonus, "kill bonus awarded");
// frozen after win
const wScore = win.score;
run(win, 1000, 16, { left: true, right: true, fire: true });
ok(win.status === "won", "stays won (frozen)");

// 9. Lose when cohort strength hits 0
section("9. Lose condition");
let lose = createGame({ seed: 6 }); start(lose);
lose.dragon.dir = 0;
// down every soldier via a giant fire zone covering the whole lane
lose.fireZones.push({ id: 94001, x: 0, halfWidth: (WORLD.maxX - WORLD.minX), ttlMs: 200, active: true });
update(lose, 16, {});
ok(lose.strength === 0, "all soldiers downed -> strength 0");
ok(lose.status === "lost", "status becomes 'lost'");
const lScore = lose.score;
run(lose, 1000, 16, { fire: true });
ok(lose.status === "lost", "stays lost (frozen)");

// 10. Enrage + difficulty level by HP fraction
section("10. Enrage / level");
let en = createGame({ seed: 11 });
ok(levelForState(en) === 1 && !isEnraged(en), "full hp -> level 1, not enraged");
en.dragon.hp = CONFIG.dragon.maxHp * 0.5;
ok(isEnraged(en) && levelForState(en) === 2, "<=50% hp -> enraged, level 2");
en.dragon.hp = CONFIG.dragon.maxHp * 0.2;
ok(levelForState(en) === 3, "<=25% hp -> level 3 (final stand)");

// 11. Pause halts simulation
section("11. Pause");
let pz = createGame({ seed: 12 }); start(pz);
togglePause(pz);
ok(pz.paused === true, "togglePause pauses");
const snapBefore = { t: pz.timeMs, s: pz.score, ax: pz.aim.x };
run(pz, 1000, 16, { right: true, fire: true });
ok(pz.timeMs === snapBefore.t && pz.score === snapBefore.s && pz.aim.x === snapBefore.ax,
   "no state change while paused");
togglePause(pz);
ok(pz.paused === false, "togglePause resumes");

// 12. Dragon eventually attacks on its own (AI cadence produces a fire zone or a loss)
section("12. Dragon AI fires over time");
let ai = createGame({ seed: 123 }); start(ai);
let sawHazard = false;
let t = 0;
while (t < 12000 && ai.status === "playing") {
  update(ai, 16, {}); // no player input; just stand there
  if (ai.fireZones.length > 0 || ai.soldiersLost > 0) { sawHazard = true; break; }
  t += 16;
}
ok(sawHazard, "dragon produces a hazard / downs a soldier within 12s of idle play");

// 13. Determinism: same seed + same scripted input => identical outcome
section("13. Determinism");
function play(seed) {
  const s = createGame({ seed }); start(s);
  for (let i = 0; i < 400; i++) {
    const input = { left: i % 5 === 0, right: i % 3 === 0, fire: i % 6 === 0 };
    update(s, 16, input);
  }
  return s;
}
const A = play(2026);
const B = play(2026);
ok(A.score === B.score, `determinism: score matches (${A.score} == ${B.score})`);
ok(A.dragon.hp === B.dragon.hp, "determinism: dragon hp matches");
ok(A.strength === B.strength, "determinism: strength matches");
ok(A.soldiersLost === B.soldiersLost, "determinism: soldiers lost matches");
const C = play(99);
ok(A.score !== C.score || A.dragon.hp !== C.dragon.hp || A.soldiersLost !== C.soldiersLost,
   "different seed yields a different run");

// 14. Reset -> clean ready, preserves best
section("14. Reset");
let rs = play(5); reset(rs, { seed: 5, bestScore: 1234 });
ok(rs.status === "ready" && rs.strength === CONFIG.cohort.strength && rs.score === 0,
   "reset -> clean ready state");
ok(rs.dragon.hp === CONFIG.dragon.maxHp, "reset restores dragon hp");
ok(rs.bestScore === 1234, "reset preserves provided bestScore");

// 15. snapshot() shape for the HUD
section("15. Snapshot");
let sn = createGame({ seed: 1 }); start(sn);
const snap = snapshot(sn);
for (const key of ["status","score","best","dragonHp","dragonHpFrac","strength","alive","level","enraged","hits"]) {
  ok(key in snap, `snapshot has '${key}'`);
}

// ---- Report ----
console.log(`\n----------------------------------------`);
console.log(`Legions vs Dragon engine smoke: ${passed} passed, ${failed} failed`);
if (failed > 0) {
  console.log("FAILURES:");
  for (const f of fails) console.log("  - " + f);
  console.log("SMOKE FAILED");
  process.exit(1);
} else {
  console.log("SMOKE PASSED");
  process.exit(0);
}
