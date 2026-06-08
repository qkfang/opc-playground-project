import { useEffect, useMemo, useState, type CSSProperties, type FormEvent } from 'react'
import { difficulties, type Difficulty } from './data/difficulties'
import './App.css'

const BOARD_SIZE = 3
const SOLVED_BOARD = [1, 2, 3, 4, 5, 6, 7, 8, 0]
const TILE_COLORS = [
  '#ef4444',
  '#f97316',
  '#facc15',
  '#22c55e',
  '#06b6d4',
  '#3b82f6',
  '#8b5cf6',
  '#ec4899',
]

type LeaderboardEntry = {
  id?: string
  displayName: string
  score: number
  moves: number
  seconds: number
  createdAt?: string
}

type DifficultyConfig = {
  shuffleMoves: number
  hintEnabled: boolean
  scoreMultiplier: number
  completionBonus: number
  tileGlow: string
  helperText: string
}

const difficultyConfigs: Record<string, DifficultyConfig> = {
  'very-easy': {
    shuffleMoves: 6,
    hintEnabled: true,
    scoreMultiplier: 0.75,
    completionBonus: 450,
    tileGlow: 'rgba(34, 197, 94, 0.45)',
    helperText: 'Gentle mode: the puzzle starts close to solved and hints stay enabled.',
  },
  easy: {
    shuffleMoves: 12,
    hintEnabled: true,
    scoreMultiplier: 0.9,
    completionBonus: 375,
    tileGlow: 'rgba(132, 204, 22, 0.4)',
    helperText: 'Relaxed mode: a shorter shuffle with hints to keep you moving.',
  },
  medium: {
    shuffleMoves: 24,
    hintEnabled: true,
    scoreMultiplier: 1,
    completionBonus: 300,
    tileGlow: 'rgba(234, 179, 8, 0.4)',
    helperText: 'Balanced mode: the classic puzzle with the standard scoring curve.',
  },
  hard: {
    shuffleMoves: 40,
    hintEnabled: false,
    scoreMultiplier: 1.2,
    completionBonus: 250,
    tileGlow: 'rgba(249, 115, 22, 0.45)',
    helperText: 'Hard mode: deeper shuffle, no hints, and tighter scoring pressure.',
  },
  insane: {
    shuffleMoves: 70,
    hintEnabled: false,
    scoreMultiplier: 1.45,
    completionBonus: 200,
    tileGlow: 'rgba(239, 68, 68, 0.55)',
    helperText: 'Insane mode: maximum chaos, no safety rails, highest score ceiling.',
  },
}

function isSolved(board: number[]) {
  return board.every((value, index) => value === SOLVED_BOARD[index])
}

function isAdjacent(first: number, second: number) {
  const firstRow = Math.floor(first / BOARD_SIZE)
  const firstColumn = first % BOARD_SIZE
  const secondRow = Math.floor(second / BOARD_SIZE)
  const secondColumn = second % BOARD_SIZE

  return Math.abs(firstRow - secondRow) + Math.abs(firstColumn - secondColumn) === 1
}

function getAdjacentIndices(emptyIndex: number) {
  return SOLVED_BOARD.map((_, index) => index).filter((index) => isAdjacent(index, emptyIndex))
}

function createBoardForDifficulty(config: DifficultyConfig) {
  const board = [...SOLVED_BOARD]
  let emptyIndex = board.indexOf(0)
  let previousIndex: number | null = null

  for (let move = 0; move < config.shuffleMoves; move += 1) {
    const adjacentIndices = getAdjacentIndices(emptyIndex).filter((index) => index !== previousIndex)
    const candidateIndices = adjacentIndices.length > 0 ? adjacentIndices : getAdjacentIndices(emptyIndex)
    const swapIndex = candidateIndices[Math.floor(Math.random() * candidateIndices.length)]
    ;[board[emptyIndex], board[swapIndex]] = [board[swapIndex], board[emptyIndex]]
    previousIndex = emptyIndex
    emptyIndex = swapIndex
  }

  if (isSolved(board)) {
    const adjacentIndices = getAdjacentIndices(emptyIndex)
    const swapIndex = adjacentIndices[0]
    ;[board[emptyIndex], board[swapIndex]] = [board[swapIndex], board[emptyIndex]]
  }

  return board
}

function getHintIndex(board: number[]) {
  const emptyIndex = board.indexOf(0)
  const adjacentIndices = getAdjacentIndices(emptyIndex)

  const winningMove = adjacentIndices.find((index) => {
    const nextBoard = [...board]
    ;[nextBoard[index], nextBoard[emptyIndex]] = [nextBoard[emptyIndex], nextBoard[index]]
    return isSolved(nextBoard)
  })

  if (winningMove !== undefined) {
    return winningMove
  }

  return adjacentIndices.reduce((bestIndex, currentIndex) => {
    const currentTile = board[currentIndex]

    if (bestIndex === null) {
      return currentIndex
    }

    const bestTile = board[bestIndex]
    const currentDistance = Math.abs(currentIndex - (currentTile - 1))
    const bestDistance = Math.abs(bestIndex - (bestTile - 1))

    return currentDistance < bestDistance ? currentIndex : bestIndex
  }, null as number | null)
}

function formatDuration(seconds: number) {
  const minutes = Math.floor(seconds / 60)
  const remainingSeconds = seconds % 60
  return `${String(minutes).padStart(2, '0')}:${String(remainingSeconds).padStart(2, '0')}`
}

function getScore(moves: number, seconds: number, difficultyId: string, completed: boolean) {
  const config = difficultyConfigs[difficultyId] ?? difficultyConfigs.medium
  const baseScore = 1500 - moves * 25 - seconds * 8
  const adjustedScore = Math.round(baseScore * config.scoreMultiplier)
  return Math.max(0, adjustedScore + (completed ? config.completionBonus : 0))
}

function getInitialSelectedId() {
  if (typeof window === 'undefined') {
    return 'medium'
  }

  const savedId = window.localStorage.getItem('difficulty-selection')
  const hasSavedDifficulty = difficulties.some((difficulty) => difficulty.id === savedId)

  return hasSavedDifficulty ? savedId ?? 'medium' : 'medium'
}

function getInitialLeaderboard(): LeaderboardEntry[] {
  if (typeof window === 'undefined') {
    return []
  }

  try {
    const rawValue = window.localStorage.getItem('difficulty-puzzle-leaderboard')
    if (!rawValue) {
      return []
    }

    const parsed = JSON.parse(rawValue) as LeaderboardEntry[]
    return Array.isArray(parsed) ? parsed : []
  } catch {
    return []
  }
}

function sortLeaderboard(entries: LeaderboardEntry[]) {
  return entries
    .slice()
    .sort(
      (first, second) =>
        second.score - first.score ||
        first.seconds - second.seconds ||
        first.moves - second.moves ||
        first.displayName.localeCompare(second.displayName),
    )
}

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
            <svg
              aria-hidden="true"
              viewBox="0 0 12 10"
              width="12"
              height="10"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            >
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
        <StatRow label="Enemy Spawn Rate" value={`${stats.spawnRate}x`} />
      </div>
    </button>
  )
}

function App() {
  const [selectedId, setSelectedId] = useState<string>(getInitialSelectedId)
  const [startedId, setStartedId] = useState<string | null>(null)
  const [board, setBoard] = useState<number[]>(() => createBoardForDifficulty(difficultyConfigs[getInitialSelectedId()]))
  const [moves, setMoves] = useState(0)
  const [elapsedSeconds, setElapsedSeconds] = useState(0)
  const [startedAt, setStartedAt] = useState<number | null>(null)
  const [completed, setCompleted] = useState(false)
  const [hintIndex, setHintIndex] = useState<number | null>(null)
  const [displayName, setDisplayName] = useState('')
  const [submitted, setSubmitted] = useState(false)
  const [leaderboard, setLeaderboard] = useState<LeaderboardEntry[]>(() => sortLeaderboard(getInitialLeaderboard()))

  const selected = difficulties.find((difficulty) => difficulty.id === selectedId) ?? difficulties[2]
  const started = difficulties.find((difficulty) => difficulty.id === startedId) ?? null
  const activeDifficulty = difficulties.find((difficulty) => difficulty.id === (startedId ?? selectedId)) ?? selected
  const activeConfig = difficultyConfigs[activeDifficulty.id] ?? difficultyConfigs.medium

  const score = useMemo(
    () => getScore(moves, elapsedSeconds, activeDifficulty.id, completed),
    [activeDifficulty.id, completed, elapsedSeconds, moves],
  )

  useEffect(() => {
    if (!startedAt || completed) {
      return undefined
    }

    const timerId = window.setInterval(() => {
      setElapsedSeconds(Math.floor((performance.now() - startedAt) / 1000))
    }, 1000)

    return () => window.clearInterval(timerId)
  }, [completed, startedAt])

  useEffect(() => {
    window.localStorage.setItem('difficulty-selection', selectedId)
  }, [selectedId])

  useEffect(() => {
    window.localStorage.setItem('difficulty-puzzle-leaderboard', JSON.stringify(leaderboard))
  }, [leaderboard])

  function resetBoardForDifficulty(difficultyId: string) {
    const config = difficultyConfigs[difficultyId] ?? difficultyConfigs.medium
    setBoard(createBoardForDifficulty(config))
    setMoves(0)
    setElapsedSeconds(0)
    setStartedAt(null)
    setCompleted(false)
    setHintIndex(null)
    setSubmitted(false)
  }

  function handleSelect(id: string) {
    setStartedId(null)
    setSelectedId(id)
    resetBoardForDifficulty(id)
  }

  function handleStart() {
    setStartedId(selected.id)
    resetBoardForDifficulty(selected.id)
  }

  function restartGame() {
    resetBoardForDifficulty(activeDifficulty.id)
  }

  function showHint() {
    if (completed || !activeConfig.hintEnabled) {
      return
    }

    setHintIndex(getHintIndex(board))
  }

  function handleTileClick(index: number, timestamp: number) {
    if (completed) {
      return
    }

    const emptyIndex = board.indexOf(0)
    if (!isAdjacent(index, emptyIndex)) {
      return
    }

    const nextBoard = [...board]
    ;[nextBoard[index], nextBoard[emptyIndex]] = [nextBoard[emptyIndex], nextBoard[index]]

    const moveTimestamp = Math.floor(timestamp)
    const startTime = startedAt ?? moveTimestamp
    const nextMoves = moves + 1
    const solved = isSolved(nextBoard)
    const nextElapsedSeconds = Math.floor((moveTimestamp - startTime) / 1000)

    setBoard(nextBoard)
    setMoves(nextMoves)
    setHintIndex(null)

    if (!startedAt) {
      setStartedAt(startTime)
    }

    setElapsedSeconds(nextElapsedSeconds)

    if (solved) {
      setCompleted(true)
    }
  }

  function handleScoreSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!completed || submitted || !displayName.trim()) {
      return
    }

    const nextEntries = sortLeaderboard([
      ...leaderboard,
      {
        id: `${Date.now()}`,
        displayName: displayName.trim(),
        score,
        moves,
        seconds: elapsedSeconds,
        createdAt: new Date().toISOString(),
      },
    ]).slice(0, 10)

    setLeaderboard(nextEntries)
    setSubmitted(true)
  }

  return (
    <main className="app-shell">
      <section className="hero">
        <div>
          <p className="eyebrow">Game Setup</p>
          <h1>Choose Your Difficulty</h1>
          <p className="hero-sub">
            Pick your challenge, then jump straight into the integrated Lego puzzle game.
          </p>
        </div>
        <div className="hero-inline-note">
          <strong>{activeDifficulty.label}</strong>
          <span>{activeConfig.helperText}</span>
        </div>
      </section>

      <section className="difficulty-grid" aria-label="Difficulty levels">
        {difficulties.map((difficulty) => (
          <DifficultyCard
            key={difficulty.id}
            difficulty={difficulty}
            selected={selectedId === difficulty.id}
            onSelect={handleSelect}
          />
        ))}
      </section>

      <section className="confirm-bar">
        <div className="confirm-copy">
          <p className="confirm-hint">
            Selected: <strong>{selected.label}</strong>
          </p>
          <p className="confirm-subhint">
            Start the puzzle with this difficulty preset. Shuffle depth, hints, and scoring all adapt.
          </p>
          {started ? (
            <p className="start-banner" role="status">
              Puzzle loaded on <strong>{started.label}</strong> mode.
            </p>
          ) : null}
        </div>
        <button className="play-button" type="button" onClick={handleStart}>
          {started?.id === selected.id ? `Restart ${selected.label}` : `Start Game -> ${selected.label}`}
        </button>
      </section>

      <section className="game-shell">
        <section className="hero-panel">
          <div className="hero-copy hero-copy--game">
            <p className="eyebrow">Lego Puzzle Playground</p>
            <h2>Snap every brick back into place.</h2>
            <p className="hero-text">
              Slide the colorful bricks into order, beat the clock, and post your best run to the local leaderboard.
            </p>
          </div>
          <div className="hero-actions">
            <button className="restart-button" type="button" onClick={restartGame}>
              Shuffle new puzzle
            </button>
            <button className="hint-button" type="button" onClick={showHint} disabled={!activeConfig.hintEnabled}>
              {activeConfig.hintEnabled ? 'Hint' : 'Hints disabled'}
            </button>
          </div>
        </section>

        <section className="dashboard" aria-label="Game stats">
          <article className="stat-card">
            <span>Timer</span>
            <strong>{formatDuration(elapsedSeconds)}</strong>
          </article>
          <article className="stat-card">
            <span>Moves</span>
            <strong>{moves}</strong>
          </article>
          <article className="stat-card">
            <span>Score</span>
            <strong>{score}</strong>
          </article>
          <article className={`stat-card ${completed ? 'stat-card--complete' : ''}`}>
            <span>Status</span>
            <strong>{completed ? 'Completed!' : startedAt ? 'Building…' : 'Ready'}</strong>
          </article>
        </section>

        <section className="content-grid">
          <article className="board-panel">
            <div className="panel-heading">
              <div>
                <h2>Brick board</h2>
                <p>Arrange the bricks from 1 to 8 with the empty space in the bottom-right corner.</p>
                <p className="board-tip">{activeConfig.helperText}</p>
              </div>
              {completed ? <span className="completion-badge">Great build!</span> : null}
            </div>

            <div className="board" role="grid" aria-label="Sliding brick puzzle">
              {board.map((tile, index) =>
                tile === 0 ? (
                  <div key={`empty-${index}`} aria-hidden="true" className="tile tile--empty" />
                ) : (
                  <button
                    key={`${tile}-${index}`}
                    className={`tile ${hintIndex === index ? 'tile--hint' : ''}`}
                    style={{
                      '--tile-color': TILE_COLORS[tile - 1],
                      '--hint-glow': activeConfig.tileGlow,
                    } as CSSProperties}
                    type="button"
                    onClick={(event) => handleTileClick(index, event.timeStamp)}
                  >
                    <span>{tile}</span>
                  </button>
                ),
              )}
            </div>
          </article>

          <article className="leaderboard-panel">
            <div className="panel-heading">
              <div>
                <h2>Local leaderboard</h2>
                <p>Integrated from the Lego puzzle experience, now stored directly in this app.</p>
              </div>
            </div>

            <form className="score-form" onSubmit={handleScoreSubmit}>
              <label htmlFor="display-name">Display name</label>
              <div className="score-form__row">
                <input
                  id="display-name"
                  maxLength={32}
                  placeholder="Brick Master"
                  value={displayName}
                  onChange={(event) => setDisplayName(event.target.value)}
                />
                <button disabled={!completed || submitted || !displayName.trim()} type="submit">
                  {submitted ? 'Submitted' : 'Save score'}
                </button>
              </div>
              <p className="score-form__hint">
                Finish the puzzle to submit. Scores vary by difficulty, moves, and time.
              </p>
              {submitted ? <p className="message message--success">Score saved to the local leaderboard.</p> : null}
            </form>

            {leaderboard.length > 0 ? (
              <ol className="leaderboard-list">
                {leaderboard.map((entry, index) => (
                  <li key={entry.id ?? `${entry.displayName}-${entry.score}-${index}`}>
                    <span className="leaderboard-rank">#{index + 1}</span>
                    <div>
                      <strong>{entry.displayName}</strong>
                      <p>
                        {entry.score} pts · {entry.moves} moves · {formatDuration(entry.seconds)}
                      </p>
                    </div>
                  </li>
                ))}
              </ol>
            ) : (
              <p className="leaderboard-state">No scores yet. Be the first builder on the board.</p>
            )}
          </article>
        </section>
      </section>
    </main>
  )
}

export default App
