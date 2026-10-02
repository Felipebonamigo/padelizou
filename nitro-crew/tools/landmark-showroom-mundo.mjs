// Folha de contato dos marcos do mundo (tools/landmark-showroom-mundo.html), com `npx vite --port <porta>` no ar.
// Uso: node tools/landmark-showroom-mundo.mjs <porta> <saida.png> [query]   ex.: 5233 dia.png · 5233 noite.png 'night=1'
import { chromium } from 'playwright';
const [port, out, query = ''] = process.argv.slice(2);
const cols = Number(/cols=(\d+)/.exec(query)?.[1] ?? 6); const cw = Number(/cw=(\d+)/.exec(query)?.[1] ?? 330); const ch = Number(/ch=(\d+)/.exec(query)?.[1] ?? 250);
const n = /ids=([^&]+)/.exec(query)?.[1].split(',').length ?? 44;
const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
const page = await browser.newPage({ viewport: { width: Math.min(n, cols) * cw, height: Math.ceil(n / cols) * ch } });
page.setDefaultTimeout(600000);
const errors = [];
page.on('pageerror', (e) => errors.push(e.message));
page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
await page.goto(`http://localhost:${port}/tools/landmark-showroom-mundo.html?${query}`, { waitUntil: 'load', timeout: 600000 });
await page.waitForFunction(() => window.ready, null, { timeout: 600000 });
await page.screenshot({ path: out });
if (errors.length) console.log('erros:', errors.slice(0, 5).join('\n'));
await browser.close();
console.log('ok', out);
