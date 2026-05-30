import { chromium } from 'playwright';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const root = resolve(__dirname, '..');
const htmlPath = resolve(root, 'index.html');
const outDir = resolve(root, 'assets');

const viewports = [
  { name: 'desktop', width: 1440, height: 900 },
  { name: 'mobile', width: 430, height: 932 },
];

const url = new URL(`file:///${htmlPath.replace(/\\/g, '/')}`);

const browser = await chromium.launch();
const page = await browser.newPage();

for (const vp of viewports) {
  await page.setViewportSize({ width: vp.width, height: vp.height });
  await page.goto(url.toString(), { waitUntil: 'networkidle' });
  await page.waitForTimeout(250);

  const outPath = resolve(outDir, `screenshot-${vp.name}.png`);
  await page.screenshot({ path: outPath, fullPage: true });
  console.log(`wrote ${outPath}`);
}

await browser.close();
