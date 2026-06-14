/* ============================================================
   Cogsworth Robotics — progressive enhancement
   The page is fully readable/usable WITHOUT this script.
   ============================================================ */
(function () {
  "use strict";

  /* ---- Footer year ---- */
  var yearEl = document.getElementById("year");
  if (yearEl) yearEl.textContent = String(new Date().getFullYear());

  /* ---- Mobile nav toggle ---- */
  var toggle = document.getElementById("navToggle");
  var nav = document.getElementById("primaryNav");

  function closeNav() {
    if (!toggle || !nav) return;
    toggle.setAttribute("aria-expanded", "false");
    toggle.setAttribute("aria-label", "Open menu");
    nav.classList.remove("is-open");
  }
  function openNav() {
    if (!toggle || !nav) return;
    toggle.setAttribute("aria-expanded", "true");
    toggle.setAttribute("aria-label", "Close menu");
    nav.classList.add("is-open");
  }

  if (toggle && nav) {
    toggle.addEventListener("click", function () {
      var expanded = toggle.getAttribute("aria-expanded") === "true";
      if (expanded) closeNav(); else openNav();
    });

    // Close the menu after clicking a link (mobile).
    nav.addEventListener("click", function (e) {
      var t = e.target;
      if (t && t.classList && t.classList.contains("nav-link")) closeNav();
    });

    // Close on Escape.
    document.addEventListener("keydown", function (e) {
      if (e.key === "Escape") closeNav();
    });

    // Reset state if resized up to desktop.
    var mq = window.matchMedia("(min-width: 880px)");
    var onChange = function () { if (mq.matches) closeNav(); };
    if (mq.addEventListener) mq.addEventListener("change", onChange);
    else if (mq.addListener) mq.addListener(onChange);
  }

  /* ---- Scroll reveal (graceful: visible if unsupported/JS off) ---- */
  var revealEls = Array.prototype.slice.call(document.querySelectorAll(".reveal"));
  if ("IntersectionObserver" in window && revealEls.length) {
    var revealObs = new IntersectionObserver(function (entries, obs) {
      entries.forEach(function (entry) {
        if (entry.isIntersecting) {
          entry.target.classList.add("is-visible");
          obs.unobserve(entry.target);
        }
      });
    }, { threshold: 0.12, rootMargin: "0px 0px -8% 0px" });
    revealEls.forEach(function (el) { revealObs.observe(el); });
  } else {
    revealEls.forEach(function (el) { el.classList.add("is-visible"); });
  }

  /* ---- Active nav link based on visible section ---- */
  var sections = Array.prototype.slice.call(document.querySelectorAll("main section[id]"));
  var navLinks = Array.prototype.slice.call(document.querySelectorAll(".primary-nav .nav-link"));
  var linkById = {};
  navLinks.forEach(function (a) {
    var id = (a.getAttribute("href") || "").replace("#", "");
    if (id) linkById[id] = a;
  });

  function setActive(id) {
    navLinks.forEach(function (a) { a.classList.remove("is-active"); });
    if (linkById[id]) linkById[id].classList.add("is-active");
  }

  if ("IntersectionObserver" in window && sections.length) {
    var current = null;
    var sectionObs = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (entry.isIntersecting) {
          current = entry.target.id;
          setActive(current);
        }
      });
    }, { rootMargin: "-45% 0px -50% 0px", threshold: 0 });
    sections.forEach(function (s) { sectionObs.observe(s); });
  }

  /* ---- Smooth-scroll + focus management for anchor links ---- */
  document.addEventListener("click", function (e) {
    var link = e.target && e.target.closest ? e.target.closest('a[href^="#"]') : null;
    if (!link) return;
    var hash = link.getAttribute("href");
    if (!hash || hash === "#") return;
    var target = document.querySelector(hash);
    if (!target) return;
    e.preventDefault();
    var reduce = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    target.scrollIntoView({ behavior: reduce ? "auto" : "smooth", block: "start" });
    // Move focus for accessibility without an extra visible jump.
    target.setAttribute("tabindex", "-1");
    target.focus({ preventScroll: true });
    if (history.replaceState) history.replaceState(null, "", hash);
  });

  /* ---- Contact form: client-side validation + inline success ---- */
  var form = document.getElementById("contactForm");
  var success = document.getElementById("formSuccess");

  function fieldWrap(input) { return input ? input.closest(".field") : null; }
  function errorEl(id) { return document.querySelector('[data-error-for="' + id + '"]'); }

  function setError(input, msg) {
    var wrap = fieldWrap(input);
    var err = errorEl(input.id);
    if (wrap) wrap.classList.add("has-error");
    if (err) err.textContent = msg;
    input.setAttribute("aria-invalid", "true");
  }
  function clearError(input) {
    var wrap = fieldWrap(input);
    var err = errorEl(input.id);
    if (wrap) wrap.classList.remove("has-error");
    if (err) err.textContent = "";
    input.removeAttribute("aria-invalid");
  }

  var EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

  if (form) {
    var nameI = form.querySelector("#cf-name");
    var emailI = form.querySelector("#cf-email");
    var msgI = form.querySelector("#cf-message");

    // Live-clear an error once the user fixes the field.
    [nameI, emailI, msgI].forEach(function (input) {
      if (!input) return;
      input.addEventListener("input", function () {
        if (input.value.trim()) clearError(input);
      });
    });

    form.addEventListener("submit", function (e) {
      e.preventDefault();
      var ok = true;
      if (success) success.hidden = true;

      if (!nameI.value.trim()) { setError(nameI, "Please enter your name."); ok = false; }
      else clearError(nameI);

      if (!emailI.value.trim()) { setError(emailI, "Please enter your email."); ok = false; }
      else if (!EMAIL_RE.test(emailI.value.trim())) { setError(emailI, "Please enter a valid email."); ok = false; }
      else clearError(emailI);

      if (!msgI.value.trim()) { setError(msgI, "Please add a short message."); ok = false; }
      else clearError(msgI);

      if (!ok) {
        // Focus the first invalid field.
        var firstBad = form.querySelector(".has-error input, .has-error textarea");
        if (firstBad) firstBad.focus();
        return;
      }

      // No backend (MVP): acknowledge locally and reset.
      form.reset();
      if (success) {
        success.hidden = false;
        success.focus && success.focus();
      }
    });
  }
})();
