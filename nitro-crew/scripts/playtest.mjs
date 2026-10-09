// Playtest automatizado no Chromium (Playwright): menus, corrida com 1 jogador, tela dividida
// com 3 e 4 jogadores, resultado e pausa. Exige `npm run preview` (porta 4174) em outro terminal.
// Uso: node scripts/playtest.mjs [url] [prefixo-das-capturas]
import { chromium } from 'playwright';
const url = process.argv[2] ?? 'http://localhost:4174/?nosurvey=1';
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
// Entradas do jogador (K1, fluxo sem atrito): cada tecla apertada pelo roteiro conta uma.
let presses = 0;
const key = async (k) => { presses++; await page.keyboard.press(k); await page.waitForTimeout(300); };
// Segurar: o 2º keyboard.down sem up sai com repeat=true (é o que o sistema manda com a tecla presa). Conta uma entrada.
const hold = async (k) => { presses++; await page.keyboard.down(k); await page.waitForTimeout(150); await page.keyboard.down(k); await page.waitForTimeout(150); await page.keyboard.up(k); await page.waitForTimeout(300); };
const focusText = () => page.evaluate(() => (document.querySelector('#ui .focus')?.textContent ?? '').trim());
/** Leva a corrida atual ao fim: win=true, os humanos cruzam primeiro; false, a IA cruza antes e os humanos chegam por último. */
async function endRace(win) {
  const ok = await page.evaluate((w) => {
    const s = window.nc.session; const r = s.race; if (!r) return false;
    s.debugStep(260); // passa da contagem (210 ticks)
    let k = 0;
    for (const c of r.state.cars) {
      if (c.seat >= 0) { c.lap = r.state.config.laps; c.x = 0; c.z = r.track.length - (w ? 50 : 900) - c.seat * 10; c.speed = w ? 4000 : 1500; }
      else if (!w) { c.lap = r.state.config.laps; c.x = 0; c.z = r.track.length - 40 - k * 3; c.speed = 4000; k++; }
    }
    s.debugStep(600);
    r.overFor = 99; // o resultado sai no próximo quadro (sem os 3 s depois da bandeirada)
    return r.state.phase === 'finished';
  }, win);
  for (let i = 0; ok && i < 30 && (await S()).menu !== 'results'; i++) await page.waitForTimeout(500);
  return ok && (await S()).menu === 'results';
}
/** Botões da tela à vista e sem corte, e a tela sem rolar, a 1280×720 e a 1024×640 (volta a 1280×720 no fim). */
async function fitsAt(label) {
  for (const [w, h] of [[1280, 720], [1024, 640]]) {
    await page.setViewportSize({ width: w, height: h });
    await page.waitForTimeout(400);
    const f = await page.evaluate(() => {
      const btns = [...document.querySelectorAll('#ui .actions .btn')];
      const scr = document.querySelector('#ui .screen');
      return { n: btns.length, scroll: scr ? scr.scrollHeight - scr.clientHeight : -1,
        out: btns.filter((b) => b.getBoundingClientRect().bottom > innerHeight + 0.5 || b.scrollWidth > b.clientWidth + 1).map((b) => b.textContent.trim()) };
    });
    check(f.n > 0 && f.out.length === 0 && f.scroll <= 1, `${label} a ${w}×${h}: ${f.n} botões à vista e inteiros (fora/cortados: ${JSON.stringify(f.out)}), rolagem ${f.scroll}px`);
  }
  await page.setViewportSize({ width: 1280, height: 720 });
  await page.waitForTimeout(400);
}

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
// "Primeira vez?" na lista de foco (perfil novo): ↑ do 1º item chega no cartão; ↓ volta ao 1º item.
await page.keyboard.press('ArrowUp'); await page.waitForTimeout(300);
check(await page.evaluate(() => !!document.querySelector('.scr-main .tut-offer.focus')), 'cartão "Primeira vez?" recebe o foco com ↑');
await page.screenshot({ path: `${out}-02b-main-cartao.png` });
await page.keyboard.press('ArrowDown'); await page.waitForTimeout(300);

// Lobby pelo fluxo real: Campeonato → lobby; kb1 entra com Enter, kb2 com F.
await page.keyboard.press('Enter'); await page.waitForTimeout(400);
st = await S();
check(st.menu === 'lobby', `Campeonato abre o lobby (${st.menu})`);
check(await page.evaluate(() => window.nc.session.input.seatDevice(0)) === 'kb1', 'Enter no menu já senta o P1 (kb1)');
await page.keyboard.press('KeyF'); await page.waitForTimeout(250);
const bound = await page.evaluate(() => [0, 1, 2, 3].map((s) => window.nc.session.input.seatDevice(s)));
check(bound[0] === 'kb1' && bound[1] === 'kb2', `dois assentos ligados no lobby (${JSON.stringify(bound)})`);
await page.screenshot({ path: `${out}-03-lobby.png` });

// Entradas do menu à contagem (Campeonato, teclado, 1 jogador). Laço adaptativo, igual nos dois builds: Enter, exceto
// quando o P1 está PRONTO no lobby e o foco não está em INICIAR (aí ↑ até INICIAR).
await page.evaluate(() => window.nc.session.handleMenuEvent({ type: 'toMain' }));
await page.waitForTimeout(300);
presses = 0;
let shotSolo = false; let shotReady = false;
for (let i = 0; i < 20 && (await S()).race?.phase !== 'countdown'; i++) {
  const s = await page.evaluate(() => ({
    m: window.nc.session.menus.current(), p1: window.nc.session.input.seatDevice(0),
    ready: !!document.querySelector('#ui .slot.occupied.ready'), onStart: !!document.querySelector('#ui .btn-start.focus'),
    labels: [...document.querySelectorAll('#ui .lobby-options .sel-label')].map((e) => e.textContent),
  }));
  if (s.m === 'lobby' && s.p1 && !s.ready && !shotSolo) {
    shotSolo = true;
    check(s.labels.length === 3 && !s.labels.includes('Modo'), `lobby do Campeonato com 1 assento: 3 seletores, sem Modo (${JSON.stringify(s.labels)})`);
    await page.screenshot({ path: `${out}-03b-lobby-solo.png` });
  }
  if (s.m === 'lobby' && s.ready && !shotReady) {
    shotReady = true;
    check(s.onStart, 'depois do PRONTO o foco do P1 está em INICIAR');
    await page.screenshot({ path: `${out}-03c-lobby-pronto.png` });
  }
  if (s.m === 'lobby' && s.p1 && s.ready && !s.onStart) await key('ArrowUp'); else await key('Enter');
}
st = await S();
console.log(`entradas menu→contagem (Campeonato, teclado, 1 jogador): ${presses} (meta ≤ 4; antes da K1: 8)`);
check(st.race?.phase === 'countdown' && presses <= 4, `menu → contagem em ${presses} entradas (meta ≤ 4)`);
// "Continuar" (a copa que o laço acabou de largar) com Enter segurado no lobby.
await page.evaluate(() => window.nc.session.handleMenuEvent({ type: 'toMain' }));
await page.waitForTimeout(300);
presses = 0;
await key('Enter');
await hold('Enter');
st = await S();
check(st.race?.phase === 'countdown' && presses <= 2, `"Continuar" com Enter segurado: contagem em ${presses} entradas (meta ≤ 2)`);

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
// Velocidade varia com as trombadas no pelotão; o que importa é ter saído do grid e estar correndo.
check(st.race.phase === 'racing' && me.speed > 800 && me.z > 5000, `acelerando: fase ${st.race.phase}, ${me.speed} u/s, z=${me.z}`);
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

// Estatísticas e conquistas da corrida (passo 3.6; ganchos em src/game/raceEnd.ts): gravadas no save e no resultado.
const end = await page.evaluate(() => {
  const r = window.nc.session.race;
  return {
    humans: r.humans.map((h) => h.name.trim()),
    unlocked: r.outcome ? r.outcome.achievements.map((u) => u.id) : null,
    chips: document.querySelectorAll('.scr-results .ach-chip').length,
    save: JSON.parse(localStorage.getItem('nitro-crew.save') ?? 'null'),
  };
});
check(end.save?.racesRun === 1 && end.save?.stats?.totals?.races === end.humans.length,
  `corrida somada uma vez: racesRun ${end.save?.racesRun}, corridas no total ${end.save?.stats?.totals?.races} (${end.humans.length} jogadores)`);
check(end.humans.every((n) => end.save?.stats?.players?.some((p) => p.name === n && p.races === 1 && p.meters > 0)),
  `perfil de cada jogador com corrida e distância (${end.save?.stats?.players?.map((p) => `${p.name}:${p.meters}m`).join(', ')})`);
check(end.unlocked !== null && end.chips === end.unlocked.length && end.unlocked.every((id) => end.save.achievements.includes(id)),
  `conquistas da corrida no save e no quadro do resultado (${end.unlocked?.join(', ') || 'nenhuma'}; ${end.chips} no quadro)`);
// 1ª corrida na pista: PRIMEIRA MARCA, não RECORDE; e "Mais uma?" em foco, na pista seguinte.
const badges = await page.evaluate(() => [...document.querySelectorAll('.scr-results .record-badge')].map((e) => e.textContent.trim()));
check(badges.length >= 1 && badges.every((b) => b === 'PRIMEIRA MARCA'), `1ª corrida na pista: selos ${JSON.stringify(badges)} (todos PRIMEIRA MARCA)`);
check((await focusText()).startsWith('MAIS UMA'), `foco do resultado em "MAIS UMA?" (${await focusText()})`);
await page.screenshot({ path: `${out}-08b-mais-uma.png` });
await fitsAt('resultado da corrida rápida');
// Pior caso do resultado em 720p: 20 carros, humanos no fim e 14 conquistas de uma vez.
const busy = await page.evaluate(() => {
  const s = window.nc.session; const r = s.race;
  const ids = ['PRIMEIRA_VITORIA', 'EQUIPE_COMPLETA', 'SEM_ARRANHAO', 'NITRO_TRIPLO', 'EMPURRAO', 'VOLTA_PERFEITA', 'MESTRE_DO_VACUO', 'NITRO_NA_BANDEIRA', 'MADRUGADA', 'SEM_BOX', 'PODIO_DE_EQUIPE', 'DO_ULTIMO_AO_PRIMEIRO', 'COPA_BR_RJ', 'CAMPEAO'];
  const results = Array.from({ length: 20 }, (_, i) => ({ carId: i, seat: i === 19 ? 0 : -1, name: i === 19 ? r.humans[0].name : `IA ${i}`, teamId: i === 19 ? 0 : 100 + i, carDefId: 'falcao', position: i + 1, finished: true, totalTicks: 9000 + i * 30, bestLapTicks: 3000, points: 0 }));
  s.menus.show('results', { mode: 'quick', trackDef: r.track.def, results, humans: r.humans, champ: null, newRecords: [], achievements: ids.map((id) => ({ id, seats: [0] })) });
  for (const a of document.getAnimations()) a.finish();
  const box = (el) => el.getBoundingClientRect();
  const panel = document.querySelector('.scr-results .results-ach'); const p = box(panel);
  const wrap = document.querySelector('.scr-results .table-wrap'); const w = box(wrap);
  const rowH = box(wrap.querySelector('tbody tr')).height; const headH = box(wrap.querySelector('thead')).height;
  return {
    clipped: [...panel.querySelectorAll('.ach-chip')].filter((c) => { const b = box(c); return b.top < p.top - 0.5 || b.bottom > p.bottom + 0.5; }).length,
    rows: Math.floor((w.height - headH) / rowH),
    btnsIn: [...document.querySelectorAll('.scr-results .actions .btn')].every((b) => box(b).bottom <= innerHeight),
  };
});
check(busy.clipped === 0 && busy.rows >= 5 && busy.btnsIn, `resultado com 20 carros e 14 conquistas: nenhum cartão cortado (${busy.clipped}), ${busy.rows} linhas da tabela à vista, botões na tela`);
// "Mais uma?" (o pior caso acima também é um resultado de corrida rápida em copacabana): 1 entrada até a contagem.
// Só depois do pior caso: com uma corrida nova rodando, o getAnimations().finish() dele lança (animação infinita do HUD).
presses = 0;
await key('Enter');
st = await S();
const quickNext = await page.evaluate(() => window.nc.session.race?.state.config.trackId);
check(quickNext === 'paraty' && st.race?.phase === 'countdown' && presses <= 2, `"Mais uma?": ${quickNext} na contagem em ${presses} entrada(s) (meta: paraty, ≤ 2)`);

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

// Eliminado: "Recomeçar a copa" em foco; recomeçar mantém a copa e o assento.
const freshCup = (h) => { const s = window.nc.session; s.menus.hide(); for (let i = 0; i < 4; i++) s.input.unbindSeat(i); s.debugBind(0, 'kb1'); s.startCup('br_rj', h); };
await page.evaluate(freshCup, humans(1));
if (!(await endRace(false))) check(false, 'eliminado: a corrida não chegou ao resultado');
else {
  presses = 0;
  await key('Enter'); // CLASSIFICAÇÃO
  const elim = await page.evaluate(() => !!document.querySelector('.scr-standings .verdict.bad'));
  check(elim && (await focusText()) === 'RECOMEÇAR A COPA', `classificação de eliminado com "RECOMEÇAR A COPA" em foco (${elim ? 'eliminado' : 'sem veredito'}, foco "${await focusText()}")`);
  await page.screenshot({ path: `${out}-17-eliminado.png` });
  await fitsAt('classificação de eliminado');
  await key('Enter');
  const re = await page.evaluate(() => { const s = window.nc.session; return { mode: s.race?.mode, track: s.race?.state.config.trackId, dev: s.input.seatDevice(0), phase: s.race?.state.phase }; });
  check(re.mode === 'cup' && re.track === 'copacabana' && re.dev === 'kb1' && re.phase === 'countdown' && presses <= 2, `recomeçar a copa: ${JSON.stringify(re)} em ${presses} entradas (meta ≤ 2)`);
}
// Campeão (independente do bloco acima): as 3 corridas da Copa RJ com 2 entradas entre elas; no fim, "Próxima copa".
await page.evaluate(freshCup, humans(1));
let cupOk = true;
for (let race = 1; race <= 3 && cupOk; race++) {
  if (!(await endRace(true))) { check(false, `campeão: a corrida ${race} não chegou ao resultado`); cupOk = false; break; }
  presses = 0;
  await key('Enter'); // CLASSIFICAÇÃO
  if (race < 3) {
    await key('Enter'); // PRÓXIMA CORRIDA
    st = await S();
    check(st.race?.phase === 'countdown' && presses <= 2, `copa: resultado da corrida ${race} → largada da ${race + 1} em ${presses} entradas (meta ≤ 2)`);
  }
}
if (cupOk) {
  check((await focusText()) === 'PRÓXIMA COPA', `classificação de campeão com "PRÓXIMA COPA" em foco (${await focusText()})`);
  await page.screenshot({ path: `${out}-18-campeao.png` });
  await fitsAt('classificação de campeão');
  await key('Enter');
  const nx = await page.evaluate(() => window.nc.session.race?.state.config.trackId);
  check(nx === 'ilhabela' && presses <= 2, `"Próxima copa" larga ${nx} em ${presses} entradas (meta: ilhabela, ≤ 2)`);
}

// Menus restantes por teclado: volta ao principal e passeia pelas telas.
await page.evaluate(() => window.nc.session.handleMenuEvent({ type: 'toMain' }));
await page.waitForTimeout(300);
for (const [screen, file] of [['options', '11-options'], ['controls', '12-controls'], ['records', '13-records'], ['cups', '14-cups'], ['tracks', '15-tracks'], ['passport', '16-passport'], ['credits', '16b-credits']]) {
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
