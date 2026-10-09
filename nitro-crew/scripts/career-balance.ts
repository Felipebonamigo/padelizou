// Balanceamento da carreira, copa a copa, em três partes:
// 1. Economia (instantânea, sem corrida): prêmio de cada copa para quem chega sempre em 1º, 4º (o piloto médio) e
//    8º, o acumulado, as melhorias do piloto médio (compra a peça mais barata do Falcão depois de cada corrida) e o
//    nível da IA; depois, em que copa cada carro à venda cabe no bolso — guardando tudo desde a largada, e depois
//    de o piloto médio completar o Falcão — e o carro com as melhorias todas.
// 2. Perfis de compra (sonda 2.0, instantânea): 5 perfis (scripts/career-balance-lib.ts, PROBE_PROFILES) pela carreira
//    inteira; por copa, o saldo no fim (dinheiro parado) e as corridas sem compra.
// 3. Sonda (corridas inteiras): o piloto médio (piloto-proxy de scripts/career-balance-lib.ts) com as melhorias da
//    calibragem contra o nível e o ritmo da IA (careerAiLevel, careerAiPace) de cada copa. Imprime posições, média e
//    quantas corridas no top 5.
// Mudou prêmio, preço, efeito de melhoria, CAREER_AI_LEVEL_MAX ou CAREER_AI_PACE_POINTS: rode e compare com docs/CARREIRA.md.
// Uso: npx tsx scripts/career-balance.ts [habilidade=0.97] [sementes=3] [máximo da IA=CAREER_AI_LEVEL_MAX] [dificuldade=profissional] [primeira copa] [última copa] [ritmo]
//   sementes=0 imprime só a economia e os perfis; "primeira copa"/"última copa" (ids) limitam a sonda a esse trecho;
//   "ritmo" numérico fixa o aiPace de todas as copas (0 = a IA de antes da onda K); "-" ou ausente usa o careerAiPace de cada copa.
import {
  AVERAGE_POSITION, averagePlayerShare, CAREER_AI_LEVEL_MAX, CAREER_START_MONEY, careerAiLevel, careerAiPace, newCareer, partMaxLevel,
  prizeFor, prizeMultiplier, UPGRADE_PARTS, upgradePrice,
} from '../src/core/career';
import { CARS } from '../src/core/data/cars';
import { CUPS } from '../src/core/data/cups';
import type { Difficulty } from '../src/core/types';
import { averagePlayerUpgrades, PROBE_PROFILES, PROXY_SKILL, proxyPositions, simulateProfile } from './career-balance-lib';

const skill = Number(process.argv[2] ?? PROXY_SKILL);
const seedCount = Number(process.argv[3] ?? 3);
const aiMax = process.argv[4] === undefined || process.argv[4] === '-' ? null : Number(process.argv[4]);
const difficulty = (process.argv[5] ?? 'profissional') as Difficulty;
const firstProbe = process.argv[6] ? Math.max(0, CUPS.findIndex((c) => c.id === process.argv[6])) : 0;
const lastProbe = process.argv[7] ? CUPS.findIndex((c) => c.id === process.argv[7]) : CUPS.length - 1;
if (lastProbe < 0) throw new Error(`copa desconhecida: ${process.argv[7]}`);
const fixedPace = process.argv[8] === undefined || process.argv[8] === '-' ? null : Number(process.argv[8]);
if (fixedPace !== null && !Number.isFinite(fixedPace)) throw new Error(`ritmo inválido: ${process.argv[8]} (use ponto decimal: 0.4)`);
const seeds = Array.from({ length: seedCount }, (_, i) => i + 1);
const career = newCareer([{ seat: 0, name: 'P1', carId: 'falcao', teamId: 0, color: '#fff' }]);
const money = (v: number) => `$ ${Math.round(v).toLocaleString('pt-BR')}`;
const levelSum = (lv: Record<string, number>) => UPGRADE_PARTS.reduce((a, p) => a + lv[p], 0);
const falcaoMax = UPGRADE_PARTS.reduce((a, p) => a + partMaxLevel('falcao', p), 0);

// ───────────────────────────── 1. Economia ─────────────────────────────

/** Dinheiro do piloto que chega sempre na posição `pos`, guardando tudo, ao fim de cada corrida (índice global). */
function savings(pos: number): Array<{ cup: number; race: number; total: number }> {
  const out: Array<{ cup: number; race: number; total: number }> = [];
  let total = CAREER_START_MONEY; let race = 0;
  CUPS.forEach((cup, i) => {
    for (let r = 0; r < cup.trackIds.length; r++) { total += prizeFor(pos, prizeMultiplier(i, CUPS.length)); out.push({ cup: i, race: ++race, total }); }
  });
  return out;
}

/** O que sobra para o piloto médio depois de comprar a peça mais barata do Falcão a cada corrida (a do nível da IA). */
function averageLeftover(): Array<{ cup: number; race: number; total: number }> {
  const out: Array<{ cup: number; race: number; total: number }> = [];
  const lv: Record<string, number> = Object.fromEntries(UPGRADE_PARTS.map((p) => [p, 0]));
  let total = CAREER_START_MONEY; let race = 0;
  CUPS.forEach((cup, i) => {
    for (let r = 0; r < cup.trackIds.length; r++) {
      total += prizeFor(AVERAGE_POSITION, prizeMultiplier(i, CUPS.length));
      for (;;) {
        let best: { part: string; price: number } | null = null;
        for (const part of UPGRADE_PARTS) {
          const price = upgradePrice(part, lv[part], 'falcao');
          if (price !== null && (!best || price < best.price)) best = { part, price };
        }
        if (!best || best.price > total) break;
        total -= best.price; lv[best.part]++;
      }
      out.push({ cup: i, race: ++race, total: levelSum(lv) === falcaoMax ? total : 0 });
    }
  });
  return out;
}

/** Custo de todas as melhorias de um carro (respeitando o teto de cada peça nele). */
function fullUpgradeCost(carId: string): number {
  let sum = 0;
  for (const part of UPGRADE_PARTS) for (let l = 0; l < partMaxLevel(carId, part); l++) sum += upgradePrice(part, l, carId) ?? 0;
  return sum;
}

const when = (rows: Array<{ cup: number; race: number; total: number }>, price: number) => {
  const hit = rows.find((r) => r.total >= price);
  return hit ? `${CUPS[hit.cup].id} (corrida ${hit.race})` : 'nunca';
};

const win = savings(1); const avg = savings(AVERAGE_POSITION); const low = savings(8); const left = averageLeftover();
console.log(`Economia: ${CUPS.length} copas, ${avg.length} corridas. Por copa: prêmio da copa (1º / 4º / 8º sempre), acumulado do 4º guardando tudo, melhorias do Falcão do piloto médio no início da copa, nível da IA.`);
console.log('| # | Copa | Corridas | × prêmio | 1º | 4º | 8º | Acumulado 4º | Melhorias (início) | IA |');
console.log('|---|---|---|---|---|---|---|---|---|---|');
CUPS.forEach((cup, i) => {
  const n = cup.trackIds.length; const m = prizeMultiplier(i, CUPS.length);
  const acc = avg.filter((r) => r.cup === i).at(-1)?.total ?? 0;
  const lv = averagePlayerUpgrades(i);
  const share = averagePlayerShare(i);
  console.log(`| ${i + 1} | ${cup.id} | ${n} | ${m.toFixed(2)} | ${money(n * prizeFor(1, m))} | ${money(n * prizeFor(AVERAGE_POSITION, m))} | ${money(n * prizeFor(8, m))} | ${money(acc)} | ${levelSum(lv)}/${falcaoMax} (${Math.round(100 * share)}%) | ${careerAiLevel({ ...career, cupId: cup.id }).toFixed(2)} |`);
});
console.log(`Total da carreira (com os ${money(CAREER_START_MONEY)} iniciais): 1º ${money(win.at(-1)?.total ?? 0)} · 4º ${money(avg.at(-1)?.total ?? 0)} · 8º ${money(low.at(-1)?.total ?? 0)}. Falcão completo: ${money(fullUpgradeCost('falcao'))}.`);
console.log('');
console.log('Carros à venda: em que copa o preço cabe (guardando tudo desde a largada; o piloto médio depois de completar o Falcão), e o carro com todas as melhorias.');
console.log('| Carro | Preço | 1º guardando | 4º guardando | 4º depois do Falcão completo | Melhorias todas | Carro + melhorias, 4º guardando |');
console.log('|---|---|---|---|---|---|---|');
for (const car of CARS.filter((c) => c.price > 0)) {
  const up = fullUpgradeCost(car.id);
  console.log(`| ${car.name} | ${money(car.price)} | ${when(win, car.price)} | ${when(avg, car.price)} | ${when(left, car.price)} | ${money(up)} | ${when(avg, car.price + up)} |`);
}

// ───────────────────────────── 2. Perfis de compra (sonda 2.0) ─────────────────────────────

const runs = PROBE_PROFILES.map((p) => simulateProfile(p));
console.log('');
console.log('Perfis (sem corrida): saldo no fim da copa · corridas sem compra na copa; "!" = saldo parado acima do prêmio da copa do perfil.');
console.log(`| # | Copa | ${runs.map((r) => r.profile).join(' | ')} |`);
console.log(`|---|---|${runs.map(() => '---').join('|')}|`);
CUPS.forEach((cup, i) => {
  console.log(`| ${i + 1} | ${cup.id} | ${runs.map((r) => { const row = r.rows[i]; return `${money(row.money)}${row.money > row.prize ? '!' : ''} · ${row.racesWithoutPurchase}/${row.races}`; }).join(' | ')} |`);
});
for (const r of runs) console.log(`${r.profile}: maior sequência sem compra ${r.longestWithoutPurchase} corridas · catálogo completo ${r.catalogDoneAtRace === null ? 'nunca' : `na corrida ${r.catalogDoneAtRace}`} · prêmios ${money(r.totalPrize)} · gasto ${money(r.totalSpent)}`);

// ───────────────────────────── 3. Sonda ─────────────────────────────

if (seeds.length > 0) {
  console.log('');
  console.log(`Sonda: piloto ${skill}, ${seeds.length} sementes, ${difficulty}${aiMax === null ? '' : `, IA com máximo ${aiMax}`}.`);
  for (let i = firstProbe; i <= lastProbe; i++) {
    const cup = CUPS[i];
    const level = careerAiLevel({ ...career, cupId: cup.id });
    const aiLevel = aiMax === null ? level : (level * aiMax) / CAREER_AI_LEVEL_MAX;
    const upgrades = averagePlayerUpgrades(i);
    const aiPace = fixedPace ?? careerAiPace({ ...career, cupId: cup.id });
    const t0 = Date.now();
    const ps = proxyPositions(cup.trackIds, aiLevel, upgrades, seeds, skill, difficulty, aiPace);
    const mean = ps.reduce((a, b) => a + b, 0) / ps.length;
    const top5 = ps.filter((p) => p <= 5).length;
    const lv = UPGRADE_PARTS.map((p) => upgrades[p]).join('');
    console.log(`${cup.id.padEnd(13)} IA ${aiLevel.toFixed(2)}  ritmo ${aiPace.toFixed(2).padStart(5)}  [${lv}]  ${ps.join(',')}  média ${mean.toFixed(2)}  top5 ${top5}/${ps.length}  (${((Date.now() - t0) / 1000).toFixed(0)} s)`);
  }
}
