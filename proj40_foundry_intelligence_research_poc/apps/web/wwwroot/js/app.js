'use strict';
// proj40 — Intelligence & Research POC front-end. Vanilla JS, no build step.

const $ = (sel) => document.querySelector(sel);
const $$ = (sel) => Array.from(document.querySelectorAll(sel));
const esc = (s) => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

let currentEmailId = null;
let currentCase = null;

// ----------------------------------------------------------------------------- boot
document.addEventListener('DOMContentLoaded', async () => {
    await loadEngineBadge();
    await loadInbox();
    wireTabs();
    wireCompose();
    wireCases();
    $('#runBtn').addEventListener('click', runForSelected);
});

async function loadEngineBadge() {
    try {
        const h = await fetch('/api/health').then(r => r.json());
        const badge = $('#engineBadge');
        badge.textContent = h.engine === 'foundry' ? 'Foundry (live)' : 'Offline (mock)';
        badge.classList.add(h.engine === 'foundry' ? 'engine-live' : 'engine-offline');
    } catch { /* ignore */ }
}

// ----------------------------------------------------------------------------- inbox
async function loadInbox() {
    const list = $('#inboxList');
    try {
        const emails = await fetch('/api/inbox').then(r => r.json());
        if (!emails.length) { list.innerHTML = '<div class="muted">No messages.</div>'; return; }
        list.innerHTML = emails.map(e => `
            <button class="inbox-item" data-id="${esc(e.id)}">
                <div class="ii-top">
                    <span class="ii-from">${esc(e.fromName || e.from)}</span>
                    <span class="ii-time">${fmtDate(e.receivedUtc)}</span>
                </div>
                <div class="ii-subject">${esc(e.subject)}</div>
                <div class="ii-preview">${esc(e.preview)}</div>
                ${e.hasDocument ? `<div class="ii-doc">📎 ${esc(e.document.docType)} · ${esc(e.document.fileName)} · ${e.document.wordCount} words</div>` : '<div class="ii-doc ii-nodoc">— no document —</div>'}
            </button>`).join('');
        $$('.inbox-item').forEach(el => el.addEventListener('click', () => selectEmail(el.dataset.id)));
    } catch (err) {
        list.innerHTML = `<div class="error">Failed to load inbox: ${esc(err.message)}</div>`;
    }
}

async function selectEmail(id) {
    currentEmailId = id;
    currentCase = null;
    $$('.inbox-item').forEach(el => el.classList.toggle('selected', el.dataset.id === id));
    $('#emptyState').hidden = true;
    $('#readerBody').hidden = false;
    $('#resultWrap').hidden = true;
    $('#runStatus').textContent = '';
    try {
        const e = await fetch(`/api/inbox/${encodeURIComponent(id)}`).then(r => r.json());
        $('#mSubject').textContent = e.subject;
        $('#mMeta').innerHTML = `<strong>${esc(e.fromName)}</strong> &lt;${esc(e.from)}&gt; → ${esc(e.to)} · ${fmtDate(e.receivedUtc)}`;
        $('#mDoc').innerHTML = e.document
            ? `<span class="docchip">📎 ${esc(e.document.docType)}: ${esc(e.document.fileName)} (${e.document.wordCount} words)</span>`
            : '<span class="docchip docchip-none">No document attached</span>';
        $('#mBody').textContent = e.body;
        $('#mDocBody').textContent = e.document ? `\n--- ${e.document.fileName} ---\n${e.document.content}` : '';
    } catch (err) {
        $('#mSubject').textContent = 'Failed to load message';
        $('#mMeta').textContent = err.message;
    }
}

// ----------------------------------------------------------------------------- run pipeline
async function runForSelected() {
    if (!currentEmailId) return;
    await runPipeline(() => fetch(`/api/process/${encodeURIComponent(currentEmailId)}`, { method: 'POST' }), $('#runStatus'), $('#runBtn'));
}

async function runPipeline(fetchFn, statusEl, btnEl) {
    statusEl.textContent = 'Running agents…';
    if (btnEl) btnEl.disabled = true;
    try {
        const res = await fetchFn();
        if (!res.ok) { const e = await res.json().catch(() => ({})); throw new Error(e.error || `HTTP ${res.status}`); }
        currentCase = await res.json();
        renderCase(currentCase);
        statusEl.textContent = `Done · engine: ${currentCase.engine} · case ${currentCase.caseId}`;
    } catch (err) {
        statusEl.textContent = `Error: ${err.message}`;
    } finally {
        if (btnEl) btnEl.disabled = false;
    }
}

// ----------------------------------------------------------------------------- render case → tabs
function renderCase(c) {
    $('#resultWrap').hidden = false;
    renderEntities(c.entities);
    renderInsights(c.insights);
    renderSources(c.sourceHits);
    renderResearch(c.brief);
    renderReport(c.reportEmail, c.caseId);
    renderTrace(c.agentSteps);
    activateTab('entities');
}

function renderEntities(x) {
    const chips = (arr) => (arr && arr.length) ? arr.map(v => `<span class="chip">${esc(v)}</span>`).join('') : '<span class="muted">—</span>';
    $('#tab-entities').innerHTML = `
        <h3>Key entities</h3>
        <div class="kv"><span>Primary org</span><div><strong>${esc(x.primaryOrganisation || '—')}</strong>${x.industry ? ` · ${esc(x.industry)}` : ''}</div></div>
        <div class="kv"><span>Intent</span><div>${esc(x.intent || '—')}</div></div>
        <div class="entgrid">
            <div><h4>People</h4>${chips(x.people)}</div>
            <div><h4>Organisations / systems</h4>${chips(x.organisations)}</div>
            <div><h4>Topics</h4>${chips(x.topics)}</div>
            <div><h4>Technologies</h4>${chips(x.technologies)}</div>
            <div><h4>Locations</h4>${chips(x.locations)}</div>
            <div><h4>Amounts</h4>${chips(x.monetaryAmounts)}</div>
            <div><h4>Dates / timeframes</h4>${chips(x.dates)}</div>
        </div>`;
}

function renderInsights(items) {
    if (!items || !items.length) { $('#tab-insights').innerHTML = '<p class="muted">No insights.</p>'; return; }
    $('#tab-insights').innerHTML = `<h3>Insights from email + document</h3>` + items.map(i => `
        <div class="insight cat-${esc((i.category || '').toLowerCase())}">
            <div class="insight-top"><span class="badge">${esc(i.category)}</span><span class="conf conf-${esc((i.confidence||'').toLowerCase())}">${esc(i.confidence)}</span></div>
            <div class="insight-head">${esc(i.headline)}</div>
            <div class="insight-detail">${esc(i.detail)}</div>
            <div class="insight-ev">⮑ ${esc(i.evidence)}</div>
        </div>`).join('');
}

function renderSources(hits) {
    if (!hits || !hits.length) { $('#tab-sources').innerHTML = '<p class="muted">No source hits matched the extracted entities.</p>'; return; }
    const row = (h, idx) => `
        <div class="source src-${esc(h.sourceType.toLowerCase())}">
            <div class="source-top">
                <span class="marker">[S${idx + 1}]</span>
                <span class="badge ${h.sourceType === 'Internal' ? 'b-internal' : 'b-external'}">${esc(h.sourceType)}</span>
                <span class="rel rel-${esc((h.relevance||'').toLowerCase())}">${esc(h.relevance)}</span>
                <span class="source-name">${esc(h.sourceName)}</span>
                <span class="source-entity">⟵ ${esc(h.entity)}</span>
            </div>
            <div class="source-title">${h.url ? `<a href="${esc(h.url)}" target="_blank" rel="noopener">${esc(h.title)}</a>` : esc(h.title)}</div>
            <div class="source-snippet">${esc(h.snippet)}</div>
            ${h.dated ? `<div class="source-date">${fmtDate(h.dated)}</div>` : ''}
        </div>`;
    $('#tab-sources').innerHTML = `<h3>Pulled sources (keyed by entities)</h3>` + hits.map(row).join('');
}

function renderResearch(b) {
    if (!b) { $('#tab-research').innerHTML = '<p class="muted">No brief.</p>'; return; }
    const ul = (arr) => (arr && arr.length) ? `<ul>${arr.map(x => `<li>${esc(x)}</li>`).join('')}</ul>` : '<p class="muted">—</p>';
    const cites = (b.citations && b.citations.length)
        ? `<div class="cites"><h4>Citations</h4>${b.citations.map(c => `<div class="cite">${esc(c.marker)} ${esc(c.sourceName)} — ${c.url ? `<a href="${esc(c.url)}" target="_blank" rel="noopener">${esc(c.title)}</a>` : esc(c.title)}</div>`).join('')}</div>`
        : '';
    $('#tab-research').innerHTML = `
        <div class="brief-head"><h3>${esc(b.title || 'Research brief')}</h3><span class="conf conf-${esc((b.confidence||'').toLowerCase())}">${esc(b.confidence)} confidence</span></div>
        <p class="exec">${esc(b.executiveSummary)}</p>
        <h4>Key findings</h4>${ul(b.keyFindings)}
        <div class="two-col">
            <div><h4>Risks</h4>${ul(b.risks)}</div>
            <div><h4>Opportunities</h4>${ul(b.opportunities)}</div>
        </div>
        <h4>Recommended actions</h4>${ul(b.recommendedActions)}
        ${b.openQuestions && b.openQuestions.length ? `<h4>Open questions</h4>${ul(b.openQuestions)}` : ''}
        ${cites}`;
}

function renderReport(r, caseId) {
    if (!r) { $('#tab-report').innerHTML = '<p class="muted">No report email.</p>'; return; }
    $('#tab-report').innerHTML = `
        <div class="report-head">
            <h3>Report email</h3>
            <a class="btn-ghost" href="/api/cases/${encodeURIComponent(caseId)}/report" target="_blank">⬇ Download .md</a>
        </div>
        <div class="email-card">
            <div class="email-h"><span>To:</span> ${esc(r.to)}</div>
            ${r.cc ? `<div class="email-h"><span>Cc:</span> ${esc(r.cc)}</div>` : ''}
            <div class="email-h"><span>Subject:</span> <strong>${esc(r.subject)}</strong></div>
            <hr/>
            <div class="email-greet">${esc(r.greeting)}</div>
            <pre class="email-body">${esc(r.body)}</pre>
            <div class="email-sig">${esc(r.signature)}</div>
        </div>`;
}

function renderTrace(steps) {
    if (!steps || !steps.length) { $('#agentTrace').innerHTML = ''; return; }
    $('#agentTrace').innerHTML = `<details><summary>Agent trace (${steps.length} steps)</summary>` +
        steps.map(s => `<div class="trace"><span class="trace-step">${esc(s.step)}</span> ${esc(s.summary)}</div>`).join('') + `</details>`;
}

// ----------------------------------------------------------------------------- tabs
function wireTabs() {
    $$('.tab').forEach(t => t.addEventListener('click', () => activateTab(t.dataset.tab)));
}
function activateTab(name) {
    $$('.tab').forEach(t => t.classList.toggle('active', t.dataset.tab === name));
    $$('.tabpane').forEach(p => p.classList.toggle('active', p.id === `tab-${name}`));
}

// ----------------------------------------------------------------------------- compose modal
function wireCompose() {
    const m = $('#composeModal');
    $('#composeBtn').addEventListener('click', () => { m.hidden = false; });
    $('#composeClose').addEventListener('click', () => { m.hidden = true; });
    $('#composeRun').addEventListener('click', async () => {
        const payload = {
            fromName: $('#cFromName').value, from: $('#cFrom').value, subject: $('#cSubject').value,
            body: $('#cBody').value, documentType: $('#cDocType').value, documentContent: $('#cDocBody').value
        };
        $('#emptyState').hidden = true; $('#readerBody').hidden = false; $('#resultWrap').hidden = true;
        $('#mSubject').textContent = payload.subject || '(ad-hoc)';
        $('#mMeta').innerHTML = `<strong>${esc(payload.fromName || 'Unknown')}</strong> &lt;${esc(payload.from || 'unknown@example.com')}&gt;`;
        $('#mDoc').innerHTML = payload.documentContent ? `<span class="docchip">📎 ${esc(payload.documentType || 'Document')}</span>` : '<span class="docchip docchip-none">No document</span>';
        $('#mBody').textContent = payload.body || '';
        $('#mDocBody').textContent = payload.documentContent ? `\n--- document ---\n${payload.documentContent}` : '';
        m.hidden = true;
        await runPipeline(() => fetch('/api/process', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) }), $('#runStatus'), $('#runBtn'));
    });
}

// ----------------------------------------------------------------------------- cases modal
function wireCases() {
    const m = $('#casesModal');
    $('#casesLink').addEventListener('click', async (e) => {
        e.preventDefault(); m.hidden = false;
        const list = $('#casesList');
        try {
            const cases = await fetch('/api/cases').then(r => r.json());
            list.innerHTML = cases.length ? cases.map(c => `
                <button class="case-item" data-id="${esc(c.caseId)}">
                    <div><strong>${esc(c.org || c.subject || '(case)')}</strong> <span class="muted">${esc(c.engine)}</span></div>
                    <div class="muted">${esc(c.subject)} · ${fmtDate(c.createdUtc)} · ${c.findings} findings</div>
                </button>`).join('') : '<p class="muted">No cases yet.</p>';
            $$('.case-item').forEach(el => el.addEventListener('click', async () => {
                const c = await fetch(`/api/cases/${encodeURIComponent(el.dataset.id)}`).then(r => r.json());
                currentCase = c; m.hidden = true;
                $('#emptyState').hidden = true; $('#readerBody').hidden = false;
                $('#mSubject').textContent = c.email.subject;
                $('#mMeta').innerHTML = `<strong>${esc(c.email.fromName)}</strong> &lt;${esc(c.email.from)}&gt;`;
                $('#mDoc').innerHTML = c.email.document ? `<span class="docchip">📎 ${esc(c.email.document.docType)}</span>` : '';
                $('#mBody').textContent = c.email.body;
                $('#mDocBody').textContent = c.email.document ? `\n--- ${c.email.document.fileName} ---\n${c.email.document.content}` : '';
                renderCase(c);
                $('#runStatus').textContent = `Loaded case ${c.caseId} · engine ${c.engine}`;
            }));
        } catch (err) { list.innerHTML = `<div class="error">${esc(err.message)}</div>`; }
    });
    $('#casesClose').addEventListener('click', () => { m.hidden = true; });
}

// ----------------------------------------------------------------------------- util
function fmtDate(s) {
    if (!s) return '';
    const d = new Date(s);
    if (isNaN(d)) return '';
    return d.toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
}
