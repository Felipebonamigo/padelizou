// Modo Carreira (passo 3.3) e campeonato salvo (1.7a): economia, compras, atributos efetivos
// das melhorias no núcleo, IA que evolui, save e continuar a copa. Tudo puro, sem DOM.
import fs from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';
import {
  buyCar, buyUpgrade, CAREER_AI_LEVEL_MAX, CAREER_START_MONEY, careerAiLevel, careerHumans, levelsOf, newCareer,
  beginCareerCup, ownsCar, prizeFor, prizeMultiplier, PRIZE_CUP_GROWTH, racePrizes, selectCar, settleCareerRace,
  teamBonusFor, upgradePrice, walletOf, type CareerState,
} from '../src/core/career';
import { applyRaceResult, createChampionship, nextTrackId } from '../src/core/championship';
import {
  HANDLING_MAX, NITRO_PER_RACE, POINTS_TABLE, TICK_RATE, UPGRADE_BRAKES, UPGRADE_ENGINE_TOP, UPGRADE_MAX_LEVEL,
  UPGRADE_NITRO_CHARGES, UPGRADE_TANK_FUEL, UPGRADE_TIRES_HANDLING, UPGRADE_TURBO_ACCEL,
} from '../src/core/constants';
import { AI_CAR_POOL, CARS, carDef } from '../src/core/data/cars';
import { CUPS } from '../src/core/data/cups';
import { AI_TEAM_ID_BASE } from '../src/core/data/drivers';
import { deserializeRace, hashRace, serializeRace } from '../src/core/serialize';
import { createRace } from '../src/core/sim/race';
import { aiStats, carStats, effectiveStats } from '../src/core/sim/stats';
import { getTrack } from '../src/core/track';
import type { CarStats, HumanEntry, RaceConfig, RaceResultRow, UpgradeLevels, UpgradePart } from '../src/core/types';
import { NEUTRAL_INPUT } from '../src/core/types';
import { DEFAULT_SAVE } from '../src/game/contracts';
import { clearCupProgress, saveCupProgress, unlockCar } from '../src/game/career-save';
import { compactHumans } from '../src/game/career-session';
import { recordRaceResults, sanitizeSave } from '../src/game/save';
import { ALL_ASSISTS, NO_ASSISTS, human, humanCar, idle, run, syntheticTrack } from './helpers';

const PARTS: UpgradePart[] = ['engine', 'turbo', 'tires', 'brakes', 'tank', 'nitro'];
const ZERO: UpgradeLevels = { engine: 0, turbo: 0, tires: 0, brakes: 0, tank: 0, nitro: 0 };
const lv = (over: Partial<UpgradeLevels>): UpgradeLevels => ({ ...ZERO, ...over });

/** Resultado de corrida com os humanos nas posições dadas e a IA preenchendo o resto, em duplas. */
function fakeResults(humanPositions: Record<number, number>, humans: HumanEntry[], total = 20): RaceResultRow[] {
  const rows: RaceResultRow[] = [];
  const taken = Object.values(humanPositions);
  for (const h of humans) {
    const p = humanPositions[h.seat];
    rows.push({ carId: 100 + h.seat, seat: h.seat, name: h.name, teamId: h.teamId, carDefId: h.carId, position: p, finished: true, totalTicks: 1000, bestLapTicks: 100, points: POINTS_TABLE[p - 1] ?? 0 });
  }
  let ai = 0;
  for (let p = 1; p <= total; p++) {
    if (taken.includes(p)) continue;
    rows.push({ carId: ai, seat: -1, name: `IA${ai}`, teamId: AI_TEAM_ID_BASE + Math.floor(ai / 2), carDefId: 'falcao', position: p, finished: true, totalTicks: 1000, bestLapTicks: 100, points: POINTS_TABLE[p - 1] ?? 0 });
    ai++;
  }
  return rows.sort((a, b) => a.position - b.position);
}

function coopCareer(n = 2): CareerState {
  return newCareer(Array.from({ length: n }, (_, i) => human(i, 0)));
}
function versusCareer(n = 2): CareerState {
  return newCareer(Array.from({ length: n }, (_, i) => human(i, i)));
}

/** Corre uma corrida da carreira com os humanos nas posições dadas. */
function race(career: CareerState, positions: Record<number, number>) {
  if (!career.champ) beginCareerCup(career, 1234);
  return settleCareerRace(career, fakeResults(positions, careerHumans(career)));
}

// ───────────────────────────── Carros ─────────────────────────────

describe('carros', () => {
  it('são 8, com nomes próprios, os 4 originais grátis e os 4 novos à venda', () => {
    expect(CARS.length).toBe(8);
    expect(new Set(CARS.map((c) => c.id)).size).toBe(8);
    expect(CARS.filter((c) => c.price === 0).map((c) => c.id)).toEqual(['falcao', 'trovao', 'tornado', 'camelo']);
    for (const c of CARS.filter((x) => x.price > 0)) {
      expect(c.price).toBeGreaterThan(10_000);
      expect(c.name.length).toBeGreaterThan(3);
      expect(c.blurb.length).toBeGreaterThan(10);
    }
  });

  it('cada carro novo é o melhor (ou empatado) em algum atributo e perde para um original em outro', () => {
    const originals = CARS.filter((c) => c.price === 0);
    const keys = ['topSpeed', 'accel', 'handling', 'economy'] as const;
    const value = (c: (typeof CARS)[number], k: (typeof keys)[number]) => (k === 'economy' ? 1 / c.fuelPerUnit : c[k]);
    for (const c of CARS.filter((x) => x.price > 0)) {
      const wins = keys.filter((k) => originals.every((o) => value(c, k) >= value(o, k)));
      const loses = keys.filter((k) => originals.some((o) => value(c, k) < value(o, k)));
      expect(wins.length, `${c.id} não se destaca`).toBeGreaterThan(0);
      expect(loses.length, `${c.id} não troca nada por nada`).toBeGreaterThan(0);
    }
  });

  it('a IA só usa os carros originais (o elenco das corridas não muda com os carros novos)', () => {
    expect(AI_CAR_POOL.map((c) => c.id)).toEqual(['falcao', 'trovao', 'tornado', 'camelo']);
    const config: RaceConfig = { trackId: 'copacabana', laps: 2, humans: [human(0)], totalCars: 20, difficulty: 'profissional', manualGear: false, assists: NO_ASSISTS, seed: 5 };
    const state = createRace(config, getTrack('copacabana'));
    for (const c of state.cars) if (c.seat < 0) expect(carDef(c.carId).price).toBe(0);
  });
});

// ───────────────────────────── Atributos efetivos ─────────────────────────────

describe('atributos efetivos (núcleo)', () => {
  const def = carDef('falcao');
  const base = effectiveStats(def, null);
  const changed = (a: CarStats, b: CarStats) => (Object.keys(a) as Array<keyof CarStats>).filter((k) => a[k] !== b[k]);

  it('sem melhoria, os atributos são os do carro', () => {
    expect(base).toEqual({ topSpeed: def.topSpeed, accel: def.accel, brake: def.brake, handling: def.handling, fuelPerUnit: def.fuelPerUnit, nitro: NITRO_PER_RACE });
  });

  it('cada melhoria muda o atributo certo, e só ele', () => {
    const expected: Record<UpgradePart, keyof CarStats> = { engine: 'topSpeed', turbo: 'accel', tires: 'handling', brakes: 'brake', tank: 'fuelPerUnit', nitro: 'nitro' };
    for (const part of PARTS) {
      const s = effectiveStats(def, lv({ [part]: 1 }));
      expect(changed(base, s), part).toEqual([expected[part]]);
    }
    const s3 = effectiveStats(def, lv({ engine: 3, turbo: 3, tires: 3, brakes: 3, tank: 3, nitro: 3 }));
    expect(s3.topSpeed).toBeCloseTo(def.topSpeed * (1 + 3 * UPGRADE_ENGINE_TOP), 6);
    expect(s3.accel).toBeCloseTo(def.accel * (1 + 3 * UPGRADE_TURBO_ACCEL), 6);
    expect(s3.brake).toBeCloseTo(def.brake * (1 + 3 * UPGRADE_BRAKES), 6);
    expect(s3.handling).toBeCloseTo(Math.min(HANDLING_MAX, def.handling + 3 * UPGRADE_TIRES_HANDLING), 6);
    expect(s3.fuelPerUnit).toBeCloseTo(def.fuelPerUnit * (1 - 3 * UPGRADE_TANK_FUEL), 12);
    expect(s3.nitro).toBe(NITRO_PER_RACE + 3 * UPGRADE_NITRO_CHARGES);
    // Melhorar sempre ajuda: mais rápido, mais aceleração, mais freio, mais aderência, menos consumo.
    expect(s3.topSpeed).toBeGreaterThan(base.topSpeed);
    expect(s3.fuelPerUnit).toBeLessThan(base.fuelPerUnit);
  });

  it('limites: nível acima do máximo vale o máximo, negativo ou lixo vale zero, dirigibilidade tem teto', () => {
    expect(effectiveStats(def, lv({ engine: 9 }))).toEqual(effectiveStats(def, lv({ engine: UPGRADE_MAX_LEVEL })));
    expect(effectiveStats(def, lv({ turbo: -2 }))).toEqual(base);
    expect(effectiveStats(def, lv({ brakes: Number.NaN }))).toEqual(base);
    const agile = CARS.reduce((a, b) => (b.handling > a.handling ? b : a));
    expect(effectiveStats(agile, lv({ tires: 3 })).handling).toBeLessThanOrEqual(HANDLING_MAX);
    expect(Number.isInteger(effectiveStats(def, lv({ nitro: 1.7 })).nitro)).toBe(true);
  });

  it('a IA melhora por nível (motor, turbo, pneus, freios), fracionário, sem mexer em tanque e nitro', () => {
    const a0 = aiStats(def, 0); const a15 = aiStats(def, 1.5); const a3 = aiStats(def, 3);
    expect(a0).toEqual(base);
    expect(a15.topSpeed).toBeGreaterThan(a0.topSpeed); expect(a3.topSpeed).toBeGreaterThan(a15.topSpeed);
    expect(a3.accel).toBeGreaterThan(a0.accel); expect(a3.brake).toBeGreaterThan(a0.brake);
    expect(a3.fuelPerUnit).toBe(a0.fuelPerUnit); expect(a3.nitro).toBe(a0.nitro);
  });

  it('createRace guarda os atributos de cada carro no estado (JSON) e o nitro extra entra nas cargas e no cofre', () => {
    const up = lv({ engine: 2, nitro: 2 });
    const humans = [{ ...human(0), upgrades: up }, human(1)];
    const track = syntheticTrack();
    const mk = (assists: typeof ALL_ASSISTS) => createRace({ trackId: track.def.id, laps: 2, humans, totalCars: 4, difficulty: 'profissional', manualGear: false, assists, seed: 3, aiLevel: 2 }, track);
    const state = mk(NO_ASSISTS);
    const p1 = humanCar(state, 0); const p2 = humanCar(state, 1);
    expect(carStats(p1)).toEqual(effectiveStats(carDef(p1.carId), up));
    expect(carStats(p2)).toEqual(effectiveStats(carDef(p2.carId), null));
    expect(p1.nitroLeft).toBe(NITRO_PER_RACE + 2);
    expect(p2.nitroLeft).toBe(NITRO_PER_RACE);
    for (const c of state.cars.filter((x) => x.seat < 0)) expect(carStats(c)).toEqual(aiStats(carDef(c.carId), 2));
    expect(JSON.parse(serializeRace(state)).cars[0].stats).toEqual(state.cars[0].stats);
    expect(mk(ALL_ASSISTS).teamNitro[0]).toBe(NITRO_PER_RACE * 2 + 2);
  });

  it('estado antigo sem atributos ganha o padrão ao desserializar', () => {
    const track = syntheticTrack();
    const state = createRace({ trackId: track.def.id, laps: 2, humans: [{ ...human(0), upgrades: lv({ turbo: 3 }) }], totalCars: 3, difficulty: 'amador', manualGear: false, assists: NO_ASSISTS, seed: 9, aiLevel: 1 }, track);
    const raw = JSON.parse(serializeRace(state)) as { cars: Array<Record<string, unknown>> };
    for (const c of raw.cars) delete c.stats;
    const back = deserializeRace(JSON.stringify(raw));
    expect(back.cars.map((c) => c.stats)).toEqual(state.cars.map((c) => c.stats));
  });

  it('na pista, o motor sobe a velocidade final e o tanque gasta menos combustível', () => {
    const track = syntheticTrack([{ op: 'straight', length: 900 }]);
    const drive = (upgrades: UpgradeLevels) => {
      const s = createRace({ trackId: track.def.id, laps: 3, humans: [{ ...human(0), upgrades }], totalCars: 1, difficulty: 'profissional', manualGear: false, assists: NO_ASSISTS, seed: 1 }, track);
      run(s, track, TICK_RATE * 20);
      return humanCar(s, 0);
    };
    const plain = drive(ZERO); const engine = drive(lv({ engine: 3 })); const tank = drive(lv({ tank: 3 }));
    expect(engine.speed).toBeGreaterThan(plain.speed * 1.05);
    expect(engine.speed).toBeCloseTo(carDef('falcao').topSpeed * (1 + 3 * UPGRADE_ENGINE_TOP), 0);
    expect(1 - tank.fuel).toBeLessThan((1 - plain.fuel) * 0.8);
  });
});

// ───────────────────────────── Determinismo e IA ─────────────────────────────

describe('determinismo e IA da carreira', () => {
  const config = (aiLevel: number, upgrades?: UpgradeLevels): RaceConfig => ({
    trackId: 'rota_66', laps: 3, humans: [{ ...human(0, 0, 'trovao'), upgrades }, human(1, 0, 'tornado')], totalCars: 12,
    difficulty: 'profissional', manualGear: false, assists: ALL_ASSISTS, seed: 77, rosterSeed: 5, aiLevel,
  });
  const input = (s: { tick: number }, seat: number) => ({ ...NEUTRAL_INPUT, steer: (s.tick + seat * 40) % 240 < 120 ? 0.3 : -0.3, throttle: true, nitro: s.tick % 700 === 300 + seat });

  it('com melhorias e IA evoluída, duas corridas com a mesma semente são idênticas', () => {
    const track = getTrack('rota_66');
    const a = createRace(config(1.5, lv({ engine: 3, turbo: 2, nitro: 1 })), track);
    const b = createRace(config(1.5, lv({ engine: 3, turbo: 2, nitro: 1 })), track);
    run(a, track, 2500, input); run(b, track, 2500, input);
    expect(serializeRace(a)).toBe(serializeRace(b));
    expect(hashRace(a)).toBe(hashRace(b));
    const c = createRace(config(1.5), track);
    run(c, track, 2500, input);
    expect(hashRace(c)).not.toBe(hashRace(a));
  }, 30_000);

  it('o elenco (nomes e carros) não depende do nível da IA', () => {
    const track = getTrack('rota_66');
    const names = (lvl: number) => createRace(config(lvl), track).cars.map((c) => `${c.name}/${c.carId}`);
    expect(names(3)).toEqual(names(0));
  });

  it('IA melhorada anda mais: nível 3 abre vantagem sobre nível 0 na mesma corrida', () => {
    const track = getTrack('rota_66');
    const best = (lvl: number) => {
      const s = createRace({ ...config(lvl), humans: [human(0)], totalCars: 8, assists: NO_ASSISTS }, track);
      run(s, track, TICK_RATE * 40, idle);
      return Math.max(...s.cars.filter((c) => c.seat < 0).map((c) => c.progress));
    };
    expect(best(3)).toBeGreaterThan(best(0) * 1.03);
  });

  it('o nível da IA cresce com a copa da carreira, de 0 na primeira ao máximo na última, qualquer que seja o número de copas', () => {
    const career = coopCareer();
    expect(careerAiLevel(career)).toBe(0);
    career.cupId = CUPS[CUPS.length - 1].id;
    expect(careerAiLevel(career)).toBe(CAREER_AI_LEVEL_MAX);
    const levels = CUPS.map((c) => careerAiLevel({ ...career, cupId: c.id }));
    for (let i = 1; i < levels.length; i++) expect(levels[i]).toBeGreaterThan(levels[i - 1]);
    const two = CUPS.slice(0, 2);
    expect(careerAiLevel({ ...career, cupId: two[1].id }, two)).toBe(CAREER_AI_LEVEL_MAX);
  });
});

// ───────────────────────────── Economia ─────────────────────────────

describe('prêmio e carteira', () => {
  it('o prêmio cai com a posição, nunca é zero para quem correu, e cresce com a copa', () => {
    for (let p = 1; p < 20; p++) expect(prizeFor(p, 1)).toBeGreaterThanOrEqual(prizeFor(p + 1, 1));
    expect(prizeFor(20, 1)).toBeGreaterThan(0);
    expect(prizeMultiplier(0, 4)).toBe(1);
    expect(prizeMultiplier(3, 4)).toBeCloseTo(1 + PRIZE_CUP_GROWTH, 9);
    expect(prizeMultiplier(7, 8)).toBeCloseTo(1 + PRIZE_CUP_GROWTH, 9);
    expect(prizeMultiplier(0, 1)).toBe(1);
    expect(prizeFor(1, prizeMultiplier(3, 4))).toBeGreaterThan(prizeFor(1, 1));
  });

  it('co-op: carteira única da equipe, com o prêmio de cada um e o bônus pela colocação da equipe', () => {
    const career = coopCareer(2);
    expect(career.wallets).toEqual([CAREER_START_MONEY * 2]);
    const { report } = race(career, { 0: 1, 1: 3 });
    const bonus = teamBonusFor(1, 1);
    expect(report.teamRank).toBe(1);
    expect(report.teamBonus).toBe(bonus);
    expect(bonus).toBeGreaterThan(0);
    expect(career.wallets).toEqual([CAREER_START_MONEY * 2 + prizeFor(1, 1) + prizeFor(3, 1) + bonus]);
    expect(walletOf(career, 0)).toBe(walletOf(career, 1));
    expect(career.drivers[0].earnings).toBe(prizeFor(1, 1));
    expect(career.drivers[1].earnings).toBe(prizeFor(3, 1));
  });

  it('versus: cada assento tem a sua carteira e não há bônus de equipe', () => {
    const career = versusCareer(2);
    expect(career.coop).toBe(false);
    expect(career.wallets).toEqual([CAREER_START_MONEY, CAREER_START_MONEY]);
    const { report } = race(career, { 0: 2, 1: 9 });
    expect(report.teamBonus).toBe(0);
    expect(career.wallets).toEqual([CAREER_START_MONEY + prizeFor(2, 1), CAREER_START_MONEY + prizeFor(9, 1)]);
  });

  it('racePrizes lista um prêmio por humano, em ordem de assento', () => {
    const humans = [human(1, 0), human(0, 0)];
    const out = racePrizes(fakeResults({ 0: 4, 1: 12 }, humans), humans, true, 2);
    expect(out.rows).toEqual([{ driver: 0, position: 4, prize: prizeFor(4, 2) }, { driver: 1, position: 12, prize: prizeFor(12, 2) }]);
  });

  it('eliminação: a corrida que eliminou não paga, o já ganho fica e a copa recomeça da primeira corrida', () => {
    const career = versusCareer(1);
    race(career, { 0: 2 });
    const afterFirst = career.wallets[0];
    const { report, champ } = race(career, { 0: 9 });
    expect(report.verdict).toBe('eliminated');
    expect(report.rows[0].prize).toBe(0);
    expect(career.wallets[0]).toBe(afterFirst);
    expect(champ.eliminated).toBe(true);
    expect(career.champ).toBeNull();
    expect(career.cupId).toBe(CUPS[0].id);
    expect(career.attempts).toBe(2);
    beginCareerCup(career, 99);
    expect(nextTrackId(career.champ!)).toBe(CUPS[0].trackIds[0]);
  });

  it('copa concluída: passa para a seguinte (IA mais forte, prêmio maior); a última encerra a carreira', () => {
    const career = versusCareer(1);
    let last = race(career, { 0: 1 });
    for (let i = 1; i < CUPS[0].trackIds.length; i++) last = race(career, { 0: 1 });
    expect(last.report.cupCompleted).toBe(true);
    expect(last.champ.completed).toBe(true);
    expect(career.cupId).toBe(CUPS[1].id);
    expect(career.champ).toBeNull();
    expect(career.attempts).toBe(1);
    expect(careerAiLevel(career)).toBeGreaterThan(0);
    const before = career.wallets[0];
    race(career, { 0: 1 });
    expect(career.wallets[0] - before).toBe(prizeFor(1, prizeMultiplier(1, CUPS.length)));
    career.cupId = CUPS[CUPS.length - 1].id; career.champ = null;
    for (let i = 0; i < CUPS[CUPS.length - 1].trackIds.length; i++) last = race(career, { 0: 1 });
    expect(last.report.careerCompleted).toBe(true);
    expect(career.completed).toBe(true);
    expect(career.champ).toBeNull();
  });

  it('calibragem: um piloto médio (4º lugar) compra de 1 a 2 itens por corrida até o fim, com 4 ou 8 copas', () => {
    for (const cupCount of [4, 8]) {
      let money = CAREER_START_MONEY; let bought = 0; let races = 0;
      const levels: Record<string, UpgradeLevels> = {};
      let car = 'falcao';
      const cheapest = (): { price: number; buy: () => void } | null => {
        const lvls = (levels[car] ??= { ...ZERO });
        let best: { price: number; buy: () => void } | null = null;
        for (const part of PARTS) {
          const price = upgradePrice(part, lvls[part]);
          if (price !== null && (!best || price < best.price)) best = { price, buy: () => { lvls[part]++; } };
        }
        if (best) return best;
        const next = CARS.filter((c) => c.price > 0 && !levels[c.id]).sort((a, b) => a.price - b.price)[0];
        return next ? { price: next.price, buy: () => { car = next.id; levels[car] = { ...ZERO }; } } : null;
      };
      for (let cup = 0; cup < cupCount; cup++) {
        for (let r = 0; r < 3; r++) {
          money += prizeFor(4, prizeMultiplier(cup, cupCount)); races++;
          for (let item = cheapest(); item && item.price <= money; item = cheapest()) { money -= item.price; item.buy(); bought++; }
        }
      }
      const perRace = bought / races;
      expect(perRace, `${cupCount} copas: ${bought} itens em ${races} corridas`).toBeGreaterThanOrEqual(1);
      expect(perRace, `${cupCount} copas: ${bought} itens em ${races} corridas`).toBeLessThanOrEqual(2);
    }
  });
});

// ───────────────────────────── Garagem ─────────────────────────────

describe('compras na garagem', () => {
  it('os carros originais já são da garagem; comprar um deles de novo é recusado', () => {
    const career = versusCareer(1);
    const g = career.drivers[0].garage;
    for (const c of CARS.filter((x) => x.price === 0)) expect(ownsCar(g, c.id)).toBe(true);
    expect(buyCar(career, 0, 'trovao')).toBe('owned');
    expect(buyCar(career, 0, 'inexistente')).toBe('unknown');
  });

  it('sem saldo, nada muda', () => {
    const career = versusCareer(1);
    const pricey = CARS.filter((c) => c.price > 0).sort((a, b) => b.price - a.price)[0];
    const before = JSON.stringify(career);
    expect(buyCar(career, 0, pricey.id)).toBe('noMoney');
    career.wallets[0] = 0;
    const snap = JSON.stringify(career);
    expect(buyUpgrade(career, 0, 'engine')).toBe('noMoney');
    expect(JSON.stringify(career)).toBe(snap);
    expect(before).not.toBe(snap);
  });

  it('comprar carro desconta o preço, entra na garagem, é selecionado e depois é recusado como já possuído', () => {
    const career = versusCareer(1);
    const car = CARS.filter((c) => c.price > 0)[0];
    career.wallets[0] = car.price + 500;
    expect(buyCar(career, 0, car.id)).toBe('ok');
    expect(career.wallets[0]).toBe(500);
    const g = career.drivers[0].garage;
    expect(ownsCar(g, car.id)).toBe(true);
    expect(g.carId).toBe(car.id);
    expect(buyCar(career, 0, car.id)).toBe('owned');
    expect(careerHumans(career)[0].carId).toBe(car.id);
  });

  it('melhoria: preço sobe por nível, vale só para o carro melhorado e para no nível máximo', () => {
    const career = versusCareer(1);
    career.wallets[0] = 1_000_000;
    const prices: number[] = [];
    for (let i = 0; i < UPGRADE_MAX_LEVEL; i++) {
      const p = upgradePrice('engine', i)!;
      prices.push(p);
      const before = career.wallets[0];
      expect(buyUpgrade(career, 0, 'engine')).toBe('ok');
      expect(before - career.wallets[0]).toBe(p);
    }
    expect(prices[1]).toBeGreaterThan(prices[0]); expect(prices[2]).toBeGreaterThan(prices[1]);
    expect(upgradePrice('engine', UPGRADE_MAX_LEVEL)).toBeNull();
    const snap = career.wallets[0];
    expect(buyUpgrade(career, 0, 'engine')).toBe('maxLevel');
    expect(career.wallets[0]).toBe(snap);
    const g = career.drivers[0].garage;
    expect(levelsOf(g, g.carId).engine).toBe(UPGRADE_MAX_LEVEL);
    expect(selectCar(career, 0, 'tornado')).toBe(true);
    expect(levelsOf(g, 'tornado').engine).toBe(0);
    expect(careerHumans(career)[0].upgrades).toEqual(ZERO);
    expect(selectCar(career, 0, 'falcao')).toBe(true);
    expect(careerHumans(career)[0].upgrades?.engine).toBe(UPGRADE_MAX_LEVEL);
  });

  it('não dá para melhorar nem escolher carro que não é seu', () => {
    const career = versusCareer(1);
    career.wallets[0] = 1_000_000;
    const car = CARS.filter((c) => c.price > 0)[0];
    expect(buyUpgrade(career, 0, 'turbo', car.id)).toBe('notOwned');
    expect(selectCar(career, 0, car.id)).toBe(false);
  });

  it('co-op compra da carteira da equipe; versus, da própria', () => {
    const coop = coopCareer(2);
    buyUpgrade(coop, 1, 'tires');
    expect(coop.wallets).toEqual([CAREER_START_MONEY * 2 - upgradePrice('tires', 0)!]);
    expect(levelsOf(coop.drivers[1].garage, coop.drivers[1].garage.carId).tires).toBe(1);
    expect(levelsOf(coop.drivers[0].garage, coop.drivers[0].garage.carId).tires).toBe(0);
    const vs = versusCareer(2);
    buyUpgrade(vs, 1, 'tires');
    expect(vs.wallets).toEqual([CAREER_START_MONEY, CAREER_START_MONEY - upgradePrice('tires', 0)!]);
  });

  it('os humanos da carreira levam carro, time, assento contíguo e melhorias para a corrida', () => {
    const career = newCareer([human(2, 0, 'camelo'), human(0, 0, 'trovao')]);
    const hs = careerHumans(career);
    expect(hs.map((h) => [h.seat, h.teamId, h.carId])).toEqual([[0, 0, 'trovao'], [1, 0, 'camelo']]);
    const vs = careerHumans(versusCareer(3));
    expect(vs.map((h) => h.teamId)).toEqual([0, 1, 2]);
  });
});

// ───────────────────────────── Save ─────────────────────────────

describe('save da carreira e da copa em andamento', () => {
  it('carreira vai e volta pelo save sem perder nada', () => {
    const career = coopCareer(2);
    race(career, { 0: 1, 1: 2 });
    career.wallets[0] = 100_000;
    buyCar(career, 1, 'furacao'); buyUpgrade(career, 1, 'nitro'); buyUpgrade(career, 0, 'engine');
    const save = sanitizeSave({});
    save.career = career;
    unlockCar(save, 'furacao');
    const back = sanitizeSave(JSON.parse(JSON.stringify(save)));
    expect(back.career).toEqual(career);
    expect(back.carsUnlocked).toEqual(['furacao']);
  });

  it('lixo vira ausente e nunca lança', () => {
    for (const junk of [5, 'x', [], { drivers: 3 }, { drivers: [], wallets: [1] }, { version: 1, drivers: [{ name: 'a' }], wallets: 'muito' }]) {
      expect(() => sanitizeSave({ career: junk, cupInProgress: junk, carsUnlocked: junk })).not.toThrow();
      const s = sanitizeSave({ career: junk, cupInProgress: junk, carsUnlocked: junk });
      expect(s.cupInProgress).toBeNull();
      expect(s.carsUnlocked).toEqual([]);
    }
    expect(sanitizeSave({ career: 5 }).career).toBeNull();
    expect(sanitizeSave({}).career).toBe(DEFAULT_SAVE.career);
  });

  it('campos ruins da carreira são consertados: níveis no limite, carros desconhecidos fora, carteiras coerentes', () => {
    const career = versusCareer(2);
    const raw = JSON.parse(JSON.stringify(career)) as Record<string, unknown> & { drivers: Array<{ garage: Record<string, unknown> }> };
    raw.drivers[0].garage = { carId: 'delorean', owned: ['furacao', 'delorean', 7], upgrades: { falcao: { engine: 7, turbo: -1, tires: 'x' }, delorean: { engine: 1 } } };
    raw.wallets = [-50, 1e40, 3];
    raw.cupId = 'lua';
    raw.carsUnlocked = ['furacao', 'falcao', 'x'];
    const s = sanitizeSave({ career: raw, carsUnlocked: ['furacao', 'falcao', 'x', 'furacao'] });
    const c = s.career!;
    expect(c).not.toBeNull();
    const g = c.drivers[0].garage;
    expect(g.owned).toEqual(['furacao']);
    expect(g.carId).toBe('falcao');
    expect(g.upgrades).toEqual({ falcao: lv({ engine: UPGRADE_MAX_LEVEL }) });
    expect(c.wallets.length).toBe(2);
    expect(c.wallets[0]).toBe(0);
    expect(Number.isSafeInteger(c.wallets[1])).toBe(true);
    expect(c.cupId).toBe(CUPS[0].id);
    expect(c.champ).toBeNull();
    expect(s.carsUnlocked).toEqual(['furacao']);
  });

  it('campeonato normal salvo a cada corrida: ida e volta, e continuar segue da próxima pista', () => {
    const humans = [human(0), human(1)];
    const champ = createChampionship('brasil', humans);
    applyRaceResult(champ, fakeResults({ 0: 2, 1: 4 }, humans), humans);
    const save = sanitizeSave({});
    saveCupProgress(save, champ, 4242, humans);
    const back = sanitizeSave(JSON.parse(JSON.stringify(save)));
    expect(back.cupInProgress).not.toBeNull();
    const cup = back.cupInProgress!;
    expect(cup.cupSeed).toBe(4242);
    expect(cup.humans).toEqual(humans);
    expect(cup.champ.standings).toEqual(champ.standings);
    expect(nextTrackId(cup.champ)).toBe(CUPS[0].trackIds[1]);
    applyRaceResult(cup.champ, fakeResults({ 0: 1, 1: 3 }, humans), humans);
    expect(cup.champ.standings.find((r) => r.seat === 0)!.positions.slice(0, 2)).toEqual([2, 1]);
  });

  it('copa encerrada (concluída ou eliminado) some do save; copa salva inválida vira ausente', () => {
    const humans = [human(0)];
    const save = sanitizeSave({});
    const champ = createChampionship('brasil', humans);
    saveCupProgress(save, champ, 1, humans);
    expect(save.cupInProgress).not.toBeNull();
    champ.eliminated = true;
    saveCupProgress(save, champ, 1, humans);
    expect(save.cupInProgress).toBeNull();
    saveCupProgress(save, createChampionship('brasil', humans), 1, humans);
    clearCupProgress(save);
    expect(save.cupInProgress).toBeNull();
    const bad = [{ champ: { cupId: 'lua' }, cupSeed: 1, humans }, { champ, cupSeed: 1, humans: [] }, { champ: createChampionship('brasil', humans), cupSeed: 'x', humans: [{ seat: 9 }] }];
    for (const b of bad) expect(sanitizeSave({ cupInProgress: b }).cupInProgress).toBeNull();
  });

  it('assentos com buraco (P1 e P3) viram contíguos antes de salvar, levando o dispositivo junto', () => {
    const bound: Array<string | null> = ['kb1', null, 'gp0', null];
    const input = {
      seatDevice: (s: number) => bound[s] ?? null,
      bindSeat: (s: number, d: string) => { bound[s] = d; },
      unbindSeat: (s: number) => { bound[s] = null; },
    };
    const out = compactHumans(input, [human(0), { ...human(2), name: 'Bia' }]);
    expect(out.map((h) => [h.seat, h.name])).toEqual([[0, 'P1'], [1, 'Bia']]);
    expect(bound).toEqual(['kb1', 'gp0', null, null]);
  });
});

// ───────────────────────────── Fora do núcleo ─────────────────────────────

function walk(dir: string, out: string[] = []): string[] {
  for (const f of fs.readdirSync(dir)) { const p = path.join(dir, f); if (fs.statSync(p).isDirectory()) walk(p, out); else if (p.endsWith('.ts')) out.push(p); }
  return out;
}

describe('a carreira fora do núcleo', () => {
  it('corrida com carro melhorado conta corrida e vitória, mas não entra nos recordes (recorde é de carro de fábrica)', () => {
    const humans = [{ ...human(0), upgrades: lv({ engine: 1 }) }, { ...human(1), upgrades: { ...ZERO } }];
    const results = fakeResults({ 0: 1, 1: 2 }, humans).map((r) => (r.seat === 0 ? { ...r, bestLapTicks: 90, totalTicks: 900 } : r));
    const save = sanitizeSave({});
    const out = recordRaceResults(save, results, humans, 'copacabana', 3);
    expect(save.racesRun).toBe(1);
    expect(save.racesWon).toBe(1);
    expect(save.bestLaps.copacabana?.ticks).toBe(100);
    expect(save.bestLaps.copacabana?.name).toBe('P2');
    expect(out.length).toBeGreaterThan(0);
    expect(out.every((r) => r.seat === 1)).toBe(true);
    // Só humanos melhorados na corrida: nenhum recorde.
    const solo = sanitizeSave({});
    expect(recordRaceResults(solo, results.filter((r) => r.seat !== 1), [humans[0]], 'copacabana', 3)).toEqual([]);
    expect(solo.bestLaps.copacabana).toBeUndefined();
    expect(solo.racesWon).toBe(1);
  });

  it('HUD, áudio e câmera leem a velocidade máxima da corrida (car.stats), não a de fábrica', () => {
    for (const dir of ['src/render', 'src/audio']) {
      for (const f of walk(dir)) {
        const src = fs.readFileSync(f, 'utf8').replace(/\/\/.*$/gm, '').replace(/\/\*[\s\S]*?\*\//g, '');
        const m = src.match(/(?<!stats)\.topSpeed\b/);
        expect(m, `${f} lê a velocidade de fábrica (${m?.[0]}); use car.stats.topSpeed`).toBeNull();
      }
    }
  });
});
