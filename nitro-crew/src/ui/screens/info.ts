// Tela de consulta dos recordes. (A de controles, com remapeamento, mora em controls.ts.)
import { formatTicks } from '../../core/sim/race';
import { t } from '../../i18n';
import { button, createFocusList, h, listNav, screenFrame, type ScreenApi, type ScreenInstance } from './common';

export function recordsScreen(api: ScreenApi): ScreenInstance {
  const { save, tracks, cars } = api.ctx;
  const carName = (id: string) => cars.find((c) => c.id === id)?.name ?? id;
  const rows: HTMLElement[] = [];
  for (const def of tracks) {
    const lap = save.bestLaps[def.id];
    const races = Object.entries(save.bestRaces)
      .filter(([key]) => key.startsWith(`${def.id}:`))
      .map(([key, rec]) => ({ laps: Number(key.slice(def.id.length + 1)), rec }))
      .sort((a, b) => a.laps - b.laps);
    if (!lap && races.length === 0) continue;
    rows.push(h('div', { class: 'record-row glass' },
      h('div', { class: 'record-track' }, h('strong', { text: def.name }), h('span', { class: 'muted', text: t(`core.country.${def.country}`) })),
      h('div', { class: 'record-entries' },
        lap ? h('div', { class: 'record-entry' },
          h('span', { class: 'record-kind', text: t('ui.records.lap') }),
          h('span', { class: 'record-time mono', text: formatTicks(lap.ticks) }),
          h('span', { class: 'record-who', text: `${lap.name} · ${carName(lap.carId)}` }),
        ) : null,
        races.map(({ laps, rec }) => h('div', { class: 'record-entry' },
          h('span', { class: 'record-kind', text: t('ui.records.race', { n: laps }) }),
          h('span', { class: 'record-time mono', text: formatTicks(rec.ticks) }),
          h('span', { class: 'record-who', text: `${rec.name} · ${carName(rec.carId)}` }),
        )),
      ),
    ));
  }
  const back = button(t('ui.common.back'), () => api.back());
  const list = createFocusList([back], { sfx: api.sfx });
  const el = screenFrame('records', t('ui.records.title'),
    h('p', { class: 'hint', text: t('ui.records.stats', { run: save.racesRun, won: save.racesWon, cups: save.cupsCompleted.length }) }),
    rows.length > 0 ? h('div', { class: 'record-list' }, rows) : h('p', { class: 'empty', text: t('ui.records.empty') }),
    h('div', { class: 'actions' }, back.el),
  );
  return { el, nav: (nav) => listNav(list, nav, api.sfx, () => api.back()) };
}
