// O diário de jogo ligado à sessão (onda K, K2 × K1, T4b): a corrida terminada vira uma linha do diário, o funil
// fecha na 1ª largada e o questionário abre no próximo evento-portão depois da 3ª corrida (nunca no fim dela).
// Arnês copiado de tests/fluxo-sessao.test.ts.
import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { COUNTDOWN_TICKS } from '../src/core/constants';
import type { HumanEntry } from '../src/core/types';
import { NEUTRAL_INPUT } from '../src/core/types';
import type { InputProvider, MenuContext, MenuNav, MenuScreen, Menus } from '../src/game/contracts';
import { createPlaylog, setActivePlaylog, type Playlog } from '../src/game/playlog';
import { SETTINGS_KEY } from '../src/game/settings';
import type { Session } from '../src/game/session';
import { fakeDeps } from './playlog-fixtures';

const shown: MenuScreen[] = [];
let open: MenuScreen | null = null;

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
    show(screen: MenuScreen) { shown.push(screen); open = screen; },
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


let log: Playlog;
beforeEach(() => {
  log = createPlaylog(fakeDeps().deps);
  log.sessionStart();
  setActivePlaylog(log);
});
afterEach(() => { setActivePlaylog(null); });

const humans: HumanEntry[] = [{ seat: 0, name: 'Ana', carId: 'trovao', teamId: 0, color: '#fff' }];

/** Roda a corrida em andamento até o resultado: carros na reta final, `debugStep`, e quadros até `results`. */
function finishRace(s: Session): void {
  const r = s.race;
  if (!r) throw new Error('sem corrida');
  s.debugStep(COUNTDOWN_TICKS + 60);
  let k = 0;
  for (const c of r.state.cars) {
    c.lap = r.state.config.laps; c.x = 0;
    c.z = r.track.length - 40 - k * 3; c.speed = 4000; k++;
  }
  s.debugStep(600);
  for (let i = 0, now = 1000; i < 40 && open !== 'results'; i++, now += 250) s.frame(now);
  expect(open).toBe('results');
}

describe('diário de jogo na sessão (K2)', () => {
  it('uma corrida rápida terminada entra no diário, e o funil fecha na largada', async () => {
    const s = await newSession();
    s.input.bindSeat(0, 'kb1');
    s.handleMenuEvent({ type: 'startQuick', trackId: 'copacabana', laps: 1, humans });
    finishRace(s);
    const d = log.data();
    expect(d.races).toHaveLength(1);
    expect(d.races[0]).toMatchObject({ mode: 'quick', track: 'copacabana', abandoned: false });
    expect(d.sessions[0].funnel?.mode).toBe('quick');
  }, 120_000);

  it('o questionário não abre no fim da 3ª corrida: abre no próximo evento-portão (toMain)', async () => {
    const s = await newSession();
    s.input.bindSeat(0, 'kb1');
    s.handleMenuEvent({ type: 'startQuick', trackId: 'copacabana', laps: 1, humans });
    finishRace(s);
    s.handleMenuEvent({ type: 'retryRace' });
    finishRace(s);
    s.handleMenuEvent({ type: 'retryRace' }); // só 2 corridas completas: sem questionário, larga a 3ª
    expect(open).not.toBe('survey');
    expect(s.race?.state.phase).not.toBe('finished');
    finishRace(s);
    expect(open).toBe('results');
    s.handleMenuEvent({ type: 'toMain' });
    expect(open).toBe('survey');
  }, 240_000);
});
