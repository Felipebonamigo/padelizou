// A direção assistida de cada humano na config da corrida e os rótulos de quem corre assistido
// (sem DOM, testável).
import type { AssistLevel, HumanEntry } from '../core/types';
import { t } from '../i18n';
import './strings';

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

/** Nível do humano do assento (ausente ou sem humano = 'none'). */
export function seatAssist(humans: readonly HumanEntry[], seat: number): AssistLevel {
  return humans.find((h) => h.seat === seat)?.assist ?? 'none';
}

/**
 * Humanos da sessão com o nível que a corrida de fato usou (o da config, que é o que o `stepRace`
 * lê): quem monta os humanos do save (copa retomada, carreira) não traz o nível, e a config ganha o
 * das opções em `assistedHumans`. Serve para o resultado marcar quem correu assistido.
 */
export function withRaceAssists(humans: readonly HumanEntry[], raceHumans: readonly HumanEntry[]): HumanEntry[] {
  return humans.map((h) => {
    const level = seatAssist(raceHumans, h.seat);
    const { assist: _drop, ...rest } = h;
    return level === 'none' ? rest : { ...rest, assist: level };
  });
}

/** Selo da sala online: "ASSIST · Freio"; null para quem não usa. */
export function assistTagText(level: AssistLevel | undefined): string | null {
  if (!level || level === 'none') return null;
  return `${t('access.hud.assist')} · ${t(`access.short.${level}`)}`;
}
