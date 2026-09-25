// Calibragem da carreira contra a IA que evolui, com corridas inteiras (20 carros, profissional,
// assistências padrão). O "piloto médio" da economia é o que chega por volta de 4º na primeira copa
// (a calibragem de dinheiro supõe sempre 4º); aqui ele é o cérebro da IA com habilidade fixa no assento
// humano. Com as melhorias que essa calibragem diz que ele teria no início da última copa, e contra o
// nível da IA daquela copa, ele precisa seguir brigando pelo top 5 — senão o jogador médio não chega
// ao fim da carreira. Arquivo próprio porque as corridas levam alguns segundos (roda em paralelo).
// A sonda completa, copa a copa: `npx tsx scripts/career-balance.ts` (docs/CARREIRA.md).
import { describe, expect, it } from 'vitest';
import { averagePlayerUpgrades, PROXY_SKILL, proxyPositions } from '../scripts/career-balance-lib';
import { careerAiLevel, newCareer, NO_UPGRADES } from '../src/core/career';
import { CUPS } from '../src/core/data/cups';

const SEEDS = [1, 2];
const mean = (xs: number[]) => xs.reduce((a, b) => a + b, 0) / xs.length;

describe('calibragem contra a IA que evolui', () => {
  it('o piloto médio (≈4º na primeira copa, de fábrica) segue no top 5 na última copa, com as melhorias da calibragem', () => {
    const first = proxyPositions(CUPS[0].trackIds, 0, NO_UPGRADES, SEEDS);
    expect(mean(first), `âncora: o piloto-proxy (habilidade ${PROXY_SKILL}) devia chegar por volta de 4º na primeira copa (${first.join(',')}); recalibre PROXY_SKILL`).toBeGreaterThanOrEqual(2.5);
    expect(mean(first), `âncora (${first.join(',')})`).toBeLessThanOrEqual(5.5);

    const last = CUPS.length - 1;
    const career = newCareer([{ seat: 0, name: 'P1', carId: 'falcao', teamId: 0, color: '#fff' }]);
    career.cupId = CUPS[last].id;
    const aiLevel = careerAiLevel(career);
    expect(aiLevel).toBeGreaterThan(0);
    const upgrades = averagePlayerUpgrades(last);
    const final = proxyPositions(CUPS[last].trackIds, aiLevel, upgrades, SEEDS);
    const msg = `última copa (${CUPS[last].id}, IA ${aiLevel.toFixed(2)}, melhorias ${JSON.stringify(upgrades)}): ${final.join(',')}`;
    expect(mean(final), msg).toBeLessThanOrEqual(5.5);
    expect(final.filter((p) => p <= 5).length, msg).toBeGreaterThanOrEqual(final.length / 2);
  }, 180_000);
});
