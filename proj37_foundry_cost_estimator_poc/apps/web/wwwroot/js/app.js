'use strict';

const $ = (sel) => document.querySelector(sel);
const fmtMoney = (n, ccy) => {
  const sym = { USD: '$', AUD: 'A$', EUR: '€', GBP: '£' }[ccy] || (ccy + ' ');
  return sym + Number(n || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
};
const esc = (s) => String(s ?? '').replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

let selectedFiles = [];

document.addEventListener('DOMContentLoaded', () => {
  loadHealth();
  loadHistory();
  wireUpload();
  wireTabs();
});

async function loadHealth() {
  try {
    const r = await fetch('/api/health');
    const h = await r.json();
    const badge = $('#engineBadge');
    badge.textContent = 'engine: ' + h.engine;
    badge.className = 'badge ' + (h.engine === 'foundry' ? 'foundry' : 'offline');
  } catch { /* ignore */ }
}

function wireUpload() {
  const input = $('#fileInput');
  const dz = $('#dropzone');

  input.addEventListener('change', () => { selectedFiles = Array.from(input.files); renderFileList(); });

  ['dragover', 'dragenter'].forEach(ev => dz.addEventListener(ev, (e) => { e.preventDefault(); dz.classList.add('drag'); }));
  ['dragleave', 'drop'].forEach(ev => dz.addEventListener(ev, (e) => { e.preventDefault(); dz.classList.remove('drag'); }));
  dz.addEventListener('drop', (e) => {
    selectedFiles = Array.from(e.dataTransfer.files);
    renderFileList();
  });

  $('#uploadForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    if (selectedFiles.length === 0) { setStatus('Please choose at least one document, or run the sample.', 'error'); return; }
    const fd = new FormData();
    selectedFiles.forEach(f => fd.append('files', f));
    await runEstimation(() => fetch('/api/estimations', { method: 'POST', body: fd }));
  });

  $('#sampleBtn').addEventListener('click', async () => {
    await runEstimation(() => fetch('/api/estimations/sample', { method: 'POST' }));
  });
}

function renderFileList() {
  const list = $('#fileList');
  if (selectedFiles.length === 0) { list.innerHTML = ''; $('#dropLabel').textContent = 'Click to choose files or drag & drop'; return; }
  $('#dropLabel').textContent = selectedFiles.length + ' file(s) selected';
  list.innerHTML = selectedFiles.map(f =>
    `<div class="file-chip"><span>📄 ${esc(f.name)}</span><span class="muted">${(f.size / 1024).toFixed(1)} KB</span></div>`).join('');
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
    renderResult(job);
    loadHistory();
  } catch (err) {
    setStatus('Request error: ' + err.message, 'error');
  } finally {
    setBusy(false);
  }
}

function renderResult(job) {
  $('#resultCard').hidden = false;
  $('#resultProject').textContent = job.scope?.projectName || 'Estimation result';
  const eng = $('#resultEngine');
  eng.textContent = 'engine: ' + job.engine;
  eng.className = 'badge ' + (job.engine === 'foundry' ? 'foundry' : 'offline');

  const dl = $('#downloadBtn');
  dl.href = `/api/estimations/${job.jobId}/workbook`;
  dl.setAttribute('download', '');

  const c = job.cost || {};
  $('#totals').innerHTML = `
    <div class="total-box"><div class="num">${fmtMoney(c.monthlyTotal, c.currency)}</div><div class="lbl">Monthly (raw)</div></div>
    <div class="total-box"><div class="num">${c.contingencyPercent || 0}%</div><div class="lbl">Contingency</div></div>
    <div class="total-box hi"><div class="num">${fmtMoney(c.monthlyTotalWithContingency, c.currency)}</div><div class="lbl">Monthly total</div></div>
    <div class="total-box hi"><div class="num">${fmtMoney(c.annualTotal, c.currency)}</div><div class="lbl">Annual (raw)</div></div>`;

  renderScope(job.scope || {});
  renderRequirements(job.requirements || []);
  renderCost(c);
  renderSteps(job.agentSteps || []);
  $('#resultCard').scrollIntoView({ behavior: 'smooth' });
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

function renderCost(c) {
  const items = c.lineItems || [];
  if (!items.length) { $('#tab-cost').innerHTML = '<p class="muted">No cost items.</p>'; return; }
  $('#tab-cost').innerHTML = `
    <table><thead><tr><th>Category</th><th>Service</th><th>SKU</th><th>Assumption</th>
      <th class="num-col">Qty</th><th class="num-col">Unit price</th><th class="num-col">Monthly</th></tr></thead>
    <tbody>${items.map(i => `<tr>
      <td>${esc(i.category)}</td><td>${esc(i.service)}</td><td>${esc(i.sku)}</td>
      <td class="muted">${esc(i.assumption)}</td>
      <td class="num-col">${Number(i.quantity).toLocaleString()}</td>
      <td class="num-col">${fmtMoney(i.unitPrice, c.currency)}</td>
      <td class="num-col">${fmtMoney(i.monthlyCost, c.currency)}</td></tr>`).join('')}
    </tbody>
    <tfoot><tr><th colspan="6" class="num-col">Monthly total (incl. ${c.contingencyPercent}% contingency)</th>
      <th class="num-col">${fmtMoney(c.monthlyTotalWithContingency, c.currency)}</th></tr></tfoot></table>
    <p class="muted" style="margin-top:.7rem">${(c.notes || []).map(esc).join(' · ')}</p>`;
}

function renderSteps(steps) {
  if (!steps.length) { $('#tab-steps').innerHTML = '<p class="muted">No steps recorded.</p>'; return; }
  $('#tab-steps').innerHTML = '<ul class="tight">' + steps.map(s =>
    `<li><strong>${esc(s.step)}:</strong> ${esc(s.summary)}</li>`).join('') + '</ul>';
}

function wireTabs() {
  document.querySelectorAll('.tab').forEach(t => t.addEventListener('click', () => {
    document.querySelectorAll('.tab').forEach(x => x.classList.remove('active'));
    document.querySelectorAll('.tab-panel').forEach(x => x.classList.remove('active'));
    t.classList.add('active');
    $('#tab-' + t.dataset.tab).classList.add('active');
  }));
}

async function loadHistory() {
  try {
    const r = await fetch('/api/estimations');
    const items = await r.json();
    const el = $('#history');
    if (!items.length) { el.innerHTML = '<p class="muted">No estimations yet.</p>'; return; }
    el.innerHTML = `<table><thead><tr><th>Project</th><th>Engine</th><th>Docs</th><th>Reqs</th>
      <th class="num-col">Monthly</th><th>Created</th><th></th></tr></thead><tbody>${items.map(i => `<tr>
      <td>${esc(i.project)}</td><td><span class="pill Could">${esc(i.engine)}</span></td>
      <td>${i.documents}</td><td>${i.requirements}</td>
      <td class="num-col">${fmtMoney(i.monthlyTotal, i.currency)}</td>
      <td class="muted">${new Date(i.createdUtc).toLocaleString()}</td>
      <td><a href="/api/estimations/${i.jobId}/workbook">⬇ xlsx</a></td></tr>`).join('')}</tbody></table>`;
  } catch {
    $('#history').innerHTML = '<p class="muted">Could not load history.</p>';
  }
}

function setStatus(msg, kind) { const s = $('#status'); s.hidden = false; s.textContent = msg; s.className = 'status ' + kind; }
function setBusy(b) { $('#estimateBtn').disabled = b; $('#sampleBtn').disabled = b; }
