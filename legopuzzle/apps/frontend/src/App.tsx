import { useEffect, useMemo, useState, type CSSProperties, type FormEvent } from 'react'
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
const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? '').trim().replace(/\/$/, '')

type LeaderboardEntry = {
  id?: string
  displayName: string
  score: number
  moves: number
  seconds: number
  createdAt?: string
}

function createShuffledBoard() {
  const board = [...SOLVED_BOARD]

  do {
    for (let index = board.length - 1; index > 0; index -= 1) {
      const swapIndex = Math.floor(Math.random() * (index + 1))
      ;[board[index], board[swapIndex]] = [board[swapIndex], board[index]]
    }
  } while (!isSolvable(board) || isSolved(board))

  return board
}

function isSolvable(board: number[]) {
  const numbers = board.filter((value) => value !== 0)
  let inversions = 0

  for (let index = 0; index < numbers.length; index += 1) {
    for (let compareIndex = index + 1; compareIndex < numbers.length; compareIndex += 1) {
      if (numbers[index] > numbers[compareIndex]) {
        inversions += 1
      }
    }
  }

  return inversions % 2 === 0
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

function formatDuration(seconds: number) {
  const minutes = Math.floor(seconds / 60)
  const remainingSeconds = seconds % 60
  return `${String(minutes).padStart(2, '0')}:${String(remainingSeconds).padStart(2, '0')}`
}

function getScore(moves: number, seconds: number, completed: boolean) {
  const baseScore = 1500 - moves * 25 - seconds * 8
  return Math.max(0, baseScore + (completed ? 300 : 0))
}

function getApiUrl(path: string) {
  return API_BASE_URL ? `${API_BASE_URL}${path}` : path
}

async function fetchLeaderboard(signal?: AbortSignal) {
  const response = await fetch(getApiUrl('/leaderboard'), { signal })

  if (!response.ok) {
    throw new Error('Unable to load the leaderboard right now.')
  }

  const payload = (await response.json()) as LeaderboardEntry[]
  return payload
    .slice()
    .sort(
      (first, second) =>
        second.score - first.score ||
        first.seconds - second.seconds ||
        first.moves - second.moves ||
        first.displayName.localeCompare(second.displayName),
    )
}

async function submitScore(entry: Omit<LeaderboardEntry, 'id' | 'createdAt'>) {
  const response = await fetch(getApiUrl('/leaderboard'), {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(entry),
  })

  if (!response.ok) {
    throw new Error('Unable to submit your score yet.')
  }
}

function App() {
  const [board, setBoard] = useState(() => createShuffledBoard())
  const [moves, setMoves] = useState(0)
  const [elapsedSeconds, setElapsedSeconds] = useState(0)
  const [startedAt, setStartedAt] = useState<number | null>(null)
  const [completed, setCompleted] = useState(false)
  const [displayName, setDisplayName] = useState('')
  const [submitted, setSubmitted] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [leaderboard, setLeaderboard] = useState<LeaderboardEntry[]>([])
  const [leaderboardLoading, setLeaderboardLoading] = useState(true)
  const [leaderboardError, setLeaderboardError] = useState<string | null>(null)
  const score = useMemo(
    () => getScore(moves, elapsedSeconds, completed),
    [completed, elapsedSeconds, moves],
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
    const abortController = new AbortController()

    fetchLeaderboard(abortController.signal)
      .then(setLeaderboard)
      .catch((error: unknown) => {
        if (abortController.signal.aborted) {
          return
        }

        setLeaderboardError(error instanceof Error ? error.message : 'Unable to load leaderboard.')
      })
      .finally(() => {
        if (!abortController.signal.aborted) {
          setLeaderboardLoading(false)
        }
      })

    return () => abortController.abort()
  }, [])

  function restartGame() {
    setBoard(createShuffledBoard())
    setMoves(0)
    setElapsedSeconds(0)
    setStartedAt(null)
    setCompleted(false)
    setSubmitted(false)
    setSubmitError(null)
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

    if (!startedAt) {
      setStartedAt(startTime)
    }

    setElapsedSeconds(nextElapsedSeconds)

    if (solved) {
      setCompleted(true)
    }
  }

  async function handleScoreSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!completed || submitted || !displayName.trim()) {
      return
    }

    setSubmitting(true)
    setSubmitError(null)

    try {
      await submitScore({
        displayName: displayName.trim(),
        score,
        moves,
        seconds: elapsedSeconds,
      })

      const refreshedLeaderboard = await fetchLeaderboard()
      setLeaderboard(refreshedLeaderboard)
      setSubmitted(true)
    } catch (error) {
      setSubmitError(error instanceof Error ? error.message : 'Unable to submit score.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <main className="app-shell">
      <section className="hero-panel">
        <div className="hero-copy">
          <p className="eyebrow">Lego Puzzle Playground</p>
          <h1>Snap every brick back into place.</h1>
          <p className="hero-text">
            Slide the colorful bricks into order, beat the clock, and post your best run to the
            leaderboard.
          </p>
        </div>
        <button className="restart-button" type="button" onClick={restartGame}>
          Shuffle new puzzle
        </button>
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
            </div>
            {completed ? <span className="completion-badge">Great build!</span> : null}
          </div>

          <div className="board" role="grid" aria-label="Sliding brick puzzle">
            {board.map((tile, index) =>
              tile === 0 ? (
                <div key={`empty-${index}`} aria-hidden="true" className="tile tile--empty" />
              ) : (
                <button
                  key={tile}
                  className="tile"
                  style={{ '--tile-color': TILE_COLORS[tile - 1] } as CSSProperties}
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
              <h2>Leaderboard</h2>
              <p>Top builders powered by the backend API.</p>
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
              <button disabled={!completed || submitting || submitted || !displayName.trim()} type="submit">
                {submitted ? 'Submitted' : submitting ? 'Saving…' : 'Submit score'}
              </button>
            </div>
            <p className="score-form__hint">
              Finish the puzzle to submit. Your score uses time, moves, and a completion bonus.
            </p>
            {submitError ? <p className="message message--error">{submitError}</p> : null}
            {submitted ? <p className="message message--success">Score submitted to the leaderboard.</p> : null}
          </form>

          {leaderboardLoading ? <p className="leaderboard-state">Loading leaderboard…</p> : null}
          {leaderboardError ? <p className="leaderboard-state message--error">{leaderboardError}</p> : null}

          {!leaderboardLoading && !leaderboardError ? (
            leaderboard.length > 0 ? (
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
            )
          ) : null}
        </article>
      </section>
    </main>
  )
}

export default App
