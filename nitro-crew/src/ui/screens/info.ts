// Telas de consulta: controles (mapeamentos + dispositivos detectados) e recordes.
import { formatTicks } from '../../core/sim/race';
import { t } from '../../i18n';
import { button, createFocusList, h, listNav, screenFrame, type ScreenApi, type ScreenInstance } from './common';

interface ControlRow { action: string; kb1: string; kb2: string; gp: string }

function controlRows(): ControlRow[] {
  const sp = t('ui.controls.key.space');
  return [
    { action: t('ui.controls.accel'), kb1: '↑', kb2: 'W', gp: 'A / RT' },
    { action: t('ui.controls.brake'), kb1: '↓', kb2: 'S', gp: 'X / B / LT' },
    { action: t('ui.controls.steer'), kb1: '← →', kb2: 'A D', gp: t('ui.controls.key.stick') },
    { action: t('ui.controls.nitro'), kb1: sp, kb2: 'F', gp: 'RB' },
    { action: t('ui.controls.gearUp'), kb1: 'M', kb2: 'E', gp: 'Y' },
    { action: t('ui.controls.gearDown'), kb1: 'N', kb2: 'Q', gp: 'LB' },
    { action: t('ui.controls.confirm'), kb1: `Enter / ${sp}`, kb2: 'F', gp: 'A' },
    { action: t('ui.controls.back'), kb1: 'Esc / Backspace', kb2: 'Esc', gp: 'B' },
    { action: t('ui.controls.pause'), kb1: 'Esc', kb2: 'Esc', gp: 'Start' },
  ];
}

export function controlsScreen(api: ScreenApi): ScreenInstance {
  const table = h('table', { class: 'table controls-table' },
    h('thead', {}, h('tr', {},
      h('th', { text: t('ui.controls.action') }),
      h('th', { text: t('ui.device.kb1') }),
      h('th', { text: t('ui.device.kb2') }),
      h('th', { text: t('ui.controls.gamepad') }),
    )),
    h('tbody', {}, controlRows().map((r) => h('tr', {},
      h('td', { text: r.action }),
      h('td', { class: 'key', text: r.kb1 }),
      h('td', { class: 'key', text: r.kb2 }),
      h('td', { class: 'key', text: r.gp }),
    ))),
  );
  const deviceList = h('ul', { class: 'device-list' });
  let signature = '';
  const refreshDevices = () => {
    const devices = api.ctx.input.devices();
    const sig = devices.map((d) => `${d.id}|${d.label}|${d.connected}|${d.boundSeat}`).join(';');
    if (sig === signature) return;
    signature = sig;
    deviceList.replaceChildren(...devices.map((d) => h('li', { class: d.connected ? 'device on' : 'device off' },
      h('span', { class: 'device-dot' }),
      h('span', { class: 'device-label', text: d.label }),
      h('span', { class: 'device-state', text: d.connected ? t('ui.controls.connected') : t('ui.controls.disconnected') }),
      h('span', { class: 'device-seat', text: d.boundSeat === null ? t('ui.controls.free') : t('ui.controls.seat', { n: d.boundSeat + 1 }) }),
    )));
  };
  refreshDevices();
  const back = button(t('ui.common.back'), () => api.back());
  const list = createFocusList([back], { sfx: api.sfx });
  const el = screenFrame('controls', t('ui.controls.title'),
    table,
    h('h2', { class: 'sub-title', text: t('ui.controls.detected') }),
    deviceList,
    h('p', { class: 'hint', text: t('ui.controls.gamepadHint') }),
    back.el,
  );
  return {
    el,
    nav: (nav) => listNav(list, nav, api.sfx, () => api.back()),
    update: refreshDevices,
  };
}

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
    rows.push(h('div', { class: 'record-row' },
      h('div', { class: 'record-track' }, h('strong', { text: def.name }), h('span', { class: 'muted', text: ` — ${t(`core.country.${def.country}`)}` })),
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
    back.el,
  );
  return { el, nav: (nav) => listNav(list, nav, api.sfx, () => api.back()) };
}
