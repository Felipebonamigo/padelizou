// Fluxo sem atrito (onda K, frente K1): do menu à contagem e do resultado à próxima largada com o mínimo de
// entradas. DOM falso mínimo (o mesmo de tests/paint-screens.test.ts); o resto é o código de verdade.
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createChampionship } from '../src/core/championship';
import { CARS } from '../src/core/data/cars';
import { CUPS } from '../src/core/data/cups';
import { TRACKS, trackDef } from '../src/core/track/tracks';
import type { HumanEntry, RaceResultRow } from '../src/core/types';
import { DEFAULT_SETTINGS, type DeviceId, type InputProvider, type MenuEvent, type MenuNav, type MenuScreen, type RaceMode, type ResultsScreenData, type SaveData, type Settings } from '../src/game/contracts';
import { isCupUnlocked, markCupCompleted, sanitizeSave } from '../src/game/save';
import { setLanguage, t } from '../src/i18n';
import { keyNav } from '../src/ui/menus';
import '../src/ui/strings';
import { cupTabs } from '../src/ui/screens/select';
import { trackName, type LobbyState, type ScreenApi, type ScreenInstance } from '../src/ui/screens/common';
import { lobbyScreen } from '../src/ui/screens/lobby';
import { nextQuickTrackId, resultsScreen, standingsScreen } from '../src/ui/screens/results';
import { mainScreen } from '../src/ui/screens/simple';

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

/** O foco da lista mora no classList (createFocusList), não no className. */
const focused = (el: FakeEl) => el.classList.contains('focus');

/** Telas abertas com api.go e eventos emitidos, zerados antes de cada teste. */
const went: MenuScreen[] = [];
const events: MenuEvent[] = [];

function api(save: SaveData, bound: Array<DeviceId | null>, lobby: LobbyState = lobbyState('quick')): ScreenApi {
  const settings: Settings = { ...DEFAULT_SETTINGS, seatAssists: [...DEFAULT_SETTINGS.seatAssists] };
  return {
    ctx: { cars: CARS, save, settings, input: fakeInput(bound), cups: CUPS, tracks: TRACKS, isCupUnlocked: (id: string) => isCupUnlocked(save, id, CUPS) },
    lobby,
    go: (screen: MenuScreen) => { went.push(screen); },
    back: () => undefined,
    emit: (e: MenuEvent) => { events.push(e); },
    sfx: () => undefined,
    refresh: () => undefined,
  } as unknown as ScreenApi;
}

const lobbyState = (mode: RaceMode): LobbyState => ({ mode, versus: false, seats: [null, null, null, null] });
const NAV: MenuNav = { up: false, down: false, left: false, right: false, confirm: false, back: false, start: false, device: null };
function press(screen: ScreenInstance, device: DeviceId, key: 'up' | 'down' | 'left' | 'right' | 'confirm' | 'start'): void {
  screen.nav({ ...NAV, [key]: true, device });
}
/** Rótulos dos botões da barra de ações (resultado e classificação). */
const actions = (screen: ScreenInstance) => one(screen.el, hasClass('actions')).children.map(text);
const firstAction = (screen: ScreenInstance) => one(screen.el, hasClass('actions')).children[0] as FakeEl;

const humans: HumanEntry[] = [{ seat: 0, name: 'Ana', carId: 'trovao', teamId: 0, color: '#fff', paint: 'rubi' }];
/** 20 carros, o humano do assento 0 na posição `pos`. */
function rows(pos: number): RaceResultRow[] {
  return Array.from({ length: 20 }, (_, i) => {
    const position = i + 1;
    const me = position === pos;
    return { carId: i, seat: me ? 0 : -1, name: me ? 'Ana' : `IA ${i}`, teamId: me ? 0 : 100 + i, carDefId: 'falcao', position, finished: true, totalTicks: 9000 + i * 30, bestLapTicks: 3000 + i, points: 0 };
  });
}
function quickData(mode: RaceMode, extra: Partial<ResultsScreenData> = {}): ResultsScreenData {
  return { mode, trackDef: trackDef('copacabana'), results: rows(20), humans, champ: null, newRecords: [], ...extra };
}

beforeEach(() => {
  went.length = 0;
  events.length = 0;
  vi.stubGlobal('document', {
    activeElement: null,
    createElement: (tag: string) => new FakeEl(tag),
    createElementNS: (_ns: string, tag: string) => new FakeEl(tag),
    createTextNode: (t: string) => ({ text: t }),
  });
});
afterEach(() => { vi.unstubAllGlobals(); setLanguage('pt'); });

describe('lobby: PRONTO, INICIAR e Start', () => {
  const startBtn = (screen: ScreenInstance) => one(screen.el, hasClass('btn-start'));

  it('Enter no carro depois do PRONTO não desfaz o PRONTO, e o foco do P1 vai para INICIAR', () => {
    const lobby = lobbyState('quick');
    const screen = lobbyScreen(api(sanitizeSave({}), ['kb1', 'gp0', null, null], lobby));
    // P2 (controle): o foco dele não pula; confirmar duas vezes no carro deixa PRONTO.
    press(screen, 'gp0', 'confirm');
    press(screen, 'gp0', 'confirm');
    expect(lobby.seats[1]?.ready).toBe(true);
    // P1: o 1º confirmar dá PRONTO e leva o foco a INICIAR; o 2º larga.
    press(screen, 'kb1', 'confirm');
    expect(lobby.seats[0]?.ready).toBe(true);
    expect(focused(startBtn(screen))).toBe(true);
    press(screen, 'kb1', 'confirm');
    expect(lobby.seats[0]?.ready).toBe(true);
    expect(went).toEqual(['tracks']);
  });

  it('pintura e direção só dão PRONTO, não tiram', () => {
    const lobby = lobbyState('quick');
    const screen = lobbyScreen(api(sanitizeSave({}), ['kb1', 'gp0', null, null], lobby));
    press(screen, 'gp0', 'down'); // carro → Pintura
    press(screen, 'gp0', 'confirm');
    press(screen, 'gp0', 'confirm');
    expect(lobby.seats[1]?.ready).toBe(true);
    const lobby2 = lobbyState('quick');
    const again = lobbyScreen(api(sanitizeSave({}), ['kb1', 'gp0', null, null], lobby2));
    press(again, 'gp0', 'down');
    press(again, 'gp0', 'down'); // carro → Pintura → Direção
    press(again, 'gp0', 'confirm');
    press(again, 'gp0', 'confirm');
    expect(lobby2.seats[1]?.ready).toBe(true);
  });

  it('a dica de largar fala do dispositivo do P1', () => {
    const hint = (screen: ScreenInstance) => text(one(one(screen.el, hasClass('lobby-panel')), hasClass('hint')));
    const kb = lobbyScreen(api(sanitizeSave({}), ['kb1', null, null, null]));
    press(kb, 'kb1', 'confirm');
    expect(hint(kb)).toBe(t('ui.lobby.startHint.kb'));
    expect(hint(kb)).not.toContain('Start');
    const pad = lobbyScreen(api(sanitizeSave({}), ['gp0', null, null, null]));
    press(pad, 'gp0', 'confirm');
    expect(hint(pad)).toBe(t('ui.lobby.startHint.pad'));
  });

  it('Start de quem não está pronto dá PRONTO e larga quando todos estão prontos', () => {
    const lobby = lobbyState('quick');
    const screen = lobbyScreen(api(sanitizeSave({}), ['gp0', null, null, null], lobby));
    press(screen, 'gp0', 'start');
    expect(lobby.seats[0]?.ready).toBe(true);
    expect(went).toEqual(['tracks']);
  });
});

describe('lobby com 1 assento', () => {
  const labels = (screen: ScreenInstance) => all(one(screen.el, hasClass('lobby-options')), hasClass('sel-label')).map(text);

  it('com 1 assento somem o Modo e as 4 assistências de co-op; com 2 voltam', () => {
    expect(labels(lobbyScreen(api(sanitizeSave({}), ['kb1', null, null, null], lobbyState('quick')))))
      .toEqual(['Dificuldade', 'Câmbio', 'Carros na pista', 'Voltas']);
    expect(labels(lobbyScreen(api(sanitizeSave({}), ['kb1', null, null, null], lobbyState('cup')))))
      .toEqual(['Dificuldade', 'Câmbio', 'Carros na pista']);
    const two = labels(lobbyScreen(api(sanitizeSave({}), ['kb1', 'gp0', null, null], lobbyState('quick'))));
    expect(two).toContain('Modo');
    expect(two).toContain('Cofre de nitro');
  });
});

describe('keyNav', () => {
  it('keyNav: Enter segurado vira start; setas seguradas repetem; tecla sem uso é null', () => {
    expect(keyNav('Enter', true)).toMatchObject({ start: true, confirm: false, device: 'kb1' });
    expect(keyNav('Enter', false)).toMatchObject({ start: false, confirm: true, device: 'kb1' });
    expect(keyNav('KeyF', true)).toMatchObject({ start: true, confirm: false, device: 'kb2' });
    expect(keyNav('ArrowDown', true)).toMatchObject({ down: true, start: false, device: 'kb1' });
    expect(keyNav('KeyZ', false)).toBeNull();
  });
});

describe('menu principal', () => {
  it('Enter ou A num modo do menu já senta o P1 com esse dispositivo', () => {
    const bound: Array<DeviceId | null> = [null, null, null, null];
    press(mainScreen(api(sanitizeSave({}), bound)), 'gp0', 'confirm'); // 1º item: Campeonato
    expect(went).toEqual(['lobby']);
    expect(bound[0]).toBe('gp0');
    went.length = 0;
    const bound2: Array<DeviceId | null> = [null, null, null, null];
    press(mainScreen(api(sanitizeSave({}), bound2)), 'kb1', 'confirm');
    expect(went).toEqual(['lobby']);
    expect(bound2[0]).toBe('kb1');
  });

  it('o cartão "Primeira vez?" entra na lista de foco sem roubar o foco inicial', () => {
    const screen = mainScreen(api(sanitizeSave({}), [null, null, null, null]));
    const first = one(screen.el, hasClass('menu-list')).children[0] as FakeEl;
    const card = one(screen.el, hasClass('tut-offer'));
    expect(focused(first)).toBe(true);
    expect(focused(card)).toBe(false);
    press(screen, 'kb1', 'up');
    expect(focused(card)).toBe(true);
    press(screen, 'kb1', 'confirm');
    expect(went).toEqual(['tutorial']);
    // → também chega ao cartão; ← volta ao 1º item.
    const again = mainScreen(api(sanitizeSave({}), [null, null, null, null]));
    press(again, 'kb1', 'right');
    expect(focused(one(again.el, hasClass('tut-offer')))).toBe(true);
    press(again, 'kb1', 'left');
    expect(focused(one(again.el, hasClass('menu-list')).children[0] as FakeEl)).toBe(true);
    // Quem já correu não vê o cartão: ↑ vai para o último item do menu.
    went.length = 0;
    const veteran = mainScreen(api(sanitizeSave({ racesRun: 1 }), [null, null, null, null]));
    expect(all(veteran.el, hasClass('tut-offer'))).toHaveLength(0);
    press(veteran, 'kb1', 'up');
    press(veteran, 'kb1', 'confirm');
    expect(went).toEqual(['credits']);
  });
});

describe('resultado e classificação: a próxima largada em foco', () => {
  it('eliminado: "RECOMEÇAR A COPA" em foco emite startCup da mesma copa com os mesmos humanos', () => {
    const champ = createChampionship('br_rj', humans);
    champ.eliminated = true;
    const screen = standingsScreen(api(sanitizeSave({}), ['kb1', null, null, null]), { champ, humans, cup: CUPS[0] });
    expect(actions(screen)).toEqual(['RECOMEÇAR A COPA', 'MENU']);
    expect(hasClass('btn-primary')(firstAction(screen))).toBe(true);
    expect(focused(firstAction(screen))).toBe(true);
    press(screen, 'kb1', 'confirm');
    expect(events).toEqual([{ type: 'startCup', cupId: 'br_rj', humans }]);
  });

  it('campeão: "PRÓXIMA COPA" emite startCup da copa que esta abriu; na última, só MENU', () => {
    const save = sanitizeSave({});
    markCupCompleted(save, 'br_rj');
    const champ = createChampionship('br_rj', humans);
    champ.completed = true;
    const screen = standingsScreen(api(save, ['kb1', null, null, null]), { champ, humans, cup: CUPS[0] });
    expect(actions(screen)).toEqual(['PRÓXIMA COPA', 'MENU']);
    press(screen, 'kb1', 'confirm');
    expect(events).toEqual([{ type: 'startCup', cupId: 'br_sp', humans }]);
    const last = CUPS.find((c) => c.id === 'mediterraneo');
    if (!last) throw new Error('mediterraneo');
    markCupCompleted(save, 'mediterraneo');
    const top = createChampionship('mediterraneo', humans);
    top.completed = true;
    expect(actions(standingsScreen(api(save, ['kb1', null, null, null]), { champ: top, humans, cup: last }))).toEqual(['MENU']);
  });

  it('corrida rápida: "MAIS UMA?" em foco, com a próxima pista na tela', () => {
    const d = quickData('quick');
    const screen = resultsScreen(api(sanitizeSave({}), ['kb1', null, null, null]), d);
    expect(actions(screen)).toEqual(['MAIS UMA?', 'TENTAR DE NOVO', 'MENU']);
    expect(focused(firstAction(screen))).toBe(true);
    expect(text(one(screen.el, hasClass('status-line')))).toContain(trackName('paraty'));
    press(screen, 'kb1', 'confirm');
    expect(events).toEqual([{ type: 'startQuick', trackId: 'paraty', laps: DEFAULT_SETTINGS.quickLaps, humans: d.humans }]);
  });

  it('nextQuickTrackId segue a ordem da tela de pistas e dá a volta', () => {
    const order = cupTabs(CUPS).flatMap((tab) => tab.cups).flatMap((c) => c.trackIds);
    expect(nextQuickTrackId(CUPS, 'copacabana')).toBe('paraty');
    expect(nextQuickTrackId(CUPS, order[order.length - 1])).toBe(order[0]);
    expect(nextQuickTrackId(CUPS, 'nao_existe')).toBe(order[0]);
  });

  it('contra-relógio e escolta mantêm TENTAR DE NOVO + MENU', () => {
    const tt = resultsScreen(api(sanitizeSave({}), ['kb1', null, null, null]), quickData('timetrial'));
    expect(actions(tt)).toEqual(['TENTAR DE NOVO', 'MENU']);
    const escort = resultsScreen(api(sanitizeSave({}), ['kb1', null, null, null]), quickData('escort', { party: { kind: 'escort', vipPosition: 3, success: true, vipCarId: 5 } }));
    expect(actions(escort)).toEqual(['TENTAR DE NOVO', 'MENU']);
  });
});

describe('recorde × primeira marca', () => {
  it('1ª marca mostra "PRIMEIRA MARCA", nunca "RECORDE"; marca batida mostra "RECORDE"', () => {
    const badges = (d: ResultsScreenData) => all(resultsScreen(api(sanitizeSave({}), ['kb1', null, null, null]), d).el, hasClass('record-badge')).map(text);
    expect(badges(quickData('quick', { newRecords: [{ seat: 0, kind: 'lap', first: true }, { seat: 0, kind: 'race', first: true }] })))
      .toEqual(['PRIMEIRA MARCA', 'PRIMEIRA MARCA']);
    expect(badges(quickData('quick', { newRecords: [{ seat: 0, kind: 'lap' }] }))).toEqual(['RECORDE']);
  });
});

describe('créditos', () => {
  it('créditos: Three.js/WebGL e CC0, em PT e EN', () => {
    for (const lang of ['pt', 'en'] as const) {
      setLanguage(lang);
      expect(t('ui.credits.tech')).toContain('Three.js');
      expect(t('ui.credits.tech')).not.toContain('Canvas 2D');
      expect(t('ui.credits.assets')).toContain('CC0');
    }
  });
});
