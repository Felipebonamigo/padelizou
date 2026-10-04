// Marco turístico em glTF → modelo do jogo (docs/ARTE.md, "Marcos baixados"). A convenção é a do próprio jogo
// (types.ts), sem giro na entrada:
// - metros reais, +Y para cima, origem no centro da pegada, base em y = 0, a frente (o lado que o jogador vê) em +X
//   e o comprimento ao longo da pista em Z;
// - cor por vértice (COLOR_0) vezes a cor do material (baseColorFactor, a regra do glTF); textura é ignorada (o
//   conversor, scripts/lib/landmark-convert.ts, já assa a textura em cor por face);
// - material pelo nome: `glow` (luz pintada que brilha à noite), `beacon`, `cone`, as fachadas `office`,
//   `apartment`, `classic`, `house` (com uv); qualquer outro nome é `flat`. Uma parte por material (até 4).
// A transformação dos nós entra na malha. Puro (three + GLTFLoader, sem DOM): os testes rodam no Node.
import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { cleanName } from '../../cars/gltf';
import type { Geo, MatKey, Model, ModelPart } from '../geom';
import { checkLandmarkModel, checkLandmarkPart, LANDMARK_MATS } from './check';
import type { LandmarkDef } from './types';

const FACADES: readonly MatKey[] = ['office', 'apartment', 'classic', 'house'];

/** Material do marco pelo nome: "glow", "glow_janelas", "Glow.001" → glow; nome fora da lista → flat. */
export function matKeyOf(material: THREE.Material): MatKey {
  const n = cleanName(material.name);
  for (const k of LANDMARK_MATS) if (n === k || n.startsWith(`${k}_`)) return k;
  return 'flat';
}

interface Acc { pos: number[]; nor: number[]; col: number[] | null; uv: number[] | null }

export interface GltfLandmark { model: Model | null; problems: string[]; warnings: string[] }

/**
 * Modelo do jogo a partir da cena de um .glb. `model: null` quando falta o que a convenção exige (cor por vértice);
 * os problemas de medida (escala, orçamento, base, origem) vêm do validador comum, para o lugar do marco.
 */
export function landmarkModelFromGltf(root: THREE.Object3D, place: LandmarkDef['place']): GltfLandmark {
  const r = readLandmarkGltf(root);
  return r.model ? { ...r, problems: checkLandmarkModel(r.model, place) } : r;
}

/** Só a leitura (malhas → partes por material), sem as medidas: o marco e a peça têm validadores diferentes. */
function readLandmarkGltf(root: THREE.Object3D): GltfLandmark {
  root.updateMatrixWorld(true);
  const groups = new Map<MatKey, Acc>();
  const structural: string[] = [];
  const warnings: string[] = [];
  const a = new THREE.Vector3(); const b = new THREE.Vector3(); const c = new THREE.Vector3();
  const n = new THREE.Vector3(); const tint = new THREE.Color();
  root.traverse((o) => {
    const mesh = o as THREE.Mesh;
    if (!mesh.isMesh) return;
    const geo = mesh.geometry;
    const pos = geo.getAttribute('position');
    if (!pos) return;
    const index = geo.getIndex();
    const nor = geo.getAttribute('normal'); const col = geo.getAttribute('color'); const uv = geo.getAttribute('uv');
    const m = mesh.matrixWorld;
    const nm = new THREE.Matrix3().getNormalMatrix(m);
    // Nó espelhado (escala negativa): a ordem dos vértices inverte, senão a face fica para dentro.
    const order = m.determinant() < 0 ? [0, 2, 1] : [0, 1, 2];
    const mats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
    const total = index ? index.count : pos.count;
    const groupsOf = geo.groups.length ? geo.groups : [{ start: 0, count: total, materialIndex: 0 }];
    for (const grp of groupsOf) {
      const material = mats[grp.materialIndex ?? 0] ?? mats[0];
      const key = matKeyOf(material);
      const std = material as THREE.MeshStandardMaterial;
      if (std.map) warnings.push(`textura ignorada no material "${material.name}": o marco usa cor por vértice (passe pelo conversor)`);
      if (key !== 'cone' && !col) { structural.push(`malha "${mesh.name || '?'}" (material "${material.name}") sem cor por vértice (COLOR_0) — passe pelo conversor`); continue; }
      let acc = groups.get(key);
      if (!acc) { acc = { pos: [], nor: [], col: key === 'cone' ? null : [], uv: FACADES.includes(key) ? [] : null }; groups.set(key, acc); }
      if (acc.uv && !uv) structural.push(`malha "${mesh.name || '?'}" com material de fachada "${key}" sem uv`);
      tint.set(std.color ?? '#ffffff');
      const at = (k: number) => (index ? index.getX(k) : k);
      const end = Math.min(grp.start + grp.count, total);
      for (let k = grp.start; k + 2 < end; k += 3) {
        const vs = order.map((j) => at(k + j));
        a.fromBufferAttribute(pos, vs[0]).applyMatrix4(m);
        b.fromBufferAttribute(pos, vs[1]).applyMatrix4(m);
        c.fromBufferAttribute(pos, vs[2]).applyMatrix4(m);
        acc.pos.push(a.x, a.y, a.z, b.x, b.y, b.z, c.x, c.y, c.z);
        // Normal do arquivo (girada com o nó); sem ela, a da face.
        if (!nor) n.subVectors(c, b).cross(a.clone().sub(b)).normalize();
        for (const v of vs) {
          if (nor) n.fromBufferAttribute(nor, v).applyMatrix3(nm).normalize();
          acc.nor.push(n.x, n.y, n.z);
          if (acc.col && col) acc.col.push(col.getX(v) * tint.r, col.getY(v) * tint.g, col.getZ(v) * tint.b);
          if (acc.uv && uv) acc.uv.push(uv.getX(v), uv.getY(v));
        }
      }
    }
  });
  if (structural.length) return { model: null, problems: [...new Set(structural)], warnings: [...new Set(warnings)] };
  if (groups.size === 0) return { model: null, problems: ['nenhuma malha no arquivo'], warnings: [...new Set(warnings)] };

  const parts: ModelPart[] = [];
  for (const [mat, acc] of groups) {
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(acc.pos, 3));
    g.setAttribute('normal', new THREE.Float32BufferAttribute(acc.nor, 3));
    if (acc.col) g.setAttribute('color', new THREE.Float32BufferAttribute(acc.col, 3));
    if (acc.uv) g.setAttribute('uv', new THREE.Float32BufferAttribute(acc.uv, 2));
    parts.push({ geometry: g, mat });
  }
  const model: Model = { parts, blob: 0 };
  return { model, problems: [], warnings: [...new Set(warnings)] };
}

/** Lê um .glb (ArrayBuffer) e devolve o modelo do jogo e os problemas, para o lugar do marco. */
export async function parseLandmarkGlb(place: LandmarkDef['place'], data: ArrayBuffer): Promise<GltfLandmark> {
  try {
    const gltf = await new GLTFLoader().parseAsync(data, '');
    return landmarkModelFromGltf(gltf.scene, place);
  } catch (e) {
    return { model: null, problems: [`o arquivo não abre como glTF: ${e instanceof Error ? e.message : String(e)}`], warnings: [] };
  }
}

export interface GltfPart { geometry: Geo | null; problems: string[]; warnings: string[] }

/**
 * Peça baixada (parts.ts) a partir da cena de um .glb: uma parte lisa só (`flat`) — luz, baliza ou fachada numa peça
 * é recusada (o conversor no modo --part põe tudo na parte lisa) —, conferida pelo validador da peça.
 */
export function landmarkPartFromGltf(root: THREE.Object3D, name: string): GltfPart {
  const r = readLandmarkGltf(root);
  if (!r.model) return { geometry: null, problems: r.problems, warnings: r.warnings };
  const problems = r.model.parts.filter((p) => p.mat !== 'flat').map((p) => `material "${p.mat}" numa peça: a peça é uma parte lisa só ("flat"; o conversor no modo --part junta tudo nela)`);
  const flat = r.model.parts.find((p) => p.mat === 'flat');
  if (!flat) problems.push('nenhuma malha lisa ("flat") na peça');
  else problems.push(...checkLandmarkPart(flat.geometry, name));
  return { geometry: problems.length === 0 && flat ? flat.geometry : null, problems, warnings: r.warnings };
}

/** Lê o .glb de uma peça (ArrayBuffer): a geometria e os problemas. */
export async function parseLandmarkPartGlb(name: string, data: ArrayBuffer): Promise<GltfPart> {
  try {
    const gltf = await new GLTFLoader().parseAsync(data, '');
    return landmarkPartFromGltf(gltf.scene, name);
  } catch (e) {
    return { geometry: null, problems: [`o arquivo não abre como glTF: ${e instanceof Error ? e.message : String(e)}`], warnings: [] };
  }
}
