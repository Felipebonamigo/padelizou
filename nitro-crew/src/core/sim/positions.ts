// Voltas, progresso, posições, chegada e resultado final.
import { FINISH_GRACE_TICKS, POINTS_TABLE } from '../constants';
import type { CarState, RaceResultRow, RaceState, Track } from '../types';

/** Detecta a passagem pela linha (z deu a volta) comparando com o z anterior. */
export function updateLaps(state: RaceState, track: Track, car: CarState, prevZ: number): void {
  if (state.phase === 'countdown') return;
  const crossed = car.z < prevZ && prevZ - car.z > track.length / 2;
  if (!crossed) return;
  if (car.lap === 0) {
    // Primeira passagem pela linha depois do grid: começa a volta 1, sem tempo registrado.
    car.lap = 1;
    car.lapStartTick = state.tick;
    return;
  }
  const lapTicks = state.tick - car.lapStartTick;
  car.lapTicks.push(lapTicks);
  car.lapStartTick = state.tick;
  const best = car.lapTicks.every((t) => lapTicks <= t);
  car.lap++;
  if (!car.finished) {
    state.events.push({ type: 'lap', carId: car.id, lap: car.lap, lapTicks, best });
    if (car.lap > state.config.laps) {
      car.finished = true;
      car.finishTick = state.tick;
      const position = state.cars.filter((c) => c.finished).length;
      car.position = position;
      state.events.push({ type: 'finish', carId: car.id, position });
      if (car.seat >= 0 && state.firstHumanFinishTick < 0) state.firstHumanFinishTick = state.tick;
    }
  }
}

export function updatePositions(state: RaceState, track: Track): void {
  for (const c of state.cars) c.progress = (c.lap - 1) * track.length + c.z;
  const order = state.cars.slice().sort((a, b) => {
    if (a.finished !== b.finished) return a.finished ? -1 : 1;
    if (a.finished && b.finished) return a.finishTick - b.finishTick;
    return b.progress - a.progress;
  });
  order.forEach((c, i) => { c.position = i + 1; });
}

/** A corrida acaba quando todos os humanos terminam ou o tempo de tolerância expira. */
export function checkRaceOver(state: RaceState): boolean {
  if (state.phase !== 'racing') return false;
  const humans = state.cars.filter((c) => c.seat >= 0);
  const allDone = humans.every((c) => c.finished);
  const graceOver = state.firstHumanFinishTick >= 0 && state.tick - state.firstHumanFinishTick >= FINISH_GRACE_TICKS;
  return allDone || graceOver;
}

export function buildResults(state: RaceState): RaceResultRow[] {
  const order = state.cars.slice().sort((a, b) => a.position - b.position);
  return order.map((c) => ({
    carId: c.id, seat: c.seat, name: c.name, teamId: c.teamId, carDefId: c.carId,
    position: c.position, finished: c.finished,
    totalTicks: c.finished ? c.finishTick - state.startTick : -1,
    bestLapTicks: c.lapTicks.length ? Math.min(...c.lapTicks) : -1,
    points: POINTS_TABLE[c.position - 1] ?? 0,
  }));
}
