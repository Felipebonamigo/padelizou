// Marco do jogo → cena glTF na convenção de gltf.ts (a mesma do jogo: metros, +Y, frente em +X): uma malha por parte,
// com o material nomeado pela parte (flat, glow, …), cor branca e a cor de verdade nos vértices (COLOR_0); uv só nas
// fachadas. É o que o conversor grava e o que a ida e volta do teste exporta (tests/landmark-gltf.test.ts).
// Só para ferramentas e testes: o jogo não importa este arquivo. Exportar: `exportGlb` (cars/template.ts).
import * as THREE from 'three';
import type { MatKey, Model } from '../geom';

const FACADES: readonly MatKey[] = ['office', 'apartment', 'classic', 'house'];

export function landmarkScene(model: Model, name = 'marco'): THREE.Group {
  const root = new THREE.Group();
  root.name = `landmark_${name}`;
  for (const part of model.parts) {
    const src = part.geometry.index ? part.geometry.toNonIndexed() : part.geometry;
    const g = new THREE.BufferGeometry();
    for (const k of ['position', 'normal', 'color', ...(FACADES.includes(part.mat) ? ['uv'] : [])]) {
      const a = src.getAttribute(k);
      if (a) g.setAttribute(k, a);
    }
    const color = g.getAttribute('color') !== undefined;
    const mat = new THREE.MeshStandardMaterial({ color: '#ffffff', vertexColors: color, roughness: 0.86, metalness: 0, flatShading: true });
    mat.name = part.mat;
    const mesh = new THREE.Mesh(g, mat);
    mesh.name = part.mat;
    root.add(mesh);
  }
  root.updateMatrixWorld(true);
  return root;
}
