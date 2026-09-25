// Corrida completa sem interface: um humano em piloto automático + IA, até o resultado.
// Uso: npx tsx scripts/smoke.ts [pista=copacabana] [humanos=2] [dificuldade=profissional] [semente=42]
import { TICK_RATE } from '../src/core/constants';
import { createRace, stepRace, formatTicks } from '../src/core/sim/race';
import { aiInput } from '../src/core/sim/ai';
import { getTrack } from '../src/core/track';
import { SEAT_COLORS } from '../src/core/data/drivers';
import { CARS } from '../src/core/data/cars';
import { createChampionship, applyRaceResult, teamRaceRank } from '../src/core/championship';
import type { Difficulty, HumanEntry, PlayerInput } from '../src/core/types';

const trackId = process.argv[2] ?? 'copacabana';
const humanCount = Math.min(4, Math.max(1, Number(process.argv[3] ?? 2)));
const difficulty = (process.argv[4] ?? 'profissional') as Difficulty;
const seed = Number(process.argv[5] ?? 42);

const track = getTrack(trackId);
const humans: HumanEntry[] = Array.from({ length: humanCount }, (_, i) => ({ seat: i, name: `P${i + 1}`, carId: CARS[i % CARS.length].id, teamId: 0, color: SEAT_COLORS[i] }));
const state = createRace({ trackId, laps: track.def.laps, humans, totalCars: 20, difficulty, manualGear: false, assists: { sharedNitro: true, tow: true, teamDraft: true, catchup: true }, seed }, track);

// Humanos em "piloto automático": o mesmo cérebro da IA, com habilidade alta (representa um jogador competente).
for (const c of state.cars) if (c.seat >= 0) c.ai = { skill: 0.97, laneX: 0.2 * (c.seat - 1.5), laneUntil: 0, lookahead: 28, aggression: 0.6 };

const counts: Record<string, number> = {};
const t0 = Date.now();
let ticks = 0;
while (state.phase !== 'finished' && ticks < TICK_RATE * 600) {
  const inputs: PlayerInput[] = [];
  for (const c of state.cars) if (c.seat >= 0) { const car = c; inputs[c.seat] = aiInput(state, track, car); }
  // O piloto automático humano usa o próprio cérebro (car.ai), mas a IA de verdade só roda para seat < 0; aqui forçamos.
  for (const c of state.cars) if (c.seat >= 0) { const brain = c.ai; c.ai = null; const i = inputs[c.seat]; c.ai = brain; inputs[c.seat] = i; }
  stepRace(state, track, inputs);
  for (const e of state.events) counts[e.type] = (counts[e.type] ?? 0) + 1;
  ticks++;
}
const ms = Date.now() - t0;
console.log(`${track.def.name} — ${track.def.laps} voltas, ${state.cars.length} carros, ${humanCount} humanos (${difficulty}), semente ${seed}`);
console.log(`Fase: ${state.phase} em ${formatTicks(ticks)} de corrida simulada (${ms} ms de CPU)`);
for (const r of state.results ?? []) {
  console.log(`${String(r.position).padStart(2)}º ${r.name.padEnd(14)} ${r.carDefId.padEnd(8)} ${r.seat >= 0 ? 'P' + (r.seat + 1) : '  '} total ${formatTicks(r.totalTicks)} melhor ${formatTicks(r.bestLapTicks)} pts ${r.points}`);
}
console.log('Eventos:', Object.entries(counts).map(([k, v]) => `${k}=${v}`).join(' '));
if (state.results) {
  const champ = createChampionship('brasil', humans);
  applyRaceResult(champ, state.results, humans);
  console.log(`Equipe humana: colocação ${teamRaceRank(state.results, 0)} entre equipes → ${champ.lastVerdict}`);
}
