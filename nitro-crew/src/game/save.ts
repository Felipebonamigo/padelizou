// Progresso do jogador: copas concluídas, recordes por pista e o que o lobby lembra de cada
// assento. Mesmo contrato do settings.ts: saneado na leitura, nunca lança.
import { CARS } from '../core/data/cars';
import type { CupDef, HumanEntry, RaceResultRow } from '../core/types';
import { DEFAULT_SAVE, type BestLap, type SaveData } from './contracts';
import { isRecord, pickNumber, pickString, readJson, writeJson } from './settings';

export const SAVE_KEY = 'nitro-crew.save';
export const SEATS = 4;
export const NAME_MAX_LENGTH = 12;

export interface NewRecord { seat: number; kind: 'lap' | 'race' }

function stringList(v: unknown): string[] {
  if (!Array.isArray(v)) return [];
  return [...new Set(v.filter((x): x is string => typeof x === 'string' && x.length > 0))];
}

function pickBestLap(v: unknown): BestLap | null {
  if (!isRecord(v)) return null;
  const ticks = v.ticks;
  if (typeof ticks !== 'number' || !Number.isFinite(ticks) || ticks <= 0) return null;
  return {
    ticks: Math.round(ticks),
    name: pickString(v.name, '?', NAME_MAX_LENGTH),
    carId: pickString(v.carId, DEFAULT_SAVE.seatCars[0]),
    date: pickString(v.date, ''),
  };
}

function bestLapTable(v: unknown): Record<string, BestLap> {
  const out: Record<string, BestLap> = {};
  if (!isRecord(v)) return out;
  for (const [key, entry] of Object.entries(v)) {
    const lap = pickBestLap(entry);
    if (lap) out[key] = lap;
  }
  return out;
}

function seatList(v: unknown, defaults: readonly string[], valid: (s: string) => boolean, maxLength: number): string[] {
  const arr = Array.isArray(v) ? v : [];
  const out: string[] = [];
  for (let i = 0; i < SEATS; i++) {
    const x = arr[i];
    out.push(typeof x === 'string' && x.trim().length > 0 && valid(x) ? x.trim().slice(0, maxLength) : defaults[i]);
  }
  return out;
}

/** Funde `raw` com os padrões e descarta entradas inválidas. Nunca lança. */
export function sanitizeSave(raw: unknown): SaveData {
  const r = isRecord(raw) ? raw : {};
  const d = DEFAULT_SAVE;
  const knownCar = (id: string) => CARS.some((c) => c.id === id);
  return {
    cupsCompleted: stringList(r.cupsCompleted),
    bestLaps: bestLapTable(r.bestLaps),
    bestRaces: bestLapTable(r.bestRaces),
    achievements: stringList(r.achievements),
    racesRun: pickNumber(r.racesRun, 0, Number.MAX_SAFE_INTEGER, d.racesRun, true),
    racesWon: pickNumber(r.racesWon, 0, Number.MAX_SAFE_INTEGER, d.racesWon, true),
    seatNames: seatList(r.seatNames, d.seatNames, () => true, NAME_MAX_LENGTH),
    seatCars: seatList(r.seatCars, d.seatCars, knownCar, 32),
  };
}

export function loadSave(): SaveData {
  return sanitizeSave(readJson(SAVE_KEY));
}

export function saveSave(s: SaveData): void {
  writeJson(SAVE_KEY, sanitizeSave(s));
}

/** Destravada quando não exige nada ou quando a copa exigida já foi concluída. Copa desconhecida: travada. */
export function isCupUnlocked(save: SaveData, cupId: string, cups: readonly CupDef[]): boolean {
  const cup = cups.find((c) => c.id === cupId);
  if (!cup) return false;
  return cup.requires === null || save.cupsCompleted.includes(cup.requires);
}

/** Marca a copa como concluída (idempotente). Devolve verdadeiro se era a primeira vez. */
export function markCupCompleted(save: SaveData, cupId: string): boolean {
  if (save.cupsCompleted.includes(cupId)) return false;
  save.cupsCompleted.push(cupId);
  return true;
}

export function bestRaceKey(trackId: string, laps: number): string {
  return `${trackId}:${laps}`;
}

function bestHuman(rows: RaceResultRow[], ticksOf: (r: RaceResultRow) => number): RaceResultRow | null {
  let best: RaceResultRow | null = null;
  for (const r of rows) {
    const ticks = ticksOf(r);
    if (ticks <= 0) continue; // -1 = sem volta completa; 0 não é um tempo
    if (!best || ticks < ticksOf(best)) best = r;
  }
  return best;
}

function improves(ticks: number, previous: BestLap | undefined): boolean {
  return previous === undefined || ticks < previous.ticks;
}

/**
 * Atualiza os recordes da pista com o melhor humano da corrida e as contagens de corridas.
 * Devolve os recordes novos (só quando melhora o anterior), por assento.
 */
export function recordRaceResults(
  save: SaveData, results: RaceResultRow[], humans: HumanEntry[], trackId: string, laps: number,
): NewRecord[] {
  const out: NewRecord[] = [];
  const humanRows = results.filter((r) => r.seat >= 0);
  save.racesRun += 1;
  if (humanRows.some((r) => r.position === 1)) save.racesWon += 1;

  const date = new Date().toISOString();
  const entry = (r: RaceResultRow, ticks: number): BestLap => {
    const h = humans.find((x) => x.seat === r.seat);
    return { ticks, name: h?.name ?? r.name, carId: h?.carId ?? r.carDefId, date };
  };

  const lap = bestHuman(humanRows, (r) => r.bestLapTicks);
  if (lap && improves(lap.bestLapTicks, save.bestLaps[trackId])) {
    save.bestLaps[trackId] = entry(lap, lap.bestLapTicks);
    out.push({ seat: lap.seat, kind: 'lap' });
  }

  const race = bestHuman(humanRows.filter((r) => r.finished), (r) => r.totalTicks);
  const key = bestRaceKey(trackId, laps);
  if (race && improves(race.totalTicks, save.bestRaces[key])) {
    save.bestRaces[key] = entry(race, race.totalTicks);
    out.push({ seat: race.seat, kind: 'race' });
  }
  return out;
}

/** Guarda nome e carro de cada assento para o próximo lobby. */
export function rememberLobby(save: SaveData, humans: HumanEntry[]): void {
  for (const h of humans) {
    if (h.seat < 0 || h.seat >= SEATS) continue;
    if (h.name.trim().length > 0) save.seatNames[h.seat] = h.name.trim().slice(0, NAME_MAX_LENGTH);
    save.seatCars[h.seat] = h.carId;
  }
}
