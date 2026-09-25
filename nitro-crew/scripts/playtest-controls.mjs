// Playtest da tela de controles e da vibração pelo fluxo real, no Chromium (Playwright): teclado,
// mouse e um gamepad falso com motor de vibração que registra as chamadas. Cobre o que os testes em
// Node não alcançam (DOM da tela: captura, recusa, cancelamento, tempo limite, teste de entrada) e a
// corrida com o mapeamento gravado pela tela. Exige `npm run preview` (porta 4174) em outro terminal.
// Uso: node scripts/playtest-controls.mjs [url] [prefixo-das-capturas]
import { chromium } from 'playwright';
const url = process.argv[2] ?? 'http://localhost:4174/';
const out = process.argv[3] ?? 'scratch/ctl';
const exe = process.env.CHROME_PATH ?? '/opt/pw-browsers/chromium-1194/chrome-linux/chrome';
const browser = await chromium.launch({ executablePath: exe, args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist', '--autoplay-policy=no-user-gesture-required'] });
const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
page.setDefaultTimeout(180_000);
const errors = [];
page.on('pageerror', (e) => { errors.push('pageerror: ' + e.message); console.log(`  !! pageerror: ${e.message}`); });
page.on('console', (m) => { if (m.type() === 'error') { errors.push(`console: ${m.text()}`); console.log(`  !! console: ${m.text()}`); } });
const fails = [];
const check = (cond, msg) => { console.log(`${cond ? '✓' : '✗'} ${msg}`); if (!cond) fails.push(msg); };
const shot = (name) => page.screenshot({ path: `${out}-${name}.png`, timeout: 180_000 });

// Gamepad falso (Xbox, com motor de vibração que só registra as chamadas), ligado por window.__pad.on.
await page.addInitScript(() => {
  const mk = () => ({ pressed: false, value: 0, touched: false });
  const calls = [];
  const pad = {
    id: 'Xbox Wireless Controller (STANDARD GAMEPAD Vendor: 045e Product: 0b13)', index: 0, connected: true, mapping: 'standard', timestamp: 0,
    axes: [0, 0, 0, 0], buttons: Array.from({ length: 17 }, mk),
    vibrationActuator: { type: 'dual-rumble', effects: ['dual-rumble'], playEffect(type, params) { calls.push({ type, ...params }); return Promise.resolve('complete'); }, reset() { return Promise.resolve('complete'); } },
  };
  window.__pad = { pad, calls, on: false, press(i, v = true) { pad.buttons[i] = { pressed: v, value: v ? 1 : 0, touched: v }; } };
  Object.defineProperty(Navigator.prototype, 'getGamepads', { configurable: true, value() { return [window.__pad.on ? pad : null, null, null, null]; } });
});

const frames = (n = 2) => page.evaluate((k) => new Promise((resolve) => { let i = 0; const f = () => (++i >= k ? resolve() : requestAnimationFrame(f)); requestAnimationFrame(f); }), n);
const menu = () => page.evaluate(() => window.nc.session.menus.current());
const controls = () => page.evaluate(() => JSON.parse(JSON.stringify(window.nc.session.settings.controls)));
const statusText = () => page.evaluate(() => document.querySelector('.remap-status')?.textContent ?? '');
const statusKind = () => page.evaluate(() => document.querySelector('.remap-status')?.className ?? '');
const capturing = () => page.evaluate(() => { const c = document.querySelector('.bind-cell.capturing'); return c ? `${c.dataset.action}:${c.dataset.device}` : null; });
const focused = () => page.evaluate(() => { const c = document.querySelector('.scr-controls .focus'); return c ? (c.dataset.action ? `${c.dataset.action}:${c.dataset.device}` : c.textContent) : null; });
const key = async (code, n = 1) => { for (let i = 0; i < n; i++) { await page.keyboard.press(code); await frames(1); } };

/** Posição de um item do menu principal pelo texto (o menu muda com Carreira, Online, Continuar…; contar setas quebra). */
const mainIndex = (p, pattern) => p.evaluate((src) => {
  const re = new RegExp(src, 'i');
  const i = [...document.querySelectorAll('.scr-main .menu-list > *')].findIndex((el) => re.test((el.textContent ?? '').trim()));
  if (i < 0) throw new Error(`item do menu principal não encontrado: ${src}`);
  return i;
}, pattern);
const pad = async (i, v = true) => { await page.evaluate(([b, on]) => window.__pad.press(b, on), [i, v]); await frames(3); };

await page.goto(url, { waitUntil: 'networkidle' });
await page.evaluate(() => { window.nc.session.settings.quality = 'low'; });
await frames(2);
// O fundo 3D dos menus custa ~1 s por quadro no swiftshader: congela o cenário (o último quadro fica
// na tela) para o roteiro não levar minutos. (O renderizador já expõe `__idle` = câmera de depuração.)
const freezeIdle = () => page.evaluate(() => { const r = window.nc.session.renderer; if (!r.__ncFrozenIdle) { r.__ncFrozenIdle = r.renderIdle; r.renderIdle = () => undefined; } });
const thawIdle = () => page.evaluate(() => { const r = window.nc.session.renderer; if (r.__ncFrozenIdle) { r.renderIdle = r.__ncFrozenIdle; delete r.__ncFrozenIdle; } });
await freezeIdle();

// ── Menu principal → Controles, pelo teclado ──
await key('Enter');
check((await menu()) === 'main', 'Enter leva ao menu principal');
await key('ArrowDown', await mainIndex(page, '^controles$'));
await key('Enter');
check((await menu()) === 'controls', `Controles abre pelo menu (${await menu()})`);
const grid = await page.evaluate(() => ({ cells: document.querySelectorAll('.bind-cell').length, restore: document.querySelectorAll('.btn-restore').length, first: document.querySelector('.bind-cell')?.textContent }));
check(grid.cells === 24 && grid.restore === 3 && grid.first === '↑', `grade 8×3 com restaurar (${JSON.stringify(grid)})`);
check((await focused()) === 'throttle:kb1', `foco começa em Acelerar/Teclado 1 (${await focused()})`);
await frames(2);
await shot('01-tela');

// ── Captura no teclado 1: Nitro → R ──
await key('ArrowDown', 4);
check((await focused()) === 'nitro:kb1', `setas navegam a grade (${await focused()})`);
await key('Enter');
check((await capturing()) === 'nitro:kb1', `Enter abre a captura (${await capturing()})`);
check((await statusText()).includes('Nitro'), `aviso pede a tecla (${await statusText()})`);
await key('KeyR');
let c = await controls();
check(JSON.stringify(c.kb1.nitro) === '["KeyR"]' && (await capturing()) === null, `R vira o nitro do teclado 1 (${JSON.stringify(c.kb1.nitro)})`);
const stored = await page.evaluate(() => JSON.parse(localStorage.getItem('nitro-crew.settings') ?? '{}').controls?.kb1?.nitro);
check(JSON.stringify(stored) === '["KeyR"]', `gravado no localStorage (${JSON.stringify(stored)})`);
check((await menu()) === 'controls', 'a tecla capturada não fez mais nada');

// ── Conflito no mesmo dispositivo: Acelerar → R troca com o Nitro ──
await key('ArrowUp', 4);
await key('Enter');
await key('KeyR');
c = await controls();
check(JSON.stringify(c.kb1.throttle) === '["KeyR"]' && JSON.stringify(c.kb1.nitro) === '["ArrowUp"]', `troca: acelerar R, nitro ↑ (${JSON.stringify(c.kb1)})`);
check((await statusKind()).includes('warn') && (await statusText()).includes('era de'), `aviso de troca (${await statusText()})`);
await shot('03-troca');

// ── Conflito entre teclados: Frear (teclado 1) → W, que é o acelerar do teclado 2 ──
await key('ArrowDown');
await key('Enter');
await key('KeyW');
c = await controls();
const conflicts = await page.evaluate(() => ({ cells: [...document.querySelectorAll('.bind-cell.conflict')].map((e) => `${e.dataset.action}:${e.dataset.device}`), text: document.querySelector('.remap-conflicts')?.textContent ?? '' }));
check(JSON.stringify(c.kb1.brake) === '["KeyW"]' && conflicts.cells.includes('brake:kb1') && conflicts.cells.includes('throttle:kb2'), `W nos dois teclados é marcado (${JSON.stringify(conflicts)})`);
await shot('04-conflito');

// ── Esc cancela a captura (e não sai da tela); recusa de tecla proibida ──
await key('Enter');
await key('F7');
check((await capturing()) === 'brake:kb1' && (await statusKind()).includes('warn') && (await statusText()).startsWith('F7 não pode ser usada'), `F7 é recusada e a captura continua (${await statusText()})`);
await key('Escape');
check((await capturing()) === null && (await menu()) === 'controls', `Esc cancela sem sair da tela (${await menu()})`);
check(JSON.stringify((await controls()).kb1.brake) === '["KeyW"]', 'cancelar não muda nada');

// ── Tempo limite de 5 s (e a captura de tela do estado "esperando a tecla") ──
await key('Enter');
const t0 = Date.now();
await shot('02-captura');
// O relógio da captura anda com o dt limitado da sessão (≥ 20 quadros): sob carga, 5 s viram mais.
while ((await capturing()) !== null && Date.now() - t0 < 180_000) await page.waitForTimeout(500);
const waited = (Date.now() - t0) / 1000;
check((await capturing()) === null && waited >= 4.5, `captura desiste sozinha em ~5 s (${waited.toFixed(1)} s, ${await statusText()})`);

// ── Teste de entrada: setas e R (acelerar remapeado) mexem as barras do teclado 1 ──
await page.keyboard.down('ArrowLeft');
await page.keyboard.down('KeyR');
await frames(3);
const bars = await page.evaluate(() => { const r = document.querySelector('.remap-device[data-device="kb1"]'); return { steer: r?.querySelector('.steer-fill')?.style.width, left: r?.querySelector('.steer-fill')?.style.left, gas: r?.querySelector('.pedal-fill')?.style.width, active: r?.classList.contains('active') }; });
check(bars.steer === '50%' && bars.left === '0%' && bars.gas === '100%' && bars.active, `barras do teclado 1 reagem (${JSON.stringify(bars)})`);
await shot('05-teste');
await page.keyboard.up('ArrowLeft');
await page.keyboard.up('KeyR');
await frames(2);

// ── Gamepad conectado ao vivo ──
await page.evaluate(() => { window.__pad.on = true; });
await frames(3);
check(await page.evaluate(() => !!document.querySelector('.remap-device[data-device="gp0"]')), 'controle aparece na lista ao conectar');
await pad(7);
const gpActive = await page.evaluate(() => { const r = document.querySelector('.remap-device[data-device="gp0"]'); return { gas: r?.querySelector('.pedal-fill')?.style.width, active: r?.classList.contains('active') }; });
check(gpActive.gas === '100%' && gpActive.active, `RT acende o acelerar do controle (${JSON.stringify(gpActive)})`);
await shot('06-controle');
await pad(7, false);

// ── Captura no gamepad pelo próprio controle: A abre, o A segurado não conta, LT vira o nitro ──
// foco: Frear/teclado 1 (3) → Nitro/controles (14)
await key('ArrowDown', 3);
await key('ArrowRight', 2);
check((await focused()) === 'nitro:gamepad', `foco em Nitro/Controles (${await focused()})`);
await pad(0);
check((await capturing()) === 'nitro:gamepad', `A abre a captura do controle (${await capturing()})`);
await frames(3);
check((await capturing()) === 'nitro:gamepad', 'o A que abriu a captura não vira a tecla nova');
await pad(0, false);
await pad(6);
c = await controls();
check(JSON.stringify(c.gamepad.nitro) === '[6]' && JSON.stringify(c.gamepad.brake) === '[2,1]', `LT vira o nitro e sai do freio (${JSON.stringify(c.gamepad)})`);
await shot('07-controle-captura');
await pad(6, false);
check((await focused()) === 'nitro:gamepad', `foco não andou com o botão segurado (${await focused()})`);
await pad(0);
await pad(0, false);
await pad(9);
check((await capturing()) === null && JSON.stringify((await controls()).gamepad.nitro) === '[6]', `Start cancela a captura do controle (${await statusText()})`);
await pad(9, false);

// ── Restaurar padrão do controle (teclado segue remapeado) ──
await key('ArrowDown', 4);
check((await focused())?.toString().length > 0 && (await page.evaluate(() => document.querySelector('.scr-controls .focus')?.dataset.restore)) === 'gamepad', 'foco no Padrão dos controles');
await key('Enter');
c = await controls();
check(JSON.stringify(c.gamepad.nitro) === '[5]' && JSON.stringify(c.gamepad.brake) === '[2,1,6]' && JSON.stringify(c.kb1.throttle) === '["KeyR"]', `Padrão restaura só os controles (${JSON.stringify(c.gamepad.nitro)})`);

// ── Mouse: clicar numa célula captura; clicar fora cancela ──
await page.click('.bind-cell[data-action="gearUp"][data-device="kb2"]');
await frames(1);
check((await capturing()) === 'gearUp:kb2', `clique abre a captura (${await capturing()})`);
await page.mouse.click(640, 30);
await frames(1);
check((await capturing()) === null, 'clique fora cancela');

// ── Pausa do teclado 2 no R, que é o acelerar do teclado 1: vira aviso de conflito ──
await page.click('.bind-cell[data-action="pause"][data-device="kb2"]');
await frames(1);
await key('KeyR');
c = await controls();
const kbConflict = await page.evaluate(() => [...document.querySelectorAll('.bind-cell.conflict')].map((e) => `${e.dataset.action}:${e.dataset.device}`));
check(JSON.stringify(c.kb2.pause) === '["KeyR"]' && kbConflict.includes('pause:kb2') && kbConflict.includes('throttle:kb1'), `R pausa o teclado 2 e acelera o 1, com aviso (${JSON.stringify(kbConflict)})`);

// ── Pausa do controle no A, pela tela: o Home é recusado (no masculino) e o A é gravado ──
await page.click('.bind-cell[data-action="pause"][data-device="gamepad"]');
await frames(1);
check((await capturing()) === 'pause:gamepad', `clique abre a captura da pausa do controle (${await capturing()})`);
await pad(16);
check((await capturing()) === 'pause:gamepad' && (await statusText()).startsWith('Home não pode ser usado — escolha outro.'), `Home é recusado no masculino (${await statusText()})`);
await pad(16, false);
await pad(0);
c = await controls();
check(JSON.stringify(c.gamepad.pause) === '[0]' && JSON.stringify(c.gamepad.throttle) === '[7]', `A vira a pausa do controle e sai do acelerar (${JSON.stringify(c.gamepad)})`);
await pad(0, false);
await shot('08-final');

// ── Os menus continuam nas setas/Esc com o teclado remapeado ──
await key('Escape');
check((await menu()) === 'main', `Esc volta ao menu principal (${await menu()})`);
const before = await page.evaluate(() => document.querySelector('.btn-main.focus')?.textContent);
await key('ArrowDown');
const after = await page.evaluate(() => document.querySelector('.btn-main.focus')?.textContent);
check(before !== after, `↓ ainda navega o menu (${before} → ${after})`);
await key('ArrowUp');

// ── Opção de vibração ──
await page.evaluate(() => window.nc.session.menus.show('options'));
await frames(2);
await page.waitForTimeout(800); // animação de entrada da tela
const vib = await page.evaluate(() => [...document.querySelectorAll('.sel')].find((e) => e.textContent?.includes('Vibração'))?.textContent ?? null);
check(vib !== null && vib.includes('Ligado'), `opção Vibração nas opções (${vib})`);
await shot('09-opcoes');

// ── Corrida com o teclado remapeado: R acelera, ↑ não ──
await thawIdle();
const humans = (n) => Array.from({ length: n }, (_, i) => ({ seat: i, name: `P${i + 1}`, carId: 'falcao', teamId: 0, color: '#ffd23f' }));
await page.evaluate((h) => { const s = window.nc.session; s.menus.hide(); for (let i = 0; i < 4; i++) s.input.unbindSeat(i); s.debugBind(0, 'kb1'); s.startQuick('copacabana', 2, h); }, humans(1));
await page.keyboard.down('KeyR');
// Quadros de verdade com o R apertado: é no quadro (não no debugStep) que a pausa é lida.
await frames(2);
const pausedByR = await page.evaluate(() => ({ paused: window.nc.session.paused, menu: window.nc.session.menus.current() }));
check(!pausedByR.paused && pausedByR.menu === null, `R do teclado 1 não pausa pela pausa do teclado 2, que está sem assento (${JSON.stringify(pausedByR)})`);
await page.evaluate(() => window.nc.session.debugStep(60 * 8));
let speed = await page.evaluate(() => Math.round(window.nc.session.race.state.cars.find((x) => x.seat === 0).speed));
await page.keyboard.up('KeyR');
check(speed > 800, `R (acelerar remapeado) acelera na corrida (${speed} u/s)`);
await page.evaluate((h) => window.nc.session.startQuick('copacabana', 2, h), humans(1));
await page.keyboard.down('ArrowUp');
await page.evaluate(() => window.nc.session.debugStep(60 * 6));
speed = await page.evaluate(() => Math.round(window.nc.session.race.state.cars.find((x) => x.seat === 0).speed));
await page.keyboard.up('ArrowUp');
check(speed < 100, `↑ não acelera mais o teclado 1 (${speed} u/s)`);

// ── Vibração do controle na corrida: largada, nitro; desligada = nada ──
// Contra-relógio: só o carro humano, então nenhuma batida entre carros encobre o pulso do nitro
// (um tremor mais fraco não corta um mais forte que ainda está tocando).
await page.evaluate((h) => { const s = window.nc.session; s.input.unbindSeat(0); s.input.bindSeat(0, 'gp0'); window.__pad.calls.length = 0; s.startQuick('copacabana', 2, h, true); }, humans(1));
await pad(7);
await page.evaluate(() => window.nc.session.debugStep(60 * 4));
let calls = await page.evaluate(() => window.__pad.calls.map((c) => `${c.type}:${c.strongMagnitude}:${c.duration}`));
check(calls.some((c) => c.startsWith('dual-rumble:0.55')), `largada vibra o controle (${calls.slice(0, 4).join(', ')})`);
await page.waitForTimeout(400);
await pad(5);
await page.evaluate(() => window.nc.session.debugStep(2));
await pad(5, false);
calls = await page.evaluate(() => window.__pad.calls.map((c) => `${c.type}:${c.strongMagnitude}:${c.duration}`));
check(calls.some((c) => c.startsWith('dual-rumble:0.35:110')), `nitro dá um tremor curto (${calls.join(', ')})`);
// Grama: acelerando e virando tudo para a esquerda, o carro sai do asfalto; pulsos fracos e espaçados.
await page.evaluate(() => { window.__pad.calls.length = 0; });
await pad(14);
await page.evaluate(() => window.nc.session.debugStep(60 * 4));
await pad(14, false);
calls = await page.evaluate(() => window.__pad.calls.map((c) => `${c.type}:${c.strongMagnitude}:${c.duration}`));
const grass = calls.filter((c) => c.startsWith('dual-rumble:0.16:'));
check(grass.length > 0 && grass.length < 60 * 4 / 5, `grama vibra fraco e com limite de frequência (${grass.length} pulsos em 4 s; batidas: ${calls.filter((c) => c.startsWith('dual-rumble:1:')).length})`);
await page.evaluate(() => { const s = window.nc.session; s.settings.vibration = false; window.__pad.calls.length = 0; });
await pad(5);
await page.evaluate(() => window.nc.session.debugStep(60 * 3));
await pad(5, false);
calls = await page.evaluate(() => window.__pad.calls.length);
check(calls === 0, `vibração desligada: nenhuma chamada (${calls})`);
await pad(7, false);

// ── Pausa escolhida no A (gravada pela tela): abre e fica aberta; no menu, o A confirma "Continuar" ──
const pauseState = () => page.evaluate(() => ({ paused: window.nc.session.paused, menu: window.nc.session.menus.current() }));
await pad(0);
let ps = await pauseState();
check(ps.paused && ps.menu === 'pause', `A pausa a corrida (${JSON.stringify(ps)})`);
await pad(0, false);
ps = await pauseState();
check(ps.paused && ps.menu === 'pause', `a pausa no A fica aberta depois de soltar (${JSON.stringify(ps)})`);
await shot('10-pausa-a');
await pad(0);
ps = await pauseState();
check(!ps.paused && ps.menu === null, `A no menu de pausa continua a corrida (${JSON.stringify(ps)})`);
await pad(0, false);

// ── Recarregar mantém o remapeamento ──
await page.reload({ waitUntil: 'networkidle' });
await frames(2);
c = await controls();
check(JSON.stringify(c.kb1.throttle) === '["KeyR"]' && JSON.stringify(c.kb1.brake) === '["KeyW"]' && JSON.stringify(c.gamepad.pause) === '[0]' && JSON.stringify(c.kb2.pause) === '["KeyR"]', `remapeamento sobrevive ao recarregar (${JSON.stringify({ kb1: c.kb1.throttle, kb2: c.kb2.pause, gp: c.gamepad.pause })})`);

console.log('erros de página:', errors.length ? errors.join('\n') : 'nenhum');
await browser.close();
if (errors.length || fails.length) { console.log(`FALHOU: ${fails.length} conferências, ${errors.length} erros`); process.exit(1); }
console.log('playtest de controles OK');
