// Relay Desk — SPA front-end. Hash-routed 5-stage view over the orchestration API.
(() => {
  'use strict';
  const $ = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => Array.from(r.querySelectorAll(s));
  const esc = (s) => (s ?? '').toString().replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const pct = (n) => Math.round((Number(n) || 0) * 100);

  const SUBTITLE = {
    email: 'Watched mailbox — inbound messages that trigger the orchestration pipeline.',
    triage: 'Classification agent — category, urgency, sentiment, spam risk and SLA.',
    intent: 'Intent router — decides the purpose and routes uncertain cases to a human queue.',
    task: 'Task agent — mock Dynamics 365 MCP lookups + the downstream operation.',
    outcome: 'Outcome reporter — final status, drafted reply and the full audit trail.'
  };
  const TITLE = { email: 'Email', triage: 'Triage', intent: 'Intent', task: 'Task · Dynamics 365', outcome: 'Outcome' };

  const state = { current: null, agents: {}, view: 'email' };

  const api = {
    async get(u) { const r = await fetch(u); if (!r.ok) throw new Error(`${u} → ${r.status}`); return r.json(); },
    async post(u, body) { const r = await fetch(u, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: body ? JSON.stringify(body) : null }); if (!r.ok) throw new Error(`${u} → ${r.status}`); return r.json(); },
    async del(u) { const r = await fetch(u, { method: 'DELETE' }); if (!r.ok) throw new Error(`${u} → ${r.status}`); return r.json(); }
  };

  let toastT;
  function toast(msg, kind = '') {
    const t = $('#toast'); t.className = `toast ${kind}`; t.innerHTML = msg; t.classList.remove('hidden');
    clearTimeout(toastT); toastT = setTimeout(() => t.classList.add('hidden'), 4200);
  }

  const confClass = (c) => c >= 0.75 ? 'good' : c >= 0.5 ? 'warn' : 'bad';
  const prioClass = (p) => ({ P1: 'p1', P2: 'p2', P3: 'p3', P4: 'p4' }[p] || '');
  const execKind = (s) => ({ executed: 'good', simulated: 'info', 'deferred-to-human': 'warn', skipped: '' }[s] || '');
  function meter(c) { return `<div class="meter ${confClass(c)}"><i style="width:${pct(c)}%"></i></div>`; }
  function badge(text, kind = '') { return `<span class="badge ${kind}">${esc(text)}</span>`; }
  function chips(arr) { return (arr && arr.length) ? arr.map(a => `<span class="chip">${esc(a)}</span>`).join('') : '<span class="muted">—</span>'; }
  function emptyDetail(msg) { return `<div class="empty">${esc(msg)}</div>`; }
  function statusKind(s) { s = (s || '').toLowerCase(); if (s.includes('spam')) return ''; if (s.includes('human') || s.includes('pending')) return 'warn'; if (s.includes('action') || s.includes('resolved')) return 'good'; return ''; }

  async function loadHealth() {
    try {
      const h = await api.get('/api/health');
      const b = $('#engineBadge');
      b.className = `engine-badge ${h.engine === 'foundry' ? 'live' : 'offline'}`;
      b.textContent = `● engine: ${h.engine}${h.foundryConfigured ? '' : ' (offline)'}`;
      b.title = `Foundry configured: ${h.foundryConfigured} · human-review threshold ${h.intentHumanReviewThreshold}`;
    } catch { }
  }

  async function loadAgents() { try { const list = await api.get('/api/agents'); list.forEach(a => state.agents[a.key] = a); } catch { } }
  const PAGE_AGENT = { email: 'extraction', triage: 'triage', intent: 'intent', task: 'task', outcome: 'outcome' };
  function renderAgentPanel(view) {
    const a = state.agents[PAGE_AGENT[view]];
    const panel = $('#agentPanel');
    if (!a) { panel.classList.add('hidden'); return; }
    panel.classList.remove('hidden');
    $('#agentName').textContent = a.name;
    $('#agentRole').textContent = a.role;
    $('#agentInstructions').textContent = a.instructions;
  }

  async function loadInbox() {
    try {
      const inbox = await api.get('/api/inbox');
      $('#inboxCount').textContent = inbox.length;
      $('#inbox').innerHTML = inbox.map(e => `
        <div class="mail" data-id="${esc(e.id)}">
          <div class="m-top">
            <span class="m-from">${e.unread ? '<span class="dot-unread"></span> ' : ''}${esc(e.fromName || e.from)}</span>
            <span class="m-mbx">${esc(e.mailbox || '')}</span>
          </div>
          <div class="m-sub">${esc(e.subject)}</div>
          <div class="m-pre">${esc(e.body)}</div>
        </div>`).join('');
      $$('#inbox .mail').forEach(el => el.addEventListener('click', () => selectMail(el.dataset.id, inbox)));
    } catch { $('#inbox').innerHTML = `<div class="muted">Failed to load inbox.</div>`; }
  }

  function selectMail(id, inbox) {
    $$('#inbox .mail').forEach(m => m.classList.toggle('sel', m.dataset.id === id));
    const e = inbox.find(x => x.id === id); if (!e) return;
    $('#selectedPreview').classList.remove('muted');
    $('#selectedPreview').textContent = `From: ${e.fromName} <${e.from}>\nMailbox: ${e.mailbox}\nSubject: ${e.subject}\n\n${e.body}`;
    const btn = $('#runSelected'); btn.classList.remove('hidden'); btn.dataset.id = id;
  }

  async function loadCases() {
    try {
      const cases = await api.get('/api/cases');
      const q = $('#queue');
      if (!cases.length) { q.innerHTML = `<div class="muted">No processed cases yet. Ingest an email or run the whole mailbox.</div>`; return; }
      q.innerHTML = cases.map(c => `
        <div class="qrow" data-id="${esc(c.caseId)}">
          <div>
            <div class="q-title">${esc(c.subject)}</div>
            <div class="q-meta">
              ${badge(c.engine, c.engine === 'foundry' ? 'info' : '')}
              ${badge(c.category)} ${badge(c.urgency, prioClass(c.urgency))}
              <span>${esc(c.from)}</span>${c.account ? ` · <span>${esc(c.account)}</span>` : ''}
            </div>
          </div>
          <div class="q-right">
            ${c.requiresHuman ? badge('Human review', 'warn') : badge(c.intent, 'info')}
            ${badge(c.finalStatus, statusKind(c.finalStatus))}
          </div>
        </div>`).join('');
      $$('#queue .qrow').forEach(el => el.addEventListener('click', () => openCase(el.dataset.id)));
    } catch { }
  }

  async function loadQueue() {
    try {
      const items = await api.get('/api/queue');
      const pending = items.filter(i => i.status === 'pending');
      $('#queueCount').textContent = pending.length;
      $('#queueCount2').textContent = pending.length;
      const host = $('#humanQueue');
      if (!items.length) { host.innerHTML = `<div class="muted">No items awaiting human review.</div>`; return; }
      const intents = ['Billing Dispute', 'Cancellation Request', 'Technical Issue', 'Sales Enquiry', 'Complaint Escalation', 'Information Request', 'Renewal'];
      host.innerHTML = items.map(i => `
        <div class="hq-row" data-id="${esc(i.caseId)}">
          <div class="hq-top">
            <div><strong>${esc(i.subject)}</strong> <span class="muted">· ${esc(i.fromName)}</span></div>
            ${i.status === 'resolved' ? badge('resolved → ' + (i.resolvedIntent || ''), 'good') : badge('pending', 'warn')}
          </div>
          <div class="q-meta muted" style="margin-top:6px">
            Proposed: ${badge(i.proposedIntent || 'Unknown', 'info')} conf ${pct(i.intentConfidence)}% · ${esc(i.reason)} · queue ${esc(i.queue)}
          </div>
          ${i.status === 'pending' ? `
          <div class="hq-actions">
            <select data-sel="${esc(i.caseId)}">${intents.map(x => `<option ${x === i.proposedIntent ? 'selected' : ''}>${x}</option>`).join('')}</select>
            <button class="btn primary sm" data-resolve="${esc(i.caseId)}">Confirm intent</button>
            <button class="btn sm" data-open="${esc(i.caseId)}">Open case</button>
          </div>` : `<div class="hq-actions"><button class="btn sm" data-open="${esc(i.caseId)}">Open case</button></div>`}
        </div>`).join('');
      $$('#humanQueue [data-resolve]').forEach(b => b.addEventListener('click', async () => {
        const id = b.dataset.resolve; const sel = $(`select[data-sel="${id}"]`);
        b.disabled = true; b.innerHTML = '<span class="spin"></span>Confirming';
        try { await api.post(`/api/queue/${id}/resolve`, { intent: sel.value, resolvedBy: 'reviewer' }); toast('Intent confirmed; case updated.', 'good'); await refreshAll(); await openCase(id); }
        catch { toast('Failed to resolve.', 'bad'); b.disabled = false; b.textContent = 'Confirm intent'; }
      }));
      $$('#humanQueue [data-open]').forEach(b => b.addEventListener('click', () => openCase(b.dataset.open)));
    } catch { }
  }

  async function runInbox(id, btn) {
    const prev = btn ? btn.innerHTML : null;
    if (btn) { btn.disabled = true; btn.innerHTML = '<span class="spin"></span>Running pipeline…'; }
    try {
      const c = await api.post(`/api/cases/from-inbox/${id}`, null);
      state.current = c; toast(`Processed → ${esc(c.intent.intent)} · ${esc(c.outcome.finalStatus)}`, 'good');
      await refreshAll(); renderAll(); go('triage');
    } catch (e) { toast('Pipeline failed: ' + esc(e.message), 'bad'); }
    finally { if (btn) { btn.disabled = false; btn.innerHTML = prev; } }
  }

  async function runAdhoc(btn) {
    const from = $('#fldFrom').value.trim(), subject = $('#fldSubject').value.trim(), body = $('#fldBody').value.trim();
    if (!from && !body) { toast('Enter at least a From or Body.', 'bad'); return; }
    const prev = btn.innerHTML; btn.disabled = true; btn.innerHTML = '<span class="spin"></span>Running…';
    try {
      const c = await api.post('/api/cases', { from, fromName: from.split('@')[0], subject, body, channel: 'email', mailbox: 'support@relay-desk.example' });
      state.current = c; toast(`Processed → ${esc(c.intent.intent)}`, 'good');
      $('#fldFrom').value = $('#fldSubject').value = $('#fldBody').value = '';
      await refreshAll(); renderAll(); go('triage');
    } catch (e) { toast('Pipeline failed: ' + esc(e.message), 'bad'); }
    finally { btn.disabled = false; btn.innerHTML = prev; }
  }

  async function openCase(id) {
    try { state.current = await api.get(`/api/cases/${id}`); renderAll(); go('triage'); }
    catch { toast('Failed to open case.', 'bad'); }
  }

  function renderAll() { renderTriage(); renderIntent(); renderTask(); renderOutcome(); updatePipeline(); }

  function currentHeader() {
    const c = state.current; if (!c) return '';
    return `<div class="card"><div class="card-head"><h2>${esc(c.source.subject)}</h2>${badge(c.reference)} ${badge(c.engine, c.engine === 'foundry' ? 'info' : '')}</div>
      <div class="q-meta muted">From ${esc(c.source.fromName)} &lt;${esc(c.source.from)}&gt; · ${esc(c.source.mailbox)} · ${esc(c.source.channel)}</div></div>`;
  }

  function timeline(trace) {
    if (!trace || !trace.length) return '';
    return `<div class="card"><div class="section-title">🧭 Agent timeline</div><div class="timeline">${trace.map(t => `
      <div class="tl ${t.engine}">
        <div class="tl-h"><span class="tl-stage">${esc(t.stage)}</span>
          ${badge(t.engine, t.engine === 'foundry' ? 'info' : t.engine === 'human' ? 'good' : '')}
          ${t.decision ? badge(t.decision) : ''}
          ${t.confidence != null ? `<span class="muted" style="font-size:12px">conf ${pct(t.confidence)}%</span>` : ''}
        </div>
        <div class="tl-sum">${esc(t.summary)}</div>
        <div class="tl-meta"><span>🧠 ${esc(t.agent)}</span><span>⏱ ${t.durationMs}ms</span></div>
      </div>`).join('')}</div></div>`;
  }

  function renderTriage() {
    const host = $('#triageBody'); const c = state.current;
    if (!c) { host.innerHTML = emptyDetail('Run a pipeline to see triage results.'); return; }
    const t = c.triage, x = c.extraction;
    host.innerHTML = currentHeader() + `
      <div class="card">
        <div class="section-title">📥 Extraction</div>
        <div class="kv">
          <div><div class="k">Language</div><div class="v">${esc(x.language)}</div></div>
          <div><div class="k">Extraction confidence</div><div class="v">${pct(x.extractionConfidence)}%</div>${meter(x.extractionConfidence)}</div>
          <div><div class="k">Refs found</div><div class="v">${(x.orderRefs || []).length}</div></div>
        </div>
        <div style="margin-top:12px"><div class="k">Entities</div><div style="margin-top:6px">${chips(x.entities)}</div></div>
        <div style="margin-top:10px"><div class="k">Account hints</div><div style="margin-top:6px">${chips(x.accountHints)}</div></div>
        <div style="margin-top:10px"><div class="k">Order / invoice refs</div><div style="margin-top:6px">${chips(x.orderRefs)}</div></div>
      </div>
      <div class="card">
        <div class="section-title">🏷️ Triage classification</div>
        <div class="dcards">
          <div class="dcard"><div class="d-h">Category</div><div class="d-v">${esc(t.category)}</div><div class="d-sub">${esc(t.subCategory)}</div></div>
          <div class="dcard"><div class="d-h">Urgency</div><div class="d-v">${badge(t.urgency, prioClass(t.urgency))}</div><div class="d-sub">SLA ${t.slaHours}h</div></div>
          <div class="dcard"><div class="d-h">Sentiment</div><div class="d-v">${esc(t.sentiment)}</div></div>
          <div class="dcard"><div class="d-h">Spam risk</div><div class="d-v">${pct(t.spamRisk)}%</div>${meter(t.spamRisk)}</div>
          <div class="dcard"><div class="d-h">Triage confidence</div><div class="d-v">${pct(t.triageConfidence)}%</div>${meter(t.triageConfidence)}</div>
        </div>
        <div style="margin-top:12px"><div class="k">Risk flags</div><div style="margin-top:6px">${chips(t.riskFlags)}</div></div>
        <p class="muted" style="margin-top:12px">${esc(t.rationale)}</p>
      </div>` + timeline(c.trace);
  }

  function renderIntent() {
    const host = $('#intentBody'); const c = state.current;
    if (!c) { host.innerHTML = emptyDetail('Run a pipeline to see the intent decision.'); return; }
    const i = c.intent;
    const altRows = (i.alternativeIntents || []).map(a => `<div class="q-meta" style="margin-top:6px">${badge(a.intent)} <span class="muted">conf ${pct(a.confidence)}%</span></div>`).join('') || '<span class="muted">—</span>';
    host.innerHTML = currentHeader() + `
      <div class="card">
        <div class="section-title">🎯 Intent decision</div>
        <div class="dcards">
          <div class="dcard"><div class="d-h">Intent</div><div class="d-v">${esc(i.intent)}</div><div class="d-sub">band ${esc(i.intentBand)}</div></div>
          <div class="dcard"><div class="d-h">Confidence</div><div class="d-v">${pct(i.intentConfidence)}%</div>${meter(i.intentConfidence)}</div>
          <div class="dcard"><div class="d-h">Routing</div><div class="d-v" style="font-size:15px">${esc(i.suggestedQueue)}</div></div>
          <div class="dcard"><div class="d-h">Human review</div><div class="d-v">${i.requiresHuman ? badge('REQUIRED', 'warn') : badge('No', 'good')}</div></div>
        </div>
        ${i.requiresHuman ? `<div class="card" style="margin-top:12px;background:rgba(251,191,36,.06);border-color:rgba(251,191,36,.35)">
          <strong>🧑‍⚖️ Routed to human queue.</strong> <span class="muted">${esc(i.humanReason)}</span>
          <div style="margin-top:8px"><button class="btn sm" id="gotoQueue">Go to human queue ↗</button></div></div>` : ''}
        <div style="margin-top:14px"><div class="section-title">Alternative intents considered</div>${altRows}</div>
        <p class="muted" style="margin-top:12px">${esc(i.rationale)}</p>
      </div>` + timeline(c.trace);
    const gq = $('#gotoQueue'); if (gq) gq.addEventListener('click', () => { go('email'); $('#humanQueueCard').scrollIntoView({ behavior: 'smooth' }); });
  }

  function mcpCard(call) {
    const args = Object.entries(call.arguments || {}).map(([k, v]) => `${esc(k)}=${esc(v)}`).join(', ') || '—';
    return `<div class="mcp-call">
      <div class="mcp-top">
        <span class="mcp-tool">${esc(call.tool)}</span>
        <span class="mcp-sum">${esc(call.resultSummary)}</span>
        ${badge(call.ok ? 'ok' : 'error', call.ok ? 'good' : 'bad')}
        <span class="mcp-badge muted" style="font-size:11px">⏱ ${call.durationMs}ms · ▾</span>
      </div>
      <div class="mcp-body hidden">
        <div class="mcp-args">args: <code>${args}</code></div>
        <pre class="mcp-json">${esc(call.resultJson)}</pre>
      </div>
    </div>`;
  }

  function renderTask() {
    const host = $('#taskBody'); const c = state.current;
    if (!c) { host.innerHTML = emptyDetail('Run a pipeline to see the Dynamics 365 task execution.'); return; }
    const k = c.task, cust = k.customer, p = k.plan;
    const custCard = cust.matched ? `
      <div class="dcards">
        <div class="dcard"><div class="d-h">Account</div><div class="d-v" style="font-size:16px">${esc(cust.accountName)}</div><div class="d-sub">${esc(cust.accountId)} · ${esc(cust.industry)}</div></div>
        <div class="dcard"><div class="d-h">Tier</div><div class="d-v">${badge(cust.tier, 'info')}</div><div class="d-sub">status ${esc(cust.status)}</div></div>
        <div class="dcard"><div class="d-h">Annual value</div><div class="d-v">${cust.annualValue != null ? '$' + Number(cust.annualValue).toLocaleString() : '—'}</div><div class="d-sub">owner ${esc(cust.owner)}</div></div>
        <div class="dcard"><div class="d-h">Open items</div><div class="d-v">${cust.openServiceCases} cases</div><div class="d-sub">${cust.openOpportunities} opps · contact ${esc(cust.primaryContact)}</div></div>
      </div>` : `<div class="card" style="background:rgba(248,113,113,.06);border-color:rgba(248,113,113,.3)"><strong>No D365 match.</strong> <span class="muted">${esc(cust.matchNote)}</span></div>`;

    const opArgs = Object.entries(p.operationArgs || {}).map(([kk, v]) => `${esc(kk)}=${esc(v)}`).join(', ') || '—';
    host.innerHTML = currentHeader() + `
      <div class="card">
        <div class="card-head"><div class="section-title" style="margin:0">🧩 Customer context (via D365 MCP)</div>${cust.matchNote ? badge(cust.matched ? 'matched' : 'unmatched', cust.matched ? 'good' : 'bad') : ''}</div>
        ${custCard}
      </div>
      <div class="card">
        <div class="section-title">🛠️ Planned operation</div>
        <div class="dcards">
          <div class="dcard"><div class="d-h">Operation</div><div class="d-v" style="font-size:16px">${esc(p.operation)}</div><div class="d-sub">${p.plannedTool ? `<span class="op-tool">⚙ ${esc(p.plannedTool)}</span>` : ''}</div></div>
          <div class="dcard"><div class="d-h">Risk level</div><div class="d-v">${badge(p.riskLevel, p.riskLevel === 'High' ? 'bad' : p.riskLevel === 'Medium' ? 'warn' : 'good')}</div></div>
          <div class="dcard"><div class="d-h">Approval</div><div class="d-v">${p.requiresApproval ? badge('Required', 'warn') : badge('Not needed', 'good')}</div></div>
          <div class="dcard"><div class="d-h">Execution</div><div class="d-v">${badge(k.executionStatus, execKind(k.executionStatus))}</div><div class="d-sub">${esc(k.operationReference || '')}</div></div>
        </div>
        <div style="margin-top:12px"><div class="k">Operation args</div><div style="margin-top:5px"><code>${opArgs}</code></div></div>
        <p style="margin-top:10px">${esc(p.expectedEffect)}</p>
        <p class="muted" style="margin-top:4px">${esc(p.rationale)}</p>
        ${k.operationResult ? `<div class="reply" style="border-left-color:var(--good);margin-top:10px">${esc(k.operationResult)}</div>` : ''}
      </div>
      <div class="card">
        <div class="card-head"><div class="section-title" style="margin:0">🔌 D365 MCP tool calls</div><span class="pill">${(k.toolCalls || []).length}</span></div>
        <div class="mcp">${(k.toolCalls || []).length ? k.toolCalls.map(mcpCard).join('') : '<span class="muted">No MCP calls (spam / deferred).</span>'}</div>
      </div>` + timeline(c.trace);
    $$('#taskBody .mcp-top').forEach(t => t.addEventListener('click', () => t.nextElementSibling.classList.toggle('hidden')));
  }

  function renderOutcome() {
    const host = $('#outcomeBody'); const c = state.current;
    if (!c) { host.innerHTML = emptyDetail('Run a pipeline to see the outcome and audit trail.'); return; }
    const o = c.outcome;
    host.innerHTML = currentHeader() + `
      <div class="card">
        <div class="section-title">✅ Outcome</div>
        <div class="dcards">
          <div class="dcard"><div class="d-h">Final status</div><div class="d-v" style="font-size:16px">${badge(o.finalStatus, statusKind(o.finalStatus))}</div></div>
          <div class="dcard"><div class="d-h">SLA</div><div class="d-v">${o.slaMet ? badge('Met', 'good') : badge('At risk', 'bad')}</div></div>
          <div class="dcard"><div class="d-h">Engine</div><div class="d-v">${badge(c.engine, c.engine === 'foundry' ? 'info' : '')}</div></div>
        </div>
        <p style="margin-top:12px">${esc(o.executiveSummary)}</p>
      </div>
      <div class="card">
        <div class="section-title">✉️ Drafted customer reply</div>
        <div class="reply">${esc(o.customerReplyDraft)}</div>
      </div>
      <div class="card">
        <div class="section-title">📋 Next actions</div>
        <ul class="list-tight">${(o.nextActions || []).map(a => `<li>${esc(a)}</li>`).join('') || '<li class="muted">—</li>'}</ul>
      </div>
      <div class="card">
        <div class="card-head"><div class="section-title" style="margin:0">🧾 Audit trail</div><span class="pill">${(o.auditTrail || []).length}</span></div>
        <div class="audit">${(o.auditTrail || []).map(a => `<div class="audit-row"><div class="a-step">${esc(a.step)}</div><div>${esc(a.detail)}</div></div>`).join('')}</div>
      </div>` + timeline(c.trace);
  }

  // ---------- pipeline header state ----------
  function updatePipeline() {
    const order = ['email', 'triage', 'intent', 'task', 'outcome'];
    const idx = order.indexOf(state.view);
    $$('#pipeline .stage-pill').forEach(p => {
      const i = order.indexOf(p.dataset.stage);
      p.classList.toggle('active', p.dataset.stage === state.view);
      p.classList.toggle('done', state.current && i < idx);
    });
  }

  // ---------- routing ----------
  function go(view) { if (!['email','triage','intent','task','outcome'].includes(view)) view = 'email'; location.hash = '#' + view; }
  function applyRoute() {
    let view = (location.hash || '#email').replace('#', '');
    if (!['email','triage','intent','task','outcome'].includes(view)) view = 'email';
    state.view = view;
    $$('.view').forEach(v => v.classList.add('hidden'));
    $(`#view-${view}`).classList.remove('hidden');
    $$('.nav-item').forEach(n => n.classList.toggle('active', n.dataset.view === view && !n.classList.contains('subtle')));
    $('#viewTitle').textContent = TITLE[view];
    $('#viewSubtitle').textContent = SUBTITLE[view];
    renderAgentPanel(view);
    $('#agentBody').classList.add('hidden');
    $('#agentToggle').setAttribute('aria-expanded', 'false');
    $('#agentToggle').textContent = 'View instructions ▾';
    if (view !== 'email' && !state.current) { /* detail shows empty hint */ }
    updatePipeline();
  }

  async function refreshAll() { await Promise.all([loadInbox(), loadCases(), loadQueue(), loadHealth()]); }

  function wire() {
    $('#runSelected').addEventListener('click', (e) => runInbox(e.currentTarget.dataset.id, e.currentTarget));
    $('#runAdhoc').addEventListener('click', (e) => runAdhoc(e.currentTarget));
    $('#runDemo').addEventListener('click', async (e) => {
      const b = e.currentTarget, prev = b.innerHTML; b.disabled = true; b.innerHTML = '<span class="spin"></span>Running mailbox…';
      try { const r = await api.post('/api/cases/run-demo', null); toast(`Processed ${r.processed} emails (engine: ${r.engine}).`, 'good'); await refreshAll(); }
      catch { toast('Run failed.', 'bad'); } finally { b.disabled = false; b.innerHTML = prev; }
    });
    $('#resetCases').addEventListener('click', async () => {
      if (!confirm('Clear all processed cases and the human queue?')) return;
      try { await api.del('/api/cases'); state.current = null; toast('Reset done.', 'good'); await refreshAll(); renderAll(); } catch { toast('Reset failed.', 'bad'); }
    });
    $('#agentToggle').addEventListener('click', () => {
      const body = $('#agentBody'); const open = body.classList.toggle('hidden') === false;
      $('#agentToggle').setAttribute('aria-expanded', String(open));
      $('#agentToggle').textContent = open ? 'Hide instructions ▴' : 'View instructions ▾';
    });
    $('#agentPanelHead').addEventListener('click', (e) => { if (e.target.id !== 'agentToggle') $('#agentToggle').click(); });
    $$('.nav-item').forEach(n => n.addEventListener('click', () => go(n.dataset.view)));
    window.addEventListener('hashchange', applyRoute);
  }

  async function init() {
    wire();
    await loadAgents();
    applyRoute();
    await refreshAll();
    renderAll();
  }
  document.addEventListener('DOMContentLoaded', init);
})();
