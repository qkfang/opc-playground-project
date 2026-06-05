import { useEffect, useState, useCallback } from 'react'
import type { NewsItem } from './types'
import './App.css'

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? ''

function formatDate(iso: string): string {
  try {
    return new Intl.DateTimeFormat(undefined, {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date(iso))
  } catch {
    return iso
  }
}

function NewsCard({ item }: { item: NewsItem }) {
  return (
    <article className="news-card">
      <h2>
        <a href={item.url} target="_blank" rel="noopener noreferrer">
          {item.title}
        </a>
      </h2>
      <p className="news-meta">
        <span className="news-source">{item.source}</span>
        <span className="news-date">{formatDate(item.publishedAt)}</span>
      </p>
      {item.summary && <p className="news-summary">{item.summary}</p>}
      {item.tags.length > 0 && (
        <ul className="news-tags" aria-label="tags">
          {item.tags.map((tag) => (
            <li key={tag} className="news-tag">
              {tag}
            </li>
          ))}
        </ul>
      )}
    </article>
  )
}

function App() {
  const [items, setItems] = useState<NewsItem[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [refreshKey, setRefreshKey] = useState(0)

  const refresh = useCallback(() => setRefreshKey((k) => k + 1), [])

  useEffect(() => {
    let cancelled = false

    async function load() {
      setLoading(true)
      setError(null)
      try {
        const res = await fetch(`${API_BASE}/api/news`)
        if (!res.ok) {
          throw new Error(`Server error: ${res.status} ${res.statusText}`)
        }
        const data: NewsItem[] = await res.json()
        if (!cancelled) setItems(data)
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Unknown error')
      } finally {
        if (!cancelled) setLoading(false)
      }
    }

    load()
    return () => {
      cancelled = true
    }
  }, [refreshKey])

  return (
    <div className="app">
      <header className="app-header">
        <h1>Robotics News</h1>
        <p className="app-subtitle">Fresh robotics headlines via .NET API</p>
        <button
          className="refresh-btn"
          onClick={refresh}
          disabled={loading}
          aria-label="Refresh news"
        >
          {loading ? 'Loading…' : 'Refresh'}
        </button>
      </header>

      <main className="app-main">
        {error && (
          <div className="status-error" role="alert">
            <strong>Error:</strong> {error}
          </div>
        )}

        {!loading && !error && items.length === 0 && (
          <p className="status-empty">No articles found.</p>
        )}

        <ul className="news-list">
          {items.map((item) => (
            <li key={item.id}>
              <NewsCard item={item} />
            </li>
          ))}
        </ul>
      </main>
    </div>
  )
}

export default App
