// Ritmo da IA na carreira (onda K, frente K3): RaceConfig.aiPace, em degraus de dificuldade, interpolando
// DIFFICULTY_SPEED/DIFFICULTY_SKILL (src/core/sim/ai.ts). Ausente ou 0 = a corrida de antes, bit a bit.
import { describe, expect, it } from 'vitest';
import { CAREER_AI_PACE_POINTS, careerAiPace, newCareer } from '../src/core/career';
import { CUPS } from '../src/core/data/cups';
import { aiInput, DIFFICULTY_SKILL, DIFFICULTY_SPEED, pacedSkill, pacedSpeed } from '../src/core/sim/ai';
import { deserializeRace, serializeRace } from '../src/core/serialize';
import { createRace } from '../src/core/sim/race';
import type { Difficulty, RaceConfig } from '../src/core/types';
import { createCareerSession, type CareerHost } from '../src/game/career-session';
import { DEFAULT_SAVE, DEFAULT_SETTINGS, type Menus, type SaveData } from '../src/game/contracts';
import { human, humanCar, NO_ASSISTS, quickRace, run, skipCountdown, syntheticTrack } from './helpers';

const DIFFICULTIES: Difficulty[] = ['amador', 'profissional', 'campeao'];

describe('ritmo da IA (RaceConfig.aiPace)', () => {
  it('ritmo 0 ou ausente devolve as tabelas de hoje, sem conta', () => {
    for (const d of DIFFICULTIES) {
      expect(pacedSpeed(d)).toBe(DIFFICULTY_SPEED[d]);
      expect(pacedSpeed(d, 0)).toBe(DIFFICULTY_SPEED[d]);
      expect(pacedSkill(d)).toBe(DIFFICULTY_SKILL[d]);
    }
  });

  it('um degrau é a dificuldade vizinha, meio degrau é o meio, e fica preso entre amador e campeão', () => {
    expect(pacedSpeed('profissional', 1)).toBeCloseTo(DIFFICULTY_SPEED.campeao, 12);
    expect(pacedSkill('profissional', 1)[1]).toBeCloseTo(DIFFICULTY_SKILL.campeao[1], 12);
    expect(pacedSpeed('profissional', -1)).toBeCloseTo(DIFFICULTY_SPEED.amador, 12);
    expect(pacedSkill('profissional', -1)[0]).toBeCloseTo(DIFFICULTY_SKILL.amador[0], 12);
    expect(pacedSpeed('profissional', 0.5)).toBeCloseTo(0.97, 12);
    expect(pacedSkill('profissional', 0.5)[0]).toBeCloseTo(0.91, 12);
    expect(pacedSkill('profissional', 0.5)[1]).toBeCloseTo(1.005, 12);
    expect(pacedSpeed('amador', 0.5)).toBeCloseTo(0.9, 12);
    expect(pacedSpeed('campeao', 0.8)).toBe(DIFFICULTY_SPEED.campeao);
    expect(pacedSpeed('amador', -0.5)).toBe(DIFFICULTY_SPEED.amador);
  });

  it('aiPace ausente e aiPace 0 dão a mesma corrida, bit a bit', () => {
    const base = quickRace({ totalCars: 8, seed: 5 });
    const zero = createRace({ ...base.state.config, aiPace: 0 }, base.track);
    run(base.state, base.track, 1500); run(zero, base.track, 1500);
    expect(JSON.stringify({ tick: zero.tick, rng: zero.rng, cars: zero.cars })).toBe(JSON.stringify({ tick: base.state.tick, rng: base.state.rng, cars: base.state.cars }));
  });

  it('deserializeRace: aiPace ausente continua ausente (o JSON não muda) e lixo vira ausente', () => {
    const json = serializeRace(quickRace({ seed: 3 }).state);
    expect(serializeRace(deserializeRace(json))).toBe(json);
    const bad = JSON.parse(json); bad.config.aiPace = null;
    expect('aiPace' in deserializeRace(JSON.stringify(bad)).config).toBe(false);
    const good = JSON.parse(json); good.config.aiPace = 0.4;
    expect(deserializeRace(JSON.stringify(good)).config.aiPace).toBe(0.4);
  });

  it('o ritmo muda só a faixa da habilidade: os sorteios do cérebro são os mesmos', () => {
    const { state, track } = quickRace({ totalCars: 8, seed: 5 });
    const paced = createRace({ ...state.config, aiPace: 0.5 }, track);
    const [lo, hi] = DIFFICULTY_SKILL.profissional;
    const [plo, phi] = pacedSkill('profissional', 0.5);
    let checked = 0;
    state.cars.forEach((car, i) => {
      const a = car.ai; const b = paced.cars[i].ai;
      if (car.seat >= 0 || !a || !b) return;
      expect(b.laneX).toBe(a.laneX);
      expect(b.lookahead).toBe(a.lookahead);
      expect(b.aggression).toBe(a.aggression);
      expect(b.skill).toBeCloseTo(plo + ((a.skill - lo) / (hi - lo)) * (phi - plo), 9);
      checked++;
    });
    expect(checked).toBe(7);
  });

  it('o ritmo vale só para a IA: o humano guiado pelo cérebro (o piloto-proxy da sonda) não acelera junto', () => {
    const track = syntheticTrack();
    const base = quickRace({ track, totalCars: 2, seed: 1, humans: [human(0)] });
    const state = createRace({ ...base.state.config, aiPace: 1 }, track);
    skipCountdown(state, track);
    const me = humanCar(state);
    const ai = state.cars.find((c) => c.seat < 0);
    if (!ai?.ai) throw new Error('sem IA');
    me.ai = { skill: 0.97, laneX: 0.6, laneUntil: 0, lookahead: 32, aggression: 0.5 };
    ai.ai = { skill: 0.97, laneX: -0.6, laneUntil: 0, lookahead: 32, aggression: 0 };
    for (const [c, x] of [[me, 0.6], [ai, -0.6]] as const) { c.z = me.z; c.x = x; c.progress = me.progress; c.speed = 0.94 * c.stats.topSpeed; }
    // Reta: alvo = habilidade × ritmo. IA: 0,97 × 1,0 (campeão) > 0,94 → acelera. Humano: 0,97 × 0,94 < 0,94 → tira o pé.
    expect(aiInput(state, track, ai).throttle).toBe(true);
    expect(aiInput(state, track, me).throttle).toBe(false);
  });
});

describe('ritmo da IA na carreira (careerAiPace)', () => {
  const pace = (cupId: string) => careerAiPace({ ...newCareer([human(0)]), cupId });

  it('os pontos da rampa existem em CUPS, em ordem de copa', () => {
    const idx = CAREER_AI_PACE_POINTS.map(([id]) => CUPS.findIndex((c) => c.id === id));
    expect(idx.every((i) => i >= 0), JSON.stringify(idx)).toBe(true);
    for (let k = 1; k < idx.length; k++) expect(idx[k]).toBeGreaterThan(idx[k - 1]);
  });

  it('rampa provisória: 0 do RJ ao ES, sobe a 0,1 no RS, 0,4 do DF ao AC, 0,3 no TO, 0,2 no Mundial', () => {
    for (const id of ['br_rj', 'br_sp', 'br_mg', 'br_es']) expect(pace(id)).toBe(0);
    expect(pace('br_pr')).toBeGreaterThan(0);
    expect(pace('br_sc')).toBeGreaterThan(pace('br_pr'));
    expect(pace('br_rs')).toBeCloseTo(0.1, 12);
    const df = CUPS.findIndex((c) => c.id === 'br_df'); const ac = CUPS.findIndex((c) => c.id === 'br_ac');
    for (let i = df; i <= ac; i++) expect(pace(CUPS[i].id), CUPS[i].id).toBeCloseTo(0.4, 12);
    expect(pace('br_to')).toBeCloseTo(0.3, 12);
    for (const c of CUPS.filter((x) => x.stage === 'mundial')) expect(pace(c.id), c.id).toBeCloseTo(0.2, 12);
  });

  it('lista de copas sem nenhum ponto da rampa: ritmo 0', () => {
    expect(careerAiPace({ ...newCareer([human(0)]), cupId: 'outra' }, [{ ...CUPS[0], id: 'outra' }])).toBe(0);
  });

  it('a corrida da carreira leva o ritmo da copa na config', () => {
    const career = { ...newCareer([human(0)]), cupId: 'br_pb' };
    const save: SaveData = { ...structuredClone(DEFAULT_SAVE), career };
    let config: RaceConfig | null = null;
    const host: CareerHost = {
      save, settings: structuredClone(DEFAULT_SETTINGS), menus: { show() {}, hide() {} } as unknown as Menus, input: { seatDevice: () => null, bindSeat() {}, unbindSeat() {} },
      baseConfig: (trackId, laps, humans, seed) => ({ trackId, laps, humans, totalCars: 20, difficulty: 'profissional', manualGear: false, assists: NO_ASSISTS, seed }),
      beginRace: (c) => { config = c; }, toIdle() {}, randomSeed: () => 42, persist() {},
    };
    createCareerSession(host).race();
    expect((config as RaceConfig | null)?.aiPace).toBeCloseTo(0.4, 12);
  });
});
