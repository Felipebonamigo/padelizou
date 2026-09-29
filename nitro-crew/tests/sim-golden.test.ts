// Trava de comportamento da simulação: a impressão digital de corridas inteiras (hash de cada tick, todos
// os eventos e o estado completo) tem de bater com o valor fixo abaixo. Existe para as otimizações de CPU
// (docs/DESEMPENHO.md): otimizar o stepRace não pode mudar um único bit da corrida. Os valores foram
// gravados no commit 1605294, antes de qualquer otimização, e regravados em 28/09 depois do co-op afinado
// (passo 1.4: empurrão, vácuo de equipe, elástico, box da IA) — com e sem a otimização deram o mesmo valor.
//
// Mudou a jogabilidade DE PROPÓSITO (constante, IA, física)? Aí o hash muda e é esperado: rode
// `npx tsx scripts/perf-sim.ts --fingerprints`, confira que só mudaram as corridas que deviam e atualize.
import { describe, expect, it } from 'vitest';
import { fingerprintScenario, GOLDEN_SCENARIOS } from '../scripts/sim-scenarios';
import { SEGMENT_LENGTH } from '../src/core/constants';
import { wrappedDelta } from '../src/core/sim/collisions';
import { getTrack } from '../src/core/track';
import { fmodFast, maxCurveAhead, segmentAt } from '../src/core/track/builder';
import { PHYSICS_REVISION } from '../src/game/content-version';

const EXPECTED: Record<string, { fingerprint: string; ticks: number }> = {
  'solo-sem-assistencias': { fingerprint: '08a9194b', ticks: 9193 },
  'coop4-tudo': { fingerprint: 'bf7ff410', ticks: 8078 },
  'versus-cambio-manual': { fingerprint: '7b72a4b9', ticks: 10196 },
  escolta: { fingerprint: '6b252411', ticks: 8476 },
  revezamento: { fingerprint: 'ab70b19b', ticks: 11852 },
  'tomada-pela-ia': { fingerprint: 'd1868c01', ticks: 9249 },
  'contra-relogio': { fingerprint: '89f654e0', ticks: 7768 },
  'sem-personalidades': { fingerprint: 'aac2f1dc', ticks: 10146 },
};

describe('corridas de referência (impressão digital fixa)', () => {
  it('toda corrida de referência tem valor gravado', () => {
    expect(GOLDEN_SCENARIOS.map((s) => s.name).sort()).toEqual(Object.keys(EXPECTED).sort());
  });

  for (const s of GOLDEN_SCENARIOS) {
    it(`${s.name}: a corrida inteira é bit a bit a mesma`, () => {
      const run = fingerprintScenario(s);
      expect(run.finished).toBe(true);
      expect({ fingerprint: run.fingerprint, ticks: run.ticks }).toEqual(EXPECTED[s.name]);
    }, 120_000);
  }

  // O contra-relógio de referência é o carro sozinho na pista (sem IA): se o hash dele mudou, a física de quem pilota
  // mudou, e um fantasma ou recorde gravado antes pode ter ficado impossível. As constantes, o carro e o traçado já
  // entram na impressão da volta; mudança só de CÓDIGO não entra — por isso a revisão manual anda presa a este hash.
  const PHYSICS_REVISION_AT = { revision: 1, contraRelogio: '89f654e0' };
  it('a revisão da física dos fantasmas e recordes anda junto com o contra-relógio de referência', () => {
    expect(
      { revision: PHYSICS_REVISION, contraRelogio: EXPECTED['contra-relogio'].fingerprint },
      'o contra-relógio mudou? suba PHYSICS_REVISION em src/game/content-version.ts e grave o par novo aqui (docs/FANTASMA.md)',
    ).toEqual(PHYSICS_REVISION_AT);
  });
});

describe('atalhos sem fmod (docs/DESEMPENHO.md) dão o mesmo bit que as fórmulas originais', () => {
  const track = getTrack('copacabana');
  const len = track.length;
  const n = track.segments.length;
  // Bordas (±0, ±len, 2·len, o maior double abaixo de len) e uma varredura densa com frações quebradas.
  const below = (v: number) => v - v * Number.EPSILON;
  const values = [0, -0, len, -len, 2 * len, -2 * len, below(len), -below(len), below(2 * len), 3.5 * len, -3.5 * len, 1e-300, -1e-300, NaN, Infinity, -Infinity];
  for (let k = -2000; k <= 4000; k++) values.push(k * (len / 997) + k * 0.123456789, k * SEGMENT_LENGTH, k * SEGMENT_LENGTH - 1e-9);

  it('fmodFast(v, len) é v % len', () => {
    expect(values.filter((v) => !Object.is(fmodFast(v, len), v % len))).toEqual([]);
  });

  it('segmentAt e maxCurveAhead escolhem os mesmos segmentos', () => {
    const segmentAtRef = (z: number) => { let zz = z % len; if (zz < 0) zz += len; return track.segments[Math.floor(zz / SEGMENT_LENGTH) % n]; };
    const maxCurveRef = (z: number, count: number) => {
      const start = Math.floor(((z % len) + len) % len / SEGMENT_LENGTH);
      let max = 0;
      for (let i = 0; i < count; i++) { const c = Math.abs(track.segments[(start + i) % n].curve); if (c > max) max = c; }
      return max;
    };
    const wrong: string[] = [];
    for (const z of values) {
      if (!Number.isFinite(z)) continue;
      if (segmentAt(track, z) !== segmentAtRef(z)) wrong.push(`segmentAt ${z}`);
      for (const count of [1, 12, 40, n + 5]) if (!Object.is(maxCurveAhead(track, z, count), maxCurveRef(z, count))) wrong.push(`maxCurveAhead ${z} ${count}`);
    }
    expect(wrong).toEqual([]);
  });

  it('wrappedDelta é a conta original', () => {
    const ref = (a: number, b: number) => { let d = a - b; d %= len; if (d < 0) d += len; if (d > len / 2) d -= len; return d; };
    const wrong: string[] = [];
    for (let i = 0; i < values.length; i += 7) {
      for (let j = 0; j < values.length; j += 13) if (!Object.is(wrappedDelta(values[i], values[j], len), ref(values[i], values[j]))) wrong.push(`${values[i]} ${values[j]}`);
    }
    expect(wrong).toEqual([]);
  });
});
