// Conversor de marcos baixados (docs/ARTE.md, "Marcos baixados"): um modelo realista, texturizado e com muitos
// triângulos (ex.: a galeria CC0 do Meshy) → o marco do jogo, low-poly de cor chapada por face, na convenção de
// landmarks/types.ts. Puro (three, sem DOM): o navegador (tools/convert-landmark.html) lê o .glb e as texturas e
// chama `convertLandmark`; os testes chamam as partes com uma textura em memória (tests/landmark-gltf.test.ts).
//
// Etapas:
// 1. `gatherTriangles`: todas as malhas viram uma sopa de triângulos no referencial do mundo (transformação dos nós
//    aplicada), cada um com a cor da textura sob ele (média de 4 amostras, sRGB → linear) × a cor do material × a
//    cor dos vértices, e a marca de luz (material emissivo ou de nome pedido);
// 2. `orient`: em pé (+Y), frente em +X, na altura pedida, pegada centrada na origem e base em y = 0;
// 3. `quantize`: a paleta curta (k-means sobre as cores, pesado pela área) — é o que faz ler como o estilo chapado
//    do jogo e não como ruído de foto;
// 4. `decimate`: reduz ao alvo de triângulos com o simplificador do meshoptimizer (o do SimplifyModifier do three),
//    que preserva a silhueta (erro quadrático); componentes pequenos e, em último caso, agrupamento de vértices;
// 5. `transfer`: cada triângulo da malha reduzida fica com a cor da paleta de MAIOR ÁREA entre os triângulos
//    originais mais próximos dele (com a mesma face voltada) — moda, não média: a média de branco e preto é cinza;
// 6. `paint` (--paint): regiões da caixa final (x, y, z de 0 a 1) repintadas de uma cor — a cegonha que vira tuiuiú
//    (cabeça e pescoço pretos, o colar vermelho), o boi que vira búfalo.
//
// Modo PEÇA (`part`, docs/ARTE.md "Peças baixadas"): o bicho ou a estátua que entra num marco procedural
// (src/render/scenery/landmarks/parts.ts). Sem lugar: o alvo de triângulos e a medida padrão (altura ou comprimento)
// são os da peça, tudo vai para a parte lisa (nada brilha) e quem confere é o validador da peça.
import * as THREE from 'three';
import { MeshoptSimplifier } from 'three/examples/jsm/libs/meshopt_simplifier.module.js';
import { mergeVertices } from 'three/examples/jsm/utils/BufferGeometryUtils.js';
import type { Model, ModelPart } from '../../src/render/scenery/geom';
import { checkLandmarkModel, checkLandmarkPart } from '../../src/render/scenery/landmarks/check';
import { isPartName, PART_NAMES, PART_SPECS } from '../../src/render/scenery/landmarks/parts';
import type { LandmarkDef } from '../../src/render/scenery/landmarks/types';

export type Place = LandmarkDef['place'];

/** Alvo de triângulos por lugar (abaixo do orçamento do validador: near 3.500, far 5.000, skyline 2.500). */
export const TARGET_TRIS: Record<Place, number> = { near: 2500, far: 4000, skyline: 2000 };
/** Altura sem `height`/`scale` e sem marco procedural para medir (o tool usa a altura do procedural do mesmo id). */
export const DEFAULT_HEIGHT: Record<Place, number> = { near: 12, far: 40, skyline: 150 };

/** Pixels RGBA de 8 bits; a linha 0 é v = 0 quando a textura tem flipY = false (o glTF). */
export interface Pixels { width: number; height: number; data: ArrayLike<number> }
/** Lê os pixels de uma textura (no navegador: desenha a imagem num canvas). null = não deu para ler. */
export type PixelReader = (texture: THREE.Texture) => Pixels | null;

/** Sopa de triângulos: 9 números de posição, 3 de cor (linear) e a marca de luz por triângulo. */
export interface Soup { count: number; pos: Float32Array; color: Float32Array; glow: Uint8Array; warnings: string[] }

export interface GatherOptions {
  /** Materiais cujo nome casa viram luz (`glow`). */
  glowMaterials?: RegExp | null;
  /** Material com emissivo vira luz (padrão: sim). */
  emissiveGlow?: boolean;
}

const SRGB_LUT = Float32Array.from({ length: 256 }, (_, i) => {
  const c = i / 255;
  return c < 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
});
const toSrgb = (c: number) => (c <= 0.0031308 ? c * 12.92 : 1.055 * Math.max(0, c) ** (1 / 2.4) - 0.055);

/** Canal de uv de uma textura (glTF TEXCOORD_n → uv, uv1, uv2, uv3). */
const uvName = (channel: number) => (channel > 0 ? `uv${channel}` : 'uv');

export function gatherTriangles(root: THREE.Object3D, read: PixelReader, opts: GatherOptions = {}): Soup {
  root.updateMatrixWorld(true);
  const meshes: THREE.Mesh[] = [];
  root.traverseVisible((o) => { if ((o as THREE.Mesh).isMesh) meshes.push(o as THREE.Mesh); });
  let total = 0;
  for (const m of meshes) {
    const g = m.geometry; const idx = g.getIndex(); const pos = g.getAttribute('position');
    if (pos) total += Math.floor((idx ? idx.count : pos.count) / 3);
  }
  const out: Soup = { count: 0, pos: new Float32Array(total * 9), color: new Float32Array(total * 3), glow: new Uint8Array(total), warnings: [] };
  const pixelsOf = new Map<THREE.Texture, Pixels | null>();
  const va = new THREE.Vector3(); const vb = new THREE.Vector3(); const vc = new THREE.Vector3();
  const ua = new THREE.Vector2(); const ub = new THREE.Vector2(); const uc = new THREE.Vector2(); const us = new THREE.Vector2();
  const tint = new THREE.Color();
  for (const mesh of meshes) {
    const g = mesh.geometry;
    const pos = g.getAttribute('position');
    if (!pos) continue;
    const index = g.getIndex();
    const col = g.getAttribute('color');
    const m = mesh.matrixWorld;
    const order = m.determinant() < 0 ? [0, 2, 1] : [0, 1, 2];
    const mats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
    const count = index ? index.count : pos.count;
    const groups = g.groups.length ? g.groups : [{ start: 0, count, materialIndex: 0 }];
    for (const grp of groups) {
      const material = (mats[grp.materialIndex ?? 0] ?? mats[0]) as THREE.MeshStandardMaterial;
      if (!material || material.visible === false) continue;
      tint.set(material.color ?? '#ffffff');
      const e = material.emissive;
      const lit = (opts.emissiveGlow ?? true) && !!e && Math.max(e.r, e.g, e.b) * (material.emissiveIntensity ?? 1) > 0.05;
      const glow = lit || !!opts.glowMaterials?.test(material.name ?? '') ? 1 : 0;
      const map = material.map ?? null;
      let px: Pixels | null = null;
      // (o atributo pode vir intercalado do GLTFLoader: getX/getY servem igual, só o tipo do three pede BufferAttribute)
      let uv: THREE.BufferAttribute | undefined;
      if (map) {
        if (!pixelsOf.has(map)) {
          let p: Pixels | null = null;
          try { p = read(map); } catch { p = null; }
          pixelsOf.set(map, p);
          if (!p) out.warnings.push(`textura do material "${material.name}" não pôde ser lida: fica a cor do material`);
        }
        px = pixelsOf.get(map) ?? null;
        uv = g.getAttribute(uvName(map.channel ?? 0)) as THREE.BufferAttribute | undefined;
        if (px && !uv) out.warnings.push(`material "${material.name}" tem textura mas a malha "${mesh.name}" não tem uv`);
        if (map.matrixAutoUpdate) map.updateMatrix();
      }
      const lut = map && map.colorSpace === THREE.SRGBColorSpace;
      const sample = (u: THREE.Vector2, acc: number[]) => {
        if (!px || !map) return;
        us.copy(u);
        map.transformUv(us);
        const x = Math.min(px.width - 1, Math.max(0, Math.floor(us.x * px.width)));
        const y = Math.min(px.height - 1, Math.max(0, Math.floor(us.y * px.height)));
        const o = (y * px.width + x) * 4;
        for (let k = 0; k < 3; k++) acc[k] += lut ? SRGB_LUT[px.data[o + k]] : px.data[o + k] / 255;
      };
      const at = (k: number) => (index ? index.getX(k) : k);
      const end = Math.min(grp.start + grp.count, count);
      for (let k = grp.start; k + 2 < end; k += 3) {
        const i0 = at(k + order[0]); const i1 = at(k + order[1]); const i2 = at(k + order[2]);
        va.fromBufferAttribute(pos, i0).applyMatrix4(m);
        vb.fromBufferAttribute(pos, i1).applyMatrix4(m);
        vc.fromBufferAttribute(pos, i2).applyMatrix4(m);
        const t = out.count++;
        out.pos.set([va.x, va.y, va.z, vb.x, vb.y, vb.z, vc.x, vc.y, vc.z], t * 9);
        // Cor: textura (média do centro e de 3 pontos entre o centro e cada canto) × material × vértices.
        let r = tint.r; let gg = tint.g; let b = tint.b;
        if (px && uv) {
          ua.fromBufferAttribute(uv, i0); ub.fromBufferAttribute(uv, i1); uc.fromBufferAttribute(uv, i2);
          const acc = [0, 0, 0];
          const cx = (ua.x + ub.x + uc.x) / 3; const cy = (ua.y + ub.y + uc.y) / 3;
          sample(new THREE.Vector2(cx, cy), acc);
          for (const q of [ua, ub, uc]) sample(new THREE.Vector2((cx + q.x) / 2, (cy + q.y) / 2), acc);
          r *= acc[0] / 4; gg *= acc[1] / 4; b *= acc[2] / 4;
        }
        if (col) {
          r *= (col.getX(i0) + col.getX(i1) + col.getX(i2)) / 3;
          gg *= (col.getY(i0) + col.getY(i1) + col.getY(i2)) / 3;
          b *= (col.getZ(i0) + col.getZ(i1) + col.getZ(i2)) / 3;
        }
        out.color[t * 3] = r; out.color[t * 3 + 1] = gg; out.color[t * 3 + 2] = b;
        out.glow[t] = glow;
      }
    }
  }
  out.pos = out.pos.slice(0, out.count * 9);
  out.color = out.color.slice(0, out.count * 3);
  out.glow = out.glow.slice(0, out.count);
  out.warnings = [...new Set(out.warnings)];
  return out;
}

// ───────────────────────────── Orientação, escala e base ─────────────────────────────

export interface OrientOptions {
  /** Lugar do marco (sem `part`): dá o alvo de triângulos e a altura padrão. */
  place?: Place;
  /** Altura final (m); sem ela, sem `length` e sem `scale`, DEFAULT_HEIGHT do lugar. */
  height?: number;
  /** Comprimento final (m) ao longo de X (da traseira à frente), no lugar da altura: o jacaré mede 2,7 m. */
  length?: number;
  /** Escala uniforme (no lugar da altura). */
  scale?: number;
  /** Eixo do arquivo que é a frente do marco (vai para +X, a pista). Padrão: +z (a frente do glTF). */
  front?: '+x' | '-x' | '+z' | '-z';
  /** Eixo de cima do arquivo (z: exportado em Z para cima). Padrão: y. */
  up?: 'y' | 'z';
  /** Giro extra em torno de Y (graus, anti-horário visto de cima), depois da frente. */
  yaw?: number;
}

const FRONT_TURN: Record<NonNullable<OrientOptions['front']>, number> = { '+x': 0, '-x': Math.PI, '+z': Math.PI / 2, '-z': -Math.PI / 2 };

/** Posições na convenção do marco e a matriz usada (para a prévia mostrar o original no mesmo lugar). */
export function orient(pos: Float32Array, opts: OrientOptions): { pos: Float32Array; matrix: THREE.Matrix4; scale: number } {
  const rot = new THREE.Matrix4();
  if (opts.up === 'z') rot.makeRotationX(-Math.PI / 2);
  rot.premultiply(new THREE.Matrix4().makeRotationY(FRONT_TURN[opts.front ?? '+z'] + ((opts.yaw ?? 0) * Math.PI) / 180));
  const out = new Float32Array(pos.length);
  const v = new THREE.Vector3();
  const box = new THREE.Box3();
  for (let i = 0; i < pos.length; i += 3) {
    v.set(pos[i], pos[i + 1], pos[i + 2]).applyMatrix4(rot);
    out[i] = v.x; out[i + 1] = v.y; out[i + 2] = v.z;
    box.expandByPoint(v);
  }
  const tall = Math.max(1e-9, box.max.y - box.min.y);
  const scale = opts.scale ?? (opts.length !== undefined
    ? opts.length / Math.max(1e-9, box.max.x - box.min.x)
    : (opts.height ?? DEFAULT_HEIGHT[opts.place ?? 'near']) / tall);
  const cx = (box.min.x + box.max.x) / 2; const cz = (box.min.z + box.max.z) / 2;
  for (let i = 0; i < out.length; i += 3) {
    out[i] = (out[i] - cx) * scale; out[i + 1] = (out[i + 1] - box.min.y) * scale; out[i + 2] = (out[i + 2] - cz) * scale;
  }
  const matrix = new THREE.Matrix4().makeScale(scale, scale, scale)
    .multiply(new THREE.Matrix4().makeTranslation(-cx, -box.min.y, -cz)).multiply(rot);
  return { pos: out, matrix, scale };
}

// ───────────────────────────── Paleta ─────────────────────────────

export interface Quantized {
  /** Cores da paleta (linear, 3 por cor), da de maior peso para a de menor. */
  palette: Float32Array;
  /** Índice na paleta de cada cor de entrada. */
  index: Uint16Array;
}

/**
 * Paleta de até `k` cores para `colors` (linear, 3 por amostra) pesadas por `weights` (a área). As cores vão para
 * caixas de 6 bits por canal (sRGB) e o k-means roda sobre as caixas ocupadas, começando pela mais pesada e depois
 * pela de maior peso × distância² (k-means++ sem sorteio: determinístico). A cor da paleta é a média LINEAR do grupo.
 * Depois, os grupos a menos de `merge` um do outro (distância sRGB 0–1) viram um só, do par mais perto ao mais longe:
 * o k-means parte a maior área (o corpo, com o ruído e o sombreado da foto) em tons quase iguais, e o modelo sai
 * manchado em vez de chapado. `fixed`: paleta dada (--palette), cada cor vai para a mais próxima dela.
 */
export interface QuantizeOptions {
  fixed?: readonly THREE.Color[];
  merge?: number;
  /**
   * Com `fixed`: cada cor vai para o tom da paleta de MESMA CLARIDADE RELATIVA, não para o de cor mais perto (a
   * estátua de bronze, o boi que vira búfalo de ardósia). A claridade (luma sRGB) de cada cor é posta na faixa entre os
   * percentis 5 e 95 do modelo (pesados pela área) e cai no tom da mesma altura da escada da paleta (ordenada pela
   * claridade de cada tom): o escuro vai ao tom mais escuro, o claro ao mais claro, e o matiz não conta.
   */
  byLight?: boolean;
}
export const DEFAULT_MERGE = 0.12;

const luma = (r: number, g: number, b: number) => 0.2126 * r + 0.7152 * g + 0.0722 * b;

export function quantize(colors: Float32Array, weights: Float32Array, k: number, opts: QuantizeOptions = {}): Quantized {
  const fixed = opts.fixed;
  const merge = opts.merge ?? DEFAULT_MERGE;
  const n = weights.length;
  const srgb = new Float32Array(n * 3);
  for (let i = 0; i < n * 3; i++) srgb[i] = toSrgb(Math.min(1, Math.max(0, colors[i])));
  const nearest = (cs: Float32Array, kk: number, x: number, y: number, z: number) => {
    let best = 0; let bd = Infinity;
    for (let j = 0; j < kk; j++) {
      const d = (cs[j * 3] - x) ** 2 + (cs[j * 3 + 1] - y) ** 2 + (cs[j * 3 + 2] - z) ** 2;
      if (d < bd) { bd = d; best = j; }
    }
    return best;
  };
  if (fixed && fixed.length) {
    const palette = new Float32Array(fixed.flatMap((c) => [c.r, c.g, c.b]));
    const ps = palette.map(toSrgb);
    const index = new Uint16Array(n);
    if (!opts.byLight) {
      for (let i = 0; i < n; i++) index[i] = nearest(ps, fixed.length, srgb[i * 3], srgb[i * 3 + 1], srgb[i * 3 + 2]);
      return { palette, index };
    }
    const m = fixed.length;
    const lumaOf = (j: number) => luma(ps[j * 3], ps[j * 3 + 1], ps[j * 3 + 2]);
    const ladder = Array.from({ length: m }, (_, j) => j).sort((a, b) => lumaOf(a) - lumaOf(b) || a - b);
    const L = new Float32Array(n);
    for (let i = 0; i < n; i++) L[i] = luma(srgb[i * 3], srgb[i * 3 + 1], srgb[i * 3 + 2]);
    const byL = Array.from({ length: n }, (_, i) => i).sort((a, b) => L[a] - L[b] || a - b);
    let total = 0;
    for (let i = 0; i < n; i++) total += Math.max(1e-12, weights[i]);
    const percentile = (p: number) => {
      let acc = 0;
      for (const i of byL) { acc += Math.max(1e-12, weights[i]); if (acc >= p * total) return L[i]; }
      return L[byL[n - 1]];
    };
    const lo = percentile(0.05); const hi = percentile(0.95);
    for (let i = 0; i < n; i++) {
      const t = hi > lo ? Math.min(1, Math.max(0, (L[i] - lo) / (hi - lo))) : 0.5;
      index[i] = ladder[Math.min(m - 1, Math.round(t * (m - 1)))];
    }
    return { palette, index };
  }
  // Caixas ocupadas: peso, soma sRGB (para o k-means) e soma linear (para a cor final).
  const binOf = new Map<number, number>();
  const bin = new Int32Array(n);
  const bw: number[] = []; const bs: number[] = []; const bl: number[] = [];
  for (let i = 0; i < n; i++) {
    const q = (c: number) => Math.min(63, Math.floor(c * 64));
    const key = (q(srgb[i * 3]) << 12) | (q(srgb[i * 3 + 1]) << 6) | q(srgb[i * 3 + 2]);
    let b = binOf.get(key);
    if (b === undefined) { b = bw.length; binOf.set(key, b); bw.push(0); bs.push(0, 0, 0); bl.push(0, 0, 0); }
    bin[i] = b;
    const w = Math.max(1e-12, weights[i]);
    bw[b] += w;
    for (let c = 0; c < 3; c++) { bs[b * 3 + c] += srgb[i * 3 + c] * w; bl[b * 3 + c] += colors[i * 3 + c] * w; }
  }
  const nb = bw.length;
  const pts = new Float32Array(nb * 3);
  for (let b = 0; b < nb; b++) for (let c = 0; c < 3; c++) pts[b * 3 + c] = bs[b * 3 + c] / bw[b];
  const kk = Math.max(1, Math.min(k, nb));
  // Começo: a caixa mais pesada; depois a de maior peso × distância² até o centro mais perto.
  const cs = new Float32Array(kk * 3);
  let first = 0;
  for (let b = 1; b < nb; b++) if (bw[b] > bw[first]) first = b;
  cs.set(pts.subarray(first * 3, first * 3 + 3), 0);
  const d2 = new Float64Array(nb).fill(Infinity);
  for (let j = 1; j < kk; j++) {
    let pick = 0; let best = -1;
    for (let b = 0; b < nb; b++) {
      const p = j - 1;
      const d = (pts[b * 3] - cs[p * 3]) ** 2 + (pts[b * 3 + 1] - cs[p * 3 + 1]) ** 2 + (pts[b * 3 + 2] - cs[p * 3 + 2]) ** 2;
      if (d < d2[b]) d2[b] = d;
      const s = bw[b] * d2[b];
      if (s > best) { best = s; pick = b; }
    }
    cs.set(pts.subarray(pick * 3, pick * 3 + 3), j * 3);
  }
  const assign = new Int32Array(nb);
  for (let it = 0; it < 40; it++) {
    let moved = 0;
    for (let b = 0; b < nb; b++) {
      const j = nearest(cs, kk, pts[b * 3], pts[b * 3 + 1], pts[b * 3 + 2]);
      if (j !== assign[b] || it === 0) { moved++; assign[b] = j; }
    }
    const sum = new Float64Array(kk * 3); const w = new Float64Array(kk);
    for (let b = 0; b < nb; b++) { const j = assign[b]; w[j] += bw[b]; for (let c = 0; c < 3; c++) sum[j * 3 + c] += pts[b * 3 + c] * bw[b]; }
    for (let j = 0; j < kk; j++) if (w[j] > 0) for (let c = 0; c < 3; c++) cs[j * 3 + c] = sum[j * 3 + c] / w[j];
    if (moved === 0) break;
  }
  // Peso, centro sRGB e soma linear de cada grupo.
  const w = new Float64Array(kk); const lin = new Float64Array(kk * 3); const ss = new Float64Array(kk * 3);
  for (let b = 0; b < nb; b++) {
    const j = assign[b]; w[j] += bw[b];
    for (let c = 0; c < 3; c++) { lin[j * 3 + c] += bl[b * 3 + c]; ss[j * 3 + c] += bs[b * 3 + c]; }
  }
  // Tons quase iguais: junta o par mais perto enquanto ele estiver a menos de `merge`.
  const into = Int32Array.from({ length: kk }, (_, j) => j);
  for (;;) {
    let pa = -1; let pb = -1; let pd = merge;
    for (let a = 0; a < kk; a++) {
      if (w[a] <= 0) continue;
      for (let b = a + 1; b < kk; b++) {
        if (w[b] <= 0) continue;
        const d = Math.hypot(ss[a * 3] / w[a] - ss[b * 3] / w[b], ss[a * 3 + 1] / w[a] - ss[b * 3 + 1] / w[b], ss[a * 3 + 2] / w[a] - ss[b * 3 + 2] / w[b]);
        if (d < pd) { pd = d; pa = a; pb = b; }
      }
    }
    if (pa < 0) break;
    w[pa] += w[pb]; w[pb] = 0;
    for (let c = 0; c < 3; c++) { lin[pa * 3 + c] += lin[pb * 3 + c]; ss[pa * 3 + c] += ss[pb * 3 + c]; }
    for (let j = 0; j < kk; j++) if (into[j] === pb) into[j] = pa;
  }
  for (let b = 0; b < nb; b++) assign[b] = into[assign[b]];
  // Grupos não vazios, do mais pesado ao mais leve; a cor é a média linear dos membros.
  const order = Array.from({ length: kk }, (_, j) => j).filter((j) => w[j] > 0).sort((a, b) => w[b] - w[a] || a - b);
  const remap = new Int32Array(kk).fill(-1);
  order.forEach((j, i) => { remap[j] = i; });
  const palette = new Float32Array(order.length * 3);
  order.forEach((j, i) => { for (let c = 0; c < 3; c++) palette[i * 3 + c] = lin[j * 3 + c] / w[j]; });
  const index = new Uint16Array(n);
  for (let i = 0; i < n; i++) index[i] = remap[assign[bin[i]]];
  return { palette, index };
}

// ───────────────────────────── Redução ─────────────────────────────

export interface Decimated { pos: Float32Array; method: 'nenhuma' | 'meshopt' | 'meshopt+componentes' | 'agrupamento' }

/** Sopa de triângulos (9 por triângulo) → até `target` triângulos, pela malha soldada nas posições. */
export async function decimate(pos: Float32Array, target: number): Promise<Decimated> {
  const tris = pos.length / 9;
  if (tris <= target) return { pos: pos.slice(), method: 'nenhuma' };
  await MeshoptSimplifier.ready;
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
  const box = new THREE.Box3().setFromBufferAttribute(g.getAttribute('position') as THREE.BufferAttribute);
  const welded = mergeVertices(g, Math.max(1e-6, box.max.distanceTo(box.min) * 1e-6));
  const index = new Uint32Array((welded.getIndex() as THREE.BufferAttribute).array);
  const verts = new Float32Array((welded.getAttribute('position') as THREE.BufferAttribute).array);
  const goal = Math.max(3, Math.floor(target) * 3);
  // 1) Colapso de arestas pelo erro quadrático, sem teto de erro (para no alvo).
  let [out] = MeshoptSimplifier.simplify(index, verts, 3, goal, 1, []);
  let method: Decimated['method'] = 'meshopt';
  // 2) Muitas peças soltas (cada uma não desce de uns poucos triângulos): some com as pequenas (≤ 1% do tamanho).
  if (out.length > goal) { [out] = MeshoptSimplifier.simplify(index, verts, 3, goal, 0.01, ['Prune']); [out] = MeshoptSimplifier.simplify(out, verts, 3, goal, 1, []); method = 'meshopt+componentes'; }
  // 3) Último recurso: agrupamento de vértices (sempre chega; a silhueta sofre mais).
  if (out.length > goal) { [out] = MeshoptSimplifier.simplifySloppy(index, verts, 3, null, goal, 1); method = 'agrupamento'; }
  const res: number[] = [];
  const a = new THREE.Vector3(); const b = new THREE.Vector3(); const c = new THREE.Vector3();
  for (let i = 0; i + 2 < out.length; i += 3) {
    const [i0, i1, i2] = [out[i], out[i + 1], out[i + 2]];
    if (i0 === i1 || i1 === i2 || i0 === i2) continue;
    a.fromArray(verts, i0 * 3); b.fromArray(verts, i1 * 3); c.fromArray(verts, i2 * 3);
    if (b.clone().sub(a).cross(c.clone().sub(a)).lengthSq() < 1e-14) continue;
    res.push(a.x, a.y, a.z, b.x, b.y, b.z, c.x, c.y, c.z);
  }
  g.dispose(); welded.dispose();
  return { pos: new Float32Array(res), method };
}

// ───────────────────────────── Cor da malha reduzida ─────────────────────────────

function triInfo(pos: Float32Array, t: number, n: THREE.Vector3): number {
  const o = t * 9;
  const ux = pos[o + 3] - pos[o]; const uy = pos[o + 4] - pos[o + 1]; const uz = pos[o + 5] - pos[o + 2];
  const vx = pos[o + 6] - pos[o]; const vy = pos[o + 7] - pos[o + 1]; const vz = pos[o + 8] - pos[o + 2];
  n.set(uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx);
  const len = n.length();
  if (len > 0) n.divideScalar(len);
  return len / 2;
}

/**
 * Para cada triângulo de `outPos`, a cor (índice da paleta) e a luz de maior área entre os triângulos de `inPos` que
 * ficam mais perto dele que de qualquer outro (distância ao triângulo + penalidade se a face olha para o outro lado).
 */
export function transfer(inPos: Float32Array, inIndex: Uint16Array, inGlow: Uint8Array, outPos: Float32Array, paletteSize: number): { index: Uint16Array; glow: Uint8Array } {
  const nIn = inPos.length / 9; const nOut = outPos.length / 9;
  const tris: THREE.Triangle[] = []; const normals: THREE.Vector3[] = [];
  const box = new THREE.Box3();
  let edge = 0;
  for (let t = 0; t < nOut; t++) {
    const o = t * 9;
    const tri = new THREE.Triangle(new THREE.Vector3().fromArray(outPos, o), new THREE.Vector3().fromArray(outPos, o + 3), new THREE.Vector3().fromArray(outPos, o + 6));
    tris.push(tri);
    const n = new THREE.Vector3(); triInfo(outPos, t, n); normals.push(n);
    box.expandByPoint(tri.a).expandByPoint(tri.b).expandByPoint(tri.c);
    edge += Math.max(tri.a.distanceTo(tri.b), tri.b.distanceTo(tri.c), tri.c.distanceTo(tri.a));
  }
  const diag = Math.max(1e-6, box.max.distanceTo(box.min));
  const s = Math.max(edge / Math.max(1, nOut), diag / 128);
  const cell = (v: number, o: number) => Math.floor((v - o) / s);
  const key = (x: number, y: number, z: number) => ((x + 1024) * 4096 + (y + 1024)) * 4096 + (z + 1024);
  const grid = new Map<number, number[]>();
  const tb = new THREE.Box3();
  for (let t = 0; t < nOut; t++) {
    tb.setFromPoints([tris[t].a, tris[t].b, tris[t].c]);
    for (let x = cell(tb.min.x, box.min.x); x <= cell(tb.max.x, box.min.x); x++)
      for (let y = cell(tb.min.y, box.min.y); y <= cell(tb.max.y, box.min.y); y++)
        for (let z = cell(tb.min.z, box.min.z); z <= cell(tb.max.z, box.min.z); z++) {
          const k = key(x, y, z);
          const list = grid.get(k);
          if (list) list.push(t); else grid.set(k, [t]);
        }
  }
  const votes = new Float64Array(nOut * paletteSize);
  const glowVotes = new Float64Array(nOut); const all = new Float64Array(nOut);
  const seen = new Int32Array(nOut).fill(-1);
  const p = new THREE.Vector3(); const q = new THREE.Vector3(); const n = new THREE.Vector3();
  const maxRing = Math.ceil(diag / s) + 1;
  for (let i = 0; i < nIn; i++) {
    const area = triInfo(inPos, i, n);
    if (area <= 0) continue;
    const o = i * 9;
    p.set((inPos[o] + inPos[o + 3] + inPos[o + 6]) / 3, (inPos[o + 1] + inPos[o + 4] + inPos[o + 7]) / 3, (inPos[o + 2] + inPos[o + 5] + inPos[o + 8]) / 3);
    const cx = cell(p.x, box.min.x); const cy = cell(p.y, box.min.y); const cz = cell(p.z, box.min.z);
    let best = -1; let bestScore = Infinity;
    for (let r = 0; r <= maxRing; r++) {
      if (best >= 0 && (r - 1) * s > bestScore) break;
      for (let x = cx - r; x <= cx + r; x++) for (let y = cy - r; y <= cy + r; y++) for (let z = cz - r; z <= cz + r; z++) {
        if (Math.max(Math.abs(x - cx), Math.abs(y - cy), Math.abs(z - cz)) !== r) continue;
        const list = grid.get(key(x, y, z));
        if (!list) continue;
        for (const t of list) {
          if (seen[t] === i) continue;
          seen[t] = i;
          tris[t].closestPointToPoint(p, q);
          const score = q.distanceTo(p) + 0.5 * s * (1 - n.dot(normals[t]));
          if (score < bestScore) { bestScore = score; best = t; }
        }
      }
    }
    if (best < 0) continue;
    votes[best * paletteSize + inIndex[i]] += area;
    all[best] += area;
    if (inGlow[i]) glowVotes[best] += area;
  }
  const index = new Uint16Array(nOut); const glow = new Uint8Array(nOut);
  const voted: number[] = [];
  for (let t = 0; t < nOut; t++) {
    if (all[t] <= 0) continue;
    voted.push(t);
    let bj = 0;
    for (let j = 1; j < paletteSize; j++) if (votes[t * paletteSize + j] > votes[t * paletteSize + bj]) bj = j;
    index[t] = bj;
    glow[t] = glowVotes[t] > all[t] / 2 ? 1 : 0;
  }
  // Triângulo sem nenhum original perto (pequeno, no meio de outros): a cor do vizinho com voto mais perto.
  const centre = (t: number, v: THREE.Vector3) => tris[t].getMidpoint(v);
  for (let t = 0; t < nOut; t++) {
    if (all[t] > 0 || voted.length === 0) continue;
    centre(t, p);
    let bd = Infinity; let bt = voted[0];
    for (const u of voted) { const d = centre(u, q).distanceToSquared(p); if (d < bd) { bd = d; bt = u; } }
    index[t] = index[bt]; glow[t] = glow[bt];
  }
  return { index, glow };
}

// ───────────────────────────── Pintura por região (--paint) ─────────────────────────────

export type PaintAxis = 'x' | 'y' | 'z';
export interface PaintCond { axis: PaintAxis; op: '<' | '>' | '<=' | '>='; value: number }
/**
 * Regra de pintura: as faces cujo centro cumpre TODAS as condições ficam com `color` (hex sRGB). As coordenadas são
 * da caixa final, de 0 a 1: x de trás (0) para a frente (1, a cabeça, +X), y da base (0) ao topo (1), z de −Z (0) a
 * +Z (1). Várias regras: aplicadas em ordem, a última que casa vence.
 */
export interface PaintRule { when: PaintCond[]; color: string }

const COND = /^([xyz])\s*(<=|>=|<|>)\s*(-?[0-9]*\.?[0-9]+)$/i;
const HEX = /^#([0-9a-f]{3}|[0-9a-f]{6})$/;

/** Lê `--paint 'y>0.58,y<0.62:#c8202a'` (condições separadas por vírgula, dois-pontos, a cor). */
export function parsePaintRule(text: string): PaintRule {
  const usage = `--paint "${text}": use "<condições>:<cor>", ex. 'y>0.62:#1b1b1b' ou 'y>0.58,y<0.62,x>0.5:#c8202a' (x, y, z de 0 a 1)`;
  const at = text.lastIndexOf(':');
  if (at < 0) throw new Error(usage);
  const conds = text.slice(0, at).split(',').map((c) => c.trim()).filter(Boolean);
  const colorText = text.slice(at + 1).trim().toLowerCase();
  if (conds.length === 0) throw new Error(usage);
  const when: PaintCond[] = conds.map((c) => {
    const m = COND.exec(c);
    if (!m) throw new Error(`${usage} — a condição "${c}" não se lê (eixo x, y ou z; <, >, <= ou >=; um número)`);
    const value = Number(m[3]);
    if (!(value >= 0 && value <= 1)) throw new Error(`--paint "${text}": a condição "${c}" pede um número de 0 a 1 (a fração da caixa)`);
    return { axis: m[1].toLowerCase() as PaintAxis, op: m[2] as PaintCond['op'], value };
  });
  const hex = HEX.exec(colorText);
  if (!hex) throw new Error(`--paint "${text}": a cor "${colorText}" não é #rrggbb`);
  const h = hex[1];
  const color = h.length === 3 ? `#${h[0]}${h[0]}${h[1]}${h[1]}${h[2]}${h[2]}` : `#${h}`;
  return { when, color };
}

const holds = (c: PaintCond, v: number): boolean => (c.op === '<' ? v < c.value : c.op === '>' ? v > c.value : c.op === '<=' ? v <= c.value : v >= c.value);

/** Índice da regra (a última que casa) para o ponto normalizado, ou −1. */
export function paintRuleAt(rules: readonly PaintRule[], x: number, y: number, z: number): number {
  let hit = -1;
  rules.forEach((r, i) => { if (r.when.every((c) => holds(c, c.axis === 'x' ? x : c.axis === 'y' ? y : z))) hit = i; });
  return hit;
}

// ───────────────────────────── Tudo junto ─────────────────────────────

export interface ConvertOptions extends OrientOptions, GatherOptions {
  /** Modo peça (parts.ts): o nome da peça; sem lugar, o alvo e a medida padrão são os dela, nada brilha. */
  part?: string;
  /** Pintura por região, em ordem (a última que casa vence). */
  paint?: PaintRule[];
  /** Com `palette`: cada face vai para o tom de mesma claridade relativa (QuantizeOptions.byLight). */
  byLight?: boolean;
  /** Alvo de triângulos (padrão: TARGET_TRIS do lugar). */
  tris?: number;
  /** Tamanho da paleta (padrão 12). */
  colors?: number;
  /** Paleta fixa (hex sRGB): cada face vai para a cor mais próxima da lista. */
  palette?: string[];
  /** Tons a menos disto (sRGB 0–1) viram um só (padrão DEFAULT_MERGE; 0 desliga). */
  merge?: number;
  /** Cores (hex sRGB) cujas faces vão para a parte que brilha à noite: a cor mais próxima da paleta e as parecidas. */
  glowColors?: string[];
}

export interface ConvertReport {
  place?: Place;
  part?: string;
  trianglesIn: number;
  trianglesOut: number;
  target: number;
  method: Decimated['method'];
  scale: number;
  bounds: { minX: number; maxX: number; minY: number; maxY: number; minZ: number; maxZ: number };
  /** Cores usadas, da de maior área para a de menor; `share` = fração da área do modelo. */
  palette: { hex: string; share: number; glow: boolean }[];
  glowTriangles: number;
  /** O validador do jogo (vazio = aceito). */
  problems: string[];
  warnings: string[];
}

export async function convertLandmark(root: THREE.Object3D, read: PixelReader, options: ConvertOptions): Promise<{ model: Model; report: ConvertReport; matrix: THREE.Matrix4 }> {
  const opts: ConvertOptions = { ...options };
  if (opts.part !== undefined && !isPartName(opts.part)) throw new Error(`"${opts.part}" não é uma peça (${PART_NAMES.join(', ')})`);
  const spec = opts.part !== undefined && isPartName(opts.part) ? PART_SPECS[opts.part] : null;
  if (!spec && !opts.place) throw new Error('diga o lugar do marco (place) ou a peça (part)');
  if (opts.byLight && !opts.palette?.length) throw new Error('--by-light pede uma paleta (--palette)');
  if (spec) {
    // A peça: a medida de verdade da especificação (altura ou comprimento); nada brilha.
    if (opts.height === undefined && opts.length === undefined && opts.scale === undefined) {
      if (spec.measure === 'length') opts.length = spec.size; else opts.height = spec.size;
    }
    opts.emissiveGlow = false; opts.glowMaterials = null; opts.glowColors = [];
  }
  const soup = gatherTriangles(root, read, opts);
  if (soup.count === 0) throw new Error('nenhum triângulo no arquivo');
  const target = Math.max(4, Math.floor(opts.tris ?? (spec ? spec.tris : TARGET_TRIS[opts.place ?? 'near'])));
  const placed = orient(soup.pos, opts);
  const area = new Float32Array(soup.count);
  const nv = new THREE.Vector3();
  for (let t = 0; t < soup.count; t++) area[t] = triInfo(placed.pos, t, nv);
  const q = quantize(soup.color, area, opts.colors ?? 12, { fixed: opts.palette?.map((h) => new THREE.Color(h)), merge: opts.merge, byLight: opts.byLight });
  const k = q.palette.length / 3;
  const dec = await decimate(placed.pos, target);
  const tr = transfer(placed.pos, q.index, soup.glow, dec.pos, k);

  // Luz por cor: a cor da paleta mais perto de cada uma pedida e as que ficam a ≤ 0,12 dela (sRGB).
  const glowEntry = new Uint8Array(k);
  for (const hex of opts.glowColors ?? []) {
    const c = new THREE.Color(hex);
    const s = [toSrgb(c.r), toSrgb(c.g), toSrgb(c.b)];
    const d = Array.from({ length: k }, (_, j) => Math.hypot(toSrgb(q.palette[j * 3]) - s[0], toSrgb(q.palette[j * 3 + 1]) - s[1], toSrgb(q.palette[j * 3 + 2]) - s[2]));
    const m = Math.min(...d);
    d.forEach((v, j) => { if (v <= m + 1e-9 || v <= 0.12) glowEntry[j] = 1; });
  }

  // Base de novo em y = 0, pegada centrada e a medida pedida (a redução mexe um pouco nas pontas).
  const out = dec.pos;
  const box = new THREE.Box3().setFromBufferAttribute(new THREE.BufferAttribute(out, 3));
  const fit = opts.scale !== undefined ? 1 : opts.length !== undefined
    ? opts.length / Math.max(1e-9, box.max.x - box.min.x)
    : (opts.height ?? DEFAULT_HEIGHT[opts.place ?? 'near']) / Math.max(1e-9, box.max.y - box.min.y);
  const cx = (box.min.x + box.max.x) / 2; const cz = (box.min.z + box.max.z) / 2;
  for (let i = 0; i < out.length; i += 3) {
    out[i] = (out[i] - cx) * fit; out[i + 1] = (out[i + 1] - box.min.y) * fit; out[i + 2] = (out[i + 2] - cz) * fit;
  }

  // Pintura por região: as cores das regras entram no fim da paleta; a face pintada não brilha.
  const rules = opts.paint ?? [];
  const ruleColors = [...new Set(rules.map((r) => r.color))];
  const kk = k + ruleColors.length;
  const pal = new Float32Array(kk * 3);
  pal.set(q.palette);
  ruleColors.forEach((hex, i) => { const c = new THREE.Color(hex); pal.set([c.r, c.g, c.b], (k + i) * 3); });
  const pb = new THREE.Box3().setFromBufferAttribute(new THREE.BufferAttribute(out, 3));
  const unit = (v: number, lo: number, hi: number) => (v - lo) / Math.max(1e-9, hi - lo);

  const nOut = out.length / 9;
  const lists: Record<'flat' | 'glow', { pos: number[]; col: number[] }> = { flat: { pos: [], col: [] }, glow: { pos: [], col: [] } };
  const share = new Float64Array(kk); const shareGlow = new Float64Array(kk);
  let totalArea = 0; let glowTriangles = 0;
  for (let t = 0; t < nOut; t++) {
    let j = tr.index[t];
    let lit = tr.glow[t] === 1 || glowEntry[j] === 1;
    if (rules.length) {
      const o = t * 9;
      const r = paintRuleAt(rules,
        unit((out[o] + out[o + 3] + out[o + 6]) / 3, pb.min.x, pb.max.x),
        unit((out[o + 1] + out[o + 4] + out[o + 7]) / 3, pb.min.y, pb.max.y),
        unit((out[o + 2] + out[o + 5] + out[o + 8]) / 3, pb.min.z, pb.max.z));
      if (r >= 0) { j = k + ruleColors.indexOf(rules[r].color); lit = false; }
    }
    const dst = lit ? lists.glow : lists.flat;
    for (let v = 0; v < 9; v++) dst.pos.push(out[t * 9 + v]);
    for (let v = 0; v < 3; v++) dst.col.push(pal[j * 3], pal[j * 3 + 1], pal[j * 3 + 2]);
    const a = triInfo(out, t, nv);
    share[j] += a; totalArea += a;
    if (lit) { shareGlow[j] += a; glowTriangles++; }
  }
  const parts: ModelPart[] = [];
  for (const mat of ['flat', 'glow'] as const) {
    const l = lists[mat];
    if (!l.pos.length) continue;
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(l.pos, 3));
    g.setAttribute('color', new THREE.Float32BufferAttribute(l.col, 3));
    g.computeVertexNormals(); // não indexada: a normal da face
    parts.push({ geometry: g, mat });
  }
  const model: Model = { parts, blob: 0 };
  const fb = new THREE.Box3().setFromBufferAttribute(new THREE.BufferAttribute(out, 3));
  const palette = Array.from({ length: kk }, (_, j) => j).filter((j) => share[j] > 0).sort((a, b) => share[b] - share[a])
    .map((j) => ({ hex: `#${new THREE.Color(pal[j * 3], pal[j * 3 + 1], pal[j * 3 + 2]).getHexString()}`, share: share[j] / Math.max(1e-12, totalArea), glow: shareGlow[j] > share[j] / 2 }));
  // A peça é uma parte lisa só (nada brilha no modo peça); o marco, até 4 partes.
  const problems = spec && opts.part
    ? (parts.length === 1 && parts[0].mat === 'flat' ? checkLandmarkPart(parts[0].geometry, opts.part) : ['a peça saiu com mais de uma parte'])
    : checkLandmarkModel(model, opts.place ?? 'near');
  const report: ConvertReport = {
    place: spec ? undefined : opts.place, part: spec ? opts.part : undefined, trianglesIn: soup.count, trianglesOut: nOut, target, method: dec.method, scale: placed.scale * fit,
    bounds: { minX: fb.min.x, maxX: fb.max.x, minY: fb.min.y, maxY: fb.max.y, minZ: fb.min.z, maxZ: fb.max.z },
    palette, glowTriangles, problems, warnings: soup.warnings,
  };
  const matrix = new THREE.Matrix4().makeScale(fit, fit, fit).multiply(new THREE.Matrix4().makeTranslation(-cx, -box.min.y, -cz)).multiply(placed.matrix);
  return { model, report, matrix };
}
