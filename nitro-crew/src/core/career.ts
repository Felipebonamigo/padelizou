// Modo Carreira: regras puras (sem DOM, sem aleatoriedade própria). Dinheiro por corrida, carteira
// da equipe (co-op) ou por assento (versus), garagem com carros e melhorias, avanço de copa em copa
// e eliminação. A sessão (src/game/career-session.ts) orquestra; a garagem (src/ui/screens/garage.ts)
// chama as compras daqui. Decisões e números: docs/CARREIRA.md.
import { applyRaceResult, createChampionship, isCoop, teamRaceRank } from './championship';
import { UPGRADE_MAX_LEVEL } from './constants';
import { CARS } from './data/cars';
import { CUPS } from './data/cups';
import { HUMAN_TEAM_ID, SEAT_COLORS } from './data/drivers';
import type { ChampionshipState, CupDef, HumanEntry, RaceResultRow, UpgradeLevels, UpgradePart } from './types';

export const CAREER_VERSION = 1;
export const UPGRADE_PARTS: readonly UpgradePart[] = ['engine', 'turbo', 'tires', 'brakes', 'tank', 'nitro'];

// ───────────────────────────── Economia ─────────────────────────────

/** Dinheiro inicial de cada piloto (no co-op, a carteira da equipe começa com isto × pilotos). */
export const CAREER_START_MONEY = 2500;
/** Prêmio por posição na corrida (1º…10º), na primeira copa. */
export const PRIZE_BY_POSITION: readonly number[] = [6000, 4500, 3500, 2800, 2200, 1700, 1300, 1000, 800, 600];
/** Do 11º em diante: ajuda de custo. */
export const PRIZE_PARTICIPATION = 400;
/** Co-op: bônus para a carteira da equipe pela colocação da equipe na corrida (1ª, 2ª, 3ª). */
export const TEAM_BONUS_BY_RANK: readonly number[] = [3000, 2000, 1000];
/** Quanto o prêmio cresce da primeira à última copa (a última paga 1 + isto), qualquer que seja o número de copas. */
export const PRIZE_CUP_GROWTH = 0.75;
/** Preço de cada nível de melhoria (1º, 2º, 3º), antes do fator da peça. */
export const UPGRADE_PRICES: readonly number[] = [2000, 3500, 5500];
export const PART_PRICE_FACTOR: Readonly<Record<UpgradePart, number>> = { engine: 1.2, turbo: 1, tires: 1, brakes: 0.8, tank: 0.7, nitro: 1.1 };
/** Nível de melhoria da IA na última copa (a primeira é 0; entre elas, linear). */
export const CAREER_AI_LEVEL_MAX = 3;
/** Teto de dinheiro no save (lixo acima disto é trazido para cá). */
export const MONEY_MAX = 999_999_999;

export const NO_UPGRADES: Readonly<UpgradeLevels> = Object.freeze({ engine: 0, turbo: 0, tires: 0, brakes: 0, tank: 0, nitro: 0 });

// ───────────────────────────── Estado ─────────────────────────────

export interface CareerGarage {
  /** Carro que o piloto leva para a próxima corrida (sempre um carro seu). */
  carId: string;
  /** Carros comprados; os de preço 0 são de todos e não precisam constar. */
  owned: string[];
  /** Níveis de melhoria por carro (só os carros que já receberam alguma). */
  upgrades: Record<string, UpgradeLevels>;
}

export interface CareerDriver {
  name: string;
  garage: CareerGarage;
  /** Total de prêmios ganhos por este piloto (estatística; o bônus de equipe não entra). */
  earnings: number;
}

export interface CareerPrizeRow {
  /** Índice do piloto na carreira (= assento na corrida). */
  driver: number;
  position: number;
  prize: number;
}

/** O que a última corrida rendeu, para a garagem mostrar. */
export interface CareerReport {
  cupId: string;
  trackId: string;
  /** Corrida da copa (0 = primeira). */
  raceIndex: number;
  rows: CareerPrizeRow[];
  teamBonus: number;
  /** Colocação da equipe na corrida (co-op); 0 fora do co-op. */
  teamRank: number;
  verdict: 'qualified' | 'eliminated';
  cupCompleted: boolean;
  careerCompleted: boolean;
}

export interface CareerState {
  version: number;
  /** Dois ou mais humanos no mesmo time: carteira única e bônus de equipe. */
  coop: boolean;
  /** Pilotos humanos, na ordem dos assentos (o índice é o assento na corrida). */
  drivers: CareerDriver[];
  /** Co-op: uma carteira (a da equipe); senão, uma por piloto. */
  wallets: number[];
  /** Copa atual (a próxima a disputar, ou a em andamento). */
  cupId: string;
  /** Copa em andamento; null = a copa atual ainda não começou (ou recomeça depois de uma eliminação). */
  champ: ChampionshipState | null;
  /** Semente do elenco da IA, fixa durante a copa. */
  rosterSeed: number;
  /** Tentativa atual na copa (1 = primeira; sobe a cada eliminação). */
  attempts: number;
  /** A última copa foi concluída. */
  completed: boolean;
  racesRun: number;
  lastReport: CareerReport | null;
}

export type PurchaseResult = 'ok' | 'noMoney' | 'maxLevel' | 'notOwned' | 'owned' | 'unknown';

// ───────────────────────────── Dinheiro ─────────────────────────────

function roundMoney(v: number): number {
  return Math.round(v / 10) * 10;
}

/** Multiplicador do prêmio na copa `cupIndex` de `cupCount`: 1 na primeira, 1 + PRIZE_CUP_GROWTH na última. */
export function prizeMultiplier(cupIndex: number, cupCount: number): number {
  if (cupCount <= 1) return 1;
  const i = Math.min(cupCount - 1, Math.max(0, cupIndex));
  return 1 + PRIZE_CUP_GROWTH * (i / (cupCount - 1));
}

export function prizeFor(position: number, mult: number): number {
  return roundMoney((PRIZE_BY_POSITION[position - 1] ?? PRIZE_PARTICIPATION) * mult);
}

export function teamBonusFor(rank: number, mult: number): number {
  return roundMoney((TEAM_BONUS_BY_RANK[rank - 1] ?? 0) * mult);
}

/** Prêmio de cada humano (em ordem de assento) e bônus da equipe (só co-op). Não sabe de eliminação. */
export function racePrizes(results: RaceResultRow[], humans: HumanEntry[], coop: boolean, mult: number): { rows: CareerPrizeRow[]; teamBonus: number; teamRank: number } {
  const sorted = humans.slice().sort((a, b) => a.seat - b.seat);
  const rows = sorted.map((h, driver) => {
    const r = results.find((x) => x.seat === h.seat);
    return { driver, position: r?.position ?? 0, prize: r ? prizeFor(r.position, mult) : 0 };
  });
  if (!coop || sorted.length === 0) return { rows, teamBonus: 0, teamRank: 0 };
  const teamRank = teamRaceRank(results, sorted[0].teamId);
  return { rows, teamBonus: teamBonusFor(teamRank, mult), teamRank };
}

export function walletIndex(career: CareerState, driver: number): number {
  return career.coop ? 0 : driver;
}

export function walletOf(career: CareerState, driver: number): number {
  return career.wallets[walletIndex(career, driver)] ?? 0;
}

function credit(career: CareerState, driver: number, amount: number): void {
  const i = walletIndex(career, driver);
  career.wallets[i] = Math.min(MONEY_MAX, (career.wallets[i] ?? 0) + amount);
}

// ───────────────────────────── Garagem ─────────────────────────────

export function isFreeCar(carId: string): boolean {
  return CARS.some((c) => c.id === carId && c.price === 0);
}

export function ownsCar(garage: CareerGarage, carId: string): boolean {
  const def = CARS.find((c) => c.id === carId);
  if (!def) return false;
  return def.price === 0 || garage.owned.includes(carId);
}

/** Níveis de melhoria de um carro da garagem (cópia; carro sem melhoria = tudo 0). */
export function levelsOf(garage: CareerGarage, carId: string): UpgradeLevels {
  const u = garage.upgrades[carId];
  const out = { ...NO_UPGRADES };
  if (u) for (const p of UPGRADE_PARTS) out[p] = u[p];
  return out;
}

/** Preço do próximo nível da peça (estando em `level`); null quando já está no máximo. */
export function upgradePrice(part: UpgradePart, level: number): number | null {
  if (level >= UPGRADE_MAX_LEVEL) return null;
  const base = UPGRADE_PRICES[Math.max(0, Math.floor(level))];
  return Math.round((base * PART_PRICE_FACTOR[part]) / 100) * 100;
}

/** Compra o próximo nível da peça para um carro do piloto (padrão: o carro escolhido). */
export function buyUpgrade(career: CareerState, driver: number, part: UpgradePart, carId?: string): PurchaseResult {
  const d = career.drivers[driver];
  if (!d) return 'unknown';
  const id = carId ?? d.garage.carId;
  if (!ownsCar(d.garage, id)) return 'notOwned';
  const levels = levelsOf(d.garage, id);
  const price = upgradePrice(part, levels[part]);
  if (price === null) return 'maxLevel';
  if (walletOf(career, driver) < price) return 'noMoney';
  career.wallets[walletIndex(career, driver)] -= price;
  levels[part]++;
  d.garage.upgrades[id] = levels;
  return 'ok';
}

/** Compra um carro à venda; comprado, ele já vira o carro escolhido. */
export function buyCar(career: CareerState, driver: number, carId: string): PurchaseResult {
  const d = career.drivers[driver];
  const def = CARS.find((c) => c.id === carId);
  if (!d || !def) return 'unknown';
  if (ownsCar(d.garage, carId)) return 'owned';
  if (walletOf(career, driver) < def.price) return 'noMoney';
  career.wallets[walletIndex(career, driver)] -= def.price;
  d.garage.owned.push(carId);
  d.garage.carId = carId;
  return 'ok';
}

/** Escolhe o carro da próxima corrida; só vale para carro da garagem. */
export function selectCar(career: CareerState, driver: number, carId: string): boolean {
  const d = career.drivers[driver];
  if (!d || !ownsCar(d.garage, carId)) return false;
  d.garage.carId = carId;
  return true;
}

// ───────────────────────────── Ciclo da carreira ─────────────────────────────

/** Carreira nova a partir dos humanos do lobby (ordenados por assento). Co-op = mesma regra da copa. */
export function newCareer(humans: HumanEntry[], cups: readonly CupDef[] = CUPS): CareerState {
  const sorted = humans.slice().sort((a, b) => a.seat - b.seat);
  const coop = isCoop(sorted);
  const fallback = CARS.find((c) => c.price === 0)?.id ?? CARS[0].id;
  const drivers: CareerDriver[] = sorted.map((h) => ({
    name: h.name,
    garage: { carId: isFreeCar(h.carId) ? h.carId : fallback, owned: [], upgrades: {} },
    earnings: 0,
  }));
  return {
    version: CAREER_VERSION, coop, drivers,
    wallets: coop ? [CAREER_START_MONEY * drivers.length] : drivers.map(() => CAREER_START_MONEY),
    cupId: cups[0].id, champ: null, rosterSeed: 0, attempts: 1, completed: false, racesRun: 0, lastReport: null,
  };
}

/** Humanos da próxima corrida: assento = índice do piloto, carro escolhido e as melhorias dele. */
export function careerHumans(career: CareerState): HumanEntry[] {
  return career.drivers.map((d, i) => ({
    seat: i, name: d.name, carId: d.garage.carId, teamId: career.coop ? HUMAN_TEAM_ID : i,
    color: SEAT_COLORS[i] ?? '#ffffff', upgrades: levelsOf(d.garage, d.garage.carId),
  }));
}

export function cupIndexOf(career: CareerState, cups: readonly CupDef[] = CUPS): number {
  return Math.max(0, cups.findIndex((c) => c.id === career.cupId));
}

/** Nível da IA na copa atual: 0 na primeira, CAREER_AI_LEVEL_MAX na última, linear entre elas. */
export function careerAiLevel(career: CareerState, cups: readonly CupDef[] = CUPS): number {
  if (cups.length <= 1) return 0;
  return (CAREER_AI_LEVEL_MAX * cupIndexOf(career, cups)) / (cups.length - 1);
}

/** Começa (ou devolve) a copa atual. `rosterSeed` fixa o elenco da IA até a copa acabar. */
export function beginCareerCup(career: CareerState, rosterSeed: number): ChampionshipState {
  if (career.champ) return career.champ;
  career.champ = createChampionship(career.cupId, careerHumans(career));
  career.rosterSeed = rosterSeed >>> 0;
  return career.champ;
}

/**
 * Aplica o resultado de uma corrida: pontos da copa, prêmios (a corrida que elimina não paga),
 * avanço de copa e eliminação (a copa recomeça da primeira corrida, sem limite de tentativas).
 * Devolve o relatório e a copa como ficou (para a classificação), mesmo que a carreira já tenha
 * seguido para a próxima copa.
 */
export function settleCareerRace(career: CareerState, results: RaceResultRow[], cups: readonly CupDef[] = CUPS): { report: CareerReport; champ: ChampionshipState } {
  const champ = career.champ;
  if (!champ) throw new Error('Carreira sem copa em andamento');
  const humans = careerHumans(career);
  const cupIndex = cupIndexOf(career, cups);
  const raceIndex = champ.raceIndex;
  const trackId = cups[cupIndex]?.trackIds[raceIndex] ?? '';
  applyRaceResult(champ, results, humans);
  const qualified = champ.lastVerdict === 'qualified';
  const prizes = racePrizes(results, humans, career.coop, prizeMultiplier(cupIndex, cups.length));
  const rows = prizes.rows.map((r) => ({ ...r, prize: qualified ? r.prize : 0 }));
  const teamBonus = qualified ? prizes.teamBonus : 0;
  for (const r of rows) {
    credit(career, r.driver, r.prize);
    const d = career.drivers[r.driver];
    if (d) d.earnings = Math.min(MONEY_MAX, d.earnings + r.prize);
  }
  if (teamBonus > 0) credit(career, 0, teamBonus);
  career.racesRun++;

  let cupCompleted = false;
  let careerCompleted = false;
  if (!qualified) {
    career.champ = null;
    career.attempts++;
  } else if (champ.completed) {
    cupCompleted = true;
    career.champ = null;
    career.attempts = 1;
    const next = cups[cupIndex + 1];
    if (next) career.cupId = next.id;
    else { career.completed = true; careerCompleted = true; }
  }
  const report: CareerReport = {
    cupId: champ.cupId, trackId, raceIndex, rows, teamBonus, teamRank: prizes.teamRank,
    verdict: qualified ? 'qualified' : 'eliminated', cupCompleted, careerCompleted,
  };
  career.lastReport = report;
  return { report, champ };
}
