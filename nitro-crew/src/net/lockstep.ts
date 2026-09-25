// Lockstep determinístico, puro (sem rede, sem relógio, sem DOM): quem usa entrega o que chegou
// (`receive`), o que o jogador local está apertando (`setLocalInput`) e o instante (`now`), e
// chama `step` para tentar rodar o próximo tick.
//
// Regras:
// - Cada cliente manda a entrada dos seus assentos para o tick T + atraso (padrão 3). O tick T só
//   roda quando há entrada de TODOS os assentos humanos para T; até lá o cliente espera.
// - Todo mundo simula a entrada DECODIFICADA (volante em int8), inclusive quem a gerou.
// - Entradas chegam fora de ordem, repetidas ou adiantadas: o buffer por tick absorve; repetida é
//   ignorada, atrasada (tick já rodado) é descartada, conflitante fica com a primeira.
// - A cada `hashInterval` ticks o hashRace é trocado; diferença → relatório de dessincronia.
// - Pacote perdido (o relay descarta o que passa do limite de taxa) não pode travar a corrida para
//   sempre: quem está parado há `resendMs` reenvia as próprias entradas recentes (duplicata é
//   inofensiva). Numa trava, todos estão parados, então todos reenviam e as lacunas se fecham.
// - Assento de quem não voltou vira IA por um registro TAKEOVER no próprio fluxo de entradas, com o
//   tick em que passa a valer: todas as máquinas aplicam no mesmo tick, antes do stepRace.
import { DIFFICULTY_SKILL } from '../core/sim/ai';
import { hashRace } from '../core/serialize';
import { NEUTRAL_INPUT, type PlayerInput, type RaceState } from '../core/types';
import { decodeInput, DEFAULT_INPUT_DELAY, encodeInput, HASH_INTERVAL, isTakeover, TAKEOVER_BIT, type InputRecord } from './protocol';

export interface LockstepOptions {
  /** Todos os assentos humanos da corrida (globais, 0..3). */
  seats: readonly number[];
  /** Assentos deste computador. */
  localSeats: readonly number[];
  delay?: number;
  hashInterval?: number;
  /** Tick do estado quando o lockstep começa (0 numa largada; o tick do snapshot numa reconexão). */
  startTick?: number;
  /** Tomadas de assento já decididas: pares [assento, tick]. */
  ai?: ReadonlyArray<readonly [number, number]>;
  /** Quantos ticks à frente do atual um registro pode estar (o resto é descartado como inválido). */
  maxAhead?: number;
  /** Parado há mais que isto (na unidade de `now`) → reenvia as entradas locais recentes. */
  resendMs?: number;
}

export interface LockstepTransport {
  sendInputs(records: InputRecord[]): void;
  sendHash(tick: number, hash: number): void;
}

export interface DesyncReport {
  tick: number;
  local: number;
  remote: number;
  /** Quem mandou o hash diferente (id do cliente, como o transporte informou). */
  from: number;
}

export interface LockstepStats {
  sent: number;
  received: number;
  duplicates: number;
  /** Chegaram depois de o tick já ter rodado. */
  stale: number;
  /** Fora das regras (assento alheio, tick absurdo) — descartadas. */
  rejected: number;
  /** Mesmo tick e assento com valores diferentes (fica o primeiro). */
  conflicts: number;
  /** Tentativas de passo que esperaram a rede. */
  stalls: number;
  /** Registros reenviados enquanto parado. */
  resent: number;
}

/** Entrada local ainda não enviada: o volante e os pedais valem o último; bordas acumulam até sair. */
interface PendingInput { steer: number; throttle: boolean; brake: boolean; nitro: boolean; gearUp: boolean; gearDown: boolean }

/**
 * A IA assume o carro do assento (determinístico: sem sorteio, mesmo cérebro em toda máquina).
 * Carro que já terminou já anda sozinho (piloto automático) e fica como está.
 */
export function applyTakeover(state: RaceState, seat: number): void {
  const car = state.cars.find((c) => c.seat === seat);
  if (!car || car.ai) return;
  const [lo, hi] = DIFFICULTY_SKILL[state.config.difficulty];
  car.ai = { skill: (lo + hi) / 2, laneX: 0, laneUntil: state.tick, lookahead: 25, aggression: 0.3 };
}

export class Lockstep {
  readonly seats: readonly number[];
  readonly localSeats: readonly number[];
  readonly delay: number;
  readonly hashInterval: number;
  readonly maxAhead: number;
  /** Próximo tick a rodar (acompanha `state.tick`). */
  tick: number;
  readonly stats: LockstepStats = { sent: 0, received: 0, duplicates: 0, stale: 0, rejected: 0, conflicts: 0, stalls: 0, resent: 0 };
  readonly desyncs: DesyncReport[] = [];
  onDesync: ((report: DesyncReport) => void) | null = null;

  private readonly transport: LockstepTransport;
  /** tick → assento → registro. */
  private readonly buffer = new Map<number, Map<number, InputRecord>>();
  /** Último tick para o qual os assentos locais já mandaram entrada. */
  private lastSent: number;
  private readonly pending = new Map<number, PendingInput>();
  /** assento → tick a partir do qual a IA dirige. */
  private readonly aiFrom = new Map<number, number>();
  /** assento → maior tick com entrada conhecida (para a tomada começar logo depois). */
  private readonly lastKnown = new Map<number, number>();
  private readonly localHashes = new Map<number, number>();
  private readonly remoteHashes = new Map<number, Array<[number, number]>>();
  private stallSince: number | null = null;
  private readonly resendMs: number;
  private lastResend = -Infinity;
  /** O que este cliente mandou nos últimos ticks (entradas locais e tomadas), para reenviar. */
  private sentLog: InputRecord[] = [];

  constructor(opts: LockstepOptions, transport: LockstepTransport) {
    this.seats = [...opts.seats].sort((a, b) => a - b);
    this.localSeats = [...opts.localSeats].sort((a, b) => a - b);
    this.delay = Math.max(1, Math.floor(opts.delay ?? DEFAULT_INPUT_DELAY));
    this.hashInterval = Math.max(1, Math.floor(opts.hashInterval ?? HASH_INTERVAL));
    this.maxAhead = Math.max(this.delay + 1, opts.maxAhead ?? 1200);
    this.resendMs = opts.resendMs ?? 500;
    this.tick = opts.startTick ?? 0;
    this.lastSent = this.tick - 1;
    this.transport = transport;
    for (const s of this.seats) this.lastKnown.set(s, this.tick - 1);
    for (const [seat, from] of opts.ai ?? []) if (this.seats.includes(seat)) this.aiFrom.set(seat, from);
  }

  // ───────────────────────────── Entrada ─────────────────────────────

  /** O que o jogador local está fazendo agora; bordas (nitro, marchas) ficam guardadas até serem enviadas. */
  setLocalInput(seat: number, input: PlayerInput): void {
    if (!this.localSeats.includes(seat)) return;
    const prev = this.pending.get(seat);
    this.pending.set(seat, {
      steer: input.steer, throttle: input.throttle, brake: input.brake,
      nitro: input.nitro || (prev?.nitro ?? false),
      gearUp: input.gearUp || (prev?.gearUp ?? false),
      gearDown: input.gearDown || (prev?.gearDown ?? false),
    });
  }

  /** Registros vindos da rede (forma já validada pelo protocolo). */
  receive(records: readonly InputRecord[]): void {
    this.stats.received += records.length;
    for (const r of records) this.accept(r, false);
  }

  /** Registros de confiança (buffer de um snapshot): podem incluir os assentos locais. */
  ingest(records: readonly InputRecord[]): void {
    for (const r of records) {
      this.accept(r, true);
      if (this.localSeats.includes(r.seat) && !isTakeover(r) && r.tick > this.lastSent) this.lastSent = r.tick;
    }
  }

  private accept(r: InputRecord, trusted: boolean): void {
    if (!this.seats.includes(r.seat)) { this.stats.rejected++; return; }
    const takeover = isTakeover(r);
    // Ninguém de fora manda entrada em nome deste computador (a tomada pela IA pode).
    if (!trusted && !takeover && this.localSeats.includes(r.seat)) { this.stats.rejected++; return; }
    if (r.tick > this.tick + this.maxAhead) { this.stats.rejected++; return; }
    if (takeover) {
      const known = this.aiFrom.get(r.seat);
      if (known === undefined) {
        if (r.tick < this.tick) { this.stats.stale++; return; }
        this.aiFrom.set(r.seat, r.tick);
      } else {
        this.stats.duplicates++;
        return;
      }
    }
    if (r.tick < this.tick) { this.stats.stale++; return; }
    let row = this.buffer.get(r.tick);
    if (!row) { row = new Map(); this.buffer.set(r.tick, row); }
    const existing = row.get(r.seat);
    if (existing) {
      if (existing.bits === r.bits && existing.steer === r.steer) this.stats.duplicates++;
      else this.stats.conflicts++;
      return;
    }
    row.set(r.seat, { tick: r.tick, seat: r.seat, bits: r.bits, steer: r.steer });
    if (r.tick > (this.lastKnown.get(r.seat) ?? -1)) this.lastKnown.set(r.seat, r.tick);
  }

  /** Manda as entradas locais que faltam até `tick + delay` (as intermediárias neutras; a do alvo, a atual). */
  private flush(): void {
    const target = this.tick + this.delay;
    if (this.lastSent >= target) return;
    const out: InputRecord[] = [];
    for (let t = Math.max(this.lastSent + 1, this.tick); t <= target; t++) {
      for (const seat of this.localSeats) {
        if (this.isAi(seat, t)) continue;
        const p = t === target ? this.pending.get(seat) : undefined;
        const { bits, steer } = encodeInput(p ?? NEUTRAL_INPUT);
        const rec: InputRecord = { tick: t, seat, bits, steer };
        this.accept(rec, true);
        out.push(rec);
      }
    }
    // As bordas já saíram; o volante e os pedais continuam valendo até a próxima leitura.
    for (const [seat, p] of this.pending) this.pending.set(seat, { ...p, nitro: false, gearUp: false, gearDown: false });
    this.lastSent = target;
    if (out.length) { this.stats.sent += out.length; this.log(out); this.transport.sendInputs(out); }
  }

  /**
   * Guarda o enviado dos últimos ticks. A janela cobre o pior caso de uma trava: o outro pode estar
   * esperando uma entrada de até 2×atraso+1 ticks antes da última que mandei.
   */
  private log(records: InputRecord[]): void {
    const keepFrom = this.lastSent - (2 * this.delay + 4);
    this.sentLog = this.sentLog.filter((r) => r.tick >= keepFrom || (isTakeover(r) && r.tick >= this.tick)).concat(records);
  }

  // ───────────────────────────── Passo ─────────────────────────────

  isAi(seat: number, tick = this.tick): boolean {
    const from = this.aiFrom.get(seat);
    return from !== undefined && tick >= from;
  }

  /** Assentos cuja entrada para o tick atual ainda não chegou. */
  missingSeats(): number[] {
    const row = this.buffer.get(this.tick);
    return this.seats.filter((s) => !this.isAi(s) && !row?.has(s));
  }

  /**
   * Tenta rodar o tick atual. `run` recebe as entradas decodificadas por assento e deve chamar o
   * stepRace (a sessão também trata os eventos ali). Devolve falso se está esperando a rede.
   */
  step(state: RaceState, now: number, run: (inputs: PlayerInput[]) => void): boolean {
    if (state.tick !== this.tick) throw new Error(`lockstep no tick ${this.tick}, estado no ${state.tick}`);
    this.flush();
    if (this.checkStall(now)) {
      this.stats.stalls++;
      return false;
    }
    this.stallSince = null;
    const T = this.tick;
    const row = this.buffer.get(T);
    const inputs: PlayerInput[] = [];
    for (const seat of this.seats) {
      if (this.aiFrom.get(seat) === T) applyTakeover(state, seat);
      if (this.isAi(seat, T)) continue;
      const r = row?.get(seat);
      inputs[seat] = r ? decodeInput(r.bits, r.steer) : { ...NEUTRAL_INPUT };
    }
    run(inputs);
    this.buffer.delete(T);
    this.tick = state.tick;
    if (this.tick % this.hashInterval === 0) this.recordHash(this.tick, hashRace(state));
    return true;
  }

  /**
   * Confere se o tick atual espera a rede (marca desde quando) e, parado há `resendMs`, reenvia as
   * entradas recentes. Devolve verdadeiro se está parado.
   */
  checkStall(now: number): boolean {
    if (this.missingSeats().length === 0) return false;
    if (this.stallSince === null) this.stallSince = now;
    if (now - this.stallSince >= this.resendMs && now - this.lastResend >= this.resendMs && this.sentLog.length > 0) {
      this.lastResend = now;
      this.stats.resent += this.sentLog.length;
      this.transport.sendInputs(this.sentLog.slice());
    }
    return true;
  }

  /** Há quanto tempo o tick atual espera a rede (0 se não está esperando). */
  stalledMs(now: number): number {
    return this.stallSince === null ? 0 : Math.max(0, now - this.stallSince);
  }

  /**
   * Quantos ticks este cliente está atrás do mais lento dos outros (pela entrada que eles já
   * mandaram): quem está atrás pode rodar mais de um tick por quadro para alcançar.
   */
  behindBy(): number {
    let min = Infinity;
    for (const seat of this.seats) {
      if (this.localSeats.includes(seat) || this.isAi(seat)) continue;
      min = Math.min(min, (this.lastKnown.get(seat) ?? this.tick - 1) - this.delay);
    }
    return min === Infinity ? 0 : Math.max(0, min - this.tick);
  }

  // ───────────────────────────── Tomada pela IA ─────────────────────────────

  /** Maior tick com entrada conhecida do assento. */
  lastTickFrom(seat: number): number {
    return this.lastKnown.get(seat) ?? this.tick - 1;
  }

  /**
   * Decide que a IA assume `seat` a partir do primeiro tick sem entrada dele (só o anfitrião chama).
   * Devolve o registro já aceito aqui e enviado aos outros, ou null se o assento já é da IA.
   */
  takeover(seat: number): InputRecord | null {
    if (!this.seats.includes(seat) || this.aiFrom.has(seat)) return null;
    const from = Math.max(this.tick, this.lastTickFrom(seat) + 1);
    const rec: InputRecord = { tick: from, seat, bits: TAKEOVER_BIT, steer: 0 };
    this.accept(rec, true);
    this.log([rec]);
    this.transport.sendInputs([rec]);
    return rec;
  }

  /** Já há tomada decidida para o assento (valendo agora ou num tick adiante). */
  hasTakeover(seat: number): boolean {
    return this.aiFrom.has(seat);
  }

  /** Pares [assento, tick] das tomadas decididas (para o snapshot). */
  aiList(): number[] {
    const out: number[] = [];
    for (const [seat, from] of [...this.aiFrom].sort((a, b) => a[0] - b[0])) out.push(seat, from);
    return out;
  }

  /** Tudo o que está no buffer para ticks ≥ atual, em ordem (para o snapshot de quem reconecta). */
  bufferedRecords(): InputRecord[] {
    const out: InputRecord[] = [];
    for (const t of [...this.buffer.keys()].sort((a, b) => a - b)) {
      if (t < this.tick) continue;
      const row = this.buffer.get(t);
      if (!row) continue;
      for (const s of [...row.keys()].sort((a, b) => a - b)) { const r = row.get(s); if (r) out.push(r); }
    }
    return out;
  }

  // ───────────────────────────── Hash ─────────────────────────────

  private recordHash(tick: number, hash: number): void {
    this.localHashes.set(tick, hash);
    this.transport.sendHash(tick, hash);
    const remote = this.remoteHashes.get(tick);
    if (remote) { for (const [from, h] of remote) this.compare(tick, hash, h, from); this.remoteHashes.delete(tick); }
    // Guarda só os últimos (hash de quem está muito atrás chega e é comparado com estes).
    const keep = this.hashInterval * 30;
    for (const t of this.localHashes.keys()) if (t < tick - keep) this.localHashes.delete(t);
    for (const t of this.remoteHashes.keys()) if (t < tick - keep) this.remoteHashes.delete(t);
  }

  receiveHash(from: number, tick: number, hash: number): void {
    if (tick % this.hashInterval !== 0 || tick > this.tick + this.maxAhead) { this.stats.rejected++; return; }
    const mine = this.localHashes.get(tick);
    if (mine !== undefined) { this.compare(tick, mine, hash, from); return; }
    if (tick <= this.tick) return; // antigo demais para comparar
    let list = this.remoteHashes.get(tick);
    if (!list) { list = []; this.remoteHashes.set(tick, list); }
    if (list.length < 8) list.push([from, hash]);
  }

  private compare(tick: number, local: number, remote: number, from: number): void {
    if (local === remote) return;
    const report: DesyncReport = { tick, local, remote, from };
    this.desyncs.push(report);
    this.onDesync?.(report);
  }

  /** Hash local registrado no tick (para depuração e para o playtest comparar máquinas). */
  hashAt(tick: number): number | undefined {
    return this.localHashes.get(tick);
  }
}
