// Roadster: conversível de dois lugares sem teto. Capô longo, traseira curta com as duas corcovas
// atrás dos bancos, para-brisa baixo com moldura, cockpit aberto (bancos, volante, piloto de capacete
// na cor de acento, à esquerda), santantônio duplo, faróis redondos, escapes gêmeos, rodas de magnésio.
import { BodyShape, type Axle } from '../body';
import { ball, exhaust, grille, mirrors, roundLampPair } from '../details';
import { AXIS_NZ, beam, box, cylinder, extrudeZY, insideBox, MeshBuilder } from '../kit';
import { finish, regions, type CarModel } from '../model';
import { ACCENT, AMBER, CHROME, GLASS, GLASS_DARK, HEAD, INTERIOR, LIVERY_A, LIVERY_B, PAINT, SEAT, TAIL, TRIM, TRIM_SOFT } from '../paints';

const PIT0 = -0.5; // começo do cockpit (painel)
const PIT1 = 0.78; // fim (atrás dos bancos)

export function buildRoadster(): CarModel {
  const b = new MeshBuilder();
  const axles: [Axle, Axle] = [{ z: -1.36, r: 0.355, x: 0.79, w: 0.3 }, { z: 1.32, r: 0.365, x: 0.79, w: 0.31 }];
  const base = regions();
  const shape = new BodyShape({
    secs: [
      { z: -2.2, yb: 0.26, yt: 0.5, hw: 0.76, crown: 0.03, sh: 0.2, tuck: 0.07, belt: 0.5 },
      { z: -2.05, yb: 0.2, yt: 0.58, hw: 0.88, crown: 0.04, sh: 0.18, tuck: 0.07 },
      { z: -1.5, yb: 0.19, yt: 0.65, hw: 0.93, crown: 0.05, sh: 0.17 },
      { z: PIT0, yb: 0.19, yt: 0.74, hw: 0.93, crown: 0.05, sh: 0.17 },
      { z: PIT1, yb: 0.2, yt: 0.78, hw: 0.93, crown: 0.04, sh: 0.17 },
      { z: 1.6, yb: 0.21, yt: 0.82, hw: 0.94, crown: 0.04, sh: 0.17 },
      { z: 2.05, yb: 0.25, yt: 0.8, hw: 0.88, crown: 0.04, sh: 0.18 },
      { z: 2.2, yb: 0.31, yt: 0.74, hw: 0.78, crown: 0.03, sh: 0.2 },
    ],
    axles, rise: [0.1, 0.08], band: 0.05,
    // Cockpit: sem as faces de cima entre as portas (o interior é desenhado à parte).
    brush: (r, z) => (z > PIT0 && z < PIT1 && (r === 'top' || r === 'stripe' || r === 'center') ? null : base(r)),
    capFront: PAINT, capBack: PAINT,
  });
  shape.build(b);
  const inner = shape.at(0).hw - shape.at(0).sh;
  insideBox(b, [0, 0.56, (PIT0 + PIT1) / 2], [inner * 2, 0.4, PIT1 - PIT0], INTERIOR);
  // Bancos e o piloto (à esquerda: o Brasil dirige pela direita da pista).
  b.mirrored(() => {
    box(b, [0.36, 0.44, 0.45], [0.44, 0.1, 0.44], SEAT);
    box(b, [0.36, 0.66, 0.66], [0.44, 0.5, 0.1], SEAT);
  });
  box(b, [-0.36, 0.72, 0.4], [0.4, 0.34, 0.26], TRIM_SOFT); // tronco
  ball(b, [-0.36, 1.0, 0.38], 0.15, ACCENT, 8, GLASS_DARK); // capacete com viseira
  cylinder(b, { ...AXIS_NZ, o: [-0.36, 0.8, -0.1] }, 0.16, 0, 0.03, 10, TRIM); // volante
  box(b, [0, 0.72, -0.44], [inner * 2, 0.12, 0.12], TRIM); // painel
  // Para-brisa baixo com moldura.
  const wy0 = 0.76; const wy1 = 1.05; const wz0 = -0.52; const wz1 = -0.36;
  extrudeZY(b, [[wz0, wy0], [wz0 + 0.02, wy0], [wz1 + 0.02, wy1], [wz1, wy1]], -0.72, 0.72, GLASS);
  beam(b, [-0.74, wy1, wz1], [0.74, wy1, wz1], 0.04, 0.04, CHROME);
  b.mirrored(() => beam(b, [0.74, wy0, wz0], [0.74, wy1, wz1], 0.04, 0.04, CHROME));
  // Santantônio duplo e as corcovas atrás dos bancos.
  b.mirrored(() => {
    beam(b, [0.2, 0.78, PIT1 + 0.02], [0.2, 1.12, PIT1 - 0.02], 0.05, 0.05, CHROME);
    beam(b, [0.52, 0.78, PIT1 + 0.02], [0.52, 1.12, PIT1 - 0.02], 0.05, 0.05, CHROME);
    beam(b, [0.18, 1.12, PIT1 - 0.02], [0.54, 1.12, PIT1 - 0.02], 0.05, 0.05, CHROME);
    extrudeZY(b, [[PIT1 - 0.02, 0.78], [PIT1 + 0.02, 0.98], [PIT1 + 0.2, 1.0], [1.7, 0.84], [1.7, 0.8]], 0.2, 0.52, PAINT);
  });
  // Frente: faróis redondos, boca com grade cromada, piscas.
  grille(b, 0.36, -2.21, 0.64, 0.14, CHROME, 2, CHROME);
  roundLampPair(b, 0.6, 0.53, -2.02, 0.1, HEAD, true, CHROME, 0.06, 12);
  b.mirrored(() => box(b, [0.66, 0.34, -2.15], [0.1, 0.05, 0.05], AMBER));
  box(b, [0, 0.24, -2.2], [1.3, 0.05, 0.08], TRIM);
  mirrors(b, 0.84, 0.86, -0.44, CHROME, 0.8);
  // Traseira: lanternas redondas, escapes gêmeos.
  roundLampPair(b, 0.56, 0.64, 2.17, 0.075, TAIL, false, CHROME, 0.05, 10);
  box(b, [0, 0.34, 2.2], [1.4, 0.08, 0.08], TRIM);
  const exhausts = [exhaust(b, 0.4, 0.26, 2.26, 0.055), exhaust(b, -0.4, 0.26, 2.26, 0.055)];
  return finish('roadster', b, { wheel: 'mag', axles, exhausts, liveries: [LIVERY_A, LIVERY_B] });
}
