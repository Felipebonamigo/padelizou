// Sonda 2.0 (onda K, frente K3): a economia da carreira por perfil de compra, sem corrida (instantânea).
import { describe, expect, it } from 'vitest';
import { averagePlayerUpgrades, CAREER_START_MONEY } from '../src/core/career';
import { CUPS } from '../src/core/data/cups';
import { PROBE_PROFILES, simulateProfile } from '../scripts/career-balance-lib';

describe('sonda 2.0: perfis de compra', () => {
  it('os 5 perfis do cronograma, com a posição de cada um', () => {
    expect(PROBE_PROFILES.map((p) => [p.id, p.position])).toEqual([['focado', 4], ['colecionador', 4], ['vaidoso', 4], ['sempre1', 1], ['sempre8', 8]]);
  });

  it('o focado é o piloto médio do núcleo: as mesmas melhorias no início de cada copa', () => {
    const focado = PROBE_PROFILES.find((p) => p.id === 'focado');
    if (!focado) throw new Error('sem o focado');
    simulateProfile(focado).rows.forEach((row, i) => expect(row.startLevels, row.cupId).toEqual(averagePlayerUpgrades(i)));
  });

  it('nenhum perfil cria ou perde dinheiro, nem fica negativo', () => {
    for (const p of PROBE_PROFILES) {
      const r = simulateProfile(p);
      expect(r.rows).toHaveLength(CUPS.length);
      expect(r.rows.at(-1)?.money, p.id).toBe(CAREER_START_MONEY + r.totalPrize - r.totalSpent);
      for (const row of r.rows) {
        expect(row.money, `${p.id} ${row.cupId}`).toBeGreaterThanOrEqual(0);
        expect(row.racesWithoutPurchase).toBeLessThanOrEqual(row.races);
      }
    }
  });

  it('hoje o focado não tem o que comprar do DF em diante (Falcão completo no fim do RS): é o que a P1 vai mudar', () => {
    const focado = PROBE_PROFILES.find((p) => p.id === 'focado');
    if (!focado) throw new Error('sem o focado');
    const r = simulateProfile(focado);
    const df = CUPS.findIndex((c) => c.id === 'br_df');
    for (const row of r.rows.slice(df)) expect(row.racesWithoutPurchase, row.cupId).toBe(row.races);
    expect(r.rows[df - 1].racesWithoutPurchase).toBeLessThan(r.rows[df - 1].races);
  });
});
