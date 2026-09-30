import { describe, it, expect } from 'vitest';
import { CAR_HALF_WIDTH, CAR_LENGTH, TICK_RATE } from '../src/core/constants';
import { AI_CAR_POOL, carDef } from '../src/core/data/cars';
import { TRACKS } from '../src/core/track/tracks';
import { getTrack } from '../src/core/track';
import { aiInput } from '../src/core/sim/ai';
import { wrappedDelta } from '../src/core/sim/collisions';
import { NEUTRAL_INPUT } from '../src/core/types';
import { ALL_ASSISTS, human, humanCar, idle, quickRace, run, skipCountdown, syntheticTrack } from './helpers';

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
  // inteira. Campeão é quem mais gasta por volta; o grid tem todos os carros da IA (o sorteio do roster
  // escolhe os modelos, então a semente é a primeira que põe os 7 livres na pista); o humano fica
  // parado para a corrida só acabar quando toda a IA cruzar a linha.
  it.each(TRACKS.map((d) => [d.id, d.laps] as const))('%s: corrida inteira (%i voltas) — nenhum carro da IA fica sem combustível', (id, laps) => {
    const track = getTrack(id);
    let state = quickRace({ track, totalCars: 10, laps, difficulty: 'campeao', seed: 1 }).state;
    // A IA só corre com os carros do AI_CAR_POOL (os comprados na carreira ficam para os humanos). Comparar
    // com CARS (8 depois da carreira) fazia o laço procurar para sempre um grid impossível; o limite de
    // sementes transforma isso numa falha com mensagem em vez de um teste que nunca termina.
    let seed = 2;
    for (; new Set(state.cars.filter((c) => c.seat < 0).map((c) => c.carId)).size < AI_CAR_POOL.length && seed < 500; seed++) {
      state = quickRace({ track, totalCars: 10, laps, difficulty: 'campeao', seed }).state;
    }
    expect(seed, `${id}: nenhuma semente pôs os ${AI_CAR_POOL.length} carros da IA no grid`).toBeLessThan(500);
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

  // Onda F: com a colisão do tamanho do carro na tela, um não atravessa mais o outro. A IA que chegava por fora
  // de um carro parado perto da borda escolhia passar por fora, a faixa batia no limite de 0,7 e sobrava 0,2 de
  // lado — menos que a largura do carro: ficava batendo na traseira dele para sempre (antes passava por dentro).
  it.each([0.5, -0.5])('carro parado perto da borda (x %f): a IA que vem por fora passa pelo outro lado, sem ficar batendo', (x) => {
    const { state, track } = quickRace({ track: syntheticTrack(), totalCars: 2, humans: [human(0)], seed: 3 });
    skipCountdown(state, track);
    const parked = humanCar(state, 0);
    const ai = state.cars.find((c) => c.seat < 0);
    if (!ai?.ai) throw new Error('sem IA');
    delete ai.ai.personality;
    parked.z = 20000; parked.x = x; parked.speed = 0;
    ai.z = 20000 - 1500; ai.x = x + Math.sign(x) * 0.12; ai.speed = 3000; ai.ai.laneX = ai.x; ai.ai.laneUntil = 0;
    let hits = 0;
    for (let i = 0; i < TICK_RATE * 10; i++) {
      run(state, track, 1, idle);
      hits += state.events.filter((e) => e.type === 'collision').length;
    }
    expect(wrappedDelta(ai.z, parked.z, track.length), 'a IA passou o carro parado').toBeGreaterThan(CAR_LENGTH);
    expect(hits).toBeLessThanOrEqual(1);
  });

  // Onda F: sem o tranco fixo da colisão antiga (que empurrava o outro para longe), a IA que esterçava para uma faixa
  // com um carro do lado ficava empurrando-o de lado, os dois perdendo velocidade — e, indo para o box com alguém
  // ao lado, perdia a entrada e secava (tests abaixo: "o gasto que sobe com o elástico", Rochosas).
  it('a IA não esterça para dentro de um carro que está do lado: espera ele sair antes de trocar de faixa', () => {
    const { state, track } = quickRace({ track: syntheticTrack(), totalCars: 2, humans: [human(0)], seed: 3 });
    skipCountdown(state, track);
    const other = humanCar(state, 0);
    const ai = state.cars.find((c) => c.seat < 0);
    if (!ai?.ai) throw new Error('sem IA');
    delete ai.ai.personality;
    ai.z = 20000; ai.x = -0.35; ai.speed = 4000;
    ai.ai.laneX = 0.5; ai.ai.laneUntil = state.tick + TICK_RATE * 10; // quer ir para a direita, onde está o outro
    other.z = 20000 - 60; other.x = 0; other.speed = 4000;
    let closest = Infinity; let pushed = 0;
    for (let i = 0; i < TICK_RATE * 2; i++) {
      run(state, track, 1, (s) => ({ ...NEUTRAL_INPUT, throttle: true, steer: (0 - humanCar(s, 0).x) * 5 }));
      closest = Math.min(closest, Math.abs(ai.x - other.x));
      pushed = Math.max(pushed, Math.abs(other.x));
    }
    expect(closest, 'encostou de lado').toBeGreaterThanOrEqual(CAR_HALF_WIDTH * 2);
    expect(pushed, 'empurrou o outro').toBeLessThan(0.01);
  });

  it('indo para o box com um carro PARADO no caminho, a IA não fica esperando ao lado dele: passa e entra', () => {
    // Tirar o pé para passar por trás de quem está do lado só serve com ele andando: parado, esperar é para sempre.
    const track = syntheticTrack([{ op: 'pit', length: 40 }, { op: 'straight', length: 260 }], 'sintetica-box');
    const { state } = quickRace({ track, totalCars: 2, humans: [human(0)], seed: 3 });
    skipCountdown(state, track);
    const parked = humanCar(state, 0);
    const ai = state.cars.find((c) => c.seat < 0);
    if (!ai?.ai) throw new Error('sem IA');
    delete ai.ai.personality;
    ai.lap = 1; ai.z = track.length - 1500; ai.x = 0.3; ai.speed = 800; ai.fuel = 0.05;
    parked.lap = 1; parked.z = ai.z + 60; parked.x = 0.9; parked.speed = 0;
    let pitted = false;
    // 1.500 u até o box a 800 u/s: uns 2 s sem esperar; esperando ao lado do parado, mais de 8.
    for (let i = 0; i < TICK_RATE * 5 && !pitted; i++) {
      run(state, track, 1, idle);
      pitted = ai.inPit;
    }
    expect(pitted, `a IA ficou em z ${ai.z.toFixed(0)} a ${ai.speed.toFixed(0)} u/s, o parado em z ${parked.z.toFixed(0)}`).toBe(true);
  });

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

  it('o elástico só acelera a IA que ficou atrás de TODOS os humanos, não a que está no meio deles (onda D)', () => {
    // Mirando o melhor humano, a IA atrás do líder e na frente do último andava 4,6% mais rápido — contra o
    // humano que já ia mal. Medido: sem isso o pior de 4 humanos médios ganhava 2,6 posições.
    const track = syntheticTrack([{ op: 'straight', length: 4000 }]);
    const { state } = quickRace({ track, humans: [human(0), human(1)], totalCars: 4, difficulty: 'profissional', assists: ALL_ASSISTS, seed: 3 });
    skipCountdown(state, track);
    const [lead, last] = [humanCar(state, 0), humanCar(state, 1)];
    const [middle, back] = state.cars.filter((c) => c.seat < 0);
    const place = (c: typeof lead, z: number, x: number) => { c.z = z; c.x = x; c.lap = 1; };
    place(lead, 400_000, -0.6); place(last, 200_000, 0.6);
    place(middle, 300_000, 0); place(back, 100_000, 0);
    for (const c of [middle, back]) { c.carId = 'falcao'; c.stats = { ...lead.stats }; c.ai = { ...c.ai!, skill: 0.95, laneX: 0, laneUntil: 1e9 }; c.speed = c.stats.topSpeed * 0.8; }
    run(state, track, TICK_RATE * 8, idle);
    expect(back.speed).toBeGreaterThan(middle.speed * 1.03);
  });

  it('o elástico deixa a IA mais lenta quando dispara à frente do humano (amador)', () => {
    const track = getTrack('rota_66');
    const { state } = quickRace({ track, totalCars: 4, difficulty: 'amador', seed: 2 });
    run(state, track, TICK_RATE * 30, idle);
    const ai = state.cars.filter((c) => c.seat < 0);
    for (const c of ai) expect(c.speed).toBeLessThan(carDef(c.carId).topSpeed * 0.9);
  });
});
