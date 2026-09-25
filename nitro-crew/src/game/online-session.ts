// Sessão online: sala no relay, lobby em rede, largada, corrida em lockstep, reconexão por
// snapshot e tomada do assento pela IA. Não toca o DOM nem a simulação diretamente: fala com a
// sessão por `OnlineHost` (startRace/raceState/showScreen…) e com o relay por `NetClient`, o que
// permite rodar isto inteiro no Node (tests/net-relay.test.ts) com um relay de verdade.
//
// Assentos: cada cliente tem 1–2 jogadores locais; na largada o anfitrião numera os assentos
// globais 0..3 pela ordem dos clientes (id no relay) e dos jogadores de cada um. Cada cliente liga
// os próprios controles aos assentos globais que recebeu e só desenha os viewports deles.
import { TICK_RATE } from '../core/constants';
import { CARS } from '../core/data/cars';
import { SEAT_COLORS } from '../core/data/drivers';
import { deserializeRace, serializeRace } from '../core/serialize';
import { TRACKS } from '../core/track';
import { NEUTRAL_INPUT, type HumanEntry, type PlayerInput, type RaceConfig, type RaceState } from '../core/types';
import { NetClient, type SocketFactory } from '../net/client';
import { Lockstep, type DesyncReport } from '../net/lockstep';
import {
  DEFAULT_INPUT_DELAY, isTakeover, MAX_HUMANS, MAX_INPUT_DELAY, MAX_LOCAL_PLAYERS, MAX_RECORDS_PER_MESSAGE, MIN_INPUT_DELAY, normalizeRoomCode,
  packRecords, PROTOCOL_VERSION, RECONNECT_WINDOW_MS, unpackRecordList,
  type ClientInfo, type ClientMessage, type ContentRules, type ErrorCode, type InputRecord, type RoomSettings,
  type RoomView, type SeatAssignment, type ServerMessage, type Snapshot, type StartConfig,
} from '../net/protocol';
import type { DeviceId, InputProvider, RaceDriver, ResultsScreenData, SaveData, Settings } from './contracts';

const DT = 1 / TICK_RATE;
/** No máximo quanto tempo de simulação um quadro tenta recuperar (o resto é esquecido). */
const MAX_BACKLOG = 0.25;
/** Ticks extras por quadro para quem ficou atrás dos outros. */
const MAX_CATCHUP = 4;

export const CONTENT_RULES: ContentRules = { cars: CARS.map((c) => c.id), tracks: TRACKS.map((t) => t.id) };

export type OnlinePhase = 'idle' | 'connecting' | 'lobby' | 'racing' | 'results' | 'error';

export interface LocalPlayer {
  device: DeviceId;
  name: string;
  car: string;
}

export interface WaitingSeat { seat: number; name: string; color: string; lost: boolean }

export interface OnlineStatus {
  /** Ida e volta até o relay, em ms (null antes do primeiro pong). */
  ping: number | null;
  delayTicks: number;
  delayMs: number;
  /** Assentos que seguram a corrida (só depois de `waitNoticeMs` parado). */
  waiting: WaitingSeat[];
  /** Este computador caiu e está tentando voltar: segundos restantes da janela. */
  reconnecting: number | null;
  /** Voltou e espera o estado do anfitrião. */
  syncing: boolean;
  desync: DesyncReport | null;
  tick: number;
}

/** O que a sessão oferece ao online. */
export interface OnlineHost {
  readonly settings: Settings;
  readonly save: SaveData;
  readonly input: InputProvider;
  /** Começa a corrida online (ou a retoma de um snapshot, com `state`). */
  startRace(config: RaceConfig, localSeats: number[], driver: RaceDriver, state?: RaceState): void;
  raceState(): RaceState | null;
  /** Tira a corrida da tela (volta para a sala) sem sair do online. */
  clearRace(): void;
  /** Mostra a tela online (sala, confirmação de saída, resultado, erro). */
  showScreen(): void;
  hideScreen(): void;
  menuOpen(): boolean;
  /** Fim do online: menu principal. */
  exitToMain(): void;
  persistSettings(): void;
}

export interface OnlineHud {
  update(status: OnlineStatus): void;
  dispose(): void;
}

export interface OnlineOptions {
  socket?: SocketFactory;
  hud?: () => OnlineHud;
  now?: () => number;
  /** Parado esperando a rede por mais que isto → aviso "aguardando". */
  waitNoticeMs?: number;
  reconnectWindowMs?: number;
  retryMs?: number;
  pingMs?: number;
  /** Parado há mais que isto → reenvia as entradas recentes (ver Lockstep). */
  resendMs?: number;
  random?: () => number;
}

type Listener = () => void;

function defaultNow(): number {
  return typeof performance !== 'undefined' ? performance.now() : Date.now();
}

/**
 * Nome mostrado para o assento: o padrão "P1"…"P4" (cada computador começa com o seu "P1")
 * acompanha o assento global recebido; nome escolhido pelo jogador fica como está.
 */
export function seatName(name: string, seat: number): string {
  return /^P[1-4]$/.test(name) ? `P${seat + 1}` : name;
}

/** Assentos globais pela ordem dos clientes e dos jogadores de cada um (o anfitrião chama). */
export function assignSeats(room: RoomView): SeatAssignment[] {
  const out: SeatAssignment[] = [];
  for (const c of [...room.clients].sort((a, b) => a.id - b.id)) {
    for (const p of c.info?.players ?? []) {
      if (out.length >= MAX_HUMANS) return out;
      out.push({ seat: out.length, client: c.id, name: seatName(p.name, out.length), car: p.car });
    }
  }
  return out;
}

/** A configuração da corrida que todas as máquinas montam a partir da largada. */
export function raceConfigFrom(cfg: StartConfig): RaceConfig {
  const humans: HumanEntry[] = cfg.seats.map((s) => ({
    seat: s.seat, name: s.name, carId: s.car, teamId: cfg.versus ? s.seat : 0, color: SEAT_COLORS[s.seat] ?? '#ffffff',
  }));
  return {
    trackId: cfg.trackId, laps: cfg.laps, humans, totalCars: Math.max(cfg.totalCars, humans.length),
    difficulty: cfg.difficulty, manualGear: cfg.manualGear, assists: { ...cfg.assists }, seed: cfg.seed,
  };
}

export class OnlineController implements RaceDriver {
  phase: OnlinePhase = 'idle';
  /** Chave de texto do último erro (`online.err.*`). */
  error: string | null = null;
  room: RoomView | null = null;
  myId = -1;
  code = '';
  locals: LocalPlayer[] = [];
  ready = false;
  start: StartConfig | null = null;
  results: ResultsScreenData | null = null;
  /** "Sair da partida?" aberto durante a corrida. */
  quitOpen = false;
  ping: number | null = null;
  desync: DesyncReport | null = null;
  /** Mensagens que chegaram mas não valiam (forma ou remetente). */
  dropped = 0;

  private readonly host: OnlineHost;
  private readonly opts: OnlineOptions;
  private client: NetClient | null = null;
  private lockstep: Lockstep | null = null;
  private token = '';
  private listeners = new Set<Listener>();
  private pending: ClientMessage | null = null;
  private pingTimer: ReturnType<typeof setInterval> | null = null;
  private retryTimer: ReturnType<typeof setTimeout> | null = null;
  private pingSent = new Map<number, number>();
  private pingSeq = 0;
  private reconnectSince: number | null = null;
  private awaitingSnapshot = false;
  private queued: Array<{ from: number; records: InputRecord[] }> = [];
  private backlog = 0;
  /** Registros de entrada a enviar: sai uma mensagem por quadro, não uma por tick. */
  private outbox: InputRecord[] = [];
  private hud: OnlineHud | null = null;
  private localSeatList: number[] = [];

  constructor(host: OnlineHost, opts: OnlineOptions = {}) {
    this.host = host;
    this.opts = opts;
  }

  // ───────────────────────────── Estado para a interface ─────────────────────────────

  get isHost(): boolean {
    return this.room !== null && this.room.host === this.myId;
  }

  get localSeats(): readonly number[] {
    return this.localSeatList;
  }

  onChange(fn: Listener): () => void {
    this.listeners.add(fn);
    return () => this.listeners.delete(fn);
  }

  private changed(): void {
    for (const fn of [...this.listeners]) fn();
  }

  private now(): number {
    return (this.opts.now ?? defaultNow)();
  }

  /** Total de jogadores humanos na sala (declarados pelos clientes). */
  roomSeats(): number {
    return this.room ? this.room.clients.reduce((n, c) => n + c.seats, 0) : this.locals.length;
  }

  /** Por que o anfitrião ainda não pode largar (chave de texto), ou null se pode. */
  startBlocker(): string | null {
    const room = this.room;
    if (!room || !this.isHost) return 'online.lobby.waitHost';
    if (room.clients.length < 2) return 'online.lobby.needPeer';
    if (room.clients.some((c) => !c.connected || !c.info)) return 'online.lobby.waitPeers';
    if (room.clients.some((c) => c.id !== room.host && !c.info?.ready)) return 'online.lobby.waitReady';
    if (this.roomSeats() > MAX_HUMANS) return 'online.err.full';
    if (!room.settings) return 'online.lobby.waitHost';
    return null;
  }

  status(): OnlineStatus {
    const now = this.now();
    const delay = this.start?.delay ?? this.room?.settings?.delay ?? DEFAULT_INPUT_DELAY;
    const waiting: WaitingSeat[] = [];
    const ls = this.lockstep;
    if (ls && this.start && ls.stalledMs(now) >= (this.opts.waitNoticeMs ?? 250)) {
      for (const seat of ls.missingSeats()) {
        const a = this.start.seats.find((s) => s.seat === seat);
        const client = this.room?.clients.find((c) => c.id === a?.client);
        waiting.push({ seat, name: a?.name ?? `P${seat + 1}`, color: SEAT_COLORS[seat] ?? '#fff', lost: !client || !client.connected });
      }
    }
    const windowMs = this.opts.reconnectWindowMs ?? RECONNECT_WINDOW_MS;
    return {
      ping: this.ping, delayTicks: delay, delayMs: Math.round((delay * 1000) / TICK_RATE), waiting,
      reconnecting: this.reconnectSince === null ? null : Math.max(0, Math.ceil((windowMs - (now - this.reconnectSince)) / 1000)),
      syncing: this.awaitingSnapshot, desync: this.desync, tick: ls?.tick ?? 0,
    };
  }

  /** Para o playtest e para depuração (window.nc). */
  debugInfo(): Record<string, unknown> {
    return {
      phase: this.phase, room: this.room, myId: this.myId, isHost: this.isHost, localSeats: this.localSeatList,
      tick: this.lockstep?.tick ?? null, stats: this.lockstep?.stats ?? null, desyncs: this.lockstep?.desyncs ?? [],
      dropped: this.dropped, invalid: this.client?.stats.invalid ?? 0, ping: this.ping,
    };
  }

  // ───────────────────────────── Jogadores locais ─────────────────────────────

  private defaultPlayer(index: number, device: DeviceId): LocalPlayer {
    const { save } = this.host;
    const car = save.seatCars[index] ?? CARS[0].id;
    return { device, name: (save.seatNames[index] ?? `P${index + 1}`).slice(0, 12), car: CONTENT_RULES.cars.includes(car) ? car : CARS[0].id };
  }

  /** Garante o jogador 1 deste computador (com o dispositivo que abriu a tela). */
  ensurePrimary(device: DeviceId): void {
    if (this.locals.length === 0) this.locals.push(this.defaultPlayer(0, device));
    else if (this.phase === 'idle' || this.phase === 'error') this.locals[0].device = device;
  }

  /** Segundo jogador deste computador (tela dividida + online). */
  addLocal(device: DeviceId): boolean {
    if (this.phase !== 'lobby' || this.locals.length >= MAX_LOCAL_PLAYERS || this.ready) return false;
    if (this.locals.some((p) => p.device === device)) return false;
    if (this.roomSeats() + 1 > MAX_HUMANS) { this.error = 'online.err.full'; this.changed(); return false; }
    this.locals.push(this.defaultPlayer(this.locals.length, device));
    this.error = null;
    this.publishInfo();
    return true;
  }

  removeLocal(index: number): void {
    if (index <= 0 || index >= this.locals.length || this.phase !== 'lobby') return;
    this.locals.splice(index, 1);
    this.ready = false;
    this.publishInfo();
  }

  setName(index: number, name: string): void {
    const p = this.locals[index];
    if (!p) return;
    p.name = name.slice(0, 12);
    this.publishInfo();
  }

  cycleCar(index: number, dir: -1 | 1): void {
    const p = this.locals[index];
    if (!p || this.ready) return;
    const ids = CONTENT_RULES.cars;
    p.car = ids[(ids.indexOf(p.car) + dir + ids.length) % ids.length];
    this.publishInfo();
  }

  toggleReady(): void {
    if (this.phase !== 'lobby') return;
    this.ready = !this.ready;
    this.publishInfo();
  }

  private info(): ClientInfo {
    return {
      players: this.locals.map((p, i) => ({ name: p.name.trim() || `P${i + 1}`, car: p.car })),
      ready: this.ready,
    };
  }

  private publishInfo(): void {
    this.send({ t: 'info', seats: Math.max(1, this.locals.length), info: this.info() });
    this.changed();
  }

  // ───────────────────────────── Sala ─────────────────────────────

  private defaultRoomSettings(): RoomSettings {
    const s = this.host.settings;
    return {
      trackId: TRACKS[0].id, laps: s.quickLaps, versus: false, difficulty: s.difficulty,
      totalCars: s.totalCars, manualGear: s.manualGear, assists: { ...s.assists }, delay: DEFAULT_INPUT_DELAY,
    };
  }

  /** Anfitrião muda a corrida (pista, voltas, modo…). */
  updateRoomSettings(patch: Partial<RoomSettings>): void {
    if (!this.isHost || !this.room || this.phase !== 'lobby') return;
    const next: RoomSettings = { ...(this.room.settings ?? this.defaultRoomSettings()), ...patch };
    next.laps = Math.max(1, Math.min(8, Math.round(next.laps)));
    next.totalCars = Math.max(1, Math.min(20, Math.round(next.totalCars)));
    next.delay = Math.max(MIN_INPUT_DELAY, Math.min(MAX_INPUT_DELAY, Math.round(next.delay)));
    this.room.settings = next;
    this.send({ t: 'settings', settings: next });
    this.changed();
  }

  setServerUrl(url: string): void {
    this.host.settings.serverUrl = url;
    this.host.persistSettings();
  }

  create(device: DeviceId): void {
    this.ensurePrimary(device);
    this.connect({ t: 'create', v: PROTOCOL_VERSION, seats: this.locals.length, info: this.info() });
  }

  join(codeText: string, device: DeviceId): boolean {
    const code = normalizeRoomCode(codeText);
    if (!code) { this.error = 'online.err.badCode'; this.phase = 'error'; this.changed(); return false; }
    this.ensurePrimary(device);
    this.connect({ t: 'join', v: PROTOCOL_VERSION, room: code, seats: this.locals.length, info: this.info() });
    return true;
  }

  private connect(first: ClientMessage): void {
    this.dropConnection();
    this.phase = 'connecting';
    this.error = null;
    this.ready = false;
    this.room = null;
    this.pending = first;
    this.openClient();
    this.changed();
  }

  private openClient(): void {
    const client = new NetClient(this.host.settings.serverUrl, CONTENT_RULES, {
      onOpen: () => {
        if (this.client !== client) return;
        if (this.pending) { client.send(this.pending); this.pending = null; }
      },
      onMessage: (msg) => { if (this.client === client) this.handle(msg); },
      onClose: (info) => { if (this.client === client) this.handleClose(info.wasOpen); },
    }, this.opts.socket);
    this.client = client;
  }

  private send(msg: ClientMessage): void {
    this.client?.send(msg);
  }

  // ───────────────────────────── Mensagens ─────────────────────────────

  private handle(msg: ServerMessage): void {
    switch (msg.t) {
      case 'welcome': return this.onWelcome(msg.room, msg.id, msg.token, msg.rejoined);
      case 'room': return this.onRoom(msg.room);
      case 'peer':
        if (msg.e === 'rejoin' && this.isHost) this.sendSnapshot(msg.id);
        this.changed();
        return;
      case 'error': return this.onError(msg.code);
      case 'pong': {
        const sent = this.pingSent.get(msg.n);
        if (sent !== undefined) {
          const rtt = this.now() - sent;
          this.ping = this.ping === null ? rtt : this.ping * 0.7 + rtt * 0.3;
          this.pingSent.delete(msg.n);
        }
        return;
      }
      case 'start':
        if (!this.room || msg.from !== this.room.host) { this.dropped++; return; }
        return this.beginRace(msg.cfg);
      case 'i':
        if (this.awaitingSnapshot) { if (this.queued.length < 2000) this.queued.push({ from: msg.from, records: msg.records }); return; }
        return this.receiveInputs(msg.from, msg.records);
      case 'h':
        this.lockstep?.receiveHash(msg.from, msg.k, msg.h);
        return;
      case 'snap':
        if (!this.awaitingSnapshot || !this.room || msg.from !== this.room.host) { this.dropped++; return; }
        return this.applySnapshot(msg.snap);
    }
  }

  private onWelcome(code: string, id: number, token: string, rejoined: boolean): void {
    this.code = code;
    this.myId = id;
    this.token = token;
    this.startPing();
    if (rejoined) {
      this.reconnectSince = null;
      if (this.retryTimer) { clearTimeout(this.retryTimer); this.retryTimer = null; }
      // Na corrida, espera o estado do anfitrião; no resultado não há o que sincronizar.
      if (this.phase === 'racing') { this.awaitingSnapshot = true; this.queued = []; }
    } else {
      this.phase = 'lobby';
      this.localSeatList = [];
    }
    this.changed();
  }

  private onRoom(room: RoomView): void {
    const wasHost = this.isHost;
    this.room = room;
    if (this.phase === 'lobby' && this.isHost && !room.settings) {
      room.settings = this.defaultRoomSettings();
      this.send({ t: 'settings', settings: room.settings });
    }
    // Anfitrião (de nascença ou por sucessão): quem sumiu da sala no meio da corrida vira IA.
    if (this.isHost && this.lockstep && this.start) {
      for (const s of this.start.seats) {
        if (s.client !== this.myId && !room.clients.some((c) => c.id === s.client) && !this.lockstep.hasTakeover(s.seat)) {
          this.lockstep.takeover(s.seat);
        }
      }
      this.flushOutbox();
    }
    // Quem herdou a sala no lobby não precisa mais do "pronto" (o anfitrião larga).
    if (!wasHost && this.isHost && this.phase === 'lobby' && this.ready) { this.ready = false; this.publishInfo(); return; }
    this.changed();
  }

  private onError(code: ErrorCode): void {
    if (code === 'rate' || code === 'bad' || code === 'not_host') { this.dropped++; return; }
    if (this.reconnectSince !== null && (code === 'expired' || code === 'no_room' || code === 'version')) {
      this.fail('online.err.expired');
      return;
    }
    if (code === 'full' && this.phase === 'lobby') {
      // O segundo jogador local não coube: volta a um.
      if (this.locals.length > 1) { this.locals.length = 1; this.publishInfo(); }
      this.error = 'online.err.full';
      this.changed();
      return;
    }
    this.fail(`online.err.${code}`);
  }

  /** Erro que encerra a conexão: tela de erro (e a corrida, se houver, sai da tela). */
  private fail(errorKey: string): void {
    const wasRacing = this.phase === 'racing' || this.phase === 'results';
    this.dropConnection();
    this.error = errorKey;
    this.phase = 'error';
    this.room = null;
    this.endRaceState();
    if (wasRacing) this.host.clearRace();
    this.host.showScreen();
    this.changed();
  }

  private handleClose(wasOpen: boolean): void {
    this.client = null;
    this.stopPing();
    if (this.phase === 'racing' || this.phase === 'results') {
      if (this.reconnectSince === null) this.reconnectSince = this.now();
      this.scheduleRetry();
      this.changed();
      return;
    }
    if (this.reconnectSince !== null) { this.scheduleRetry(); return; }
    this.error = wasOpen ? 'online.err.lost' : 'online.err.connect';
    this.phase = 'error';
    this.room = null;
    this.changed();
  }

  /** Tenta voltar à sala com o mesmo token até a janela de reconexão acabar. */
  private scheduleRetry(): void {
    if (this.retryTimer) return;
    const since = this.reconnectSince ?? this.now();
    const windowMs = this.opts.reconnectWindowMs ?? RECONNECT_WINDOW_MS;
    if (this.now() - since > windowMs) { this.fail('online.err.expired'); return; }
    this.retryTimer = setTimeout(() => {
      this.retryTimer = null;
      if (this.reconnectSince === null) return;
      this.pending = { t: 'rejoin', v: PROTOCOL_VERSION, room: this.code, token: this.token };
      this.openClient();
    }, this.opts.retryMs ?? 1000);
  }

  private startPing(): void {
    this.stopPing();
    const ping = () => {
      this.pingSeq = (this.pingSeq + 1) % 0x7fffffff;
      this.pingSent.set(this.pingSeq, this.now());
      if (this.pingSent.size > 20) this.pingSent.delete(this.pingSent.keys().next().value as number);
      this.send({ t: 'ping', n: this.pingSeq });
    };
    ping();
    this.pingTimer = setInterval(ping, this.opts.pingMs ?? 1000);
  }

  private stopPing(): void {
    if (this.pingTimer) { clearInterval(this.pingTimer); this.pingTimer = null; }
  }

  private dropConnection(): void {
    this.stopPing();
    if (this.retryTimer) { clearTimeout(this.retryTimer); this.retryTimer = null; }
    this.reconnectSince = null;
    this.client?.close();
    this.client = null;
  }

  // ───────────────────────────── Largada ─────────────────────────────

  /** Anfitrião: todos prontos → largada com os assentos numerados. */
  startRace(): boolean {
    if (this.startBlocker() !== null || !this.room?.settings) return false;
    const random = this.opts.random ?? Math.random;
    const cfg: StartConfig = { ...this.room.settings, seed: Math.floor(random() * 0xffffffff) >>> 0, seats: assignSeats(this.room) };
    cfg.totalCars = Math.max(cfg.totalCars, cfg.seats.length);
    this.send({ t: 'start', cfg });
    return true;
  }

  private beginRace(cfg: StartConfig): void {
    if (this.phase !== 'lobby') { this.dropped++; return; }
    const mine = cfg.seats.filter((s) => s.client === this.myId).sort((a, b) => a.seat - b.seat);
    if (mine.length === 0) { this.fail('online.err.noSeat'); return; }
    this.start = cfg;
    this.localSeatList = mine.map((s) => s.seat);
    this.bindDevices();
    this.lockstep = this.newLockstep(cfg, 0, []);
    this.phase = 'racing';
    // O "pronto" vale para uma largada só: na volta à sala todos confirmam de novo. Sem isso o
    // anfitrião largaria com um convidado ainda no resultado, que ficaria de fora da corrida.
    if (this.ready) { this.ready = false; this.send({ t: 'info', seats: Math.max(1, this.locals.length), info: this.info() }); }
    this.quitOpen = false;
    this.desync = null;
    this.results = null;
    this.backlog = 0;
    this.hud?.dispose();
    this.hud = this.opts.hud ? this.opts.hud() : null;
    this.host.startRace(raceConfigFrom(cfg), [...this.localSeatList], this);
    this.changed();
  }

  /** Os controles deste computador passam a responder pelos assentos globais recebidos. */
  private bindDevices(): void {
    const { input } = this.host;
    for (let seat = 0; seat < MAX_HUMANS; seat++) input.unbindSeat(seat);
    this.localSeatList.forEach((seat, i) => { const p = this.locals[i]; if (p) input.bindSeat(seat, p.device); });
  }

  private newLockstep(cfg: StartConfig, startTick: number, ai: Array<[number, number]>): Lockstep {
    this.outbox = [];
    const ls = new Lockstep({ seats: cfg.seats.map((s) => s.seat), localSeats: this.localSeatList, delay: cfg.delay, startTick, ai, resendMs: this.opts.resendMs }, {
      sendInputs: (records) => { this.outbox.push(...records); },
      sendHash: (tick, hash) => this.send({ t: 'h', k: tick, h: hash }),
    });
    ls.onDesync = (report) => {
      if (!this.desync) this.desync = report;
      console.warn(`[online] dessincronia no tick ${report.tick}: local ${report.local} × cliente ${report.from} ${report.remote}`);
      this.changed();
    };
    return ls;
  }

  /** Uma mensagem por quadro (em pedaços de até 64 registros): o relay limita mensagens por segundo. */
  private flushOutbox(): void {
    while (this.outbox.length > 0) {
      const chunk = this.outbox.splice(0, MAX_RECORDS_PER_MESSAGE);
      this.send({ t: 'i', d: packRecords(chunk) });
    }
  }

  /** Só vale a entrada de quem é dono do assento; a tomada pela IA, só do anfitrião. */
  private receiveInputs(from: number, records: InputRecord[]): void {
    const ls = this.lockstep;
    if (!ls || !this.start) return;
    const ok: InputRecord[] = [];
    for (const r of records) {
      const owner = this.start.seats.find((s) => s.seat === r.seat)?.client;
      if (isTakeover(r) ? from === this.room?.host : owner === from) ok.push(r);
      else this.dropped++;
    }
    if (ok.length) ls.receive(ok);
  }

  // ───────────────────────────── Reconexão ─────────────────────────────

  private sendSnapshot(to: number): void {
    const state = this.host.raceState();
    if (!this.lockstep || !this.start || !state || state.tick !== this.lockstep.tick) return;
    const snap: Snapshot = {
      tick: state.tick, state: serializeRace(state), start: this.start,
      inputs: packRecords(this.lockstep.bufferedRecords()), ai: this.lockstep.aiList(),
    };
    this.send({ t: 'snap', to, snap });
  }

  private applySnapshot(snap: Snapshot): void {
    let state: RaceState;
    try {
      state = deserializeRace(snap.state);
    } catch {
      this.fail('online.err.snapshot');
      return;
    }
    if (state.tick !== snap.tick || state.trackId !== snap.start.trackId) { this.fail('online.err.snapshot'); return; }
    const mine = snap.start.seats.filter((s) => s.client === this.myId).sort((a, b) => a.seat - b.seat);
    if (mine.length === 0) { this.fail('online.err.noSeat'); return; }
    this.start = snap.start;
    this.localSeatList = mine.map((s) => s.seat);
    this.bindDevices();
    const ai: Array<[number, number]> = [];
    for (let i = 0; i < snap.ai.length; i += 2) ai.push([snap.ai[i], snap.ai[i + 1]]);
    this.lockstep = this.newLockstep(snap.start, snap.tick, ai);
    this.lockstep.ingest(unpackRecordList(snap.inputs) ?? []);
    this.awaitingSnapshot = false;
    for (const q of this.queued) this.receiveInputs(q.from, q.records);
    this.queued = [];
    this.backlog = 0;
    if (!this.hud && this.opts.hud) this.hud = this.opts.hud();
    this.host.startRace(raceConfigFrom(snap.start), [...this.localSeatList], this, state);
    this.changed();
  }

  // ───────────────────────────── RaceDriver ─────────────────────────────

  private feedLocal(local: PlayerInput[]): void {
    const ls = this.lockstep;
    if (!ls) return;
    const blocked = this.quitOpen || this.host.menuOpen();
    for (const seat of this.localSeatList) ls.setLocalInput(seat, blocked ? NEUTRAL_INPUT : local[seat] ?? NEUTRAL_INPUT);
  }

  private run(budget: number, step: (inputs: PlayerInput[]) => void): number {
    const ls = this.lockstep;
    const state = this.host.raceState();
    if (!ls || !state || this.awaitingSnapshot || this.reconnectSince !== null) return 0;
    const now = this.now();
    let n = 0;
    while (n < budget && ls.step(state, now, step)) n++;
    // Quadro sem tick a rodar: ainda confere se está parado há tempo de reenviar.
    if (budget === 0) ls.checkStall(now);
    this.flushOutbox();
    return n;
  }

  advance(dt: number, local: PlayerInput[], step: (inputs: PlayerInput[]) => void): number {
    this.feedLocal(local);
    this.backlog = Math.min(MAX_BACKLOG, this.backlog + Math.max(0, dt));
    let budget = Math.floor(this.backlog / DT + 1e-9);
    const behind = this.lockstep?.behindBy() ?? 0;
    if (behind > 2) budget += Math.min(MAX_CATCHUP, behind - 2);
    const n = this.run(budget, step);
    this.backlog = Math.max(0, this.backlog - n * DT);
    this.hud?.update(this.status());
    return n;
  }

  force(ticks: number, local: PlayerInput[], step: (inputs: PlayerInput[]) => void): number {
    this.feedLocal(local);
    const n = this.run(ticks, step);
    this.hud?.update(this.status());
    return n;
  }

  pauseKey(): void {
    if (this.phase !== 'racing') return;
    this.quitOpen = true;
    this.host.showScreen();
    this.changed();
  }

  /** Fecha o "Sair da partida?" e volta a correr. */
  closeQuit(): void {
    this.quitOpen = false;
    this.host.hideScreen();
    this.changed();
  }

  finished(data: ResultsScreenData): void {
    this.results = data;
    this.phase = 'results';
    this.quitOpen = false;
    this.hud?.dispose();
    this.hud = null;
    this.host.showScreen();
    this.changed();
  }

  dispose(): void {
    // A sessão largou a corrida (menu principal): o online acaba junto.
    if (this.phase !== 'idle') this.leave(false);
  }

  // ───────────────────────────── Saídas ─────────────────────────────

  private endRaceState(): void {
    this.lockstep = null;
    this.start = null;
    this.awaitingSnapshot = false;
    this.queued = [];
    this.quitOpen = false;
    this.localSeatList = [];
    this.hud?.dispose();
    this.hud = null;
  }

  /** Depois do resultado: de volta à sala (o anfitrião reabre a sala para a próxima). */
  backToRoom(): void {
    if (this.phase !== 'results') return;
    this.endRaceState();
    this.phase = 'lobby';
    this.ready = false;
    this.results = null;
    this.host.clearRace();
    if (this.isHost) this.send({ t: 'lobby' });
    this.publishInfo();
    this.host.showScreen();
  }

  /** Sai da sala (e da corrida, se houver). `toMain` falso quando quem chama já está indo ao menu. */
  leave(toMain = true): void {
    this.send({ t: 'leave' });
    this.dropConnection();
    const hadRace = this.lockstep !== null || this.phase === 'results';
    this.endRaceState();
    this.phase = 'idle';
    this.room = null;
    this.myId = -1;
    this.code = '';
    this.ready = false;
    this.error = null;
    this.results = null;
    this.locals = this.locals.slice(0, 1);
    if (toMain) this.host.exitToMain();
    else if (hadRace) this.host.clearRace();
    this.changed();
  }

  /** Tela de erro → volta ao início do online. */
  resetError(): void {
    this.error = null;
    this.phase = 'idle';
    this.changed();
  }

  /** Para testes: derruba a conexão como se a rede tivesse caído (sem avisar o relay). */
  debugDropConnection(): void {
    const c = this.client;
    if (!c) return;
    this.client = null;
    c.close();
    this.handleClose(true);
  }

  /** Para testes: some de vez (a conexão cai e ninguém tenta voltar). */
  debugVanish(): void {
    this.dropConnection();
    this.endRaceState();
    this.phase = 'idle';
    this.changed();
  }

  /** Para o playtest: hash do estado local num tick múltiplo do intervalo de hash. */
  hashAt(tick: number): number | undefined {
    return this.lockstep?.hashAt(tick);
  }
}

export function createOnlineController(host: OnlineHost, opts: OnlineOptions = {}): OnlineController {
  return new OnlineController(host, opts);
}
