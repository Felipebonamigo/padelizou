// Atributos efetivos de cada carro: o CarDef com as melhorias da carreira (humanos) ou com o
// nível da IA aplicados. Calculados uma vez em createRace e guardados em `car.stats` (JSON puro),
// para a corrida ser reproduzível e serializável; física, IA, colisões e co-op leem por carStats().
import {
  HANDLING_MAX, NITRO_PER_RACE, UPGRADE_BRAKES, UPGRADE_ENGINE_TOP, UPGRADE_MAX_LEVEL, UPGRADE_NITRO_CHARGES,
  UPGRADE_TANK_FUEL, UPGRADE_TIRES_HANDLING, UPGRADE_TURBO_ACCEL,
} from '../constants';
import { carDef } from '../data/cars';
import type { CarDef, CarState, CarStats, RaceConfig, UpgradeLevels, UpgradePart } from '../types';

/** Nível dentro de [0, UPGRADE_MAX_LEVEL]; lixo (NaN, infinito, ausente) vale 0. Fracionário é aceito (IA). */
export function clampLevel(v: number | undefined): number {
  if (typeof v !== 'number' || !Number.isFinite(v)) return 0;
  return Math.min(UPGRADE_MAX_LEVEL, Math.max(0, v));
}

/** Atributos do carro `def` com as melhorias `up` (ausente = de fábrica). */
export function effectiveStats(def: CarDef, up?: Partial<UpgradeLevels> | null): CarStats {
  const lv = (part: keyof UpgradeLevels) => clampLevel(up?.[part]);
  return {
    topSpeed: def.topSpeed * (1 + UPGRADE_ENGINE_TOP * lv('engine')),
    accel: def.accel * (1 + UPGRADE_TURBO_ACCEL * lv('turbo')),
    brake: def.brake * (1 + UPGRADE_BRAKES * lv('brakes')),
    handling: Math.max(def.handling, Math.min(HANDLING_MAX, def.handling + UPGRADE_TIRES_HANDLING * lv('tires'))),
    fuelPerUnit: def.fuelPerUnit * (1 - UPGRADE_TANK_FUEL * lv('tank')),
    nitro: NITRO_PER_RACE + UPGRADE_NITRO_CHARGES * Math.floor(lv('nitro')),
  };
}

/** Atributo que cada peça melhora. */
export const PART_STAT: Readonly<Record<UpgradePart, keyof CarStats>> = {
  engine: 'topSpeed', turbo: 'accel', tires: 'handling', brakes: 'brake', tank: 'fuelPerUnit', nitro: 'nitro',
};

/**
 * Último nível da peça que ainda muda alguma coisa neste carro. É UPGRADE_MAX_LEVEL para quase tudo;
 * os pneus param antes nos carros que batem no teto de dirigibilidade (Tornado RS no 2, Carcará RS
 * no 1). A carreira não vende nível acima disto — seria cobrar por nada.
 */
export function upgradeCap(def: CarDef, part: UpgradePart): number {
  const k = PART_STAT[part];
  let cap = 0;
  for (let level = 1; level <= UPGRADE_MAX_LEVEL; level++) {
    if (effectiveStats(def, { [part]: level })[k] === effectiveStats(def, { [part]: level - 1 })[k]) break;
    cap = level;
  }
  return cap;
}

/** IA da carreira: motor, turbo, pneus e freios no nível `level` (tanque e nitro de fábrica). */
export function aiStats(def: CarDef, level: number): CarStats {
  return effectiveStats(def, { engine: level, turbo: level, tires: level, brakes: level });
}

/** Atributos de um carro da corrida a partir da configuração (humano: suas melhorias; IA: o nível da corrida). */
export function statsFor(config: RaceConfig, car: Pick<CarState, 'seat' | 'carId'>): CarStats {
  const def = carDef(car.carId);
  if (car.seat < 0) return aiStats(def, config.aiLevel ?? 0);
  return effectiveStats(def, config.humans.find((h) => h.seat === car.seat)?.upgrades);
}

/** Atributos de desempenho do carro nesta corrida. Cor e nome continuam no CarDef. */
export function carStats(car: CarState): CarStats {
  return car.stats;
}
