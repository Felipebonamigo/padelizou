// Marcos turísticos do Norte e do Nordeste (onda G, docs/PISTAS-TURISMO.md). Um construtor por id de
// src/core/data/places.ts. Convenção dos modelos daqui: centro em x = z = 0, base em y = 0 (fundações e
// pilares entram até 2 m no chão: terreno inclinado não deixa nada flutuando), a frente olha +X (a pista)
// e o que é comprido (ponte, galpões, paredão, serra) corre ao longo de Z, paralelo à pista.
// Escala real, low-poly de faces planas e cor chapada; janelas e luzes acendem à noite (fachada texturizada,
// `glow`) e as luzes de balizamento piscam (`beacon`). Sem Math.random: variação por hash.
// Leitura (onda J, docs/VISUAL.md › "Leitura"): o marco tem de ser reconhecido da pista, a 150–250 m, na névoa. O
// traço que define o lugar vem exagerado (a torre do Elevador, as guaritas dos fortes, o furo das pedras, a cúpula do
// Teatro) e os marcos pequenos, escalados 1,2–1,5× (`scaled`); cada refeito tem a sua medida em
// tests/landmarks-leitura-norte-nordeste-1.test.ts.
import * as THREE from 'three';
import { hash2, hash3 } from '../../noise';
import { box, cone, cyl, frond, gable, hip, ico, jitter, merge, paint, shadeY, speckle, sphere, tf, tintUp, tris, type Geo, type MatKey, type Model, type ModelPart } from '../geom';
import { FACADE_TILE } from '../structures';
import { cliff, dune as duneShape } from './kit';
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

/** Coqueiro alto (12–28 m): tronco anelado e inclinado, folhas caídas, cocos; `lush` = copa cheia (10 folhas largas). */
function palm(k: Kit, x: number, z: number, H: number, seed: number, y0 = 0, lush = false): void {
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
  const n = lush ? 10 : 8;
  const s = H / 16; const w = lush ? 1.9 : 1.3;
  for (let q = 0; q < n; q++) {
    const a = (q / n) * PI * 2 + hash2(seed, q) * 0.5;
    k.add(frond(4.6 * s + hash2(seed, q + 20) * s, w * s, a, 1.0 * s, 3.4 * s, 3), q % 2 ? '#3f9a3a' : '#52ae44', tf(tx, ty, tz));
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

/**
 * Ponte estaiada ao longo de Z: tabuleiro, pilares, duas torres (H, A ou mastro único), leque de estais, luzes. `lit`:
 * a ponte das corridas à noite — os estais (0,6 m) e a frente das torres são luz (`glow`), o desenho dela no
 * escuro (sem isso, à noite, só a fileira de postes do tabuleiro aparecia).
 */
function cableBridge(k: Kit, o: { L: number; deckY: number; W: number; span: number; pylonH: number; type: 'H' | 'A'; deck: string; pylon: string; cable: string; pierStep: number; lamp: string; lit?: boolean }): void {
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
      if (o.lit) k.glow.push(paint(box(0.3, 1, 1.6), o.lamp, segMatrix([leg + 1.4, deckY, zp], [leg + 0.8, pylonH - 1, zp])));
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
        for (const dir of [-1, 1]) {
          const a: V3 = [topX, yTop, zp]; const b: V3 = [sx * (W / 2 - 0.6), deckY + 0.4, zp + dir * reach];
          if (o.lit) k.glow.push(paint(box(0.6, 1, 0.6), o.cable, segMatrix(a, b))); else k.beam(a, b, 0.22, o.cable);
        }
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

/** Nuvem baixa de poucos triângulos (icosaedros achatados). */
function cloud(k: Kit, x: number, y: number, z: number, s: number, seed: number): void {
  for (let q = 0; q < 4; q++) {
    const dz = (q - 1.5) * s * 0.9; const r = s * (0.6 + hash2(seed, q) * 0.5);
    k.add(jitter(ico(1, 0), 0.15, seed + q), '#f4f6fa', tf(x, y + hash2(seed, q + 9) * s * 0.2, z + dz, r * 1.2, r * 0.55, r));
  }
}

/** Monta com `build` e escala o que ele pôs no kit (em torno da origem, a base fica no chão): o exagero de um marco pequeno. */
function scaled(k: Kit, s: number, build: () => void): void {
  const n = [k.flat.length, k.glow.length, k.beacon.length];
  const fac = new Map([...k.facade].map(([m, l]) => [m, l.length]));
  build();
  const m = new THREE.Matrix4().makeScale(s, s, s);
  for (const g of [...k.flat.slice(n[0]), ...k.glow.slice(n[1]), ...k.beacon.slice(n[2])]) g.applyMatrix4(m);
  for (const [mat, l] of k.facade) for (const g of l.slice(fac.get(mat) ?? 0)) g.applyMatrix4(m);
}

/** Parede inclinada que fecha o contorno `a` (em y0) até o mesmo contorno noutra escala, `b` (em y1): a escarpa dos fortes. */
function ring(a: Array<[number, number]>, b: Array<[number, number]>, y0: number, y1: number): Geo {
  const p: number[] = [];
  for (let i = 0; i < a.length; i++) {
    const j = (i + 1) % a.length;
    p.push(a[i][0], y0, a[i][1], b[j][0], y1, b[j][1], a[j][0], y0, a[j][1], a[i][0], y0, a[i][1], b[i][0], y1, b[i][1], b[j][0], y1, b[j][1]);
  }
  return tris(p);
}

/** Guarita de forte (a sentinela redonda na ponta do baluarte): mísula, corpo, fresta virada para `out`, cúpula e pináculo. */
function guarita(k: Kit, x: number, y: number, z: number, r: number, body: string, cap: string, out: number): void {
  k.add(cone(r * 1.05, r * 1.2, 8), body, tf(x, y - r * 0.4, z, 1, 1, 1, PI, 0, 0));
  k.add(cyl(r, r, r * 2.2, 8), body, tf(x, y + r * 1.3, z));
  k.add(box(0.25, r * 0.9, r * 0.5), '#2a2a2e', tf(x + Math.cos(out) * r, y + r * 1.5, z + Math.sin(out) * r, 1, 1, 1, 0, -out, 0));
  k.add(sphere(r * 1.1, 8, 4, 0, PI * 2, 0, PI / 2), cap, tf(x, y + r * 2.4, z, 1, 0.9, 1));
  k.add(cone(r * 0.25, r * 1.0, 6), cap, tf(x, y + r * 3.6, z));
}

/**
 * Forte abaluartado (estrela de baluartes em ponta de lança) do jeito que se lê da pista, de baixo: a escarpa
 * inclinada e mais escura no pé, o cordão claro, o muro com parapeito e, na ponta de cada baluarte, a guarita redonda
 * de cúpula — de longe, o que diz "forte colonial" é a fileira de guaritas sobre o muro. `rot` = 0 põe um baluarte de
 * proa para a pista (+X). Devolve o contorno do pátio.
 */
function starFort(k: Kit, o: { R: number; n: number; H: number; rot: number; stone: string; base: string; band: string; court: string; body: string; cap: string; gr: number }): Array<[number, number]> {
  const pts: Array<[number, number]> = [];
  const tips: Array<[number, number, number]> = [];
  for (let i = 0; i < o.n; i++) {
    const a = o.rot + (i / o.n) * PI * 2;
    const P = (r: number, da: number): [number, number] => [Math.cos(a + da) * o.R * r, Math.sin(a + da) * o.R * r];
    pts.push(P(0.6, -0.2), P(0.8, -0.34), P(1, 0), P(0.8, 0.34), P(0.6, 0.2));
    const [tx, tz] = P(0.95, 0);
    tips.push([tx, tz, a]);
  }
  const sc = (s: number): Array<[number, number]> => pts.map(([x, z]): [number, number] => [x * s, z * s]);
  const inner = sc(0.8);
  const yB = o.H * 0.45;
  k.add(ring(sc(1.08), pts, -1.5, yB), o.base);
  k.add(plan(sc(1.015), 0.7, [sc(0.97)]), o.band, tf(0, yB, 0));
  k.add(plan(pts, o.H - yB, [inner]), o.stone, tf(0, yB, 0));
  k.add(plan(sc(1.01), 0.9, [sc(0.95)]), o.band, tf(0, o.H, 0));
  // O terrapleno: o pátio cheio até perto do alto do muro (de cima não é um poço).
  k.add(plan(inner, o.H - 1 - yB), o.court, tf(0, yB, 0));
  for (const [x, z, a] of tips) guarita(k, x, o.H + 0.9, z, o.gr, o.body, o.cap, a);
  return inner;
}

/**
 * Laje de pedra com furo (arco), de frente para +X: uma grade de células em (z, y) — `solid(z, y)` diz o que é
 * pedra —, a face da frente em x0 (desvio por hash nos cantos, compartilhado: a malha não abre), o fundo em
 * x0 − depth e as bordas (o contorno e o aro do furo) fechadas. Cor por célula (`color`: estratos, manchas).
 */
function holedSlab(o: { z0: number; z1: number; y0: number; y1: number; cols: number; rows: number; x0: number; depth: number; bump: number; seed: number; solid: (z: number, y: number) => boolean; color: (z: number, y: number) => THREE.Color; snap?: (z: number, y: number) => [number, number] }): Geo {
  const dz = (o.z1 - o.z0) / o.cols; const dy = (o.y1 - o.y0) / o.rows;
  const Z = (i: number): number => o.z0 + i * dz; const Y = (j: number): number => o.y0 + j * dy;
  // Os cantos das células vão para o contorno (`snap`): a borda e o aro do furo saem lisos, sem degraus de célula.
  const at = (i: number, j: number): [number, number] => (o.snap ? o.snap(Z(i), Y(j)) : [Z(i), Y(j)]);
  const fill = (i: number, j: number): boolean => i >= 0 && j >= 0 && i < o.cols && j < o.rows && o.solid(Z(i + 0.5), Y(j + 0.5));
  const fx = (i: number, j: number): number => o.x0 + (hash2(o.seed + i * 131, j) - 0.5) * 2 * o.bump;
  const bx = (i: number, j: number): number => o.x0 - o.depth + (hash2(o.seed + 77 + i * 131, j) - 0.5) * o.bump;
  const pos: number[] = []; const col: number[] = [];
  // Os quadriláteros abaixo vêm na ordem do relógio vistos de fora: a fusão inverte (a normal sai para fora).
  const quad = (a: V3, b: V3, c: V3, d: V3, cc: THREE.Color): void => {
    pos.push(...a, ...c, ...b, ...a, ...d, ...c);
    for (let q = 0; q < 6; q++) col.push(cc.r, cc.g, cc.b);
  };
  for (let i = 0; i < o.cols; i++) for (let j = 0; j < o.rows; j++) {
    if (!fill(i, j)) continue;
    const c = o.color(Z(i + 0.5), Y(j + 0.5)); const dark = c.clone().multiplyScalar(0.72);
    const F = (ii: number, jj: number): V3 => { const [z, y] = at(ii, jj); return [fx(ii, jj), y, z]; };
    const B = (ii: number, jj: number): V3 => { const [z, y] = at(ii, jj); return [bx(ii, jj), y, z]; };
    quad(F(i, j), F(i + 1, j), F(i + 1, j + 1), F(i, j + 1), c);
    quad(B(i, j), B(i, j + 1), B(i + 1, j + 1), B(i + 1, j), dark);
    if (!fill(i - 1, j)) quad(F(i, j), F(i, j + 1), B(i, j + 1), B(i, j), dark);
    if (!fill(i + 1, j)) quad(F(i + 1, j), B(i + 1, j), B(i + 1, j + 1), F(i + 1, j + 1), dark);
    if (!fill(i, j - 1)) quad(F(i, j), B(i, j), B(i + 1, j), F(i + 1, j), dark);
    if (!fill(i, j + 1)) quad(F(i, j + 1), F(i + 1, j + 1), B(i + 1, j + 1), B(i, j + 1), c.clone().multiplyScalar(0.9));
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  g.setAttribute('color', new THREE.Float32BufferAttribute(col, 3));
  g.computeVertexNormals();
  return g;
}

/** x da face de um paredão (`cliff` do kit) na altura y, entre o pé (acima do talude) e a quina de cima. */
function cliffFaceX(c: { topAt: (z: number) => number; lipAt: (z: number) => number; footAt: (z: number) => number }, talus: number, z: number, y: number): number {
  const t = Math.min(1, Math.max(0, (y - talus) / Math.max(1, c.topAt(z) - talus)));
  return c.footAt(z) + (c.lipAt(z) - c.footAt(z)) * t;
}

/** Lagoa inclinada para a pista (+X): o contorno irregular deitado, a borda de trás mais alta (`tilt` rad), e a orla de areia. */
function tiltedLagoon(k: Kit, x: number, z: number, rx: number, rz: number, tilt: number, y0: number, color: string, shore: string, seed: number): void {
  const poly = blobPoly(12, rx, rz, 0.16, seed);
  const lift = Math.tan(tilt);
  const fan = (s: number, dy: number): number[] => {
    const p: number[] = [];
    const at = (px: number, pz: number): V3 => [x + px * s, y0 + dy + (rx - px * s) * lift, z + pz * s];
    for (let i = 0; i < poly.length; i++) {
      const [ax, az] = poly[i]; const [bx, bz] = poly[(i + 1) % poly.length];
      p.push(...at(0, 0), ...at(bx, bz), ...at(ax, az));
    }
    return p;
  };
  k.add(tris(fan(1.12, -0.35)), shore);
  k.add(tris(fan(1, 0)), color);
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

/**
 * Elevador Lacerda: a torre art déco clara na frente da escarpa verde de Salvador, ligada pela passarela à Cidade Alta.
 * Leitura (onda J): a escarpa era uma placa verde lisa e a torre, da altura do casario do alto, sumia entre os
 * prédios. Agora a escarpa é encosta (rocha em sulcos, mata agarrada) e a torre, exagerada (~1,2×) e a peça mais
 * alta e mais clara, passa o casario de cima com folga — o casario fica baixo e recuado da borda.
 */
function elevadorLacerda(): Model {
  const k = new Kit();
  const talus = 8;
  const esc = cliff({ len: 176, H: 44, strata: ['#6a6a4c', '#5c6446', '#787254', '#566040'], layers: 6, seed: 21, depth: 44, batter: 0.3, gully: 4, gullyLen: 13, bay: 5, ragged: 0.08, ledge: 1.1, talus, talusColor: '#4c7c38', top: '#4a8a3a', ledgeTop: '#5a9a44', ends: 0.1, cols: 26 }, tf(-14, 0, 0));
  k.raw(esc.geo);
  // Mata agarrada na encosta (a escarpa de Salvador é verde, com a rocha aparecendo nos sulcos).
  for (let i = 0; i < 24; i++) {
    const z = -80 + (i / 23) * 160 + (hash2(24, i) - 0.5) * 6;
    if (Math.abs(z) < 12) continue;
    const y = 10 + hash2(25, i) * (esc.topAt(z) - 18);
    const r = 3.5 + hash2(26, i) * 3;
    k.raw(speckle(paint(jitter(ico(1, 0), 0.2, 260 + i), i % 3 ? '#3f7f34' : '#4f9a3c', tf(-14 + cliffFaceX(esc, talus, z, y) + r * 0.4, y, z, r * 0.9, r * 0.75, r * 1.2)), 0.08, 260 + i));
  }
  // Cidade Alta: sobrados coloridos recuados da borda, baixos (a torre manda na silhueta).
  for (let i = 0; i < 12; i++) {
    const z = -78 + i * 14 + hash2(21, i) * 3;
    if (Math.abs(z) < 14) continue;
    const top = esc.topAt(z); const x = -14 + esc.lipAt(z) - 10 - hash2(23, i) * 14;
    const h = 6 + hash2(22, i) * 3;
    k.fac('classic', 10, h, 12, HOUSE_COLORS[i % HOUSE_COLORS.length], tf(x, top + h / 2 - 0.5, z));
    k.add(hip(10.6, 2.2, 12.6), '#b8552e', tf(x, top + h - 0.5, z));
  }
  // Cidade Baixa: o prédio da base.
  k.fac('classic', 16, 12, 34, '#f0e2c0', tf(5, 6, 0));
  k.add(box(16.6, 0.8, 34.6), '#d8c49a', tf(5, 12.4, 0));
  // Torre: os dois poços colados, frisos verticais claros, faixas, a casa de máquinas mais larga e o coroamento.
  const T0 = 12; const T1 = 70; const tx = -2;
  k.fac('classic', 10, T1 - T0, 15, '#f6efdc', tf(tx, (T0 + T1) / 2, 0));
  for (const dz of [-6.6, -2.2, 2.2, 6.6]) k.add(box(1.0, T1 - T0, 1.0), '#fffaf0', tf(tx + 5.2, (T0 + T1) / 2, dz));
  for (const dz of [-7.7, 7.7]) k.add(box(10.4, T1 - T0, 0.8), '#fffaf0', tf(tx, (T0 + T1) / 2, dz));
  for (const y of [26, 40, 54]) k.add(box(10.8, 0.8, 15.8), '#e6d6b0', tf(tx, y, 0));
  k.light(box(0.3, T1 - T0 - 8, 0.6), '#ffd890', tf(tx + 5.75, (T0 + T1) / 2, 0));
  k.fac('classic', 13, 9, 19, '#f6efdc', tf(tx - 1, T1 + 4.5, 0));
  k.add(box(13.6, 0.8, 19.6), '#e6d6b0', tf(tx - 1, T1 + 9.4, 0));
  k.add(box(9, 2.6, 13), '#fffaf0', tf(tx - 1, T1 + 11.1, 0));
  k.add(box(5, 2.2, 7), '#e6d6b0', tf(tx - 1, T1 + 13.5, 0));
  k.light(box(0.25, 1.4, 13), '#ffe2a6', tf(tx + 5.6, T1 + 7.4, 0));
  // Passarela da torre até a borda da escarpa (a Praça Tomé de Sousa).
  const lip = -14 + esc.lipAt(0); const top0 = esc.topAt(0);
  const len = tx - 5 - lip + 2;
  k.fac('classic', len, 5, 8, '#efe4c8', tf(tx - 5 - len / 2 + 1, top0 - 2.5, 0));
  k.add(box(len + 0.4, 0.6, 8.4), '#d8c9a6', tf(tx - 5 - len / 2 + 1, top0 + 0.3, 0));
  return k.model(T1 + 15, false);
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

/**
 * Ponte estaiada Aracaju–Barra dos Coqueiros: torres em H, tabuleiro a 22 m. Leitura (onda J): a pista de Aracaju é
 * à noite, e de noite a ponte era a fileira de postes do tabuleiro — as torres brancas e os estais de 0,22 m sumiam
 * no escuro. Agora os estais (0,6 m) e a frente das torres acendem: o leque da estaiada se desenha em luz.
 */
function ponteAracaju(): Model {
  const k = new Kit();
  cableBridge(k, { L: 520, deckY: 22, W: 16, span: 190, pylonH: 72, type: 'H', deck: '#e4e6e8', pylon: '#f2f2ee', cable: '#e8eef6', pierStep: 40, lamp: '#ffe2a6', lit: true });
  return k.model(74, false);
}

// ───────────────────────────── Alagoas (Maragogi) ─────────────────────────────

/** Uma jangada (escala `S`): seis paus roliços, banco, mastro e a vela triangular com a faixa. Comprimento ao longo de Z. */
function oneJangada(k: Kit, x: number, z: number, yaw: number, sail: string, stripe: string, seed: number, S = 1.4): void {
  const c = Math.cos(yaw); const s = Math.sin(yaw);
  const P = (lx: number, y: number, lz: number): V3 => [x + (lx * c + lz * s) * S, y * S, z + (-lx * s + lz * c) * S];
  for (let q = 0; q < 6; q++) {
    const lx = (q - 2.5) * 0.46;
    k.rod(P(lx, 0.25, -3.8), P(lx, 0.25, 3.8), 0.26 * S, q % 2 ? '#b08a5a' : '#9a7448', 6);
  }
  k.beam(P(0, 0.55, 2.6), P(0, 0.55, -3.8), 2.8 * S, '#c9a26a', 0.12 * S);
  k.beam(P(-0.9, 0.9, 0.4), P(0.9, 0.9, 0.4), 0.4 * S, '#8a6a42', 0.6 * S);
  const mastTop = P(0.2, 10, 1.2);
  k.rod(P(0, 0.4, 1.4), mastTop, 0.14 * S, '#6a4a2a', 5);
  const boomEnd = P(0, 1.4, -5.6);
  k.rod(P(0, 1.3, 1.3), boomEnd, 0.1 * S, '#6a4a2a', 4);
  const foot = P(0.05, 1.5, 1.2);
  k.add(sheet(foot, mastTop, boomEnd), sail);
  // Faixa larga na vela, paralela à testa (o desenho que se vê do outro lado da praia).
  const mid = (a: V3, b: V3, t: number): V3 => [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t];
  const off = (v: V3, d: number): V3 => [v[0] + c * d, v[1], v[2] - s * d];
  for (const d of [0.05, -0.05]) {
    const a0 = mid(foot, boomEnd, 0.3); const a1 = mid(foot, boomEnd, 0.45);
    const b0 = mid(mastTop, boomEnd, 0.3); const b1 = mid(mastTop, boomEnd, 0.45);
    k.add(sheet(off(a0, d), off(b0, d), off(b1, d)), stripe).add(sheet(off(a0, d), off(b1, d), off(a1, d)), stripe);
  }
  if (hash2(seed, 1) > 0.4) { const b = P(0.4, 0.8, -2); k.add(box(0.7, 0.7, 0.7), '#e8e2d0', tf(b[0], b[1], b[2])); }
}

/**
 * Três jangadas na beira da praia, de velas coloridas. Leitura (onda J): eram duas, de 9,5 m, do tamanho do veleiro
 * branco do cenário (10,4 m) e confundidas com ele. Agora exageradas (1,4×: a vela a 14 m), em trio e de velas de
 * cor — a jangada de Maragogi e Maceió, não um veleiro.
 */
function jangada(): Model {
  const k = new Kit();
  oneJangada(k, 0, -9, 0.08, '#e8462e', '#f6f2e8', 51);
  oneJangada(k, -6, 1, -0.18, '#ffcf2a', '#1e88e5', 52);
  oneJangada(k, 1, 11, 0.2, '#1e88e5', '#ffd23f', 53);
  return k.model(14, true);
}

/**
 * Coqueiral: o bosque de coqueiros da beira-mar de Alagoas na areia branca, com a barraca de palha. Leitura (onda J):
 * eram 12 coqueiros soltos de 12–22 m, de copa rala — da pista, uns paus no meio dos coqueiros do cenário (7–9 m).
 * Agora 16, de 17–27 m e copa cheia, apertados num bosque de copa corrida bem acima da beira da pista.
 */
function coqueiral(): Model {
  const k = new Kit();
  k.raw(speckle(paint(jitter(sphere(1, 10, 3, 0, PI * 2, 0, PI / 2), 0.1, 60, true), '#efe2c0', tf(0, -0.5, 0, 20, 1.8, 30)), 0.04, 60));
  const n = 16;
  for (let i = 0; i < n; i++) {
    const z = (i / (n - 1) - 0.5) * 48 + (hash2(62, i) - 0.5) * 4;
    const x = (hash2(61, i) - 0.5) * 20;
    palm(k, x, z, 17 + hash2(63, i) * 10, 600 + i, 0, true);
  }
  // Coqueiros novos e moitas no pé (a mata baixa da restinga).
  for (let i = 0; i < 10; i++) k.add(jitter(ico(1, 0), 0.2, 70 + i), i % 3 ? '#4f9a3c' : '#5aa846', tf((hash2(64, i) - 0.5) * 18, 1.0, (i - 4.5) * 5, 3.2, 2.4, 3.2));
  // Barraca de palha na frente.
  for (const [dx, dz] of [[-2, -2], [2, -2], [-2, 2], [2, 2]] as const) k.add(box(0.25, 2.6, 0.25), '#7a5c36', tf(13 + dx, 1.3, 6 + dz));
  k.raw(speckle(paint(cone(4.2, 2.6, 6), '#c9a86a', tf(13, 3.9, 6)), 0.06, 66));
  return k.model(28, true, [0.75, 1.1]);
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

/** Um boneco gigante de Olinda (~6,6 m × `s`): saia comprida, a cabeçorra, braços de pano. */
function boneco(k: Kit, x: number, z: number, s: number, seed: number, yaw: number): void {
  const robe = ['#d63a3a', '#1e88e5', '#ffd23f', '#3ddc84', '#8e24aa', '#ff8c1a', '#e8407a'];
  const skin = ['#f1c7a3', '#d9a07a', '#a86b45', '#6e4630'];
  const hair = ['#1a1410', '#4a2e1a', '#e8d070', '#f4f4f4'];
  const c1 = robe[Math.floor(hash2(seed, 1) * robe.length)];
  const c2 = robe[(robe.indexOf(c1) + 1 + Math.floor(hash2(seed, 2) * (robe.length - 1))) % robe.length];
  const sk = skin[Math.floor(hash2(seed, 3) * skin.length)];
  const T = (lx: number, y: number, lz: number, sx = 1, sy = 1, sz = 1, rx = 0): THREE.Matrix4 => {
    const c = Math.cos(yaw); const sn = Math.sin(yaw);
    return tf(x + (lx * c + lz * sn) * s, y * s, z + (-lx * sn + lz * c) * s, sx * s, sy * s, sz * s, rx, yaw, 0);
  };
  k.add(box(0.25, 0.5, 0.2), '#2a2a2e', T(0, 0.25, -0.25)).add(box(0.25, 0.5, 0.2), '#2a2a2e', T(0, 0.25, 0.25));
  k.add(cyl(0.7, 1.15, 2.4, 8), c1, T(0, 1.7, 0));
  k.add(cyl(1.05, 1.05, 0.28, 8), c2, T(0, 1.0, 0));
  k.add(cyl(0.5, 0.68, 1.2, 8), c2, T(0, 3.5, 0));
  k.add(cyl(0.8, 0.6, 0.24, 8), '#f4f4ee', T(0, 4.15, 0));
  // A cabeçorra (o traço do boneco: a cabeça é um terço da altura).
  k.add(sphere(1.3, 8, 6), sk, T(0, 5.4, 0, 1, 1.12, 1));
  k.add(sphere(1.37, 8, 4, 0, PI * 2, 0, PI / 2), hair[Math.floor(hash2(seed, 4) * hair.length)], T(-0.12, 5.6, 0, 1, 1, 1));
  k.add(box(0.14, 0.3, 0.3), '#1a1a1e', T(1.25, 5.6, -0.44)).add(box(0.14, 0.3, 0.3), '#1a1a1e', T(1.25, 5.6, 0.44));
  k.add(box(0.14, 0.18, 0.66), '#b8282a', T(1.22, 4.85, 0));
  k.add(box(0.3, 0.38, 0.28), sk, T(1.34, 5.25, 0));
  for (const dz of [-1, 1]) {
    k.add(box(0.4, 2.3, 0.4), c2, T(0.15, 2.9, dz * 0.92, 1, 1, 1, dz * 0.32));
    k.add(box(0.34, 0.36, 0.34), sk, T(0.15, 1.7, dz * 1.26));
  }
  if (hash2(seed, 5) > 0.5) {
    k.add(cyl(1.6, 1.6, 0.12, 10), c1, T(0, 6.75, 0));
    k.add(cyl(0.85, 0.9, 0.9, 10), c1, T(0, 7.2, 0));
  }
}

/**
 * Bonecos gigantes de Olinda (4) e dois passistas de frevo com a sombrinha colorida. Leitura (onda J): tinham 7 m —
 * da pista, a 150 m, um risco. Agora ~10 m (1,5×: a altura de um sobrado de dois andares, o "gigante" que se vê por
 * cima do bloco), de cabeça maior e roupas de cores fortes.
 */
function bonecosOlinda(): Model {
  const k = new Kit();
  for (let i = 0; i < 4; i++) boneco(k, (i % 2) * -3.5, -11 + i * 7.4, 1.48 + hash2(91, i) * 0.12, 900 + i, (hash2(92, i) - 0.5) * 0.6);
  const umb = ['#d63a3a', '#ffd23f', '#3ddc84', '#1e88e5'];
  for (const [x, z] of [[4.5, -7], [5, 7]] as const) {
    const s = 1.3;
    k.add(box(0.3 * s, 0.9 * s, 0.35 * s), '#f4f4ee', tf(x, 0.45 * s, z)).add(box(0.5 * s, 0.75 * s, 0.55 * s), '#ff8c1a', tf(x, 1.3 * s, z));
    k.add(sphere(0.2 * s, 6, 4), '#a86b45', tf(x, 1.9 * s, z));
    k.add(box(0.06, 0.8 * s, 0.06), '#333333', tf(x, 2.1 * s, z + 0.4 * s));
    const u = paint(cone(0.9 * s, 0.45 * s, 8), '#ffffff', tf(x, 2.6 * s, z + 0.4 * s));
    k.raw(recolor(u, (cx, _y, cz) => umb[Math.floor(((Math.atan2(cz - z - 0.4 * s, cx - x) + PI) / (PI * 2)) * 4) % 4]));
  }
  return k.model(10, true);
}

// ───────────────────────────── Paraíba (João Pessoa) ─────────────────────────────

/**
 * Farol do Cabo Branco: a torre triangular modernista (três aletas, as folhas de sisal) no alto da falésia. Leitura
 * (onda J): a falésia era um bolo de caixas (uma cor por camada, de ponta a ponta); agora é a barreira de sedimento
 * do Cabo Branco — faixas vermelhas, ocre e amarelas sulcadas pela chuva, praia no pé, mata no alto —, e a torre é
 * maior (1,15×) e mais grossa. A caixa do modelo fica perto da de antes (o lugar dele, perto da largada, é o limite
 * de tempo à vista: tests/landmarks-enquadramento.test.ts).
 */
function farolCaboBranco(): Model {
  const k = new Kit();
  const H = 18; const talus = 2.5;
  const f = cliff({ len: 64, H, strata: ['#c0603a', '#d89a62', '#b8503a', '#e6b47a', '#c97a4a', '#d8a070'], layers: 7, seed: 101, depth: 28, batter: 0.2, gully: 1.8, gullyLen: 5, bay: 2, ragged: 0.05, ledge: 0.5, talus, talusColor: '#e2c99a', top: '#5f9a40', ends: 0.1, cols: 16 }, tf(12, 0, 0));
  k.raw(f.geo);
  const y0 = f.topAt(0) - 0.3;
  // Torre: prisma triangular branco afinando, com as três aletas escalonadas.
  const tx = -8; const TH = 23;
  k.add(box(11, 1.2, 11), '#e4e2da', tf(tx, y0 + 0.3, 0));
  k.add(cyl(2.4, 3.6, TH, 3), '#f8f6f0', tf(tx, y0 + 0.9 + TH / 2, 0));
  for (let q = 0; q < 3; q++) {
    const a = q * (2 * PI / 3) + PI / 3;
    for (let st = 0; st < 6; st++) {
      const h = TH / 6; const out = 5.4 - st * 0.75;
      const r = 1.6 + out / 2;
      k.add(box(out, h - 0.5, 1.3), '#fbfaf6', tf(tx + Math.cos(a) * r, y0 + 0.9 + st * h + h / 2, -Math.sin(a) * r, 1, 1, 1, 0, a, 0));
    }
  }
  const top = y0 + 0.9 + TH;
  k.add(cyl(2.8, 2.8, 0.4, 9), '#e4e2da', tf(tx, top + 0.2, 0));
  k.add(cyl(2.8, 2.8, 0.9, 9, true), '#3a3e44', tf(tx, top + 0.85, 0));
  k.light(cyl(1.3, 1.3, 2.0, 9), '#fff1b8', tf(tx, top + 1.4, 0));
  k.add(cone(1.6, 1.4, 9), '#d63a3a', tf(tx, top + 3.1, 0));
  k.blink(sphere(0.3, 6, 4), '#ff3a2a', tf(tx, top + 4.0, 0));
  // Mirante na beira, com guarda-corpo, e uns coqueiros baixos no alto.
  k.add(box(6, 0.4, 14), '#d8d0c0', tf(1, y0 + 0.2, 17));
  for (const z of [10.5, 17, 23.5]) k.add(box(0.15, 1.0, 0.15), '#e8e8e4', tf(3.9, y0 + 0.9, z));
  k.add(box(0.1, 0.1, 13), '#e8e8e4', tf(3.9, y0 + 1.4, 17));
  for (let i = 0; i < 3; i++) palm(k, -22 + i * 5, -24 + i * 22, 7 + hash2(103, i) * 2, 1030 + i, f.topAt(-24 + i * 22) - 0.3);
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

/**
 * Forte dos Reis Magos: estrela de baluartes de pedra clara sobre o arrecife, na boca do Potengi. Leitura (onda J): de
 * longe era um muro bege baixo (a planta em estrela não se vê da pista) e o passadiço de 46 m para a pista empurrava
 * o forte para longe. Agora: proa de baluarte para quem vem, escarpa escura no pé, cordão claro e as guaritas de
 * cúpula nas pontas — a fileira de sentinelas sobre o muro é o que diz "forte" de baixo —, sem o passadiço.
 */
function forteReisMagos(): Model {
  const k = new Kit();
  const R = 33;
  // Arrecife escuro e a espuma em volta.
  k.add(jitter(cyl(1, 1, 1, 14), 0.1, 111), '#5a5448', tf(0, -0.5, 0, R * 1.12, 1.6, R * 1.12));
  k.add(jitter(cyl(1, 1, 1, 16, true), 0.06, 112), '#f2f6f6', tf(0, -0.3, 0, R * 1.16, 0.9, R * 1.16));
  starFort(k, { R, n: 4, H: 11, rot: 0, stone: '#e2d6b8', base: '#a8966e', band: '#f6f2e6', court: '#cfc09a', body: '#f6f2e8', cap: '#f6f2e8', gr: 1.7 });
  // Capela e casa da guarnição no terrapleno (baixas: de fora só o telhado), e o mastro com a bandeira.
  k.add(box(12, 3.5, 9), '#f6f2e8', tf(-6, 11.75, 5)).add(gable(12, 2.2, 9, 0.3), '#b8552e', tf(-6, 13.5, 5));
  k.add(box(10, 3.5, 12), '#ece2c8', tf(-4, 11.75, -10)).add(hip(10.4, 2, 12.4), '#b8552e', tf(-4, 13.5, -10));
  k.add(cyl(0.14, 0.14, 12, 5), '#d8d8d8', tf(4, 16, 0));
  k.add(box(0.06, 2.0, 3.0), '#2a8a3a', tf(4, 21, 1.5)).add(box(0.07, 0.9, 1.4), '#ffd23f', tf(4, 21, 1.5));
  return k.model(24, true);
}

/** Ponte Newton Navarro: estaiada alta (tabuleiro a 55 m), torres em A brancas. */
function ponteNewtonNavarro(): Model {
  const k = new Kit();
  cableBridge(k, { L: 640, deckY: 55, W: 20, span: 212, pylonH: 128, type: 'A', deck: '#e8eaec', pylon: '#f4f4f0', cable: '#e4e8ec', pierStep: 52, lamp: '#ffe2a6' });
  return k.model(130, false);
}

// ───────────────────────────── Ceará (Jericoacoara) ─────────────────────────────

/**
 * Pedra Furada de Jeri: o arco de rocha escura na beira-mar, aberto até a areia. Leitura (onda J): a pedra tinha 9 m
 * de fundo para um vão de 10 m — de quem chega (a ~1 rad da frente), um bloco marrom sem furo. Agora é a lâmina fina
 * da foto (4 m), 1,3× maior, com o vão de 15 × 17 m: o furo aparece de longe e de lado.
 */
function pedraFuradaJeri(): Model {
  const k = new Kit();
  const hw = 7.5; const hh = 8.5; const cy = 7.5; const Ht = 25.5;
  const halfW = (y: number): number => 17 * Math.sqrt(Math.max(0, 1 - (Math.max(0, y) / Ht) ** 2));
  const rocks = ['#7a4a32', '#8a5a3e', '#6e4430', '#9a6a48', '#5e3a2a'].map((c) => new THREE.Color(c));
  k.raw(holedSlab({
    z0: -17, z1: 17, y0: -1, y1: Ht, cols: 17, rows: 13, x0: 2, depth: 4, bump: 0.35, seed: 121,
    solid: (z, y) => Math.abs(z) < halfW(y) && (z / hw) ** 2 + ((y - cy) / hh) ** 2 > 1,
    snap: (z, y) => {
      const e = (z / hw) ** 2 + ((y - cy) / hh) ** 2;
      if (e < 1 && y > -0.5) { const f = 1 / Math.sqrt(Math.max(1e-6, e)); z *= f; y = cy + (y - cy) * f; }
      const w = halfW(y);
      return [Math.max(-w, Math.min(w, z)), y];
    },
    color: (z, y) => rocks[((Math.floor((y + 1.2 * Math.sin(z / 4)) / 2.6) % rocks.length) + rocks.length) % rocks.length].clone().multiplyScalar(0.92 + 0.16 * hash2(122, Math.floor((z + 17) * 0.5) * 31 + Math.floor(y))),
  }));
  // Blocos caídos e a espuma no pé.
  for (let i = 0; i < 6; i++) k.add(jitter(ico(1, 0), 0.25, 122 + i), '#6e4a36', tf(4 + (hash2(123, i) - 0.5) * 10, 0.4, -20 + i * 8, 1.8 + hash2(124, i) * 1.4, 1.2, 2.0));
  return k.model(Ht, true);
}

/**
 * Duna do Pôr do Sol (Jericoacoara): a duna grande de areia dourada de crista viva, o mato rasteiro no pé. Leitura
 * (onda J): era uma meia esfera bege escurecida no pé — da pista, no entardecer, um morro marrom igual aos do
 * horizonte. Agora é duna (o kit: barlavento com as ondulações, crista, face de avalanche), mais alta (52 m, 1,2×) e
 * clara, que salta do verde e dos morros.
 */
function dunaPorDoSol(): Model {
  const k = new Kit();
  const sand = '#f6dca4'; const slip = '#f0d29a';
  k.raw(duneShape({ len: 270, H: 50, back: 110, seed: 131, sand, slip, rows: 8, ripple: 0.05, horns: 30, sinuous: 14 }, tf(-10, 0, -10)).geo);
  k.raw(duneShape({ len: 120, H: 24, back: 60, seed: 132, sand: '#f4dca6', slip: '#eed29c', rows: 6, ripple: 0.05, horns: 10 }, tf(-60, 0, 100)).geo);
  for (let i = 0; i < 10; i++) k.add(jitter(ico(1, 0), 0.25, 133 + i), '#7a9a4a', tf(82 + hash2(134, i) * 12, 0.6, -110 + i * 24, 3, 1.4, 3));
  return k.model(50, false, [0.95, 1.04]);
}

// ───────────────────────────── Piauí (Serra da Capivara) ─────────────────────────────

/**
 * Pedra Furada da Serra da Capivara: a lâmina de arenito com o arco gigante no alto do paredão. Leitura (onda J): era
 * um zigurate de caixas (uma cor por degrau) e o furo, numa pedra de 18 m de fundo, fechava para quem chega de lado.
 * Agora o paredão é rocha (sulcos, manchas verticais escuras de escorrimento, caatinga no alto e no pé) e o furo, de
 * 23 × 28 m, fica numa lâmina de 9 m — aberto até ~35° da frente.
 */
function pedraFuradaCapivara(): Model {
  const k = new Kit();
  const rocks = ['#c87a52', '#b86a48', '#d8956a', '#a85e44', '#d0a07a'];
  const wall = cliff({ len: 136, H: 38, strata: rocks, layers: 7, seed: 141, depth: 26, batter: 0.06, gully: 2.6, gullyLen: 9, bay: 3, ragged: 0.1, ledge: 0.8, talus: 7, talusColor: '#9a6a50', top: '#7f8a4a', ends: 0.3, cols: 24 }, tf(4, 0, 0));
  k.raw(wall.geo);
  // A lâmina com o furo: o maciço de topo arredondado que sobe do paredão (largo no pé, emendado nele), o arco no
  // alto com a pedra em volta grossa (não é um anel). Estratos em faixas onduladas, as do paredão, e algumas
  // manchas escuras de escorrimento descendo do alto.
  const cy = 64; const hw = 10.5; const hh = 13;
  const halfW = (y: number): number => (y < 50 ? 42 - (y - 26) * 0.2 : 37.2 * Math.sqrt(Math.max(0, 1 - ((y - 50) / 43) ** 2)));
  const base = rocks.map((c) => new THREE.Color(c));
  const streak = new THREE.Color('#6a5048');
  k.raw(holedSlab({
    z0: -42, z1: 42, y0: 26, y1: 93, cols: 30, rows: 26, x0: 0.6, depth: 9, bump: 0.5, seed: 148,
    solid: (z, y) => Math.abs(z) < halfW(y) && (z / hw) ** 2 + ((y - cy) / hh) ** 2 > 1,
    snap: (z, y) => {
      const e = (z / hw) ** 2 + ((y - cy) / hh) ** 2;
      if (e < 1) { const f = 1 / Math.sqrt(Math.max(1e-6, e)); z *= f; y = cy + (y - cy) * f; }
      const w = halfW(y);
      return [Math.max(-w, Math.min(w, z)), y];
    },
    color: (z, y) => {
      const b = Math.floor((y + 2.2 * Math.sin(z / 9)) / 6.5);
      const band = base[((b % base.length) + base.length) % base.length].clone().multiplyScalar(0.95 + 0.1 * hash2(151, Math.floor((z + 42) / 2.8)));
      const col = Math.floor((z + 42) / 2.8);
      const s = hash2(150, col);
      // Escorrimento: da crista para baixo, mais forte no alto.
      return s < 0.16 && y > 40 + 30 * hash2(152, col) ? band.lerp(streak, 0.55) : band;
    },
  }));
  // Caatinga no alto e no pé, e os blocos caídos no talude.
  for (let i = 0; i < 9; i++) k.add(jitter(ico(1, 0), 0.25, 142 + i), '#9a6a50', tf(12 + hash2(143, i) * 6, 1, -60 + i * 15, 4 + hash2(144, i) * 3, 3, 5));
  for (let i = 0; i < 12; i++) k.add(jitter(ico(1, 0), 0.2, 150 + i), i % 3 ? '#7a8a4a' : '#8a7a4a', tf(22 + hash2(151, i) * 14, 1.4, -66 + i * 12, 2.4, 1.8, 2.4));
  for (let i = 0; i < 8; i++) { const z = (i < 4 ? -1 : 1) * (34 + (i % 4) * 9); k.add(jitter(ico(1, 0), 0.2, 146 + i), '#7a8a4a', tf(-6 + hash2(147, i) * 6, wall.topAt(z) + 0.5, z, 3, 1.6, 3)); }
  return k.model(92, false, [0.8, 1.06]);
}

/**
 * Paredão com pinturas rupestres da Serra da Capivara: o abrigo de arenito claro, inclinado sobre as pinturas, e as
 * figuras em vermelho-ocre — veados, caçadores de braços erguidos, a roda de dança. Leitura (onda J): era uma caixa
 * de pedra com figuras de 1–2 m (2% da face) que de longe sumiam; agora a parede é rocha clara e as figuras, grandes
 * (veados de 8 m, gente de 5–6 m: o painel estilizado para ler da pista), cobrem ~1/8 da face.
 */
function pinturasRupestres(): Model {
  const k = new Kit();
  const talus = 2.2;
  const wall = cliff({ len: 66, H: 24, strata: ['#e4c098', '#dab088', '#e8caa4', '#d2a47c'], layers: 5, seed: 161, depth: 18, batter: -0.12, gully: 0.8, gullyLen: 8, bay: 1.2, ragged: 0.1, ledge: 0.5, talus, talusColor: '#b89070', top: '#7f8a4a', ends: 0.15, cols: 16 });
  k.raw(wall.geo);
  // As pinturas: placas finas rentes à face (na frente dela em todo o trecho da figura).
  const reds = ['#b8321e', '#a8281a'];
  const faceMax = (z0: number, z1: number, y0: number, y1: number): number => {
    let x = -Infinity;
    for (let t = 0; t <= 4; t++) for (const y of [y0, y1]) x = Math.max(x, cliffFaceX(wall, talus, z0 + ((z1 - z0) * t) / 4, y));
    return x + 0.25;
  };
  const bar = (z: number, y: number, w: number, h: number, rx: number, red: string): void => {
    const x = faceMax(z - w, z + w, y - h / 2, y + h / 2);
    k.add(box(0.12, h, w), red, tf(x, y, z, 1, 1, 1, rx, 0, 0));
  };
  const deer = (z: number, y: number, s: number, red: string): void => {
    bar(z, y + 2.6 * s, 7 * s, 2.2 * s, 0, red);
    for (const dz of [-2.8, -1.6, 1.6, 2.8]) bar(z + dz * s, y + 1.0 * s, 0.9 * s, 2.4 * s, 0, red);
    bar(z + 3.8 * s, y + 4.0 * s, 0.9 * s, 2.6 * s, 0.45, red);
    bar(z + 4.6 * s, y + 5.1 * s, 1.8 * s, 1.0 * s, 0, red);
    for (const d of [-0.5, 0.5]) bar(z + (4.3 + d) * s, y + 6.4 * s, 0.4 * s, 1.8 * s, d * 0.9, red);
  };
  const person = (z: number, y: number, s: number, arms: number, red: string): void => {
    bar(z, y + 3.0 * s, 1.1 * s, 2.6 * s, 0, red);
    bar(z, y + 4.9 * s, 1.3 * s, 1.2 * s, 0, red);
    for (const d of [-1, 1]) {
      bar(z + d * 0.5 * s, y + 0.9 * s, 0.8 * s, 2.0 * s, d * 0.3, red);
      bar(z + d * 1.2 * s, y + 3.9 * s, 0.7 * s, 2.2 * s, -d * arms, red);
    }
  };
  deer(-23, 3.5, 1.1, reds[0]); deer(-4, 9.5, 0.95, reds[1]); deer(15, 3.8, 1.15, reds[0]);
  person(-12, 11.5, 1.05, 0.9, reds[1]); person(-8.5, 12, 1.0, 0.9, reds[0]);
  person(4.5, 3.6, 1.1, 0.5, reds[0]); person(8, 3.4, 1.1, 0.5, reds[1]);
  for (let i = 0; i < 6; i++) { const a = (i / 6) * PI * 2; person(25 + Math.cos(a) * 3.2, 11.5 + Math.sin(a) * 2.2, 0.55, 1.2, reds[i % 2]); }
  // Passarela de madeira com guarda-corpo e a caatinga no pé.
  k.add(box(3, 0.3, 50), '#8a6a42', tf(6.5, 0.9, 0));
  for (let z = -24; z <= 24; z += 4) k.add(box(0.15, 1.9, 0.15), '#6a4a2a', tf(7.9, 1.6, z));
  k.add(box(0.12, 0.12, 50), '#6a4a2a', tf(7.9, 2.4, 0));
  for (let i = 0; i < 8; i++) k.add(jitter(ico(1, 0), 0.2, 165 + i), '#7a8a4a', tf(11 + hash2(166, i) * 6, 1, -26 + i * 7.4, 1.8, 1.4, 1.8));
  return k.model(27, true);
}

// ───────────────────────────── Maranhão (Lençóis) ─────────────────────────────

/**
 * Lençóis Maranhenses: dunas brancas e as lagoas azuis e verde-água entre elas. Leitura (onda J): as lagoas eram
 * discos deitados no chão — da pista (a câmera a 2 m, o marco a 150–300 m) não apareciam, e as dunas brancas sozinhas
 * liam como neve ou nuvem. Agora as dunas têm crista viva (o kit) em duas fileiras, as da frente baixas e com vãos, e
 * as lagoas ficam inclinadas para a pista (estilizado: é a faixa azul entre as dunas da foto, vista de baixo).
 */
function lagoasLencois(): Model {
  const k = new Kit();
  const sand = '#f7f5ee'; const slip = '#e8e5da';
  const dunes: Array<[number, number, number, number, number, number]> = [
    [-50, -80, 200, 30, 70, 171], [-36, 100, 170, 26, 60, 172],
    [58, -128, 84, 9, 30, 173], [56, 20, 72, 8, 28, 174], [60, 158, 60, 7, 26, 175],
  ];
  for (const [x, z, len, H, back, seed] of dunes) k.raw(duneShape({ len, H, back, seed, sand, slip, rows: 6, ripple: 0.04, horns: len * 0.06 }, tf(x, 0, z)).geo);
  const shore = '#efe6cc';
  tiltedLagoon(k, 20, -40, 32, 38, 0.2, 0.4, '#1f9ad6', shore, 180);
  tiltedLagoon(k, 20, 92, 30, 32, 0.2, 0.4, '#28b8c8', shore, 181);
  tiltedLagoon(k, 98, -136, 18, 34, 0.12, 0.3, '#2fc0b0', shore, 182);
  tiltedLagoon(k, 96, 28, 16, 22, 0.12, 0.3, '#1e88d0', shore, 183);
  return k.model(32, false, [0.92, 1.04]);
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

/**
 * Teatro Amazonas: corpo rosa com colunata branca e frontão, e a cúpula de azulejos verde, amarelo e azul. Leitura
 * (onda J): da pista, um prédio acinzentado com uma cupulazinha atrás — a cúpula (18 m) era pequena para o corpo e o
 * rosa, sob a textura da fachada, ficava cinza-lilás. Agora a cúpula tem 26 m (1,4×: o traço que define o lugar)
 * sobre um tambor mais alto, e o corpo é rosa liso com as janelas pintadas (a pista de Manaus é de dia).
 */
function teatroAmazonas(): Model {
  const k = new Kit();
  const D = 34; const L = 46; const H = 16;
  k.add(box(D + 6, 2.4, L + 4), '#d8d0c4', tf(-D / 2 + 2, 0.4, 0));
  // Paredes rosa lisas (a textura da fachada, por cima do rosa, deixava o corpo cinza-lilás de longe) com as janelas
  // pintadas em duas fileiras na frente e nos lados.
  const pink = '#f3868e';
  k.add(box(D, H, L), pink, tf(-D / 2, 1.6 + H / 2, 0));
  for (const y of [1.6 + 4.2, 1.6 + 10.6]) {
    for (let z = -20; z <= 20; z += 3.6) if (Math.abs(z) > 13) k.add(box(0.2, 2.6, 1.3), '#6a3a44', tf(0.05, y, z));
    for (let x = -D + 3; x <= -3; x += 3.6) for (const sz of [-1, 1]) k.add(box(1.3, 2.6, 0.2), '#6a3a44', tf(x, y, sz * (L / 2 + 0.05)));
  }
  k.add(box(D + 0.8, 1.0, L + 0.8), '#f6f2ea', tf(-D / 2, 1.6 + H + 0.5, 0));
  k.add(box(D - 4, 1.4, L - 4), pink, tf(-D / 2, 1.6 + H + 1.6, 0));
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
  // Tambor e cúpula de azulejos (as escamas em verde, amarelo e azul).
  const R = 13; const dx = -D / 2 - 4; const dy = y0 + H + 2.3; const dh = 6;
  k.add(cyl(R, R + 0.5, dh, 20), pink, tf(dx, dy + dh / 2, 0));
  for (let q = 0; q < 10; q++) { const a = (q / 10) * PI * 2; k.add(box(1.0, dh - 1, 1.0), '#f6f4ee', tf(dx + Math.cos(a) * (R + 0.3), dy + dh / 2, Math.sin(a) * (R + 0.3))); }
  k.add(cyl(R + 0.4, R + 0.4, 0.7, 20), '#f6f4ee', tf(dx, dy + dh + 0.1, 0));
  const tiles = ['#2f8f4a', '#f2c230', '#2a5aa8'];
  const dome = paint(sphere(R, 20, 7, 0, PI * 2, 0, PI / 2), '#2f8f4a', tf(dx, dy + dh + 0.4, 0, 1, 1.25, 1));
  k.raw(recolor(dome, (x, y, z) => {
    const a = Math.floor(((Math.atan2(z, x - dx) + PI) / (PI * 2)) * 20);
    const b = Math.floor((y - dy - dh) / 2.4);
    return tiles[((a + b) % 3 + 3) % 3];
  }));
  const dt = dy + dh + 0.4 + R * 1.25;
  k.add(cyl(1.5, 1.9, 2.8, 8), '#f6f4ee', tf(dx, dt + 1.0, 0));
  k.add(cone(1.8, 3.0, 8), '#f2c230', tf(dx, dt + 3.9, 0));
  k.light(box(0.3, 2.4, 1.4), '#ffd890', tf(0.5, y0 + 13, -8)).light(box(0.3, 2.4, 1.4), '#ffd890', tf(0.5, y0 + 13, 8));
  for (const z of [-20, 20]) { k.add(cyl(0.12, 0.16, 5, 6), '#2e3238', tf(9, 2.5, z)); k.light(sphere(0.45, 6, 4), '#fff1c8', tf(9, 5.2, z)); }
  return k.model(dt + 5, true);
}

/**
 * Barco regional da Amazônia (o gaiola): casco branco de listras, dois conveses abertos com redes coloridas. Leitura
 * (onda J): 11 m de altura e 24 de comprido — da pista, um barquinho entre os do cenário. Agora 1,3× (o exagero de um
 * marco pequeno: 31 m, 14 m com o mastro), as redes e as faixas do casco de cor forte.
 */
function barcoRegional(): Model {
  const k = new Kit();
  scaled(k, 1.3, () => {
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
  });
  return k.model(15, true);
}

// ───────────────────────────── Amapá (Macapá) ─────────────────────────────

/**
 * Monumento Marco Zero do Equador: o obelisco claro com o furo redondo no alto (o sol do equinócio passa por ele) e a
 * linha do Equador no chão. Leitura (onda J): o furo tinha 3 m numa torre de 3 m de fundo — de frente, 4 m²; de quem
 * chega, nada: um obelisco qualquer. Agora a lâmina é mais larga e fina (1,4 m) e o furo tem 6 m, com 1,4 m de pedra
 * em volta (o aro não some de longe).
 */
function marcoZeroEquador(): Model {
  const k = new Kit();
  const cy = 23.5; const r = 3.0; const Ht = 30;
  const halfW = (y: number): number => 5.2 - Math.max(0, y) * 0.035;
  const concrete = new THREE.Color('#e6e2d8');
  k.raw(holedSlab({
    z0: -5.3, z1: 5.3, y0: -1, y1: Ht, cols: 14, rows: 28, x0: 0.7, depth: 1.4, bump: 0, seed: 501,
    solid: (z, y) => Math.abs(z) < halfW(y) && z * z + (y - cy) ** 2 > r * r,
    snap: (z, y) => {
      const d = Math.hypot(z, y - cy);
      if (d < r && d > 1e-6) { z *= r / d; y = cy + (y - cy) * (r / d); }
      const w = halfW(y);
      return [Math.max(-w, Math.min(w, z)), y];
    },
    color: (_z, y) => concrete.clone().multiplyScalar(0.94 + 0.08 * hash2(502, Math.floor(y / 3))),
  }));
  k.add(cone(3.2, 2.8, 4), '#d8d4cc', tf(0, Ht + 1.4, 0, 0.5, 1, 1, 0, PI / 4, 0));
  // Praça redonda (o mostrador) e a linha do Equador atravessando (vermelha e branca).
  k.add(cyl(14, 14, 0.4, 16), '#e8e2d4', tf(0, 0.2, 0));
  for (let q = 0; q < 12; q++) { const a = (q / 12) * PI * 2; k.add(box(0.8, 0.5, 2.4), '#5a5a5a', tf(Math.cos(a) * 12, 0.45, Math.sin(a) * 12, 1, 1, 1, 0, -a, 0)); }
  for (let q = -8; q < 8; q++) k.add(box(6, 0.12, 1.2), q % 2 ? '#f4f4f0' : '#d63a2a', tf(q * 6 + 3, 0.45, 0));
  k.add(box(4, 0.8, 16), '#d63a2a', tf(18, 0.4, 0));
  k.light(cyl(r + 0.05, r + 0.05, 0.3, 12, true), '#fff1c8', tf(0, cy, 0, 1, 1, 1, 0, 0, PI / 2));
  for (const z of [-9, 9]) { k.add(cyl(0.1, 0.14, 6, 6), '#5d636b', tf(9, 3, z)); k.light(sphere(0.4, 6, 4), '#fff1c8', tf(9, 6.2, z)); }
  return k.model(33, true);
}

/**
 * Fortaleza de São José de Macapá: quatro baluartes de pedra na beira do Amazonas, as guaritas brancas nas pontas,
 * gramado e casas brancas dentro. Leitura (onda J): eram 164 m de muro de 8 m — de baixo, uma faixa cinza no chão.
 * Agora é menor e mais alta (88 m, muro de 12,5 m: estilizado para caber no olhar), com a proa de um baluarte para a
 * pista e as guaritas de cúpula sobre o muro.
 */
function fortalezaMacapa(): Model {
  const k = new Kit();
  const H = 12.5;
  const inner = starFort(k, { R: 44, n: 4, H, rot: 0, stone: '#8e8472', base: '#6a6256', band: '#e8e2d2', court: '#6a9a4a', body: '#f4f0e6', cap: '#f4f0e6', gr: 1.9 });
  void inner;
  // As casas brancas no terrapleno: de fora, o telhado de telha sobre o muro.
  for (const [x, z, w, d] of [[-12, 0, 10, 26], [10, 0, 10, 26], [0, -15, 22, 8]] as const) {
    k.add(box(w, 3.5, d), '#f4f0e6', tf(x, H + 0.75, z)).add(hip(w + 0.6, 2.6, d + 0.6), '#b8552e', tf(x, H + 2.5, z));
  }
  k.add(cyl(0.15, 0.15, 14, 5), '#d8d8d8', tf(0, H + 7, 14));
  k.add(box(0.06, 2, 3), '#ffd23f', tf(0, H + 12.5, 15.5));
  return k.model(H + 14, false);
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

/**
 * Maloca: casa redonda de palha com a cobertura cônica até perto do chão (e uma menor ao lado). Leitura (onda J): com
 * 13 m, da pista, entre as árvores do cerrado e as pedras, uma moita marrom. Agora 1,4× (a principal com 18 m: o
 * exagero de um marco pequeno), a mesma forma.
 */
function maloca(): Model {
  const k = new Kit();
  scaled(k, 1.4, () => {
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
  });
  return k.model(19, true);
}

// ───────────────────────────── Rondônia (Porto Velho) ─────────────────────────────

/**
 * Locomotiva a vapor da Madeira-Mamoré (a maria-fumaça do pátio de Porto Velho): caldeira preta, rodas vermelhas,
 * cabine e tênder, nos trilhos. Leitura (onda J): tinha 6 m de altura — da pista, a 150 m, um risco preto entre as
 * moitas. Agora 1,5× (o exagero de um marco pequeno) e com a fumaça branca saindo da chaminé, o sinal de "trem a vapor"
 * que se lê de longe.
 */
function locomotivaMamore(): Model {
  const k = new Kit();
  scaled(k, 1.5, () => {
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
  });
  // Fumaça: rolos brancos subindo da chaminé (em z = 6,6 × 1,5) e indo para trás, crescendo.
  for (let q = 0; q < 5; q++) {
    const t = q / 4; const r = 1.0 + t * 1.6;
    k.raw(speckle(paint(jitter(ico(1, 1), 0.16, 280 + q), '#f2f2ee', tf(0, 9.0 + t * 4.2, 9.6 - t * 9, r, r * 0.85, r * 1.2)), 0.03, 280 + q));
  }
  return k.model(14, true);
}

/**
 * As Três Caixas-d'Água de Porto Velho (as "Três Marias"): tanques de ferro em torres de pernas treliçadas. Leitura
 * (onda J): as pernas de 0,4 m e o contraventamento de 0,18 m sumiam de longe — os três tanques flutuavam no ar.
 * Agora as pernas têm 0,9 m e o contraventamento (anéis e as diagonais em X da face) é grosso o bastante para ler a
 * 150–250 m; os tanques, com a faixa de ferrugem e o chapéu cônico, ficam 1,1× maiores.
 */
function caixasDagua(): Model {
  const k = new Kit();
  const iron = '#4a4e55'; const leg = '#3a3d42';
  const towers: Array<[number, number, number]> = [[0, -12, 23], [-3, 0, 24.5], [0, 12, 23]];
  for (const [x, z, H] of towers) {
    const r = 3.8; const base = 3.2; const topR = 2.4; const Ht = H - 8;
    const at = (a: number, y: number): V3 => { const f = base + (topR - base) * (y / Ht); return [x + Math.cos(a) * f, y, z + Math.sin(a) * f]; };
    for (let q = 0; q < 4; q++) k.beam(at(PI / 4 + q * PI / 2, -1), at(PI / 4 + q * PI / 2, Ht), 0.9, leg);
    const rings = [4.5, 9, 13.2];
    for (const y of rings) for (let q = 0; q < 4; q++) k.beam(at(PI / 4 + q * PI / 2, y), at(PI / 4 + (q + 1) * PI / 2, y), 0.5, leg);
    // Diagonais em X entre os anéis, nas quatro faces.
    const ys = [0.5, ...rings];
    for (let i = 0; i + 1 < ys.length; i++) for (let q = 0; q < 4; q++) {
      const a0 = PI / 4 + q * PI / 2; const a1 = a0 + PI / 2;
      k.beam(at(a0, ys[i]), at(a1, ys[i + 1]), 0.32, leg).beam(at(a1, ys[i]), at(a0, ys[i + 1]), 0.32, leg);
    }
    k.add(cyl(r, r, 0.6, 12), leg, tf(x, Ht + 0.3, z));
    k.raw(recolor(paint(cyl(r, r, 7.5, 12), iron, tf(x, Ht + 0.6 + 3.75, z)), (_cx, cy, _cz, t) => (hash2(t, Math.round(cy)) > 0.82 ? '#7a4a32' : null)));
    k.add(cyl(r + 0.15, r + 0.15, 0.5, 12), '#7a4a32', tf(x, Ht + 4.3, z));
    k.add(cone(r + 0.4, 2.6, 12), leg, tf(x, Ht + 9.4, z));
    k.add(cyl(0.12, 0.12, 1.4, 5), leg, tf(x, Ht + 11.4, z));
  }
  k.add(box(15, 0.4, 38), '#a8a092', tf(-1.5, 0.2, 0));
  for (const z of [-19, 19]) { k.add(cyl(0.1, 0.14, 5, 6), '#2e3238', tf(5, 2.5, z)); k.light(sphere(0.4, 6, 4), '#fff1c8', tf(5, 5.2, z)); }
  return k.model(28, true);
}

// ───────────────────────────── Acre (Rio Branco) ─────────────────────────────

/**
 * Gameleira (Rio Branco): a figueira centenária da beira do rio — raízes tabulares, o tronco grosso de galhos em
 * leque e a copa muito mais larga que alta. Leitura (onda J): com 30 × 38 m, da pista, mais uma árvore de copa de
 * guarda-chuva entre as do cenário. Agora a copa tem ~60 m de largura e 39 de altura (o exagero da árvore-símbolo) e
 * o tronco, 7 m de diâmetro no pé.
 */
function gameleira(): Model {
  const k = new Kit();
  const bark = '#8a7a66';
  // Raízes tabulares: aletas do tronco até o chão.
  for (let q = 0; q < 9; q++) {
    const a = (q / 9) * PI * 2 + hash2(231, q) * 0.3;
    const r = 7.5 + hash2(232, q) * 3;
    k.add(tris([
      0, 0, 0, Math.cos(a) * r, 0, Math.sin(a) * r, 0, 9, 0,
      0, 0, 0, 0, 9, 0, Math.cos(a) * r, 0, Math.sin(a) * r,
    ]), bark, tf(0, -0.3, 0));
    k.add(box(1.0, 8, r * 0.8), bark, tf(Math.cos(a) * r * 0.35, 3, Math.sin(a) * r * 0.35, 1, 1, 1, 0, -a + PI / 2, 0).multiply(tf(0, 0, 0, 1, 1, 1, 0.38, 0, 0)));
  }
  k.add(cyl(2.4, 3.6, 18, 9), bark, tf(0, 9, 0));
  // Galhos grossos saindo em leque, quase horizontais (a copa de figueira abre para os lados).
  const tops: V3[] = [];
  for (let q = 0; q < 7; q++) {
    const a = (q / 7) * PI * 2 + 0.3;
    const t: V3 = [Math.cos(a) * 15, 24 + hash2(233, q) * 4, Math.sin(a) * 15];
    k.rod([0, 16, 0], t, 1.1, bark, 6);
    tops.push(t);
  }
  // Copa: massas achatadas de verdes diferentes — a de cima, as dos galhos e o anel de fora, baixo.
  const greens = ['#3f7f34', '#4a8f3a', '#356f2e', '#58a044'];
  tops.forEach(([x, y, z], q) => k.raw(speckle(paint(jitter(ico(1, 1), 0.14, 234 + q), greens[q % 4], tf(x, y + 3, z, 12, 6.5, 12)), 0.08, 234 + q)));
  k.raw(speckle(paint(jitter(ico(1, 1), 0.12, 240), '#4a8f3a', tf(0, 32, 0, 16, 7, 16)), 0.08, 240));
  for (let q = 0; q < 8; q++) {
    const a = (q / 8) * PI * 2 + 0.2;
    k.raw(speckle(paint(jitter(ico(1, 0), 0.15, 241 + q), greens[(q + 1) % 4], tf(Math.cos(a) * 22, 22, Math.sin(a) * 22, 8, 4.5, 8)), 0.08, 241 + q));
  }
  // Grade da praça em volta do tronco.
  for (let q = 0; q < 14; q++) { const a = (q / 14) * PI * 2; k.add(box(0.12, 0.9, 0.12), '#3a3d42', tf(Math.cos(a) * 12, 0.45, Math.sin(a) * 12)); }
  return k.model(39, true, [0.72, 1.08]);
}

/**
 * Palácio Rio Branco: neoclássico branco e creme, pórtico de colunas em dois andares, frontão e platibanda, e a
 * bandeira do Acre no mastro do alto. Leitura (onda J): de longe era "um palácio branco" qualquer — a bandeira, de
 * 3 m, sumia. Agora a bandeira tem 7,2 × 4,8 m (estilizada: a cor-assinatura do lugar, verde e amarela em diagonal
 * com a estrela vermelha) num mastro de 13 m sobre o telhado.
 */
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
  // Mastro no alto, atrás do frontão, com a bandeira do Acre: o triângulo amarelo de cima (com a estrela vermelha) e o
  // verde de baixo, divididos pela diagonal; pano de dois lados.
  const base = 1.1 + H + 2.3; const mx = -6; const top = base + 13;
  k.add(cyl(0.16, 0.2, 13, 6), '#d8d8d8', tf(mx, base + 6.5, 0));
  k.add(sphere(0.3, 6, 4), '#c9a040', tf(mx, top + 0.2, 0));
  const fz = 0.2; const fw = 7.2; const fh = 4.8; const fy = top - 0.4;
  const P = (z: number, y: number): V3 => [mx, fy - y, fz + z];
  k.add(sheet(P(0, 0), P(fw, 0), P(0, fh)), '#ffd23f');
  k.add(sheet(P(fw, 0), P(fw, fh), P(0, fh)), '#2a8a3a');
  k.add(new THREE.OctahedronGeometry(0.9, 0), '#d63a2a', tf(mx, fy - 1.3, fz + 1.5, 0.3, 1, 1));
  for (const z of [-14, 14]) k.light(box(0.2, 1.4, 0.8), '#ffe2a6', tf(0.2, 1.1 + 4, z));
  for (const z of [-18, 18]) { k.add(cyl(0.12, 0.16, 5, 6), '#2e3238', tf(8, 2.5, z)); k.light(sphere(0.45, 6, 4), '#fff1c8', tf(8, 5.2, z)); }
  return k.model(top + 1, true);
}

// ───────────────────────────── Tocantins (Jalapão) ─────────────────────────────

/** Serra do Espírito Santo: paredão de mesa laranja em estratos, talude de cerrado (escala grande). */
function serraEspiritoSanto(): Model {
  const k = new Kit();
  mesa(k, { rx: 220, rz: 800, H: 300, talus: 120, seed: 251, n: 18, rock: ['#d0703a', '#c0602e', '#e08848', '#b85a34', '#d88a50'], top: '#8a8a4a', slope: '#a08850', step: 18 });
  return k.model(240, false, [0.82, 1.05]);
}

/**
 * Dunas do Jalapão: as dunas douradas de crista viva ao pé da serra, com a vereda de buritis e capim no pé. Leitura
 * (onda J): eram meias esferas laranja (#e8964a, l 0,57 com o sombreado) no chão laranja do deserto (#d9964f–#e9b464):
 * a cor sumia no bioma. Agora a areia é dourada e clara (l ~0,75), a duna tem crista (o kit) e, no pé, a faixa verde
 * da vereda com os buritis — o contraste que separa a duna do chão.
 */
function dunasJalapao(): Model {
  const k = new Kit();
  const sand = '#fae0b0'; const slip = '#f4d29c';
  k.raw(duneShape({ len: 220, H: 46, back: 90, seed: 261, sand, slip, rows: 8, ripple: 0.05, horns: 20, sinuous: 10 }, tf(-14, 0, 0)).geo);
  k.raw(duneShape({ len: 120, H: 28, back: 60, seed: 262, sand: '#f8dcaa', slip: '#f2cc94', rows: 6, ripple: 0.05, horns: 10 }, tf(-40, 0, -150)).geo);
  k.raw(duneShape({ len: 110, H: 26, back: 56, seed: 263, sand: '#fae0b0', slip: '#f2ce98', rows: 6, ripple: 0.05, horns: 10 }, tf(-34, 0, 145)).geo);
  // A vereda: o capim verde e as moitas ao longo do pé das dunas, e os buritis.
  for (let i = 0; i < 20; i++) k.add(jitter(ico(1, 0), 0.25, 264 + i), i % 3 ? '#4f9a3c' : '#6aa846', tf(76 + hash2(265, i) * 12, 1.0, -180 + i * 19, 5, 3.0, 6.5));
  for (let i = 0; i < 7; i++) palm(k, 80 + hash2(266, i) * 12, -150 + i * 50, 15 + hash2(267, i) * 5, 2660 + i, 0, true);
  return k.model(46, false, [0.92, 1.04]);
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
  // 21 m de altura: a 120–330 m (far) lia pequeno; perto, de frente para quem chega.
  forte_reis_magos: def(forteReisMagos, 'near', 'sea', 1),
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
  // Forte baixo (16 m) e largo: a 120–330 m (far) sumia; perto, de frente para quem chega.
  fortaleza_macapa: def(fortalezaMacapa, 'near', 'any', 1),
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
