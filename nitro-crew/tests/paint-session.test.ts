// A pintura pela sessão de verdade (session.ts): o que o lobby, a copa salva, a carreira e o tutorial mandam
// chega ao renderizador em `RenderFrame.paints` (um por carro), e a corrida (config/estado) não a carrega.
// Renderizador, entrada, áudio e menus são dublês; o save mora num localStorage em memória.
import { afterAll, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { CUPS } from '../src/core/data/cups';
import type { HumanEntry } from '../src/core/types';
import { NEUTRAL_INPUT } from '../src/core/types';
import type { InputProvider, MenuContext, MenuNav, MenuScreen, Menus, RenderFrame } from '../src/game/contracts';
import { paintColors, withPaint } from '../src/game/paints';
import { SAVE_KEY } from '../src/game/save';
import { SETTINGS_KEY } from '../src/game/settings';
import type { Session } from '../src/game/session';

const frames: RenderFrame[] = [];
let open: MenuScreen | null = null;

vi.mock('../src/render/renderer', () => ({
  createRenderer: () => ({ canvas: {}, resize() {}, render(f: RenderFrame) { frames.push(f); }, renderIdle() {}, dispose() {} }),
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
// O painel do tutorial é DOM no HUD; aqui só importa o que a sessão manda desenhar.
vi.mock('../src/ui/screens/tutorial', () => ({
  createTutorialPanel: () => ({ update() {}, hide() {}, layout: () => null }),
}));
vi.mock('../src/ui/menus', () => ({
  createMenus: (_ctx: MenuContext): Partial<Menus> => ({
    show(screen: MenuScreen) { open = screen; },
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
  store.set(SETTINGS_KEY, JSON.stringify({ totalCars: 8 }));
  frames.length = 0;
  open = null;
});

async function newSession(): Promise<Session> {
  const { createSession } = await import('../src/game/session');
  const dummy = {} as HTMLElement;
  return createSession(dummy as HTMLCanvasElement, dummy, dummy);
}

/** O último quadro desenhado da corrida atual. */
function lastFrame(s: Session): RenderFrame {
  s.frame(1000 + frames.length * 16);
  const f = frames[frames.length - 1];
  if (!f) throw new Error('nada desenhado');
  return f;
}

function paintOfSeat(s: Session, f: RenderFrame, seat: number) {
  const i = s.race?.state.cars.findIndex((c) => c.seat === seat) ?? -1;
  expect(i).toBeGreaterThanOrEqual(0);
  return f.paints?.[i] ?? null;
}

/** A sessão de verdade (importada na hora, como em career-session.test.ts) leva segundos sob carga. */
const SESSION_TIMEOUT = 60_000;

const human = (seat: number, carId: string, paint?: string): HumanEntry =>
  withPaint({ seat, name: `P${seat + 1}`, carId, teamId: 0, color: '#fff' }, paint);

describe('sessão: a pintura chega ao renderizador', () => {
  it('corrida rápida: o carro de cada humano na cor escolhida, a IA de fábrica; a config e o estado sem pintura', async () => {
    const s = await newSession();
    s.startQuick('copacabana', 2, [human(0, 'falcao', 'cobalto'), human(1, 'trovao')]);
    const f = lastFrame(s);
    expect(paintOfSeat(s, f, 0)).toEqual(paintColors('cobalto'));
    expect(paintOfSeat(s, f, 1)).toBeNull();
    s.race?.state.cars.forEach((c, i) => { if (c.seat < 0) expect(f.paints?.[i] ?? null).toBeNull(); });
    expect(s.race?.state.config.humans.some((h) => 'paint' in h)).toBe(false);
    expect(JSON.stringify(s.race?.state)).not.toContain('paint');
    // "Correr de novo" mantém a pintura (os humanos da sessão a guardam; a config continua sem).
    s.handleMenuEvent({ type: 'retryRace' });
    expect(paintOfSeat(s, lastFrame(s), 0)).toEqual(paintColors('cobalto'));
    expect(s.race?.state.config.humans.some((h) => 'paint' in h)).toBe(false);
  }, SESSION_TIMEOUT);

  it('copa: a pintura vai junto para o "Continuar"', async () => {
    const s1 = await newSession();
    s1.handleMenuEvent({ type: 'startCup', cupId: CUPS[0].id, humans: [human(0, 'saci', 'esmeralda')] });
    expect(paintOfSeat(s1, lastFrame(s1), 0)).toEqual(paintColors('esmeralda'));
    const s2 = await newSession();
    s2.handleMenuEvent({ type: 'continueCup' });
    expect(paintOfSeat(s2, lastFrame(s2), 0)).toEqual(paintColors('esmeralda'));
  }, SESSION_TIMEOUT);

  it('carreira: a pintura guardada para o assento e o carro da garagem', async () => {
    store.set(SAVE_KEY, JSON.stringify({ seatPaints: [{ falcao: 'onix' }, { falcao: 'gelo' }] }));
    const s = await newSession();
    s.handleMenuEvent({ type: 'startCareer', humans: [human(0, 'falcao'), human(1, 'falcao')], resume: false });
    expect(open).toBe('garage');
    s.handleMenuEvent({ type: 'careerRace' });
    expect(s.race?.mode).toBe('career');
    const f = lastFrame(s);
    expect(paintOfSeat(s, f, 0)).toEqual(paintColors('onix'));
    expect(paintOfSeat(s, f, 1)).toEqual(paintColors('gelo'));
    expect(s.race?.state.config.humans.some((h) => 'paint' in h)).toBe(false);
  }, SESSION_TIMEOUT);

  it('tutorial: a pintura que vem do cartão do tutorial, e a corrida sem ela', async () => {
    const s = await newSession();
    s.handleMenuEvent({ type: 'startTutorial', humans: [human(0, 'tatu', 'prata')] });
    const f = lastFrame(s);
    expect(paintOfSeat(s, f, 0)).toEqual(paintColors('prata'));
    expect(s.race?.state.config.humans.some((h) => 'paint' in h)).toBe(false);
  }, SESSION_TIMEOUT);
});
