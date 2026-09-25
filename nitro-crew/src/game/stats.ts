// Estatísticas por jogador (pelo nome digitado no lobby) e o total de todos os jogadores,
// acumuladas no save ao fim de cada corrida a partir da telemetria que a sessão junta a cada tick
// (achievements.ts: observeTick). Tudo puro e testável em Node.
//
// Sem importar nada de src/game em tempo de execução (só tipos): contracts.ts importa EMPTY_STATS
// daqui para o DEFAULT_SAVE, e um ciclo contracts → stats → settings → contracts dependeria da
// ordem de avaliação dos módulos. Por isso os dois saneadores pequenos abaixo são locais.
import { isCoop } from '../core/championship';
import { MAX_CARS, SPEED_TO_KMH, TICK_RATE } from '../core/constants';
import type { HumanEntry, RaceResultRow, RaceState } from '../core/types';
import { getLanguage, t } from '../i18n';
import '../stats/strings';
import type { RaceTelemetry } from './achievements';
import type { RaceMode } from './contracts';

/** Perfis guardados: os nomes que correram mais recentemente. O total não perde nada. */
export const MAX_PROFILES = 32;
/** Mesmo limite do nome no lobby (NAME_MAX_LENGTH do save.ts; tests/stats.test.ts confere). */
export const PROFILE_NAME_MAX = 12;
/** Limite de pistas por perfil no save (proteção contra save adulterado; o jogo tem bem menos). */
const MAX_TRACK_KEYS = 256;
const TRACK_KEY_MAX = 64;

/** Metros por unidade de mundo pela escala do velocímetro: 6000 u/s = 300 km/h → 1 u = 1/72 m. */
export const METERS_PER_UNIT = (SPEED_TO_KMH * 1000) / 3600;

export interface PlayerStats {
  races: number;
  /** Vitórias fora do contra-relógio (lá a posição é sempre 1). */
  wins: number;
  podiums: number;
  /** Voltas completadas até a chegada (as do piloto automático depois dela não contam). */
  laps: number;
  /** Distância percorrida do grid até a chegada (ou até o fim da corrida), em metros. */
  meters: number;
  nitros: number;
  towsGiven: number;
  towsReceived: number;
  /** Contatos carro-carro (batida por trás ou raspão lado a lado), vistos pelo estado a cada tick. */
  collisions: number;
  /** Batidas no cenário (árvore, placa, muro). */
  crashes: number;
  pitStops: number;
  /** Tempo de corrida em ticks: até a chegada, ou até o fim da corrida para quem não terminou. */
  raceTicks: number;
  /** Vitórias próprias em corridas co-op (2+ humanos na mesma equipe). */
  coopWins: number;
  /** Melhor posição por pista (id → 1..MAX_CARS), fora do contra-relógio. */
  bestPositions: Record<string, number>;
}

export interface PlayerProfile extends PlayerStats {
  name: string;
}

export interface StatsData {
  /** Soma de todos os jogadores, inclusive os que já saíram da lista pelo limite de perfis. */
  totals: PlayerStats;
  /** Um perfil por nome (sem diferenciar maiúsculas), do mais recente para o mais antigo. */
  players: PlayerProfile[];
}

export type CounterKey = Exclude<keyof PlayerStats, 'bestPositions'>;

/** Contadores na ordem em que a tela de recordes mostra. */
export const COUNTER_KEYS: readonly CounterKey[] = [
  'races', 'wins', 'podiums', 'coopWins', 'laps', 'meters', 'raceTicks',
  'nitros', 'towsGiven', 'towsReceived', 'collisions', 'crashes', 'pitStops',
];

export function emptyPlayerStats(): PlayerStats {
  return {
    races: 0, wins: 0, podiums: 0, laps: 0, meters: 0, nitros: 0, towsGiven: 0, towsReceived: 0,
    collisions: 0, crashes: 0, pitStops: 0, raceTicks: 0, coopWins: 0, bestPositions: {},
  };
}

export function emptyStats(): StatsData {
  return { totals: emptyPlayerStats(), players: [] };
}

/** Padrão congelado do DEFAULT_SAVE: quem tentar acumular nele (em vez de num save saneado) estoura. */
export const EMPTY_STATS: StatsData = (() => {
  const s = emptyStats();
  Object.freeze(s.totals.bestPositions);
  Object.freeze(s.totals);
  Object.freeze(s.players);
  return Object.freeze(s);
})();

// ───────────────────────────── Nomes ─────────────────────────────

/** Nome como fica no perfil: sem espaços nas pontas nem repetidos, no limite do lobby. */
export function cleanName(name: string): string {
  return name.trim().replace(/\s+/g, ' ').slice(0, PROFILE_NAME_MAX);
}

/** Chave do perfil: "Ana", "ana" e " ANA " são a mesma pessoa. */
export function profileKey(name: string): string {
  return cleanName(name).toLowerCase();
}

// ───────────────────────────── Corrida → estatísticas ─────────────────────────────

export interface RaceStatsInput {
  mode: RaceMode;
  state: RaceState;
  results: RaceResultRow[];
  humans: HumanEntry[];
  telemetry: RaceTelemetry;
}

export interface Contribution {
  seat: number;
  name: string;
  stats: PlayerStats;
}

/** O que cada humano soma nesta corrida (puro; não mexe no save). */
export function raceContributions(input: RaceStatsInput): Contribution[] {
  const { mode, state, results, humans, telemetry } = input;
  const timeTrial = mode === 'timetrial' || state.config.timeTrial === true;
  const coop = isCoop(humans);
  const laps = state.config.laps;
  const finishLine = laps * state.trackLength;
  // stepRace incrementa o tick depois de fechar a corrida: o último tick disputado é o anterior.
  const endTick = Math.max(state.startTick, state.phase === 'finished' ? state.tick - 1 : state.tick);
  const out: Contribution[] = [];
  for (const h of [...humans].sort((a, b) => a.seat - b.seat)) {
    const car = state.cars.find((c) => c.seat === h.seat);
    const row = results.find((r) => r.seat === h.seat);
    if (!car || !row) continue;
    const tel = telemetry.seats.get(h.seat);
    const s = emptyPlayerStats();
    s.races = 1;
    if (!timeTrial) {
      if (row.position === 1) s.wins = 1;
      if (row.position <= 3) s.podiums = 1;
      if (row.position === 1 && coop) s.coopWins = 1;
      s.bestPositions[state.trackId] = row.position;
    }
    s.laps = Math.min(car.lapTicks.length, laps);
    // Progresso limitado à linha de chegada: quem terminou segue no piloto automático e isso não conta.
    const start = tel ? tel.startProgress : car.progress;
    s.meters = Math.round(Math.max(0, Math.min(car.progress, finishLine) - start) * METERS_PER_UNIT);
    s.raceTicks = row.finished ? Math.max(0, row.totalTicks) : endTick - state.startTick;
    if (tel) {
      s.nitros = tel.nitros;
      s.towsGiven = tel.towsGiven;
      s.towsReceived = tel.towsReceived;
      s.collisions = tel.collisions;
      s.crashes = tel.crashes;
      s.pitStops = tel.pitStops;
    }
    out.push({ seat: h.seat, name: h.name, stats: s });
  }
  return out;
}

/** Soma `add` em `into`: contadores somam; melhor posição fica com a menor. */
export function addStats(into: PlayerStats, add: PlayerStats): void {
  for (const k of COUNTER_KEYS) into[k] = Math.min(Number.MAX_SAFE_INTEGER, into[k] + add[k]);
  for (const [trackId, pos] of Object.entries(add.bestPositions)) {
    const prev = into.bestPositions[trackId];
    if (prev === undefined || pos < prev) into.bestPositions[trackId] = pos;
  }
}

/** Soma no perfil do nome (criando se preciso) e o põe no topo da lista (o mais recente). */
function touchProfile(stats: StatsData, name: string, add: PlayerStats): void {
  const clean = cleanName(name);
  if (!clean) return;
  const key = profileKey(clean);
  const i = stats.players.findIndex((p) => profileKey(p.name) === key);
  const profile: PlayerProfile = i >= 0 ? stats.players.splice(i, 1)[0] : { name: clean, ...emptyPlayerStats() };
  profile.name = clean; // a grafia mais recente vence ("ana" → "Ana")
  addStats(profile, add);
  stats.players.unshift(profile);
}

/**
 * Acumula a corrida no save: soma no total e no perfil de cada jogador, e corta a lista nos
 * MAX_PROFILES nomes mais recentes. Dois assentos com o mesmo nome somam no mesmo perfil.
 */
export function recordRaceStats(stats: StatsData, input: RaceStatsInput): Contribution[] {
  const list = raceContributions(input);
  for (const c of list) addStats(stats.totals, c.stats);
  // De trás para a frente: o P1 termina no topo da lista.
  for (let i = list.length - 1; i >= 0; i--) touchProfile(stats, list[i].name, list[i].stats);
  if (stats.players.length > MAX_PROFILES) stats.players.length = MAX_PROFILES;
  return list;
}

/** Pistas com resultado registrado que ainda existem no jogo. */
export function tracksRaced(s: PlayerStats, trackIds: readonly string[]): number {
  return trackIds.filter((id) => s.bestPositions[id] !== undefined).length;
}

// ───────────────────────────── Saneamento ─────────────────────────────

function isObj(v: unknown): v is Record<string, unknown> {
  return typeof v === 'object' && v !== null && !Array.isArray(v);
}

/** Contador: inteiro não negativo; lixo vira 0. */
function counter(v: unknown): number {
  if (typeof v !== 'number' || !Number.isFinite(v) || v <= 0) return 0;
  return Math.min(Number.MAX_SAFE_INTEGER, Math.round(v));
}

function positions(v: unknown): Record<string, number> {
  const out: Record<string, number> = {};
  if (!isObj(v)) return out;
  let n = 0;
  for (const [key, pos] of Object.entries(v)) {
    if (n >= MAX_TRACK_KEYS) break;
    if (key.length === 0 || key.length > TRACK_KEY_MAX || key === '__proto__') continue;
    if (typeof pos !== 'number' || !Number.isFinite(pos)) continue;
    const p = Math.round(pos);
    if (p < 1 || p > MAX_CARS) continue;
    out[key] = p;
    n++;
  }
  return out;
}

export function sanitizePlayerStats(v: unknown): PlayerStats {
  const r = isObj(v) ? v : {};
  const s = emptyPlayerStats();
  for (const k of COUNTER_KEYS) s[k] = counter(r[k]);
  s.bestPositions = positions(r.bestPositions);
  return s;
}

/** Funde `raw` com o padrão, descarta perfis inválidos ou repetidos e aplica o limite. Nunca lança. */
export function sanitizeStats(raw: unknown): StatsData {
  const r = isObj(raw) ? raw : {};
  const out: StatsData = { totals: sanitizePlayerStats(r.totals), players: [] };
  const seen = new Set<string>();
  for (const item of Array.isArray(r.players) ? r.players : []) {
    if (out.players.length >= MAX_PROFILES) break;
    if (!isObj(item) || typeof item.name !== 'string') continue;
    const name = cleanName(item.name);
    const key = profileKey(name);
    if (!name || seen.has(key)) continue; // repetido: fica o primeiro (o mais recente)
    seen.add(key);
    out.players.push({ name, ...sanitizePlayerStats(item) });
  }
  return out;
}

// ───────────────────────────── Formatação ─────────────────────────────

/**
 * "1.234,5 km" / "1,234.5 km": uma casa abaixo de 100 km, inteiro acima. Trunca em vez de
 * arredondar: 999.600 m é "999 km", nunca "1.000 km" com a MARATONA ainda bloqueada.
 */
export function formatDistance(meters: number, lang: 'pt' | 'en'): string {
  const m = Math.max(0, Math.floor(meters));
  const digits = m < 100_000 ? 1 : 0;
  // Divisão inteira antes de voltar a km: sem erro de ponto flutuante no corte.
  const km = digits === 1 ? Math.floor(m / 100) / 10 : Math.floor(m / 1000);
  const n = new Intl.NumberFormat(lang === 'pt' ? 'pt-BR' : 'en-US', { minimumFractionDigits: digits, maximumFractionDigits: digits }).format(km);
  return `${n} km`;
}

/** Inteiro com o separador de milhar do idioma atual. */
export function formatCount(n: number): string {
  return new Intl.NumberFormat(getLanguage() === 'pt' ? 'pt-BR' : 'en-US').format(n);
}

/** "1 corrida" / "2 corridas": singular só para 1 (PT e EN), com o separador de milhar. */
export function countText(kind: 'races' | 'wins' | 'cups', n: number): string {
  return t(`stats.count.${kind}.${n === 1 ? 'one' : 'other'}`, { n: formatCount(n) });
}

/** Linha de um jogador na lista da tela de recordes. */
export function playerLine(s: Pick<PlayerStats, 'races' | 'wins'>): string {
  return `${countText('races', s.races)} · ${countText('wins', s.wins)}`;
}

/** Cabeçalho da tela de recordes: corridas, vitórias e copas do save (uma por corrida, não por jogador). */
export function recordsLine(run: number, won: number, cups: number): string {
  return `${countText('races', run)} · ${countText('wins', won)} · ${countText('cups', cups)}`;
}

/** Tempo de corrida acumulado: "0:42", "12:05", "3:07:45" (h:mm:ss a partir de uma hora). */
export function formatDuration(ticks: number): string {
  const total = Math.floor(Math.max(0, ticks) / TICK_RATE);
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = total % 60;
  const ss = String(s).padStart(2, '0');
  return h > 0 ? `${h}:${String(m).padStart(2, '0')}:${ss}` : `${m}:${ss}`;
}
