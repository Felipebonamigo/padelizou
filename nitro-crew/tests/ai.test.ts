import { describe, it, expect } from 'vitest';
import { TICK_RATE } from '../src/core/constants';
import { carDef, CARS } from '../src/core/data/cars';
import { TRACKS } from '../src/core/track/tracks';
import { getTrack } from '../src/core/track';
import { aiInput } from '../src/core/sim/ai';
import { ALL_ASSISTS, human, humanCar, idle, quickRace, run } from './helpers';

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

  // 25/09: o tanque foi calibrado para voltas de ~400.000 unidades e a IA só parava abaixo de 22%;
  // nas pistas longas das copas novas o Trovão chegava à última volta com ~0,33, não parava e secava
  // antes da chegada. A primeira volta (teste acima, balance de 150 s) não mostra isso: só a corrida
  // inteira. Campeão é quem mais gasta por volta; o grid tem os quatro carros (o sorteio do roster
  // escolhe os modelos, então a semente é a primeira que põe os quatro na pista); o humano fica
  // parado para a corrida só acabar quando toda a IA cruzar a linha.
  it.each(TRACKS.map((d) => [d.id, d.laps] as const))('%s: corrida inteira (%i voltas) — nenhum carro da IA fica sem combustível', (id, laps) => {
    const track = getTrack(id);
    let state = quickRace({ track, totalCars: 10, laps, difficulty: 'campeao', seed: 1 }).state;
    for (let seed = 2; new Set(state.cars.filter((c) => c.seat < 0).map((c) => c.carId)).size < CARS.length; seed++) {
      state = quickRace({ track, totalCars: 10, laps, difficulty: 'campeao', seed }).state;
    }
    const ai = state.cars.filter((c) => c.seat < 0);
    const empty: string[] = [];
    for (let i = 0; i < TICK_RATE * 900 && !ai.every((c) => c.finished); i++) {
      const racing = new Set(ai.filter((c) => !c.finished).map((c) => c.id));
      run(state, track, 1, idle);
      for (const e of state.events) {
        if (e.type !== 'fuel_empty' || !racing.has(e.carId)) continue;
        const c = state.cars[e.carId];
        empty.push(`${c.name} (${c.carId}) na volta ${c.lap}`);
      }
    }
    expect(ai.every((c) => c.finished), `${id}: IA sem terminar`).toBe(true);
    expect(empty, `${id}: sem combustível`).toEqual([]);
  }, 60_000);

  // O elástico muda o gasto no meio da corrida: atrás do humano, a IA acelera o tempo todo e gasta
  // ~50% a mais por volta. Rochosas (semente 7): com a média desde a largada, um Tornado gastou 0,25 na
  // 1ª volta e 0,37 na 2ª (já atrás), chegou à 3ª com 0,376, a média dizia 0,31, passou reto e secou.
  // Autobahn (semente 42): um Trovão gastou 0,30 na 2ª volta, passou reto com 0,348, ficou para trás
  // dos humanos na 3ª, acelerou em 93% dela e secou em 2,76 voltas. Montagem do scripts/smoke.ts:
  // 2 humanos em piloto automático e 18 carros de IA, campeão, todas as assistências.
  it.each([['rochosas', 7], ['autobahn', 42]] as const)('%s (semente %i): o gasto que sobe com o elástico no meio da corrida não seca a IA', (id, seed) => {
    const track = getTrack(id);
    const { state } = quickRace({ track, humans: [human(0, 0, 'falcao'), human(1, 0, 'trovao')], totalCars: 20, laps: track.def.laps, difficulty: 'campeao', assists: ALL_ASSISTS, seed });
    for (const c of state.cars) if (c.seat >= 0) c.ai = { skill: 0.97, laneX: 0.2 * (c.seat - 1.5), laneUntil: 0, lookahead: 28, aggression: 0.6 };
    const empty: string[] = [];
    for (let i = 0; i < TICK_RATE * 900 && state.phase !== 'finished'; i++) {
      const racing = new Set(state.cars.filter((c) => !c.finished).map((c) => c.id));
      run(state, track, 1, (s, seat) => aiInput(s, track, humanCar(s, seat))); // como o smoke.ts
      for (const e of state.events) if (e.type === 'fuel_empty' && racing.has(e.carId)) empty.push(`${state.cars[e.carId].carId} na volta ${state.cars[e.carId].lap}`);
    }
    expect(empty).toEqual([]);
  }, 60_000);

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
