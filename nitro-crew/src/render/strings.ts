// Textos do HUD. Curtos e em caixa alta, como num painel de arcade.
import { registerStrings } from '../i18n';

registerStrings('hud', {
  pt: {
    lap: 'VOLTA', lapOf: 'VOLTA {n}/{total}', lastLap: 'ÚLTIMA VOLTA', position: 'POSIÇÃO', positionOf: '{pos}/{total}',
    ordinal: '{n}º', pit: 'BOX', fuel: 'COMBUSTÍVEL', nitro: 'NITRO', team: 'EQUIPE', go: 'JÁ!', pause: 'PAUSA',
    time: 'TEMPO', last: 'ÚLTIMA', best: 'MELHOR', kmh: 'km/h', gear: 'M', standings: 'CLASSIFICAÇÃO',
    finished: 'CHEGOU', lowFuel: 'COMBUSTÍVEL BAIXO', noNitro: 'SEM NITRO', pitStop: 'NO BOX',
    countdown: 'PREPARAR', timeTrial: 'CONTRA-RELÓGIO', spectating: 'ASSISTINDO',
  },
  en: {
    lap: 'LAP', lapOf: 'LAP {n}/{total}', lastLap: 'FINAL LAP', position: 'POSITION', positionOf: '{pos}/{total}',
    ordinal: '{n}{s}', pit: 'PIT', fuel: 'FUEL', nitro: 'NITRO', team: 'TEAM', go: 'GO!', pause: 'PAUSED',
    time: 'TIME', last: 'LAST', best: 'BEST', kmh: 'km/h', gear: 'G', standings: 'STANDINGS',
    finished: 'FINISHED', lowFuel: 'LOW FUEL', noNitro: 'NO NITRO', pitStop: 'IN PIT',
    countdown: 'READY', timeTrial: 'TIME TRIAL', spectating: 'SPECTATING',
  },
});
