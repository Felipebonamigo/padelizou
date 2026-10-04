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

// ───────────────────────── Bicho sintético (para as peças: tests/landmark-parts.test.ts) ─────────────────────────
// Um quadrúpede "realista" de prova para o modo peça do conversor, sem baixar nada: o boi (que a docs/ARTE.md manda
// recolorir de búfalo) e a girafa de manchas, ~25 mil triângulos no detalhe 1, malhas separadas com transformação
// nos nós, uv num atlas e textura com ruído. Convenção do glTF: +Y para cima, a cabeça para +Z (a frente), fora de
// escala (o nó vem × 0,5: o conversor ajusta a medida).

/** Cores-base (sRGB) de cada bicho: couro (com as manchas da girafa), barriga, chifre e casco. */
export const SYNTH_ANIMAL = {
  boi: { hide: '#8a6448', spot: '#8a6448', belly: '#e0d4bc', horn: '#ece2cc', hoof: '#2a2420' },
  girafa: { hide: '#e8c890', spot: '#9a5226', belly: '#f2e2c0', horn: '#4a3020', hoof: '#2a2018' },
} as const;
export type SynthAnimal = keyof typeof SYNTH_ANIMAL;
const ANIMAL_REGIONS = ['hide', 'belly', 'horn', 'hoof'] as const;
type AnimalRegion = (typeof ANIMAL_REGIONS)[number];

/** Atlas 256 × 256 do bicho: 4 faixas de 64 linhas, a cor-base com ruído de foto (±8%) e as manchas no couro. */
function animalPixels(kind: SynthAnimal): { width: number; height: number; data: Uint8Array } {
  const data = new Uint8Array(TEX * TEX * 4);
  const pal = SYNTH_ANIMAL[kind];
  const srgb = (hex: string) => { const o = { r: 0, g: 0, b: 0 }; new THREE.Color(hex).getRGB(o, THREE.SRGBColorSpace); return o; };
  for (let y = 0; y < TEX; y++) {
    const region = ANIMAL_REGIONS[Math.floor(y / 64)];
    for (let x = 0; x < TEX; x++) {
      const spot = region === 'hide' && hash(x >> 3, y >> 3, 7) < 0.45;
      const c = srgb(spot ? pal.spot : pal[region]);
      const f = 1 + (hash(x, y, 4) - 0.5) * 0.16;
      const o = (y * TEX + x) * 4;
      data[o] = Math.min(255, Math.round(c.r * 255 * f));
      data[o + 1] = Math.min(255, Math.round(c.g * 255 * f));
      data[o + 2] = Math.min(255, Math.round(c.b * 255 * f));
      data[o + 3] = 255;
    }
  }
  return { width: TEX, height: TEX, data };
}

function toAnimalRegion(g: THREE.BufferGeometry, region: AnimalRegion): THREE.BufferGeometry {
  const uv = g.getAttribute('uv') as THREE.BufferAttribute;
  const r = ANIMAL_REGIONS.indexOf(region);
  for (let i = 0; i < uv.count; i++) uv.setXY(i, 0.02 + uv.getX(i) * 0.96, (r + 0.1 + uv.getY(i) * 0.8) / 4);
  return g;
}

/** O bicho sintético; `detail` multiplica os segmentos (1 ≈ 25 mil triângulos). */
export function syntheticAnimal(kind: SynthAnimal, detail = 1): THREE.Group {
  const seg = (n: number) => Math.max(3, Math.round(n * detail));
  const px = animalPixels(kind);
  const map = new THREE.DataTexture(px.data, px.width, px.height, THREE.RGBAFormat);
  map.colorSpace = THREE.SRGBColorSpace;
  map.needsUpdate = true;
  const skin = new THREE.MeshStandardMaterial({ name: `${kind}_baseColor`, map, roughness: 0.85 });
  const root = new THREE.Group();
  root.name = `Meshy_${kind}`;
  const body = new THREE.Group();
  body.name = kind;
  body.scale.setScalar(0.5);
  root.add(body);
  const Y = new THREE.Vector3(0, 1, 0);
  const add = (name: string, g: THREE.BufferGeometry, region: AnimalRegion, m: THREE.Matrix4) => {
    const mesh = new THREE.Mesh(toAnimalRegion(g, region).applyMatrix4(m), skin);
    mesh.name = name;
    body.add(mesh);
  };
  const TS = (x: number, y: number, z: number, sx = 1, sy = 1, sz = 1) => new THREE.Matrix4().compose(new THREE.Vector3(x, y, z), new THREE.Quaternion(), new THREE.Vector3(sx, sy, sz));
  const V = (x: number, y: number, z: number) => new THREE.Vector3(x, y, z);
  /** A peça de altura 1 do three (cilindro, cone) esticada de a até b. */
  const limb = (a: THREE.Vector3, b: THREE.Vector3) => {
    const d = b.clone().sub(a);
    return new THREE.Matrix4().compose(a.clone().add(b).multiplyScalar(0.5), new THREE.Quaternion().setFromUnitVectors(Y, d.clone().normalize()), new THREE.Vector3(1, d.length(), 1));
  };
  const giraffe = kind === 'girafa';
  const legH = giraffe ? 1.9 : 0.75; const bodyY = legH + (giraffe ? 0.45 : 0.4);
  // Corpo e barriga, quatro pernas com casco, rabo.
  add('body', new THREE.SphereGeometry(1, seg(96), seg(64)), 'hide', TS(0, bodyY, 0, 0.45, giraffe ? 0.55 : 0.5, 1.0));
  add('belly', new THREE.SphereGeometry(1, seg(48), seg(24)), 'belly', TS(0, bodyY - 0.25, 0, 0.4, 0.25, 0.8));
  for (const x of [-0.25, 0.25]) for (const z of [-0.62, 0.62]) {
    add(`leg_${x}_${z}`, new THREE.CylinderGeometry(giraffe ? 0.07 : 0.09, giraffe ? 0.06 : 0.08, 1, seg(24), seg(8)), 'hide', limb(V(x, 0.06, z), V(x, legH + 0.2, z)));
    add(`hoof_${x}_${z}`, new THREE.CylinderGeometry(0.1, 0.11, 0.12, seg(16), 1), 'hoof', TS(x, 0.06, z));
  }
  add('tail', new THREE.CylinderGeometry(0.03, 0.05, 1, seg(12), seg(4)), 'hide', limb(V(0, bodyY + 0.2, -0.95), V(0, bodyY - 0.6, -1.15)));
  // Pescoço, cabeça e focinho para +Z (a frente); chifres (boi) ou ossicones (girafa).
  const neckTop = giraffe ? V(0, bodyY + 2.1, 1.45) : V(0, bodyY + 0.15, 1.3);
  add('neck', new THREE.CylinderGeometry(giraffe ? 0.17 : 0.24, 0.3, 1, seg(48), seg(16)), 'hide', limb(V(0, bodyY + 0.2, 0.75), neckTop));
  const head = neckTop.clone().add(V(0, 0.05, 0.25));
  add('head', new THREE.SphereGeometry(1, seg(64), seg(48)), 'hide', TS(head.x, head.y, head.z, 0.19, 0.2, 0.36));
  add('muzzle', new THREE.SphereGeometry(1, seg(32), seg(24)), giraffe ? 'belly' : 'hoof', TS(head.x, head.y - 0.06, head.z + 0.3, 0.13, 0.12, 0.12));
  for (const x of [-1, 1]) {
    if (giraffe) add(`ossicone_${x}`, new THREE.CylinderGeometry(0.03, 0.04, 0.28, seg(12), 2), 'horn', TS(head.x + x * 0.08, head.y + 0.28, head.z - 0.12));
    else add(`horn_${x}`, new THREE.ConeGeometry(0.06, 1, seg(16), seg(4)), 'horn', limb(V(head.x + x * 0.15, head.y + 0.15, head.z - 0.1), V(head.x + x * 0.55, head.y + 0.35, head.z - 0.2)));
  }
  root.updateMatrixWorld(true);
  return root;
}
