import { hydrateFromDisk, installSaveMirror } from './game/cloudsave';
import { getDesktop } from './game/desktop';
import { createErrorReporter, GAME_VERSION, installGlobalHandlers, setActiveReporter, telemetryConsented } from './game/errors';
import { createSession, type Session } from './game/session';
import { showFatal } from './errors/fatal';
import { createErrorToast } from './errors/toast';

// API de depuração/playtest: window.nc.session, window.nc.startQuick(...)
declare global { interface Window { nc: { session: Session } } }

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

async function boot(): Promise<void> {
  // 2) No Electron, o save em arquivo (Steam Cloud) vale mais que o localStorage — antes de a sessão ler as opções.
  if (desktop && storage) {
    await hydrateFromDisk(desktop, storage);
    installSaveMirror(desktop, storage);
  }

  const canvas = document.getElementById('game') as HTMLCanvasElement | null;
  const hud = document.getElementById('hud');
  const ui = document.getElementById('ui');
  if (!canvas || !hud || !ui) throw new Error('index.html precisa de #game, #hud e #ui');

  const s = createSession(canvas, hud, ui);
  session = s;

  // O AudioContext só nasce depois de um gesto do usuário.
  const unlock = () => { s.audio.unlock(); window.removeEventListener('keydown', unlock); window.removeEventListener('pointerdown', unlock); };
  window.addEventListener('keydown', unlock);
  window.addEventListener('pointerdown', unlock);

  s.start();
  window.nc = { session: s };
}

// 3) Se o jogo nem começar (WebGL recusado, por exemplo), uma tela explica e oferece o relatório — nada de janela preta.
boot().catch((err: unknown) => {
  console.error(err);
  reporter.report(err, 'fatal');
  showFatal(document.body, err);
});
