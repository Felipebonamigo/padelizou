// Playtest da pintura do carro pelo fluxo real (teclado; docs/CARROS.md, "Pintura"): Corrida rápida → lobby com
// P1 (setas) e P2 (WASD) → cada um desce até "Pintura" e anda pela paleta → a escolha vai para o save (por assento
// e carro) → PRONTO, INICIAR, pista → a corrida desenha cada carro na sua cor (3D e Retrô) → a garagem da carreira
// pinta o carro do piloto → reabrir o jogo traz a escolha de volta no lobby. Capturas em <prefixo>-*.png.
// Uso: node scripts/playtest-paint.mjs [url=http://localhost:4174/] [prefixo=scratch/cores]  (com `npm run preview` no ar)
import { chromium } from 'playwright';
const url = process.argv[2] ?? 'http://localhost:4174/';
const out = process.argv[3] ?? 'scratch/cores';
const browser = await chromium.launch({ executablePath: process.env.CHROME_PATH ?? '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
const context = await browser.newContext({ viewport: { width: 1280, height: 720 } });
const page = await context.newPage();
page.setDefaultTimeout(240000);
const errors = [];
page.on('pageerror', (e) => errors.push('pageerror: ' + e.message));
page.on('console', (m) => { if (m.type() === 'error') errors.push(`console: ${m.text()}`); });
const fails = [];
const check = (cond, msg) => { console.log(`${cond ? '✓' : '✗'} ${msg}`); if (!cond) fails.push(msg); };
const press = async (key, n = 1) => { for (let i = 0; i < n; i++) { await page.keyboard.press(key); await page.waitForTimeout(90); } };
const menu = () => page.evaluate(() => window.nc.session.menus.current());
const mainIndex = (pattern) => page.evaluate((src) => {
  const re = new RegExp(src, 'i');
  const i = [...document.querySelectorAll('.scr-main .menu-list > *')].findIndex((el) => re.test((el.textContent ?? '').trim()));
  if (i < 0) throw new Error(`item do menu principal não encontrado: ${src}`);
  return i;
}, pattern);
/** Pintura à mostra em cada cartão do lobby (data-paint do seletor) e o nome dela. */
const slotPaints = () => page.evaluate(() => [...document.querySelectorAll('.slot.occupied .sel-paint')].map((e) => `${e.dataset.paint}:${e.querySelector('.paint-name')?.textContent}`));
const savedPaints = () => page.evaluate(() => JSON.parse(localStorage.getItem('nitro-crew.save') ?? '{}').seatPaints ?? null);
const settle = () => page.evaluate(() => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))));
async function openGame() {
  await page.goto(url, { waitUntil: 'networkidle' });
  await page.waitForTimeout(800);
  await page.evaluate(() => { window.nc.session.settings.quality = 'low'; });
  await press('Enter'); // título → menu principal
  check(await menu() === 'main', 'menu principal');
}
async function quickLobby() {
  await press('ArrowDown', await mainIndex('corrida r(á|a)pida|quick race'));
  await press('Enter');
  // P1 entra (setas, Enter) e P2 (WASD, F). Com o 3D por software um quadro pode levar mais que a tecla: repete até entrar.
  const occupied = () => page.evaluate(() => document.querySelectorAll('.slot.occupied').length);
  for (let i = 0; i < 6 && (await occupied()) < 1; i++) { await press('Enter'); await page.waitForTimeout(150); }
  for (let i = 0; i < 6 && (await occupied()) < 2; i++) { await press('KeyF'); await page.waitForTimeout(150); }
  check(await menu() === 'lobby' && await page.evaluate(() => document.querySelectorAll('.slot.occupied').length) === 2, 'lobby da corrida rápida com P1 e P2');
}

await openGame();
await quickLobby();
check(JSON.stringify(await slotPaints()) === JSON.stringify(['original:Original', 'original:Original']), `os dois começam na Original (${await slotPaints()})`);
// P1: o cursor começa no carro; ↓ vai à pintura, → anda uma (Rubi). P2: S desce, D anda oito (Cobalto).
await press('ArrowDown');
check(await page.evaluate(() => document.querySelector('.slot.occupied .sel-paint')?.classList.contains('focus')), 'P1 com o foco na pintura');
await press('ArrowRight');
await press('KeyS');
await press('KeyD', 8);
const shown = await slotPaints();
check(JSON.stringify(shown) === JSON.stringify(['rubi:Rubi', 'cobalto:Cobalto']), `cartões mostram ${JSON.stringify(shown)}`);
const preview = await page.evaluate(() => [...document.querySelectorAll('.slot.occupied .car-card')].map((c) => `${c.dataset.car}:${c.dataset.paint}:${c.querySelector('.car-svg path')?.getAttribute('fill')}`));
check(preview[0] === 'falcao:rubi:#d7263d' && preview[1] === 'trovao:cobalto:#2346d6', `a prévia do carro troca de cor (${JSON.stringify(preview)})`);
const saved = await savedPaints();
check(saved?.[0]?.falcao === 'rubi' && saved?.[1]?.trovao === 'cobalto', `save guarda por assento e carro (${JSON.stringify(saved)})`);
await settle();
await page.screenshot({ path: `${out}-lobby.png` });

// Prontos (confirmar na pintura é PRONTO, como no carro) e larga pela pista.
await press('Enter'); await press('KeyF');
check(await page.evaluate(() => document.querySelectorAll('.slot.ready').length) === 2, 'confirmar no seletor de pintura marca PRONTO (os dois)');
for (let i = 0; i < 30 && !(await page.evaluate(() => !!document.querySelector('.btn-start.focus'))); i++) await press('ArrowDown');
await press('Enter');
check(await menu() === 'tracks', 'INICIAR abre as pistas');
await press('Enter');
await page.waitForTimeout(300);
const race = await page.evaluate(() => {
  const r = window.nc.session.race;
  const of = (seat) => r.paints[r.state.cars.findIndex((c) => c.seat === seat)];
  return { p1: of(0), p2: of(1), ai: r.state.cars.filter((c) => c.seat < 0).map((c) => r.paints[c.id]).filter((x) => x !== null).length, config: JSON.stringify(r.state.config).includes('paint') };
});
check(race.p1?.color === '#d7263d' && race.p2?.color === '#2346d6', `a corrida pinta cada carro na cor escolhida (${JSON.stringify([race.p1, race.p2])})`);
check(race.ai === 0, 'os rivais da IA ficam com a cor de fábrica');
check(!race.config, 'a config da corrida não carrega a pintura');
await page.keyboard.down('ArrowUp');
await page.evaluate(() => window.nc.session.debugStep(60 * 9));
await page.keyboard.up('ArrowUp');
// Para a foto: o P2 logo à frente do P1, à esquerda, e a IA longe dos dois (só os carros pintados na tela). A
// simulação para com speed = 0 (a pausa escureceria a tela com o "PAUSA").
await page.evaluate(() => {
  const r = window.nc.session.race; const s = r.state; const L = r.track.length;
  const [a, b] = [0, 1].map((seat) => s.cars.find((c) => c.seat === seat));
  b.z = (a.z + 650) % L; b.x = a.x - 0.45; b.speed = a.speed;
  for (const c of s.cars) if (c.seat < 0) c.z = (a.z + L / 2 + c.id * 300) % L;
});
await page.evaluate(() => { window.nc.session.settings.quality = 'medium'; window.nc.session.speed = 0; });
await settle();
await page.waitForTimeout(400);
await page.screenshot({ path: `${out}-corrida-3d.png` });

// Modo Retrô (Opções › Visual): o mesmo carro, a mesma cor.
await page.evaluate(() => { const s = window.nc.session; s.handleMenuEvent({ type: 'settingsChanged', settings: { ...s.settings, renderStyle: 'retro' } }); });
await settle();
await page.waitForTimeout(400);
const retro = await page.evaluate(() => window.nc.session.renderer.debugInfo?.().mode ?? null);
check(retro === 'retro', `modo Retrô ligado (${retro})`);
await page.screenshot({ path: `${out}-corrida-retro.png` });
await page.evaluate(() => { const s = window.nc.session; s.speed = 1; s.handleMenuEvent({ type: 'settingsChanged', settings: { ...s.settings, renderStyle: 'modern' } }); s.settings.quality = 'low'; s.handleMenuEvent({ type: 'toMain' }); });

// Garagem da carreira: P1 pinta o carro dele (↓ do carro vai à pintura).
await page.evaluate(() => {
  const s = window.nc.session; for (let i = 0; i < 4; i++) s.input.unbindSeat(i); ['kb1', 'kb2'].forEach((d, i) => s.input.bindSeat(i, d));
  s.handleMenuEvent({ type: 'startCareer', humans: [0, 1].map((i) => ({ seat: i, name: ['Ana', 'Bia'][i], carId: ['falcao', 'saci'][i], teamId: 0, color: '#fff' })), resume: false });
});
check(await menu() === 'garage', 'garagem da carreira aberta');
const garagePaints = () => page.evaluate(() => [...document.querySelectorAll('.gp .sel-paint')].map((e) => e.dataset.paint));
let garage = await garagePaints();
check(garage[0] === 'rubi' && garage[1] === 'original', `garagem: o Falcão do P1 com a Rubi escolhida no lobby, o Saci do P2 na Original (${JSON.stringify(garage)})`);
// A pintura fica na linha do PRONTO: do carro, ↑ dá a volta no PRONTO e chega nela.
// P1 (setas): ↑↑, → três (Limão). P2 (WASD): W W, A duas para trás (Grafite).
await press('ArrowUp', 2);
await press('ArrowRight', 3);
await press('KeyW', 2);
await press('KeyA', 2);
garage = await garagePaints();
const savedG = await savedPaints();
const gsvg = await page.evaluate(() => [...document.querySelectorAll('.gp .gp-car-visual .car-svg path')].map((p) => p.getAttribute('fill'))[0]);
check(garage[0] === 'limao' && savedG?.[0]?.falcao === 'limao' && gsvg === '#9bd61f', `P1 pinta o Falcão na garagem (${garage[0]}, desenho ${gsvg})`);
check(garage[1] === 'grafite' && savedG?.[1]?.saci === 'grafite', `P2 pinta o Saci na garagem (${garage[1]}, save ${JSON.stringify(savedG?.[1])})`);
await settle();
await page.screenshot({ path: `${out}-garagem.png` });

// Reabrir o jogo: o lobby volta com a escolha de cada assento.
await openGame();
await quickLobby();
const back = await slotPaints();
check(JSON.stringify(back) === JSON.stringify(['limao:Limão', 'cobalto:Cobalto']), `reabrir o jogo traz as pinturas de volta (${JSON.stringify(back)})`);

console.log('erros de página:', errors.length ? errors.join('\n') : 'nenhum');
await browser.close();
if (errors.length || fails.length) { console.log(`FALHOU: ${fails.length} conferências, ${errors.length} erros`); process.exit(1); }
console.log('playtest da pintura OK');
