// Hot hatch: compacto e alto, capô curto, cabine que vai até o fim do carro e cai quase vertical na
// tampa traseira, aerofólio no teto, para-lamas alargados, grade colmeia com o friso na cor de acento,
// faróis de canto, escape central duplo e rodas de dez raios.
import { BodyShape, buildCabin, type Axle } from '../body';
import { exhaust, grille, lampPair, mirrors, pitched, plate, roundLampPair, splitter } from '../details';
import { beam, box, MeshBuilder } from '../kit';
import { finish, regions, type CarModel } from '../model';
import { ACCENT, AMBER, GLASS, GRILLE, HEAD, HEAD_HOUSING, LIVERY_A, LIVERY_B, PAINT, STRIPE_A, TAIL, TRIM, TRIM_SOFT } from '../paints';

export function buildHatch(): CarModel {
  const b = new MeshBuilder();
  // Rodas grandes nos cantos (entre-eixos longo, balanços curtos) e para-lamas alargados com borda preta.
  const axles: [Axle, Axle] = [{ z: -1.34, r: 0.36, x: 0.79, w: 0.3 }, { z: 1.38, r: 0.36, x: 0.79, w: 0.3 }];
  const base = regions();
  const shape = new BodyShape({
    secs: [
      { z: -2.08, yb: 0.27, yt: 0.64, hw: 0.84, crown: 0.02, sh: 0.1, tuck: 0.05, belt: 0.55 },
      { z: -1.94, yb: 0.21, yt: 0.73, hw: 0.89, crown: 0.03, sh: 0.1 },
      { z: -1.5, yb: 0.2, yt: 0.79, hw: 0.9, crown: 0.04, sh: 0.11 },
      { z: -0.9, yb: 0.2, yt: 0.85, hw: 0.9, crown: 0.04, sh: 0.11 },
      { z: 1.2, yb: 0.21, yt: 0.93, hw: 0.9, crown: 0.03, sh: 0.11 },
      { z: 1.92, yb: 0.25, yt: 0.96, hw: 0.88, crown: 0.02, sh: 0.1 },
      { z: 2.08, yb: 0.31, yt: 0.95, hw: 0.84, crown: 0.02, sh: 0.1 },
    ],
    axles, rise: [0.03, 0.03], flare: [0.06, 0.06], band: 0.05, clear: 0.05,
    brush: (r, _z, inArch) => (inArch && (r === 'sill' || r === 'lower') ? TRIM : base(r)),
    capFront: PAINT, capBack: PAINT,
  });
  shape.build(b);
  // Cabine alta que desce em curva até a tampa quase vertical.
  buildCabin(b, {
    secs: [
      { z: -0.95, hwb: 0.8, yb: 0.85, hwt: 0.8, yt: 0.85 },
      { z: -0.22, hwb: 0.83, yb: 0.87, hwt: 0.68, yt: 1.39, crown: 0.03 },
      { z: 1.3, hwb: 0.84, yb: 0.92, hwt: 0.67, yt: 1.37, crown: 0.03 },
      { z: 1.72, hwb: 0.84, yb: 0.94, hwt: 0.66, yt: 1.3, crown: 0.03 },
      { z: 2.05, hwb: 0.8, yb: 0.96, hwt: 0.8, yt: 0.96 },
    ],
    top: (gap, region) => (gap === 1 ? (region === 'stripe' ? STRIPE_A : PAINT) : gap === 2 ? PAINT : GLASS),
    side: (gap) => (gap >= 2 ? PAINT : GLASS),
  });
  // Colunas escuras: A e B.
  b.mirrored(() => {
    beam(b, [0.8, 0.86, -0.93], [0.68, 1.41, -0.22], 0.06, 0.04, TRIM_SOFT);
    beam(b, [0.845, 0.9, 0.5], [0.675, 1.38, 0.5], 0.08, 0.03, TRIM_SOFT);
  });
  // Aerofólio grande no fim do teto, na cor de acento, com as abas laterais.
  pitched(b, [0, 1.33, 1.8], 0.12, () => {
    box(b, [0, 1.33, 1.8], [1.36, 0.05, 0.42], ACCENT);
    b.mirrored(() => box(b, [0.66, 1.28, 1.78], [0.04, 0.12, 0.4], ACCENT));
  });
  // Frente: grade colmeia com friso de acento, faróis de canto, entrada inferior e milha redondos.
  grille(b, 0.6, -2.09, 0.8, 0.13, null, 1, ACCENT);
  lampPair(b, 0.6, 0.6, -2.06, 0.36, 0.13, 0.35, HEAD, true);
  box(b, [0, 0.33, -2.1], [1.2, 0.13, 0.06], GRILLE);
  splitter(b, 0.2, -2.06, 1.5, 0.16);
  roundLampPair(b, 0.66, 0.33, -2.07, 0.05, HEAD, true, TRIM, 0.04, 8);
  b.mirrored(() => box(b, [0.84, 0.62, -1.96], [0.08, 0.05, 0.1], AMBER));
  mirrors(b, 0.86, 0.95, -0.62, PAINT);
  // Traseira: lanternas nos cantos da tampa, placa, difusor e escape central duplo.
  lampPair(b, 0.64, 0.84, 2.07, 0.3, 0.17, 0, TAIL, false, HEAD_HOUSING, 0.05);
  plate(b, 0.6, 2.09);
  box(b, [0, 0.31, 2.06], [1.3, 0.12, 0.1], TRIM);
  const exhausts = [exhaust(b, 0.1, 0.3, 2.16, 0.055), exhaust(b, -0.1, 0.3, 2.16, 0.055)];
  return finish('hatch', b, { wheel: 'multi', axles, exhausts, liveries: [LIVERY_B, LIVERY_A] });
}
