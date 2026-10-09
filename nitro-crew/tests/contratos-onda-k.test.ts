// Contratos da onda K (docs/ondas/K.md, K0): o que a K3 (aiPace) e a K5 (bend) usam, commitado antes das frentes.
import { describe, expect, it } from 'vitest';
import { createRace } from '../src/core/sim/race';
import { deserializeRace, hashRace, serializeRace } from '../src/core/serialize';
import type { RaceConfig, TrackOp } from '../src/core/types';
import { human, NO_ASSISTS, syntheticTrack } from './helpers';

const base = (extra: Partial<RaceConfig> = {}): RaceConfig => ({
  trackId: 'sintetica', laps: 2, humans: [human(0)], totalCars: 4, difficulty: 'profissional',
  manualGear: false, assists: NO_ASSISTS, seed: 7, ...extra,
});

describe('contratos da onda K', () => {
  it('RaceConfig.aiPace atravessa serializar e desserializar', () => {
    const s = createRace({ ...base(), aiPace: 0.5 }, syntheticTrack());
    expect(deserializeRace(serializeRace(s)).config.aiPace).toBe(0.5);
  });
  it('aiPace ausente continua ausente: o estado volta igual, no texto e no hash', () => {
    const s = createRace(base(), syntheticTrack());
    const back = deserializeRace(serializeRace(s));
    expect('aiPace' in back.config).toBe(false);
    expect(serializeRace(back)).toBe(serializeRace(s));
    expect(hashRace(back)).toBe(hashRace(s));
  });
  it('TrackOp aceita o trecho bend (curva constante, sem rampa)', () => {
    const op: TrackOp = { op: 'bend', length: 12, curve: 2, hill: 1 };
    expect(op).toEqual({ op: 'bend', length: 12, curve: 2, hill: 1 });
  });
});
