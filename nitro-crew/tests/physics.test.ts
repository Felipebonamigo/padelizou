import { describe, it, expect } from 'vitest';
import { GEAR_TOP, NITRO_PER_RACE, OFFROAD_LIMIT_FACTOR, NITRO_SPEED_MULT } from '../src/core/constants';
import { carDef } from '../src/core/data/cars';
import { holdableSpeedFraction } from '../src/core/sim/physics';
import { NEUTRAL_INPUT } from '../src/core/types';
import { fullThrottle, human, humanCar, idle, quickRace, run, skipCountdown, syntheticTrack, NO_ASSISTS } from './helpers';

const straight = () => syntheticTrack();

describe('física do carro', () => {
  it('acelerando numa reta chega à velocidade máxima e não passa dela', () => {
    const { state, track } = quickRace({ track: straight(), totalCars: 1 });
    run(state, track, 60 * 15);
    const car = humanCar(state);
    const top = carDef(car.carId).topSpeed;
    expect(car.speed).toBeCloseTo(top, 0);
    expect(car.speed).toBeLessThanOrEqual(top + 1e-6);
  });

  it('freando, o carro para', () => {
    const { state, track } = quickRace({ track: straight(), totalCars: 1 });
    run(state, track, 60 * 10);
    run(state, track, 60 * 5, () => ({ ...NEUTRAL_INPUT, brake: true }));
    expect(humanCar(state).speed).toBe(0);
  });

  it('durante a contagem ninguém sai do lugar', () => {
    const { state, track } = quickRace({ track: straight(), totalCars: 4 });
    const z0 = state.cars.map((c) => c.z);
    run(state, track, 100);
    expect(state.phase).toBe('countdown');
    expect(state.cars.map((c) => c.z)).toEqual(z0);
  });

  it('na grama a velocidade cai para a fração fora de pista', () => {
    const { state, track } = quickRace({ track: straight(), totalCars: 1 });
    run(state, track, 60 * 10);
    run(state, track, 60 * 6, () => ({ ...NEUTRAL_INPUT, throttle: true, steer: 1 }));
    const car = humanCar(state);
    expect(Math.abs(car.x)).toBeGreaterThan(1.05);
    expect(car.speed).toBeLessThanOrEqual(carDef(car.carId).topSpeed * OFFROAD_LIMIT_FACTOR + 1);
  });

  it('a curva empurra para fora sem volante, e o volante compensa', () => {
    const curvy = syntheticTrack([{ op: 'straight', length: 50 }, { op: 'curve', length: 2000, curve: 2 }]);
    const a = quickRace({ track: curvy, totalCars: 1 });
    skipCountdown(a.state, a.track);
    run(a.state, a.track, 60 * 12);
    expect(humanCar(a.state).x).toBeLessThan(-0.8); // curva à direita joga para a esquerda (larga em -0.45)
    const b = quickRace({ track: curvy, totalCars: 1 });
    skipCountdown(b.state, b.track);
    run(b.state, b.track, 60 * 12, (s) => ({ ...NEUTRAL_INPUT, throttle: true, steer: humanCar(s).x < 0.2 ? 1 : -1 }));
    expect(Math.abs(humanCar(b.state).x)).toBeLessThan(1.05);
  });

  it('a fração de velocidade que segura uma curva é decrescente na força da curva e melhor com boa dirigibilidade', () => {
    const tornado = carDef('tornado'); const trovao = carDef('trovao');
    expect(holdableSpeedFraction(tornado, 0)).toBe(1);
    expect(holdableSpeedFraction(tornado, 2)).toBeGreaterThan(holdableSpeedFraction(tornado, 6));
    expect(holdableSpeedFraction(tornado, 6)).toBeGreaterThan(holdableSpeedFraction(trovao, 6));
  });

  it('o nitro passa da velocidade máxima, gasta uma carga e acaba', () => {
    const { state, track } = quickRace({ track: straight(), totalCars: 1 });
    run(state, track, 60 * 12);
    const car = humanCar(state);
    run(state, track, 1, () => ({ ...NEUTRAL_INPUT, throttle: true, nitro: true }));
    expect(car.nitroLeft).toBe(NITRO_PER_RACE - 1);
    run(state, track, 140);
    expect(car.speed).toBeGreaterThan(carDef(car.carId).topSpeed * 1.1);
    expect(car.speed).toBeLessThanOrEqual(carDef(car.carId).topSpeed * NITRO_SPEED_MULT + 1e-6);
    run(state, track, 60 * 6);
    expect(car.speed).toBeCloseTo(carDef(car.carId).topSpeed, 0);
  });

  it('sem cargas, apertar nitro gera o evento de negado', () => {
    const { state, track } = quickRace({ track: straight(), totalCars: 1 });
    run(state, track, 60 * 5);
    for (let i = 0; i < NITRO_PER_RACE; i++) { run(state, track, 1, () => ({ ...NEUTRAL_INPUT, throttle: true, nitro: true })); run(state, track, 160); }
    run(state, track, 1, () => ({ ...NEUTRAL_INPUT, throttle: true, nitro: true }));
    expect(state.events.some((e) => e.type === 'nitro_denied')).toBe(true);
  });

  it('o combustível cai acelerando e o box reabastece com o carro devagar', () => {
    const { state, track } = quickRace({ track: straight(), totalCars: 1 });
    run(state, track, 60 * 30);
    const car = humanCar(state);
    expect(car.fuel).toBeLessThan(1);
    const before = car.fuel;
    // Vai para o box (x ≈ 1.55) nos segmentos com box, devagar.
    car.z = 10 * 200; car.x = 1.55; car.speed = 300;
    run(state, track, 30, () => ({ ...NEUTRAL_INPUT, throttle: false, brake: false }));
    expect(car.inPit).toBe(true);
    expect(car.fuel).toBeGreaterThan(before);
  });

  it('o tanque vazio limita a velocidade', () => {
    const { state, track } = quickRace({ track: straight(), totalCars: 1 });
    run(state, track, 60 * 10);
    const car = humanCar(state);
    car.fuel = 0;
    run(state, track, 60 * 8);
    expect(car.speed).toBeLessThan(carDef(car.carId).topSpeed * 0.25);
  });

  it('no contra-relógio o combustível não é gasto', () => {
    const { state, track } = quickRace({ track: straight(), totalCars: 1, timeTrial: true });
    run(state, track, 60 * 20);
    expect(humanCar(state).fuel).toBe(1);
  });

  it('marcha manual limita a velocidade em cada marcha e sobe/desce com a borda do botão', () => {
    const { state, track } = quickRace({ track: straight(), totalCars: 1, manualGear: true });
    run(state, track, 60 * 12);
    const car = humanCar(state);
    expect(car.gear).toBe(0);
    expect(car.speed).toBeCloseTo(carDef(car.carId).topSpeed * GEAR_TOP[0], 0);
    run(state, track, 1, () => ({ ...NEUTRAL_INPUT, throttle: true, gearUp: true }));
    expect(car.gear).toBe(1);
    run(state, track, 60 * 12);
    expect(car.speed).toBeCloseTo(carDef(car.carId).topSpeed * GEAR_TOP[1], 0);
    run(state, track, 1, () => ({ ...NEUTRAL_INPUT, throttle: true, gearDown: true }));
    expect(car.gear).toBe(0);
  });

  it('a IA não usa marcha manual mesmo com a opção ligada', () => {
    const { state, track } = quickRace({ track: straight(), totalCars: 3, manualGear: true, humans: [human(0)], assists: NO_ASSISTS });
    run(state, track, 60 * 12, idle);
    const ai = state.cars.find((c) => c.seat < 0)!;
    expect(ai.speed).toBeGreaterThan(carDef(ai.carId).topSpeed * 0.5);
  });

  it('o acelerador cheio mantém a pose do volante em 0 e a esterçada muda a pose', () => {
    const { state, track } = quickRace({ track: straight(), totalCars: 1 });
    run(state, track, 60 * 5, fullThrottle);
    expect(humanCar(state).steerPose).toBe(0);
    run(state, track, 1, () => ({ ...NEUTRAL_INPUT, throttle: true, steer: -1 }));
    expect(humanCar(state).steerPose).toBe(-1);
  });
});
