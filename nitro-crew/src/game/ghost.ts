// Fantasma do contra-relógio (passo 1.7b): gravação da volta, codificação compacta, reprodução
// sincronizada pelo tempo da volta e diferença ao vivo. Puro: nada de DOM, armazenamento ou relógio
// (a data entra por parâmetro). O armazenamento mora em ghost-store.ts e a ligação com a sessão em
// ghost-session.ts. O fantasma nunca entra no estado da corrida: é lido do estado, não o altera.
//
// Tempo da volta: `elapsed = state.tick - car.lapStartTick`, lido DEPOIS do stepRace. No tick em que o
// carro cruza a linha o núcleo põe lapStartTick = tick e incrementa o tick, então a primeira amostra da
// volta tem elapsed 1 e a última elapsed = lapTicks. Gravação e reprodução usam a mesma conta.
//
// Codificação (string compacta): amostra a cada GHOST_SAMPLE_TICKS, quantizada em inteiros, quatro
// canais em sequência — z (segunda diferença), x, velocidade e bandeiras (volante + nitro), estes três em
// primeira diferença —, cada valor em zigue-zague, zeros seguidos em corrida, tudo em varint de base 64
// (5 bits por caractere + continuação, alfabeto base64url). Erro máximo nas amostras: meio passo de
// quantização (Z_STEP/2, X_STEP/2, SPEED_STEP/2); entre amostras, interpolação linear.
import { TICK_RATE } from '../core/constants';
import { hasUpgrades } from '../core/career';
import type { CarState, HumanEntry, RaceState } from '../core/types';
import type { GhostFrame, GhostPose } from './contracts';

export const GHOST_VERSION = 1;
/**
 * Uma amostra a cada 3 ticks (20 Hz): a interpolação linear entre elas não se nota e a volta de um jogador
 * ziguezagueando na grama (o pior caso medido, 3 min) fica em ~13 KB; a da IA em ~5 KB.
 */
export const GHOST_SAMPLE_TICKS = 3;
/** Passos de quantização: unidades de pista, lateral normalizado e unidades/s. */
export const Z_STEP = 1;
export const X_STEP = 1 / 128;
export const SPEED_STEP = 4;
/** Volta mais longa que vira fantasma (10 min): acima disso o jogador parou; não vale guardar. */
export const MAX_GHOST_TICKS = 10 * 60 * TICK_RATE;
/** Teto da string de uma volta; a volta que passar disso não vira fantasma (o teste confere que nenhuma pista chega perto). */
export const MAX_GHOST_CHARS = 19_000;
/** Teto do arquivo importado (antes de qualquer parse). */
export const MAX_GHOST_FILE_CHARS = 64 * 1024;
export const GHOST_FILE_FORMAT = 'nitro-crew-ghost';

export interface GhostSample {
  z: number;
  x: number;
  speed: number;
  /** -1, 0 ou 1. */
  steer: number;
  nitro: boolean;
}

/** Volta decodificada: a amostra i é o instante elapsed = t0 + i * sampleTicks. */
export interface GhostTrace {
  sampleTicks: number;
  t0: number;
  samples: GhostSample[];
}

/** A volta guardada (e exportada): tempo, quem fez, com que carro, e a string codificada. */
export interface GhostRecord {
  trackId: string;
  /** Tempo da volta em ticks. */
  ticks: number;
  name: string;
  carId: string;
  /** Data ISO da volta. */
  date: string;
  data: string;
}

// ───────────────────────────── Varint base 64 ─────────────────────────────

const ALPHABET = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_';
const DIGITS: number[] = (() => {
  const d = new Array<number>(128).fill(-1);
  for (let i = 0; i < ALPHABET.length; i++) d[ALPHABET.charCodeAt(i)] = i;
  return d;
})();
/** 8 caracteres = 40 bits: sobra para qualquer valor válido; mais que isso é lixo. */
const MAX_VARINT_CHARS = 8;

function zig(v: number): number { return v >= 0 ? v * 2 : -v * 2 - 1; }
function unzig(u: number): number { return u % 2 === 0 ? u / 2 : -(u + 1) / 2; }

/** Aritmética em vez de operadores de bit: os valores passam de 32 bits sem susto. */
function putVarint(out: string[], n: number): void {
  let v = n;
  do {
    let d = v % 32;
    v = Math.floor(v / 32);
    if (v > 0) d += 32;
    out.push(ALPHABET[d]);
  } while (v > 0);
}

class Reader {
  pos = 0;
  constructor(private readonly s: string) {}
  get done(): boolean { return this.pos >= this.s.length; }
  /** Próximo varint, ou -1 se a string acabou no meio ou tem caractere fora do alfabeto. */
  varint(): number {
    let value = 0; let scale = 1;
    for (let k = 0; k < MAX_VARINT_CHARS; k++) {
      if (this.pos >= this.s.length) return -1;
      const code = this.s.charCodeAt(this.pos++);
      const d = code < 128 ? DIGITS[code] : -1;
      if (d < 0) return -1;
      value += (d % 32) * scale;
      if (d < 32) return value;
      scale *= 32;
    }
    return -1;
  }
}

/** Canal: zero isolado é o valor 0; dois ou mais zeros seguidos viram uma corrida (ficha ímpar). */
function putChannel(out: string[], values: readonly number[]): void {
  let i = 0;
  while (i < values.length) {
    if (values[i] === 0) {
      let n = 1;
      while (i + n < values.length && values[i + n] === 0) n++;
      if (n >= 2) { putVarint(out, n * 2 + 1); i += n; continue; }
    }
    putVarint(out, zig(values[i]) * 2);
    i++;
  }
}

function readChannel(r: Reader, count: number): number[] | null {
  const out: number[] = [];
  while (out.length < count) {
    const token = r.varint();
    if (token < 0) return null;
    if (token % 2 === 1) {
      const n = (token - 1) / 2;
      if (n < 2 || out.length + n > count) return null;
      for (let k = 0; k < n; k++) out.push(0);
    } else out.push(unzig(token / 2));
  }
  return out;
}

function diff1(q: readonly number[]): number[] {
  return q.map((v, i) => (i === 0 ? v : v - q[i - 1]));
}

function undiff1(d: readonly number[]): number[] {
  const out: number[] = [];
  for (let i = 0; i < d.length; i++) out.push(i === 0 ? d[0] : out[i - 1] + d[i]);
  return out;
}

function diff2(q: readonly number[]): number[] {
  return q.map((v, i) => (i < 2 ? (i === 0 ? v : v - q[0]) : (v - q[i - 1]) - (q[i - 1] - q[i - 2])));
}

function undiff2(d: readonly number[]): number[] {
  const out: number[] = [];
  let step = 0;
  for (let i = 0; i < d.length; i++) {
    if (i === 0) { out.push(d[0]); continue; }
    step = i === 1 ? d[1] : step + d[i];
    out.push(out[i - 1] + step);
  }
  return out;
}

function flagsOf(s: GhostSample): number { return (Math.max(-1, Math.min(1, Math.round(s.steer))) + 1) + (s.nitro ? 3 : 0); }

// ───────────────────────────── Codificar / decodificar ─────────────────────────────

export function encodeTrace(trace: GhostTrace): string {
  const s = trace.samples;
  const out: string[] = [];
  putVarint(out, GHOST_VERSION);
  putVarint(out, trace.sampleTicks);
  putVarint(out, trace.t0);
  putVarint(out, s.length);
  putChannel(out, diff2(s.map((p) => Math.round(p.z / Z_STEP))));
  putChannel(out, diff1(s.map((p) => Math.round(p.x / X_STEP))));
  putChannel(out, diff1(s.map((p) => Math.round(p.speed / SPEED_STEP))));
  putChannel(out, diff1(s.map(flagsOf)));
  return out.join('');
}

/** A volta codificada, ou null se a string estiver corrompida (nunca lança). */
export function decodeTrace(data: unknown): GhostTrace | null {
  if (typeof data !== 'string' || data.length === 0 || data.length > MAX_GHOST_CHARS) return null;
  const r = new Reader(data);
  const version = r.varint();
  const sampleTicks = r.varint();
  const t0 = r.varint();
  const count = r.varint();
  if (version !== GHOST_VERSION || sampleTicks < 1 || sampleTicks > TICK_RATE || t0 < 0 || t0 > sampleTicks) return null;
  if (count < 1 || count > Math.ceil(MAX_GHOST_TICKS / sampleTicks) + 1) return null;
  const zd = readChannel(r, count); if (!zd) return null;
  const xd = readChannel(r, count); if (!xd) return null;
  const vd = readChannel(r, count); if (!vd) return null;
  const fd = readChannel(r, count); if (!fd) return null;
  if (!r.done) return null;
  const z = undiff2(zd); const x = undiff1(xd); const v = undiff1(vd); const f = undiff1(fd);
  const samples: GhostSample[] = [];
  for (let i = 0; i < count; i++) {
    if (f[i] < 0 || f[i] > 5 || !Number.isFinite(z[i]) || !Number.isFinite(x[i]) || !Number.isFinite(v[i])) return null;
    samples.push({ z: z[i] * Z_STEP, x: x[i] * X_STEP, speed: v[i] * SPEED_STEP, steer: (f[i] % 3) - 1, nitro: f[i] >= 3 });
  }
  return { sampleTicks, t0, samples };
}

// ───────────────────────────── Reprodução ─────────────────────────────

/** Tempo (em ticks de volta) da última amostra. */
export function traceEnd(trace: GhostTrace): number {
  return trace.t0 + (trace.samples.length - 1) * trace.sampleTicks;
}

/**
 * Pose do fantasma `elapsed` ticks depois de ele cruzar a linha, interpolada entre amostras; antes da
 * primeira amostra fica nela, e depois da última (a volta dele acabou) devolve null.
 */
export function ghostPoseAt(trace: GhostTrace, elapsed: number): GhostPose | null {
  const s = trace.samples;
  const f = (elapsed - trace.t0) / trace.sampleTicks;
  if (f <= 0) return poseOf(s[0]);
  const i = Math.floor(f);
  if (i >= s.length - 1) {
    // Depois da última amostra (menos de um passo antes da linha): segue pela velocidade dela.
    const extra = elapsed - traceEnd(trace);
    if (extra > trace.sampleTicks) return null;
    const last = poseOf(s[s.length - 1]);
    last.z += (last.speed * extra) / TICK_RATE;
    return last;
  }
  const a = s[i]; const b = s[i + 1]; const k = f - i;
  return {
    z: a.z + (b.z - a.z) * k, x: a.x + (b.x - a.x) * k, speed: a.speed + (b.speed - a.speed) * k,
    steerPose: k < 0.5 ? a.steer : b.steer, nitro: k < 0.5 ? a.nitro : b.nitro,
  };
}

function poseOf(p: GhostSample): GhostPose {
  return { z: p.z, x: p.x, speed: p.speed, steerPose: p.steer, nitro: p.nitro };
}

/**
 * Em que tick da volta o fantasma passou por `z` (fracionário). O z da volta só cresce (velocidade ≥ 0),
 * então é busca binária; antes da primeira ou depois da última amostra, extrapola pela velocidade.
 */
export function ghostTicksAtZ(trace: GhostTrace, z: number): number {
  const s = trace.samples;
  const perTick = (p: GhostSample) => Math.max(1, p.speed) / TICK_RATE;
  if (z <= s[0].z) return Math.max(0, trace.t0 - (s[0].z - z) / perTick(s[0]));
  const last = s[s.length - 1];
  if (z >= last.z) return traceEnd(trace) + (z - last.z) / perTick(last);
  let lo = 0; let hi = s.length - 1; // s[lo].z < z ≤ s[hi].z
  while (hi - lo > 1) {
    const mid = (lo + hi) >> 1;
    if (s[mid].z < z) lo = mid; else hi = mid;
  }
  const span = s[hi].z - s[lo].z;
  const k = span > 0 ? (z - s[lo].z) / span : 0;
  return trace.t0 + (lo + k) * trace.sampleTicks;
}

/** Diferença ao vivo em segundos: quanto o carro (em `elapsed`, na posição `z`) está atrás (+) ou à frente (−) do fantasma. */
export function liveDelta(trace: GhostTrace, elapsed: number, z: number): number {
  return (elapsed - ghostTicksAtZ(trace, z)) / TICK_RATE;
}

/** "+0,42" / "−0,15" (sinal de menos tipográfico); `decimal` = separador do idioma. */
export function formatGhostDelta(seconds: number, decimal = ','): string {
  const cs = Math.round(Math.abs(seconds) * 100);
  const sign = seconds < 0 && cs > 0 ? '−' : '+';
  return `${sign}${Math.floor(cs / 100)}${decimal}${String(cs % 100).padStart(2, '0')}`;
}

// ───────────────────────────── Gravação ─────────────────────────────

/** Grava a volta em andamento de um carro, tick a tick (amostras cruas; a redução vem no fechamento). */
export interface GhostRecorder {
  seat: number;
  lapStart: number;
  lapsDone: number;
  /** Gravando: a volta começou com o carro ainda correndo (não depois da bandeirada). */
  active: boolean;
  samples: GhostSample[];
}

export function createRecorder(seat: number): GhostRecorder {
  return { seat, lapStart: -1, lapsDone: 0, active: false, samples: [] };
}

/**
 * Um tick (chamar depois de cada stepRace). Devolve a volta completa quando o carro acabou de fechar
 * uma volta inteira gravada tick a tick; senão null. Voltas depois da bandeirada (piloto automático)
 * e voltas com tick faltando não contam.
 */
export function recordTick(rec: GhostRecorder, state: RaceState, car: CarState): { ticks: number; trace: GhostTrace } | null {
  let done: { ticks: number; trace: GhostTrace } | null = null;
  if (car.lapStartTick !== rec.lapStart) {
    if (rec.active && car.lapTicks.length === rec.lapsDone + 1) {
      const ticks = car.lapTicks[car.lapTicks.length - 1];
      if (rec.samples.length === ticks && ticks <= MAX_GHOST_TICKS) done = { ticks, trace: downsample(rec.samples) };
    }
    rec.lapStart = car.lapStartTick;
    rec.lapsDone = car.lapTicks.length;
    rec.samples = [];
    rec.active = car.lap >= 1 && !car.finished && state.phase === 'racing';
  }
  if (rec.active) {
    if (rec.samples.length <= MAX_GHOST_TICKS) rec.samples.push({ z: car.z, x: car.x, speed: car.speed, steer: car.steerPose, nitro: car.nitroTicks > 0 });
  }
  return done;
}

/** Amostras por tick (elapsed 1, 2, 3…) → uma a cada GHOST_SAMPLE_TICKS, começando na primeira. */
export function downsample(perTick: readonly GhostSample[]): GhostTrace {
  const samples: GhostSample[] = [];
  for (let i = 0; i < perTick.length; i += GHOST_SAMPLE_TICKS) samples.push(perTick[i]);
  return { sampleTicks: GHOST_SAMPLE_TICKS, t0: 1, samples };
}

// ───────────────────────────── Registro e arquivo ─────────────────────────────

const ID_RE = /^[a-z0-9_-]{1,64}$/i;

function str(v: unknown, max: number): string | null {
  return typeof v === 'string' && v.trim().length > 0 ? v.trim().slice(0, max) : null;
}

/** A volta decodificada tem que cobrir o tempo declarado (a última amostra cai no último passo). */
function traceMatches(trace: GhostTrace, ticks: number): boolean {
  const end = traceEnd(trace);
  return end <= ticks && ticks - end < trace.sampleTicks;
}

/** Registro válido (campos e string que decodifica e bate com o tempo), ou null. Nunca lança. */
export function sanitizeGhostRecord(raw: unknown): GhostRecord | null {
  if (typeof raw !== 'object' || raw === null || Array.isArray(raw)) return null;
  const r = raw as Record<string, unknown>;
  const trackId = typeof r.trackId === 'string' && ID_RE.test(r.trackId) ? r.trackId : null;
  const carId = typeof r.carId === 'string' && ID_RE.test(r.carId) ? r.carId : null;
  const ticks = typeof r.ticks === 'number' && Number.isInteger(r.ticks) && r.ticks > 0 && r.ticks <= MAX_GHOST_TICKS ? r.ticks : 0;
  const name = str(r.name, 24);
  const date = str(r.date, 40) ?? '';
  if (!trackId || !carId || !ticks || !name) return null;
  const trace = decodeTrace(r.data);
  if (!trace || !traceMatches(trace, ticks)) return null;
  return { trackId, ticks, name, carId, date, data: r.data as string };
}

export function makeGhostRecord(trackId: string, ticks: number, trace: GhostTrace, who: { name: string; carId: string }, date: string): GhostRecord | null {
  const data = encodeTrace(trace);
  if (data.length > MAX_GHOST_CHARS) return null;
  return { trackId, ticks, name: who.name.trim().slice(0, 24) || '?', carId: who.carId, date, data };
}

/** Texto do arquivo exportado (desafiar um amigo). */
export function ghostFileText(rec: GhostRecord): string {
  return JSON.stringify({ format: GHOST_FILE_FORMAT, v: GHOST_VERSION, ...rec });
}

/** Lê um arquivo de fantasma; null se não for um (grande demais, JSON inválido, formato errado, dados corrompidos). */
export function parseGhostFile(text: unknown): GhostRecord | null {
  if (typeof text !== 'string' || text.length > MAX_GHOST_FILE_CHARS) return null;
  let raw: unknown;
  try { raw = JSON.parse(text); } catch { return null; }
  if (typeof raw !== 'object' || raw === null) return null;
  const o = raw as Record<string, unknown>;
  if (o.format !== GHOST_FILE_FORMAT || o.v !== GHOST_VERSION) return null;
  return sanitizeGhostRecord(o);
}

// ───────────────────────────── A corrida contra o fantasma ─────────────────────────────

/** Fechamento de volta de um humano: diferença para o fantasma daquela volta e, se melhorou, o novo fantasma. */
export interface GhostLapEvent {
  seat: number;
  lapTicks: number;
  /** Tempo da volta menos o do fantasma que correu com ela (negativo = mais rápido); null sem fantasma. */
  deltaTicks: number | null;
  newBest: GhostRecord | null;
}

export interface GhostRun {
  /** Chamar depois de cada tick da corrida. */
  afterTick(state: RaceState): GhostLapEvent[];
  /** O fantasma no referencial do primeiro humano (carro, pose e diferença ao vivo). */
  frame(state: RaceState): GhostFrame | null;
  /** O fantasma em uso (o carregado, ou a melhor volta desta sessão). */
  current(): GhostRecord | null;
}

/**
 * Fantasma de uma corrida de contra-relógio: reproduz `best` (se houver) e grava as voltas dos humanos
 * de carro de fábrica (como os recordes do save). Volta mais rápida que o fantasma vira o fantasma na
 * hora — a volta seguinte já é contra ela. `now` dá a data da volta.
 */
export function createGhostRun(trackId: string, humans: readonly HumanEntry[], best: GhostRecord | null, now: () => string): GhostRun {
  let record = best && best.trackId === trackId ? best : null;
  let trace = record ? decodeTrace(record.data) : null;
  if (!trace) record = null;
  const recorders = humans.filter((h) => !hasUpgrades(h.upgrades)).map((h) => createRecorder(h.seat));
  const lead = humans.slice().sort((a, b) => a.seat - b.seat)[0];

  function afterTick(state: RaceState): GhostLapEvent[] {
    const events: GhostLapEvent[] = [];
    for (const rec of recorders) {
      const car = state.cars.find((c) => c.seat === rec.seat);
      if (!car) continue;
      const lap = recordTick(rec, state, car);
      if (!lap) continue;
      const deltaTicks = record ? lap.ticks - record.ticks : null;
      let newBest: GhostRecord | null = null;
      if (!record || lap.ticks < record.ticks) {
        const h = humans.find((x) => x.seat === rec.seat);
        newBest = makeGhostRecord(trackId, lap.ticks, lap.trace, { name: h?.name ?? car.name, carId: h?.carId ?? car.carId }, now());
        if (newBest) { record = newBest; trace = lap.trace; }
      }
      events.push({ seat: rec.seat, lapTicks: lap.ticks, deltaTicks, newBest });
    }
    return events;
  }

  function frame(state: RaceState): GhostFrame | null {
    if (!record || !trace || !lead) return null;
    const car = state.cars.find((c) => c.seat === lead.seat);
    const racing = car && car.lap >= 1 && !car.finished && state.phase === 'racing';
    if (!car || !racing) return { pose: null, carId: record.carId, delta: null };
    const elapsed = state.tick - car.lapStartTick;
    return { pose: ghostPoseAt(trace, elapsed), carId: record.carId, delta: liveDelta(trace, elapsed, car.z) };
  }

  return { afterTick, frame, current: () => record };
}
