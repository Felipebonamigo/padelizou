// Lockstep puro com 2–4 clientes simulados em memória: rede com atraso, reordenação e
// duplicação sorteados por semente fixa. Prova que todos chegam ao mesmo hashRace.
import { describe, expect, it } from 'vitest';
import { createRng, nextFloat, nextInt, type RngState } from '../src/core/rng';
import { deserializeRace, hashRace, serializeRace } from '../src/core/serialize';
import { createRace, stepRace } from '../src/core/sim/race';
import { getTrack } from '../src/core/track';
import type { PlayerInput, RaceConfig, RaceState, Track } from '../src/core/types';
import { Lockstep, type DesyncReport } from '../src/net/lockstep';
import { decodeInput, encodeInput, type InputRecord } from '../src/net/protocol';
import { human, NO_ASSISTS } from './helpers';

const TRACK: Track = getTrack('copacabana');

function config(humans: number, seed = 99): RaceConfig {
  return {
    trackId: TRACK.def.id, laps: 3, humans: Array.from({ length: humans }, (_, i) => human(i, 0, ['falcao', 'trovao', 'tornado', 'camelo'][i])),
    totalCars: 8, difficulty: 'profissional', manualGear: false, assists: { ...NO_ASSISTS, sharedNitro: true, tow: true }, seed,
  };
}

/** Piloto de teste: volante com fração (testa a quantização), freio de vez em quando, nitro em borda. */
function pilot(seat: number, frame: number): PlayerInput {
  const phase = (frame + seat * 53) % 240;
  return {
    steer: phase < 80 ? 0.37 : phase < 160 ? -0.61 : 0.05 * (seat + 1),
    throttle: phase % 97 !== 0,
    brake: phase > 225,
    nitro: frame % 700 === 100 + seat * 20,
    gearUp: false, gearDown: false,
  };
}

type Payload = { from: number; kind: 'i'; records: InputRecord[] } | { from: number; kind: 'h'; tick: number; hash: number };
type Packet = Payload & { at: number; to: number };

interface SimClient {
  id: number;
  seats: number[];
  state: RaceState;
  ls: Lockstep;
  frames: number;
  online: boolean;
  desyncs: DesyncReport[];
}

/** Rede em memória: cada pacote chega depois de um atraso sorteado; às vezes duas vezes. */
class Net {
  now = 0;
  packets: Packet[] = [];
  clients: SimClient[] = [];
  constructor(readonly rng: RngState, readonly maxDelay: number, readonly dupRate = 0.05, readonly lossRate = 0) {}
  send(p: Payload): void {
    for (const c of this.clients) {
      if (c.id === p.from) continue;
      if (nextFloat(this.rng) < this.lossRate) continue; // pacote perdido (ex.: descartado pelo limite de taxa do relay)
      const at = this.now + nextInt(this.rng, 0, this.maxDelay);
      this.packets.push({ ...p, at, to: c.id });
      if (nextFloat(this.rng) < this.dupRate) this.packets.push({ ...p, at: at + nextInt(this.rng, 0, this.maxDelay), to: c.id });
    }
  }
  /** Entrega o que venceu, em ordem embaralhada (fora de ordem de propósito). */
  deliver(all = false): void {
    const due = this.packets.filter((p) => all || p.at <= this.now);
    this.packets = this.packets.filter((p) => !(all || p.at <= this.now));
    for (let i = due.length - 1; i > 0; i--) { const j = nextInt(this.rng, 0, i); [due[i], due[j]] = [due[j], due[i]]; }
    for (const p of due) {
      const c = this.clients.find((x) => x.id === p.to);
      if (!c || !c.online) continue;
      if (p.kind === 'i') c.ls.receive(p.records);
      else c.ls.receiveHash(p.from, p.tick, p.hash);
    }
  }
}

function makeClient(net: Net, id: number, seats: number[], allSeats: number[], cfg: RaceConfig, delay = 3, resendMs = 20): SimClient {
  const c: SimClient = { id, seats, state: createRace(cfg, TRACK), ls: null as unknown as Lockstep, frames: 0, online: true, desyncs: [] };
  c.ls = new Lockstep({ seats: allSeats, localSeats: seats, delay, resendMs }, {
    sendInputs: (records) => { if (c.online) net.send({ kind: 'i', from: id, records: records.map((r) => ({ ...r })) }); },
    sendHash: (tick, hash) => { if (c.online) net.send({ kind: 'h', from: id, tick, hash }); },
  });
  c.ls.onDesync = (r) => c.desyncs.push(r);
  net.clients.push(c);
  return c;
}

/** Um "quadro" do cliente: lê o piloto e tenta rodar até `budget` ticks (sem passar de `limit`). */
function frame(c: SimClient, budget: number, limit: number, now: number): number {
  c.frames++;
  for (const s of c.seats) c.ls.setLocalInput(s, pilot(s, c.frames));
  let n = 0;
  while (n < budget && c.state.tick < limit && c.ls.step(c.state, now, (inputs) => stepRace(c.state, TRACK, inputs))) n++;
  return n;
}

function runAll(net: Net, clients: SimClient[], limit: number, maxIterations = 200_000): void {
  for (let it = 0; it < maxIterations && clients.some((c) => c.online && c.state.tick < limit); it++) {
    net.now++;
    for (const c of clients) if (c.online) frame(c, nextInt(net.rng, 0, 3), limit, net.now);
    net.deliver();
  }
}

describe('lockstep em rede simulada', () => {
  for (const n of [2, 3, 4]) {
    it(`${n} clientes com atraso e reordenação sorteados chegam ao mesmo hash após 3000 ticks`, () => {
      const net = new Net(createRng(1000 + n), 6);
      const seats = Array.from({ length: n }, (_, i) => i);
      const cfg = config(n);
      const clients = seats.map((s) => makeClient(net, s, [s], seats, cfg));
      runAll(net, clients, 3000);
      for (const c of clients) expect(c.state.tick).toBe(3000);
      const h = hashRace(clients[0].state);
      for (const c of clients) {
        expect(hashRace(c.state)).toBe(h);
        expect(serializeRace(c.state)).toBe(serializeRace(clients[0].state));
        expect(c.desyncs).toEqual([]);
      }
      // A rede de fato bagunçou: houve duplicadas e esperas.
      expect(clients.reduce((a, c) => a + c.ls.stats.duplicates, 0)).toBeGreaterThan(0);
      expect(clients.reduce((a, c) => a + c.ls.stats.stalls, 0)).toBeGreaterThan(0);
    }, 60_000);
  }

  it('pacote de entrada perdido não trava a corrida para sempre: quem está parado reenvia o recente', () => {
    const net = new Net(createRng(77), 3, 0, 0.08);
    const cfg = config(3);
    const clients = [0, 1, 2].map((s) => makeClient(net, s, [s], [0, 1, 2], cfg));
    runAll(net, clients, 1500, 60_000);
    for (const c of clients) expect(c.state.tick).toBe(1500);
    expect(hashRace(clients[1].state)).toBe(hashRace(clients[0].state));
    expect(hashRace(clients[2].state)).toBe(hashRace(clients[0].state));
    expect(clients.reduce((a, c) => a + c.ls.stats.resent, 0)).toBeGreaterThan(0);
  }, 60_000);

  it('um cliente com dois jogadores locais e outro com um (3 humanos) ficam iguais', () => {
    const net = new Net(createRng(7), 4);
    const cfg = config(3);
    const a = makeClient(net, 0, [0, 1], [0, 1, 2], cfg, 4);
    const b = makeClient(net, 1, [2], [0, 1, 2], cfg, 4);
    runAll(net, [a, b], 1500);
    expect(hashRace(a.state)).toBe(hashRace(b.state));
    // As entradas locais valem nos dois lados: o carro do assento 1 andou.
    expect(b.state.cars.find((c) => c.seat === 1)?.z).toBe(a.state.cars.find((c) => c.seat === 1)?.z);
  }, 30_000);

  it('a mesma corrida rodada sem rede, com as entradas decodificadas, dá o mesmo hash (o lockstep não inventa nada)', () => {
    const net = new Net(createRng(3), 3);
    const cfg = config(2);
    const a = makeClient(net, 0, [0], [0, 1], cfg);
    const b = makeClient(net, 1, [1], [0, 1], cfg);
    // Grava o que cada tick recebeu no cliente A.
    const log: PlayerInput[][] = [];
    const origStep = a.ls.step.bind(a.ls);
    a.ls.step = (state, now, run) => origStep(state, now, (inputs) => { log.push(inputs.map((x) => ({ ...x }))); run(inputs); });
    runAll(net, [a, b], 900);
    const solo = createRace(cfg, TRACK);
    for (const inputs of log) stepRace(solo, TRACK, inputs);
    expect(hashRace(solo)).toBe(hashRace(a.state));
  }, 30_000);

  it('a tomada pela IA chega ao stepRace como entrada: repetir só as entradas de cada tick reproduz a corrida (replay, fantasma)', () => {
    const net = new Net(createRng(41), 3);
    const cfg = config(2);
    const a = makeClient(net, 0, [0], [0, 1], cfg);
    const b = makeClient(net, 1, [1], [0, 1], cfg);
    const log: PlayerInput[][] = [];
    const origStep = a.ls.step.bind(a.ls);
    a.ls.step = (state, now, run) => origStep(state, now, (inputs) => { log.push(inputs.map((x) => ({ ...x }))); run(inputs); });
    runAll(net, [a, b], 600);
    b.online = false;
    net.deliver(true);
    runAll(net, [a], 700, 300);
    expect(a.ls.takeover(1)).not.toBeNull();
    runAll(net, [a], 1500);
    expect(a.state.tick).toBe(1500);
    expect(a.state.cars.find((c) => c.seat === 1)?.ai).not.toBeNull();
    // Nada muda o estado por fora: a corrida inteira é stepRace com o que o lockstep entregou.
    const solo = createRace(cfg, TRACK);
    for (const inputs of log) stepRace(solo, TRACK, inputs);
    expect(solo.cars.find((c) => c.seat === 1)?.ai).not.toBeNull();
    expect(hashRace(solo)).toBe(hashRace(a.state));
  }, 30_000);

  it('sem a entrada do outro assento o tick não roda (nem o primeiro)', () => {
    const state = createRace(config(2), TRACK);
    const sent: InputRecord[][] = [];
    const ls = new Lockstep({ seats: [0, 1], localSeats: [0], delay: 3 }, { sendInputs: (r) => sent.push(r), sendHash: () => undefined });
    let advanced = 0;
    for (let i = 0; i < 20; i++) if (ls.step(state, i * 16, (inputs) => stepRace(state, TRACK, inputs))) advanced++;
    expect(advanced).toBe(0);
    expect(state.tick).toBe(0);
    expect(ls.missingSeats()).toEqual([1]);
    expect(ls.stalledMs(19 * 16)).toBe(19 * 16);
    // Mandou a própria entrada dos ticks 0..3 uma única vez (não repete enquanto espera).
    expect(sent.flat().map((r) => r.tick)).toEqual([0, 1, 2, 3]);
    // Chegou a entrada do assento 1 para os ticks 0 e 1: roda exatamente dois ticks.
    ls.receive([{ tick: 1, seat: 1, bits: 1, steer: 0 }, { tick: 0, seat: 1, bits: 1, steer: 0 }]);
    for (let i = 0; i < 5; i++) ls.step(state, 400, (inputs) => stepRace(state, TRACK, inputs));
    expect(state.tick).toBe(2);
    expect(ls.stalledMs(400)).toBe(0);
  });

  it('entrada repetida, atrasada, de assento alheio ou absurda é descartada e contada', () => {
    const state = createRace(config(2), TRACK);
    const ls = new Lockstep({ seats: [0, 1], localSeats: [0], delay: 2 }, { sendInputs: () => undefined, sendHash: () => undefined });
    ls.receive([{ tick: 0, seat: 1, bits: 1, steer: 10 }, { tick: 0, seat: 1, bits: 1, steer: 10 }]);
    expect(ls.stats.duplicates).toBe(1);
    ls.receive([{ tick: 0, seat: 1, bits: 3, steer: 10 }]);
    expect(ls.stats.conflicts).toBe(1);
    ls.receive([{ tick: 5, seat: 0, bits: 1, steer: 0 }]); // alguém mandando pelo assento local
    ls.receive([{ tick: 5, seat: 3, bits: 1, steer: 0 }]); // assento que não está na corrida
    ls.receive([{ tick: 999_999, seat: 1, bits: 1, steer: 0 }]);
    expect(ls.stats.rejected).toBe(3);
    ls.step(state, 0, (inputs) => stepRace(state, TRACK, inputs));
    expect(state.tick).toBe(1);
    ls.receive([{ tick: 0, seat: 1, bits: 1, steer: 10 }]);
    expect(ls.stats.stale).toBe(1);
  });

  it('borda de nitro apertada enquanto a rede trava não se perde: sai no próximo envio', () => {
    const state = createRace(config(2), TRACK);
    const sent: InputRecord[] = [];
    const ls = new Lockstep({ seats: [0, 1], localSeats: [0], delay: 3 }, { sendInputs: (r) => sent.push(...r), sendHash: () => undefined });
    const run = (inputs: PlayerInput[]) => stepRace(state, TRACK, inputs);
    ls.step(state, 0, run); // manda 0..3, trava esperando o assento 1
    ls.setLocalInput(0, { steer: 0.5, throttle: true, brake: false, nitro: true, gearUp: false, gearDown: false });
    ls.setLocalInput(0, { steer: 0.5, throttle: true, brake: false, nitro: false, gearUp: false, gearDown: false });
    ls.step(state, 16, run); // ainda travado: nada sai, a borda fica guardada
    ls.receive([0, 1, 2, 3].map((tick) => ({ tick, seat: 1, bits: 0, steer: 0 })));
    for (let i = 0; i < 3; i++) ls.step(state, 32 + i * 16, run); // roda 0, 1, 2 e manda 4 e 5
    const t4 = sent.find((r) => r.tick === 4 && r.seat === 0);
    const t5 = sent.find((r) => r.tick === 5 && r.seat === 0);
    expect(t4 && decodeInput(t4.bits, t4.steer)).toMatchObject({ nitro: true, throttle: true, steer: 64 / 127 });
    expect(t5 && decodeInput(t5.bits, t5.steer)).toMatchObject({ nitro: false, throttle: true, steer: 64 / 127 });
    expect(t5 && t5.steer).toBe(encodeInput({ steer: 0.5, throttle: true, brake: false, nitro: false, gearUp: false, gearDown: false }).steer);
  });

  it('dessincronia: um cliente que altera o próprio estado é apontado no tick do hash seguinte', () => {
    const net = new Net(createRng(11), 3, 0);
    const cfg = config(3);
    const clients = [0, 1, 2].map((s) => makeClient(net, s, [s], [0, 1, 2], cfg));
    runAll(net, clients, 500);
    // Erro simulado no cliente 2 (um arredondamento diferente, um bit trocado). O z não é
    // corrigido por nada (o x a IA traz de volta para a faixa e o erro sumiria).
    clients[2].state.cars[0].z += 5;
    runAll(net, clients, 700);
    net.deliver(true);
    expect(clients[0].desyncs[0]).toMatchObject({ tick: 540, from: 2 });
    expect(clients[1].desyncs[0]).toMatchObject({ tick: 540, from: 2 });
    expect(clients[2].desyncs.length).toBeGreaterThan(0);
    expect(clients[0].desyncs[0].local).not.toBe(clients[0].desyncs[0].remote);
  }, 30_000);

  it('reconexão por snapshot: quem caiu volta com o estado do anfitrião e todos terminam com o mesmo hash', () => {
    const net = new Net(createRng(21), 5);
    const cfg = config(3);
    const [a, b, c] = [0, 1, 2].map((s) => makeClient(net, s, [s], [0, 1, 2], cfg));
    runAll(net, [a, b, c], 1200);
    // C cai: o que ele já tinha mandado ainda chega (o relay entrega antes de avisar a queda).
    c.online = false;
    net.deliver(true);
    runAll(net, [a, b], 1500, 300); // A e B andam até travar esperando C
    expect(a.state.tick).toBeLessThan(1500);
    expect(a.ls.missingSeats()).toEqual([2]);
    // C volta: o anfitrião (A) manda o snapshot com o buffer e C recomeça dali.
    const snap = { tick: a.state.tick, state: serializeRace(a.state), inputs: a.ls.bufferedRecords(), ai: a.ls.aiList() };
    const c2 = makeClient(net, 2, [2], [0, 1, 2], cfg);
    net.clients.splice(net.clients.indexOf(c), 1);
    c2.state = deserializeRace(snap.state);
    c2.ls = new Lockstep({ seats: [0, 1, 2], localSeats: [2], delay: 3, startTick: snap.tick }, {
      sendInputs: (records) => net.send({ kind: 'i', from: 2, records }),
      sendHash: (tick, hash) => net.send({ kind: 'h', from: 2, tick, hash }),
    });
    c2.ls.onDesync = (r) => c2.desyncs.push(r);
    c2.ls.ingest(snap.inputs);
    runAll(net, [a, b, c2], 3000);
    for (const x of [a, b, c2]) expect(x.state.tick).toBe(3000);
    expect(hashRace(c2.state)).toBe(hashRace(a.state));
    expect(hashRace(b.state)).toBe(hashRace(a.state));
    expect([...a.desyncs, ...b.desyncs, ...c2.desyncs]).toEqual([]);
  }, 60_000);

  it('quem não volta vira IA pelo comando no fluxo de entradas, no mesmo tick em todas as máquinas', () => {
    const net = new Net(createRng(31), 4);
    const cfg = config(3);
    const [a, b, c] = [0, 1, 2].map((s) => makeClient(net, s, [s], [0, 1, 2], cfg));
    runAll(net, [a, b, c], 1000);
    c.online = false;
    net.deliver(true);
    runAll(net, [a, b], 1300, 300);
    expect(a.ls.missingSeats()).toEqual([2]);
    const last = a.ls.lastTickFrom(2);
    const progressBefore = a.state.cars.find((x) => x.seat === 2)?.progress ?? 0;
    const rec = a.ls.takeover(2);
    expect(rec).toEqual({ tick: last + 1, seat: 2, bits: 128, steer: 0 });
    expect(a.ls.takeover(2)).toBeNull(); // uma vez só
    runAll(net, [a, b], 3000);
    expect(a.state.tick).toBe(3000);
    expect(hashRace(b.state)).toBe(hashRace(a.state));
    const carC = a.state.cars.find((x) => x.seat === 2);
    expect(carC?.ai).not.toBeNull();
    // A IA dirigiu de verdade: com entrada neutra o carro pararia em poucos segundos.
    expect((carC?.progress ?? 0) - progressBefore).toBeGreaterThan(60_000);
    expect(a.ls.aiList()).toEqual([2, rec?.tick]);
    expect([...a.desyncs, ...b.desyncs]).toEqual([]);
  }, 60_000);

  it('quem está atrás dos outros sabe quanto (para correr mais ticks por quadro e alcançar)', () => {
    const state = createRace(config(2), TRACK);
    const ls = new Lockstep({ seats: [0, 1], localSeats: [0], delay: 3 }, { sendInputs: () => undefined, sendHash: () => undefined });
    expect(ls.behindBy()).toBe(0);
    ls.receive(Array.from({ length: 40 }, (_, tick) => ({ tick, seat: 1, bits: 1, steer: 0 })));
    // O outro já mandou até o tick 39 = ele está no 36; eu estou no 0.
    expect(ls.behindBy()).toBe(36);
    for (let i = 0; i < 10; i++) ls.step(state, 0, (inputs) => stepRace(state, TRACK, inputs));
    expect(ls.behindBy()).toBe(26);
  });
});
