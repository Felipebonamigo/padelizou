// Os dois ganchos da sessão para estatísticas e conquistas, fora de session.ts para rodarem nos
// testes em Node (a sessão precisa de DOM e WebGL): o passo observado de cada tick e o fechamento
// das contas no tick em que a corrida acaba. A sessão só liga os efeitos (Steam, HUD, save).
import { applyRaceResult } from '../core/championship';
import { stepRace } from '../core/sim/race';
import type { ChampionshipState, HumanEntry, PlayerInput, RaceState, SimEvent, Track } from '../core/types';
import { achievementMessages, observeTick, unlockAchievements, type AchievementUnlock, type RaceTelemetry } from './achievements';
import type { HudMessage, RaceMode, SaveData } from './contracts';
import { markCupCompleted, recordRaceResults, rememberLobby, type NewRecord } from './save';
import { recordRaceStats } from './stats';

/** O que a corrida deixou: recordes novos e conquistas, para o resultado. */
export interface RaceOutcome {
  newRecords: NewRecord[];
  achievements: AchievementUnlock[];
}

/** A parte da corrida ativa da sessão que o fechamento lê (e marca). */
export interface SettleTarget {
  state: RaceState;
  track: Track;
  mode: RaceMode;
  humans: HumanEntry[];
  telemetry: RaceTelemetry;
  outcome: RaceOutcome | null;
}

export interface SettleEffects {
  /** Conquista desbloqueada (Steam). */
  achievement(id: string): void;
  /** Mensagem no HUD de um assento. */
  hud(seat: number, message: HudMessage): void;
  /** Grava o save. */
  persist(): void;
}

export interface SettleOptions {
  champ: ChampionshipState | null;
  difficulty: string;
  /** Segundos da mensagem de conquista no HUD. */
  hudTtl: number;
  effects: SettleEffects;
}

/**
 * Um tick como a sessão faz: simula e observa a telemetria ANTES de alguém tratar os eventos,
 * para que o tick que fecha a corrida (chegada, nitro na bandeirada) já esteja contado quando o
 * race_over chamar settleRace. Devolve os eventos do tick.
 */
export function stepObserved(r: Pick<SettleTarget, 'state' | 'track' | 'telemetry'>, inputs: ReadonlyArray<PlayerInput | undefined>): readonly SimEvent[] {
  stepRace(r.state, r.track, inputs);
  observeTick(r.telemetry, r.state, r.track);
  return r.state.events;
}

/**
 * Fecha as contas no tick em que a corrida acaba (evento race_over): recordes, copa, estatísticas,
 * conquistas e save. As estatísticas entram ANTES das conquistas: as cumulativas (MARATONA,
 * DEZ_VITORIAS, GIRO_COMPLETO) leem o save já com esta corrida somada. As conquistas vão já para
 * o HUD de quem as ganhou (mensagem 'good'). Idempotente.
 */
export function settleRace(save: SaveData, r: SettleTarget, o: SettleOptions): RaceOutcome {
  if (r.outcome) return r.outcome;
  // Marcado antes de qualquer efeito: se um passo lançar, a próxima chamada (a sessão chama de novo
  // no quadro seguinte) devolve o que já foi fechado em vez de somar a corrida outra vez, e o
  // resultado aparece. O que já entrou no save é gravado mesmo assim (finally).
  const out: RaceOutcome = { newRecords: [], achievements: [] };
  r.outcome = out;
  try {
    const results = r.state.results ?? [];
    out.newRecords = recordRaceResults(save, results, r.humans, r.track.def.id, r.state.config.laps, r.mode);
    rememberLobby(save, r.humans);
    recordRaceStats(save.stats, { mode: r.mode, state: r.state, results, humans: r.humans, telemetry: r.telemetry });
    let cupJustCompleted: string | null = null;
    if (o.champ && r.mode === 'cup') {
      applyRaceResult(o.champ, results, r.humans);
      if (o.champ.completed) { markCupCompleted(save, o.champ.cupId); cupJustCompleted = o.champ.cupId; }
    }
    out.achievements = unlockAchievements(save, r.mode, r.state, results, r.humans, r.telemetry, r.track.def.timeOfDay === 'night', cupJustCompleted, o.difficulty);
    for (const u of out.achievements) {
      save.achievements.push(u.id);
      o.effects.achievement(u.id);
    }
    for (const [seat, text] of achievementMessages(out.achievements)) o.effects.hud(seat, { text, kind: 'good', ttl: o.hudTtl });
  } finally {
    o.effects.persist();
  }
  return out;
}
