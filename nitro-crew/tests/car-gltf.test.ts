// Carros em glTF (passo 2.4 do roteiro): o validador da convenção do briefing de arte, a ida e volta
// pelos modelos-base (o carro do jogo exportado em .glb e carregado de volta sai igual) e o que o
// artista entrega sem os extras do jogo (materiais só pelo nome). Tudo no Node, sem DOM.
import * as THREE from 'three';
import { beforeAll, describe, expect, it } from 'vitest';
import { CAR_BODIES } from '../src/core/data/cars';
import type { CarBody } from '../src/core/types';
import { checkCarModel } from '../src/render/cars/check';
import { carModelFromGltf, parseCarGlb } from '../src/render/cars/gltf';
import type { CarModel } from '../src/render/cars/model';
import { MODEL_BUILDERS } from '../src/render/cars/models';
import { carTemplateScene, exportGlb } from '../src/render/cars/template';
import { installNodeFileReader } from '../scripts/lib/node-filereader';

beforeAll(installNodeFileReader);

/** Um texto por triângulo (centro + cor, pintura e material dos 3 vértices), em ordem: compara malhas sem depender da ordem das faces. */
function faces(m: CarModel): string[] {
  const g = m.shell;
  const p = g.getAttribute('position'); const c = g.getAttribute('color'); const a = g.getAttribute('aPaint'); const t = g.getAttribute('aMat');
  const q = (v: number) => Math.round(v * 255);
  const out: string[] = [];
  for (let i = 0; i < p.count; i += 3) {
    const cx = (p.getX(i) + p.getX(i + 1) + p.getX(i + 2)) / 3;
    const cy = (p.getY(i) + p.getY(i + 1) + p.getY(i + 2)) / 3;
    const cz = (p.getZ(i) + p.getZ(i + 1) + p.getZ(i + 2)) / 3;
    out.push(`${cx.toFixed(4)},${cy.toFixed(4)},${cz.toFixed(4)}|${q(c.getX(i))},${q(c.getY(i))},${q(c.getZ(i))}|${q(a.getX(i))},${q(a.getY(i))},${q(a.getZ(i))},${q(a.getW(i))}|${q(t.getX(i))},${q(t.getY(i))},${q(t.getZ(i))},${q(t.getW(i))}`);
  }
  return out.sort();
}

describe('carro em glTF: o validador da convenção', () => {
  it('os 13 carros do jogo passam', () => {
    for (const body of CAR_BODIES) expect(checkCarModel(MODEL_BUILDERS[body]()), body).toEqual([]);
  });

  it('acusa carro grande demais, lanterna faltando e triângulos demais', () => {
    const big = MODEL_BUILDERS.gt();
    big.shell = big.shell.clone().scale(1.3, 1.3, 1.3);
    big.shell.computeBoundingBox();
    const problems = checkCarModel(big).join(' | ');
    expect(problems).toMatch(/comprimento/);
    expect(problems).toMatch(/largura/);

    const dark = MODEL_BUILDERS.sedan();
    dark.shell = dark.shell.clone();
    const mat = dark.shell.getAttribute('aMat');
    for (let i = 0; i < mat.count; i++) mat.setW(i, 0);
    expect(checkCarModel(dark).join(' | ')).toMatch(/lanterna/);

    const heavy = { ...MODEL_BUILDERS.micro(), triangles: 2600 };
    expect(checkCarModel(heavy).join(' | ')).toMatch(/2\.600 triângulos/);
  });
});

describe('carro em glTF: ida e volta pelo modelo-base', () => {
  it.each(CAR_BODIES.map((b) => [b] as const))('%s: o .glb do modelo-base carrega igual ao carro do jogo', async (body: CarBody) => {
    const game = MODEL_BUILDERS[body]();
    const glb = await exportGlb(carTemplateScene(body));
    const { model, problems } = await parseCarGlb(body, glb);
    expect(problems).toEqual([]);
    if (!model) throw new Error('sem modelo');
    expect(model.triangles).toBe(game.triangles);
    expect(model.wheel).toBe(game.wheel);
    expect(model.liveries).toEqual(game.liveries);
    expect(model.height).toBeCloseTo(game.height, 4);
    for (let k = 0; k < 2; k++) for (const f of ['z', 'r', 'x', 'w'] as const) expect(model.axles[k][f], `eixo ${k} ${f}`).toBeCloseTo(game.axles[k][f], 3);
    // Os escapes vêm nomeados por lado (exhaust_l, exhaust_r): compara da esquerda para a direita.
    const byX = (l: readonly (readonly number[])[]) => [...l].sort((p, q) => p[0] - q[0]);
    expect(model.exhausts.length).toBe(game.exhausts.length);
    const want = byX(game.exhausts);
    byX(model.exhausts).forEach((e, i) => e.forEach((v, j) => expect(v, `escape ${i}`).toBeCloseTo(want[i][j], 3)));
    expect(faces(model)).toEqual(faces(game));
  }, 30_000);
});

describe('carro em glTF: o modelo-base depois do Blender (sem os extras do jogo)', () => {
  // O Blender, no padrão, não exporta as propriedades próprias (extras): o jogo tem de entender o
  // modelo-base só pelos nomes, cores e brilho dos materiais — tom dos painéis, luzes fracas, faróis
  // escamoteáveis.
  it.each(CAR_BODIES.map((b) => [b] as const))('%s: sem extras, carrega igual ao carro do jogo', async (body: CarBody) => {
    const scene = carTemplateScene(body);
    scene.traverse((o) => {
      const mesh = o as THREE.Mesh;
      if (mesh.isMesh) for (const m of Array.isArray(mesh.material) ? mesh.material : [mesh.material]) m.userData = {};
    });
    const { model, problems } = await parseCarGlb(body, await exportGlb(scene));
    expect(problems).toEqual([]);
    if (!model) throw new Error('sem modelo');
    expect(faces(model)).toEqual(faces(MODEL_BUILDERS[body]()));
  }, 30_000);
});

describe('carro em glTF: o que o artista entrega (materiais só pelo nome)', () => {
  /** Carro de caixas na convenção do glTF: frente em +Z, esquerda em +X, metros. */
  function boxCar(withWheels = true): THREE.Group {
    const root = new THREE.Group();
    const body = new THREE.Group(); body.name = 'body'; root.add(body);
    const part = (name: string, color: string, w: number, h: number, d: number, x: number, y: number, z: number) => {
      const mat = new THREE.MeshStandardMaterial({ color, roughness: 0.5, metalness: 0 });
      mat.name = name;
      const m = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), mat);
      m.position.set(x, y, z);
      body.add(m);
    };
    part('paint', '#ff0000', 1.86, 0.9, 4.3, 0, 0.75, 0);
    part('headlight.001', '#ffffff', 0.3, 0.12, 0.05, 0.6, 0.8, 2.17);
    part('taillight', '#ff2000', 0.3, 0.12, 0.05, -0.6, 0.8, -2.17);
    part('interior_seat', '#123456', 0.5, 0.4, 0.5, 0.3, 1.1, 0);
    if (withWheels) {
      for (const [n, x, z] of [['wheel_fl', 0.8, 1.4], ['wheel_fr', -0.8, 1.4], ['wheel_rl', 0.8, -1.4], ['wheel_rr', -0.8, -1.4]] as const) {
        const w = new THREE.Mesh(new THREE.CylinderGeometry(0.33, 0.33, 0.24, 12).rotateZ(Math.PI / 2), new THREE.MeshStandardMaterial({ name: 'tire' }));
        w.name = n; w.position.set(x, 0.33, z);
        root.add(w);
      }
      const ex = new THREE.Object3D(); ex.name = 'exhaust_l'; ex.position.set(0.4, 0.35, -2.2); root.add(ex);
    }
    root.updateMatrixWorld(true);
    return root;
  }

  it('paint recebe a cor do carro, headlight acende na frente, taillight atrás, nome desconhecido fica com a cor dele', () => {
    const { model, problems } = carModelFromGltf('sedan', boxCar());
    expect(problems).toEqual([]);
    if (!model) throw new Error('sem modelo');
    const g = model.shell;
    const p = g.getAttribute('position'); const c = g.getAttribute('color'); const a = g.getAttribute('aPaint'); const t = g.getAttribute('aMat');
    let paint = 0; let heads = 0; let tails = 0; let seat = 0;
    const seatRgb = new THREE.Color('#123456');
    for (let i = 0; i < p.count; i++) {
      if (a.getX(i) > 0.99) { paint++; expect(c.getX(i)).toBeCloseTo(1, 2); } // tom branco: a cor é a da instância
      if (t.getZ(i) > 0.99) { heads++; expect(p.getZ(i), 'farol na frente (−z no jogo)').toBeLessThan(-2); }
      if (t.getW(i) > 0.99) { tails++; expect(p.getZ(i), 'lanterna atrás (+z no jogo)').toBeGreaterThan(2); }
      if (Math.abs(c.getX(i) - seatRgb.r) < 0.01 && Math.abs(c.getZ(i) - seatRgb.b) < 0.01) { seat++; expect(a.getX(i)).toBe(0); }
    }
    expect(paint).toBeGreaterThan(0); expect(heads).toBeGreaterThan(0); expect(tails).toBeGreaterThan(0); expect(seat).toBeGreaterThan(0);
    // Rodas: posição e tamanho saem das malhas wheel_*; o eixo da frente fica em −z no jogo.
    expect(model.axles[0].z).toBeCloseTo(-1.4, 3);
    expect(model.axles[1].z).toBeCloseTo(1.4, 3);
    expect(model.axles[0].r).toBeCloseTo(0.33, 3);
    expect(model.axles[0].x).toBeCloseTo(0.8, 3);
    expect(model.axles[0].w).toBeCloseTo(0.24, 3);
    expect(model.exhausts.length).toBe(1);
    expect(model.exhausts[0][2]).toBeCloseTo(2.2, 3);
  });

  it('sem as quatro rodas não carrega e diz quais faltam', () => {
    const { model, problems } = carModelFromGltf('sedan', boxCar(false));
    expect(model).toBeNull();
    expect(problems.join(' | ')).toMatch(/wheel_fl.*wheel_fr.*wheel_rl.*wheel_rr/);
  });
});

describe('carro em glTF: os arquivos da arte no jogo (assets.ts)', () => {
  const dataUrl = (b: ArrayBuffer) => `data:model/gltf-binary;base64,${Buffer.from(b).toString('base64')}`;

  it('o .glb válido substitui o procedural; nome que não é estilo e arquivo quebrado ficam de fora, com o motivo', async () => {
    const { loadCarAssets } = await import('../src/render/cars/assets');
    const { buildModel, modelSource, setModelOverride } = await import('../src/render/cars/models');
    const gt = dataUrl(await exportGlb(carTemplateScene('gt')));
    const report = await loadCarAssets({
      '../../assets/cars/gt.glb': async () => gt,
      '../../assets/cars/fusca.glb': async () => gt,
      '../../assets/cars/sedan.glb': async () => 'data:model/gltf-binary;base64,AAAA',
    });
    try {
      expect(report.loaded).toEqual(['gt']);
      expect(modelSource('gt')).toBe('glb');
      expect(modelSource('sedan')).toBe('procedural');
      expect(buildModel('gt').triangles).toBe(MODEL_BUILDERS.gt().triangles);
      const rejected = Object.fromEntries(report.rejected.map((r) => [r.file, r.problems.join(' | ')]));
      expect(rejected['fusca.glb']).toMatch(/não é um estilo/);
      expect(rejected['sedan.glb']).toMatch(/não abre como glTF/);
    } finally {
      setModelOverride('gt', null);
    }
  }, 30_000);
});
