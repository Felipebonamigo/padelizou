// Entrada: um teclado que serve dois assentos (setas e WASD) e até quatro gamepads no
// mapeamento "standard" da Gamepad API. O estado segurado é lido uma vez por quadro em
// `poll()`, que também fecha as bordas (nitro, marchas e navegação de menu): borda é
// verdadeira só no quadro em que o botão foi apertado. As funções de mapeamento são puras
// e rodam em Node (tests/ui.test.ts, tests/input-remap.test.ts); só `createInput` toca o DOM.
// Pilotagem segue os bindings remapeáveis (src/ui/remap/bindings.ts); a navegação de menu é fixa.
import { NEUTRAL_INPUT, type PlayerInput } from '../core/types';
import type { DeviceId, DeviceInfo, DevicePeek, InputProvider, MenuNav } from '../game/contracts';
import { t } from '../i18n';
import { DEFAULT_BINDINGS, RESERVED_BUTTON, RESERVED_KEY, type ControlBindings } from './remap/bindings';
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

/**
 * Botões "standard": 0 A, 1 B, 2 X, 3 Y, 4 LB, 5 RB, 6 LT, 7 RT, 9 Start, 12–15 d-pad. A pilotagem
 * segue `bindings.gamepad`; o analógico esquerdo sempre vira, e d-pad/analógico, A, B e Start sempre
 * navegam os menus. Start sempre pausa, além do botão de pausa escolhido.
 */
export function mapGamepad(buttons: readonly boolean[], axes: readonly number[], bindings: ControlBindings = DEFAULT_BINDINGS): DeviceRaw {
  const b = (i: number) => buttons[i] === true;
  const any = (list: readonly number[]) => list.some(b);
  const ax = (i: number) => (typeof axes[i] === 'number' && Number.isFinite(axes[i]) ? axes[i] : 0);
  const g = bindings.gamepad;
  let steer = applyDeadzone(ax(0));
  if (any(g.left)) steer = -1;
  else if (any(g.right)) steer = 1;
  const x = ax(0);
  const y = ax(1);
  return {
    steer,
    throttle: any(g.throttle),
    brake: any(g.brake),
    nitro: any(g.nitro),
    gearUp: any(g.gearUp),
    gearDown: any(g.gearDown),
    up: b(12) || y < -0.5,
    down: b(13) || y > 0.5,
    left: b(14) || x < -0.5,
    right: b(15) || x > 0.5,
    confirm: b(0),
    back: b(1),
    start: b(RESERVED_BUTTON),
    pause: any(g.pause) || b(RESERVED_BUTTON),
  };
}

export interface NavLayout {
  up: readonly string[]; down: readonly string[]; left: readonly string[]; right: readonly string[];
  confirm: readonly string[]; back: readonly string[];
}

/**
 * Teclas de navegação dos menus por teclado — fixas, fora do remapeamento, para ninguém se trancar
 * fora (as mesmas de `KEY_NAV` em menus.ts). As de pilotagem vêm de `DEFAULT_BINDINGS` e das opções.
 */
export const NAV_LAYOUTS: Readonly<Record<KeyboardId, NavLayout>> = {
  kb1: {
    up: ['ArrowUp'], down: ['ArrowDown'], left: ['ArrowLeft'], right: ['ArrowRight'],
    confirm: ['Enter', 'NumpadEnter', 'Space'], back: ['Escape', 'Backspace'],
  },
  kb2: {
    up: ['KeyW'], down: ['KeyS'], left: ['KeyA'], right: ['KeyD'],
    // Enter confirma nos menus para todo mundo, mas como "dispositivo" pertence ao kb1: se
    // contasse para o kb2, o Enter do P1 no lobby faria o kb2 entrar num assento sozinho.
    confirm: ['KeyF'], back: ['Escape'],
  },
};

/** Códigos que recebem preventDefault: navegação fixa + tudo o que está ligado a alguma ação. */
export function usedKeyCodes(bindings: ControlBindings): Set<string> {
  const out = new Set<string>();
  for (const layout of Object.values(NAV_LAYOUTS)) for (const codes of Object.values(layout)) for (const c of codes) out.add(c);
  for (const kb of KEYBOARDS) for (const codes of Object.values(bindings[kb])) for (const c of codes) out.add(c);
  return out;
}

/** As teclas em uso com o mapeamento padrão. */
export const USED_KEY_CODES: ReadonlySet<string> = usedKeyCodes(DEFAULT_BINDINGS);

/** Esc sempre pausa, além da tecla de pausa escolhida (e sempre volta nos menus). */
export function mapKeyboard(keys: ReadonlySet<string>, layout: KeyboardId, bindings: ControlBindings = DEFAULT_BINDINGS): DeviceRaw {
  const B = bindings[layout];
  const N = NAV_LAYOUTS[layout];
  const any = (codes: readonly string[]) => codes.some((c) => keys.has(c));
  return {
    steer: (any(B.right) ? 1 : 0) - (any(B.left) ? 1 : 0),
    throttle: any(B.throttle),
    brake: any(B.brake),
    nitro: any(B.nitro),
    gearUp: any(B.gearUp),
    gearDown: any(B.gearDown),
    up: any(N.up), down: any(N.down), left: any(N.left), right: any(N.right),
    confirm: any(N.confirm),
    back: any(N.back),
    start: false,
    pause: any(B.pause) || keys.has(RESERVED_KEY),
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

// ───────────────────────────── Vibração (puro) ─────────────────────────────

/** Tremor em andamento num gamepad: até quando (ms de `performance.now`) e com que força. */
export interface RumbleSlot { until: number; strength: number }

/**
 * Decide se um pedido de vibração toca: um tremor mais fraco não corta um mais forte que ainda
 * está tocando (o `playEffect` novo substitui o anterior). Devolve o novo estado ou null.
 */
export function nextRumble(prev: RumbleSlot, now: number, strength: number, ms: number): RumbleSlot | null {
  if (!(strength > 0) || !(ms > 0)) return null;
  if (now < prev.until && strength < prev.strength) return null;
  return { until: now + ms, strength };
}

/** Força 0..1 → motores do "dual-rumble": o forte (grave) segue a força; o fraco (agudo) dá corpo aos toques leves. */
export function rumbleMagnitudes(strength: number): { strong: number; weak: number } {
  const s = Math.min(1, Math.max(0, Number.isFinite(strength) ? strength : 0));
  return { strong: s, weak: Math.min(1, 0.08 + s * 0.6) };
}

interface HapticActuator {
  playEffect(type: string, params: { startDelay: number; duration: number; strongMagnitude: number; weakMagnitude: number }): unknown;
}

/** `gamepad.vibrationActuator` quando existe e tem `playEffect` (Chromium/Electron); senão null. */
function hapticsOf(pad: unknown): HapticActuator | null {
  if (typeof pad !== 'object' || pad === null) return null;
  const actuator = (pad as { vibrationActuator?: unknown }).vibrationActuator;
  if (typeof actuator !== 'object' || actuator === null) return null;
  return typeof (actuator as { playEffect?: unknown }).playEffect === 'function' ? (actuator as HapticActuator) : null;
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
  /** Botões "standard" apertados no último `poll()` (só gamepads). */
  buttons: number[];
  rumble: RumbleSlot;
}

function newDevice(id: DeviceId, connected: boolean, hardwareName = ''): DeviceState {
  return {
    id, raw: { ...NEUTRAL_RAW }, edges: { ...NO_EDGES }, repeat: { up: null, down: null, left: null, right: null }, connected, hardwareName,
    buttons: [], rumble: { until: 0, strength: 0 },
  };
}

export interface InputOptions {
  /** Mapeamento atual, lido a cada quadro (a sessão passa `() => settings.controls`). */
  bindings?: () => ControlBindings;
  /** Vibração ligada nas opções (a sessão passa `() => settings.vibration`). */
  vibration?: () => boolean;
}

function nowMs(): number {
  return typeof performance !== 'undefined' && typeof performance.now === 'function' ? performance.now() : Date.now();
}

export function createInput(target: Window = window, opts: InputOptions = {}): InputProvider {
  const bindingsOf = opts.bindings ?? (() => DEFAULT_BINDINGS);
  const vibrationOn = opts.vibration ?? (() => true);
  let usedFor: ControlBindings | null = null;
  let used: ReadonlySet<string> = USED_KEY_CODES;
  /** Teclas em uso, recalculadas só quando o objeto de bindings muda (a tela de controles grava um novo). */
  const usedKeys = (): ReadonlySet<string> => {
    const b = bindingsOf();
    if (b !== usedFor) { usedFor = b; used = usedKeyCodes(b); }
    return used;
  };
  const held = new Set<string>();
  /** Teclas apertadas desde o último `poll()` — captura um toque mais curto que um quadro. */
  const tapped = new Set<string>();
  /** Esc segurado no último `poll()` e a borda dele: é a única pausa de um teclado sem assento. */
  let escapeHeld = false;
  let escapeEdge = false;
  const devices = new Map<DeviceId, DeviceState>();
  const seats: Array<DeviceId | null> = [null, null, null, null];
  for (const kb of KEYBOARDS) devices.set(kb, newDevice(kb, true));

  const onKeyDown = (e: KeyboardEvent) => {
    if (e.ctrlKey || e.metaKey || e.altKey) return;
    if (isEditableTarget(e.target)) return;
    held.add(e.code);
    if (!e.repeat) tapped.add(e.code);
    if (usedKeys().has(e.code)) e.preventDefault();
  };
  const onKeyUp = (e: KeyboardEvent) => { held.delete(e.code); };
  const onBlur = () => { held.clear(); };
  const onGamepadConnected = (e: GamepadEvent) => { registerGamepad(e.gamepad); };
  const onGamepadDisconnected = (e: GamepadEvent) => {
    const d = devices.get(`gp${e.gamepad.index}`);
    if (d) { d.connected = false; d.raw = { ...NEUTRAL_RAW }; d.edges = { ...NO_EDGES }; d.buttons = []; }
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
      return Array.from(nav.getGamepads() ?? []);
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
      const bindings = bindingsOf();
      const pads = readGamepads();
      for (let i = 0; i < MAX_GAMEPADS; i++) {
        const pad = pads[i];
        const id: DeviceId = `gp${i}`;
        if (pad && pad.connected) {
          // Chrome só lista o gamepad depois de um botão apertado, e nem sempre dispara o evento.
          const d = registerGamepad(pad);
          if (d) {
            const pressed = pad.buttons.map((b) => b.pressed || b.value > 0.5);
            d.buttons = pressed.flatMap((p, index) => (p ? [index] : []));
            step(d, mapGamepad(pressed, pad.axes, bindings), now);
          }
        } else {
          const d = devices.get(id);
          if (d) { d.connected = false; d.buttons = []; step(d, { ...NEUTRAL_RAW }, now); }
        }
      }
      const keys = new Set<string>([...held, ...tapped]);
      tapped.clear();
      escapeEdge = edge(escapeHeld, keys.has(RESERVED_KEY));
      escapeHeld = keys.has(RESERVED_KEY);
      for (const kb of KEYBOARDS) {
        const d = devices.get(kb);
        if (d) step(d, mapKeyboard(keys, kb, bindings), now);
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
        // Teclado sem assento pausa só pelo Esc (vale como assento 0: todos nos controles e alguém
        // aperta Esc). A pausa escolhida dele não: ela pode ser a tecla de pilotagem do outro teclado.
        if (isKeyboard(d.id) && escapeEdge) return 0;
      }
      return -1;
    },

    peek(id): DevicePeek | null {
      const d = devices.get(id);
      if (!d) return null;
      const r = d.connected ? d.raw : NEUTRAL_RAW;
      return {
        steer: r.steer, throttle: r.throttle, brake: r.brake, nitro: r.nitro, gearUp: r.gearUp, gearDown: r.gearDown, pause: r.pause,
        buttons: d.connected ? [...d.buttons] : [],
      };
    },

    rumble(seat, strength, ms) {
      if (!vibrationOn()) return;
      const id = seats[seat];
      if (!id || isKeyboard(id)) return;
      const d = devices.get(id);
      if (!d || !d.connected) return;
      const actuator = hapticsOf(readGamepads()[gamepadIndex(id)]);
      if (!actuator) return;
      const next = nextRumble(d.rumble, nowMs(), strength, ms);
      if (!next) return;
      d.rumble = next;
      const m = rumbleMagnitudes(strength);
      try {
        const done = actuator.playEffect('dual-rumble', { startDelay: 0, duration: Math.round(ms), strongMagnitude: m.strong, weakMagnitude: m.weak });
        // Promessa recusada (controle sem motor, aba em segundo plano) não pode virar erro solto.
        if (done instanceof Promise) done.catch(() => undefined);
      } catch { /* navegador sem suporte ao efeito */ }
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
