// Cliente e sessão online contra um socket falso: nada que vem da rede é confiável. Mensagem fora
// do formato é descartada e contada no NetClient; mensagem bem formada mas de quem não pode
// mandá-la (entrada de assento alheio, largada ou tomada pela IA de quem não é o anfitrião) é
// descartada e contada na sessão.
import { afterEach, describe, expect, it } from 'vitest';
import { createRace } from '../src/core/sim/race';
import { getTrack } from '../src/core/track';
import type { RaceConfig, RaceState } from '../src/core/types';
import { DEFAULT_SAVE, DEFAULT_SETTINGS, type DeviceId, type InputProvider, type MenuNav, type RaceDriver, type SaveData, type Settings } from '../src/game/contracts';
import { OnlineController, type OnlineHost } from '../src/game/online-session';
import { NetClient } from '../src/net/client';
import { TAKEOVER_BIT, type StartConfig } from '../src/net/protocol';

/** O mínimo de um WebSocket que o NetClient usa; o teste faz o papel do servidor. */
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
  push(msg: unknown): void { this.onmessage?.({ data: typeof msg === 'string' ? msg : JSON.stringify(msg) }); }
  drop(code = 1006): void { this.readyState = 3; this.onclose?.({ code }); }
}
const asWebSocket = (s: FakeSocket) => s as unknown as WebSocket;

const RULES = { cars: ['falcao', 'trovao'], tracks: ['copacabana'] };

describe('NetClient', () => {
  it('mensagem fora do formato é descartada e contada; a válida chega', () => {
    const sock = new FakeSocket();
    const got: string[] = [];
    const client = new NetClient('ws://x', RULES, { onOpen() {}, onMessage: (m) => got.push(m.t), onClose() {} }, () => asWebSocket(sock));
    sock.open();
    sock.push('isto não é json');
    sock.push({ t: 'desconhecido' });
    sock.push({ t: 'i', from: 1, d: [5, 9, 1, 0] }); // assento 9 não existe
    sock.push({ t: 'i', from: 1, d: [5, 1, 64, 0] }); // bit que não é de jogador
    sock.push({ t: 'pong', n: -1 });
    sock.onmessage?.({ data: new ArrayBuffer(4) }); // binário
    sock.push({ t: 'pong', n: 7 });
    sock.push({ t: 'i', from: 1, d: [5, 1, 1, -127] });
    expect(got).toEqual(['pong', 'i']);
    expect(client.stats).toMatchObject({ received: 8, invalid: 6 });
  });

  it('endereço que o WebSocket recusa vira falha de conexão (sem exceção para quem chamou)', async () => {
    const closes: Array<{ code: number; wasOpen: boolean }> = [];
    const client = new NetClient('lixo', RULES, { onOpen() {}, onMessage() {}, onClose: (i) => closes.push(i) }, () => { throw new Error('SyntaxError'); });
    expect(client.isOpen).toBe(false);
    expect(client.send({ t: 'ping', n: 1 })).toBe(false);
    await Promise.resolve();
    expect(closes).toEqual([{ code: 1006, wasOpen: false }]);
  });

  it('queda avisa uma vez; fechar por conta própria não avisa', () => {
    const a = new FakeSocket();
    const closes: boolean[] = [];
    new NetClient('ws://x', RULES, { onOpen() {}, onMessage() {}, onClose: (i) => closes.push(i.wasOpen) }, () => asWebSocket(a));
    a.open();
    a.drop();
    a.drop();
    const b = new FakeSocket();
    const own = new NetClient('ws://x', RULES, { onOpen() {}, onMessage() {}, onClose: (i) => closes.push(i.wasOpen) }, () => asWebSocket(b));
    b.open();
    own.close();
    b.drop();
    expect(closes).toEqual([true]);
  });
});

// ───────────────────────────── Sessão ─────────────────────────────

function fakeInput(): InputProvider {
  const seats: Array<DeviceId | null> = [null, null, null, null];
  const nav: MenuNav = { up: false, down: false, left: false, right: false, confirm: false, back: false, start: false, device: null };
  return {
    poll() {}, readSeat: () => ({ steer: 0, throttle: false, brake: false, nitro: false, gearUp: false, gearDown: false }),
    bindSeat(seat, device) { seats[seat] = device; }, unbindSeat(seat) { seats[seat] = null; }, seatDevice: (s) => seats[s] ?? null,
    devices: () => [], joinPressed: () => null, leavePressed: () => null, menuNav: () => nav, pausePressed: () => -1, dispose() {},
  };
}

class Host implements OnlineHost {
  readonly settings: Settings = { ...DEFAULT_SETTINGS, assists: { ...DEFAULT_SETTINGS.assists } };
  readonly save: SaveData = { ...DEFAULT_SAVE, seatNames: ['Ana', 'P2', 'P3', 'P4'], seatCars: [...DEFAULT_SAVE.seatCars] };
  readonly input = fakeInput();
  state: RaceState | null = null;
  starts = 0;
  startRace(config: RaceConfig, _localSeats: number[], _driver: RaceDriver, state?: RaceState): void { this.starts++; this.state = state ?? createRace(config, getTrack(config.trackId)); }
  raceState() { return this.state; }
  clearRace() { this.state = null; }
  showScreen() {}
  hideScreen() {}
  menuOpen() { return false; }
  exitToMain() {}
  persistSettings() {}
}

const TOKEN = 'a'.repeat(24);
const player = (name: string, car = 'falcao') => ({ name, car });
function roomMsg(host: number, started = false) {
  return {
    t: 'room', room: {
      code: 'KQXTR', host, started, settings: null, clients: [
        { id: 0, seats: 1, connected: true, info: { players: [player('Ana')], ready: false } },
        { id: 1, seats: 1, connected: true, info: { players: [player('Bia', 'trovao')], ready: true } },
      ],
    },
  };
}
function startCfg(): StartConfig {
  return {
    trackId: 'copacabana', laps: 1, versus: false, difficulty: 'amador', totalCars: 4, manualGear: false,
    assists: { sharedNitro: true, tow: true, teamDraft: true, catchup: true }, delay: 3, seed: 42,
    seats: [{ seat: 0, client: 0, name: 'Ana', car: 'falcao' }, { seat: 1, client: 1, name: 'Bia', car: 'trovao' }],
  };
}

describe('sessão online: remetente conferido', () => {
  let online: OnlineController | null = null;
  afterEach(() => { online?.leave(false); online = null; });

  function setup(myId: number): { sock: FakeSocket; host: Host; ctl: OnlineController } {
    const sock = new FakeSocket();
    const host = new Host();
    const ctl = new OnlineController(host, { socket: () => asWebSocket(sock), pingMs: 60_000 });
    online = ctl;
    if (myId === 0) ctl.create('kb1'); else ctl.join('KQXTR', 'kb1');
    sock.open();
    expect(sock.sent[0]).toMatchObject({ t: myId === 0 ? 'create' : 'join', v: 1 });
    sock.push({ t: 'welcome', room: 'KQXTR', id: myId, token: TOKEN, rejoined: false });
    sock.push(roomMsg(0));
    expect(ctl.phase).toBe('lobby');
    return { sock, host, ctl };
  }

  it('largada de quem não é o anfitrião é ignorada; a do anfitrião começa a corrida', () => {
    const { sock, host, ctl } = setup(1);
    sock.push({ t: 'start', from: 1, cfg: startCfg() });
    expect(ctl.phase).toBe('lobby');
    expect(host.starts).toBe(0);
    expect(ctl.dropped).toBe(1);
    sock.push({ t: 'start', from: 0, cfg: startCfg() });
    expect(ctl.phase).toBe('racing');
    expect(host.starts).toBe(1);
    expect([...ctl.localSeats]).toEqual([1]);
    expect(host.input.seatDevice(1)).toBe('kb1');
  });

  it('entrada de assento alheio e tomada pela IA fora do anfitrião são descartadas', () => {
    const { sock, ctl } = setup(0);
    sock.push({ t: 'start', from: 0, cfg: startCfg() });
    expect(ctl.phase).toBe('racing');
    // O cliente 1 tenta dirigir o carro do assento 0 (meu) e pôr a IA no próprio assento.
    sock.push({ t: 'i', from: 1, d: [0, 0, 1, 0, 1, 1, 1, 0] });
    sock.push({ t: 'i', from: 1, d: [50, 1, TAKEOVER_BIT, 0] });
    const info = ctl.debugInfo() as { dropped: number; stats: { received: number } };
    expect(info.dropped).toBe(2);
    expect(info.stats.received).toBe(1); // só a entrada do assento 1 para o tick 1
  });

  it('snapshot sem ter pedido (ou de quem não é o anfitrião) não substitui a corrida', () => {
    const { sock, host, ctl } = setup(1);
    sock.push({ t: 'start', from: 0, cfg: startCfg() });
    const before = host.state;
    sock.push({ t: 'snap', from: 0, snap: { tick: 0, state: '{}', start: startCfg(), inputs: [], ai: [] } });
    expect(host.state).toBe(before);
    expect(host.starts).toBe(1);
    expect(ctl.dropped).toBe(1);
  });
});
