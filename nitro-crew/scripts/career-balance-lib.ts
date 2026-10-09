// Sonda de balanceamento da carreira (usada por tests/career-balance.test.ts e scripts/career-balance.ts).
// O "piloto médio" da economia é um carro humano guiado pelo cérebro da IA com habilidade fixa
// (PROXY_SKILL): o que chega por volta de 4º na primeira copa, de fábrica, contra a IA nível 0 — o mesmo
// piloto que a calibragem de dinheiro supõe (sempre 4º). Corridas inteiras: 20 carros, profissional,
// assistências padrão, todas as voltas da pista.
import { AVERAGE_POSITION, CAREER_START_MONEY, NO_UPGRADES, prizeFor, prizeMultiplier, UPGRADE_PARTS, upgradePrice } from '../src/core/career';
import { CARS } from '../src/core/data/cars';
import { CUPS } from '../src/core/data/cups';
import { TICK_RATE } from '../src/core/constants';
import { createRace, stepRace } from '../src/core/sim/race';
import { getTrack } from '../src/core/track';
import type { CupDef, Difficulty, RaceConfig, UpgradeLevels, UpgradePart } from '../src/core/types';

/** Habilidade do piloto-proxy: ~4º na primeira copa (profissional, 20 carros). Medida em docs/CARREIRA.md. */
export const PROXY_SKILL = 0.97;
export { AVERAGE_POSITION };
const ALL_ASSISTS = { sharedNitro: true, tow: true, teamDraft: true, catchup: true };
const MAX_TICKS = TICK_RATE * 60 * 20;

/** Posição final do piloto-proxy numa corrida inteira. */
export function proxyRace(trackId: string, aiLevel: number, upgrades: UpgradeLevels, seed: number, skill = PROXY_SKILL, difficulty: Difficulty = 'profissional', aiPace = 0): number {
  const track = getTrack(trackId);
  const config: RaceConfig = {
    trackId, laps: track.def.laps, humans: [{ seat: 0, name: 'P1', carId: 'falcao', teamId: 0, color: '#fff', upgrades }],
    totalCars: 20, difficulty, manualGear: false, assists: ALL_ASSISTS, seed, rosterSeed: seed * 7 + 1, aiLevel, aiPace,
  };
  const state = createRace(config, track);
  const me = state.cars.find((c) => c.seat === 0);
  if (!me) throw new Error('sem o carro humano');
  me.ai = { skill, laneX: 0.3, laneUntil: 0, lookahead: 32, aggression: 0.5 };
  for (let i = 0; i < MAX_TICKS && state.phase !== 'finished'; i++) stepRace(state, track, []);
  return state.results?.find((r) => r.seat === 0)?.position ?? config.totalCars;
}

/** Posições do piloto-proxy em cada pista, para cada semente. */
export function proxyPositions(trackIds: readonly string[], aiLevel: number, upgrades: UpgradeLevels, seeds: readonly number[], skill = PROXY_SKILL, difficulty: Difficulty = 'profissional', aiPace = 0): number[] {
  const out: number[] = [];
  for (const seed of seeds) for (const t of trackIds) out.push(proxyRace(t, aiLevel, upgrades, seed, skill, difficulty, aiPace));
  return out;
}

// ───────────────────────────── Sonda 2.0: perfis de compra ─────────────────────────────
// Economia sem corrida: posição fixa por perfil; depois de cada corrida o perfil compra enquanto quiser e puder.
// Em O2 os perfis ganham habilidade de derrapagem e nitro para a parte com corrida (não criar esses campos agora).
export interface ProbeItem { kind: 'car' | 'upgrade'; carId: string; part?: UpgradePart; price: number }
export interface ProbeGarage { money: number; carId: string; owned: string[]; upgrades: Record<string, UpgradeLevels> }
export type ProbeProfileId = 'focado' | 'colecionador' | 'vaidoso' | 'sempre1' | 'sempre8';
/** `next`: a compra que o perfil quer agora (se custar mais que o saldo, ele guarda), ou null. */
export interface ProbeProfile { id: ProbeProfileId; position: number; next(g: ProbeGarage): ProbeItem | null }

const owns = (g: ProbeGarage, carId: string) => CARS.some((c) => c.id === carId && c.price === 0) || g.owned.includes(carId);
const levels = (g: ProbeGarage, carId: string): UpgradeLevels => ({ ...NO_UPGRADES, ...g.upgrades[carId] });
function upgradesOf(g: ProbeGarage, carId: string): ProbeItem[] {
  const lv = levels(g, carId); const out: ProbeItem[] = [];
  for (const part of UPGRADE_PARTS) { const price = upgradePrice(part, lv[part], carId); if (price !== null) out.push({ kind: 'upgrade', carId, part, price }); }
  return out;
}
const carsForSale = (g: ProbeGarage): ProbeItem[] => CARS.filter((c) => c.price > 0 && !owns(g, c.id)).map((c) => ({ kind: 'car', carId: c.id, price: c.price }));
const allUpgrades = (g: ProbeGarage): ProbeItem[] => CARS.filter((c) => owns(g, c.id)).flatMap((c) => upgradesOf(g, c.id));
/** O mais barato; no empate, o primeiro (ordem de CARS e de UPGRADE_PARTS — a mesma de averagePlayerUpgrades). */
function cheapest(items: readonly ProbeItem[]): ProbeItem | null {
  let best: ProbeItem | null = null;
  for (const it of items) if (!best || it.price < best.price) best = it;
  return best;
}
const TOP_CAR = CARS.reduce((a, b) => (b.price > a.price ? b : a));

export const PROBE_PROFILES: readonly ProbeProfile[] = [
  // Só melhora o carro que pilota, a peça mais barata primeiro: o piloto médio do núcleo (averagePlayerUpgrades).
  { id: 'focado', position: AVERAGE_POSITION, next: (g) => cheapest(upgradesOf(g, g.carId)) },
  // Guarda para o próximo carro à venda (o mais barato que falta); com todos, melhora o que for mais barato.
  { id: 'colecionador', position: AVERAGE_POSITION, next: (g) => cheapest(carsForSale(g)) ?? cheapest(allUpgrades(g)) },
  // Quer o carro mais caro: guarda até ele, depois só o melhora (sem Oficina ainda; a P1 dá a ele os cosméticos).
  { id: 'vaidoso', position: AVERAGE_POSITION, next: (g) => (owns(g, TOP_CAR.id) ? cheapest(upgradesOf(g, TOP_CAR.id)) : { kind: 'car', carId: TOP_CAR.id, price: TOP_CAR.price }) },
  // Completistas: o item mais barato do catálogo inteiro (carros à venda e melhorias de qualquer carro seu).
  { id: 'sempre1', position: 1, next: (g) => cheapest([...carsForSale(g), ...allUpgrades(g)]) },
  { id: 'sempre8', position: 8, next: (g) => cheapest([...carsForSale(g), ...allUpgrades(g)]) },
];

export interface ProbeCupRow { cupId: string; races: number; prize: number; startLevels: UpgradeLevels; spent: number; money: number; racesWithoutPurchase: number }
export interface ProbeRun { profile: ProbeProfileId; rows: ProbeCupRow[]; longestWithoutPurchase: number; catalogDoneAtRace: number | null; totalPrize: number; totalSpent: number }

function buy(g: ProbeGarage, it: ProbeItem): void {
  g.money -= it.price;
  if (it.kind === 'car') { g.owned.push(it.carId); g.carId = it.carId; return; }
  const lv = levels(g, it.carId); if (it.part) lv[it.part]++; g.upgrades[it.carId] = lv;
}
const catalogDone = (g: ProbeGarage) => carsForSale(g).length === 0 && allUpgrades(g).length === 0;

/** A carreira inteira de um perfil: por copa, o saldo no fim (dinheiro parado) e as corridas sem compra. */
export function simulateProfile(profile: ProbeProfile, cups: readonly CupDef[] = CUPS): ProbeRun {
  const g: ProbeGarage = { money: CAREER_START_MONEY, carId: 'falcao', owned: [], upgrades: {} };
  let race = 0; let streak = 0; let longest = 0; let doneAt: number | null = null; let totalPrize = 0;
  const rows = cups.map((cup, i): ProbeCupRow => {
    const mult = prizeMultiplier(i, cups.length);
    const startLevels = levels(g, g.carId);
    let spent = 0; let without = 0;
    for (let r = 0; r < cup.trackIds.length; r++) {
      race++;
      const prize = prizeFor(profile.position, mult); g.money += prize; totalPrize += prize;
      let bought = false;
      for (;;) { const it = profile.next(g); if (!it || it.price > g.money) break; buy(g, it); spent += it.price; bought = true; }
      if (bought) streak = 0; else { without++; streak++; if (streak > longest) longest = streak; }
      if (doneAt === null && catalogDone(g)) doneAt = race;
    }
    return { cupId: cup.id, races: cup.trackIds.length, prize: cup.trackIds.length * prizeFor(profile.position, mult), startLevels, spent, money: g.money, racesWithoutPurchase: without };
  });
  return { profile: profile.id, rows, longestWithoutPurchase: longest, catalogDoneAtRace: doneAt, totalPrize, totalSpent: rows.reduce((a, r) => a + r.spent, 0) };
}

// As melhorias do piloto médio moram no núcleo (o nível da IA da carreira as segue): reexportadas aqui para a sonda.
export { averagePlayerUpgrades } from '../src/core/career';
