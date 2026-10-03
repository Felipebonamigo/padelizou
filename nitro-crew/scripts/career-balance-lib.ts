// Sonda de balanceamento da carreira (usada por tests/career-balance.test.ts e scripts/career-balance.ts).
// O "piloto médio" da economia é um carro humano guiado pelo cérebro da IA com habilidade fixa
// (PROXY_SKILL): o que chega por volta de 4º na primeira copa, de fábrica, contra a IA nível 0 — o mesmo
// piloto que a calibragem de dinheiro supõe (sempre 4º). Corridas inteiras: 20 carros, profissional,
// assistências padrão, todas as voltas da pista.
import { AVERAGE_POSITION } from '../src/core/career';
import { TICK_RATE } from '../src/core/constants';
import { createRace, stepRace } from '../src/core/sim/race';
import { getTrack } from '../src/core/track';
import type { Difficulty, RaceConfig, UpgradeLevels } from '../src/core/types';

/** Habilidade do piloto-proxy: ~4º na primeira copa (profissional, 20 carros). Medida em docs/CARREIRA.md. */
export const PROXY_SKILL = 0.97;
export { AVERAGE_POSITION };
const ALL_ASSISTS = { sharedNitro: true, tow: true, teamDraft: true, catchup: true };
const MAX_TICKS = TICK_RATE * 60 * 20;

/** Posição final do piloto-proxy numa corrida inteira. */
export function proxyRace(trackId: string, aiLevel: number, upgrades: UpgradeLevels, seed: number, skill = PROXY_SKILL, difficulty: Difficulty = 'profissional'): number {
  const track = getTrack(trackId);
  const config: RaceConfig = {
    trackId, laps: track.def.laps, humans: [{ seat: 0, name: 'P1', carId: 'falcao', teamId: 0, color: '#fff', upgrades }],
    totalCars: 20, difficulty, manualGear: false, assists: ALL_ASSISTS, seed, rosterSeed: seed * 7 + 1, aiLevel,
  };
  const state = createRace(config, track);
  const me = state.cars.find((c) => c.seat === 0);
  if (!me) throw new Error('sem o carro humano');
  me.ai = { skill, laneX: 0.3, laneUntil: 0, lookahead: 32, aggression: 0.5 };
  for (let i = 0; i < MAX_TICKS && state.phase !== 'finished'; i++) stepRace(state, track, []);
  return state.results?.find((r) => r.seat === 0)?.position ?? config.totalCars;
}

/** Posições do piloto-proxy em cada pista, para cada semente. */
export function proxyPositions(trackIds: readonly string[], aiLevel: number, upgrades: UpgradeLevels, seeds: readonly number[], skill = PROXY_SKILL, difficulty: Difficulty = 'profissional'): number[] {
  const out: number[] = [];
  for (const seed of seeds) for (const t of trackIds) out.push(proxyRace(t, aiLevel, upgrades, seed, skill, difficulty));
  return out;
}

// As melhorias do piloto médio moram no núcleo (o nível da IA da carreira as segue): reexportadas aqui para a sonda.
export { averagePlayerUpgrades } from '../src/core/career';
