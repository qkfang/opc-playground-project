import { useState } from 'react'
import { difficulties, type Difficulty } from './data/difficulties'
import './App.css'

type StatRowProps = {
  label: string
  value: string
}

function StatRow({ label, value }: StatRowProps) {
  return (
    <div className="stat-row">
      <span className="stat-label">{label}</span>
      <span className="stat-value">{value}</span>
    </div>
  )
}

type DifficultyCardProps = {
  difficulty: Difficulty
  selected: boolean
  onSelect: (id: string) => void
}

function DifficultyCard({ difficulty, selected, onSelect }: DifficultyCardProps) {
  const { id, label, description, accentColor, stats } = difficulty

  return (
    <button
      className={`difficulty-card${selected ? ' difficulty-card--selected' : ''}`}
      style={{ '--accent': accentColor } as React.CSSProperties}
      type="button"
      aria-pressed={selected}
      onClick={() => onSelect(id)}
    >
      <div className="card-header">
        <span className="card-badge">{label}</span>
        {selected && (
          <span className="card-check" aria-hidden="true">
            <svg aria-hidden="true" viewBox="0 0 12 10" width="12" height="10" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <polyline points="1,5 4.5,9 11,1" />
            </svg>
          </span>
        )}
      </div>
      <p className="card-description">{description}</p>
      <div className="card-stats">
        <StatRow label="Player Health" value={`${stats.playerHealth} HP`} />
        <StatRow label="Enemy Health" value={`${stats.enemyHealth} HP`} />
        <StatRow label="Enemy Damage" value={`${stats.enemyDamage} dmg`} />
        <StatRow label="Loot / Resources" value={`${stats.lootPercent}%`} />
        <StatRow label="Enemy Spawn Rate" value={`${stats.spawnRate}×`} />
      </div>
    </button>
  )
}

function App() {
  const [selectedId, setSelectedId] = useState<string | null>(null)

  const selected = difficulties.find((d) => d.id === selectedId) ?? null

  function handleSelect(id: string) {
    setSelectedId((prev) => (prev === id ? null : id))
  }

  return (
    <main className="app-shell">
      <section className="hero">
        <p className="eyebrow">Game Setup</p>
        <h1>Choose Your Difficulty</h1>
        <p className="hero-sub">
          Pick the challenge that fits your playstyle. You can always change it before starting a
          new session.
        </p>
      </section>

      <section className="difficulty-grid" aria-label="Difficulty levels">
        {difficulties.map((d) => (
          <DifficultyCard
            key={d.id}
            difficulty={d}
            selected={selectedId === d.id}
            onSelect={handleSelect}
          />
        ))}
      </section>

      <section className="confirm-bar">
        {selected ? (
          <>
            <p className="confirm-hint">
              Selected: <strong>{selected.label}</strong>
            </p>
            <button className="play-button" type="button">
              Play on {selected.label}
            </button>
          </>
        ) : (
          <p className="confirm-hint confirm-hint--muted">Select a difficulty to continue.</p>
        )}
      </section>
    </main>
  )
}

export default App
