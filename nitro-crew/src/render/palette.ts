// Paleta por cenário × hora do dia. Tudo derivado de uma base diurna por cenário e de um
// "filtro" por hora (entardecer esquenta e escurece; noite azula e apaga), para as 18
// combinações existirem sem 18 tabelas escritas à mão. Funções puras; sem DOM.
import type { SceneryId, TimeOfDay } from '../core/types';

export interface Palette {
  /** Gradiente do céu, de cima para o horizonte. */
  sky: [string, string, string];
  /** Cor do sol (dia/entardecer) ou null. */
  sun: string | null;
  /** Cor da lua (noite) ou null. */
  moon: string | null;
  stars: boolean;
  grassLight: string;
  grassDark: string;
  rumbleLight: string;
  rumbleDark: string;
  roadLight: string;
  roadDark: string;
  /** Faixa central da pista. */
  lane: string;
  fog: string;
  /** Camada de parallax distante (montanhas/skyline) e próxima (colinas/prédios). */
  far: string;
  near: string;
  /** Asfalto do box e a linha que o separa da pista. */
  pit: string;
  pitLine: string;
  /** Luz ambiente 0..1 (1 = dia), para os sprites decidirem se acendem janelas e postes. */
  light: number;
}

export const SCENERIES: readonly SceneryId[] = ['tropical', 'desert', 'city_night', 'alpine', 'coast', 'savanna'];
export const TIMES_OF_DAY: readonly TimeOfDay[] = ['day', 'dusk', 'night'];

// ───────────────────────────── Utilitários de cor ─────────────────────────────

export function hexToRgb(hex: string): [number, number, number] {
  const h = hex.replace('#', '');
  const full = h.length === 3 ? h.split('').map((c) => c + c).join('') : h;
  const n = parseInt(full, 16);
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
}

export function rgbToHex(r: number, g: number, b: number): string {
  const c = (v: number) => Math.max(0, Math.min(255, Math.round(v))).toString(16).padStart(2, '0');
  return `#${c(r)}${c(g)}${c(b)}`;
}

/** Mistura `a` com `b` na proporção `t` (0 = só a, 1 = só b). */
export function mix(a: string, b: string, t: number): string {
  const [ar, ag, ab] = hexToRgb(a);
  const [br, bg, bb] = hexToRgb(b);
  return rgbToHex(ar + (br - ar) * t, ag + (bg - ag) * t, ab + (bb - ab) * t);
}

/** Clareia (f > 1) ou escurece (f < 1). */
export function shade(hex: string, f: number): string {
  const [r, g, b] = hexToRgb(hex);
  return rgbToHex(r * f, g * f, b * f);
}

// ───────────────────────────── Bases diurnas ─────────────────────────────

interface Base {
  sky: [string, string, string];
  grass: string;
  road: string;
  far: string;
  near: string;
}

const BASES: Record<SceneryId, Base> = {
  tropical: { sky: ['#1b6fd6', '#4fa6ef', '#bfe6ff'], grass: '#2e9e3a', road: '#6b6b6b', far: '#3a7a5a', near: '#2f6d3e' },
  desert: { sky: ['#2a7fd8', '#7fc0f0', '#f7e2b0'], grass: '#d9a85a', road: '#7a7168', far: '#b06a48', near: '#c98a52' },
  city_night: { sky: ['#4d6fb0', '#8fa9d8', '#d8dde8'], grass: '#4c7a3a', road: '#5f5f66', far: '#5e6f8f', near: '#48546c' },
  alpine: { sky: ['#2c62c4', '#6ea2e6', '#d6ecff'], grass: '#3f9b46', road: '#6c6f72', far: '#8c98b0', near: '#3c7a4b' },
  coast: { sky: ['#2277dd', '#5cb6f2', '#cfefff'], grass: '#4faa4c', road: '#707070', far: '#2a7fb8', near: '#3d8a5b' },
  savanna: { sky: ['#3a86d8', '#8ac4ee', '#f2e6c0'], grass: '#9fb33f', road: '#6e6a62', far: '#a08a56', near: '#7c8f3c' },
};

/** Aplica a hora do dia a uma base diurna. */
function build(scenery: SceneryId, time: TimeOfDay): Palette {
  const b = BASES[scenery];
  let sky: [string, string, string];
  let light: number;
  let sun: string | null = '#fff3a0';
  let moon: string | null = null;
  let stars = false;
  let tint = '#000000';
  let tintAmount = 0;
  if (time === 'day') {
    sky = b.sky; light = 1;
  } else if (time === 'dusk') {
    sky = ['#3b2a6e', '#c85a5a', '#f6b26b']; light = 0.7; sun = '#ffb347';
    tint = '#5a2e4a'; tintAmount = 0.3;
  } else {
    sky = ['#03061a', '#0b1440', '#25336b']; light = 0.32; sun = null; moon = '#f4f1d8'; stars = true;
    tint = '#0a1030'; tintAmount = 0.62;
  }
  const t = (c: string) => mix(c, tint, tintAmount);
  const grass = t(b.grass);
  const road = t(b.road);
  return {
    sky, sun, moon, stars,
    grassLight: shade(grass, 1.08), grassDark: shade(grass, 0.82),
    rumbleLight: t('#f4f4f4'), rumbleDark: t('#d63a3a'),
    roadLight: shade(road, 1.05), roadDark: shade(road, 0.9),
    lane: time === 'night' ? '#e9e9c0' : t('#f0f0f0'),
    fog: sky[2],
    far: t(b.far), near: t(b.near),
    pit: shade(road, 0.8), pitLine: t('#f2e04a'),
    light,
  };
}

const cache = new Map<string, Palette>();

/** Paleta da combinação (com cache: é a mesma para a corrida inteira). */
export function palette(scenery: SceneryId, time: TimeOfDay): Palette {
  const key = `${scenery}:${time}`;
  let p = cache.get(key);
  if (!p) { p = build(scenery, time); cache.set(key, p); }
  return p;
}
