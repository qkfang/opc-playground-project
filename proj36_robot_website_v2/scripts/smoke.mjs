// Headless static smoke test for the Cogsworth Robotics 2.0 website (proj36).
// Zero deps. Parses the built static files with regex/string checks — no browser.
// Run: node scripts/smoke.mjs   (exit 0 = pass, 1 = fail)
import { readFile } from "node:fs/promises";
import { existsSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const WEB = join(__dirname, "..", "apps", "web");

let passed = 0;
let failed = 0;
const fails = [];
function ok(cond, msg) {
  if (cond) { passed++; }
  else { failed++; fails.push(msg); console.error("  \u2717 FAIL:", msg); }
}
function section(name) { console.log(`\n# ${name}`); }

const html = await readFile(join(WEB, "index.html"), "utf8");
const css = await readFile(join(WEB, "styles.css"), "utf8");
const js = await readFile(join(WEB, "js", "main.js"), "utf8");

// 1. Required files exist
section("1. Required files exist");
for (const rel of [
  "index.html",
  "styles.css",
  "js/main.js",
  "assets/favicon.svg",
  "staticwebapp.config.json",
]) {
  ok(existsSync(join(WEB, rel)), `file exists: apps/web/${rel}`);
}

// 2. Document head essentials
section("2. Document head essentials");
ok(/<!DOCTYPE html>/i.test(html), "has <!DOCTYPE html>");
ok(/<html[^>]*\blang=["']en["']/i.test(html), 'has <html lang="en">');
ok(/<html[^>]*\bdata-theme=/i.test(html), "has data-theme on <html> (default theme)");
ok(/<meta[^>]*charset=["']?UTF-8/i.test(html), "has UTF-8 charset meta");
ok(/<meta[^>]*name=["']viewport["'][^>]*width=device-width/i.test(html), "has responsive viewport meta");
ok(/<title>[^<]*Cogsworth[^<]*<\/title>/i.test(html), "has a <title> mentioning the brand");
ok(/<title>[^<]*2\.0[^<]*<\/title>/i.test(html), "title signals v2 (2.0)");
ok(/<meta[^>]*name=["']description["'][^>]*content=/i.test(html), "has a meta description");

// 3. Asset references resolve to real files
section("3. Asset references resolve");
const cssHref = html.match(/<link[^>]*rel=["']stylesheet["'][^>]*href=["']([^"']+)["']/i);
ok(!!cssHref && existsSync(join(WEB, cssHref[1])), "stylesheet href points to an existing file");
const jsSrc = html.match(/<script[^>]*src=["']([^"']+)["']/i);
ok(!!jsSrc && existsSync(join(WEB, jsSrc[1])), "script src points to an existing file");
const favHref = html.match(/<link[^>]*rel=["']icon["'][^>]*href=["']([^"']+)["']/i);
ok(!!favHref && existsSync(join(WEB, favHref[1])), "favicon href points to an existing file");
ok(/\bdefer\b/i.test(jsSrc ? jsSrc.input.slice(jsSrc.index, jsSrc.index + 120) : ""), "main.js is deferred");

// 4. Required sections with stable ids (v2 adds build + faq + reviews)
section("4. Required section ids present");
const requiredIds = ["home", "robots", "build", "features", "how", "specs", "reviews", "faq", "contact"];
const idRe = (id) => new RegExp(`<section[^>]*\\bid=["']${id}["']`, "i");
for (const id of requiredIds) {
  ok(idRe(id).test(html), `<section id="${id}"> present`);
}

// 5. Every in-page nav anchor targets an existing id
section("5. Nav anchors resolve to real ids");
const allIds = new Set();
let m;
const idAttrRe = /\bid=["']([^"']+)["']/g;
while ((m = idAttrRe.exec(html))) allIds.add(m[1]);
const hrefRe = /href=["']#([^"']+)["']/g;
const anchorTargets = new Set();
while ((m = hrefRe.exec(html))) anchorTargets.add(m[1]);
ok(anchorTargets.size >= 7, `found ${anchorTargets.size} in-page anchors (expected >= 7)`);
for (const target of anchorTargets) {
  ok(allIds.has(target), `anchor #${target} resolves to an existing id`);
}

// 6. Primary nav has the expected links + theme toggle + mobile toggle
section("6. Primary navigation + controls");
ok(/<nav[^>]*class=["'][^"']*primary-nav/i.test(html), "primary <nav> present");
for (const label of ["Home", "Robots", "Build", "Features", "Specs", "FAQ", "Contact"]) {
  ok(new RegExp(`>${label}<`).test(html), `nav link labelled "${label}"`);
}
ok(/id=["']navToggle["']/.test(html) && /aria-expanded/.test(html), "accessible mobile nav toggle present");
ok(/id=["']themeToggle["']/.test(html) && /aria-pressed/.test(html), "accessible theme toggle present");
ok(/id=["']scrollProgress["']/.test(html), "scroll progress element present");
ok(/id=["']toTop["']/.test(html), "back-to-top button present");

// 7. Robot showcase content (v2 = 4 robots incl. Aero) + filter chips
section("7. Robot showcase + filter");
for (const bot of ["Helpa", "Labbie", "Rover-X", "Aero"]) {
  ok(new RegExp(bot.replace("-", "\\-")).test(html), `robot card present: ${bot}`);
}
const robotCards = (html.match(/class=["'][^"']*robot-card/g) || []).length;
ok(robotCards >= 4, `at least 4 robot cards (found ${robotCards})`);
const chips = (html.match(/class=["'][^"']*chip/g) || []).length;
ok(chips >= 5, `filter chips present (found ${chips}, expected >= 5)`);
for (const f of ["all", "home", "lab", "outdoor", "air"]) {
  ok(new RegExp(`data-filter=["']${f}["']`).test(html), `filter chip data-filter="${f}" present`);
}
ok(/data-category=["']air["']/.test(html), "Aero card tagged data-category=air");

// 8. Build-your-bot configurator
section("8. Configurator");
ok(/id=["']builderForm["']/.test(html), "builder form present");
ok((html.match(/name=["']model["']/g) || []).length >= 4, "4 model radio options present");
ok((html.match(/name=["']addon["']/g) || []).length >= 3, "3 add-on checkboxes present");
ok((html.match(/data-price=/g) || []).length >= 7, "data-price attributes present on options");
ok((html.match(/data-battery=/g) || []).length >= 7, "data-battery attributes present on options");
ok(/id=["']sumPrice["']/.test(html) && /id=["']sumBattery["']/.test(html), "live price + battery outputs present");

// 9. Features + steps + specs table (v2 = 4 robot columns)
section("9. Features, steps, specs");
const features = (html.match(/class="feature reveal"/g) || []).length;
ok(features === 6, `6 feature tiles (found ${features})`);
const steps = (html.match(/class="step reveal"/g) || []).length;
ok(steps === 3, `3 how-it-works steps (found ${steps})`);
ok(/<table[^>]*class=["']specs-table/i.test(html), "specs comparison <table> present");
ok((html.match(/<th[^>]*scope=["']col["']/g) || []).length >= 5, "specs table has >= 5 column headers (Spec + 4 bots)");

// 10. Testimonials + FAQ accordion
section("10. Testimonials + FAQ");
const quotes = (html.match(/class="quote-card reveal"/g) || []).length;
ok(quotes >= 3, `>= 3 testimonial cards (found ${quotes})`);
const faqTriggers = (html.match(/class=["']faq-trigger/g) || []).length;
ok(faqTriggers >= 5, `>= 5 FAQ accordion triggers (found ${faqTriggers})`);
ok((html.match(/class=["']faq-trigger[^>]*aria-expanded/g) || []).length >= 5, "FAQ triggers use aria-expanded");
ok((html.match(/class=["']faq-a["'][^>]*hidden/g) || []).length >= 5, "FAQ answer panels start hidden");

// 11. Contact form structure
section("11. Contact form");
ok(/<form[^>]*id=["']contactForm["']/i.test(html), "contact <form> present");
for (const fid of ["cf-name", "cf-email", "cf-message"]) {
  ok(new RegExp(`id=["']${fid}["']`).test(html), `form field #${fid} present`);
  ok(new RegExp(`<label[^>]*for=["']${fid}["']`).test(html), `label for #${fid} present`);
}
ok(/type=["']email["']/.test(html), "email input uses type=email");
ok(/id=["']formSuccess["']/.test(html), "inline success element present");

// 12. CSS sanity: design system + themes + responsive + reduced motion
section("12. CSS sanity");
ok(/:root\s*{/.test(css), "CSS defines :root custom properties");
ok(/--brand:/.test(css), "brand color variable defined");
ok(/\[data-theme=["']light["']\]/.test(css), "light theme token block present");
ok(/@media\s*\(min-width:\s*880px\)/.test(css), "desktop breakpoint present");
ok(/@media\s*\(max-width:\s*879px\)/.test(css), "mobile-nav breakpoint present");
ok(/@media\s*\(min-width:\s*640px\)/.test(css), "tablet breakpoint present");
ok(/prefers-reduced-motion/.test(css), "respects prefers-reduced-motion");
ok(/\.reveal\b/.test(css) && /\.is-visible\b/.test(css), "scroll-reveal classes styled");
ok(/\.chip\b/.test(css) && /\.is-active\b/.test(css), "filter chip styles present");
ok(/\.builder\b/.test(css) && /\.builder-summary\b/.test(css), "configurator styles present");
ok(/\.faq-trigger\b/.test(css), "FAQ styles present");
ok(/\.to-top\b/.test(css), "back-to-top styles present");
// No leftover placeholder/typo tokens from authoring.
ok(!/karst/.test(css), "no stray authoring typos in CSS");
ok(!/#\s\w{6}/.test(css), "no malformed hex color (stray space) in CSS");

// 13. JS sanity: progressive enhancement hooks (v2 features)
section("13. JS sanity");
ok(/IntersectionObserver/.test(js), "uses IntersectionObserver for reveal/active-link");
ok(/getElementById\(["']navToggle["']\)/.test(js), "wires the mobile nav toggle");
ok(/getElementById\(["']themeToggle["']\)/.test(js), "wires the theme toggle");
ok(/localStorage/.test(js), "persists theme via localStorage");
ok(/prefers-color-scheme/.test(js), "respects system color scheme on first load");
ok(/data-count/.test(js) && /requestAnimationFrame/.test(js), "animated count-up implemented");
ok(/data-filter/.test(js), "robot filter logic present");
ok(/getElementById\(["']builderForm["']\)/.test(js), "wires the configurator");
ok(/toLocaleString/.test(js), "configurator formats prices");
ok(/faq-trigger/.test(js), "wires FAQ accordion");
ok(/getElementById\(["']contactForm["']\)/.test(js), "wires the contact form");
ok(/EMAIL_RE|@/.test(js), "has email validation logic");
ok(/getElementById\(["']year["']\)/.test(js), "injects footer year");
ok(/preventDefault\(\)/.test(js), "intercepts form submit (no backend)");
ok(/getElementById\(["']toTop["']\)/.test(js), "wires back-to-top");

// 14. SWA config
section("14. SWA config");
const swa = JSON.parse(await readFile(join(WEB, "staticwebapp.config.json"), "utf8"));
ok(!!swa.navigationFallback && swa.navigationFallback.rewrite === "/index.html", "SWA navigationFallback -> /index.html");
ok(!!swa.responseOverrides && swa.responseOverrides["404"], "SWA 404 override present");

// ---- Report ----
console.log(`\n----------------------------------------`);
console.log(`Cogsworth Robotics 2.0 static smoke: ${passed} passed, ${failed} failed`);
if (failed > 0) {
  console.log("FAILURES:");
  for (const f of fails) console.log("  - " + f);
  console.log("SMOKE FAILED");
  process.exit(1);
} else {
  console.log("SMOKE PASSED");
  process.exit(0);
}
