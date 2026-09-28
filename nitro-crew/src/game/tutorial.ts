// Tutorial de 90 segundos (passo 1.9): a máquina de passos, pura e sem DOM. Recebe o estado da
// corrida a cada tick e devolve o que mudou (passo cumprido, repetir, carro parado, fim); a visão
// de cada assento diz o passo, as ações a mostrar e o progresso. Nada aqui muda a corrida: o único
// efeito é o comando do carro que o passo 6 para de propósito (tutorialInput), aplicado pela sessão
// antes do stepRace — como qualquer outra entrada.
//
// Cada jogador anda pelos passos 1–5 no próprio ritmo; os que dependem de um trecho (curvas, box)
// só contam uma passagem inteira por ele, do começo ao fim. O passo 6 (empurrão) é da equipe e só
// existe com 2+ jogadores: quando todos passaram do box, o líder é parado na reta e qualquer
// companheiro que passar colado conclui.
import { OFFROAD_X, SEGMENT_LENGTH, SPEED_TO_KMH } from '../core/constants';
import { wrappedDelta } from '../core/sim/collisions';
import { holdableSpeedFraction } from '../core/sim/physics';
import { carStats } from '../core/sim/stats';
import type { CarState, PlayerInput, RaceState } from '../core/types';
import { NEUTRAL_INPUT } from '../core/types';
import type { BindAction } from '../ui/remap/bindings';
import { inSection, STRONG_CURVE, type SectionId } from './tutorial-track';

export type TutorialStepId = 'throttle' | 'curve' | 'brake' | 'nitro' | 'pit' | 'tow';
export const TUTORIAL_STEPS: readonly TutorialStepId[] = ['throttle', 'curve', 'brake', 'nitro', 'pit', 'tow'];
const TOW_STEP = TUTORIAL_STEPS.indexOf('tow');

/** Passo 1: velocidade a alcançar. */
export const TARGET_KMH = 150;
/** Combustível com que os humanos largam (o tanque é 1): dá uma volta e pouco, e o HUD já pisca. */
export const START_FUEL = 0.22;
/** Passo 5: nível do tanque, ainda dentro do box, que conta como abastecido. */
export const REFUEL_TARGET = 0.6;
/** Passo 3: a entrada da curva forte aceita até esta folga sobre a velocidade que o carro segura nela. */
export const BRAKE_MARGIN = 1.15;
/** Passo 6: o carro escolhido só para quando está nesta faixa lateral (no asfalto, longe do box). */
const PARK_MAX_X = 0.9;
/** Passo 6: ultrapassar o carro parado a menos disto (u) sem empurrar conta como "passou direto". */
const TOW_PASS_WINDOW = SEGMENT_LENGTH * 10;

/**
 * Por que o passo recomeça: saiu do asfalto, entrou rápido demais na curva forte, passou reto pelo
 * box, ou entrou no box e saiu antes de abastecer (o limitador do box só freia aos poucos: quem entra
 * embalado atravessa a faixa sem tempo de encher).
 */
export type RetryReason = 'offroad' | 'fast' | 'pitMissed' | 'pitShort';

export interface SeatProgress {
  seat: number;
  /** Índice em TUTORIAL_STEPS; igual ao total do tutorial = terminou. */
  step: number;
  /** Segmento do carro no tick anterior (−1 = ainda não visto). */
  lastSeg: number;
  /** Entrou no trecho do passo atual pelo começo e ainda não saiu (nem errou); no passo 5, entrou na faixa do box. */
  tracking: boolean;
  /** Motivo da última repetição, para o painel; limpo quando o passo é cumprido. */
  retry: RetryReason | null;
  /** Passo 6: estava atrás do carro parado no tick anterior (null = ainda não visto). */
  towBehind: boolean | null;
}

export type TutorialPhase = 'running' | 'done' | 'skipped' | 'timeout';

export interface TutorialState {
  seats: SeatProgress[];
  /** Total de passos: 6 com 2+ jogadores, 5 sozinho (o empurrão fica de fora). */
  total: number;
  /** Assento do carro que o passo 6 para de propósito (−1 = ainda não escolhido). */
  towSeat: number;
  /** O carro escolhido já está parado esperando o empurrão. */
  parked: boolean;
  phase: TutorialPhase;
}

export type TutorialEvent =
  | { type: 'stepDone'; seat: number; step: TutorialStepId }
  | { type: 'retry'; seat: number; step: TutorialStepId; reason: RetryReason }
  /** Passo 4 sem carga de nitro (gasta antes): segue sem ele. */
  | { type: 'noNitro'; seat: number }
  | { type: 'parked'; seat: number }
  | { type: 'towed'; seat: number; bySeat: number }
  /** Um companheiro passou pelo carro parado sem empurrar (rápido demais ou longe demais do lado). */
  | { type: 'towMissed'; seat: number }
  | { type: 'done' }
  /** Alguém começou a última volta sem a equipe terminar: o tutorial acaba antes da bandeirada. */
  | { type: 'timeout' };

export function newTutorial(seats: readonly number[]): TutorialState {
  const sorted = seats.slice().sort((a, b) => a - b);
  return {
    seats: sorted.map((seat) => ({ seat, step: 0, lastSeg: -1, tracking: false, retry: null, towBehind: null })),
    total: sorted.length >= 2 ? TUTORIAL_STEPS.length : TOW_STEP,
    towSeat: -1, parked: false, phase: 'running',
  };
}

export function stepId(index: number): TutorialStepId | null {
  return TUTORIAL_STEPS[index] ?? null;
}

/** Esc/Start: encerra (a sessão mostra as regras de ouro). */
export function skipTutorial(tut: TutorialState): void {
  if (tut.phase === 'running') tut.phase = 'skipped';
}

function carOf(state: RaceState, seat: number): CarState | undefined {
  return state.cars.find((c) => c.seat === seat);
}

function segmentOf(car: CarState): number {
  return Math.floor(car.z / SEGMENT_LENGTH);
}

/** Velocidade máxima de entrada na curva forte que conta como "freou antes". */
export function strongCurveEntryLimit(car: CarState): number {
  const s = carStats(car);
  return holdableSpeedFraction(s, STRONG_CURVE) * s.topSpeed * BRAKE_MARGIN;
}

function nitroCharges(state: RaceState, car: CarState): number {
  return state.config.assists.sharedNitro ? (state.teamNitro[car.teamId] ?? 0) : car.nitroLeft;
}

function advance(p: SeatProgress, events: TutorialEvent[]): void {
  const step = TUTORIAL_STEPS[p.step];
  p.step++;
  p.tracking = false;
  p.retry = null;
  if (step) events.push({ type: 'stepDone', seat: p.seat, step });
}

function retry(p: SeatProgress, reason: RetryReason, events: TutorialEvent[]): void {
  const step = TUTORIAL_STEPS[p.step];
  p.tracking = false;
  p.retry = reason;
  if (step) events.push({ type: 'retry', seat: p.seat, step, reason });
}

/**
 * Passo de trecho: conta só a passagem inteira (entrou pelo começo, saiu pelo fim) sem sair do
 * asfalto. `onEnter` pode reprovar já na entrada (curva forte rápido demais); sair do asfalto
 * reprova na hora — o aviso vem quando o erro acontece, e o passo espera a próxima volta.
 */
function traverse(p: SeatProgress, car: CarState, seg: number, id: SectionId, events: TutorialEvent[], onEnter?: () => RetryReason | null): void {
  const inside = inSection(id, seg);
  const wasInside = inSection(id, p.lastSeg);
  if (inside && !wasInside) {
    p.tracking = true;
    const verdict = onEnter?.() ?? null;
    if (verdict) { retry(p, verdict, events); return; }
  }
  if (inside && p.tracking && Math.abs(car.x) > OFFROAD_X) { retry(p, 'offroad', events); return; }
  if (!inside && wasInside && p.tracking) advance(p, events);
}

function stepSeat(p: SeatProgress, car: CarState, seg: number, state: RaceState, events: TutorialEvent[]): void {
  switch (TUTORIAL_STEPS[p.step]) {
    case 'throttle':
      if (car.speed * SPEED_TO_KMH >= TARGET_KMH) advance(p, events);
      break;
    case 'curve':
      traverse(p, car, seg, 'curve', events);
      break;
    case 'brake':
      traverse(p, car, seg, 'strong', events, () => (car.speed > strongCurveEntryLimit(car) ? 'fast' : null));
      break;
    case 'nitro':
      if (state.events.some((e) => e.type === 'nitro' && e.carId === car.id)) advance(p, events);
      else if (car.nitroTicks === 0 && nitroCharges(state, car) <= 0) { events.push({ type: 'noNitro', seat: p.seat }); advance(p, events); }
      break;
    case 'pit':
      if (car.inPit && car.fuel >= REFUEL_TARGET) { advance(p, events); break; }
      if (car.inPit) p.tracking = true;
      if (!inSection('pit', seg) && inSection('pit', p.lastSeg)) retry(p, p.tracking ? 'pitShort' : 'pitMissed', events);
      break;
    default:
      break;
  }
}

/** Passo 6: escolhe o líder, para o carro dele na reta e espera o empurrão de um companheiro. */
function stepTow(tut: TutorialState, state: RaceState, events: TutorialEvent[]): void {
  if (tut.total <= TOW_STEP || !tut.seats.every((p) => p.step >= TOW_STEP)) return;
  if (tut.towSeat < 0) {
    let leader: CarState | undefined;
    for (const p of tut.seats) {
      const c = carOf(state, p.seat);
      if (c && (!leader || c.progress > leader.progress)) leader = c;
    }
    if (!leader) return;
    tut.towSeat = leader.seat;
  }
  const car = carOf(state, tut.towSeat);
  if (!car) return;
  if (!tut.parked) {
    if (inSection('accel', segmentOf(car)) && Math.abs(car.x) <= PARK_MAX_X) {
      tut.parked = true;
      events.push({ type: 'parked', seat: car.seat });
    }
    return;
  }
  const tow = state.events.find((e) => e.type === 'tow' && e.carId === car.id);
  if (tow && tow.type === 'tow') {
    const by = state.cars[tow.byId];
    events.push({ type: 'towed', seat: car.seat, bySeat: by ? by.seat : -1 });
    tut.parked = false;
    for (const p of tut.seats) if (p.step === TOW_STEP) advance(p, events);
    return;
  }
  for (const p of tut.seats) {
    const mate = p.seat === car.seat ? undefined : carOf(state, p.seat);
    if (!mate) continue;
    const d = wrappedDelta(mate.z, car.z, state.trackLength);
    if (p.towBehind === true && d > 0 && d < TOW_PASS_WINDOW) events.push({ type: 'towMissed', seat: p.seat });
    p.towBehind = d <= 0;
  }
}

/** Um tick: chamar depois de cada stepRace, com o estado já avançado (lê `state.events` do tick). */
export function updateTutorial(tut: TutorialState, state: RaceState): TutorialEvent[] {
  const events: TutorialEvent[] = [];
  if (tut.phase !== 'running') return events;
  for (const p of tut.seats) {
    const car = carOf(state, p.seat);
    if (!car) continue;
    const seg = segmentOf(car);
    if (state.phase === 'racing' && p.step < tut.total) stepSeat(p, car, seg, state, events);
    p.lastSeg = seg;
  }
  if (state.phase === 'racing') stepTow(tut, state, events);
  if (tut.seats.every((p) => p.step >= tut.total)) {
    tut.phase = 'done';
    events.push({ type: 'done' });
  } else if (state.cars.some((c) => c.seat >= 0 && c.lap >= state.config.laps)) {
    tut.phase = 'timeout';
    events.push({ type: 'timeout' });
  }
  return events;
}

/** O comando que vai para a simulação: o carro parado no passo 6 fica no freio, o resto passa igual. */
export function tutorialInput(tut: TutorialState, seat: number, input: PlayerInput): PlayerInput {
  if (tut.phase === 'running' && tut.parked && seat === tut.towSeat) return { ...NEUTRAL_INPUT, brake: true };
  return input;
}

// ───────────────────────────── Visão por assento ─────────────────────────────

/**
 * O que o painel mostra a um assento. `step` 'wait' = já passou do box e espera a equipe;
 * 'stalled' = é o carro que o passo 6 para (antes ou depois de parar, ver `parked`); 'done' = acabou.
 */
export interface TutorialView {
  step: TutorialStepId | 'wait' | 'stalled' | 'done';
  /** Passo em 1..total (o painel mostra "PASSO 2/6"). */
  index: number;
  total: number;
  /** Ações cujas teclas/botões o painel mostra. */
  actions: BindAction[];
  retry: RetryReason | null;
  /** 0..1 para a barra do passo (velocidade, tanque); null = sem barra. */
  progress: number | null;
  /** Assento do carro parado no passo 6 (para o nome); −1 fora dele. */
  towSeat: number;
  parked: boolean;
}

const ACTIONS: Readonly<Record<TutorialView['step'], BindAction[]>> = {
  throttle: ['throttle'],
  curve: ['left', 'right'],
  brake: ['brake'],
  nitro: ['nitro'],
  pit: ['right', 'brake'],
  tow: ['throttle', 'left', 'right'],
  wait: ['throttle'],
  stalled: [],
  done: [],
};

export function tutorialView(tut: TutorialState, state: RaceState, seat: number): TutorialView {
  const p = tut.seats.find((s) => s.seat === seat);
  const car = carOf(state, seat);
  const base = { total: tut.total, towSeat: tut.towSeat, parked: tut.parked, retry: p?.retry ?? null };
  if (!p || p.step >= tut.total || tut.phase === 'done') return { ...base, step: 'done', index: tut.total, actions: [], progress: 1 };
  const id = TUTORIAL_STEPS[p.step];
  let step: TutorialView['step'] = id;
  if (id === 'tow') {
    const everyone = tut.seats.every((s) => s.step >= TOW_STEP);
    step = !everyone ? 'wait' : seat === tut.towSeat ? 'stalled' : 'tow';
  }
  let progress: number | null = null;
  if (car && step === 'throttle') progress = Math.min(1, (car.speed * SPEED_TO_KMH) / TARGET_KMH);
  if (car && step === 'pit') progress = Math.min(1, car.fuel / REFUEL_TARGET);
  return { ...base, step, index: p.step + 1, actions: ACTIONS[step], progress };
}
