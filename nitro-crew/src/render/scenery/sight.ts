// Enquadramento dos marcos turísticos (docs/VISUAL.md, "Marcos turísticos"): quanto tempo um marco fica NA TELA de
// quem corre. A câmera de perseguição de verdade (camera.ts: altura, distância atrás do carro, ponto olhado, FOV na
// velocidade) anda pela linha central a uma velocidade de corrida e, a cada SIGHT_DT, conta o instante em que uma parte
// do marco (SIGHT_MIN_POINTS dos 7 pontos de amostra da caixa dele) está ao mesmo tempo
//   - dentro do quadro (na faixa do meio da largura: na beirada só se vê um pedaço passando),
//   - perto o bastante para a névoa deixar ler (a do resto do cenário, ou a mais fina do lote do horizonte),
//   - sem nada alto na frente perto da pista (prédio, arquibancada, box, outdoor: a grade de alturas do layout),
//   - e na janela que o runtime desenha (o RoadFrame da qualidade alta, cortado pela névoa).
// Geometria simples e determinística, sem raycast em malha; o relevo do terreno e a mata (posta depois) não entram.
// Puro: só contas sobre a linha central desenrolada.
import { REFERENCE_SPEED, SEGMENT_LENGTH } from '../../core/constants';
import type { Track } from '../../core/types';
import { CHASE_CAMERA } from '../camera';
import { palette } from '../palette';
import { FRAME_AHEAD } from '../roadframe';
import { FOG_DENSITY } from '../sky';
import { HEADING_PER_CURVE, SEGMENT_M, Y_SCALE } from '../units';

/**
 * Marcos no horizonte (skyline: Cristo, Pão de Açúcar, vulcão): a névoa deles conta a distância × HAZE_FOG_SCALE (lote
 * `haze` do runtime). Ficam a 400 m+ da pista e são vistos a 600–1.000 m; com a névoa do resto do cenário (0,0019/m)
 * chegariam 70–95% apagados. Com 0,55 entram na névoa (40–65%) como os planos do horizonte do terreno.
 */
export const HAZE_FOG_SCALE = 0.55;
/** O runtime não desenha cenário além de SCENERY_FOG_REACH / densidade da névoa (m ao longo da pista): já está apagado. */
export const SCENERY_FOG_REACH = 2.2;
/** Segmentos atrás do carro que o runtime ainda desenha (sombra e canto da tela). */
export const SCENERY_BEHIND_DRAW = 4;

/**
 * Velocidade da conta: 0,8 da máxima de referência = 96 m/s (a IA profissional corre a 0,55–0,88 da máxima em média,
 * docs/PISTAS.md, "v média"). O FOV abre com ela como no jogo (62° + 13° × 0,8 ≈ 72° na vertical).
 */
export const SIGHT_SPEED_FRAC = 0.8;
export const SIGHT_SPEED_MS = (REFERENCE_SPEED * SIGHT_SPEED_FRAC * SEGMENT_M) / SEGMENT_LENGTH;
/** Passo da simulação (s): 9,6 m de pista a 96 m/s. */
export const SIGHT_DT = 0.1;
/** Proporção do quadro: a mais estreita do jogo (1 jogador e a grade 2×2 são 16:9; 2 jogadores, 32:9). */
export const SIGHT_ASPECT = 16 / 9;
/** Só conta o ponto nos 80% do meio da largura do quadro: na beirada o marco só passa. */
export const SIGHT_FRAME_X = 0.8;
/** Névoa (FogExp2: 1 − e^−(ρ·d)²) até a qual o marco ainda se lê. */
export const SIGHT_FOG = 0.6;
/** Quantos dos 7 pontos de amostra do marco (ver `sightSeconds`) bastam no mesmo instante: um já é uma parte dele na tela. */
export const SIGHT_MIN_POINTS = 1;
/**
 * O mínimo (s) que todo marco tem de ficar à vista na aproximação (tests/landmarks-enquadramento.test.ts) e a meta da
 * busca do layout. 2,5 s a 96 m/s são ~240 m de pista: dá para notar e reconhecer o marco sem tirar o olho da pista. O do
 * horizonte (o cartão-postal: Cristo, Fuji, Uluru) é grande e visto de longe na névoa fina: pede mais.
 */
export const LANDMARK_SIGHT_MIN = { near: 2.5, far: 2.5, skyline: 4 } as const;
/** Passo (m) da linha de visada contra a grade de alturas (a grade tem 2 m × 1 segmento; o que tapa ocupa ≥ 3 segmentos). */
const RAY_STEP_M = 4;

/** O que tapa a vista: altura (m, 0 = nada) na célula do segmento `seg` (0..n−1), lado `side`, a `lat` m do centro. */
export type BlockHeight = (seg: number, side: number, lat: number) => number;

export interface SightRoad {
  readonly n: number;
  /** Linha central desenrolada em três voltas (o marco do segmento i é medido na do meio, k = i + n): início de cada segmento. */
  readonly px: Float64Array;
  readonly pz: Float64Array;
  /** Rumo (rad) no início de cada segmento, e o seno/cosseno dele. */
  readonly hd: Float64Array;
  readonly hs: Float64Array;
  readonly hc: Float64Array;
  /** Altura da pista (m) no início de cada segmento. */
  readonly py: Float64Array;
  /** Profundidade (m, no eixo da câmera) até onde a névoa deixa ler: o cenário e o lote do horizonte. */
  readonly fogM: number;
  readonly hazeM: number;
  /** Segmentos à frente do carro em que o runtime desenha o cenário. */
  readonly ahead: number;
  readonly block: BlockHeight;
  /** Lateral máxima (m) de algo alto na grade: a visada além dela não é conferida. */
  readonly blockLat: number;
  /** Soma acumulada de "segmento com algo alto" pela linha desenrolada (corte rápido: nada no caminho). */
  readonly blockCum: Int32Array;
}

/** Linha central desenrolada em três voltas: a pista do DSL não fecha, só a vizinhança vale. */
export function unrollTrack(track: Track): { px: Float64Array; pz: Float64Array; hd: Float64Array; py: Float64Array } {
  const segs = track.segments; const n = segs.length;
  const px = new Float64Array(3 * n + 1); const pz = new Float64Array(3 * n + 1); const hd = new Float64Array(3 * n + 1);
  const py = new Float64Array(3 * n + 1);
  for (let k = 0; k < 3 * n; k++) {
    const s = segs[k % n];
    const d = s.curve * HEADING_PER_CURVE;
    const a = hd[k] + d / 2;
    px[k + 1] = px[k] + SEGMENT_M * Math.sin(a); pz[k + 1] = pz[k] - SEGMENT_M * Math.cos(a); hd[k + 1] = hd[k] + d;
    py[k] = s.y0 * Y_SCALE;
  }
  py[3 * n] = segs[0].y0 * Y_SCALE;
  return { px, pz, hd, py };
}

/**
 * A pista pronta para a conta: linha desenrolada, a névoa do período dela na qualidade alta (a padrão) e a grade do
 * que tapa a vista (`hasBlock(seg)`: o segmento tem algo alto em algum lado).
 */
export function sightRoad(track: Track, block: BlockHeight, blockLat: number, hasBlock: (seg: number) => boolean): SightRoad {
  const n = track.segments.length;
  const { px, pz, hd, py } = unrollTrack(track);
  const hs = new Float64Array(hd.length); const hc = new Float64Array(hd.length);
  for (let k = 0; k < hd.length; k++) { hs[k] = Math.sin(hd[k]); hc[k] = Math.cos(hd[k]); }
  const rho = FOG_DENSITY.high * palette(track.def.scenery, track.def.timeOfDay).fogDensity;
  // 1 − e^−(ρd)² = SIGHT_FOG  →  d = √(−ln(1 − SIGHT_FOG)) / ρ.
  const fogM = Math.sqrt(-Math.log(1 - SIGHT_FOG)) / rho;
  const ahead = Math.min(FRAME_AHEAD.high, Math.floor(SCENERY_FOG_REACH / rho / SEGMENT_M));
  const blockCum = new Int32Array(3 * n + 2);
  for (let q = 0; q <= 3 * n; q++) blockCum[q + 1] = blockCum[q] + (hasBlock(q % n) ? 1 : 0);
  return { n, px, pz, hd, hs, hc, py, fogM, hazeM: fogM / HAZE_FOG_SCALE, ahead, block, blockLat, blockCum };
}

/** Caixa do modelo (catalog.ts, `modelBounds`). */
export interface SightBounds { minX: number; maxX: number; minZ: number; maxZ: number; maxY: number }

const TAN_V = Math.tan(((CHASE_CAMERA.baseFov + CHASE_CAMERA.speedFov * Math.min(1.2, SIGHT_SPEED_FRAC)) * Math.PI) / 360);
/** Tangente da meia largura do quadro que conta (os 80% do meio): a `lat` m de lado, o marco sai dele a `lat / SIGHT_TAN_H` m. */
export const SIGHT_TAN_H = TAN_V * SIGHT_ASPECT * SIGHT_FRAME_X;
const TAN_H = SIGHT_TAN_H;
const SAMPLES = 7;
const SAMPLE_X = new Float64Array(SAMPLES); const SAMPLE_Y = new Float64Array(SAMPLES); const SAMPLE_Z = new Float64Array(SAMPLES);

/**
 * Segundos em que o marco posto no segmento `k` (desenrolado: a volta do meio), na fração `f`, a `x` m do centro
 * (+ = direita), com a base a `y` m do início do segmento e girado `yaw` (Placement do layout) fica à vista da câmera de
 * perseguição na aproximação: da entrada dele na janela do runtime até o carro passar. `haze`: a névoa do horizonte.
 */
export function sightSeconds(road: SightRoad, k: number, f: number, x: number, y: number, yaw: number, b: SightBounds, haze: boolean, from = -Infinity): number {
  const { px, pz, hd, hs, hc, py } = road;
  // Centro e giro como em runtime.place: lateral somada nas duas pontas do segmento e interpolada.
  const ax = px[k] + x * hc[k]; const az = pz[k] + x * hs[k];
  const X = ax + (px[k + 1] + x * hc[k + 1] - ax) * f;
  const Z = az + (pz[k + 1] + x * hs[k + 1] - az) * f;
  const Y = py[k] + y;
  const th = yaw - (hd[k] + (hd[k + 1] - hd[k]) * f);
  const ct = Math.cos(th); const st = Math.sin(th);
  const H = b.maxY;
  const mx = (b.minX + b.maxX) / 2; const mz = (b.minZ + b.maxZ) / 2;
  // Pontos de amostra: o centro embaixo, a meia altura e no alto (o pé da torre de 350 m que não cabe no quadro de
  // perto, o Cristo no morro) e, a meio caminho do centro para as faces que quem chega vê (+X, a da pista; +Z, a de quem
  // vem), três ao longo da face da pista e um na de quem vem, a 40% da altura — dentro do morro cônico, na ponta da
  // ponte de 1 km, na parede do tepui de 2 km.
  const fx = (mx + b.maxX) / 2; const zAhead = (mz + b.minZ) / 2; const zBack = (mz + b.maxZ) / 2;
  const local: Array<[number, number, number]> = [[mx, 0.2 * H, mz], [mx, 0.5 * H, mz], [mx, 0.85 * H, mz], [fx, 0.4 * H, mz], [fx, 0.4 * H, zAhead], [fx, 0.4 * H, zBack], [mx, 0.4 * H, zBack]];
  for (let m = 0; m < SAMPLES; m++) {
    const [lx, ly, lz] = local[m];
    // Giro do runtime: x' = cos θ·x + sen θ·z, z' = −sen θ·x + cos θ·z.
    SAMPLE_X[m] = X + ct * lx + st * lz; SAMPLE_Y[m] = Y + ly; SAMPLE_Z[m] = Z - st * lx + ct * lz;
  }
  // Até onde a pegada vai ao longo da pista (ponte de 1 km): a visada é conferida até lá.
  const reach = Math.ceil(Math.max(-b.minX, b.maxX, -b.minZ, b.maxZ) / SEGMENT_M) + 2;
  const fogM = haze ? road.hazeM : road.fogM;
  const fog2 = fogM * fogM;
  const step = (SIGHT_SPEED_MS * SIGHT_DT) / SEGMENT_M;
  const anchor = k + f;
  const backS = CHASE_CAMERA.back / SEGMENT_M; const lookS = CHASE_CAMERA.lookAhead / SEGMENT_M;
  const lookDist = CHASE_CAMERA.back + CHASE_CAMERA.lookAhead;
  let seen = 0;
  // O runtime desenha o marco enquanto o segmento dele está entre SCENERY_BEHIND_DRAW atrás e `ahead` à frente do carro.
  for (let s = Math.max(k - road.ahead, from); s < k + SCENERY_BEHIND_DRAW + 1; s += step) {
    const j = Math.floor(s); const u = s - j;
    const cx = px[j] + (px[j + 1] - px[j]) * u; const cz = pz[j] + (pz[j + 1] - pz[j]) * u;
    const h = hd[j] + (hd[j + 1] - hd[j]) * u;
    const sh = Math.sin(h); const ch = Math.cos(h);
    const ry = roadY(py, s);
    const camY = roadY(py, s - backS) + CHASE_CAMERA.height;
    const lookY = ry + CHASE_CAMERA.lookY + Math.max(-4, Math.min(4, (roadY(py, s + lookS) - ry) * 0.6));
    // Câmera atrás do carro no rumo dele; olha o ponto à frente. Base: D (eixo), R (direita), U = R × D.
    const camX = cx - CHASE_CAMERA.back * sh; const camZ = cz + CHASE_CAMERA.back * ch;
    const dyLook = lookY - camY;
    const inv = 1 / Math.hypot(lookDist, dyLook);
    const Dx = sh * lookDist * inv; const Dy = dyLook * inv; const Dz = -ch * lookDist * inv;
    const Rx = ch; const Rz = sh;
    const Ux = -Rz * Dy; const Uy = Rz * Dx - Rx * Dz; const Uz = Rx * Dy;
    const camSeg = Math.floor(s - backS);
    let ok = 0;
    for (let m = 0; m < SAMPLES && ok < SIGHT_MIN_POINTS; m++) {
      const dx = SAMPLE_X[m] - camX; const dy = SAMPLE_Y[m] - camY; const dz = SAMPLE_Z[m] - camZ;
      const zc = dx * Dx + dy * Dy + dz * Dz;
      if (zc < 0.3 || zc * zc > fog2) continue;
      const xc = dx * Rx + dz * Rz;
      if (Math.abs(xc) > zc * TAN_H) continue;
      const yc = dx * Ux + dy * Uy + dz * Uz;
      if (Math.abs(yc) > zc * TAN_V) continue;
      if (blocked(road, camX, camY, camZ, SAMPLE_X[m], SAMPLE_Y[m], SAMPLE_Z[m], camSeg, Math.ceil(anchor) + reach)) continue;
      ok++;
    }
    if (ok >= SIGHT_MIN_POINTS) seen += SIGHT_DT;
  }
  return Math.round(seen * 1000) / 1000;
}

/** Altura da pista (m) no ponto `t` (segmentos, desenrolado). */
function roadY(py: Float64Array, t: number): number {
  const q = Math.floor(t);
  return py[q] + (py[q + 1] - py[q]) * (t - q);
}

/**
 * Algo alto da grade na linha de visada? Anda pela linha (horizontal) de RAY_STEP_M em RAY_STEP_M, acha o segmento e a
 * lateral de cada ponto (plano de início do segmento, andando a partir do da câmera) e compara a altura da visada ali,
 * acima da pista, com a do que estiver na célula. Para quando a visada passa da lateral de tudo o que é alto.
 */
function blocked(road: SightRoad, cx: number, cy: number, cz: number, tx: number, ty: number, tz: number, q0: number, qEnd: number): boolean {
  const { px, pz, hs, hc, py, n } = road;
  const lo = Math.max(0, q0 - 2); const hi = Math.min(3 * n - 1, Math.max(q0, qEnd) + 2);
  if (road.blockCum[hi + 1] - road.blockCum[lo] === 0) return false;
  const dx = tx - cx; const dy = ty - cy; const dz = tz - cz;
  const steps = Math.ceil(Math.hypot(dx, dz) / RAY_STEP_M);
  let q = Math.max(lo, Math.min(hi, q0));
  for (let m = 1; m < steps; m++) {
    const t = m / steps;
    const wx = cx + dx * t; const wz = cz + dz * t;
    while (q < hi && (wx - px[q + 1]) * hs[q + 1] - (wz - pz[q + 1]) * hc[q + 1] >= 0) q++;
    while (q > lo && (wx - px[q]) * hs[q] - (wz - pz[q]) * hc[q] < 0) q--;
    const lat = (wx - px[q]) * hc[q] + (wz - pz[q]) * hs[q];
    const al = Math.abs(lat);
    if (al > road.blockLat + 10) return false;
    const top = road.block(q % n, lat < 0 ? -1 : 1, al);
    if (top > 0 && cy + dy * t - py[q] < top) return true;
  }
  return false;
}
