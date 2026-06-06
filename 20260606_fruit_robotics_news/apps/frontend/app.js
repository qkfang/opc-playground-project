(function () {
  const refreshButton = document.getElementById('refresh-button');
  const statusElement = document.getElementById('status');
  const newsListElement = document.getElementById('news-list');
  const newsCount = 10;

  const config = window.FRUIT_ROBOTICS_CONFIG || {};
  const apiBaseUrl = typeof config.apiBaseUrl === 'string'
    ? config.apiBaseUrl.replace(/\/+$/, '')
    : '';
  const requestUrl = `${apiBaseUrl}/api/news/robotics?count=${newsCount}`;

  function setStatus(message, variant) {
    statusElement.textContent = message;
    statusElement.className = `status is-visible is-${variant}`;
  }

  function clearStatus() {
    statusElement.textContent = '';
    statusElement.className = 'status';
  }

  function clearNewsList() {
    newsListElement.replaceChildren();
  }

  function formatDate(value) {
    if (!value) {
      return 'Published date unavailable';
    }

    const parsed = new Date(value);
    if (Number.isNaN(parsed.getTime())) {
      return String(value);
    }

    return parsed.toLocaleDateString(undefined, {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
  }

  function normalizeItems(payload) {
    if (Array.isArray(payload)) {
      return payload.filter(Boolean);
    }

    if (payload && Array.isArray(payload.items)) {
      return payload.items.filter(Boolean);
    }

    return [];
  }

  function getSafeUrl(value) {
    if (typeof value !== 'string' || value.trim() === '') {
      return '#';
    }

    if (!/^https?:\/\//i.test(value)) {
      return '#';
    }

    try {
      const parsed = new URL(value);
      if (parsed.protocol === 'http:' || parsed.protocol === 'https:') {
        return parsed.href;
      }
    } catch {
      return '#';
    }

    return '#';
  }

  function createNewsCard(item) {
    const article = document.createElement('article');
    article.className = 'news-card';

    const title = document.createElement('h2');
    const link = document.createElement('a');
    const href = getSafeUrl(item.url || item.link);
    link.href = href;
    link.target = '_blank';
    link.rel = 'noreferrer noopener';
    link.textContent = item.title || 'Untitled story';
    title.appendChild(link);

    const meta = document.createElement('p');
    meta.className = 'news-meta';
    meta.textContent = formatDate(item.published || item.publishedAt || item.date);

    const summary = document.createElement('p');
    summary.className = 'news-summary';
    summary.textContent = item.summary || item.description || 'No summary available.';

    article.append(title, meta, summary);
    return article;
  }

  async function loadNews() {
    refreshButton.disabled = true;
    setStatus('Loading robotics news…', 'loading');

    try {
      const response = await fetch(requestUrl, {
        headers: {
          Accept: 'application/json',
        },
      });

      if (!response.ok) {
        throw new Error(`Request failed with status ${response.status}`);
      }

      const payload = await response.json();
      const items = normalizeItems(payload).slice(0, newsCount);

      clearNewsList();

      if (items.length === 0) {
        setStatus('No robotics stories are available right now. Please try again later.', 'empty');
        return;
      }

      clearStatus();
      newsListElement.append(...items.map(createNewsCard));
    } catch (error) {
      clearNewsList();
      setStatus(
        `Unable to load robotics news. ${error instanceof Error ? error.message : 'Unexpected error.'}`,
        'error',
      );
    } finally {
      refreshButton.disabled = false;
    }
  }

  refreshButton.addEventListener('click', loadNews);
  loadNews();
}());
