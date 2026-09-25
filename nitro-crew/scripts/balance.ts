// Balanceamento: corre IA x IA em cada pista, sem interface, e imprime voltas, ritmo e incidentes.
// Uso: npx tsx scripts/balance.ts [segundos=150] [dificuldade=profissional] [semente=11] [pista]
import { TICK_RATE } from '../src/core/constants';
import { carDef } from '../src/core/data/cars';
import { createRace, stepRace, formatTicks } from '../src/core/sim/race';
import { getTrack, TRACKS } from '../src/core/track';
import type { Difficulty } from '../src/core/types';

const seconds = Number(process.argv[2] ?? 150);
const difficulty = (process.argv[3] ?? 'profissional') as Difficulty;
const seed = Number(process.argv[4] ?? 11);
const only = process.argv[5];

for (const def of TRACKS) {
  if (only && def.id !== only) continue;
  const track = getTrack(def.id);
  const state = createRace({ trackId: def.id, laps: 3, humans: [{ seat: 0, name: 'P1', carId: 'falcao', teamId: 0, color: '#fff' }], totalCars: 20, difficulty, manualGear: false, assists: { sharedNitro: false, tow: false, teamDraft: false, catchup: false }, seed }, track);
  let crashes = 0, collisions = 0, pits = 0, off = 0, samples = 0;
  const t0 = Date.now();
  for (let i = 0; i < TICK_RATE * seconds; i++) {
    stepRace(state, track, []);
    for (const e of state.events) { if (e.type === 'crash') crashes++; if (e.type === 'collision') collisions++; if (e.type === 'pit_enter') pits++; }
    if (state.phase === 'racing') for (const c of state.cars) if (c.seat < 0) { samples++; if (Math.abs(c.x) > 1.05) off++; }
  }
  const ms = Date.now() - t0;
  const ai = state.cars.filter((c) => c.seat < 0);
  const laps = ai.flatMap((c) => c.lapTicks);
  const best = laps.length ? Math.min(...laps) : -1;
  const worst = laps.length ? Math.max(...laps) : -1;
  const avgV = ai.reduce((a, c) => a + c.speed / carDef(c.carId).topSpeed, 0) / ai.length;
  const minLap = Math.min(...ai.map((c) => c.lap)); const maxLap = Math.max(...ai.map((c) => c.lap));
  console.log(`${def.id.padEnd(14)} segs=${track.segments.length} voltas IA ${minLap}–${maxLap} | melhor volta ${formatTicks(best)} pior ${formatTicks(worst)} | grama ${(100 * off / Math.max(1, samples)).toFixed(1)}% | batidas ${collisions} cenário ${crashes} box ${pits} | v média ${avgV.toFixed(2)} | ${ms} ms de CPU para ${seconds}s`);
}
