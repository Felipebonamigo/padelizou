import { getTrack } from '../src/core/track';
import { createRace, stepRace } from '../src/core/sim/race';
import { carDef } from '../src/core/data/cars';
import { segmentAt } from '../src/core/track/builder';
const track = getTrack(process.argv[2] ?? 'copacabana');
const state = createRace({ trackId: track.def.id, laps: 3, humans: [{ seat: 0, name: 'P1', carId: 'falcao', teamId: 0, color: '#fff' }], totalCars: 20, difficulty: 'profissional', manualGear: false, assists: { sharedNitro: false, tow: false, teamDraft: false, catchup: false }, seed: Number(process.argv[3] ?? 11) }, track);
const log: string[] = [];
for (let i = 0; i < 60 * 120; i++) {
  stepRace(state, track, []);
  for (const e of state.events) if (e.type === 'crash' || e.type === 'pit_enter' || e.type === 'pit_exit' || e.type === 'fuel_empty') log.push(`${state.tick} ${JSON.stringify(e)}`);
}
for (const c of state.cars) {
  const seg = segmentAt(track, c.z);
  console.log(`#${c.id} ${c.name.padEnd(14)} seat=${c.seat} lap=${c.lap} pos=${c.position} z=${c.z.toFixed(0)} seg=${seg.index} curve=${seg.curve.toFixed(2)} x=${c.x.toFixed(2)} v=${(c.speed / carDef(c.carId).topSpeed).toFixed(2)} fuel=${c.fuel.toFixed(2)} pit=${c.inPit} nitro=${c.nitroLeft} skill=${c.ai?.skill.toFixed(2)} lane=${c.ai?.laneX.toFixed(2)}`);
}
console.log(log.filter((l) => l.includes('crash')).length, 'crashes;', log.filter((l) => l.includes('pit_enter')).length, 'pit entries');
console.log(log.slice(-15).join('\n'));
