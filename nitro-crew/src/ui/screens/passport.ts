// Passaporte da Expedição Brasil (onda G, docs/PISTAS-TURISMO.md "Identidade"; docs/TELAS.md): os 27 carimbos, um
// por estado, em 5 linhas (uma por região), e o cartão-postal do estado em foco — nome, região, as 3 pistas e os
// marcos (src/core/data/places.ts). Vencer a copa do estado carimba (save.stamps); sem carimbo, o cartão diz qual
// copa vencer. Só DOM e CSS: o "cenário" do cartão é um degradê por região (passport.css).
import { BRAZIL_REGIONS } from '../../core/data/cups';
import { placeOf } from '../../core/data/places';
import type { BrazilRegion, CupDef } from '../../core/types';
import type { MenuContext } from '../../game/contracts';
import { t } from '../../i18n';
import '../passport/strings';
import { createFocusList, h, listNav, screenFrame, trackName, type FocusItem, type ScreenApi, type ScreenInstance } from './common';
import { icon } from './icons';
import './passport.css';

export interface PassportStamp {
  state: string;
  cup: CupDef;
}

export interface PassportRow {
  region: BrazilRegion;
  stamps: PassportStamp[];
}

/** Os carimbos por região, na ordem da expedição (BRAZIL_REGIONS); estado sem copa fica de fora. */
export function passportRows(cups: readonly CupDef[]): PassportRow[] {
  return BRAZIL_REGIONS.map((r) => ({
    region: r.id,
    stamps: r.states.flatMap((state) => {
      const cup = cups.find((c) => c.stage === 'brasil' && c.state === state);
      return cup ? [{ state, cup }] : [];
    }),
  }));
}

/** Marcos do estado: os das pistas da copa, na ordem da copa e de cada pista, sem repetir. */
export function stateLandmarks(cup: CupDef): string[] {
  const out: string[] = [];
  for (const id of cup.trackIds) for (const l of placeOf(id)?.landmarks ?? []) if (!out.includes(l)) out.push(l);
  return out;
}

export function landmarkName(id: string): string {
  return t(`passport.landmark.${id}`);
}

/** Carimbo grande do passaporte: a sigla dentro do anel, com o nome do estado embaixo. */
function stampEl(s: PassportStamp, on: boolean): HTMLElement {
  return h('div', { class: `pp-stamp${on ? ' on' : ''}`, attrs: { 'data-state': s.state, 'data-region': s.cup.region ?? '', title: t(`core.state.${s.state}`) } },
    h('span', { class: 'pp-stamp-ring' }, h('span', { class: 'pp-uf', text: s.state })),
  );
}

/** Cartão-postal do estado: carimbado mostra tudo; sem carimbo, esmaecido, com a copa a vencer. */
export function postcard(ctx: Pick<MenuContext, 'save'>, s: PassportStamp): HTMLElement {
  const on = ctx.save.stamps.includes(s.state);
  const region = s.cup.region ? t(`core.region.${s.cup.region}`) : '';
  const landmarks = stateLandmarks(s.cup).map(landmarkName);
  return h('div', { class: `postcard${on ? ' on' : ' off'}`, attrs: { 'data-region': s.cup.region ?? '', 'data-state': s.state } },
    // O cenário e o título sobre ele: o título e o carimbo postal ficam por cima (absolutos), o resto em fluxo.
    h('div', { class: 'pc-scene' }, h('i', { class: 'pc-sun' }), h('i', { class: 'pc-hill a' }), h('i', { class: 'pc-hill b' })),
    h('div', { class: 'pc-title' },
      h('span', { class: 'pc-from', text: t('passport.postcard.from') }),
      h('strong', { class: 'pc-state', text: t(`core.state.${s.state}`) }),
    ),
    // Carimbo postal: sigla e situação desenhadas por ::before/::after (o carimbo gira inteiro, sem filhos soltos).
    h('span', { class: 'pc-postmark', attrs: { 'data-uf': s.state, 'data-label': on ? t('passport.stamped') : t('passport.notStamped'), 'aria-label': `${s.state} · ${on ? t('passport.stamped') : t('passport.notStamped')}` } }),
    h('div', { class: 'pc-body' },
      h('span', { class: 'pc-region', text: t('passport.postcard.region', { region }) }),
      h('div', { class: 'pc-cols' },
        h('div', { class: 'pc-col' },
          h('span', { class: 'pc-label', text: t('passport.postcard.tracks') }),
          h('ol', { class: 'pc-tracks' }, s.cup.trackIds.map((id) => h('li', { text: trackName(id) }))),
        ),
        h('div', { class: 'pc-col' },
          h('span', { class: 'pc-label', text: t('passport.postcard.landmarks') }),
          h('p', { class: 'pc-landmarks', text: landmarks.join(' · ') }),
        ),
      ),
      on ? null : h('p', { class: 'pc-locked' }, icon('lock'), h('span', { text: t('passport.postcard.locked', { cup: t(`core.cup.${s.cup.id}`) }) })),
    ),
  );
}

export function passportScreen(api: ScreenApi): ScreenInstance {
  const { ctx } = api;
  const rows = passportRows(ctx.cups);
  const all = rows.flatMap((r) => r.stamps);
  const stamped = (s: PassportStamp) => ctx.save.stamps.includes(s.state);
  const total = all.filter(stamped).length;
  const items: FocusItem[] = all.map((s) => ({ el: stampEl(s, stamped(s)) }));
  // Começa no último carimbo ganho (o cartão que o jogador acabou de receber); sem nenhum, no primeiro estado.
  const lastState = ctx.save.stamps[ctx.save.stamps.length - 1];
  const startAt = Math.max(0, all.findIndex((s) => s.state === lastState));
  const list = createFocusList(items, { rows: rows.map((r) => r.stamps.length), sfx: api.sfx, start: startAt });
  const cardSlot = h('div', { class: 'pp-card-slot' });
  let shown = -1;
  const sync = () => {
    if (list.index === shown || list.index < 0) return;
    shown = list.index;
    cardSlot.replaceChildren(postcard(ctx, all[shown]));
  };
  sync();
  let k = 0;
  const book = h('div', { class: 'pp-book glass' }, rows.map((r) => {
    const n = r.stamps.filter(stamped).length;
    const els = r.stamps.map(() => items[k++].el);
    return h('section', { class: `pp-region${n === r.stamps.length && n > 0 ? ' done' : ''}`, attrs: { 'data-region': r.region } },
      h('h2', { class: 'pp-region-head' },
        h('span', { text: t(`core.region.${r.region}`) }),
        h('span', { class: 'pp-region-count mono', text: `${n}/${r.stamps.length}` }),
      ),
      h('div', { class: 'pp-stamps' }, els),
    );
  }));
  const el = screenFrame('passport', null,
    h('div', { class: 'screen-title-row' },
      h('h1', { class: 'screen-title', text: t('core.passport.title') }),
      h('span', { class: 'chip', text: t('core.passport.stamps', { n: total, total: all.length }) }),
    ),
    h('div', { class: 'pp-layout' }, book, cardSlot),
    h('p', { class: 'hint', text: t('passport.hint') }),
  );
  return {
    el,
    nav: (nav) => { listNav(list, nav, api.sfx, () => api.back()); sync(); },
    update: sync,
  };
}
