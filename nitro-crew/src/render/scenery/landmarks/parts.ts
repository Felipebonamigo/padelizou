// Peças baixadas dos marcos (docs/ARTE.md, "Peças baixadas"): um bicho ou uma estátua da galeria CC0 do Meshy,
// convertido para o estilo (`node tools/convert-landmark.mjs … --part <peça>`), entra como PEÇA de um marco
// procedural — não o substitui. O tuiuiú fica no ninho da árvore, os jacarés na baía, a manada de búfalos com as garças
// no lombo, o par de girafas, as renas na vila da Lapônia, o cavalo debaixo do cavaleiro da Cavalhada, o troll ao lado
// da placa, o par de shisas no muro e o garimpeiro de bronze no pedestal. O resto da cena continua em código.
//
// Convenção da peça (a do conversor no modo peça): metros, +Y para cima, base em y = 0, pegada centrada na origem, a
// FRENTE (a cabeça do bicho, o rosto da estátua) para +X; uma parte lisa só, sem índice, cor por vértice (cor chapada
// por face). Cada `src/assets/landmarks/parts/<peça>.glb` aceito pelo validador (check.ts, `checkLandmarkPart`) vira a
// peça; sem arquivo, o construtor do marco usa o bicho procedural de sempre (o fallback).
//
// O construtor usa a peça pelo kit (`landmarkPart` em kit.ts): `peça.at(encaixe, x, y, z, giro)` devolve uma CÓPIA
// escalada pelo encaixe — a altura, o comprimento ou o lombo do bicho procedural que ela substitui (a cena continua
// com as proporções em que foi desenhada, qualquer que seja a altura usada na conversão) —, girada em Y e com a base
// em (x, y, z). Determinístico: nada sorteado; a mesma peça dá o mesmo marco.
//
// Ordem: as peças carregam antes do renderizador (main.ts e os harnesses, logo antes dos marcos inteiros); e, se uma
// peça mudar depois de algum marco ter sido montado, o catálogo esquece os marcos (catalog.ts escuta
// `onLandmarkPartsChange`). Puro: só three.
import * as THREE from 'three';
import type { Geo } from '../geom';

export type PartName = 'tuiuiu' | 'jacare' | 'bufalo' | 'garca' | 'girafa' | 'rena' | 'cavalo' | 'troll' | 'shisa' | 'garimpeiro';

export interface PartSpec {
  /** Nome para as mensagens. */
  label: string;
  /** A medida de verdade (m) da conversão: a altura total (`height`) ou o comprimento ao longo de X (`length`). */
  size: number;
  measure: 'height' | 'length';
  /** Alvo de triângulos do conversor (`--part` sem `--tris`). */
  tris: number;
  /** Teto do validador: com todas as instâncias no teto, o marco cabe no orçamento (tests/landmark-parts.test.ts). */
  maxTris: number;
  /** Marcos que usam a peça e quantas instâncias cada um monta (o teste de orçamento conta com isto). */
  users: Readonly<Record<string, number>>;
}

/**
 * As peças da primeira leva. O teto de triângulos sai do orçamento do marco (perto: 3.500) menos o resto da cena,
 * dividido pelas instâncias (tests/landmark-parts.test.ts monta cada marco com todas as peças no teto):
 * - girafa ×2 (a cena é só o par), shisa ×2, troll ×1 e garimpeiro ×1 (estátuas sobre pouca coisa) levam detalhe;
 * - tuiuiú ×3 no ninho (resto da cena ~830) e rena ×3 na vila (~1.530), o meio-termo;
 * - os que vêm em bando dividem o que sobra: jacaré ×8 na baía (~1.420 de resto), búfalo ×9 com a garça ×3 no lombo
 *   (~1.010) e o cavalo ×6 sob os cavaleiros (~2.160: camarotes, castelos, os cavaleiros e as mantas).
 */
export const PART_SPECS: Readonly<Record<PartName, PartSpec>> = {
  tuiuiu: { label: 'tuiuiú', size: 1.6, measure: 'height', tris: 600, maxTris: 800, users: { tuiuiu_ninho: 3 } },
  jacare: { label: 'jacaré', size: 2.7, measure: 'length', tris: 220, maxTris: 250, users: { jacare: 8 } },
  bufalo: { label: 'búfalo', size: 1.7, measure: 'height', tris: 190, maxTris: 220, users: { bufalo: 9 } },
  garca: { label: 'garça', size: 1.0, measure: 'height', tris: 150, maxTris: 160, users: { bufalo: 3 } },
  girafa: { label: 'girafa', size: 5.0, measure: 'height', tris: 1200, maxTris: 1600, users: { girafa: 2 } },
  rena: { label: 'rena', size: 2.1, measure: 'height', tris: 500, maxTris: 600, users: { vila_lapponia: 3 } },
  cavalo: { label: 'cavalo', size: 2.3, measure: 'height', tris: 190, maxTris: 215, users: { cavalhada: 6 } },
  troll: { label: 'troll', size: 3.5, measure: 'height', tris: 1500, maxTris: 2000, users: { placa_trolls: 1 } },
  shisa: { label: 'shisa', size: 1.8, measure: 'height', tris: 1200, maxTris: 1600, users: { shisa: 2 } },
  garimpeiro: { label: 'garimpeiro', size: 2.5, measure: 'height', tris: 2000, maxTris: 2500, users: { monumento_garimpeiro: 1 } },
};

export const PART_NAMES = Object.keys(PART_SPECS) as readonly PartName[];

export function isPartName(name: string): name is PartName {
  return Object.prototype.hasOwnProperty.call(PART_SPECS, name);
}

/** Como a peça entra na cena: altura total, comprimento em X ou a altura do lombo/sela (`back`). */
export type PartFit = { height: number } | { length: number } | { back: number };

export interface PartBounds { minX: number; maxX: number; minY: number; maxY: number; minZ: number; maxZ: number }

const UP = new THREE.Vector3(0, 1, 0);

/** Uma peça registrada: a geometria (nunca alterada: cada uso é uma cópia), a caixa e o lombo. */
export class LandmarkPart {
  /** Só posição, normal e cor (Float32, 3 por vértice): funde com as peças pintadas do marco (`merge`). */
  readonly geometry: Geo;
  readonly bounds: PartBounds;
  readonly triangles: number;
  /**
   * Altura do lombo (ou da sela): o ponto mais alto da faixa do meio do comprimento (±10% em torno do centro de X) —
   * a cabeça, os chifres e a galhada ficam na ponta da frente e não contam. É onde o cavaleiro senta.
   */
  readonly back: number;

  constructor(readonly name: string, source: Geo) {
    const src = source.index ? source.toNonIndexed() : source;
    const pos = src.getAttribute('position');
    const col = src.getAttribute('color');
    const n = pos ? pos.count : 0;
    const p = new Float32Array(n * 3); const c = new Float32Array(n * 3);
    for (let i = 0; i < n; i++) {
      p[i * 3] = pos.getX(i); p[i * 3 + 1] = pos.getY(i); p[i * 3 + 2] = pos.getZ(i);
      if (col) { c[i * 3] = col.getX(i); c[i * 3 + 1] = col.getY(i); c[i * 3 + 2] = col.getZ(i); } else c.fill(1, i * 3, i * 3 + 3);
    }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.BufferAttribute(p, 3));
    g.setAttribute('color', new THREE.BufferAttribute(c, 3));
    g.computeVertexNormals();
    this.geometry = g;
    this.triangles = Math.floor(n / 3);
    const b: PartBounds = { minX: Infinity, maxX: -Infinity, minY: Infinity, maxY: -Infinity, minZ: Infinity, maxZ: -Infinity };
    for (let i = 0; i < n; i++) {
      const x = p[i * 3]; const y = p[i * 3 + 1]; const z = p[i * 3 + 2];
      if (x < b.minX) b.minX = x; if (x > b.maxX) b.maxX = x;
      if (y < b.minY) b.minY = y; if (y > b.maxY) b.maxY = y;
      if (z < b.minZ) b.minZ = z; if (z > b.maxZ) b.maxZ = z;
    }
    this.bounds = b;
    const cx = (b.minX + b.maxX) / 2; const band = (b.maxX - b.minX) * 0.1;
    let back = -Infinity;
    for (let i = 0; i < n; i++) if (Math.abs(p[i * 3] - cx) <= band && p[i * 3 + 1] > back) back = p[i * 3 + 1];
    this.back = Number.isFinite(back) && back > 0 ? back : Math.max(b.maxY, 1e-6);
  }

  /** Escala uniforme que leva a medida da peça ao encaixe pedido. */
  scaleFor(fit: PartFit): number {
    const b = this.bounds;
    if ('height' in fit) return fit.height / Math.max(1e-9, b.maxY);
    if ('length' in fit) return fit.length / Math.max(1e-9, b.maxX - b.minX);
    return fit.back / Math.max(1e-9, this.back);
  }

  /**
   * Cópia da peça na cena: escalada pelo encaixe, girada `yaw` em torno de Y (a frente, +X, vai para
   * (cos yaw, 0, −sen yaw): yaw = −π/2 põe a cabeça em +Z) e com a base em (x, y, z).
   */
  at(fit: PartFit, x: number, y: number, z: number, yaw = 0): Geo {
    const s = this.scaleFor(fit);
    const m = new THREE.Matrix4().compose(new THREE.Vector3(x, y, z), new THREE.Quaternion().setFromAxisAngle(UP, yaw), new THREE.Vector3(s, s, s));
    return this.geometry.clone().applyMatrix4(m);
  }
}

const registry = new Map<PartName, LandmarkPart>();
const listeners: Array<() => void> = [];

/** A peça carregada, ou null (o construtor usa o bicho procedural). */
export function landmarkPart(name: PartName): LandmarkPart | null {
  return registry.get(name) ?? null;
}

/** Registra (ou, com null, tira) a peça; quem escuta (o catálogo) esquece os marcos já montados. */
export function setLandmarkPart(name: PartName, geometry: Geo | null): void {
  if (geometry) registry.set(name, new LandmarkPart(name, geometry)); else registry.delete(name);
  for (const fn of listeners) fn();
}

/** Avisa quando uma peça muda (catalog.ts invalida os marcos montados e as medidas deles). */
export function onLandmarkPartsChange(fn: () => void): void {
  listeners.push(fn);
}
