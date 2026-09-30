// Peças que vários estilos usam: lanternas e faróis (caixa inclinada ou redondos), grade, retrovisores,
// escapamentos, aerofólio com suportes, placa, pinça de freio, difusor e divisor dianteiro.
import * as THREE from 'three';
import { AXIS_NZ, AXIS_Y, AXIS_Z, beam, box, cylinder, cuboid, lathe, type Brush, type MeshBuilder, type P3 } from './kit';
import type { Axle, BodyShape } from './body';
import { ACCENT, CHROME, GRILLE, HEAD_HOUSING, PLATE, TRIM, UNDER } from './paints';

/** Rotação em torno de x (inclinação) de uma peça construída em volta de `c`. */
export function pitched(b: MeshBuilder, c: P3, pitch: number, fn: () => void): void {
  const m = new THREE.Matrix4().makeTranslation(c[0], c[1], c[2])
    .multiply(new THREE.Matrix4().makeRotationX(pitch))
    .multiply(new THREE.Matrix4().makeTranslation(-c[0], -c[1], -c[2]));
  b.transformed(m, fn);
}

/** Rotação em torno de y (peça virada para o lado). */
export function yawed(b: MeshBuilder, c: P3, yaw: number, fn: () => void): void {
  const m = new THREE.Matrix4().makeTranslation(c[0], c[1], c[2])
    .multiply(new THREE.Matrix4().makeRotationY(yaw))
    .multiply(new THREE.Matrix4().makeTranslation(-c[0], -c[1], -c[2]));
  b.transformed(m, fn);
}

/**
 * Lâmpada retangular (par espelhado em x): caixa fina com a lente na face da frente (`front`) ou de
 * trás; `pitch` inclina junto com a superfície do carro. `x` é o centro da lâmpada da direita.
 */
export function lampPair(b: MeshBuilder, x: number, y: number, z: number, w: number, h: number, pitch: number, lens: Brush, front: boolean, housing: Brush = HEAD_HOUSING, depth = 0.08): void {
  b.mirrored(() => pitched(b, [x, y, z], pitch, () => {
    box(b, [x, y, z], [w, h, depth], housing, front ? { nu: lens } : { pu: lens });
  }));
}

/** Lâmpada redonda (par espelhado): cilindro curto no eixo z com a lente na ponta. */
export function roundLampPair(b: MeshBuilder, x: number, y: number, z: number, r: number, lens: Brush, front: boolean, bezel: Brush = CHROME, depth = 0.08, segs = 10): void {
  b.mirrored(() => {
    if (front) cylinder(b, { ...AXIS_NZ, o: [x, y, z] }, r, 0, depth, segs, bezel, bezel, lens);
    else cylinder(b, { ...AXIS_Z, o: [x, y, z] }, r, 0, depth, segs, bezel, bezel, lens);
  });
}

/** Grade: caixa escura com moldura e barras horizontais. `z` é a face da frente. */
export function grille(b: MeshBuilder, y: number, z: number, w: number, h: number, frame: Brush | null, bars: number, bar: Brush = TRIM, pitch = 0): void {
  pitched(b, [0, y, z], pitch, () => {
    box(b, [0, y, z + 0.04], [w, h, 0.08], GRILLE);
    for (let i = 1; i <= bars; i++) {
      const yy = y - h / 2 + (i * h) / (bars + 1);
      box(b, [0, yy, z - 0.005], [w - 0.04, 0.022, 0.03], bar);
    }
    if (frame) {
      const t = 0.035;
      box(b, [0, y + h / 2, z], [w + t, t, 0.05], frame); box(b, [0, y - h / 2, z], [w + t, t, 0.05], frame);
      b.mirrored(() => box(b, [w / 2, y, z], [t, h, 0.05], frame));
    }
  });
}

/** Retrovisores: haste e concha, com a face de trás espelhada (cromo). */
export function mirrors(b: MeshBuilder, x: number, y: number, z: number, shell: Brush, s = 1): void {
  b.mirrored(() => {
    beam(b, [x - 0.1 * s, y - 0.04 * s, z + 0.02], [x + 0.02 * s, y, z + 0.02], 0.03 * s, 0.03 * s, TRIM);
    box(b, [x + 0.05 * s, y + 0.02 * s, z], [0.12 * s, 0.09 * s, 0.13 * s], shell, { pu: CHROME });
  });
}

/** Ponteira de escapamento (cilindro no eixo z com o fundo escuro). Devolve a ponta, para a chama. */
export function exhaust(b: MeshBuilder, x: number, y: number, z: number, r: number, len = 0.14, tip: Brush = CHROME): P3 {
  cylinder(b, { ...AXIS_Z, o: [x, y, z - len] }, r, 0, len, 8, tip, tip, UNDER);
  return [x, y, z + 0.02];
}

export interface WingSpec {
  z: number; y: number; span: number; chord: number; thick?: number; pitch?: number;
  brush: Brush; plate?: Brush | null; strut?: 'post' | 'swan' | null; strutBrush?: Brush; baseY?: number; strutX?: number;
}

/** Aerofólio: lâmina inclinada, placas nas pontas e dois suportes até a tampa. */
export function wing(b: MeshBuilder, w: WingSpec): void {
  const t = w.thick ?? 0.05;
  pitched(b, [0, w.y, w.z], w.pitch ?? -0.12, () => {
    // Perfil afilado: borda de ataque mais grossa que a de fuga.
    const hs = w.span / 2; const c0 = w.z - w.chord / 2; const c1 = w.z + w.chord / 2;
    cuboid(b, [
      [-hs, w.y - t / 2, c0], [hs, w.y - t / 2, c0], [hs, w.y + t / 2, c0], [-hs, w.y + t / 2, c0],
      [-hs, w.y - t * 0.2, c1], [hs, w.y - t * 0.2, c1], [hs, w.y + t * 0.2, c1], [-hs, w.y + t * 0.2, c1],
    ], w.brush);
    if (w.plate !== null) {
      const pb = w.plate ?? w.brush;
      b.mirrored(() => box(b, [hs + 0.015, w.y - 0.03, w.z + 0.02], [0.03, 0.2, w.chord + 0.1], pb));
    }
  });
  if (w.strut) {
    const sb = w.strutBrush ?? TRIM;
    const sx = w.strutX ?? w.span * 0.3;
    const base = w.baseY ?? w.y - 0.25;
    b.mirrored(() => {
      if (w.strut === 'post') beam(b, [sx, base, w.z + 0.05], [sx, w.y - 0.02, w.z + 0.02], 0.04, 0.16, sb, [0, 0, -1]);
      else { // pescoço de cisne: sai da tampa, curva por trás e segura a lâmina por cima
        beam(b, [sx, base, w.z + 0.12], [sx, w.y + 0.08, w.z - 0.02], 0.035, 0.1, sb, [0, 0, -1]);
      }
    });
  }
}

/** Placa (sem texto), na frente ou atrás. */
export function plate(b: MeshBuilder, y: number, z: number, pitch = 0): void {
  pitched(b, [0, y, z], pitch, () => box(b, [0, y, z], [0.46, 0.13, 0.02], PLATE, { pv: TRIM, nv: TRIM }));
}

/** Pinça de freio (acento) no lado de dentro do aro, fora do que gira. */
export function calipers(b: MeshBuilder, axles: readonly Axle[], brush: Brush = ACCENT): void {
  for (const a of axles) {
    b.mirrored(() => box(b, [a.x + a.w * 0.08, a.r + a.r * 0.32, a.z + a.r * 0.26], [0.05, a.r * 0.36, a.r * 0.3], brush));
  }
}

/** Difusor traseiro com aletas. */
export function diffuser(b: MeshBuilder, y: number, z: number, w: number, fins: number, finBrush: Brush = TRIM): void {
  box(b, [0, y, z], [w, 0.1, 0.3], UNDER);
  for (let i = 0; i < fins; i++) {
    const x = -w / 2 + ((i + 0.5) * w) / fins;
    box(b, [x, y + 0.02, z + 0.08], [0.025, 0.14, 0.26], finBrush);
  }
}

/** Divisor dianteiro (lâmina escura rente ao chão). */
export function splitter(b: MeshBuilder, y: number, z: number, w: number, depth = 0.2, br: Brush = TRIM): void {
  box(b, [0, y, z], [w, 0.035, depth], br);
}

/** Lanterna em barra de ponta a ponta (elétrico, protótipo). */
export function lightBar(b: MeshBuilder, y: number, z: number, w: number, h: number, lens: Brush, front: boolean, pitch = 0): void {
  pitched(b, [0, y, z], pitch, () => box(b, [0, y, z], [w, h, 0.05], TRIM, front ? { nu: lens } : { pu: lens }));
}

/** Esfera facetada (capacete, farol de milha). */
export function ball(b: MeshBuilder, c: P3, r: number, br: Brush, segs = 8, visor?: Brush): void {
  const prof: Array<[number, number]> = [[0, -r], [r * 0.71, -r * 0.71], [r, 0], [r * 0.71, r * 0.71], [0, r]];
  lathe(b, { ...AXIS_Y, o: c }, prof, segs, (i) => (visor && i === 2 ? visor : br));
}


/**
 * Peça deitada na superfície de cima (farol no capô, tomada de ar, grelha): caixa de `w` × `thick` ×
 * `len` com a inclinação local do capô/tampa em (x, z); `faces.pv` é a face de cima (a lente).
 */
export function onTop(b: MeshBuilder, shape: BodyShape, x: number, z: number, w: number, len: number, thick: number, br: Brush, faces: Partial<Record<'pv' | 'nu' | 'pu', Brush>> = {}, lift = 0): void {
  const y0 = shape.topAt(z, x);
  const dy = shape.topAt(z + 0.05, x) - shape.topAt(z - 0.05, x);
  const alpha = Math.atan2(dy, 0.1);
  pitched(b, [x, y0, z], -alpha, () => box(b, [x, y0 + thick * 0.25 + lift, z], [w, thick, len], br, faces));
}
