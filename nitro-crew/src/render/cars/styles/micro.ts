// Microcarro: curto (3,3 m) e gordinho (mesma largura dos outros), rodas pequenas nos cantos, cabine
// alta e redonda com o teto em outra cor, faróis redondos grandes ("olhos"), bigode cromado na frente,
// para-choques cromados finos, lanternas redondas e calotas.
import { BodyShape, buildCabin, type Axle } from '../body';
import { exhaust, mirrors, plate, roundLampPair } from '../details';
import { beam, box, MeshBuilder } from '../kit';
import { finish, regions, type CarModel } from '../model';
import { AMBER, CHROME, GLASS, HEAD, LIVERY_A, LIVERY_B, PAINT, STRIPE_A, TAIL, TRIM_SOFT } from '../paints';

export function buildMicro(): CarModel {
  const b = new MeshBuilder();
  const axles: [Axle, Axle] = [{ z: -1.1, r: 0.3, x: 0.78, w: 0.26 }, { z: 1.1, r: 0.3, x: 0.78, w: 0.26 }];
  const shape = new BodyShape({
    secs: [
      { z: -1.65, yb: 0.28, yt: 0.55, hw: 0.8, crown: 0.04, sh: 0.24, tuck: 0.1, belt: 0.5 },
      { z: -1.5, yb: 0.22, yt: 0.65, hw: 0.88, crown: 0.06, sh: 0.22, tuck: 0.1 },
      { z: -1.1, yb: 0.2, yt: 0.72, hw: 0.92, crown: 0.07, sh: 0.2, tuck: 0.1 },
      { z: -0.7, yb: 0.2, yt: 0.76, hw: 0.92, crown: 0.06, sh: 0.2, tuck: 0.1 },
      { z: 0.9, yb: 0.2, yt: 0.84, hw: 0.92, crown: 0.05, sh: 0.2, tuck: 0.1 },
      { z: 1.45, yb: 0.24, yt: 0.83, hw: 0.88, crown: 0.05, sh: 0.22, tuck: 0.1 },
      { z: 1.65, yb: 0.3, yt: 0.76, hw: 0.8, crown: 0.04, sh: 0.24, tuck: 0.1 },
    ],
    axles, rise: [0.05, 0.04], band: 0.05,
    brush: regions({ sill: PAINT }),
    capFront: PAINT, capBack: PAINT,
  });
  shape.build(b);
  // Cabine alta e redonda; o teto é a camada A (na pintura A fica na cor de acento, como um teto branco).
  const ROOF = STRIPE_A;
  buildCabin(b, {
    secs: [
      { z: -0.82, hwb: 0.8, yb: 0.76, hwt: 0.8, yt: 0.76 },
      { z: -0.3, hwb: 0.84, yb: 0.79, hwt: 0.68, yt: 1.46, crown: 0.07 },
      { z: 0.95, hwb: 0.84, yb: 0.84, hwt: 0.66, yt: 1.44, crown: 0.07 },
      { z: 1.5, hwb: 0.8, yb: 0.83, hwt: 0.8, yt: 0.83 },
    ],
    top: (gap) => (gap === 1 ? ROOF : GLASS),
    side: () => GLASS,
  });
  b.mirrored(() => {
    beam(b, [0.8, 0.78, -0.8], [0.68, 1.46, -0.3], 0.07, 0.04, PAINT);
    beam(b, [0.855, 0.82, 0.35], [0.675, 1.45, 0.35], 0.08, 0.03, TRIM_SOFT);
    beam(b, [0.85, 0.84, 0.95], [0.66, 1.44, 0.95], 0.09, 0.04, PAINT);
  });
  // Frente: "olhos" redondos grandes, bigode cromado, piscas, para-choque cromado fino.
  roundLampPair(b, 0.54, 0.6, -1.58, 0.14, HEAD, true, CHROME, 0.07, 12);
  beam(b, [-0.24, 0.46, -1.67], [0.24, 0.46, -1.67], 0.04, 0.03, CHROME);
  b.mirrored(() => box(b, [0.78, 0.46, -1.58], [0.08, 0.06, 0.05], AMBER));
  box(b, [0, 0.32, -1.68], [1.5, 0.06, 0.07], CHROME);
  plate(b, 0.4, -1.69);
  mirrors(b, 0.86, 0.9, -0.62, CHROME, 0.9);
  // Traseira: lanternas redondas, para-choque cromado, escape pequeno.
  roundLampPair(b, 0.6, 0.7, 1.62, 0.08, TAIL, false, CHROME, 0.05, 10);
  box(b, [0, 0.36, 1.68], [1.5, 0.06, 0.07], CHROME);
  plate(b, 0.52, 1.66);
  const exhausts = [exhaust(b, 0.46, 0.26, 1.72, 0.045, 0.14)];
  return finish('micro', b, { wheel: 'hubcap', axles, exhausts, liveries: [LIVERY_A, LIVERY_B] });
}
