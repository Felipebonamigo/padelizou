// Roteiro Playwright do fantasma do contra-relógio pelo fluxo real de teclado:
// título → menu (acha "Contra-relógio" pelo texto) → lobby → pistas → corrida de 2 voltas pilotada
// pelo teclado (volta 1 vira fantasma, volta 2 corre contra ela) → resultado → "Correr de novo"
// (a volta 1 larga do grid e o fantasma lançado fica à frente: captura) → pausa → menu → Recordes
// (a pista mostra o fantasma; Enter exporta; "Importar fantasma" traz o de um amigo) → Opções (desliga).
// O laço do requestAnimationFrame fica parado; os quadros são dados à mão com session.frame().
// Uso: npm run build && npx vite preview --port 4195 --strictPort &
//      node scripts/playtest-ghost.mjs http://localhost:4195/ scratch/gh
import { chromium } from 'playwright';
const url = process.argv[2] ?? 'http://localhost:4195/';
const out = process.argv[3] ?? 'scratch/gh';
const exe = '/opt/pw-browsers/chromium-1194/chrome-linux/chrome';
const browser = await chromium.launch({ executablePath: exe, args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
const page = await browser.newPage({ viewport: { width: 1280, height: 720 }, acceptDownloads: true });
page.setDefaultTimeout(240_000);
const errors = [];
page.on('pageerror', (e) => errors.push('pageerror: ' + e.message));
page.on('console', (m) => { if (m.type() === 'error') errors.push(`console: ${m.text()}`); });
const fails = [];
const t0 = Date.now();
const check = (cond, msg) => { console.log(`${cond ? '✓' : '✗'} [${Math.round((Date.now() - t0) / 1000)}s] ${msg}`); if (!cond) fails.push(msg); };

let clock = 0;
/** Um quadro do jogo com relógio controlado (dt de 1/60 s, a não ser que peça mais). */
const frame = (dtMs = 1000 / 60) => page.evaluate((dt) => { window.__t = (window.__t ?? performance.now()) + dt; window.nc.session.frame(window.__t); }, dtMs + (clock++ * 0));
async function held(code) { await page.keyboard.down(code); await frame(); await page.keyboard.up(code); await frame(); }
async function press(code) { await page.keyboard.press(code); await page.waitForTimeout(60); }
const menu = () => page.evaluate(() => window.nc.session.menus.current());
const focusText = () => page.evaluate(() => (document.querySelector('#ui .focus')?.textContent ?? '').trim());
async function focusBy(re, key = 'ArrowDown', max = 20) {
  for (let i = 0; i < max && !re.test(await focusText()); i++) await press(key);
  return re.test(await focusText());
}
const mainIndex = (pattern) => page.evaluate((src) => {
  const re = new RegExp(src, 'i');
  const i = [...document.querySelectorAll('.scr-main .menu-list > *')].findIndex((el) => re.test((el.textContent ?? '').trim()));
  if (i < 0) throw new Error(`item do menu principal não encontrado: ${src}`);
  return i;
}, pattern);
const car = () => page.evaluate(() => {
  const r = window.nc.session.race; if (!r) return null;
  const c = r.state.cars.find((x) => x.seat === 0);
  return { phase: r.state.phase, lap: c.lap, z: c.z, x: c.x, speed: c.speed, finished: c.finished, laps: c.lapTicks.slice(), len: r.track.length };
});
const ghostFrame = () => page.evaluate(() => {
  const s = window.nc.session; const r = s.race; if (!r) return null;
  const g = r.ghost?.frame(r.state);
  return g ? { pose: g.pose ? { z: Math.round(g.pose.z), x: +g.pose.x.toFixed(2) } : null, delta: g.delta, carId: g.carId } : null;
});
const store = () => page.evaluate(() => JSON.parse(localStorage.getItem('nitro-crew.ghosts') ?? 'null'));

/** Pilota pelo teclado: acelerador sempre; volante para voltar ao meio da pista. Passos de `chunk` ticks. */
async function drive(untilFn, chunk = 5, maxTicks = 30000) {
  let steer = null;
  await page.keyboard.down('ArrowUp');
  for (let n = 0; n < maxTicks; n += chunk) {
    const c = await car();
    if (!c || await untilFn(c)) break;
    const want = c.x > 0.25 ? 'ArrowLeft' : c.x < -0.25 ? 'ArrowRight' : null;
    if (want !== steer) { if (steer) await page.keyboard.up(steer); if (want) await page.keyboard.down(want); steer = want; }
    await page.evaluate((k) => window.nc.session.debugStep(k), chunk);
  }
  if (steer) await page.keyboard.up(steer);
  await page.keyboard.up('ArrowUp');
}

/** Fecha a volta por quadros de verdade (o debugStep limpa as mensagens do HUD): pilota por debugStep até perto da linha, depois quadro a quadro. */
async function closeLapByFrames(lapsBefore) {
  await drive((k) => k.laps.length > lapsBefore || (k.lap >= 1 && k.z > k.len - 2500));
  await page.keyboard.down('ArrowUp');
  for (let i = 0; i < 200 && (await car()).laps.length <= lapsBefore; i++) await frame(1000 / 15);
  await page.keyboard.up('ArrowUp');
}

// ───────────── Início: loja de fantasmas vazia, 2 voltas ─────────────
await page.addInitScript(() => { try { if (!sessionStorage.getItem('gh-init')) { localStorage.clear(); sessionStorage.setItem('gh-init', '1'); } } catch { /* */ } });
await page.goto(url, { waitUntil: 'networkidle' });
await page.evaluate(() => { const s = window.nc.session; s.settings.quality = 'low'; s.stop(); s.handleMenuEvent({ type: 'settingsChanged', settings: { ...s.settings, quickLaps: 2, quality: 'low' } }); });
await frame();
check(await menu() === 'title', 'abre no título');
await press('Enter');
check(await menu() === 'main', 'menu principal');
const tt = await mainIndex('contra|time trial');
for (let i = 0; i < tt; i++) await press('ArrowDown');
await press('Enter');
check(await menu() === 'lobby', 'Contra-relógio abre o lobby');
await frame();
for (let i = 0; i < 6 && await page.evaluate(() => window.nc.session.input.seatDevice(0)) !== 'kb1'; i++) await held('Enter');
const ready = () => page.evaluate(() => document.querySelector('#ui .btn-start')?.classList.contains('disabled') === false);
for (let i = 0; i < 6 && !(await ready()); i++) await held('Enter');
for (let i = 0; i < 20; i++) {
  if (await page.evaluate(() => document.querySelector('#ui .focus')?.classList.contains('btn-start') ?? false)) break;
  await held('ArrowUp');
}
await held('Enter');
check(await menu() === 'tracks', 'INICIAR abre as pistas');
const trackId = await page.evaluate(() => document.querySelector('#ui .focus')?.getAttribute('data-track'));
await press('Enter');
await frame();
let c = await car();
check(c && c.phase === 'countdown', `corrida de contra-relógio em ${trackId} (${c?.phase})`);
check((await ghostFrame()) === null || (await ghostFrame()).pose === null, 'sem fantasma na primeira vez');

// ───────────── Corrida 1: a volta 1 vira fantasma; a volta 2 corre contra ela ─────────────
await closeLapByFrames(0);
c = await car();
check(c.laps.length === 1, `volta 1 fechada em ${(c.laps[0] / 60).toFixed(2)} s`);
let st = await store();
check(st?.ghosts?.[trackId]?.ticks === c.laps[0], `fantasma gravado na chave própria (${st?.ghosts?.[trackId]?.ticks} ticks, ${JSON.stringify(st).length} caracteres)`);
const msgs = await page.evaluate(() => [...window.nc.session.race.messages.get(0)].map((m) => `${m.kind}:${m.text}`));
check(msgs.some((m) => /FANTASMA GRAVADO/.test(m)), `mensagem no HUD (${msgs.join(' | ')})`);
await page.evaluate(() => window.nc.session.debugStep(300));
const g1 = await ghostFrame();
check(g1?.pose !== null && typeof g1?.delta === 'number', `volta 2 com fantasma e diferença ao vivo (${JSON.stringify(g1)})`);
await frame();
await page.screenshot({ path: `${out}-01-volta2.png` });
await drive((k) => k.finished);
c = await car();
check(c.finished && c.laps.length === 2, `corrida acabou: voltas ${c.laps.map((t) => (t / 60).toFixed(2)).join(' / ')}`);
st = await store();
const best = Math.min(...c.laps);
check(st?.ghosts?.[trackId]?.ticks === best, `o fantasma é a melhor volta (${st?.ghosts?.[trackId]?.ticks} = ${best})`);
for (let i = 0; i < 30 && await menu() !== 'results'; i++) await frame(250);
check(await menu() === 'results', 'resultado');

// ───────────── Corrida 2: "Correr de novo"; a volta 1 (do grid) é mais lenta que o fantasma lançado ─────────────
check(await focusBy(/correr de novo|de novo|retry|again/i), `foco em correr de novo (${await focusText()})`);
await press('Enter');
await frame();
// Logo depois da linha o fantasma (volta lançada) abre alguns carros de vantagem: perto o bastante para a captura.
await drive(async (k) => { if (k.lap < 1 || k.phase !== 'racing') return false; const g = await ghostFrame(); return !!g?.pose && g.delta > 0.12; }, 2);
const g2 = await ghostFrame();
check(g2?.pose && g2.pose.z > (await car()).z && g2.delta > 0 && g2.pose.z - (await car()).z < 4000, `fantasma à frente e diferença positiva (${JSON.stringify(g2)})`);
await frame(); await frame();
const hud = await page.evaluate(() => { const e = document.querySelector('.nc-hud .ghost-delta'); return e ? { cls: e.className, text: e.textContent } : null; });
check(hud && /behind/.test(hud.cls) && /FANTASMA \+\d+,\d\d/.test(hud.text), `HUD: ${JSON.stringify(hud)}`);
await page.screenshot({ path: `${out}-02-fantasma-a-frente.png` });
const visible = await page.evaluate(() => {
  let n = 0; window.nc.session.renderer.__scene.traverse((o) => { if (o.isMesh && !o.isInstancedMesh && o.material?.opacity < 0.5 && o.material?.transparent && o.visible) n++; });
  return n;
});
check(visible >= 2, `carro fantasma na cena (${visible} malhas translúcidas visíveis)`);
// Fechamento da volta: mensagem com a diferença.
await closeLapByFrames(0);
const msgs2 = await page.evaluate(() => [...window.nc.session.race.messages.get(0)].map((m) => `${m.kind}:${m.text}`));
check(msgs2.some((m) => /FANTASMA [+−]\d+,\d\d/.test(m)), `mensagem do fechamento (${msgs2.join(' | ')})`);
await frame();
await page.screenshot({ path: `${out}-03-fechamento.png` });

// ───────────── Pausa → menu → Recordes ─────────────
await held('Escape');
check(await menu() === 'pause', 'Esc pausa');
check(await focusBy(/sair|menu principal|quit/i), `foco em sair (${await focusText()})`);
await press('Enter');
await frame();
check(await menu() === 'main', 'volta ao menu');
const rec = await mainIndex('recordes|records');
for (let i = 0; i < rec; i++) await press('ArrowDown');
await press('Enter');
check(await menu() === 'records', 'Recordes');
const row = await page.evaluate(() => { const r = document.querySelector('#ui .record-row.has-ghost'); return r ? r.textContent : null; });
check(row && /Fantasma/.test(row), `linha da pista com fantasma (${row})`);
await page.screenshot({ path: `${out}-04-recordes.png` });
// Enter na linha exporta (navegador: download).
check(await focusBy(/Fantasma/), `foco na pista com fantasma`);
const [download] = await Promise.all([page.waitForEvent('download'), press('Enter')]);
const file = await download.path().then((p) => import('node:fs').then((fs) => fs.readFileSync(p, 'utf8')));
const parsed = JSON.parse(file);
check(download.suggestedFilename() === `fantasma-${trackId}.nitro.json` && parsed.format === 'nitro-crew-ghost' && parsed.trackId === trackId, `exportou ${download.suggestedFilename()} (${file.length} bytes)`);
await page.waitForTimeout(200);
check(/exportado/.test(await page.evaluate(() => document.querySelector('#ui .ghost-status')?.textContent ?? '')), 'status: exportado');
// Importar: o arquivo do "amigo" (mesmo fantasma, outro nome) substitui o da pista.
const friend = JSON.stringify({ ...parsed, name: 'Amigo', date: '2026-09-20T10:00:00.000Z' });
check(await focusBy(/importar fantasma/i), `foco em importar (${await focusText()})`);
const [chooser] = await Promise.all([page.waitForEvent('filechooser'), press('Enter')]);
await chooser.setFiles({ name: 'amigo.nitro.json', mimeType: 'application/json', buffer: Buffer.from(friend) });
for (let i = 0; i < 20 && (await store())?.ghosts?.[trackId]?.name !== 'Amigo'; i++) await page.waitForTimeout(100);
check((await store())?.ghosts?.[trackId]?.name === 'Amigo', 'importado: o fantasma da pista é o do amigo');
await page.waitForTimeout(200);
const after = await page.evaluate(() => ({ status: document.querySelector('#ui .ghost-status')?.textContent ?? '', row: document.querySelector('#ui .record-row.has-ghost')?.textContent ?? '' }));
check(/importado/.test(after.status) && /Amigo/.test(after.row), `status e linha atualizados (${after.status})`);
await page.screenshot({ path: `${out}-05-importado.png` });
// Arquivo inválido: aviso, nada muda.
const [chooser2] = await Promise.all([page.waitForEvent('filechooser'), (async () => { await focusBy(/importar fantasma/i); await press('Enter'); })()]);
await chooser2.setFiles({ name: 'lixo.nitro.json', mimeType: 'application/json', buffer: Buffer.from('{"format":"nitro-crew-ghost","v":1,"data":"@@"}') });
await page.waitForTimeout(400);
check(/não é um fantasma/.test(await page.evaluate(() => document.querySelector('#ui .ghost-status')?.textContent ?? '')) && (await store())?.ghosts?.[trackId]?.name === 'Amigo', 'arquivo inválido: aviso e fantasma intacto');
await page.screenshot({ path: `${out}-06-invalido.png` });

// ───────────── Opções: desligar o fantasma ─────────────
await press('Escape');
await frame();
const opt = await mainIndex('opções|options');
if (await menu() !== 'main') await press('Escape');
for (let i = 0; i < opt; i++) await press('ArrowDown');
await press('Enter');
check(await menu() === 'options', 'Opções');
check(await focusBy(/fantasma/i, 'ArrowDown', 30), `item do fantasma (${await focusText()})`);
await press('ArrowRight');
check(await page.evaluate(() => window.nc.session.settings.ghost === false && JSON.parse(localStorage.getItem('nitro-crew.settings')).ghost === false), 'desligado e gravado');
await page.screenshot({ path: `${out}-07-opcoes.png` });
await page.evaluate(() => { const s = window.nc.session; s.debugBind(0, 'kb1'); s.startQuick(s.menus ? 'copacabana' : 'copacabana', 2, [{ seat: 0, name: 'P1', carId: 'falcao', teamId: 0, color: '#ffd23f' }], true); });
await drive((k) => k.lap >= 1 && k.z > 8000);
const off = await page.evaluate(() => { const r = window.nc.session.race; return r.ghost?.frame(r.state); });
check(off === undefined, 'com a opção desligada a RenderFrame não leva fantasma');

check(errors.length === 0, `sem erros no console (${errors.join(' | ')})`);
console.log(fails.length ? `FALHOU: ${fails.length}` : 'OK');
await browser.close();
process.exit(fails.length ? 1 : 0);
