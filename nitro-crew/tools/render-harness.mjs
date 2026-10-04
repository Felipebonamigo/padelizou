// Capturas do renderizador no Chromium headless (swiftshader), uma página por vez.
// Uso: node tools/render-harness.mjs [porta=5174] [filtro-de-nome]
// Cada cena vira scratch/render-<nome>.png; imprime debugInfo() (draw calls, triângulos, geometrias,
// texturas, programas) e erros. Sem filtro roda as cenas da lista principal; as vistas de um estilo de
// carro (carro_<estilo>_<vista>, 13 × 4) só rodam pelo filtro — ex.: `carro_muscle` ou `_front34`.
import { mkdirSync } from 'node:fs';
import { chromium } from 'playwright';
mkdirSync('scratch', { recursive: true });
const port = process.argv[2] ?? '5174';
const filter = process.argv[3] ?? '';
const BIG = { width: 1920, height: 1080 };
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
  ['ghost', 'track=copacabana&ticks=1500&ghost=1&frames=24'],
  // Carros: os 13 estilos em grade na pista e em folhas de contato (frente 3/4, traseira 3/4, lado, noite).
  ['showroom', 'track=copacabana&showroom=1&frames=6'],
  ['showroom_tras', 'track=copacabana&showroom=1&carview=rear34&frames=6'],
  ['folha_frente34', 'track=copacabana&sheet=front34&frames=3', BIG],
  ['folha_tras34', 'track=copacabana&sheet=rear34&frames=3', BIG],
  ['folha_lado', 'track=copacabana&sheet=side&frames=3', BIG],
  ['folha_noite', 'track=sampa_noite&sheet=front34&frames=3', BIG],
  // Pintura (docs/CARROS.md, "Pintura"): o mesmo carro nas 16 da paleta; e a corrida com dois humanos pintados.
  ['cores_folha', 'track=copacabana&paintsheet=falcao&carview=front34&frames=3', BIG],
  ['cores_split2', 'track=copacabana&humans=2&paints=rubi,cobalto&ticks=1500&frames=24'],
  ['cores_rubi', 'track=copacabana&carview=rear34&car=falcao&paints=rubi&ticks=60&frames=6'],
  ['cores_cobalto', 'track=copacabana&carview=rear34&car=falcao&paints=cobalto&ticks=60&frames=6'],
];
const BODIES = ['gt', 'muscle', 'hatch', 'sedan', 'electric', 'rally', 'hyper', 'classic', 'wedge', 'pickup', 'prototype', 'micro', 'roadster'];
const carScenes = BODIES.flatMap((b) => ['front34', 'rear34', 'side', 'rear'].map((v) => [`carro_${b}_${v}`, `track=copacabana&carview=${v}&body=${b}&frames=6`]));
const run = filter ? [...scenes, ...carScenes].filter(([name]) => name.includes(filter)) : scenes;
const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
for (const [name, query, size] of run) {
  const page = await browser.newPage({ viewport: size ?? { width: 1280, height: 720 } });
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.message));
  page.on('console', (m) => { if (m.type() === 'error' || m.type() === 'warning') errors.push(m.text()); });
  const t0 = Date.now();
  try {
    await page.goto(`http://localhost:${port}/tools/render-harness.html?${query}`, { waitUntil: 'networkidle' });
    await page.waitForFunction(() => window.ready, null, { timeout: 400000 });
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
