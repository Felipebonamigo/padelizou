// Registro dos 13 estilos de carroceria: cada `CarBody` tem o seu construtor, sem modelo genérico de
// reserva (estilo sem construtor é erro, e o teste confere que as 13 malhas são diferentes).
import type { CarBody } from '../../core/types';
import type { CarModel } from './model';
import { buildClassic } from './styles/classic';
import { buildElectric } from './styles/electric';
import { buildGt } from './styles/gt';
import { buildHatch } from './styles/hatch';
import { buildHyper } from './styles/hyper';
import { buildMicro } from './styles/micro';
import { buildMuscle } from './styles/muscle';
import { buildPickup } from './styles/pickup';
import { buildPrototype } from './styles/prototype';
import { buildRally } from './styles/rally';
import { buildRoadster } from './styles/roadster';
import { buildSedan } from './styles/sedan';
import { buildWedge } from './styles/wedge';

export const MODEL_BUILDERS: Readonly<Record<CarBody, () => CarModel>> = {
  gt: buildGt,
  muscle: buildMuscle,
  hatch: buildHatch,
  sedan: buildSedan,
  electric: buildElectric,
  rally: buildRally,
  hyper: buildHyper,
  classic: buildClassic,
  wedge: buildWedge,
  pickup: buildPickup,
  prototype: buildPrototype,
  micro: buildMicro,
  roadster: buildRoadster,
};

export function buildModel(body: CarBody): CarModel {
  const make = MODEL_BUILDERS[body];
  if (!make) throw new Error(`Estilo de carroceria sem modelo: ${body}`);
  return make();
}
