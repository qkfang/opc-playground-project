// Headless static smoke test for the Cogsworth Robotics website (proj35).
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
ok(/<meta[^>]*charset=["']?UTF-8/i.test(html), "has UTF-8 charset meta");
ok(/<meta[^>]*name=["']viewport["'][^>]*width=device-width/i.test(html), "has responsive viewport meta");
ok(/<title>[^<]*Cogsworth[^<]*<\/title>/i.test(html), "has a <title> mentioning the brand");
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

// 4. Required sections with stable ids
section("4. Required section ids present");
const requiredIds = ["home", "robots", "features", "how", "specs", "contact"];
const idRe = (id) => new RegExp(`<section[^>]*\\bid=["']${id}["']`, "i");
for (const id of requiredIds) {
  ok(idRe(id).test(html), `<section id="${id}"> present`);
}

// 5. Every in-page nav anchor targets an existing id
section("5. Nav anchors resolve to real ids");
// Collect all element ids in the document.
const allIds = new Set();
let m;
const idAttrRe = /\bid=["']([^"']+)["']/g;
while ((m = idAttrRe.exec(html))) allIds.add(m[1]);
// Collect all internal hash links.
const hrefRe = /href=["']#([^"']+)["']/g;
const anchorTargets = new Set();
while ((m = hrefRe.exec(html))) anchorTargets.add(m[1]);
ok(anchorTargets.size >= 6, `found ${anchorTargets.size} in-page anchors (expected >= 6)`);
for (const target of anchorTargets) {
  ok(allIds.has(target), `anchor #${target} resolves to an existing id`);
}

// 6. Primary nav has the expected links
section("6. Primary navigation");
ok(/<nav[^>]*class=["'][^"']*primary-nav/i.test(html), "primary <nav> present");
for (const label of ["Home", "Robots", "Features", "How it works", "Specs", "Contact"]) {
  ok(new RegExp(`>${label}<`).test(html), `nav link labelled "${label}"`);
}
ok(/id=["']navToggle["']/.test(html) && /aria-expanded/.test(html), "accessible mobile nav toggle present");

// 7. Robot showcase content
section("7. Robot showcase");
for (const bot of ["Helpa", "Labbie", "Rover-X"]) {
  ok(new RegExp(bot.replace("-", "\\-")).test(html), `robot card present: ${bot}`);
}
const robotCards = (html.match(/class=["'][^"']*robot-card/g) || []).length;
ok(robotCards === 3, `exactly 3 robot cards (found ${robotCards})`);

// 8. Features + steps + specs table
section("8. Features, steps, specs");
const features = (html.match(/class="feature reveal"/g) || []).length;
ok(features === 6, `6 feature tiles (found ${features})`);
const steps = (html.match(/class="step reveal"/g) || []).length;
ok(steps === 3, `3 how-it-works steps (found ${steps})`);
ok(/<table[^>]*class=["']specs-table/i.test(html), "specs comparison <table> present");
ok((html.match(/<th[^>]*scope=["']col["']/g) || []).length >= 4, "specs table has >= 4 column headers");

// 9. Contact form structure
section("9. Contact form");
ok(/<form[^>]*id=["']contactForm["']/i.test(html), "contact <form> present");
for (const fid of ["cf-name", "cf-email", "cf-message"]) {
  ok(new RegExp(`id=["']${fid}["']`).test(html), `form field #${fid} present`);
  ok(new RegExp(`<label[^>]*for=["']${fid}["']`).test(html), `label for #${fid} present`);
}
ok(/type=["']email["']/.test(html), "email input uses type=email");
ok(/id=["']formSuccess["']/.test(html), "inline success element present");

// 10. CSS sanity: design system + responsive + reduced motion
section("10. CSS sanity");
ok(/:root\s*{/.test(css), "CSS defines :root custom properties");
ok(/--brand:/.test(css), "brand color variable defined");
ok(/@media\s*\(min-width:\s*880px\)/.test(css), "desktop breakpoint present");
ok(/@media\s*\(max-width:\s*879px\)/.test(css), "mobile-nav breakpoint present");
ok(/prefers-reduced-motion/.test(css), "respects prefers-reduced-motion");
ok(/\.reveal\b/.test(css) && /\.is-visible\b/.test(css), "scroll-reveal classes styled");
// No leftover placeholder/typo tokens from authoring.
ok(!/karst/.test(css), "no stray authoring typos in CSS");

// 11. JS sanity: progressive enhancement hooks
section("11. JS sanity");
ok(/IntersectionObserver/.test(js), "uses IntersectionObserver for reveal/active-link");
ok(/getElementById\(["']navToggle["']\)/.test(js), "wires the mobile nav toggle");
ok(/getElementById\(["']contactForm["']\)/.test(js), "wires the contact form");
ok(/EMAIL_RE|\\@/.test(js) || /@/.test(js), "has email validation logic");
ok(/getElementById\(["']year["']\)/.test(js), "injects footer year");
ok(/preventDefault\(\)/.test(js), "intercepts form submit (no backend)");

// 12. SWA config
section("12. SWA config");
const swa = JSON.parse(await readFile(join(WEB, "staticwebapp.config.json"), "utf8"));
ok(!!swa.navigationFallback && swa.navigationFallback.rewrite === "/index.html", "SWA navigationFallback -> /index.html");
ok(!!swa.responseOverrides && swa.responseOverrides["404"], "SWA 404 override present");

// ---- Report ----
console.log(`\n----------------------------------------`);
console.log(`Cogsworth Robotics static smoke: ${passed} passed, ${failed} failed`);
if (failed > 0) {
  console.log("FAILURES:");
  for (const f of fails) console.log("  - " + f);
  console.log("SMOKE FAILED");
  process.exit(1);
} else {
  console.log("SMOKE PASSED");
  process.exit(0);
}
