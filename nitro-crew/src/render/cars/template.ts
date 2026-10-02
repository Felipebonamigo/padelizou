// Modelo-base de cada estilo para o artista: o carro procedural do jogo na convenção do briefing de
// arte (gltf.ts), pronto para abrir no Blender e redesenhar na escala, no pivô e com os nomes certos.
// - uma malha por material, sob o nó `body`; o material leva o nome da convenção e, nos extras
//   (`nitro`), o pincel exato do jogo (a ida e volta sai idêntica: tests/car-gltf.test.ts);
// - as 4 rodas (`wheel_fl`…), cada uma com o pivô no centro, e os vazios `exhaust_l`/`exhaust_r`;
// - frente em +Z e esquerda em +X (glTF); o jogo usa −Z como frente.
// Só para scripts e testes (scripts/export-car-templates.ts): o jogo não importa este arquivo.
import * as THREE from 'three';
import { GLTFExporter } from 'three/examples/jsm/exporters/GLTFExporter.js';
import type { CarBody } from '../../core/types';
import { brushToBytes, bytesToBrush, type BrushBytes } from './gltf';
import type { Brush } from './kit';
import { MODEL_BUILDERS } from './models';
import * as P from './paints';
import { buildWheel } from './wheels';

/** Jogo (frente −Z) ↔ glTF (frente +Z): x → −x, z → −z. */
const FLIP = new THREE.Matrix4().makeScale(-1, 1, -1);

/** Nome da convenção para os pincéis do jogo; os outros viram "part_<n>" (a cor e o material vão no arquivo). */
const NAMES: ReadonlyArray<readonly [Brush, string]> = [
  [P.PAINT, 'paint'], [P.PAINT_SHADE, 'paint_shade'], [P.ACCENT, 'accent'],
  [P.STRIPE_A, 'stripe_a'], [P.STRIPE_B, 'stripe_b'], [P.STRIPE_AB, 'stripe_ab'],
  [P.GLASS, 'glass'], [P.GLASS_DARK, 'glass_dark'], [P.TRIM, 'trim'], [P.TRIM_SOFT, 'trim_soft'], [P.UNDER, 'trim_under'],
  [P.GRILLE, 'trim_grille'], [P.CARBON, 'trim_carbon'], [P.CHROME, 'chrome'], [P.ALU, 'rim'], [P.GUNMETAL, 'rim_gunmetal'],
  [P.HEAD, 'headlight'], [P.DRL, 'headlight_drl'], [P.AMBER, 'headlight_amber'], [P.HEAD_HOUSING, 'chrome_housing'],
  [P.TAIL, 'taillight'], [P.TAIL_DIM, 'taillight_dim'], [P.REVERSE, 'taillight_reverse'],
  [P.TIRE, 'tire'], [P.TIRE_SIDE, 'tire_side'], [P.WHITEWALL, 'tire_whitewall'],
  [P.INTERIOR, 'interior'], [P.SEAT, 'interior_seat'], [P.PLATE, 'plate'], [P.WHITE, 'white'], [P.MUD, 'mud'],
];

const key = (b: BrushBytes) => b.join(',');
const NAME_BY_KEY = new Map(NAMES.map(([b, n]) => [key(brushToBytes(b)), n]));

/** Prefixo da convenção para um pincel sem nome próprio: o nome é o que diz ao jogo como a face se comporta. */
function kindOf(b: BrushBytes): string {
  const [, , , paint, accent, layer, , , , head, tail] = b;
  if (paint > 0) {
    if (accent > 0) return 'accent';
    const l = Math.round((layer / 255) * 3);
    return l === 1 ? 'stripe_a' : l === 2 ? 'stripe_b' : l === 3 ? 'stripe_ab' : 'paint';
  }
  if (head > 0) return 'headlight';
  if (tail > 0) return 'taillight';
  return 'part';
}

/** Bytes do pincel do vértice `i` da malha do jogo (color, aPaint, aMat). */
function vertexBytes(g: THREE.BufferGeometry, i: number): BrushBytes {
  const q = (v: number) => Math.round(v * 255);
  const c = g.getAttribute('color'); const p = g.getAttribute('aPaint'); const m = g.getAttribute('aMat');
  return [q(c.getX(i)), q(c.getY(i)), q(c.getZ(i)), q(p.getX(i)), q(p.getY(i)), q(p.getZ(i)), q(p.getW(i)), q(m.getX(i)), q(m.getY(i)), q(m.getZ(i)), q(m.getW(i))];
}

/** Material do glTF para um pincel: cor e acabamento para ver no Blender, o pincel exato nos extras. */
function material(bytes: BrushBytes, name: string): THREE.MeshStandardMaterial {
  const b = bytesToBrush(bytes);
  const color = new THREE.Color().setRGB(b.rgb[0], b.rgb[1], b.rgb[2], THREE.LinearSRGBColorSpace);
  const mat = new THREE.MeshStandardMaterial({ color, roughness: b.rough, metalness: b.metal });
  const glow = Math.max(b.head, b.tail);
  if (glow > 0) { mat.emissive.copy(color); mat.emissiveIntensity = glow; }
  mat.name = name;
  mat.userData = { nitro: bytes };
  return mat;
}

/**
 * Uma malha por pincel, com `matrix` aplicada às posições (do referencial do jogo para o do arquivo).
 * Os nomes de material saem de `names` (compartilhado entre casco e rodas: o mesmo pincel, o mesmo nome).
 */
function split(g: THREE.BufferGeometry, matrix: THREE.Matrix4, names: Map<string, string>, mirror = false): THREE.Mesh[] {
  const pos = g.getAttribute('position');
  const byKey = new Map<string, { bytes: BrushBytes; pts: number[] }>();
  const v = new THREE.Vector3();
  for (let i = 0; i < pos.count; i += 3) {
    const bytes = vertexBytes(g, i);
    const k = key(bytes);
    let bucket = byKey.get(k);
    if (!bucket) { bucket = { bytes, pts: [] }; byKey.set(k, bucket); }
    // Espelhado: a ordem dos vértices inverte para a face continuar para fora.
    for (const j of mirror ? [0, 2, 1] : [0, 1, 2]) {
      v.fromBufferAttribute(pos, i + j).applyMatrix4(matrix);
      bucket.pts.push(v.x, v.y, v.z);
    }
  }
  const meshes: THREE.Mesh[] = [];
  for (const [k, { bytes, pts }] of byKey) {
    let name = names.get(k);
    if (!name) {
      // Sem os extras (o Blender não exporta no padrão), o nome é o que sobra: a convenção do briefing,
      // e "popup" para o que sobe com o farol escamoteável.
      name = NAME_BY_KEY.get(k) ?? `${kindOf(bytes)}_${names.size + 1}`;
      if (bytes[6] > 0 && !name.includes('popup')) name = `${name}_popup`;
      names.set(k, name);
    }
    const geo = new THREE.BufferGeometry();
    geo.setAttribute('position', new THREE.Float32BufferAttribute(pts, 3));
    geo.computeVertexNormals();
    const mesh = new THREE.Mesh(geo, material(bytes, name));
    mesh.name = name;
    meshes.push(mesh);
  }
  return meshes;
}

/** O carro do estilo como cena glTF na convenção (ver o cabeçalho). */
export function carTemplateScene(body: CarBody): THREE.Group {
  const m = MODEL_BUILDERS[body]();
  const names = new Map<string, string>();
  const root = new THREE.Group();
  root.name = `car_${body}`;
  const shell = new THREE.Group();
  shell.name = 'body';
  for (const mesh of split(m.shell, FLIP, names)) shell.add(mesh);
  root.add(shell);

  // Rodas: a do estilo, na escala do eixo, a da esquerda espelhada; o nó fica no centro da roda.
  const unit = buildWheel(m.wheel).geometry;
  const [front, rear] = m.axles;
  const wheels: ReadonlyArray<readonly [string, typeof front, number]> = [
    ['wheel_fl', front, -1], ['wheel_fr', front, 1], ['wheel_rl', rear, -1], ['wheel_rr', rear, 1],
  ];
  for (const [name, a, side] of wheels) {
    const node = new THREE.Group();
    node.name = name;
    // Centro no jogo: (lado × x, r, z) → no arquivo: (−lado × x, r, −z).
    node.position.set(-side * a.x, a.r, -a.z);
    // Escala da roda (e o espelho da esquerda) no referencial do jogo, depois o giro para o do arquivo.
    const shape = new THREE.Matrix4().makeScale(side * a.w, a.r, a.r).premultiply(FLIP);
    for (const mesh of split(unit, shape, names, side < 0)) node.add(mesh);
    root.add(node);
  }

  // Escapes, da esquerda para a direita do carro.
  const ex = [...m.exhausts].sort((p, q) => p[0] - q[0]);
  ex.forEach((e, i) => {
    const o = new THREE.Object3D();
    o.name = i === 0 ? 'exhaust_l' : 'exhaust_r';
    o.position.set(-e[0], e[1], -e[2]);
    root.add(o);
  });
  root.updateMatrixWorld(true);
  return root;
}

/** Cena → .glb (ArrayBuffer). No Node precisa de um FileReader (scripts/lib/node-filereader.ts). */
export async function exportGlb(root: THREE.Object3D): Promise<ArrayBuffer> {
  const out = await new GLTFExporter().parseAsync(root, { binary: true });
  if (!(out instanceof ArrayBuffer)) throw new Error('o exportador não devolveu um .glb');
  return out;
}
