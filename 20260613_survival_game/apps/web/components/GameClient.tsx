"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { SurvivalGame, GameSnapshot } from "@/lib/engine";
import { GameMode, MODE_LIST, ModeId, getMode } from "@/lib/modes";
import { formatScore, formatTime } from "@/lib/format";

const ARENA_W = 880;
const ARENA_H = 560;
const BEST_KEY = "survival-best-scores";

type Screen = "menu" | "playing" | "gameover";

interface BestScores {
  [mode: string]: number;
}

function loadBest(): BestScores {
  if (typeof window === "undefined") return {};
  try {
    return JSON.parse(window.localStorage.getItem(BEST_KEY) || "{}");
  } catch {
    return {};
  }
}

function saveBest(scores: BestScores) {
  try {
    window.localStorage.setItem(BEST_KEY, JSON.stringify(scores));
  } catch {
    /* ignore */
  }
}

export default function GameClient() {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const gameRef = useRef<SurvivalGame | null>(null);
  const rafRef = useRef<number | null>(null);
  const lastTsRef = useRef<number>(0);

  const [screen, setScreen] = useState<Screen>("menu");
  const [modeId, setModeId] = useState<ModeId>("classic");
  const [hud, setHud] = useState<GameSnapshot | null>(null);
  const [best, setBest] = useState<BestScores>({});
  const [finalSnap, setFinalSnap] = useState<GameSnapshot | null>(null);
  const [newBest, setNewBest] = useState(false);

  useEffect(() => {
    setBest(loadBest());
  }, []);

  // Keyboard input.
  useEffect(() => {
    const keyMap: Record<string, keyof typeof keyState> = {
      arrowup: "up",
      w: "up",
      arrowdown: "down",
      s: "down",
      arrowleft: "left",
      a: "left",
      arrowright: "right",
      d: "right",
    };
    const keyState = { up: false, down: false, left: false, right: false };

    function apply() {
      gameRef.current?.setInput({ ...keyState });
    }
    function onDown(e: KeyboardEvent) {
      const k = e.key.toLowerCase();
      if (k in keyMap) {
        e.preventDefault();
        keyState[keyMap[k]] = true;
        apply();
      }
      if (k === "p") togglePause();
    }
    function onUp(e: KeyboardEvent) {
      const k = e.key.toLowerCase();
      if (k in keyMap) {
        keyState[keyMap[k]] = false;
        apply();
      }
    }
    window.addEventListener("keydown", onDown);
    window.addEventListener("keyup", onUp);
    return () => {
      window.removeEventListener("keydown", onDown);
      window.removeEventListener("keyup", onUp);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const [paused, setPaused] = useState(false);
  const pausedRef = useRef(false);
  const togglePause = useCallback(() => {
    if (!gameRef.current || gameRef.current.status !== "running") return;
    pausedRef.current = !pausedRef.current;
    setPaused(pausedRef.current);
  }, []);

  const loop = useCallback((ts: number) => {
    const game = gameRef.current;
    const canvas = canvasRef.current;
    if (!game || !canvas) return;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;

    const last = lastTsRef.current || ts;
    let dt = (ts - last) / 1000;
    lastTsRef.current = ts;
    if (dt > 0.1) dt = 0.1;

    if (!pausedRef.current) {
      game.update(dt);
    }
    game.render(ctx);

    if (paused || pausedRef.current) {
      ctx.fillStyle = "rgba(0,0,0,0.55)";
      ctx.fillRect(0, 0, ARENA_W, ARENA_H);
      ctx.fillStyle = "#e7ecf5";
      ctx.font = "bold 34px ui-sans-serif, system-ui";
      ctx.textAlign = "center";
      ctx.fillText("Paused", ARENA_W / 2, ARENA_H / 2);
      ctx.font = "16px ui-sans-serif, system-ui";
      ctx.fillText("Press P to resume", ARENA_W / 2, ARENA_H / 2 + 30);
      ctx.textAlign = "start";
    }

    const snap = game.snapshot();
    setHud(snap);

    if (snap.status === "gameover") {
      handleGameOver(snap);
      return;
    }
    rafRef.current = requestAnimationFrame(loop);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [paused]);

  const handleGameOver = useCallback((snap: GameSnapshot) => {
    if (rafRef.current) cancelAnimationFrame(rafRef.current);
    rafRef.current = null;
    setFinalSnap(snap);
    setScreen("gameover");
    setBest((prev) => {
      const current = prev[modeId] ?? 0;
      if (snap.score > current) {
        const updated = { ...prev, [modeId]: snap.score };
        saveBest(updated);
        setNewBest(true);
        return updated;
      }
      setNewBest(false);
      return prev;
    });
  }, [modeId]);

  const startGame = useCallback((mode: GameMode) => {
    const game = new SurvivalGame(ARENA_W, ARENA_H, mode);
    game.start();
    gameRef.current = game;
    lastTsRef.current = 0;
    pausedRef.current = false;
    setPaused(false);
    setFinalSnap(null);
    setNewBest(false);
    setScreen("playing");
    if (rafRef.current) cancelAnimationFrame(rafRef.current);
    rafRef.current = requestAnimationFrame(loop);
  }, [loop]);

  // Restart loop reference when `loop` identity changes mid-game (pause toggling).
  useEffect(() => {
    if (screen === "playing" && gameRef.current?.status === "running" && rafRef.current == null) {
      rafRef.current = requestAnimationFrame(loop);
    }
  }, [loop, screen]);

  useEffect(() => {
    return () => {
      if (rafRef.current) cancelAnimationFrame(rafRef.current);
    };
  }, []);

  // Pointer steering (hold mouse/touch to move toward cursor).
  const pointerActive = useRef(false);
  function pointerPos(e: React.PointerEvent<HTMLCanvasElement>) {
    const rect = e.currentTarget.getBoundingClientRect();
    const scaleX = ARENA_W / rect.width;
    const scaleY = ARENA_H / rect.height;
    return { x: (e.clientX - rect.left) * scaleX, y: (e.clientY - rect.top) * scaleY };
  }
  function onPointerDown(e: React.PointerEvent<HTMLCanvasElement>) {
    pointerActive.current = true;
    gameRef.current?.setInput({ pointer: pointerPos(e) });
  }
  function onPointerMove(e: React.PointerEvent<HTMLCanvasElement>) {
    if (pointerActive.current) gameRef.current?.setInput({ pointer: pointerPos(e) });
  }
  function onPointerUp() {
    pointerActive.current = false;
    gameRef.current?.setInput({ pointer: null });
  }

  const mode = getMode(modeId)!;

  return (
    <div className="flex flex-col items-center gap-5">
      <div className="relative" style={{ width: "100%", maxWidth: ARENA_W }}>
        <canvas
          ref={canvasRef}
          width={ARENA_W}
          height={ARENA_H}
          className="w-full rounded-2xl border border-white/10 bg-[#0a0e17] shadow-2xl"
          style={{ aspectRatio: `${ARENA_W} / ${ARENA_H}`, touchAction: "none" }}
          onPointerDown={onPointerDown}
          onPointerMove={onPointerMove}
          onPointerUp={onPointerUp}
          onPointerLeave={onPointerUp}
        />

        {/* HUD overlay while playing */}
        {screen === "playing" && hud && (
          <div className="pointer-events-none absolute inset-x-0 top-0 flex items-start justify-between p-3 text-sm font-semibold">
            <div className="flex gap-2">
              <Stat label="Time" value={formatTime(hud.timeSurvived)} />
              <Stat label="Score" value={formatScore(hud.score)} />
              <Stat label="Wave" value={String(hud.wave + 1)} />
            </div>
            <div className="flex gap-2">
              <Stat label="Lives" value={"♥".repeat(Math.max(0, hud.lives)) || "—"} />
              <button
                onClick={togglePause}
                className="pointer-events-auto rounded-lg border border-white/15 bg-black/40 px-3 py-1 text-xs text-white/80 backdrop-blur hover:bg-black/60"
              >
                {paused ? "Resume" : "Pause"} (P)
              </button>
            </div>
          </div>
        )}

        {/* Menu overlay */}
        {screen === "menu" && (
          <MenuOverlay
            modeId={modeId}
            setModeId={setModeId}
            best={best}
            onStart={() => startGame(mode)}
          />
        )}

        {/* Game over overlay */}
        {screen === "gameover" && finalSnap && (
          <GameOverOverlay
            mode={mode}
            snap={finalSnap}
            newBest={newBest}
            best={best[modeId] ?? 0}
            onRestart={() => startGame(mode)}
            onMenu={() => setScreen("menu")}
          />
        )}
      </div>

      <p className="text-center text-xs text-white/40">
        Move: WASD / Arrow keys · or hold the mouse to steer · Pause: P
      </p>
    </div>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-white/10 bg-black/45 px-3 py-1 backdrop-blur">
      <span className="mr-1 text-[10px] uppercase tracking-wide text-white/45">{label}</span>
      <span className="text-white">{value}</span>
    </div>
  );
}

function MenuOverlay({
  modeId,
  setModeId,
  best,
  onStart,
}: {
  modeId: ModeId;
  setModeId: (m: ModeId) => void;
  best: BestScores;
  onStart: () => void;
}) {
  const mode = getMode(modeId)!;
  return (
    <div className="absolute inset-0 flex flex-col items-center justify-center gap-5 rounded-2xl bg-black/70 p-6 backdrop-blur-sm">
      <div className="text-center">
        <h2 className="text-2xl font-bold text-white">Choose your mode</h2>
        <p className="mt-1 text-sm text-white/55">
          Survive as long as you can. Each mode plays differently.
        </p>
      </div>

      <div className="grid w-full max-w-2xl gap-3 sm:grid-cols-3">
        {MODE_LIST.map((m) => {
          const selected = m.id === modeId;
          return (
            <button
              key={m.id}
              onClick={() => setModeId(m.id)}
              className={`rounded-xl border p-3 text-left transition ${
                selected
                  ? "border-white/40 bg-white/10"
                  : "border-white/10 bg-black/30 hover:border-white/25"
              }`}
              style={selected ? { boxShadow: `0 0 0 1px ${m.accent}55` } : undefined}
            >
              <div className="flex items-center gap-2">
                <span
                  className="inline-block h-3 w-3 rounded-full"
                  style={{ background: m.accent }}
                />
                <span className="font-semibold text-white">{m.name}</span>
              </div>
              <p className="mt-1 text-xs text-white/55">{m.tagline}</p>
              {best[m.id] ? (
                <p className="mt-2 text-[11px] text-white/40">Best: {formatScore(best[m.id])}</p>
              ) : null}
            </button>
          );
        })}
      </div>

      <p className="max-w-xl text-center text-sm text-white/65">{mode.description}</p>

      <button
        onClick={onStart}
        className="rounded-full px-8 py-3 text-base font-bold text-black transition hover:brightness-110"
        style={{ background: mode.accent }}
      >
        Play {mode.name}
      </button>
    </div>
  );
}

function GameOverOverlay({
  mode,
  snap,
  newBest,
  best,
  onRestart,
  onMenu,
}: {
  mode: GameMode;
  snap: GameSnapshot;
  newBest: boolean;
  best: number;
  onRestart: () => void;
  onMenu: () => void;
}) {
  return (
    <div className="absolute inset-0 flex flex-col items-center justify-center gap-4 rounded-2xl bg-black/75 p-6 text-center backdrop-blur-sm">
      <h2 className="text-3xl font-bold text-white">Game Over</h2>
      <p className="text-sm text-white/55">{mode.name} mode</p>

      {newBest && (
        <span
          className="rounded-full px-3 py-1 text-xs font-bold text-black"
          style={{ background: mode.accent }}
        >
          ★ New best score!
        </span>
      )}

      <div className="grid grid-cols-2 gap-3 text-left sm:grid-cols-4">
        <Result label="Score" value={formatScore(snap.score)} />
        <Result label="Survived" value={formatTime(snap.timeSurvived)} />
        <Result label="Wave reached" value={String(snap.wave + 1)} />
        <Result label="Best" value={formatScore(Math.max(best, snap.score))} />
      </div>

      <div className="mt-2 flex gap-3">
        <button
          onClick={onRestart}
          className="rounded-full px-6 py-2.5 text-sm font-bold text-black transition hover:brightness-110"
          style={{ background: mode.accent }}
        >
          Play again
        </button>
        <button
          onClick={onMenu}
          className="rounded-full border border-white/20 px-6 py-2.5 text-sm font-semibold text-white/85 hover:bg-white/10"
        >
          Change mode
        </button>
      </div>
    </div>
  );
}

function Result({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl border border-white/10 bg-black/40 px-4 py-3">
      <div className="text-[10px] uppercase tracking-wide text-white/45">{label}</div>
      <div className="text-lg font-bold text-white">{value}</div>
    </div>
  );
}
