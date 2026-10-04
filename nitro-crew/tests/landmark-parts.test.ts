// Peças baixadas (docs/ARTE.md, "Peças baixadas"): um bicho ou uma estátua da galeria CC0 do Meshy convertido para o
// estilo vira uma PEÇA de um marco procedural (src/assets/landmarks/parts/<peça>.glb) — o tuiuiú no ninho, os jacarés
// na baía, a manada de búfalos com as garças, o par de girafas, as renas da vila, o cavalo da Cavalhada, o troll da
// placa, o par de shisas e o garimpeiro de bronze. Aqui: o validador da peça, o carregador, os 9 construtores usando a
// peça quando ela existe e caindo no procedural quando não, o orçamento de triângulos com as instâncias, os caches do
// catálogo e o sombreado suave; e, no conversor (scripts/lib/landmark-convert.ts), o modo peça, a pintura por região
// (--paint) e a paleta por claridade (--by-light). Peças sintéticas geradas aqui mesmo; tudo no Node, sem DOM.
import * as THREE from 'three';
import { afterEach, beforeAll, describe, expect, it } from 'vitest';
import { exportGlb } from '../src/render/cars/template';
import { getModel, LANDMARK_PREFIX, modelBounds } from '../src/render/scenery/catalog';
import type { Geo, Model } from '../src/render/scenery/geom';
import { LANDMARKS } from '../src/render/scenery/landmarks';
import { checkLandmarkModel, checkLandmarkPart, LANDMARK_LIMITS } from '../src/render/scenery/landmarks/check';
import { parseLandmarkPartGlb } from '../src/render/scenery/landmarks/gltf';
import { landmarkPart, LandmarkPart, PART_NAMES, PART_SPECS, setLandmarkPart, type PartName } from '../src/render/scenery/landmarks/parts';
import { landmarkScene } from '../src/render/scenery/landmarks/template';
import { modelTriangles } from '../src/render/scenery/vegetation';
import { installNodeFileReader } from '../scripts/lib/node-filereader';
import { convertLandmark, parsePaintRule, quantize, type Pixels } from '../scripts/lib/landmark-convert';
import { syntheticAnimal, syntheticLandmark } from '../scripts/lib/synthetic-landmark';

beforeAll(installNodeFileReader);
afterEach(() => { for (const n of PART_NAMES) setLandmarkPart(n, null); });

/** Os 9 marcos que ganham peças (o pedido do dono, 04/10). */
const SCENES = ['tuiuiu_ninho', 'jacare', 'bufalo', 'girafa', 'vila_lapponia', 'placa_trolls', 'shisa', 'cavalhada', 'monumento_garimpeiro'];
const lm = (id: string) => LANDMARK_PREFIX + id;

// Cor marcadora por peça: verde exatamente 0 (o sombreado por altura e o salpicado multiplicam os três canais pelo
// mesmo fator, então o zero fica zero e a razão azul/vermelho fica) e azul/vermelho = (índice + 1) / 16.
const ratioOf = (name: PartName) => (PART_NAMES.indexOf(name) + 1) / 16;

/** Triângulos com a cor marcadora da peça. */
function markerTris(m: Model, name: PartName): number {
  let n = 0;
  for (const p of m.parts) {
    const c = p.geometry.getAttribute('color');
    if (!c) continue;
    for (let i = 0; i < c.count; i += 3) {
      const r = c.getX(i); const g = c.getY(i); const b = c.getZ(i);
      if (g === 0 && r > 0 && Math.abs(b / r - ratioOf(name)) < 0.01) n++;
    }
  }
  return n;
}

/** Vértices com a cor marcadora da peça (para medir onde ela ficou). */
function markerPoints(m: Model, name: PartName): THREE.Vector3[] {
  const out: THREE.Vector3[] = [];
  for (const p of m.parts) {
    const c = p.geometry.getAttribute('color'); const pos = p.geometry.getAttribute('position');
    if (!c) continue;
    for (let i = 0; i < c.count; i++) {
      const r = c.getX(i); const b = c.getZ(i);
      if (c.getY(i) === 0 && r > 0 && Math.abs(b / r - ratioOf(name)) < 0.01) out.push(new THREE.Vector3().fromBufferAttribute(pos, i));
    }
  }
  return out;
}

/**
 * Peça sintética da peça `name`: um elipsoide com a "cabeça" (a ponta de +X) puxada para cima — a frente da peça é
 * +X —, base em y = 0, pegada centrada, na medida da especificação (altura ou comprimento; o bicho medido pelo
 * comprimento sai baixo e comprido, o outro alto), com exatamente `tris` triângulos (não indexada, com cor por vértice
 * e normal da face). `k` multiplica a medida.
 */
function synthPart(name: PartName, tris: number, k = 1): Geo {
  const spec = PART_SPECS[name];
  const long = spec.measure === 'length';
  const W = 24; const H = Math.max(3, Math.ceil(tris / (2 * W)) + 1);
  const g = new THREE.SphereGeometry(1, W, H).toNonIndexed();
  g.deleteAttribute('uv');
  const p = g.getAttribute('position') as THREE.BufferAttribute;
  for (let i = 0; i < p.count; i++) {
    const x = p.getX(i); const y = p.getY(i);
    p.setXYZ(i, x * (long ? 1.2 : 0.5), y * (long ? 0.15 : 0.5) + (y > 0 && x > 0 ? (long ? 0.1 : 0.6) * x * x : 0), p.getZ(i) * 0.3);
  }
  const all = p.array as Float32Array;
  const pos = new Float32Array(tris * 9);
  pos.set(all.subarray(0, Math.min(all.length, tris * 9)));
  const out = new THREE.BufferGeometry();
  out.setAttribute('position', new THREE.BufferAttribute(pos, 3));
  out.computeBoundingBox();
  const bb = out.boundingBox ?? new THREE.Box3();
  const size = spec.measure === 'length' ? bb.max.x - bb.min.x : bb.max.y - bb.min.y;
  const s = (spec.size * k) / size;
  out.translate(-(bb.min.x + bb.max.x) / 2, -bb.min.y, -(bb.min.z + bb.max.z) / 2).scale(s, s, s);
  const col = new Float32Array(tris * 9);
  for (let i = 0; i < tris * 3; i++) col.set([1, 0, ratioOf(name)], i * 3);
  out.setAttribute('color', new THREE.BufferAttribute(col, 3));
  out.computeVertexNormals();
  return out;
}

/** Texto por triângulo (centro + cor), ordenado: compara dois modelos sem depender da ordem. */
function faces(m: Model): string[] {
  const out: string[] = [];
  for (const part of m.parts) {
    const p = part.geometry.getAttribute('position'); const c = part.geometry.getAttribute('color');
    for (let i = 0; i < p.count; i += 3) {
      let s = `${part.mat}:`;
      for (let k = 0; k < 3; k++) s += `${p.getX(i + k).toFixed(4)},${p.getY(i + k).toFixed(4)},${p.getZ(i + k).toFixed(4)}`;
      if (c) s += `|${c.getX(i).toFixed(5)},${c.getY(i).toFixed(5)},${c.getZ(i).toFixed(5)}`;
      out.push(s);
    }
  }
  return out.sort();
}

describe('peças baixadas: a especificação (parts.ts)', () => {
  it('as 10 peças do pedido, cada uma usada por um dos 9 marcos; os 9 marcos estão todos cobertos', () => {
    expect([...PART_NAMES].sort()).toEqual(['bufalo', 'cavalo', 'garca', 'garimpeiro', 'girafa', 'jacare', 'rena', 'shisa', 'troll', 'tuiuiu']);
    const users = new Set(PART_NAMES.flatMap((n) => Object.keys(PART_SPECS[n].users)));
    expect([...users].sort()).toEqual([...SCENES].sort());
    for (const n of PART_NAMES) {
      const s = PART_SPECS[n];
      expect(s.tris, n).toBeLessThanOrEqual(s.maxTris);
      expect(s.tris, n).toBeGreaterThanOrEqual(150);
      for (const [id, count] of Object.entries(s.users)) {
        expect(Object.prototype.hasOwnProperty.call(LANDMARKS, id), `${n} → ${id}`).toBe(true);
        expect(count, `${n} em ${id}`).toBeGreaterThanOrEqual(1);
      }
    }
  });
});

describe('peças baixadas: o validador (check.ts)', () => {
  it('a peça no tamanho e no teto de triângulos passa (todas as 10)', () => {
    for (const n of PART_NAMES) expect(checkLandmarkPart(synthPart(n, PART_SPECS[n].maxTris), n), n).toEqual([]);
  });

  it('acusa triângulos acima do teto, escala errada (centímetros, pequena demais), base fora do chão e pegada fora do centro', () => {
    const cap = PART_SPECS.girafa.maxTris;
    expect(checkLandmarkPart(synthPart('girafa', cap + 1), 'girafa').join(' | ')).toMatch(new RegExp(`${(cap + 1).toLocaleString('pt-BR')} triângulos.*${cap.toLocaleString('pt-BR')}`));
    expect(checkLandmarkPart(synthPart('girafa', 400, 100), 'girafa').join(' | ')).toMatch(/altura .* escala/);
    expect(checkLandmarkPart(synthPart('girafa', 400, 0.1), 'girafa').join(' | ')).toMatch(/altura .* escala/);
    expect(checkLandmarkPart(synthPart('jacare', 400, 100), 'jacare').join(' | ')).toMatch(/comprimento .* escala/);
    expect(checkLandmarkPart(synthPart('girafa', 400).translate(0, 0.5, 0), 'girafa').join(' | ')).toMatch(/base.*chão/);
    expect(checkLandmarkPart(synthPart('girafa', 400).translate(2, 0, 0), 'girafa').join(' | ')).toMatch(/centr/);
  });

  it('acusa malha indexada, cor faltando, coordenada inválida, peça vazia e nome desconhecido', () => {
    const indexed = new THREE.SphereGeometry(1, 8, 6).translate(0, 1, 0).scale(2, 2, 2);
    expect(checkLandmarkPart(indexed, 'girafa').join(' | ')).toMatch(/indexada/);
    const grey = synthPart('girafa', 400); grey.deleteAttribute('color');
    expect(checkLandmarkPart(grey, 'girafa').join(' | ')).toMatch(/cor por vértice/);
    const nan = synthPart('girafa', 400); (nan.getAttribute('position') as THREE.BufferAttribute).setX(5, Number.NaN);
    expect(checkLandmarkPart(nan, 'girafa').join(' | ')).toMatch(/inválida/);
    const empty = new THREE.BufferGeometry(); empty.setAttribute('position', new THREE.Float32BufferAttribute([], 3));
    expect(checkLandmarkPart(empty, 'girafa').join(' | ')).toMatch(/sem triângulos/);
    expect(checkLandmarkPart(synthPart('girafa', 400), 'elefante').join(' | ')).toMatch(/"elefante" não é uma peça/);
  });

  it('o .glb da peça volta pelo leitor do jogo; material de luz (glow) numa peça é recusado', async () => {
    const good = await exportGlb(landmarkScene({ parts: [{ geometry: synthPart('rena', 300), mat: 'flat' }] }, 'rena'));
    const r = await parseLandmarkPartGlb('rena', good);
    expect(r.problems).toEqual([]);
    expect(r.geometry?.getAttribute('position').count).toBe(900);
    const lit = await exportGlb(landmarkScene({ parts: [{ geometry: synthPart('rena', 300), mat: 'flat' }, { geometry: synthPart('rena', 20), mat: 'glow' }] }, 'rena'));
    const bad = await parseLandmarkPartGlb('rena', lit);
    expect(bad.geometry).toBeNull();
    expect(bad.problems.join(' | ')).toMatch(/"glow".*peça/);
    const broken = await parseLandmarkPartGlb('rena', new Uint8Array([0, 0, 0, 0]).buffer);
    expect(broken.problems.join(' | ')).toMatch(/não abre como glTF/);
  });
});

describe('peças baixadas: o carregador (landmarks/assets.ts)', () => {
  const dataUrl = (b: ArrayBuffer) => `data:model/gltf-binary;base64,${Buffer.from(b).toString('base64')}`;

  it('carrega a peça válida e o marco passa a usá-la; nome que não é peça, arquivo quebrado e fora de escala ficam de fora', async () => {
    const { loadLandmarkParts } = await import('../src/render/scenery/landmarks/assets');
    const glb = async (g: Geo) => dataUrl(await exportGlb(landmarkScene({ parts: [{ geometry: g, mat: 'flat' }] }, 'p')));
    const before = modelTriangles(getModel(lm('girafa')));
    const report = await loadLandmarkParts({
      '../../../assets/landmarks/parts/girafa.glb': async () => glb(synthPart('girafa', 500)),
      '../../../assets/landmarks/parts/elefante.glb': async () => glb(synthPart('girafa', 500)),
      '../../../assets/landmarks/parts/rena.glb': async () => 'data:model/gltf-binary;base64,AAAA',
      '../../../assets/landmarks/parts/bufalo.glb': async () => glb(synthPart('bufalo', 300, 100)),
    });
    expect(report.loaded).toEqual(['girafa']);
    const rejected = Object.fromEntries(report.rejected.map((r) => [r.file, r.problems.join(' | ')]));
    expect(rejected['elefante.glb']).toMatch(/não é uma peça/);
    expect(rejected['rena.glb']).toMatch(/não abre como glTF/);
    expect(rejected['bufalo.glb']).toMatch(/altura/);
    expect(landmarkPart('girafa')).not.toBeNull();
    expect(landmarkPart('rena')).toBeNull();
    expect(landmarkPart('bufalo')).toBeNull();
    expect(markerTris(getModel(lm('girafa')), 'girafa')).toBe(500 * PART_SPECS.girafa.users.girafa);
    setLandmarkPart('girafa', null);
    expect(modelTriangles(getModel(lm('girafa')))).toBe(before);
  }, 60_000);
});

describe('peças baixadas: os 9 marcos usam a peça e, sem ela, ficam como eram', () => {
  for (const name of ['tuiuiu', 'jacare', 'bufalo', 'garca', 'girafa', 'rena', 'cavalo', 'troll', 'shisa', 'garimpeiro'] as PartName[]) {
    it(`${name}: entra ${Object.entries(PART_SPECS[name].users).map(([id, n]) => `${n}× em ${id}`).join(', ')}; determinístico; volta ao procedural`, () => {
      for (const [id, count] of Object.entries(PART_SPECS[name].users)) {
        const procedural = faces(getModel(lm(id)));
        expect(markerTris(getModel(lm(id)), name), `${id} sem peça`).toBe(0);
        const tris = PART_SPECS[name].tris;
        setLandmarkPart(name, synthPart(name, tris));
        const withPart = getModel(lm(id));
        expect(markerTris(withPart, name), id).toBe(count * tris);
        expect(checkLandmarkModel(withPart, LANDMARKS[id].place), id).toEqual([]);
        // Mesma peça, mesmo marco (o construtor não sorteia nada).
        expect(faces(LANDMARKS[id].build()), id).toEqual(faces(LANDMARKS[id].build()));
        // A peça fica dentro do marco: no chão (o búfalo n'água e o jacaré nadando afundam um pouco) e na pegada.
        const pts = markerPoints(withPart, name);
        const b = modelBounds(lm(id));
        for (const v of pts) {
          expect(v.y, id).toBeGreaterThan(-1.5);
          expect(v.x, id).toBeGreaterThanOrEqual(b.minX - 1e-6); expect(v.x, id).toBeLessThanOrEqual(b.maxX + 1e-6);
        }
        setLandmarkPart(name, null);
        expect(faces(getModel(lm(id))), `${id} de volta ao procedural`).toEqual(procedural);
      }
    }, 60_000);
  }

  it('a peça não muda a peça registrada (cada instância é uma cópia) nem o tamanho dela entre marcos', () => {
    const g = synthPart('garca', 200);
    setLandmarkPart('garca', g);
    const before = (landmarkPart('garca')?.geometry.getAttribute('position').array as Float32Array).slice();
    getModel(lm('bufalo'));
    expect(Array.from(landmarkPart('garca')?.geometry.getAttribute('position').array as Float32Array)).toEqual(Array.from(before));
  });

  it('girafa: a cabeça da peça (+X) vai para onde a procedural olha (+Z, girada); o par fica com tamanhos diferentes', () => {
    setLandmarkPart('girafa', synthPart('girafa', 400));
    const pts = markerPoints(getModel(lm('girafa')), 'girafa');
    // As duas girafas: a de trás em z ≈ −2,5 e a menor em z ≈ 3 (procedural: cabeça para +Z).
    const back = pts.filter((v) => v.z < 0.25); const front = pts.filter((v) => v.z >= 0.25);
    const top = (l: THREE.Vector3[]) => l.reduce((a, v) => (v.y > a.y ? v : a));
    expect(top(back).z).toBeGreaterThan(-2.5);
    expect(top(front).z).toBeGreaterThan(3.0);
    expect(top(back).y).toBeGreaterThan(top(front).y * 1.2);
  });

  it('encaixe pelo lombo (cavalo sob o cavaleiro, búfalo do vaqueiro): o lombo da peça escalada fica na altura pedida', () => {
    const p = new LandmarkPart('cavalo', synthPart('cavalo', 400));
    const s = p.scaleFor({ back: 4.4 });
    expect(p.back * s).toBeCloseTo(4.4, 6);
    const h = p.scaleFor({ height: 3 });
    expect(p.bounds.maxY * h).toBeCloseTo(3, 6);
    const j = new LandmarkPart('jacare', synthPart('jacare', 400));
    expect((j.bounds.maxX - j.bounds.minX) * j.scaleFor({ length: 5.67 })).toBeCloseTo(5.67, 6);
  });
});

describe('peças baixadas: orçamento de triângulos com as instâncias', () => {
  it(`cada marco com TODAS as suas peças no teto de triângulos cabe no orçamento do lugar (perto ${LANDMARK_LIMITS.budget.near.toLocaleString('pt-BR')})`, () => {
    for (const n of PART_NAMES) setLandmarkPart(n, synthPart(n, PART_SPECS[n].maxTris));
    const report: string[] = [];
    for (const id of SCENES) {
      const m = getModel(lm(id));
      const tris = modelTriangles(m);
      report.push(`${id}: ${tris}`);
      expect(tris, id).toBeLessThanOrEqual(LANDMARK_LIMITS.budget[LANDMARKS[id].place]);
      expect(checkLandmarkModel(m, LANDMARKS[id].place), id).toEqual([]);
      for (const n of PART_NAMES) {
        const count = PART_SPECS[n].users[id] ?? 0;
        expect(markerTris(m, n), `${n} em ${id}`).toBe(count * PART_SPECS[n].maxTris);
      }
    }
  });
});

describe('peças baixadas: caches do catálogo e sombreado', () => {
  it('peça registrada depois de o marco ter sido montado e medido: o catálogo esquece o modelo e as medidas', () => {
    const first = getModel(lm('girafa'));
    const box = { ...modelBounds(lm('girafa')) };
    setLandmarkPart('girafa', synthPart('girafa', 300, 1.6));
    const second = getModel(lm('girafa'));
    expect(second).not.toBe(first);
    expect(markerTris(second, 'girafa')).toBeGreaterThan(0);
    expect(modelBounds(lm('girafa'))).not.toEqual(box);
    setLandmarkPart('girafa', null);
    expect(markerTris(getModel(lm('girafa')), 'girafa')).toBe(0);
    expect(modelBounds(lm('girafa'))).toEqual(box);
  });

  it('a peça passa pelo smoothModel com o marco: a superfície curva fica lisa (normal do canto ≠ normal da face)', () => {
    setLandmarkPart('troll', synthPart('troll', 800));
    const m = getModel(lm('placa_trolls'));
    let bent = 0; let total = 0;
    const a = new THREE.Vector3(); const b = new THREE.Vector3(); const c = new THREE.Vector3(); const n = new THREE.Vector3();
    for (const part of m.parts) {
      const col = part.geometry.getAttribute('color'); const pos = part.geometry.getAttribute('position'); const nor = part.geometry.getAttribute('normal');
      for (let i = 0; i < pos.count; i += 3) {
        if (!(col.getY(i) === 0 && col.getX(i) > 0 && Math.abs(col.getZ(i) / col.getX(i) - ratioOf('troll')) < 0.01)) continue;
        a.fromBufferAttribute(pos, i); b.fromBufferAttribute(pos, i + 1); c.fromBufferAttribute(pos, i + 2);
        const fn = b.clone().sub(a).cross(c.clone().sub(a)).normalize();
        for (let k = 0; k < 3; k++) { total++; if (n.fromBufferAttribute(nor, i + k).dot(fn) < Math.cos((2 * Math.PI) / 180)) bent++; }
      }
    }
    expect(total).toBe(800 * 3);
    expect(bent / total).toBeGreaterThan(0.5);
  });
});

// ───────────────────────── Conversor: modo peça, --paint e --by-light ─────────────────────────

const read = (t: THREE.Texture) => (t.image && 'data' in (t.image as object) ? (t.image as Pixels) : null);

function boundsOf(m: Model): THREE.Box3 {
  const b = new THREE.Box3();
  for (const p of m.parts) { p.geometry.computeBoundingBox(); if (p.geometry.boundingBox) b.union(p.geometry.boundingBox); }
  return b;
}

describe('conversor: modo peça (--part)', () => {
  it('sem lugar: altura pedida, alvo de triângulos da peça, uma parte lisa só, frente em +X (o comprido em X); passa no validador da peça', async () => {
    const { model, report } = await convertLandmark(syntheticAnimal('boi', 0.6), read, { part: 'bufalo', height: 1.7, colors: 8 });
    expect(model.parts.map((p) => p.mat)).toEqual(['flat']);
    expect(modelTriangles(model)).toBeLessThanOrEqual(PART_SPECS.bufalo.tris);
    expect(modelTriangles(model)).toBeGreaterThan(PART_SPECS.bufalo.tris * 0.6);
    expect(report.problems).toEqual([]);
    expect(report.part).toBe('bufalo');
    expect(checkLandmarkPart(model.parts[0].geometry, 'bufalo')).toEqual([]);
    const b = boundsOf(model);
    expect(b.max.y).toBeCloseTo(1.7, 2);
    expect(b.min.y).toBeCloseTo(0, 4);
    expect(Math.abs(b.max.x + b.min.x)).toBeLessThan(0.01);
    expect(Math.abs(b.max.z + b.min.z)).toBeLessThan(0.01);
    expect(b.max.x - b.min.x).toBeGreaterThan(b.max.z - b.min.z);
  }, 60_000);

  it('material emissivo não vira luz numa peça (tudo na parte lisa); o alvo padrão é o da peça', async () => {
    const { model, report } = await convertLandmark(syntheticLandmark(0.4), read, { part: 'garimpeiro', height: 2.5 });
    expect(model.parts.map((p) => p.mat)).toEqual(['flat']);
    expect(report.target).toBe(PART_SPECS.garimpeiro.tris);
    expect(report.glowTriangles).toBe(0);
  }, 60_000);

  it('--length: o comprimento (X, da cauda ao focinho) é o pedido — o jacaré mede 2,7 m', async () => {
    const { model } = await convertLandmark(syntheticAnimal('boi', 0.4), read, { part: 'jacare', length: 2.7, colors: 6 });
    const b = boundsOf(model);
    expect(b.max.x - b.min.x).toBeCloseTo(2.7, 2);
    expect(b.min.y).toBeCloseTo(0, 4);
  }, 60_000);
});

describe('conversor: pintura por região (--paint)', () => {
  it('lê a regra: condições em x, y, z normalizados (0–1) e a cor', () => {
    expect(parsePaintRule('y>0.62:#1b1b1b')).toEqual({ when: [{ axis: 'y', op: '>', value: 0.62 }], color: '#1b1b1b' });
    expect(parsePaintRule(' y>0.58, y<0.62 , x>=0.5 : #C8202A ')).toEqual({
      when: [{ axis: 'y', op: '>', value: 0.58 }, { axis: 'y', op: '<', value: 0.62 }, { axis: 'x', op: '>=', value: 0.5 }], color: '#c8202a',
    });
    expect(parsePaintRule('z<=0.3:#fff')).toEqual({ when: [{ axis: 'z', op: '<=', value: 0.3 }], color: '#ffffff' });
    expect(() => parsePaintRule('y>0.6')).toThrow(/--paint/);
    expect(() => parsePaintRule('w>0.6:#000000')).toThrow(/--paint/);
    expect(() => parsePaintRule('y>1.5:#000000')).toThrow(/0 a 1/);
    expect(() => parsePaintRule('y>0.5:verde')).toThrow(/cor/);
  });

  it('pinta as faces cujo centro cai na região (a última regra que casa vence) e as cores entram na paleta do relatório', async () => {
    const paint = [parsePaintRule('y>0.62:#1b1b1b'), parsePaintRule('y>0.58,y<0.62:#c8202a'), parsePaintRule('x<0.2,y>0.62:#2266ee')];
    const { model, report } = await convertLandmark(syntheticAnimal('boi', 0.5), read, { part: 'tuiuiu', height: 1.6, colors: 6, paint });
    const g = model.parts[0].geometry;
    const p = g.getAttribute('position'); const c = g.getAttribute('color');
    const b = boundsOf(model);
    const lin = (hex: string) => new THREE.Color(hex);
    const near = (i: number, hex: string) => { const k = lin(hex); return Math.hypot(c.getX(i) - k.r, c.getY(i) - k.g, c.getZ(i) - k.b) < 1e-4; };
    let black = 0; let red = 0; let blue = 0;
    for (let i = 0; i < p.count; i += 3) {
      const y = ((p.getY(i) + p.getY(i + 1) + p.getY(i + 2)) / 3 - b.min.y) / (b.max.y - b.min.y);
      const x = ((p.getX(i) + p.getX(i + 1) + p.getX(i + 2)) / 3 - b.min.x) / (b.max.x - b.min.x);
      if (y > 0.62 && x < 0.2) { expect(near(i, '#2266ee'), `face ${i / 3}`).toBe(true); blue++; } else if (y > 0.62) { expect(near(i, '#1b1b1b'), `face ${i / 3}`).toBe(true); black++; } else if (y > 0.58 && y < 0.62) { expect(near(i, '#c8202a'), `face ${i / 3}`).toBe(true); red++; } else expect(near(i, '#1b1b1b') || near(i, '#c8202a') || near(i, '#2266ee'), `face ${i / 3}`).toBe(false);
    }
    expect(black).toBeGreaterThan(0); expect(red).toBeGreaterThan(0);
    const hexes = report.palette.map((q) => q.hex);
    expect(hexes).toContain('#1b1b1b'); expect(hexes).toContain('#c8202a');
    if (blue > 0) expect(hexes).toContain('#2266ee');
  }, 60_000);
});

describe('conversor: paleta por claridade (--palette … --by-light)', () => {
  const bronze = ['#4a3520', '#6e5030', '#9a7444', '#c8a060'].map((h) => new THREE.Color(h));

  it('cada cor vai para o tom de mesma claridade relativa (escuro → escuro, claro → claro), sem olhar o matiz', () => {
    // Azul escuro, vermelho médio, amarelo claro: pela claridade, o azul (o mais escuro) vai ao tom mais escuro, o
    // vermelho ao segundo e o amarelo (o mais claro) ao mais claro — o matiz não conta.
    const colors = new Float32Array([0.02, 0.02, 0.2, 0.4, 0.05, 0.05, 0.9, 0.85, 0.3]);
    const w = new Float32Array(3).fill(1);
    const q = quantize(colors, w, 12, { fixed: bronze, byLight: true });
    expect(Array.from(q.index)).toEqual([0, 1, 3]);
    // A ordem dos tons na lista não importa: vale a claridade de cada um.
    const shuffled = [bronze[2], bronze[0], bronze[3], bronze[1]];
    const r = quantize(colors, w, 12, { fixed: shuffled, byLight: true });
    expect(Array.from(r.index).map((i) => shuffled[i].getHexString())).toEqual([bronze[0], bronze[1], bronze[3]].map((c) => c.getHexString()));
  });

  it('no conversor: a estátua vira bronze — só os tons da paleta, do mais escuro (o pescoço preto) ao mais claro (o corpo branco)', async () => {
    const palette = ['#4a3520', '#6e5030', '#9a7444', '#c8a060'];
    const { report } = await convertLandmark(syntheticLandmark(0.4), read, { part: 'garimpeiro', height: 2.5, palette, byLight: true });
    const used = report.palette.map((p) => p.hex);
    for (const h of used) expect(palette).toContain(h);
    expect(used).toContain('#4a3520');
    expect(used).toContain('#c8a060');
  }, 60_000);
});
