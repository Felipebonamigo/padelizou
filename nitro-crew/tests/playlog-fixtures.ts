// Dependências falsas e corridas prontas para o diário de jogo (src/game/playlog.ts). Não é teste: o vitest só coleta *.test.ts.
import type { PlaylogDeps, PlaylogRaceInput } from '../src/game/playlog';

/** Relógio controlável (clock.now em ms) e registro de cada gravação. */
export function fakeDeps(start = '2026-10-12T20:00:00Z') {
  const clock = { now: Date.parse(start) };
  const writes: Array<[string, unknown]> = [];
  const deps: PlaylogDeps = {
    now: () => new Date(clock.now),
    read: () => undefined,
    write: (k, v) => { writes.push([k, structuredClone(v)]); return true; },
    version: '0.1.0',
  };
  return { deps, writes, clock };
}

/** Corrida já resumida (o que playlogRace devolve), com um assento. */
export function fakeRace(over: Partial<PlaylogRaceInput> = {}): PlaylogRaceInput {
  return {
    mode: 'quick', online: false, track: 'copacabana', cup: null, laps: 3, field: 20, seconds: 200,
    eliminated: false, cupCompleted: false, abandoned: false,
    seats: [{ seat: 0, car: 19, position: 4, finished: true, assist: 'none', crashes: 0, contacts: 2, pits: 0, fuelEmpty: false, feel: null }],
    ...over,
  };
}
