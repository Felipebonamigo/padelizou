// Remapeamento de controles e vibração: partes puras e o provedor de entrada com uma janela falsa.
// Roda em Node, sem DOM.
import { describe, expect, it, vi } from 'vitest';
import { carDef } from '../src/core/data/cars';
import { effectiveStats } from '../src/core/sim/stats';
import type { CarState, RaceState, SimEvent } from '../src/core/types';
import { DEFAULT_SETTINGS } from '../src/game/contracts';
import { newRumbleMemory, RUMBLE_GRASS, rumbleCues } from '../src/game/rumble';
import { sanitizeSettings } from '../src/game/settings';
import { setLanguage } from '../src/i18n';
import {
  createInput, mapGamepad, mapKeyboard, nextRumble, rumbleMagnitudes, usedKeyCodes, USED_KEY_CODES,
} from '../src/ui/input';
import {
  assignBinding, BIND_ACTIONS, BIND_DEVICES, DEFAULT_BINDINGS, defaultBindings, isDefaultDevice, keyboardConflicts,
  restoreDefaults, sanitizeBindings, type BindAction, type BindCode, type BindDevice, type ControlBindings,
} from '../src/ui/remap/bindings';
import { CAPTURE_SECONDS, captureButtons, captureKey, captureTick, startCapture } from '../src/ui/remap/capture';
import { buttonLabel, codesLabel, deviceLabel, deviceTitle, keyLabel, padStyleOf, rejectedText } from '../src/ui/remap/labels';

const keys = (...codes: string[]) => new Set(codes);
function buttons(...pressed: number[]): boolean[] {
  const b = new Array<boolean>(17).fill(false);
  for (const i of pressed) b[i] = true;
  return b;
}
function assign(b: ControlBindings, device: BindDevice, action: BindAction, code: BindCode): ControlBindings {
  const r = assignBinding(b, device, action, code);
  expect(r.rejected, `${device}.${action} ← ${code}`).toBe(false);
  return r.bindings;
}
/** Nenhuma ação sem código e nenhum código repetido dentro de um dispositivo. */
function expectSound(b: ControlBindings): void {
  for (const device of BIND_DEVICES) {
    const map: Record<BindAction, readonly BindCode[]> = b[device];
    const all = BIND_ACTIONS.flatMap((a) => map[a]);
    for (const a of BIND_ACTIONS) expect(map[a].length, `${device}.${a} vazio`).toBeGreaterThan(0);
    expect(new Set(all).size, `${device} repete código: ${all.join(',')}`).toBe(all.length);
  }
}

// ───────────────────────────── Padrão e saneamento ─────────────────────────────

describe('bindings: padrão e saneamento', () => {
  it('o padrão é o mapeamento de antes e é o das opções', () => {
    expect(DEFAULT_BINDINGS.kb1).toEqual({ throttle: ['ArrowUp'], brake: ['ArrowDown'], left: ['ArrowLeft'], right: ['ArrowRight'], nitro: ['Space'], gearUp: ['KeyM'], gearDown: ['KeyN'], pause: ['Escape'] });
    expect(DEFAULT_BINDINGS.kb2).toEqual({ throttle: ['KeyW'], brake: ['KeyS'], left: ['KeyA'], right: ['KeyD'], nitro: ['KeyF'], gearUp: ['KeyE'], gearDown: ['KeyQ'], pause: ['Escape'] });
    expect(DEFAULT_BINDINGS.gamepad).toEqual({ throttle: [0, 7], brake: [2, 1, 6], left: [14], right: [15], nitro: [5], gearUp: [3], gearDown: [4], pause: [9] });
    expect(DEFAULT_SETTINGS.controls).toEqual(DEFAULT_BINDINGS);
    expect(DEFAULT_SETTINGS.vibration).toBe(true);
    expectSound(DEFAULT_BINDINGS);
  });

  it('o padrão é imutável e o saneamento devolve cópias novas', () => {
    expect(() => { DEFAULT_BINDINGS.kb1.nitro.push('KeyZ'); }).toThrow();
    const s = sanitizeSettings({});
    expect(s.controls).toEqual(DEFAULT_BINDINGS);
    s.controls.kb1.nitro.push('KeyZ');
    s.controls.gamepad.throttle.length = 0;
    expect(DEFAULT_BINDINGS.kb1.nitro).toEqual(['Space']);
    expect(defaultBindings()).toEqual(DEFAULT_BINDINGS);
    expect(defaultBindings().kb1).not.toBe(defaultBindings().kb1);
  });

  it('lixo vira o padrão; código inválido vira o padrão da ação', () => {
    for (const raw of [null, undefined, 3, 'x', [], {}]) expect(sanitizeBindings(raw)).toEqual(DEFAULT_BINDINGS);
    const b = sanitizeBindings({
      kb1: { nitro: ['Foo'], throttle: 'KeyW', brake: [42], gearUp: ['F5'], gearDown: ['ControlLeft'], pause: ['CapsLock'] },
      gamepad: { nitro: [99], throttle: ['0'], brake: [1.5], pause: [16], left: [-1] },
      kb2: 'lixo',
    });
    expect(b.kb1).toEqual(DEFAULT_BINDINGS.kb1);
    expect(b.kb2).toEqual(DEFAULT_BINDINGS.kb2);
    expect(b.gamepad).toEqual(DEFAULT_BINDINGS.gamepad);
  });

  it('códigos válidos ficam, sem repetição e no máximo três por ação', () => {
    const b = sanitizeBindings({ kb1: { nitro: ['KeyR', 'KeyR', 'KeyT', 'KeyY', 'KeyU'] }, gamepad: { nitro: [10, 11, 10] } });
    expect(b.kb1.nitro).toEqual(['KeyR', 'KeyT', 'KeyY']);
    expect(b.gamepad.nitro).toEqual([10, 11]);
    expect(b.kb1.throttle).toEqual(['ArrowUp']);
  });

  it('Esc e Start só valem na pausa (eles cancelam a captura e sempre pausam)', () => {
    const b = sanitizeBindings({ kb1: { nitro: ['Escape'], pause: ['KeyP'] }, gamepad: { nitro: [9], pause: [8] } });
    expect(b.kb1.nitro).toEqual(['Space']);
    expect(b.kb1.pause).toEqual(['KeyP']);
    expect(b.gamepad.nitro).toEqual([5]);
    expect(b.gamepad.pause).toEqual([8]);
  });

  it('conflito no mesmo dispositivo é resolvido sem deixar ação sem tecla', () => {
    const all = Object.fromEntries(BIND_ACTIONS.map((a) => [a, ['KeyX']]));
    const allPad = Object.fromEntries(BIND_ACTIONS.map((a) => [a, [0]]));
    const b = sanitizeBindings({ kb1: all, kb2: all, gamepad: allPad });
    expectSound(b);
    expect(b.kb1.throttle).toEqual(['KeyX']);
    expect(b.gamepad.throttle).toEqual([0]);
    // Uma troca gravada cujo outro lado se corrompeu: quem perdeu a tecla recebe a que ficou livre.
    const swap = sanitizeBindings({ kb1: { throttle: ['Space'], nitro: ['???'] } });
    expect(swap.kb1.throttle).toEqual(['Space']);
    expect(swap.kb1.nitro).toEqual(['ArrowUp']);
    expectSound(swap);
  });

  it('a mesma tecla em teclados diferentes não é conflito de dispositivo (só aviso)', () => {
    const b = sanitizeBindings({ kb1: { nitro: ['KeyW'] } });
    expect(b.kb1.nitro).toEqual(['KeyW']);
    expect(b.kb2.throttle).toEqual(['KeyW']);
    expect(keyboardConflicts(b)).toEqual([{ code: 'KeyW', kb1: 'nitro', kb2: 'throttle' }]);
    expect(keyboardConflicts(DEFAULT_BINDINGS)).toEqual([]);
  });

  it('sanitizeSettings cuida dos controles e da vibração', () => {
    const s = sanitizeSettings({ vibration: false, controls: { kb2: { nitro: ['KeyG'] } } });
    expect(s.vibration).toBe(false);
    expect(s.controls.kb2.nitro).toEqual(['KeyG']);
    expect(s.controls.kb1).toEqual(DEFAULT_BINDINGS.kb1);
    expect(sanitizeSettings({ vibration: 'sim' }).vibration).toBe(true);
    // O que foi gravado sobrevive a um ciclo JSON.
    expect(sanitizeSettings(JSON.parse(JSON.stringify(s)))).toEqual(s);
  });
});

// ───────────────────────────── Trocar e restaurar ─────────────────────────────

describe('assignBinding', () => {
  it('liga a tecla sem alterar a entrada', () => {
    const r = assignBinding(DEFAULT_BINDINGS, 'kb1', 'nitro', 'KeyR');
    expect(r).toMatchObject({ changed: true, rejected: false, displaced: null });
    expect(r.bindings.kb1.nitro).toEqual(['KeyR']);
    expect(r.bindings.kb2).toEqual(DEFAULT_BINDINGS.kb2);
    expect(DEFAULT_BINDINGS.kb1.nitro).toEqual(['Space']);
  });

  it('conflito no mesmo dispositivo: troca', () => {
    const r = assignBinding(DEFAULT_BINDINGS, 'kb1', 'nitro', 'ArrowUp');
    expect(r.bindings.kb1.nitro).toEqual(['ArrowUp']);
    expect(r.bindings.kb1.throttle).toEqual(['Space']);
    expect(r.displaced).toEqual({ action: 'throttle', codes: ['Space'], swapped: true });
    expectSound(r.bindings);
  });

  it('gamepad: tira o botão de uma ação que tinha vários; troca quando ela ficaria vazia', () => {
    const a = assignBinding(DEFAULT_BINDINGS, 'gamepad', 'nitro', 7);
    expect(a.bindings.gamepad.nitro).toEqual([7]);
    expect(a.bindings.gamepad.throttle).toEqual([0]);
    expect(a.displaced).toEqual({ action: 'throttle', codes: [0], swapped: false });
    const b = assignBinding(a.bindings, 'gamepad', 'nitro', 0);
    expect(b.bindings.gamepad.nitro).toEqual([0]);
    expect(b.bindings.gamepad.throttle).toEqual([7]);
    expect(b.displaced).toEqual({ action: 'throttle', codes: [7], swapped: true });
    expectSound(b.bindings);
  });

  it('o mesmo código de antes não muda nada', () => {
    const r = assignBinding(DEFAULT_BINDINGS, 'kb1', 'nitro', 'Space');
    expect(r).toMatchObject({ changed: false, rejected: false, displaced: null });
    expect(r.bindings).toEqual(DEFAULT_BINDINGS);
  });

  it('recusa tecla reservada, fora da lista ou do tipo errado', () => {
    const rejected = (device: BindDevice, action: BindAction, code: BindCode) => assignBinding(DEFAULT_BINDINGS, device, action, code).rejected;
    expect(rejected('kb1', 'nitro', 'Escape')).toBe(true);
    expect(rejected('kb1', 'nitro', 'F5')).toBe(true);
    expect(rejected('kb1', 'nitro', 'ControlLeft')).toBe(true);
    expect(rejected('kb1', 'nitro', 5)).toBe(true);
    expect(rejected('gamepad', 'nitro', 'KeyA')).toBe(true);
    expect(rejected('gamepad', 'nitro', 9)).toBe(true);
    expect(rejected('gamepad', 'nitro', 16)).toBe(true);
    expect(rejected('gamepad', 'pause', 9)).toBe(false);
    expect(rejected('kb2', 'pause', 'Escape')).toBe(false);
  });

  it('a troca nunca entrega Esc a quem não é pausa', () => {
    const b1 = assign(DEFAULT_BINDINGS, 'kb1', 'nitro', 'KeyP');
    const r = assignBinding(b1, 'kb1', 'pause', 'KeyP');
    expect(r.bindings.kb1.pause).toEqual(['KeyP']);
    expect(r.bindings.kb1.nitro).toEqual(['Space']);
    expect(r.displaced).toEqual({ action: 'nitro', codes: ['Space'], swapped: false });
    expectSound(r.bindings);
  });

  it('qualquer sequência de trocas mantém todas as ações com código e nenhum repetido', () => {
    const keyPool = ['ArrowUp', 'ArrowDown', 'Space', 'KeyM', 'KeyN', 'KeyW', 'KeyR', 'Escape', 'Enter', 'ShiftLeft', 'F5'];
    let b = defaultBindings();
    let seed = 12345;
    const next = (n: number) => { seed = (seed * 1103515245 + 12345) % 2147483648; return seed % n; };
    for (let i = 0; i < 600; i++) {
      const device = BIND_DEVICES[next(3)];
      const action = BIND_ACTIONS[next(BIND_ACTIONS.length)];
      const code: BindCode = device === 'gamepad' ? next(18) : keyPool[next(keyPool.length)];
      b = assignBinding(b, device, action, code).bindings;
      expectSound(b);
    }
    expect(sanitizeBindings(b)).toEqual(b);
  });
});

describe('restaurar padrão', () => {
  it('volta só o dispositivo pedido', () => {
    let b = assign(DEFAULT_BINDINGS, 'kb1', 'nitro', 'KeyR');
    b = assign(b, 'kb2', 'nitro', 'KeyG');
    b = assign(b, 'gamepad', 'nitro', 10);
    expect(isDefaultDevice(b, 'kb1')).toBe(false);
    const r = restoreDefaults(b, 'kb1');
    expect(r.kb1).toEqual(DEFAULT_BINDINGS.kb1);
    expect(isDefaultDevice(r, 'kb1')).toBe(true);
    expect(r.kb2.nitro).toEqual(['KeyG']);
    expect(r.gamepad.nitro).toEqual([10]);
    expect(b.kb1.nitro).toEqual(['KeyR']);
    const all = BIND_DEVICES.reduce((acc, d) => restoreDefaults(acc, d), b);
    expect(all).toEqual(DEFAULT_BINDINGS);
  });
});

// ───────────────────────────── Mapeamento com bindings ─────────────────────────────

describe('mapKeyboard / mapGamepad com bindings', () => {
  it('teclado: a tecla nova pilota e a antiga só navega', () => {
    let b = assign(DEFAULT_BINDINGS, 'kb1', 'throttle', 'KeyR');
    b = assign(b, 'kb1', 'left', 'KeyJ');
    expect(mapKeyboard(keys('KeyR'), 'kb1', b)).toMatchObject({ throttle: true, up: false });
    expect(mapKeyboard(keys('ArrowUp'), 'kb1', b)).toMatchObject({ throttle: false, up: true });
    expect(mapKeyboard(keys('KeyJ'), 'kb1', b)).toMatchObject({ steer: -1, left: false });
    expect(mapKeyboard(keys('ArrowLeft'), 'kb1', b)).toMatchObject({ steer: 0, left: true });
    // O teclado 2 não lê os bindings do teclado 1.
    expect(mapKeyboard(keys('KeyR', 'KeyJ'), 'kb2', b)).toMatchObject({ throttle: false, steer: 0 });
  });

  it('teclado: confirmar e voltar são fixos; Esc sempre pausa', () => {
    const b = assign(assign(DEFAULT_BINDINGS, 'kb1', 'nitro', 'KeyR'), 'kb1', 'pause', 'KeyP');
    expect(mapKeyboard(keys('Space'), 'kb1', b)).toMatchObject({ nitro: false, confirm: true });
    expect(mapKeyboard(keys('KeyR'), 'kb1', b)).toMatchObject({ nitro: true, confirm: false });
    expect(mapKeyboard(keys('KeyP'), 'kb1', b)).toMatchObject({ pause: true, back: false });
    expect(mapKeyboard(keys('Escape'), 'kb1', b)).toMatchObject({ pause: true, back: true });
    expect(mapKeyboard(keys('Escape'), 'kb2', b)).toMatchObject({ pause: true, back: true });
  });

  it('gamepad: botões trocados pilotam; A, B, d-pad e Start continuam navegando', () => {
    let b = assign(DEFAULT_BINDINGS, 'gamepad', 'nitro', 0);
    b = assign(b, 'gamepad', 'left', 4);
    b = assign(b, 'gamepad', 'pause', 8);
    expect(b.gamepad.throttle).toEqual([7]);
    expect(mapGamepad(buttons(0), [], b)).toMatchObject({ nitro: true, throttle: false, confirm: true });
    expect(mapGamepad(buttons(7), [], b)).toMatchObject({ throttle: true, nitro: false });
    expect(mapGamepad(buttons(4), [], b)).toMatchObject({ steer: -1, left: false, gearDown: false });
    expect(mapGamepad(buttons(14), [], b)).toMatchObject({ steer: 0, left: true });
    expect(mapGamepad(buttons(), [-1, 0], b)).toMatchObject({ steer: -1, left: true });
    expect(mapGamepad(buttons(8), [], b)).toMatchObject({ pause: true, start: false });
    expect(mapGamepad(buttons(9), [], b)).toMatchObject({ pause: true, start: true });
    expect(mapGamepad(buttons(1), [], b)).toMatchObject({ back: true });
  });

  it('as teclas que recebem preventDefault seguem os bindings', () => {
    const b = assign(DEFAULT_BINDINGS, 'kb1', 'nitro', 'KeyR');
    const used = usedKeyCodes(b);
    expect(used.has('KeyR')).toBe(true);
    for (const nav of ['ArrowUp', 'Enter', 'Space', 'Escape', 'Backspace', 'KeyW', 'KeyF']) expect(used.has(nav), nav).toBe(true);
    expect(used.has('KeyZ')).toBe(false);
    expect(USED_KEY_CODES.has('KeyR')).toBe(false);
  });
});

// ───────────────────────────── Provedor com janela falsa ─────────────────────────────

interface FakeKey { code: string; repeat: boolean; ctrlKey: boolean; metaKey: boolean; altKey: boolean; target: null; preventDefault: () => void }
interface FakePad {
  index: number; id: string; connected: boolean; axes: number[];
  buttons: Array<{ pressed: boolean; value: number }>;
  vibrationActuator?: { playEffect: (type: string, params: Record<string, number>) => unknown };
}

function fakeWindow(pads: Array<FakePad | null> = []) {
  const listeners = new Map<string, Array<(e: unknown) => void>>();
  const target = {
    addEventListener: (type: string, fn: (e: unknown) => void) => { listeners.set(type, [...(listeners.get(type) ?? []), fn]); },
    removeEventListener: (type: string, fn: (e: unknown) => void) => { listeners.set(type, (listeners.get(type) ?? []).filter((f) => f !== fn)); },
    navigator: { getGamepads: () => pads },
  };
  const key = (type: 'keydown' | 'keyup', code: string): FakeKey => {
    const e: FakeKey = { code, repeat: false, ctrlKey: false, metaKey: false, altKey: false, target: null, preventDefault: vi.fn() };
    for (const fn of listeners.get(type) ?? []) fn(e);
    return e;
  };
  return { target: target as unknown as Window, down: (code: string) => key('keydown', code), up: (code: string) => key('keyup', code) };
}

function fakePad(index: number, pressed: number[] = [], actuator?: FakePad['vibrationActuator']): FakePad {
  return {
    index, id: 'Xbox Wireless Controller (STANDARD GAMEPAD Vendor: 045e Product: 0b13)', connected: true, axes: [0, 0, 0, 0],
    buttons: Array.from({ length: 17 }, (_, i) => ({ pressed: pressed.includes(i), value: pressed.includes(i) ? 1 : 0 })),
    vibrationActuator: actuator,
  };
}

describe('createInput com bindings', () => {
  it('os bindings valem ao vivo: trocar nas opções muda a leitura no próximo quadro', () => {
    const w = fakeWindow();
    let bindings = defaultBindings();
    const input = createInput(w.target, { bindings: () => bindings });
    input.bindSeat(0, 'kb1');
    w.down('KeyR');
    input.poll();
    expect(input.readSeat(0).throttle).toBe(false);
    bindings = assign(bindings, 'kb1', 'throttle', 'KeyR');
    input.poll();
    expect(input.readSeat(0).throttle).toBe(true);
    expect(input.peek('kb1')).toMatchObject({ throttle: true, steer: 0, buttons: [] });
    input.dispose();
  });

  it('a navegação de menu ignora os bindings (teclado e gamepad)', () => {
    const pad = fakePad(0);
    const w = fakeWindow([pad]);
    let bindings = assign(DEFAULT_BINDINGS, 'kb1', 'brake', 'KeyR');
    bindings = assign(bindings, 'gamepad', 'nitro', 0);
    const input = createInput(w.target, { bindings: () => bindings });
    w.down('KeyR');
    input.poll();
    expect(input.menuNav()).toMatchObject({ down: false, up: false });
    w.up('KeyR');
    w.down('ArrowDown');
    input.poll();
    expect(input.menuNav()).toMatchObject({ down: true, device: 'kb1' });
    expect(input.peek('kb1')?.brake).toBe(false);
    w.up('ArrowDown');
    input.poll();
    pad.buttons[0] = { pressed: true, value: 1 };
    input.poll();
    expect(input.menuNav()).toMatchObject({ confirm: true, device: 'gp0' });
    expect(input.joinPressed()).toBe('gp0');
    expect(input.peek('gp0')).toMatchObject({ nitro: true, throttle: false, buttons: [0] });
    input.dispose();
  });

  it('teclado sem assento só pausa pelo Esc: a pausa escolhida dele não pausa a corrida do outro', () => {
    const w = fakeWindow([fakePad(0)]);
    // Teclado 2 com a pausa no Espaço, que é o nitro do teclado 1; teclado 1 com a pausa no P.
    const bindings = assign(assign(DEFAULT_BINDINGS, 'kb2', 'pause', 'Space'), 'kb1', 'pause', 'KeyP');
    const input = createInput(w.target, { bindings: () => bindings });
    const tap = (code: string): number => {
      w.down(code);
      input.poll();
      const seat = input.pausePressed();
      w.up(code);
      input.poll();
      return seat;
    };
    // Só o teclado 1 em uso: o Espaço é nitro, não pausa.
    input.bindSeat(0, 'kb1');
    input.poll();
    w.down('Space');
    input.poll();
    expect(input.readSeat(0).nitro).toBe(true);
    expect(input.pausePressed()).toBe(-1);
    w.up('Space');
    input.poll();
    expect(tap('KeyP')).toBe(0);
    // Com o teclado 2 em uso, o Espaço pausa também (é o que o aviso de conflito diz).
    input.bindSeat(1, 'kb2');
    expect(tap('Space')).toBe(1);
    // Todos nos controles: o Esc continua pausando; a pausa escolhida de um teclado sem assento, não.
    input.unbindSeat(0);
    input.unbindSeat(1);
    input.bindSeat(0, 'gp0');
    expect(tap('KeyP')).toBe(-1);
    expect(tap('Space')).toBe(-1);
    expect(tap('Escape')).toBe(0);
    input.dispose();
  });

  it('preventDefault só nas teclas em uso (navegação fixa + bindings atuais)', () => {
    const w = fakeWindow();
    const bindings = assign(DEFAULT_BINDINGS, 'kb1', 'nitro', 'KeyR');
    const input = createInput(w.target, { bindings: () => bindings });
    expect(w.down('KeyR').preventDefault).toHaveBeenCalled();
    expect(w.down('ArrowUp').preventDefault).toHaveBeenCalled();
    expect(w.down('KeyZ').preventDefault).not.toHaveBeenCalled();
    input.dispose();
  });
});

describe('vibração', () => {
  it('rumble é no-op sem gamepad: teclado, assento vazio, sem Gamepad API', () => {
    const w = fakeWindow();
    const input = createInput(w.target);
    input.bindSeat(0, 'kb1');
    expect(() => { input.rumble(0, 1, 200); input.rumble(3, 1, 200); input.rumble(-1, 1, 200); }).not.toThrow();
    const bare = createInput({ addEventListener: () => undefined, removeEventListener: () => undefined } as unknown as Window);
    bare.bindSeat(0, 'gp0');
    expect(() => bare.rumble(0, 1, 100)).not.toThrow();
    input.dispose();
    bare.dispose();
  });

  it('chama dual-rumble no gamepad do assento, respeitando a opção de vibração', () => {
    const playEffect = vi.fn(() => Promise.resolve('complete'));
    const w = fakeWindow([null, fakePad(1, [], { playEffect })]);
    let on = true;
    const input = createInput(w.target, { vibration: () => on });
    input.poll();
    input.bindSeat(2, 'gp1');
    input.rumble(2, 0.8, 200);
    expect(playEffect).toHaveBeenCalledTimes(1);
    expect(playEffect).toHaveBeenCalledWith('dual-rumble', expect.objectContaining({ duration: 200, startDelay: 0, strongMagnitude: 0.8 }));
    on = false;
    input.rumble(2, 1, 300);
    expect(playEffect).toHaveBeenCalledTimes(1);
    input.dispose();
  });

  it('assento de teclado ou vazio não vibra, nem com um controle com motor conectado', () => {
    const playEffect = vi.fn(() => Promise.resolve('complete'));
    const w = fakeWindow([fakePad(0, [], { playEffect })]);
    const input = createInput(w.target);
    input.poll();
    input.bindSeat(0, 'kb1');
    input.bindSeat(1, 'kb2');
    for (const seat of [0, 1, 2, 3, -1, 4]) input.rumble(seat, 1, 200);
    expect(playEffect).not.toHaveBeenCalled();
    input.bindSeat(2, 'gp0');
    input.rumble(2, 1, 200);
    expect(playEffect).toHaveBeenCalledTimes(1);
    input.dispose();
  });

  it('no provedor, um pedido mais fraco durante um tremor mais forte não chama playEffect', () => {
    const playEffect = vi.fn(() => Promise.resolve('complete'));
    const w = fakeWindow([fakePad(0, [], { playEffect })]);
    const clock = vi.spyOn(performance, 'now');
    const input = createInput(w.target);
    input.poll();
    input.bindSeat(0, 'gp0');
    clock.mockReturnValue(1000);
    input.rumble(0, 1, 300);
    clock.mockReturnValue(1100);
    input.rumble(0, 0.16, 160); // grama no meio de uma batida no cenário: ignorado
    expect(playEffect).toHaveBeenCalledTimes(1);
    input.rumble(0, 1, 200); // outra batida tão forte quanto: toca
    expect(playEffect).toHaveBeenCalledTimes(2);
    clock.mockReturnValue(1301); // a batida acabou (1100 + 200): a grama volta a tocar
    input.rumble(0, 0.16, 160);
    expect(playEffect).toHaveBeenCalledTimes(3);
    expect(playEffect).toHaveBeenLastCalledWith('dual-rumble', expect.objectContaining({ duration: 160, strongMagnitude: 0.16 }));
    clock.mockRestore();
    input.dispose();
  });

  it('gamepad sem vibrationActuator ou com playEffect que falha não vira erro', async () => {
    const w = fakeWindow([fakePad(0), fakePad(1, [], { playEffect: () => Promise.reject(new Error('sem motor')) }), fakePad(2, [], { playEffect: () => { throw new Error('x'); } })]);
    const input = createInput(w.target);
    input.poll();
    input.bindSeat(0, 'gp0');
    input.bindSeat(1, 'gp1');
    input.bindSeat(2, 'gp2');
    expect(() => { input.rumble(0, 1, 100); input.rumble(1, 1, 100); input.rumble(2, 1, 100); }).not.toThrow();
    await Promise.resolve();
    input.dispose();
  });

  it('um tremor mais fraco não corta um mais forte em andamento', () => {
    expect(nextRumble({ until: 0, strength: 0 }, 1000, 1, 300)).toEqual({ until: 1300, strength: 1 });
    expect(nextRumble({ until: 1300, strength: 1 }, 1100, 0.2, 150)).toBeNull();
    expect(nextRumble({ until: 1300, strength: 1 }, 1300, 0.2, 150)).toEqual({ until: 1450, strength: 0.2 });
    expect(nextRumble({ until: 1300, strength: 0.2 }, 1100, 0.2, 150)).toEqual({ until: 1250, strength: 0.2 });
    expect(nextRumble({ until: 0, strength: 0 }, 1000, 0, 300)).toBeNull();
    expect(nextRumble({ until: 0, strength: 0 }, 1000, 0.5, 0)).toBeNull();
    const m = rumbleMagnitudes(0.5);
    expect(m.strong).toBe(0.5);
    expect(m.weak).toBeGreaterThan(0);
    expect(rumbleMagnitudes(7)).toEqual({ strong: 1, weak: expect.any(Number) as number });
    expect(rumbleMagnitudes(7).weak).toBeLessThanOrEqual(1);
  });
});

function car(id: number, seat: number, over: Partial<CarState> = {}): CarState {
  return {
    id, seat, name: `c${id}`, teamId: 0, carId: 'falcao', z: 0, x: 0, speed: 3000, gear: 3, fuel: 1, nitroLeft: 3, nitroTicks: 0,
    lap: 1, lapTicks: [], lapStartTick: 0, finished: false, finishTick: 0, position: 1, progress: 0, inPit: false,
    collisionCooldown: 0, towCooldown: 0, skidTicks: 0, steerPose: 0, ai: null, stats: effectiveStats(carDef('falcao')), ...over,
  };
}
function race(cars: CarState[], events: SimEvent[], tick = 500): RaceState {
  return { tick, phase: 'racing', cars, events } as unknown as RaceState;
}

describe('rumbleCues (o que a sessão manda vibrar)', () => {
  it('largada vibra todos os humanos; IA nunca vibra', () => {
    const cues = rumbleCues(race([car(0, -1), car(1, 0), car(2, 2)], [{ type: 'go' }]), newRumbleMemory());
    expect(cues.map((c) => c.seat).sort()).toEqual([0, 2]);
    expect(rumbleCues(race([car(0, -1)], [{ type: 'nitro', carId: 0 }, { type: 'crash', carId: 0, sprite: 'palm' }]), newRumbleMemory())).toEqual([]);
  });

  it('batida entre carros: os dois humanos sentem, com força pela intensidade', () => {
    const cars = [car(0, 0), car(1, 1), car(2, -1)];
    const weak = rumbleCues(race(cars, [{ type: 'collision', carId: 0, otherId: 1, strength: 0.1 }]), newRumbleMemory());
    const strong = rumbleCues(race(cars, [{ type: 'collision', carId: 0, otherId: 1, strength: 1 }]), newRumbleMemory());
    expect(weak.map((c) => c.seat)).toEqual([0, 1]);
    expect(strong[0].strength).toBeGreaterThan(weak[0].strength);
    expect(strong[0].ms).toBeGreaterThanOrEqual(weak[0].ms);
    expect(rumbleCues(race(cars, [{ type: 'collision', carId: 2, otherId: 1, strength: 0.5 }]), newRumbleMemory()).map((c) => c.seat)).toEqual([1]);
  });

  it('batida no cenário é forte; nitro é curto', () => {
    const [crash] = rumbleCues(race([car(0, 0)], [{ type: 'crash', carId: 0, sprite: 'palm' }]), newRumbleMemory());
    const [nitro] = rumbleCues(race([car(0, 0)], [{ type: 'nitro', carId: 0 }]), newRumbleMemory());
    expect(crash.strength).toBeGreaterThan(0.8);
    expect(nitro.ms).toBeLessThan(crash.ms);
    expect(nitro.strength).toBeLessThan(crash.strength);
  });

  it('grama: pulso fraco com limite de frequência, só andando e só na corrida', () => {
    const mem = newRumbleMemory();
    const onGrass = [car(0, 0, { skidTicks: 6 })];
    const first = rumbleCues(race(onGrass, [], 100), mem);
    expect(first).toHaveLength(1);
    expect(first[0].strength).toBeLessThan(0.3);
    expect(rumbleCues(race(onGrass, [], 101), mem)).toEqual([]);
    // Um segundo de grama a 60 Hz: um pulso exatamente a cada `everyTicks` (≈ 7 por segundo, sem
    // inundar a API), e cada pulso dura o intervalo inteiro, então o tremor é contínuo.
    const pulses: number[] = [];
    const second = newRumbleMemory();
    for (let tick = 100; tick < 160; tick++) if (rumbleCues(race(onGrass, [], tick), second).length) pulses.push(tick);
    const gaps = pulses.slice(1).map((tick, i) => tick - pulses[i]);
    expect(gaps.length).toBeGreaterThan(0);
    expect(gaps).toEqual(gaps.map(() => RUMBLE_GRASS.everyTicks));
    expect(pulses.length).toBeGreaterThanOrEqual(6);
    expect(pulses.length).toBeLessThanOrEqual(7);
    expect(RUMBLE_GRASS.ms).toBeGreaterThanOrEqual((RUMBLE_GRASS.everyTicks * 1000) / 60);
    // Corrida nova (tick recomeça): não espera o intervalo da anterior.
    expect(rumbleCues(race(onGrass, [], 3), mem)).toHaveLength(1);
    expect(rumbleCues(race([car(0, 0, { skidTicks: 6, speed: 50 })], [], 900), newRumbleMemory())).toEqual([]);
    expect(rumbleCues(race([car(0, 0, { skidTicks: 0 })], [], 900), newRumbleMemory())).toEqual([]);
    const counting = { ...race(onGrass, [], 900), phase: 'countdown' } as RaceState;
    expect(rumbleCues(counting, newRumbleMemory())).toEqual([]);
  });
});

// ───────────────────────────── Captura e nomes ─────────────────────────────

describe('captura da tecla nova', () => {
  it('teclado: aceita tecla válida, recusa reservada/proibida, Esc cancela', () => {
    const c = startCapture('nitro', 'kb1');
    expect(captureKey(c, 'KeyR')).toEqual({ kind: 'accept', code: 'KeyR' });
    expect(captureKey(c, 'F5')).toEqual({ kind: 'reject', code: 'F5' });
    expect(captureKey(c, 'Escape')).toEqual({ kind: 'cancel', reason: 'escape' });
    expect(captureKey(startCapture('nitro', 'gamepad'), 'KeyR')).toEqual({ kind: 'wait' });
  });

  it('gamepad: o botão que abriu a captura só conta depois de solto; Start cancela', () => {
    const c = startCapture('nitro', 'gamepad', { gp0: [0] });
    expect(captureButtons(c, 'gp0', [0])).toEqual({ kind: 'wait' });
    expect(captureButtons(c, 'gp0', [])).toEqual({ kind: 'wait' });
    expect(captureButtons(c, 'gp0', [0])).toEqual({ kind: 'accept', code: 0 });
    expect(captureButtons(startCapture('nitro', 'gamepad'), 'gp1', [7])).toEqual({ kind: 'accept', code: 7 });
    expect(captureButtons(startCapture('nitro', 'gamepad'), 'gp1', [16])).toEqual({ kind: 'reject', code: 16 });
    expect(captureButtons(startCapture('nitro', 'kb2'), 'gp1', [9])).toEqual({ kind: 'cancel', reason: 'start' });
    expect(captureButtons(startCapture('nitro', 'kb2'), 'gp1', [3])).toEqual({ kind: 'wait' });
  });

  it(`sem nada em ${CAPTURE_SECONDS} s, cancela sozinha`, () => {
    const c = startCapture('brake', 'kb1');
    expect(captureTick(c, 2)).toEqual({ kind: 'wait' });
    expect(captureTick(c, 2.9)).toEqual({ kind: 'wait' });
    expect(captureTick(c, 0.2)).toEqual({ kind: 'cancel', reason: 'timeout' });
  });
});

describe('nomes de teclas e botões', () => {
  it('teclas pelo código físico, com o layout do sistema quando houver', () => {
    setLanguage('pt');
    expect(keyLabel('KeyW')).toBe('W');
    expect(keyLabel('Digit7')).toBe('7');
    expect(keyLabel('ArrowLeft')).toBe('←');
    expect(keyLabel('Space')).toBe('Espaço');
    expect(keyLabel('Numpad4')).toBe('Num 4');
    expect(keyLabel('Escape')).toBe('Esc');
    const azerty = new Map([['KeyQ', 'a'], ['Semicolon', 'ç']]);
    expect(keyLabel('KeyQ', azerty)).toBe('A');
    expect(keyLabel('Semicolon', azerty)).toBe('Ç');
    expect(keyLabel('Space', azerty)).toBe('Espaço');
    setLanguage('en');
    expect(keyLabel('Space')).toBe('Space');
    setLanguage('pt');
  });

  it('botões no estilo do controle conectado', () => {
    expect(buttonLabel(0)).toBe('A');
    expect(buttonLabel(0, 'playstation')).toBe('✕');
    expect(buttonLabel(7, 'playstation')).toBe('R2');
    expect(padStyleOf('Xbox Wireless Controller')).toBe('xbox');
    expect(padStyleOf('DualSense Wireless Controller')).toBe('playstation');
    expect(padStyleOf('Wireless Controller')).toBe('playstation');
    expect(padStyleOf('USB Gamepad')).toBe('xbox');
    expect(codesLabel(DEFAULT_BINDINGS.gamepad.brake)).toBe('X / B / LT');
    expect(codesLabel(DEFAULT_BINDINGS.kb1.nitro)).toBe('Espaço');
  });

  it('aviso de recusa concorda com "tecla" (feminino) e "botão" (masculino)', () => {
    setLanguage('pt');
    expect(rejectedText('F7')).toBe('F7 não pode ser usada — escolha outra.');
    expect(rejectedText(16)).toBe('Home não pode ser usado — escolha outro.');
    expect(rejectedText(17)).toBe('Botão 17 não pode ser usado — escolha outro.');
    expect(rejectedText(16, 'playstation')).toBe('PS não pode ser usado — escolha outro.');
    setLanguage('en');
    expect(rejectedText('F7')).toBe('F7 can’t be used — pick another one.');
    expect(rejectedText(17)).toBe('Button 17 can’t be used — pick another one.');
    setLanguage('pt');
  });

  it('colunas com nome curto (o mesmo dos avisos) e o nome completo na dica', () => {
    setLanguage('pt');
    expect(BIND_DEVICES.map(deviceLabel)).toEqual(['Teclado 1', 'Teclado 2', 'Controles']);
    expect(deviceTitle('kb1')).toContain('Teclado (setas)');
    expect(deviceTitle('gamepad')).toContain('analógico');
    setLanguage('en');
    expect(deviceLabel('kb2')).toBe('Keyboard 2');
    setLanguage('pt');
  });
});
