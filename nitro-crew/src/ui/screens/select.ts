// Seleção de copa e de pista, depois do lobby.
import { formatTicks } from '../../core/sim/race';
import type { CupDef, TrackDef } from '../../core/types';
import { t } from '../../i18n';
import { countryName, createFocusList, h, listNav, screenFrame, stars, trackThumb, type FocusItem, type ScreenApi, type ScreenInstance } from './common';
import { lobbyHumans } from './lobby';

export const TRACK_GRID_COLS = 3;

function trackLine(def: TrackDef, laps: number): string {
  return `${countryName(def.country)} · ${t(`core.time.${def.timeOfDay}`)} · ${t('ui.common.laps', { n: laps })}`;
}

export function cupsScreen(api: ScreenApi): ScreenInstance {
  const { ctx } = api;
  const trackById = (id: string) => ctx.tracks.find((x) => x.id === id);
  const items: FocusItem[] = ctx.cups.map((cup: CupDef) => {
    const unlocked = ctx.isCupUnlocked(cup.id);
    const requiredName = cup.requires ? t(`core.cup.${cup.requires}`) : '';
    const tracks = cup.trackIds.map((id) => trackById(id)).filter((x): x is TrackDef => x !== undefined);
    const el = h('div', { class: `cup-row${unlocked ? '' : ' locked'}` },
      h('div', { class: 'cup-head' },
        h('div', { class: 'cup-name-row' },
          h('span', { class: 'cup-flag', text: cup.flag }),
          h('div', { class: 'cup-title' },
            h('strong', { text: t(`core.cup.${cup.id}`) }),
            h('span', { class: 'muted', text: countryName(cup.country) }),
          ),
        ),
        unlocked ? null : h('span', { class: 'cup-lock', text: `🔒 ${t('ui.cups.locked', { cup: requiredName })}` }),
      ),
      h('div', { class: 'cup-tracks' }, tracks.map((def) => h('div', { class: 'track-mini' },
        trackThumb(ctx, def, 56, unlocked ? '#4fc3f7' : '#6b7280'),
        h('div', { class: 'track-mini-text' },
          h('span', { class: 'track-mini-name', text: def.name }),
          h('span', { class: 'muted', text: `${t('ui.common.laps', { n: def.laps })} · ${stars(def.difficulty)}` }),
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
    const el = h('div', { class: 'track-card' },
      trackThumb(ctx, def, 72),
      h('div', { class: 'track-info' },
        h('strong', { class: 'track-name', text: def.name }),
        h('span', { class: 'track-meta', text: trackLine(def, laps) }),
        h('span', { class: 'track-foot' },
          h('span', { class: 'stars', text: stars(def.difficulty) }),
          h('span', { class: 'track-best mono', text: `${t('ui.tracks.bestLap')}: ${best ? formatTicks(best.ticks) : '—'}` }),
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
