// Remapeamento dos controles: que tecla (teclados kb1/kb2) ou botão "standard" (gamepads) dispara
// cada ação de pilotagem. Puro e sem DOM: saneamento do que vem do localStorage, troca em caso de
// conflito e restaurar padrão. A navegação dos menus (setas, Enter, Esc; d-pad, A, B, Start) NÃO
// passa por aqui: é fixa em src/ui/input.ts e src/ui/menus.ts, para ninguém se trancar fora.

export type BindAction = 'throttle' | 'brake' | 'left' | 'right' | 'nitro' | 'gearUp' | 'gearDown' | 'pause';
export const BIND_ACTIONS: readonly BindAction[] = ['throttle', 'brake', 'left', 'right', 'nitro', 'gearUp', 'gearDown', 'pause'];

/** Colunas da tela de controles: os dois teclados e um mapeamento único para todos os gamepads. */
export type BindDevice = 'kb1' | 'kb2' | 'gamepad';
export const BIND_DEVICES: readonly BindDevice[] = ['kb1', 'kb2', 'gamepad'];

/** Códigos físicos (`KeyboardEvent.code`) por ação. */
export type KeyBindings = Record<BindAction, string[]>;
/** Índices de botão do mapeamento "standard" da Gamepad API por ação. */
export type PadBindings = Record<BindAction, number[]>;
export interface ControlBindings { kb1: KeyBindings; kb2: KeyBindings; gamepad: PadBindings }

export type BindCode = string | number;

/** Máximo de códigos por ação (o padrão do gamepad usa três: X / B / LT freiam). A captura sempre grava um só. */
export const MAX_CODES_PER_ACTION = 3;

/**
 * Esc (teclado) e Start (gamepad) sempre pausam e navegam os menus. Só a ação "pausa" os aceita
 * (é o padrão dela), e a captura os usa para cancelar — então nunca vão parar em outra ação.
 */
export const RESERVED_KEY = 'Escape';
export const RESERVED_BUTTON = 9;
/** Botões aceitos: 0..15 do mapeamento "standard" (16 é Home/Guide, que o sistema usa). */
export const MAX_BUTTON = 15;

// Teclas aceitas: letras, números, numérico, setas, espaço, Enter, Shift e pontuação. Ctrl, Alt e
// Meta ficam de fora (a entrada ignora eventos com eles, por causa dos atalhos do navegador), assim
// como F1–F12 (F5 recarrega, F11 tela cheia, F12 ferramentas) e Caps Lock (alterna).
const KEY_PATTERN = new RegExp('^(' + [
  'Key[A-Z]', 'Digit[0-9]', 'Numpad[0-9]', 'Numpad(Add|Subtract|Multiply|Divide|Decimal|Enter)',
  'Arrow(Up|Down|Left|Right)', 'Space', 'Enter', 'Backspace', 'Tab', 'Shift(Left|Right)',
  'Minus', 'Equal', 'Bracket(Left|Right)', 'Backslash', 'Semicolon', 'Quote', 'Comma', 'Period', 'Slash',
  'Backquote', 'IntlBackslash', 'IntlRo', 'Home', 'End', 'PageUp', 'PageDown', 'Insert', 'Delete',
].join('|') + ')$');

export function isBindableKey(code: unknown, action: BindAction): code is string {
  if (typeof code !== 'string') return false;
  if (code === RESERVED_KEY) return action === 'pause';
  return KEY_PATTERN.test(code);
}

export function isBindableButton(index: unknown, action: BindAction): index is number {
  if (typeof index !== 'number' || !Number.isInteger(index) || index < 0 || index > MAX_BUTTON) return false;
  return index !== RESERVED_BUTTON || action === 'pause';
}

// ───────────────────────────── Padrões ─────────────────────────────

function deepFreeze<T>(value: T): T {
  if (typeof value === 'object' && value !== null) {
    for (const v of Object.values(value)) deepFreeze(v);
    Object.freeze(value);
  }
  return value;
}

/** O mapeamento de antes do remapeamento (setas + Espaço + M/N; WASD + F + E/Q; A/RT, X/B/LT, RB, Y/LB). */
export const DEFAULT_BINDINGS: ControlBindings = deepFreeze({
  kb1: {
    throttle: ['ArrowUp'], brake: ['ArrowDown'], left: ['ArrowLeft'], right: ['ArrowRight'],
    nitro: ['Space'], gearUp: ['KeyM'], gearDown: ['KeyN'], pause: ['Escape'],
  },
  kb2: {
    throttle: ['KeyW'], brake: ['KeyS'], left: ['KeyA'], right: ['KeyD'],
    nitro: ['KeyF'], gearUp: ['KeyE'], gearDown: ['KeyQ'], pause: ['Escape'],
  },
  gamepad: {
    throttle: [0, 7], brake: [2, 1, 6], left: [14], right: [15],
    nitro: [5], gearUp: [3], gearDown: [4], pause: [9],
  },
});

/** Reserva para quando uma ação fica sem código e o padrão dela já está com outra (só com save adulterado). */
const SPARE_KEYS: readonly string[] = [
  'KeyZ', 'KeyX', 'KeyC', 'KeyV', 'KeyB', 'KeyG', 'KeyH', 'KeyJ', 'KeyK', 'KeyL', 'KeyI', 'KeyO', 'KeyP', 'KeyU',
  'Digit1', 'Digit2', 'Digit3', 'Digit4', 'Digit5', 'Digit6', 'Digit7', 'Digit8', 'Digit9', 'Digit0',
];
const SPARE_BUTTONS: readonly number[] = Array.from({ length: MAX_BUTTON + 1 }, (_, i) => i);

function cloneMap<C extends BindCode>(map: Readonly<Record<BindAction, readonly C[]>>): Record<BindAction, C[]> {
  const out = {} as Record<BindAction, C[]>;
  for (const a of BIND_ACTIONS) out[a] = [...map[a]];
  return out;
}

export function cloneBindings(b: ControlBindings): ControlBindings {
  return { kb1: cloneMap(b.kb1), kb2: cloneMap(b.kb2), gamepad: cloneMap(b.gamepad) };
}

/** Cópia nova e mutável do padrão. */
export function defaultBindings(): ControlBindings {
  return cloneBindings(DEFAULT_BINDINGS);
}

// ───────────────────────────── Saneamento ─────────────────────────────

interface DeviceRules<C extends BindCode> {
  defaults: Readonly<Record<BindAction, readonly C[]>>;
  valid: (code: unknown, action: BindAction) => code is C;
  spare: readonly C[];
}

const KEY_RULES: Readonly<Record<'kb1' | 'kb2', DeviceRules<string>>> = {
  kb1: { defaults: DEFAULT_BINDINGS.kb1, valid: isBindableKey, spare: SPARE_KEYS },
  kb2: { defaults: DEFAULT_BINDINGS.kb2, valid: isBindableKey, spare: SPARE_KEYS },
};
const PAD_RULES: DeviceRules<number> = { defaults: DEFAULT_BINDINGS.gamepad, valid: isBindableButton, spare: SPARE_BUTTONS };

function isRecord(v: unknown): v is Record<string, unknown> {
  return typeof v === 'object' && v !== null && !Array.isArray(v);
}

/** Primeiro código de `candidates` que a ação aceita e que ninguém mais usa. */
function firstFree<C extends BindCode>(candidates: readonly C[], action: BindAction, taken: readonly C[], rules: DeviceRules<C>): C | undefined {
  return candidates.find((c) => rules.valid(c, action) && !taken.includes(c));
}

function sanitizeDevice<C extends BindCode>(raw: unknown, rules: DeviceRules<C>): Record<BindAction, C[]> {
  const r = isRecord(raw) ? raw : {};
  const lists = {} as Record<BindAction, C[]>;
  // 1) Por ação: só códigos válidos, sem repetição, no máximo três; nada válido → o padrão da ação.
  for (const a of BIND_ACTIONS) {
    const codes: C[] = [];
    const v = r[a];
    if (Array.isArray(v)) {
      for (const c of v) if (rules.valid(c, a) && !codes.includes(c) && codes.length < MAX_CODES_PER_ACTION) codes.push(c);
    }
    lists[a] = codes.length > 0 ? codes : [...rules.defaults[a]];
  }
  // 2) Conflito no mesmo dispositivo: na ordem das ações, quem chega primeiro fica com o código.
  //    Quem ficar sem nada recebe o próprio padrão ou, se já estiver tomado, o primeiro código livre
  //    entre os padrões das outras ações (a "troca") e a reserva. Nenhuma ação termina vazia.
  const taken: C[] = [];
  const pool: C[] = [...BIND_ACTIONS.flatMap((a) => rules.defaults[a]), ...rules.spare];
  for (const a of BIND_ACTIONS) {
    let codes = lists[a].filter((c) => !taken.includes(c));
    if (codes.length === 0) codes = rules.defaults[a].filter((c) => !taken.includes(c));
    if (codes.length === 0) {
      const free = firstFree(pool, a, taken, rules);
      codes = free === undefined ? [] : [free];
    }
    taken.push(...codes);
    lists[a] = codes;
  }
  return lists;
}

/** Funde o que veio do armazenamento com o padrão: código inválido vira o padrão da ação, conflito é resolvido. Nunca lança. */
export function sanitizeBindings(raw: unknown): ControlBindings {
  const r = isRecord(raw) ? raw : {};
  return {
    kb1: sanitizeDevice(r.kb1, KEY_RULES.kb1),
    kb2: sanitizeDevice(r.kb2, KEY_RULES.kb2),
    gamepad: sanitizeDevice(r.gamepad, PAD_RULES),
  };
}

// ───────────────────────────── Trocar, restaurar ─────────────────────────────

export interface Displaced {
  /** Ação que perdeu o código. */
  action: BindAction;
  /** O que ficou com ela. */
  codes: BindCode[];
  /** Verdadeiro quando ela recebeu os códigos antigos da ação editada (troca); falso quando só perdeu um de vários. */
  swapped: boolean;
}

export interface AssignResult {
  bindings: ControlBindings;
  /** Falso quando nada mudou (mesmo código de antes, ou código recusado). */
  changed: boolean;
  /** Código recusado: tipo errado para o dispositivo, reservado (Esc/Start fora da pausa) ou fora da lista. */
  rejected: boolean;
  displaced: Displaced | null;
}

function assignIn<C extends BindCode>(map: Record<BindAction, C[]>, action: BindAction, code: C, rules: DeviceRules<C>): { map: Record<BindAction, C[]>; changed: boolean; displaced: Displaced | null } {
  const next = cloneMap(map);
  const old = next[action];
  if (old.length === 1 && old[0] === code) return { map: next, changed: false, displaced: null };
  next[action] = [code];
  let displaced: Displaced | null = null;
  for (const o of BIND_ACTIONS) {
    if (o === action || !next[o].includes(code)) continue;
    const rest = next[o].filter((c) => c !== code);
    if (rest.length > 0) {
      next[o] = rest;
      displaced = { action: o, codes: rest, swapped: false };
      continue;
    }
    // Ficaria sem nada: recebe o que a ação editada tinha (a troca); se não servir, o próprio padrão ou a reserva.
    const taken = BIND_ACTIONS.filter((a) => a !== o).flatMap((a) => next[a]);
    const given = old.filter((c) => c !== code && rules.valid(c, o) && !taken.includes(c));
    const pool = [...rules.defaults[o], ...BIND_ACTIONS.flatMap((a) => rules.defaults[a]), ...rules.spare];
    const fallback = firstFree(pool, o, taken, rules);
    next[o] = given.length > 0 ? given : fallback === undefined ? [] : [fallback];
    displaced = { action: o, codes: [...next[o]], swapped: given.length > 0 };
  }
  return { map: next, changed: true, displaced };
}

/**
 * Liga `code` à ação no dispositivo (substitui o que ela tinha). Se outra ação do mesmo
 * dispositivo usava o código, ela o perde; se ficaria sem nada, recebe os códigos antigos (troca).
 * Devolve cópia nova; a entrada não é alterada.
 */
export function assignBinding(b: ControlBindings, device: BindDevice, action: BindAction, code: BindCode): AssignResult {
  const unchanged = (rejected: boolean): AssignResult => ({ bindings: cloneBindings(b), changed: false, rejected, displaced: null });
  if (device === 'gamepad') {
    if (!isBindableButton(code, action)) return unchanged(true);
    const r = assignIn(b.gamepad, action, code, PAD_RULES);
    return { bindings: { kb1: cloneMap(b.kb1), kb2: cloneMap(b.kb2), gamepad: r.map }, changed: r.changed, rejected: false, displaced: r.displaced };
  }
  if (!isBindableKey(code, action)) return unchanged(true);
  const r = assignIn(b[device], action, code, KEY_RULES[device]);
  const out = cloneBindings(b);
  out[device] = r.map;
  return { bindings: out, changed: r.changed, rejected: false, displaced: r.displaced };
}

/** Cópia com um dispositivo de volta ao padrão (os outros ficam como estão). */
export function restoreDefaults(b: ControlBindings, device: BindDevice): ControlBindings {
  const out = cloneBindings(b);
  if (device === 'gamepad') out.gamepad = cloneMap(DEFAULT_BINDINGS.gamepad);
  else out[device] = cloneMap(DEFAULT_BINDINGS[device]);
  return out;
}

export function isDefaultDevice(b: ControlBindings, device: BindDevice): boolean {
  const cur: Record<BindAction, readonly BindCode[]> = b[device];
  const def: Record<BindAction, readonly BindCode[]> = DEFAULT_BINDINGS[device];
  return BIND_ACTIONS.every((a) => cur[a].length === def[a].length && cur[a].every((c, i) => c === def[a][i]));
}

export interface KeyboardConflict { code: string; kb1: BindAction; kb2: BindAction }

/**
 * Teclas ligadas nos dois teclados ao mesmo tempo. Não é bloqueado (quem joga sozinho no teclado 1
 * não se importa com o teclado 2), mas a tela avisa: com os dois em uso, a tecla serve aos dois.
 * Esc fica de fora — é a pausa padrão de ambos e pausa de qualquer jeito.
 */
export function keyboardConflicts(b: ControlBindings): KeyboardConflict[] {
  const out: KeyboardConflict[] = [];
  for (const a of BIND_ACTIONS) {
    for (const code of b.kb1[a]) {
      if (code === RESERVED_KEY) continue;
      const other = BIND_ACTIONS.find((o) => b.kb2[o].includes(code));
      if (other) out.push({ code, kb1: a, kb2: other });
    }
  }
  return out;
}
