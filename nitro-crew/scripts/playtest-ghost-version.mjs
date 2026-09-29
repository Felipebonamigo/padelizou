// Roteiro Playwright da versão do conteúdo no fantasma e nos recordes (onda E, fantasma-versao):
// 1) contra-relógio de 1 volta grava o fantasma com a impressão; 2) no seguinte ele aparece;
// 3) com a impressão trocada na loja ele NÃO aparece, a volta corre sem rival e grava um novo;
// 4) Recordes: fantasma de outra versão some da linha e o recorde de outra versão ganha "versão anterior";
// 5) Importar: arquivo de outra versão, v1 (sem impressão) e de outra pista dão aviso e não mudam a loja;
//    o arquivo desta versão entra.
// Uso: npx vite build && npx vite preview --port 4174 --strictPort &
//      node scripts/playtest-ghost-version.mjs http://localhost:4174/ scratch/pt-fv
// ⚠️ O menu lê o teclado por quadro e só então abre o seletor de arquivo; o navegador só deixa abrir até alguns segundos
//    depois da tecla. Com o 3D por software (~1 quadro/s) e a máquina carregada, o "filechooser" pode não vir (visto uma
//    vez depois de 7 playtests seguidos; sozinho passou). A 60 quadros/s isso não acontece — rode de novo antes de caçar defeito.
import { chromium } from 'playwright';
const url = process.argv[2] ?? 'http://localhost:4174/';
const out = process.argv[3] ?? 'scratch/pt-fv';
const TRACK = 'passo_alpino'; // a mais curta: a volta de 1 min custa menos no swiftshader
const exe = '/opt/pw-browsers/chromium-1194/chrome-linux/chrome';
const browser = await chromium.launch({ executablePath: exe, args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
page.setDefaultTimeout(240_000);
const errors = [];
page.on('pageerror', (e) => errors.push('pageerror: ' + e.message));
page.on('console', (m) => { if (m.type() === 'error') errors.push(`console: ${m.text()}`); });
const fails = [];
const t0 = Date.now();
const check = (cond, msg) => { console.log(`${cond ? '✓' : '✗'} [${Math.round((Date.now() - t0) / 1000)}s] ${msg}`); if (!cond) fails.push(msg); };

const frame = (dtMs = 1000 / 60) => page.evaluate((dt) => { window.__t = (window.__t ?? performance.now()) + dt; window.nc.session.frame(window.__t); }, dtMs);
async function press(code) { await page.keyboard.press(code); await page.waitForTimeout(60); }
const menu = () => page.evaluate(() => window.nc.session.menus.current());
const focusText = () => page.evaluate(() => (document.querySelector('#ui .focus')?.textContent ?? '').trim());
async function focusBy(re, key = 'ArrowDown', max = 40) {
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
  return { phase: r.state.phase, lap: c.lap, z: c.z, x: c.x, finished: c.finished, laps: c.lapTicks.slice(), len: r.track.length };
});
const ghostFrame = () => page.evaluate(() => {
  const r = window.nc.session.race; if (!r) return null;
  const g = r.ghost?.frame(r.state);
  return g ? { pose: g.pose ? { z: Math.round(g.pose.z) } : null, delta: g.delta } : null;
});
const translucent = () => page.evaluate(() => {
  let n = 0; window.nc.session.renderer.__scene.traverse((o) => { if (o.isMesh && !o.isInstancedMesh && o.material?.opacity < 0.5 && o.material?.transparent && o.visible) n++; });
  return n;
});
const store = () => page.evaluate(() => JSON.parse(localStorage.getItem('nitro-crew.ghosts') ?? 'null'));
const setGhostFp = (fp) => page.evaluate(([id, v]) => {
  const s = JSON.parse(localStorage.getItem('nitro-crew.ghosts'));
  s.ghosts[id].fp = v;
  localStorage.setItem('nitro-crew.ghosts', JSON.stringify(s));
}, [TRACK, fp]);
const hudMessages = () => page.evaluate(() => [...(window.nc.session.race?.messages.get(0) ?? [])].map((m) => m.text));

/** Acelerador sempre; volante para voltar ao meio da pista. Passos de `chunk` ticks pela API de depuração. */
async function drive(untilFn, chunk = 6, maxTicks = 30000) {
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
/** Fecha a volta por quadros de verdade no fim (o debugStep limpa as mensagens do HUD). */
async function closeLapByFrames() {
  await drive((k) => k.laps.length > 0 || (k.lap >= 1 && k.z > k.len - 2500));
  await page.keyboard.down('ArrowUp');
  for (let i = 0; i < 200 && (await car()).laps.length === 0; i++) await frame(1000 / 15);
  await page.keyboard.up('ArrowUp');
}
async function timeTrial() {
  await page.evaluate((id) => {
    const s = window.nc.session;
    s.debugBind(0, 'kb1');
    s.startQuick(id, 1, [{ seat: 0, name: 'P1', carId: 'falcao', teamId: 0, color: '#ffd23f' }], true);
  }, TRACK);
  await frame();
}
async function toResults() {
  for (let i = 0; i < 40 && await menu() !== 'results'; i++) await frame(250);
}

// ───────────── Início: loja vazia ─────────────
await page.addInitScript(() => { try { if (!sessionStorage.getItem('fv-init')) { localStorage.clear(); sessionStorage.setItem('fv-init', '1'); } } catch { /* */ } });
await page.goto(url, { waitUntil: 'networkidle' });
await page.evaluate(() => { const s = window.nc.session; s.settings.quality = 'low'; s.stop(); });
await frame();
check(await menu() === 'title', 'abre no título');

// ───────────── 1) Grava o fantasma ─────────────
await timeTrial();
check((await ghostFrame())?.pose == null, 'primeira vez: sem fantasma');
await closeLapByFrames();
let c = await car();
check(c.laps.length === 1, `volta fechada em ${(c.laps[0] / 60).toFixed(2)} s`);
let st = await store();
const fp0 = st?.ghosts?.[TRACK]?.fp;
check(st?.ghosts?.[TRACK]?.ticks === c.laps[0] && /^[0-9a-f]{8}$/.test(fp0 ?? ''), `fantasma gravado com a impressão (${fp0})`);
check((await hudMessages()).some((m) => /FANTASMA GRAVADO/.test(m)), `HUD: ${(await hudMessages()).join(' | ')}`);
await toResults();

// ───────────── 2) No contra-relógio seguinte ele aparece ─────────────
await timeTrial();
await drive(async (k) => { if (k.lap < 1 || k.phase !== 'racing') return false; const g = await ghostFrame(); return !!g?.pose && g.delta > 0.12; }, 2);
const g2 = await ghostFrame();
await frame(); await frame();
const n2 = await translucent();
check(g2?.pose != null && typeof g2.delta === 'number' && n2 >= 2, `fantasma aparece (pose z=${g2?.pose?.z}, diferença ${g2?.delta?.toFixed(2)}, ${n2} malhas translúcidas)`);
await page.screenshot({ path: `${out}-01-fantasma-aparece.png` });

// ───────────── 3) Impressão trocada: não aparece; a volta grava um novo ─────────────
await setGhostFp('deadbeef');
await timeTrial();
let seen = 0;
await drive(async (k) => {
  if (k.lap >= 1 && k.phase === 'racing') { const g = await ghostFrame(); if (g?.pose) seen++; }
  return k.lap >= 1 && k.z > 20000;
}, 6);
await frame(); await frame();
const n3 = await translucent();
const hud3 = await page.evaluate(() => { const e = document.querySelector('.nc-hud .ghost-delta'); return e ? { cls: e.className, text: e.textContent, shown: getComputedStyle(e).display !== 'none' && e.textContent !== '' } : null; });
check(seen === 0 && n3 === 0 && !(hud3?.shown), `impressão de outra versão: sem fantasma (${seen} quadros com pose, ${n3} malhas translúcidas, HUD ${JSON.stringify(hud3)})`);
await page.screenshot({ path: `${out}-02-outra-versao-sem-fantasma.png` });
await closeLapByFrames();
const msgs3 = await hudMessages();
check(msgs3.some((m) => /^FANTASMA GRAVADO$/.test(m)) && !msgs3.some((m) => /[+−]\d+,\d\d/.test(m)), `volta sem rival grava um fantasma novo (${msgs3.join(' | ')})`);
st = await store();
check(st?.ghosts?.[TRACK]?.fp === fp0, `a loja volta a ter a impressão desta versão (${st?.ghosts?.[TRACK]?.fp})`);
await toResults();

// ───────────── 4) Recordes: fantasma e recorde de outra versão ─────────────
const valid = (await store()).ghosts[TRACK];
await setGhostFp('deadbeef');
await page.evaluate((id) => {
  const s = JSON.parse(localStorage.getItem('nitro-crew.save'));
  s.bestLaps[id].fp = 'deadbeef';
  localStorage.setItem('nitro-crew.save', JSON.stringify(s));
}, TRACK);
await page.reload({ waitUntil: 'networkidle' });
await page.evaluate(() => { const s = window.nc.session; s.settings.quality = 'low'; s.stop(); });
await frame();
await press('Enter');
check(await menu() === 'main', 'menu principal depois de recarregar');
const rec = await mainIndex('recordes|records');
for (let i = 0; i < rec; i++) await press('ArrowDown');
await press('Enter');
check(await menu() === 'records', 'Recordes');
const row = () => page.evaluate((name) => {
  const r = [...document.querySelectorAll('#ui .record-row')].find((el) => el.textContent.includes(name));
  return r ? { text: r.textContent, ghost: r.classList.contains('has-ghost'), old: [...r.querySelectorAll('.record-old')].map((e) => ({ text: e.textContent, title: e.title })) } : null;
}, 'Passo Alpino');
const r4 = await row();
check(r4 && !r4.ghost && !/Fantasma/.test(r4.text), `fantasma de outra versão não aparece na linha (${r4?.text})`);
check(r4 && r4.old.length === 1 && r4.old[0].text === 'versão anterior' && /versão anterior/.test(r4.old[0].title), `recorde de outra versão: "versão anterior" (${JSON.stringify(r4?.old)})`);
const box = await page.evaluate(() => { const e = document.querySelector('#ui .record-old'); const r = e?.getBoundingClientRect(); const p = e?.closest('.record-row')?.getBoundingClientRect(); return r && p ? { inside: r.right <= p.right + 0.5 && r.left >= p.left - 0.5, h: Math.round(r.height), fs: getComputedStyle(e).fontSize } : null; });
check(box?.inside === true, `a marca cabe na linha (${JSON.stringify(box)})`);
await page.evaluate(() => document.querySelector('#ui .record-old')?.scrollIntoView({ block: 'center' }));
await page.screenshot({ path: `${out}-03-recordes-versao-anterior.png` });

// ───────────── 5) Importar ─────────────
const before = JSON.stringify(await store());
const status = () => page.evaluate(() => document.querySelector('#ui .ghost-status')?.textContent ?? '');
async function importFile(name, text) {
  check(await focusBy(/importar fantasma/i), `foco em importar (${await focusText()})`);
  const [chooser] = await Promise.all([page.waitForEvent('filechooser'), press('Enter')]);
  await chooser.setFiles({ name, mimeType: 'application/json', buffer: Buffer.from(text) });
  await page.waitForTimeout(500);
  return status();
}
const file = (o) => JSON.stringify({ format: 'nitro-crew-ghost', ...o });
const { savedAt: _s, fp: _fp, ...plain } = valid;
let msg = await importFile('outra-versao.nitro.json', file({ v: 2, ...plain, fp: 'deadbeef' }));
check(/outra versão do jogo/.test(msg) && JSON.stringify(await store()) === before, `outra versão: aviso e loja igual (${msg})`);
msg = await importFile('antigo-v1.nitro.json', file({ v: 1, ...plain }));
check(/outra versão do jogo/.test(msg) && JSON.stringify(await store()) === before, `arquivo v1 (sem impressão): aviso e loja igual (${msg})`);
await page.screenshot({ path: `${out}-04-importar-outra-versao.png` });
msg = await importFile('outra-pista.nitro.json', file({ v: 2, ...plain, trackId: 'monaco_noite', fp: valid.fp }));
check(/não cabe nesta pista/.test(msg) && JSON.stringify(await store()) === before, `volta de ${TRACK} com o id de monaco_noite: aviso e loja igual (${msg})`);
msg = await importFile('valido.nitro.json', file({ v: 2, ...plain, name: 'Amigo', fp: valid.fp }));
for (let i = 0; i < 20 && (await store())?.ghosts?.[TRACK]?.name !== 'Amigo'; i++) await page.waitForTimeout(100);
await page.waitForTimeout(300);
const r5 = await row();
check((await store())?.ghosts?.[TRACK]?.name === 'Amigo' && r5?.ghost && /Amigo/.test(r5.text) && /importado/.test(await status()), `arquivo desta versão entra (${await status()})`);
await page.screenshot({ path: `${out}-05-importado.png` });

check(errors.length === 0, `sem erros no console (${errors.join(' | ')})`);
console.log(fails.length ? `FALHOU: ${fails.length}` : 'OK');
await browser.close();
process.exit(fails.length ? 1 : 0);
