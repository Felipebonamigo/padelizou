// Calibragem da escolta (docs/MODOS.md): posição do VIP sem ajuda (humanos no piloto automático) e com um
// "empurrador" simples que só segue o VIP. Uso: npx tsx scripts/escort-balance.ts [amador|profissional|campeao]
import { createRace, stepRace } from '../src/core/sim/race';
import { getTrack, TRACKS } from '../src/core/track';
import { vipCar } from '../src/core/modes';
import { aiInput } from '../src/core/sim/ai';
import { wrappedDelta } from '../src/core/sim/collisions';
import type { Difficulty, PlayerInput, RaceConfig } from '../src/core/types';

const diff = (process.argv[2] ?? 'profissional') as Difficulty;
const trackIds = TRACKS.filter((_, i) => i % 4 === 0).map((t) => t.id);
for (const helper of [false, true]) {
  const positions: number[] = [];
  for (const id of trackIds) for (const seed of [1, 2]) {
    const track = getTrack(id);
    const config: RaceConfig = { trackId: id, laps: 3, humans: [{ seat: 0, name: 'A', carId: 'falcao', teamId: 0, color: '#fff' }, { seat: 1, name: 'B', carId: 'trovao', teamId: 0, color: '#fff' }], totalCars: 20, difficulty: diff, manualGear: false, assists: { sharedNitro: true, tow: true, teamDraft: true, catchup: true }, seed, mode: 'escort' };
    const state = createRace(config, track);
    for (const c of state.cars) if (c.seat === 1) c.ai = { skill: 0.97, laneX: 0.3, laneUntil: 0, lookahead: 25, aggression: 0.3 };
    const pusher = state.cars.find((c) => c.seat === 0)!;
    if (!helper) pusher.ai = { skill: 0.97, laneX: -0.3, laneUntil: 0, lookahead: 25, aggression: 0.3 };
    let guard = 0;
    while (state.phase !== 'finished' && guard++ < 60 * 60 * 12) {
      const inputs: PlayerInput[] = [];
      if (helper && !pusher.ai) {
        const v = vipCar(state)!;
        const base = aiInput(state, track, { ...pusher, ai: { skill: 1.0, laneX: v.x, laneUntil: 1e9, lookahead: 25, aggression: 0 } } as typeof pusher);
        const d = wrappedDelta(v.z, pusher.z, track.length);
        const steer = Math.max(-1, Math.min(1, (v.x - pusher.x) * 5 + base.steer * 0.3));
        inputs[0] = { ...base, steer, nitro: false, throttle: d > 160 || d < 0 ? base.throttle : false, brake: d > 0 && d < 130 };
      }
      stepRace(state, track, inputs);
    }
    positions.push(vipCar(state)!.position);
  }
  const top3 = positions.filter((p) => p <= 3).length;
  console.log(`${diff} ${helper ? 'com empurrador' : 'sem ajuda   '}: VIP ${positions.join(' ')} → top 3 em ${top3}/${positions.length}`);
}
