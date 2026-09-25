// Tela de erro fatal: o jogo não chegou a começar (o caso real é o WebGL recusado por driver ou GPU na lista de
// bloqueio do Chromium). Em vez de uma janela preta, explica o que houve e oferece copiar o relatório e tentar
// de novo. DOM puro, sem os menus — eles podem ser justamente o que falhou.
// Controle: sem sessão não há quem leia a Gamepad API, então a tela lê sozinha (createFatalPadNav): direcional
// ou analógico anda entre os dois botões, A ou Start aperta — quem joga no sofá ou no Steam Deck também sai dela.
import { copyToClipboard, currentReportText, describeError } from '../game/errors';
import { loadSettings } from '../game/settings';
import { setLanguage, t } from '../i18n';
import { closeEdges, mapGamepad, MAX_GAMEPADS, NEUTRAL_RAW, type DeviceRaw } from '../ui/input';
import './errors.css';
import './strings';

export function isWebGlFailure(message: string): boolean {
  return /webgl/i.test(message);
}

/** Opção de inicialização citada no texto de WebGL (o Electron repassa as opções do Chromium). */
export const LAUNCH_FLAG = '--ignore-gpu-blocklist';

/** Separa a opção do resto do texto, para ela ir num `<code>` que não quebra linha no meio do "--". */
export function splitLaunchFlag(text: string): { before: string; flag: string; after: string } | null {
  const i = text.indexOf(LAUNCH_FLAG);
  if (i < 0) return null;
  return { before: text.slice(0, i), flag: LAUNCH_FLAG, after: text.slice(i + LAUNCH_FLAG.length) };
}

export type FatalPadAction = 'prev' | 'next' | 'activate';

/** O que um controle pede à tela neste quadro: ← ↑ anterior, → ↓ próximo, A ou Start aperta. Segurar não repete. */
export function fatalPadAction(prev: DeviceRaw, now: DeviceRaw): FatalPadAction | null {
  const e = closeEdges(prev, now);
  if (e.confirm || e.start) return 'activate';
  if (e.left || e.up) return 'prev';
  if (e.right || e.down) return 'next';
  return null;
}

/** O pedaço de um `Gamepad` que a tela lê (o de verdade, ou um falso nos testes). */
export interface PadLike {
  connected: boolean;
  buttons: ReadonlyArray<{ pressed: boolean; value: number }>;
  axes: readonly number[];
}

type Focusable = Pick<HTMLElement, 'focus' | 'click' | 'addEventListener'>;

/**
 * Navegação por controle entre `buttons`: `poll()` uma vez por quadro. O botão em foco acompanha também o
 * mouse e o Tab (evento `focus`), para o controle continuar de onde o jogador está.
 */
export function createFatalPadNav(buttons: readonly Focusable[], readPads: () => ReadonlyArray<PadLike | null>): { poll(): void } {
  let index = 0;
  buttons.forEach((b, i) => b.addEventListener('focus', () => { index = i; }));
  const prev: DeviceRaw[] = [];
  return {
    poll() {
      if (buttons.length === 0) return;
      let pads: ReadonlyArray<PadLike | null>;
      try { pads = readPads(); } catch { return; }
      for (let i = 0; i < Math.min(pads.length, MAX_GAMEPADS); i++) {
        const pad = pads[i];
        const raw = pad && pad.connected ? mapGamepad(pad.buttons.map((b) => b.pressed || b.value > 0.5), pad.axes) : NEUTRAL_RAW;
        const action = fatalPadAction(prev[i] ?? NEUTRAL_RAW, raw);
        prev[i] = raw;
        if (action === 'activate') buttons[index].click();
        else if (action) buttons[(index + (action === 'next' ? 1 : buttons.length - 1)) % buttons.length].focus();
      }
    },
  };
}

function readGamepads(): ReadonlyArray<PadLike | null> {
  if (typeof navigator === 'undefined' || typeof navigator.getGamepads !== 'function') return [];
  return Array.from(navigator.getGamepads());
}

export function showFatal(parent: HTMLElement, err: unknown): HTMLElement {
  setLanguage(loadSettings().language);
  const { message } = describeError(err);
  const root = document.createElement('div');
  root.className = 'nc-fatal';
  root.setAttribute('role', 'alertdialog');
  const panel = document.createElement('div');
  panel.className = 'nc-fatal-panel';
  const title = document.createElement('h1');
  title.textContent = t('errors.fatal.title');
  const text = document.createElement('p');
  const explanation = isWebGlFailure(message) ? t('errors.fatal.webgl') : t('errors.fatal.generic');
  const parts = splitLaunchFlag(explanation);
  if (parts) {
    const code = document.createElement('code');
    code.textContent = parts.flag;
    text.append(parts.before, code, parts.after);
  } else {
    text.textContent = explanation;
  }
  const detail = document.createElement('pre');
  detail.textContent = message;
  const actions = document.createElement('div');
  actions.className = 'nc-fatal-actions';
  const copy = document.createElement('button');
  copy.type = 'button';
  copy.className = 'btn btn-primary';
  copy.textContent = t('errors.fatal.copy');
  copy.addEventListener('click', () => {
    void copyToClipboard(currentReportText({ stage: 'boot' })).then((ok) => {
      copy.textContent = ok ? t('errors.options.copied') : t('errors.options.copyFailed');
      copy.classList.toggle('ok', ok);
    });
  });
  const retry = document.createElement('button');
  retry.type = 'button';
  retry.className = 'btn';
  retry.textContent = t('errors.fatal.retry');
  retry.addEventListener('click', () => location.reload());
  actions.append(copy, retry);
  panel.append(title, text, detail, actions);
  root.appendChild(panel);
  parent.appendChild(root);
  copy.focus();
  const pad = createFatalPadNav([copy, retry], readGamepads);
  const tick = () => {
    if (!root.isConnected) return;
    pad.poll();
    requestAnimationFrame(tick);
  };
  requestAnimationFrame(tick);
  return root;
}
