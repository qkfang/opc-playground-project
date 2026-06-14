// proj38 — Legions vs the Castle Dragon
// Deterministic game engine. Pure JS: NO DOM, NO three.js. Holds ALL game state + rules
// so it can be smoke-tested headless in Node (scripts/smoke.mjs) and rendered by render.js.
//
// Coordinate model (engine units, renderer maps to its own scale):
//   x: lane position across the courtyard line. Range [WORLD.minX, WORLD.maxX].
//   The cohort (Roman line) sits at the near edge; the dragon flies over the keep (far edge).
//   "depth" (how far a pila has travelled toward the dragon) is tracked per-pila as t in [0,1].

export const WORLD = Object.freeze({
  minX: -10,
  maxX: 10,
  laneY: 0, // legionaries stand here
});

export const CONFIG = Object.freeze({
  cohort: {
    count: 8, // legionaries in the line
    strength: 8, // == count; each downed soldier costs 1; lose at 0
    reviveMs: 6000, // a dive-bombed soldier is "down" this long then returns
    iframeMs: 1200, // a soldier can't be hit again briefly after being hit
  },
  aim: {
    speed: 12, // lane units / second
    startX: 0,
  },
  pila: {
    cooldownMs: 360, // volley rate limit
    speed: 1.9, // travel rate in t-units/sec (t goes 0 -> 1 toward dragon)
    perVolley: 3, // javelins thrown per volley (spread around aim)
    spread: 2.2, // lane spread of the volley around aim x
    hitRadius: 2.4, // lane distance within which a pila at the dragon's depth connects
    damage: 6, // HP per connecting pila
  },
  dragon: {
    maxHp: 300,
    x: 0,
    hoverY: 1, // not used for collisions; renderer uses for height
    moveSpeed: 3.0, // lane units/sec base drift
    moveSpeedEnraged: 5.4,
    // attack cadence (ms) — lower = more frequent. Scales down as HP drops.
    breathEveryMs: 3200,
    breathEveryMsEnraged: 1900,
    diveEveryMs: 5200,
    diveEveryMsEnraged: 3200,
    breathTelegraphMs: 900, // warning window before fire lands
    breathActiveMs: 700, // fire is dangerous for this long
    breathWidth: 3.0, // lane half-width of the fire sweep
    diveTelegraphMs: 800,
    enrageAtHpFrac: 0.5, // below this HP fraction -> enraged
  },
  scoring: {
    hitPoints: 10, // per connecting pila
    killBonus: 500, // depleting the dragon
    survivePerSec: 2, // ticking survival score
  },
});

// ---- Seeded PRNG (mulberry32) → deterministic smoke tests ----
function mulberry32(seed) {
  let a = seed >>> 0;
  return function () {
    a |= 0;
    a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

let _idSeq = 1;
function nextId() {
  return _idSeq++;
}

function clamp(v, lo, hi) {
  return v < lo ? lo : v > hi ? hi : v;
}

export function enrageFraction(state) {
  return state.dragon.hp / CONFIG.dragon.maxHp;
}

export function isEnraged(state) {
  return enrageFraction(state) <= CONFIG.dragon.enrageAtHpFrac && state.dragon.hp > 0;
}

// Difficulty "level" surfaced to the HUD: 1 normal, 2 enraged, 3 final-stand (<25% hp).
export function levelForState(state) {
  const f = enrageFraction(state);
  if (f <= 0.25) return 3;
  if (f <= CONFIG.dragon.enrageAtHpFrac) return 2;
  return 1;
}

export function aliveCount(state) {
  return state.cohort.filter((s) => !s.down).length;
}

// ---- Construction ----
export function createGame({ seed = 1, bestScore = 0 } = {}) {
  const rng = mulberry32(seed);
  const cohort = [];
  const n = CONFIG.cohort.count;
  for (let i = 0; i < n; i++) {
    // spread soldiers evenly across the lane
    const x = WORLD.minX + ((i + 0.5) / n) * (WORLD.maxX - WORLD.minX);
    cohort.push({ id: nextId(), x, down: false, downMs: 0, iframeMs: 0, hitFlash: 0 });
  }
  // Seed the dragon's starting position, heading, and first attack timers from the RNG
  // so different seeds diverge from t=0 (and the boss feels less robotic).
  const startX = WORLD.minX + rng() * (WORLD.maxX - WORLD.minX);
  const startDir = rng() < 0.5 ? -1 : 1;
  const firstBreath = CONFIG.dragon.breathEveryMs * (0.55 + rng() * 0.6);
  const firstDive = CONFIG.dragon.diveEveryMs * (0.55 + rng() * 0.6);
  return {
    status: "ready", // ready | playing | won | lost
    paused: false,
    seed,
    _rng: rng,
    timeMs: 0,
    aim: { x: CONFIG.aim.startX },
    cohort,
    strength: CONFIG.cohort.strength,
    dragon: {
      x: startX,
      dir: startDir,
      hp: CONFIG.dragon.maxHp,
      state: "hover", // hover | breath | dive
      // timers counting DOWN to the next action (seeded so seeds diverge early)
      breathCd: firstBreath,
      diveCd: firstDive,
      // active attack windows
      breathTelegraphMs: 0,
      breathActiveMs: 0,
      breathX: 0, // lane center of the pending/active fire
      diveTelegraphMs: 0,
      diveTargetId: null,
      diveX: 0,
      hitFlash: 0,
    },
    pila: [], // {id, x, t, vt}
    fireZones: [], // {id, x, halfWidth, ttlMs, active} (visual + damage window)
    particles: [], // {id, kind, x, y, ttlMs, life}
    pilaCd: 0,
    score: 0,
    displayScore: 0,
    bestScore,
    hits: 0, // pila that connected
    volleys: 0,
    soldiersLost: 0,
    _surviveAcc: 0,
  };
}

export function reset(state, { seed = state.seed, bestScore = state.bestScore } = {}) {
  const fresh = createGame({ seed, bestScore });
  Object.assign(state, fresh);
  return state;
}

export function start(state) {
  if (state.status === "ready") {
    state.status = "playing";
    state.paused = false;
  }
  return state;
}

export function togglePause(state) {
  if (state.status === "playing") state.paused = !state.paused;
  return state.paused;
}

// Throw a volley of pila from the cohort line toward the dragon, centered on the aim.
// Returns true if a volley was actually launched (not on cooldown / not playing).
export function fire(state) {
  if (state.status !== "playing" || state.paused) return false;
  if (state.pilaCd > 0) return false;
  if (aliveCount(state) <= 0) return false;
  state.pilaCd = CONFIG.pila.cooldownMs;
  state.volleys++;
  const k = CONFIG.pila.perVolley;
  for (let i = 0; i < k; i++) {
    const off = k === 1 ? 0 : (i / (k - 1) - 0.5) * CONFIG.pila.spread;
    const x = clamp(state.aim.x + off, WORLD.minX, WORLD.maxX);
    state.pila.push({ id: nextId(), x, t: 0, vt: CONFIG.pila.speed });
  }
  return true;
}

function spawnParticles(state, kind, x, y, count) {
  for (let i = 0; i < count; i++) {
    state.particles.push({
      id: nextId(),
      kind,
      x: x + (state._rng() - 0.5) * 1.2,
      y: y + (state._rng() - 0.5) * 1.2,
      ttlMs: 380 + state._rng() * 320,
      life: 1,
    });
  }
}

function startBreath(state) {
  const d = state.dragon;
  d.state = "breath";
  d.breathTelegraphMs = CONFIG.dragon.breathTelegraphMs;
  d.breathActiveMs = CONFIG.dragon.breathActiveMs;
  // Fire aims at where the cohort aim currently is (punishes camping), with slight jitter.
  d.breathX = clamp(state.aim.x + (state._rng() - 0.5) * 3, WORLD.minX, WORLD.maxX);
}

function startDive(state) {
  const d = state.dragon;
  const alive = state.cohort.filter((s) => !s.down);
  if (alive.length === 0) return;
  d.state = "dive";
  d.diveTelegraphMs = CONFIG.dragon.diveTelegraphMs;
  const target = alive[Math.floor(state._rng() * alive.length)];
  d.diveTargetId = target.id;
  d.diveX = target.x;
}

function downSoldier(state, soldier) {
  if (soldier.down || soldier.iframeMs > 0) return false;
  soldier.down = true;
  soldier.downMs = CONFIG.cohort.reviveMs;
  soldier.hitFlash = 1;
  state.strength = Math.max(0, state.strength - 1);
  state.soldiersLost++;
  spawnParticles(state, "dust", soldier.x, 0, 8);
  if (state.strength <= 0) {
    state.status = "lost";
  }
  return true;
}

function applyDamage(state, amount) {
  const d = state.dragon;
  if (d.hp <= 0) return;
  d.hp = Math.max(0, d.hp - amount);
  d.hitFlash = 1;
  if (d.hp <= 0) {
    state.score += CONFIG.scoring.killBonus;
    state.status = "won";
  }
}

// Main fixed-ish timestep update. dtMs is elapsed milliseconds; input is a struct:
//   { left, right, fire }  (fire is edge-triggered by caller OR held; we rate-limit internally)
export function update(state, dtMs, input = {}) {
  if (state.status !== "playing" || state.paused) {
    // keep displayScore easing even when paused/ended so UI is smooth
    state.displayScore += (state.score - state.displayScore) * Math.min(1, dtMs / 200);
    return state;
  }
  const dt = dtMs / 1000;
  state.timeMs += dtMs;

  // cooldowns
  state.pilaCd = Math.max(0, state.pilaCd - dtMs);

  // aim movement
  if (input.left) state.aim.x -= CONFIG.aim.speed * dt;
  if (input.right) state.aim.x += CONFIG.aim.speed * dt;
  state.aim.x = clamp(state.aim.x, WORLD.minX, WORLD.maxX);
  if (input.fire) fire(state);

  // survival score
  state._surviveAcc += dt * CONFIG.scoring.survivePerSec;
  if (state._surviveAcc >= 1) {
    const whole = Math.floor(state._surviveAcc);
    state.score += whole;
    state._surviveAcc -= whole;
  }

  updateDragon(state, dtMs, dt);
  updatePila(state, dt);
  updateCohort(state, dtMs);
  updateHazards(state, dtMs);
  updateParticles(state, dtMs);

  // ease display values
  state.displayScore += (state.score - state.displayScore) * Math.min(1, dtMs / 200);
  if (state.score > state.bestScore) state.bestScore = state.score;

  return state;
}

function updateDragon(state, dtMs, dt) {
  const d = state.dragon;
  if (d.hp <= 0) return;
  const enraged = isEnraged(state);
  const speed = enraged ? CONFIG.dragon.moveSpeedEnraged : CONFIG.dragon.moveSpeed;

  // drift across the keep, bounce at bounds
  d.x += d.dir * speed * dt;
  if (d.x > WORLD.maxX) { d.x = WORLD.maxX; d.dir = -1; }
  if (d.x < WORLD.minX) { d.x = WORLD.minX; d.dir = 1; }

  d.hitFlash = Math.max(0, d.hitFlash - dtMs / 200);

  // tick attack cooldowns
  d.breathCd -= dtMs;
  d.diveCd -= dtMs;

  // resolve active attack windows
  if (d.state === "breath") {
    if (d.breathTelegraphMs > 0) {
      d.breathTelegraphMs -= dtMs;
      if (d.breathTelegraphMs <= 0) {
        // fire lands: create a damaging fire zone
        state.fireZones.push({
          id: nextId(),
          x: d.breathX,
          halfWidth: CONFIG.dragon.breathWidth,
          ttlMs: d.breathActiveMs,
          active: true,
        });
        spawnParticles(state, "fire", d.breathX, 0, 14);
      }
    } else {
      d.breathActiveMs -= dtMs;
      if (d.breathActiveMs <= 0) {
        d.state = "hover";
        d.breathCd = enraged ? CONFIG.dragon.breathEveryMsEnraged : CONFIG.dragon.breathEveryMs;
      }
    }
  } else if (d.state === "dive") {
    d.diveTelegraphMs -= dtMs;
    if (d.diveTelegraphMs <= 0) {
      // strike the targeted soldier (if still valid)
      const tgt = state.cohort.find((s) => s.id === d.diveTargetId);
      if (tgt && !tgt.down) downSoldier(state, tgt);
      spawnParticles(state, "dust", d.diveX, 0, 10);
      d.state = "hover";
      d.diveCd = enraged ? CONFIG.dragon.diveEveryMsEnraged : CONFIG.dragon.diveEveryMs;
      d.diveTargetId = null;
    }
  } else {
    // hover: pick the next attack when a cooldown elapses (breath prioritized if both ready)
    if (d.breathCd <= 0) startBreath(state);
    else if (d.diveCd <= 0) startDive(state);
  }
}

function updatePila(state, dt) {
  const keep = [];
  for (const p of state.pila) {
    p.t += p.vt * dt;
    if (p.t >= 1) {
      // reached the dragon's depth: does it connect?
      const dx = Math.abs(p.x - state.dragon.x);
      if (state.dragon.hp > 0 && dx <= CONFIG.pila.hitRadius) {
        // closer hits do a touch more (reward aim)
        const acc = 1 - dx / (CONFIG.pila.hitRadius * 1.6);
        const dmg = CONFIG.pila.damage * (0.7 + 0.6 * Math.max(0, acc));
        applyDamage(state, dmg);
        state.score += CONFIG.scoring.hitPoints;
        state.hits++;
        spawnParticles(state, "spark", state.dragon.x, 0, 6);
      }
      // pila consumed (hit or sails past)
      continue;
    }
    keep.push(p);
  }
  state.pila = keep;
}

function updateCohort(state, dtMs) {
  for (const s of state.cohort) {
    if (s.iframeMs > 0) s.iframeMs = Math.max(0, s.iframeMs - dtMs);
    if (s.hitFlash > 0) s.hitFlash = Math.max(0, s.hitFlash - dtMs / 300);
    if (s.down) {
      s.downMs -= dtMs;
      if (s.downMs <= 0) {
        // soldier returns to the line (strength recovers — keeps long fights winnable)
        s.down = false;
        s.downMs = 0;
        s.iframeMs = CONFIG.cohort.iframeMs;
        state.strength = Math.min(CONFIG.cohort.strength, state.strength + 1);
      }
    }
  }
}

function updateHazards(state, dtMs) {
  const keep = [];
  for (const z of state.fireZones) {
    z.ttlMs -= dtMs;
    if (z.active) {
      // any standing soldier inside the fire lane gets downed (respecting i-frames)
      for (const s of state.cohort) {
        if (!s.down && Math.abs(s.x - z.x) <= z.halfWidth) downSoldier(state, s);
      }
    }
    if (z.ttlMs > 0) keep.push(z);
  }
  state.fireZones = keep;
}

function updateParticles(state, dtMs) {
  const keep = [];
  for (const p of state.particles) {
    p.ttlMs -= dtMs;
    p.life = Math.max(0, p.ttlMs / 600);
    if (p.ttlMs > 0) keep.push(p);
  }
  state.particles = keep;
}

// Convenience for UI/tests
export function snapshot(state) {
  return {
    status: state.status,
    paused: state.paused,
    score: Math.round(state.displayScore),
    rawScore: state.score,
    best: Math.round(state.bestScore),
    dragonHp: state.dragon.hp,
    dragonHpFrac: enrageFraction(state),
    strength: state.strength,
    alive: aliveCount(state),
    level: levelForState(state),
    enraged: isEnraged(state),
    hits: state.hits,
    timeMs: state.timeMs,
  };
}
