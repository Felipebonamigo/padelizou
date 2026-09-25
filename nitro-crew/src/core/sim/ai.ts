// Piloto de IA: decide o PlayerInput de um carro a cada tick a partir do que vê na pista.
import { CATCHUP_DISTANCE, SEGMENT_LENGTH } from '../constants';
import { nextFloat, nextRange } from '../rng';
import { maxCurveAhead, segmentAt } from '../track/builder';
import type { AiBrain, CarState, Difficulty, PlayerInput, RaceState, Track } from '../types';
import { wrappedDelta } from './collisions';
import { centrifugalRate, holdableSpeedFraction, PIT_LANE_X, steerRate } from './physics';
import { carStats } from './stats';

export const DIFFICULTY_SPEED: Record<Difficulty, number> = { amador: 0.86, profissional: 0.94, campeao: 1.0 };
export const DIFFICULTY_SKILL: Record<Difficulty, [number, number]> = {
  amador: [0.8, 0.92], profissional: [0.88, 0.98], campeao: [0.94, 1.03],
};

export function createBrain(state: RaceState, difficulty: Difficulty, slot: number): AiBrain {
  const [lo, hi] = DIFFICULTY_SKILL[difficulty];
  return {
    skill: nextRange(state.rng, lo, hi),
    laneX: nextRange(state.rng, -0.6, 0.6),
    laneUntil: 0,
    lookahead: 20 + Math.floor(nextFloat(state.rng) * 15) + slot % 3,
    aggression: nextFloat(state.rng),
  };
}

/** Distância (em unidades, positiva = à frente) até o carro mais próximo na mesma faixa. */
function nearestAhead(state: RaceState, track: Track, car: CarState, lateral: number, range: number): CarState | null {
  let best: CarState | null = null;
  let bestD = range;
  for (const o of state.cars) {
    if (o === car) continue;
    const d = wrappedDelta(o.z, car.z, track.length);
    if (d <= 0 || d >= bestD) continue;
    if (Math.abs(o.x - car.x) > lateral) continue;
    best = o; bestD = d;
  }
  return best;
}

function bestHumanProgress(state: RaceState): number {
  let best = -Infinity;
  for (const c of state.cars) if (c.seat >= 0 && c.progress > best) best = c.progress;
  return best;
}

export function aiInput(state: RaceState, track: Track, car: CarState): PlayerInput {
  const brain = car.ai!;
  const def = carStats(car);
  const seg = segmentAt(track, car.z);
  const speedFrac = car.speed / def.topSpeed;
  const difficulty = state.config.difficulty;
  const input: PlayerInput = { steer: 0, throttle: true, brake: false, nitro: false, gearUp: false, gearDown: false };

  if (state.phase === 'countdown') return input;

  // ── Velocidade-alvo: na reta, o que a habilidade permite; para cada curva à frente, a
  // velocidade que dá para chegar nela freando a partir de agora (ponto de frenagem).
  const lookahead = brain.lookahead + Math.floor(speedFrac * 30);
  const straightLimit = brain.skill * DIFFICULTY_SPEED[difficulty];
  let target = straightLimit;
  const brakeDecel = def.brake * 0.8 + def.accel * 0.45;
  for (let i = 0; i < lookahead; i++) {
    const seg = segmentAt(track, car.z + i * SEGMENT_LENGTH);
    if (Math.abs(seg.curve) < 1.5) continue;
    const limit = holdableSpeedFraction(def, seg.curve) * (0.9 + 0.12 * brain.skill) * def.topSpeed;
    const allowed = Math.sqrt(limit * limit + 2 * brakeDecel * i * SEGMENT_LENGTH) / def.topSpeed;
    if (allowed < target) target = allowed;
  }
  const curveAhead = maxCurveAhead(track, car.z, 12);

  // Elástico: quem ficou para trás do melhor humano acelera um pouco; quem disparou, segura.
  const human = bestHumanProgress(state);
  if (human > -Infinity && !car.finished) {
    const gap = car.progress - human;
    if (gap < -CATCHUP_DISTANCE) target *= difficulty === 'amador' ? 1.03 : 1.06;
    else if (gap > CATCHUP_DISTANCE * 1.5) target *= difficulty === 'campeao' ? 0.99 : 0.96;
  }
  if (car.finished) target = Math.min(target, 0.6);

  // ── Box: com pouco combustível, entra no box quando o trecho chega.
  const pitAhead = seg.pit || segmentAt(track, car.z + SEGMENT_LENGTH * 30).pit;
  const wantsPit = !state.config.timeTrial && ((car.fuel < 0.22 && pitAhead) || (car.inPit && car.fuel < 0.98));
  let laneTarget = brain.laneX;
  if (wantsPit) {
    laneTarget = PIT_LANE_X;
    if (seg.pit && car.x > 1.1) target = Math.min(target, 0.24);
  } else {
    // Dentro da curva é mais curto e sofre menos com o empurrão.
    if (Math.abs(seg.curve) >= 2) laneTarget = Math.max(-0.75, Math.min(0.75, laneTarget - Math.sign(seg.curve) * 0.35));
    // Desvio de quem está na frente.
    const ahead = nearestAhead(state, track, car, 0.5, SEGMENT_LENGTH * 5);
    if (ahead) {
      if (state.tick >= brain.laneUntil) {
        const goLeft = ahead.x > car.x || (ahead.x === car.x && nextFloat(state.rng) < 0.5);
        brain.laneX = Math.max(-0.7, Math.min(0.7, ahead.x + (goLeft ? -0.55 : 0.55)));
        brain.laneUntil = state.tick + 45;
      }
      laneTarget = brain.laneX;
      const aheadFrac = ahead.speed / def.topSpeed;
      // Carro andando: iguala a velocidade dele até conseguir passar. Carro quase parado é
      // obstáculo: mantém um mínimo para conseguir esterçar e contornar.
      if (aheadFrac >= 0.3 && ahead.speed < car.speed) target = Math.min(target, aheadFrac * 1.02);
      else if (aheadFrac < 0.3) target = Math.min(target, 0.35);
    }
    // De vez em quando troca de faixa por conta própria.
    if (state.tick >= brain.laneUntil && nextFloat(state.rng) < 0.004) {
      brain.laneX = nextRange(state.rng, -0.6, 0.6);
      brain.laneUntil = state.tick + 120;
    }
  }

  // ── Acelerador/freio
  const targetSpeed = target * def.topSpeed;
  if (car.speed > targetSpeed * 1.08) { input.throttle = false; input.brake = true; }
  else if (car.speed > targetSpeed) { input.throttle = false; }

  // ── Volante: vai para a faixa e compensa o empurrão da curva.
  const sf = Math.min(1, speedFrac);
  const counter = sf > 0.05 ? (centrifugalRate(def) * sf * seg.curve) / steerRate(def) : 0;
  const toLane = (laneTarget - car.x) * 5;
  input.steer = Math.max(-1, Math.min(1, toLane + counter));

  // ── Nitro: reta livre, velocidade alta, ninguém colado na frente.
  const nitroAvailable = state.config.assists.sharedNitro && car.seat >= 0 ? (state.teamNitro[car.teamId] ?? 0) : car.nitroLeft;
  if (nitroAvailable > 0 && car.nitroTicks === 0 && !car.finished && !wantsPit && curveAhead < 2 && speedFrac > 0.85) {
    const blocked = nearestAhead(state, track, car, 0.45, SEGMENT_LENGTH * 4);
    if (!blocked && nextFloat(state.rng) < 0.015 + brain.aggression * 0.02) input.nitro = true;
  }
  return input;
}
