import GameClient from "@/components/GameClient";
import { MODE_LIST } from "@/lib/modes";

export default function Home() {
  return (
    <main className="mx-auto flex min-h-screen max-w-5xl flex-col gap-8 px-4 py-8">
      <header className="text-center">
        <p className="text-xs font-semibold uppercase tracking-[0.3em] text-white/40">
          Browser survival game
        </p>
        <h1 className="mt-2 bg-gradient-to-r from-sky-400 via-fuchsia-400 to-violet-400 bg-clip-text text-4xl font-black text-transparent sm:text-5xl">
          Last Stand
        </h1>
        <p className="mx-auto mt-3 max-w-xl text-sm text-white/60">
          Stay alive in the arena as enemies close in from every side. Keep moving,
          grab score orbs, and outlast the escalating waves. Three modes, three very
          different fights.
        </p>
      </header>

      <GameClient />

      <section className="grid gap-4 sm:grid-cols-3">
        {MODE_LIST.map((m) => (
          <article
            key={m.id}
            className="rounded-2xl border border-white/10 bg-white/[0.03] p-5"
          >
            <div className="flex items-center gap-2">
              <span
                className="inline-block h-3 w-3 rounded-full"
                style={{ background: m.accent }}
              />
              <h2 className="font-bold text-white">{m.name}</h2>
            </div>
            <p className="mt-1 text-xs font-medium text-white/45">{m.tagline}</p>
            <p className="mt-3 text-sm leading-6 text-white/65">{m.description}</p>
            <ul className="mt-3 space-y-1 text-xs text-white/45">
              <li>Lives: {m.startingLives}</li>
              <li>Score multiplier: ×{m.scoreMultiplier}</li>
              {m.visionRadius ? <li>Limited vision ({m.visionRadius}px light)</li> : null}
            </ul>
          </article>
        ))}
      </section>

      <section className="rounded-2xl border border-white/10 bg-white/[0.03] p-5">
        <h2 className="font-bold text-white">How to play</h2>
        <ul className="mt-3 grid gap-2 text-sm text-white/65 sm:grid-cols-2">
          <li>
            <span className="font-semibold text-white">Move:</span> WASD or arrow keys,
            or hold the mouse/touch to steer toward the cursor.
          </li>
          <li>
            <span className="font-semibold text-white">Goal:</span> survive as long as
            possible — score rises with time, waves, and score orbs.
          </li>
          <li>
            <span className="font-semibold text-white">Avoid:</span> enemies. Each hit
            costs a life; you get brief invulnerability after being hit.
          </li>
          <li>
            <span className="font-semibold text-white">Pause:</span> press P anytime.
            Your best score per mode is saved locally.
          </li>
        </ul>
      </section>

      <footer className="pb-6 text-center text-xs text-white/30">
        Last Stand — an MVP survival game. Single-player, no install, pure browser.
      </footer>
    </main>
  );
}
