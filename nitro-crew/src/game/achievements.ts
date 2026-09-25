// Conquistas: regras avaliadas pela sessão ao fim de cada corrida (e algumas no meio dela).
// A lista com textos mora em desktop.ts (ponte com a Steam); aqui só as regras.
import type { RaceResultRow, RaceState, HumanEntry } from '../core/types';
import type { SaveData } from './contracts';
import type { RaceMode } from './contracts';

export interface RaceTelemetry {
  /** Por assento: entrou no box nesta corrida. */
  pitted: Set<number>;
  /** Por assento: nitros usados na volta atual e máximo por volta. */
  nitrosThisLap: Map<number, number>;
  maxNitrosInLap: Map<number, number>;
  /** Por assento: deu um empurrão a um companheiro. */
  gaveTow: Set<number>;
  /** Por assento: saiu do asfalto na volta atual. */
  offroadThisLap: Set<number>;
  perfectLap: Set<number>;
}

export function newTelemetry(): RaceTelemetry {
  return { pitted: new Set(), nitrosThisLap: new Map(), maxNitrosInLap: new Map(), gaveTow: new Set(), offroadThisLap: new Set(), perfectLap: new Set() };
}

/** Conquistas desbloqueadas por esta corrida (ids ainda não presentes no save). */
export function evaluateAchievements(
  save: SaveData, mode: RaceMode, state: RaceState, results: RaceResultRow[], humans: HumanEntry[], telemetry: RaceTelemetry,
  trackNight: boolean, cupJustCompleted: string | null, difficulty: string,
): string[] {
  const out = new Set<string>();
  const has = (id: string) => save.achievements.includes(id) || out.has(id);
  const add = (id: string) => { if (!has(id)) out.add(id); };
  const humanRows = results.filter((r) => r.seat >= 0);
  const won = humanRows.some((r) => r.position === 1);
  if (won && mode !== 'timetrial') add('PRIMEIRA_VITORIA');
  if (won && trackNight && mode !== 'timetrial') add('MADRUGADA');
  if (humans.length === 4) add('EQUIPE_COMPLETA');
  for (const r of humanRows) {
    if (r.position === 1 && !telemetry.pitted.has(r.seat) && !state.config.timeTrial) add('SEM_BOX');
    if ((telemetry.maxNitrosInLap.get(r.seat) ?? 0) >= 3) add('NITRO_TRIPLO');
    if (telemetry.gaveTow.has(r.seat)) add('EMPURRAO');
    if (telemetry.perfectLap.has(r.seat)) add('VOLTA_PERFEITA');
  }
  if (cupJustCompleted) {
    add(`COPA_${cupJustCompleted.toUpperCase()}`);
    if (difficulty === 'campeao') add('CAMPEAO');
  }
  return [...out];
}
