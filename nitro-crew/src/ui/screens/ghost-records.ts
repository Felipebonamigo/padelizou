// Fantasmas na aba de pistas da tela de recordes (info.ts): a linha da pista mostra o fantasma
// guardado (tempo, quem e carro), Enter nela exporta o arquivo, e "Importar fantasma" traz o de um
// amigo (substitui o da pista). A linha de status diz o que aconteceu — e, quando recusa o arquivo, por quê
// (outra versão do jogo, volta que não cabe na pista, pista que o jogo não tem).
import { formatTicks } from '../../core/sim/race';
import type { GhostRecord } from '../../game/ghost';
import {
  exportGhostFile, ghostFor, importGhostFile, loadGhostStore, putGhost, saveGhostStore, type GhostImport, type GhostStore,
} from '../../game/ghost-store';
import { t } from '../../i18n';
import '../../ghost/strings';
import { button, h, trackName, type FocusItem, type ScreenApi } from './common';
import './ghost-records.css';

/** Aviso de cada arquivo recusado (nada muda na loja). */
const REFUSED: Readonly<Record<Exclude<GhostImport, GhostRecord | 'cancel'>, string>> = {
  invalid: 'ghost.records.invalid',
  unknownTrack: 'ghost.records.unknownTrack',
  otherTrack: 'ghost.records.otherTrack',
  otherVersion: 'ghost.records.otherVersion',
};

/** Status da última ação; sobrevive à reconstrução da aba depois de importar. */
let lastStatus: { text: string; ok: boolean } | null = null;

export interface GhostRecordsView {
  store: GhostStore;
  /** Linha de status (em cima da lista). */
  status: HTMLElement;
  /** Botão "Importar fantasma" (vai para a barra de ações, antes do Voltar). */
  importItem: FocusItem;
  ghost(trackId: string): GhostRecord | null;
  /** Entrada do fantasma na linha da pista. */
  entry(rec: GhostRecord, carName: string): HTMLElement;
  /** Exporta o fantasma da pista (ação da linha). */
  exportTrack(rec: GhostRecord, trackName: string): void;
}

/** `rebuild` refaz a aba (depois de importar, a lista muda). */
export function ghostRecordsView(api: ScreenApi, rebuild: () => void): GhostRecordsView {
  const store = loadGhostStore();
  const status = h('p', { class: 'hint ghost-status', attrs: { 'aria-live': 'polite' } });
  const show = (text: string, ok: boolean) => {
    lastStatus = { text, ok };
    status.textContent = text;
    status.classList.toggle('ok', ok);
    status.classList.toggle('fail', !ok);
  };
  if (lastStatus) { show(lastStatus.text, lastStatus.ok); lastStatus = null; } else status.textContent = t('ghost.records.hint');
  const trackLabel = (id: string) => (api.ctx.tracks.some((d) => d.id === id) ? trackName(id) : null);

  const importItem = button(t('ghost.records.import'), () => {
    void importGhostFile().then((got) => {
      if (got === 'cancel') return;
      if (typeof got === 'string') { show(t(REFUSED[got]), false); return; }
      const name = trackLabel(got.trackId);
      if (!name) { show(t('ghost.records.unknownTrack'), false); return; }
      const fresh = loadGhostStore();
      putGhost(fresh, got, new Date().toISOString());
      if (!saveGhostStore(fresh)) { show(t('ghost.records.saveFailed'), false); return; }
      lastStatus = { text: t('ghost.records.imported', { name: got.name, track: name }), ok: true };
      rebuild();
    });
  }, 'ghost-import');

  return {
    store, status, importItem,
    ghost: (trackId) => ghostFor(store, trackId),
    entry: (rec, carName) => h('div', { class: 'record-entry ghost-entry' },
      h('span', { class: 'record-kind', text: t('ghost.records.ghost') }),
      h('span', { class: 'record-time mono', text: formatTicks(rec.ticks) }),
      h('span', { class: 'record-who', text: `${rec.name} · ${carName}` }),
    ),
    exportTrack: (rec, name) => {
      void exportGhostFile(rec).then((ok) => { if (ok) show(t('ghost.records.exported', { track: name }), true); else show(t('ghost.records.exportFailed'), false); });
    },
  };
}
