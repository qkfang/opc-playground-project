/* Minimal interactivity + timeline + project rendering */

const $ = (id) => document.getElementById(id);
// Fallback for cases where header height cannot be read.
const SCROLL_OFFSET_PX = 140;

/* ── Updates timeline ─────────────────────────────── */

function renderTimeline(items){
  // Use UPDATES from site-data.js when available; fall back to empty array.
  const data = items || (typeof UPDATES !== "undefined" ? UPDATES : []);
  const root = $("timeline");
  const state = $("timelineState");
  if(!root) return;
  try {
    if(!Array.isArray(data)) throw new Error(`Expected updates array, received ${typeof data}`);
    if(data.length === 0){
      root.innerHTML = "";
      if(state) state.textContent = "No updates yet. Check back soon for milestones.";
      return;
    }
    root.innerHTML = data.map(u => `
      <article class="update">
        <div class="update__date">${escapeHtml(u.date)}</div>
        <div>
          <div class="update__title">${escapeHtml(u.title)}</div>
          <p class="update__body">${escapeHtml(u.body)}</p>
        </div>
      </article>
    `).join("");
    if(state) state.hidden = true;
  } catch (err) {
    root.innerHTML = "";
    if(state) state.textContent = "We couldn't load updates right now. Please refresh to try again.";
    console.error(err);
  }
}

/* ── Projects grid ────────────────────────────────── */

const STATUS_LABELS = {
  "active":       "Active",
  "in-progress":  "In Progress",
  "experimental": "Experimental",
  "archived":     "Archived"
};

function renderProjects(items){
  const data = items || (typeof PROJECTS !== "undefined" ? PROJECTS : []);
  const root = $("projectGrid");
  const state = $("projectsState");
  if(!root) return;
  try {
    if(!Array.isArray(data)) throw new Error(`Expected projects array, received ${typeof data}`);
    if(data.length === 0){
      root.innerHTML = "";
      if(state){ state.hidden = false; state.textContent = "No projects yet. Check back soon."; }
      return;
    }
    root.innerHTML = data.map(p => {
      const tags = (p.tags || []).map(t => `<span class="pill">${escapeHtml(t)}</span>`).join("");
      const statusKey = (p.status || "").toLowerCase().replace(/\s+/g, "-");
      const statusLabel = STATUS_LABELS[statusKey] || escapeHtml(p.status || "");
      const links = (p.links || []).map(l =>
        `<a class="link" href="${escapeHtml(l.href)}"${l.href.startsWith("http") ? ' target="_blank" rel="noreferrer"' : ""}>${escapeHtml(l.label)}</a>`
      ).join("");
      return `
        <article class="project-card">
          <div class="project-card__head">
            <h2 class="project-card__title">${escapeHtml(p.title)}</h2>
            <span class="status-badge status-badge--${escapeHtml(statusKey)}">${statusLabel}</span>
          </div>
          <p class="project-card__desc muted">${escapeHtml(p.description)}</p>
          <div class="project-card__tags">${tags}</div>
          <div class="project-card__links">${links}</div>
        </article>
      `;
    }).join("");
    if(state) state.hidden = true;
  } catch (err) {
    root.innerHTML = "";
    if(state){ state.hidden = false; state.textContent = "We couldn't load projects right now. Please refresh to try again."; }
    console.error(err);
  }
}

/* ── Contact form ─────────────────────────────────── */

function initContactForm(){
  const form = $("contactForm");
  if(!form) return;
  form.addEventListener("submit", (e) => {
    e.preventDefault();
    const name    = (form.elements["name"]?.value    || "").trim();
    const email   = (form.elements["email"]?.value   || "").trim();
    const message = (form.elements["message"]?.value || "").trim();
    if(!name || !email || !message){
      const note = $("formNote");
      if(note){ note.hidden = false; note.textContent = "Please fill in all fields before sending."; }
      return;
    }
    const subject = encodeURIComponent(`Message from ${name}`);
    const body    = encodeURIComponent(`From: ${name} <${email}>\n\n${message}`);
    window.location.href = `mailto:you@example.com?subject=${subject}&body=${body}`;
  });
}

/* ── Shared utilities ─────────────────────────────── */

function escapeHtml(s){
  return String(s)
    .replaceAll("&","&amp;")
    .replaceAll("<","&lt;")
    .replaceAll(">","&gt;")
    .replaceAll('"',"&quot;")
    .replaceAll("'","&#039;");
}

function setText(id, value){
  const el = $(id);
  if(el) el.textContent = value;
}

/* ── Boot ─────────────────────────────────────────── */

function boot(){
  setText("year", String(new Date().getFullYear()));

  // Stats (home page only — IDs absent on other pages so safely no-ops)
  setText("statRuns", "128");
  setText("statUptime", "97.3%");
  setText("statBuild", "v0.1.0");

  renderTimeline();
  renderProjects();
  initContactForm();
  initNavState();

  const btn = $("runDemo");
  if(btn){
    btn.addEventListener("click", runTelemetryDemo);
  }
}

function initNavState(){
  const nav = document.querySelector(".nav");
  if(!nav) return;
  const links = Array.from(nav.querySelectorAll('a[href^="#"]'));
  const sections = links
    .map((link) => document.querySelector(link.getAttribute("href")))
    .filter(Boolean);
  if(links.length === 0 || sections.length === 0) return;

  const header = document.querySelector(".header");
  const setActive = () => {
    const scrollOffsetPx = (header?.offsetHeight ?? SCROLL_OFFSET_PX) + 16;
    const y = window.scrollY + scrollOffsetPx;
    let activeId = sections[0].id;
    for (const section of sections) {
      if(section.offsetTop <= y) activeId = section.id;
    }
    for (const link of links) {
      const isActive = link.getAttribute("href") === `#${activeId}`;
      link.classList.toggle("is-active", isActive);
      if(isActive) link.setAttribute("aria-current", "location");
      else link.removeAttribute("aria-current");
    }
  }

  setActive();
  window.addEventListener("scroll", setActive, { passive: true });
}

let demoTimer = null;
function runTelemetryDemo(){
  if(demoTimer){
    clearInterval(demoTimer);
    demoTimer = null;
  }

  const started = performance.now();
  setText("tMode", "Autonomy (sim)");
  setText("tStatus", "Starting…");

  demoTimer = setInterval(() => {
    const t = (performance.now() - started) / 1000;
    const batt = Math.max(62, 99 - t * 1.2);
    const x = (Math.sin(t * 0.7) * 2.1).toFixed(2);
    const y = (Math.cos(t * 0.6) * 1.7).toFixed(2);
    const yaw = ((t * 18) % 360).toFixed(0);

    setText("tBatt", `${batt.toFixed(0)}%`);
    setText("tPose", `x=${x}m, y=${y}m, yaw=${yaw}°`);
    setText("tGoal", t < 4 ? "Acquire map" : t < 10 ? "Navigate to waypoint" : "Dock" );
    setText("tStatus", t < 2 ? "Initializing sensors" : t < 6 ? "Localizing" : t < 12 ? "Navigating" : "Completed run" );

    if(t > 14){
      clearInterval(demoTimer);
      demoTimer = null;
      setText("tMode", "Standby");
      setText("tStatus", "Standby" );
    }
  }, 200);
}

document.addEventListener("DOMContentLoaded", boot);

