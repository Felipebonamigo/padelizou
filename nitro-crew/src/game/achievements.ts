// Conquistas: regras avaliadas pela sessão no tick em que a corrida acaba, com a telemetria que
// ela junta a cada tick (observeTick). A lista com nomes PT/EN mora em desktop.ts (ponte com a
// Steam) e as descrições em src/stats/strings.ts; aqui ficam a coleta e as regras.
import { CAR_HALF_WIDTH, CAR_LENGTH, COLLISION_COOLDOWN_TICKS, TICK_RATE } from '../core/constants';
import { CUPS } from '../core/data/cups';
import { wrappedDelta } from '../core/sim/collisions';
import { computeModifiers } from '../core/sim/coop';
import { TRACKS } from '../core/track';
import type { CarState, HumanEntry, RaceResultRow, RaceState, SimEvent, Track } from '../core/types';
import { getLanguage, t } from '../i18n';
import '../i18n/core';
import '../stats/strings';
import type { RaceMode, SaveData } from './contracts';
import { ACHIEVEMENTS } from './desktop';
import './strings';

/** MESTRE_DO_VACUO: tempo no vácuo numa mesma corrida. */
export const DRAFT_MASTER_TICKS = 60 * TICK_RATE;
/** MARATONA: distância somada de todos os jogadores, em metros. */
export const MARATHON_METERS = 1_000_000;
/** DEZ_VITORIAS. */
export const WINS_TARGET = 10;
/**
 * Folga lateral do contato visto pelo estado: resolveCarCollisions (src/core/sim/collisions.ts)
 * separa os dois carros em 0,03 para cada lado no mesmo tick, então depois do passo uma batida de
 * quina já não se sobrepõe. Custo aceito: passar a menos de 0,06 de outro carro também conta.
 */
export const CONTACT_LATERAL_MARGIN = 0.06;

/** Contadores de um assento nesta corrida (a sessão observa; stats.ts e as regras leem). */
export interface SeatTelemetry {
  nitros: number;
  towsGiven: number;
  towsReceived: number;
  /** Contatos carro-carro (quem bate e quem é batido; raspão lado a lado também). Ver observeTick. */
  collisions: number;
  /** Batidas no cenário. */
  crashes: number;
  pitStops: number;
  /** Ticks com vácuo, correndo e antes da chegada. */
  draftTicks: number;
  /** Progresso no grid (primeiro tick observado), para medir a distância. */
  startProgress: number;
  /** Cruzou a chegada com o nitro ligado. */
  nitroAtFinish: boolean;
  /** Estava em último ao fechar a primeira volta. */
  lastAfterFirstLap: boolean;
}

export interface RaceTelemetry {
  /** Por assento: entrou no box nesta corrida. */
  pitted: Set<number>;
  /** Por assento: nitros usados na volta atual e máximo por volta. */
  nitrosThisLap: Map<number, number>;
  maxNitrosInLap: Map<number, number>;
  /** Por assento: deu um empurrão a um companheiro. */
  gaveTow: Set<number>;
  /** Por assento: saiu do asfalto na volta atual. */
  offroadThisLap: Set<number>;
  perfectLap: Set<number>;
  /** Contadores por assento, preenchidos por observeTick. */
  seats: Map<number, SeatTelemetry>;
  /** Último tick em contato, por par "assento>carro" (janela de uma colisão). */
  contactTick: Map<string, number>;
}

export function newTelemetry(): RaceTelemetry {
  return {
    pitted: new Set(), nitrosThisLap: new Map(), maxNitrosInLap: new Map(), gaveTow: new Set(), offroadThisLap: new Set(),
    perfectLap: new Set(), seats: new Map(), contactTick: new Map(),
  };
}

function blankSeat(startProgress: number): SeatTelemetry {
  return {
    nitros: 0, towsGiven: 0, towsReceived: 0, collisions: 0, crashes: 0, pitStops: 0, draftTicks: 0, startProgress,
    nitroAtFinish: false, lastAfterFirstLap: false,
  };
}

/**
 * Telemetria de um tick, chamada pela sessão logo depois de stepRace (antes de tratar os eventos,
 * para que o tick que fecha a corrida já esteja contado). Só lê o estado. O vácuo não vira evento
 * no núcleo; aqui ele é recalculado com a mesma função pura que a física usa (computeModifiers).
 */
export function observeTick(tel: RaceTelemetry, state: RaceState, track: Track): void {
  for (const car of state.cars) if (car.seat >= 0 && !tel.seats.has(car.seat)) tel.seats.set(car.seat, blankSeat(car.progress));
  const human = (carId: number): SeatTelemetry | null => {
    const car = state.cars[carId];
    // Depois da chegada o carro segue no piloto automático: o que ele faz não é do jogador.
    return car && car.seat >= 0 && !car.finished ? tel.seats.get(car.seat) ?? null : null;
  };
  for (const e of state.events) observeEvent(state, e, human, tel);
  if (state.phase !== 'racing') return;
  for (const car of state.cars) {
    if (car.seat < 0 || car.finished) continue;
    const seat = tel.seats.get(car.seat);
    if (!seat) continue;
    if (computeModifiers(state, track, car).draft) seat.draftTicks++;
    observeContacts(tel, state, track, car, seat);
  }
}

/** Caixas do núcleo (comprimento e largura do carro) sobrepostas, com a folga do empurrão lateral. */
function touching(a: CarState, b: CarState, trackLength: number): boolean {
  return Math.abs(wrappedDelta(a.z, b.z, trackLength)) < CAR_LENGTH
    && Math.abs(a.x - b.x) < CAR_HALF_WIDTH * 2 + CONTACT_LATERAL_MARGIN;
}

/**
 * Colisões pelo estado, não só pelo evento: o núcleo só emite 'collision' com os DOIS carros fora
 * do cooldown, mas aplica a batida mesmo assim — bater numa IA que acabou de bater em outra não
 * gerava evento. Aqui conta cada contato (o evento do tick também vale como contato); um contato
 * que continua, ou outro com o mesmo carro em até COLLISION_COOLDOWN_TICKS, é a mesma colisão.
 */
function observeContacts(tel: RaceTelemetry, state: RaceState, track: Track, car: CarState, seat: SeatTelemetry): void {
  for (const other of state.cars) {
    if (other === car) continue;
    const byEvent = state.events.some((e) => e.type === 'collision'
      && ((e.carId === car.id && e.otherId === other.id) || (e.carId === other.id && e.otherId === car.id)));
    if (!byEvent && !touching(car, other, track.length)) continue;
    const key = `${car.seat}>${other.id}`;
    const last = tel.contactTick.get(key);
    if (last === undefined || state.tick - last > COLLISION_COOLDOWN_TICKS) seat.collisions++;
    tel.contactTick.set(key, state.tick);
  }
}

function observeEvent(state: RaceState, e: SimEvent, human: (carId: number) => SeatTelemetry | null, tel: RaceTelemetry): void {
  switch (e.type) {
    case 'nitro': { const s = human(e.carId); if (s) s.nitros++; break; }
    case 'tow': {
      const got = human(e.carId); if (got) got.towsReceived++;
      const gave = human(e.byId); if (gave) gave.towsGiven++;
      break;
    }
    case 'crash': { const s = human(e.carId); if (s) s.crashes++; break; }
    case 'pit_enter': { const s = human(e.carId); if (s) s.pitStops++; break; }
    case 'finish': {
      // O carro já está como terminado aqui; o nitro do tick da chegada é o que vale.
      const car = state.cars[e.carId];
      const s = car && car.seat >= 0 ? tel.seats.get(car.seat) : undefined;
      if (car && s && car.nitroTicks > 0) s.nitroAtFinish = true;
      break;
    }
    case 'lap': {
      // lap = volta que começa; 2 = acabou de fechar a primeira. As posições já são as deste tick.
      const car = state.cars[e.carId];
      const s = human(e.carId);
      if (car && s && e.lap === 2 && state.cars.length > 1 && car.position === state.cars.length) s.lastAfterFirstLap = true;
      break;
    }
    default: break;
  }
}

/** Uma conquista desbloqueada e os assentos que a ganharam (as de equipe vão para todos). */
export interface AchievementUnlock {
  id: string;
  seats: number[];
}

/** Conquistas desbloqueadas por esta corrida (ids ainda não presentes no save), com quem as ganhou. */
export function unlockAchievements(
  save: SaveData, mode: RaceMode, state: RaceState, results: RaceResultRow[], humans: HumanEntry[], telemetry: RaceTelemetry,
  trackNight: boolean, cupJustCompleted: string | null, difficulty: string,
): AchievementUnlock[] {
  const out = new Map<string, Set<number>>();
  const add = (id: string, seats: readonly number[]) => {
    if (save.achievements.includes(id)) return;
    const set = out.get(id) ?? new Set<number>();
    for (const s of seats) set.add(s);
    out.set(id, set);
  };
  const everyone = humans.map((h) => h.seat);
  const timeTrial = mode === 'timetrial' || state.config.timeTrial === true;
  const vsAi = !timeTrial && state.cars.some((c) => c.seat < 0);
  const humanRows = results.filter((r) => r.seat >= 0);
  const winners = timeTrial ? [] : humanRows.filter((r) => r.position === 1).map((r) => r.seat);

  if (winners.length) add('PRIMEIRA_VITORIA', winners);
  if (winners.length && trackNight) add('MADRUGADA', winners);
  if (humans.length === 4) add('EQUIPE_COMPLETA', everyone);
  for (const r of humanRows) {
    const seat = telemetry.seats.get(r.seat);
    if (r.position === 1 && !telemetry.pitted.has(r.seat) && !timeTrial) add('SEM_BOX', [r.seat]);
    if ((telemetry.maxNitrosInLap.get(r.seat) ?? 0) >= 3) add('NITRO_TRIPLO', [r.seat]);
    if (telemetry.gaveTow.has(r.seat)) add('EMPURRAO', [r.seat]);
    if (telemetry.perfectLap.has(r.seat)) add('VOLTA_PERFEITA', [r.seat]);
    if (!seat) continue;
    if (vsAi && r.finished && seat.collisions === 0 && seat.crashes === 0) add('SEM_ARRANHAO', [r.seat]);
    if (seat.draftTicks >= DRAFT_MASTER_TICKS) add('MESTRE_DO_VACUO', [r.seat]);
    if (seat.nitroAtFinish) add('NITRO_NA_BANDEIRA', [r.seat]);
    if (!timeTrial && r.position === 1 && seat.lastAfterFirstLap) add('DO_ULTIMO_AO_PRIMEIRO', [r.seat]);
  }
  const podium = humanRows.filter((r) => r.position <= 3);
  if (vsAi && podium.length >= 3) add('PODIO_DE_EQUIPE', podium.map((r) => r.seat));
  // As cumulativas leem o save já com esta corrida somada (a sessão grava as estatísticas antes).
  const totals = save.stats.totals;
  if (totals.meters >= MARATHON_METERS) add('MARATONA', everyone);
  if (totals.wins >= WINS_TARGET) add('DEZ_VITORIAS', winners.length ? winners : everyone);
  if (TRACKS.every((def) => totals.bestPositions[def.id] !== undefined)) add('GIRO_COMPLETO', everyone);
  if (cupJustCompleted) {
    add(`COPA_${cupJustCompleted.toUpperCase()}`, everyone);
    if (difficulty === 'campeao') add('CAMPEAO', everyone);
  }
  return [...out].map(([id, seats]) => ({ id, seats: [...seats].sort((a, b) => a - b) }));
}

/** Só os ids (compatível com o contrato antigo). */
export function evaluateAchievements(
  save: SaveData, mode: RaceMode, state: RaceState, results: RaceResultRow[], humans: HumanEntry[], telemetry: RaceTelemetry,
  trackNight: boolean, cupJustCompleted: string | null, difficulty: string,
): string[] {
  return unlockAchievements(save, mode, state, results, humans, telemetry, trackNight, cupJustCompleted, difficulty).map((u) => u.id);
}

// ───────────────────────────── Textos ─────────────────────────────

/** Nome no idioma atual (o id, se não estiver na lista da Steam). */
export function achievementName(id: string): string {
  const def = ACHIEVEMENTS.find((a) => a.id === id);
  return def ? def[getLanguage()] : id;
}

/**
 * Descrição no idioma atual. As de copa saem do nome da copa (core.cup.<id>): copa nova em
 * data/cups.ts ganha descrição sem precisar de uma string por conquista.
 */
export function achievementDescription(id: string): string {
  const key = `stats.achDesc.${id}`;
  const own = t(key);
  if (own !== key) return own;
  const cup = CUPS.find((c) => `COPA_${c.id.toUpperCase()}` === id);
  if (!cup) return own;
  const nameKey = `core.cup.${cup.id}`;
  const name = t(nameKey);
  return t('stats.achDescCup', { cup: name === nameKey ? cup.name : name });
}

/**
 * Uma mensagem de HUD por assento com as conquistas que ele ganhou. O centro do HUD tem 3
 * vagas (e a de chegada ocupa uma), então várias conquistas viram uma linha só.
 */
export function achievementMessages(unlocks: readonly AchievementUnlock[]): Map<number, string> {
  const bySeat = new Map<number, string[]>();
  for (const u of unlocks) for (const seat of u.seats) bySeat.set(seat, [...(bySeat.get(seat) ?? []), achievementName(u.id)]);
  const out = new Map<number, string>();
  for (const [seat, names] of bySeat) {
    if (names.length === 1) out.set(seat, t('session.achievement', { name: names[0] }));
    else {
      const shown = names.slice(0, 2).join(' · ');
      out.set(seat, t('stats.hudMany', { names: names.length > 2 ? `${shown} +${names.length - 2}` : shown }));
    }
  }
  return out;
}
