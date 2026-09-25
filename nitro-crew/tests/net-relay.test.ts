// Integração com o relay de verdade: sobe server/relay.mjs numa porta livre e conversa com ele
// pelo WebSocket global do Node 22. Parte 1: regras do relay com mensagens cruas. Parte 2: duas
// sessões online completas (sem DOM) — sala, lobby, largada, 600 ticks em lockstep com hashes
// iguais, queda e volta por snapshot, e tomada do assento pela IA quando alguém não volta.
// Precisa de `npm ci` em server/ (o CI faz; localmente o teste é pulado se o ws não estiver lá,
// a menos que NC_REQUIRE_RELAY=1).
import { spawn, type ChildProcess } from 'node:child_process';
import fs from 'node:fs';
import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import { aiInput } from '../src/core/sim/ai';
import { deserializeRace, hashRace, serializeRace } from '../src/core/serialize';
import { createRace, stepRace } from '../src/core/sim/race';
import { getTrack } from '../src/core/track';
import type { PlayerInput, RaceConfig, RaceState, Track } from '../src/core/types';
import { DEFAULT_SAVE, DEFAULT_SETTINGS, type DeviceId, type InputProvider, type MenuNav, type RaceDriver, type ResultsScreenData, type SaveData, type Settings } from '../src/game/contracts';
import { OnlineController, type OnlineHost } from '../src/game/online-session';

const HAS_WS = fs.existsSync('server/node_modules/ws/package.json');
if (!HAS_WS && process.env.NC_REQUIRE_RELAY === '1') throw new Error('server/node_modules/ws não instalado: rode `npm ci` em nitro-crew/server');
const RECONNECT_MS = 1500;

let relay: ChildProcess | null = null;
let port = 0;

const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));

async function until(cond: () => boolean, timeoutMs = 5000, label = 'condição'): Promise<void> {
  const t0 = Date.now();
  while (!cond()) {
    if (Date.now() - t0 > timeoutMs) throw new Error(`tempo esgotado esperando ${label}`);
    await sleep(5);
  }
}

// ───────────────────────────── Cliente cru ─────────────────────────────

interface Raw {
  ws: WebSocket;
  msgs: Array<Record<string, unknown>>;
  closed: { code: number } | null;
  send(m: unknown): void;
  next(pred: (m: Record<string, unknown>) => boolean, label?: string): Promise<Record<string, unknown>>;
}

async function raw(): Promise<Raw> {
  const ws = new WebSocket(`ws://127.0.0.1:${port}`);
  const r: Raw = {
    ws, msgs: [], closed: null,
    send: (m) => ws.send(typeof m === 'string' ? m : JSON.stringify(m)),
    async next(pred, label = 'mensagem') {
      let found: Record<string, unknown> | undefined;
      await until(() => { found = r.msgs.find(pred); return found !== undefined; }, 3000, label);
      r.msgs.splice(r.msgs.indexOf(found as Record<string, unknown>), 1);
      return found as Record<string, unknown>;
    },
  };
  ws.onmessage = (e) => r.msgs.push(JSON.parse(String(e.data)) as Record<string, unknown>);
  ws.onclose = (e) => { r.closed = { code: e.code }; };
  await new Promise<void>((res, rej) => { ws.onopen = () => res(); ws.onerror = () => rej(new Error('não conectou')); });
  return r;
}

const INFO = (name: string) => ({ players: [{ name, car: 'falcao' }], ready: false });
const isT = (t: string) => (m: Record<string, unknown>) => m.t === t;

// ───────────────────────────── Sessão falsa (sem DOM) ─────────────────────────────

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

interface FakeRace {
  state: RaceState; track: Track; driver: RaceDriver; localSeats: number[]; starts: number;
  /** Segundos desde o fim (a sessão mostra o resultado 3 s depois) e se já mostrou. */
  overFor: number; reported: boolean;
}

class FakeHost implements OnlineHost {
  readonly settings: Settings = { ...DEFAULT_SETTINGS, assists: { ...DEFAULT_SETTINGS.assists }, totalCars: 8 };
  readonly save: SaveData = { ...DEFAULT_SAVE, seatNames: ['Ana', 'Bia', 'Caio', 'Duda'], seatCars: [...DEFAULT_SAVE.seatCars] };
  readonly input = fakeInput();
  race: FakeRace | null = null;
  debug: (() => unknown) | null = null;
  starts = 0;
  exited = false;
  /** hashRace a cada 60 ticks, tirado do estado deste "computador". */
  readonly hashes = new Map<number, number>();
  constructor(name: string) { this.save.seatNames = [name, `${name}2`, 'P3', 'P4']; this.settings.serverUrl = `ws://127.0.0.1:${port}`; }
  startRace(config: RaceConfig, localSeats: number[], driver: RaceDriver, state?: RaceState): void {
    const track = getTrack(config.trackId);
    this.starts++;
    this.race = { state: state ?? createRace(config, track), track, driver, localSeats, starts: this.starts, overFor: 0, reported: false };
  }
  raceState() { return this.race?.state ?? null; }
  clearRace() { this.race = null; }
  showScreen() {}
  hideScreen() {}
  menuOpen() { return false; }
  exitToMain() { this.race = null; this.exited = true; }
  persistSettings() {}
  results: ResultsScreenData | null = null;
  /**
   * Um quadro de verdade, como session.frame: `dt` segundos pelo `advance` (orçamento do quadro e
   * alcance), piloto automático nos assentos locais e o resultado 3 s depois do fim.
   */
  frame(dt: number): number {
    const r = this.race;
    if (!r) return 0;
    const local: PlayerInput[] = [];
    for (const seat of r.localSeats) local[seat] = autopilot(r.state, r.track, seat);
    const n = r.driver.advance(dt, local, (inputs) => {
      stepRace(r.state, r.track, inputs);
      if (r.state.tick % 60 === 0) this.hashes.set(r.state.tick, hashRace(r.state));
    });
    if (r.state.phase === 'finished' && !r.reported) {
      r.overFor += dt;
      if (r.overFor >= 3) {
        r.reported = true;
        this.results = { mode: 'quick', trackDef: r.track.def, results: r.state.results ?? [], humans: [], champ: null, newRecords: [] };
        r.driver.finished(this.results);
      }
    }
    return n;
  }
  /**
   * Janela escondida (aba em segundo plano, minimizada): o teste para de chamar `frame`, como o
   * navegador para o requestAnimationFrame; o online anda pelo `runHidden` (a sessão de verdade o liga
   * ao `advance`, igual aqui).
   */
  hiddenNow = false;
  hidden() { return this.hiddenNow; }
  runHidden(dt: number): void {
    const r = this.race;
    if (!r) return;
    r.driver.advance(dt, [], (inputs) => {
      stepRace(r.state, r.track, inputs);
      if (r.state.tick % 60 === 0) this.hashes.set(r.state.tick, hashRace(r.state));
    });
  }
  /** Um "quadro": até `max` ticks com o piloto de teste nos assentos locais. */
  pump(max: number, limit: number): number {
    const r = this.race;
    if (!r) return 0;
    const local: PlayerInput[] = [];
    for (const seat of r.localSeats) local[seat] = pilot(seat, r.state.tick);
    return r.driver.force(Math.max(0, Math.min(max, limit - r.state.tick)), local, (inputs) => {
      stepRace(r.state, r.track, inputs);
      if (r.state.tick % 60 === 0) this.hashes.set(r.state.tick, hashRace(r.state));
    });
  }
}

/** O cérebro da IA dirigindo por um humano, numa cópia do estado (o de verdade só muda no stepRace). */
function autopilot(state: RaceState, track: Track, seat: number): PlayerInput {
  const clone = deserializeRace(serializeRace(state));
  const car = clone.cars.find((c) => c.seat === seat);
  if (!car) return { steer: 0, throttle: false, brake: false, nitro: false, gearUp: false, gearDown: false };
  car.ai = { skill: 0.95, laneX: 0, laneUntil: 0, lookahead: 28, aggression: 0.5 };
  return aiInput(clone, track, car);
}

function pilot(seat: number, tick: number): PlayerInput {
  const phase = (tick + seat * 41) % 200;
  return { steer: phase < 70 ? 0.42 : phase < 140 ? -0.33 : 0, throttle: true, brake: phase > 190, nitro: tick % 500 === 250 + seat, gearUp: false, gearDown: false };
}

/** Roda os "computadores" (com pausas para a rede entregar) até todos chegarem a `limit`. */
async function race(hosts: FakeHost[], limit: number, timeoutMs = 15000): Promise<void> {
  const t0 = Date.now();
  while (hosts.some((h) => (h.race?.state.tick ?? limit) < limit)) {
    if (Date.now() - t0 > timeoutMs) throw new Error(`corrida travou: ${hosts.map((h) => `${h.race?.state.tick} ${JSON.stringify(h.debug?.())}`).join(' | ')}`);
    for (const h of hosts) h.pump(8, limit);
    // ~100 mensagens/s por computador: abaixo do limite de taxa do relay (150/s), como no jogo (60/s).
    await sleep(10);
  }
}

beforeAll(async () => {
  if (!HAS_WS) return;
  relay = spawn(process.execPath, ['server/relay.mjs'], { env: { ...process.env, PORT: '0', HOST: '127.0.0.1', RELAY_RECONNECT_MS: String(RECONNECT_MS), RELAY_QUIET: '1' }, stdio: ['ignore', 'pipe', 'pipe'] });
  port = await new Promise<number>((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error('relay não subiu')), 5000);
    relay?.stdout?.on('data', (d: Buffer) => { const m = String(d).match(/:(\d+) \(/); if (m) { clearTimeout(timer); resolve(Number(m[1])); } });
    relay?.on('exit', (code) => reject(new Error(`relay saiu com código ${code}`)));
  });
});

afterAll(() => {
  relay?.kill();
});

describe.skipIf(!HAS_WS)('relay (mensagens cruas)', () => {
  it('cria sala com código de 5 letras, recebe quem entra e repassa entradas só para os outros', async () => {
    const a = await raw();
    a.send({ t: 'create', v: 1, seats: 1, info: INFO('Ana') });
    const w = await a.next(isT('welcome'));
    expect(String(w.room)).toMatch(/^[A-HJ-NP-Z]{5}$/);
    const b = await raw();
    b.send({ t: 'join', v: 1, room: String(w.room).toLowerCase(), seats: 2, info: INFO('Bia') });
    expect(await b.next(isT('welcome'))).toMatchObject({ id: 1, rejoined: false });
    expect(await a.next((m) => m.t === 'peer' && m.e === 'join')).toMatchObject({ id: 1 });
    a.send({ t: 'start', cfg: { qualquer: 1 } });
    expect(await b.next(isT('start'))).toMatchObject({ from: 0 });
    expect(await a.next(isT('start'))).toMatchObject({ from: 0 }); // o anfitrião também recebe
    b.send({ t: 'i', d: [10, 1, 1, -5] });
    expect(await a.next(isT('i'))).toEqual({ t: 'i', from: 1, d: [10, 1, 1, -5] });
    await sleep(50);
    expect(b.msgs.some((m) => m.t === 'i')).toBe(false);
    a.ws.close(); b.ws.close();
  });

  it('recusa sala inexistente, versão errada, sala cheia, assentos demais e sala em corrida', async () => {
    const a = await raw();
    a.send({ t: 'join', v: 1, room: 'ZZZZZ', seats: 1, info: INFO('x') });
    expect(await a.next(isT('error'))).toEqual({ t: 'error', code: 'no_room' });
    a.send({ t: 'create', v: 99, seats: 1, info: INFO('x') });
    expect(await a.next(isT('error'))).toEqual({ t: 'error', code: 'version' });
    a.send({ t: 'create', v: 1, seats: 2, info: INFO('Ana') });
    const room = String((await a.next(isT('welcome'))).room);
    const b = await raw();
    b.send({ t: 'join', v: 1, room, seats: 2, info: INFO('Bia') });
    await b.next(isT('welcome'));
    const c = await raw();
    c.send({ t: 'join', v: 1, room, seats: 1, info: INFO('Caio') }); // 2 + 2 + 1 > 4 humanos
    expect(await c.next(isT('error'))).toEqual({ t: 'error', code: 'full' });
    b.send({ t: 'start', cfg: {} }); // só o anfitrião larga
    expect(await b.next(isT('error'))).toEqual({ t: 'error', code: 'not_host' });
    a.send({ t: 'start', cfg: {} });
    await a.next(isT('start'));
    c.send({ t: 'join', v: 1, room, seats: 1, info: INFO('Caio') });
    expect(await c.next(isT('error'))).toEqual({ t: 'error', code: 'started' });
    for (const x of [a, b, c]) x.ws.close();
  });

  it('só junta na mesma sala quem tem o mesmo conteúdo: impressão diferente é recusada com "build"', async () => {
    const a = await raw();
    a.send({ t: 'create', v: 1, b: 'aaaa1111', seats: 1, info: INFO('Ana') });
    const room = String((await a.next(isT('welcome'))).room);
    const b = await raw();
    b.send({ t: 'join', v: 1, b: 'bbbb2222', room, seats: 1, info: INFO('Bia') }); // build com uma pista nova
    expect(await b.next(isT('error'))).toEqual({ t: 'error', code: 'build' });
    b.send({ t: 'join', v: 1, room, seats: 1, info: INFO('Bia') }); // sem impressão nenhuma
    expect(await b.next(isT('error'))).toEqual({ t: 'error', code: 'build' });
    b.send({ t: 'join', v: 1, b: 'aaaa1111', room, seats: 1, info: INFO('Bia') });
    expect(await b.next(isT('welcome'))).toMatchObject({ id: 1 });
    const c = await raw();
    c.send({ t: 'create', v: 1, b: 'x'.repeat(65), seats: 1, info: INFO('Caio') }); // impressão absurda: mensagem inválida
    expect(await c.next(isT('error'))).toEqual({ t: 'error', code: 'bad' });
    for (const x of [a, b, c]) x.ws.close();
  });

  it('anfitrião que cai na corrida passa a sala a quem está conectado há mais tempo, não a quem acabou de voltar', async () => {
    const h = await raw();
    h.send({ t: 'create', v: 1, seats: 1, info: INFO('Hugo') });
    const room = String((await h.next(isT('welcome'))).room);
    const a = await raw();
    a.send({ t: 'join', v: 1, room, seats: 1, info: INFO('Ana') });
    const token = String((await a.next(isT('welcome'))).token);
    const c = await raw();
    c.send({ t: 'join', v: 1, room, seats: 1, info: INFO('Caio') });
    await c.next(isT('welcome'));
    h.send({ t: 'start', cfg: {} });
    await c.next(isT('start'));
    a.ws.close();
    await c.next((m) => m.t === 'peer' && m.e === 'lost');
    const a2 = await raw();
    a2.send({ t: 'rejoin', v: 1, room, token });
    await a2.next(isT('welcome'));
    h.ws.close();
    // Ana (id 1) acabou de voltar e espera o estado; Caio (id 2) nunca caiu: o estado completo é dele.
    const r = await c.next((m) => m.t === 'room' && (m.room as { clients: Array<{ id: number; connected: boolean }> }).clients.some((x) => x.id === 0 && !x.connected));
    expect((r.room as { host: number }).host).toBe(2);
    for (const x of [a2, c]) x.ws.close();
  });

  it('lixo, binário e mensagem acima do limite: descarta, e a grande derruba a conexão (1009)', async () => {
    const a = await raw();
    a.send('isto não é json');
    a.send({ t: 'nada' });
    a.send({ t: 'i', d: [1, 2, 3] });
    a.ws.send(new Uint8Array([1, 2, 3]));
    a.send({ t: 'ping', n: 1 });
    expect(await a.next(isT('pong'))).toEqual({ t: 'pong', n: 1 });
    a.send({ t: 'create', v: 1, seats: 1, info: { players: [], lixo: 'x'.repeat(70 * 1024) } });
    await until(() => a.closed !== null, 3000, 'fechar');
    expect(a.closed?.code).toBe(1009);
  });

  it('limite de taxa: rajada acima do permitido é descartada com aviso', async () => {
    const a = await raw();
    for (let i = 0; i < 500; i++) a.send({ t: 'ping', n: i });
    await a.next(isT('error'), 'erro de taxa');
    await sleep(100);
    const pongs = a.msgs.filter(isT('pong')).length;
    expect(pongs).toBeGreaterThanOrEqual(250);
    expect(pongs).toBeLessThan(500);
    a.ws.close();
  });

  it('no lobby, quem cai sai e o anfitrião passa adiante; sala vazia é apagada', async () => {
    const a = await raw();
    a.send({ t: 'create', v: 1, seats: 1, info: INFO('Ana') });
    const room = String((await a.next(isT('welcome'))).room);
    const b = await raw();
    b.send({ t: 'join', v: 1, room, seats: 1, info: INFO('Bia') });
    await b.next(isT('welcome'));
    a.ws.close();
    expect(await b.next((m) => m.t === 'peer' && m.e === 'leave')).toMatchObject({ id: 0 });
    const r = await b.next((m) => m.t === 'room' && (m.room as { clients: unknown[] }).clients.length === 1);
    expect((r.room as { host: number }).host).toBe(1);
    b.ws.close();
    await sleep(100);
    const c = await raw();
    c.send({ t: 'join', v: 1, room, seats: 1, info: INFO('Caio') });
    expect(await c.next(isT('error'))).toEqual({ t: 'error', code: 'no_room' });
    c.ws.close();
  });

  it('na corrida, quem cai tem o lugar guardado: volta com o token; token errado não entra; depois da janela, sai', async () => {
    const a = await raw();
    a.send({ t: 'create', v: 1, seats: 1, info: INFO('Ana') });
    const room = String((await a.next(isT('welcome'))).room);
    const b = await raw();
    b.send({ t: 'join', v: 1, room, seats: 1, info: INFO('Bia') });
    const token = String((await b.next(isT('welcome'))).token);
    a.send({ t: 'start', cfg: {} });
    await b.next(isT('start'));
    b.ws.close();
    expect(await a.next((m) => m.t === 'peer' && m.e === 'lost')).toMatchObject({ id: 1 });
    const intruder = await raw();
    intruder.send({ t: 'rejoin', v: 1, room, token: 'ffffffffffffffffffffffff' });
    expect(await intruder.next(isT('error'))).toEqual({ t: 'error', code: 'expired' });
    const b2 = await raw();
    b2.send({ t: 'rejoin', v: 1, room, token });
    expect(await b2.next(isT('welcome'))).toMatchObject({ id: 1, rejoined: true });
    expect(await a.next((m) => m.t === 'peer' && m.e === 'rejoin')).toMatchObject({ id: 1 });
    // Snapshot: só do anfitrião para um destinatário.
    a.send({ t: 'snap', to: 1, snap: { tick: 5 } });
    expect(await b2.next(isT('snap'))).toEqual({ t: 'snap', from: 0, snap: { tick: 5 } });
    b2.ws.close();
    await a.next((m) => m.t === 'peer' && m.e === 'lost');
    expect(await a.next((m) => m.t === 'peer' && m.e === 'drop', 'drop depois da janela')).toMatchObject({ id: 1 });
    const b3 = await raw();
    b3.send({ t: 'rejoin', v: 1, room, token });
    expect(await b3.next(isT('error'))).toEqual({ t: 'error', code: 'expired' });
    for (const x of [a, intruder, b3]) x.ws.close();
  }, 10_000);
});

describe.skipIf(!HAS_WS)('duas sessões online completas pelo relay', () => {
  it('lobby, largada, lockstep com hashes iguais, queda e volta por snapshot, e IA assumindo quem não volta', async () => {
    const ha = new FakeHost('Ana');
    const hb = new FakeHost('Bia');
    const A = new OnlineController(ha, { retryMs: 100, reconnectWindowMs: 10_000, pingMs: 200, resendMs: 150 });
    const B = new OnlineController(hb, { retryMs: 100, reconnectWindowMs: 10_000, pingMs: 200, resendMs: 150 });
    ha.debug = () => { const d = A.debugInfo(); return { dropped: d.dropped, stats: d.stats }; };
    hb.debug = () => { const d = B.debugInfo(); return { dropped: d.dropped, stats: d.stats }; };

    // Sala e lobby.
    A.create('kb1');
    await until(() => A.phase === 'lobby' && A.room !== null && A.room.settings !== null, 3000, 'sala criada');
    expect(A.isHost).toBe(true);
    B.join(A.code.toLowerCase(), 'gp0');
    await until(() => B.phase === 'lobby' && (A.room?.clients.length ?? 0) === 2, 3000, 'B na sala');
    expect(B.addLocal('kb2')).toBe(true); // dois jogadores no computador B
    await until(() => A.roomSeats() === 3, 3000, '3 jogadores');
    expect(A.startBlocker()).toBe('online.lobby.waitReady');
    A.updateRoomSettings({ trackId: 'copacabana', laps: 2, totalCars: 8 });
    B.toggleReady();
    await until(() => A.startBlocker() === null && B.room?.settings?.laps === 2, 3000, 'todos prontos');
    expect(B.startRace()).toBe(false); // convidado não larga
    expect(A.startRace()).toBe(true);
    await until(() => ha.race !== null && hb.race !== null, 3000, 'largada');

    // Assentos: A = 0; B = 1 e 2, com os controles de B ligados a eles.
    expect(ha.race?.localSeats).toEqual([0]);
    expect(hb.race?.localSeats).toEqual([1, 2]);
    expect(hb.input.seatDevice(1)).toBe('gp0');
    expect(hb.input.seatDevice(2)).toBe('kb2');
    expect(ha.race?.state.cars.filter((c) => c.seat >= 0).map((c) => c.name)).toEqual(['Ana', 'Bia', 'Bia2']);

    // 600 ticks em lockstep: mesmo hash nos dois computadores em cada troca.
    await race([ha, hb], 600);
    expect(ha.race?.state.tick).toBe(600);
    expect(hb.race?.state.tick).toBe(600);
    expect(hashRace(hb.race!.state)).toBe(hashRace(ha.race!.state));
    for (let t = 60; t <= 600; t += 60) expect(hb.hashes.get(t), `tick ${t}`).toBe(ha.hashes.get(t));
    expect(A.debugInfo().desyncs).toEqual([]);
    expect(A.ping).not.toBeNull();

    // B perde a conexão: A trava esperando; B volta com o token e recebe o snapshot.
    B.debugDropConnection();
    expect(B.status().reconnecting).not.toBeNull();
    for (let i = 0; i < 40; i++) { ha.pump(8, 1200); await sleep(2); }
    const stuckAt = ha.race?.state.tick ?? 0;
    expect(stuckAt).toBeLessThan(620);
    await until(() => (hb.race?.starts ?? 0) === 2, 5000, 'snapshot aplicado');
    expect(B.status().reconnecting).toBeNull();
    await race([ha, hb], 1200);
    expect(hashRace(hb.race!.state)).toBe(hashRace(ha.race!.state));
    expect(B.debugInfo().desyncs).toEqual([]);
    expect(A.debugInfo().desyncs).toEqual([]);

    // B some de vez: depois da janela do relay, o anfitrião põe a IA nos dois carros de B.
    B.debugVanish();
    await sleep(RECONNECT_MS + 300);
    await race([ha], 1500, 5000);
    const bCars = ha.race!.state.cars.filter((c) => c.seat === 1 || c.seat === 2);
    expect(bCars.every((c) => c.ai !== null)).toBe(true);
    expect(ha.race?.state.tick).toBe(1500);

    A.leave();
    expect(ha.exited).toBe(true);
  }, 30_000);

  it('os dois computadores caem juntos (soluço do relay): quem volta primeiro vira anfitrião, segue do próprio estado e manda o snapshot ao outro', async () => {
    const ha = new FakeHost('Ana');
    const hb = new FakeHost('Bia');
    // A tenta voltar antes de B: é ele quem o relay elege anfitrião (o anterior está desconectado).
    const A = new OnlineController(ha, { retryMs: 100, reconnectWindowMs: 10_000, pingMs: 200, resendMs: 150 });
    const B = new OnlineController(hb, { retryMs: 400, reconnectWindowMs: 10_000, pingMs: 200, resendMs: 150 });
    ha.debug = () => ({ host: A.isHost, ...A.status() });
    hb.debug = () => ({ host: B.isHost, ...B.status() });
    A.create('kb1');
    await until(() => A.phase === 'lobby' && A.room?.settings !== null && A.room?.settings !== undefined, 3000, 'sala criada');
    B.join(A.code, 'kb1');
    await until(() => B.phase === 'lobby' && (A.room?.clients.length ?? 0) === 2, 3000, 'B na sala');
    A.updateRoomSettings({ trackId: 'copacabana', laps: 2, totalCars: 6 });
    B.toggleReady();
    await until(() => A.startBlocker() === null, 3000, 'B pronto');
    expect(A.startRace()).toBe(true);
    await until(() => ha.race !== null && hb.race !== null, 3000, 'largada');
    await race([ha, hb], 300);

    A.debugDropConnection();
    B.debugDropConnection();
    // Ninguém fica esperando um snapshot que não vem: a corrida anda de novo, igual nos dois.
    await race([ha, hb], 900, 10_000);
    expect(A.isHost).toBe(true);
    expect(hb.race?.starts).toBe(2); // B recomeçou do snapshot de A
    expect(hashRace(hb.race!.state)).toBe(hashRace(ha.race!.state));
    for (let t = 360; t <= 900; t += 60) expect(hb.hashes.get(t), `tick ${t}`).toBe(ha.hashes.get(t));
    expect([...A.debugInfo().desyncs as unknown[], ...B.debugInfo().desyncs as unknown[]]).toEqual([]);
    A.leave(false); B.leave(false);
  }, 20_000);

  it('três computadores: o anfitrião cai sem mandar o snapshot a quem voltava; o novo anfitrião é quem ficou conectado, e ele manda', async () => {
    const [hh, ha, hc] = [new FakeHost('Hugo'), new FakeHost('Ana'), new FakeHost('Caio')];
    const opts = { retryMs: 100, reconnectWindowMs: 10_000, pingMs: 200, resendMs: 150 };
    const [H, A, C] = [new OnlineController(hh, opts), new OnlineController(ha, opts), new OnlineController(hc, opts)];
    ha.debug = () => ({ host: A.isHost, ...A.status() });
    hc.debug = () => ({ host: C.isHost, ...C.status() });
    H.create('kb1');
    await until(() => H.phase === 'lobby' && H.room?.settings !== null && H.room?.settings !== undefined, 3000, 'sala criada');
    A.join(H.code, 'kb1');
    await until(() => (H.room?.clients.length ?? 0) === 2, 3000, 'A na sala');
    C.join(H.code, 'kb1');
    await until(() => (H.room?.clients.length ?? 0) === 3, 3000, 'C na sala');
    H.updateRoomSettings({ trackId: 'copacabana', laps: 3, totalCars: 6 });
    A.toggleReady();
    C.toggleReady();
    await until(() => H.startBlocker() === null, 3000, 'todos prontos');
    expect(H.startRace()).toBe(true);
    await until(() => hh.race !== null && ha.race !== null && hc.race !== null, 3000, 'largada');
    await race([hh, ha, hc], 300);

    // O anfitrião não chega a mandar o snapshot a A (vai cair logo depois da volta dele). Trocar o
    // envio antes da queda, e não depois: com retryMs de 100 ms, A às vezes voltava e recebia o
    // snapshot ainda durante o laço abaixo, e a espera por "syncing" nunca terminava.
    (H as unknown as { sendSnapshot(to: number): void }).sendSnapshot = () => undefined;
    // A cai. H e C ainda andam até onde a entrada de A deixa, mandando entradas que A não recebe.
    A.debugDropConnection();
    for (let i = 0; i < 30; i++) { hh.pump(8, 1200); hc.pump(8, 1200); await sleep(3); }
    await until(() => A.status().syncing, 3000, 'A de volta, esperando o estado');
    // Agora o anfitrião cai de vez, sem ter mandado nada.
    H.debugVanish();
    hh.race = null;
    // A e C seguem juntos (depois da janela do relay, a IA assume o carro de Hugo).
    await race([ha, hc], 900, 15_000);
    expect(C.isHost).toBe(true);
    expect(ha.race?.starts).toBe(2); // A recomeçou do snapshot de C
    expect(hashRace(ha.race!.state)).toBe(hashRace(hc.race!.state));
    expect([...A.debugInfo().desyncs as unknown[], ...C.debugInfo().desyncs as unknown[]]).toEqual([]);
    expect(hc.race?.state.cars.find((c) => c.seat === 0)?.ai).not.toBeNull();
    A.leave(false); C.leave(false);
  }, 30_000);

  it('fim natural pela rede, quadro a quadro: o mesmo resultado nos dois computadores, e a próxima largada funciona', async () => {
    const ha = new FakeHost('Ana');
    const hb = new FakeHost('Bia');
    const A = new OnlineController(ha, { pingMs: 200 });
    const B = new OnlineController(hb, { pingMs: 200 });
    A.create('kb1');
    await until(() => A.phase === 'lobby' && A.room?.settings !== null && A.room?.settings !== undefined, 3000, 'sala criada');
    B.join(A.code, 'kb1');
    await until(() => B.phase === 'lobby' && (A.room?.clients.length ?? 0) === 2, 3000, 'B na sala');
    A.updateRoomSettings({ trackId: 'copacabana', laps: 1, totalCars: 4 });
    B.toggleReady();
    await until(() => A.startBlocker() === null && B.room?.settings?.laps === 1, 3000, 'B pronto');
    expect(A.startRace()).toBe(true);
    await until(() => ha.race !== null && hb.race !== null, 3000, 'largada');

    // Quadros de 0,25 s (15 ticks + alcance) nos dois, com a rede entregando entre eles.
    const t0 = Date.now();
    while (A.phase !== 'results' || B.phase !== 'results') {
      if (Date.now() - t0 > 60_000) throw new Error(`não terminou: ${ha.race?.state.tick} ${ha.race?.state.phase} ${A.phase} | ${hb.race?.state.tick} ${hb.race?.state.phase} ${B.phase}`);
      ha.frame(0.25);
      hb.frame(0.25);
      await sleep(4);
    }
    const rows = ha.results?.results ?? [];
    const humans = rows.filter((r) => r.seat >= 0);
    expect(humans.map((r) => r.seat).sort()).toEqual([0, 1]);
    expect(humans.every((r) => r.finished)).toBe(true);
    expect(JSON.stringify(hb.results?.results)).toBe(JSON.stringify(rows));
    let compared = 0;
    for (const [t, h] of ha.hashes) if (hb.hashes.has(t)) { expect(hb.hashes.get(t), `tick ${t}`).toBe(h); compared++; }
    expect(compared).toBeGreaterThan(20);
    expect([...A.debugInfo().desyncs as unknown[], ...B.debugInfo().desyncs as unknown[]]).toEqual([]);

    // Convidado volta à sala antes do anfitrião; a nova largada só sai com os dois na sala.
    B.backToRoom();
    B.toggleReady();
    A.backToRoom();
    await until(() => A.startBlocker() === null, 3000, 'segunda largada liberada');
    expect(A.startRace()).toBe(true);
    await until(() => A.phase === 'racing' && B.phase === 'racing' && ha.race !== null && hb.race !== null, 3000, 'segunda largada');
    await race([ha, hb], 300);
    expect(hashRace(hb.race!.state)).toBe(hashRace(ha.race!.state));
    A.leave(false); B.leave(false);
  }, 90_000);

  it('convidado com a janela escondida (sem quadros): o anfitrião não fica em "Aguardando…"; os dois seguem juntos', async () => {
    const ha = new FakeHost('Ana');
    const hb = new FakeHost('Bia');
    const A = new OnlineController(ha, { pingMs: 200 });
    const B = new OnlineController(hb, { pingMs: 200 });
    A.create('kb1');
    await until(() => A.phase === 'lobby' && A.room?.settings !== null && A.room?.settings !== undefined, 3000, 'sala criada');
    B.join(A.code, 'kb1');
    await until(() => B.phase === 'lobby' && (A.room?.clients.length ?? 0) === 2, 3000, 'B na sala');
    B.toggleReady();
    await until(() => A.startBlocker() === null, 3000, 'B pronto');
    expect(A.startRace()).toBe(true);
    await until(() => ha.race !== null && hb.race !== null, 3000, 'largada');
    hb.hiddenNow = true;
    // Só o anfitrião tem quadros, no relógio de verdade, por 2 s.
    const t0 = performance.now();
    let last = t0;
    while (performance.now() - t0 < 2000) {
      const now = performance.now();
      ha.frame((now - last) / 1000);
      last = now;
      await sleep(16);
    }
    const [ta, tb] = [ha.race?.state.tick ?? 0, hb.race?.state.tick ?? 0];
    const info = `${ta} ${JSON.stringify(A.debugInfo().stats)} | ${tb} ${JSON.stringify(B.debugInfo().stats)}`;
    // Sem o bombeamento pela rede o anfitrião fica no tick 0; no relógio de verdade são ~120. A
    // folga (60) é para máquina carregada: uma rodada em ~40 falhou sem mostrar o número.
    expect(ta, info).toBeGreaterThanOrEqual(60);
    expect(Math.abs(ta - tb), info).toBeLessThanOrEqual(12);
    let compared = 0;
    for (const [t, h] of ha.hashes) if (hb.hashes.has(t)) { expect(hb.hashes.get(t), `tick ${t}`).toBe(h); compared++; }
    expect(compared).toBeGreaterThanOrEqual(1);
    expect([...A.debugInfo().desyncs as unknown[], ...B.debugInfo().desyncs as unknown[]]).toEqual([]);
    A.leave(false); B.leave(false);
  }, 10_000);

  it('depois da corrida o anfitrião não larga de novo enquanto um convidado ainda está no resultado', async () => {
    const ha = new FakeHost('Ana');
    const hb = new FakeHost('Bia');
    const A = new OnlineController(ha, { pingMs: 200 });
    const B = new OnlineController(hb, { pingMs: 200 });
    A.create('kb1');
    await until(() => A.phase === 'lobby' && A.room?.settings !== null && A.room?.settings !== undefined, 3000, 'sala criada');
    B.join(A.code, 'kb1');
    await until(() => B.phase === 'lobby' && (A.room?.clients.length ?? 0) === 2, 3000, 'B na sala');
    B.toggleReady();
    await until(() => A.startBlocker() === null, 3000, 'B pronto');
    expect(A.startRace()).toBe(true);
    await until(() => A.phase === 'racing' && B.phase === 'racing', 3000, 'largada');
    // Fim da corrida nos dois; só o anfitrião volta para a sala.
    const data: ResultsScreenData = { mode: 'quick', trackDef: getTrack('copacabana').def, results: [], humans: [], champ: null, newRecords: [] };
    A.finished(data);
    B.finished(data);
    A.backToRoom();
    await sleep(150);
    // B ainda olha o resultado: uma largada agora o deixaria de fora (e o anfitrião esperando para sempre).
    expect(B.phase).toBe('results');
    expect(A.startBlocker()).toBe('online.lobby.waitReady');
    expect(A.startRace()).toBe(false);
    // B volta e fica pronto de novo: aí sim.
    B.backToRoom();
    B.toggleReady();
    await until(() => A.startBlocker() === null, 3000, 'B pronto de novo');
    expect(A.startRace()).toBe(true);
    await until(() => A.phase === 'racing' && B.phase === 'racing', 3000, 'segunda largada');
    A.leave(); B.leave();
  }, 10_000);
});
