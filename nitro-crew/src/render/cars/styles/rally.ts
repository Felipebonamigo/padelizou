// Carro de rali: hatch quadrado e alto (suspensão levantada), para-lamas alargados em caixa com
// borda preta, aerofólio grande na tampa, tomada de ar no teto, quatro faróis de milha na frente,
// para-barros, painel de número nas portas, escape único grosso e rodas de prato brancas.
import { BodyShape, buildCabin, cabinGlassTop, type Axle, type CabinSpec } from '../body';
import { exhaust, grille, lampPair, mirrors, roundLampPair, wing } from '../details';
import { beam, box, MeshBuilder, roundBar, roundBox } from '../kit';
import { finish, regions, type CarModel } from '../model';
import { ACCENT, GLASS, GRILLE, HEAD, HEAD_HOUSING, LIVERY_A, LIVERY_B, MUD, PAINT, STRIPE_A, TAIL, TRIM, TRIM_SOFT, WHITE } from '../paints';

export function buildRally(): CarModel {
  const b = new MeshBuilder();
  const axles: [Axle, Axle] = [{ z: -1.3, r: 0.35, x: 0.8, w: 0.3 }, { z: 1.28, r: 0.35, x: 0.8, w: 0.3 }];
  const flareBrush = regions({ sill: TRIM });
  const shape = new BodyShape({
    secs: [
      { z: -2.15, yb: 0.31, yt: 0.66, hw: 0.84, crown: 0.02, sh: 0.08, tuck: 0.05, belt: 0.55 },
      { z: -2.0, yb: 0.27, yt: 0.75, hw: 0.88, crown: 0.02, sh: 0.08, tuck: 0.05 },
      { z: -1.4, yb: 0.27, yt: 0.8, hw: 0.89, crown: 0.03, sh: 0.08 },
      { z: -0.9, yb: 0.27, yt: 0.85, hw: 0.89, crown: 0.03, sh: 0.08 },
      { z: 1.3, yb: 0.28, yt: 0.93, hw: 0.89, crown: 0.02, sh: 0.08 },
      { z: 1.95, yb: 0.31, yt: 0.97, hw: 0.87, crown: 0.02, sh: 0.08 },
      { z: 2.12, yb: 0.37, yt: 0.96, hw: 0.84, crown: 0.02, sh: 0.08 },
    ],
    axles, rise: [0.02, 0.02], flare: [0.08, 0.08], band: 0.07, clear: 0.07,
    brush: (r, _z, inArch) => (inArch && (r === 'sill' || r === 'lower') ? TRIM : flareBrush(r)),
    capFront: PAINT, capBack: PAINT,
  });
  shape.build(b);
  const cab: CabinSpec = {
    secs: [
      { z: -0.95, hwb: 0.8, yb: 0.85, hwt: 0.8, yt: 0.85 },
      { z: -0.28, hwb: 0.83, yb: 0.87, hwt: 0.7, yt: 1.4, crown: 0.02 },
      { z: 1.5, hwb: 0.84, yb: 0.93, hwt: 0.68, yt: 1.38, crown: 0.02 },
      { z: 1.97, hwb: 0.8, yb: 0.96, hwt: 0.8, yt: 0.96 },
    ],
    top: (gap, region) => (gap === 1 ? (region === 'stripe' ? STRIPE_A : PAINT) : GLASS),
    side: (gap) => (gap === 2 ? PAINT : GLASS),
  };
  buildCabin(b, cab);
  b.mirrored(() => {
    beam(b, [0.8, 0.86, -0.93], cabinGlassTop(cab, -0.32), 0.06, 0.04, TRIM_SOFT);
    beam(b, [0.85, 0.9, 0.5], cabinGlassTop(cab, 0.5), 0.08, 0.03, TRIM_SOFT);
  });
  // Tomada de ar no teto (caixa arredondada) e aerofólio grande na tampa (acento).
  roundBox(b, [0, 1.43, 0.1], [0.3, 0.08, 0.36], PAINT, 0.035, { nu: GRILLE });
  wing(b, { z: 1.92, y: 1.26, span: 1.56, chord: 0.36, thick: 0.05, pitch: -0.16, brush: ACCENT, plate: ACCENT, strut: 'post', baseY: 1.0, strutX: 0.5 });
  // Frente: grade, faróis, barra com quatro faróis de milha (acendem à noite), gancho de reboque.
  grille(b, 0.62, -2.16, 0.7, 0.12, null, 2);
  lampPair(b, 0.6, 0.64, -2.14, 0.34, 0.12, 0.2, HEAD, true);
  roundBar(b, [0, 0.36, -2.18], [1.5, 0.14, 0.08], TRIM, { r: 0.035 });
  box(b, [0, 0.48, -2.22], [1.1, 0.04, 0.06], TRIM);
  roundLampPair(b, 0.17, 0.48, -2.22, 0.075, HEAD, true, TRIM, 0.07, 10);
  roundLampPair(b, 0.44, 0.48, -2.22, 0.075, HEAD, true, TRIM, 0.07, 10);
  box(b, [0.45, 0.24, -2.2], [0.08, 0.06, 0.08], ACCENT);
  mirrors(b, 0.86, 0.95, -0.62, PAINT);
  // Painel de número nas portas e para-barros atrás das rodas.
  b.mirrored(() => {
    box(b, [0.9, 0.6, 0.05], [0.02, 0.26, 0.46], WHITE);
    for (const a of axles) box(b, [0.8, 0.22, a.z + a.r + 0.14], [0.3, 0.26, 0.02], MUD);
  });
  // Traseira: lanternas altas, para-choque preto e escape único grosso.
  lampPair(b, 0.64, 0.84, 2.13, 0.28, 0.16, 0, TAIL, false, HEAD_HOUSING, 0.05);
  roundBar(b, [0, 0.38, 2.14], [1.6, 0.14, 0.08], TRIM, { r: 0.035 });
  const exhausts = [exhaust(b, 0.55, 0.3, 2.22, 0.08, 0.16)];
  return finish('rally', b, { wheel: 'dish', axles, exhausts, liveries: [LIVERY_A | LIVERY_B, LIVERY_B] });
}
