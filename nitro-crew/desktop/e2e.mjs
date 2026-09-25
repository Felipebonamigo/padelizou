// E2E do pacote Electron de verdade (release/linux-unpacked/nitro-crew) com o Playwright da raiz do jogo.
// Uso, dentro de desktop/ e depois de `npm run dist:linux`:
//   xvfb-run -a node e2e.mjs [prefixo-das-capturas]      (sem Xvfb, numa máquina com tela: node e2e.mjs)
// userData isolado numa pasta temporária (XDG_CONFIG_HOME) — nunca toca o ~/.config de verdade.
// Confere: app/index.html de dentro do asar, preload, pasta "Nitro Crew", erro → aviso + log em arquivo,
// "Copiar relatório de erros" → área de transferência, telemetria → saves/*.json, save da nuvem vencendo o
// localStorage na volta, e a tela de erro fatal quando o WebGL não existe.
import { _electron as electron } from 'playwright';
import { mkdtempSync, readFileSync, writeFileSync, existsSync, readdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const shots = process.argv[2] ?? join(here, 'release', 'e2e');
const exe = join(here, 'release', 'linux-unpacked', 'nitro-crew');
const xdg = mkdtempSync(join(tmpdir(), 'nc-xdg-'));
const userData = join(xdg, 'Nitro Crew');
const fails = [];
const check = (cond, msg) => { console.log(`${cond ? '✓' : '✗'} ${msg}`); if (!cond) fails.push(msg); };
const shot = (name) => page.screenshot({ path: `${shots}-${name}.png`, timeout: 120000 });
const args = ['--no-sandbox', '--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'];

async function launch(extraArgs = []) {
  const app = await electron.launch({ executablePath: exe, args: [...args, ...extraArgs], env: { ...process.env, XDG_CONFIG_HOME: xdg, ELECTRON_ENABLE_LOGGING: '1' }, timeout: 60000 });
  const page = await app.firstWindow();
  const pageErrors = [];
  page.on('pageerror', (e) => pageErrors.push(e.message));
  page.on('console', (m) => { if (m.type() === 'error') pageErrors.push(`console: ${m.text()}`); });
  await page.waitForFunction(() => window.nc && window.nc.session, null, { timeout: 60000 });
  await page.evaluate(() => { window.nc.session.settings.quality = 'low'; });
  return { app, page, pageErrors };
}

// ───────────── 1ª execução ─────────────
let { app, page, pageErrors } = await launch();
const ud = await app.evaluate(({ app: a }) => a.getPath('userData'));
check(ud === userData, `userData = <XDG>/Nitro Crew (${ud})`);
check(await page.evaluate(() => typeof window.desktop?.storeReadAll === 'function'), 'preload expõe storeReadAll');
const info = await page.evaluate(() => ({ href: location.href, menu: window.nc.session.menus.current() }));
check(/app\.asar\/app\/index\.html$/.test(info.href), `carregou app/index.html de dentro do asar (${info.href.split('/').slice(-3).join('/')})`);
check(info.menu === 'title', `abre na tela de título (${info.menu})`);
await page.waitForTimeout(1500);
await shot('01-title');

// Primeira gravação: a sessão grava o save ao terminar corrida; aqui, as opções pelo fluxo real de teclado.
await page.keyboard.press('Enter'); await page.waitForTimeout(400);
check(await page.evaluate(() => window.nc.session.menus.current()) === 'main', 'Enter → menu principal');
for (let i = 0; i < 4; i++) { await page.keyboard.press('ArrowDown'); await page.waitForTimeout(120); }
await page.keyboard.press('Enter'); await page.waitForTimeout(500);
check(await page.evaluate(() => window.nc.session.menus.current()) === 'options', 'Opções abre pelo teclado');

// Um erro de verdade, fora de qualquer try: window.onerror → anel, localStorage, log em arquivo e aviso no canto.
await page.evaluate(() => { setTimeout(() => { throw new Error('erro E2E de propósito'); }, 0); });
await page.waitForTimeout(700);
const toastShown = await page.evaluate(() => document.querySelector('.nc-error-toast')?.classList.contains('show') ?? false);
check(toastShown, 'aviso discreto aparece no canto');
await shot('02-toast-options');

// Desce até "Copiar relatório de erros" (rodapé, depois das colunas Geral e Corrida) e copia com Enter.
const focusedItem = () => page.evaluate(() => document.querySelector('.options-footer .focus, .focus[data-item]')?.getAttribute('data-item') ?? null);
for (let i = 0; i < 30 && (await focusedItem()) !== 'error-report'; i++) { await page.keyboard.press('ArrowDown'); await page.waitForTimeout(80); }
const focused = await focusedItem();
check(focused === 'error-report', `foco no item do relatório (${focused})`);
await page.keyboard.press('Enter'); await page.waitForTimeout(600);
const valueText = await page.evaluate(() => document.querySelector('[data-item="error-report"] .sel-value')?.textContent);
check(valueText === 'Copiado ✓', `retorno visual da cópia (${valueText})`);
const clip = await app.evaluate(({ clipboard }) => clipboard.readText());
check(clip.includes('Nitro Crew — error report') && clip.includes('erro E2E de propósito') && clip.includes('desktop: yes'), 'área de transferência tem o relatório com o erro');
check(!/\/tmp\/|nc-xdg|\/home\/[a-z]/.test(clip), 'relatório sem caminho do computador');
await shot('03-copied');

// Telemetria: desce um, liga, e a opção vai para o arquivo de save (Steam Cloud).
for (let i = 0; i < 3 && (await focusedItem()) !== 'telemetry'; i++) { await page.keyboard.press('ArrowDown'); await page.waitForTimeout(100); }
check((await focusedItem()) === 'telemetry', 'foco na telemetria');
await page.keyboard.press('Enter'); await page.waitForTimeout(600);
const settingsFile = join(userData, 'saves', 'nitro-crew.settings.json');
check(existsSync(settingsFile) && JSON.parse(readFileSync(settingsFile, 'utf8')).telemetry === true, 'telemetria ligada gravada em saves/nitro-crew.settings.json');
await shot('04-telemetry');

const logFile = join(userData, 'logs', 'errors.log');
check(existsSync(logFile) && readFileSync(logFile, 'utf8').includes('erro E2E de propósito'), 'erro gravado em logs/errors.log');
console.log('arquivos no userData:', readdirSync(userData).join(', '), '| saves:', readdirSync(join(userData, 'saves')).join(', '));
check(pageErrors.every((e) => e.includes('erro E2E de propósito')), `nenhum erro além do provocado (${pageErrors.length})`);
await app.close();

// ───────────── "Outro computador": a nuvem troca o save em disco antes da 2ª execução ─────────────
writeFileSync(join(userData, 'saves', 'nitro-crew.save.json'), JSON.stringify({ racesRun: 42, racesWon: 7, cupsCompleted: ['brasil'] }));
({ app, page, pageErrors } = await launch());
const local = await page.evaluate(() => JSON.parse(localStorage.getItem('nitro-crew.save') ?? '{}'));
check(local.racesRun === 42 && local.cupsCompleted?.[0] === 'brasil', `save do disco venceu o localStorage na inicialização (racesRun ${local.racesRun})`);
const telemetry = await page.evaluate(() => window.nc.session.settings.telemetry);
check(telemetry === true, 'opção de telemetria sobreviveu ao reinício');
const kept = await page.evaluate(() => JSON.parse(localStorage.getItem('nitro-crew.errors') ?? '[]').length);
check(kept === 1, `anel de erros sobreviveu ao reinício (${kept})`);
// A copa Brasil concluída destrava a seguinte: o progresso da nuvem chegou à sessão.
await page.evaluate(() => window.nc.session.menus.show('cups'));
await page.waitForTimeout(600);
await shot('05-cups-from-cloud');
check(pageErrors.length === 0, `2ª execução sem erro de página (${pageErrors.join(' | ')})`);
await app.close();

// ───────────── Sem WebGL (driver recusado): tela de erro fatal em vez de janela preta ─────────────
// Um getContext('webgl*') que devolve null reproduz, em qualquer máquina, o que acontece com a GPU na lista de
// bloqueio do Chromium (visto de verdade neste pacote sem --ignore-gpu-blocklist: "WebGL2 blocklisted").
app = await electron.launch({ executablePath: exe, args, env: { ...process.env, XDG_CONFIG_HOME: xdg }, timeout: 60000 });
page = await app.firstWindow();
await app.context().addInitScript(() => {
  const original = HTMLCanvasElement.prototype.getContext;
  HTMLCanvasElement.prototype.getContext = function getContext(type, ...rest) {
    return /webgl/i.test(String(type)) ? null : original.call(this, type, ...rest);
  };
});
await page.reload();
await page.waitForSelector('.nc-fatal', { timeout: 60000 });
const fatalText = await page.evaluate(() => document.querySelector('.nc-fatal p')?.textContent ?? '');
check(fatalText.includes('WebGL'), `tela fatal explica a falha de WebGL (${fatalText.slice(0, 60)}…)`);
await page.click('.nc-fatal .btn-primary');
await page.waitForTimeout(500);
const fatalClip = await app.evaluate(({ clipboard }) => clipboard.readText());
check(fatalClip.includes('fatal') && fatalClip.includes('stage: boot'), 'botão da tela fatal copia o relatório');
check(readFileSync(logFile, 'utf8').includes(' fatal '), 'erro fatal gravado em logs/errors.log');
await shot('06-fatal-webgl');
await app.close();

console.log(fails.length ? `FALHOU: ${fails.length}` : 'electron e2e OK');
process.exit(fails.length ? 1 : 0);
