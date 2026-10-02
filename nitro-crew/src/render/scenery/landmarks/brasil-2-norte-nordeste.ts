// Marcos da segunda leva do Brasil (EXTRA_BRAZIL_PLACES em src/core/data/places.ts), Norte e Nordeste.
// Convenção dos modelos: landmarks/types.ts — origem no centro da pegada, y = 0 no chão (alicerce e saia de pedra
// abaixo de 0 cobrem o declive), frente para +X (a pista), o comprido (ponte, falésia, cânion) ao longo de Z.
// Escala real (skyline: já grande), low-poly de faces planas e cor chapada; luz (`glow`) onde há luz de verdade à
// noite (pontes, Marco Zero, Parque do Povo, Bumbódromo), `beacon` no topo do que é alto. Sem Math.random: hash.
// O que é baixo e largo (geoglifo, búfalos, palafitas) vai perto e ganha uma estrutura que lê de pé (torre de
// observação, mangueira, açaizeiros).
import * as THREE from 'three';
import { hash2, hash3 } from '../../noise';
import { box, cone, cyl, dodeca, frond, gable, hip, ico, jitter, merge, paint, shadeY, speckle, sphere, tf, tintUp, tris, type Geo, type MatKey, type Model, type ModelPart } from '../geom';
import { FACADE_TILE } from '../structures';
import type { LandmarkDef, LandmarkRegistry } from './types';

// ───────────────────────────── Kit ─────────────────────────────

type FacadeStyle = keyof typeof FACADE_TILE;
type V3 = [number, number, number];
const UP = new THREE.Vector3(0, 1, 0);
const PI = Math.PI;

/** Partes de um marco, separadas por material (sem sombra própria: o marco fica longe do mapa de sombra). */
class Kit {
  readonly flat: Geo[] = [];
  readonly glow: Geo[] = [];
  readonly beacon: Geo[] = [];
  readonly facade = new Map<MatKey, Geo[]>();

  add(g: Geo, color: THREE.ColorRepresentation, m?: THREE.Matrix4): this { this.flat.push(paint(g, color, m)); return this; }
  /** Geometria já pintada (recolorida por triângulo, deformada...). */
  raw(g: Geo): this { this.flat.push(g); return this; }
  light(g: Geo, color: THREE.ColorRepresentation, m?: THREE.Matrix4): this { this.glow.push(paint(g, color, m)); return this; }
  blink(g: Geo, color: THREE.ColorRepresentation, m?: THREE.Matrix4): this { this.beacon.push(paint(g, color, m)); return this; }

  /** Caixa com a textura de janelas (acende à noite); UV em metros como em structures.ts. */
  fac(style: FacadeStyle, w: number, h: number, d: number, color: string, m: THREE.Matrix4, vOffset = 0): this {
    const g = new THREE.BoxGeometry(w, h, d);
    const uv = g.attributes.uv as THREE.BufferAttribute;
    const [tu, tv] = FACADE_TILE[style];
    const faces: Array<[number, number] | null> = [[d, h], [d, h], null, null, [w, h], [w, h]];
    for (let f = 0; f < 6; f++) {
      const s = faces[f];
      for (let q = 0; q < 4; q++) {
        const i = f * 4 + q;
        if (!s) uv.setXY(i, 0.01, 0.01);
        else uv.setXY(i, (uv.getX(i) * s[0]) / tu, (uv.getY(i) * s[1] + vOffset) / tv);
      }
    }
    const list = this.facade.get(style) ?? [];
    list.push(paint(g, color, m, true));
    this.facade.set(style, list);
    return this;
  }

  /** Barra (caixa) de `a` até `b`, seção w × d. */
  beam(a: V3, b: V3, w: number, color: THREE.ColorRepresentation, d = w): this {
    return this.add(box(w, 1, d), color, segMatrix(a, b));
  }
  rod(a: V3, b: V3, r: number, color: THREE.ColorRepresentation, seg = 5): this {
    return this.add(cyl(r, r, 1, seg, true), color, segMatrix(a, b));
  }
  /** Fio de luz (cabo estaiado aceso, cordão de lâmpadas): prisma de 3 lados no `glow`. */
  wire(a: V3, b: V3, t: number, color: THREE.ColorRepresentation): this {
    this.glow.push(paint(cyl(0.5, 0.5, 1, 3, true), color, segMatrix(a, b).multiply(new THREE.Matrix4().makeScale(t, 1, t))));
    return this;
  }

  /** Monta o Model: sombreado por altura no `flat` (pé mais escuro, topo mais claro). */
  model(height: number, shade: [number, number] = [0.8, 1.06]): Model {
    const parts: ModelPart[] = [];
    if (this.flat.length) parts.push({ geometry: shadeY(merge(this.flat), 0, height, shade[0], shade[1]), mat: 'flat' });
    for (const [mat, list] of this.facade) parts.push({ geometry: merge(list), mat });
    if (this.glow.length) parts.push({ geometry: merge(this.glow), mat: 'glow' });
    if (this.beacon.length) parts.push({ geometry: merge(this.beacon), mat: 'beacon' });
    return { parts, blob: 0 };
  }
}

/** Matriz que leva uma peça unitária em Y (centrada na origem) para o segmento a → b. */
function segMatrix(a: V3, b: V3): THREE.Matrix4 {
  const d = new THREE.Vector3(b[0] - a[0], b[1] - a[1], b[2] - a[2]);
  const len = d.length();
  const q = new THREE.Quaternion().setFromUnitVectors(UP, d.normalize());
  return new THREE.Matrix4().compose(new THREE.Vector3((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, (a[2] + b[2]) / 2), q, new THREE.Vector3(1, len, 1));
}

/** Perfil 2D (x, y) extrudado em +Z de 0 a `depth` (com furos opcionais). */
function extrude(pts: Array<[number, number]>, depth: number, holes: Array<Array<[number, number]>> = [], steps = 1): Geo {
  const shape = new THREE.Shape(pts.map(([x, y]) => new THREE.Vector2(x, y)));
  for (const h of holes) shape.holes.push(new THREE.Path(h.map(([x, y]) => new THREE.Vector2(x, y))));
  return new THREE.ExtrudeGeometry(shape, { depth, bevelEnabled: false, curveSegments: 1, steps });
}

/** Planta (x, z) erguida de y = 0 a `h` (paredes verticais), com furos (pátios). */
function plan(pts: Array<[number, number]>, h: number, holes: Array<Array<[number, number]>> = [], steps = 1): Geo {
  const flip = (p: Array<[number, number]>): Array<[number, number]> => p.map(([x, z]) => [x, -z]);
  return extrude(flip(pts), h, holes.map(flip), steps).rotateX(-PI / 2);
}

/** Recolore cada triângulo pela posição do centro (listras, estratos, a boca da pedra). */
function recolor(g: Geo, f: (x: number, y: number, z: number, tri: number) => THREE.ColorRepresentation | null): Geo {
  const p = g.attributes.position as THREE.BufferAttribute;
  const c = g.attributes.color as THREE.BufferAttribute;
  const col = new THREE.Color();
  for (let i = 0; i + 2 < p.count; i += 3) {
    const x = (p.getX(i) + p.getX(i + 1) + p.getX(i + 2)) / 3;
    const y = (p.getY(i) + p.getY(i + 1) + p.getY(i + 2)) / 3;
    const z = (p.getZ(i) + p.getZ(i + 1) + p.getZ(i + 2)) / 3;
    const out = f(x, y, z, i / 3);
    if (out === null) continue;
    col.set(out);
    for (let q = 0; q < 3; q++) c.setXYZ(i + q, col.r, col.g, col.b);
  }
  return g;
}

/** Estratos de rocha: faixa por altura (com um desvio por hash na borda). */
function strata(g: Geo, colors: string[], step: number, seed: number): Geo {
  return recolor(g, (x, y, z) => {
    const wob = (hash3(Math.round(x / 9), Math.round(z / 9), seed) - 0.5) * step * 0.5;
    return colors[((Math.floor((y + wob) / step) % colors.length) + colors.length) % colors.length];
  });
}

/** Triângulo de dois lados (bandeirinha, vela): a malha não depende do lado da câmera. */
function sheet(a: V3, b: V3, c: V3): Geo {
  return tris([...a, ...b, ...c, ...a, ...c, ...b]);
}

/** Polígono irregular de n pontas em torno da origem (raio com desvio por hash). */
function blobPoly(n: number, rx: number, rz: number, amount: number, seed: number): Array<[number, number]> {
  const out: Array<[number, number]> = [];
  for (let i = 0; i < n; i++) {
    const a = (i / n) * PI * 2;
    const f = 1 + (hash2(seed, i) - 0.5) * 2 * amount;
    out.push([Math.cos(a) * rx * f, Math.sin(a) * rz * f]);
  }
  return out;
}

/** Elipse (x, z) de n pontos. */
function ellipse(n: number, rx: number, rz: number, x = 0, z = 0): Array<[number, number]> {
  const out: Array<[number, number]> = [];
  for (let i = 0; i < n; i++) { const a = (i / n) * PI * 2; out.push([x + Math.cos(a) * rx, z + Math.sin(a) * rz]); }
  return out;
}

/** Espelho d'água / rio: laje rasa irregular de cor chapada, 0,1 m acima do chão. */
function water(k: Kit, x: number, z: number, rx: number, rz: number, color: string, seed: number, n = 12): void {
  k.add(plan(blobPoly(n, rx, rz, 0.1, seed), 0.15), color, tf(x, 0.02, z));
}

/** Morro facetado (icosaedro deformado), meia esfera de cima vira o morro, a de baixo a saia sob o chão. */
function hill(rx: number, h: number, rz: number, seed: number, rock: string, top: string | null, detail = 1, amount = 0.16, skirt = 0.25, m?: THREE.Matrix4): Geo {
  const g = jitter(new THREE.IcosahedronGeometry(1, detail), amount, seed);
  const p = g.attributes.position as THREE.BufferAttribute;
  for (let i = 0; i < p.count; i++) {
    const y = p.getY(i);
    p.setXYZ(i, p.getX(i) * rx, y >= 0 ? y * h : y * h * skirt, p.getZ(i) * rz);
  }
  const out = paint(g, rock, m);
  if (top) tintUp(out, top, 0.55);
  return out;
}

/** Copa de árvore: massa achatada (icosaedro jitterado) com tom variado por face. */
function crown(k: Kit, x: number, y: number, z: number, rx: number, ry: number, rz: number, color: string, seed: number, detail = 0): void {
  k.raw(speckle(paint(jitter(ico(1, detail), 0.16, seed), color, tf(x, y, z, rx, ry, rz)), 0.09, seed));
}

/** Árvore genérica de mata (tronco + 1–2 copas). */
function tree(k: Kit, x: number, z: number, h: number, seed: number, greens = ['#3f7f34', '#4a8f3a', '#356f2e']): void {
  k.rod([x, -0.5, z], [x, h * 0.6, z], Math.max(0.2, h * 0.03), '#6a5038', 5);
  crown(k, x, h * 0.72, z, h * 0.32, h * 0.26, h * 0.32, greens[Math.floor(hash2(seed, 1) * greens.length)], seed);
}

// ───────────────────────────── Peças reaproveitadas ─────────────────────────────

/** Coqueiro (12–22 m): tronco anelado e inclinado, folhas caídas, cocos. */
function palm(k: Kit, x: number, z: number, H: number, seed: number, y0 = 0, nf = 8): void {
  const dir = hash2(seed, 2) * PI * 2;
  const leanM = H * (0.06 + hash2(seed, 3) * 0.14);
  const cd = Math.cos(dir); const sd = Math.sin(dir);
  let prev: V3 = [x, y0 - 0.5, z];
  for (let q = 1; q <= 3; q++) {
    const t = q / 3;
    const o = leanM * t * t;
    const next: V3 = [x + o * cd, y0 + t * H, z + o * sd];
    k.rod(prev, next, 0.34 - 0.1 * t, q % 2 ? '#8a6a3f' : '#7a5c36', 5);
    prev = next;
  }
  const [tx, ty, tz] = prev;
  const s = H / 16;
  for (let q = 0; q < nf; q++) {
    const a = (q / nf) * PI * 2 + hash2(seed, q) * 0.5;
    k.add(frond(4.6 * s + hash2(seed, q + 20) * s, 1.3 * s, a, 1.0 * s, 3.4 * s, 3), q % 2 ? '#3f9a3a' : '#52ae44', tf(tx, ty, tz));
  }
  for (let q = 0; q < 3; q++) k.add(new THREE.OctahedronGeometry(0.32 * s, 0), '#6b4a22', tf(tx + Math.cos(q * 2.1) * 0.4 * s, ty - 0.5 * s, tz + Math.sin(q * 2.1) * 0.4 * s));
}

/** Açaizeiro: estipe fino e reto (12–18 m), poucas folhas finas e o cacho roxo. */
function acai(k: Kit, x: number, z: number, H: number, seed: number): void {
  const lx = (hash2(seed, 1) - 0.5) * H * 0.12; const lz = (hash2(seed, 2) - 0.5) * H * 0.12;
  k.rod([x, -0.5, z], [x + lx, H, z + lz], 0.13, '#7a6a52', 4);
  for (let q = 0; q < 6; q++) {
    const a = (q / 6) * PI * 2 + hash2(seed, q) * 0.6;
    k.add(frond(3.6, 0.8, a, 0.9, 2.6, 2), q % 2 ? '#3f8a3a' : '#4f9a40', tf(x + lx, H, z + lz));
  }
  k.add(new THREE.OctahedronGeometry(0.5, 0), '#4a2a4a', tf(x + lx + 0.3, H - 1.0, z + lz));
}

/** Torre de farol cilíndrica com faixas, varanda, lanterna (acesa) e cúpula; luz de balizamento no topo. */
function lightTower(k: Kit, x: number, z: number, y0: number, H: number, r0: number, r1: number, colors: string[], bands: number, cap: string): number {
  for (let q = 0; q < bands; q++) {
    const t0 = q / bands; const t1 = (q + 1) / bands;
    k.add(cyl(r0 + (r1 - r0) * t1, r0 + (r1 - r0) * t0, H / bands, 10), colors[q % colors.length], tf(x, y0 + ((t0 + t1) / 2) * H, z));
  }
  const top = y0 + H;
  k.add(cyl(r1 + 0.9, r1 + 0.6, 0.5, 10), '#2e3238', tf(x, top + 0.25, z));
  k.add(cyl(r1 + 0.9, r1 + 0.9, 0.9, 10, true), '#2e3238', tf(x, top + 0.95, z));
  k.light(cyl(r1 * 0.65, r1 * 0.65, 2.2, 8), '#fff1b8', tf(x, top + 2.0, z));
  k.add(cone(r1 * 0.9, 1.8, 8), cap, tf(x, top + 4.0, z));
  k.blink(sphere(0.3, 6, 4), '#ff3a2a', tf(x, top + 5.1, z));
  return top + 5.4;
}

/**
 * Igreja colonial com a fachada em +X na posição (x, y, z): nave de duas águas, frontão, uma ou duas torres
 * (pirâmide ou bulbo), porta e janelas. `s` = escala.
 */
function church(k: Kit, x: number, y: number, z: number, o: { wall: string; trim: string; towers: 1 | 2; cap: 'pyramid' | 'bulb'; s?: number; lit?: boolean; roof?: string }): void {
  const s = o.s ?? 1;
  const D = 22 * s; const W = 12 * s; const H = 12 * s;
  k.add(box(D, H + 2, W), o.wall, tf(x - D / 2, y + H / 2 - 1, z));
  k.add(gable(W, 4.2 * s, D, 0.4 * s), o.roof ?? '#b8552e', tf(x - D / 2, y + H, z, 1, 1, 1, 0, PI / 2, 0));
  k.add(box(1.2 * s, H + 3 * s, W + 0.6 * s), o.wall, tf(x - 0.4 * s, y + (H + 3 * s) / 2 - 1, z));
  k.add(gable(W + 0.6 * s, 4.6 * s, 1.2 * s), o.wall, tf(x - 0.4 * s, y + H + 2 * s, z, 1, 1, 1, 0, PI / 2, 0));
  k.add(box(1.5 * s, 0.6 * s, W + 1.2 * s), o.trim, tf(x - 0.3 * s, y + H + 1.8 * s, z));
  for (const dz of [-1, 1]) k.add(box(1.4 * s, H + 2 * s, 0.8 * s), o.trim, tf(x - 0.3 * s, y + (H + 2 * s) / 2 - 1, z + dz * (W / 2 + 0.1)));
  k.add(box(0.2, 2.9 * s, 0.35 * s), o.trim, tf(x - 0.3 * s, y + H + 7.6 * s, z));
  k.add(box(0.2, 0.3 * s, 1.6 * s), o.trim, tf(x - 0.3 * s, y + H + 8.2 * s, z));
  k.add(box(0.3, 4.2 * s, 2.6 * s), '#3a2a20', tf(x + 0.25 * s, y + 2.1 * s, z));
  k.add(box(0.3, 4.6 * s, 2.9 * s), o.trim, tf(x + 0.15 * s, y + 2.3 * s, z));
  for (const dz of [-3.4, 3.4]) k.add(box(0.3, 2.2 * s, 1.3 * s), '#2a3442', tf(x + 0.25 * s, y + 7.2 * s, z + dz * s));
  const choir = box(0.3, 2.0 * s, 1.6 * s);
  if (o.lit) k.light(choir, '#ffcf80', tf(x + 0.28 * s, y + 7.6 * s, z)); else k.add(choir, '#2a3442', tf(x + 0.28 * s, y + 7.6 * s, z));
  const tz = o.towers === 2 ? [-(W / 2 + 1.6 * s), W / 2 + 1.6 * s] : [W / 2 + 1.6 * s];
  for (const dz of tz) {
    const TH = H + 9 * s; const tw = 4.4 * s;
    k.add(box(tw, TH + 1, tw), o.wall, tf(x - tw / 2 - 0.2 * s, y + TH / 2 - 1, z + dz));
    k.add(box(tw + 0.5 * s, 0.6 * s, tw + 0.5 * s), o.trim, tf(x - tw / 2 - 0.2 * s, y + H + 1.8 * s, z + dz));
    k.add(box(tw + 0.5 * s, 0.6 * s, tw + 0.5 * s), o.trim, tf(x - tw / 2 - 0.2 * s, y + TH, z + dz));
    k.add(box(0.3, 2.4 * s, 1.6 * s), '#2a2a2e', tf(x + 0.05 * s, y + TH - 2.3 * s, z + dz));
    if (o.cap === 'pyramid') k.add(hip(tw, 4.6 * s, tw), o.trim, tf(x - tw / 2 - 0.2 * s, y + TH + 0.3 * s, z + dz));
    else {
      k.add(sphere(tw * 0.55, 8, 4, 0, PI * 2, 0, PI / 2), o.trim, tf(x - tw / 2 - 0.2 * s, y + TH + 0.3 * s, z + dz, 1, 1.4, 1));
      k.add(cone(0.6 * s, 2.4 * s, 6), o.trim, tf(x - tw / 2 - 0.2 * s, y + TH + 4.0 * s, z + dz));
    }
  }
}

/** Casinha térrea de cor viva, telhado de duas águas ao longo de Z, porta e janela para +X. */
function cottage(k: Kit, x: number, z: number, w: number, d: number, h: number, color: string, yaw = 0, roof = '#b8552e'): void {
  const c = Math.cos(yaw); const s = Math.sin(yaw);
  const at = (lx: number, lz: number): [number, number] => [x + lx * c + lz * s, z - lx * s + lz * c];
  k.add(box(w, h + 1, d), color, tf(x, (h - 1) / 2, z, 1, 1, 1, 0, yaw, 0));
  k.add(gable(w + 0.6, h * 0.5, d + 0.6), roof, tf(x, h, z, 1, 1, 1, 0, yaw, 0));
  const [px, pz] = at(w / 2 + 0.05, -d * 0.2);
  k.add(box(0.2, 2.2, 1.1), '#3a2a20', tf(px, 1.1, pz, 1, 1, 1, 0, yaw, 0));
  const [wx, wz] = at(w / 2 + 0.05, d * 0.22);
  k.add(box(0.2, 1.1, 1.2), '#2a3a5a', tf(wx, h * 0.55, wz, 1, 1, 1, 0, yaw, 0));
}

/** Mesa de rocha (chapada): talude de cerrado e paredão vertical em estratos, topo com mato. */
function mesa(k: Kit, o: { rx: number; rz: number; H: number; talus: number; seed: number; n: number; rock: string[]; top: string; slope: string; step: number; x?: number; z?: number }): void {
  const x = o.x ?? 0; const z = o.z ?? 0;
  const poly = blobPoly(o.n, o.rx, o.rz, 0.12, o.seed);
  const cliffH = o.H - o.talus * 0.5;
  const cliff = plan(poly, cliffH, [], Math.max(2, Math.round(cliffH / o.step)));
  jitter(cliff, 0.012, o.seed);
  const cp = paint(cliff, o.rock[0], tf(x, o.talus * 0.5, z));
  strata(cp, o.rock, o.step, o.seed);
  k.raw(tintUp(cp, o.top, 0.7, 1));
  const tal = jitter(cyl(0.98, 1.35, 1, o.n, true), 0.08, o.seed + 1, true);
  k.raw(speckle(paint(tal, o.slope, tf(x, o.talus / 2 - 1, z, o.rx, o.talus + 2, o.rz)), 0.07, o.seed));
}

/** Duna: meia elipse com a face de escorregamento (−X) mais curta e íngreme, ondulada por hash. */
function dune(k: Kit, x: number, z: number, rx: number, h: number, rz: number, color: string, crest: string, seed: number, yaw = 0): void {
  const g = sphere(1, 14, 6, 0, PI * 2, 0, PI / 2);
  const p = g.attributes.position as THREE.BufferAttribute;
  for (let i = 0; i < p.count; i++) {
    const vx = p.getX(i); const vy = p.getY(i);
    p.setXYZ(i, (vx < 0 ? vx * 0.5 : vx) - vy * 0.25, Math.pow(vy, 1.3), p.getZ(i));
  }
  jitter(g, 0.05, seed, true);
  const pg = paint(g, color, tf(x, -0.3, z, rx, h, rz, 0, yaw, 0));
  k.raw(speckle(tintUp(pg, crest, 0.9, 0.6), 0.05, seed));
}

/**
 * Paredão de arenito ao longo de Z (face em +X, topo em y = n · step): estratos horizontais e, em cada estrato,
 * blocos de `chunk` m em Z com profundidade e recuo por hash (sulcos de erosão). `half(y)` = meia largura em Z;
 * `hole(y)` abre um vão no meio (cachoeira, cânion).
 */
function cliffWall(k: Kit, o: { x: number; depth: number; step: number; n: number; half: (y: number) => number; chunk: number; colors: string[]; seed: number; hole?: (y: number) => number; recess?: number; cap?: string }): void {
  for (let i = 0; i < o.n; i++) {
    const ym = (i + 0.5) * o.step;
    const hw = o.half(ym);
    if (hw <= 1) continue;
    const hz = o.hole ? o.hole(ym) : 0;
    const cnt = Math.max(1, Math.round((2 * hw) / o.chunk));
    const len = (2 * hw) / cnt;
    const base = o.colors[i % o.colors.length];
    for (let j = 0; j < cnt; j++) {
      const zc = -hw + (j + 0.5) * len;
      if (hz > 0 && Math.abs(zc) < hz) continue;
      // Recuo por coluna (sulco vertical de erosão, igual em todos os estratos) + um pouco por bloco.
      const col0 = Math.round(zc / o.chunk);
      const groove = hash2(o.seed, col0) * (o.recess ?? 3);
      const d = o.depth * (0.85 + hash2(o.seed + 7, i * 31 + j) * 0.3);
      const dx = -groove - hash2(o.seed + i, j) * (o.recess ?? 3) * 0.3 - i * 0.25;
      const col = new THREE.Color(base).multiplyScalar(0.9 + hash2(o.seed + 3, i * 17 + j) * 0.2);
      const top = i === o.n - 1 || o.half(ym + o.step) < Math.abs(zc);
      const g = paint(jitter(box(d, o.step + 0.1, len + 0.6), 0.025, o.seed + i * 13 + j), col, tf(o.x - d / 2 + dx, ym, zc));
      if (top && o.cap) tintUp(g, o.cap, 0.6);
      k.raw(g);
    }
  }
}

/** Vara de bandeirinhas de São João: cordão entre `a` e `b`, triângulos coloridos pendurados. */
function bunting(k: Kit, a: V3, b: V3, n: number, sag: number, seed: number, bulbs = 0): void {
  const cols = ['#e83a3a', '#ffd23f', '#2a8ae0', '#3ab84a', '#f07a2a', '#e04ab0', '#f4f2ea'];
  const P = (t: number): V3 => [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t - sag * 4 * t * (1 - t), a[2] + (b[2] - a[2]) * t];
  const dx = b[0] - a[0]; const dz = b[2] - a[2]; const L = Math.hypot(dx, dz);
  const ux = dx / L; const uz = dz / L;
  for (let q = 0; q < n; q++) {
    const t = (q + 0.5) / n; const p = P(t);
    const w = 0.35;
    k.add(sheet([p[0] - ux * w, p[1], p[2] - uz * w], [p[0] + ux * w, p[1], p[2] + uz * w], [p[0], p[1] - 0.8, p[2]]), cols[Math.floor(hash2(seed, q) * cols.length)]);
  }
  for (let q = 0; q < 6; q++) k.add(cyl(0.5, 0.5, 1, 3, true), '#3a3030', segMatrix(P(q / 6), P((q + 1) / 6)).multiply(new THREE.Matrix4().makeScale(0.05, 1, 0.05)));
  for (let q = 0; q < bulbs; q++) { const p = P((q + 0.5) / bulbs); k.light(box(0.3, 0.3, 0.3), q % 2 ? '#ffe08a' : '#ffb84a', tf(p[0], p[1] + 0.15, p[2])); }
}

/** Poste de praça: coluna escura e luminária acesa. */
function lamp(k: Kit, x: number, z: number, h: number, color = '#fff1c8', y = 0): void {
  k.add(cyl(0.1, 0.15, h, 5, true), '#2e3238', tf(x, y + h / 2, z));
  k.light(box(0.6, 0.6, 0.6), color, tf(x, y + h + 0.2, z));
}

// ───────────────────────────── Bahia ─────────────────────────────

/** Morro do Pai Inácio (Chapada Diamantina): mesa de paredões cinza-dourados, talude de cerrado e o cruzeiro. */
function morroPaiInacio(): Model {
  const k = new Kit();
  mesa(k, { rx: 170, rz: 300, H: 270, talus: 150, seed: 311, n: 20, rock: ['#a8987a', '#8f8270', '#b8a684', '#9a8a72', '#c0aa86'], top: '#6f8448', slope: '#7f8a4a', step: 20 });
  // Morro do Camelo ao lado: mesa mais baixa e mais larga, ao fundo.
  mesa(k, { rx: 140, rz: 220, H: 170, talus: 110, seed: 312, n: 14, rock: ['#9a8a72', '#86786a', '#ad9c7c'], top: '#6a7e44', slope: '#7a8648', step: 22, x: -160, z: 380 });
  // Cruzeiro no alto (×2,5 para ler de longe) e a trilha clara subindo o talude.
  k.beam([40, 268, -40], [40, 300, -40], 3, '#e8e2d0').beam([40, 292, -50], [40, 292, -30], 2.4, '#e8e2d0');
  for (let i = 0; i < 6; i++) k.add(box(10, 2, 26), '#c8b890', tf(120 - i * 13, 20 + i * 22, -120 + i * 14, 1, 1, 1, 0.5, 0, 0));
  return k.model(300, [0.78, 1.06]);
}

/** Quadrado de Trancoso / Porto Seguro: a igrejinha branca no fim do gramado, casinhas coloridas e coqueiros. */
function igrejaQuadrado(): Model {
  const k = new Kit();
  k.add(box(60, 0.4, 56), '#6fae4a', tf(-12, 0.2, 0));
  church(k, -22, 0.2, 0, { wall: '#f6f4ec', trim: '#3f7cc0', towers: 1, cap: 'pyramid', s: 0.85, roof: '#b8603a' });
  k.add(box(4, 1.2, 50), '#c9b48a', tf(16, 0.6, 0)); // mureta da beira do quadrado
  const cols = ['#f2a8b8', '#ffd23f', '#5aa8e0', '#7ac87a', '#f08a4a', '#46c1b8', '#e85a5a', '#b89ae6'];
  for (let i = 0; i < 8; i++) {
    const side = i < 4 ? -1 : 1;
    const x = 8 - (i % 4) * 9;
    cottage(k, x, side * 24, 8, 6, 3.6, cols[i], side * PI / 2);
  }
  for (let i = 0; i < 5; i++) palm(k, 10 - i * 9 + hash2(321, i) * 3, (i % 2 ? 1 : -1) * (14 + hash2(322, i) * 4), 13 + hash2(323, i) * 6, 3230 + i);
  crown(k, -40, 7, -16, 7, 5, 7, '#3f7f34', 324);
  k.rod([-40, 0, -16], [-40, 5, -16], 0.5, '#6a5038');
  return k.model(26);
}

// ───────────────────────────── Sergipe ─────────────────────────────

const XINGO_ROCK = ['#b0603a', '#9a5034', '#c2784a', '#8a4a30', '#d08a58'];

/** Cânion do Xingó: paredões de arenito vermelho em degraus sobre a água verde do São Francisco. */
function canionXingo(): Model {
  const k = new Kit();
  k.add(box(70, 0.3, 380), '#2f8f7c', tf(-6, 0.15, 0));
  // Paredão do fundo (face para a pista, do outro lado do rio) e a ponta que avança na curva do cânion.
  cliffWall(k, { x: -40, depth: 40, step: 5, n: 11, half: (y) => 190 - y * 0.6, chunk: 22, colors: XINGO_ROCK, seed: 331, recess: 5, cap: '#8a8a4a' });
  cliffWall(k, { x: 34, depth: 30, step: 5, n: 8, half: (y) => 36 - y * 0.4, chunk: 12, colors: XINGO_ROCK, seed: 332, recess: 3, cap: '#8a8a4a' });
  // Margem da frente: lajes baixas de pedra, sem esconder a água.
  for (let i = 0; i < 7; i++) {
    const z = -170 + i * 50 + hash2(333, i) * 10;
    if (Math.abs(z - 0) < 40) continue;
    k.raw(paint(jitter(box(14, 2.4, 30), 0.12, 334 + i), XINGO_ROCK[i % 5], tf(30, 0.7, z)));
  }
  // Ilhotas de pedra na água e caatinga no alto dos paredões.
  for (let i = 0; i < 4; i++) k.raw(paint(jitter(dodeca(1), 0.15, 335 + i), XINGO_ROCK[(i + 2) % 5], tf(-10 + hash2(336, i) * 20, 1, -120 + i * 70, 6, 5 + i, 7)));
  for (let i = 0; i < 12; i++) crown(k, -60 + hash2(337, i) * 25, 55 + hash2(338, i) * 2, -150 + i * 26, 4, 2.5, 4, i % 2 ? '#7a8a4a' : '#6a7a40', 3370 + i);
  // Catamarã pequenino no rio (escala do cânion).
  k.add(box(5, 1.6, 14), '#f4f2ea', tf(-5, 1, 40)).add(box(4.6, 0.3, 12), '#d8402a', tf(-5, 3.6, 40));
  return k.model(56, [0.78, 1.08]);
}

/** Catamarã do Xingó: dois cascos, dois conveses e o toldo, na água verde junto do paredão vermelho. */
function catamara(): Model {
  const k = new Kit();
  water(k, 0, 0, 26, 40, '#2f8f7c', 341, 14);
  cliffWall(k, { x: -24, depth: 16, step: 4.6, n: 4, half: (y) => 44 - y * 0.9, chunk: 13, colors: XINGO_ROCK, seed: 342, recess: 2.5, cap: '#8a8a4a' });
  const bx = 4;
  // Cascos (com a proa afilada) e o convés.
  for (const sx of [-1, 1]) {
    k.add(box(2.2, 1.6, 18), '#f4f4ef', tf(bx + sx * 3.2, 0.6, 0));
    k.add(cone(1.25, 3, 4), '#f4f4ef', tf(bx + sx * 3.2, 0.6, 10.5, 1, 1, 0.75, PI / 2, 0, 0));
    k.add(box(2.25, 0.35, 18.1), '#1e5aa8', tf(bx + sx * 3.2, 0.1, 0));
  }
  k.add(box(8.6, 0.5, 18), '#e8e8e2', tf(bx, 1.6, -0.5));
  // Cabine de baixo: janelas escuras, paredes brancas.
  k.add(box(7.4, 2.3, 11), '#f4f4ef', tf(bx, 3.0, -2));
  for (const sx of [-1, 1]) k.add(box(0.15, 1.0, 9.6), '#2a4a6a', tf(bx + sx * 3.72, 3.3, -2));
  k.add(box(0.15, 1.0, 5.6), '#2a4a6a', tf(bx + 0.02, 3.3, 3.55));
  // Convés de cima com guarda-corpo e toldo vermelho.
  k.add(box(8, 0.3, 13), '#e8e8e2', tf(bx, 4.3, -2.5));
  for (const sx of [-1, 1]) k.add(box(0.1, 0.9, 13), '#f4f4ef', tf(bx + sx * 3.95, 4.9, -2.5));
  for (const sx of [-1, 1]) for (const z of [-8, -2.5, 3]) k.add(box(0.15, 2.4, 0.15), '#f4f4ef', tf(bx + sx * 3.7, 5.6, z));
  k.add(gable(8.4, 0.8, 12.6, 0.1), '#d8402a', tf(bx, 6.8, -2.5));
  k.add(box(0.08, 1.6, 0.08), '#d8d8d8', tf(bx, 8.4, 3.5)).add(box(0.05, 0.6, 1.0), '#2a8a3a', tf(bx, 8.9, 3.0));
  // Esteira de espuma na popa.
  k.add(box(9, 0.2, 7), '#e8f4f0', tf(bx, 0.2, -12.5));
  return k.model(16);
}

/** Praça São Francisco (São Cristóvão): igreja e convento franciscano, o cruzeiro e o calçamento da praça. */
function pracaSaoFrancisco(): Model {
  const k = new Kit();
  k.add(box(70, 0.3, 76), '#c4b498', tf(-4, 0.15, 0));
  for (let i = 0; i < 8; i++) k.add(box(70, 0.05, 0.3), '#a89878', tf(-4, 0.32, -35 + i * 10));
  // Igreja (uma torre) à esquerda, convento comprido de dois pavimentos à direita, alinhados ao fundo da praça.
  church(k, -22, 0.3, -20, { wall: '#f4efe2', trim: '#c9a46a', towers: 1, cap: 'pyramid', s: 1.0, roof: '#a8502e' });
  const cz = 14; const cl = 40;
  k.fac('classic', 14, 10, cl, '#f2ece0', tf(-30, 5.3, cz));
  k.add(box(14.6, 0.6, cl + 0.6), '#c9a46a', tf(-30, 10.3, cz));
  k.add(hip(15, 3.6, cl + 1, 0, cl - 13), '#a8502e', tf(-30, 10.6, cz));
  k.add(box(0.3, 3, 2), '#4a2e22', tf(-22.85, 1.8, cz));
  // Galeria de arcos do térreo (pilares) na frente do convento.
  for (let i = 0; i < 9; i++) k.add(box(1, 4, 1), '#e8e0cc', tf(-21.5, 2.3, cz - cl / 2 + 2 + i * 4.5));
  k.add(box(3, 0.8, cl), '#e8e0cc', tf(-21.5, 4.7, cz));
  // Cruzeiro no centro da praça.
  k.add(box(3, 1.2, 3), '#b8ab90', tf(4, 0.9, 0)).add(box(2, 1, 2), '#b8ab90', tf(4, 2, 0));
  k.add(box(0.6, 8, 0.6), '#d8d0bc', tf(4, 6.4, 0)).add(box(0.6, 0.6, 3.6), '#d8d0bc', tf(4, 8.8, 0));
  // Sobrados nas laterais da praça e árvores.
  const cols = ['#f2d27a', '#7ab8d8', '#e89a7a', '#f4efe2', '#9ad0a0'];
  for (let i = 0; i < 4; i++) for (const sz of [-1, 1]) {
    k.fac('house', 9, 7, 10, cols[(i * 2 + (sz > 0 ? 1 : 0)) % 5], tf(14 - i * 11, 3.6, sz * 34, 1, 1, 1, 0, 0, 0));
    k.add(gable(9.6, 2.4, 10.6), '#a8502e', tf(14 - i * 11, 7.1, sz * 34));
  }
  for (const [x, z] of [[16, -18], [16, 18], [-4, -24], [-4, 24]] as Array<[number, number]>) { k.rod([x, 0, z], [x, 3, z], 0.3, '#6a5038'); crown(k, x, 5, z, 3.2, 2.6, 3.2, '#3f7f34', 350 + x + z); }
  for (const z of [-12, 12]) lamp(k, 10, z, 4.2, '#ffe0a0');
  return k.model(32);
}

// ───────────────────────────── Alagoas ─────────────────────────────

/** Farol de Piaçabuçu: torre branca de faixas vermelhas sobre as dunas da foz, casa do faroleiro e coqueiros. */
function farolPiacabucu(): Model {
  const k = new Kit();
  dune(k, -4, 0, 46, 9, 56, '#ead6a6', '#f6e6be', 361);
  k.add(box(9, 3, 9), '#f4f2ea', tf(0, 6.5, 0));
  const top = lightTower(k, 0, 0, 8, 28, 2.6, 1.8, ['#f4f4ee', '#d63a2a'], 7, '#d63a2a');
  k.add(box(8, 3.4, 10), '#f4f2ea', tf(-9, 7.8, 9)).add(gable(8.6, 1.8, 10.6), '#c8503a', tf(-9, 9.5, 9));
  for (let i = 0; i < 5; i++) palm(k, 26 + hash2(362, i) * 8, -36 + i * 18, 12 + hash2(363, i) * 5, 3630 + i);
  for (let i = 0; i < 6; i++) k.raw(paint(jitter(ico(1, 0), 0.2, 364 + i), '#8a9a4a', tf(18 + hash2(365, i) * 14, 0.4, -40 + i * 16, 3, 1.2, 3)));
  return k.model(top);
}

/** Dunas de Piaçabuçu: dunas altas e claras na foz do São Francisco, lagoa entre elas e coqueiral no pé. */
function dunasPiacabucu(): Model {
  const k = new Kit();
  dune(k, -10, 0, 80, 32, 100, '#ecd8aa', '#f8ead0', 371);
  dune(k, -50, -120, 60, 24, 70, '#e6d0a0', '#f4e4c4', 372, 0.4);
  dune(k, -40, 120, 56, 22, 64, '#efdcb0', '#faeed6', 373, -0.3);
  water(k, 50, -70, 18, 28, '#4aa0b0', 374);
  for (let i = 0; i < 7; i++) palm(k, 70 + hash2(375, i) * 16, -130 + i * 42, 12 + hash2(376, i) * 6, 3760 + i);
  for (let i = 0; i < 10; i++) k.raw(paint(jitter(ico(1, 0), 0.25, 377 + i), i % 2 ? '#9aa850' : '#7a9044', tf(58 + hash2(378, i) * 20, 0.4, -140 + i * 30, 3.2, 1.2, 3.2)));
  // Bugue subindo a duna (escala).
  k.add(box(2.4, 1, 3.6), '#ffcf3a', tf(30, 6.4, 20, 1, 1, 1, 0, 0, 0.35));
  return k.model(34, [0.82, 1.06]);
}

// ───────────────────────────── Pernambuco ─────────────────────────────

/** Marco Zero do Recife: a rosa dos ventos no chão da praça, postes acesos, sobrados do Recife Antigo e a torre de cristal. */
function marcoZeroRecife(): Model {
  const k = new Kit();
  k.add(box(70, 0.3, 80), '#cfc3a8', tf(-6, 0.15, 0));
  k.add(cyl(18, 18, 0.25, 24), '#e8dcc0', tf(4, 0.42, 0));
  // Rosa dos ventos: estrela de 8 pontas em duas cores por ponta, e o anel.
  const cols = ['#2a6a9a', '#e8c040', '#c84a3a', '#2a8a5a'];
  for (let i = 0; i < 16; i++) {
    const a = (i / 16) * PI * 2; const big = i % 2 === 0; const R = big ? 16 : 9; const w = big ? 2.4 : 1.8;
    const tip: V3 = [4 + Math.cos(a) * R, 0.6, Math.sin(a) * R];
    const l: V3 = [4 + Math.cos(a + PI / 2) * w, 0.6, Math.sin(a + PI / 2) * w];
    const r: V3 = [4 + Math.cos(a - PI / 2) * w, 0.6, Math.sin(a - PI / 2) * w];
    k.add(tris([0 + 4, 0.6, 0, ...tip, ...l]), cols[(i >> 1) % 4]);
    k.add(tris([4, 0.6, 0, ...r, ...tip]), big ? '#1e1e24' : '#f4f2ea');
  }
  k.add(cyl(17.2, 17.2, 0.28, 24, true), '#2a2a30', tf(4, 0.45, 0));
  k.add(box(1.2, 1.4, 1.2), '#b8ab90', tf(4, 0.9, 0));
  // Postes em volta da rosa (acesos à noite).
  for (let i = 0; i < 8; i++) { const a = (i / 8) * PI * 2 + PI / 8; lamp(k, 4 + Math.cos(a) * 21, Math.sin(a) * 21, 5, '#fff1c8'); }
  // Sobrados coloridos do Recife Antigo ao fundo.
  const sc = ['#f2a8b8', '#ffd23f', '#5aa8e0', '#7ac87a', '#f08a4a', '#f6f2e6', '#e85a5a', '#46c1b8'];
  for (let i = 0; i < 8; i++) {
    const z = -35 + i * 10; const h = 10 + (i % 3) * 3.5;
    k.fac('classic', 12, h, 9.8, sc[i], tf(-30, h / 2, z));
    k.add(box(12.6, 0.8, 10.2), '#f4f2ea', tf(-30, h + 0.4, z));
  }
  // Torre de cristal (o farol das esculturas, do outro lado do porto): coluna branca alta e o topo aceso.
  k.add(cyl(1.6, 2.2, 32, 8), '#f2efe6', tf(-52, 16, 26));
  k.light(cyl(2.4, 1.6, 5, 8), '#bfe6ff', tf(-52, 34.5, 26));
  k.blink(sphere(0.5, 6, 4), '#ff3a2a', tf(-52, 37.6, 26));
  k.add(box(30, 2, 12), '#8a8478', tf(-52, 0, 26)); // molhe
  return k.model(38);
}

/** Ponte Maurício de Nassau: arcos de concreto sobre o Capibaribe, balaustrada, postes acesos e as estátuas nas cabeceiras. */
function ponteMauricio(): Model {
  const k = new Kit();
  const L = 120; const deckY = 6.5; const W = 14;
  // Rio cruzando por baixo (corre em X, da pista para o fundo) e o cais de pedra das margens.
  k.add(box(120, 0.3, 84), '#3a5a6a', tf(-30, 0.15, 0));
  for (const sz of [-1, 1]) k.add(box(120, 2, 3), '#8a8478', tf(-30, 0.8, sz * 43.5));
  // Tabuleiro em arcos: perfil (z, y) com 5 vãos, extrudado na largura (X).
  const prof: Array<[number, number]> = [[-L / 2, -0.5], [L / 2, -0.5], [L / 2, deckY], [-L / 2, deckY]];
  const holes: Array<Array<[number, number]>> = [];
  const nA = 5; const span = 84 / nA;
  for (let a = 0; a < nA; a++) {
    const zc = -42 + (a + 0.5) * span; const r = span / 2 - 1.6;
    const h: Array<[number, number]> = [[zc + r, 0.2]];
    for (let q = 1; q < 6; q++) { const t = (q / 6) * PI; h.push([zc + Math.cos(t) * r, 0.2 + Math.sin(t) * r * 0.85 + 1.2]); }
    h.push([zc - r, 0.2]);
    holes.push(h);
  }
  const deck = extrude(prof, W, holes).translate(0, 0, -W / 2).rotateY(PI / 2);
  k.add(deck, '#d8d2c2');
  k.add(box(W + 0.6, 0.6, L), '#c4bca8', tf(0, deckY + 0.3, 0));
  k.add(box(W - 2, 0.05, L), '#4a4e55', tf(0, deckY + 0.62, 0));
  for (const sx of [-1, 1]) {
    k.add(box(0.5, 1.1, L), '#ece6d6', tf(sx * (W / 2 - 0.2), deckY + 1.15, 0));
    for (let z = -L / 2 + 6; z < L / 2; z += 14) lamp(k, sx * (W / 2 - 0.4), z, 5, '#ffe2a6', deckY + 0.6);
  }
  // Estátuas de bronze sobre pedestais nas quatro cabeceiras.
  for (const sx of [-1, 1]) for (const sz of [-1, 1]) {
    const x = sx * (W / 2 + 1.8); const z = sz * (L / 2 - 3);
    k.add(box(2.6, 4, 2.6), '#d8d2c2', tf(x, deckY - 1 + 2, z));
    k.add(cyl(0.6, 0.8, 3, 6), '#5a4a2a', tf(x, deckY + 2.5, z));
    k.add(sphere(0.45, 6, 4), '#5a4a2a', tf(x, deckY + 4.4, z));
    k.add(box(0.3, 1.6, 0.3), '#5a4a2a', tf(x, deckY + 4.2, z + 0.7, 1, 1, 1, 0.6, 0, 0));
    k.light(box(0.3, 0.3, 0.3), '#ffe2a6', tf(x + sx * 1.4, deckY + 1.5, z));
  }
  // Rampas de chão nas cabeceiras (aterro).
  for (const sz of [-1, 1]) k.add(box(W, deckY + 1, 14), '#b8b0a0', tf(0, (deckY - 1) / 2, sz * (L / 2 + 7)));
  return k.model(14);
}

/** Morro do Pico (Noronha): agulha de rocha escura sobre um morro de mata, à beira do mar. */
function morroDoPico(): Model {
  const k = new Kit();
  k.raw(speckle(hill(260, 120, 210, 391, '#5f7a48', '#4f8f42', 2, 0.14, 0.6), 0.06, 391));
  const spire = hill(62, 330, 54, 392, '#857c70', null, 2, 0.1, 0.4, tf(-20, 0, 10, 1, 1, 1, 0, 0, -0.06));
  k.raw(speckle(tintUp(spire, '#4a8040', 0.62, 0.9), 0.07, 392));
  k.raw(speckle(hill(110, 170, 90, 393, '#4f7040', '#4a8a3c', 1, 0.14, 0.5, tf(-70, 0, -90)), 0.06, 393));
  return k.model(330, [0.78, 1.04]);
}

/** Morro Dois Irmãos (Noronha): as duas ilhas cônicas lado a lado no mar, topo de mata, espuma na base. */
function doisIrmaos(): Model {
  const k = new Kit();
  const isle = (z: number, h: number, r: number, seed: number): void => {
    const g = hill(r, h, r * 0.9, seed, '#6a5a4c', null, 2, 0.08, 0.5, tf(0, 0, z));
    // Rocha nua embaixo, mata densa da metade para cima.
    recolor(g, (_x, y) => (y > h * 0.42 + hash2(seed, Math.round(y)) * 4 ? (hash2(seed, Math.round(y * 3)) > 0.5 ? '#3f7f34' : '#4a8f3a') : null));
    k.raw(speckle(g, 0.07, seed));
    k.add(cyl(r * 1.05, r * 1.12, 0.5, 14, true), '#f2f6f6', tf(0, 0.25, z));
  };
  isle(-30, 46, 30, 401);
  isle(30, 40, 27, 402);
  for (let i = 0; i < 5; i++) k.raw(paint(jitter(dodeca(1), 0.2, 403 + i), '#5a4c40', tf(14 + hash2(404, i) * 10, 0.5, -60 + i * 30, 3 + i % 3, 2.5, 3)));
  return k.model(46, [0.8, 1.05]);
}

// ───────────────────────────── Paraíba ─────────────────────────────

/** Parque do Povo (São João de Campina Grande): a pirâmide, bandeirinhas, cordões de luz, palco, fogueira e o portal. */
function parqueDoPovo(): Model {
  const k = new Kit();
  k.add(box(80, 0.3, 90), '#b89a72', tf(-10, 0.15, 0));
  // A Pirâmide: pilares e telhado piramidal, borda de luzes.
  const px = -22; const S = 30;
  for (let i = 0; i < 4; i++) for (let j = 0; j < 4; j++) if (i === 0 || i === 3 || j === 0 || j === 3) k.add(box(0.9, 6, 0.9), '#8a5a3a', tf(px - S / 2 + i * S / 3, 3, -S / 2 + j * S / 3));
  k.add(box(S + 2, 1, S + 2), '#c8783a', tf(px, 6.4, 0));
  k.add(hip(S + 3, 16, S + 3), '#d8883a', tf(px, 6.9, 0));
  k.add(cone(1, 3, 4), '#f4c040', tf(px, 24.2, 0));
  for (let i = 0; i < 24; i++) { const t = i / 24; const side = Math.floor(t * 4); const u = (t * 4 - side) * (S + 2) - (S + 2) / 2; const [dx, dz] = [[u, -(S + 2) / 2], [(S + 2) / 2, u], [-u, (S + 2) / 2], [-(S + 2) / 2, -u]][side]; k.light(box(0.5, 0.5, 0.5), i % 2 ? '#ffd860' : '#ff7a3a', tf(px + dx, 6.1, dz)); }
  // Palco com fundo aceso.
  k.add(box(10, 1.4, 22), '#3a3a40', tf(-48, 0.7, 0));
  k.add(box(1, 9, 22), '#2a2a30', tf(-53, 4.5, 0));
  k.light(box(0.3, 6, 18), '#ff9a4a', tf(-52.3, 5, 0));
  k.add(box(12, 0.6, 24), '#2a2a30', tf(-48, 9.3, 0));
  // Postes de bandeirinhas e cordões de luz cruzando o arraial.
  const poles: V3[] = [];
  for (let i = 0; i < 5; i++) for (const sz of [-1, 1]) { const p: V3 = [22 - i * 16, 8, sz * 34]; poles.push(p); k.add(box(0.35, 8, 0.35), '#6a4a2a', tf(p[0], 4, p[2])); }
  for (let i = 0; i < 5; i++) bunting(k, poles[i * 2], poles[i * 2 + 1], 32, 1.4, 410 + i, 10);
  for (let i = 0; i < 4; i++) for (const s of [0, 1]) bunting(k, poles[i * 2 + s], poles[i * 2 + 2 + s], 14, 0.8, 420 + i * 2 + s, 0);
  // Fogueira de São João (acesa).
  for (let q = 0; q < 6; q++) { const a = (q / 6) * PI; k.beam([8 + Math.cos(a) * 1.6, 0, Math.sin(a) * 1.6], [8 - Math.cos(a) * 1.6, 0.6, -Math.sin(a) * 1.6], 0.4, '#5a3a22'); }
  k.light(cone(1.6, 3.4, 5), '#ff8a2a', tf(8, 2, 0)).light(cone(0.9, 2.4, 5), '#ffd040', tf(8, 2.6, 0.3));
  // Balões de enfeite e o portal de entrada.
  for (let i = 0; i < 4; i++) k.light(new THREE.OctahedronGeometry(1, 0), ['#ff5a5a', '#ffd23f', '#5ab8ff', '#7ae07a'][i], tf(-2 - i * 9, 11, (i % 2 ? 1 : -1) * 18, 1, 1.4, 1));
  for (const sz of [-1, 1]) k.add(box(1.2, 9, 1.2), '#8a5a3a', tf(30, 4.5, sz * 8));
  k.add(gable(4, 2.4, 19), '#c8502a', tf(30, 9, 0));
  k.add(box(0.4, 1.6, 14), '#f4d040', tf(31.2, 7.6, 0));
  return k.model(25);
}

/** Pedra da Boca: domo de granito com a "boca" escura aberta na face, outro lajedo ao lado e a caatinga no pé. */
function pedraDaBoca(): Model {
  const k = new Kit();
  const H = 190; const RX = 150; const RZ = 130;
  const dome = hill(RX, H, RZ, 431, '#8e877a', null, 3, 0.05, 0.4);
  // A boca: cavidade oval escura no alto da face da frente (+X), com a borda mais clara.
  recolor(dome, (x, y, z) => {
    if (x < RX * 0.25) return null;
    const e = ((y - H * 0.6) / (H * 0.17)) ** 2 + ((z + 10) / 42) ** 2;
    if (e < 1) return e < 0.55 ? '#231e1a' : '#3a322a';
    if (e < 1.45) return '#b0a898';
    return null;
  });
  k.raw(speckle(tintUp(dome, '#7a8a50', 0.86, 0.5), 0.04, 431));
  k.raw(speckle(hill(110, 110, 120, 432, '#857e72', '#7a8a50', 2, 0.08, 0.4, tf(-80, 0, 210)), 0.05, 432));
  k.raw(speckle(hill(220, 22, 300, 433, '#8a8a52', '#6f8a44', 1, 0.2, 0.5, tf(20, 0, 60)), 0.08, 433));
  return k.model(H, [0.78, 1.05]);
}

// ───────────────────────────── Rio Grande do Norte ─────────────────────────────

/** Morro do Careca (Ponta Negra): morro de mata com a grande encosta de areia careca descendo até a praia. */
function morroDoCareca(): Model {
  const k = new Kit();
  const g = hill(110, 100, 150, 441, '#4a8a3c', null, 3, 0.07, 0.5);
  recolor(g, (x, y, z) => {
    const w = 62 - y * 0.32 + (hash2(441, Math.round(y / 6)) - 0.5) * 8;
    if (x > 8 && Math.abs(z - 10) < w && y > 1) return y > 88 ? '#d8c08a' : '#ead6a4';
    return hash2(442, Math.round(x + z * 3 + y * 7)) > 0.5 ? '#3f7f34' : '#4f8f42';
  });
  k.raw(speckle(g, 0.05, 441));
  // Praia de areia na frente e o mar raso.
  k.add(box(40, 1.2, 330), '#ecd8aa', tf(112, 0.3, 0));
  k.add(box(14, 0.4, 330), '#f2f6f6', tf(134, 0.1, 0));
  for (let i = 0; i < 6; i++) palm(k, 104 + hash2(443, i) * 8, -150 + i * 22, 11 + hash2(444, i) * 5, 4440 + i);
  for (let i = 0; i < 6; i++) { const z = 30 + i * 18; k.add(cyl(0.05, 0.05, 2.4, 3, true), '#f4f2ea', tf(118, 1.6, z)); k.add(cone(1.6, 0.8, 6), ['#e83a3a', '#ffd23f', '#2a8ae0'][i % 3], tf(118, 3.0, z)); }
  return k.model(100, [0.8, 1.05]);
}

/** Cajueiro de Pirangi: a copa baixa e imensa que se espalha em galhos que tocam o chão, cajus e o mirante. */
function cajueiroGigante(): Model {
  const k = new Kit();
  const bark = '#6a5a44';
  // Galhos que saem do tronco e descem até o chão, onde enraízam e sobem de novo.
  const tips: V3[] = [];
  for (let q = 0; q < 12; q++) {
    const a = (q / 12) * PI * 2 + hash2(451, q) * 0.4;
    const r1 = 12 + hash2(452, q) * 6; const r2 = 26 + hash2(453, q) * 12;
    const p1: V3 = [Math.cos(a) * r1, 4.5, Math.sin(a) * r1 * 1.3];
    const p2: V3 = [Math.cos(a) * r2, 0.6, Math.sin(a) * r2 * 1.3];
    const p3: V3 = [Math.cos(a) * (r2 + 6), 5 + hash2(454, q) * 2, Math.sin(a) * (r2 + 6) * 1.3];
    k.rod([0, 2.5, 0], p1, 0.7, bark, 5).rod(p1, p2, 0.55, bark, 5).rod(p2, p3, 0.45, bark, 5);
    tips.push(p1, p3);
  }
  k.rod([0, -0.5, 0], [0, 3, 0], 1.3, bark, 6);
  // Copa: massas largas e baixas, verde-escuro com tons.
  const greens = ['#3a7a30', '#4a8a36', '#2f6a2a', '#56983e'];
  // Uma copa só, contínua: massas sobrepostas, mais altas no meio e caindo para a borda.
  for (let q = 0; q < 40; q++) {
    const a = hash2(455, q) * PI * 2; const r = Math.sqrt(hash2(456, q)) * 38;
    crown(k, Math.cos(a) * r, 8.2 - (r / 38) * 3.6 + hash2(457, q), Math.sin(a) * r * 1.3, 8 + hash2(458, q) * 4, 3 + hash2(459, q) * 1.4, 8 + hash2(460, q) * 4, greens[q % 4], 4560 + q);
  }
  crown(k, 0, 9, 0, 16, 4, 18, '#3f7f34', 457, 1);
  void tips;
  // Cajus vermelhos e amarelos na borda da copa.
  for (let q = 0; q < 36; q++) {
    const a = hash2(461, q) * PI * 2; const r = 30 + hash2(462, q) * 14;
    k.add(new THREE.OctahedronGeometry(0.5, 0), q % 2 ? '#e83a2a' : '#f4b830', tf(Math.cos(a) * r, 2.6 + hash2(463, q) * 2, Math.sin(a) * r * 1.3));
  }
  // Mirante de madeira na frente (para ver a copa de cima) e a passarela.
  const mx = 40; const mz = -30;
  for (const dx of [-2, 2]) for (const dz of [-2, 2]) k.add(box(0.4, 9, 0.4), '#7a5a3a', tf(mx + dx, 4.5, mz + dz));
  k.add(box(5, 0.4, 5), '#8a6a42', tf(mx, 8, mz)).add(box(5, 1, 0.15), '#8a6a42', tf(mx, 8.7, mz - 2.5)).add(box(0.15, 1, 5), '#8a6a42', tf(mx + 2.5, 8.7, mz));
  k.add(hip(6, 2.2, 6), '#b8603a', tf(mx, 10.6, mz));
  for (const dx of [-2, 2]) for (const dz of [-2, 2]) k.add(box(0.2, 2.2, 0.2), '#7a5a3a', tf(mx + dx, 9.5, mz + dz));
  k.beam([mx - 2, 8, mz + 2.5], [mx - 10, 0, mz + 10], 1.4, '#8a6a42', 0.3);
  k.add(box(6, 0.4, 6), '#c8b890', tf(mx, 0.2, mz));
  return k.model(13, [0.7, 1.1]);
}

// ───────────────────────────── Ceará ─────────────────────────────

/** Ponte dos Ingleses (Fortaleza): o píer comprido sobre estacas mar adentro, postes acesos e o mirante na ponta. */
function ponteDosIngleses(): Model {
  const k = new Kit();
  const x0 = 14; const x1 = -84; const deckY = 6; const W = 7;
  // Mar e faixa de areia (o calçadão fica do lado da pista).
  k.add(plan([[x0 + 8, -60], [x0 + 8, 60], [x1 - 30, 70], [x1 - 30, -70]], 0.2), '#2f7a9a', tf(0, 0.02, 0));
  k.add(box(14, 0.5, 120), '#e2d0a4', tf(x0 + 8, 0.25, 0));
  for (let i = 0; i < 3; i++) k.add(box(1.6, 0.25, 116 - i * 20), '#e8f2f2', tf(x0 + 0.5 - i * 5, 0.24, (hash2(461, i) - 0.5) * 10));
  // Tabuleiro sobre estacas, guarda-corpo, postes.
  const L = x0 - x1;
  k.add(box(L, 0.8, W), '#c8bca4', tf((x0 + x1) / 2, deckY, 0));
  k.add(box(L, 0.5, W + 0.4), '#8a7a62', tf((x0 + x1) / 2, deckY - 0.6, 0));
  for (let x = x0 - 3; x > x1; x -= 7) for (const sz of [-1, 1]) k.add(box(0.7, deckY, 0.7), '#6a645a', tf(x, deckY / 2 - 0.6, sz * (W / 2 - 0.6)));
  for (const sz of [-1, 1]) k.add(box(L, 1.0, 0.25), '#e8e2d0', tf((x0 + x1) / 2, deckY + 0.9, sz * W / 2));
  for (let x = x0 - 6; x > x1 + 4; x -= 12) for (const sz of [-1, 1]) lamp(k, x, sz * (W / 2 - 0.3), 4.5, '#ffe2a6', deckY + 0.4);
  // A ponta: plataforma mais larga com o quiosque de telhado verde.
  k.add(box(16, 0.8, 18), '#c8bca4', tf(x1 - 4, deckY, 0));
  for (let x = x1 - 10; x < x1 + 4; x += 6) for (const sz of [-1, 0, 1]) k.add(box(0.7, deckY, 0.7), '#6a645a', tf(x, deckY / 2 - 0.6, sz * 7.5));
  for (const dx of [-3, 3]) for (const dz of [-3, 3]) k.add(box(0.3, 3.2, 0.3), '#e8e2d0', tf(x1 - 4 + dx, deckY + 2, dz));
  k.add(hip(8, 2.4, 8), '#3a7a5a', tf(x1 - 4, deckY + 3.6, 0));
  k.light(box(1, 0.6, 1), '#ffe2a6', tf(x1 - 4, deckY + 3.3, 0));
  // Coqueiros do calçadão.
  for (let i = 0; i < 4; i++) palm(k, x0 + 10, -48 + i * 32, 11 + hash2(462, i) * 3, 4620 + i);
  return k.model(18);
}

/** Falésias de Canoa Quebrada: paredão de arenito colorido ao longo da praia, a lua e a estrela entalhadas e jangadas. */
function falesiasCanoa(): Model {
  const k = new Kit();
  const colors = ['#c8603a', '#e8a868', '#f4e4c8', '#d87a48', '#b84a30', '#eac08a'];
  cliffWall(k, { x: -10, depth: 30, step: 4, n: 8, half: (y) => 200 - y * 0.8, chunk: 18, colors, seed: 471, recess: 5, cap: '#9a9a52' });
  // Mato no alto da falésia.
  for (let i = 0; i < 16; i++) k.raw(paint(jitter(ico(1, 0), 0.2, 472 + i), i % 2 ? '#8a9a4a' : '#9aa856', tf(-25 + hash2(473, i) * 14, 32.5, -180 + i * 24, 5, 1.4, 5)));
  // Lua crescente e estrela entalhadas na face (sulco escuro).
  const moon: Array<[number, number]> = [];
  for (let q = 0; q <= 8; q++) { const a = -PI * 0.7 + (q / 8) * PI * 1.4; moon.push([Math.cos(a + PI) * 9, Math.sin(a + PI) * 9]); }
  for (let q = 8; q >= 0; q--) { const a = -PI * 0.55 + (q / 8) * PI * 1.1; moon.push([Math.cos(a + PI) * 7.2 - 3.3, Math.sin(a + PI) * 7.2]); }
  k.add(extrude(moon.map(([u, v]): [number, number] => [-v, u]), 0.6).rotateY(PI / 2), '#6a2e1e', tf(-9.3, 16, 0));
  k.raw(paint(jitter(box(2, 24, 40), 0.02, 479), '#e8b880', tf(-10, 18, -6)));
  const star: Array<[number, number]> = [];
  for (let q = 0; q < 10; q++) { const a = PI / 2 + (q / 10) * PI * 2; const r = q % 2 ? 1.8 : 4.5; star.push([Math.cos(a) * r, Math.sin(a) * r]); }
  k.add(extrude(star.map(([u, v]): [number, number] => [u, v]), 0.6).rotateY(PI / 2), '#6a2e1e', tf(-9.3, 22, -14));
  // Praia na frente, jangadas e barracas.
  k.add(box(40, 0.4, 400), '#efdcb0', tf(16, 0.2, 0));
  for (let i = 0; i < 3; i++) {
    const z = -90 + i * 80; const x = 24;
    k.add(box(2.4, 0.5, 7), '#b08a5a', tf(x, 0.6, z));
    k.add(sheet([x, 1, z + 1], [x, 9, z + 1.5], [x, 1.2, z - 4]), ['#f6f2e8', '#ffd23f', '#e85a5a'][i]);
    k.rod([x, 0.6, z + 1.2], [x, 9.2, z + 1.5], 0.08, '#6a4a2a', 4);
  }
  for (let i = 0; i < 4; i++) { const z = 30 + i * 12; k.add(box(4, 2.6, 4), '#8a6a42', tf(6, 1.3, z)); k.add(hip(6, 2, 6), '#c8a860', tf(6, 2.6, z)); }
  return k.model(34, [0.8, 1.06]);
}

// ───────────────────────────── Piauí ─────────────────────────────

/** Manguezal do Delta do Parnaíba: mangues de raízes-escora na água escura, guarás vermelhos e a voadeira. */
function manguezalDelta(): Model {
  const k = new Kit();
  water(k, 0, 0, 34, 46, '#4a6656', 481, 14);
  k.add(plan(blobPoly(12, 26, 40, 0.15, 482), 0.25), '#466052', tf(-8, 0.05, 0));
  const greens = ['#2f6a34', '#3a7a3a', '#2a5a30'];
  for (let i = 0; i < 20; i++) {
    const x = -26 + hash2(483, i) * 32; const z = -40 + i * 4.2 + (hash2(484, i) - 0.5) * 4;
    const H = 7 + hash2(485, i) * 5;
    const hub: V3 = [x, 2.4, z];
    for (let q = 0; q < 5; q++) {
      const a = (q / 5) * PI * 2 + hash2(486, i * 7 + q);
      const r = 2.2 + hash2(487, i * 7 + q) * 1.5;
      k.add(cyl(0.5, 0.5, 1, 3, true), '#6a5a48', segMatrix(hub, [x + Math.cos(a) * r, -0.3, z + Math.sin(a) * r]).multiply(new THREE.Matrix4().makeScale(0.22, 1, 0.22)));
    }
    k.rod(hub, [x, H * 0.7, z], 0.22, '#6a5a48', 4);
    crown(k, x, H * 0.8, z, 4.6, 2.6, 4.6, greens[i % 3], 4880 + i);
    crown(k, x + 2.4, H * 0.64, z - 1.6, 3.2, 2, 3.2, greens[(i + 1) % 3], 4900 + i);
    // Guarás pousados na copa.
    if (i % 3 === 0) for (let q = 0; q < 3; q++) k.add(new THREE.OctahedronGeometry(0.35, 0), '#e8302a', tf(x + (q - 1) * 1.6, H * 0.8 + 2, z + hash2(489, i + q) * 2 - 1, 1.4, 0.8, 0.7));
  }
  // Bando de guarás em voo.
  for (let q = 0; q < 8; q++) k.add(sheet([0, 0, -0.7], [0, 0, 0.7], [0.5, 0, 0]), '#e8302a', tf(4 + q * 2.2, 14 + (q % 3) * 1.2, -10 + q * 2.4, 1, 1, 1, 0, 0.3, 0.2));
  // Voadeira de passeio com toldo.
  k.add(box(2.2, 0.8, 8), '#f4f2ea', tf(20, 0.5, 8)).add(cone(1.1, 2, 4), '#f4f2ea', tf(20, 0.5, 13, 1, 0.7, 1, PI / 2, 0, PI / 4));
  for (const dz of [-3, 2]) for (const sx of [-1, 1]) k.add(box(0.1, 1.6, 0.1), '#d8d8d8', tf(20 + sx * 0.9, 1.7, 8 + dz));
  k.add(box(2.4, 0.15, 6), '#2a6ab8', tf(20, 2.5, 7.5));
  return k.model(13, [0.72, 1.08]);
}

/** Pedras de Sete Cidades: torres de arenito ruiniforme em placas (casco de tartaruga), o arco e o cerrado. */
function pedrasSeteCidades(): Model {
  const k = new Kit();
  const rock = ['#a07a58', '#8a6a4c', '#b48a64', '#7a6250'];
  const stack = (x: number, z: number, H: number, R: number, seed: number): void => {
    const n = Math.max(3, Math.round(H / 5));
    for (let i = 0; i < n; i++) {
      const t = i / n; const r = R * (1 - t * 0.35) * (0.85 + hash2(seed, i) * 0.3);
      const g = paint(jitter(sphere(1, 7, 3), 0.12, seed + i), rock[(i + seed) % 4], tf(x + (hash2(seed, i + 9) - 0.5) * R * 0.3, (t + 0.5 / n) * H, z, r, H / n * 0.62, r * 0.8));
      k.raw(speckle(tintUp(g, '#5a5a4c', 0.8, 0.4), 0.08, seed + i));
    }
  };
  stack(0, 0, 42, 20, 491);
  stack(-30, -40, 36, 17, 492);
  stack(-18, 38, 30, 15, 493);
  stack(-56, 4, 38, 19, 494);
  stack(14, -52, 18, 11, 495);
  // Arco do Triunfo: dois pilares e a laje.
  stack(4, 52, 18, 5, 496);
  stack(4, 70, 18, 5, 497);
  k.raw(speckle(paint(jitter(box(10, 5, 26), 0.08, 498), '#9a7656', tf(4, 19, 61)), 0.08, 498));
  // Cerrado em volta.
  for (let i = 0; i < 9; i++) tree(k, 24 + hash2(499, i) * 14, -70 + i * 17, 5 + hash2(500, i) * 3, 5000 + i, ['#7a8a44', '#6a7e3e', '#8a9650']);
  return k.model(42, [0.8, 1.08]);
}

// ───────────────────────────── Maranhão ─────────────────────────────

/** Casario de azulejos de São Luís: sobrados de três andares com fachadas de azulejo, sacadas de ferro e mirantes. */
function casarioAzulejos(): Model {
  const k = new Kit();
  const tiles = ['#5a86c8', '#e8e0c8', '#e8c860', '#6aa8c0', '#f0b0a0', '#3a6ab0'];
  k.add(box(30, 0.3, 66), '#9a9080', tf(-6, 0.15, 0));
  for (let i = 0; i < 6; i++) {
    const z = -27.5 + i * 11; const floors = i % 3 === 1 ? 2 : 3; const h = floors * 4.2;
    const col = tiles[i];
    k.fac('classic', 14, h, 10.8, col, tf(-7, h / 2, z));
    // Barrado de azulejo em xadrez (mais claro/mais escuro) na frente do térreo.
    for (let a = 0; a < 6; a++) for (let b = 0; b < 2; b++) if ((a + b) % 2 === 0) k.add(box(0.06, 0.8, 0.8), new THREE.Color(col).multiplyScalar(0.72), tf(0.03, 0.5 + b * 0.8, z - 4.6 + a * 1.8 + (a > 2 ? 0.6 : 0)));
    // Cornijas brancas, sacadas de ferro em cada andar de cima, telhado de barro.
    for (let f = 1; f <= floors; f++) k.add(box(0.5, 0.35, 11), '#f4f2ea', tf(0.15, f * 4.2 - 0.1, z));
    for (let f = 1; f < floors; f++) k.add(box(1.2, 1.0, 9), '#2a2e36', tf(0.6, f * 4.2 + 0.6, z)).add(box(1.3, 0.15, 9.2), '#f4f2ea', tf(0.6, f * 4.2 + 0.1, z));
    k.add(box(0.6, h, 0.5), '#f4f2ea', tf(0.1, h / 2, z - 5.2));
    k.add(hip(14.8, 3, 11.6, 3, 0), '#b8552e', tf(-7, h, z));
    if (i === 2) { k.fac('house', 5, 3.6, 5, '#e8e0c8', tf(-7, h + 2.6, z)); k.add(hip(5.8, 1.6, 5.8), '#b8552e', tf(-7, h + 4.4, z)); }
    k.add(box(0.3, 2.8, 1.6), '#3a2a20', tf(0.15, 1.4, z + 2.5));
  }
  // Calçada de pedra e lampiões acesos.
  for (const z of [-22, 0, 22]) { k.add(box(0.15, 2.6, 0.15), '#2a2e36', tf(1.2, 6, z)); k.add(box(1.4, 0.12, 0.12), '#2a2e36', tf(1.8, 7.2, z)); k.light(box(0.5, 0.7, 0.5), '#ffd890', tf(2.4, 6.8, z)); }
  for (const z of [-30, 30]) { k.add(cyl(0.1, 0.14, 4.6, 5, true), '#2a2e36', tf(6, 2.3, z)); k.light(box(0.6, 0.8, 0.6), '#ffd890', tf(6, 5, z)); }
  return k.model(22);
}

/** Chapada das Mesas: o Morro do Chapéu (mesa sobre um cone de talude) e a serra de mesas ao fundo. */
function morroDasMesas(): Model {
  const k = new Kit();
  const rock = ['#b8704a', '#a0603e', '#c8845a', '#9a5a3a'];
  // Morro do Chapéu: cone largo de talude com o "chapéu" de paredão por cima.
  k.raw(speckle(paint(jitter(cyl(0.45, 1, 1, 14, true), 0.07, 510, true), '#8a8a4a', tf(0, 69, 0, 150, 142, 160)), 0.07, 510));
  const cap = plan(blobPoly(12, 66, 74, 0.1, 511), 70, [], 5);
  jitter(cap, 0.01, 511);
  const cp = paint(cap, rock[0], tf(0, 128, 0));
  strata(cp, rock, 14, 511);
  k.raw(tintUp(cp, '#7a8a44', 0.7, 1));
  mesa(k, { rx: 140, rz: 360, H: 130, talus: 70, seed: 512, n: 18, rock, top: '#6f8444', slope: '#808848', step: 16, x: -240, z: 60 });
  mesa(k, { rx: 90, rz: 160, H: 110, talus: 60, seed: 513, n: 12, rock, top: '#6f8444', slope: '#808848', step: 16, x: -60, z: 420 });
  return k.model(200, [0.8, 1.06]);
}

/** Cachoeira de São Romão: cortina larga de água (acesa no escuro) caindo do paredão, poço, névoa e mata ciliar. */
function cachoeiraSaoRomao(): Model {
  const k = new Kit();
  const rock = ['#8a6a52', '#7a5a44', '#9a7a5a', '#6a5040'];
  const H = 30;
  cliffWall(k, { x: -18, depth: 30, step: 3, n: 10, half: (y) => 110 - y * 0.6, hole: () => 34, chunk: 14, colors: rock, seed: 521, recess: 3, cap: '#4a7a3a' });
  // Recuo atrás da queda e o rio de cima.
  k.add(box(20, H, 70), '#5a4636', tf(-42, H / 2, 0));
  k.add(box(60, 0.4, 66), '#6a9aa0', tf(-60, H + 0.2, 0));
  // Cortina d'água em três faixas e a crista branca.
  for (let i = 0; i < 3; i++) k.light(box(1.6, H + 0.4, 21.4), i === 1 ? '#e4f4ff' : '#cfe8f6', tf(-31 + (i % 2) * 0.6, H / 2, -22 + i * 22));
  k.light(box(3, 1.2, 66), '#ffffff', tf(-31, H - 0.4, 0));
  // Poço, espuma e névoa.
  water(k, 0, 0, 34, 50, '#3f8a8a', 522, 14);
  k.light(box(8, 1, 64), '#e8f6ff', tf(-25, 0.5, 0));
  for (let i = 0; i < 6; i++) k.raw(paint(jitter(ico(1, 0), 0.2, 523 + i), '#f4f8fa', tf(-22 + hash2(524, i) * 6, 2 + hash2(525, i) * 3, -28 + i * 11, 6, 3, 6)));
  // Mata ciliar no alto e nas margens.
  for (let i = 0; i < 16; i++) { const sz = i % 2 ? 1 : -1; tree(k, -30 - hash2(526, i) * 30, sz * (40 + hash2(527, i) * 50), 9 + hash2(528, i) * 5, 5280 + i); }
  for (let i = 0; i < 8; i++) k.add(jitter(dodeca(1), 0.2, 529 + i), '#6a5a4a', tf(10 + hash2(530, i) * 20, 0.6, -40 + i * 11, 3, 1.6, 3));
  return k.model(H + 8, [0.78, 1.06]);
}

// ───────────────────────────── Pará ─────────────────────────────

/** Praia de rio (Alter do Chão): banco de areia branca na água azul, barracas de palha e a serra ao fundo. */
function praiaDeRio(): Model {
  const k = new Kit();
  water(k, -6, 0, 48, 60, '#3a9aa8', 541, 14);
  k.raw(speckle(hill(26, 2.2, 52, 542, '#f2e6c8', null, 1, 0.12, 0.3, tf(4, 0, 0)), 0.03, 542));
  // Serra da Piroca (morrinho de topo chato) ao fundo.
  const serra = hill(34, 34, 46, 543, '#7a7048', null, 1, 0.12, 0.4, tf(-64, 0, -10));
  const sp = serra.attributes.position as THREE.BufferAttribute;
  for (let i = 0; i < sp.count; i++) if (sp.getY(i) > 22) sp.setY(i, 22 + (sp.getY(i) - 22) * 0.1); // topo chato
  k.raw(speckle(tintUp(serra, '#4a8a3a', 0.5), 0.07, 543));
  // Barracas de palha na areia, com mesas e guarda-sóis.
  for (let i = 0; i < 4; i++) {
    const z = -30 + i * 20; const x = 6 + hash2(544, i) * 4;
    for (const dx of [-2, 2]) for (const dz of [-2, 2]) k.add(box(0.25, 3, 0.25), '#7a5a3a', tf(x + dx, 2.8, z + dz));
    k.add(hip(6.5, 2.6, 6.5), '#c8a860', tf(x, 4.3, z));
    k.add(box(1.4, 0.8, 1.4), '#8a6a42', tf(x + 4, 1.6, z + 3)).add(cone(1.5, 0.7, 6), i % 2 ? '#e85a3a' : '#ffd23f', tf(x + 4, 3.8, z + 3)).add(cyl(0.04, 0.04, 2.4, 3, true), '#f4f2ea', tf(x + 4, 2.4, z + 3));
  }
  // Barcos de madeira na água e uma bandeira.
  for (let i = 0; i < 3; i++) { const z = -40 + i * 38; k.add(box(2.2, 1, 9), ['#2a7ab8', '#e8c040', '#d8402a'][i], tf(-22, 0.6, z)).add(box(2, 1.6, 3), '#f4f2ea', tf(-22, 1.6, z - 1)); }
  for (let i = 0; i < 3; i++) palm(k, 14, -46 + i * 46, 10 + hash2(545, i) * 4, 5450 + i);
  return k.model(24, [0.8, 1.06]);
}

/** Búfalos do Marajó: a manada no campo alagado, o vaqueiro montado, o curral, a porteira e a mangueira. */
function bufalo(): Model {
  const k = new Kit();
  water(k, -6, 6, 22, 30, '#6a8a70', 551, 12);
  const one = (x: number, z: number, yaw: number, s: number, rider: boolean, seed: number): void => {
    const c = Math.cos(yaw); const sn = Math.sin(yaw);
    const P = (lx: number, y: number, lz: number): THREE.Matrix4 => tf(x + lx * c + lz * sn, y, z - lx * sn + lz * c, 1, 1, 1, 0, yaw, 0);
    const hide = hash2(seed, 1) > 0.3 ? '#3a3634' : '#5a4a40';
    k.add(box(1.25 * s, 1.15 * s, 2.5 * s), hide, P(0, 1.35 * s, 0));
    k.add(box(1.35 * s, 0.6 * s, 1.0 * s), hide, P(0, 1.85 * s, 0.75 * s));
    for (const lx of [-0.4, 0.4]) for (const lz of [-0.95, 0.95]) k.add(box(0.28 * s, 0.9 * s, 0.28 * s), hide, P(lx * s, 0.45 * s, lz * s));
    k.add(box(0.6 * s, 0.6 * s, 0.9 * s), hide, P(0, 1.4 * s, 1.55 * s, ));
    // Chifres largos e curvados para trás.
    for (const side of [-1, 1]) {
      const a = P(side * 0.25 * s, 1.75 * s, 1.5 * s); const b = P(side * 0.85 * s, 1.95 * s, 1.15 * s); const d = P(side * 0.75 * s, 2.15 * s, 0.75 * s);
      const pa = new THREE.Vector3().setFromMatrixPosition(a); const pb = new THREE.Vector3().setFromMatrixPosition(b); const pd = new THREE.Vector3().setFromMatrixPosition(d);
      k.beam([pa.x, pa.y, pa.z], [pb.x, pb.y, pb.z], 0.14 * s, '#c8bca8').beam([pb.x, pb.y, pb.z], [pd.x, pd.y, pd.z], 0.11 * s, '#c8bca8');
    }
    if (rider) {
      k.add(box(0.5, 0.8, 0.4), '#d8c8a0', P(0, 2.4 * s, 0.1));
      k.add(box(0.36, 0.36, 0.36), '#8a5a3a', P(0, 3.0 * s, 0.15));
      k.add(cyl(0.45, 0.45, 0.08, 8), '#c8a860', P(0, 3.22 * s, 0.15)).add(cyl(0.2, 0.22, 0.25, 6), '#c8a860', P(0, 3.35 * s, 0.15));
    }
  };
  for (let i = 0; i < 16; i++) one(-14 + hash2(553, i) * 28, -30 + i * 3.8 + (hash2(554, i) - 0.5) * 3, hash2(555, i) * PI * 2, 1, false, 552 + i);
  // Barracão de palha do retiro (onde o vaqueiro guarda a sela).
  for (const dx of [-3, 3]) for (const dz of [-4, 0, 4]) k.add(box(0.35, 3.4, 0.35), '#6a4a2a', tf(-24 + dx, 1.7, 4 + dz));
  k.add(gable(8, 3.4, 10.4, 0.6), '#c8a860', tf(-24, 3.4, 4));
  one(18, 12, -PI / 2 + 0.3, 1, true, 560);
  // Curral de madeira e porteira alta.
  const cx = -22; const cz = -26;
  for (let i = 0; i < 6; i++) for (const sz of [-1, 1]) k.add(box(0.25, 1.8, 0.25), '#7a5a3a', tf(cx - 6 + i * 2.4, 0.9, cz + sz * 6));
  for (const sz of [-1, 1]) for (const y of [0.7, 1.4]) k.add(box(13, 0.15, 0.12), '#8a6a42', tf(cx, y, cz + sz * 6));
  for (const sz of [-1, 1]) k.add(box(0.5, 6.6, 0.5), '#6a4a2a', tf(14, 3.3, -40 + sz * 4));
  k.add(box(0.5, 0.6, 9.2), '#6a4a2a', tf(14, 6.5, -40)).add(box(0.2, 0.9, 5), '#e8e0c8', tf(14.3, 5.6, -40));
  for (let i = 0; i < 4; i++) k.add(box(0.12, 0.12, 4), '#8a6a42', tf(14, 0.8 + i * 0.4, -38 + (i % 2 ? 0 : 0)));
  // Mangueira grande na beira do campo.
  k.rod([-20, -0.5, 26], [-20, 5, 26], 0.8, '#5a4636', 6);
  crown(k, -20, 9, 26, 8, 5, 8, '#2f6a2a', 561, 1);
  crown(k, -16, 7, 31, 5, 3.4, 5, '#3a7a32', 562);
  // Garças brancas no alagado.
  for (let i = 0; i < 4; i++) { const x = -10 + i * 3; const z = 10 + (i % 2) * 4; k.add(box(0.05, 0.6, 0.05), '#2a2a2a', tf(x, 0.3, z)).add(new THREE.OctahedronGeometry(0.35, 0), '#f4f4f0', tf(x, 0.8, z, 0.8, 0.8, 1.4)); }
  return k.model(13);
}

/** Palafitas: casinhas de madeira coloridas sobre estacas no rio, passarelas, canoas e açaizeiros. */
function palafita(): Model {
  const k = new Kit();
  water(k, 0, 0, 30, 46, '#7a6a48', 571, 14);
  const cols = ['#3a8ac8', '#e85a7a', '#5ab86a', '#b08a5a', '#f2c040'];
  const houses: Array<[number, number, number]> = [[6, -26, 0], [-8, -10, 0.15], [4, 6, -0.1], [-10, 24, 0.2], [8, 30, 0]];
  houses.forEach(([x, z, yaw], i) => {
    const y = 2.6; const w = 6 + hash2(572, i) * 2; const d = 7 + hash2(573, i) * 2;
    for (const dx of [-1, 0, 1]) for (const dz of [-1, 1]) k.add(box(0.3, y + 1, 0.3), '#6a5a44', tf(x + dx * (w / 2 - 0.4), (y - 1) / 2, z + dz * (d / 2 - 0.4)));
    k.add(box(w + 1.6, 0.3, d + 0.6), '#8a6a42', tf(x + 0.8, y, z, 1, 1, 1, 0, yaw, 0));
    k.add(box(w, 2.8, d), cols[i], tf(x, y + 1.55, z, 1, 1, 1, 0, yaw, 0));
    k.add(gable(w + 1, 1.8, d + 1), i % 2 ? '#9aa0a8' : '#b89a62', tf(x, y + 2.95, z, 1, 1, 1, 0, yaw, 0));
    k.add(box(0.2, 2, 1), '#3a2a20', tf(x + w / 2 + 0.05, y + 1.15, z - 1, 1, 1, 1, 0, yaw, 0));
    k.add(box(0.2, 1, 1.4), '#f4f2ea', tf(x + w / 2 + 0.05, y + 1.7, z + 1.6, 1, 1, 1, 0, yaw, 0));
  });
  // Passarelas de tábua entre as casas e até a margem (+X).
  for (let i = 0; i + 1 < houses.length; i++) {
    const [ax, az] = houses[i]; const [bx, bz] = houses[i + 1];
    k.beam([ax, 2.6, az], [bx, 2.6, bz], 1.2, '#9a7a52', 0.15);
  }
  k.beam([8, 2.6, 30], [30, 0.4, 34], 1.2, '#9a7a52', 0.15);
  k.beam([6, 2.6, -26], [30, 0.4, -30], 1.2, '#9a7a52', 0.15);
  // Canoas e açaizeiros.
  for (let i = 0; i < 3; i++) k.add(box(1, 0.5, 5), '#5a3a22', tf(14 + i * 3, 0.3, -12 + i * 10, 1, 1, 1, 0, 0.3 * i, 0));
  for (let i = 0; i < 7; i++) acai(k, -22 + hash2(574, i) * 8, -36 + i * 12, 12 + hash2(575, i) * 5, 5750 + i);
  for (let i = 0; i < 4; i++) acai(k, 22 + hash2(576, i) * 6, -40 + i * 26, 11 + hash2(577, i) * 4, 5770 + i);
  return k.model(16);
}

// ───────────────────────────── Amazonas ─────────────────────────────

/** Ponte Rio Negro: estaiada de pilone em arco ogival sobre o tabuleiro, cabos acesos, sobre o rio de água escura. */
function ponteRioNegro(): Model {
  const k = new Kit();
  const L = 900; const deckY = 50; const W = 20; const PH = 185;
  k.add(box(180, 0.3, L + 200), '#2e2a24', tf(-60, 0.15, 0));
  k.add(box(W, 3, L), '#d8dce0', tf(0, deckY - 1.5, 0));
  k.add(box(W - 3, 0.06, L), '#4a4e55', tf(0, deckY + 0.03, 0));
  for (const sx of [-1, 1]) k.add(box(0.5, 1.2, L), '#e8ecef', tf(sx * (W / 2 - 0.25), deckY + 0.6, 0));
  // Pilares de concreto do vão de acesso (o central fica livre).
  for (let z = -L / 2 + 25; z < L / 2; z += 50) {
    if (Math.abs(z) < 150) continue;
    for (const sx of [-1, 1]) k.add(box(3.4, deckY, 4), '#c8ccd0', tf(sx * 5, deckY / 2 - 1.5, z));
    k.add(box(W, 2.4, 4.4), '#c8ccd0', tf(0, deckY - 4, z));
  }
  // Rampas: o tabuleiro desce até o chão nas pontas.
  for (const sz of [-1, 1]) {
    const rl = 260;
    k.add(box(W, 3, Math.hypot(rl, deckY)), '#d8dce0', tf(0, deckY / 2 - 1.5, sz * (L / 2 + rl / 2), 1, 1, 1, Math.atan2(deckY, rl) * sz, 0, 0));
    for (let q = 1; q < 5; q++) { const t = q / 5; const h = deckY * (1 - t); k.add(box(6, h, 3.4), '#c8ccd0', tf(0, h / 2 - 2, sz * (L / 2 + rl * t))); }
  }
  // Pilone: duas pernas em ogiva que se encontram no topo, montadas em vigas sobre o tabuleiro.
  const legX = (t: number): number => (W / 2 + 10) * Math.cos(t * PI / 2) ** 0.7;
  const N = 8; let prev: Array<V3> = [[-legX(0), -2, 0], [legX(0), -2, 0]];
  const pts: V3[] = [];
  for (let q = 1; q <= N; q++) {
    const t = q / N; const y = -2 + t * (PH + 2);
    const next: Array<V3> = [[-legX(t), y, 0], [legX(t), y, 0]];
    for (const s of [0, 1]) k.beam(prev[s], next[s], 5 - t * 2, '#eceae4', 6 - t * 2);
    if (t > 0.5) pts.push(next[0], next[1]);
    prev = next;
  }
  k.add(box(W + 22, 4, 7), '#eceae4', tf(0, deckY - 4, 0));
  k.blink(sphere(1, 6, 4), '#ff3a2a', tf(0, PH + 1.5, 0));
  // Estais em leque para os dois lados, acesos à noite.
  for (let c = 0; c < 9; c++) {
    const t = 0.55 + c * 0.05; const y = -2 + t * (PH + 2); const lx = legX(t);
    const reach = 40 + c * 22;
    for (const sx of [-1, 1]) for (const dir of [-1, 1]) k.wire([sx * lx, y, 0], [sx * (W / 2 - 0.6), deckY + 0.6, dir * reach], 0.35, '#cfe4ff');
  }
  // Postes do tabuleiro.
  for (let z = -L / 2 + 15; z < L / 2; z += 40) for (const sx of [-1, 1]) { k.add(box(0.25, 8, 0.25), '#9aa0a8', tf(sx * (W / 2 - 0.5), deckY + 4, z)); k.light(box(1, 0.3, 0.6), '#ffe2a6', tf(sx * (W / 2 - 1.2), deckY + 8, z)); }
  return k.model(PH);
}

/** Bumbódromo de Parintins: a arena metade vermelha (Garantido) e metade azul (Caprichoso), as cabeças de boi e os refletores. */
function bumbodromo(): Model {
  const k = new Kit();
  const RX = 60; const RZ = 75; const H = 20;
  const outer = ellipse(28, RX, RZ); const inner = ellipse(28, RX - 26, RZ - 26);
  const ring = paint(plan(outer, H, [inner]), '#ffffff');
  recolor(ring, (_x, y, z) => (y > H - 0.5 ? '#e8e4dc' : z > 2 ? '#c8202a' : z < -2 ? '#1e4ab8' : '#f4f2ea'));
  k.raw(ring);
  // Arquibancada em degraus por dentro (anel inclinado) e o terreiro.
  const stands = paint(cyl(1, 0.6, 1, 28, true), '#ffffff', tf(0, H / 2, 0, RX - 2, H - 2, RZ - 2));
  recolor(stands, (_x, _y, z) => (z > 0 ? '#e85a5a' : '#5a8ae8'));
  k.raw(stands);
  k.add(plan(inner, 0.4), '#b88a5a', tf(0, 0, 0));
  // Faixas brancas no topo (camarotes) e portões na frente.
  k.add(plan(outer.map(([x, z]): [number, number] => [x * 1.02, z * 1.02]), 2.2, [outer.map(([x, z]): [number, number] => [x * 0.96, z * 0.96])]), '#f4f2ea', tf(0, H, 0));
  for (const sz of [-1, 1]) k.add(box(2, 8, 10), '#2a2a30', tf(RX - 0.4, 4, sz * 18));
  // As duas cabeças de boi na frente: vermelha com o coração, azul com a estrela.
  const boi = (z: number, body: string, mark: 'heart' | 'star'): void => {
    const x = RX + 14;
    k.add(box(5, 10, 6), '#f4f2ea', tf(x, 5, z));
    k.add(box(6, 6, 8), body, tf(x, 13, z));
    k.add(box(3, 3, 5), body, tf(x + 4, 12, z));
    for (const s of [-1, 1]) { k.beam([x, 15, z + s * 3.5], [x, 18, z + s * 7], 1, '#f4f2ea'); k.beam([x, 18, z + s * 7], [x + 1, 21, z + s * 6], 0.8, '#f4f2ea'); }
    const markG = mark === 'star'
      ? extrude(Array.from({ length: 10 }, (_, q): [number, number] => { const a = PI / 2 + (q / 10) * PI * 2; const r = q % 2 ? 0.9 : 2.2; return [Math.cos(a) * r, Math.sin(a) * r]; }), 0.4)
      : extrude([[0, -2], [2, 0.2], [1.4, 1.6], [0, 0.8], [-1.4, 1.6], [-2, 0.2]], 0.4);
    k.light(markG.rotateY(PI / 2), '#ffffff', tf(x + 3.05, 13.5, z));
  };
  boi(-34, '#1e4ab8', 'star');
  boi(34, '#c8202a', 'heart');
  // Torres de refletores e cordões de luz no topo.
  for (const [x, z] of [[RX * 0.7, RZ * 0.7], [RX * 0.7, -RZ * 0.7], [-RX * 0.7, RZ * 0.7], [-RX * 0.7, -RZ * 0.7]] as Array<[number, number]>) {
    k.add(box(1.2, 34, 1.2), '#8a9098', tf(x * 1.1, 17, z * 1.1));
    k.add(box(2, 2.4, 5), '#3a3e44', tf(x * 1.1, 34, z * 1.1));
    k.light(box(2.2, 1.8, 4.4), '#fff4d8', tf(x * 1.1 - Math.sign(x) * 0.3, 34, z * 1.1));
  }
  for (let i = 0; i < 28; i++) { const [x, z] = outer[i]; k.light(box(0.8, 0.8, 0.8), z > 0 ? '#ff6a6a' : '#6aa8ff', tf(x * 1.02, H + 2.8, z * 1.02)); }
  return k.model(36);
}

// ───────────────────────────── Amapá ─────────────────────────────

/** Vila Serra do Navio: casas modernistas da mineradora sobre pilotis, telhado borboleta, brises de madeira e a caixa d'água. */
function vilaSerraDoNavio(): Model {
  const k = new Kit();
  k.add(box(60, 0.3, 80), '#6fae4a', tf(-8, 0.15, 0));
  const house = (x: number, z: number, c: string): void => {
    for (const dx of [-3, 3]) for (const dz of [-4.5, 4.5]) k.add(box(0.35, 1.4, 0.35), '#8a8a88', tf(x + dx, 0.7, z + dz));
    k.add(box(8, 0.4, 11), '#c8c4bc', tf(x, 1.5, z));
    k.add(box(7.4, 2.8, 10.4), '#f2efe6', tf(x, 3.1, z));
    for (let i = 0; i < 9; i++) k.add(box(0.15, 2.4, 0.18), c, tf(x + 3.8, 3.1, z - 4.4 + i * 1.1));
    // Telhado borboleta: duas águas que caem para o meio.
    for (const s of [-1, 1]) k.add(box(4.6, 0.25, 12), '#8a8c8e', tf(x + s * 2.2, 5.0, z, 1, 1, 1, 0, 0, s * 0.26));
    k.add(box(0.2, 2.2, 1.2), c === '#8a5a3a' ? '#2a6ab8' : '#d8402a', tf(x + 3.86, 2.9, z + 3.6));
  };
  for (let i = 0; i < 3; i++) house(10, -26 + i * 26, ['#8a5a3a', '#7a6a4a', '#9a6a42'][i]);
  for (let i = 0; i < 3; i++) house(-14, -14 + i * 26, ['#7a6a4a', '#8a5a3a', '#9a6a42'][i]);
  // Caixa d'água de concreto (marca da vila) e a escada.
  k.add(cyl(1.2, 1.6, 16, 8), '#d8d4cc', tf(-30, 8, -30));
  k.add(cyl(5, 3, 4, 10), '#e4e0d8', tf(-30, 18, -30)).add(cyl(5, 5, 2.4, 10), '#e4e0d8', tf(-30, 21.2, -30)).add(cone(5.4, 1.6, 10), '#9a9c9e', tf(-30, 23.2, -30));
  k.blink(sphere(0.3, 6, 4), '#ff3a2a', tf(-30, 24.3, -30));
  // Ruas de saibro e mata em volta.
  k.add(box(4, 0.35, 80), '#b8946a', tf(-2, 0.18, 0));
  for (let i = 0; i < 10; i++) tree(k, -36 + hash2(581, i) * 10, -36 + i * 8, 12 + hash2(582, i) * 6, 5820 + i);
  for (let i = 0; i < 4; i++) tree(k, 22, -30 + i * 20, 7 + hash2(583, i) * 3, 5830 + i);
  return k.model(26);
}

// ───────────────────────────── Roraima ─────────────────────────────

/** Monumento ao Garimpeiro (Boa Vista): o garimpeiro de cócoras com a bateia, no espelho d'água, com holofotes. */
function monumentoGarimpeiro(): Model {
  const k = new Kit();
  k.add(box(40, 0.3, 46), '#c8c0b0', tf(-4, 0.15, 0));
  k.add(plan(ellipse(16, 15, 15), 0.9), '#bab2a2', tf(0, 0, 0));
  k.add(plan(ellipse(16, 14, 14), 0.2), '#4a8ab0', tf(0, 0.8, 0));
  k.add(box(7, 3.2, 7), '#a8a090', tf(0, 2.2, 0));
  // Figura de cócoras virada para a pista, segurando a bateia (cone raso largo).
  const bronze = '#9a8a68'; const y0 = 3.8; const S = 2.3;
  k.add(box(1.6 * S, 1.2 * S, 2.0 * S), bronze, tf(-0.6 * S, y0 + 0.6 * S, 0)); // pernas dobradas
  k.add(box(1.2 * S, 2.0 * S, 1.6 * S), bronze, tf(-0.5 * S, y0 + 2.0 * S, 0, 1, 1, 1, 0, 0, -0.35)); // tronco inclinado
  k.add(sphere(0.48 * S, 7, 5), bronze, tf(0.2 * S, y0 + 3.3 * S, 0));
  k.add(cyl(0.75 * S, 0.75 * S, 0.12 * S, 8), bronze, tf(0.2 * S, y0 + 3.62 * S, 0)).add(cyl(0.38 * S, 0.42 * S, 0.4 * S, 6), bronze, tf(0.2 * S, y0 + 3.85 * S, 0));
  for (const s of [-1, 1]) k.beam([-0.2 * S, y0 + 2.7 * S, s * 0.8 * S], [1.3 * S, y0 + 1.5 * S, s * 1.1 * S], 0.35 * S, bronze);
  k.add(cyl(1.7 * S, 0.4 * S, 0.5 * S, 10), '#8a7a5a', tf(1.6 * S, y0 + 1.5 * S, 0));
  // Holofotes no pé (acendem à noite) e palmeiras imperiais e postes da praça.
  for (let i = 0; i < 4; i++) { const a = (i / 4) * PI * 2 + PI / 4; k.light(box(0.6, 0.4, 0.6), '#fff1c8', tf(Math.cos(a) * 5.5, 1.0, Math.sin(a) * 5.5)); }
  for (const z of [-18, 18]) for (const x of [10, -16]) palm(k, x, z, 14, 590 + x + z, 0, 6);
  for (const z of [-10, 10]) lamp(k, 14, z, 6);
  return k.model(14);
}

// ───────────────────────────── Rondônia ─────────────────────────────

/** Real Forte Príncipe da Beira: forte quadrado de quatro baluartes em pedra (um apontado para a pista), guaritas, portão, mastro e o rio atrás. */
function fortePrincipeBeira(): Model {
  const k = new Kit();
  const R = 92; const H = 10;
  // Estrela de 4 baluartes em ponta de lança (polar: raio relativo e desvio de ângulo), muralha com o pátio dentro.
  const pts: Array<[number, number]> = []; const tips: Array<[number, number]> = [];
  for (let i = 0; i < 4; i++) {
    const a = (i / 4) * PI * 2;
    const P = (r: number, da: number): [number, number] => [Math.cos(a + da) * R * r, Math.sin(a + da) * R * r];
    pts.push(P(0.58, -0.3), P(0.74, -0.36), P(1, 0), P(0.74, 0.36), P(0.58, 0.3));
    tips.push(P(0.96, 0));
  }
  const sc = (f: number): Array<[number, number]> => pts.map(([x, z]): [number, number] => [x * f, z * f]);
  const stone = '#8a8478';
  k.add(plan(pts, H + 2, [sc(0.84)]), stone, tf(0, -2, 0));
  k.add(plan(sc(1.05), 2.4, [sc(0.92)]), '#767064', tf(0, -0.6, 0)); // escarpa
  k.add(plan(sc(1.008), 0.9, [sc(0.95)]), '#a49e90', tf(0, H, 0)); // parapeito
  k.add(plan(sc(0.84), 0.4), '#7a9a4a', tf(0, 0, 0));
  for (const [x, z] of tips) { k.add(cyl(1.3, 1.3, 2.8, 6), '#d0c8b8', tf(x, H + 1.4, z)); k.add(cone(1.7, 1.9, 6), '#8a7a6a', tf(x, H + 3.7, z)); }
  // Portão na cortina da frente-direita (entre os baluartes de +X e +Z), com o arco escuro.
  const gx = R * 0.58 * Math.cos(PI / 4) + 1; const ang = -PI / 4;
  k.add(box(4, 8, 10), '#d0c8b8', tf(gx, 4, gx, 1, 1, 1, 0, ang, 0));
  k.add(box(4.4, 4.8, 4.2), '#2a2622', tf(gx + 0.3, 2.4, gx + 0.3, 1, 1, 1, 0, ang, 0));
  k.add(gable(4, 1.6, 10.4), '#d0c8b8', tf(gx, 8, gx, 1, 1, 1, 0, ang + PI / 2, 0));
  // Ruínas dos quartéis em volta do pátio e a capela.
  for (let i = 0; i < 4; i++) {
    const t = (i / 4) * PI * 2 + PI / 4; const r = R * 0.36;
    k.add(box(34, 6, 7), i % 2 ? '#a49e90' : '#bab2a2', tf(Math.cos(t) * r, 3, Math.sin(t) * r, 1, 1, 1, 0, -t + PI / 2, 0));
    for (let w = -2; w <= 2; w++) k.add(box(1.4, 2, 7.2), '#4a4440', tf(Math.cos(t) * r + Math.cos(t + PI / 2) * w * 6, 3.2, Math.sin(t) * r + Math.sin(t + PI / 2) * w * 6, 1, 1, 1, 0, -t + PI / 2, 0));
  }
  k.add(box(10, 9, 14), '#e0d8c8', tf(-6, 4.5, 0)).add(gable(10.6, 3, 14.6), '#8a6a52', tf(-6, 9, 0));
  k.add(box(2, 3, 2), '#e0d8c8', tf(-0.6, 10.5, 0)).add(box(0.2, 1.8, 0.4), '#e0d8c8', tf(-0.6, 13, 0)).add(box(0.2, 0.4, 1.2), '#e0d8c8', tf(-0.6, 13.3, 0));
  k.add(cyl(0.15, 0.2, 18, 5), '#d8d8d8', tf(14, 9, 0));
  k.add(box(0.06, 1.8, 3), '#2a8a3a', tf(14, 17, 1.6)).add(box(0.07, 1.0, 1.6), '#ffd23f', tf(14, 17, 1.6));
  // Rio Guaporé atrás e a mata.
  k.add(box(70, 0.3, 280), '#4a6a5a', tf(-R - 40, 0.15, 0));
  for (let i = 0; i < 12; i++) tree(k, -R - 2 + hash2(601, i) * 8, -120 + i * 22, 11 + hash2(602, i) * 6, 6020 + i);
  return k.model(18);
}

// ───────────────────────────── Acre ─────────────────────────────

/** Geoglifo do Acre: o quadrado e o círculo de valas no pasto, os taludes de terra e a torre de observação de madeira. */
function geoglifo(): Model {
  const k = new Kit();
  k.add(box(150, 0.25, 130), '#7aa84e', tf(-30, 0.12, 0));
  // Vala quadrada (terra escura) com o talude claro por fora, e a vala circular ao lado ligada por um caminho.
  const sq = 70; const vw = 6;
  for (const s of [-1, 1]) {
    k.add(box(sq + vw, 0.2, vw), '#5a3a22', tf(-30, 0.3, s * sq / 2));
    k.add(box(vw, 0.2, sq + vw), '#5a3a22', tf(-30 + s * sq / 2, 0.3, 0));
    k.add(box(sq + vw * 2 + 6, 1.2, 3), '#a8784a', tf(-30, 0.6, s * (sq / 2 + vw / 2 + 2)));
    k.add(box(3, 1.2, sq + vw * 2 + 6), '#a8784a', tf(-30 + s * (sq / 2 + vw / 2 + 2), 0.6, 0));
  }
  const cR = 22;
  for (let q = 0; q < 20; q++) {
    const a0 = (q / 20) * PI * 2; const a1 = ((q + 1) / 20) * PI * 2;
    const p = (a: number, r: number): V3 => [-30 + Math.cos(a) * r, 0.3, 64 + Math.sin(a) * r];
    k.beam(p(a0, cR), p(a1, cR), 5, '#5a3a22', 0.2);
    k.beam(p(a0, cR + 4.5), p(a1, cR + 4.5), 2.6, '#a8784a', 1.0);
  }
  k.add(box(5, 0.2, 14), '#5a3a22', tf(-30, 0.3, 38));
  // Torre de observação: quatro pernas de madeira em tronco de pirâmide, plataforma e cobertura, escada.
  const tx = 20; const tz = -40; const TH = 15;
  for (const dx of [-1, 1]) for (const dz of [-1, 1]) k.beam([tx + dx * 3.2, -0.5, tz + dz * 3.2], [tx + dx * 2, TH, tz + dz * 2], 0.45, '#7a5a3a');
  for (const y of [5, 10]) { const r = 3.2 - (y / TH) * 1.2; for (const s of [-1, 1]) { k.add(box(2 * r, 0.25, 0.25), '#8a6a42', tf(tx, y, tz + s * r)); k.add(box(0.25, 0.25, 2 * r), '#8a6a42', tf(tx + s * r, y, tz)); } }
  k.add(box(5.4, 0.4, 5.4), '#8a6a42', tf(tx, TH, tz));
  for (const s of [-1, 1]) { k.add(box(5.4, 1, 0.15), '#8a6a42', tf(tx, TH + 0.7, tz + s * 2.7)); k.add(box(0.15, 1, 5.4), '#8a6a42', tf(tx + s * 2.7, TH + 0.7, tz)); }
  for (const dx of [-1, 1]) for (const dz of [-1, 1]) k.add(box(0.2, 2.6, 0.2), '#7a5a3a', tf(tx + dx * 2.5, TH + 1.5, tz + dz * 2.5));
  k.add(hip(6.6, 2.2, 6.6), '#c8a860', tf(tx, TH + 2.8, tz));
  k.beam([tx - 2.4, TH, tz + 2.2], [tx - 10, 0, tz + 9], 1.2, '#8a6a42', 0.3);
  // Castanheiras isoladas no pasto (como no Acre).
  for (let i = 0; i < 3; i++) { const x = -100 + i * 8; const z = -50 + i * 50; k.rod([x, -0.5, z], [x, 18, z], 0.5, '#7a6a5a', 5); crown(k, x, 20, z, 7, 3.6, 7, '#3f7f34', 610 + i); }
  return k.model(21);
}

/** Ponte sobre o rio Acre (Estrada do Pacífico): tabuleiro de concreto sobre pilares, o arco central de aço e a mata nas margens. */
function ponteRioAcre(): Model {
  const k = new Kit();
  const L = 300; const deckY = 16; const W = 14;
  k.add(box(110, 0.3, 120), '#8a6a42', tf(-20, 0.15, 0));
  k.add(box(W, 2.2, L), '#d0d0c8', tf(0, deckY - 1.1, 0));
  k.add(box(W - 2, 0.05, L), '#4a4e55', tf(0, deckY + 0.03, 0));
  for (const sx of [-1, 1]) k.add(box(0.4, 1.0, L), '#e8e8e2', tf(sx * (W / 2 - 0.2), deckY + 0.5, 0));
  for (let z = -L / 2 + 20; z < L / 2; z += 40) if (Math.abs(z) > 50) { k.add(box(4, deckY, 3), '#b8b8b0', tf(0, deckY / 2 - 1.5, z)); k.add(box(W, 1.6, 3.4), '#b8b8b0', tf(0, deckY - 2.8, z)); }
  // Arco de aço sobre o vão do rio (dois arcos, um de cada lado, com pendurais).
  const span = 100; const AH = 26; const N = 10;
  for (const sx of [-1, 1]) {
    const P = (t: number): V3 => [sx * (W / 2 + 0.4), deckY + AH * 4 * t * (1 - t), -span / 2 + span * t];
    for (let q = 0; q < N; q++) {
      k.beam(P(q / N), P((q + 1) / N), 1.4, '#3a7ab8', 1.8);
      if (q > 0) { const p = P(q / N); k.beam([p[0], deckY, p[2]], p, 0.25, '#e8e8e2'); }
    }
  }
  for (let q = 2; q < N - 1; q += 2) { const t = q / N; const y = deckY + AH * 4 * t * (1 - t); k.add(box(W + 1, 0.8, 0.8), '#3a7ab8', tf(0, y, -span / 2 + span * t)); }
  k.blink(sphere(0.5, 6, 4), '#ff3a2a', tf(W / 2, deckY + AH + 1, 0));
  for (let z = -L / 2 + 10; z < L / 2; z += 30) for (const sx of [-1, 1]) { k.add(box(0.2, 6, 0.2), '#9aa0a8', tf(sx * (W / 2 - 0.4), deckY + 3, z)); k.light(box(0.8, 0.25, 0.5), '#ffe2a6', tf(sx * (W / 2 - 1.0), deckY + 6, z)); }
  // Barrancas com mata.
  for (let i = 0; i < 14; i++) { const sz = i % 2 ? 1 : -1; tree(k, -60 + hash2(621, i) * 70, sz * (70 + hash2(622, i) * 60), 14 + hash2(623, i) * 8, 6230 + i); }
  return k.model(deckY + AH + 2);
}

/** Castanheira: tronco reto e alto com sapopemas na base, a copa em guarda-chuva acima da mata e os ouriços no chão. */
function castanheira(): Model {
  const k = new Kit();
  const H = 40; const bark = '#7a6a5a';
  k.add(cyl(0.75, 1.15, H, 8), bark, tf(0, H / 2 - 0.5, 0));
  // Sapopemas: aletas triangulares na base.
  for (let q = 0; q < 5; q++) {
    const a = (q / 5) * PI * 2 + 0.3; const c = Math.cos(a); const s = Math.sin(a);
    const g = tris([0.8 * c, 4.5, 0.8 * s, 0.8 * c, -0.5, 0.8 * s, 3.6 * c, -0.5, 3.6 * s]);
    k.add(sheet([0.8 * c, 4.5, 0.8 * s], [0.8 * c, -0.5, 0.8 * s], [3.6 * c, -0.5, 3.6 * s]), '#6a5a4a');
    g.dispose();
  }
  // Galhos no alto e a copa larga.
  const tops: V3[] = [];
  for (let q = 0; q < 6; q++) {
    const a = (q / 6) * PI * 2 + 0.2; const r = 8 + hash2(631, q) * 4;
    const t: V3 = [Math.cos(a) * r, H + 4 + hash2(632, q) * 3, Math.sin(a) * r];
    k.rod([0, H - 6, 0], t, 0.45, bark, 5);
    tops.push(t);
  }
  const greens = ['#3a7a30', '#4a8a38', '#2f6a2a', '#58983e'];
  tops.forEach(([x, y, z], q) => crown(k, x, y + 1.6, z, 7 + hash2(633, q) * 2, 3.4, 7 + hash2(634, q) * 2, greens[q % 4], 6340 + q, 1));
  crown(k, 0, H + 7, 0, 10, 4, 10, '#4a8a38', 635, 1);
  // Sub-bosque: árvores menores e palmeiras em volta, os ouriços e o paneiro no chão.
  for (let i = 0; i < 6; i++) { const a = (i / 6) * PI * 2 + 0.5; tree(k, Math.cos(a) * 14, Math.sin(a) * 14, 8 + hash2(636, i) * 5, 6360 + i); }
  for (let q = 0; q < 10; q++) k.add(sphere(0.35, 5, 3), '#5a3a22', tf(5 + hash2(637, q) * 3, 0.3, -2 + hash2(638, q) * 4));
  k.add(cyl(0.7, 0.5, 0.9, 7, true), '#b89a62', tf(7.5, 0.45, 1.5));
  return k.model(H + 11, [0.72, 1.08]);
}

// ───────────────────────────── Tocantins ─────────────────────────────

/** Palácio Araguaia (Palmas): bloco branco modernista com colunata alta, faixa de vidro acesa, espelho d'água e os mastros. */
function palacioAraguaia(): Model {
  const k = new Kit();
  const D = 36; const L = 90; const H = 18;
  k.add(box(D + 30, 1.2, L + 20), '#d8d4c8', tf(-D / 2 + 6, 0.3, 0));
  k.fac('office', D - 8, H - 4, L - 8, '#9ab8c8', tf(-D / 2, 1 + (H - 4) / 2, 0));
  // Laje de cobertura saliente e a colunata alta na frente.
  k.add(box(D + 4, 1.6, L + 4), '#f6f4ee', tf(-D / 2, H + 0.6, 0));
  for (let i = 0; i <= 14; i++) k.add(box(0.9, H - 1, 0.9), '#f6f4ee', tf(1.2, 1 + (H - 1) / 2, -L / 2 + 1 + i * (L - 2) / 14));
  for (let i = 0; i <= 6; i++) for (const sz of [-1, 1]) k.add(box(0.9, H - 1, 0.9), '#f6f4ee', tf(-3 - i * 5.5, 1 + (H - 1) / 2, sz * (L / 2 + 1.2)));
  // Pórtico central mais alto com a cúpula baixa.
  k.add(box(D + 2, 4, 26), '#f6f4ee', tf(-D / 2 + 1, H + 3, 0));
  k.add(sphere(9, 12, 4, 0, PI * 2, 0, PI / 2), '#e8e4d8', tf(-D / 2, H + 5, 0, 1, 0.45, 1));
  k.light(box(0.3, 1.2, L - 10), '#ffe8b0', tf(1.6, H - 2, 0));
  // Escadaria, espelho d'água e mastros.
  k.add(box(10, 0.6, 30), '#e8e4d8', tf(6, 0.9, 0)).add(box(6, 0.6, 30), '#e8e4d8', tf(4, 1.4, 0));
  for (const sz of [-1, 1]) k.add(box(16, 0.4, 30), '#4a8ab0', tf(16, 0.9, sz * 26));
  const flags: Array<[string, string]> = [['#2a9a4a', '#ffd23f'], ['#2a6ab8', '#ffd23f'], ['#f4f2ea', '#2a6ab8']];
  flags.forEach(([a, b], i) => {
    const z = -10 + i * 10;
    k.add(cyl(0.12, 0.15, 16, 5), '#d8d8d8', tf(22, 8.5, z));
    k.add(box(0.06, 1.8, 3), a, tf(22, 15.4, z + 1.6)).add(box(0.07, 0.8, 3), b, tf(22, 15.4, z + 1.6));
  });
  for (const z of [-40, -20, 20, 40]) lamp(k, 24, z, 6);
  for (let i = 0; i < 6; i++) palm(k, 26, -46 + i * 18.4, 10 + hash2(641, i) * 3, 6410 + i, 0, 6);
  return k.model(H + 9);
}

/** Ponte de Palmas (sobre o lago do Tocantins): tabuleiro baixo e comprido, pilares duplos e a fileira de postes acesos. */
function pontePalmas(): Model {
  const k = new Kit();
  const L = 640; const deckY = 9; const W = 16;
  k.add(box(70, 0.3, L + 80), '#3a7aa0', tf(-8, 0.15, 0));
  k.add(box(W, 1.8, L), '#d8d8d0', tf(0, deckY - 0.9, 0));
  k.add(box(W - 2, 0.05, L), '#4a4e55', tf(0, deckY + 0.03, 0));
  for (const sx of [-1, 1]) k.add(box(0.5, 1.0, L), '#ecece6', tf(sx * (W / 2 - 0.25), deckY + 0.5, 0));
  for (let z = -L / 2 + 10; z < L / 2; z += 32) {
    for (const sx of [-1, 1]) k.add(box(1.6, deckY, 1.6), '#b8b8b0', tf(sx * 4.5, deckY / 2 - 1.5, z));
    k.add(box(W - 2, 1.4, 2.2), '#b8b8b0', tf(0, deckY - 2.4, z));
  }
  // Postes altos dos dois lados (a fileira de luzes que lê de noite) e o canteiro central.
  for (let z = -L / 2 + 26; z < L / 2; z += 32) for (const sx of [-1, 1]) {
    k.add(box(0.3, 11, 0.3), '#9aa0a8', tf(sx * (W / 2 - 0.5), deckY + 5.5, z));
    k.add(box(1.8, 0.2, 0.2), '#9aa0a8', tf(sx * (W / 2 - 1.3), deckY + 11, z));
    k.light(box(1.0, 0.35, 0.6), '#ffe2a6', tf(sx * (W / 2 - 2), deckY + 10.8, z));
  }
  // Cabeceira com aterro e o mirante de pórtico (portal da cidade).
  for (const sz of [-1, 1]) k.add(box(W + 6, deckY + 1, 30), '#b8b0a0', tf(0, (deckY - 1) / 2, sz * (L / 2 + 15)));
  for (const sx of [-1, 1]) k.add(box(1.6, 14, 1.6), '#f4f2ea', tf(sx * (W / 2 + 1), deckY + 7, -L / 2 + 4));
  k.add(box(W + 4, 1.6, 1.6), '#f4f2ea', tf(0, deckY + 14, -L / 2 + 4));
  return k.model(deckY + 16);
}

// ───────────────────────────── Registro ─────────────────────────────

const def = (build: () => Model, place: LandmarkDef['place'], side: LandmarkDef['side'], perLap: number, turn?: number): LandmarkDef =>
  (turn === undefined ? { build, place, side, perLap } : { build, place, side, perLap, turn });

export const LANDMARKS_BRASIL_2_NORTE_NORDESTE: LandmarkRegistry = {
  // Bahia
  morro_pai_inacio: def(morroPaiInacio, 'skyline', 'land', 1),
  igreja_quadrado: def(igrejaQuadrado, 'near', 'land', 1),
  // Sergipe
  canion_xingo: def(canionXingo, 'far', 'any', 1),
  catamara: def(catamara, 'near', 'any', 2),
  praca_sao_francisco: def(pracaSaoFrancisco, 'near', 'land', 1),
  // Alagoas
  farol_piacabucu: def(farolPiacabucu, 'far', 'any', 1),
  dunas_piacabucu: def(dunasPiacabucu, 'far', 'any', 2),
  // Pernambuco
  marco_zero_recife: def(marcoZeroRecife, 'near', 'land', 1),
  ponte_mauricio: def(ponteMauricio, 'near', 'any', 1),
  morro_do_pico: def(morroDoPico, 'skyline', 'land', 1),
  dois_irmaos: def(doisIrmaos, 'far', 'sea', 1),
  // Paraíba
  parque_do_povo: def(parqueDoPovo, 'near', 'land', 1),
  pedra_da_boca: def(pedraDaBoca, 'skyline', 'land', 1),
  // Rio Grande do Norte
  morro_do_careca: def(morroDoCareca, 'far', 'sea', 1),
  cajueiro_gigante: def(cajueiroGigante, 'near', 'any', 1),
  // Ceará
  ponte_dos_ingleses: def(ponteDosIngleses, 'near', 'any', 1),
  falesias_canoa: def(falesiasCanoa, 'far', 'any', 1),
  // Piauí
  manguezal_delta: def(manguezalDelta, 'near', 'any', 2),
  pedras_sete_cidades: def(pedrasSeteCidades, 'far', 'any', 1),
  // Maranhão
  casario_azulejos: def(casarioAzulejos, 'near', 'land', 2),
  morro_das_mesas: def(morroDasMesas, 'skyline', 'land', 1),
  cachoeira_sao_romao: def(cachoeiraSaoRomao, 'far', 'any', 1),
  // Pará
  praia_de_rio: def(praiaDeRio, 'near', 'any', 1),
  bufalo: def(bufalo, 'near', 'land', 2),
  palafita: def(palafita, 'near', 'any', 2),
  // Amazonas
  ponte_rio_negro: def(ponteRioNegro, 'far', 'any', 1),
  bumbodromo: def(bumbodromo, 'far', 'land', 1),
  // Amapá
  vila_serra_do_navio: def(vilaSerraDoNavio, 'near', 'land', 1),
  // Roraima
  monumento_garimpeiro: def(monumentoGarimpeiro, 'near', 'any', 1),
  // Rondônia
  forte_principe_beira: def(fortePrincipeBeira, 'near', 'any', 1),
  // Acre
  geoglifo: def(geoglifo, 'near', 'land', 1),
  ponte_rio_acre: def(ponteRioAcre, 'far', 'any', 1),
  castanheira: def(castanheira, 'near', 'any', 2),
  // Tocantins
  palacio_araguaia: def(palacioAraguaia, 'near', 'land', 1),
  ponte_palmas: def(pontePalmas, 'near', 'any', 1, 0.1),
};
