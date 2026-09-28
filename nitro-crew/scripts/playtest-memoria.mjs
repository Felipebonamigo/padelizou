// Sessão longa no Chromium headless: procura vazamento de memória (docs/DESEMPENHO.md).
// Roda ~60 corridas curtas em sequência — largar, reiniciar, trocar de pista, 1→4 jogadores, qualidade baixa
// e alta, terminar até o resultado, voltar ao menu e abrir as telas dos menus — e, ao fim de cada bloco (sempre
// no menu principal, depois de coletar o lixo), mede:
//   heap JS (performance.memory, com --enable-precise-memory-info), geometrias/texturas/programas do Three
//   (renderer.info), materiais que o renderizador conhece e ninguém liberou (material largado sem dispose()
//   prende o programa na GPU para sempre), objetos na cena, nós do DOM (vivos e os que o Chrome ainda guarda), ouvintes de evento
//   (CDP Memory.getDOMCounters) e os filhos do HUD e dos menus.
// O roteiro se repete a cada CYCLE blocos (as mesmas corridas: sementes fixas) e o veredito compara o último
// ciclo inteiro com o anterior, posição a posição — o primeiro ciclo é aquecimento (tudo que é criado uma vez).
// Contador que cresce em TODAS as posições cresce sem parar: vazamento, e o roteiro falha. Um degrau isolado
// (algo desenhado pela primeira vez, como um objeto do cenário que só agora entrou na câmera do fundo dos menus,
// que anda com o relógio acumulado) é mostrado como aviso, desde que pequeno.
// Exige `npx vite build && npx vite preview --port P` no ar.
// Uso: node scripts/playtest-memoria.mjs [url] [prefixo-das-capturas] [blocos=36]
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from 'playwright';

const url = process.argv[2] ?? 'http://localhost:4174/';
const out = process.argv[3] ?? 'scratch/mem';
const BLOCKS = Number(process.argv[4] ?? 36);
const exe = process.env.CHROME_PATH ?? '/opt/pw-browsers/chromium-1194/chrome-linux/chrome';
fs.mkdirSync(path.dirname(out), { recursive: true }); // scratch/ não vem no clone (.gitignore)

/** Pistas do roteiro (cenários e horários diferentes: costa/dia, cidade/noite, deserto, alpino, tropical, savana). */
const TRACKS = ['copacabana', 'sampa_noite', 'canion', 'monte_fuji', 'daintree', 'kruger'];
/** Blocos até o roteiro se repetir: mmc(6 pistas, 4 lotações, 3 qualidades) = 12. */
const CYCLE = 12;
/** Telas visitadas no menu a cada bloco (as que abrem sem dados de corrida). */
const SCREENS = ['lobby', 'cups', 'tracks', 'options', 'controls', 'records', 'credits', 'party', 'access'];
/** Folga do heap JS entre ciclos: a coleta não é exata (compilação, caches internos do V8). */
const HEAP_SLACK_BYTES = 1.5 * 1024 * 1024;
/** Maior degrau isolado aceito num contador entre dois ciclos (primeira vez que algo é desenhado). */
const STEP_SLACK = 3;

const browser = await chromium.launch({
  executablePath: exe,
  args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist', '--autoplay-policy=no-user-gesture-required',
    '--enable-precise-memory-info', '--js-flags=--expose-gc'],
});
const page = await browser.newPage({ viewport: { width: 320, height: 180 } });
page.setDefaultTimeout(240000);
// Roteiro reproduzível: o jogo sorteia a semente da corrida (Date.now ^ Math.random) e a pista do fundo dos
// menus com Math.random. Aqui o Date.now da página é fixo (só a semente o usa) e o Math.random tem semente,
// posta de novo na mesma chamada que larga, reinicia ou volta ao menu — os sons também sorteiam, e em tempo
// real, então uma semente posta antes, noutra chamada, já chegaria gasta. O bloco k e o k+CYCLE sorteiam igual.
await page.addInitScript(() => {
  Date.now = () => 0;
  let s = 0x9e3779b9;
  window.__ncSeed = (n) => { s = (0x9e3779b9 ^ Math.imul(n + 1, 0x85ebca6b)) | 0; };
  Math.random = () => { s = (s + 0x6d2b79f5) | 0; let t = Math.imul(s ^ (s >>> 15), 1 | s); t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t; return ((t ^ (t >>> 14)) >>> 0) / 4294967296; };
});
const errors = [];
page.on('pageerror', (e) => errors.push('pageerror: ' + e.message));
page.on('console', (m) => { if (m.type() === 'error') errors.push(`console: ${m.text()}`); });
const cdp = await page.context().newCDPSession(page);

await page.goto(url, { waitUntil: 'networkidle' });
await page.waitForTimeout(800);
// Sem o laço do requestAnimationFrame: os quadros são desenhados pelo roteiro (session.frame), um a um.
// Mesmo código do laço, mas sem o 3D por software disputando a CPU entre um passo e outro.
await page.evaluate(() => window.nc.session.stop());
// Materiais vivos no renderizador. O three não conta materiais (renderer.info só tem geometrias e texturas):
// um material ganha entrada nas propriedades do renderizador ao ser desenhado pela primeira vez e só a perde
// no dispose(). Material largado sem dispose() some do heap (a entrada é de um WeakMap), mas o programa dele
// fica preso (usedTimes nunca volta a zero) — então a conta é "entradas criadas − entradas removidas".
// Não serve somar usedTimes dos programas: um material que vive a sessão inteira ganha um programa por
// configuração em que é desenhado (com/sem sombra, na tela/no alvo do bloom) e a soma sobe devagar até todos
// terem visto todas, sem vazar nada (docs/DESEMPENHO.md).
await page.evaluate(() => {
  const props = window.nc.session.renderer.__gl.properties;
  const { get, remove, has } = props;
  let live = 0;
  props.get = (o) => { if (o && o.isMaterial && !has(o)) live++; return get(o); };
  props.remove = (o) => { if (o && o.isMaterial && has(o)) live--; return remove(o); };
  window.__ncLiveMaterials = () => live;
});

const humans = (n, assist) => Array.from({ length: n }, (_, i) => ({
  seat: i, name: `P${i + 1}`, carId: ['falcao', 'trovao', 'tornado', 'camelo'][i], teamId: 0, color: ['#ffd23f', '#3ddc84', '#4fc3f7', '#ff7ab6'][i],
  ...(i === 0 && assist ? { assist } : {}),
}));

/** Quadros desenhados de verdade (sem esperar o requestAnimationFrame lento do 3D por software). */
const frames = (n, stepMs = 250) => page.evaluate(({ n, stepMs }) => {
  const s = window.nc.session;
  window.__ncNow = window.__ncNow ?? performance.now();
  for (let i = 0; i < n; i++) { window.__ncNow += stepMs; s.frame(window.__ncNow); }
}, { n, stepMs });

async function measure() {
  // Cada nota da música e cada efeito sonoro é um nó de áudio com ouvinte de 'ended', e o sequenciador
  // agenda notas 100 ms à frente sem parar: com a música tocando, a contagem de ouvintes varia de 0 a ~22
  // conforme o instante. A música cala durante a medição (fade de 0,5 s + o que já estava agendado + o
  // desligamento do ganho 1 s depois) e volta em seguida — ela toca normalmente no resto do bloco.
  const music = await page.evaluate(() => { const a = window.nc.session.audio; const m = a.currentMusic(); a.setMusic(null); return m; });
  await page.waitForTimeout(2500);
  await page.evaluate(() => { window.gc(); window.gc(); });
  await cdp.send('HeapProfiler.collectGarbage');
  const counters = await cdp.send('Memory.getDOMCounters');
  const m = await page.evaluate(() => {
    const s = window.nc.session;
    const gl = s.renderer.__gl;
    let objects = 0;
    s.renderer.__scene.traverse(() => { objects++; });
    return {
      heap: performance.memory.usedJSHeapSize,
      geometries: gl.info.memory.geometries, textures: gl.info.memory.textures, programs: gl.info.programs ? gl.info.programs.length : -1,
      materials: window.__ncLiveMaterials(),
      objects, domLive: document.getElementsByTagName('*').length,
      hud: document.getElementById('hud').getElementsByTagName('*').length, ui: document.getElementById('ui').getElementsByTagName('*').length,
      menu: s.menus.current(),
    };
  });
  await page.evaluate((m) => window.nc.session.audio.setMusic(m), music);
  return { ...m, domNodes: counters.nodes, listeners: counters.jsEventListeners, documents: counters.documents };
}

/** Um bloco = 2 corridas (largar e reiniciar), às vezes até o resultado, e a volta ao menu. */
async function block(k) {
  const track = TRACKS[k % TRACKS.length];
  const n = 1 + (k % 4);
  const quality = k % 3 === 2 ? 'high' : 'low';
  const finish = k % 4 === 3;
  const seed = (k % CYCLE) * 4;
  // Nos blocos que vão até o resultado, o P1 (teclado, direção assistida completa) acelera e termina; os
  // outros ficam parados e a corrida fecha pela tolerância depois da chegada dele.
  await page.evaluate(({ track, h, quality, finish, seed }) => {
    const s = window.nc.session;
    s.settings.quality = quality;
    for (let i = 0; i < 4; i++) s.input.unbindSeat(i);
    if (finish) s.debugBind(0, 'kb1');
    window.__ncSeed(seed);
    s.startQuick(track, 1, h);
  }, { track, h: humans(n, finish ? 'full' : undefined), quality, finish, seed });
  await page.evaluate(() => window.nc.session.debugStep(260));
  await frames(2);
  // Reiniciar (o mesmo que "Reiniciar" da pausa): corrida nova, mesma pista e lotação.
  await page.evaluate((seed) => { window.__ncSeed(seed); window.nc.session.handleMenuEvent({ type: 'restart' }); }, seed + 1);
  await page.evaluate(() => window.nc.session.debugStep(260));
  await frames(2);
  if (finish) {
    await page.keyboard.down('ArrowUp');
    await page.evaluate(() => { const s = window.nc.session; for (let i = 0; i < 40 && s.race && s.race.state.phase !== 'finished'; i++) s.debugStep(600); });
    await page.keyboard.up('ArrowUp');
    await frames(16); // 3 s de atraso até a tela de resultado
  }
  const menuAfter = await page.evaluate(() => window.nc.session.menus.current());
  await page.evaluate((seed) => { window.__ncSeed(seed); window.nc.session.handleMenuEvent({ type: 'toMain' }); }, seed + 2);
  await frames(2);
  const visited = await page.evaluate((screens) => {
    const s = window.nc.session;
    const failed = [];
    for (const sc of screens) { try { s.menus.show(sc); s.frame((window.__ncNow += 250)); } catch (e) { failed.push(`${sc}: ${e.message}`); } }
    s.menus.show('main');
    return failed;
  }, SCREENS);
  await frames(2);
  return { track, n, quality, finish, menuAfter, visited };
}

const rows = [];
const base = await measure();
console.log(`início: heap ${(base.heap / 1048576).toFixed(1)} MB, geo ${base.geometries}, tex ${base.textures}, prog ${base.programs}, obj ${base.objects}, DOM ${base.domNodes}, ouvintes ${base.listeners}`);
console.log(' bloco pista          hum qual  fim  | heap MB  geo  tex prog  mat   obj  DOMvivo DOMnós ouvintes  hud   ui');
for (let k = 0; k < BLOCKS; k++) {
  const b = await block(k);
  const m = await measure();
  rows.push({ block: k, ...b, ...m });
  console.log(`${String(k).padStart(6)} ${b.track.padEnd(14)} ${String(b.n).padStart(3)} ${b.quality.padEnd(5)} ${(b.finish ? (b.menuAfter ?? '-') : '').padEnd(4).slice(0, 4)} | ${(m.heap / 1048576).toFixed(1).padStart(7)} ${String(m.geometries).padStart(4)} ${String(m.textures).padStart(4)} ${String(m.programs).padStart(4)} ${String(m.materials).padStart(4)} ${String(m.objects).padStart(5)} ${String(m.domLive).padStart(8)} ${String(m.domNodes).padStart(6)} ${String(m.listeners).padStart(8)} ${String(m.hud).padStart(4)} ${String(m.ui).padStart(4)}${b.visited.length ? '  telas com erro: ' + b.visited.join('; ') : ''}`);
}
await page.setViewportSize({ width: 1280, height: 720 });
await frames(1);
await page.screenshot({ path: `${out}-fim.png` });
fs.writeFileSync(`${out}.json`, JSON.stringify({ url, blocks: BLOCKS, cycle: CYCLE, base, rows, errors }, null, 2));

// Veredito: o último ciclo inteiro contra o anterior, na mesma posição do roteiro.
const fails = [];
const check = (cond, msg) => { console.log(`${cond ? '✓' : '✗'} ${msg}`); if (!cond) fails.push(msg); };
const cycles = Math.floor(rows.length / CYCLE);
if (cycles < 3) {
  console.log(`(só ${rows.length} blocos: são precisos ${3 * CYCLE} — um ciclo de aquecimento e dois para comparar)`);
  fails.push('blocos insuficientes');
} else {
  const a0 = (cycles - 2) * CYCLE; const b0 = (cycles - 1) * CYCLE;
  const races = CYCLE * 2;
  console.log(`ciclo dos blocos ${a0}–${a0 + CYCLE - 1} contra o dos blocos ${b0}–${b0 + CYCLE - 1} (${races} corridas depois):`);
  for (const key of ['geometries', 'textures', 'programs', 'materials', 'objects', 'domLive', 'domNodes', 'listeners', 'hud', 'ui']) {
    const deltas = Array.from({ length: CYCLE }, (_, p) => rows[b0 + p][key] - rows[a0 + p][key]);
    const grew = deltas.filter((d) => d > 0).length;
    const max = Math.max(...deltas);
    const where = `${rows[a0 + CYCLE - 1][key]} → ${rows[b0 + CYCLE - 1][key]} no fim do ciclo; cresceu em ${grew} de ${CYCLE} posições, no máximo +${Math.max(0, max)}`;
    if (grew > 0 && grew < CYCLE && max <= STEP_SLACK) console.log(`⚠ ${key}: degrau isolado, não contínuo (${where})`);
    else check(grew === 0, `${key}: ${where}`);
  }
  const a = rows[b0 - 1]; const b = rows[b0 + CYCLE - 1];
  const growth = b.heap - a.heap;
  check(growth <= HEAP_SLACK_BYTES, `heap JS: ${(a.heap / 1048576).toFixed(2)} → ${(b.heap / 1048576).toFixed(2)} MB no fim de cada ciclo (${growth >= 0 ? '+' : ''}${(growth / 1024).toFixed(0)} KB em ${races} corridas; folga ${(HEAP_SLACK_BYTES / 1024).toFixed(0)} KB)`);
}
check(errors.length === 0, `sem erros na página (${errors.length})${errors.length ? ': ' + errors.slice(0, 5).join(' | ') : ''}`);
await browser.close();
if (fails.length) { console.log(`\n${fails.length} falha(s).`); process.exit(1); }
console.log('\nSem crescimento entre ciclos.');
