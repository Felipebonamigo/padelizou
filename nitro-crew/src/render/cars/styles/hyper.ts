// Hipercarro: cunha muito baixa, bico rente ao chão entre para-lamas altos, cabine-bolha avançada,
// tampa do motor com venezianas, entradas de ar laterais enormes, asa traseira em pescoço de cisne,
// difusor grande, escape central duplo e rodas de trava central (traseiras maiores).
import { BodyShape, buildCabin, type Axle } from '../body';
import { calipers, diffuser, exhaust, lampPair, mirrors, onTop, splitter, wing } from '../details';
import { beam, box, cuboid, MeshBuilder } from '../kit';
import { finish, regions, type CarModel } from '../model';
import { ACCENT, CARBON, GLASS, GRILLE, HEAD, HEAD_HOUSING, LIVERY_A, LIVERY_B, PAINT, STRIPE_A, TAIL, TRIM, UNDER } from '../paints';

export function buildHyper(): CarModel {
  const b = new MeshBuilder();
  const axles: [Axle, Axle] = [{ z: -1.42, r: 0.36, x: 0.8, w: 0.3 }, { z: 1.38, r: 0.385, x: 0.79, w: 0.34 }];
  const shape = new BodyShape({
    secs: [
      { z: -2.2, yb: 0.15, yt: 0.33, hw: 0.8, crown: 0.02, sh: 0.22, tuck: 0.04, belt: 0.5 },
      { z: -2.0, yb: 0.12, yt: 0.44, hw: 0.9, crown: 0.02, sh: 0.22, tuck: 0.05 },
      { z: -1.5, yb: 0.12, yt: 0.56, hw: 0.94, crown: 0.02, sh: 0.22, tuck: 0.06 },
      { z: -0.9, yb: 0.12, yt: 0.62, hw: 0.95, crown: 0.03, sh: 0.2, tuck: 0.07 },
      { z: 0.2, yb: 0.13, yt: 0.68, hw: 0.96, crown: 0.05, sh: 0.18, tuck: 0.09, belt: 0.45 },
      { z: 1.3, yb: 0.14, yt: 0.77, hw: 0.96, crown: 0.04, sh: 0.16, tuck: 0.08 },
      { z: 2.0, yb: 0.18, yt: 0.8, hw: 0.94, crown: 0.03, sh: 0.14 },
      { z: 2.2, yb: 0.24, yt: 0.75, hw: 0.88, crown: 0.02, sh: 0.14 },
    ],
    axles, rise: [0.2, 0.1], flare: [0.02, 0.03], band: 0.05, clear: 0.05,
    brush: regions({ sill: CARBON }),
    capFront: PAINT, capBack: TRIM,
  });
  shape.build(b);
  // Cabine-bolha avançada e tampa do motor em fastback.
  buildCabin(b, {
    secs: [
      { z: -1.08, hwb: 0.7, yb: 0.64, hwt: 0.7, yt: 0.64 },
      { z: -0.28, hwb: 0.8, yb: 0.68, hwt: 0.56, yt: 1.07, crown: 0.04 },
      { z: 0.34, hwb: 0.8, yb: 0.71, hwt: 0.52, yt: 1.05, crown: 0.04 },
      { z: 1.55, hwb: 0.7, yb: 0.79, hwt: 0.7, yt: 0.79 },
    ],
    top: (gap, region) => (gap === 1 ? (region === 'stripe' ? STRIPE_A : PAINT) : gap === 2 ? (region === 'stripe' ? STRIPE_A : PAINT) : GLASS),
    side: (gap) => (gap === 2 ? PAINT : GLASS),
  });
  b.mirrored(() => beam(b, [0.7, 0.65, -1.06], [0.56, 1.07, -0.28], 0.05, 0.035, CARBON));
  // Venezianas na tampa do motor.
  for (let i = 0; i < 4; i++) onTop(b, shape, 0, 1.62 + i * 0.12, 0.9, 0.05, 0.03, TRIM);
  // Entradas de ar laterais: cunha escura antes da roda traseira, com a lâmina de acento em cima.
  b.mirrored(() => {
    cuboid(b, [
      [0.9, 0.3, 0.2], [0.975, 0.3, 0.2], [0.975, 0.58, 0.2], [0.9, 0.58, 0.2],
      [0.9, 0.3, 0.86], [0.975, 0.3, 0.86], [0.975, 0.66, 0.86], [0.9, 0.66, 0.86],
    ], GRILLE, { ps: UNDER });
    beam(b, [0.975, 0.6, -0.1], [0.975, 0.68, 0.86], 0.03, 0.04, ACCENT);
  });
  // Frente: fendas de farol nos para-lamas, entrada central, divisor largo.
  lampPair(b, 0.62, 0.46, -1.98, 0.36, 0.05, 0.95, HEAD, true, HEAD_HOUSING, 0.05);
  box(b, [0, 0.25, -2.18], [0.9, 0.1, 0.08], GRILLE);
  b.mirrored(() => box(b, [0.66, 0.24, -2.1], [0.34, 0.12, 0.08], GRILLE));
  splitter(b, 0.14, -2.16, 1.72, 0.26, CARBON);
  mirrors(b, 0.84, 0.8, -0.72, PAINT, 0.85);
  // Asa traseira em pescoço de cisne (acento), lanternas finas, difusor e escape central duplo.
  wing(b, { z: 1.98, y: 1.1, span: 1.76, chord: 0.38, thick: 0.05, pitch: -0.14, brush: ACCENT, plate: ACCENT, strut: 'swan', baseY: 0.8, strutX: 0.42, strutBrush: CARBON });
  lampPair(b, 0.55, 0.66, 2.19, 0.6, 0.05, 0, TAIL, false, TRIM, 0.04);
  diffuser(b, 0.22, 2.1, 1.5, 7, CARBON);
  const exhausts = [exhaust(b, 0.13, 0.46, 2.25, 0.07, 0.12), exhaust(b, -0.13, 0.46, 2.25, 0.07, 0.12)];
  calipers(b, axles);
  return finish('hyper', b, { wheel: 'center', axles, exhausts, liveries: [LIVERY_B, LIVERY_A] });
}
