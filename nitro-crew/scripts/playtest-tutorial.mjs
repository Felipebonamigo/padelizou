// Playtest do tutorial de 90 segundos (passo 1.9) pelo fluxo real, com teclado: oferta no menu
// principal, "Como jogar", entrada de 2 jogadores (teclado 1 e 2), corrida guiada inteira dirigida
// por teclas (setas / WASD) com debugStep, uma captura por passo, tela final → primeira copa; depois,
// Esc pula (1 jogador) e mostra as regras de ouro; o menu para de oferecer. Laço de quadros parado
// (como pistas-ui.mjs): cada quadro é chamado à mão, para o 3D por software não mandar no tempo.
// Exige `npm run preview` (porta 4174) em outro terminal. Uso: node scripts/playtest-tutorial.mjs [url] [prefixo-das-capturas]
import { chromium } from 'playwright';
const url = process.argv[2] ?? 'http://localhost:4174/';
const out = process.argv[3] ?? 'scratch/tut';
const exe = '/opt/pw-browsers/chromium-1194/chrome-linux/chrome';
const browser = await chromium.launch({ executablePath: exe, args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
const BIG = { width: 1280, height: 720 };
const SMALL = { width: 320, height: 180 };
const page = await browser.newPage({ viewport: BIG });
page.setDefaultTimeout(240000);
const errors = [];
page.on('pageerror', (e) => errors.push('pageerror: ' + e.message));
page.on('console', (m) => { if (m.type() === 'error') errors.push(`console: ${m.text()}`); });
const fails = [];
const check = (cond, msg) => { console.log(`${cond ? '✓' : '✗'} ${msg}`); if (!cond) fails.push(msg); };
const t0 = Date.now();
const log = (msg) => console.log(`[${Math.round((Date.now() - t0) / 1000)}s] ${msg}`);

const frame = () => page.evaluate(() => window.nc.session.frame(performance.now()));
async function held(code) { await page.keyboard.down(code); await frame(); await page.keyboard.up(code); await frame(); }
const menu = () => page.evaluate(() => window.nc.session.menus.current());
async function shot(name) {
  await page.setViewportSize(BIG);
  await page.evaluate(() => { window.dispatchEvent(new Event('resize')); });
  await frame(); await frame();
  await page.evaluate(() => { for (const a of document.getAnimations()) { try { a.finish(); } catch { /* animação infinita */ } } });
  await page.screenshot({ path: `${out}-${name}.png` });
}
const mainIndex = (pattern) => page.evaluate((src) => {
  const re = new RegExp(src, 'i');
  const i = [...document.querySelectorAll('.scr-main .menu-list > *')].findIndex((el) => re.test((el.textContent ?? '').trim()));
  if (i < 0) throw new Error(`item do menu principal não encontrado: ${src}`);
  return i;
}, pattern);

await page.goto(url, { waitUntil: 'networkidle' });
await page.waitForTimeout(800);
await page.evaluate(() => { window.nc.session.settings.quality = 'low'; window.nc.session.stop(); });
await frame();

// ── Menu principal: oferta na primeira abertura; "Como jogar" pelo texto.
await held('Enter');
check(await menu() === 'main', `título → menu principal (${await menu()})`);
check(await page.evaluate(() => !!document.querySelector('.scr-main .tut-offer')), 'primeira abertura: o menu oferece o tutorial');
// Pior caso de altura: o menu com "Continuar" e "Sair" (13 itens hoje) tem que caber em 1280×720.
const fit = await page.evaluate(() => {
  const list = document.querySelector('.scr-main .menu-list');
  const first = list.firstElementChild;
  const cont = first.cloneNode(true); cont.textContent = 'Continuar'; list.prepend(cont);
  const quit = first.cloneNode(true); quit.textContent = 'Sair'; quit.classList.add('btn-quit'); list.append(quit);
  const box = document.querySelector('.scr-main .main-left').getBoundingClientRect();
  const r = { items: list.children.length, top: Math.round(box.top), bottom: Math.round(box.bottom), h: innerHeight };
  cont.remove(); quit.remove();
  return r;
});
check(fit.items >= 12 && fit.top >= 0 && fit.bottom <= fit.h, `menu com ${fit.items} itens cabe em 720p (painel ${fit.top}..${fit.bottom} de ${fit.h})`);
await page.evaluate(() => {
  const list = document.querySelector('.scr-main .menu-list');
  const first = list.firstElementChild;
  const cont = first.cloneNode(true); cont.textContent = 'Continuar'; cont.classList.remove('focus'); list.prepend(cont);
  const quit = first.cloneNode(true); quit.textContent = 'Sair'; quit.classList.remove('focus'); quit.classList.add('btn-quit'); list.append(quit);
  window.__fake = [cont, quit];
});
await shot('01-main-12-itens');
await page.evaluate(() => { for (const el of window.__fake) el.remove(); });
const idx = await mainIndex('^como jogar$');
for (let i = 0; i < idx; i++) await held('ArrowDown');
await shot('02-main-como-jogar');
await held('Enter');
check(await menu() === 'tutorial', `Como jogar abre a tela do tutorial (${await menu()})`);

// ── Entrada: kb1 com Enter, kb2 com F (a tela ignora o primeiro quadro).
await frame();
for (let i = 0; i < 6 && await page.evaluate(() => window.nc.session.input.seatDevice(0)) !== 'kb1'; i++) await held('Enter');
for (let i = 0; i < 6 && await page.evaluate(() => window.nc.session.input.seatDevice(1)) !== 'kb2'; i++) await held('KeyF');
const seats = await page.evaluate(() => [0, 1, 2, 3].map((s) => window.nc.session.input.seatDevice(s)));
check(seats[0] === 'kb1' && seats[1] === 'kb2', `P1 no teclado 1 e P2 no teclado 2 (${JSON.stringify(seats)})`);
await shot('03-como-jogar-2p');
await held('Enter'); // COMEÇAR (foco já no botão)
let st = await page.evaluate(() => { const s = window.nc.session; return { menu: s.menus.current(), track: s.race?.track.def.id, cars: s.race?.state.cars.length, fuel: s.race?.state.cars.map((c) => c.fuel) }; });
check(st.menu === null && st.track === 'tutorial' && st.cars === 2 && st.fuel.every((f) => f < 0.3), `COMEÇAR: corrida do tutorial, 2 carros, sem IA, tanque baixo (${JSON.stringify(st)})`);

// ── Corrida guiada, dirigida por teclas. Cada jogador segue o painel dele.
const KEYS = [
  { up: 'ArrowUp', down: 'ArrowDown', left: 'ArrowLeft', right: 'ArrowRight', nitro: 'Space' },
  { up: 'KeyW', down: 'KeyS', left: 'KeyA', right: 'KeyD', nitro: 'KeyF' },
];
const held2 = new Set();
async function setKey(code, on) {
  if (on && !held2.has(code)) { await page.keyboard.down(code); held2.add(code); }
  if (!on && held2.has(code)) { await page.keyboard.up(code); held2.delete(code); }
}
const snapshot = () => page.evaluate(() => {
  const s = window.nc.session; const r = s.race; const tut = s.tutorial.current();
  if (!r || !tut) return null;
  const seg = (c) => Math.floor(c.z / 200);
  return {
    tick: r.state.tick, start: r.state.startTick, phase: r.state.phase, towSeat: tut.towSeat, parked: tut.parked, tphase: tut.phase,
    steps: [...document.querySelectorAll('.nc-tut .tut-vp')].map((v) => v.dataset.step),
    retry: [...document.querySelectorAll('.nc-tut .tut-retry')].map((v) => v.textContent),
    cars: r.state.cars.map((c) => ({ seat: c.seat, seg: seg(c), x: c.x, speed: c.speed, top: c.stats.topSpeed, nitroTicks: c.nitroTicks, curve: r.track.segments[seg(c)].curve, progress: c.progress, fuel: c.fuel, inPit: c.inPit, handling: c.stats.handling })),
  };
});
// Mesma regra do teste (tests/tutorial.test.ts: botInput), com o volante digital das teclas.
const STRONG = { start: 310, end: 400 }; const PIT = { start: 560, end: 610 };
function decide(s, seat) {
  const car = s.cars.find((c) => c.seat === seat);
  const step = s.steps[seat];
  const sf = Math.min(1, car.speed / car.top);
  const steerRate = 2.2 * (0.75 + 0.35 * car.handling);
  const centr = 2.2 * 0.3 * (1.2 - 0.4 * car.handling);
  const limit = Math.min(1, (0.85 * steerRate) / (centr * 6)) * car.top;
  let lane = seat === 0 ? -0.3 : 0.3; let up = true; let down = false;
  if (car.seg >= STRONG.start - 45 && car.seg < STRONG.end - 10 && car.speed > limit * 0.9) { up = false; down = true; }
  const nitro = step === 'nitro' && car.nitroTicks === 0;
  if (step === 'pit' && car.seg >= PIT.start - 25 && car.seg < PIT.end) {
    if (car.seg >= PIT.start - 4) lane = 1.55;
    if (car.speed > car.top * 0.3) { up = false; down = true; }
  }
  if (step === 'tow' && s.towSeat >= 0) {
    const target = s.cars.find((c) => c.seat === s.towSeat);
    lane = target.x;
    const gap = target.progress - car.progress;
    if (s.parked && target.speed > target.top * 0.2 && gap > 0 && gap < 200 * 25) { up = false; down = car.speed > 1500; }
  }
  if (step === 'wait') { up = car.seg < 40 || car.seg > 600 ? true : car.speed < car.top * 0.5; }
  const counter = sf > 0.05 ? (centr * sf * car.curve) / steerRate : 0;
  const steer = (lane - car.x) * 5 + counter;
  return { up, down, left: steer < -0.25, right: steer > 0.25, nitro };
}
await page.setViewportSize(SMALL);
await page.evaluate(() => { window.dispatchEvent(new Event('resize')); });
const seen = new Set();
const shots = [];
for (let i = 0; i < 1400; i++) {
  const s = await snapshot();
  if (!s) break;
  // Uma captura na primeira vez que o P1 (ou o P2, no empurrão) mostra cada passo.
  for (const [seat, step] of s.steps.entries()) {
    const key = `${step}`;
    if (!seen.has(key) && (seat === 0 || step === 'tow')) {
      seen.add(key);
      for (const code of held2) await page.keyboard.up(code);
      held2.clear();
      const n = String(shots.length + 4).padStart(2, '0');
      await shot(`${n}-passo-${step}`);
      shots.push(step);
      log(`passo ${step} (P${seat + 1}), tick ${s.tick - s.start}`);
      await page.setViewportSize(SMALL);
      await page.evaluate(() => { window.dispatchEvent(new Event('resize')); });
    }
  }
  if (s.parked && !seen.has('parked')) {
    seen.add('parked');
    // Deixa o carro frear até quase parar antes da captura (o empurrão exige < 20% da máxima).
    const target = s.cars.find((c) => c.seat === s.towSeat);
    if (target.speed < target.top * 0.2) {
      for (const code of held2) await page.keyboard.up(code);
      held2.clear();
      await shot(`${String(shots.length + 4).padStart(2, '0')}-passo-parado`);
      shots.push('parado');
      log(`carro do P${s.towSeat + 1} parado, tick ${s.tick - s.start}`);
      await page.setViewportSize(SMALL);
      await page.evaluate(() => { window.dispatchEvent(new Event('resize')); });
    } else seen.delete('parked');
  }
  if (s.retry.some((r) => r) && !seen.has(`retry:${s.retry.join('|')}`)) { seen.add(`retry:${s.retry.join('|')}`); log(`aviso: ${JSON.stringify(s.retry)}`); }
  for (const seat of [0, 1]) {
    const d = decide(s, seat);
    const k = KEYS[seat];
    await setKey(k.up, d.up); await setKey(k.down, d.down); await setKey(k.left, d.left); await setKey(k.right, d.right);
    await setKey(k.nitro, d.nitro);
  }
  await page.evaluate(() => window.nc.session.debugStep(4));
  for (const seat of [0, 1]) await setKey(KEYS[seat].nitro, false);
}
for (const code of held2) await page.keyboard.up(code);
held2.clear();
st = await page.evaluate(() => ({ menu: window.nc.session.menus.current(), race: !!window.nc.session.race, save: JSON.parse(localStorage.getItem('nitro-crew.save') ?? 'null') }));
check(st.menu === 'tutorialDone' && !st.race, `tutorial concluído → tela final (${st.menu}); passos vistos: ${shots.join(', ')}`);
check(['throttle', 'curve', 'brake', 'nitro', 'pit', 'tow'].every((s) => shots.includes(s)), 'os 6 passos apareceram no painel');
check(st.save?.tutorialDone === true, `flag tutorialDone gravada no save (${st.save?.tutorialDone})`);
const done = await page.evaluate(() => ({ title: document.querySelector('.scr-tutorialDone .screen-title')?.textContent, rules: document.querySelectorAll('.tut-rules li').length }));
check(done.title === 'Parabéns!' && done.rules === 5, `tela final: ${done.title}, ${done.rules} regras de ouro (co-op inclui o empurrão)`);
await shot(`${String(shots.length + 4).padStart(2, '0')}-final-parabens`);
await held('Enter');
st = await page.evaluate(() => { const s = window.nc.session; return { menu: s.menus.current(), mode: s.race?.mode, track: s.race?.track.def.id, humans: s.race?.humans.length }; });
check(st.menu === null && st.mode === 'cup' && st.track === 'copacabana' && st.humans === 2, `"Correr a Copa Brasil" começa a primeira copa com os 2 (${JSON.stringify(st)})`);
await shot('20-primeira-copa');

// ── Segunda rodada: 1 jogador, Esc pula e mostra só as regras de ouro; o menu parou de oferecer.
await page.evaluate(() => window.nc.session.handleMenuEvent({ type: 'toMain' }));
await frame();
check(await page.evaluate(() => !document.querySelector('.scr-main .tut-offer')), 'depois do tutorial o menu não oferece mais');
const idx2 = await mainIndex('^como jogar$');
for (let i = 0; i < idx2; i++) await held('ArrowDown');
await held('Enter');
await frame();
for (let i = 0; i < 6 && await page.evaluate(() => window.nc.session.input.seatDevice(0)) !== 'kb1'; i++) await held('Enter');
await held('Enter');
check(await page.evaluate(() => window.nc.session.race?.track.def.id) === 'tutorial', 'tutorial de 1 jogador começou');
await page.evaluate(() => window.nc.session.debugStep(60 * 4));
await shot('21-solo-passo1');
const solo = await page.evaluate(() => document.querySelector('.nc-tut .tut-counter')?.textContent);
check(solo === 'PASSO 1/5', `sozinho são 5 passos (${solo})`);
await held('Escape');
st = await page.evaluate(() => ({ menu: window.nc.session.menus.current(), race: !!window.nc.session.race, title: document.querySelector('.scr-tutorialDone .screen-title')?.textContent, rules: document.querySelectorAll('.tut-rules li').length, panel: document.querySelector('.nc-tut')?.classList.contains('hidden') }));
check(st.menu === 'tutorialDone' && !st.race && st.title === 'Regras de ouro' && st.rules === 4 && st.panel === true, `Esc pula: ${JSON.stringify(st)}`);
await shot('22-pulado-regras');
await held('ArrowDown'); await held('Enter');
check(await menu() === 'main', `"Menu principal" volta ao menu (${await menu()})`);

console.log(errors.length ? `erros de página:\n${errors.join('\n')}` : 'erros de página: nenhum');
if (errors.length) fails.push('erros de página');
await browser.close();
console.log(fails.length ? `FALHOU (${fails.length})` : 'tutorial OK');
process.exit(fails.length ? 1 : 0);
