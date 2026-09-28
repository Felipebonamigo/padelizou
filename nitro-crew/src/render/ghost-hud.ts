// Diferença ao vivo para o fantasma do contra-relógio, no painel do tempo (embaixo das voltas):
// "FANTASMA +0,42" em vermelho quando atrás, "−0,15" em verde quando à frente. Só no viewport do
// primeiro humano (o fantasma segue a volta dele); some sem fantasma ou fora da volta cronometrada.
import './ghost-hud.css';
import '../ghost/strings';
import { formatGhostDelta } from '../game/ghost';
import type { GhostFrame } from '../game/contracts';
import { getLanguage, t } from '../i18n';

export class GhostDelta {
  private readonly root: HTMLElement;
  private readonly label: HTMLElement;
  private readonly value: HTMLElement;
  private shown = '';

  constructor(parent: HTMLElement) {
    this.root = document.createElement('div');
    this.root.className = 'ghost-delta';
    this.label = document.createElement('span');
    this.value = document.createElement('b');
    this.root.append(this.label, this.value);
    parent.appendChild(this.root);
  }

  update(ghost: GhostFrame | undefined, lead: boolean): void {
    const delta = lead && ghost ? ghost.delta : null;
    const text = delta === null ? '' : formatGhostDelta(delta, getLanguage() === 'pt' ? ',' : '.');
    // Empate na casa do centésimo (logo depois da linha) fica neutro: "+0,00" em vermelho parecia atraso.
    const state = delta === null ? 'off' : text.startsWith('−') ? 'ahead' : Math.round(Math.abs(delta) * 100) === 0 ? 'even' : 'behind';
    const key = `${state}|${text}|${getLanguage()}`;
    if (key === this.shown) return;
    this.shown = key;
    this.root.className = `ghost-delta ${state}`;
    this.label.textContent = `${t('ghost.hud.label')} `;
    this.value.textContent = text;
  }
}
