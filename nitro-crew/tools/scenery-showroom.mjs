// Capturas do showroom de modelos do cenário (sem a pista). Uso: node tools/scenery-showroom.mjs <porta> <saida.png> <ids,...> [query-extra]
import { chromium } from 'playwright';
const [port, out, ids, extra = ''] = process.argv.slice(2);
const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
page.setDefaultTimeout(300000);
const errors = [];
page.on('pageerror', (e) => errors.push(e.message));
page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
await page.goto(`http://localhost:${port}/tools/scenery-showroom.html?ids=${ids}${extra}`, { waitUntil: 'networkidle', timeout: 300000 });
await page.waitForFunction(() => window.ready, null, { timeout: 300000 });
await page.screenshot({ path: out });
if (errors.length) console.log('erros:', errors.slice(0, 5).join('\n'));
await browser.close();
console.log('ok', out);
