// Opções da sala (pista, voltas, carros…) com a rede atrasada: o anfitrião ajusta, o relay guarda e
// devolve a sala a todos — inclusive a ele mesmo, um pouco depois. Um eco que chega depois de um
// ajuste mais novo não pode desfazer esse ajuste, nem servir de base para o próximo, nem ir na
// largada. Dois computadores ligados a um relay de mentira (a mesma regra do server/relay.mjs para o
// lobby) que segura o que manda a cada um numa fila: o teste decide quando cada mensagem chega.
import { afterEach, describe, expect, it } from 'vitest';
import { createRace } from '../src/core/sim/race';
import { getTrack } from '../src/core/track';
import type { RaceConfig, RaceState } from '../src/core/types';
import { DEFAULT_SAVE, DEFAULT_SETTINGS, type DeviceId, type InputProvider, type MenuNav, type RaceDriver, type SaveData, type Settings } from '../src/game/contracts';
import { OnlineController, type OnlineHost } from '../src/game/online-session';
import type { RoomSettings } from '../src/net/protocol';

type Msg = Record<string, unknown>;
const TOKEN = 'a'.repeat(24);

/** O socket de um computador, ligado ao relay de mentira. */
class LinkSocket {
  readyState = 0;
  onopen: (() => void) | null = null;
  onmessage: ((ev: { data: unknown }) => void) | null = null;
  onclose: ((ev: { code: number }) => void) | null = null;
  onerror: (() => void) | null = null;
  /** O que o relay já mandou e ainda não chegou a este computador, em ordem. */
  readonly inbox: string[] = [];
  constructor(private readonly relay: QueueRelay) {}
  send(data: string): void { this.relay.receive(this, JSON.parse(data) as Msg); }
  close(): void { this.readyState = 3; this.relay.disconnect(this); }
}

interface RelayClient { id: number; seats: number; info: unknown; sock: LinkSocket }

/**
 * O relay no lobby (create, join, info, settings, start, leave), como o server/relay.mjs: cada
 * mudança empurra a sala inteira — com as opções que ele tem naquele instante — a todos. O que ele
 * manda fica na fila de cada computador até o teste entregar (`deliver`/`flush`).
 */
class QueueRelay {
  readonly code = 'KQXTR';
  host = -1;
  started = false;
  settings: unknown = null;
  private nextId = 0;
  private clients: RelayClient[] = [];
  private readonly sockets = new Map<string, LinkSocket>();

  /** Fábrica de sockets de um computador (o nome serve para entregar a fila dele). */
  factory(name: string): () => WebSocket {
    return () => {
      const s = new LinkSocket(this);
      this.sockets.set(name, s);
      return s as unknown as WebSocket;
    };
  }

  receive(sock: LinkSocket, msg: Msg): void {
    const me = this.clients.find((c) => c.sock === sock);
    switch (msg.t) {
      case 'ping': this.to(sock, { t: 'pong', n: msg.n }); return;
      case 'create':
      case 'join': {
        const c: RelayClient = { id: this.nextId++, seats: msg.seats as number, info: msg.info, sock };
        if (msg.t === 'create') this.host = c.id;
        this.clients.push(c);
        this.to(sock, { t: 'welcome', room: this.code, id: c.id, token: TOKEN, rejoined: false });
        for (const o of this.clients) if (o !== c) this.to(o.sock, { t: 'peer', id: c.id, e: 'join' });
        this.pushRoom();
        return;
      }
      case 'info':
        if (!me) return;
        me.seats = msg.seats as number;
        me.info = msg.info;
        this.pushRoom();
        return;
      case 'settings':
        if (!me || me.id !== this.host) { this.to(sock, { t: 'error', code: 'not_host' }); return; }
        this.settings = msg.settings;
        this.pushRoom();
        return;
      case 'start':
        if (!me || me.id !== this.host || this.started) return;
        this.started = true;
        for (const c of this.clients) this.to(c.sock, { t: 'start', from: me.id, cfg: msg.cfg });
        this.pushRoom();
        return;
      case 'leave':
        if (me) this.remove(me);
        return;
    }
  }

  disconnect(sock: LinkSocket): void {
    const me = this.clients.find((c) => c.sock === sock);
    if (me && !this.started) this.remove(me);
  }

  private remove(c: RelayClient): void {
    this.clients = this.clients.filter((x) => x !== c);
    if (this.host === c.id && this.clients.length > 0) this.host = this.clients[0].id;
    for (const o of this.clients) this.to(o.sock, { t: 'peer', id: c.id, e: 'leave' });
    this.pushRoom();
  }

  private pushRoom(): void {
    const room = {
      code: this.code, host: this.host, started: this.started, settings: this.settings,
      clients: this.clients.map((c) => ({ id: c.id, seats: c.seats, info: c.info, connected: true })),
    };
    for (const c of this.clients) this.to(c.sock, { t: 'room', room });
  }

  /** Serializa na hora: o que chega é a sala como estava quando o relay mandou. */
  private to(sock: LinkSocket, msg: Msg): void {
    if (sock.readyState !== 3) sock.inbox.push(JSON.stringify(msg));
  }

  private open(): void {
    for (const s of this.sockets.values()) if (s.readyState === 0) { s.readyState = 1; s.onopen?.(); }
  }

  /** Entrega a `name` as `n` primeiras mensagens da fila dele (todas, sem `n`). */
  deliver(name: string, n = Infinity): void {
    this.open();
    const s = this.sockets.get(name);
    if (!s) return;
    for (let i = 0; i < n && s.inbox.length > 0; i++) {
      const data = s.inbox.shift() as string;
      if (s.readyState === 1) s.onmessage?.({ data });
    }
  }

  /** A conexão de `name` cai (do lado da rede): o jogo recebe o close. */
  drop(name: string): void {
    const s = this.sockets.get(name);
    if (!s) return;
    this.disconnect(s);
    s.readyState = 3;
    s.onclose?.({ code: 1006 });
  }

  /** Quantas mensagens esperam na fila de `name`. */
  queued(name: string): number {
    return this.sockets.get(name)?.inbox.length ?? 0;
  }

  /** Entrega tudo a todos até a rede ficar quieta. */
  flush(): void {
    for (let guard = 0; guard < 100; guard++) {
      this.open();
      if ([...this.sockets.values()].every((s) => s.inbox.length === 0)) return;
      for (const name of this.sockets.keys()) this.deliver(name);
    }
    throw new Error('a rede não ficou quieta');
  }
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

/** Um computador: as opções padrão do jogo (3 voltas, 20 carros) e a corrida que ele montou na largada. */
class Computer implements OnlineHost {
  readonly settings: Settings = { ...DEFAULT_SETTINGS, assists: { ...DEFAULT_SETTINGS.assists }, seatAssists: [...DEFAULT_SETTINGS.seatAssists] };
  readonly save: SaveData;
  readonly input = fakeInput();
  config: RaceConfig | null = null;
  state: RaceState | null = null;
  constructor(name: string) { this.save = { ...DEFAULT_SAVE, seatNames: [name, 'P2', 'P3', 'P4'], seatCars: [...DEFAULT_SAVE.seatCars] }; }
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
  persistSettings() {}
}

/**
 * Uma seta nos seletores da sala, como a tela Online faz (src/ui/screens/online.ts): o valor novo
 * é o que a tela mostra agora (`room.settings`) mais ou menos um.
 */
function laps(ctl: OnlineController, d: -1 | 1): void {
  ctl.updateRoomSettings({ laps: (ctl.room?.settings?.laps ?? 3) + d });
}
function cars(ctl: OnlineController, d: -1 | 1): void {
  ctl.updateRoomSettings({ totalCars: Math.max(8, (ctl.room?.settings?.totalCars ?? 20) + d) });
}
const pick = (s: RoomSettings | null | undefined) => (s ? { trackId: s.trackId, laps: s.laps, totalCars: s.totalCars } : null);
const raceOf = (c: RaceConfig | null) => (c ? { trackId: c.trackId, laps: c.laps, totalCars: c.totalCars } : null);

describe('online: opções da sala com o eco do relay atrasado', () => {
  let open: OnlineController[] = [];
  afterEach(() => { for (const c of open) c.leave(false); open = []; });

  /** Anfitrião (A, id 0) e convidado (B, id 1) na mesma sala, rede quieta, B ainda não pronto. */
  function room(): { relay: QueueRelay; a: Computer; b: Computer; A: OnlineController; B: OnlineController } {
    const relay = new QueueRelay();
    const a = new Computer('Ana');
    const b = new Computer('Bia');
    const A = new OnlineController(a, { socket: relay.factory('A'), pingMs: 60_000 });
    const B = new OnlineController(b, { socket: relay.factory('B'), pingMs: 60_000 });
    open = [A, B];
    A.create('kb1');
    relay.flush();
    B.join(relay.code, 'kb1');
    relay.flush();
    expect(A.isHost).toBe(true);
    expect(pick(A.room?.settings)).toEqual({ trackId: 'copacabana', laps: 3, totalCars: 20 });
    expect(pick(B.room?.settings)).toEqual(pick(A.room?.settings));
    return { relay, a, b, A, B };
  }

  it('ajustes seguidos com o eco atrasado: a tela mostra o último e o próximo ajuste parte dele (as voltas não voltam ao mexer nos carros)', () => {
    const { relay, a, b, A, B } = room();
    B.toggleReady();
    relay.flush();
    // 3 → 2 → 1 volta; do relay só chegou ao anfitrião a sala do primeiro ajuste (2 voltas).
    laps(A, -1);
    laps(A, -1);
    relay.deliver('A', 1);
    expect(A.room?.settings?.laps).toBe(1);
    // 20 → 8 carros, com a sala de cada ajuste chegando ao anfitrião dois ajustes depois.
    for (let i = 0; i < 12; i++) {
      cars(A, -1);
      if (relay.queued('A') > 2) relay.deliver('A', 1);
    }
    expect(pick(A.room?.settings)).toEqual({ trackId: 'copacabana', laps: 1, totalCars: 8 });
    relay.flush();
    expect(relay.settings).toMatchObject({ laps: 1, totalCars: 8 });
    expect(pick(A.room?.settings)).toEqual({ trackId: 'copacabana', laps: 1, totalCars: 8 });
    expect(pick(B.room?.settings)).toEqual({ trackId: 'copacabana', laps: 1, totalCars: 8 });
    expect(A.startRace()).toBe(true);
    relay.flush();
    expect(raceOf(a.config)).toEqual({ trackId: 'copacabana', laps: 1, totalCars: 8 });
    expect(JSON.stringify(b.config)).toBe(JSON.stringify(a.config));
  });

  it('o sintoma do playtest: 1 volta e 8 carros escolhidos não viram 2 voltas e 15 carros', () => {
    const { relay, a, b, A, B } = room();
    B.toggleReady();
    relay.flush();
    // ← ← nas voltas e ← ×12 nos carros; a sala do primeiro ajuste (2 voltas, 20 carros) só chega
    // ao anfitrião depois da sétima seta nos carros, e nenhuma outra chega antes do fim.
    laps(A, -1);
    laps(A, -1);
    for (let i = 0; i < 7; i++) cars(A, -1);
    relay.deliver('A', 1);
    for (let i = 0; i < 5; i++) cars(A, -1);
    relay.flush();
    expect(pick(relay.settings as RoomSettings)).toEqual({ trackId: 'copacabana', laps: 1, totalCars: 8 });
    expect(A.startRace()).toBe(true);
    relay.flush();
    expect(raceOf(a.config)).toEqual({ trackId: 'copacabana', laps: 1, totalCars: 8 });
    expect(JSON.stringify(b.config)).toBe(JSON.stringify(a.config));
  });

  it('LARGAR logo depois do ajuste, com ecos velhos chegando: larga com o último escolhido, igual nos dois', () => {
    const { relay, a, b, A, B } = room();
    B.toggleReady();
    relay.flush();
    laps(A, -1);
    laps(A, -1);
    for (let i = 0; i < 12; i++) cars(A, -1);
    // Chegam ao anfitrião só as salas dos três primeiros ajustes (2 voltas, 20 e 19 carros) e ele aperta LARGAR.
    relay.deliver('A', 3);
    expect(A.startRace()).toBe(true);
    relay.flush();
    expect(raceOf(a.config)).toEqual({ trackId: 'copacabana', laps: 1, totalCars: 8 });
    expect(JSON.stringify(b.config)).toBe(JSON.stringify(a.config));
  });

  it('a sala reenviada por causa do convidado (pronto, carro) traz as opções que o relay tinha e não desfaz o ajuste do anfitrião', () => {
    const { relay, a, b, A, B } = room();
    // O "pronto" do convidado chega ao relay antes do ajuste; a sala que ele gera (3 voltas) chega ao anfitrião depois.
    B.toggleReady();
    relay.deliver('B');
    laps(A, -1);
    laps(A, -1);
    relay.deliver('A', 1);
    expect(A.room?.clients.find((c) => c.id === 1)?.info?.ready).toBe(true);
    expect(A.room?.settings?.laps).toBe(1);
    expect(A.startBlocker()).toBeNull();
    expect(A.startRace()).toBe(true);
    relay.flush();
    expect(raceOf(a.config)).toEqual({ trackId: 'copacabana', laps: 1, totalCars: 20 });
    expect(JSON.stringify(b.config)).toBe(JSON.stringify(a.config));
  });

  it('quem herda a sala no lobby parte das opções que o anfitrião anterior deixou', () => {
    const { relay, A, B } = room();
    laps(A, -1);
    for (let i = 0; i < 4; i++) cars(A, -1);
    relay.flush();
    A.leave(false);
    open = [B];
    relay.flush();
    expect(B.isHost).toBe(true);
    expect(pick(B.room?.settings)).toEqual({ trackId: 'copacabana', laps: 2, totalCars: 16 });
    laps(B, 1);
    relay.flush();
    expect(pick(B.room?.settings)).toEqual({ trackId: 'copacabana', laps: 3, totalCars: 16 });
    expect(relay.settings).toMatchObject({ laps: 3, totalCars: 16 });
  });

  it('sala nova começa das opções do jogo, não das escolhidas na sala anterior', () => {
    // O jogo tem um OnlineController só: o mesmo sai de uma sala e cria outra.
    let relay = new QueueRelay();
    const A = new OnlineController(new Computer('Ana'), { socket: () => relay.factory('A')(), pingMs: 60_000 });
    open = [A];
    A.create('kb1');
    relay.flush();
    laps(A, -1);
    cars(A, -1);
    relay.flush();
    expect(pick(A.room?.settings)).toEqual({ trackId: 'copacabana', laps: 2, totalCars: 19 });
    A.leave(false);
    relay = new QueueRelay();
    A.create('kb1');
    relay.flush();
    expect(pick(A.room?.settings)).toEqual({ trackId: 'copacabana', laps: 3, totalCars: 20 });
    // Sem passar pelo "Sair": a conexão cai no lobby, a tela de erro volta ao início e ele cria outra.
    laps(A, 1);
    relay.flush();
    relay.drop('A');
    expect(A.phase).toBe('error');
    A.resetError();
    relay = new QueueRelay();
    A.create('kb1');
    relay.flush();
    expect(pick(A.room?.settings)).toEqual({ trackId: 'copacabana', laps: 3, totalCars: 20 });
  });
});
