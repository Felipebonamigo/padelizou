// Protocolo do online (lockstep via servidor de retransmissão). Tudo o que chega da rede passa
// por aqui antes de ser usado: `parseServerMessage` devolve uma mensagem tipada e validada, ou
// `null` (a mensagem é descartada e contada). O servidor (server/relay.mjs) espelha as mesmas
// regras de forma e de tamanho, mas não entende o jogo; quem valida o conteúdo é o cliente.
import type { CoopAssists, Difficulty, PlayerInput } from '../core/types';

export const PROTOCOL_VERSION = 1;
export const DEFAULT_SERVER_URL = 'ws://localhost:8787';
export const SERVER_URL_MAX_LENGTH = 200;

/** Códigos de sala: 5 letras, sem I e O (não se confundem com 1 e 0 quando ditados). */
export const ROOM_CODE_LENGTH = 5;
export const ROOM_CODE_ALPHABET = 'ABCDEFGHJKLMNPQRSTUVWXYZ';
export const MAX_CLIENTS = 4;
export const MAX_HUMANS = 4;
export const MAX_LOCAL_PLAYERS = 2;
export const PLAYER_NAME_MAX = 12;

/** Atraso de entrada padrão em ticks (3 = 50 ms a 60 Hz) e a faixa aceita. */
export const DEFAULT_INPUT_DELAY = 3;
export const MIN_INPUT_DELAY = 1;
export const MAX_INPUT_DELAY = 12;
/** A cada quantos ticks os clientes trocam o hash do estado. */
export const HASH_INTERVAL = 60;
/** Um pacote de entradas não traz mais que isto (4 números por registro). */
export const MAX_RECORDS_PER_MESSAGE = 64;
/** Janela de reconexão (o servidor usa o mesmo número por padrão). */
export const RECONNECT_WINDOW_MS = 60_000;

// ───────────────────────────── Entrada por tick ─────────────────────────────

/** Bits da entrada compacta. `TAKEOVER` não é do jogador: é o comando "a IA assume este assento". */
export const INPUT_BIT = { throttle: 1, brake: 2, nitro: 4, gearUp: 8, gearDown: 16 } as const;
export const TAKEOVER_BIT = 128;
const PLAYER_BITS = 31;

/** Um registro do fluxo de entradas: o que o assento `seat` faz no tick `tick`. */
export interface InputRecord {
  tick: number;
  seat: number;
  /** INPUT_BIT somados, ou TAKEOVER_BIT sozinho. */
  bits: number;
  /** Volante quantizado em int8 (-127..127). */
  steer: number;
}

/** Volante -1..1 → -127..127 (arredondado simétrico: -0,5 e 0,5 viram -64 e 64; fora da faixa vai para a borda). */
export function quantizeSteer(steer: number): number {
  if (!Number.isFinite(steer)) return 0;
  const q = Math.round(Math.abs(steer) * 127);
  return Math.min(127, q) * (steer < 0 ? -1 : 1) || 0;
}

export function encodeInput(input: PlayerInput): { bits: number; steer: number } {
  let bits = 0;
  if (input.throttle) bits |= INPUT_BIT.throttle;
  if (input.brake) bits |= INPUT_BIT.brake;
  if (input.nitro) bits |= INPUT_BIT.nitro;
  if (input.gearUp) bits |= INPUT_BIT.gearUp;
  if (input.gearDown) bits |= INPUT_BIT.gearDown;
  return { bits, steer: quantizeSteer(input.steer) };
}

/** A entrada que todas as máquinas simulam (inclusive a que a gerou): sempre a decodificada. */
export function decodeInput(bits: number, steer: number): PlayerInput {
  return {
    steer: Math.max(-127, Math.min(127, Math.trunc(steer))) / 127,
    throttle: (bits & INPUT_BIT.throttle) !== 0,
    brake: (bits & INPUT_BIT.brake) !== 0,
    nitro: (bits & INPUT_BIT.nitro) !== 0,
    gearUp: (bits & INPUT_BIT.gearUp) !== 0,
    gearDown: (bits & INPUT_BIT.gearDown) !== 0,
  };
}

export function isTakeover(r: InputRecord): boolean {
  return r.bits === TAKEOVER_BIT;
}

/** Registros → lista plana [tick, assento, bits, volante, …] (o formato que vai no fio). */
export function packRecords(records: readonly InputRecord[]): number[] {
  const out: number[] = [];
  for (const r of records) out.push(r.tick, r.seat, r.bits, r.steer);
  return out;
}

function isInt(v: unknown, min: number, max: number): v is number {
  return typeof v === 'number' && Number.isInteger(v) && v >= min && v <= max;
}

/** Tick máximo aceito (uma corrida de 8 voltas não chega perto: 2^31 ticks são 400 dias). */
export const MAX_TICK = 0x7fffffff;

/** Lista plana → registros validados; `null` se qualquer coisa estiver fora do formato. */
export function unpackRecords(d: unknown): InputRecord[] | null {
  if (!Array.isArray(d) || d.length === 0 || d.length % 4 !== 0 || d.length > MAX_RECORDS_PER_MESSAGE * 4) return null;
  const out: InputRecord[] = [];
  for (let i = 0; i < d.length; i += 4) {
    const [tick, seat, bits, steer] = [d[i], d[i + 1], d[i + 2], d[i + 3]];
    if (!isInt(tick, 0, MAX_TICK) || !isInt(seat, 0, MAX_HUMANS - 1) || !isInt(bits, 0, 255) || !isInt(steer, -127, 127)) return null;
    if (bits !== TAKEOVER_BIT && (bits & ~PLAYER_BITS) !== 0) return null;
    if (bits === TAKEOVER_BIT && steer !== 0) return null;
    out.push({ tick, seat, bits, steer });
  }
  return out;
}

// ───────────────────────────── Sala ─────────────────────────────

export interface LobbyPlayer {
  name: string;
  /** Id do carro (core/data/cars). */
  car: string;
}

/** O que cada cliente publica sobre si no lobby. */
export interface ClientInfo {
  players: LobbyPlayer[];
  ready: boolean;
}

/** O que o anfitrião escolhe para a corrida. */
export interface RoomSettings {
  trackId: string;
  laps: number;
  versus: boolean;
  difficulty: Difficulty;
  totalCars: number;
  manualGear: boolean;
  assists: CoopAssists;
  delay: number;
}

export interface RoomClient {
  id: number;
  /** Jogadores locais declarados (o servidor garante a soma ≤ 4). */
  seats: number;
  info: ClientInfo | null;
  connected: boolean;
}

export interface RoomView {
  code: string;
  host: number;
  started: boolean;
  settings: RoomSettings | null;
  clients: RoomClient[];
}

export interface SeatAssignment {
  /** Assento global 0..3 (a posição do carro em `inputs` e a cor do jogador). */
  seat: number;
  /** Cliente dono do assento. */
  client: number;
  name: string;
  car: string;
}

/** Tudo que um cliente precisa para montar a mesma corrida que os outros. */
export interface StartConfig extends RoomSettings {
  seed: number;
  seats: SeatAssignment[];
}

/** Estado completo para quem reconecta: o tick, o estado serializado e o que está no buffer. */
export interface Snapshot {
  tick: number;
  state: string;
  start: StartConfig;
  /** Registros de entrada conhecidos para ticks ≥ `tick` (lista plana, como no fio). */
  inputs: number[];
  /** Pares [assento, tick] das tomadas de assento pela IA já decididas. */
  ai: number[];
}

// ───────────────────────────── Mensagens ─────────────────────────────

/** `build`: a sala foi criada por um jogo com outro conteúdo (carros, pistas, física). */
export type ErrorCode = 'version' | 'build' | 'no_room' | 'full' | 'started' | 'expired' | 'not_host' | 'bad' | 'rate' | 'rooms';

/** `b` no create/join: impressão do conteúdo do jogo; o relay só junta na mesma sala impressões iguais. */
export type ClientMessage =
  | { t: 'create'; v: number; b: string; seats: number; info: ClientInfo }
  | { t: 'join'; v: number; b: string; room: string; seats: number; info: ClientInfo }
  | { t: 'rejoin'; v: number; room: string; token: string }
  | { t: 'info'; seats: number; info: ClientInfo }
  | { t: 'settings'; settings: RoomSettings }
  | { t: 'start'; cfg: StartConfig }
  | { t: 'lobby' }
  | { t: 'i'; d: number[] }
  | { t: 'h'; k: number; h: number }
  | { t: 'snap'; to: number; snap: Snapshot }
  | { t: 'ping'; n: number }
  | { t: 'leave' };

export type PeerEvent = 'join' | 'rejoin' | 'lost' | 'drop' | 'leave';

export type ServerMessage =
  | { t: 'welcome'; room: string; id: number; token: string; rejoined: boolean }
  | { t: 'room'; room: RoomView }
  | { t: 'peer'; id: number; e: PeerEvent }
  | { t: 'error'; code: ErrorCode }
  | { t: 'pong'; n: number }
  | { t: 'start'; from: number; cfg: StartConfig }
  | { t: 'i'; from: number; records: InputRecord[] }
  | { t: 'h'; from: number; k: number; h: number }
  | { t: 'snap'; from: number; snap: Snapshot };

// ───────────────────────────── Validação ─────────────────────────────

export interface ContentRules {
  /** Ids de carro existentes. */
  cars: readonly string[];
  /** Ids de pista existentes. */
  tracks: readonly string[];
}

const DIFFICULTIES: readonly Difficulty[] = ['amador', 'profissional', 'campeao'];
const ERROR_CODES: readonly ErrorCode[] = ['version', 'build', 'no_room', 'full', 'started', 'expired', 'not_host', 'bad', 'rate', 'rooms'];
const PEER_EVENTS: readonly PeerEvent[] = ['join', 'rejoin', 'lost', 'drop', 'leave'];

function isRecord(v: unknown): v is Record<string, unknown> {
  return typeof v === 'object' && v !== null && !Array.isArray(v);
}

export function isRoomCode(v: unknown): v is string {
  if (typeof v !== 'string' || v.length !== ROOM_CODE_LENGTH) return false;
  for (const ch of v) if (!ROOM_CODE_ALPHABET.includes(ch)) return false;
  return true;
}

/** Texto digitado → código de sala (maiúsculas, sem espaços), ou `null` se não for um código. */
export function normalizeRoomCode(text: string): string | null {
  const code = text.replace(/\s+/g, '').toUpperCase();
  return isRoomCode(code) ? code : null;
}

/**
 * Endereço digitado → URL de WebSocket. Aceita `host:porta` (vira ws://), http(s):// (vira ws(s)://)
 * e ws(s)://. Devolve `null` para o que não for um endereço.
 */
export function normalizeServerUrl(text: unknown): string | null {
  if (typeof text !== 'string') return null;
  let s = text.trim();
  if (!s || s.length > SERVER_URL_MAX_LENGTH || /\s/.test(s)) return null;
  if (/^https?:\/\//i.test(s)) s = s.replace(/^http/i, 'ws');
  else if (/^[a-z][a-z0-9+.-]*:\/\//i.test(s) && !/^wss?:\/\//i.test(s)) return null;
  else if (!/^wss?:\/\//i.test(s)) s = `ws://${s}`;
  try {
    const u = new URL(s);
    if ((u.protocol !== 'ws:' && u.protocol !== 'wss:') || !u.hostname) return null;
    return s.replace(/^wss?/i, (p) => p.toLowerCase());
  } catch {
    return null;
  }
}

/** Nome visível: sem caracteres de controle, aparado e cortado; vazio vira `null`. */
export function cleanName(v: unknown): string | null {
  if (typeof v !== 'string') return null;
  const s = v.replace(/[\u0000-\u001f\u007f]/g, '').trim().slice(0, PLAYER_NAME_MAX);
  return s.length > 0 ? s : null;
}

export function parseClientInfo(v: unknown, rules: ContentRules): ClientInfo | null {
  if (!isRecord(v) || !Array.isArray(v.players) || typeof v.ready !== 'boolean') return null;
  if (v.players.length < 1 || v.players.length > MAX_LOCAL_PLAYERS) return null;
  const players: LobbyPlayer[] = [];
  for (const p of v.players) {
    if (!isRecord(p)) return null;
    const name = cleanName(p.name);
    if (!name || typeof p.car !== 'string' || !rules.cars.includes(p.car)) return null;
    players.push({ name, car: p.car });
  }
  return { players, ready: v.ready };
}

function parseAssists(v: unknown): CoopAssists | null {
  if (!isRecord(v)) return null;
  const keys: Array<keyof CoopAssists> = ['sharedNitro', 'tow', 'teamDraft', 'catchup'];
  for (const k of keys) if (typeof v[k] !== 'boolean') return null;
  return { sharedNitro: v.sharedNitro as boolean, tow: v.tow as boolean, teamDraft: v.teamDraft as boolean, catchup: v.catchup as boolean };
}

export function parseRoomSettings(v: unknown, rules: ContentRules): RoomSettings | null {
  if (!isRecord(v)) return null;
  if (typeof v.trackId !== 'string' || !rules.tracks.includes(v.trackId)) return null;
  if (!isInt(v.laps, 1, 8) || typeof v.versus !== 'boolean' || typeof v.manualGear !== 'boolean') return null;
  if (typeof v.difficulty !== 'string' || !(DIFFICULTIES as readonly string[]).includes(v.difficulty)) return null;
  if (!isInt(v.totalCars, 1, 20) || !isInt(v.delay, MIN_INPUT_DELAY, MAX_INPUT_DELAY)) return null;
  const assists = parseAssists(v.assists);
  if (!assists) return null;
  return {
    trackId: v.trackId, laps: v.laps, versus: v.versus, difficulty: v.difficulty as Difficulty,
    totalCars: v.totalCars, manualGear: v.manualGear, assists, delay: v.delay,
  };
}

export function parseStartConfig(v: unknown, rules: ContentRules): StartConfig | null {
  const base = parseRoomSettings(v, rules);
  if (!base || !isRecord(v) || !isInt(v.seed, 0, 0xffffffff) || !Array.isArray(v.seats)) return null;
  if (v.seats.length < 1 || v.seats.length > MAX_HUMANS) return null;
  const seats: SeatAssignment[] = [];
  const used = new Set<number>();
  for (const s of v.seats) {
    if (!isRecord(s) || !isInt(s.seat, 0, MAX_HUMANS - 1) || !isInt(s.client, 0, 0x7fffffff)) return null;
    const name = cleanName(s.name);
    if (!name || typeof s.car !== 'string' || !rules.cars.includes(s.car) || used.has(s.seat)) return null;
    used.add(s.seat);
    seats.push({ seat: s.seat, client: s.client, name, car: s.car });
  }
  // Nenhum cliente com mais assentos que o limite local.
  const perClient = new Map<number, number>();
  for (const s of seats) perClient.set(s.client, (perClient.get(s.client) ?? 0) + 1);
  for (const n of perClient.values()) if (n > MAX_LOCAL_PLAYERS) return null;
  if (base.totalCars < seats.length) return null;
  return { ...base, seed: v.seed, seats };
}

export function parseSnapshot(v: unknown, rules: ContentRules): Snapshot | null {
  if (!isRecord(v) || !isInt(v.tick, 0, MAX_TICK) || typeof v.state !== 'string' || !Array.isArray(v.ai)) return null;
  const start = parseStartConfig(v.start, rules);
  if (!start) return null;
  const inputs = unpackRecordList(v.inputs);
  if (!inputs) return null;
  if (v.ai.length % 2 !== 0 || v.ai.length > MAX_HUMANS * 2) return null;
  for (let i = 0; i < v.ai.length; i += 2) if (!isInt(v.ai[i], 0, MAX_HUMANS - 1) || !isInt(v.ai[i + 1], 0, MAX_TICK)) return null;
  return { tick: v.tick, state: v.state, start, inputs: packRecords(inputs), ai: v.ai as number[] };
}

/** Como `unpackRecords`, sem o limite por mensagem e aceitando lista vazia (o buffer de um snapshot). */
export function unpackRecordList(d: unknown): InputRecord[] | null {
  if (!Array.isArray(d) || d.length % 4 !== 0 || d.length > 4 * MAX_HUMANS * 1200) return null;
  const out: InputRecord[] = [];
  for (let i = 0; i < d.length; i += 4 * MAX_RECORDS_PER_MESSAGE) {
    const part = unpackRecords(d.slice(i, i + 4 * MAX_RECORDS_PER_MESSAGE));
    if (!part) return null;
    out.push(...part);
  }
  return out;
}

function parseRoomView(v: unknown, rules: ContentRules): RoomView | null {
  if (!isRecord(v) || !isRoomCode(v.code) || !isInt(v.host, -1, 0x7fffffff) || typeof v.started !== 'boolean' || !Array.isArray(v.clients)) return null;
  if (v.clients.length > MAX_CLIENTS) return null;
  const clients: RoomClient[] = [];
  for (const c of v.clients) {
    if (!isRecord(c) || !isInt(c.id, 0, 0x7fffffff) || !isInt(c.seats, 1, MAX_LOCAL_PLAYERS) || typeof c.connected !== 'boolean') return null;
    // Info de outro cliente fora do formato não derruba a sala inteira: ele aparece sem jogadores.
    const info = c.info === null || c.info === undefined ? null : parseClientInfo(c.info, rules);
    clients.push({ id: c.id, seats: c.seats, info, connected: c.connected });
  }
  const settings = v.settings === null || v.settings === undefined ? null : parseRoomSettings(v.settings, rules);
  return { code: v.code, host: v.host, started: v.started, settings, clients };
}

/**
 * Texto recebido do servidor → mensagem validada, ou `null`. Nada daqui para a frente precisa
 * desconfiar da forma; o conteúdo (quem pode mandar o quê) ainda é conferido por quem usa.
 */
export function parseServerMessage(raw: unknown, rules: ContentRules): ServerMessage | null {
  if (typeof raw !== 'string' || raw.length > 256 * 1024) return null;
  let m: unknown;
  try { m = JSON.parse(raw); } catch { return null; }
  if (!isRecord(m) || typeof m.t !== 'string') return null;
  const from = m.from;
  switch (m.t) {
    case 'welcome':
      if (!isRoomCode(m.room) || !isInt(m.id, 0, 0x7fffffff) || typeof m.token !== 'string' || m.token.length < 8 || m.token.length > 64) return null;
      return { t: 'welcome', room: m.room, id: m.id, token: m.token, rejoined: m.rejoined === true };
    case 'room': {
      const room = parseRoomView(m.room, rules);
      return room ? { t: 'room', room } : null;
    }
    case 'peer':
      if (!isInt(m.id, 0, 0x7fffffff) || typeof m.e !== 'string' || !(PEER_EVENTS as readonly string[]).includes(m.e)) return null;
      return { t: 'peer', id: m.id, e: m.e as PeerEvent };
    case 'error':
      if (typeof m.code !== 'string' || !(ERROR_CODES as readonly string[]).includes(m.code)) return null;
      return { t: 'error', code: m.code as ErrorCode };
    case 'pong':
      return isInt(m.n, 0, MAX_TICK) ? { t: 'pong', n: m.n } : null;
    case 'start': {
      const cfg = parseStartConfig(m.cfg, rules);
      return cfg && isInt(from, 0, 0x7fffffff) ? { t: 'start', from, cfg } : null;
    }
    case 'i': {
      const records = unpackRecords(m.d);
      return records && isInt(from, 0, 0x7fffffff) ? { t: 'i', from, records } : null;
    }
    case 'h':
      return isInt(from, 0, 0x7fffffff) && isInt(m.k, 0, MAX_TICK) && isInt(m.h, 0, 0xffffffff) ? { t: 'h', from, k: m.k, h: m.h } : null;
    case 'snap': {
      const snap = parseSnapshot(m.snap, rules);
      return snap && isInt(from, 0, 0x7fffffff) ? { t: 'snap', from, snap } : null;
    }
    default:
      return null;
  }
}
