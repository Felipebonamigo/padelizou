// Servidor de retransmissão (relay) do Nitro Crew para o online em lockstep.
// Não simula nada e não entende o jogo: guarda salas por código, sabe quem é o anfitrião, repassa
// as mensagens dos clientes e segura o lugar de quem caiu por um tempo (reconexão). Tudo o que
// chega é tratado como hostil: tamanho máximo por mensagem, limite de taxa por conexão, forma
// conferida antes de repassar. O conteúdo (carros, pistas, entradas) é validado nos clientes.
//
// Uso: node server/relay.mjs            (porta pelo env PORT, padrão 8787)
//      PORT=0 node server/relay.mjs     (porta livre; a linha "ouvindo em" diz qual)
// Variáveis: PORT, HOST, RELAY_RECONNECT_MS, RELAY_HEARTBEAT_MS, RELAY_MAX_ROOMS, RELAY_RATE, RELAY_BURST,
// RELAY_QUIET=1 (sem log por sala).
import { randomBytes, randomInt } from 'node:crypto';
import { pathToFileURL } from 'node:url';
import { WebSocketServer } from 'ws';

export const PROTOCOL_VERSION = 1;
/** Impressão do conteúdo do jogo (`b` no create/join): texto curto e opaco para o relay. */
const BUILD_MAX = 64;
const CODE_ALPHABET = 'ABCDEFGHJKLMNPQRSTUVWXYZ';
const CODE_LENGTH = 5;

export const DEFAULTS = Object.freeze({
  port: 8787,
  host: '0.0.0.0',
  /** Clientes (computadores) por sala. */
  maxClients: 4,
  /** Jogadores humanos por sala, somando os locais de cada cliente. */
  maxSeats: 4,
  maxLocalSeats: 2,
  /** Tamanho máximo de uma mensagem (o snapshot de reconexão é a maior, ~15 KB com 20 carros). */
  maxPayload: 64 * 1024,
  /** Tamanho máximo dos blocos opacos (info do cliente, opções da sala, configuração da largada). */
  maxBlob: 4 * 1024,
  /** Registros de entrada por mensagem (4 números cada). */
  maxRecords: 64,
  /** Quanto tempo o lugar de quem caiu no meio da corrida fica guardado. */
  reconnectMs: 60_000,
  /** Intervalo do ping de protocolo; quem não responde a um é derrubado no seguinte. */
  heartbeatMs: 10_000,
  /** Mensagens por segundo por conexão (sustentado) e rajada. A corrida manda ~62/s. */
  rateLimit: 150,
  rateBurst: 300,
  /** Descartes por taxa antes de derrubar a conexão. */
  maxRateDrops: 600,
  maxRooms: 1000,
  log: true,
});

const isInt = (v, min, max) => typeof v === 'number' && Number.isInteger(v) && v >= min && v <= max;
const validBuild = (v) => v === undefined || (typeof v === 'string' && v.length <= BUILD_MAX);
const isObj = (v) => typeof v === 'object' && v !== null && !Array.isArray(v);

/** Sobe o relay. Devolve o servidor, as salas (para testes) e `close()`. */
export function startRelay(options = {}) {
  const o = { ...DEFAULTS, ...options };
  const wss = new WebSocketServer({ port: o.port, host: o.host, maxPayload: o.maxPayload });
  /** code → { code, clients: Map<id, Client>, host, started, settings, nextId } */
  const rooms = new Map();
  const conns = new Set();
  /** Ordem de chegada das conexões (quem está conectado há mais tempo tem o número menor). */
  let connSeq = 0;
  const stats = { connections: 0, invalid: 0, rateDropped: 0, forwarded: 0 };
  const log = (...a) => { if (o.log) console.log(new Date().toISOString(), ...a); };

  const send = (ws, msg) => { if (ws && ws.readyState === 1) ws.send(JSON.stringify(msg)); };
  const sendTo = (client, msg) => { if (client.conn) send(client.conn.ws, msg); };
  const broadcast = (room, msg, exceptId = -1) => { for (const c of room.clients.values()) if (c.id !== exceptId) sendTo(c, msg); };

  const roomView = (room) => ({
    code: room.code, host: room.host, started: room.started, settings: room.settings,
    clients: [...room.clients.values()].map((c) => ({ id: c.id, seats: c.seats, info: c.info, connected: c.conn !== null })),
  });
  const pushRoom = (room) => broadcast(room, { t: 'room', room: roomView(room) });
  const usedSeats = (room, exceptId = -1) => { let n = 0; for (const c of room.clients.values()) if (c.id !== exceptId) n += c.seats; return n; };

  function newCode() {
    for (let tries = 0; tries < 50; tries++) {
      let code = '';
      for (let i = 0; i < CODE_LENGTH; i++) code += CODE_ALPHABET[randomInt(CODE_ALPHABET.length)];
      if (!rooms.has(code)) return code;
    }
    return null;
  }

  /**
   * O anfitrião é sempre um cliente conectado; se o atual caiu, passa a quem está conectado há mais
   * tempo. No meio da corrida é quem não caiu: o estado dele é o mais completo, e quem acabou de
   * voltar espera o snapshot dele. (Pelo menor id, um recém-voltado podia herdar a sala e seguir do
   * próprio estado atrasado, travando quem ficou.)
   */
  function electHost(room) {
    const current = room.clients.get(room.host);
    if (current && current.conn) return;
    const next = [...room.clients.values()].filter((c) => c.conn).sort((a, b) => a.conn.order - b.conn.order)[0];
    if (next) room.host = next.id;
  }

  function deleteIfEmpty(room) {
    if (room.clients.size === 0) { rooms.delete(room.code); log(`sala ${room.code} encerrada`); return true; }
    // Ninguém conectado e fora de corrida: ninguém vai voltar para o lobby.
    if (!room.started && ![...room.clients.values()].some((c) => c.conn)) {
      for (const c of room.clients.values()) clearTimeout(c.dropTimer);
      rooms.delete(room.code);
      log(`sala ${room.code} encerrada (vazia)`);
      return true;
    }
    return false;
  }

  function removeClient(room, client, event) {
    clearTimeout(client.dropTimer);
    room.clients.delete(client.id);
    if (client.conn) { client.conn.client = null; client.conn.room = null; }
    if (deleteIfEmpty(room)) return;
    electHost(room);
    broadcast(room, { t: 'peer', id: client.id, e: event });
    pushRoom(room);
  }

  function attach(conn, room, client) {
    client.conn = conn;
    conn.room = room;
    conn.client = client;
  }

  function validInfo(v) { return isObj(v) && JSON.stringify(v).length <= o.maxBlob; }

  // ───────────────────────────── Mensagens ─────────────────────────────

  function handle(conn, msg) {
    const { ws } = conn;
    const room = conn.room;
    const me = conn.client;
    switch (msg.t) {
      case 'ping':
        if (isInt(msg.n, 0, 0x7fffffff)) send(ws, { t: 'pong', n: msg.n });
        return true;
      case 'create': {
        if (room) return false;
        if (msg.v !== PROTOCOL_VERSION) { send(ws, { t: 'error', code: 'version' }); return true; }
        if (!isInt(msg.seats, 1, o.maxLocalSeats) || !validInfo(msg.info) || !validBuild(msg.b)) return false;
        if (rooms.size >= o.maxRooms) { send(ws, { t: 'error', code: 'rooms' }); return true; }
        const code = newCode();
        if (!code) { send(ws, { t: 'error', code: 'rooms' }); return true; }
        // A sala guarda a impressão do conteúdo de quem a criou: só entra quem tem a mesma.
        const r = { code, clients: new Map(), host: 0, started: false, settings: null, nextId: 1, build: msg.b ?? null };
        const c = { id: 0, token: randomBytes(12).toString('hex'), seats: msg.seats, info: msg.info, conn: null, dropTimer: null };
        r.clients.set(0, c);
        rooms.set(code, r);
        attach(conn, r, c);
        log(`sala ${code} criada`);
        send(ws, { t: 'welcome', room: code, id: 0, token: c.token, rejoined: false });
        pushRoom(r);
        return true;
      }
      case 'join': {
        if (room) return false;
        if (msg.v !== PROTOCOL_VERSION) { send(ws, { t: 'error', code: 'version' }); return true; }
        if (typeof msg.room !== 'string' || !isInt(msg.seats, 1, o.maxLocalSeats) || !validInfo(msg.info) || !validBuild(msg.b)) return false;
        const r = rooms.get(msg.room.toUpperCase());
        if (!r) { send(ws, { t: 'error', code: 'no_room' }); return true; }
        // Outro conteúdo (pista ou carro que um dos lados não conhece, física diferente): nem entra.
        if ((msg.b ?? null) !== r.build) { send(ws, { t: 'error', code: 'build' }); return true; }
        if (r.started) { send(ws, { t: 'error', code: 'started' }); return true; }
        if (r.clients.size >= o.maxClients || usedSeats(r) + msg.seats > o.maxSeats) { send(ws, { t: 'error', code: 'full' }); return true; }
        const c = { id: r.nextId++, token: randomBytes(12).toString('hex'), seats: msg.seats, info: msg.info, conn: null, dropTimer: null };
        r.clients.set(c.id, c);
        attach(conn, r, c);
        send(ws, { t: 'welcome', room: r.code, id: c.id, token: c.token, rejoined: false });
        broadcast(r, { t: 'peer', id: c.id, e: 'join' }, c.id);
        pushRoom(r);
        return true;
      }
      case 'rejoin': {
        if (room) return false;
        if (msg.v !== PROTOCOL_VERSION) { send(ws, { t: 'error', code: 'version' }); return true; }
        if (typeof msg.room !== 'string' || typeof msg.token !== 'string' || msg.token.length > 64) return false;
        const r = rooms.get(msg.room.toUpperCase());
        const c = r ? [...r.clients.values()].find((x) => x.token === msg.token) : undefined;
        if (!r) { send(ws, { t: 'error', code: 'no_room' }); return true; }
        if (!c) { send(ws, { t: 'error', code: 'expired' }); return true; }
        // Conexão antiga meio-aberta (o cliente percebeu a queda antes do servidor): a nova vale.
        if (c.conn) { const old = c.conn; old.client = null; old.room = null; old.ws.terminate(); }
        clearTimeout(c.dropTimer);
        c.dropTimer = null;
        attach(conn, r, c);
        electHost(r);
        send(ws, { t: 'welcome', room: r.code, id: c.id, token: c.token, rejoined: true });
        broadcast(r, { t: 'peer', id: c.id, e: 'rejoin' }, c.id);
        pushRoom(r);
        log(`sala ${r.code}: cliente ${c.id} reconectou`);
        return true;
      }
    }
    if (!room || !me) return false;
    const isHost = room.host === me.id;
    switch (msg.t) {
      case 'info': {
        if (!isInt(msg.seats, 1, o.maxLocalSeats) || !validInfo(msg.info)) return false;
        if (!room.started && usedSeats(room, me.id) + msg.seats > o.maxSeats) { send(ws, { t: 'error', code: 'full' }); return true; }
        if (!room.started) me.seats = msg.seats;
        me.info = msg.info;
        pushRoom(room);
        return true;
      }
      case 'settings':
        if (!isHost) { send(ws, { t: 'error', code: 'not_host' }); return true; }
        if (!validInfo(msg.settings)) return false;
        room.settings = msg.settings;
        pushRoom(room);
        return true;
      case 'start':
        if (!isHost) { send(ws, { t: 'error', code: 'not_host' }); return true; }
        if (!validInfo(msg.cfg) || room.started) return false;
        room.started = true;
        // Para todos, inclusive o anfitrião: todo mundo começa pelo mesmo caminho.
        broadcast(room, { t: 'start', from: me.id, cfg: msg.cfg });
        pushRoom(room);
        log(`sala ${room.code}: largada com ${room.clients.size} clientes`);
        return true;
      case 'lobby':
        if (!isHost) { send(ws, { t: 'error', code: 'not_host' }); return true; }
        if (!room.started) return true;
        room.started = false;
        // Quem ainda estava desconectado não volta mais para uma sala fora de corrida.
        for (const c of [...room.clients.values()]) if (!c.conn) removeClient(room, c, 'drop');
        if (rooms.has(room.code)) pushRoom(room);
        return true;
      case 'i': {
        const d = msg.d;
        if (!room.started || !Array.isArray(d) || d.length === 0 || d.length % 4 !== 0 || d.length > o.maxRecords * 4) return false;
        for (const x of d) if (!isInt(x, -127, 0x7fffffff)) return false;
        broadcast(room, { t: 'i', from: me.id, d }, me.id);
        stats.forwarded++;
        return true;
      }
      case 'h':
        if (!room.started || !isInt(msg.k, 0, 0x7fffffff) || !isInt(msg.h, 0, 0xffffffff)) return false;
        broadcast(room, { t: 'h', from: me.id, k: msg.k, h: msg.h }, me.id);
        return true;
      case 'snap': {
        if (!isHost) { send(ws, { t: 'error', code: 'not_host' }); return true; }
        const target = room.clients.get(msg.to);
        if (!room.started || !target || target.id === me.id || !isObj(msg.snap)) return false;
        sendTo(target, { t: 'snap', from: me.id, snap: msg.snap });
        return true;
      }
      case 'leave':
        removeClient(room, me, 'leave');
        return true;
      default:
        return false;
    }
  }

  // ───────────────────────────── Conexões ─────────────────────────────

  wss.on('connection', (ws) => {
    stats.connections++;
    const conn = { ws, order: ++connSeq, room: null, client: null, alive: true, tokens: o.rateBurst, last: Date.now(), drops: 0, lastRateError: 0 };
    conns.add(conn);
    ws.on('pong', () => { conn.alive = true; });
    ws.on('message', (data, isBinary) => {
      // Limite de taxa (balde de fichas): mensagem acima da taxa é descartada; abuso derruba.
      const now = Date.now();
      conn.tokens = Math.min(o.rateBurst, conn.tokens + ((now - conn.last) / 1000) * o.rateLimit);
      conn.last = now;
      if (conn.tokens < 1) {
        stats.rateDropped++;
        conn.drops++;
        if (now - conn.lastRateError > 1000) { conn.lastRateError = now; send(ws, { t: 'error', code: 'rate' }); }
        if (conn.drops > o.maxRateDrops) ws.close(1008, 'rate');
        return;
      }
      conn.tokens -= 1;
      if (isBinary) { stats.invalid++; return; }
      let msg;
      try { msg = JSON.parse(String(data)); } catch { stats.invalid++; return; }
      if (!isObj(msg) || typeof msg.t !== 'string') { stats.invalid++; return; }
      if (!handle(conn, msg)) { stats.invalid++; send(ws, { t: 'error', code: 'bad' }); }
    });
    ws.on('close', () => {
      conns.delete(conn);
      const { room, client } = conn;
      conn.room = null;
      conn.client = null;
      if (!room || !client || client.conn !== conn) return;
      client.conn = null;
      if (!room.started) { removeClient(room, client, 'leave'); return; }
      // No meio da corrida o lugar fica guardado; o anfitrião decide a IA se ninguém voltar.
      electHost(room);
      client.dropTimer = setTimeout(() => {
        if (room.clients.get(client.id) === client && !client.conn) {
          log(`sala ${room.code}: cliente ${client.id} não voltou em ${o.reconnectMs} ms`);
          removeClient(room, client, 'drop');
        }
      }, o.reconnectMs);
      broadcast(room, { t: 'peer', id: client.id, e: 'lost' });
      pushRoom(room);
    });
    ws.on('error', () => { /* o 'close' vem em seguida */ });
  });

  const heartbeat = setInterval(() => {
    for (const conn of conns) {
      if (!conn.alive) { conn.ws.terminate(); continue; }
      conn.alive = false;
      try { conn.ws.ping(); } catch { /* fechando */ }
    }
  }, o.heartbeatMs);

  return {
    wss, rooms, stats,
    port: () => wss.address().port,
    close: () => new Promise((resolve) => {
      clearInterval(heartbeat);
      for (const r of rooms.values()) for (const c of r.clients.values()) clearTimeout(c.dropTimer);
      for (const ws of wss.clients) ws.terminate();
      wss.close(() => resolve());
    }),
  };
}

// ───────────────────────────── Linha de comando ─────────────────────────────

const invokedDirectly = process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href;
if (invokedDirectly) {
  const env = process.env;
  const num = (v, fallback) => (v !== undefined && v !== '' && Number.isFinite(Number(v)) ? Number(v) : fallback);
  const relay = startRelay({
    port: num(env.PORT, DEFAULTS.port),
    host: env.HOST || DEFAULTS.host,
    reconnectMs: num(env.RELAY_RECONNECT_MS, DEFAULTS.reconnectMs),
    heartbeatMs: num(env.RELAY_HEARTBEAT_MS, DEFAULTS.heartbeatMs),
    maxRooms: num(env.RELAY_MAX_ROOMS, DEFAULTS.maxRooms),
    rateLimit: num(env.RELAY_RATE, DEFAULTS.rateLimit),
    rateBurst: num(env.RELAY_BURST, DEFAULTS.rateBurst),
    log: env.RELAY_QUIET !== '1',
  });
  relay.wss.on('listening', () => {
    // O teste de integração lê a porta desta linha (com PORT=0 ela é sorteada pelo sistema).
    console.log(`Nitro Crew relay ouvindo em ws://${env.HOST || DEFAULTS.host}:${relay.port()} (protocolo v${PROTOCOL_VERSION})`);
  });
  const stop = () => { relay.close().then(() => process.exit(0)); };
  process.on('SIGINT', stop);
  process.on('SIGTERM', stop);
}
