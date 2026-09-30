// Objetos de autódromo e de estrada: poste, outdoor, placa de curva, arquibancada com público, pórtico de
// largada, garagens do box, placa do box e cone. Os que "olham a pista" têm a frente em +X (o renderizador
// espelha do lado direito); os de painel olham +Z (para quem vem). A borda que o carro toca fica na meia
// largura de colisão (collisionHalfM). Puro: o atlas de painéis aqui é só o mapa de UV — o desenho é em
// textures.ts.
import * as THREE from 'three';
import { hash3 } from '../noise';
import { box, cone, cyl, merge, paint, shadeY, tf, type Geo, type Model } from './geom';
import { collisionHalfM } from './vegetation';

// ───────────────────────────── Atlas de painéis ─────────────────────────────

export const PANEL_ATLAS_SIZE = 1024;

/** Retângulos (px, origem no canto de cima à esquerda do canvas) de cada arte do atlas. */
export const PANEL_RECTS = {
  billboard: [0, 1, 2, 3, 4, 5, 6, 7].map((i) => [(i % 2) * 512, Math.floor(i / 2) * 192, 512, 192] as const),
  banner: [0, 768, 1024, 128] as const,
  pit: [0, 896, 256, 128] as const,
  chevronLeft: [256, 896, 256, 96] as const,
  chevronRight: [512, 896, 256, 96] as const,
  checker: [768, 896, 256, 128] as const,
};

/** Plano w × h (no plano XY, olhando +Z) com a UV apontando para um retângulo do atlas. */
export function atlasPlane(w: number, h: number, rect: readonly [number, number, number, number]): Geo {
  const g = new THREE.PlaneGeometry(w, h);
  const uv = g.attributes.uv as THREE.BufferAttribute;
  const [x, y, rw, rh] = rect;
  const S = PANEL_ATLAS_SIZE;
  const u0 = x / S; const u1 = (x + rw) / S; const v0 = 1 - (y + rh) / S; const v1 = 1 - y / S;
  for (let i = 0; i < uv.count; i++) uv.setXY(i, u0 + uv.getX(i) * (u1 - u0), v0 + uv.getY(i) * (v1 - v0));
  const out = g.toNonIndexed();
  g.dispose();
  return out;
}

// ───────────────────────────── Poste de luz (sprite `lamp`) ─────────────────────────────

/** Poste com base de concreto (a pegada), braço curvo até a pista, luminária e facho (só à noite). */
/** Distância (m) do pé do poste até o centro da luminária, para o lado da pista. A poça de luz no asfalto (road.ts) lê daqui. */
export const LAMP_HEAD_X = 2.35;

export function streetLamp(): Model {
  const R = collisionHalfM('lamp');
  const metal = '#5d636b';
  const pole = merge([
    paint(box(R * 2, 0.7, R * 2), '#b9b6ae', tf(0, 0.35, 0)),
    paint(box(R * 2 - 0.12, 0.08, R * 2 - 0.12), '#9a978f', tf(0, 0.74, 0)),
    paint(cyl(0.09, 0.14, 8.6, 6, true), metal, tf(0, 0.7 + 4.3, 0)),
    paint(cyl(0.06, 0.06, 1.4, 5, true), metal, tf(0.45, 9.3, 0, 1, 1, 1, 0, 0, -0.9)),
    paint(cyl(0.06, 0.06, 1.3, 5, true), metal, tf(1.55, 9.75, 0, 1, 1, 1, 0, 0, -Math.PI / 2)),
    paint(box(1.0, 0.16, 0.42), '#3c4148', tf(LAMP_HEAD_X, 9.7, 0)),
  ]);
  const head = paint(box(0.86, 0.06, 0.32), '#fff2c8', tf(LAMP_HEAD_X, 9.6, 0));
  const beam = new THREE.ConeGeometry(2.8, 9.4, 14, 1, true).translate(LAMP_HEAD_X, 4.9, 0).toNonIndexed();
  return {
    parts: [
      { geometry: shadeY(pole, 0, 10, 0.8, 1.05), mat: 'flat', shadow: true },
      { geometry: head, mat: 'glow' },
      { geometry: beam, mat: 'cone' },
    ],
    blob: 0.7,
  };
}

// ───────────────────────────── Outdoor (sprite `billboard`) ─────────────────────────────

/** Outdoor de duas pernas (a pegada é a largura toda), moldura, painel do atlas, passarela e refletores. */
export function billboard(variant: number): Model {
  const R = collisionHalfM('billboard');
  const steel = '#3d4148';
  const legX = R - 0.16;
  const frame = merge([
    paint(box(0.32, 5.0, 0.32), steel, tf(-legX, 2.5, 0)), paint(box(0.32, 5.0, 0.32), steel, tf(legX, 2.5, 0)),
    paint(box(0.5, 0.25, 0.5), '#8a8680', tf(-legX, 0.12, 0)), paint(box(0.5, 0.25, 0.5), '#8a8680', tf(legX, 0.12, 0)),
    paint(box(2 * R - 0.3, 0.12, 0.12), steel, tf(0, 1.4, 0)),
    paint(box(2 * R, 3.7, 0.28), '#23262b', tf(0, 6.6, -0.05)),
    paint(box(2 * R + 0.1, 0.16, 0.9), '#6a6e75', tf(0, 4.68, 0.35)),
    paint(box(2 * R + 0.1, 0.5, 0.04), '#50545a', tf(0, 5.0, 0.78)),
  ]);
  const arms: Geo[] = [];
  const lights: Geo[] = [];
  for (let k = 0; k < 3; k++) {
    const x = (k - 1) * R * 0.62;
    arms.push(paint(box(0.06, 0.06, 1.1), steel, tf(x, 8.6, 0.45)));
    arms.push(paint(box(0.4, 0.22, 0.3), '#2c2f34', tf(x, 8.55, 0.95)));
    lights.push(paint(box(0.34, 0.04, 0.24), '#fff4d8', tf(x, 8.43, 0.95)));
  }
  const panel = atlasPlane(2 * R - 0.34, 3.36, PANEL_RECTS.billboard[variant % 8]).translate(0, 6.6, 0.1);
  return {
    parts: [
      { geometry: shadeY(merge([frame, ...arms]), 0, 9, 0.8, 1.05), mat: 'flat', shadow: true },
      { geometry: panel, mat: 'panel', shadow: false },
      { geometry: merge(lights), mat: 'glow' },
    ],
    blob: 1.2,
  };
}

// ───────────────────────────── Placa de curva (sprite `sign_left`/`sign_right`) ─────────────────────────────

/** Chevrons na altura do para-choque: o painel ocupa a largura de colisão inteira. */
export function chevronSign(dir: -1 | 1): Model {
  const R = collisionHalfM('sign_left');
  const posts = merge([
    paint(box(0.12, 1.9, 0.12), '#5a5e66', tf(-R + 0.35, 0.95, -0.08)), paint(box(0.12, 1.9, 0.12), '#5a5e66', tf(R - 0.35, 0.95, -0.08)),
    paint(box(2 * R, 1.08, 0.08), '#2b2e33', tf(0, 1.2, -0.05)),
  ]);
  const panel = atlasPlane(2 * R - 0.06, 1.0, dir < 0 ? PANEL_RECTS.chevronLeft : PANEL_RECTS.chevronRight).translate(0, 1.2, 0.0);
  return { parts: [{ geometry: posts, mat: 'flat', shadow: true }, { geometry: panel, mat: 'panel' }], blob: 0.9 };
}

// ───────────────────────────── Arquibancada (sprite `grandstand`) ─────────────────────────────

const CROWD = ['#ff3b3b', '#ffd23f', '#3ddc84', '#4fc3f7', '#ff7ab6', '#ffffff', '#ff8c1a', '#8e24aa', '#1e88e5', '#f4f4f4'];
const SKIN = ['#f1c7a3', '#d9a07a', '#a86b45', '#6e4630'];

/**
 * Arquibancada que olha a pista (+X): muro com faixas de patrocínio e alambrado na borda de colisão (+R),
 * passarela, 6 degraus com torcida, cobertura em balanço e refletores. 14,4 m ao longo da pista.
 */
export function grandstand(variant: number): Model {
  const R = collisionHalfM('grandstand');
  const L = 14.4;
  const roof = variant === 0 ? '#ffd23f' : '#1e88e5';
  const parts: Geo[] = [];
  // Muro da frente e alambrado (o que o carro toca).
  parts.push(paint(box(0.35, 1.15, L), '#e8e8e4', tf(R - 0.175, 0.575, 0)));
  for (let k = 0; k < 6; k++) parts.push(paint(box(0.04, 0.5, L / 6 - 0.1), k % 2 ? '#d63a3a' : '#1e88e5', tf(R + 0.01, 0.62, -L / 2 + (k + 0.5) * (L / 6))));
  for (let k = 0; k <= 6; k++) parts.push(paint(box(0.08, 3.0, 0.08), '#8a9098', tf(R - 0.2, 2.6, -L / 2 + k * (L / 6))));
  for (const y of [1.6, 2.6, 3.6]) parts.push(paint(box(0.03, 0.03, L), '#9aa0a8', tf(R - 0.2, y, 0)));
  // Base e degraus com público.
  const rows = 6;
  const x0 = R - 2.6; const stepW = 1.25; const stepH = 0.62;
  parts.push(paint(box(R + x0 + 0.4, 0.3, L), '#8f959c', tf((x0 - R) / 2, 0.15, 0)));
  for (let r = 0; r < rows; r++) {
    const x = x0 - r * stepW; const y = 0.3 + r * stepH;
    parts.push(paint(box(stepW, stepH + y, L), r % 2 ? '#aeb4bb' : '#c6cbd1', tf(x - stepW / 2, (stepH + y) / 2, 0)));
    const n = 16;
    for (let i = 0; i < n; i++) {
      if (hash3(variant, r, i) < 0.12) continue;
      const z = -L / 2 + 0.5 + (i + 0.5) * ((L - 1) / n) + (hash3(variant, r, i + 50) - 0.5) * 0.2;
      const px = x - stepW * 0.45;
      const py = y + stepH;
      const shirt = CROWD[Math.floor(hash3(variant, r, i + 7) * CROWD.length)];
      const skin = SKIN[Math.floor(hash3(variant, r, i + 9) * SKIN.length)];
      const h = 0.5 + hash3(variant, r, i + 11) * 0.12;
      parts.push(paint(box(0.34, h, 0.4), shirt, tf(px, py + h / 2, z)));
      parts.push(paint(box(0.22, 0.24, 0.22), skin, tf(px + 0.02, py + h + 0.13, z)));
      if (hash3(variant, r, i + 13) > 0.8) parts.push(paint(box(0.08, 0.5, 0.08), skin, tf(px + 0.05, py + h + 0.2, z + 0.2)));
      if (hash3(variant, r, i + 17) > 0.93) parts.push(paint(box(0.02, 0.5, 0.6), CROWD[(i + r) % CROWD.length], tf(px, py + h + 0.55, z + 0.3)));
    }
  }
  const backX = x0 - rows * stepW;
  const topY = 0.3 + rows * stepH;
  parts.push(paint(box(0.3, topY + 1.2, L), '#9aa0a8', tf(backX - 0.15, (topY + 1.2) / 2, 0)));
  // Cobertura em balanço: pilares atrás, viga, telhado inclinado.
  const roofY = topY + 3.2;
  for (const z of [-L / 2 + 0.3, 0, L / 2 - 0.3]) parts.push(paint(box(0.3, roofY, 0.3), '#4a4e55', tf(backX - 0.1, roofY / 2, z)));
  parts.push(paint(box(R - backX + 0.4 - 1.0, 0.18, L + 0.4), roof, tf((R - 1.0 + backX) / 2, roofY + 0.2, 0, 1, 1, 1, 0, 0, 0.08)));
  parts.push(paint(box(0.25, 0.6, L + 0.4), '#2e3238', tf(R - 1.2, roofY - 0.05, 0)));
  const lights: Geo[] = [];
  for (let k = 0; k < 4; k++) lights.push(paint(box(0.12, 0.18, 1.0), '#fff6dc', tf(R - 1.3, roofY - 0.4, -L / 2 + 1.8 + k * (L - 3.6) / 3)));
  const g = merge(parts);
  return { parts: [{ geometry: shadeY(g, 0, roofY, 0.78, 1.05), mat: 'flat', shadow: true }, { geometry: merge(lights), mat: 'glow' }], blob: 0 };
}

// ───────────────────────────── Pórtico de largada (sprite `banner_start`) ─────────────────────────────

/**
 * Pórtico em balanço: torre à esquerda (fora do asfalto, atrás da linha do alambrado), viga sobre a pista
 * com o painel, a faixa quadriculada e as cinco luzes de largada. Não sólido (x = 0).
 */
export function startGantry(): Model {
  const tower = -10.6;
  const reach = 8.2;
  const steel = '#2b2f36';
  const parts: Geo[] = [
    paint(box(1.3, 11.2, 1.6), steel, tf(tower, 5.6, 0)),
    paint(box(1.6, 0.5, 1.9), '#8a8680', tf(tower, 0.25, 0)),
    paint(box(reach - tower, 1.9, 1.0), '#f2f2f2', tf((reach + tower) / 2, 9.4, 0)),
    paint(box(reach - tower, 0.16, 1.1), '#d63a3a', tf((reach + tower) / 2, 10.42, 0)),
    paint(box(reach - tower, 0.16, 1.1), '#d63a3a', tf((reach + tower) / 2, 8.38, 0)),
    paint(box(0.25, 5.2, 0.25), steel, tf(tower + 2.0, 8.1, 0, 1, 1, 1, 0, 0, 0.6)),
    paint(box(4.6, 0.7, 0.5), '#1a1c20', tf(0, 7.95, 0.3)),
  ];
  const lights: Geo[] = [];
  for (let k = 0; k < 5; k++) lights.push(paint(cyl(0.22, 0.22, 0.12, 10), '#ff2a2a', tf(-1.8 + k * 0.9, 7.95, 0.56, 1, 1, 1, Math.PI / 2, 0, 0)));
  const panel = atlasPlane(15.5, 1.5, PANEL_RECTS.banner).translate(0, 9.4, 0.51);
  const panelBack = atlasPlane(15.5, 1.5, PANEL_RECTS.banner).rotateY(Math.PI).translate(0, 9.4, -0.51);
  const checker = atlasPlane(reach - tower - 0.4, 0.5, PANEL_RECTS.checker).translate((reach + tower) / 2, 10.75, 0);
  const checkerBack = checker.clone().rotateY(Math.PI).translate(reach + tower, 0, 0);
  return {
    parts: [
      { geometry: merge(parts), mat: 'flat', shadow: true },
      { geometry: merge([panel, panelBack, checker, checkerBack]), mat: 'panel' },
      { geometry: merge(lights), mat: 'beacon' },
    ],
  };
}

// ───────────────────────────── Garagens do box (sprite `pit_wall`) ─────────────────────────────

/**
 * Fileira de garagens de 12 m ao longo da pista, fachada (com as portas abertas) na borda de colisão (+R),
 * 10 m de fundo. É o "muro do box": o carro que passa do box encosta na fachada quando bate.
 */
export function pitGarages(team: number): Model {
  const R = collisionHalfM('pit_wall');
  const L = 12; const D = 10; const H = 5.6;
  const teamColors = ['#d63a3a', '#1e88e5', '#ffd23f', '#3ddc84', '#ff8c1a', '#8e24aa'];
  const c1 = teamColors[team % teamColors.length]; const c2 = teamColors[(team + 2) % teamColors.length];
  const parts: Geo[] = [];
  const back = R - D;
  // Corpo: paredes entre as portas, fundo, laje e platibanda.
  parts.push(paint(box(D - 0.4, H, 0.5), '#d9dce0', tf((R + back) / 2 - 0.2, H / 2, -L / 2 + 0.25)));
  parts.push(paint(box(D - 0.4, H, 0.5), '#d9dce0', tf((R + back) / 2 - 0.2, H / 2, 0)));
  parts.push(paint(box(0.4, H, L), '#c9ccd0', tf(back + 0.2, H / 2, 0)));
  parts.push(paint(box(D, 0.5, L), '#eceef0', tf((R + back) / 2, H + 0.25, 0)));
  parts.push(paint(box(0.3, 1.9, L), '#eceef0', tf(R - 0.15, H - 0.55, 0)));
  parts.push(paint(box(0.06, 0.5, L), c1, tf(R + 0.01, H - 0.3, 0)));
  parts.push(paint(box(0.06, 0.25, L), c2, tf(R + 0.01, H - 0.75, 0)));
  // Andar de cima envidraçado (camarote) recuado, com toldo.
  parts.push(paint(box(D * 0.55, 2.4, L), '#e4e6ea', tf(R - D * 0.55 / 2 - 1.2, H + 1.7, 0)));
  parts.push(paint(box(0.05, 1.4, L - 0.6), '#2c3e55', tf(R - 1.18, H + 1.8, 0)));
  parts.push(paint(box(1.3, 0.1, L), c1, tf(R - 0.6, H + 3.0, 0, 1, 1, 1, 0, 0, -0.12)));
  // Interior das duas portas (escuro) com piso claro e pneus empilhados.
  for (const zc of [-L / 4, L / 4]) {
    parts.push(paint(box(0.2, 3.9, L / 2 - 0.9), '#20242a', tf(R - 3.5, 1.95, zc)));
    parts.push(paint(box(3.3, 0.04, L / 2 - 0.9), '#8a9098', tf(R - 1.8, 0.02, zc)));
    for (let k = 0; k < 2; k++) parts.push(paint(cyl(0.34, 0.34, 1.1, 8), '#1a1a1a', tf(R - 3.0, 0.55, zc - 1.6 + k * 3.2)));
    parts.push(paint(box(0.6, 1.0, 1.4), c2, tf(R - 3.0, 0.5, zc)));
  }
  parts.push(paint(box(0.3, 0.9, L), '#e8e8e4', tf(back + 0.2, 0.45, 0)));
  const lights: Geo[] = [];
  for (const zc of [-L / 4, L / 4]) lights.push(paint(box(2.6, 0.06, L / 2 - 1.4), '#f4f8ff', tf(R - 1.9, 3.95, zc)));
  lights.push(paint(box(0.04, 1.2, L - 1.0), '#ffe6b0', tf(R - 1.17, H + 1.8, 0)));
  return { parts: [{ geometry: shadeY(merge(parts), 0, H + 3, 0.82, 1.05), mat: 'flat', shadow: true }, { geometry: merge(lights), mat: 'glow' }] };
}

/** Placa do box (não sólida). */
export function pitSign(): Model {
  const post = merge([paint(cyl(0.07, 0.08, 2.4, 6), '#5a5e66', tf(0, 1.2, 0)), paint(box(1.8, 0.9, 0.06), '#2b2e33', tf(0, 2.6, -0.04))]);
  const panel = atlasPlane(1.74, 0.84, PANEL_RECTS.pit).translate(0, 2.6, 0.0);
  return { parts: [{ geometry: post, mat: 'flat', shadow: true }, { geometry: panel, mat: 'panel' }] };
}

/** Cone de sinalização com faixa refletiva. */
export function trafficCone(): Model {
  const g = merge([
    paint(cone(0.3, 0.74, 10), '#ff6a14', tf(0, 0.39, 0)), paint(box(0.62, 0.05, 0.62), '#222222', tf(0, 0.025, 0)),
    paint(cyl(0.19, 0.235, 0.14, 10), '#f4f4f4', tf(0, 0.36, 0)),
  ]);
  return { parts: [{ geometry: g, mat: 'flat', shadow: true }] };
}
