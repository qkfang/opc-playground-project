/* Minimal interactivity for multi-page robotics site */

const $ = (id) => document.getElementById(id);
// Fallback for cases where header height cannot be read.
const SCROLL_OFFSET_PX = 140;

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

  // Stats (static-ish — update these to reflect real numbers)
  setText("statRuns", "128");
  setText("statUptime", "97.3%");
  setText("statBuild", "v0.1.0");

  initTelemetryDemo();
  initContactForm();
}

/* --- Telemetry demo (Home page only) --- */

function initTelemetryDemo(){
  const btn = $("runDemo");
  if(btn){
    btn.addEventListener("click", runTelemetryDemo);
  }
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

/* --- Contact form client-side validation (Contact page only) --- */

function initContactForm(){
  const form = $("contactForm");
  if(!form) return;

  form.addEventListener("submit", (e) => {
    let valid = true;

    const name = $("contactName");
    const nameErr = $("nameError");
    if(name && nameErr){
      if(!name.value.trim()){
        nameErr.hidden = false;
        name.setAttribute("aria-invalid", "true");
        valid = false;
      } else {
        nameErr.hidden = true;
        name.removeAttribute("aria-invalid");
      }
    }

    const email = $("contactEmail");
    const emailErr = $("emailError");
    if(email && emailErr){
      const emailOk = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.value.trim());
      if(!emailOk){
        emailErr.hidden = false;
        email.setAttribute("aria-invalid", "true");
        valid = false;
      } else {
        emailErr.hidden = true;
        email.removeAttribute("aria-invalid");
      }
    }

    const msg = $("contactMessage");
    const msgErr = $("messageError");
    if(msg && msgErr){
      if(!msg.value.trim()){
        msgErr.hidden = false;
        msg.setAttribute("aria-invalid", "true");
        valid = false;
      } else {
        msgErr.hidden = true;
        msg.removeAttribute("aria-invalid");
      }
    }

    if(!valid) e.preventDefault();
  });
}

document.addEventListener("DOMContentLoaded", boot);
