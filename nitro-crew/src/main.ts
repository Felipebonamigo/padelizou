import { hydrateFromDisk, installSaveMirror } from './game/cloudsave';
import { getDesktop } from './game/desktop';
import { createErrorReporter, dropStoredErrors, GAME_VERSION, installGlobalHandlers, setActiveReporter, telemetryConsented } from './game/errors';
import { dropOldestGhost } from './game/ghost-store';
import { createSaveNotice } from './game/save-notice';
import { createSession, type Session } from './game/session';
import { loadCarAssets, type CarAssetReport } from './render/cars/assets';
import { loadLandmarkAssets, loadLandmarkParts, type LandmarkAssetReport } from './render/scenery/landmarks/assets';
import { createPlaylog, getActivePlaylog, setActivePlaylog } from './game/playlog';
import { readJson, saveHealth, setSpaceFreers, writeJson } from './game/storage';
import { showFatal } from './errors/fatal';
import { createErrorToast, createSaveToast } from './errors/toast';

// API de depuração/playtest: window.nc.session, window.nc.startQuick(...)
declare global { interface Window { nc: { session: Session; carAssets?: CarAssetReport; landmarkAssets?: LandmarkAssetReport; landmarkParts?: LandmarkAssetReport } } }

function localStore(): Storage | null {
  try { return typeof localStorage === 'undefined' ? null : localStorage; } catch { return null; }
}

// 1) Relatório de erros antes de tudo: o que falhar daqui em diante fica registrado (src/game/errors.ts).
let session: Session | null = null;
const desktop = getDesktop();
const storage = localStore();
const toast = createErrorToast(document.body);
const reporter = createErrorReporter({
  version: GAME_VERSION,
  now: () => new Date(),
  storage,
  logAppend: desktop ? (text) => desktop.logAppend(text) : null,
  context: () => ({ mode: session?.race?.mode ?? null, track: session?.race?.track.def.id ?? null, screen: session?.menus.current() ?? null }),
  telemetryEnabled: () => telemetryConsented(session?.settings.telemetryConsent ?? 0),
  send: (url, body) => { navigator.sendBeacon(url, body); },
  // A tela de erro fatal já explica o que houve; o aviso do canto é para erro com o jogo rodando.
  onNew: (entry, total) => { if (entry.kind !== 'fatal') toast.show(total); },
  schedule: (fn, ms) => { setTimeout(fn, ms); },
});
setActiveReporter(reporter);
installGlobalHandlers(window, reporter);
// O que só estava na memória (contador de erro repetido, rajada) vai para o localStorage antes de a página fechar.
window.addEventListener('pagehide', () => reporter.flush());
// localStorage cheio e sem o arquivo do Electron: antes de perder uma gravação, descarta o log de erros e depois os
// fantasmas mais antigos — nunca save, carreira, estatísticas ou opções (src/game/storage.ts, docs/SAVE.md).
setSpaceFreers([(key) => dropStoredErrors(storage, key), dropOldestGhost]);

async function boot(): Promise<void> {
  // 2) No Electron, o save em arquivo (Steam Cloud) vale mais que o localStorage — antes de a sessão ler as opções.
  if (desktop && storage) {
    await hydrateFromDisk(desktop, storage);
    installSaveMirror(desktop, storage);
  }

  // Diário de jogo (playlog.ts); ?nosurvey=1 desliga o questionário (playtests que encadeiam corridas).
  setActivePlaylog(createPlaylog({ now: () => new Date(), read: readJson, write: writeJson, version: GAME_VERSION, survey: !new URLSearchParams(location.search).has('nosurvey') }));

  // 3) Carros da arte (src/assets/cars/*.glb) antes do renderizador: o recusado fica procedural, com o motivo no console.
  const carAssets = await loadCarAssets();
  for (const r of carAssets.rejected) console.warn(`[carros] ${r.file} recusado: ${r.problems.join('; ')}`);
  for (const w of carAssets.warnings) console.warn(`[carros] ${w}`);
  // Peças baixadas (src/assets/landmarks/parts/<peça>.glb: o bicho, a estátua), idem: entram nos marcos procedurais
  // que as usam (landmarks/parts.ts) no lugar do bicho procedural.
  const landmarkParts = await loadLandmarkParts();
  for (const r of landmarkParts.rejected) console.warn(`[peças] ${r.file} recusada: ${r.problems.join('; ')}`);
  for (const w of landmarkParts.warnings) console.warn(`[peças] ${w}`);
  // Marcos da arte (src/assets/landmarks/<id>.glb), idem: substituem a geometria do marco procedural do mesmo id.
  const landmarkAssets = await loadLandmarkAssets();
  for (const r of landmarkAssets.rejected) console.warn(`[marcos] ${r.file} recusado: ${r.problems.join('; ')}`);
  for (const w of landmarkAssets.warnings) console.warn(`[marcos] ${w}`);

  const canvas = document.getElementById('game') as HTMLCanvasElement | null;
  const hud = document.getElementById('hud');
  const ui = document.getElementById('ui');
  if (!canvas || !hud || !ui) throw new Error('index.html precisa de #game, #hud e #ui');

  // Banco de prova (?bench=1; no Electron, --bench vira ?bench=1&uncapped=1…): mede e para — sem sessão, sem menus.
  // Import dinâmico: o código do bench fica num pedaço à parte do pacote (src/bench/run.ts, docs/DESEMPENHO.md §4).
  if (new URLSearchParams(location.search).get('bench') === '1') {
    const { runBench } = await import('./bench/run');
    await runBench(location.search, { canvas, hud, ui, desktop });
    return;
  }

  const s = createSession(canvas, hud, ui);
  session = s;

  // O AudioContext só nasce depois de um gesto do usuário.
  const unlock = () => { s.audio.unlock(); window.removeEventListener('keydown', unlock); window.removeEventListener('pointerdown', unlock); };
  window.addEventListener('keydown', unlock);
  window.addEventListener('pointerdown', unlock);

  getActivePlaylog()?.sessionStart();
  s.start();
  window.nc = { session: s, carAssets, landmarkAssets, landmarkParts };

  // Gravação que não ficou em lugar nenhum: aviso no canto, no menu principal ou no resultado (save-notice.ts).
  const saveToast = createSaveToast(document.body);
  const saveNotice = createSaveNotice(() => saveToast.show(), saveHealth);
  setInterval(() => saveNotice.update(s.menus.current()), 500);
}

// 4) Se o jogo nem começar (WebGL recusado, por exemplo), uma tela explica e oferece o relatório — nada de janela preta.
boot().catch((err: unknown) => {
  console.error(err);
  reporter.report(err, 'fatal');
  showFatal(document.body, err);
});
