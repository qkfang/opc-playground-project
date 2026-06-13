// Framework-agnostic survival game engine.
// All game logic lives here as pure TypeScript so it can run headless (for tests)
// and be driven by any renderer. The only DOM touch is `render(ctx)`, which is
// optional and never called during headless simulation.

import { GameMode } from "./modes";

export interface Vec {
  x: number;
  y: number;
}

export interface Player extends Vec {
  radius: number;
  lives: number;
  invulnUntil: number; // seconds; brief i-frames after a hit
}

export interface Enemy extends Vec {
  radius: number;
  speed: number;
  hue: number;
  id: number;
}

export interface Orb extends Vec {
  radius: number;
  id: number;
}

export type GameStatus = "ready" | "running" | "gameover";

export interface InputState {
  up: boolean;
  down: boolean;
  left: boolean;
  right: boolean;
  /** Optional pointer target; when set the player steers toward it. */
  pointer: Vec | null;
}

export interface GameSnapshot {
  status: GameStatus;
  timeSurvived: number;
  wave: number;
  score: number;
  lives: number;
  kills: number;
  enemyCount: number;
}

const TWO_PI = Math.PI * 2;

export class SurvivalGame {
  readonly width: number;
  readonly height: number;
  readonly mode: GameMode;

  status: GameStatus = "ready";
  player: Player;
  enemies: Enemy[] = [];
  orbs: Orb[] = [];

  timeSurvived = 0;
  wave = 0;
  kills = 0;
  score = 0;

  private spawnTimer = 0;
  private orbTimer = 0;
  private waveTimer = 0;
  private nextId = 1;
  private rng: () => number;

  input: InputState = {
    up: false,
    down: false,
    left: false,
    right: false,
    pointer: null,
  };

  constructor(width: number, height: number, mode: GameMode, rng: () => number = Math.random) {
    this.width = width;
    this.height = height;
    this.mode = mode;
    this.rng = rng;
    this.player = {
      x: width / 2,
      y: height / 2,
      radius: 12,
      lives: mode.startingLives,
      invulnUntil: 0,
    };
  }

  /** Reset to a fresh runnable state and begin playing. */
  start(): void {
    this.status = "running";
    this.enemies = [];
    this.orbs = [];
    this.timeSurvived = 0;
    this.wave = 0;
    this.kills = 0;
    this.score = 0;
    this.spawnTimer = 0;
    this.orbTimer = 1.5;
    this.waveTimer = 0;
    this.nextId = 1;
    this.player.x = this.width / 2;
    this.player.y = this.height / 2;
    this.player.lives = this.mode.startingLives;
    this.player.invulnUntil = 0;
    this.input = { up: false, down: false, left: false, right: false, pointer: null };
  }

  setInput(partial: Partial<InputState>): void {
    this.input = { ...this.input, ...partial };
  }

  snapshot(): GameSnapshot {
    return {
      status: this.status,
      timeSurvived: this.timeSurvived,
      wave: this.wave,
      score: Math.floor(this.score),
      lives: this.player.lives,
      kills: this.kills,
      enemyCount: this.enemies.length,
    };
  }

  private spawnIntervalForWave(): number {
    const m = this.mode;
    const reduced = m.baseSpawnIntervalSec - this.wave * 0.06;
    return Math.max(m.minSpawnIntervalSec, reduced);
  }

  private enemySpeedForWave(): number {
    const m = this.mode;
    return m.baseEnemySpeed + this.wave * m.enemySpeedPerWave;
  }

  private spawnEnemy(): void {
    if (this.enemies.length >= this.mode.maxEnemies) return;
    const edge = Math.floor(this.rng() * 4);
    let x = 0;
    let y = 0;
    if (edge === 0) {
      x = this.rng() * this.width;
      y = -20;
    } else if (edge === 1) {
      x = this.width + 20;
      y = this.rng() * this.height;
    } else if (edge === 2) {
      x = this.rng() * this.width;
      y = this.height + 20;
    } else {
      x = -20;
      y = this.rng() * this.height;
    }
    const jitter = 0.85 + this.rng() * 0.3;
    this.enemies.push({
      x,
      y,
      radius: 10,
      speed: this.enemySpeedForWave() * jitter,
      hue: 200 + Math.floor(this.rng() * 140),
      id: this.nextId++,
    });
  }

  private spawnOrb(): void {
    const margin = 40;
    this.orbs.push({
      x: margin + this.rng() * (this.width - margin * 2),
      y: margin + this.rng() * (this.height - margin * 2),
      radius: 7,
      id: this.nextId++,
    });
  }

  /** Advance the simulation by dt seconds. Pure logic; safe to call headless. */
  update(dt: number): void {
    if (this.status !== "running") return;
    // Clamp dt so a long frame / tab-stall can't tunnel collisions.
    dt = Math.min(dt, 0.05);

    this.timeSurvived += dt;

    // Wave escalation.
    this.waveTimer += dt;
    if (this.waveTimer >= this.mode.waveIntervalSec) {
      this.waveTimer -= this.mode.waveIntervalSec;
      this.wave += 1;
    }

    // Survival score: time * multiplier, plus wave bonus baked in.
    this.score += dt * 10 * this.mode.scoreMultiplier * (1 + this.wave * 0.1);

    // Movement input -> velocity.
    let vx = 0;
    let vy = 0;
    if (this.input.left) vx -= 1;
    if (this.input.right) vx += 1;
    if (this.input.up) vy -= 1;
    if (this.input.down) vy += 1;
    if (this.input.pointer && vx === 0 && vy === 0) {
      const dx = this.input.pointer.x - this.player.x;
      const dy = this.input.pointer.y - this.player.y;
      const d = Math.hypot(dx, dy);
      if (d > 4) {
        vx = dx / d;
        vy = dy / d;
      }
    }
    const len = Math.hypot(vx, vy);
    if (len > 0) {
      vx /= len;
      vy /= len;
      this.player.x += vx * this.mode.playerSpeed * dt;
      this.player.y += vy * this.mode.playerSpeed * dt;
    }
    // Keep player inside the arena.
    this.player.x = clamp(this.player.x, this.player.radius, this.width - this.player.radius);
    this.player.y = clamp(this.player.y, this.player.radius, this.height - this.player.radius);

    // Spawn enemies.
    this.spawnTimer += dt;
    const interval = this.spawnIntervalForWave();
    while (this.spawnTimer >= interval) {
      this.spawnTimer -= interval;
      this.spawnEnemy();
    }

    // Occasionally spawn a score orb.
    this.orbTimer -= dt;
    if (this.orbTimer <= 0 && this.orbs.length < 4) {
      this.orbTimer = 3 + this.rng() * 3;
      this.spawnOrb();
    }

    // Move enemies toward the player.
    for (const e of this.enemies) {
      const dx = this.player.x - e.x;
      const dy = this.player.y - e.y;
      const d = Math.hypot(dx, dy) || 1;
      e.x += (dx / d) * e.speed * dt;
      e.y += (dy / d) * e.speed * dt;
    }

    // Enemy-enemy soft separation so they don't perfectly stack.
    this.separateEnemies();

    // Collisions: enemy hits player.
    if (this.timeSurvived > this.player.invulnUntil) {
      for (const e of this.enemies) {
        if (circlesOverlap(e, this.player)) {
          this.player.lives -= 1;
          this.player.invulnUntil = this.timeSurvived + 1.0; // 1s i-frames
          // Knock nearby enemies back a bit so you aren't instantly re-hit.
          this.pushEnemiesAway();
          if (this.player.lives <= 0) {
            this.status = "gameover";
          }
          break;
        }
      }
    }

    // Orb pickups.
    if (this.orbs.length) {
      this.orbs = this.orbs.filter((o) => {
        if (circlesOverlap(o, this.player)) {
          this.score += 25 * this.mode.scoreMultiplier;
          return false;
        }
        return true;
      });
    }
  }

  private separateEnemies(): void {
    const n = this.enemies.length;
    if (n < 2) return;
    for (let i = 0; i < n; i++) {
      const a = this.enemies[i];
      for (let j = i + 1; j < n; j++) {
        const b = this.enemies[j];
        const dx = b.x - a.x;
        const dy = b.y - a.y;
        const min = a.radius + b.radius;
        const d2 = dx * dx + dy * dy;
        if (d2 > 0 && d2 < min * min) {
          const d = Math.sqrt(d2);
          const push = (min - d) / 2;
          const nx = dx / d;
          const ny = dy / d;
          a.x -= nx * push;
          a.y -= ny * push;
          b.x += nx * push;
          b.y += ny * push;
        }
      }
    }
  }

  private pushEnemiesAway(): void {
    for (const e of this.enemies) {
      const dx = e.x - this.player.x;
      const dy = e.y - this.player.y;
      const d = Math.hypot(dx, dy) || 1;
      if (d < 90) {
        e.x += (dx / d) * (90 - d);
        e.y += (dy / d) * (90 - d);
      }
    }
  }

  /** Optional canvas renderer. Never called during headless simulation. */
  render(ctx: CanvasRenderingContext2D): void {
    const { width, height } = this;
    ctx.clearRect(0, 0, width, height);

    // Arena background.
    ctx.fillStyle = "#0a0e17";
    ctx.fillRect(0, 0, width, height);

    // Subtle grid.
    ctx.strokeStyle = "rgba(255,255,255,0.04)";
    ctx.lineWidth = 1;
    const grid = 40;
    for (let x = 0; x <= width; x += grid) {
      ctx.beginPath();
      ctx.moveTo(x, 0);
      ctx.lineTo(x, height);
      ctx.stroke();
    }
    for (let y = 0; y <= height; y += grid) {
      ctx.beginPath();
      ctx.moveTo(0, y);
      ctx.lineTo(width, y);
      ctx.stroke();
    }

    // Orbs.
    for (const o of this.orbs) {
      ctx.beginPath();
      ctx.fillStyle = "#fbbf24";
      ctx.shadowColor = "#fbbf24";
      ctx.shadowBlur = 14;
      ctx.arc(o.x, o.y, o.radius, 0, TWO_PI);
      ctx.fill();
    }
    ctx.shadowBlur = 0;

    // Enemies.
    for (const e of this.enemies) {
      ctx.beginPath();
      ctx.fillStyle = `hsl(${e.hue}, 80%, 60%)`;
      ctx.arc(e.x, e.y, e.radius, 0, TWO_PI);
      ctx.fill();
    }

    // Player (blinks while invulnerable).
    const blink = this.timeSurvived < this.player.invulnUntil && Math.floor(this.timeSurvived * 12) % 2 === 0;
    if (!blink) {
      ctx.beginPath();
      ctx.fillStyle = this.mode.accent;
      ctx.shadowColor = this.mode.accent;
      ctx.shadowBlur = 18;
      ctx.arc(this.player.x, this.player.y, this.player.radius, 0, TWO_PI);
      ctx.fill();
      ctx.shadowBlur = 0;
    }

    // Nightfall: darkness mask with a vision radius around the player.
    if (this.mode.visionRadius) {
      const r = this.mode.visionRadius;
      const grd = ctx.createRadialGradient(
        this.player.x,
        this.player.y,
        r * 0.35,
        this.player.x,
        this.player.y,
        r,
      );
      grd.addColorStop(0, "rgba(0,0,0,0)");
      grd.addColorStop(1, "rgba(0,0,0,0.96)");
      ctx.fillStyle = grd;
      ctx.fillRect(0, 0, width, height);
      // Hard black beyond the radius.
      ctx.fillStyle = "rgba(0,0,0,0.96)";
      ctx.beginPath();
      ctx.rect(0, 0, width, height);
      ctx.arc(this.player.x, this.player.y, r, 0, TWO_PI, true);
      ctx.fill("evenodd");
    }
  }
}

function clamp(v: number, lo: number, hi: number): number {
  return v < lo ? lo : v > hi ? hi : v;
}

function circlesOverlap(a: Vec & { radius: number }, b: Vec & { radius: number }): boolean {
  const dx = a.x - b.x;
  const dy = a.y - b.y;
  const r = a.radius + b.radius;
  return dx * dx + dy * dy <= r * r;
}
