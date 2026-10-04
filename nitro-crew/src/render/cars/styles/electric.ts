// Elétrico: fastback liso num arco só, bico fechado (sem grade), teto de vidro, faixa de luz de ponta a
// ponta na frente e atrás, ombros arredondados, sem escapamento (a chama do nitro sai do difusor) e
// rodas aerodinâmicas de disco.
import { BodyShape, buildCabin, cabinGlassTop, type Axle, type CabinSpec } from '../body';
import { lightBar, mirrors, onTop } from '../details';
import { beam, box, MeshBuilder, roundBar, type P3 } from '../kit';
import { finish, regions, type CarModel } from '../model';
import { DRL, GLASS, GLASS_DARK, HEAD, HEAD_HOUSING, LIVERY_B, PAINT, TAIL, TRIM, TRIM_SOFT, UNDER } from '../paints';

export function buildElectric(): CarModel {
  const b = new MeshBuilder();
  const axles: [Axle, Axle] = [{ z: -1.4, r: 0.36, x: 0.79, w: 0.29 }, { z: 1.36, r: 0.36, x: 0.79, w: 0.29 }];
  const shape = new BodyShape({
    secs: [
      { z: -2.2, yb: 0.26, yt: 0.52, hw: 0.8, crown: 0.03, sh: 0.2, tuck: 0.08, belt: 0.5 },
      { z: -2.02, yb: 0.19, yt: 0.63, hw: 0.9, crown: 0.05, sh: 0.19, tuck: 0.09 },
      { z: -1.5, yb: 0.18, yt: 0.71, hw: 0.93, crown: 0.06, sh: 0.17, tuck: 0.09 },
      { z: -0.9, yb: 0.18, yt: 0.77, hw: 0.93, crown: 0.06, sh: 0.17, tuck: 0.09 },
      { z: 0.8, yb: 0.19, yt: 0.83, hw: 0.93, crown: 0.05, sh: 0.16, tuck: 0.09 },
      { z: 1.6, yb: 0.21, yt: 0.87, hw: 0.93, crown: 0.04, sh: 0.17, tuck: 0.09 },
      { z: 2.05, yb: 0.27, yt: 0.87, hw: 0.88, crown: 0.03, sh: 0.18 },
      { z: 2.2, yb: 0.35, yt: 0.84, hw: 0.8, crown: 0.03, sh: 0.2 },
    ],
    axles, rise: [0.06, 0.05], band: 0.04,
    brush: regions({ sill: TRIM_SOFT }),
    capFront: PAINT, capBack: PAINT,
  });
  shape.build(b);
  // Teto de vidro inteiro: da base do para-brisa ao fim do vidro traseiro, uma peça escura só.
  const cab: CabinSpec = {
    secs: [
      { z: -0.98, hwb: 0.8, yb: 0.78, hwt: 0.8, yt: 0.78 },
      { z: -0.12, hwb: 0.83, yb: 0.81, hwt: 0.62, yt: 1.36, crown: 0.05 },
      { z: 0.6, hwb: 0.84, yb: 0.83, hwt: 0.6, yt: 1.34, crown: 0.05 },
      { z: 1.95, hwb: 0.76, yb: 0.88, hwt: 0.76, yt: 0.88 },
    ],
    top: (gap) => (gap === 1 ? GLASS_DARK : GLASS),
    side: () => GLASS,
  };
  buildCabin(b, cab);
  // Moldura fina de acabamento em volta do vidro (colunas na cor do carro), no alto do vidro lateral.
  const ga = cabinGlassTop(cab, -0.16); const gb = cabinGlassTop(cab, 0.64);
  b.mirrored(() => {
    beam(b, [0.8, 0.79, -0.96], ga, 0.06, 0.035, PAINT);
    beam(b, ga, gb, 0.05, 0.035, PAINT);
    beam(b, gb, [0.76, 0.89, 1.93], 0.06, 0.035, PAINT);
  });
  // Frente: faixa de luz no bico e dois faróis finos nas pontas; entrada inferior estreita.
  onTop(b, shape, 0, -2.12, 1.3, 0.05, 0.04, TRIM, { pv: DRL });
  onTop(b, shape, 0.62, -2.07, 0.3, 0.1, 0.05, HEAD_HOUSING, { pv: HEAD });
  onTop(b, shape, -0.62, -2.07, 0.3, 0.1, 0.05, HEAD_HOUSING, { pv: HEAD });
  roundBar(b, [0, 0.3, -2.18], [1.0, 0.06, 0.06], TRIM, { r: 0.03 });
  mirrors(b, 0.85, 0.9, -0.66, PAINT, 0.9);
  // Traseira: faixa de lanterna de ponta a ponta e difusor liso (sem escapamento).
  lightBar(b, 0.78, 2.205, 1.5, 0.06, TAIL, false);
  roundBar(b, [0, 0.3, 2.18], [1.3, 0.1, 0.1], UNDER, { r: 0.04 });
  // Maçanetas embutidas (detalhe fino na cor escura).
  b.mirrored(() => { box(b, [0.935, 0.66, -0.25], [0.02, 0.025, 0.16], TRIM); box(b, [0.935, 0.68, 0.55], [0.02, 0.025, 0.16], TRIM); });
  const exhausts: P3[] = [[0.3, 0.3, 2.24], [-0.3, 0.3, 2.24]];
  return finish('electric', b, { wheel: 'aero', axles, exhausts, liveries: [LIVERY_B, 0] });
}
