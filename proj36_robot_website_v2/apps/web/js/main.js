/* ============================================================
   Cogsworth Robotics 2.0 — progressive enhancement
   The page is fully readable/usable WITHOUT this script.
   ES5-safe, single IIFE, no dependencies.
   ============================================================ */
(function () {
  "use strict";

  var doc = document;
  var root = doc.documentElement;
  var reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  function $(sel, ctx) { return (ctx || doc).querySelector(sel); }
  function $all(sel, ctx) { return Array.prototype.slice.call((ctx || doc).querySelectorAll(sel)); }

  /* ---- Footer year ---- */
  var yearEl = doc.getElementById("year");
  if (yearEl) yearEl.textContent = String(new Date().getFullYear());

  /* ---- Theme toggle (persisted + system default) ---- */
  var THEME_KEY = "cogsworth-theme";
  var themeToggle = doc.getElementById("themeToggle");

  function storedTheme() {
    try { return window.localStorage.getItem(THEME_KEY); } catch (e) { return null; }
  }
  function saveTheme(t) {
    try { window.localStorage.setItem(THEME_KEY, t); } catch (e) {}
  }
  function applyTheme(theme) {
    root.setAttribute("data-theme", theme);
    if (themeToggle) {
      var isLight = theme === "light";
      themeToggle.setAttribute("aria-pressed", isLight ? "true" : "false");
      themeToggle.setAttribute("aria-label", isLight ? "Switch to dark theme" : "Switch to light theme");
    }
  }

  // Initial theme: stored > system preference > dark default (already on <html>).
  var initial = storedTheme();
  if (!initial) {
    initial = window.matchMedia && window.matchMedia("(prefers-color-scheme: light)").matches ? "light" : "dark";
  }
  applyTheme(initial);

  if (themeToggle) {
    themeToggle.addEventListener("click", function () {
      var next = root.getAttribute("data-theme") === "light" ? "dark" : "light";
      applyTheme(next);
      saveTheme(next);
    });
  }

  /* ---- Mobile nav toggle ---- */
  var navToggle = doc.getElementById("navToggle");
  var nav = doc.getElementById("primaryNav");

  function closeNav() {
    if (!navToggle || !nav) return;
    navToggle.setAttribute("aria-expanded", "false");
    navToggle.setAttribute("aria-label", "Open menu");
    nav.classList.remove("is-open");
  }
  function openNav() {
    if (!navToggle || !nav) return;
    navToggle.setAttribute("aria-expanded", "true");
    navToggle.setAttribute("aria-label", "Close menu");
    nav.classList.add("is-open");
  }

  if (navToggle && nav) {
    navToggle.addEventListener("click", function () {
      var expanded = navToggle.getAttribute("aria-expanded") === "true";
      if (expanded) closeNav(); else openNav();
    });
    nav.addEventListener("click", function (e) {
      var t = e.target;
      if (t && t.classList && t.classList.contains("nav-link")) closeNav();
    });
    doc.addEventListener("keydown", function (e) {
      if (e.key === "Escape" || e.keyCode === 27) closeNav();
    });
    var mq = window.matchMedia("(min-width: 880px)");
    var onChange = function () { if (mq.matches) closeNav(); };
    if (mq.addEventListener) mq.addEventListener("change", onChange);
    else if (mq.addListener) mq.addListener(onChange);
  }

  /* ---- Scroll reveal (graceful: visible if unsupported/JS off) ---- */
  var revealEls = $all(".reveal");
  if ("IntersectionObserver" in window && revealEls.length && !reduceMotion) {
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

  /* ---- Animated count-up for hero stats ---- */
  var counters = $all("[data-count]");
  function animateCount(el) {
    var target = parseFloat(el.getAttribute("data-count"));
    var suffix = el.getAttribute("data-suffix") || "";
    if (isNaN(target)) return;
    if (reduceMotion) { el.textContent = String(target) + suffix; return; }
    var dur = 1100, start = null;
    function tick(ts) {
      if (start === null) start = ts;
      var p = Math.min((ts - start) / dur, 1);
      var eased = 1 - Math.pow(1 - p, 3);
      el.textContent = String(Math.round(target * eased)) + suffix;
      if (p < 1) requestAnimationFrame(tick);
      else el.textContent = String(target) + suffix;
    }
    requestAnimationFrame(tick);
  }
  if (counters.length) {
    if ("IntersectionObserver" in window) {
      var countObs = new IntersectionObserver(function (entries, obs) {
        entries.forEach(function (entry) {
          if (entry.isIntersecting) { animateCount(entry.target); obs.unobserve(entry.target); }
        });
      }, { threshold: 0.5 });
      counters.forEach(function (el) { countObs.observe(el); });
    } else {
      counters.forEach(animateCount);
    }
  }

  /* ---- Active nav link based on visible section ---- */
  var sections = $all("main section[id]");
  var navLinks = $all(".primary-nav .nav-link");
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
    var sectionObs = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (entry.isIntersecting) setActive(entry.target.id);
      });
    }, { rootMargin: "-45% 0px -50% 0px", threshold: 0 });
    sections.forEach(function (s) { sectionObs.observe(s); });
  }

  /* ---- Smooth-scroll + focus management for anchor links ---- */
  doc.addEventListener("click", function (e) {
    var link = e.target && e.target.closest ? e.target.closest('a[href^="#"]') : null;
    if (!link) return;
    var hash = link.getAttribute("href");
    if (!hash || hash === "#") return;
    var target = doc.querySelector(hash);
    if (!target) return;
    e.preventDefault();
    target.scrollIntoView({ behavior: reduceMotion ? "auto" : "smooth", block: "start" });
    target.setAttribute("tabindex", "-1");
    if (target.focus) target.focus({ preventScroll: true });
    if (history.replaceState) history.replaceState(null, "", hash);
  });

  /* ---- Robot filter chips ---- */
  var chips = $all(".chip");
  var robotCards = $all("#robotGrid .robot-card");
  var gridEmpty = doc.getElementById("gridEmpty");
  function applyFilter(filter) {
    var shown = 0;
    robotCards.forEach(function (card) {
      var match = filter === "all" || card.getAttribute("data-category") === filter;
      if (match) { card.classList.remove("is-hidden"); shown++; }
      else { card.classList.add("is-hidden"); }
    });
    if (gridEmpty) gridEmpty.hidden = shown !== 0;
  }
  chips.forEach(function (chip) {
    chip.addEventListener("click", function () {
      chips.forEach(function (c) { c.classList.remove("is-active"); c.setAttribute("aria-pressed", "false"); });
      chip.classList.add("is-active");
      chip.setAttribute("aria-pressed", "true");
      applyFilter(chip.getAttribute("data-filter") || "all");
    });
  });

  /* ---- Build your bot (configurator) ---- */
  var builderForm = doc.getElementById("builderForm");
  if (builderForm) {
    var sumModel = doc.getElementById("sumModel");
    var sumPrice = doc.getElementById("sumPrice");
    var sumBattery = doc.getElementById("sumBattery");
    var summaryLines = doc.getElementById("summaryLines");

    var MODEL_LABEL = { helpa: "Helpa", labbie: "Labbie", rover: "Rover-X", aero: "Aero" };
    var ADDON_LABEL = { battery: "Extended battery", voice: "Voice pack", care: "Care plan (1yr)" };

    function money(n) { return "$" + n.toLocaleString("en-US"); }

    function recalc() {
      var modelInput = builderForm.querySelector('input[name="model"]:checked');
      var price = 0, battery = 0, modelKey = "helpa";
      if (modelInput) {
        modelKey = modelInput.value;
        price += parseInt(modelInput.getAttribute("data-price"), 10) || 0;
        battery += parseInt(modelInput.getAttribute("data-battery"), 10) || 0;
      }
      var lines = [];
      lines.push({ label: MODEL_LABEL[modelKey] || modelKey, value: money(parseInt(modelInput ? modelInput.getAttribute("data-price") : 0, 10) || 0) });

      var addons = $all('input[name="addon"]:checked', builderForm);
      addons.forEach(function (a) {
        var p = parseInt(a.getAttribute("data-price"), 10) || 0;
        var b = parseInt(a.getAttribute("data-battery"), 10) || 0;
        price += p; battery += b;
        lines.push({ label: ADDON_LABEL[a.value] || a.value, value: "+" + money(p) });
      });

      if (sumModel) sumModel.textContent = MODEL_LABEL[modelKey] || modelKey;
      if (sumPrice) sumPrice.textContent = money(price);
      if (sumBattery) sumBattery.textContent = battery + "h" + (modelKey === "aero" ? "*" : "");

      if (summaryLines) {
        summaryLines.innerHTML = "";
        lines.forEach(function (ln) {
          var li = doc.createElement("li");
          var s1 = doc.createElement("span"); s1.textContent = ln.label;
          var s2 = doc.createElement("span"); s2.textContent = ln.value;
          li.appendChild(s1); li.appendChild(s2);
          summaryLines.appendChild(li);
        });
      }
    }

    builderForm.addEventListener("change", recalc);
    recalc();
  }

  /* ---- FAQ accordion (accessible) ---- */
  var faqTriggers = $all(".faq-trigger");
  faqTriggers.forEach(function (trigger) {
    trigger.addEventListener("click", function () {
      var expanded = trigger.getAttribute("aria-expanded") === "true";
      var panelId = trigger.getAttribute("aria-controls");
      var panel = panelId ? doc.getElementById(panelId) : null;
      trigger.setAttribute("aria-expanded", expanded ? "false" : "true");
      if (panel) panel.hidden = expanded;
    });
  });

  /* ---- Contact form: client-side validation + inline success ---- */
  var form = doc.getElementById("contactForm");
  var success = doc.getElementById("formSuccess");

  function fieldWrap(input) { return input ? input.closest(".field") : null; }
  function errorEl(id) { return doc.querySelector('[data-error-for="' + id + '"]'); }
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
        var firstBad = form.querySelector(".has-error input, .has-error textarea");
        if (firstBad) firstBad.focus();
        return;
      }

      form.reset();
      if (success) {
        success.hidden = false;
        if (success.focus) success.focus();
      }
    });
  }

  /* ---- Back-to-top + scroll progress ---- */
  var toTop = doc.getElementById("toTop");
  var progress = doc.getElementById("scrollProgress");
  var header = doc.getElementById("top");

  function onScroll() {
    var scrollTop = window.pageYOffset || doc.documentElement.scrollTop || 0;
    var height = doc.documentElement.scrollHeight - window.innerHeight;
    var pct = height > 0 ? (scrollTop / height) * 100 : 0;
    if (progress) progress.style.width = pct + "%";
    if (toTop) {
      if (scrollTop > 420) { toTop.hidden = false; toTop.classList.add("is-visible"); }
      else { toTop.classList.remove("is-visible"); toTop.hidden = true; }
    }
    if (header) {
      if (scrollTop > 8) header.classList.add("is-shrunk");
      else header.classList.remove("is-shrunk");
    }
  }
  var ticking = false;
  window.addEventListener("scroll", function () {
    if (!ticking) {
      window.requestAnimationFrame(function () { onScroll(); ticking = false; });
      ticking = true;
    }
  }, { passive: true });
  onScroll();

  if (toTop) {
    toTop.addEventListener("click", function () {
      window.scrollTo({ top: 0, behavior: reduceMotion ? "auto" : "smooth" });
    });
  }
})();
