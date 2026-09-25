import { buildTrack } from '../src/core/track/builder';
import { getTrack } from '../src/core/track';
import { createRace, stepRace } from '../src/core/sim/race';
import { NEUTRAL_INPUT, type CoopAssists, type Difficulty, type HumanEntry, type PlayerInput, type RaceConfig, type RaceState, type Track, type TrackDef, type TrackOp } from '../src/core/types';

export const ALL_ASSISTS: CoopAssists = { sharedNitro: true, tow: true, teamDraft: true, catchup: true };
export const NO_ASSISTS: CoopAssists = { sharedNitro: false, tow: false, teamDraft: false, catchup: false };

/** Pista sintética: uma reta longa (ou as operações dadas), sem cenário que atrapalhe os testes. */
export function syntheticTrack(ops: TrackOp[] = [{ op: 'straight', length: 600 }], id = 'sintetica'): Track {
  const def: TrackDef = { id, name: 'Sintética', country: 'Teste', scenery: 'savanna', timeOfDay: 'day', laps: 3, difficulty: 1, ops };
  const t = buildTrack(def);
  for (const s of t.segments) s.sprites = s.sprites.filter((sp) => !sp.solid);
  return t;
}

export function human(seat: number, teamId = 0, carId = 'falcao'): HumanEntry {
  return { seat, name: `P${seat + 1}`, carId, teamId, color: '#fff' };
}

export interface QuickOpts {
  track?: Track; humans?: HumanEntry[]; totalCars?: number; laps?: number; difficulty?: Difficulty;
  assists?: CoopAssists; seed?: number; manualGear?: boolean; timeTrial?: boolean;
}

export function quickRace(opts: QuickOpts = {}): { state: RaceState; track: Track } {
  const track = opts.track ?? getTrack('copacabana');
  const humans = opts.humans ?? [human(0)];
  const config: RaceConfig = {
    trackId: track.def.id, laps: opts.laps ?? 2, humans, totalCars: opts.totalCars ?? 8,
    difficulty: opts.difficulty ?? 'profissional', manualGear: opts.manualGear ?? false,
    assists: opts.assists ?? NO_ASSISTS, seed: opts.seed ?? 42, timeTrial: opts.timeTrial,
  };
  return { state: createRace(config, track), track };
}

export type InputFn = (state: RaceState, seat: number) => PlayerInput;
export const idle: InputFn = () => NEUTRAL_INPUT;
export const fullThrottle: InputFn = () => ({ ...NEUTRAL_INPUT, throttle: true });

export function run(state: RaceState, track: Track, ticks: number, input: InputFn = fullThrottle): void {
  for (let i = 0; i < ticks; i++) {
    const inputs: PlayerInput[] = [];
    for (const c of state.cars) if (c.seat >= 0) inputs[c.seat] = input(state, c.seat);
    stepRace(state, track, inputs);
  }
}

/** Avança até a contagem acabar. */
export function skipCountdown(state: RaceState, track: Track): void {
  while (state.phase === 'countdown') stepRace(state, track, []);
}

export function humanCar(state: RaceState, seat = 0) {
  const c = state.cars.find((x) => x.seat === seat);
  if (!c) throw new Error('sem humano no assento ' + seat);
  return c;
}
