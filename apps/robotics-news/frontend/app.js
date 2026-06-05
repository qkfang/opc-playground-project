function byId(id){ return document.getElementById(id); }

const API_BASE_URL_PLACEHOLDER_PREFIX = '__ROBOTICS_NEWS_';
const DEFAULT_NEWS_LIMIT = 20;

const refreshBtn = byId('refreshBtn');
const statusEl = byId('status');
const listEl = byId('list');

const params = new URLSearchParams(window.location.search);
const configuredApiBaseUrl = params.get('apiBaseUrl') || window.ROBOTICS_NEWS_CONFIG?.apiBaseUrl || '';

function normalizeApiBaseUrl(value){
  const normalized = String(value || '').trim().replace(/\/+$/, '');
  return normalized.startsWith(API_BASE_URL_PLACEHOLDER_PREFIX) ? '' : normalized;
}

function setStatus(text, state = 'info'){
  statusEl.textContent = text || '';
  statusEl.dataset.state = state;
}

function formatPublishedAt(value){
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

function render(articles){
  listEl.innerHTML = '';

  if (!articles.length){
    const li = document.createElement('li');
    li.className = 'item';
    li.textContent = 'No robotics news articles are available right now.';
    listEl.appendChild(li);
    return;
  }

  for (const article of articles){
    const li = document.createElement('li');
    li.className = 'item';

    const title = document.createElement('h3');
    const link = document.createElement('a');
    link.href = article.url;
    link.target = '_blank';
    link.rel = 'noreferrer';
    link.textContent = article.title;
    title.appendChild(link);

    const meta = document.createElement('p');
    meta.className = 'meta';
    meta.textContent = `${article.source} • ${formatPublishedAt(article.publishedAt)}`;

    li.append(title, meta);
    listEl.appendChild(li);
  }
}

async function loadNews(){
  const apiBaseUrl = normalizeApiBaseUrl(configuredApiBaseUrl);

  if (!apiBaseUrl){
    render([]);
    setStatus('API base URL is not configured.', 'error');
    return;
  }

  setStatus('Loading latest robotics news…', 'loading');
  refreshBtn.disabled = true;

  try{
    const response = await fetch(`${apiBaseUrl}/api/news?limit=${DEFAULT_NEWS_LIMIT}`, {
      headers: { accept: 'application/json' }
    });

    if (!response.ok){
      throw new Error(`HTTP ${response.status}`);
    }

    const data = await response.json();
    const articles = Array.isArray(data) ? data : [];
    render(articles);
    setStatus(`Loaded ${articles.length} article(s).`, 'success');
  }catch(error){
    console.error(error);
    render([]);
    setStatus(`Unable to load robotics news: ${error.message || error}`, 'error');
  }finally{
    refreshBtn.disabled = false;
  }
}

refreshBtn.addEventListener('click', loadNews);
loadNews();
