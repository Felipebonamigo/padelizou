// Save da carreira e do campeonato normal em andamento (1.7a): saneamento na leitura (lixo vira
// ausente, campo ruim é consertado, nunca lança) e os ajudantes que a sessão usa para gravar.
// O save.ts chama os sanitize* daqui; as regras de jogo moram em src/core/career.ts.
import {
  CAREER_VERSION, MONEY_MAX, NO_UPGRADES, ownsCar, UPGRADE_PARTS,
  type CareerDriver, type CareerGarage, type CareerPrizeRow, type CareerReport, type CareerState,
} from '../core/career';
import { MAX_SEATS, UPGRADE_MAX_LEVEL } from '../core/constants';
import { CARS } from '../core/data/cars';
import { CUPS } from '../core/data/cups';
import { AI_TEAM_ID_BASE, SEAT_COLORS } from '../core/data/drivers';
import type { ChampionshipState, HumanEntry, RaceResultRow, StandingRow, TeamStandingRow, UpgradeLevels } from '../core/types';
import type { SaveData, SavedCup } from './contracts';
import { isRecord, pickNumber, pickString } from './settings';

/** Mesmo teto de nome do save.ts (importar de lá faria um ciclo save ↔ career-save). */
const NAME_MAX = 12;
const UINT32_MAX = 0xffffffff;

// ───────────────────────────── Primitivos ─────────────────────────────

function int(v: unknown, min: number, max: number, fallback: number): number {
  return pickNumber(v, min, max, fallback, true);
}

/** Inteiro exigido: sem ele o registro inteiro é descartado. */
function strictInt(v: unknown, min: number, max: number): number | null {
  return typeof v === 'number' && Number.isInteger(v) && v >= min && v <= max ? v : null;
}

function name(v: unknown, fallback: string): string {
  return typeof v === 'string' && v.trim().length > 0 ? v.trim().slice(0, NAME_MAX) : fallback;
}

const knownCar = (id: unknown): id is string => typeof id === 'string' && CARS.some((c) => c.id === id);
const pricedCar = (id: unknown): id is string => typeof id === 'string' && CARS.some((c) => c.id === id && c.price > 0);

function uniqueStrings(v: unknown, keep: (id: unknown) => id is string): string[] {
  if (!Array.isArray(v)) return [];
  const out: string[] = [];
  for (const x of v) if (keep(x) && !out.includes(x)) out.push(x);
  return out;
}

/** Carros comprados na carreira (liberados em todo modo): só ids de carro à venda, sem repetição. */
export function sanitizeUnlocked(v: unknown): string[] {
  return uniqueStrings(v, pricedCar);
}

// ───────────────────────────── Copa (ChampionshipState) ─────────────────────────────

function sanitizeResultRow(v: unknown): RaceResultRow | null {
  if (!isRecord(v)) return null;
  const carId = strictInt(v.carId, 0, 999); const seat = strictInt(v.seat, -1, MAX_SEATS - 1);
  const teamId = strictInt(v.teamId, 0, 9999); const position = strictInt(v.position, 1, 999);
  if (carId === null || seat === null || teamId === null || position === null || typeof v.name !== 'string' || !knownCar(v.carDefId)) return null;
  return {
    carId, seat, name: v.name.slice(0, 32), teamId, carDefId: v.carDefId, position, finished: v.finished === true,
    totalTicks: int(v.totalTicks, -1, Number.MAX_SAFE_INTEGER, -1), bestLapTicks: int(v.bestLapTicks, -1, Number.MAX_SAFE_INTEGER, -1),
    points: int(v.points, 0, 999, 0),
  };
}

function sanitizeStanding(v: unknown, raceCount: number): StandingRow | null {
  if (!isRecord(v) || typeof v.key !== 'string' || typeof v.name !== 'string') return null;
  const seat = strictInt(v.seat, -1, MAX_SEATS - 1); const teamId = strictInt(v.teamId, 0, 9999);
  if (seat === null || teamId === null) return null;
  const raw = Array.isArray(v.positions) ? v.positions : [];
  return {
    key: v.key.slice(0, 40), name: v.name.slice(0, 32), seat, teamId,
    points: int(v.points, 0, 99_999, 0), wins: int(v.wins, 0, 999, 0),
    positions: Array.from({ length: raceCount }, (_, i) => int(raw[i], 0, 999, 0)),
  };
}

function sanitizeTeam(v: unknown): TeamStandingRow | null {
  if (!isRecord(v) || typeof v.name !== 'string') return null;
  const teamId = strictInt(v.teamId, 0, 9999);
  if (teamId === null) return null;
  return { teamId, name: v.name.slice(0, 32), points: int(v.points, 0, 99_999, 0), isHuman: v.isHuman === true || teamId < AI_TEAM_ID_BASE };
}

function list<T>(v: unknown, fn: (x: unknown) => T | null): T[] {
  if (!Array.isArray(v)) return [];
  const out: T[] = [];
  for (const x of v) { const y = fn(x); if (y) out.push(y); }
  return out;
}

/** Copa salva; null quando a copa não existe mais ou o índice da corrida é impossível. */
export function sanitizeChampionship(v: unknown): ChampionshipState | null {
  if (!isRecord(v)) return null;
  const cup = CUPS.find((c) => c.id === v.cupId);
  if (!cup) return null;
  const raceCount = cup.trackIds.length;
  const raceIndex = strictInt(v.raceIndex, 0, raceCount);
  if (raceIndex === null) return null;
  const lastRace = Array.isArray(v.lastRace) ? list(v.lastRace, sanitizeResultRow) : null;
  return {
    cupId: cup.id, raceIndex,
    standings: list(v.standings, (x) => sanitizeStanding(x, raceCount)),
    teams: list(v.teams, sanitizeTeam),
    coop: v.coop === true, eliminated: v.eliminated === true, completed: v.completed === true,
    lastRace: lastRace && lastRace.length > 0 ? lastRace : null,
    lastVerdict: v.lastVerdict === 'qualified' || v.lastVerdict === 'eliminated' ? v.lastVerdict : null,
  };
}

// ───────────────────────────── Carreira ─────────────────────────────

function sanitizeLevels(v: unknown): UpgradeLevels | null {
  if (!isRecord(v)) return null;
  const out = { ...NO_UPGRADES };
  for (const p of UPGRADE_PARTS) out[p] = int(v[p], 0, UPGRADE_MAX_LEVEL, 0);
  return out;
}

function sanitizeGarage(v: unknown): CareerGarage {
  const r = isRecord(v) ? v : {};
  const garage: CareerGarage = { carId: '', owned: uniqueStrings(r.owned, pricedCar), upgrades: {} };
  if (isRecord(r.upgrades)) {
    for (const [id, lv] of Object.entries(r.upgrades)) {
      if (!ownsCar(garage, id)) continue;
      const levels = sanitizeLevels(lv);
      if (levels) garage.upgrades[id] = levels;
    }
  }
  const fallback = CARS.find((c) => c.price === 0)?.id ?? CARS[0].id;
  garage.carId = typeof r.carId === 'string' && ownsCar(garage, r.carId) ? r.carId : fallback;
  return garage;
}

function sanitizeDriver(v: Record<string, unknown>, i: number): CareerDriver {
  return { name: name(v.name, `P${i + 1}`), garage: sanitizeGarage(v.garage), earnings: int(v.earnings, 0, MONEY_MAX, 0) };
}

function sanitizePrizeRow(v: unknown): CareerPrizeRow | null {
  if (!isRecord(v)) return null;
  const driver = strictInt(v.driver, 0, MAX_SEATS - 1);
  if (driver === null) return null;
  return { driver, position: int(v.position, 0, 999, 0), prize: int(v.prize, 0, MONEY_MAX, 0) };
}

function sanitizeReport(v: unknown): CareerReport | null {
  if (!isRecord(v) || typeof v.cupId !== 'string' || typeof v.trackId !== 'string') return null;
  if (v.verdict !== 'qualified' && v.verdict !== 'eliminated') return null;
  return {
    cupId: v.cupId, trackId: v.trackId, raceIndex: int(v.raceIndex, 0, 99, 0), rows: list(v.rows, sanitizePrizeRow),
    teamBonus: int(v.teamBonus, 0, MONEY_MAX, 0), teamRank: int(v.teamRank, 0, 99, 0), verdict: v.verdict,
    cupCompleted: v.cupCompleted === true, careerCompleted: v.careerCompleted === true,
  };
}

/** Carreira salva; null quando não há piloto aproveitável. Campo ruim é consertado, não derruba o resto. */
export function sanitizeCareer(v: unknown): CareerState | null {
  if (!isRecord(v) || !Array.isArray(v.drivers)) return null;
  const drivers = v.drivers.filter(isRecord).slice(0, MAX_SEATS).map(sanitizeDriver);
  if (drivers.length === 0) return null;
  const coop = v.coop === true && drivers.length >= 2;
  const rawWallets = Array.isArray(v.wallets) ? v.wallets : [];
  const wallets = Array.from({ length: coop ? 1 : drivers.length }, (_, i) => int(rawWallets[i], 0, MONEY_MAX, 0));
  const cupKnown = typeof v.cupId === 'string' && CUPS.some((c) => c.id === v.cupId);
  const cupId = cupKnown ? (v.cupId as string) : CUPS[0].id;
  let champ = sanitizeChampionship(v.champ);
  // Copa salva que não bate com a atual, ou já encerrada, recomeça do zero.
  if (champ && (champ.cupId !== cupId || champ.eliminated || champ.completed)) champ = null;
  return {
    version: CAREER_VERSION, coop, drivers, wallets, cupId, champ,
    rosterSeed: int(v.rosterSeed, 0, UINT32_MAX, 0), attempts: int(v.attempts, 1, 999_999, 1),
    completed: v.completed === true, racesRun: int(v.racesRun, 0, Number.MAX_SAFE_INTEGER, 0),
    lastReport: sanitizeReport(v.lastReport),
  };
}

// ───────────────────────────── Campeonato normal salvo (1.7a) ─────────────────────────────

function sanitizeHuman(v: unknown): HumanEntry | null {
  if (!isRecord(v)) return null;
  const seat = strictInt(v.seat, 0, MAX_SEATS - 1); const teamId = strictInt(v.teamId, 0, AI_TEAM_ID_BASE - 1);
  if (seat === null || teamId === null || !knownCar(v.carId)) return null;
  return { seat, name: name(v.name, `P${seat + 1}`), carId: v.carId, teamId, color: pickString(v.color, SEAT_COLORS[seat] ?? '#ffffff', 16) };
}

/** Copa normal salva; null se algo essencial não fecha (copa, semente, humanos com assentos 0..n-1). */
export function sanitizeSavedCup(v: unknown): SavedCup | null {
  if (!isRecord(v)) return null;
  const champ = sanitizeChampionship(v.champ);
  const cupSeed = strictInt(v.cupSeed, 0, UINT32_MAX);
  if (!champ || champ.completed || champ.eliminated || cupSeed === null) return null;
  if (!Array.isArray(v.humans) || v.humans.length === 0 || v.humans.length > MAX_SEATS) return null;
  const humans: HumanEntry[] = [];
  for (const x of v.humans) { const h = sanitizeHuman(x); if (!h) return null; humans.push(h); }
  humans.sort((a, b) => a.seat - b.seat);
  if (humans.some((h, i) => h.seat !== i)) return null;
  return { champ, cupSeed, humans };
}

/** Grava a copa normal em andamento; copa concluída ou eliminada sai do save. */
export function saveCupProgress(save: SaveData, champ: ChampionshipState, cupSeed: number, humans: HumanEntry[]): void {
  if (champ.completed || champ.eliminated) { save.cupInProgress = null; return; }
  save.cupInProgress = {
    champ: JSON.parse(JSON.stringify(champ)) as ChampionshipState,
    cupSeed: cupSeed >>> 0,
    humans: humans.map((h) => ({ seat: h.seat, name: h.name, carId: h.carId, teamId: h.teamId, color: h.color })),
  };
}

export function clearCupProgress(save: SaveData): void {
  save.cupInProgress = null;
}

/** Libera um carro comprado em todas as modalidades. Devolve verdadeiro se era novo. */
export function unlockCar(save: SaveData, carId: string): boolean {
  if (!pricedCar(carId) || save.carsUnlocked.includes(carId)) return false;
  save.carsUnlocked.push(carId);
  return true;
}
