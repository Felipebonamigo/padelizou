// Defeito relatado pelo dono (09/10/2026): "o nitro nem sempre funciona de primeira". A entrada fecha a borda do botão a
// cada quadro de tela (`poll()`), mas a física roda a 60 passos por segundo; num quadro de monitor rápido em que não cabe
// nenhum passo, o aperto era lido, descartado e nunca chegava a `stepRace`. Aqui o monitor roda a 144 Hz.
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { MenuContext, Menus, MenuScreen, Renderer } from '../src/game/contracts';
import { createSession, type Session } from '../src/game/session';
import { human } from './helpers';

vi.mock('../src/render/renderer', () => ({
  createRenderer: (canvas: HTMLCanvasElement): Renderer => ({
    canvas, resize: () => undefined, render: () => undefined, renderIdle: () => undefined, dispose: () => undefined,
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

const BUTTON_NITRO = 5; // RB (decisão nº 9 do cronograma muda isto na M; aqui vale o mapa de hoje)

function fakePad() {
  return {
    index: 0, id: 'Xbox Wireless Controller (STANDARD GAMEPAD Vendor: 045e Product: 0b13)', connected: true, axes: [0, 0, 0, 0],
    buttons: Array.from({ length: 17 }, () => ({ pressed: false, value: 0 })),
    vibrationActuator: { playEffect: vi.fn(() => Promise.resolve('complete')) },
  };
}

let pad: ReturnType<typeof fakePad>;
let session: Session;
let now = 0;

let frameMs = 1000 / 144;
function frame(): void { now += frameMs; session.frame(now); }

beforeEach(() => {
  frameMs = 1000 / 144;
  pad = fakePad();
  const win = {
    innerWidth: 1280, innerHeight: 720, devicePixelRatio: 1,
    addEventListener: () => undefined, removeEventListener: () => undefined,
    navigator: { getGamepads: () => [pad, null, null, null] },
  };
  vi.stubGlobal('window', win);
  session = createSession({} as HTMLCanvasElement, {} as HTMLElement, {} as HTMLElement);
  now = 1000;
});
afterEach(() => { session.input.dispose(); vi.unstubAllGlobals(); });

describe('nitro num monitor de 144 Hz', () => {
  it('o aperto num quadro em que não cabe nenhum passo da física ainda dispara o nitro', () => {
    session.input.bindSeat(0, 'gp0');
    session.startQuick('copacabana', 2, [human(0)], true);
    session.debugStep(60 * 5);
    const car = () => session.race!.state.cars.find((c) => c.seat === 0)!;
    expect(session.race!.state.phase).toBe('racing');
    // Depois de um quadro que rodou um passo, o seguinte nunca roda (sobra < 6,95 ms; o passo pede 16,67).
    let lastTick = session.race!.state.tick;
    for (let i = 0; i < 20; i++) {
      frame();
      const t = session.race!.state.tick;
      if (t !== lastTick) { lastTick = t; break; }
      lastTick = t;
    }
    pad.buttons[BUTTON_NITRO] = { pressed: true, value: 1 };
    frame();
    expect(session.race!.state.tick, 'este quadro não pode ter rodado passo nenhum').toBe(lastTick);
    pad.buttons[BUTTON_NITRO] = { pressed: false, value: 0 };
    for (let i = 0; i < 6; i++) frame();
    expect(car().nitroTicks, 'o nitro disparou').toBeGreaterThan(0);
  });

  it('controle: a 60 Hz (todo quadro roda um passo) o mesmo aperto dispara — prova que botão e mapa estão certos', () => {
    frameMs = 1000 / 60;
    session.input.bindSeat(0, 'gp0');
    session.startQuick('copacabana', 2, [human(0)], true);
    session.debugStep(60 * 5);
    for (let i = 0; i < 3; i++) frame();
    pad.buttons[BUTTON_NITRO] = { pressed: true, value: 1 };
    frame();
    pad.buttons[BUTTON_NITRO] = { pressed: false, value: 0 };
    for (let i = 0; i < 3; i++) frame();
    expect(session.race!.state.cars.find((c) => c.seat === 0)!.nitroTicks).toBeGreaterThan(0);
  });
});
