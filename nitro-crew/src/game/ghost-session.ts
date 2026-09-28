// Ligação do fantasma com a sessão (session.ts só chama três ganchos): no início de um contra-relógio
// local, carrega o fantasma da pista; a cada tick grava e, no fechamento da volta, mostra a diferença
// no HUD e guarda a volta se ela virou o novo fantasma; a cada quadro entrega o fantasma à RenderFrame.
// Só local: o online corre em modo 'quick' (lockstep) e não tem contra-relógio.
import { TICK_RATE } from '../core/constants';
import { getLanguage, t } from '../i18n';
import '../ghost/strings';
import type { HudMessage, RaceMode, Settings, GhostFrame } from './contracts';
import type { HumanEntry, RaceState } from '../core/types';
import { createGhostRun, formatGhostDelta, type GhostLapEvent, type GhostRun } from './ghost';
import { ghostFor, loadGhostStore, putGhost, saveGhostStore, type GhostStore } from './ghost-store';

/** Quanto a mensagem de fechamento de volta fica no HUD (s). */
const LAP_MESSAGE_TTL = 2.5;

export interface GhostHooks {
  afterTick(state: RaceState): void;
  /** O fantasma para a RenderFrame; undefined com a opção desligada. */
  frame(state: RaceState): GhostFrame | undefined;
}

export interface GhostSessionDeps {
  settings: Pick<Settings, 'ghost'>;
  hud(seat: number, message: HudMessage): void;
  /** Troca de armazenamento nos testes. */
  load?: () => GhostStore;
  save?: (store: GhostStore) => GhostStore | null;
  now?: () => string;
}

export function decimalSeparator(): string {
  return getLanguage() === 'pt' ? ',' : '.';
}

/** Texto e cor da mensagem de fechamento de volta; null quando não há o que dizer. */
export function lapMessage(e: GhostLapEvent, showGhost: boolean): HudMessage | null {
  const delta = e.deltaTicks === null ? null : formatGhostDelta(e.deltaTicks / TICK_RATE, decimalSeparator());
  if (e.newBest) {
    return { text: delta ? t('ghost.hud.newBestDelta', { delta }) : t('ghost.hud.recorded'), kind: 'good', ttl: LAP_MESSAGE_TTL };
  }
  if (!showGhost || delta === null || e.deltaTicks === null) return null;
  return { text: t('ghost.hud.lapDelta', { delta }), kind: e.deltaTicks < 0 ? 'good' : 'warn', ttl: LAP_MESSAGE_TTL };
}

/** Ganchos do fantasma para a corrida, ou null quando ela não tem fantasma (fora do contra-relógio, online). */
export function startGhost(mode: RaceMode, online: boolean, trackId: string, humans: readonly HumanEntry[], deps: GhostSessionDeps): GhostHooks | null {
  if (mode !== 'timetrial' || online) return null;
  const load = deps.load ?? (() => loadGhostStore());
  const save = deps.save ?? ((s: GhostStore) => saveGhostStore(s));
  const now = deps.now ?? (() => new Date().toISOString());
  const run: GhostRun = createGhostRun(trackId, humans, ghostFor(load(), trackId), now);
  return {
    afterTick(state) {
      for (const e of run.afterTick(state)) {
        if (e.newBest) {
          // Relê antes de gravar: a tela de recordes pode ter importado outro fantasma desde a largada.
          const store = load();
          putGhost(store, e.newBest, now());
          save(store);
        }
        const msg = lapMessage(e, deps.settings.ghost);
        if (msg) deps.hud(e.seat, msg);
      }
    },
    frame(state) {
      if (!deps.settings.ghost) return undefined;
      return run.frame(state) ?? undefined;
    },
  };
}
