// Os ganchos da sessão (src/game/raceEnd.ts): passo observado e fechamento das contas no race_over,
// dirigidos como a sessão faz. session.ts precisa de DOM e WebGL; ela só liga os efeitos.
import fs from 'node:fs';
import { describe, expect, it } from 'vitest';
import { NEUTRAL_INPUT, type HumanEntry, type PlayerInput, type RaceState, type Track } from '../src/core/types';
import { setLanguage } from '../src/i18n';
import { MARATHON_METERS, newTelemetry } from '../src/game/achievements';
import type { HudMessage } from '../src/game/contracts';
import { settleRace, stepObserved, type RaceOutcome, type SettleOptions, type SettleTarget } from '../src/game/raceEnd';
import { sanitizeSave } from '../src/game/save';
import { EMPTY_STATS } from '../src/game/stats';
import { human, quickRace, run, syntheticTrack } from './helpers';

interface Effects {
  achievements: string[];
  hud: Array<{ seat: number; message: HudMessage }>;
  persisted: number;
}

function options(fx: Effects): SettleOptions {
  return {
    champ: null, difficulty: 'profissional', hudTtl: 3.5,
    effects: {
      achievement: (id) => { fx.achievements.push(id); },
      hud: (seat, message) => { fx.hud.push({ seat, message }); },
      persist: () => { fx.persisted++; },
    },
  };
}

function target(state: RaceState, track: Track, humans: HumanEntry[]): SettleTarget {
  return { state, track, mode: 'quick', humans, telemetry: newTelemetry(), outcome: null };
}

describe('fim de corrida dirigido como a sessão', () => {
  const humans = [human(0)];
  const { state, track } = quickRace({ track: syntheticTrack([{ op: 'straight', length: 300 }]), humans, totalCars: 4, laps: 1, seed: 3 });
  const r = target(state, track, humans);
  const save = sanitizeSave({});
  // Faltam 100 m para os 1.000 km: a MARATONA só sai nesta corrida se as estatísticas entrarem antes das conquistas.
  save.stats.totals.meters = MARATHON_METERS - 100;
  const fx: Effects = { achievements: [], hud: [], persisted: 0 };
  const finishLine = state.config.laps * track.length;
  let settledAt = -1;
  let first: RaceOutcome | null = null;
  setLanguage('pt');
  for (let i = 0; i < 60 * 240 && state.phase !== 'finished'; i++) {
    const me = state.cars.find((c) => c.seat === 0);
    // Nitro ligado nos últimos metros: cruza a linha com ele ativo.
    const inputs: PlayerInput[] = [{ ...NEUTRAL_INPUT, throttle: true, nitro: !!me && me.progress > finishLine - 5000 }];
    for (const e of stepObserved(r, inputs)) {
      if (e.type === 'race_over') { first = settleRace(save, r, options(fx)); settledAt = state.tick; }
    }
  }

  it('a corrida acaba e as contas fecham no tick do race_over', () => {
    expect(state.phase).toBe('finished');
    expect(settledAt).toBeGreaterThan(0);
    expect(first).not.toBeNull();
    expect(r.outcome).toBe(first);
  });

  it('o tick que fecha a corrida já está observado: nitro na bandeirada', () => {
    expect(r.telemetry.seats.get(0)?.nitroAtFinish).toBe(true);
    expect(r.outcome?.achievements.map((u) => u.id)).toContain('NITRO_NA_BANDEIRA');
  });

  it('estatísticas antes das conquistas: a MARATONA sai na corrida que passa dos 1.000 km', () => {
    expect(save.stats.totals.meters).toBeGreaterThanOrEqual(MARATHON_METERS);
    expect(save.stats.totals.races).toBe(1);
    expect(save.stats.players.map((p) => p.name)).toEqual(['P1']);
    expect(r.outcome?.achievements.map((u) => u.id)).toContain('MARATONA');
  });

  it('cada conquista vai para o save e para a Steam; o HUD recebe uma mensagem good; o save é gravado uma vez', () => {
    const ids = r.outcome?.achievements.map((u) => u.id) ?? [];
    expect(save.achievements).toEqual(ids);
    expect(fx.achievements).toEqual(ids);
    expect(fx.hud).toHaveLength(1);
    expect(fx.hud[0].seat).toBe(0);
    expect(fx.hud[0].message.kind).toBe('good');
    expect(fx.hud[0].message.ttl).toBe(3.5);
    expect(fx.hud[0].message.text).toMatch(/^CONQUISTAS?: /);
    expect(fx.persisted).toBe(1);
  });

  it('idempotente: chamar de novo devolve o mesmo resultado sem somar nem repetir efeito', () => {
    const before = JSON.stringify(save);
    expect(settleRace(save, r, options(fx))).toBe(first);
    expect(JSON.stringify(save)).toBe(before);
    expect(fx.persisted).toBe(1);
    expect(fx.hud).toHaveLength(1);
    expect(save.racesRun).toBe(1);
  });
});

describe('fechamento que lança no meio', () => {
  it('não soma de novo a cada quadro nem trava o resultado: a segunda chamada devolve o que foi fechado', () => {
    // Revisão: settleRace só marcava o resultado no fim. Com um save montado do DEFAULT_SAVE sem
    // saneamento (EMPTY_STATS congelado), recordRaceStats lança; a sessão chamava de novo a cada
    // quadro, racesRun subia toda vez e a tela de resultado nunca aparecia.
    const humans = [human(0)];
    const { state, track } = quickRace({ track: syntheticTrack([{ op: 'straight', length: 300 }]), humans, totalCars: 2, laps: 1 });
    run(state, track, 60 * 120);
    expect(state.phase).toBe('finished');
    const save = { ...sanitizeSave({}), stats: EMPTY_STATS };
    const r = target(state, track, humans);
    const fx: Effects = { achievements: [], hud: [], persisted: 0 };
    expect(() => settleRace(save, r, options(fx))).toThrow(TypeError);
    expect(save.racesRun).toBe(1);
    let again: RaceOutcome | null = null;
    expect(() => { again = settleRace(save, r, options(fx)); }).not.toThrow();
    expect(save.racesRun).toBe(1);
    expect(again).not.toBeNull();
    expect(again).toBe(r.outcome);
    // O que já tinha sido somado (recordes, contagem de corridas) é gravado mesmo assim.
    expect(fx.persisted).toBe(1);
  });
});

describe('a sessão usa os ganchos', () => {
  it('session.ts passa por stepObserved e settleRace (não reimplementa a ordem)', () => {
    const src = fs.readFileSync('src/game/session.ts', 'utf8');
    expect(src).toMatch(/stepObserved\(r, inputs\)/);
    expect(src).toMatch(/settleRace\(save, r, /);
    expect(src).not.toMatch(/\bstepRace\(/);
    expect(src).not.toMatch(/\brecordRaceStats\(|\bunlockAchievements\(/);
  });
});
