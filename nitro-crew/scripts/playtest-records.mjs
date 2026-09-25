// Playtest da tela de recordes com recorde em 32 pistas, em 1280×720, rolada pelo controle (↑↓ movem o
// foco pelas linhas) e pelo teclado. Se o jogo tiver menos de 32 pistas, completa com pistas fictícias
// empurradas em TRACKS — por isso roda no servidor de desenvolvimento (`npm run dev`), onde a página
// importa o mesmo módulo que a sessão usa. Ver docs/ESTATISTICAS.md.
// Uso: node scripts/playtest-records.mjs [url do vite dev] [prefixo das capturas]
import { chromium } from 'playwright';
const url = process.argv[2] ?? 'http://localhost:5174/';
const out = process.argv[3] ?? 'scratch/rec';
const exe = process.env.CHROME_PATH ?? '/opt/pw-browsers/chromium-1194/chrome-linux/chrome';
const TOTAL = 32;
const browser = await chromium.launch({ executablePath: exe, args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist', '--autoplay-policy=no-user-gesture-required'] });
const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
page.setDefaultTimeout(240_000);
const errors = [];
page.on('pageerror', (e) => errors.push('pageerror: ' + e.message));
page.on('console', (m) => { if (m.type() === 'error') errors.push(`console: ${m.text()}`); });
const fails = [];
const check = (cond, msg) => { console.log(`${cond ? '✓' : '✗'} ${msg}`); if (!cond) fails.push(msg); };
const shot = (name) => page.screenshot({ path: `${out}-${name}.png`, timeout: 240_000 });
const key = async (k, n = 1) => { for (let i = 0; i < n; i++) { await page.keyboard.press(k); await page.waitForTimeout(120); } };
const current = () => page.evaluate(() => window.nc.session.menus.current());
const settle = () => page.evaluate(() => { for (const a of document.getAnimations()) a.finish(); });
/** Borda de controle (não teclado): o que a sessão entrega a menus.navigate a cada quadro. */
const pad = (dir, n = 1) => page.evaluate(({ dir, n }) => {
  for (let i = 0; i < n; i++) {
    window.nc.session.menus.navigate({ up: dir === 'up', down: dir === 'down', left: dir === 'left', right: dir === 'right', confirm: false, back: false, start: false, device: 'gp0' });
  }
}, { dir, n });
/** Completa TRACKS até TOTAL com cópias da primeira pista (a cada 5, um nome longo). Devolve id e nome de todas. */
const padTracks = () => page.evaluate(async (total) => {
  const m = await import('/src/core/track/tracks.ts');
  const base = m.TRACKS[0];
  for (let i = m.TRACKS.length + 1; i <= total; i++) {
    m.TRACKS.push({ ...base, id: `ficticia_${i}`, name: i % 5 === 0 ? `Pista Fictícia Número ${i} do Interior Profundo` : `Pista Fictícia ${i}` });
  }
  return m.TRACKS.map((d) => ({ id: d.id, name: d.name }));
}, TOTAL);

// 1ª carga: descobre as pistas e grava o save (recorde de volta em todas; corridas de 3 e 5 voltas em
// várias); 2ª carga: a sessão lê esse save. As fictícias somem no reload e são empurradas de novo.
await page.goto(url, { waitUntil: 'networkidle' });
const tracks = await padTracks();
await page.evaluate((ids) => {
  const zero = { races: 0, wins: 0, podiums: 0, laps: 0, meters: 0, nitros: 0, towsGiven: 0, towsReceived: 0, collisions: 0, crashes: 0, pitStops: 0, raceTicks: 0, coopWins: 0 };
  const bestLaps = {}; const bestRaces = {}; const bestPositions = {};
  ids.forEach((id, i) => {
    bestLaps[id] = { ticks: 5000 + i * 37, name: ['Felipe', 'Ana', 'Bia', 'Maximiliano'][i % 4], carId: 'falcao', date: '2026-09-20' };
    if (i % 2 === 0) bestRaces[`${id}:3`] = { ticks: 16000 + i * 90, name: 'Felipe', carId: 'trovao', date: '2026-09-21' };
    if (i % 3 === 0) bestRaces[`${id}:5`] = { ticks: 27000 + i * 90, name: 'Maximiliano', carId: 'tornado', date: '2026-09-22' };
    bestPositions[id] = 1 + (i % 12);
  });
  const felipe = { ...zero, name: 'Felipe', races: 64, wins: 20, podiums: 40, laps: 190, meters: 1_234_000, raceTicks: 60 * 12000, bestPositions };
  const ana = { ...zero, name: 'Ana', races: 2, meters: 9000, bestPositions: { [ids[0]]: 2, [ids[19]]: 5 } };
  localStorage.setItem('nitro-crew.save', JSON.stringify({
    cupsCompleted: ['brasil'], racesRun: 65, racesWon: 20, achievements: ['PRIMEIRA_VITORIA', 'GIRO_COMPLETO'],
    bestLaps, bestRaces, seatNames: ['Felipe', 'Ana', 'P3', 'P4'],
    // Ana primeiro (mais recente): o Felipe, com as 32 pistas, é o último da lista, logo antes da grade.
    stats: { totals: { ...felipe, races: 66, meters: 1_243_000 }, players: [ana, felipe] },
  }));
}, tracks.map((d) => d.id));
await page.reload({ waitUntil: 'networkidle' });
await page.waitForTimeout(1000);
check((await padTracks()).length === TOTAL, `${TOTAL} pistas em TRACKS (${tracks.filter((d) => d.id.startsWith('ficticia_')).length} fictícias)`);
await page.evaluate(() => { window.nc.session.settings.quality = 'low'; });

// Recordes pelo teclado: título → menu → Recordes.
await key('Enter');
await key('ArrowDown', 3);
await key('Enter');
check(await current() === 'records', `Recordes abre pelo menu (${await current()})`);
await settle();
check(await page.evaluate(() => document.querySelectorAll('.record-row').length) === TOTAL, `aba Pistas: ${TOTAL} pistas com recorde`);

// Legibilidade em 720p: tamanhos de fonte, nada vazando da linha, só o miolo rola.
const leg = await page.evaluate(() => {
  const px = (sel) => parseFloat(getComputedStyle(document.querySelector(sel)).fontSize);
  const over = [...document.querySelectorAll('.record-row, .record-row .record-entry, .record-row .record-track')]
    .filter((el) => el.scrollWidth > el.clientWidth + 1).map((el) => el.textContent.slice(0, 40));
  const scr = document.querySelector('.scr-records');
  const scroll = document.querySelector('.rec-scroll');
  const back = [...document.querySelectorAll('.scr-records .actions .btn')].map((b) => b.getBoundingClientRect());
  return {
    name: px('.record-track strong'), time: px('.record-time'), who: px('.record-who'), over,
    screenScrolls: scr.scrollHeight > scr.clientHeight + 1, listScrolls: scroll.scrollHeight > scroll.clientHeight + 1,
    backIn: back.length === 1 && back[0].bottom <= innerHeight && back[0].top >= 0,
  };
});
check(leg.name >= 16 && leg.time >= 14 && leg.who >= 12, `fontes legíveis em 720p (pista ${leg.name}px, tempo ${leg.time}px, quem ${leg.who}px)`);
check(leg.over.length === 0, `nenhum texto vazando da linha (${leg.over.join(' | ')})`);
check(!leg.screenScrolls && leg.listScrolls && leg.backIn, 'só a lista rola; a tela não, e o Voltar fica à vista');
await shot('01-pistas-topo');

// Controle: ↓ percorre as 32 linhas; cada linha focada fica inteira à vista na área que rola.
const focusInfo = () => page.evaluate(() => {
  const f = document.querySelector('.scr-records .focus');
  if (!f) return null;
  const scroll = document.querySelector('.rec-scroll').getBoundingClientRect();
  const r = f.getBoundingClientRect();
  const isBtn = f.classList.contains('btn');
  return {
    row: [...document.querySelectorAll('.record-row')].indexOf(f), back: isBtn, text: f.querySelector('strong')?.textContent ?? f.textContent,
    inside: isBtn ? r.bottom <= innerHeight : r.top >= scroll.top - 0.5 && r.bottom <= scroll.bottom + 0.5,
  };
});
let last = await focusInfo();
check(last?.row === 0 && last.inside, `a aba abre com o foco na primeira pista (${last?.text})`);
const hidden = [];
for (let i = 1; i < TOTAL; i++) {
  await pad('down');
  last = await focusInfo();
  if (!last || last.row !== i || !last.inside) hidden.push(`${i}:${JSON.stringify(last)}`);
  if (i === 15) await shot('02-pistas-meio');
}
check(hidden.length === 0, `controle ↓ passa pelas ${TOTAL} linhas, cada uma inteira à vista (${hidden.slice(0, 3).join(' ; ')})`);
check(last?.text === tracks[TOTAL - 1].name, `a última linha é a ${TOTAL}ª pista (${last?.text})`);
await shot('03-pistas-fim');
await pad('down');
const back = await focusInfo();
check(back?.back === true && back.inside, 'depois da última pista, o Voltar');
const upHidden = [];
for (let i = TOTAL - 1; i >= 0; i--) {
  await pad('up');
  const f = await focusInfo();
  if (!f || f.row !== i || !f.inside) upHidden.push(`${i}:${JSON.stringify(f)}`);
}
check(upHidden.length === 0, `controle ↑ volta linha a linha até a primeira (${upHidden.slice(0, 3).join(' ; ')})`);
await key('ArrowDown', 20);
const kb = await focusInfo();
check(kb?.row === 20 && kb.inside, `teclado ↓ também rola a lista (${kb?.text})`);

// Jogadores: a grade de melhor posição (todas as pistas, em linhas focáveis) alcançada pelo controle.
await pad('right');
await settle();
check((await page.evaluate(() => document.querySelector('.rec-tab.on')?.textContent)) === 'Jogadores', 'controle ▶ troca para a aba Jogadores');
const bestInfo = () => page.evaluate(() => {
  const d = document.querySelector('.pl-detail'); const dr = d.getBoundingClientRect();
  const rows = [...document.querySelectorAll('.best-row')];
  const f = document.querySelector('.best-row.focus');
  const r = f?.getBoundingClientRect();
  const head = document.querySelector('.best-head').getBoundingClientRect();
  return {
    rows: rows.length, row: f ? rows.indexOf(f) : -1, inside: !!r && r.top >= dr.top - 0.5 && r.bottom <= dr.bottom + 0.5,
    headVisible: head.top >= dr.top - 0.5 && head.bottom <= dr.bottom + 0.5,
    chips: document.querySelectorAll('.best-chip').length, none: document.querySelectorAll('.best-chip.none').length,
    count: document.querySelector('.best-count')?.textContent, name: document.querySelector('.pl-detail-name')?.textContent,
    rowText: f ? [...f.querySelectorAll('.best-name')].map((e) => e.textContent).join(',') : '',
    over: [...document.querySelectorAll('.best-chip')].filter((c) => c.scrollWidth > c.clientWidth + 1).length,
    scrollTop: Math.round(d.scrollTop), detailScrolls: d.scrollHeight > d.clientHeight + 1,
  };
});
await pad('down', 2); // Todos → Ana → Felipe (o último da lista)
const bestRows = Math.ceil(TOTAL / 3);
const bestHidden = [];
let bi = null;
for (let i = 0; i < bestRows; i++) {
  await pad('down');
  bi = await bestInfo();
  if (bi.row !== i || !bi.inside) bestHidden.push(`${i}:${JSON.stringify(bi)}`);
  if (i === 0) await shot('04-jogadores-grade');
}
check(bi.rows === bestRows && bi.chips === TOTAL && bi.none === 0 && bi.count === `${TOTAL} de ${TOTAL} pistas` && bi.name === 'Felipe',
  `grade de melhor posição do Felipe: ${bi.count}, ${bi.chips} pistas em ${bi.rows} linhas`);
check(bestHidden.length === 0, `controle ↓ passa pelas ${bestRows} linhas da grade, cada uma à vista (${bestHidden.slice(0, 2).join(' ; ')})`);
check(bi.rowText.includes(tracks[TOTAL - 1].name) && bi.detailScrolls && bi.over === 0, `a última linha (com a ${TOTAL}ª pista) aparece rolando o detalhe (${bi.rowText})`);
await shot('05-jogadores-grade-fim');
await pad('up', bestRows - 1);
bi = await bestInfo();
check(bi.row === 0 && bi.inside && bi.headVisible, 'de volta à primeira linha, o título da grade rola junto');
await pad('up');
bi = await bestInfo();
check(bi.row === -1 && bi.scrollTop === 0, `de volta ao Felipe, o detalhe volta ao topo (scrollTop ${bi.scrollTop})`);
await pad('up');
bi = await bestInfo();
check(bi.name === 'Ana' && bi.count === `2 de ${TOTAL} pistas` && bi.none === TOTAL - 2 && bi.chips === TOTAL, `Ana: ${bi.count}, ${bi.none} pistas com "—"`);
await shot('06-jogadores-ana');

// Conquistas: o controle chega à última.
await pad('right');
await settle();
const cards = await page.evaluate(() => document.querySelectorAll('.ach-card').length);
await pad('down', cards - 1);
const lastCard = await page.evaluate(() => {
  const f = document.querySelector('.ach-card.focus'); const s = document.querySelector('.rec-scroll').getBoundingClientRect();
  const r = f?.getBoundingClientRect();
  return f ? { name: f.querySelector('.ach-name')?.textContent, inside: r.top >= s.top - 0.5 && r.bottom <= s.bottom + 0.5, last: f === [...document.querySelectorAll('.ach-card')].pop() } : null;
});
check(!!lastCard?.last && lastCard.inside, `aba Conquistas: controle chega à última (${lastCard?.name})`);
await shot('07-conquistas-fim');

console.log('erros de página:', errors.length ? errors.join('\n') : 'nenhum');
await browser.close();
if (errors.length || fails.length) { console.log(`FALHOU: ${fails.length} conferências, ${errors.length} erros`); process.exit(1); }
console.log('playtest dos recordes OK');
