// Progresso do jogador: copas concluídas, recordes por pista e o que o lobby lembra de cada
// assento. Mesmo contrato do settings.ts: saneado na leitura, nunca lança.
import { hasUpgrades } from '../core/career';
import { CARS } from '../core/data/cars';
import { CUPS, currentCupId, LEGACY_CUP_IDS, legacyCupOpens } from '../core/data/cups';
import type { CupDef, HumanEntry, RaceResultRow } from '../core/types';
import { sanitizeCareer, sanitizeSavedCup, sanitizeUnlocked } from './career-save';
import { isFingerprint, lapFingerprint } from './content-version';
import { sanitizeSeatPaints } from './paints';
import { DEFAULT_SAVE, type BestLap, type RaceMode, type SaveData } from './contracts';
import { isRecord, pickNumber, pickString, readJson, writeJson } from './settings';
import { sanitizeStats } from './stats';

export const SAVE_KEY = 'nitro-crew.save';
export const SEATS = 4;
export const NAME_MAX_LENGTH = 12;

/** Marca nova de um assento. `first` só vem quando não havia marca anterior (a tela mostra "PRIMEIRA MARCA", não "RECORDE"). */
export interface NewRecord { seat: number; kind: 'lap' | 'race'; first?: boolean }

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
    // Impressão estragada some sozinha; o recorde fica (vira "versão desconhecida", sem marca).
    ...(isFingerprint(v.fp) ? { fp: v.fp } : {}),
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

/** Copas concluídas com o id de hoje (save de antes da onda G: `brasil` → `br_rj`), sem repetição. */
function cupList(v: unknown): string[] {
  return [...new Set(stringList(v).map(currentCupId))];
}

/**
 * Copas abertas fora da fila: as que o save já guardava (só copa que existe) mais as que uma copa antiga concluída
 * abria e hoje não abre (`legacyCupOpens`: Copa Brasil → EUA). Lido de `rawCompleted` ANTES da troca de id — depois
 * dela, `br_rj` vencida hoje e `brasil` vencida antes da onda G são iguais, e só a segunda abria os EUA.
 */
function unlockedList(v: unknown, rawCompleted: unknown): string[] {
  const known = (id: string) => CUPS.some((c) => c.id === id);
  const out = stringList(v).filter(known);
  for (const old of stringList(rawCompleted)) {
    for (const id of legacyCupOpens(old)) if (known(id) && !out.includes(id)) out.push(id);
  }
  return out;
}

/** Conquista de copa que mudou de id (COPA_BRASIL → COPA_BR_RJ); as outras passam como estão. */
function currentAchievement(id: string): string {
  for (const [old, now] of Object.entries(LEGACY_CUP_IDS)) if (id === `COPA_${old.toUpperCase()}`) return `COPA_${now.toUpperCase()}`;
  return id;
}

/** Sigla do estado que a copa carimba (copa da Expedição Brasil), ou null. */
function stampOf(cupId: string): string | null {
  return CUPS.find((c) => c.id === cupId && c.stage === 'brasil')?.state ?? null;
}

/** Carimbos válidos (estado de alguma copa da Expedição), sem repetição, mais os das copas de estado já concluídas. */
function stampList(v: unknown, cupsCompleted: readonly string[]): string[] {
  const valid = new Set(CUPS.flatMap((c) => (c.stage === 'brasil' && c.state ? [c.state] : [])));
  const out = stringList(v).filter((s) => valid.has(s));
  for (const id of cupsCompleted) { const s = stampOf(id); if (s && !out.includes(s)) out.push(s); }
  return out;
}

/** Funde `raw` com os padrões e descarta entradas inválidas. Nunca lança. */
export function sanitizeSave(raw: unknown): SaveData {
  const r = isRecord(raw) ? raw : {};
  const d = DEFAULT_SAVE;
  const knownCar = (id: string) => CARS.some((c) => c.id === id);
  const cupsCompleted = cupList(r.cupsCompleted);
  return {
    cupsCompleted,
    cupsUnlocked: unlockedList(r.cupsUnlocked, r.cupsCompleted),
    stamps: stampList(r.stamps, cupsCompleted),
    bestLaps: bestLapTable(r.bestLaps),
    bestRaces: bestLapTable(r.bestRaces),
    achievements: [...new Set(stringList(r.achievements).map(currentAchievement))],
    racesRun: pickNumber(r.racesRun, 0, Number.MAX_SAFE_INTEGER, d.racesRun, true),
    racesWon: pickNumber(r.racesWon, 0, Number.MAX_SAFE_INTEGER, d.racesWon, true),
    seatNames: seatList(r.seatNames, d.seatNames, () => true, NAME_MAX_LENGTH),
    seatCars: seatList(r.seatCars, d.seatCars, knownCar, 32),
    // Save de antes da pintura (sem o campo): todo assento com a Original em todo carro.
    seatPaints: sanitizeSeatPaints(r.seatPaints),
    stats: sanitizeStats(r.stats),
    carsUnlocked: sanitizeUnlocked(r.carsUnlocked),
    career: sanitizeCareer(r.career),
    cupInProgress: sanitizeSavedCup(r.cupInProgress),
    tutorialDone: r.tutorialDone === true,
  };
}

export function loadSave(): SaveData {
  return sanitizeSave(readJson(SAVE_KEY));
}

export function saveSave(s: SaveData): void {
  writeJson(SAVE_KEY, sanitizeSave(s));
}

/**
 * Destravada quando não exige nada, quando a copa exigida já foi concluída, quando ela mesma já foi concluída, ou
 * quando o save a herdou aberta (`cupsUnlocked`). Os dois últimos são do save de antes da Expedição Brasil: quem
 * venceu o Mundial antigo continua com ele aberto, e quem venceu a Copa Brasil continua com os EUA abertos, mesmo
 * sem os estados que agora vêm antes. Copa desconhecida: travada.
 */
export function isCupUnlocked(save: SaveData, cupId: string, cups: readonly CupDef[]): boolean {
  const cup = cups.find((c) => c.id === cupId);
  if (!cup) return false;
  return cup.requires === null || save.cupsCompleted.includes(cup.requires) || save.cupsCompleted.includes(cup.id)
    || save.cupsUnlocked.includes(cup.id);
}

/** Marca a copa como concluída (idempotente) e, se for de um estado, carimba o passaporte. Verdadeiro se era a primeira vez. */
export function markCupCompleted(save: SaveData, cupId: string): boolean {
  const stamp = stampOf(cupId);
  if (stamp && !save.stamps.includes(stamp)) save.stamps.push(stamp);
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
 * Devolve os recordes novos (só quando melhora o anterior), por assento. No contra-relógio a
 * posição é sempre 1 (sozinho na pista), então ele conta corrida mas não vitória.
 */
export function recordRaceResults(
  save: SaveData, results: RaceResultRow[], humans: HumanEntry[], trackId: string, laps: number, mode: RaceMode = 'quick',
): NewRecord[] {
  const out: NewRecord[] = [];
  const humanRows = results.filter((r) => r.seat >= 0);
  save.racesRun += 1;
  if (mode !== 'timetrial' && humanRows.some((r) => r.position === 1)) save.racesWon += 1;
  // Revezamento: a volta e o tempo são de dois pilotos — não viram recorde de ninguém (docs/MODOS.md).
  if (mode === 'relay') return out;

  const date = new Date().toISOString();
  const entry = (r: RaceResultRow, ticks: number): BestLap => {
    const h = humans.find((x) => x.seat === r.seat);
    const carId = h?.carId ?? r.carDefId;
    const fp = lapFingerprint(trackId, carId);
    return { ticks, name: h?.name ?? r.name, carId, date, ...(fp ? { fp } : {}) };
  };

  // Recorde é de carro de fábrica: quem corre com melhorias da carreira conta corrida e vitória, não recorde.
  const factory = humanRows.filter((r) => !hasUpgrades(humans.find((x) => x.seat === r.seat)?.upgrades));
  const lap = bestHuman(factory, (r) => r.bestLapTicks);
  const prevLap = save.bestLaps[trackId];
  if (lap && improves(lap.bestLapTicks, prevLap)) {
    save.bestLaps[trackId] = entry(lap, lap.bestLapTicks);
    out.push(prevLap ? { seat: lap.seat, kind: 'lap' } : { seat: lap.seat, kind: 'lap', first: true });
  }

  const race = bestHuman(factory.filter((r) => r.finished), (r) => r.totalTicks);
  const key = bestRaceKey(trackId, laps);
  const prevRace = save.bestRaces[key];
  if (race && improves(race.totalTicks, prevRace)) {
    save.bestRaces[key] = entry(race, race.totalTicks);
    out.push(prevRace ? { seat: race.seat, kind: 'race' } : { seat: race.seat, kind: 'race', first: true });
  }
  return out;
}

/**
 * Recorde feito noutra versão do jogo (a física, o carro ou a pista mudaram desde então): a tela de recordes marca
 * "versão anterior". Nenhum recorde é apagado nem trocado por volta mais lenta por isso — só sai quando batido.
 * Sem impressão (feito antes dela) a versão é desconhecida: sem marca.
 */
export function recordFromOtherVersion(rec: BestLap, trackId: string): boolean {
  return rec.fp !== undefined && rec.fp !== lapFingerprint(trackId, rec.carId);
}

/** Guarda nome e carro de cada assento para o próximo lobby. */
export function rememberLobby(save: SaveData, humans: HumanEntry[]): void {
  for (const h of humans) {
    if (h.seat < 0 || h.seat >= SEATS) continue;
    if (h.name.trim().length > 0) save.seatNames[h.seat] = h.name.trim().slice(0, NAME_MAX_LENGTH);
    save.seatCars[h.seat] = h.carId;
  }
}
