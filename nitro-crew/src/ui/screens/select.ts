// Seleção de copa e de pista, depois do lobby.
import { formatTicks } from '../../core/sim/race';
import type { CupDef, TrackDef } from '../../core/types';
import { t } from '../../i18n';
import { countryName, createFocusList, dayIcon, dots, flagFor, h, listNav, screenFrame, trackThumb, type FocusItem, type ScreenApi, type ScreenInstance } from './common';
import { icon } from './icons';
import { lobbyHumans } from './lobby';

export const TRACK_GRID_COLS = 3;

export function cupsScreen(api: ScreenApi): ScreenInstance {
  const { ctx } = api;
  const trackById = (id: string) => ctx.tracks.find((x) => x.id === id);
  const items: FocusItem[] = ctx.cups.map((cup: CupDef) => {
    const unlocked = ctx.isCupUnlocked(cup.id);
    const requiredName = cup.requires ? t(`core.cup.${cup.requires}`) : '';
    const tracks = cup.trackIds.map((id) => trackById(id)).filter((x): x is TrackDef => x !== undefined);
    const el = h('div', { class: `cup-card glass${unlocked ? '' : ' locked'}` },
      h('div', { class: 'cup-head' },
        h('div', { class: 'cup-name-row' },
          h('span', { class: 'cup-flag', text: cup.flag }),
          h('div', { class: 'cup-title' },
            h('strong', { text: t(`core.cup.${cup.id}`) }),
            h('span', { class: 'muted', text: countryName(cup.country) }),
          ),
        ),
        unlocked ? null : h('span', { class: 'cup-lock' }, icon('lock'), h('span', { text: t('ui.cups.locked', { cup: requiredName }) })),
      ),
      h('div', { class: 'cup-tracks' }, tracks.map((def) => h('div', { class: 'track-mini' },
        trackThumb(ctx, def, 56),
        h('div', { class: 'track-mini-text' },
          h('span', { class: 'track-mini-name', text: def.name }),
          h('span', { class: 'meta' }, dayIcon(def.timeOfDay), h('span', { text: t('ui.common.laps', { n: def.laps }) }), dots(def.difficulty)),
        ),
      ))),
    );
    return {
      el,
      activate: () => {
        if (!unlocked) { api.sfx('back'); return; }
        api.emit({ type: 'startCup', cupId: cup.id, humans: lobbyHumans(api) });
      },
    };
  });
  const list = createFocusList(items, { sfx: api.sfx });
  const el = screenFrame('cups', t('ui.cups.title'),
    h('div', { class: 'cup-list' }, items.map((i) => i.el)),
    h('p', { class: 'hint', text: t('ui.cups.hint') }),
  );
  return { el, nav: (nav) => listNav(list, nav, api.sfx, () => api.back()) };
}

export function tracksScreen(api: ScreenApi): ScreenInstance {
  const { ctx, lobby } = api;
  const timeTrial = lobby.mode === 'timetrial';
  const items: FocusItem[] = ctx.tracks.map((def) => {
    const laps = timeTrial ? def.laps : ctx.settings.quickLaps;
    const best = ctx.save.bestLaps[def.id];
    const el = h('div', { class: 'track-card glass' },
      trackThumb(ctx, def, 72),
      h('div', { class: 'track-info' },
        h('strong', { class: 'track-name', text: def.name }),
        h('span', { class: 'meta' },
          h('span', { class: 'flag', text: flagFor(ctx, def.country) }),
          h('span', { text: countryName(def.country) }),
          dayIcon(def.timeOfDay),
          h('span', { text: t('ui.common.laps', { n: laps }) }),
        ),
        h('span', { class: 'track-foot' },
          dots(def.difficulty),
          h('span', { class: 'track-best mono' }, icon('timer'), h('span', { text: best ? formatTicks(best.ticks) : '—' })),
        ),
      ),
    );
    return {
      el,
      activate: () => {
        const humans = lobbyHumans(api);
        if (timeTrial) api.emit({ type: 'startTimeTrial', trackId: def.id, humans });
        else api.emit({ type: 'startQuick', trackId: def.id, laps: ctx.settings.quickLaps, humans });
      },
    };
  });
  const list = createFocusList(items, { cols: TRACK_GRID_COLS, sfx: api.sfx });
  const el = screenFrame('tracks', t('ui.tracks.title'),
    h('div', { class: 'track-grid' }, items.map((i) => i.el)),
    h('p', { class: 'hint', text: t('ui.tracks.hint') }),
  );
  return { el, nav: (nav) => listNav(list, nav, api.sfx, () => api.back()) };
}
