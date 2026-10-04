// Modelo "realista" sintético para provar o conversor de marcos sem baixar nada (tests/landmark-gltf.test.ts e
// `node tools/convert-landmark.mjs --make-synthetic`): a estátua de uma ave pernalta num pedestal de pedra sobre um
// gramado, com o lampião aceso ao lado — ~38 mil triângulos (detalhe 1), malhas separadas com transformação nos nós,
// uv num atlas e uma textura com ruído (penas, pedra salpicada, grama), como sai de um gerador de 3D. Convenção do
// glTF: +Y para cima, frente em +Z (o bico aponta para +Z), unidades ~metros (11 de altura). Puro (three, sem DOM).
import * as THREE from 'three';

/** Cores-base da textura (sRGB): o teste confere que o bico laranja vira a frente (+X) do marco. */
export const SYNTH = {
  body: '#f2efe6', neck: '#1c1c1c', collar: '#c8202a', beak: '#f08a1c', leg: '#3a3030', stone: '#8f8a80', grass: '#4f8a34', lamp: '#ffd23f',
} as const;
const REGIONS = ['body', 'neck', 'collar', 'beak', 'leg', 'stone', 'grass', 'lamp'] as const;
type Region = (typeof REGIONS)[number];

const TEX = 256;

function hash(x: number, y: number, s: number): number {
  let h = (x * 374761393 + y * 668265263 + s * 2246822519) | 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
}

/** Atlas 256 × 256: uma faixa de 32 linhas por região, a cor-base com ruído de foto (±8%), manchas e riscos. */
export function syntheticPixels(): { width: number; height: number; data: Uint8Array } {
  const data = new Uint8Array(TEX * TEX * 4);
  for (let y = 0; y < TEX; y++) {
    const region = REGIONS[Math.floor(y / 32)];
    const base = new THREE.Color(SYNTH[region]);
    const srgb = { r: 0, g: 0, b: 0 };
    base.getRGB(srgb, THREE.SRGBColorSpace);
    for (let x = 0; x < TEX; x++) {
      let f = 1 + (hash(x, y, 1) - 0.5) * 0.16;
      if (region === 'body' && y % 6 === 0) f *= 0.86; // riscos das penas
      if (region === 'stone' && hash(x >> 1, y >> 1, 2) < 0.15) f *= 0.72; // pedra salpicada
      if (region === 'grass' && hash(x, y >> 2, 3) < 0.25) f *= 1.25; // lâminas claras
      const o = (y * TEX + x) * 4;
      data[o] = Math.min(255, Math.round(srgb.r * 255 * f));
      data[o + 1] = Math.min(255, Math.round(srgb.g * 255 * f));
      data[o + 2] = Math.min(255, Math.round(srgb.b * 255 * f));
      data[o + 3] = 255;
    }
  }
  return { width: TEX, height: TEX, data };
}

/** uv da peça para a faixa da região no atlas (linha 0 = v 0: flipY = false, como o glTF). */
function toRegion(g: THREE.BufferGeometry, region: Region): THREE.BufferGeometry {
  const uv = g.getAttribute('uv') as THREE.BufferAttribute;
  const r = REGIONS.indexOf(region);
  for (let i = 0; i < uv.count; i++) uv.setXY(i, 0.02 + uv.getX(i) * 0.96, (r + 0.1 + uv.getY(i) * 0.8) / 8);
  return g;
}

/** A estátua; `detail` multiplica os segmentos (1 ≈ 38 mil triângulos; 0,4 ≈ 6 mil). */
export function syntheticLandmark(detail = 1): THREE.Group {
  const seg = (n: number) => Math.max(3, Math.round(n * detail));
  const px = syntheticPixels();
  const map = new THREE.DataTexture(px.data, px.width, px.height, THREE.RGBAFormat);
  map.colorSpace = THREE.SRGBColorSpace;
  map.needsUpdate = true;
  const skin = new THREE.MeshStandardMaterial({ name: 'statue_baseColor', map, roughness: 0.8 });
  const lampMat = new THREE.MeshStandardMaterial({ name: 'lamp_emissive', map, emissive: new THREE.Color(SYNTH.lamp), emissiveIntensity: 2 });
  const root = new THREE.Group();
  root.name = 'Meshy_statue';
  const statue = new THREE.Group();
  statue.name = 'statue';
  statue.scale.setScalar(0.5); // o arquivo vem fora de escala (o conversor ajusta a altura)
  root.add(statue);
  const add = (name: string, g: THREE.BufferGeometry, region: Region, m: THREE.Matrix4, mat = skin, at?: THREE.Vector3) => {
    const mesh = new THREE.Mesh(toRegion(g, region).applyMatrix4(m), mat);
    mesh.name = name;
    if (at) mesh.position.copy(at);
    statue.add(mesh);
  };
  const T = (x: number, y: number, z: number) => new THREE.Matrix4().makeTranslation(x, y, z);
  const TS = (x: number, y: number, z: number, sx: number, sy: number, sz: number) => T(x, y, z).multiply(new THREE.Matrix4().makeScale(sx, sy, sz));
  // Gramado (mais comprido na direção do bico) e pedestal de pedra.
  add('grass', new THREE.CylinderGeometry(4.8, 5, 0.8, seg(96), 2), 'grass', TS(0, 0.4, 0.8, 1, 1, 1.3));
  add('pedestal', new THREE.CylinderGeometry(4, 4.4, 4, seg(96), seg(8)), 'stone', T(0, 2.8, 0));
  // Pernas, corpo, asas e cauda.
  for (const x of [-1, 1]) add(`leg_${x}`, new THREE.CylinderGeometry(0.3, 0.3, 4, seg(24), seg(8)), 'leg', T(x * 1.1, 6.8, 0));
  add('body', new THREE.SphereGeometry(1, seg(128), seg(80)), 'body', TS(0, 10.8, 0, 3.2, 2.6, 5.2));
  for (const x of [-1, 1]) add(`wing_${x}`, new THREE.SphereGeometry(1, seg(48), seg(32)), 'body', TS(x * 2.9, 11.4, -0.8, 0.7, 1.8, 4.2));
  add('tail', new THREE.ConeGeometry(1.2, 3, seg(32), seg(4)), 'neck', T(0, 0, 0).multiply(new THREE.Matrix4().makeRotationX(-Math.PI / 2)), skin, new THREE.Vector3(0, 11, -6.2));
  // Pescoço preto com o colar vermelho, cabeça e o bico laranja apontando para +Z (a frente).
  const neck = new THREE.CylinderGeometry(0.9, 1.1, 5, seg(48), seg(16));
  add('neck', neck, 'neck', T(0, 0, 0).multiply(new THREE.Matrix4().makeRotationX(0.45)), skin, new THREE.Vector3(0, 14.2, 4.2));
  add('collar', new THREE.CylinderGeometry(1.3, 1.4, 1, seg(48), 2), 'collar', new THREE.Matrix4().makeRotationX(0.45), skin, new THREE.Vector3(0, 12.6, 3.5));
  add('head', new THREE.SphereGeometry(1.4, seg(64), seg(48)), 'neck', T(0, 17, 5.4));
  add('beak', new THREE.ConeGeometry(0.6, 5, seg(48), seg(8)), 'beak', new THREE.Matrix4().makeRotationX(Math.PI / 2), skin, new THREE.Vector3(0, 16.8, 9.2));
  // Lampião aceso ao lado do pedestal.
  add('lamp_post', new THREE.CylinderGeometry(0.2, 0.25, 4, seg(16), 2), 'leg', T(3.4, 2.8, 3.4));
  add('lamp', new THREE.SphereGeometry(0.8, seg(32), seg(16)), 'lamp', T(3.4, 5.4, 3.4), lampMat);
  root.updateMatrixWorld(true);
  return root;
}
