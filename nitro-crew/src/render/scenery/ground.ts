// Altura do chão onde o cenário pousa: a mesma conta das faixas de `render/terrain.ts`, importada de lá
// (columnRelief/columnDrop/columnDistance). Era uma cópia, e no merge da onda F o relevo novo do terreno (plano até
// 26 m, descida ao fundo do vale, 12 colunas) deixou os objetos 1,25 m fora do chão — `tests/scenery.test.ts` monta
// um Terrain de verdade, compara vértice a vértice com isto e pegou.
// Puro e por índice de segmento: não depende do quadro, então a altura de cada objeto é calculada uma vez.
import type { SceneryId, Segment, Track } from '../../core/types';
import { COLS, columnDistance as terrainColumnDistance, columnHeightAboveRoad, SEA_DEPTH_M } from '../terrain';
import { Y_SCALE } from '../units';

/** Distância lateral (m) de cada coluna da faixa de terreno (terrain.ts). */
export const TERRAIN_COLS = COLS;

/** Menor altura da pista (m): o fundo do vale e o nível do mar saem dela. Cacheada por pista. */
const minRoad = new WeakMap<Track, number>();
export function minRoadY(track: Track): number {
  let v = minRoad.get(track);
  if (v === undefined) {
    let m = Infinity;
    for (const s of track.segments) m = Math.min(m, s.y0, s.y1);
    v = m * Y_SCALE;
    minRoad.set(track, v);
  }
  return v;
}

/** O lado direito do litoral é mar. */
export function isSeaSide(biome: SceneryId, side: number): boolean {
  return biome === 'coast' && side > 0;
}

/** Nível da água (m) relativo ao ponto de início do segmento. */
export function seaLevelOffset(track: Track, seg: Segment): number {
  return minRoadY(track) - SEA_DEPTH_M - seg.y0 * Y_SCALE;
}

/** Distância real da coluna `c` no ponto do segmento: do lado de dentro da curva ela é presa (terrain.ts). */
export function columnDistance(seg: Segment, side: number, c: number): number {
  return terrainColumnDistance(seg.curve, side, c);
}

/** Altura do terreno na coluna `c` do ponto do segmento, relativa à pista nesse ponto. */
export function columnHeight(track: Track, seg: Segment, side: number, c: number): number {
  return columnHeightAboveRoad(track.def.scenery, seg.index, seg.y0 * Y_SCALE - minRoadY(track), side, c);
}

/**
 * Altura do chão (m) a `d` metros do centro da pista, lado `side`, no ponto de início do segmento
 * (relativa à pista ali). Interpola entre as colunas como a faixa do terreno.
 */
export function groundAt(track: Track, seg: Segment, side: number, d: number): number {
  const n = TERRAIN_COLS.length;
  let prevD = 0; let prevH = 0;
  for (let c = 0; c < n; c++) {
    const cd = columnDistance(seg, side, c);
    const ch = columnHeight(track, seg, side, c);
    if (d <= cd) {
      if (c === 0) return ch;
      const t = (d - prevD) / Math.max(1e-6, cd - prevD);
      return prevH + (ch - prevH) * t;
    }
    prevD = cd; prevH = ch;
  }
  return prevH;
}

/**
 * Altura do chão para um objeto na fração `f` do segmento `i` (0 = início, 1 = início do seguinte), a
 * `d` metros do centro: relativa à pista no INÍCIO do segmento `i` (é o `py` do ponto do RoadFrame).
 */
export function groundOffset(track: Track, i: number, f: number, side: number, d: number): number {
  const segs = track.segments;
  const s0 = segs[i];
  const s1 = segs[(i + 1) % segs.length];
  const rise = (s0.y1 - s0.y0) * Y_SCALE;
  const h0 = groundAt(track, s0, side, d);
  const h1 = groundAt(track, s1, side, d) + rise;
  return h0 + (h1 - h0) * f;
}
