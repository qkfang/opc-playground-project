// proj39 — Intake & Origination POC front-end.
// Drives the mock inbox, triggers the pipeline, and renders the multi-agent result.

const $ = (sel, root = document) => root.querySelector(sel);
const esc = (s) => String(s ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const money = (v, cur) => (v == null ? '—' : `${cur || 'AUD'} ${Number(v).toLocaleString()}`);

let selectedEmailId = null;

async function api(path, opts) {
    const res = await fetch(path, opts);
    if (!res.ok && res.status !== 422) {
        let msg = `HTTP ${res.status}`;
        try { const j = await res.json(); msg = j.error || msg; } catch {}
        throw new Error(msg);
    }
    return res.json();
}

// -------- Inbox --------
async function loadInbox() {
    const list = $('#emailList');
    try {
        const emails = await api('/api/emails');
        if (!emails.length) { list.innerHTML = '<li class="muted">Inbox empty.</li>'; return; }
        list.innerHTML = emails.map(e => `
            <li class="email-item" data-id="${esc(e.id)}">
                <div class="ei-top">
                    <span class="ei-from">${esc(e.fromName || e.from)}</span>
                    <span class="ei-time">${new Date(e.receivedUtc).toLocaleDateString()}</span>
                </div>
                <div class="ei-subject">${esc(e.subject)}</div>
                <div class="ei-preview">${esc(e.preview)}</div>
                ${e.attachments ? `<div class="ei-attach">📎 ${e.attachments} attachment(s)</div>` : ''}
            </li>`).join('');
        list.querySelectorAll('.email-item').forEach(li =>
            li.addEventListener('click', () => selectEmail(li.dataset.id)));
    } catch (err) {
        list.innerHTML = `<li class="error">Failed to load inbox: ${esc(err.message)}</li>`;
    }
}

async function selectEmail(id) {
    selectedEmailId = id;
    document.querySelectorAll('.email-item').forEach(li =>
        li.classList.toggle('active', li.dataset.id === id));
    const reader = $('#emailReader');
    reader.innerHTML = '<p class="muted">Loading email…</p>';
    try {
        const e = await api(`/api/emails/${id}`);
        reader.innerHTML = `
            <div class="mail-head">
                <div class="mail-subject">${esc(e.subject)}</div>
                <div class="mail-meta"><strong>${esc(e.fromName || '')}</strong> &lt;${esc(e.from)}&gt;</div>
                <div class="mail-meta">to ${esc(e.to)} · ${new Date(e.receivedUtc).toLocaleString()}</div>
                ${e.attachments?.length ? `<div class="mail-attach">📎 ${e.attachments.map(esc).join(', ')}</div>` : ''}
            </div>
            <pre class="mail-body">${esc(e.body)}</pre>
            <button id="runBtn" class="btn btn-run" type="button">▶ Run intake &amp; origination pipeline</button>`;
        $('#runBtn').addEventListener('click', () => runPipeline(id));
    } catch (err) {
        reader.innerHTML = `<p class="error">Failed: ${esc(err.message)}</p>`;
    }
}

// -------- Pipeline --------
async function runPipeline(emailId) {
    const out = $('#pipeline');
    const btn = $('#runBtn');
    if (btn) { btn.disabled = true; btn.textContent = '⏳ Running pipeline…'; }
    out.innerHTML = '<div class="running"><div class="spinner"></div><p>Agents are working…</p></div>';
    try {
        const c = await api(`/api/cases/process/${emailId}`, { method: 'POST' });
        renderCase(c);
    } catch (err) {
        out.innerHTML = `<p class="error">Pipeline failed: ${esc(err.message)}</p>`;
    } finally {
        if (btn) { btn.disabled = false; btn.textContent = '▶ Run intake & origination pipeline'; }
    }
}

function badge(cls) {
    const map = { Hot: 'b-hot', Warm: 'b-warm', Cold: 'b-cold', Spam: 'b-spam' };
    return `<span class="badge ${map[cls] || ''}">${esc(cls)}</span>`;
}

function renderCase(c) {
    const x = c.extraction, t = c.triage, r = c.research, rep = c.report;
    const steps = (c.agentSteps || []).map(s =>
        `<li><span class="step-agent">${esc(s.agent)}</span><span class="step-engine ${s.engine}">${esc(s.engine)}</span><span class="step-sum">${esc(s.summary)}</span></li>`).join('');

    const drivers = (x.opportunity.drivers || []).map(d => `<li>${esc(d)}</li>`).join('') || '<li class="muted">none extracted</li>';
    const factors = (t.factors || []).map(f =>
        `<div class="factor"><div class="f-bar" style="width:${Math.min(100, f.points * 3)}%"></div><span class="f-name">${esc(f.name)}</span><span class="f-pts">${f.points}</span><span class="f-detail">${esc(f.detail)}</span></div>`).join('');
    const signals = (r.demandSignals || []).map(s =>
        `<li><span class="sig-strength s-${esc((s.strength||'').toLowerCase())}">${esc(s.strength)}</span> <strong>${esc(s.signal)}</strong><br><span class="muted">${esc(s.implication)} <em>(${esc(s.source)})</em></span></li>`).join('') || '<li class="muted">none</li>';
    const actions = (r.recommendedActions || []).map(a => `<li>${esc(a)}</li>`).join('');

    out_html(c, x, t, r, rep, steps, drivers, factors, signals, actions);
}

function out_html(c, x, t, r, rep, steps, drivers, factors, signals, actions) {
    $('#pipeline').innerHTML = `
    <div class="result-head">
        <div>${badge(t.classification)} <span class="score">${t.score}<span class="score-max">/100</span></span></div>
        <div class="engine-tag ${c.engine}">via ${esc(c.engine)} engine</div>
    </div>

    <ol class="agent-steps">${steps}</ol>

    <div class="stage">
        <h3>1 · Extracted records <span class="conf">confidence ${Math.round((x.confidence||0)*100)}%</span></h3>
        <div class="records">
            <div class="rec"><div class="rec-title">🏢 Account</div>
                <dl><dt>Name</dt><dd>${esc(x.account.name)}</dd>
                <dt>Industry</dt><dd>${esc(x.account.industry || '—')}</dd>
                <dt>Size</dt><dd>${esc(x.account.employeeBand || '—')}</dd>
                <dt>Revenue</dt><dd>${esc(x.account.annualRevenueBand || '—')}</dd>
                <dt>Country</dt><dd>${esc(x.account.country || '—')}</dd></dl>
            </div>
            <div class="rec"><div class="rec-title">👤 Lead</div>
                <dl><dt>Name</dt><dd>${esc(x.lead.fullName)}</dd>
                <dt>Title</dt><dd>${esc(x.lead.title || '—')} <span class="muted">(${esc(x.lead.seniority||'—')})</span></dd>
                <dt>Email</dt><dd>${esc(x.lead.email || '—')}</dd>
                <dt>Phone</dt><dd>${esc(x.lead.phone || '—')}</dd>
                <dt>Decision maker</dt><dd>${x.lead.isDecisionMaker ? '✅ Yes' : '— Unclear'}</dd></dl>
            </div>
            <div class="rec"><div class="rec-title">💼 Opportunity</div>
                <dl><dt>Name</dt><dd>${esc(x.opportunity.name)}</dd>
                <dt>Interest</dt><dd>${esc(x.opportunity.productInterest || '—')}</dd>
                <dt>Est. value</dt><dd>${money(x.opportunity.estimatedValue, x.opportunity.currency)}</dd>
                <dt>Timeline</dt><dd>${esc(x.opportunity.timeline || '—')}</dd>
                <dt>Budget</dt><dd>${esc(x.opportunity.budgetStatus || '—')}</dd></dl>
                <div class="drivers"><span class="muted">Drivers</span><ul>${drivers}</ul></div>
            </div>
        </div>
    </div>

    <div class="stage">
        <h3>2 · Triage &amp; classification</h3>
        <div class="triage-meta">Routed to <strong>${esc(t.routedTo)}</strong> · SLA <strong>${esc(t.slaTarget)}</strong></div>
        <div class="factors">${factors}</div>
        <p class="reco">💡 ${esc(t.recommendation)}</p>
    </div>

    <div class="stage">
        <h3>3 · Lead research &amp; demand signals</h3>
        <p>${esc(r.companyOverview)}</p>
        <ul class="signals">${signals}</ul>
        <p class="fit"><strong>Fit:</strong> ${esc(r.fitAssessment)}</p>
        ${actions ? `<div class="actions"><span class="muted">Recommended actions</span><ol>${actions}</ol></div>` : ''}
    </div>

    <div class="stage">
        <h3>4 · Origination study <a class="dl" href="/api/cases/${esc(c.caseId)}/report" target="_blank">⬇ download .md</a></h3>
        <div class="study-summary"><strong>Disposition: ${esc(rep.disposition)}</strong><br>${esc(rep.executiveSummary)}</div>
        <details class="study-full"><summary>View full report</summary><pre class="study-md">${esc(rep.generatedMarkdown)}</pre></details>
    </div>`;
}

// -------- Compose modal --------
function wireCompose() {
    const modal = $('#composeModal');
    const open = () => modal.classList.remove('hidden');
    const close = () => modal.classList.add('hidden');
    $('#composeBtn').addEventListener('click', open);
    $('#composeCancel').addEventListener('click', close);
    $('#composeRun').addEventListener('click', async () => {
        const email = {
            fromName: $('#cFromName').value.trim(),
            from: $('#cFrom').value.trim() || 'prospect@example.com',
            subject: $('#cSubject').value.trim() || '(no subject)',
            body: $('#cBody').value.trim()
        };
        if (!email.body) { alert('Please enter an email body.'); return; }
        try {
            const saved = await api('/api/emails', {
                method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(email)
            });
            close();
            await loadInbox();
            await selectEmail(saved.id);
            await runPipeline(saved.id);
        } catch (err) { alert('Failed: ' + err.message); }
    });
}

// -------- init --------
document.addEventListener('DOMContentLoaded', () => {
    loadInbox();
    wireCompose();
});
