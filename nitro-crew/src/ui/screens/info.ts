// Telas de consulta: controles (mapeamentos + dispositivos detectados) e recordes (três abas:
// recordes por pista, estatísticas por jogador e conquistas).
import { formatTicks } from '../../core/sim/race';
import type { TrackDef } from '../../core/types';
import { achievementDescription, achievementName } from '../../game/achievements';
import { ACHIEVEMENTS } from '../../game/desktop';
import {
  COUNTER_KEYS, formatCount, formatDistance, formatDuration, MAX_PROFILES, playerLine, recordsLine, tracksRaced, type CounterKey, type PlayerStats,
} from '../../game/stats';
import { getLanguage, t } from '../../i18n';
import '../../stats/strings';
import { isKeyboard } from '../input';
import { arrowButton, blurActive, button, createFocusList, h, listNav, screenFrame, type FocusItem, type FocusList, type ScreenApi, type ScreenInstance } from './common';
import { icon, medal } from './icons';
import './records.css';

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

/** Cada tecla numa "tecla" desenhada; separadores "/" viram texto solto. */
function keys(text: string): HTMLElement {
  return h('span', { class: 'keys' }, text.split(' / ').flatMap((k, i) => [i > 0 ? h('span', { class: 'key-sep', text: '/' }) : null, h('kbd', { text: k })]));
}

export function controlsScreen(api: ScreenApi): ScreenInstance {
  const table = h('table', { class: 'table controls-table' },
    h('thead', {}, h('tr', {},
      h('th', { text: t('ui.controls.action') }),
      h('th', {}, icon('keyboard'), ` ${t('ui.device.kb1')}`),
      h('th', {}, icon('keyboard'), ` ${t('ui.device.kb2')}`),
      h('th', {}, icon('gamepad'), ` ${t('ui.controls.gamepad')}`),
    )),
    h('tbody', {}, controlRows().map((r) => h('tr', {},
      h('td', { text: r.action }),
      h('td', {}, keys(r.kb1)),
      h('td', {}, keys(r.kb2)),
      h('td', {}, keys(r.gp)),
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
      icon(isKeyboard(d.id) ? 'keyboard' : 'gamepad'),
      h('span', { class: 'device-label', text: d.label }),
      h('span', { class: 'device-state', text: d.connected ? t('ui.controls.connected') : t('ui.controls.disconnected') }),
      h('span', { class: `device-seat${d.boundSeat === null ? '' : ' bound'}`, text: d.boundSeat === null ? t('ui.controls.free') : t('ui.controls.seat', { n: d.boundSeat + 1 }) }),
    )));
  };
  refreshDevices();
  const back = button(t('ui.common.back'), () => api.back());
  const list = createFocusList([back], { sfx: api.sfx });
  const el = screenFrame('controls', t('ui.controls.title'),
    h('div', { class: 'controls-columns' },
      h('div', { class: 'table-wrap glass' }, table),
      h('div', { class: 'devices-panel glass' },
        h('h2', { class: 'sub-title', text: t('ui.controls.detected') }),
        deviceList,
        h('p', { class: 'hint', text: t('ui.controls.gamepadHint') }),
      ),
    ),
    h('div', { class: 'actions' }, back.el),
  );
  return {
    el,
    nav: (nav) => listNav(list, nav, api.sfx, () => api.back()),
    update: refreshDevices,
  };
}


// ───────────────────────────── Recordes ─────────────────────────────

type RecordsTab = 'tracks' | 'players' | 'achievements';
const RECORD_TABS: readonly RecordsTab[] = ['tracks', 'players', 'achievements'];

/** Uma aba: conteúdo rolável, a lista de foco (linhas + Voltar) e o que refazer a cada quadro. */
interface TabView {
  el: HTMLElement;
  list: FocusList;
  update?(): void;
}

/** Linhas focáveis rolam sozinhas até ficar visíveis (createFocusList chama scrollIntoView). */
function tabView(api: ScreenApi, rows: FocusItem[], content: HTMLElement, update?: (list: FocusList) => void): TabView {
  const back = button(t('ui.common.back'), () => api.back());
  const list = createFocusList([...rows, back], { sfx: api.sfx });
  const el = h('div', { class: 'rec-view' }, h('div', { class: 'rec-scroll' }, content), h('div', { class: 'actions' }, back.el));
  return { el, list, update: update ? () => update(list) : undefined };
}

function tracksTab(api: ScreenApi): TabView {
  const { save, tracks, cars } = api.ctx;
  const carName = (id: string) => cars.find((c) => c.id === id)?.name ?? id;
  const rows: FocusItem[] = [];
  for (const def of tracks) {
    const lap = save.bestLaps[def.id];
    const races = Object.entries(save.bestRaces)
      .filter(([key]) => key.startsWith(`${def.id}:`))
      .map(([key, rec]) => ({ laps: Number(key.slice(def.id.length + 1)), rec }))
      .sort((a, b) => a.laps - b.laps);
    if (!lap && races.length === 0) continue;
    rows.push({ el: h('div', { class: 'record-row glass' },
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
    ) });
  }
  const content = rows.length > 0
    ? h('div', { class: 'record-list' }, h('p', { class: 'hint rec-hint', text: t('stats.tracks.hint') }), rows.map((r) => r.el))
    : h('p', { class: 'empty', text: t('ui.records.empty') });
  return tabView(api, rows, content);
}

function statText(key: CounterKey, s: PlayerStats): string {
  if (key === 'meters') return formatDistance(s.meters, getLanguage());
  if (key === 'raceTicks') return formatDuration(s.raceTicks);
  return formatCount(s[key]);
}

/** Cabeçalho e contadores do jogador mostrado (refeitos a cada troca de foco). */
function playerSummary(name: string, s: PlayerStats, all: boolean): HTMLElement[] {
  return [
    h('div', { class: 'pl-detail-head' },
      h('h2', { class: 'pl-detail-name' }, all ? icon('users') : null, h('span', { text: name })),
      all ? h('p', { class: 'hint pl-note', text: t('stats.players.allNote', { max: MAX_PROFILES }) }) : null,
    ),
    h('div', { class: 'stat-grid' }, COUNTER_KEYS.map((k) => h('div', { class: `stat-tile stat-${k}` },
      h('span', { class: 'stat-value mono', text: statText(k, s) }),
      h('span', { class: 'stat-label', text: t(`stats.stat.${k}`) }),
    ))),
  ];
}

/** Colunas da grade de melhor posição (records.css usa o mesmo número). */
const BEST_COLS = 3;

function bestChip(d: TrackDef, pos: number | undefined): HTMLElement {
  if (pos === undefined) {
    return h('span', { class: 'best-chip none', attrs: { title: d.name } }, h('b', { class: 'best-pos mono', text: '—' }), h('span', { class: 'best-name', text: d.name }));
  }
  return h('span', { class: `best-chip${pos <= 3 ? ' podium' : ''}`, attrs: { title: d.name } },
    medal(pos) ?? h('b', { class: 'best-pos mono', text: t('stats.best.pos', { n: pos }) }),
    h('span', { class: 'best-name', text: d.name }),
  );
}

/**
 * Melhor posição em todas as pistas do jogo (as ainda não corridas com "—"), em linhas de
 * BEST_COLS. Cada linha é um item de foco: com 32 pistas a grade não cabe em 720p, e o controle
 * só rola o que o foco alcança. Como a grade lista todas as pistas, o número de linhas não
 * depende do jogador e as linhas são criadas uma vez; trocar de jogador só refaz o conteúdo.
 */
function bestPositionsView(api: ScreenApi): { el: HTMLElement; rows: FocusItem[]; fill(s: PlayerStats): void } {
  const tracks = api.ctx.tracks;
  const trackIds = tracks.map((d) => d.id);
  const count = h('span', { class: 'best-count' });
  const none = h('p', { class: 'hint best-none', text: t('stats.best.none') });
  const rows: FocusItem[] = [];
  for (let i = 0; i < tracks.length; i += BEST_COLS) rows.push({ el: h('div', { class: `best-row${i === 0 ? ' first' : ''}` }) });
  const el = h('div', { class: 'best-block' },
    h('div', { class: 'best-head' }, h('h3', { class: 'sub-title', text: t('stats.best.title') }), count),
    none,
    rows.map((r) => r.el),
  );
  return {
    el,
    rows,
    fill(s) {
      const raced = tracksRaced(s, trackIds);
      count.textContent = t('stats.best.count', { n: raced, total: tracks.length });
      none.style.display = raced > 0 ? 'none' : '';
      rows.forEach((r, i) => r.el.replaceChildren(...tracks.slice(i * BEST_COLS, (i + 1) * BEST_COLS).map((d) => bestChip(d, s.bestPositions[d.id]))));
    },
  };
}

/**
 * Lista de jogadores à esquerda (a primeira linha é o total) e o detalhe de quem está focado.
 * As duas colunas rolam cada uma por si. Depois do último jogador o foco desce pelas linhas da
 * melhor posição por pista (bestPositionsView) e então chega ao Voltar; o detalhe continua no
 * último jogador mostrado, e volta ao topo quando o foco volta à lista.
 */
function playersTab(api: ScreenApi): TabView {
  const stats = api.ctx.save.stats;
  if (stats.totals.races === 0 && stats.players.length === 0) {
    return tabView(api, [], h('p', { class: 'empty', text: t('stats.players.empty') }));
  }
  const entries: Array<{ name: string; s: PlayerStats; all: boolean }> = [
    { name: t('stats.players.all'), s: stats.totals, all: true },
    ...stats.players.map((p) => ({ name: p.name, s: p, all: false })),
  ];
  const rows: FocusItem[] = entries.map((e) => ({ el: h('div', { class: `pl-row${e.all ? ' all' : ''}` },
    e.all ? h('span', { class: 'pl-avatar all' }, icon('users')) : h('span', { class: 'pl-avatar', text: e.name.slice(0, 1).toUpperCase() }),
    h('span', { class: 'pl-text' },
      h('span', { class: 'pl-name', text: e.name }),
      // O total não repete "N corridas": somando jogadores, ele não bate com o cabeçalho (uma por corrida).
      h('span', { class: 'pl-sub', text: e.all ? t('stats.players.allSummary') : playerLine(e.s) }),
    ),
  ) }));
  const summary = h('div', { class: 'pl-summary' });
  const best = bestPositionsView(api);
  const detail = h('div', { class: 'pl-detail glass' }, summary, best.el);
  let shown = -1;
  let wasOnPlayer = true;
  const show = (list: FocusList) => {
    const onPlayer = list.index >= 0 && list.index < entries.length;
    // Voltou da grade de melhor posição para a lista: o resumo do jogador volta à vista.
    if (onPlayer && !wasOnPlayer) detail.scrollTop = 0;
    wasOnPlayer = onPlayer;
    const i = onPlayer ? list.index : Math.max(0, shown);
    if (i === shown) return;
    shown = i;
    const e = entries[i];
    summary.replaceChildren(...playerSummary(e.name, e.s, e.all));
    best.fill(e.s);
    detail.scrollTop = 0;
    rows.forEach((r, j) => r.el.classList.toggle('shown', j === i));
  };
  const content = h('div', { class: 'players-layout' },
    h('div', { class: 'pl-list glass' }, h('p', { class: 'hint pl-hint', text: t('stats.players.hint') }), rows.map((r) => r.el)),
    detail,
  );
  const view = tabView(api, [...rows, ...best.rows], content, show);
  show(view.list);
  return view;
}

function achievementsTab(api: ScreenApi): TabView {
  const got = new Set(api.ctx.save.achievements);
  const card = (id: string, unlocked: boolean): FocusItem => ({ el: h('div', { class: `ach-card ${unlocked ? 'on' : 'off'}` },
    h('span', { class: 'ach-badge' }, icon(unlocked ? 'trophy' : 'lock')),
    h('span', { class: 'ach-text' },
      h('strong', { class: 'ach-name', text: achievementName(id) }),
      h('span', { class: 'ach-desc', text: achievementDescription(id) }),
    ),
    h('span', { class: 'ach-state', text: unlocked ? t('stats.ach.stateOn') : t('stats.ach.stateOff') }),
  ) });
  const onCards = ACHIEVEMENTS.filter((a) => got.has(a.id)).map((a) => card(a.id, true));
  const offCards = ACHIEVEMENTS.filter((a) => !got.has(a.id)).map((a) => card(a.id, false));
  const pct = Math.round((onCards.length / ACHIEVEMENTS.length) * 100);
  const group = (title: string, cards: FocusItem[]) => (cards.length === 0 ? null : h('div', { class: 'ach-group' },
    h('h3', { class: 'sub-title', text: `${title} · ${cards.length}` }),
    cards.map((c) => c.el),
  ));
  const content = h('div', { class: 'ach-list' },
    h('div', { class: 'ach-progress glass' },
      h('span', { class: 'ach-progress-text', text: t('stats.ach.progress', { n: onCards.length, total: ACHIEVEMENTS.length }) }),
      h('span', { class: 'ach-bar' }, h('span', { class: 'ach-bar-fill', style: `width:${pct}%` })),
    ),
    group(t('stats.ach.unlocked'), onCards),
    group(t('stats.ach.locked'), offCards),
  );
  return tabView(api, [...onCards, ...offCards], content);
}

const TAB_BUILDERS: Readonly<Record<RecordsTab, (api: ScreenApi) => TabView>> = {
  tracks: tracksTab, players: playersTab, achievements: achievementsTab,
};

export function recordsScreen(api: ScreenApi): ScreenInstance {
  const { save } = api.ctx;
  let tab = 0;
  const unlocked = ACHIEVEMENTS.filter((a) => save.achievements.includes(a.id)).length;
  const tabLabel = (id: RecordsTab) => (id === 'achievements' ? `${t('stats.tab.achievements')} ${unlocked}/${ACHIEVEMENTS.length}` : t(`stats.tab.${id}`));
  const tabButtons = RECORD_TABS.map((id, i) => h('button', {
    class: 'rec-tab', text: tabLabel(id), attrs: { type: 'button', role: 'tab' },
    on: { click: () => { blurActive(); if (select(i)) api.sfx('move'); } },
  }));
  const markTabs = () => tabButtons.forEach((b, j) => { b.classList.toggle('on', j === tab); b.setAttribute('aria-selected', String(j === tab)); });
  let view: TabView = TAB_BUILDERS[RECORD_TABS[tab]](api);
  const holder = h('div', { class: 'rec-holder' }, view.el);

  function select(i: number): boolean {
    const next = (i + RECORD_TABS.length) % RECORD_TABS.length;
    if (next === tab) return false;
    tab = next;
    view = TAB_BUILDERS[RECORD_TABS[tab]](api);
    holder.replaceChildren(view.el);
    markTabs();
    return true;
  }
  markTabs();

  const el = screenFrame('records', t('ui.records.title'),
    h('p', { class: 'hint', text: recordsLine(save.racesRun, save.racesWon, save.cupsCompleted.length) }),
    h('div', { class: 'rec-tabs', attrs: { role: 'tablist' } },
      arrowButton(-1, () => { select(tab - 1); api.sfx('move'); }),
      h('div', { class: 'rec-tab-row' }, tabButtons),
      arrowButton(1, () => { select(tab + 1); api.sfx('move'); }),
    ),
    h('p', { class: 'hint rec-tabs-hint', text: t('stats.tabs.hint') }),
    holder,
  );
  return {
    el,
    nav(nav) {
      // ◀ ▶ trocam de aba: as abas são listas verticais, esquerda/direita não têm outro uso aqui.
      if (nav.left || nav.right) { select(tab + (nav.left ? -1 : 1)); api.sfx('move'); }
      listNav(view.list, { ...nav, left: false, right: false }, api.sfx, () => api.back());
      view.update?.();
    },
    update() { view.update?.(); },
  };
}
