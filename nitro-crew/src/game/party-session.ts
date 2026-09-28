// Orquestração dos modos de festa (docs/MODOS.md): torneio de sofá (inscrição → "passe o controle"
// → bateria → resultado → classificação → … → final), escolta e revezamento. As regras são puras e
// moram no núcleo (src/core/tournament.ts, src/core/modes.ts); aqui só se liga isso à sessão
// (corrida, menus, HUD). Em session.ts ficam só os ganchos que chamam este módulo. Tudo local: o
// online não monta estas corridas (src/game/online-session.ts: raceConfigFrom).
import { seatColor } from '../core/data/drivers';
import {
  createTournament, currentHeat, heatHumans, isFinal, qualifyingHeatCount, recordHeat, type HeatInfo, type TournamentPlayer,
  type TournamentSetup, type TournamentState,
} from '../core/tournament';
import { escortOutcome, relayTurn, vipCar } from '../core/modes';
import type { HumanEntry, RaceConfig, RaceState, SimEvent } from '../core/types';
import { t } from '../i18n';
import { ordinalText } from '../party/rules';
import '../party/strings';
import { compactHumans, type SeatBinder } from './career-session';
import type { HudMessage, Menus, PartyMode, PartyResultsInfo, RaceMode, Settings } from './contracts';

/** Inscrição do torneio sendo montada: sobrevive a ir e voltar do lobby. */
export interface TournamentDraft {
  players: TournamentPlayer[];
  rounds: number;
  cupId: string;
}

/** O que a festa precisa da sessão. */
export interface PartyHost {
  menus: Menus;
  /** Opções da sessão (objeto estável; o seatAssists dentro dele é trocado a cada mudança). */
  settings: Settings;
  input: SeatBinder;
  /** Configuração da sessão (dificuldade, câmbio, assistências, carros na pista) para a pista dada. */
  baseConfig(trackId: string, laps: number, humans: HumanEntry[], seed: number): RaceConfig;
  beginRace(config: RaceConfig, mode: RaceMode, humans: HumanEntry[]): void;
  randomSeed(): number;
}

/** Uma mensagem de HUD que um evento da corrida gera para um assento. */
export interface PartyHudEvent {
  seat: number;
  message: HudMessage;
}

export interface PartyController {
  /** Torneio em andamento (null fora dele). */
  readonly tournament: TournamentState | null;
  /** Inscrição sendo montada. */
  readonly draft: TournamentDraft;
  /** Da tela de pistas: escolta ou revezamento. */
  startMode(mode: 'escort' | 'relay', trackId: string, laps: number, humans: HumanEntry[]): void;
  /** Da inscrição: começa o torneio (religa os controles em 0..n-1) e abre "passe o controle". */
  startTournament(setup: TournamentSetup, seats: number[]): void;
  /** De "passe o controle": larga a bateria atual. */
  runHeat(): void;
  /**
   * Fim de uma corrida de festa, antes do resultado aparecer: registra a bateria do torneio
   * (idempotente) e devolve o que o resultado mostra do modo.
   */
  raceFinished(mode: RaceMode, state: RaceState): PartyResultsInfo | undefined;
  /** Linhas fixas do HUD de um assento neste quadro (mesmo objeto enquanto o texto não muda). */
  hudLines(mode: RaceMode, state: RaceState, seat: number): HudMessage[];
  /** Mensagens de HUD que um evento do núcleo gera nos modos de festa. */
  eventMessages(mode: RaceMode, state: RaceState, e: SimEvent): PartyHudEvent[];
  /** Menu principal: larga o torneio em andamento. */
  abandon(): void;
}

export function isPartyRaceMode(mode: RaceMode): mode is PartyMode {
  return mode === 'tournament' || mode === 'escort' || mode === 'relay';
}

/** "Rodada 1 de 2 · Bateria 2 de 3" ou "FINAL". */
export function heatLabel(t0: TournamentState, info: HeatInfo): string {
  if (info.final) return t('party.t.final.title');
  return `${t('party.t.round', { r: info.round + 1, total: t0.setup.rounds })} · ${t('party.t.heat', { n: info.heat + 1, m: info.heatCount })}`;
}

/** Baterias classificatórias já corridas, para "depois de 3 de 4". */
export function qualifyingDone(t0: TournamentState): number {
  return t0.history.filter((h) => !h.final).length;
}

export { qualifyingHeatCount };

/** Mensagem de HUD de um evento de festa, na duração de sempre do HUD. */
function hud(text: string, kind: HudMessage['kind'], ttl: number): HudMessage {
  return { text, kind, ttl };
}

/** Nome do assento na corrida (o do lobby/torneio), para as mensagens do revezamento. */
function seatName(humans: readonly HumanEntry[], seat: number): string {
  return humans.find((h) => h.seat === seat)?.name ?? `P${seat + 1}`;
}

/** Linhas fixas do HUD (puro): o que o assento precisa saber o tempo todo no modo. */
export function partyHudTexts(mode: RaceMode, state: RaceState, seat: number): string[] {
  if (mode === 'escort') {
    const vip = vipCar(state);
    return vip ? [t('party.escort.hud', { pos: ordinalText(vip.position) })] : [];
  }
  if (mode === 'relay') {
    const turn = relayTurn(state, seat);
    if (!turn || turn.car.finished || turn.partner < 0) return [];
    if (!turn.driving) return [t('party.relay.waiting')];
    return turn.relay.due ? [t('party.relay.pitNow', { name: seatName(state.config.humans, turn.partner) })] : [];
  }
  return [];
}

/** Mensagens de HUD que um evento do núcleo gera nos modos de festa (puro). */
export function partyEventMessages(mode: RaceMode, state: RaceState, e: SimEvent): PartyHudEvent[] {
  if (mode === 'escort' && e.type === 'tow' && e.carId === state.party?.vipId) {
    const by = state.cars[e.byId];
    return by && by.seat >= 0 ? [{ seat: by.seat, message: hud(t('party.escort.pushing'), 'good', 1.2) }] : [];
  }
  if (mode === 'relay' && e.type === 'relay_swap') {
    const name = seatName(state.config.humans, e.toSeat);
    return [
      { seat: e.toSeat, message: hud(t('party.relay.yourTurn'), 'big', 1.5) },
      { seat: e.fromSeat, message: hud(t('party.relay.handed', { name }), 'good', 2) },
    ];
  }
  if (mode === 'relay' && e.type === 'relay_missed') {
    const car = state.cars[e.carId];
    const rc = state.party?.relay.find((r) => r.carId === e.carId);
    if (!car || !rc) return [];
    const text = t('party.relay.missed', { name: seatName(state.config.humans, car.seat) });
    return rc.seats.map((seat) => ({ seat, message: hud(text, 'warn', 2.5) }));
  }
  return [];
}

export function createPartySession(host: PartyHost): PartyController {
  let tournament: TournamentState | null = null;
  const draft: TournamentDraft = { players: [], rounds: 2, cupId: 'brasil' };
  /** Linhas fixas do HUD por assento: o mesmo objeto enquanto o texto não muda (o HUD reanima a cada objeto novo). */
  const lines: Record<string, HudMessage> = {};
  function startMode(mode: 'escort' | 'relay', trackId: string, laps: number, humans: HumanEntry[]): void {
    tournament = null;
    const config = host.baseConfig(trackId, laps, humans, host.randomSeed());
    config.mode = mode;
    host.beginRace(config, mode, humans);
  }

  function startTournament(setup: TournamentSetup, seats: number[]): void {
    // Os controles do lobby vão para os assentos 0..n-1 (a bateria usa sempre os primeiros).
    const placeholders: HumanEntry[] = [...seats].sort((a, b) => a - b).map((seat) => ({ seat, name: '', carId: 'falcao', teamId: 0, color: seatColor(seat) }));
    compactHumans(host.input, placeholders, host.settings.seatAssists);
    tournament = createTournament({ ...setup, controllers: Math.max(1, Math.min(setup.controllers, seats.length)) });
    host.menus.show('handoff');
  }

  function runHeat(): void {
    if (!tournament) { host.menus.show('main'); return; }
    const info = currentHeat(tournament);
    if (!info) { host.menus.show('tournamentTable'); return; }
    const humans = heatHumans(tournament, info);
    const config = host.baseConfig(info.trackId, tournament.setup.laps, humans, host.randomSeed());
    host.beginRace(config, 'tournament', humans);
  }

  function raceFinished(mode: RaceMode, state: RaceState): PartyResultsInfo | undefined {
    if (mode === 'tournament' && tournament) {
      const info = currentHeat(tournament);
      if (!info) return undefined;
      const label = heatLabel(tournament, info);
      recordHeat(tournament, state.results ?? []);
      return { kind: 'tournament', label };
    }
    if (mode === 'relay' && state.party) {
      const swaps = state.party.relay.map((rc) => ({ name: state.cars[rc.carId]?.name ?? '?', swaps: rc.swaps }));
      return { kind: 'relay', swaps };
    }
    if (mode === 'escort') {
      const out = escortOutcome(state);
      const vip = vipCar(state);
      return out && vip ? { kind: 'escort', vipPosition: out.vipPosition, success: out.success, vipCarId: vip.id } : undefined;
    }
    return undefined;
  }

  function hudLines(mode: RaceMode, state: RaceState, seat: number): HudMessage[] {
    return partyHudTexts(mode, state, seat).map((text, i) => {
      const key = `${seat}:${i}`;
      const prev = lines[key];
      if (prev && prev.text === text) return prev;
      const m = hud(text, 'info', 1);
      lines[key] = m;
      return m;
    });
  }

  function eventMessages(mode: RaceMode, state: RaceState, e: SimEvent): PartyHudEvent[] {
    return partyEventMessages(mode, state, e);
  }

  return {
    get tournament() { return tournament; },
    draft,
    startMode, startTournament, runHeat, raceFinished, hudLines, eventMessages,
    abandon() { tournament = null; },
  };
}

/** Torneio no ponto de mostrar a final? (a classificação troca o botão por "para a final"). */
export function finalIsNext(t0: TournamentState): boolean {
  return !t0.done && isFinal(t0);
}
