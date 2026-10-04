// Carros menos quadrados pela GEOMETRIA (onda I): as peças chanfradas do kit (caixa e barra arredondadas),
// o pneu de mais lados, o arco da roda de mais facetas, o bico e a traseira redondos em planta, o ombro
// redondo — e a garantia de que faróis, lanternas, faróis escamoteáveis e pinturas continuam os mesmos.
import { describe, expect, it } from 'vitest';
import { CAR_BODIES } from '../src/core/data/cars';
import type { CarBody } from '../src/core/types';
import { BodyShape, type BodySpec } from '../src/render/cars/body';
import { CAR_LIMITS } from '../src/render/cars/check';
import { fillet, MeshBuilder, roundBar, roundBeam, roundBox, signedVolume, withBrush, type Brush, type P2 } from '../src/render/cars/kit';
import { MODEL_BUILDERS } from '../src/render/cars/models';
import * as P from '../src/render/cars/paints';
import { buildSimpleWheel, buildWheel, WHEEL_DESIGNS } from '../src/render/cars/wheels';
import { CREASE } from '../src/render/normals';

type V3 = [number, number, number];

/** Arestas soldadas da malha não indexada: cada uma com os triângulos que a usam e o sentido em que a percorrem. */
function edgesOf(pos: ArrayLike<number>) {
  const key = (i: number) => `${Math.round(pos[i * 3] * 1e4)},${Math.round(pos[i * 3 + 1] * 1e4)},${Math.round(pos[i * 3 + 2] * 1e4)}`;
  const map = new Map<string, Array<{ tri: number; fwd: boolean }>>();
  for (let t = 0; t < pos.length / 9; t++) {
    for (let k = 0; k < 3; k++) {
      const a = key(t * 3 + k); const b = key(t * 3 + ((k + 1) % 3));
      const e = a < b ? `${a}|${b}` : `${b}|${a}`;
      const l = map.get(e) ?? [];
      l.push({ tri: t, fwd: a < b });
      map.set(e, l);
    }
  }
  return map;
}

function normalOf(pos: ArrayLike<number>, t: number): V3 {
  const p = (k: number, c: number) => pos[t * 9 + k * 3 + c];
  const ux = p(1, 0) - p(0, 0); const uy = p(1, 1) - p(0, 1); const uz = p(1, 2) - p(0, 2);
  const vx = p(2, 0) - p(0, 0); const vy = p(2, 1) - p(0, 1); const vz = p(2, 2) - p(0, 2);
  const n: V3 = [uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx];
  const l = Math.hypot(...n);
  return [n[0] / l, n[1] / l, n[2] / l];
}

/** Fechada (toda aresta em exatamente duas faces, percorrida em sentidos opostos) e o maior ângulo entre vizinhas. */
function closedAndDihedrals(pos: ArrayLike<number>) {
  let closed = true; const angles: number[] = [];
  for (const l of edgesOf(pos).values()) {
    if (l.length !== 2 || l[0].fwd === l[1].fwd) { closed = false; continue; }
    const a = normalOf(pos, l[0].tri); const b = normalOf(pos, l[1].tri);
    angles.push((Math.acos(Math.max(-1, Math.min(1, a[0] * b[0] + a[1] * b[1] + a[2] * b[2]))) * 180) / Math.PI);
  }
  return { closed, angles };
}

function bounds(pos: ArrayLike<number>) {
  const min = [Infinity, Infinity, Infinity]; const max = [-Infinity, -Infinity, -Infinity];
  for (let i = 0; i < pos.length; i += 3) for (let c = 0; c < 3; c++) { min[c] = Math.min(min[c], pos[i + c]); max[c] = Math.max(max[c], pos[i + c]); }
  return { min, max };
}

describe('kit: peças chanfradas', () => {
  it('caixa arredondada: a mesma caixa por fora, fechada, faces para fora e lisa no vinco de carro', () => {
    const b = new MeshBuilder();
    roundBox(b, [0.3, 0.5, -1], [0.8, 0.2, 0.5], P.PAINT, 0.05);
    const pos = b.positions();
    const { min, max } = bounds(pos);
    [0.3 - 0.4, 0.5 - 0.1, -1 - 0.25].forEach((v, c) => expect(min[c]).toBeCloseTo(v, 5));
    [0.3 + 0.4, 0.5 + 0.1, -1 + 0.25].forEach((v, c) => expect(max[c]).toBeCloseTo(v, 5));
    const { closed, angles } = closedAndDihedrals(pos);
    expect(closed).toBe(true);
    const vol = signedVolume(pos);
    expect(vol).toBeGreaterThan(0.8 * 0.2 * 0.5 * 0.9);
    expect(vol).toBeLessThan(0.8 * 0.2 * 0.5);
    // Nenhuma aresta viva: as quinas viram em passos que o vinco de carro (45°) deixa lisos, com folga.
    expect(Math.max(...angles)).toBeLessThan(CREASE.car - 5);
    expect(b.triangles).toBeLessThanOrEqual(112);
  });

  it('barra arredondada: seção de cantos redondos ao longo do eixo, pontas planas e vivas', () => {
    for (const axis of ['x', 'y', 'z'] as const) {
      const b = new MeshBuilder();
      roundBar(b, [0, 0.3, 2.2], [1.7, 0.08, 0.1], P.CHROME, { r: 0.03, axis: axis === 'x' ? 'x' : axis });
      const pos = b.positions();
      const { min, max } = bounds(pos);
      [-0.85, 0.26, 2.15].forEach((v, c) => expect(min[c], axis).toBeCloseTo(v, 5));
      [0.85, 0.34, 2.25].forEach((v, c) => expect(max[c], axis).toBeCloseTo(v, 5));
      const { closed, angles } = closedAndDihedrals(pos);
      expect(closed, axis).toBe(true);
      // Cada canto da seção perde o triângulo fora do chanfro: (1 − 0,634) r² (o perfil de 30° vai a 0,634 r).
      const [su, sv] = axis === 'x' ? [0.08, 0.1] : axis === 'y' ? [0.1, 1.7] : [1.7, 0.08];
      const sw = axis === 'x' ? 1.7 : axis === 'y' ? 0.08 : 0.1;
      const r = Math.min(0.03, su / 2, sv / 2);
      expect(signedVolume(pos), axis).toBeCloseTo(sw * (su * sv - 4 * (1 - 1 / (1 + Math.tan(Math.PI / 6))) * r * r), 6);
      // Só dois tipos de aresta: a da seção (lisa, < 40°) e a da ponta (90°, viva).
      for (const a of angles) expect(a < CREASE.car - 5 || Math.abs(a - 90) < 1e-3, `${axis}: ${a}`).toBe(true);
      expect(b.triangles, axis).toBeLessThanOrEqual(48);
    }
  });

  it('viga arredondada: fechada, faces para fora, de ponta a ponta e com a seção lisa', () => {
    const b = new MeshBuilder();
    roundBeam(b, [0.28, 0.34, -2.22], [0.78, 0.36, -2.08], 0.05, 0.05, P.CHROME, 0.02);
    const pos = b.positions();
    const { closed, angles } = closedAndDihedrals(pos);
    expect(closed).toBe(true);
    const len = Math.hypot(0.5, 0.02, 0.14);
    expect(signedVolume(pos)).toBeCloseTo(len * (0.05 * 0.05 - 4 * (1 - 1 / (1 + Math.tan(Math.PI / 6))) * 0.02 * 0.02), 6);
    for (const a of angles) expect(a < CREASE.car - 5 || Math.abs(a - 90) < 1e-3, String(a)).toBe(true);
  });

  it('filete: o perfil arredondado fica dentro do original, sem dobra maior que 30°', () => {
    const sq: P2[] = [[0, 0], [2, 0], [2, 0.5], [0, 1]];
    const out = fillet(sq, 0.2);
    expect(out.length).toBeGreaterThan(sq.length * 2);
    for (const [x, y] of out) { expect(x).toBeGreaterThanOrEqual(-1e-9); expect(x).toBeLessThanOrEqual(2 + 1e-9); expect(y).toBeGreaterThanOrEqual(-1e-9); expect(y).toBeLessThanOrEqual(1 + 1e-9); }
    for (let i = 0; i < out.length; i++) {
      const p = out[(i + out.length - 1) % out.length]; const v = out[i]; const q = out[(i + 1) % out.length];
      const a0 = Math.atan2(v[1] - p[1], v[0] - p[0]); const a1 = Math.atan2(q[1] - v[1], q[0] - v[0]);
      const turn = Math.abs(((a1 - a0 + 3 * Math.PI) % (2 * Math.PI)) - Math.PI);
      expect((turn * 180) / Math.PI, `ponto ${i}`).toBeLessThanOrEqual(30 + 1e-6);
    }
    // Raio zero num canto: o canto fica vivo, igual ao original.
    expect(fillet(sq, [0, 0.2, 0.2, 0.2])[0]).toEqual([0, 0]);
  });

  it('a face trocada (lente) pega só o lado dela, e a face nula some', () => {
    const lens = P.HEAD;
    for (const make of [
      (b: MeshBuilder, f: Partial<Record<'nu' | 'pv', Brush | null>>) => roundBox(b, [0, 0.6, -2.1], [0.36, 0.13, 0.08], P.HEAD_HOUSING, 0.025, f),
      (b: MeshBuilder, f: Partial<Record<'nu' | 'pv', Brush | null>>) => roundBar(b, [0, 0.6, -2.1], [0.36, 0.13, 0.08], P.HEAD_HOUSING, { r: 0.025, axis: 'z', faces: f }),
    ]) {
      const b = new MeshBuilder();
      make(b, { nu: lens });
      const g = b.build();
      const pos = g.getAttribute('position').array as Float32Array; const mat = g.getAttribute('aMat');
      let lensTris = 0;
      for (let t = 0; t < pos.length / 9; t++) {
        if (mat.getZ(t * 3) > 0.5) { lensTris++; expect(normalOf(pos, t)[2]).toBeLessThan(-0.5); }
      }
      expect(lensTris).toBeGreaterThan(0);
      const open = new MeshBuilder();
      make(open, { pv: null });
      const op = open.positions();
      expect(closedAndDihedrals(op).closed).toBe(false);
      for (let t = 0; t < op.length / 9; t++) expect(normalOf(op, t)[1]).toBeLessThan(0.9);
    }
  });
});

describe('rodas e carroceria mais redondas', () => {
  it('o pneu de todo desenho tem de 16 a 20 lados (e a roda simples, 12)', () => {
    const sides = (pos: ArrayLike<number>) => {
      const angles = new Set<number>();
      for (let i = 0; i < pos.length; i += 3) {
        const r = Math.hypot(pos[i + 1], pos[i + 2]);
        if (Math.abs(r - 1) < 1e-4 && Math.abs(pos[i]) < 0.29) angles.add(Math.round(Math.atan2(pos[i + 2], pos[i + 1]) * 1e4));
      }
      return angles.size;
    };
    for (const d of WHEEL_DESIGNS) {
      const n = sides(buildWheel(d).geometry.getAttribute('position').array as Float32Array);
      expect(n, d).toBeGreaterThanOrEqual(16);
      expect(n, d).toBeLessThanOrEqual(20);
    }
    expect(sides(buildSimpleWheel().geometry.getAttribute('position').array as Float32Array)).toBe(12);
  });

  const spec = (): BodySpec => ({
    secs: [
      { z: -2.2, yb: 0.25, yt: 0.5, hw: 0.8 },
      { z: -2.0, yb: 0.18, yt: 0.62, hw: 0.92 },
      { z: 0, yb: 0.18, yt: 0.75, hw: 0.93 },
      { z: 2.0, yb: 0.2, yt: 0.8, hw: 0.92 },
      { z: 2.2, yb: 0.3, yt: 0.76, hw: 0.82 },
    ],
    axles: [{ z: -1.3, r: 0.35, x: 0.79, w: 0.3 }, { z: 1.3, r: 0.35, x: 0.79, w: 0.3 }],
    brush: () => P.PAINT, capFront: P.PAINT, capBack: P.PAINT,
  });

  it('o arco da roda tem de 10 a 12 facetas', () => {
    const shape = new BodyShape(spec());
    for (const [ai, ax] of shape.spec.axles.entries()) {
      const ra = ax.r + (shape.spec.clear ?? 0.06);
      const zs = new Set(shape.stations.filter((s) => s.axle === ai && s.archY !== null && Math.abs(s.z - ax.z) <= ra + 1e-9).map((s) => s.z.toFixed(6)));
      expect(zs.size - 1, `eixo ${ai}`).toBeGreaterThanOrEqual(10);
      expect(zs.size - 1, `eixo ${ai}`).toBeLessThanOrEqual(12);
    }
  });

  it('o bico e a traseira são redondos em planta: estações a mais nas pontas, por fora da reta entre as seções', () => {
    const s = spec();
    const shape = new BodyShape(s);
    const [a, b] = [s.secs[0], s.secs[1]];
    const [c, d] = [s.secs[3], s.secs[4]];
    expect(shape.stations.filter((st) => st.z > a.z && st.z < b.z).length).toBeGreaterThanOrEqual(2);
    expect(shape.stations.filter((st) => st.z > c.z && st.z < d.z).length).toBeGreaterThanOrEqual(2);
    const line = (p: typeof a, q: typeof a, t: number) => p.hw + (q.hw - p.hw) * t;
    for (const t of [0.15, 0.3, 0.5]) {
      expect(shape.at(a.z + (b.z - a.z) * t).hw, `frente ${t}`).toBeGreaterThan(line(a, b, t) + 0.01);
      expect(shape.at(d.z - (d.z - c.z) * t).hw, `traseira ${t}`).toBeGreaterThan(line(d, c, t) + 0.01);
    }
    // As pontas continuam as mesmas (a pegada não muda).
    expect(shape.at(a.z).hw).toBeCloseTo(a.hw, 9);
    expect(shape.at(d.z).hw).toBeCloseTo(d.hw, 9);
  });

  it('o ombro da carroceria vira do lado para o capô em passos lisos (menos de 35° cada)', () => {
    const shape = new BodyShape(spec());
    const st = shape.stations.find((x) => x.z === 0);
    if (!st) throw new Error('sem estação em z = 0');
    const ring = shape.ring(st);
    // Metade direita, do vinco (o ponto mais largo) até a borda do capô.
    const right = ring.slice(1, ring.length / 2);
    const belt = right.findIndex(([x]) => x === Math.max(...right.map((p) => p[0])));
    const top = shape.at(0).hw - shape.at(0).sh;
    const edge = right.findIndex(([x]) => Math.abs(x - top) < 1e-9);
    expect(edge - belt).toBeGreaterThanOrEqual(5); // faixa, três pontos no ombro, borda
    for (let i = belt + 2; i <= edge; i++) {
      const [x0, y0] = right[i - 2]; const [x1, y1] = right[i - 1]; const [x2, y2] = right[i];
      const a0 = Math.atan2(y1 - y0, x1 - x0); const a1 = Math.atan2(y2 - y1, x2 - x1);
      const turn = Math.abs(((a1 - a0 + 3 * Math.PI) % (2 * Math.PI)) - Math.PI);
      expect((turn * 180) / Math.PI, `ponto ${i - 1}`).toBeLessThan(35);
    }
  });
});

describe('o que não pode mudar com a forma nova', () => {
  // Pincéis de luz e de pintura de cada estilo antes da onda I (aa7b66f): a forma mudou, o comportamento não.
  const NAMED: Record<string, Brush> = {
    HEAD: P.HEAD, TAIL: P.TAIL, AMBER: P.AMBER, DRL: P.DRL, REVERSE: P.REVERSE, ACCENT: P.ACCENT, STRIPE_A: P.STRIPE_A, STRIPE_B: P.STRIPE_B,
    HEAD_DIM: withBrush(P.HEAD, { head: 0.35 }), POP_HEAD: withBrush(P.HEAD, { popup: 1 }), POP_PAINT: withBrush(P.PAINT, { popup: 1 }), POP_TRIM: withBrush(P.TRIM, { popup: 1 }),
  };
  const BEFORE: Record<CarBody, string[]> = {
    gt: ['ACCENT', 'HEAD', 'STRIPE_A', 'STRIPE_B', 'TAIL'],
    muscle: ['HEAD', 'STRIPE_A', 'STRIPE_B', 'TAIL'],
    hatch: ['ACCENT', 'AMBER', 'HEAD', 'STRIPE_A', 'STRIPE_B', 'TAIL'],
    sedan: ['AMBER', 'HEAD', 'REVERSE', 'STRIPE_A', 'STRIPE_B', 'TAIL'],
    electric: ['DRL', 'HEAD', 'STRIPE_A', 'STRIPE_B', 'TAIL'],
    rally: ['ACCENT', 'HEAD', 'STRIPE_A', 'STRIPE_B', 'TAIL'],
    hyper: ['ACCENT', 'HEAD', 'STRIPE_A', 'STRIPE_B', 'TAIL'],
    classic: ['AMBER', 'HEAD', 'STRIPE_A', 'STRIPE_B', 'TAIL'],
    wedge: ['AMBER', 'HEAD_DIM', 'POP_HEAD', 'POP_PAINT', 'POP_TRIM', 'STRIPE_A', 'STRIPE_B', 'TAIL'],
    pickup: ['AMBER', 'HEAD', 'STRIPE_A', 'STRIPE_B', 'TAIL'],
    prototype: ['ACCENT', 'HEAD', 'STRIPE_A', 'STRIPE_B', 'TAIL'],
    micro: ['AMBER', 'HEAD', 'STRIPE_A', 'STRIPE_B', 'TAIL'],
    roadster: ['ACCENT', 'AMBER', 'HEAD', 'STRIPE_A', 'STRIPE_B', 'TAIL'],
  };
  // Área (m²) das luzes e das camadas antes da onda I; a forma nova pode mudar o tamanho, não sumir nem dobrar.
  const AREA: Record<CarBody, [head: number, tail: number, popup: number, layerA: number, layerB: number, accent: number]> = {
    gt: [0.109, 0.076, 0, 1.982, 0.376, 0.216], muscle: [0.079, 0.177, 0, 2.004, 0.518, 0], hatch: [0.176, 0.102, 0, 2.289, 0.394, 1.676],
    sedan: [0.203, 0.132, 0, 2.141, 0.421, 0], electric: [0.125, 0.09, 0, 1.784, 0.325, 0], rally: [0.148, 0.09, 0, 2.434, 0.54, 1.738],
    hyper: [0.036, 0.06, 0, 2.533, 0.353, 2.447], classic: [0.115, 0.043, 0, 1.8, 0.413, 0], wedge: [0.298, 0.317, 0.691, 2.018, 0.34, 0],
    pickup: [0.27, 0.06, 0, 1.447, 0.597, 0], prototype: [0.036, 0.08, 0, 2.52, 0.391, 3.72], micro: [0.165, 0.038, 0, 3.036, 0.322, 0],
    roadster: [0.11, 0.033, 0, 1.27, 0.382, 0.16],
  };
  const bytes = (b: Brush) => [...b.rgb, b.paint, b.accent, b.layer / 3, b.popup, b.rough, b.metal, b.head, b.tail].map((v) => Math.round(Math.max(0, Math.min(1, v)) * 255)).join(',');
  const NAME_OF = new Map(Object.entries(NAMED).map(([n, b]) => [bytes(b), n]));

  it.each(CAR_BODIES.map((b) => [b] as const))('%s: os mesmos pincéis de farol, lanterna, escamoteável e faixa; áreas na mesma ordem', (body) => {
    const g = MODEL_BUILDERS[body]().shell;
    const p = g.getAttribute('position'); const c = g.getAttribute('color'); const a = g.getAttribute('aPaint'); const t = g.getAttribute('aMat');
    const q = (v: number) => Math.round(v * 255);
    const names = new Set<string>();
    const area = [0, 0, 0, 0, 0, 0];
    for (let i = 0; i < p.count; i += 3) {
      const ux = p.getX(i + 1) - p.getX(i); const uy = p.getY(i + 1) - p.getY(i); const uz = p.getZ(i + 1) - p.getZ(i);
      const vx = p.getX(i + 2) - p.getX(i); const vy = p.getY(i + 2) - p.getY(i); const vz = p.getZ(i + 2) - p.getZ(i);
      const ar = Math.hypot(uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx) / 2;
      const by = [q(c.getX(i)), q(c.getY(i)), q(c.getZ(i)), q(a.getX(i)), q(a.getY(i)), q(a.getZ(i)), q(a.getW(i)), q(t.getX(i)), q(t.getY(i)), q(t.getZ(i)), q(t.getW(i))];
      const [, , , , acc, layer, pop, , , hd, tl] = by;
      if (hd > 0 || tl > 0 || pop > 0 || layer > 0 || acc > 0) names.add(NAME_OF.get(by.join(',')) ?? `desconhecido ${by.join(',')}`);
      const l = Math.round((layer / 255) * 3);
      if (hd > 51) area[0] += ar;
      if (tl > 127) area[1] += ar;
      if (pop > 0) area[2] += ar;
      if (l === 1 || l === 3) area[3] += ar;
      if (l === 2 || l === 3) area[4] += ar;
      if (acc > 0) area[5] += ar;
    }
    expect([...names].sort()).toEqual(BEFORE[body]);
    AREA[body].forEach((before, k) => {
      const what = ['farol', 'lanterna', 'escamoteável', 'faixa A', 'faixa B', 'acento'][k];
      if (before === 0) expect(area[k], what).toBe(0);
      else { expect(area[k], what).toBeGreaterThan(before * 0.6); expect(area[k], what).toBeLessThan(before * 1.6); }
    });
  });

  it('o pneu não fura o capô: há lataria acima do topo da banda de rodagem (do meio para dentro)', () => {
    // Defeito da cunha (antes da onda I): o capô baixo ficava abaixo do topo do pneu dianteiro e o pneu aparecia
    // por cima dele. Raio para cima a partir do topo do pneu; a borda de fora pode ficar à mostra (pneu largo).
    const above = (pos: ArrayLike<number>, x: number, y: number, z: number): boolean => {
      for (let i = 0; i < pos.length; i += 9) {
        const ax = pos[i]; const az = pos[i + 2]; const bx = pos[i + 3]; const bz = pos[i + 5]; const cx = pos[i + 6]; const cz = pos[i + 8];
        const d = (bz - cz) * (ax - cx) + (cx - bx) * (az - cz);
        if (Math.abs(d) < 1e-12) continue;
        const l1 = ((bz - cz) * (x - cx) + (cx - bx) * (z - cz)) / d;
        const l2 = ((cz - az) * (x - cx) + (ax - cx) * (z - cz)) / d;
        if (l1 < 0 || l2 < 0 || l1 + l2 > 1) continue;
        if (l1 * pos[i + 1] + l2 * pos[i + 4] + (1 - l1 - l2) * pos[i + 7] > y) return true;
      }
      return false;
    };
    for (const body of CAR_BODIES) {
      const m = MODEL_BUILDERS[body]();
      const pos = m.shell.getAttribute('position').array as Float32Array;
      m.axles.forEach((a, k) => {
        for (const x of [a.x - a.w / 2, a.x]) expect(above(pos, x, 2 * a.r, a.z), `${body}: eixo ${k}, x = ${x.toFixed(2)}`).toBe(true);
      });
    }
  });

  it('todo estilo segue dentro do orçamento e da pegada (o mesmo validador do glTF)', () => {
    for (const body of CAR_BODIES) {
      const m = MODEL_BUILDERS[body]();
      expect(m.triangles, body).toBeLessThanOrEqual(CAR_LIMITS.maxShellTriangles);
      const bb = m.shell.boundingBox;
      if (!bb) throw new Error(body);
      expect(bb.max.x, body).toBeLessThanOrEqual(CAR_LIMITS.halfWidth);
      expect(-bb.min.x, body).toBeLessThanOrEqual(CAR_LIMITS.halfWidth);
      expect(bb.max.z, body).toBeLessThanOrEqual(CAR_LIMITS.halfLength);
      expect(-bb.min.z, body).toBeLessThanOrEqual(CAR_LIMITS.halfLength);
    }
  });
});
