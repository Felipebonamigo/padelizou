// Marcos turísticos do Norte e do Nordeste (onda G, docs/PISTAS-TURISMO.md). Um construtor por id de
// src/core/data/places.ts. Convenção dos modelos daqui: centro em x = z = 0, base em y = 0 (fundações e
// pilares entram até 2 m no chão: terreno inclinado não deixa nada flutuando), a frente olha +X (a pista)
// e o que é comprido (ponte, galpões, paredão, serra) corre ao longo de Z, paralelo à pista.
// Escala real, low-poly de faces planas e cor chapada; janelas e luzes acendem à noite (fachada texturizada,
// `glow`) e as luzes de balizamento piscam (`beacon`). Sem Math.random: variação por hash.
import * as THREE from 'three';
import { hash2, hash3 } from '../../noise';
import { box, cone, cyl, frond, gable, hip, ico, jitter, merge, paint, shadeY, speckle, sphere, tf, tintUp, tris, type Geo, type MatKey, type Model, type ModelPart } from '../geom';
import { FACADE_TILE } from '../structures';
import type { LandmarkDef, LandmarkRegistry } from './types';

// ───────────────────────────── Kit ─────────────────────────────

type FacadeStyle = keyof typeof FACADE_TILE;
type V3 = [number, number, number];
const UP = new THREE.Vector3(0, 1, 0);
const PI = Math.PI;

/** Partes de um marco, separadas por material. */
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

  /** Monta o Model: sombreado por altura no `flat`; sombra projetada só para os de perto. */
  model(height: number, shadow: boolean, shade: [number, number] = [0.8, 1.06]): Model {
    const parts: ModelPart[] = [];
    if (this.flat.length) parts.push({ geometry: shadeY(merge(this.flat), 0, height, shade[0], shade[1]), mat: 'flat', shadow });
    for (const [mat, list] of this.facade) parts.push({ geometry: merge(list), mat, shadow });
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

/** Perfil 2D (x, y) extrudado em +Z de 0 a `depth` (com furos opcionais). `steps` divide a extrusão. */
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

/** Recolore cada triângulo pela posição do centro (listras, azulejos, estratos). */
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

/** Triângulo de dois lados (vela, bandeira, pintura): a malha não depende do lado da câmera. */
function sheet(a: V3, b: V3, c: V3): Geo {
  return tris([...a, ...b, ...c, ...a, ...c, ...b]);
}

/** Polígono "estrela" de n pontas: raio alterna R e r, com desvio por hash. */
function blobPoly(n: number, rx: number, rz: number, amount: number, seed: number): Array<[number, number]> {
  const out: Array<[number, number]> = [];
  for (let i = 0; i < n; i++) {
    const a = (i / n) * PI * 2;
    const f = 1 + (hash2(seed, i) - 0.5) * 2 * amount;
    out.push([Math.cos(a) * rx * f, Math.sin(a) * rz * f]);
  }
  return out;
}

// ───────────────────────────── Peças reaproveitadas ─────────────────────────────

/** Coqueiro alto (12–22 m): tronco anelado e inclinado, folhas caídas, cocos. */
function palm(k: Kit, x: number, z: number, H: number, seed: number, y0 = 0): void {
  const dir = hash2(seed, 2) * PI * 2;
  const leanM = H * (0.06 + hash2(seed, 3) * 0.14);
  const cd = Math.cos(dir); const sd = Math.sin(dir);
  const pieces = 4;
  let prev: V3 = [x, y0 - 0.5, z];
  for (let q = 1; q <= pieces; q++) {
    const t = q / pieces;
    const o = leanM * t * t;
    const next: V3 = [x + o * cd, y0 + t * H, z + o * sd];
    k.rod(prev, next, 0.34 - 0.1 * t, q % 2 ? '#8a6a3f' : '#7a5c36', 5);
    prev = next;
  }
  const [tx, ty, tz] = prev;
  const n = 8;
  const s = H / 16;
  for (let q = 0; q < n; q++) {
    const a = (q / n) * PI * 2 + hash2(seed, q) * 0.5;
    k.add(frond(4.6 * s + hash2(seed, q + 20) * s, 1.3 * s, a, 1.0 * s, 3.4 * s, 3), q % 2 ? '#3f9a3a' : '#52ae44', tf(tx, ty, tz));
  }
  for (let q = 0; q < 3; q++) k.add(new THREE.OctahedronGeometry(0.32 * s, 0), '#6b4a22', tf(tx + Math.cos(q * 2.1) * 0.4 * s, ty - 0.5 * s, tz + Math.sin(q * 2.1) * 0.4 * s));
}

/** Torre de farol cilíndrica com faixas, varanda, lanterna (acesa) e cúpula; luz de balizamento no topo. */
function lightTower(k: Kit, x: number, z: number, y0: number, H: number, r0: number, r1: number, colors: string[], bands: number, cap: string): number {
  for (let q = 0; q < bands; q++) {
    const t0 = q / bands; const t1 = (q + 1) / bands;
    k.add(cyl(r0 + (r1 - r0) * t1, r0 + (r1 - r0) * t0, H / bands, 12), colors[q % colors.length], tf(x, y0 + ((t0 + t1) / 2) * H, z));
  }
  const top = y0 + H;
  k.add(cyl(r1 + 0.9, r1 + 0.6, 0.5, 12), '#2e3238', tf(x, top + 0.25, z));
  k.add(cyl(r1 + 0.9, r1 + 0.9, 0.9, 12, true), '#2e3238', tf(x, top + 0.95, z));
  k.add(cyl(r1 * 0.7, r1 * 0.7, 0.4, 10), '#2e3238', tf(x, top + 0.7, z));
  k.light(cyl(r1 * 0.65, r1 * 0.65, 2.2, 10), '#fff1b8', tf(x, top + 2.0, z));
  k.add(cone(r1 * 0.9, 1.8, 10), cap, tf(x, top + 4.0, z));
  k.blink(sphere(0.3, 6, 4), '#ff3a2a', tf(x, top + 5.1, z));
  return top + 5.4;
}

/**
 * Igreja colonial com a fachada em +X na posição (x, y, z): nave com telhado de duas águas, frontão,
 * uma ou duas torres com cobertura piramidal ou de bulbo, porta e janelas. `s` = escala.
 */
function church(k: Kit, x: number, y: number, z: number, o: { wall: string; trim: string; towers: 1 | 2; cap: 'pyramid' | 'bulb'; s?: number; lit?: boolean; roof?: string }): void {
  const s = o.s ?? 1;
  const D = 22 * s; const W = 12 * s; const H = 12 * s;
  k.add(box(D, H + 2, W), o.wall, tf(x - D / 2, y + H / 2 - 1, z));
  k.add(gable(W, 4.2 * s, D, 0.4 * s), o.roof ?? '#b8552e', tf(x - D / 2, y + H, z, 1, 1, 1, 0, PI / 2, 0));
  // Frontispício: parede da frente mais alta, frontão triangular, cornija e pilastras.
  k.add(box(1.2 * s, H + 3 * s, W + 0.6 * s), o.wall, tf(x - 0.4 * s, y + (H + 3 * s) / 2 - 1, z));
  k.add(gable(W + 0.6 * s, 4.6 * s, 1.2 * s), o.wall, tf(x - 0.4 * s, y + H + 2 * s, z, 1, 1, 1, 0, PI / 2, 0));
  k.add(box(1.5 * s, 0.6 * s, W + 1.2 * s), o.trim, tf(x - 0.3 * s, y + H + 1.8 * s, z));
  for (const dz of [-1, 1]) k.add(box(1.4 * s, H + 2 * s, 0.8 * s), o.trim, tf(x - 0.3 * s, y + (H + 2 * s) / 2 - 1, z + dz * (W / 2 + 0.1)));
  k.add(box(0.2, 0.3 * s + 2.6 * s, 0.35 * s), o.trim, tf(x - 0.3 * s, y + H + 7.6 * s, z));
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

/** Ponte estaiada ao longo de Z: tabuleiro, pilares, duas torres (H, A ou mastro único), leque de estais, luzes. */
function cableBridge(k: Kit, o: { L: number; deckY: number; W: number; span: number; pylonH: number; type: 'H' | 'A'; deck: string; pylon: string; cable: string; pierStep: number; lamp: string }): void {
  const { L, deckY, W, span, pylonH } = o;
  // Tabuleiro e guarda-corpo.
  k.add(box(W, 2.4, L), o.deck, tf(0, deckY - 1.2, 0));
  k.add(box(W - 1.6, 0.06, L), '#4a4e55', tf(0, deckY + 0.03, 0));
  for (const sx of [-1, 1]) k.add(box(0.4, 1.1, L), '#e8ecef', tf(sx * (W / 2 - 0.2), deckY + 0.55, 0));
  // Rampas de acesso nas pontas (descem até o chão).
  for (const sz of [-1, 1]) {
    const rampL = deckY * 3.5;
    const a: V3 = [0, deckY - 1.2, sz * L / 2]; const b: V3 = [0, -0.5, sz * (L / 2 + rampL)];
    const len = Math.hypot(rampL, deckY);
    k.add(box(W, 2.4, len), o.deck, tf(0, (a[1] + b[1]) / 2, (a[2] + b[2]) / 2, 1, 1, 1, Math.atan2(deckY - 0.7, rampL) * sz, 0, 0));
    for (let q = 1; q < 4; q++) {
      const t = q / 4; const zz = sz * (L / 2 + rampL * t); const yy = (deckY - 2.4) * (1 - t);
      if (yy > 2) k.add(box(W * 0.5, yy + 2, 2.2), '#c9ccd0', tf(0, yy / 2 - 1, zz));
    }
  }
  // Pilares do vão de acesso (o vão central fica livre).
  for (let zz = -L / 2 + o.pierStep / 2; zz < L / 2; zz += o.pierStep) {
    if (Math.abs(zz) < span / 2 + 4) continue;
    k.add(box(W * 0.45, deckY - 2.4 + 2, 3), '#c9ccd0', tf(0, (deckY - 2.4) / 2 - 1, zz));
    k.add(box(W * 0.9, 1.4, 3.6), '#c9ccd0', tf(0, deckY - 3.1, zz));
  }
  // Torres e estais.
  const nCables = 7;
  for (const sz of [-1, 1]) {
    const zp = sz * span / 2;
    const leg = W / 2 + 1.4;
    if (o.type === 'H') {
      for (const sx of [-1, 1]) k.beam([sx * leg, -2, zp], [sx * (leg - 0.6), pylonH, zp], 2.6, o.pylon, 3.4);
      k.add(box(leg * 2, 2.4, 3), o.pylon, tf(0, deckY - 3.4, zp));
      k.add(box(leg * 2, 2.0, 2.6), o.pylon, tf(0, pylonH - 5, zp));
    } else {
      const foot = leg + 4;
      for (const sx of [-1, 1]) {
        k.beam([sx * foot, -2, zp], [sx * 1.2, pylonH * 0.82, zp], 2.8, o.pylon, 3.6);
      }
      k.add(box(3.2, pylonH * 0.2 + 1, 3.6), o.pylon, tf(0, pylonH * 0.9, zp));
      k.add(box(foot * 2 - 2, 2.4, 3), o.pylon, tf(0, deckY - 3.4, zp));
    }
    k.blink(sphere(0.6, 6, 4), '#ff3a2a', tf(0, pylonH + 0.8, zp));
    for (const sx of [-1, 1]) {
      const topX = o.type === 'H' ? sx * (leg - 0.6) : sx * 0.6;
      for (let c = 0; c < nCables; c++) {
        const yTop = pylonH - 3 - c * (pylonH - deckY) * 0.045;
        const reach = (span / 2 - 6) * ((c + 1) / nCables);
        for (const dir of [-1, 1]) k.beam([topX, yTop, zp], [sx * (W / 2 - 0.6), deckY + 0.4, zp + dir * reach], 0.22, o.cable);
      }
    }
  }
  // Luminárias ao longo do tabuleiro.
  for (let zz = -L / 2 + 10; zz < L / 2; zz += 24) {
    for (const sx of [-1, 1]) {
      k.add(box(0.2, 6, 0.2), '#9aa0a8', tf(sx * (W / 2 - 0.4), deckY + 3, zz));
      k.light(box(0.7, 0.25, 0.5), o.lamp, tf(sx * (W / 2 - 1.0), deckY + 6, zz));
    }
  }
}

/**
 * Fortaleza abaluartada (estrela de baluartes em ponta de lança): muralha com pátio, parapeito de cor
 * própria, guaritas nas pontas. Raio `R` até a ponta dos baluartes.
 */
function bastionFort(k: Kit, R: number, n: number, H: number, wall: string, top: string, court: string, rot = 0): Array<[number, number]> {
  const pts: Array<[number, number]> = [];
  const tips: Array<[number, number]> = [];
  for (let i = 0; i < n; i++) {
    const a = rot + (i / n) * PI * 2;
    const P = (r: number, da: number): [number, number] => [Math.cos(a + da) * R * r, Math.sin(a + da) * R * r];
    pts.push(P(0.6, -0.2), P(0.8, -0.34), P(1, 0), P(0.8, 0.34), P(0.6, 0.2));
    tips.push(P(0.97, 0));
  }
  const inner = pts.map(([x, z]): [number, number] => [x * 0.8, z * 0.8]);
  k.add(plan(pts, H + 2, [inner]), wall, tf(0, -2, 0));
  k.add(plan(pts.map(([x, z]): [number, number] => [x * 1.01, z * 1.01]), 0.7, [pts.map(([x, z]): [number, number] => [x * 0.95, z * 0.95])]), top, tf(0, H, 0));
  k.add(plan(inner, 0.5), court, tf(0, 0, 0));
  // Base inclinada (escarpa) da muralha: lê como fortaleza mesmo de longe.
  k.add(plan(pts.map(([x, z]): [number, number] => [x * 1.05, z * 1.05]), 1.8, [pts.map(([x, z]): [number, number] => [x * 0.9, z * 0.9])]), top, tf(0, -0.5, 0));
  for (const [x, z] of tips) {
    k.add(cyl(1.1, 1.1, 2.4, 6), top, tf(x, H + 1.2, z));
    k.add(cone(1.4, 1.4, 6), top, tf(x, H + 3.1, z));
  }
  return inner;
}

/** Duna: meia elipse com a face de escorregamento (−X) mais curta e íngreme, ondulada por hash. */
function dune(k: Kit, x: number, z: number, rx: number, h: number, rz: number, color: string, crest: string, seed: number, yaw = 0): void {
  const g = sphere(1, 16, 6, 0, PI * 2, 0, PI / 2);
  const p = g.attributes.position as THREE.BufferAttribute;
  for (let i = 0; i < p.count; i++) {
    const vx = p.getX(i); const vy = p.getY(i);
    // Crista deslocada para trás e face de trás curta: perfil de barcana.
    p.setXYZ(i, (vx < 0 ? vx * 0.5 : vx) - vy * 0.25, Math.pow(vy, 1.3), p.getZ(i));
  }
  jitter(g, 0.05, seed, true);
  const pg = paint(g, color, tf(x, 0, z, rx, h, rz, 0, yaw, 0));
  k.raw(speckle(tintUp(pg, crest, 0.9, 0.6), 0.05, seed));
}

/** Lagoa: disco raso irregular (água de cor chapada). */
function pond(k: Kit, x: number, z: number, rx: number, rz: number, color: string, seed: number): void {
  const g = jitter(cyl(1, 1, 1, 10), 0.18, seed);
  k.add(g, color, tf(x, 0.05, z, rx, 0.5, rz));
}

/** Mesa de rocha (tepui, serra): talude de mata/areia e paredão vertical em estratos, topo irregular. */
function mesa(k: Kit, o: { rx: number; rz: number; H: number; talus: number; seed: number; n: number; rock: string[]; top: string; slope: string; step: number }): Array<[number, number]> {
  const poly = blobPoly(o.n, o.rx, o.rz, 0.12, o.seed);
  const cliff = plan(poly, o.H - o.talus * 0.5, [], Math.max(2, Math.round((o.H - o.talus * 0.5) / o.step)));
  jitter(cliff, 0.012, o.seed);
  const cp = paint(cliff, o.rock[0], tf(0, o.talus * 0.5, 0));
  strata(cp, o.rock, o.step, o.seed);
  k.raw(tintUp(cp, o.top, 0.7, 1));
  const tal = jitter(cyl(0.98, 1.3, 1, o.n, true), 0.08, o.seed + 1, true);
  k.raw(speckle(paint(tal, o.slope, tf(0, o.talus / 2 - 1, 0, o.rx, o.talus + 2, o.rz)), 0.06, o.seed));
  return poly;
}

/**
 * Paredão de arenito em camadas ao longo de Z (face em +X): uma caixa por estrato, cada uma com a sua
 * meia largura `half(y)`, um ressalto por hash e, se `hole(y)` der uma meia largura, o vão no meio.
 */
function layeredWall(k: Kit, o: { y0: number; step: number; n: number; half: (y: number) => number; hole?: (y: number) => number; depth: number; x: number; colors: string[]; seed: number; ledge?: number; shift?: (y: number) => number }): void {
  for (let i = 0; i < o.n; i++) {
    const ya = o.y0 + i * o.step; const ym = ya + o.step / 2;
    const hw = o.half(ym);
    if (hw <= 0.5) continue;
    const ledge = o.ledge ?? 0.25;
    const dx = (hash2(o.seed, i) - 0.5) * o.depth * ledge + (o.shift ? o.shift(ym) : 0);
    const d = o.depth * (0.88 + hash2(o.seed, i + 50) * 0.24);
    const hz = o.hole ? o.hole(ym) : 0;
    const segs: Array<[number, number]> = hz > 0 ? [[-hw, -hz], [hz, hw]] : [[-hw, hw]];
    const color = o.colors[Math.floor(hash2(o.seed, i + 90) * o.colors.length)];
    for (const [za, zb] of segs) {
      if (zb - za < 0.8) continue;
      const dz = (hash2(o.seed, i + 130) - 0.5) * 2;
      k.raw(paint(jitter(box(d, o.step + 0.04, zb - za + Math.abs(dz)), 0.012, o.seed + i), color, tf(o.x - d / 2 + dx, ym, (za + zb) / 2 + dz * 0.5)));
    }
  }
}

/** Nuvem baixa de poucos triângulos (icosaedros achatados). */
function cloud(k: Kit, x: number, y: number, z: number, s: number, seed: number): void {
  for (let q = 0; q < 4; q++) {
    const dz = (q - 1.5) * s * 0.9; const r = s * (0.6 + hash2(seed, q) * 0.5);
    k.add(jitter(ico(1, 0), 0.15, seed + q), '#f4f6fa', tf(x, y + hash2(seed, q + 9) * s * 0.2, z + dz, r * 1.2, r * 0.55, r));
  }
}

const HOUSE_COLORS = ['#f2a8b8', '#ffd23f', '#5aa8e0', '#7ac87a', '#f08a4a', '#b89ae6', '#f6f2e6', '#e85a5a', '#46c1b8'];

// ───────────────────────────── Bahia (Salvador) ─────────────────────────────

/** Forte de Santo Antônio da Barra com o farol de faixas pretas e brancas. ~60 m, 34 m de altura. */
function farolDaBarra(): Model {
  const k = new Kit();
  // Promontório de pedra e grama.
  const rock = jitter(sphere(1, 10, 3, 0, PI * 2, 0, PI / 2), 0.12, 11, true);
  k.raw(tintUp(paint(rock, '#7a6e62', tf(0, -1, 0, 40, 5, 34)), '#6f9a4a', 0.75, 1));
  // Forte poligonal branco sobre o promontório.
  const pts: Array<[number, number]> = [];
  for (let i = 0; i < 10; i++) { const a = (i / 10) * PI * 2; const r = i % 2 ? 24 : 27; pts.push([Math.cos(a) * r, Math.sin(a) * r * 0.85]); }
  const inner = pts.map(([x, z]): [number, number] => [x * 0.86, z * 0.86]);
  k.add(plan(pts, 9, [inner]), '#ece6d6', tf(0, 0, 0));
  k.add(plan(pts.map(([x, z]): [number, number] => [x * 1.06, z * 1.06]), 3.4, [inner]), '#c9b89a', tf(0, 0, 0));
  k.add(plan(pts.map(([x, z]): [number, number] => [x * 1.01, z * 1.01]), 0.6, [pts.map(([x, z]): [number, number] => [x * 0.95, z * 0.95])]), '#d8cfb8', tf(0, 9, 0));
  k.add(plan(inner, 0.4), '#8fae5a', tf(0, 7.6, 0));
  // Casa do faroleiro e capelinha dentro do forte.
  k.add(box(9, 4, 12), '#f4f0e6', tf(-6, 9.6, 6));
  k.add(gable(9, 2.4, 12, 0.3), '#b8552e', tf(-6, 11.6, 6));
  k.add(box(6, 5, 6), '#f4f0e6', tf(-8, 10, -9));
  k.add(hip(6.4, 3, 6.4), '#b8552e', tf(-8, 12.5, -9));
  // Farol: torre de faixas pretas e brancas sobre base quadrada.
  k.add(box(8, 3, 8), '#ece6d6', tf(4, 9, -2));
  const top = lightTower(k, 4, -2, 10.5, 21, 3.1, 2.3, ['#f4f4ee', '#24262b'], 6, '#24262b');
  k.light(box(0.25, 0.9, 0.9), '#ffe2a6', tf(24.5, 4, 3)).light(box(0.25, 0.9, 0.9), '#ffe2a6', tf(24.5, 4, -6));
  return k.model(top, false);
}

/** Elevador Lacerda: torre art déco de 72 m colada na escarpa, com a passarela para a Cidade Alta. */
function elevadorLacerda(): Model {
  const k = new Kit();
  // Escarpa (perfil extrudado ao longo de Z) com mato e pedra.
  const prof: Array<[number, number]> = [[-6, -2], [-6, 0], [-8, 22], [-11, 44], [-13, 60], [-15, 63], [-70, 66], [-70, -2]];
  const cliff = paint(jitter(extrude(prof, 170, [], 1), 0.02, 21), '#7a8a52', tf(0, 0, -85));
  k.raw(speckle(tintUp(cliff, '#5f9a44', 0.55, 1), 0.08, 21));
  // Cidade Alta: casario no alto da escarpa.
  for (let i = 0; i < 9; i++) {
    const z = -70 + i * 17 + hash2(21, i) * 4;
    if (Math.abs(z) < 16) continue;
    const h = 7 + hash2(22, i) * 9;
    k.fac('classic', 12, h, 13, HOUSE_COLORS[i % HOUSE_COLORS.length], tf(-24 - hash2(23, i) * 10, 63 + h / 2, z));
    k.add(hip(12.6, 2.4, 13.6), '#b8552e', tf(-24 - hash2(23, i) * 10, 63 + h, z));
  }
  // Prédio de baixo (Cidade Baixa).
  k.fac('classic', 14, 13, 30, '#efe4c8', tf(4, 6.5, 0));
  k.add(box(14.6, 0.8, 30.6), '#d8c9a6', tf(4, 13.3, 0));
  // Torre: dois poços colados, frisos verticais art déco.
  const T0 = 13; const T1 = 70;
  k.fac('classic', 9, T1 - T0, 15, '#f1e8d2', tf(0, (T0 + T1) / 2, 0));
  for (const dz of [-7.6, -2.6, 2.6, 7.6]) k.add(box(0.8, T1 - T0, 0.7), '#e2d4b2', tf(4.7, (T0 + T1) / 2, dz));
  for (const y of [28, 42, 56]) k.add(box(9.6, 0.6, 15.6), '#e2d4b2', tf(0, y, 0));
  k.light(box(0.3, T1 - T0 - 6, 0.5), '#ffd890', tf(5.05, (T0 + T1) / 2, 0));
  // Cabine do alto (mais larga), coroamento escalonado e passarela até a escarpa.
  k.fac('classic', 13, 9, 19, '#f1e8d2', tf(-1, T1 + 4.5, 0));
  k.add(box(13.6, 0.7, 19.6), '#e2d4b2', tf(-1, T1 + 9.2, 0));
  k.add(box(9, 2.4, 13), '#f1e8d2', tf(-1, T1 + 10.6, 0));
  k.add(box(5, 2.0, 7), '#e2d4b2', tf(-1, T1 + 12.8, 0));
  k.fac('classic', 14, 5, 8, '#efe4c8', tf(-13, 64.5, 0));
  k.add(box(14.4, 0.6, 8.4), '#d8c9a6', tf(-13, 67.3, 0));
  k.light(box(0.2, 1.2, 12), '#ffe2a6', tf(5.6, T1 + 7.6, 0));
  return k.model(T1 + 14, false);
}

/** Casario do Pelourinho: sobrados coloridos subindo a ladeira e a igreja azul do Rosário no alto. */
function casarioPelourinho(): Model {
  const k = new Kit();
  const n = 8; const W = 7.2;
  for (let i = 0; i < n; i++) {
    const z = 24 - i * W;
    const y0 = i * 1.1;
    const floors = 2 + (hash2(31, i) > 0.55 ? 1 : 0);
    const h = floors * 3.8;
    const color = HOUSE_COLORS[(i * 4 + 1) % HOUSE_COLORS.length];
    k.add(box(11, y0 + 2, W), '#8f8678', tf(-5.5, y0 / 2 - 1, z));
    k.fac('classic', 11, h, W - 0.1, color, tf(-5.5, y0 + h / 2, z));
    k.add(box(0.5, 0.4, W), '#f6f2e6', tf(0.1, y0 + h - 0.2, z));
    k.add(box(0.5, 0.35, W), '#f6f2e6', tf(0.1, y0 + 3.8, z));
    for (const dz of [-1, 1]) k.add(box(0.4, h, 0.35), '#f6f2e6', tf(0.05, y0 + h / 2, z + dz * (W / 2 - 0.2)));
    k.add(gable(11.6, 2.6, W + 0.2, 0.2), '#b8552e', tf(-5.5, y0 + h, z));
    k.add(box(0.3, 2.6, 1.3), '#4a2e22', tf(0.12, y0 + 1.3, z + 1.5));
    if (floors === 3) k.add(box(1.2, 0.18, 2.6), '#3a3a3a', tf(0.6, y0 + 7.6, z - 1));
  }
  // Calçamento da ladeira (pé-de-moleque).
  const slope = n * 1.1;
  k.add(box(6, 0.5, n * W + 4), '#a39886', tf(3.2, slope / 2 - 0.6, 24 - (n - 1) * W / 2, 1, 1, 1, Math.atan2(slope, n * W), 0, 0));
  church(k, 0, n * 1.1, 24 - n * W - 7, { wall: '#3d7cc9', trim: '#f4f2ea', towers: 2, cap: 'pyramid', s: 0.9, lit: true });
  k.add(box(14, n * 1.1 + 2, 26), '#8f8678', tf(-9, n * 0.55 - 1, 24 - n * W - 7));
  k.light(box(0.2, 0.8, 0.6), '#ffd890', tf(0.4, 3.2, 20)).light(box(0.2, 0.8, 0.6), '#ffd890', tf(0.4, 6.6, -3));
  return k.model(40, true);
}

// ───────────────────────────── Sergipe (Aracaju) ─────────────────────────────

/** Arcos da Orla de Atalaia: três arcos brancos sobre a praça, com luz colorida que acende à noite. */
function arcosAtalaia(): Model {
  const k = new Kit();
  k.add(box(30, 0.3, 46), '#d8cfbd', tf(-4, 0.15, 0));
  const arches: Array<[number, number, number, string]> = [[0, 17, 16, '#4fc3f7'], [-7, 21, 19, '#ffd23f'], [-14, 17, 16, '#ff7ab6']];
  for (const [x, H, S, led] of arches) {
    const N = 12;
    const pt = (t: number): V3 => { const zz = -S + 2 * S * t; return [x, H * (1 - (zz / S) ** 2), zz]; };
    for (let q = 0; q < N; q++) {
      const a = pt(q / N); const b = pt((q + 1) / N);
      k.beam(a, b, 1.3, '#f4f4f0', 1.6);
      const inA: V3 = [a[0] + 0.85, a[1] - 0.25, a[2]]; const inB: V3 = [b[0] + 0.85, b[1] - 0.25, b[2]];
      k.glow.push(paint(box(0.25, 1, 0.5), led, segMatrix(inA, inB)));
    }
    for (const sz of [-1, 1]) k.add(box(2.6, 1.2, 2.6), '#c9c2b2', tf(x, 0.6, sz * S));
  }
  // Coqueiros e postes da orla.
  for (let i = 0; i < 4; i++) palm(k, 8 + (i % 2) * 3, -21 + i * 14, 11 + hash2(41, i) * 4, 410 + i);
  for (const z of [-20, 20]) { k.add(cyl(0.12, 0.16, 7, 6), '#5d636b', tf(6, 3.5, z)); k.light(sphere(0.45, 6, 4), '#fff1c8', tf(6, 7.2, z)); }
  return k.model(22, true);
}

/** Ponte estaiada Aracaju–Barra dos Coqueiros: torres em H, tabuleiro a 22 m. */
function ponteAracaju(): Model {
  const k = new Kit();
  cableBridge(k, { L: 520, deckY: 22, W: 16, span: 190, pylonH: 72, type: 'H', deck: '#e4e6e8', pylon: '#f2f2ee', cable: '#dfe3e8', pierStep: 40, lamp: '#ffe2a6' });
  return k.model(74, false);
}

// ───────────────────────────── Alagoas (Maragogi) ─────────────────────────────

/** Uma jangada: cinco paus roliços, banco, mastro e a vela triangular. Comprimento ao longo de Z. */
function oneJangada(k: Kit, x: number, z: number, yaw: number, sail: string, stripe: string, seed: number): void {
  const c = Math.cos(yaw); const s = Math.sin(yaw);
  const P = (lx: number, y: number, lz: number): V3 => [x + lx * c + lz * s, y, z - lx * s + lz * c];
  for (let q = 0; q < 5; q++) {
    const lx = (q - 2) * 0.48;
    k.rod(P(lx, 0.25, -3.6), P(lx, 0.25, 3.6), 0.26, q % 2 ? '#b08a5a' : '#9a7448', 6);
  }
  k.beam(P(0, 0.55, 2.6), P(0, 0.55, -3.8), 2.6, '#c9a26a', 0.12);
  k.beam(P(-0.9, 0.9, 0.4), P(0.9, 0.9, 0.4), 0.4, '#8a6a42', 0.6);
  const mastTop = P(0.2, 9.5, 1.2);
  k.rod(P(0, 0.4, 1.4), mastTop, 0.12, '#6a4a2a', 5);
  const boomEnd = P(0, 1.4, -5.2);
  k.rod(P(0, 1.3, 1.3), boomEnd, 0.08, '#6a4a2a', 4);
  const foot = P(0.05, 1.5, 1.2);
  k.add(sheet(foot, mastTop, boomEnd), sail);
  // Faixa colorida na vela (paralela à testa).
  const mid = (a: V3, b: V3, t: number): V3 => [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t];
  const off = (v: V3, d: number): V3 => [v[0] + c * d, v[1], v[2] - s * d];
  for (const d of [0.04, -0.04]) k.add(sheet(off(mid(foot, boomEnd, 0.32), d), off(mid(mastTop, boomEnd, 0.32), d), off(mid(mid(foot, mastTop, 0.5), boomEnd, 0.55), d)), stripe);
  if (hash2(seed, 1) > 0.4) k.add(box(0.5, 0.5, 0.5), '#e8e2d0', tf(P(0.4, 0.8, -2)[0], 0.8, P(0.4, 0.8, -2)[2]));
}

/** Duas jangadas lado a lado, uma de vela branca e listra vermelha, outra colorida. */
function jangada(): Model {
  const k = new Kit();
  oneJangada(k, 0, 0, 0.1, '#f6f2e8', '#d63a3a', 51);
  oneJangada(k, -6, -7, -0.25, '#ffd23f', '#1e88e5', 52);
  return k.model(10, true);
}

/** Coqueiral denso: 12 coqueiros de 12 a 22 m num bosque de 36 × 26 m. */
function coqueiral(): Model {
  const k = new Kit();
  for (let i = 0; i < 12; i++) {
    const x = (hash2(61, i) - 0.5) * 22; const z = (i / 11 - 0.5) * 34 + (hash2(62, i) - 0.5) * 4;
    palm(k, x, z, 12 + hash2(63, i) * 10, 600 + i);
  }
  for (let i = 0; i < 5; i++) k.add(jitter(ico(1, 0), 0.2, 70 + i), '#4f9a3c', tf((hash2(64, i) - 0.5) * 18, 0.5, (i - 2) * 7, 2.2, 1.1, 2.2));
  return k.model(24, true, [0.75, 1.1]);
}

// ───────────────────────────── Pernambuco (Olinda) ─────────────────────────────

/** Colina de Olinda com as igrejas brancas no alto, casario colorido nas encostas e coqueiros. */
function igrejasOlinda(): Model {
  const k = new Kit();
  const A = 95; const B = 70; const Hh = 34;
  const hill = jitter(sphere(1, 14, 5, 0, PI * 2, 0, PI / 2), 0.04, 81, true);
  k.raw(speckle(paint(hill, '#5f9a44', tf(0, -1, 0, A, Hh + 1, B)), 0.06, 81));
  const hAt = (x: number, z: number): number => { const r = 1 - (x / A) ** 2 - (z / B) ** 2; return r > 0 ? Hh * Math.sqrt(r) - 1 : -1; };
  // Igrejas: a Sé no topo (duas torres), o Carmo e São Francisco na encosta.
  church(k, 6, hAt(6, -4) - 1.2, -4, { wall: '#f6f4ee', trim: '#c9b48a', towers: 2, cap: 'pyramid', s: 1.4 });
  k.add(box(26, 6, 18), '#f6f4ee', tf(-6, hAt(6, -4) - 3, -4));
  church(k, 40, hAt(40, 30) - 1.5, 30, { wall: '#f6f4ee', trim: '#b8a07a', towers: 1, cap: 'bulb', s: 0.85 });
  k.add(box(22, 8, 14), '#f6f4ee', tf(31, hAt(40, 30) - 4, 30));
  church(k, 30, hAt(30, -40) - 1.5, -40, { wall: '#f2efe6', trim: '#d0b07a', towers: 2, cap: 'bulb', s: 0.8 });
  k.add(box(20, 8, 20), '#f2efe6', tf(22, hAt(30, -40) - 4, -40));
  // Casario: casinhas coloridas de telha, mais densas na encosta da frente.
  for (let i = 0; i < 26; i++) {
    const a = -1.25 + (i / 25) * 2.5 + (hash2(82, i) - 0.5) * 0.08;
    const r = 0.55 + hash2(83, i) * 0.38;
    const x = Math.cos(a) * A * r; const z = Math.sin(a) * B * r;
    const y = hAt(x, z);
    const w = 7 + hash2(84, i) * 3;
    k.add(box(8, 6 + 3, w), HOUSE_COLORS[i % HOUSE_COLORS.length], tf(x, y + 1.5, z, 1, 1, 1, 0, -a, 0));
    k.add(gable(8.6, 2.2, w + 0.4), '#b8552e', tf(x, y + 6, z, 1, 1, 1, 0, -a, 0));
    k.add(box(0.3, 1.2, 1.2), '#2a3442', tf(x + Math.cos(a) * 4.1, y + 3, z + Math.sin(a) * 4.1, 1, 1, 1, 0, -a, 0));
  }
  for (let i = 0; i < 7; i++) {
    const a = -1.3 + i * 0.42; const x = Math.cos(a) * A * 0.8; const z = Math.sin(a) * B * 0.8;
    palm(k, x, z, 14 + hash2(85, i) * 6, 850 + i, hAt(x, z));
  }
  return k.model(Hh + 34, false);
}

/** Um boneco gigante de Olinda (~4 m): saia comprida, cabeçorra, braços de pano. */
function boneco(k: Kit, x: number, z: number, s: number, seed: number, yaw: number): void {
  const robe = ['#d63a3a', '#1e88e5', '#ffd23f', '#3ddc84', '#8e24aa', '#ff8c1a', '#f4f4f4'];
  const skin = ['#f1c7a3', '#d9a07a', '#a86b45', '#6e4630'];
  const hair = ['#1a1410', '#4a2e1a', '#e8d070', '#f4f4f4'];
  const c1 = robe[Math.floor(hash2(seed, 1) * robe.length)];
  const c2 = robe[Math.floor(hash2(seed, 2) * robe.length)];
  const sk = skin[Math.floor(hash2(seed, 3) * skin.length)];
  const T = (lx: number, y: number, lz: number, sx = 1, sy = 1, sz = 1, rx = 0): THREE.Matrix4 => {
    const c = Math.cos(yaw); const sn = Math.sin(yaw);
    return tf(x + (lx * c + lz * sn) * s, y * s, z + (-lx * sn + lz * c) * s, sx * s, sy * s, sz * s, rx, yaw, 0);
  };
  k.add(box(0.25, 0.5, 0.2), '#2a2a2e', T(0, 0.25, -0.25)).add(box(0.25, 0.5, 0.2), '#2a2a2e', T(0, 0.25, 0.25));
  k.add(cyl(0.6, 1.05, 2.3, 8), c1, T(0, 1.65, 0));
  k.add(cyl(0.95, 0.95, 0.25, 8), c2, T(0, 1.0, 0));
  k.add(cyl(0.45, 0.6, 1.1, 8), c2, T(0, 3.35, 0));
  k.add(cyl(0.7, 0.55, 0.22, 8), '#f4f4ee', T(0, 3.95, 0));
  k.add(sphere(1.0, 8, 6), sk, T(0, 4.95, 0, 1, 1.12, 1));
  k.add(sphere(1.06, 8, 4, 0, PI * 2, 0, PI / 2), hair[Math.floor(hash2(seed, 4) * hair.length)], T(-0.1, 5.1, 0, 1, 1, 1));
  k.add(box(0.12, 0.24, 0.24), '#1a1a1e', T(0.96, 5.1, -0.34)).add(box(0.12, 0.24, 0.24), '#1a1a1e', T(0.96, 5.1, 0.34));
  k.add(box(0.12, 0.14, 0.5), '#b8282a', T(0.94, 4.5, 0));
  k.add(box(0.24, 0.3, 0.22), sk, T(1.04, 4.85, 0));
  for (const dz of [-1, 1]) {
    k.add(box(0.36, 2.1, 0.36), c2, T(0.15, 2.75, dz * 0.82, 1, 1, 1, dz * 0.28));
    k.add(box(0.3, 0.32, 0.3), sk, T(0.15, 1.65, dz * 1.12));
  }
  if (hash2(seed, 5) > 0.5) {
    k.add(cyl(1.3, 1.3, 0.1, 10), c1, T(0, 5.9, 0));
    k.add(cyl(0.7, 0.75, 0.8, 10), c1, T(0, 6.3, 0));
  }
}

/** Bonecos gigantes de Olinda (4) e dois passistas de frevo com a sombrinha colorida. */
function bonecosOlinda(): Model {
  const k = new Kit();
  for (let i = 0; i < 4; i++) boneco(k, (i % 2) * -2.5, -7.5 + i * 5, 0.95 + hash2(91, i) * 0.15, 900 + i, (hash2(92, i) - 0.5) * 0.6);
  const umb = ['#d63a3a', '#ffd23f', '#3ddc84', '#1e88e5'];
  for (const [x, z] of [[3, -4], [3.5, 5]] as const) {
    k.add(box(0.3, 0.9, 0.35), '#f4f4ee', tf(x, 0.45, z)).add(box(0.5, 0.75, 0.55), '#ff8c1a', tf(x, 1.3, z));
    k.add(sphere(0.2, 6, 4), '#a86b45', tf(x, 1.9, z));
    k.add(box(0.06, 0.8, 0.06), '#333333', tf(x, 2.1, z + 0.4));
    const u = paint(cone(0.6, 0.35, 8), '#ffffff', tf(x, 2.6, z + 0.4));
    k.raw(recolor(u, (cx, _y, cz) => umb[Math.floor(((Math.atan2(cz - z - 0.4, cx - x) + PI) / (PI * 2)) * 4) % 4]));
  }
  return k.model(6, true);
}

// ───────────────────────────── Paraíba (João Pessoa) ─────────────────────────────

/** Farol do Cabo Branco: torre triangular modernista (três aletas) no alto da falésia. */
function farolCaboBranco(): Model {
  const k = new Kit();
  // Falésia em camadas de arenito (vermelho, ocre, amarelo), mato rasteiro no topo.
  const y0 = 18;
  layeredWall(k, { y0: -2, step: 2.5, n: 8, half: (y) => 34 - y * 0.15, depth: 34, x: 10, colors: ['#c9774a', '#d89a62', '#b8603e', '#e0b080', '#c06a40'], seed: 101, ledge: 0.06, shift: (y) => -y * 0.2 });
  k.raw(tintUp(paint(jitter(box(36, 1.2, 64), 0.01, 102), '#7aa04a', tf(-11, y0 - 0.4, 0)), '#6aa04a', 0.5, 1));
  // Torre: prisma triangular branco afinando, com as três aletas escalonadas (folhas de sisal).
  const tx = -12;
  k.add(box(9, 0.8, 9), '#e4e2da', tf(tx, y0 + 0.2, 0));
  k.add(cyl(2.0, 3.0, 20, 3), '#f6f4ee', tf(tx, y0 + 10.4, 0));
  for (let q = 0; q < 3; q++) {
    const a = q * (2 * PI / 3) + PI / 3;
    for (let st = 0; st < 5; st++) {
      const h = 4; const out = 4.4 - st * 0.75;
      const r = 1.2 + out / 2;
      k.add(box(out, h - 0.5, 0.8), '#f6f4ee', tf(tx + Math.cos(a) * r, y0 + 0.4 + st * h + h / 2, -Math.sin(a) * r, 1, 1, 1, 0, a, 0));
    }
  }
  const top = y0 + 20.4;
  k.add(cyl(2.4, 2.4, 0.4, 9), '#e4e2da', tf(tx, top + 0.2, 0));
  k.add(cyl(2.4, 2.4, 0.9, 9, true), '#3a3e44', tf(tx, top + 0.85, 0));
  k.light(cyl(1.1, 1.1, 2.0, 9), '#fff1b8', tf(tx, top + 1.4, 0));
  k.add(cone(1.4, 1.3, 9), '#d63a3a', tf(tx, top + 3.05, 0));
  k.blink(sphere(0.3, 6, 4), '#ff3a2a', tf(tx, top + 3.9, 0));
  // Mirante na beira, com guarda-corpo.
  k.add(box(7, 0.4, 16), '#d8d0c0', tf(1.5, y0 + 0.1, 16));
  for (const z of [8.5, 16, 23.5]) k.add(box(0.15, 1.0, 0.15), '#e8e8e4', tf(4.9, y0 + 0.8, z));
  k.add(box(0.1, 0.1, 15), '#e8e8e4', tf(4.9, y0 + 1.3, 16));
  for (let i = 0; i < 4; i++) palm(k, -24 + i * 4, -26 + i * 15, 9 + hash2(103, i) * 4, 1030 + i, y0);
  return k.model(top + 4, false);
}

/** Estação Cabo Branco (Niemeyer): disco de vidro sobre o núcleo, rampa curva em volta e o anexo baixo. */
function estacaoCaboBranco(): Model {
  const k = new Kit();
  k.add(cyl(5, 6, 9, 12), '#f4f4f0', tf(0, 4.5, 0));
  k.add(cyl(21, 14, 3.2, 20), '#f6f6f2', tf(0, 10.6, 0));
  k.add(cyl(21.4, 21.4, 4.4, 20, true), '#2c4a66', tf(0, 14.4, 0));
  k.add(cyl(22, 21.4, 1.6, 20), '#f6f6f2', tf(0, 17.4, 0));
  k.add(cyl(13, 15, 2.2, 16), '#eef0f2', tf(0, 19.2, 0));
  k.light(cyl(21.5, 21.5, 0.5, 20, true), '#bfe4ff', tf(0, 13.5, 0));
  // Rampa helicoidal do chão até o disco.
  const N = 14; const R = 26;
  let prev: V3 = [R * Math.cos(0.4), 0.2, R * Math.sin(0.4)];
  for (let q = 1; q <= N; q++) {
    const a = 0.4 + (q / N) * PI * 1.25; const y = (q / N) * 9.4;
    const next: V3 = [R * Math.cos(a), y, R * Math.sin(a)];
    k.beam(prev, next, 3.2, '#f4f4f0', 0.5);
    k.beam([prev[0], prev[1] + 1.1, prev[2]], [next[0], next[1] + 1.1, next[2]], 0.15, '#f4f4f0', 3.2);
    if (q % 3 === 0) k.add(box(0.7, y, 0.7), '#e8e8e4', tf(next[0], y / 2, next[2]));
    prev = next;
  }
  k.beam(prev, [R * 0.82 * Math.cos(0.4 + PI * 1.25), 9.4, R * 0.82 * Math.sin(0.4 + PI * 1.25)], 3.2, '#f4f4f0', 0.5);
  // Anexo baixo com cobertura curva.
  const roof: Array<[number, number]> = [];
  for (let q = 0; q <= 6; q++) { const t = q / 6; roof.push([-14 + t * 28, 5 + Math.sin(t * PI) * 2.5]); }
  roof.push([14, 0], [-14, 0]);
  k.add(extrude(roof, 18), '#f4f4f0', tf(-26, 0, -6, 1, 1, 1, 0, PI / 2, 0));
  k.add(box(0.3, 3.4, 26), '#2c4a66', tf(-7.9, 2.2, -6));
  return k.model(22, true);
}

// ───────────────────────────── Rio Grande do Norte (Natal) ─────────────────────────────

/** Forte dos Reis Magos: estrela de baluartes de pedra sobre o arrecife, com a capela e o passadiço. */
function forteReisMagos(): Model {
  const k = new Kit();
  k.add(jitter(cyl(1, 1, 1, 12), 0.15, 111), '#b8a88a', tf(0, -0.5, 0, 52, 1.6, 46));
  const inner = bastionFort(k, 40, 4, 9, '#c8b090', '#ddd0b2', '#d8c8a0', PI / 4);
  void inner;
  k.add(box(12, 5, 9), '#f6f2e8', tf(-4, 2.5, 4)).add(gable(12, 2.2, 9, 0.3), '#b8552e', tf(-4, 5, 4));
  k.add(box(4, 7, 4), '#f6f2e8', tf(-9, 3.5, 4)).add(hip(4.4, 2.4, 4.4), '#b8552e', tf(-9, 7, 4));
  k.add(box(10, 4, 14), '#e6dcc4', tf(6, 2, -8));
  k.add(cyl(0.12, 0.12, 12, 5), '#d8d8d8', tf(14, 15, 0));
  k.add(box(0.06, 1.6, 2.4), '#2a8a3a', tf(14, 20, 1.2));
  // Passadiço de madeira até a praia (+X).
  k.add(box(46, 0.6, 3.4), '#9a7a52', tf(52, 1.2, 0));
  for (let q = 0; q < 8; q++) k.add(box(0.4, 2.4, 0.4), '#7a5a3a', tf(32 + q * 6, 0, 1.5)).add(box(0.4, 2.4, 0.4), '#7a5a3a', tf(32 + q * 6, 0, -1.5));
  return k.model(22, false);
}

/** Ponte Newton Navarro: estaiada alta (tabuleiro a 55 m), torres em A brancas. */
function ponteNewtonNavarro(): Model {
  const k = new Kit();
  cableBridge(k, { L: 640, deckY: 55, W: 20, span: 212, pylonH: 128, type: 'A', deck: '#e8eaec', pylon: '#f4f4f0', cable: '#e4e8ec', pierStep: 52, lamp: '#ffe2a6' });
  return k.model(130, false);
}

// ───────────────────────────── Ceará (Jericoacoara) ─────────────────────────────

/** Pedra Furada de Jeri: arco de rocha escura na beira-mar (a abertura olha a pista). */
function pedraFuradaJeri(): Model {
  const k = new Kit();
  const outer: Array<[number, number]> = [[-12, -1], [-11.5, 6], [-10, 12], [-7, 16.5], [-2, 18.5], [3, 18], [8, 15], [11, 10], [12.5, 4], [12, -1]];
  const hole: Array<[number, number]> = [[-5, 0], [-5.2, 5], [-4, 9], [-1, 11.5], [2.5, 11], [4.5, 8], [5, 3], [5, 0]];
  const g = jitter(extrude(outer, 9, [hole], 2), 0.05, 121);
  const p = paint(g, '#8a5a3e', tf(0, 0, 0, 1, 1, 1, 0, PI / 2, 0).multiply(tf(0, 0, -4.5)));
  k.raw(speckle(strata(p, ['#8a5a3e', '#7a4a32', '#9a6a48', '#6e4430'], 2.6, 121), 0.06, 121));
  for (let i = 0; i < 6; i++) {
    k.add(jitter(ico(1, 0), 0.25, 122 + i), '#6e4a36', tf((hash2(123, i) - 0.3) * 14, 0.4, -16 + i * 6.4, 1.6 + hash2(124, i) * 1.4, 1.2, 1.8));
  }
  return k.model(19, true);
}

/** Duna do Pôr do Sol: duna grande de areia clara com mato rasteiro no pé. */
function dunaPorDoSol(): Model {
  const k = new Kit();
  dune(k, 0, 0, 80, 42, 120, '#e8c890', '#f8e8c0', 131);
  dune(k, -50, 90, 45, 18, 60, '#e2c48a', '#f2deb0', 132, 0.4);
  for (let i = 0; i < 10; i++) k.add(jitter(ico(1, 0), 0.25, 133 + i), '#7a9a4a', tf(70 + hash2(134, i) * 14, 0.6, -110 + i * 24, 3, 1.4, 3));
  return k.model(44, false, [0.7, 1.08]);
}

// ───────────────────────────── Piauí (Serra da Capivara) ─────────────────────────────

/** Pedra Furada da Serra da Capivara: o arco gigante recortado no alto do paredão de arenito. */
function pedraFuradaCapivara(): Model {
  const k = new Kit();
  // Paredão de 140 m: base larga, ombro em 36–46 m, o pico estreito com o furo (≈15 × 20 m) e a ponte de pedra no alto.
  const half = (y: number): number => (y < 36 ? 70 - y * 0.3 : y < 46 ? 59 - ((y - 36) / 10) * 41 : 18 - (y - 46) * 0.3);
  const hole = (y: number): number => (y > 39 && y < 61 ? 7.5 * Math.sqrt(Math.max(0, 1 - ((y - 50) / 11) ** 2)) + 0.6 : 0);
  layeredWall(k, { y0: -2, step: 4, n: 19, half, hole, depth: 18, x: 6, colors: ['#b86a48', '#9a5a40', '#c88a5e', '#a07060', '#d0a07a', '#8a5a48'], seed: 141, ledge: 0.1 });
  // Topo do pico arredondado e mato no ombro.
  k.add(jitter(ico(1, 0), 0.12, 145), '#a8644a', tf(-3, 74, 0, 9, 4, 11));
  for (let i = 0; i < 8; i++) k.add(jitter(ico(1, 0), 0.2, 146 + i), '#7a8a4a', tf(-4 + hash2(147, i) * 6, 36.5, (i < 4 ? -1 : 1) * (26 + (i % 4) * 9), 3, 1.6, 3));
  // Talude de blocos caídos e a caatinga no pé.
  for (let i = 0; i < 9; i++) k.add(jitter(ico(1, 0), 0.25, 142 + i), '#9a6a50', tf(10 + hash2(143, i) * 8, 1, -60 + i * 15, 5 + hash2(144, i) * 4, 3.5, 6));
  for (let i = 0; i < 12; i++) k.add(jitter(ico(1, 0), 0.2, 150 + i), i % 3 ? '#7a8a4a' : '#8a7a4a', tf(22 + hash2(151, i) * 14, 1.4, -66 + i * 12, 2.4, 1.8, 2.4));
  return k.model(78, false, [0.8, 1.06]);
}

/** Paredão com pinturas rupestres (veados, caçadores, a dança): abrigo de pedra com passarela. */
function pinturasRupestres(): Model {
  const k = new Kit();
  // Paredão (face em x = 0) com o teto do abrigo em balanço e o topo irregular.
  k.add(box(14, 13, 56), '#c89470', tf(-7, 5.5, 0));
  // Acima do abrigo, as camadas avançam em balanço sobre as pinturas.
  layeredWall(k, { y0: 12, step: 2.6, n: 6, half: (y) => 31 - (y - 12) * 0.5, depth: 16, x: 1, colors: ['#a86a4a', '#9a6044', '#b87a56', '#c08860'], seed: 161, ledge: 0.2, shift: (y) => (y < 20 ? (y - 12) * 0.5 : 4 - (y - 20) * 0.8) });
  k.raw(tintUp(paint(jitter(box(14, 2, 40), 0.05, 163), '#9a5e40', tf(-4, 28, 0)), '#7a8a4a', 0.6, 1));
  // Pinturas (vermelho-ocre) na face: figuras humanas, veados, a roda de dança.
  const red = '#b8321e';
  const X = 0.08;
  const stick = (z: number, y: number, s: number, arms: number): void => {
    k.add(box(0.1, 1.2 * s, 0.22 * s), red, tf(X, y + 1.1 * s, z));
    k.add(box(0.1, 0.36 * s, 0.36 * s), red, tf(X, y + 1.95 * s, z));
    for (const d of [-1, 1]) {
      k.add(box(0.1, 0.9 * s, 0.16 * s), red, tf(X, y + 0.4 * s, z + d * 0.18 * s, 1, 1, 1, d * 0.35, 0, 0));
      k.add(box(0.1, 0.8 * s, 0.14 * s), red, tf(X, y + 1.4 * s, z + d * 0.42 * s, 1, 1, 1, -d * arms, 0, 0));
    }
  };
  const deer = (z: number, y: number, s: number): void => {
    k.add(box(0.1, 0.5 * s, 1.6 * s), red, tf(X, y + 1.0 * s, z));
    for (const dz of [-0.6, -0.3, 0.4, 0.7]) k.add(box(0.1, 0.8 * s, 0.12 * s), red, tf(X, y + 0.45 * s, z + dz * s));
    k.add(box(0.1, 0.7 * s, 0.16 * s), red, tf(X, y + 1.5 * s, z + 0.85 * s, 1, 1, 1, 0.5, 0, 0));
    k.add(box(0.1, 0.25 * s, 0.4 * s), red, tf(X, y + 1.85 * s, z + 1.1 * s));
    for (const d of [-0.2, 0.25]) k.add(box(0.1, 0.6 * s, 0.08 * s), red, tf(X, y + 2.2 * s, z + (1.0 + d) * s, 1, 1, 1, d * 1.4, 0, 0));
  };
  for (let i = 0; i < 5; i++) stick(-20 + i * 1.6, 5.2 + (i % 2) * 0.3, 1.2, 1.1);
  deer(-8, 4.4, 1.5); deer(-3, 6.4, 1.3); deer(14, 5.2, 1.7);
  stick(4, 4.2, 1.5, 0.4); stick(6.4, 4.3, 1.5, 0.4);
  for (let i = 0; i < 6; i++) { const a = (i / 6) * PI * 2; stick(22 + Math.cos(a) * 2.6, 8 + Math.sin(a) * 1.6, 0.8, 1.3); }
  k.add(box(0.1, 0.3, 4), red, tf(X, 10.2, 10, 1, 1, 1, 0.2, 0, 0));
  // Passarela de madeira com guarda-corpo.
  k.add(box(3, 0.3, 50), '#8a6a42', tf(5, 0.9, 0));
  for (let z = -24; z <= 24; z += 4) k.add(box(0.15, 1.9, 0.15), '#6a4a2a', tf(6.4, 1.6, z));
  k.add(box(0.12, 0.12, 50), '#6a4a2a', tf(6.4, 2.4, 0));
  for (let i = 0; i < 8; i++) k.add(jitter(ico(1, 0), 0.2, 165 + i), '#7a8a4a', tf(10 + hash2(166, i) * 6, 1, -26 + i * 7.4, 1.8, 1.4, 1.8));
  return k.model(27, true);
}

// ───────────────────────────── Maranhão (Lençóis) ─────────────────────────────

/** Lençóis Maranhenses: dunas brancas em fila e lagoas azuis e verdes entre elas. */
function lagoasLencois(): Model {
  const k = new Kit();
  const dunes: Array<[number, number, number, number, number]> = [
    [-40, -120, 50, 28, 70], [10, -40, 46, 22, 60], [-60, 30, 60, 32, 80], [0, 120, 52, 26, 70], [-110, -40, 50, 30, 90], [-100, 140, 46, 24, 60],
  ];
  dunes.forEach(([x, z, rx, h, rz], i) => dune(k, x, z, rx, h, rz, '#f6f4ea', '#ffffff', 171 + i, (hash2(172, i) - 0.5) * 0.6));
  const lagoons: Array<[number, number, number, number, string]> = [
    [44, -80, 34, 44, '#1e96d6'], [50, 40, 30, 46, '#2cc0c4'], [-10, -10, 22, 24, '#1e86d0'], [36, 160, 36, 30, '#38c4b0'], [-50, 90, 24, 32, '#1e96d6'],
  ];
  lagoons.forEach(([x, z, rx, rz, c], i) => pond(k, x, z, rx, rz, c, 180 + i));
  return k.model(32, false, [0.74, 1.06]);
}

/** Farol de Mandacaru (Preguiças): torre de faixas vermelhas e brancas, casa do faroleiro. */
function farolPreguicas(): Model {
  const k = new Kit();
  k.add(box(9, 2, 9), '#f4f2ea', tf(0, 0, 0));
  const top = lightTower(k, 0, 0, 1, 34, 2.8, 1.7, ['#f6f6f2', '#d63a2a'], 8, '#d63a2a');
  k.add(box(9, 4, 6), '#f6f2e8', tf(-7, 2, 8)).add(gable(9, 2, 6, 0.3), '#d63a2a', tf(-7, 4, 8));
  k.add(box(0.1, 1, 1.2), '#2a3442', tf(-2.45, 2.2, 8));
  for (let i = 0; i < 5; i++) k.add(jitter(ico(1, 0), 0.2, 191 + i), '#6a9a44', tf(6 + hash2(192, i) * 6, 0.6, -10 + i * 5, 2, 1.2, 2));
  return k.model(top, false);
}

// ───────────────────────────── Pará (Belém) ─────────────────────────────

/** Ver-o-Peso: o Mercado de Ferro com as quatro torres azuis de ponta, e as barracas na frente. */
function verOPeso(): Model {
  const k = new Kit();
  const D = 22; const L = 48; const H = 9;
  k.fac('classic', D, H, L, '#9db8d0', tf(0, H / 2, 0));
  k.add(box(D + 0.6, 0.6, L + 0.6), '#5d7a98', tf(0, H + 0.3, 0));
  k.fac('classic', 12, 4, L - 6, '#9db8d0', tf(0, H + 2.6, 0));
  k.add(hip(13, 3, L - 5, 2, L - 12), '#6e8aa6', tf(0, H + 4.6, 0));
  k.add(hip(D + 1, 1.2, L + 1, 12.5, L - 5), '#6e8aa6', tf(0, H + 0.6, 0));
  // Torres das quinas: fuste quadrado, cobertura de ponta azul e o pináculo.
  for (const sx of [-1, 1]) for (const sz of [-1, 1]) {
    const x = sx * (D / 2); const z = sz * (L / 2);
    k.add(box(5, 15, 5), '#7fa2c4', tf(x, 7.5, z));
    k.add(box(5.6, 0.6, 5.6), '#e8eef4', tf(x, 15.2, z));
    k.add(hip(5.6, 7.5, 5.6), '#3d7cc9', tf(x, 15.5, z));
    k.add(cyl(0.08, 0.12, 2.4, 4), '#d8dde4', tf(x, 24, z));
    k.add(box(0.2, 4, 2), '#2a3442', tf(x + sx * 2.55, 9, z));
  }
  // Pórtico de entrada com arco de ferro.
  k.add(box(1, 11, 10), '#7fa2c4', tf(D / 2 + 0.2, 5.5, 0));
  k.add(box(0.4, 5, 6), '#2a3442', tf(D / 2 + 0.6, 3, 0));
  // Barracas de lona colorida na frente e luminárias.
  const tents = ['#d63a3a', '#ffd23f', '#3ddc84', '#1e88e5', '#ff8c1a', '#f4f4f4'];
  for (let i = 0; i < 6; i++) {
    const z = -20 + i * 8;
    k.add(gable(5, 1.6, 6.2, 0.2), tents[i], tf(D / 2 + 6, 2.6, z));
    for (const dz of [-2.8, 2.8]) k.add(box(0.12, 2.6, 0.12), '#5d636b', tf(D / 2 + 8.2, 1.3, z + dz));
    k.add(box(3, 1, 4.6), '#8a6a42', tf(D / 2 + 6, 0.5, z));
    k.light(box(0.4, 0.2, 0.4), '#ffe2a6', tf(D / 2 + 6, 2.4, z));
  }
  for (const z of [-26, -8, 8, 26]) k.light(box(0.2, 0.8, 0.8), '#ffe2a6', tf(D / 2 + 0.15, 6.5, z));
  return k.model(26, true);
}

/** Estação das Docas: três galpões de ferro com telhado de duas águas, guindastes antigos no cais. */
function estacaoDocas(): Model {
  const k = new Kit();
  const L = 30; const D = 20; const H = 9;
  for (let i = 0; i < 3; i++) {
    const z = (i - 1) * (L + 2.5);
    k.fac('classic', D, H, L, '#e8dcc4', tf(-D / 2, H / 2, z));
    k.add(gable(D + 0.8, 5, L + 0.4, 0.4), '#9aa4ae', tf(-D / 2, H, z));
    k.add(box(0.5, 0.8, L + 0.4), '#8a3a2a', tf(0.1, H - 0.4, z));
    k.add(box(0.4, 6, L * 0.6), '#2a3a4a', tf(0.05, 3.2, z));
    k.light(box(0.2, 5, L * 0.56), '#ffd8a0', tf(0.3, 3.2, z));
    k.add(box(3.6, 0.3, L), '#5d6878', tf(1.8, 6.4, z));
  }
  // Calçadão do cais e guindastes (amarelo-ocre).
  k.add(box(10, 0.6, 3 * L + 6), '#b8b0a2', tf(5, 0.3, 0));
  for (const z of [-26, 22]) {
    const c = '#d8a030';
    for (const dz of [-1.6, 1.6]) for (const dx of [-1.6, 1.6]) k.add(box(0.5, 7, 0.5), c, tf(7 + dx, 3.5, z + dz));
    k.add(box(4.4, 3.4, 4.4), c, tf(7, 8.7, z)).add(box(3, 0.3, 3), '#2a3442', tf(8.6, 9, z));
    k.add(gable(4.8, 1.2, 4.8, 0.1), '#8a3a2a', tf(7, 10.4, z));
    k.beam([8, 8, z], [22, 24, z + 4], 0.8, c, 1.2);
    k.beam([22, 24, z + 4], [22, 14, z + 4], 0.06, '#2a2a2a');
    k.add(box(0.6, 0.9, 0.6), '#2a2a2a', tf(22, 13.6, z + 4));
  }
  for (let z = -40; z <= 40; z += 16) { k.add(cyl(0.1, 0.14, 5, 6), '#2e3238', tf(9, 2.5, z)); k.light(sphere(0.4, 6, 4), '#fff1c8', tf(9, 5.2, z)); }
  return k.model(26, true);
}

// ───────────────────────────── Amazonas (Manaus) ─────────────────────────────

/** Teatro Amazonas: corpo rosa com colunata branca e frontão, e a cúpula de azulejos verde, amarelo e azul. */
function teatroAmazonas(): Model {
  const k = new Kit();
  const D = 34; const L = 46; const H = 16;
  k.add(box(D + 6, 2.4, L + 4), '#d8d0c4', tf(-D / 2 + 2, 0.4, 0));
  k.fac('classic', D, H, L, '#eaa3a8', tf(-D / 2, 1.6 + H / 2, 0));
  k.add(box(D + 0.8, 1.0, L + 0.8), '#f6f2ea', tf(-D / 2, 1.6 + H + 0.5, 0));
  k.add(box(D - 4, 1.4, L - 4), '#eaa3a8', tf(-D / 2, 1.6 + H + 1.6, 0));
  // Pórtico: colunata dupla, entablamento e frontão.
  const y0 = 1.6;
  for (let i = 0; i < 6; i++) {
    const z = -10 + i * 4;
    k.add(cyl(0.6, 0.7, 10.5, 8), '#f6f4ee', tf(3.5, y0 + 5.25, z));
  }
  k.add(box(7, 1.6, 24), '#f6f4ee', tf(1.5, y0 + 11.3, 0));
  k.add(gable(24.4, 4.2, 7), '#f6f4ee', tf(1.5, y0 + 12.1, 0, 1, 1, 1, 0, PI / 2, 0));
  k.add(box(4, 3, 26), '#d8d0c4', tf(4.5, 0.5, 0));
  for (let s = 0; s < 3; s++) k.add(box(1.2, 0.4, 24), '#e8e2d6', tf(6.4 + s * 1.2, 0.2 + (2 - s) * 0.4, 0));
  k.add(box(0.3, 6, 16), '#5a2a30', tf(0.2, y0 + 3.5, 0));
  for (const z of [-15, 15]) k.add(box(1, H + 1.5, 6), '#f6f4ee', tf(0.3, y0 + H / 2, z));
  // Tambor e cúpula de azulejos.
  const dx = -D / 2 - 4; const dy = y0 + H + 2.3;
  k.add(cyl(9, 9.5, 4.5, 16), '#eaa3a8', tf(dx, dy + 2.25, 0));
  k.add(cyl(9.4, 9.4, 0.6, 16), '#f6f4ee', tf(dx, dy + 4.6, 0));
  const tiles = ['#2f8f4a', '#f2c230', '#2a5aa8'];
  const dome = paint(sphere(9, 16, 6, 0, PI * 2, 0, PI / 2), '#2f8f4a', tf(dx, dy + 4.9, 0, 1, 1.25, 1));
  k.raw(recolor(dome, (x, y, z) => {
    const a = Math.floor(((Math.atan2(z, x - dx) + PI) / (PI * 2)) * 16);
    const b = Math.floor((y - dy) / 2.6);
    return tiles[((a + b) % 3 + 3) % 3];
  }));
  k.add(cyl(1.2, 1.5, 2.4, 8), '#f6f4ee', tf(dx, dy + 16.6, 0));
  k.add(cone(1.4, 2.4, 8), '#f2c230', tf(dx, dy + 19, 0));
  k.light(box(0.3, 2.4, 1.4), '#ffd890', tf(0.5, y0 + 13, -8)).light(box(0.3, 2.4, 1.4), '#ffd890', tf(0.5, y0 + 13, 8));
  for (const z of [-20, 20]) { k.add(cyl(0.12, 0.16, 5, 6), '#2e3238', tf(9, 2.5, z)); k.light(sphere(0.45, 6, 4), '#fff1c8', tf(9, 5.2, z)); }
  return k.model(dy + 21, true);
}

/** Barco regional da Amazônia: casco branco de listras, dois conveses abertos com redes coloridas. */
function barcoRegional(): Model {
  const k = new Kit();
  const L = 24; const W = 6.4;
  // Casco com proa em cunha (+Z).
  k.add(box(W, 2.4, L - 4), '#f4f4f0', tf(0, 0.6, -2));
  k.add(cyl(0.01, W / 2 * 1.414, 2.4, 4), '#f4f4f0', tf(0, 0.6, L / 2 - 4, 1, 1, 1.8, 0, PI / 4, 0));
  k.add(box(W + 0.1, 0.4, L - 4), '#1e6fc9', tf(0, 0.9, -2));
  k.add(box(W + 0.1, 0.3, L - 4), '#d63a3a', tf(0, 0.3, -2));
  // Convés 1 e 2: pilares, teto, guarda-corpo, redes.
  const hammock = ['#d63a3a', '#ffd23f', '#3ddc84', '#1e88e5', '#ff8c1a', '#8e24aa'];
  for (let d = 0; d < 2; d++) {
    const y = 1.8 + d * 2.6; const len = L - 6 - d * 3; const zc = -3 - d * 1.5;
    k.add(box(W - 0.2, 0.25, len), '#f4f4f0', tf(0, y + 2.4, zc));
    for (let q = 0; q <= 5; q++) for (const sx of [-1, 1]) k.add(box(0.18, 2.4, 0.18), '#f4f4f0', tf(sx * (W / 2 - 0.2), y + 1.2, zc - len / 2 + 0.2 + q * (len - 0.4) / 5));
    for (const sx of [-1, 1]) k.add(box(0.1, 0.8, len), d ? '#1e6fc9' : '#f4f4f0', tf(sx * (W / 2 - 0.15), y + 0.4, zc));
    for (let q = 0; q < 6; q++) k.add(box(1.2, 0.5, 2.0), hammock[(q + d * 2) % 6], tf((q % 2 ? 0.8 : -0.8), y + 1.5, zc - len / 2 + 1.5 + q * (len - 3) / 5));
    for (let q = 0; q < 4; q++) for (const sx of [-1, 1]) k.light(box(0.12, 0.3, 0.5), '#ffe8b0', tf(sx * (W / 2 - 0.05), y + 2.0, zc - len / 2 + 1.5 + q * (len - 3) / 3));
  }
  k.add(box(W - 0.6, 2.2, 3.4), '#f4f4f0', tf(0, 7.5, 3.5));
  k.add(box(0.1, 0.8, 3), '#2a3442', tf(W / 2 - 0.25, 7.9, 3.5));
  k.add(box(W, 0.3, 4.2), '#1e6fc9', tf(0, 8.75, 3.5));
  k.add(cyl(0.3, 0.3, 2.4, 6), '#d63a3a', tf(0, 9.7, -2));
  k.add(box(0.02, 0.7, 1.4), '#ffd23f', tf(0.3, 10.7, -2)).add(box(0.03, 0.7, 1.4), '#3ddc84', tf(0.32, 10.7, -2, 1, 0.5, 0.6));
  return k.model(11, true);
}

// ───────────────────────────── Amapá (Macapá) ─────────────────────────────

/** Monumento Marco Zero: torre de concreto com o furo do relógio de sol e a linha do Equador no chão. */
function marcoZeroEquador(): Model {
  const k = new Kit();
  const prof: Array<[number, number]> = [[-3.5, -1], [-2.4, 30], [2.4, 30], [3.5, -1]];
  const hole: Array<[number, number]> = [];
  for (let q = 0; q < 10; q++) { const a = (q / 10) * PI * 2; hole.push([Math.cos(a) * 1.5, 24 + Math.sin(a) * 1.5]); }
  // A torre olha a pista pela face larga (o furo aparece de lado e de frente).
  k.add(extrude(prof, 3, [hole]), '#d8d4cc', tf(-1.5, 0, 0, 1, 1, 1, 0, PI / 2, 0));
  k.add(box(4, 0.4, 7.6), '#b8b2a8', tf(0, 30.2, 0));
  k.add(cone(0.6, 2.4, 4), '#d8d4cc', tf(0, 31.6, 0));
  // Praça redonda (o mostrador) e a linha do Equador atravessando (vermelha e branca).
  k.add(cyl(14, 14, 0.4, 16), '#e8e2d4', tf(0, 0.2, 0));
  for (let q = 0; q < 12; q++) { const a = (q / 12) * PI * 2; k.add(box(0.8, 0.5, 2.4), '#5a5a5a', tf(Math.cos(a) * 12, 0.45, Math.sin(a) * 12, 1, 1, 1, 0, -a, 0)); }
  for (let q = -8; q < 8; q++) k.add(box(6, 0.12, 1.2), q % 2 ? '#f4f4f0' : '#d63a2a', tf(q * 6 + 3, 0.45, 0));
  k.add(box(4, 0.8, 16), '#d63a2a', tf(18, 0.4, 0));
  k.light(cyl(1.55, 1.55, 0.3, 10, true), '#fff1c8', tf(0, 24, 0, 1, 1, 1, 0, 0, PI / 2));
  for (const z of [-9, 9]) { k.add(cyl(0.1, 0.14, 6, 6), '#5d636b', tf(9, 3, z)); k.light(sphere(0.4, 6, 4), '#fff1c8', tf(9, 6.2, z)); }
  return k.model(33, true);
}

/** Fortaleza de São José de Macapá: quatro baluartes de pedra, gramado, casas brancas dentro. */
function fortalezaMacapa(): Model {
  const k = new Kit();
  bastionFort(k, 82, 4, 8, '#9a8e7a', '#c8bca4', '#6a9a4a', PI / 4);
  for (const [x, z, w, d] of [[-18, 0, 12, 36], [18, 0, 12, 36], [0, -20, 26, 10]] as const) {
    k.add(box(w, 8, d), '#f4f0e6', tf(x, 4, z)).add(hip(w + 0.6, 3, d + 0.6), '#b8552e', tf(x, 8, z));
  }
  k.add(box(9, 8, 7), '#f4f0e6', tf(0, 4, 18)).add(gable(9, 3, 7, 0.3), '#b8552e', tf(0, 8, 18));
  k.add(cyl(0.15, 0.15, 16, 5), '#d8d8d8', tf(30, 8, 0));
  k.add(box(0.06, 2, 3), '#ffd23f', tf(30, 15, 1.5));
  return k.model(18, false);
}

// ───────────────────────────── Roraima ─────────────────────────────

/** Monte Roraima: tepui de topo plano, paredões verticais, cachoeiras finas, nuvens no topo (escala grande). */
function tepui(): Model {
  const k = new Kit();
  const H = 700;
  const poly = mesa(k, { rx: 380, rz: 760, H, talus: 280, seed: 201, n: 16, rock: ['#6a6268', '#55505a', '#7a7076', '#605a62'], top: '#4a5a44', slope: '#2f6a34', step: 45 });
  // Cachoeiras na face que olha a pista.
  for (const [i, f] of [[0, 1.05], [1, 1.05], [15, 1.05]] as const) {
    const [px, pz] = poly[i];
    k.add(box(4, 300, 16), '#eaf4ff', tf(px * f, H - 170, pz * f));
  }
  cloud(k, 120, H + 20, -500, 70, 202);
  cloud(k, 60, H + 40, 300, 90, 203);
  return k.model(H + 60, false, [0.85, 1.05]);
}

/** Maloca: casa redonda de palha com cobertura cônica até perto do chão (e uma menor ao lado). */
function maloca(): Model {
  const k = new Kit();
  const one = (x: number, z: number, R: number, H: number, seed: number): void => {
    k.add(cyl(R, R, 1.8, 14), '#8a6a48', tf(x, 0.9, z));
    const layers = 4;
    for (let q = 0; q < layers; q++) {
      const t0 = q / layers; const t1 = (q + 1) / layers;
      const r0 = (R + 1) * (1 - t0 * 0.92); const r1 = (R + 1) * (1 - t1 * 0.92);
      k.raw(speckle(paint(jitter(cyl(r1, r0, (H - 1.5) / layers, 14, true), 0.03, seed + q), q % 2 ? '#c9a86a' : '#b8965a', tf(x, 1.5 + (t0 + t1) / 2 * (H - 1.5), z)), 0.05, seed + q));
    }
    k.add(cone((R + 1) * 0.1, 1.6, 6), '#9a7a48', tf(x, H + 0.8, z));
    k.add(box(0.3, 1.6, 1.4), '#2a1e14', tf(x + R + 0.05, 0.8, z));
  };
  one(0, 0, 9, 12, 211);
  one(-6, 20, 5, 7.5, 215);
  for (let i = 0; i < 5; i++) k.add(jitter(ico(1, 0), 0.2, 220 + i), '#5f8f3c', tf(-12 + hash2(221, i) * 10, 0.8, -16 + i * 6, 2, 1.4, 2));
  k.add(cyl(0.5, 0.7, 0.4, 6), '#3a2a20', tf(14, 0.2, 4)).light(cone(0.4, 0.8, 5), '#ffb040', tf(14, 0.8, 4));
  return k.model(13, true);
}

// ───────────────────────────── Rondônia (Porto Velho) ─────────────────────────────

/** Locomotiva a vapor da Madeira-Mamoré: caldeira preta, rodas vermelhas, cabine e tênder, nos trilhos. */
function locomotivaMamore(): Model {
  const k = new Kit();
  const black = '#26282c'; const red = '#c0302a'; const brass = '#c9a040';
  // Trilhos e dormentes ao longo de Z.
  for (const x of [-0.75, 0.75]) k.add(box(0.12, 0.18, 30), '#7a7e86', tf(x, 0.39, 0));
  for (let z = -14; z <= 14; z += 1.2) k.add(box(2.6, 0.2, 0.3), '#5a4632', tf(0, 0.2, z));
  k.add(box(3.4, 0.2, 31), '#8a8278', tf(0, 0.05, 0));
  // Rodas motrizes e da frente.
  for (const z of [1.2, 3.0, 4.8]) for (const x of [-1.05, 1.05]) k.add(cyl(0.75, 0.75, 0.2, 10), red, tf(x, 1.25, z, 1, 1, 1, 0, 0, PI / 2));
  for (const z of [7.2]) for (const x of [-1.05, 1.05]) k.add(cyl(0.45, 0.45, 0.2, 8), red, tf(x, 0.95, z, 1, 1, 1, 0, 0, PI / 2));
  for (const x of [-1.2, 1.2]) k.add(box(0.1, 0.15, 3.8), '#9aa0a8', tf(x, 1.25, 3));
  k.add(box(1.8, 0.6, 10), black, tf(0, 1.5, 3.5));
  // Caldeira, chaminé em funil, domo, sino, farol.
  k.add(cyl(1.0, 1.0, 7, 10), black, tf(0, 2.75, 4, 1, 1, 1, PI / 2, 0, 0));
  k.add(cyl(1.05, 1.05, 0.2, 10), brass, tf(0, 2.75, 5.5, 1, 1, 1, PI / 2, 0, 0));
  k.add(cyl(1.08, 1.08, 0.5, 10), '#3a3c42', tf(0, 2.75, 7.6, 1, 1, 1, PI / 2, 0, 0));
  k.add(cyl(0.3, 0.32, 1.2, 8), black, tf(0, 4.2, 6.6));
  k.add(cyl(0.75, 0.3, 1.1, 8), black, tf(0, 5.3, 6.6));
  k.add(cyl(0.38, 0.42, 0.7, 8), brass, tf(0, 4.0, 3.8)).add(sphere(0.38, 8, 4, 0, PI * 2, 0, PI / 2), brass, tf(0, 4.35, 3.8));
  k.add(box(0.6, 0.6, 0.5), black, tf(0, 3.95, 7.75));
  k.light(box(0.4, 0.4, 0.06), '#fff1c8', tf(0, 3.95, 8.02));
  // Limpa-trilhos vermelho.
  k.add(cone(1.3, 1.2, 4), red, tf(0, 0.6, 8.4, 1, 1, 1, -PI / 2, PI / 4, 0));
  k.add(box(2.4, 0.3, 0.3), red, tf(0, 1.3, 8.1));
  // Cabine com teto curvo e janelas.
  k.add(box(2.6, 2.6, 2.6), black, tf(0, 3.3, -0.4));
  k.add(cyl(1.5, 1.5, 3.0, 8, false), black, tf(0, 4.6, -0.4, 1, 0.3, 1, PI / 2, 0, 0));
  for (const x of [-1.31, 1.31]) k.light(box(0.04, 0.8, 0.9), '#ffd890', tf(x, 3.8, -0.4));
  k.add(box(2.7, 0.15, 2.7), red, tf(0, 2.0, -0.4));
  // Tênder com lenha.
  k.add(box(2.4, 1.8, 4.6), black, tf(0, 2.0, -4.6));
  k.add(box(2.2, 0.6, 4.2), '#7a5a3a', tf(0, 3.1, -4.6));
  for (const z of [-3.4, -5.8]) for (const x of [-1.05, 1.05]) k.add(cyl(0.45, 0.45, 0.2, 8), red, tf(x, 0.95, z, 1, 1, 1, 0, 0, PI / 2));
  k.add(box(2.6, 0.12, 4.8), red, tf(0, 1.05, -4.6));
  return k.model(6, true);
}

/** As Três Caixas-d'Água de Porto Velho: tanques de ferro cilíndricos em torres de pernas treliçadas. */
function caixasDagua(): Model {
  const k = new Kit();
  const iron = '#4a4e55'; const leg = '#3a3d42';
  const towers: Array<[number, number, number]> = [[0, -11, 21], [-3, 0, 22.5], [0, 11, 21]];
  for (const [x, z, H] of towers) {
    const r = 3.4; const base = 2.8; const topR = 2.2;
    for (let q = 0; q < 4; q++) {
      const a = PI / 4 + q * PI / 2;
      k.beam([x + Math.cos(a) * base, -1, z + Math.sin(a) * base], [x + Math.cos(a) * topR, H - 7.5, z + Math.sin(a) * topR], 0.4, leg);
    }
    for (const y of [4, 8.5, 12.5]) {
      const f = 1 - (y / (H - 7.5)) * (1 - topR / base);
      const rr = base * f;
      for (let q = 0; q < 4; q++) {
        const a0 = PI / 4 + q * PI / 2; const a1 = a0 + PI / 2;
        k.beam([x + Math.cos(a0) * rr, y, z + Math.sin(a0) * rr], [x + Math.cos(a1) * rr, y, z + Math.sin(a1) * rr], 0.18, leg);
      }
    }
    k.add(cyl(r, r, 0.5, 12), leg, tf(x, H - 7.3, z));
    k.raw(recolor(paint(cyl(r, r, 7, 12), iron, tf(x, H - 3.5, z)), (_cx, cy, _cz, t) => (hash2(t, Math.round(cy)) > 0.82 ? '#7a4a32' : null)));
    k.add(cone(r + 0.3, 2.4, 12), leg, tf(x, H + 1.2, z));
    k.add(cyl(0.12, 0.12, 1.4, 5), leg, tf(x, H + 2.9, z));
  }
  k.add(box(14, 0.4, 34), '#a8a092', tf(-1.5, 0.2, 0));
  for (const z of [-17, 17]) { k.add(cyl(0.1, 0.14, 5, 6), '#2e3238', tf(5, 2.5, z)); k.light(sphere(0.4, 6, 4), '#fff1c8', tf(5, 5.2, z)); }
  return k.model(26, true);
}

// ───────────────────────────── Acre (Rio Branco) ─────────────────────────────

/** Gameleira: árvore gigante de raízes tabulares e copa larga em camadas (~28 m, copa de 36 m). */
function gameleira(): Model {
  const k = new Kit();
  const bark = '#8a7a66';
  for (let q = 0; q < 7; q++) {
    const a = (q / 7) * PI * 2 + hash2(231, q) * 0.4;
    const r = 5.5 + hash2(232, q) * 2;
    k.add(tris([
      0, 0, 0, Math.cos(a) * r, 0, Math.sin(a) * r, 0, 7, 0,
      0, 0, 0, 0, 7, 0, Math.cos(a) * r, 0, Math.sin(a) * r,
    ]), bark, tf(0, -0.3, 0, 1, 1, 1));
    k.add(box(0.7, 6.5, r * 0.8), bark, tf(Math.cos(a) * r * 0.35, 2.4, Math.sin(a) * r * 0.35, 1, 1, 1, 0, -a + PI / 2, 0).multiply(tf(0, 0, 0, 1, 1, 1, 0.35, 0, 0)));
  }
  k.add(cyl(1.6, 2.6, 14, 8), bark, tf(0, 7, 0));
  // Galhos grossos saindo em leque.
  const tops: V3[] = [];
  for (let q = 0; q < 5; q++) {
    const a = (q / 5) * PI * 2 + 0.3;
    const t: V3 = [Math.cos(a) * 9, 19 + hash2(233, q) * 3, Math.sin(a) * 9];
    k.rod([0, 13, 0], t, 0.8, bark, 6);
    tops.push(t);
  }
  // Copa: massas achatadas de verdes diferentes.
  const greens = ['#3f7f34', '#4a8f3a', '#356f2e', '#58a044'];
  tops.forEach(([x, y, z], q) => k.raw(speckle(paint(jitter(ico(1, 1), 0.14, 234 + q), greens[q % 4], tf(x, y + 2.5, z, 9, 5, 9)), 0.08, 234 + q)));
  k.raw(speckle(paint(jitter(ico(1, 1), 0.12, 240), '#4a8f3a', tf(0, 25, 0, 12, 5.5, 12)), 0.08, 240));
  for (let q = 0; q < 6; q++) {
    const a = (q / 6) * PI * 2;
    k.raw(speckle(paint(jitter(ico(1, 0), 0.15, 241 + q), greens[(q + 1) % 4], tf(Math.cos(a) * 15, 17.5, Math.sin(a) * 15, 6, 3.6, 6)), 0.08, 241 + q));
  }
  // Grade e banquinhos da praça.
  for (let q = 0; q < 12; q++) { const a = (q / 12) * PI * 2; k.add(box(0.12, 0.9, 0.12), '#3a3d42', tf(Math.cos(a) * 9, 0.45, Math.sin(a) * 9)); }
  return k.model(31, true, [0.72, 1.08]);
}

/** Palácio Rio Branco: neoclássico branco e creme, pórtico de colunas em dois andares, frontão e platibanda. */
function palacioRioBranco(): Model {
  const k = new Kit();
  const D = 24; const L = 46; const H = 14;
  k.add(box(D + 2, 1.6, L + 2), '#c8c0b0', tf(-D / 2, 0.3, 0));
  k.fac('classic', D, H, L, '#f2e8d2', tf(-D / 2, 1.1 + H / 2, 0));
  k.add(box(D + 0.8, 0.6, L + 0.8), '#fbf8f0', tf(-D / 2, 1.1 + 7, 0));
  k.add(box(D + 0.8, 0.9, L + 0.8), '#fbf8f0', tf(-D / 2, 1.1 + H + 0.45, 0));
  // Platibanda com balaústres e pináculos.
  k.add(box(D, 1.4, L), '#f2e8d2', tf(-D / 2, 1.1 + H + 1.6, 0));
  for (let q = -5; q <= 5; q++) k.add(box(0.8, 1.6, 0.8), '#fbf8f0', tf(0.2, 1.1 + H + 2.4, q * 4.4));
  // Pórtico central: 6 colunas, entablamento e frontão triangular.
  for (let i = 0; i < 6; i++) k.add(cyl(0.55, 0.65, H - 1, 8), '#fbf8f0', tf(3, 1.1 + (H - 1) / 2, -8.5 + i * 3.4));
  k.add(box(5.2, 1.4, 21), '#fbf8f0', tf(1.4, 1.1 + H - 0.3, 0));
  k.add(gable(21.6, 4.4, 5.2), '#f2e8d2', tf(1.4, 1.1 + H + 0.4, 0, 1, 1, 1, 0, PI / 2, 0));
  k.add(box(0.2, 1.4, 4), '#c9a040', tf(1.4 + 2.62, 1.1 + H + 1.8, 0));
  k.add(box(5, 1.2, 22), '#c8c0b0', tf(3, 0.6, 0));
  k.add(box(0.3, 5, 3.4), '#3a2a20', tf(0.15, 3.6, 0));
  // Mastro com a bandeira do Acre (verde e amarela, estrela vermelha).
  k.add(cyl(0.1, 0.1, 10, 5), '#d8d8d8', tf(-D / 2, 1.1 + H + 2.3 + 5, 0));
  k.add(box(0.05, 1.8, 3), '#ffd23f', tf(-D / 2, 1.1 + H + 6.6, 1.6)).add(box(0.06, 0.9, 3), '#2a8a3a', tf(-D / 2, 1.1 + H + 6.1, 1.6));
  k.add(box(0.07, 0.4, 0.4), '#d63a2a', tf(-D / 2, 1.1 + H + 7.1, 0.6));
  for (const z of [-14, 14]) k.light(box(0.2, 1.4, 0.8), '#ffe2a6', tf(0.2, 1.1 + 4, z));
  for (const z of [-18, 18]) { k.add(cyl(0.12, 0.16, 5, 6), '#2e3238', tf(8, 2.5, z)); k.light(sphere(0.45, 6, 4), '#fff1c8', tf(8, 5.2, z)); }
  return k.model(H + 12, true);
}

// ───────────────────────────── Tocantins (Jalapão) ─────────────────────────────

/** Serra do Espírito Santo: paredão de mesa laranja em estratos, talude de cerrado (escala grande). */
function serraEspiritoSanto(): Model {
  const k = new Kit();
  mesa(k, { rx: 220, rz: 800, H: 300, talus: 120, seed: 251, n: 18, rock: ['#d0703a', '#c0602e', '#e08848', '#b85a34', '#d88a50'], top: '#8a8a4a', slope: '#a08850', step: 18 });
  return k.model(240, false, [0.82, 1.05]);
}

/** Dunas do Jalapão: dunas alaranjadas com capim dourado e buritis no pé. */
function dunasJalapao(): Model {
  const k = new Kit();
  dune(k, 0, 0, 70, 40, 90, '#e8964a', '#f6b66c', 261);
  dune(k, -30, -110, 50, 28, 60, '#e08a40', '#f2ac62', 262, 0.5);
  dune(k, -20, 100, 46, 26, 56, '#ea9c52', '#f8bc74', 263, -0.4);
  for (let i = 0; i < 14; i++) k.add(jitter(ico(1, 0), 0.25, 264 + i), i % 2 ? '#c8a848' : '#8a9a4a', tf(62 + hash2(265, i) * 18, 0.5, -150 + i * 22, 2.8, 1.0, 2.8));
  for (let i = 0; i < 4; i++) palm(k, 74 + hash2(266, i) * 10, -90 + i * 60, 14 + hash2(267, i) * 4, 2660 + i);
  return k.model(42, false, [0.7, 1.08]);
}

// ───────────────────────────── Registro ─────────────────────────────

const def = (build: () => Model, place: LandmarkDef['place'], side: LandmarkDef['side'], perLap: number): LandmarkDef => ({ build, place, side, perLap });

export const LANDMARKS_BRASIL_NORTE_NORDESTE: LandmarkRegistry = {
  // Bahia
  farol_da_barra: def(farolDaBarra, 'far', 'sea', 1),
  elevador_lacerda: def(elevadorLacerda, 'far', 'land', 1),
  casario_pelourinho: def(casarioPelourinho, 'near', 'land', 2),
  // Sergipe
  arcos_atalaia: def(arcosAtalaia, 'near', 'sea', 2),
  ponte_aracaju: def(ponteAracaju, 'far', 'sea', 1),
  // Alagoas
  jangada: def(jangada, 'near', 'sea', 4),
  coqueiral: def(coqueiral, 'near', 'any', 6),
  // Pernambuco
  igrejas_olinda: def(igrejasOlinda, 'far', 'land', 1),
  bonecos_olinda: def(bonecosOlinda, 'near', 'any', 3),
  // Paraíba
  farol_cabo_branco: def(farolCaboBranco, 'far', 'sea', 1),
  estacao_cabo_branco: def(estacaoCaboBranco, 'near', 'land', 1),
  // Rio Grande do Norte
  forte_reis_magos: def(forteReisMagos, 'far', 'sea', 1),
  ponte_newton_navarro: def(ponteNewtonNavarro, 'far', 'sea', 1),
  // Ceará
  pedra_furada_jeri: def(pedraFuradaJeri, 'near', 'sea', 1),
  duna_por_do_sol: def(dunaPorDoSol, 'far', 'any', 2),
  // Piauí
  pedra_furada_capivara: def(pedraFuradaCapivara, 'far', 'land', 1),
  pinturas_rupestres: def(pinturasRupestres, 'near', 'land', 2),
  // Maranhão
  lagoas_lencois: def(lagoasLencois, 'far', 'any', 3),
  farol_preguicas: def(farolPreguicas, 'far', 'any', 1),
  // Pará
  ver_o_peso: def(verOPeso, 'near', 'sea', 1),
  estacao_docas: def(estacaoDocas, 'near', 'sea', 1),
  // Amazonas
  teatro_amazonas: def(teatroAmazonas, 'near', 'land', 1),
  barco_regional: def(barcoRegional, 'near', 'sea', 3),
  // Amapá
  marco_zero_equador: def(marcoZeroEquador, 'near', 'any', 1),
  fortaleza_macapa: def(fortalezaMacapa, 'far', 'any', 1),
  // Roraima
  tepui: def(tepui, 'skyline', 'any', 1),
  maloca: def(maloca, 'near', 'land', 2),
  // Rondônia
  locomotiva_mamore: def(locomotivaMamore, 'near', 'land', 1),
  caixas_dagua: def(caixasDagua, 'near', 'land', 1),
  // Acre
  gameleira: def(gameleira, 'near', 'any', 1),
  palacio_rio_branco: def(palacioRioBranco, 'near', 'land', 1),
  // Tocantins
  serra_espirito_santo: def(serraEspiritoSanto, 'skyline', 'land', 1),
  dunas_jalapao: def(dunasJalapao, 'far', 'any', 2),
};
