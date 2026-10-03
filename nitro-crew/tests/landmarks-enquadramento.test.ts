// Enquadramento dos marcos turísticos (docs/VISUAL.md, "Marcos turísticos" › "Na tela"): defeito visto nas capturas da
// onda H — o marco ficava onde a câmera de perseguição quase não o mostra. Itaúnas (dunas) e Parintins (Bumbódromo):
// o primeiro logo depois da largada, à esquerda, atrás das arquibancadas, e a 200 m+ de lado só entrava na beirada do
// quadro já perto; Storseisundet, Stigfossen, Cape Point e o arco da Great Ocean (170–265 m de lado), na beirada ou
// atrás do box. A conta (scenery/sight.ts) anda com a câmera de verdade pela pista a 96 m/s e soma o tempo em que o
// marco está no quadro, legível na névoa e sem prédio, arquibancada, box ou outdoor na frente.
import { describe, expect, it } from 'vitest';
import { placeOf } from '../src/core/data/places';
import { getTrack, TRACKS } from '../src/core/track';
import type { Segment, Track } from '../src/core/types';
import { landmarkSight, sceneryLayout } from '../src/render/scenery/layout';
import { LANDMARK_SIGHT_MIN, SIGHT_SPEED_MS, sightRoad, sightSeconds, type BlockHeight } from '../src/render/scenery/sight';

/** Pista sintética: `n` segmentos com a curvatura de `curve(i)`, plana, savana de dia (névoa: 504 m). */
function fakeTrack(n: number, curve: (i: number) => number = () => 0): Track {
  const segments: Segment[] = [];
  for (let i = 0; i < n; i++) segments.push({ index: i, z: i * 200, curve: curve(i), y0: 0, y1: 0, band: 0, pit: false, sprites: [] });
  return { def: { id: 'reta', scenery: 'savanna', timeOfDay: 'day' }, segments, length: n * 200, startIndex: 0 } as unknown as Track;
}
const nothing: BlockHeight = () => 0;
const box = (w: number, h: number, d: number) => ({ minX: -w / 2, maxX: w / 2, minZ: -d / 2, maxZ: d / 2, maxY: h });

describe('enquadramento: a conta', () => {
  const n = 1000;
  const k = 500 + n; // volta do meio
  const road = sightRoad(fakeTrack(n), nothing, 0, () => false);

  it('numa reta, um marco a 60 m de lado aparece da névoa (≈ 500 m) até sair dos 80% do meio do quadro (≈ 60 m)', () => {
    const seen = sightSeconds(road, k, 0.5, -60, 0, 0, box(20, 20, 20), false);
    // (504 − 60) m a 96 m/s ≈ 4,6 s; a caixa (±10 m) mexe um pouco nas pontas.
    expect(seen).toBeGreaterThan(4.0);
    expect(seen).toBeLessThan(5.3);
    expect(SIGHT_SPEED_MS).toBeCloseTo(96, 5);
  });

  it('é simétrico (esquerda × direita, +X do modelo para a pista) e cai com a distância de lado', () => {
    const left = sightSeconds(road, k, 0.5, -150, 0, 0, box(40, 40, 40), false);
    const right = sightSeconds(road, k, 0.5, 150, 0, Math.PI, box(40, 40, 40), false);
    expect(left).toBeCloseTo(right, 5);
    expect(sightSeconds(road, k, 0.5, -300, 0, 0, box(40, 40, 40), false)).toBeLessThan(left);
  });

  it('o do horizonte (névoa × 0,55) é visto de mais longe que o resto do cenário', () => {
    const big = box(300, 300, 300);
    expect(sightSeconds(road, k, 0.5, -450, 0, 0, big, true)).toBeGreaterThan(sightSeconds(road, k, 0.5, -450, 0, 0, big, false) + 1);
  });

  it('uma fileira de prédios de 30 m na beira da pista (10–36 m) esconde o marco daquele lado, não o do outro', () => {
    // Prédios de 30 m do lado esquerdo em toda a aproximação.
    const wall = sightRoad(fakeTrack(n), (_seg, side, lat) => (side < 0 && lat >= 10 && lat <= 36 ? 30 : 0), 36, () => true);
    expect(sightSeconds(wall, k, 0.5, -60, 0, 0, box(20, 20, 20), false)).toBeLessThan(0.5);
    expect(sightSeconds(wall, k, 0.5, 60, 0, 0, box(20, 20, 20), false)).toBeCloseTo(sightSeconds(road, k, 0.5, 60, 0, 0, box(20, 20, 20), false), 5);
    // Um muro baixo (6 m: box, outdoor) não tapa um marco de 400 m: a visada até ele passa por cima.
    const low = sightRoad(fakeTrack(n), (_seg, side, lat) => (side < 0 && lat >= 10 && lat <= 24 ? 6 : 0), 24, () => true);
    const tall = box(200, 400, 200);
    expect(sightSeconds(low, k, 0.5, -300, 0, 0, tall, true)).toBeCloseTo(sightSeconds(road, k, 0.5, -300, 0, 0, tall, true), 5);
  });

  it('do lado de fora de uma curva que vem, o marco fica mais tempo na tela que na reta', () => {
    // Curva à direita (curve 4) nos 150 segmentos antes do marco: o de fora é o da esquerda.
    const curvy = sightRoad(fakeTrack(n, (i) => (i >= 330 && i < 480 ? 4 : 0)), nothing, 0, () => false);
    const straight = sightSeconds(road, k, 0.5, -200, 0, 0, box(80, 60, 80), false);
    expect(sightSeconds(curvy, k, 0.5, -200, 0, 0, box(80, 60, 80), false)).toBeGreaterThan(straight + 1);
  });
});

/**
 * Litoral e cidade: a receita do núcleo (track/builder.ts) põe prédio dos dois lados da beira da pista, e o marco de perto
 * e de longe só aparece nas brechas — o teto medido (a busca andando a volta inteira, sem a janela da largada) fica em
 * 1,3–2,5 s para os de perto do litoral. Ali o mínimo é 1 s (≈ 100 m de pista) e 2 s no horizonte; abrir mirantes nos
 * prédios (como as praças da cidade) é mudança do núcleo (docs/VISUAL.md, "Na tela").
 */
const ROADSIDE_BUILDINGS: ReadonlySet<string> = new Set(['coast', 'city_night']);
const ROADSIDE_MIN = { near: 1, far: 1, skyline: 2 } as const;

/**
 * O primeiro de cada marco que a janela perto da largada (≤ 150 segmentos o mais importante, ≤ 300 os outros:
 * tests/landmarks.test.ts) deixa abaixo do mínimo: o melhor lugar da janela, medido da largada (s). Regra de desenho,
 * não da busca — com a janela 150 segmentos maior, a praia de Alter do Chão passa (docs/VISUAL.md, "Na tela"). Quem
 * passar do mínimo sai da lista (o teste acusa).
 */
const WINDOW_LIMITED: Readonly<Record<string, number>> = {
  'alter_do_chao praia_de_rio': 2.3,
  'maceio jangada': 0.3,
  'maceio coqueiral': 0.8,
  'maragogi coqueiral': 0.6,
  'porto_seguro coqueiral': 0.7,
  'belem ver_o_peso': 0.8,
  'monaco_noite cassino_monte_carlo': 0.8,
  'santorini cupula_azul': 0.9,
};

describe('enquadramento: os marcos das pistas', () => {
  it(`todo marco fica à vista: perto e longe ≥ ${LANDMARK_SIGHT_MIN.near} s, horizonte ≥ ${LANDMARK_SIGHT_MIN.skyline} s (o primeiro, da largada)`, () => {
    const bad: string[] = [];
    const stale: string[] = [];
    for (const def of TRACKS) {
      if (!(placeOf(def.id)?.landmarks.length)) continue;
      const track = getTrack(def.id);
      for (const r of landmarkSight(track, sceneryLayout(track))) {
        // Na praça da cidade quem decide o lugar é o núcleo (plazas.ts, tests/landmarks-pracas.test.ts).
        if (r.plaza) continue;
        const key = `${def.id} ${r.id}`;
        const base = ROADSIDE_BUILDINGS.has(def.scenery) ? ROADSIDE_MIN[r.place] : LANDMARK_SIGHT_MIN[r.place];
        const listed = r.first ? WINDOW_LIMITED[key] : undefined;
        if (listed !== undefined && r.seen >= base) stale.push(`${key}: ${r.seen.toFixed(1)} s, já passa de ${base} s`);
        const min = listed ?? base;
        if (r.seen < min) bad.push(`${key}#${r.seg} (${r.place}${r.first ? ', 1º' : ''}, x ${r.x.toFixed(0)} m): ${r.seen.toFixed(1)} s < ${min} s`);
      }
    }
    expect(bad, `${bad.length} marcos pouco vistos`).toEqual([]);
    expect(stale, 'saiam de WINDOW_LIMITED').toEqual([]);
  }, 180000);
});
