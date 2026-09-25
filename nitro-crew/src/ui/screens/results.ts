// Resultado da corrida e classificação do campeonato.
import { isCoop, nextTrackId, teamRaceRank, teamRaceScore } from '../../core/championship';
import { formatTicks } from '../../core/sim/race';
import type { HumanEntry, RaceResultRow, StandingRow } from '../../core/types';
import type { ResultsScreenData, StandingsScreenData } from '../../game/contracts';
import '../../career/strings';
import { t } from '../../i18n';
import { button, createFocusList, h, listNav, screenFrame, type FocusItem, type ScreenApi, type ScreenData, type ScreenInstance } from './common';

/** Título grande com um chip ao lado (pista ou copa), em vez de "Resultado — Nome" numa linha só que quebra. */
function titleRow(title: string, chip: string): HTMLElement {
  return h('div', { class: 'screen-title-row' }, h('h1', { class: 'screen-title', text: title }), h('span', { class: 'chip', text: chip }));
}
import { icon, medal } from './icons';

function humanColor(humans: HumanEntry[], seat: number): string | null {
  return humans.find((x) => x.seat === seat)?.color ?? null;
}

function rowStyle(humans: HumanEntry[], seat: number): string {
  const color = seat >= 0 ? humanColor(humans, seat) : null;
  return color ? `--seat:${color}` : '';
}

/** Célula da posição: medalha para o pódio, número para o resto. */
function positionCell(position: number): HTMLElement {
  const m = medal(position);
  return h('td', { class: 'mono pos' }, m ?? String(position));
}

export function resultsScreen(api: ScreenApi, data?: ScreenData): ScreenInstance {
  const d = data as ResultsScreenData | undefined;
  if (!d || !('results' in d)) {
    const back = button(t('ui.results.menu'), () => api.emit({ type: 'toMain' }));
    const list = createFocusList([back], { sfx: api.sfx });
    return { el: screenFrame('results', t('ui.results.title'), back.el), nav: (nav) => listNav(list, nav, api.sfx) };
  }
  const { ctx } = api;
  const carName = (id: string) => ctx.cars.find((c) => c.id === id)?.name ?? id;
  const isRecord = (seat: number, kind: 'lap' | 'race') => d.newRecords.some((r) => r.seat === seat && r.kind === kind);
  const badge = () => h('span', { class: 'record-badge', text: t('ui.results.record') });
  const rows = [...d.results].sort((a, b) => a.position - b.position);

  const table = h('table', { class: 'table results-table' },
    h('thead', {}, h('tr', {},
      h('th', { text: '#' }), h('th', { text: t('ui.results.name') }), h('th', { text: t('ui.results.car') }),
      h('th', { text: t('ui.results.time') }), h('th', { text: t('ui.results.bestLap') }), h('th', { class: 'num', text: t('ui.results.points') }),
    )),
    h('tbody', {}, rows.map((r: RaceResultRow) => h('tr', { class: r.seat >= 0 ? 'human' : '', style: rowStyle(d.humans, r.seat) },
      positionCell(r.position),
      h('td', { text: r.name }),
      h('td', { class: 'muted-cell', text: carName(r.carDefId) }),
      h('td', { class: 'mono' }, r.finished ? formatTicks(r.totalTicks) : t('ui.results.dnf'), r.seat >= 0 && isRecord(r.seat, 'race') ? badge() : null),
      h('td', { class: 'mono' }, formatTicks(r.bestLapTicks), r.seat >= 0 && isRecord(r.seat, 'lap') ? badge() : null),
      h('td', { class: 'mono num', text: String(r.points) }),
    ))),
  );

  const extras: HTMLElement[] = [];
  const coop = d.champ ? d.champ.coop : isCoop(d.humans);
  const cupLike = d.mode === 'cup' || d.mode === 'career';
  if (cupLike && d.champ?.lastVerdict) {
    const ok = d.champ.lastVerdict === 'qualified';
    extras.push(h('div', { class: `verdict ${ok ? 'good' : 'bad'}`, text: ok ? t('ui.results.qualified') : t('ui.results.eliminated') }));
  }
  if (coop && d.humans.length > 0) {
    const teamId = d.humans[0].teamId;
    extras.push(h('p', { class: 'team-line' }, icon('users'), h('span', { text: t('ui.results.team', { team: t('core.team.human'), points: teamRaceScore(d.results, teamId), rank: teamRaceRank(d.results, teamId) }) })));
  }

  const items: FocusItem[] = [];
  if (cupLike && d.champ) {
    const champ = d.champ;
    const cup = ctx.cups.find((c) => c.id === champ.cupId);
    if (cup) items.push(button(t('ui.results.standings'), () => api.go('standings', { champ, humans: d.humans, cup, career: d.mode === 'career' }), 'btn-primary'));
    else items.push(button(t('ui.results.menu'), () => api.emit({ type: 'toMain' })));
  } else {
    items.push(button(t('ui.results.retry'), () => api.emit({ type: 'retryRace' }), 'btn-primary'));
    items.push(button(t('ui.results.menu'), () => api.emit({ type: 'toMain' })));
  }
  const list = createFocusList(items, { sfx: api.sfx });
  const el = screenFrame('results', null,
    titleRow(t('ui.results.title'), d.trackDef.name),
    h('div', { class: 'results-head' }, extras),
    h('div', { class: 'table-wrap glass' }, table),
    h('div', { class: 'actions' }, items.map((i) => i.el)),
  );
  return { el, nav: (nav) => listNav(list, nav, api.sfx) };
}

export function standingsScreen(api: ScreenApi, data?: ScreenData): ScreenInstance {
  const d = data as StandingsScreenData | undefined;
  if (!d || !('champ' in d) || !('cup' in d)) {
    const back = button(t('ui.standings.menu'), () => api.emit({ type: 'toMain' }));
    const list = createFocusList([back], { sfx: api.sfx });
    return { el: screenFrame('standings', t('ui.standings.title'), back.el), nav: (nav) => listNav(list, nav, api.sfx) };
  }
  const { champ, humans, cup } = d;
  const raceCount = cup.trackIds.length;
  const top = champ.standings.filter((s, i) => i < 10 || s.seat >= 0);
  const driverTable = h('table', { class: 'table standings-table' },
    h('thead', {}, h('tr', {},
      h('th', { text: '#' }), h('th', { text: t('ui.results.name') }), h('th', { class: 'num', text: t('ui.standings.points') }), h('th', { class: 'num', text: t('ui.standings.wins') }),
      Array.from({ length: raceCount }, (_, i) => h('th', { class: 'num', text: t('ui.standings.race', { n: i + 1 }) })),
    )),
    h('tbody', {}, top.map((s: StandingRow) => h('tr', { class: s.seat >= 0 ? 'human' : '', style: rowStyle(humans, s.seat) },
      positionCell(champ.standings.indexOf(s) + 1),
      h('td', { text: s.name }),
      h('td', { class: 'mono num strong', text: String(s.points) }),
      h('td', { class: 'mono num', text: String(s.wins) }),
      s.positions.map((p) => h('td', { class: 'mono num', text: p > 0 ? String(p) : '–' })),
    ))),
  );
  const teamTable = h('table', { class: 'table teams-table' },
    h('thead', {}, h('tr', {}, h('th', { text: '#' }), h('th', { text: t('ui.standings.teams') }), h('th', { class: 'num', text: t('ui.standings.points') }))),
    h('tbody', {}, champ.teams.map((tm, i) => h('tr', { class: tm.isHuman ? 'human' : '', style: tm.isHuman && humans.length > 0 ? `--seat:${humans[0].color}` : '' },
      positionCell(i + 1),
      h('td', { text: tm.name }),
      h('td', { class: 'mono num strong', text: String(tm.points) }),
    ))),
  );

  const next = nextTrackId(champ);
  const nextDef = next ? api.ctx.tracks.find((x) => x.id === next) : undefined;
  let status: HTMLElement;
  if (champ.completed) status = h('div', { class: 'verdict good big' }, icon('trophy'), h('span', { text: t('ui.standings.champion') }));
  else if (champ.eliminated) status = h('div', { class: 'verdict bad big', text: t('ui.standings.eliminated') });
  else status = h('p', { class: 'status-line' }, icon('flag'), h('span', { text: t('ui.standings.next', { n: champ.raceIndex + 1, m: raceCount, track: nextDef?.name ?? next ?? '?' }) }));

  const items: FocusItem[] = [];
  // Carreira: depois da classificação vem sempre a garagem (prêmio, compras, próxima corrida ou copa).
  if (d.career) items.push(button(t('ui.standings.garage'), () => api.emit({ type: 'nextRace' }), 'btn-primary'));
  else if (next && !champ.eliminated && !champ.completed) items.push(button(t('ui.standings.nextBtn'), () => api.emit({ type: 'nextRace' }), 'btn-primary'));
  else items.push(button(t('ui.standings.menu'), () => api.emit({ type: 'toMain' }), 'btn-primary'));
  const list = createFocusList(items, { sfx: api.sfx });
  const el = screenFrame('standings', null,
    titleRow(t('ui.standings.title'), t(`core.cup.${cup.id}`)),
    h('div', { class: 'results-head' }, status),
    h('div', { class: 'standings-columns' },
      h('div', { class: 'table-wrap glass' }, h('h2', { class: 'sub-title', text: t('ui.standings.drivers') }), driverTable),
      h('div', { class: 'table-wrap glass' }, h('h2', { class: 'sub-title', text: t('ui.standings.teams') }), teamTable),
    ),
    h('div', { class: 'actions' }, items.map((i) => i.el)),
  );
  return { el, nav: (nav) => listNav(list, nav, api.sfx) };
}
