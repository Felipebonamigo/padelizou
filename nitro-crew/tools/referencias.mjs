// Quadro de referências da direção de arte (docs/DIRECAO-DE-ARTE.md §3): as 7 câmeras (5 de pista × 3 períodos, a garagem
// e a escolha de carro) e as 3 folhas de carros, com saturação e contraste medidos só como ALERTA. Com o vite no ar:
//   node tools/referencias.mjs captura <porta> <pasta> <rótulo> [grupos=pistas,menus,carros]
//   node tools/referencias.mjs montar <pasta> <rótulo-antes> <rótulo-depois> <prefixo-saída>
// `captura` grava <pasta>/<rótulo>-<câmera>-<período>.png (15), <rótulo>-garagem.png, <rótulo>-escolha.png (2),
// <rótulo>-carros-{frente34,tras34,lado}.png (3) e <rótulo>-medidas.json. `montar` junta antes × depois com tools/tile.mjs
// (uma grade por grupo: dia, entardecer, noite, menus, carros), pulando o grupo a que faltar arquivo.
// Rode sob a trava da vaga de captura (docs/ondas/K.md §2.4) e em segundo plano: no swiftshader, ~15–25 min.
// Sai com 1 se alguma captura falhou (tempo-limite, erro de página); alerta de cor NUNCA dá exit 1.
import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { chromium } from 'playwright';

// As 5 câmeras de pista da bíblia (docs/ondas/K.md §3.3, item 2; segmentos conferidos com `npx tsx tools/landmark-sight.ts <pista>`).
// O orquestrador edita aqui se a bíblia trocar uma câmera.
const CAMERAS = [
  { id: 'copa', track: 'copacabana', seg: 70 },       // a entrada da fatia da L; não segue o Cristo
  { id: 'foz', track: 'foz_do_iguacu', seg: 105 },    // Cataratas @182
  { id: 'sampa', track: 'sampa_noite', seg: 970 },    // MASP @1029 (na largada a captura sai preta)
  { id: 'deserto', track: 'xingo', seg: 110 },        // cânion @158
  { id: 'neve', track: 'passo_alpino', seg: 180 },    // Matterhorn @234
];
const PERIODS = [['dia', 'day'], ['entardecer', 'dusk'], ['noite', 'night']];
const SHEETS = [['carros-frente34', 'front34'], ['carros-tras34', 'rear34'], ['carros-lado', 'side']];
const ALERTA = { satMin: 0.25, satMax: 0.80, contrasteMin: 0.12 }; // provisório: só avisa, nunca falha
const CHROME = '/opt/pw-browsers/chromium-1194/chrome-linux/chrome';
const IGNORE = /GL Driver|404/; // o mesmo filtro de tools/render-harness.mjs

const [mode, ...args] = process.argv.slice(2);

if (mode === 'captura') await captura(args);
else if (mode === 'montar') montar(args);
else { console.log('uso: node tools/referencias.mjs captura <porta> <pasta> <rótulo> [grupos=pistas,menus,carros]\n     node tools/referencias.mjs montar <pasta> <rótulo-antes> <rótulo-depois> <prefixo-saída>'); process.exit(1); }

async function captura([port, dir, label, groupsArg = 'pistas,menus,carros']) {
  if (!port || !dir || !label) { console.log('captura: faltam <porta> <pasta> <rótulo>'); process.exit(1); }
  const groups = groupsArg.replace(/^grupos=/, '').split(',');
  mkdirSync(dir, { recursive: true });
  const base = `http://localhost:${port}`;
  const browser = await chromium.launch({ executablePath: CHROME, args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader'] });
  const files = []; const failed = [];

  /** Abre uma página, deixa `body` rodar e fotografa; erro de página ou tempo-limite vira falha registrada, não parada. */
  async function shoot(file, viewport, body) {
    const ctx = await browser.newContext({ viewport });
    const page = await ctx.newPage();
    page.setDefaultTimeout(240000);
    const errs = [];
    page.on('pageerror', (e) => errs.push(`pageerror: ${e.message}`));
    page.on('console', (m) => { if (m.type() === 'error' && !IGNORE.test(m.text())) errs.push(`console: ${m.text()}`); });
    let ok = false;
    try {
      ok = await body(page);
      await page.screenshot({ path: file });
    } catch (e) { errs.push(String(e.message ?? e)); ok = false; }
    await ctx.close();
    if (ok && errs.length === 0) { files.push(file); console.log('ok', file); }
    else { failed.push(`${file}: ${ok ? '' : 'não ficou pronto; '}${errs.slice(0, 2).join(' | ')}`); console.log('FALHOU', file, errs.slice(0, 2).join(' | ')); if (ok) files.push(file); }
  }

  if (groups.includes('pistas')) {
    for (const cam of CAMERAS) for (const [name, tod] of PERIODS) {
      await shoot(`${dir}/${label}-${cam.id}-${name}.png`, { width: 1280, height: 720 }, async (page) => {
        await page.goto(`${base}/tools/render-harness.html?track=${cam.track}&seg=${cam.seg}&hud=0&tod=${tod}`);
        const ready = await page.waitForFunction(() => window.ready, null, { timeout: 240000 }).then(() => true, () => false);
        await page.waitForTimeout(2500);
        return ready;
      });
    }
  }

  if (groups.includes('menus')) {
    // ?nosurvey=1: o questionário da K2 não pode aparecer na captura (inofensivo antes dela existir).
    await shoot(`${dir}/${label}-garagem.png`, { width: 1280, height: 720 }, async (page) => {
      await page.goto(`${base}/?nosurvey=1`);
      await page.waitForFunction(() => window.nc?.session);
      // A receita da tela garage-2 (scripts/playtest-layout.mjs): 2 pilotos e a seta para a esquerda.
      await page.evaluate(() => {
        const s = window.nc.session;
        for (let i = 0; i < 4; i++) s.input.unbindSeat(i);
        ['kb1', 'kb2'].forEach((d, i) => s.input.bindSeat(i, d));
        const hs = [0, 1].map((i) => ({ seat: i, name: `Piloto ${i + 1}`, carId: ['falcao', 'tornado'][i], teamId: 0, color: '#fff' }));
        s.handleMenuEvent({ type: 'startCareer', humans: hs, resume: false });
        document.dispatchEvent(new KeyboardEvent('keydown', { code: 'ArrowLeft', bubbles: true }));
      });
      const ok = await page.waitForFunction(() => window.nc.session.menus.current() === 'garage', null, { timeout: 60000 }).then(() => true, () => false);
      await page.waitForTimeout(1500);
      return ok;
    });
    await shoot(`${dir}/${label}-escolha.png`, { width: 1280, height: 720 }, async (page) => {
      await page.goto(`${base}/?nosurvey=1`);
      await page.waitForFunction(() => window.nc?.session);
      // A receita da tela lobby-1: um controle no assento 0 e o lobby aberto. Não aperta Enter (a K1 muda esse fluxo).
      await page.evaluate(() => {
        const s = window.nc.session;
        for (let i = 0; i < 4; i++) s.input.unbindSeat(i);
        s.input.bindSeat(0, 'kb1'); s.menus.show('lobby');
      });
      const ok = await page.evaluate(() => window.nc.session.menus.current() === 'lobby' && window.nc.session.input.seatDevice(0) === 'kb1');
      await page.waitForTimeout(1500);
      return ok;
    });
  }

  if (groups.includes('carros')) {
    for (const [name, view] of SHEETS) {
      await shoot(`${dir}/${label}-${name}.png`, { width: 1920, height: 1080 }, async (page) => {
        await page.goto(`${base}/tools/render-harness.html?track=copacabana&sheet=${view}&frames=3`);
        const ready = await page.waitForFunction(() => window.ready, null, { timeout: 240000 }).then(() => true, () => false);
        await page.waitForTimeout(2500);
        return ready;
      });
    }
  }

  // Medidas (só dos PNG desta captura): luminância média, saturação média (HSV) e contraste (desvio da luminância).
  const medidas = {};
  const mctx = await browser.newContext({ viewport: { width: 400, height: 300 } });
  const mpage = await mctx.newPage();
  for (const file of files) {
    const m = await mpage.evaluate(async (b64) => {
      const img = new Image(); img.src = `data:image/png;base64,${b64}`; await img.decode();
      const c = document.createElement('canvas'); c.width = img.width; c.height = img.height;
      const ctx = c.getContext('2d'); ctx.drawImage(img, 0, 0);
      const { data } = ctx.getImageData(0, 0, c.width, c.height);
      let n = 0, sy = 0, syy = 0, ss = 0;
      for (let i = 0; i < data.length; i += 16) { // 1 pixel a cada 4, no índice linear
        const r = data[i] / 255, g = data[i + 1] / 255, b = data[i + 2] / 255;
        const y = 0.2126 * r + 0.7152 * g + 0.0722 * b; const mx = Math.max(r, g, b), mn = Math.min(r, g, b);
        ss += mx === 0 ? 0 : (mx - mn) / mx; sy += y; syy += y * y; n++;
      }
      const lum = sy / n; return { lum, sat: ss / n, contraste: Math.sqrt(Math.max(0, syy / n - lum * lum)) };
    }, readFileSync(file).toString('base64'));
    const alertas = [];
    if (m.sat < ALERTA.satMin) alertas.push('saturação baixa');
    if (m.sat > ALERTA.satMax) alertas.push('saturação alta');
    if (m.contraste < ALERTA.contrasteMin) alertas.push('contraste baixo');
    const r3 = (x) => Math.round(x * 1000) / 1000;
    medidas[file.split('/').pop()] = { lum: r3(m.lum), sat: r3(m.sat), contraste: r3(m.contraste), alertas };
    console.log(`${file.split('/').pop().padEnd(34)} lum ${m.lum.toFixed(3)} sat ${m.sat.toFixed(3)} contraste ${m.contraste.toFixed(3)}${alertas.length ? `  ⚠ alerta: ${alertas.join(', ')}` : ''}`);
  }
  await mctx.close();
  await browser.close();
  writeFileSync(`${dir}/${label}-medidas.json`, JSON.stringify(medidas, null, 2));

  // O período tem de se ver: a noite da Copacabana mais escura que o dia (prova do `tod=`).
  const dia = medidas[`${label}-copa-dia.png`]; const noite = medidas[`${label}-copa-noite.png`];
  if (dia && noite) console.log(noite.lum < dia.lum ? `tod: lum(copa-noite)=${noite.lum.toFixed(3)} < lum(copa-dia)=${dia.lum.toFixed(3)}` : `tod: lum(copa-noite)=${noite.lum.toFixed(3)} ≥ lum(copa-dia)=${dia.lum.toFixed(3)} ⚠ o tod não escureceu`);
  else console.log('tod: pulado (sem captura de pista)');

  console.log(`${failed.length ? 'FALHOU' : 'ok'} ${files.length} capturas em ${dir}/ (${failed.length} falhas)`);
  for (const f of failed) console.log(`  falha: ${f}`);
  process.exit(failed.length ? 1 : 0);
}

function montar([dir, antes, depois, prefixo]) {
  if (!dir || !antes || !depois || !prefixo) { console.log('montar: faltam <pasta> <rótulo-antes> <rótulo-depois> <prefixo-saída>'); process.exit(1); }
  const par = (nome) => [`${dir}/${antes}-${nome}.png`, `${dir}/${depois}-${nome}.png`];
  const grupos = [
    ...PERIODS.map(([p]) => [p, CAMERAS.flatMap((c) => par(`${c.id}-${p}`))]),
    ['menus', [...par('garagem'), ...par('escolha')]],
    ['carros', SHEETS.flatMap(([n]) => par(n))],
  ];
  for (const [nome, arquivos] of grupos) {
    const falta = arquivos.find((f) => !existsSync(f));
    if (falta) { console.log(`aviso: grupo ${nome} pulado (falta ${falta})`); continue; }
    execFileSync(process.execPath, ['tools/tile.mjs', `${prefixo}-${nome}.png`, '2', ...arquivos], { stdio: 'inherit' });
  }
}
