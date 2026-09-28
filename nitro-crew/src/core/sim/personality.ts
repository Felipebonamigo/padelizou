// Personalidades da IA (data/drivers.ts: quem é o quê e os números de cada uma). Funções puras que o
// piloto da IA (sim/ai.ts) consulta: a tabela do cérebro, o erro de frenagem do errático e o bloqueio
// do bloqueador. Sem sorteio no estado: o erro vem de um hash da semente, do carro, da volta e do trecho,
// então não mexe na sequência do state.rng dos outros pilotos.
import {
  AI_BRAKE_CURVE, BLOCK_ALONGSIDE, BLOCK_LATERAL_RATE, BLOCK_LATERAL_WINDOW, BLOCK_MAX_TICKS, BLOCK_MAX_X, BLOCK_RANGE,
  BLOCK_REST_TICKS,
} from '../constants';
import { NEUTRAL_TUNING, PERSONALITY_TUNING, type PersonalityTuning } from '../data/drivers';
import type { AiBrain, CarState, RaceState, Track } from '../types';
import { wrappedDelta } from './collisions';

/** Como este cérebro pilota; sem personalidade, o piloto neutro. */
export function tuningOf(brain: AiBrain): Readonly<PersonalityTuning> {
  const p = brain.personality;
  return p !== undefined && Object.prototype.hasOwnProperty.call(PERSONALITY_TUNING, p) ? PERSONALITY_TUNING[p] : NEUTRAL_TUNING;
}

/** Hash inteiro de quatro números → [0, 1). Igual em toda máquina (só inteiros de 32 bits). */
export function unitHash(a: number, b: number, c: number, d: number): number {
  let h = mix(mix(mix(mix(0x811c9dc5, a), b), c), d);
  h ^= h >>> 13;
  return (h >>> 0) / 4294967296;
}

function mix(h: number, v: number): number {
  h = Math.imul(h ^ (v | 0), 0x01000193) >>> 0;
  h ^= h >>> 15;
  return Math.imul(h, 0x2c1b3c6d) >>> 0;
}

/** Primeiro segmento do trecho de curva que contém o segmento `index` (volta para trás até a reta). */
export function curveZoneStart(track: Track, index: number): number {
  const n = track.segments.length;
  let i = index;
  for (let k = 0; k < n; k++) {
    const prev = (i - 1 + n) % n;
    if (Math.abs(track.segments[prev].curve) < AI_BRAKE_CURVE) return i;
    i = prev;
  }
  return index;
}

/**
 * Este piloto erra o ponto de frenagem no trecho de curva que começa em `zoneStart`, nesta volta? A
 * decisão vale para o trecho inteiro: ele entra rápido demais e o empurrão da curva o leva para fora.
 */
export function missesBraking(state: RaceState, car: CarState, tune: Readonly<PersonalityTuning>, zoneStart: number): boolean {
  if (tune.mistake <= 0) return false;
  return unitHash(state.config.seed, car.id, car.lap, zoneStart) < tune.mistake;
}

/** O humano mais próximo logo atrás (até BLOCK_RANGE), na janela lateral do bloqueio. */
export function chaserBehind(state: RaceState, track: Track, car: CarState): CarState | null {
  let best: CarState | null = null;
  let bestD = BLOCK_RANGE;
  for (const o of state.cars) {
    if (o === car || o.seat < 0 || o.finished) continue;
    const d = wrappedDelta(car.z, o.z, track.length);
    if (d <= 0 || d >= bestD) continue;
    if (Math.abs(o.x - car.x) > BLOCK_LATERAL_WINDOW) continue;
    best = o; bestD = d;
  }
  return best;
}

/**
 * Bloqueador: com um humano logo atrás, leva a própria faixa para a frente dele — devagar
 * (BLOCK_LATERAL_RATE), sem sair do asfalto (BLOCK_MAX_X) e nunca com ele já de bico do lado
 * (BLOCK_ALONGSIDE: respeita o desvio). Depois de BLOCK_MAX_TICKS fechando a porta, desiste e descansa
 * BLOCK_REST_TICKS. Devolve a faixa-alvo deste tick, ou null quando não está bloqueando.
 */
export function blockLane(state: RaceState, track: Track, car: CarState, brain: AiBrain, tune: Readonly<PersonalityTuning>): number | null {
  if (!tune.blocks || car.finished || state.phase !== 'racing') return null;
  if (state.tick < (brain.blockRestUntil ?? 0)) return null;
  const chaser = chaserBehind(state, track, car);
  if (!chaser || wrappedDelta(car.z, chaser.z, track.length) < BLOCK_ALONGSIDE) {
    // Sem ninguém para fechar, o relógio da investida volta devagar: sair e voltar para trás dele não zera.
    if (brain.blockTicks) brain.blockTicks = Math.max(0, brain.blockTicks - 1);
    return null;
  }
  const ticks = (brain.blockTicks ?? 0) + 1;
  if (ticks >= BLOCK_MAX_TICKS) {
    brain.blockTicks = 0;
    brain.blockRestUntil = state.tick + BLOCK_REST_TICKS;
    return null;
  }
  brain.blockTicks = ticks;
  const want = Math.max(-BLOCK_MAX_X, Math.min(BLOCK_MAX_X, chaser.x));
  brain.laneX += Math.max(-BLOCK_LATERAL_RATE, Math.min(BLOCK_LATERAL_RATE, want - brain.laneX));
  return brain.laneX;
}
