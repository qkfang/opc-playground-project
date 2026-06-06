function el(tag, attrs = {}, children = []) {
  const node = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) {
    if (k === 'class') node.className = v;
    else if (k === 'text') node.textContent = v;
    else node.setAttribute(k, v);
  }
  for (const child of children) node.appendChild(child);
  return node;
}

function fmtDate(iso) {
  try {
    const d = new Date(iso);
    return d.toLocaleString();
  } catch {
    return iso;
  }
}

function getApiBase() {
  const fromStorage = localStorage.getItem('API_BASE_URL');
  return fromStorage || '';
}

function setApiBase(v) {
  localStorage.setItem('API_BASE_URL', v);
}

async function loadNews() {
  const apiBaseInput = document.getElementById('apiBase');
  const status = document.getElementById('status');
  const list = document.getElementById('list');
  const btn = document.getElementById('refreshBtn');

  const base = (apiBaseInput.value || '').trim().replace(/\/$/, '');
  if (!base) {
    status.textContent = 'Enter your API base URL to fetch news.';
    return;
  }

  setApiBase(base);

  btn.disabled = true;
  status.textContent = 'Fetching…';
  list.innerHTML = '';

  try {
    const resp = await fetch(`${base}/api/news?limit=30`);
    if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
    const data = await resp.json();

    status.textContent = `Updated ${fmtDate(data.generatedAt)} • ${data.items.length} items`;

    for (const item of data.items) {
      const card = el('div', { class: 'card' }, [
        el('div', {}, [
          el('a', { href: item.url, target: '_blank', rel: 'noreferrer', text: item.title })
        ]),
        item.summary ? el('p', { text: item.summary }) : el('div'),
        el('div', { class: 'meta' }, [
          el('span', { class: 'badge', text: item.source }),
          el('span', { text: fmtDate(item.publishedAt) })
        ])
      ]);
      list.appendChild(card);
    }
  } catch (err) {
    status.textContent = `Error: ${err.message}. Check the API URL + CORS.`;
  } finally {
    btn.disabled = false;
  }
}

function init() {
  const apiBaseInput = document.getElementById('apiBase');
  apiBaseInput.value = getApiBase();

  document.getElementById('refreshBtn').addEventListener('click', loadNews);

  if (apiBaseInput.value) {
    loadNews();
  }
}

init();
