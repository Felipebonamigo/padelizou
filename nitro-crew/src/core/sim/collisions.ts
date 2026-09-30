// Colisões carro-carro e carro-cenário.
import {
  CAR_HALF_WIDTH, CAR_LENGTH, COLLISION_COOLDOWN_TICKS, COLLISION_PASSES, COLLISION_SLOP_X, COLLISION_SLOP_Z, DT, OFFROAD_X,
  SIDE_CONTACT_FULL_OVERLAP, SIDE_CONTACT_LOSS, SPRITE_CRASH_SPEED_FACTOR,
} from '../constants';
import { fmodFast, segmentAt } from '../track/builder';
import type { CarState, RaceState, Track } from '../types';
import { SPRITE_HALF_WIDTH } from '../track/sprites';
import { carStats } from './stats';

export { SPRITE_HALF_WIDTH } from '../track/sprites';

/** Distância longitudinal com volta: resultado em (-L/2, L/2]. */
export function wrappedDelta(a: number, b: number, length: number): number {
  // fmodFast: o mesmo `d % length`, sem o fmod no caso comum (|d| < length) — docs/DESEMPENHO.md.
  let d = fmodFast(a - b, length);
  if (d < 0) d += length;
  if (d > length / 2) d -= length;
  return d;
}

/**
 * Contato carro-carro, par a par, com a caixa de colisão (CAR_LENGTH × 2·CAR_HALF_WIDTH; docs/FISICA.md).
 * O eixo do contato é o que acabou de se fechar: se neste tick os dois ainda estavam separados em z (a distância
 * de antes, pela velocidade de cada um, passava de CAR_LENGTH), é batida em fila; senão, é raspão de lado. Carros
 * postos já sobrepostos (testes, ou o que a correção anterior não desfez) vão pelo eixo de menor sobreposição
 * proporcional. Nos dois casos a separação é só o que se sobrepõe (mais a folga): nada de tranco fixo por tick.
 */
export function resolveCarCollisions(state: RaceState, track: Track): void {
  const cars = state.cars;
  // Engavetamento: quem recua ou é empurrado para o lado pode cair dentro de um carro de um par já conferido
  // nesta passada. Mais uma passada resolve o que sobrou; o limite só evita laço num bolo de carros.
  for (let pass = 0; pass < COLLISION_PASSES; pass++) if (!resolvePass(state, track)) break;
  for (const c of cars) if (c.collisionCooldown > 0) c.collisionCooldown--;
}

/** Uma passada por todos os pares. Devolve se algum carro foi separado de outro. */
function resolvePass(state: RaceState, track: Track): boolean {
  const cars = state.cars;
  const n = cars.length;
  let moved = false;
  for (let i = 0; i < n; i++) {
    const a = cars[i];
    for (let j = i + 1; j < n; j++) {
      const b = cars[j];
      const dz = wrappedDelta(a.z, b.z, track.length);
      const gapZ = Math.abs(dz);
      if (gapZ >= CAR_LENGTH) continue;
      const dx = a.x - b.x;
      const overlapX = CAR_HALF_WIDTH * 2 - Math.abs(dx);
      if (overlapX <= 0) continue;
      // `a` está na frente se dz > 0.
      const front = dz > 0 ? a : b;
      const rear = dz > 0 ? b : a;
      const overlapZ = CAR_LENGTH - gapZ;
      const closedNow = gapZ + (rear.speed - front.speed) * DT >= CAR_LENGTH;
      if (closedNow || overlapZ / CAR_LENGTH <= overlapX / (CAR_HALF_WIDTH * 2)) rearContact(state, front, rear, overlapZ);
      else sideContact(a, b, dx, overlapX);
      moved = true;
    }
  }
  return moved;
}

/**
 * Batida em fila: quem vem atrás recua até encostar (não atravessa o outro nem fica enfiado nele na tela) e, se
 * vinha mais rápido, perde velocidade e dá um pouco a quem foi batido. O recuo nunca passa de z = 0 para trás:
 * cruzar a linha de chegada ao contrário contaria a volta de novo no tick seguinte (positions.ts).
 */
function rearContact(state: RaceState, front: CarState, rear: CarState, overlapZ: number): void {
  rear.z = Math.max(0, rear.z - overlapZ - COLLISION_SLOP_Z);
  if (rear.speed <= front.speed) return;
  // Quem bate cai a 90% da velocidade do outro (mínimo 10% da própria máxima); quem é batido ganha um pouco.
  const rearTop = carStats(rear).topSpeed;
  const strength = (rear.speed - front.speed) / rearTop;
  rear.speed = Math.max(front.speed * 0.9, rearTop * 0.1);
  front.speed = Math.min(carStats(front).topSpeed, front.speed + strength * 400);
  if (front.collisionCooldown === 0 && rear.collisionCooldown === 0) {
    state.events.push({ type: 'collision', carId: rear.id, otherId: front.id, strength: Math.min(1, strength * 4) });
    front.collisionCooldown = COLLISION_COOLDOWN_TICKS; rear.collisionCooldown = COLLISION_COOLDOWN_TICKS;
  }
}

/**
 * Raspão de lado: cada um se afasta metade da sobreposição (só até encostar) e os dois perdem velocidade na
 * proporção do quadrado da sobreposição (constants.ts: SIDE_CONTACT_LOSS).
 */
function sideContact(a: CarState, b: CarState, dx: number, overlapX: number): void {
  // Na mesma linha exata, o de id menor vai para a direita (determinístico).
  const dir = dx > 0 ? 1 : dx < 0 ? -1 : (a.id < b.id ? 1 : -1);
  const push = (overlapX + COLLISION_SLOP_X) / 2;
  a.x += dir * push; b.x -= dir * push;
  const hard = Math.min(1, overlapX / SIDE_CONTACT_FULL_OVERLAP);
  const keep = 1 - SIDE_CONTACT_LOSS * hard * hard;
  a.speed *= keep; b.speed *= keep;
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
      car.speed = Math.min(car.speed, carStats(car).topSpeed * SPRITE_CRASH_SPEED_FACTOR);
      car.x += car.x > 0 ? -0.15 : 0.15;
      car.collisionCooldown = COLLISION_COOLDOWN_TICKS;
      state.events.push({ type: 'crash', carId: car.id, sprite: s.kind });
      return;
    }
  }
}
