// Rivais com personalidade (passo 3.4, docs/RIVAIS.md): as quatro personalidades mudam métricas medidas
// em corridas simuladas com semente fixa, o bloqueador desiste no limite, o rival é estável por copa,
// entra no grid e vale na carreira, e as falas existem em PT e EN.
import { describe, expect, it } from 'vitest';
import {
  BLOCK_ALONGSIDE, BLOCK_LATERAL_RATE, BLOCK_MAX_TICKS, BLOCK_MAX_X, BLOCK_REST_TICKS, CAR_HALF_WIDTH, RIVAL_SKILL_BONUS,
  SEGMENT_LENGTH, TICK_RATE,
} from '../src/core/constants';
import { createChampionship, applyRaceResult } from '../src/core/championship';
import { newCareer } from '../src/core/career';
import { CUPS } from '../src/core/data/cups';
import { AI_DRIVERS, CUP_RIVALS, DRIVER_PERSONALITY, NEUTRAL_TUNING, PERSONALITY_TUNING, personalityOf, rosterOffsetWith } from '../src/core/data/drivers';
import { wrappedDelta } from '../src/core/sim/collisions';
import { blockLane, chaserBehind, curveZoneStart, tuningOf } from '../src/core/sim/personality';
import { createRace, stepRace } from '../src/core/sim/race';
import { serializeRace } from '../src/core/serialize';
import { maxCurveAhead } from '../src/core/track/builder';
import { getTrack } from '../src/core/track';
import type { AiBrain, CarState, HumanEntry, Personality, RaceConfig, RaceResultRow, RaceState, Track } from '../src/core/types';
import { unlockAchievements, newTelemetry } from '../src/game/achievements';
import { settleRace } from '../src/game/raceEnd';
import { sanitizeSave } from '../src/game/save';
import { createCareerSession, type CareerHost } from '../src/game/career-session';
import { DEFAULT_SAVE, DEFAULT_SETTINGS, type Menus, type SaveData } from '../src/game/contracts';
import {
  cupDuels, cupRival, duelLineKind, hasOwnLines, raceDuel, RIVAL_LINE_KINDS, rivalBeatenEveryRace, rivalLine, rivalRaceSummary,
  rivalStandingsSummary,
} from '../src/game/rivals';
import { setLanguage } from '../src/i18n';
import { ALL_ASSISTS, human, NO_ASSISTS, quickRace, syntheticTrack } from './helpers';

const PERSONALITIES: readonly Personality[] = ['clean', 'aggressive', 'blocker', 'erratic'];

// ───────────────────────────── Métricas por personalidade ─────────────────────────────

interface Metrics {
  /** Nitros nos primeiros 45 s de corrida (todos têm 3; o agressivo gasta antes). */
  earlyNitros: number;
  /** Nitros disparados com curva de 1 ou mais nos 40 segmentos à frente. */
  nitrosNearCurves: number;
  collisions: number;
  /** Trocas de faixa por conta própria ou para desviar (a faixa-alvo do cérebro mudando de uma vez). */
  laneChanges: number;
  /** Fração das amostras (carro da IA × tick) na grama. */
  grass: number;
  /** Ticks com um carro da IA logo à frente do humano, na mesma faixa (0 a 3 segmentos). */
  aheadOfHuman: number;
  /** Maior investida de bloqueio vista (ticks). */
  maxBlock: number;
  meanLap: number;
}

/**
 * Todo o grid da IA com a mesma personalidade (a do teste), mesma semente e pista: só ela muda. O humano
 * é o piloto automático neutro, um pouco mais rápido que a IA, para ter alguém tentando passar.
 */
function measure(personality: Personality | null, trackId: string, seconds: number, seed = 5): Metrics {
  const track = getTrack(trackId);
  const config: RaceConfig = {
    trackId, laps: 9, humans: [human(0)], totalCars: 12, difficulty: 'profissional', manualGear: false, assists: NO_ASSISTS, seed,
  };
  const state = createRace(config, track);
  const me = state.cars.find((c) => c.seat === 0) as CarState;
  me.ai = { skill: 1.0, laneX: 0, laneUntil: 0, lookahead: 28, aggression: 0.5 };
  const ai = state.cars.filter((c) => c.seat < 0);
  for (const c of ai) if (c.ai) { if (personality) c.ai.personality = personality; else delete c.ai.personality; }
  const m: Metrics = { earlyNitros: 0, nitrosNearCurves: 0, collisions: 0, laneChanges: 0, grass: 0, aheadOfHuman: 0, maxBlock: 0, meanLap: 0 };
  const lanes = ai.map((c) => c.ai?.laneX ?? 0);
  let samples = 0; let off = 0;
  for (let i = 0; i < TICK_RATE * seconds; i++) {
    stepRace(state, track, []);
    const racing = state.tick - state.startTick;
    for (const e of state.events) {
      if (e.type === 'collision') m.collisions++;
      if (e.type !== 'nitro' || state.cars[e.carId].seat >= 0) continue;
      if (racing < TICK_RATE * 45) m.earlyNitros++;
      if (maxCurveAhead(track, state.cars[e.carId].z, 40) >= 1) m.nitrosNearCurves++;
    }
    if (state.phase !== 'racing') continue;
    ai.forEach((c, k) => {
      const brain = c.ai as AiBrain;
      // O bloqueador anda a faixa aos poucos: só conta troca de uma vez (desvio ou troca por conta própria).
      if (Math.abs(brain.laneX - lanes[k]) > BLOCK_LATERAL_RATE * 1.01) m.laneChanges++;
      lanes[k] = brain.laneX;
      m.maxBlock = Math.max(m.maxBlock, brain.blockTicks ?? 0);
      samples++;
      if (Math.abs(c.x) > 1.05) off++;
      const d = wrappedDelta(c.z, me.z, track.length);
      if (d > 0 && d < SEGMENT_LENGTH * 3 && Math.abs(c.x - me.x) < CAR_HALF_WIDTH * 2) m.aheadOfHuman++;
    });
  }
  m.grass = off / Math.max(1, samples);
  const laps = ai.flatMap((c) => c.lapTicks.slice(1));
  m.meanLap = laps.length ? laps.reduce((a, b) => a + b, 0) / laps.length / TICK_RATE : 0;
  return m;
}

describe('personalidades: dados', () => {
  it('todo piloto da IA tem personalidade fixa, e as quatro existem', () => {
    for (const name of AI_DRIVERS) expect(personalityOf(name), name).not.toBeNull();
    expect(Object.keys(DRIVER_PERSONALITY).sort()).toEqual([...AI_DRIVERS].sort());
    for (const p of PERSONALITIES) expect(Object.values(DRIVER_PERSONALITY).filter((x) => x === p).length, p).toBeGreaterThanOrEqual(3);
    expect(personalityOf('P1')).toBeNull();
  });

  it('cérebro sem personalidade (piloto automático, IA que assume o assento) pilota como o neutro de antes', () => {
    expect(tuningOf({ skill: 1, laneX: 0, laneUntil: 0, lookahead: 25, aggression: 0 })).toBe(NEUTRAL_TUNING);
    for (const p of PERSONALITIES) expect(tuningOf({ skill: 1, laneX: 0, laneUntil: 0, lookahead: 25, aggression: 0, personality: p })).toBe(PERSONALITY_TUNING[p]);
  });

  it('createRace dá a cada piloto a personalidade dele, e o humano fica sem', () => {
    const { state } = quickRace({ totalCars: 20, seed: 3 });
    for (const c of state.cars) {
      if (c.seat >= 0) expect(c.ai).toBeNull();
      else expect(c.ai?.personality, c.name).toBe(DRIVER_PERSONALITY[c.name]);
    }
  });
});

describe('personalidades: efeitos medidos (semente fixa)', () => {
  // Passo Alpino (dificuldade 5, curvas de até 6) com 11 carros da IA, 150 s.
  const results = new Map<Personality | 'neutral', Metrics>();
  const get = (p: Personality | 'neutral'): Metrics => {
    let m = results.get(p);
    if (!m) { m = measure(p === 'neutral' ? null : p, 'passo_alpino', 150); results.set(p, m); }
    return m;
  };

  it('agressivo: mais nitro cedo, mais trocas de faixa e mais batidas que o limpo', () => {
    const a = get('aggressive'); const c = get('clean');
    expect(a.earlyNitros).toBeGreaterThan(c.earlyNitros);
    expect(a.laneChanges).toBeGreaterThan(c.laneChanges * 1.5);
    expect(a.collisions).toBeGreaterThan(c.collisions * 1.5);
  }, 60_000);

  it('limpo: nitro só em reta longa e quase nenhuma batida', () => {
    const c = get('clean'); const n = get('neutral');
    expect(c.nitrosNearCurves).toBe(0);
    expect(c.collisions).toBeLessThan(n.collisions);
    expect(c.grass).toBeLessThan(0.005);
  }, 60_000);

  it('errático: vai à grama bem mais que os outros, sem virar um carro perdido', () => {
    const e = get('erratic');
    for (const p of ['neutral', 'clean', 'blocker'] as const) expect(e.grass, p).toBeGreaterThan(get(p).grass * 3 + 0.005);
    expect(e.grass).toBeLessThan(0.08);
  }, 60_000);

  it('bloqueador: fica à frente do humano por mais tempo, e nenhuma investida passa do limite', () => {
    const b = get('blocker');
    for (const p of ['neutral', 'clean'] as const) expect(b.aheadOfHuman, p).toBeGreaterThan(get(p).aheadOfHuman * 1.2);
    expect(b.maxBlock).toBeGreaterThan(0);
    expect(b.maxBlock).toBeLessThan(BLOCK_MAX_TICKS);
  }, 60_000);

  it('justo: o tempo médio de volta de cada personalidade fica a menos de 5% do piloto neutro', () => {
    const n = get('neutral').meanLap;
    expect(n).toBeGreaterThan(0);
    for (const p of PERSONALITIES) expect(Math.abs(get(p).meanLap - n) / n, p).toBeLessThan(0.05);
  }, 60_000);
});

// ───────────────────────────── Bloqueio ─────────────────────────────

/** Reta, um bloqueador em z=2000 e um humano `gap` unidades atrás, já correndo. */
function blockScene(gap: number, humanX: number): { state: RaceState; track: Track; blocker: CarState; chaser: CarState; brain: AiBrain } {
  const { state, track } = quickRace({ track: syntheticTrack(), humans: [human(0)], totalCars: 2, seed: 1 });
  state.phase = 'racing';
  const blocker = state.cars.find((c) => c.seat < 0) as CarState;
  const chaser = state.cars.find((c) => c.seat === 0) as CarState;
  const brain = blocker.ai as AiBrain;
  brain.personality = 'blocker';
  brain.laneX = 0;
  blocker.z = 2000; blocker.x = 0;
  chaser.z = 2000 - gap; chaser.x = humanX;
  return { state, track, blocker, chaser, brain };
}

describe('bloqueador', () => {
  const tune = PERSONALITY_TUNING.blocker;

  it('com um humano logo atrás, leva a faixa para a frente dele devagar e sem sair do asfalto', () => {
    const { state, track, blocker, brain } = blockScene(300, 0.85);
    let prev = brain.laneX;
    for (let i = 0; i < 120; i++) {
      const lane = blockLane(state, track, blocker, brain, tune);
      expect(lane).not.toBeNull();
      expect(Math.abs((lane ?? 0) - prev)).toBeLessThanOrEqual(BLOCK_LATERAL_RATE + 1e-9);
      prev = lane ?? prev;
      state.tick++;
    }
    expect(prev).toBeCloseTo(BLOCK_MAX_X, 5); // o humano está em 0,85: o bloqueador para em 0,7, no asfalto
  });

  it('desiste no limite de tempo, descansa e só depois volta a bloquear', () => {
    const { state, track, blocker, brain } = blockScene(300, 0.4);
    let blocked = 0;
    while (blockLane(state, track, blocker, brain, tune) !== null) { blocked++; state.tick++; expect(blocked).toBeLessThan(BLOCK_MAX_TICKS); }
    expect(blocked).toBe(BLOCK_MAX_TICKS - 1);
    const gaveUp = state.tick;
    for (; state.tick < gaveUp + BLOCK_REST_TICKS; state.tick++) expect(blockLane(state, track, blocker, brain, tune)).toBeNull();
    expect(blockLane(state, track, blocker, brain, tune)).not.toBeNull();
  });

  it('respeita o desvio: com o humano de bico do lado, não fecha', () => {
    const { state, track, blocker, brain } = blockScene(BLOCK_ALONGSIDE - 10, 0.3);
    expect(blockLane(state, track, blocker, brain, tune)).toBeNull();
    expect(brain.laneX).toBe(0);
  });

  it('só vigia humanos: outro carro da IA atrás não é bloqueado', () => {
    const { state, track, blocker, chaser, brain } = blockScene(300, 0.3);
    chaser.seat = -1;
    expect(chaserBehind(state, track, blocker)).toBeNull();
    expect(blockLane(state, track, blocker, brain, tune)).toBeNull();
  });

  it('as outras personalidades nunca bloqueiam', () => {
    for (const p of ['clean', 'aggressive', 'erratic'] as const) {
      const { state, track, blocker, brain } = blockScene(300, 0.4);
      brain.personality = p;
      expect(blockLane(state, track, blocker, brain, PERSONALITY_TUNING[p]), p).toBeNull();
    }
  });
});

describe('erro de frenagem', () => {
  it('o trecho de curva começa onde a reta acaba', () => {
    const track = syntheticTrack([{ op: 'straight', length: 50 }, { op: 'curve', length: 40, curve: 4 }, { op: 'straight', length: 50 }]);
    const first = track.segments.findIndex((s) => Math.abs(s.curve) >= 1.5);
    expect(first).toBeGreaterThan(0);
    expect(curveZoneStart(track, first + 10)).toBe(first);
    expect(curveZoneStart(track, first)).toBe(first);
  });
});

// ───────────────────────────── Determinismo ─────────────────────────────

describe('determinismo com personalidades e rival', () => {
  it('duas corridas de copa iguais (mesma semente, mesmo rival) são idênticas', () => {
    const cfg = (): RaceConfig => ({
      trackId: 'monaco_noite', laps: 3, humans: [human(0), human(1, 0, 'trovao')], totalCars: 20, difficulty: 'campeao', manualGear: false,
      assists: ALL_ASSISTS, seed: 77, rosterSeed: 1234, rival: cupRival('europa').name,
    });
    const track = getTrack('monaco_noite');
    const a = createRace(cfg(), track); const b = createRace(cfg(), track);
    for (let i = 0; i < 3000; i++) { stepRace(a, track, []); stepRace(b, track, []); }
    expect(serializeRace(a)).toBe(serializeRace(b));
  }, 30_000);
});

// ───────────────────────────── Rival da copa ─────────────────────────────

describe('rival da copa', () => {
  it('é estável por copa, é um piloto do elenco e cada copa tem o seu', () => {
    const names = CUPS.map((c) => cupRival(c.id).name);
    expect(CUPS.map((c) => cupRival(c.id).name)).toEqual(names);
    expect(new Set(names).size).toBe(CUPS.length);
    for (const n of names) expect(AI_DRIVERS).toContain(n);
    for (const c of CUPS) expect(CUP_RIVALS[c.id], c.id).toBe(cupRival(c.id).name);
    // Copa nova sem rival escolhido: um piloto do elenco, sempre o mesmo.
    expect(cupRival('copa_nova').name).toBe(cupRival('copa_nova').name);
    expect(AI_DRIVERS).toContain(cupRival('copa_nova').name);
  });

  it('entra no grid da copa qualquer que seja o elenco sorteado, com o bônus leve de habilidade', () => {
    const track = getTrack('copacabana');
    const rival = cupRival('brasil').name;
    for (let rosterSeed = 0; rosterSeed < 40; rosterSeed++) {
      for (const totalCars of [4, 8, 20]) {
        const base: RaceConfig = { trackId: 'copacabana', laps: 2, humans: [human(0)], totalCars, difficulty: 'profissional', manualGear: false, assists: NO_ASSISTS, seed: 9, rosterSeed };
        const without = createRace(base, track);
        const withRival = createRace({ ...base, rival }, track);
        const car = withRival.cars.find((c) => c.name === rival);
        expect(car, `elenco ${rosterSeed}, ${totalCars} carros`).toBeDefined();
        expect(withRival.cars.filter((c) => c.seat < 0)).toHaveLength(totalCars - 1);
        // Mesmo sorteio de cérebros: o rival só ganha o bônus.
        const idx = withRival.cars.indexOf(car as CarState);
        expect((car as CarState).ai?.skill).toBeCloseTo((without.cars[idx].ai?.skill ?? 0) + RIVAL_SKILL_BONUS, 10);
        // Quem já estava no elenco continua onde estava.
        if (without.cars.some((c) => c.name === rival)) expect(withRival.cars.map((c) => c.name)).toEqual(without.cars.map((c) => c.name));
      }
    }
  });

  it('rosterOffsetWith: mantém o deslocamento se o piloto já está no elenco; senão o põe na última vaga', () => {
    const n = AI_DRIVERS.length;
    const idx = AI_DRIVERS.indexOf('Zé Turbo');
    expect(rosterOffsetWith(idx, 3, 'Zé Turbo')).toBe(idx);
    expect(rosterOffsetWith((idx - 2 + n) % n, 3, 'Zé Turbo')).toBe((idx - 2 + n) % n);
    expect(rosterOffsetWith((idx + 1) % n, 3, 'Zé Turbo')).toBe((idx - 2 + n) % n);
    expect(rosterOffsetWith(4, 3, undefined)).toBe(4);
    expect(rosterOffsetWith(4, 0, 'Zé Turbo')).toBe(4);
    expect(rosterOffsetWith(4, 3, 'Ninguém')).toBe(4);
  });

  it('vale também na carreira: a corrida da copa da carreira leva o mesmo rival', () => {
    const save: SaveData = { ...structuredClone(DEFAULT_SAVE), career: newCareer([human(0)]) };
    let config: RaceConfig | null = null;
    const host: CareerHost = {
      save, settings: structuredClone(DEFAULT_SETTINGS), menus: { show() {}, hide() {} } as unknown as Menus, input: { seatDevice: () => null, bindSeat() {}, unbindSeat() {} },
      baseConfig: (trackId, laps, humans, seed) => ({ trackId, laps, humans, totalCars: 20, difficulty: 'profissional', manualGear: false, assists: NO_ASSISTS, seed }),
      beginRace: (c) => { config = c; }, toIdle() {}, randomSeed: () => 42, persist() {},
    };
    createCareerSession(host).race();
    expect((config as RaceConfig | null)?.rival).toBe(cupRival(CUPS[0].id).name);
  });

  it('falas: apresentação, provocação, respeito e revanche próprias de cada rival, em PT e EN', () => {
    for (const lang of ['pt', 'en'] as const) {
      setLanguage(lang);
      for (const cup of CUPS) {
        const r = cupRival(cup.id);
        expect(hasOwnLines(r), `${r.name} (${lang})`).toBe(true);
        const lines = RIVAL_LINE_KINDS.map((k) => rivalLine(r, k));
        for (const l of lines) expect(l.length).toBeGreaterThan(10);
        expect(new Set(lines).size).toBe(lines.length);
      }
      // Piloto sem falas próprias usa as da personalidade.
      for (const name of AI_DRIVERS) {
        const r = { name, personality: personalityOf(name), key: 'sem_falas' };
        for (const k of RIVAL_LINE_KINDS) expect(rivalLine(r, k)).not.toMatch(/^rivals\./);
      }
    }
    setLanguage('pt');
    const ze = cupRival('brasil');
    const pt = rivalLine(ze, 'taunt');
    setLanguage('en');
    expect(rivalLine(ze, 'taunt')).not.toBe(pt);
    setLanguage('pt');
  });
});

// ───────────────────────────── Duelo, resultado e conquista ─────────────────────────────

function row(name: string, seat: number, position: number): RaceResultRow {
  return { carId: position, seat, name, teamId: seat >= 0 ? 0 : 100, carDefId: 'falcao', position, finished: true, totalTicks: 1000 + position, bestLapTicks: 300, points: 0 };
}

/** Copa Brasil com um humano e o rival, nas posições dadas corrida a corrida ([humano, rival]). */
function cupWith(positions: Array<[number, number]>): { champ: ReturnType<typeof createChampionship>; humans: HumanEntry[]; results: RaceResultRow[][] } {
  const humans = [human(0)];
  const champ = createChampionship('brasil', humans);
  const rival = cupRival('brasil').name;
  const all: RaceResultRow[][] = [];
  for (const [hp, rp] of positions) {
    const others = AI_DRIVERS.filter((n) => n !== rival).slice(0, 8);
    const results: RaceResultRow[] = [row('P1', 0, hp), row(rival, -1, rp)];
    let pos = 1;
    for (const n of others) { while (pos === hp || pos === rp) pos++; results.push(row(n, -1, pos++)); }
    applyRaceResult(champ, results, humans);
    all.push(results);
  }
  return { champ, humans, results: all };
}

describe('duelo com o rival', () => {
  it('provocação quando ele chega na frente, respeito quando fica atrás, revanche quando devolve a derrota', () => {
    expect(duelLineKind(true, undefined)).toBe('taunt');
    expect(duelLineKind(true, true)).toBe('taunt');
    expect(duelLineKind(true, false)).toBe('revenge');
    expect(duelLineKind(false, true)).toBe('respect');
    const { champ, results } = cupWith([[2, 5], [4, 1]]);
    expect(cupDuels(champ)).toEqual([false, true]);
    setLanguage('pt');
    const s = rivalRaceSummary(champ, results[1]);
    expect(s?.kind).toBe('revenge');
    expect(s?.title).toBe('Rival: Zé Turbo — 1º');
    expect(s?.line).toBe(rivalLine(cupRival('brasil'), 'revenge'));
    setLanguage('en');
    expect(rivalRaceSummary(champ, results[1])?.title).toBe('Rival: Zé Turbo — 1st');
    setLanguage('pt');
  });

  it('classificação: posição do rival na copa e a fala conforme quem está à frente nos pontos', () => {
    const behind = rivalStandingsSummary(cupWith([[1, 6]]).champ);
    expect(behind?.rivalAhead).toBe(false);
    expect(behind?.kind).toBe('respect');
    expect(behind?.title).toMatch(/^Rival: Zé Turbo — \d+º na copa$/);
    const ahead = rivalStandingsSummary(cupWith([[7, 1]]).champ);
    expect(ahead?.rivalAhead).toBe(true);
    expect(ahead?.position).toBe(1);
    expect(ahead?.kind).toBe('taunt');
  });

  it('sem o rival no grid (ou sem humano) não há duelo', () => {
    expect(raceDuel([row('P1', 0, 1), row('Outro', -1, 2)], 'Zé Turbo')).toBeNull();
    expect(raceDuel([row('Zé Turbo', -1, 1)], 'Zé Turbo')).toBeNull();
    // Co-op: vale o melhor humano.
    expect(raceDuel([row('P1', 0, 5), row('P2', 1, 2), row('Zé Turbo', -1, 3)], 'Zé Turbo')?.rivalAhead).toBe(false);
  });

  it('RIVAL_DERROTADO: à frente do rival em todas as corridas da copa, e só na copa concluída', () => {
    const won = cupWith([[1, 2], [3, 4], [2, 9], [5, 6]]);
    expect(rivalBeatenEveryRace(won.champ)).toBe(true);
    const lostOne = cupWith([[1, 2], [3, 4], [5, 2], [5, 6]]);
    expect(rivalBeatenEveryRace(lostOne.champ)).toBe(false);
    expect(rivalBeatenEveryRace(cupWith([[1, 2], [3, 4]]).champ)).toBe(false);

    const { state } = quickRace({ totalCars: 4, seed: 2 });
    const save = (): SaveData => ({ ...structuredClone(DEFAULT_SAVE), achievements: [] });
    const unlock = (c: typeof won) => unlockAchievements(save(), 'cup', state, c.results[3], c.humans, newTelemetry(), false, 'brasil', 'profissional', c.champ).map((u) => u.id);
    expect(unlock(won)).toContain('RIVAL_DERROTADO');
    expect(unlock(lostOne)).not.toContain('RIVAL_DERROTADO');
    // Sem a copa (versão antiga do gancho): nada.
    expect(unlockAchievements(save(), 'cup', state, won.results[3], won.humans, newTelemetry(), false, 'brasil', 'profissional').map((u) => u.id)).not.toContain('RIVAL_DERROTADO');
  });

  it('RIVAL_DERROTADO na carreira: o fechamento lê a copa depois de careerFinished (careerChamp)', () => {
    const won = cupWith([[1, 2], [3, 4], [2, 9], [5, 6]]);
    const { state, track } = quickRace({ totalCars: 4, seed: 2 });
    state.results = won.results[3];
    const unlocked: string[] = [];
    let after: typeof won.champ | null = null;
    settleRace(sanitizeSave({}), { state, track, mode: 'career', humans: won.humans, telemetry: newTelemetry(), outcome: null }, {
      champ: null, difficulty: 'profissional', hudTtl: 1,
      effects: { achievement: (id) => { unlocked.push(id); }, hud() {}, persist() {} },
      // Como a sessão: careerFinished devolve a copa concluída e deixa a copa somada à mão da sessão.
      careerFinished: () => { after = won.champ; return 'brasil'; },
      careerChamp: () => after,
    });
    expect(unlocked).toContain('RIVAL_DERROTADO');
  });
});
