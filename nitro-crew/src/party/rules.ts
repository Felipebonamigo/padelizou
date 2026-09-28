// Regras de interface dos modos de festa, sem DOM (testáveis em Node): o que o lobby mostra e
// exige em cada modo. As regras da corrida moram no núcleo (src/core/modes.ts, src/core/tournament.ts).
import type { PartyMode, RaceMode } from '../game/contracts';
import { t } from '../i18n';
import { ordinalSuffix } from '../render/strings';
import './strings';

export const PARTY_MODES: readonly PartyMode[] = ['tournament', 'escort', 'relay'];

export function isPartyMode(mode: RaceMode): mode is PartyMode {
  return (PARTY_MODES as readonly string[]).includes(mode);
}

/** Versus faz sentido no modo com este número de assentos? (Escolta e torneio: não; revezamento: só com 2 duplas.) */
export function versusAllowed(mode: RaceMode, seats: number): boolean {
  if (mode === 'timetrial' || mode === 'tournament' || mode === 'escort') return false;
  if (mode === 'relay') return seats === 4;
  return true;
}

/** Motivo (texto) de o lobby ainda não poder começar por regra do modo; null = pode. */
export function partySeatsProblem(mode: RaceMode, seats: number): string | null {
  if (mode === 'relay' && seats !== 2 && seats !== 4) return t('party.lobby.relayPairs');
  return null;
}

/** O lobby esconde o nome e o carro do assento: no torneio o controle passa de mão em mão. */
export function lobbyHidesDriver(mode: RaceMode): boolean {
  return mode === 'tournament';
}

/** Revezamento: o segundo de cada dupla não escolhe carro (a dupla corre no carro de quem larga). */
export function lobbyHidesCar(mode: RaceMode, rank: number): boolean {
  return mode === 'relay' && rank % 2 === 1;
}

/**
 * Rótulo do time no cartão do lobby, quando o modo tem um próprio. `rank` = ordem do assento entre os
 * ocupados: o núcleo forma as duplas do revezamento assim (src/core/modes.ts: relayPairs).
 */
export function lobbyTeamLabel(mode: RaceMode, rank: number): string | null {
  if (mode === 'relay') return t('party.lobby.pairN', { n: Math.floor(rank / 2) + 1 });
  if (mode === 'escort') return t('party.lobby.escortTeam');
  if (mode === 'tournament') return t('party.lobby.controllers');
  return null;
}

/** "3º" / "3rd". */
export function ordinalText(n: number): string {
  return t('hud.ordinal', { n, s: ordinalSuffix(n) });
}
