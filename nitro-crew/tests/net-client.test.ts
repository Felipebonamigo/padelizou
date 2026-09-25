// Cliente e sessão online contra um socket falso: nada que vem da rede é confiável. Mensagem fora
// do formato é descartada e contada no NetClient; mensagem bem formada mas de quem não pode
// mandá-la (entrada de assento alheio, largada ou tomada pela IA de quem não é o anfitrião) é
// descartada e contada na sessão.
import { afterEach, describe, expect, it } from 'vitest';
import { serializeRace } from '../src/core/serialize';
import { createRace, stepRace } from '../src/core/sim/race';
import { getTrack } from '../src/core/track';
import type { PlayerInput, RaceConfig, RaceState } from '../src/core/types';
import { DEFAULT_SAVE, DEFAULT_SETTINGS, type DeviceId, type InputProvider, type MenuNav, type RaceDriver, type SaveData, type Settings } from '../src/game/contracts';
import { OnlineController, raceConfigFrom, type OnlineHost, type OnlineOptions } from '../src/game/online-session';
import { NetClient } from '../src/net/client';
import { decodeInput, TAKEOVER_BIT, type StartConfig } from '../src/net/protocol';

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

/** Imita a sessão: showScreen abre a tela online, startRace (beginRace) esconde qualquer menu. */
class Host implements OnlineHost {
  readonly settings: Settings = { ...DEFAULT_SETTINGS, assists: { ...DEFAULT_SETTINGS.assists } };
  readonly save: SaveData = { ...DEFAULT_SAVE, seatNames: ['Ana', 'P2', 'P3', 'P4'], seatCars: [...DEFAULT_SAVE.seatCars] };
  readonly input = fakeInput();
  state: RaceState | null = null;
  starts = 0;
  menu: string | null = null;
  startRace(config: RaceConfig, _localSeats: number[], _driver: RaceDriver, state?: RaceState): void {
    this.starts++;
    this.menu = null;
    this.state = state ?? createRace(config, getTrack(config.trackId));
  }
  raceState() { return this.state; }
  clearRace() { this.state = null; }
  showScreen() { this.menu = 'online'; }
  hideScreen() { this.menu = null; }
  menuOpen() { return this.menu !== null; }
  exitToMain() { this.menu = 'main'; }
  persistSettings() {}
}

const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));

const TOKEN = 'a'.repeat(24);
const player = (name: string, car = 'falcao') => ({ name, car });
function roomMsg(host: number, started = false, offline: number[] = []) {
  return {
    t: 'room', room: {
      code: 'KQXTR', host, started, settings: null, clients: [
        { id: 0, seats: 1, connected: !offline.includes(0), info: { players: [player('Ana')], ready: false } },
        { id: 1, seats: 1, connected: !offline.includes(1), info: { players: [player('Bia', 'trovao')], ready: true } },
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

describe('sessão online: volta depois de cair', () => {
  let online: OnlineController | null = null;
  afterEach(() => { online?.leave(false); online = null; });

  /** Convidado (id 1) na corrida que perdeu a conexão e acabou de voltar à sala (welcome com rejoined). */
  async function rejoined(opts: OnlineOptions = {}): Promise<{ sock: FakeSocket; host: Host; ctl: OnlineController }> {
    const socks: FakeSocket[] = [];
    const host = new Host();
    const ctl = new OnlineController(host, { socket: () => { const s = new FakeSocket(); socks.push(s); return asWebSocket(s); }, pingMs: 60_000, retryMs: 1, ...opts });
    online = ctl;
    ctl.join('KQXTR', 'kb1');
    socks[0].open();
    socks[0].push({ t: 'welcome', room: 'KQXTR', id: 1, token: TOKEN, rejoined: false });
    socks[0].push(roomMsg(0));
    socks[0].push({ t: 'start', from: 0, cfg: startCfg() });
    expect(ctl.phase).toBe('racing');
    ctl.debugDropConnection();
    expect(ctl.status().reconnecting).not.toBeNull();
    await sleep(20);
    const sock = socks[1];
    sock.open();
    expect(sock.sent[0]).toMatchObject({ t: 'rejoin', room: 'KQXTR', token: TOKEN });
    sock.push({ t: 'welcome', room: 'KQXTR', id: 1, token: TOKEN, rejoined: true });
    expect(ctl.status()).toMatchObject({ reconnecting: null, syncing: true });
    return { sock, host, ctl };
  }

  it('eleito anfitrião ao voltar (o anterior caiu também): segue do próprio estado e manda o snapshot a quem volta depois', async () => {
    const { sock, host, ctl } = await rejoined();
    const before = host.state;
    sock.push(roomMsg(1, true, [0]));
    expect(ctl.isHost).toBe(true);
    expect(ctl.status().syncing).toBe(false);
    expect(host.state).toBe(before);
    expect(host.starts).toBe(1);
    // Com a entrada do assento 0 para os ticks 0..2, a corrida anda exatamente três ticks.
    sock.push({ t: 'i', from: 0, d: [0, 0, 1, 0, 1, 0, 1, 0, 2, 0, 1, 0] });
    const state = host.state as RaceState;
    expect(ctl.force(10, [], (inputs) => stepRace(state, getTrack(state.config.trackId), inputs))).toBe(3);
    // O outro volta: quem manda o estado agora é este computador.
    sock.push({ t: 'peer', id: 0, e: 'rejoin' });
    expect(sock.sent.find((m) => m.t === 'snap')).toMatchObject({ t: 'snap', to: 0, snap: { tick: 3 } });
  });

  it('o anfitrião encerrou a corrida enquanto este computador voltava: vai para a sala em vez de esperar um snapshot que não vem', async () => {
    const { sock, host, ctl } = await rejoined();
    sock.push(roomMsg(0, false));
    expect(ctl.status().syncing).toBe(false);
    expect(ctl.phase).toBe('lobby');
    expect(host.state).toBeNull();
    expect(host.menu).toBe('online');
  });

  it('"Sair da partida?" aberto durante a volta continua na tela depois do snapshot; fechando, o carro volta a responder', async () => {
    const { sock, host, ctl } = await rejoined();
    ctl.pauseKey(); // Esc durante "Conexão perdida — tentando voltar"
    expect(host.menu).toBe('online');
    sock.push(roomMsg(0, true));
    const state = createRace(raceConfigFrom(startCfg()), getTrack('copacabana'));
    sock.push({ t: 'snap', from: 0, snap: { tick: 0, state: serializeRace(state), start: startCfg(), inputs: [], ai: [] } });
    expect(host.starts).toBe(2);
    // O aviso vale (entrada neutra) enquanto está aberto: então tem de estar na tela.
    expect(ctl.quitOpen).toBe(true);
    expect(host.menu).toBe('online');
    ctl.closeQuit();
    expect(host.menu).toBeNull();
    const local: PlayerInput[] = [];
    local[1] = { steer: 0.5, throttle: true, brake: false, nitro: false, gearUp: false, gearDown: false };
    sock.sent.length = 0;
    ctl.force(1, local, () => undefined); // sem a entrada do assento 0 não roda, mas manda a sua
    const d = (sock.sent.find((m) => m.t === 'i')?.d ?? []) as number[];
    const target = d.slice(-4);
    expect(target.slice(0, 2)).toEqual([3, 1]);
    expect(decodeInput(target[2], target[3])).toMatchObject({ throttle: true, steer: 64 / 127 });
  });

  it('snapshot que não chega a tempo vira erro, em vez de "recebendo a corrida" para sempre', async () => {
    const { sock, host, ctl } = await rejoined({ syncTimeoutMs: 40 });
    sock.push(roomMsg(0, true));
    await sleep(100);
    expect(ctl.phase).toBe('error');
    expect(ctl.error).toBe('online.err.syncTimeout');
    expect(host.state).toBeNull();
    expect(host.menu).toBe('online');
  });
});

describe('sessão online: controles', () => {
  const unbound = (host: Host) => [0, 1, 2, 3].map((s) => host.input.seatDevice(s));

  /** Convidado (id 1) na corrida: o teclado dele responde pelo assento global 1. */
  function racing(): { socks: FakeSocket[]; host: Host; ctl: OnlineController } {
    const socks: FakeSocket[] = [];
    const host = new Host();
    const ctl = new OnlineController(host, { socket: () => { const s = new FakeSocket(); socks.push(s); return asWebSocket(s); }, pingMs: 60_000, retryMs: 1 });
    ctl.join('KQXTR', 'kb1');
    socks[0].open();
    socks[0].push({ t: 'welcome', room: 'KQXTR', id: 1, token: TOKEN, rejoined: false });
    socks[0].push(roomMsg(0));
    socks[0].push({ t: 'start', from: 0, cfg: startCfg() });
    expect(unbound(host)).toEqual([null, 'kb1', null, null]);
    return { socks, host, ctl };
  }

  it('erro de reconexão → Voltar: o teclado não fica preso ao assento do online (o lobby local o poria no P2)', async () => {
    const { socks, host, ctl } = racing();
    ctl.debugDropConnection();
    await sleep(20);
    socks[1].open();
    socks[1].push({ t: 'error', code: 'expired' });
    expect(ctl.phase).toBe('error');
    ctl.resetError();
    expect(unbound(host)).toEqual([null, null, null, null]);
  });

  it('resultado → sala → Esc (sair da sala): idem', () => {
    const { host, ctl } = racing();
    ctl.finished({ mode: 'quick', trackDef: getTrack('copacabana').def, results: [], humans: [], champ: null, newRecords: [] });
    ctl.backToRoom();
    expect(ctl.phase).toBe('lobby');
    ctl.leave(false);
    expect(unbound(host)).toEqual([null, null, null, null]);
  });
});
