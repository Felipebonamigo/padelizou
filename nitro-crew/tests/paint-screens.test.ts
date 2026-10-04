// As telas de escolha guardando a pintura: no lobby (corrida rápida/co-op/festa) e na garagem da carreira, o
// jogador desce até "Pintura", anda pela paleta com ←→ (teclado ou controle: a mesma navegação do menu), a
// prévia do carro troca de cor, a escolha vai para o save (por assento e carro) e volta na próxima vez.
// DOM falso mínimo (o que `h`, a lista de foco e as telas usam); o resto é o código de verdade.
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { newCareer } from '../src/core/career';
import { CARS } from '../src/core/data/cars';
import { DEFAULT_SETTINGS, type DeviceId, type InputProvider, type MenuNav, type SaveData, type Settings } from '../src/game/contracts';
import { ORIGINAL_PAINT, PAINT_IDS, paintColors, seatPaint, setSeatPaint } from '../src/game/paints';
import { sanitizeSave } from '../src/game/save';
import '../src/ui/strings';
import type { LobbyState, ScreenApi, ScreenInstance } from '../src/ui/screens/common';
import { garageScreen } from '../src/ui/screens/garage';
import { lobbyScreen } from '../src/ui/screens/lobby';
import { human } from './helpers';

class FakeEl {
  children: unknown[] = [];
  className = '';
  textContent = '';
  title = '';
  innerHTML = '';
  value = '';
  disabled = false;
  style = { setProperty() {} };
  attrs: Record<string, string> = {};
  listeners: Record<string, Array<(ev: unknown) => void>> = {};
  private readonly classes = new Set<string>();
  classList = {
    add: (...c: string[]) => { for (const x of c) this.classes.add(x); },
    remove: (...c: string[]) => { for (const x of c) this.classes.delete(x); },
    toggle: (c: string, force?: boolean) => { const on = force ?? !this.classes.has(c); if (on) this.classes.add(c); else this.classes.delete(c); return on; },
    contains: (c: string) => this.classes.has(c),
  };
  constructor(readonly tag: string) {}
  /** `<template>` de icons.ts: o SVG sai com a marcação que recebeu (para conferir as cores do desenho). */
  get content(): { firstElementChild: FakeEl } {
    const svg = new FakeEl('svg');
    svg.innerHTML = this.innerHTML;
    return { firstElementChild: svg };
  }
  setAttribute(k: string, v: string): void { this.attrs[k] = v; }
  getAttribute(k: string): string | null { return this.attrs[k] ?? null; }
  appendChild<T>(c: T): T { this.children.push(c); return c; }
  append(...c: unknown[]): void { this.children.push(...c); }
  prepend(...c: unknown[]): void { this.children.unshift(...c); }
  replaceChildren(...c: unknown[]): void { this.children = [...c]; }
  addEventListener(type: string, fn: (ev: unknown) => void): void { (this.listeners[type] ??= []).push(fn); }
  removeEventListener(): void { /* não usado */ }
  focus(): void {}
  select(): void {}
  blur(): void {}
  closest(): null { return null; }
}

function all(root: unknown, pred: (el: FakeEl) => boolean, out: FakeEl[] = []): FakeEl[] {
  if (!(root instanceof FakeEl)) return out;
  if (pred(root)) out.push(root);
  for (const c of root.children) all(c, pred, out);
  return out;
}

function one(root: unknown, pred: (el: FakeEl) => boolean): FakeEl {
  const found = all(root, pred);
  expect(found).toHaveLength(1);
  return found[0];
}

const hasClass = (cls: string) => (el: FakeEl) => el.className.split(/\s+/).includes(cls);
const text = (el: unknown): string => (el instanceof FakeEl ? el.textContent + el.children.map(text).join('') : typeof el === 'object' && el && 'text' in el ? String((el as { text: string }).text) : '');

function fakeInput(bound: Array<DeviceId | null>): InputProvider {
  const nav: MenuNav = { up: false, down: false, left: false, right: false, confirm: false, back: false, start: false, device: null };
  return {
    poll() {}, readSeat: () => ({ steer: 0, throttle: false, brake: false, nitro: false, gearUp: false, gearDown: false }),
    bindSeat(seat, device) { bound[seat] = device; }, unbindSeat(seat) { bound[seat] = null; }, seatDevice: (s) => bound[s] ?? null,
    devices: () => [], joinPressed: () => null, leavePressed: () => null, menuNav: () => nav, pausePressed: () => -1, dispose() {},
    peek: () => null, rumble() {},
  };
}

function api(save: SaveData, bound: Array<DeviceId | null>, lobby: LobbyState): ScreenApi {
  const settings: Settings = { ...DEFAULT_SETTINGS, seatAssists: [...DEFAULT_SETTINGS.seatAssists] };
  return {
    ctx: { cars: CARS, save, settings, input: fakeInput(bound), cups: [], tracks: [] },
    lobby,
    go: () => undefined, back: () => undefined, emit: () => undefined, sfx: () => undefined, refresh: () => undefined,
  } as unknown as ScreenApi;
}

const NAV: MenuNav = { up: false, down: false, left: false, right: false, confirm: false, back: false, start: false, device: null };
function press(screen: ScreenInstance, device: DeviceId, key: 'up' | 'down' | 'left' | 'right' | 'confirm'): void {
  screen.nav({ ...NAV, [key]: true, device });
}

beforeEach(() => {
  vi.stubGlobal('document', {
    activeElement: null,
    createElement: (tag: string) => new FakeEl(tag),
    createElementNS: (_ns: string, tag: string) => new FakeEl(tag),
    createTextNode: (t: string) => ({ text: t }),
  });
});
afterEach(() => vi.unstubAllGlobals());

describe('lobby: cada assento escolhe a pintura do carro', () => {
  const lobbyState = (): LobbyState => ({ mode: 'quick', versus: false, seats: [null, null, null, null] });

  it('↓ até "Pintura", → troca: a prévia muda de cor, o save guarda para o assento e o carro, e volta na próxima vez', () => {
    const save = sanitizeSave({});
    const bound: Array<DeviceId | null> = ['kb1', 'gp0', null, null];
    const screen = lobbyScreen(api(save, bound, lobbyState()));
    const slots = () => all(screen.el, hasClass('slot')).filter(hasClass('occupied'));
    expect(slots()).toHaveLength(2);
    const paintRow = (slot: FakeEl) => one(slot, hasClass('sel-paint'));
    expect(paintRow(slots()[0]).getAttribute('data-paint')).toBe(ORIGINAL_PAINT);
    expect(text(paintRow(slots()[0]))).toContain('Original');
    // O cursor do assento começa no carro: ↓ vai para a pintura e → anda uma na paleta.
    press(screen, 'kb1', 'down');
    expect(paintRow(slots()[0]).classList.contains('focus')).toBe(true);
    press(screen, 'kb1', 'right');
    const first = PAINT_IDS[1];
    expect(seatPaint(save, 0, 'falcao')).toBe(first);
    expect(seatPaint(save, 1, 'falcao')).toBe(ORIGINAL_PAINT); // o P2 (controle) não foi tocado
    expect(paintRow(slots()[0]).getAttribute('data-paint')).toBe(first);
    const card = one(slots()[0], (el) => el.getAttribute('data-car') === 'falcao' && el.getAttribute('data-paint') !== null);
    expect(card.getAttribute('data-paint')).toBe(first);
    const svg = one(card, (el) => el.tag === 'svg' && el.innerHTML.includes('<path'));
    expect(svg.innerHTML).toContain(`fill="${paintColors(first)?.color}"`);
    // O controle do P2 anda na dele, para trás.
    press(screen, 'gp0', 'down');
    press(screen, 'gp0', 'left');
    expect(seatPaint(save, 1, 'trovao')).toBe(PAINT_IDS[PAINT_IDS.length - 1]);

    // Próxima vez (outra tela sobre o mesmo save): volta com a escolha.
    const again = lobbyScreen(api(save, bound, lobbyState()));
    const slot0 = all(again.el, hasClass('slot')).filter(hasClass('occupied'))[0];
    expect(paintRow(slot0).getAttribute('data-paint')).toBe(first);
  });

  it('trocar de carro mostra a pintura guardada daquele carro; com PRONTO dado, não muda', () => {
    const save = sanitizeSave({});
    setSeatPaint(save, 0, 'trovao', 'onix');
    const screen = lobbyScreen(api(save, ['kb1', null, null, null], lobbyState()));
    const slot = () => all(screen.el, hasClass('slot')).filter(hasClass('occupied'))[0];
    press(screen, 'kb1', 'right'); // carro: Falcão → Trovão
    expect(one(slot(), hasClass('sel-paint')).getAttribute('data-paint')).toBe('onix');
    press(screen, 'kb1', 'confirm'); // PRONTO
    press(screen, 'kb1', 'down');
    press(screen, 'kb1', 'right');
    expect(seatPaint(save, 0, 'trovao')).toBe('onix');
  });
});

describe('garagem da carreira: cada piloto pinta o carro', () => {
  it('"Pintura" na linha do PRONTO (↑ do carro dá a volta: PRONTO, pintura); → troca e grava para o piloto e o carro à mostra; a prévia muda', () => {
    const save = sanitizeSave({});
    save.career = newCareer([human(0, 0, 'falcao'), human(1, 0, 'saci')]);
    const screen = garageScreen(api(save, ['kb1', 'kb2', null, null], { mode: 'career', versus: false, seats: [null, null, null, null] }));
    const panels = () => all(screen.el, hasClass('gp'));
    expect(panels()).toHaveLength(2);
    press(screen, 'kb2', 'up');
    press(screen, 'kb2', 'up');
    expect(one(panels()[1], hasClass('sel-paint')).classList.contains('focus')).toBe(true);
    press(screen, 'kb2', 'right');
    press(screen, 'kb2', 'right');
    expect(seatPaint(save, 1, 'saci')).toBe(PAINT_IDS[2]);
    expect(seatPaint(save, 0, 'falcao')).toBe(ORIGINAL_PAINT);
    const row = one(panels()[1], hasClass('sel-paint'));
    expect(row.getAttribute('data-paint')).toBe(PAINT_IDS[2]);
    const svg = one(one(panels()[1], hasClass('gp-car-visual')), (el) => el.tag === 'svg');
    expect(svg.innerHTML).toContain(`fill="${paintColors(PAINT_IDS[2])?.color}"`);
    // Confirmar na linha da pintura avança uma (não compra nada nem dá PRONTO).
    press(screen, 'kb2', 'confirm');
    expect(seatPaint(save, 1, 'saci')).toBe(PAINT_IDS[3]);
    expect(save.career?.drivers[1].garage.carId).toBe('saci');
  });
});
