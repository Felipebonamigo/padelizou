import { describe, it, expect } from 'vitest';
import { CUPS } from '../src/core/data/cups';
import { ACHIEVEMENTS, getDesktop, isDesktop, setFullscreen } from '../src/game/desktop';
import { evaluateAchievements, newTelemetry } from '../src/game/achievements';
import { DEFAULT_SAVE } from '../src/game/contracts';
import { human, quickRace, run, syntheticTrack } from './helpers';

describe('ponte com o Electron', () => {
  it('fora do Electron não há ponte e a tela cheia não estoura', async () => {
    expect(getDesktop()).toBeNull();
    expect(isDesktop()).toBe(false);
    await expect(setFullscreen(true)).resolves.toBeUndefined();
  });
  it('as conquistas têm ids únicos, PT e EN, e cobrem as copas', () => {
    const ids = ACHIEVEMENTS.map((a) => a.id);
    expect(new Set(ids).size).toBe(ids.length);
    for (const a of ACHIEVEMENTS) { expect(a.pt.length).toBeGreaterThan(3); expect(a.en.length).toBeGreaterThan(3); }
    // A regra dá `COPA_${cupId.toUpperCase()}` ao concluir a copa: cada copa de CUPS precisa da sua.
    for (const cup of CUPS) expect(ids, cup.id).toContain(`COPA_${cup.id.toUpperCase()}`);
  });
  it('ids de conquista são nomes de API aceitos pela Steam (A–Z, 0–9, _)', () => {
    for (const a of ACHIEVEMENTS) expect(a.id).toMatch(/^[A-Z][A-Z0-9_]*$/);
  });
});

describe('regras das conquistas', () => {
  it('vitória, equipe completa, sem box, nitro triplo, empurrão e copa', () => {
    const humans = [human(0), human(1), human(2), human(3)];
    const { state, track } = quickRace({ track: syntheticTrack([{ op: 'straight', length: 300 }]), totalCars: 4, laps: 1, humans });
    run(state, track, 60 * 30);
    expect(state.phase).toBe('finished');
    const tel = newTelemetry();
    tel.maxNitrosInLap.set(0, 3); tel.gaveTow.add(1); tel.perfectLap.add(2);
    const save = { ...DEFAULT_SAVE, achievements: [] as string[] };
    const got = evaluateAchievements(save, 'cup', state, state.results!, humans, tel, true, 'brasil', 'campeao');
    for (const id of ['PRIMEIRA_VITORIA', 'MADRUGADA', 'EQUIPE_COMPLETA', 'SEM_BOX', 'NITRO_TRIPLO', 'EMPURRAO', 'VOLTA_PERFEITA', 'COPA_BRASIL', 'CAMPEAO']) expect(got, id).toContain(id);
    for (const id of got) expect(ACHIEVEMENTS.some((a) => a.id === id), `${id} não está na lista da Steam`).toBe(true);
    // Já desbloqueadas não voltam.
    save.achievements = got;
    expect(evaluateAchievements(save, 'cup', state, state.results!, humans, tel, true, 'brasil', 'campeao')).toEqual([]);
  });
  it('contra-relógio não dá vitória nem "sem box"', () => {
    const humans = [human(0)];
    const { state, track } = quickRace({ track: syntheticTrack([{ op: 'straight', length: 300 }]), totalCars: 1, laps: 1, humans, timeTrial: true });
    run(state, track, 60 * 30);
    const got = evaluateAchievements({ ...DEFAULT_SAVE, achievements: [] }, 'timetrial', state, state.results!, humans, newTelemetry(), false, null, 'amador');
    expect(got).toEqual([]);
  });
});
