// Termômetro de emoção: o que um carro viveu na corrida, lido do estado a cada tick (ultrapassagens, tempo em
// disputa ou sozinho, pé no fundo, nitro, raspões e batidas traseiras). Só LÊ: nunca altera o estado, não usa relógio
// nem sorteio, e importa apenas do núcleo (../core/**) — o scripts/balance.ts roda em Node e o diário de jogo
// (playlog.ts) guarda o resultado por assento. As constantes ficam aqui e não em src/core/constants.ts de propósito:
// são medida, não jogabilidade (a content-version põe TODAS as constantes do núcleo na impressão da volta, e mexer
// nelas faria recordes e fantasmas passarem a "versão anterior").
// Uso: um rastreador por CARRO (no revezamento a dupla divide o carro); `observeFeel` logo depois de cada `stepRace`.
import { CAR_HALF_WIDTH, CAR_LENGTH, COLLISION_COOLDOWN_TICKS, COLLISION_SLOP_X, COLLISION_SLOP_Z, REFERENCE_SPEED, TICK_RATE } from '../core/constants';
import { wrappedDelta } from '../core/sim/collisions';
import { carStats } from '../core/sim/stats';
import type { RaceState } from '../core/types';

/** Em disputa: o vizinho de posição (±1) a até 5 carros de distância no progresso. */
export const BATTLE_GAP = CAR_LENGTH * 5; // 1000 u
/** Sozinho: os vizinhos de posição a mais de meio segundo na velocidade de referência. */
export const ALONE_GAP = REFERENCE_SPEED / 2; // 3000 u
/** "Pé no fundo": velocidade ≥ 85% da máxima do carro (pelo estado: vale igual para IA e humano). */
export const FLAT_OUT_FRACTION = 0.85;

// Folga do contato visto pelo estado: a mesma régua de achievements.ts (CONTACT_LATERAL_MARGIN e
// CONTACT_LONGITUDINAL_MARGIN, `touching`), COPIADA aqui porque achievements.ts puxa i18n, desktop e strings.
const CONTACT_LATERAL = COLLISION_SLOP_X * 5;
const CONTACT_LONGITUDINAL = COLLISION_SLOP_Z * 5;

export interface RaceFeel {
  seconds: number;
  overtakes: number;
  overtakesPerMin: number;
  battleSeconds: number;
  aloneSeconds: number;
  flatOutPct: number;
  nitroPct: number;
  scrapes: number;
  rearHits: number;
}

export interface FeelTracker {
  readonly carId: number;
  ticks: number;
  prevPosition: number;
  overtakes: number;
  battleTicks: number;
  aloneTicks: number;
  flatTicks: number;
  nitroOnTicks: number;
  scrapes: number;
  rearHits: number;
  /** Não é emoção: o carro secou o tanque. Fica aqui por ser o único observador por tick e por carro. */
  fuelEmpty: boolean;
  contactTick: Record<number, number>;
}

export function createFeelTracker(carId: number): FeelTracker {
  return {
    carId, ticks: 0, prevPosition: 0, overtakes: 0, battleTicks: 0, aloneTicks: 0, flatTicks: 0, nitroOnTicks: 0,
    scrapes: 0, rearHits: 0, fuelEmpty: false, contactTick: {},
  };
}

/** Chamar logo depois de cada `stepRace`. Só lê `state`. */
export function observeFeel(tr: FeelTracker, state: RaceState): void {
  const car = state.cars[tr.carId];
  if (!car || state.phase !== 'racing' || car.finished) return;
  tr.ticks++;

  // Ultrapassagem: cada posição ganha conta uma; perder posição não desconta.
  if (tr.prevPosition > 0 && car.position < tr.prevPosition) tr.overtakes += tr.prevPosition - car.position;
  tr.prevPosition = car.position;

  // Disputa ou sozinho: o menor intervalo de progresso aos vizinhos de POSIÇÃO ±1 que ainda correm.
  let gap = Infinity;
  for (const o of state.cars) {
    if (o === car || o.finished) continue;
    if (o.position !== car.position - 1 && o.position !== car.position + 1) continue;
    gap = Math.min(gap, Math.abs(o.progress - car.progress));
  }
  if (gap <= BATTLE_GAP) tr.battleTicks++;
  else if (gap > ALONE_GAP) tr.aloneTicks++;

  if (car.speed >= FLAT_OUT_FRACTION * carStats(car).topSpeed) tr.flatTicks++;
  if (car.nitroTicks > 0) tr.nitroOnTicks++;

  for (const e of state.events) {
    if (e.type === 'collision' && (e.carId === car.id || e.otherId === car.id)) tr.rearHits++;
    else if (e.type === 'fuel_empty' && e.carId === car.id) tr.fuelEmpty = true;
  }

  // Raspão: caixas encostadas SEM o evento de batida traseira; um contato que continua (ou outro com o mesmo
  // carro dentro do cooldown) é o mesmo raspão.
  for (const o of state.cars) {
    if (o === car) continue;
    const ev = state.events.some((e) => e.type === 'collision'
      && ((e.carId === car.id && e.otherId === o.id) || (e.carId === o.id && e.otherId === car.id)));
    const touch = Math.abs(wrappedDelta(car.z, o.z, state.trackLength)) < CAR_LENGTH + CONTACT_LONGITUDINAL
      && Math.abs(car.x - o.x) < CAR_HALF_WIDTH * 2 + CONTACT_LATERAL;
    if (!ev && !touch) continue;
    const last = tr.contactTick[o.id];
    if (!ev && (last === undefined || state.tick - last > COLLISION_COOLDOWN_TICKS)) tr.scrapes++;
    tr.contactTick[o.id] = state.tick;
  }
}

const r2 = (n: number): number => Math.round(n * 100) / 100;

export function feelOf(tr: FeelTracker): RaceFeel {
  const s = tr.ticks / TICK_RATE;
  return {
    seconds: r2(s),
    overtakes: tr.overtakes,
    overtakesPerMin: r2(s > 0 ? tr.overtakes / (s / 60) : 0),
    battleSeconds: r2(tr.battleTicks / TICK_RATE),
    aloneSeconds: r2(tr.aloneTicks / TICK_RATE),
    flatOutPct: r2(tr.ticks > 0 ? (100 * tr.flatTicks) / tr.ticks : 0),
    nitroPct: r2(tr.ticks > 0 ? (100 * tr.nitroOnTicks) / tr.ticks : 0),
    scrapes: tr.scrapes,
    rearHits: tr.rearHits,
  };
}

/** Média campo a campo (cada campo em 2 casas); lista vazia dá null. */
export function meanFeel(list: readonly RaceFeel[]): RaceFeel | null {
  if (list.length === 0) return null;
  const avg = (pick: (f: RaceFeel) => number): number => r2(list.reduce((a, f) => a + pick(f), 0) / list.length);
  return {
    seconds: avg((f) => f.seconds),
    overtakes: avg((f) => f.overtakes),
    overtakesPerMin: avg((f) => f.overtakesPerMin),
    battleSeconds: avg((f) => f.battleSeconds),
    aloneSeconds: avg((f) => f.aloneSeconds),
    flatOutPct: avg((f) => f.flatOutPct),
    nitroPct: avg((f) => f.nitroPct),
    scrapes: avg((f) => f.scrapes),
    rearHits: avg((f) => f.rearHits),
  };
}
