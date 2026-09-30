// Paleta do modo Retrô: cores chapadas, saturadas, no espírito dos 16 bits. Uma base diurna por cenário e
// um filtro por período (entardecer esquenta, noite azula e apaga), como na paleta do 3D — mas separada
// dela, para o Retrô não mudar quando o visual moderno mudar. Funções puras.
import type { SceneryId, TimeOfDay } from '../core/types';

export interface RetroPalette {
  /** Céu: topo e horizonte. */
  skyTop: string;
  skyBottom: string;
  /** Sol ou lua (null = nenhum). */
  sun: string | null;
  moon: string | null;
  stars: boolean;
  /** Cor para onde tudo tende ao longe. */
  fog: string;
  /** Faixas alternadas (Segment.band) de chão, zebra e asfalto: [claro, escuro]. */
  grass: [string, string];
  rumble: [string, string];
  road: [string, string];
  /** Faixa central (só nas bandas claras). */
  lane: string;
  /** Faixa do box. */
  pit: string;
  /** Silhuetas do fundo: montanhas longe e morros perto. */
  far: string;
  near: string;
  /** Noite: janelas, postes e faróis acesos. */
  lights: boolean;
}

interface Base {
  skyTop: string; skyBottom: string; grass: [string, string]; far: string; near: string;
  rumble?: [string, string]; road?: [string, string];
}

const BASE: Record<SceneryId, Base> = {
  tropical: { skyTop: '#2f7fe0', skyBottom: '#9fd8ff', grass: ['#3fb34a', '#339a3e'], far: '#5b8fd0', near: '#2f8a4a' },
  desert: { skyTop: '#3a86d8', skyBottom: '#ffd9a0', grass: ['#e9c07a', '#d9ab63'], far: '#c98a5a', near: '#b0673f', rumble: ['#ffffff', '#d23a2a'] },
  city_night: { skyTop: '#27367a', skyBottom: '#8c7bd0', grass: ['#5a6070', '#4d5261'], far: '#46508a', near: '#343a5c', road: ['#5c5e66', '#55575f'] },
  alpine: { skyTop: '#3c86e6', skyBottom: '#d6ecff', grass: ['#e8f0f8', '#d3dfeb'], far: '#8fa8c8', near: '#5c7fa0', rumble: ['#ffffff', '#c8202a'] },
  coast: { skyTop: '#1f8fe8', skyBottom: '#b8ecff', grass: ['#f0dca0', '#e2ca88'], far: '#6fb6e8', near: '#3a9fd0' },
  savanna: { skyTop: '#e08a3a', skyBottom: '#ffe0a0', grass: ['#c9b24a', '#b79f3c'], far: '#a86a3a', near: '#7c5a2c' },
};

function hex(c: string): [number, number, number] {
  const v = parseInt(c.slice(1), 16);
  return [(v >> 16) & 255, (v >> 8) & 255, v & 255];
}

function toHex([r, g, b]: [number, number, number]): string {
  const h = (n: number) => Math.max(0, Math.min(255, Math.round(n))).toString(16).padStart(2, '0');
  return `#${h(r)}${h(g)}${h(b)}`;
}

/** Mistura `a` → `b` na proporção t (0 = a, 1 = b). */
export function mix(a: string, b: string, t: number): string {
  const x = hex(a); const y = hex(b);
  return toHex([x[0] + (y[0] - x[0]) * t, x[1] + (y[1] - x[1]) * t, x[2] + (y[2] - x[2]) * t]);
}

/** Paleta do Retrô para um cenário e período. */
export function retroPalette(scenery: SceneryId, time: TimeOfDay): RetroPalette {
  const b = BASE[scenery];
  const rumble = b.rumble ?? ['#ffffff', '#e0262b'];
  const road = b.road ?? ['#6e6f75', '#66676d'];
  const day: RetroPalette = {
    skyTop: b.skyTop, skyBottom: b.skyBottom, sun: '#fff6c8', moon: null, stars: false, fog: b.skyBottom,
    grass: b.grass, rumble, road, lane: '#f4f4f4', pit: '#8a8c94', far: b.far, near: b.near,
    lights: scenery === 'city_night',
  };
  if (scenery === 'city_night') return { ...day, sun: null, moon: '#fff8e0', stars: true, lights: true, fog: mix(b.skyBottom, '#1a1f40', 0.4) };
  if (time === 'day') return day;
  if (time === 'dusk') {
    const warm = (c: string, t: number) => mix(c, '#ff7a3a', t);
    return {
      ...day,
      skyTop: mix(b.skyTop, '#5a2f8a', 0.7), skyBottom: '#ffb070', sun: '#ffd07a', fog: '#f0a070',
      grass: [warm(b.grass[0], 0.25), warm(b.grass[1], 0.3)], road: [mix(road[0], '#5a4a58', 0.4), mix(road[1], '#524252', 0.4)],
      far: mix(b.far, '#8a4a7a', 0.55), near: mix(b.near, '#4a2a4a', 0.5),
    };
  }
  const dark = (c: string, t: number) => mix(c, '#0c1030', t);
  return {
    ...day,
    skyTop: '#060a24', skyBottom: '#1e2a5c', sun: null, moon: '#f4f0d8', stars: true, fog: '#1a2250', lights: true,
    grass: [dark(b.grass[0], 0.65), dark(b.grass[1], 0.68)], rumble: [dark(rumble[0], 0.35), dark(rumble[1], 0.4)],
    road: [dark(road[0], 0.55), dark(road[1], 0.58)], lane: '#c8c8c8', far: dark(b.far, 0.7), near: dark(b.near, 0.75),
  };
}
