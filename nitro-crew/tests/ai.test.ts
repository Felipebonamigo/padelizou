import { describe, it, expect } from 'vitest';
import { TICK_RATE } from '../src/core/constants';
import { carDef } from '../src/core/data/cars';
import { TRACKS } from '../src/core/track/tracks';
import { getTrack } from '../src/core/track';
import { idle, quickRace, run } from './helpers';

describe('IA', () => {
  // Um teste por pista, cada um com o próprio limite de tempo (eram 12 pistas num teste só de 60 s).
  it.each(TRACKS.map((d) => [d.id] as const))('%s: 19 carros de IA completam uma volta em menos de 2 minutos sem ninguém travar', (id) => {
    const track = getTrack(id);
    const { state } = quickRace({ track, totalCars: 20, laps: 3, difficulty: 'profissional', seed: 11 });
    const stuck: number[] = [];
    for (let i = 0; i < TICK_RATE * 120 && !state.cars.filter((c) => c.seat < 0).every((c) => c.lap >= 2); i++) {
      run(state, track, 1, idle);
      if (state.phase === 'racing' && state.tick > state.startTick + TICK_RATE * 8) {
        for (const c of state.cars) if (c.seat < 0 && c.speed < carDef(c.carId).topSpeed * 0.1) stuck.push(c.id);
      }
    }
    const ai = state.cars.filter((c) => c.seat < 0);
    expect(ai.every((c) => c.lap >= 2), `${id}: voltas ${ai.map((c) => c.lap).join(',')}`).toBe(true);
    expect(stuck.length, `${id}: ticks com IA quase parada`).toBeLessThan(ai.length * 30);
  }, 30_000);

  // Era só o Passo Alpino; com 32 pistas, vale para todas as de dificuldade máxima.
  it.each(TRACKS.filter((d) => d.difficulty === 5).map((d) => [d.id] as const))('%s: a IA fica na pista a maior parte do tempo, mesmo nas pistas mais difíceis', (id) => {
    const track = getTrack(id);
    const { state } = quickRace({ track, totalCars: 12, laps: 3, difficulty: 'campeao', seed: 5 });
    let off = 0; let total = 0;
    for (let i = 0; i < TICK_RATE * 75; i++) {
      run(state, track, 1, idle);
      if (state.phase !== 'racing') continue;
      for (const c of state.cars) if (c.seat < 0) { total++; if (Math.abs(c.x) > 1.05) off++; }
    }
    expect(off / total).toBeLessThan(0.08);
  }, 30_000);

  it('a dificuldade muda o ritmo: campeão anda mais que amador na mesma pista', () => {
    const track = getTrack('rota_66');
    const a = quickRace({ track, totalCars: 6, difficulty: 'amador', seed: 9 });
    const b = quickRace({ track, totalCars: 6, difficulty: 'campeao', seed: 9 });
    run(a.state, track, TICK_RATE * 40, idle); run(b.state, track, TICK_RATE * 40, idle);
    const best = (s: typeof a.state) => Math.max(...s.cars.filter((c) => c.seat < 0).map((c) => c.progress));
    expect(best(b.state)).toBeGreaterThan(best(a.state) * 1.04);
  });

  it('a IA usa nitro e para no box quando o combustível acaba', () => {
    const track = getTrack('autobahn');
    const { state } = quickRace({ track, totalCars: 10, laps: 5, difficulty: 'profissional', seed: 21 });
    let nitros = 0; let pits = 0;
    for (const c of state.cars) if (c.seat < 0) c.fuel = 0.3; // encurta o teste
    for (let i = 0; i < TICK_RATE * 150; i++) {
      run(state, track, 1, idle);
      for (const e of state.events) { if (e.type === 'nitro') nitros++; if (e.type === 'pit_enter') pits++; }
    }
    expect(nitros).toBeGreaterThan(3);
    expect(pits).toBeGreaterThan(0);
    expect(state.cars.filter((c) => c.seat < 0).every((c) => c.fuel > 0 || c.inPit)).toBe(true);
  }, 30_000);

  it('o elástico deixa a IA mais lenta quando dispara à frente do humano (amador)', () => {
    const track = getTrack('rota_66');
    const { state } = quickRace({ track, totalCars: 4, difficulty: 'amador', seed: 2 });
    run(state, track, TICK_RATE * 30, idle);
    const ai = state.cars.filter((c) => c.seat < 0);
    for (const c of ai) expect(c.speed).toBeLessThan(carDef(c.carId).topSpeed * 0.9);
  });
});
