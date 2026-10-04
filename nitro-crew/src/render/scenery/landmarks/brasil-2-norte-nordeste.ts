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
import { cliff, dune as duneShape, landmarkPart, lathe } from './kit';
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

/** Árvore genérica de mata (tronco + 1–2 copas); `y0` = chão dela (no alto de um paredão). */
function tree(k: Kit, x: number, z: number, h: number, seed: number, greens = ['#3f7f34', '#4a8f3a', '#356f2e'], y0 = 0): void {
  k.rod([x, y0 - 0.5, z], [x, y0 + h * 0.6, z], Math.max(0.2, h * 0.03), '#6a5038', 5);
  crown(k, x, y0 + h * 0.72, z, h * 0.32, h * 0.26, h * 0.32, greens[Math.floor(hash2(seed, 1) * greens.length)], seed);
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

/**
 * Quadrado de Trancoso / Porto Seguro: a igrejinha branca de portas azuis no fim do gramado, as casinhas coloridas dos
 * dois lados e os coqueiros. A igreja vai exagerada (1,2×: ~30 m com a torre) para ficar acima do casario e das
 * palmeiras a 60 m da pista — na escala de antes ela tinha o tamanho das casinhas.
 */
function igrejaQuadrado(): Model {
  const k = new Kit();
  k.add(box(68, 0.4, 56), '#6fae4a', tf(-16, 0.2, 0));
  church(k, -20, 0.2, 0, { wall: '#f6f4ec', trim: '#3f7cc0', towers: 1, cap: 'pyramid', s: 1.2, roof: '#b8603a' });
  k.add(box(4, 1.2, 50), '#c9b48a', tf(16, 0.6, 0)); // mureta da beira do quadrado
  const cols = ['#f2a8b8', '#ffd23f', '#5aa8e0', '#7ac87a', '#f08a4a', '#46c1b8', '#e85a5a', '#b89ae6'];
  for (let i = 0; i < 8; i++) {
    const side = i < 4 ? -1 : 1;
    const x = 8 - (i % 4) * 9;
    cottage(k, x, side * 24, 8, 6, 3.6, cols[i], side * PI / 2);
  }
  for (let i = 0; i < 4; i++) palm(k, 10 - i * 8 + hash2(321, i) * 3, (i % 2 ? 1 : -1) * (15 + hash2(322, i) * 3), 12 + hash2(323, i) * 4, 3230 + i);
  crown(k, -46, 7, -18, 7, 5, 7, '#3f7f34', 324);
  k.rod([-46, 0, -18], [-46, 5, -18], 0.5, '#6a5038');
  return k.model(30);
}

// ───────────────────────────── Sergipe ─────────────────────────────

// Arenito do Xingó: vermelho-vivo e laranja em camadas (o marrom-ferrugem de antes era o tom das mesas do deserto da
// pista e sumia nelas; o vermelho mais escuro segura o contraste na névoa a 200 m+); caatinga cinza-esverdeada no alto.
const XINGO_ROCK = ['#b83a26', '#e8844c', '#9a2e1e', '#c8482c', '#ec8e56', '#a8341f'];
const CAATINGA = ['#7a8a52', '#8a9058', '#6a7a48', '#9a9460'];

/**
 * Água em faixas ao longo de Z, de −X para +X (`xs` = bordas, uma cor por faixa): o reflexo escuro junto da pedra,
 * o fundo, o raso, a espuma, a areia molhada. As bordas de dentro ondulam por hash (a mesma borda nas duas faixas).
 */
function waterBands(k: Kit, xs: number[], colors: string[], len: number, seed: number, wob = 0.3, n = 9): void {
  const edge = (e: number, q: number): number => (e === 0 || e === xs.length - 1 ? xs[e] : xs[e] + (hash2(seed + e, q) - 0.5) * wob * Math.min(xs[e] - xs[e - 1], xs[e + 1] - xs[e]));
  for (let i = 0; i < colors.length; i++) {
    const pts: Array<[number, number]> = [];
    for (let q = 0; q <= n; q++) pts.push([edge(i, q), -len / 2 + (len * q) / n]);
    for (let q = n; q >= 0; q--) pts.push([edge(i + 1, q), -len / 2 + (len * q) / n]);
    k.add(plan(pts, 0.1 + i * 0.03), colors[i], tf(0, 0.02, 0));
  }
}

/** Lâmina d'água em anéis (o raso claro por fora, o fundo escuro no meio): blobs concêntricos levemente mais altos. */
function pond(k: Kit, x: number, z: number, rx: number, rz: number, colors: string[], seed: number, shift = 0): void {
  colors.forEach((c, i) => {
    const f = 1 - i / (colors.length + 0.6);
    k.add(plan(blobPoly(14, rx * f, rz * f, 0.1, seed + i), 0.12 + i * 0.03), c, tf(x + shift * i, 0.02, z));
  });
}

/**
 * Cânion do Xingó: o paredão de arenito vermelho-vivo do outro lado do rio (exagerado: 72 m), em estratos com cornijas e
 * sulcos, a borda quebrada com a caatinga, o talude de pedra caindo na água verde (escura no reflexo da pedra, esmeralda
 * no meio, clara no raso de cá), ilhotas de pedra e o catamarã de passeio dando a escala.
 */
function canionXingo(): Model {
  const k = new Kit();
  const len = 330; const H = 72; const wx = -40;
  waterBands(k, [-50, -30, -8, 14, 28], ['#43473a', '#16705f', '#1f8a76', '#3aa892'], len, 330);
  const wall = cliff({ len, H, strata: XINGO_ROCK, layers: 7, seed: 331, cols: 34, depth: 46, batter: 0.12, gully: 7, gullyLen: 22, bay: 14, ragged: 0.12, ledge: 1.8, talus: 7, talusColor: '#6e4632', top: '#8a8a58', ledgeTop: '#9a8e5c', ends: 0.16 }, tf(wx, 0, 0));
  k.raw(wall.geo);
  // Caatinga na borda de cima.
  for (let i = 0; i < 22; i++) {
    const z = -len / 2 + 22 + i * ((len - 44) / 21) + (hash2(337, i) - 0.5) * 8;
    crown(k, wx + wall.lipAt(z) - 4 - hash2(338, i) * 18, wall.topAt(z) + 1, z, 3.2 + hash2(339, i) * 2, 2, 3.2 + hash2(340, i) * 2, CAATINGA[i % 4], 3370 + i);
  }
  // Margem de cá: lajes baixas de pedra vermelha e moitas, sem esconder a água.
  for (let i = 0; i < 6; i++) {
    const z = -140 + i * 56 + hash2(333, i) * 12;
    k.raw(paint(jitter(dodeca(1), 0.2, 334 + i), XINGO_ROCK[i % 6], tf(31 + hash2(341, i) * 4, 0.3, z, 6, 2, 11)));
    crown(k, 34 + hash2(342, i) * 3, 1.2, z + 14, 2.4, 1.6, 2.4, CAATINGA[(i + 1) % 4], 3420 + i);
  }
  // Ilhotas de pedra no rio.
  for (let i = 0; i < 3; i++) k.raw(paint(jitter(dodeca(1), 0.22, 335 + i), XINGO_ROCK[(i + 2) % 6], tf(-6 + hash2(336, i) * 14, 1.2, -110 + i * 95, 7, 4.5 + i * 2, 8)));
  // Catamarã de passeio (escala do cânion): casco branco, cabine, toldo vermelho, a esteira.
  k.add(box(5.4, 1.6, 17), '#f4f2ea', tf(0, 0.9, 36)).add(box(4.6, 2, 11), '#f4f4ef', tf(0, 2.6, 35)).add(box(5, 0.4, 13), '#d8402a', tf(0, 3.8, 35));
  k.add(box(5, 0.2, 14), '#e8f4f0', tf(0, 0.22, 19));
  return k.model(H + 8, [0.78, 1.08]);
}

/**
 * Catamarã do Xingó: o barco de passeio (dois cascos, dois conveses e o toldo vermelho) no remanso verde junto do
 * paredão vermelho do cânion. O barco é o assunto: grande (exagerado 1,75×: ~32 m) e branco, na frente da pedra.
 */
function catamara(): Model {
  const k = new Kit();
  const wx = -22;
  pond(k, 0, 0, 30, 44, ['#3aa892', '#1f8a76', '#16705f'], 341, -2);
  // O reflexo escuro da pedra no pé do paredão.
  k.add(plan(blobPoly(12, 5, 38, 0.15, 343), 0.22), '#43473a', tf(wx + 7, 0.02, 0));
  const wall = cliff({ len: 96, H: 24, strata: XINGO_ROCK, layers: 5, seed: 342, cols: 16, depth: 18, batter: 0.1, gully: 3, gullyLen: 10, bay: 4, ragged: 0.14, ledge: 0.9, talus: 3, talusColor: '#6e4632', top: '#8a8a58', ledgeTop: '#9a8e5c', ends: 0.2 }, tf(wx, 0, 0));
  k.raw(wall.geo);
  for (let i = 0; i < 9; i++) {
    const z = -38 + i * 9.5 + (hash2(344, i) - 0.5) * 3;
    crown(k, wx + wall.lipAt(z) - 2.5 - hash2(345, i) * 6, wall.topAt(z) + 0.8, z, 2.2 + hash2(346, i), 1.4, 2.2 + hash2(347, i), CAATINGA[i % 4], 3440 + i);
  }
  // O catamarã no referencial dele (escala 1,75), na frente do paredão.
  const G = tf(9, 0, 0, 1.75, 1.75, 1.75);
  const at = (m: THREE.Matrix4): THREE.Matrix4 => G.clone().multiply(m);
  // Cascos (com a proa afilada) e o convés.
  for (const sx of [-1, 1]) {
    k.add(box(2.2, 1.6, 18), '#f6f6f2', at(tf(sx * 3.2, 0.6, 0)));
    k.add(cone(1.25, 3, 4), '#f6f6f2', at(tf(sx * 3.2, 0.6, 10.5, 1, 1, 0.75, PI / 2, 0, 0)));
    k.add(box(2.25, 0.35, 18.1), '#1e5aa8', at(tf(sx * 3.2, 0.1, 0)));
  }
  k.add(box(8.6, 0.5, 18), '#ecece6', at(tf(0, 1.6, -0.5)));
  // Cabine de baixo: janelas escuras, paredes brancas.
  k.add(box(7.4, 2.3, 11), '#f6f6f2', at(tf(0, 3.0, -2)));
  for (const sx of [-1, 1]) k.add(box(0.15, 1.0, 9.6), '#2a4a6a', at(tf(sx * 3.72, 3.3, -2)));
  k.add(box(0.15, 1.0, 5.6), '#2a4a6a', at(tf(0.02, 3.3, 3.55)));
  // Convés de cima com guarda-corpo e toldo vermelho.
  k.add(box(8, 0.3, 13), '#ecece6', at(tf(0, 4.3, -2.5)));
  for (const sx of [-1, 1]) k.add(box(0.1, 0.9, 13), '#f6f6f2', at(tf(sx * 3.95, 4.9, -2.5)));
  for (const sx of [-1, 1]) for (const z of [-8, -2.5, 3]) k.add(box(0.15, 2.4, 0.15), '#f6f6f2', at(tf(sx * 3.7, 5.6, z)));
  k.add(gable(8.4, 1.2, 12.6, 0.1), '#e0362a', at(tf(0, 6.8, -2.5)));
  k.add(box(0.08, 1.6, 0.08), '#d8d8d8', at(tf(0, 8.4, 3.5))).add(box(0.05, 0.6, 1.0), '#2a8a3a', at(tf(0, 8.9, 3.0)));
  // Esteira de espuma na popa.
  k.add(box(9, 0.2, 7), '#e8f4f0', at(tf(0, 0.2, -12.5)));
  return k.model(16);
}

/**
 * Praça São Francisco (São Cristóvão): a igreja e o convento franciscanos de fachada branca e cantaria clara no fundo da
 * praça — a igreja exagerada (1,25×: a torre a ~32 m) acima dos sobrados —, a galeria de arcos do convento, o cruzeiro e
 * o calçamento. Antes, do tamanho dos sobrados e de cantaria ocre, lia como mais um largo colonial.
 */
function pracaSaoFrancisco(): Model {
  const k = new Kit();
  k.add(box(70, 0.3, 76), '#c4b498', tf(-4, 0.15, 0));
  for (let i = 0; i < 8; i++) k.add(box(70, 0.05, 0.3), '#a89878', tf(-4, 0.32, -35 + i * 10));
  // Igreja (uma torre) à esquerda, convento comprido de dois pavimentos à direita, alinhados ao fundo da praça.
  church(k, -20, 0.3, -18, { wall: '#f8f6f0', trim: '#e2d2a8', towers: 1, cap: 'pyramid', s: 1.25, roof: '#a8502e' });
  const cz = 14; const cl = 40;
  k.fac('classic', 14, 10, cl, '#f8f6f0', tf(-30, 5.3, cz));
  k.add(box(14.6, 0.6, cl + 0.6), '#e2d2a8', tf(-30, 10.3, cz));
  k.add(hip(15, 3.6, cl + 1, 0, cl - 13), '#a8502e', tf(-30, 10.6, cz));
  k.add(box(0.3, 3, 2), '#4a2e22', tf(-22.85, 1.8, cz));
  // Galeria de arcos do térreo (pilares) na frente do convento.
  for (let i = 0; i < 9; i++) k.add(box(1, 4, 1), '#f4f0e6', tf(-21.5, 2.3, cz - cl / 2 + 2 + i * 4.5));
  k.add(box(3, 0.8, cl), '#f4f0e6', tf(-21.5, 4.7, cz));
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
  return k.model(34);
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

/**
 * Dunas de Piaçabuçu (foz do São Francisco): o cordão alto (exagerado: 44 m) de areia branca ao fundo com o
 * barlavento de frente para a pista (as ondulações do vento em faixas paralelas à crista viva), duas barcanas de
 * través na frente — o lado do vento claro, a face de avalanche lisa e mais escura —, a lagoa entre elas, a restinga
 * verde e o coqueiral, e o bugue. A areia é branca (neutra) para não sumir no ocre do chão do deserto da pista.
 */
function dunasPiacabucu(): Model {
  const k = new Kit();
  const sand = '#f8f4ec'; const slip = '#d8c4a0';
  // Cordão do fundo: o vento sopra da pista para trás (barlavento para +X, crista a −60 m).
  k.raw(duneShape({ len: 360, H: 44, back: 130, seed: 371, sand, slip, cols: 28, rows: 10, sinuous: 16, ripple: 0.07 }, tf(-62, 0, 0, 1, 1, 1, 0, PI, 0)).geo);
  // Barcanas de través: a crista desce para a pista, um lado claro e o outro na sombra.
  k.raw(duneShape({ len: 130, H: 24, back: 66, seed: 372, sand: '#f4efe4', slip, cols: 12, rows: 8, horns: 22, sinuous: 6 }, tf(14, 0, -100, 1, 1, 1, 0, 1.15, 0)).geo);
  k.raw(duneShape({ len: 110, H: 19, back: 56, seed: 373, sand: '#f6f2e8', slip: '#dcc6a2', cols: 12, rows: 8, horns: 18, sinuous: 5 }, tf(8, 0, 104, 1, 1, 1, 0, -1.05, 0)).geo);
  // Lagoa entre as dunas (rasa e clara na borda) com a orla de capim.
  k.add(plan(blobPoly(14, 26, 36, 0.12, 374), 0.1), '#9ab060', tf(58, 0.02, -8));
  pond(k, 58, -8, 22, 31, ['#8fd0d0', '#4aa8b8', '#2f8aa4'], 374);
  for (let i = 0; i < 10; i++) palm(k, 76 + hash2(375, i) * 12, -150 + i * 33, 14 + hash2(376, i) * 6, 3760 + i);
  for (let i = 0; i < 14; i++) k.raw(paint(jitter(ico(1, 0), 0.25, 377 + i), i % 2 ? '#5f9a40' : '#4a8236', tf(64 + hash2(378, i) * 26, 0.6, -156 + i * 24, 5, 2.6, 5)));
  // Bugue subindo o barlavento (escala).
  k.add(box(2.4, 1.2, 3.6), '#ffcf3a', tf(26, 8.4, 34, 1, 1, 1, 0, PI / 2, -0.25));
  return k.model(46, [0.86, 1.06]);
}

// ───────────────────────────── Pernambuco ─────────────────────────────

/**
 * Marco Zero do Recife: a praça da rosa dos ventos no chão, o casario colorido do Recife Antigo (sobrados estreitos de
 * cores vivas, platibanda branca com pináculos) e, do outro lado do porto, no arrecife, a Torre de Cristal do Parque das
 * Esculturas (Brennand). A pista é de noite e de cidade: o que separa o Marco Zero dos prédios da pista é o casario
 * iluminado de frente — a fachada em cor (luz), não só janela acesa — e a torre acesa, mais alta (exagerada ~1,4×: 46 m)
 * que tudo em volta; a rosa dos ventos tem o anel aceso e os postes de praça.
 */
function marcoZeroRecife(): Model {
  const k = new Kit();
  k.add(box(70, 0.3, 84), '#cfc3a8', tf(-4, 0.15, 0));
  k.add(cyl(18, 18, 0.25, 24), '#e8dcc0', tf(6, 0.42, 0));
  // Rosa dos ventos: estrela de 8 pontas em duas cores por ponta, e o anel aceso.
  const cols = ['#2a6a9a', '#e8c040', '#c84a3a', '#2a8a5a'];
  for (let i = 0; i < 16; i++) {
    const a = (i / 16) * PI * 2; const big = i % 2 === 0; const R = big ? 16 : 9; const w = big ? 2.4 : 1.8;
    const tip: V3 = [6 + Math.cos(a) * R, 0.6, Math.sin(a) * R];
    const l: V3 = [6 + Math.cos(a + PI / 2) * w, 0.6, Math.sin(a + PI / 2) * w];
    const r: V3 = [6 + Math.cos(a - PI / 2) * w, 0.6, Math.sin(a - PI / 2) * w];
    k.add(tris([6, 0.6, 0, ...tip, ...l]), cols[(i >> 1) % 4]);
    k.add(tris([6, 0.6, 0, ...r, ...tip]), big ? '#1e1e24' : '#f4f2ea');
  }
  k.light(cyl(17.4, 17.4, 0.3, 24, true), '#ffd890', tf(6, 0.5, 0));
  for (let i = 0; i < 8; i++) { const a = (i / 8) * PI * 2 + PI / 8; lamp(k, 6 + Math.cos(a) * 21, Math.sin(a) * 21, 5, '#fff1c8'); }
  // Casario do Recife Antigo ao fundo da praça: fachada em cor iluminada (luz), janelas escuras em grade, cornija e
  // platibanda brancas com pináculos; o corpo do sobrado atrás, com as janelas que acendem.
  const sc = ['#e86a8a', '#f2c23a', '#3a9ae0', '#5ac06a', '#f08a3a', '#9a6ae0', '#e84a4a', '#3ac0b8'];
  const fx = -24;
  for (let i = 0; i < 8; i++) {
    const z = -36 + i * 10.3; const fl = 3 + (i % 3 === 1 ? 1 : 0); const h = fl * 4;
    k.fac('classic', 12, h, 9.8, '#d8d0c0', tf(fx - 6.2, h / 2, z));
    k.light(box(0.3, h, 9.6), sc[i], tf(fx, h / 2, z));
    for (let f = 0; f < fl; f++) for (const dz of [-2.4, 2.4]) k.add(box(0.3, 2.2, 1.5), '#2a2430', tf(fx + 0.25, f * 4 + 2.1, z + dz));
    k.light(box(0.6, 0.5, 10), '#d8d4c8', tf(fx + 0.1, h + 0.25, z));
    k.light(box(0.4, 1.4, 6), '#e8e4d8', tf(fx, h + 1.2, z));
    for (const dz of [-4.4, 4.4]) k.light(cone(0.45, 1.6, 4), '#e8e4d8', tf(fx, h + 1.3, z + dz));
  }
  // O porto (água escura) atrás do casario e o arrecife com a Torre de Cristal: coluna de bulbos, acesa.
  k.add(box(30, 0.2, 84), '#1e3446', tf(-48, 0.1, 0));
  k.add(box(12, 2, 40), '#6a6458', tf(-62, 0.6, 22));
  const tower: Array<[number, number]> = [[3.2, 0], [3.4, 6], [2.6, 14], [3.6, 22], [2.4, 30], [3.0, 35], [4.0, 39], [2.2, 43], [0.6, 46]];
  k.glow.push(lathe(tower, 10, '#c8b896', tf(-62, 1.5, 22)));
  k.light(cyl(3.2, 3.2, 3.4, 10), '#a8e0ff', tf(-62, 1.5 + 37, 22));
  k.blink(sphere(0.6, 6, 4), '#ff3a2a', tf(-62, 1.5 + 46.6, 22));
  return k.model(48);
}

/**
 * Ponte Maurício de Nassau (Recife): a ponte de arcos de concreto sobre o Capibaribe, de balaustrada clara, postes de
 * globo e as quatro estátuas de bronze nas cabeceiras. A pista é de noite: os cinco arcos vão contornados de luz (a
 * iluminação de baixo da ponte, que os desenha sobre a água escura), os globos são grandes (para ler a 100 m) e as
 * estátuas têm o pedestal aceso — de noite ela lê como ponte histórica de arcos, não como mais um viaduto.
 */
function ponteMauricio(): Model {
  const k = new Kit();
  const L = 120; const deckY = 8; const W = 14; const LAMP = '#ffe2a6';
  // Rio cruzando por baixo (corre em X, da pista para o fundo) e o cais de pedra das margens.
  k.add(box(120, 0.3, 84), '#3a5a6a', tf(-30, 0.15, 0));
  for (const sz of [-1, 1]) k.add(box(120, 2, 3), '#8a8478', tf(-30, 0.8, sz * 43.5));
  // Tabuleiro em arcos: perfil (z, y) com 5 vãos, extrudado na largura (X).
  const prof: Array<[number, number]> = [[-L / 2, -0.5], [L / 2, -0.5], [L / 2, deckY], [-L / 2, deckY]];
  const holes: Array<Array<[number, number]>> = [];
  const nA = 5; const span = 84 / nA; const r = span / 2 - 1.5; const rise = (t: number): number => 0.3 + Math.sin(t) * r * 0.82;
  for (let a = 0; a < nA; a++) {
    const zc = -42 + (a + 0.5) * span;
    const h: Array<[number, number]> = [[zc + r, 0.2]];
    for (let q = 1; q < 8; q++) { const t = (q / 8) * PI; h.push([zc + Math.cos(t) * r, rise(t)]); }
    h.push([zc - r, 0.2]);
    holes.push(h);
  }
  const deck = extrude(prof, W, holes).translate(0, 0, -W / 2).rotateY(PI / 2);
  k.add(deck, '#d8d2c2');
  // Os arcos contornados de luz na face da frente (e na de trás, para quem já passou).
  for (let a = 0; a < nA; a++) {
    const zc = -42 + (a + 0.5) * span;
    const front: number[] = []; const back: number[] = [];
    for (let q = 0; q < 8; q++) {
      const t0 = (q / 8) * PI; const t1 = ((q + 1) / 8) * PI; const w = 0.9;
      const P = (t: number, d: number, x: number): V3 => [x, rise(t) + Math.sin(t) * d, zc + Math.cos(t) * (r + d)];
      for (const [x, out, f] of [[W / 2 + 0.08, front, 1], [-W / 2 - 0.08, back, -1]] as Array<[number, number[], number]>) {
        const i0 = P(t0, 0, x); const i1 = P(t1, 0, x); const o1 = P(t1, w, x); const o0 = P(t0, w, x);
        // A luz tem um lado só: a face da frente olha para +X, a de trás para −X.
        if (f > 0) out.push(...i0, ...i1, ...o1, ...i0, ...o1, ...o0); else out.push(...i0, ...o1, ...i1, ...i0, ...o0, ...o1);
      }
    }
    k.light(tris(front), '#ffd27a').light(tris(back), '#ffd27a');
  }
  k.add(box(W + 0.6, 0.6, L), '#c4bca8', tf(0, deckY + 0.3, 0));
  k.add(box(W - 2, 0.05, L), '#4a4e55', tf(0, deckY + 0.62, 0));
  // Balaustrada e postes de globo.
  for (const sx of [-1, 1]) {
    k.add(box(0.5, 1.1, L), '#ece6d6', tf(sx * (W / 2 - 0.2), deckY + 1.15, 0));
    for (let z = -L / 2 + 6; z < L / 2; z += 12) {
      k.add(cyl(0.12, 0.18, 5.4, 5, true), '#2e3238', tf(sx * (W / 2 - 0.4), deckY + 0.6 + 2.7, z));
      k.light(new THREE.OctahedronGeometry(0.7, 0), LAMP, tf(sx * (W / 2 - 0.4), deckY + 6.4, z));
    }
  }
  // Estátuas de bronze sobre pedestais altos nas quatro cabeceiras, o pedestal aceso pelos refletores.
  for (const sx of [-1, 1]) for (const sz of [-1, 1]) {
    const x = sx * (W / 2 + 2.2); const z = sz * (L / 2 - 3);
    k.add(box(3.2, 5, 3.2), '#d8d2c2', tf(x, deckY + 1.5, z));
    k.light(box(3.4, 1.2, 3.4), '#e8d8b0', tf(x, deckY + 3.4, z));
    k.add(cyl(0.9, 1.2, 4.4, 6), '#5a4a2a', tf(x, deckY + 6.2, z));
    k.add(sphere(0.7, 6, 4), '#5a4a2a', tf(x, deckY + 9, z));
    k.add(box(0.45, 2.4, 0.45), '#5a4a2a', tf(x, deckY + 8.6, z + 1.0, 1, 1, 1, 0.6, 0, 0));
  }
  // O reflexo dos arcos acesos na água: riscos de luz para o lado da pista.
  for (let a = 0; a < nA; a++) k.light(box(10, 0.06, 1.4), '#9a7a40', tf(W / 2 + 6, 0.34, -42 + (a + 0.5) * span));
  // Rampas de chão nas cabeceiras (aterro).
  for (const sz of [-1, 1]) k.add(box(W, deckY + 1, 14), '#b8b0a0', tf(0, (deckY - 1) / 2, sz * (L / 2 + 7)));
  return k.model(deckY + 10);
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

/**
 * Parque do Povo (o São João de Campina Grande): a Pirâmide — o telhado piramidal de palha laranja sobre os pilares,
 * com as quatro cumeeiras e o beiral contornados de lâmpadas e a estrela no alto —, os cordões de bandeirinhas
 * coloridas que descem da ponta dela para os mastros em volta, como uma tenda de festa, o palco aceso, a fogueira, os
 * balões e o portal de entrada. A pista é de noite: as bandeirinhas (grandes, ~1,4 m, para ler a 70 m) e o contorno
 * da Pirâmide vão no material de luz — o arraial lê pela luz colorida, não pela silhueta escura.
 */
function parqueDoPovo(): Model {
  const k = new Kit();
  k.add(box(84, 0.3, 96), '#b89a72', tf(-10, 0.15, 0));
  // A Pirâmide: pilares de madeira, o beiral e o telhado piramidal.
  const px = -20; const S = 34; const eave = 7.2; const RH = 20; const E = S + 3;
  for (let i = 0; i < 4; i++) for (let j = 0; j < 4; j++) if (i === 0 || i === 3 || j === 0 || j === 3) k.add(box(1.1, eave, 1.1), '#7a4a2a', tf(px - S / 2 + i * S / 3, eave / 2, -S / 2 + j * S / 3));
  k.add(box(S + 2, 1, S + 2), '#a8582a', tf(px, eave - 0.3, 0));
  k.add(hip(E, RH, E), '#e8802a', tf(px, eave, 0));
  const apex: V3 = [px, eave + RH, 0];
  const corners: V3[] = [[px + E / 2, eave, E / 2], [px + E / 2, eave, -E / 2], [px - E / 2, eave, -E / 2], [px - E / 2, eave, E / 2]];
  // Cumeeiras e beiral contornados de luz (o desenho da Pirâmide à noite) e a estrela no alto.
  for (let i = 0; i < 4; i++) {
    const a = corners[i]; const b = corners[(i + 1) % 4];
    k.light(box(1, 1, 1), i % 2 ? '#ffb44a' : '#ffd86a', segMatrix(a, apex).multiply(new THREE.Matrix4().makeScale(0.9, 1, 0.9)));
    k.light(box(1, 1, 1), '#ffc85a', segMatrix([a[0], a[1] - 0.4, a[2]], [b[0], b[1] - 0.4, b[2]]).multiply(new THREE.Matrix4().makeScale(0.8, 1, 0.8)));
  }
  const star: Array<[number, number]> = [];
  for (let q = 0; q < 10; q++) { const a = PI / 2 + (q / 10) * PI * 2; const r = q % 2 ? 1.1 : 2.6; star.push([Math.cos(a) * r, Math.sin(a) * r]); }
  k.light(extrude(star, 0.6).translate(0, 0, -0.3).rotateY(PI / 2), '#fff0a0', tf(px, eave + RH + 2.6, 0));
  // Bandeirinhas: cordões da ponta da Pirâmide para os mastros em volta do arraial, e entre os mastros.
  const flags = ['#ff3a3a', '#ffd23f', '#2a8ae0', '#3ac84a', '#ff8a2a', '#e04ab0', '#8a5ae8'];
  const pole = (x: number, z: number, h: number): V3 => { k.add(box(0.45, h, 0.45), '#6a4a2a', tf(x, h / 2, z)); return [x, h, z]; };
  const string = (a: V3, b: V3, n: number, sag: number, seed: number): void => {
    const P = (t: number): V3 => [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t - sag * 4 * t * (1 - t), a[2] + (b[2] - a[2]) * t];
    const dx = b[0] - a[0]; const dz = b[2] - a[2]; const L = Math.hypot(dx, dz) || 1;
    const ux = dx / L; const uz = dz / L;
    for (let q = 0; q < n; q++) {
      const p = P((q + 0.5) / n); const w = 0.75;
      k.light(sheet([p[0] - ux * w, p[1], p[2] - uz * w], [p[0] + ux * w, p[1], p[2] + uz * w], [p[0], p[1] - 1.5, p[2]]), flags[Math.floor(hash2(seed, q) * flags.length)]);
    }
    for (let q = 0; q < 4; q++) k.add(cyl(0.5, 0.5, 1, 3, true), '#3a3030', segMatrix(P(q / 4), P((q + 1) / 4)).multiply(new THREE.Matrix4().makeScale(0.08, 1, 0.08)));
  };
  const ring: V3[] = [];
  for (let i = 0; i < 10; i++) {
    const a = (i / 10) * PI * 2 + 0.3;
    ring.push(pole(px + Math.cos(a) * 42, Math.sin(a) * 44, 10));
  }
  ring.forEach((p, i) => string([apex[0], apex[1] - 1, apex[2]], p, 18, 2.2, 410 + i));
  ring.forEach((p, i) => { if (Math.cos(((i + 0.5) / 10) * PI * 2 + 0.3) > -0.2) string(p, ring[(i + 1) % 10], 12, 1.0, 430 + i); });
  // Palco com fundo aceso.
  k.add(box(10, 1.4, 22), '#3a3a40', tf(-50, 0.7, 0));
  k.add(box(1, 9, 22), '#2a2a30', tf(-55, 4.5, 0));
  k.light(box(0.3, 6, 18), '#ff9a4a', tf(-54.3, 5, 0));
  k.add(box(12, 0.6, 24), '#2a2a30', tf(-50, 9.3, 0));
  // Fogueira de São João (acesa).
  for (let q = 0; q < 6; q++) { const a = (q / 6) * PI; k.beam([12 + Math.cos(a) * 2, 0, Math.sin(a) * 2], [12 - Math.cos(a) * 2, 0.8, -Math.sin(a) * 2], 0.5, '#5a3a22'); }
  k.light(cone(2.2, 4.4, 5), '#ff7a2a', tf(12, 2.4, 0)).light(cone(1.2, 3, 5), '#ffd040', tf(12, 3.2, 0.4));
  // Balões de enfeite e o portal de entrada com o arco de luzes.
  for (let i = 0; i < 4; i++) k.light(new THREE.OctahedronGeometry(1.4, 0), ['#ff5a5a', '#ffd23f', '#5ab8ff', '#7ae07a'][i], tf(4 - i * 10, 13, (i % 2 ? 1 : -1) * 20, 1, 1.4, 1));
  for (const sz of [-1, 1]) k.add(box(1.4, 10, 1.4), '#8a5a3a', tf(30, 5, sz * 9));
  k.add(gable(4.4, 2.8, 21), '#c8502a', tf(30, 10, 0));
  for (let q = 0; q < 9; q++) { const t = q / 8; k.light(box(0.7, 0.7, 0.7), q % 2 ? '#ffd86a' : '#ff6a4a', tf(30.8, 9.4 + Math.sin(t * PI) * 1.8, -8 + t * 16)); }
  return k.model(30);
}

/**
 * Pedra da Boca: o domo de granito com a "boca" — a cavidade larga e escura aberta no alto da face, de beiço claro —, o
 * outro lajedo ao lado e a caatinga no pé. A boca fica no meio da face da pista e é larga (±0,62 rad) e funda o bastante
 * para continuar escura na névoa do horizonte vista de quem chega dos dois lados (com o marco à direita da pista, quem
 * chega vê −Z; à esquerda, +Z).
 */
function pedraDaBoca(): Model {
  const k = new Kit();
  const H = 190; const RX = 150; const RZ = 130;
  const dome = hill(RX, H, RZ, 431, '#8e877a', null, 3, 0.05, 0.4);
  // A boca: oval deitado escuro no alto da face da pista, com a borda mais clara.
  recolor(dome, (x, y, z) => {
    if (x < 0) return null;
    const d = Math.atan2(z / RZ, x / RX);
    const e = ((y - H * 0.6) / (H * 0.2)) ** 2 + (d / 0.62) ** 2;
    if (e < 1) return e < 0.5 ? '#0c0a08' : '#17120e';
    if (e < 1.5) return '#c2baa8';
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

/**
 * Cajueiro de Pirangi: o maior cajueiro do mundo, uma árvore só do tamanho de um bosque. Lê como UMA copa — um morro
 * de folhas contínuo, mais claro no alto, que desce em saia até o chão nas bordas (os galhos que tocam o chão e
 * enraízam de novo: arcos escuros entre as abas da saia) — com os cajus vermelhos e amarelos pendurados em cachos
 * na borda e o mirante de madeira de telhado vermelho saindo de dentro da copa. Escala exagerada (~1,3×: 18 m) para
 * não sumir entre as árvores da mata tropical a 60–90 m da pista.
 */
function cajueiroGigante(): Model {
  const k = new Kit();
  const bark = '#5e4a36';
  const RX = 44; const RZ = 56; const H = 18;
  const greens = ['#3f8a34', '#4a9a3a', '#357a2e', '#56a442'];
  // A copa: bolhas redondas sobrepostas em três anéis — o miolo alto, o do meio e a saia da borda, cujas bolhas
  // descem até o chão (o domo de folhas contínuo, mais claro em cima).
  const puff = (x: number, y: number, z: number, rr: number, ry: number, color: string, seed: number): void => {
    k.raw(speckle(tintUp(paint(jitter(ico(1, 1), 0.1, seed), color, tf(x, y, z, rr, ry, rr * 0.92, 0, hash2(seed, 3) * PI, 0)), '#9ccc52', 0.45, 0.7), 0.07, seed));
  };
  const rings: Array<[number, number, number, number, number]> = [
    // [raio relativo, quantas, raio da bolha, altura da bolha, centro em y]
    [0.0, 1, 19, 8, H - 8],
    [0.42, 7, 14, 7.5, H - 9.5],
    [0.84, 13, 11, 6.6, 5.2],
  ];
  rings.forEach(([t, n, rr, ry, y], ri) => {
    for (let q = 0; q < n; q++) {
      const a = ((q + (ri === 2 ? 0 : 0.5) + (hash2(453 + ri, q) - 0.5) * 0.3) / n) * PI * 2;
      const f = 0.9 + hash2(455 + ri, q) * 0.2;
      puff(Math.cos(a) * RX * t, y + (hash2(454 + ri, q) - 0.5) * 1.6, Math.sin(a) * RZ * t, rr * f, ry * f, greens[(q + ri) % 4], 4560 + ri * 40 + q);
    }
  });
  // Os galhos que descem até o chão entre as bolhas da saia (e o escuro de dentro da copa, que não deixa ver o céu).
  k.add(cyl(RX * 0.82, RX * 0.86, 7, 12, true), '#1e361b', tf(0, 3.2, 0, 1, 1, RZ / RX));
  for (let q = 0; q < 13; q++) {
    const a = ((q + 0.5) / 13) * PI * 2;
    const c = Math.cos(a); const s = Math.sin(a);
    const p1: V3 = [c * RX * 0.5, 7, s * RZ * 0.5];
    const p2: V3 = [c * RX * 1.0, 0.1, s * RZ * 1.0];
    k.rod([0, 3, 0], p1, 0.9, bark, 5).rod(p1, p2, 0.8, bark, 5);
  }
  // Cajus em cachos na borda da copa (o pedúnculo vermelho ou amarelo e a castanha cinza embaixo), exagerados para ler.
  for (let q = 0; q < 20; q++) {
    const a = ((q + hash2(458, q) * 0.6) / 20) * PI * 2;
    const t = 0.99 + hash2(459, q) * 0.05;
    const x = Math.cos(a) * RX * t; const z = Math.sin(a) * RZ * t;
    const y = 4 + hash2(460, q) * 2.6;
    for (let f = 0; f < 3; f++) {
      const fx = x + Math.cos(a + 1.6) * (f - 1) * 1.4; const fz = z + Math.sin(a + 1.6) * (f - 1) * 1.4;
      k.add(new THREE.OctahedronGeometry(0.8, 0), (q + f) % 3 ? '#e8302a' : '#f6b82c', tf(fx, y - (f % 2) * 1.0, fz, 1, 1.3, 1));
    }
    k.add(new THREE.OctahedronGeometry(0.35, 0), '#7a6a5a', tf(x, y - 1.2, z));
  }
  // Mirante de madeira saindo de dentro da copa (de onde se vê o alto dela), telhado vermelho; a passarela até ele.
  const mx = 30; const mz = 30; const MH = H + 4;
  for (const dx of [-2.2, 2.2]) for (const dz of [-2.2, 2.2]) k.add(box(0.5, MH, 0.5), '#7a5a3a', tf(mx + dx, MH / 2 - 0.5, mz + dz));
  k.add(box(6.4, 0.5, 6.4), '#8a6a42', tf(mx, MH, mz));
  for (const s of [-1, 1]) { k.add(box(6.4, 1.1, 0.2), '#a07a4a', tf(mx, MH + 0.8, mz + s * 3.1)); k.add(box(0.2, 1.1, 6.4), '#a07a4a', tf(mx + s * 3.1, MH + 0.8, mz)); }
  for (const dx of [-2.6, 2.6]) for (const dz of [-2.6, 2.6]) k.add(box(0.25, 2.6, 0.25), '#7a5a3a', tf(mx + dx, MH + 1.6, mz + dz));
  k.add(hip(7.6, 2.8, 7.6), '#c8402a', tf(mx, MH + 2.9, mz));
  k.beam([mx + 2.6, MH * 0.55, mz + 3], [mx + 2.6, 0, mz + 16], 1.6, '#8a6a42', 0.3);
  k.rod([0, -0.5, 0], [0, 4, 0], 1.6, bark, 6);
  return k.model(H, [0.66, 1.1]);
}

// ───────────────────────────── Ceará ─────────────────────────────

/**
 * Ponte dos Ingleses (Fortaleza): o píer comprido sobre estacas mar adentro, de guarda-corpo branco, postes de globo
 * acesos e o mirante de telhado verde na ponta. A pista de Fortaleza é de cidade (sem mar) e de noite: o modelo traz o
 * pedaço de litoral dele — o calçadão com coqueiros, a areia seca, a molhada, a espuma da beira, o raso, o mar mais
 * fundo e escuro, as linhas de espuma das ondas, o espigão de pedras e o reflexo das luzes na água. O píer sai em
 * diagonal (~34°) para o lado de quem vem pela orla — reto para o mar, ele ficava de ponta e sumia —, mais alto e com o
 * guarda-corpo aceso de ponta a ponta: de noite ele é a linha de luz que entra no mar.
 */
function ponteDosIngleses(): Model {
  const k = new Kit();
  const x0 = 14; const deckY = 6.5; const W = 7.4; const len = 102; const shore = 150; const beta = 0.6;
  const LAMP = '#ffe2a6';
  // Litoral em faixas, de dentro (o calçadão, do lado da pista) para o mar.
  waterBands(k, [-124, -48, -17, 0, 1.4, 6, 22, 30], ['#1d5578', '#26809c', '#3aa8aa', '#f2f8f8', '#b49e74', '#e6d4a8', '#b8ae9c'], shore, 460, 0.5);
  // Espuma da beira e das ondas quebradas (em três fileiras): a espuma clareia ao luar (luz fraca), senão o mar some.
  k.light(box(1.1, 0.06, shore), '#5c727c', tf(0.7, 0.36, 0));
  for (let r = 0; r < 3; r++) for (let i = 0; i < 4; i++) {
    const L = 14 + hash2(463 + r, i) * 18;
    k.light(box(0.9, 0.1, L), '#4c626c', tf(-7 - r * 12 + (hash2(464 + r, i) - 0.5) * 3, 0.34, -60 + i * 36 + (hash2(465 + r, i) - 0.5) * 14));
  }
  // Espigão de pedras escuras saindo da areia mar adentro, a espuma na ponta.
  for (let i = 0; i < 12; i++) {
    const x = 8 - i * 3.6; const z = -46 + (hash2(466, i) - 0.5) * 2;
    k.raw(paint(jitter(dodeca(1), 0.25, 467 + i), i % 3 ? '#4a4842' : '#5c5850', tf(x, 0.5, z, 2.2 + hash2(468, i), 1.4 + hash2(469, i) * 0.8, 2.4)));
  }
  k.add(box(5, 0.12, 7), '#eef6f6', tf(-38, 0.32, -46));
  // O píer no referencial dele: +X local mar adentro, a partir da areia, girado `beta` para +Z.
  const G = tf(x0, 0, 0, 1, 1, 1, 0, PI + beta, 0);
  const at = (x: number, y: number, z: number, sx = 1, sy = 1, sz = 1): THREE.Matrix4 => G.clone().multiply(tf(x, y, z, sx, sy, sz));
  k.add(box(len, 0.8, W), '#c8bca4', at(len / 2, deckY, 0));
  k.add(box(len, 0.5, W + 0.4), '#8a7a62', at(len / 2, deckY - 0.6, 0));
  for (let x = 3; x < len; x += 7) for (const sz of [-1, 1]) k.add(box(0.8, deckY, 0.8), '#6a645a', at(x, deckY / 2 - 0.6, sz * (W / 2 - 0.6)));
  // Guarda-corpo aceso (a linha de luz) e os postes de globo.
  for (const sz of [-1, 1]) k.light(box(len, 1.0, 0.3), '#f4e2b8', at(len / 2, deckY + 0.95, sz * W / 2));
  for (let x = 6; x < len - 4; x += 12) for (const sz of [-1, 1]) {
    k.add(box(0.22, 4.6, 0.22), '#2e3238', at(x, deckY + 2.7, sz * (W / 2 - 0.3)));
    k.light(new THREE.OctahedronGeometry(0.75, 0), LAMP, at(x, deckY + 5.4, sz * (W / 2 - 0.3)));
  }
  // O reflexo das luzes na água: riscos para o lado da pista.
  for (let x = 18; x < len; x += 24) { const p = new THREE.Vector3().setFromMatrixPosition(at(x, 0, -W / 2 - 2)); k.light(box(12, 0.06, 1), '#a8803e', tf(p.x + 6, 0.34, p.z)); }
  // A ponta: plataforma mais larga com o quiosque de telhado verde, aceso por dentro.
  const tip = len + 6;
  k.add(box(16, 0.8, 18), '#c8bca4', at(tip, deckY, 0));
  for (let x = tip - 6; x <= tip + 6; x += 6) for (const sz of [-1, 0, 1]) k.add(box(0.8, deckY, 0.8), '#6a645a', at(x, deckY / 2 - 0.6, sz * 7.5));
  for (const dx of [-3.4, 3.4]) for (const dz of [-3.4, 3.4]) k.add(box(0.35, 3.6, 0.35), '#e8e2d0', at(tip + dx, deckY + 2.2, dz));
  k.add(hip(9.4, 3, 9.4), '#3a8a5a', at(tip, deckY + 4, 0));
  k.light(box(6, 1.6, 6), '#ffd890', at(tip, deckY + 2.2, 0));
  for (const sz of [-1, 1]) k.light(box(16, 1.0, 0.3), '#f4e2b8', at(tip, deckY + 0.95, sz * 9));
  k.add(plan(blobPoly(10, 12, 14, 0.2, 470), 0.1), '#d8ecee', at(tip, 0.26 - 0.04, 0));
  // Calçadão: coqueiros e postes acesos.
  for (let i = 0; i < 5; i++) palm(k, x0 + 12, -60 + i * 30, 11 + hash2(462, i) * 3, 4620 + i);
  for (let i = 0; i < 4; i++) lamp(k, x0 + 15, -45 + i * 30, 5, LAMP);
  return k.model(18);
}

/**
 * Falésias de Canoa Quebrada: o paredão de areia colorida à beira-mar em faixas vivas de vermelho e branco (com laranja
 * e creme), os sulcos verticais da chuva, a borda quebrada com mato ralo, a rampa de areia no pé, a praia, a espuma e o
 * mar raso; a lua e a estrela — o símbolo de Canoa — entalhadas grandes e claras na face; as jangadas de vela branca e
 * colorida na areia. O deserto da pista tem mesas ocres em volta: o que separa a falésia delas é o vermelho com o
 * branco, a lua e a estrela, as velas e a altura (exagerada ~1,3×: 42 m).
 */
function falesiasCanoa(): Model {
  const k = new Kit();
  const colors = ['#c0442a', '#f8f1e4', '#d8742e', '#b03824', '#f2d9a8', '#e8a050', '#c85030', '#fbf6ec'];
  const len = 380; const H = 42; const wx = -10; const talus = 7;
  // Praia, areia molhada, espuma, mar raso e o mais fundo (de −X para +X: o mar fica do lado da pista).
  waterBands(k, [-6, 26, 33, 35.5, 46, 60], ['#efdcb0', '#cdb084', '#f4fbff', '#4fc0b8', '#2a9ab0'], len, 470);
  for (let i = 0; i < 5; i++) k.add(box(0.8, 0.12, 30 + hash2(478, i) * 40), '#eaf6fa', tf(41 + hash2(479, i) * 3, 0.26, -150 + i * 72));
  const wall = cliff({ len, H, strata: colors, layers: 9, seed: 471, cols: 38, depth: 36, batter: 0.16, gully: 4.5, gullyLen: 12, bay: 10, ragged: 0.08, ledge: 0.7, talus, talusColor: '#ecd4a6', top: '#cdb98a', ends: 0.12 }, tf(wx, 0, 0));
  k.raw(wall.geo);
  // Mato ralo no alto.
  for (let i = 0; i < 16; i++) {
    const z = -170 + i * 22.5 + (hash2(473, i) - 0.5) * 8;
    crown(k, wx + wall.lipAt(z) - 3 - hash2(472, i) * 14, wall.topAt(z) + 0.6, z, 4, 1.3, 4, i % 2 ? '#8a9a4a' : '#9aa856', 4720 + i);
  }
  // A lua crescente e a estrela entalhadas na face, grandes e claras (a areia de dentro, lisa), inclinadas com ela.
  const faceX = (y: number, z: number): number => wx + wall.footAt(z) + (wall.lipAt(z) - wall.footAt(z)) * ((y - talus) / (wall.topAt(z) - talus)) + 1.2;
  const lean = Math.atan(0.16 + (4.5 * 0.65) / H);
  const moon: Array<[number, number]> = [];
  for (let q = 0; q <= 10; q++) { const a = -PI * 0.72 + (q / 10) * PI * 1.44; moon.push([Math.cos(a + PI) * 13, Math.sin(a + PI) * 13]); }
  for (let q = 10; q >= 0; q--) { const a = -PI * 0.56 + (q / 10) * PI * 1.12; moon.push([Math.cos(a + PI) * 10.4 - 4.6, Math.sin(a + PI) * 10.4]); }
  const SYM = '#fbf3e2';
  k.add(extrude(moon.map(([u, v]): [number, number] => [-v, u]), 3).rotateY(PI / 2), SYM, tf(faceX(25, 10) - 1.5, 25, 10, 1, 1, 1, 0, 0, lean));
  const star: Array<[number, number]> = [];
  for (let q = 0; q < 10; q++) { const a = PI / 2 + (q / 10) * PI * 2; const r = q % 2 ? 3.4 : 8.4; star.push([Math.cos(a) * r, Math.sin(a) * r]); }
  k.add(extrude(star, 3).rotateY(PI / 2), SYM, tf(faceX(29, -18) - 1.5, 29, -18, 1, 1, 1, 0, 0, lean));
  // Jangadas na praia: casco de madeira, mastro e a vela triangular alta.
  const sails = ['#fbf8f0', '#f4f0e6', '#ffd23f', '#fbf8f0', '#e8463a'];
  for (let i = 0; i < 5; i++) {
    const z = -120 + i * 58 + hash2(475, i) * 10; const x = 21 + hash2(476, i) * 4;
    k.add(box(2.6, 0.6, 8), '#b08a5a', tf(x, 0.6, z));
    k.rod([x, 0.6, z + 1.2], [x, 12.4, z + 1.6], 0.1, '#6a4a2a', 4);
    k.add(sheet([x, 1.4, z + 1.3], [x, 12.2, z + 1.7], [x, 1.6, z - 5.4]), sails[i]);
  }
  for (let i = 0; i < 4; i++) { const z = 30 + i * 12; k.add(box(4, 2.6, 4), '#8a6a42', tf(6, 1.3, z)); k.add(hip(6, 2, 6), '#c8a860', tf(6, 2.6, z)); }
  return k.model(H + 2, [0.8, 1.06]);
}

// ───────────────────────────── Piauí ─────────────────────────────

/**
 * Manguezal do Delta do Parnaíba: o cartão-postal é o guará — a ave vermelho-vivo que pousa aos bandos no mangue no fim
 * da tarde e deixa a copa pontilhada de vermelho. O mangue em fila na beira do canal (copa verde-escura sobre as
 * raízes-escora que arqueiam até a lama), dezenas de guarás pousados no alto da copa e um bando em voo por cima (voando
 * ao longo da margem, asas em V: de lado não somem), exagerados (~2×: 1,4 m) para ler a 80 m; a voadeira no canal.
 */
function manguezalDelta(): Model {
  const k = new Kit();
  const RED = '#f2302a'; const RED2 = '#dc2a2c';
  // Canal de água salobra e a lama escura no pé do mangue.
  water(k, 4, 0, 34, 50, '#4f7262', 481, 14);
  k.add(plan(blobPoly(12, 22, 46, 0.12, 482), 0.25), '#4e5a40', tf(-14, 0.04, 0));
  const greens = ['#2f6a34', '#3a7a3a', '#2a5a30', '#356e36'];
  const perch: Array<[number, number, number, number]> = [];
  for (let i = 0; i < 14; i++) {
    const x = -16 + hash2(483, i) * 12; const z = -42 + i * 6.4 + (hash2(484, i) - 0.5) * 3;
    const H = 9 + hash2(485, i) * 4;
    const hub: V3 = [x, 2.6, z];
    // Raízes-escora: arcos do pé do tronco até a lama.
    for (let q = 0; q < 4; q++) {
      const a = (q / 4) * PI * 2 + hash2(486, i * 7 + q);
      const r = 2.8 + hash2(487, i * 7 + q) * 1.4;
      const mid: V3 = [x + Math.cos(a) * r * 0.55, 2.0, z + Math.sin(a) * r * 0.55];
      k.rod(hub, mid, 0.17, '#6e5a46', 3).rod(mid, [x + Math.cos(a) * r, -0.3, z + Math.sin(a) * r], 0.17, '#6e5a46', 3);
    }
    k.rod(hub, [x, H * 0.7, z], 0.26, '#6e5a46', 4);
    const rr = 5 + hash2(488, i) * 1.5; const cy = H * 0.78;
    crown(k, x, cy, z, rr, 3, rr, greens[i % 4], 4880 + i);
    crown(k, x + 2.4, H * 0.62, z - 1.8, rr * 0.7, 2.2, rr * 0.7, greens[(i + 1) % 4], 4900 + i);
    perch.push([x, cy, z, rr]);
  }
  // Guarás pousados no alto da copa: corpo, pescoço e cabeça (o bico curvo some de longe), virados para lados diferentes.
  const bird = (x: number, y: number, z: number, yaw: number, color: string): void => {
    const c = Math.cos(yaw); const s = Math.sin(yaw);
    k.add(new THREE.OctahedronGeometry(0.7, 0), color, tf(x, y + 0.5, z, 1, 1, 1, 0, yaw, 0).multiply(new THREE.Matrix4().makeScale(1.0, 0.75, 0.55)));
    k.add(new THREE.OctahedronGeometry(0.34, 0), color, tf(x + c * 0.8, y + 1.3, z - s * 0.8));
  };
  perch.forEach(([x, cy, z, rr], i) => {
    for (let q = 0; q < 4; q++) {
      const a = (q / 4) * PI * 2 + hash2(489, i * 5 + q) * 1.2;
      const d = rr * (0.25 + 0.5 * hash2(490, i * 5 + q));
      const y = cy + 3 * Math.sqrt(Math.max(0, 1 - (d / rr) ** 2)) - 0.3;
      bird(x + Math.cos(a) * d, y, z + Math.sin(a) * d, hash2(491, i * 5 + q) * PI * 2, (i + q) % 3 ? RED : RED2);
    }
  });
  // O bando em voo, ao longo da margem (asas para os lados em V: de frente elas mostram a área). Visto de baixo, contra
  // o céu, a asa na sombra virava urubu: o bando vai no material de luz (sem sombra), vermelho chapado como no poente.
  for (let q = 0; q < 22; q++) {
    const x = -14 + hash2(492, q) * 26; const y = 15 + hash2(493, q) * 9; const z = -36 + (q / 22) * 72 + (hash2(494, q) - 0.5) * 6;
    const yaw = (hash2(495, q) - 0.5) * 0.8 + (q % 4 === 0 ? PI : 0);
    const m = tf(x, y, z, 1, 1, 1, 0, yaw, 0);
    k.light(new THREE.OctahedronGeometry(0.6, 0), RED, m.clone().multiply(new THREE.Matrix4().makeScale(0.6, 0.6, 1.4)));
    for (const sx of [-1, 1]) k.light(sheet([0, 0, -0.6], [0, 0, 0.7], [sx * 2.2, 1.3, -0.1]), RED2, m);
  }
  // Voadeira de passeio com toldo azul.
  k.add(box(2.2, 0.8, 8), '#f4f2ea', tf(20, 0.5, 8)).add(cone(1.1, 2, 4), '#f4f2ea', tf(20, 0.5, 13, 1, 0.7, 1, PI / 2, 0, PI / 4));
  for (const dz of [-3, 2]) for (const sx of [-1, 1]) k.add(box(0.1, 1.6, 0.1), '#d8d8d8', tf(20 + sx * 0.9, 1.7, 8 + dz));
  k.add(box(2.4, 0.15, 6), '#2a6ab8', tf(20, 2.5, 7.5));
  return k.model(24, [0.72, 1.08]);
}

/**
 * Pedras de Sete Cidades: as torres de arenito ruiniforme — maciças, em estratos, de topo abaulado, com o rachado de
 * casco de tartaruga (cada face de um tom) — que de longe parecem uma cidade em ruínas, o Arco do Triunfo vazado na
 * frente e o cerrado em volta. As torres eram pilhas de discos com o céu entre eles (lia como pratos empilhados).
 */
function pedrasSeteCidades(): Model {
  const k = new Kit();
  const rock = ['#a87c56', '#8e6a4a', '#b88e64', '#7e6450', '#9c7454'];
  const tower = (x: number, z: number, H: number, rx: number, rz: number, seed: number): void => {
    const t = Math.min(10, H * 0.25);
    const body = plan(blobPoly(9, rx, rz, 0.12, seed), H - t * 0.5, [], Math.max(2, Math.round(H / 6)));
    jitter(body, 0.03, seed);
    const g = paint(body, rock[0], tf(x, t * 0.5, z));
    strata(g, rock, Math.max(5, H / 6), seed);
    k.raw(speckle(g, 0.12, seed)); // o casco de tartaruga: tom por face
    k.raw(speckle(tintUp(paint(jitter(sphere(1, 9, 3, 0, PI * 2, 0, PI / 2), 0.08, seed + 2), rock[2], tf(x, H - 0.3, z, rx * 0.97, Math.min(4, H * 0.1), rz * 0.97)), '#7a7a4c', 0.8, 0.5), 0.1, seed));
    k.raw(speckle(paint(jitter(cyl(0.98, 1.3, 1, 9, true), 0.08, seed + 1, true), '#a88c64', tf(x, t / 2 - 1, z, rx, t + 2, rz)), 0.07, seed));
  };
  tower(0, 0, 48, 18, 15, 491);
  tower(-30, -42, 40, 15, 13, 492);
  tower(-20, 36, 34, 13, 12, 493);
  tower(-58, 4, 44, 17, 15, 494);
  tower(14, -56, 22, 10, 9, 495);
  // Arco do Triunfo: dois pilares e a laje, com o vão aberto para a pista.
  tower(4, 51, 20, 5, 5, 496);
  tower(4, 71, 20, 5, 5, 497);
  k.raw(speckle(strata(paint(jitter(box(11, 6, 30), 0.06, 498), '#9a7656', tf(4, 21, 61)), rock, 2.5, 498), 0.1, 498));
  // Cerrado em volta.
  for (let i = 0; i < 9; i++) tree(k, 26 + hash2(499, i) * 14, -70 + i * 17, 5 + hash2(500, i) * 3, 5000 + i, ['#7a8a44', '#6a7e3e', '#8a9650']);
  return k.model(52, [0.8, 1.08]);
}

// ───────────────────────────── Maranhão ─────────────────────────────

/**
 * Casario de azulejos de São Luís: sobrados coloniais de dois e três andares com a fachada inteira de azulejo português —
 * o xadrez de ladrilhos azul e branco, amarelo, verde —, portas e janelas altas de moldura branca, sacadas de ferro,
 * cornijas brancas, telhado de barro e um mirante. A pista é de noite: a fachada de azulejo vai no material de luz (a
 * iluminação de frente do Centro Histórico), em ladrilhos de 1,6 m que se leem a 50 m; sem isso a cor sumia no escuro e
 * o casario virava mais uma fileira de prédios da cidade.
 */
function casarioAzulejos(): Model {
  const k = new Kit();
  // Azulejo: [fundo, ladrilho] de cada sobrado (tons de luz um pouco abaixo da cor: à noite a luz é ×2).
  const tiles: Array<[string, string]> = [['#2a58a8', '#d8dce4'], ['#d8b040', '#e8e2cc'], ['#2a7a68', '#d8e4dc'], ['#3a68b8', '#e0e4ea'], ['#c87a68', '#ecdcd0'], ['#1e4c98', '#c8d8ec']];
  const T = 1.6;
  k.add(box(30, 0.3, 68), '#9a9080', tf(-6, 0.15, 0));
  for (let i = 0; i < 6; i++) {
    const z = -27.5 + i * 11; const floors = i % 3 === 1 ? 2 : 3; const h = floors * 4.2; const zw = 10.8;
    const [base, tile] = tiles[i];
    k.fac('classic', 14, h, zw, '#d8d0c0', tf(-7.1, h / 2, z));
    // A fachada de azulejo: o fundo e os ladrilhos claros em xadrez (quadrados de uma face, para a pista).
    k.light(box(0.2, h, zw), base, tf(0.05, h / 2, z));
    const out: number[] = [];
    const nz = Math.floor(zw / T); const ny = Math.floor(h / T);
    for (let a = 0; a < nz; a++) for (let b = 0; b < ny; b++) {
      if ((a + b) % 2) continue;
      const z0 = z - zw / 2 + (zw - nz * T) / 2 + a * T; const y0 = b * T + (h - ny * T) / 2;
      out.push(0.18, y0, z0, 0.18, y0 + T, z0 + T, 0.18, y0, z0 + T, 0.18, y0, z0, 0.18, y0 + T, z0, 0.18, y0 + T, z0 + T);
    }
    k.light(tris(out), tile);
    // Portas e janelas altas (escuras, moldura branca), sacadas de ferro, cornijas, beiral e o telhado.
    for (let f = 0; f < floors; f++) for (const dz of [-3.4, 0, 3.4]) {
      k.add(box(0.2, 2.8, 1.5), '#f4f2ea', tf(0.3, f * 4.2 + 1.7, z + dz));
      k.add(box(0.2, 2.4, 1.1), f === 0 && dz === 0 ? '#3a2a20' : '#1e2430', tf(0.38, f * 4.2 + 1.6, z + dz));
    }
    for (let f = 1; f <= floors; f++) k.add(box(0.5, 0.35, 11), '#f4f2ea', tf(0.25, f * 4.2 - 0.1, z));
    for (let f = 1; f < floors; f++) k.add(box(1.1, 0.9, 9), '#22262e', tf(0.85, f * 4.2 + 0.55, z)).add(box(1.2, 0.15, 9.2), '#f4f2ea', tf(0.85, f * 4.2 + 0.08, z));
    k.add(box(0.6, h, 0.5), '#f4f2ea', tf(0.2, h / 2, z - 5.3));
    k.add(hip(14.8, 3, 11.6, 3, 0), '#b8552e', tf(-7, h, z));
    if (i === 2) { k.fac('house', 5, 3.6, 5, '#e8e0c8', tf(-7, h + 2.6, z)); k.add(hip(5.8, 1.6, 5.8), '#b8552e', tf(-7, h + 4.4, z)); }
  }
  // Lampiões de parede acesos e os postes da calçada de pedra.
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

/**
 * Cachoeira de São Romão (Rio Farinha, Chapada das Mesas): o rio largo que despenca numa cortina só pela borda de um
 * paredão de arenito vermelho. Lê como queda d'água (a régua das Cataratas): a água domina a face — a cortina ocupa o
 * vão largo da ferradura (exagerado: ~150 m), lançada do lábio e abrindo para baixo, em faixas de tons de
 * branco-azulado (os riscos, não um painel liso) —, a névoa branca (luz, sem sombra) esconde o pé e a mata fechada
 * faz a borda de cima, também atrás do vão (a ilha e as margens do rio de cima); o poço verde e as pedras na frente.
 */
function cachoeiraSaoRomao(): Model {
  const k = new Kit();
  const rock = ['#8a5a3e', '#a06a48', '#7a4e38', '#b07a52', '#946044'];
  const H = 32; const wx = -16; const half = 76; const LEN = 226;
  const wall = cliff({ len: LEN, H, strata: rock, layers: 6, seed: 521, cols: 28, depth: 46, batter: 0.06, gully: 3, gullyLen: 14, bay: 5, ragged: 0.06, ledge: 1.2, talus: 4, talusColor: '#5e5a3c', top: '#4f8a3a', ledgeTop: '#4f7a36', ends: 0.12, notch: { z: 0, half, h: H - 4, recess: 10 } }, tf(wx, 0, 0));
  k.raw(wall.geo);
  const lip0 = wx + wall.lipAt(0);
  // O rio de cima chegando pelo vão.
  k.add(box(44, 0.4, 2 * half + 4), '#4f8f8a', tf(lip0 - 22, H - 3.8, 0));
  // A cortina: faixas do lábio até o poço, lançadas para a frente no alto e abrindo para baixo, em tons diferentes.
  const tones = ['#ffffff', '#eaf5fc', '#d6eaf5', '#f4fbff', '#c8e2ef'];
  const N = 16; const T = [0, 0.3, 1];
  for (let i = 0; i < N; i++) {
    const zc = -half + ((i + 0.5) * 2 * half) / N;
    const top = wall.topAt(zc) + 0.4; const xl = wx + wall.lipAt(zc) + 0.4; const w = (2 * half) / N + 0.6;
    const P = (s: number, t: number): V3 => [xl + 5.5 * Math.sqrt(t), top - (top + 0.2) * t, zc + s * w * (1 + 0.12 * t)];
    for (let b = 0; b < 2; b++) {
      const out: number[] = [];
      for (let j = 0; j < T.length - 1; j++) {
        const s0 = -0.5 + b / 2; const s1 = -0.5 + (b + 1) / 2;
        const a = P(s0, T[j]); const bb = P(s1, T[j]); const c = P(s1, T[j + 1]); const d = P(s0, T[j + 1]);
        // A face de luz tem um lado só: o quadrilátero vira para +X (quem olha).
        out.push(...a, ...c, ...d, ...a, ...bb, ...c);
      }
      k.light(tris(out), tones[Math.floor(hash2(531 + b, i) * tones.length)]);
    }
  }
  // Névoa e espuma no pé (luz, sem sombra: de longe uma faixa branca macia que esconde onde a água bate).
  const MIST = ['#e4eef2', '#d8e6ec', '#eef4f6'];
  for (let i = 0; i < 12; i++) {
    const z = -half - 10 + ((i + 0.5) * (2 * half + 20)) / 12;
    k.light(jitter(ico(1, 0), 0.14, 540 + i), MIST[i % 3], tf(lip0 + 8 + hash2(541, i) * 3, 3 + hash2(542, i) * 1.5, z, 9, 5.5 + hash2(543, i) * 2, 9));
  }
  for (let i = 0; i < 3; i++) k.light(jitter(ico(1, 0), 0.14, 545 + i), MIST[(i + 1) % 3], tf(lip0 + 5, 9 + i * 2, -30 + i * 30, 7, 4, 12));
  // Poço (claro na borda, escuro no meio) e a espuma.
  pond(k, lip0 + 30, 0, 32, half + 20, ['#5aa0a0', '#3f8a8a', '#2f7676'], 522, -3);
  k.light(box(6, 0.6, 2 * half + 8), '#e8f6ff', tf(lip0 + 7, 0.3, 0));
  // Mata fechada no alto: nas abas do paredão (copa a cada ~8 m) e atrás do vão, nas margens e na ilha do rio de cima.
  const greens = ['#3f7f34', '#4a8f3a', '#356f2e', '#2f6a33'];
  for (let z = -LEN / 2 + 8; z <= LEN / 2 - 8; z += 8) {
    if (Math.abs(z) < half - 2) continue;
    const i = Math.round(z);
    const x = wx + wall.lipAt(z) - 4 - hash2(527, i) * 6; const y0 = wall.topAt(z) - 0.5;
    k.rod([x, y0 - 0.5, z], [x, y0 + 5, z], 0.3, '#6a5038', 4);
    crown(k, x, y0 + 7, z, 5 + hash2(528, i), 4.5, 5 + hash2(529, i), greens[Math.abs(i) % 4], 5280 + i);
  }
  for (let z = -half + 4; z <= half - 4; z += 10) {
    const i = Math.round(z);
    crown(k, lip0 - 40 - hash2(536, i) * 8, H + 6 + hash2(537, i) * 3, z, 7, 6, 6, greens[Math.abs(i) % 4], 5370 + i);
  }
  for (let i = 0; i < 6; i++) k.add(jitter(dodeca(1), 0.2, 529 + i), '#6a5a4a', tf(lip0 + 18 + hash2(530, i) * 20, 0.6, -60 + i * 24, 3, 1.6, 3));
  return k.model(H + 12, [0.78, 1.06]);
}

// ───────────────────────────── Pará ─────────────────────────────

/**
 * Praia de rio (Alter do Chão, o "Caribe amazônico"): a faixa de areia branquíssima no rio azul-esverdeado, as barracas
 * de palha com os guarda-sóis coloridos, os barcos de madeira e a Serra da Piroca (o morrinho de topo chato) atrás.
 * O cartão-postal é a areia branca contra a água e a mata: o banco de areia sobe numa crista suave (~5 m) de frente
 * para a pista, branco, e corre a frente inteira — baixo e atrás da água, ele virava uma lasca sob a serra.
 */
function praiaDeRio(): Model {
  const k = new Kit();
  water(k, -6, 0, 48, 60, '#2aa0b0', 541, 14);
  // O banco de areia branca: crista de ~4 m ao longo da frente, a face lisa para a pista.
  const bank = hill(16, 5.6, 57, 542, '#f8f3e4', null, 2, 0.06, 0.3, tf(10, 0, 0));
  k.raw(speckle(bank, 0.02, 542));
  k.raw(speckle(hill(22, 2.4, 40, 546, '#f2ead4', null, 1, 0.08, 0.3, tf(-14, 0, 6)), 0.02, 546));
  // Serra da Piroca (morrinho de topo chato) ao fundo, de mata.
  const serra = hill(34, 34, 46, 543, '#5a7040', null, 1, 0.12, 0.4, tf(-64, 0, -10));
  const sp = serra.attributes.position as THREE.BufferAttribute;
  for (let i = 0; i < sp.count; i++) if (sp.getY(i) > 22) sp.setY(i, 22 + (sp.getY(i) - 22) * 0.1); // topo chato
  k.raw(speckle(tintUp(serra, '#3f7a34', 0.45), 0.07, 543));
  // Barracas de palha na areia, com mesas e guarda-sóis coloridos.
  const umb = ['#e83a2a', '#2a7ae0', '#ffd23f', '#e83a2a', '#2a9a4a', '#2a7ae0'];
  for (let i = 0; i < 4; i++) {
    const z = -36 + i * 24; const x = 6 + hash2(544, i) * 4; const y = 3.2;
    for (const dx of [-2.2, 2.2]) for (const dz of [-2.2, 2.2]) k.add(box(0.3, 3.4, 0.3), '#7a5a3a', tf(x + dx, y + 1.4, z + dz));
    k.add(hip(7.2, 2.8, 7.2), '#c8a860', tf(x, y + 3.1, z));
  }
  for (let i = 0; i < 6; i++) {
    const z = -46 + i * 18 + hash2(547, i) * 4; const x = 14 + hash2(548, i) * 3; const y = 2.4;
    k.add(cyl(0.05, 0.05, 2.6, 3, true), '#f4f2ea', tf(x, y + 1.3, z));
    k.add(cone(2.8, 1.5, 8), umb[i], tf(x, y + 3.1, z));
    k.add(box(1.4, 0.8, 1.4), '#8a6a42', tf(x - 1.6, y + 0.4, z + 1.2));
  }
  // Barcos de madeira coloridos no rio, na frente da praia, e os coqueiros.
  for (let i = 0; i < 3; i++) { const z = -40 + i * 38; k.add(box(2.6, 1.6, 11), ['#2a6ad0', '#e8c040', '#d8302a'][i], tf(32, 0.8, z)).add(box(2.4, 2, 3.6), '#f4f2ea', tf(32, 2.4, z - 1)); }
  for (let i = 0; i < 3; i++) palm(k, 4, -46 + i * 46, 10 + hash2(545, i) * 4, 5450 + i);
  return k.model(24, [0.94, 1.05]);
}

/**
 * Búfalos do Marajó: a manada (×2,3, para ler a 70 m da pista) dentro do campo alagado claro — o céu refletido faz o escuro do
 * búfalo saltar —, quase todos de lado para a pista (o comprido do bicho é o que lê), os da água afundados até o
 * joelho, garças-vaqueiras brancas no lombo, o vaqueiro montado num búfalo na frente com a vara, o curral de três
 * réguas, a porteira alta, o barracão de palha e a mangueira.
 */
function bufalo(): Model {
  const k = new Kit();
  const S = 2.3;
  // Peças baixadas (parts.ts): o búfalo (o boi recolorido de ardósia) entra no lugar do de caixas, encaixado pelo
  // lombo na altura do lombo do procedural (1,925 × S: o vaqueiro senta nele como antes), a cabeça (+X da peça) para
  // onde a procedural olha (+Z local girado `yaw`); a garça-vaqueira no lombo, com 0,5 m × S (a peça é a garça-branca
  // grande, de 1 m: a vaqueira é a metade). Com a peça, a manada fica com 8 + o do vaqueiro (orçamento do marco).
  const buffaloPart = landmarkPart('bufalo');
  const egretPart = landmarkPart('garca');
  // Campo: capim claro em volta, a lâmina d'água (clara, do céu) e touceiras de capim aquático.
  k.add(plan(blobPoly(14, 30, 40, 0.12, 550), 0.08), '#9ab45a', tf(-2, 0.02, 0));
  pond(k, -3, 2, 25, 34, ['#a4c4bc', '#8cb4ae'], 551);
  for (let i = 0; i < 8; i++) k.raw(paint(jitter(ico(1, 0), 0.25, 556 + i), i % 2 ? '#7fa050' : '#94b45a', tf(-18 + hash2(557, i) * 32, 0.2, -27 + i * 7.5, 1.8 + hash2(558, i), 0.5, 1.8 + hash2(559, i))));
  const one = (x: number, z: number, yaw: number, rider: boolean, seed: number, wade = 0, egret = false): void => {
    const s = S; const c = Math.cos(yaw); const sn = Math.sin(yaw);
    const P = (lx: number, y: number, lz: number): THREE.Matrix4 => tf(x + lx * c + lz * sn, y - wade, z - lx * sn + lz * c, 1, 1, 1, 0, yaw, 0);
    const at = (lx: number, y: number, lz: number): V3 => { const v = new THREE.Vector3().setFromMatrixPosition(P(lx, y, lz)); return [v.x, v.y, v.z]; };
    const hide = hash2(seed, 1) > 0.3 ? '#2e2a28' : '#4a3c34';
    if (buffaloPart) k.raw(buffaloPart.at({ back: 1.925 * s }, x, -wade, z, yaw - PI / 2));
    else {
      k.add(box(1.25 * s, 1.15 * s, 2.5 * s), hide, P(0, 1.35 * s, 0));
      k.add(box(1.35 * s, 0.6 * s, 1.0 * s), hide, P(0, 1.85 * s, 0.75 * s));
      for (const lx of [-0.4, 0.4]) for (const lz of [-0.95, 0.95]) k.add(box(0.28 * s, 0.9 * s, 0.28 * s), hide, P(lx * s, 0.45 * s, lz * s));
      k.add(box(0.6 * s, 0.6 * s, 0.9 * s), hide, P(0, 1.4 * s, 1.55 * s));
      // Chifres largos e curvados para trás.
      for (const side of [-1, 1]) {
        const a = at(side * 0.25 * s, 1.75 * s, 1.5 * s); const b = at(side * 0.85 * s, 1.95 * s, 1.15 * s); const d = at(side * 0.75 * s, 2.15 * s, 0.75 * s);
        k.beam(a, b, 0.14 * s, '#d8ccb8').beam(b, d, 0.11 * s, '#d8ccb8');
      }
    }
    if (rider) {
      k.add(box(0.5 * s, 0.8 * s, 0.4 * s), '#e8dcc0', P(0, 2.45 * s, 0.1 * s));
      k.add(box(0.36 * s, 0.36 * s, 0.36 * s), '#8a5a3a', P(0, 3.05 * s, 0.15 * s));
      k.add(cyl(0.45 * s, 0.45 * s, 0.08 * s, 8), '#c8a860', P(0, 3.27 * s, 0.15 * s)).add(cyl(0.2 * s, 0.22 * s, 0.25 * s, 6), '#c8a860', P(0, 3.4 * s, 0.15 * s));
      k.beam(at(0.35 * s, 1.2 * s, 0.6 * s), at(0.45 * s, 3.8 * s, 1.4 * s), 0.07 * s, '#7a5a3a');
    } else if (egretPart ? egret : hash2(seed, 5) > 0.5) {
      // Garça-vaqueira no lombo.
      if (egretPart) { const [ex, ey, ez] = at(0.1 * s, 1.925 * s, -0.4 * s); k.raw(egretPart.at({ height: 0.5 * s }, ex, ey, ez, yaw - PI / 2)); } else k.add(new THREE.OctahedronGeometry(0.3 * s, 0), '#f6f6f2', P(0.1 * s, 2.1 * s, -0.4 * s).multiply(tf(0, 0, 0, 0.8, 0.9, 1.4)));
    }
  };
  // Manada no alagado: de lado para a pista (yaw 0 ou π), alguns virados; os de dentro d'água afundados.
  // Com a peça da garça, as vaqueiras vão no lombo de uns búfalos escolhidos (EGRET_BACKS), com ou sem a peça do búfalo.
  for (let i = 0; i < (buffaloPart ? 8 : 11); i++) {
    const side = hash2(555, i) < 0.75;
    const yaw = (side ? (i % 2 ? 0 : PI) : hash2(565, i) * PI * 2) + (hash2(566, i) - 0.5) * 0.5;
    one(-12 + hash2(553, i) * 22, -31 + i * 6.2 + (hash2(554, i) - 0.5) * 2.5, yaw, false, 552 + i, 0.3 * S, EGRET_BACKS.includes(i));
  }
  // O vaqueiro montado, na frente, de lado para quem chega.
  one(19, 8, PI + 0.25, true, 560);
  // Barracão de palha do retiro (onde o vaqueiro guarda a sela).
  for (const dx of [-3, 3]) for (const dz of [-4, 0, 4]) k.add(box(0.35, 3.4, 0.35), '#6a4a2a', tf(-27 + dx, 1.7, 8 + dz));
  k.add(gable(8, 3.4, 10.4, 0.6), '#c8a860', tf(-27, 3.4, 8));
  // Curral de três réguas e a porteira alta na frente.
  const cx = -24; const cz = -30;
  for (let i = 0; i < 6; i++) for (const sz of [-1, 1]) k.add(box(0.3, 2.4, 0.3), '#6a4a2a', tf(cx - 6 + i * 2.4, 1.2, cz + sz * 6));
  for (const sz of [-1, 1]) for (const y of [0.7, 1.4, 2.1]) k.add(box(13, 0.18, 0.14), '#9a7a52', tf(cx, y, cz + sz * 6));
  for (const sz of [-1, 1]) k.add(box(0.6, 7.2, 0.6), '#5a3e26', tf(16, 3.6, -40 + sz * 4.5));
  k.add(box(0.6, 0.7, 10.4), '#5a3e26', tf(16, 7.1, -40)).add(box(0.25, 1.1, 5.6), '#e8e0c8', tf(16.4, 6.0, -40));
  for (let i = 0; i < 4; i++) k.add(box(0.14, 0.16, 8.4), '#9a7a52', tf(16, 0.7 + i * 0.5, -40));
  // Mangueira grande na beira do campo.
  k.rod([-20, -0.5, 28], [-20, 5, 28], 0.8, '#5a4636', 6);
  crown(k, -20, 9, 28, 8, 5, 8, '#2f6a2a', 561, 1);
  crown(k, -16, 7, 33, 5, 3.4, 5, '#3a7a32', 562);
  // Garças brancas no alagado (procedurais: pequenas, e o orçamento da peça vai para a manada).
  for (let i = 0; i < 5; i++) {
    const x = 6 + i * 2.6; const z = -6 + (i % 2) * 5;
    k.add(box(0.08, 1.0, 0.08), '#2a2a2a', tf(x, 0.5, z)).add(new THREE.OctahedronGeometry(0.55, 0), '#f6f6f2', tf(x, 1.25, z, 0.8, 0.85, 1.4));
  }
  return k.model(14);
}

/** Búfalos da manada (índice) que levam a garça-vaqueira quando a peça da garça existe (dentro dos 8 da manada curta). */
const EGRET_BACKS: readonly number[] = [1, 3, 6];

/**
 * Palafitas: casinhas de madeira de cores vivas no alto de estacas sobre o rio barrento (o assoalho a ~4,6 m: o vão de
 * água e as estacas embaixo é o que lê de longe), varanda, passarelas de tábua entre elas e até a margem, canoas e
 * açaizeiros atrás (os da frente tapavam as casas).
 */
function palafita(): Model {
  const k = new Kit();
  water(k, 0, 0, 30, 46, '#7a6a48', 571, 14);
  const cols = ['#2a8ad8', '#f05a82', '#4ac86a', '#f0a030', '#f2d040'];
  const houses: Array<[number, number, number]> = [[6, -26, 0], [-8, -10, 0.15], [4, 6, -0.1], [-10, 24, 0.2], [8, 30, 0]];
  const y = 4.6;
  houses.forEach(([x, z, yaw], i) => {
    const w = 6.5 + hash2(572, i) * 2; const d = 7.5 + hash2(573, i) * 2;
    for (const dx of [-1, 0, 1]) for (const dz of [-1, 1]) k.add(box(0.35, y + 1, 0.35), '#5a4a38', tf(x + dx * (w / 2 - 0.4), (y - 1) / 2, z + dz * (d / 2 - 0.4)));
    k.add(box(w + 1.6, 0.3, d + 0.6), '#8a6a42', tf(x + 0.8, y, z, 1, 1, 1, 0, yaw, 0));
    k.add(box(w, 3, d), cols[i], tf(x, y + 1.65, z, 1, 1, 1, 0, yaw, 0));
    k.add(gable(w + 1, 2, d + 1), i % 2 ? '#9aa0a8' : '#b89a62', tf(x, y + 3.15, z, 1, 1, 1, 0, yaw, 0));
    k.add(box(0.2, 2.1, 1), '#3a2a20', tf(x + w / 2 + 0.05, y + 1.2, z - 1, 1, 1, 1, 0, yaw, 0));
    k.add(box(0.2, 1, 1.4), '#f4f2ea', tf(x + w / 2 + 0.05, y + 1.8, z + 1.6, 1, 1, 1, 0, yaw, 0));
    // Guarda-corpo da varanda (na frente, para a pista).
    k.add(box(0.12, 0.9, d + 0.4), '#d8c8a8', tf(x + w / 2 + 1.5, y + 0.6, z, 1, 1, 1, 0, yaw, 0));
  });
  // Passarelas de tábua entre as casas e até a margem (+X).
  for (let i = 0; i + 1 < houses.length; i++) {
    const [ax, az] = houses[i]; const [bx, bz] = houses[i + 1];
    k.beam([ax, y, az], [bx, y, bz], 1.2, '#9a7a52', 0.15);
  }
  k.beam([8, y, 30], [30, 0.4, 34], 1.2, '#9a7a52', 0.15);
  k.beam([6, y, -26], [30, 0.4, -30], 1.2, '#9a7a52', 0.15);
  // Canoas e açaizeiros (atrás das casas).
  for (let i = 0; i < 3; i++) k.add(box(1, 0.5, 5), '#5a3a22', tf(14 + i * 3, 0.3, -12 + i * 10, 1, 1, 1, 0, 0.3 * i, 0));
  for (let i = 0; i < 9; i++) acai(k, -24 + hash2(574, i) * 8, -40 + i * 10, 13 + hash2(575, i) * 5, 5750 + i);
  return k.model(18);
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

/**
 * Vila Serra do Navio: a vila modernista da mineradora (Bratke, anos 1950) no meio da mata do Amapá — a fileira de casas
 * iguais, brancas, sobre pilotis, com os brises de madeira na fachada e o telhado borboleta, de frente para a pista (a
 * repetição é o que diz "vila planejada"), o gramado aberto na frente, a caixa-d'água de concreto alta (o marco da vila,
 * exagerada a ~30 m) e a mata só atrás. Antes as casas eram térreas, baixas e atrás de árvores: lia como sítio.
 */
function vilaSerraDoNavio(): Model {
  const k = new Kit();
  k.add(box(64, 0.3, 84), '#6fae4a', tf(-8, 0.15, 0));
  const S = 1.3;
  const house = (x: number, z: number, c: string): void => {
    const p = 2.0; // pilotis
    for (const dx of [-3, 3]) for (const dz of [-4.5, 4.5]) k.add(box(0.4, p, 0.4), '#8a8a88', tf(x + dx * S, p / 2, z + dz * S));
    k.add(box(8 * S, 0.4, 11 * S), '#c8c4bc', tf(x, p + 0.2, z));
    k.add(box(7.4 * S, 3 * S, 10.4 * S), '#f6f4ee', tf(x, p + 0.4 + 1.5 * S, z));
    // Brises de madeira na fachada (para a pista) e a porta.
    for (let i = 0; i < 9; i++) k.add(box(0.18, 2.4 * S, 0.22), c, tf(x + 3.8 * S, p + 0.4 + 1.5 * S, z + (-4.4 + i * 1.1) * S));
    k.add(box(0.2, 2.2, 1.3), c === '#8a5a3a' ? '#2a6ab8' : '#d8402a', tf(x + 3.86 * S, p + 1.5, z + 3.6 * S));
    // Telhado borboleta: duas águas que caem para o meio.
    for (const s of [-1, 1]) k.add(box(4.6 * S, 0.3, 12 * S), '#8a8c8e', tf(x + s * 2.2 * S, p + 0.6 + 3.2 * S, z, 1, 1, 1, 0, 0, s * 0.26));
  };
  for (let i = 0; i < 4; i++) house(12, -36 + i * 24, ['#8a5a3a', '#7a6a4a', '#9a6a42', '#8a5a3a'][i]);
  for (let i = 0; i < 3; i++) house(-14, -24 + i * 24, ['#7a6a4a', '#8a5a3a', '#9a6a42'][i]);
  // Caixa-d'água de concreto (o marco da vila): coluna, a taça e a tampa cônica.
  const cx = -30; const cz = -32; const TH = 22;
  k.add(cyl(1.5, 2, TH, 8), '#e4e0d8', tf(cx, TH / 2, cz));
  k.add(cyl(6.4, 3.4, 5, 10), '#ece8e0', tf(cx, TH + 2.5, cz)).add(cyl(6.4, 6.4, 3, 10), '#ece8e0', tf(cx, TH + 6.5, cz)).add(cone(6.8, 2, 10), '#9a9c9e', tf(cx, TH + 9, cz));
  k.blink(sphere(0.35, 6, 4), '#ff3a2a', tf(cx, TH + 10.4, cz));
  // Ruas de saibro e a mata atrás.
  k.add(box(4, 0.35, 84), '#b8946a', tf(0, 0.18, 0));
  for (let i = 0; i < 7; i++) tree(k, -38 + hash2(581, i) * 6, -36 + i * 12, 10 + hash2(582, i) * 4, 5820 + i);
  return k.model(TH + 10);
}

// ───────────────────────────── Roraima ─────────────────────────────

/**
 * Monumento ao Garimpeiro (Boa Vista): o garimpeiro de cócoras com a bateia, grande (exagerado: ~12 m de figura), num
 * pedestal alto no meio do espelho d'água da praça, palmeiras imperiais e postes. A pista é de noite: a figura e o
 * pedestal são banhados pelos holofotes (material de luz, bronze dourado), o espelho d'água e os jatos acesos — antes
 * era uma sombra de 9 m no escuro, com quatro holofotes de 0,6 m no pé.
 */
function monumentoGarimpeiro(): Model {
  const k = new Kit();
  k.add(box(44, 0.3, 50), '#c8c0b0', tf(-4, 0.15, 0));
  k.add(plan(ellipse(16, 17, 17), 0.9), '#bab2a2', tf(0, 0, 0));
  k.light(plan(ellipse(16, 16, 16), 0.2), '#2a5a80', tf(0, 0.8, 0));
  // Jatos d'água em volta do pedestal (acesos).
  for (let i = 0; i < 8; i++) { const a = (i / 8) * PI * 2; k.light(cone(0.5, 3.2, 4), '#cfe8ff', tf(Math.cos(a) * 9, 2.6, Math.sin(a) * 9)); }
  // Pedestal alto, aceso pelos holofotes.
  const y0 = 5.6; const S = 3.0;
  k.light(box(8, y0 - 1, 8), '#a8a090', tf(0, (y0 - 1) / 2 + 1, 0));
  k.add(box(9, 0.6, 9), '#7a7468', tf(0, y0 + 0.3 - 0.6, 0));
  // Figura de cócoras virada para a pista, segurando a bateia (cone raso largo), em bronze iluminado. A estátua baixada
  // (peça `garimpeiro`, parts.ts) entra no lugar dela, da mesma altura (4,05 × S sobre o pedestal), de frente (+X).
  const bronze = '#b09060';
  const statue = landmarkPart('garimpeiro');
  if (statue) k.glow.push(statue.at({ height: 4.05 * S }, 0, y0, 0, 0));
  else {
    k.light(box(1.6 * S, 1.2 * S, 2.0 * S), bronze, tf(-0.6 * S, y0 + 0.6 * S, 0)); // pernas dobradas
    k.light(box(1.2 * S, 2.0 * S, 1.6 * S), bronze, tf(-0.5 * S, y0 + 2.0 * S, 0, 1, 1, 1, 0, 0, -0.35)); // tronco inclinado
    k.light(sphere(0.48 * S, 7, 5), bronze, tf(0.2 * S, y0 + 3.3 * S, 0));
    k.light(cyl(0.75 * S, 0.75 * S, 0.12 * S, 8), '#8a6a40', tf(0.2 * S, y0 + 3.62 * S, 0)).light(cyl(0.38 * S, 0.42 * S, 0.4 * S, 6), '#8a6a40', tf(0.2 * S, y0 + 3.85 * S, 0));
    for (const s of [-1, 1]) k.light(box(1, 1, 1), bronze, segMatrix([-0.2 * S, y0 + 2.7 * S, s * 0.8 * S], [1.3 * S, y0 + 1.5 * S, s * 1.1 * S]).multiply(new THREE.Matrix4().makeScale(0.35 * S, 1, 0.35 * S)));
    k.light(cyl(1.7 * S, 0.4 * S, 0.5 * S, 10), '#9a7a4a', tf(1.6 * S, y0 + 1.5 * S, 0));
  }
  // Holofotes no pé do pedestal, palmeiras imperiais e postes da praça.
  for (let i = 0; i < 4; i++) { const a = (i / 4) * PI * 2 + PI / 4; k.light(box(0.9, 0.6, 0.9), '#fff1c8', tf(Math.cos(a) * 6, 1.3, Math.sin(a) * 6)); }
  for (const z of [-20, 20]) for (const x of [12, -16]) palm(k, x, z, 14, 590 + x + z, 0, 6);
  for (const z of [-10, 10]) lamp(k, 16, z, 6);
  return k.model(y0 + 4.05 * S + 1);
}

// ───────────────────────────── Rondônia ─────────────────────────────

/**
 * Real Forte Príncipe da Beira: a fortaleza de pedra em estrela de quatro baluartes no meio da mata do Guaporé (um
 * baluarte apontado para a pista). Lê pela muralha alta de cantaria clara (exagerada ~1,5×: 15 m, contra o verde), a
 * escarpa inclinada no pé, o parapeito e as guaritas brancas de cúpula nas pontas e nos ombros dos baluartes — o
 * desenho de forte colonial —, o portão branco de frontão, o mastro com a bandeira; o rio atrás.
 */
function fortePrincipeBeira(): Model {
  const k = new Kit();
  const R = 92; const H = 15;
  // Estrela de 4 baluartes em ponta de lança (polar: raio relativo e desvio de ângulo), muralha com o pátio dentro.
  const pts: Array<[number, number]> = []; const posts: Array<[number, number]> = [];
  for (let i = 0; i < 4; i++) {
    const a = (i / 4) * PI * 2;
    const P = (r: number, da: number): [number, number] => [Math.cos(a + da) * R * r, Math.sin(a + da) * R * r];
    pts.push(P(0.58, -0.3), P(0.74, -0.36), P(1, 0), P(0.74, 0.36), P(0.58, 0.3));
    posts.push(P(0.975, 0), P(0.725, -0.345), P(0.725, 0.345));
  }
  const sc = (f: number): Array<[number, number]> => pts.map(([x, z]): [number, number] => [x * f, z * f]);
  const stone = '#cbbd9e';
  // Muralha de cantaria com as fiadas (faixas um pouco mais escuras) e a escarpa clara no pé.
  const wall = paint(plan(pts, H + 2, [sc(0.86)], 4), stone, tf(0, -2, 0));
  recolor(wall, (_x, y) => (Math.floor((y + 2) / 4.25) % 2 ? '#c2b394' : null));
  k.raw(speckle(wall, 0.04, 601));
  k.add(plan(sc(1.07), 3.2, [sc(0.95)]), '#b3a688', tf(0, -0.8, 0)); // escarpa
  k.add(plan(sc(1.012), 1.3, [sc(0.95)]), '#e2d8c2', tf(0, H, 0)); // parapeito
  k.add(plan(sc(1.15), 0.3, [sc(1.06)]), '#4f7a3a', tf(0, 0, 0)); // fosso seco, de capim
  k.add(plan(sc(0.86), 0.4), '#7a9a4a', tf(0, 0, 0));
  // Guaritas brancas de cúpula nas pontas e nos ombros dos baluartes.
  for (const [x, z] of posts) {
    k.add(cyl(1.9, 1.9, 4.4, 8), '#f4f0e6', tf(x, H + 2.2, z));
    k.add(sphere(2.1, 8, 3, 0, PI * 2, 0, PI / 2), '#f4f0e6', tf(x, H + 4.4, z));
    k.add(box(0.3, 1.6, 1.0), '#3a3430', tf(x + Math.sign(x) * 1.75, H + 2.4, z));
  }
  // Portão na cortina da frente-direita (entre os baluartes de +X e +Z): fachada branca, arco escuro e frontão.
  const gx = R * 0.58 * Math.cos(PI / 4) + 1.5; const ang = -PI / 4;
  k.add(box(4, H - 1, 14), '#f2eee2', tf(gx, (H - 1) / 2, gx, 1, 1, 1, 0, ang, 0));
  k.add(box(4.4, 6.4, 5), '#2a2622', tf(gx + 0.3, 3.2, gx + 0.3, 1, 1, 1, 0, ang, 0));
  k.add(gable(4.2, 3, 14.4), '#f2eee2', tf(gx, H - 1, gx, 1, 1, 1, 0, ang + PI / 2, 0));
  // Ruínas dos quartéis em volta do pátio e a capela.
  for (let i = 0; i < 4; i++) {
    const t = (i / 4) * PI * 2 + PI / 4; const r = R * 0.36;
    k.add(box(34, 8, 7), i % 2 ? '#b8ad98' : '#c4b8a2', tf(Math.cos(t) * r, 4, Math.sin(t) * r, 1, 1, 1, 0, -t + PI / 2, 0));
    for (let w = -2; w <= 2; w++) k.add(box(1.4, 2.4, 7.2), '#4a4440', tf(Math.cos(t) * r + Math.cos(t + PI / 2) * w * 6, 4.4, Math.sin(t) * r + Math.sin(t + PI / 2) * w * 6, 1, 1, 1, 0, -t + PI / 2, 0));
  }
  k.add(box(10, 11, 14), '#ece4d4', tf(-6, 5.5, 0)).add(gable(10.6, 3.4, 14.6), '#8a6a52', tf(-6, 11, 0));
  k.add(box(2, 3, 2), '#ece4d4', tf(-0.6, 12.5, 0)).add(box(0.3, 2, 0.5), '#ece4d4', tf(-0.6, 15, 0)).add(box(0.3, 0.5, 1.4), '#ece4d4', tf(-0.6, 15.4, 0));
  // Mastro com a bandeira no baluarte da frente.
  k.add(cyl(0.2, 0.28, 16, 5), '#d8d8d8', tf(R * 0.86, H + 8, 0));
  k.add(box(0.08, 2.6, 4.4), '#2a8a3a', tf(R * 0.86, H + 14.4, 2.3)).add(box(0.1, 1.4, 2.4), '#ffd23f', tf(R * 0.86, H + 14.4, 2.3));
  // Rio Guaporé atrás e a mata.
  k.add(box(70, 0.3, 280), '#4a6a5a', tf(-R - 40, 0.15, 0));
  for (let i = 0; i < 12; i++) tree(k, -R - 2 + hash2(601, i) * 8, -120 + i * 22, 11 + hash2(602, i) * 6, 6020 + i);
  return k.model(H + 6);
}

// ───────────────────────────── Acre ─────────────────────────────

/**
 * Geoglifo do Acre: o quadrado e o círculo de valas no pasto, cada um com o talude claro por fora, ligados por um
 * caminho — o desenho que só se vê de cima. Da pista (câmera a 2 m, a 60–90 m de lado) um desenho rente ao chão é uma
 * linha: aqui ele fica numa encosta suave voltada para a pista (~10°, o platô onde os geoglifos estão), com a vala
 * larga de terra vermelho-escura contra o capim verde-vivo, e a torre de observação de madeira no pé. Castanheiras
 * isoladas no alto do platô, como no Acre.
 */
function geoglifo(): Model {
  const k = new Kit();
  const S = 0.18; const x0 = 24; // inclinação da encosta e o pé dela (y = 0)
  const up = (x: number): number => Math.max(0, (x0 - x) * S);
  // Deita a peça na encosta: cada vértice sobe a altura do chão debaixo dele.
  const onSlope = (g: Geo): Geo => {
    const p = g.attributes.position as THREE.BufferAttribute;
    for (let i = 0; i < p.count; i++) p.setY(i, p.getY(i) + up(p.getX(i)));
    g.computeVertexNormals();
    return g;
  };
  const xb = -92; const top = up(xb); const L = 190;
  // O platô: a encosta da frente (o pasto) e o fundo que desce atrás.
  k.raw(speckle(paint(extrude([[x0 + 6, -0.6], [x0, 0], [xb, top], [xb - 26, top * 0.7], [xb - 40, -0.6]], L).translate(0, 0, -L / 2), '#7cb04c'), 0.05, 611));
  const ditch = '#6a321c'; const bank = '#b8804e';
  // Vala quadrada (terra escura, 8 m) com o talude claro por fora.
  const cx = -34; const sq = 72; const vw = 8;
  const flat = (g: Geo, color: string, m: THREE.Matrix4): void => { k.raw(onSlope(paint(g, color, m))); };
  for (const s of [-1, 1]) {
    flat(box(sq + vw, 0.5, vw), ditch, tf(cx, 0.25, s * sq / 2));
    flat(box(vw, 0.5, sq + vw), ditch, tf(cx + s * sq / 2, 0.25, 0));
    flat(box(sq + vw * 2 + 6, 1.4, 3.4), bank, tf(cx, 0.5, s * (sq / 2 + vw / 2 + 2.2)));
    flat(box(3.4, 1.4, sq + vw * 2 + 6), bank, tf(cx + s * (sq / 2 + vw / 2 + 2.2), 0.5, 0));
  }
  // Vala circular ao lado, ligada ao quadrado por um caminho.
  const cR = 24; const cz = 68;
  for (let q = 0; q < 20; q++) {
    const a0 = (q / 20) * PI * 2; const a1 = ((q + 1) / 20) * PI * 2;
    const p = (a: number, r: number, y: number): V3 => [cx + Math.cos(a) * r, y, cz + Math.sin(a) * r];
    k.raw(onSlope(paint(box(1, 1, 1), ditch, segMatrixScaled(p(a0, cR, 0.25), p(a1, cR, 0.25), vw * 0.85, 0.5))));
    k.raw(onSlope(paint(box(1, 1, 1), bank, segMatrixScaled(p(a0, cR + vw * 0.5 + 2, 0.5), p(a1, cR + vw * 0.5 + 2, 0.5), 3.2, 1.4))));
  }
  flat(box(6, 0.5, 18), ditch, tf(cx, 0.25, cz - cR - 8));
  // Torre de observação no pé da encosta: quatro pernas de madeira em tronco de pirâmide, plataforma, cobertura, escada.
  const tx = 20; const tz = -42; const ty = up(tx); const TH = 16;
  for (const dx of [-1, 1]) for (const dz of [-1, 1]) k.beam([tx + dx * 3.4, ty - 0.5, tz + dz * 3.4], [tx + dx * 2.1, ty + TH, tz + dz * 2.1], 0.5, '#7a5a3a');
  for (const y of [5, 10]) { const r = 3.4 - (y / TH) * 1.3; for (const s of [-1, 1]) { k.add(box(2 * r, 0.25, 0.25), '#8a6a42', tf(tx, ty + y, tz + s * r)); k.add(box(0.25, 0.25, 2 * r), '#8a6a42', tf(tx + s * r, ty + y, tz)); } }
  k.add(box(5.6, 0.4, 5.6), '#8a6a42', tf(tx, ty + TH, tz));
  for (const s of [-1, 1]) { k.add(box(5.6, 1, 0.15), '#8a6a42', tf(tx, ty + TH + 0.7, tz + s * 2.8)); k.add(box(0.15, 1, 5.6), '#8a6a42', tf(tx + s * 2.8, ty + TH + 0.7, tz)); }
  for (const dx of [-1, 1]) for (const dz of [-1, 1]) k.add(box(0.2, 2.6, 0.2), '#7a5a3a', tf(tx + dx * 2.6, ty + TH + 1.5, tz + dz * 2.6));
  k.add(hip(7, 2.4, 7), '#c8a860', tf(tx, ty + TH + 2.8, tz));
  k.beam([tx - 2.4, ty + TH, tz + 2.3], [tx - 4, 0, tz + 14], 1.2, '#8a6a42', 0.3);
  // Castanheiras isoladas no alto do platô.
  for (let i = 0; i < 3; i++) { const x = xb + 6 + i * 3; const z = -64 + i * 58; const y = up(x); k.rod([x, y - 0.5, z], [x, y + 14, z], 0.6, '#7a6a5a', 5); crown(k, x, y + 16, z, 8, 3.6, 8, '#3f7f34', 610 + i); }
  return k.model(top + 16);
}

/** Barra (caixa unitária) de `a` até `b` ao longo do chão, largura `w` (perpendicular, em XZ) e altura `h`. */
function segMatrixScaled(a: V3, b: V3, w: number, h: number): THREE.Matrix4 {
  const dx = b[0] - a[0]; const dz = b[2] - a[2]; const len = Math.hypot(dx, dz);
  return tf((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, (a[2] + b[2]) / 2, len + 0.6, h, w, 0, -Math.atan2(dz, dx), 0);
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

/**
 * Palácio Araguaia (Palmas): a sede do governo no meio da Praça dos Girassóis — o bloco modernista atrás da colunata
 * alta de pilares brancos, a laje de cobertura saliente, o pórtico central com a cúpula baixa, a escadaria, o espelho
 * d'água e os mastros. A pista é de noite: a colunata, a laje e a cúpula vão banhadas de luz (material de luz, branco
 * quente) e o corpo de vidro fica escuro atrás — sem isso o palácio era o bloco de janelas acesas de um escritório.
 */
function palacioAraguaia(): Model {
  const k = new Kit();
  const D = 36; const L = 90; const H = 18; const LIT = '#e8e0cc';
  k.add(box(D + 30, 1.2, L + 20), '#d8d4c8', tf(-D / 2 + 6, 0.3, 0));
  k.fac('office', D - 8, H - 4, L - 8, '#4a6070', tf(-D / 2, 1 + (H - 4) / 2, 0));
  // Laje de cobertura saliente e a colunata alta na frente (pilares de 1,4 m), banhadas de luz.
  k.light(box(D + 4, 1.8, L + 4), LIT, tf(-D / 2, H + 0.7, 0));
  for (let i = 0; i <= 14; i++) k.light(box(1.4, H - 1, 1.4), LIT, tf(1.4, 1 + (H - 1) / 2, -L / 2 + 1 + i * (L - 2) / 14));
  for (let i = 0; i <= 6; i++) for (const sz of [-1, 1]) k.light(box(1.2, H - 1, 1.2), LIT, tf(-3 - i * 5.5, 1 + (H - 1) / 2, sz * (L / 2 + 1.2)));
  // Pórtico central mais alto com a cúpula baixa (acesa).
  k.light(box(D + 2, 4, 26), LIT, tf(-D / 2 + 1, H + 3.6, 0));
  k.light(sphere(10, 12, 4, 0, PI * 2, 0, PI / 2), '#f4ecd8', tf(-D / 2, H + 5.6, 0, 1, 0.5, 1));
  k.blink(sphere(0.4, 6, 4), '#ff3a2a', tf(-D / 2, H + 10.8, 0));
  // Escadaria, espelho d'água e mastros.
  k.add(box(10, 0.6, 30), '#e8e4d8', tf(6, 0.9, 0)).add(box(6, 0.6, 30), '#e8e4d8', tf(4, 1.4, 0));
  for (const sz of [-1, 1]) k.light(box(16, 0.4, 30), '#2a5a80', tf(16, 0.9, sz * 26));
  const flags: Array<[string, string]> = [['#2a9a4a', '#ffd23f'], ['#2a6ab8', '#ffd23f'], ['#f4f2ea', '#2a6ab8']];
  flags.forEach(([a, b], i) => {
    const z = -10 + i * 10;
    k.add(cyl(0.12, 0.15, 16, 5), '#d8d8d8', tf(22, 8.5, z));
    k.add(box(0.06, 1.8, 3), a, tf(22, 15.4, z + 1.6)).add(box(0.07, 0.8, 3), b, tf(22, 15.4, z + 1.6));
  });
  for (const z of [-40, -20, 20, 40]) lamp(k, 24, z, 6);
  for (let i = 0; i < 6; i++) palm(k, 26, -46 + i * 18.4, 10 + hash2(641, i) * 3, 6410 + i, 0, 6);
  return k.model(H + 11);
}

/**
 * Ponte de Palmas (a Ponte FHC sobre o lago do Tocantins): o tabuleiro baixo e comprido sobre pilares duplos, que de
 * noite é uma fileira de luzes riscando o lago escuro — o que se reconhece da orla. Postes altos de braço com a
 * luminária grande (exagerada para ler a 200 m+), o guarda-corpo aceso correndo a ponte inteira, o reflexo de cada
 * poste na água e o pórtico da cabeceira (o portal da cidade) contornado de luz.
 */
function pontePalmas(): Model {
  const k = new Kit();
  const L = 640; const deckY = 9; const W = 16; const LAMP = '#ffe0a0';
  k.add(box(70, 0.3, L + 80), '#2f6a90', tf(-8, 0.15, 0));
  k.add(box(W, 1.8, L), '#d8d8d0', tf(0, deckY - 0.9, 0));
  k.add(box(W - 2, 0.05, L), '#4a4e55', tf(0, deckY + 0.03, 0));
  // Guarda-corpo aceso dos dois lados (a linha de luz contínua).
  for (const sx of [-1, 1]) k.light(box(0.4, 1.0, L), sx > 0 ? '#ffd88a' : '#e8c070', tf(sx * (W / 2 - 0.2), deckY + 0.8, 0));
  for (let z = -L / 2 + 10; z < L / 2; z += 32) {
    for (const sx of [-1, 1]) k.add(box(1.8, deckY, 1.8), '#b8b8b0', tf(sx * 4.5, deckY / 2 - 1.5, z));
    k.add(box(W - 2, 1.4, 2.2), '#b8b8b0', tf(0, deckY - 2.4, z));
  }
  // Postes altos de braço, a luminária grande acesa e o reflexo dela no lago (para o lado da pista).
  for (let z = -L / 2 + 26; z < L / 2; z += 32) for (const sx of [-1, 1]) {
    k.add(box(0.4, 13, 0.4), '#8a9098', tf(sx * (W / 2 - 0.5), deckY + 6.5, z));
    k.add(box(2.6, 0.3, 0.3), '#8a9098', tf(sx * (W / 2 - 1.6), deckY + 13, z));
    k.light(box(1.4, 1.1, 2.2), LAMP, tf(sx * (W / 2 - 2.6), deckY + 12.6, z));
    if (sx > 0) k.light(box(18, 0.06, 1.3), '#9a7a40', tf(W / 2 + 10, 0.36, z));
  }
  // Cabeceira com aterro e o pórtico (portal da cidade), contornado de luz.
  for (const sz of [-1, 1]) k.add(box(W + 6, deckY + 1, 30), '#b8b0a0', tf(0, (deckY - 1) / 2, sz * (L / 2 + 15)));
  const pz = -L / 2 + 4;
  for (const sx of [-1, 1]) { k.add(box(1.8, 16, 1.8), '#f4f2ea', tf(sx * (W / 2 + 1), deckY + 8, pz)); k.light(box(0.5, 16, 0.5), LAMP, tf(sx * (W / 2 + 1) + 1.1, deckY + 8, pz)); }
  k.add(box(W + 4, 1.8, 1.8), '#f4f2ea', tf(0, deckY + 16, pz));
  k.light(box(W + 4, 0.6, 0.5), LAMP, tf(1.1, deckY + 15.4, pz));
  return k.model(deckY + 17);
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
