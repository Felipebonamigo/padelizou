// E2E do pacote Electron de verdade (release/linux-unpacked/nitro-crew) com o Playwright da raiz do jogo.
// Uso, dentro de desktop/ e depois de `npm run dist:linux`:
//   xvfb-run -a node e2e.mjs [prefixo-das-capturas]      (sem Xvfb, numa máquina com tela: node e2e.mjs)
// userData isolado numa pasta temporária (XDG_CONFIG_HOME) — nunca toca o ~/.config de verdade.
// Confere: app/index.html de dentro do asar, preload, pasta "Nitro Crew", erro → aviso + log em arquivo,
// "Copiar relatório de erros" → área de transferência, telemetria → saves/*.json, arquivo trocado "pela nuvem"
// vencendo um localStorage DIFERENTE na volta (conflito de verdade), o mesmo erro na 2ª abertura voltando ao log,
// e a tela de erro fatal quando o WebGL não existe — com a opção num <code> e operada por um controle simulado.
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
// Mesma função nas duas aberturas: mesma pilha, então é o MESMO erro (a 2ª abertura tem que voltar ao log).
const throwOnPurpose = () => page.evaluate(() => { setTimeout(() => { throw new Error('erro E2E de propósito'); }, 0); });
await throwOnPurpose();
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
const consent = existsSync(settingsFile) ? JSON.parse(readFileSync(settingsFile, 'utf8')).telemetryConsent : undefined;
check(consent === 1, `telemetria ligada gravada em saves/nitro-crew.settings.json com a versão dos termos (${consent})`);
await shot('04-telemetry');
// As opções passaram pelo espelho: localStorage e arquivo iguais. O save de progresso fica só no localStorage,
// diferente do que a "nuvem" vai pôr no disco.
const localSettingsRun1 = await page.evaluate(() => localStorage.getItem('nitro-crew.settings'));
check(localSettingsRun1 === readFileSync(settingsFile, 'utf8'), 'opções: localStorage e arquivo iguais depois da gravação');
await page.evaluate(() => { localStorage.setItem('nitro-crew.save', JSON.stringify({ racesRun: 1 })); });

const logFile = join(userData, 'logs', 'errors.log');
check(existsSync(logFile) && readFileSync(logFile, 'utf8').includes('erro E2E de propósito'), 'erro gravado em logs/errors.log');
console.log('arquivos no userData:', readdirSync(userData).join(', '), '| saves:', readdirSync(join(userData, 'saves')).join(', '));
check(pageErrors.every((e) => e.includes('erro E2E de propósito')), `nenhum erro além do provocado (${pageErrors.length})`);
await app.close();

// ───────────── "Outro computador": a nuvem troca os arquivos em disco antes da 2ª execução ─────────────
// Conflito de verdade: o localStorage TEM as duas chaves, com valores diferentes dos que a nuvem trouxe.
writeFileSync(join(userData, 'saves', 'nitro-crew.save.json'), JSON.stringify({ racesRun: 42, racesWon: 7, cupsCompleted: ['brasil'] }));
const cloudSettings = { ...JSON.parse(readFileSync(settingsFile, 'utf8')), language: 'en', quickLaps: 5 };
writeFileSync(settingsFile, JSON.stringify(cloudSettings));
({ app, page, pageErrors } = await launch());
const local = await page.evaluate(() => JSON.parse(localStorage.getItem('nitro-crew.save') ?? '{}'));
check(local.racesRun === 42 && local.cupsCompleted?.[0] === 'brasil', `save do disco venceu o localStorage diferente (racesRun 1 → ${local.racesRun})`);
const s2 = await page.evaluate(() => ({ language: window.nc.session.settings.language, quickLaps: window.nc.session.settings.quickLaps, consent: window.nc.session.settings.telemetryConsent }));
check(s2.language === 'en' && s2.quickLaps === 5, `opções do disco venceram as do localStorage (idioma ${s2.language}, voltas ${s2.quickLaps})`);
check(s2.consent === 1, 'consentimento da telemetria sobreviveu ao reinício');
const kept = await page.evaluate(() => JSON.parse(localStorage.getItem('nitro-crew.errors') ?? '[]').length);
check(kept === 1, `anel de erros sobreviveu ao reinício (${kept})`);
// O mesmo erro de novo: continua uma entrada (x2), e volta ao log nesta abertura.
await throwOnPurpose();
await page.waitForTimeout(700);
const ring2 = await page.evaluate(() => JSON.parse(localStorage.getItem('nitro-crew.errors') ?? '[]'));
check(ring2.length === 1 && ring2[0].count === 2, `erro repetido em outra abertura: uma entrada, contador 2 no localStorage (${ring2.map((e) => `x${e.count}`).join(',')})`);
const purposeLines = readFileSync(logFile, 'utf8').split('\n').filter((l) => l.includes('erro E2E de propósito'));
check(purposeLines.length === 2, `log recebeu o erro nas duas aberturas (${purposeLines.length} linhas)`);
pageErrors.splice(0, pageErrors.length, ...pageErrors.filter((e) => !e.includes('erro E2E de propósito')));
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
  // Um controle "standard" falso: o teste aperta botões mudando window.__pad.
  window.__pad = { id: 'E2E pad', index: 0, connected: true, mapping: 'standard', timestamp: 0, axes: [0, 0, 0, 0], buttons: Array.from({ length: 17 }, () => ({ pressed: false, touched: false, value: 0 })) };
  Object.defineProperty(Navigator.prototype, 'getGamepads', { configurable: true, value: () => [window.__pad, null, null, null] });
});
await page.reload();
await page.waitForSelector('.nc-fatal', { timeout: 60000 });
const fatalText = await page.evaluate(() => document.querySelector('.nc-fatal p')?.textContent ?? '');
check(fatalText.includes('WebGL'), `tela fatal explica a falha de WebGL (${fatalText.slice(0, 60)}…)`);
const flagCode = await page.evaluate(() => document.querySelector('.nc-fatal p code')?.textContent ?? null);
check(flagCode === '--ignore-gpu-blocklist', `a opção de inicialização vai inteira num <code> (${flagCode})`);
await page.click('.nc-fatal .btn-primary');
await page.waitForTimeout(500);
const fatalClip = await app.evaluate(({ clipboard }) => clipboard.readText());
check(fatalClip.includes('fatal') && fatalClip.includes('stage: boot'), 'botão da tela fatal copia o relatório');

// Só com controle: direcional anda entre os botões, A aperta o que está em foco.
const tap = async (button) => {
  await page.evaluate((b) => { window.__pad.buttons[b] = { pressed: true, touched: true, value: 1 }; }, button);
  await page.waitForTimeout(400);
  await page.evaluate((b) => { window.__pad.buttons[b] = { pressed: false, touched: false, value: 0 }; }, button);
  await page.waitForTimeout(400);
};
const focusedButton = () => page.evaluate(() => (document.activeElement?.classList.contains('btn-primary') ? 'copy' : document.activeElement?.closest?.('.nc-fatal-actions') ? 'retry' : 'none'));
await tap(15);
check(await focusedButton() === 'retry', 'controle: → leva o foco para "Tentar de novo"');
await tap(14);
check(await focusedButton() === 'copy', 'controle: ← volta para "Copiar relatório"');
await app.evaluate(({ clipboard }) => clipboard.writeText('vazio'));
await tap(0);
const padClip = await app.evaluate(({ clipboard }) => clipboard.readText());
check(padClip.includes('stage: boot'), 'controle: A no botão em foco copia o relatório');
check(readFileSync(logFile, 'utf8').includes(' fatal '), 'erro fatal gravado em logs/errors.log');
await shot('06-fatal-webgl');
await app.close();

console.log(fails.length ? `FALHOU: ${fails.length}` : 'electron e2e OK');
process.exit(fails.length ? 1 : 0);
