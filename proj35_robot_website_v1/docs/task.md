# Task Breakdown — proj35 Simple Robot Website v1

project_id: proj35
project_code: proj35_robot_website_v1
owner: toad (Coder)

The build is split into 3 cohesive components.

## Task 1 — Structure & content (index.html)
- Semantic HTML5 shell: header/nav, main, footer.
- Sticky nav with brand + anchor links (Home, Robots, Features, How it works, Specs, Contact)
  and an accessible mobile menu button.
- Sections with stable ids: `#home` (hero), `#robots`, `#features`, `#how`, `#specs`, `#contact`.
- Hero: headline, subcopy, primary + secondary CTAs, CSS robot mascot markup.
- Robots: 3 cards (Helpa / Labbie / Rover-X) with CSS avatar + stats.
- Features: 6 tiles. How-it-works: 3 steps. Specs: comparison table. Contact: form (name/email/message).
- `<head>`: lang, charset, viewport, title, meta description, favicon link.
- Done when: all section ids present; every nav anchor targets an existing id; valid, accessible markup.

## Task 2 — Design system & responsive layout (styles.css)
- CSS custom properties (colors, radii, spacing, shadows, fonts).
- Layout: container, grid for robot/feature cards, flbox nav; reflow to single column on mobile.
- CSS robot mascot (hero) + 3 distinct robot avatars (pure CSS, no images).
- States: nav active link, button hover/focus, form field focus/error/success.
- Responsive: mobile-first; breakpoints for tablet/desktop; mobile menu styles.
- Respect `prefers-reduced-motion`; visible focus outlines (accessibility).
- Done when: renders cleanly + reflows at narrow and wide widths with no overflow.

## Task 3 — Progressive enhancement (js/main.js) + infra
- Mobile nav toggle (`aria-expanded`, closes on link click / Escape).
- Scroll-reveal via IntersectionObserver (adds a class; content visible without JS).
- Active nav link highlight based on visible section (IntersectionObserver).
- Smooth-scroll for anchor links (CSS `scroll-behavior` + JS focus management).
- Contact form: client-side validation (required + email pattern), inline error + success message;
  prevent default submit (no backend).
- Footer year injected; static fallback year present in HTML.
- Infra: bicep SWA (Free) + GitHub Actions infra/deploy (token upload, skip_app_build).
- Done when: smoke test green; browser checks pass; deploy scaffolding committed.

## Verification (smallest meaningful)
- `node scripts/smoke.mjs` → static structure/link/section assertions PASS.
- Browser play-through on local static server: nav scroll + active link + mobile menu + form
  validation/success + responsive reflow; screenshot captured to media/outbound.
