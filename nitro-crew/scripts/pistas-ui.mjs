// Roteiro Playwright das telas de copas e pistas (onda G: 34 copas — a Expedição Brasil, 27 estados × 3 pistas em
// 5 regiões, e o Mundial, 7 países × 4 —, 109 pistas), pelo fluxo real de teclado:
// título → menu → lobby (Enter entra, Enter fica pronto, ↑ até INICIAR) → copas/pistas.
// Nas telas de seleção o teclado chega por keydown (menus.ts), então basta `press`; o lobby lê a
// entrada por quadro. Com a máquina carregada o 3D por software leva segundos por quadro (captura
// estourava 30 s), então o laço do requestAnimationFrame é parado e os quadros são dados à mão com
// session.frame() — a mesma função que o laço chama — só onde a tela depende deles.
// Partes: fluxo completo em 1280×720 e 1920×1080 (abas por região e Mundial, copa de 3 e de 4 pistas, grade de
// linhas desiguais, passaporte); Mundial fechado; contra-relógio (o cartão mostra as voltas com que a corrida
// larga); e as duas telas em tamanhos intermediários (Steam Deck 1280×800, 1366×768, 1600×900, 1024×600), onde
// nenhum nome pode sair cortado. `--tamanhos` roda só a última.
// Uso: npm run build && npx vite preview --port 4381 --strictPort &
//      node scripts/pistas-ui.mjs [url] [prefixo das capturas] [pasta extra das capturas] [--tamanhos]
import { copyFileSync, mkdirSync } from 'node:fs';
import { basename } from 'node:path';
import { chromium } from 'playwright';
const args = process.argv.slice(2).filter((a) => !a.startsWith('--'));
const onlySizes = process.argv.includes('--tamanhos');
const url = args[0] ?? 'http://localhost:4381/';
const out = args[1] ?? 'scratch/pistas';
const extra = args[2] ?? null;
if (extra) mkdirSync(extra, { recursive: true });
const exe = '/opt/pw-browsers/chromium-1194/chrome-linux/chrome';
const browser = await chromium.launch({ executablePath: exe, args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
const fails = [];
const errors = [];
const t0 = Date.now();
const check = (cond, msg) => { console.log(`${cond ? '✓' : '✗'} [${Math.round((Date.now() - t0) / 1000)}s] ${msg}`); if (!cond) fails.push(msg); };
async function shot(page, name) {
  const path = `${out}-${name}.png`;
  await page.screenshot({ path });
  if (extra) copyFileSync(path, `${extra}/${basename(path)}`);
}

const BRASIL = ['br_rj', 'br_sp', 'br_mg', 'br_es', 'br_pr', 'br_sc', 'br_rs', 'br_df', 'br_go', 'br_ms', 'br_mt', 'br_ba', 'br_se', 'br_al',
  'br_pe', 'br_pb', 'br_rn', 'br_ce', 'br_pi', 'br_ma', 'br_pa', 'br_am', 'br_ap', 'br_rr', 'br_ro', 'br_ac', 'br_to'];
// Progresso de quem fez a Expedição Brasil inteira (27 carimbos) e três copas do Mundial, com dois recordes.
const SAVE = {
  cupsCompleted: [...BRASIL, 'eua', 'japao', 'europa'],
  bestLaps: { copacabana: { ticks: 3912, name: 'Fê', carId: 'falcao', date: '2026-09-25' }, kruger: { ticks: 5231, name: 'Bia', carId: 'tornado', date: '2026-09-25' } },
  bestRaces: {}, achievements: [], racesRun: 20, racesWon: 6, seatNames: ['Fê', 'Bia', 'P3', 'P4'], seatCars: ['falcao', 'tornado', 'tornado', 'camelo'],
};
// No meio da Expedição: 12 estados (Sudeste, Sul, Centro-Oeste e a Bahia); o Mundial fechado.
const SAVE_MID = { ...SAVE, cupsCompleted: BRASIL.slice(0, 12) };
const gp = (dir) => ({ up: false, down: false, left: false, right: false, confirm: false, back: false, start: false, [dir]: true, device: 'gp0' });

// Espera N quadros do jogo (o laço da sessão roda no requestAnimationFrame).
const frame = (page) => page.evaluate(() => window.nc.session.frame(performance.now()));
async function held(page, code) { await page.keyboard.down(code); await frame(page); await page.keyboard.up(code); await frame(page); }
async function press(page, code) { await page.keyboard.press(code); await page.waitForTimeout(60); }
const menu = (page) => page.evaluate(() => window.nc.session.menus.current());
const focused = (page, attr) => page.evaluate((a) => document.querySelector('#ui .focus')?.getAttribute(a) ?? null, attr);

async function open(w, hgt, tag, save = SAVE) {
  const page = await browser.newPage({ viewport: { width: w, height: hgt } });
  page.on('pageerror', (e) => errors.push(`${tag} pageerror: ${e.message}`));
  page.on('console', (m) => { if (m.type() === 'error') errors.push(`${tag} console: ${m.text()}`); });
  await page.addInitScript((sv) => { try { localStorage.setItem('nitro-crew.save', JSON.stringify(sv)); } catch { /* sem storage */ } }, save);
  page.setDefaultTimeout(180_000);
  await page.goto(url, { waitUntil: 'networkidle' });
  await page.evaluate(() => { window.nc.session.settings.quality = 'low'; window.nc.session.stop(); });
  await frame(page);
  console.log(`[${Math.round((Date.now() - t0) / 1000)}s] ${tag}: página aberta, laço parado`);
  return page;
}

async function toSelection(page, modeIndex, tag) {
  await press(page, 'Enter'); // título → menu
  console.log(`[${Math.round((Date.now() - t0) / 1000)}s] ${tag}: ${await menu(page)}`);
  // Pelo rótulo, não pela posição: o menu ganhou Carreira, Online e Continuar desde que este roteiro nasceu.
  const label = ['^campeonato$', '^corrida rápida$', '^contra-relógio$'][modeIndex];
  const steps = await page.evaluate((src) => { const re = new RegExp(src, 'i'); return [...document.querySelectorAll('.scr-main .menu-list > *')].findIndex((el) => re.test((el.textContent ?? '').trim())); }, label);
  if (steps < 0) throw new Error(`item do menu principal não encontrado: ${label}`);
  for (let i = 0; i < steps; i++) await press(page, 'ArrowDown');
  await press(page, 'Enter'); // Campeonato / Corrida rápida → lobby
  check(await menu(page) === 'lobby', `${tag} modo ${modeIndex}: lobby aberto`);
  await frame(page); // o lobby ignora o primeiro quadro (carência da borda que o abriu)
  // Enter até o P1 entrar (teclado 1) e de novo até ficar pronto (cursor no carro): cada passo
  // confere o estado em vez de contar teclas, porque a borda que abriu o lobby pode engolir a primeira.
  for (let i = 0; i < 6 && await page.evaluate(() => window.nc.session.input.seatDevice(0)) !== 'kb1'; i++) await held(page, 'Enter');
  const ready = () => page.evaluate(() => document.querySelector('#ui .btn-start')?.classList.contains('disabled') === false);
  for (let i = 0; i < 6 && !(await ready()); i++) await held(page, 'Enter');
  check(await ready(), `${tag} modo ${modeIndex}: P1 entrou e está pronto`);
  for (let i = 0; i < 20; i++) {
    const onStart = await page.evaluate(() => document.querySelector('#ui .focus')?.classList.contains('btn-start') ?? false);
    if (onStart) break;
    await held(page, 'ArrowUp');
  }
  await held(page, 'Enter'); // INICIAR
}

// Espera a rolagem suave terminar (a posição para de mudar por 3 leituras seguidas, ou 8 s).
async function settle(page) {
  let last = -1; let still = 0;
  for (let i = 0; i < 80 && still < 3; i++) {
    const top = await page.evaluate(() => document.querySelector('#ui .track-scroll')?.scrollTop ?? 0);
    still = top === last ? still + 1 : 0; last = top;
    await page.waitForTimeout(100);
  }
}

async function layoutChecks(page, tag) {
  const r = await page.evaluate(() => {
    const scr = document.querySelector('#ui .screen');
    const vw = window.innerWidth;
    const outside = [...document.querySelectorAll('#ui .screen *')].filter((el) => {
      const b = el.getBoundingClientRect();
      if (b.width === 0 || b.height === 0) return false;
      return b.right > vw + 1 || b.left < -1;
    }).length;
    // Texto cortado: nome com reticências ou com mais linhas do que o clamp mostra.
    const cut = [...document.querySelectorAll('#ui .cup-row-name, #ui .cup-detail-title strong, #ui .cup-race-name, #ui .track-name, #ui .track-section-cup, #ui .cup-tab-name, #ui .cup-stage-name')]
      .filter((el) => el.scrollWidth > el.clientWidth + 1 || el.scrollHeight > el.clientHeight + 1).map((el) => el.textContent);
    const hint = document.querySelector('#ui .screen > .hint')?.getBoundingClientRect();
    const body = document.querySelector('#ui .track-scroll, #ui .cups-layout')?.getBoundingClientRect();
    const hintClear = !hint || !body || hint.top >= body.bottom - 1;
    return { hScroll: scr.scrollWidth > scr.clientWidth + 1, vScrollScreen: scr.scrollHeight > scr.clientHeight + 1, outside, cut, hintClear };
  });
  check(!r.hScroll && r.outside === 0, `${tag}: nada vaza na horizontal (elementos fora: ${r.outside})`);
  check(!r.vScrollScreen, `${tag}: a tela em si não rola (só a área da lista)`);
  check(r.cut.length === 0, `${tag}: nenhum nome cortado (${r.cut.join(' | ') || 'ok'})`);
  check(r.hintClear, `${tag}: a dica de teclas fica abaixo da lista, sem sobrepor`);
}

const activeTab = (page) => page.evaluate(() => document.querySelector('#ui .cup-tab.active')?.dataset.tab ?? null);
const rowIds = (page) => page.evaluate(() => [...document.querySelectorAll('#ui .cup-row')].map((e) => e.dataset.cup));
const detailTracks = (page) => page.evaluate(() => [...document.querySelectorAll('#ui .cup-race-name')].map((e) => e.textContent));
const listFits = (page) => page.evaluate(() => { const list = document.querySelector('#ui .cup-rows').getBoundingClientRect(); const last = [...document.querySelectorAll('#ui .cup-row')].pop().getBoundingClientRect(); return last.bottom <= list.bottom + 1 && list.bottom <= window.innerHeight; });

const inView = (page) => page.evaluate(() => {
  const box = document.querySelector('#ui .track-scroll').getBoundingClientRect();
  const card = document.querySelector('#ui .track-card.focus');
  const f = card.getBoundingClientRect();
  const head = card.closest('.track-section').querySelector('.track-section-head').getBoundingClientRect();
  return { card: f.top >= box.top - 1 && f.bottom <= box.bottom + 1, head: head.top >= box.top - 1 };
});

for (const [w, hgt] of onlySizes ? [] : [[1280, 720], [1920, 1080]]) {
  const tag = `${w}x${hgt}`;
  // ───────────── Copas ─────────────
  const page = await open(w, hgt, tag);
  await toSelection(page, 0, tag);
  check(await menu(page) === 'cups', `${tag}: INICIAR no Campeonato abre as copas`);
  await page.waitForTimeout(300);
  const tabs = await page.evaluate(() => [...document.querySelectorAll('#ui .cup-tab')].map((e) => `${e.dataset.tab}:${e.querySelector('.cup-tab-count')?.textContent ?? ''}`));
  check(tabs.join(' ') === 'sudeste:4/4 sul:3/3 centro_oeste:4/4 nordeste:9/9 norte:7/7 mundial:3/7', `${tag}: 5 abas de região com os carimbos e a do Mundial (${tabs.join(' ')})`);
  check(await activeTab(page) === 'mundial' && (await rowIds(page)).length === 7, `${tag}: abre na aba da fronteira, o Mundial, com 7 copas (${await activeTab(page)})`);
  check(await focused(page, 'data-cup') === 'africa_do_sul', `${tag}: cursor começa na fronteira (África do Sul) — ${await focused(page, 'data-cup')}`);
  const detail = await detailTracks(page);
  check(detail.length === 4 && detail[0] === 'Hoedspruit · Savana do Kruger', `${tag}: detalhe mostra as 4 pistas da copa em foco (${detail.join(', ')})`);
  check(await listFits(page), `${tag}: as 7 copas do Mundial cabem sem rolar`);
  await layoutChecks(page, `${tag} copas`);
  await shot(page, `copas-${tag}`);

  // ↓↓↓ até a última (travada): o detalhe acompanha; Enter numa travada não sai da tela.
  for (let i = 0; i < 3; i++) await press(page, 'ArrowDown');
  check(await focused(page, 'data-cup') === 'mediterraneo', `${tag}: ↓×3 chega ao Mediterrâneo`);
  const title = await page.evaluate(() => document.querySelector('#ui .cup-detail-title strong')?.textContent);
  check(title === 'Copa Mediterrâneo', `${tag}: detalhe trocou para a copa em foco (${title})`);
  const lockChip = await page.evaluate(() => document.querySelector('#ui .cup-chip')?.textContent ?? '');
  check(lockChip.includes('Copa Escandinávia'), `${tag}: copa travada diz qual concluir (${lockChip})`);
  await press(page, 'Enter');
  check(await menu(page) === 'cups', `${tag}: Enter numa copa travada não começa corrida`);
  await layoutChecks(page, `${tag} copas travada`);
  await shot(page, `copas-travada-${tag}`);
  // Controle (mesmo caminho que a sessão usa para gamepads): ↓ dá a volta para os Estados Unidos.
  await page.evaluate((n) => window.nc.session.menus.navigate(n), gp('down'));
  check(await focused(page, 'data-cup') === 'eua', `${tag}: ↓ no controle dá a volta até os Estados Unidos`);
  const doneChip = await page.evaluate(() => document.querySelector('#ui .cup-chip.done')?.textContent ?? '');
  check(doneChip.length > 0, `${tag}: copa concluída mostra o selo (${doneChip})`);
  // Mouse: passar por cima de uma copa também troca o detalhe.
  await page.hover('#ui .cup-row[data-cup="australia"]');
  await frame(page); // o detalhe acompanha o cursor no update() da tela, a cada quadro
  const hoverTitle = await page.evaluate(() => document.querySelector('#ui .cup-detail-title strong')?.textContent);
  check(hoverTitle === 'Copa Austrália', `${tag}: mouse por cima troca o detalhe (${hoverTitle})`);
  await page.mouse.move(2, 2);

  // ← no controle: a aba do Norte, com os 7 estados; copa de 3 pistas no detalhe e o carimbo dela.
  await page.evaluate((n) => window.nc.session.menus.navigate(n), gp('left'));
  check(await activeTab(page) === 'norte', `${tag}: ← troca para a aba do Norte (${await activeTab(page)})`);
  const norte = await rowIds(page);
  check(norte.join(',') === 'br_pa,br_am,br_ap,br_rr,br_ro,br_ac,br_to', `${tag}: Norte com os 7 estados na ordem (${norte.join(',')})`);
  check(await focused(page, 'data-cup') === 'br_to', `${tag}: com a região toda concluída, o cursor fica na última (${await focused(page, 'data-cup')})`);
  const to = await detailTracks(page);
  check(to.join(', ') === 'Mateiros · Dunas do Jalapão, Ponte de Palmas, Formoso do Araguaia · Ilha do Bananal', `${tag}: copa de estado com 3 pistas no detalhe (${to.join(', ')})`);
  const stampChip = await page.evaluate(() => ({ chip: document.querySelector('#ui .cup-chip.done')?.textContent ?? '', badge: document.querySelector('#ui .cup-detail .uf-badge.on')?.textContent ?? '' }));
  check(stampChip.chip.includes('Carimbada') && stampChip.badge === 'TO', `${tag}: estado carimbado no detalhe (${stampChip.chip}; ${stampChip.badge})`);
  await layoutChecks(page, `${tag} copas Norte`);
  await shot(page, `copas-norte-${tag}`);
  // Clique na aba do Nordeste: a lista mais longa (9 estados) cabe sem rolar.
  await page.click('#ui .cup-tab[data-tab="nordeste"]');
  check(await activeTab(page) === 'nordeste' && (await rowIds(page)).length === 9, `${tag}: clique abre o Nordeste com 9 estados`);
  check(await listFits(page), `${tag}: as 9 copas do Nordeste cabem sem rolar`);
  await layoutChecks(page, `${tag} copas Nordeste`);
  await shot(page, `copas-nordeste-${tag}`);
  // ← nas pontas: da 1ª aba não passa.
  for (let i = 0; i < 6; i++) await press(page, 'ArrowLeft');
  check(await activeTab(page) === 'sudeste', `${tag}: ← para na primeira aba (Sudeste)`);
  if (w === 1280) {
    // Inglês: textos da tela no outro idioma.
    await page.evaluate(() => { const s = window.nc.session; s.handleMenuEvent({ type: 'settingsChanged', settings: { ...s.settings, language: 'en' } }); });
    await page.waitForTimeout(300);
    const en = await page.evaluate(() => ({ title: document.querySelector('#ui .cup-detail-title strong')?.textContent, stage: document.querySelector('#ui .cup-stage-name')?.textContent, track: document.querySelector('#ui .cup-race-name')?.textContent }));
    check(typeof en.title === 'string' && en.title.endsWith(' Cup') && (en.stage ?? '').startsWith('Brazil Expedition'), `${tag}: copas em inglês (${en.title}; ${en.stage}; a tela é remontada e o cursor volta à fronteira)`);
    check(en.track === 'Hoedspruit · Kruger Savanna', `${tag}: nome da pista em inglês pelas strings do núcleo (${en.track})`);
    await layoutChecks(page, `${tag} copas EN`);
    await shot(page, `copas-en-${tag}`);
    await page.evaluate(() => { const s = window.nc.session; s.handleMenuEvent({ type: 'settingsChanged', settings: { ...s.settings, language: 'pt' } }); });
    await page.waitForTimeout(300);
  } else {
    // Sem a troca de idioma a tela continua no Sudeste: volta ao Mundial, na África do Sul, para o passo seguinte.
    await page.click('#ui .cup-tab[data-tab="mundial"]');
    await page.mouse.move(2, 2);
  }
  // Enter numa copa aberta começa a primeira corrida dela (a primeira pista do detalhe). A tela está na África do
  // Sul (em 1280 a troca de idioma a remontou na fronteira), então ↑ cai na Europa.
  await page.evaluate((n) => window.nc.session.menus.navigate(n), gp('up'));
  const cupNow = await focused(page, 'data-cup');
  const firstName = await page.evaluate(() => document.querySelector('#ui .cup-race-name')?.textContent ?? null);
  await press(page, 'Enter');
  await page.waitForTimeout(300);
  const race = await page.evaluate(() => { const r = window.nc.session.race; return r ? { id: r.state.trackId, name: r.track.def.name, mode: r.mode } : null; });
  check(await menu(page) === null && race !== null && firstName.endsWith(race.name), `${tag}: Enter em ${cupNow} começa a 1ª corrida dela (${race?.id}, ${race?.mode})`);

  // Classificação de uma copa de 4 corridas depois da primeira: 4 colunas e "2 de 4".
  await page.evaluate(() => {
    const s = window.nc.session;
    const names = ['Fê', 'Rossi', 'Mendes', 'Okafor', 'Lind', 'Tanaka', 'Moreau', 'Silva', 'Novak', 'Keller', 'Duarte', 'Sato', 'Park', 'Laine', 'Costa', 'Ibarra', 'Vogel', 'Nkosi', 'Hale', 'Reyes'];
    const standings = names.map((name, i) => ({ key: i === 0 ? 'seat:0' : name, name, seat: i === 0 ? 0 : -1, teamId: Math.floor(i / 2), points: Math.max(0, 20 - i * 2), wins: i === 0 ? 1 : 0, positions: [i + 1, 0, 0, 0] }));
    const teams = Array.from({ length: 10 }, (_, i) => ({ teamId: i, name: i === 0 ? 'Fê & cia.' : `Equipe ${i}`, points: 38 - i * 4, isHuman: i === 0 }));
    const champ = { cupId: 'africa_do_sul', raceIndex: 1, standings, teams, coop: false, eliminated: false, completed: false, lastRace: null, lastVerdict: 'qualified' };
    const cup = { id: 'africa_do_sul', name: 'Copa África do Sul', country: 'África do Sul', flag: '🇿🇦', trackIds: ['kruger', 'karoo', 'drakensberg', 'boa_esperanca'], requires: 'europa' };
    s.menus.show('standings', { champ, humans: [{ seat: 0, name: 'Fê', carId: 'falcao', teamId: 0, color: '#ffd23f' }], cup });
  });
  await page.waitForTimeout(300);
  const st = await page.evaluate(() => ({
    cols: document.querySelectorAll('#ui .standings-table thead th').length,
    line: document.querySelector('#ui .status-line')?.textContent ?? '',
    fits: (() => { const scr = document.querySelector('#ui .screen'); return scr.scrollWidth <= scr.clientWidth + 1; })(),
  }));
  check(st.cols === 8 && st.line.includes('2 de 4') && st.line.includes('Deserto do Karoo'), `${tag}: classificação com 4 corridas (${st.cols} colunas; "${st.line}")`);
  check(st.fits, `${tag}: classificação cabe na largura`);
  await shot(page, `classificacao-${tag}`);
  // Copa de estado (3 corridas): 3 colunas de corrida e "2 de 3".
  await page.evaluate(() => {
    const s = window.nc.session;
    const standings = Array.from({ length: 20 }, (_, i) => ({ key: i === 0 ? 'seat:0' : `ia${i}`, name: i === 0 ? 'Fê' : `IA ${i}`, seat: i === 0 ? 0 : -1, teamId: Math.floor(i / 2), points: Math.max(0, 20 - i * 2), wins: i === 0 ? 1 : 0, positions: [i + 1, 0, 0] }));
    const teams = Array.from({ length: 10 }, (_, i) => ({ teamId: i, name: `Equipe ${i}`, points: 38 - i * 4, isHuman: i === 0 }));
    const champ = { cupId: 'br_ba', raceIndex: 1, standings, teams, coop: false, eliminated: false, completed: false, lastRace: null, lastVerdict: 'qualified' };
    const cup = { id: 'br_ba', name: 'Copa Bahia', country: 'Brasil', flag: '🇧🇷', stage: 'brasil', region: 'nordeste', state: 'BA', trackIds: ['porto_seguro', 'salvador', 'chapada_diamantina'], requires: 'br_mt' };
    s.menus.show('standings', { champ, humans: [{ seat: 0, name: 'Fê', carId: 'falcao', teamId: 0, color: '#ffd23f' }], cup });
  });
  await page.waitForTimeout(300);
  const st3 = await page.evaluate(() => ({ cols: document.querySelectorAll('#ui .standings-table thead th').length, line: document.querySelector('#ui .status-line')?.textContent ?? '' }));
  check(st3.cols === 7 && st3.line.includes('2 de 3') && st3.line.includes('Orla de Salvador'), `${tag}: classificação de copa de estado com 3 corridas (${st3.cols} colunas; "${st3.line}")`);
  await shot(page, `classificacao-3-${tag}`);
  // Passaporte (menu principal › Passaporte): 27 carimbos, todos ganhos, e o cartão-postal com nomes de marco.
  await page.evaluate(() => window.nc.session.menus.show('passport'));
  await page.waitForTimeout(300);
  const pp = await page.evaluate(() => ({
    stamps: document.querySelectorAll('#ui .pp-stamp').length, on: document.querySelectorAll('#ui .pp-stamp.on').length,
    regions: [...document.querySelectorAll('#ui .pp-region')].map((r) => r.querySelectorAll('.pp-stamp').length).join(','),
    focus: document.querySelector('#ui .pp-stamp.focus')?.dataset.state, state: document.querySelector('#ui .pc-state')?.textContent,
    tracks: document.querySelectorAll('#ui .pc-tracks li').length, landmarks: document.querySelector('#ui .pc-landmarks')?.textContent ?? '',
  }));
  check(pp.stamps === 27 && pp.on === 27 && pp.regions === '4,3,4,9,7', `${tag}: passaporte com 27 carimbos em 5 regiões (${pp.on}/${pp.stamps}; ${pp.regions})`);
  check(pp.focus === 'TO' && pp.state === 'Tocantins' && pp.tracks === 3 && pp.landmarks.length > 0 && !pp.landmarks.includes('_'), `${tag}: cartão-postal do último carimbo (${pp.state}: ${pp.tracks} pistas; ${pp.landmarks})`);
  await press(page, 'ArrowUp');
  const pp2 = await page.evaluate(() => ({ focus: document.querySelector('#ui .pp-stamp.focus')?.dataset.state, state: document.querySelector('#ui .pc-state')?.textContent }));
  check(pp2.focus === 'CE' && pp2.state === 'Ceará', `${tag}: ↑ no passaporte sobe para o Nordeste na mesma coluna (${pp2.focus})`);
  await layoutChecks(page, `${tag} passaporte`);
  await shot(page, `passaporte-${tag}`);

  await page.close();

  // ───────────── Pistas ─────────────
  const p2 = await open(w, hgt, tag);
  await toSelection(p2, 1, tag);
  check(await menu(p2) === 'tracks', `${tag}: INICIAR na Corrida rápida abre as pistas`);
  await p2.waitForTimeout(300);
  const cards = await p2.evaluate(() => document.querySelectorAll('#ui .track-card').length);
  check(cards === 109, `${tag}: 109 pistas na grade (${cards})`);
  const heads = await p2.evaluate(() => ({ sections: document.querySelectorAll('#ui .track-section').length, groups: [...document.querySelectorAll('#ui .track-group-head')].map((e) => e.textContent).join(' | ') }));
  check(heads.sections === 34, `${tag}: uma seção por copa (${heads.sections})`);
  check(heads.groups.startsWith('Expedição BrasilSudeste') && heads.groups.endsWith('Mundial') && heads.groups.split(' | ').length === 6, `${tag}: etapa e região em cima da primeira copa de cada uma (${heads.groups})`);
  await layoutChecks(p2, `${tag} pistas`);
  await shot(p2, `pistas-${tag}`);
  // Teclado: ↓ troca de copa (mesma coluna), → anda na copa; numa copa de 3, → para na 3ª.
  await press(p2, 'ArrowRight');
  await press(p2, 'ArrowDown');
  check(await focused(p2, 'data-track') === 'campos_do_jordao', `${tag}: → ↓ vai à 2ª pista da copa seguinte (${await focused(p2, 'data-track')})`);
  for (let i = 0; i < 4; i++) await press(p2, 'ArrowRight');
  check(await focused(p2, 'data-track') === 'sampa_noite', `${tag}: → para na 3ª pista de uma copa de 3 (${await focused(p2, 'data-track')})`);
  for (let i = 0; i < 40; i++) await press(p2, 'ArrowDown');
  check(await focused(p2, 'data-track') === 'etna', `${tag}: ↓ até o fim, da 3ª coluna para a 3ª do Mediterrâneo (${await focused(p2, 'data-track')})`);
  await press(p2, 'ArrowRight');
  await settle(p2);
  check(await focused(p2, 'data-track') === 'roma', `${tag}: → chega à última pista, a 4ª do Mediterrâneo (${await focused(p2, 'data-track')})`);
  const vEnd = await inView(p2);
  check(vEnd.card, `${tag}: a rolagem acompanhou o foco até o fim (cartão inteiro visível)`);
  await layoutChecks(p2, `${tag} pistas fim`);
  await shot(p2, `pistas-fim-${tag}`);
  // Controle: ↑ ×6 volta aos Estados Unidos na 4ª coluna; mais um ↑ entra no Tocantins (3 pistas) preso na 3ª.
  for (let i = 0; i < 6; i++) await p2.evaluate((n) => window.nc.session.menus.navigate(n), gp('up'));
  await settle(p2);
  check(await focused(p2, 'data-track') === 'las_vegas', `${tag}: ↑×6 no controle volta aos Estados Unidos, mesma coluna (${await focused(p2, 'data-track')})`);
  await p2.evaluate((n) => window.nc.session.menus.navigate(n), gp('up'));
  await settle(p2);
  check(await focused(p2, 'data-track') === 'ilha_do_bananal', `${tag}: ↑ da 4ª coluna para uma copa de 3 para na 3ª (${await focused(p2, 'data-track')})`);
  const vUp = await inView(p2);
  check(vUp.card && vUp.head, `${tag}: subindo, o cartão e o cabeçalho da copa ficam visíveis (${JSON.stringify(vUp)})`);
  await shot(p2, `pistas-meio-${tag}`);
  const target = await focused(p2, 'data-track');
  await press(p2, 'Enter');
  await p2.waitForTimeout(300);
  const qr = await p2.evaluate(() => window.nc.session.race?.state.trackId ?? null);
  check(await menu(p2) === null && qr === target, `${tag}: Enter começa a corrida rápida na pista em foco (${qr})`);
  await p2.close();
}

// ───────────── Mundial fechado (no meio da Expedição) ─────────────
if (!onlySizes) {
  const tag = '1280x720 Mundial fechado';
  const page = await open(1280, 720, tag, SAVE_MID);
  await toSelection(page, 0, tag);
  check(await menu(page) === 'cups' && await activeTab(page) === 'nordeste' && await focused(page, 'data-cup') === 'br_se', `${tag}: abre no Nordeste, em Sergipe (${await activeTab(page)}, ${await focused(page, 'data-cup')})`);
  for (let i = 0; i < 2; i++) await press(page, 'ArrowRight');
  const lock = await page.evaluate(() => ({ tab: document.querySelector('#ui .cup-tab.active')?.dataset.tab, note: document.querySelector('#ui .cup-stage-lock')?.textContent ?? '', locked: document.querySelectorAll('#ui .cup-row.locked').length, icon: !!document.querySelector('#ui .cup-tab[data-tab="mundial"] .cup-tab-count .ico') }));
  check(lock.tab === 'mundial' && lock.note.includes('Copa Tocantins') && lock.locked === 7 && lock.icon, `${tag}: Mundial fechado diz por quê ("${lock.note}"; ${lock.locked} travadas; cadeado na aba: ${lock.icon})`);
  await press(page, 'Enter');
  check(await menu(page) === 'cups', `${tag}: Enter numa copa do Mundial fechado não começa corrida`);
  await layoutChecks(page, `${tag}`);
  await shot(page, 'copas-mundial-fechado-1280x720');
  await page.close();
}

// ───────────── Contra-relógio: o cartão mostra as voltas com que a corrida larga ─────────────
// (revisão de 25/09: mostrava as da pista — 4 nas de noite — e a sessão largava com as da corrida rápida)
if (!onlySizes) {
  const tag = '1280x720 contra-relógio';
  const page = await open(1280, 720, tag);
  await page.evaluate(() => { const s = window.nc.session; s.handleMenuEvent({ type: 'settingsChanged', settings: { ...s.settings, quickLaps: 5 } }); });
  await toSelection(page, 2, tag);
  check(await menu(page) === 'tracks', `${tag}: INICIAR abre as pistas`);
  const shown = await page.evaluate(() => document.querySelector('#ui .track-card[data-track="sampa_noite"] .meta')?.textContent ?? '');
  check(shown.includes('5 voltas'), `${tag}: Noite em Sampa (4 voltas na copa) mostra as 5 da corrida (${shown})`);
  const first = await focused(page, 'data-track');
  await press(page, 'Enter');
  await page.waitForTimeout(300);
  const cfg = await page.evaluate(() => { const r = window.nc.session.race; return r ? { laps: r.state.config.laps, tt: r.state.config.timeTrial === true, id: r.state.trackId } : null; });
  check(cfg !== null && cfg.tt && cfg.laps === 5 && cfg.id === first, `${tag}: a corrida larga com as voltas do cartão (${JSON.stringify(cfg)})`);
  await page.close();
}

// ───────────── Tamanhos intermediários: nada cortado, nada vazando ─────────────
// (revisão de 25/09: "Transpantaneira" saía "Transpantan…" em 1280×800 e 1024×600)
for (const [w, hgt] of [[1280, 800], [1366, 768], [1600, 900], [1024, 600]]) {
  const tag = `${w}x${hgt}`;
  const page = await open(w, hgt, tag);
  await page.evaluate(() => window.nc.session.menus.show('cups'));
  await page.waitForTimeout(300);
  check(await listFits(page), `${tag}: as copas do Mundial cabem sem rolar`);
  await layoutChecks(page, `${tag} copas`);
  await shot(page, `tamanho-copas-${tag}`);
  await page.click('#ui .cup-tab[data-tab="nordeste"]');
  check(await listFits(page), `${tag}: as 9 copas do Nordeste cabem sem rolar`);
  await layoutChecks(page, `${tag} copas Nordeste`);
  await page.evaluate(() => window.nc.session.menus.show('tracks'));
  await page.waitForTimeout(300);
  await layoutChecks(page, `${tag} pistas`); // confere os 109 cartões, visíveis ou não
  await shot(page, `tamanho-pistas-${tag}`);
  await page.close();
}

console.log('erros de página:', errors.length ? errors.join('\n') : 'nenhum');
await browser.close();
if (errors.length || fails.length) { console.log(`FALHOU: ${fails.length} conferências, ${errors.length} erros`); process.exit(1); }
console.log(`roteiro das pistas OK (${Math.round((Date.now() - t0) / 1000)} s)`);
