// Rival principal de cada copa (docs/RIVAIS.md): quem é, as falas e o duelo com os humanos corrida a
// corrida. O rival é fixo por copa (CUP_RIVALS em data/drivers.ts) — vale na copa normal e na carreira,
// e a tela de copas o mostra antes da largada; createRace o põe no grid (config.rival) com um bônus leve
// de habilidade. Tudo aqui é puro: a interface (resultado, classificação, copas, garagem) e as
// conquistas só leem.
import { cupDef } from '../core/data/cups';
import { AI_DRIVERS, CUP_RIVALS, personalityOf } from '../core/data/drivers';
import { hashString } from '../core/rng';
import type { ChampionshipState, Personality, RaceResultRow } from '../core/types';
import { getLanguage, t } from '../i18n';
import '../rivals/strings';

export interface Rival {
  name: string;
  personality: Personality | null;
  /** Chave das falas próprias (rivals.line.<key>.*): o nome sem acento, em minúsculas. */
  key: string;
}

/** Fala do rival: apresentação e as três do duelo (provocação, respeito, revanche). */
export type RivalLineKind = 'intro' | 'taunt' | 'respect' | 'revenge';
export const RIVAL_LINE_KINDS: readonly RivalLineKind[] = ['intro', 'taunt', 'respect', 'revenge'];

export function driverKey(name: string): string {
  return name.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().replace(/[^a-z0-9]+/g, '_').replace(/^_|_$/g, '');
}

/** Rival da copa. Copa sem rival escolhido (uma nova, por exemplo) sorteia um piloto pelo id, sempre o mesmo. */
export function cupRival(cupId: string): Rival {
  const name = Object.prototype.hasOwnProperty.call(CUP_RIVALS, cupId) ? CUP_RIVALS[cupId] : AI_DRIVERS[hashString(cupId) % AI_DRIVERS.length];
  return { name, personality: personalityOf(name), key: driverKey(name) };
}

export function personalityName(p: Personality | null): string {
  return t(`rivals.personality.${p ?? 'clean'}`);
}

/** A fala no idioma atual: a própria do rival ou, sem ela, a da personalidade dele. */
export function rivalLine(rival: Rival, kind: RivalLineKind): string {
  const own = `rivals.line.${rival.key}.${kind}`;
  const text = t(own);
  return text !== own ? text : t(`rivals.generic.${rival.personality ?? 'clean'}.${kind}`);
}

/** Tem falas próprias (e não as genéricas da personalidade)? */
export function hasOwnLines(rival: Rival): boolean {
  return RIVAL_LINE_KINDS.every((k) => t(`rivals.line.${rival.key}.${k}`) !== `rivals.line.${rival.key}.${k}`);
}

export function ordinal(n: number): string {
  if (getLanguage() !== 'en') return t('rivals.ordinal', { n, s: '' });
  const m100 = n % 100;
  const m10 = n % 10;
  const s = m100 >= 11 && m100 <= 13 ? 'th' : m10 === 1 ? 'st' : m10 === 2 ? 'nd' : m10 === 3 ? 'rd' : 'th';
  return t('rivals.ordinal', { n, s });
}

/** Duelo numa corrida: posição do rival e do melhor humano. Null se um dos lados não correu. */
export interface Duel {
  rivalPosition: number;
  humanPosition: number;
  rivalAhead: boolean;
}

export function raceDuel(results: readonly RaceResultRow[], rivalName: string): Duel | null {
  const rival = results.find((r) => r.seat < 0 && r.name === rivalName);
  const humans = results.filter((r) => r.seat >= 0);
  if (!rival || humans.length === 0) return null;
  const best = Math.min(...humans.map((r) => r.position));
  return { rivalPosition: rival.position, humanPosition: best, rivalAhead: rival.position < best };
}

/**
 * Duelos das corridas já disputadas na copa, pela classificação (posições por corrida): true = o rival
 * terminou à frente do melhor humano; false = atrás; null = um dos lados não correu aquela.
 */
export function cupDuels(champ: ChampionshipState): Array<boolean | null> {
  const rival = cupRival(champ.cupId);
  const row = champ.standings.find((s) => s.seat < 0 && s.name === rival.name);
  const humans = champ.standings.filter((s) => s.seat >= 0);
  const out: Array<boolean | null> = [];
  for (let i = 0; i < champ.raceIndex; i++) {
    const rp = row?.positions[i] ?? 0;
    const hp = humans.map((h) => h.positions[i] ?? 0).filter((p) => p > 0);
    out.push(rp > 0 && hp.length > 0 ? rp < Math.min(...hp) : null);
  }
  return out;
}

/** Fala depois de um duelo: provocação, revanche (ganhou depois de perder o anterior) ou respeito. */
export function duelLineKind(rivalAhead: boolean, previous: boolean | null | undefined): RivalLineKind {
  if (!rivalAhead) return 'respect';
  return previous === false ? 'revenge' : 'taunt';
}

export interface RivalSummary {
  rival: Rival;
  /** Posição na corrida (resultado) ou na copa (classificação). */
  position: number;
  rivalAhead: boolean;
  kind: RivalLineKind;
  /** "Rival: <nome> — <posição>" no idioma atual. */
  title: string;
  line: string;
}

/** Resultado de uma corrida da copa (a copa já com esta corrida somada). Null sem rival no grid ou sem humano. */
export function rivalRaceSummary(champ: ChampionshipState, results: readonly RaceResultRow[]): RivalSummary | null {
  const rival = cupRival(champ.cupId);
  const duel = raceDuel(results, rival.name);
  if (!duel) return null;
  const previous = cupDuels(champ)[champ.raceIndex - 2];
  const kind = duelLineKind(duel.rivalAhead, previous);
  return {
    rival, position: duel.rivalPosition, rivalAhead: duel.rivalAhead, kind,
    title: t('rivals.result', { name: rival.name, pos: ordinal(duel.rivalPosition) }), line: rivalLine(rival, kind),
  };
}

/** Classificação da copa: posição do rival nos pontos e a fala conforme quem está à frente. */
export function rivalStandingsSummary(champ: ChampionshipState): RivalSummary | null {
  const rival = cupRival(champ.cupId);
  const rank = champ.standings.findIndex((s) => s.seat < 0 && s.name === rival.name) + 1;
  const humanRanks = champ.standings.map((s, i) => (s.seat >= 0 ? i + 1 : 0)).filter((r) => r > 0);
  if (rank <= 0 || humanRanks.length === 0) return null;
  const rivalAhead = rank < Math.min(...humanRanks);
  const duels = cupDuels(champ);
  // Revanche só quando ele está à frente, ganhou a última e tinha perdido a anterior; senão, provocação.
  const kind = duelLineKind(rivalAhead, rivalAhead && duels[duels.length - 1] === true ? duels[duels.length - 2] : null);
  return {
    rival, position: rank, rivalAhead, kind,
    title: t('rivals.standing', { name: rival.name, pos: ordinal(rank) }), line: rivalLine(rival, kind),
  };
}

/** RIVAL_DERROTADO: copa inteira disputada com algum humano à frente do rival em todas as corridas. */
export function rivalBeatenEveryRace(champ: ChampionshipState): boolean {
  const races = cupDef(champ.cupId).trackIds.length;
  if (champ.raceIndex < races) return false;
  const duels = cupDuels(champ);
  return duels.length === races && duels.every((d) => d === false);
}
