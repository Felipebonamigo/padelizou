// Marcos turísticos do mundo (onda G, docs/PISTAS-TURISMO.md): um modelo procedural por id de
// src/core/data/places.ts — EUA, Japão, Europa, África do Sul, Austrália, Escandinávia e Mediterrâneo.
//
// Convenção dos modelos (a mesma dos prédios e placas do cenário): base no chão em y = 0 (fundações e
// pedras podem entrar no chão), centro em x = z = 0, FRENTE EM +X (o lado que olha a pista; o layout gira
// π do outro lado — girar, nunca espelhar, senão as letras das placas saem invertidas). O que é comprido
// (pontes, represa, paredões, serras) corre em Z, ao longo da pista, para ser visto de lado.
// Escala real em metros nos marcos `near` e `far`; as montanhas do horizonte (`skyline`) são reduzidas
// (Fuji com 215 m, Matterhorn com 300 m) para caberem antes da névoa — a silhueta é a de verdade.
// Faces planas e cor chapada (geom.ts); luz que acende à noite vai no material `glow` (e `beacon`, que
// pisca; `cone`, o facho, só na noite fechada). Sem Math.random: variação por hash. Sem marca real: a
// placa de Las Vegas diz só "WELCOME".
import * as THREE from 'three';
import { hash2, hash3, valueNoise } from '../../noise';
import { box, cone, cyl, dodeca, frond, gable, hip, ico, merge, paint, shadeY, speckle, sphere, tf, tintUp, tris, type Geo, type Model, type ModelPart } from '../geom';
import { landmarkPart } from './kit';
import type { LandmarkRegistry } from './types';

type C = THREE.ColorRepresentation;
type P3 = readonly [number, number, number];

// ───────────────────────────── Kit de montagem ─────────────────────────────

const UP = new THREE.Vector3(0, 1, 0);
/** Profundidade máxima (m) de fundação sob o chão. */
export const FOUNDATION_M = 2.7;

/** Matriz que leva uma peça de altura 1 (eixo Y, centrada) para o segmento a → b. */
function along(a: P3, b: P3): THREE.Matrix4 {
  const d = new THREE.Vector3(b[0] - a[0], b[1] - a[1], b[2] - a[2]);
  const len = d.length();
  const q = new THREE.Quaternion().setFromUnitVectors(UP, d.normalize());
  return new THREE.Matrix4().compose(new THREE.Vector3((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, (a[2] + b[2]) / 2), q, new THREE.Vector3(1, len, 1));
}

/** Acumula as peças de um modelo por material. */
class Kit {
  readonly flat: Geo[] = [];
  readonly glow: Geo[] = [];
  readonly beacon: Geo[] = [];
  readonly beams: Geo[] = [];

  /** Peça lisa (sombreada). */
  f(g: Geo, c: C, m?: THREE.Matrix4): this { this.flat.push(paint(g, c, m)); return this; }
  /** Peça de luz (acende à noite). */
  l(g: Geo, c: C, m?: THREE.Matrix4): this { this.glow.push(paint(g, c, m)); return this; }
  /** Luz de balizamento (pisca). */
  b(g: Geo, c: C, m?: THREE.Matrix4): this { this.beacon.push(paint(g, c, m)); return this; }
  /** Geometria já pintada (cor por face, deformada). */
  raw(g: Geo, glow = false): this { (glow ? this.glow : this.flat).push(g); return this; }
  /** Facho de luz (material `cone`: só posição e normal, sem cor). */
  beam(g: Geo, m?: THREE.Matrix4): this {
    const n = g.index ? g.toNonIndexed() : g.clone();
    g.dispose();
    n.deleteAttribute('uv');
    if (m) n.applyMatrix4(m);
    n.computeVertexNormals();
    this.beams.push(n);
    return this;
  }
  /** Barra de seção w × d de a até b (treliças, cabos, pernas). */
  bar(c: C, a: P3, b: P3, w: number, d = w, glow = false): this {
    const g = paint(box(w, 1, d), c, along(a, b));
    (glow ? this.glow : this.flat).push(g);
    return this;
  }
  /** Cilindro de a (raio r0) até b (raio r1). */
  rod(c: C, a: P3, b: P3, r0: number, r1 = r0, seg = 6): this {
    this.flat.push(paint(cyl(r1, r0, 1, seg), c, along(a, b)));
    return this;
  }
  /** Junta outro kit com uma transformação (grupos montados no próprio referencial). */
  add(o: Kit, m: THREE.Matrix4): this {
    const pairs: Array<[Geo[], Geo[]]> = [[o.flat, this.flat], [o.glow, this.glow], [o.beacon, this.beacon], [o.beams, this.beams]];
    for (const [src, dst] of pairs) for (const g of src) { g.applyMatrix4(m); dst.push(g); }
    return this;
  }
  model(height: number, opt: { shadow?: boolean; blob?: number; shade?: readonly [number, number] } = {}): Model {
    // Fundações e rochas param a 2,7 m sob o chão (a convenção dos marcos): o resto seria só triângulo enterrado.
    for (const list of [this.flat, this.glow, this.beacon, this.beams]) {
      for (const g of list) {
        const p = g.attributes.position as THREE.BufferAttribute;
        for (let i = 0; i < p.count; i++) if (p.getY(i) < -FOUNDATION_M) p.setY(i, -FOUNDATION_M);
      }
    }
    const parts: ModelPart[] = [];
    const [s0, s1] = opt.shade ?? [0.8, 1.05];
    if (this.flat.length) parts.push({ geometry: shadeY(merge(this.flat), 0, height, s0, s1), mat: 'flat', shadow: opt.shadow ?? false });
    if (this.glow.length) parts.push({ geometry: merge(this.glow), mat: 'glow' });
    if (this.beacon.length) parts.push({ geometry: merge(this.beacon), mat: 'beacon' });
    if (this.beams.length) parts.push({ geometry: merge(this.beams), mat: 'cone' });
    return { parts, blob: opt.blob ?? 0 };
  }
}

/** Pinta cada face pela função (centro da face e |normal.y|): neve por altura, manchas, faixas de rocha. */
function faceColor(g: Geo, fn: (x: number, y: number, z: number, up: number, face: number) => C): Geo {
  const p = g.attributes.position as THREE.BufferAttribute;
  const c = g.attributes.color as THREE.BufferAttribute;
  const a = new THREE.Vector3(); const b = new THREE.Vector3(); const d = new THREE.Vector3();
  const col = new THREE.Color();
  for (let i = 0; i + 2 < p.count; i += 3) {
    a.fromBufferAttribute(p, i); b.fromBufferAttribute(p, i + 1); d.fromBufferAttribute(p, i + 2);
    const cx = (a.x + b.x + d.x) / 3; const cy = (a.y + b.y + d.y) / 3; const cz = (a.z + b.z + d.z) / 3;
    const n = b.sub(a).cross(d.sub(a)).normalize();
    col.set(fn(cx, cy, cz, Math.abs(n.y), i / 3));
    for (let k = 0; k < 3; k++) c.setXYZ(i + k, col.r, col.g, col.b);
  }
  return g;
}

/** Desvio por hash da posição arredondada (em metros): vértices coincidentes andam juntos, a malha não abre. */
function roughen(g: Geo, amp: number, seed: number, keepBottom = true): Geo {
  const p = g.attributes.position as THREE.BufferAttribute;
  for (let i = 0; i < p.count; i++) {
    const x = p.getX(i); const y = p.getY(i); const z = p.getZ(i);
    const kx = Math.round(x * 8); const ky = Math.round(y * 8); const kz = Math.round(z * 8);
    const dx = (hash3(kx + seed * 7919, ky, kz) - 0.5) * 2 * amp;
    const dy = (hash3(kx, ky + seed * 131, kz + 7) - 0.5) * 2 * amp;
    const dz = (hash3(kx + 3, ky, kz + seed * 977) - 0.5) * 2 * amp;
    p.setXYZ(i, x + dx, keepBottom && y <= 0.01 ? y : y + dy, z + dz);
  }
  return g;
}

/** Ruído de valor 2D suave em [-1, 1] (grade inteira, interpolação suavizada): neve em estrias, relevo, manchas. */
function noise2(seed: number, x: number, z: number): number {
  const i = Math.floor(x); const j = Math.floor(z);
  const fx = x - i; const fz = z - j;
  const sx = fx * fx * (3 - 2 * fx); const sz = fz * fz * (3 - 2 * fz);
  const h = (a: number, b: number): number => hash3(a, b, seed) * 2 - 1;
  const a = h(i, j) + (h(i + 1, j) - h(i, j)) * sx;
  const b = h(i, j + 1) + (h(i + 1, j + 1) - h(i, j + 1)) * sx;
  return a + (b - a) * sz;
}

/** Interpolação linear numa tabela [x, y] crescente em x (fora dela, o valor da ponta). */
function table(pts: ReadonlyArray<readonly [number, number]>, x: number): number {
  if (x <= pts[0][0]) return pts[0][1];
  for (let i = 1; i < pts.length; i++) {
    if (x <= pts[i][0]) { const [x0, y0] = pts[i - 1]; const [x1, y1] = pts[i]; return y0 + ((y1 - y0) * (x - x0)) / (x1 - x0); }
  }
  return pts[pts.length - 1][1];
}

const smooth = (a: number, b: number, x: number): number => { const t = Math.min(1, Math.max(0, (x - a) / (b - a))); return t * t * (3 - 2 * t); };

/** Superfície paramétrica (u, v em [0, 1]) em triângulos soltos. */
function grid(nu: number, nv: number, fn: (u: number, v: number) => P3): Geo {
  const pts: P3[] = [];
  for (let j = 0; j <= nv; j++) for (let i = 0; i <= nu; i++) pts.push(fn(i / nu, j / nv));
  const P = (i: number, j: number): P3 => pts[j * (nu + 1) + i];
  const out: number[] = [];
  for (let j = 0; j < nv; j++) {
    for (let i = 0; i < nu; i++) {
      const a = P(i, j); const b = P(i + 1, j); const c = P(i + 1, j + 1); const d = P(i, j + 1);
      out.push(...a, ...b, ...c, ...a, ...c, ...d);
    }
  }
  return tris(out);
}

/** Triângulos com as duas faces (para o `glow`, que só desenha a frente). */
function tris2(p: number[]): Geo {
  const out = [...p];
  for (let i = 0; i + 8 < p.length; i += 9) out.push(p[i], p[i + 1], p[i + 2], p[i + 6], p[i + 7], p[i + 8], p[i + 3], p[i + 4], p[i + 5]);
  return tris(out);
}

/**
 * Espelha as peças em Z e desvira o sentido dos triângulos (o `glow` só desenha a frente). Para os marcos de
 * `side: 'sea'`: o mar fica sempre à direita, e quem chega vê o modelo pela ponta −Z.
 */
function mirrorZ(geos: readonly Geo[]): void {
  const m = new THREE.Matrix4().makeScale(1, 1, -1);
  for (const g of geos) {
    g.applyMatrix4(m);
    for (const name of ['position', 'color', 'normal']) {
      const a = g.getAttribute(name) as THREE.BufferAttribute | undefined;
      if (!a) continue;
      for (let i = 0; i + 2 < a.count; i += 3) {
        const x = a.getX(i + 1); const y = a.getY(i + 1); const z = a.getZ(i + 1);
        a.setXYZ(i + 1, a.getX(i + 2), a.getY(i + 2), a.getZ(i + 2));
        a.setXYZ(i + 2, x, y, z);
      }
    }
  }
}

/** Tabuleiro (pista, deque) que segue um caminho: largura w para os lados, espessura t para baixo. */
function ribbon(path: readonly P3[], w: number, t: number): Geo {
  const L: P3[] = []; const R: P3[] = [];
  for (let i = 0; i < path.length; i++) {
    const a = path[Math.max(0, i - 1)]; const b = path[Math.min(path.length - 1, i + 1)];
    const tx = b[0] - a[0]; const tz = b[2] - a[2]; const n = Math.hypot(tx, tz) || 1;
    const lx = (-tz / n) * w / 2; const lz = (tx / n) * w / 2;
    const p = path[i];
    L.push([p[0] + lx, p[1], p[2] + lz]); R.push([p[0] - lx, p[1], p[2] - lz]);
  }
  const dn = (q: P3): P3 => [q[0], q[1] - t, q[2]];
  const out: number[] = [];
  const quad = (a: P3, b: P3, c: P3, d: P3): void => { out.push(...a, ...b, ...c, ...a, ...c, ...d); };
  for (let i = 0; i + 1 < path.length; i++) {
    quad(L[i], R[i], R[i + 1], L[i + 1]);
    quad(dn(L[i]), dn(L[i + 1]), dn(R[i + 1]), dn(R[i]));
    quad(L[i], L[i + 1], dn(L[i + 1]), dn(L[i]));
    quad(R[i], dn(R[i]), dn(R[i + 1]), R[i + 1]);
  }
  const e = path.length - 1;
  quad(L[0], dn(L[0]), dn(R[0]), R[0]);
  quad(L[e], R[e], dn(R[e]), dn(L[e]));
  return tris(out);
}

function lathe(pts: ReadonlyArray<readonly [number, number]>, seg: number, phase = 0): Geo {
  return new THREE.LatheGeometry(pts.map(([r, y]) => new THREE.Vector2(r, y)), seg, phase);
}

function shapeOf(pts: ReadonlyArray<readonly [number, number]>, s = 1): THREE.Shape {
  return new THREE.Shape(pts.map(([x, y]) => new THREE.Vector2(x * s, y * s)));
}

/** Extrusão de um contorno do plano XY, centrada em z (espessura `depth`). */
function extrude(shape: THREE.Shape, depth: number, curveSegments = 6): Geo {
  return new THREE.ExtrudeGeometry(shape, { depth, bevelEnabled: false, curveSegments }).translate(0, 0, -depth / 2);
}

/**
 * Contorno (plano XY) de uma parede w × h com vãos em arco pleno que descem até o chão:
 * cada vão é [centro x, largura, altura até o topo do arco]. Base em y = 0.
 */
function archOutline(w: number, h: number, arches: ReadonlyArray<readonly [number, number, number]>): THREE.Shape {
  const s = new THREE.Shape();
  s.moveTo(-w / 2, 0);
  for (const [cx, aw, ah] of [...arches].sort((a, b) => a[0] - b[0])) {
    const r = aw / 2;
    s.lineTo(cx - r, 0);
    s.lineTo(cx - r, ah - r);
    s.absarc(cx, ah - r, r, Math.PI, 0, true);
    s.lineTo(cx + r, 0);
  }
  s.lineTo(w / 2, 0); s.lineTo(w / 2, h); s.lineTo(-w / 2, h); s.lineTo(-w / 2, 0);
  return s;
}

/** Furo em arco pleno (janela, sineira) para `Shape.holes`. */
function archHole(cx: number, y0: number, w: number, h: number): THREE.Path {
  const r = w / 2; const p = new THREE.Path();
  p.moveTo(cx - r, y0); p.lineTo(cx + r, y0); p.lineTo(cx + r, y0 + h - r);
  p.absarc(cx, y0 + h - r, r, 0, Math.PI, false);
  p.lineTo(cx - r, y0);
  return p;
}

/** Disco/elipse em triângulos (leque) no plano XY. */
function fan2d(cx: number, cy: number, rx: number, ry: number, n: number, z = 0): number[] {
  const out: number[] = [];
  for (let k = 0; k < n; k++) {
    const a0 = (k / n) * Math.PI * 2; const a1 = ((k + 1) / n) * Math.PI * 2;
    out.push(cx, cy, z, cx + Math.cos(a0) * rx, cy + Math.sin(a0) * ry, z, cx + Math.cos(a1) * rx, cy + Math.sin(a1) * ry, z);
  }
  return out;
}

// ───────────────────────────── Letras de blocos ─────────────────────────────

const FONT: Readonly<Record<string, readonly string[]>> = {
  A: ['.###.', '#...#', '#...#', '#####', '#...#', '#...#', '#...#'],
  C: ['.####', '#....', '#....', '#....', '#....', '#....', '.####'],
  D: ['####.', '#...#', '#...#', '#...#', '#...#', '#...#', '####.'],
  E: ['#####', '#....', '#....', '####.', '#....', '#....', '#####'],
  G: ['.####', '#....', '#....', '#.###', '#...#', '#...#', '.###.'],
  I: ['#####', '..#..', '..#..', '..#..', '..#..', '..#..', '#####'],
  K: ['#...#', '#..#.', '#.#..', '##...', '#.#..', '#..#.', '#...#'],
  L: ['#....', '#....', '#....', '#....', '#....', '#....', '#####'],
  M: ['#...#', '##.##', '#.#.#', '#.#.#', '#...#', '#...#', '#...#'],
  N: ['#...#', '##..#', '#.#.#', '#.#.#', '#..##', '#...#', '#...#'],
  O: ['.###.', '#...#', '#...#', '#...#', '#...#', '#...#', '.###.'],
  R: ['####.', '#...#', '#...#', '####.', '#.#..', '#..#.', '#...#'],
  S: ['.####', '#....', '#....', '.###.', '....#', '....#', '####.'],
  T: ['#####', '..#..', '..#..', '..#..', '..#..', '..#..', '..#..'],
  U: ['#...#', '#...#', '#...#', '#...#', '#...#', '#...#', '.###.'],
  W: ['#...#', '#...#', '#...#', '#.#.#', '#.#.#', '##.##', '#...#'],
  '6': ['..##.', '.#...', '#....', '####.', '#...#', '#...#', '.###.'],
};

/** Texto de blocos 5 × 7 no plano XY (lido de +Z), centrado em (0, 0), altura h, espessura d. */
function blockText(s: string, h: number, d: number): Geo[] {
  const px = h / 7; const out: Geo[] = [];
  const total = (s.length * 6 - 1) * px;
  for (let li = 0; li < s.length; li++) {
    const g = FONT[s[li]];
    if (!g) continue;
    const x0 = -total / 2 + li * 6 * px;
    for (let r = 0; r < 7; r++) {
      let c = 0;
      while (c < 5) {
        if (g[r][c] !== '#') { c++; continue; }
        let e = c;
        while (e < 5 && g[r][e] === '#') e++;
        const w = (e - c) * px;
        out.push(box(w, px, d).translate(x0 + c * px + w / 2, h / 2 - (r + 0.5) * px, 0));
        c = e;
      }
    }
  }
  return out;
}

/** Texto nas duas faces de uma placa (cada face lida do próprio lado), a ±z0 do plano da placa. */
function signText(k: Kit, s: string, h: number, color: C, m: THREE.Matrix4, z0: number, glow = false): void {
  for (const back of [false, true]) {
    const mm = m.clone().multiply(tf(0, 0, 0, 1, 1, 1, 0, back ? Math.PI : 0, 0)).multiply(tf(0, 0, z0));
    for (const g of blockText(s, h, 0.05)) (glow ? k.l(g, color, mm) : k.f(g, color, mm));
  }
}

const SQ = Math.SQRT2;

/** Treliça de 4 montantes (seção quadrada): níveis [y, meia largura]; cintas e diagonais em X opcionais. */
function latticeTower(k: Kit, levels: ReadonlyArray<readonly [number, number]>, colorOf: (i: number) => C, legW: (i: number) => number, xBrace = true, glowEdge?: C): void {
  for (let i = 0; i + 1 < levels.length; i++) {
    const [y0, h0] = levels[i]; const [y1, h1] = levels[i + 1];
    const c = colorOf(i); const w = legW(i);
    for (let q = 0; q < 4; q++) {
      const sx = q === 0 || q === 3 ? 1 : -1; const sz = q < 2 ? 1 : -1;
      k.bar(c, [sx * h0, y0, sz * h0], [sx * h1, y1, sz * h1], w);
      if (glowEdge) k.bar(glowEdge, [sx * (h0 + w * 0.6), y0, sz * (h0 + w * 0.6)], [sx * (h1 + w * 0.6), y1, sz * (h1 + w * 0.6)], w * 0.3, w * 0.3, true);
    }
    const corners: P3[] = [[h1, y1, h1], [-h1, y1, h1], [-h1, y1, -h1], [h1, y1, -h1]];
    for (let q = 0; q < 4; q++) k.bar(c, corners[q], corners[(q + 1) % 4], w * 0.6);
    if (!xBrace) continue;
    const low: P3[] = [[h0, y0, h0], [-h0, y0, h0], [-h0, y0, -h0], [h0, y0, -h0]];
    for (let q = 0; q < 4; q++) {
      k.bar(c, low[q], corners[(q + 1) % 4], w * 0.45);
      k.bar(c, low[(q + 1) % 4], corners[q], w * 0.45);
    }
  }
}

// ───────────────────────────── Estados Unidos ─────────────────────────────

/** Escudo da US Route 66 (contorno), largura ≈ 4,4 m. */
function shield66(s = 1): THREE.Shape {
  return shapeOf([
    [-2.2, 2.25], [-1.25, 2.4], [0, 2.1], [1.25, 2.4], [2.2, 2.25], [2.1, 0.9], [1.75, -0.6], [1.05, -1.75], [0, -2.4],
    [-1.05, -1.75], [-1.75, -0.6], [-2.1, 0.9],
  ], s);
}

/** Placa da Rota 66: escudo gigante num poste, com uma bomba de gasolina antiga ao lado. */
function placaRota66(): Model {
  const k = new Kit();
  k.f(box(1.4, 0.5, 1.4), '#a8a49a', tf(0, 0.25, 0));
  k.f(cyl(0.16, 0.2, 8.4, 8), '#5f646b', tf(0, 4.4, 0));
  const s = new Kit();
  s.f(extrude(shield66(1.04), 0.2), '#15171b');
  s.f(extrude(shield66(0.9), 0.3), '#f7f5ee');
  const m = new THREE.Matrix4();
  signText(s, 'US', 0.9, '#15171b', tf(0, 1.15, 0), 0.17);
  signText(s, '66', 1.9, '#15171b', tf(0, -0.45, 0), 0.17);
  // O escudo olha a pista (+X), virado 30° para quem vem (+Z).
  k.add(s, tf(0.15, 8.6, 0, 1, 1, 1, 0, Math.PI / 2 - 0.52, 0).multiply(m));
  // Bomba de gasolina dos anos 50 (vermelha, globo aceso).
  k.f(box(2.6, 0.25, 3.2), '#b8b4aa', tf(1.5, 0.12, 4.2));
  k.f(box(0.8, 2.1, 0.7), '#c8282a', tf(1.5, 1.3, 4.2));
  k.f(box(0.82, 0.5, 0.72), '#f2f0ea', tf(1.5, 1.6, 4.2));
  k.f(box(0.84, 0.12, 0.74), '#2a2a2a', tf(1.5, 2.3, 4.2));
  k.l(sphere(0.32, 8, 6), '#fff2d8', tf(1.5, 2.65, 4.2));
  k.f(cyl(0.05, 0.05, 1.2, 4), '#1a1a1a', tf(1.95, 1.2, 4.55, 1, 1, 1, 0.5, 0, 0));
  return k.model(13, { shadow: true, blob: 2 });
}

/** Lanchonete "diner" de vagão inox com faixas vermelhas, letreiro de neon no teto e a seta luminosa. */
function dinerNeon(): Model {
  const k = new Kit();
  const steel = '#c9ced6'; const red = '#d22a2a';
  k.f(box(16, 0.12, 32), '#5c5e62', tf(4, 0.06, -2));
  k.f(box(6.6, 0.7, 16.4), '#3a3b40', tf(0, 0.35, 0));
  k.f(box(6.5, 3.0, 16), steel, tf(0, 2.2, 0));
  for (const z of [-8, 8]) {
    k.f(cyl(3.25, 3.25, 3.0, 12), steel, tf(0, 2.2, z));
    k.f(cyl(3.28, 3.28, 0.35, 12), red, tf(0, 1.25, z));
    k.f(cyl(3.28, 3.28, 0.3, 12), red, tf(0, 3.45, z));
    k.l(cyl(3.27, 3.27, 1.0, 12, true), '#ffd890', tf(0, 2.45, z));
  }
  k.f(box(6.56, 0.35, 16.05), red, tf(0, 1.25, 0));
  k.f(box(6.56, 0.3, 16.05), red, tf(0, 3.45, 0));
  // Janelas em fita (acesas), com montantes.
  for (const sx of [1, -1]) {
    k.l(box(0.1, 1.0, 14.5), '#ffd890', tf(sx * 3.26, 2.45, 0));
    for (let z = -7; z <= 7; z += 1.75) k.f(box(0.14, 1.04, 0.12), '#e8ecf2', tf(sx * 3.29, 2.45, z));
  }
  // Teto abaulado.
  k.f(cyl(3.45, 3.45, 22.6, 14), '#efe9d8', tf(0, 3.62, 0, 1, 1, 0.28, Math.PI / 2, 0, 0));
  // Entrada com marquise.
  k.f(box(2.0, 3.6, 3.6), steel, tf(3.9, 1.8, 0));
  k.f(box(2.6, 0.25, 4.4), red, tf(4.2, 3.75, 0));
  k.l(box(0.08, 2.2, 1.4), '#ffe6b0', tf(4.92, 1.5, 0));
  // Letreiro no teto: DINER em neon vermelho.
  k.f(box(0.3, 1.8, 9.4), '#1a1c22', tf(0, 5.25, 0));
  k.f(box(0.2, 0.9, 0.2), '#1a1c22', tf(0, 4.3, -3.5)).f(box(0.2, 0.9, 0.2), '#1a1c22', tf(0, 4.3, 3.5));
  signText(k, 'DINER', 1.3, '#ff3048', tf(0, 5.25, 0, 1, 1, 1, 0, Math.PI / 2, 0), 0.17, true);
  // Seta luminosa num poste, apontando para a porta.
  k.f(cyl(0.18, 0.24, 10, 8), '#4a4f58', tf(8, 5, -10));
  const s = new Kit();
  s.f(box(5.2, 2.4, 0.4), '#1f5aa8', tf(0, 0, 0));
  s.f(box(5.5, 0.22, 0.46), '#f2c33a', tf(0, 1.25, 0)).f(box(5.5, 0.22, 0.46), '#f2c33a', tf(0, -1.25, 0));
  signText(s, 'EAT', 1.3, '#fff4e0', tf(0, 0.05, 0), 0.22, true);
  const arrow = [-1.2, 0, 0, 1.2, 0, 0, 0, -1.6, 0, -0.45, 0, 0, 0.45, 0, 0, 0.45, 1.3, 0, -0.45, 0, 0, 0.45, 1.3, 0, -0.45, 1.3, 0];
  for (const z of [0.24, -0.24]) s.raw(paint(tris2(arrow), '#ffd23a', tf(1.5, -2.6, z)), true);
  k.add(s, tf(8, 9.6, -10, 1, 1, 1, 0, Math.PI / 2 - 0.5, 0));
  return k.model(12, { shadow: true, blob: 6 });
}

/** Lodge de toras das Rochosas: paredes de toras cruzadas, telhado verde, chaminé de pedra e varanda. */
function logLodge(): Model {
  const k = new Kit();
  const L = 22; const D = 12;
  k.f(box(D + 1, 0.8, L + 1), '#8a8478', tf(0, 0.4, 0));
  const logs = 9; const lh = 0.68;
  for (let i = 0; i < logs; i++) {
    const y = 0.8 + lh / 2 + i * lh; const c = i % 2 ? '#7a4a2a' : '#8c5834';
    const o = i % 2 ? 0.7 : 0.35;
    k.f(box(0.7, lh - 0.04, L + o * 2), c, tf(D / 2, y, 0)).f(box(0.7, lh - 0.04, L + o * 2), c, tf(-D / 2, y, 0));
    k.f(box(D + (1 - o) * 1.4, lh - 0.04, 0.7), c, tf(0, y + lh / 2 * 0, L / 2)).f(box(D + (1 - o) * 1.4, lh - 0.04, 0.7), c, tf(0, y, -L / 2));
  }
  const top = 0.8 + logs * lh;
  k.f(gable(D - 0.4, 4.6, L - 0.6), '#8c5834', tf(0, top, 0));
  k.f(gable(D, 5.0, L, 1.0), '#2f4a3a', tf(0, top + 0.1, 0));
  // Água-furtada central na frente (+X).
  k.f(box(5, 3, 6), '#8c5834', tf(D / 2 - 1, top + 1.2, 0));
  k.f(gable(6.6, 2.8, 7, 0.5), '#2f4a3a', tf(D / 2 - 0.5, top + 2.6, 0, 1, 1, 1, 0, Math.PI / 2, 0));
  k.l(box(0.12, 1.4, 2.2), '#ffcf80', tf(D / 2 + 1.55, top + 1.6, 0));
  // Janelas e porta.
  for (const z of [-8, -4.2, 4.2, 8]) {
    k.f(box(0.16, 1.9, 2.0), '#efe6d2', tf(D / 2 + 0.36, 3.5, z));
    k.l(box(0.12, 1.5, 1.6), '#ffcf80', tf(D / 2 + 0.42, 3.5, z));
  }
  k.f(box(0.2, 2.6, 1.8), '#4a2e1a', tf(D / 2 + 0.38, 2.1, 0));
  // Chaminé de pedra.
  const ch = paint(box(2.4, top + 6.5, 2.6), '#8f8a80', tf(-1.5, (top + 6.5) / 2, -L / 2 - 1.2));
  k.raw(speckle(ch, 0.1, 3));
  k.f(box(2.8, 0.4, 3.0), '#6f6a62', tf(-1.5, top + 6.6, -L / 2 - 1.2));
  // Varanda coberta.
  k.f(box(3.4, 0.4, 16), '#9a7048', tf(D / 2 + 2.1, 1.0, 0));
  for (const z of [-7.6, -2.6, 2.6, 7.6]) k.f(cyl(0.2, 0.22, 3.4, 6), '#8c5834', tf(D / 2 + 3.4, 2.9, z));
  k.f(box(4.4, 0.25, 17), '#2f4a3a', tf(D / 2 + 2.3, 4.7, 0, 1, 1, 1, 0, 0, -0.25));
  // Pilha de lenha e um alce de madeira? Só a lenha.
  for (let i = 0; i < 6; i++) k.f(cyl(0.22, 0.22, 2.2, 6), '#a07448', tf(D / 2 + 1, 0.3 + (i % 3) * 0.42, -L / 2 + 2.5 + Math.floor(i / 3) * 0.44, 1, 1, 1, Math.PI / 2, 0, 0));
  return k.model(top + 7, { shadow: true, blob: 9 });
}

/** Ponte de treliça de aço sobre o desfiladeiro (três vãos, pilares de pedra, encontros de rocha). */
function ponteTrelica(): Model {
  const k = new Kit();
  const D = 22; const T = 9; const W = 4.2; const span = 48; const n = 6;
  const steel = '#b5482e';
  for (let s = 0; s < 3; s++) {
    const z0 = -72 + s * span;
    const bot = (i: number): number => z0 + (i * span) / n;
    const topY = (i: number): number => D + T * (0.72 + 0.28 * Math.sin((Math.PI * i) / n));
    for (const x of [W, -W]) {
      for (let i = 0; i < n; i++) k.bar(steel, [x, D, bot(i)], [x, D, bot(i + 1)], 0.6);
      for (let i = 1; i < n - 1; i++) k.bar(steel, [x, topY(i), bot(i)], [x, topY(i + 1), bot(i + 1)], 0.6);
      k.bar(steel, [x, D, bot(0)], [x, topY(1), bot(1)], 0.6);
      k.bar(steel, [x, topY(n - 1), bot(n - 1)], [x, D, bot(n)], 0.6);
      for (let i = 1; i < n; i++) k.bar(steel, [x, D, bot(i)], [x, topY(i), bot(i)], 0.4);
      for (let i = 1; i < n - 1; i++) {
        if (i < n / 2) k.bar(steel, [x, topY(i), bot(i)], [x, D, bot(i + 1)], 0.32);
        else k.bar(steel, [x, D, bot(i)], [x, topY(i + 1), bot(i + 1)], 0.32);
      }
    }
    for (let i = 1; i < n; i++) k.bar(steel, [W, topY(i), bot(i)], [-W, topY(i), bot(i)], 0.35);
  }
  k.f(box(2 * W + 1.4, 1.0, 146), '#55524e', tf(0, D - 0.5, 0));
  for (const z of [-24, 24]) {
    k.f(hip(6, D - 1, 11, 4, 9), '#9a8f80', tf(0, 0, z));
    k.f(box(5, 1.2, 10), '#8a8070', tf(0, D - 1.6, z));
  }
  // Encontros: as paredes do desfiladeiro nas pontas, em bancadas de rocha com o pinheiral no alto.
  for (const sz of [-1, 1]) {
    for (let l = 0; l < 7; l++) {
      const y0 = l * 3.6; const y1 = y0 + 3.6;
      const color = ['#8a7a68', '#7d6e5e', '#94846f', '#76685a'][(l + (sz > 0 ? 1 : 0)) % 4];
      const zIn = 72 + l * 0.8 + 3 * hash2(l, 391 + sz); const zOut = 122;
      const g = paint(tris(strata(zIn, zOut, y0, y1, (z) => 17 + 4 * noise2(392 + sz, z / 12, l * 0.7) + (z - zIn < 6 ? -2 : 0), (z) => 2 * noise2(393, z / 20, sz))), color);
      if (sz < 0) mirrorZ([g]);
      faceColor(g, (_x, y, _z, up) => (up > 0.8 && y > 24 ? '#5f7a42' : color));
      k.raw(speckle(g, 0.07, 394 + l));
    }
    for (let i = 0; i < 6; i++) {
      const x = -14 + (28 * i) / 5 + (hash2(i, 395) - 0.5) * 4; const z = sz * (88 + hash2(i, 396 + sz) * 26); const h = 9 + hash2(i, 397) * 5;
      k.f(cone(2.6, h, 6), i % 2 ? '#2a5434' : '#23482c', tf(x, 25.2 + h / 2, z));
    }
  }
  return k.model(D + T + 2, { shade: [0.75, 1.05] });
}

/** Represa Hoover: arco de concreto encaixado num cânion escuro, torres de captação e a usina no pé. */
function represaHoover(): Model {
  const k = new Kit();
  const R = 220; const A = 0.5; const H = 120;
  const thick = (y: number): number => 9 + 62 * Math.pow(1 - y / H, 1.6);
  const P = (th: number, rr: number, y: number): P3 => [R - rr * Math.cos(th), y, rr * Math.sin(th)];
  const concrete = '#d9d2c2';
  k.f(grid(18, 6, (u, v) => { const th = -A + 2 * A * u; const y = v * H; return P(th, R - thick(y), y); }), concrete);
  k.f(grid(18, 1, (u, v) => { const th = -A + 2 * A * u; return P(th, R - 9 - v * 1, H); }), '#c8c0b0');
  k.f(grid(18, 1, (u, v) => { const th = -A + 2 * A * u; return P(th, R - 9 + v * 9, H); }), '#bdb5a5');
  // Mureta e postes do coroamento.
  k.f(grid(18, 1, (u, v) => { const th = -A + 2 * A * u; return P(th, R - 9, H + v * 1.4); }), '#e8e2d4');
  for (let i = 0; i <= 6; i++) {
    const th = -A + (2 * A * i) / 6; const [x, , z] = P(th, R - 9.2, H);
    k.f(cyl(0.2, 0.25, 6, 5), '#5a5a5a', tf(x, H + 3, z));
    k.l(box(0.6, 0.5, 0.6), '#fff0c8', tf(x, H + 6.2, z));
  }
  // Água do reservatório atrás.
  k.f(box(100, 1, 260), '#2f6c8c', tf(-45, H - 6, 0));
  // Torres de captação (no lago, montante).
  for (const th of [-0.32, -0.16, 0.16, 0.32]) {
    const [x, , z] = P(th, R + 26, 0);
    k.f(cyl(6, 7, 40, 10), concrete, tf(x, H - 20, z));
    k.f(cyl(7.2, 7.2, 2, 10), '#b8b0a0', tf(x, H + 1, z));
    k.f(cyl(5, 6, 6, 10), '#e8e2d4', tf(x, H + 5, z));
    k.l(cyl(6.05, 6.05, 1.4, 10, true), '#ffe2a8', tf(x, H + 4, z));
  }
  // Usina em U no pé da represa.
  const base = thick(0);
  k.f(box(22, 20, 60), '#d8d0c0', tf(base + 11, 10, -44)).f(box(22, 20, 60), '#d8d0c0', tf(base + 11, 10, 44));
  k.f(box(18, 14, 30), '#cfc6b4', tf(base + 9, 7, 0));
  for (const z of [-44, 44]) k.l(box(0.2, 2, 50), '#ffe2a8', tf(base + 22.1, 14, z));
  // Paredes do cânion: rocha vulcânica marrom-escura em bancadas (camadas de 13 m com o contorno irregular), coladas
  // nas ombreiras da represa e abrindo rio abaixo; do lado do lago, a margem recua.
  const xEnd = (y: number): number => R - (R - thick(Math.min(y, H))) * Math.cos(A);
  const zEnd = (y: number): number => (R - thick(Math.min(y, H))) * Math.sin(A);
  const rocks = ['#6e5040', '#7c5a46', '#5e4436', '#76523f', '#664a3a'];
  const turn = new THREE.Matrix4().makeRotationY(Math.PI / 2);
  for (const sz of [-1, 1]) {
    for (let l = 0; l < 10; l++) {
      const y0 = l * 13; const y1 = y0 + 13;
      const zin = (x: number, y: number): number => {
        const down = zEnd(y) - 6 + 0.25 * Math.max(0, x - xEnd(y)) + 6 * noise2(271 + (sz > 0 ? 9 : 0), x / 28, l * 0.6);
        return x < xEnd(y) - 30 ? 132 + 6 * noise2(272, x / 30, l) : x < xEnd(y) - 6 ? down + (132 - down) * smooth(-6, -30, x - xEnd(y)) : down;
      };
      const ym = (y0 + y1) / 2;
      const color = rocks[(l * 3 + (sz > 0 ? 1 : 0)) % rocks.length];
      // No referencial da camada, o eixo dela (z) é o X do mundo e o lado dela (x) é o −Z do mundo (gira 90°).
      // Rio abaixo, cada bancada acaba antes da de baixo: a borda do cânion desce em degraus e não tapa a represa.
      const xMax = xEnd(ym) + 34 - l * 2.5 + 8 * noise2(274, l, sz);
      const g = paint(tris(strata(-75, xMax, y0, y1, (x, y) => (200 - zin(x, y)) / 2, (x) => (-sz * (zin(x, ym) + 200)) / 2, 16)), color);
      g.applyMatrix4(turn);
      faceColor(g, (_x, y, _z, up) => (up > 0.8 && y > y1 - 0.5 ? '#a8865e' : color));
      k.raw(speckle(g, 0.07, 273 + l));
    }
  }
  // O rio Colorado saindo da usina, verde-azulado.
  k.f(box(70, 0.6, 70), '#3f7f86', tf(base + 50, 0.3, 0));
  return k.model(H + 10, { shade: [0.7, 1.05] });
}

/** Placa "WELCOME" de Las Vegas: losango branco, letras vermelhas em discos, estrela e lâmpadas. */
function placaLasVegas(): Model {
  const k = new Kit();
  const outline: Array<[number, number]> = [
    [-4.0, 0.3], [-3.3, 1.6], [-1.2, 1.9], [0, 2.45], [1.2, 1.9], [3.3, 1.6], [4.0, 0.3], [3.3, -1.6], [1.2, -1.9], [0, -2.1], [-1.2, -1.9], [-3.3, -1.6],
  ];
  const s = new Kit();
  s.f(extrude(shapeOf(outline, 1.06), 0.3), '#d8262e');
  s.f(extrude(shapeOf(outline, 0.98), 0.4), '#f6f3ea');
  // WELCOME: uma letra vermelha em cada disco branco.
  const word = 'WELCOME';
  for (let i = 0; i < word.length; i++) {
    const x = (i - 3) * 1.0;
    s.f(cyl(0.46, 0.46, 0.48, 14), '#ffffff', tf(x, 0.85, 0, 1, 1, 1, Math.PI / 2, 0, 0));
    s.f(cyl(0.48, 0.48, 0.44, 14), '#c9c4b8', tf(x, 0.85, 0, 1, 1, 1, Math.PI / 2, 0, 0));
    signText(s, word[i], 0.56, '#d0202a', tf(x, 0.85, 0), 0.25);
  }
  // Faixas no lugar do resto do letreiro (sem texto).
  s.f(box(5.6, 0.12, 0.44), '#2a5ec8', tf(0, 0.05, 0));
  s.f(box(5.0, 0.9, 0.44), '#d8262e', tf(0, -0.75, 0));
  s.f(box(3.2, 0.12, 0.44), '#2a5ec8', tf(0, -1.45, 0));
  // Lâmpadas no contorno (as duas faces).
  const per: Array<[number, number]> = [];
  for (let i = 0; i < outline.length; i++) {
    const [ax, ay] = outline[i]; const [bx, by] = outline[(i + 1) % outline.length];
    const n = Math.max(1, Math.round(Math.hypot(bx - ax, by - ay) / 0.5));
    for (let j = 0; j < n; j++) per.push([(ax + ((bx - ax) * j) / n) * 1.02, (ay + ((by - ay) * j) / n) * 1.02]);
  }
  for (const [x, y] of per) for (const z of [0.22, -0.22]) s.l(new THREE.OctahedronGeometry(0.09, 0), '#fff2b0', tf(x, y, z));
  // Estrela vermelha no topo, com haste.
  s.f(box(0.25, 1.0, 0.25), '#e8e4da', tf(0, 2.9, 0));
  const star: Array<[number, number]> = [];
  for (let i = 0; i < 16; i++) { const r = i % 2 ? 0.38 : 0.95; const a = (i / 16) * Math.PI * 2 + Math.PI / 2; star.push([Math.cos(a) * r, Math.sin(a) * r]); }
  s.f(extrude(shapeOf(star), 0.22), '#e0262e', tf(0, 3.9, 0));
  for (const z of [0.13, -0.13]) s.l(new THREE.OctahedronGeometry(0.16, 0), '#ffe680', tf(0, 3.9, z));
  // Dois postes e a ilha de concreto.
  k.f(box(7, 0.35, 4), '#c8c4ba', tf(0, 0.17, 0));
  for (const z of [-1.3, 1.3]) k.f(box(0.45, 4.4, 0.45), '#eceae4', tf(0, 2.2, z));
  k.add(s, tf(0, 6.3, 0, 1, 1, 1, 0, Math.PI / 2 - 0.5, 0));
  return k.model(11, { shadow: true, blob: 3 });
}

/** Pirâmide de vidro negro (Las Vegas) com o facho no céu à noite, a esfinge e o obelisco. */
function piramideLuxor(): Model {
  const k = new Kit();
  const H = 107; const B = 90; const bands = 24;
  for (let i = 0; i < bands; i++) {
    const y0 = (i / bands) * H; const y1 = ((i + 1) / bands) * H;
    const r0 = (1 - y0 / H) * B * SQ; const r1 = (1 - y1 / H) * B * SQ;
    const g = cyl(r1, r0, y1 - y0, 4, true).rotateY(Math.PI / 4);
    k.f(g, i % 2 ? '#1c2029' : '#262c38', tf(0, (y0 + y1) / 2, 0));
  }
  k.l(new THREE.OctahedronGeometry(2.6, 0), '#e8f4ff', tf(0, H + 0.5, 0));
  k.l(box(0.3, 3, 24), '#ffd8a0', tf(B - 2, 2, 0, 1, 1, 1, 0, 0, 0.785));
  for (const r of [3, 6, 10]) k.beam(new THREE.CylinderGeometry(r, 2, 700, 10, 1, true), tf(0, H + 350, 0));
  // Esfinge na frente (+X), de arenito.
  const sand = '#c8aa7a';
  k.f(box(50, 3, 18), '#b89a6c', tf(118, 1.5, 0));
  k.f(box(34, 11, 12), sand, tf(108, 8.5, 0));
  k.f(box(12, 7, 10), sand, tf(96, 6.5, 0));
  for (const z of [-3.6, 3.6]) k.f(box(16, 3.2, 3.4), sand, tf(132, 4.6, z));
  k.f(box(8, 10, 10), sand, tf(124, 15, 0));
  k.f(box(6, 8, 7), '#d2b484', tf(127, 22, 0));
  k.f(hip(9, 9, 11, 3, 3), '#2c4a8a', tf(125, 18, 0));
  k.f(box(6.2, 1.2, 7.2), '#d8b048', tf(127, 25.4, 0));
  k.f(box(0.4, 1.2, 4.6), '#3a2e22', tf(130.1, 22.8, 0));
  // Obelisco.
  k.f(hip(4.6, 34, 4.6, 2.8, 2.8), '#3a3530', tf(78, 0, 70));
  k.f(hip(2.8, 3, 2.8), '#d8b048', tf(78, 34, 70));
  return k.model(H, { shade: [0.85, 1.1] });
}

/** Stratosphere: fuste de três pernas de 250 m, a cápsula de andares de vidro e a agulha. */
function torreStratosphere(): Model {
  const k = new Kit();
  const white = '#ece8e0';
  k.f(box(46, 12, 46), '#c8c0b0', tf(0, 6, 0));
  k.l(box(46.2, 2.2, 46.2), '#ffe2a8', tf(0, 6.5, 0));
  for (let i = 0; i < 3; i++) {
    const a = (i / 3) * Math.PI * 2;
    k.rod(white, [Math.cos(a) * 9, 12, Math.sin(a) * 9], [Math.cos(a) * 4.5, 258, Math.sin(a) * 4.5], 5.5, 3.2, 8);
  }
  k.f(cyl(4.5, 6.5, 250, 10), '#ddd8cf', tf(0, 135, 0));
  const lv: Array<[number, number, number, number, C, boolean]> = [
    [252, 8, 9, 15, white, false], [260, 6, 15, 21, white, false], [266, 6, 21, 21, '#9fd8ff', true], [272, 4, 21, 19, white, false],
    [276, 4, 19, 19, '#bfe6ff', true], [280, 4, 19, 14, white, false], [284, 6, 10, 10, '#d8d2c8', false],
  ];
  for (const [y, h, rb, rt, c, glass] of lv) {
    if (glass) { k.f(cyl(rt - 0.5, rb - 0.5, h, 18), '#3a4a5a', tf(0, y + h / 2, 0)); k.l(cyl(rt, rb, h * 0.8, 18, true), c, tf(0, y + h / 2, 0)); } else k.f(cyl(rt, rb, h, 18), c, tf(0, y + h / 2, 0));
  }
  k.l(cyl(15.2, 15.2, 0.8, 18, true), '#ff5ad8', tf(0, 256, 0));
  k.l(cyl(21.2, 21.2, 0.6, 18, true), '#ff5ad8', tf(0, 265.8, 0));
  k.f(cyl(0.9, 2.4, 60, 8), '#f2f0ea', tf(0, 320, 0));
  k.b(sphere(1.2, 6, 4), '#ff3030', tf(0, 351, 0));
  return k.model(350, { shade: [0.8, 1.05] });
}

// ───────────────────────────── Japão ─────────────────────────────

/** Rainbow Bridge (Tóquio): pênsil branca, torres em H, cabos com luzes coloridas, deque de dois andares. */
function rainbowBridge(): Model {
  const k = new Kit();
  const white = '#eef0f2'; const deckY = 34;
  const towersZ = [-130, 130]; const top = 88;
  for (const z of towersZ) {
    k.f(box(34, 8, 16), '#c8c8c2', tf(0, 4, z));
    for (const x of [-12, 12]) k.f(box(4, top, 6), white, tf(x, top / 2, z));
    for (const y of [deckY - 3, 58, top - 2]) k.f(box(28, 3, 5), white, tf(0, y, z));
    k.b(sphere(0.9, 6, 4), '#ff3030', tf(-12, top + 1, z)).b(sphere(0.9, 6, 4), '#ff3030', tf(12, top + 1, z));
    k.l(box(4.2, 1.2, 6.2), '#ffffff', tf(-12, top - 6, z)).l(box(4.2, 1.2, 6.2), '#ffffff', tf(12, top - 6, z));
  }
  // Deque: caixão treliçado escuro com a pista em cima.
  k.f(box(26, 2, 470), '#e8eaec', tf(0, deckY, 0));
  k.f(box(24, 6, 470), '#5a6068', tf(0, deckY - 4, 0));
  k.f(box(26, 1, 470), '#e0e2e4', tf(0, deckY - 7.5, 0));
  for (const z of [-200, -170, 170, 200]) k.f(box(6, deckY - 8, 8), '#c8c8c2', tf(0, (deckY - 8) / 2, z));
  for (const z of [-222, 222]) k.f(box(34, 32, 26), '#d4d4ce', tf(0, 16, z));
  // Cabos principais e pendurais; contas de luz nos cabos (arco-íris).
  const rainbow = ['#ff6a6a', '#ffb44a', '#ffe95a', '#7aff8a', '#6ac8ff', '#b88aff'];
  for (const x of [-12, 12]) {
    const main = (z: number): number => 40 + (top - 40) * (z / 130) ** 2;
    const pts: P3[] = [];
    for (let i = 0; i <= 20; i++) { const z = -130 + i * 13; pts.push([x, main(z), z]); }
    for (let i = 0; i < 20; i++) k.bar('#d8dadc', pts[i], pts[i + 1], 0.9);
    for (let i = 1; i < 20; i++) { k.bar('#d0d2d4', [x, pts[i][1], pts[i][2]], [x, deckY + 1, pts[i][2]], 0.25); k.l(new THREE.OctahedronGeometry(0.9, 0), rainbow[i % 6], tf(x, pts[i][1] + 0.6, pts[i][2])); }
    for (const sz of [-1, 1]) {
      const side: P3[] = [];
      for (let i = 0; i <= 8; i++) { const t = i / 8; const z = sz * (130 + 85 * t); side.push([x, top + (32 - top) * t - 10 * Math.sin(Math.PI * t), z]); }
      for (let i = 0; i < 8; i++) k.bar('#d8dadc', side[i], side[i + 1], 0.9);
      for (let i = 1; i < 8; i++) { k.bar('#d0d2d4', side[i], [x, deckY + 1, side[i][2]], 0.25); k.l(new THREE.OctahedronGeometry(0.9, 0), rainbow[(i + 3) % 6], tf(x, side[i][1] + 0.6, side[i][2])); }
    }
  }
  return k.model(top, { shade: [0.82, 1.05] });
}

/** Torre de Tóquio: treliça laranja e branca de 333 m, mirantes e a base com o prédio. */
function torreToquio(): Model {
  const k = new Kit();
  const orange = '#f2581f'; const white = '#f2f2ee';
  k.f(box(64, 16, 64), '#e4e4e0', tf(0, 8, 0));
  k.l(box(64.2, 2.4, 64.2), '#ffe2a0', tf(0, 9, 0));
  const lv: Array<[number, number]> = [[16, 33], [40, 22], [65, 15.5], [95, 11.5], [118, 9.5], [145, 8], [170, 6.8], [195, 5.6], [222, 4.5], [250, 3.2]];
  latticeTower(k, lv, (i) => (i % 2 ? white : orange), (i) => (i < 3 ? 1.8 : i < 6 ? 1.3 : 0.9), true, '#ff8a4a');
  // Pernas abertas sobre o prédio, com o arco em cada face.
  for (let q = 0; q < 4; q++) {
    const rot = (x: number, z: number): [number, number] => [q === 0 ? x : q === 1 ? -z : q === 2 ? -x : z, q === 0 ? z : q === 1 ? x : q === 2 ? -z : -x];
    let prev: P3 | null = null;
    for (let i = 0; i <= 6; i++) {
      const t = i / 6; const zz = -25 * Math.cos(Math.PI * t); const y = 18 + 20 * Math.sin(Math.PI * t);
      const [x, z] = rot(31 - (y - 16) * 0.45, zz);
      const p: P3 = [x, y, z];
      if (prev) k.bar(orange, prev, p, 1.4);
      prev = p;
    }
  }
  // Mirantes.
  k.f(cyl(15, 13, 10, 8), white, tf(0, 125, 0, 1, 1, 1, 0, Math.PI / 8, 0));
  k.l(cyl(15.1, 15.1, 3.4, 8, true), '#cfe8ff', tf(0, 126, 0, 1, 1, 1, 0, Math.PI / 8, 0));
  k.f(cyl(7, 6, 6, 8), white, tf(0, 224, 0, 1, 1, 1, 0, Math.PI / 8, 0));
  k.l(cyl(7.05, 7.05, 2, 8, true), '#cfe8ff', tf(0, 224.5, 0, 1, 1, 1, 0, Math.PI / 8, 0));
  // Antena com faixas.
  for (let i = 0; i < 6; i++) k.f(cyl(2.4 - i * 0.35, 2.6 - i * 0.35, 14, 8), i % 2 ? white : orange, tf(0, 257 + i * 14, 0));
  k.b(sphere(1.2, 6, 4), '#ff3030', tf(0, 334, 0));
  return k.model(333, { shade: [0.85, 1.05] });
}

/** Portão Shurei (Okinawa): quatro pilares vermelhos, dois telhados de telha vermelha com rejunte branco. */
function portaoShurei(): Model {
  const k = new Kit();
  const red = '#c8382a'; const tile = '#c4502e';
  k.f(box(8, 0.3, 17), '#cfc6b2', tf(0, 0.15, 0));
  for (const z of [-6, -2.6, 2.6, 6]) {
    k.f(box(1.0, 0.5, 1.0), '#b8b0a0', tf(0, 0.55, z));
    k.f(cyl(0.34, 0.38, 5.4, 10), red, tf(0, 3.0, z));
    for (const x of [-1.1, 1.1]) k.bar(red, [x, 0.4, z], [0, 3.4, z], 0.28);
  }
  k.f(box(0.7, 0.7, 13.6), red, tf(0, 5.6, 0));
  k.f(box(1.4, 0.5, 13.0), '#2a2422', tf(0, 6.1, 0));
  // Telhado de baixo (todo o portão) com beiral escuro e cumeeira branca.
  k.f(box(4.4, 0.18, 16.4), '#3a2a24', tf(0, 6.45, 0));
  k.f(hip(4.6, 1.5, 16.6, 0.7, 13.8), tile, tf(0, 6.5, 0));
  k.f(box(0.5, 0.3, 14), '#f2efe8', tf(0, 8.05, 0));
  // Corpo de cima com a placa, e o telhado de cima.
  k.f(box(2.2, 1.8, 6.6), red, tf(0, 8.9, 0));
  for (const sx of [1, -1]) {
    k.f(box(0.16, 1.1, 3.4), '#d8b040', tf(sx * 1.14, 8.9, 0));
    k.f(box(0.18, 0.86, 3.1), '#1e1814', tf(sx * 1.2, 8.9, 0));
  }
  k.f(box(4.0, 0.18, 8.6), '#3a2a24', tf(0, 9.85, 0));
  k.f(hip(4.2, 1.7, 8.8, 0.5, 6.2), tile, tf(0, 9.9, 0));
  k.f(box(0.5, 0.35, 6.4), '#f2efe8', tf(0, 11.7, 0));
  for (const z of [-3.3, 3.3]) k.f(box(0.6, 0.9, 0.5), '#f2efe8', tf(0, 12.0, z));
  // Linhas de rejunte branco nas águas dos telhados.
  for (let i = -3; i <= 3; i++) for (const sx of [1, -1]) k.bar('#efe8dc', [sx * 2.25, 6.55, i * 2.1], [sx * 0.4, 7.9, i * 1.8], 0.1);
  // Muros de pedra coral e hibiscos.
  for (const sz of [-1, 1]) {
    k.f(box(1.0, 1.7, 7), '#d8cfb4', tf(0, 0.85, sz * 11.6));
    k.raw(speckle(paint(ico(1.0, 1), '#3f8a3a', tf(1.4, 0.9, sz * 9.4, 1.2, 1, 1.4)), 0.12, 4));
    k.l(sphere(0.18, 5, 3), '#ff4a5a', tf(2.4, 1.2, sz * 9.2)).l(sphere(0.18, 5, 3), '#ff4a5a', tf(2.0, 1.7, sz * 9.9));
  }
  return k.model(12.5, { shadow: true, blob: 5 });
}

/** Shisa (leão-cão de Okinawa) de terracota, sentado; `open` = boca aberta. */
function shisaFigure(k: Kit, m: THREE.Matrix4, open: boolean, seed: number): void {
  const s = new Kit();
  const t = '#c86a3c'; const t2 = '#a8522c';
  s.f(ico(0.75, 1), t, tf(-0.3, 0.6, 0, 1.05, 0.8, 1));
  s.f(ico(0.6, 1), t, tf(0.22, 1.05, 0, 0.85, 1.15, 0.9));
  for (const z of [-0.32, 0.32]) { s.f(box(0.32, 1.0, 0.3), t, tf(0.45, 0.45, z)); s.f(box(0.48, 0.26, 0.38), t2, tf(0.62, 0.13, z)); }
  s.f(ico(0.62, 1), t, tf(0.42, 1.88, 0, 1, 0.9, 1.12));
  s.f(box(0.5, 0.42, 0.86), t, tf(0.92, 1.72, 0));
  if (open) {
    s.f(box(0.12, 0.3, 0.66), '#3a1a12', tf(1.17, 1.56, 0));
    s.f(box(0.1, 0.12, 0.4), '#c8302a', tf(1.2, 1.46, 0));
    s.f(box(0.46, 0.18, 0.8), t, tf(0.92, 1.34, 0));
    for (const z of [-0.25, 0.25]) s.f(cone(0.06, 0.18, 4), '#f6f0e4', tf(1.15, 1.62, z, 1, 1, 1, Math.PI, 0, 0));
  } else s.f(box(0.06, 0.06, 0.7), '#3a1a12', tf(1.18, 1.62, 0));
  for (const z of [-0.26, 0.26]) { s.f(sphere(0.17, 6, 4), '#f6f0e4', tf(0.88, 2.06, z)); s.f(sphere(0.08, 5, 3), '#1a1410', tf(1.02, 2.07, z)); s.f(box(0.3, 0.1, 0.24), t2, tf(0.9, 2.26, z)); }
  for (let i = 0; i < 9; i++) {
    const a = -Math.PI * 0.9 + (i / 8) * Math.PI * 1.8;
    s.f(dodeca(0.24), t2, tf(0.2 + Math.cos(a) * -0.05 - 0.25, 1.9 + Math.sin(a) * 0.55, Math.cos(a) * 0.62));
  }
  for (const z of [-0.45, 0.45]) s.f(cone(0.14, 0.3, 4), t2, tf(0.3, 2.45, z));
  s.f(dodeca(0.3), t2, tf(-0.95, 1.3, 0)).f(dodeca(0.26), t2, tf(-1.05, 1.7, 0.1)).f(dodeca(0.22), t2, tf(-0.9, 2.0, -0.05));
  for (const g of s.flat) speckle(g, 0.08, seed);
  k.add(s, m);
}

/**
 * Par de shisas sobre pilares de pedra coral, num muro baixo (a entrada de uma casa de Okinawa). A shisa baixada
 * (peça `shisa`, parts.ts, com a base dela) entra no lugar das procedurais, da mesma altura (2,6 × 1,35 sobre o
 * pilar), de frente (+X) para a pista e um pouco virada para a outra.
 */
function shisa(): Model {
  const k = new Kit();
  const part = landmarkPart('shisa');
  for (const sz of [-1, 1]) {
    k.raw(speckle(paint(box(1.7, 2.6, 1.7), '#d8cfb4', tf(0, 1.3, sz * 3.4)), 0.07, 2));
    k.f(box(2.0, 0.3, 2.0), '#c4b89c', tf(0, 2.75, sz * 3.4));
    k.raw(speckle(paint(box(1.0, 1.6, 6), '#d8cfb4', tf(0, 0.8, sz * 7.2)), 0.07, 3));
    if (part) k.raw(part.at({ height: 2.6 * 1.35 }, 0, 2.9, sz * 3.4, sz * 0.15));
    else shisaFigure(k, tf(0, 2.9, sz * 3.4, 1.35, 1.35, 1.35), sz < 0, sz < 0 ? 5 : 6);
  }
  k.f(box(5, 0.12, 5.4), '#cfc6b2', tf(1, 0.06, 0));
  k.raw(speckle(paint(ico(1.2, 1), '#3f8a3a', tf(1.2, 0.8, -10.5, 1.3, 0.9, 1.4)), 0.12, 7));
  k.l(sphere(0.2, 5, 3), '#ff4a5a', tf(2.4, 1.2, -10.4));
  return k.model(6.5, { shadow: true, blob: 3 });
}

/** Monte Fuji: cone côncavo de cratera achatada, neve em estrias, sopé azulado (escala do horizonte). */
function monteFujiPico(): Model {
  const k = new Kit();
  const H = 215; const R = 345; const Rt = 24;
  const prof: Array<[number, number]> = [[0, H - 10], [Rt - 8, H - 3], [Rt, H]];
  for (let i = 1; i <= 10; i++) { const s = i / 10; prof.push([Rt + s * (R - Rt), H * Math.pow(1 - s, 1.75)]); }
  const g = paint(lathe(prof, 40), '#506684');
  roughen(g, 3, 21);
  faceColor(g, (x, y, z) => {
    const az = Math.atan2(z, x);
    const line = H * (0.55 + 0.1 * valueNoise(31, az * 4)) - (hash2(Math.floor((az + 4) * 6.4), 3) > 0.55 ? H * 0.16 : 0);
    if (y > line) return y > H * 0.9 ? '#f6f8fc' : '#eef2f8';
    return y < H * 0.12 ? '#4a6a6a' : '#566c8c';
  });
  k.raw(speckle(g, 0.04, 22));
  return k.model(H, { shade: [0.95, 1.02] });
}

/** Altura (m) do morro do pagode Chureito num ponto. */
function chureitoHill(x: number, z: number): number {
  const t = 1 - (x / 52) ** 2 - (z / 44) ** 2;
  return t > 0 ? 16 * Math.sqrt(t) : 0;
}

/** Pagode Chureito: cinco andares vermelhos no alto de um morro, cerejeiras floridas e a escadaria. */
function pagodeChureito(): Model {
  const k = new Kit();
  const hill = paint(sphere(1, 16, 6, 0, Math.PI * 2, 0, Math.PI / 2), '#5a8a3a', tf(0, 0, 0, 52, 16, 44));
  k.raw(speckle(roughen(hill, 1.2, 31), 0.1, 32));
  const px = -6; const py = chureitoHill(px, 0) - 0.6;
  k.f(box(14, 1.6, 14), '#b8b0a0', tf(px, py + 0.8, 0));
  let y = py + 1.6;
  for (let i = 0; i < 5; i++) {
    const w = 8.2 - i * 0.8; const h = i === 0 ? 4.4 : 3.3;
    k.f(box(w, h, w), '#c8402a', tf(px, y + h / 2, 0));
    k.f(box(w + 0.1, 0.35, w + 0.1), '#efe6d2', tf(px, y + h - 0.6, 0));
    k.f(box(w + 4.4, 0.28, w + 4.4), '#2e2e34', tf(px, y + h + 0.05, 0));
    k.f(hip(w + 4.2, 1.3, w + 4.2, w * 0.45, w * 0.45), '#3a3a42', tf(px, y + h + 0.18, 0));
    const e = (w + 4.2) / 2;
    for (const [sx, sz] of [[1, 1], [1, -1], [-1, 1], [-1, -1]] as const) k.bar('#3a3a42', [px + sx * (e - 0.9), y + h + 0.2, sz * (e - 0.9)], [px + sx * (e + 0.3), y + h + 0.75, sz * (e + 0.3)], 0.3);
    y += h + 1.5;
  }
  k.f(cyl(0.16, 0.22, 7, 6), '#c8a040', tf(px, y + 3.5, 0));
  for (let i = 0; i < 6; i++) k.f(cyl(0.55, 0.55, 0.16, 8), '#c8a040', tf(px, y + 1 + i * 0.8, 0));
  // Cerejeiras.
  const trees: Array<[number, number]> = [[14, -17], [20, 11], [6, 20], [-18, -14], [-15, 17], [28, -5], [8, -26], [30, 18], [-28, 2]];
  trees.forEach(([x, z], i) => {
    const gy = chureitoHill(x, z) - 0.3;
    k.f(cyl(0.3, 0.45, 4, 5), '#5a3e30', tf(x, gy + 2, z));
    const c = i % 2 ? '#f6a8c8' : '#f8c4d8';
    k.raw(speckle(paint(ico(3.0, 0), c, tf(x, gy + 5.2, z, 1.2, 0.9, 1.2)), 0.08, 40 + i));
    k.raw(speckle(paint(ico(2.2, 0), c, tf(x + 1.6, gy + 6.4, z - 1, 1.1, 0.9, 1.1)), 0.08, 50 + i));
  });
  // Escadaria que sobe pela frente.
  for (let i = 0; i < 14; i++) {
    const x = 50 - i * 3.6; const gy = chureitoHill(x, 0);
    k.f(box(4.6, 0.8, 3.2), '#c8c4bc', tf(x, gy - 0.1, 0));
  }
  return k.model(y + 8, { shade: [0.8, 1.05] });
}

/** Castelo de Osaka: base de pedra em talude, torre de cinco telhados verdes com dourado; paredes acesas. */
function casteloOsaka(): Model {
  const k = new Kit();
  const stone = paint(hip(58, 16, 58, 47, 47), '#8a8a86');
  k.raw(speckle(stone, 0.08, 61));
  const lv: Array<[number, number, number]> = [[30, 36, 7], [26, 31, 6], [22, 26, 6], [17, 20, 5], [13, 15, 5]];
  let y = 16;
  lv.forEach(([w, d, h], i) => {
    const top = i === 4;
    if (top) k.f(box(w, h, d), '#2f4a3e', tf(0, y + h / 2, 0));
    else k.l(box(w, h, d), '#dcdad2', tf(0, y + h / 2, 0));
    if (top) for (const sx of [1, -1]) k.l(box(0.12, 1.2, d * 0.7), '#e8c050', tf(sx * (w / 2 + 0.05), y + h / 2, 0));
    else for (const sx of [1, -1]) for (let z = -d / 2 + 2.5; z < d / 2 - 1.5; z += 3.2) k.f(box(0.14, 1.2, 1.2), '#2a2e2c', tf(sx * (w / 2 + 0.04), y + h * 0.55, z));
    k.f(box(w + 5.4, 0.3, d + 5.4), '#f0efe8', tf(0, y + h, 0));
    k.f(hip(w + 5.2, 2.4, d + 5.2, w * 0.55, d * 0.65), '#4f9a86', tf(0, y + h + 0.15, 0));
    k.f(box(w + 5.6, 0.2, d + 5.6), '#d8b040', tf(0, y + h + 0.32, 0));
    if (i === 1 || i === 3) {
      // Oitões triangulares (chidori-hafu) na frente e nos lados.
      for (const sx of [1, -1]) {
        k.f(gable(7, 3.2, 3.5, 0.3), '#4f9a86', tf(sx * (w / 2 + 1.2), y + h + 0.4, 0, 1, 1, 1, 0, Math.PI / 2, 0));
        k.l(tris2([0, 0, 0, 0, 2.6, 2.9, 0, 0, 5.8].map((v, j) => (j % 3 === 2 ? v - 2.9 : v))), '#dcdad2', tf(sx * (w / 2 + 2.97), y + h + 0.45, 0));
      }
    }
    y += h + 2.6;
  });
  // Peixes dourados (shachihoko) na cumeeira.
  for (const z of [-4.5, 4.5]) { k.f(box(1, 1.8, 1.2), '#e0b838', tf(0, y + 0.7, z)); k.f(cone(0.6, 1.2, 5), '#e0b838', tf(0, y + 2.0, z * 1.08, 1, 1, 1, z > 0 ? 0.6 : -0.6, 0, 0)); }
  // Pinheiros no pé do muro.
  for (let i = 0; i < 8; i++) {
    const a = (i / 8) * Math.PI * 2 + 0.3; const r = 38 + hash2(i, 61) * 6;
    k.f(cyl(0.35, 0.5, 3, 5), '#5a4030', tf(Math.cos(a) * r, 1.5, Math.sin(a) * r));
    k.raw(speckle(paint(ico(3.4, 0), '#2f5a36', tf(Math.cos(a) * r, 5, Math.sin(a) * r, 1.4, 0.7, 1.4)), 0.1, 70 + i));
  }
  return k.model(y + 3, { shade: [0.8, 1.05] });
}

/** Tsutenkaku (Osaka): pernas em arco sobre a rua, fuste treliçado, mirante, relógio e a luz no topo. */
function tsutenkaku(): Model {
  const k = new Kit();
  const silver = '#c4c8ce';
  for (const [sx, sz] of [[1, 1], [1, -1], [-1, 1], [-1, -1]] as const) {
    k.bar(silver, [sx * 13, 0, sz * 13], [sx * 5.5, 22, sz * 5.5], 1.8);
    k.f(box(3, 1, 3), '#8a8a86', tf(sx * 13, 0.5, sz * 13));
  }
  for (let q = 0; q < 4; q++) {
    let prev: P3 | null = null;
    for (let i = 0; i <= 6; i++) {
      const t = i / 6; const u = -10 + 20 * t; const y = 6 + 12 * Math.sin(Math.PI * t);
      const x = 13 - (y / 22) * 7.5;
      const p: P3 = q === 0 ? [x, y, u] : q === 1 ? [-x, y, u] : q === 2 ? [u, y, x] : [u, y, -x];
      if (prev) k.bar(silver, prev, p, 0.9);
      prev = p;
    }
  }
  k.f(box(12, 2, 12), silver, tf(0, 22, 0));
  latticeTower(k, [[23, 5.2], [40, 4.6], [57, 4.2], [72, 3.9], [84, 3.6]], () => silver, () => 0.8, true, '#6ae0ff');
  k.f(box(7, 62, 7), '#9aa0a8', tf(0, 54, 0));
  // Relógio redondo na frente e nos lados.
  for (const [rx, ry] of [[0, 0], [0, Math.PI / 2], [0, -Math.PI / 2]] as const) {
    const m = tf(0, 70, 0, 1, 1, 1, rx, ry, 0);
    k.l(cyl(3.2, 3.2, 0.3, 16), '#fff6dc', m.clone().multiply(tf(4.4, 0, 0, 1, 1, 1, 0, 0, Math.PI / 2)));
    k.f(box(0.2, 2.4, 0.3), '#1a1a1a', m.clone().multiply(tf(4.62, 0.9, 0)));
    k.f(box(0.2, 0.3, 1.8), '#1a1a1a', m.clone().multiply(tf(4.62, 0, 0.7)));
  }
  // Mirante e a cúpula de luz.
  k.f(box(15, 3, 15), silver, tf(0, 86.5, 0));
  k.f(box(13, 6, 13), '#d8dce0', tf(0, 91, 0));
  k.l(box(13.2, 2.4, 13.2), '#bfe4ff', tf(0, 91.5, 0));
  k.f(box(15, 1, 15), silver, tf(0, 94.5, 0));
  k.f(box(7, 4, 7), '#d8dce0', tf(0, 97, 0));
  k.l(sphere(3.2, 10, 6, 0, Math.PI * 2, 0, Math.PI / 2), '#ffcc40', tf(0, 99, 0));
  k.f(cyl(0.3, 0.5, 7, 6), silver, tf(0, 105, 0));
  k.b(sphere(0.6, 6, 4), '#ff3030', tf(0, 108.8, 0));
  return k.model(108, { shade: [0.82, 1.05] });
}

// ───────────────────────────── Europa ─────────────────────────────

/** Neuschwanstein: castelo branco de torres finas e cones de ardósia, no alto de um penhasco com pinheiros. */
function casteloNeuschwanstein(): Model {
  const k = new Kit();
  const white = '#efeadc'; const slate = '#4c5d75';
  const crag = paint(ico(1, 2), '#7a7670', tf(0, 4, 0, 52, 30, 78));
  k.raw(speckle(tintUp(roughen(crag, 3.5, 81), '#4f7a3a', 0.6, 0.85), 0.08, 82));
  const Y = 30;
  k.f(box(40, 8, 86), '#a49e94', tf(-2, Y - 3, 2));
  // Palácio principal.
  k.f(box(16, 30, 40), white, tf(-4, Y + 15, -10));
  k.f(gable(16, 12, 41, 0.6), slate, tf(-4, Y + 30, -10));
  for (let r = 0; r < 4; r++) for (let i = 0; i < 6; i++) k.f(box(0.14, 2.6, 1.3), '#3a3e48', tf(4.05, Y + 6 + r * 6.5, -26 + i * 6.4));
  k.f(box(4, 14, 6), white, tf(4, Y + 37, -10));
  k.f(gable(4.4, 4, 6.4, 0.2), slate, tf(4, Y + 44, -10, 1, 1, 1, 0, Math.PI / 2, 0));
  // Torre alta e fina (a da silhueta).
  k.f(cyl(4.2, 4.5, 46, 12), white, tf(2, Y + 23, 16));
  k.f(cyl(5.2, 5.2, 1.2, 12), '#d8d2c4', tf(2, Y + 46.6, 16));
  k.f(cone(5.2, 16, 12), slate, tf(2, Y + 55, 16));
  k.f(cyl(0.12, 0.12, 4, 4), '#c8a040', tf(2, Y + 64, 16));
  // Torrezinhas nos cantos.
  for (const [x, z, h] of [[4, -30, 30], [-12, -30, 28], [4, 9, 32], [-12, 9, 30]] as const) {
    k.f(cyl(1.7, 1.7, 9, 8), white, tf(x, Y + h + 4.5, z));
    k.f(cone(2.2, 7.5, 8), slate, tf(x, Y + h + 12.7, z));
  }
  // Torre quadrada e a ala dos cavaleiros.
  k.f(box(9, 40, 9), white, tf(-8, Y + 20, 26));
  k.f(hip(10.4, 8, 10.4), slate, tf(-8, Y + 40, 26));
  k.f(box(11, 20, 24), white, tf(-4, Y + 10, 36));
  k.f(gable(11, 7, 24.6, 0.4), slate, tf(-4, Y + 20, 36));
  // Portaria de tijolo avermelhado no fundo.
  k.f(box(12, 18, 22), '#c9866a', tf(-8, Y + 9, 56));
  k.f(gable(12, 6, 22.6, 0.4), slate, tf(-8, Y + 18, 56));
  for (const z of [46, 66]) { k.f(cyl(2, 2, 22, 8), '#c9866a', tf(-1, Y + 11, z)); k.f(cone(2.6, 6, 8), slate, tf(-1, Y + 25, z)); }
  // Pinheiros no penhasco.
  for (let i = 0; i < 14; i++) {
    const a = (i / 14) * Math.PI * 2; const r = 48 + hash2(i, 81) * 14;
    const x = Math.cos(a) * r * 0.8; const z = Math.sin(a) * r * 1.3;
    const h = 12 + hash2(i, 82) * 8;
    k.f(cone(3.6, h, 6), i % 2 ? '#2a5434' : '#23482c', tf(x, h / 2 + 2, z));
  }
  return k.model(Y + 65, { shade: [0.8, 1.05] });
}

/** Torre Eiffel: quatro pés em curva com os arcos, duas plataformas, fuste treliçado; luzes douradas. */
function torreEiffel(): Model {
  const k = new Kit();
  const brown = '#8a6a4c'; const gold = '#ffc860';
  const hw = (y: number): number => (y <= 276 ? 62 * Math.exp(-y / 100) : 3.9 - ((y - 276) / 24) * 2.2);
  const legW = (y: number): number => (y <= 57 ? 26 - (y / 57) * 12 : 14 + ((y - 57) / 59) * (hw(116) - 14));
  for (let q = 0; q < 4; q++) {
    const sx = q === 0 || q === 3 ? 1 : -1; const sz = q < 2 ? 1 : -1;
    const ys = [0, 28, 57, 86, 116];
    const ring = (y: number): P3[] => {
      const o = hw(y); const i = Math.max(0.5, o - legW(y));
      return [[sx * o, y, sz * o], [sx * i, y, sz * o], [sx * i, y, sz * i], [sx * o, y, sz * i]];
    };
    for (let s = 0; s + 1 < ys.length; s++) {
      const a = ring(ys[s]); const b = ring(ys[s + 1]);
      const w = s < 2 ? 1.8 : 1.3;
      for (let c = 0; c < 4; c++) k.bar(brown, a[c], b[c], w);
      for (let c = 0; c < 4; c++) k.bar(brown, b[c], b[(c + 1) % 4], w * 0.7);
      k.bar(brown, a[0], b[1], w * 0.55).bar(brown, a[0], b[3], w * 0.55);
      k.bar(gold, [a[0][0] * 1.01, a[0][1], a[0][2] * 1.01], [b[0][0] * 1.01, b[0][1], b[0][2] * 1.01], 0.5, 0.5, true);
    }
    k.f(box(18, 3, 18), '#9a948a', tf(sx * (hw(0) - 13), 1.5, sz * (hw(0) - 13)));
  }
  // Arcos decorativos entre os pés, sob a primeira plataforma.
  for (let q = 0; q < 4; q++) {
    let prev: P3 | null = null;
    for (let i = 0; i <= 10; i++) {
      const t = i / 10; const u = -34 * Math.cos(Math.PI * t); const y = 6 + 33 * Math.sin(Math.PI * t);
      const x = hw(y) - legW(y) * 0.35;
      const p: P3 = q === 0 ? [x, y, u] : q === 1 ? [-x, y, u] : q === 2 ? [u, y, x] : [u, y, -x];
      if (prev) k.bar(brown, prev, p, 1.2);
      prev = p;
    }
  }
  // Plataformas.
  const p1 = hw(57) + 2; const p2 = hw(116) + 1.5;
  k.f(box(p1 * 2, 6, p1 * 2), brown, tf(0, 60, 0));
  k.l(box(p1 * 2 + 0.3, 1.0, p1 * 2 + 0.3), gold, tf(0, 61.5, 0));
  k.f(box(p2 * 2, 4, p2 * 2), brown, tf(0, 118, 0));
  k.l(box(p2 * 2 + 0.3, 0.8, p2 * 2 + 0.3), gold, tf(0, 119, 0));
  // Fuste até o topo.
  const lv: Array<[number, number]> = [116, 150, 185, 220, 250, 276].map((y) => [y, hw(y)] as [number, number]);
  latticeTower(k, lv, () => brown, (i) => 1.4 - i * 0.15, true, gold);
  k.f(box(9, 6, 9), brown, tf(0, 279, 0));
  k.l(box(9.2, 1.2, 9.2), gold, tf(0, 279, 0));
  k.f(cyl(1.6, 3.6, 18, 8), brown, tf(0, 291, 0));
  k.f(cyl(0.4, 0.9, 24, 6), '#b8b4ac', tf(0, 312, 0));
  k.b(sphere(1.2, 6, 4), '#ffe0a0', tf(0, 300.5, 0));
  return k.model(324, { shade: [0.82, 1.08] });
}

/** Arco do Triunfo: arco central de 29 m, arcos laterais, ático com cornijas, relevos; luz sob a abóbada. */
function arcoTriunfo(): Model {
  const k = new Kit();
  const lime = '#e3d8c0';
  k.f(box(26, 1.2, 49), '#cfc6b4', tf(0, 0.6, 0));
  const front = extrude(archOutline(45, 36, [[0, 14.6, 29.2]]), 6.8, 10);
  k.f(front, lime, tf(7.6, 0, 0, 1, 1, 1, 0, Math.PI / 2, 0));
  k.f(extrude(archOutline(45, 36, [[0, 14.6, 29.2]]), 6.8, 10), lime, tf(-7.6, 0, 0, 1, 1, 1, 0, Math.PI / 2, 0));
  for (const sz of [-1, 1]) {
    k.f(extrude(archOutline(22, 36, [[0, 8.4, 18.7]]), 3, 8), lime, tf(0, 0, sz * 21));
    k.f(box(8.4, 17.3, 12.2), lime, tf(0, 27.35, sz * 13.4));
  }
  k.f(box(8.4, 6.8, 14.6), lime, tf(0, 32.6, 0));
  k.l(box(8.0, 0.2, 14.0), '#ffd28a', tf(0, 29.1, 0));
  // Cornijas, friso e ático.
  k.f(box(23.2, 1.2, 46.2), '#d8ccb2', tf(0, 36.6, 0));
  k.f(box(22.4, 11, 45.2), lime, tf(0, 42.7, 0));
  k.f(box(23.6, 1.4, 46.6), '#d8ccb2', tf(0, 48.9, 0));
  k.f(box(22, 1.2, 44.6), '#e8dec8', tf(0, 50.2, 0));
  for (const sx of [1, -1]) {
    k.f(box(0.4, 2.4, 45.2), '#d2c6ac', tf(sx * 11.15, 33.6, 0));
    for (const z of [-15.4, 15.4]) {
      k.f(box(0.6, 11, 7), '#d4c8ae', tf(sx * 11.25, 11, z));
      k.f(box(0.5, 5, 6), '#cbbfa4', tf(sx * 11.3, 22.5, z));
    }
    for (let i = 0; i < 10; i++) k.f(cyl(0.9, 0.9, 0.3, 8), '#d2c6ac', tf(sx * 11.3, 45, -20 + i * 4.45, 1, 1, 1, 0, 0, Math.PI / 2));
  }
  k.l(box(0.15, 0.4, 44), '#ffe2a8', tf(11.5, 36, 0)).l(box(0.15, 0.4, 44), '#ffe2a8', tf(-11.5, 36, 0));
  return k.model(51, { shade: [0.82, 1.05] });
}

/**
 * Matterhorn: a trompa de pedra de quatro arestas (a do Hörnli aponta para a pista), faces côncavas e íngremes que
 * se fecham num cume torto (o "gancho" cai para +Z), ombro no Hörnli e o do Pic Tyndall na aresta de trás; neve em
 * estrias nas faces (mais na face "norte", a de +X+Z), geleira e morena no sopé largo (escala do horizonte).
 */
function matterhorn(): Model {
  const k = new Kit();
  const H = 300; const Rb = 205; const K = 32;
  // Raio relativo por altura (t = y/H): sopé largo de geleira, a trompa íngreme (≈ 60°) do meio para cima.
  const prof: Array<[number, number]> = [[-0.03, 1.05], [0, 1.0], [0.06, 0.79], [0.13, 0.65], [0.22, 0.54], [0.34, 0.43], [0.47, 0.33], [0.6, 0.235], [0.72, 0.158], [0.82, 0.097], [0.9, 0.055], [0.96, 0.024]];
  const ts = [-0.03, 0, 0.04, 0.08, 0.13, 0.19, 0.26, 0.34, 0.42, 0.5, 0.58, 0.65, 0.72, 0.78, 0.84, 0.89, 0.93, 0.965];
  // Alcance de cada aresta (0 = +X Hörnli, 1 = +Z, 2 = −X, 3 = −Z Lion) com os ombros.
  const bump = (t: number, c: number, w: number, a: number): number => a * Math.exp(-(((t - c) / w) ** 2));
  const reach = (q: number, t: number): number => [1.14 + bump(t, 0.7, 0.07, 0.42), 0.94 + bump(t, 0.4, 0.1, 0.1), 1.0, 1.08 + bump(t, 0.52, 0.07, 0.3)][q & 3];
  // O gancho: o terço de cima tomba para +Z e um pouco para trás.
  const cx = (t: number): number => -10 * t - 8 * smooth(0.7, 1, t);
  const cz = (t: number): number => 24 * smooth(0.6, 1, t) ** 1.5;
  const vert = (j: number, i: number): P3 => {
    const t = ts[j]; const th = (i / K) * Math.PI * 2;
    const c = Math.abs(Math.cos(th)); const s = Math.abs(Math.sin(th));
    const star = 1 / Math.pow(Math.pow(c, 0.88) + Math.pow(s, 0.88), 1 / 0.88); // 1 na aresta, 0,65 no meio da face
    const q = Math.floor(th / (Math.PI / 2)); const f = smooth(0, 1, th / (Math.PI / 2) - q);
    const r = Rb * table(prof, t) * star * (reach(q, t) * (1 - f) + reach(q + 1, t) * f) * (1 + 0.08 * noise2(301, i * 0.9, j * 0.8));
    const y = t <= 0 ? t * H : t * H + 5 * noise2(302, i * 1.3, j);
    return [cx(t) + Math.cos(th) * r, y, cz(t) + Math.sin(th) * r];
  };
  const out: number[] = [];
  for (let j = 0; j + 1 < ts.length; j++) {
    for (let i = 0; i < K; i++) {
      const a = vert(j, i); const b = vert(j, (i + 1) % K); const c = vert(j + 1, (i + 1) % K); const d = vert(j + 1, i);
      out.push(...a, ...d, ...c, ...a, ...c, ...b);
    }
  }
  const apex: P3 = [cx(1) + 4, H, cz(1)];
  for (let i = 0; i < K; i++) out.push(...vert(ts.length - 1, i), ...apex, ...vert(ts.length - 1, (i + 1) % K));
  const g = paint(tris(out), '#5f5852');
  faceColor(g, (x, y, z, up, fi) => {
    const t = y / H;
    const th = Math.atan2(z - cz(t), x - cx(t));
    const ridge = Math.abs(((th + Math.PI * 2) % (Math.PI / 2)) - Math.PI / 4); // π/4 na aresta, 0 no meio da face
    if (t < 0.07) return noise2(303, x / 40, z / 40) > -0.35 ? (up > 0.75 ? '#eef3f8' : '#dfe8f0') : '#8a847c';
    if (t < 0.17) return up > 0.55 || noise2(304, x / 30, z / 30) > 0.25 ? '#e8eef4' : '#7a736b';
    const north = th > 0 && th < Math.PI / 2 ? 0.2 : 0; // a face de +X+Z (a "norte") guarda mais neve
    const streak = noise2(305, (th / (Math.PI * 2)) * 40, t * 3.2) + north + (up - 0.4) * 1.1 - (ridge > 0.66 ? 0.2 : 0) - (t > 0.92 ? 0.15 : 0);
    if (streak > 0.2) return up > 0.5 ? '#f4f7fb' : '#e2e9f0';
    if (ridge > 0.7) return '#4a4440';
    return hash2(fi, 306) > 0.5 ? '#625a53' : (t > 0.55 ? '#6e6359' : '#57514c');
  });
  k.raw(speckle(g, 0.04, 307));
  return k.model(H, { shade: [0.9, 1.04] });
}

/** Capela de pedra dos Alpes: torre com cúpula de cebola verde, telhado íngreme, cruz de caminho. */
function capelaAlpina(): Model {
  const k = new Kit();
  const wall = '#f2efe6'; const roof = '#4a4a52';
  k.raw(speckle(paint(box(13, 1.2, 9), '#8f8a82', tf(-1, 0.4, 0)), 0.1, 101));
  k.f(box(11, 6.5, 7.5), wall, tf(-1.5, 4, 0));
  for (const [x, z] of [[4, 3.75], [4, -3.75], [-7, 3.75], [-7, -3.75]] as const) k.f(box(0.7, 6.5, 0.7), '#a8a296', tf(x, 4, z));
  k.f(gable(8.3, 5, 12.4, 0.2), roof, tf(-1.5, 7.25, 0, 1, 1, 1, 0, Math.PI / 2, 0));
  k.f(cyl(3.3, 3.3, 6.5, 10, false), wall, tf(-7, 4, 0, 1, 1, 1, 0, 0, 0));
  k.f(cone(3.6, 3.4, 10), roof, tf(-7, 8.9, 0));
  for (const sz of [-1, 1]) for (const x of [-4, 0]) k.f(box(1, 2.2, 0.12), '#3a4250', tf(x, 5, sz * 3.8));
  // Torre na frente (+X) com relógio e cúpula de cebola.
  k.f(box(3.4, 11, 3.4), wall, tf(4.8, 6.2, 0));
  k.f(box(3.6, 0.4, 3.6), '#a8a296', tf(4.8, 11.8, 0));
  k.f(box(0.12, 1.4, 1.0), '#2a2e36', tf(6.52, 10.3, 0));
  k.l(cyl(0.7, 0.7, 0.1, 10), '#fff2c8', tf(6.55, 8.6, 0, 1, 1, 1, 0, 0, Math.PI / 2));
  const onion = lathe([[0, 0], [1.9, 0], [2.1, 0.8], [2.0, 1.7], [1.3, 2.6], [0.45, 3.3], [0.3, 4.0], [0.6, 4.5], [0.25, 5.0], [0, 5.3]], 10);
  k.f(onion, '#4f8a6a', tf(4.8, 12.0, 0));
  k.f(box(0.12, 1.4, 0.12), '#c8a040', tf(4.8, 18.0, 0)).f(box(0.12, 0.12, 0.8), '#c8a040', tf(4.8, 18.2, 0));
  k.f(box(0.2, 2.6, 1.8), '#5a3e28', tf(6.55, 1.9, 0));
  // Rochas, cruz de caminho e banco.
  k.raw(speckle(paint(roughen(paint(ico(1, 1), '#8a8680', tf(2, 0.6, 8, 3, 1.8, 2.4)), 0.4, 103), '#8a8680'), 0.1, 104));
  k.f(box(0.3, 4, 0.3), '#6a4a30', tf(5, 2, -7)).f(box(0.3, 0.3, 1.8), '#6a4a30', tf(5, 3.2, -7));
  k.f(gable(0.9, 0.5, 2.2, 0.1), '#4a3a2a', tf(5, 4.0, -7));
  k.f(box(0.6, 0.15, 2.2), '#7a5a3a', tf(6, 0.6, -3.5));
  return k.model(18, { shadow: true, blob: 6 });
}

/** Cassino de Monte Carlo: fachada Beaux-Arts creme, torres de cúpula verde, janelas em arco acesas. */
function cassinoMonteCarlo(): Model {
  const k = new Kit();
  const cream = '#efdfbc'; const copper = '#6aa892'; const warm = '#ffd27a';
  k.f(box(30, 0.3, 70), '#d8cfc0', tf(14, 0.15, 0));
  k.f(cyl(1, 1, 0.4, 16), '#4f9a4a', tf(16, 0.3, 0, 9, 1, 22));
  k.f(box(26, 18, 52), cream, tf(-13, 9, 0));
  k.f(box(27, 0.8, 53), '#f6ead0', tf(-13, 18.2, 0));
  k.f(hip(27, 4.2, 53, 20, 46), '#5f7f7a', tf(-13, 18.6, 0));
  // Torres das pontas com cúpula quadrada em sino e lanterna.
  for (const sz of [-1, 1]) {
    k.f(box(11, 26, 11), cream, tf(-5, 13, sz * 29));
    k.f(box(11.8, 0.8, 11.8), '#f6ead0', tf(-5, 26.2, sz * 29));
    k.f(lathe([[7.6, 0], [7.4, 1.4], [6.4, 3.4], [4.4, 5.6], [2.2, 7.2], [1.4, 7.6]], 4, Math.PI / 4), copper, tf(-5, 26.6, sz * 29));
    k.f(cyl(1.4, 1.4, 2.6, 8), cream, tf(-5, 35.4, sz * 29));
    k.f(cone(1.6, 3.4, 8), copper, tf(-5, 38.4, sz * 29));
    for (const y of [9, 19]) k.l(box(0.12, 4, 2.2), warm, tf(0.56, y, sz * 29));
    k.l(cyl(1.45, 1.45, 1.2, 8, true), warm, tf(-5, 35.4, sz * 29));
  }
  // Pavilhão central com relógio.
  k.f(box(9, 23, 18), cream, tf(-3, 11.5, 0));
  k.f(hip(10, 5, 19, 4, 12), '#5f7f7a', tf(-3, 23, 0));
  k.l(cyl(1.6, 1.6, 0.2, 14), '#fff6dc', tf(1.6, 19.5, 0, 1, 1, 1, 0, 0, Math.PI / 2));
  for (const z of [-5, 0, 5]) { k.l(box(0.14, 6.5, 2.6), warm, tf(1.56, 8, z)); k.f(cyl(1.3, 1.3, 0.2, 8, false), cream, tf(1.62, 11.2, z, 1, 1, 1, 0, 0, Math.PI / 2)); }
  for (let i = 0; i < 6; i++) k.f(box(0.6, 2.2, 0.6), '#e8dcc0', tf(1.4, 25.2, -6 + i * 2.4));
  // Janelas em arco da fachada (duas fileiras).
  for (let z = -22; z <= 22; z += 5.5) {
    if (Math.abs(z) < 9) continue;
    k.l(box(0.14, 4.4, 2.1), warm, tf(0.04, 5.6, z));
    k.l(box(0.14, 3.0, 1.6), warm, tf(0.04, 13.4, z));
    k.f(box(0.5, 18, 0.6), '#f8eedb', tf(0.1, 9, z + 2.75));
  }
  k.f(box(4, 0.4, 12), '#3a4a48', tf(3.4, 5.6, 0));
  k.l(box(3.6, 0.1, 11.4), warm, tf(3.4, 5.35, 0));
  k.l(box(0.2, 0.3, 52), '#fff0c8', tf(0.15, 17.6, 0));
  // Duas palmeiras no jardim.
  for (const z of [-16, 16]) {
    k.f(cyl(0.3, 0.45, 9, 6), '#8a6a4a', tf(18, 4.5, z));
    for (let i = 0; i < 7; i++) k.f(frond(4.2, 1.1, (i / 7) * Math.PI * 2, 0.6, 2.2, 3), '#3f8a3a', tf(18, 9, z));
  }
  return k.model(40, { shadow: true, blob: 0 });
}

// ───────────────────────────── África do Sul ─────────────────────────────

/** Portão do Kruger: pilares de pedra, viga e telhado de sapé sobre a entrada, cancela, guarita redonda, placa. */
function portaoKruger(): Model {
  const k = new Kit();
  const thatch = '#b89a5a'; const stone = '#a08868';
  k.f(box(10, 0.15, 16), '#b8a888', tf(2, 0.07, 0));
  for (const sz of [-1, 1]) k.raw(speckle(roughen(paint(box(2.2, 6, 2.2), stone, tf(0, 3, sz * 5.2)), 0.12, 111 + sz), 0.12, 112));
  k.f(box(1.0, 0.8, 13), '#5a3e28', tf(0, 6.4, 0));
  k.f(hip(6, 3.4, 15, 0.6, 11), thatch, tf(0, 6.8, 0));
  k.f(box(6.3, 0.25, 15.3), '#8a7040', tf(0, 6.85, 0));
  // Cancela vermelha e branca.
  k.f(box(0.5, 1.4, 0.5), '#e8e4da', tf(2.4, 0.7, -4.2));
  for (let i = 0; i < 8; i++) k.f(box(0.18, 0.18, 1.0), i % 2 ? '#f2f2ee' : '#d63a3a', tf(2.4, 1.25, -3.4 + i * 1.0));
  // Guarita redonda de sapé.
  k.f(cyl(3.4, 3.4, 3.2, 12), '#e8dcc4', tf(1, 1.6, 11));
  k.f(cone(4.6, 4.2, 12), thatch, tf(1, 5.3, 11));
  k.f(box(0.12, 1.2, 1.2), '#3a2e22', tf(4.42, 2, 11));
  k.f(box(0.12, 2.2, 1.1), '#5a3e28', tf(4.0, 1.1, 9.4, 1, 1, 1, 0, -0.35, 0));
  // Placa de madeira com KRUGER e chifres de kudu.
  for (const z of [-12.6, -9.4]) k.f(cyl(0.2, 0.24, 3.6, 6), '#6a4a30', tf(1.5, 1.8, z));
  const s = new Kit();
  s.f(box(4.6, 1.6, 0.3), '#6a4a2e');
  signText(s, 'KRUGER', 0.8, '#f2c33a', new THREE.Matrix4(), 0.17);
  for (const sx of [-1, 1]) {
    let prev: P3 = [sx * 0.4, 0.8, 0];
    for (let i = 1; i <= 5; i++) { const a = i * 0.9; const p: P3 = [sx * (0.4 + i * 0.22 + Math.cos(a) * 0.12), 0.8 + i * 0.28, Math.sin(a) * 0.18]; s.bar('#d8ccb4', prev, p, 0.1); prev = p; }
  }
  k.add(s, tf(1.5, 3.4, -11, 1, 1, 1, 0, Math.PI / 2 - 0.4, 0));
  return k.model(10.5, { shadow: true, blob: 5 });
}

/** Uma girafa de perfil para a pista (corpo em Z): manchas por face, pescoço alto, ossicones. */
function giraffe(k: Kit, m: THREE.Matrix4, seed: number): void {
  const s = new Kit();
  const spot = (g: Geo): Geo => faceColor(g, (_x, _y, _z, _u, f) => (hash2(f, seed) < 0.55 ? '#b5652a' : '#f1dfb3'));
  s.raw(spot(paint(ico(1, 1), '#c88a4a', tf(0, 2.65, 0, 0.72, 0.82, 1.35))));
  for (const [x, z, h] of [[0.32, 0.85, 2.35], [-0.32, 0.85, 2.35], [0.3, -0.85, 2.1], [-0.3, -0.85, 2.1]] as const) {
    s.raw(spot(paint(cyl(0.1, 0.14, h, 5), '#c88a4a', tf(x, h / 2, z))));
    s.f(box(0.2, 0.16, 0.24), '#2a2018', tf(x, 0.08, z));
  }
  const neck = paint(cyl(0.2, 0.36, 2.6, 6), '#c88a4a', along([0, 2.9, 0.95], [0, 5.1, 1.75]));
  s.raw(spot(neck));
  s.bar('#5a3a22', [0, 3.15, 0.62], [0, 5.25, 1.5], 0.08, 0.12);
  s.raw(spot(paint(box(0.36, 0.4, 0.78), '#c88a4a', tf(0, 5.2, 2.0, 1, 1, 1, 0.35, 0, 0))));
  s.f(box(0.3, 0.26, 0.3), '#e8d4a8', tf(0, 5.05, 2.4));
  for (const x of [-0.1, 0.1]) { s.f(cyl(0.04, 0.05, 0.4, 4), '#7a5030', tf(x, 5.55, 1.85)); s.f(sphere(0.07, 5, 3), '#2a2018', tf(x, 5.76, 1.85)); }
  for (const x of [-0.24, 0.24]) s.f(cone(0.08, 0.3, 4), '#c88a4a', tf(x, 5.42, 1.78, 1, 1, 1, 0, 0, x > 0 ? -1.2 : 1.2));
  s.bar('#c88a4a', [0, 3.0, -1.25], [0, 1.9, -1.45], 0.06);
  s.f(cone(0.1, 0.35, 5), '#2a2018', tf(0, 1.75, -1.47, 1, 1, 1, Math.PI, 0, 0));
  k.add(s, m);
}

/**
 * O par de girafas. A girafa baixada (peça `girafa`, parts.ts) entra no lugar das procedurais, com 6,2 m (a cabeça da
 * procedural fica em 5,8 m e o pescoço passa dela; um marco de perto tem de ter ≥ 6 m; a menor × 0,7) e com a cabeça
 * (+X da peça) para onde a procedural olha (+Z local girado `ry`).
 */
function girafa(): Model {
  const k = new Kit();
  const part = landmarkPart('girafa');
  for (const [x, z, ry, sc, seed] of [[0, -2.5, 0.25, 1, 121], [1.5, 3.0, -0.4, 0.7, 122]] as const) {
    if (part) k.raw(part.at({ height: 6.2 * sc }, x, 0, z, ry - Math.PI / 2));
    else giraffe(k, tf(x, 0, z, sc, sc, sc, 0, ry, 0), seed);
  }
  return k.model(5.9, { shadow: true, blob: 2.5 });
}

/** Igreja reformada do Karoo: branca neogótica em cruz, torre com coruchéu alto, janelas ogivais, muro baixo. */
function igrejaKaroo(): Model {
  const k = new Kit();
  const white = '#f4f2ec'; const roof = '#5a5e66'; const dark = '#3a4048';
  k.f(box(28, 9, 11), white, tf(-2, 4.5, 0));
  k.f(gable(11, 6, 28.6, 0.4), roof, tf(-2, 9, 0, 1, 1, 1, 0, Math.PI / 2, 0));
  k.f(box(9, 9, 25), white, tf(-6, 4.5, 0));
  k.f(gable(9.4, 6, 25.6, 0.4), roof, tf(-6, 9, 0));
  for (const sz of [-1, 1]) {
    k.f(tris2([-4.5, 0, 0, 4.5, 0, 0, 0, 5.8, 0]), white, tf(-6, 9, sz * 12.52));
    k.f(box(1.4, 4.4, 0.14), dark, tf(-6, 5, sz * 12.56)).f(cone(0.7, 1.2, 4), dark, tf(-6, 7.7, sz * 12.56, 1, 1, 0.1));
    for (const x of [-14, -10, 2, 6]) { k.f(box(1.1, 4.0, 0.14), dark, tf(x, 4.6, sz * 5.56)); k.f(box(0.7, 7.2, 1), white, tf(x + 2, 3.6, sz * 5.9)); }
  }
  // Torre da frente (+X) com pináculos e coruchéu.
  k.f(box(5.4, 19, 5.4), white, tf(14.6, 9.5, 0));
  k.f(box(5.8, 0.6, 5.8), '#d8d6d0', tf(14.6, 19.3, 0));
  for (const [x, z] of [[2.5, 2.5], [2.5, -2.5], [-2.5, 2.5], [-2.5, -2.5]] as const) k.f(cone(0.5, 3, 4), white, tf(14.6 + x, 21, z));
  k.f(cone(3.3, 22, 8), '#d8d8d4', tf(14.6, 30.6, 0, 1, 1, 1, 0, Math.PI / 8, 0));
  k.f(box(0.14, 2.4, 0.14), '#3a3a3a', tf(14.6, 42.6, 0)).f(box(0.14, 0.14, 1.2), '#3a3a3a', tf(14.6, 42.9, 0));
  k.f(box(0.14, 3.6, 1.6), dark, tf(17.34, 14.5, 0)).f(cone(0.8, 1.2, 4), dark, tf(17.34, 16.9, 0, 0.1, 1, 1));
  k.f(box(0.14, 3.4, 2.2), '#5a3e28', tf(17.34, 1.7, 0));
  k.l(cyl(0.9, 0.9, 0.1, 12), '#fff2c8', tf(17.4, 11, 0, 1, 1, 1, 0, 0, Math.PI / 2));
  // Muro do adro.
  for (const [x, z, w, d] of [[22, 0, 0.6, 30], [-20, 0, 0.6, 30], [1, 15, 42, 0.6], [1, -15, 42, 0.6]] as const) k.f(box(w, 1.2, d), white, tf(x, 0.6, z));
  return k.model(43, { shadow: true, blob: 0 });
}

/** Anfiteatro do Drakensberg: paredão de basalto em crescente com a cachoeira do Tugela (horizonte). */
function anfiteatroDrakensberg(): Model {
  const k = new Kit();
  const R = 300; const A = 1.0;
  const prof: Array<[number, number]> = [[130, 0], [72, 40], [36, 74], [20, 86], [12, 160], [6, 232], [-8, 240], [-120, 250], [-200, 0]];
  const P = (u: number, j: number): P3 => {
    const th = -A + 2 * A * u;
    const [r0, y0] = prof[j];
    const jag = j >= 3 && j <= 5 ? 16 * valueNoise(141, th * 16) + 7 * valueNoise(142, th * 37) - (j === 4 ? 6 : 0) : 0;
    const r = r0 + jag;
    const e = Math.min(1, Math.min(u, 1 - u) / 0.14); const fall = j >= 3 && j <= 7 ? 0.3 + 0.7 * e * e * (3 - 2 * e) : 1;
    const y = (y0 + (j >= 5 && j <= 7 ? 8 * valueNoise(143, th * 6) : 0)) * fall;
    return [R - (R - r) * Math.cos(th), y, (R - r) * Math.sin(th)];
  };
  const g = paint(grid(56, prof.length - 1, (u, v) => P(u, Math.round(v * (prof.length - 1)))), '#7a6656');
  faceColor(g, (_x, y, _z, up) => {
    if (y < 80 && up > 0.3) return '#6f8f45';
    if (y > 200 && up > 0.5) return '#8a9a5a';
    return Math.floor(y / 18) % 2 ? '#7a6656' : '#8c7663';
  });
  k.raw(speckle(g, 0.06, 144));
  // Cachoeira do Tugela: duas quedas brancas no paredão.
  const u0 = 0.62; const out: number[] = [];
  for (let j = 3; j < 6; j++) {
    const a = P(u0, j); const b = P(u0, j + 1);
    const th = -A + 2 * A * u0; const cx = Math.cos(th); const sz = Math.sin(th);
    const w = 3.5; const off = 3;
    const A0: P3 = [a[0] + off - sz * 0, a[1], a[2] - w]; const A1: P3 = [a[0] + off, a[1], a[2] + w];
    const B0: P3 = [b[0] + off * cx, b[1], b[2] - w * 0.6]; const B1: P3 = [b[0] + off * cx, b[1], b[2] + w * 0.6];
    out.push(...A0, ...A1, ...B1, ...A0, ...B1, ...B0);
  }
  k.raw(paint(tris(out), '#eef4fa'));
  return k.model(252, { shade: [0.9, 1.03] });
}

/**
 * Farol do Cabo da Boa Esperança (Cape Point): a crista estreita do promontório saindo do mar ao longo da pista,
 * falésias de arenito dos dois lados, fynbos (mato oliva) no alto, o farol velho branco no pico com o facho para o
 * mar e o farol novo, menor, na ponta; espuma no pé.
 */
function farolCapePoint(): Model {
  const k = new Kit();
  const crest = (z: number): number => table([[-112, 0], [-100, 14], [-86, 25], [-62, 40], [-42, 33], [-18, 54], [-4, 72], [4, 76], [12, 73], [26, 56], [48, 32], [72, 12], [86, 0]], z);
  const half = (z: number): number => 26 + 12 * (crest(z) / 76);
  const xc = (z: number): number => -10 + 5 * Math.sin(z / 40);
  const hY = (x: number, z: number): number => {
    const u = Math.abs((x - xc(z)) / half(z));
    if (u >= 1) return -1;
    const f = Math.pow(1 - u ** 3, 0.5);
    return crest(z) * f + 3.5 * noise2(451, x / 14, z / 14) * f - 1;
  };
  const g = paint(grid(26, 14, (u, v) => { const z = -112 + 198 * u; const x = 46 - 112 * v; return [x, hY(x, z), z]; }), '#7a7046');
  faceColor(g, (_x, y, _z, up, f) => {
    if (y < 3) return '#5e564e';
    if (up < 0.5) return hash2(f, 452) > 0.5 ? '#80705f' : '#8f7f6b';
    return ['#6f7442', '#7c7a48', '#8a7e52', '#737a46'][Math.floor(hash2(f, 453) * 4)];
  });
  k.raw(speckle(g, 0.06, 454));
  // O farol velho no pico (1,6× o farol do cenário, para ler no alto do penhasco), montado no próprio referencial.
  const lz = 4; const lx = xc(lz); const top = crest(lz) - 1.2; const sc = 1.6;
  const lighthouse = (s: Kit, h: number): void => {
    s.f(box(8, 4, 8), '#d8d4ca', tf(0, -1, 0));
    s.f(cyl(2.0, 2.5, h, 10), '#f6f4ee', tf(0, h / 2 + 1, 0));
    s.f(cyl(2.8, 2.8, 0.4, 10), '#2a2e34', tf(0, h + 1.2, 0));
    s.l(cyl(1.5, 1.5, 2.0, 10), '#fff2c0', tf(0, h + 2.4, 0));
    s.f(cone(1.9, 1.8, 10), '#2a2e34', tf(0, h + 4.3, 0));
  };
  const f = new Kit();
  lighthouse(f, 11);
  f.f(box(4.2, 5, 6), '#f6f4ee', tf(0, 0.2, -6.5));
  f.f(gable(4.6, 1.4, 6.4, 0.2), '#b84a3a', tf(0, 2.7, -6.5));
  k.add(f, tf(lx, top, lz, sc, sc, sc));
  k.beam(new THREE.ConeGeometry(9, 110, 12, 1, true).rotateZ(-Math.PI / 2).translate(-55, 0, 0), tf(lx, top + 13.4 * sc, lz, 1, 1, 1, 0, -0.5, 0));
  // O farol novo, mais baixo, na ponta.
  const n = new Kit();
  lighthouse(n, 8);
  k.add(n, tf(xc(-98), crest(-98) - 1, -98, 1.3, 1.3, 1.3));
  // Espuma no pé do promontório.
  k.f(new THREE.RingGeometry(1, 1.1, 22, 1).rotateX(-Math.PI / 2), '#eef6f8', tf(xc(-12), 0.35, -13, 44, 1, 104));
  return k.model(top + 16 * sc, { shade: [0.85, 1.05] });
}

/** Table Mountain: mesa de arenito de topo plano com a "toalha" de nuvem, Lion's Head e Devil's Peak. */
function tableMountain(): Model {
  const k = new Kit();
  const prof: Array<[number, number]> = [[150, 0], [92, 58], [56, 108], [42, 128], [32, 204], [-60, 210], [-170, 0]];
  const Lz = 330;
  const g = paint(grid(36, prof.length - 1, (u, v) => {
    const j = Math.round(v * (prof.length - 1)); const z = -Lz + 2 * Lz * u;
    const [x0, y0] = prof[j];
    const end = Math.min(1, Math.max(0, (Math.abs(z) - 250) / 80));
    const fall = 1 - end * end * (3 - 2 * end) * 0.75;
    const jag = j === 3 || j === 4 ? 7 * valueNoise(161, z / 22) : 0;
    return [x0 + jag, y0 * fall + (j === 5 ? 4 * valueNoise(162, z / 60) : 0), z];
  }), '#857d70');
  faceColor(g, (_x, y, _z, up) => (y < 110 && up > 0.3 ? '#4f6e3e' : up > 0.8 ? '#6f6e58' : Math.floor(y / 14) % 2 ? '#857d70' : '#948b7c'));
  k.raw(speckle(g, 0.06, 163));
  const peak = (x: number, z: number, h: number, r: number, seed: number, round: number): void => {
    const pg = paint(lathe([[0, h], [r * 0.2, h - round], [r * 0.45, h * 0.62], [r * 0.75, h * 0.28], [r, 0]], 9), '#7d7668', tf(x, 0, z));
    roughen(pg, 6, seed);
    faceColor(pg, (_x, y, _z, up) => (y < h * 0.45 && up > 0.3 ? '#4f6e3e' : Math.floor(y / 14) % 2 ? '#7d7668' : '#8a8274'));
    k.raw(speckle(pg, 0.06, seed + 1));
  };
  peak(10, 350, 196, 112, 165, 18);
  peak(40, -365, 140, 90, 167, 6);
  // A toalha de nuvem: lâmina branca no topo que escorre pela beira do penhasco.
  const cloud: number[] = [];
  const N = 22;
  for (let i = 0; i < N; i++) {
    const z0 = -240 + (480 * i) / N; const z1 = -240 + (480 * (i + 1)) / N;
    const d0 = 26 + 14 * (hash2(i, 168) - 0.3); const d1 = 26 + 14 * (hash2(i + 1, 168) - 0.3);
    const top: P3[] = [[38, 214, z0], [38, 214, z1]];
    const bot: P3[] = [[44, 214 - d0, z0], [44, 214 - d1, z1]];
    cloud.push(...top[0], ...bot[0], ...bot[1], ...top[0], ...bot[1], ...top[1]);
    cloud.push(-60, 216, z0, 38, 214, z0, 38, 214, z1, -60, 216, z0, 38, 214, z1, -60, 216, z1);
  }
  k.raw(paint(tris(cloud), '#f4f6fa'));
  return k.model(214, { shade: [0.9, 1.03] });
}

// ───────────────────────────── Austrália ─────────────────────────────

/**
 * Uluru: o monólito vermelho-alaranjado comprido (ao longo da pista), de flancos quase a prumo e topo arredondado, com
 * ravinas irregulares nos flancos, as manchas escuras de escorrimento descendo deles e o pé sombreado das cavernas
 * (horizonte; 110 m de altura para não sumir na névoa).
 */
function uluru(): Model {
  const k = new Kit();
  const H = 110; const L = 360; const W = 175;
  const gully = (az: number): number => Math.max(0, valueNoise(173, az * 10) + 0.5 * valueNoise(176, az * 23)) ** 1.5;
  const g = new THREE.SphereGeometry(1, 64, 12, 0, Math.PI * 2, 0, Math.PI / 2);
  const p = g.attributes.position as THREE.BufferAttribute;
  for (let i = 0; i < p.count; i++) {
    const x = p.getX(i); const y = p.getY(i); const z = p.getZ(i);
    const hr = Math.hypot(x, z); const az = Math.atan2(z, x);
    const fh = Math.pow(hr, 0.33); const fy = Math.pow(Math.max(0, y), 0.42);
    const groove = 1 - 0.045 * gully(az) * (0.4 + 0.6 * hr);
    const sx = Math.sign(Math.cos(az)) * Math.pow(Math.abs(Math.cos(az)), 0.6); const sz = Math.sign(Math.sin(az)) * Math.pow(Math.abs(Math.sin(az)), 0.6);
    const lump = 1 + 0.07 * valueNoise(171, az * 3) + 0.04 * valueNoise(174, az * 7);
    // Comprido ao longo da pista (Z), como os marcos compridos: o flanco longo olha a estrada.
    p.setXYZ(i, sz * fh * W * groove, fy * H * lump * (1 - 0.1 * Math.abs(Math.cos(az))), sx * fh * L * groove);
  }
  const gg = paint(g, '#b33f17');
  faceColor(gg, (x, y, z, up, f) => {
    const az = Math.atan2(x / W, z / L);
    // Vermelho mais fundo que as mesas e o arenito do terreno do deserto em volta (laranja-claro): destaca na névoa.
    if (y < 7 && hash2(f, 177) > 0.4) return '#7e2c14';
    if (up < 0.6 && (gully(az) > 0.5 || valueNoise(175, az * 31) > 0.72)) return '#8e3416';
    if (y > H * 0.82) return hash2(f, 178) > 0.7 ? '#a8461f' : '#bd5326';
    return hash2(f, 179) > 0.5 ? '#b33f17' : '#a93a17';
  });
  k.raw(speckle(gg, 0.04, 172));
  return k.model(H, { shade: [0.85, 1.05] });
}

/**
 * Uma camada de rocha sedimentar entre z0 e z1, de y0 a y1: em cada z, a meia largura `hw(z, y)` para os dois lados de
 * `cx(z)` (planta irregular), paredes, topo e fundo. Pilha delas = falésia em estratos (London Arch, apóstolos).
 */
function strata(z0: number, z1: number, y0: number, y1: number, hw: (z: number, y: number) => number, cx: (z: number) => number = () => 0, step = 6.5): number[] {
  const n = Math.max(2, Math.ceil((z1 - z0) / step));
  const L0: P3[] = []; const R0: P3[] = []; const L1: P3[] = []; const R1: P3[] = [];
  for (let i = 0; i <= n; i++) {
    const z = z0 + ((z1 - z0) * i) / n; const c = cx(z);
    const b = Math.max(0.3, hw(z, y0)); const t = Math.max(0.3, hw(z, y1));
    L0.push([c - b, y0, z]); R0.push([c + b, y0, z]); L1.push([c - t, y1, z]); R1.push([c + t, y1, z]);
  }
  const o: number[] = [];
  const q = (a: P3, b: P3, c: P3, d: P3): void => { o.push(...a, ...b, ...c, ...a, ...c, ...d); };
  for (let i = 0; i < n; i++) {
    q(L1[i], R1[i], R1[i + 1], L1[i + 1]);
    q(L0[i], L0[i + 1], R0[i + 1], R0[i]);
    q(R0[i], R0[i + 1], R1[i + 1], R1[i]);
    q(L0[i], L1[i], L1[i + 1], L0[i + 1]);
  }
  q(L0[0], R0[0], R1[0], L1[0]);
  q(L0[n], L1[n], R1[n], R0[n]);
  return o;
}

/**
 * London Arch (Great Ocean Road): o rochedo de calcário em estratos horizontais (creme, ocre e ferrugem, cada camada
 * com o contorno um pouco diferente e a de baixo cavada pelo mar), o arco na ponta de fora, mato rasteiro em cima,
 * um "apóstolo" (coluna solta) ao lado e a espuma das ondas em volta.
 */
function arcoGreatOcean(): Model {
  const k = new Kit();
  const bands = ['#e3b47c', '#d29a5e', '#e9c48e', '#c7894f', '#dcaa70', '#b97c48', '#e6bc84'];
  const nL = 13; const hL = 3.5;
  const zA = 24; const ra = 11; const ya = 12; // o arco: centro em z, raio, altura das paredes retas
  const z0 = -58; const z1 = 52;
  // Topo: mais alto do lado de terra (−Z), baixando para o arco; ondula.
  const top = (z: number): number => 46 - 11 * smooth(-20, 48, z) + 2.5 * valueNoise(422, z / 14);
  // Planta: ponta de terra arredondada, ponta de fora mais reta; a camada de baixo cavada pelo mar.
  const plan = (z: number, l: number): number => {
    const zn = Math.min(1, Math.abs((z - (z0 + z1) / 2) / ((z1 - z0) / 2)));
    const ex = z < 0 ? 2 : 3.5;
    const under = l === 0 ? 2.8 : l === 1 ? 1.3 : 0;
    return 16 * Math.pow(1 - zn ** ex, 1 / ex) * (1 + 0.1 * noise2(421, z / 13, l * 0.45)) - under + 0.5 * noise2(429, l * 0.8, 3.1);
  };
  const opening = (y: number): number => (y <= ya ? ra : y < ya + ra ? Math.sqrt(ra * ra - (y - ya) ** 2) : 0);
  const cx = (z: number): number => 1.5 * noise2(425, z / 18, 0.5);
  const yAt = (l: number): number => l * hL + 0.8 * (hash2(l, 432) - 0.5) * (l > 0 && l < nL ? 1 : 0);
  for (let l = 0; l < nL; l++) {
    const y0 = yAt(l); const y1 = yAt(l + 1); const ym = (y0 + y1) / 2;
    // A camada vai até onde o topo ainda passa da metade dela (o topo desce em degraus para o arco).
    let zEnd = z1;
    while (zEnd > z0 + 4 && top(zEnd) < ym) zEnd -= 2;
    if (zEnd <= z0 + 4) break;
    const hw = opening(ym);
    const pieces: Array<[number, number]> = hw > 0.5 ? [[z0, Math.min(zEnd, zA - hw)], [zA + hw, zEnd]] : [[z0, zEnd]];
    const color = bands[(l * 5 + 2) % bands.length];
    const above = (z: number): boolean => l + 1 < nL && top(z) >= y1 + hL / 2 && !(opening(y1 + hL / 2) > Math.abs(z - zA));
    for (const [a, b] of pieces) {
      if (b - a < 1.5) continue;
      const g = paint(tris(strata(a, b, y0, y1, (z, y) => plan(z, l) - (y > y0 + 0.01 ? 0.6 : 0), cx)), color);
      faceColor(g, (_x, y, z, up) => (up > 0.8 && y > y1 - 0.2 && !above(z) ? '#7d8a4c' : color));
      k.raw(speckle(g, 0.05, 426 + l));
    }
  }
  // O apóstolo: coluna solta de calcário na água, além do arco.
  for (let l = 0; l < 8; l++) {
    const y0 = l * 3.6; const y1 = y0 + 3.6;
    const r = (l === 0 ? 5.4 : 6.6) - l * 0.3 + 0.4 * noise2(431, l, 0.5);
    const c = bands[(l * 3 + 1) % bands.length];
    const g = paint(tris(strata(70 - r, 70 + r, y0, y1, (z) => r * Math.sqrt(Math.max(0, 1 - ((z - 70) / r) ** 2)) + 0.4, () => -4)), c);
    if (l === 7) faceColor(g, (_x, _y, _z, up) => (up > 0.8 ? '#7d8a4c' : c));
    k.raw(speckle(g, 0.05, 430 + l));
  }
  // Pedras caídas e a espuma das ondas em volta.
  for (const [x, z, r] of [[16, -34, 4.5], [-14, 44, 3.5], [18, 46, 3]] as const) k.raw(speckle(roughen(paint(ico(r, 0), '#b88a5a', tf(x, r * 0.3, z)), 0.6, 427), 0.06, 428));
  k.f(new THREE.RingGeometry(1, 1.22, 20, 1).rotateX(-Math.PI / 2), '#eef6f8', tf(0, 0.3, -3, 18, 1, 58));
  k.f(new THREE.RingGeometry(1, 1.5, 12, 1).rotateX(-Math.PI / 2), '#eef6f8', tf(-4, 0.3, 70, 8, 1, 8));
  // O arco fica na ponta −Z, a que quem chega vê (mirrorZ).
  mirrorZ(k.flat);
  return k.model(nL * hL, { shade: [0.82, 1.05] });
}

/** Passarela suspensa de Daintree: torre de observação de 24 m e o deque na copa das árvores gigantes. */
function passarelaDaintree(): Model {
  const k = new Kit();
  const steel = '#4a5a3a'; const wood = '#8a6a46';
  const tz = -10;
  for (const [x, z] of [[2, 2], [2, -2], [-2, 2], [-2, -2]] as const) k.bar(steel, [x, 0, tz + z], [x, 24.5, tz + z], 0.3);
  for (let i = 1; i <= 5; i++) {
    const y = i * 4.8;
    k.f(box(5.2, 0.3, 5.2), wood, tf(0, y, tz));
    for (const [a, b] of [[[2.5, 2.5], [2.5, -2.5]], [[2.5, -2.5], [-2.5, -2.5]], [[-2.5, -2.5], [-2.5, 2.5]]] as const) k.bar(steel, [a[0], y + 1.1, tz + a[1]], [b[0], y + 1.1, tz + b[1]], 0.1);
    k.bar(wood, [i % 2 ? 1.6 : -1.6, y - 4.8 + 0.2, tz - 1.6], [i % 2 ? -1.6 : 1.6, y, tz + 1.6], 0.9, 0.15);
  }
  k.f(hip(6.4, 2.2, 6.4), '#3f5a32', tf(0, 25, tz));
  // Árvores gigantes da floresta.
  const tree = (x: number, z: number, h: number, seed: number): void => {
    k.f(cyl(1.0, 1.6, h, 8), '#7a6a58', tf(x, h / 2, z));
    for (let i = 0; i < 3; i++) { const a = (i / 3) * Math.PI * 2 + seed; k.bar('#7a6a58', [x + Math.cos(a) * 3, 0, z + Math.sin(a) * 3], [x + Math.cos(a) * 0.6, 4, z + Math.sin(a) * 0.6], 0.4, 1.4); }
    for (let i = 0; i < 4; i++) {
      const a = (i / 4) * Math.PI * 2 + seed; const r = i === 0 ? 0 : 4.5;
      k.raw(speckle(paint(dodeca(5.5 - (i ? 1 : 0)), i % 2 ? '#2f6a2f' : '#3a7a32', tf(x + Math.cos(a) * r, h + (i ? -1 : 2.5), z + Math.sin(a) * r, 1.2, 0.75, 1.2)), 0.1, seed * 10 + i));
    }
  };
  tree(-1, 18, 26, 1);
  tree(-7, -24, 22, 2);
  k.f(cyl(3.4, 3.4, 0.3, 10), wood, tf(-1, 11, 18));
  // Passarela suspensa em catenária entre a torre e a árvore.
  const path: P3[] = [];
  for (let i = 0; i <= 8; i++) { const t = i / 8; path.push([-0.5 * t, 9.8 + 1.0 * t - 1.6 * Math.sin(Math.PI * t), tz + 2.6 + (18 - 3.2 - tz - 2.6) * t]); }
  k.f(ribbon(path, 1.6, 0.2), wood);
  for (const sx of [0.8, -0.8]) for (let i = 0; i < 8; i++) {
    const a = path[i]; const b = path[i + 1];
    k.bar('#5a4a3a', [a[0] + sx, a[1] + 1.1, a[2]], [b[0] + sx, b[1] + 1.1, b[2]], 0.08);
  }
  return k.model(30, { shadow: true, blob: 4 });
}

/** Uma concha da Ópera: arco ogival que cresce do bico (z = 0) até a boca (z = L), inclinada para a frente. */
function operaShell(k: Kit, m: THREE.Matrix4, W: number, H: number, L: number, seed: number): void {
  const s = new Kit();
  const pt = (t: number, a: number): P3 => {
    const g = Math.pow(Math.sin((Math.PI / 2) * Math.max(0.04, t)), 0.8);
    const w = W * g; const h = H * g;
    const left = a <= 0.5; const q = left ? a / 0.5 : (1 - a) / 0.5;
    const al = Math.PI - q * (Math.PI / 3);
    const x = (w / 2 + w * Math.cos(al)) * (left ? 1 : -1);
    const y = (w * Math.sin(al)) * (h / (0.866 * Math.max(w, 1e-3)));
    return [x, y, L * t + 0.28 * y];
  };
  const g = paint(grid(10, 12, (u, v) => pt(u, v)), '#f4f2ea');
  faceColor(g, (_x, _y, _z, _u, f) => (Math.floor(f / 2) % 2 ? '#f4f2ea' : '#e2dccb'));
  s.raw(g);
  const glass: number[] = [];
  const c: P3 = [0, 0, L * 0.9];
  for (let i = 0; i < 12; i++) { const a = pt(1, i / 12); const b = pt(1, (i + 1) / 12); glass.push(...c, ...[a[0], a[1], a[2] - 1], ...[b[0], b[1], b[2] - 1]); }
  s.raw(paint(tris2(glass), '#ffc878'), true);
  void seed;
  k.add(s, m);
}

/** Ópera de Sydney: pódio de granito e as conchas brancas em duas fileiras, bocas de vidro acesas. */
function operaSydney(): Model {
  const k = new Kit();
  k.f(box(96, 12, 180), '#c4a074', tf(0, 6, 0));
  for (let i = 0; i < 5; i++) k.f(box(60 - i * 3, 1.2, 10), '#d0ae84', tf(4, 0.6 + i * 1.2, -92 - i * 2 + 10));
  const hall = (x: number, s: number): void => {
    const shells: Array<[number, number, number, number, number]> = [
      [-70, 36, 46, 42, 1], [-36, 40, 62, 44, 1], [6, 38, 54, 40, 1], [70, 30, 38, 26, -1],
    ];
    shells.forEach(([z0, W, H, L, dir], i) => {
      const m = dir > 0 ? tf(x, 12, z0 * s) : tf(x, 12, z0 * s, 1, 1, 1, 0, Math.PI, 0);
      operaShell(k, m.multiply(tf(0, 0, 0, s, s, s)), W, H, L, i);
    });
  };
  hall(22, 1);
  hall(-24, 0.82);
  operaShell(k, tf(36, 12, -88, 1, 1, 1, 0, Math.PI, 0), 18, 22, 18, 9);
  k.l(box(96.4, 1.2, 180.4), '#ffe2b0', tf(0, 11.2, 0));
  return k.model(80, { shade: [0.85, 1.05] });
}

/** Harbour Bridge: arco de aço treliçado ("cabide") com os pilones de granito, deque e luzes no arco. */
function harbourBridge(): Model {
  const k = new Kit();
  const steel = '#7d858d'; const deckY = 30; const S = 150; const W = 14;
  const yb = (z: number): number => 80 * (1 - (z / S) ** 2);
  const yt = (z: number): number => 58 + 24 * (1 - (z / S) ** 2);
  const n = 16;
  for (const x of [W, -W]) {
    for (let i = 0; i < n; i++) {
      const z0 = -S + (2 * S * i) / n; const z1 = -S + (2 * S * (i + 1)) / n;
      k.bar(steel, [x, yb(z0), z0], [x, yb(z1), z1], 2.2, 2.6);
      k.bar(steel, [x, yt(z0), z0], [x, yt(z1), z1], 1.6, 2.0);
      k.bar(steel, [x, yb(z0), z0], [x, yt(z1), z1], 0.7);
      k.l(new THREE.OctahedronGeometry(1.5, 0), '#fff2c8', tf(x, yt(z0) + 1.8, z0));
    }
    for (let i = 0; i <= n; i++) {
      const z = -S + (2 * S * i) / n;
      k.bar(steel, [x, yb(z), z], [x, yt(z), z], 0.9);
      if (yb(z) > deckY + 2) k.bar(steel, [x, yb(z), z], [x, deckY, z], 0.4);
      else if (yb(z) < deckY - 2) k.bar(steel, [x, yb(z), z], [x, deckY, z], 1.0);
    }
  }
  for (let i = 1; i < n; i += 2) { const z = -S + (2 * S * i) / n; k.bar(steel, [W, yt(z), z], [-W, yt(z), z], 0.8); }
  k.f(box(2 * W + 6, 3, 470), '#5a6066', tf(0, deckY - 1.5, 0));
  k.f(box(2 * W + 6.4, 0.6, 470), '#8a9096', tf(0, deckY + 0.2, 0));
  for (const sz of [-1, 1]) {
    for (const x of [W, -W]) {
      k.f(box(12, 58, 14), '#c8b89a', tf(x, 29, sz * (S + 9)));
      k.f(box(13, 3, 15), '#b8a888', tf(x, 59.5, sz * (S + 9)));
      k.f(box(10, 4, 12), '#c8b89a', tf(x, 63, sz * (S + 9)));
    }
    for (const z of [180, 205, 228]) k.f(box(2 * W, deckY - 3, 6), '#b8ac94', tf(0, (deckY - 3) / 2, sz * z));
  }
  return k.model(86, { shade: [0.82, 1.05] });
}

// ───────────────────────────── Escandinávia ─────────────────────────────

/**
 * Varre um contorno (pontos [lateral, dy] em volta do eixo) ao longo de um caminho: viga-caixão, parapeito.
 * `scale(i)` estica o contorno em dy (viga mais alta no vão principal). Fecha as duas pontas.
 */
function sweep(path: readonly P3[], section: ReadonlyArray<readonly [number, number]>, scale: (i: number) => number = () => 1): number[] {
  const rings: P3[][] = path.map((p, i) => {
    const a = path[Math.max(0, i - 1)]; const b = path[Math.min(path.length - 1, i + 1)];
    const tx = b[0] - a[0]; const tz = b[2] - a[2]; const n = Math.hypot(tx, tz) || 1;
    const lx = -tz / n; const lz = tx / n; const sc = scale(i);
    return section.map(([l, dy]) => [p[0] + lx * l, p[1] + dy * sc, p[2] + lz * l] as P3);
  });
  const out: number[] = [];
  const m = section.length;
  for (let i = 0; i + 1 < rings.length; i++) {
    for (let j = 0; j < m; j++) {
      const a = rings[i][j]; const b = rings[i][(j + 1) % m]; const c = rings[i + 1][(j + 1) % m]; const d = rings[i + 1][j];
      out.push(...a, ...b, ...c, ...a, ...c, ...d);
    }
  }
  for (const r of [rings[0], rings[rings.length - 1]]) for (let j = 1; j + 1 < m; j++) out.push(...r[0], ...r[j], ...r[j + 1]);
  return out;
}

/**
 * Storseisundet (Estrada do Atlântico): a ponte de concreto que sobe íngreme pelo lado de quem chega (−Z), faz a
 * curva no alto e parece acabar no ar — viga-caixão alta (mais funda no vão principal de 125 m), parapeito branco,
 * postes de luz, pilares-parede no mar e as ilhotas de pedra onde a estrada pousa nas duas pontas.
 */
function ponteStorseisundet(): Model {
  const k = new Kit();
  const path: P3[] = [];
  const top = 27; const zTop = 40;
  for (let i = 0; i <= 30; i++) {
    const z = -140 + i * (280 / 30);
    const y = z < zTop ? 3 + (top - 3) * (1 - ((z - zTop) / 180) ** 2) : 3 + (top - 3) * (1 - ((z - zTop) / 100) ** 2);
    const x = 24 * Math.sin(((z + 140) / 280) * Math.PI) - 12;
    path.push([x, y, z]);
  }
  // Viga-caixão: laje com balanços e a caixa embaixo, mais funda no vão principal.
  const deck: Array<[number, number]> = [[-5, 0], [5, 0], [5, -0.75], [3, -1.1], [2.5, -4.4], [-2.5, -4.4], [-3, -1.1], [-5, -0.75]];
  const depth = (i: number): number => { const z = path[i][2]; return 0.62 + 0.38 * Math.exp(-(((z - 30) / 80) ** 2)); };
  k.raw(paint(tris(sweep(path, deck, depth)), '#c9cac4'));
  k.f(ribbon(path.map(([x, y, z]) => [x, y + 0.06, z] as P3), 8, 0.06), '#4b4b4e');
  for (const off of [4.75, -4.75]) k.raw(paint(tris(sweep(path, [[off - 0.18, 0], [off + 0.18, 0], [off + 0.18, 1.2], [off - 0.18, 1.2]])), '#ebebe6'));
  // Pilares-parede no mar (os do vão principal mais grossos) com a espuma em volta.
  const at = (z: number): P3 => {
    const f = (z + 140) / (280 / 30); const i = Math.min(29, Math.floor(f)); const t = f - i;
    const a = path[i]; const b = path[i + 1];
    return [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t];
  };
  for (const [z, w, d] of [[-115, 4.2, 1.8], [-82, 4.6, 2.0], [-48, 5, 2.2], [-22, 6.4, 3.2], [103, 6.4, 3.2], [122, 4.2, 1.8]] as const) {
    const [x, y] = at(z); const h = y - 3.6 * (Math.abs(z - 30) < 80 ? 1.1 : 0.8) + 2;
    k.f(box(w, h, d), '#b9bab4', tf(x, h / 2 - 2, z));
    k.f(box(w + 1.2, 1.4, d + 1.2), '#a6a7a1', tf(x, 0.4, z));
    k.f(new THREE.RingGeometry(w * 0.7, w * 0.7 + 2.2, 10, 1).rotateX(-Math.PI / 2), '#eef6f8', tf(x, 0.15, z, 1, 1, 0.6));
  }
  // Postes de luz de um lado (acesos à noite).
  for (let i = 2; i < path.length - 1; i += 4) {
    const [x, y, z] = path[i];
    k.bar('#9a9c9e', [x + 4.6, y + 1.2, z], [x + 4.6, y + 7.5, z], 0.22);
    k.bar('#9a9c9e', [x + 4.6, y + 7.5, z], [x + 3.2, y + 7.9, z], 0.18);
    k.l(box(0.9, 0.35, 0.6), '#fff0c8', tf(x + 3.1, y + 7.7, z));
  }
  // Ilhotas de pedra com capim nas pontas, e a estrada que segue rente à água.
  for (const sz of [-1, 1]) {
    const [x0, , z0] = path[sz < 0 ? 0 : path.length - 1];
    const g = paint(ico(1, 1), '#7c7a72', tf(x0 - 2, 0, z0 + sz * 18, 34, 9, 26));
    k.raw(speckle(tintUp(roughen(g, 1.6, 361 + sz), '#6f8a4a', 0.55, 0.9), 0.08, 363 + sz));
    k.f(box(8.4, 1.2, 30), '#55555a', tf(x0, 3.1, z0 + sz * 15));
  }
  // A subida íngreme fica na ponta −Z, a que quem chega vê (mirrorZ).
  mirrorZ([...k.flat, ...k.glow]);
  return k.model(top + 8, { shade: [0.78, 1.05] });
}

/**
 * Vila da Lapônia: o casarão de toras com o telhadão nevado e as janelas acesas, cabanas, duas lavvu (tendas sami)
 * com as varas para fora, a kota sextavada, abetos nevados (um com luzinhas), renas com o trenó e lampiões, num
 * chão de neve de borda irregular.
 */
function vilaLapponia(): Model {
  const k = new Kit();
  const snow: number[] = [];
  const N = 18;
  const rr = (i: number): number => 24 + 5 * valueNoise(461, i * 0.9);
  for (let i = 0; i < N; i++) {
    const a0 = (i / N) * Math.PI * 2; const a1 = ((i + 1) / N) * Math.PI * 2;
    snow.push(0, 0.08, 0, Math.cos(a1) * rr(i + 1) * 0.75, 0.08, Math.sin(a1) * rr(i + 1), Math.cos(a0) * rr(i) * 0.75, 0.08, Math.sin(a0) * rr(i));
  }
  k.raw(paint(tris(snow), '#eaf0f7'));
  const logs = (s: Kit, w: number, h: number, d: number): void => {
    s.f(box(w, h, d), '#5a3a26', tf(0, h / 2, 0));
    for (let i = 0; i < Math.floor(h / 0.75); i++) s.f(box(w + 0.12, 0.12, d + 0.12), '#3e2818', tf(0, 0.6 + i * 0.75, 0));
  };
  // O casarão de toras (dois andares no oitão), frente com janelas acesas e alpendre.
  const L = new Kit();
  logs(L, 10, 4.6, 18);
  L.f(gable(10, 6.2, 18), '#5a3a26', tf(0, 4.6, 0));
  L.f(gable(11.2, 6.6, 19.2, 0.3), '#f4f7fb', tf(0, 4.7, 0));
  for (const z of [-6, -2, 2, 6]) L.l(box(0.12, 1.3, 1.4), '#ffc870', tf(5.04, 2.4, z));
  L.l(box(0.12, 1.2, 1.2), '#ffc870', tf(0, 7.2, 9.04, 1, 1, 1, 0, Math.PI / 2, 0));
  L.f(box(0.12, 2.4, 1.6), '#2a1a10', tf(5.04, 1.2, 0));
  L.f(box(2.6, 0.25, 7), '#f4f7fb', tf(6.2, 3.1, 0, 1, 1, 1, 0, 0, -0.3));
  for (const z of [-3.2, 3.2]) L.f(cyl(0.16, 0.16, 2.8, 6), '#4a3020', tf(7.2, 1.4, z));
  L.f(box(1.1, 3.2, 1.1), '#6f6a66', tf(-2, 9.5, -5));
  k.add(L, tf(-5, 0, -2, 1, 1, 1, 0, 0.12, 0));
  // Cabanas menores.
  const cabin = (x: number, z: number, ry: number): void => {
    const c = new Kit();
    logs(c, 6, 3.2, 8.4);
    c.f(gable(6.0, 3.0, 8.4), '#5a3a26', tf(0, 3.2, 0));
    c.f(gable(6.8, 3.2, 9.2, 0.3), '#f4f7fb', tf(0, 3.25, 0));
    c.f(box(0.9, 2.2, 0.9), '#6f6a66', tf(-1.2, 6.0, 2));
    c.l(box(0.12, 1.0, 1.1), '#ffc870', tf(3.03, 1.8, -2)).l(box(0.12, 1.0, 1.1), '#ffc870', tf(3.03, 1.8, 2));
    c.f(box(0.12, 2.1, 1.0), '#2a1a10', tf(3.03, 1.05, 0));
    k.add(c, tf(x, 0, z, 1, 1, 1, 0, ry, 0));
  };
  cabin(-9, 16, -0.25);
  cabin(4, -19, 0.3);
  // Lavvu (tendas sami) com as varas saindo pela ponta e a fogueira na porta.
  const lavvu = (x: number, z: number, r: number, h: number): void => {
    k.f(cone(r, h, 9), '#d6c6a4', tf(x, h / 2, z));
    k.f(cone(r * 1.01, h * 0.25, 9), '#7a4a32', tf(x, h * 0.125, z));
    for (let i = 0; i < 6; i++) { const a = (i / 6) * Math.PI * 2; k.bar('#5a4030', [x + Math.cos(a) * 0.25, h - 0.6, z + Math.sin(a) * 0.25], [x + Math.cos(a) * 0.8, h + 1.6, z + Math.sin(a) * 0.8], 0.09); }
    k.l(box(0.7, 0.45, 0.7), '#ffb860', tf(x + r + 0.8, 0.25, z));
  };
  lavvu(9, 6, 3.6, 7.5);
  lavvu(12, -6, 2.8, 6);
  // Kota de madeira (sextavada) com cobertura de neve.
  k.f(cyl(3, 3.4, 2.2, 6), '#6a4a30', tf(2, 1.1, 14));
  k.f(cone(3.8, 3.4, 6), '#f0f4f8', tf(2, 3.9, 14));
  k.f(cyl(0.3, 0.3, 1.4, 5), '#3a3a3a', tf(2, 5.6, 14));
  // Abetos nevados; o da frente com luzinhas.
  const spruce = (x: number, z: number, h: number, lights: boolean): void => {
    k.f(cyl(0.25, 0.35, h * 0.2, 5), '#4a3424', tf(x, h * 0.1, z));
    for (let i = 0; i < 4; i++) {
      const y = h * (0.15 + i * 0.2); const r = h * (0.24 - i * 0.045);
      k.f(cone(r, h * 0.32, 7), '#2a4a32', tf(x, y + h * 0.16, z));
      k.f(cone(r * 0.7, h * 0.12, 7), '#eef3f8', tf(x, y + h * 0.27, z));
    }
    if (lights) for (let i = 0; i < 9; i++) { const a = i * 2.4; const t = i / 9; k.l(new THREE.OctahedronGeometry(0.22, 0), ['#ff5040', '#ffd040', '#60c0ff'][i % 3], tf(x + Math.cos(a) * h * 0.2 * (1 - t), h * (0.22 + t * 0.6), z + Math.sin(a) * h * 0.2 * (1 - t))); }
  };
  spruce(-14, -14, 13, false);
  spruce(-16, 4, 11, false);
  spruce(14, 16, 12, false);
  spruce(8, -14, 8, true);
  // Renas e o trenó. A rena baixada (peça `rena`, parts.ts) entra no lugar das procedurais, encaixada pelo lombo na
  // altura do lombo delas (1,475 × 1,3; a galhada fica como veio), a cabeça (+X da peça) para +Z local girado `ry`.
  const reindeerPart = landmarkPart('rena');
  const reindeer = (x: number, z: number, ry: number): void => {
    if (reindeerPart) { k.raw(reindeerPart.at({ back: 1.475 * 1.3 }, x, 0, z, ry - Math.PI / 2)); return; }
    const r = new Kit();
    r.f(box(0.55, 0.65, 1.5), '#6a5444', tf(0, 1.15, 0));
    for (const [lx, lz] of [[0.18, 0.55], [-0.18, 0.55], [0.18, -0.55], [-0.18, -0.55]] as const) r.f(box(0.12, 0.85, 0.12), '#4a3a30', tf(lx, 0.42, lz));
    r.f(box(0.42, 0.45, 0.4), '#e8e2d6', tf(0, 1.3, 0.8));
    r.bar('#6a5444', [0, 1.4, 0.75], [0, 1.85, 1.05], 0.28);
    r.f(box(0.3, 0.3, 0.55), '#6a5444', tf(0, 1.9, 1.25));
    for (const sx of [-1, 1]) {
      r.bar('#cdbb9a', [sx * 0.1, 2.05, 1.1], [sx * 0.45, 2.7, 0.95], 0.06);
      r.bar('#cdbb9a', [sx * 0.3, 2.4, 1.02], [sx * 0.3, 2.7, 1.3], 0.05);
      r.bar('#cdbb9a', [sx * 0.45, 2.7, 0.95], [sx * 0.6, 2.9, 0.7], 0.05);
    }
    k.add(r, tf(x, 0, z, 1.3, 1.3, 1.3, 0, ry, 0));
  };
  reindeer(15, 3, 0.2);
  reindeer(16.5, 0.5, 0.25);
  reindeer(13, -12, 2.4);
  k.f(box(1.2, 0.5, 2.8), '#9a3a2a', tf(15.6, 0.6, -1.4, 1, 1, 1, 0, 0.2, 0)).f(box(1.3, 0.1, 3.3), '#3a3a3a', tf(15.6, 0.25, -1.4, 1, 1, 1, 0, 0.2, 0));
  for (const [x, z] of [[4, -6], [3, 6], [-12, -3]] as const) { k.f(cyl(0.08, 0.1, 2.4, 5), '#2a2a2a', tf(x, 1.2, z)); k.l(box(0.36, 0.44, 0.36), '#ffc870', tf(x, 2.5, z)); }
  return k.model(13, { blob: 0 });
}

/** Placa "cuidado com os trolls": triângulo de borda vermelha com o troll, e um troll de pedra ao lado. */
function placaTrolls(): Model {
  const k = new Kit();
  const tri = (s: number): THREE.Shape => shapeOf([[-s / 2, -s * 0.2887], [s / 2, -s * 0.2887], [0, s * 0.5774]]);
  const s = new Kit();
  s.f(extrude(tri(3.6), 0.14), '#d02a2a');
  s.f(extrude(tri(2.75), 0.2), '#f6f4ee');
  // Pictograma do troll (silhueta preta): corpo, cabeça, narigão, pernas, rabo, braço.
  const p: number[] = [];
  p.push(...fan2d(0.0, -0.12, 0.34, 0.36, 9), ...fan2d(0.12, 0.38, 0.2, 0.2, 8));
  p.push(0.25, 0.42, 0, 0.62, 0.2, 0, 0.26, 0.28, 0);
  p.push(-0.2, -0.3, 0, -0.04, -0.3, 0, -0.26, -0.68, 0, -0.04, -0.3, 0, -0.1, -0.68, 0, -0.26, -0.68, 0);
  p.push(0.06, -0.3, 0, 0.24, -0.3, 0, 0.12, -0.68, 0, 0.24, -0.3, 0, 0.3, -0.68, 0, 0.12, -0.68, 0);
  p.push(-0.3, -0.2, 0, -0.62, -0.55, 0, -0.26, -0.05, 0, -0.62, -0.55, 0, -0.72, -0.62, 0, -0.66, -0.48, 0);
  p.push(0.25, 0.05, 0, 0.5, -0.32, 0, 0.3, -0.08, 0);
  for (const back of [false, true]) {
    const q = back ? p.map((v, i) => (i % 3 === 0 ? -v : v)) : p;
    const g = tris(back ? q : q);
    if (back) {
      const pos = g.attributes.position as THREE.BufferAttribute;
      for (let i = 0; i < pos.count; i += 3) { const x = pos.getX(i + 1); const y = pos.getY(i + 1); pos.setXY(i + 1, pos.getX(i + 2), pos.getY(i + 2)); pos.setXY(i + 2, x, y); }
    }
    s.f(g, '#16181c', tf(0, -0.15, back ? -0.11 : 0.11));
  }
  k.f(cyl(0.07, 0.07, 4.2, 6), '#8a8e94', tf(0, 2.1, 0));
  k.add(s, tf(0.1, 4.9, 0, 1, 1, 1, 0, Math.PI / 2 - 0.5, 0));
  // Troll de pedra musgoso.
  const t = new Kit();
  const moss = '#6f7a5a';
  t.f(ico(1.1, 1), moss, tf(0, 1.7, 0, 1, 1.25, 0.95));
  t.f(ico(0.75, 1), moss, tf(0.2, 3.25, 0));
  t.f(cone(0.28, 1.4, 6), '#7a8466', tf(1.15, 3.1, 0, 1, 1, 1, 0, 0, -Math.PI / 2 + 0.25));
  for (const z of [-0.6, 0.6]) {
    t.f(cone(0.18, 0.6, 5), moss, tf(0.1, 3.4, z * 1.25, 1, 1, 1, z > 0 ? 1.1 : -1.1, 0, 0));
    t.f(sphere(0.11, 5, 3), '#f2eedc', tf(0.82, 3.45, z * 0.38));
    t.f(box(0.5, 1.4, 0.45), '#5f6a4c', tf(0.15, 0.7, z * 0.75));
    t.bar(moss, [0.2, 2.5, z * 1.45], [0.75, 1.2, z * 1.75], 0.38);
  }
  for (let i = 0; i < 5; i++) t.f(cone(0.14, 0.7, 4), '#3a3226', tf(-0.1 + i * 0.08, 3.95, -0.4 + i * 0.2, 1, 1, 1, 0.3 - i * 0.15, 0, 0.3));
  t.bar(moss, [-0.9, 1.0, 0], [-1.7, 0.4, 0.4], 0.3);
  for (const g of t.flat) speckle(g, 0.1, 191);
  // O troll baixado (peça `troll`, parts.ts) entra no lugar do de pedra, da mesma altura (4,3 × 1,1) e no mesmo giro
  // (os dois olham para +X).
  const part = landmarkPart('troll');
  if (part) k.raw(part.at({ height: 4.3 * 1.1 }, 1.0, 0, 4.2, 0.5));
  else k.add(t, tf(1.0, 0, 4.2, 1.1, 1.1, 1.1, 0, 0.5, 0));
  return k.model(7, { shadow: true, blob: 2 });
}

/** Encosta do Trollstigen: x da face na altura y (pé de tálus a ~36°, paredão a ~70° acima, a garganta da cachoeira em z = 0). */
function stigX(z: number, y: number): number {
  const base = y < 52 ? 26 - y * 1.35 : 26 - 52 * 1.35 - (y - 52) * 0.36;
  const gully = 10 * Math.exp(-((z / 20) ** 2)) * smooth(8, 60, y); // rasa: a queda aparece também de quem vem de lado
  return base - gully + (7 * noise2(401, z / 26, y / 34) + 4 * noise2(402, z / 9, y / 12)) * smooth(0, 24, y);
}

/** Crista do paredão do Trollstigen: picos dos lados, o entalhe da cachoeira no meio, as pontas descendo. */
function stigTop(z: number): number {
  const peaks = 168 + 40 * Math.exp(-(((z + 92) / 34) ** 2)) + 24 * Math.exp(-(((z - 88) / 30) ** 2)) - 12 * Math.exp(-((z / 20) ** 2)) + 8 * valueNoise(403, z / 20);
  return peaks * (1 - 0.55 * smooth(98, 130, Math.abs(z)));
}

/**
 * Stigfossen (Trollstigen): o paredão de rocha com a garganta onde a cachoeira desce em degraus brancos, neve na
 * crista entre os picos, o tálus verde no pé — e a estrada dos grampos subindo em zigue-zague ao lado da queda, com
 * muro de pedra, e a ponte de pedra em arco sobre o rio no fundo do vale.
 */
function cachoeiraStigfossen(): Model {
  const k = new Kit();
  const g = paint(grid(40, 16, (u, v) => { const z = -130 + 260 * u; const y = v * stigTop(z); return [stigX(z, y), y, z]; }), '#62666a');
  faceColor(g, (_x, y, z, up, f) => {
    const top = stigTop(z);
    if (y > top - 22 && noise2(404, z / 14, y / 10) > -0.25) return '#eef2f6';
    if (up > 0.5 && y < 60) return hash2(f, 405) > 0.5 ? '#5f7a3c' : '#6b8443';
    if (up > 0.42) return '#577040';
    return hash2(f, 406) > 0.5 ? '#62666a' : '#6e7276';
  });
  k.raw(speckle(g, 0.06, 407));
  // A cachoeira: faixa branca na garganta, mais larga nos degraus, e o poço com o borrifo no pé.
  const fall: number[] = [];
  const yTop = stigTop(0) - 4; const N = 16;
  const pt = (j: number): P3 => { const y = yTop * (1 - j / N); const z = 4 * Math.sin(y / 17); return [stigX(z, y) + 3, y, z]; };
  for (let j = 0; j < N; j++) {
    const a = pt(j); const b = pt(j + 1);
    const wa = 6 + 8 * (j / N) + (j % 4 === 3 ? 3 : 0); const wb = 6 + 8 * ((j + 1) / N) + ((j + 1) % 4 === 3 ? 3 : 0);
    fall.push(a[0], a[1], a[2] - wa, b[0], b[1], b[2] - wb, b[0], b[1], b[2] + wb, a[0], a[1], a[2] - wa, b[0], b[1], b[2] + wb, a[0], a[1], a[2] + wa);
  }
  k.raw(paint(tris(fall), '#f2f7fb'));
  k.f(cyl(13, 13, 0.6, 10), '#7fb6cf', tf(stigX(0, 0) + 8, 0.3, 0, 1, 1, 1.2));
  k.f(dodeca(9), '#f4f8fb', tf(stigX(0, 0) + 6, 2.5, 0, 1.2, 0.5, 1.4));
  // O rio corre do poço para a pista, com corredeiras.
  k.f(box(34, 0.4, 9), '#7fb6cf', tf(stigX(0, 0) + 25, 0.2, 0));
  for (let i = 0; i < 3; i++) k.f(box(2.5, 0.5, 6), '#eef6fa', tf(stigX(0, 0) + 18 + i * 10, 0.3, (hash2(i, 408) - 0.5) * 3));
  // A estrada do vale com a ponte de pedra em arco sobre o rio, ligando os grampos dos dois lados.
  const bx = 40;
  const valley: P3[] = [[stigX(-96, 4) + 3, 4.2, -96], [bx, 1.2, -80], [bx, 1.2, -26], [bx, 4.5, -15], [bx, 6.6, -5], [bx, 6.6, 5], [bx, 4.5, 15], [bx, 1.2, 26], [bx, 1.2, 80], [stigX(96, 4) + 3, 4.2, 96]];
  k.f(ribbon(valley, 7, 7), '#9a968c');
  k.f(ribbon(valley.map(([x, y, z]) => [x, y + 0.06, z] as P3), 5.6, 0.06), '#6c6a67');
  k.f(extrude(archOutline(22, 6, [[0, 9, 5.2]]), 7.2, 8), '#8a8580', tf(bx, 0, 0, 1, 1, 1, 0, Math.PI / 2, 0));
  // Os grampos dos dois lados da queda: pernas que sobem em diagonal pela encosta de tálus, com o muro de pedra
  // embaixo e a curva fechada (os de −Z, mais baixos, são os que quem chega pela direita vê primeiro).
  for (const [side, legs] of [[1, 6], [-1, 4]] as const) for (let L = 0; L < legs; L++) {
    const y0 = 4.2 + L * 8.5; const y1 = y0 + 8.5;
    const zA = side * (L % 2 === 0 ? 96 : 22); const zB = side * (L % 2 === 0 ? 22 : 96);
    const leg: P3[] = [];
    for (let q = 0; q <= 5; q++) { const t = q / 5; const z = zA + (zB - zA) * t; const y = y0 + (y1 - y0) * t; leg.push([stigX(z, y) + 3, y, z]); }
    k.f(ribbon(leg, 6.4, 4.5), '#9a968c');
    k.f(ribbon(leg.map(([x, y, z]) => [x, y + 0.06, z] as P3), 5.2, 0.06), '#6c6a67');
    const [ex, ey, ez] = leg[leg.length - 1];
    k.f(cyl(5.2, 5.2, 4.5, 8), '#9a968c', tf(ex, ey - 2.25, ez + (zB > zA ? 2.5 : -2.5)));
  }
  return k.model(stigTop(-92), { shade: [0.84, 1.05] });
}

/**
 * Catedral Ártica (Tromsø): onze painéis triangulares brancos escalonados (cada um mais baixo e mais estreito que o da
 * frente, então o cume e as águas descem em degraus), as fendas de vidro azulado entre eles (acesas à noite), a
 * grande frente de vidro com a cruz de montantes e o pórtico baixo da entrada.
 */
function catedralArtica(): Model {
  const k = new Kit();
  const white = '#eef2f6'; const edge = '#d4dbe2';
  const n = 11; const step = 4.1;
  for (let i = 0; i < n; i++) {
    const h = 35 - i * 2.45; const b = 15.5 - i * 0.55; const x = -1.7 - i * step;
    k.f(gable(b * 2, h, 2.8), white, tf(x, 0, 0, 1, 1, 1, 0, Math.PI / 2, 0));
    // Aresta de alumínio do painel (o friso cinza que separa cada um do vidro).
    k.f(gable(b * 2 + 0.5, h + 0.45, 0.3), edge, tf(x - 1.5, 0, 0, 1, 1, 1, 0, Math.PI / 2, 0));
    // Fenda de vidro até quase a borda do painel da frente: a linha azulada (acesa à noite) entre um e outro.
    if (i + 1 < n) k.l(gable((b - 0.15) * 2, h - 0.4, 1.3), '#8fb2da', tf(x - 2.05, 0, 0, 1, 1, 1, 0, Math.PI / 2, 0));
  }
  // Frente de vidro com montantes e a cruz.
  k.raw(paint(tris2([0.05, 0, 15.2, 0.05, 0, -15.2, 0.05, 35.4, 0]), '#ffd08a'), true);
  for (let i = -6; i <= 6; i++) { const z = i * 2.2; const h = 35.4 * (1 - Math.abs(z) / 15.2); if (h > 1) k.f(box(0.3, h, 0.3), '#f2f4f6', tf(0.25, h / 2, z)); }
  for (const y of [9, 18, 26]) { const hw = 15.2 * (1 - y / 35.4); k.f(box(0.3, 0.3, hw * 2), '#f2f4f6', tf(0.25, y, 0)); }
  k.f(box(0.5, 19, 1.8), '#f6f8fa', tf(0.4, 14.5, 0)).f(box(0.5, 1.8, 10), '#f6f8fa', tf(0.4, 17.5, 0));
  // Pórtico da entrada e o adro.
  k.f(box(7, 5, 15), white, tf(3.6, 2.5, 0));
  k.l(box(0.12, 3.2, 5), '#ffd08a', tf(7.16, 1.6, 0));
  k.f(box(7.6, 0.5, 15.6), edge, tf(3.6, 5.1, 0));
  k.f(box(64, 0.3, 42), '#e4eaf0', tf(-18, 0.15, 0));
  return k.model(36, { shade: [0.84, 1.05] });
}

// ───────────────────────────── Mediterrâneo ─────────────────────────────

/** Beira-mar de Positano (x): no meio a enseada com a praia; nas pontas (±Z) os esporões de pedra avançam até o mar. */
const positanoShore = (z: number): number => 44 + 12 * smooth(46, 84, Math.abs(z)) - 10 * smooth(100, 115, Math.abs(z));

/** Altura do morro de Positano: encosta de ~48° da praia para o fundo, esporões nas pontas e a crista de pedra no alto. */
function positanoY(x: number, z: number): number {
  const d = positanoShore(z) - x;
  if (d <= 0) return 0;
  const az = Math.abs(z);
  const crest = 9 * valueNoise(215, z / 16) * smooth(110, 150, d);
  // As pontas (|z| > 60) descem até o chão em ±115: a silhueta é a de um morro, não a de uma laje cortada.
  const ends = (1 - 0.8 * smooth(60, 115, az)) * (1 - smooth(103, 115, az));
  return (148 * Math.pow(Math.min(d, 165) / 150, 0.92) + 30 * smooth(44, 92, az) * smooth(0, 22, d) * (1 - smooth(92, 112, az)) + crest) * ends + 2.5 * noise2(211, x / 22, z / 22) * smooth(0, 12, d);
}

/** x da encosta de Positano na altura `y` (busca binária; a encosta sobe para −X). */
function positanoX(y: number, z: number): number {
  let lo = -120; let hi = positanoShore(z);
  for (let i = 0; i < 26; i++) { const m = (lo + hi) / 2; if (positanoY(m, z) > y) lo = m; else hi = m; }
  return (lo + hi) / 2;
}

/**
 * Positano: a vila vertical — casas pastel em degraus, empilhadas fileira sobre fileira na enseada da encosta (as
 * frentes com janelas olham o mar e a pista), esporões de pedra nas pontas, a estrada da Costa Amalfitana cortando o
 * alto da vila, e no pé, junto à praia de guarda-sóis, a igreja de Santa Maria Assunta com a cúpula de majólica.
 */
function positano(): Model {
  const k = new Kit();
  const hill = paint(grid(30, 20, (u, v) => { const z = -115 + 230 * u; const x = 56 - 166 * v; return [x, Math.max(0, positanoY(x, z)), z]; }), '#64773f');
  faceColor(hill, (_x, y, z, up, f) => {
    if (y < 0.6) return '#9d9584';
    if (y > 148 + 8 * valueNoise(216, z / 20)) return hash2(f, 216) > 0.5 ? '#a39a8d' : '#b0a89a';
    if (up < 0.5) return hash2(f, 217) > 0.5 ? '#8c8274' : '#9a9083';
    if (Math.abs(z) < 66 && y < 108) return hash2(f, 218) > 0.45 ? '#7b8a4e' : '#a59b84';
    return hash2(f, 219) > 0.55 ? '#55693a' : '#647a42';
  });
  k.raw(speckle(hill, 0.06, 213));
  // A igreja e o campanário ficam no pé, à frente; a vila não nasce em cima deles.
  const ch = { x: 31, z: -14 };
  const clear = (z0: number, z1: number, y: number): boolean => y < 40 && z1 > ch.z - 13 && z0 < ch.z + 24;
  const walls = ['#f6c6a2', '#f3e4b4', '#f4b4a4', '#f4eee4', '#eaa47a', '#f6da82', '#fbfaf6', '#f2cfc0', '#ebbccb', '#fbf6ea', '#f0d8a8'];
  const quad = (o: number[], a: P3, b: P3, c: P3, d: P3): void => { o.push(...a, ...b, ...c, ...a, ...c, ...d); };
  for (let r = 0; ; r++) {
    const yb = 1.5 + r * 7.2;
    if (yb > 100) break;
    const W = 50 + 14 * Math.sin(Math.PI * Math.min(1, yb / 100)) - Math.max(0, yb - 78) * 1.3;
    let z = -W + hash2(r, 221) * 5; let i = 0;
    while (z < W - 5) {
      const w = 8 + hash2(r * 37 + i, 222) * 6;
      const garden = hash2(i, r * 13 + 223) < 0.1;
      const edge = Math.max(0, (Math.abs(z + w / 2) - (W - 14)) / 14);
      if (!garden && hash2(i + 91, r) > edge && !clear(z, z + w, yb)) {
        const h = 7.5 + hash2(i * 7, r + 224) * 4.5;
        const y0 = yb - 3; const y1 = yb + h + (hash2(r, i + 225) - 0.5) * 2;
        const xf = Math.max(positanoX(yb, z), positanoX(yb, z + w / 2), positanoX(yb, z + w)) + 0.8 + (hash2(i, r + 226) - 0.5) * 2.4;
        const d = 11; const z0 = z; const z1 = z + w;
        const o: number[] = [];
        quad(o, [xf, y0, z0], [xf, y0, z1], [xf, y1, z1], [xf, y1, z0]);
        quad(o, [xf, y1, z0], [xf, y1, z1], [xf - d, y1, z1], [xf - d, y1, z0]);
        quad(o, [xf, y0, z0], [xf, y1, z0], [xf - d, y1, z0], [xf - d, y0, z0]);
        quad(o, [xf, y0, z1], [xf - d, y0, z1], [xf - d, y1, z1], [xf, y1, z1]);
        k.raw(paint(tris(o), walls[Math.floor(hash2(r * 11 + i, 227) * walls.length)]));
        // Janelas e portas de terraço (escuras, algumas com as venezianas verdes), uma ou duas por andar.
        const win: number[] = [];
        const floors = h > 9.5 ? 2 : 1; const cols = w > 11 ? 2 : 1;
        for (let fl = 0; fl < floors; fl++) for (let cI = 0; cI < cols; cI++) {
          const wz = z0 + (w * (cI + 1)) / (cols + 1); const wy = yb + 1.4 + fl * 3.6;
          quad(win, [xf + 0.12, wy, wz - 0.75], [xf + 0.12, wy, wz + 0.75], [xf + 0.12, wy + 2.1, wz + 0.75], [xf + 0.12, wy + 2.1, wz - 0.75]);
        }
        k.raw(paint(tris(win), hash2(i, r + 228) > 0.6 ? '#3e6650' : '#4a3d34'));
      }
      z += w + (garden ? 6 + hash2(i, r) * 5 : 0.3 + hash2(r, i * 3) * 1.4); i++;
    }
  }
  // Ciprestes e pinheiros-mansos entre as casas e nas bordas da vila.
  for (let i = 0; i < 18; i++) {
    const z = -78 + (156 * i) / 17 + (hash2(i, 229) - 0.5) * 8; const y = 6 + hash2(i, 230) * 92;
    const x = positanoX(y, z) + 1;
    if (clear(z - 3, z + 3, y)) continue;
    k.f(cone(2.2, 11, 6), i % 3 ? '#2f4a2c' : '#3a5530', tf(x, y + 4.5, z));
  }
  // A estrada da Costa Amalfitana, com o muro de arrimo, cortando o morro acima da vila.
  const road: P3[] = [];
  for (let z = -100; z <= 100; z += 8) if (positanoY(-100, z) > 132) road.push([positanoX(116, z) + 3, 118.5, z]); // entra no túnel onde o morro baixa
  k.f(ribbon(road, 7, 7), '#d5ccba');
  k.f(ribbon(road.map(([x, y, z]) => [x - 0.4, y + 0.05, z] as P3), 5.6, 0.1), '#5d5a56');
  // Praia de areia escura com os guarda-sóis laranja e azuis.
  k.f(box(12, 0.5, 92), '#8f877a', tf(50, 0.25, 0));
  for (let i = 0; i < 22; i++) {
    const z = -40 + (80 * (i % 11)) / 10 + (hash2(i, 231) - 0.5) * 3; const x = i < 11 ? 47.5 : 52;
    k.f(cone(1.6, 0.8, 6), i % 2 ? '#e8642e' : '#2f6fc0', tf(x, 2.4, z));
  }
  // Santa Maria Assunta: nave caiada de frente para o mar, tambor e a cúpula de majólica (amarela, verde e azul).
  k.f(box(20, 15, 15), '#f4ecd8', tf(ch.x, 6.5, ch.z));
  k.f(gable(15.4, 3.2, 20.4, 0.2), '#c8784a', tf(ch.x, 14, ch.z, 1, 1, 1, 0, Math.PI / 2, 0));
  k.f(box(0.3, 12, 9), '#efe4cc', tf(ch.x + 10.1, 7, ch.z));
  k.f(box(0.2, 4.5, 3), '#5a4030', tf(ch.x + 10.3, 2.2, ch.z));
  const dx = ch.x - 5;
  k.f(cyl(7.2, 7.2, 5, 16), '#f4ecd8', tf(dx, 16.5, ch.z));
  k.f(cyl(7.6, 7.6, 0.7, 16), '#e2d6bc', tf(dx, 19.2, ch.z));
  const dome = paint(sphere(7.4, 16, 7, 0, Math.PI * 2, 0, Math.PI / 2), '#f2c230', tf(dx, 19.5, ch.z, 1, 1.18, 1));
  faceColor(dome, (x, y, z) => { const a = Math.atan2(z - ch.z, x - dx); const b = Math.floor((a + Math.PI) / (Math.PI / 8)) % 3; return y > 27 ? '#2a5aa8' : ['#f2c230', '#2f8a5a', '#2a5aa8'][b]; });
  k.raw(dome);
  k.f(cyl(1.3, 1.3, 2.2, 8), '#f4ecd8', tf(dx, 29.6, ch.z)).f(cone(1.6, 2.2, 8), '#2f8a5a', tf(dx, 31.8, ch.z));
  // Campanário ao lado, com a cupulinha.
  k.f(box(5.5, 24, 5.5), '#f4ecd8', tf(ch.x + 4, 12, ch.z + 15));
  k.f(box(0.2, 3, 2), '#4a3d34', tf(ch.x + 6.8, 19.5, ch.z + 15));
  k.f(sphere(2.9, 8, 4, 0, Math.PI * 2, 0, Math.PI / 2), '#2f8a5a', tf(ch.x + 4, 24, ch.z + 15));
  return k.model(150, { shade: [0.85, 1.05] });
}

/** Igreja de cúpula azul de Santorini: cubo caiado, cúpula azul, sineira com dois arcos e sinos, terraço. */
function cupulaAzul(): Model {
  const k = new Kit();
  const white = '#f8f6f0'; const blue = '#1f5fc0';
  k.f(box(16, 1.2, 14), '#efebe2', tf(0, 0.6, 0));
  k.f(box(7.4, 5.6, 7.4), white, tf(-1, 4, 0));
  k.f(box(7.8, 0.4, 7.8), '#f2efe8', tf(-1, 7, 0));
  k.f(cyl(2.9, 2.9, 1.4, 16), white, tf(-1, 7.9, 0));
  k.f(sphere(3.05, 16, 8, 0, Math.PI * 2, 0, Math.PI / 2), blue, tf(-1, 8.6, 0));
  k.f(cyl(0.45, 0.45, 0.8, 8), white, tf(-1, 11.9, 0));
  k.f(box(0.16, 1.4, 0.16), white, tf(-1, 12.9, 0)).f(box(0.16, 0.16, 0.8), white, tf(-1, 13.1, 0));
  k.f(box(0.12, 2.4, 1.4), blue, tf(2.76, 2.4, 0));
  for (const z of [-2.2, 2.2]) k.f(box(0.12, 1.1, 0.8), blue, tf(2.76, 4.4, z));
  // Sineira: parede caiada com dois arcos, sinos e cruz.
  const bs = new THREE.Shape();
  bs.moveTo(-3, 0); bs.lineTo(3, 0); bs.lineTo(3, 5.6); bs.lineTo(1.4, 5.6); bs.lineTo(1.4, 6.6); bs.absarc(0, 6.6, 1.4, 0, Math.PI, false); bs.lineTo(-1.4, 5.6); bs.lineTo(-3, 5.6); bs.lineTo(-3, 0);
  bs.holes.push(archHole(-1.3, 3.0, 1.5, 2.2), archHole(1.3, 3.0, 1.5, 2.2), archHole(0, 6.1, 1.1, 1.6));
  k.f(extrude(bs, 0.8, 6), white, tf(3.8, 1.2, 4.8, 1, 1, 1, 0, Math.PI / 2, 0));
  for (const [z, y] of [[4.8 + 1.3, 5.6], [4.8 - 1.3, 5.6], [4.8, 8.5]] as const) k.f(cone(0.45, 0.8, 8), '#9a7a3a', tf(3.8, y, z));
  k.f(box(0.14, 1.4, 0.14), white, tf(3.8, 9.9, 4.8)).f(box(0.14, 0.14, 0.8), white, tf(3.8, 10.1, 4.8));
  // Mureta azul e branca, buganvília.
  k.f(box(0.5, 1.0, 13), white, tf(7.7, 1.7, 0));
  k.f(box(0.55, 0.15, 13.1), blue, tf(7.7, 2.25, 0));
  k.raw(speckle(paint(ico(1.4, 1), '#d6368f', tf(-6.5, 2.4, -5, 1.2, 1, 1)), 0.1, 221));
  k.raw(speckle(paint(ico(1.1, 1), '#3f8a3a', tf(-6.2, 1.8, -3.6)), 0.1, 222));
  for (let i = 0; i < 5; i++) k.f(box(1.6, 0.3, 1.2), '#efebe2', tf(8.6 + i * 0.7, 1.05 - i * 0.22, -5));
  return k.model(13, { shadow: true, blob: 5 });
}

/** Moinhos de Santorini: três torres caiadas de telhado de sapé, rodas de raios com velas triangulares. */
function moinhoSantorini(): Model {
  const k = new Kit();
  const ridge = paint(ico(1, 1), '#9a8a78', tf(0, -1.5, 0, 9, 6.5, 32));
  k.raw(speckle(roughen(ridge, 0.8, 231), 0.08, 232));
  [-15, 0, 15].forEach((z, idx) => {
    const y = 3.6 - Math.abs(z) * 0.06;
    k.f(cyl(2.6, 3.0, 7, 12), '#f6f4ee', tf(0, y + 3.5, z));
    k.f(cone(2.9, 3.0, 12), '#c8a46a', tf(0, y + 8.5, z));
    k.f(box(0.12, 1.9, 1.0), idx === 1 ? '#1f5fc0' : '#7a5a3a', tf(2.95, y + 0.95, z));
    k.f(box(0.12, 0.7, 0.7), '#3a4a5a', tf(2.62, y + 5.0, z));
    const hub: P3 = [3.4, y + 7.4, z];
    k.f(box(1.6, 0.4, 0.4), '#5a4030', tf(2.6, y + 7.4, z));
    const bare = idx === 2;
    for (let i = 0; i < 8; i++) {
      const a = (i / 8) * Math.PI * 2 + idx * 0.2;
      const tip: P3 = [3.4, hub[1] + Math.cos(a) * 5.6, z + Math.sin(a) * 5.6];
      k.bar('#6a5038', hub, tip, 0.14);
      const a1 = ((i + 1) / 8) * Math.PI * 2 + idx * 0.2;
      k.bar('#8a7a60', tip, [3.4, hub[1] + Math.cos(a1) * 5.6, z + Math.sin(a1) * 5.6], 0.04);
      if (!bare) k.f(tris([3.45, hub[1] + Math.cos(a) * 1.2, z + Math.sin(a) * 1.2, 3.45, tip[1], tip[2], 3.45, hub[1] + Math.cos(a + 0.42) * 4.8, z + Math.sin(a + 0.42) * 4.8]), '#f4f0e4');
    }
  });
  return k.model(13, { shadow: true, blob: 0 });
}

/** Etna: vulcão largo e escuro com neve no alto, rios de lava acesos e o penacho de fumaça (horizonte). */
function vulcaoEtna(): Model {
  const k = new Kit();
  const H = 210; const R = 360; const Rt = 34;
  const surf = (r: number): number => (r <= Rt ? H - 14 + (r / Rt) * 14 : H * Math.pow(1 - (r - Rt) / (R - Rt), 1.45));
  const prof: Array<[number, number]> = [[0, H - 16], [Rt * 0.7, H - 6], [Rt, H]];
  for (let i = 1; i <= 10; i++) { const r = Rt + (i / 10) * (R - Rt); prof.push([r, surf(r)]); }
  const g = paint(lathe(prof, 36), '#4a4446');
  roughen(g, 4, 241);
  faceColor(g, (x, y, z, _u, f) => {
    const az = Math.atan2(z, x);
    if (y > H * 0.72 && valueNoise(242, az * 5) > -0.1) return '#e8ecf0';
    if (y < H * 0.18) return hash2(f, 243) > 0.5 ? '#5a5a44' : '#646a4a';
    return hash2(f, 244) > 0.6 ? '#3a3436' : '#4a4446';
  });
  k.raw(speckle(g, 0.05, 245));
  // Rios de lava descendo o flanco da frente.
  for (const [th0, seed] of [[0.25, 1], [-0.35, 2]] as const) {
    const out: number[] = [];
    let prev: [P3, P3] | null = null;
    for (let i = 0; i <= 10; i++) {
      const r = Rt + 4 + i * 18; const th = th0 + 0.08 * Math.sin(r / 25 + seed);
      const y = surf(r) + 1.5; const w = 3 + i * 0.6;
      const c = Math.cos(th); const s = Math.sin(th);
      const a: P3 = [c * r - s * w, y, s * r + c * w]; const b: P3 = [c * r + s * w, y, s * r - c * w];
      if (prev) out.push(...prev[0], ...a, ...b, ...prev[0], ...b, ...prev[1]);
      prev = [a, b];
    }
    k.raw(paint(tris2(out), '#ff5a1a'), true);
  }
  k.l(cyl(Rt * 0.6, Rt * 0.6, 1, 10), '#ff7a2a', tf(0, H - 12, 0));
  // Penacho de fumaça que sobe e deriva.
  const puffs: Array<[number, number, number, number]> = [[0, 26, 0, 26], [-6, 52, -18, 32], [-10, 82, -44, 38], [-12, 108, -76, 42], [-14, 130, -112, 44], [-16, 146, -150, 40]];
  puffs.forEach(([x, y, z, r], i) => k.raw(speckle(paint(dodeca(r), i < 2 ? '#8a8a8e' : '#a8a8ac', tf(x, H + y, z, 1, 0.75, 1)), 0.06, 250 + i)));
  return k.model(H + 160, { shade: [0.9, 1.04] });
}

/** Coliseu: elipse de três ordens de arcos e ático, metade arruinada mostrando o anel de dentro; arcos acesos. */
function coliseu(): Model {
  const k = new Kit();
  const a = 94; const b = 78; const bays = 40;
  const trav = '#d8c2a0'; const trav2 = '#cdb592';
  const lvY = [0, 11, 22, 33, 46];
  const ruin = (th: number): number => {
    const t = ((th % (Math.PI * 2)) + Math.PI * 2) % (Math.PI * 2);
    if (t > 0.5 && t < 3.0) return t < 0.75 || t > 2.75 ? 3 : 2;
    return 4;
  };
  for (let i = 0; i < bays; i++) {
    const th = (i / bays) * Math.PI * 2; const th1 = ((i + 1) / bays) * Math.PI * 2;
    const p0: P3 = [b * Math.cos(th), 0, a * Math.sin(th)]; const p1: P3 = [b * Math.cos(th1), 0, a * Math.sin(th1)];
    const len = Math.hypot(p1[0] - p0[0], p1[2] - p0[2]);
    const yaw = -Math.atan2(p1[2] - p0[2], p1[0] - p0[0]);
    const lv = ruin((th + th1) / 2);
    for (let l = 0; l < lv; l++) {
      const y0 = lvY[l]; const y1 = lvY[l + 1]; const h = y1 - y0;
      const mid: P3 = [(p0[0] + p1[0]) / 2, 0, (p0[2] + p1[2]) / 2];
      if (l < 3) {
        k.f(box(len * 0.3, h, 3.2), l % 2 ? trav2 : trav, tf(p0[0], y0 + h / 2, p0[2], 1, 1, 1, 0, yaw, 0));
        k.f(box(len, h * 0.26, 3.0), trav, tf(mid[0], y1 - h * 0.13, mid[2], 1, 1, 1, 0, yaw, 0));
      } else {
        const top = lv === 4 && hash2(i, 261) < 0.25 ? h - 2 : h;
        k.f(box(len * 1.02, top, 3.0), trav, tf(mid[0], y0 + top / 2, mid[2], 1, 1, 1, 0, yaw, 0));
        k.f(box(0.3, 1.6, 1.2), '#5a4a3a', tf(mid[0] * 1.02, y0 + 6, mid[2] * 1.02, 1, 1, 1, 0, yaw + Math.PI / 2, 0));
      }
    }
  }
  for (let l = 0; l < 3; l++) k.l(cyl(1, 1, (lvY[l + 1] - lvY[l]) * 0.74, bays, true), '#7a5a3a', tf(0, lvY[l] + (lvY[l + 1] - lvY[l]) * 0.37, 0, b - 1.8, 1, a - 1.8));
  // Anel de dentro (aparece onde o de fora caiu), arquibancada em funil e a arena.
  k.f(cyl(1, 1, 32, 40, true), '#c4ab88', tf(0, 16, 0, b * 0.86, 1, a * 0.86));
  k.f(cyl(0.84, 0.42, 30, 40, true), '#b89c78', tf(0, 17, 0, b, 1, a));
  k.f(cyl(1, 1, 1, 24), '#c8a878', tf(0, 2, 0, b * 0.42, 1, a * 0.42));
  return k.model(46, { shade: [0.82, 1.05] });
}

/** Basílica de São Pedro: fachada com colunas e estátuas, tambor de colunas pares, cúpula de chumbo com nervuras. */
function cupulaSaoPedro(): Model {
  const k = new Kit();
  const trav = '#e8dcc4'; const lead = '#8e9aa6'; const warm = '#ffd27a';
  // Fachada (+X) e nave.
  k.f(box(12, 46, 115), trav, tf(72, 23, 0));
  for (let i = 0; i < 8; i++) k.f(cyl(1.4, 1.5, 28, 8), '#f2e8d4', tf(78.6, 15, -24 + i * (48 / 7)));
  k.f(gable(4, 8, 30, 0.4), trav, tf(79, 46, 0, 1, 1, 1, 0, Math.PI / 2, 0));
  for (let i = 0; i < 13; i++) k.f(box(1.2, 4, 1.2), '#f2e8d4', tf(77, 48, -54 + i * 9));
  k.l(box(0.3, 0.6, 115), warm, tf(78.2, 30, 0));
  k.l(box(0.3, 18, 30), '#ffe8b8', tf(78.15, 10, 0));
  k.f(box(66, 44, 30), trav, tf(34, 22, 0));
  k.f(box(70, 44, 70), trav, tf(0, 22, 0));
  for (const [x, z] of [[-35, 0], [0, 35], [0, -35]] as const) k.f(cyl(18, 18, 44, 12, false), trav, tf(x, 22, z, 1, 1, 1, 0, 0, 0));
  // Tambor com colunas pares e janelas.
  k.f(cyl(22, 22, 4, 16), '#d8ccb4', tf(0, 46, 0));
  k.f(cyl(21, 21, 24, 16), trav, tf(0, 60, 0));
  for (let i = 0; i < 16; i++) {
    const t = (i / 16) * Math.PI * 2;
    k.f(box(3.4, 22, 2.4), '#f2e8d4', tf(Math.cos(t) * 22, 59, Math.sin(t) * 22, 1, 1, 1, 0, -t, 0));
    const tw = t + Math.PI / 16;
    k.l(box(0.4, 7, 3), warm, tf(Math.cos(tw) * 21.1, 60, Math.sin(tw) * 21.1, 1, 1, 1, 0, -tw, 0));
  }
  k.f(cyl(23, 23, 2.4, 16), '#d8ccb4', tf(0, 73, 0));
  // Cúpula de chumbo com nervuras claras.
  const prof: Array<[number, number]> = [[22, 0], [21.5, 8], [19.6, 18], [16, 28], [11, 37], [5, 43], [4.5, 44]];
  k.f(lathe(prof, 32), lead, tf(0, 74, 0));
  for (let i = 0; i < 16; i++) {
    const t = (i / 16) * Math.PI * 2; const c = Math.cos(t); const s = Math.sin(t);
    for (let j = 0; j + 1 < prof.length - 1; j++) {
      const [r0, y0] = prof[j]; const [r1, y1] = prof[j + 1];
      k.bar('#ddd6c6', [c * (r0 + 0.3), 74 + y0, s * (r0 + 0.3)], [c * (r1 + 0.3), 74 + y1, s * (r1 + 0.3)], 0.9);
    }
  }
  // Lanterna, bola e cruz.
  k.f(cyl(4.5, 4.8, 9, 8), trav, tf(0, 122.5, 0));
  k.l(cyl(4.85, 4.85, 4, 8, true), warm, tf(0, 122.5, 0));
  k.f(lathe([[5, 0], [4.4, 1.5], [2.6, 3.2], [0.6, 4.2], [0, 4.3]], 8), lead, tf(0, 127, 0));
  k.f(sphere(0.9, 8, 6), '#d8b048', tf(0, 132.2, 0));
  k.f(box(0.4, 3.6, 0.4), '#d8b048', tf(0, 134.6, 0)).f(box(0.4, 0.4, 2), '#d8b048', tf(0, 135.4, 0));
  // Cúpulas menores dos lados.
  for (const z of [-30, 30]) {
    k.f(cyl(6, 6, 6, 12), trav, tf(14, 47, z));
    k.f(sphere(6.2, 12, 5, 0, Math.PI * 2, 0, Math.PI / 2), lead, tf(14, 50, z, 1, 1.2, 1));
    k.f(cyl(1, 1, 3, 6), trav, tf(14, 58.5, z));
  }
  return k.model(136, { shade: [0.82, 1.05] });
}

// ───────────────────────────── Registro ─────────────────────────────

export const LANDMARKS_MUNDO: LandmarkRegistry = {
  // Estados Unidos
  placa_rota_66: { build: placaRota66, place: 'near', side: 'any', perLap: 2 },
  diner_neon: { build: dinerNeon, place: 'near', side: 'land', perLap: 1 },
  log_lodge: { build: logLodge, place: 'near', side: 'land', perLap: 1 },
  ponte_trelica: { build: ponteTrelica, place: 'far', side: 'any', perLap: 1 },
  represa_hoover: { build: represaHoover, place: 'far', side: 'any', perLap: 1 },
  placa_las_vegas: { build: placaLasVegas, place: 'near', side: 'any', perLap: 1 },
  piramide_luxor: { build: piramideLuxor, place: 'far', side: 'any', perLap: 1 },
  torre_stratosphere: { build: torreStratosphere, place: 'far', side: 'any', perLap: 1 },
  // Japão
  rainbow_bridge: { build: rainbowBridge, place: 'far', side: 'sea', perLap: 1 },
  torre_toquio: { build: torreToquio, place: 'far', side: 'land', perLap: 1 },
  portao_shurei: { build: portaoShurei, place: 'near', side: 'any', perLap: 1 },
  shisa: { build: shisa, place: 'near', side: 'any', perLap: 2 },
  monte_fuji_pico: { build: monteFujiPico, place: 'skyline', side: 'any', perLap: 1 },
  pagode_chureito: { build: pagodeChureito, place: 'far', side: 'land', perLap: 1 },
  castelo_osaka: { build: casteloOsaka, place: 'far', side: 'any', perLap: 1 },
  tsutenkaku: { build: tsutenkaku, place: 'far', side: 'any', perLap: 1 },
  // Europa
  castelo_neuschwanstein: { build: casteloNeuschwanstein, place: 'far', side: 'any', perLap: 1 },
  torre_eiffel: { build: torreEiffel, place: 'far', side: 'any', perLap: 1 },
  arco_triunfo: { build: arcoTriunfo, place: 'far', side: 'any', perLap: 1 },
  matterhorn: { build: matterhorn, place: 'skyline', side: 'any', perLap: 1 },
  capela_alpina: { build: capelaAlpina, place: 'near', side: 'any', perLap: 1 },
  cassino_monte_carlo: { build: cassinoMonteCarlo, place: 'near', side: 'land', perLap: 1 },
  // África do Sul
  portao_kruger: { build: portaoKruger, place: 'near', side: 'any', perLap: 1 },
  girafa: { build: girafa, place: 'near', side: 'any', perLap: 2 },
  igreja_karoo: { build: igrejaKaroo, place: 'near', side: 'any', perLap: 1 },
  anfiteatro_drakensberg: { build: anfiteatroDrakensberg, place: 'skyline', side: 'any', perLap: 1 },
  farol_cape_point: { build: farolCapePoint, place: 'far', side: 'sea', perLap: 1 },
  table_mountain: { build: tableMountain, place: 'skyline', side: 'land', perLap: 1 },
  // Austrália
  uluru: { build: uluru, place: 'skyline', side: 'any', perLap: 1 },
  arco_great_ocean: { build: arcoGreatOcean, place: 'far', side: 'sea', perLap: 1 },
  passarela_daintree: { build: passarelaDaintree, place: 'near', side: 'any', perLap: 1 },
  opera_sydney: { build: operaSydney, place: 'far', side: 'any', perLap: 1 },
  harbour_bridge: { build: harbourBridge, place: 'far', side: 'any', perLap: 1 },
  // Escandinávia
  ponte_storseisundet: { build: ponteStorseisundet, place: 'far', side: 'sea', perLap: 1 },
  vila_lapponia: { build: vilaLapponia, place: 'near', side: 'any', perLap: 1 },
  placa_trolls: { build: placaTrolls, place: 'near', side: 'any', perLap: 2 },
  cachoeira_stigfossen: { build: cachoeiraStigfossen, place: 'far', side: 'any', perLap: 1 },
  catedral_artica: { build: catedralArtica, place: 'far', side: 'land', perLap: 1 },
  // Mediterrâneo
  positano: { build: positano, place: 'far', side: 'land', perLap: 1 },
  cupula_azul: { build: cupulaAzul, place: 'near', side: 'land', perLap: 1 },
  moinho_santorini: { build: moinhoSantorini, place: 'near', side: 'land', perLap: 1 },
  vulcao_etna: { build: vulcaoEtna, place: 'skyline', side: 'any', perLap: 1 },
  coliseu: { build: coliseu, place: 'far', side: 'any', perLap: 1 },
  cupula_sao_pedro: { build: cupulaSaoPedro, place: 'far', side: 'any', perLap: 1 },
};
