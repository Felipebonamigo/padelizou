// Rodas: pneu com ombro e flanco (torno de `TIRE_SIDES` = 18 lados; eram 12 até a onda I), aro com
// borda, poço escuro e o desenho de cada estilo (raios, calota, prato de rali, trava central, raios de
// arame, roda de aço). Montadas em escala unitária (raio 1, largura 1, face de fora em +x) — a instância
// escala para o raio e a largura do carro. `symmetry` é o ângulo que repete o desenho: o giro na tela é
// limitado a uma fração dele por quadro, para os raios não "andarem para trás" (efeito estroboscópico) a
// 300 km/h.
import type * as THREE from 'three';
import { AXIS_X, beam, box, cylinder, lathe, MeshBuilder, type Brush, type P2, type P3 } from './kit';
import { ACCENT, ALU, CHROME, GUNMETAL, TIRE, TIRE_SIDE, TRIM, UNDER, WHITE, WHITEWALL } from './paints';
import { withBrush } from './kit';
import { CREASE } from '../normals';

export type WheelDesign = 'sport5' | 'mag' | 'multi' | 'hubcap' | 'aero' | 'dish' | 'center' | 'wire' | 'steel';
export const WHEEL_DESIGNS: readonly WheelDesign[] = ['sport5', 'mag', 'multi', 'hubcap', 'aero', 'dish', 'center', 'wire', 'steel'];

export interface WheelModel { design: WheelDesign; geometry: THREE.BufferGeometry; symmetry: number; triangles: number }

/** Lados do pneu e do aro (torno). 18: o contorno lê redondo de perto (eram 12, um dodecágono na tela). */
export const TIRE_SIDES = 18;
const SEGS = TIRE_SIDES;
const X = (o: number) => ({ ...AXIS_X, o: [o, 0, 0] as const });

/** Pneu + aro + poço: `rim` = raio do aro (fração do pneu), `lip` = pincel da borda, `dish` = fundo. */
function tireAndRim(b: MeshBuilder, rim: number, lip: Brush, dish: Brush, whitewall = false, dishT = 0.18): void {
  // Lado de dentro simplificado (quase nunca aparece): do eixo direto ao flanco.
  const p: P2[] = [
    [0, -0.42], [0.84, -0.5], [1, -0.3], [1, 0.28], [0.96, 0.44], [0.86, 0.5],
  ];
  const brushes: Brush[] = [UNDER, TIRE_SIDE, TIRE, TIRE, TIRE_SIDE];
  if (whitewall) {
    p.push([0.8, 0.5], [0.68, 0.5]);
    brushes.push(TIRE_SIDE, WHITEWALL);
  }
  p.push([rim, 0.5], [rim * 0.92, 0.47], [rim * 0.88, dishT], [0, dishT]);
  brushes.push(TIRE_SIDE, lip, lip, dish);
  lathe(b, X(0), p, SEGS, (i) => brushes[i]);
}

/** Raios retos do cubo (r0) até o aro (r1), com espessura em x de t0 a t1. */
function spokes(b: MeshBuilder, n: number, r0: number, r1: number, width: number, t0: number, t1: number, br: Brush, phase = 0): void {
  for (let k = 0; k < n; k++) {
    const th = phase + (k / n) * Math.PI * 2;
    const c = Math.cos(th); const s = Math.sin(th);
    const tm = (t0 + t1) / 2;
    // Só as faces que aparecem: as pontas (no cubo e no aro) e a de dentro (−x) ficam escondidas.
    beam(b, [tm, r0 * c, r0 * s], [tm, r1 * c, r1 * s], width, t1 - t0, br, [1, 0, 0], { nu: null, pu: null, nv: null });
  }
}

/** Detalhe chapado na face do aro (só a face de fora, +x): furos, entradas, porcas. */
function decal(b: MeshBuilder, c: P3, size: P3, br: Brush): void {
  box(b, c, size, br, { nu: null, pu: null, ns: null, nv: null, pv: null });
}

function hub(b: MeshBuilder, r: number, t0: number, t1: number, br: Brush, segs = 8): void {
  cylinder(b, X(0), r, t0, t1, segs, br);
}

/** Aro prata que, na pintura B do carro, fica na cor de acento. */
const RIM_B = withBrush(ALU, { layer: 2 });

function build(design: WheelDesign): { b: MeshBuilder; n: number } {
  const b = new MeshBuilder();
  switch (design) {
    case 'sport5': {
      // Cinco raios largos prata; na pintura B do carro o aro fica na cor de acento.
      tireAndRim(b, 0.72, ALU, UNDER);
      spokes(b, 5, 0.16, 0.67, 0.13, 0.2, 0.4, RIM_B);
      hub(b, 0.17, 0.18, 0.44, RIM_B);
      hub(b, 0.07, 0.43, 0.47, TRIM, 6);
      return { b, n: 5 };
    }
    case 'mag': {
      // Magnésio cromado de prato fundo: borda larga e cinco raios grossos.
      tireAndRim(b, 0.66, CHROME, UNDER, false, 0.26);
      lathe(b, X(0), [[0.63, 0.47], [0.5, 0.42], [0.5, 0.3]], SEGS, () => CHROME);
      spokes(b, 5, 0.14, 0.52, 0.16, 0.26, 0.4, CHROME);
      hub(b, 0.16, 0.24, 0.45, CHROME);
      return { b, n: 5 };
    }
    case 'multi': {
      tireAndRim(b, 0.72, GUNMETAL, UNDER);
      spokes(b, 10, 0.14, 0.67, 0.045, 0.22, 0.4, GUNMETAL);
      hub(b, 0.15, 0.2, 0.44, GUNMETAL);
      hub(b, 0.06, 0.43, 0.46, ACCENT, 6);
      return { b, n: 10 };
    }
    case 'hubcap': {
      // Calota lisa prateada com oito entradas escuras.
      tireAndRim(b, 0.64, ALU, TRIM, false, 0.3);
      lathe(b, X(0), [[0.6, 0.3], [0.58, 0.4], [0.3, 0.44], [0, 0.46]], SEGS, () => ALU);
      for (let k = 0; k < 8; k++) {
        const th = (k / 8) * Math.PI * 2;
        decal(b, [0.43, Math.cos(th) * 0.45, Math.sin(th) * 0.45], [0.04, 0.12, 0.12], TRIM);
      }
      hub(b, 0.13, 0.42, 0.49, TRIM_CAP, 8);
      return { b, n: 8 };
    }
    case 'aero': {
      // Disco aerodinâmico: face lisa clara com cinco cortes escuros em hélice.
      tireAndRim(b, 0.74, GUNMETAL, TRIM, false, 0.3);
      lathe(b, X(0), [[0.7, 0.3], [0.68, 0.4], [0.2, 0.45], [0, 0.46]], SEGS, (i) => (i === 0 ? GUNMETAL : WHITE_DISC));
      for (let k = 0; k < 5; k++) {
        const th = (k / 5) * Math.PI * 2;
        const c = Math.cos(th); const s = Math.sin(th);
        const c2 = Math.cos(th + 0.5); const s2 = Math.sin(th + 0.5);
        beam(b, [0.445, 0.28 * c, 0.28 * s], [0.425, 0.62 * c2, 0.62 * s2], 0.08, 0.03, TRIM, [1, 0, 0]);
      }
      return { b, n: 5 };
    }
    case 'dish': {
      // Roda de rali: prato branco inteiro com seis furos.
      tireAndRim(b, 0.64, WHITE, UNDER, false, 0.3);
      lathe(b, X(0), [[0.6, 0.3], [0.58, 0.38], [0.2, 0.42], [0, 0.44]], SEGS, () => WHITE);
      for (let k = 0; k < 6; k++) {
        const th = (k / 6) * Math.PI * 2;
        decal(b, [0.4, Math.cos(th) * 0.4, Math.sin(th) * 0.4], [0.04, 0.13, 0.13], UNDER);
      }
      hub(b, 0.1, 0.4, 0.47, GUNMETAL, 6);
      return { b, n: 6 };
    }
    case 'center': {
      // Hipercarro/protótipo: seis raios finos escuros e a trava central na cor de acento.
      tireAndRim(b, 0.78, CARBON_RIM, UNDER);
      spokes(b, 6, 0.15, 0.73, 0.06, 0.22, 0.38, CARBON_RIM);
      spokes(b, 6, 0.15, 0.5, 0.05, 0.22, 0.4, CARBON_RIM, Math.PI / 6);
      hub(b, 0.15, 0.2, 0.44, CARBON_RIM);
      hub(b, 0.09, 0.43, 0.5, ACCENT, 6);
      return { b, n: 6 };
    }
    case 'wire': {
      // Clássico: raios de arame cromados, faixa branca no pneu, porca borboleta.
      tireAndRim(b, 0.62, CHROME, TRIM, true, 0.24);
      spokes(b, 10, 0.12, 0.58, 0.028, 0.26, 0.34, CHROME);
      hub(b, 0.14, 0.22, 0.42, CHROME);
      beam(b, [0.46, -0.16, 0], [0.46, 0.16, 0], 0.06, 0.05, CHROME, [1, 0, 0]);
      return { b, n: 10 };
    }
    case 'steel': {
      // Picape: roda de aço de prato fundo com seis porcas e pneu de flanco alto.
      tireAndRim(b, 0.58, PAINT_RIM, UNDER, false, 0.3);
      lathe(b, X(0), [[0.55, 0.3], [0.52, 0.36], [0.26, 0.38], [0, 0.4]], SEGS, () => PAINT_RIM);
      for (let k = 0; k < 6; k++) {
        const th = (k / 6) * Math.PI * 2;
        decal(b, [0.41, Math.cos(th) * 0.17, Math.sin(th) * 0.17], [0.04, 0.05, 0.05], CHROME);
        decal(b, [0.39, Math.cos(th + 0.52) * 0.4, Math.sin(th + 0.52) * 0.4], [0.03, 0.09, 0.09], UNDER);
      }
      hub(b, 0.1, 0.38, 0.45, CHROME, 6);
      return { b, n: 6 };
    }
  }
}

const TRIM_CAP = withBrush(TRIM, { rough: 0.4 });
const WHITE_DISC = withBrush(WHITE, { rough: 0.3, metal: 0.2 });
const CARBON_RIM = withBrush(GUNMETAL, { rgb: [0.03, 0.032, 0.036] });
/** Aro de aço da picape: preto, ou na cor de acento na pintura B. */
const PAINT_RIM = withBrush(TRIM, { rough: 0.45, metal: 0.4, layer: 2 });

export function buildWheel(design: WheelDesign): WheelModel {
  const { b, n } = build(design);
  return { design, geometry: b.build(CREASE.wheel), symmetry: (Math.PI * 2) / n, triangles: b.triangles };
}

/**
 * Roda simples da qualidade baixa: pneu e disco liso prateado (a camada B pinta o disco), a mesma para
 * todos os estilos — uma chamada de desenho só para as 80 rodas e ~140 triângulos cada (12 lados).
 */
export function buildSimpleWheel(): WheelModel {
  const b = new MeshBuilder();
  lathe(b, X(0), [[0, -0.4], [0.86, -0.5], [1, -0.28], [1, 0.28], [0.88, 0.5], [0.62, 0.5], [0.6, 0.34], [0, 0.34]], 12,
    (i) => [UNDER, TIRE_SIDE, TIRE, TIRE_SIDE, TIRE_SIDE, RIM_B, RIM_B][i]);
  return { design: 'hubcap', geometry: b.build(CREASE.wheel), symmetry: Math.PI * 2, triangles: b.triangles };
}

/** Fração do ângulo de repetição do desenho que a roda pode girar por quadro na tela (anti-estroboscópio). */
export const MAX_SPIN_FRACTION = 0.4;

/** Giro mostrado num quadro: o real, limitado para o desenho nunca parecer girar para trás. */
export function spinStep(angle: number, symmetry: number): number {
  return Math.max(0, Math.min(angle, symmetry * MAX_SPIN_FRACTION));
}
