// Capturas do cenário por bioma × período (1 e 4 jogadores) com o custo do cenário isolado:
// o mesmo quadro com e sem o grupo 'scenery' (chamadas de desenho e triângulos). Saída em scratch/.
// Uso: node tools/scenery-harness.mjs <porta> <prefixo-de-saida> [filtro,filtro] [humanos=1,4] [&query-extra]
import { mkdirSync } from 'node:fs';
import { chromium } from 'playwright';
const port = process.argv[2] ?? '5174';
const prefix = process.argv[3] ?? 'scratch/cen';
const filter = process.argv[4] ?? '';
const humansList = (process.argv[5] ?? '1,4').split(',').map(Number);
const extra = process.argv[6] ?? '';
mkdirSync(prefix.replace(/\/[^/]*$/, ''), { recursive: true });
const scenes = [
  ['coast-day', 'track=copacabana&ticks=1500'],
  ['coast-dusk', 'track=baia_toquio&ticks=1500'],
  ['coast-night', 'track=monaco_noite&ticks=1500'],
  ['savanna-day', 'track=kruger&ticks=1500'],
  ['savanna-dusk', 'track=transpantaneira&ticks=1500'],
  ['tropical-day', 'track=serra_do_mar&ticks=1500'],
  ['city-night', 'track=sampa_noite&ticks=1400'],
  ['city-dusk', 'track=paris&ticks=1500'],
  ['desert-day', 'track=rota_66&ticks=1500'],
  ['desert-dusk', 'track=canion&ticks=1600'],
  ['alpine-day', 'track=monte_fuji&ticks=1700'],
  ['alpine-dusk', 'track=laponia&ticks=1500'],
  ['start', 'track=copacabana&ticks=30'],
  ['pit', 'track=copacabana&idle=1&t=0.3'],
];
const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
for (const [name, query] of scenes) {
  if (filter && !filter.split(',').some((f) => name === f || name.startsWith(f))) continue;
  for (const humans of humansList) {
    if (humans > 1 && name === 'pit') continue;
    const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
    page.setDefaultTimeout(900000);
    const errors = [];
    page.on('pageerror', (e) => errors.push(e.message));
    page.on('console', (m) => { if (m.type() === 'error' || m.type() === 'warning') errors.push(m.text()); });
    const t0 = Date.now();
    const tag = `${name}-${humans}p`;
    try {
      await page.goto(`http://localhost:${port}/tools/scenery-harness.html?${query}&humans=${humans}&frames=10${extra}`, { waitUntil: "networkidle", timeout: 900000 });
      await page.waitForFunction(() => window.ready, null, { timeout: 900000 });
      const debug = await page.evaluate(() => window.debug);
      await page.screenshot({ path: `${prefix}-${tag}.png`, timeout: 900000 });
      console.log(`${tag.padEnd(18)} ${JSON.stringify(debug)} (${Date.now() - t0} ms)`);
    } catch (e) { console.log(`${tag}: FALHOU ${e.message}`); }
    const relevant = errors.filter((e) => !e.includes('GL Driver') && !e.includes('404') && !e.includes('GPU stall'));
    if (relevant.length) console.log('  console:', relevant.slice(0, 6).join('\n           '));
    await page.close();
  }
}
await browser.close();
