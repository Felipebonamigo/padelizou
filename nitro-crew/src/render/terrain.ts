// Terreno: duas faixas (uma por lado) que seguem o RoadFrame, com relevo por bioma (ruído
// por índice de segmento, nunca por quadro), mar animado no litoral, quadras + prédios de
// fundo na cidade, disco de chão e o anel distante (cordilheira / mesas / skyline) que gira
// pelo rumo absoluto. Buffers alocados uma vez, atualizados no lugar.
import * as THREE from 'three';
import type { SceneryId, Track } from '../core/types';
import { fbm, hash2, hash3, valueNoise } from './noise';
import { mix, shade, type Palette } from './palette';
import type { RoadFrame } from './roadframe';
import { windowTextures } from './textures';
import { HEADING_PER_CURVE, SEGMENT_M, Y_SCALE } from './units';

/** Distância lateral (m) de cada coluna da faixa, a partir do centro da pista. */
const COLS = [8.4, 16, 26, 40, 60, 90, 130, 180, 260, 380];
const SEA_DEPTH_M = 3.2;
const RING_RADIUS = 470;
const RING_INNER = 330;

function reliefAmplitude(biome: SceneryId): [number, number] {
  switch (biome) {
    case 'desert': return [24, 0.016];
    case 'alpine': return [62, 0.03];
    case 'tropical': return [34, 0.035];
    case 'savanna': return [9, 0.028];
    case 'coast': return [20, 0.03];
    case 'city_night': return [0, 0.03];
  }
}

/** Altura do relevo (m) na coluna `col` do lado `side` do segmento `seg`. Zero perto da pista. */
function relief(biome: SceneryId, seg: number, col: number, side: number): number {
  if (col < 2) return 0;
  const [amp, freq] = reliefAmplitude(biome);
  if (amp === 0) return 0;
  const ramp = Math.pow((col - 1) / (COLS.length - 2), 1.25);
  const seed = side > 0 ? 501 : 907;
  const n = fbm(seed, seg * freq + col * 0.85, 2) * 0.5 + 0.5;
  const m = valueNoise(seed + 77, seg * freq * 1.9 + col * 2.3) * 0.5 + 0.5;
  let h = (n * 0.75 + m * 0.25) * amp * ramp;
  if (biome === 'desert') h = amp * ramp * Math.pow(n, 1.6) * 0.9 + m * 3 * ramp; // dunas suaves
  return h;
}

const PERIODIC = 64;
/** Ruído periódico ao redor do anel (t em voltas: 0..1 fecha sem emenda). */
function ringNoise(seed: number, t: number, octaves: number): number {
  let sum = 0; let amp = 1; let norm = 0; let period = PERIODIC;
  for (let o = 0; o < octaves; o++) {
    const x = t * period;
    const i = Math.floor(x); const f = x - i; const s = f * f * (3 - 2 * f);
    const a = hash2(seed + o * 31, i % period) * 2 - 1;
    const b = hash2(seed + o * 31, (i + 1) % period) * 2 - 1;
    sum += (a + (b - a) * s) * amp;
    norm += amp; amp *= 0.5; period *= 2;
  }
  return sum / norm;
}

const SEA_VERT = /* glsl */ `
varying vec3 vWorld;
#include <fog_pars_vertex>
void main() {
  vec4 wp = modelMatrix * vec4(position, 1.0);
  vWorld = wp.xyz;
  vec4 mvPosition = viewMatrix * wp;
  gl_Position = projectionMatrix * mvPosition;
  #include <fog_vertex>
}`;

const SEA_FRAG = /* glsl */ `
uniform vec3 uDeep; uniform vec3 uShallow; uniform vec3 uSky; uniform vec3 uSunDir; uniform vec3 uSunColor;
uniform float uTime;
varying vec3 vWorld;
#include <fog_pars_fragment>
void main() {
  vec2 p = vWorld.xz;
  float w1 = sin(p.x * 0.21 + uTime * 1.4) ;
  float w2 = sin(p.y * 0.17 - uTime * 1.1 + p.x * 0.05);
  float w3 = sin((p.x + p.y) * 0.09 + uTime * 0.6);
  float w = (w1 + w2 + w3) / 3.0;
  vec3 n = normalize(vec3(cos(p.x * 0.21 + uTime * 1.4) * 0.09 + cos((p.x + p.y) * 0.09 + uTime * 0.6) * 0.04, 1.0, cos(p.y * 0.17 - uTime * 1.1) * 0.09));
  vec3 v = normalize(cameraPosition - vWorld);
  float fres = pow(1.0 - max(dot(n, v), 0.0), 3.5);
  vec3 col = mix(uDeep, uShallow, 0.4 + 0.3 * w);
  col = mix(col, uSky, fres * 0.75);
  vec3 r = reflect(-v, n);
  float spec = pow(max(dot(r, uSunDir), 0.0), 160.0);
  col += uSunColor * spec * 3.0;
  gl_FragColor = vec4(col, 1.0);
  #include <fog_fragment>
  #include <tonemapping_fragment>
  #include <colorspace_fragment>
}`;

/** Faixa com `lanes` vértices por ponto, com cor por vértice e sombreamento plano. */
class Strip {
  readonly geometry = new THREE.BufferGeometry();
  readonly position: THREE.BufferAttribute;
  readonly color: THREE.BufferAttribute;
  readonly mesh: THREE.Mesh;
  constructor(readonly capacity: number, readonly lanes: number, material: THREE.Material) {
    const verts = capacity * lanes;
    this.position = new THREE.BufferAttribute(new Float32Array(verts * 3), 3);
    this.position.setUsage(THREE.DynamicDrawUsage);
    this.color = new THREE.BufferAttribute(new Float32Array(verts * 3), 3);
    this.color.setUsage(THREE.DynamicDrawUsage);
    this.geometry.setAttribute('position', this.position);
    this.geometry.setAttribute('color', this.color);
    this.geometry.setAttribute('normal', new THREE.BufferAttribute(new Float32Array(verts * 3), 3));
    const index = new Uint32Array((capacity - 1) * (lanes - 1) * 6);
    let k = 0;
    for (let j = 0; j < capacity - 1; j++) for (let l = 0; l < lanes - 1; l++) {
      const a = j * lanes + l; const b = a + 1; const c = a + lanes; const d = c + 1;
      index[k++] = a; index[k++] = b; index[k++] = c;
      index[k++] = b; index[k++] = d; index[k++] = c;
    }
    this.geometry.setIndex(new THREE.BufferAttribute(index, 1));
    this.mesh = new THREE.Mesh(this.geometry, material);
    this.mesh.frustumCulled = false;
    this.mesh.receiveShadow = true;
  }
  set(point: number, lane: number, x: number, y: number, z: number, c: THREE.Color): void {
    const i = point * this.lanes + lane;
    this.position.setXYZ(i, x, y, z);
    this.color.setXYZ(i, c.r, c.g, c.b);
  }
  finish(points: number): void {
    this.geometry.setDrawRange(0, Math.max(0, points - 1) * (this.lanes - 1) * 6);
    this.position.needsUpdate = true; this.color.needsUpdate = true;
  }
  dispose(): void { this.geometry.dispose(); }
}

/** Anel distante: cordilheira/mesas/colinas em três anéis de vértices (base, meio, topo). */
function buildRing(biome: SceneryId, radius: number, height: number, seed: number, colors: [THREE.Color, THREE.Color, THREE.Color]): THREE.BufferGeometry {
  const N = 144;
  const pos: number[] = []; const col: number[] = [];
  const push = (x: number, y: number, z: number, c: THREE.Color) => { pos.push(x, y, z); col.push(c.r, c.g, c.b); };
  const heightAt = (i: number): number => {
    const t = (i % N) / N;
    const n = ringNoise(seed, t, 3) * 0.5 + 0.5;
    if (biome === 'desert') {
      const m = ringNoise(seed + 9, t, 1);
      return m > 0.1 ? height * (0.75 + 0.25 * n) : height * (0.18 + 0.15 * n);
    }
    if (biome === 'coast') return Math.max(0, n - 0.35) * height * 1.6;
    if (biome === 'city_night') return height * (0.2 + 0.3 * n);
    return height * (0.3 + 0.7 * n);
  };
  for (let i = 0; i < N; i++) {
    const a0 = (i / N) * Math.PI * 2; const a1 = ((i + 1) / N) * Math.PI * 2;
    const h0 = heightAt(i); const h1 = heightAt(i + 1);
    const x0 = Math.cos(a0) * radius; const z0 = Math.sin(a0) * radius;
    const x1 = Math.cos(a1) * radius; const z1 = Math.sin(a1) * radius;
    const rIn = 0.985;
    const B = -80;
    // Faixa de baixo (base → meio) e de cima (meio → topo); topo ligeiramente para dentro.
    const m0 = h0 * 0.62; const m1 = h1 * 0.62;
    push(x0, B, z0, colors[0]); push(x1, B, z1, colors[0]); push(x0, m0, z0, colors[1]);
    push(x1, B, z1, colors[0]); push(x1, m1, z1, colors[1]); push(x0, m0, z0, colors[1]);
    push(x0, m0, z0, colors[1]); push(x1, m1, z1, colors[1]); push(x0 * rIn, h0, z0 * rIn, colors[2]);
    push(x1, m1, z1, colors[1]); push(x1 * rIn, h1, z1 * rIn, colors[2]); push(x0 * rIn, h0, z0 * rIn, colors[2]);
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  g.setAttribute('color', new THREE.Float32BufferAttribute(col, 3));
  g.computeVertexNormals();
  return g;
}

/** Caixa com UV em metros (janelas de 3 m de passo, sem esticar). */
function meteredBox(w: number, h: number, d: number): THREE.BoxGeometry {
  const g = new THREE.BoxGeometry(w, h, d);
  const uv = g.attributes.uv as THREE.BufferAttribute;
  const faces: Array<[number, number]> = [[d, h], [d, h], [w, d], [w, d], [w, h], [w, h]];
  for (let f = 0; f < 6; f++) {
    const [su, sv] = faces[f];
    for (let k = 0; k < 4; k++) { const i = f * 4 + k; uv.setXY(i, uv.getX(i) * su / 6, uv.getY(i) * sv / 6); }
  }
  return g;
}

export class Terrain {
  readonly group = new THREE.Group();
  /** Anel distante e skyline: giram pelo rumo absoluto. */
  readonly farGroup = new THREE.Group();
  private readonly left: Strip;
  private readonly right: Strip;
  private readonly material: THREE.MeshStandardMaterial;
  private readonly ground: THREE.Mesh<THREE.CircleGeometry, THREE.MeshStandardMaterial>;
  private readonly sea: THREE.Mesh<THREE.PlaneGeometry, THREE.ShaderMaterial>;
  private readonly cityBlocks: THREE.InstancedMesh[] = [];
  private readonly cityMaterial: THREE.MeshStandardMaterial;
  private readonly skyline: THREE.InstancedMesh;
  private readonly neon: THREE.InstancedMesh;
  private ringMeshes: THREE.Mesh[] = [];
  private biome: SceneryId = 'coast';
  private trackKey = '';
  private minRoadY = 0;
  private readonly cA = new THREE.Color(); private readonly cB = new THREE.Color(); private readonly cSand = new THREE.Color();
  private readonly cSnow = new THREE.Color(); private readonly cBlock = [new THREE.Color(), new THREE.Color(), new THREE.Color()];
  private readonly tmp = new THREE.Color();
  private readonly dummy = new THREE.Object3D();

  constructor(capacity: number) {
    this.material = new THREE.MeshStandardMaterial({ vertexColors: true, flatShading: true, roughness: 1, metalness: 0 });
    this.left = new Strip(capacity, COLS.length, this.material);
    this.right = new Strip(capacity, COLS.length, this.material);
    this.group.add(this.left.mesh, this.right.mesh);

    this.ground = new THREE.Mesh(new THREE.CircleGeometry(1500, 48), new THREE.MeshStandardMaterial({ color: '#333333', roughness: 1 }));
    this.ground.rotation.x = -Math.PI / 2;
    this.ground.position.y = -14;
    this.ground.receiveShadow = false;
    this.group.add(this.ground);

    const seaMat = new THREE.ShaderMaterial({
      vertexShader: SEA_VERT, fragmentShader: SEA_FRAG, fog: true,
      uniforms: THREE.UniformsUtils.merge([THREE.UniformsLib.fog, {
        uDeep: { value: new THREE.Color('#0b4f8a') }, uShallow: { value: new THREE.Color('#2aa8c8') }, uSky: { value: new THREE.Color('#cfefff') },
        uSunDir: { value: new THREE.Vector3(0, 1, 0) }, uSunColor: { value: new THREE.Color('#fff3a0') }, uTime: { value: 0 },
      }]),
    });
    this.sea = new THREE.Mesh(new THREE.PlaneGeometry(3200, 3200), seaMat);
    this.sea.rotation.x = -Math.PI / 2;
    this.sea.frustumCulled = false;
    this.sea.visible = false;
    this.group.add(this.sea);

    const win = windowTextures();
    this.cityMaterial = new THREE.MeshStandardMaterial({ map: win.wall, emissiveMap: win.win, emissive: '#ffd27a', emissiveIntensity: 1.3, color: '#5a6478', roughness: 0.7 });
    for (const [w, h, d] of [[18, 22, 18], [16, 42, 16], [22, 70, 22]] as const) {
      const im = new THREE.InstancedMesh(meteredBox(w, h, d), this.cityMaterial, 160);
      im.frustumCulled = false; im.castShadow = false; im.visible = false;
      im.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
      this.cityBlocks.push(im);
      this.group.add(im);
    }
    this.skyline = new THREE.InstancedMesh(meteredBox(1, 1, 1), this.cityMaterial, 160);
    this.skyline.frustumCulled = false; this.skyline.visible = false;
    this.farGroup.add(this.skyline);
    this.neon = new THREE.InstancedMesh(new THREE.BoxGeometry(1, 1, 1), new THREE.MeshBasicMaterial({ color: '#ff3fd0', toneMapped: false }), 48);
    this.neon.frustumCulled = false; this.neon.visible = false;
    this.farGroup.add(this.neon);
    this.group.add(this.farGroup);
  }

  setTrack(track: Track, p: Palette, key: string): void {
    if (key === this.trackKey) return;
    this.trackKey = key;
    this.biome = track.def.scenery;
    let minY = Infinity;
    for (const s of track.segments) minY = Math.min(minY, s.y0, s.y1);
    this.minRoadY = minY * Y_SCALE;
    this.cA.set(p.grassLight); this.cB.set(p.grassDark);
    const night = 1 - p.light;
    this.cSand.set(mix(shade('#e6d3a3', 0.4 + 0.6 * p.light), '#1a2040', night * 0.45));
    this.cSnow.set(mix(shade('#f4f8ff', 0.5 + 0.5 * p.light), '#1a2040', night * 0.4));
    this.cBlock[0].set(shade(p.roadDark, 0.9)); this.cBlock[1].set(mix(p.roadLight, '#9a9a90', 0.5)); this.cBlock[2].set(p.grassDark);
    this.ground.material.color.set(shade(p.grassDark, 0.85));
    this.ground.visible = this.biome !== 'coast';
    this.sea.visible = this.biome === 'coast';
    const su = this.sea.material.uniforms;
    (su.uDeep.value as THREE.Color).set(mix('#0b5e9c', p.sky[0], 0.25));
    (su.uShallow.value as THREE.Color).set(mix('#2fc2c9', p.sky[1], 0.2));
    (su.uSky.value as THREE.Color).set(p.sky[2]);
    (su.uSunColor.value as THREE.Color).set(p.sun ?? p.moon ?? '#ffffff');
    const city = this.biome === 'city_night';
    for (const im of this.cityBlocks) im.visible = city;
    this.skyline.visible = city;
    this.neon.visible = city;
    this.cityMaterial.emissiveIntensity = p.light < 0.8 ? 1.3 : 0;
    this.buildFar(p);
  }

  private buildFar(p: Palette): void {
    for (const m of this.ringMeshes) { this.farGroup.remove(m); m.geometry.dispose(); }
    this.ringMeshes = [];
    const mat = new THREE.MeshStandardMaterial({ vertexColors: true, flatShading: true, roughness: 1 });
    const far = new THREE.Color(p.far);
    const near = new THREE.Color(p.near);
    const fog = new THREE.Color(p.fog);
    const b = this.biome;
    const H = b === 'alpine' ? 230 : b === 'desert' ? 120 : b === 'coast' ? 70 : b === 'city_night' ? 60 : b === 'tropical' ? 150 : 90;
    const top = b === 'alpine' ? this.cSnow.clone() : far.clone().lerp(fog, 0.15).multiplyScalar(1.15);
    const outer = new THREE.Mesh(buildRing(b, RING_RADIUS, H, 11, [far.clone().lerp(fog, 0.45), far.clone().lerp(fog, 0.2), top]), mat);
    const innerTop = b === 'alpine' ? near.clone().lerp(this.cSnow, 0.25) : near.clone().multiplyScalar(1.1);
    const inner = new THREE.Mesh(buildRing(b, RING_INNER, H * 0.42, 23, [near.clone().lerp(fog, 0.35), near.clone(), innerTop]), mat);
    outer.frustumCulled = false; inner.frustumCulled = false;
    this.farGroup.add(outer, inner);
    this.ringMeshes.push(outer, inner);
    if (b === 'city_night') this.buildSkyline();
  }

  private buildSkyline(): void {
    const d = this.dummy;
    let k = 0;
    for (let i = 0; i < this.skyline.count; i++) {
      const a = hash2(i, 3) * Math.PI * 2;
      const r = RING_INNER * 0.9 + hash2(i, 4) * 260;
      const w = 18 + hash2(i, 5) * 26; const dep = 18 + hash2(i, 6) * 22;
      const env = 0.55 + 0.45 * Math.cos(a - 0.8);
      const h = (30 + hash2(i, 7) * 130) * env;
      d.position.set(Math.cos(a) * r, h / 2 - 10, Math.sin(a) * r);
      d.rotation.set(0, hash2(i, 8) * 0.6, 0);
      d.scale.set(w, h, dep);
      d.updateMatrix();
      this.skyline.setMatrixAt(i, d.matrix);
      if (k < this.neon.count && hash2(i, 9) > 0.7) {
        d.position.y = h - 10 + 1.5;
        d.scale.set(w * 0.7, 2.2, 2.2);
        d.updateMatrix();
        this.neon.setMatrixAt(k, d.matrix);
        this.neon.setColorAt(k, this.tmp.set(hash2(i, 10) > 0.5 ? '#ff3fd0' : '#3fe8ff'));
        k++;
      }
    }
    this.neon.count = k;
    this.skyline.instanceMatrix.needsUpdate = true;
    this.neon.instanceMatrix.needsUpdate = true;
    if (this.neon.instanceColor) this.neon.instanceColor.needsUpdate = true;
  }

  /** Cor do chão numa coluna: dois tons por ruído; areia na praia; quadras na cidade; neve no alto. */
  private groundColor(seg: number, col: number, side: number, h: number, out: THREE.Color): THREE.Color {
    const b = this.biome;
    if (b === 'city_night') {
      const block = hash3(Math.floor(seg / 10), col, side);
      return out.copy(this.cBlock[block < 0.4 ? 0 : block < 0.7 ? 1 : 2]);
    }
    if (b === 'coast' && side > 0) return out.copy(this.cSand);
    const n = fbm(side > 0 ? 31 : 57, seg * 0.09 + col * 1.7, 2) * 0.5 + 0.5;
    out.copy(this.cB).lerp(this.cA, n);
    if (b === 'alpine' && h > 34) out.lerp(this.cSnow, Math.min(1, (h - 34) / 14));
    if (b === 'coast' && col === 0) out.lerp(this.cSand, 0.35);
    return out;
  }

  update(frame: RoadFrame, track: Track, time: number, sunDirLocal: THREE.Vector3, absHeading: number): void {
    const segs = track.segments;
    const n = frame.count;
    const coast = this.biome === 'coast';
    const originY = segs[frame.baseIndex].y0 * Y_SCALE - frame.py[frame.behind];
    const seaY = this.minRoadY - SEA_DEPTH_M - originY;
    for (let j = 0; j < n; j++) {
      const s = segs[frame.segIndex[j]];
      const h = frame.heading[j];
      const cx = Math.cos(h); const sz = Math.sin(h);
      const px = frame.px[j]; const py = frame.py[j]; const pz = frame.pz[j];
      const delta = s.curve * HEADING_PER_CURVE;
      // Do lado de dentro da curva, colunas longe demais dobrariam para trás: prende a distância.
      const clampD = 0.9 * SEGMENT_M / (Math.abs(delta) + 1e-6);
      for (let c = 0; c < COLS.length; c++) {
        for (const side of [-1, 1] as const) {
          const inside = (side > 0) === (delta > 0) && delta !== 0;
          const dist = inside ? Math.min(COLS[c], clampD) : COLS[c];
          let y: number;
          if (coast && side > 0) y = c === 0 ? py : c === 1 ? py - 0.7 : seaY - 0.6;
          else y = py + relief(this.biome, s.index, c, side);
          const color = this.groundColor(s.index, c, side, y - py, this.tmp);
          const strip = side < 0 ? this.left : this.right;
          // Esquerda: pistas de fora para dentro (x crescente), como a pista.
          const lane = side < 0 ? COLS.length - 1 - c : c;
          strip.set(j, lane, px + side * dist * cx, y, pz + side * dist * sz, color);
        }
      }
    }
    this.left.finish(n);
    this.right.finish(n);
    this.sea.position.y = seaY;
    const su = this.sea.material.uniforms;
    su.uTime.value = time;
    (su.uSunDir.value as THREE.Vector3).copy(sunDirLocal);
    this.ground.position.y = Math.min(-14, seaY - 20);
    this.farGroup.rotation.y = absHeading;
    if (this.biome === 'city_night') this.updateCityBlocks(frame, track);
  }

  /** Prédios de fundo da cidade: um a cada 2 segmentos por lado, entre 45 e 300 m da pista. */
  private updateCityBlocks(frame: RoadFrame, track: Track): void {
    const counts = [0, 0, 0];
    const d = this.dummy;
    const segs = track.segments;
    for (let j = 0; j < frame.count; j += 2) {
      const s = segs[frame.segIndex[j]];
      const h = frame.heading[j];
      const cx = Math.cos(h); const sz = Math.sin(h);
      const delta = s.curve * HEADING_PER_CURVE;
      for (const side of [-1, 1] as const) {
        const r = hash2(s.index, side + 10);
        if (r < 0.25) continue;
        const cls = r < 0.6 ? 0 : r < 0.85 ? 1 : 2;
        if (counts[cls] >= 160) continue;
        const inside = (side > 0) === (delta > 0) && delta !== 0;
        let dist = 45 + hash2(s.index, side + 20) * 240;
        if (inside) dist = Math.min(dist, 0.9 * SEGMENT_M / (Math.abs(delta) + 1e-6));
        const ws = 0.8 + hash2(s.index, side + 30) * 0.6;
        d.position.set(frame.px[j] + side * dist * cx, frame.py[j] - 1, frame.pz[j] + side * dist * sz);
        d.rotation.set(0, -h + (hash2(s.index, side + 40) - 0.5) * 0.5, 0);
        d.scale.set(ws, 1, ws);
        d.updateMatrix();
        this.cityBlocks[cls].setMatrixAt(counts[cls]++, d.matrix);
      }
    }
    for (let c = 0; c < 3; c++) {
      this.cityBlocks[c].count = counts[c];
      this.cityBlocks[c].instanceMatrix.needsUpdate = true;
    }
  }

  dispose(): void {
    this.left.dispose(); this.right.dispose(); this.material.dispose();
    this.ground.geometry.dispose(); this.ground.material.dispose();
    this.sea.geometry.dispose(); this.sea.material.dispose();
    for (const im of this.cityBlocks) { im.geometry.dispose(); im.dispose(); }
    this.skyline.geometry.dispose(); this.skyline.dispose();
    this.neon.geometry.dispose(); (this.neon.material as THREE.Material).dispose(); this.neon.dispose();
    this.cityMaterial.dispose();
    for (const m of this.ringMeshes) { m.geometry.dispose(); (m.material as THREE.Material).dispose(); }
  }
}
