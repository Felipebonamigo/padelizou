// Modos de festa com regra no núcleo (docs/MODOS.md): escolta e revezamento. Regras puras e
// determinísticas; o estado de cada modo mora em `state.party` (JSON puro), e tudo é aplicado dentro
// do stepRace (race.ts chama os ganchos daqui). O torneio de sofá não precisa de nada aqui: cada
// bateria é uma corrida rápida comum (src/core/tournament.ts).
import {
  ESCORT_GOAL_POSITION, ESCORT_PUSH_DISTANCE, ESCORT_PUSH_LATERAL, ESCORT_PUSH_SPEED_FACTOR, ESCORT_PUSH_TOP_CAP, ESCORT_VIP_TOP_FACTOR,
} from './constants';
import { DIFFICULTY_SKILL } from './sim/ai';
import { wrappedDelta } from './sim/collisions';
import { segmentAt } from './track/builder';
import type { AiBrain, CarState, HumanEntry, PartyState, RaceConfig, RaceState, RelayCar, Track } from './types';

/** Nome do VIP na classificação (igual em PT e EN). */
export const VIP_NAME = 'VIP';
/** Carro do VIP (um dos livres, como a IA). */
export const VIP_CAR_ID = 'falcao';
/** Cor do VIP na interface (etiqueta, minimapa, linha do resultado): dourado, fora das cores de assento. */
export const VIP_COLOR = '#ffb300';

/** Um carro humano da corrida: no revezamento, um por dupla; nos outros modos, um por humano. */
export interface HumanDriver {
  seat: number;
  name: string;
  teamId: number;
  carId: string;
}

/**
 * Duplas do revezamento: os humanos em ordem de assento, de dois em dois (P1 + P2, P3 + P4; com
 * assentos salteados, os dois primeiros ocupados formam a primeira dupla). Quem vem primeiro larga
 * ao volante, e o carro da dupla é o dele. Um número ímpar deixa o último sozinho (o lobby exige 2 ou 4).
 */
export function relayPairs(humans: readonly HumanEntry[]): HumanEntry[][] {
  const sorted = humans.slice().sort((a, b) => a.seat - b.seat);
  const out: HumanEntry[][] = [];
  for (let i = 0; i < sorted.length; i += 2) out.push(sorted.slice(i, i + 2));
  return out;
}

/** Os carros humanos que a corrida cria (no revezamento, um por dupla, com os dois nomes). */
export function humanDrivers(config: RaceConfig): HumanDriver[] {
  if (config.mode === 'relay') {
    return relayPairs(config.humans).map((pair) => ({
      seat: pair[0].seat, name: pair.map((h) => h.name).join(' / '), teamId: pair[0].teamId, carId: pair[0].carId,
    }));
  }
  const humans = config.humans.slice().sort((a, b) => a.seat - b.seat);
  return humans.map((h) => ({ seat: h.seat, name: h.name, teamId: h.teamId, carId: h.carId }));
}

/** Carros da corrida que não são humanos nem IA rival (o VIP da escolta). */
export function extraCars(config: RaceConfig): number {
  return config.mode === 'escort' && config.humans.length > 0 ? 1 : 0;
}

/**
 * Cérebro fixo do VIP (sem sorteio): a habilidade do melhor rival da dificuldade, faixa do meio, sem
 * agressividade. Quem o deixa mais lento que os rivais é ESCORT_VIP_TOP_FACTOR, na velocidade máxima.
 */
export function vipBrain(config: RaceConfig): AiBrain {
  const [, hi] = DIFFICULTY_SKILL[config.difficulty];
  return { skill: hi, laneX: 0, laneUntil: 0, lookahead: 30, aggression: 0 };
}

export type CarFactory = (seat: number, name: string, teamId: number, carId: string) => CarState;

/**
 * Monta os carros do modo e a ordem do grid (o primeiro larga na frente). Escolta: o VIP na pole,
 * a equipe logo atrás dele e a IA atrás de todos — o VIP larga protegido, mas é mais lento: a equipe
 * precisa segurar quem vem. Nos outros modos, a ordem de sempre (IA na frente, humanos no fundo).
 * Os ids são renumerados pela ordem final.
 */
export function arrangeGrid(state: RaceState, ai: CarState[], humans: CarState[], makeCar: CarFactory): CarState[] {
  const config = state.config;
  let cars: CarState[];
  if (config.mode === 'escort' && humans.length > 0) {
    const vip = makeCar(-1, VIP_NAME, humans[0].teamId, VIP_CAR_ID);
    vip.stats = { ...vip.stats, topSpeed: vip.stats.topSpeed * ESCORT_VIP_TOP_FACTOR };
    vip.ai = vipBrain(config);
    cars = [vip, ...humans, ...ai];
    state.party = { vipId: 0, pushBy: -1, relay: [] };
  } else {
    cars = [...ai, ...humans];
  }
  cars.forEach((c, i) => { c.id = i; });
  if (config.mode === 'relay') {
    const relay: RelayCar[] = [];
    for (const pair of relayPairs(config.humans)) {
      const car = cars.find((c) => c.seat === pair[0].seat);
      if (car) relay.push({ carId: car.id, seats: pair.map((h) => h.seat), active: 0, due: false, sawPit: false, swaps: 0 });
    }
    state.party = { vipId: -1, pushBy: -1, relay };
  }
  return cars;
}

/** O VIP da escolta, ou null. */
export function vipCar(state: RaceState): CarState | null {
  const id = state.party?.vipId ?? -1;
  return id >= 0 ? state.cars[id] ?? null : null;
}

export function isVip(state: RaceState, car: CarState): boolean {
  return state.party !== undefined && state.party.vipId === car.id;
}

/** Carro que conta como "da equipe humana" para a chegada: os humanos e o VIP. */
export function countsAsHuman(state: RaceState, car: CarState): boolean {
  return car.seat >= 0 || isVip(state, car);
}

/**
 * Escolta: companheiro humano colado atrás do VIP, na mesma faixa e mais rápido, empurra — o VIP vai
 * a ESCORT_PUSH_SPEED_FACTOR da velocidade de quem empurra, até ESCORT_PUSH_TOP_CAP da máxima dele.
 * Vale sempre na escolta (é a mecânica do modo, não a assistência "empurrão"). O evento 'tow' sai só
 * quando um empurrão começa (ou troca de quem empurra).
 */
export function applyEscortPush(state: RaceState, track: Track): void {
  const party = state.party;
  const vip = vipCar(state);
  if (!party || !vip) return;
  if (state.phase !== 'racing' || vip.finished) { party.pushBy = -1; return; }
  let pusher: CarState | null = null;
  for (const mate of state.cars) {
    if (mate.seat < 0 || mate.finished || mate.teamId !== vip.teamId) continue;
    const d = wrappedDelta(vip.z, mate.z, track.length);
    if (d <= 0 || d >= ESCORT_PUSH_DISTANCE || Math.abs(mate.x - vip.x) >= ESCORT_PUSH_LATERAL) continue;
    if (mate.speed <= vip.speed) continue;
    if (!pusher || mate.speed > pusher.speed) pusher = mate;
  }
  if (!pusher) { party.pushBy = -1; return; }
  vip.speed = Math.max(vip.speed, Math.min(pusher.speed * ESCORT_PUSH_SPEED_FACTOR, vip.stats.topSpeed * ESCORT_PUSH_TOP_CAP));
  if (party.pushBy !== pusher.id) state.events.push({ type: 'tow', carId: vip.id, byId: pusher.id });
  party.pushBy = pusher.id;
}

export interface EscortOutcome {
  vipPosition: number;
  success: boolean;
}

/** Resultado da escolta: vale a posição do VIP (entre os ESCORT_GOAL_POSITION primeiros = vitória). */
export function escortOutcome(state: RaceState): EscortOutcome | null {
  const vip = vipCar(state);
  if (!vip) return null;
  const row = state.results?.find((r) => r.carId === vip.id);
  const vipPosition = row ? row.position : vip.position;
  return { vipPosition, success: vipPosition <= ESCORT_GOAL_POSITION };
}

/**
 * Índice em `state.cars` do carro que o assento vê: o próprio carro, ou, no revezamento, o carro da
 * dupla (quem espera a vez assiste ao parceiro). -1 = nenhum.
 */
export function carIndexOfSeat(state: RaceState, seat: number): number {
  const own = state.cars.findIndex((c) => c.seat === seat);
  if (own >= 0) return own;
  const rc = relayOfSeat(state, seat);
  return rc ? rc.carId : -1;
}

/** O carro de revezamento da dupla do assento, ou null. */
export function relayOfSeat(state: RaceState, seat: number): RelayCar | null {
  return state.party?.relay.find((rc) => rc.seats.includes(seat)) ?? null;
}

/** Quem dirige o carro de revezamento agora e quem espera (o parceiro), ou null fora do revezamento. */
export function relayTurn(state: RaceState, seat: number): { car: CarState; relay: RelayCar; driving: boolean; partner: number } | null {
  const rc = relayOfSeat(state, seat);
  const car = rc ? state.cars[rc.carId] : undefined;
  if (!rc || !car) return null;
  const partner = rc.seats.find((s) => s !== seat) ?? -1;
  return { car, relay: rc, driving: car.seat === seat, partner };
}

/**
 * Revezamento, a cada tick depois das posições: fechar uma volta (sem ser a última) libera a troca;
 * com ela pendente, entrar no box passa o controle ao parceiro (o `seat` do carro muda, e com ele a
 * entrada que o stepRace lê a partir do próximo tick). Passar pelo trecho do box sem entrar perde a
 * troca desta volta: quem dirige segue mais uma. A entrada de quem espera nunca é lida.
 */
function updateRelay(state: RaceState, track: Track): void {
  const party = state.party;
  if (!party || party.relay.length === 0 || state.phase !== 'racing') return;
  const laps = state.config.laps;
  for (const rc of party.relay) {
    const car = state.cars[rc.carId];
    if (!car || car.finished || rc.seats.length < 2) { rc.due = false; continue; }
    for (const e of state.events) {
      if (e.type === 'lap' && e.carId === car.id && e.lap >= 2 && e.lap <= laps && !rc.due) {
        rc.due = true; rc.sawPit = false;
        state.events.push({ type: 'relay_due', carId: car.id });
        break;
      }
    }
    if (!rc.due) continue;
    if (car.inPit) {
      const from = car.seat;
      rc.active = (rc.active + 1) % rc.seats.length;
      car.seat = rc.seats[rc.active];
      rc.due = false; rc.sawPit = false; rc.swaps++;
      state.events.push({ type: 'relay_swap', carId: car.id, fromSeat: from, toSeat: car.seat });
    } else if (segmentAt(track, car.z).pit) {
      rc.sawPit = true;
    } else if (rc.sawPit) {
      rc.due = false; rc.sawPit = false;
      state.events.push({ type: 'relay_missed', carId: car.id });
    }
  }
}

/** A corrida pode ir para o online? Os modos de festa são só locais (docs/MODOS.md). */
export function onlineAllowed(config: Pick<RaceConfig, 'mode'>): boolean {
  return config.mode === undefined;
}

/** Valores padrão do estado de festa (deserializeRace: estado salvo sem algum campo). */
export function normalizeParty(party: Partial<PartyState> | undefined): PartyState | undefined {
  if (!party) return undefined;
  return { vipId: party.vipId ?? -1, pushBy: party.pushBy ?? -1, relay: party.relay ?? [] };
}

/** Regras de modo depois das posições de cada tick (revezamento: troca no box). */
export function updateModes(state: RaceState, track: Track): void {
  updateRelay(state, track);
}
