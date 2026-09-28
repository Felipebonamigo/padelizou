// Orquestração do Modo Carreira: lobby → garagem → corrida da copa atual → resultado →
// classificação → garagem → … As regras (dinheiro, compras, avanço de copa) são puras e moram
// em src/core/career.ts; aqui só se liga isso à sessão (corrida, menus, save). Em session.ts ficam
// só os ganchos que chamam este módulo.
import {
  beginCareerCup, careerAiLevel, careerHumans, newCareer, settleCareerRace, type CareerState,
} from '../core/career';
import { isCoop, nextTrackId } from '../core/championship';
import { seatColor } from '../core/data/drivers';
import { hashString } from '../core/rng';
import { trackDef } from '../core/track';
import type { AssistLevel, ChampionshipState, HumanEntry, RaceConfig, RaceResultRow } from '../core/types';
import type { Menus, RaceMode, SaveData, Settings } from './contracts';
import { gridRival } from './rivals';

/** O pedaço do InputProvider que troca assentos (testável sem DOM). */
export interface SeatBinder {
  seatDevice(seat: number): string | null;
  bindSeat(seat: number, device: string): void;
  unbindSeat(seat: number): void;
}

/**
 * Humanos com assentos contíguos (0..n-1), levando o dispositivo de cada um junto. Quem saiu de um
 * assento do meio do lobby deixa buraco (P1 e P3); a copa e a carreira salvas guardam os assentos,
 * e o lobby de "Continuar" sempre os preenche a partir do P1 — então eles nascem contíguos.
 * `seatAssists` (opções) é reordenado junto: a direção assistida escolhida no cartão segue o dispositivo,
 * senão quem vai do P3 para o P2 correria com a opção que sobrou no P2 (docs/ASSISTENCIAS.md).
 */
export function compactHumans(input: SeatBinder, humans: HumanEntry[], seatAssists?: AssistLevel[]): HumanEntry[] {
  const sorted = humans.slice().sort((a, b) => a.seat - b.seat);
  if (sorted.every((h, i) => h.seat === i)) return sorted.map((h) => ({ ...h }));
  const coop = isCoop(sorted);
  const devices = sorted.map((h) => input.seatDevice(h.seat));
  for (const h of sorted) input.unbindSeat(h.seat);
  if (seatAssists) {
    const chosen = sorted.map((h) => seatAssists[h.seat] ?? 'none');
    chosen.forEach((level, i) => { seatAssists[i] = level; });
  }
  return sorted.map((h, i) => {
    const device = devices[i];
    if (device) input.bindSeat(i, device);
    if (h.seat === i) return { ...h };
    return { ...h, seat: i, teamId: coop ? h.teamId : i, color: seatColor(i) };
  });
}

/** O que a carreira precisa da sessão. */
export interface CareerHost {
  save: SaveData;
  /** Opções da sessão (objeto estável; o seatAssists dentro dele é trocado a cada mudança). */
  settings: Settings;
  menus: Menus;
  input: SeatBinder;
  /** Configuração da sessão (dificuldade, câmbio, assistências, carros na pista) para a pista dada. */
  baseConfig(trackId: string, laps: number, humans: HumanEntry[], seed: number): RaceConfig;
  beginRace(config: RaceConfig, mode: RaceMode, humans: HumanEntry[]): void;
  /** Sai da corrida para o fundo animado dos menus, sem desligar os assentos. */
  toIdle(): void;
  randomSeed(): number;
  /** Grava o save. */
  persist(): void;
}

export interface CareerSession {
  /** Do lobby: carreira nova (substitui a salva) ou continuar a salva. Abre a garagem. */
  start(humans: HumanEntry[], resume: boolean): void;
  /** Da garagem: corre a próxima corrida da copa atual (começando a copa, se preciso). */
  race(): void;
  /** Fim de uma corrida da carreira: pontos, prêmios e avanço. Devolve a copa para a classificação. */
  raceFinished(results: RaceResultRow[]): { champ: ChampionshipState | null; cupCompleted: string | null };
  /** Da classificação: volta para a garagem. */
  showGarage(): void;
}

export function createCareerSession(host: CareerHost): CareerSession {
  const current = (): CareerState | null => host.save.career;

  function showGarage(): void {
    host.toIdle();
    host.menus.show('garage');
  }

  function start(humans: HumanEntry[], resume: boolean): void {
    const hs = compactHumans(host.input, humans, host.settings.seatAssists);
    if (!resume || !current()) host.save.career = newCareer(hs);
    host.persist();
    showGarage();
  }

  function race(): void {
    const career = current();
    if (!career || career.completed) { showGarage(); return; }
    const champ = beginCareerCup(career, hashString(`${career.cupId}:${host.randomSeed()}`));
    const trackId = nextTrackId(champ);
    if (!trackId) { showGarage(); return; }
    const humans = careerHumans(career);
    const config = host.baseConfig(trackId, trackDef(trackId).laps, humans, host.randomSeed());
    config.rosterSeed = career.rosterSeed;
    config.aiLevel = careerAiLevel(career);
    // O rival da copa vale também na carreira: o mesmo piloto da copa normal.
    config.rival = gridRival(champ);
    host.persist();
    host.beginRace(config, 'career', humans);
  }

  function raceFinished(results: RaceResultRow[]): { champ: ChampionshipState | null; cupCompleted: string | null } {
    const career = current();
    if (!career || !career.champ) return { champ: null, cupCompleted: null };
    const cupId = career.champ.cupId;
    const { report, champ } = settleCareerRace(career, results);
    return { champ, cupCompleted: report.cupCompleted ? cupId : null };
  }

  return { start, race, raceFinished, showGarage };
}
