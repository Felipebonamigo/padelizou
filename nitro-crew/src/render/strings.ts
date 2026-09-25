// Textos do HUD. Curtos e em caixa alta, como num painel de arcade.
import { registerStrings } from '../i18n';

registerStrings('hud', {
  pt: {
    lap: 'VOLTA', lapOf: 'VOLTA {n}/{total}', lastLap: 'ÚLTIMA VOLTA', position: 'POSIÇÃO', positionOf: '{pos}/{total}',
    ordinal: '{n}º', pit: 'BOX', fuel: 'COMBUSTÍVEL', nitro: 'NITRO', team: 'EQUIPE', go: 'JÁ!', pause: 'PAUSA',
    time: 'TEMPO', last: 'ÚLTIMA', best: 'MELHOR', kmh: 'km/h', gear: 'M', standings: 'CLASSIFICAÇÃO',
    finished: 'CHEGOU', lowFuel: 'COMBUSTÍVEL BAIXO', noNitro: 'SEM NITRO', pitStop: 'NO BOX',
    countdown: 'PREPARAR', timeTrial: 'CONTRA-RELÓGIO', spectating: 'ASSISTINDO', lapShort: 'V{n}', of: '/{total}',
  },
  en: {
    lap: 'LAP', lapOf: 'LAP {n}/{total}', lastLap: 'FINAL LAP', position: 'POSITION', positionOf: '{pos}/{total}',
    ordinal: '{n}{s}', pit: 'PIT', fuel: 'FUEL', nitro: 'NITRO', team: 'TEAM', go: 'GO!', pause: 'PAUSED',
    time: 'TIME', last: 'LAST', best: 'BEST', kmh: 'km/h', gear: 'G', standings: 'STANDINGS',
    finished: 'FINISHED', lowFuel: 'LOW FUEL', noNitro: 'NO NITRO', pitStop: 'IN PIT',
    countdown: 'READY', timeTrial: 'TIME TRIAL', spectating: 'SPECTATING', lapShort: 'L{n}', of: '/{total}',
  },
});

/** Sufixo ordinal em inglês (1st, 2nd, 3rd, 4th…); ignorado em português. */
export function ordinalSuffix(n: number): string {
  const m100 = n % 100;
  if (m100 >= 11 && m100 <= 13) return 'th';
  const m10 = n % 10;
  return m10 === 1 ? 'st' : m10 === 2 ? 'nd' : m10 === 3 ? 'rd' : 'th';
}
