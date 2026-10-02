// Folha de contato dos marcos do Norte e Nordeste. Com `npx vite --port <porta>` no ar:
// node tools/landmark-showroom-norte.mjs <porta> <saida.png> [ids,...|all] [query-extra] [largura] [altura]
import { chromium } from 'playwright';
const [port, out, ids = 'all', extra = '', w = '1600', h = '900'] = process.argv.slice(2);
const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
const page = await browser.newPage({ viewport: { width: Number(w), height: Number(h) } });
page.setDefaultTimeout(300000);
const errors = [];
page.on('pageerror', (e) => errors.push(e.message));
page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
const idq = ids === 'all' ? '' : `ids=${ids}`;
await page.goto(`http://localhost:${port}/tools/landmark-showroom-norte.html?${idq}${extra}`, { waitUntil: 'networkidle', timeout: 300000 });
await page.waitForFunction(() => window.ready, null, { timeout: 300000 });
await page.screenshot({ path: out });
if (errors.length) console.log('erros:', errors.slice(0, 5).join('\n'));
await browser.close();
console.log('ok', out);
