// Construções: prédios (fachadas com textura de janelas que acende à noite), casas por país, torres
// (TV, mastro, farol, caixa-d'água, cata-vento), cercas, postes com fios, e os pontos de referência
// (turbina eólica, torii, pagode, igreja, barcos). Prédios olham a pista pelo +X com a fachada em x = 0
// (o renderizador põe x = 0 na borda de colisão); torres são simétricas e ocupam ±R.
// Puro: só Three, sem DOM.
import * as THREE from 'three';
import { hash2 } from '../noise';
import { box, cone, cyl, gable, hip, ico, jitter, lean, lumpy, merge, mottle, paint, roundedBar, shadeY, sphere, tf, tintUp, tintUpSoft, tris, type Geo, type MatKey, type Model, type ModelPart } from './geom';

/** Tamanho (m) de uma repetição de cada textura de fachada (4 colunas de janela × 4 andares). */
export const FACADE_TILE: Record<'office' | 'apartment' | 'classic' | 'house', [number, number]> = {
  office: [12, 14], apartment: [12, 12], classic: [14, 15.2], house: [12, 12],
};

type FacadeStyle = keyof typeof FACADE_TILE;

/** Caixa com UV em metros para a textura da fachada; topo e fundo apontam para um canto sem janela. */
function facadeBox(w: number, h: number, d: number, style: FacadeStyle, color: string, m: THREE.Matrix4, vOffset = 0): Geo {
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

interface Parts { flat: Geo[]; glow: Geo[]; beacon: Geo[]; facade: Map<MatKey, Geo[]> }
function newParts(): Parts { return { flat: [], glow: [], beacon: [], facade: new Map() }; }
function addFacade(p: Parts, style: MatKey, g: Geo): void {
  const list = p.facade.get(style) ?? [];
  list.push(g);
  p.facade.set(style, list);
}
function toModel(p: Parts, height: number, blob = 0, shadow = true): Model {
  const parts: ModelPart[] = [];
  if (p.flat.length) parts.push({ geometry: shadeY(merge(p.flat), -1, height, 0.78, 1.06), mat: 'flat', shadow });
  for (const [mat, list] of p.facade) parts.push({ geometry: merge(list), mat, shadow });
  if (p.glow.length) parts.push({ geometry: merge(p.glow), mat: 'glow' });
  if (p.beacon.length) parts.push({ geometry: merge(p.beacon), mat: 'beacon' });
  return { parts, blob };
}

/** Fundação que entra 5 m no chão: prédio em terreno inclinado não flutua. */
function foundation(p: Parts, L: number, D: number, color = '#8f8a82'): void {
  p.flat.push(paint(box(D, 5, L), color, tf(-D / 2, -2.4, 0)));
}

// ───────────────────────────── Prédios de cidade ─────────────────────────────

/** Torre de vidro sobre embasamento; coroa escalonada e antena com luz de balizamento. */
export function glassTower(seed: number, floors: number, tint: string): Model {
  const p = newParts();
  const L = 18; const D = 18;
  foundation(p, L, D);
  addFacade(p, 'office', facadeBox(D, 7, L, 'office', '#dfe6ee', tf(-D / 2, 3.5, 0)));
  p.flat.push(paint(box(D + 0.6, 0.5, L + 0.6), '#c8ccd2', tf(-D / 2, 7.2, 0)));
  const th = floors * 3.5;
  const inset = 2;
  addFacade(p, 'office', facadeBox(D - inset * 2, th, L - inset * 2, 'office', tint, tf(-D / 2, 7.45 + th / 2, 0)));
  const top = 7.45 + th;
  p.flat.push(paint(box(D - inset * 2 + 0.4, 0.6, L - inset * 2 + 0.4), '#d8dde4', tf(-D / 2, top + 0.3, 0)));
  const crown = 3 + hash2(seed, 1) * 5;
  addFacade(p, 'office', facadeBox(D - 8, crown, L - 8, 'office', tint, tf(-D / 2, top + 0.6 + crown / 2, 0)));
  p.flat.push(paint(box(3, 1.4, 3), '#9aa0a8', tf(-D / 2 + 3, top + 1.3, 3)));
  const ant = 6 + hash2(seed, 2) * 8;
  p.flat.push(paint(cyl(0.12, 0.2, ant, 5), '#c7c9cc', tf(-D / 2, top + 0.6 + crown + ant / 2, 0)));
  p.beacon.push(paint(sphere(0.35, 6, 4), '#ff3030', tf(-D / 2, top + 0.6 + crown + ant, 0)));
  // Faixa de luz no topo (acende à noite).
  p.glow.push(paint(box(0.08, 0.35, L - inset * 2 - 1), '#bfe4ff', tf(-inset + 0.05, top - 0.6, 0)));
  return toModel(p, top + crown);
}

/** Bloco de apartamentos com sacadas; térreo com portaria e comércio; caixa-d'água no teto. */
export function apartmentBlock(seed: number, floors: number, tint: string, L = 18, D = 15): Model {
  const p = newParts();
  foundation(p, L, D);
  const H = 4 + floors * 3;
  p.flat.push(paint(box(D, 4, L), '#5a5e66', tf(-D / 2, 2, 0)));
  p.flat.push(paint(box(0.1, 2.6, L - 2), '#2c3e55', tf(0.02, 1.6, 0)));
  p.flat.push(paint(box(1.2, 0.18, L), ['#d63a3a', '#1e88e5', '#ffd23f', '#3ddc84'][seed % 4], tf(0.55, 3.2, 0, 1, 1, 1, 0, 0, -0.15)));
  addFacade(p, 'apartment', facadeBox(D, H - 4, L, 'apartment', tint, tf(-D / 2, 4 + (H - 4) / 2, 0)));
  // Lajes das sacadas em relevo (o que dá volume à fachada).
  for (let f = 1; f < floors; f += 1) p.flat.push(paint(box(0.7, 0.18, L - 1.2), '#e8e6e0', tf(0.3, 4 + f * 3, 0)));
  p.flat.push(paint(box(D + 0.3, 0.8, L + 0.3), '#d8d4cc', tf(-D / 2, H + 0.4, 0)));
  p.flat.push(paint(box(4, 2.6, 3.4), '#b8b2a8', tf(-D / 2 - 1, H + 2.1, -L / 4)));
  p.flat.push(paint(cyl(1, 1, 1.8, 8), '#6a8aa8', tf(-D / 2 + 2, H + 1.7, L / 4)));
  p.glow.push(paint(box(0.06, 0.4, 1.8), '#fff0c0', tf(0.08, 3.6, L / 2 - 2.5)));
  return toModel(p, H + 3);
}

/** Prédio clássico (bulevar europeu): térreo de lojas com toldos, cornija, telhado mansarda com lucarnas. */
export function classicBuilding(seed: number, floors: number, tint: string, L = 21, D = 14): Model {
  const p = newParts();
  foundation(p, L, D, '#9a9286');
  const shopH = 4.6;
  p.flat.push(paint(box(D, shopH, L), '#c9bfae', tf(-D / 2, shopH / 2, 0)));
  const shops = 3;
  const awnings = ['#b8322a', '#2f6b4a', '#1f4f8a', '#c98a2a'];
  for (let k = 0; k < shops; k++) {
    const z = -L / 2 + (k + 0.5) * (L / shops);
    p.flat.push(paint(box(0.12, 3.0, L / shops - 1.6), '#23303e', tf(0.03, 1.8, z)));
    p.flat.push(paint(box(1.4, 0.12, L / shops - 1.2), awnings[(seed + k) % awnings.length], tf(0.65, 3.5, z, 1, 1, 1, 0, 0, -0.35)));
    p.glow.push(paint(box(0.05, 2.4, L / shops - 2.2), '#b8844e', tf(0.1, 1.6, z)));
  }
  const H = floors * 3.8;
  addFacade(p, 'classic', facadeBox(D, H, L, 'classic', tint, tf(-D / 2, shopH + H / 2, 0)));
  for (let f = 1; f <= floors; f++) if (f === 1 || f === floors) p.flat.push(paint(box(0.5, 0.35, L + 0.2), '#e6ddcc', tf(0.15, shopH + f * 3.8 - (f === floors ? 0 : 3.8) + 0.1, 0)));
  const top = shopH + H;
  p.flat.push(paint(box(D + 0.8, 0.5, L + 0.8), '#e6ddcc', tf(-D / 2, top + 0.25, 0)));
  p.flat.push(paint(hip(D, 4.2, L, D * 0.5, L * 0.8), '#5d6878', tf(-D / 2, top + 0.5, 0)));
  for (let k = 0; k < 4; k++) {
    const z = -L / 2 + (k + 0.5) * (L / 4);
    p.flat.push(paint(box(1.2, 1.6, 1.3), '#e6ddcc', tf(-1.4, top + 1.6, z)));
    p.flat.push(paint(gable(1.4, 0.7, 1.5), '#4d5868', tf(-1.4, top + 2.4, z, 1, 1, 1, 0, Math.PI / 2, 0)));
  }
  for (const z of [-L / 2 + 1.5, L / 2 - 2]) p.flat.push(paint(box(0.9, 2.2, 1.4), '#b86a4a', tf(-D * 0.6, top + 4.4, z)));
  return toModel(p, top + 5);
}

/** Prédio de tijolo médio com letreiro luminoso no alto (Sampa, Osaka, Las Vegas). */
export function brickMidrise(seed: number, floors: number, tint: string, neon: string, L = 16, D = 14): Model {
  const p = newParts();
  foundation(p, L, D);
  const H = 4 + floors * 3;
  p.flat.push(paint(box(D, 4, L), '#3a3e46', tf(-D / 2, 2, 0)));
  p.glow.push(paint(box(0.06, 2.4, L - 3), '#ffd8a0', tf(0.04, 1.7, 0)));
  addFacade(p, 'house', facadeBox(D, H - 4, L, 'house', tint, tf(-D / 2, 4 + (H - 4) / 2, 0)));
  p.flat.push(paint(box(D + 0.3, 0.6, L + 0.3), '#6a5a50', tf(-D / 2, H + 0.3, 0)));
  // Letreiro: estrutura + painel de neon (placa vertical na quina, olhando a pista).
  const signH = 4 + hash2(seed, 3) * 4;
  p.flat.push(paint(box(0.4, signH, 2.2), '#1a1c20', tf(0.8, H - signH / 2 - 1, L / 2 - 1.6)));
  p.glow.push(paint(box(0.08, signH - 0.6, 1.8), neon, tf(1.02, H - signH / 2 - 1, L / 2 - 1.6)));
  p.flat.push(paint(box(0.3, 2.6, L * 0.7), '#1a1c20', tf(-D / 2, H + 1.9, 0)));
  p.glow.push(paint(box(0.06, 1.6, L * 0.62), neon, tf(-D / 2 + 0.18, H + 1.95, 0)));
  return toModel(p, H + 4);
}

// ───────────────────────────── Casas por região ─────────────────────────────

/** Casas cúbicas do Mediterrâneo: blocos brancos ou pastel empilhados, uma cúpula azul, escadinha. */
export function cubeHouses(seed: number, walls: string[], dome: boolean, L = 16, D = 14): Model {
  const p = newParts();
  foundation(p, L, D, '#d8d0c0');
  const blocks: Array<[number, number, number, number, number]> = [
    [0, 0, 7, 6, L * 0.55], [0, 0, 6, 5, -L * 0.3], [-7, 5.5, 6, 5, L * 0.2], [-8, 0, 8, 7, -L * 0.1],
  ];
  blocks.forEach(([x, y, d, h, z], k) => {
    const w = k === 0 ? L * 0.42 : L * 0.38;
    const color = walls[(seed + k) % walls.length];
    addFacade(p, 'house', facadeBox(d, h, w, 'house', color, tf(x - d / 2, y + h / 2, z * 0.5)));
    p.flat.push(paint(box(d + 0.2, 0.25, w + 0.2), '#f4f1ea', tf(x - d / 2, y + h + 0.12, z * 0.5)));
  });
  if (dome) {
    p.flat.push(paint(cyl(1.9, 1.9, 1.2, 12), '#ffffff', tf(-10.5, 7.6, -L * 0.05)));
    p.flat.push(paint(sphere(2.0, 12, 6), '#2a62c9', tf(-10.5, 8.2, -L * 0.05, 1, 0.9, 1)));
    p.flat.push(paint(box(0.2, 1.4, 0.2), '#ffffff', tf(-10.5, 10.3, -L * 0.05)));
  }
  for (let k = 0; k < 5; k++) p.flat.push(paint(box(1.2, 0.3, 1.2), '#f0ece2', tf(-0.2 - k * 0.9, 0.15 + k * 0.3, -L * 0.48)));
  p.flat.push(paint(jitter(ico(0.9, 0), 0.1, seed), '#d6368f', tf(0.2, 1.4, L * 0.1, 1, 0.8, 1)));
  return toModel(p, 12);
}

/** Casa de duas águas (Escandinávia, Cabo, Austrália): madeira pintada, telhado, chaminé, varanda. */
export function gableHouse(seed: number, wall: string, roof: string, floors = 2, L = 12, D = 9): Model {
  const p = newParts();
  foundation(p, L, D, '#7a7470');
  const H = floors * 3;
  addFacade(p, 'house', facadeBox(D, H, L, 'house', wall, tf(-D / 2, H / 2, 0)));
  // Cumeeira ao longo da pista: a água da frente olha a estrada.
  p.flat.push(paint(gable(D, 3.6, L, 0.5), roof, tf(-D / 2, H, 0, 1, 1, 1, 0, 0, 0)));
  p.flat.push(paint(box(0.9, 2.4, 0.9), '#6a5a50', tf(-D * 0.7, H + 2.4, L * 0.3)));
  p.flat.push(paint(box(1.6, 0.15, L * 0.5), '#f2f2ea', tf(0.8, 2.6, -L * 0.1)));
  for (let k = 0; k < 3; k++) p.flat.push(paint(box(0.12, 2.6, 0.12), '#f2f2ea', tf(1.5, 1.3, -L * 0.1 - L * 0.22 + k * L * 0.22)));
  p.glow.push(paint(box(0.05, 1.0, 0.9), '#ffdca0', tf(0.03, 1.6, L * 0.3)));
  if (hash2(seed, 5) > 0.5) p.flat.push(paint(box(0.3, 1.4, L + 2), '#f2f2ea', tf(2.8, 0.7, 0)));
  return toModel(p, H + 4);
}

/** Chalé alpino: base de pedra, andar de madeira, beiral largo, sacada com floreiras. */
export function chalet(seed: number): Model {
  const p = newParts();
  const L = 13; const D = 11;
  foundation(p, L, D, '#8a8680');
  p.flat.push(paint(jitter(box(D, 3.2, L), 0.01, seed), '#9a968e', tf(-D / 2, 1.6, 0)));
  p.glow.push(paint(box(0.05, 1.2, 1.4), '#ffd08a', tf(0.02, 1.7, -L * 0.25)));
  addFacade(p, 'house', facadeBox(D, 3.4, L, 'house', '#b87a45', tf(-D / 2, 3.2 + 1.7, 0)));
  p.flat.push(paint(box(1.3, 0.16, L - 1), '#7a4a2a', tf(0.55, 3.35, 0)));
  p.flat.push(paint(box(0.08, 0.9, L - 1), '#6a3e22', tf(1.15, 3.85, 0)));
  for (let k = 0; k < 5; k++) p.flat.push(paint(box(0.3, 0.3, 1.2), '#d63a3a', tf(1.2, 4.4, -L / 2 + 1.5 + k * (L - 3) / 4)));
  p.flat.push(paint(gable(D, 4.4, L, 1.6), '#4a2e22', tf(-D / 2, 6.6, 0)));
  p.flat.push(paint(box(0.8, 1.8, 0.8), '#8a8680', tf(-D * 0.6, 9.8, L * 0.25)));
  const g = toModel(p, 11);
  // Neve no telhado (topo do modelo): pinta as faces voltadas para cima da parte lisa.
  tintUp(g.parts[0].geometry, '#f2f5fb', 0.8, hash2(seed, 9) > 0.4 ? 0.85 : 0);
  return g;
}

/** Casa japonesa: paredes claras, madeira escura, telhado de quatro águas largo e escuro, varanda. */
export function japaneseHouse(seed: number): Model {
  const p = newParts();
  const L = 14; const D = 10;
  foundation(p, L, D, '#6a6258');
  p.flat.push(paint(box(D + 1.6, 0.6, L + 1.6), '#6a4a32', tf(-D / 2, 0.3, 0)));
  addFacade(p, 'house', facadeBox(D, 3.2, L, 'house', '#efe8d8', tf(-D / 2, 0.6 + 1.6, 0)));
  for (let k = 0; k <= 4; k++) p.flat.push(paint(box(0.2, 3.2, 0.2), '#3a2a1e', tf(0.02, 2.2, -L / 2 + k * L / 4)));
  p.flat.push(paint(hip(D + 3.4, 2.8, L + 3.4, 0.6, L * 0.55), '#3a3e46', tf(-D / 2, 3.8, 0)));
  if (hash2(seed, 4) > 0.5) {
    addFacade(p, 'house', facadeBox(D * 0.6, 2.6, L * 0.6, 'house', '#efe8d8', tf(-D / 2, 6.0, 0)));
    p.flat.push(paint(hip(D * 0.6 + 2.4, 2.0, L * 0.6 + 2.4, 0.4, L * 0.3), '#3a3e46', tf(-D / 2, 7.3, 0)));
  }
  p.glow.push(paint(box(0.05, 1.8, L * 0.5), '#ffe0b0', tf(0.04, 2.0, 0)));
  return toModel(p, 9);
}

/** Rondavéis (Drakensberg): cabanas redondas com teto de palha cônico e mureta de pedra. */
export function rondavels(seed: number, L = 14, D = 12): Model {
  const p = newParts();
  const huts: Array<[number, number, number]> = [[-3.2, -L * 0.2, 3.0], [-8.2, L * 0.22, 3.4], [-4, L * 0.38, 2.2]];
  for (const [x, z, r] of huts) {
    p.flat.push(paint(cyl(r, r, 2.8, 12), '#d8b88a', tf(x, 1.4, z)));
    p.flat.push(paint(cyl(r + 0.02, r + 0.02, 0.35, 12), '#f4f0e6', tf(x, 2.5, z)));
    p.flat.push(paint(jitter(cone(r + 0.9, 3.4, 12), 0.03, seed), '#b8964e', tf(x, 2.8 + 1.7, z)));
    p.flat.push(paint(box(0.1, 1.9, 1.0), '#5a3e28', tf(x + r, 0.95, z)));
  }
  for (let k = 0; k < 8; k++) p.flat.push(paint(jitter(box(0.8, 0.7, 1.8), 0.08, seed + k), '#8a8272', tf(0.4, 0.35, -L / 2 + (k + 0.5) * (L / 8))));
  return toModel(p, 7, 0);
}

/** Casa de fazenda: paredes brancas, telhado de telha (quatro águas), varanda de colunas. */
export function farmhouse(seed: number, roof = '#b8552e', wall = '#f2ede2', L = 15, D = 11): Model {
  const p = newParts();
  foundation(p, L, D, '#8a7a6a');
  addFacade(p, 'house', facadeBox(D, 3.6, L, 'house', wall, tf(-D / 2, 1.8, 0)));
  p.flat.push(paint(box(D + 0.2, 0.4, L + 0.2), '#6a4a32', tf(-D / 2, 0.2, 0)));
  p.flat.push(paint(hip(D + 4, 3.2, L + 1.6, 0.6, L * 0.5), roof, tf(-D / 2 + 1.2, 3.6, 0)));
  for (let k = 0; k < 5; k++) p.flat.push(paint(box(0.22, 3.6, 0.22), '#f2ede2', tf(2.8, 1.8, -L / 2 + 0.6 + k * (L - 1.2) / 4)));
  p.flat.push(paint(box(0.12, 0.9, L), '#6a4a32', tf(3.0, 0.45, 0)));
  p.glow.push(paint(box(0.05, 1.1, 1.0), '#ffd8a0', tf(0.03, 1.8, L * 0.2)));
  if (hash2(seed, 2) > 0.5) p.flat.push(paint(jitter(ico(1.2, 0), 0.1, seed), '#d6368f', tf(2.6, 1.0, -L * 0.42)));
  return toModel(p, 7);
}

/** Galpão portuário/industrial: telha ondulada (faixas), portões, letreiro. */
export function warehouse(seed: number, wall: string, L = 20, D = 18): Model {
  const p = newParts();
  foundation(p, L, D);
  const H = 9;
  p.flat.push(paint(box(D, H, L), wall, tf(-D / 2, H / 2, 0)));
  for (let k = 0; k < 10; k++) p.flat.push(paint(box(0.06, H, 0.18), new THREE.Color(wall).multiplyScalar(0.85).getStyle(), tf(0.02, H / 2, -L / 2 + (k + 0.5) * (L / 10))));
  p.flat.push(paint(box(0.1, 5, 6), '#3a3e46', tf(0.05, 2.5, -L * 0.2)));
  p.flat.push(paint(gable(D, 2.2, L, 0.3), '#8a9098', tf(-D / 2, H, 0)));
  p.flat.push(paint(box(0.1, 1.3, 8), ['#1e88e5', '#d63a3a'][seed % 2], tf(0.07, H - 1.3, L * 0.18)));
  p.glow.push(paint(box(0.3, 0.25, 0.5), '#fff0c8', tf(0.3, 5.6, -L * 0.2)));
  return toModel(p, H + 2);
}

// ───────────────────────────── Torres (sprite `tower`: simétricas, ±R) ─────────────────────────────

/** Torre de TV de concreto sobre embasamento que ocupa a pegada; cabine com janelas acesas. */
export function tvTower(seed: number, R: number): Model {
  const p = newParts();
  const H = 46 + hash2(seed, 1) * 18;
  p.flat.push(paint(box(2 * R, 5, 2 * R), '#b8bcc4', tf(0, 0, 0)));
  p.flat.push(paint(box(2 * R - 1, 1.4, 2 * R - 1), '#9aa0a8', tf(0, 3.2, 0)));
  p.flat.push(paint(cyl(1.3, 2.2, H, 10), '#d8dade', tf(0, 3.9 + H / 2, 0)));
  const podY = 3.9 + H * 0.82;
  p.flat.push(paint(cyl(4.2, 3.0, 2.2, 14), '#c8ccd2', tf(0, podY - 1.6, 0)));
  p.flat.push(paint(cyl(4.4, 4.4, 0.4, 14), '#e8eaee', tf(0, podY + 1.5, 0)));
  p.flat.push(paint(cyl(3.2, 4.2, 1.6, 14), '#c8ccd2', tf(0, podY + 2.5, 0)));
  p.glow.push(paint(cyl(4.25, 4.25, 1.0, 14, true), '#9fd6ff', tf(0, podY + 0.2, 0)));
  p.flat.push(paint(cyl(0.3, 0.6, 14, 6), '#e0e0e0', tf(0, podY + 10, 0)));
  for (let k = 0; k < 3; k++) p.flat.push(paint(cyl(0.34, 0.34, 1.2, 6), k % 2 ? '#e8e8e8' : '#d63a3a', tf(0, podY + 12 + k * 1.3, 0)));
  p.beacon.push(paint(sphere(0.5, 6, 4), '#ff3030', tf(0, podY + 17.3, 0)));
  return toModel(p, podY + 18, R * 0.9);
}

/** Mastro treliçado vermelho e branco, pés na borda da pegada, luz de balizamento. */
export function latticeMast(seed: number, R: number): Model {
  const p = newParts();
  const H = 38 + hash2(seed, 1) * 16;
  const sections = 5;
  const topR = 0.6;
  for (let s = 0; s < sections; s++) {
    const t0 = s / sections; const t1 = (s + 1) / sections;
    const baseR = R * Math.SQRT2 - 0.16;
    const r0 = baseR + (topR - baseR) * t0; const r1 = baseR + (topR - baseR) * t1;
    const y0 = H * t0; const y1 = H * t1;
    const color = s % 2 ? '#f2f2f2' : '#d63a3a';
    for (let k = 0; k < 4; k++) {
      const a = Math.PI / 4 + k * Math.PI / 2;
      const c = Math.cos(a); const sn = Math.sin(a);
      const len = Math.hypot(y1 - y0, r0 - r1);
      const tilt = Math.atan2(r0 - r1, y1 - y0);
      p.flat.push(paint(box(0.22, len, 0.22), color, lean(c * (r0 + r1) / 2, (y0 + y1) / 2, sn * (r0 + r1) / 2, a + Math.PI, tilt)));
    }
    for (const [dx, dz] of [[1, 0], [0, 1], [-1, 0], [0, -1]] as const) {
      const w = r1 * Math.SQRT2 * 0.7071 * 2;
      p.flat.push(paint(box(dz !== 0 ? w : 0.08, 0.08, dx !== 0 ? w : 0.08), '#8a8a8a', tf(dx * r1 * 0.7071, y1, dz * r1 * 0.7071)));
    }
  }
  p.flat.push(paint(box(2 * R, 0.4, 2 * R), '#9a968e', tf(0, 0.2, 0)));
  p.flat.push(paint(box(3, 2.6, 2.4), '#c8c4bc', tf(R * 0.4, 1.3, -R * 0.4)));
  p.beacon.push(paint(sphere(0.45, 6, 4), '#ff3030', tf(0, H + 0.5, 0)));
  return toModel(p, H, 0);
}

/** Torre de escritórios sobre embasamento que ocupa a pegada (±R): fuste envidraçado, coroa e antena. */
export function officeSpire(seed: number, R: number, tint: string): Model {
  const p = newParts();
  const podH = 7;
  addFacade(p, 'office', facadeBox(2 * R, podH, 2 * R, 'office', '#dfe6ee', tf(0, podH / 2, 0)));
  p.flat.push(paint(box(2 * R + 0.6, 0.5, 2 * R + 0.6), '#c8ccd2', tf(0, podH + 0.25, 0)));
  const w = Math.max(6, 2 * R - 3);
  const H = 42 + hash2(seed, 1) * 30;
  addFacade(p, 'office', facadeBox(w, H, w, 'office', tint, tf(0, podH + 0.5 + H / 2, 0)));
  const top = podH + 0.5 + H;
  p.flat.push(paint(box(w + 0.4, 0.6, w + 0.4), '#d8dde4', tf(0, top + 0.3, 0)));
  addFacade(p, 'office', facadeBox(w * 0.6, 5, w * 0.6, 'office', tint, tf(0, top + 3.1, 0)));
  p.flat.push(paint(cyl(0.12, 0.2, 10, 5), '#c7c9cc', tf(0, top + 10.6, 0)));
  p.beacon.push(paint(new THREE.OctahedronGeometry(0.4, 0), '#ff3030', tf(0, top + 15.8, 0)));
  p.glow.push(paint(box(w + 0.1, 0.3, w + 0.1), '#bfe4ff', tf(0, top - 0.5, 0)));
  return toModel(p, top + 16, 0);
}

/** Prédio clássico com cúpula (Paris, Roma): tambor com colunas sobre base quadrada (±R), cúpula e lanterna. */
export function domeBuilding(seed: number, R: number): Model {
  const p = newParts();
  const baseH = 12;
  addFacade(p, 'classic', facadeBox(2 * R, baseH, 2 * R, 'classic', '#efe4cc', tf(0, baseH / 2, 0)));
  p.flat.push(paint(box(2 * R + 0.8, 0.8, 2 * R + 0.8), '#e6dcc6', tf(0, baseH + 0.4, 0)));
  const dr = Math.min(R * 0.72, 9);
  p.flat.push(paint(cyl(dr, dr, 7, 16), '#efe6d2', tf(0, baseH + 0.8 + 3.5, 0)));
  for (let k = 0; k < 12; k++) {
    const a = (k / 12) * Math.PI * 2;
    p.flat.push(paint(box(0.7, 7, 0.7), '#fbf6ea', tf(Math.cos(a) * (dr + 0.4), baseH + 0.8 + 3.5, Math.sin(a) * (dr + 0.4))));
  }
  p.flat.push(paint(cyl(dr + 0.8, dr + 0.8, 0.6, 16), '#e6dcc6', tf(0, baseH + 8.1, 0)));
  p.flat.push(paint(sphere(dr, 16, 6, 0, Math.PI * 2, 0, Math.PI / 2), hash2(seed, 2) > 0.5 ? '#6f8f8a' : '#8a929c', tf(0, baseH + 8.4, 0, 1, 1.15, 1)));
  p.flat.push(paint(cyl(1.0, 1.2, 3, 8), '#efe6d2', tf(0, baseH + 8.4 + dr * 1.15 + 1.2, 0)));
  p.flat.push(paint(cone(1.3, 2.4, 8), '#6f8f8a', tf(0, baseH + 8.4 + dr * 1.15 + 3.9, 0)));
  p.glow.push(paint(cyl(dr + 0.05, dr + 0.05, 1.2, 16, true), '#ffd89a', tf(0, baseH + 3.2, 0)));
  return toModel(p, baseH + 8.4 + dr * 1.3 + 6, 0);
}

/** Farol sobre rochedo (o rochedo é a pegada); lanterna acesa e facho girando à noite. */
export function lighthouse(seed: number, R: number, bands = '#d63a3a'): Model {
  const p = newParts();
  p.flat.push(paint(jitter(ico(1, 1), 0.12, seed), '#6f6a64', tf(0, 0.6, 0, R, 3.2, R)));
  const H = 20; const y0 = 2.6;
  const segs = 5;
  for (let k = 0; k < segs; k++) {
    const t0 = k / segs; const t1 = (k + 1) / segs;
    const r0 = 2.3 - 0.7 * t0; const r1 = 2.3 - 0.7 * t1;
    p.flat.push(paint(cyl(r1, r0, H / segs, 12), k % 2 ? bands : '#f6f6f2', tf(0, y0 + (t0 + t1) / 2 * H, 0)));
  }
  const top = y0 + H;
  p.flat.push(paint(cyl(2.2, 2.2, 0.35, 12), '#2e3238', tf(0, top + 0.17, 0)));
  p.flat.push(paint(cyl(2.25, 2.25, 0.7, 12, true), '#2e3238', tf(0, top + 0.7, 0)));
  p.glow.push(paint(cyl(1.3, 1.3, 2.0, 10), '#fff4c8', tf(0, top + 1.4, 0)));
  p.flat.push(paint(cone(1.6, 1.5, 10), bands, tf(0, top + 3.1, 0)));
  p.flat.push(paint(box(4, 3, 3.4), '#f2f0ea', tf(R * 0.35, 2.4 + 1.5, R * 0.3)));
  p.flat.push(paint(gable(4, 1.6, 3.4, 0.2), bands, tf(R * 0.35, 5.4, R * 0.3)));
  return toModel(p, top + 4, 0);
}

/** Facho do farol: cone aberto deitado (+X), para girar em Y (só à noite). */
export function lighthouseBeam(): Model {
  const g = new THREE.ConeGeometry(6, 70, 12, 1, true).rotateZ(Math.PI / 2).translate(-35, 0, 0).toNonIndexed();
  return { parts: [{ geometry: g, mat: 'cone' }] };
}

/** Caixa-d'água de fazenda: quatro pernas na borda da pegada, travamento, tanque com teto cônico. */
export function waterTower(seed: number, R: number): Model {
  const p = newParts();
  const H = 12 + hash2(seed, 1) * 4;
  const legR = R * Math.SQRT2 - 0.5;
  for (let k = 0; k < 4; k++) {
    const a = Math.PI / 4 + k * Math.PI / 2;
    const tilt = Math.atan2(legR - 2.2, H);
    p.flat.push(paint(cyl(0.16, 0.2, H / Math.cos(tilt), 6), '#8a7a6a', lean(Math.cos(a) * (legR + 2.2) / 2, H / 2, Math.sin(a) * (legR + 2.2) / 2, a + Math.PI, tilt)));
    p.flat.push(paint(box(0.7, 0.4, 0.7), '#9a968e', tf(Math.cos(a) * legR, 0.2, Math.sin(a) * legR)));
  }
  for (const y of [H * 0.33, H * 0.66]) {
    const r = legR + (2.2 - legR) * (y / H);
    for (const [dx, dz] of [[1, 0], [0, 1], [-1, 0], [0, -1]] as const) {
      p.flat.push(paint(box(dz !== 0 ? r * 1.414 : 0.1, 0.1, dx !== 0 ? r * 1.414 : 0.1), '#7a6a5a', tf(dx * r * 0.707, y, dz * r * 0.707)));
    }
  }
  p.flat.push(paint(cyl(3.2, 3.2, 4.6, 14), '#c9c4b8', tf(0, H + 2.3, 0)));
  p.flat.push(paint(cyl(3.25, 3.25, 0.8, 14), '#d63a3a', tf(0, H + 2.8, 0)));
  p.flat.push(paint(cone(3.5, 1.8, 14), '#8a8f96', tf(0, H + 5.5, 0)));
  return toModel(p, H + 6, 0);
}

/** Cata-vento de fazenda (Karoo, Outback, Rota 66): torre treliçada até a pegada; o cata-vento é à parte. */
export function windpumpTower(seed: number, R: number): Model {
  const p = newParts();
  const H = 11 + hash2(seed, 1) * 3;
  for (let k = 0; k < 4; k++) {
    const a = Math.PI / 4 + k * Math.PI / 2;
    const legR = R * Math.SQRT2 - 0.1;
    const tilt = Math.atan2(legR - 0.3, H);
    p.flat.push(paint(box(0.12, H / Math.cos(tilt), 0.12), '#9a9ea4', lean(Math.cos(a) * (legR + 0.3) / 2, H / 2, Math.sin(a) * (legR + 0.3) / 2, a + Math.PI, tilt)));
    p.flat.push(paint(box(0.5, 0.3, 0.5), '#9a968e', tf(Math.cos(a) * (legR - 0.1), 0.15, Math.sin(a) * (legR - 0.1))));
  }
  for (let s = 1; s < 5; s++) {
    const y = (H * s) / 5; const r = (R * Math.SQRT2 - 0.1) + (0.3 - (R * Math.SQRT2 - 0.1)) * (y / H);
    for (const [dx, dz] of [[1, 0], [0, 1], [-1, 0], [0, -1]] as const) p.flat.push(paint(box(dz !== 0 ? r * 1.414 : 0.06, 0.06, dx !== 0 ? r * 1.414 : 0.06), '#8a8e94', tf(dx * r * 0.707, y, dz * r * 0.707)));
  }
  p.flat.push(paint(box(0.8, 0.5, 0.5), '#6a6e74', tf(0, H + 0.2, 0)));
  p.flat.push(paint(box(0.06, 1.8, 3.2), '#b8bcc2', tf(0, H + 0.6, -2.0)));
  // Tanque australiano de chapa ao lado (dentro da pegada).
  p.flat.push(paint(cyl(1.6, 1.6, 2.2, 12), '#b8bcc2', tf(R * 0.55, 1.1, R * 0.4)));
  p.flat.push(paint(cone(1.7, 0.5, 12), '#9a9ea4', tf(R * 0.55, 2.45, R * 0.4)));
  return toModel(p, H + 2, 0);
}

/** Roda do cata-vento (no plano XY, gira em torno de Z): 12 pás num aro. */
export function windpumpFan(): Model {
  const parts: Geo[] = [paint(cyl(0.25, 0.25, 0.3, 8), '#6a6e74', tf(0, 0, 0, 1, 1, 1, Math.PI / 2, 0, 0))];
  for (let k = 0; k < 12; k++) {
    const a = (k / 12) * Math.PI * 2;
    parts.push(paint(box(0.36, 1.3, 0.04), '#d8dce2', tf(Math.sin(a) * 1.05, Math.cos(a) * 1.05, 0.1, 1, 1, 1, 0, 0, -a)));
  }
  parts.push(paint(cyl(1.75, 1.75, 0.06, 16, true), '#9a9ea4', tf(0, 0, 0.1, 1, 1, 1, Math.PI / 2, 0, 0)));
  return { parts: [{ geometry: merge(parts), mat: 'flat', shadow: true }] };
}

// ───────────────────────────── Decoração de beira (só visual) ─────────────────────────────

export type FenceStyle = 'ranch' | 'rail' | 'armco' | 'stone' | 'railing' | 'hedge';

/** Um lance de cerca de 4 m (um segmento) ao longo de Z, com o poste em z = −2. */
export function fence(style: FenceStyle, seed: number): Model {
  const parts: Geo[] = [];
  const L = 4.02;
  switch (style) {
    case 'ranch':
      parts.push(paint(box(0.14, 1.35, 0.14), '#7a5a3a', tf(0, 0.62, -2, 1, 1, 1, 0, 0, (hash2(seed, 1) - 0.5) * 0.08)));
      for (const y of [0.75, 1.15]) parts.push(paint(box(0.025, 0.025, L), '#6a6a6a', tf(0, y, 0)));
      break;
    case 'rail':
      parts.push(paint(box(0.16, 1.3, 0.16), '#f2f0ea', tf(0, 0.6, -2)));
      for (const y of [0.45, 0.8, 1.15]) parts.push(paint(box(0.06, 0.16, L), '#f2f0ea', tf(0, y, 0)));
      break;
    case 'armco':
      parts.push(paint(box(0.12, 0.8, 0.16), '#8a9098', tf(-0.1, 0.4, -2)));
      parts.push(paint(box(0.08, 0.34, L), '#c8ccd2', tf(0.02, 0.62, 0)));
      parts.push(paint(box(0.1, 0.08, L), '#dfe3e8', tf(0.03, 0.62, 0)));
      break;
    case 'stone':
      for (let k = 0; k < 4; k++) parts.push(paint(jitter(box(0.62, 0.42, 1.08), 0.08, seed + k), k % 2 ? '#a8a092' : '#968e80', tf(0, 0.2, -1.5 + k * 1.0)));
      for (let k = 0; k < 3; k++) parts.push(paint(jitter(box(0.54, 0.36, 1.2), 0.08, seed + k + 9), k % 2 ? '#9a9284' : '#b0a898', tf(0, 0.58, -1.3 + k * 1.3)));
      break;
    case 'railing':
      parts.push(paint(box(0.08, 1.05, 0.08), '#e8eaee', tf(0, 0.52, -2)));
      for (const y of [0.55, 1.02]) parts.push(paint(box(0.06, 0.06, L), '#e8eaee', tf(0, y, 0)));
      break;
    case 'hedge':
      // Cerca-viva aparada de quinas redondas (era uma caixa). A seção não muda ao longo do lance: emenda sem degrau.
      parts.push(paint(roundedBar(0.9, 1.1, L + 0.1, 0.32, 3), '#3c7a3a'));
      break;
  }
  const g = merge(parts);
  return { parts: [{ geometry: shadeY(g, 0, 1.3, 0.8, 1.08), mat: 'flat' }] };
}

/** Poste de madeira com travessa e isoladores (os fios são um modelo à parte, esticado por quadro). */
export function utilityPole(): Model {
  const g = merge([
    paint(cyl(0.12, 0.16, 9.4, 6), '#6a5238', tf(0, 4.7, 0)),
    paint(box(0.14, 0.16, 2.4), '#5a4430', tf(0, 8.8, 0)),
    paint(cyl(0.06, 0.06, 0.2, 5), '#e8ecf0', tf(0, 9.0, -1.0)), paint(cyl(0.06, 0.06, 0.2, 5), '#e8ecf0', tf(0, 9.0, 1.0)),
    paint(cyl(0.06, 0.06, 0.2, 5), '#e8ecf0', tf(0, 9.5, 0)),
  ]);
  return { parts: [{ geometry: g, mat: 'flat' }], blob: 0.4 };
}

/** Fios entre dois postes: comprimento 1 em −Z (a instância estica até o próximo poste). */
export function wires(): Model {
  const parts: Geo[] = [];
  for (const [x, y] of [[-1.0, 9.1], [1.0, 9.1], [0, 9.6]] as const) parts.push(paint(box(0.035, 0.035, 1), '#2a2a2a', tf(x, y, -0.5)));
  return { parts: [{ geometry: merge(parts), mat: 'flat' }] };
}

/** Turbina eólica (torre + nacele); o rotor é à parte e gira em torno de X. */
export function windTurbine(): Model {
  const g = merge([
    paint(cyl(1.1, 2.0, 62, 10), '#f2f4f6', tf(0, 31, 0)),
    paint(box(2.2, 2.4, 6.0), '#e8eaee', tf(0, 63, -0.8)),
    paint(box(3, 0.6, 3), '#c8ccd2', tf(0, 0.3, 0)),
  ]);
  return { parts: [{ geometry: shadeY(g, 0, 64, 0.85, 1.05), mat: 'flat' }], blob: 2.5 };
}

/** Rotor de três pás (gira em torno de Z do modelo, que o renderizador põe olhando a pista). */
export function turbineRotor(): Model {
  const parts: Geo[] = [paint(sphere(1.0, 8, 6), '#f2f4f6', tf(0, 0, 0, 1, 1, 1.4))];
  for (let k = 0; k < 3; k++) {
    const a = (k / 3) * Math.PI * 2;
    parts.push(paint(box(0.3, 26, 1.4), '#f6f7f8', tf(Math.sin(a) * 13, Math.cos(a) * 13, 0.2, 1, 1, 1, 0, 0, -a)));
  }
  return { parts: [{ geometry: merge(parts), mat: 'flat' }] };
}

/** Portal torii vermelho (Japão). */
export function torii(): Model {
  const g = merge([
    paint(cyl(0.45, 0.5, 9, 10), '#d8402a', tf(-4.4, 4.5, 0)), paint(cyl(0.45, 0.5, 9, 10), '#d8402a', tf(4.4, 4.5, 0)),
    paint(box(12.4, 0.8, 1.1), '#2a2a2a', tf(0, 9.4, 0)), paint(box(11.6, 0.6, 0.9), '#d8402a', tf(0, 8.7, 0)),
    paint(box(10, 0.5, 0.6), '#d8402a', tf(0, 7.3, 0)), paint(box(0.7, 1.4, 0.4), '#d8402a', tf(0, 8.0, 0)),
    paint(box(1.3, 0.6, 1.3), '#3a3a3a', tf(-4.4, 0.3, 0)), paint(box(1.3, 0.6, 1.3), '#3a3a3a', tf(4.4, 0.3, 0)),
  ]);
  return { parts: [{ geometry: g, mat: 'flat', shadow: true }], blob: 3 };
}

/** Pagode de cinco andares (Japão). */
export function pagoda(): Model {
  const parts: Geo[] = [paint(box(9, 1.2, 9), '#8a8680', tf(0, 0.6, 0))];
  let y = 1.2;
  for (let k = 0; k < 5; k++) {
    const w = 6.4 - k * 0.8; const h = 2.6;
    parts.push(paint(box(w, h, w), k % 2 ? '#e8e0cc' : '#c8402a', tf(0, y + h / 2, 0)));
    parts.push(paint(hip(w + 3.6, 1.3, w + 3.6, w * 0.5, w * 0.5), '#3a3e46', tf(0, y + h, 0)));
    y += h + 1.1;
  }
  parts.push(paint(cyl(0.18, 0.25, 5, 6), '#b8a060', tf(0, y + 2.5, 0)));
  return { parts: [{ geometry: shadeY(merge(parts), 0, y + 5, 0.8, 1.06), mat: 'flat', shadow: true }], blob: 5 };
}

/** Igrejinha de vila com torre pontuda (Alpes, Escandinávia, interior do Brasil). */
export function chapel(seed: number, wall = '#f2efe6', roof = '#6a4a3a'): Model {
  const parts: Geo[] = [
    paint(box(8, 6, 14), wall, tf(0, 3, 0)), paint(gable(8, 4, 14, 0.4), roof, tf(0, 6, 0)),
    paint(box(4, 13, 4), wall, tf(0, 6.5, 8)), paint(hip(4.4, 7, 4.4), roof, tf(0, 13, 8)),
    paint(box(0.1, 1.6, 1.2), '#2a2a2a', tf(2.05, 10.5, 8)), paint(box(0.2, 2.6, 2.0), '#5a3e28', tf(0, 1.3, 10.02)),
  ];
  const g = shadeY(merge(parts), 0, 20, 0.82, 1.05);
  return { parts: [{ geometry: g, mat: 'flat', shadow: true }, { geometry: paint(box(0.4, 0.5, 0.3), '#ffe0a0', tf(0, 4.2, -7.02 + hash2(seed, 1) * 0)), mat: 'glow' }], blob: 0 };
}

/** Veleiro (flutua no mar). */
export function sailboat(seed: number): Model {
  const hull = hash2(seed, 1) > 0.5 ? '#f4f4f4' : '#1e3a5a';
  const parts: Geo[] = [
    paint(box(2.4, 1.0, 8), hull, tf(0, 0.3, 0)), paint(cone(1.2, 2.4, 4), hull, tf(0, 0.3, -5.1, 1, 0.42, 1, -Math.PI / 2, 0, 0)),
    paint(box(1.6, 0.7, 2.6), '#e8e4da', tf(0, 1.1, 1)), paint(cyl(0.07, 0.07, 10, 5), '#d8d8d8', tf(0, 5.8, -0.8)),
    paint(tris([0, 1.4, -0.7, 0, 10.4, -0.9, 0, 1.4, 3.2]), '#fbfbf6'), paint(tris([0, 1.4, -1.2, 0, 9, -1.0, 0, 1.4, -4.8]), '#f2f0e8'),
  ];
  return { parts: [{ geometry: merge(parts), mat: 'flat' }], blob: 0 };
}

/** Iate (Mônaco): casco branco, conveses escalonados, janelas escuras. */
export function yacht(seed: number): Model {
  const parts: Geo[] = [
    paint(box(5, 2.0, 18), '#f6f6f4', tf(0, 0.6, 0)), paint(cone(2.5, 5, 4), '#f6f6f4', tf(0, 0.6, -11.4, 1, 0.4, 1, -Math.PI / 2, Math.PI / 4, 0)),
    paint(box(4.2, 1.6, 10), '#f0f0ee', tf(0, 2.4, 1.5)), paint(box(4.25, 0.5, 9.6), '#1f2a38', tf(0, 2.4, 1.5)),
    paint(box(3.2, 1.4, 6), '#f0f0ee', tf(0, 3.9, 2.2)), paint(box(3.25, 0.4, 5.6), '#1f2a38', tf(0, 3.9, 2.2)),
    paint(box(0.2, 2.4, 0.2), '#c8ccd2', tf(0, 5.6, 1.5)),
  ];
  const lights: Geo[] = [paint(box(4.3, 0.12, 9.2), '#ffe6b0', tf(0, 2.0, 1.5)), paint(sphere(0.2, 5, 3), '#ff4040', tf(-2.5, 1.8, -4)), paint(sphere(0.2, 5, 3), '#40ff60', tf(2.5, 1.8, -4))];
  return { parts: [{ geometry: merge(parts), mat: 'flat' }, { geometry: merge(lights), mat: 'glow' }], blob: 0 };
}

/** Barco de pesca colorido (Escandinávia, Japão, Cabo). */
export function fishingBoat(seed: number): Model {
  const hullColors = ['#d63a3a', '#1e88e5', '#2f8a4a', '#f2c33a'];
  const hull = hullColors[seed % hullColors.length];
  const parts: Geo[] = [
    paint(box(3, 1.6, 9), hull, tf(0, 0.4, 0)), paint(box(3.05, 0.3, 9.05), '#f4f4f4', tf(0, 1.15, 0)),
    paint(box(2.2, 2.2, 2.8), '#f4f4f4', tf(0, 2.3, 1.6)), paint(box(2.3, 0.3, 3.0), '#3a3e46', tf(0, 3.5, 1.6)),
    paint(cyl(0.08, 0.08, 5, 5), '#d8d8d8', tf(0, 3.5, -2)),
  ];
  return { parts: [{ geometry: merge(parts), mat: 'flat' }, { geometry: paint(box(2.25, 0.5, 0.1), '#ffe6b0', tf(0, 2.6, 0.18)), mat: 'glow' }], blob: 0 };
}

/** Guarda-sol de praia com cadeiras (fica na areia além do alcance do carro). */
export function beachUmbrella(seed: number): Model {
  const colors = ['#ff4f5a', '#ffd23f', '#1e88e5', '#3ddc84', '#ff8c1a'];
  const c = colors[seed % colors.length];
  const parts: Geo[] = [paint(cyl(0.04, 0.04, 2.4, 5), '#e8e8e8', tf(0, 1.2, 0))];
  for (let k = 0; k < 8; k++) {
    const a = (k / 8) * Math.PI * 2;
    parts.push(paint(tris([0, 2.6, 0, Math.cos(a) * 1.4, 2.1, Math.sin(a) * 1.4, Math.cos(a + 0.785) * 1.4, 2.1, Math.sin(a + 0.785) * 1.4]), k % 2 ? c : '#ffffff'));
  }
  parts.push(paint(box(0.6, 0.3, 1.6), colors[(seed + 2) % colors.length], tf(0.9, 0.2, 0.4, 1, 1, 1, 0, 0, 0.2)));
  return { parts: [{ geometry: merge(parts), mat: 'flat' }], blob: 1.2 };
}

/** Moinho de vento clássico de pedra (Mediterrâneo): corpo cilíndrico caiado e telhado cônico. */
export function stoneWindmill(): Model {
  const g = merge([
    paint(cyl(2.4, 2.8, 8, 12), '#f4f2ea', tf(0, 4, 0)), paint(cone(2.8, 2.6, 12), '#8a5a3a', tf(0, 9.3, 0)),
    paint(box(0.1, 1.8, 1.0), '#3a5a8a', tf(2.62, 0.9, 0)),
  ]);
  const sails: Geo[] = [];
  for (let k = 0; k < 4; k++) {
    const a = (k / 4) * Math.PI * 2 + 0.3;
    sails.push(paint(box(0.1, 6, 1.2), '#e8e2d2', tf(3.0, 8.4 + Math.cos(a) * 3.2, Math.sin(a) * 3.2, 1, 1, 1, a, 0, 0)));
  }
  return { parts: [{ geometry: merge([g, ...sails]), mat: 'flat', shadow: true }], blob: 3 };
}

/** Molhe de madeira entrando no mar (+X é a praia): 40 m de deque sobre estacas. */
export function pier(): Model {
  const parts: Geo[] = [paint(box(40, 0.3, 3), '#8a6a4a', tf(-20, 0, 0))];
  for (let k = 0; k < 9; k++) for (const z of [-1.3, 1.3]) parts.push(paint(cyl(0.15, 0.15, 5, 5), '#6a5238', tf(-2 - k * 4.6, -2.4, z)));
  for (let k = 0; k < 9; k++) parts.push(paint(box(0.08, 1.0, 0.08), '#f2f0ea', tf(-2 - k * 4.6, 0.6, 1.4)));
  parts.push(paint(box(40, 0.06, 0.06), '#f2f0ea', tf(-20, 1.1, 1.4)));
  return { parts: [{ geometry: merge(parts), mat: 'flat' }] };
}

/** Pedras soltas grandes no mar/encosta (um penedo): redondo em bolhas macias, a cor por ponto (era por face). */
export function seaRock(seed: number, color: string, r: number): Model {
  const g = paint(lumpy(sphere(1, 14, 7), 0.12, seed), color, tf(0, r * 0.2, 0, r, r * 0.8, r * 0.9));
  return { parts: [{ geometry: mottle(tintUpSoft(g, '#ffffff', 0.7, 0.2), 0.1, seed, 0.5), mat: 'flat' }] };
}

