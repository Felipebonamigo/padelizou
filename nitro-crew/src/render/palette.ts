// Paleta por cenário × hora do dia: o que o chão, o céu e a luz de cada combinação usam.
// Céus do entardecer e da noite são escritos por bioma (o laranja do cânion não é o rosa da
// Lapônia); o resto sai de uma base diurna por bioma e de uma "gradação" por hora (entardecer
// esquenta e puxa para o roxo; noite azula e escurece sem virar preto). Referência de arte:
// Horizon Chase Turbo — cor saturada, céu em gradiente forte, sombra colorida, névoa com cor.
// Funções puras; sem DOM. Calibrado para o tone mapping ACES do renderer.ts.
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

  // ── Céu e luz ──
  /** Brilho do céu em volta do sol/lua (o horizonte do lado do sol puxa para ela). */
  skyGlow: string;
  /** Luz direcional (sol ou lua). */
  sunLight: string;
  sunIntensity: number;
  /** Hemisférica: o céu dá a cor das sombras (azulada/arroxeada), o chão o rebatimento. */
  hemiSky: string;
  hemiGround: string;
  hemiIntensity: number;
  /** Força do reflexo do céu (env map) nos materiais. */
  envIntensity: number;
  /** Exposição do tone mapping. */
  exposure: number;
  /** Multiplicador da densidade da névoa (1 = dia). */
  fogDensity: number;
  /** Bloom da qualidade alta: força, raio, limiar (luminância linear antes do tone mapping). */
  bloom: [number, number, number];
  /** Nuvens: lado iluminado e lado da sombra (null = sem nuvens). */
  cloudLit: string | null;
  cloudShade: string;

  // ── Chão ──
  /** Asfalto (cor base da textura). */
  asphalt: string;
  /** Acostamento (cascalho/terra/areia) e a faixa de transição até o terreno. */
  shoulder: string;
  verge: string;
  /** Terreno por altura e inclinação. */
  groundLow: string;
  groundHigh: string;
  rock: string;
  snow: string;
  /** Areia da praia (litoral) e das dunas. */
  sand: string;
  /** Montanhas do horizonte em três planos, de perto para longe (cor do topo; a base some na névoa). */
  layers: [string, string, string];
  /** Quanto cada plano já está dentro da névoa (0 = cor pura, 1 = cor da névoa). */
  layerHaze: [number, number, number];
  /** Água: fundo, raso e espuma. */
  waterDeep: string;
  waterShallow: string;
  foam: string;
  /** Poça de luz dos postes no asfalto (noite) ou null. */
  lampPool: string | null;
  /** Brilho das marcações da pista (tinta refletiva) à noite: 0 de dia. */
  markingGlow: number;
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

/** Luminância relativa (0..1, sRGB aproximado) — para os testes de contraste. */
export function luminance(hex: string): number {
  const [r, g, b] = hexToRgb(hex);
  return (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255;
}

// ───────────────────────────── Bases diurnas ─────────────────────────────

interface Base {
  sky: [string, string, string];
  asphalt: string;
  shoulder: string;
  verge: string;
  groundLow: string;
  groundHigh: string;
  rock: string;
  sand: string;
  layers: [string, string, string];
  waterDeep: string;
  waterShallow: string;
}

const BASES: Record<SceneryId, Base> = {
  // Mata atlântica: verde fundo, terra vermelha na beira, serra azulada ao longe.
  tropical: {
    sky: ['#1257d2', '#3b9bf2', '#aee8ff'], asphalt: '#55585e', shoulder: '#7a6654', verge: '#c8784a',
    groundLow: '#34a844', groundHigh: '#1d7a44', rock: '#7c6a52', sand: '#e8d49a',
    layers: ['#23814f', '#3c8f82', '#76a9c2'], waterDeep: '#0b6aa8', waterShallow: '#2fd0c4',
  },
  // Deserto: areia dourada, rocha vermelha nas encostas, mesas no horizonte.
  desert: {
    sky: ['#1c63cf', '#5eabf0', '#ffd89a'], asphalt: '#625b55', shoulder: '#a08a6c', verge: '#eec486',
    groundLow: '#e9b464', groundHigh: '#d9964f', rock: '#bb5a36', sand: '#f0c77e',
    layers: ['#c56a3a', '#d08c62', '#dcae96'], waterDeep: '#1a6aa0', waterShallow: '#3ec0c0',
  },
  // Cidade: calçada clara, grama de praça, prédios no fundo (a skyline vem do terrain.ts).
  city_night: {
    sky: ['#3468c4', '#80ade4', '#cddcf0'], asphalt: '#4a4d55', shoulder: '#7a7a7c', verge: '#bdbbb4',
    groundLow: '#4e8f40', groundHigh: '#3e7a3a', rock: '#6c6c74', sand: '#c8c0a8',
    layers: ['#3e4c70', '#5a6892', '#8a94ba'], waterDeep: '#123a6a', waterShallow: '#2a7a9a',
  },
  // Montanha: prado verde, pedra azulada, neve no alto e nos picos do horizonte.
  alpine: {
    sky: ['#1a53c9', '#5799ec', '#c4e4ff'], asphalt: '#565a61', shoulder: '#827c72', verge: '#a0968a',
    groundLow: '#4aab4c', groundHigh: '#2b7646', rock: '#868b98', sand: '#c8c0a8',
    layers: ['#2c6a4c', '#4a6aa0', '#7a8ccc'], waterDeep: '#1a5a8a', waterShallow: '#3aa0b0',
  },
  // Litoral: mar turquesa, praia clara, morros verdes e ilhas ao longe.
  coast: {
    sky: ['#0e61da', '#43b0f5', '#a6e6ff'], asphalt: '#5a5c61', shoulder: '#b0a080', verge: '#ecd49c',
    groundLow: '#4aa848', groundHigh: '#2f8a4a', rock: '#9a8a72', sand: '#f3dca0',
    layers: ['#2a8a5a', '#3a8ab0', '#6aa8dc'], waterDeep: '#0660b8', waterShallow: '#22d4cf',
  },
  // Savana/planície: capim dourado-esverdeado, terra avermelhada, chapadas baixas.
  savanna: {
    sky: ['#2876d2', '#7ebff0', '#fae2a8'], asphalt: '#5e5a54', shoulder: '#957a58', verge: '#c89456',
    groundLow: '#b5b547', groundHigh: '#86993b', rock: '#a07650', sand: '#e0c88a',
    layers: ['#768a3c', '#9a8c62', '#bcae94'], waterDeep: '#2a6a7a', waterShallow: '#5aa8a0',
  },
};

/** Céus do entardecer por bioma (topo, meio, horizonte) e o brilho em volta do sol. */
const DUSK_SKY: Record<SceneryId, { sky: [string, string, string]; glow: string }> = {
  tropical: { sky: ['#272f7c', '#cf5a7c', '#ffb96a'], glow: '#ffd27a' },
  desert: { sky: ['#2b1e5e', '#d1526a', '#ffa64e'], glow: '#ffc460' },
  city_night: { sky: ['#252a6c', '#b24f8c', '#ffa872'], glow: '#ffc88a' },
  alpine: { sky: ['#333c90', '#d479aa', '#ffd0aa'], glow: '#ffe0b0' },
  coast: { sky: ['#1d2a74', '#d4568a', '#ffbb70'], glow: '#ffd07a' },
  savanna: { sky: ['#33296e', '#df673e', '#ffca60'], glow: '#ffdc7a' },
};

/** Céus da noite: a cidade tem o horizonte roxo-alaranjado da poluição luminosa; o resto, luar. */
function nightSky(b: SceneryId): [string, string, string] {
  if (b === 'city_night') return ['#05071c', '#161c4c', '#5c3a74'];
  if (b === 'coast') return ['#03071a', '#0b1c50', '#26508e'];
  if (b === 'desert') return ['#050722', '#161a52', '#3a3a7c'];
  return ['#04081c', '#0e1a4a', '#2c3e7e'];
}

// ───────────────────────────── Gradação por hora ─────────────────────────────

interface Grade { tint: string; amount: number; bright: number }

const GRADE: Record<TimeOfDay, Grade> = {
  day: { tint: '#000000', amount: 0, bright: 1 },
  // Entardecer: o sol laranja já esquenta o que ilumina; o albedo só puxa um pouco para o vinho.
  dusk: { tint: '#6a2f58', amount: 0.14, bright: 0.96 },
  // Noite: azula sem apagar — quem escurece a cena é a luz, não a cor (senão vira borrão preto).
  night: { tint: '#16204a', amount: 0.34, bright: 0.86 },
};

function grade(c: string, time: TimeOfDay): string {
  const g = GRADE[time];
  return shade(mix(c, g.tint, g.amount), g.bright);
}

/** Aplica a hora do dia a uma base diurna. */
function build(scenery: SceneryId, time: TimeOfDay): Palette {
  const b = BASES[scenery];
  const g = (c: string) => grade(c, time);
  let sky: [string, string, string];
  let glow: string;
  let light: number;
  let sun: string | null = null;
  let moon: string | null = null;
  let sunLight: string; let sunIntensity: number;
  let hemiSky: string; let hemiIntensity: number;
  let envIntensity: number; let exposure: number; let fogDensity: number;
  let bloom: [number, number, number];
  let cloudLit: string | null; let cloudShade: string;
  if (time === 'day') {
    sky = b.sky; glow = '#fff6d0'; light = 1; sun = '#fff1b0';
    sunLight = '#fff0d6'; sunIntensity = 3.3;
    // O céu azul saturado é a luz das sombras: é ele que as deixa azuladas em vez de pretas.
    hemiSky = mix(b.sky[1], '#b4c8f4', 0.6); hemiIntensity = 1.3;
    envIntensity = 0.45; exposure = 0.98; fogDensity = 1;
    bloom = [0.2, 0.35, 2.6];
    cloudLit = '#ffffff'; cloudShade = mix(b.sky[1], '#c8d8f0', 0.55);
  } else if (time === 'dusk') {
    sky = DUSK_SKY[scenery].sky; glow = DUSK_SKY[scenery].glow; light = 0.7; sun = '#ffc46a';
    sunLight = '#ffab62'; sunIntensity = 3.0;
    hemiSky = '#8a6ad2'; hemiIntensity = 1.45; // sombras arroxeadas
    envIntensity = 0.35; exposure = 1.0; fogDensity = 0.9;
    bloom = [0.3, 0.4, 1.6];
    cloudLit = mix(glow, '#ffa07a', 0.3); cloudShade = mix(sky[1], sky[0], 0.3);
  } else {
    sky = nightSky(scenery); glow = scenery === 'city_night' ? '#ff9a6a' : '#b8c8ff'; light = 0.32; moon = '#f6f2dc';
    sunLight = '#9db6ff'; sunIntensity = 0.95;
    hemiSky = scenery === 'city_night' ? '#5a5aa8' : '#4a66b8'; hemiIntensity = 1.35;
    envIntensity = 0.8; exposure = 1.12; fogDensity = 1.3;
    bloom = [0.5, 0.45, 1.0];
    cloudLit = null; cloudShade = sky[1];
  }
  // Névoa: o horizonte com um pouco do meio do céu — tem cor, nunca cinza.
  const fog = mix(sky[2], sky[1], time === 'day' ? 0.28 : time === 'dusk' ? 0.32 : 0.2);
  const groundLow = g(b.groundLow);
  const groundHigh = g(b.groundHigh);
  // À noite o asfalto escurece mais que a beira: a borda da pista continua lendo sob o luar.
  const asphalt = time === 'night' ? shade(g(b.asphalt), 0.82) : g(b.asphalt);
  // Os planos do horizonte puxam para o céu do período (o rosa do entardecer, o azul da noite).
  const skyPull = time === 'day' ? 0 : time === 'dusk' ? 0.28 : 0.5;
  const layers = b.layers.map((c, i) => mix(g(c), mix(sky[1], sky[2], 0.5), skyPull * (0.6 + 0.2 * i))) as [string, string, string];
  const layerHaze: [number, number, number] = time === 'night' ? [0.14, 0.3, 0.48] : [0.14, 0.32, 0.52];
  const pitLine = time === 'night' ? '#ffe45a' : '#ffd83a';
  const lane = time === 'night' ? '#f2f0dc' : '#f4f4f0';
  return {
    sky, sun, moon, stars: time === 'night',
    grassLight: groundLow, grassDark: groundHigh,
    rumbleLight: time === 'night' ? '#e8e8f0' : '#f6f6f6', rumbleDark: time === 'night' ? '#d8343c' : '#e0262e',
    roadLight: shade(asphalt, 1.06), roadDark: shade(asphalt, 0.9),
    lane, fog,
    far: layers[2], near: layers[0],
    pit: shade(asphalt, 1.22), pitLine,
    light,
    skyGlow: glow, sunLight, sunIntensity, hemiSky, hemiGround: shade(mix(groundLow, asphalt, 0.3), 0.55), hemiIntensity,
    envIntensity, exposure, fogDensity, bloom, cloudLit, cloudShade,
    asphalt, shoulder: g(b.shoulder), verge: g(b.verge), groundLow, groundHigh, rock: g(b.rock),
    snow: time === 'day' ? '#f4f8ff' : time === 'dusk' ? '#ffe0e4' : '#aebce6',
    sand: g(b.sand),
    layers, layerHaze,
    waterDeep: time === 'day' ? b.waterDeep : time === 'dusk' ? mix(b.waterDeep, '#3a2a7a', 0.45) : mix(b.waterDeep, '#040c30', 0.7),
    waterShallow: time === 'day' ? b.waterShallow : time === 'dusk' ? mix(b.waterShallow, '#d07a8a', 0.4) : mix(b.waterShallow, '#0c2a5a', 0.65),
    foam: time === 'night' ? '#9aaee0' : time === 'dusk' ? '#ffe6d8' : '#ffffff',
    lampPool: time === 'night' ? (scenery === 'city_night' ? '#ffc890' : '#ffd8a0') : null,
    markingGlow: time === 'night' ? 0.22 : time === 'dusk' ? 0.05 : 0,
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
