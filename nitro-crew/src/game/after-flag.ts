// Depois da bandeirada o `stepRace` só conta o tick, então os carros congelariam na tela até o resultado. Esta cópia do
// estado segue rodando só para exibição (quem terminou já cruza em piloto automático): o estado real, o resultado e o
// hash não mudam, e nada aqui passa por `stepObserved` — ninguém observa, grava ou pontua a cópia.
import { DT } from '../core/constants';
import { deserializeRace, serializeRace } from '../core/serialize';
import { stepRace } from '../core/sim/race';
import type { RaceState, Track } from '../core/types';

const MAX_STEPS_PER_FRAME = 4;

export interface AfterFlag { display: RaceState; acc: number }

/** Avança a cópia de exibição `dt` segundos (cria a cópia na primeira chamada) e devolve o estado a mostrar. */
export function advanceAfterFlag(prev: AfterFlag | undefined, finished: RaceState, track: Track, dt: number): AfterFlag {
  const a = prev ?? { display: deserializeRace(serializeRace(finished)), acc: 0 };
  a.acc += dt;
  let steps = 0;
  while (a.acc >= DT && steps < MAX_STEPS_PER_FRAME) {
    a.display.phase = 'racing'; // o fim da corrida já foi decidido no estado real
    stepRace(a.display, track, []);
    a.acc -= DT;
    steps++;
  }
  if (steps === MAX_STEPS_PER_FRAME) a.acc = 0;
  return a;
}
