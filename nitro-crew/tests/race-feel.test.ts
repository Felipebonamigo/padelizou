// Termômetro de emoção (src/game/race-feel.ts): ultrapassagens, disputa, sozinho, pé no fundo, nitro e contatos de um
// carro, lidos do estado a cada tick. Só lê. Os testes 1–5 montam o estado à mão (o `progress` e a `position` só o
// stepRace recalcula); o 6 prova que ele não muda nada; o 7 é uma corrida inteira de verdade; o 8 confere a linha de
// base gravada em scripts/race-feel-base/.
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { COLLISION_COOLDOWN_TICKS, TICK_RATE } from '../src/core/constants';
import { createRace, stepRace } from '../src/core/sim/race';
import { carStats } from '../src/core/sim/stats';
import { getTrack, trackDef, TRACKS } from '../src/core/track';
import { serializeRace } from '../src/core/serialize';
import type { CarState, RaceState } from '../src/core/types';
import {
  ALONE_GAP, BATTLE_GAP, createFeelTracker, feelOf, meanFeel, observeFeel, type RaceFeel,
} from '../src/game/race-feel';
import { PROXY_SKILL } from '../scripts/career-balance-lib';
import { humanCar, NO_ASSISTS, quickRace, run, skipCountdown, syntheticTrack } from './helpers';

/** Corrida de 3 carros em fase 'racing', sem ninguém encostado: o rastreado é o carro 1. */
function setup() {
  const { state } = quickRace({ track: syntheticTrack(), totalCars: 3 });
  state.phase = 'racing';
  state.events = [];
  state.cars.forEach((c, i) => { c.z = 1000 + i * 10_000; c.x = (i - 1) * 0.8; c.speed = 0; c.nitroTicks = 0; c.finished = false; c.progress = c.z; c.position = i + 1; });
  const tr = createFeelTracker(state.cars[1].id);
  const tick = (n: number, fn?: (i: number) => void) => { for (let i = 0; i < n; i++) { fn?.(i); observeFeel(tr, state); state.tick++; } };
  return { state, tr, tick, me: state.cars[1], cars: state.cars };
}

describe('termômetro de emoção: cada medida pelo estado', () => {
  it('ultrapassagens: cada posição ganha conta uma, perder posição não desconta', () => {
    const { tr, tick, me } = setup();
    for (const p of [5, 4, 2, 3]) { me.position = p; tick(1); }
    expect(tr.overtakes).toBe(3);
    expect(feelOf(tr).overtakes).toBe(3);
  });

  it('disputa e sozinho pela distância de progresso aos vizinhos de posição', () => {
    // Posições 1/2/3 em x -0,8 / 0 / 0,8 (sem raspão); o rastreado é o de posição 2, com progress 50 000.
    const a = setup();
    a.cars[0].position = 1; a.cars[1].position = 2; a.cars[2].position = 3;
    a.me.progress = 50_000; a.cars[0].progress = 50_000 + BATTLE_GAP; a.cars[2].progress = 40_000;
    a.tick(TICK_RATE);
    expect(feelOf(a.tr).battleSeconds).toBe(1);

    const b = setup();
    b.me.progress = 50_000; b.cars[0].progress = 50_000 + ALONE_GAP + 1; b.cars[2].progress = 50_000 - ALONE_GAP - 1;
    b.tick(TICK_RATE);
    expect(feelOf(b.tr).aloneSeconds).toBe(1);
    expect(feelOf(b.tr).battleSeconds).toBe(0);

    // Carro terminado não é vizinho: o de 50 500 terminou, o outro está longe.
    const c = setup();
    c.me.progress = 50_000; c.cars[0].progress = 50_500; c.cars[0].finished = true; c.cars[2].progress = 40_000;
    c.tick(TICK_RATE);
    expect(feelOf(c.tr).aloneSeconds).toBe(1);
    expect(feelOf(c.tr).battleSeconds).toBe(0);
  });

  it('pé no fundo e nitro em % do tempo correndo', () => {
    const { tr, tick, me } = setup();
    const top = carStats(me).topSpeed;
    tick(30, () => { me.speed = 0.85 * top; });
    tick(30, () => { me.speed = 0.425 * top; });
    expect(feelOf(tr).flatOutPct).toBe(50);
    expect(feelOf(tr).nitroPct).toBe(0);

    const n = setup();
    const nTop = carStats(n.me).topSpeed;
    n.tick(60, (i) => { n.me.speed = i < 30 ? 0.85 * nTop : 0.425 * nTop; n.me.nitroTicks = i < 15 ? 5 : 0; });
    expect(feelOf(n.tr).flatOutPct).toBe(50);
    expect(feelOf(n.tr).nitroPct).toBe(25);
  });

  it('raspão de lado e batida traseira contam separados, um contato contínuo conta uma vez', () => {
    const { state, tr, tick, me, cars } = setup();
    me.z = 1000; me.x = 0; cars[0].z = 1199; cars[0].x = 0; cars[2].z = 30_000; cars[2].x = 0.8;
    state.events = [];
    tick(1);
    expect(tr.scrapes).toBe(1);
    tick(10);
    expect(tr.scrapes).toBe(1);
    cars[0].z = 5000;
    tick(COLLISION_COOLDOWN_TICKS + 1);
    cars[0].z = 1199;
    tick(1);
    expect(tr.scrapes).toBe(2);

    // Batida traseira: o evento 'collision' conta em rearHits e não vira raspão.
    const r = setup();
    r.me.z = 1000; r.me.x = 0; r.cars[0].z = 1199; r.cars[0].x = 0; r.cars[2].z = 30_000;
    r.state.events = [{ type: 'collision', carId: r.me.id, otherId: r.cars[0].id, strength: 0.5 }];
    r.tick(1);
    expect(r.tr.rearHits).toBe(1);
    expect(r.tr.scrapes).toBe(0);
  });

  it('fora da corrida não conta: contagem regressiva e depois da chegada', () => {
    const { state, tr, tick, me } = setup();
    state.phase = 'countdown';
    tick(10);
    expect(tr.ticks).toBe(0);
    state.phase = 'racing';
    me.finished = true;
    tick(10);
    expect(tr.ticks).toBe(0);
  });

  it('só lê: o estado inteiro não muda, e o arquivo só importa do núcleo', () => {
    const { state, track } = quickRace({ track: syntheticTrack(), totalCars: 6, seed: 7 });
    skipCountdown(state, track);
    const tr = createFeelTracker(humanCar(state).id);
    for (let i = 0; i < 100; i++) {
      run(state, track, 1);
      const before = serializeRace(state);
      observeFeel(tr, state);
      expect(serializeRace(state)).toBe(before);
    }
    expect(tr.ticks).toBe(100);
    const src = readFileSync(join(__dirname, '..', 'src', 'game', 'race-feel.ts'), 'utf8');
    const imports = [...src.matchAll(/from '([^']+)'/g)].map((m) => m[1]);
    expect(imports.length).toBeGreaterThan(0);
    for (const p of imports) expect(p.startsWith('../core/')).toBe(true);
    expect(src).not.toMatch(/\bDate\b|Math\.random/);
  });

  it('corrida inteira de verdade (autobahn, 20 carros, semente 11, piloto médio): números no intervalo', () => {
    // meanFeel (usado pelo balance e pelo diário): lista vazia dá null; senão, a média campo a campo.
    expect(meanFeel([])).toBeNull();
    const a: RaceFeel = { seconds: 100, overtakes: 10, overtakesPerMin: 6, battleSeconds: 50, aloneSeconds: 10, flatOutPct: 40, nitroPct: 2, scrapes: 4, rearHits: 2 };
    const b: RaceFeel = { seconds: 200, overtakes: 20, overtakesPerMin: 8, battleSeconds: 100, aloneSeconds: 30, flatOutPct: 60, nitroPct: 4, scrapes: 6, rearHits: 4 };
    expect(meanFeel([a, b])).toEqual({ seconds: 150, overtakes: 15, overtakesPerMin: 7, battleSeconds: 75, aloneSeconds: 20, flatOutPct: 50, nitroPct: 3, scrapes: 5, rearHits: 3 });
    const track = getTrack('autobahn');
    const state: RaceState = createRace({
      trackId: 'autobahn', laps: trackDef('autobahn').laps, humans: [{ seat: 0, name: 'P1', carId: 'falcao', teamId: 0, color: '#fff' }],
      totalCars: 20, difficulty: 'profissional', manualGear: false, assists: NO_ASSISTS, seed: 11,
    }, track);
    const me: CarState = humanCar(state);
    me.ai = { skill: PROXY_SKILL, laneX: 0.3, laneUntil: 0, lookahead: 32, aggression: 0.5 };
    const tr = createFeelTracker(me.id);
    for (let i = 0; i < TICK_RATE * 60 * 20 && state.phase !== 'finished'; i++) {
      stepRace(state, track, []);
      observeFeel(tr, state);
    }
    expect(state.phase).toBe('finished');
    const f = feelOf(tr);
    expect(f.seconds).toBeGreaterThanOrEqual(200);
    expect(f.seconds).toBeLessThanOrEqual(260);
    expect(f.overtakes).toBeGreaterThanOrEqual(10);
    expect(f.battleSeconds + f.aloneSeconds).toBeGreaterThan(0);
    expect(f.battleSeconds + f.aloneSeconds).toBeLessThanOrEqual(f.seconds + 0.02);
    expect(f.flatOutPct).toBeGreaterThanOrEqual(50);
    expect(f.nitroPct).toBeGreaterThanOrEqual(0.5);
    expect(f.nitroPct).toBeLessThanOrEqual(10);
    expect(f.scrapes).toBeGreaterThanOrEqual(1);
    expect(f.rearHits).toBeGreaterThanOrEqual(1);
  }, 60_000);

  it('a linha de base gravada tem as 109 pistas nas sementes 11, 12 e 13, com números válidos', () => {
    for (const seed of [11, 12, 13]) {
      const base = JSON.parse(readFileSync(join(__dirname, '..', 'scripts', 'race-feel-base', `profissional-${seed}.json`), 'utf8')) as {
        seed: number; difficulty: string; tracks: Record<string, RaceFeel>;
      };
      expect(base.seed).toBe(seed);
      expect(base.difficulty).toBe('profissional');
      expect(Object.keys(base.tracks).sort()).toEqual(TRACKS.map((d) => d.id).sort());
      for (const [id, f] of Object.entries(base.tracks)) {
        expect(f.seconds, id).toBeGreaterThan(0);
        expect(f.flatOutPct, id).toBeGreaterThanOrEqual(0);
        expect(f.flatOutPct, id).toBeLessThanOrEqual(100);
        expect(f.nitroPct, id).toBeGreaterThanOrEqual(0);
        expect(f.nitroPct, id).toBeLessThanOrEqual(100);
        expect(f.battleSeconds + f.aloneSeconds, id).toBeLessThanOrEqual(f.seconds + 0.02);
      }
    }
  });
});
