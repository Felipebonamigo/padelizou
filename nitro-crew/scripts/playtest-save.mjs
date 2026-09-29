// Playtest do save com o localStorage cheio, no Chromium de verdade (cota real da origem) — docs/SAVE.md.
// A) navegador sem arquivo: a corrida não grava → aviso no resultado (uma vez, em PT); opção que não coube → aviso de
//    novo no menu (em EN); liberado o espaço, a corrida seguinte grava tudo, inclusive a que não tinha cabido.
// B) "Electron" (window.desktop falso, arquivo na memória do Node): nenhum aviso, o arquivo recebe, e a reabertura
//    com o localStorage ainda cheio parte do arquivo. Leva alguns minutos (3D por software).
// Exige `npm run preview` (porta 4174). Uso: node scripts/playtest-save.mjs [url] [prefixo-das-capturas]
import { chromium } from 'playwright';
const url = process.argv[2] ?? 'http://localhost:4174/';
const out = process.argv[3] ?? 'scratch/pt-save';
const exe = '/opt/pw-browsers/chromium-1194/chrome-linux/chrome';
const browser = await chromium.launch({ executablePath: exe, args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
const fails = [];
const check = (cond, msg) => { console.log(`${cond ? '✓' : '✗'} ${msg}`); if (!cond) fails.push(msg); };
const humans = [{ seat: 0, name: 'P1', carId: 'falcao', teamId: 0, color: '#ffd23f' }];

async function open(page) {
  await page.goto(url, { waitUntil: 'networkidle' });
  await page.waitForFunction(() => window.nc && window.nc.session, null, { timeout: 120000 });
  await page.evaluate(() => { window.nc.session.settings.quality = 'low'; });
}
/** Enche o localStorage da origem com "outro site" até a cota; devolve o tamanho e o nome do erro da próxima gravação. */
const fill = (page) => page.evaluate(() => {
  const key = 'outro-site';
  localStorage.removeItem(key);
  let lo = 0; let hi = 12 * 1024 * 1024;
  while (lo < hi) { const mid = Math.ceil((lo + hi) / 2); try { localStorage.setItem(key, 'x'.repeat(mid)); lo = mid; } catch { hi = mid - 1; } }
  localStorage.setItem(key, 'x'.repeat(lo));
  let name = 'nenhum';
  try { localStorage.setItem('sonda', 'x'.repeat(64)); localStorage.removeItem('sonda'); } catch (e) { name = e.name; }
  return { chars: lo, name };
});
const toast = (page) => page.evaluate(() => {
  const el = document.querySelector('.nc-save-toast');
  return { shown: !!el && el.classList.contains('show'), text: el ? el.textContent : null };
});
/** O 3D por software prende a página (~1 quadro/s): espera o aviso aparecer em vez de um tempo fixo. */
async function waitToast(page) {
  let t = await toast(page);
  for (let i = 0; i < 40 && !t.shown; i++) { await page.waitForTimeout(500); t = await toast(page); }
  return t;
}
const menu = (page) => page.evaluate(() => window.nc.session.menus.current());
async function race(page) {
  await page.evaluate((h) => { const s = window.nc.session; for (let i = 0; i < 4; i++) s.input.unbindSeat(i); s.debugBind(0, 'kb1'); s.startQuick('copacabana', 1, h); }, humans);
  await page.keyboard.down('ArrowUp');
  for (let i = 0; i < 40; i++) { const f = await page.evaluate(() => { window.nc.session.debugStep(600); return window.nc.session.race?.state.phase; }); if (f === 'finished') break; }
  await page.keyboard.up('ArrowUp');
  for (let i = 0; i < 40; i++) { await page.waitForTimeout(500); if (await menu(page) === 'results') break; }
  await page.waitForTimeout(1200); // o aviso é conferido a cada 0,5 s
}

// ───────────── A) navegador ─────────────
{
  const ctx = await browser.newContext({ viewport: { width: 1280, height: 720 } });
  const page = await ctx.newPage();
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await open(page);
  const f = await fill(page);
  check(f.name === 'QuotaExceededError', `localStorage cheio: ${f.chars} caracteres; a gravação seguinte lança ${f.name}`);
  await page.keyboard.press('Enter'); await page.waitForTimeout(1200);
  check((await menu(page)) === 'main' && !(await toast(page)).shown, 'menu principal sem aviso (nada foi perdido ainda)');
  await race(page);
  let t = await waitToast(page);
  check((await menu(page)) === 'results' && t.shown, `fim de corrida sem onde gravar: aviso no resultado ("${t.text}")`);
  check(t.text?.includes('O progresso não foi salvo'), 'texto em PT');
  check(await page.evaluate(() => localStorage.getItem('nitro-crew.save') === null), 'o localStorage não tem o save (cota)');
  await page.screenshot({ path: `${out}-A1-resultado.png` });
  await page.waitForTimeout(10000);
  check(!(await toast(page)).shown, 'o aviso some sozinho e não volta a cada quadro no resultado');
  await page.evaluate(() => window.nc.session.handleMenuEvent({ type: 'toMain' })); await page.waitForTimeout(1500);
  check((await menu(page)) === 'main' && !(await toast(page)).shown, 'de volta ao menu principal: não repete o mesmo aviso');
  // Opção mudada (idioma) também não coube: novo aviso no menu principal, agora em inglês.
  await page.evaluate(() => { const s = window.nc.session; s.handleMenuEvent({ type: 'settingsChanged', settings: { ...s.settings, language: 'en' } }); });
  t = await waitToast(page);
  check(t.shown && t.text?.includes('Your progress was not saved'), `opção que não coube: aviso de novo, em EN ("${t.text}")`);
  await page.screenshot({ path: `${out}-A2-menu-en.png` });
  // O jogador libera espaço: a próxima corrida grava tudo — inclusive a anterior, que ficou na memória.
  await page.evaluate(() => localStorage.removeItem('outro-site'));
  await page.waitForTimeout(9500);
  await race(page);
  const saved = await page.evaluate(() => JSON.parse(localStorage.getItem('nitro-crew.save') ?? 'null'));
  check(saved?.racesRun === 2 && saved?.stats?.totals?.races === 2, `espaço liberado: o save grava as duas corridas (racesRun ${saved?.racesRun})`);
  check(!(await toast(page)).shown, 'e sem aviso');
  check(errors.length === 0, `sem erro de página (${errors.join(' | ')})`);
  await ctx.close();
}

// ───────────── B) Electron (arquivo falso no Node) ─────────────
{
  const files = {};
  const ctx = await browser.newContext({ viewport: { width: 1280, height: 720 } });
  await ctx.exposeFunction('__storeReadAll', () => ({ ...files }));
  await ctx.exposeFunction('__storeWrite', (key, json) => { files[key] = json; return true; });
  await ctx.addInitScript(() => {
    const ok = () => Promise.resolve(true);
    window.desktop = {
      toggleFullscreen: () => Promise.resolve(), setFullscreen: () => Promise.resolve(), isFullscreen: () => Promise.resolve(false),
      quit: () => Promise.resolve(), steamName: () => Promise.resolve(null), achievement: ok, richPresence: () => Promise.resolve(),
      saveFile: ok, openFile: () => Promise.resolve(null), storeReadAll: () => window.__storeReadAll(),
      storeWrite: (k, j) => window.__storeWrite(k, j), logAppend: ok, copyText: ok, onFullscreen: () => undefined,
    };
  });
  const page = await ctx.newPage();
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await open(page);
  const f = await fill(page);
  check(f.name === 'QuotaExceededError', `[Electron] localStorage cheio (${f.chars} caracteres)`);
  await page.keyboard.press('Enter'); await page.waitForTimeout(800);
  await race(page);
  check(!(await toast(page)).shown, '[Electron] fim de corrida com o arquivo recebendo: sem aviso');
  check(await page.evaluate(() => localStorage.getItem('nitro-crew.save') === null), '[Electron] o localStorage recusou o save');
  const run1 = JSON.parse(files['nitro-crew.save'] ?? 'null')?.racesRun;
  check(run1 === 1, `[Electron] o arquivo tem a corrida (racesRun ${run1})`);
  await page.screenshot({ path: `${out}-B1-resultado-electron.png` });
  // Reabertura com o localStorage ainda cheio: a sessão parte do arquivo (a corrida seguinte soma em cima dele).
  await open(page);
  await page.keyboard.press('Enter'); await page.waitForTimeout(800);
  await race(page);
  const run2 = JSON.parse(files['nitro-crew.save'] ?? 'null')?.racesRun;
  check(run2 === 2, `[Electron] reabertura leu o arquivo: a 2ª corrida soma em cima (racesRun ${run2})`);
  check(!(await toast(page)).shown, '[Electron] e sem aviso');
  check(errors.length === 0, `[Electron] sem erro de página (${errors.join(' | ')})`);
  await ctx.close();
}

await browser.close();
console.log(fails.length ? `\n${fails.length} FALHA(S)` : '\nTUDO OK');
process.exit(fails.length ? 1 : 0);
