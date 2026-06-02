/* Minimal interactivity + sample update timeline */

const $ = (id) => document.getElementById(id);
// Fallback for cases where header height cannot be read.
const SCROLL_OFFSET_PX = 140;

const updates = [
  {
    date: "2026-05-30",
    title: "Site scaffolded",
    body: "Landing page, sections, and a simple telemetry demo added. Replace placeholders with your real content."
  },
  {
    date: "2026-05-28",
    title: "Navigation baseline",
    body: "Configured mapping + localization pipeline; started collecting repeatable hallway datasets."
  },
  {
    date: "2026-05-20",
    title: "Hardware bring-up",
    body: "Motor drivers tuned; safety E-stop and current limits verified on the bench."
  }
];

function renderTimeline(items = updates){
  const root = $("timeline");
  const state = $("timelineState");
  if(!root) return;
  try {
    if(!Array.isArray(items)) throw new Error(`Expected updates array, received ${typeof items}`);
    if(items.length === 0){
      root.innerHTML = "";
      if(state) state.textContent = "No updates yet. Check back soon for milestones.";
      return;
    }
    root.innerHTML = items.map(u => `
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
    if(state) state.textContent = "We couldn’t load updates right now. Please refresh to try again.";
    console.error(err);
  }
}

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

function boot(){
  setText("year", String(new Date().getFullYear()));

  // Stats (static-ish)
  setText("statRuns", "128");
  setText("statUptime", "97.3%");
  setText("statBuild", "v0.1.0");

  renderTimeline();
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
