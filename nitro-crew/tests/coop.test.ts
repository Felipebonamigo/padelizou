import { describe, it, expect } from 'vitest';
import { NITRO_PER_RACE, TEAM_DRAFT_TOP_MULT, DRAFT_TOP_MULT } from '../src/core/constants';
import { carDef } from '../src/core/data/cars';
import { computeModifiers } from '../src/core/sim/coop';
import { effectiveTopSpeed } from '../src/core/sim/physics';
import { NEUTRAL_INPUT } from '../src/core/types';
import { ALL_ASSISTS, NO_ASSISTS, human, humanCar, quickRace, run, skipCountdown, syntheticTrack } from './helpers';

describe('cooperativo', () => {
  it('nitro compartilhado: a equipe tem um cofre único que qualquer um gasta', () => {
    const { state, track } = quickRace({ track: syntheticTrack(), totalCars: 2, humans: [human(0), human(1)], assists: ALL_ASSISTS });
    expect(state.teamNitro[0]).toBe(NITRO_PER_RACE * 2);
    run(state, track, 60 * 5);
    run(state, track, 1, (_, seat) => ({ ...NEUTRAL_INPUT, throttle: true, nitro: seat === 1 }));
    expect(state.teamNitro[0]).toBe(NITRO_PER_RACE * 2 - 1);
    expect(humanCar(state, 1).nitroTicks).toBeGreaterThan(0);
    expect(humanCar(state, 0).nitroLeft).toBe(NITRO_PER_RACE); // a carga individual não é tocada
  });

  it('sem o cofre, cada um gasta as próprias cargas', () => {
    const { state, track } = quickRace({ track: syntheticTrack(), totalCars: 2, humans: [human(0), human(1)], assists: NO_ASSISTS });
    run(state, track, 60 * 5);
    run(state, track, 1, (_, seat) => ({ ...NEUTRAL_INPUT, throttle: true, nitro: seat === 1 }));
    expect(humanCar(state, 1).nitroLeft).toBe(NITRO_PER_RACE - 1);
    expect(state.teamNitro[0]).toBeUndefined();
  });

  it('empurrão: companheiro que passa perto de um parado devolve velocidade a ele', () => {
    const { state, track } = quickRace({ track: syntheticTrack(), totalCars: 2, humans: [human(0), human(1)], assists: ALL_ASSISTS });
    skipCountdown(state, track);
    const a = humanCar(state, 0); const b = humanCar(state, 1);
    a.speed = 0; a.z = 5000; a.x = 0;
    b.speed = carDef(b.carId).topSpeed; b.z = 4500; b.x = 0.3;
    let towed = false;
    for (let i = 0; i < 60 && !towed; i++) {
      run(state, track, 1, (_, seat) => ({ ...NEUTRAL_INPUT, throttle: seat === 1 }));
      towed = state.events.some((e) => e.type === 'tow');
    }
    expect(towed).toBe(true);
    expect(a.speed).toBeGreaterThan(carDef(a.carId).topSpeed * 0.5);
  });

  it('sem a assistência, não há empurrão', () => {
    const { state, track } = quickRace({ track: syntheticTrack(), totalCars: 2, humans: [human(0), human(1)], assists: NO_ASSISTS });
    skipCountdown(state, track);
    const a = humanCar(state, 0); const b = humanCar(state, 1);
    a.speed = 0; a.z = 5000; b.speed = carDef(b.carId).topSpeed; b.z = 4500; b.x = 0.3;
    run(state, track, 60, (_, seat) => ({ ...NEUTRAL_INPUT, throttle: seat === 1 }));
    expect(a.speed).toBe(0);
  });

  it('vácuo de equipe rende mais que vácuo comum, e só entre companheiros humanos', () => {
    const mk = (assists: typeof ALL_ASSISTS, teamB: number) => {
      const r = quickRace({ track: syntheticTrack(), totalCars: 2, humans: [human(0), human(1, teamB)], assists });
      skipCountdown(r.state, r.track);
      const a = humanCar(r.state, 0); const b = humanCar(r.state, 1);
      a.z = 4000; a.x = 0; a.speed = 5000; b.z = 4600; b.x = 0.1; b.speed = 5000;
      return { ...r, a };
    };
    const team = mk(ALL_ASSISTS, 0);
    const mods = computeModifiers(team.state, team.track, team.a);
    expect(mods.draft && mods.teamDraft).toBe(true);
    expect(effectiveTopSpeed(team.a, carDef(team.a.carId), team.state, mods)).toBeCloseTo(carDef(team.a.carId).topSpeed * TEAM_DRAFT_TOP_MULT, 3);
    const rival = mk(ALL_ASSISTS, 1);
    const m2 = computeModifiers(rival.state, rival.track, rival.a);
    expect(m2.draft).toBe(true); expect(m2.teamDraft).toBe(false);
    expect(effectiveTopSpeed(rival.a, carDef(rival.a.carId), rival.state, m2)).toBeCloseTo(carDef(rival.a.carId).topSpeed * DRAFT_TOP_MULT, 3);
  });

  it('elástico: só o último humano da equipe, e só quando fica longe de todos os companheiros', () => {
    const { state, track } = quickRace({ track: syntheticTrack(), totalCars: 3, humans: [human(0), human(1), human(2)], assists: ALL_ASSISTS });
    skipCountdown(state, track);
    const [a, b, c] = [0, 1, 2].map((s) => humanCar(state, s));
    a.z = 1000; b.z = 30000; c.z = 40000;
    run(state, track, 1);
    expect(computeModifiers(state, track, a).catchup).toBe(true);
    expect(computeModifiers(state, track, b).catchup).toBe(false);
    expect(computeModifiers(state, track, c).catchup).toBe(false);
    b.z = 3000; run(state, track, 1);
    expect(computeModifiers(state, track, a).catchup).toBe(false);
  });
});
