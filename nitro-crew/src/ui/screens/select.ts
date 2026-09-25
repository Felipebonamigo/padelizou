// Seleção de copa e de pista, depois do lobby. Oito copas de quatro pistas: a tela de copas é uma
// lista com o detalhe da copa em foco ao lado; a de pistas é uma grade de quatro colunas com uma
// seção por copa, que rola acompanhando o foco (↑↓ trocam de copa, ←→ de pista).
import { formatTicks } from '../../core/sim/race';
import type { CupDef, TrackDef } from '../../core/types';
import type { MenuContext } from '../../game/contracts';
import { t } from '../../i18n';
import { countryName, createFocusList, dayIcon, dots, h, listNav, screenFrame, trackThumb, type FocusItem, type ScreenApi, type ScreenInstance } from './common';
import { icon } from './icons';
import { lobbyHumans } from './lobby';
import './select.css';

/** Colunas da grade de pistas: uma copa por linha. */
export const TRACK_GRID_COLS = 4;

/**
 * Miniaturas desenhadas neste tamanho (px) e reduzidas pelo CSS para `calc(N * var(--u))`: ficam
 * nítidas de 720p a 1440p sem redesenhar quando a janela muda de tamanho.
 */
const THUMB_DRAW_PX = 112;

function scalableThumb(ctx: MenuContext, def: TrackDef): HTMLCanvasElement {
  const c = trackThumb(ctx, def, THUMB_DRAW_PX);
  c.style.removeProperty('width');
  c.style.removeProperty('height');
  return c;
}

/** Pistas da copa, na ordem da copa (ids desconhecidos ficam de fora). */
export function cupTracks(ctx: MenuContext, cup: CupDef): TrackDef[] {
  return cup.trackIds.map((id) => ctx.tracks.find((x) => x.id === id)).filter((x): x is TrackDef => x !== undefined);
}

export type CupStatus = 'done' | 'open' | 'locked';

export function cupStatus(ctx: MenuContext, cup: CupDef): CupStatus {
  if (!ctx.isCupUnlocked(cup.id)) return 'locked';
  return ctx.save.cupsCompleted.includes(cup.id) ? 'done' : 'open';
}

/**
 * Onde o cursor começa na tela de copas: a primeira copa aberta e ainda não concluída (a
 * "fronteira" do jogador); se todas as abertas já foram concluídas, a última aberta.
 */
export function frontierCupIndex(statuses: readonly CupStatus[]): number {
  const open = statuses.indexOf('open');
  if (open >= 0) return open;
  const lastDone = statuses.lastIndexOf('done');
  return lastDone >= 0 ? lastDone : 0;
}

/** Dificuldade média da copa, arredondada para os cinco pontos. */
export function cupDifficulty(tracks: readonly TrackDef[]): number {
  if (tracks.length === 0) return 0;
  return Math.round(tracks.reduce((a, d) => a + d.difficulty, 0) / tracks.length);
}

function statusBadge(status: CupStatus): HTMLElement | null {
  if (status === 'done') return h('span', { class: 'cup-status done', title: t('ui.cups.done') }, icon('check'));
  if (status === 'locked') return h('span', { class: 'cup-status locked' }, icon('lock'));
  return null;
}

function bestLapText(ctx: MenuContext, id: string): string {
  const best = ctx.save.bestLaps[id];
  return best ? formatTicks(best.ticks) : '—';
}

function cupDetail(ctx: MenuContext, cup: CupDef, status: CupStatus): HTMLElement {
  const tracks = cupTracks(ctx, cup);
  let chip: HTMLElement;
  if (status === 'locked') {
    const required = cup.requires ? t(`core.cup.${cup.requires}`) : '';
    chip = h('span', { class: 'cup-chip locked' }, icon('lock'), h('span', { text: t('ui.cups.locked', { cup: required }) }));
  } else if (status === 'done') {
    chip = h('span', { class: 'cup-chip done' }, icon('trophy'), h('span', { text: t('ui.cups.done') }));
  } else {
    chip = h('span', { class: 'cup-chip open' }, icon('flag'), h('span', { text: t('ui.cups.open') }));
  }
  return h('div', { class: `cup-detail glass${status === 'locked' ? ' locked' : ''}` },
    h('div', { class: 'cup-detail-head' },
      h('span', { class: 'cup-detail-flag', text: cup.flag }),
      h('div', { class: 'cup-detail-title' },
        h('strong', { text: t(`core.cup.${cup.id}`) }),
        h('span', { class: 'meta' },
          h('span', { text: countryName(cup.country) }),
          h('span', { class: 'sep', text: '·' }),
          h('span', { text: t('ui.cups.races', { n: tracks.length }) }),
          h('span', { class: 'sep', text: '·' }),
          dots(cupDifficulty(tracks)),
        ),
        chip,
      ),
    ),
    h('ol', { class: 'cup-detail-tracks' }, tracks.map((def, i) => h('li', { class: 'cup-race' },
      h('span', { class: 'cup-race-n mono', text: String(i + 1) }),
      scalableThumb(ctx, def),
      h('div', { class: 'cup-race-text' },
        h('span', { class: 'cup-race-name', text: def.name }),
        h('span', { class: 'meta' },
          dayIcon(def.timeOfDay), h('span', { text: t(`core.time.${def.timeOfDay}`) }),
          h('span', { class: 'sep', text: '·' }),
          h('span', { text: t('ui.common.laps', { n: def.laps }) }),
        ),
      ),
      h('span', { class: 'cup-race-side' },
        dots(def.difficulty),
        h('span', { class: 'track-best mono' }, icon('timer'), h('span', { text: bestLapText(ctx, def.id) })),
      ),
    ))),
  );
}

export function cupsScreen(api: ScreenApi): ScreenInstance {
  const { ctx } = api;
  const statuses = ctx.cups.map((cup) => cupStatus(ctx, cup));
  const items: FocusItem[] = ctx.cups.map((cup: CupDef, i) => {
    const status = statuses[i];
    const el = h('div', { class: `cup-row${status === 'locked' ? ' locked' : ''}`, attrs: { 'data-cup': cup.id } },
      h('span', { class: 'cup-flag', text: cup.flag }),
      h('span', { class: 'cup-row-name', text: t(`core.cup.${cup.id}`) }),
      statusBadge(status),
    );
    return {
      el,
      activate: () => {
        if (status === 'locked') { api.sfx('back'); return; }
        api.emit({ type: 'startCup', cupId: cup.id, humans: lobbyHumans(api) });
      },
    };
  });
  const list = createFocusList(items, { sfx: api.sfx, start: frontierCupIndex(statuses) });
  const detailSlot = h('div', { class: 'cup-detail-slot' });
  let shown = -1;
  // O detalhe acompanha o cursor, venha ele do teclado, do controle ou do mouse.
  const sync = () => {
    if (list.index === shown || list.index < 0) return;
    shown = list.index;
    detailSlot.replaceChildren(cupDetail(ctx, ctx.cups[shown], statuses[shown]));
  };
  sync();
  const el = screenFrame('cups', t('ui.cups.title'),
    h('div', { class: 'cups-layout' },
      h('div', { class: 'cup-rows glass' }, items.map((i) => i.el)),
      detailSlot,
    ),
    h('p', { class: 'hint', text: t('ui.cups.hint') }),
  );
  return {
    el,
    nav: (nav) => { listNav(list, nav, api.sfx, () => api.back()); sync(); },
    update: sync,
  };
}

export function tracksScreen(api: ScreenApi): ScreenInstance {
  const { ctx, lobby } = api;
  const timeTrial = lobby.mode === 'timetrial';
  const items: FocusItem[] = [];
  const rows: HTMLElement[] = [];
  for (const cup of ctx.cups) {
    const cards = cupTracks(ctx, cup).map((def) => {
      const laps = timeTrial ? def.laps : ctx.settings.quickLaps;
      const el = h('div', { class: 'track-card glass', attrs: { 'data-track': def.id } },
        scalableThumb(ctx, def),
        h('div', { class: 'track-info' },
          h('strong', { class: 'track-name', text: def.name }),
          h('span', { class: 'meta' }, dayIcon(def.timeOfDay), h('span', { text: t('ui.common.laps', { n: laps }) })),
          h('span', { class: 'track-foot' },
            dots(def.difficulty),
            h('span', { class: 'track-best mono' }, icon('timer'), h('span', { text: bestLapText(ctx, def.id) })),
          ),
        ),
      );
      const item: FocusItem = {
        el,
        activate: () => {
          const humans = lobbyHumans(api);
          if (timeTrial) api.emit({ type: 'startTimeTrial', trackId: def.id, humans });
          else api.emit({ type: 'startQuick', trackId: def.id, laps: ctx.settings.quickLaps, humans });
        },
      };
      items.push(item);
      return el;
    });
    rows.push(h('section', { class: 'track-section' },
      h('h2', { class: 'track-section-head' },
        h('span', { class: 'cup-flag', text: cup.flag }),
        h('span', { text: countryName(cup.country) }),
        h('span', { class: 'track-section-cup', text: t(`core.cup.${cup.id}`) }),
      ),
      h('div', { class: 'track-grid' }, cards),
    ));
  }
  const list = createFocusList(items, { cols: TRACK_GRID_COLS, sfx: api.sfx });
  const el = screenFrame('tracks', t('ui.tracks.title'),
    h('div', { class: 'track-scroll' }, rows),
    h('p', { class: 'hint', text: t('ui.tracks.hint') }),
  );
  return { el, nav: (nav) => listNav(list, nav, api.sfx, () => api.back()) };
}
