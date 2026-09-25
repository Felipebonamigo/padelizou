import { describe, it, expect } from 'vitest';
import { carDef } from '../src/core/data/cars';
import { buildTrack } from '../src/core/track/builder';
import { wrappedDelta } from '../src/core/sim/collisions';
import { NEUTRAL_INPUT } from '../src/core/types';
import { human, humanCar, quickRace, run, skipCountdown, syntheticTrack } from './helpers';

describe('colisões', () => {
  it('wrappedDelta dá a menor distância com volta', () => {
    expect(wrappedDelta(10, 5, 1000)).toBe(5);
    expect(wrappedDelta(5, 995, 1000)).toBe(10);
    expect(wrappedDelta(995, 5, 1000)).toBe(-10);
  });

  it('batida por trás: quem bate perde velocidade, quem é batido ganha um pouco, com evento', () => {
    const { state, track } = quickRace({ track: syntheticTrack(), totalCars: 2, humans: [human(0), human(1, 1)] });
    skipCountdown(state, track);
    const a = humanCar(state, 0); const b = humanCar(state, 1);
    a.z = 5000; a.x = 0; a.speed = 6000; b.z = 5150; b.x = 0.05; b.speed = 2000;
    run(state, track, 1, () => ({ ...NEUTRAL_INPUT, throttle: true }));
    expect(state.events.some((e) => e.type === 'collision' && e.carId === a.id && e.otherId === b.id)).toBe(true);
    expect(a.speed).toBeLessThan(2500);
    expect(b.speed).toBeGreaterThan(2000);
  });

  it('bater numa árvore fora da pista corta a velocidade e empurra de volta', () => {
    const def = { id: 'arv', name: 'x', country: 'x', scenery: 'alpine' as const, timeOfDay: 'day' as const, laps: 1, difficulty: 1, ops: [{ op: 'straight' as const, length: 300 }] };
    const track = buildTrack(def);
    for (const s of track.segments) s.sprites = [];
    track.segments[60].sprites.push({ kind: 'tree', x: 1.4, scale: 1, solid: true, variant: 0 });
    const { state } = quickRace({ track, totalCars: 1 });
    skipCountdown(state, track);
    const car = humanCar(state);
    car.z = 60 * 200 + 10; car.x = 1.35; car.speed = 5000;
    run(state, track, 1, () => ({ ...NEUTRAL_INPUT, throttle: true }));
    expect(state.events.some((e) => e.type === 'crash')).toBe(true);
    expect(car.speed).toBeLessThanOrEqual(carDef(car.carId).topSpeed * 0.25 + 1);
    expect(car.x).toBeLessThan(1.35);
  });

  it('no box não se bate no muro nem se conta como grama', () => {
    const { state, track } = quickRace({ track: syntheticTrack(), totalCars: 1 });
    skipCountdown(state, track);
    const car = humanCar(state);
    car.z = 10 * 200; car.x = 1.55; car.speed = 800;
    run(state, track, 5, () => ({ ...NEUTRAL_INPUT, throttle: true }));
    expect(state.events.some((e) => e.type === 'crash')).toBe(false);
    expect(car.inPit).toBe(true);
  });
});
