// Kit dos marcos turísticos: um acumulador de peças por material (liso, luz, baliza, fachadas) e as formas que os
// marcos repetem — viga entre dois pontos (cabo, pilar inclinado, braço), sólido de revolução (cúpula, cuia, torre
// redonda), fachada com janelas que acendem à noite, morro facetado com saia abaixo do chão. Puro: só Three.
// A convenção do modelo está em types.ts (origem no centro da base, frente para +X, metros reais).
import * as THREE from 'three';
import { FACADE_TILE } from '../structures';
import { jitter, merge, paint, shadeY, speckle, tintUp, type Geo, type MatKey, type Model, type ModelPart } from '../geom';

type FacadeStyle = keyof typeof FACADE_TILE;

/** Peças de um marco por material; `model()` funde cada grupo numa parte (uma geometria por lote). */
export class Kit {
  readonly flat: Geo[] = [];
  readonly glow: Geo[] = [];
  readonly beacon: Geo[] = [];
  private readonly facades = new Map<FacadeStyle, Geo[]>();

  /** Peça lisa já pintada (paint). */
  add(...g: Geo[]): this { this.flat.push(...g); return this; }
  light(...g: Geo[]): this { this.glow.push(...g); return this; }
  blink(...g: Geo[]): this { this.beacon.push(...g); return this; }
  facade(style: FacadeStyle, g: Geo): this {
    const list = this.facades.get(style) ?? [];
    list.push(g);
    this.facades.set(style, list);
    return this;
  }

  /**
   * Funde. `shade` = [y0, y1] do sombreado por altura (pé mais escuro, topo mais claro); `seed`/`speck` variam o tom
   * de cada face (pedra, mata).
   */
  model(shade: [number, number], speck = 0, seed = 1): Model {
    const parts: ModelPart[] = [];
    if (this.flat.length) {
      let g = shadeY(merge(this.flat), shade[0], shade[1], 0.8, 1.06);
      if (speck > 0) g = speckle(g, speck, seed);
      parts.push({ geometry: g, mat: 'flat' });
    }
    for (const [style, list] of this.facades) parts.push({ geometry: merge(list), mat: style as MatKey });
    if (this.glow.length) parts.push({ geometry: merge(this.glow), mat: 'glow' });
    if (this.beacon.length) parts.push({ geometry: merge(this.beacon), mat: 'beacon' });
    return { parts, blob: 0 };
  }
}

const UP = new THREE.Vector3(0, 1, 0);
const tmpA = new THREE.Vector3();
const tmpB = new THREE.Vector3();
const tmpQ = new THREE.Quaternion();

/** Matriz que leva o eixo +Y (de 0 a 1) ao segmento de `a` a `b`. */
function along(a: [number, number, number], b: [number, number, number], w: number, d: number): THREE.Matrix4 {
  tmpA.set(a[0], a[1], a[2]); tmpB.set(b[0], b[1], b[2]);
  const dir = tmpB.clone().sub(tmpA);
  const len = dir.length();
  tmpQ.setFromUnitVectors(UP, dir.normalize());
  const mid = tmpA.add(tmpB).multiplyScalar(0.5);
  return new THREE.Matrix4().compose(mid, tmpQ, new THREE.Vector3(w, len, d));
}

/** Viga de seção retangular `w` × `d` entre dois pontos (12 triângulos). */
export function beam(a: [number, number, number], b: [number, number, number], w: number, color: THREE.ColorRepresentation, d = w): Geo {
  return paint(new THREE.BoxGeometry(1, 1, 1), color, along(a, b, w, d));
}

/** Cabo: prisma triangular aberto entre dois pontos (6 triângulos), espessura `t`. */
export function cable(a: [number, number, number], b: [number, number, number], t: number, color: THREE.ColorRepresentation): Geo {
  return paint(new THREE.CylinderGeometry(0.5, 0.5, 1, 3, 1, true), color, along(a, b, t, t));
}

/** Sólido de revolução em torno de Y: `profile` = [raio, altura] de baixo para cima; `seg` lados. */
export function lathe(profile: Array<[number, number]>, seg: number, color: THREE.ColorRepresentation, m?: THREE.Matrix4): Geo {
  const pts = profile.map(([r, y]) => new THREE.Vector2(Math.max(0.0001, r), y));
  return paint(new THREE.LatheGeometry(pts, seg), color, m);
}

/** Caixa com uv em metros para a textura de fachada (`structures.FACADE_TILE`): janelas de ~3 m que acendem à noite. */
export function facadeBox(style: FacadeStyle, w: number, h: number, d: number, color: THREE.ColorRepresentation, m: THREE.Matrix4, vOffset = 0): Geo {
  const g = new THREE.BoxGeometry(w, h, d);
  const uv = g.attributes.uv as THREE.BufferAttribute;
  const [tu, tv] = FACADE_TILE[style];
  const faces: Array<[number, number] | null> = [[d, h], [d, h], null, null, [w, h], [w, h]];
  for (let f = 0; f < 6; f++) {
    const s = faces[f];
    for (let k = 0; k < 4; k++) {
      const i = f * 4 + k;
      if (!s) uv.setXY(i, 0.01, 0.01);
      else uv.setXY(i, (uv.getX(i) * s[0]) / tu, (uv.getY(i) * s[1] + vOffset) / tv);
    }
  }
  return paint(g, color, m, true);
}

/**
 * Morro facetado: icosaedro deformado por hash, achatado em `rx` × `h` × `rz`, com a base `skirt` metros abaixo do
 * chão (pousa em declive sem mostrar o fundo). `top` pinta as faces de cima (mata, grama), `rock` as encostas.
 */
export function hill(rx: number, h: number, rz: number, seed: number, rock: string, top: string | null, detail = 1, amount = 0.16, skirt = 0.25, m?: THREE.Matrix4): Geo {
  const g = jitter(new THREE.IcosahedronGeometry(1, detail), amount, seed);
  // Meia esfera de cima vira o morro; a de baixo, a saia (achatada para baixo do chão).
  const p = g.attributes.position as THREE.BufferAttribute;
  for (let i = 0; i < p.count; i++) {
    const y = p.getY(i);
    p.setXYZ(i, p.getX(i) * rx, y >= 0 ? y * h : y * h * skirt, p.getZ(i) * rz);
  }
  const out = paint(g, rock, m);
  if (top) tintUp(out, top, 0.55);
  return out;
}
