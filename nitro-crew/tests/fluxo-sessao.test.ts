// Recomeçar a copa pela sessão de verdade (onda K, frente K1): o eliminado larga de novo a mesma copa com os
// humanos que o resultado mostrou — assentos, nomes, carros e pinturas. Dublês e save em memória copiados de
// tests/career-session.test.ts (linhas 5–78); o menu falso guarda os dados da última tela mostrada.
import { afterAll, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { COUNTDOWN_TICKS } from '../src/core/constants';
import { CUPS } from '../src/core/data/cups';
import type { HumanEntry } from '../src/core/types';
import { NEUTRAL_INPUT } from '../src/core/types';
import type { InputProvider, MenuContext, MenuNav, MenuScreen, Menus, SaveData } from '../src/game/contracts';
import { SAVE_KEY } from '../src/game/save';
import { SETTINGS_KEY } from '../src/game/settings';
import type { Session } from '../src/game/session';
import type { ResultsScreenData } from '../src/game/contracts';

const shown: MenuScreen[] = [];
let open: MenuScreen | null = null;
let lastData: ResultsScreenData | null = null;

vi.mock('../src/render/renderer', () => ({
  createRenderer: () => ({ canvas: {}, resize() {}, render() {}, renderIdle() {}, dispose() {} }),
}));
vi.mock('../src/audio/audio', () => ({
  createAudio: () => ({
    unlock() {}, update() {}, onEvent() {}, setMusic() {}, currentMusic: () => null, setVolumes() {}, musicList: () => [], ui() {},
  }),
}));
vi.mock('../src/ui/input', () => {
  const bound: Array<string | null> = [null, null, null, null];
  const nav: MenuNav = { up: false, down: false, left: false, right: false, confirm: false, back: false, start: false, device: null };
  const input: Partial<InputProvider> = {
    poll() {}, readSeat: () => NEUTRAL_INPUT, devices: () => [], joinPressed: () => null, leavePressed: () => null,
    menuNav: () => nav, pausePressed: () => -1, dispose() {}, peek: () => null, rumble() {},
    bindSeat(seat, device) { bound[seat] = device; },
    unbindSeat(seat) { bound[seat] = null; },
    seatDevice: (seat) => (bound[seat] ?? null) as ReturnType<InputProvider['seatDevice']>,
  };
  return { createInput: () => input };
});
vi.mock('../src/ui/menus', () => ({
  createMenus: (_ctx: MenuContext): Partial<Menus> => ({
    show(screen: MenuScreen, data?: unknown) { shown.push(screen); open = screen; lastData = (data ?? null) as ResultsScreenData | null; },
    hide() { open = null; },
    current: () => open,
    navigate() {}, update() {}, refreshLanguage() {},
  }),
}));

const store = new Map<string, string>();
const memStorage = {
  getItem: (k: string) => store.get(k) ?? null,
  setItem: (k: string, v: string) => { store.set(k, v); },
  removeItem: (k: string) => { store.delete(k); },
  clear: () => store.clear(),
  key: () => null,
  get length() { return store.size; },
};

beforeAll(() => {
  vi.stubGlobal('localStorage', memStorage);
  vi.stubGlobal('window', { addEventListener() {}, innerWidth: 1280, innerHeight: 720, devicePixelRatio: 1 });
});
afterAll(() => { vi.unstubAllGlobals(); });
beforeEach(() => {
  store.clear();
  // Pista menor para a corrida inteira caber no teste (o mínimo das opções).
  store.set(SETTINGS_KEY, JSON.stringify({ totalCars: 8 }));
  shown.length = 0;
  open = null;
});

async function newSession(): Promise<Session> {
  const { createSession } = await import('../src/game/session');
  const dummy = {} as HTMLElement;
  return createSession(dummy as HTMLCanvasElement, dummy, dummy);
}

function saved(): SaveData {
  return JSON.parse(store.get(SAVE_KEY) ?? '{}') as SaveData;
}

describe('recomeçar a copa (K1)', () => {
  it('eliminado → startCup com os humanos do resultado: mesma copa do início, mesmos assentos, nomes, carros e pinturas', async () => {
    const s = await newSession();
    s.input.bindSeat(0, 'kb1');
    s.input.bindSeat(1, 'gp0');
    const humans: HumanEntry[] = [
      { seat: 0, name: 'Ana', carId: 'trovao', teamId: 0, color: '#fff', paint: 'rubi' },
      { seat: 1, name: 'Bia', carId: 'tornado', teamId: 0, color: '#fff' },
    ];
    s.handleMenuEvent({ type: 'startCup', cupId: CUPS[0].id, humans });
    const r = s.race;
    if (!r) throw new Error('sem corrida');
    // A IA cruza a linha antes; os humanos chegam por último (eliminação sem depender do balanceamento).
    s.debugStep(COUNTDOWN_TICKS + 60);
    let k = 0;
    for (const c of r.state.cars) {
      c.lap = r.state.config.laps; c.x = 0;
      if (c.seat >= 0) { c.z = r.track.length - 900; c.speed = 1500; } else { c.z = r.track.length - 40 - k * 3; c.speed = 4000; k++; }
    }
    s.debugStep(600);
    for (let i = 0, now = 1000; i < 40 && open !== 'results'; i++, now += 250) s.frame(now);
    expect(open).toBe('results');
    const d = lastData;
    if (!d) throw new Error('sem dados do resultado');
    const champ = d.champ;
    if (!champ) throw new Error('resultado sem campeonato');
    expect(champ.eliminated).toBe(true);
    s.handleMenuEvent({ type: 'startCup', cupId: champ.cupId, humans: d.humans });
    expect(s.race?.mode).toBe('cup');
    expect(s.race?.state.config.trackId).toBe('copacabana');
    expect(s.race?.humans).toEqual(d.humans);
    expect(s.race?.humans[0].paint).toBe('rubi');
    expect(s.input.seatDevice(0)).toBe('kb1');
    expect(s.input.seatDevice(1)).toBe('gp0');
    expect(saved().cupInProgress?.champ.raceIndex).toBe(0);
  }, 120_000);
});
