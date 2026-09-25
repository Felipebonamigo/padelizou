// Lobby: um cartão por assento (grade 2×2), cada um navegado pelo próprio dispositivo; o
// Jogador 1 configura a corrida no painel da direita. Entrar/sair de assento vem do
// InputProvider (`joinPressed`/`leavePressed`) no `update()`; navegação vem do teclado (DOM)
// e do gamepad (`navigate`), sempre com o `device` que apertou.
import { SEAT_COLORS } from '../../core/data/drivers';
import type { HumanEntry } from '../../core/types';
import type { DeviceId, MenuNav } from '../../game/contracts';
import { NAME_MAX_LENGTH } from '../../game/save';
import { t } from '../../i18n';
import { isKeyboard } from '../input';
import { arrowButton, button, carCard, createFocusList, h, listNav, screenFrame, selector, type FocusItem, type FocusList, type LobbySeat, type LobbyState, type ScreenApi, type ScreenInstance } from './common';
import { icon } from './icons';
import { commitSettings, raceOptionSelectors } from './options';

export const LOBBY_SEATS = 4;

export function maxSeats(lobby: LobbyState): number {
  return lobby.mode === 'timetrial' ? 1 : LOBBY_SEATS;
}

export function occupiedSeats(lobby: LobbyState): LobbySeat[] {
  return lobby.seats.filter((s): s is LobbySeat => s !== null).sort((a, b) => a.seat - b.seat);
}

export function seatOfDevice(lobby: LobbyState, device: DeviceId): number {
  return lobby.seats.findIndex((s) => s !== null && s.device === device);
}

export function canStart(lobby: LobbyState): boolean {
  const seats = occupiedSeats(lobby);
  return lobby.seats[0] !== null && seats.length > 0 && seats.every((s) => s.ready);
}

/** Humanos da corrida a partir do lobby: co-op = todos no time 0; versus = time = assento. */
export function lobbyHumans(api: ScreenApi): HumanEntry[] {
  const { cars } = api.ctx;
  return occupiedSeats(api.lobby).map((s) => ({
    seat: s.seat,
    name: s.name.trim() || `P${s.seat + 1}`,
    carId: (cars[s.carIndex] ?? cars[0]).id,
    teamId: api.lobby.versus ? s.seat : 0,
    color: SEAT_COLORS[s.seat] ?? '#ffffff',
  }));
}

function newSeat(api: ScreenApi, seat: number, device: DeviceId): LobbySeat {
  const { save, cars } = api.ctx;
  const carIndex = Math.max(0, cars.findIndex((c) => c.id === save.seatCars[seat]));
  return { seat, device, name: save.seatNames[seat] ?? `P${seat + 1}`, carIndex, ready: false, cursor: 1 };
}

/** Alinha o lobby com os assentos do InputProvider (quem já estava ligado continua no lugar). */
function syncWithInput(api: ScreenApi): void {
  const { lobby } = api;
  const limit = maxSeats(lobby);
  for (let seat = 0; seat < LOBBY_SEATS; seat++) {
    const device = api.ctx.input.seatDevice(seat);
    if (seat >= limit) {
      if (device) api.ctx.input.unbindSeat(seat);
      lobby.seats[seat] = null;
      continue;
    }
    const existing = lobby.seats[seat];
    if (!device) lobby.seats[seat] = null;
    else if (!existing || existing.device !== device) lobby.seats[seat] = newSeat(api, seat, device);
  }
  if (lobby.mode === 'timetrial') lobby.versus = false;
}

export function lobbyScreen(api: ScreenApi): ScreenInstance {
  const { ctx, lobby } = api;
  const { cars, input } = ctx;
  syncWithInput(api);

  const body = h('div', { class: 'lobby-body' });
  const el = screenFrame('lobby', null,
    h('div', { class: 'lobby-head' },
      h('h1', { class: 'screen-title', text: t('ui.lobby.title') }),
      h('span', { class: 'chip', text: t(`ui.main.${lobby.mode}`) }),
      h('span', { class: 'lobby-help', text: t('ui.lobby.joinHint') }),
    ),
    body,
  );
  let lists: Array<FocusList | null> = [null, null, null, null];

  const saveCursors = () => {
    lobby.seats.forEach((s, i) => { const l = lists[i]; if (s && l && l.index >= 0) s.cursor = l.index; });
  };

  const deviceLabel = (device: DeviceId) => input.devices().find((d) => d.id === device)?.label ?? device;

  const join = (seat: number, device: DeviceId) => {
    input.bindSeat(seat, device);
    lobby.seats[seat] = newSeat(api, seat, device);
    api.sfx('confirm');
    render();
  };

  const leave = (seat: number) => {
    input.unbindSeat(seat);
    lobby.seats[seat] = null;
    api.sfx('back');
    render();
  };

  const toggleReady = (s: LobbySeat) => {
    s.ready = !s.ready;
    api.sfx(s.ready ? 'confirm' : 'back');
    render();
  };

  const start = () => {
    if (!canStart(lobby)) { api.sfx('back'); return; }
    api.sfx('confirm');
    api.go(lobby.mode === 'cup' ? 'cups' : 'tracks');
  };

  function occupiedSlot(s: LobbySeat): { el: HTMLElement; items: FocusItem[] } {
    const color = SEAT_COLORS[s.seat];
    const nameInput = h('input', {
      class: 'name-input',
      attrs: { type: 'text', maxlength: String(NAME_MAX_LENGTH), value: s.name, spellcheck: 'false', autocomplete: 'off' },
      on: { input: () => { s.name = nameInput.value; } },
    });
    const nameRow: FocusItem = {
      el: h('div', { class: 'sel sel-name' }, h('span', { class: 'sel-label', text: t('ui.lobby.name') }), nameInput),
      activate: () => { nameInput.focus(); nameInput.select(); },
    };
    const heroBody = h('div', { class: 'car-hero-body' }, carCard(cars[s.carIndex] ?? cars[0], cars));
    const changeCar = (dir: -1 | 1) => {
      if (s.ready) return;
      s.carIndex = (s.carIndex + dir + cars.length) % cars.length;
      heroBody.replaceChildren(carCard(cars[s.carIndex], cars));
    };
    const carItem: FocusItem = {
      el: h('div', { class: `car-hero${s.ready ? ' locked' : ''}` },
        arrowButton(-1, () => { changeCar(-1); api.sfx('move'); }),
        heroBody,
        arrowButton(1, () => { changeCar(1); api.sfx('move'); }),
      ),
      adjust: changeCar,
      activate: () => toggleReady(s),
    };
    const ready = button(t('ui.lobby.ready'), () => toggleReady(s), s.ready ? 'btn-ready on' : 'btn-ready');
    if (s.ready) ready.el.prepend(icon('check'));
    const team = lobby.versus ? t('ui.lobby.teamN', { n: s.seat + 1 }) : t('core.team.human');
    const slot = h('div', { class: `slot occupied glass${s.ready ? ' ready' : ''}`, style: `--seat:${color}` },
      h('div', { class: 'slot-head' },
        h('span', { class: 'seat-badge', text: `P${s.seat + 1}` }),
        h('span', { class: 'slot-device' }, icon(isKeyboard(s.device) ? 'keyboard' : 'gamepad'), h('span', { text: deviceLabel(s.device) })),
        s.ready ? h('span', { class: 'slot-state on' }, icon('check'), t('ui.lobby.ready')) : null,
      ),
      nameRow.el,
      carItem.el,
      h('div', { class: 'slot-foot' },
        h('span', { class: 'slot-team' }, icon('users'), h('span', { text: team })),
        ready.el,
      ),
    );
    return { el: slot, items: [nameRow, carItem, ready] };
  }

  function emptySlot(seat: number): HTMLElement {
    const slot = h('div', { class: 'slot empty glass', style: `--seat:${SEAT_COLORS[seat]}` },
      h('span', { class: 'seat-badge', text: `P${seat + 1}` }),
      h('span', { class: 'slot-empty-icons' }, icon('keyboard'), icon('gamepad')),
      h('p', { class: 'slot-join', text: t('ui.lobby.join') }),
    );
    slot.addEventListener('click', () => {
      // Com o mouse: entra com o primeiro teclado livre.
      const free = (['kb1', 'kb2'] as const).find((id) => seatOfDevice(lobby, id) < 0);
      if (free && lobby.seats[seat] === null) join(seat, free);
    });
    return slot;
  }

  function panel(): { el: HTMLElement; items: FocusItem[] } {
    const tt = lobby.mode === 'timetrial';
    const items: FocusItem[] = [];
    if (!tt) {
      items.push(selector(t('ui.lobby.mode'), () => (lobby.versus ? t('ui.lobby.versus') : t('ui.lobby.coop')), () => { lobby.versus = !lobby.versus; render(); }, { sfx: api.sfx }));
    }
    items.push(...raceOptionSelectors(api, () => commitSettings(api), {
      difficulty: !tt, gear: true, totalCars: !tt, quickLaps: lobby.mode === 'quick', assists: !tt && !lobby.versus, lapsLabel: t('ui.lobby.laps'),
    }));
    const startBtn = button(t('ui.lobby.start'), start, 'btn-primary btn-start');
    startBtn.disabled = !canStart(lobby);
    if (startBtn.disabled) startBtn.el.classList.add('disabled');
    const backBtn = button(t('ui.common.back'), () => { api.sfx('back'); api.back(); });
    items.push(startBtn, backBtn);
    const panelEl = h('div', { class: 'lobby-panel glass' },
      h('h2', { class: 'sub-title', text: t('ui.lobby.options') }),
      h('div', { class: 'lobby-options' }, items.slice(0, items.length - 2).map((i) => i.el)),
      h('p', { class: 'hint', text: canStart(lobby) ? t('ui.lobby.startHint') : t('ui.lobby.waitHint') }),
      h('div', { class: 'lobby-actions' }, startBtn.el, backBtn.el),
    );
    return { el: panelEl, items };
  }

  function render(): void {
    saveCursors();
    lists = [null, null, null, null];
    const limit = maxSeats(lobby);
    const slotsEl = h('div', { class: `lobby-slots seats-${limit}` });
    const slotItems: Array<FocusItem[]> = [];
    for (let seat = 0; seat < limit; seat++) {
      const s = lobby.seats[seat];
      if (s) {
        const slot = occupiedSlot(s);
        slotsEl.appendChild(slot.el);
        slotItems[seat] = slot.items;
      } else {
        slotsEl.appendChild(emptySlot(seat));
      }
    }
    const p1 = lobby.seats[0];
    let panelEl: HTMLElement;
    if (p1) {
      const pn = panel();
      panelEl = pn.el;
      slotItems[0] = [...(slotItems[0] ?? []), ...pn.items];
    } else {
      const backBtn = button(t('ui.common.back'), () => { api.sfx('back'); api.back(); });
      panelEl = h('div', { class: 'lobby-panel glass waiting' },
        h('h2', { class: 'sub-title', text: t('ui.lobby.options') }),
        h('p', { class: 'hint', text: t('ui.lobby.needP1') }),
        h('div', { class: 'lobby-actions' }, backBtn.el),
      );
      createFocusList([backBtn], { sfx: api.sfx });
    }
    for (let seat = 0; seat < limit; seat++) {
      const s = lobby.seats[seat];
      const items = slotItems[seat];
      if (s && items) lists[seat] = createFocusList(items, { start: Math.min(s.cursor, items.length - 1), sfx: api.sfx });
    }
    body.replaceChildren(slotsEl, panelEl);
  }

  render();
  // A borda que abriu esta tela (Enter no menu, Esc na seleção de pista) ainda está fechada no
  // primeiro quadro: sem esta carência, o Esc que volta da seleção tiraria o P1 do assento.
  let armed = false;

  return {
    el,
    nav(nav: MenuNav) {
      const device = nav.device;
      if (!device) return;
      const seat = seatOfDevice(lobby, device);
      if (seat < 0) {
        // Sem assento: só sai do lobby quando ele está vazio (entrar é via joinPressed).
        if (nav.back && occupiedSeats(lobby).length === 0) { api.sfx('back'); api.back(); }
        return;
      }
      if (nav.start) { start(); return; }
      const list = lists[seat];
      const s = lobby.seats[seat];
      if (!list || !s) return;
      // Voltar de quem tem assento é sair do assento, e isso vem de leavePressed() no update().
      listNav(list, { ...nav, back: false }, api.sfx);
      s.cursor = list.index;
    },
    update() {
      if (!armed) { armed = true; return; }
      const joining = input.joinPressed();
      if (joining) {
        const limit = maxSeats(lobby);
        const free = lobby.seats.findIndex((s, i) => s === null && i < limit);
        if (free >= 0) join(free, joining);
      }
      const leaving = input.leavePressed();
      if (leaving) {
        const seat = seatOfDevice(lobby, leaving);
        if (seat >= 0) leave(seat);
      }
    },
  };
}
