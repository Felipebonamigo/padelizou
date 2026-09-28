// Tela de Acessibilidade (Opções › Acessibilidade): cores dos jogadores, tamanho do HUD, texto
// grande, reduzir efeitos e a direção assistida de cada assento, com a explicação do item em foco.
// Tela própria porque a de Opções já ocupa 1280×720 inteira: uma terceira coluna lá cortava os rótulos.
import { COLOR_PALETTES, seatColor } from '../core/data/drivers';
import { ASSIST_LEVELS } from '../core/sim/assist';
import { HUD_SCALE_MAX, HUD_SCALE_MIN } from '../game/settings';
import { t } from '../i18n';
import { button, createFocusList, h, listNav, onOff, screenFrame, selector, type FocusItem, type ScreenApi, type ScreenInstance, type Selector } from '../ui/screens/common';
import { commitSettings, percent } from '../ui/screens/options';
import './access.css';
import './strings';

const SEATS = 4;

function cycle<T>(list: readonly T[], current: T, dir: -1 | 1): T {
  const i = list.indexOf(current);
  return list[(i + dir + list.length) % list.length];
}

/** Item da coluna de corrida das Opções que abre esta tela. */
export function accessEntry(api: ScreenApi): FocusItem {
  const el = h('div', { class: 'sel sel-action sel-access', attrs: { 'data-item': 'accessibility' } },
    h('span', { class: 'sel-label', text: t('access.options.title') }),
    h('span', { class: 'sel-box' }, h('span', { class: 'sel-value', text: t('access.options.open') })),
  );
  return { el, activate: () => api.go('access') };
}

export function accessScreen(api: ScreenApi): ScreenInstance {
  const s = api.ctx.settings;
  const sfx = api.sfx;
  const commit = () => commitSettings(api);
  const swatches = h('div', { class: 'access-swatches', attrs: { 'aria-hidden': 'true' } });
  const seatRows: Selector[] = [];
  const paint = () => {
    swatches.replaceChildren(...Array.from({ length: SEATS }, (_, i) => h('span', { class: 'access-swatch', style: `--seat:${seatColor(i, s.colorPalette)}`, text: `P${i + 1}` })));
    seatRows.forEach((row, i) => row.el.style.setProperty('--seat', seatColor(i, s.colorPalette)));
  };

  const vision: Array<{ item: Selector; desc: () => string }> = [
    { item: selector(t('access.options.palette'), () => t(`access.palette.${s.colorPalette}`), (d) => { s.colorPalette = cycle(COLOR_PALETTES, s.colorPalette, d); commit(); paint(); }, { sfx }), desc: () => t(`access.desc.palette.${s.colorPalette}`) },
    { item: selector(t('access.options.hudScale'), () => percent(s.hudScale), (d) => { s.hudScale = Math.round(Math.min(HUD_SCALE_MAX, Math.max(HUD_SCALE_MIN, s.hudScale + d * 0.1)) * 10) / 10; commit(); }, { sfx }), desc: () => t('access.desc.hudScale') },
    { item: selector(t('access.options.largeText'), () => onOff(s.largeText), () => { s.largeText = !s.largeText; commit(); }, { sfx }), desc: () => t('access.desc.largeText') },
    { item: selector(t('access.options.reduceEffects'), () => onOff(s.reduceEffects), () => { s.reduceEffects = !s.reduceEffects; commit(); }, { sfx }), desc: () => t('access.desc.reduceEffects') },
  ];
  // A sessão troca o array a cada mudança de opções: sempre lido de s.seatAssists na hora.
  const levelOf = (seat: number) => s.seatAssists[seat] ?? 'none';
  const driving = Array.from({ length: SEATS }, (_, seat) => {
    const item = selector(t('access.options.seatAssist', { n: seat + 1 }), () => t(`access.level.${levelOf(seat)}`), (d) => {
      s.seatAssists[seat] = cycle(ASSIST_LEVELS, levelOf(seat), d);
      commit();
    }, { sfx, cls: 'sel-seat' });
    seatRows.push(item);
    return { item, desc: () => `${t(`access.desc.level.${levelOf(seat)}`)} ${levelOf(seat) === 'none' ? '' : t('access.desc.override')}`.trim() };
  });
  paint();

  const back = button(t('ui.common.back'), () => api.back());
  const entries = [...vision, ...driving];
  const list = createFocusList([...entries.map((e) => e.item), back], { sfx });
  const desc = h('p', { class: 'access-desc glass', attrs: { 'aria-live': 'polite' } });
  let shown = '';
  const refreshDesc = () => {
    const text = entries[list.index]?.desc() ?? t('access.desc.intro');
    if (text !== shown) { shown = text; desc.textContent = text; }
  };
  refreshDesc();

  const el = screenFrame('access', t('access.options.title'),
    h('div', { class: 'options-columns access-columns' },
      h('div', { class: 'options-col' }, h('h2', { class: 'sub-title', text: t('access.options.vision') }), vision[0].item.el, swatches, vision.slice(1).map((v) => v.item.el)),
      h('div', { class: 'options-col' }, h('h2', { class: 'sub-title', text: t('access.options.driving') }), driving.map((d) => d.item.el)),
    ),
    desc,
    h('div', { class: 'access-actions' }, back.el),
  );
  return {
    el,
    nav: (nav) => { listNav(list, nav, sfx, () => api.back()); refreshDesc(); },
    // Foco pelo mouse também troca a explicação.
    update: () => refreshDesc(),
  };
}
