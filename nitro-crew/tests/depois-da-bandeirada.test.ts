// Defeito relatado pelo dono (09/10/2026): "quando terminar, não ter o carro parado acelerando". Com a corrida acabada o
// `stepRace` só conta o tick, então os carros congelavam na tela (e o motor ficava no último giro) durante os
// RESULTS_DELAY antes do resultado. A cena agora segue numa cópia só de exibição; o estado real não muda (nem o hash).
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { MenuContext, Menus, MenuScreen, Renderer, RenderFrame } from '../src/game/contracts';
import { createSession, type Session } from '../src/game/session';
import { human } from './helpers';

const seen = vi.hoisted(() => ({ frame: null as RenderFrame | null }));
vi.mock('../src/render/renderer', () => ({
  createRenderer: (canvas: HTMLCanvasElement): Renderer => ({
    canvas, resize: () => undefined, render: (f: RenderFrame) => { seen.frame = f; }, renderIdle: () => undefined, dispose: () => undefined,
  }),
}));
vi.mock('../src/ui/menus', () => ({
  createMenus: (_ctx: MenuContext): Menus => {
    let current: MenuScreen | null = null;
    return {
      show: (s: MenuScreen) => { current = s; }, hide: () => { current = null; }, current: () => current,
      navigate: () => undefined, update: () => undefined, refreshLanguage: () => undefined,
    };
  },
}));

let session: Session;
let now = 0;
function frame(): void { now += 1000 / 60; session.frame(now); }

beforeEach(() => {
  vi.stubGlobal('window', {
    innerWidth: 1280, innerHeight: 720, devicePixelRatio: 1,
    addEventListener: () => undefined, removeEventListener: () => undefined,
    navigator: { getGamepads: () => [null, null, null, null] },
  });
  session = createSession({} as HTMLCanvasElement, {} as HTMLElement, {} as HTMLElement);
  now = 1000;
  seen.frame = null;
});
afterEach(() => { session.input.dispose(); vi.unstubAllGlobals(); });

describe('depois da bandeirada', () => {
  it('os carros seguem andando na tela até o resultado, e o estado real não muda', () => {
    session.input.bindSeat(0, 'kb1');
    session.startQuick('copacabana', 2, [human(0)]);
    session.debugStep(60 * 8);
    const state = session.race!.state;
    expect(state.phase).toBe('racing');
    const me = state.cars.find((c) => c.seat === 0)!;
    me.finished = true; me.finishTick = state.tick; state.firstHumanFinishTick = state.tick;
    session.debugStep(1);
    expect(state.phase).toBe('finished');
    frame();
    const z0 = seen.frame!.state.cars.map((c) => c.z);
    const real0 = state.cars.map((c) => [c.z, c.x, c.speed]);
    for (let i = 0; i < 30; i++) frame();
    expect(session.race!.state.phase).toBe('finished');
    expect(state.cars.map((c) => [c.z, c.x, c.speed]), 'o estado real não pode andar depois da bandeirada').toEqual(real0);
    const z1 = seen.frame!.state.cars.map((c) => c.z);
    expect(z1[me.id], 'o carro de quem terminou segue andando').toBeGreaterThan(z0[me.id] + 5);
    expect(seen.frame!.state.cars.filter((c, i) => c.z > z0[i] + 5).length, 'os outros também').toBeGreaterThanOrEqual(2);
  });
});
