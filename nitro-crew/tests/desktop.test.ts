import { describe, it, expect } from 'vitest';
import fs from 'node:fs';
import { BRAZIL_REGIONS, CUPS } from '../src/core/data/cups';
import { ACHIEVEMENTS, getDesktop, isDesktop, setFullscreen } from '../src/game/desktop';
import { achievementDescription, evaluateAchievements, newTelemetry } from '../src/game/achievements';
import { registerStrings, setLanguage } from '../src/i18n';
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
  it('57 conquistas (24 de antes da onda G + 27 copas de estado + 5 regiões + passaporte completo), cada uma com descrição PT e EN e linha no desktop/README.md', () => {
    expect(ACHIEVEMENTS).toHaveLength(57);
    // A Copa Brasil deixou de existir (save antigo: COPA_BRASIL → COPA_BR_RJ, tests/migration-brasil.test.ts).
    expect(ACHIEVEMENTS.map((a) => a.id)).not.toContain('COPA_BRASIL');
    for (const r of BRAZIL_REGIONS) expect(ACHIEVEMENTS.map((a) => a.id)).toContain(`REGIAO_${r.id.toUpperCase()}`);
    const readme = fs.readFileSync('desktop/README.md', 'utf8');
    for (const lang of ['pt', 'en'] as const) {
      setLanguage(lang);
      for (const a of ACHIEVEMENTS) {
        const desc = achievementDescription(a.id);
        expect(desc, `${a.id} sem descrição em ${lang}`).not.toBe(`stats.achDesc.${a.id}`);
        expect(desc.length).toBeGreaterThan(10);
      }
    }
    setLanguage('pt');
    for (const a of ACHIEVEMENTS) expect(readme, `${a.id} fora da tabela do README`).toContain(`| \`${a.id}\` | ${a.pt} | ${a.en} |`);
  });
});

describe('descrição das conquistas de copa', () => {
  it('copa nova ganha a descrição pelo nome da copa, sem string por id', () => {
    // Revisão: o merge com mais copas trazia COPA_* sem stats.achDesc.<ID>, e a tela mostrava a chave crua.
    registerStrings('core', { pt: { 'cup.teste_merge': 'Copa Teste' }, en: { 'cup.teste_merge': 'Test Cup' } });
    CUPS.push({ id: 'teste_merge', name: 'Copa Teste', country: 'Teste', flag: '', trackIds: ['copacabana'], requires: null, stage: 'mundial' });
    try {
      setLanguage('pt');
      expect(achievementDescription('COPA_TESTE_MERGE')).toBe('Concluir a Copa Teste.');
      setLanguage('en');
      expect(achievementDescription('COPA_TESTE_MERGE')).toBe('Complete the Test Cup.');
    } finally {
      CUPS.pop();
      setLanguage('pt');
    }
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
    const got = evaluateAchievements(save, 'cup', state, state.results!, humans, tel, true, 'br_rj', 'campeao');
    for (const id of ['PRIMEIRA_VITORIA', 'MADRUGADA', 'EQUIPE_COMPLETA', 'SEM_BOX', 'NITRO_TRIPLO', 'EMPURRAO', 'VOLTA_PERFEITA', 'COPA_BR_RJ', 'CAMPEAO']) expect(got, id).toContain(id);
    for (const id of got) expect(ACHIEVEMENTS.some((a) => a.id === id), `${id} não está na lista da Steam`).toBe(true);
    // Já desbloqueadas não voltam.
    save.achievements = got;
    expect(evaluateAchievements(save, 'cup', state, state.results!, humans, tel, true, 'br_rj', 'campeao')).toEqual([]);
  });
  it('passaporte: a região inteira carimbada dá REGIAO_<REGIÃO>; os 27 estados, PASSAPORTE_COMPLETO', () => {
    const humans = [human(0)];
    const { state, track } = quickRace({ track: syntheticTrack([{ op: 'straight', length: 300 }]), totalCars: 2, laps: 1, humans });
    run(state, track, 60 * 30);
    const ev = (stamps: string[], cup: string) => evaluateAchievements({ ...DEFAULT_SAVE, achievements: [], stamps }, 'cup', state, state.results!, humans, newTelemetry(), false, cup, 'amador');
    const sul = BRAZIL_REGIONS.find((r) => r.id === 'sul')!.states;
    // A sessão carimba antes de avaliar (raceEnd.ts → markCupCompleted); o carimbo da copa recém-vencida conta mesmo assim.
    expect(ev(['PR', 'SC'], 'br_rs')).toContain('REGIAO_SUL');
    expect(ev([...sul], 'br_rs')).toContain('REGIAO_SUL');
    expect(ev(['PR'], 'br_rs')).not.toContain('REGIAO_SUL');
    expect(ev(['PR', 'SC'], 'br_rs')).not.toContain('PASSAPORTE_COMPLETO');
    const all = BRAZIL_REGIONS.flatMap((r) => r.states);
    const got = ev(all.filter((s) => s !== 'TO'), 'br_to');
    expect(got).toContain('REGIAO_NORTE');
    expect(got).toContain('PASSAPORTE_COMPLETO');
    // Copa do Mundial não carimba nem fecha região.
    expect(ev(all, 'eua').filter((id) => id.startsWith('REGIAO_') || id === 'PASSAPORTE_COMPLETO')).toEqual([]);
  });
  it('contra-relógio não dá vitória nem "sem box"', () => {
    const humans = [human(0)];
    const { state, track } = quickRace({ track: syntheticTrack([{ op: 'straight', length: 300 }]), totalCars: 1, laps: 1, humans, timeTrial: true });
    run(state, track, 60 * 30);
    const got = evaluateAchievements({ ...DEFAULT_SAVE, achievements: [] }, 'timetrial', state, state.results!, humans, newTelemetry(), false, null, 'amador');
    expect(got).toEqual([]);
  });
});
