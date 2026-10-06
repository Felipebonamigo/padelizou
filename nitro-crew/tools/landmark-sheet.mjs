// Folha de contato de qualquer marco (tools/landmark-sheet.html). Com o vite no ar (`npx vite --port <porta> --strictPort`):
//   node tools/landmark-sheet.mjs <porta> <saida.png> "ids=a,b,c&cols=3&cw=500&ch=320&zoom=0.8&az=0.3&el=0.05"
// Query: ids (padrão: todos os 161), cols, cw/ch (célula em px), zoom (>1 afasta), az/el (ângulo da câmera em rad; el baixo
// 0,02–0,06 com zoom 1,5–2,5 imita a vista da pista de longe), night=1. O rótulo mostra id · lugar · triângulos.
import { chromium } from 'playwright';
const [port, out, query = ''] = process.argv.slice(2);
if (!port || !out) { console.log('uso: node tools/landmark-sheet.mjs <porta> <saida.png> [query]'); process.exit(1); }
const cols = Number(/cols=(\d+)/.exec(query)?.[1] ?? 6); const cw = Number(/cw=(\d+)/.exec(query)?.[1] ?? 330); const ch = Number(/ch=(\d+)/.exec(query)?.[1] ?? 250);
const n = /ids=([^&]+)/.exec(query)?.[1].split(',').length ?? 161;
const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
const page = await browser.newPage({ viewport: { width: Math.min(n, cols) * cw, height: Math.ceil(n / cols) * ch } });
page.setDefaultTimeout(600000);
const errors = [];
page.on('pageerror', (e) => errors.push(e.message));
page.on('console', (m) => { if (m.type() === 'error' && !/404/.test(m.text())) errors.push(m.text()); });
await page.goto(`http://localhost:${port}/tools/landmark-sheet.html?${query}`, { waitUntil: 'load', timeout: 600000 });
await page.waitForFunction(() => window.ready, null, { timeout: 600000 });
await page.screenshot({ path: out });
if (errors.length) console.log('erros:', errors.slice(0, 5).join('\n'));
await browser.close();
console.log('ok', out);
