// Combustível: quando avisar o jogador e quando a IA para no box. Funções puras; a física (aviso) e a
// IA (box) leem daqui. O tanque (data/cars.ts) foi calibrado para voltas de ~400.000 unidades e as
// regras antigas eram níveis fixos (aviso em 25%, box abaixo de 22%); com as voltas longas das copas
// novas (~2.100 segmentos) as duas chegavam depois do último box que ainda salvava a corrida.
// O box fica logo depois da linha de chegada (op `pit` no começo de toda pista; tests/track.test.ts).
import { FUEL_LOW_LAPS, FUEL_PIT_MARGIN, SEGMENT_LENGTH } from '../constants';
import type { AiBrain, CarState, RaceState, Track } from '../types';

/** A IA procura o box a partir desta distância antes dele. */
export const PIT_LOOKAHEAD = SEGMENT_LENGTH * 30;

/** Nível do aviso: abaixo dele o tanque não garante FUEL_LOW_LAPS voltas em aceleração total. */
export function fuelLowLevel(fuelPerUnit: number, lapLength: number): number {
  return FUEL_LOW_LAPS * fuelPerUnit * lapLength;
}

/** "Entre no box" só vale se ainda há box antes da chegada: na última volta ele já ficou para trás. */
export function pitStillAhead(state: RaceState, car: CarState): boolean {
  return car.lap < state.config.laps;
}

/** Quanto falta correr até a chegada (negativo depois dela). */
export function distanceToFinish(state: RaceState, track: Track, car: CarState): number {
  return state.config.laps * track.length - car.progress;
}

/**
 * Tanque justo: não garante, em aceleração total, chegar à próxima linha (o box vem logo depois) ou
 * à chegada. A IA que passou reto pelo box contando com o ritmo medido não aceita o empurrão do
 * elástico assim: atrás do humano ela aceleraria o tempo todo e gastaria ~50% a mais que o medido.
 */
export function fuelTight(state: RaceState, track: Track, car: CarState, fuelPerUnit: number): boolean {
  return car.fuel < fuelPerUnit * Math.min(track.length - car.z, distanceToFinish(state, track, car));
}

/**
 * Mede o consumo da IA volta a volta. A marca é o começo da volta medida: a largada (primeira
 * chamada) ou a saída do box — no box, ou se o combustível passou da marca, ela acompanha o carro.
 * A cada volta inteira desde a marca, guarda o gasto por unidade em `lapBurn` e recomeça dali.
 */
export function markFuel(brain: AiBrain, car: CarState, lapLength: number): void {
  if (brain.fuelMark === undefined || brain.progressMark === undefined || car.inPit || car.fuel > brain.fuelMark) {
    brain.fuelMark = car.fuel;
    brain.progressMark = car.progress;
    return;
  }
  const distance = car.progress - brain.progressMark;
  if (distance < lapLength) return;
  brain.lapBurn = (brain.fuelMark - car.fuel) / distance;
  brain.fuelMark = car.fuel;
  brain.progressMark = car.progress;
}

/**
 * Consumo por unidade de pista para prever a próxima volta: o maior entre a última volta inteira
 * medida e a volta em curso (depois de meia volta) — o gasto sobe no meio da corrida quando o
 * elástico empurra a IA, e a média desde a largada ficava para trás. Sem nada medido (largada, ou a
 * IA assumiu o carro agora), o pior caso: aceleração total o tempo todo.
 */
export function measuredBurn(brain: AiBrain, car: CarState, fuelPerUnit: number, lapLength: number): number {
  let burn = brain.lapBurn;
  if (brain.fuelMark !== undefined && brain.progressMark !== undefined) {
    const distance = car.progress - brain.progressMark;
    if (distance >= lapLength / 2) {
      const current = Math.max(0, brain.fuelMark - car.fuel) / distance;
      burn = burn === undefined ? current : Math.max(burn, current);
    }
  }
  return burn ?? fuelPerUnit;
}

/**
 * Combustível para passar reto pelo box agora: até a próxima passagem por ele (uma volta mais a
 * aproximação) ou até a chegada, o que vier antes, com a folga FUEL_PIT_MARGIN.
 */
export function fuelToSkipPit(burnPerUnit: number, lapLength: number, toFinish: number): number {
  return burnPerUnit * Math.min(lapLength + PIT_LOOKAHEAD, toFinish) * FUEL_PIT_MARGIN;
}
