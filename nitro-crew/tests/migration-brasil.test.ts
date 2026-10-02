// Save de antes da onda G (02/10): a Copa Brasil (copacabana, transpantaneira, serra_do_mar, sampa_noite) deixou de
// existir e virou a Expedição Brasil — 27 copas de estado (br_<uf>) — e o Mundial. Quem já jogava tem de continuar
// abrindo o jogo sem erro e sem perder o que conquistou (docs/PISTAS.md, "Migração").
import { describe, expect, it } from 'vitest';
import { createChampionship } from '../src/core/championship';
import { CUPS, cupDef } from '../src/core/data/cups';
import { isCupUnlocked, markCupCompleted, sanitizeSave } from '../src/game/save';

/** Save como o jogo gravava até a onda F: copa `brasil`, conquista COPA_BRASIL, carreira e copa salvas nela. */
function oldSave(): Record<string, unknown> {
  const humans = [{ seat: 0, name: 'Ana', carId: 'falcao', teamId: 0, color: '#ffd23f' }];
  // O createChampionship de hoje recusa 'brasil'; o save antigo é JSON puro, montado à mão como era gravado.
  const champ = (raceIndex: number) => ({
    cupId: 'brasil', raceIndex, standings: [{ key: 'seat:0', name: 'Ana', seat: 0, teamId: 0, points: 20, wins: 1, positions: [1, 0, 0, 0] }],
    teams: [], coop: false, eliminated: false, completed: false, lastRace: null, lastVerdict: 'qualified',
  });
  return {
    cupsCompleted: ['brasil', 'eua', 'japao'],
    achievements: ['PRIMEIRA_VITORIA', 'COPA_BRASIL', 'COPA_EUA', 'COPA_JAPAO'],
    bestLaps: { copacabana: { ticks: 3900, name: 'Ana', carId: 'falcao', date: '2026-09-30T10:00:00.000Z' } },
    bestRaces: { 'sampa_noite:4': { ticks: 16000, name: 'Ana', carId: 'falcao', date: '2026-09-30T10:00:00.000Z' } },
    racesRun: 40, racesWon: 12,
    career: {
      version: 1, coop: false, drivers: [{ name: 'Ana', garage: { carId: 'falcao', owned: [], upgrades: { falcao: { engine: 2, turbo: 1, tires: 0, brakes: 0, tank: 0, nitro: 0 } } }, earnings: 30000 }],
      wallets: [5200], cupId: 'brasil', champ: champ(1), rosterSeed: 77, attempts: 2, completed: false, racesRun: 9, lastReport: null,
    },
    cupInProgress: { champ: champ(2), cupSeed: 99, humans },
  };
}

describe('save antigo com a Copa Brasil', () => {
  it('abre sem erro; a copa concluída vira a Copa Rio de Janeiro e o carimbo do RJ', () => {
    const s = sanitizeSave(oldSave());
    expect(s.cupsCompleted).toEqual(['br_rj', 'eua', 'japao']);
    expect(s.stamps).toEqual(['RJ']);
    // Recordes e contagens são por pista: nada muda.
    expect(s.bestLaps.copacabana.ticks).toBe(3900);
    expect(s.bestRaces['sampa_noite:4'].ticks).toBe(16000);
    expect(s.racesRun).toBe(40);
  });

  it('a conquista COPA_BRASIL vira COPA_BR_RJ (a que existe hoje), sem duplicar', () => {
    const s = sanitizeSave(oldSave());
    expect(s.achievements).toContain('COPA_BR_RJ');
    expect(s.achievements).not.toContain('COPA_BRASIL');
    expect(sanitizeSave({ achievements: ['COPA_BRASIL', 'COPA_BR_RJ'] }).achievements).toEqual(['COPA_BR_RJ']);
  });

  it('carreira na Copa Brasil segue na Copa Rio de Janeiro, com o dinheiro e a garagem; a copa em andamento recomeça', () => {
    const s = sanitizeSave(oldSave());
    expect(s.career).not.toBeNull();
    const c = s.career!;
    expect(c.cupId).toBe('br_rj');
    expect(c.champ).toBeNull();
    expect(c.wallets).toEqual([5200]);
    expect(c.drivers[0].garage.upgrades.falcao.engine).toBe(2);
    expect(c.attempts).toBe(1);
  });

  it('campeonato normal em andamento na Copa Brasil: some do save (as corridas não batem mais), sem erro', () => {
    expect(sanitizeSave(oldSave()).cupInProgress).toBeNull();
  });

  it('copa já concluída continua aberta, mesmo que a anterior na fila nova não esteja: o Mundial de quem já o jogava', () => {
    const s = sanitizeSave(oldSave());
    // eua exige br_to (fim da Expedição), que esse jogador nunca correu — mas ele já tinha vencido a eua.
    expect(isCupUnlocked(s, 'eua', CUPS)).toBe(true);
    expect(isCupUnlocked(s, 'japao', CUPS)).toBe(true);
    expect(isCupUnlocked(s, 'europa', CUPS)).toBe(true); // exige japao, concluída
    expect(isCupUnlocked(s, 'africa_do_sul', CUPS)).toBe(false);
    // Na Expedição ele segue do RJ (carimbado) para São Paulo.
    expect(isCupUnlocked(s, 'br_sp', CUPS)).toBe(true);
    expect(isCupUnlocked(s, 'br_mg', CUPS)).toBe(false);
  });
});

describe('passaporte: carimbos dos estados', () => {
  it('vencer a copa de um estado carimba o passaporte; copa do Mundial não carimba', () => {
    const s = sanitizeSave({});
    expect(s.stamps).toEqual([]);
    markCupCompleted(s, 'br_sp');
    markCupCompleted(s, 'eua');
    expect(s.stamps).toEqual(['SP']);
    markCupCompleted(s, 'br_sp');
    expect(s.stamps).toEqual(['SP']);
  });

  it('o save conserta os carimbos: lixo sai, e copa de estado concluída sem carimbo ganha o seu', () => {
    const s = sanitizeSave({ stamps: ['RJ', 'XX', 3, 'RJ', 'ba'], cupsCompleted: ['br_pe'] });
    expect(s.stamps).toEqual(['RJ', 'PE']);
    expect(sanitizeSave(JSON.parse(JSON.stringify(s))).stamps).toEqual(['RJ', 'PE']);
  });

  it('a copa legada não existe mais para começar um campeonato', () => {
    expect(() => cupDef('brasil')).toThrow();
    expect(() => createChampionship('brasil', [])).toThrow();
  });
});
