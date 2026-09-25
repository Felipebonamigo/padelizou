// Nomes de teclas, botões, ações e colunas para a interface. `KeyboardEvent.code` é a posição
// física no layout americano; com o mapa do layout do sistema (`navigator.keyboard.getLayoutMap()`,
// no Chromium/Electron) a letra mostrada é a da tecla de verdade — AZERTY, e no ABNT2 a tecla
// `Semicolon` aparece como Ç.
import { t } from '../../i18n';
import type { BindAction, BindCode, BindDevice } from './bindings';
import './strings';

export type LayoutMap = ReadonlyMap<string, string>;
export type PadStyle = 'xbox' | 'playstation';

const ARROWS: Readonly<Record<string, string>> = { ArrowUp: '↑', ArrowDown: '↓', ArrowLeft: '←', ArrowRight: '→' };
const PUNCTUATION: Readonly<Record<string, string>> = {
  Minus: '-', Equal: '=', BracketLeft: '[', BracketRight: ']', Backslash: '\\', Semicolon: ';', Quote: "'",
  Comma: ',', Period: '.', Slash: '/', Backquote: '`', IntlBackslash: '\\', IntlRo: '/',
};
const NUMPAD_OPS: Readonly<Record<string, string>> = {
  NumpadAdd: '+', NumpadSubtract: '−', NumpadMultiply: '×', NumpadDivide: '÷', NumpadDecimal: ',', NumpadEnter: 'Enter',
};
const NAMED: Readonly<Record<string, string>> = {
  Enter: 'Enter', Escape: 'Esc', Tab: 'Tab', Backspace: '⌫', Home: 'Home', End: 'End', PageUp: 'PgUp', PageDown: 'PgDn',
  Insert: 'Ins', Delete: 'Del', ControlLeft: 'Ctrl', ControlRight: 'Ctrl', AltLeft: 'Alt', AltRight: 'AltGr',
  MetaLeft: 'Meta', MetaRight: 'Meta', CapsLock: 'Caps Lock', ContextMenu: 'Menu',
};
/** Teclas cujo símbolo depende do layout (letras, números, pontuação). */
const LAYOUT_DEPENDENT = /^(Key[A-Z]|Digit[0-9]|Minus|Equal|Bracket(Left|Right)|Backslash|Semicolon|Quote|Comma|Period|Slash|Backquote|IntlBackslash|IntlRo)$/;

export function keyLabel(code: string, layout?: LayoutMap | null): string {
  if (LAYOUT_DEPENDENT.test(code)) {
    const printed = layout?.get(code);
    if (printed && printed.trim().length === 1) return printed.toUpperCase();
  }
  let m = /^Key([A-Z])$/.exec(code);
  if (m) return m[1];
  m = /^Digit([0-9])$/.exec(code);
  if (m) return m[1];
  m = /^Numpad([0-9])$/.exec(code);
  if (m) return t('remap.key.numpad', { key: m[1] });
  if (code in NUMPAD_OPS) return t('remap.key.numpad', { key: NUMPAD_OPS[code] });
  if (code in ARROWS) return ARROWS[code];
  if (code in PUNCTUATION) return PUNCTUATION[code];
  if (code === 'Space') return t('remap.key.space');
  if (code === 'ShiftLeft') return t('remap.key.shiftLeft');
  if (code === 'ShiftRight') return t('remap.key.shiftRight');
  return NAMED[code] ?? code;
}

const XBOX_BUTTONS: readonly string[] = ['A', 'B', 'X', 'Y', 'LB', 'RB', 'LT', 'RT', 'Back', 'Start', 'LS', 'RS', 'D-pad ↑', 'D-pad ↓', 'D-pad ←', 'D-pad →', 'Home'];
const PLAYSTATION_BUTTONS: readonly string[] = ['✕', '○', '□', '△', 'L1', 'R1', 'L2', 'R2', 'Share', 'Options', 'L3', 'R3', 'D-pad ↑', 'D-pad ↓', 'D-pad ←', 'D-pad →', 'PS'];

export function buttonLabel(index: number, style: PadStyle = 'xbox'): string {
  const names = style === 'playstation' ? PLAYSTATION_BUTTONS : XBOX_BUTTONS;
  return names[index] ?? t('remap.button.n', { n: index });
}

/**
 * Estilo dos nomes de botão pelo nome do controle ("DualSense Wireless Controller", "Xbox Wireless
 * Controller", vendor 054c = Sony). O Xbox é testado antes: "Wireless Controller" existe nos dois.
 */
export function padStyleOf(name: string): PadStyle {
  if (/xbox|xinput|045e/i.test(name)) return 'xbox';
  if (/dualsense|dualshock|playstation|sony|054c|ps[345]\b|^wireless controller/i.test(name)) return 'playstation';
  return 'xbox';
}

export function codeLabel(code: BindCode, style: PadStyle = 'xbox', layout?: LayoutMap | null): string {
  return typeof code === 'number' ? buttonLabel(code, style) : keyLabel(code, layout);
}

export function codesLabel(codes: readonly BindCode[], style: PadStyle = 'xbox', layout?: LayoutMap | null): string {
  return codes.map((c) => codeLabel(c, style, layout)).join(' / ');
}

export function actionLabel(action: BindAction): string {
  return t(`remap.action.${action}`);
}

/** Nome curto da coluna ("Teclado 1", "Teclado 2", "Controles"), o mesmo dos avisos de conflito. */
export function deviceLabel(device: BindDevice): string {
  return t(`remap.device.${device}`);
}

/** Dica da coluna: o nome completo do dispositivo e o que continua fixo nele. */
export function deviceTitle(device: BindDevice): string {
  return device === 'gamepad' ? t('remap.gamepadNote') : t(`remap.deviceTitle.${device}`, { name: t(`ui.device.${device}`) });
}
