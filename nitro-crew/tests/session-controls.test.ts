// A sessão ligada aos controles: os bindings e a vibração saem das opções, a vibração sai dos
// eventos da corrida e a pausa escolhida no controle fica aberta. Roda em Node: o renderizador
// (WebGL) e os menus (DOM) são dublês; entrada, simulação, opções e áudio são os de verdade.
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { MenuContext, MenuNav, MenuScreen, Menus, Renderer } from '../src/game/contracts';
import { createSession, type Session } from '../src/game/session';
import { RUMBLE_GO } from '../src/game/rumble';
import { assignBinding, DEFAULT_BINDINGS, type BindAction, type BindCode, type BindDevice, type ControlBindings } from '../src/ui/remap/bindings';
import { human } from './helpers';

vi.mock('../src/render/renderer', () => ({
  createRenderer: (canvas: HTMLCanvasElement): Renderer => ({
    canvas, resize: () => undefined, render: () => undefined, renderIdle: () => undefined, dispose: () => undefined,
  }),
}));

// Menus de mentira com o comportamento da pauseScreen de verdade (src/ui/screens/simple.ts): o foco
// começa em "Continuar", então confirmar (A) ou voltar (B) retomam a corrida. O teclado chega aos
// menus pelo DOM, não por aqui — `navigate` só recebe gamepads, como o createMenus de verdade.
vi.mock('../src/ui/menus', () => ({
  createMenus: (ctx: MenuContext): Menus => {
    let current: MenuScreen | null = null;
    return {
      show: (screen: MenuScreen) => { current = screen; },
      hide: () => { current = null; },
      current: () => current,
      navigate: (nav: MenuNav) => {
        if (!nav.device || nav.device === 'kb1' || nav.device === 'kb2') return;
        if (current === 'pause' && (nav.confirm || nav.back)) ctx.onEvent({ type: 'resume' });
      },
      update: () => undefined,
      refreshLanguage: () => undefined,
    };
  },
}));

interface FakePad {
  index: number; id: string; connected: boolean; axes: number[];
  buttons: Array<{ pressed: boolean; value: number }>;
  vibrationActuator: { playEffect: ReturnType<typeof vi.fn> };
}

function fakePad(index: number): FakePad {
  return {
    index, id: 'Xbox Wireless Controller (STANDARD GAMEPAD Vendor: 045e Product: 0b13)', connected: true, axes: [0, 0, 0, 0],
    buttons: Array.from({ length: 17 }, () => ({ pressed: false, value: 0 })),
    vibrationActuator: { playEffect: vi.fn(() => Promise.resolve('complete')) },
  };
}

/** Janela mínima para a sessão e a entrada: ouvintes, tamanho e a Gamepad API com um controle falso. */
function fakeWindow(pads: Array<FakePad | null>) {
  const listeners = new Map<string, Array<(e: unknown) => void>>();
  const win = {
    innerWidth: 1280, innerHeight: 720, devicePixelRatio: 1,
    addEventListener: (type: string, fn: (e: unknown) => void) => { listeners.set(type, [...(listeners.get(type) ?? []), fn]); },
    removeEventListener: (type: string, fn: (e: unknown) => void) => { listeners.set(type, (listeners.get(type) ?? []).filter((f) => f !== fn)); },
    navigator: { getGamepads: () => pads },
  };
  const key = (type: 'keydown' | 'keyup', code: string) => {
    const e = { code, repeat: false, ctrlKey: false, metaKey: false, altKey: false, target: null, preventDefault: () => undefined };
    for (const fn of listeners.get(type) ?? []) fn(e);
  };
  return { win, down: (code: string) => key('keydown', code), up: (code: string) => key('keyup', code) };
}

const element = {} as HTMLElement;

function bind(b: ControlBindings, device: BindDevice, action: BindAction, code: BindCode): ControlBindings {
  const r = assignBinding(b, device, action, code);
  expect(r.rejected, `${device}.${action} ← ${code}`).toBe(false);
  return r.bindings;
}

let pad: FakePad;
let w: ReturnType<typeof fakeWindow>;
let session: Session;
let now = 0;

/** Um quadro da sessão, a 60 Hz. */
function frame(): void {
  now += 1000 / 60;
  session.frame(now);
}

function press(button: number, down = true): void {
  pad.buttons[button] = { pressed: down, value: down ? 1 : 0 };
  frame();
}

function speedOf(seat: number): number {
  const car = session.race?.state.cars.find((c) => c.seat === seat);
  return car ? car.speed : -1;
}

beforeEach(() => {
  pad = fakePad(0);
  w = fakeWindow([pad, null, null, null]);
  vi.stubGlobal('window', w.win);
  session = createSession({} as HTMLCanvasElement, element, element);
  now = 1000;
});

afterEach(() => {
  session.input.dispose();
  vi.unstubAllGlobals();
});

describe('sessão: pausa escolhida no controle', () => {
  it.each([
    [0, 'A'],
    [1, 'B'],
  ])('pausa no botão %i (%s) abre a pausa e ela fica aberta; o mesmo botão depois retoma', (button) => {
    session.settings.controls = bind(DEFAULT_BINDINGS, 'gamepad', 'pause', button);
    session.input.bindSeat(0, 'gp0');
    session.startQuick('copacabana', 2, [human(0)]);
    frame();
    session.debugStep(60 * 5);
    press(button);
    expect({ paused: session.paused, menu: session.menus.current() }).toEqual({ paused: true, menu: 'pause' });
    press(button, false);
    frame();
    expect({ paused: session.paused, menu: session.menus.current() }).toEqual({ paused: true, menu: 'pause' });
    // No menu de pausa o botão volta a ser confirmar/voltar (navegação fixa): retoma a corrida.
    press(button);
    expect({ paused: session.paused, menu: session.menus.current() }).toEqual({ paused: false, menu: null });
  });

  it('Start continua pausando e a pausa fica aberta', () => {
    session.input.bindSeat(0, 'gp0');
    session.startQuick('copacabana', 2, [human(0)]);
    frame();
    press(9);
    press(9, false);
    expect({ paused: session.paused, menu: session.menus.current() }).toEqual({ paused: true, menu: 'pause' });
  });
});

describe('sessão: bindings das opções na corrida', () => {
  it('a tecla remapeada acelera e a antiga não', () => {
    session.settings.controls = bind(DEFAULT_BINDINGS, 'kb1', 'throttle', 'KeyR');
    session.input.bindSeat(0, 'kb1');
    session.startQuick('copacabana', 2, [human(0)], true);
    w.down('KeyR');
    session.debugStep(60 * 8);
    w.up('KeyR');
    expect(speedOf(0)).toBeGreaterThan(800);
    session.startQuick('copacabana', 2, [human(0)], true);
    w.down('ArrowUp');
    session.debugStep(60 * 8);
    w.up('ArrowUp');
    expect(speedOf(0)).toBeLessThan(100);
  });
});

describe('sessão: vibração', () => {
  const rumbles = () => pad.vibrationActuator.playEffect.mock.calls.map((c: unknown[]) => c[1] as { duration: number; strongMagnitude: number });

  it('a largada vibra o controle do assento com dual-rumble', () => {
    session.input.bindSeat(0, 'gp0');
    session.startQuick('copacabana', 2, [human(0)], true);
    session.debugStep(60 * 5);
    expect(pad.vibrationActuator.playEffect).toHaveBeenCalledWith('dual-rumble', expect.objectContaining({ duration: RUMBLE_GO.ms, strongMagnitude: RUMBLE_GO.strength }));
    expect(rumbles().length).toBeGreaterThan(0);
  });

  it('com a Vibração desligada nas opções, nenhuma chamada', () => {
    session.settings.vibration = false;
    session.input.bindSeat(0, 'gp0');
    session.startQuick('copacabana', 2, [human(0)], true);
    session.debugStep(60 * 5);
    expect(pad.vibrationActuator.playEffect).not.toHaveBeenCalled();
  });

  it('controle sem assento não vibra (o assento é do teclado)', () => {
    session.input.bindSeat(0, 'kb1');
    session.startQuick('copacabana', 2, [human(0)], true);
    session.debugStep(60 * 5);
    expect(pad.vibrationActuator.playEffect).not.toHaveBeenCalled();
  });
});
