// Sentinel Underwriting — Submission Desk SPA (vanilla JS, no framework).
"use strict";
const $ = (s, r = document) => r.querySelector(s);
const $$ = (s, r = document) => Array.from(r.querySelectorAll(s));
const esc = (s) => String(s ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
const money = (v) => v == null ? "—" : (v >= 1e9 ? "$" + (v / 1e9).toFixed(2) + "B" : v >= 1e6 ? "$" + (v / 1e6).toFixed(2) + "M" : v >= 1e3 ? "$" + (v / 1e3).toFixed(1) + "K" : "$" + Math.round(v));
const num = (v) => v == null ? "—" : Number(v).toLocaleString();
const api = async (path, opts) => { const r = await fetch(path, opts); if (!r.ok) throw new Error((await r.text().catch(() => "")) || r.statusText); return r.json(); };

const state = { inbox: [], cases: [], selectedEmail: null, currentCase: null };

function toast(msg) {
  const t = $("#toast"); t.textContent = msg; t.classList.remove("hidden");
  clearTimeout(toast._t); toast._t = setTimeout(() => t.classList.add("hidden"), 2600);
}

// ---------- navigation ----------
const VIEW_META = {
  desk: ["Submission Desk", "Broker submissions triaged through the multi-agent underwriting pipeline."],
  records: ["Risk Records", "Structured producer, insured and risk-submission records extracted from the email."],
  triage: ["Appetite & Triage", "Appetite decision, risk/fit scoring, referral triggers and desk routing."],
  research: ["Exposure Research", "Inbound demand and exposure signals captured by the Risk Research agent."],
  study: ["Underwriting Study", "Executive risk study with recommendation, pricing, conditions and next actions."]
};
function showView(v) {
  $$(".nav-item").forEach(b => b.classList.toggle("active", b.dataset.view === v));
  $$(".view").forEach(s => s.classList.add("hidden"));
  $("#view-" + v).classList.remove("hidden");
  $("#viewTitle").textContent = VIEW_META[v][0];
  $("#viewSubtitle").textContent = VIEW_META[v][1];
  if (v !== "desk" && !state.currentCase) {
    $("#" + v + "Body").innerHTML = `<div class="card"><div class="empty">Run a submission from the Submission Desk to populate this view.</div></div>`;
  }
}
$$(".nav-item").forEach(b => b.addEventListener("click", () => showView(b.dataset.view)));

function setPipeline(stage) {
  $$("#pipeline .stage-pill").forEach(p => p.classList.toggle("on", Number(p.dataset.stage) <= stage));
}

// ---------- health ----------
async function loadHealth() {
  try {
    const h = await api("/api/health");
    const b = $("#engineBadge");
    b.textContent = "● " + (h.engine === "foundry" ? "Foundry agents" : "offline engine");
    b.classList.add(h.engine === "foundry" ? "foundry" : "offline");
  } catch { /* ignore */ }
}

// ---------- inbox ----------
async function loadInbox() {
  state.inbox = await api("/api/inbox");
  $("#inboxCount").textContent = state.inbox.length;
  const box = $("#inbox");
  box.innerHTML = state.inbox.map(m => `
    <div class="mail" data-id="${esc(m.id)}">
      <div class="m-top">
        <span class="m-from">${esc(m.fromName || m.from)}</span>
        <span class="m-chan">${esc(m.channel || "email")}</span>
      </div>
      <div class="m-sub">${esc(m.subject)}</div>
      <div class="m-snip">${esc(m.body)}</div>
    </div>`).join("");
  $$(".mail", box).forEach(el => el.addEventListener("click", () => selectEmail(el.dataset.id)));
}

function selectEmail(id) {
  const m = state.inbox.find(x => x.id === id);
  if (!m) return;
  state.selectedEmail = m;
  $$(".mail").forEach(el => el.classList.toggle("sel", el.dataset.id === id));
  $("#selectedPreview").classList.remove("muted");
  $("#selectedPreview").innerHTML = `<strong>${esc(m.subject)}</strong><br><span class="muted">${esc(m.fromName || "")} &lt;${esc(m.from)}&gt; · ${esc(m.channel)}</span><br><br>${esc(m.body).slice(0, 280)}${m.body.length > 280 ? "…" : ""}`;
  $("#runSelected").classList.remove("hidden");
}

// ---------- run pipeline ----------
async function runSelected() {
  if (!state.selectedEmail) return;
  await runAndShow(() => api(`/api/cases/from-inbox/${encodeURIComponent(state.selectedEmail.id)}`, { method: "POST" }));
}
async function runAdhoc() {
  const from = $("#fldFrom").value.trim(), subject = $("#fldSubject").value.trim(), body = $("#fldBody").value.trim();
  if (!from && !body) { toast("Add a From or a submission body first."); return; }
  await runAndShow(() => api("/api/cases", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ from, subject, body, channel: "email" }) }));
}
async function runAndShow(fn) {
  toast("Running underwriting pipeline…");
  try {
    const c = await fn();
    state.currentCase = c;
    await loadCases();
    renderAll(c);
    showView("records");
    toast(`Case ${c.reference} · ${c.triage.appetiteClass}`);
  } catch (e) { toast("Pipeline error: " + e.message); }
}

// ---------- cases queue ----------
function pBadge(p) { return `<span class="bdg b-${(p || "p3").toLowerCase()}">${esc(p)}</span>`; }
function appetiteBadge(a) {
  const cls = a === "In Appetite" ? "b-good" : a === "Decline" || a === "Out of Appetite" ? "b-bad" : "b-warn";
  return `<span class="bdg ${cls}">${esc(a)}</span>`;
}
async function loadCases() {
  state.cases = await api("/api/cases");
  const q = $("#queue");
  if (!state.cases.length) { q.innerHTML = `<div class="empty">No submissions processed yet. Run one above or use “Run full inbox”.</div>`; return; }
  q.innerHTML = `
    <div class="qrow qhead">
      <span>Reference</span><span>Insured</span><span>Line</span><span>Appetite</span><span>Risk</span><span>Priority</span><span>Premium</span>
    </div>` + state.cases.map(c => `
    <div class="qrow" data-id="${esc(c.caseId)}">
      <span class="ref">${esc(c.reference)}</span>
      <span><div class="nm">${esc(c.insured || "—")}</div><div class="sub">${esc(c.recommendation)}</div></span>
      <span class="sub">${esc(c.lineOfBusiness || "—")}</span>
      <span>${appetiteBadge(c.appetite)}</span>
      <span class="sub">${num(c.riskScore)}</span>
      <span>${pBadge(c.priority)}</span>
      <span class="sub">${money(c.premium)}</span>
    </div>`).join("");
  $$(".qrow:not(.qhead)", q).forEach(el => el.addEventListener("click", () => openCase(el.dataset.id)));
}
async function openCase(id) {
  try {
    const c = await api(`/api/cases/${encodeURIComponent(id)}`);
    state.currentCase = c; renderAll(c); showView("records");
  } catch (e) { toast("Could not open case: " + e.message); }
}

// ---------- renderers ----------
function confBar(v) { return `<div class="conf"><i style="width:${Math.round((v || 0) * 100)}%"></i></div>`; }
function detailHead(c) {
  return `<div class="detail-head">
    <div><h2>${esc(c.records.insured?.companyName || "Unnamed insured")}</h2>
      <div class="ref">${esc(c.reference)} · <span class="muted">${esc(c.records.submission?.lineOfBusiness || "")} · ${esc(c.engine)} engine</span></div></div>
    <div>${appetiteBadge(c.triage.appetiteClass)} ${pBadge(c.triage.priority)}</div>
  </div>`;
}

function renderRecords(c) {
  const p = c.records.producer, ins = c.records.insured, s = c.records.submission;
  const kv = (k, v) => `<div class="kv"><span class="k">${k}</span><span class="v">${v}</span></div>`;
  $("#recordsBody").innerHTML = `<div class="card">${detailHead(c)}
    <div class="entity-grid">
      <div class="entity">
        <h3>Producer · Lead</h3>
        ${kv("Contact", esc(p.contactName || "—"))}
        ${kv("Title", esc(p.title || "—"))}
        ${kv("Brokerage", esc(p.brokerage || "—"))}
        ${kv("Tier", esc(p.brokerTier))}
        ${kv("Appointed", p.appointed ? "Yes" : "No")}
        ${kv("Email", esc(p.email || "—"))}
        ${confBar(p.confidence)}
      </div>
      <div class="entity">
        <h3>Insured · Account</h3>
        ${kv("Company", esc(ins.companyName || "—"))}
        ${kv("Industry", esc(ins.industry || "—"))}
        ${kv("SIC division", esc(ins.sicDivision || "—"))}
        ${kv("HQ", esc(ins.headquarters || ins.country || "—"))}
        ${kv("Employees", num(ins.employeeCount))}
        ${kv("Locations", num(ins.locationCount))}
        ${kv("TIV", money(ins.totalInsurableValue))}
        ${kv("Years trading", num(ins.yearsInBusiness))}
        ${confBar(ins.confidence)}
      </div>
      <div class="entity">
        <h3>Risk Submission · Opportunity</h3>
        ${kv("Line", esc(s.lineOfBusiness || "—"))}
        ${kv("Coverage", esc(s.coverageType || "—"))}
        ${kv("Type", esc(s.submissionType))}
        ${kv("Requested limit", money(s.requestedLimit))}
        ${kv("Deductible", money(s.deductible))}
        ${kv("Est. premium", money(s.estimatedAnnualPremium))}
        ${kv("Effective", s.effectiveDate ? esc(String(s.effectiveDate).slice(0, 10)) : "—")}
        ${kv("Incumbents", esc((s.incumbentCarriers || []).join(", ") || "—"))}
        ${confBar(s.confidence)}
      </div>
    </div>
    ${(c.records.missingForUnderwriting || []).length ? `<div class="section"><h3>Missing for underwriting</h3><div class="chips">${c.records.missingForUnderwriting.map(m => `<span class="chip warn">${esc(m)}</span>`).join("")}</div></div>` : ""}
    ${renderTrace(c)}
  </div>`;
}

function ring(v, color) { return `<div class="ring" style="--v:${v || 0};--c:${color}"><div>${num(v)}</div></div>`; }

function renderTriage(c) {
  const t = c.triage;
  const riskColor = t.riskScore >= 70 ? "var(--bad)" : t.riskScore >= 45 ? "var(--warn)" : "var(--good)";
  const fitColor = t.fitScore >= 65 ? "var(--good)" : t.fitScore >= 45 ? "var(--warn)" : "var(--bad)";
  $("#triageBody").innerHTML = `<div class="card">${detailHead(c)}
    <div class="entity-grid">
      <div class="entity"><h3>Risk score</h3><div class="gauge">${ring(t.riskScore, riskColor)}<div class="score-meta">Higher = more scrutiny.<br>Hazard-weighted.</div></div></div>
      <div class="entity"><h3>Fit / desirability</h3><div class="gauge">${ring(t.fitScore, fitColor)}<div class="score-meta">Account attractiveness.</div></div></div>
      <div class="entity"><h3>Routing</h3>
        <div class="kv"><span class="k">Appetite</span><span class="v">${appetiteBadge(t.appetiteClass)}</span></div>
        <div class="kv"><span class="k">Action</span><span class="v">${esc(t.recommendation)}</span></div>
        <div class="kv"><span class="k">Priority</span><span class="v">${pBadge(t.priority)}</span></div>
        <div class="kv"><span class="k">SLA</span><span class="v">${num(t.slaHours)}h</span></div>
        <div class="kv"><span class="k">Queue</span><span class="v">${esc(t.routingQueue || "—")}</span></div>
        <div class="kv"><span class="k">Desk</span><span class="v">${esc(t.assignedDesk || "—")}</span></div>
      </div>
    </div>
    ${(t.referralTriggers || []).length ? `<div class="section"><h3>Referral triggers</h3><div class="chips">${t.referralTriggers.map(x => `<span class="chip warn">${esc(x)}</span>`).join("")}</div></div>` : ""}
    ${(t.riskFlags || []).length ? `<div class="section"><h3>Risk flags</h3><div class="chips">${t.riskFlags.map(x => `<span class="chip bad">${esc(x)}</span>`).join("")}</div></div>` : ""}
    <div class="section"><h3>Rationale</h3><p>${esc(t.rationale)}</p></div>
  </div>`;
}

function renderResearch(c) {
  const r = c.research;
  const intentColor = r.intentScore >= 70 ? "var(--good)" : r.intentScore >= 45 ? "var(--warn)" : "var(--p3)";
  $("#researchBody").innerHTML = `<div class="card">${detailHead(c)}
    <div class="entity-grid">
      <div class="entity" style="grid-column:span 2"><h3>Account overview</h3><p class="section" style="border:none;margin:0;padding:0">${esc(r.accountOverview)}</p>
        ${(r.exposureHighlights || []).length ? `<div class="chips">${r.exposureHighlights.map(h => `<span class="chip">${esc(h)}</span>`).join("")}</div>` : ""}</div>
      <div class="entity"><h3>Binding intent</h3><div class="gauge">${ring(r.intentScore, intentColor)}<div class="score-meta">${esc(r.intentBand || "")}</div></div></div>
    </div>
    <div class="section"><h3>Exposure &amp; demand signals</h3>
      ${(r.signals || []).map(s => `
        <div class="sig">
          <div class="s-top"><span class="s-cat">${esc(s.category)}</span> <span class="bdg ${s.sentiment === "Adverse" ? "b-bad" : s.sentiment === "Positive" ? "b-good" : "b-neutral"}">${esc(s.sentiment)} · ${esc(s.impact)}</span></div>
          <div class="s-head">${esc(s.headline)}</div>
          <div class="s-detail">${esc(s.detail)}</div>
        </div>`).join("") || `<p class="muted">No signals.</p>`}
    </div>
    ${(r.recommendedQuestions || []).length ? `<div class="section"><h3>Recommended questions to the broker</h3><ul class="list">${r.recommendedQuestions.map(q => `<li>${esc(q)}</li>`).join("")}</ul></div>` : ""}
  </div>`;
}

function renderStudy(c) {
  const s = c.study, t = c.triage;
  const recoCls = s.overallRecommendation === "Bind" ? "b-good" : s.overallRecommendation === "Decline" ? "b-bad" : "b-warn";
  $("#studyBody").innerHTML = `<div class="card">${detailHead(c)}
    <div class="reco-banner">
      <span class="bdg ${recoCls}" style="font-size:13px">${esc(s.overallRecommendation)}</span>
      <span class="big">${esc(s.title)}</span>
      <span class="prem"><div class="muted">Indicated premium</div><b>${money(s.indicatedPremium)}</b></span>
    </div>
    <div class="section" style="border:none;margin:0;padding:0"><h3>Executive summary</h3><p>${esc(s.executiveSummary)}</p></div>
    <div class="section"><h3>Pricing rationale</h3><p>${esc(s.pricingRationale)}</p></div>
    ${(s.keyRiskFlags || []).length ? `<div class="section"><h3>Key risk flags</h3><div class="chips">${s.keyRiskFlags.map(x => `<span class="chip bad">${esc(x)}</span>`).join("")}</div></div>` : ""}
    ${(s.recommendedConditions || []).length ? `<div class="section"><h3>Recommended conditions</h3><ul class="list">${s.recommendedConditions.map(x => `<li>${esc(x)}</li>`).join("")}</ul></div>` : ""}
    ${(s.exclusions || []).length ? `<div class="section"><h3>Exclusions</h3><div class="chips">${s.exclusions.map(x => `<span class="chip warn">${esc(x)}</span>`).join("")}</div></div>` : ""}
    ${(s.sections || []).map(sec => `<div class="section"><h3>${esc(sec.heading)}</h3><p>${esc(sec.body)}</p></div>`).join("")}
    ${(s.nextActions || []).length ? `<div class="section"><h3>Next actions</h3><ul class="list">${s.nextActions.map(x => `<li>${esc(x)}</li>`).join("")}</ul></div>` : ""}
  </div>`;
}

function renderTrace(c) {
  if (!(c.trace || []).length) return "";
  return `<div class="section"><h3>Agent trace</h3><div class="trace">${c.trace.map(t => `
    <div class="t-item"><span class="t-dot ${t.engine === "offline" ? "offline" : ""}"></span>
      <div><div class="t-stage">${esc(t.stage)} <span class="muted">· ${esc(t.agent)}</span></div><div class="t-sum">${esc(t.summary)}</div></div>
      <span class="t-meta">${esc(t.engine)} · ${num(t.durationMs)}ms</span>
    </div>`).join("")}</div></div>`;
}

function renderAll(c) {
  setPipeline(4);
  renderRecords(c); renderTriage(c); renderResearch(c); renderStudy(c);
}

// ---------- demo / reset ----------
async function runDemo() {
  toast("Processing full inbox…");
  try { const r = await api("/api/cases/run-demo", { method: "POST" }); await loadCases(); toast(`Processed ${r.processed} submissions (${r.engine}).`); }
  catch (e) { toast("Demo failed: " + e.message); }
}
async function resetCases() {
  try { await api("/api/cases", { method: "DELETE" }); state.currentCase = null; await loadCases(); ["records", "triage", "research", "study"].forEach(v => $("#" + v + "Body").innerHTML = ""); toast("Cleared."); }
  catch (e) { toast("Reset failed: " + e.message); }
}

// ---------- boot ----------
$("#runSelected").addEventListener("click", runSelected);
$("#runAdhoc").addEventListener("click", runAdhoc);
$("#runDemo").addEventListener("click", runDemo);
$("#resetCases").addEventListener("click", resetCases);

(async function init() {
  setPipeline(0);
  await Promise.all([loadHealth(), loadInbox(), loadCases()]);
})();
