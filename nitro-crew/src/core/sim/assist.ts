// Direção assistida (acessibilidade): mistura o comando do jogador com a lógica da IA para quem
// joga pela primeira vez ou é criança no mesmo sofá. O nível vem da config do humano
// (HumanEntry.assist), então roda dentro do stepRace e vale igual em todo cliente do online.
// Sem estado próprio: tudo sai do carro e da pista no tick. Níveis:
//   brake — freio automático antes das curvas fortes (o ponto de frenagem da IA, ai.ts);
//   steer — volante assistido: contra-esterço da curva (o da IA) e empurrão de volta na borda;
//   full  — as duas coisas e o volante inteiro (faixa, desvio, box): o jogador só acelera e usa nitro.
// O jogador sempre por cima: o freio dele vale sempre, o volante dele sobrepõe o da assistência,
// e acelerar (e o nitro) é sempre dele — a assistência só tira velocidade, nunca põe.
import {
  ASSIST_EDGE_GAIN, ASSIST_EDGE_X, ASSIST_GRIP_BRAKE, ASSIST_GRIP_FULL, ASSIST_GRIP_STEER, ASSIST_LANE_LIMIT,
  ASSIST_LOOKAHEAD, ASSIST_STEER_DEADZONE, SEGMENT_LENGTH,
} from '../constants';
import { segmentAt } from '../track/builder';
import type { AssistLevel, CarState, PlayerInput, RaceState, Track } from '../types';
import { brakingTarget, counterSteer, nearestAhead } from './ai';
import { distanceToFinish, fuelToSkipPit, PIT_LOOKAHEAD } from './fuel';
import { PIT_LANE_X } from './physics';
import { carStats } from './stats';

export const ASSIST_LEVELS: readonly AssistLevel[] = ['none', 'brake', 'steer', 'full'];

/** Nível do humano sentado em `seat` na config da corrida ('none' se ausente). */
export function assistLevelOf(state: RaceState, seat: number): AssistLevel {
  for (const h of state.config.humans) if (h.seat === seat) return h.assist ?? 'none';
  return 'none';
}

function clamp(v: number, lo: number, hi: number): number {
  return v < lo ? lo : v > hi ? hi : v;
}

/** Empurrão de volta ao asfalto a partir de ASSIST_EDGE_X; `openRight` libera a borda direita (entrada do box). */
function edgeGuard(x: number, openRight: boolean): number {
  if (x > ASSIST_EDGE_X && !openRight) return -(x - ASSIST_EDGE_X) * ASSIST_EDGE_GAIN;
  if (x < -ASSIST_EDGE_X) return (-ASSIST_EDGE_X - x) * ASSIST_EDGE_GAIN;
  return 0;
}

/**
 * Assistência completa: quer o box — com ele à frente e o tanque sem garantir, em aceleração total,
 * a próxima passagem por ele ou a chegada (a mesma conta da IA, sim/fuel.ts) — ou ainda enchendo.
 */
function wantsPit(state: RaceState, track: Track, car: CarState, fuelPerUnit: number): boolean {
  if (state.config.timeTrial) return false;
  if (car.inPit) return car.fuel < 0.98;
  const pitAhead = segmentAt(track, car.z).pit || segmentAt(track, car.z + PIT_LOOKAHEAD).pit;
  return pitAhead && car.fuel < fuelToSkipPit(fuelPerUnit, track.length, distanceToFinish(state, track, car));
}

/**
 * O comando que chega à física para um humano com assistência `level`. `player` é o que ele
 * apertou. Na contagem, sem assistência ou com o carro sob a IA, devolve `player` como está.
 */
export function assistInput(state: RaceState, track: Track, car: CarState, player: PlayerInput, level: AssistLevel): PlayerInput {
  if (level === 'none' || state.phase !== 'racing' || car.ai) return player;
  const def = carStats(car);
  const seg = segmentAt(track, car.z);
  const sf = Math.min(1, car.speed / def.topSpeed);
  const full = level === 'full';
  const out: PlayerInput = { ...player };

  // ── Freio automático: o ponto de frenagem da IA, com margem maior se quem esterça é o jogador.
  // O volante assistido sozinho só freia onde nem o volante todo seguraria a curva.
  const grip = level === 'brake' ? ASSIST_GRIP_BRAKE : full ? ASSIST_GRIP_FULL : ASSIST_GRIP_STEER;
  const target = brakingTarget(track, car.z, def, grip, ASSIST_LOOKAHEAD + Math.floor(sf * 30), 2) * def.topSpeed;
  if (car.speed > target * 1.08) { out.throttle = false; out.brake = true; }
  else if (car.speed > target) out.throttle = false;
  if (level === 'brake') return out;

  // ── Volante: contra-esterço da curva + borda; na completa, a faixa quando o jogador solta.
  const counter = counterSteer(def, sf, seg.curve);
  const playerSteers = Math.abs(player.steer) > ASSIST_STEER_DEADZONE;
  // Box: no trecho dele, a borda direita fica aberta para quem vai (ou já está) para lá.
  const pit = full && wantsPit(state, track, car, def.fuelPerUnit);
  const openRight = car.inPit || (seg.pit && (pit || (playerSteers && player.steer > 0) || car.x > 1.1));
  let steer: number;
  if (full && !playerSteers) {
    let lane = clamp(car.x, -ASSIST_LANE_LIMIT, ASSIST_LANE_LIMIT);
    if (pit) {
      // Encosta na direita antes do box e entra quando o trecho dele começa, devagar.
      lane = seg.pit ? PIT_LANE_X : ASSIST_EDGE_X + 0.1;
      if (segmentAt(track, car.z + SEGMENT_LENGTH * 8).pit && car.speed > def.topSpeed * 0.4) { out.throttle = false; out.brake = true; }
    } else {
      // Desvio de quem está na frente na mesma faixa: passa pelo lado com mais pista.
      const ahead = nearestAhead(state, track, car, 0.5, SEGMENT_LENGTH * 5);
      if (ahead) {
        const goLeft = ahead.x > car.x || (ahead.x === car.x && ahead.x > 0);
        lane = clamp(ahead.x + (goLeft ? -0.55 : 0.55), -0.75, 0.75);
      }
    }
    steer = (lane - car.x) * 5 + counter;
  } else {
    steer = player.steer + counter;
  }
  out.steer = clamp(steer + edgeGuard(car.x, openRight), -1, 1);
  return out;
}
