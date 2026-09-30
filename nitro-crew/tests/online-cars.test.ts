// Carro no online: cada computador escolhe entre os mesmos carros que o lobby local oferece — os livres
// e os comprados numa carreira deste computador. Antes, a sala percorria os 14 (CONTENT_RULES) e dava de
// graça, no online, os 7 carros à venda da carreira. O carro que chega de outro computador continua
// valendo qualquer um do jogo (lá ele pode ter sido comprado).
import { afterEach, describe, expect, it } from 'vitest';
import { CARS } from '../src/core/data/cars';
import { createRace } from '../src/core/sim/race';
import { getTrack } from '../src/core/track';
import type { RaceConfig, RaceState } from '../src/core/types';
import { DEFAULT_SAVE, DEFAULT_SETTINGS, type DeviceId, type InputProvider, type MenuNav, type RaceDriver, type SaveData, type Settings } from '../src/game/contracts';
import { CONTENT_RULES, OnlineController, type OnlineHost } from '../src/game/online-session';

class FakeSocket {
  readyState = 0;
  sent: Array<Record<string, unknown>> = [];
  onopen: (() => void) | null = null;
  onmessage: ((ev: { data: unknown }) => void) | null = null;
  onclose: ((ev: { code: number }) => void) | null = null;
  onerror: (() => void) | null = null;
  send(data: string): void { this.sent.push(JSON.parse(data) as Record<string, unknown>); }
  close(): void { this.readyState = 3; }
  open(): void { this.readyState = 1; this.onopen?.(); }
  push(msg: unknown): void { this.onmessage?.({ data: JSON.stringify(msg) }); }
}

function fakeInput(): InputProvider {
  const seats: Array<DeviceId | null> = [null, null, null, null];
  const nav: MenuNav = { up: false, down: false, left: false, right: false, confirm: false, back: false, start: false, device: null };
  return {
    poll() {}, readSeat: () => ({ steer: 0, throttle: false, brake: false, nitro: false, gearUp: false, gearDown: false }),
    bindSeat(seat, device) { seats[seat] = device; }, unbindSeat(seat) { seats[seat] = null; }, seatDevice: (s) => seats[s] ?? null,
    devices: () => [], joinPressed: () => null, leavePressed: () => null, menuNav: () => nav, pausePressed: () => -1, dispose() {},
    peek: () => null, rumble() {},
  };
}

class Host implements OnlineHost {
  readonly settings: Settings = { ...DEFAULT_SETTINGS, assists: { ...DEFAULT_SETTINGS.assists }, seatAssists: [...DEFAULT_SETTINGS.seatAssists] };
  readonly save: SaveData;
  readonly input = fakeInput();
  state: RaceState | null = null;
  constructor(seatCars: string[], unlocked: string[]) {
    this.save = { ...DEFAULT_SAVE, seatNames: ['Ana', 'P2', 'P3', 'P4'], seatCars, carsUnlocked: unlocked };
  }
  startRace(config: RaceConfig, _localSeats: number[], _driver: RaceDriver, state?: RaceState): void { this.state = state ?? createRace(config, getTrack(config.trackId)); }
  raceState() { return this.state; }
  clearRace() { this.state = null; }
  showScreen() {}
  hideScreen() {}
  menuOpen() { return false; }
  exitToMain() {}
  persistSettings() {}
}

const TOKEN = 'a'.repeat(24);

describe('online: a escolha de carro', () => {
  let online: OnlineController | null = null;
  afterEach(() => { online?.leave(false); online = null; });

  function lobby(seatCars: string[], unlocked: string[]): OnlineController {
    const sock = new FakeSocket();
    const ctl = new OnlineController(new Host(seatCars, unlocked), { socket: () => sock as unknown as WebSocket, pingMs: 60_000 });
    online = ctl;
    ctl.create('kb1');
    sock.open();
    sock.push({ t: 'welcome', room: 'KQXTR', id: 0, token: TOKEN, rejoined: false });
    sock.push({ t: 'room', room: { code: 'KQXTR', host: 0, started: false, settings: null, clients: [{ id: 0, seats: 1, connected: true, info: { players: [{ name: 'Ana', car: 'falcao' }], ready: false } }] } });
    expect(ctl.phase).toBe('lobby');
    return ctl;
  }

  /** Os carros que ←→ percorre a partir do carro atual, uma volta inteira. */
  function cycle(ctl: OnlineController): string[] {
    const seen: string[] = [];
    for (let i = 0; i < CARS.length + 2; i++) { ctl.cycleCar(0, 1); if (!seen.includes(ctl.locals[0].car)) seen.push(ctl.locals[0].car); }
    return seen.sort();
  }

  it('percorre os livres e os comprados neste computador, não os carros à venda', () => {
    const free = CARS.filter((c) => c.price === 0).map((c) => c.id);
    expect(cycle(lobby(['falcao'], []))).toEqual(free.slice().sort());
    expect(cycle(lobby(['falcao'], ['iara', 'sucuri']))).toEqual([...free, 'iara', 'sucuri'].sort());
  });

  it('carro guardado no assento que não está liberado aqui cai no primeiro livre', () => {
    expect(lobby(['beijaflor'], []).locals[0].car).toBe('falcao');
    expect(lobby(['beijaflor'], ['beijaflor']).locals[0].car).toBe('beijaflor');
    expect(lobby(['saci'], []).locals[0].car).toBe('saci');
  });

  it('o carro que chega de outro computador pode ser qualquer um do jogo', () => {
    expect(CONTENT_RULES.cars).toEqual(CARS.map((c) => c.id));
  });
});
