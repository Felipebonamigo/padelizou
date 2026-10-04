// Protótipo de endurance: muito baixo e comprido, bico rente entre para-lamas altos sobre as rodas,
// cockpit fechado estreito (bolha de vidro), barbatana de tubarão do teto até a asa, asa traseira de
// ponta a ponta com placas grandes, entradas laterais, faixa de lanterna, painéis de número e rodas
// de trava central.
import { BodyShape, buildCabin, type Axle } from '../body';
import { calipers, diffuser, exhaust, lampPair, lightBar, mirrors, splitter, wing } from '../details';
import { extrudeZY, MeshBuilder, roundBar } from '../kit';
import { finish, regions, type CarModel } from '../model';
import { ACCENT, CARBON, GLASS, GRILLE, HEAD, LIVERY_A, LIVERY_B, PAINT, STRIPE_A, TAIL, TRIM, WHITE } from '../paints';

export function buildPrototype(): CarModel {
  const b = new MeshBuilder();
  const axles: [Axle, Axle] = [{ z: -1.5, r: 0.355, x: 0.8, w: 0.31 }, { z: 1.45, r: 0.37, x: 0.79, w: 0.34 }];
  const shape = new BodyShape({
    secs: [
      { z: -2.2, yb: 0.1, yt: 0.26, hw: 0.86, crown: 0.02, sh: 0.3, tuck: 0.03, belt: 0.4 },
      { z: -2.0, yb: 0.09, yt: 0.34, hw: 0.93, crown: 0.03, sh: 0.3, tuck: 0.04 },
      { z: -1.4, yb: 0.09, yt: 0.43, hw: 0.95, crown: 0.04, sh: 0.3, tuck: 0.05 },
      { z: -0.6, yb: 0.09, yt: 0.5, hw: 0.95, crown: 0.06, sh: 0.26, tuck: 0.06 },
      { z: 0.6, yb: 0.1, yt: 0.58, hw: 0.95, crown: 0.05, sh: 0.2, tuck: 0.06 },
      { z: 1.5, yb: 0.11, yt: 0.68, hw: 0.95, crown: 0.04, sh: 0.2, tuck: 0.05 },
      { z: 2.2, yb: 0.2, yt: 0.72, hw: 0.93, crown: 0.03, sh: 0.18, tuck: 0.04 },
    ],
    axles, rise: [0.4, 0.14], flare: [0.01, 0.01], band: 0.06, clear: 0.05,
    brush: regions({ sill: CARBON }),
    capFront: CARBON, capBack: TRIM,
  });
  shape.build(b);
  // Cockpit fechado estreito e a tampa do motor que desce até a asa.
  buildCabin(b, {
    secs: [
      { z: -1.0, hwb: 0.5, yb: 0.52, hwt: 0.5, yt: 0.52 },
      { z: -0.36, hwb: 0.58, yb: 0.55, hwt: 0.44, yt: 1.02, crown: 0.05 },
      { z: 0.3, hwb: 0.6, yb: 0.58, hwt: 0.4, yt: 1.0, crown: 0.05 },
      { z: 1.4, hwb: 0.5, yb: 0.66, hwt: 0.5, yt: 0.66 },
    ],
    top: (gap, region) => (gap === 0 ? GLASS : region === 'stripe' ? STRIPE_A : PAINT),
    side: (gap) => (gap === 2 ? PAINT : GLASS),
  });
  // Barbatana de tubarão do teto até a asa.
  extrudeZY(b, [[0.25, 1.0], [2.0, 1.02], [2.0, 0.72], [1.2, 0.68]], -0.012, 0.012, ACCENT);
  // Asa de ponta a ponta com placas grandes (acento).
  wing(b, { z: 2.0, y: 0.98, span: 1.84, chord: 0.34, thick: 0.045, pitch: -0.12, brush: ACCENT, plate: ACCENT, strut: 'swan', baseY: 0.72, strutX: 0.3, strutBrush: CARBON });
  b.mirrored(() => roundBar(b, [0.935, 0.86, 1.96], [0.03, 0.34, 0.5], ACCENT, { r: 0.07, axis: 'x' }));
  // Frente: fendas de farol na frente dos para-lamas, divisor largo, entradas escuras.
  lampPair(b, 0.66, 0.5, -1.93, 0.3, 0.06, 1.0, HEAD, true, TRIM, 0.05);
  splitter(b, 0.1, -2.12, 1.82, 0.3, CARBON);
  b.mirrored(() => roundBar(b, [0.5, 0.2, -2.18], [0.4, 0.1, 0.06], GRILLE, { r: 0.04, axis: 'z' }));
  // Entrada lateral atrás da roda dianteira e painel de número.
  b.mirrored(() => {
    roundBar(b, [0.955, 0.34, -0.85], [0.02, 0.2, 0.4], GRILLE, { r: 0.06, axis: 'x' });
    roundBar(b, [0.955, 0.36, 0.2], [0.02, 0.28, 0.4], WHITE, { r: 0.07, axis: 'x' });
  });
  mirrors(b, 0.78, 0.66, -0.62, PAINT, 0.8);
  // Traseira: faixa de lanterna, difusor grande, escape central.
  lightBar(b, 0.62, 2.205, 1.6, 0.05, TAIL, false);
  diffuser(b, 0.2, 2.1, 1.6, 8, CARBON);
  const exhausts = [exhaust(b, 0, 0.44, 2.24, 0.07, 0.12)];
  calipers(b, axles);
  return finish('prototype', b, { wheel: 'center', axles, exhausts, liveries: [LIVERY_A | LIVERY_B, LIVERY_B] });
}
