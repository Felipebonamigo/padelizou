// Kit de modelagem low-poly do cenário: primitivas com cor por vértice, transformação, fusão e
// deformação determinística. Puro (só Three, sem DOM): os modelos podem ser montados e medidos nos
// testes. Toda geometria sai não indexada (faces planas: o material usa flatShading) com os atributos
// position, normal e color — e uv só quando pedida (fachadas, painéis).
import * as THREE from 'three';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';
import { hash3 } from '../noise';

export type Geo = THREE.BufferGeometry;

/**
 * Material de cada parte (scenery.ts cria um de cada): `flat` é o geral (cor por vértice, faces planas);
 * as fachadas têm textura de janelas que acende à noite; `glow` é luz pintada (brilha à noite);
 * `panel` é o atlas de outdoors e placas; `cone` é o facho de luz aditivo (só à noite).
 */
export type MatKey = 'flat' | 'office' | 'apartment' | 'classic' | 'house' | 'glow' | 'beacon' | 'cone' | 'panel';

export interface ModelPart {
  geometry: Geo;
  mat: MatKey;
  /** Projeta sombra (só o que fica perto da pista: o cenário distante usa a mancha). */
  shadow?: boolean;
}

/** Um modelo = uma ou mais partes, cada uma vira um InstancedMesh (uma chamada de desenho por parte). */
export interface Model {
  parts: ModelPart[];
  /** Raio (m, escala 1) da mancha de sombra no chão; 0 ou ausente = sem mancha. */
  blob?: number;
}

const tmpQ = new THREE.Quaternion();
const tmpE = new THREE.Euler();
const tmpP = new THREE.Vector3();
const tmpS = new THREE.Vector3();

/** Matriz de posição, escala e giro (ordem YXZ: primeiro o rumo, depois inclinações). */
export function tf(x: number, y: number, z: number, sx = 1, sy = 1, sz = 1, rx = 0, ry = 0, rz = 0): THREE.Matrix4 {
  tmpQ.setFromEuler(tmpE.set(rx, ry, rz, 'YXZ'));
  return new THREE.Matrix4().compose(tmpP.set(x, y, z), tmpQ, tmpS.set(sx, sy, sz));
}

/**
 * Posição + inclinação: o eixo +Y da peça tomba `tilt` radianos (a partir da vertical) na direção `a`
 * (radianos no plano XZ: 0 = +X, π/2 = +Z). Escala antes do giro.
 */
export function lean(x: number, y: number, z: number, a: number, tilt: number, sx = 1, sy = 1, sz = 1): THREE.Matrix4 {
  return tf(x, y, z, sx, sy, sz, 0, -a, -tilt);
}

/** Cópia não indexada com cor por vértice (e a transformação aplicada). Descarta a uv, salvo `keepUv`. */
export function paint(geo: Geo, color: THREE.ColorRepresentation, m?: THREE.Matrix4, keepUv = false): Geo {
  const g = geo.index ? geo.toNonIndexed() : geo.clone();
  geo.dispose();
  if (!keepUv) g.deleteAttribute('uv');
  if (m) g.applyMatrix4(m);
  if (!g.getAttribute('normal')) g.computeVertexNormals();
  const c = new THREE.Color(color);
  const n = g.attributes.position.count;
  const arr = new Float32Array(n * 3);
  for (let i = 0; i < n; i++) { arr[i * 3] = c.r; arr[i * 3 + 1] = c.g; arr[i * 3 + 2] = c.b; }
  g.setAttribute('color', new THREE.BufferAttribute(arr, 3));
  return g;
}

/** Funde as partes (e libera cada uma). Normais por face: a malha não compartilha vértices. */
export function merge(parts: Geo[]): Geo {
  const g = mergeGeometries(parts, false);
  if (!g) throw new Error('fusão de geometria falhou (atributos diferentes entre as partes?)');
  for (const p of parts) p.dispose();
  g.computeVertexNormals();
  return g;
}

/**
 * Deforma radialmente por hash da posição arredondada: vértices coincidentes recebem o mesmo desvio,
 * então a malha não abre. `amount` é a fração do raio (0,15 = ±15%).
 */
export function jitter(geo: Geo, amount: number, seed: number, keepBottom = false): Geo {
  const p = geo.attributes.position as THREE.BufferAttribute;
  for (let i = 0; i < p.count; i++) {
    const x = p.getX(i); const y = p.getY(i); const z = p.getZ(i);
    const k = hash3(Math.round(x * 97) + seed * 7919, Math.round(y * 97) + seed * 131, Math.round(z * 97));
    const f = 1 + (k - 0.5) * 2 * amount;
    p.setXYZ(i, x * f, keepBottom && y < 0 ? y : y * f, z * f);
  }
  return geo;
}

/** Multiplica a cor pela altura: `f0` em y0, `f1` em y1 (oclusão no pé, luz no topo). */
export function shadeY(geo: Geo, y0: number, y1: number, f0: number, f1: number): Geo {
  const p = geo.attributes.position as THREE.BufferAttribute;
  const c = geo.attributes.color as THREE.BufferAttribute;
  for (let i = 0; i < p.count; i++) {
    const t = Math.max(0, Math.min(1, (p.getY(i) - y0) / (y1 - y0)));
    const f = f0 + (f1 - f0) * t;
    c.setXYZ(i, c.getX(i) * f, c.getY(i) * f, c.getZ(i) * f);
  }
  return geo;
}

/** Pinta de `color` as faces voltadas para cima (normal.y > limiar): neve, musgo, grama no topo. */
export function tintUp(geo: Geo, color: THREE.ColorRepresentation, threshold: number, mixAmount = 1): Geo {
  const p = geo.attributes.position as THREE.BufferAttribute;
  const c = geo.attributes.color as THREE.BufferAttribute;
  const top = new THREE.Color(color);
  const a = new THREE.Vector3(); const b = new THREE.Vector3(); const d = new THREE.Vector3();
  for (let i = 0; i + 2 < p.count; i += 3) {
    a.fromBufferAttribute(p, i); b.fromBufferAttribute(p, i + 1); d.fromBufferAttribute(p, i + 2);
    b.sub(a); d.sub(a);
    const n = b.cross(d).normalize();
    if (n.y <= threshold) continue;
    for (let k = 0; k < 3; k++) {
      const j = i + k;
      c.setXYZ(j, c.getX(j) + (top.r - c.getX(j)) * mixAmount, c.getY(j) + (top.g - c.getY(j)) * mixAmount, c.getZ(j) + (top.b - c.getZ(j)) * mixAmount);
    }
  }
  return geo;
}

/** Varia o tom de cada face (±amount) por hash: dá textura às copas e pedras sem textura. */
export function speckle(geo: Geo, amount: number, seed: number): Geo {
  const c = geo.attributes.color as THREE.BufferAttribute;
  for (let i = 0; i + 2 < c.count; i += 3) {
    const f = 1 + (hash3(i, seed, 17) - 0.5) * 2 * amount;
    for (let k = 0; k < 3; k++) c.setXYZ(i + k, c.getX(i + k) * f, c.getY(i + k) * f, c.getZ(i + k) * f);
  }
  return geo;
}

export function box(w: number, h: number, d: number): Geo { return new THREE.BoxGeometry(w, h, d); }
export function cyl(rt: number, rb: number, h: number, seg: number, open = false): Geo { return new THREE.CylinderGeometry(rt, rb, h, seg, 1, open); }
export function cone(r: number, h: number, seg: number): Geo { return new THREE.ConeGeometry(r, h, seg); }
export function ico(r: number, detail = 0): Geo { return new THREE.IcosahedronGeometry(r, detail); }
export function dodeca(r: number): Geo { return new THREE.DodecahedronGeometry(r, 0); }
export function sphere(r: number, w: number, h: number, phi0 = 0, phiLen = Math.PI * 2, theta0 = 0, thetaLen = Math.PI): Geo { return new THREE.SphereGeometry(r, w, h, phi0, phiLen, theta0, thetaLen); }

/** Geometria a partir de uma lista de triângulos (x,y,z × 3 por triângulo), sem índice. */
export function tris(positions: number[]): Geo {
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
  return g;
}

/** Prisma triangular (telhado de duas águas): base `w` em X, altura `h`, comprimento `d` em Z. */
export function gable(w: number, h: number, d: number, overhang = 0): Geo {
  const x = w / 2 + overhang; const z = d / 2 + overhang;
  const p = [
    // Águas (a de +X e a de −X).
    x, 0, z, x, 0, -z, 0, h, -z, x, 0, z, 0, h, -z, 0, h, z,
    -x, 0, -z, -x, 0, z, 0, h, z, -x, 0, -z, 0, h, z, 0, h, -z,
    // Oitões.
    -x, 0, z, x, 0, z, 0, h, z,
    x, 0, -z, -x, 0, -z, 0, h, -z,
    // Fundo.
    -x, 0, -z, x, 0, -z, x, 0, z, -x, 0, -z, x, 0, z, -x, 0, z,
  ];
  return tris(p);
}

/** Pirâmide de base retangular (telhado de quatro águas / torre). */
export function hip(w: number, h: number, d: number, topW = 0, topD = 0): Geo {
  const g = new THREE.CylinderGeometry(Math.SQRT1_2, Math.SQRT1_2, 1, 4, 1, false);
  g.rotateY(Math.PI / 4);
  g.translate(0, 0.5, 0);
  const p = g.attributes.position as THREE.BufferAttribute;
  for (let i = 0; i < p.count; i++) {
    const top = p.getY(i) > 0.5;
    p.setXYZ(i, p.getX(i) * (top ? topW : w), p.getY(i) * h, p.getZ(i) * (top ? topD : d));
  }
  return g;
}

/**
 * Faixa dobrada (folha de palmeira, samambaia): começa na origem e segue `dir` (radianos em XZ),
 * subindo `rise` e caindo pela `droop` ao longo de `len`, com largura máxima `w` no meio.
 */
export function frond(len: number, w: number, dir: number, rise: number, droop: number, pieces = 3): Geo {
  const pts: Array<[number, number, number]> = [];
  for (let k = 0; k <= pieces; k++) {
    const t = k / pieces;
    const along = len * t;
    const y = rise * t - droop * t * t;
    const half = w * 0.5 * Math.sin(Math.PI * Math.min(1, t * 1.1));
    pts.push([along, y, half]);
  }
  const c = Math.cos(dir); const s = Math.sin(dir);
  const out: number[] = [];
  const P = (a: number, y: number, side: number): [number, number, number] => [a * c - side * s, y, a * s + side * c];
  for (let k = 0; k < pieces; k++) {
    const [a0, y0, h0] = pts[k]; const [a1, y1, h1] = pts[k + 1];
    // Nervura central um pouco acima das bordas: a folha tem vinco (lê melhor com faces planas).
    const m0 = P(a0, y0 + 0.04, 0); const m1 = P(a1, y1 + 0.04, 0);
    const l0 = P(a0, y0, -h0); const l1 = P(a1, y1, -h1);
    const r0 = P(a0, y0, h0); const r1 = P(a1, y1, h1);
    out.push(...m0, ...l0, ...l1, ...m0, ...l1, ...m1);
    out.push(...m0, ...r1, ...r0, ...m0, ...m1, ...r1);
  }
  return tris(out);
}

/** Tufo de lâminas (capim): `n` triângulos finos em leque, altura `h`, abertura `r`. */
export function tuft(n: number, h: number, r: number, seed: number): Geo {
  const out: number[] = [];
  for (let k = 0; k < n; k++) {
    const a = (k / n) * Math.PI * 2 + hash3(seed, k, 1) * 0.8;
    const lean = r * (0.6 + hash3(seed, k, 2) * 0.6);
    const hh = h * (0.7 + hash3(seed, k, 3) * 0.5);
    const c = Math.cos(a); const s = Math.sin(a);
    const w = 0.07 + r * 0.12;
    out.push(-s * w, 0, c * w, s * w, 0, -c * w, c * lean, hh, s * lean);
  }
  return tris(out);
}

/**
 * Pontos do modelo (escalado por sx, sy, sz) dentro da faixa de altura [y0, y1]: os vértices que caem nela
 * e os cruzamentos das arestas com os planos y0 e y1 — o contorno exato da fatia (um triângulo grande
 * que atravessa a faixa sem vértice dentro dela também conta). É o que o carro pode tocar.
 */
export function bandPoints(geo: Geo, y0: number, y1: number, cb: (x: number, y: number, z: number) => void, sx = 1, sy = 1, sz = 1): void {
  const p = geo.attributes.position as THREE.BufferAttribute;
  const v = [0, 0, 0, 0, 0, 0, 0, 0, 0];
  for (let i = 0; i + 2 < p.count; i += 3) {
    for (let k = 0; k < 3; k++) { v[k * 3] = p.getX(i + k) * sx; v[k * 3 + 1] = p.getY(i + k) * sy; v[k * 3 + 2] = p.getZ(i + k) * sz; }
    for (let k = 0; k < 3; k++) {
      const ax = v[k * 3]; const ay = v[k * 3 + 1]; const az = v[k * 3 + 2];
      if (ay >= y0 && ay <= y1) cb(ax, ay, az);
      const n = (k + 1) % 3;
      const bx = v[n * 3]; const by = v[n * 3 + 1]; const bz = v[n * 3 + 2];
      for (const yp of [y0, y1]) {
        if ((ay - yp) * (by - yp) < 0) {
          const t = (yp - ay) / (by - ay);
          cb(ax + (bx - ax) * t, yp, az + (bz - az) * t);
        }
      }
    }
  }
}
