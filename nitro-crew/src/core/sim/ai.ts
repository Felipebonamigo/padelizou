// Piloto de IA: decide o PlayerInput de um carro a cada tick a partir do que vê na pista.
import { AI_BRAKE_CURVE, CATCHUP_DISTANCE, MISTAKE_BRAKE_LATE, MISTAKE_CORNER_SPEED, MISTAKE_WIDE_SEGMENTS, MISTAKE_WIDE_X, RIVAL_SKILL_BONUS, SEGMENT_LENGTH } from '../constants';
import { nextFloat, nextRange } from '../rng';
import { maxCurveAhead, segmentAt } from '../track/builder';
import type { AiBrain, CarState, CarStats, Difficulty, Personality, PlayerInput, RaceState, Track } from '../types';
import { wrappedDelta } from './collisions';
import { distanceToFinish, fuelTight, fuelToSkipPit, markFuel, measuredBurn, PIT_LOOKAHEAD } from './fuel';
import { blockLane, curveZoneStart, missesBraking, tuningOf } from './personality';
import { centrifugalRate, holdableSpeedFraction, PIT_LANE_X, steerRate } from './physics';
import { carStats } from './stats';

export const DIFFICULTY_SPEED: Record<Difficulty, number> = { amador: 0.86, profissional: 0.94, campeao: 1.0 };
export const DIFFICULTY_SKILL: Record<Difficulty, [number, number]> = {
  amador: [0.8, 0.92], profissional: [0.88, 0.98], campeao: [0.94, 1.03],
};

/** Quem é o piloto: personalidade fixa (data/drivers.ts) e se é o rival principal da copa. */
export interface DriverProfile {
  personality: Personality | null;
  rival: boolean;
}

export function createBrain(state: RaceState, difficulty: Difficulty, slot: number, profile?: DriverProfile): AiBrain {
  const [lo, hi] = DIFFICULTY_SKILL[difficulty];
  // Os quatro sorteios na mesma ordem de sempre: o perfil não muda o acaso dos outros pilotos.
  const brain: AiBrain = {
    skill: nextRange(state.rng, lo, hi),
    laneX: nextRange(state.rng, -0.6, 0.6),
    laneUntil: 0,
    lookahead: 20 + Math.floor(nextFloat(state.rng) * 15) + slot % 3,
    aggression: nextFloat(state.rng),
  };
  if (profile?.personality) brain.personality = profile.personality;
  if (profile?.rival) brain.skill += RIVAL_SKILL_BONUS;
  return brain;
}

/** Distância (em unidades, positiva = à frente) até o carro mais próximo na mesma faixa. */
export function nearestAhead(state: RaceState, track: Track, car: CarState, lateral: number, range: number): CarState | null {
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
  // Escolta (modes.ts): o elástico da IA mira o VIP — é ele que a equipe precisa levar ao pódio.
  const vip = state.party && state.party.vipId >= 0 ? state.cars[state.party.vipId] : undefined;
  if (vip) return vip.progress;
  let best = -Infinity;
  for (const c of state.cars) if (c.seat >= 0 && c.progress > best) best = c.progress;
  return best;
}

/**
 * Ponto de frenagem: a maior fração da velocidade máxima (até `cap`) com que dá para chegar a cada
 * curva forte dos próximos `lookahead` segmentos freando a partir de agora. `grip` escala o limite
 * de cada curva (holdableSpeedFraction). Usado pela direção assistida (sim/assist.ts); é a conta da IA sem
 * personalidade — o aiInput repete o laço com o freio tardio e os erros de frenagem de cada piloto.
 */
export function brakingTarget(track: Track, z: number, def: CarStats, grip: number, lookahead: number, cap: number): number {
  let target = cap;
  const brakeDecel = def.brake * 0.8 + def.accel * 0.45;
  for (let i = 0; i < lookahead; i++) {
    const seg = segmentAt(track, z + i * SEGMENT_LENGTH);
    if (Math.abs(seg.curve) < AI_BRAKE_CURVE) continue;
    const limit = holdableSpeedFraction(def, seg.curve) * grip * def.topSpeed;
    const allowed = Math.sqrt(limit * limit + 2 * brakeDecel * i * SEGMENT_LENGTH) / def.topSpeed;
    if (allowed < target) target = allowed;
  }
  return target;
}

/** Contra-esterço que anula o empurrão da curva `curve` na fração de velocidade `sf` (0..1). */
export function counterSteer(def: CarStats, sf: number, curve: number): number {
  return sf > 0.05 ? (centrifugalRate(def) * sf * curve) / steerRate(def) : 0;
}

export function aiInput(state: RaceState, track: Track, car: CarState): PlayerInput {
  const brain = car.ai!;
  const tune = tuningOf(brain);
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
  // Personalidade: o agressivo supõe frear mais forte (freia mais tarde) e entrar mais rápido; o
  // errático (e, menos, o agressivo) às vezes erra o ponto de frenagem de um trecho e vai à grama.
  const brakeDecel = (def.brake * 0.8 + def.accel * 0.45) * tune.brakeLate;
  let zone = -1; let miss = false;
  /** Segmentos já andados dentro de um trecho de curva em que errou a frenagem (-1 = não está num). */
  let missedFor = -1;
  for (let i = 0; i < lookahead; i++) {
    const seg = segmentAt(track, car.z + i * SEGMENT_LENGTH);
    if (Math.abs(seg.curve) < AI_BRAKE_CURVE) { zone = -1; continue; }
    if (tune.mistake > 0 && zone < 0) {
      zone = i === 0 ? curveZoneStart(track, seg.index) : seg.index;
      miss = missesBraking(state, car, tune, zone);
      if (miss && i === 0) missedFor = (seg.index - zone + track.segments.length) % track.segments.length;
    }
    const limit = holdableSpeedFraction(def, seg.curve) * (0.9 + 0.12 * brain.skill) * tune.cornerSpeed * (miss ? MISTAKE_CORNER_SPEED : 1) * def.topSpeed;
    const allowed = Math.sqrt(limit * limit + 2 * brakeDecel * (miss ? MISTAKE_BRAKE_LATE : 1) * i * SEGMENT_LENGTH) / def.topSpeed;
    if (allowed < target) target = allowed;
  }
  const curveAhead = maxCurveAhead(track, car.z, tune.nitroLook);

  // Elástico: quem ficou para trás do melhor humano acelera um pouco (menos com o tanque justo, que
  // não aguenta o pé no fundo até o box — sim/fuel.ts); quem disparou, segura.
  const human = bestHumanProgress(state);
  if (human > -Infinity && !car.finished) {
    const gap = car.progress - human;
    if (gap < -CATCHUP_DISTANCE && !fuelTight(state, track, car, def.fuelPerUnit)) target *= difficulty === 'amador' ? 1.03 : 1.06;
    else if (gap > CATCHUP_DISTANCE * 1.5) target *= difficulty === 'campeao' ? 0.99 : 0.96;
  }
  if (car.finished) target = Math.min(target, 0.6);

  // ── Box: com o box à frente, para se o que resta — no consumo medido volta a volta — não chega
  // à próxima passagem por ele (ou à chegada). Era "abaixo de 22%", que secava o tanque nas voltas
  // longas (sim/fuel.ts).
  markFuel(brain, car, track.length);
  const pitAhead = seg.pit || segmentAt(track, car.z + PIT_LOOKAHEAD).pit;
  const short = pitAhead && car.fuel < fuelToSkipPit(measuredBurn(brain, car, def.fuelPerUnit, track.length), track.length, distanceToFinish(state, track, car));
  const wantsPit = !state.config.timeTrial && (short || (car.inPit && car.fuel < 0.98));
  let laneTarget = brain.laneX;
  if (wantsPit) {
    laneTarget = PIT_LANE_X;
    if (seg.pit && car.x > 1.1) target = Math.min(target, 0.24);
  } else {
    // Dentro da curva é mais curto e sofre menos com o empurrão.
    if (Math.abs(seg.curve) >= 2) laneTarget = Math.max(-0.75, Math.min(0.75, laneTarget - Math.sign(seg.curve) * tune.insideLine));
    // Desvio de quem está na frente (o agressivo chega mais perto, passa raspando e troca mais de faixa).
    const ahead = nearestAhead(state, track, car, 0.5, SEGMENT_LENGTH * tune.avoidRange);
    // O bloqueador só fecha a porta com a frente livre: desviar de quem está na frente vem primeiro.
    const block = ahead ? null : blockLane(state, track, car, brain, tune);
    if (ahead) {
      if (state.tick >= brain.laneUntil) {
        const goLeft = ahead.x > car.x || (ahead.x === car.x && nextFloat(state.rng) < 0.5);
        brain.laneX = Math.max(-0.7, Math.min(0.7, ahead.x + (goLeft ? -tune.passOffset : tune.passOffset)));
        brain.laneUntil = state.tick + tune.laneHold;
      }
      laneTarget = brain.laneX;
      const aheadFrac = ahead.speed / def.topSpeed;
      // Carro andando: iguala a velocidade dele até conseguir passar (o agressivo cola e bate; o limpo
      // fica um pouco atrás). Carro quase parado é obstáculo: mantém um mínimo para conseguir esterçar.
      if (aheadFrac >= 0.3 && ahead.speed < car.speed) target = Math.min(target, aheadFrac * tune.follow);
      else if (aheadFrac < 0.3) target = Math.min(target, 0.35);
    } else if (block !== null) {
      laneTarget = block;
    }
    // Errou a frenagem: no começo do trecho o carro abre para fora da curva e passa pela grama.
    if (missedFor >= 0 && missedFor < MISTAKE_WIDE_SEGMENTS) laneTarget = -Math.sign(seg.curve) * MISTAKE_WIDE_X;
    // De vez em quando troca de faixa por conta própria (nunca no meio de um bloqueio).
    if (block === null && state.tick >= brain.laneUntil && nextFloat(state.rng) < tune.wander) {
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
  const counter = counterSteer(def, sf, seg.curve);
  const toLane = (laneTarget - car.x) * 5;
  input.steer = Math.max(-1, Math.min(1, toLane + counter));

  // ── Nitro: reta livre, velocidade alta, ninguém colado na frente (o limpo só em reta longa; o
  // agressivo mais cedo, mais vezes e com curva leve à frente).
  const nitroAvailable = state.config.assists.sharedNitro && car.seat >= 0 ? (state.teamNitro[car.teamId] ?? 0) : car.nitroLeft;
  if (nitroAvailable > 0 && car.nitroTicks === 0 && !car.finished && !wantsPit && curveAhead < tune.nitroCurveMax && speedFrac > tune.nitroMinSpeed) {
    const blocked = nearestAhead(state, track, car, 0.45, SEGMENT_LENGTH * 4);
    if (!blocked && nextFloat(state.rng) < tune.nitroBase + brain.aggression * tune.nitroAggression) input.nitro = true;
  }
  return input;
}
