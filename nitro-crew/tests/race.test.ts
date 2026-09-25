import { describe, it, expect } from 'vitest';
import { COUNTDOWN_TICKS, FINISH_GRACE_TICKS, POINTS_TABLE, TICK_RATE } from '../src/core/constants';
import { formatTicks, stepRace } from '../src/core/sim/race';
import { NEUTRAL_INPUT } from '../src/core/types';
import { human, humanCar, idle, quickRace, run, skipCountdown, syntheticTrack } from './helpers';

describe('corrida', () => {
  it('a contagem emite 3, 2, 1 e "JÁ" e só então a corrida começa', () => {
    const { state, track } = quickRace({ track: syntheticTrack(), totalCars: 2 });
    const seen: string[] = [];
    while (state.phase === 'countdown') {
      stepRace(state, track, []);
      for (const e of state.events) if (e.type === 'countdown') seen.push(String(e.value)); else if (e.type === 'go') seen.push('go');
    }
    expect(seen).toEqual(['3', '2', '1', 'go']);
    expect(state.startTick).toBe(COUNTDOWN_TICKS);
  });

  it('o grid põe os humanos atrás da IA e ninguém sobreposto', () => {
    const { state } = quickRace({ totalCars: 20, humans: [human(0), human(1)] });
    const ai = state.cars.filter((c) => c.seat < 0); const hs = state.cars.filter((c) => c.seat >= 0);
    expect(ai.length).toBe(18);
    expect(Math.max(...hs.map((c) => c.z))).toBeLessThan(Math.min(...ai.map((c) => c.z)) + 1);
    const positions = new Set(state.cars.map((c) => `${c.z.toFixed(0)}:${c.x}`));
    expect(positions.size).toBe(20);
    expect(hs.every((c) => c.position >= 19)).toBe(true);
  });

  it('cruzar a linha conta volta, registra o tempo e termina depois das voltas configuradas', () => {
    const track = syntheticTrack([{ op: 'straight', length: 400 }]);
    const { state } = quickRace({ track, totalCars: 1, laps: 2 });
    const car = humanCar(state);
    const laps: number[] = [];
    let finishPos = -1;
    for (let i = 0; i < 60 * 60 && state.phase !== 'finished'; i++) {
      stepRace(state, track, [{ ...NEUTRAL_INPUT, throttle: true }]);
      for (const e of state.events) { if (e.type === 'lap') laps.push(e.lap); if (e.type === 'finish') finishPos = e.position; }
    }
    expect(laps).toEqual([2, 3]);
    expect(car.finished).toBe(true);
    expect(car.lapTicks.length).toBe(2);
    expect(finishPos).toBe(1);
    expect(state.phase).toBe('finished');
    expect(state.results![0].points).toBe(POINTS_TABLE[0]);
    expect(state.results![0].totalTicks).toBe(car.finishTick - state.startTick);
    expect(state.results![0].bestLapTicks).toBe(Math.min(...car.lapTicks));
  });

  it('a primeira passagem pela linha (saindo do grid) não vale como volta', () => {
    const track = syntheticTrack([{ op: 'straight', length: 400 }]);
    const { state } = quickRace({ track, totalCars: 1, laps: 2 });
    run(state, track, 60 * 8);
    const car = humanCar(state);
    expect(car.lap).toBe(1);
    expect(car.lapTicks.length).toBe(0);
  });

  it('as posições seguem o progresso e o líder é a posição 1', () => {
    const { state, track } = quickRace({ totalCars: 6 });
    run(state, track, 60 * 20, idle);
    const sorted = state.cars.slice().sort((a, b) => a.position - b.position);
    for (let i = 1; i < sorted.length; i++) expect(sorted[i - 1].progress).toBeGreaterThanOrEqual(sorted[i].progress);
    expect(sorted[0].position).toBe(1);
    expect(humanCar(state).position).toBe(6);
  });

  it('com o humano parado, a corrida acaba pela tolerância depois que alguém terminaria… não: só humanos contam', () => {
    // Nenhum humano terminou → sem tolerância correndo; a corrida segue.
    const { state, track } = quickRace({ track: syntheticTrack([{ op: 'straight', length: 300 }]), totalCars: 3, laps: 1 });
    run(state, track, FINISH_GRACE_TICKS + 60 * 30, idle);
    expect(state.phase).toBe('racing');
    expect(state.cars.filter((c) => c.seat < 0).every((c) => c.finished)).toBe(true);
  });

  it('quando o primeiro humano termina, os outros têm a tolerância e depois a corrida fecha com posições por progresso', () => {
    const track = syntheticTrack([{ op: 'straight', length: 300 }]);
    const { state } = quickRace({ track, totalCars: 2, laps: 1, humans: [human(0), human(1, 1)] });
    skipCountdown(state, track);
    const input = (_: unknown, seat: number) => ({ ...NEUTRAL_INPUT, throttle: seat === 0 });
    run(state, track, 60 * 40, input);
    expect(humanCar(state, 0).finished).toBe(true);
    expect(state.phase).toBe('racing');
    run(state, track, FINISH_GRACE_TICKS, input);
    expect(state.phase).toBe('finished');
    const p2 = state.results!.find((r) => r.seat === 1)!;
    expect(p2.finished).toBe(false);
    expect(p2.position).toBe(2);
    expect(p2.totalTicks).toBe(-1);
  });

  it('humano que terminou segue em piloto automático', () => {
    const track = syntheticTrack([{ op: 'straight', length: 300 }]);
    const { state } = quickRace({ track, totalCars: 1, laps: 1, humans: [human(0), human(1, 1)] });
    skipCountdown(state, track);
    run(state, track, 60 * 40, (_, seat) => ({ ...NEUTRAL_INPUT, throttle: seat === 0 }));
    const p1 = humanCar(state, 0);
    expect(p1.finished).toBe(true);
    const z0 = p1.z;
    run(state, track, 60, idle);
    expect(p1.z).not.toBe(z0);
  });

  it('formatTicks escreve m:ss.cc', () => {
    expect(formatTicks(TICK_RATE * 65 + 30)).toBe('1:05.50');
    expect(formatTicks(-1)).toBe('--:--.--');
  });
});
