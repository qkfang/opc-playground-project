import { useState, useCallback } from 'react'
import './App.css'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || ''

function NewsItem({ item }) {
  return (
    <article className="news-item">
      <h2 className="news-title">
        <a href={item.url} target="_blank" rel="noopener noreferrer">
          {item.title}
        </a>
      </h2>
      <div className="news-meta">
        {item.source && <span className="news-source">🍊 {item.source}</span>}
        {item.published && (
          <span className="news-date">
            {new Date(item.published).toLocaleDateString(undefined, {
              year: 'numeric',
              month: 'short',
              day: 'numeric',
            })}
          </span>
        )}
      </div>
      {item.summary && <p className="news-summary">{item.summary}</p>}
    </article>
  )
}

function App() {
  const [items, setItems] = useState([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)
  const [loaded, setLoaded] = useState(false)

  const fetchNews = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const res = await fetch(`${API_BASE_URL}/api/news`)
      if (!res.ok) {
        throw new Error(`Server returned ${res.status}`)
      }
      const data = await res.json()
      setItems(data.items ?? [])
      setLoaded(true)
    } catch (err) {
      setError(err.message || 'Failed to load news')
    } finally {
      setLoading(false)
    }
  }, [])

  return (
    <div className="app">
      <header className="app-header">
        <div className="header-brand">
          <span className="header-emoji">🍓</span>
          <span className="header-title">Fruit Robotics News</span>
          <span className="header-emoji">🤖</span>
        </div>
        <p className="header-subtitle">Fresh robotics headlines, delivered daily</p>
      </header>

      <main className="app-main">
        <div className="controls">
          <button
            className="refresh-btn"
            onClick={fetchNews}
            disabled={loading}
          >
            {loading ? '⏳ Loading…' : '🍋 Refresh News'}
          </button>
        </div>

        {error && (
          <div className="error-banner" role="alert">
            <span>🍅 Oops! {error}</span>
            <button className="retry-btn" onClick={fetchNews}>Retry</button>
          </div>
        )}

        {!loaded && !loading && !error && (
          <div className="empty-state">
            <span className="empty-emoji">🍇</span>
            <p>Click <strong>Refresh News</strong> to load the latest robotics headlines.</p>
          </div>
        )}

        {loading && (
          <div className="loading-state" aria-live="polite">
            <span className="loading-spinner">🍊</span>
            <p>Fetching fresh news…</p>
          </div>
        )}

        {!loading && loaded && items.length === 0 && (
          <div className="empty-state">
            <span className="empty-emoji">🍍</span>
            <p>No news items found. Try again later.</p>
          </div>
        )}

        {!loading && items.length > 0 && (
          <section className="news-list" aria-label="Robotics news">
            {items.map((item, idx) => (
              <NewsItem key={item.url ?? idx} item={item} />
            ))}
          </section>
        )}
      </main>

      <footer className="app-footer">
        <p>🍒 Powered by Fruit Robotics News &mdash; {new Date().getFullYear()}</p>
      </footer>
    </div>
  )
}

export default App
