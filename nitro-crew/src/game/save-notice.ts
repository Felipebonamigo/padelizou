// Aviso de "progresso não salvo" (storage.ts não conseguiu gravar em lugar nenhum): quando mostrar. Só no menu
// principal ou no resultado da corrida — nunca no meio dela —, uma vez por gravação perdida desde o último aviso, e
// nada se a gravação seguinte já deu certo. O desenho do aviso é o do canto (src/errors/toast.ts); main.ts liga.
import type { MenuScreen } from './contracts';
import type { SaveHealth } from './storage';

const CALM_SCREENS: ReadonlySet<MenuScreen> = new Set<MenuScreen>(['main', 'results']);

/** Mostrar agora? `shownAt` é o `failures` do último aviso mostrado. */
export function saveNoticeDue(health: SaveHealth, shownAt: number, screen: MenuScreen | null): boolean {
  return health.lost.length > 0 && health.failures > shownAt && screen !== null && CALM_SCREENS.has(screen);
}

export interface SaveNotice {
  /** Chamado com a tela aberta (null = em corrida); mostra o aviso quando for a hora. */
  update(screen: MenuScreen | null): void;
}

export function createSaveNotice(show: () => void, health: () => SaveHealth): SaveNotice {
  let shownAt = 0;
  return {
    update(screen) {
      const h = health();
      if (!saveNoticeDue(h, shownAt, screen)) return;
      shownAt = h.failures;
      show();
    },
  };
}
