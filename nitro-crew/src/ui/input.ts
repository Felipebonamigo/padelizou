// Entrada: um teclado que serve dois assentos (setas e WASD) e até quatro gamepads no
// mapeamento "standard" da Gamepad API. O estado segurado é lido uma vez por quadro em
// `poll()`, que também fecha as bordas (nitro, marchas e navegação de menu): borda é
// verdadeira só no quadro em que o botão foi apertado. As funções de mapeamento são puras
// e rodam em Node (tests/ui.test.ts); só `createInput` toca o DOM.
import { NEUTRAL_INPUT, type PlayerInput } from '../core/types';
import type { DeviceId, DeviceInfo, InputProvider, MenuNav } from '../game/contracts';
import { t } from '../i18n';
import './strings';

export const DEADZONE = 0.2;
/** Intervalo de repetição da navegação de menu com direcional segurado. */
export const MENU_REPEAT_MS = 180;
export const MAX_GAMEPADS = 4;
export const MAX_SEATS = 4;
export type KeyboardId = 'kb1' | 'kb2';
export const KEYBOARDS: readonly KeyboardId[] = ['kb1', 'kb2'];

/** Estado segurado de um dispositivo num quadro (sem bordas). */
export interface DeviceRaw {
  steer: number;
  throttle: boolean;
  brake: boolean;
  nitro: boolean;
  gearUp: boolean;
  gearDown: boolean;
  up: boolean;
  down: boolean;
  left: boolean;
  right: boolean;
  confirm: boolean;
  back: boolean;
  start: boolean;
  pause: boolean;
}

export const NEUTRAL_RAW: Readonly<DeviceRaw> = Object.freeze({
  steer: 0, throttle: false, brake: false, nitro: false, gearUp: false, gearDown: false,
  up: false, down: false, left: false, right: false, confirm: false, back: false, start: false, pause: false,
});

export type EdgeKey = 'nitro' | 'gearUp' | 'gearDown' | 'up' | 'down' | 'left' | 'right' | 'confirm' | 'back' | 'start' | 'pause';
export const EDGE_KEYS: readonly EdgeKey[] = ['nitro', 'gearUp', 'gearDown', 'up', 'down', 'left', 'right', 'confirm', 'back', 'start', 'pause'];
export type NavKey = 'up' | 'down' | 'left' | 'right';
export const NAV_KEYS: readonly NavKey[] = ['up', 'down', 'left', 'right'];
export type DeviceEdges = Record<EdgeKey, boolean>;

export const NO_EDGES: Readonly<DeviceEdges> = Object.freeze({
  nitro: false, gearUp: false, gearDown: false, up: false, down: false, left: false, right: false,
  confirm: false, back: false, start: false, pause: false,
});

// ───────────────────────────── Mapeamentos (puros) ─────────────────────────────

/** Zona morta com reescala: abaixo de `dz` é zero; de `dz` a 1 vira 0..1. */
export function applyDeadzone(v: number, dz = DEADZONE): number {
  if (!Number.isFinite(v)) return 0;
  const a = Math.abs(v);
  if (a < dz) return 0;
  return Math.sign(v) * Math.min(1, (a - dz) / (1 - dz));
}

/** Botões "standard": 0 A, 1 B, 2 X, 3 Y, 4 LB, 5 RB, 6 LT, 7 RT, 9 Start, 12–15 d-pad. */
export function mapGamepad(buttons: readonly boolean[], axes: readonly number[]): DeviceRaw {
  const b = (i: number) => buttons[i] === true;
  const ax = (i: number) => (typeof axes[i] === 'number' && Number.isFinite(axes[i]) ? axes[i] : 0);
  let steer = applyDeadzone(ax(0));
  if (b(14)) steer = -1;
  else if (b(15)) steer = 1;
  const x = ax(0);
  const y = ax(1);
  return {
    steer,
    throttle: b(0) || b(7),
    brake: b(1) || b(2) || b(6),
    nitro: b(5),
    gearUp: b(3),
    gearDown: b(4),
    up: b(12) || y < -0.5,
    down: b(13) || y > 0.5,
    left: b(14) || x < -0.5,
    right: b(15) || x > 0.5,
    confirm: b(0),
    back: b(1),
    start: b(9),
    pause: b(9),
  };
}

export interface KeyLayout {
  up: readonly string[]; down: readonly string[]; left: readonly string[]; right: readonly string[];
  nitro: readonly string[]; gearUp: readonly string[]; gearDown: readonly string[];
  confirm: readonly string[]; back: readonly string[]; pause: readonly string[];
}

/** Códigos físicos (`KeyboardEvent.code`) de cada assento de teclado. */
export const KEY_LAYOUTS: Readonly<Record<KeyboardId, KeyLayout>> = {
  kb1: {
    up: ['ArrowUp'], down: ['ArrowDown'], left: ['ArrowLeft'], right: ['ArrowRight'],
    nitro: ['Space'], gearUp: ['KeyM'], gearDown: ['KeyN'],
    confirm: ['Enter', 'NumpadEnter', 'Space'], back: ['Escape', 'Backspace'], pause: ['Escape'],
  },
  kb2: {
    up: ['KeyW'], down: ['KeyS'], left: ['KeyA'], right: ['KeyD'],
    nitro: ['KeyF'], gearUp: ['KeyE'], gearDown: ['KeyQ'],
    // Enter confirma nos menus para todo mundo, mas como "dispositivo" pertence ao kb1: se
    // contasse para o kb2, o Enter do P1 no lobby faria o kb2 entrar num assento sozinho.
    confirm: ['KeyF'], back: ['Escape'], pause: ['Escape'],
  },
};

/** Todos os códigos usados por algum assento (para o preventDefault). */
export const USED_KEY_CODES: ReadonlySet<string> = new Set(
  Object.values(KEY_LAYOUTS).flatMap((l) => Object.values(l).flat()),
);

export function mapKeyboard(keys: ReadonlySet<string>, layout: KeyboardId): DeviceRaw {
  const L = KEY_LAYOUTS[layout];
  const any = (codes: readonly string[]) => codes.some((c) => keys.has(c));
  const left = any(L.left);
  const right = any(L.right);
  const up = any(L.up);
  const down = any(L.down);
  return {
    steer: (right ? 1 : 0) - (left ? 1 : 0),
    throttle: up,
    brake: down,
    nitro: any(L.nitro),
    gearUp: any(L.gearUp),
    gearDown: any(L.gearDown),
    up, down, left, right,
    confirm: any(L.confirm),
    back: any(L.back),
    start: false,
    pause: any(L.pause),
  };
}

/** Borda de subida: verdadeiro só quando estava solto e agora está apertado. */
export function edge(prev: boolean, now: boolean): boolean {
  return now && !prev;
}

/** Fecha as bordas de todos os botões entre dois quadros (sem repetição). */
export function closeEdges(prev: DeviceRaw, now: DeviceRaw): DeviceEdges {
  const out = { ...NO_EDGES };
  for (const k of EDGE_KEYS) out[k] = edge(prev[k], now[k]);
  return out;
}

/**
 * Repetição de direcional segurado: borda ao apertar e a cada `interval` ms enquanto segura.
 * `last` é o instante da última borda emitida (null = solto). Devolve a borda e o novo `last`.
 */
export function repeatEdge(held: boolean, last: number | null, nowMs: number, interval = MENU_REPEAT_MS): { edge: boolean; last: number | null } {
  if (!held) return { edge: false, last: null };
  if (last === null || nowMs - last >= interval) return { edge: true, last: nowMs };
  return { edge: false, last };
}

export function toPlayerInput(raw: DeviceRaw, edges: DeviceEdges): PlayerInput {
  return { steer: raw.steer, throttle: raw.throttle, brake: raw.brake, nitro: edges.nitro, gearUp: edges.gearUp, gearDown: edges.gearDown };
}

export function isKeyboard(id: DeviceId): id is KeyboardId {
  return id === 'kb1' || id === 'kb2';
}

export function gamepadIndex(id: DeviceId): number {
  return id.startsWith('gp') ? Number(id.slice(2)) : -1;
}

/** Nome curto do gamepad: "Xbox 360 Controller (XInput STANDARD GAMEPAD Vendor…)" → "Xbox 360 Controller". */
export function shortGamepadName(id: string): string {
  const cut = id.split(' (')[0].trim();
  return cut.length > 28 ? cut.slice(0, 27) + '…' : cut;
}

/** Alvo de evento onde o jogador está digitando (nome no lobby): o jogo não pode roubar as teclas. */
export function isEditableTarget(target: EventTarget | null): boolean {
  const el = target as { tagName?: string; isContentEditable?: boolean } | null;
  if (!el || typeof el.tagName !== 'string') return false;
  const tag = el.tagName.toUpperCase();
  return tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || el.isContentEditable === true;
}

// ───────────────────────────── Provedor ─────────────────────────────

interface DeviceState {
  id: DeviceId;
  raw: DeviceRaw;
  edges: DeviceEdges;
  repeat: Record<NavKey, number | null>;
  connected: boolean;
  /** `Gamepad.id` cru (só gamepads). */
  hardwareName: string;
}

function newDevice(id: DeviceId, connected: boolean, hardwareName = ''): DeviceState {
  return { id, raw: { ...NEUTRAL_RAW }, edges: { ...NO_EDGES }, repeat: { up: null, down: null, left: null, right: null }, connected, hardwareName };
}

function nowMs(): number {
  return typeof performance !== 'undefined' && typeof performance.now === 'function' ? performance.now() : Date.now();
}

export function createInput(target: Window = window): InputProvider {
  const held = new Set<string>();
  /** Teclas apertadas desde o último `poll()` — captura um toque mais curto que um quadro. */
  const tapped = new Set<string>();
  const devices = new Map<DeviceId, DeviceState>();
  const seats: Array<DeviceId | null> = [null, null, null, null];
  for (const kb of KEYBOARDS) devices.set(kb, newDevice(kb, true));

  const onKeyDown = (e: KeyboardEvent) => {
    if (e.ctrlKey || e.metaKey || e.altKey) return;
    if (isEditableTarget(e.target)) return;
    held.add(e.code);
    if (!e.repeat) tapped.add(e.code);
    if (USED_KEY_CODES.has(e.code)) e.preventDefault();
  };
  const onKeyUp = (e: KeyboardEvent) => { held.delete(e.code); };
  const onBlur = () => { held.clear(); };
  const onGamepadConnected = (e: GamepadEvent) => { registerGamepad(e.gamepad); };
  const onGamepadDisconnected = (e: GamepadEvent) => {
    const d = devices.get(`gp${e.gamepad.index}`);
    if (d) { d.connected = false; d.raw = { ...NEUTRAL_RAW }; d.edges = { ...NO_EDGES }; }
  };

  function registerGamepad(gp: Gamepad): DeviceState | null {
    if (gp.index < 0 || gp.index >= MAX_GAMEPADS) return null;
    const id: DeviceId = `gp${gp.index}`;
    let d = devices.get(id);
    if (!d) { d = newDevice(id, true, gp.id); devices.set(id, d); }
    d.connected = true;
    d.hardwareName = gp.id;
    return d;
  }

  function readGamepads(): Array<Gamepad | null> {
    try {
      const nav = target.navigator;
      if (!nav || typeof nav.getGamepads !== 'function') return [];
      return Array.from(nav.getGamepads());
    } catch {
      return [];
    }
  }

  /** Ordem fixa: teclados primeiro, depois os gamepads por índice. */
  function ordered(): DeviceState[] {
    const out: DeviceState[] = [];
    for (const kb of KEYBOARDS) { const d = devices.get(kb); if (d) out.push(d); }
    for (let i = 0; i < MAX_GAMEPADS; i++) { const d = devices.get(`gp${i}`); if (d) out.push(d); }
    return out;
  }

  function seatOf(id: DeviceId): number {
    return seats.indexOf(id);
  }

  function label(d: DeviceState): string {
    if (isKeyboard(d.id)) return t(`ui.device.${d.id}`);
    const n = gamepadIndex(d.id) + 1;
    const name = shortGamepadName(d.hardwareName);
    return name ? t('ui.device.gp', { n, name }) : t('ui.device.gpGeneric', { n });
  }

  function step(d: DeviceState, raw: DeviceRaw, now: number): void {
    const edges = closeEdges(d.raw, raw);
    for (const k of NAV_KEYS) {
      const r = repeatEdge(raw[k], d.repeat[k], now);
      d.repeat[k] = r.last;
      edges[k] = r.edge;
    }
    d.edges = edges;
    d.raw = raw;
  }

  function attach(): void {
    target.addEventListener('keydown', onKeyDown);
    target.addEventListener('keyup', onKeyUp);
    target.addEventListener('blur', onBlur);
    target.addEventListener('gamepadconnected', onGamepadConnected);
    target.addEventListener('gamepaddisconnected', onGamepadDisconnected);
  }

  attach();

  return {
    poll() {
      const now = nowMs();
      const pads = readGamepads();
      for (let i = 0; i < MAX_GAMEPADS; i++) {
        const pad = pads[i];
        const id: DeviceId = `gp${i}`;
        if (pad && pad.connected) {
          // Chrome só lista o gamepad depois de um botão apertado, e nem sempre dispara o evento.
          const d = registerGamepad(pad);
          if (d) step(d, mapGamepad(pad.buttons.map((b) => b.pressed || b.value > 0.5), pad.axes), now);
        } else {
          const d = devices.get(id);
          if (d) { d.connected = false; step(d, { ...NEUTRAL_RAW }, now); }
        }
      }
      const keys = new Set<string>([...held, ...tapped]);
      tapped.clear();
      for (const kb of KEYBOARDS) {
        const d = devices.get(kb);
        if (d) step(d, mapKeyboard(keys, kb), now);
      }
    },

    readSeat(seat) {
      const id = seats[seat];
      const d = id ? devices.get(id) : undefined;
      if (!d || !d.connected) return { ...NEUTRAL_INPUT };
      return toPlayerInput(d.raw, d.edges);
    },

    bindSeat(seat, device) {
      if (seat < 0 || seat >= MAX_SEATS) return;
      const previous = seatOf(device);
      if (previous >= 0) seats[previous] = null;
      seats[seat] = device;
    },

    unbindSeat(seat) {
      if (seat >= 0 && seat < MAX_SEATS) seats[seat] = null;
    },

    seatDevice(seat) {
      return seats[seat] ?? null;
    },

    devices(): DeviceInfo[] {
      return ordered().map((d) => {
        const seat = seatOf(d.id);
        return { id: d.id, label: label(d), connected: d.connected, boundSeat: seat >= 0 ? seat : null };
      });
    },

    joinPressed() {
      for (const d of ordered()) {
        if (!d.connected || seatOf(d.id) >= 0) continue;
        if (d.edges.confirm || d.edges.start) return d.id;
      }
      return null;
    },

    leavePressed() {
      for (const d of ordered()) {
        if (seatOf(d.id) >= 0 && d.edges.back) return d.id;
      }
      return null;
    },

    menuNav(): MenuNav {
      const nav: MenuNav = { up: false, down: false, left: false, right: false, confirm: false, back: false, start: false, device: null };
      for (const d of ordered()) {
        const e = d.edges;
        const any = e.up || e.down || e.left || e.right || e.confirm || e.back || e.start;
        if (!any) continue;
        nav.up ||= e.up; nav.down ||= e.down; nav.left ||= e.left; nav.right ||= e.right;
        nav.confirm ||= e.confirm; nav.back ||= e.back; nav.start ||= e.start;
        if (nav.device === null) nav.device = d.id;
      }
      return nav;
    },

    pausePressed() {
      for (const d of ordered()) {
        if (!d.edges.pause) continue;
        const seat = seatOf(d.id);
        if (seat >= 0) return seat;
        if (isKeyboard(d.id)) return 0;
      }
      return -1;
    },

    dispose() {
      target.removeEventListener('keydown', onKeyDown);
      target.removeEventListener('keyup', onKeyUp);
      target.removeEventListener('blur', onBlur);
      target.removeEventListener('gamepadconnected', onGamepadConnected);
      target.removeEventListener('gamepaddisconnected', onGamepadDisconnected);
      held.clear();
      tapped.clear();
    },
  };
}
