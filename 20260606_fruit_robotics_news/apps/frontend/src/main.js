import './style.css';

const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000').replace(/\/$/, '');

document.querySelector('#app').innerHTML = `
  <main class="page">
    <header class="header">
      <h1>🍓 Fruit Robotics News</h1>
      <p>Fresh robotics headlines served from our .NET orchard API.</p>
      <button id="refresh-btn" type="button">Refresh news</button>
      <p class="status" id="status">Ready.</p>
    </header>
    <section>
      <ul id="news-list" class="news-list"></ul>
    </section>
  </main>
`;

const refreshButton = document.querySelector('#refresh-btn');
const status = document.querySelector('#status');
const newsList = document.querySelector('#news-list');

refreshButton.addEventListener('click', () => {
  loadNews();
});

async function loadNews() {
  status.textContent = 'Loading robotics headlines...';
  refreshButton.disabled = true;

  try {
    const response = await fetch(`${API_BASE_URL}/api/news/robotics?count=10`);

    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }

    const items = await response.json();
    renderNews(items);
    status.textContent = `Loaded ${items.length} items.`;
  } catch (error) {
    newsList.innerHTML = '';
    status.textContent = `Error loading news: ${error.message}`;
  } finally {
    refreshButton.disabled = false;
  }
}

function renderNews(items) {
  if (!Array.isArray(items) || items.length === 0) {
    newsList.innerHTML = '<li class="news-item">No robotics stories available right now.</li>';
    return;
  }

  newsList.innerHTML = items
    .map(item => {
      const published = item.published
        ? new Date(item.published).toLocaleString()
        : 'Unknown publish date';

      return `
        <li class="news-item">
          <a href="${item.url}" target="_blank" rel="noopener noreferrer">${escapeHtml(item.title)}</a>
          <p class="meta">${escapeHtml(published)}</p>
          <p>${escapeHtml(item.summary || 'No summary available.')}</p>
        </li>
      `;
    })
    .join('');
}

function escapeHtml(value) {
  return String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}

loadNews();
