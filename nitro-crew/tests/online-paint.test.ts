// Pintura no online: cada jogador local escolhe a sua na sala (começa com a guardada para aquele assento e
// carro), a escolha vai no `info` (LobbyPlayer.paint) e na largada (SeatAssignment.paint), e cada computador
// monta a mesma corrida — a config sem a pintura (o estado não carrega cosmético) e os humanos da sessão com
// ela, para todos desenharem cada carro na cor que o dono escolheu. Partes puras e o controlador com socket falso.
import { afterEach, describe, expect, it } from 'vitest';
import { createRace } from '../src/core/sim/race';
import { getTrack } from '../src/core/track';
import type { HumanEntry, RaceConfig, RaceState } from '../src/core/types';
import { DEFAULT_SAVE, DEFAULT_SETTINGS, type DeviceId, type InputProvider, type MenuNav, type RaceDriver, type SaveData, type Settings } from '../src/game/contracts';
import { assignSeats, CONTENT_RULES, OnlineController, raceConfigFrom, startHumans, type OnlineHost } from '../src/game/online-session';
import { nextPaint, ORIGINAL_PAINT, paintColors, racePaints, seatPaint, setSeatPaint } from '../src/game/paints';
import { parseClientInfo, parseStartConfig, type RoomView, type StartConfig } from '../src/net/protocol';

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
  readonly save: SaveData = { ...DEFAULT_SAVE, seatNames: ['Ana', 'P2', 'P3', 'P4'], seatCars: [...DEFAULT_SAVE.seatCars] };
  readonly input = fakeInput();
  state: RaceState | null = null;
  config: RaceConfig | null = null;
  humans: HumanEntry[] | null = null;
  saved = 0;
  startRace(config: RaceConfig, _localSeats: number[], _driver: RaceDriver, state?: RaceState, humans?: HumanEntry[]): void {
    this.config = config;
    this.humans = humans ?? null;
    this.state = state ?? createRace(config, getTrack(config.trackId));
  }
  raceState() { return this.state; }
  clearRace() { this.state = null; }
  showScreen() {}
  hideScreen() {}
  menuOpen() { return false; }
  exitToMain() {}
  persistSettings() {}
  persistSave() { this.saved++; }
}

const TOKEN = 'a'.repeat(24);

function roomMsg(infos: Record<number, { players: Array<Record<string, unknown>>; ready: boolean }> = {}) {
  const base: Record<number, { players: Array<Record<string, unknown>>; ready: boolean }> = {
    0: { players: [{ name: 'Ana', car: 'falcao' }], ready: false },
    1: { players: [{ name: 'Bia', car: 'trovao' }], ready: false },
    ...infos,
  };
  return {
    t: 'room', room: {
      code: 'KQXTR', host: 0, started: false, settings: null,
      clients: [0, 1].map((id) => ({ id, seats: base[id].players.length, connected: true, info: base[id] })),
    },
  };
}

type Info = { players: Array<{ name: string; car: string; paint?: string }>; ready: boolean };
const lastInfo = (sock: FakeSocket): Info => {
  const m = [...sock.sent].reverse().find((x) => x.t === 'info' || x.t === 'create' || x.t === 'join');
  return (m as { info: Info }).info;
};

const START_BASE = {
  trackId: 'copacabana', laps: 1, versus: false, difficulty: 'amador', totalCars: 4, manualGear: false,
  assists: { sharedNitro: true, tow: true, teamDraft: true, catchup: true }, delay: 3, seed: 42,
} as const;

describe('protocolo: a pintura do jogador na sala e na largada', () => {
  it('vai e volta; ausente fica ausente; desconhecida, de outro tipo ou a Original viram a de fábrica sem recusar a mensagem', () => {
    const info = (paint: unknown) => parseClientInfo({ players: [{ name: 'Ana', car: 'falcao', paint }], ready: false }, CONTENT_RULES);
    expect(info('rubi')?.players[0].paint).toBe('rubi');
    for (const bad of [undefined, 'roxo-que-nao-existe', 42, null, { a: 1 }, ORIGINAL_PAINT]) {
      const p = info(bad);
      expect(p, String(bad)).not.toBeNull();
      expect(p?.players[0], String(bad)).not.toHaveProperty('paint');
    }
    const cfg = (paint: unknown) => parseStartConfig({ ...START_BASE, seats: [{ seat: 0, client: 0, name: 'Ana', car: 'falcao', paint }] }, CONTENT_RULES);
    expect(cfg('gelo')?.seats[0].paint).toBe('gelo');
    expect(cfg('xyz')?.seats[0]).not.toHaveProperty('paint');
    expect(cfg(7)).not.toBeNull();
  });

  it('cada computador monta a config sem a pintura (estado sem cosmético) e os humanos da sessão com ela', () => {
    const start: StartConfig = { ...START_BASE, seats: [{ seat: 0, client: 0, name: 'Ana', car: 'falcao', paint: 'rubi' }, { seat: 1, client: 1, name: 'Bia', car: 'falcao' }] };
    const config = raceConfigFrom(start);
    expect(config.humans.some((h) => 'paint' in h)).toBe(false);
    const humans = startHumans(start);
    expect(humans.map((h) => h.paint)).toEqual(['rubi', undefined]);
    // As duas máquinas pintam igual: a mesma função pura sobre a mesma largada.
    const a = createRace(config, getTrack('copacabana'));
    const b = createRace(raceConfigFrom(JSON.parse(JSON.stringify(start)) as StartConfig), getTrack('copacabana'));
    expect(racePaints(a.cars, humans)).toEqual(racePaints(b.cars, startHumans(JSON.parse(JSON.stringify(start)) as StartConfig)));
    expect(racePaints(a.cars, humans)[a.cars.findIndex((c) => c.seat === 0)]).toEqual(paintColors('rubi'));
  });
});

describe('online: cada jogador local escolhe a própria pintura', () => {
  let online: OnlineController | null = null;
  afterEach(() => { online?.leave(false); online = null; });

  function setup(myId: number, prepare?: (host: Host) => void): { sock: FakeSocket; host: Host; ctl: OnlineController } {
    const sock = new FakeSocket();
    const host = new Host();
    prepare?.(host);
    const ctl = new OnlineController(host, { socket: () => sock as unknown as WebSocket, pingMs: 60_000 });
    online = ctl;
    if (myId === 0) ctl.create('kb1'); else ctl.join('KQXTR', 'kb1');
    sock.open();
    sock.push({ t: 'welcome', room: 'KQXTR', id: myId, token: TOKEN, rejoined: false });
    sock.push(roomMsg());
    expect(ctl.phase).toBe('lobby');
    return { sock, host, ctl };
  }

  it('começa com a guardada (assento + carro); trocar publica na sala e grava no save', () => {
    const { sock, host, ctl } = setup(1, (h) => setSeatPaint(h.save, 0, 'falcao', 'gelo'));
    expect(ctl.paintOf(0)).toBe('gelo');
    expect(lastInfo(sock).players[0]).toMatchObject({ name: 'Ana', car: 'falcao', paint: 'gelo' });
    ctl.cyclePaint(0, 1);
    expect(ctl.paintOf(0)).toBe(nextPaint('gelo', 1));
    expect(lastInfo(sock).players[0].paint).toBe(nextPaint('gelo', 1));
    expect(seatPaint(host.save, 0, 'falcao')).toBe(nextPaint('gelo', 1));
    expect(host.saved).toBe(1);
    // De volta à Original: o campo some da mensagem (a de quem não pinta é a de antes).
    while (ctl.paintOf(0) !== ORIGINAL_PAINT) ctl.cyclePaint(0, 1);
    expect(lastInfo(sock).players[0]).not.toHaveProperty('paint');
  });

  it('trocar de carro traz a pintura guardada daquele carro', () => {
    const { sock, ctl } = setup(1, (h) => setSeatPaint(h.save, 0, 'trovao', 'onix'));
    expect(ctl.paintOf(0)).toBe(ORIGINAL_PAINT);
    ctl.cycleCar(0, 1);
    expect(ctl.locals[0].car).toBe('trovao');
    expect(ctl.paintOf(0)).toBe('onix');
    expect(lastInfo(sock).players[0]).toMatchObject({ car: 'trovao', paint: 'onix' });
  });

  it('o segundo jogador deste computador tem a sua (a do P2 local no save)', () => {
    const { sock, ctl } = setup(1, (h) => setSeatPaint(h.save, 1, h.save.seatCars[1], 'flamingo'));
    expect(ctl.addLocal('gp0')).toBe(true);
    expect(lastInfo(sock).players.map((p) => p.paint)).toEqual([undefined, 'flamingo']);
  });

  it('com o "pronto" dado, não muda', () => {
    const { sock, host, ctl } = setup(1);
    ctl.toggleReady();
    const sent = sock.sent.length;
    ctl.cyclePaint(0, 1);
    expect(ctl.paintOf(0)).toBe(ORIGINAL_PAINT);
    expect(sock.sent.length).toBe(sent);
    expect(host.saved).toBe(0);
  });

  it('a pintura do convidado aparece para o anfitrião, vai na largada e chega à corrida de todos', () => {
    const { sock, host, ctl } = setup(0);
    sock.push(roomMsg({ 1: { players: [{ name: 'Bia', car: 'trovao', paint: 'flamingo' }], ready: true } }));
    const seats = assignSeats(ctl.room as RoomView);
    expect(seats.map((s) => s.paint)).toEqual([undefined, 'flamingo']);
    expect(ctl.startRace()).toBe(true);
    const start = sock.sent.find((m) => m.t === 'start') as { cfg: StartConfig };
    expect(start.cfg.seats.map((s) => s.paint)).toEqual([undefined, 'flamingo']);
    sock.push({ t: 'start', from: 0, cfg: start.cfg });
    expect(host.config?.humans.some((h) => 'paint' in h)).toBe(false);
    expect(host.humans?.map((h) => h.paint)).toEqual([undefined, 'flamingo']);
    const state = host.state as RaceState;
    const paints = racePaints(state.cars, host.humans ?? []);
    expect(paints[state.cars.findIndex((c) => c.seat === 1)]).toEqual(paintColors('flamingo'));
    expect(paints[state.cars.findIndex((c) => c.seat === 0)]).toBeNull();
  });

  it('o anfitrião que troca e larga em seguida larga com a pintura nova (sem esperar o eco da sala)', () => {
    const { sock, ctl } = setup(0);
    sock.push(roomMsg({ 1: { players: [{ name: 'Bia', car: 'trovao' }], ready: true } }));
    ctl.cyclePaint(0, 1);
    expect(ctl.startRace()).toBe(true);
    const start = sock.sent.find((m) => m.t === 'start') as { cfg: StartConfig };
    expect(start.cfg.seats[0]).toMatchObject({ seat: 0, client: 0, paint: nextPaint(ORIGINAL_PAINT, 1) });
    expect(parseStartConfig(JSON.parse(JSON.stringify(start.cfg)), CONTENT_RULES)?.seats[0].paint).toBe(nextPaint(ORIGINAL_PAINT, 1));
  });
});
