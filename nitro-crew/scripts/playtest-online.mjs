// Playtest do online no Chromium (Playwright): dois "computadores" (duas páginas com armazenamento
// separado) no mesmo relay. Pelo fluxo real de teclado: menu → Online → servidor → criar sala /
// entrar com o código → pronto → o anfitrião troca a pista e larga. Depois correm 10 s de jogo
// com debugStep, e os hashes do lockstep têm que ser iguais; um cai e volta (snapshot); o outro
// fica parado para aparecer o "aguardando". Capturas em <prefixo>-*.png.
// Uso: node scripts/playtest-online.mjs [url-do-jogo] [prefixo] [ws://relay]
//      (sem relay, sobe server/relay.mjs numa porta livre; precisa de `npm ci` em server/)
import { spawn } from 'node:child_process';
import { chromium } from 'playwright';

const url = process.argv[2] ?? 'http://localhost:4174/';
const out = process.argv[3] ?? 'scratch/pto';
let relayUrl = process.argv[4] ?? '';
const exe = process.env.CHROME_PATH ?? '/opt/pw-browsers/chromium-1194/chrome-linux/chrome';

let relay = null;
if (!relayUrl) {
  relay = spawn(process.execPath, ['server/relay.mjs'], { env: { ...process.env, PORT: '0', HOST: '127.0.0.1', RELAY_QUIET: '1' }, stdio: ['ignore', 'pipe', 'inherit'] });
  const port = await new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error('relay não subiu')), 5000);
    relay.stdout.on('data', (d) => { const m = String(d).match(/:(\d+) \(/); if (m) { clearTimeout(timer); resolve(Number(m[1])); } });
  });
  relayUrl = `ws://127.0.0.1:${port}`;
}
console.log('relay:', relayUrl);

const browser = await chromium.launch({ executablePath: exe, args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist', '--autoplay-policy=no-user-gesture-required'] });
const errors = [];
const fails = [];
const check = (cond, msg) => { console.log(`${cond ? '✓' : '✗'} ${msg}`); if (!cond) fails.push(msg); };
const wait = (ms) => new Promise((r) => setTimeout(r, ms));

async function openPage(name) {
  const context = await browser.newContext({ viewport: SMALL });
  const page = await context.newPage();
  page.on('pageerror', (e) => errors.push(`${name} pageerror: ${e.message}`));
  page.on('console', (m) => { if (m.type() === 'error') errors.push(`${name} console: ${m.text()}`); });
  await page.goto(url, { waitUntil: 'networkidle' });
  await page.waitForTimeout(600);
  await page.evaluate(() => { window.nc.session.settings.quality = 'low'; });
  return page;
}

const info = (p) => p.evaluate(() => {
  const s = window.nc.session; const o = s.online; const d = o.debugInfo();
  return { menu: s.menus.current(), phase: o.phase, code: o.code, isHost: o.isHost, tick: s.race?.state.tick ?? null, seats: o.localSeats, room: o.room ? o.room.clients.length : 0, error: o.error, stats: d.stats, desyncs: d.desyncs.length, ping: o.ping, view: document.querySelector('.scr-online')?.dataset.view ?? null };
});
async function until(p, pred, label, timeoutMs = 20000) {
  const t0 = Date.now();
  let st = await info(p);
  while (!pred(st)) {
    if (Date.now() - t0 > timeoutMs) { check(false, `${label} (tempo esgotado: ${JSON.stringify(st)})`); return st; }
    await wait(150);
    st = await info(p);
  }
  return st;
}
/** Espera `fn` (avaliada na página) dar verdadeiro; devolve o último valor. Com o 3D por software um quadro leva segundos. */
async function waitFor(p, fn, arg, timeoutMs = 60000) {
  const t0 = Date.now();
  let v = await p.evaluate(fn, arg);
  while (!v && Date.now() - t0 < timeoutMs) { await wait(200); v = await p.evaluate(fn, arg); }
  return v;
}
const press = async (p, key, n = 1) => { for (let i = 0; i < n; i++) { await p.keyboard.press(key); await p.waitForTimeout(120); } };

/** Posição de um item do menu principal pelo texto (o menu muda com Carreira, Online, Continuar…; contar setas quebra). */
const mainIndex = (p, pattern) => p.evaluate((src) => {
  const re = new RegExp(src, 'i');
  const i = [...document.querySelectorAll('.scr-main .menu-list > *')].findIndex((el) => re.test((el.textContent ?? '').trim()));
  if (i < 0) throw new Error(`item do menu principal não encontrado: ${src}`);
  return i;
}, pattern);
// O 3D por software custa por pixel: fora das capturas as páginas ficam pequenas, senão um quadro
// leva segundos, a rede só é lida entre quadros e o lockstep anda 3 ticks a cada ida e volta.
const BIG = { width: 1280, height: 720 };
const SMALL = { width: 320, height: 180 };
async function shot(p, name) {
  await p.setViewportSize(BIG);
  await p.evaluate(() => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))));
  await p.screenshot({ path: `${out}-${name}.png`, timeout: 120000 });
  await p.setViewportSize(SMALL);
}

// ── Os dois computadores abrem o jogo e vão ao Online pelo menu principal.
const host = await openPage('anfitrião');
const guest = await openPage('convidado');
for (const p of [host, guest]) {
  await press(p, 'Enter'); // título → menu
  await press(p, 'ArrowDown', await mainIndex(p, '^online$'));
  await press(p, 'Enter');
}
let h = await info(host);
check(h.menu === 'online' && h.view === 'connect', `Online abre a tela de conexão (${h.menu}/${h.view})`);

// Servidor: desce até o campo, Enter entra no campo, digita, Enter salva.
for (const p of [host, guest]) {
  await press(p, 'ArrowDown', 3);
  await press(p, 'Enter');
  await p.keyboard.press('Control+A');
  await p.keyboard.type(relayUrl);
  await press(p, 'Enter');
}
const savedUrl = await host.evaluate(() => window.nc.session.settings.serverUrl);
check(savedUrl === relayUrl, `endereço do servidor salvo (${savedUrl})`);
await shot(host, '01-connect');

// Anfitrião: sobe até "Criar sala".
await press(host, 'ArrowUp', 3);
await press(host, 'Enter');
h = await until(host, (s) => s.phase === 'lobby' && s.code.length === 5 && s.room === 1, 'anfitrião cria a sala');
check(h.isHost, `anfitrião com código ${h.code}`);

// Convidado: campo do código, digita, Enter entra.
await press(guest, 'ArrowUp', 2);
await press(guest, 'Enter');
await guest.keyboard.type(h.code.toLowerCase());
await press(guest, 'Enter');
let g = await until(guest, (s) => s.phase === 'lobby' && s.room === 2, 'convidado entra na sala');
check(!g.isHost && g.code === h.code, `convidado na sala ${g.code}`);
h = await until(host, (s) => s.room === 2, 'anfitrião vê o convidado');

// Convidado troca de carro e fica pronto (itens: nome, carro, PRONTO, sair).
await press(guest, 'ArrowDown');
await press(guest, 'ArrowRight');
await press(guest, 'ArrowDown');
await press(guest, 'Enter');
const guestReady = await waitFor(guest, () => window.nc.session.online.ready);
check(guestReady, 'convidado pronto');
await shot(guest, '02-lobby-guest');

// Anfitrião: nome, carro, pista (→ próxima), voltas (← uma a menos), modo, dificuldade, carros, atraso, LARGAR.
await press(host, 'ArrowDown', 2);
await press(host, 'ArrowRight');
await press(host, 'ArrowDown');
await press(host, 'ArrowLeft');
await waitFor(guest, () => { const s = window.nc.session.online.room?.settings; return !!s && s.trackId !== 'copacabana' && s.laps === 2; });
const roomSettings = await guest.evaluate(() => window.nc.session.online.room.settings);
check(roomSettings.trackId !== 'copacabana' && roomSettings.laps === 2, `convidado vê a pista e as voltas escolhidas (${roomSettings.trackId}, ${roomSettings.laps} voltas)`);
await shot(host, '03-lobby-host');
await press(host, 'ArrowDown', 5); // modo, dificuldade, carros, atraso, LARGAR
await press(host, 'Enter');
h = await until(host, (s) => s.phase === 'racing' && s.tick !== null, 'largada no anfitrião');
g = await until(guest, (s) => s.phase === 'racing' && s.tick !== null, 'largada no convidado');
check(JSON.stringify(h.seats) === '[0]' && JSON.stringify(g.seats) === '[1]', `assentos: anfitrião ${JSON.stringify(h.seats)}, convidado ${JSON.stringify(g.seats)}`);
const vps = await guest.evaluate(() => document.querySelectorAll('#hud > *').length);
console.log('elementos no HUD do convidado:', vps);
await shot(guest, '04-countdown-guest');

// ── 10 s de corrida (600 ticks) nos dois, acelerando e virando.
const step = (p, n) => p.evaluate((k) => window.nc.session.debugStep(k), n);
for (const p of [host, guest]) await p.keyboard.down('ArrowUp');
async function raceTo(target, pages = [host, guest], timeoutMs = 240000) {
  const t0 = Date.now();
  for (;;) {
    const ticks = await Promise.all(pages.map((p) => p.evaluate(() => window.nc.session.race?.state.tick ?? 0)));
    if (ticks.every((t) => t >= target)) return ticks;
    if (Date.now() - t0 > timeoutMs) { check(false, `corrida parou em ${ticks.join(', ')} (alvo ${target})`); return ticks; }
    await Promise.all(pages.map((p, i) => (ticks[i] < target ? step(p, Math.min(12, target - ticks[i])) : null)));
    await wait(15);
  }
}
await raceTo(300);
await guest.keyboard.down('ArrowLeft'); await raceTo(360); await guest.keyboard.up('ArrowLeft');
await host.keyboard.down('ArrowRight'); await raceTo(420); await host.keyboard.up('ArrowRight');
await raceTo(600);
const hashes = async (tick) => Promise.all([host, guest].map((p) => p.evaluate((t) => window.nc.session.online.hashAt(t) ?? null, tick)));
let [ha, hg] = await hashes(600);
console.log('hash no tick 600 → anfitrião', ha, '· convidado', hg);
check(ha !== null && ha === hg, `mesmo hash no tick 600 (${ha} × ${hg})`);
const speeds = await Promise.all([host, guest].map((p) => p.evaluate(() => { const s = window.nc.session; return s.race.state.cars.filter((c) => c.seat >= 0).map((c) => Math.round(c.speed)); })));
// Os dois computadores podem estar em ticks um pouco diferentes aqui (o laço de quadros também
// roda); a igualdade do estado já foi provada pelo hash acima.
check(speeds.every((list) => list.length === 2 && list.every((v) => v > 1000)), `os dois carros andam nos dois computadores (${JSON.stringify(speeds)})`);
await host.waitForTimeout(800);
await shot(host, '05-race-host');
await shot(guest, '06-race-guest');

// ── Esc não pausa: abre "Sair da partida?"; Enter (Continuar) fecha.
await guest.keyboard.up('ArrowUp');
await press(guest, 'Escape');
g = await until(guest, (s) => s.view === 'quit', 'confirmação de saída aberta');
check(g.menu === 'online' && g.view === 'quit', `Esc abre a confirmação de saída (${g.menu}/${g.view})`);
const before = g.tick;
await raceTo(before + 30, [host, guest], 30000);
g = await info(guest);
check(g.tick > before, `a corrida continua com a confirmação aberta (${before} → ${g.tick})`);
await shot(guest, '07-quit-dialog');
await press(guest, 'Enter');
g = await until(guest, (s) => s.menu === null, 'confirmação fechada');
check(g.menu === null, `Continuar correndo fecha a confirmação (${g.menu})`);
await guest.keyboard.down('ArrowUp');

// ── O convidado para de mandar entrada (a simulação dele fica parada): o anfitrião espera e avisa.
// `paused` congela o convidado de verdade (com `speed = 0` o lockstep ainda o faz alcançar o
// anfitrião, um tick por quadro): é um computador travado que parou de mandar entrada.
await guest.evaluate(() => { window.nc.session.paused = true; });
// O anfitrião ainda roda o que já tem do convidado (até o tick dele + atraso) e então para.
for (let i = 0; i < 6; i++) { await step(host, 12); await wait(120); }
const hostTick = (await info(host)).tick;
for (let i = 0; i < 4; i++) { await step(host, 12); await wait(120); }
h = await info(host);
const missing = await host.evaluate(() => { const o = window.nc.session.online; return o.debugInfo().tick === null ? null : o.status().waiting.map((w) => w.seat); });
check(h.tick === hostTick, `anfitrião espera o convidado (parado no tick ${hostTick} → ${h.tick}, faltando ${JSON.stringify(missing)})`);
await host.evaluate(() => window.nc.session.debugStep(1));
const banner = (await waitFor(host, () => document.querySelector('.nc-online-banner.on')?.textContent ?? '')) || '';
check(/Aguardando/.test(banner), `aviso de espera no anfitrião ("${banner}")`);
await shot(host, '08-waiting');
await guest.evaluate(() => { window.nc.session.paused = false; });

// ── O convidado cai e volta: reconecta com o token e recebe o snapshot do anfitrião.
await guest.evaluate(() => window.nc.session.online.debugDropConnection());
await wait(300);
const reconnecting = await guest.evaluate(() => document.querySelector('.nc-online-banner.on')?.textContent ?? '');
console.log('convidado durante a queda:', reconnecting);
g = await until(guest, (s) => s.phase === 'racing' && s.stats !== null && s.tick !== null && s.tick >= (h.tick ?? 0) - 20, 'convidado reconectado', 30000);
const target = Math.ceil(((await info(host)).tick + 120) / 60) * 60;
await raceTo(target);
[ha, hg] = await hashes(target);
console.log(`hash no tick ${target} depois da reconexão → anfitrião`, ha, '· convidado', hg);
check(ha !== null && ha === hg, `mesmo hash depois da reconexão (${ha} × ${hg})`);
h = await info(host); g = await info(guest);
check(h.desyncs === 0 && g.desyncs === 0, `nenhuma dessincronia (${h.desyncs}, ${g.desyncs})`);
console.log('estatísticas anfitrião:', JSON.stringify(h.stats), 'ping', Math.round(h.ping ?? -1), 'ms');
console.log('estatísticas convidado:', JSON.stringify(g.stats), 'ping', Math.round(g.ping ?? -1), 'ms');
await shot(guest, '09-after-reconnect');
for (const p of [host, guest]) await p.keyboard.up('ArrowUp');

// ── O anfitrião sai da partida (Esc → Sair): o convidado herda a sala e a IA assume o carro dele.
await press(host, 'Escape');
await press(host, 'ArrowDown');
await press(host, 'Enter');
h = await until(host, (s) => s.menu === 'main', 'anfitrião no menu');
check(h.menu === 'main' && h.phase === 'idle', `anfitrião volta ao menu (${h.menu}, ${h.phase})`);
g = await until(guest, (s) => s.isHost, 'convidado vira anfitrião');
const gt = g.tick;
await raceTo(gt + 120, [guest], 60000);
const aiOnHostCar = await guest.evaluate(() => window.nc.session.race.state.cars.find((c) => c.seat === 0)?.ai !== null);
check(aiOnHostCar, 'a IA assumiu o carro de quem saiu');
await shot(guest, '10-ai-takeover');

console.log('erros de página:', errors.length ? errors.join('\n') : 'nenhum');
await browser.close();
relay?.kill();
if (errors.length || fails.length) { console.log(`FALHOU: ${fails.length} conferências, ${errors.length} erros`); process.exit(1); }
console.log('playtest online OK');
