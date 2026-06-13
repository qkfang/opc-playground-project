// Star Ascent — browser glue. Wires the pure engine to canvas, input, DOM.
import {
  createGame, start, togglePause, fire, update, reset,
  WORLD, isInvulnerable,
} from "./engine.js";

const BEST_KEY = "star-ascent-best";

// ----- DOM -----
const canvas = document.getElementById("game");
const ctx = canvas.getContext("2d");
const el = {
  score: document.getElementById("hud-score"),
  level: document.getElementById("hud-level"),
  lives: document.getElementById("hud-lives"),
  best: document.getElementById("hud-best"),
  ovStart: document.getElementById("overlay-start"),
  ovPause: document.getElementById("overlay-pause"),
  ovOver: document.getElementById("overlay-over"),
  finalScore: document.getElementById("final-score"),
  finalBest: document.getElementById("final-best"),
  finalNew: document.getElementById("final-new"),
  btnStart: document.getElementById("btn-start"),
  btnResume: document.getElementById("btn-resume"),
  btnRestart: document.getElementById("btn-restart"),
  btnPause: document.getElementById("btn-pause"),
  touch: document.getElementById("touch"),
};

function loadBest() {
  try { return parseInt(localStorage.getItem(BEST_KEY) || "0", 10) || 0; }
  catch { return 0; }
}
function saveBest(v) {
  try { localStorage.setItem(BEST_KEY, String(v)); } catch { /* ignore */ }
}

const game = createGame({ seed: (Date.now() & 0x7fffffff) || 1, bestScore: loadBest() });

// ----- Input -----
const input = { left: false, right: false, up: false, down: false };
const keyMap = {
  ArrowLeft: "left", KeyA: "left",
  ArrowRight: "right", KeyD: "right",
  ArrowUp: "up", KeyW: "up",
  ArrowDown: "down", KeyS: "down",
};

window.addEventListener("keydown", (e) => {
  if (keyMap[e.code]) { input[keyMap[e.code]] = true; e.preventDefault(); }
  else if (e.code === "Space") { e.preventDefault(); handleFire(); }
  else if (e.code === "KeyP") { e.preventDefault(); doPause(); }
  else if (e.code === "Enter") { e.preventDefault(); handleEnter(); }
}, { passive: false });

window.addEventListener("keyup", (e) => {
  if (keyMap[e.code]) { input[keyMap[e.code]] = false; e.preventDefault(); }
}, { passive: false });

function handleFire() {
  if (game.status === "ready") return launch();
  if (game.status === "gameover") return launch();
  if (game.status === "playing") fire(game);
}
function handleEnter() {
  if (game.status === "playing" || game.status === "paused") return;
  launch();
}

// ----- Buttons -----
el.btnStart.addEventListener("click", launch);
el.btnRestart.addEventListener("click", launch);
el.btnResume.addEventListener("click", doPause);
el.btnPause.addEventListener("click", () => {
  if (game.status === "playing" || game.status === "paused") doPause();
});

// ----- Touch -----
function bindTouch(dir) {
  return (e) => {
    e.preventDefault();
    if (dir === "fire") { handleFire(); return; }
    input[dir] = true;
  };
}
document.querySelectorAll(".tbtn").forEach((b) => {
  const dir = b.getAttribute("data-dir");
  b.addEventListener("touchstart", bindTouch(dir), { passive: false });
  b.addEventListener("mousedown", bindTouch(dir));
  const clear = (e) => { e.preventDefault(); if (dir !== "fire") input[dir] = false; };
  b.addEventListener("touchend", clear, { passive: false });
  b.addEventListener("mouseup", clear);
  b.addEventListener("mouseleave", () => { if (dir !== "fire") input[dir] = false; });
});

function launch() {
  start(game);
  syncOverlays();
}
function doPause() {
  if (game.status === "playing" || game.status === "paused") {
    togglePause(game);
    syncOverlays();
  }
}

function syncOverlays() {
  el.ovStart.classList.toggle("hidden", game.status !== "ready");
  el.ovPause.classList.toggle("hidden", game.status !== "paused");
  const over = game.status === "gameover";
  el.ovOver.classList.toggle("hidden", !over);
  if (over) {
    el.finalScore.textContent = game.displayScore;
    el.finalBest.textContent = game.bestScore;
    el.finalNew.classList.toggle("hidden", !(game.displayScore >= game.bestScore && game.displayScore > 0));
  }
}

// ----- Starfield (render-only, parallax; not part of engine state) -----
const stars = [];
function initStars() {
  stars.length = 0;
  const rng = (() => { let a = 9911; return () => (a = (a * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff; })();
  for (let i = 0; i < 90; i++) {
    stars.push({ x: rng() * WORLD.w, y: rng() * WORLD.h, z: 0.3 + rng() * 1.4, r: 0.5 + rng() * 1.5 });
  }
}
initStars();

// ----- Render -----
function drawStars(dt) {
  for (const s of stars) {
    if (game.status === "playing") {
      s.y += s.z * 0.04 * dt;
      if (s.y > WORLD.h) { s.y = 0; s.x = (s.x * 1.7 + 53) % WORLD.w; }
    }
    ctx.globalAlpha = 0.4 + s.z * 0.4;
    ctx.fillStyle = s.z > 1 ? "#bcd4ff" : "#5a6aa8";
    ctx.fillRect(s.x, s.y, s.r, s.r);
  }
  ctx.globalAlpha = 1;
}

function drawRocket() {
  const r = game.rocket;
  const cx = r.x + r.w / 2;
  const flashing = isInvulnerable(game) && Math.floor(game.timeMs / 90) % 2 === 0;
  if (flashing) ctx.globalAlpha = 0.35;

  // exhaust flame
  if (game.status === "playing") {
    const flick = 6 + (Math.sin(game.timeMs / 40) + 1) * 5;
    const grad = ctx.createLinearGradient(cx, r.y + r.h, cx, r.y + r.h + flick + 8);
    grad.addColorStop(0, "#ffd24c");
    grad.addColorStop(1, "rgba(255,92,122,0)");
    ctx.fillStyle = grad;
    ctx.beginPath();
    ctx.moveTo(cx - 7, r.y + r.h - 2);
    ctx.lineTo(cx + 7, r.y + r.h - 2);
    ctx.lineTo(cx, r.y + r.h + flick + 8);
    ctx.closePath();
    ctx.fill();
  }

  // body
  ctx.fillStyle = "#e8ecff";
  ctx.beginPath();
  ctx.moveTo(cx, r.y);                       // nose
  ctx.lineTo(r.x + r.w, r.y + r.h * 0.7);     // right shoulder
  ctx.lineTo(r.x + r.w * 0.78, r.y + r.h);    // right base
  ctx.lineTo(r.x + r.w * 0.22, r.y + r.h);    // left base
  ctx.lineTo(r.x, r.y + r.h * 0.7);           // left shoulder
  ctx.closePath();
  ctx.fill();

  // fins
  ctx.fillStyle = "#b46cff";
  ctx.beginPath();
  ctx.moveTo(r.x, r.y + r.h * 0.7);
  ctx.lineTo(r.x - 6, r.y + r.h);
  ctx.lineTo(r.x + r.w * 0.22, r.y + r.h);
  ctx.closePath();
  ctx.fill();
  ctx.beginPath();
  ctx.moveTo(r.x + r.w, r.y + r.h * 0.7);
  ctx.lineTo(r.x + r.w + 6, r.y + r.h);
  ctx.lineTo(r.x + r.w * 0.78, r.y + r.h);
  ctx.closePath();
  ctx.fill();

  // window
  ctx.fillStyle = "#4cd6ff";
  ctx.beginPath();
  ctx.arc(cx, r.y + r.h * 0.42, 5, 0, Math.PI * 2);
  ctx.fill();

  ctx.globalAlpha = 1;
}

function drawAsteroid(a) {
  ctx.save();
  ctx.translate(a.x, a.y);
  ctx.rotate(a.rot);
  const sides = 9;
  ctx.beginPath();
  for (let i = 0; i < sides; i++) {
    const ang = (i / sides) * Math.PI * 2;
    // deterministic-ish jagged radius based on id+i so each rock looks distinct
    const jitter = 0.78 + ((Math.sin(a.id * 12.9898 + i * 4.1414) + 1) / 2) * 0.34;
    const rr = a.r * jitter;
    const px = Math.cos(ang) * rr;
    const py = Math.sin(ang) * rr;
    if (i === 0) ctx.moveTo(px, py); else ctx.lineTo(px, py);
  }
  ctx.closePath();
  const g = ctx.createRadialGradient(-a.r * 0.3, -a.r * 0.3, a.r * 0.2, 0, 0, a.r);
  g.addColorStop(0, "#9aa6c8");
  g.addColorStop(1, "#46507a");
  ctx.fillStyle = g;
  ctx.fill();
  ctx.lineWidth = 1.5;
  ctx.strokeStyle = "#2b3358";
  ctx.stroke();
  ctx.restore();
}

function drawBullets() {
  for (const b of game.bullets) {
    ctx.fillStyle = "#ffd24c";
    ctx.shadowColor = "#ffd24c";
    ctx.shadowBlur = 10;
    ctx.fillRect(b.x, b.y, b.w, b.h);
  }
  ctx.shadowBlur = 0;
}

function drawParticles() {
  for (const p of game.particles) {
    const t = Math.max(0, p.life / p.maxLife);
    ctx.globalAlpha = t;
    ctx.fillStyle = t > 0.5 ? "#ffd24c" : "#ff5c7a";
    const s = 1 + t * 3;
    ctx.fillRect(p.x - s / 2, p.y - s / 2, s, s);
  }
  ctx.globalAlpha = 1;
}

function render(dt) {
  ctx.clearRect(0, 0, WORLD.w, WORLD.h);
  // subtle vignette background
  ctx.fillStyle = "#04061a";
  ctx.fillRect(0, 0, WORLD.w, WORLD.h);
  drawStars(dt);
  drawParticles();
  for (const a of game.asteroids) drawAsteroid(a);
  drawBullets();
  if (game.status !== "ready") drawRocket();
}

// ----- HUD -----
let lastHud = "";
function renderHud() {
  const hearts = "♥".repeat(Math.max(0, game.lives)) || "—";
  const key = `${game.displayScore}|${game.level}|${hearts}|${game.bestScore}`;
  if (key === lastHud) return;
  lastHud = key;
  el.score.textContent = game.displayScore;
  el.level.textContent = game.level;
  el.lives.textContent = hearts;
  el.best.textContent = game.bestScore;
}

// ----- Fixed-timestep loop -----
const STEP = 1000 / 120; // 120 Hz logic for smoothness/determinism
let acc = 0;
let prev = performance.now();
let wasOver = false;

function frame(now) {
  let dt = now - prev;
  prev = now;
  if (dt > 250) dt = 250; // avoid huge catch-up after tab switch
  acc += dt;
  while (acc >= STEP) {
    update(game, STEP, input);
    acc -= STEP;
  }

  // detect transition into gameover to persist best + show overlay once
  if (game.status === "gameover" && !wasOver) {
    wasOver = true;
    if (game.displayScore > loadBest()) saveBest(game.bestScore);
    syncOverlays();
  }
  if (game.status !== "gameover") wasOver = false;

  render(dt);
  renderHud();
  requestAnimationFrame(frame);
}

// initial paint + overlay
syncOverlays();
renderHud();
requestAnimationFrame(frame);

// Expose minimal hooks for automated/manual debugging (non-essential).
window.__starAscent = { game, launch, fire: () => fire(game), input };
