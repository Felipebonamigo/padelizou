// Projeção pseudo-3D (modo Retrô) no molde do "Javascript Racer" (Jake Gordon) / "Lou's Pseudo 3d Page".
// Funções puras, sem DOM nem canvas: recebem a pista do núcleo e devolvem coordenadas de tela, para os
// testes poderem conferir a matemática e o renderizador (renderer.ts) só pintar.
//
// Mundo do Retrô: z ao longo da pista em unidades do núcleo (SEGMENT_LENGTH = 200 por segmento), x lateral
// em unidades de mundo (x normalizado do núcleo × ROAD_WIDTH; ±1 = bordas do asfalto) e y = y0/y1 dos
// segmentos (o builder já gera as alturas nessa escala, a mesma do Javascript Racer). A curva não tem
// coordenada: cada segmento desloca a estrada na tela acumulando `dx += curve` (a "curva falsa" clássica).
import { SEGMENT_LENGTH } from '../core/constants';
import type { Segment, Track } from '../core/types';

/** Meia largura da pista em unidades de mundo (x = ±1 do núcleo). */
export const ROAD_WIDTH = 2000;
/** Altura da câmera acima da pista. */
export const CAMERA_HEIGHT = 1000;
/** Campo de visão vertical, em graus. */
export const FIELD_OF_VIEW = 100;
/** 1 / tan(fov/2): distância do plano de projeção. */
export const CAMERA_DEPTH = 1 / Math.tan(((FIELD_OF_VIEW / 2) * Math.PI) / 180);
/** Distância entre a câmera e o carro do jogador (o carro fica sempre no mesmo ponto da tela). */
export const PLAYER_Z = CAMERA_HEIGHT * CAMERA_DEPTH;
/** Densidade da névoa (exponencial ao quadrado). */
export const FOG_DENSITY = 5;

/** Um ponto projetado: posição na tela, meia largura da pista nesse ponto e a escala (px por unidade × 2/largura). */
export interface ScreenPoint {
  x: number;
  y: number;
  /** Meia largura da pista na tela, em px. */
  w: number;
  /** CAMERA_DEPTH / z da câmera: multiplica tamanhos de mundo (× largura/2) para virar px. */
  scale: number;
  /** z relativo à câmera (≤ CAMERA_DEPTH = atrás ou colado na câmera). */
  cz: number;
}

/** Um segmento projetado, na ordem da câmera para longe (n = 0 é o mais perto). */
export interface ProjectedSegment {
  n: number;
  segment: Segment;
  /** Borda perto (início do segmento) e longe (fim). */
  p1: ScreenPoint;
  p2: ScreenPoint;
  /** A estrada deste segmento aparece (não está atrás da câmera nem de costas atrás de um morro). */
  visible: boolean;
  /** O segmento está na frente da câmera: seus sprites e carros podem ser desenhados. */
  inFront: boolean;
  /** 0 = sem névoa, 1 = névoa total. */
  fog: number;
}

export interface RoadCamera {
  /** z da câmera ao longo da pista (qualquer valor; é trazido para [0, length)). */
  z: number;
  /** x da câmera em unidades de mundo (x normalizado × ROAD_WIDTH). */
  x: number;
  /** y da câmera (altura da pista sob ela + CAMERA_HEIGHT). */
  y: number;
}

export interface Viewport { width: number; height: number }

/** z em [0, length). */
export function wrapZ(z: number, length: number): number {
  const w = z % length;
  return w < 0 ? w + length : w;
}

/** Interpolação linear. */
export function lerp(a: number, b: number, t: number): number {
  return a + (b - a) * t;
}

/** Altura da pista em z (interpola y0 → y1 dentro do segmento). */
export function roadYAt(track: Track, z: number): number {
  const zz = wrapZ(z, track.length);
  const seg = track.segments[Math.floor(zz / SEGMENT_LENGTH) % track.segments.length];
  return lerp(seg.y0, seg.y1, (zz % SEGMENT_LENGTH) / SEGMENT_LENGTH);
}

/**
 * Projeta um ponto do mundo (relativo à câmera) na tela — a fórmula clássica:
 * escala = profundidade / z; x_tela = W/2 + escala·x·W/2; y_tela = H/2 − escala·y·H/2.
 */
export function projectPoint(worldX: number, worldY: number, worldZ: number, cam: RoadCamera, view: Viewport, out: ScreenPoint): ScreenPoint {
  const cx = worldX - cam.x;
  const cy = worldY - cam.y;
  const cz = worldZ - cam.z;
  const scale = cz > 0 ? CAMERA_DEPTH / cz : 0;
  out.cz = cz;
  out.scale = scale;
  out.x = Math.round(view.width / 2 + scale * cx * (view.width / 2));
  out.y = Math.round(view.height / 2 - scale * cy * (view.height / 2));
  out.w = Math.round(scale * ROAD_WIDTH * (view.width / 2));
  return out;
}

/** Névoa exponencial: 1 perto, → 0 longe (o renderizador usa 1 − isto como quantidade de névoa). */
function fogFactor(distance: number, density: number): number {
  return 1 / Math.exp(distance * distance * density);
}

function emptyPoint(): ScreenPoint { return { x: 0, y: 0, w: 0, scale: 0, cz: 0 }; }

/**
 * Projeta `drawDistance` segmentos a partir do que contém a câmera, da frente para trás (índice 0 = perto).
 * Faz a curva falsa (x += dx; dx += curve), a altura por y0/y1, a volta da pista (segmentos depois da
 * linha somam `length` no z) e marca o que fica escondido atrás de morro. `out` é reaproveitado entre
 * quadros para não alocar.
 */
export function projectRoad(track: Track, cam: RoadCamera, view: Viewport, drawDistance: number, out: ProjectedSegment[] = []): ProjectedSegment[] {
  const segs = track.segments;
  const n = segs.length;
  const camZ = wrapZ(cam.z, track.length);
  const baseIndex = Math.floor(camZ / SEGMENT_LENGTH) % n;
  const basePercent = (camZ % SEGMENT_LENGTH) / SEGMENT_LENGTH;
  const camera: RoadCamera = { x: cam.x, y: cam.y, z: camZ };
  // O deslocamento da curva começa proporcional a quanto do segmento base já ficou para trás.
  let x = 0;
  let dx = -(segs[baseIndex].curve * basePercent);
  // Linha do horizonte da estrada já desenhada (y mínimo); serve só para descartar o que está atrás de morro.
  let maxY = view.height;
  const count = Math.min(drawDistance, n);
  out.length = count;
  for (let i = 0; i < count; i++) {
    const segment = segs[(baseIndex + i) % n];
    const looped = segment.index < baseIndex;
    const z1 = segment.z + (looped ? track.length : 0);
    const item = out[i] ?? { n: i, segment, p1: emptyPoint(), p2: emptyPoint(), visible: false, inFront: false, fog: 0 };
    item.n = i;
    item.segment = segment;
    // A linha central deste segmento está deslocada `x` (e o fim dele, `x + dx`) pela curva acumulada.
    projectPoint(x, segment.y0, z1, camera, view, item.p1);
    projectPoint(x + dx, segment.y1, z1 + SEGMENT_LENGTH, camera, view, item.p2);
    x += dx;
    dx += segment.curve;
    item.fog = 1 - fogFactor(i / count, FOG_DENSITY);
    item.inFront = item.p1.cz > CAMERA_DEPTH;
    // Atrás da câmera, de costas (descida depois da crista) ou todo abaixo da estrada já vista.
    item.visible = item.inFront && item.p2.y < item.p1.y && item.p2.y < maxY;
    if (item.visible) maxY = Math.min(maxY, item.p2.y);
    out[i] = item;
  }
  return out;
}

/** Onde (e em que escala) desenhar algo que está em (z, x normalizado) — carro, sprite — a partir da projeção. */
export interface Placement {
  /** Índice em `projected` do segmento que contém z. */
  n: number;
  x: number;
  y: number;
  scale: number;
}

/**
 * Coloca (z, x) na tela: acha o segmento projetado que contém z, interpola p1 → p2 pela fração e desloca
 * x normalizado × meia largura da pista projetada. Null se estiver fora da distância desenhada ou atrás da câmera.
 */
export function placeOnRoad(projected: readonly ProjectedSegment[], track: Track, z: number, xNorm: number, view: Viewport): Placement | null {
  if (projected.length === 0) return null;
  const first = projected[0].segment.index;
  const n = track.segments.length;
  const zz = wrapZ(z, track.length);
  const index = Math.floor(zz / SEGMENT_LENGTH) % n;
  const k = (index - first + n) % n;
  if (k >= projected.length) return null;
  const item = projected[k];
  if (!item.inFront) return null;
  const t = (zz % SEGMENT_LENGTH) / SEGMENT_LENGTH;
  const scale = lerp(item.p1.scale, item.p2.scale, t);
  const cx = lerp(item.p1.x, item.p2.x, t);
  return {
    n: k,
    x: cx + scale * xNorm * ROAD_WIDTH * (view.width / 2),
    y: lerp(item.p1.y, item.p2.y, t),
    scale,
  };
}

/** Tamanho na tela (px) de algo com `worldSize` unidades de mundo, na escala de projeção `scale`. */
export function screenSize(worldSize: number, scale: number, view: Viewport): number {
  return worldSize * scale * (view.width / 2);
}
