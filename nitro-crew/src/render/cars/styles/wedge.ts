// Cunha dos anos 80: planos retos e vincos secos, bico rente ao chão subindo em linha reta até a
// traseira, faróis escamoteáveis (fechados de dia, sobem à noite), frisos horizontais nas portas,
// venezianas pretas sobre a tampa, grade preta de ponta a ponta cobrindo as lanternas, asa na tampa.
import { BodyShape, buildCabin, type Axle } from '../body';
import { exhaust, mirrors, onTop, wing } from '../details';
import { beam, box, MeshBuilder, withBrush } from '../kit';
import { finish, regions, type CarModel } from '../model';
import { AMBER, GLASS, HEAD, HEAD_HOUSING, LIVERY_A, LIVERY_B, PAINT, STRIPE_A, TAIL, TRIM, TRIM_SOFT, UNDER } from '../paints';

export function buildWedge(): CarModel {
  const b = new MeshBuilder();
  const axles: [Axle, Axle] = [{ z: -1.36, r: 0.35, x: 0.8, w: 0.29 }, { z: 1.36, r: 0.375, x: 0.79, w: 0.33 }];
  const shape = new BodyShape({
    secs: [
      { z: -2.2, yb: 0.2, yt: 0.34, hw: 0.84, crown: 0, sh: 0.1, tuck: 0.09, belt: 0.45 },
      { z: -1.95, yb: 0.14, yt: 0.44, hw: 0.92, crown: 0, sh: 0.1, tuck: 0.1, belt: 0.45 },
      { z: -0.9, yb: 0.14, yt: 0.6, hw: 0.94, crown: 0.01, sh: 0.1, tuck: 0.12, belt: 0.45 },
      { z: 0.2, yb: 0.15, yt: 0.72, hw: 0.95, crown: 0.01, sh: 0.1, tuck: 0.12, belt: 0.45 },
      { z: 1.3, yb: 0.16, yt: 0.81, hw: 0.96, crown: 0.01, sh: 0.1, tuck: 0.12, belt: 0.45 },
      { z: 2.05, yb: 0.21, yt: 0.84, hw: 0.95, crown: 0.01, sh: 0.1, tuck: 0.1 },
      { z: 2.2, yb: 0.27, yt: 0.83, hw: 0.93, crown: 0, sh: 0.1, tuck: 0.08 },
    ],
    axles, rise: [0.03, 0.02], band: 0.05,
    brush: regions({ sill: TRIM }),
    capFront: PAINT, capBack: TRIM,
  });
  shape.build(b);
  buildCabin(b, {
    secs: [
      { z: -1.02, hwb: 0.78, yb: 0.61, hwt: 0.78, yt: 0.61 },
      { z: -0.1, hwb: 0.82, yb: 0.71, hwt: 0.62, yt: 1.08, crown: 0 },
      { z: 0.5, hwb: 0.84, yb: 0.75, hwt: 0.62, yt: 1.07, crown: 0 },
      { z: 1.56, hwb: 0.8, yb: 0.82, hwt: 0.8, yt: 0.82 },
    ],
    top: (gap, region) => (gap === 1 ? (region === 'stripe' ? STRIPE_A : PAINT) : gap === 2 ? PAINT : GLASS),
    side: (gap) => (gap === 2 ? PAINT : GLASS),
  });
  b.mirrored(() => beam(b, [0.78, 0.62, -1.0], [0.62, 1.08, -0.1], 0.05, 0.035, TRIM_SOFT));
  // Venezianas pretas sobre a tampa (no lugar do vidro traseiro).
  for (let i = 0; i < 6; i++) onTop(b, shape, 0, 0.72 + i * 0.13, 1.16 - i * 0.05, 0.05, 0.03, TRIM, {}, 0.1 + i * 0.012);
  // Faróis escamoteáveis: a caixa fica abaixo do capô de dia e sobe 11 cm à noite (aPaint.w no shader).
  const pod = withBrush(PAINT, { popup: 1 });
  const lens = withBrush(HEAD, { popup: 1 });
  b.mirrored(() => {
    const z = -1.72; const x = 0.52;
    const y = shape.topAt(z, x) - 0.075;
    box(b, [x, y, z], [0.4, 0.12, 0.24], pod, { nu: lens, nv: withBrush(TRIM, { popup: 1 }) });
    // Tampa desenhada no capô (linha escura fina) para mostrar onde o farol mora de dia.
    onTop(b, shape, x, z + 0.125, 0.4, 0.015, 0.01, TRIM);
  });
  // Frisos horizontais nas portas (cinco lâminas pretas).
  b.mirrored(() => {
    for (let i = 0; i < 5; i++) box(b, [0.948, 0.34 + i * 0.065, 0.45], [0.02, 0.03, 0.9 - i * 0.06], TRIM);
  });
  // Frente: luzes de posição no para-choque, divisor.
  b.mirrored(() => {
    box(b, [0.6, 0.29, -2.21], [0.3, 0.06, 0.03], AMBER);
    box(b, [0.3, 0.29, -2.21], [0.22, 0.06, 0.03], withBrush(HEAD, { head: 0.35 }));
  });
  box(b, [0, 0.18, -2.18], [1.6, 0.04, 0.12], TRIM);
  mirrors(b, 0.86, 0.78, -0.82, PAINT, 0.85);
  // Traseira: grade preta de ponta a ponta com as lanternas atrás das lâminas, asa na tampa.
  box(b, [0, 0.62, 2.205], [1.76, 0.18, 0.04], HEAD_HOUSING, { pu: TAIL });
  for (let i = 0; i < 4; i++) box(b, [0, 0.55 + i * 0.045, 2.23], [1.78, 0.012, 0.02], TRIM);
  box(b, [0, 0.3, 2.2], [1.7, 0.12, 0.06], UNDER);
  wing(b, { z: 1.98, y: 1.02, span: 1.62, chord: 0.3, thick: 0.045, pitch: -0.08, brush: PAINT, plate: PAINT, strut: 'post', baseY: 0.84, strutX: 0.62 });
  const exhausts = [exhaust(b, 0.42, 0.26, 2.26, 0.055), exhaust(b, -0.42, 0.26, 2.26, 0.055)];
  return finish('wedge', b, { wheel: 'aero', axles, exhausts, liveries: [LIVERY_B, LIVERY_A] });
}
