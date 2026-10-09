// Banco de prova no Chromium headless (swiftshader): abre /?bench=1&<query> no vite, espera window.ncBench.done, grava o
// JSON e imprime uma linha por corrida, a meta de cada uma e cada problema/aviso. Só prova o formato; FPS de verdade é o do
// dono (docs/DESEMPENHO.md §3–4: no PC com `"Nitro Crew.exe" --bench`, no Deck com `./nitro-crew --bench`).
// Uso: node tools/bench.mjs <porta> [query] [saida.json]     ex.: node tools/bench.mjs 5641 "cenas=copa-1p&q=low&frames=2&warm=1"
// Níveis (q=): low, medium, high e ultra (2560x1440 na Alta; `res=LxA` força outro tamanho). Sem q=, mede os quatro.
// Sai com 1 se houver problema de formato ou erro na página; aviso e meta não batida NÃO dão exit 1.
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';
import { chromium } from 'playwright';

const [port, query = '', out = 'scratch/bench.json'] = process.argv.slice(2);
if (!port) { console.log('uso: node tools/bench.mjs <porta> [query] [saida.json]'); process.exit(1); }
const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader'] });
const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
page.setDefaultTimeout(1800000);
const errors = [];
let onError; const failed = new Promise((resolve) => { onError = resolve; });
const IGNORE = /GL Driver|404/; // o mesmo filtro de tools/render-harness.mjs:60
page.on('pageerror', (e) => { errors.push(`pageerror: ${e.message}`); onError(); });
page.on('console', (m) => { if (m.type() === 'error' && !IGNORE.test(m.text())) { errors.push(`console: ${m.text()}`); onError(); } });
await page.goto(`http://localhost:${port}/?bench=1${query ? `&${query}` : ''}`);
const done = page.waitForFunction(() => window.ncBench?.done === true, null, { polling: 1000 });
done.catch(() => {}); // perde a corrida para `failed` → rejeita quando o navegador fecha; sem isto o Node cai antes de imprimir
await Promise.race([done, failed]);
const st = await page.evaluate(() => window.ncBench ?? null);
await browser.close();
const report = st?.report ?? null;
if (report) { mkdirSync(dirname(out), { recursive: true }); writeFileSync(out, JSON.stringify(report, null, 2)); }
const mark = (v) => (!v || v.ok === null ? '  ' : v.ok ? 'ok' : '--');
(report?.runs ?? []).forEach((r, i) => {
  const vp = Array.isArray(r.viewports) && r.viewports.length ? `${r.viewports.length}×${r.viewports[0].targetW}x${r.viewports[0].targetH}` : 'viewports ?';
  const meta = report.verdicts?.[i];
  console.log(`${r.scene.padEnd(9)} ${r.quality.padEnd(6)} fps ${r.avgFps.toFixed(1).padStart(6)} 1%low ${r.low1Fps.toFixed(1).padStart(6)} chamadas ${String(r.calls).padStart(5)} triângulos ${String(r.triangles).padStart(8)} ${vp.padEnd(11)} meta ${meta?.targetFps ?? '-'} ${mark(meta)}`);
});
for (const w of report?.warnings ?? []) console.log(`aviso: ${w}`);
const problems = [...(st?.problems ?? []), ...errors];
for (const p of problems) console.log(`problema: ${p}`);
const bad = problems.length > 0 || !report;
console.log(`${bad ? 'FALHOU' : 'ok'} ${report ? out : '(sem relatório)'} (${report?.runs.length ?? 0} corridas, ${problems.length} problemas)`);
process.exit(bad ? 1 : 0);
