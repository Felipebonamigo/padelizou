// Cenário de beira de pista: cada SpriteKind × variante vira um modelo low-poly (geometrias
// fundidas com cor por vértice) desenhado por InstancedMesh. A cada quadro, para cada
// viewport, os sprites da janela do RoadFrame viram matrizes de instância. Tamanhos batem com
// SPRITE_HALF_WIDTH (largura visual ≈ 2 × meia largura × 7 m × scale).
import * as THREE from 'three';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';
import type { SpriteKind, Track } from '../core/types';
import { hash2, hash3 } from './noise';
import type { RoadFrame } from './roadframe';
import { chevronPanel, textPanel, windowTextures } from './textures';
import { ROAD_HALF_WIDTH_M } from './units';

type Geo = THREE.BufferGeometry;

interface Part {
  geometry: Geo;
  material: THREE.Material;
  castShadow: boolean;
  /** Visível só à noite (cone de luz do poste). */
  nightOnly?: boolean;
}

interface ModelDef {
  parts: Part[];
  /** A frente do modelo (+X) olha para a pista: espelha quando o sprite está à direita. */
  facesRoad?: boolean;
  /** Giro aleatório por instância (vegetação, pedras). */
  randomYaw?: boolean;
  /** Variação de tom por instância. */
  tint?: boolean;
  max: number;
}

interface InstanceSet {
  def: ModelDef;
  meshes: THREE.InstancedMesh[];
  count: number;
}

const VARIANTS: Record<SpriteKind, number> = {
  tree: 4, pine: 4, palm: 4, cactus: 4, bush: 4, boulder: 4, building: 12, tower: 2, lamp: 1, billboard: 8,
  sign_left: 1, sign_right: 1, grandstand: 2, banner_start: 1, pit_wall: 1, pit_sign: 1, cone: 1,
};

/** Escalas bakeadas das geometrias de prédio; a instância só corrige o resto. */
const BUILDING_BUCKETS = [1.0, 1.7, 2.6];

const BILLBOARDS: Array<[string, string, string]> = [
  ['NITRO', '#ff3b3b', '#ffffff'], ['CREW', '#1e88e5', '#ffffff'], ['PADELIZOU', '#0f8b5f', '#ffffff'], ['TOP SPEED', '#111111', '#ffd23f'],
  ['BOX →', '#ffd23f', '#111111'], ['RÁDIO 66', '#8e24aa', '#ffffff'], ['CAFÉ', '#6b3e1e', '#ffe9c0'], ['PNEUS', '#e6e6e6', '#111111'],
];

// ───────────────────────────── Utilitários de modelagem ─────────────────────────────

function tf(x: number, y: number, z: number, sx = 1, sy = 1, sz = 1, rx = 0, ry = 0, rz = 0): THREE.Matrix4 {
  const q = new THREE.Quaternion().setFromEuler(new THREE.Euler(rx, ry, rz, 'YXZ'));
  return new THREE.Matrix4().compose(new THREE.Vector3(x, y, z), q, new THREE.Vector3(sx, sy, sz));
}

/** Cópia não indexada com cor por vértice e transformação aplicada. */
function paint(geo: Geo, color: string, m?: THREE.Matrix4): Geo {
  const g = geo.index ? geo.toNonIndexed() : geo.clone();
  if (geo.index) geo.dispose();
  if (m) g.applyMatrix4(m);
  const c = new THREE.Color(color);
  const n = g.attributes.position.count;
  const arr = new Float32Array(n * 3);
  for (let i = 0; i < n; i++) { arr[i * 3] = c.r; arr[i * 3 + 1] = c.g; arr[i * 3 + 2] = c.b; }
  g.setAttribute('color', new THREE.BufferAttribute(arr, 3));
  return g;
}

/** Deforma radialmente por hash da posição (vértices coincidentes recebem o mesmo desvio). */
function jitter(geo: Geo, amount: number, seed: number): Geo {
  const p = geo.attributes.position as THREE.BufferAttribute;
  for (let i = 0; i < p.count; i++) {
    const x = p.getX(i); const y = p.getY(i); const z = p.getZ(i);
    const k = hash3(Math.round(x * 50), Math.round(y * 50) + seed * 131, Math.round(z * 50));
    const f = 1 + (k - 0.5) * 2 * amount;
    p.setXYZ(i, x * f, y * f, z * f);
  }
  geo.computeVertexNormals();
  return geo;
}

function merge(parts: Geo[]): Geo {
  const g = mergeGeometries(parts, false);
  if (!g) throw new Error('fusão de geometria falhou');
  for (const p of parts) p.dispose();
  g.computeVertexNormals();
  return g;
}

function box(w: number, h: number, d: number): THREE.BoxGeometry { return new THREE.BoxGeometry(w, h, d); }
function cyl(rt: number, rb: number, h: number, seg: number): THREE.CylinderGeometry { return new THREE.CylinderGeometry(rt, rb, h, seg); }

/** Caixa com UV em metros para o azulejo de janelas (6 m por repetição). */
function meteredBox(w: number, h: number, d: number): THREE.BoxGeometry {
  const g = box(w, h, d);
  const uv = g.attributes.uv as THREE.BufferAttribute;
  const faces: Array<[number, number]> = [[d, h], [d, h], [w, d], [w, d], [w, h], [w, h]];
  for (let f = 0; f < 6; f++) {
    const [su, sv] = faces[f];
    for (let k = 0; k < 4; k++) { const i = f * 4 + k; uv.setXY(i, uv.getX(i) * su / 6, uv.getY(i) * sv / 6); }
  }
  return g;
}

// ───────────────────────────── Modelos ─────────────────────────────

export class Scenery {
  readonly group = new THREE.Group();
  private readonly sets = new Map<string, InstanceSet>();
  private readonly flat: THREE.MeshStandardMaterial;
  private readonly wall: THREE.MeshStandardMaterial;
  private readonly lampHead: THREE.MeshStandardMaterial;
  private readonly lampCone: THREE.MeshBasicMaterial;
  private readonly towerLight: THREE.MeshStandardMaterial;
  private readonly panels: THREE.MeshStandardMaterial[] = [];
  private readonly textures: THREE.Texture[] = [];
  private readonly dummy = new THREE.Object3D();
  private readonly tmpColor = new THREE.Color();
  private night = false;

  constructor() {
    this.flat = new THREE.MeshStandardMaterial({ vertexColors: true, flatShading: true, roughness: 0.88, metalness: 0 });
    const win = windowTextures();
    this.wall = new THREE.MeshStandardMaterial({ vertexColors: true, map: win.wall, emissiveMap: win.win, emissive: '#ffd27a', emissiveIntensity: 0, roughness: 0.75 });
    this.lampHead = new THREE.MeshStandardMaterial({ color: '#fff4d0', emissive: '#fff1c4', emissiveIntensity: 0.2, roughness: 0.5 });
    this.lampCone = new THREE.MeshBasicMaterial({ color: '#ffd98a', transparent: true, opacity: 0.055, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide });
    this.towerLight = new THREE.MeshStandardMaterial({ color: '#ff2a2a', emissive: '#ff2020', emissiveIntensity: 2, roughness: 0.4 });
    this.build();
  }

  private panelMaterial(tex: THREE.Texture, emissive = true): THREE.MeshStandardMaterial {
    this.textures.push(tex);
    const m = new THREE.MeshStandardMaterial({ map: tex, emissiveMap: emissive ? tex : null, emissive: '#ffffff', emissiveIntensity: 0, roughness: 0.6 });
    this.panels.push(m);
    return m;
  }

  private add(kind: SpriteKind, variant: number, def: ModelDef): void {
    const meshes: THREE.InstancedMesh[] = [];
    for (const part of def.parts) {
      const im = new THREE.InstancedMesh(part.geometry, part.material, def.max);
      im.castShadow = part.castShadow;
      im.receiveShadow = part.castShadow;
      im.frustumCulled = false;
      im.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
      im.count = 0;
      im.visible = false;
      if (part.nightOnly) im.userData.nightOnly = true;
      this.group.add(im);
      meshes.push(im);
    }
    this.sets.set(`${kind}:${variant}`, { def, meshes, count: 0 });
  }

  private build(): void {
    const flat = this.flat;
    const solid = (geometry: Geo): Part => ({ geometry, material: flat, castShadow: true });

    // Árvore: tronco + copa de icosaedro deformado.
    const treeGreens = ['#2f8f3a', '#3aa043', '#2a7f45', '#4ea33c'];
    for (let v = 0; v < 4; v++) {
      const s = 0.9 + v * 0.08;
      const trunk = paint(cyl(0.16, 0.26, 2.6, 7), '#6b4a2b', tf(0, 1.3, 0));
      const canopy = paint(jitter(new THREE.IcosahedronGeometry(1.75 * s, 1), 0.16, v), treeGreens[v], tf(0, 3.4 + v * 0.2, 0, 1, 0.85, 1));
      this.add('tree', v, { parts: [solid(merge([trunk, canopy]))], randomYaw: true, tint: true, max: 120 });
    }
    // Pinheiro: tronco + 3 cones.
    const pineGreens = ['#2a6f3a', '#1f5e33', '#2f7a42', '#25683b'];
    for (let v = 0; v < 4; v++) {
      const f = [1, 0.85, 1.15, 1.0][v];
      const parts = [paint(cyl(0.14, 0.22, 1.9, 7), '#5a3d24', tf(0, 0.95, 0))];
      const tiers: Array<[number, number, number]> = [[1.55, 2.9, 2.7], [1.2, 2.6, 4.3], [0.78, 2.4, 5.8]];
      for (const [r, h, y] of tiers) parts.push(paint(new THREE.ConeGeometry(r * f, h * f, 7), pineGreens[v], tf(0, y * f, 0)));
      this.add('pine', v, { parts: [solid(merge(parts))], randomYaw: true, tint: true, max: 120 });
    }
    // Palmeira: tronco curvo em pedaços + 8 folhas + cocos.
    for (let v = 0; v < 4; v++) {
      const lean = [0.7, -0.6, 1.0, 0.25][v];
      const parts: Geo[] = [];
      const pieces = 7;
      for (let k = 0; k < pieces; k++) {
        const t0 = k / pieces; const t1 = (k + 1) / pieces;
        const x0 = lean * t0 * t0; const x1 = lean * t1 * t1;
        const y0 = t0 * 6.4; const y1 = t1 * 6.4;
        const ang = Math.atan2(x1 - x0, y1 - y0);
        const r = 0.25 - 0.1 * t0;
        parts.push(paint(cyl(r - 0.015, r, 1.0, 7), k % 2 ? '#8a6a3f' : '#7d5f38', tf((x0 + x1) / 2, (y0 + y1) / 2, 0, 1, 1, 1, 0, 0, -ang)));
      }
      const topX = lean;
      for (let k = 0; k < 8; k++) {
        const yaw = (k / 8) * Math.PI * 2 + v * 0.3;
        const tilt = 0.55 + hash2(v, k) * 0.35;
        const leaf = paint(box(0.45, 0.05, 3.0), k % 2 ? '#3f9a3a' : '#48a844', tf(0, 0, 1.45));
        leaf.applyMatrix4(tf(topX, 6.5, 0, 1, 1, 1, tilt, yaw, 0));
        parts.push(leaf);
      }
      for (let k = 0; k < 3; k++) parts.push(paint(new THREE.SphereGeometry(0.16, 6, 5), '#5a3a1a', tf(topX + Math.cos(k * 2.1) * 0.25, 6.25, Math.sin(k * 2.1) * 0.25)));
      this.add('palm', v, { parts: [solid(merge(parts))], randomYaw: true, tint: true, max: 120 });
    }
    // Cacto: coluna + 2 braços.
    for (let v = 0; v < 4; v++) {
      const f = [1, 0.8, 1.2, 0.9][v];
      const green = ['#3f8a3a', '#4a9a44', '#357a33', '#3f8a3a'][v];
      const parts = [paint(cyl(0.3 * f, 0.36 * f, 3.4 * f, 8), green, tf(0, 1.7 * f, 0)), paint(new THREE.SphereGeometry(0.3 * f, 8, 6), green, tf(0, 3.4 * f, 0))];
      for (const [side, h] of [[1, 1.4], [-1, 2.0]] as const) {
        parts.push(paint(cyl(0.18 * f, 0.18 * f, 0.8 * f, 7), green, tf(side * 0.45 * f, h * f, 0, 1, 1, 1, 0, 0, Math.PI / 2)));
        parts.push(paint(cyl(0.17 * f, 0.19 * f, 1.3 * f, 7), green, tf(side * 0.8 * f, (h + 0.6) * f, 0)));
        parts.push(paint(new THREE.SphereGeometry(0.18 * f, 7, 5), green, tf(side * 0.8 * f, (h + 1.25) * f, 0)));
      }
      this.add('cactus', v, { parts: [solid(merge(parts))], randomYaw: true, max: 80 });
    }
    // Arbusto: 3 icosaedros.
    const bushGreens = ['#3b8a35', '#5a9a3a', '#2f7a40', '#7a9a3a'];
    for (let v = 0; v < 4; v++) {
      const parts = [
        paint(jitter(new THREE.IcosahedronGeometry(0.95, 0), 0.1, v), bushGreens[v], tf(0, 0.7, 0)),
        paint(jitter(new THREE.IcosahedronGeometry(0.7, 0), 0.1, v + 5), bushGreens[(v + 1) % 4], tf(0.75, 0.55, 0.25)),
        paint(jitter(new THREE.IcosahedronGeometry(0.62, 0), 0.1, v + 9), bushGreens[v], tf(-0.65, 0.5, -0.3)),
      ];
      this.add('bush', v, { parts: [solid(merge(parts))], randomYaw: true, tint: true, max: 120 });
    }
    // Pedra: icosaedro achatado e deformado.
    const grays = ['#8a8a86', '#7a7268', '#9a9080', '#6f6f6f'];
    for (let v = 0; v < 4; v++) {
      const rock = paint(jitter(new THREE.IcosahedronGeometry(2.1, 1), 0.22, v + 20), grays[v], tf(0, 0.85, 0, 1, 0.62, 0.85));
      this.add('boulder', v, { parts: [solid(merge([rock]))], randomYaw: true, max: 80 });
    }
    // Prédio: caixas com azulejo de janelas (emissivo à noite).
    const walls = ['#cfcac2', '#9aa5b1', '#6f7f95', '#b9a68f'];
    const shapes: Array<Array<[number, number, number, number, number, number]>> = [
      [[12, 22, 10, 0, 11, 0]], [[12, 14, 12, 0, 7, 0]], [[9, 30, 9, 0, 15, 0], [4, 2.5, 4, 0, 31.2, 0]], [[12, 12, 10, 0, 6, 0], [8, 10, 8, -1, 17, 0]],
    ];
    // Três classes de escala por forma (BUILDING_BUCKETS), para o azulejo de janelas não esticar.
    for (let v = 0; v < 4; v++) for (let b = 0; b < BUILDING_BUCKETS.length; b++) {
      const k = BUILDING_BUCKETS[b];
      const parts = shapes[v].map(([w, h, d, x, y, z], i) => paint(meteredBox(w * k, h * k, d * k), i === 1 && v === 2 ? '#3a3f48' : walls[v], tf(x * k, y * k, z * k)));
      this.add('building', v * BUILDING_BUCKETS.length + b, { parts: [{ geometry: merge(parts), material: this.wall, castShadow: true }], max: 40 });
    }
    // Torre: pirâmide de 4 lados + plataforma + antena + luz vermelha piscando.
    for (let v = 0; v < 2; v++) {
      const f = [1, 1.3][v];
      const body = merge([
        paint(cyl(1.0, 2.6, 24 * f, 4), '#8a8f96', tf(0, 12 * f, 0, 1, 1, 1, 0, Math.PI / 4, 0)),
        paint(box(3.4, 0.4, 3.4), '#5f646b', tf(0, 18 * f, 0)),
        paint(cyl(0.12, 0.16, 8, 6), '#c7c9cc', tf(0, 24 * f + 4, 0)),
      ]);
      const light = new THREE.SphereGeometry(0.5, 8, 6).translate(0, 24 * f + 8.3, 0);
      this.add('tower', v, { parts: [solid(body), { geometry: light, material: this.towerLight, castShadow: false }], max: 30 });
    }
    // Poste: haste + braço, luminária emissiva e cone de luz falso (só à noite).
    {
      const pole = merge([paint(cyl(0.1, 0.15, 8, 8), '#6e737a', tf(0, 4, 0)), paint(box(1.9, 0.12, 0.12), '#6e737a', tf(0.9, 8, 0))]);
      const head = box(0.75, 0.18, 0.36).translate(1.7, 7.95, 0);
      const cone = new THREE.ConeGeometry(3.4, 7.7, 14, 1, true).translate(1.7, 4.05, 0);
      this.add('lamp', 0, { parts: [solid(pole), { geometry: head, material: this.lampHead, castShadow: false }, { geometry: cone, material: this.lampCone, castShadow: false, nightOnly: true }], max: 60 });
    }
    // Outdoor: postes + moldura + painel com texto (8 variantes).
    {
      const frame = merge([
        paint(cyl(0.12, 0.14, 4.2, 7), '#4a4e55', tf(-3.0, 2.1, 0)), paint(cyl(0.12, 0.14, 4.2, 7), '#4a4e55', tf(3.0, 2.1, 0)),
        paint(box(8.0, 3.9, 0.18), '#2a2a2e', tf(0, 5.75, 0)),
      ]);
      for (let v = 0; v < 8; v++) {
        const [text, bg, fg] = BILLBOARDS[v];
        const panel = new THREE.PlaneGeometry(7.6, 3.5).translate(0, 5.75, 0.1);
        this.add('billboard', v, { parts: [solid(frame), { geometry: panel, material: this.panelMaterial(textPanel(text, bg, fg)), castShadow: false }], max: 12 });
      }
    }
    // Placas de curva (chevrons).
    for (const [kind, dir] of [['sign_left', -1], ['sign_right', 1]] as Array<[SpriteKind, -1 | 1]>) {
      const posts = merge([
        paint(cyl(0.07, 0.07, 2.0, 6), '#5a5e66', tf(-1.2, 1.0, 0)), paint(cyl(0.07, 0.07, 2.0, 6), '#5a5e66', tf(1.2, 1.0, 0)),
        paint(box(3.1, 1.1, 0.08), '#33363c', tf(0, 2.25, -0.05)),
      ]);
      const panel = new THREE.PlaneGeometry(3, 1).translate(0, 2.25, 0.01);
      this.add(kind, 0, { parts: [solid(posts), { geometry: panel, material: this.panelMaterial(chevronPanel(dir), false), castShadow: false }], max: 24 });
    }
    // Arquibancada: degraus + torcida colorida + cobertura. A frente (+X) olha para a pista.
    const crowd = ['#ff3b3b', '#ffd23f', '#3ddc84', '#4fc3f7', '#ff7ab6', '#ffffff', '#ff8c1a', '#8e24aa'];
    for (let v = 0; v < 2; v++) {
      const parts: Geo[] = [paint(box(7, 0.4, 14.4), '#9aa0a8', tf(-0.4, 0.2, 0))];
      for (let k = 0; k < 5; k++) {
        const x = 2.4 - k * 1.2; const y = 0.4 + k * 0.7;
        parts.push(paint(box(1.2, 0.7, 14), k % 2 ? '#b0b6be' : '#c8ccd2', tf(x, y + 0.35, 0)));
        for (let i = 0; i < 20; i++) {
          const c = crowd[Math.floor(hash3(v, k, i) * crowd.length)];
          parts.push(paint(box(0.36, 0.55, 0.36), c, tf(x + 0.15, y + 0.7 + 0.28, -6.3 + i * 0.66)));
        }
      }
      parts.push(paint(box(5.8, 0.2, 14.8), v === 0 ? '#ffd23f' : '#1e88e5', tf(-1.4, 5.3, 0, 1, 1, 1, 0, 0, 0.14)));
      parts.push(paint(cyl(0.12, 0.12, 5, 6), '#4a4e55', tf(-3.7, 2.5, 6.7)), paint(cyl(0.12, 0.12, 5, 6), '#4a4e55', tf(-3.7, 2.5, -6.7)));
      this.add('grandstand', v, { parts: [solid(merge(parts))], facesRoad: true, max: 8 });
    }
    // Arco de largada: dois pilares + viga + painel.
    {
      const arch = merge([
        paint(box(0.9, 8.5, 0.9), '#2b2f36', tf(-8.6, 4.25, 0)), paint(box(0.9, 8.5, 0.9), '#2b2f36', tf(8.6, 4.25, 0)),
        paint(box(18.6, 1.8, 0.7), '#f2f2f2', tf(0, 8.7, 0)),
      ]);
      const panel = new THREE.PlaneGeometry(17.6, 1.5).translate(0, 8.7, 0.37);
      this.add('banner_start', 0, { parts: [solid(arch), { geometry: panel, material: this.panelMaterial(textPanel('NITRO CREW', '#111111', '#ffd23f', 1024, 96, '#ff3b3b')), castShadow: false }], max: 2 });
    }
    // Muro de box: 12 m ao longo da pista, branco com faixa vermelha.
    this.add('pit_wall', 0, { parts: [solid(merge([paint(box(0.5, 0.8, 12), '#f4f4f4', tf(0, 0.4, 0)), paint(box(0.5, 0.3, 12), '#d63a3a', tf(0, 0.95, 0))]))], max: 30 });
    // Placa do box.
    {
      const post = merge([paint(cyl(0.07, 0.08, 2.3, 6), '#5a5e66', tf(0, 1.15, 0))]);
      const panel = new THREE.PlaneGeometry(1.7, 0.8).translate(0, 2.6, 0.02);
      this.add('pit_sign', 0, { parts: [solid(post), { geometry: panel, material: this.panelMaterial(textPanel('BOX →', '#ffd23f', '#111111', 256, 128)), castShadow: false }], max: 3 });
    }
    // Cone.
    this.add('cone', 0, { parts: [solid(merge([
      paint(new THREE.ConeGeometry(0.3, 0.72, 8), '#ff7a1a', tf(0, 0.36, 0)), paint(box(0.6, 0.04, 0.6), '#222222', tf(0, 0.02, 0)),
      paint(cyl(0.19, 0.24, 0.14, 8), '#f4f4f4', tf(0, 0.38, 0)),
    ]))], max: 6 });
  }

  setNight(night: boolean): void {
    this.night = night;
    this.wall.emissiveIntensity = night ? 1.0 : 0;
    this.lampHead.emissiveIntensity = night ? 3.5 : 0.2;
    for (const m of this.panels) m.emissiveIntensity = night ? 0.7 : 0;
  }

  update(frame: RoadFrame, track: Track, time: number): void {
    for (const set of this.sets.values()) set.count = 0;
    const segs = track.segments;
    const d = this.dummy;
    const n = frame.count;
    for (let j = 0; j < n - 1; j++) {
      const s = segs[frame.segIndex[j]];
      if (s.sprites.length === 0) continue;
      const h = (frame.heading[j] + frame.heading[j + 1]) * 0.5;
      const cx = Math.cos(h); const sz = Math.sin(h);
      const px = (frame.px[j] + frame.px[j + 1]) * 0.5;
      const py = (frame.py[j] + frame.py[j + 1]) * 0.5;
      const pz = (frame.pz[j] + frame.pz[j + 1]) * 0.5;
      for (let k = 0; k < s.sprites.length; k++) {
        const sp = s.sprites[k];
        let variant = sp.variant % VARIANTS[sp.kind];
        let scale = sp.scale;
        if (sp.kind === 'building') {
          let bucket = 0;
          for (let b = 1; b < BUILDING_BUCKETS.length; b++) if (Math.abs(BUILDING_BUCKETS[b] - sp.scale) < Math.abs(BUILDING_BUCKETS[bucket] - sp.scale)) bucket = b;
          variant = (sp.variant % 4) * BUILDING_BUCKETS.length + bucket;
          scale = sp.scale / BUILDING_BUCKETS[bucket];
        }
        const set = this.sets.get(`${sp.kind}:${variant}`);
        if (!set || set.count >= set.def.max) continue;
        const def = set.def;
        let xm = sp.x * ROAD_HALF_WIDTH_M;
        if (sp.kind === 'building') {
          // atalho: o builder pode pôr prédio grande com a borda sobre o asfalto; empurra para
          // fora (a colisão do núcleo continua onde estava — corrigir no builder depois).
          const half = 6.3 * sp.scale;
          if (Math.abs(xm) - half < 11) xm = Math.sign(xm) * (11 + half);
        }
        const r1 = hash2(s.index * 7 + k, 1); const r2 = hash2(s.index * 7 + k, 2);
        d.position.set(px + xm * cx, py, pz + xm * sz);
        let yaw = -h;
        if (def.facesRoad && sp.x > 0) yaw += Math.PI;
        if (def.randomYaw) yaw += r1 * Math.PI * 2;
        d.rotation.set(0, yaw, 0);
        const sc = scale * (def.randomYaw ? 0.92 + r2 * 0.16 : 1);
        d.scale.set(sc, sc, sc);
        d.updateMatrix();
        const i = set.count++;
        for (const m of set.meshes) m.setMatrixAt(i, d.matrix);
        if (def.tint) {
          this.tmpColor.setRGB(0.88 + hash2(s.index, k + 3) * 0.24, 0.9 + hash2(s.index, k + 4) * 0.2, 0.86 + hash2(s.index, k + 5) * 0.2);
          set.meshes[0].setColorAt(i, this.tmpColor);
        }
      }
    }
    const blink = Math.sin(time * 5) > 0.2 ? 5 : 0.4;
    this.towerLight.emissiveIntensity = this.night ? blink : blink * 0.5;
    for (const set of this.sets.values()) {
      for (const m of set.meshes) {
        m.count = set.count;
        m.visible = set.count > 0 && !(m.userData.nightOnly === true && !this.night);
        if (set.count > 0) {
          m.instanceMatrix.needsUpdate = true;
          if (m.instanceColor) m.instanceColor.needsUpdate = true;
        }
      }
    }
  }

  dispose(): void {
    for (const set of this.sets.values()) for (const m of set.meshes) { m.geometry.dispose(); m.dispose(); }
    this.flat.dispose(); this.wall.dispose(); this.lampHead.dispose(); this.lampCone.dispose(); this.towerLight.dispose();
    for (const m of this.panels) m.dispose();
    for (const t of this.textures) t.dispose();
  }
}
