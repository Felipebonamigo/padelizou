// Direção assistida no online: cada jogador local escolhe a sua na tela da sala, a escolha vai no
// `info` (LobbyPlayer.assist) e aparece para todos; o anfitrião larga com a escolha que acabou de
// fazer; depois da largada (ou com o "pronto") não muda. E os rótulos que marcam quem correu
// assistido (sala, HUD, resultado) — partes puras, sem DOM.
import { afterEach, describe, expect, it } from 'vitest';
import { assistTagText, seatAssist, withRaceAssists } from '../src/access/humans';
import '../src/access/strings';
import { createRace } from '../src/core/sim/race';
import { getTrack } from '../src/core/track';
import type { HumanEntry, RaceConfig, RaceState } from '../src/core/types';
import { DEFAULT_SAVE, DEFAULT_SETTINGS, type DeviceId, type InputProvider, type MenuNav, type RaceDriver, type SaveData, type Settings } from '../src/game/contracts';
import { assignSeats, OnlineController, raceConfigFrom, type OnlineHost } from '../src/game/online-session';
import { setLanguage } from '../src/i18n';
import { parseStartConfig, type RoomView, type StartConfig } from '../src/net/protocol';
import { CONTENT_RULES } from '../src/game/online-session';

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
  persisted = 0;
  startRace(config: RaceConfig, _localSeats: number[], _driver: RaceDriver, state?: RaceState): void {
    this.config = config;
    this.state = state ?? createRace(config, getTrack(config.trackId));
  }
  raceState() { return this.state; }
  clearRace() { this.state = null; }
  showScreen() {}
  hideScreen() {}
  menuOpen() { return false; }
  exitToMain() {}
  persistSettings() { this.persisted++; }
}

const TOKEN = 'a'.repeat(24);

/** Sala com o anfitrião (0) e um convidado (1); `infos` substitui o que cada um declarou. */
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

type Info = { players: Array<{ name: string; car: string; assist?: string }>; ready: boolean };
const lastInfo = (sock: FakeSocket): Info => {
  const m = [...sock.sent].reverse().find((x) => x.t === 'info' || x.t === 'create' || x.t === 'join');
  return (m as { info: Info }).info;
};

describe('online: cada jogador local escolhe a própria direção assistida', () => {
  let online: OnlineController | null = null;
  afterEach(() => { online?.leave(false); online = null; });

  function setup(myId: number): { sock: FakeSocket; host: Host; ctl: OnlineController } {
    const sock = new FakeSocket();
    const host = new Host();
    const ctl = new OnlineController(host, { socket: () => sock as unknown as WebSocket, pingMs: 60_000 });
    online = ctl;
    if (myId === 0) ctl.create('kb1'); else ctl.join('KQXTR', 'kb1');
    sock.open();
    sock.push({ t: 'welcome', room: 'KQXTR', id: myId, token: TOKEN, rejoined: false });
    sock.push(roomMsg());
    expect(ctl.phase).toBe('lobby');
    return { sock, host, ctl };
  }

  it('começa com a do assento local nas opções; trocar publica na sala e fica salvo nas opções', () => {
    const { sock, host, ctl } = setup(1);
    expect(ctl.assistOf(0)).toBe('none');
    expect(lastInfo(sock).players[0]).not.toHaveProperty('assist');
    ctl.cycleAssist(0, 1);
    expect(ctl.assistOf(0)).toBe('brake');
    expect(lastInfo(sock).players[0]).toMatchObject({ name: 'Ana', assist: 'brake' });
    expect(host.settings.seatAssists[0]).toBe('brake');
    expect(host.persisted).toBe(1);
    ctl.cycleAssist(0, -1);
    ctl.cycleAssist(0, -1);
    expect(ctl.assistOf(0)).toBe('full');
    expect(lastInfo(sock).players[0].assist).toBe('full');
    // De volta a "nenhuma": o campo some da mensagem (a de quem não usa é a de antes).
    ctl.cycleAssist(0, 1);
    expect(lastInfo(sock).players[0]).not.toHaveProperty('assist');
  });

  it('o segundo jogador deste computador tem a sua (a do P2 local nas opções)', () => {
    const { sock, host, ctl } = setup(1);
    host.settings.seatAssists[1] = 'steer';
    expect(ctl.addLocal('gp0')).toBe(true);
    expect(lastInfo(sock).players.map((p) => p.assist)).toEqual([undefined, 'steer']);
    ctl.cycleAssist(1, 1);
    expect(lastInfo(sock).players.map((p) => p.assist)).toEqual([undefined, 'full']);
    expect(host.settings.seatAssists).toEqual(['none', 'full', 'none', 'none']);
  });

  it('com o "pronto" dado ou depois da largada, não muda', () => {
    const { sock, host, ctl } = setup(1);
    ctl.toggleReady();
    const sent = sock.sent.length;
    ctl.cycleAssist(0, 1);
    expect(ctl.assistOf(0)).toBe('none');
    expect(sock.sent.length).toBe(sent);
    ctl.toggleReady();
    ctl.cycleAssist(0, 1);
    expect(ctl.assistOf(0)).toBe('brake');
    const cfg: StartConfig = {
      trackId: 'copacabana', laps: 1, versus: false, difficulty: 'amador', totalCars: 4, manualGear: false,
      assists: { sharedNitro: true, tow: true, teamDraft: true, catchup: true }, delay: 3, seed: 42,
      seats: [{ seat: 0, client: 0, name: 'Ana', car: 'falcao' }, { seat: 1, client: 1, name: 'Ana', car: 'falcao', assist: 'brake' }],
    };
    sock.push({ t: 'start', from: 0, cfg });
    expect(ctl.phase).toBe('racing');
    ctl.cycleAssist(0, 1);
    expect(ctl.assistOf(0)).toBe('brake');
    expect(host.settings.seatAssists[0]).toBe('brake');
    expect(host.config?.humans.map((h) => h.assist)).toEqual([undefined, 'brake']);
  });

  it('a escolha do convidado aparece para o anfitrião e vai na largada', () => {
    const { sock, host, ctl } = setup(0);
    sock.push(roomMsg({ 1: { players: [{ name: 'Bia', car: 'trovao', assist: 'full' }], ready: true } }));
    const seats = assignSeats(ctl.room as RoomView);
    expect(seats.map((s) => s.assist)).toEqual([undefined, 'full']);
    expect(ctl.startRace()).toBe(true);
    const start = sock.sent.find((m) => m.t === 'start') as { cfg: StartConfig };
    expect(start.cfg.seats.map((s) => s.assist)).toEqual([undefined, 'full']);
    // Cada computador monta a mesma config a partir da largada.
    sock.push({ t: 'start', from: 0, cfg: start.cfg });
    expect(host.config?.humans.map((h) => h.assist)).toEqual([undefined, 'full']);
  });

  it('o anfitrião que troca e larga em seguida larga com a escolha nova (sem esperar o eco da sala)', () => {
    const { sock, ctl } = setup(0);
    sock.push(roomMsg({ 1: { players: [{ name: 'Bia', car: 'trovao' }], ready: true } }));
    ctl.cycleAssist(0, 1);
    // O relay ainda não devolveu a sala com a escolha nova quando o anfitrião aperta LARGAR.
    expect(ctl.startRace()).toBe(true);
    const start = sock.sent.find((m) => m.t === 'start') as { cfg: StartConfig };
    expect(start.cfg.seats[0]).toMatchObject({ seat: 0, client: 0, assist: 'brake' });
    expect(parseStartConfig(JSON.parse(JSON.stringify(start.cfg)), CONTENT_RULES)?.seats[0].assist).toBe('brake');
  });
});

describe('rótulos de quem corre assistido', () => {
  afterEach(() => setLanguage('pt'));

  it('selo com o nível na sala; nada para quem não usa', () => {
    setLanguage('pt');
    expect(assistTagText(undefined)).toBeNull();
    expect(assistTagText('none')).toBeNull();
    expect(assistTagText('brake')).toBe('ASSIST · Freio');
    expect(assistTagText('steer')).toBe('ASSIST · Volante');
    expect(assistTagText('full')).toBe('ASSIST · Completa');
    setLanguage('en');
    expect(assistTagText('steer')).toBe('ASSIST · Steering');
  });

  const human = (seat: number, assist?: HumanEntry['assist']): HumanEntry =>
    ({ seat, name: `P${seat + 1}`, carId: 'falcao', teamId: 0, color: '#fff', ...(assist ? { assist } : {}) });

  it('nível de um assento a partir dos humanos da corrida', () => {
    const humans = [human(0), human(1, 'full'), human(2, 'none')];
    expect(seatAssist(humans, 0)).toBe('none');
    expect(seatAssist(humans, 1)).toBe('full');
    expect(seatAssist(humans, 2)).toBe('none');
    expect(seatAssist(humans, 3)).toBe('none');
  });

  it('resultado: a assistência vem da config da corrida (a que a simulação usou), mesmo se os humanos da sessão não a trazem', () => {
    // Copa retomada/carreira: os humanos vêm do save sem o nível; a config ganhou o das opções.
    const session = [human(0), human(1)];
    const config = [human(0, 'brake'), human(1)];
    expect(withRaceAssists(session, config).map((h) => h.assist)).toEqual(['brake', undefined]);
    // Online: a config de todos traz o nível de cada assento, remoto inclusive.
    const rc = raceConfigFrom({
      trackId: 'copacabana', laps: 1, versus: false, difficulty: 'amador', totalCars: 4, manualGear: false,
      assists: { sharedNitro: true, tow: true, teamDraft: true, catchup: true }, delay: 3, seed: 1,
      seats: [{ seat: 0, client: 0, name: 'Ana', car: 'falcao' }, { seat: 1, client: 1, name: 'Bia', car: 'trovao', assist: 'steer' }],
    });
    expect(withRaceAssists(rc.humans, rc.humans).map((h) => seatAssist([h], h.seat))).toEqual(['none', 'steer']);
    // Nível 'none' não entra no objeto.
    expect(withRaceAssists([human(0, 'full')], [human(0)])[0]).not.toHaveProperty('assist');
  });
});
