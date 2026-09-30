// Picape: alta e quadrada, cabine simples e caçamba aberta (fundo e paredes de dentro, caixas das rodas),
// santantônio com quatro faróis de milha, grade cromada grande, para-choques cromados grossos,
// para-lamas de plástico preto, lanternas verticais nos cantos e rodas de aço com pneu de flanco alto.
import { BodyShape, buildCabin, type Axle } from '../body';
import { exhaust, grille, lampPair, mirrors, plate, roundLampPair } from '../details';
import { beam, box, insideBox, MeshBuilder } from '../kit';
import { finish, regions, type CarModel } from '../model';
import { AMBER, CHROME, GLASS, HEAD, HEAD_HOUSING, LIVERY_A, LIVERY_B, PAINT, STRIPE_A, TAIL, TRIM, TRIM_SOFT, UNDER } from '../paints';

const BED0 = 0.44; // começo da caçamba (atrás da cabine)
const BED1 = 2.1; // fim (tampa)
const FLOOR = 0.8;
const RAIL = 1.06;

export function buildPickup(): CarModel {
  const b = new MeshBuilder();
  const axles: [Axle, Axle] = [{ z: -1.35, r: 0.4, x: 0.77, w: 0.33 }, { z: 1.32, r: 0.4, x: 0.77, w: 0.33 }];
  const base = regions({ sill: TRIM });
  const shape = new BodyShape({
    secs: [
      { z: -2.2, yb: 0.44, yt: 0.98, hw: 0.88, crown: 0.01, sh: 0.06, tuck: 0.04, belt: 0.55 },
      { z: -2.08, yb: 0.42, yt: 1.01, hw: 0.91, crown: 0.02, sh: 0.07, tuck: 0.05, belt: 0.55 },
      { z: -0.8, yb: 0.42, yt: 1.03, hw: 0.91, crown: 0.02, sh: 0.07, belt: 0.55 },
      { z: BED0, yb: 0.42, yt: RAIL, hw: 0.91, crown: 0.0, sh: 0.07, belt: 0.55 },
      { z: BED1, yb: 0.43, yt: RAIL, hw: 0.91, crown: 0.0, sh: 0.07, belt: 0.55 },
      { z: 2.2, yb: 0.45, yt: RAIL - 0.02, hw: 0.9, crown: 0.0, sh: 0.07, belt: 0.55 },
    ],
    axles, flare: [0.05, 0.05], band: 0.07, clear: 0.07,
    // Caçamba: sem as faces de cima entre as paredes (o interior é desenhado à parte).
    brush: (r, z, inArch) => {
      if (z > BED0 && z < BED1 && (r === 'top' || r === 'stripe' || r === 'center')) return null;
      if (inArch && (r === 'sill' || r === 'lower')) return TRIM;
      return base(r);
    },
    capFront: PAINT, capBack: PAINT,
  });
  shape.build(b);
  // Interior da caçamba: fundo, paredes e as caixas das rodas.
  const inner = shape.at(1).hw - shape.at(1).sh;
  insideBox(b, [0, (FLOOR + RAIL) / 2 + 0.01, (BED0 + BED1) / 2], [inner * 2, RAIL - FLOOR, BED1 - BED0], TRIM_SOFT, { nv: UNDER });
  b.mirrored(() => box(b, [inner - 0.14, FLOOR + 0.07, axles[1].z], [0.28, 0.14, 0.9], TRIM_SOFT));
  // Cabine alta com o vidro traseiro quase vertical.
  buildCabin(b, {
    secs: [
      { z: -0.98, hwb: 0.84, yb: 1.03, hwt: 0.84, yt: 1.03 },
      { z: -0.45, hwb: 0.86, yb: 1.04, hwt: 0.76, yt: 1.72, crown: 0.02 },
      { z: BED0 - 0.02, hwb: 0.86, yb: 1.05, hwt: 0.76, yt: 1.72, crown: 0.02 },
    ],
    top: (gap, region) => (gap === 1 ? (region === 'stripe' ? STRIPE_A : PAINT) : GLASS),
    side: () => GLASS,
  });
  // Parede de trás da cabine (o loft da cabine é aberto nas pontas) com o vidro traseiro.
  box(b, [0, 1.395, BED0 - 0.03], [1.72, 0.7, 0.04], PAINT);
  box(b, [0, 1.47, BED0 - 0.005], [1.12, 0.3, 0.02], GLASS);
  b.mirrored(() => {
    beam(b, [0.84, 1.04, -0.96], [0.76, 1.72, -0.45], 0.07, 0.04, PAINT);
    beam(b, [0.865, 1.06, -0.05], [0.765, 1.71, -0.05], 0.08, 0.03, TRIM_SOFT);
  });
  // Santantônio com quatro faróis de milha.
  b.mirrored(() => {
    beam(b, [0.7, RAIL, BED0 + 0.1], [0.64, 1.78, BED0 + 0.1], 0.06, 0.06, TRIM);
    beam(b, [0.64, 1.74, BED0 + 0.12], [0.8, RAIL + 0.01, BED0 + 0.75], 0.05, 0.05, TRIM); // escora até a borda
  });
  beam(b, [-0.67, 1.78, BED0 + 0.1], [0.67, 1.78, BED0 + 0.1], 0.07, 0.06, TRIM);
  roundLampPair(b, 0.18, 1.84, BED0 + 0.02, 0.07, HEAD, true, TRIM, 0.07, 10);
  roundLampPair(b, 0.46, 1.84, BED0 + 0.02, 0.07, HEAD, true, TRIM, 0.07, 10);
  // Frente: grade cromada grande, faróis retangulares, para-choque cromado grosso, protetor inferior.
  grille(b, 0.78, -2.21, 1.12, 0.32, CHROME, 3, CHROME);
  lampPair(b, 0.72, 0.8, -2.2, 0.28, 0.17, 0, HEAD, true, CHROME, 0.06);
  b.mirrored(() => box(b, [0.72, 0.66, -2.2], [0.24, 0.06, 0.05], AMBER));
  box(b, [0, 0.5, -2.22], [1.84, 0.16, 0.12], CHROME);
  box(b, [0, 0.38, -2.12], [1.1, 0.06, 0.2], TRIM);
  plate(b, 0.5, -2.29);
  mirrors(b, 0.87, 1.2, -0.72, TRIM, 1.1);
  // Traseira: lanternas verticais nos cantos, para-choque cromado com engate, escape de lado.
  lampPair(b, 0.84, 0.9, 2.2, 0.1, 0.3, 0, TAIL, false, HEAD_HOUSING, 0.05);
  box(b, [0, 0.5, 2.22], [1.84, 0.14, 0.12], CHROME);
  box(b, [0, 0.43, 2.3], [0.08, 0.06, 0.08], TRIM);
  plate(b, 0.7, 2.21);
  const exhausts = [exhaust(b, -0.62, 0.4, 2.2, 0.065, 0.2)];
  return finish('pickup', b, { wheel: 'steel', axles, exhausts, liveries: [LIVERY_B, LIVERY_A] });
}
