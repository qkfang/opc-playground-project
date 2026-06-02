import { chromium } from 'playwright';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const root = resolve(__dirname, '..');
const outDir = resolve(root, 'assets');

const pages = [
  { file: 'index.html', name: 'desktop', width: 1440, height: 900 },
  { file: 'index.html', name: 'mobile',  width: 430,  height: 932 },
];

const browser = await chromium.launch();
const page = await browser.newPage();

for (const vp of pages) {
  const htmlPath = resolve(root, vp.file);
  const url = new URL(`file:///${htmlPath.replace(/\\/g, '/')}`);

  await page.setViewportSize({ width: vp.width, height: vp.height });
  await page.goto(url.toString(), { waitUntil: 'networkidle' });
  await page.waitForTimeout(250);

  const outPath = resolve(outDir, `screenshot-${vp.name}.png`);
  await page.screenshot({ path: outPath, fullPage: true });
  console.log(`wrote ${outPath}`);
}

await browser.close();
