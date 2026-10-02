// Carro em glTF → modelo do jogo (passo 2.4 do roteiro). A convenção é a do briefing de arte:
// - metros, +Y para cima, frente em +Z (padrão do glTF); o jogo usa −Z como frente, então a malha gira
//   180° em Y ao entrar (x → −x, z → −z, exato: preserva o sentido das faces);
// - nós `wheel_fl`, `wheel_fr`, `wheel_rl`, `wheel_rr` (posição e tamanho das rodas, medidos pela malha de
//   cada um) e os vazios `exhaust_l`/`exhaust_r` (saída da chama do nitro); o resto das malhas é o casco;
// - material pelo nome (`paint`, `accent`, `stripe_a`, `stripe_b`, `headlight`, `taillight`, …): o nome
//   diz como a face recebe a cor do carro e se acende; a cor, a rugosidade e o metal das peças fixas vêm
//   do próprio material. Os modelos-base (template.ts) levam o pincel exato nos extras (`nitro`), para a
//   ida e volta sair idêntica.
// O desenho das rodas continua o do estilo (a malha das rodas do .glb só dá posição e tamanho, por ora).
// Puro (three + GLTFLoader, sem DOM): os testes rodam no Node.
import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import type { CarBody } from '../../core/types';
import { checkCarModel } from './check';
import { MeshBuilder, type Brush, type P3 } from './kit';
import type { Axle } from './body';
import type { CarModel } from './model';
import { MODEL_BUILDERS } from './models';

export const WHEEL_NODES = ['wheel_fl', 'wheel_fr', 'wheel_rl', 'wheel_rr'] as const;
export const EXHAUST_NODES = ['exhaust_l', 'exhaust_r'] as const;

/** Pincel guardado nos extras do material (`userData.nitro`): os 11 bytes da malha do jogo. */
export type BrushBytes = [number, number, number, number, number, number, number, number, number, number, number];

export function brushToBytes(b: Brush): BrushBytes {
  const q = (v: number) => Math.max(0, Math.min(255, Math.round(v * 255)));
  return [q(b.rgb[0]), q(b.rgb[1]), q(b.rgb[2]), q(b.paint), q(b.accent), q((b.layer / 3)), q(b.popup), q(b.rough), q(b.metal), q(b.head), q(b.tail)];
}

export function bytesToBrush(v: readonly number[]): Brush {
  const f = (i: number) => v[i] / 255;
  return {
    rgb: [f(0), f(1), f(2)], paint: f(3), accent: f(4), layer: Math.round(f(5) * 3) as 0 | 1 | 2 | 3, popup: f(6),
    rough: f(7), metal: f(8), head: f(9), tail: f(10),
  };
}

/** Nome limpo: minúsculas, sem o sufixo ".001" que o Blender acrescenta. */
export function cleanName(name: string): string {
  return name.toLowerCase().replace(/\.\d+$/, '').trim();
}

/**
 * Como uma face com este material entra no carro. Pelos extras (`nitro`, os modelos-base) ou pelo nome:
 * - `paint*`, `accent*`, `stripe_a*`, `stripe_b*`, `stripe_ab`: recebem a cor do carro; a cor do material é o
 *   TOM (branco = a cor pura, cinza = um tom abaixo);
 * - `headlight*` / `taillight*`: acendem; a força é a do emissivo (emissivo igual à cor = 1; sem emissivo = 1);
 * - "popup" no nome: sobe com o farol escamoteável à noite;
 * - o resto: peça fixa com a cor, a rugosidade e o metal do próprio material.
 */
export function brushForMaterial(material: THREE.Material): Brush {
  const extra = (material.userData as { nitro?: unknown }).nitro;
  if (Array.isArray(extra) && extra.length === 11 && extra.every((n) => typeof n === 'number')) return bytesToBrush(extra);
  const name = cleanName(material.name);
  const std = material as THREE.MeshStandardMaterial;
  const c = std.color ?? new THREE.Color('#808080');
  const base: Brush = {
    rgb: [c.r, c.g, c.b], paint: 0, accent: 0, layer: 0, rough: std.roughness ?? 0.6, metal: std.metalness ?? 0,
    head: 0, tail: 0, popup: name.includes('popup') ? 1 : 0,
  };
  // Força da luz = emissivo ÷ cor (o modelo-base grava emissivo = cor × força).
  const glow = (): number => {
    const e = std.emissive;
    if (!e || Math.max(e.r, e.g, e.b) === 0) return 1;
    const k = Math.max(e.r, e.g, e.b) * (std.emissiveIntensity ?? 1) / Math.max(1e-6, c.r, c.g, c.b);
    return Math.min(1, k);
  };
  if (name.startsWith('stripe_ab')) return { ...base, paint: 1, layer: 3 };
  if (name.startsWith('stripe_a')) return { ...base, paint: 1, layer: 1 };
  if (name.startsWith('stripe_b')) return { ...base, paint: 1, layer: 2 };
  if (name.startsWith('accent')) return { ...base, paint: 1, accent: 1 };
  if (name.startsWith('paint')) return { ...base, paint: 1 };
  if (name.startsWith('headlight')) return { ...base, head: glow() };
  if (name.startsWith('taillight')) return { ...base, tail: glow() };
  return base;
}

interface Part { mesh: THREE.Mesh; under: string | null }

/** Malhas do arquivo e, para cada uma, o nó da convenção (roda ou escape) acima dela, se houver. */
function collect(root: THREE.Object3D): { parts: Part[]; nodes: Map<string, THREE.Object3D> } {
  const nodes = new Map<string, THREE.Object3D>();
  const parts: Part[] = [];
  const known = new Set<string>([...WHEEL_NODES, ...EXHAUST_NODES]);
  const walk = (o: THREE.Object3D, under: string | null) => {
    const n = cleanName(o.name);
    let here = under;
    if (known.has(n) && !nodes.has(n)) { nodes.set(n, o); here = n; }
    if ((o as THREE.Mesh).isMesh) parts.push({ mesh: o as THREE.Mesh, under: here });
    for (const c of o.children) walk(c, here);
  };
  walk(root, null);
  return { parts, nodes };
}

/** glTF (frente +Z) → jogo (frente −Z): giro de 180° em Y. */
const TO_GAME = new THREE.Matrix4().makeScale(-1, 1, -1);

/** Pontos (no referencial do jogo) das malhas sob um nó. */
function gamePoints(parts: Part[], under: string): THREE.Vector3[] {
  const out: THREE.Vector3[] = [];
  for (const p of parts) {
    if (p.under !== under) continue;
    const pos = p.mesh.geometry.getAttribute('position');
    const m = new THREE.Matrix4().multiplyMatrices(TO_GAME, p.mesh.matrixWorld);
    for (let i = 0; i < pos.count; i++) out.push(new THREE.Vector3().fromBufferAttribute(pos, i).applyMatrix4(m));
  }
  return out;
}

export interface GltfCar { model: CarModel | null; problems: string[]; warnings: string[] }

/**
 * Modelo do jogo a partir da cena de um .glb (já com `matrixWorld` atualizada). Devolve `model: null` quando
 * falta o que a convenção exige; os problemas de medida (pegada, rodas, luzes…) vêm do validador comum.
 */
export function carModelFromGltf(body: CarBody, root: THREE.Object3D): GltfCar {
  root.updateMatrixWorld(true);
  const { parts, nodes } = collect(root);
  const missing = WHEEL_NODES.filter((n) => !nodes.has(n) || !parts.some((p) => p.under === n));
  if (missing.length) return { model: null, problems: [`faltam as rodas (nós com malha): ${missing.join(', ')}`], warnings: [] };
  const shellParts = parts.filter((p) => p.under === null);
  if (!shellParts.length) return { model: null, problems: ['nenhuma malha de carroceria (fora dos nós wheel_* e exhaust_*)'], warnings: [] };

  const b = new MeshBuilder();
  const warnings: string[] = [];
  const a = new THREE.Vector3(); const bv = new THREE.Vector3(); const c = new THREE.Vector3();
  for (const part of shellParts) {
    const geo = part.mesh.geometry;
    const pos = geo.getAttribute('position');
    const index = geo.getIndex();
    const m = new THREE.Matrix4().multiplyMatrices(TO_GAME, part.mesh.matrixWorld);
    const mats = Array.isArray(part.mesh.material) ? part.mesh.material : [part.mesh.material];
    const groups = geo.groups.length ? geo.groups : [{ start: 0, count: index ? index.count : pos.count, materialIndex: 0 }];
    // Peça espelhada (escala negativa): a ordem dos vértices inverte, senão a face fica para dentro.
    const flip = m.determinant() < 0;
    for (const grp of groups) {
      const material = mats[grp.materialIndex ?? 0] ?? mats[0];
      if ((material as THREE.MeshStandardMaterial).map) warnings.push(`textura ignorada no material "${material.name}": o carro usa cor por material`);
      const br = brushForMaterial(material);
      const at = (k: number) => (index ? index.getX(k) : k);
      for (let k = grp.start; k + 2 < grp.start + grp.count; k += 3) {
        a.fromBufferAttribute(pos, at(k)).applyMatrix4(m);
        bv.fromBufferAttribute(pos, at(flip ? k + 2 : k + 1)).applyMatrix4(m);
        c.fromBufferAttribute(pos, at(flip ? k + 1 : k + 2)).applyMatrix4(m);
        b.tri([a.x, a.y, a.z], [bv.x, bv.y, bv.z], [c.x, c.y, c.z], br);
      }
    }
  }

  // Rodas (eixo em x): centro e largura pela caixa; o raio é o do vértice mais longe do eixo — a caixa
  // de um pneu de 12 lados mede cos 15° do raio, e o carro afundaria no chão.
  const wheel = (n: string) => {
    const pts = gamePoints(parts, n);
    const box = new THREE.Box3().setFromPoints(pts);
    const ctr = box.getCenter(new THREE.Vector3());
    let r = 0;
    for (const p of pts) r = Math.max(r, Math.hypot(p.y - ctr.y, p.z - ctr.z));
    return { z: ctr.z, x: Math.abs(ctr.x), r, w: box.max.x - box.min.x };
  };
  const axle = (l: string, r: string): Axle => {
    const p = wheel(l); const q = wheel(r);
    return { z: (p.z + q.z) / 2, x: (p.x + q.x) / 2, r: (p.r + q.r) / 2, w: (p.w + q.w) / 2 };
  };
  const front = axle('wheel_fl', 'wheel_fr');
  const rear = axle('wheel_rl', 'wheel_rr');

  // O estilo dá o desenho da roda, a ordem das pinturas e o escape quando o arquivo não traz nenhum.
  const style = MODEL_BUILDERS[body]();
  const exhausts: P3[] = [];
  for (const n of EXHAUST_NODES) {
    const o = nodes.get(n);
    if (!o) continue;
    const p = new THREE.Vector3().setFromMatrixPosition(o.matrixWorld).applyMatrix4(TO_GAME);
    exhausts.push([p.x, p.y, p.z]);
  }

  const shell = b.build();
  const model: CarModel = {
    body, shell, wheel: style.wheel, axles: [front, rear], exhausts: exhausts.length ? exhausts : style.exhausts,
    height: shell.boundingBox?.max.y ?? 0, liveries: style.liveries, triangles: b.triangles,
  };
  return { model, problems: checkCarModel(model), warnings: [...new Set(warnings)] };
}

/** Lê um .glb (ArrayBuffer) e devolve o modelo do jogo e os problemas. */
export async function parseCarGlb(body: CarBody, data: ArrayBuffer): Promise<GltfCar> {
  try {
    const gltf = await new GLTFLoader().parseAsync(data, '');
    return carModelFromGltf(body, gltf.scene);
  } catch (e) {
    return { model: null, problems: [`o arquivo não abre como glTF: ${e instanceof Error ? e.message : String(e)}`], warnings: [] };
  }
}
