// Marcos turísticos em glTF (docs/ARTE.md, "Marcos baixados"): o validador da convenção (types.ts), a ida e volta
// (o marco procedural exportado em .glb e carregado de volta sai igual), o arquivo da arte substituindo o
// procedural (src/assets/landmarks/<id>.glb) e as partes puras do conversor de modelos baixados
// (scripts/lib/landmark-convert.ts): paleta, cor por face a partir da textura e redução de triângulos.
// Tudo no Node, sem DOM.
import * as THREE from 'three';
import { beforeAll, describe, expect, it } from 'vitest';
import { getTrack } from '../src/core/track';
import { exportGlb } from '../src/render/cars/template';
import { getModel, LANDMARK_PREFIX, landmarkSource, modelBounds, setLandmarkOverride } from '../src/render/scenery/catalog';
import type { Model } from '../src/render/scenery/geom';
import { LANDMARKS } from '../src/render/scenery/landmarks';
import { checkLandmarkModel, LANDMARK_LIMITS } from '../src/render/scenery/landmarks/check';
import { landmarkModelFromGltf, parseLandmarkGlb } from '../src/render/scenery/landmarks/gltf';
import { landmarkScene } from '../src/render/scenery/landmarks/template';
import { sceneryLayout } from '../src/render/scenery/layout';
import { modelTriangles } from '../src/render/scenery/vegetation';
import { installNodeFileReader } from '../scripts/lib/node-filereader';
import {
  convertLandmark, decimate, gatherTriangles, quantize, TARGET_TRIS, type Pixels,
} from '../scripts/lib/landmark-convert';
import { SYNTH, syntheticLandmark } from '../scripts/lib/synthetic-landmark';

beforeAll(installNodeFileReader);

const id = (k: string) => LANDMARK_PREFIX + k;

/** Um texto por triângulo (centro + cor e uv dos 3 vértices), em ordem, por material: compara sem depender da ordem. */
function faces(m: Model): Record<string, string[]> {
  const out: Record<string, string[]> = {};
  for (const part of m.parts) {
    const g = part.geometry;
    const p = g.getAttribute('position'); const c = g.getAttribute('color'); const uv = g.getAttribute('uv');
    const list = out[part.mat] ?? (out[part.mat] = []);
    for (let i = 0; i < p.count; i += 3) {
      const cx = (p.getX(i) + p.getX(i + 1) + p.getX(i + 2)) / 3;
      const cy = (p.getY(i) + p.getY(i + 1) + p.getY(i + 2)) / 3;
      const cz = (p.getZ(i) + p.getZ(i + 1) + p.getZ(i + 2)) / 3;
      let s = `${cx.toFixed(4)},${cy.toFixed(4)},${cz.toFixed(4)}`;
      for (let k = 0; k < 3; k++) {
        if (c) s += `|${c.getX(i + k).toFixed(5)},${c.getY(i + k).toFixed(5)},${c.getZ(i + k).toFixed(5)}`;
        if (uv) s += `|${uv.getX(i + k).toFixed(4)},${uv.getY(i + k).toFixed(4)}`;
      }
      list.push(s);
    }
  }
  for (const k of Object.keys(out)) out[k].sort();
  return out;
}

function boundsOf(m: Model): THREE.Box3 {
  const b = new THREE.Box3();
  for (const p of m.parts) { p.geometry.computeBoundingBox(); if (p.geometry.boundingBox) b.union(p.geometry.boundingBox); }
  return b;
}

/** Caixa lisa pintada (não indexada, com cor), base em y = 0, centro na origem. */
function boxModel(w: number, h: number, d: number, color = '#cc8844'): Model {
  const g = new THREE.BoxGeometry(w, h, d).toNonIndexed().translate(0, h / 2, 0);
  g.deleteAttribute('uv');
  const c = new THREE.Color(color);
  g.setAttribute('color', new THREE.Float32BufferAttribute(Array.from({ length: g.attributes.position.count }, () => [c.r, c.g, c.b]).flat(), 3));
  return { parts: [{ geometry: g, mat: 'flat' }], blob: 0 };
}

describe('marco em glTF: o validador da convenção (types.ts)', () => {
  it('os marcos procedurais do jogo passam', () => {
    const bad: string[] = [];
    for (const [k, def] of Object.entries(LANDMARKS)) {
      const p = checkLandmarkModel(getModel(id(k)), def.place);
      if (p.length) bad.push(`${k}: ${p.join('; ')}`);
    }
    expect(bad).toEqual([]);
  });

  it('acusa escala errada (pequeno demais, em centímetros), triângulos demais e cor faltando', () => {
    // Saído do Meshy sem escala: ~2 m de altura, um marco "perto" pede ≥ 6 m.
    expect(checkLandmarkModel(boxModel(1, 2, 1), 'near').join(' | ')).toMatch(/altura 2,0 m.*mínimo 6/);
    // Em centímetros: um ninho de 24 m vira 2,4 km.
    expect(checkLandmarkModel(boxModel(1800, 2400, 3300), 'near').join(' | ')).toMatch(/altura 2\.400,0 m.*escala/);
    // Triângulos: 300 caixas = 3.600 > 3.500 do "perto".
    const many = boxModel(10, 10, 10);
    const parts: THREE.BufferGeometry[] = [];
    for (let i = 0; i < 300; i++) parts.push(many.parts[0].geometry.clone().translate((i % 20) * 11, 0, Math.floor(i / 20) * 11));
    const heavy = new THREE.BufferGeometry();
    for (const name of ['position', 'normal', 'color']) {
      const arrs = parts.map((g) => g.getAttribute(name).array as Float32Array);
      const all = new Float32Array(arrs.reduce((n, a) => n + a.length, 0));
      let o = 0; for (const a of arrs) { all.set(a, o); o += a.length; }
      heavy.setAttribute(name, new THREE.BufferAttribute(all, 3));
    }
    heavy.center(); heavy.computeBoundingBox(); heavy.translate(0, -(heavy.boundingBox?.min.y ?? 0), 0);
    expect(checkLandmarkModel({ parts: [{ geometry: heavy, mat: 'flat' }] }, 'near').join(' | ')).toMatch(/3\.600 triângulos.*3\.500/);
    // Sem cor por vértice.
    const grey = boxModel(10, 10, 10);
    grey.parts[0].geometry.deleteAttribute('color');
    expect(checkLandmarkModel(grey, 'near').join(' | ')).toMatch(/cor por vértice/);
  });

  it('acusa base fora do chão, origem fora da pegada, malha indexada, material desconhecido e partes demais', () => {
    const floating = boxModel(10, 10, 10); floating.parts[0].geometry.translate(0, 3, 0);
    expect(checkLandmarkModel(floating, 'near').join(' | ')).toMatch(/base.*chão/);
    const aside = boxModel(10, 10, 10); aside.parts[0].geometry.translate(40, 0, 0);
    expect(checkLandmarkModel(aside, 'near').join(' | ')).toMatch(/origem/);
    const indexed: Model = { parts: [{ geometry: new THREE.BoxGeometry(10, 10, 10).translate(0, 5, 0), mat: 'flat' }] };
    expect(checkLandmarkModel(indexed, 'near').join(' | ')).toMatch(/indexada/);
    const odd = boxModel(10, 10, 10); (odd.parts[0] as { mat: string }).mat = 'panel';
    expect(checkLandmarkModel(odd, 'near').join(' | ')).toMatch(/material "panel"/);
    const five: Model = { parts: [0, 1, 2, 3, 4].map(() => boxModel(10, 10, 10).parts[0]) };
    expect(checkLandmarkModel(five, 'near').join(' | ')).toMatch(/5 partes/);
  });

  it('limites por lugar são os do teste dos marcos (orçamento e altura mínima)', () => {
    expect(LANDMARK_LIMITS.budget).toEqual({ near: 3500, far: 5000, skyline: 2500 });
    expect(LANDMARK_LIMITS.minHeight).toEqual({ near: 6, far: 25, skyline: 90 });
  });
});

describe('marco em glTF: ida e volta', () => {
  it('todo marco procedural exportado em .glb volta com as mesmas faces, cores e caixa', async () => {
    const bad: string[] = [];
    for (const [k, def] of Object.entries(LANDMARKS)) {
      const game = getModel(id(k));
      const glb = await exportGlb(landmarkScene(game, k));
      const { model, problems } = await parseLandmarkGlb(def.place, glb);
      if (!model) { bad.push(`${k}: sem modelo (${problems.join('; ')})`); continue; }
      if (problems.length) bad.push(`${k}: ${problems.join('; ')}`);
      expect(model.parts.map((p) => p.mat), k).toEqual(game.parts.map((p) => p.mat));
      expect(faces(model), k).toEqual(faces(game));
      const a = boundsOf(model); const b = boundsOf(game);
      expect(a.min.distanceTo(b.min) + a.max.distanceTo(b.max), k).toBeLessThan(1e-4);
      for (const p of model.parts) expect(p.geometry.index, k).toBeNull();
      expect(model.blob ?? 0).toBe(0);
    }
    expect(bad).toEqual([]);
  }, 180_000);

  it('o material vem pelo nome (glow, beacon, fachadas; ".001" do Blender), o resto é liso; textura avisa', () => {
    const root = new THREE.Group();
    const add = (name: string, color = '#ffffff', map = false) => {
      const g = boxModel(4, 8, 4, color).parts[0].geometry;
      if (name.startsWith('office')) g.setAttribute('uv', new THREE.Float32BufferAttribute(new Float32Array(g.attributes.position.count * 2), 2));
      const mat = new THREE.MeshStandardMaterial({ name, vertexColors: true });
      if (map) mat.map = new THREE.Texture();
      root.add(new THREE.Mesh(g, mat));
    };
    add('Glow.001'); add('beacon'); add('office'); add('pedra', '#ffffff', true); add('Material_0');
    root.updateMatrixWorld(true);
    const { model, problems, warnings } = landmarkModelFromGltf(root, 'near');
    expect(problems).toEqual([]);
    expect(model?.parts.map((p) => p.mat)).toEqual(['glow', 'beacon', 'office', 'flat']);
    expect(warnings.join(' | ')).toMatch(/textura ignorada.*pedra/);
  });

  it('a cor do material multiplica a do vértice (glTF: baseColorFactor × COLOR_0); sem COLOR_0 é recusado', () => {
    const root = new THREE.Group();
    root.add(new THREE.Mesh(boxModel(4, 8, 4, '#ffffff').parts[0].geometry, new THREE.MeshStandardMaterial({ name: 'flat', color: new THREE.Color(0.5, 0.25, 1), vertexColors: true })));
    root.updateMatrixWorld(true);
    const { model } = landmarkModelFromGltf(root, 'near');
    const c = model?.parts[0].geometry.getAttribute('color');
    expect([c?.getX(0), c?.getY(0), c?.getZ(0)]).toEqual([0.5, 0.25, 1]);

    const bare = new THREE.Group();
    const g = boxModel(4, 8, 4).parts[0].geometry; g.deleteAttribute('color');
    bare.add(new THREE.Mesh(g, new THREE.MeshStandardMaterial({ name: 'flat' })));
    bare.updateMatrixWorld(true);
    const r = landmarkModelFromGltf(bare, 'near');
    expect(r.model).toBeNull();
    expect(r.problems.join(' | ')).toMatch(/cor por vértice/);
  });

  it('a transformação dos nós entra na malha (escala, giro, posição), e o espelho não vira a face para dentro', () => {
    const root = new THREE.Group();
    const node = new THREE.Group(); node.scale.set(-2, 2, 2); node.position.set(0, 0, 0); root.add(node);
    const g = boxModel(4, 4, 4).parts[0].geometry;
    node.add(new THREE.Mesh(g, new THREE.MeshStandardMaterial({ name: 'flat', vertexColors: true })));
    root.updateMatrixWorld(true);
    const { model } = landmarkModelFromGltf(root, 'near');
    if (!model) throw new Error('sem modelo');
    const b = boundsOf(model);
    expect(b.max.y).toBeCloseTo(8, 5);
    expect(b.max.x - b.min.x).toBeCloseTo(8, 5);
    // A normal da face (pela ordem dos vértices) aponta para fora da caixa.
    const p = model.parts[0].geometry.getAttribute('position');
    const a = new THREE.Vector3(); const bb = new THREE.Vector3(); const c = new THREE.Vector3(); const ctr = new THREE.Vector3(0, 4, 0);
    for (let i = 0; i < p.count; i += 3) {
      a.fromBufferAttribute(p, i); bb.fromBufferAttribute(p, i + 1); c.fromBufferAttribute(p, i + 2);
      const n = bb.clone().sub(a).cross(c.clone().sub(a));
      const m = a.clone().add(bb).add(c).divideScalar(3).sub(ctr);
      expect(n.dot(m)).toBeGreaterThan(0);
    }
  });
});

describe('marco em glTF: o arquivo da arte no jogo (landmarks/assets.ts)', () => {
  const dataUrl = (b: ArrayBuffer) => `data:model/gltf-binary;base64,${Buffer.from(b).toString('base64')}`;

  it('o .glb válido substitui o procedural (caixa e layout seguem); id que não é marco, arquivo quebrado e fora da escala ficam de fora', async () => {
    const { loadLandmarkAssets } = await import('../src/render/scenery/landmarks/assets');
    const procBounds = { ...modelBounds(id('tuiuiu_ninho')) };
    const procTris = modelTriangles(getModel(id('tuiuiu_ninho')));
    const replacement = boxModel(12, 18, 30, '#e8e0d0');
    const good = dataUrl(await exportGlb(landmarkScene(replacement, 'tuiuiu_ninho')));
    const tiny = dataUrl(await exportGlb(landmarkScene(boxModel(1, 2, 1), 'buriti')));
    const report = await loadLandmarkAssets({
      '../../../assets/landmarks/tuiuiu_ninho.glb': async () => good,
      '../../../assets/landmarks/nao_existe.glb': async () => good,
      '../../../assets/landmarks/masp.glb': async () => 'data:model/gltf-binary;base64,AAAA',
      '../../../assets/landmarks/buriti.glb': async () => tiny,
    });
    try {
      expect(report.loaded).toEqual(['tuiuiu_ninho']);
      expect(landmarkSource('tuiuiu_ninho')).toBe('glb');
      expect(landmarkSource('buriti')).toBe('procedural');
      expect(landmarkSource('masp')).toBe('procedural');
      const rejected = Object.fromEntries(report.rejected.map((r) => [r.file, r.problems.join(' | ')]));
      expect(rejected['nao_existe.glb']).toMatch(/não é um marco/);
      expect(rejected['masp.glb']).toMatch(/não abre como glTF/);
      expect(rejected['buriti.glb']).toMatch(/altura/);
      // O modelo e a caixa são os do arquivo; o registro (lugar, lado, vezes por volta) continua o do LANDMARKS.
      expect(modelTriangles(getModel(id('tuiuiu_ninho')))).toBe(12);
      const b = modelBounds(id('tuiuiu_ninho'));
      expect(b.maxY).toBeCloseTo(18, 4);
      expect(b.maxZ - b.minZ).toBeCloseTo(30, 4);
      const placed = sceneryLayout(getTrack('transpantaneira'));
      const n = placed.bySeg.flat().filter((p) => placed.models[p.model] === id('tuiuiu_ninho')).length;
      expect(n).toBe(LANDMARKS.tuiuiu_ninho.perLap);
    } finally {
      setLandmarkOverride('tuiuiu_ninho', null);
    }
    // De volta ao procedural, com a caixa dele (os caches do catálogo esquecem o arquivo).
    expect(landmarkSource('tuiuiu_ninho')).toBe('procedural');
    expect(modelTriangles(getModel(id('tuiuiu_ninho')))).toBe(procTris);
    expect(modelBounds(id('tuiuiu_ninho'))).toEqual(procBounds);
  }, 60_000);
});

// ───────────────────────── Conversor (scripts/lib/landmark-convert.ts): as partes puras ─────────────────────────

/** Textura sintética em memória: `w` × `h`, linha 0 = v 0 (como o glTF, flipY = false). */
function pixels(w: number, h: number, at: (x: number, y: number) => [number, number, number]): Pixels {
  const data = new Uint8Array(w * h * 4);
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    const [r, g, b] = at(x, y);
    data.set([r, g, b, 255], (y * w + x) * 4);
  }
  return { width: w, height: h, data };
}
const srgbByte = (v: number) => new THREE.Color().setRGB(v / 255, 0, 0, THREE.SRGBColorSpace).r; // byte sRGB → linear

describe('conversor: paleta (k-means)', () => {
  it('três nuvens de cor viram três cores, cada amostra na sua nuvem; é determinístico', () => {
    const centres = [[0.8, 0.1, 0.1], [0.1, 0.6, 0.2], [0.9, 0.9, 0.85]];
    const n = 900;
    const colors = new Float32Array(n * 3); const weights = new Float32Array(n).fill(1);
    let s = 7;
    const rnd = () => { s = (s * 1103515245 + 12345) % 2147483648; return s / 2147483648; };
    for (let i = 0; i < n; i++) for (let k = 0; k < 3; k++) colors[i * 3 + k] = Math.min(1, Math.max(0, centres[i % 3][k] + (rnd() - 0.5) * 0.08));
    const q = quantize(colors, weights, 3);
    expect(q.palette.length / 3).toBe(3);
    for (let i = 0; i < n; i++) {
      // Todas as amostras da mesma nuvem caem no mesmo índice.
      expect(q.index[i]).toBe(q.index[i % 3]);
    }
    for (const c of centres) {
      let best = Infinity;
      for (let j = 0; j < 3; j++) best = Math.min(best, Math.hypot(q.palette[j * 3] - c[0], q.palette[j * 3 + 1] - c[1], q.palette[j * 3 + 2] - c[2]));
      expect(best).toBeLessThan(0.05);
    }
    expect(quantize(colors, weights, 3)).toEqual(q);
  });

  it('pede mais cores do que existem: a paleta não inventa (2 cores distintas → 2)', () => {
    const colors = new Float32Array([1, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 1]);
    const q = quantize(colors, new Float32Array(4).fill(1), 12);
    expect(q.palette.length / 3).toBe(2);
  });

  // Defeito visto na prévia do conversor (modelo sintético): com 12 cores, o k-means partia o corpo branco (a maior
  // área, com o ruído e o sombreado da foto) em 4 brancos quase iguais — a face vizinha mudava de tom e o corpo saía
  // manchado, não chapado. Tons a menos de --merge (sRGB, padrão 0,12) viram um só.
  it('tons quase iguais viram uma cor só (--merge); cor distinta, mesmo pequena, fica', () => {
    const n = 400;
    const colors = new Float32Array(n * 3); const weights = new Float32Array(n).fill(1);
    for (let i = 0; i < n; i++) {
      const c = i < 10 ? [0.8, 0.05, 0.05] : [[0.80, 0.78, 0.72], [0.86, 0.84, 0.78], [0.92, 0.90, 0.84], [0.76, 0.74, 0.68]][i % 4];
      colors.set(c, i * 3);
    }
    expect(quantize(colors, weights, 12).palette.length / 3).toBe(2);
    expect(quantize(colors, weights, 12, { merge: 0 }).palette.length / 3).toBe(5);
  });

  it('paleta fixa (--palette): toda cor vai para a mais próxima da lista', () => {
    const colors = new Float32Array([0.9, 0.1, 0.1, 0.1, 0.1, 0.8, 0.85, 0.2, 0.15]);
    const fixed = ['#ff0000', '#0000ff'].map((h) => new THREE.Color(h));
    const q = quantize(colors, new Float32Array(3).fill(1), 12, { fixed });
    expect(Array.from(q.palette)).toEqual([1, 0, 0, 0, 0, 1]);
    expect(Array.from(q.index)).toEqual([0, 1, 0]);
  });
});

describe('conversor: cor por face a partir da textura', () => {
  /** Quadrado de 2 × 1 m no plano XY com uv de 0 a 1; a metade da esquerda em u < 0,5. */
  function quad(material: THREE.Material, flipY = false): THREE.Group {
    const g = new THREE.PlaneGeometry(2, 1, 8, 2); // 32 triângulos
    const mesh = new THREE.Mesh(g, material);
    const root = new THREE.Group(); root.add(mesh);
    if ((material as THREE.MeshStandardMaterial).map) (material as THREE.MeshStandardMaterial).map!.flipY = flipY;
    root.updateMatrixWorld(true);
    return root;
  }
  // Esquerda (x < 2) vermelha, direita azul; linha de cima (y = 0) clara, a de baixo escura: dá para ver o flipY.
  const tex = pixels(4, 2, (x, y) => (x < 2 ? (y === 0 ? [255, 0, 0] : [128, 0, 0]) : [0, 0, 255]));
  const read = (t: THREE.Texture) => (t.userData.px as Pixels) ?? null;
  const map = () => { const t = new THREE.Texture(); t.userData.px = tex; t.colorSpace = THREE.SRGBColorSpace; return t; };

  it('cada triângulo recebe a cor da textura sob ele (sRGB → linear), vezes a cor do material', () => {
    const soup = gatherTriangles(quad(new THREE.MeshStandardMaterial({ map: map(), color: new THREE.Color(1, 1, 1) })), read);
    expect(soup.count).toBe(32);
    for (let t = 0; t < soup.count; t++) {
      const cx = (soup.pos[t * 9] + soup.pos[t * 9 + 3] + soup.pos[t * 9 + 6]) / 3;
      const r = soup.color[t * 3]; const b = soup.color[t * 3 + 2];
      if (cx < -0.15) { expect(r, `tri ${t}`).toBeGreaterThan(0.2); expect(b).toBeLessThan(0.01); }
      if (cx > 0.15) { expect(b, `tri ${t}`).toBeCloseTo(1, 3); expect(r).toBeLessThan(0.01); }
    }
    const half = gatherTriangles(quad(new THREE.MeshStandardMaterial({ map: map(), color: new THREE.Color(0.5, 0.5, 0.5) })), read);
    const right = Array.from({ length: 32 }, (_, t) => t).filter((t) => (half.pos[t * 9] + half.pos[t * 9 + 3] + half.pos[t * 9 + 6]) / 3 > 0.15);
    for (const t of right) expect(half.color[t * 3 + 2]).toBeCloseTo(0.5, 3);
  });

  it('flipY: com flipY = false a linha 0 é v = 0 (glTF); com true, a linha 0 é v = 1', () => {
    // PlaneGeometry: v = 1 em cima (y = +0,5). Sem flipY a linha 0 (vermelho claro) fica em baixo.
    const lo = (flip: boolean) => {
      const soup = gatherTriangles(quad(new THREE.MeshStandardMaterial({ map: map() }), flip), read);
      let top = 0; let bottom = 0;
      for (let t = 0; t < soup.count; t++) {
        const cx = (soup.pos[t * 9] + soup.pos[t * 9 + 3] + soup.pos[t * 9 + 6]) / 3;
        const cy = (soup.pos[t * 9 + 1] + soup.pos[t * 9 + 4] + soup.pos[t * 9 + 7]) / 3;
        if (cx > -0.3) continue;
        if (cy > 0) top = Math.max(top, soup.color[t * 3]); else bottom = Math.max(bottom, soup.color[t * 3]);
      }
      return { top, bottom };
    };
    const a = lo(false); const b = lo(true);
    expect(a.bottom).toBeCloseTo(1, 2); expect(a.top).toBeCloseTo(srgbByte(128), 2);
    expect(b.top).toBeCloseTo(1, 2); expect(b.bottom).toBeCloseTo(srgbByte(128), 2);
  });

  it('sem textura: a cor do material e a do vértice; material emissivo marca a face como luz', () => {
    const g = new THREE.PlaneGeometry(1, 1).toNonIndexed();
    g.setAttribute('color', new THREE.Float32BufferAttribute(new Array(6).fill([0.5, 1, 1]).flat(), 3));
    const lamp = new THREE.MeshStandardMaterial({ color: new THREE.Color(0.8, 0.6, 0.2), emissive: new THREE.Color(1, 0.8, 0.3), vertexColors: true });
    const root = new THREE.Group(); root.add(new THREE.Mesh(g, lamp)); root.updateMatrixWorld(true);
    const soup = gatherTriangles(root, read);
    expect(soup.count).toBe(2);
    expect(soup.color[0]).toBeCloseTo(0.4, 5); expect(soup.color[1]).toBeCloseTo(0.6, 5);
    expect(Array.from(soup.glow)).toEqual([1, 1]);
    // Por nome (--glow-material) também: um material "janela" vira luz.
    const named = new THREE.Group(); named.add(new THREE.Mesh(new THREE.PlaneGeometry(1, 1), new THREE.MeshStandardMaterial({ name: 'Janela_acesa' }))); named.updateMatrixWorld(true);
    expect(Array.from(gatherTriangles(named, read, { glowMaterials: /janela/i }).glow)).toEqual([1, 1]);
    expect(Array.from(gatherTriangles(named, read).glow)).toEqual([0, 0]);
  });
});

describe('conversor: redução de triângulos', () => {
  it('esfera de 40 mil triângulos → até o alvo, com a caixa praticamente igual', async () => {
    const g = new THREE.SphereGeometry(5, 200, 100).toNonIndexed();
    const pos = new Float32Array(g.getAttribute('position').array);
    expect(pos.length / 9).toBeGreaterThan(39_000);
    for (const target of [2000, 500]) {
      const out = await decimate(pos, target);
      const tris = out.pos.length / 9;
      expect(tris).toBeLessThanOrEqual(target);
      expect(tris).toBeGreaterThan(target * 0.6);
      const b = new THREE.Box3().setFromBufferAttribute(new THREE.BufferAttribute(out.pos, 3));
      for (const v of [b.min.x, b.min.y, b.min.z]) expect(v).toBeLessThan(-4.8);
      for (const v of [b.max.x, b.max.y, b.max.z]) expect(v).toBeGreaterThan(4.8);
    }
  });

  it('malha já pequena fica como está', async () => {
    const pos = new Float32Array(new THREE.BoxGeometry(1, 1, 1).toNonIndexed().getAttribute('position').array);
    const out = await decimate(pos, 2500);
    expect(out.pos.length / 9).toBe(12);
    expect(out.method).toBe('nenhuma');
  });
});

describe('conversor: de ponta a ponta no modelo sintético (alta resolução, textura com ruído)', () => {
  it('sai na convenção: alvo de triângulos, paleta curta, cor por face, base em 0, centrado, frente em +X, passa no validador', async () => {
    const root = syntheticLandmark(1);
    const read = (t: THREE.Texture) => (t.image && 'data' in (t.image as object) ? (t.image as Pixels) : null);
    const before = gatherTriangles(root, read);
    expect(before.count).toBeGreaterThanOrEqual(30_000);
    const { model, report } = await convertLandmark(root, read, { place: 'near', height: 20, colors: 10 });
    const tris = modelTriangles(model);
    expect(report.trianglesIn).toBe(before.count);
    expect(report.trianglesOut).toBe(tris);
    expect(tris).toBeLessThanOrEqual(TARGET_TRIS.near);
    expect(tris).toBeGreaterThan(TARGET_TRIS.near * 0.6);
    expect(checkLandmarkModel(model, 'near')).toEqual([]);
    expect(report.problems).toEqual([]);
    // Partes: lisa e luz (o lampião emissivo do pedestal).
    expect(model.parts.map((p) => p.mat)).toEqual(['flat', 'glow']);
    // Cor por face: os 3 vértices de cada triângulo com a mesma cor, todas da paleta (≤ 10).
    const used = new Set<string>();
    for (const p of model.parts) {
      const c = p.geometry.getAttribute('color');
      expect(p.geometry.index).toBeNull();
      expect(p.geometry.getAttribute('uv')).toBeUndefined();
      for (let i = 0; i < c.count; i += 3) {
        const k = `${c.getX(i)},${c.getY(i)},${c.getZ(i)}`;
        expect(`${c.getX(i + 1)},${c.getY(i + 1)},${c.getZ(i + 1)}`).toBe(k);
        expect(`${c.getX(i + 2)},${c.getY(i + 2)},${c.getZ(i + 2)}`).toBe(k);
        used.add(k);
      }
    }
    expect(used.size).toBeLessThanOrEqual(10);
    expect(report.palette.length).toBe(used.size);
    // Medidas: altura pedida, base em y = 0, pegada centrada na origem.
    const b = boundsOf(model);
    expect(b.max.y - b.min.y).toBeCloseTo(20, 1);
    expect(b.min.y).toBeCloseTo(0, 4);
    expect(Math.abs(b.max.x + b.min.x)).toBeLessThan(0.01);
    expect(Math.abs(b.max.z + b.min.z)).toBeLessThan(0.01);
    // Frente: o bico laranja (em +Z no arquivo, a frente do glTF) vem para +X; e é laranja.
    const flat = model.parts[0].geometry;
    const p = flat.getAttribute('position'); const c = flat.getAttribute('color');
    let tip = -1; let best = -Infinity;
    for (let i = 0; i < p.count; i++) if (p.getX(i) > best) { best = p.getX(i); tip = i; }
    const beak = new THREE.Color(SYNTH.beak);
    expect(Math.hypot(c.getX(tip) - beak.r, c.getY(tip) - beak.g, c.getZ(tip) - beak.b)).toBeLessThan(0.25);
    // O corpo é branco: a cor de maior área é clara (o ruído da textura não vira tom sujo).
    const top = report.palette[0];
    expect(top.share).toBeGreaterThan(0.2);
    expect(new THREE.Color(top.hex).getHSL({ h: 0, s: 0, l: 0 }).l).toBeGreaterThan(0.6);
  }, 120_000);

  it('--glow <cor>: as faces dessa cor da paleta vão para a parte que brilha à noite', async () => {
    const root = syntheticLandmark(0.4);
    const read = (t: THREE.Texture) => (t.image && 'data' in (t.image as object) ? (t.image as Pixels) : null);
    const { model } = await convertLandmark(root, read, { place: 'near', height: 20, colors: 10, emissiveGlow: false, glowColors: [SYNTH.beak] });
    const glow = model.parts.find((p) => p.mat === 'glow');
    expect(glow).toBeDefined();
    const c = glow?.geometry.getAttribute('color');
    const beak = new THREE.Color(SYNTH.beak);
    if (c) for (let i = 0; i < c.count; i += 3) expect(Math.hypot(c.getX(i) - beak.r, c.getY(i) - beak.g, c.getZ(i) - beak.b)).toBeLessThan(0.25);
  }, 60_000);

  it('frente em −X no arquivo (--front -x) e giro extra (--yaw 90) chegam ao mesmo lugar', async () => {
    const read = (t: THREE.Texture) => (t.image && 'data' in (t.image as object) ? (t.image as Pixels) : null);
    const tipX = async (front: '+z' | '-x', yaw: number) => {
      const root = syntheticLandmark(0.4);
      if (front === '-x') { root.rotation.y = -Math.PI / 2; root.updateMatrixWorld(true); } // o bico passa de +Z para −X
      const { model } = await convertLandmark(root, read, { place: 'near', height: 20, colors: 8, front, yaw });
      const b = boundsOf(model);
      return { w: b.max.x - b.min.x, d: b.max.z - b.min.z };
    };
    const a = await tipX('+z', 0); const b = await tipX('-x', 0); const c = await tipX('+z', 90);
    expect(b.w).toBeCloseTo(a.w, 0); expect(b.d).toBeCloseTo(a.d, 0);
    // Girado 90°: largura e profundidade trocam.
    expect(c.w).toBeCloseTo(a.d, 0); expect(c.d).toBeCloseTo(a.w, 0);
  }, 60_000);
});
