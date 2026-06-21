'use strict';

const $ = (sel, root = document) => root.querySelector(sel);
const $all = (sel, root = document) => Array.from(root.querySelectorAll(sel));
const fmtMoney = (n, ccy) => {
  const sym = { USD: '$', AUD: 'A$', EUR: '€', GBP: '£' }[ccy] || (ccy + ' ');
  return sym + Number(n || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
};
const esc = (s) => String(s ?? '').replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

// ---------------------------------------------------------------- store
const STORE_KEY = 'proj37.currentJob';
const Store = {
  get() { try { return JSON.parse(localStorage.getItem(STORE_KEY) || 'null'); } catch { return null; } },
  set(job) { try { localStorage.setItem(STORE_KEY, JSON.stringify(job)); } catch { /* ignore quota */ } },
};

let AGENT_INSTRUCTIONS = null;

// ---------------------------------------------------------------- bootstrap
document.addEventListener('DOMContentLoaded', () => {
  loadHealth();
  wireModal();
  wireAgentStepButtons();
  const page = document.body.dataset.page || '';
  if (page === 'upload') initUpload();
  else if (page === 'scope') initScope();
  else if (page === 'requirements') initRequirements();
  else if (page === 'cost') initCost();
  else if (page === 'steps') initSteps();
  else if (page === 'estimations') initEstimations();
});

async function loadHealth() {
  try {
    const r = await fetch('/api/health');
    const h = await r.json();
    const badge = $('#engineBadge');
    if (!badge) return;
    badge.textContent = 'engine: ' + h.engine;
    badge.className = 'badge ' + (h.engine === 'foundry' ? 'foundry' : 'offline');
  } catch { /* ignore */ }
}

// ================================================================ UPLOAD page
let selectedFiles = [];

function initUpload() {
  wireUpload();
  loadSamples();
  // If a job already exists, surface the "ready" shortcut.
  const job = Store.get();
  if (job && job.scope) showDone(job);
}

function wireUpload() {
  const input = $('#fileInput');
  const dz = $('#dropzone');
  input.addEventListener('change', () => { selectedFiles = Array.from(input.files); renderFileList(); });
  ['dragover', 'dragenter'].forEach(ev => dz.addEventListener(ev, (e) => { e.preventDefault(); dz.classList.add('drag'); }));
  ['dragleave', 'drop'].forEach(ev => dz.addEventListener(ev, (e) => { e.preventDefault(); dz.classList.remove('drag'); }));
  dz.addEventListener('drop', (e) => { selectedFiles = Array.from(e.dataTransfer.files); renderFileList(); });

  $('#uploadForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    if (selectedFiles.length === 0) { setStatus('Please choose at least one document, or run an example brief below.', 'error'); return; }
    const fd = new FormData();
    selectedFiles.forEach(f => fd.append('files', f));
    await runEstimation(() => fetch('/api/estimations', { method: 'POST', body: fd }));
  });
}

function renderFileList() {
  const list = $('#fileList');
  if (selectedFiles.length === 0) { list.innerHTML = ''; $('#dropLabel').textContent = 'Click to choose files or drag & drop'; return; }
  $('#dropLabel').textContent = selectedFiles.length + ' file(s) selected';
  list.innerHTML = selectedFiles.map(f =>
    `<div class="file-chip"><span>📄 ${esc(f.name)}</span><span class="muted">${(f.size / 1024).toFixed(1)} KB</span></div>`).join('');
}

async function loadSamples() {
  const el = $('#sampleList');
  if (!el) return;
  try {
    const r = await fetch('/api/samples');
    const items = await r.json();
    if (!items.length) { el.innerHTML = '<p class="muted">No example documents available.</p>'; return; }
    el.innerHTML = items.map(s => `
      <div class="sample-item">
        <div class="sample-meta">
          <span class="sample-title">📑 ${esc(s.title)}</span>
          <span class="muted sample-file">${esc(s.fileName)} · ${(s.sizeBytes / 1024).toFixed(1)} KB</span>
        </div>
        <div class="sample-actions">
          <button type="button" class="btn btn-secondary btn-sm" data-view-sample="${esc(s.id)}" data-title="${esc(s.title)}">View</button>
          <button type="button" class="btn btn-primary btn-sm" data-use-sample="${esc(s.id)}">Use this</button>
        </div>
      </div>`).join('');
    $all('[data-view-sample]', el).forEach(b => b.addEventListener('click', () => viewSample(b.dataset.viewSample, b.dataset.title)));
    $all('[data-use-sample]', el).forEach(b => b.addEventListener('click', () => useSample(b.dataset.useSample)));
  } catch {
    el.innerHTML = '<p class="muted">Could not load example documents.</p>';
  }
}

async function viewSample(id, title) {
  openModal(title || 'Example brief', '<p class="muted">Loading…</p>');
  try {
    // Prefer server-rendered HTML (Markdig) so the popup shows nicely formatted content, not raw markdown.
    const r = await fetch('/api/samples/' + encodeURIComponent(id) + '/html');
    if (r.ok) {
      const html = await r.text();
      setModalBody(`<div class="doc-html">${html}</div>`);
      return;
    }
    // Fallback: raw markdown as preformatted text.
    const raw = await fetch('/api/samples/' + encodeURIComponent(id));
    if (!raw.ok) { setModalBody('<p class="muted">Could not load this document.</p>'); return; }
    const md = await raw.text();
    setModalBody(`<pre class="doc-md">${esc(md)}</pre>`);
  } catch {
    setModalBody('<p class="muted">Could not load this document.</p>');
  }
}

async function useSample(id) {
  await runEstimation(() => fetch('/api/estimations/sample?id=' + encodeURIComponent(id), { method: 'POST' }));
}

async function runEstimation(call) {
  setBusy(true);
  setStatus('Ingesting documents and running the estimation pipeline…', 'busy');
  try {
    const r = await call();
    const job = await r.json();
    if (!r.ok && job.status !== 'completed') {
      setStatus('Estimation failed: ' + (job.error || r.statusText), 'error');
      return;
    }
    setStatus('Estimation complete.', 'info');
    Store.set(job);
    showDone(job);
  } catch (err) {
    setStatus('Request error: ' + err.message, 'error');
  } finally {
    setBusy(false);
  }
}

function showDone(job) {
  const card = $('#doneCard');
  if (!card) return;
  card.hidden = false;
  const c = job.cost || {};
  $('#doneSummary').textContent =
    `${job.scope?.projectName || 'Estimation'} · engine: ${job.engine} · ${job.requirements?.length || 0} requirements · `
    + `${fmtMoney(c.monthlyTotalWithContingency, c.currency)}/mo (incl. ${c.contingencyPercent || 0}% contingency).`;
  const dl = $('#doneDownload');
  dl.href = `/api/estimations/${job.jobId}/workbook`;
  dl.setAttribute('download', '');
  card.scrollIntoView({ behavior: 'smooth' });
}

function setStatus(msg, kind) { const s = $('#status'); if (!s) return; s.hidden = false; s.textContent = msg; s.className = 'status ' + kind; }
function setBusy(b) { const e = $('#estimateBtn'); if (e) e.disabled = b; $all('[data-use-sample]').forEach(x => x.disabled = b); }

// ================================================================ PLATFORM pages
function platformContext(job) {
  const line = $('#ctxLine');
  if (!line) return;
  if (!job || !job.scope) { line.textContent = 'No estimation loaded yet.'; return; }
  const c = job.cost || {};
  line.innerHTML = `<strong>${esc(job.scope.projectName || 'Estimation')}</strong> · engine: ${esc(job.engine)} · `
    + `${fmtMoney(c.monthlyTotalWithContingency, c.currency)}/mo · <span class="muted">job ${esc(job.jobId)}</span>`;
}

function showOrEmpty(job, cardSel) {
  const empty = $('#emptyState');
  const card = $(cardSel);
  if (!job || !job.scope) { if (empty) empty.hidden = false; if (card) card.hidden = true; return false; }
  if (empty) empty.hidden = true;
  if (card) card.hidden = false;
  return true;
}

function initScope() {
  const job = Store.get();
  platformContext(job);
  if (!showOrEmpty(job, '#scopeCard')) return;
  renderScope(job.scope || {});
}

function initRequirements() {
  const job = Store.get();
  platformContext(job);
  if (!showOrEmpty(job, '#reqCard')) return;
  renderRequirements(job.requirements || []);
}

function initCost() {
  const job = Store.get();
  platformContext(job);
  if (!showOrEmpty(job, '#costCard')) return;
  const dl = $('#downloadBtn');
  if (dl) { dl.hidden = false; dl.href = `/api/estimations/${job.jobId}/workbook`; dl.setAttribute('download', ''); }
  renderCost(job.cost || {});
}

function initSteps() {
  const job = Store.get();
  platformContext(job);
  renderStepCards();
  if (!showOrEmpty(job, '#stepsCard')) return;
  renderSteps(job.agentSteps || []);
}

function renderScope(s) {
  const ul = (arr) => (arr && arr.length) ? '<ul class="tight">' + arr.map(x => `<li>${esc(x)}</li>`).join('') + '</ul>' : '<span class="muted">—</span>';
  $('#tab-scope').innerHTML = `
    <dl class="kv">
      <dt>Overview</dt><dd>${esc(s.overview)}</dd>
      <dt>Business goal</dt><dd>${esc(s.businessGoal)}</dd>
      <dt>Workload profile</dt><dd>${esc(s.workloadProfile)}</dd>
      <dt>Expected scale</dt><dd>${esc(s.expectedScale)}</dd>
      <dt>Data sensitivity</dt><dd>${esc(s.dataSensitivity)}</dd>
      <dt>Environment</dt><dd>${esc(s.environment)}</dd>
      <dt>In scope</dt><dd>${ul(s.inScope)}</dd>
      <dt>Out of scope</dt><dd>${ul(s.outOfScope)}</dd>
      <dt>Assumptions</dt><dd>${ul(s.assumptions)}</dd>
    </dl>`;
}

function renderRequirements(reqs) {
  if (!reqs.length) { $('#tab-requirements').innerHTML = '<p class="muted">No requirements.</p>'; return; }
  $('#tab-requirements').innerHTML = `
    <table><thead><tr><th>ID</th><th>Category</th><th>Priority</th><th>Requirement</th><th>Rationale</th></tr></thead>
    <tbody>${reqs.map(q => `<tr>
      <td>${esc(q.id)}</td><td>${esc(q.category)}</td>
      <td><span class="pill ${esc(q.priority)}">${esc(q.priority)}</span></td>
      <td>${esc(q.requirement)}</td><td class="muted">${esc(q.rationale)}</td></tr>`).join('')}</tbody></table>`;
}

// Editable cost model with non-prod / prod / total environment views. Qty cells are inputs; editing
// recalculates Monthly + totals live. Pricing reference links make each line auditable.
let COST_STATE = null;
let COST_ENV = 'total';   // 'nonprod' | 'prod' | 'total'

const ENV_LABEL = { nonprod: 'Non-Prod', prod: 'Prod', total: 'Total' };
const ENV_NOTE = {
  nonprod: 'Non-production (dev/test/POC) footprint — a scaled-down version of the same architecture.',
  prod: 'Production footprint — full sizing for the live workload.',
  total: 'Total cost of ownership — Non-Prod + Prod across all environments.'
};

function ensureLineDefaults(i) {
  // Backfill env fields for older stored jobs that predate the non-prod model.
  if (i.nonProdQuantity === undefined || i.nonProdQuantity === null) {
    i.nonProdQuantity = Math.round(Number(i.quantity || 0) * 0.4 * 10000) / 10000;
  }
  i.prodMonthlyCost = Math.round(Number(i.quantity || 0) * Number(i.unitPrice || 0) * 100) / 100;
  i.nonProdMonthlyCost = Math.round(Number(i.nonProdQuantity || 0) * Number(i.unitPrice || 0) * 100) / 100;
  i.totalMonthlyCost = Math.round((i.prodMonthlyCost + i.nonProdMonthlyCost) * 100) / 100;
}

function priceRefLink(i) {
  if (!i.pricingReferenceUrl) return '<span class="muted">—</span>';
  const label = i.pricingReferenceLabel || 'Azure pricing';
  return `<a class="price-ref" href="${esc(i.pricingReferenceUrl)}" target="_blank" rel="noopener noreferrer" title="${esc(i.pricingReferenceUrl)}">${esc(label)} ↗</a>`;
}

function renderCost(c) {
  COST_STATE = c;
  (c.lineItems || []).forEach(ensureLineDefaults);
  wireEnvToggle();
  renderCostTable();
}

function wireEnvToggle() {
  const toggle = $('#envToggle');
  if (!toggle || toggle.dataset.wired) return;
  $all('.env-btn', toggle).forEach(btn => btn.addEventListener('click', () => {
    COST_ENV = btn.dataset.env;
    $all('.env-btn', toggle).forEach(b => {
      const on = b === btn;
      b.classList.toggle('active', on);
      b.setAttribute('aria-selected', on ? 'true' : 'false');
    });
    renderCostTable();
  }));
  toggle.dataset.wired = '1';
}

function renderCostTable() {
  const c = COST_STATE;
  if (!c) return;
  const items = c.lineItems || [];
  const note = $('#envNote'); if (note) note.textContent = ENV_NOTE[COST_ENV] || '';
  if (!items.length) { $('#tab-cost').innerHTML = '<p class="muted">No cost items.</p>'; const t = $('#totals'); if (t) t.innerHTML = ''; return; }
  renderTotals(c);

  const head = (COST_ENV === 'total')
    ? `<tr><th>Category</th><th>Service</th><th>SKU</th><th>Pricing ref</th>
         <th class="num-col">Non-Prod Qty</th><th class="num-col">Prod Qty</th>
         <th class="num-col">Non-Prod</th><th class="num-col">Prod</th><th class="num-col">Total</th></tr>`
    : `<tr><th>Category</th><th>Service</th><th>SKU</th><th>Assumption</th><th>Pricing ref</th>
         <th class="num-col">Qty</th><th class="num-col">Unit price</th><th class="num-col">Monthly</th></tr>`;

  const rows = items.map((i, idx) => {
    if (COST_ENV === 'total') {
      return `<tr>
        <td>${esc(i.category)}</td><td>${esc(i.service)}</td><td>${esc(i.sku)}</td>
        <td>${priceRefLink(i)}</td>
        <td class="num-col"><input class="qty-input" type="number" min="0" step="any" data-row="${idx}" data-field="nonprod" value="${Number(i.nonProdQuantity)}" aria-label="Non-prod quantity for ${esc(i.service)}" /></td>
        <td class="num-col"><input class="qty-input" type="number" min="0" step="any" data-row="${idx}" data-field="prod" value="${Number(i.quantity)}" aria-label="Prod quantity for ${esc(i.service)}" /></td>
        <td class="num-col" data-np="${idx}">${fmtMoney(i.nonProdMonthlyCost, c.currency)}</td>
        <td class="num-col" data-pr="${idx}">${fmtMoney(i.prodMonthlyCost, c.currency)}</td>
        <td class="num-col" data-tot="${idx}"><strong>${fmtMoney(i.totalMonthlyCost, c.currency)}</strong></td></tr>`;
    }
    const qty = COST_ENV === 'nonprod' ? Number(i.nonProdQuantity) : Number(i.quantity);
    const monthly = COST_ENV === 'nonprod' ? i.nonProdMonthlyCost : i.prodMonthlyCost;
    const field = COST_ENV === 'nonprod' ? 'nonprod' : 'prod';
    return `<tr>
      <td>${esc(i.category)}</td><td>${esc(i.service)}</td><td>${esc(i.sku)}</td>
      <td class="muted">${esc(i.assumption)}</td>
      <td>${priceRefLink(i)}</td>
      <td class="num-col"><input class="qty-input" type="number" min="0" step="any" data-row="${idx}" data-field="${field}" value="${qty}" aria-label="Quantity for ${esc(i.service)}" /></td>
      <td class="num-col">${fmtMoney(i.unitPrice, c.currency)}</td>
      <td class="num-col" data-monthly="${idx}">${fmtMoney(monthly, c.currency)}</td></tr>`;
  }).join('');

  const footColspan = COST_ENV === 'total' ? 8 : 7;
  const footTotal = envTotalWithContingency(c);
  $('#tab-cost').innerHTML = `
    <table><thead>${head}</thead>
    <tbody>${rows}</tbody>
    <tfoot><tr><th colspan="${footColspan}" class="num-col">${ENV_LABEL[COST_ENV]} monthly total (incl. <span id="contPct">${c.contingencyPercent}</span>% contingency)</th>
      <th class="num-col" id="costFootTotal">${fmtMoney(footTotal, c.currency)}</th></tr></tfoot></table>
    <p class="muted" style="margin-top:.7rem">${(c.notes || []).map(esc).join(' · ')}</p>`;
  $all('.qty-input').forEach(inp => inp.addEventListener('input', onQtyEdit));
}

function envRawTotal(c) {
  const items = c.lineItems || [];
  if (COST_ENV === 'nonprod') return items.reduce((s, i) => s + Number(i.nonProdMonthlyCost || 0), 0);
  if (COST_ENV === 'prod') return items.reduce((s, i) => s + Number(i.prodMonthlyCost || 0), 0);
  return items.reduce((s, i) => s + Number(i.totalMonthlyCost || 0), 0);
}

function envTotalWithContingency(c) {
  const raw = envRawTotal(c);
  const pct = Number(c.contingencyPercent || 0);
  return Math.round(raw * (1 + pct / 100) * 100) / 100;
}

function onQtyEdit(e) {
  const idx = Number(e.target.dataset.row);
  const field = e.target.dataset.field;
  const c = COST_STATE;
  if (!c || !c.lineItems[idx]) return;
  const val = Number(e.target.value);
  const item = c.lineItems[idx];
  const safe = isFinite(val) && val >= 0 ? val : 0;
  if (field === 'nonprod') item.nonProdQuantity = safe; else item.quantity = safe;
  ensureLineDefaults(item);
  // Keep legacy field in sync for any code/store that still reads monthlyCost (= prod).
  item.monthlyCost = item.prodMonthlyCost;
  const npCell = $(`[data-np="${idx}"]`); if (npCell) npCell.textContent = fmtMoney(item.nonProdMonthlyCost, c.currency);
  const prCell = $(`[data-pr="${idx}"]`); if (prCell) prCell.innerHTML = fmtMoney(item.prodMonthlyCost, c.currency);
  const totCell = $(`[data-tot="${idx}"]`); if (totCell) totCell.innerHTML = `<strong>${fmtMoney(item.totalMonthlyCost, c.currency)}</strong>`;
  const mCell = $(`[data-monthly="${idx}"]`);
  if (mCell) mCell.textContent = fmtMoney(COST_ENV === 'nonprod' ? item.nonProdMonthlyCost : item.prodMonthlyCost, c.currency);
  recomputeTotals(c);
}

function recomputeTotals(c) {
  // Maintain prod headline fields (used elsewhere: context line, history, download summary).
  const prodRaw = (c.lineItems || []).reduce((sum, i) => sum + Number(i.prodMonthlyCost || 0), 0);
  const pct = Number(c.contingencyPercent || 0);
  c.monthlyTotal = Math.round(prodRaw * 100) / 100;
  c.monthlyTotalWithContingency = Math.round(prodRaw * (1 + pct / 100) * 100) / 100;
  c.annualTotal = Math.round(prodRaw * 12 * 100) / 100;
  const foot = $('#costFootTotal'); if (foot) foot.textContent = fmtMoney(envTotalWithContingency(c), c.currency);
  renderTotals(c);
}

function renderTotals(c) {
  const el = $('#totals');
  if (!el) return;
  const items = c.lineItems || [];
  const pct = Number(c.contingencyPercent || 0);
  const npRaw = items.reduce((s, i) => s + Number(i.nonProdMonthlyCost || 0), 0);
  const prRaw = items.reduce((s, i) => s + Number(i.prodMonthlyCost || 0), 0);
  const totRaw = Math.round((npRaw + prRaw) * 100) / 100;
  const withCont = (n) => Math.round(n * (1 + pct / 100) * 100) / 100;
  // Highlight the box for the currently selected environment view.
  const hi = (env) => COST_ENV === env ? ' hi' : '';
  el.innerHTML = `
    <div class="total-box${hi('nonprod')}"><div class="num">${fmtMoney(withCont(npRaw), c.currency)}</div><div class="lbl">Non-Prod / mo</div></div>
    <div class="total-box${hi('prod')}"><div class="num">${fmtMoney(withCont(prRaw), c.currency)}</div><div class="lbl">Prod / mo</div></div>
    <div class="total-box${hi('total')}"><div class="num">${fmtMoney(withCont(totRaw), c.currency)}</div><div class="lbl">Total / mo</div></div>
    <div class="total-box"><div class="num">${fmtMoney(withCont(totRaw) * 12, c.currency)}</div><div class="lbl">Total / yr</div></div>
    <div class="total-box"><div class="num">${pct}%</div><div class="lbl">Contingency</div></div>`;
}

function renderSteps(steps) {
  if (!steps.length) { $('#tab-steps').innerHTML = '<p class="muted">No steps recorded.</p>'; return; }
  $('#tab-steps').innerHTML = '<ul class="tight">' + steps.map(s =>
    `<li><strong>${esc(s.step)}:</strong> ${esc(s.summary)}</li>`).join('') + '</ul>';
}

// Step instruction cards on the Agent Steps page.
async function renderStepCards() {
  const host = $('#stepCards');
  if (!host) return;
  const data = await getAgentInstructions();
  if (!data) { host.innerHTML = '<p class="muted">Agent instructions unavailable.</p>'; return; }
  host.innerHTML = data.steps.map(s => `
    <div class="step-card">
      <div class="step-card-head"><span class="step-badge">${esc(s.title)}</span><span class="muted">${esc(s.agent)}</span></div>
      <p>${esc(s.goal)}</p>
      <button type="button" class="btn btn-secondary btn-sm" data-agent-step="${esc(s.key)}">View instructions</button>
    </div>`).join('');
  $all('[data-agent-step]', host).forEach(b => b.addEventListener('click', () => showAgentStep(b.dataset.agentStep)));
}

// ================================================================ ESTIMATIONS page
function initEstimations() { loadHistory(); }

async function loadHistory() {
  const el = $('#history');
  if (!el) return;
  try {
    const r = await fetch('/api/estimations');
    const items = await r.json();
    if (!items.length) { el.innerHTML = '<p class="muted">No estimations yet. <a href="/">Run one →</a></p>'; return; }
    el.innerHTML = `<table><thead><tr><th>Project</th><th>Engine</th><th>Docs</th><th>Reqs</th>
      <th class="num-col">Monthly</th><th>Created</th><th></th><th></th></tr></thead><tbody>${items.map(i => `<tr>
      <td>${esc(i.project)}</td><td><span class="pill Could">${esc(i.engine)}</span></td>
      <td>${i.documents}</td><td>${i.requirements}</td>
      <td class="num-col">${fmtMoney(i.monthlyTotal, i.currency)}</td>
      <td class="muted">${new Date(i.createdUtc).toLocaleString()}</td>
      <td><button type="button" class="btn btn-secondary btn-sm" data-load="${esc(i.jobId)}">Open</button></td>
      <td><a href="/api/estimations/${esc(i.jobId)}/workbook">⬇ xlsx</a></td></tr>`).join('')}</tbody></table>`;
    $all('[data-load]', el).forEach(b => b.addEventListener('click', () => loadJobIntoPlatform(b.dataset.load)));
  } catch {
    el.innerHTML = '<p class="muted">Could not load history.</p>';
  }
}

async function loadJobIntoPlatform(jobId) {
  try {
    const r = await fetch('/api/estimations/' + encodeURIComponent(jobId));
    if (!r.ok) return;
    const job = await r.json();
    Store.set(job);
    window.location.href = '/platform/scope';
  } catch { /* ignore */ }
}

// ================================================================ Agent-instruction popups
function wireAgentStepButtons() {
  $all('[data-agent-step]').forEach(b => {
    // Skip ones inside dynamically-rendered step cards (wired on render).
    if (b.closest('#stepCards')) return;
    b.addEventListener('click', () => showAgentStep(b.dataset.agentStep));
  });
}

async function getAgentInstructions() {
  if (AGENT_INSTRUCTIONS) return AGENT_INSTRUCTIONS;
  try {
    const r = await fetch('/api/agent-instructions');
    AGENT_INSTRUCTIONS = await r.json();
  } catch { AGENT_INSTRUCTIONS = null; }
  return AGENT_INSTRUCTIONS;
}

async function showAgentStep(key) {
  openModal('Agent instructions', '<p class="muted">Loading…</p>');
  const data = await getAgentInstructions();
  if (!data) { setModalBody('<p class="muted">Agent instructions unavailable.</p>'); return; }
  const step = data.steps.find(s => s.key === key);
  if (!step) { setModalBody('<p class="muted">No instructions for this step.</p>'); return; }
  setModalTitle(`${step.title} — ${step.agent}`);
  setModalBody(`
    <p class="step-goal"><strong>Goal:</strong> ${esc(step.goal)}</p>
    <h4>Agent persona</h4>
    <pre class="doc-md">${esc(data.persona)}</pre>
    <h4>Step instructions</h4>
    <pre class="doc-md">${esc(step.instructions)}</pre>`);
}

// ================================================================ Modal
function wireModal() {
  const root = $('#modalRoot');
  if (!root) return;
  root.addEventListener('click', (e) => { if (e.target.dataset.close) closeModal(); });
  document.addEventListener('keydown', (e) => { if (e.key === 'Escape') closeModal(); });
}
function openModal(title, bodyHtml) { setModalTitle(title); setModalBody(bodyHtml); const r = $('#modalRoot'); if (r) r.hidden = false; }
function closeModal() { const r = $('#modalRoot'); if (r) r.hidden = true; }
function setModalTitle(t) { const e = $('#modalTitle'); if (e) e.textContent = t; }
function setModalBody(html) { const e = $('#modalBody'); if (e) e.innerHTML = html; }
