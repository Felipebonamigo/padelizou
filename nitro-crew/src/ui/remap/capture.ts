// Captura da tecla/botão novo de uma célula da tela de controles. Puro: a tela entrega as teclas
// apertadas, os botões segurados de cada gamepad e o tempo; aqui se decide aceitar, recusar,
// esperar ou cancelar. Esc (teclado) e Start (gamepad) cancelam; sem nada em 5 s, cancela sozinho.
import { isBindableButton, isBindableKey, RESERVED_BUTTON, RESERVED_KEY, type BindAction, type BindCode, type BindDevice } from './bindings';

export const CAPTURE_SECONDS = 5;

export interface Capture {
  action: BindAction;
  device: BindDevice;
  /** Segundos restantes. */
  left: number;
  /**
   * Botões que cada gamepad já segurava. Só contam depois de soltos e apertados de novo — senão o
   * A que abriu a captura viraria a tecla nova no quadro seguinte.
   */
  held: Record<string, number[]>;
}

export type CaptureOutcome =
  | { kind: 'wait' }
  | { kind: 'cancel'; reason: 'escape' | 'start' | 'timeout' }
  | { kind: 'reject'; code: BindCode }
  | { kind: 'accept'; code: BindCode };

const WAIT: CaptureOutcome = { kind: 'wait' };

export function startCapture(action: BindAction, device: BindDevice, held: Readonly<Record<string, readonly number[]>> = {}): Capture {
  const copy: Record<string, number[]> = {};
  for (const [pad, buttons] of Object.entries(held)) copy[pad] = [...buttons];
  return { action, device, left: CAPTURE_SECONDS, held: copy };
}

/** Tecla apertada durante a captura (a tela já descartou a repetição automática do teclado). */
export function captureKey(c: Capture, code: string): CaptureOutcome {
  if (code === RESERVED_KEY) return { kind: 'cancel', reason: 'escape' };
  if (c.device === 'gamepad') return WAIT;
  return isBindableKey(code, c.action) ? { kind: 'accept', code } : { kind: 'reject', code };
}

/** Botões segurados agora por um gamepad (`pad` = id do dispositivo). Atualiza `c.held`. */
export function captureButtons(c: Capture, pad: string, pressed: readonly number[]): CaptureOutcome {
  const before = c.held[pad] ?? [];
  const fresh = pressed.filter((b) => !before.includes(b));
  c.held[pad] = [...pressed];
  if (fresh.includes(RESERVED_BUTTON)) return { kind: 'cancel', reason: 'start' };
  if (c.device !== 'gamepad' || fresh.length === 0) return WAIT;
  const code = fresh[0];
  return isBindableButton(code, c.action) ? { kind: 'accept', code } : { kind: 'reject', code };
}

/** Passa o tempo; ao zerar, cancela. */
export function captureTick(c: Capture, dt: number): CaptureOutcome {
  c.left = Math.max(0, c.left - Math.max(0, dt));
  return c.left <= 0 ? { kind: 'cancel', reason: 'timeout' } : WAIT;
}
