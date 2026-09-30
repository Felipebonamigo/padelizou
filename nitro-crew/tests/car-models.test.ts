import { describe, expect, it } from 'vitest';
import { CAR_BODIES, CARS } from '../src/core/data/cars';
import type { CarBody, CarDef } from '../src/core/types';
import { box, cylinder, extrudeZY, insideBox, lathe, loft, MeshBuilder, signedVolume, AXIS_X, type P2 } from '../src/render/cars/kit';
import type { CarModel } from '../src/render/cars/model';
import { buildModel, MODEL_BUILDERS } from '../src/render/cars/models';
import { carPaint, defaultAccent, LIVERY_A, LIVERY_B, PAINT } from '../src/render/cars/paints';
import { buildSimpleWheel, buildWheel, MAX_SPIN_FRACTION, spinStep, WHEEL_DESIGNS } from '../src/render/cars/wheels';

const models = new Map<CarBody, CarModel>(CAR_BODIES.map((b) => [b, buildModel(b)]));
const HEX = /^#[0-9a-f]{6}$/i;

/** Lê os atributos do casco: posição e os canais de pintura/material (normalizados 0..1). */
function attrs(m: CarModel) {
  const pos = m.shell.getAttribute('position');
  const paint = m.shell.getAttribute('aPaint');
  const mat = m.shell.getAttribute('aMat');
  return { pos, paint, mat, n: pos.count };
}

describe('modelos dos carros', () => {
  it('cada um dos 13 estilos tem construtor próprio e devolve o próprio estilo', () => {
    expect(Object.keys(MODEL_BUILDERS).sort()).toEqual([...CAR_BODIES].sort());
    for (const body of CAR_BODIES) {
      const m = models.get(body);
      expect(m?.body, body).toBe(body);
      expect(m?.triangles, body).toBeGreaterThan(600);
    }
    expect(() => buildModel('carroça' as CarBody)).toThrow(/sem modelo/);
  });

  it('nenhum estilo cai num modelo genérico: as 13 malhas são diferentes', () => {
    const sig = (m: CarModel) => {
      const p = m.shell.getAttribute('position').array as Float32Array;
      let h = 0;
      for (let i = 0; i < p.length; i += 7) h = (h * 31 + Math.round(p[i] * 1000)) | 0;
      const bb = m.shell.boundingBox;
      return `${m.triangles}|${h}|${bb?.max.y.toFixed(3)}|${bb?.min.z.toFixed(3)}`;
    };
    const sigs = new Set(CAR_BODIES.map((b) => sig(models.get(b) as CarModel)));
    expect(sigs.size).toBe(CAR_BODIES.length);
  });

  it('todo estilo cabe na pegada de colisão (~4,4 × 1,9 m), com as rodas dentro dela', () => {
    for (const body of CAR_BODIES) {
      const m = models.get(body) as CarModel;
      const bb = m.shell.boundingBox;
      if (!bb) throw new Error(body);
      expect(bb.max.x, body).toBeLessThanOrEqual(1.0);
      expect(bb.min.x, body).toBeGreaterThanOrEqual(-1.0);
      expect(bb.max.z, body).toBeLessThanOrEqual(2.35);
      expect(bb.min.z, body).toBeGreaterThanOrEqual(-2.35);
      expect(bb.min.y, body).toBeGreaterThan(0.04);
      expect(bb.max.y, body).toBeLessThan(1.95);
      // Mais curto só o micro; todos com a largura inteira.
      expect(bb.max.z - bb.min.z, body).toBeGreaterThan(body === 'micro' ? 3.2 : 4.2);
      expect(bb.max.x - bb.min.x, body).toBeGreaterThan(1.8);
      for (const a of m.axles) {
        expect(a.x + a.w / 2, body).toBeLessThanOrEqual(0.99);
        expect(a.r, body).toBeGreaterThan(0.26);
        expect(Math.abs(a.z) + a.r, body).toBeLessThan(2.2);
      }
      expect(m.axles[0].z, body).toBeLessThan(0);
      expect(m.axles[1].z, body).toBeGreaterThan(0);
    }
  });

  it('faróis na frente, lanternas atrás, e todo estilo tem os dois', () => {
    for (const body of CAR_BODIES) {
      const { pos, mat, n } = attrs(models.get(body) as CarModel);
      let heads = 0; let tails = 0;
      for (let i = 0; i < n; i++) {
        if (mat.getZ(i) > 0.2) { heads++; expect(pos.getZ(i), `${body}: farol atrás`).toBeLessThan(0.7); }
        if (mat.getW(i) > 0.5) { tails++; expect(pos.getZ(i), `${body}: lanterna na frente`).toBeGreaterThan(0.5); }
      }
      expect(heads, body).toBeGreaterThan(0);
      expect(tails, body).toBeGreaterThan(0);
    }
  });

  it('as pinturas da lista do modelo existem na malha (camada A/B) e as saídas do nitro ficam atrás', () => {
    for (const body of CAR_BODIES) {
      const m = models.get(body) as CarModel;
      const { paint, n } = attrs(m);
      const layers = new Set<number>();
      for (let i = 0; i < n; i++) layers.add(Math.round(paint.getZ(i) * 3));
      for (const liv of m.liveries) {
        if (liv & LIVERY_A) expect(layers.has(1) || layers.has(3), `${body}: camada A`).toBe(true);
        if (liv & LIVERY_B) expect(layers.has(2) || layers.has(3), `${body}: camada B`).toBe(true);
      }
      expect(m.exhausts.length, body).toBeGreaterThanOrEqual(1);
      expect(m.exhausts.length, body).toBeLessThanOrEqual(2);
      const bb = m.shell.boundingBox;
      for (const e of m.exhausts) {
        expect(e[2], body).toBeGreaterThan((bb?.max.z ?? 0) - 0.25);
        expect(Math.abs(e[0]), body).toBeLessThan(0.95);
      }
    }
  });

  it('orçamento: casco ≤ 2.000 triângulos, roda ≤ 360 e a roda da qualidade baixa ≤ 130', () => {
    for (const body of CAR_BODIES) expect((models.get(body) as CarModel).triangles, body).toBeLessThanOrEqual(2000);
    for (const d of WHEEL_DESIGNS) expect(buildWheel(d).triangles, d).toBeLessThanOrEqual(360);
    expect(buildSimpleWheel().triangles).toBeLessThanOrEqual(130);
  });

  it('as rodas de todo estilo são de um desenho conhecido, fechadas e com as faces para fora', () => {
    for (const body of CAR_BODIES) expect(WHEEL_DESIGNS, body).toContain((models.get(body) as CarModel).wheel);
    for (const d of WHEEL_DESIGNS) {
      const w = buildWheel(d);
      expect(signedVolume(w.geometry.getAttribute('position').array as Float32Array), d).toBeGreaterThan(1.5);
      expect(w.symmetry, d).toBeGreaterThan(0);
    }
  });
});

describe('pintura dos carros', () => {
  it('dois carros do mesmo estilo nunca saem com a mesma pintura (Falcão GT × Boitatá GT)', () => {
    const gts = CARS.filter((c) => c.body === 'gt');
    expect(gts.length).toBeGreaterThanOrEqual(2);
    const order = (models.get('gt') as CarModel).liveries;
    const a = carPaint(gts[0], CARS, order); const b = carPaint(gts[1], CARS, order);
    expect(a.livery).not.toBe(b.livery);
    // Vale para qualquer estilo com dois carros e ao menos duas pinturas na lista.
    for (const body of CAR_BODIES) {
      const same = CARS.filter((c) => c.body === body);
      const liv = (models.get(body) as CarModel).liveries;
      if (same.length >= 2 && liv.length >= 2) expect(carPaint(same[0], CARS, liv).livery, body).not.toBe(carPaint(same[1], CARS, liv).livery);
    }
  });

  it('segunda cor: a do carro quando existe, senão uma que contrasta', () => {
    for (const c of CARS) {
      const p = carPaint(c, CARS, [0]);
      expect(p.accent, c.id).toMatch(HEX);
      if (c.accent) expect(p.accent).toBe(c.accent);
      else expect(p.accent.toLowerCase(), c.id).not.toBe(c.color.toLowerCase());
    }
    expect(defaultAccent('#f2f2f2')).toMatch(HEX);
    expect(defaultAccent('#e53935')).not.toBe(defaultAccent('#1e88e5')); // quente × frio
    const custom: CarDef = { ...CARS[0], id: 'x', accent: '#123456' };
    expect(carPaint(custom, [custom], [0]).accent).toBe('#123456');
  });
});

describe('kit de modelagem', () => {
  it('primitivas fechadas saem com as faces para fora (volume assinado positivo e certo)', () => {
    const b1 = new MeshBuilder(); box(b1, [0.3, 0.2, -0.1], [1, 2, 3], PAINT);
    expect(signedVolume(b1.positions())).toBeCloseTo(6, 5);
    const b2 = new MeshBuilder(); cylinder(b2, { ...AXIS_X, o: [0, 0, 0] }, 1, -0.5, 0.5, 64, PAINT);
    expect(signedVolume(b2.positions())).toBeCloseTo(Math.PI, 1);
    const b3 = new MeshBuilder(); extrudeZY(b3, [[0, 0], [2, 0], [2, 1], [0, 1]], -0.5, 0.5, PAINT);
    expect(signedVolume(b3.positions())).toBeCloseTo(2, 5);
    const b4 = new MeshBuilder(); extrudeZY(b4, [[0, 1], [2, 1], [2, 0], [0, 0]], -0.5, 0.5, PAINT); // sentido contrário
    expect(signedVolume(b4.positions())).toBeCloseTo(2, 5);
    const b5 = new MeshBuilder(); lathe(b5, { ...AXIS_X, o: [0, 0, 0] }, [[0, -1], [1, -1], [1, 1], [0, 1]], 48, () => PAINT);
    expect(signedVolume(b5.positions())).toBeGreaterThan(6);
    const sq: P2[] = [[1, -1], [1, 1], [-1, 1], [-1, -1]];
    const b6 = new MeshBuilder(); loft(b6, [sq, sq], [0, 3], () => PAINT, true, PAINT, PAINT);
    expect(signedVolume(b6.positions())).toBeCloseTo(12, 5);
    const b7 = new MeshBuilder(); b7.mirrored(() => box(b7, [1, 0, 0], [1, 1, 1], PAINT));
    expect(signedVolume(b7.positions())).toBeCloseTo(2, 5);
    // Interior (caçamba/cockpit): faces para dentro, sem tampa.
    const b8 = new MeshBuilder(); insideBox(b8, [0, 0, 0], [1, 1, 1], PAINT);
    expect(b8.triangles).toBe(10);
  });

  it('triângulo degenerado é descartado', () => {
    const b = new MeshBuilder();
    b.tri([0, 0, 0], [1, 0, 0], [2, 0, 0], PAINT);
    expect(b.triangles).toBe(0);
  });

  it('giro da roda na tela: o real até o limite anti-estroboscópio', () => {
    const sym = (Math.PI * 2) / 5;
    expect(spinStep(0.1, sym)).toBeCloseTo(0.1, 10);
    expect(spinStep(10, sym)).toBeCloseTo(sym * MAX_SPIN_FRACTION, 10);
    expect(spinStep(-1, sym)).toBe(0);
    expect(MAX_SPIN_FRACTION).toBeLessThan(0.5); // abaixo de meio passo o desenho nunca "anda para trás"
  });
});
