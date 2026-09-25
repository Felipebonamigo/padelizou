// Tela Online: conectar (servidor, criar sala, entrar com código), lobby em rede (jogadores de
// cada computador, carro, pronto; o anfitrião escolhe pista e voltas e larga), confirmação de
// saída durante a corrida (o online não pausa), resultado e erro. Tudo vem do OnlineController
// (src/game/online-session.ts); esta tela só desenha e repassa o que o jogador faz.
// Também exporta o aviso por cima da corrida (ping/atraso, "aguardando", dessincronia).
import { CARS } from '../../core/data/cars';
import { SEAT_COLORS } from '../../core/data/drivers';
import { formatTicks } from '../../core/sim/race';
import { TRACKS } from '../../core/track';
import type { DeviceId, MenuNav } from '../../game/contracts';
import { assignSeats, seatName, type OnlineController, type OnlineHud, type OnlineStatus } from '../../game/online-session';
import { DIFFICULTIES } from '../../game/settings';
import { t } from '../../i18n';
import { MAX_INPUT_DELAY, MIN_INPUT_DELAY, normalizeServerUrl, PLAYER_NAME_MAX, ROOM_CODE_LENGTH, type RoomSettings } from '../../net/protocol';
import '../../net/strings';
import { isKeyboard } from '../input';
import { button, createFocusList, dayIcon, flagFor, h, listNav, screenFrame, selector, trackThumb, type FocusItem, type FocusList, type ScreenApi, type ScreenInstance } from './common';
import { icon, medal } from './icons';
import './online.css';

type View = 'connect' | 'connecting' | 'lobby' | 'quit' | 'results' | 'error';

function viewOf(online: OnlineController): View {
  switch (online.phase) {
    case 'connecting': return 'connecting';
    case 'lobby': return 'lobby';
    case 'racing': return 'quit';
    case 'results': return 'results';
    case 'error': return 'error';
    default: return 'connect';
  }
}

/** Cursor por vista, para uma reconstrução da tela não jogar o foco para o topo. */
const cursors: Partial<Record<View, number>> = {};
/** Código digitado (sobrevive a trocas de vista e de idioma). */
let typedCode = '';

/** "1 volta" / "3 voltas". */
function lapsText(n: number): string {
  return n === 1 ? t('online.lobby.oneLap') : t('online.lobby.lapsValue', { n });
}

function carName(id: string): string {
  return CARS.find((c) => c.id === id)?.name ?? id;
}

function textRow(label: string, input: HTMLInputElement, cls = ''): FocusItem {
  return {
    el: h('div', { class: `sel sel-name online-field ${cls}`.trim() }, h('span', { class: 'sel-label', text: label }), input),
    activate: () => { input.focus(); input.select(); },
  };
}

export function onlineScreen(api: ScreenApi): ScreenInstance {
  const online = api.ctx.online;
  const el = screenFrame('online', null);
  if (!online) {
    const back = button(t('online.err.back'), () => api.back());
    const list = createFocusList([back], { sfx: api.sfx });
    el.append(h('p', { class: 'empty', text: t('online.err.unavailable') }), back.el);
    return { el, nav: (nav) => listNav(list, nav, api.sfx, () => api.back()) };
  }
  const { ctx } = api;
  let view: View = viewOf(online);
  let list: FocusList | null = null;
  let structure = '';
  /** Atualizações no lugar (sem reconstruir: não tira o foco de quem digita). */
  let refreshers: Array<() => void> = [];
  /** Dispositivo que abriu a tela (vira o jogador 1 deste computador). */
  let opener: DeviceId = 'kb1';

  function structureKey(): string {
    const r = online!.room;
    return [view, online!.locals.length, online!.isHost, online!.ready, online!.error ?? '', r ? 'room' : '-', ctx.settings.language].join('|');
  }

  function render(): void {
    if (list && list.index >= 0) cursors[view] = list.index;
    view = viewOf(online!);
    structure = structureKey();
    refreshers = [];
    let items: FocusItem[] = [];
    let body: HTMLElement;
    switch (view) {
      case 'connect': ({ body, items } = connectView()); break;
      case 'connecting': ({ body, items } = connectingView()); break;
      case 'lobby': ({ body, items } = lobbyView()); break;
      case 'quit': ({ body, items } = quitView()); break;
      case 'results': ({ body, items } = resultsView()); break;
      default: ({ body, items } = errorView()); break;
    }
    el.dataset.view = view;
    el.replaceChildren(body);
    list = createFocusList(items, { sfx: api.sfx, start: Math.min(cursors[view] ?? 0, Math.max(0, items.length - 1)) });
    // Vidro escuro só nos menus; na saída da corrida, a pista continua visível atrás.
    el.classList.toggle('over-race', view === 'quit');
  }

  function update(): void {
    if (viewOf(online!) !== view || structureKey() !== structure) { render(); return; }
    for (const fn of refreshers) fn();
  }

  // ───────────────────────────── Conectar ─────────────────────────────

  function connectView(): { body: HTMLElement; items: FocusItem[] } {
    const serverInput = h('input', {
      class: 'name-input online-url',
      attrs: { type: 'text', value: ctx.settings.serverUrl, spellcheck: 'false', autocomplete: 'off', maxlength: '200', 'data-field': 'server' },
    });
    const note = h('p', { class: 'online-note' });
    const saveServer = (): boolean => {
      const url = normalizeServerUrl(serverInput.value);
      if (!url) { note.textContent = t('online.err.badUrl'); note.className = 'online-note bad'; return false; }
      if (url !== ctx.settings.serverUrl) { online!.setServerUrl(url); note.textContent = t('online.connect.serverSaved'); note.className = 'online-note good'; }
      serverInput.value = url;
      return true;
    };
    serverInput.addEventListener('change', () => { saveServer(); });
    serverInput.addEventListener('keydown', (e) => { if (e.code === 'Enter' || e.code === 'NumpadEnter') saveServer(); });

    const codeInput = h('input', {
      class: 'name-input online-code-input',
      attrs: { type: 'text', value: typedCode, spellcheck: 'false', autocomplete: 'off', maxlength: String(ROOM_CODE_LENGTH + 2), placeholder: 'ABCDE', 'data-field': 'code' },
      on: { input: () => { codeInput.value = codeInput.value.toUpperCase(); typedCode = codeInput.value; } },
    });
    const create = () => { if (saveServer()) online!.create(opener); };
    const join = () => { if (saveServer()) online!.join(codeInput.value, opener); };
    codeInput.addEventListener('keydown', (e) => { if (e.code === 'Enter' || e.code === 'NumpadEnter') { e.preventDefault(); codeInput.blur(); join(); api.sfx('confirm'); } });

    const serverRow = textRow(t('online.connect.server'), serverInput, 'online-server-row');
    const createBtn = button(t('online.connect.create'), create, 'btn-primary btn-online-create');
    const codeRow = textRow(t('online.connect.code'), codeInput, 'online-code-row');
    const joinBtn = button(t('online.connect.join'), join, 'btn-online-join');
    const backBtn = button(t('ui.common.back'), () => api.back());
    const items = [createBtn, codeRow, joinBtn, serverRow, backBtn];
    const body = h('div', { class: 'online-connect glass' },
      h('h1', { class: 'screen-title', text: t('online.title') }),
      h('p', { class: 'online-intro', text: t('online.intro') }),
      h('div', { class: 'online-connect-grid' },
        h('div', { class: 'online-card' },
          h('div', { class: 'online-card-head' }, h('div', { class: 'online-card-icon' }, icon('flag')), h('p', { class: 'online-card-text', text: t('online.connect.createHint') })),
          createBtn.el),
        h('div', { class: 'online-card' },
          h('div', { class: 'online-card-head' }, h('div', { class: 'online-card-icon' }, icon('users')), h('p', { class: 'online-card-text', text: t('online.connect.joinHint') })),
          codeRow.el, joinBtn.el),
      ),
      serverRow.el,
      note,
      h('p', { class: 'hint', text: t('online.connect.typeHint') }),
      h('div', { class: 'actions' }, backBtn.el),
    );
    return { body, items };
  }

  function connectingView(): { body: HTMLElement; items: FocusItem[] } {
    const cancel = button(t('online.connect.cancel'), () => online!.leave(false));
    const body = h('div', { class: 'online-panel glass' },
      h('h1', { class: 'screen-title', text: t('online.title') }),
      h('p', { class: 'online-status pulse', text: t('online.connect.connecting', { url: ctx.settings.serverUrl }) }),
      h('div', { class: 'actions' }, cancel.el),
    );
    return { body, items: [cancel] };
  }

  // ───────────────────────────── Lobby ─────────────────────────────

  function roomSettings(): RoomSettings | null {
    return online!.room?.settings ?? null;
  }

  function lobbyView(): { body: HTMLElement; items: FocusItem[] } {
    const items: FocusItem[] = [];
    const locked = online!.ready;

    // Jogadores deste computador.
    const localEls = online!.locals.map((p, i) => {
      const nameInput = h('input', {
        class: 'name-input',
        attrs: { type: 'text', maxlength: String(PLAYER_NAME_MAX), value: p.name, spellcheck: 'false', autocomplete: 'off', 'data-field': `name${i}` },
        on: { input: () => online!.setName(i, nameInput.value) },
      });
      if (locked) nameInput.disabled = true;
      const nameRow = textRow(t('online.lobby.name'), nameInput);
      const carSel = selector(t('online.lobby.car'), () => carName(online!.locals[i]?.car ?? ''), (d) => online!.cycleCar(i, d), { sfx: api.sfx, cls: locked ? 'locked' : '' });
      items.push(nameRow, carSel);
      refreshers.push(() => carSel.refresh());
      const badge = h('span', { class: 'seat-badge' });
      const box = h('div', { class: 'online-local' },
        h('div', { class: 'online-local-head' },
          badge,
          h('span', { class: 'slot-device' }, icon(isKeyboard(p.device) ? 'keyboard' : 'gamepad'), h('span', { text: deviceLabel(p.device) })),
        ),
        nameRow.el, carSel.el,
      );
      // O assento (e a cor) segue a ordem da sala: muda se alguém de id menor sai.
      refreshers.push(() => {
        const seat = localSeatPreview(i);
        badge.textContent = `P${seat + 1}`;
        box.style.setProperty('--seat', SEAT_COLORS[seat] ?? '#fff');
        // Nome padrão acompanha o assento (fora de quando o jogador está digitando).
        const name = online!.locals[i]?.name ?? '';
        if (document.activeElement !== nameInput && seatName(name, seat) !== nameInput.value && seatName(name, seat) !== name) nameInput.value = seatName(name, seat);
      });
      return box;
    });
    let removeBtn: FocusItem | null = null;
    if (online!.locals.length > 1 && !locked) {
      removeBtn = button(t('online.lobby.removeLocal'), () => online!.removeLocal(1), 'btn-small');
      items.push(removeBtn);
    }

    // Opções da corrida: o anfitrião muda; os outros só leem.
    const s = () => roomSettings();
    const change = (patch: Partial<RoomSettings>) => online!.updateRoomSettings(patch);
    const trackIndex = () => Math.max(0, TRACKS.findIndex((x) => x.id === s()?.trackId));
    const settingSels = [
      selector(t('online.lobby.track'), () => TRACKS[trackIndex()]?.name ?? '—', (d) => change({ trackId: TRACKS[(trackIndex() + d + TRACKS.length) % TRACKS.length].id }), { sfx: api.sfx }),
      selector(t('online.lobby.laps'), () => String(s()?.laps ?? '—'), (d) => change({ laps: (s()?.laps ?? 3) + d }), { sfx: api.sfx }),
      selector(t('online.lobby.mode'), () => (s()?.versus ? t('online.lobby.versus') : t('online.lobby.coop')), () => change({ versus: !s()?.versus }), { sfx: api.sfx }),
      selector(t('online.lobby.difficulty'), () => (s() ? t(`core.difficulty.${s()?.difficulty}`) : '—'), (d) => {
        const cur = s()?.difficulty ?? 'profissional';
        change({ difficulty: DIFFICULTIES[(DIFFICULTIES.indexOf(cur) + d + DIFFICULTIES.length) % DIFFICULTIES.length] });
      }, { sfx: api.sfx }),
      selector(t('online.lobby.cars'), () => String(s()?.totalCars ?? '—'), (d) => change({ totalCars: Math.max(8, (s()?.totalCars ?? 20) + d) }), { sfx: api.sfx }),
      selector(t('online.lobby.delay'), () => { const n = s()?.delay ?? 3; return t('online.lobby.delayValue', { n, ms: Math.round((n * 1000) / 60) }); },
        (d) => change({ delay: Math.max(MIN_INPUT_DELAY, Math.min(MAX_INPUT_DELAY, (s()?.delay ?? 3) + d)) }), { sfx: api.sfx }),
    ];
    for (const sel of settingSels) {
      refreshers.push(() => sel.refresh());
      if (!online!.isHost) { sel.el.classList.add('locked', 'readonly'); sel.adjust = undefined; sel.activate = undefined; }
    }
    if (online!.isHost) items.push(...settingSels);

    const trackBox = h('div', { class: 'online-track' });
    const renderTrack = () => {
      const def = TRACKS[trackIndex()];
      if (!def) { trackBox.replaceChildren(); return; }
      trackBox.replaceChildren(
        trackThumb(ctx, def, 92),
        h('div', { class: 'online-track-info' },
          h('div', { class: 'online-track-name' }, h('span', { text: `${flagFor(ctx, def.country)} ${def.name}` })),
          h('div', { class: 'online-track-meta' }, dayIcon(def.timeOfDay), h('span', { text: lapsText(s()?.laps ?? def.laps) })),
        ),
      );
    };
    let lastTrack = '';
    refreshers.push(() => { const k = `${s()?.trackId}:${s()?.laps}`; if (k !== lastTrack) { lastTrack = k; renderTrack(); } });

    // Pronto (convidado) ou Largar (anfitrião), e sair.
    const hint = h('p', { class: 'hint online-lobby-hint' });
    let mainBtn: FocusItem;
    if (online!.isHost) {
      mainBtn = button(t('online.lobby.start'), () => { if (!online!.startRace()) api.sfx('back'); }, 'btn-primary btn-start btn-online-start');
      refreshers.push(() => {
        const blocker = online!.startBlocker();
        mainBtn.el.classList.toggle('disabled', blocker !== null);
        hint.textContent = blocker ? t(blocker) : t('online.lobby.canStart');
      });
    } else {
      mainBtn = button(t('online.lobby.readyBtn'), () => online!.toggleReady(), `btn-ready btn-online-ready${online!.ready ? ' on' : ''}`);
      if (online!.ready) mainBtn.el.prepend(icon('check'));
      refreshers.push(() => { hint.textContent = online!.ready ? t('online.lobby.guestReady') : t('online.lobby.guestHint'); });
    }
    const leaveBtn = button(t('online.lobby.leave'), () => online!.leave(), 'btn-danger btn-online-leave');
    items.push(mainBtn, leaveBtn);

    // Lista da sala (quem está em cada computador).
    const roster = h('div', { class: 'online-roster' });
    const count = h('span', { class: 'chip' });
    refreshers.push(() => { renderRoster(roster); count.textContent = t('online.lobby.count', { n: online!.roomSeats() }); });

    const pingEl = h('span', { class: 'online-ping mono' });
    refreshers.push(() => { pingEl.textContent = pingText(online!.status()); });

    const errorEl = online!.error ? h('p', { class: 'online-note bad', text: t(online!.error) }) : null;

    const body = h('div', { class: 'online-lobby' },
      h('div', { class: 'lobby-head online-head' },
        h('h1', { class: 'screen-title', text: t('online.title') }),
        h('span', { class: 'online-code-chip' }, h('span', { class: 'online-code-label', text: t('online.lobby.room') }), h('span', { class: 'online-code mono', text: online!.code })),
        pingEl,
      ),
      h('div', { class: 'online-lobby-body' },
        h('div', { class: 'online-col glass' },
          h('h2', { class: 'sub-title', text: t('online.lobby.here') }),
          localEls,
          removeBtn ? h('div', { class: 'online-row-actions' }, removeBtn.el) : (online!.locals.length < 2 && !locked ? h('p', { class: 'hint online-add', text: t('online.lobby.addLocal') }) : null),
          errorEl,
          h('div', { class: 'online-main-actions' }, mainBtn.el, leaveBtn.el),
          hint,
        ),
        h('div', { class: 'online-col glass' },
          h('h2', { class: 'sub-title' }, t('online.lobby.race')),
          trackBox,
          h('div', { class: 'lobby-options online-settings' }, settingSels.map((x) => x.el)),
        ),
        h('div', { class: 'online-col glass online-room' },
          h('div', { class: 'online-bigcode' },
            h('span', { class: 'online-code-label', text: t('online.lobby.room') }),
            h('span', { class: 'online-code-big mono', text: online!.code }),
            h('span', { class: 'hint', text: t('online.lobby.codeHint') }),
          ),
          h('h2', { class: 'sub-title online-players-title' }, h('span', { text: t('online.lobby.players') }), count),
          roster,
        ),
      ),
    );
    for (const fn of refreshers) fn();
    return { body, items };
  }

  /** Assento que o jogador local `i` deve receber, pela ordem da sala (a numeração é do anfitrião na largada). */
  function localSeatPreview(i: number): number {
    const room = online!.room;
    if (!room) return i;
    const seats = assignSeats(room).filter((s) => s.client === online!.myId);
    return seats[i]?.seat ?? i;
  }

  function deviceLabel(device: DeviceId): string {
    return ctx.input.devices().find((d) => d.id === device)?.label ?? device;
  }

  function renderRoster(root: HTMLElement): void {
    const room = online!.room;
    if (!room) { root.replaceChildren(); return; }
    const seats = assignSeats(room);
    const rows = [...room.clients].sort((a, b) => a.id - b.id).map((c) => {
      const players = c.info?.players ?? [];
      const tags = [
        c.id === online!.myId ? h('span', { class: 'tag you', text: t('online.lobby.you') }) : null,
        c.id === room.host ? h('span', { class: 'tag host', text: t('online.lobby.host') }) : null,
        !c.connected ? h('span', { class: 'tag off', text: t('online.lobby.offline') }) : null,
        c.id !== room.host && c.connected ? h('span', { class: `tag ${c.info?.ready ? 'ok' : ''}`, text: c.info?.ready ? t('online.lobby.ready') : t('online.lobby.notReady') }) : null,
      ];
      return h('div', { class: `online-client${c.connected ? '' : ' offline'}${c.info?.ready || c.id === room.host ? ' ready' : ''}` },
        h('div', { class: 'online-client-tags' }, icon(c.id === room.host ? 'trophy' : 'users'), tags),
        players.map((p, i) => {
          const seat = seats.filter((s) => s.client === c.id)[i]?.seat ?? 0;
          return h('div', { class: 'online-player', style: `--seat:${SEAT_COLORS[seat] ?? '#fff'}` },
            h('span', { class: 'seat-badge', text: `P${seat + 1}` }),
            h('span', { class: 'online-player-name', text: seatName(p.name, seat) }),
            h('span', { class: 'online-player-car', text: carName(p.car) }),
          );
        }),
      );
    });
    root.replaceChildren(...rows);
  }

  // ───────────────────────────── Corrida, resultado, erro ─────────────────────────────

  function quitView(): { body: HTMLElement; items: FocusItem[] } {
    const stay = button(t('online.quit.stay'), () => online!.closeQuit(), 'btn-primary');
    const leave = button(t('online.quit.leave'), () => online!.leave(), 'btn-danger');
    const body = h('div', { class: 'pause-panel glass online-quit' },
      h('h1', { class: 'screen-title', text: t('online.quit.title') }),
      h('p', { class: 'online-quit-body', text: t('online.quit.body') }),
      h('div', { class: 'menu-list' }, stay.el, leave.el),
    );
    return { body, items: [stay, leave] };
  }

  function resultsView(): { body: HTMLElement; items: FocusItem[] } {
    const d = online!.results;
    const back = button(t('online.results.backToRoom'), () => online!.backToRoom(), 'btn-primary');
    const leave = button(t('online.results.leave'), () => online!.leave());
    const items = [back, leave];
    if (!d) return { body: h('div', { class: 'online-panel glass' }, back.el, leave.el), items };
    const colorOf = (seat: number) => d.humans.find((x) => x.seat === seat)?.color ?? null;
    const rows = [...d.results].sort((a, b) => a.position - b.position);
    const mine = new Set(online!.localSeats);
    const ai = new Set(online!.aiSeats);
    const table = h('table', { class: 'table results-table' },
      h('thead', {}, h('tr', {},
        h('th', { text: '#' }), h('th', { text: t('ui.results.name') }), h('th', { text: t('ui.results.car') }),
        h('th', { text: t('ui.results.time') }), h('th', { text: t('ui.results.bestLap') }), h('th', { class: 'num', text: t('ui.results.points') }),
      )),
      h('tbody', {}, rows.map((r) => {
        const color = r.seat >= 0 ? colorOf(r.seat) : null;
        return h('tr', { class: `${r.seat >= 0 ? 'human' : ''}${mine.has(r.seat) ? ' mine' : ''}`, style: color ? `--seat:${color}` : '' },
          h('td', { class: 'mono pos' }, medal(r.position) ?? String(r.position)),
          h('td', {}, h('span', { text: r.name }), ai.has(r.seat) ? h('span', { class: 'tag ai', text: t('online.results.ai'), attrs: { title: t('online.results.aiHint') } }) : null),
          h('td', { class: 'muted-cell', text: carName(r.carDefId) }),
          h('td', { class: 'mono', text: r.finished ? formatTicks(r.totalTicks) : t('ui.results.dnf') }),
          h('td', { class: 'mono', text: formatTicks(r.bestLapTicks) }),
          h('td', { class: 'mono num', text: String(r.points) }),
        );
      })),
    );
    const body = h('div', { class: 'online-results' },
      h('div', { class: 'screen-title-row' }, h('h1', { class: 'screen-title', text: t('online.results.title') }), h('span', { class: 'chip', text: d.trackDef.name }), h('span', { class: 'chip online-code-chip-small mono', text: online!.code })),
      h('div', { class: 'table-wrap glass' }, table),
      h('div', { class: 'actions' }, back.el, leave.el),
    );
    return { body, items };
  }

  function errorView(): { body: HTMLElement; items: FocusItem[] } {
    const back = button(t('online.err.back'), () => online!.resetError(), 'btn-primary');
    const menu = button(t('online.err.menu'), () => { online!.resetError(); api.emit({ type: 'toMain' }); });
    const body = h('div', { class: 'online-panel glass' },
      h('h1', { class: 'screen-title', text: t('online.err.title') }),
      h('p', { class: 'online-error-text', text: t(online!.error ?? 'online.err.lost') }),
      h('div', { class: 'actions' }, back.el, menu.el),
    );
    return { body, items: [back, menu] };
  }

  // ───────────────────────────── Navegação ─────────────────────────────

  function onBack(): void {
    api.sfx('back');
    switch (view) {
      case 'connect': api.back(); break;
      case 'connecting': online!.leave(false); break;
      case 'lobby': online!.leave(false); break;
      case 'quit': online!.closeQuit(); break;
      case 'results': online!.backToRoom(); break;
      default: online!.resetError(); break;
    }
  }

  const unsubscribe = online.onChange(update);
  render();

  return {
    el,
    nav(nav: MenuNav) {
      if (view === 'connect' && nav.device) opener = nav.device;
      if (view === 'lobby' && nav.device) {
        const local = online!.locals.findIndex((p) => p.device === nav.device);
        // Controle que ainda não joga aqui: o confirmar dele é "entrar" (tratado no update).
        if (local < 0 && !isKeyboard(nav.device)) return;
        if (local < 0 && nav.confirm && nav.device === 'kb2') return;
        if (local === 1 && nav.back) { api.sfx('back'); online!.removeLocal(1); return; }
      }
      if (list) listNav(list, nav, api.sfx, onBack);
    },
    update() {
      if (view !== 'lobby') return;
      const joining = ctx.input.joinPressed();
      if (joining && !online!.locals.some((p) => p.device === joining) && online!.addLocal(joining)) api.sfx('confirm');
      // Ping e contagem mudam sem evento de sala.
      for (const fn of refreshers) fn();
    },
    destroy() {
      unsubscribe();
      if (list && list.index >= 0) cursors[view] = list.index;
    },
  };
}

// ───────────────────────────── Aviso sobre a corrida ─────────────────────────────

function pingText(s: OnlineStatus): string {
  const ping = s.ping === null ? t('online.hud.pingUnknown') : t('online.hud.ping', { ms: Math.round(s.ping) });
  return `${ping} · ${t('online.hud.delay', { n: s.delayTicks, ms: s.delayMs })}`;
}

/** Ping/atraso no canto e avisos de espera, reconexão e dessincronia no meio da tela. */
export function createOnlineHud(): OnlineHud {
  const chip = h('div', { class: 'nc-online-chip mono' });
  const banner = h('div', { class: 'nc-online-banner' });
  const warn = h('div', { class: 'nc-online-warn' });
  const root = h('div', { class: 'nc-online-hud' }, chip, banner, warn);
  document.body.appendChild(root);
  let last = { chip: '', banner: '', warn: '', color: '' };
  return {
    update(s) {
      const chipText = pingText(s);
      let bannerText = '';
      let color = '';
      if (s.reconnecting !== null) bannerText = t('online.hud.reconnecting', { s: s.reconnecting });
      else if (s.syncing) bannerText = t('online.hud.syncing');
      else if (s.waiting.length > 0) {
        const w = s.waiting[0];
        bannerText = t(w.lost ? 'online.hud.waitingLost' : 'online.hud.waiting', { name: w.name });
        color = w.color;
      }
      const warnText = s.desync ? t('online.hud.desync', { tick: s.desync.tick }) : '';
      if (chipText !== last.chip) chip.textContent = chipText;
      if (bannerText !== last.banner || color !== last.color) {
        banner.textContent = bannerText;
        banner.classList.toggle('on', bannerText !== '');
        banner.style.setProperty('--seat', color || 'var(--accent)');
      }
      if (warnText !== last.warn) { warn.textContent = warnText; warn.classList.toggle('on', warnText !== ''); }
      last = { chip: chipText, banner: bannerText, warn: warnText, color };
    },
    dispose() {
      root.remove();
    },
  };
}
