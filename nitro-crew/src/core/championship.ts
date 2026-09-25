// Copa: soma de pontos por corrida, classificação individual e por equipe, regra de eliminação.
import { QUALIFY_POSITION, TEAM_QUALIFY_RANK } from './constants';
import { cupDef } from './data/cups';
import { AI_TEAM_ID_BASE, AI_TEAMS, HUMAN_TEAM_NAME } from './data/drivers';
import type { ChampionshipState, HumanEntry, RaceResultRow, StandingRow, TeamStandingRow } from './types';

export function entrantKey(row: { seat: number; name: string }): string {
  return row.seat >= 0 ? `seat:${row.seat}` : row.name;
}

export function teamName(teamId: number, humans: HumanEntry[]): string {
  if (teamId >= AI_TEAM_ID_BASE) return AI_TEAMS[(teamId - AI_TEAM_ID_BASE) % AI_TEAMS.length];
  const members = humans.filter((h) => h.teamId === teamId);
  if (members.length === 1) return members[0].name;
  return HUMAN_TEAM_NAME;
}

/** Cooperativo de verdade só com dois ou mais humanos no mesmo time. */
export function isCoop(humans: HumanEntry[]): boolean {
  return humans.length >= 2 && humans.every((h) => h.teamId === humans[0].teamId);
}

export function createChampionship(cupId: string, humans: HumanEntry[]): ChampionshipState {
  cupDef(cupId);
  return { cupId, raceIndex: 0, standings: [], teams: [], coop: isCoop(humans), eliminated: false, completed: false, lastRace: null, lastVerdict: null };
}

export function nextTrackId(champ: ChampionshipState): string | null {
  const cup = cupDef(champ.cupId);
  return champ.raceIndex < cup.trackIds.length ? cup.trackIds[champ.raceIndex] : null;
}

/** Pontuação de equipe numa corrida: os dois melhores resultados do time. */
export function teamRaceScore(results: RaceResultRow[], teamId: number): number {
  return results.filter((r) => r.teamId === teamId).map((r) => r.points).sort((a, b) => b - a).slice(0, 2).reduce((a, b) => a + b, 0);
}

/** Colocação da equipe entre todas as equipes da corrida (1 = melhor). Empate favorece o humano. */
export function teamRaceRank(results: RaceResultRow[], teamId: number): number {
  const ids = [...new Set(results.map((r) => r.teamId))];
  const scores = ids.map((id) => ({ id, score: teamRaceScore(results, id) }));
  const mine = scores.find((s) => s.id === teamId)?.score ?? 0;
  return 1 + scores.filter((s) => s.id !== teamId && s.score > mine).length;
}

export function applyRaceResult(champ: ChampionshipState, results: RaceResultRow[], humans: HumanEntry[]): void {
  const cup = cupDef(champ.cupId);
  const raceCount = cup.trackIds.length;
  for (const r of results) {
    const key = entrantKey(r);
    let row = champ.standings.find((s) => s.key === key);
    if (!row) {
      row = { key, name: r.name, seat: r.seat, teamId: r.teamId, points: 0, wins: 0, positions: new Array(raceCount).fill(0) } satisfies StandingRow;
      champ.standings.push(row);
    }
    row.points += r.points;
    if (r.position === 1) row.wins++;
    row.positions[champ.raceIndex] = r.position;
  }
  champ.standings.sort((a, b) => b.points - a.points || b.wins - a.wins || a.name.localeCompare(b.name));

  const teamIds = [...new Set(results.map((r) => r.teamId))];
  for (const id of teamIds) {
    let t = champ.teams.find((x) => x.teamId === id);
    if (!t) { t = { teamId: id, name: teamName(id, humans), points: 0, isHuman: id < AI_TEAM_ID_BASE } satisfies TeamStandingRow; champ.teams.push(t); }
    t.points += teamRaceScore(results, id);
  }
  champ.teams.sort((a, b) => b.points - a.points || (a.isHuman ? -1 : 1));

  // Regra de classificação: equipe entre as TEAM_QUALIFY_RANK melhores (co-op) ou algum humano no top QUALIFY_POSITION.
  let qualified: boolean;
  if (champ.coop) {
    qualified = teamRaceRank(results, humans[0].teamId) <= TEAM_QUALIFY_RANK;
  } else {
    qualified = results.some((r) => r.seat >= 0 && r.position <= QUALIFY_POSITION);
  }
  champ.lastRace = results;
  champ.lastVerdict = qualified ? 'qualified' : 'eliminated';
  champ.raceIndex++;
  if (!qualified) champ.eliminated = true;
  else if (champ.raceIndex >= raceCount) champ.completed = true;
}
