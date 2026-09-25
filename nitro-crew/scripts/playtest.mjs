// Playtest automatizado no Chromium (Playwright): menus, corrida com 1 jogador, tela dividida
// com 3 e 4 jogadores, resultado e pausa. Exige `npm run preview` (porta 4174) em outro terminal.
// Uso: node scripts/playtest.mjs [url] [prefixo-das-capturas]
import { chromium } from 'playwright';
const url = process.argv[2] ?? 'http://localhost:4174/';
const out = process.argv[3] ?? 'scratch/pt';
const exe = process.env.CHROME_PATH ?? '/opt/pw-browsers/chromium-1194/chrome-linux/chrome';
const browser = await chromium.launch({ executablePath: exe, args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist', '--autoplay-policy=no-user-gesture-required'] });
const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
const errors = [];
page.on('pageerror', (e) => errors.push('pageerror: ' + e.message));
page.on('console', (m) => { if (m.type() === 'error') errors.push(`console: ${m.text()}`); });
const fails = [];
const check = (cond, msg) => { console.log(`${cond ? '✓' : '✗'} ${msg}`); if (!cond) fails.push(msg); };
const S = () => page.evaluate(() => {
  const s = window.nc.session; const r = s.race;
  return {
    menu: s.menus.current(), paused: s.paused,
    race: r ? { phase: r.state.phase, tick: r.state.tick, cars: r.state.cars.length, viewports: r.humans.length,
      teamNitro: r.state.teamNitro,
      humans: r.state.cars.filter((c) => c.seat >= 0).map((c) => ({ seat: c.seat, z: Math.round(c.z), x: +c.x.toFixed(2), speed: Math.round(c.speed), lap: c.lap, pos: c.position, nitro: c.nitroLeft, nitroActive: c.nitroTicks > 0, fuel: +c.fuel.toFixed(2) })) } : null,
  };
});
const humans = (n) => Array.from({ length: n }, (_, i) => ({ seat: i, name: `P${i + 1}`, carId: ['falcao', 'trovao', 'tornado', 'camelo'][i], teamId: 0, color: ['#ffd23f', '#3ddc84', '#4fc3f7', '#ff7ab6'][i] }));

await page.goto(url, { waitUntil: 'networkidle' });
await page.waitForTimeout(800);
// O Chromium daqui renderiza por software (swiftshader): qualidade baixa e passos síncronos da simulação.
await page.evaluate(() => { window.nc.session.settings.quality = 'low'; });
let st = await S();
check(st.menu === 'title', `abre na tela de título (${st.menu})`);
await page.screenshot({ path: `${out}-01-title.png` });

await page.keyboard.press('Enter'); await page.waitForTimeout(400);
st = await S();
check(st.menu === 'main', `Enter leva ao menu principal (${st.menu})`);
await page.screenshot({ path: `${out}-02-main.png` });

// Lobby pelo fluxo real: Campeonato → lobby; kb1 entra com Enter, kb2 com F.
await page.keyboard.press('Enter'); await page.waitForTimeout(400);
st = await S();
check(st.menu === 'lobby', `Campeonato abre o lobby (${st.menu})`);
await page.keyboard.press('Enter'); await page.waitForTimeout(250);
await page.keyboard.press('KeyF'); await page.waitForTimeout(250);
const bound = await page.evaluate(() => [0, 1, 2, 3].map((s) => window.nc.session.input.seatDevice(s)));
check(bound[0] === 'kb1' && bound[1] === 'kb2', `dois assentos ligados no lobby (${JSON.stringify(bound)})`);
await page.screenshot({ path: `${out}-03-lobby.png` });

// Corrida de 1 jogador pela API de depuração (não depende do DOM do lobby).
await page.evaluate((h) => { const s = window.nc.session; for (let i = 0; i < 4; i++) s.input.unbindSeat(i); s.debugBind(0, 'kb1'); s.startQuick('copacabana', 2, h); }, humans(1));
await page.waitForTimeout(300);
st = await S();
check(st.race && st.race.phase === 'countdown' && st.menu === null, `corrida começa na contagem (${st.race?.phase}, menu ${st.menu})`);
await page.screenshot({ path: `${out}-04-countdown.png` });
await page.keyboard.down('ArrowUp');
await page.evaluate(() => window.nc.session.debugStep(60 * 10));
await page.waitForTimeout(300);
st = await S();
const me = st.race.humans[0];
check(st.race.phase === 'racing' && me.speed > 3000, `acelerando: fase ${st.race.phase}, ${me.speed} u/s, z=${me.z}`);
await page.screenshot({ path: `${out}-05-racing.png` });
await page.keyboard.down('Space'); await page.evaluate(() => window.nc.session.debugStep(2)); await page.keyboard.up('Space');
await page.evaluate(() => window.nc.session.debugStep(30));
st = await S();
check(st.race.humans[0].nitroActive, `nitro ativo depois do Espaço (cargas próprias ${st.race.humans[0].nitro}, cofre ${JSON.stringify(st.race.teamNitro)})`);
await page.keyboard.down('ArrowRight'); await page.evaluate(() => window.nc.session.debugStep(40)); await page.waitForTimeout(400); await page.keyboard.up('ArrowRight');
await page.screenshot({ path: `${out}-06-nitro-steer.png` });
await page.keyboard.up('ArrowUp');

// Pausa e volta.
await page.keyboard.press('Escape'); await page.waitForTimeout(300);
st = await S();
check(st.paused && st.menu === 'pause', `Esc pausa (${st.paused}, ${st.menu})`);
await page.screenshot({ path: `${out}-07-pause.png` });
await page.keyboard.press('Enter'); await page.waitForTimeout(300);
st = await S();
check(!st.paused && st.menu === null, `Continuar despausa (${st.paused}, ${st.menu})`);

// Avança a simulação até o fim e confere a tela de resultado (a sessão mostra o resultado ~3 s depois).
await page.keyboard.down('ArrowUp');
for (let i = 0; i < 40; i++) { await page.evaluate(() => window.nc.session.debugStep(60 * 10)); st = await S(); if (st.race.phase === 'finished') break; }
await page.keyboard.up('ArrowUp');
for (let i = 0; i < 30; i++) { await page.waitForTimeout(500); st = await S(); if (st.menu === 'results') break; }
check(st.menu === 'results', `resultado aparece ao fim (${st.menu}, fase ${st.race?.phase})`);
await page.screenshot({ path: `${out}-08-results.png` });

// Tela dividida com 4 e com 3 jogadores.
await page.evaluate((h) => { const s = window.nc.session; s.menus.hide(); s.debugBind(0, 'kb1'); s.debugBind(1, 'kb2'); s.startQuick('sampa_noite', 2, h); }, humans(4));
await page.keyboard.down('ArrowUp'); await page.keyboard.down('KeyW');
await page.evaluate(() => window.nc.session.debugStep(60 * 8));
await page.waitForTimeout(600);
st = await S();
check(st.race && st.race.viewports === 4 && st.race.humans[0].speed > 1000 && st.race.humans[1].speed > 1000, `4 jogadores: P1 ${st.race?.humans[0].speed} u/s, P2 ${st.race?.humans[1].speed} u/s`);
await page.screenshot({ path: `${out}-09-split4.png` });
await page.keyboard.up('ArrowUp'); await page.keyboard.up('KeyW');
await page.evaluate((h) => { const s = window.nc.session; s.startQuick('monte_fuji', 2, h); }, humans(3));
await page.keyboard.down('ArrowUp'); await page.evaluate(() => window.nc.session.debugStep(60 * 8)); await page.waitForTimeout(600);
await page.screenshot({ path: `${out}-10-split3.png` });
await page.keyboard.up('ArrowUp');

// Medição de quadros por segundo com 3 viewports (swiftshader: só referência).
const fps = await page.evaluate(() => new Promise((resolve) => { let n = 0; const t0 = performance.now(); const tick = () => { n++; if (performance.now() - t0 < 2000) requestAnimationFrame(tick); else resolve(Math.round(n / 2)); }; requestAnimationFrame(tick); }));
console.log(`fps (3 viewports, swiftshader): ${fps}`);

// Menus restantes por teclado: volta ao principal e passeia pelas telas.
await page.evaluate(() => window.nc.session.handleMenuEvent({ type: 'toMain' }));
await page.waitForTimeout(300);
for (const [screen, file] of [['options', '11-options'], ['controls', '12-controls'], ['records', '13-records'], ['cups', '14-cups'], ['tracks', '15-tracks']]) {
  await page.evaluate((sc) => window.nc.session.menus.show(sc), screen);
  await page.waitForTimeout(300);
  const cur = await page.evaluate(() => window.nc.session.menus.current());
  check(cur === screen, `tela ${screen} abre (${cur})`);
  await page.screenshot({ path: `${out}-${file}.png` });
}

console.log('erros de página:', errors.length ? errors.join('\n') : 'nenhum');
await browser.close();
if (errors.length || fails.length) { console.log(`FALHOU: ${fails.length} conferências, ${errors.length} erros`); process.exit(1); }
console.log('playtest OK');
