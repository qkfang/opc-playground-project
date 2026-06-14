// proj38 — main: wire engine + renderer + input, run the loop, drive HUD + overlays + persistence.
import * as engine from "./engine.js";
import { Renderer } from "./render.js";
import { Input } from "./input.js";

const { createGame, reset, start, togglePause, update, snapshot, WORLD, CONFIG } = engine;

const BEST_KEY = "proj38_best_score";
const canvas = document.getElementById("game");
const renderer = new Renderer(canvas);

let bestScore = 0;
try { bestScore = parseInt(localStorage.getItem(BEST_KEY) || "0", 10) || 0; } catch {}

let seed = (Math.random() * 1e9) | 0;
let state = createGame({ seed, bestScore });

// ---- HUD elements ----
const el = (id) => document.getElementById(id);
const ui = {
  hpFill: el("hpFill"),
  hpText: el("hpText"),
  strengthPips: el("strengthPips"),
  score: el("score"),
  best: el("best"),
  level: el("level"),
  cooldown: el("cooldownFill"),
  overlay: el("overlay"),
  overlayTitle: el("overlayTitle"),
  overlaySub: el("overlaySub"),
  overlayBtn: el("overlayBtn"),
  pauseBadge: el("pauseBadge"),
  enrageBadge: el("enrageBadge"),
};

// build strength pips
function buildPips() {
  ui.strengthPips.innerHTML = "";
  for (let i = 0; i < CONFIG.cohort.strength; i++) {
    const p = document.createElement("span");
    p.className = "pip";
    ui.strengthPips.appendChild(p);
  }
}
buildPips();

// ---- control actions ----
function doStart() {
  if (state.status === "ready") { start(state); hideOverlay(); }
}
function doPause() {
  if (state.status === "playing") {
    const paused = togglePause(state);
    ui.pauseBadge.classList.toggle("show", paused);
  }
}
function doRestart() {
  seed = (Math.random() * 1e9) | 0;
  reset(state, { seed, bestScore });
  buildPips();
  ui.pauseBadge.classList.remove("show");
  showTitle();
}

const input = new Input(canvas, { onStart: doStart, onPause: doPause, onRestart: doRestart });
input.bindTouchButtons({ leftBtn: el("btnLeft"), rightBtn: el("btnRight"), fireBtn: el("btnFire") });

el("overlayBtn").addEventListener("click", () => {
  if (state.status === "ready") doStart();
  else doRestart();
});
el("btnPause").addEventListener("click", doPause);
window.addEventListener("resize", () => renderer.resize());

// ---- overlays ----
function showTitle() {
  ui.overlay.classList.add("show");
  ui.overlay.classList.remove("win", "lose");
  ui.overlayTitle.textContent = "LEGIONS vs THE CASTLE DRAGON";
  ui.overlaySub.innerHTML =
    "Hold the line, Centurion. Volley your <b>pila</b> at the dragon and break it before it breaks your cohort.<br><br>" +
    "<b>← →</b> / <b>A D</b> move &nbsp;·&nbsp; <b>Space</b> / click throw &nbsp;·&nbsp; <b>P</b> pause &nbsp;·&nbsp; <b>R</b> restart";
  ui.overlayBtn.textContent = "▶ Start the battle";
}
function showEnd(won) {
  ui.overlay.classList.add("show");
  ui.overlay.classList.toggle("win", won);
  ui.overlay.classList.toggle("lose", !won);
  const snap = snapshot(state);
  ui.overlayTitle.textContent = won ? "🏛️  VICTORY — the keep stands!" : "🔥  THE LINE IS BROKEN";
  ui.overlaySub.innerHTML = won
    ? `The dragon falls. Rome endures.<br><br>Score <b>${snap.score}</b> &nbsp;·&nbsp; Best <b>${snap.best}</b> &nbsp;·&nbsp; Hits <b>${snap.hits}</b>`
    : `The cohort is overrun.<br><br>Score <b>${snap.score}</b> &nbsp;·&nbsp; Best <b>${snap.best}</b> &nbsp;·&nbsp; Dragon left at <b>${Math.ceil(snap.dragonHpFrac * 100)}%</b>`;
  ui.overlayBtn.textContent = "↻ Fight again";
}
function hideOverlay() { ui.overlay.classList.remove("show"); }

showTitle();

// ---- HUD update ----
let endShown = false;
function updateHud() {
  const snap = snapshot(state);
  const frac = Math.max(0, snap.dragonHpFrac);
  ui.hpFill.style.width = (frac * 100).toFixed(1) + "%";
  ui.hpFill.classList.toggle("enraged", snap.enraged);
  ui.hpText.textContent = `DRAGON  ${Math.ceil(snap.dragonHp)} / ${CONFIG.dragon.maxHp}`;
  ui.score.textContent = snap.score.toLocaleString();
  ui.best.textContent = snap.best.toLocaleString();
  ui.level.textContent = snap.level === 3 ? "FINAL STAND" : snap.level === 2 ? "ENRAGED" : "ADVANCING";
  ui.enrageBadge.classList.toggle("show", snap.enraged);

  // strength pips
  const pips = ui.strengthPips.children;
  for (let i = 0; i < pips.length; i++) {
    pips[i].classList.toggle("down", i >= snap.alive);
  }

  // cooldown bar (1 = ready)
  const cdFrac = 1 - Math.min(1, state.pilaCd / CONFIG.pila.cooldownMs);
  ui.cooldown.style.width = (cdFrac * 100).toFixed(0) + "%";
}

// ---- main loop ----
let last = performance.now();
function loop(now) {
  let dt = now - last;
  last = now;
  if (dt > 60) dt = 60; // clamp big tab-switch gaps

  const frameInput = input.frame();

  // pointer steering: if pointer active and playing, nudge aim toward pointer lane
  const pn = input.pointerAimNorm();
  if (pn !== null && state.status === "playing" && !state.paused) {
    const targetLane = pn * WORLD.maxX;
    const diff = targetLane - state.aim.x;
    if (Math.abs(diff) > 0.15) {
      if (diff > 0) frameInput.right = true; else frameInput.left = true;
    }
  }

  update(state, dt, frameInput);
  renderer.sync(state, dt);
  renderer.render();
  updateHud();

  // end-state overlay (once)
  if ((state.status === "won" || state.status === "lost")) {
    if (!endShown) {
      endShown = true;
      // persist best
      if (state.bestScore > bestScore) {
        bestScore = Math.round(state.bestScore);
        try { localStorage.setItem(BEST_KEY, String(bestScore)); } catch {}
      }
      setTimeout(() => showEnd(state.status === "won"), 600);
    }
  } else {
    endShown = false;
  }

  requestAnimationFrame(loop);
}
requestAnimationFrame(loop);

// expose for debugging / smoke-in-browser
window.__proj38 = { engine, get state() { return state; }, renderer };

// Optional demo/QA autopilot: ?demo=1 auto-starts and auto-volleys with gentle sweeping aim
// so the scene reaches an active combat state without manual input (handy for screenshots/QA).
try {
  const params = new URLSearchParams(location.search);
  if (params.get("demo") === "1") {
    doStart();
    let dir = 1;
    setInterval(() => {
      if (state.status === "ready") doStart();
      if (state.status === "won" || state.status === "lost") return;
      // sweep the aim back and forth across the lane
      if (state.aim.x > WORLD.maxX - 1) dir = -1;
      if (state.aim.x < WORLD.minX + 1) dir = 1;
      input.left = dir < 0; input.right = dir > 0;
      input.fireHeld = true; // engine rate-limits volleys
    }, 250);
  }
} catch {}
