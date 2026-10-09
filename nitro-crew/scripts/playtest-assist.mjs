// Playtest da acessibilidade pelo fluxo real (teclado): Opções → coluna de acessibilidade (paleta,
// HUD, texto grande, reduzir efeitos, direção assistida), lobby com a direção por assento, corrida
// com o selo ASSIST. Uso: node scripts/playtest-assist.mjs http://localhost:4174/ scratch/pa
import { chromium } from 'playwright';
const url = process.argv[2] ?? 'http://localhost:4174/';
const out = process.argv[3] ?? 'scratch/pa';
const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
page.setDefaultTimeout(240000);
const errors = [];
page.on('pageerror', (e) => errors.push('pageerror: ' + e.message));
page.on('console', (m) => { if (m.type() === 'error') errors.push(`console: ${m.text()}`); });
const fails = [];
const check = (cond, msg) => { console.log(`${cond ? '✓' : '✗'} ${msg}`); if (!cond) fails.push(msg); };
const press = async (key, n = 1) => { for (let i = 0; i < n; i++) { await page.keyboard.press(key); await page.waitForTimeout(90); } };
const settings = () => page.evaluate(() => { const s = window.nc.session.settings; return { seatAssists: [...s.seatAssists], colorPalette: s.colorPalette, hudScale: s.hudScale, largeText: s.largeText, reduceEffects: s.reduceEffects }; });
const focused = () => page.evaluate(() => document.querySelector('#ui .focus')?.textContent?.trim() ?? '');
const mainIndex = (pattern) => page.evaluate((src) => {
  const re = new RegExp(src, 'i');
  const i = [...document.querySelectorAll('.scr-main .menu-list > *')].findIndex((el) => re.test((el.textContent ?? '').trim()));
  if (i < 0) throw new Error(`item do menu principal não encontrado: ${src}`);
  return i;
}, pattern);
/** Desce até o item focado começar com `label` (no máximo 40 passos). */
async function downTo(label, key = 'ArrowDown') {
  for (let i = 0; i < 40; i++) { if ((await focused()).startsWith(label)) return true; await press(key); }
  return false;
}
/** Tudo o que é focável na tela cabe em 1280×720, sem rolagem. */
const fits = () => page.evaluate(() => {
  const scr = document.querySelector('#ui .screen');
  const over = [...document.querySelectorAll('#ui .sel, #ui .btn')].filter((e) => { const r = e.getBoundingClientRect(); return r.bottom > window.innerHeight + 0.5 || r.right > window.innerWidth + 0.5; }).map((e) => e.textContent?.trim().slice(0, 30));
  return { scroll: scr ? scr.scrollHeight - scr.clientHeight : -1, over };
});

await page.goto(url, { waitUntil: 'networkidle' });
await page.waitForTimeout(800);
await page.evaluate(() => { window.nc.session.settings.quality = 'low'; });
await press('Enter'); // título → menu principal
await press('ArrowDown', await mainIndex('^op(ç|t)(õ|i)(e)?s|^options$'));
await press('Enter');
check(await page.evaluate(() => window.nc.session.menus.current()) === 'options', 'Opções abertas pelo menu');
let f = await fits();
check(f.scroll <= 0 && f.over.length === 0, `opções cabem em 1280×720 sem rolar (rolagem ${f.scroll}, fora: ${JSON.stringify(f.over)})`);
await page.screenshot({ path: `${out}-01-options.png` });

check(await downTo('Acessibilidade'), 'chega em "Acessibilidade" na coluna de corrida');
await press('Enter');
check(await page.evaluate(() => window.nc.session.menus.current()) === 'access', 'Enter abre a tela de acessibilidade');
f = await fits();
check(f.scroll <= 0 && f.over.length === 0, `acessibilidade cabe em 1280×720 (rolagem ${f.scroll}, fora: ${JSON.stringify(f.over)})`);
await page.screenshot({ path: `${out}-01b-access.png` });
check(await downTo('Cores dos jogadores'), 'chega em "Cores dos jogadores" pelo teclado');
await press('ArrowRight');
check(await downTo('Tamanho do HUD'), 'chega em "Tamanho do HUD"');
await press('ArrowRight', 6);
check(await downTo('Reduzir efeitos'), 'chega em "Reduzir efeitos"');
await press('Enter');
check(await downTo('Jogador 1'), 'chega em "Jogador 1" (direção assistida)');
await press('ArrowRight', 2);
const desc = await page.evaluate(() => document.querySelector('.access-desc')?.textContent ?? '');
check(desc.startsWith('Volante assistido'), `explicação do nível em foco (${desc.slice(0, 40)}…)`);
let s = await settings();
check(s.colorPalette === 'deutan' && s.hudScale === 1.5 && s.reduceEffects && s.seatAssists[0] === 'steer', `opções gravadas: ${JSON.stringify(s)}`);
const stored = await page.evaluate(() => JSON.parse(localStorage.getItem('nitro-crew.settings') ?? '{}'));
check(stored.colorPalette === 'deutan' && stored.seatAssists?.[0] === 'steer', 'e salvas no localStorage');
check(await page.evaluate(() => document.getElementById('hud').style.getPropertyValue('--hud-scale')) === '1.5', '--hud-scale 1.5 no #hud');
await page.screenshot({ path: `${out}-02-options-set.png` });

// Texto grande: liga, fotografa, vê se ainda cabe, desliga.
check(await downTo('Texto grande', 'ArrowUp'), 'chega em "Texto grande"');
await press('Enter');
s = await settings();
check(s.largeText && await page.evaluate(() => document.getElementById('ui').style.getPropertyValue('--text-scale')) === '1.2', 'texto grande liga o --text-scale 1.2 no #ui');
f = await fits();
console.log(`  (texto grande: rolagem ${f.scroll}, fora: ${JSON.stringify(f.over)})`);
await page.screenshot({ path: `${out}-03-options-large.png` });
await press('Enter');

// Lobby da corrida rápida: P1 (setas) e P2 (WASD) escolhem a direção no próprio cartão.
await press('Escape'); await press('Escape');
check(await page.evaluate(() => window.nc.session.menus.current()) === 'main', 'Esc volta às opções e ao menu principal');
await press('ArrowDown', await mainIndex('corrida r(á|a)pida|quick race'));
await press('Enter');
await press('KeyF');
check(await page.evaluate(() => window.nc.session.menus.current()) === 'lobby', 'lobby com dois jogadores');
// O cartão pode ter mais linhas acima (nome, carro, dupla da festa): desce até a direção pelo rótulo.
check(await downTo('Direção'), `P1 no seletor de direção (${await focused()})`);
await press('ArrowRight'); // steer → full
await press('KeyS', 2); await press('KeyD'); // P2: carro → Pintura → Direção; none → brake
s = await settings();
check(s.seatAssists[0] === 'full' && s.seatAssists[1] === 'brake', `direção por assento no lobby: ${JSON.stringify(s.seatAssists)}`);
const cards = await page.evaluate(() => [...document.querySelectorAll('.slot.occupied .sel-assist .sel-value')].map((e) => e.textContent));
check(JSON.stringify(cards) === JSON.stringify(['Completa', 'Freio automático']), `cartões mostram ${JSON.stringify(cards)}`);
const seatColors = await page.evaluate(() => [...document.querySelectorAll('.slot')].map((e) => e.style.getPropertyValue('--seat')));
check(seatColors[0] === '#f7ee2a' && seatColors[1] === '#2ef5c6', `cores dos cartões na paleta deutera: ${JSON.stringify(seatColors)}`);
f = await fits();
check(f.over.length === 0, `lobby cabe (fora: ${JSON.stringify(f.over)})`);
const overlap = await page.evaluate(() => [...document.querySelectorAll('.slot.occupied')].map((slot) => {
  const bars = slot.querySelector('.car-bars'); const sel = slot.querySelector('.sel-assist');
  return bars && sel ? Math.round(bars.getBoundingClientRect().bottom - sel.getBoundingClientRect().top) : null;
}));
check(overlap.every((o) => o !== null && o <= 0), `barras do carro não passam por baixo da direção (sobreposição px: ${JSON.stringify(overlap)})`);
await page.screenshot({ path: `${out}-04-lobby.png` });

// Prontos (confirmar no seletor de direção é PRONTO) e larga pelo botão Iniciar.
await press('Enter'); await press('KeyF');
check(await page.evaluate(() => document.querySelectorAll('.slot.ready').length) === 2, 'confirmar no seletor de direção marca PRONTO (os dois)');
for (let i = 0; i < 30 && !(await page.evaluate(() => !!document.querySelector('.btn-start.focus'))); i++) await press('ArrowDown');
await press('Enter');
check(await page.evaluate(() => window.nc.session.menus.current()) === 'tracks', 'Iniciar leva à escolha de pista');
await press('Enter');
await page.waitForTimeout(500);
if (await page.evaluate(() => window.nc.session.menus.current()) === 'tracks') await press('Enter');
await page.waitForTimeout(500);
const cfg = await page.evaluate(() => window.nc.session.race?.state.config.humans.map((h) => ({ seat: h.seat, assist: h.assist ?? 'none' })));
check(JSON.stringify(cfg) === JSON.stringify([{ seat: 0, assist: 'full' }, { seat: 1, assist: 'brake' }]), `a corrida leva a direção de cada um: ${JSON.stringify(cfg)}`);

// Corrida: P1 (completa) só acelera; P2 (freio automático) acelera e esterça pouco.
await page.keyboard.down('ArrowUp'); await page.keyboard.down('KeyW');
let worst = 0;
for (let i = 0; i < 12; i++) {
  await page.evaluate(() => window.nc.session.debugStep(120));
  worst = Math.max(worst, await page.evaluate(() => Math.abs(window.nc.session.race.state.cars.find((c) => c.seat === 0).x)));
}
check(worst < 1.05, `P1 com assistência completa e sem volante fica no asfalto (|x| máx ${worst.toFixed(2)})`);
await page.waitForTimeout(400);
const hud = await page.evaluate(() => ({
  badges: [...document.querySelectorAll('#hud .tag .assist.on')].map((e) => e.textContent),
  accents: [...document.querySelectorAll('#hud .vp')].filter((e) => e.style.display !== 'none').map((e) => e.style.getPropertyValue('--accent')),
  s: document.querySelector('#hud .vp')?.style.getPropertyValue('--s'),
  calm: document.getElementById('hud').classList.contains('reduce-fx'),
}));
check(hud.badges.length === 2 && hud.badges.every((b) => b === 'ASSIST'), `selo ASSIST nos dois HUDs (${JSON.stringify(hud.badges)})`);
check(hud.accents[0] === '#f7ee2a' && hud.accents[1] === '#2ef5c6', `HUD na paleta deutera (${JSON.stringify(hud.accents)})`);
check(/var\(--hud-scale/.test(hud.s ?? '') && hud.calm, `HUD escalado e calmo (--s ${hud.s})`);
await page.screenshot({ path: `${out}-05-race.png` });
await page.keyboard.up('ArrowUp'); await page.keyboard.up('KeyW');

check(errors.length === 0, `sem erros no console (${errors.slice(0, 3).join(' | ')})`);
await browser.close();
console.log(fails.length ? `\n${fails.length} falha(s)` : '\ntudo verde');
process.exit(fails.length ? 1 : 0);
