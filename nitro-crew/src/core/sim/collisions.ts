// Colisões carro-carro e carro-cenário.
import { CAR_HALF_WIDTH, CAR_LENGTH, COLLISION_COOLDOWN_TICKS, OFFROAD_X, SPRITE_CRASH_SPEED_FACTOR } from '../constants';
import { carDef } from '../data/cars';
import { segmentAt } from '../track/builder';
import type { CarState, RaceState, Track } from '../types';
import { SPRITE_HALF_WIDTH } from '../track/sprites';

export { SPRITE_HALF_WIDTH } from '../track/sprites';

/** Distância longitudinal com volta: resultado em (-L/2, L/2]. */
export function wrappedDelta(a: number, b: number, length: number): number {
  let d = a - b;
  d %= length;
  if (d < 0) d += length;
  if (d > length / 2) d -= length;
  return d;
}

export function resolveCarCollisions(state: RaceState, track: Track): void {
  const cars = state.cars;
  const n = cars.length;
  for (let i = 0; i < n; i++) {
    const a = cars[i];
    for (let j = i + 1; j < n; j++) {
      const b = cars[j];
      const dz = wrappedDelta(a.z, b.z, track.length);
      if (Math.abs(dz) >= CAR_LENGTH) continue;
      const dx = a.x - b.x;
      if (Math.abs(dx) >= CAR_HALF_WIDTH * 2) continue;
      // `a` está na frente se dz > 0.
      const front = dz > 0 ? a : b;
      const rear = dz > 0 ? b : a;
      const push = dx === 0 ? (a.id < b.id ? 0.02 : -0.02) : (dx > 0 ? 0.03 : -0.03);
      a.x += push; b.x -= push;
      const sideBySide = Math.abs(dz) < CAR_LENGTH * 0.35;
      if (sideBySide) {
        a.speed *= 0.97; b.speed *= 0.97;
      } else if (rear.speed > front.speed) {
        // Batida por trás: quem bate perde velocidade, quem é batido ganha um pouco.
        const rearTop = carDef(rear.carId).topSpeed;
        const strength = (rear.speed - front.speed) / rearTop;
        rear.speed = Math.max(front.speed * 0.9, rearTop * 0.1);
        front.speed = Math.min(carDef(front.carId).topSpeed, front.speed + strength * 400);
        if (a.collisionCooldown === 0 && b.collisionCooldown === 0) {
          state.events.push({ type: 'collision', carId: rear.id, otherId: front.id, strength: Math.min(1, strength * 4) });
          a.collisionCooldown = COLLISION_COOLDOWN_TICKS; b.collisionCooldown = COLLISION_COOLDOWN_TICKS;
        }
      }
    }
  }
  for (const c of cars) if (c.collisionCooldown > 0) c.collisionCooldown--;
}

/** Bater em árvore, placa ou muro estando fora do asfalto. */
export function resolveSpriteCrash(state: RaceState, track: Track, car: CarState): void {
  const inPitLane = car.x > 1.25 && car.x < 1.95 && segmentAt(track, car.z).pit;
  if (Math.abs(car.x) <= OFFROAD_X || inPitLane) return;
  if (car.collisionCooldown > 0) return;
  const seg = segmentAt(track, car.z);
  for (const s of seg.sprites) {
    if (!s.solid) continue;
    const half = SPRITE_HALF_WIDTH[s.kind] * s.scale + CAR_HALF_WIDTH;
    if (Math.abs(s.x - car.x) < half) {
      const def = carDef(car.carId);
      car.speed = Math.min(car.speed, def.topSpeed * SPRITE_CRASH_SPEED_FACTOR);
      car.x += car.x > 0 ? -0.15 : 0.15;
      car.collisionCooldown = COLLISION_COOLDOWN_TICKS;
      state.events.push({ type: 'crash', carId: car.id, sprite: s.kind });
      return;
    }
  }
}
