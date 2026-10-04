// Sedã econômico: três volumes (capô, cabine alta, porta-malas), linhas mansas, friso cromado na linha
// das janelas, frisos de borracha nas portas, faróis e lanternas retangulares largos, placas, um escape
// discreto e calotas.
import { BodyShape, buildCabin, cabinGlassTop, type Axle, type CabinSpec } from '../body';
import { exhaust, grille, lampPair, mirrors, plate } from '../details';
import { beam, box, MeshBuilder, roundBar } from '../kit';
import { finish, regions, type CarModel } from '../model';
import { AMBER, CHROME, GLASS, HEAD, HEAD_HOUSING, LIVERY_A, LIVERY_B, PAINT, PAINT_SHADE, REVERSE, STRIPE_A, TAIL, TRIM, TRIM_SOFT } from '../paints';

export function buildSedan(): CarModel {
  const b = new MeshBuilder();
  const axles: [Axle, Axle] = [{ z: -1.36, r: 0.33, x: 0.78, w: 0.25 }, { z: 1.32, r: 0.33, x: 0.78, w: 0.25 }];
  const shape = new BodyShape({
    secs: [
      { z: -2.2, yb: 0.27, yt: 0.66, hw: 0.85, crown: 0.03, sh: 0.14, tuck: 0.07, belt: 0.55 },
      { z: -2.06, yb: 0.22, yt: 0.72, hw: 0.9, crown: 0.04, sh: 0.14, tuck: 0.07 },
      { z: -1.2, yb: 0.21, yt: 0.79, hw: 0.92, crown: 0.05, sh: 0.13 },
      { z: -0.75, yb: 0.21, yt: 0.83, hw: 0.92, crown: 0.05, sh: 0.13 },
      { z: 1.0, yb: 0.22, yt: 0.91, hw: 0.92, crown: 0.03, sh: 0.13 },
      { z: 1.6, yb: 0.24, yt: 0.93, hw: 0.91, crown: 0.03, sh: 0.13 },
      { z: 2.06, yb: 0.28, yt: 0.93, hw: 0.88, crown: 0.03, sh: 0.13 },
      { z: 2.2, yb: 0.34, yt: 0.89, hw: 0.83, crown: 0.02, sh: 0.13 },
    ],
    axles, rise: [0.02, 0.02], band: 0.05,
    brush: regions({ sill: PAINT_SHADE }),
    capFront: PAINT, capBack: PAINT,
  });
  shape.build(b);
  const cab: CabinSpec = {
    secs: [
      { z: -0.8, hwb: 0.81, yb: 0.83, hwt: 0.81, yt: 0.83 },
      { z: -0.1, hwb: 0.84, yb: 0.85, hwt: 0.7, yt: 1.42, crown: 0.03 },
      { z: 0.82, hwb: 0.85, yb: 0.88, hwt: 0.7, yt: 1.42, crown: 0.03 },
      { z: 1.46, hwb: 0.82, yb: 0.92, hwt: 0.82, yt: 0.92 },
    ],
    top: (gap, region) => (gap === 1 ? (region === 'stripe' ? STRIPE_A : PAINT) : GLASS),
    side: () => GLASS,
  };
  buildCabin(b, cab);
  b.mirrored(() => {
    beam(b, [0.81, 0.84, -0.78], cabinGlassTop(cab, -0.14), 0.07, 0.04, PAINT);
    beam(b, [0.86, 0.87, 0.36], cabinGlassTop(cab, 0.36), 0.08, 0.03, TRIM_SOFT);
    beam(b, [0.855, 0.9, 0.82], cabinGlassTop(cab, 0.86), 0.1, 0.03, PAINT);
    // Friso cromado na linha das janelas e friso de borracha nas portas.
    beam(b, [0.865, 0.86, -0.75], [0.865, 0.905, 1.4], 0.02, 0.02, CHROME);
    box(b, [0.925, 0.5, 0.2], [0.02, 0.05, 1.5], TRIM);
  });
  // Frente: grade fina com barra cromada, faróis largos, pisca âmbar, para-choque um tom abaixo.
  grille(b, 0.58, -2.21, 0.72, 0.12, CHROME, 1, CHROME);
  lampPair(b, 0.6, 0.6, -2.19, 0.38, 0.13, 0.1, HEAD, true);
  b.mirrored(() => box(b, [0.74, 0.47, -2.205], [0.12, 0.05, 0.03], AMBER));
  roundBar(b, [0, 0.33, -2.22], [1.66, 0.14, 0.06], PAINT_SHADE, { r: 0.03 });
  box(b, [0, 0.3, -2.25], [0.9, 0.06, 0.03], TRIM);
  plate(b, 0.4, -2.26);
  mirrors(b, 0.87, 0.93, -0.58, PAINT);
  // Traseira: lanternas largas com a luz de ré, placa, um escape.
  lampPair(b, 0.58, 0.8, 2.21, 0.44, 0.15, 0, TAIL, false, HEAD_HOUSING, 0.05);
  lampPair(b, 0.3, 0.8, 2.215, 0.12, 0.15, 0, REVERSE, false, HEAD_HOUSING, 0.04);
  plate(b, 0.6, 2.22);
  roundBar(b, [0, 0.36, 2.23], [1.7, 0.14, 0.06], PAINT_SHADE, { r: 0.03 });
  const exhausts = [exhaust(b, 0.5, 0.26, 2.25, 0.05)];
  return finish('sedan', b, { wheel: 'hubcap', axles, exhausts, liveries: [0, LIVERY_B, LIVERY_A] });
}
