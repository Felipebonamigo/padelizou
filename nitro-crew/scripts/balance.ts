// Balanceamento: corre IA x IA em cada pista, sem interface, e imprime voltas, ritmo e incidentes.
// Uso: npx tsx scripts/balance.ts [segundos=150 | corrida] [dificuldade=profissional] [semente=11] [pista]
// (pelo npm: npm run balance -- corrida amador 12)
// - segundos: 3 voltas de corrida, cortada no tempo dado; o carro humano fica parado na largada.
// - corrida: a corrida inteira (as voltas da pista), com o piloto médio da calibragem (PROXY_SKILL, Falcão de
//   fábrica, scripts/career-balance-lib.ts) no lugar do carro parado. Imprime também a duração da corrida (tempo do
//   vencedor) e a posição de chegada do piloto médio — a dificuldade medida da pista (docs/PISTAS.md, "Balanceamento").
// "v média" é a velocidade da IA ÷ velocidade máxima do carro, na média de todos os ticks de corrida.
import { TICK_RATE } from '../src/core/constants';
import { carDef } from '../src/core/data/cars';
import { createRace, stepRace, formatTicks } from '../src/core/sim/race';
import { getTrack, TRACKS } from '../src/core/track';
import type { Difficulty } from '../src/core/types';
import { PROXY_SKILL } from './career-balance-lib';

const full = process.argv[2] === 'corrida';
const seconds = full ? 0 : Number(process.argv[2] ?? 150);
const difficulty = (process.argv[3] ?? 'profissional') as Difficulty;
const seed = Number(process.argv[4] ?? 11);
const only = process.argv[5];
/** Teto da corrida inteira (nenhuma pista chega perto: a mais longa dá ~6 min). */
const RACE_CAP_TICKS = TICK_RATE * 60 * 20;
/** Média das voltas da IA por pista (para comparar antes/depois de mexer na IA; linha "média geral" no fim). */
const means: number[] = [];

for (const def of TRACKS) {
  if (only && def.id !== only) continue;
  const track = getTrack(def.id);
  const laps = full ? def.laps : 3;
  const state = createRace({ trackId: def.id, laps, humans: [{ seat: 0, name: 'P1', carId: 'falcao', teamId: 0, color: '#fff' }], totalCars: 20, difficulty, manualGear: false, assists: { sharedNitro: false, tow: false, teamDraft: false, catchup: false }, seed }, track);
  const me = state.cars.find((c) => c.seat === 0);
  if (full && me) me.ai = { skill: PROXY_SKILL, laneX: 0.3, laneUntil: 0, lookahead: 32, aggression: 0.5 };
  let crashes = 0, collisions = 0, pits = 0, off = 0, samples = 0, speedSum = 0;
  const t0 = Date.now();
  const ticks = full ? RACE_CAP_TICKS : TICK_RATE * seconds;
  for (let i = 0; i < ticks && state.phase !== 'finished'; i++) {
    stepRace(state, track, []);
    for (const e of state.events) { if (e.type === 'crash') crashes++; if (e.type === 'collision') collisions++; if (e.type === 'pit_enter') pits++; }
    if (state.phase === 'racing') {
      for (const c of state.cars) {
        if (c.seat >= 0) continue;
        samples++;
        speedSum += c.speed / carDef(c.carId).topSpeed;
        if (Math.abs(c.x) > 1.05) off++;
      }
    }
  }
  const ms = Date.now() - t0;
  const ai = state.cars.filter((c) => c.seat < 0);
  const lapTimes = ai.flatMap((c) => c.lapTicks);
  const best = lapTimes.length ? Math.min(...lapTimes) : -1;
  const worst = lapTimes.length ? Math.max(...lapTimes) : -1;
  const mean = lapTimes.length ? lapTimes.reduce((a, b) => a + b, 0) / lapTimes.length : -1;
  if (mean > 0) means.push(mean);
  const avgV = speedSum / Math.max(1, samples);
  const minLap = Math.min(...ai.map((c) => c.lap)); const maxLap = Math.max(...ai.map((c) => c.lap));
  const head = `${def.id.padEnd(18)} dif ${def.difficulty} voltas ${laps} segs=${track.segments.length}`;
  const pace = `média ${formatTicks(Math.round(mean))} melhor volta ${formatTicks(best)} pior ${formatTicks(worst)}`;
  const incidents = `grama ${(100 * off / Math.max(1, samples)).toFixed(1)}% | batidas ${collisions} cenário ${crashes} box ${pits} | v média ${avgV.toFixed(2)}`;
  if (full) {
    const winner = state.results?.find((r) => r.position === 1);
    const mine = state.results?.find((r) => r.seat === 0)?.position ?? 20;
    console.log(`${head} | corrida ${formatTicks(winner?.totalTicks ?? -1)} | ${pace} | piloto médio ${mine}º | ${incidents} | ${ms} ms de CPU`);
  } else {
    console.log(`${head} voltas IA ${minLap}–${maxLap} | ${pace} | ${incidents} | ${ms} ms de CPU para ${seconds}s`);
  }
}
if (means.length > 1) console.log(`média geral das voltas da IA: ${(means.reduce((a, b) => a + b, 0) / means.length / TICK_RATE).toFixed(2)} s em ${means.length} pistas`);
