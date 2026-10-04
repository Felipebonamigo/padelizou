// Playtest de layout (passo 5.5): abre cada tela de menu em todas as resoluções alvo, com texto
// normal e com "Texto grande", e confere quatro regras (docs/TELAS.md):
//   1. nada focável fora da viewport (o que mora numa caixa com rolagem própria, como a tabela do
//      resultado, conta pela caixa: ela tem de estar inteira na tela);
//   2. sem rolagem inesperada da tela (.screen) nem da página;
//   3. nenhum rótulo cortado com reticências (text-overflow: ellipsis que de fato cortou);
//   4. nenhum par de elementos irmãos sobreposto (cada irmão conta com o que transborda dele).
// Depois mede o HUD do Steam Deck: 2 jogadores em tela dividida a 1280×800, números de posição,
// volta e velocidade com pelo menos 12 px efetivos, e a legenda dos marcos sem cobrir os painéis dos cantos.
// Uso: node scripts/playtest-layout.mjs [url] [prefixo-das-capturas]
//   NC_LAYOUT_ONLY=options,lobby-4   só essas telas      NC_LAYOUT_RES=1280x720,1024x640   só essas resoluções
//   NC_LAYOUT_TEXT=normal|large      só um tamanho de texto      NC_LAYOUT_SKIP_HUD=1   pula a parte do HUD
//   NC_LAYOUT_CSS=arquivo.css        injeta CSS antes de medir (experimentar um ajuste sem refazer o build)
//   NC_LAYOUT_SHOTS=1                fotografa também as combinações limpas (sem ele, só as que falham e as mais apertadas)
import { chromium } from 'playwright';
import { readFileSync, writeFileSync } from 'node:fs';

const url = process.argv[2] ?? 'http://localhost:4174/';
const out = process.argv[3] ?? 'scratch/layout';
const exe = process.env.CHROME_PATH ?? '/opt/pw-browsers/chromium-1194/chrome-linux/chrome';

/** Resoluções alvo: 720p, Steam Deck (16:10), notebook comum, 900p, 1080p, 1440p e o mínimo suportado. */
const RESOLUTIONS = [[1280, 720], [1280, 800], [1366, 768], [1600, 900], [1920, 1080], [2560, 1440], [1024, 640]]
  .filter(([w, h]) => !process.env.NC_LAYOUT_RES || process.env.NC_LAYOUT_RES.split(',').includes(`${w}x${h}`));
const TEXT_SIZES = [false, true].filter((large) => !process.env.NC_LAYOUT_TEXT || process.env.NC_LAYOUT_TEXT === (large ? 'large' : 'normal'));
/** Tamanho mínimo, em px efetivos, dos números do HUD (posição, volta, tempo, velocidade) no Steam Deck. */
const HUD_MIN_PX = 12;
/** Rótulos do HUD (nome do jogador, COMBUSTÍVEL, KM/H): menores que os números, mas legíveis. */
const HUD_LABEL_MIN_PX = 10;

const browser = await chromium.launch({ executablePath: exe, args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
page.setDefaultTimeout(240000);
const errors = [];
page.on('pageerror', (e) => errors.push('pageerror: ' + e.message));
page.on('console', (m) => { if (m.type() === 'error') errors.push(`console: ${m.text()}`); });
const fails = [];
const check = (cond, msg) => { console.log(`${cond ? '✓' : '✗'} ${msg}`); if (!cond) fails.push(msg); };

const humans = (n) => Array.from({ length: n }, (_, i) => ({ seat: i, name: `P${i + 1}`, carId: ['falcao', 'trovao', 'tornado', 'camelo'][i], teamId: 0, color: ['#ffd23f', '#3ddc84', '#4fc3f7', '#ff7ab6'][i] }));

// Pior caso da escolha de carro (onda F, 14 carros): os 7 à venda liberados e, nos assentos do lobby, os de
// nome mais largo nos assentos que a corrida de preparação não troca (P3 "POROROCA V10", P4 "IARA TURBO").
// Só num save vazio (a primeira carga): não apaga o save de ninguém.
await page.addInitScript(() => {
  if (localStorage.getItem('nitro-crew.save')) return;
  // Expedição Brasil no meio (onda G): Sudeste, Sul e Centro-Oeste carimbados e a Bahia; o Mundial ainda fechado
  // (a aba dele mostra o aviso do porquê, a linha a mais que aperta a tela).
  const done = ['br_rj', 'br_sp', 'br_mg', 'br_es', 'br_pr', 'br_sc', 'br_rs', 'br_df', 'br_go', 'br_ms', 'br_mt', 'br_ba'];
  localStorage.setItem('nitro-crew.save', JSON.stringify({
    carsUnlocked: ['curupira', 'sucuri', 'carcara', 'pororoca', 'iara', 'boitata', 'beijaflor'],
    seatCars: ['falcao', 'trovao', 'pororoca', 'iara'],
    cupsCompleted: done,
    stamps: ['RJ', 'SP', 'MG', 'ES', 'PR', 'SC', 'RS', 'DF', 'GO', 'MS', 'MT', 'BA'],
  }));
});
await page.goto(url, { waitUntil: 'networkidle' });
await page.waitForTimeout(800);
await page.evaluate(() => {
  const s = window.nc.session;
  s.settings.quality = 'low';
  // Guarda o que a sessão passa para as telas com dados (resultado), para remontá-las depois.
  window.__shown = {};
  const show = s.menus.show.bind(s.menus);
  s.menus.show = (screen, data) => { if (data !== undefined) window.__shown[screen] = data; return show(screen, data); };
});

// ───────────── Preparação: uma corrida de copa de verdade (resultado, classificação, "Continuar") ─────────────
// Com NC_LAYOUT_ONLY sem as telas que dependem dela (resultado, classificação, "Continuar" no menu), pula a corrida.
const ONLY = process.env.NC_LAYOUT_ONLY?.split(',') ?? null;
if (!ONLY || ONLY.some((id) => ['main', 'results', 'results-busy', 'results-stamp', 'standings'].includes(id))) {
  console.log('preparando: corrida da Copa Rio de Janeiro com 2 jogadores…');
  await page.evaluate((h) => { const s = window.nc.session; s.menus.hide(); s.debugBind(0, 'kb1'); s.debugBind(1, 'kb2'); s.startCup('br_rj', h); }, humans(2));
  await page.keyboard.down('ArrowUp'); await page.keyboard.down('KeyW');
  for (let i = 0; i < 60; i++) {
    const phase = await page.evaluate(() => { window.nc.session.debugStep(600); return window.nc.session.race?.state.phase; });
    if (phase === 'finished') break;
  }
  await page.keyboard.up('ArrowUp'); await page.keyboard.up('KeyW');
  // O resultado aparece 3 s de jogo depois da bandeirada; o playtest não precisa esperar esses quadros.
  await page.evaluate(() => { const r = window.nc.session.race; if (r) r.overFor = 99; });
  for (let i = 0; i < 60 && (await page.evaluate(() => window.nc.session.menus.current())) !== 'results'; i++) await page.waitForTimeout(500);
  check(await page.evaluate(() => !!window.__shown.results), 'corrida terminada e resultado guardado para as medições');
}
if (process.env.NC_LAYOUT_CSS) await page.addStyleTag({ content: readFileSync(process.env.NC_LAYOUT_CSS, 'utf8') });
// Daqui em diante só DOM: o laço de quadros (3D por software, ~1 quadro/s) para de competir com as medições.
await page.evaluate(() => { window.nc.session.stop(); });

// Pior caso do resultado: 14 conquistas de uma vez (como no playtest.mjs) sobre a corrida de copa.
const ACH = ['PRIMEIRA_VITORIA', 'EQUIPE_COMPLETA', 'SEM_ARRANHAO', 'NITRO_TRIPLO', 'EMPURRAO', 'VOLTA_PERFEITA', 'MESTRE_DO_VACUO', 'NITRO_NA_BANDEIRA', 'MADRUGADA', 'SEM_BOX', 'PODIO_DE_EQUIPE', 'DO_ULTIMO_AO_PRIMEIRO', 'COPA_BR_RJ', 'CAMPEAO'];

/** Cada tela: como montá-la dentro da página (roda a cada resolução/tamanho de texto, depois do redimensionamento). */
const SCREENS = [
  ['title', () => window.nc.session.menus.show('title')],
  ['main', () => window.nc.session.menus.show('main')],
  ['lobby-1', () => { const s = window.nc.session; for (let i = 0; i < 4; i++) s.input.unbindSeat(i); s.input.bindSeat(0, 'kb1'); s.menus.show('lobby'); }],
  ['lobby-4', () => { const s = window.nc.session; ['kb1', 'kb2', 'gp0', 'gp1'].forEach((d, i) => s.input.bindSeat(i, d)); s.menus.show('lobby'); }],
  // Copas em duas etapas (onda G): abre na aba da fronteira (Nordeste, 9 estados: a lista mais longa).
  ['cups', () => window.nc.session.menus.show('cups')],
  // Mundial fechado: o aviso do porquê em cima da lista e a copa de 4 pistas (o detalhe mais alto), na última copa.
  ['cups-last', () => {
    window.nc.session.menus.show('cups');
    const key = (code, n) => { for (let i = 0; i < n; i++) document.dispatchEvent(new KeyboardEvent('keydown', { code, bubbles: true })); };
    key('ArrowRight', 5); key('ArrowDown', 6);
  }],
  // Uma região de 3 estados (Sul): a lista mais curta, todos carimbados.
  ['cups-sul', () => { window.nc.session.menus.show('cups'); document.querySelector('.cup-tab[data-tab="sul"]')?.click(); }],
  ['tracks', () => window.nc.session.menus.show('tracks')],
  // Fim da grade: o Mundial (copas de 4) depois das copas de 3 da Expedição.
  ['tracks-end', () => { window.nc.session.menus.show('tracks'); for (let i = 0; i < 40; i++) document.dispatchEvent(new KeyboardEvent('keydown', { code: 'ArrowDown', bubbles: true })); }],
  // Passaporte: abre no último carimbo (Bahia, cartão-postal completo); e um estado sem carimbo (o aviso a mais).
  ['passport', () => window.nc.session.menus.show('passport')],
  ['passport-off', () => { window.nc.session.menus.show('passport'); document.dispatchEvent(new KeyboardEvent('keydown', { code: 'ArrowDown', bubbles: true })); document.dispatchEvent(new KeyboardEvent('keydown', { code: 'ArrowRight', bubbles: true })); }],
  ['career', () => window.nc.session.menus.show('career')],
  // Dois pilotos: P1 na vitrine do carro mais caro (a etiqueta de preço na coluna da metade da tela).
  ['garage-2', () => {
    const s = window.nc.session; for (let i = 0; i < 4; i++) s.input.unbindSeat(i); ['kb1', 'kb2'].forEach((d, i) => s.input.bindSeat(i, d));
    const hs = [0, 1].map((i) => ({ seat: i, name: `Piloto ${i + 1}`, carId: ['falcao', 'tornado'][i], teamId: 0, color: '#fff' }));
    s.handleMenuEvent({ type: 'startCareer', humans: hs, resume: false });
    document.dispatchEvent(new KeyboardEvent('keydown', { code: 'ArrowLeft', bubbles: true }));
  }],
  ['garage-4', () => {
    const s = window.nc.session; ['kb1', 'kb2', 'gp0', 'gp1'].forEach((d, i) => s.input.bindSeat(i, d));
    // Os nomes livres mais largos (o carro da carreira nova é o do lobby, se for livre).
    const hs = [0, 1, 2, 3].map((i) => ({ seat: i, name: `Piloto ${i + 1}`, carId: ['trovao', 'tornado', 'saci', 'boto'][i], teamId: 0, color: '#fff' }));
    s.handleMenuEvent({ type: 'startCareer', humans: hs, resume: false });
    // P1 (setas) volta dois carros (Trovão → Falcão → o último da lista): a vitrine do mais caro, com nome,
    // preço e "faltam" na coluna estreita.
    for (let i = 0; i < 2; i++) document.dispatchEvent(new KeyboardEvent('keydown', { code: 'ArrowLeft', bubbles: true }));
  }],
  ['party', () => window.nc.session.menus.show('party')],
  ['tournament-8', () => {
    const s = window.nc.session; ['kb1', 'kb2', 'gp0', 'gp1'].forEach((d, i) => s.input.bindSeat(i, d));
    s.menus.show('lobby'); // a inscrição conta os controles pelos assentos do lobby
    s.menus.show('tournament');
    for (let i = 0; i < 8; i++) document.querySelector('.tp-row-actions .btn:not(.disabled)')?.click();
  }],
  ['handoff', () => {
    const s = window.nc.session; ['kb1', 'kb2', 'gp0', 'gp1'].forEach((d, i) => s.input.bindSeat(i, d));
    const players = Array.from({ length: 8 }, (_, i) => ({ name: `Piloto ${i + 1}`, carId: ['falcao', 'trovao', 'tornado', 'camelo'][i % 4] }));
    s.handleMenuEvent({ type: 'startTournament', setup: { players, controllers: 4, rounds: 2, trackIds: ['copacabana', 'transpantaneira', 'sampa_noite'], laps: 2 }, seats: [0, 1, 2, 3] });
  }],
  ['tournament-table', () => window.nc.session.menus.show('tournamentTable')],
  ['options', () => window.nc.session.menus.show('options')],
  ['access', () => window.nc.session.menus.show('access')],
  ['controls', () => window.nc.session.menus.show('controls')],
  ['records', () => window.nc.session.menus.show('records')],
  ['credits', () => window.nc.session.menus.show('credits')],
  ['online', () => window.nc.session.menus.show('online')],
  ['tutorial', () => { const s = window.nc.session; ['kb1', 'kb2', 'gp0', 'gp1'].forEach((d, i) => s.input.bindSeat(i, d)); s.menus.show('tutorial'); }],
  ['tutorial-done', () => window.nc.session.menus.show('tutorialDone', { completed: true, players: 4 })],
  ['tutorial-skipped', () => window.nc.session.menus.show('tutorialDone', { completed: false, players: 1 })],
  ['results', () => window.nc.session.menus.show('results', window.__shown.results)],
  ['results-busy', (ach) => window.nc.session.menus.show('results', { ...window.__shown.results, achievements: ach.map((id) => ({ id, seats: [0, 1] })) }), ACH],
  // Carimbo novo que fecha a região (Espírito Santo, com o Sudeste todo carimbado no save): a linha a mais do resultado.
  ['results-stamp', () => window.nc.session.menus.show('results', { ...window.__shown.results, newStamp: 'ES' })],
  ['standings', () => { window.nc.session.menus.show('results', window.__shown.results); document.querySelector('.scr-results .actions .btn-primary')?.click(); }],
  ['pause', () => window.nc.session.menus.show('pause')],
].filter(([id]) => !ONLY || ONLY.includes(id));

/**
 * Caixas feitas para rolar por dentro (listas longas): o foco rola até o item (scrollIntoView), então
 * basta a caixa caber. Qualquer outra caixa com rolagem é defeito de layout.
 */
const ALLOWED_SCROLL = ['.table-wrap', '.rec-scroll', '.pl-list', '.pl-detail', '.cup-rows', '.track-scroll', '.tp-rows'];

/**
 * Conteúdo que tem de caber inteiro na caixa com borda que o envolve (sem transbordar por cima da borda):
 * [seletor do conteúdo, seletor da caixa]. O cartão do carro no lobby transbordava do quadro das setas.
 */
const CONTAINED = [['.car-card', '.car-hero']];

/** Estilo só da medição: sem animação nem a escala do foco (1,03), para medir o layout parado. */
const FREEZE_CSS = '#ui *, #ui *::before, #ui *::after { animation: none !important; transition: none !important; } #ui .focusable.focus { transform: none !important; }';

/** Roda dentro da página: devolve os problemas da tela montada e a folga (px) até a borda de baixo. */
function measure({ ALLOWED_SCROLL, CONTAINED }) {
  const vw = window.innerWidth;
  const vh = window.innerHeight;
  const scr = document.querySelector('#ui .screen');
  const problems = [];
  if (!scr) return { problems: [{ rule: 'tela', what: 'nenhuma .screen montada' }], slack: 0, screen: null };
  const label = (el) => {
    const cls = (typeof el.className === 'string' ? el.className : '').split(/\s+/).filter((c) => c && c !== 'focusable' && c !== 'focus' && c !== 'glass').slice(0, 2).join('.');
    const text = (el.textContent ?? '').trim().replace(/\s+/g, ' ').slice(0, 28);
    return `${el.tagName.toLowerCase()}${cls ? '.' + cls : ''}${text ? ` "${text}"` : ''}`;
  };
  const styleOf = (el) => getComputedStyle(el);
  const shown = (el) => {
    const cs = styleOf(el);
    if (cs.display === 'contents') return true;
    if (cs.display === 'none' || cs.visibility === 'hidden') return false;
    const r = el.getBoundingClientRect();
    return r.width > 0 && r.height > 0;
  };
  const inFlow = (el) => { const p = styleOf(el).position; return p !== 'absolute' && p !== 'fixed'; };

  // 2. Rolagem inesperada.
  const sy = scr.scrollHeight - scr.clientHeight;
  const sx = scr.scrollWidth - scr.clientWidth;
  if (sy > 1) problems.push({ rule: 'rolagem', what: `a tela rola ${sy}px na vertical` });
  if (sx > 1) problems.push({ rule: 'rolagem', what: `a tela rola ${sx}px na horizontal` });
  const doc = document.scrollingElement;
  if (doc && (doc.scrollHeight > vh + 1 || doc.scrollWidth > vw + 1)) problems.push({ rule: 'rolagem', what: `a página passa da janela (${doc.scrollWidth}×${doc.scrollHeight})` });

  // Caixas internas com rolagem: só as listas feitas para rolar (ALLOWED_SCROLL); qualquer outra que role é defeito.
  for (const el of scr.querySelectorAll('*')) {
    const cs = styleOf(el);
    if (!/(auto|scroll)/.test(cs.overflowY + cs.overflowX) || !shown(el)) continue;
    const dy = el.scrollHeight - el.clientHeight;
    const dx = el.scrollWidth - el.clientWidth;
    if ((dy > 1 || dx > 1) && !ALLOWED_SCROLL.some((sel) => el.matches(sel))) problems.push({ rule: 'rolagem', what: `${label(el)} rola ${Math.max(dy, dx)}px por dentro` });
  }

  // 1. Focáveis fora da viewport ou escondidos por uma caixa que recorta (overflow) — nas listas que
  //    rolam de propósito basta a caixa estar inteira na tela (o foco rola até o item).
  let slack = Infinity;
  for (const el of scr.querySelectorAll('.focusable, button, input, select, textarea, [tabindex]')) {
    if (!shown(el)) continue;
    const r = el.getBoundingClientRect();
    let vis = { left: 0, top: 0, right: vw, bottom: vh };
    let clipper = 'a janela';
    let box = el;
    for (let p = el.parentElement; p && p !== document.body; p = p.parentElement) {
      const cs = styleOf(p);
      if (cs.overflowX === 'visible' && cs.overflowY === 'visible') continue;
      if (p !== scr && ALLOWED_SCROLL.some((sel) => p.matches(sel))) { box = p; break; }
      if (p === scr) continue; // a rolagem da própria tela já é a regra 2
      const c = p.getBoundingClientRect();
      const next = { left: Math.max(vis.left, c.left), top: Math.max(vis.top, c.top), right: Math.min(vis.right, c.right), bottom: Math.min(vis.bottom, c.bottom) };
      if (next.left > vis.left || next.top > vis.top || next.right < vis.right || next.bottom < vis.bottom) clipper = label(p);
      vis = next;
    }
    const b = box === el ? r : box.getBoundingClientRect();
    slack = Math.min(slack, vh - b.bottom);
    if (box !== el) vis = { left: 0, top: 0, right: vw, bottom: vh };
    const out = Math.max(vis.left - b.left, vis.top - b.top, b.right - vis.right, b.bottom - vis.bottom);
    if (out > 0.5) problems.push({ rule: 'fora', what: `${label(box)} passa ${Math.round(out)}px de ${out === b.bottom - vis.bottom && vis.bottom === vh ? 'a janela' : clipper} (${Math.round(b.left)},${Math.round(b.top)} → ${Math.round(b.right)},${Math.round(b.bottom)})` });
  }

  // 3. Rótulo cortado com reticências.
  for (const el of scr.querySelectorAll('*')) {
    const cs = styleOf(el);
    if (cs.textOverflow !== 'ellipsis' || !shown(el)) continue;
    if (el.scrollWidth > el.clientWidth + 1) problems.push({ rule: 'reticências', what: `${label(el)} cortado (${el.scrollWidth}px em ${el.clientWidth}px)` });
  }
  // 3b. Texto recortado sem reticências: caixa que esconde o que passa da largura (overflow hidden) com
  //     texto dentro que não coube ("Tanqu" no lugar de "Tanque").
  for (const el of scr.querySelectorAll('*')) {
    const cs = styleOf(el);
    if (cs.textOverflow === 'ellipsis' || !/(hidden|clip)/.test(cs.overflowX) || !shown(el)) continue;
    if (ALLOWED_SCROLL.some((sel) => el.matches(sel)) || !(el.textContent ?? '').trim()) continue;
    if (el.scrollWidth > el.clientWidth + 1) problems.push({ rule: 'recortado', what: `${label(el)} recortado (${el.scrollWidth}px em ${el.clientWidth}px)` });
  }

  // 4. Irmãos sobrepostos: cada um conta com o que transborda dele (menos o que um overflow recorta).
  const union = (a, b) => (!a ? b : !b ? a : { left: Math.min(a.left, b.left), top: Math.min(a.top, b.top), right: Math.max(a.right, b.right), bottom: Math.max(a.bottom, b.bottom) });
  const memo = new Map();
  const extent = (el) => {
    if (memo.has(el)) return memo.get(el);
    const cs = styleOf(el);
    let u = null;
    if (cs.display !== 'contents') {
      const r = el.getBoundingClientRect();
      if (r.width > 0 || r.height > 0) u = { left: r.left, top: r.top, right: r.right, bottom: r.bottom };
    }
    const clips = cs.display !== 'contents' && (cs.overflowX !== 'visible' || cs.overflowY !== 'visible');
    if (!clips && el.namespaceURI === 'http://www.w3.org/1999/xhtml') {
      for (const node of el.childNodes) {
        if (node.nodeType === Node.TEXT_NODE && node.textContent.trim()) {
          const range = document.createRange();
          range.selectNodeContents(node);
          const r = range.getBoundingClientRect();
          if (r.width > 0) u = union(u, { left: r.left, top: r.top, right: r.right, bottom: r.bottom });
        } else if (node.nodeType === Node.ELEMENT_NODE && shown(node) && inFlow(node)) {
          u = union(u, extent(node));
        }
      }
    }
    memo.set(el, u);
    return u;
  };
  for (const parent of [scr, ...scr.querySelectorAll('*')]) {
    if (parent.namespaceURI !== 'http://www.w3.org/1999/xhtml' || !shown(parent)) continue;
    const kids = [...parent.children].filter((k) => shown(k) && inFlow(k));
    for (let i = 0; i < kids.length; i++) {
      for (let j = i + 1; j < kids.length; j++) {
        const a = extent(kids[i]);
        const b = extent(kids[j]);
        if (!a || !b) continue;
        const w = Math.min(a.right, b.right) - Math.max(a.left, b.left);
        const h = Math.min(a.bottom, b.bottom) - Math.max(a.top, b.top);
        if (w > 1 && h > 1) problems.push({ rule: 'sobreposição', what: `${label(kids[i])} × ${label(kids[j])} (${Math.round(w)}×${Math.round(h)}px, em ${label(parent)})` });
      }
    }
  }
  // 5. Conteúdo que transborda da própria caixa com borda.
  for (const [inner, outer] of CONTAINED) {
    for (const el of scr.querySelectorAll(inner)) {
      const box = el.closest(outer);
      if (!box || !shown(el)) continue;
      const a = extent(el);
      const b = box.getBoundingClientRect();
      const out = Math.max(b.left - a.left, b.top - a.top, a.right - b.right, a.bottom - b.bottom);
      if (out > 0.5) problems.push({ rule: 'transborda', what: `${label(el)} passa ${Math.round(out)}px da borda de ${label(box)}` });
    }
  }
  return { problems, slack: Number.isFinite(slack) ? Math.round(slack) : null, screen: scr.dataset.screen ?? null };
}

async function mount(id, fn, arg) {
  await page.evaluate(({ src, arg }) => {
    // eslint-disable-next-line no-new-func
    const f = new Function(`return (${src})`)();
    f(arg);
  }, { src: fn.toString(), arg });
  await page.evaluate(() => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))));
  return page.evaluate(() => document.querySelector('#ui .screen')?.dataset.screen ?? null);
}

const setLargeText = (large) => page.evaluate((on) => {
  const s = window.nc.session;
  s.settings.largeText = on;
  s.handleMenuEvent({ type: 'settingsChanged', settings: s.settings });
}, large);

const report = [];
const worst = [];
for (const [w, h] of RESOLUTIONS) {
  await page.setViewportSize({ width: w, height: h });
  for (const large of TEXT_SIZES) {
    await setLargeText(large);
    for (const [id, fn, arg] of SCREENS) {
      const tag = `${id} ${w}×${h}${large ? ' texto grande' : ''}`;
      const screen = await mount(id, fn, arg);
      const freeze = await page.addStyleTag({ content: FREEZE_CSS });
      const m = await page.evaluate(measure, { ALLOWED_SCROLL, CONTAINED });
      await freeze.evaluate((el) => el.remove());
      report.push({ id, w, h, large, screen, slack: m.slack, problems: m.problems });
      if (m.problems.length) {
        console.log(`✗ ${tag} (${screen}): ${m.problems.length} problema(s)`);
        for (const p of m.problems.slice(0, 6)) console.log(`    [${p.rule}] ${p.what}`);
        if (m.problems.length > 6) console.log(`    … e mais ${m.problems.length - 6}`);
        await page.evaluate(() => { for (const a of document.getAnimations()) { try { a.finish(); } catch { /* infinita (a barra fantasma da garagem): fica como está */ } } });
        await page.screenshot({ path: `${out}-FAIL-${id}-${w}x${h}${large ? '-grande' : ''}.png` });
      } else if (process.env.NC_LAYOUT_SHOTS) {
        await page.evaluate(() => { for (const a of document.getAnimations()) { try { a.finish(); } catch { /* infinita (a barra fantasma da garagem): fica como está */ } } });
        await page.screenshot({ path: `${out}-${id}-${w}x${h}${large ? '-grande' : ''}.png` });
      }
      worst.push({ id, w, h, large, slack: m.slack });
    }
  }
}
const bad = report.filter((r) => r.problems.length);
check(bad.length === 0, `telas de menu sem problema de layout: ${report.length - bad.length}/${report.length} combinações limpas`);
writeFileSync(`${out}-report.json`, JSON.stringify(report, null, 1));

// As mais apertadas (menor folga até a borda de baixo) ficam fotografadas para conferência visual.
const tight = worst.filter((x) => x.slack !== null).sort((a, b) => a.slack - b.slack).slice(0, 6);
console.log('mais apertadas (folga até a borda de baixo):', tight.map((x) => `${x.id} ${x.w}×${x.h}${x.large ? ' grande' : ''}: ${x.slack}px`).join(' · '));
for (const x of tight) {
  await page.setViewportSize({ width: x.w, height: x.h });
  await setLargeText(x.large);
  const def = SCREENS.find(([id]) => id === x.id);
  await mount(x.id, def[1], def[2]);
  await page.evaluate(() => { for (const a of document.getAnimations()) { try { a.finish(); } catch { /* infinita (a barra fantasma da garagem): fica como está */ } } });
  await page.screenshot({ path: `${out}-tight-${x.id}-${x.w}x${x.h}${x.large ? '-grande' : ''}.png` });
}
await setLargeText(false);

// ───────────── HUD no Steam Deck: 2 jogadores em tela dividida a 1280×800 ─────────────
// Números (posição, volta, tempo, velocidade, marcha) com pelo menos HUD_MIN_PX; rótulos (nome, COMBUSTÍVEL,
// KM/H) com pelo menos HUD_LABEL_MIN_PX; os painéis do HUD de um viewport sem se sobrepor. O pior caso é o
// HUD em 80% (a menor opção de acessibilidade) com 4 jogadores em 1024×640.
if (!process.env.NC_LAYOUT_SKIP_HUD) {
  for (const [n, w, h, hudScale] of [[2, 1280, 800, 1], [4, 1280, 800, 1], [2, 1280, 720, 1], [4, 1024, 640, 0.8]]) {
    await page.setViewportSize({ width: w, height: h });
    await page.evaluate(({ hs, hudScale }) => {
      const s = window.nc.session;
      // Com o laço de quadros parado o navegador não despacha o resize sozinho: a sessão precisa saber do tamanho novo.
      window.dispatchEvent(new Event('resize'));
      s.settings.hudScale = hudScale;
      s.handleMenuEvent({ type: 'settingsChanged', settings: s.settings });
      s.menus.hide();
      s.debugBind(0, 'kb1'); s.debugBind(1, 'kb2');
      s.startQuick('copacabana', 3, hs);
      s.debugStep(60 * 6);
      s.frame(performance.now()); // um quadro desenhado (HUD atualizado) sem religar o laço
    }, { hs: humans(n), hudScale });
    // A legenda dos marcos (src/render/caption/) na tela com o nome e o lugar mais compridos: ela não pode cobrir os
    // painéis dos cantos (combustível, velocímetro, minimapa) em nenhuma divisão de tela. O laço está parado: a classe
    // posta à mão fica até a medição.
    await page.evaluate(() => {
      for (const cap of document.querySelectorAll('#hud .vp .lmk')) {
        cap.querySelector('.lmk-name').textContent = 'Passarela nas copas do Daintree';
        const where = cap.querySelector('.lmk-where');
        where.textContent = 'Território do Norte · Austrália'; where.classList.remove('empty');
        cap.classList.add('show');
      }
    });
    await page.waitForTimeout(800);
    const hud = await page.evaluate(() => {
      const px = (el) => (el ? parseFloat(getComputedStyle(el).fontSize) : null);
      return [...document.querySelectorAll('#hud .vp')].filter((v) => v.style.display !== 'none').map((vp) => {
        const panels = [...vp.querySelectorAll(':scope > .tl, :scope > .tr, :scope > .mates, :scope > .bl, :scope > .br, :scope > .mini, :scope > .lmk')]
          .filter((e) => getComputedStyle(e).display !== 'none' && e.getBoundingClientRect().width > 0);
        const overlaps = [];
        for (let i = 0; i < panels.length; i++) for (let j = i + 1; j < panels.length; j++) {
          const a = panels[i].getBoundingClientRect(); const b = panels[j].getBoundingClientRect();
          if (Math.min(a.right, b.right) - Math.max(a.left, b.left) > 1 && Math.min(a.bottom, b.bottom) - Math.max(a.top, b.top) > 1) overlaps.push(`${panels[i].className.split(' ')[0]}×${panels[j].className.split(' ')[0]}`);
        }
        return {
          nums: { pos: px(vp.querySelector('.pos-n')), of: px(vp.querySelector('.pos-of')), lap: px(vp.querySelector('.lap')), time: px(vp.querySelector('.time')),
            laps: px(vp.querySelector('.laps')), kmh: px(vp.querySelector('.kmh')), gear: px(vp.querySelector('.gear')), landmark: px(vp.querySelector('.lmk-name')) },
          labels: { tag: px(vp.querySelector('.tag')), label: px(vp.querySelector('.label')), unit: px(vp.querySelector('.unit')), caption: px(vp.querySelector('.lmk-where')) },
          overlaps,
        };
      });
    });
    const minOf = (key) => Math.min(...hud.flatMap((v) => Object.values(v[key])));
    const fmt = (key) => Object.entries(hud[0]?.[key] ?? {}).map(([k, x]) => `${k} ${x?.toFixed(1)}`).join(' ');
    const tag = `HUD ${n}P a ${w}×${h}${hudScale !== 1 ? ` (HUD ${Math.round(hudScale * 100)}%)` : ''}`;
    check(hud.length === n && minOf('nums') >= HUD_MIN_PX - 0.05, `${tag}: menor número ${minOf('nums').toFixed(1)}px (mín. ${HUD_MIN_PX}) — ${fmt('nums')}`);
    check(minOf('labels') >= HUD_LABEL_MIN_PX - 0.05, `${tag}: menor rótulo ${minOf('labels').toFixed(1)}px (mín. ${HUD_LABEL_MIN_PX}) — ${fmt('labels')}`);
    const ov = hud.flatMap((v) => v.overlaps);
    check(ov.length === 0, `${tag}: painéis do HUD sem sobreposição (${ov.join(', ') || 'nenhuma'})`);
    await page.screenshot({ path: `${out}-hud-${n}p-${w}x${h}${hudScale !== 1 ? '-80' : ''}.png` });
  }
  await page.evaluate(() => { const s = window.nc.session; s.settings.hudScale = 1; s.handleMenuEvent({ type: 'settingsChanged', settings: s.settings }); });
}

check(errors.length === 0, `sem erros no console (${errors.slice(0, 3).join(' | ')})`);
await browser.close();
console.log(fails.length ? `\n${fails.length} falha(s)` : '\ntudo verde');
process.exit(fails.length ? 1 : 0);
