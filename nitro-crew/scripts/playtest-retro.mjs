// Playtest do modo Retrô (pseudo-3D, src/render-pseudo3d) no Chromium: Opções › Visual troca o renderizador na
// hora; o que outros módulos puseram no HUD (painel do tutorial, HUD do online) sobrevive à troca; corrida de 1
// jogador em quatro cenários, tela dividida em 2 e 4, fundo dos menus; e a volta ao visual Moderno.
// Uso: npx vite build && npx vite preview --port 4174 --strictPort &
//      node scripts/playtest-retro.mjs http://localhost:4174/ scratch/pr
import { chromium } from 'playwright';
const url = process.argv[2] ?? 'http://localhost:4174/';
const out = process.argv[3] ?? 'scratch/pr';
const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
page.setDefaultTimeout(240000);
const errors = [];
page.on('pageerror', (e) => errors.push('pageerror: ' + e.message));
page.on('console', (m) => { if (m.type() === 'error') errors.push(`console: ${m.text()}`); });
const fails = [];
const check = (cond, msg) => { console.log(`${cond ? '✓' : '✗'} ${msg}`); if (!cond) fails.push(msg); };
const press = async (key, n = 1) => { for (let i = 0; i < n; i++) { await page.keyboard.press(key); await page.waitForTimeout(90); } };
const focused = () => page.evaluate(() => document.querySelector('#ui .focus')?.textContent?.trim() ?? '');
const mainIndex = (pattern) => page.evaluate((src) => {
  const re = new RegExp(src, 'i');
  const i = [...document.querySelectorAll('.scr-main .menu-list > *')].findIndex((el) => re.test((el.textContent ?? '').trim()));
  if (i < 0) throw new Error(`item do menu principal não encontrado: ${src}`);
  return i;
}, pattern);
async function downTo(label) {
  for (let i = 0; i < 40; i++) { if ((await focused()).startsWith(label)) return true; await press('ArrowDown'); }
  return false;
}
const info = () => page.evaluate(() => {
  const s = window.nc.session; const r = s.renderer;
  const dbg = typeof r.debugInfo === 'function' ? r.debugInfo() : null;
  return { style: s.settings.renderStyle, mode: dbg?.mode ?? '3d', dbg, marker: !!document.getElementById('hud-marker'), canvas: document.getElementById('game') === r.canvas };
});
/** Variedade de cores numa grade de pixels da tela (tela preta ou de uma cor só = nada desenhado). */
const colorfulness = () => page.evaluate(() => {
  const c = document.getElementById('game');
  const ctx = c.getContext('2d');
  if (!ctx) return -1;
  const seen = new Set();
  for (let y = 5; y < c.height; y += Math.floor(c.height / 24)) for (let x = 5; x < c.width; x += Math.floor(c.width / 32)) {
    const d = ctx.getImageData(x, y, 1, 1).data; seen.add(`${d[0] >> 4},${d[1] >> 4},${d[2] >> 4}`);
  }
  return seen.size;
});
const humans = (n) => Array.from({ length: n }, (_, i) => ({ seat: i, name: `P${i + 1}`, carId: ['falcao', 'trovao', 'tornado', 'camelo'][i], teamId: 0, color: '#ffffff' }));
async function race(trackId, n, shot) {
  await page.evaluate(({ id, h }) => { const s = window.nc.session; s.menus.hide(); for (let i = 0; i < 4; i++) s.input.unbindSeat(i); s.debugBind(0, 'kb1'); if (h.length > 1) s.debugBind(1, 'kb2'); s.startQuick(id, 2, h); }, { id: trackId, h: humans(n) });
  await page.keyboard.down('ArrowUp');
  await page.evaluate(() => window.nc.session.debugStep(60 * 12));
  await page.keyboard.up('ArrowUp');
  await page.waitForTimeout(400);
  await page.screenshot({ path: `${out}-${shot}.png` });
  return info();
}

await page.goto(url, { waitUntil: 'networkidle' });
await page.waitForTimeout(800);
// O 3D daqui é por software (swiftshader): nos trechos no visual Moderno, qualidade baixa. O Retrô usa a mesma
// opção só para o número de linhas; ele é testado em alta abaixo.
await page.evaluate(() => { window.nc.session.settings.quality = 'low'; });
await press('Enter');
check(await page.evaluate(() => window.nc.session.menus.current()) === 'main', 'menu principal');
check((await info()).mode === '3d', 'começa no visual Moderno (3D)');
// Marcador no HUD, como o painel do tutorial e o HUD do online fazem: a troca de renderizador não pode apagá-lo.
await page.evaluate(() => { const m = document.createElement('div'); m.id = 'hud-marker'; document.getElementById('hud').appendChild(m); });

// Opções › Visual → Retrô, pelo teclado.
await press('ArrowDown', await mainIndex('^op(ç|t)(õ|i)(e)?s|^options$'));
await press('Enter');
check(await downTo('Visual'), `chega em "Visual" (${await focused()})`);
await press('ArrowRight');
await page.evaluate(() => { window.nc.session.settings.quality = 'high'; });
let st = await info();
check(st.style === 'retro' && st.mode === 'retro', `Visual → Retrô troca o renderizador na hora (${JSON.stringify(st)})`);
check(st.canvas, 'o canvas novo tomou o lugar do antigo (#game)');
check(st.marker, 'o que outros módulos puseram no HUD sobrevive à troca');
await page.screenshot({ path: `${out}-01-opcoes.png` });
await press('Escape');
await page.waitForTimeout(600);
await page.screenshot({ path: `${out}-02-menu-fundo.png` });
check(await colorfulness() > 8, 'fundo dos menus desenhado pelo Retrô');

for (const [id, name] of [['copacabana', '03-copacabana'], ['sampa_noite', '04-sampa-noite'], ['canion', '05-canion'], ['monte_fuji', '06-monte-fuji']]) {
  st = await race(id, 1, name);
  const d = st.dbg ?? {};
  // Numa crista de morro poucos segmentos aparecem (o resto está atrás dela): basta haver pista e carros.
  check(d.segments > 5 && d.cars > 0, `${id}: pista e carros desenhados (${JSON.stringify(d)})`);
  check(await colorfulness() > 12, `${id}: a tela tem cor (não ficou preta nem lisa)`);
}
st = await race('serra_do_mar', 2, '07-dois-jogadores');
check((st.dbg?.cars ?? 0) >= 2, `tela dividida em 2 (${JSON.stringify(st.dbg)})`);
st = await race('rota_66', 4, '08-quatro-jogadores');
check((st.dbg?.cars ?? 0) >= 4, `tela dividida em 4 (${JSON.stringify(st.dbg)})`);
const hudSeats = await page.evaluate(() => [...document.querySelectorAll('#hud .vp')].filter((e) => e.style.display !== 'none').length);
check(hudSeats === 4, `HUD de cada um dos 4 jogadores por cima do Retrô (${hudSeats})`);

// De volta ao Moderno (em qualidade baixa: o 3D daqui é por software).
await page.evaluate(() => { const s = window.nc.session; s.settings.quality = 'low'; s.handleMenuEvent({ type: 'toMain' }); });
await page.waitForTimeout(300);
await press('ArrowDown', await mainIndex('^op(ç|t)(õ|i)(e)?s|^options$'));
await press('Enter');
await downTo('Visual');
await press('ArrowRight');
st = await info();
check(st.style === 'modern' && st.mode === '3d' && st.canvas && st.marker, `volta ao Moderno, HUD de fora intacto (${JSON.stringify({ style: st.style, mode: st.mode, marker: st.marker })})`);
const persisted = await page.evaluate(() => JSON.parse(localStorage.getItem('nitro-crew.settings') ?? '{}').renderStyle);
check(persisted === 'modern', `a escolha fica salva (${persisted})`);

check(errors.length === 0, `sem erros na página (${errors.slice(0, 3).join(' | ')})`);
await browser.close();
console.log(fails.length ? `FALHOU (${fails.length})` : 'playtest retrô OK');
process.exit(fails.length ? 1 : 0);
