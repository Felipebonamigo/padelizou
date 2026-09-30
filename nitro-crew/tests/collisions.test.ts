import { describe, it, expect } from 'vitest';
import { CAR_HALF_WIDTH, CAR_LENGTH } from '../src/core/constants';
import { carDef } from '../src/core/data/cars';
import { buildTrack } from '../src/core/track/builder';
import { resolveCarCollisions, wrappedDelta } from '../src/core/sim/collisions';
import { NEUTRAL_INPUT, type CarState, type PlayerInput } from '../src/core/types';
import { ROAD_HALF_WIDTH_M, xToMeters, zToMeters } from '../src/render/units';
import { human, humanCar, quickRace, run, skipCountdown, syntheticTrack } from './helpers';

// Pegada VISUAL do carro: o modelo de src/render/cars.ts mede 4,4 × 1,9 m, e todos os estilos de carroceria
// ocupam a mesma (contrato de CarBody em src/core/types.ts). A física tem de bater com o que a tela mostra.
const VISUAL_LENGTH_M = 4.4;
const VISUAL_WIDTH_M = 1.9;
/** Metros na tela → `x` normalizado e → unidades de `z` (escala de src/render/units.ts). */
const xOf = (m: number) => m / ROAD_HALF_WIDTH_M;
const zOf = (m: number) => m / zToMeters(1);
/** Distância entre centros, em metros na tela. */
const lateralM = (a: CarState, b: CarState) => xToMeters(Math.abs(a.x - b.x));

/** Dois humanos parados numa reta, já correndo, sem cooldown: o teste põe cada um onde quiser. */
function pair() {
  const { state, track } = quickRace({ track: syntheticTrack(), totalCars: 2, humans: [human(0), human(1, 1)] });
  skipCountdown(state, track);
  state.events = [];
  const a = humanCar(state, 0); const b = humanCar(state, 1);
  for (const c of [a, b]) { c.collisionCooldown = 0; c.speed = 5000; }
  return { state, track, a, b };
}

/** Lado a lado (mesmo z) com os centros a `centersM` metros; devolve quanto cada um andou de lado (m). */
function sideBySide(centersM: number): { moved: [number, number]; after: number; sameSide: boolean; a: CarState; b: CarState } {
  const { state, track, a, b } = pair();
  a.z = 20000; b.z = 20000;
  a.x = -xOf(centersM / 2); b.x = xOf(centersM / 2);
  const ax = a.x; const bx = b.x;
  resolveCarCollisions(state, track);
  return { moved: [xToMeters(Math.abs(a.x - ax)), xToMeters(Math.abs(b.x - bx))], after: lateralM(a, b), sameSide: a.x < b.x, a, b };
}

describe('pegada de colisão = pegada visual', () => {
  it('a caixa de colisão tem entre 85% e 100% do carro na tela, nos dois eixos', () => {
    // Era 120 u (2,4 m) × 0,44 (3,08 m): de lado batia com mais de 1 m de ar; em fila, só depois de 2 m enfiado no outro.
    const length = zToMeters(CAR_LENGTH) / VISUAL_LENGTH_M;
    const width = xToMeters(CAR_HALF_WIDTH * 2) / VISUAL_WIDTH_M;
    expect(length).toBeGreaterThanOrEqual(0.85); expect(length).toBeLessThanOrEqual(1);
    expect(width).toBeGreaterThanOrEqual(0.85); expect(width).toBeLessThanOrEqual(1);
  });

  it('lado a lado com 0,5 m de ar entre as latarias não colidem', () => {
    const r = sideBySide(VISUAL_WIDTH_M + 0.5);
    expect(r.moved).toEqual([0, 0]);
    expect(r.a.speed).toBe(5000); expect(r.b.speed).toBe(5000);
  });

  it('andando lado a lado a 0,3 m um do outro por 3 s, ninguém perde velocidade', () => {
    const { state, track } = quickRace({ track: syntheticTrack(), totalCars: 3, humans: [human(0), human(1, 1), human(2, 2)] });
    skipCountdown(state, track);
    const [a, b, alone] = [humanCar(state, 0), humanCar(state, 1), humanCar(state, 2)];
    const half = xOf((VISUAL_WIDTH_M + 0.3) / 2);
    a.z = 20000; b.z = 20000; alone.z = 60000;
    a.x = -half; b.x = half; alone.x = 0;
    for (const c of [a, b, alone]) c.speed = 4000;
    const lane = [-half, half, 0];
    run(state, track, 180, (s, seat) => ({ ...NEUTRAL_INPUT, throttle: true, steer: (lane[seat] - humanCar(s, seat).x) * 5 }));
    expect(a.speed).toBe(alone.speed);
    expect(b.speed).toBe(alone.speed);
  });

  it('encostados (latarias sobrepostas) colidem: separação proporcional à sobreposição, sem atravessar nem arremessar', () => {
    const small = sideBySide(VISUAL_WIDTH_M - 0.2);
    const big = sideBySide(VISUAL_WIDTH_M - 0.6);
    for (const r of [small, big]) {
      expect(r.moved[0]).toBeGreaterThan(0);
      expect(r.moved[0]).toBeCloseTo(r.moved[1], 9); // os dois recuam igual
      expect(r.sameSide).toBe(true); // ninguém passa para o outro lado
      expect(r.after).toBeGreaterThanOrEqual(VISUAL_WIDTH_M * 0.85); // já não estão enfiados um no outro…
      expect(r.after).toBeLessThanOrEqual(VISUAL_WIDTH_M + 0.02); // …e só se afastam até encostar
    }
    // Sem tranco fixo por tick: raspão de 0,2 m move cada um no máximo 0,1 m; o de 0,6 m move bem mais.
    expect(small.moved[0]).toBeLessThanOrEqual(0.1 + 1e-9);
    expect(big.moved[0]).toBeGreaterThan(small.moved[0] * 2);
  });

  it('em fila, o contato vem perto do comprimento na tela (não em 2,4 m) e passa velocidade como antes', () => {
    const hit = (centersM: number) => {
      const { state, track, a, b } = pair();
      a.z = 20000; a.x = 0; a.speed = 6000;
      b.z = 20000 + zOf(centersM); b.x = xOf(0.15); b.speed = 2000;
      resolveCarCollisions(state, track);
      return { state, a, b };
    };
    const touching = hit(VISUAL_LENGTH_M - 1.2); // 1,2 m enfiado: antes passava sem nada
    expect(touching.state.events).toEqual([{ type: 'collision', carId: touching.a.id, otherId: touching.b.id, strength: expect.any(Number) }]);
    expect(touching.a.speed).toBeLessThan(2500);
    expect(touching.b.speed).toBeGreaterThan(2000);
    const apart = hit(VISUAL_LENGTH_M + 0.2); // 0,2 m de ar
    expect(apart.state.events).toEqual([]);
    expect(apart.a.speed).toBe(6000); expect(apart.b.speed).toBe(2000);
  });

  it('batida por trás não atravessa: quem bate fica atrás, encostado, sem tranco para o lado', () => {
    const { state, track } = quickRace({ track: syntheticTrack(), totalCars: 2, humans: [human(0), human(1, 1)] });
    skipCountdown(state, track);
    const rear = humanCar(state, 0); const front = humanCar(state, 1);
    rear.z = 5000; rear.x = 0; rear.speed = 6000;
    front.z = 5000 + zOf(8); front.x = 0.02; front.speed = 1500;
    const inputs = (s: unknown, seat: number): PlayerInput => (seat === 0 ? { ...NEUTRAL_INPUT, throttle: true } : NEUTRAL_INPUT);
    let minGap = Infinity; let hits = 0;
    for (let i = 0; i < 120; i++) {
      run(state, track, 1, inputs);
      hits += state.events.filter((e) => e.type === 'collision').length;
      const d = wrappedDelta(front.z, rear.z, track.length);
      expect(d, `tick ${i}`).toBeGreaterThan(0); // nunca passa por dentro
      minGap = Math.min(minGap, zToMeters(d));
    }
    expect(hits).toBeGreaterThan(0);
    expect(minGap).toBeGreaterThanOrEqual(VISUAL_LENGTH_M * 0.85);
    expect(Math.abs(front.x - 0.02)).toBeLessThan(0.005);
    expect(Math.abs(rear.x)).toBeLessThan(0.005);
  });
});

describe('engavetamento', () => {
  it('quem recua depois de bater não fica enfiado em quem vinha atrás: nenhum par sobra sobreposto', () => {
    // A ordem dos pares importa: o do meio bate no da frente e recua para dentro do de trás, que já tinha sido conferido.
    const { state, track } = quickRace({ track: syntheticTrack(), totalCars: 3, humans: [human(0), human(1, 1), human(2, 2)] });
    skipCountdown(state, track);
    const [middle, rear, front] = [humanCar(state, 0), humanCar(state, 1), humanCar(state, 2)];
    for (const c of [middle, rear, front]) { c.x = 0; c.collisionCooldown = 0; }
    front.z = 20000; front.speed = 2000;
    middle.z = front.z - (CAR_LENGTH - 10); middle.speed = 5000; // acabou de encostar no da frente
    rear.z = middle.z - (CAR_LENGTH + 5); rear.speed = 5000; // 5 u atrás do do meio, na mesma velocidade
    resolveCarCollisions(state, track);
    const cars = [middle, rear, front];
    for (let i = 0; i < cars.length; i++) {
      for (let j = i + 1; j < cars.length; j++) {
        const gap = Math.abs(wrappedDelta(cars[i].z, cars[j].z, track.length));
        expect(gap, `seats ${cars[i].seat} e ${cars[j].seat}`).toBeGreaterThanOrEqual(CAR_LENGTH);
      }
    }
    expect(wrappedDelta(front.z, middle.z, track.length)).toBeGreaterThan(0);
    expect(wrappedDelta(middle.z, rear.z, track.length)).toBeGreaterThan(0);
  });
});

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
