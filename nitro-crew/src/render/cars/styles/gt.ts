// GT: grã-turismo esportivo de motor dianteiro. Capô longo entre para-lamas que sobem sobre as rodas,
// cabine recuada em fastback, traseira curta com "ducktail", quatro lanternas redondas, saída de ar
// atrás da roda dianteira e rodas grandes de cinco raios.
import { BodyShape, buildCabin, cabinGlassTop, type Axle, type CabinSpec } from '../body';
import { calipers, diffuser, exhaust, grille, lampPair, mirrors, onTop, plate, roundLampPair, splitter } from '../details';
import { box, extrudeZY, fillet, MeshBuilder, roundBar } from '../kit';
import { finish, type CarModel } from '../model';
import { CHROME, GLASS, GRILLE, HEAD, HEAD_HOUSING, LIVERY_A, LIVERY_B, PAINT, STRIPE_A, STRIPE_B, TAIL, TRIM, TRIM_SOFT, UNDER } from '../paints';
import { beam } from '../kit';

export function buildGt(): CarModel {
  const b = new MeshBuilder();
  const axles: [Axle, Axle] = [{ z: -1.36, r: 0.37, x: 0.79, w: 0.3 }, { z: 1.34, r: 0.37, x: 0.79, w: 0.3 }];
  const shape = new BodyShape({
    secs: [
      { z: -2.2, yb: 0.25, yt: 0.5, hw: 0.8, crown: 0.02, sh: 0.16, tuck: 0.06, belt: 0.55 },
      { z: -2.06, yb: 0.17, yt: 0.6, hw: 0.9, crown: 0.03, sh: 0.15 },
      { z: -1.7, yb: 0.16, yt: 0.66, hw: 0.94, crown: 0.04 },
      { z: -0.85, yb: 0.17, yt: 0.74, hw: 0.94, crown: 0.05 },
      { z: 0.4, yb: 0.17, yt: 0.79, hw: 0.93, crown: 0.04, belt: 0.52 },
      { z: 1.3, yb: 0.18, yt: 0.84, hw: 0.95, crown: 0.03 },
      { z: 1.95, yb: 0.22, yt: 0.86, hw: 0.9, crown: 0.03 },
      { z: 2.2, yb: 0.3, yt: 0.84, hw: 0.8, crown: 0.02, sh: 0.14 },
    ],
    axles, rise: [0.12, 0.05], flare: [0, 0.015], band: 0.05,
    brush: (r) => ({ under: UNDER, well: UNDER, sill: TRIM, lower: PAINT, band: STRIPE_B, upper: PAINT, shoulder: PAINT, top: PAINT, stripe: STRIPE_A, center: PAINT })[r],
    capFront: PAINT, capBack: PAINT,
  });
  shape.build(b);
  // Cabine fastback.
  const cab: CabinSpec = {
    secs: [
      { z: -0.58, hwb: 0.8, yb: 0.77, hwt: 0.8, yt: 0.77 },
      { z: 0.16, hwb: 0.83, yb: 0.8, hwt: 0.6, yt: 1.16, crown: 0.03 },
      { z: 0.66, hwb: 0.84, yb: 0.81, hwt: 0.58, yt: 1.14, crown: 0.03 },
      { z: 1.72, hwb: 0.76, yb: 0.86, hwt: 0.76, yt: 0.86 },
    ],
    top: (gap, region) => (gap === 1 ? (region === 'stripe' ? STRIPE_A : PAINT) : GLASS),
    side: (gap) => (gap === 2 ? PAINT : GLASS),
  };
  buildCabin(b, cab);
  // Colunas A escuras (a faixa de vidro lê como uma peça só), até a borda redonda do teto.
  b.mirrored(() => beam(b, [0.8, 0.78, -0.56], cabinGlassTop(cab, 0.12), 0.06, 0.04, TRIM_SOFT));
  // Capô comprido de motor dianteiro: duas saídas de ar e a lombada central.
  b.mirrored(() => onTop(b, shape, 0.36, -1.35, 0.16, 0.34, 0.03, TRIM));
  onTop(b, shape, 0, -1.0, 0.18, 0.9, 0.04, PAINT, {}, 0.005, 0.02); // entre as duas faixas
  // Ducktail: lábio que sobe no fim da tampa (perfil arredondado).
  extrudeZY(b, fillet([[1.86, 0.87], [2.2, 0.85], [2.24, 0.93]], [0.04, 0.02, 0.015]), -0.74, 0.74, PAINT);
  // Frente: grade escura larga com moldura cromada, faróis finos no bico, divisor.
  grille(b, 0.37, -2.21, 0.86, 0.15, CHROME, 2);
  b.mirrored(() => roundBar(b, [0.62, 0.3, -2.17], [0.26, 0.09, 0.08], TRIM, { r: 0.03, axis: 'z' }));
  roundBar(b, [0, 0.24, -2.16], [0.9, 0.08, 0.08], GRILLE, { r: 0.03, axis: 'z' });
  onTop(b, shape, 0.58, -2.12, 0.34, 0.16, 0.05, HEAD_HOUSING, { pv: HEAD }, 0, 0.025);
  onTop(b, shape, -0.58, -2.12, 0.34, 0.16, 0.05, HEAD_HOUSING, { pv: HEAD }, 0, 0.025);
  splitter(b, 0.17, -2.14, 1.56, 0.22);
  // Saída de ar atrás da roda dianteira.
  b.mirrored(() => box(b, [0.935, 0.5, -0.62], [0.03, 0.12, 0.34], TRIM));
  mirrors(b, 0.84, 0.88, -0.36, PAINT);
  // Traseira: quatro lanternas redondas, placa, difusor, dois escapes.
  roundLampPair(b, 0.6, 0.66, 2.2, 0.085, TAIL, false, HEAD_HOUSING, 0.05);
  roundLampPair(b, 0.38, 0.66, 2.2, 0.075, TAIL, false, HEAD_HOUSING, 0.05);
  lampPair(b, 0.55, 0.52, 2.21, 0.5, 0.025, 0, TRIM, false, TRIM, 0.04);
  plate(b, 0.46, 2.22);
  diffuser(b, 0.24, 2.1, 1.2, 5);
  const exhausts = [exhaust(b, 0.42, 0.25, 2.26, 0.06), exhaust(b, -0.42, 0.25, 2.26, 0.06)];
  calipers(b, axles);
  return finish('gt', b, { wheel: 'sport5', axles, exhausts, liveries: [LIVERY_A, LIVERY_B] });
}
