// Mecânicas de equipe: vácuo, empurrão e elástico entre companheiros.
import { CATCHUP_DISTANCE, DRAFT_DISTANCE, DRAFT_LATERAL, TOW_COOLDOWN_TICKS, TOW_DISTANCE, TOW_LATERAL, TOW_MIN_SPEED_FACTOR, TOW_SPEED_FACTOR } from '../constants';
import { carDef } from '../data/cars';
import type { CarState, RaceState, Track } from '../types';
import { wrappedDelta } from './collisions';
import type { CarModifiers } from './physics';

export function computeModifiers(state: RaceState, track: Track, car: CarState): CarModifiers {
  const mods: CarModifiers = { draft: false, teamDraft: false, catchup: false };
  if (state.phase === 'countdown') return mods;
  for (const o of state.cars) {
    if (o === car) continue;
    const d = wrappedDelta(o.z, car.z, track.length);
    if (d > 0 && d < DRAFT_DISTANCE && Math.abs(o.x - car.x) < DRAFT_LATERAL && o.speed > car.speed * 0.6) {
      mods.draft = true;
      if (state.config.assists.teamDraft && o.teamId === car.teamId && o.seat >= 0 && car.seat >= 0) mods.teamDraft = true;
    }
  }
  if (state.config.assists.catchup && car.seat >= 0 && !car.finished) {
    let mates = 0; let behindAll = true;
    for (const o of state.cars) {
      if (o === car || o.teamId !== car.teamId || o.seat < 0) continue;
      mates++;
      if (o.progress - car.progress < CATCHUP_DISTANCE) behindAll = false;
    }
    mods.catchup = mates > 0 && behindAll;
  }
  return mods;
}

/** Companheiro que passa perto de um parado dá um empurrão. */
export function applyTow(state: RaceState, track: Track): void {
  if (!state.config.assists.tow || state.phase !== 'racing') return;
  for (const car of state.cars) {
    if (car.towCooldown > 0) { car.towCooldown--; continue; }
    if (car.seat < 0 || car.finished) continue;
    const def = carDef(car.carId);
    if (car.speed > def.topSpeed * TOW_MIN_SPEED_FACTOR) continue;
    for (const mate of state.cars) {
      if (mate === car || mate.seat < 0 || mate.teamId !== car.teamId) continue;
      if (mate.speed < def.topSpeed * 0.4) continue;
      const d = Math.abs(wrappedDelta(mate.z, car.z, track.length));
      if (d < TOW_DISTANCE && Math.abs(mate.x - car.x) < TOW_LATERAL) {
        car.speed = Math.max(car.speed, mate.speed * TOW_SPEED_FACTOR);
        car.towCooldown = TOW_COOLDOWN_TICKS;
        state.events.push({ type: 'tow', carId: car.id, byId: mate.id });
        break;
      }
    }
  }
}
