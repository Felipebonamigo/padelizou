// Telas dos modos de festa (docs/MODOS.md): a escolha do modo ("Festa"), a inscrição do torneio de
// sofá, "passe o controle" entre baterias, a classificação do torneio e o pedaço do resultado que
// cada modo acrescenta. O estado do torneio vem de ctx.party (src/game/party-session.ts).
import { seatColor } from '../../core/data/drivers';
import { formatTicks } from '../../core/sim/race';
import {
  currentHeat, finalistCount, heatSizes, setupError, TOURNAMENT_MAX_PLAYERS, TOURNAMENT_MAX_ROUNDS, TOURNAMENT_MIN_PLAYERS,
  TOURNAMENT_MIN_ROUNDS, tournamentRanking, tournamentStandings, type TournamentPlayer, type TournamentSetup, type TournamentState,
} from '../../core/tournament';
import type { CupDef } from '../../core/types';
import type { PartyMode, ResultsScreenData } from '../../game/contracts';
import { finalIsNext, heatLabel, qualifyingDone, qualifyingHeatCount, type TournamentDraft } from '../../game/party-session';
import { NAME_MAX_LENGTH } from '../../game/save';
import { t } from '../../i18n';
import { ordinalText } from '../../party/rules';
import '../../party/strings';
import { arrowButton, button, createFocusList, lapsText, h, listNav, screenFrame, selector, trackName, trackThumb, type FocusItem, type FocusList, type ScreenApi, type ScreenInstance } from './common';
import { icon, medal, type IconName } from './icons';
import { availableCars, occupiedSeats, startCursor } from './lobby';
import { commitSettings, raceOptionSelectors } from './options';
import './lobby.css';
import './party.css';

// ───────────────────────────── Festa: escolha do modo ─────────────────────────────

const MODE_ICON: Readonly<Record<PartyMode, IconName>> = { tournament: 'trophy', escort: 'users', relay: 'timer' };

export function partyScreen(api: ScreenApi): ScreenInstance {
  const modes: PartyMode[] = ['tournament', 'escort', 'relay'];
  const choose = (mode: PartyMode) => () => {
    api.lobby.mode = mode;
    api.lobby.resume = false;
    // Como no menu principal: quem continua sentado volta como "não pronto".
    for (const seat of api.lobby.seats) if (seat) { seat.ready = false; seat.cursor = startCursor(api.lobby); }
    api.go('lobby');
  };
  const cards: FocusItem[] = modes.map((mode) => ({
    el: h('div', { class: 'party-card glass', attrs: { 'data-mode': mode } },
      h('span', { class: 'party-card-icon' }, icon(MODE_ICON[mode])),
      h('strong', { class: 'party-card-name', text: t(`ui.main.${mode}`) }),
      h('span', { class: 'party-card-who', text: t(`party.mode.${mode}.who`) }),
      h('p', { class: 'party-card-desc', text: t(`party.mode.${mode}.desc`) }),
    ),
    activate: choose(mode),
  }));
  const back = button(t('ui.common.back'), () => api.back());
  const list = createFocusList([...cards, back], { cols: modes.length, sfx: api.sfx });
  const el = screenFrame('party', t('party.title'),
    h('div', { class: 'party-cards' }, cards.map((c) => c.el)),
    h('p', { class: 'hint party-local' }, icon('lock'), h('span', { text: t('party.localOnly') })),
    h('div', { class: 'actions' }, back.el),
  );
  return { el, nav: (nav) => listNav(list, nav, api.sfx, () => { api.sfx('back'); api.back(); }) };
}

// ───────────────────────────── Torneio: inscrição ─────────────────────────────

/** Sem sessão completa (testes de interface): só volta. */
function missingParty(api: ScreenApi, name: string): ScreenInstance {
  const back = button(t('ui.common.back'), () => api.emit({ type: 'toMain' }));
  const list = createFocusList([back], { sfx: api.sfx });
  return { el: screenFrame(name, t('party.title'), back.el), nav: (nav) => listNav(list, nav, api.sfx) };
}

/** Pilotos iniciais da inscrição: os nomes mais recentes das estatísticas, depois "Piloto N". */
export function defaultPlayers(count: number, recentNames: readonly string[], carIds: readonly string[]): TournamentPlayer[] {
  const out: TournamentPlayer[] = [];
  const used = new Set<string>();
  for (const name of recentNames) {
    if (out.length >= count) break;
    const key = name.trim().toLowerCase();
    if (!key || used.has(key)) continue;
    used.add(key);
    out.push({ name: name.trim().slice(0, NAME_MAX_LENGTH), carId: carIds[out.length % carIds.length] });
  }
  for (let n = 1; out.length < count; n++) {
    const name = t('party.t.defaultName', { n });
    if (used.has(name.toLowerCase())) continue;
    used.add(name.toLowerCase());
    out.push({ name, carId: carIds[out.length % carIds.length] });
  }
  return out;
}

/** Nome livre para um piloto novo ("Piloto 5"). */
function freshName(players: readonly TournamentPlayer[]): string {
  const taken = new Set(players.map((p) => p.name.trim().toLowerCase()));
  for (let n = players.length + 1; ; n++) {
    const name = t('party.t.defaultName', { n });
    if (!taken.has(name.toLowerCase())) return name;
  }
}

function unlockedCups(api: ScreenApi): CupDef[] {
  return api.ctx.cups.filter((c) => api.ctx.isCupUnlocked(c.id));
}

/** Pistas do torneio a partir da copa: uma por rodada, e a última da copa na final. */
export function tournamentTracks(cup: CupDef, rounds: number): string[] {
  const ids = cup.trackIds;
  return [...ids.slice(0, Math.min(rounds, ids.length - 1)), ids[ids.length - 1]];
}

/**
 * Teto de rodadas classificatórias numa copa: cada rodada usa uma pista e a final a última, então rodadas + final
 * cabem nas pistas da copa (as de estado têm 3 → até 2 rodadas; as do Mundial, 4 → até 3).
 */
export function tournamentMaxRounds(cup: CupDef): number {
  return Math.max(TOURNAMENT_MIN_ROUNDS, Math.min(TOURNAMENT_MAX_ROUNDS, cup.trackIds.length - 1));
}

/** Inscrição → torneio; rodadas pedidas acima do teto da copa (trocou para uma copa de 3 pistas) caem para o teto. */
export function draftSetup(draft: TournamentDraft, cup: CupDef, controllers: number, laps: number): TournamentSetup {
  const rounds = Math.min(draft.rounds, tournamentMaxRounds(cup));
  return { players: draft.players.map((p) => ({ ...p })), controllers, rounds, trackIds: tournamentTracks(cup, rounds), laps };
}

export function tournamentScreen(api: ScreenApi): ScreenInstance {
  const party = api.ctx.party;
  if (!party) return missingParty(api, 'tournament');
  const { ctx } = api;
  const draft = party.draft;
  const cars = availableCars(ctx);
  const carIds = cars.map((c) => c.id);
  const seats = occupiedSeats(api.lobby).map((s) => s.seat);
  const controllers = Math.max(1, seats.length);
  if (draft.players.length === 0) {
    draft.players = defaultPlayers(Math.max(TOURNAMENT_MIN_PLAYERS, controllers), ctx.save.stats.players.map((p) => p.name), carIds);
  }
  // Carro que deixou de existir ou não está liberado: volta ao primeiro.
  for (const p of draft.players) if (!carIds.includes(p.carId)) p.carId = carIds[0];
  const cups = unlockedCups(api);
  if (!cups.some((c) => c.id === draft.cupId)) draft.cupId = cups[0]?.id ?? ctx.cups[0].id;
  const cupOf = () => cups.find((c) => c.id === draft.cupId) ?? ctx.cups[0];

  const body = h('div', { class: 'tp-layout' });
  const el = screenFrame('tournament', null,
    h('div', { class: 'screen-title-row' }, h('h1', { class: 'screen-title', text: t('party.t.title') }), h('span', { class: 'chip', text: t(`party.t.controllers.${controllers === 1 ? 'one' : 'other'}`, { n: controllers }) })),
    body,
  );
  let list: FocusList | null = null;
  let focusAt = 0;

  const setup = () => draftSetup(draft, cupOf(), controllers, ctx.settings.quickLaps);

  function playerRow(p: TournamentPlayer, i: number): FocusItem {
    const input = h('input', {
      class: 'name-input',
      attrs: { type: 'text', maxlength: String(NAME_MAX_LENGTH), value: p.name, spellcheck: 'false', autocomplete: 'off', 'aria-label': t('ui.lobby.name') },
      on: { input: () => { p.name = input.value; refreshStatus(); } },
    });
    const carName = h('span', { class: 'tp-car-name', text: cars.find((c) => c.id === p.carId)?.name ?? p.carId });
    const swatch = h('i', { class: 'tp-swatch', style: `background:${cars.find((c) => c.id === p.carId)?.color ?? '#fff'}` });
    const changeCar = (dir: -1 | 1) => {
      const k = carIds.indexOf(p.carId);
      p.carId = carIds[(k + dir + carIds.length) % carIds.length];
      const def = cars.find((c) => c.id === p.carId);
      carName.textContent = def?.name ?? p.carId;
      swatch.style.background = def?.color ?? '#fff';
    };
    const row = h('div', { class: 'tp-row', attrs: { 'data-player': String(i) } },
      h('span', { class: 'tp-n mono', text: String(i + 1) }),
      input,
      h('span', { class: 'sel-box tp-car' },
        arrowButton(-1, () => { changeCar(-1); api.sfx('move'); }),
        swatch, carName,
        arrowButton(1, () => { changeCar(1); api.sfx('move'); }),
      ),
    );
    return { el: row, activate: () => { input.focus(); input.select(); }, adjust: changeCar };
  }

  const summary = h('p', { class: 'tp-summary' });
  const tracksLine = h('p', { class: 'hint tp-tracks' });
  const error = h('p', { class: 'hint tp-error' });
  let startBtn: FocusItem | null = null;

  function refreshStatus(): void {
    const s = setup();
    const heats = heatSizes(s.players.length, controllers).length;
    const fin = finalistCount(s);
    summary.textContent = t('party.t.summary', {
      heats: t(`party.t.heats.${heats === 1 ? 'one' : 'other'}`, { n: heats }),
      final: fin > 0 ? t('party.t.final', { n: fin }) : t('party.t.noFinal'),
    });
    const rounds = s.trackIds.slice(0, s.rounds).map((id) => trackName(id)).join(', ');
    tracksLine.textContent = t('party.t.tracksLine', { tracks: rounds, final: trackName(s.trackIds[s.trackIds.length - 1]) });
    const err = setupError(s);
    error.textContent = err ? t(err) : t('party.t.nameHint');
    error.classList.toggle('bad', err !== null);
    if (startBtn) {
      startBtn.disabled = err !== null;
      startBtn.el.classList.toggle('disabled', err !== null);
    }
  }

  function render(): void {
    if (list && list.index >= 0) focusAt = list.index;
    const rows = draft.players.map(playerRow);
    const add = button(t('party.t.add'), () => {
      if (draft.players.length >= TOURNAMENT_MAX_PLAYERS) { api.sfx('back'); return; }
      draft.players.push({ name: freshName(draft.players), carId: carIds[draft.players.length % carIds.length] });
      focusAt = draft.players.length - 1;
      list = null;
      render();
    }, 'btn-small');
    add.disabled = draft.players.length >= TOURNAMENT_MAX_PLAYERS;
    if (add.disabled) add.el.classList.add('disabled');
    const remove = button(t('party.t.remove'), () => {
      if (draft.players.length <= TOURNAMENT_MIN_PLAYERS) { api.sfx('back'); return; }
      draft.players.pop();
      render();
    }, 'btn-small');
    remove.disabled = draft.players.length <= TOURNAMENT_MIN_PLAYERS;
    if (remove.disabled) remove.el.classList.add('disabled');

    // O seletor mostra as rodadas que vão valer: na copa de 3 pistas o teto é 2 (tournamentMaxRounds).
    const rounds = selector(t('party.t.rounds'), () => String(Math.min(draft.rounds, tournamentMaxRounds(cupOf()))), (d) => {
      draft.rounds = Math.min(tournamentMaxRounds(cupOf()), Math.max(TOURNAMENT_MIN_ROUNDS, Math.min(draft.rounds, tournamentMaxRounds(cupOf())) + d));
      refreshStatus();
    }, { sfx: api.sfx });
    const cup = selector(t('party.t.cup'), () => `${cupOf().flag} ${t(`core.cup.${cupOf().id}`)}`, (d) => {
      const k = cups.findIndex((c) => c.id === draft.cupId);
      draft.cupId = cups[(k + d + cups.length) % cups.length].id;
      rounds.refresh();
      refreshStatus();
    }, { sfx: api.sfx });
    const laps = raceOptionSelectors(api, () => { commitSettings(api); refreshStatus(); }, { quickLaps: true, lapsLabel: t('party.t.laps') });
    const start = button(t('party.t.start'), () => {
      const s = setup();
      if (setupError(s)) { api.sfx('back'); return; }
      api.emit({ type: 'startTournament', setup: s, seats });
    }, 'btn-primary btn-start');
    startBtn = start;
    const back = button(t('ui.common.back'), () => { api.sfx('back'); api.back(); });
    const items: FocusItem[] = [...rows, add, remove, rounds, cup, ...laps, start, back];

    body.replaceChildren(
      h('div', { class: 'tp-players glass' },
        h('h2', { class: 'sub-title', text: t('party.t.players', { n: draft.players.length, max: TOURNAMENT_MAX_PLAYERS }) }),
        h('div', { class: 'tp-rows' }, rows.map((r) => r.el)),
        h('div', { class: 'tp-row-actions' }, add.el, remove.el),
      ),
      h('div', { class: 'tp-side glass' },
        h('div', { class: 'lobby-options' }, rounds.el, cup.el, laps.map((l) => l.el)),
        summary, tracksLine, error,
        h('div', { class: 'lobby-actions' }, start.el, back.el),
      ),
    );
    refreshStatus();
    list = createFocusList(items, { start: Math.min(focusAt, items.length - 1), sfx: api.sfx });
  }
  render();

  return {
    el,
    nav(nav) {
      if (!list) return;
      if (nav.start && startBtn && !startBtn.disabled) { startBtn.activate?.(); return; }
      listNav(list, nav, api.sfx, () => api.back());
    },
  };
}

// ───────────────────────────── Torneio: passe o controle ─────────────────────────────

/** Quem segurava cada controle (assento) na bateria anterior: índice de jogador, ou -1. */
export function previousHolders(tour: TournamentState): number[] {
  const last = tour.history[tour.history.length - 1];
  const out = [-1, -1, -1, -1];
  for (const e of last?.entries ?? []) if (e.seat >= 0 && e.seat < out.length) out[e.seat] = e.player;
  return out;
}

export function handoffScreen(api: ScreenApi): ScreenInstance {
  const tour = api.ctx.party?.tournament ?? null;
  const info = tour ? currentHeat(tour) : null;
  if (!tour || !info) return missingParty(api, 'handoff');
  const { ctx } = api;
  const holders = previousHolders(tour);
  const trackDef = ctx.tracks.find((x) => x.id === info.trackId);
  const deviceLabel = (seat: number) => {
    const id = ctx.input.seatDevice(seat);
    return id ? ctx.input.devices().find((d) => d.id === id)?.label ?? id : '—';
  };
  const cards = info.players.map((player, seat) => {
    const p = tour.setup.players[player];
    const keep = holders[seat] === player;
    const car = ctx.cars.find((c) => c.id === p.carId);
    return h('div', { class: `handoff-card glass${keep ? ' keep' : ''}`, style: `--seat:${seatColor(seat, ctx.settings.colorPalette)}`, attrs: { 'data-seat': String(seat) } },
      h('div', { class: 'handoff-head' },
        h('span', { class: 'seat-badge', text: `P${seat + 1}` }),
        h('span', { class: 'handoff-device' }, icon(ctx.input.seatDevice(seat)?.startsWith('kb') ? 'keyboard' : 'gamepad'), h('span', { text: deviceLabel(seat) })),
      ),
      h('strong', { class: 'handoff-name', text: p.name }),
      h('span', { class: 'handoff-car', text: car?.name ?? p.carId }),
      h('p', { class: 'handoff-say', text: keep ? t('party.handoff.keep', { n: seat + 1, name: p.name }) : t('party.handoff.pass', { n: seat + 1, name: p.name }) }),
    );
  });
  const go = button(t('party.handoff.go'), () => api.emit({ type: 'tournamentHeat' }), 'btn-primary btn-start');
  const table = button(t('party.handoff.table'), () => api.go('tournamentTable'));
  const quit = button(t('party.handoff.quit'), () => api.emit({ type: 'toMain' }), 'btn-danger');
  const list = createFocusList([go, table, quit], { sfx: api.sfx });
  const el = screenFrame('handoff', null,
    h('div', { class: 'screen-title-row' }, h('h1', { class: 'screen-title', text: t('party.handoff.title') }), h('span', { class: 'chip', text: heatLabel(tour, info) })),
    h('div', { class: 'handoff-track' },
      trackDef ? trackThumb(ctx, trackDef, 56) : null,
      h('strong', { text: trackName(info.trackId, trackDef?.name) }),
      h('span', { class: 'muted', text: lapsText(tour.setup.laps) }),
    ),
    h('div', { class: `handoff-cards n-${cards.length}` }, cards),
    h('p', { class: 'hint', text: `${t('party.handoff.hint')} ${info.final ? t('party.handoff.grid') : ''}`.trim() }),
    h('div', { class: 'actions' }, go.el, table.el, quit.el),
  );
  return {
    el,
    nav(nav) {
      if (nav.start) { go.activate?.(); return; }
      listNav(list, nav, api.sfx);
    },
  };
}

// ───────────────────────────── Torneio: classificação ─────────────────────────────

export function tournamentTableScreen(api: ScreenApi): ScreenInstance {
  const tour = api.ctx.party?.tournament ?? null;
  if (!tour) return missingParty(api, 'tournamentTable');
  const done = tour.done;
  const rows = done ? tournamentRanking(tour) : tournamentStandings(tour);
  const hasFinal = tour.history.some((x) => x.final);
  const finalists = tour.finalists;
  const table = h('table', { class: 'table tournament-table' },
    h('thead', {}, h('tr', {},
      h('th', { text: '#' }), h('th', { text: t('party.table.name') }), h('th', { class: 'num', text: t('party.table.points') }),
      h('th', { class: 'num', text: t('party.table.heats') }), h('th', { class: 'num', text: t('party.table.best') }),
      h('th', { class: 'num', text: t('party.table.time') }), hasFinal ? h('th', { class: 'num', text: t('party.table.final') }) : null,
    )),
    h('tbody', {}, rows.map((r, i) => h('tr', { class: finalists.includes(r.player) ? 'human finalist' : '', attrs: { 'data-player': String(r.player) } },
      h('td', { class: 'mono pos' }, done ? medal(i + 1) ?? String(i + 1) : String(i + 1)),
      h('td', {}, h('span', { text: r.name }), !done && finalists.includes(r.player) ? h('span', { class: 'tp-badge', text: t('party.table.finalist') }) : null),
      h('td', { class: 'mono num strong', text: String(r.points) }),
      h('td', { class: 'mono num', text: String(r.heats) }),
      h('td', { class: 'mono num', text: r.best > 0 ? ordinalText(r.best) : '–' }),
      h('td', { class: 'mono num', text: r.heats === 0 ? '–' : r.ticks < 0 ? t('ui.results.dnf') : formatTicks(r.ticks) }),
      hasFinal ? h('td', { class: 'mono num', text: r.finalPosition > 0 ? ordinalText(r.finalPosition) : '–' }) : null,
    ))),
  );
  const total = qualifyingHeatCount(tour.setup);
  let status: HTMLElement;
  if (done) status = h('div', { class: 'verdict good big' }, icon('trophy'), h('span', { text: t('party.table.champion', { name: rows[0]?.name ?? '?' }) }));
  else if (finalIsNext(tour)) status = h('p', { class: 'status-line' }, icon('flag'), h('span', { text: t('party.table.finalists', { n: finalists.length }) }));
  else status = h('p', { class: 'status-line' }, icon('flag'), h('span', { text: t('party.table.after', { n: qualifyingDone(tour), m: total }) }));

  const items: FocusItem[] = [];
  if (done) items.push(button(t('party.table.menu'), () => api.emit({ type: 'toMain' }), 'btn-primary'));
  else {
    items.push(button(finalIsNext(tour) ? t('party.table.toFinal') : t('party.table.next'), () => api.go('handoff'), 'btn-primary'));
    items.push(button(t('party.handoff.quit'), () => api.emit({ type: 'toMain' }), 'btn-danger'));
  }
  const list = createFocusList(items, { sfx: api.sfx });
  const el = screenFrame('tournamentTable', null,
    h('div', { class: 'screen-title-row' }, h('h1', { class: 'screen-title', text: t('party.table.title') }), h('span', { class: 'chip', text: t(`party.table.chip.${total === 1 ? 'one' : 'other'}`, { n: tour.setup.players.length, heats: total }) })),
    h('div', { class: 'results-head' }, status),
    h('div', { class: 'table-wrap glass' }, table),
    h('p', { class: 'hint', text: t('party.table.tiebreak') }),
    h('div', { class: 'actions' }, items.map((i) => i.el)),
  );
  return { el, nav: (nav) => listNav(list, nav, api.sfx) };
}

// ───────────────────────────── Resultado ─────────────────────────────

/** Linha do VIP no resultado da escolta (destacada como a de um humano, em dourado). */
export function isVipRow(d: ResultsScreenData, row: { carId: number }): boolean {
  return d.party?.kind === 'escort' && d.party.vipCarId === row.carId;
}

/**
 * O que o resultado de uma corrida de festa acrescenta: linhas no cabeçalho e, no torneio, o
 * botão que segue para a classificação no lugar de "correr de novo" (null = botões de sempre).
 */
export function partyResultsParts(api: ScreenApi, d: ResultsScreenData): { extras: HTMLElement[]; items: FocusItem[] | null } {
  const info = d.party;
  if (!info) return { extras: [], items: null };
  if (info.kind === 'tournament') {
    return {
      extras: [h('p', { class: 'status-line' }, icon('trophy'), h('span', { text: info.label }))],
      items: [button(t('party.results.continue'), () => api.go('tournamentTable'), 'btn-primary')],
    };
  }
  if (info.kind === 'escort') {
    const pos = ordinalText(info.vipPosition);
    return { extras: [h('div', { class: `verdict ${info.success ? 'good' : 'bad'}`, text: info.success ? t('party.escort.won', { pos }) : t('party.escort.lost', { pos }) })], items: null };
  }
  const lines = info.swaps.map((s) => t(`party.relay.swaps.${s.swaps === 1 ? 'one' : 'other'}`, { name: s.name, n: s.swaps }));
  return { extras: [h('p', { class: 'team-line' }, icon('timer'), h('span', { text: lines.join(' · ') }))], items: null };
}
