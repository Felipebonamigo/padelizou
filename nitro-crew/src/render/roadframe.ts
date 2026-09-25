// Referencial local da pista. A simulação é 1D (z ao longo da pista) + lateral; as pistas do
// DSL não fecham geometricamente e podem se cruzar, então não existe um mundo fixo. A cada
// quadro, para cada viewport, a linha central é reconstruída no referencial do carro daquele
// viewport: origem exatamente no ponto da linha central em `baseZ`, eixo −Z para a frente,
// rumo integrado segmento a segmento (para trás e para a frente). Com a câmera de perseguição
// alinhada à pista, o resultado é indistinguível de um mundo fixo. Puro: sem Three, testável.
import { SEGMENT_LENGTH } from '../core/constants';
import type { Track } from '../core/types';
import { HEADING_PER_CURVE, ROAD_HALF_WIDTH_M, SEGMENT_M, Y_SCALE } from './units';

export interface RoadFrame {
  /** Pontos válidos (behind + ahead + 1). */
  count: number;
  /** Segmento que contém `baseZ`; o ponto `behind` é o início dele. */
  baseIndex: number;
  /** Quantos pontos existem antes do ponto do segmento base. */
  behind: number;
  /** Posição (m) do início de cada segmento da janela, no referencial local. */
  px: Float32Array;
  py: Float32Array;
  pz: Float32Array;
  /** Rumo (rad, positivo = direita) no início de cada segmento, relativo ao carro. */
  heading: Float32Array;
  /** Índice do segmento de cada ponto (com volta na pista). */
  segIndex: Int32Array;
}

function allocate(capacity: number, behind: number): RoadFrame {
  return {
    count: 0, baseIndex: 0, behind,
    px: new Float32Array(capacity), py: new Float32Array(capacity), pz: new Float32Array(capacity),
    heading: new Float32Array(capacity), segIndex: new Int32Array(capacity),
  };
}

function wrapZ(z: number, length: number): number {
  let zz = z % length;
  if (zz < 0) zz += length;
  return zz;
}

/**
 * Monta a janela de `behind` segmentos atrás e `ahead` à frente de `baseZ`. Cada segmento é
 * uma corda de 4 m na direção do rumo médio dele (a curvatura por segmento é pequena: o erro
 * da corda é < 0,01%). A origem fica sobre a corda do segmento base, na fração de `baseZ`
 * dentro dele — assim o carro do viewport, localizado por `locateOnFrame`, cai exatamente em
 * (0,0,0) e o movimento entre segmentos é contínuo. Passe `out` para não alocar por quadro.
 */
export function buildRoadFrame(track: Track, baseZ: number, behind: number, ahead: number, out?: RoadFrame): RoadFrame {
  const segs = track.segments;
  const n = segs.length;
  const count = behind + ahead + 1;
  const frame = out && out.px.length >= count ? out : allocate(count, behind);
  frame.count = count;
  frame.behind = behind;
  const { px, py, pz, heading, segIndex } = frame;

  const zz = wrapZ(baseZ, track.length);
  const b = Math.floor(zz / SEGMENT_LENGTH) % n;
  const base = segs[b];
  const f = (zz - b * SEGMENT_LENGTH) / SEGMENT_LENGTH;
  frame.baseIndex = b;
  const originY = (base.y0 + (base.y1 - base.y0) * f) * Y_SCALE;

  // Segmento base: rumo no início tal que, após a fração f, o rumo seja 0 (o carro olha −Z).
  const dBase = base.curve * HEADING_PER_CURVE;
  const hBase = -f * dBase;
  const aBase = hBase + dBase / 2;
  px[behind] = -f * SEGMENT_M * Math.sin(aBase);
  pz[behind] = f * SEGMENT_M * Math.cos(aBase);
  py[behind] = base.y0 * Y_SCALE - originY;
  heading[behind] = hBase;
  segIndex[behind] = b;

  // Para a frente.
  for (let j = behind + 1; j < count; j++) {
    const s = segIndex[j - 1];
    const seg = segs[s];
    const d = seg.curve * HEADING_PER_CURVE;
    const a = heading[j - 1] + d / 2;
    px[j] = px[j - 1] + SEGMENT_M * Math.sin(a);
    pz[j] = pz[j - 1] - SEGMENT_M * Math.cos(a);
    heading[j] = heading[j - 1] + d;
    py[j] = seg.y1 * Y_SCALE - originY;
    segIndex[j] = (s + 1) % n;
  }
  // Para trás.
  for (let j = behind - 1; j >= 0; j--) {
    const s = (segIndex[j + 1] - 1 + n) % n;
    const seg = segs[s];
    const d = seg.curve * HEADING_PER_CURVE;
    heading[j] = heading[j + 1] - d;
    const a = heading[j] + d / 2;
    px[j] = px[j + 1] - SEGMENT_M * Math.sin(a);
    pz[j] = pz[j + 1] + SEGMENT_M * Math.cos(a);
    py[j] = seg.y0 * Y_SCALE - originY;
    segIndex[j] = s;
  }
  return frame;
}

export interface FramePoint { x: number; y: number; z: number; heading: number }

/**
 * Posição 3D (m, referencial local) de algo em `z` ao longo da pista e `x` lateral (meias
 * larguras). Falso se estiver fora da janela. Interpola dentro do segmento.
 */
export function locateOnFrame(frame: RoadFrame, track: Track, z: number, x: number, out: FramePoint): boolean {
  const n = track.segments.length;
  const zz = wrapZ(z, track.length);
  const si = Math.floor(zz / SEGMENT_LENGTH) % n;
  const f = (zz - si * SEGMENT_LENGTH) / SEGMENT_LENGTH;
  let d = (si - frame.baseIndex) % n;
  if (d < 0) d += n;
  if (d > n / 2) d -= n;
  const j = frame.behind + d;
  if (j < 0 || j >= frame.count - 1) return false;
  const h = frame.heading[j] + (frame.heading[j + 1] - frame.heading[j]) * f;
  const cx = frame.px[j] + (frame.px[j + 1] - frame.px[j]) * f;
  const cy = frame.py[j] + (frame.py[j + 1] - frame.py[j]) * f;
  const cz = frame.pz[j] + (frame.pz[j + 1] - frame.pz[j]) * f;
  const xm = x * ROAD_HALF_WIDTH_M;
  out.x = cx + xm * Math.cos(h);
  out.y = cy;
  out.z = cz + xm * Math.sin(h);
  out.heading = h;
  return true;
}

/** Altura da pista (m) a `meters` da origem ao longo da janela (negativo = atrás), interpolada. */
export function frameYAt(frame: RoadFrame, meters: number): number {
  const t = frame.behind + meters / SEGMENT_M;
  const j = Math.max(0, Math.min(frame.count - 2, Math.floor(t)));
  const f = Math.max(0, Math.min(1, t - j));
  return frame.py[j] + (frame.py[j + 1] - frame.py[j]) * f;
}

const headingTables = new WeakMap<Track, Float64Array>();

/**
 * Rumo absoluto (rad) em `z`: integral da curvatura desde o início da pista. O que fica "no
 * mundo" (céu, sol, cordilheira) gira por ele. Tabela por segmento, cacheada por pista.
 */
export function absoluteHeading(track: Track, z: number): number {
  let table = headingTables.get(track);
  if (!table) {
    const segs = track.segments;
    table = new Float64Array(segs.length + 1);
    let h = 0;
    for (let i = 0; i < segs.length; i++) { table[i] = h; h += segs[i].curve * HEADING_PER_CURVE; }
    table[segs.length] = h;
    headingTables.set(track, table);
  }
  const n = track.segments.length;
  const zz = wrapZ(z, track.length);
  const si = Math.floor(zz / SEGMENT_LENGTH) % n;
  const f = (zz - si * SEGMENT_LENGTH) / SEGMENT_LENGTH;
  return table[si] + f * track.segments[si].curve * HEADING_PER_CURVE;
}
