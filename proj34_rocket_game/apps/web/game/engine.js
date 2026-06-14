// Star Ascent — pure game engine (NO DOM, NO browser APIs).
// Importable both in the browser (ES module) and in Node (smoke test).
// Deterministic given a seed + identical input/step sequence.

// ----- World constants -----
export const WORLD = { w: 480, h: 720 };

export const CONFIG = {
  rocket: { w: 34, h: 46, speed: 0.34 },     // speed in px per ms
  bullet: { w: 5, h: 14, speed: 0.62, cooldownMs: 220 },
  asteroid: {
    minR: 14, maxR: 30,
    baseSpeed: 0.085, speedPerLevel: 0.018,
    baseSpawnMs: 900, spawnDecPerLevel: 70, minSpawnMs: 260,
  },
  scoring: { survivalPerSec: 1, asteroidPoints: 10, levelStep: 100 },
  lives: 3,
  invulnMs: 1400,
  particleMs: 520,
};

// ----- Deterministic RNG (mulberry32) -----
export function makeRng(seed) {
  let a = seed >>> 0;
  return function rng() {
    a |= 0; a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

// ----- AABB overlap (rocket/bullet are rects; asteroid uses bounding box) -----
function rectsOverlap(ax, ay, aw, ah, bx, by, bw, bh) {
  return ax < bx + bw && ax + aw > bx && ay < by + bh && ay + ah > by;
}

export function levelForScore(score) {
  return Math.floor(score / CONFIG.scoring.levelStep) + 1;
}

// ----- Game factory -----
export function createGame(opts = {}) {
  const seed = opts.seed ?? 12345;
  const state = {
    seed,
    rng: makeRng(seed),
    status: "ready", // ready | playing | paused | gameover
    score: 0,
    displayScore: 0, // integer score shown in HUD
    level: 1,
    lives: CONFIG.lives,
    timeMs: 0,
    rocket: {
      x: WORLD.w / 2 - CONFIG.rocket.w / 2,
      y: WORLD.h - CONFIG.rocket.h - 58,
      w: CONFIG.rocket.w,
      h: CONFIG.rocket.h,
    },
    asteroids: [],
    bullets: [],
    particles: [],
    spawnTimerMs: 0,
    fireCooldownMs: 0,
    invulnMs: 0,
    nextId: 1,
    asteroidsDestroyed: 0,
    bestScore: opts.bestScore ?? 0,
  };
  return state;
}

export function reset(state, opts = {}) {
  const seed = opts.seed ?? state.seed ?? 12345;
  const best = opts.bestScore ?? state.bestScore ?? 0;
  const fresh = createGame({ seed, bestScore: best });
  Object.assign(state, fresh);
  return state;
}

export function start(state) {
  if (state.status === "ready" || state.status === "gameover") {
    reset(state, { seed: state.seed, bestScore: state.bestScore });
    state.status = "playing";
  }
  return state;
}

export function togglePause(state) {
  if (state.status === "playing") state.status = "paused";
  else if (state.status === "paused") state.status = "playing";
  return state;
}

// Rate-limited fire.
export function fire(state) {
  if (state.status !== "playing") return false;
  if (state.fireCooldownMs > 0) return false;
  const r = state.rocket;
  state.bullets.push({
    id: state.nextId++,
    x: r.x + r.w / 2 - CONFIG.bullet.w / 2,
    y: r.y - CONFIG.bullet.h,
    w: CONFIG.bullet.w,
    h: CONFIG.bullet.h,
  });
  state.fireCooldownMs = CONFIG.bullet.cooldownMs;
  return true;
}

function spawnAsteroid(state) {
  const a = CONFIG.asteroid;
  const r = a.minR + state.rng() * (a.maxR - a.minR);
  const x = r + state.rng() * (WORLD.w - 2 * r);
  const speed = a.baseSpeed + (state.level - 1) * a.speedPerLevel;
  const drift = (state.rng() - 0.5) * 0.04; // slight horizontal drift
  state.asteroids.push({
    id: state.nextId++,
    x, y: -r * 2,
    r,
    vx: drift,
    vy: speed,
    spin: (state.rng() - 0.5) * 0.01,
    rot: state.rng() * Math.PI * 2,
  });
}

function addExplosion(state, x, y, count = 8) {
  for (let i = 0; i < count; i++) {
    const ang = state.rng() * Math.PI * 2;
    const spd = 0.04 + state.rng() * 0.12;
    state.particles.push({
      id: state.nextId++,
      x, y,
      vx: Math.cos(ang) * spd,
      vy: Math.sin(ang) * spd,
      life: CONFIG.particleMs,
      maxLife: CONFIG.particleMs,
    });
  }
}

function spawnIntervalMs(state) {
  const a = CONFIG.asteroid;
  return Math.max(a.minSpawnMs, a.baseSpawnMs - (state.level - 1) * a.spawnDecPerLevel);
}

// input: { left, right, up, down } booleans
export function update(state, dtMs, input = {}) {
  if (state.status !== "playing") return state;
  // Clamp dt to avoid tunneling on tab-switch spikes.
  const dt = Math.min(dtMs, 50);
  state.timeMs += dt;

  // Timers
  if (state.fireCooldownMs > 0) state.fireCooldownMs = Math.max(0, state.fireCooldownMs - dt);
  if (state.invulnMs > 0) state.invulnMs = Math.max(0, state.invulnMs - dt);

  // --- Rocket movement ---
  const sp = CONFIG.rocket.speed * dt;
  const r = state.rocket;
  if (input.left) r.x -= sp;
  if (input.right) r.x += sp;
  if (input.up) r.y -= sp;
  if (input.down) r.y += sp;
  // Clamp to world (never exits bounds)
  r.x = Math.max(0, Math.min(WORLD.w - r.w, r.x));
  // Rocket constrained to lower ~55% of the screen for fair play.
  const minY = WORLD.h * 0.45;
  r.y = Math.max(minY, Math.min(WORLD.h - r.h - 24, r.y));

  // --- Score (survival) + level ---
  state.score += (CONFIG.scoring.survivalPerSec * dt) / 1000;
  state.displayScore = Math.floor(state.score);
  state.level = levelForScore(state.displayScore);

  // --- Spawn asteroids ---
  state.spawnTimerMs += dt;
  const interval = spawnIntervalMs(state);
  while (state.spawnTimerMs >= interval) {
    state.spawnTimerMs -= interval;
    spawnAsteroid(state);
  }

  // --- Move bullets (up); cull off-screen ---
  for (const b of state.bullets) b.y -= CONFIG.bullet.speed * dt;
  state.bullets = state.bullets.filter((b) => b.y + b.h > 0);

  // --- Move asteroids; cull below screen ---
  for (const ast of state.asteroids) {
    ast.x += ast.vx * dt;
    ast.y += ast.vy * dt;
    ast.rot += ast.spin * dt;
    // bounce horizontally off walls
    if (ast.x - ast.r < 0) { ast.x = ast.r; ast.vx = Math.abs(ast.vx); }
    if (ast.x + ast.r > WORLD.w) { ast.x = WORLD.w - ast.r; ast.vx = -Math.abs(ast.vx); }
  }
  state.asteroids = state.asteroids.filter((a) => a.y - a.r <= WORLD.h);

  // --- Bullet x asteroid collisions ---
  const deadAsteroids = new Set();
  const deadBullets = new Set();
  for (const b of state.bullets) {
    for (const ast of state.asteroids) {
      if (deadAsteroids.has(ast.id)) continue;
      // asteroid bounding box
      if (rectsOverlap(b.x, b.y, b.w, b.h, ast.x - ast.r, ast.y - ast.r, ast.r * 2, ast.r * 2)) {
        deadAsteroids.add(ast.id);
        deadBullets.add(b.id);
        state.score += CONFIG.scoring.asteroidPoints;
        state.asteroidsDestroyed += 1;
        addExplosion(state, ast.x, ast.y, 10);
        break;
      }
    }
  }
  if (deadAsteroids.size) state.asteroids = state.asteroids.filter((a) => !deadAsteroids.has(a.id));
  if (deadBullets.size) state.bullets = state.bullets.filter((b) => !deadBullets.has(b.id));
  state.displayScore = Math.floor(state.score);
  state.level = levelForScore(state.displayScore);

  // --- Rocket x asteroid collision (only when not invulnerable) ---
  if (state.invulnMs <= 0) {
    for (const ast of state.asteroids) {
      if (rectsOverlap(r.x, r.y, r.w, r.h, ast.x - ast.r, ast.y - ast.r, ast.r * 2, ast.r * 2)) {
        // lose a life, remove that asteroid, grant i-frames (prevents double hit same frame/window)
        state.lives -= 1;
        addExplosion(state, ast.x, ast.y, 14);
        state.asteroids = state.asteroids.filter((a) => a.id !== ast.id);
        state.invulnMs = CONFIG.invulnMs;
        if (state.lives <= 0) {
          state.lives = 0;
          state.status = "gameover";
          if (state.displayScore > state.bestScore) state.bestScore = state.displayScore;
        }
        break; // at most one hit per frame
      }
    }
  }

  // --- Particles ---
  for (const p of state.particles) {
    p.x += p.vx * dt;
    p.y += p.vy * dt;
    p.life -= dt;
  }
  state.particles = state.particles.filter((p) => p.life > 0);

  return state;
}

// Convenience: returns true if rocket is currently flashing (invulnerable).
export function isInvulnerable(state) {
  return state.invulnMs > 0;
}
