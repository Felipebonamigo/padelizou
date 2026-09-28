// Ciclo de vida da corrida: criação do grid, contagem, passo da simulação e resultado.
import { COUNTDOWN_TICKS, MAX_CARS, TICK_RATE } from '../constants';
import { AI_CAR_POOL } from '../data/cars';
import { AI_DRIVERS, AI_TEAM_ID_BASE, personalityOf, rosterOffsetWith } from '../data/drivers';
import { createRng, nextInt } from '../rng';
import type { CarState, PlayerInput, RaceConfig, RaceState, Track } from '../types';
import { NEUTRAL_INPUT } from '../types';
import { arrangeGrid, extraCars, humanDrivers, updateModes } from '../modes';
import { aiInput, createBrain, DIFFICULTY_SKILL } from './ai';
import { resolveCarCollisions, resolveSpriteCrash } from './collisions';
import { applyTow, computeModifiers } from './coop';
import { stepCarPhysics } from './physics';
import { buildResults, checkRaceOver, updateLaps, updatePositions } from './positions';
import { statsFor } from './stats';

function blankCar(config: RaceConfig, id: number, seat: number, name: string, teamId: number, carId: string): CarState {
  // Atributos efetivos (melhorias da carreira / nível da IA) fixados na largada; o nitro extra vira carga.
  const stats = statsFor(config, { seat, carId });
  return {
    id, seat, name, teamId, carId, z: 0, x: 0, speed: 0, gear: 0, fuel: 1, nitroLeft: stats.nitro, nitroTicks: 0,
    lap: 1, lapTicks: [], lapStartTick: 0, finished: false, finishTick: -1, position: id + 1, progress: 0,
    inPit: false, collisionCooldown: 0, towCooldown: 0, skidTicks: 0, steerPose: 0, ai: null, stats,
  };
}

/** Monta o grid: humanos no fundo (como no Top Gear, você larga em último) e a IA na frente. */
export function createRace(config: RaceConfig, track: Track): RaceState {
  const total = Math.max(config.humans.length, Math.min(MAX_CARS, config.timeTrial ? config.humans.length : config.totalCars));
  const state: RaceState = {
    tick: 0, phase: 'countdown', config, trackId: track.def.id, trackLength: track.length, cars: [],
    rng: createRng(config.seed), teamNitro: {}, startTick: COUNTDOWN_TICKS, firstHumanFinishTick: -1, events: [], results: null,
  };
  // Modos de festa (modes.ts): o revezamento tem um carro por dupla; a escolta, o VIP no lugar de uma IA.
  const drivers = humanDrivers(config);
  const aiCount = Math.max(0, total - drivers.length - extraCars(config));
  const ai: CarState[] = [];
  // IA: nomes em ordem fixa a partir de um deslocamento sorteado, times aos pares. O rival da copa
  // (config.rival) sempre entra; cada piloto traz a própria personalidade (data/drivers.ts).
  const roster = createRng(config.rosterSeed ?? config.seed);
  const nameOffset = rosterOffsetWith(nextInt(roster, 0, AI_DRIVERS.length - 1), aiCount, config.rival);
  for (let i = 0; i < aiCount; i++) {
    const driverIndex = (nameOffset + i) % AI_DRIVERS.length;
    const name = AI_DRIVERS[driverIndex];
    const car = blankCar(config, ai.length, -1, name, AI_TEAM_ID_BASE + Math.floor(driverIndex / 2), AI_CAR_POOL[nextInt(roster, 0, AI_CAR_POOL.length - 1)].id);
    car.ai = createBrain(state, config.difficulty, i, { personality: personalityOf(name), rival: name === config.rival });
    ai.push(car);
  }
  const humanCars = drivers.map((d, i) => blankCar(config, aiCount + i, d.seat, d.name, d.teamId, d.carId));
  const cars = arrangeGrid(state, ai, humanCars, (seat, name, teamId, carId) => blankCar(config, -1, seat, name, teamId, carId));

  // Grid 2 a 2: o primeiro da lista larga na frente. Humanos ficam por último.
  const gridGap = 260;
  cars.forEach((c, i) => {
    const row = Math.floor(i / 2);
    c.z = track.length - 600 - row * gridGap;
    c.x = i % 2 === 0 ? -0.45 : 0.45;
    // Começam "atrás" da linha: z alto na volta 0 → a primeira passagem pela linha vira a volta 1.
    c.lap = 0;
  });
  // Cofre da equipe: as cargas de cada humano, com o nitro extra das melhorias.
  for (const c of cars) if (c.seat >= 0 && config.assists.sharedNitro) state.teamNitro[c.teamId] = (state.teamNitro[c.teamId] ?? 0) + c.stats.nitro;
  state.cars = cars;
  updatePositions(state, track);
  return state;
}

/** Um tick. `inputs[seat]` é o comando do humano daquele assento (ausente = neutro). */
export function stepRace(state: RaceState, track: Track, inputs: ReadonlyArray<PlayerInput | undefined>): void {
  state.events = [];
  if (state.phase === 'finished') { state.tick++; return; }

  if (state.phase === 'countdown') {
    const remaining = COUNTDOWN_TICKS - state.tick;
    if (remaining % TICK_RATE === 0 && remaining > 0 && remaining / TICK_RATE <= 3) state.events.push({ type: 'countdown', value: remaining / TICK_RATE });
    if (state.tick >= COUNTDOWN_TICKS) { state.phase = 'racing'; state.startTick = state.tick; state.events.push({ type: 'go' }); for (const c of state.cars) c.lapStartTick = state.tick; }
  }

  for (const car of state.cars) {
    const command = inputs[car.seat];
    if (command?.takeover && !car.ai) car.ai = takeoverBrain(state);
    const input = car.ai ? aiInput(state, track, car) : (car.finished ? cruiseInput(state, track, car) : (command ?? NEUTRAL_INPUT));
    const mods = computeModifiers(state, track, car);
    const prevZ = car.z;
    stepCarPhysics(state, track, car, input, mods);
    resolveSpriteCrash(state, track, car);
    updateLaps(state, track, car, prevZ);
  }
  resolveCarCollisions(state, track);
  applyTow(state, track);
  updatePositions(state, track);
  updateModes(state, track);

  if (checkRaceOver(state)) {
    state.phase = 'finished';
    state.results = buildResults(state);
    state.events.push({ type: 'race_over' });
  }
  state.tick++;
}

/** Cérebro de quem assume o carro de um humano: fixo pela dificuldade, sem sorteio. */
function takeoverBrain(state: RaceState) {
  const [lo, hi] = DIFFICULTY_SKILL[state.config.difficulty];
  return { skill: (lo + hi) / 2, laneX: 0, laneUntil: state.tick, lookahead: 25, aggression: 0.3 };
}

/** Humano que já terminou segue em piloto automático, para os outros verem o carro. */
function cruiseInput(state: RaceState, track: Track, car: CarState): PlayerInput {
  if (!car.ai) car.ai = { skill: 0.6, laneX: 0.5, laneUntil: 0, lookahead: 25, aggression: 0 };
  const input = aiInput(state, track, car);
  input.nitro = false;
  return input;
}

/** Tempo de corrida em segundos, a partir do "JÁ". */
export function raceSeconds(state: RaceState): number {
  return Math.max(0, state.tick - state.startTick) / TICK_RATE;
}

/** Formata ticks como m:ss.cc. */
export function formatTicks(ticks: number): string {
  if (ticks < 0) return '--:--.--';
  const total = ticks / TICK_RATE;
  const m = Math.floor(total / 60);
  const s = Math.floor(total % 60);
  const c = Math.floor((total * 100) % 100);
  return `${m}:${s.toString().padStart(2, '0')}.${c.toString().padStart(2, '0')}`;
}
