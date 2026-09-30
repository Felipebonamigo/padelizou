// Altura do chão onde o cenário pousa. ESPELHO de `render/terrain.ts` (COLS, relief, o mar do
// litoral e a dobra do lado de dentro da curva): o terreno é de outra tarefa e não exporta a conta.
// `tests/scenery.test.ts` monta um Terrain de verdade e compara vértice a vértice com isto — se o
// relevo mudar lá, o teste falha e aponta para cá (o certo, no merge, é o terrain exportar a função).
// Puro e por índice de segmento: não depende do quadro, então a altura de cada objeto é calculada uma vez.
import type { SceneryId, Segment, Track } from '../../core/types';
import { fbm, valueNoise } from '../noise';
import { HEADING_PER_CURVE, SEGMENT_M, Y_SCALE } from '../units';

/** Distância lateral (m) de cada coluna da faixa de terreno (terrain.ts). */
export const TERRAIN_COLS = [8.4, 16, 26, 40, 60, 90, 130, 180, 260, 380];
const SEA_DEPTH_M = 3.2;

function reliefAmplitude(biome: SceneryId): [number, number] {
  switch (biome) {
    case 'desert': return [24, 0.016];
    case 'alpine': return [62, 0.03];
    case 'tropical': return [34, 0.035];
    case 'savanna': return [9, 0.028];
    case 'coast': return [20, 0.03];
    case 'city_night': return [0, 0.03];
  }
}

/** Relevo (m acima da pista) na coluna `col` do lado `side` do segmento `seg` — igual ao terrain.ts. */
export function terrainRelief(biome: SceneryId, seg: number, col: number, side: number): number {
  if (col < 2) return 0;
  const [amp, freq] = reliefAmplitude(biome);
  if (amp === 0) return 0;
  const ramp = Math.pow((col - 1) / (TERRAIN_COLS.length - 2), 1.25);
  const seed = side > 0 ? 501 : 907;
  const n = fbm(seed, seg * freq + col * 0.85, 2) * 0.5 + 0.5;
  const m = valueNoise(seed + 77, seg * freq * 1.9 + col * 2.3) * 0.5 + 0.5;
  let h = (n * 0.75 + m * 0.25) * amp * ramp;
  if (biome === 'desert') h = amp * ramp * Math.pow(n, 1.6) * 0.9 + m * 3 * ramp;
  return h;
}

/** Menor altura da pista (m): o nível do mar do litoral sai dela. Cacheada por pista. */
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

/** O lado direito do litoral é mar (a partir da 3ª coluna). */
export function isSeaSide(biome: SceneryId, side: number): boolean {
  return biome === 'coast' && side > 0;
}

/** Nível da água (m) relativo ao ponto de início do segmento. */
export function seaLevelOffset(track: Track, seg: Segment): number {
  return minRoadY(track) - SEA_DEPTH_M - seg.y0 * Y_SCALE;
}

/** Distância real da coluna `c` no ponto do segmento: do lado de dentro da curva ela é presa (terrain.ts). */
export function columnDistance(seg: Segment, side: number, c: number): number {
  const delta = seg.curve * HEADING_PER_CURVE;
  const inside = (side > 0) === (delta > 0) && delta !== 0;
  if (!inside) return TERRAIN_COLS[c];
  return Math.min(TERRAIN_COLS[c], 0.9 * SEGMENT_M / (Math.abs(delta) + 1e-6));
}

/** Altura do terreno na coluna `c` do ponto do segmento, relativa à pista nesse ponto. */
export function columnHeight(track: Track, seg: Segment, side: number, c: number): number {
  const biome = track.def.scenery;
  if (isSeaSide(biome, side)) return c === 0 ? 0 : c === 1 ? -0.7 : seaLevelOffset(track, seg) - 0.6;
  return terrainRelief(biome, seg.index, c, side);
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
