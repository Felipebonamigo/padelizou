// Capturas do renderizador no Chromium headless (swiftshader), uma página por vez.
// Uso: node tools/render-harness.mjs [porta=5174] [filtro-de-nome]
// Cada cena vira scratch/render-<nome>.png; imprime debugInfo() (draw calls, triângulos) e erros.
import { mkdirSync } from 'node:fs';
import { chromium } from 'playwright';
mkdirSync('scratch', { recursive: true });
const port = process.argv[2] ?? '5174';
const filter = process.argv[3] ?? '';
const scenes = [
  ['copacabana', 'track=copacabana&ticks=1500&frames=24'],
  ['sampa_noite', 'track=sampa_noite&ticks=1400&frames=24'],
  ['canion', 'track=canion&ticks=1600&frames=24'],
  ['monte_fuji', 'track=monte_fuji&ticks=1700&frames=24'],
  ['rota_66', 'track=rota_66&ticks=1500&frames=24'],
  ['split4', 'track=copacabana&humans=4&ticks=1500&frames=24'],
  ['split3', 'track=copacabana&humans=3&ticks=1500&frames=24'],
  ['idle', 'track=serra_do_mar&idle=1&t=40&frames=24'],
  ['nitro', 'track=rota_66&ticks=1500&nitro=1&frames=24'],
  ['countdown', 'track=copacabana&ticks=100&msg=x&frames=24'],
  ['skid', 'track=copacabana&ticks=1500&skid=1&frames=24'],
  ['pit', 'track=copacabana&idle=1&t=0.3&frames=24'],
  ['medium', 'track=serra_do_mar&ticks=1500&q=medium&frames=24'],
  ['low', 'track=serra_do_mar&ticks=1500&q=low&frames=24'],
];
const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
for (const [name, query] of scenes) {
  if (filter && !name.includes(filter)) continue;
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.message));
  page.on('console', (m) => { if (m.type() === 'error' || m.type() === 'warning') errors.push(m.text()); });
  const t0 = Date.now();
  try {
    await page.goto(`http://localhost:${port}/tools/render-harness.html?${query}`, { waitUntil: 'networkidle' });
    await page.waitForFunction(() => window.ready, null, { timeout: 180000 });
    const debug = await page.evaluate(() => window.debug);
    await page.screenshot({ path: `scratch/render-${name}.png`, timeout: 120000 });
    const pageErrors = await page.evaluate(() => window.errors);
    console.log(`${name.padEnd(12)} ${JSON.stringify(debug)} (${Date.now() - t0} ms)`);
    if (pageErrors.length) console.log('  erros na página:', pageErrors.join(' | '));
  } catch (e) { console.log(`${name}: FALHOU ${e.message}`); }
  const relevant = errors.filter((e) => !e.includes('GL Driver') && !e.includes('404'));
  if (relevant.length) console.log('  console:', relevant.slice(0, 6).join('\n           '));
  await page.close();
}
await browser.close();
