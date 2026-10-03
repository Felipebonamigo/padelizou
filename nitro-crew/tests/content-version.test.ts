// Versão do conteúdo (src/game/content-version.ts): a impressão de uma volta — física, carro de fábrica e
// traçado da pista — decide se um fantasma ou um recorde é desta versão do jogo. A do online continua a mesma.
import { describe, expect, it } from 'vitest';
import { GEAR_TOP } from '../src/core/constants';
import { CARS } from '../src/core/data/cars';
import { getTrack } from '../src/core/track';
import type { HumanEntry, RaceResultRow, Segment, SpriteRef } from '../src/core/types';
import {
  onlineFingerprint, contentFingerprint, fingerprintContent, lapContent, lapFingerprint, PHYSICS_REVISION, trackLength,
} from '../src/game/content-version';
import type { BestLap } from '../src/game/contracts';
import * as online from '../src/game/online-session';
import { recordFromOtherVersion, recordRaceResults, sanitizeSave } from '../src/game/save';

/** Muda um valor, confere e desfaz (o conteúdo é global: nada pode vazar para os outros testes). */
function withTweak(apply: () => () => void, check: () => void): void {
  const undo = apply();
  try { check(); } finally { undo(); }
}

function set<T extends object, K extends keyof T>(obj: T, key: K, value: T[K]): () => void {
  const old = obj[key];
  obj[key] = value;
  return () => { obj[key] = old; };
}

function falcao() {
  const car = CARS.find((c) => c.id === 'falcao');
  if (!car) throw new Error('falcao');
  return car;
}

function firstSegment(pred: (s: Segment) => boolean): Segment {
  const seg = getTrack('copacabana').segments.find(pred);
  if (!seg) throw new Error('segmento');
  return seg;
}

function firstSprite(solid: boolean): SpriteRef {
  const sp = getTrack('copacabana').segments.flatMap((s) => s.sprites).find((s) => s.solid === solid);
  if (!sp) throw new Error('sprite');
  return sp;
}

describe('impressão de uma volta (pista + carro)', () => {
  it('8 hex e estável; muda de pista para pista — mesmo com o mesmo comprimento — e de carro para carro', () => {
    const fp = lapFingerprint('copacabana', 'falcao');
    expect(fp).toMatch(/^[0-9a-f]{8}$/);
    expect(lapFingerprint('copacabana', 'falcao')).toBe(fp);
    // Copacabana e Rota 66 têm o mesmo comprimento: só a impressão separa uma da outra.
    expect(trackLength('copacabana')).toBe(getTrack('copacabana').length);
    expect(trackLength('rota_66')).toBe(trackLength('copacabana'));
    expect(lapFingerprint('rota_66', 'falcao')).not.toBe(fp);
    expect(lapFingerprint('copacabana', 'trovao')).not.toBe(fp);
    expect(lapFingerprint('pista_que_nao_existe', 'falcao')).toBeNull();
    expect(lapFingerprint('copacabana', 'carro_que_nao_existe')).toBeNull();
    expect(trackLength('pista_que_nao_existe')).toBeNull();
    expect(lapContent('copacabana', 'falcao')).toMatchObject({ physics: PHYSICS_REVISION });
  });

  it('muda com o que muda a volta: constante da física, atributo do carro, curva, box, obstáculo sólido', () => {
    const fp = () => contentFingerprint(lapContent('copacabana', 'falcao'));
    const before = fp();
    const curve = firstSegment((s) => s.curve !== 0);
    const plain = firstSegment((s) => !s.pit);
    const solid = firstSprite(true);
    const tweaks: Array<[string, () => () => void]> = [
      ['constante (GEAR_TOP)', () => { const o = GEAR_TOP[0]; GEAR_TOP[0] = o + 0.01; return () => { GEAR_TOP[0] = o; }; }],
      ['aceleração do carro', () => set(falcao(), 'accel', falcao().accel + 1)],
      ['curva', () => set(curve, 'curve', curve.curve + 0.5)],
      ['box', () => set(plain, 'pit', true)],
      ['posição do obstáculo sólido', () => set(solid, 'x', solid.x + 0.1)],
      ['tamanho do obstáculo sólido', () => set(solid, 'scale', solid.scale * 1.2)],
    ];
    for (const [name, tweak] of tweaks) withTweak(tweak, () => expect(fp(), name).not.toBe(before));
    expect(fp()).toBe(before);
  });

  it('não muda com o que é só de exibição: nome, cor, texto e preço do carro, nome da pista, enfeite não sólido, zebra, relevo', () => {
    const fp = () => contentFingerprint(lapContent('copacabana', 'falcao'));
    const before = fp();
    const deco = firstSprite(false);
    const seg = getTrack('copacabana').segments[10];
    const tweaks: Array<[string, () => () => void]> = [
      ['nome do carro', () => set(falcao(), 'name', 'Falcão Novo')],
      ['cor do carro', () => set(falcao(), 'color', '#000000')],
      ['texto do carro', () => set(falcao(), 'blurb', 'outro')],
      ['preço do carro', () => set(falcao(), 'price', 999)],
      ['nome da pista', () => set(getTrack('copacabana').def, 'name', 'Outra Orla')],
      ['enfeite não sólido', () => set(deco, 'x', deco.x + 0.3)],
      ['variante do enfeite', () => set(deco, 'variant', deco.variant + 1)],
      ['zebra', () => set(seg, 'band', seg.band === 0 ? 1 : 0)],
      ['relevo', () => set(seg, 'y1', seg.y1 + 50)],
    ];
    for (const [name, tweak] of tweaks) withTweak(tweak, () => expect(fp(), name).toBe(before));
  });

  // Defeito (onda G, praças nas pistas de cidade): o builder deixou de pôr prédios sólidos em trechos das pistas de
  // cidade sem mudar a definição delas. O online só olhava as definições: um build velho e um novo entravam na mesma
  // sala e dessincronizavam na primeira batida. Obstáculo sólido construído muda a impressão do online.
  it('obstáculo sólido construído pelo builder muda a impressão do online', () => {
    const seg = getTrack('sampa_noite').segments.find((s) => s.sprites.some((sp) => sp.solid));
    if (!seg) throw new Error('sampa_noite sem obstáculo sólido');
    const before = contentFingerprint(fingerprintContent());
    withTweak(() => { const removed = seg.sprites.splice(0, seg.sprites.length); return () => { seg.sprites.push(...removed); }; },
      () => expect(contentFingerprint(fingerprintContent())).not.toBe(before));
  });

  it('a impressão do online é a mesma de antes, e o online-session só a reexporta', () => {
    expect(online.onlineFingerprint).toBe(onlineFingerprint);
    expect(online.contentFingerprint).toBe(contentFingerprint);
    expect(onlineFingerprint()).toBe(contentFingerprint(fingerprintContent()));
  });
});

function row(seat: number, totalTicks: number, bestLapTicks: number): RaceResultRow {
  return { carId: 100 + seat, seat, name: `P${seat + 1}`, teamId: 0, carDefId: 'falcao', position: seat + 1, finished: true, totalTicks, bestLapTicks, points: 0 };
}
const humans: HumanEntry[] = [
  { seat: 0, name: 'Fê', carId: 'trovao', teamId: 0, color: '#fff' },
  { seat: 1, name: 'Bia', carId: 'tornado', teamId: 0, color: '#fff' },
];

describe('recordes guardam a impressão (nenhum é apagado)', () => {
  it('recorde novo leva a impressão da pista e do carro de quem fez', () => {
    const save = sanitizeSave({});
    recordRaceResults(save, [row(0, 9100, 2800), row(1, 9000, 2900)], humans, 'copacabana', 3);
    expect(save.bestLaps.copacabana).toMatchObject({ ticks: 2800, carId: 'trovao', fp: lapFingerprint('copacabana', 'trovao') });
    expect(save.bestRaces['copacabana:3']).toMatchObject({ ticks: 9000, carId: 'tornado', fp: lapFingerprint('copacabana', 'tornado') });
    // Ida e volta pelo saneamento do save.
    expect(sanitizeSave(JSON.parse(JSON.stringify(save))).bestLaps.copacabana).toEqual(save.bestLaps.copacabana);
  });

  it('save antigo sem impressão segue igual; impressão estragada some sem levar o recorde junto', () => {
    const old: BestLap = { ticks: 3000, name: 'Ana', carId: 'falcao', date: '2026-09-01' };
    const s = sanitizeSave({ bestLaps: { copacabana: old, rota_66: { ...old, fp: 'lixo!' }, paris: { ...old, fp: 12345678 } } });
    expect(s.bestLaps.copacabana).toEqual(old);
    expect(s.bestLaps.rota_66).toEqual(old);
    expect(s.bestLaps.paris).toEqual(old);
  });

  it('"versão anterior" só com impressão presente e diferente; recorde antigo não é trocado por volta mais lenta', () => {
    const current = lapFingerprint('copacabana', 'falcao') ?? '';
    const base: BestLap = { ticks: 3000, name: 'Ana', carId: 'falcao', date: '' };
    expect(recordFromOtherVersion(base, 'copacabana')).toBe(false); // sem impressão: versão desconhecida, sem marca
    expect(recordFromOtherVersion({ ...base, fp: current }, 'copacabana')).toBe(false);
    expect(recordFromOtherVersion({ ...base, fp: 'deadbeef' }, 'copacabana')).toBe(true);
    expect(recordFromOtherVersion({ ...base, fp: current }, 'rota_66')).toBe(true); // a impressão é da pista
    const save = sanitizeSave({ bestLaps: { copacabana: { ...base, fp: 'deadbeef' } } });
    const one: HumanEntry[] = [{ seat: 0, name: 'Bia', carId: 'falcao', teamId: 0, color: '#fff' }];
    expect(recordRaceResults(save, [row(0, 9000, 3100)], one, 'copacabana', 3).map((r) => r.kind)).toEqual(['race']);
    expect(save.bestLaps.copacabana).toEqual({ ...base, fp: 'deadbeef' });
    recordRaceResults(save, [row(0, 9000, 2900)], one, 'copacabana', 3);
    expect(save.bestLaps.copacabana).toMatchObject({ ticks: 2900, name: 'Bia', fp: current });
  });
});
