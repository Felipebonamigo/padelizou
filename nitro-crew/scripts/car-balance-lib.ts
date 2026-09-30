// Balanceamento dos carros por dados (docs/CARROS.md): o MESMO piloto — o cérebro da IA com habilidade
// fixa, no assento humano — corre com cada carro. Duas medidas, corridas inteiras com combustível e box:
//  - solo: sozinho na pista (sem tráfego), tempo total da corrida → o ritmo puro do carro, com o box que
//    o consumo dele exige;
//  - grade: 20 carros, profissional, contra a IA de sempre → posição de chegada (tráfego, batidas, vácuo).
// Usado por scripts/car-balance.ts (a tabela) e por tests/cars.test.ts (a trava: nenhum carro domina nem
// é inútil).
import { MAX_CARS, TICK_RATE } from '../src/core/constants';
import { createRace, stepRace } from '../src/core/sim/race';
import { getTrack } from '../src/core/track';
import type { AiBrain, Difficulty, RaceConfig } from '../src/core/types';
import { PROXY_SKILL } from './career-balance-lib';

const ALL_ASSISTS = { sharedNitro: true, tow: true, teamDraft: true, catchup: true };
const MAX_TICKS = TICK_RATE * 60 * 25;

/** O piloto da medida solo: habilidade 1 no campeão (reta a 100% da máxima do carro). */
export const SOLO_SKILL = 1;

function proxyBrain(skill: number): AiBrain {
  return { skill, laneX: 0.3, laneUntil: 0, lookahead: 32, aggression: 0.5 };
}

/** Tempo da corrida inteira (ticks, do "JÁ" à chegada) do carro sozinho na pista. */
export function soloRaceTicks(trackId: string, carId: string, seed = 1, skill = SOLO_SKILL, difficulty: Difficulty = 'campeao'): number {
  const track = getTrack(trackId);
  const config: RaceConfig = {
    trackId, laps: track.def.laps, humans: [{ seat: 0, name: 'P1', carId, teamId: 0, color: '#fff' }],
    totalCars: 1, difficulty, manualGear: false, assists: ALL_ASSISTS, seed,
  };
  const state = createRace(config, track);
  const me = state.cars[0];
  me.ai = proxyBrain(skill);
  for (let i = 0; i < MAX_TICKS && state.phase !== 'finished'; i++) stepRace(state, track, []);
  return me.finished ? me.finishTick - state.startTick : MAX_TICKS;
}

/** Posição final do piloto-proxy (PROXY_SKILL) com o carro dado numa corrida inteira de 20 carros. */
export function gridPosition(trackId: string, carId: string, seed: number, skill = PROXY_SKILL, difficulty: Difficulty = 'profissional'): number {
  const track = getTrack(trackId);
  const config: RaceConfig = {
    trackId, laps: track.def.laps, humans: [{ seat: 0, name: 'P1', carId, teamId: 0, color: '#fff' }],
    totalCars: MAX_CARS, difficulty, manualGear: false, assists: ALL_ASSISTS, seed, rosterSeed: seed * 7 + 1,
  };
  const state = createRace(config, track);
  const me = state.cars.find((c) => c.seat === 0);
  if (!me) throw new Error('sem o carro humano');
  me.ai = proxyBrain(skill);
  for (let i = 0; i < MAX_TICKS && state.phase !== 'finished'; i++) stepRace(state, track, []);
  return state.results?.find((r) => r.seat === 0)?.position ?? MAX_CARS;
}

export interface CarSummary {
  carId: string;
  /** Média, pelas pistas, do tempo solo relativo ao do carro de referência naquela pista (1 = igual a ele). */
  relTime: number;
  /** Pistas em que foi o mais rápido e o mais lento (solo). */
  wins: number;
  lasts: number;
  /** Melhor e pior tempo relativo numa pista. */
  best: number;
  worst: number;
}

/**
 * Resumo do solo: `times[car][track]` em ticks. A referência de cada pista é o carro `ref` (o Falcão GT, o
 * "equilibrado") quando ele foi medido; sem ele, a média dos carros medidos.
 */
export function summarizeSolo(times: Record<string, Record<string, number>>, ref = 'falcao'): CarSummary[] {
  const cars = Object.keys(times);
  const tracks = Object.keys(times[cars[0]] ?? {});
  const rel: Record<string, number[]> = Object.fromEntries(cars.map((c) => [c, []]));
  const wins: Record<string, number> = Object.fromEntries(cars.map((c) => [c, 0]));
  const lasts: Record<string, number> = Object.fromEntries(cars.map((c) => [c, 0]));
  for (const t of tracks) {
    const vals = cars.map((c) => times[c][t]);
    const mean = times[ref]?.[t] ?? vals.reduce((a, b) => a + b, 0) / vals.length;
    const min = Math.min(...vals); const max = Math.max(...vals);
    for (const c of cars) {
      rel[c].push(times[c][t] / mean);
      if (times[c][t] === min) wins[c]++;
      if (times[c][t] === max) lasts[c]++;
    }
  }
  return cars.map((c) => ({
    carId: c, relTime: rel[c].reduce((a, b) => a + b, 0) / Math.max(1, rel[c].length), wins: wins[c], lasts: lasts[c],
    best: Math.min(...rel[c]), worst: Math.max(...rel[c]),
  }));
}
