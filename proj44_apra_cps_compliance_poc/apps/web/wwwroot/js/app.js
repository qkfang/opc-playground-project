'use strict';

const $ = (sel, root = document) => root.querySelector(sel);
const $all = (sel, root = document) => Array.from(root.querySelectorAll(sel));
const esc = (s) => String(s ?? '').replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const pct = (n) => `${Number(n ?? 0).toFixed(1)}%`;

let AGENT_INSTRUCTIONS = null;
let FRAMEWORK = null;

document.addEventListener('DOMContentLoaded', () => {
  loadHealth();
  wireModal();
  wireAgentStepButtons();
  wireRunButtons();
  const page = document.body.dataset.page || '';
  const init = {
    overview: initOverview,
    requirements: initRequirements,
    policies: initPolicies,
    standards: initStandards,
    controls: initControls,
    mappings: initMappings,
    gaps: initGaps,
    traceability: initTraceability,
    pipeline: initPipeline,
  }[page];
  if (init) init();
});

async function loadHealth() {
  try {
    const h = await (await fetch('/api/health')).json();
    setEngineBadge(h.engine);
    const pe = $('#pipeEngine'); if (pe) pe.textContent = `${h.engine}${h.foundryConfigured ? ' (Foundry configured)' : ' (offline / no Foundry config)'}`;
  } catch { /* ignore */ }
}
function setEngineBadge(engine) {
  const badge = $('#engineBadge');
  if (!badge) return;
  badge.textContent = 'engine: ' + engine;
  badge.className = 'badge ' + (engine === 'foundry' ? 'foundry' : 'offline');
}

async function getFramework(force = false) {
  if (FRAMEWORK && !force) return FRAMEWORK;
  FRAMEWORK = await (await fetch('/api/framework')).json();
  return FRAMEWORK;
}

// ================================================================ OVERVIEW
async function initOverview() {
  const fw = await getFramework();
  const s = fw.source || {};
  $('#sourceLine').innerHTML = `<strong>${esc(s.regulator)} ${esc(s.code)}</strong> — ${esc(s.title)} · ${esc(s.version)}`;
  renderCounts('#countCards', fw.counts);
  $('#sourceCard').innerHTML = `
    <dl class="kv">
      <dt>Regulator</dt><dd>${esc(s.regulator)}</dd>
      <dt>Standard</dt><dd>${esc(s.code)} — ${esc(s.title)}</dd>
      <dt>Version</dt><dd>${esc(s.version)}</dd>
      <dt>Summary</dt><dd>${esc(s.summary)}</dd>
      <dt>Themes</dt><dd>${(s.themes || []).map(t => `<span class="tag">${esc(t)}</span>`).join('')}</dd>
    </dl>`;
  renderClauses(fw.clauses || []);
}

function renderCounts(sel, c) {
  const host = $(sel);
  if (!host || !c) return;
  const boxes = [
    ['Clauses', c.clauses], ['Requirements', c.requirements], ['Policies', c.policies],
    ['Standards', c.standards], ['Controls', c.controls],
    ['Req→Pol links', c.requirementToPolicyLinks], ['Pol→Std links', c.policyToStandardLinks],
    ['Std→Ctl links', c.standardToControlLinks],
  ];
  host.innerHTML = boxes.map(([lbl, n]) =>
    `<div class="count-box"><div class="num">${n}</div><div class="lbl">${lbl}</div></div>`).join('');
}

function renderClauses(clauses) {
  if (!clauses.length) { $('#clauseTable').innerHTML = '<p class="muted">No clauses.</p>'; return; }
  $('#clauseTable').innerHTML = `<table><thead><tr><th>ID</th><th>Reference</th><th>Theme</th><th>Heading</th><th>Text</th></tr></thead>
    <tbody>${clauses.map(c => `<tr>
      <td class="mono">${esc(c.id)}</td><td>${esc(c.reference)}</td>
      <td><span class="tag">${esc(c.theme)}</span></td><td>${esc(c.heading)}</td>
      <td class="muted">${esc(c.text)}</td></tr>`).join('')}</tbody></table>`;
}

// ================================================================ REQUIREMENTS
async function initRequirements() {
  const fw = await getFramework();
  const reqs = fw.requirements || [];
  const polById = indexBy(fw.policies);
  const render = (filter) => {
    const rows = reqs.filter(r => matchText(filter, r.id, r.title, r.text, r.theme));
    $('#reqCount').textContent = `${rows.length} of ${reqs.length}`;
    $('#reqTable').innerHTML = rows.length ? `<table><thead><tr><th>ID</th><th>Theme</th><th>Obligation</th><th>Requirement</th><th>Clause</th><th>Policies</th></tr></thead>
      <tbody>${rows.map(r => `<tr>
        <td class="mono">${esc(r.id)}</td><td><span class="tag">${esc(r.theme)}</span></td>
        <td><span class="pill ${esc(r.obligation)}">${esc(r.obligation)}</span></td>
        <td><strong>${esc(r.title)}</strong><br><span class="muted">${esc(r.text)}</span></td>
        <td class="mono">${esc(r.clauseId)}</td>
        <td>${mapTags(r.policyIds, polById)}</td></tr>`).join('')}</tbody></table>`
      : '<p class="muted">No matching requirements.</p>';
  };
  render('');
  wireFilter('#reqFilter', render);
}

// ================================================================ POLICIES
async function initPolicies() {
  const fw = await getFramework();
  const pols = fw.policies || [];
  const stdById = indexBy(fw.standards);
  const render = (filter) => {
    const rows = pols.filter(p => matchText(filter, p.id, p.title, p.statement, p.domain, p.owner));
    $('#polCount').textContent = `${rows.length} of ${pols.length}`;
    $('#polTable').innerHTML = rows.length ? `<table><thead><tr><th>ID</th><th>Domain</th><th>Policy</th><th>Owner</th><th>Standards</th></tr></thead>
      <tbody>${rows.map(p => `<tr>
        <td class="mono">${esc(p.id)}</td><td><span class="tag">${esc(p.domain)}</span></td>
        <td><strong>${esc(p.title)}</strong><br><span class="muted">${esc(p.statement)}</span></td>
        <td>${esc(p.owner)}</td>
        <td>${mapTags(p.standardIds, stdById)}</td></tr>`).join('')}</tbody></table>`
      : '<p class="muted">No matching policies.</p>';
  };
  render('');
  wireFilter('#polFilter', render);
}

// ================================================================ STANDARDS
async function initStandards() {
  const fw = await getFramework();
  const stds = fw.standards || [];
  const ctlById = indexBy(fw.controls);
  const render = (filter) => {
    const rows = stds.filter(s => matchText(filter, s.id, s.title, s.requirement, s.domain));
    $('#stdCount').textContent = `${rows.length} of ${stds.length}`;
    $('#stdTable').innerHTML = rows.length ? `<table><thead><tr><th>ID</th><th>Domain</th><th>Standard</th><th>Controls</th></tr></thead>
      <tbody>${rows.map(s => `<tr>
        <td class="mono">${esc(s.id)}</td><td><span class="tag">${esc(s.domain)}</span></td>
        <td><strong>${esc(s.title)}</strong><br><span class="muted">${esc(s.requirement)}</span></td>
        <td>${mapTags(s.controlIds, ctlById)}</td></tr>`).join('')}</tbody></table>`
      : '<p class="muted">No matching standards.</p>';
  };
  render('');
  wireFilter('#stdFilter', render);
}

// ================================================================ CONTROLS
async function initControls() {
  const fw = await getFramework();
  const ctls = fw.controls || [];
  const render = (filter) => {
    const rows = ctls.filter(c => matchText(filter, c.id, c.title, c.description, c.domain, c.type, c.frequency));
    $('#ctlCount').textContent = `${rows.length} of ${ctls.length}`;
    $('#ctlTable').innerHTML = rows.length ? `<table><thead><tr><th>ID</th><th>Domain</th><th>Type</th><th>Frequency</th><th>Control</th><th>Test method</th></tr></thead>
      <tbody>${rows.map(c => `<tr>
        <td class="mono">${esc(c.id)}</td><td><span class="tag">${esc(c.domain)}</span></td>
        <td><span class="pill ${esc(c.type)}">${esc(c.type)}</span></td><td>${esc(c.frequency)}</td>
        <td><strong>${esc(c.title)}</strong><br><span class="muted">${esc(c.description)}</span></td>
        <td class="muted">${esc(c.testMethod)}</td></tr>`).join('')}</tbody></table>`
      : '<p class="muted">No matching controls.</p>';
  };
  render('');
  wireFilter('#ctlFilter', render);
}

// ================================================================ MAPPINGS
let MAP_VIEW = 'req-pol';
async function initMappings() {
  const fw = await getFramework();
  wireMapToggle();
  renderMap(fw);
}
function wireMapToggle() {
  const t = $('#mapToggle');
  if (!t || t.dataset.wired) return;
  $all('.env-btn', t).forEach(b => b.addEventListener('click', () => {
    MAP_VIEW = b.dataset.map;
    $all('.env-btn', t).forEach(x => { const on = x === b; x.classList.toggle('active', on); x.setAttribute('aria-selected', on ? 'true' : 'false'); });
    getFramework().then(renderMap);
  }));
  t.dataset.wired = '1';
}
function renderMap(fw) {
  const note = $('#mapNote');
  let html = '', noteText = '';
  if (MAP_VIEW === 'req-pol') {
    noteText = 'Each regulatory requirement and the policies that satisfy it. Rows with no policy are gaps.';
    const polById = indexBy(fw.policies);
    html = mapMatrix(fw.requirements, 'Requirement', r => r.title, r => r.policyIds, polById, 'Policies');
  } else if (MAP_VIEW === 'pol-std') {
    noteText = 'Each policy and the implementation standards that operationalise it. Rows with no standard are gaps.';
    const stdById = indexBy(fw.standards);
    html = mapMatrix(fw.policies, 'Policy', p => p.title, p => p.standardIds, stdById, 'Standards');
  } else {
    noteText = 'Each standard and the controls that enforce it. Rows with no control are gaps.';
    const ctlById = indexBy(fw.controls);
    html = mapMatrix(fw.standards, 'Standard', s => s.title, s => s.controlIds, ctlById, 'Controls');
  }
  if (note) note.textContent = noteText;
  $('#mapTable').innerHTML = html;
}
function mapMatrix(items, leftLabel, titleFn, idsFn, rightById, rightLabel) {
  if (!items || !items.length) return '<p class="muted">No data.</p>';
  return `<table><thead><tr><th>ID</th><th>${leftLabel}</th><th>${rightLabel}</th><th class="num-col">#</th></tr></thead>
    <tbody>${items.map(it => {
      const ids = idsFn(it) || [];
      return `<tr>
        <td class="mono">${esc(it.id)}</td>
        <td>${esc(titleFn(it))}</td>
        <td>${ids.length ? mapTags(ids, rightById) : '<span class="tag empty">— none (gap)</span>'}</td>
        <td class="num-col">${ids.length}</td></tr>`;
    }).join('')}</tbody></table>`;
}

// ================================================================ GAPS
async function initGaps() {
  const ga = await (await fetch('/api/gaps')).json();
  const c = ga.coverage || {};
  $('#coverageCards').innerHTML = [
    ['Requirement → Policy', c.requirementCoverage], ['Policy → Standard', c.policyCoverage],
    ['Standard → Control', c.standardCoverage], ['Control referenced', c.controlCoverage],
    ['End-to-end', c.endToEndCoverage],
  ].map(([lbl, v]) => {
    const warn = Number(v) < 100 ? ' warnbox' : '';
    return `<div class="count-box${warn}"><div class="num">${pct(v)}</div><div class="lbl">${lbl}</div></div>`;
  }).join('') + `<div class="count-box${ga.totalGaps ? ' warnbox' : ''}"><div class="num">${ga.totalGaps}</div><div class="lbl">Total gaps</div></div>`;

  $('#findings').innerHTML = '<ul class="tight">' + (ga.findings || []).map(f => `<li>${esc(f)}</li>`).join('') + '</ul>';

  const sections = [
    ['Requirements with no policy', ga.unmappedRequirements],
    ['Policies with no standard', ga.unmappedPolicies],
    ['Standards with no control', ga.unmappedStandards],
    ['Controls referenced by no standard', ga.orphanControls],
  ];
  $('#gapTables').innerHTML = sections.map(([title, items]) => `
    <h3 style="color:var(--brand);font-size:.92rem;margin:1rem 0 .4rem">${esc(title)} <span class="pill ${items && items.length ? 'gap' : 'ok'}">${items ? items.length : 0}</span></h3>
    ${(items && items.length) ? `<table><thead><tr><th>ID</th><th>Title</th><th>Detail</th></tr></thead>
      <tbody>${items.map(g => `<tr><td class="mono">${esc(g.id)}</td><td>${esc(g.title)}</td><td class="muted">${esc(g.detail)}</td></tr>`).join('')}</tbody></table>`
      : '<p class="muted">None.</p>'}`).join('');
}

// ================================================================ TRACEABILITY
async function initTraceability() {
  const fw = await getFramework();
  const sel = $('#reqSelect');
  sel.innerHTML = (fw.requirements || []).map(r => `<option value="${esc(r.id)}">${esc(r.id)} — ${esc(r.title)}</option>`).join('');
  sel.addEventListener('change', () => loadTrace(sel.value));
  if (fw.requirements && fw.requirements.length) loadTrace(fw.requirements[0].id);
}
async function loadTrace(reqId) {
  const view = $('#traceView');
  view.innerHTML = '<p class="muted">Loading chain…</p>';
  const r = await fetch('/api/traceability/' + encodeURIComponent(reqId));
  if (!r.ok) { view.innerHTML = '<p class="muted">Requirement not found.</p>'; return; }
  const t = await r.json();
  const req = t.requirement || {};
  let html = `<div class="trace-summary ${t.isComplete ? 'ok' : 'broken'}">${t.isComplete ? '✓ Complete chain — traces to at least one control.' : '✗ Broken chain — see issues below.'}</div>`;
  if (t.brokenLinks && t.brokenLinks.length)
    html += '<ul class="tight">' + t.brokenLinks.map(b => `<li class="trace-broken">${esc(b)}</li>`).join('') + '</ul>';
  html += '<div class="trace-tree">';
  html += `<div class="trace-node req"><span class="nid">${esc(req.id)}</span> · ${esc(req.title)} <span class="tag">${esc(req.theme)}</span><br><span class="muted">${esc(req.text)}</span></div>`;
  (t.policies || []).forEach(pn => {
    const p = pn.policy || {};
    html += `<div class="trace-node pol"><span class="nid">${esc(p.id)}</span> · ${esc(p.title)} <span class="muted">(${esc(p.domain)})</span></div>`;
    (pn.standards || []).forEach(sn => {
      const s = sn.standard || {};
      html += `<div class="trace-node std"><span class="nid">${esc(s.id)}</span> · ${esc(s.title)}</div>`;
      (sn.controls || []).forEach(c => {
        html += `<div class="trace-node ctl"><span class="nid">${esc(c.id)}</span> · ${esc(c.title)} <span class="pill ${esc(c.type)}">${esc(c.type)}</span></div>`;
      });
    });
  });
  html += '</div>';
  view.innerHTML = html;
}

// ================================================================ PIPELINE
async function initPipeline() {
  await renderAgentCards();
  await renderTranscript();
}
async function renderAgentCards() {
  const host = $('#agentCards');
  const data = await getAgentInstructions();
  if (!data) { host.innerHTML = '<p class="muted">Agent instructions unavailable.</p>'; return; }
  host.innerHTML = data.stages.map((s, i) => `
    <div class="step-card">
      <div class="step-card-head"><span class="step-num">${i + 1}</span><span class="step-badge">${esc(s.agent)}</span></div>
      <p><strong>${esc(s.title)}</strong><br>${esc(s.goal)}</p>
      <button type="button" class="btn btn-secondary btn-sm" data-agent-step="${esc(s.key)}">View instructions</button>
    </div>`).join('');
  $all('[data-agent-step]', host).forEach(b => b.addEventListener('click', () => showAgentStep(b.dataset.agentStep)));
}
async function renderTranscript() {
  const host = $('#transcript');
  const fw = await getFramework(true);
  const steps = fw.agentSteps || [];
  host.innerHTML = steps.length ? `<table><thead><tr><th>#</th><th>Stage</th><th>Agent</th><th>Summary</th></tr></thead>
    <tbody>${steps.map((s, i) => `<tr><td class="num-col">${i + 1}</td><td><span class="tag">${esc(s.step)}</span></td>
      <td>${esc(s.agent)}</td><td class="muted">${esc(s.summary)}</td></tr>`).join('')}</tbody></table>`
    : '<p class="muted">No transcript yet. Run the pipeline.</p>';
}

// ================================================================ RUN PIPELINE
function wireRunButtons() {
  ['#runBtn', '#runBtn2'].forEach(sel => { const b = $(sel); if (b) b.addEventListener('click', runPipeline); });
}
async function runPipeline() {
  const statusSel = document.body.dataset.page === 'pipeline' ? '#runStatus2' : '#runStatus';
  setStatus(statusSel, 'Running the six-agent pipeline…', 'busy');
  $all('#runBtn, #runBtn2').forEach(b => b.disabled = true);
  try {
    const res = await (await fetch('/api/run', { method: 'POST' })).json();
    setEngineBadge(res.engine);
    FRAMEWORK = null; // invalidate cache
    setStatus(statusSel, `✓ Pipeline complete (engine: ${res.engine}) — ${res.agentSteps.length} agent steps · ${res.counts.policies} policies · ${res.counts.controls} controls · ${res.gaps.totalGaps} gaps.`, 'info');
    const page = document.body.dataset.page;
    if (page === 'overview') initOverview();
    else if (page === 'pipeline') renderTranscript();
  } catch (err) {
    setStatus(statusSel, 'Run failed: ' + err.message, 'error');
  } finally {
    $all('#runBtn, #runBtn2').forEach(b => b.disabled = false);
  }
}
function setStatus(sel, msg, kind) { const s = $(sel); if (!s) return; s.hidden = false; s.textContent = msg; s.className = 'status ' + kind; }

// ================================================================ helpers
function indexBy(arr) { const m = {}; (arr || []).forEach(x => m[x.id] = x); return m; }
function mapTags(ids, byId) {
  if (!ids || !ids.length) return '<span class="tag empty">—</span>';
  return ids.map(id => {
    const item = byId[id];
    const title = item ? (item.title || '') : '';
    return `<span class="tag" title="${esc(title)}">${esc(id)}</span>`;
  }).join('');
}
function matchText(filter, ...fields) {
  if (!filter) return true;
  const f = filter.toLowerCase();
  return fields.some(x => String(x ?? '').toLowerCase().includes(f));
}
function wireFilter(sel, render) {
  const el = $(sel);
  if (!el) return;
  el.addEventListener('input', () => render(el.value));
}

// ================================================================ agent-instruction modal
function wireAgentStepButtons() {
  $all('[data-agent-step]').forEach(b => {
    if (b.closest('#agentCards')) return; // wired on render in renderAgentCards
    b.addEventListener('click', () => showAgentStep(b.dataset.agentStep));
  });
}
async function getAgentInstructions() {
  if (AGENT_INSTRUCTIONS) return AGENT_INSTRUCTIONS;
  try { AGENT_INSTRUCTIONS = await (await fetch('/api/agent-instructions')).json(); }
  catch { AGENT_INSTRUCTIONS = null; }
  return AGENT_INSTRUCTIONS;
}
async function showAgentStep(key) {
  openModal('Agent instructions', '<p class="muted">Loading…</p>');
  const data = await getAgentInstructions();
  if (!data) { setModalBody('<p class="muted">Agent instructions unavailable.</p>'); return; }
  const step = data.stages.find(s => s.key === key);
  if (!step) { setModalBody('<p class="muted">No instructions for this stage.</p>'); return; }
  setModalTitle(`${step.title} — ${step.agent}`);
  setModalBody(`
    <p class="step-goal"><strong>Goal:</strong> ${esc(step.goal)}</p>
    <h4>Shared persona</h4>
    <pre class="doc-md">${esc(data.persona)}</pre>
    <h4>Stage instructions</h4>
    <pre class="doc-md">${esc(step.instructions)}</pre>`);
}

// ================================================================ modal plumbing
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
