function byId(id){ return document.getElementById(id); }

const apiInput = byId('apiBaseUrl');
const loadBtn = byId('loadBtn');
const statusEl = byId('status');
const listEl = byId('list');

// Allow ?apiBaseUrl=... to prefill (useful for SWA deployments)
const params = new URLSearchParams(location.search);
const apiBaseFromQuery = params.get('apiBaseUrl');
if (apiBaseFromQuery) apiInput.value = apiBaseFromQuery;

function setStatus(text){ statusEl.textContent = text || ''; }

function render(articles){
  listEl.innerHTML = '';

  if (!articles.length){
    const li = document.createElement('li');
    li.className = 'item';
    li.textContent = 'No articles found.';
    listEl.appendChild(li);
    return;
  }

  for (const a of articles){
    const li = document.createElement('li');
    li.className = 'item';

    const h3 = document.createElement('h3');
    const link = document.createElement('a');
    link.href = a.url;
    link.target = '_blank';
    link.rel = 'noreferrer';
    link.textContent = a.title;
    h3.appendChild(link);

    const meta = document.createElement('div');
    meta.className = 'meta';
    const date = new Date(a.publishedAt);
    meta.textContent = `${a.source} • ${isNaN(date.getTime()) ? a.publishedAt : date.toLocaleString()}`;

    const summary = document.createElement('div');
    summary.textContent = a.summary || '';

    const tags = document.createElement('div');
    tags.className = 'tags';
    for (const t of (a.tags || [])){
      const span = document.createElement('span');
      span.className = 'tag';
      span.textContent = t;
      tags.appendChild(span);
    }

    li.append(h3, meta, summary, tags);
    listEl.appendChild(li);
  }
}

async function loadNews(){
  const base = (apiInput.value || '').trim().replace(/\/$/, '');
  if (!base){
    setStatus('Enter an API Base URL (or use ?apiBaseUrl=...)');
    return;
  }

  setStatus('Loading…');
  loadBtn.disabled = true;

  try{
    const res = await fetch(`${base}/api/news`, { headers: { 'accept': 'application/json' }});
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const data = await res.json();
    render(Array.isArray(data) ? data : []);
    setStatus(`Loaded ${Array.isArray(data) ? data.length : 0} article(s).`);
  }catch(err){
    console.error(err);
    setStatus(`Error loading news: ${err.message || err}`);
  }finally{
    loadBtn.disabled = false;
  }
}

loadBtn.addEventListener('click', loadNews);
