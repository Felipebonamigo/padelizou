// Ganchos da sessão (session.ts) para o campeonato salvo (1.7a) e a carreira (3.3), com a sessão de
// verdade: renderizador, entrada, áudio e menus trocados por dublês; save num localStorage em memória
// (reabrir o jogo = criar outra sessão sobre o mesmo armazenamento). A corrida é inteira, com o carro
// humano guiado pelo cérebro da IA — é o caminho corrida → resultado → save que se quer travar.
import { afterAll, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { COUNTDOWN_TICKS } from '../src/core/constants';
import { CUPS } from '../src/core/data/cups';
import type { HumanEntry } from '../src/core/types';
import { NEUTRAL_INPUT } from '../src/core/types';
import type { InputProvider, MenuContext, MenuNav, MenuScreen, Menus, SaveData } from '../src/game/contracts';
import { SAVE_KEY } from '../src/game/save';
import { SETTINGS_KEY } from '../src/game/settings';
import type { Session } from '../src/game/session';

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
    menuNav: () => nav, pausePressed: () => -1, dispose() {},
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

function saved(): SaveData {
  return JSON.parse(store.get(SAVE_KEY) ?? '{}') as SaveData;
}

/**
 * Leva a corrida atual ao fim com os humanos vencendo — o humano larga e é posto na reta final da
 * última volta, para o resultado não depender do balanceamento — e deixa a sessão mostrar o resultado.
 */
function finishRace(s: Session): void {
  const r = s.race;
  if (!r) throw new Error('sem corrida');
  s.debugStep(COUNTDOWN_TICKS + 60);
  for (const c of r.state.cars) {
    if (c.seat < 0) continue;
    c.lap = r.state.config.laps; c.z = r.track.length - 50 - c.seat * 10; c.x = 0; c.speed = 4000;
  }
  s.debugStep(60 * 10);
  expect(r.state.phase).toBe('finished');
  // Depois da linha, a sessão espera alguns segundos antes do resultado: quadros de 0,25 s.
  for (let i = 0, now = 1000; i < 40 && open === null; i++, now += 250) s.frame(now);
  expect(open).toBe('results');
}

const solo: HumanEntry[] = [{ seat: 0, name: 'Ana', carId: 'falcao', teamId: 0, color: '#fff' }];

describe('campeonato salvo pela sessão (1.7a)', () => {
  it('começar grava; cada corrida grava; reabrir e Continuar corre a pista seguinte com o mesmo elenco; sair pela pausa mantém o índice', async () => {
    const cup = CUPS[0];
    const s1 = await newSession();
    s1.handleMenuEvent({ type: 'startCup', cupId: cup.id, humans: solo });
    const seed = s1.race?.state.config.rosterSeed;
    expect(seed).toBeTypeOf('number');
    // Gravada já na largada: fechar o jogo na primeira corrida ainda deixa o "Continuar".
    expect(saved().cupInProgress?.champ.raceIndex).toBe(0);
    expect(saved().cupInProgress?.cupSeed).toBe(seed);

    finishRace(s1);
    expect(saved().cupInProgress?.champ.lastVerdict).toBe('qualified');
    expect(saved().cupInProgress?.champ.raceIndex).toBe(1);

    // Reabre o jogo: outra sessão sobre o mesmo save.
    const s2 = await newSession();
    s2.handleMenuEvent({ type: 'continueCup' });
    expect(s2.race?.mode).toBe('cup');
    expect(s2.race?.state.config.trackId).toBe(cup.trackIds[1]);
    expect(s2.race?.state.config.rosterSeed).toBe(seed);
    // Sai pela pausa no meio da corrida: nada muda no save.
    s2.debugStep(600);
    s2.handleMenuEvent({ type: 'toMain' });
    expect(s2.race).toBeNull();
    expect(saved().cupInProgress?.champ.raceIndex).toBe(1);

    const s3 = await newSession();
    s3.handleMenuEvent({ type: 'continueCup' });
    expect(s3.race?.state.config.trackId).toBe(cup.trackIds[1]);
    // Da classificação, "próxima corrida" segue a copa (e não vai para a garagem).
    finishRace(s3);
    expect(saved().cupInProgress?.champ.raceIndex).toBe(2);
    s3.handleMenuEvent({ type: 'nextRace' });
    expect(s3.race?.mode).toBe('cup');
    expect(s3.race?.state.config.trackId).toBe(cup.trackIds[2]);
    expect(s3.race?.state.config.rosterSeed).toBe(seed);
  }, 120_000);
});

describe('carreira pela sessão (3.3)', () => {
  it('garagem → corrida → resultado grava prêmio; da classificação, o próximo passo é a garagem; sair no meio mantém a pista', async () => {
    const s = await newSession();
    s.handleMenuEvent({ type: 'startCareer', humans: solo, resume: false });
    expect(open).toBe('garage');
    expect(saved().career?.drivers.map((d) => d.name)).toEqual(['Ana']);

    s.handleMenuEvent({ type: 'careerRace' });
    expect(s.race?.mode).toBe('career');
    expect(s.race?.state.config.trackId).toBe(CUPS[0].trackIds[0]);
    // Sair no meio da corrida não conta nada: a corrida volta a ser a próxima.
    s.debugStep(600);
    s.handleMenuEvent({ type: 'toMain' });
    expect(saved().career?.racesRun).toBe(0);
    s.handleMenuEvent({ type: 'startCareer', humans: solo, resume: true });
    s.handleMenuEvent({ type: 'careerRace' });
    expect(s.race?.state.config.trackId).toBe(CUPS[0].trackIds[0]);

    const money = saved().career?.wallets[0] ?? 0;
    finishRace(s);
    const career = saved().career;
    expect(career?.racesRun).toBe(1);
    expect(career?.lastReport?.verdict).toBe('qualified');
    expect(career?.wallets[0]).toBeGreaterThan(money);
    expect(career?.champ?.raceIndex).toBe(1);

    s.handleMenuEvent({ type: 'nextRace' });
    expect(open).toBe('garage');
    expect(s.race).toBeNull();
    s.handleMenuEvent({ type: 'careerRace' });
    expect(s.race?.mode).toBe('career');
    expect(s.race?.state.config.trackId).toBe(CUPS[0].trackIds[1]);
  }, 120_000);
});
