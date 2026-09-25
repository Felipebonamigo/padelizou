import { describe, it, expect } from 'vitest';
import { applyRaceResult, createChampionship, isCoop, nextTrackId, teamRaceRank, teamRaceScore } from '../src/core/championship';
import { POINTS_TABLE } from '../src/core/constants';
import { cupDef } from '../src/core/data/cups';
import { AI_TEAM_ID_BASE } from '../src/core/data/drivers';
import type { HumanEntry, RaceResultRow } from '../src/core/types';
import { createRace } from '../src/core/sim/race';
import { human, quickRace } from './helpers';

function fakeResults(humanPositions: Record<number, number>, humans: HumanEntry[], total = 20): RaceResultRow[] {
  const rows: RaceResultRow[] = [];
  const taken = new Set(Object.values(humanPositions));
  for (const h of humans) rows.push({ carId: 100 + h.seat, seat: h.seat, name: h.name, teamId: h.teamId, carDefId: h.carId, position: humanPositions[h.seat], finished: true, totalTicks: 1000, bestLapTicks: 100, points: POINTS_TABLE[humanPositions[h.seat] - 1] ?? 0 });
  let ai = 0;
  for (let p = 1; p <= total; p++) {
    if (taken.has(p)) continue;
    rows.push({ carId: ai, seat: -1, name: `IA${ai}`, teamId: AI_TEAM_ID_BASE + Math.floor(ai / 2), carDefId: 'falcao', position: p, finished: true, totalTicks: 1000, bestLapTicks: 100, points: POINTS_TABLE[p - 1] ?? 0 });
    ai++;
  }
  return rows.sort((a, b) => a.position - b.position);
}

describe('campeonato', () => {
  it('cooperativo é dois ou mais humanos no mesmo time', () => {
    expect(isCoop([human(0)])).toBe(false);
    expect(isCoop([human(0), human(1)])).toBe(true);
    expect(isCoop([human(0), human(1, 1)])).toBe(false);
  });

  it('solo: fica na copa terminando entre os 5, é eliminado em 6º', () => {
    const humans = [human(0)];
    const champ = createChampionship('brasil', humans);
    const [first, second] = cupDef('brasil').trackIds;
    expect(nextTrackId(champ)).toBe(first);
    applyRaceResult(champ, fakeResults({ 0: 5 }, humans), humans);
    expect(champ.lastVerdict).toBe('qualified'); expect(champ.eliminated).toBe(false);
    expect(nextTrackId(champ)).toBe(second);
    applyRaceResult(champ, fakeResults({ 0: 6 }, humans), humans);
    expect(champ.eliminated).toBe(true); expect(champ.lastVerdict).toBe('eliminated');
  });

  it('solo: uma corrida boa em cada pista completa a copa (só na última), com pontos e vitórias somados', () => {
    const humans = [human(0)];
    const champ = createChampionship('brasil', humans);
    // Copas de 4 pistas (eram 3): a copa só fecha depois da última, e as posições cabem todas.
    const places = cupDef('brasil').trackIds.map((_, i) => (i % 2 === 0 ? 1 : 2));
    expect(places).toHaveLength(4);
    places.forEach((p, i) => {
      expect(champ.completed, `antes da corrida ${i + 1}`).toBe(false);
      applyRaceResult(champ, fakeResults({ 0: p }, humans), humans);
    });
    expect(champ.completed).toBe(true);
    const me = champ.standings.find((s) => s.seat === 0)!;
    expect(me.points).toBe(20 + 15 + 20 + 15); expect(me.wins).toBe(2); expect(me.positions).toEqual([1, 2, 1, 2]);
    expect(nextTrackId(champ)).toBeNull();
  });

  it('equipe: a pontuação da corrida usa os dois melhores do time', () => {
    const humans = [human(0), human(1), human(2), human(3)];
    const results = fakeResults({ 0: 1, 1: 2, 2: 10, 3: 20 }, humans);
    expect(teamRaceScore(results, 0)).toBe(20 + 15);
    expect(teamRaceRank(results, 0)).toBe(1);
  });

  it('equipe: fica na copa entre as 3 melhores equipes, é eliminada em 4º', () => {
    const humans = [human(0), human(1)];
    const champ = createChampionship('eua', humans);
    expect(champ.coop).toBe(true);
    // IA em pares: posições 1-2 = 35, 3-4 = 22, 5-6 = 14; humanos em 7 e 8 = 4+3 = 7 → 4ª equipe
    applyRaceResult(champ, fakeResults({ 0: 7, 1: 8 }, humans), humans);
    expect(champ.lastVerdict).toBe('eliminated');
    const again = createChampionship('eua', humans);
    // humanos em 3 e 5 = 12 + 8 = 20; IA: (1,2)=35, (4,6)=16, (7,8)=7 → 2ª equipe
    applyRaceResult(again, fakeResults({ 0: 3, 1: 5 }, humans), humans);
    expect(again.lastVerdict).toBe('qualified');
    const teams = again.teams.filter((t) => t.isHuman);
    expect(teams.length).toBe(1); expect(teams[0].points).toBe(20);
  });

  it('o elenco da IA é o mesmo em todas as corridas da copa quando a semente do elenco é fixa', () => {
    const humans = [human(0)];
    const roster = (seed: number, rosterSeed?: number) => {
      const r = quickRace({ humans, totalCars: 20, seed });
      const s = createRace({ ...r.state.config, rosterSeed }, r.track);
      return s.cars.map((c) => `${c.name}/${c.carId}`);
    };
    expect(roster(1)).not.toEqual(roster(2));
    expect(roster(1, 99)).toEqual(roster(2, 99));
    expect(roster(3, 99)).toEqual(roster(4, 99));
  });
});
