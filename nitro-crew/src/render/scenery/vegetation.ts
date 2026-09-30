// Vegetação e pedras low-poly: espécies por bioma/país, com cor por vértice, sombreado por altura
// (pé escuro, topo claro) e variação por semente. Os modelos de sprite SÓLIDO chegam à meia largura
// de colisão (collisionHalfM) na altura do carro — pelas raízes, pela saia de folhagem ou pelo tronco —,
// para a batida acontecer quando o carro encosta, nem antes nem depois (tests/scenery.test.ts mede).
// Puro: só Three, sem DOM.
import * as THREE from 'three';
import type { SpriteKind } from '../../core/types';
import { SPRITE_HALF_WIDTH } from '../../core/track/sprites';
import { hash2 } from '../noise';
import { ROAD_HALF_WIDTH_M } from '../units';
import { box, cone, cyl, dodeca, frond, ico, jitter, lean, merge, paint, shadeY, speckle, sphere, tf, tintUp, tris, tuft, type Geo, type Model } from './geom';

/** Meia largura de colisão (m, escala 1) de um sprite: o modelo sólido tem de chegar até ela na altura do carro. */
export function collisionHalfM(kind: SpriteKind): number {
  return SPRITE_HALF_WIDTH[kind] * ROAD_HALF_WIDTH_M;
}

const TREE_R = collisionHalfM('tree');
const PINE_R = collisionHalfM('pine');
const PALM_R = collisionHalfM('palm');
const CACTUS_R = collisionHalfM('cactus');
const ROCK_R = collisionHalfM('boulder');

function flat(geometry: Geo, blob: number, shadow = true): Model {
  return { parts: [{ geometry, mat: 'flat', shadow }], blob };
}

/** Bolha de copa: icosaedro deformado e achatado. */
function blob(r: number, x: number, y: number, z: number, color: string, seed: number, sy = 0.8, detail = 0, amount = 0.14): Geo {
  return paint(jitter(ico(r, detail), amount, seed), color, tf(x, y, z, 1, sy, 1, 0, hash2(seed, 3) * 6.28, 0));
}

/** Tronco afinando, com leve inclinação. */
function trunk(r0: number, r1: number, h: number, color: string, seg = 6, lean = 0, dir = 0): Geo {
  return paint(cyl(r1, r0, h, seg), color, tf(Math.cos(dir) * lean * h * 0.5, h / 2, Math.sin(dir) * lean * h * 0.5, 1, 1, 1, Math.sin(dir) * lean, 0, -Math.cos(dir) * lean));
}

/**
 * Moita contínua em volta do pé (meia esfera achatada de 10 lados): a borda fica em `reach` em todas as
 * direções (≥ 95% entre os vértices), então a pegada não depende do giro. Três tufos em cima quebram o domo.
 */
function mound(reach: number, h: number, colors: string[], seed: number): Geo[] {
  const dome = new THREE.SphereGeometry(1, 10, 3, hash2(seed, 71) * 0.6, Math.PI * 2, 0, Math.PI / 2);
  const out: Geo[] = [speckle(paint(dome, colors[0], tf(0, 0, 0, reach, h * 0.8, reach)), 0.1, seed)];
  // Tufos por cima do domo, dentro da borda: a moita parece folhagem, não um pedestal.
  for (let k = 0; k < 4; k++) {
    const a = (k / 4) * Math.PI * 2 + hash2(seed, k + 80);
    const r = reach * (0.34 + hash2(seed, k + 90) * 0.1);
    const d = reach - r * 1.15;
    out.push(paint(jitter(ico(r, 0), 0.12, seed * 3 + k), colors[(k + 1) % colors.length], tf(Math.cos(a) * d, h * 0.45, Math.sin(a) * d, 1, h / r * 0.62, 1, 0, a, 0)));
  }
  return out;
}

/** Samambaia/folhagem rasteira: `n` folhas que alcançam `reach` a partir do centro. */
function fernRing(n: number, reach: number, w: number, colors: string[], seed: number, rise = 0.55, droop = 0.45, pieces = 3): Geo[] {
  const out: Geo[] = [];
  for (let k = 0; k < n; k++) {
    const a = (k / n) * Math.PI * 2 + hash2(seed, k + 40) * 0.6;
    out.push(paint(frond(reach, w, a, rise, droop, pieces), colors[k % colors.length], tf(0, 0.06, 0)));
  }
  return out;
}

/** Degradê de cor por altura dentro de uma peça: de nada em y0 até `color` pleno em y1. */
function topTint(g: Geo, y0: number, y1: number, color: string): Geo {
  const p = g.attributes.position as THREE.BufferAttribute;
  const c = g.attributes.color as THREE.BufferAttribute;
  const t = new THREE.Color(color);
  for (let i = 0; i < p.count; i++) {
    const k = Math.max(0, Math.min(1, (p.getY(i) - y0) / (y1 - y0)));
    c.setXYZ(i, c.getX(i) + (t.r - c.getX(i)) * k, c.getY(i) + (t.g - c.getY(i)) * k, c.getZ(i) + (t.b - c.getZ(i)) * k);
  }
  return g;
}

function finish(g: Geo, h: number, lo = 0.72, hi = 1.12, spec = 0.05, seed = 1): Geo {
  return speckle(shadeY(g, 0, h, lo, hi), spec, seed);
}

// ───────────────────────────── Árvores (sprite `tree`) ─────────────────────────────

/** Gigante da mata atlântica/tropical: raízes tabulares (a pegada), tronco alto, copa em camadas. */
export function tropicalGiant(seed: number, greens = ['#1f7a3a', '#2c8f40', '#3aa345']): Model {
  const parts: Geo[] = [trunk(0.34, 0.5, 7.2, '#6d553d', 7)];
  // Raízes tabulares: aletas triangulares do tronco até a borda da pegada.
  for (let k = 0; k < 5; k++) {
    const a = (k / 5) * Math.PI * 2 + hash2(seed, k) * 0.4;
    const c = Math.cos(a); const s = Math.sin(a);
    const r0 = 0.42; const h = 1.5 + hash2(seed, k + 9) * 0.6;
    parts.push(paint(tris([c * r0, h, s * r0, c * r0, 0, s * r0, c * TREE_R, 0, s * TREE_R]), '#5e4833'));
  }
  const hTop = 8.4 + hash2(seed, 20) * 1.4;
  parts.push(blob(3.1, 0, hTop - 1.1, 0, greens[0], seed, 0.5, 0, 0.18));
  parts.push(blob(2.2, 1.5, hTop - 1.9, 0.6, greens[1], seed + 1, 0.55));
  parts.push(blob(2.0, -1.3, hTop - 1.6, -0.9, greens[1], seed + 2, 0.55));
  parts.push(blob(1.9, 0.2, hTop, -0.3, greens[2], seed + 3, 0.6));
  parts.push(...mound(TREE_R, 0.75, ['#2f7f36', '#3f9a3c', '#2a7032'], seed));
  parts.push(...fernRing(5, TREE_R * 1.0, 0.5, ['#3f9a3c', '#2f8a38'], seed, 0.95, 0.9));
  return flat(finish(merge(parts), hTop + 1.5, 0.6, 1.15, 0.06, seed), 2.6);
}

/** Árvore de copa redonda (mangueira, carvalho, ipê): saia de arbustos no pé e copa de 5 bolhas. */
export function roundTree(seed: number, canopy: string[], skirtColors: string[], trunkColor = '#6b4a2b', height = 1, R = TREE_R): Model {
  const H = height;
  const parts: Geo[] = [trunk(0.2, 0.32, 2.6 * H, trunkColor, 6)];
  parts.push(paint(cyl(0.08, 0.14, 1.8, 5), trunkColor, tf(0.55, 2.6 * H, 0.1, 1, 1, 1, 0, 0, -0.7)));
  parts.push(paint(cyl(0.08, 0.13, 1.6, 5), trunkColor, tf(-0.45, 2.7 * H, -0.3, 1, 1, 1, 0.3, 0, 0.6)));
  const cy = 4.1 * H;
  parts.push(blob(2.2, 0, cy, 0, canopy[0], seed, 0.78, 1, 0.12));
  for (let k = 0; k < 4; k++) {
    const a = (k / 4) * Math.PI * 2 + hash2(seed, k) * 0.7;
    parts.push(blob(1.35, Math.cos(a) * 1.5, cy - 0.9 + hash2(seed, k + 5) * 0.5, Math.sin(a) * 1.5, canopy[(k + 1) % canopy.length], seed + k + 1, 0.8));
  }
  parts.push(...mound(R, 0.85, skirtColors, seed));
  return flat(finish(merge(parts), cy + 2, 0.66, 1.14, 0.05, seed), 2.4);
}

/** Acácia-guarda-chuva: troncos abertos, copa chata e larga, moita espinhosa no pé. */
export function acacia(seed: number): Model {
  const parts: Geo[] = [];
  const bark = '#5f4a36';
  parts.push(trunk(0.16, 0.26, 2.2, bark, 6));
  for (let k = 0; k < 3; k++) {
    const a = (k / 3) * Math.PI * 2 + hash2(seed, k) * 0.8;
    parts.push(paint(cyl(0.08, 0.14, 3.0, 5), bark, lean(Math.cos(a) * 0.55, 3.35, Math.sin(a) * 0.55, a, 0.42)));
  }
  const g = ['#6f8f35', '#7f9a3a', '#5f8030'];
  parts.push(blob(3.4, 0, 4.9, 0, g[0], seed, 0.24, 1, 0.1));
  parts.push(blob(2.2, 1.6, 4.6, 0.8, g[1], seed + 1, 0.3));
  parts.push(blob(2.0, -1.7, 4.7, -0.6, g[2], seed + 2, 0.3));
  parts.push(...mound(TREE_R, 0.7, ['#8a8a3c', '#7a7a38', '#9a9245'], seed + 3));
  parts.push(paint(tuft(7, 1.0, 0.5, seed), '#c9b25a', tf(0.3, 0, -0.4)));
  return flat(finish(merge(parts), 5.6, 0.7, 1.1, 0.05, seed), 3.2);
}

/** Baobá: o tronco é a pegada (raio de colisão no chão), galhos grossos e tufos esparsos. */
export function baobab(seed: number): Model {
  const bark = '#9d8a78';
  const parts: Geo[] = [paint(cyl(1.12, TREE_R, 5.6, 9), bark, tf(0, 2.8, 0))];
  for (let k = 0; k < 6; k++) {
    const a = (k / 6) * Math.PI * 2 + hash2(seed, k) * 0.5;
    const tilt = 0.55 + hash2(seed, k + 3) * 0.4;
    const len = 1.8 + hash2(seed, k + 6) * 1.0;
    const x = Math.cos(a) * 0.7; const z = Math.sin(a) * 0.7;
    parts.push(paint(cyl(0.16, 0.36, len, 5), bark, lean(x + Math.cos(a) * Math.sin(tilt) * len * 0.5, 5.5 + Math.cos(tilt) * len * 0.5, z + Math.sin(a) * Math.sin(tilt) * len * 0.5, a, tilt)));
    parts.push(blob(0.8, x + Math.cos(a) * Math.sin(tilt) * len, 5.6 + Math.cos(tilt) * len, z + Math.sin(a) * Math.sin(tilt) * len, k % 2 ? '#7a9a3c' : '#6a8a36', seed + k, 0.7));
  }
  return flat(finish(merge(parts), 8, 0.78, 1.08, 0.05, seed), 2.6);
}

/** Choupo (Europa): coluna estreita e alta, saia de arbustos. */
export function poplar(seed: number, R = TREE_R): Model {
  const parts: Geo[] = [trunk(0.14, 0.22, 2.0, '#6b5a44', 6)];
  parts.push(paint(jitter(ico(1.25, 1), 0.1, seed), '#3f8a3a', tf(0, 5.6, 0, 1, 3.4, 1)));
  parts.push(paint(jitter(ico(0.9, 0), 0.1, seed + 1), '#4f9a40', tf(0.25, 8.8, 0.1, 1, 1.6, 1)));
  parts.push(...mound(R, 0.8, ['#3b7a35', '#4a8a3a'], seed));
  return flat(finish(merge(parts), 10.5, 0.66, 1.12, 0.05, seed), 1.6);
}

/** Bétula/álamo (montanha): três troncos claros, copas estreitas — verde ou dourada de outono. */
export function aspen(seed: number, leaves: string[], R = TREE_R): Model {
  const parts: Geo[] = [];
  for (let k = 0; k < 3; k++) {
    const a = (k / 3) * Math.PI * 2 + hash2(seed, k) * 0.8;
    const x = Math.cos(a) * 0.45; const z = Math.sin(a) * 0.45;
    const h = 5.2 + hash2(seed, k + 3) * 1.8;
    parts.push(paint(cyl(0.08, 0.13, h, 5), '#e9e5da', tf(x, h / 2, z, 1, 1, 1, Math.sin(a) * 0.06, 0, -Math.cos(a) * 0.06)));
    parts.push(paint(box(0.2, 0.1, 0.2), '#3a3a3a', tf(x, 1.3 + k * 0.5, z)));
    parts.push(blob(1.15, x * 1.4, h - 0.6, z * 1.4, leaves[k % leaves.length], seed + k, 1.5));
  }
  parts.push(...mound(R, 0.7, ['#4f7a3a', '#5a8a40'], seed + 7));
  return flat(finish(merge(parts), 7.5, 0.7, 1.12, 0.05, seed), 1.8);
}

/** Eucalipto (Austrália): tronco claro e alto, copa rala caindo em tufos. */
export function eucalyptus(seed: number, R = TREE_R): Model {
  const parts: Geo[] = [trunk(0.18, 0.34, 6.5, '#d8d2c4', 6, 0.05, seed)];
  parts.push(paint(cyl(0.07, 0.12, 2.6, 5), '#d0c8b8', tf(0.7, 6.0, 0.2, 1, 1, 1, 0, 0, -0.6)));
  const g = ['#7f9a6a', '#8fa877', '#6f8a60'];
  for (let k = 0; k < 5; k++) {
    const a = (k / 5) * Math.PI * 2 + hash2(seed, k) * 0.7;
    parts.push(blob(1.1, Math.cos(a) * 1.2, 6.6 + hash2(seed, k + 3) * 1.4, Math.sin(a) * 1.2, g[k % 3], seed + k, 0.75));
  }
  parts.push(...mound(R, 0.6, ['#9a8a5a', '#8a8a55'], seed + 9));
  return flat(finish(merge(parts), 8.5, 0.72, 1.1, 0.05, seed), 2.2);
}

/** Oliveira: tronco torto e grosso, copa baixa prateada que desce até a altura do carro. */
export function olive(seed: number, R = TREE_R): Model {
  const parts: Geo[] = [trunk(0.26, 0.4, 1.6, '#6a5a48', 6, 0.12, seed)];
  parts.push(paint(cyl(0.12, 0.2, 1.4, 5), '#6a5a48', tf(0.4, 1.9, 0, 1, 1, 1, 0, 0, -0.5)));
  const g = ['#8ea56a', '#7f9860', '#9ab478'];
  parts.push(blob(1.5, 0, 2.9, 0, g[0], seed, 0.62, 1));
  for (let k = 0; k < 5; k++) {
    const a = (k / 5) * Math.PI * 2 + hash2(seed, k) * 0.6;
    parts.push(blob(0.72, Math.cos(a) * (R - 0.95), 1.75 + hash2(seed, k + 2) * 0.3, Math.sin(a) * (R - 0.95), g[k % 3], seed + k + 1, 0.7));
  }
  parts.push(...mound(R, 0.65, ['#7f9860', '#8ea56a'], seed + 5));
  return flat(finish(merge(parts), 4, 0.72, 1.12, 0.05, seed), 2.2);
}

/** Cipreste (Mediterrâneo): chama verde-escura, estreita e alta. */
export function cypress(seed: number, R = TREE_R): Model {
  const parts: Geo[] = [trunk(0.1, 0.16, 1.0, '#5a4a36', 5)];
  parts.push(paint(jitter(ico(1.0, 1), 0.08, seed), '#2f5a36', tf(0, 4.3, 0, 1, 4.0, 1)));
  parts.push(paint(cone(0.55, 1.6, 6), '#2a5232', tf(0, 8.1, 0)));
  parts.push(...mound(R, 0.6, ['#4a6a3a', '#5a7a40'], seed));
  return flat(finish(merge(parts), 9, 0.68, 1.12, 0.04, seed), 1.3);
}

// ───────────────────────────── Coníferas (sprite `pine`) ─────────────────────────────

/**
 * Pinheiro em camadas: o degrau de baixo nasce a 0,3 m com o raio de colisão (é o que o carro toca).
 * `snow`: chapéu branco em cada camada; `tiers`/`slim` mudam a silhueta; `color` a espécie.
 */
export function conifer(seed: number, color: string, tiers = 5, slim = 1, snow = false, trunkH = 0.5, R = PINE_R): Model {
  const parts: Geo[] = [paint(cyl(0.12, 0.2, 1.2 + trunkH, 6), '#5a3d24', tf(0, (1.2 + trunkH) / 2, 0))];
  let y = 0.3 + trunkH * 0.2;
  const total = (6.8 + hash2(seed, 1) * 2.2) * slim;
  const step = total / (tiers + 1);
  const dark = new THREE.Color(color).multiplyScalar(0.82).getStyle();
  for (let t = 0; t < tiers; t++) {
    const k = t / (tiers - 1 || 1);
    const r = R * (1 - k * 0.72) / (t === 0 ? 1 : Math.max(0.75, slim));
    const h = step * 1.9;
    const c = t % 2 ? dark : color;
    const tier = paint(jitter(cone(t === 0 ? R : r, h, 7), 0.02, seed + t), c, tf(0, y + h / 2, 0, 1, 1, 1, (hash2(seed, t) - 0.5) * 0.08, t * 0.45, (hash2(seed, t + 9) - 0.5) * 0.08));
    // Neve: o bico de cada camada branco, degradê até o verde da borda.
    if (snow) topTint(tier, y + h * 0.62, y + h * 0.95, '#f4f8ff');
    parts.push(tier);
    y += step;
  }
  parts.push(paint(cone(0.25, step * 1.2, 5), snow ? '#f2f6ff' : color, tf(0, y + step * 0.6, 0)));
  return flat(finish(merge(parts), y + step, 0.7, 1.16, 0.04, seed), 1.9);
}

// ───────────────────────────── Palmeiras (sprite `palm`) ─────────────────────────────

function palmBase(seed: number, colors: string[]): Geo[] {
  return [...mound(PALM_R, 0.6, colors, seed + 100), ...fernRing(5, PALM_R, 0.42, colors, seed + 100, 0.8, 0.75)];
}

/** Coqueiro: tronco curvo anelado, folhas caídas, cocos; plantas no pé até a pegada. */
export function coconutPalm(seed: number, lean: number, baseColors = ['#3f9a3c', '#58ad44']): Model {
  const parts: Geo[] = [];
  const pieces = 5; const H = 7.0 + hash2(seed, 1) * 1.6;
  const dir = hash2(seed, 2) * Math.PI * 2;
  const cd = Math.cos(dir); const sd = Math.sin(dir);
  let topX = 0; let topZ = 0;
  for (let k = 0; k < pieces; k++) {
    const t0 = k / pieces; const t1 = (k + 1) / pieces;
    const o0 = lean * t0 * t0; const o1 = lean * t1 * t1;
    const y0 = t0 * H; const y1 = t1 * H;
    const ang = Math.atan2(o1 - o0, y1 - y0);
    const r = 0.26 - 0.1 * t0;
    const mx = ((o0 + o1) / 2) * cd; const mz = ((o0 + o1) / 2) * sd;
    parts.push(paint(cyl(r - 0.02, r, (y1 - y0) / Math.cos(ang) + 0.04, 6, true), k % 2 ? '#8a6a3f' : '#7a5c36', tf(mx, (y0 + y1) / 2, mz, 1, 1, 1, sd * ang, 0, -cd * ang)));
    topX = o1 * cd; topZ = o1 * sd;
  }
  const n = 9;
  for (let k = 0; k < n; k++) {
    const a = (k / n) * Math.PI * 2 + hash2(seed, k) * 0.4;
    const len = 3.1 + hash2(seed, k + 20) * 0.8;
    parts.push(paint(frond(len, 0.95, a, 0.9, 2.6 + hash2(seed, k + 30) * 0.8, 3), k % 2 ? '#3f9a3a' : '#4fae44', tf(topX, H - 0.05, topZ)));
  }
  for (let k = 0; k < 3; k++) parts.push(paint(frond(1.6, 0.5, k * 2.1, 1.3, 0.6, 2), '#6cc050', tf(topX, H, topZ)));
  for (let k = 0; k < 3; k++) parts.push(paint(new THREE.OctahedronGeometry(0.2, 0), '#6b4a22', tf(topX + Math.cos(k * 2.1) * 0.26, H - 0.35, topZ + Math.sin(k * 2.1) * 0.26)));
  parts.push(...palmBase(seed, baseColors));
  return flat(finish(merge(parts), H + 1, 0.75, 1.12, 0.04, seed), 2.4);
}

/** Palmeira-imperial: tronco reto cinza, palmito verde, folhas em arco. */
export function royalPalm(seed: number, baseColors = ['#3f9a3c', '#58ad44']): Model {
  const H = 9.5 + hash2(seed, 1) * 2;
  const parts: Geo[] = [paint(cyl(0.24, 0.36, H, 7, true), '#b8b0a4', tf(0, H / 2, 0))];
  parts.push(paint(cyl(0.2, 0.27, 1.6, 7), '#5a9a3a', tf(0, H + 0.8, 0)));
  for (let k = 0; k < 10; k++) {
    const a = (k / 10) * Math.PI * 2 + hash2(seed, k) * 0.3;
    parts.push(paint(frond(3.4, 0.8, a, 1.6, 2.4, 3), k % 2 ? '#3a8f38' : '#48a040', tf(0, H + 1.5, 0)));
  }
  parts.push(...palmBase(seed, baseColors));
  return flat(finish(merge(parts), H + 3, 0.78, 1.1, 0.04, seed), 2.2);
}

/** Tamareira baixa e cheia: as folhas de baixo caem até a altura do carro, na pegada. */
export function datePalm(seed: number): Model {
  const H = 2.6 + hash2(seed, 1) * 1.2;
  const parts: Geo[] = [paint(cyl(0.34, 0.42, H, 7), '#8a6e48', tf(0, H / 2, 0))];
  for (let k = 0; k < 4; k++) parts.push(paint(cyl(0.44, 0.44, 0.12, 7, true), '#7a603e', tf(0, 0.3 + k * H / 4, 0, 1, 1, 1, 0, k * 0.4, 0)));
  for (let k = 0; k < 14; k++) {
    const a = (k / 14) * Math.PI * 2 + hash2(seed, k) * 0.3;
    const low = k % 2 === 0;
    const len = low ? PALM_R * 0.95 : 2.8;
    parts.push(paint(frond(len, 0.7, a, low ? 0.3 : 1.4, low ? 0.9 : 1.9, 3), low ? '#3f8f3a' : '#56a846', tf(0, H + (low ? -0.1 : 0.1), 0)));
  }
  parts.push(...palmBase(seed, ['#4a9a3c', '#3a8a36']));
  return flat(finish(merge(parts), H + 2, 0.75, 1.12, 0.04, seed), 2.4);
}

/** Palmeira-de-leque (washingtonia): tronco fino e alto, saia de folhas secas, copa de leques. */
export function fanPalm(seed: number, baseColors = ['#6a9a3c', '#8aa84a']): Model {
  const H = 10 + hash2(seed, 1) * 2.5;
  const parts: Geo[] = [paint(cyl(0.2, 0.3, H, 7), '#8f7a5c', tf(0, H / 2, 0))];
  parts.push(paint(cyl(0.62, 0.34, 2.0, 8), '#a88a5a', tf(0, H - 0.8, 0)));
  for (let k = 0; k < 9; k++) {
    const a = (k / 9) * Math.PI * 2;
    parts.push(paint(cone(0.85, 0.12, 7), '#4f9a44', lean(Math.cos(a) * 0.9, H + 0.35 + hash2(seed, k) * 0.3, Math.sin(a) * 0.9, a, 0.5, 1.6, 1, 1)));
  }
  parts.push(...palmBase(seed, baseColors));
  return flat(finish(merge(parts), H + 1, 0.78, 1.1, 0.04, seed), 1.6);
}

/** Bananeira: folhas largas saindo do chão — a pegada é a própria folhagem. */
export function banana(seed: number, R = TREE_R): Model {
  const parts: Geo[] = [paint(cyl(0.16, 0.24, 2.4, 6), '#7a9a4a', tf(0, 1.2, 0))];
  for (let k = 0; k < 7; k++) {
    const a = (k / 7) * Math.PI * 2 + hash2(seed, k) * 0.5;
    parts.push(paint(frond(2.2, 0.9, a, 1.6, 1.3, 3), k % 2 ? '#5cb04a' : '#4a9e40', tf(0, 2.2 + hash2(seed, k + 3) * 0.6, 0)));
  }
  parts.push(...mound(R, 0.7, ['#3f9a3c', '#58ad44'], seed));
  return flat(finish(merge(parts), 4, 0.75, 1.12, 0.05, seed), 1.8);
}

/** Samambaiaçu (samambaia arbórea): tronco fino e roseta de frondes no alto. */
export function treeFern(seed: number, R = TREE_R): Model {
  const H = 3.2 + hash2(seed, 1) * 1.6;
  const parts: Geo[] = [paint(cyl(0.15, 0.2, H, 6), '#4a3a2a', tf(0, H / 2, 0))];
  for (let k = 0; k < 9; k++) parts.push(paint(frond(2.2, 0.55, (k / 9) * Math.PI * 2 + seed, 0.7, 1.4, 3), k % 2 ? '#3a9a44' : '#2f8a3c', tf(0, H, 0)));
  parts.push(...mound(R, 0.6, ['#2f8a38', '#3f9a3c'], seed), ...fernRing(5, R, 0.45, ['#3f9a3c', '#2f8a38'], seed, 0.8, 0.75));
  return flat(finish(merge(parts), H + 1, 0.72, 1.12, 0.04, seed), 1.6);
}

// ───────────────────────────── Deserto (sprite `cactus`) ─────────────────────────────

/** Saguaro: coluna canelada e braços ALINHADOS com X (o giro por instância é pequeno) até a pegada. */
export function saguaro(seed: number, arms: 2 | 3): Model {
  const green = ['#4f8a4a', '#5a9650', '#467f42'][seed % 3];
  const R = CACTUS_R; const tr = 0.3; const ar = 0.19;
  const H = 4.2 + hash2(seed, 1) * 1.2;
  const parts: Geo[] = [paint(cyl(tr, tr + 0.05, H, 8, true), green, tf(0, H / 2, 0)), paint(sphere(tr, 8, 3, 0, Math.PI * 2, 0, Math.PI / 2), green, tf(0, H, 0, 1, 0.8, 1))];
  // Cotovelos baixos: mesmo o saguaro mais alto (escala × variação de altura) encosta os braços na altura do carro.
  const specs: Array<[number, number, number]> = [[1, 0.72, 1.9], [-1, 0.8, 2.4], [1, 2.3, 1.2]];
  for (let k = 0; k < arms; k++) {
    const [side, elbow, up] = specs[k];
    const reach = k === 2 ? R * 0.72 : R;
    const x = side * (reach - ar);
    parts.push(paint(cyl(ar, ar, Math.abs(x) - tr * 0.5, 6, true), green, tf(side * (Math.abs(x) + tr * 0.5) / 2, elbow, 0, 1, 1, 1, 0, 0, Math.PI / 2)));
    parts.push(paint(sphere(ar, 6, 3), green, tf(x, elbow, 0)));
    parts.push(paint(cyl(ar * 0.92, ar, up, 6, true), green, tf(x, elbow + up / 2, 0)));
    parts.push(paint(sphere(ar * 0.92, 6, 2, 0, Math.PI * 2, 0, Math.PI / 2), green, tf(x, elbow + up, 0, 1, 0.8, 1)));
  }
  parts.push(paint(new THREE.OctahedronGeometry(0.1, 0), '#f4f0e0', tf(0.1, H + 0.2, 0)), paint(new THREE.OctahedronGeometry(0.09, 0), '#f2a0c0', tf(-0.12, H + 0.18, 0.06)));
  return flat(finish(merge(parts), H, 0.8, 1.1, 0.03, seed), 1.1);
}

/** Moita de cactos-barril: quatro em volta e um no meio, a borda externa na pegada. */
export function barrelCluster(seed: number): Model {
  const parts: Geo[] = [];
  const R = CACTUS_R;
  for (let k = 0; k < 5; k++) {
    const center = k === 4;
    const r = center ? 0.42 : 0.3 + hash2(seed, k) * 0.08;
    const a = (k / 4) * Math.PI * 2 + hash2(seed, 9) * 0.5;
    const d = center ? 0 : R - r;
    const h = center ? 1.0 : 0.6 + hash2(seed, k + 3) * 0.3;
    const x = Math.cos(a) * d; const z = Math.sin(a) * d;
    parts.push(paint(cyl(r * 0.85, r, h, 8, true), '#5a8f45', tf(x, h / 2, z)));
    parts.push(paint(sphere(r * 0.85, 8, 2, 0, Math.PI * 2, 0, Math.PI / 2), '#5a8f45', tf(x, h, z, 1, 0.5, 1)));
    parts.push(paint(new THREE.OctahedronGeometry(0.08, 0), k % 2 ? '#f2c33a' : '#e8506a', tf(x, h + r * 0.35, z)));
  }
  parts.push(...mound(R, 0.3, ['#b8a07a', '#a89070'], seed + 9));
  return flat(finish(merge(parts), 1.4, 0.8, 1.1, 0.04, seed), 1.1);
}

/** Palma (figueira-da-índia): raquetes em leque em X e Z até a pegada, com frutos. */
export function pricklyPear(seed: number): Model {
  const parts: Geo[] = [];
  const R = CACTUS_R;
  const pad = (x: number, y: number, z: number, s: number, yaw: number, roll: number, k: number) => {
    parts.push(paint(jitter(sphere(0.42 * s, 7, 4), 0.05, seed + k), k % 3 ? '#5f9a4a' : '#6aa650', tf(x, y, z, 1, 1.25, 0.28, 0, yaw, roll)));
  };
  pad(0, 0.5, 0, 1.1, 0, 0, 0);
  pad(0.5, 0.95, 0, 1, 0, -0.5, 1); pad(-0.5, 0.9, 0.05, 1, 0, 0.5, 2);
  pad(R - 0.38, 0.55, 0, 0.9, 0, -1.1, 3); pad(-(R - 0.38), 0.5, 0, 0.9, 0, 1.1, 4);
  pad(0, 0.55, R - 0.4, 0.85, Math.PI / 2, -1.0, 5); pad(0.1, 1.5, 0, 0.9, 0.3, -0.2, 6);
  for (let k = 0; k < 5; k++) parts.push(paint(new THREE.OctahedronGeometry(0.08, 0), '#d8386a', tf(-0.3 + k * 0.2, 1.95 - Math.abs(k - 2) * 0.12, 0)));
  return flat(finish(merge(parts), 2, 0.8, 1.1, 0.03, seed), 1.1);
}

/** Cupinzeiro (Outback): catedral de terra vermelha, base com o raio da pegada. */
export function termiteMound(seed: number): Model {
  // Catedral de terra: corpo arredondado e irregular (nada de cone liso de trânsito), torres baixas grudadas.
  const H = 2.2 + hash2(seed, 1) * 0.9;
  const body = new THREE.CylinderGeometry(0.34, CACTUS_R, H, 9, 4);
  const earth = ['#a8653a', '#b8744a', '#96583a'];
  const parts: Geo[] = [paint(jitter(body, 0.12, seed, true), earth[0], tf(0, H / 2, 0))];
  parts.push(paint(jitter(sphere(0.36, 7, 4), 0.1, seed + 3), earth[1], tf(0, H, 0, 1, 0.8, 1)));
  for (let k = 0; k < 4; k++) {
    const a = (k / 4) * Math.PI * 2 + hash2(seed, k);
    const hh = 0.9 + hash2(seed, k + 5) * 1.0;
    parts.push(paint(jitter(cyl(0.12, 0.3, hh, 6), 0.14, seed + k), earth[(k + 1) % 3], tf(Math.cos(a) * 0.45, 0.6 + hh / 2, Math.sin(a) * 0.45)));
    parts.push(paint(jitter(sphere(0.14, 5, 3), 0.1, seed + k + 9), earth[(k + 1) % 3], tf(Math.cos(a) * 0.45, 0.6 + hh, Math.sin(a) * 0.45)));
  }
  return flat(finish(merge(parts), H + 0.5, 0.78, 1.1, 0.08, seed), 1.2);
}

/** Touceira de spinifex: domo de espetos dourados, raio da pegada. */
export function spinifex(seed: number): Model {
  const parts: Geo[] = [];
  for (let k = 0; k < 7; k++) {
    const a = (k / 7) * Math.PI * 2;
    const d = k === 0 ? 0 : CACTUS_R * 0.55;
    parts.push(paint(tuft(9, 0.9, 0.45, seed + k), k % 2 ? '#c9b25a' : '#b5a050', tf(Math.cos(a) * d, 0, Math.sin(a) * d)));
  }
  parts.push(...mound(CACTUS_R, 0.55, ['#a89a4a', '#b8a850'], seed + 20));
  return flat(finish(merge(parts), 1.1, 0.8, 1.1, 0.05, seed), 1.1);
}

/** Eufórbia-candelabro (Karoo): braços que sobem de um cálice baixo, pegada nos cotovelos. */
export function euphorbia(seed: number): Model {
  const g = '#5a8a4a';
  const parts: Geo[] = [paint(cyl(0.22, 0.28, 0.7, 7), '#6a6a4a', tf(0, 0.35, 0))];
  for (let k = 0; k < 6; k++) {
    const a = (k / 6) * Math.PI * 2 + hash2(seed, k) * 0.3;
    const d = CACTUS_R - 0.13;
    const up = 1.6 + hash2(seed, k + 4) * 1.4;
    parts.push(paint(cyl(0.12, 0.12, d, 6), g, lean(Math.cos(a) * d / 2, 0.62, Math.sin(a) * d / 2, a, Math.PI / 2)));
    parts.push(paint(cyl(0.11, 0.13, up, 6), g, tf(Math.cos(a) * d, 0.62 + up / 2, Math.sin(a) * d)));
  }
  parts.push(paint(cyl(0.14, 0.14, 2.8, 6), g, tf(0, 2.3, 0)));
  return flat(finish(merge(parts), 3.5, 0.8, 1.1, 0.04, seed), 1.1);
}

/** Aloe/agave: roseta de folhas grossas até a pegada e haste com flores. */
export function rosette(seed: number, leaf: string, flower: string, stalk: number): Model {
  const parts: Geo[] = [];
  for (let k = 0; k < 12; k++) {
    const a = (k / 12) * Math.PI * 2 + hash2(seed, k) * 0.3;
    const inner = k % 2 === 1;
    const tilt = inner ? 0.55 : 1.05;
    const L = inner ? 0.95 : CACTUS_R / Math.sin(tilt) * 0.98;
    parts.push(paint(cone(0.13, L, 4), leaf, lean(Math.cos(a) * Math.sin(tilt) * L / 2, 0.12 + Math.cos(tilt) * L / 2, Math.sin(a) * Math.sin(tilt) * L / 2, a, tilt, 1, 1, 0.5)));
  }
  parts.push(paint(cyl(0.035, 0.05, stalk, 4), '#7a6a3a', tf(0, stalk / 2, 0)));
  for (let k = 0; k < 4; k++) parts.push(paint(cone(0.14, 0.5, 5), flower, tf(Math.cos(k * 1.6) * 0.18, stalk - 0.2 - k * 0.35, Math.sin(k * 1.6) * 0.18)));
  return flat(finish(merge(parts), stalk, 0.82, 1.1, 0.04, seed), 1.1);
}

// ───────────────────────────── Arbustos (sprite `bush`, não sólido) ─────────────────────────────

/** Moita baixa (≤ 1,1 m: o carro passa por cima dela sem parecer atravessar uma parede). */
export function shrub(seed: number, colors: string[], flowers: string | null, r = 1.2, h = 0.95): Model {
  const parts: Geo[] = [];
  for (let k = 0; k < 4; k++) {
    const a = (k / 4) * Math.PI * 2 + hash2(seed, k);
    const d = k === 0 ? 0 : r * 0.45;
    const rr = (k === 0 ? 0.62 : 0.48) * r;
    parts.push(paint(jitter(ico(rr, 0), 0.12, seed + k), colors[k % colors.length], tf(Math.cos(a) * d, rr * 0.55, Math.sin(a) * d, 1, h / (rr * 1.9), 1, 0, a, 0)));
  }
  if (flowers) {
    for (let k = 0; k < 9; k++) {
      const a = hash2(seed, k + 50) * 6.28; const d = hash2(seed, k + 60) * r * 0.7;
      const y = h * (0.7 + hash2(seed, k + 70) * 0.35);
      parts.push(paint(tris([-0.12, y, 0, 0.12, y + 0.02, 0.05, 0, y + 0.04, -0.13]), flowers, tf(Math.cos(a) * d, 0, Math.sin(a) * d)));
    }
  }
  return flat(finish(merge(parts), h, 0.7, 1.12, 0.06, seed), r * 0.9);
}

/** Moita de samambaias. */
export function fernClump(seed: number, colors = ['#3f9a3c', '#2f8a38', '#58ad44']): Model {
  const parts = fernRing(7, 1.4, 0.5, colors, seed, 0.9, 0.7, 2);
  parts.push(...fernRing(4, 0.9, 0.4, colors, seed + 3, 1.0, 0.5, 2));
  return flat(finish(merge(parts), 1, 0.75, 1.12, 0.05, seed), 1.2);
}

/** Cerca-viva aparada (cidade): caixa verde num canteiro. */
export function hedge(seed: number): Model {
  const parts: Geo[] = [paint(box(2.6, 0.4, 1.3), '#9a948a', tf(0, 0.2, 0))];
  parts.push(paint(box(2.3, 0.7, 1.05), '#3c8a45', tf(0, 0.72, 0)));
  for (let k = 0; k < 6; k++) parts.push(paint(tris([-0.13, 1.08, 0, 0.13, 1.1, 0.06, 0, 1.12, -0.14]), ['#ff4f7a', '#ffd23f', '#ffffff'][k % 3], tf(-0.9 + k * 0.36, 0, (hash2(seed, k) - 0.5) * 0.7)));
  return flat(finish(merge(parts), 1.1, 0.8, 1.1, 0.03, seed), 1.4);
}

// ───────────────────────────── Pedras (sprite `boulder`) ─────────────────────────────

export type RockStyle = 'sandstone' | 'granite' | 'snowy' | 'mossy' | 'kopje' | 'basalt' | 'lava';

/** Grupo de pedras: a principal ocupa a pegada em toda direção (giro livre), mais duas menores. */
export function rocks(seed: number, style: RockStyle, R = ROCK_R): Model {
  const base = { sandstone: '#c8683a', granite: '#8d8f94', snowy: '#8a8d96', mossy: '#6f6a5a', kopje: '#a39480', basalt: '#5b5a5e', lava: '#3e393b' }[style];
  const parts: Geo[] = [];
  if (style === 'sandstone') {
    // Camadas de arenito: fatias com cores alternadas; a de baixo (10 lados, sem deformar) é a pegada.
    const strata = ['#c8683a', '#e08a50', '#b35a32', '#d97a45'];
    let y = 0;
    for (let k = 0; k < 4; k++) {
      const rr = R * (1.04 - k * 0.2);
      const h = 0.85 - k * 0.1;
      const g = jitter(cyl(rr * 0.86, rr, h, k === 0 ? 10 : 7), 0.05, seed + k);
      const off = k === 0 ? 0 : (hash2(seed, k) - 0.5) * R * 0.3;
      parts.push(paint(g, strata[k], tf(off, y + h / 2, off * 0.6, 1, 1, k === 0 ? 1 : 0.85, 0, k * 0.7 + seed, 0)));
      y += h * 0.92;
    }
  } else {
    // Um matacão largo cuja cintura (na altura do carro) é a pegada em toda direção; duas pedras menores em cima/ao lado.
    const main = paint(jitter(ico(1, 1), 0.03, seed), base, tf(0, R * 0.2, 0, R * 1.04, R * 0.42, R * 1.0));
    parts.push(main);
    // Blocos angulosos por cima (dentro da borda): a silhueta deixa de ser um ovo liso.
    parts.push(paint(jitter(ico(R * 0.55, 0), 0.15, seed + 1), base, tf(R * 0.1, R * 0.55, -R * 0.08, 1, 0.85, 0.9, 0.3, seed, 0.2)));
    parts.push(paint(jitter(dodeca(R * 0.38), 0.14, seed + 2), base, tf(-R * 0.36, R * 0.46, R * 0.22, 1, 0.8, 1, 0, seed * 2, 0.3)));
    parts.push(paint(jitter(ico(R * 0.26, 0), 0.15, seed + 3), base, tf(R * 0.42, R * 0.3, R * 0.36, 1, 0.7, 1)));
  }
  const g = merge(parts);
  if (style === 'snowy') tintUp(g, '#f3f6ff', 0.55);
  if (style === 'mossy') tintUp(g, '#4f7a3a', 0.6, 0.85);
  if (style === 'kopje') tintUp(g, '#b8aa90', 0.7, 0.6);
  if (style === 'basalt' || style === 'granite') tintUp(g, style === 'basalt' ? '#6e6c70' : '#a3a5aa', 0.7, 0.6);
  if (style === 'lava') tintUp(g, '#5a4a48', 0.7, 0.5);
  return flat(finish(g, R * 1.1, 0.66, 1.14, 0.12, seed), R * 1.1);
}

// ───────────────────────────── Forração (decoração no chão, só visual) ─────────────────────────────

/** Tufo de capim/folhagem baixa (o carro passa por cima). */
export function grassTuft(seed: number, color: string, h = 0.55, n = 7): Model {
  return { parts: [{ geometry: shadeY(paint(tuft(n, h, 0.35, seed), color), 0, h, 0.7, 1.15), mat: 'flat' }] };
}

/** Canteiro de flores do campo: tufo com pontos coloridos. */
export function flowerPatch(seed: number, green: string, flowers: string[]): Model {
  const parts: Geo[] = [paint(tuft(6, 0.35, 0.4, seed), green)];
  for (let k = 0; k < 7; k++) {
    const a = hash2(seed, k) * 6.28; const d = 0.15 + hash2(seed, k + 9) * 0.5;
    const y = 0.3 + hash2(seed, k + 3) * 0.12;
    parts.push(paint(tris([-0.08, y, 0, 0.08, y, 0.03, 0, y + 0.02, -0.09]), flowers[k % flowers.length], tf(Math.cos(a) * d, 0, Math.sin(a) * d)));
  }
  return { parts: [{ geometry: merge(parts), mat: 'flat' }] };
}

/** Pedrinhas espalhadas. */
export function pebbles(seed: number, color: string): Model {
  const parts: Geo[] = [];
  for (let k = 0; k < 4; k++) {
    const a = hash2(seed, k) * 6.28; const d = hash2(seed, k + 5) * 0.8;
    const r = 0.14 + hash2(seed, k + 7) * 0.16;
    parts.push(paint(jitter(new THREE.OctahedronGeometry(r, 0), 0.15, seed + k), color, tf(Math.cos(a) * d, r * 0.3, Math.sin(a) * d, 1, 0.6, 1)));
  }
  return { parts: [{ geometry: tintUp(merge(parts), '#ffffff', 0.7, 0.18), mat: 'flat' }] };
}

// ───────────────────────────── Silhuetas de longe (LOD) ─────────────────────────────

/** Árvore redonda distante: tronco + uma bolha (≈ 30 triângulos). */
export function farRound(color: string, h = 5.5, r = 2.3): Model {
  const g = merge([paint(cyl(0.2, 0.3, h * 0.45, 4), '#5a4430', tf(0, h * 0.22, 0)), paint(ico(r, 0), color, tf(0, h * 0.68, 0, 1, 0.85, 1))]);
  return { parts: [{ geometry: shadeY(g, 0, h + r, 0.7, 1.12), mat: 'flat' }], blob: r * 0.9 };
}

/** Conífera distante: dois cones (≈ 16 triângulos). */
export function farCone(color: string, h = 9, r = 1.7): Model {
  const g = merge([paint(cone(r, h * 0.62, 5), color, tf(0, h * 0.33, 0)), paint(cone(r * 0.62, h * 0.5, 5), color, tf(0, h * 0.72, 0))]);
  return { parts: [{ geometry: shadeY(g, 0, h, 0.66, 1.15), mat: 'flat' }], blob: r * 0.8 };
}

/** Palmeira distante: haste + estrela de folhas. */
export function farPalm(color: string, h = 8): Model {
  const parts: Geo[] = [paint(cyl(0.16, 0.24, h, 4), '#8a6a3f', tf(0, h / 2, 0))];
  for (let k = 0; k < 5; k++) parts.push(paint(frond(2.8, 0.8, (k / 5) * Math.PI * 2, 0.6, 2.0, 2), color, tf(0, h, 0)));
  return { parts: [{ geometry: shadeY(merge(parts), 0, h + 1, 0.75, 1.1), mat: 'flat' }], blob: 1.2 };
}

/** Saguaro distante: coluna e dois braços em caixas (≈ 36 triângulos). */
export function farSaguaro(): Model {
  const g = merge([
    paint(box(0.6, 4.6, 0.6), '#4f8a4a', tf(0, 2.3, 0)), paint(box(0.4, 1.9, 0.4), '#4f8a4a', tf(0.78, 1.7, 0)),
    paint(box(0.4, 2.4, 0.4), '#4f8a4a', tf(-0.78, 2.0, 0)),
  ]);
  return { parts: [{ geometry: g, mat: 'flat' }], blob: 1 };
}

/** Rocha distante (formações do deserto, penedos). */
export function farRock(color: string, seed: number, w = 6, h = 5): Model {
  const g = paint(jitter(ico(1, 0), 0.18, seed), color, tf(0, h * 0.45, 0, w, h, w * 0.8));
  return { parts: [{ geometry: tintUp(g, '#ffffff', 0.7, 0.15), mat: 'flat' }], blob: w * 0.8 };
}

/** Mesa/butte do deserto: bloco de topo chato com camadas. */
export function mesa(seed: number, colors: string[], w: number, h: number): Model {
  const parts: Geo[] = [];
  let y = 0;
  const layers = 4;
  for (let k = 0; k < layers; k++) {
    const lh = h / layers;
    const top = k === layers - 1;
    const rb = w * (1.12 - k * 0.07); const rt = top ? w * 0.86 : w * (1.05 - k * 0.07);
    parts.push(paint(jitter(cyl(rt, rb, lh, 8), 0.06, seed + k), colors[k % colors.length], tf(0, y + lh / 2, 0, 1, 1, 0.75, 0, seed, 0)));
    y += lh;
  }
  // Tálus: rampa de detritos no pé.
  parts.push(paint(jitter(cyl(w * 1.1, w * 1.5, h * 0.22, 8), 0.08, seed + 9), colors[0], tf(0, h * 0.11, 0, 1, 1, 0.8)));
  return { parts: [{ geometry: shadeY(merge(parts), 0, h, 0.72, 1.1), mat: 'flat' }] };
}

/** Coluna de arenito no mar (os "apóstolos" do litoral australiano; geologia, não marca). */
export function seaStack(seed: number, h: number): Model {
  const parts: Geo[] = [];
  const colors = ['#d9a066', '#c98a52', '#e6b27a'];
  let y = -4;
  const n = 4;
  for (let k = 0; k < n; k++) {
    const lh = (h + 4) / n;
    const r = 3.4 - k * 0.45 + hash2(seed, k) * 0.5;
    parts.push(paint(jitter(cyl(r * 0.9, r, lh, 7), 0.08, seed + k), colors[k % 3], tf(0, y + lh / 2, 0, 1, 1, 0.8)));
    y += lh;
  }
  parts.push(paint(jitter(ico(2.3, 0), 0.1, seed), '#8a9a4a', tf(0, y, 0, 1, 0.3, 0.8)));
  return { parts: [{ geometry: shadeY(merge(parts), -4, h, 0.75, 1.1), mat: 'flat' }] };
}

/** Todas as partes de um modelo em triângulos (para os testes e a medição). */
export function modelTriangles(m: Model): number {
  let t = 0;
  for (const p of m.parts) t += p.geometry.attributes.position.count / 3;
  return t;
}
