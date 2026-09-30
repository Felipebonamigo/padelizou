// Malha da pista a partir do RoadFrame: asfalto com textura procedural (granulado, manchas,
// trilhas de pneu, faixas de bordo e tracejado central, que brilham um pouco à noite),
// acostamento de cascalho/terra, zebras vermelho/branco em relevo nas curvas, box com divisa
// zebrada e vagas, decalque da largada (quadriculado + marcas do grid) e, à noite, as poças de
// luz dos postes no asfalto. Os buffers são alocados uma vez e atualizados no lugar.
import * as THREE from 'three';
import type { Segment, Track } from '../core/types';
import type { Palette } from './palette';
import {
  ASPHALT_REPEAT_M, KERB_REPEAT_M, paintAsphalt, paintKerb, paintPit, paintPool, paintShoulder, paintStartDecal, PIT_REPEAT_M,
  PIT_X0, PIT_X1, SHOULDER_REPEAT_M, START_AHEAD, START_BEHIND,
} from './road-textures';
import type { RoadFrame } from './roadframe';
import { ROAD_HALF_WIDTH_M, SEGMENT_M } from './units';

/** Acostamento: da borda do asfalto até onde o terreno começa (terrain.ts, COLS[0]). */
export const SHOULDER_M = 1.4;
/** Zebra: começa um pouco dentro do asfalto, sobe até a crista e desce do lado de fora (m). */
const KERB_IN = -0.12;
const KERB_CREST = 0.4;
const KERB_OUT = 1.1;
const KERB_H = 0.07;
/** Poça de luz: deslocamento lateral do poste até o centro da poça (braço de 1,7 m + alcance), e tamanho. */
const POOL_OFFSET_M = 4.4;
const POOL_W = 12;
const POOL_L = 17;
const MAX_POOLS = 48;

/** Segmento com zebra: curva de média para cima (|curve| ≥ 2). */
export function hasKerb(s: Segment): boolean { return Math.abs(s.curve) >= 2; }

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

/**
 * Zebras: quadriláteros soltos por segmento (3 vértices na seção: dentro, crista, fora), para
 * poder pular os trechos retos sem esticar um quad entre duas curvas.
 */
class KerbList {
  readonly geometry = new THREE.BufferGeometry();
  readonly position: THREE.BufferAttribute;
  readonly uv: THREE.BufferAttribute;
  readonly mesh: THREE.Mesh;
  private count = 0;
  constructor(readonly capacity: number, material: THREE.Material) {
    const verts = capacity * 6;
    this.position = new THREE.BufferAttribute(new Float32Array(verts * 3), 3);
    this.position.setUsage(THREE.DynamicDrawUsage);
    this.uv = new THREE.BufferAttribute(new Float32Array(verts * 2), 2);
    this.uv.setUsage(THREE.DynamicDrawUsage);
    this.geometry.setAttribute('position', this.position);
    this.geometry.setAttribute('uv', this.uv);
    this.geometry.setAttribute('normal', new THREE.BufferAttribute(new Float32Array(verts * 3).map((_, i) => (i % 3 === 1 ? 1 : 0)), 3));
    const index = new Uint32Array(capacity * 12);
    for (let q = 0; q < capacity; q++) {
      const o = q * 6; const k = q * 12;
      // 0 1 2 = seção de trás (dentro → fora), 3 4 5 = seção da frente.
      index.set([o, o + 1, o + 3, o + 1, o + 4, o + 3, o + 1, o + 2, o + 4, o + 2, o + 5, o + 4], k);
    }
    this.geometry.setIndex(new THREE.BufferAttribute(index, 1));
    this.mesh = new THREE.Mesh(this.geometry, material);
    this.mesh.frustumCulled = false;
    this.mesh.receiveShadow = true;
  }
  begin(): void { this.count = 0; }
  /** Um trecho de zebra entre os pontos a e b do frame, do lado `side` (−1 esquerda, 1 direita). */
  add(frame: RoadFrame, a: number, b: number, side: number, v0: number, v1: number): void {
    if (this.count >= this.capacity) return;
    const o = this.count * 6;
    const W = ROAD_HALF_WIDTH_M;
    const offs = [KERB_IN, KERB_CREST, KERB_OUT];
    const hs = [0.012, KERB_H, 0.016];
    for (let e = 0; e < 2; e++) {
      const j = e === 0 ? a : b;
      const h = frame.heading[j];
      const cx = Math.cos(h); const sz = Math.sin(h);
      for (let k = 0; k < 3; k++) {
        // Na esquerda a seção vai de fora para dentro para manter a mesma orientação dos triângulos.
        const kk = side > 0 ? k : 2 - k;
        const x = side * (W + offs[kk]);
        this.position.setXYZ(o + e * 3 + k, frame.px[j] + x * cx, frame.py[j] + hs[kk], frame.pz[j] + x * sz);
        this.uv.setXY(o + e * 3 + k, kk / 2, e === 0 ? v0 : v1);
      }
    }
    this.count++;
  }
  finish(): void {
    this.geometry.setDrawRange(0, this.count * 12);
    this.position.needsUpdate = true; this.uv.needsUpdate = true;
    this.mesh.visible = this.count > 0;
  }
  dispose(): void { this.geometry.dispose(); }
}

export class Road {
  readonly group = new THREE.Group();
  private readonly asphalt: Strip;
  private readonly shoulders: Strip;
  private readonly pit: Strip;
  private readonly decal: Strip;
  private readonly kerbs: KerbList;
  private readonly asphaltMaterial: THREE.MeshStandardMaterial;
  private readonly shoulderMaterial: THREE.MeshStandardMaterial;
  private readonly kerbMaterial: THREE.MeshStandardMaterial;
  private readonly pitMaterial: THREE.MeshStandardMaterial;
  private readonly decalMaterial: THREE.MeshStandardMaterial;
  private readonly pools: THREE.InstancedMesh<THREE.BufferGeometry, THREE.MeshBasicMaterial>;
  private readonly poolTexture: THREE.CanvasTexture;
  private textures: THREE.Texture[] = [];
  private paletteKey = '';
  private night = false;
  private readonly cBand = [new THREE.Color(), new THREE.Color()];
  private readonly dummy = new THREE.Object3D();

  constructor(capacity: number, private readonly anisotropy: number) {
    this.asphaltMaterial = new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 0.9, metalness: 0.0, emissive: '#ffffff', emissiveIntensity: 0 });
    this.shoulderMaterial = new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 1, flatShading: true });
    this.kerbMaterial = new THREE.MeshStandardMaterial({ roughness: 0.6, flatShading: true, emissive: '#ffffff', emissiveIntensity: 0 });
    this.pitMaterial = new THREE.MeshStandardMaterial({ roughness: 0.85, flatShading: true });
    this.decalMaterial = new THREE.MeshStandardMaterial({
      roughness: 0.7, transparent: true, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2,
      emissive: '#ffffff', emissiveIntensity: 0,
    });
    this.asphalt = new Strip(capacity, 2, this.asphaltMaterial);
    this.shoulders = new Strip(capacity, 4, this.shoulderMaterial);
    this.pit = new Strip(capacity, 2, this.pitMaterial);
    this.decal = new Strip(START_BEHIND + START_AHEAD + 1, 2, this.decalMaterial);
    this.kerbs = new KerbList(capacity * 2, this.kerbMaterial);
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
    this.poolTexture = paintPool();
    const poolGeo = new THREE.PlaneGeometry(1, 1).rotateX(-Math.PI / 2);
    this.pools = new THREE.InstancedMesh(poolGeo, new THREE.MeshBasicMaterial({
      map: this.poolTexture, color: '#ffb45a', transparent: true, opacity: 0.22, blending: THREE.AdditiveBlending, depthWrite: false,
      polygonOffset: true, polygonOffsetFactor: -3, polygonOffsetUnits: -3,
    }), MAX_POOLS);
    this.pools.frustumCulled = false;
    this.pools.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
    this.pools.count = 0;
    this.pools.visible = false;
    this.pools.renderOrder = 2;
    this.group.add(this.asphalt.mesh, this.shoulders.mesh, this.kerbs.mesh, this.pit.mesh, this.decal.mesh, this.pools);
    this.shoulders.mesh.position.y = 0.004;
    this.pit.mesh.position.y = 0.012;
    this.decal.mesh.position.y = 0.015;
    this.decal.mesh.renderOrder = 1;
  }

  setPalette(p: Palette, night: boolean, key: string): void {
    if (key === this.paletteKey) return;
    this.paletteKey = key;
    this.night = night;
    for (const t of this.textures) t.dispose();
    const asphalt = paintAsphalt(p, this.anisotropy);
    const kerb = paintKerb(p, this.anisotropy);
    const shoulder = paintShoulder(p, this.anisotropy);
    const pit = paintPit(p, this.anisotropy);
    const decal = paintStartDecal(p, SEGMENT_M, this.anisotropy);
    this.textures = [asphalt.map, asphalt.glow, kerb, shoulder, pit, decal];
    const a = this.asphaltMaterial;
    a.map = asphalt.map;
    a.emissiveMap = asphalt.glow;
    a.emissive.set(p.lane);
    a.emissiveIntensity = p.markingGlow;
    // À noite o asfalto fica acetinado: pega o brilho das poças e dos faróis sem virar espelho.
    a.roughness = night ? 0.6 : 0.9;
    a.envMapIntensity = night ? 0.9 : 0.15; // de dia o céu no env map pintava a sombra de azul-marinho
    a.needsUpdate = true;
    this.kerbMaterial.map = kerb;
    this.kerbMaterial.emissiveMap = kerb;
    this.kerbMaterial.emissiveIntensity = p.markingGlow * 0.5;
    this.kerbMaterial.needsUpdate = true;
    this.shoulderMaterial.map = shoulder;
    this.shoulderMaterial.needsUpdate = true;
    this.pitMaterial.map = pit;
    this.pitMaterial.needsUpdate = true;
    this.decalMaterial.map = decal;
    this.decalMaterial.emissiveMap = decal;
    this.decalMaterial.emissiveIntensity = p.markingGlow * 0.6;
    this.decalMaterial.needsUpdate = true;
    // Faixas de 3 segmentos um pouco mais claras/escuras: o ritmo que dá sensação de velocidade.
    this.cBand[0].setRGB(1, 1, 1); this.cBand[1].setRGB(0.93, 0.93, 0.94);
    this.pools.material.color.set(p.lampPool ?? '#000000');
  }

  update(frame: RoadFrame, track: Track): void {
    const segs = track.segments;
    const n = frame.count;
    const W = ROAD_HALF_WIDTH_M;
    let pitPoints = 0;
    let prevPit = false;
    let decalPoints = 0;
    let pools = 0;
    const nSeg = segs.length;
    this.kerbs.begin();
    const d = this.dummy;
    for (let j = 0; j < n; j++) {
      const s = segs[frame.segIndex[j]];
      const h = frame.heading[j];
      const cx = Math.cos(h); const sz = Math.sin(h);
      const px = frame.px[j]; const py = frame.py[j]; const pz = frame.pz[j];
      const dist = s.index * SEGMENT_M;
      const v = dist / ASPHALT_REPEAT_M;
      const band = this.cBand[s.band];
      this.asphalt.set(j, 0, px - W * cx, py, pz - W * sz, 0, v, band.r, band.g, band.b);
      this.asphalt.set(j, 1, px + W * cx, py, pz + W * sz, 1, v, band.r, band.g, band.b);
      // Acostamento: u = 0 junto do asfalto, 1 no terreno (a textura escurece a borda de dentro).
      const o = W + SHOULDER_M;
      const vs = dist / SHOULDER_REPEAT_M;
      this.shoulders.set(j, 0, px - o * cx, py, pz - o * sz, 1, vs, 1, 1, 1);
      this.shoulders.set(j, 1, px - W * cx, py, pz - W * sz, 0, vs, 1, 1, 1);
      this.shoulders.set(j, 2, px + W * cx, py, pz + W * sz, 0, vs, 1, 1, 1);
      this.shoulders.set(j, 3, px + o * cx, py, pz + o * sz, 1, vs, 1, 1, 1);
      if (j + 1 < n && hasKerb(s)) {
        const v0 = dist / KERB_REPEAT_M; const v1 = (dist + SEGMENT_M) / KERB_REPEAT_M;
        this.kerbs.add(frame, j, j + 1, -1, v0, v1);
        this.kerbs.add(frame, j, j + 1, 1, v0, v1);
      }
      // Box: só nos segmentos `pit` (mais o ponto que fecha o trecho), compactados no começo
      // do buffer. atalho: um só trecho de box por pista; dois trechos na mesma janela
      // ficariam ligados por um quad esticado.
      if (s.pit || prevPit) {
        const x0 = PIT_X0 * W; const x1 = PIT_X1 * W;
        const pv = dist / PIT_REPEAT_M;
        this.pit.set(pitPoints, 0, px + x0 * cx, py, pz + x0 * sz, 0, pv, 1, 1, 1);
        this.pit.set(pitPoints, 1, px + x1 * cx, py, pz + x1 * sz, 1, pv, 1, 1, 1);
        pitPoints++;
      }
      prevPit = s.pit;
      // Decalque da largada: START_BEHIND segmentos antes da linha até START_AHEAD depois.
      let rel = (s.index - track.startIndex) % nSeg;
      if (rel < 0) rel += nSeg;
      if (rel > nSeg / 2) rel -= nSeg;
      if (rel >= -START_BEHIND && rel <= START_AHEAD && decalPoints < START_BEHIND + START_AHEAD + 1) {
        const dv = (rel + START_BEHIND) / (START_BEHIND + START_AHEAD);
        this.decal.set(decalPoints, 0, px - W * cx, py, pz - W * sz, 0, dv, 1, 1, 1);
        this.decal.set(decalPoints, 1, px + W * cx, py, pz + W * sz, 1, dv, 1, 1, 1);
        decalPoints++;
      }
      // Poças de luz dos postes (só à noite): no meio do segmento, sob a luminária.
      if (this.night && j + 1 < n && pools < MAX_POOLS && s.sprites.length > 0) {
        for (const sp of s.sprites) {
          if (sp.kind !== 'lamp' || pools >= MAX_POOLS) continue;
          const hm = (h + frame.heading[j + 1]) * 0.5;
          const mcx = Math.cos(hm); const msz = Math.sin(hm);
          const xm = sp.x * W + POOL_OFFSET_M;
          d.position.set((px + frame.px[j + 1]) * 0.5 + xm * mcx, (py + frame.py[j + 1]) * 0.5 + 0.03, (pz + frame.pz[j + 1]) * 0.5 + xm * msz);
          d.rotation.set(0, -hm, 0);
          d.scale.set(POOL_W, 1, POOL_L);
          d.updateMatrix();
          this.pools.setMatrixAt(pools++, d.matrix);
        }
      }
    }
    this.asphalt.finish(n, true);
    this.shoulders.finish(n, false);
    this.kerbs.finish();
    this.pit.finish(pitPoints, false);
    this.pit.mesh.visible = pitPoints > 1;
    this.decal.finish(decalPoints, true);
    this.decal.mesh.visible = decalPoints > 1;
    this.pools.count = pools;
    this.pools.visible = pools > 0; // sem instância, sem chamada de desenho
    if (pools > 0) this.pools.instanceMatrix.needsUpdate = true;
  }

  dispose(): void {
    this.asphalt.dispose(); this.shoulders.dispose(); this.pit.dispose(); this.decal.dispose(); this.kerbs.dispose();
    this.asphaltMaterial.dispose(); this.shoulderMaterial.dispose(); this.kerbMaterial.dispose(); this.pitMaterial.dispose(); this.decalMaterial.dispose();
    this.pools.geometry.dispose(); this.pools.material.dispose(); this.pools.dispose();
    this.poolTexture.dispose();
    for (const t of this.textures) t.dispose();
  }
}
