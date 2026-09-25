// Malha da pista a partir do RoadFrame: asfalto com textura procedural (faixas laterais e
// linha central tracejada pintadas nela), zebras vermelho/branco nas curvas, linha de largada
// quadriculada e box (asfalto claro + faixa amarela + marcação de área). Os buffers são
// alocados uma vez e atualizados no lugar a cada quadro.
import * as THREE from 'three';
import type { Track } from '../core/types';
import { mix, shade, type Palette } from './palette';
import type { RoadFrame } from './roadframe';
import { ROAD_HALF_WIDTH_M, SEGMENT_M } from './units';

const SHOULDER_M = 1.4;
const PIT_X0 = 1.09;
const PIT_X1 = 2.06;
/** Metros de pista por repetição da textura do asfalto. */
const ASPHALT_REPEAT_M = 8;
const PIT_REPEAT_M = 16;

function canvas(w: number, h: number): [HTMLCanvasElement, CanvasRenderingContext2D] {
  const c = document.createElement('canvas');
  c.width = w; c.height = h;
  const ctx = c.getContext('2d');
  if (!ctx) throw new Error('sem canvas 2D');
  return [c, ctx];
}

function makeTexture(c: HTMLCanvasElement, anisotropy: number): THREE.CanvasTexture {
  const t = new THREE.CanvasTexture(c);
  t.wrapS = THREE.ClampToEdgeWrapping; t.wrapT = THREE.RepeatWrapping;
  t.colorSpace = THREE.SRGBColorSpace;
  t.anisotropy = anisotropy;
  t.minFilter = THREE.LinearMipmapLinearFilter;
  return t;
}

/** Asfalto: granulado, trilhas de pneu mais escuras, faixas laterais e tracejado central. */
function paintAsphalt(p: Palette, night: boolean, anisotropy: number): THREE.CanvasTexture {
  const W = 256; const H = 1024;
  const [c, ctx] = canvas(W, H);
  ctx.fillStyle = p.roadLight;
  ctx.fillRect(0, 0, W, H);
  const img = ctx.getImageData(0, 0, W, H);
  const d = img.data;
  let seed = 1234567;
  for (let i = 0; i < W * H; i++) {
    seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
    const g = ((seed >>> 8) / 16777216 - 0.5) * (night ? 18 : 26);
    const x = i % W;
    const u = x / W;
    // Trilhas dos pneus (um pouco mais escuras e lisas).
    const track = Math.exp(-Math.pow((u - 0.3) / 0.05, 2)) + Math.exp(-Math.pow((u - 0.7) / 0.05, 2));
    const k = i * 4;
    const dark = 1 - 0.07 * track;
    d[k] = Math.max(0, Math.min(255, d[k] * dark + g));
    d[k + 1] = Math.max(0, Math.min(255, d[k + 1] * dark + g));
    d[k + 2] = Math.max(0, Math.min(255, d[k + 2] * dark + g));
  }
  ctx.putImageData(img, 0, 0);
  // Faixas laterais contínuas e tracejado central (dash de 3,6 m a cada 8 m).
  ctx.fillStyle = p.lane;
  ctx.fillRect(Math.round(W * 0.018), 0, Math.round(W * 0.02), H);
  ctx.fillRect(Math.round(W * 0.962), 0, Math.round(W * 0.02), H);
  ctx.fillRect(Math.round(W * 0.493), 0, Math.round(W * 0.014), Math.round(H * 0.45));
  return makeTexture(c, anisotropy);
}

/** Box: faixa amarela na divisa, asfalto claro e contorno branco das áreas de parada. */
function paintPit(p: Palette, anisotropy: number): THREE.CanvasTexture {
  const W = 128; const H = 512;
  const [c, ctx] = canvas(W, H);
  ctx.fillStyle = shade(p.roadLight, 1.28);
  ctx.fillRect(0, 0, W, H);
  ctx.fillStyle = p.pitLine;
  ctx.fillRect(0, 0, 7, H);
  ctx.strokeStyle = mix(p.lane, p.roadLight, 0.2);
  ctx.lineWidth = 3;
  ctx.strokeRect(38, 40, 76, 432);
  ctx.fillStyle = mix(p.lane, p.roadLight, 0.5);
  ctx.fillRect(48, 52, 56, 12);
  return makeTexture(c, anisotropy);
}

function paintChecker(): THREE.CanvasTexture {
  const [c, ctx] = canvas(256, 64);
  for (let y = 0; y < 4; y++) for (let x = 0; x < 16; x++) {
    ctx.fillStyle = (x + y) % 2 === 0 ? '#f4f4f4' : '#141414';
    ctx.fillRect(x * 16, y * 16, 16, 16);
  }
  const t = new THREE.CanvasTexture(c);
  t.colorSpace = THREE.SRGBColorSpace;
  return t;
}

/** Faixa contínua ao longo da janela, com `lanes` vértices por ponto. */
class Strip {
  readonly geometry = new THREE.BufferGeometry();
  readonly position: THREE.BufferAttribute;
  readonly color: THREE.BufferAttribute;
  readonly uv: THREE.BufferAttribute;
  readonly mesh: THREE.Mesh;
  constructor(readonly capacity: number, readonly lanes: number, material: THREE.Material) {
    const verts = capacity * lanes;
    this.position = new THREE.BufferAttribute(new Float32Array(verts * 3), 3);
    this.position.setUsage(THREE.DynamicDrawUsage);
    this.color = new THREE.BufferAttribute(new Float32Array(verts * 3), 3);
    this.color.setUsage(THREE.DynamicDrawUsage);
    this.uv = new THREE.BufferAttribute(new Float32Array(verts * 2), 2);
    this.uv.setUsage(THREE.DynamicDrawUsage);
    this.geometry.setAttribute('position', this.position);
    this.geometry.setAttribute('color', this.color);
    this.geometry.setAttribute('uv', this.uv);
    this.geometry.setAttribute('normal', new THREE.BufferAttribute(new Float32Array(verts * 3), 3));
    // Índices: para cada par de pontos consecutivos, (lanes − 1) quadriláteros.
    const quads = (capacity - 1) * (lanes - 1);
    const index = new Uint32Array(quads * 6);
    let k = 0;
    for (let j = 0; j < capacity - 1; j++) {
      for (let l = 0; l < lanes - 1; l++) {
        const a = j * lanes + l; const b = a + 1; const c = a + lanes; const d = c + 1;
        index[k++] = a; index[k++] = b; index[k++] = c;
        index[k++] = b; index[k++] = d; index[k++] = c;
      }
    }
    this.geometry.setIndex(new THREE.BufferAttribute(index, 1));
    this.mesh = new THREE.Mesh(this.geometry, material);
    this.mesh.frustumCulled = false;
    this.mesh.receiveShadow = true;
  }
  set(point: number, lane: number, x: number, y: number, z: number, u: number, v: number, r: number, g: number, b: number): void {
    const i = point * this.lanes + lane;
    this.position.setXYZ(i, x, y, z);
    this.uv.setXY(i, u, v);
    this.color.setXYZ(i, r, g, b);
  }
  /** Quantos pontos estão em uso (o resto do buffer fica fora do drawRange). */
  finish(points: number, recomputeNormals: boolean): void {
    this.geometry.setDrawRange(0, Math.max(0, points - 1) * (this.lanes - 1) * 6);
    this.position.needsUpdate = true; this.uv.needsUpdate = true; this.color.needsUpdate = true;
    if (recomputeNormals) this.geometry.computeVertexNormals();
  }
  dispose(): void { this.geometry.dispose(); }
}

export class Road {
  readonly group = new THREE.Group();
  private readonly asphalt: Strip;
  private readonly shoulders: Strip;
  private readonly pit: Strip;
  private readonly asphaltMaterial: THREE.MeshStandardMaterial;
  private readonly shoulderMaterial: THREE.MeshStandardMaterial;
  private readonly pitMaterial: THREE.MeshStandardMaterial;
  private readonly startLine: THREE.Mesh<THREE.BufferGeometry, THREE.MeshStandardMaterial>;
  private readonly startPos: THREE.BufferAttribute;
  private textures: THREE.Texture[] = [];
  private paletteKey = '';
  private readonly cBand = [new THREE.Color(), new THREE.Color()];
  private readonly cRumble = [new THREE.Color(), new THREE.Color()];
  private readonly cGrass = new THREE.Color();

  constructor(capacity: number, private readonly anisotropy: number) {
    this.asphaltMaterial = new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 0.92, metalness: 0.0 });
    this.shoulderMaterial = new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 0.95, flatShading: true });
    this.pitMaterial = new THREE.MeshStandardMaterial({ roughness: 0.9, flatShading: true });
    this.asphalt = new Strip(capacity, 2, this.asphaltMaterial);
    this.shoulders = new Strip(capacity, 4, this.shoulderMaterial);
    this.pit = new Strip(capacity, 2, this.pitMaterial);
    // Os ombros usam 4 pistas: [esq. fora, esq. dentro, dir. dentro, dir. fora] — o quad do
    // meio (pistas 1→2) cobriria o asfalto, então o índice pula ele.
    const idx = this.shoulders.geometry.getIndex();
    if (idx) {
      const arr = idx.array as Uint32Array;
      for (let j = 0; j < capacity - 1; j++) {
        const base = (j * 3 + 1) * 6;
        for (let k = 0; k < 6; k++) arr[base + k] = 0;
      }
    }
    this.group.add(this.asphalt.mesh, this.shoulders.mesh, this.pit.mesh);
    this.shoulders.mesh.position.y = 0.005;
    this.pit.mesh.position.y = 0.012;

    const sg = new THREE.BufferGeometry();
    this.startPos = new THREE.BufferAttribute(new Float32Array(4 * 3), 3);
    this.startPos.setUsage(THREE.DynamicDrawUsage);
    sg.setAttribute('position', this.startPos);
    sg.setAttribute('uv', new THREE.BufferAttribute(new Float32Array([0, 0, 1, 0, 0, 1, 1, 1]), 2));
    sg.setAttribute('normal', new THREE.BufferAttribute(new Float32Array([0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0]), 3));
    sg.setIndex([0, 1, 2, 1, 3, 2]);
    const checker = paintChecker();
    this.textures.push(checker);
    this.startLine = new THREE.Mesh(sg, new THREE.MeshStandardMaterial({ map: checker, roughness: 0.8 }));
    this.startLine.frustumCulled = false;
    this.startLine.receiveShadow = true;
    this.startLine.position.y = 0.02;
    this.group.add(this.startLine);
  }

  setPalette(p: Palette, night: boolean, key: string): void {
    if (key === this.paletteKey) return;
    this.paletteKey = key;
    for (const t of this.textures) t.dispose();
    this.textures = [];
    const asphalt = paintAsphalt(p, night, this.anisotropy);
    const pit = paintPit(p, this.anisotropy);
    this.textures.push(asphalt, pit, paintChecker());
    this.asphaltMaterial.map = asphalt;
    this.asphaltMaterial.roughness = night ? 0.38 : 0.9;
    this.asphaltMaterial.metalness = night ? 0.2 : 0.0;
    this.asphaltMaterial.envMapIntensity = night ? 1.2 : 0.4;
    this.asphaltMaterial.needsUpdate = true;
    this.pitMaterial.map = pit;
    this.pitMaterial.needsUpdate = true;
    this.cBand[0].set('#ffffff'); this.cBand[1].set('#f2f2f2');
    this.cRumble[0].set(p.rumbleLight); this.cRumble[1].set(p.rumbleDark);
    this.cGrass.set(p.grassLight);
  }

  update(frame: RoadFrame, track: Track): void {
    const segs = track.segments;
    const n = frame.count;
    const W = ROAD_HALF_WIDTH_M;
    let pitPoints = 0;
    let prevPit = false;
    let startJ = -1;
    for (let j = 0; j < n; j++) {
      const s = segs[frame.segIndex[j]];
      const h = frame.heading[j];
      const cx = Math.cos(h); const sz = Math.sin(h);
      const px = frame.px[j]; const py = frame.py[j]; const pz = frame.pz[j];
      const v = (s.index * SEGMENT_M) / ASPHALT_REPEAT_M;
      const band = this.cBand[s.band];
      this.asphalt.set(j, 0, px - W * cx, py, pz - W * sz, 0, v, band.r, band.g, band.b);
      this.asphalt.set(j, 1, px + W * cx, py, pz + W * sz, 1, v, band.r, band.g, band.b);
      // Ombros: zebra nas curvas (|curve| ≥ 2), senão a cor da grama (some no terreno).
      const zebra = Math.abs(s.curve) >= 2;
      const c = zebra ? this.cRumble[s.band] : this.cGrass;
      const o = W + SHOULDER_M;
      this.shoulders.set(j, 0, px - o * cx, py, pz - o * sz, 0, v, c.r, c.g, c.b);
      this.shoulders.set(j, 1, px - W * cx, py, pz - W * sz, 0, v, c.r, c.g, c.b);
      this.shoulders.set(j, 2, px + W * cx, py, pz + W * sz, 0, v, c.r, c.g, c.b);
      this.shoulders.set(j, 3, px + o * cx, py, pz + o * sz, 0, v, c.r, c.g, c.b);
      // Box: só nos segmentos `pit` (mais o ponto que fecha o trecho), compactados no começo
      // do buffer. atalho: um só trecho de box por pista; dois trechos na mesma janela
      // ficariam ligados por um quad esticado.
      if (s.pit || prevPit) {
        const x0 = PIT_X0 * W; const x1 = PIT_X1 * W;
        const pv = (s.index * SEGMENT_M) / PIT_REPEAT_M;
        this.pit.set(pitPoints, 0, px + x0 * cx, py, pz + x0 * sz, 0, pv, 1, 1, 1);
        this.pit.set(pitPoints, 1, px + x1 * cx, py, pz + x1 * sz, 1, pv, 1, 1, 1);
        pitPoints++;
      }
      prevPit = s.pit;
      if (s.index === track.startIndex && j + 1 < n) startJ = j;
    }
    this.asphalt.finish(n, true);
    this.shoulders.finish(n, false);
    this.pit.finish(pitPoints, false);
    if (startJ >= 0) {
      const j = startJ;
      for (let k = 0; k < 2; k++) {
        const jj = j + k;
        const h = frame.heading[jj];
        const cx = Math.cos(h); const sz = Math.sin(h);
        this.startPos.setXYZ(k * 2, frame.px[jj] - W * cx, frame.py[jj], frame.pz[jj] - W * sz);
        this.startPos.setXYZ(k * 2 + 1, frame.px[jj] + W * cx, frame.py[jj], frame.pz[jj] + W * sz);
      }
      this.startPos.needsUpdate = true;
      this.startLine.visible = true;
    } else {
      this.startLine.visible = false;
    }
  }

  dispose(): void {
    this.asphalt.dispose(); this.shoulders.dispose(); this.pit.dispose();
    this.startLine.geometry.dispose(); this.startLine.material.dispose();
    this.asphaltMaterial.dispose(); this.shoulderMaterial.dispose(); this.pitMaterial.dispose();
    for (const t of this.textures) t.dispose();
  }
}
