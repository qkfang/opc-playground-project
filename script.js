/* Minimal interactivity + content rendering */

const $ = (id) => document.getElementById(id);
// Fallback for cases where header height cannot be read.
const SCROLL_OFFSET_PX = 140;

const siteContent = window.siteContent ?? {};
const projects = Array.isArray(siteContent.projects) ? siteContent.projects : [];
const updates = Array.isArray(siteContent.updates) ? [...siteContent.updates] : [];

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

function setText(id, value) {
  const el = $(id);
  if (el) el.textContent = value;
}

function renderProjects(items = projects) {
  const root = $("projectsGrid");
  if (!root) return;

  if (!Array.isArray(items) || items.length === 0) {
    root.innerHTML = '<article class="project"><p class="muted">No projects published yet.</p></article>';
    return;
  }

  root.innerHTML = items.map((project) => {
    const tags = Array.isArray(project.tags) ? project.tags : [];
    const links = Array.isArray(project.links) ? project.links : [];

    return `
      <article class="project">
        <div class="project__meta">
          <h3>${escapeHtml(project.title)}</h3>
          <span class="status">${escapeHtml(project.status ?? "Planned")}</span>
        </div>
        <p class="muted">${escapeHtml(project.description ?? "")}</p>
        <div class="taglist" aria-label="Project tags">
          ${tags.map((tag) => `<span class="pill">${escapeHtml(tag)}</span>`).join("")}
        </div>
        <div class="project__links">
          ${links.map((link) => `
            <a class="link" href="${escapeHtml(link.url ?? "#")}">${escapeHtml(link.label ?? "Link")}</a>
          `).join("")}
        </div>
      </article>
    `;
  }).join("");
}

function renderTimeline(items = updates) {
  const root = $("timeline");
  const state = $("timelineState");
  if (!root) return;

  try {
    if (!Array.isArray(items)) throw new Error(`Expected updates array, received ${typeof items}`);
    if (items.length === 0) {
      root.innerHTML = "";
      if (state) state.textContent = "No updates yet. Check back soon for milestones.";
      return;
    }

    const ordered = [...items].sort((a, b) => String(b.date ?? "").localeCompare(String(a.date ?? "")));
    root.innerHTML = ordered.map((update) => {
      const highlights = Array.isArray(update.highlights) ? update.highlights : [];
      return `
        <article class="update">
          <div class="update__date">${escapeHtml(update.date ?? "")}</div>
          <div>
            <div class="update__title">${escapeHtml(update.title ?? "")}</div>
            <p class="update__body">${escapeHtml(update.body ?? "")}</p>
            ${highlights.length > 0 ? `
              <ul class="update__highlights">
                ${highlights.map((highlight) => `<li>${escapeHtml(highlight)}</li>`).join("")}
              </ul>
            ` : ""}
          </div>
        </article>
      `;
    }).join("");

    if (state) state.hidden = true;
  } catch (err) {
    root.innerHTML = "";
    if (state) state.textContent = "We couldn’t load updates right now. Please refresh to try again.";
    console.error(err);
  }
}

function initContactForm() {
  const form = $("contactForm");
  if (!form) return;

  form.addEventListener("submit", (event) => {
    event.preventDefault();

    const recipient = form.getAttribute("data-recipient") ?? "hello@opc-project-1.dev";
    const name = $("contactName")?.value.trim() ?? "";
    const email = $("contactEmail")?.value.trim() ?? "";
    const message = $("contactMessage")?.value.trim() ?? "";
    const subject = encodeURIComponent(`OPC Project 1 inquiry${name ? ` from ${name}` : ""}`);
    const body = encodeURIComponent([
      name ? `Name: ${name}` : "",
      email ? `Email: ${email}` : "",
      "",
      message || "Hello, I’d like to connect about your robotics projects."
    ].filter(Boolean).join("\n"));

    window.location.href = `mailto:${recipient}?subject=${subject}&body=${body}`;
  });
}

function boot() {
  setText("year", String(new Date().getFullYear()));
  setText("statProjects", String(projects.length));
  setText("statUpdates", String(updates.length));
  setText("statDeploy", "GitHub Pages");

  renderProjects();
  renderTimeline();
  initNavState();
  initContactForm();

  const btn = $("runDemo");
  if (btn) {
    btn.addEventListener("click", runTelemetryDemo);
  }
}

function initNavState() {
  const nav = document.querySelector(".nav");
  if (!nav) return;

  const links = Array.from(nav.querySelectorAll('a[href^="#"]'));
  const sections = links
    .map((link) => document.querySelector(link.getAttribute("href")))
    .filter(Boolean);
  if (links.length === 0 || sections.length === 0) return;

  const header = document.querySelector(".header");
  const setActive = () => {
    const scrollOffsetPx = (header?.offsetHeight ?? SCROLL_OFFSET_PX) + 16;
    const y = window.scrollY + scrollOffsetPx;
    let activeId = sections[0].id;

    for (const section of sections) {
      if (section.offsetTop <= y) activeId = section.id;
    }

    for (const link of links) {
      const isActive = link.getAttribute("href") === `#${activeId}`;
      link.classList.toggle("is-active", isActive);
      if (isActive) link.setAttribute("aria-current", "location");
      else link.removeAttribute("aria-current");
    }
  };

  setActive();
  window.addEventListener("scroll", setActive, { passive: true });
}

let demoTimer = null;
function runTelemetryDemo() {
  if (demoTimer) {
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
    setText("tGoal", t < 4 ? "Acquire map" : t < 10 ? "Navigate to waypoint" : "Dock");
    setText("tStatus", t < 2 ? "Initializing sensors" : t < 6 ? "Localizing" : t < 12 ? "Navigating" : "Completed run");

    if (t > 14) {
      clearInterval(demoTimer);
      demoTimer = null;
      setText("tMode", "Standby");
      setText("tStatus", "Standby");
    }
  }, 200);
}

document.addEventListener("DOMContentLoaded", boot);
