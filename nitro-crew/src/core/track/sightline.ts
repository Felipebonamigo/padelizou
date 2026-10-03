// Linha de visada da câmera de perseguição até um marco turístico, no núcleo e sem trigonometria: as praças e os
// mirantes (plazas.ts) são abertos onde a visada de quem chega cruza a faixa dos prédios da beira — do lado do marco
// e, quando a aproximação faz curva, do outro lado (a visada atravessa a parte de dentro da curva). É a mesma conta do
// renderizador (render/scenery/sight.ts), simplificada: a câmera na linha central, um ponto do marco, a faixa dos
// prédios no lugar da grade de alturas. O builder chama durante a montagem da pista (a colisão depende disto), então
// tudo aqui é soma, produto e raiz — iguais em toda máquina (o giro por segmento é pequeno: série de Taylor).
import type { Track } from '../types';

/**
 * O que a conta precisa do renderizador e da conta de enquadramento (o núcleo não importa o renderizador;
 * `tests/landmarks-pracas.test.ts` confere cada número com render/units.ts e render/scenery/sight.ts).
 */
export const SIGHT_MODEL = {
  /** Metros por segmento e giro (rad) por unidade de curva por segmento (render/units.ts). */
  segmentM: 4,
  headingPerCurve: 0.0035,
  /** Tangente da meia largura do quadro que conta (os 80% do meio, 16:9, FOV na velocidade da conta: SIGHT_TAN_H). */
  tanH: 1.04,
  /** Profundidade (m) até onde a névoa deixa ler o marco, por período (um pouco aquém da de sight.ts). */
  depthM: { day: 490, dusk: 545, night: 380 },
  /** Distância (m) que a câmera anda no mínimo de tela que a praça garante: 2,5 s (LANDMARK_SIGHT_MIN) × 96 m/s × 1,1. */
  needM: 264,
  /** A faixa dos prédios da beira (m de lado): fachada na borda de colisão (8,75–10 m) até o fundo (~45 m). */
  bandM: [9, 45] as const,
} as const;

/** Linha central desenrolada (duas voltas: o segmento i é medido na segunda, k = i + n) com o rumo de cada segmento. */
export interface Centerline {
  readonly n: number;
  /** Início de cada segmento (m). */
  readonly px: Float64Array;
  readonly pz: Float64Array;
  /** Seno e cosseno do rumo no início de cada segmento (frente = (sen, −cos); direita = (cos, sen)). */
  readonly hs: Float64Array;
  readonly hc: Float64Array;
}

/** Giro de (s, c) por um ângulo pequeno `d` (|d| ≤ 0,03 rad: o erro da série é < 1e−13). */
function rotate(s: number, c: number, d: number): [number, number] {
  const d2 = d * d;
  const cd = 1 - d2 / 2 + (d2 * d2) / 24;
  const sd = d * (1 - d2 / 6 + (d2 * d2) / 120);
  const ns = s * cd + c * sd; const nc = c * cd - s * sd;
  const m = Math.sqrt(ns * ns + nc * nc);
  return [ns / m, nc / m];
}

/** A pista desenrolada como o renderizador faz (render/scenery/sight.ts, unrollTrack), sem seno nem cosseno. */
export function centerline(track: Track): Centerline {
  const segs = track.segments; const n = segs.length;
  const len = 2 * n + 1;
  const px = new Float64Array(len); const pz = new Float64Array(len);
  const hs = new Float64Array(len); const hc = new Float64Array(len);
  let s = 0; let c = 1;
  for (let k = 0; k < len; k++) {
    hs[k] = s; hc[k] = c;
    if (k + 1 === len) break;
    const d = segs[k % n].curve * SIGHT_MODEL.headingPerCurve;
    // Anda pelo rumo do meio do segmento e gira o rumo inteiro para o próximo.
    const [ms, mc] = rotate(s, c, d / 2);
    px[k + 1] = px[k] + SIGHT_MODEL.segmentM * ms; pz[k + 1] = pz[k] - SIGHT_MODEL.segmentM * mc;
    [s, c] = rotate(s, c, d);
  }
  return { n, px, pz, hs, hc };
}

/** O que a visada pede para um lugar de marco: quanto ele fica no quadro e as células da faixa dos prédios cruzadas. */
export interface SightNeed {
  /** Metros que a câmera anda com o marco no quadro e na névoa (até `needM`). */
  seenM: number;
  /** Por lado (0 = esquerda, 1 = direita), o primeiro e o último segmento (k desenrolado) cruzados pela visada. */
  cross: [[number, number], [number, number]];
}

/**
 * A câmera de perseguição vem pela linha central até o marco (um ponto a `lat` m do centro, + = direita, no segmento
 * desenrolado `k`) e para quando ele já ficou `needM` no quadro (de 2 em 2 segmentos, de perto para longe), ou quando
 * passa da névoa, ou antes de `minCam` (o primeiro de cada marco: a câmera começa depois da largada). Cada visada conta
 * e marca os segmentos em que cruza a faixa dos prédios de cada lado.
 */
export function sightNeed(line: Centerline, k: number, lat: number, depthM: number, minCam: number): SightNeed {
  const { px, pz, hs, hc } = line;
  const [bandIn, bandOut] = SIGHT_MODEL.bandM;
  const tx = px[k] + lat * hc[k]; const tz = pz[k] + lat * hs[k];
  const cross: SightNeed['cross'] = [[Infinity, -Infinity], [Infinity, -Infinity]];
  const stepM = 2 * SIGHT_MODEL.segmentM;
  let seenM = 0;
  for (let j = k - 1; j >= Math.max(1, minCam) && seenM < SIGHT_MODEL.needM; j -= 2) {
    const vx = tx - px[j]; const vz = tz - pz[j];
    const depth = vx * hs[j] - vz * hc[j];
    if (depth > depthM) break;
    const side = vx * hc[j] + vz * hs[j];
    if (depth < 1 || (side < 0 ? -side : side) > depth * SIGHT_MODEL.tanH) continue;
    seenM += stepM;
    // A visada, de 4 em 4 m: segmento em que cada ponto cai (anda pelos planos de início) e a lateral nele.
    const dist = Math.sqrt(vx * vx + vz * vz);
    const steps = Math.ceil(dist / SIGHT_MODEL.segmentM);
    let q = j;
    for (let m = 1; m < steps; m++) {
      const t = m / steps;
      const wx = px[j] + vx * t; const wz = pz[j] + vz * t;
      while (q + 1 < k + 4 && (wx - px[q + 1]) * hs[q + 1] - (wz - pz[q + 1]) * hc[q + 1] >= 0) q++;
      while (q > 1 && (wx - px[q]) * hs[q] - (wz - pz[q]) * hc[q] < 0) q--;
      const l = (wx - px[q]) * hc[q] + (wz - pz[q]) * hs[q];
      const al = l < 0 ? -l : l;
      if (al < bandIn || al > bandOut) continue;
      const r = cross[l < 0 ? 0 : 1];
      if (q < r[0]) r[0] = q;
      if (q > r[1]) r[1] = q;
    }
  }
  return { seenM, cross };
}
