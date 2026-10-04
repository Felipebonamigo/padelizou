// Sombreamento suave com vinco (pedido do dono: "menos quadrado"). A utilidade `smoothNormals` (malha não
// indexada, cor por face) e a garantia de que cenário e carros saem com ela — uma volta ao sombreado plano
// (flatShading, ou o modelo sem a normal suave) quebra aqui. Tudo no Node, sem DOM (o Scenery usa um
// documento mínimo, como em scenery.test.ts).
import * as THREE from 'three';
import { describe, expect, it } from 'vitest';
import { CAR_BODIES } from '../src/core/data/cars';
import { getTrack } from '../src/core/track';
import { carModelFromGltf } from '../src/render/cars/gltf';
import { createCarMaterial } from '../src/render/cars/material';
import { MODEL_BUILDERS } from '../src/render/cars/models';
import { carTemplateScene } from '../src/render/cars/template';
import { buildWheel, WHEEL_DESIGNS } from '../src/render/cars/wheels';
import { CREASE, ROOF_TURN_DEG, smoothNormals, WELD_EPS } from '../src/render/normals';
import { buildRoadFrame } from '../src/render/roadframe';
import { getModel, LANDMARK_PREFIX, setLandmarkOverride } from '../src/render/scenery/catalog';
import { hip, sphere } from '../src/render/scenery/geom';
import { sceneryShading } from '../src/render/scenery/smooth';

/** Cópia com a normal da própria face em cada vértice (o que `computeVertexNormals` dá na malha não indexada). */
function flatCopy(g: THREE.BufferGeometry): THREE.BufferGeometry {
  const c = g.clone();
  c.deleteAttribute('normal');
  c.computeVertexNormals();
  return c;
}

function faceNormal(pos: THREE.BufferAttribute | THREE.InterleavedBufferAttribute, f: number): THREE.Vector3 {
  const a = new THREE.Vector3().fromBufferAttribute(pos, f * 3);
  const b = new THREE.Vector3().fromBufferAttribute(pos, f * 3 + 1);
  const c = new THREE.Vector3().fromBufferAttribute(pos, f * 3 + 2);
  return b.sub(a).cross(c.sub(a)).normalize();
}

/** Fração dos cantos cuja normal se afasta mais de `deg` graus da normal da própria face. */
function smoothFraction(g: THREE.BufferGeometry, deg = 2): number {
  const pos = g.getAttribute('position'); const nor = g.getAttribute('normal');
  const lim = Math.cos((deg * Math.PI) / 180);
  let bent = 0;
  for (let i = 0; i < pos.count; i++) {
    const fn = faceNormal(pos, Math.floor(i / 3));
    if (fn.lengthSq() === 0) continue;
    if (new THREE.Vector3().fromBufferAttribute(nor, i).dot(fn) < lim) bent++;
  }
  return bent / pos.count;
}

function maxDiff(a: ArrayLike<number>, b: ArrayLike<number>): number {
  let m = 0;
  for (let i = 0; i < a.length; i++) m = Math.max(m, Math.abs(a[i] - b[i]));
  return m;
}

const cylinder = (seg = 12) => flatCopy(new THREE.CylinderGeometry(1, 1, 2, seg, 1, false).toNonIndexed());

describe('normais suaves: a utilidade (malha não indexada)', () => {
  it('cilindro: o costado fica liso (a normal é a radial) e as tampas continuam planas', () => {
    const g = cylinder(12);
    const before = (g.getAttribute('position').array as Float32Array).slice();
    const tris = g.getAttribute('position').count / 3;
    smoothNormals(g, 45);
    expect(g.index).toBeNull();
    expect(g.getAttribute('position').count / 3).toBe(tris);
    expect(maxDiff(g.getAttribute('position').array as Float32Array, before)).toBe(0);
    const pos = g.getAttribute('position'); const nor = g.getAttribute('normal');
    let side = 0; let caps = 0;
    for (let i = 0; i < pos.count; i++) {
      const fn = faceNormal(pos, Math.floor(i / 3));
      const n = new THREE.Vector3().fromBufferAttribute(nor, i);
      if (Math.abs(fn.y) < 1e-6) {
        // Costado: no vértice, a normal é a direção radial — a mesma para as duas faces vizinhas.
        const radial = new THREE.Vector3(pos.getX(i), 0, pos.getZ(i)).normalize();
        expect(n.distanceTo(radial), `canto ${i}`).toBeLessThan(1e-5);
        side++;
      } else {
        expect(n.distanceTo(new THREE.Vector3(0, Math.sign(fn.y), 0)), `tampa ${i}`).toBeLessThan(1e-6);
        caps++;
      }
    }
    expect(side).toBe(12 * 6);
    expect(caps).toBe(12 * 3 * 2);
  });

  it('faces vizinhas do costado compartilham a normal no vértice em comum', () => {
    const g = smoothNormals(cylinder(10), 45);
    const pos = g.getAttribute('position'); const nor = g.getAttribute('normal');
    const byPoint = new Map<string, THREE.Vector3[]>();
    for (let i = 0; i < pos.count; i++) {
      if (Math.abs(faceNormal(pos, Math.floor(i / 3)).y) > 1e-6) continue;
      const k = [pos.getX(i), pos.getY(i), pos.getZ(i)].map((v) => Math.round(v * 1e4) + 0).join(','); // + 0: −0 vira 0 (costura)
      const list = byPoint.get(k) ?? [];
      list.push(new THREE.Vector3().fromBufferAttribute(nor, i));
      byPoint.set(k, list);
    }
    expect(byPoint.size).toBe(20);
    for (const [k, list] of byPoint) {
      expect(list.length, k).toBeGreaterThanOrEqual(2);
      for (const n of list) expect(n.distanceTo(list[0]), k).toBeLessThan(1e-6);
    }
  });

  it('caixa: as arestas de 90° continuam vivas (6 normais, cada vértice com a da própria face)', () => {
    const g = flatCopy(new THREE.BoxGeometry(2, 1, 3).toNonIndexed());
    const flat = (g.getAttribute('normal').array as Float32Array).slice();
    smoothNormals(g, 60);
    expect(maxDiff(g.getAttribute('normal').array as Float32Array, flat)).toBeLessThan(1e-6);
    const nor = g.getAttribute('normal');
    const distinct = new Set<string>();
    for (let i = 0; i < nor.count; i++) distinct.add([nor.getX(i), nor.getY(i), nor.getZ(i)].map((v) => Math.round(v)).join(','));
    expect(distinct.size).toBe(6);
  });

  it('o vinco decide: icosaedro (vizinhas a 41,8°, as do outro lado do vértice a 70,5°)', () => {
    const ico = () => flatCopy(new THREE.IcosahedronGeometry(1, 0));
    // 30°: nenhuma vizinha entra — facetado como antes.
    expect(smoothFraction(smoothNormals(ico(), 30), 0.5)).toBe(0);
    // 50°: as arestas (41,8°) ficam lisas — a normal sai da face para a radial (o leque inteiro: ver "sem costura").
    const mid = smoothNormals(ico(), 50);
    // 75°: idem, com folga — a normal é a radial (esfera lisa).
    const soft = smoothNormals(ico(), 75);
    const pos = soft.getAttribute('position');
    for (let i = 0; i < pos.count; i++) {
      const radial = new THREE.Vector3().fromBufferAttribute(pos, i).normalize();
      const face = faceNormal(pos, Math.floor(i / 3));
      const m = new THREE.Vector3().fromBufferAttribute(mid.getAttribute('normal'), i);
      expect(m.angleTo(radial), `canto ${i} (50°)`).toBeLessThan(face.angleTo(radial) - 0.1);
      expect(new THREE.Vector3().fromBufferAttribute(soft.getAttribute('normal'), i).distanceTo(radial), `canto ${i} (75°)`).toBeLessThan(1e-5);
    }
  });

  it('sem costura: no mesmo ponto de uma região lisa, todos os cantos têm a mesma normal (vale o leque, não a face)', () => {
    // Icosaedro com 50°: as arestas (41,8°) são todas lisas — as 5 faces de cada vértice formam um leque só, mesmo
    // com as do outro lado do vértice a 70,5° umas das outras. Normal por face (a média só das vizinhas dentro do
    // vinco) deixava cada canto com uma normal diferente no mesmo ponto: costura de luz em cada aresta.
    const g = smoothNormals(flatCopy(new THREE.IcosahedronGeometry(1, 0)), 50);
    const pos = g.getAttribute('position'); const nor = g.getAttribute('normal');
    for (let i = 0; i < pos.count; i++) {
      const radial = new THREE.Vector3().fromBufferAttribute(pos, i).normalize();
      expect(new THREE.Vector3().fromBufferAttribute(nor, i).distanceTo(radial), `canto ${i}`).toBeLessThan(1e-5);
    }
  });

  it('enrolamento trocado (o sombreado plano escondia): a face virada continua lisa e não entorta as vizinhas', () => {
    const g = cylinder(12);
    const pos = g.getAttribute('position') as THREE.BufferAttribute;
    // Vira a face 0 do costado (troca dois cantos): a normal dela passa a apontar para dentro.
    const v1 = [pos.getX(1), pos.getY(1), pos.getZ(1)];
    pos.setXYZ(1, pos.getX(2), pos.getY(2), pos.getZ(2));
    pos.setXYZ(2, v1[0], v1[1], v1[2]);
    smoothNormals(g, 45);
    const nor = g.getAttribute('normal');
    for (let i = 0; i < pos.count; i++) {
      const fn = faceNormal(pos, Math.floor(i / 3));
      if (Math.abs(fn.y) > 1e-6) continue;
      // Cada canto segue o sentido da própria face: a virada fica com a radial para dentro, as outras para fora.
      const radial = new THREE.Vector3(pos.getX(i), 0, pos.getZ(i)).normalize().multiplyScalar(i < 3 ? -1 : 1);
      expect(new THREE.Vector3().fromBufferAttribute(nor, i).distanceTo(radial), `canto ${i}`).toBeLessThan(1e-5);
    }
  });

  it('telhado de quatro águas baixo continua com as quinas vivas; cúpula de 8 gomos continua lisa', () => {
    // As águas de um telhado baixo dobram só ~28° entre si (menos que o vinco de um cilindro de 10 lados): pelo vinco
    // o telhado virava travesseiro. O que separa: as duas águas inclinadas viram 90° em planta (o gomo da cúpula, 45°).
    const roof = () => flatCopy(hip(19, 4, 27).toNonIndexed());
    expect(smoothFraction(smoothNormals(roof(), CREASE.built), 1)).toBeGreaterThan(0.2); // o defeito, sem a regra (as 4 águas; o resto é tampa)
    expect(smoothFraction(smoothNormals(roof(), CREASE.built, { roofTurnDeg: ROOF_TURN_DEG }), 1)).toBe(0);
    // Com cumeeira (o telhado da casa de fazenda): a água não se funde com a tira plana do topo — senão o leque do
    // canto de cima passa de uma água para a outra através dela e a quina some lá em cima.
    const ridge = flatCopy(hip(14, 3.2, 20, 0.6, 10).toNonIndexed());
    expect(smoothFraction(smoothNormals(ridge, CREASE.built, { roofTurnDeg: ROOF_TURN_DEG }), 1)).toBe(0);
    const dome = () => flatCopy(sphere(1, 8, 6, 0, Math.PI * 2, 0, Math.PI / 2).toNonIndexed());
    const plain = smoothNormals(dome(), CREASE.landmark);
    const ruled = smoothNormals(dome(), CREASE.landmark, { roofTurnDeg: ROOF_TURN_DEG });
    expect(smoothFraction(ruled)).toBeGreaterThan(0.5);
    expect(maxDiff(ruled.getAttribute('normal').array as Float32Array, plain.getAttribute('normal').array as Float32Array)).toBeLessThan(1e-6);
  });

  it('cor por face, posição e contagem de triângulos não mudam; o resultado é determinístico', () => {
    const g = flatCopy(new THREE.SphereGeometry(1, 8, 6).toNonIndexed());
    const n = g.getAttribute('position').count;
    const col = new Float32Array(n * 3);
    for (let f = 0; f < n / 3; f++) for (let k = 0; k < 3; k++) col.set([(f * 37 % 11) / 10, (f * 13 % 7) / 6, (f % 5) / 4], (f * 3 + k) * 3);
    g.setAttribute('color', new THREE.BufferAttribute(col.slice(), 3));
    const a = smoothNormals(g.clone(), 60);
    const b = smoothNormals(g.clone(), 60);
    expect(a.getAttribute('position').count).toBe(n);
    expect(Array.from(a.getAttribute('color').array)).toEqual(Array.from(col));
    expect(Array.from(a.getAttribute('normal').array)).toEqual(Array.from(b.getAttribute('normal').array));
    expect(smoothFraction(a)).toBeGreaterThan(0.5);
  });

  it('solda pontos quase iguais (contas em float32) e não mistura faces de sentidos opostos', () => {
    // Um canto do costado empurrado bem abaixo da tolerância: continua ligado aos vizinhos.
    const g = cylinder(12);
    const pos = g.getAttribute('position') as THREE.BufferAttribute;
    const nudge = WELD_EPS * 0.3;
    pos.setX(0, pos.getX(0) + nudge);
    smoothNormals(g, 45);
    const nor = g.getAttribute('normal');
    const radial = new THREE.Vector3(pos.getX(0), 0, pos.getZ(0)).normalize();
    expect(new THREE.Vector3().fromBufferAttribute(nor, 0).distanceTo(radial)).toBeLessThan(1e-3);

    // Folha de duas faces (o mesmo triângulo nos dois sentidos): cada uma fica com a própria normal.
    const leaf = new THREE.BufferGeometry();
    leaf.setAttribute('position', new THREE.Float32BufferAttribute([0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 1, 0, 0], 3));
    smoothNormals(leaf, 179);
    const ln = leaf.getAttribute('normal');
    for (let i = 0; i < 3; i++) expect(ln.getZ(i)).toBeCloseTo(1, 6);
    for (let i = 3; i < 6; i++) expect(ln.getZ(i)).toBeCloseTo(-1, 6);
  });

  it('triângulo degenerado não gera NaN; malha indexada é recusada', () => {
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute([0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 2, 0, 0], 3));
    smoothNormals(g, 60);
    for (const v of g.getAttribute('normal').array as Float32Array) expect(Number.isFinite(v)).toBe(true);
    expect(() => smoothNormals(new THREE.BoxGeometry(1, 1, 1), 60)).toThrow(/indexada/);
  });
});

describe('normais suaves: carros', () => {
  it('o material dos carros não usa sombreado plano', () => {
    expect(createCarMaterial().material.flatShading).toBe(false);
  });

  it('os vincos de carro e roda são de arredondar sem derreter caixas', () => {
    for (const k of ['car', 'wheel'] as const) {
      expect(CREASE[k], k).toBeGreaterThanOrEqual(20);
      expect(CREASE[k], k).toBeLessThan(70);
    }
  });

  it.each(CAR_BODIES.map((b) => [b] as const))('%s: o casco sai com as normais suaves do vinco de carro', (body) => {
    const shell = MODEL_BUILDERS[body]().shell;
    const want = smoothNormals(flatCopy(shell), CREASE.car);
    expect(maxDiff(shell.getAttribute('normal').array as Float32Array, want.getAttribute('normal').array as Float32Array)).toBeLessThan(1e-6);
    expect(smoothFraction(shell), body).toBeGreaterThan(0.2);
  });

  it('as rodas saem com as normais suaves do vinco de roda (pneu redondo)', () => {
    for (const d of WHEEL_DESIGNS) {
      const g = buildWheel(d).geometry;
      const want = smoothNormals(flatCopy(g), CREASE.wheel);
      expect(maxDiff(g.getAttribute('normal').array as Float32Array, want.getAttribute('normal').array as Float32Array), d).toBeLessThan(1e-6);
      expect(smoothFraction(g), d).toBeGreaterThan(0.2);
    }
  });

  it('o carro carregado de um glTF recebe o mesmo tratamento que o procedural', () => {
    for (const body of ['gt', 'classic'] as const) {
      const scene = carTemplateScene(body);
      const { model } = carModelFromGltf(body, scene);
      if (!model) throw new Error('sem modelo');
      const want = smoothNormals(flatCopy(model.shell), CREASE.car);
      expect(maxDiff(model.shell.getAttribute('normal').array as Float32Array, want.getAttribute('normal').array as Float32Array), body).toBeLessThan(1e-6);
      expect(smoothFraction(model.shell), body).toBeGreaterThan(0.2);
    }
  });
});

describe('normais suaves: cenário', () => {
  // Famílias redondas (árvore, conífera, palmeira, cacto, pedra, moita, marco com cúpula) e as de construção.
  const SAMPLE = [
    'tree:mango:0', 'tree:baobab', 'pine:spruce', 'palm:coco', 'cactus:saguaro3', 'rock:granite', 'bush:green', 'far:round-green',
    'lamp', 'stand:0', 'house:red', 'chapel:white', 'windmill', 'lm:cupula_azul', 'lm:catedral_brasilia', 'lm:cuia_chimarrao',
  ];

  it('as partes iluminadas dos modelos saem com as normais suaves do vinco da família', () => {
    for (const id of SAMPLE) {
      const shading = sceneryShading(id);
      expect(shading.crease, id).toBeGreaterThanOrEqual(20);
      expect(shading.crease, id).toBeLessThan(90); // caixa (90°) continua caixa
      for (const p of getModel(id).parts) {
        if (p.mat === 'glow' || p.mat === 'beacon' || p.mat === 'cone' || p.mat === 'panel') continue;
        const want = smoothNormals(flatCopy(p.geometry), shading.crease, shading);
        expect(maxDiff(p.geometry.getAttribute('normal').array as Float32Array, want.getAttribute('normal').array as Float32Array), `${id}/${p.mat}`).toBeLessThan(1e-6);
      }
    }
  });

  it('construções e marcos guardam a quina do telhado; vegetação, pedra e carro não precisam da regra', () => {
    for (const id of ['house:farm', 'house:japan', 'pagoda', 'lamp', 'lm:convento_penha', 'lm:cupula_azul']) expect(sceneryShading(id).roofTurnDeg, id).toBe(ROOF_TURN_DEG);
    for (const id of ['tree:oak', 'pine:spruce', 'rock:granite', 'far:round-green']) expect(sceneryShading(id).roofTurnDeg, id).toBeUndefined();
  });

  it('o que é redondo fica liso de verdade (árvore, pedra, cacto, cúpula)', () => {
    for (const id of ['tree:mango:0', 'pine:spruce', 'cactus:saguaro3', 'rock:granite', 'lm:cupula_azul']) {
      const flat = getModel(id).parts.filter((p) => p.mat === 'flat');
      const all = flat.reduce((s, p) => s + p.geometry.getAttribute('position').count, 0);
      const bent = flat.reduce((s, p) => s + smoothFraction(p.geometry) * p.geometry.getAttribute('position').count, 0);
      expect(bent / all, id).toBeGreaterThan(0.25);
    }
  });

  it('os materiais iluminados do cenário (liso, decoração, horizonte, fachadas) não usam sombreado plano', async () => {
    const ctx = new Proxy({}, { get: () => () => ({ addColorStop: () => undefined }), set: () => true });
    const g = globalThis as { document?: unknown };
    const had = g.document;
    g.document = { createElement: () => ({ width: 0, height: 0, getContext: () => ctx }) };
    try {
      const { Scenery } = await import('../src/render/scenery/runtime');
      const scenery = new Scenery();
      const track = getTrack('sampa_noite');
      scenery.update(buildRoadFrame(track, track.length * 0.2, 30, 260), track, 0);
      const mats = new Set<string>();
      for (const o of scenery.group.children) {
        const m = (o as THREE.Mesh).material as THREE.Material;
        if (!(m instanceof THREE.MeshStandardMaterial)) continue;
        mats.add(o.name);
        expect(m.flatShading, o.name).toBe(false);
      }
      for (const name of ['scenery:flat', 'scenery:dress', 'scenery:office']) expect([...mats]).toContain(name);
      scenery.dispose();
    } finally {
      g.document = had;
    }
  }, 60000);
});

describe('normais suaves: marco baixado e convertido (glTF)', () => {
  // Defeito da integração (onda I): o modelo de src/assets/landmarks/ entrava pelo atalho do arquivo no getModel e
  // pulava o smoothModel — um bicho convertido (forma orgânica, o motivo de baixá-lo) ficava todo facetado.
  it('o marco que vem de arquivo recebe o mesmo sombreado suave que o procedural', () => {
    const g = cylinder(16);
    const color = new Float32Array(g.getAttribute('position').count * 3).fill(0.8);
    g.setAttribute('color', new THREE.BufferAttribute(color, 3));
    const id = 'tuiuiu_ninho';
    setLandmarkOverride(id, { parts: [{ geometry: g, mat: 'flat' }], blob: null } as unknown as Parameters<typeof setLandmarkOverride>[1]);
    try {
      const m = getModel(LANDMARK_PREFIX + id);
      expect(smoothFraction(m.parts[0].geometry)).toBeGreaterThan(0.3);
    } finally {
      setLandmarkOverride(id, null);
    }
  });
});
