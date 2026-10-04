// Kit de modelagem procedural dos carros: um construtor de malha com "pincéis" (cor + como a face
// recebe a pintura da instância + material) e primitivas fechadas (cuboide, viga, torno, loft). Tudo
// em coordenadas do carro: x para a direita, y para cima, −z é a frente. Saída: BufferGeometry não
// indexada (cor, pintura e material por face, em Uint8 normalizado) com a normal suave do vinco de carro
// (`normals.ts`): a lataria é lisa à luz e os vincos de verdade continuam vivos.
// Puro (só Three, sem DOM): os testes montam os 13 modelos no Node.
import * as THREE from 'three';
import { CREASE, smoothNormals } from '../normals';

export type P3 = readonly [number, number, number];
export type P2 = readonly [number, number];

/** Como uma face é pintada e de que material ela é (ver o shader em material.ts). */
export interface Brush {
  /** Cor linear 0..1. Nas faces pintadas é o tom (branco = a cor da instância pura). */
  readonly rgb: readonly [number, number, number];
  /** 1 = recebe a cor da instância (pintura ou acento); 0 = cor fixa. */
  readonly paint: number;
  /** Peso do acento (0 = pintura, 1 = acento). */
  readonly accent: number;
  /** Camada de pintura (0 nenhuma, 1 = A, 2 = B, 3 = A ou B): a face vira a cor de acento quando a instância usa a camada. */
  readonly layer: 0 | 1 | 2 | 3;
  readonly rough: number;
  readonly metal: number;
  /** Emissivo de farol e de lanterna (0..1; o freio reforça a lanterna). */
  readonly head: number;
  readonly tail: number;
  /** 1 = sobe com o farol escamoteável (à noite). */
  readonly popup: number;
}

export interface BrushOptions {
  paint?: number; accent?: number; layer?: 0 | 1 | 2 | 3; rough?: number; metal?: number; head?: number; tail?: number; popup?: number;
}

/** Pincel a partir de uma cor sRGB em hex (convertida para linear, como o three faz com as cores de vértice). */
export function brush(hex: string, o: BrushOptions = {}): Brush {
  const c = new THREE.Color(hex);
  return {
    rgb: [c.r, c.g, c.b], paint: o.paint ?? 0, accent: o.accent ?? 0, layer: o.layer ?? 0,
    rough: o.rough ?? 0.6, metal: o.metal ?? 0, head: o.head ?? 0, tail: o.tail ?? 0, popup: o.popup ?? 0,
  };
}

/** Mesmo pincel com outras opções (ex.: a pintura numa camada de faixa). */
export function withBrush(b: Brush, o: Partial<Brush>): Brush {
  return { ...b, ...o };
}

const q8 = (v: number): number => Math.max(0, Math.min(255, Math.round(v * 255)));

export class MeshBuilder {
  private readonly pos: number[] = [];
  private readonly col: number[] = [];
  private readonly pnt: number[] = [];
  private readonly mat: number[] = [];

  get triangles(): number { return this.pos.length / 9; }

  private vertex(p: P3, b: Brush): void {
    this.pos.push(p[0], p[1], p[2]);
    this.col.push(b.rgb[0], b.rgb[1], b.rgb[2]);
    this.pnt.push(b.paint, b.accent, b.layer / 3, b.popup);
    this.mat.push(b.rough, b.metal, b.head, b.tail);
  }

  /** Triângulo com a frente no sentido anti-horário visto de fora. Degenerado (área ~0) é descartado. */
  tri(a: P3, b: P3, c: P3, br: Brush): void {
    const ux = b[0] - a[0]; const uy = b[1] - a[1]; const uz = b[2] - a[2];
    const vx = c[0] - a[0]; const vy = c[1] - a[1]; const vz = c[2] - a[2];
    const nx = uy * vz - uz * vy; const ny = uz * vx - ux * vz; const nz = ux * vy - uy * vx;
    if (nx * nx + ny * ny + nz * nz < 1e-14) return;
    this.vertex(a, br); this.vertex(b, br); this.vertex(c, br);
  }

  /** Quadrilátero a-b-c-d (anti-horário visto de fora); a diagonal mais curta vira a dobra. */
  quad(a: P3, b: P3, c: P3, d: P3, br: Brush): void {
    const ac = dist2(a, c); const bd = dist2(b, d);
    if (ac <= bd) { this.tri(a, b, c, br); this.tri(a, c, d, br); } else { this.tri(a, b, d, br); this.tri(b, c, d, br); }
  }

  /** Polígono convexo em leque (anti-horário visto de fora). */
  poly(pts: readonly P3[], br: Brush): void {
    for (let i = 1; i < pts.length - 1; i++) this.tri(pts[0], pts[i], pts[i + 1], br);
  }

  /** Constrói a peça e acrescenta a cópia espelhada em x (com o sentido das faces invertido). */
  mirrored(fn: () => void): void {
    const start = this.pos.length / 3;
    fn();
    const end = this.pos.length / 3;
    for (let v = start; v < end; v += 3) for (const k of [0, 2, 1]) this.copyVertex(v + k, true);
  }

  /** Constrói a peça e aplica `m` (rotação/translação/escala positiva) aos vértices dela. */
  transformed(m: THREE.Matrix4, fn: () => void): void {
    const start = this.pos.length / 3;
    fn();
    const v = new THREE.Vector3();
    for (let i = start; i < this.pos.length / 3; i++) {
      v.set(this.pos[i * 3], this.pos[i * 3 + 1], this.pos[i * 3 + 2]).applyMatrix4(m);
      this.pos[i * 3] = v.x; this.pos[i * 3 + 1] = v.y; this.pos[i * 3 + 2] = v.z;
    }
  }

  private copyVertex(i: number, flipX: boolean): void {
    this.pos.push(flipX ? -this.pos[i * 3] : this.pos[i * 3], this.pos[i * 3 + 1], this.pos[i * 3 + 2]);
    for (let k = 0; k < 3; k++) this.col.push(this.col[i * 3 + k]);
    for (let k = 0; k < 4; k++) { this.pnt.push(this.pnt[i * 4 + k]); this.mat.push(this.mat[i * 4 + k]); }
  }

  /** Posições cruas (para testes e medidas). */
  positions(): Float32Array { return new Float32Array(this.pos); }

  /** Monta a geometria; `crease` é o vinco (graus) da normal suave: o de carro, ou o de roda em wheels.ts. */
  build(crease: number = CREASE.car): THREE.BufferGeometry {
    const n = this.pos.length / 3;
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(this.pos, 3));
    const col = new Uint8Array(n * 3); const pnt = new Uint8Array(n * 4); const mat = new Uint8Array(n * 4);
    for (let i = 0; i < n * 3; i++) col[i] = q8(this.col[i]);
    for (let i = 0; i < n * 4; i++) { pnt[i] = q8(this.pnt[i]); mat[i] = q8(this.mat[i]); }
    g.setAttribute('color', new THREE.BufferAttribute(col, 3, true));
    g.setAttribute('aPaint', new THREE.BufferAttribute(pnt, 4, true));
    g.setAttribute('aMat', new THREE.BufferAttribute(mat, 4, true));
    smoothNormals(g, crease);
    g.computeBoundingBox(); g.computeBoundingSphere();
    return g;
  }
}

function dist2(a: P3, b: P3): number {
  const x = a[0] - b[0]; const y = a[1] - b[1]; const z = a[2] - b[2];
  return x * x + y * y + z * z;
}

/**
 * Hexaedro de 8 cantos quaisquer (pode ser afunilado ou inclinado). Índices num referencial destro
 * (s, v, u): 0 (−,−,−) 1 (+,−,−) 2 (+,+,−) 3 (−,+,−) 4 (−,−,+) 5 (+,−,+) 6 (+,+,+) 7 (−,+,+).
 * `faces` troca o pincel de uma face (nu = −u/frente, pu = +u, ns/ps = lados, nv = baixo, pv = cima).
 */
export type FaceKey = 'nu' | 'pu' | 'ns' | 'ps' | 'nv' | 'pv';
export function cuboid(b: MeshBuilder, c: readonly P3[], br: Brush, faces: Partial<Record<FaceKey, Brush | null>> = {}): void {
  const f = (k: FaceKey, i: number, j: number, l: number, m: number) => {
    const fb = faces[k];
    if (fb === null) return;
    b.quad(c[i], c[j], c[l], c[m], fb ?? br);
  };
  f('nu', 0, 3, 2, 1); f('pu', 4, 5, 6, 7); f('ns', 0, 4, 7, 3); f('ps', 1, 2, 6, 5); f('nv', 0, 1, 5, 4); f('pv', 3, 7, 6, 2);
}

/** Caixa alinhada aos eixos (s = x, v = y, u = z: `nu` é a face da frente, −z). */
export function box(b: MeshBuilder, center: P3, size: P3, br: Brush, faces: Partial<Record<FaceKey, Brush | null>> = {}): void {
  const [x, y, z] = center; const hx = size[0] / 2; const hy = size[1] / 2; const hz = size[2] / 2;
  cuboid(b, [
    [x - hx, y - hy, z - hz], [x + hx, y - hy, z - hz], [x + hx, y + hy, z - hz], [x - hx, y + hy, z - hz],
    [x - hx, y - hy, z + hz], [x + hx, y - hy, z + hz], [x + hx, y + hy, z + hz], [x - hx, y + hy, z + hz],
  ], br, faces);
}

/** Caixa com as faces para dentro e sem tampa (caçamba, cockpit aberto): o interior visto de cima. */
export function insideBox(b: MeshBuilder, center: P3, size: P3, br: Brush, faces: Partial<Record<FaceKey, Brush | null>> = {}): void {
  const [x, y, z] = center; const hx = size[0] / 2; const hy = size[1] / 2; const hz = size[2] / 2;
  // Cantos com x trocado: o determinante fica negativo e todas as faces viram para dentro.
  cuboid(b, [
    [x + hx, y - hy, z - hz], [x - hx, y - hy, z - hz], [x - hx, y + hy, z - hz], [x + hx, y + hy, z - hz],
    [x + hx, y - hy, z + hz], [x - hx, y - hy, z + hz], [x - hx, y + hy, z + hz], [x + hx, y + hy, z + hz],
  ], br, { pv: null, ...faces });
}

/** Viga de seção retangular (w × h) de p0 a p1; `up` orienta a altura. */
export function beam(b: MeshBuilder, p0: P3, p1: P3, w: number, h: number, br: Brush, up: P3 = [0, 1, 0], faces: Partial<Record<FaceKey, Brush | null>> = {}): void {
  const u = new THREE.Vector3(p1[0] - p0[0], p1[1] - p0[1], p1[2] - p0[2]).normalize();
  const upv = new THREE.Vector3(up[0], up[1], up[2]);
  const s = new THREE.Vector3().crossVectors(upv, u);
  if (s.lengthSq() < 1e-8) s.set(1, 0, 0); else s.normalize();
  const v = new THREE.Vector3().crossVectors(u, s);
  const corner = (p: P3, ss: number, vv: number): P3 => [p[0] + s.x * ss * w / 2 + v.x * vv * h / 2, p[1] + s.y * ss * w / 2 + v.y * vv * h / 2, p[2] + s.z * ss * w / 2 + v.z * vv * h / 2];
  cuboid(b, [corner(p0, -1, -1), corner(p0, 1, -1), corner(p0, 1, 1), corner(p0, -1, 1), corner(p1, -1, -1), corner(p1, 1, -1), corner(p1, 1, 1), corner(p1, -1, 1)], br, faces);
}

/** Referencial de um torno: origem, eixo e dois vetores com a × c = eixo. */
export interface LatheFrame { o: P3; axis: P3; a: P3; c: P3 }

export const AXIS_X: Omit<LatheFrame, 'o'> = { axis: [1, 0, 0], a: [0, 1, 0], c: [0, 0, 1] };
export const AXIS_Z: Omit<LatheFrame, 'o'> = { axis: [0, 0, 1], a: [1, 0, 0], c: [0, 1, 0] };
/** Eixo −z (a frente do carro): peças redondas olhando para a frente. */
export const AXIS_NZ: Omit<LatheFrame, 'o'> = { axis: [0, 0, -1], a: [0, 1, 0], c: [1, 0, 0] };
export const AXIS_Y: Omit<LatheFrame, 'o'> = { axis: [0, 1, 0], a: [0, 0, 1], c: [1, 0, 0] };

/**
 * Sólido de revolução: `profile` é uma lista de [raio, t] (t ao longo do eixo). Para fechar, comece e
 * termine com raio 0 (tampas no eixo). `brushAt(i)` pinta o trecho entre os pontos i e i+1.
 */
export function lathe(b: MeshBuilder, fr: LatheFrame, profile: readonly P2[], segs: number, brushAt: (i: number) => Brush, phase = 0.5): void {
  const pt = (r: number, t: number, k: number): P3 => {
    const th = ((k + phase) / segs) * Math.PI * 2;
    const ca = Math.cos(th) * r; const sa = Math.sin(th) * r;
    return [fr.o[0] + fr.axis[0] * t + fr.a[0] * ca + fr.c[0] * sa, fr.o[1] + fr.axis[1] * t + fr.a[1] * ca + fr.c[1] * sa, fr.o[2] + fr.axis[2] * t + fr.a[2] * ca + fr.c[2] * sa];
  };
  for (let i = 0; i < profile.length - 1; i++) {
    const [r0, t0] = profile[i]; const [r1, t1] = profile[i + 1];
    const br = brushAt(i);
    for (let k = 0; k < segs; k++) b.quad(pt(r0, t0, k), pt(r0, t0, k + 1), pt(r1, t1, k + 1), pt(r1, t1, k), br);
  }
}

/** Cilindro fechado ao longo do eixo do referencial, do t0 ao t1 (tampas com `cap`). */
export function cylinder(b: MeshBuilder, fr: LatheFrame, r: number, t0: number, t1: number, segs: number, br: Brush, capStart: Brush = br, capEnd: Brush = br): void {
  lathe(b, fr, [[0, t0], [r, t0], [r, t1], [0, t1]], segs, (i) => (i === 0 ? capStart : i === 2 ? capEnd : br));
}

/**
 * Loft entre anéis 2D (x, y) em estações z crescentes (anéis em sentido anti-horário vistos de +z:
 * a frente das faces fica para fora). `edgeBrush(k, i)` pinta a face entre os pontos k e k+1 dos anéis
 * i e i+1. `closed` liga o último ponto ao primeiro; `caps` fecha as pontas em leque.
 */
export function loft(b: MeshBuilder, rings: ReadonlyArray<readonly P2[]>, zs: readonly number[], edgeBrush: (k: number, i: number) => Brush | null, closed: boolean, capFront: Brush | null, capBack: Brush | null): void {
  const K = rings[0].length;
  const edges = closed ? K : K - 1;
  for (let i = 0; i < rings.length - 1; i++) {
    const a = rings[i]; const c = rings[i + 1];
    for (let k = 0; k < edges; k++) {
      const k1 = (k + 1) % K;
      const br = edgeBrush(k, i);
      if (!br) continue;
      b.quad([a[k][0], a[k][1], zs[i]], [a[k1][0], a[k1][1], zs[i]], [c[k1][0], c[k1][1], zs[i + 1]], [c[k][0], c[k][1], zs[i + 1]], br);
    }
  }
  const cap = (r: readonly P2[], z: number, front: boolean, br: Brush) => {
    let cx = 0; let cy = 0;
    for (const [x, y] of r) { cx += x; cy += y; }
    cx /= r.length; cy /= r.length;
    for (let k = 0; k < r.length; k++) {
      const k1 = (k + 1) % r.length;
      if (front) b.tri([cx, cy, z], [r[k1][0], r[k1][1], z], [r[k][0], r[k][1], z], br);
      else b.tri([cx, cy, z], [r[k][0], r[k][1], z], [r[k1][0], r[k1][1], z], br);
    }
  };
  if (capFront) cap(rings[0], zs[0], true, capFront);
  if (capBack) cap(rings[rings.length - 1], zs[zs.length - 1], false, capBack);
}

/** Prisma de um perfil 2D no plano (z, y), extrudado em x de x0 a x1 (aerofólio, barbatana, paredes). */
export function extrudeZY(b: MeshBuilder, profile: readonly P2[], x0: number, x1: number, br: Brush, side: Brush = br): void {
  // Perfil em (z, y), anti-horário visto de +x. Laterais em leque (perfil convexo), contorno em quads.
  const n = profile.length;
  let area = 0;
  for (let i = 0; i < n; i++) { const [z0, y0] = profile[i]; const [z1, y1] = profile[(i + 1) % n]; area += z0 * y1 - z1 * y0; }
  // Visto de +x com z para a direita e y para cima o sentido "anti-horário" tem área negativa neste
  // cálculo (o eixo z aponta para a esquerda de quem olha de +x); normaliza para a ordem certa.
  const pts = area > 0 ? [...profile].reverse() : [...profile];
  const at = (x: number, p: P2): P3 => [x, p[1], p[0]];
  for (let i = 1; i < n - 1; i++) {
    b.tri(at(x1, pts[0]), at(x1, pts[i]), at(x1, pts[i + 1]), side);
    b.tri(at(x0, pts[0]), at(x0, pts[i + 1]), at(x0, pts[i]), side);
  }
  for (let i = 0; i < n; i++) {
    const p = pts[i]; const q = pts[(i + 1) % n];
    b.quad(at(x0, p), at(x0, q), at(x1, q), at(x1, p), br);
  }
}

// ---------------------------------------------------------------------------------------------------------
// Peças chanfradas (onda I, "menos quadrado" pela geometria). A quina de 90° vira em DOIS passos de 30° (três
// dobras de 30°: lado → chanfro → chanfro → topo), e não num chanfro de 45°: com o vinco de carro em 45° a dobra
// de 45° fica no fio da navalha (lisa ou viva conforme o arredondamento do float32) e a luz sai manchada. Com 30°
// as três dobras ficam lisas com folga e a peça lê redonda à luz, com metade dos triângulos de um arco de verdade.
// ---------------------------------------------------------------------------------------------------------

/** Perfil unitário da quina de 90° (do lado ao topo, em volta do centro do raio): dobras de 30°, 30° e 30°. */
const BEVEL: readonly P2[] = [[1, 0], [1 / (1 + Math.tan(Math.PI / 6)), 1 / (1 + Math.tan(Math.PI / 6))], [0, 1]];

type FaceMap = Partial<Record<FaceKey, Brush | null>>;
type RectFace = 'nu' | 'pu' | 'nv' | 'pv';
/** Face de cada lado do retângulo de cantos chanfrados (12 pontos, anti-horário a partir do canto de baixo à direita). */
const RECT_FACE: readonly RectFace[] = ['nv', 'pu', 'pu', 'pu', 'pv', 'pv', 'pv', 'nu', 'nu', 'nu', 'nv', 'nv'];

/**
 * Retângulo de meias medidas (hu, hv) com os cantos chanfrados pelo `BEVEL` de raio r (anti-horário, 12 pontos,
 * do fundo à direita). `rho` (0..1) encolhe o chanfro para os anéis das pontas da caixa arredondada.
 */
function roundRect(hu: number, hv: number, r: number, rho = 1): P2[] {
  const out: P2[] = [];
  const corner = (su: number, sv: number, rev: boolean) => {
    const ou = su * (hu - r); const ov = sv * (hv - r);
    for (let k = 0; k < 3; k++) {
      const [pu, pv] = BEVEL[rev ? 2 - k : k];
      out.push([ou + su * r * rho * pu, ov + sv * r * rho * pv]);
    }
  };
  corner(1, -1, true); corner(1, 1, false); corner(-1, 1, true); corner(-1, -1, false);
  return out;
}

/** Eixo de uma barra: o loft corre em w; (u, v, w) → mundo por permutação cíclica (o sentido das faces fica). */
const BAR_AXES = {
  z: { m: [1, 0, 0, 0, 1, 0, 0, 0, 1], keys: { nu: 'ns', pu: 'ps', nv: 'nv', pv: 'pv', nw: 'nu', pw: 'pu' } },
  x: { m: [0, 0, 1, 1, 0, 0, 0, 1, 0], keys: { nu: 'nv', pu: 'pv', nv: 'nu', pv: 'pu', nw: 'ns', pw: 'ps' } },
  y: { m: [0, 1, 0, 0, 0, 1, 1, 0, 0], keys: { nu: 'nu', pu: 'pu', nv: 'ns', pv: 'ps', nw: 'nv', pw: 'pv' } },
} as const;

function axisSizes(axis: 'x' | 'y' | 'z', s: P3): P3 {
  return axis === 'z' ? [s[0], s[1], s[2]] : axis === 'x' ? [s[1], s[2], s[0]] : [s[2], s[0], s[1]];
}

function placeAxis(b: MeshBuilder, axis: 'x' | 'y' | 'z', c: P3, fn: () => void): void {
  const [a, bb, cc, d, e, f, g, h, i] = BAR_AXES[axis].m;
  b.transformed(new THREE.Matrix4().set(a, bb, cc, c[0], d, e, f, c[1], g, h, i, c[2], 0, 0, 0, 1), fn);
}

const pick = (faces: FaceMap, k: FaceKey, br: Brush): Brush | null => { const f = faces[k]; return f === null ? null : f ?? br; };

export interface RoundBarOptions {
  /** Raio do chanfro (limitado à metade da menor medida da seção). */
  r: number;
  /** Eixo ao longo do qual a barra corre (a seção redonda fica no plano dos outros dois); padrão x. */
  axis?: 'x' | 'y' | 'z';
  faces?: FaceMap;
}

/**
 * Barra de seção arredondada (para-choque, lâmina, carcaça de farol, retrovisor): as quatro arestas ao longo do
 * eixo chanfradas em dois passos de 30° (lisas no vinco de carro), as pontas planas e vivas. 48 triângulos
 * (a caixa tem 12). Mesma caixa por fora que `box` com o mesmo centro e medidas; `faces` como em `box`.
 */
export function roundBar(b: MeshBuilder, center: P3, size: P3, br: Brush, o: RoundBarOptions): void {
  const axis = o.axis ?? 'x';
  const [su, sv, sw] = axisSizes(axis, size);
  const r = Math.max(0, Math.min(o.r, su / 2, sv / 2));
  const keys = BAR_AXES[axis].keys; const faces = o.faces ?? {};
  const ring = roundRect(su / 2, sv / 2, r);
  placeAxis(b, axis, center, () => {
    loft(b, [ring, ring], [-sw / 2, sw / 2], (k) => pick(faces, keys[RECT_FACE[k]], br), true, pick(faces, keys.nw, br), pick(faces, keys.pw, br));
  });
}

/** Viga de seção arredondada (w × h, cantos de raio r) de p0 a p1, como `beam`: para-choque cromado, santantônio. */
export function roundBeam(b: MeshBuilder, p0: P3, p1: P3, w: number, h: number, br: Brush, r: number, up: P3 = [0, 1, 0]): void {
  const d = new THREE.Vector3(p1[0] - p0[0], p1[1] - p0[1], p1[2] - p0[2]);
  const len = d.length();
  if (len < 1e-9) return;
  const u = d.clone().divideScalar(len);
  const s = new THREE.Vector3().crossVectors(new THREE.Vector3(up[0], up[1], up[2]), u);
  if (s.lengthSq() < 1e-8) s.set(1, 0, 0); else s.normalize();
  const v = new THREE.Vector3().crossVectors(u, s);
  const m = new THREE.Matrix4().makeBasis(s, v, u).setPosition(p0[0], p0[1], p0[2]);
  const ring = roundRect(w / 2, h / 2, Math.max(0, Math.min(r, w / 2, h / 2)));
  b.transformed(m, () => loft(b, [ring, ring], [0, len], () => br, true, br, br));
}

/**
 * Caixa arredondada em todas as arestas e cantos (tomada de ar, carcaça, peça de destaque): o mesmo perfil de dois
 * passos de 30° nas 12 arestas, sem nenhuma aresta viva. Até 112 triângulos — para poucas peças por carro; a barra
 * (`roundBar`) é a de uso geral. Mesma caixa por fora que `box`; `faces` como em `box` (o chanfro fica com a face
 * mais perto dele).
 */
export function roundBox(b: MeshBuilder, center: P3, size: P3, br: Brush, r: number, faces: FaceMap = {}): void {
  const [hx, hy, hz] = [size[0] / 2, size[1] / 2, size[2] / 2];
  const rr = Math.max(0, Math.min(r, hx, hy, hz));
  // Estações em z: a ponta da frente (tampa → começo do lado), o meio, a ponta de trás.
  const prof = BEVEL; // [ρ, τ]: ρ = quanto do chanfro o anel já abriu, τ = quanto a estação recua da face
  const rings: P2[][] = []; const zs: number[] = [];
  for (let k = 2; k >= 0; k--) { rings.push(roundRect(hx, hy, rr, prof[k][0])); zs.push(center[2] - (hz - rr) - rr * prof[k][1]); }
  for (let k = 0; k <= 2; k++) { rings.push(roundRect(hx, hy, rr, prof[k][0])); zs.push(center[2] + (hz - rr) + rr * prof[k][1]); }
  const keys = BAR_AXES.z.keys;
  const shifted = rings.map((ring) => ring.map(([x, y]) => [x + center[0], y + center[1]] as P2));
  loft(b, shifted, zs, (k, i) => {
    if (i === 0) return pick(faces, 'nu', br);
    if (i === 4) return pick(faces, 'pu', br);
    return pick(faces, keys[RECT_FACE[k]], br);
  }, true, pick(faces, 'nu', br), pick(faces, 'pu', br));
}

/**
 * Perfil 2D com os cantos arredondados (filete de raio r em arco, passos de até 30°: lisos no vinco de carro). O
 * raio encolhe onde o lado é curto (o filete usa até 45% de cada lado). `radii` dá um raio por canto.
 */
export function fillet(poly: readonly P2[], r: number | readonly number[], maxStep = Math.PI / 6): P2[] {
  const n = poly.length;
  const out: P2[] = [];
  for (let i = 0; i < n; i++) {
    const p = poly[(i + n - 1) % n]; const v = poly[i]; const q = poly[(i + 1) % n];
    const ax = v[0] - p[0]; const ay = v[1] - p[1]; const bx = q[0] - v[0]; const by = q[1] - v[1];
    const la = Math.hypot(ax, ay); const lb = Math.hypot(bx, by);
    const ri = typeof r === 'number' ? r : r[i];
    const turn = Math.acos(Math.max(-1, Math.min(1, (ax * bx + ay * by) / (la * lb))));
    if (ri <= 0 || la < 1e-9 || lb < 1e-9 || turn < 1e-3) { out.push(v); continue; }
    const t = Math.min(ri * Math.tan(turn / 2), la * 0.45, lb * 0.45);
    const steps = Math.max(1, Math.ceil(turn / maxStep));
    const p0: P2 = [v[0] - (ax / la) * t, v[1] - (ay / la) * t];
    const p2: P2 = [v[0] + (bx / lb) * t, v[1] + (by / lb) * t];
    // Arco de p0 a p2 tangente aos dois lados: centro na normal de p0 para o lado de dentro da curva.
    const cross = ax * by - ay * bx;
    const side = cross > 0 ? 1 : -1;
    const rad = t / Math.tan(turn / 2);
    const cx = p0[0] - (ay / la) * rad * side; const cy = p0[1] + (ax / la) * rad * side;
    const a0 = Math.atan2(p0[1] - cy, p0[0] - cx);
    for (let k = 0; k <= steps; k++) {
      const a = a0 + side * turn * (k / steps);
      out.push(k === 0 ? p0 : k === steps ? p2 : [cx + Math.cos(a) * rad, cy + Math.sin(a) * rad]);
    }
  }
  return out;
}

/** Volume assinado (positivo = faces para fora). Para testes das primitivas. */
export function signedVolume(pos: ArrayLike<number>): number {
  let v = 0;
  for (let i = 0; i < pos.length; i += 9) {
    const ax = pos[i]; const ay = pos[i + 1]; const az = pos[i + 2];
    const bx = pos[i + 3]; const by = pos[i + 4]; const bz = pos[i + 5];
    const cx = pos[i + 6]; const cy = pos[i + 7]; const cz = pos[i + 8];
    v += ax * (by * cz - bz * cy) - ay * (bx * cz - bz * cx) + az * (bx * cy - by * cx);
  }
  return v / 6;
}
