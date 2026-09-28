// A direção assistida de cada humano na config da corrida (sem DOM, testável).
import type { AssistLevel, HumanEntry } from '../core/types';

/**
 * Humanos com a assistência escolhida: a que já vem no HumanEntry (lobby, online) ou a do assento
 * nas opções (copa retomada, carreira, que montam os humanos do save). 'none' não entra no objeto —
 * a config de quem não usa assistência fica igual à de antes.
 */
export function assistedHumans(humans: readonly HumanEntry[], seatAssists: readonly AssistLevel[]): HumanEntry[] {
  return humans.map((h) => {
    const level = h.assist ?? seatAssists[h.seat] ?? 'none';
    const { assist: _drop, ...rest } = h;
    return level === 'none' ? rest : { ...rest, assist: level };
  });
}
