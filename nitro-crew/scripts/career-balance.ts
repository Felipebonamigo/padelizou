// Balanceamento da carreira, copa a copa: o piloto médio (piloto-proxy de scripts/career-balance-lib.ts)
// com as melhorias da calibragem contra o nível da IA de cada copa. Imprime posições, média e quantas
// corridas no top 5. Mudou prêmio, preço, efeito de melhoria ou CAREER_AI_LEVEL_MAX: rode e compare
// com a tabela de docs/CARREIRA.md.
// Uso: npx tsx scripts/career-balance.ts [habilidade=0.97] [sementes=3] [máximo da IA=CAREER_AI_LEVEL_MAX]
import { CAREER_AI_LEVEL_MAX, careerAiLevel, newCareer } from '../src/core/career';
import { CUPS } from '../src/core/data/cups';
import { averagePlayerUpgrades, PROXY_SKILL, proxyPositions } from './career-balance-lib';

const skill = Number(process.argv[2] ?? PROXY_SKILL);
const seedCount = Number(process.argv[3] ?? 3);
const aiMax = process.argv[4] === undefined ? null : Number(process.argv[4]);
const seeds = Array.from({ length: seedCount }, (_, i) => i + 1);
const career = newCareer([{ seat: 0, name: 'P1', carId: 'falcao', teamId: 0, color: '#fff' }]);

for (let i = 0; i < CUPS.length; i++) {
  const cup = CUPS[i];
  const level = careerAiLevel({ ...career, cupId: cup.id });
  const aiLevel = aiMax === null ? level : (level * aiMax) / CAREER_AI_LEVEL_MAX;
  const upgrades = averagePlayerUpgrades(i);
  const t0 = Date.now();
  const ps = proxyPositions(cup.trackIds, aiLevel, upgrades, seeds, skill);
  const avg = ps.reduce((a, b) => a + b, 0) / ps.length;
  const top5 = ps.filter((p) => p <= 5).length;
  const lv = Object.entries(upgrades).map(([k, v]) => `${k}${v}`).join(' ');
  console.log(`${cup.id.padEnd(8)} IA ${aiLevel.toFixed(2)}  [${lv}]  ${ps.join(',')}  média ${avg.toFixed(2)}  top5 ${top5}/${ps.length}  (${((Date.now() - t0) / 1000).toFixed(0)} s)`);
}
