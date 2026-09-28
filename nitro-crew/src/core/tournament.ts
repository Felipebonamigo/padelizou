// Torneio de sofá (docs/MODOS.md): 2–8 pessoas revezando até 4 controles. Regras puras, sem DOM e
// sem sorteio: rodadas classificatórias em baterias de até `controllers` humanos (+ IA), pontos pela
// posição na corrida (POINTS_TABLE), final com os melhores e desempate em cadeia. O estado é JSON
// puro; a sessão (src/game/party-session.ts) monta cada bateria como uma corrida rápida comum.
import { POINTS_TABLE } from './constants';
import { seatColor } from './data/drivers';
import type { HumanEntry, RaceResultRow } from './types';

export const TOURNAMENT_MIN_PLAYERS = 2;
export const TOURNAMENT_MAX_PLAYERS = 8;
export const TOURNAMENT_MAX_CONTROLLERS = 4;
export const TOURNAMENT_MIN_ROUNDS = 1;
export const TOURNAMENT_MAX_ROUNDS = 3;
/** Quantos vão à final (se houver controles para todos). */
export const TOURNAMENT_FINALISTS = 4;

export interface TournamentPlayer {
  name: string;
  carId: string;
}

export interface TournamentSetup {
  players: TournamentPlayer[];
  /** Controles que circulam (assentos 0..controllers-1): o tamanho máximo de uma bateria. */
  controllers: number;
  /** Rodadas classificatórias: cada pessoa corre uma bateria por rodada. */
  rounds: number;
  /** Pista de cada rodada (na ordem) e, na última posição, a da final: pelo menos rounds + 1 pistas. */
  trackIds: string[];
  laps: number;
}

/** Uma pessoa numa bateria corrida. */
export interface HeatEntry {
  player: number;
  seat: number;
  position: number;
  /** Pontos da classificação (na final ficam só como registro: a final decide pelo lugar). */
  points: number;
  finished: boolean;
  /** Tempo de corrida em ticks; -1 = não terminou. */
  totalTicks: number;
}

export interface HeatRecord {
  round: number;
  heat: number;
  final: boolean;
  trackId: string;
  entries: HeatEntry[];
}

export interface TournamentState {
  setup: TournamentSetup;
  /** Rodada atual (0..rounds-1); `rounds` = a final. */
  round: number;
  /** Bateria atual dentro da rodada. */
  heat: number;
  /** Baterias da rodada atual: índices de jogadores, na ordem dos assentos (o primeiro larga na frente). */
  heats: number[][];
  /** Finalistas em ordem de classificação (vazio até a classificatória acabar). */
  finalists: number[];
  history: HeatRecord[];
  done: boolean;
}

/** Linha da classificação, com os critérios de desempate já calculados. */
export interface TournamentRow {
  player: number;
  name: string;
  points: number;
  heats: number;
  /** Melhor posição numa bateria classificatória (0 = ainda não correu). */
  best: number;
  /** Tempo somado das baterias classificatórias; -1 se alguma não terminou (perde para quem terminou todas). */
  ticks: number;
  /** Posição na final (0 = não correu a final). */
  finalPosition: number;
}

/** Tamanho das baterias de uma rodada: o mínimo de baterias que cabe nos controles, o mais iguais possível. */
export function heatSizes(players: number, controllers: number): number[] {
  const cap = Math.max(1, Math.min(TOURNAMENT_MAX_CONTROLLERS, controllers));
  if (players <= 0) return [];
  const count = Math.ceil(players / cap);
  const base = Math.floor(players / count);
  const extra = players % count;
  return Array.from({ length: count }, (_, i) => base + (i < extra ? 1 : 0));
}

/** Quantos correm a final: os 4 melhores, limitado pelos controles e pelo número de pessoas. Menos de 2 = sem final. */
export function finalistCount(setup: Pick<TournamentSetup, 'players' | 'controllers'>): number {
  const n = Math.min(TOURNAMENT_FINALISTS, setup.players.length, Math.max(1, Math.min(TOURNAMENT_MAX_CONTROLLERS, setup.controllers)));
  return n >= 2 ? n : 0;
}

/** Erro de configuração (chave de texto `party.t.err.*`), ou null se dá para começar. */
export function setupError(setup: TournamentSetup): string | null {
  const n = setup.players.length;
  if (n < TOURNAMENT_MIN_PLAYERS) return 'party.t.err.fewPlayers';
  if (n > TOURNAMENT_MAX_PLAYERS) return 'party.t.err.manyPlayers';
  if (setup.controllers < 1 || setup.controllers > TOURNAMENT_MAX_CONTROLLERS) return 'party.t.err.controllers';
  if (setup.rounds < TOURNAMENT_MIN_ROUNDS || setup.rounds > TOURNAMENT_MAX_ROUNDS) return 'party.t.err.rounds';
  if (setup.trackIds.length < setup.rounds + 1) return 'party.t.err.tracks';
  if (setup.players.some((p) => p.name.trim().length === 0)) return 'party.t.err.emptyName';
  const names = setup.players.map((p) => p.name.trim().toLowerCase());
  if (names.some((name, i) => names.indexOf(name) !== i)) return 'party.t.err.sameName';
  return null;
}

/** Distribui `order` (jogadores já na ordem desejada) em baterias consecutivas. */
function splitHeats(order: number[], controllers: number): number[][] {
  const out: number[][] = [];
  let k = 0;
  for (const size of heatSizes(order.length, controllers)) { out.push(order.slice(k, k + size)); k += size; }
  return out;
}

/**
 * Baterias da rodada: a primeira na ordem de inscrição; as seguintes pela classificação (os
 * melhores correm juntos, como no sistema suíço), para as baterias ficarem mais disputadas.
 */
export function planRound(t: TournamentState, round: number): number[][] {
  const order = round === 0 ? t.setup.players.map((_, i) => i) : tournamentStandings(t).map((r) => r.player);
  return splitHeats(order, t.setup.controllers);
}

export function createTournament(setup: TournamentSetup): TournamentState {
  const err = setupError(setup);
  if (err) throw new Error(`Torneio inválido: ${err}`);
  const t: TournamentState = {
    setup: { ...setup, players: setup.players.map((p) => ({ name: p.name.trim(), carId: p.carId })), trackIds: [...setup.trackIds] },
    round: 0, heat: 0, heats: [], finalists: [], history: [], done: false,
  };
  t.heats = planRound(t, 0);
  return t;
}

export function isFinal(t: TournamentState): boolean {
  return t.round >= t.setup.rounds;
}

export interface HeatInfo {
  round: number;
  heat: number;
  heatCount: number;
  final: boolean;
  trackId: string;
  /** Jogadores na ordem dos assentos (assento = índice). */
  players: number[];
}

/** A próxima bateria a correr; null com o torneio encerrado. */
export function currentHeat(t: TournamentState): HeatInfo | null {
  if (t.done) return null;
  const players = t.heats[t.heat];
  if (!players) return null;
  const final = isFinal(t);
  return {
    round: t.round, heat: t.heat, heatCount: t.heats.length, final,
    trackId: (final ? t.setup.trackIds[t.setup.trackIds.length - 1] : t.setup.trackIds[t.round]) ?? t.setup.trackIds[0],
    players: [...players],
  };
}

/** Humanos da corrida da bateria: cada um por si (time = assento), na cor do controle que segura. */
export function heatHumans(t: TournamentState, info: HeatInfo): HumanEntry[] {
  return info.players.map((p, seat) => ({
    seat, name: t.setup.players[p].name, carId: t.setup.players[p].carId, teamId: seat, color: seatColor(seat),
  }));
}

/**
 * Registra o resultado da bateria atual e avança (próxima bateria, próxima rodada, final ou fim).
 * Idempotente: a mesma bateria não entra duas vezes. Devolve falso se nada foi registrado.
 */
export function recordHeat(t: TournamentState, results: readonly RaceResultRow[]): boolean {
  const info = currentHeat(t);
  if (!info) return false;
  if (t.history.some((h) => h.round === info.round && h.heat === info.heat)) return false;
  const entries: HeatEntry[] = info.players.map((player, seat) => {
    const row = results.find((r) => r.seat === seat);
    const position = row ? row.position : results.length + 1;
    return {
      player, seat, position, points: row ? POINTS_TABLE[position - 1] ?? 0 : 0,
      finished: row ? row.finished : false, totalTicks: row && row.finished ? row.totalTicks : -1,
    };
  });
  t.history.push({ round: info.round, heat: info.heat, final: info.final, trackId: info.trackId, entries });
  t.heat++;
  if (t.heat < t.heats.length) return true;
  if (info.final) { t.done = true; return true; }
  t.round++;
  t.heat = 0;
  if (t.round < t.setup.rounds) { t.heats = planRound(t, t.round); return true; }
  // Fim da classificatória: final com os melhores (a ordem dos assentos é a da classificação).
  const n = finalistCount(t.setup);
  t.finalists = tournamentStandings(t).slice(0, n).map((r) => r.player);
  if (n === 0) { t.heats = []; t.done = true; } else t.heats = [[...t.finalists]];
  return true;
}

/**
 * Desempate da classificatória, nesta ordem: mais pontos → melhor colocação numa bateria → menor
 * tempo somado (quem não terminou alguma bateria fica atrás de quem terminou todas) → ordem de
 * inscrição. Todos correm as mesmas pistas em cada rodada, então o tempo somado é comparável.
 */
export function compareRows(a: TournamentRow, b: TournamentRow): number {
  if (a.points !== b.points) return b.points - a.points;
  const ab = a.best || Number.MAX_SAFE_INTEGER; const bb = b.best || Number.MAX_SAFE_INTEGER;
  if (ab !== bb) return ab - bb;
  const at = a.ticks < 0 ? Number.MAX_SAFE_INTEGER : a.ticks; const bt = b.ticks < 0 ? Number.MAX_SAFE_INTEGER : b.ticks;
  if (at !== bt) return at - bt;
  return a.player - b.player;
}

/** Classificação da fase classificatória (pontos acumulados), com os critérios de desempate. */
export function tournamentStandings(t: TournamentState): TournamentRow[] {
  const rows: TournamentRow[] = t.setup.players.map((p, player) => ({ player, name: p.name, points: 0, heats: 0, best: 0, ticks: 0, finalPosition: 0 }));
  for (const h of t.history) {
    for (const e of h.entries) {
      const row = rows[e.player];
      if (!row) continue;
      if (h.final) { row.finalPosition = e.position; continue; }
      row.points += e.points;
      row.heats++;
      if (row.best === 0 || e.position < row.best) row.best = e.position;
      if (row.ticks >= 0) row.ticks = e.totalTicks >= 0 ? row.ticks + e.totalTicks : -1;
    }
  }
  return rows.sort(compareRows);
}

/**
 * Classificação final: os finalistas pela chegada da final (entre eles), depois o resto pela
 * classificatória. Sem final (menos de 2 controles), vale a classificatória.
 */
export function tournamentRanking(t: TournamentState): TournamentRow[] {
  const standings = tournamentStandings(t);
  const final = t.history.find((h) => h.final);
  if (!final) return standings;
  const finalists = [...final.entries].sort((a, b) => a.position - b.position).map((e) => e.player);
  const byPlayer = (p: number) => standings.find((r) => r.player === p);
  const top = finalists.map(byPlayer).filter((r): r is TournamentRow => r !== undefined);
  return [...top, ...standings.filter((r) => !finalists.includes(r.player))];
}

/** Total de baterias da classificatória (para "bateria 3 de 5"). */
export function qualifyingHeatCount(setup: Pick<TournamentSetup, 'players' | 'controllers' | 'rounds'>): number {
  return heatSizes(setup.players.length, setup.controllers).length * setup.rounds;
}
