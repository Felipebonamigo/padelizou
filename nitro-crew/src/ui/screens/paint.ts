// Pintura do carro nos menus: a amostra (cor + detalhe) e o seletor "Pintura ‹ ● Rubi ›" que o lobby, a garagem
// e a sala online usam (docs/CARROS.md, "Pintura"). ←→ (ou clique nas setas) percorrem a paleta; o desenho do
// carro da própria tela é a prévia. A marcação é nossa (nunca dado do usuário).
import type { CarDef } from '../../core/types';
import { paintColors } from '../../game/paints';
import { t } from '../../i18n';
import { arrowButton, h, type ScreenApi, type Selector } from './common';
import './paint.css';

/** Estilo da amostra: a cor e o detalhe da pintura; a Original mostra os do carro. */
export function swatchStyle(id: string, car: Pick<CarDef, 'color' | 'accent'>): string {
  const c = paintColors(id) ?? { color: car.color, accent: car.accent ?? car.color };
  return `--paint:${c.color};--paint-accent:${c.accent}`;
}

/** Amostra redonda: a cor e, na diagonal, o detalhe. */
export function paintSwatch(id: string, car: Pick<CarDef, 'color' | 'accent'>): HTMLElement {
  return h('i', { class: 'paint-swatch', style: swatchStyle(id, car), attrs: { 'aria-hidden': 'true', 'data-paint': id } });
}

export interface PaintSelectorOptions {
  /** Id da pintura à mostra agora. */
  current(): string;
  /** O carro que ela pinta (a Original mostra as cores dele). */
  car(): Pick<CarDef, 'color' | 'accent'>;
  /** ←/→: troca (e grava) a pintura; quem chama redesenha a prévia. */
  onAdjust(dir: -1 | 1): void;
  sfx?: ScreenApi['sfx'];
  /** Confirmar na linha (no lobby é PRONTO, como no carro); sem isto, avança uma. */
  onActivate?: () => void;
  cls?: string;
}

/** Linha "Pintura  ‹ ● Rubi ›" (um seletor como os outros, com a amostra antes do nome). */
export function paintSelector(o: PaintSelectorOptions): Selector {
  const swatch = h('i', { class: 'paint-swatch', attrs: { 'aria-hidden': 'true' } });
  const name = h('span', { class: 'paint-name' });
  const arrow = (dir: -1 | 1) => arrowButton(dir, () => { o.onAdjust(dir); sel.refresh(); o.sfx?.('move'); });
  const el = h('div', { class: `sel sel-paint ${o.cls ?? ''}`.trim() },
    h('span', { class: 'sel-label', text: t('ui.lobby.paint') }),
    h('span', { class: 'sel-box' }, arrow(-1), h('span', { class: 'sel-value paint-value' }, swatch, name), arrow(1)),
  );
  const sel: Selector = {
    el,
    adjust: (dir) => { o.onAdjust(dir); sel.refresh(); },
    activate: o.onActivate ?? (() => { o.onAdjust(1); sel.refresh(); }),
    refresh: () => {
      const id = o.current();
      swatch.setAttribute('style', swatchStyle(id, o.car()));
      name.textContent = t(`ui.paint.${id}`);
      el.setAttribute('data-paint', id);
      // Onde o rótulo some (garagem) ou o nome não cabe (3–4 pilotos), a dica do mouse diz qual é.
      el.setAttribute('title', `${t('ui.lobby.paint')}: ${name.textContent}`);
    },
  };
  sel.refresh();
  return sel;
}
