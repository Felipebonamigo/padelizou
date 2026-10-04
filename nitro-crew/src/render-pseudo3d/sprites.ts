// Sprites do modo Retrô: desenhados uma vez em bitmaps pequenos (pixel art procedural, sem arquivo de
// imagem) e escalados sem suavização na hora de pintar. Cenário por tipo × variante × noite; carros vistos
// de trás por cor × carroceria × pose do volante × freio. Tudo em cache (só render; nada disso é estado).
import { carDef } from '../core/data/cars';
import { SPRITE_HALF_WIDTH } from '../core/track/sprites';
import type { CarBody, SpriteKind } from '../core/types';
import type { CarColors } from '../game/contracts';
import { mix } from './palette';
import { ROAD_WIDTH } from './projection';

/** Largura do carro na pista, em unidades de mundo (~1,9 m numa pista de 14 m). */
export const CAR_WORLD_WIDTH = 0.27 * ROAD_WIDTH;
/** Altura do sprite do carro em relação à largura. */
const CAR_ASPECT = 0.56;
const CAR_PX_W = 64;
const CAR_PX_H = Math.round(CAR_PX_W * CAR_ASPECT);

/** Proporção (altura / largura) de cada sprite de cenário; a largura vem da colisão (SPRITE_HALF_WIDTH). */
const ASPECT: Record<SpriteKind, number> = {
  tree: 1.5, pine: 2.1, palm: 2.5, cactus: 1.8, bush: 0.55, boulder: 0.62, building: 1.6, tower: 3.8,
  lamp: 5.4, billboard: 0.62, sign_left: 1.0, sign_right: 1.0, grandstand: 0.42, banner_start: 0.42,
  pit_wall: 0.4, pit_sign: 2.0, cone: 1.3,
};

/** Largura em unidades de mundo de um sprite (a mesma meia largura que a colisão usa). */
export function spriteWorldWidth(kind: SpriteKind, scale: number): number {
  if (kind === 'banner_start') return 2.4 * ROAD_WIDTH;
  return 2 * SPRITE_HALF_WIDTH[kind] * scale * ROAD_WIDTH;
}

export function spriteAspect(kind: SpriteKind, variant: number): number {
  if (kind === 'building') return ASPECT.building + (variant % 3) * 0.35;
  return ASPECT[kind];
}

type Ctx = CanvasRenderingContext2D;

function bitmap(w: number, h: number): [HTMLCanvasElement, Ctx] {
  const c = document.createElement('canvas');
  c.width = Math.max(1, Math.round(w)); c.height = Math.max(1, Math.round(h));
  const ctx = c.getContext('2d');
  if (!ctx) throw new Error('canvas 2d indisponível');
  ctx.imageSmoothingEnabled = false;
  return [c, ctx];
}

function rect(ctx: Ctx, color: string, x: number, y: number, w: number, h: number): void {
  ctx.fillStyle = color;
  ctx.fillRect(Math.round(x), Math.round(y), Math.max(1, Math.round(w)), Math.max(1, Math.round(h)));
}

function poly(ctx: Ctx, color: string, pts: Array<[number, number]>): void {
  ctx.fillStyle = color;
  ctx.beginPath();
  ctx.moveTo(pts[0][0], pts[0][1]);
  for (let i = 1; i < pts.length; i++) ctx.lineTo(pts[i][0], pts[i][1]);
  ctx.closePath();
  ctx.fill();
}

function disc(ctx: Ctx, color: string, x: number, y: number, r: number): void {
  ctx.fillStyle = color;
  ctx.beginPath();
  ctx.arc(x, y, r, 0, Math.PI * 2);
  ctx.fill();
}

/** Tom da noite: escurece e azula (as luzes acesas são pintadas por cima, sem esse filtro). */
function tone(color: string, night: boolean): string {
  return night ? mix(color, '#101634', 0.6) : color;
}

const BUILDING_COLORS = ['#d8c3a5', '#9fb4c8', '#c98f7a', '#e0d8c8', '#8f9fb0', '#b8a0c8'];
const BILLBOARD_COLORS = ['#ff5a36', '#2f8fe0', '#ffd23f', '#3fb36a'];

/** Desenha um sprite de cenário num bitmap de largura `w` (altura pela proporção). */
function drawScenery(kind: SpriteKind, variant: number, night: boolean): HTMLCanvasElement {
  const wide = kind === 'grandstand' || kind === 'banner_start' || kind === 'building' || kind === 'billboard';
  const w = wide ? 160 : 96;
  const h = w * spriteAspect(kind, variant);
  const [c, ctx] = bitmap(w, h);
  const t = (col: string) => tone(col, night);
  switch (kind) {
    case 'tree': {
      rect(ctx, t('#6b4424'), w * 0.44, h * 0.55, w * 0.12, h * 0.45);
      disc(ctx, t('#2f8a3a'), w * 0.5, h * 0.42, w * 0.42);
      disc(ctx, t('#3aa446'), w * 0.36, h * 0.3, w * 0.26);
      disc(ctx, t('#56c060'), w * 0.6, h * 0.24, w * 0.2);
      break;
    }
    case 'pine': {
      rect(ctx, t('#5a3a1e'), w * 0.45, h * 0.82, w * 0.1, h * 0.18);
      for (let i = 0; i < 3; i++) {
        const top = h * (0.02 + i * 0.24); const base = h * (0.4 + i * 0.22); const half = w * (0.26 + i * 0.12);
        poly(ctx, t(i % 2 ? '#1f6a3a' : '#2a7d45'), [[w * 0.5, top], [w * 0.5 + half, base], [w * 0.5 - half, base]]);
      }
      break;
    }
    case 'palm': {
      for (let i = 0; i < 10; i++) rect(ctx, t(i % 2 ? '#8a6038' : '#9c6e42'), w * 0.46 + i * 0.4, h * (0.3 + i * 0.07), w * 0.1, h * 0.075);
      const cx = w * 0.5; const cy = h * 0.28;
      for (const [dx, dy] of [[-0.48, 0.12], [0.48, 0.12], [-0.34, -0.1], [0.34, -0.1], [0, -0.16]] as const) {
        poly(ctx, t('#2f9a3e'), [[cx, cy], [cx + w * dx, cy + h * dy], [cx + w * dx * 0.7, cy + h * dy + h * 0.05]]);
      }
      disc(ctx, t('#6b4a24'), cx, cy + h * 0.02, w * 0.06);
      break;
    }
    case 'cactus': {
      rect(ctx, t('#2f8a4a'), w * 0.4, h * 0.1, w * 0.2, h * 0.9);
      rect(ctx, t('#2f8a4a'), w * 0.14, h * 0.35, w * 0.14, h * 0.28);
      rect(ctx, t('#2f8a4a'), w * 0.14, h * 0.55, w * 0.3, h * 0.08);
      rect(ctx, t('#2f8a4a'), w * 0.72, h * 0.25, w * 0.14, h * 0.26);
      rect(ctx, t('#2f8a4a'), w * 0.56, h * 0.45, w * 0.3, h * 0.08);
      rect(ctx, t('#46a860'), w * 0.44, h * 0.12, w * 0.05, h * 0.86);
      break;
    }
    case 'bush': {
      disc(ctx, t('#2f8a3a'), w * 0.3, h * 0.62, h * 0.42);
      disc(ctx, t('#3aa446'), w * 0.62, h * 0.55, h * 0.48);
      disc(ctx, t('#2a7a34'), w * 0.82, h * 0.7, h * 0.3);
      break;
    }
    case 'boulder': {
      poly(ctx, t('#8a8a90'), [[w * 0.06, h], [w * 0.2, h * 0.3], [w * 0.55, h * 0.04], [w * 0.88, h * 0.35], [w * 0.96, h]]);
      poly(ctx, t('#a8a8b0'), [[w * 0.22, h * 0.32], [w * 0.55, h * 0.06], [w * 0.62, h * 0.5], [w * 0.3, h * 0.62]]);
      break;
    }
    case 'building':
    case 'tower': {
      const base = BUILDING_COLORS[variant % BUILDING_COLORS.length];
      rect(ctx, t(base), 0, h * 0.04, w, h * 0.96);
      rect(ctx, t(mix(base, '#000000', 0.25)), w * 0.85, h * 0.04, w * 0.15, h * 0.96);
      if (kind === 'tower') rect(ctx, t('#9aa0aa'), w * 0.47, 0, w * 0.06, h * 0.06);
      const cols = kind === 'tower' ? 3 : 5; const rows = Math.floor(h / (w / cols) * 0.9);
      for (let r = 0; r < rows; r++) {
        for (let k = 0; k < cols; k++) {
          const lit = night && ((r * 7 + k * 3 + variant) % 5 !== 0);
          const win = lit ? '#ffd86a' : t('#3a4a66');
          rect(ctx, win, (k + 0.25) * (w * 0.85 / cols), h * 0.08 + r * (h * 0.9 / rows), (w * 0.85 / cols) * 0.5, (h * 0.9 / rows) * 0.55);
        }
      }
      break;
    }
    case 'lamp': {
      rect(ctx, t('#7a7f88'), w * 0.44, h * 0.08, w * 0.12, h * 0.92);
      rect(ctx, t('#7a7f88'), w * 0.2, h * 0.06, w * 0.3, w * 0.1);
      rect(ctx, night ? '#fff2b0' : t('#d8d8d8'), w * 0.12, h * 0.07, w * 0.22, w * 0.12);
      if (night) disc(ctx, 'rgba(255,230,150,0.35)', w * 0.23, h * 0.1, w * 0.3);
      break;
    }
    case 'billboard': {
      const col = BILLBOARD_COLORS[variant % BILLBOARD_COLORS.length];
      rect(ctx, t('#5a5a60'), w * 0.2, h * 0.6, w * 0.05, h * 0.4);
      rect(ctx, t('#5a5a60'), w * 0.75, h * 0.6, w * 0.05, h * 0.4);
      rect(ctx, t('#f4f4f4'), 0, 0, w, h * 0.62);
      rect(ctx, night ? mix(col, '#ffffff', 0.1) : col, w * 0.04, h * 0.05, w * 0.92, h * 0.52);
      ctx.fillStyle = '#ffffff'; ctx.font = `bold ${Math.round(h * 0.26)}px sans-serif`; ctx.textAlign = 'center';
      ctx.fillText('NITRO', w * 0.5, h * 0.42);
      break;
    }
    case 'sign_left':
    case 'sign_right': {
      rect(ctx, t('#6a6a70'), w * 0.46, h * 0.5, w * 0.08, h * 0.5);
      rect(ctx, t('#ffd23f'), w * 0.08, h * 0.08, w * 0.84, h * 0.46);
      const dir = kind === 'sign_left' ? -1 : 1;
      for (let i = 0; i < 3; i++) {
        const cx = w * (0.5 + dir * (i - 1) * 0.24);
        poly(ctx, '#1a1a1a', [[cx - dir * w * 0.08, h * 0.14], [cx + dir * w * 0.08, h * 0.31], [cx - dir * w * 0.08, h * 0.48], [cx - dir * w * 0.02, h * 0.31]]);
      }
      break;
    }
    case 'grandstand': {
      rect(ctx, t('#b8bcc4'), 0, h * 0.12, w, h * 0.88);
      rect(ctx, t('#d23a2a'), 0, 0, w, h * 0.14);
      const crowd = ['#ff5a36', '#ffd23f', '#2f8fe0', '#ffffff', '#3fb36a', '#b04ac0'];
      for (let r = 0; r < 5; r++) {
        rect(ctx, t('#8a8e96'), 0, h * (0.2 + r * 0.16), w, h * 0.03);
        for (let k = 0; k < 40; k++) rect(ctx, t(crowd[(k * 7 + r * 3 + variant) % crowd.length]), k * (w / 40) + 1, h * (0.23 + r * 0.16), w / 40 - 2, h * 0.1);
      }
      break;
    }
    case 'banner_start': {
      rect(ctx, t('#d8d8e0'), 0, h * 0.1, w * 0.04, h * 0.9);
      rect(ctx, t('#d8d8e0'), w * 0.96, h * 0.1, w * 0.04, h * 0.9);
      rect(ctx, t('#1a1a1a'), 0, 0, w, h * 0.3);
      for (let k = 0; k < 24; k++) for (let r = 0; r < 3; r++) if ((k + r) % 2 === 0) rect(ctx, t('#ffffff'), k * (w / 24), r * (h * 0.1), w / 24, h * 0.1);
      break;
    }
    case 'pit_wall': {
      rect(ctx, t('#c8ccd4'), 0, h * 0.2, w, h * 0.8);
      for (let k = 0; k < 6; k++) rect(ctx, t(k % 2 ? '#d23a2a' : '#ffffff'), k * (w / 6), 0, w / 6, h * 0.22);
      break;
    }
    case 'pit_sign': {
      rect(ctx, t('#6a6a70'), w * 0.44, h * 0.35, w * 0.12, h * 0.65);
      rect(ctx, t('#2f6fe0'), 0, 0, w, h * 0.38);
      ctx.fillStyle = '#ffffff'; ctx.font = `bold ${Math.round(w * 0.36)}px sans-serif`; ctx.textAlign = 'center';
      ctx.fillText('BOX', w * 0.5, h * 0.28);
      break;
    }
    case 'cone': {
      poly(ctx, t('#ff6a1a'), [[w * 0.5, 0], [w * 0.85, h * 0.88], [w * 0.15, h * 0.88]]);
      rect(ctx, t('#ffffff'), w * 0.3, h * 0.45, w * 0.4, h * 0.12);
      rect(ctx, t('#ff6a1a'), 0, h * 0.86, w, h * 0.14);
      break;
    }
  }
  return c;
}

/** Como cada carroceria aparece vista de trás (proporções num bitmap de 64 × 36). */
interface RearShape { bodyTop: number; cabinW: number; cabinH: number; spoiler: boolean; wide: number; open?: boolean; bed?: boolean }

const REAR: Record<CarBody, RearShape> = {
  gt: { bodyTop: 0.42, cabinW: 0.62, cabinH: 0.26, spoiler: true, wide: 1 },
  muscle: { bodyTop: 0.4, cabinW: 0.6, cabinH: 0.28, spoiler: false, wide: 1.04 },
  hatch: { bodyTop: 0.38, cabinW: 0.7, cabinH: 0.34, spoiler: true, wide: 0.94 },
  sedan: { bodyTop: 0.4, cabinW: 0.68, cabinH: 0.3, spoiler: false, wide: 0.96 },
  electric: { bodyTop: 0.44, cabinW: 0.66, cabinH: 0.26, spoiler: false, wide: 0.98 },
  rally: { bodyTop: 0.38, cabinW: 0.66, cabinH: 0.32, spoiler: true, wide: 1.04 },
  hyper: { bodyTop: 0.5, cabinW: 0.5, cabinH: 0.2, spoiler: true, wide: 1.06 },
  classic: { bodyTop: 0.42, cabinW: 0.6, cabinH: 0.3, spoiler: false, wide: 0.96 },
  wedge: { bodyTop: 0.46, cabinW: 0.56, cabinH: 0.22, spoiler: true, wide: 1 },
  pickup: { bodyTop: 0.34, cabinW: 0.66, cabinH: 0.32, spoiler: false, wide: 1.04, bed: true },
  prototype: { bodyTop: 0.52, cabinW: 0.34, cabinH: 0.24, spoiler: true, wide: 1.06 },
  micro: { bodyTop: 0.34, cabinW: 0.72, cabinH: 0.4, spoiler: false, wide: 0.86 },
  roadster: { bodyTop: 0.46, cabinW: 0.5, cabinH: 0.16, spoiler: false, wide: 0.96, open: true },
};

/** Traseira de um carro em pixel art. `pose` −1/0/1 inclina (vira), `braking` acende as lanternas. */
function drawCar(color: string, accent: string, body: CarBody, pose: number, braking: boolean, night: boolean): HTMLCanvasElement {
  const [c, ctx] = bitmap(CAR_PX_W, CAR_PX_H);
  const s = REAR[body];
  const w = CAR_PX_W; const h = CAR_PX_H;
  const bodyW = w * 0.9 * s.wide; const x0 = (w - bodyW) / 2;
  const lean = pose * w * 0.05;                     // o teto desliza para o lado da curva
  const shade = (col: string, k: number) => mix(col, '#000000', k);
  const paint = night ? mix(color, '#1a2040', 0.35) : color;
  // Sombra e pneus
  rect(ctx, 'rgba(0,0,0,0.35)', x0 - 2, h * 0.9, bodyW + 4, h * 0.1);
  rect(ctx, '#141414', x0 + 1, h * 0.66, bodyW * 0.2, h * 0.3);
  rect(ctx, '#141414', x0 + bodyW * 0.8 - 1, h * 0.66, bodyW * 0.2, h * 0.3);
  // Carroceria (lado da curva à mostra: uma faixa mais escura)
  rect(ctx, paint, x0, h * s.bodyTop, bodyW, h * (0.86 - s.bodyTop));
  if (pose !== 0) rect(ctx, shade(paint, 0.35), pose > 0 ? x0 : x0 + bodyW * 0.9, h * s.bodyTop, bodyW * 0.1, h * (0.86 - s.bodyTop));
  rect(ctx, shade(paint, 0.25), x0, h * 0.74, bodyW, h * 0.06);   // para-choque
  rect(ctx, accent, x0 + bodyW * 0.44, h * s.bodyTop, bodyW * 0.12, h * (0.74 - s.bodyTop)); // faixa
  // Cabine / caçamba / conversível
  const cw = bodyW * s.cabinW; const cx = (w - cw) / 2 + lean;
  const cTop = h * (s.bodyTop - s.cabinH);
  if (s.open) {
    rect(ctx, '#2a2a30', cx, h * s.bodyTop - 2, cw, 3);
    disc(ctx, '#f0c8a0', w / 2 + lean - cw * 0.18, h * s.bodyTop - 5, 3);
    rect(ctx, accent, w / 2 + lean - cw * 0.18 - 3, h * s.bodyTop - 9, 6, 3);   // capacete
  } else {
    rect(ctx, shade(paint, 0.1), cx, cTop, cw, h * s.cabinH);
    rect(ctx, night ? '#1a2446' : '#27324e', cx + 3, cTop + 2, cw - 6, h * s.cabinH - 3);  // vidro traseiro
    rect(ctx, 'rgba(255,255,255,0.25)', cx + 4, cTop + 3, cw * 0.3, 2);
  }
  if (s.bed) rect(ctx, shade(paint, 0.4), x0 + 3, h * s.bodyTop, bodyW - 6, h * 0.08);
  if (s.spoiler) {
    rect(ctx, accent, x0 + 2, cTop - (body === 'hyper' || body === 'prototype' ? 3 : 1), bodyW - 4, 3);
    rect(ctx, shade(accent, 0.3), x0 + bodyW * 0.2, cTop + 1, 2, h * s.cabinH * 0.6);
    rect(ctx, shade(accent, 0.3), x0 + bodyW * 0.8 - 2, cTop + 1, 2, h * s.cabinH * 0.6);
  }
  // Lanternas (freio mais forte), placa e escapamento
  const tail = braking ? '#ff3a3a' : night ? '#d02020' : '#b01818';
  rect(ctx, tail, x0 + 2, h * (s.bodyTop + 0.06), bodyW * 0.22, h * 0.1);
  rect(ctx, tail, x0 + bodyW * 0.78 - 2, h * (s.bodyTop + 0.06), bodyW * 0.22, h * 0.1);
  if (braking || night) {
    ctx.fillStyle = braking ? 'rgba(255,60,60,0.45)' : 'rgba(255,40,40,0.25)';
    ctx.fillRect(x0 - 2, h * (s.bodyTop + 0.02), bodyW * 0.3, h * 0.18);
    ctx.fillRect(x0 + bodyW * 0.72, h * (s.bodyTop + 0.02), bodyW * 0.3, h * 0.18);
  }
  rect(ctx, '#f0f0e0', w / 2 - 5, h * 0.6, 10, 4);
  rect(ctx, '#3a3a3a', x0 + bodyW * 0.18, h * 0.8, 4, 2);
  return c;
}

/** Cache de bitmaps. As chaves são strings (Map aqui é só render; o núcleo não usa). */
export class SpriteAtlas {
  private readonly cache = new Map<string, HTMLCanvasElement>();

  scenery(kind: SpriteKind, variant: number, night: boolean): HTMLCanvasElement {
    const v = kind === 'building' || kind === 'tower' || kind === 'billboard' || kind === 'grandstand' ? variant % 6 : 0;
    const key = `s:${kind}:${v}:${night ? 1 : 0}`;
    let b = this.cache.get(key);
    if (!b) { b = drawScenery(kind, v, night); this.cache.set(key, b); }
    return b;
  }

  /** `paint` = a pintura escolhida pelo jogador (RenderFrame.paints); ausente/null = a de fábrica. */
  car(carId: string, pose: number, braking: boolean, night: boolean, paint?: CarColors | null): HTMLCanvasElement {
    const key = `c:${carId}:${pose}:${braking ? 1 : 0}:${night ? 1 : 0}${paint ? `:${paint.color}:${paint.accent}` : ''}`;
    let b = this.cache.get(key);
    if (!b) {
      const def = carDef(carId);
      b = paint
        ? drawCar(paint.color, paint.accent, def.body, pose, braking, night)
        : drawCar(def.color, def.accent ?? mix(def.color, '#ffffff', 0.55), def.body, pose, braking, night);
      this.cache.set(key, b);
    }
    return b;
  }

  /** Proporção altura/largura do bitmap de carro. */
  static readonly carAspect = CAR_ASPECT;

  clear(): void { this.cache.clear(); }
}
