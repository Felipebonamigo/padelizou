// Mensagem grande do HUD quando um humano cruza a chegada.
import { formatTicks } from '../core/sim/race';
import type { RaceState } from '../core/types';
import { t } from '../i18n';
import './strings';

/** Contra-relógio: o tempo (sozinho na pista, a posição é sempre 1º); corrida: a posição. */
export function finishMessage(state: RaceState, carId: number, position: number): string {
  const car = state.cars[carId];
  if (state.config.timeTrial && car && car.finished) return t('session.finishTime', { time: formatTicks(car.finishTick - state.startTick) });
  return position === 1 ? t('session.finishFirst') : t('session.finish', { pos: position });
}
