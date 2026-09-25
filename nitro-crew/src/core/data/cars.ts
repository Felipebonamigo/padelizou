// Os quatro carros, no espírito dos quatro do Top Gear: cada um troca uma coisa por outra.
import { REFERENCE_SPEED } from '../constants';
import type { CarDef } from '../types';

/** Consumo de referência: um tanque dura ~2,4 voltas de 400.000 unidades em aceleração total. */
const BASE_FUEL = 1 / (2.4 * 400_000);

export const CARS: CarDef[] = [
  {
    id: 'falcao', name: 'Falcão GT', color: '#f2f2f2',
    topSpeed: REFERENCE_SPEED * 1.0, accel: 700, brake: 2600, handling: 0.75, fuelPerUnit: BASE_FUEL * 1.0,
    blurb: 'Equilibrado. Bom em tudo, excelente em nada.',
  },
  {
    id: 'trovao', name: 'Trovão V12', color: '#e53935',
    topSpeed: REFERENCE_SPEED * 1.06, accel: 640, brake: 2400, handling: 0.6, fuelPerUnit: BASE_FUEL * 1.25,
    blurb: 'O mais rápido na reta. Bebe muito e sofre nas curvas.',
  },
  {
    id: 'tornado', name: 'Tornado RS', color: '#1e88e5',
    topSpeed: REFERENCE_SPEED * 0.95, accel: 830, brake: 2800, handling: 0.92, fuelPerUnit: BASE_FUEL * 0.95,
    blurb: 'Arranca e faz curva como ninguém. Falta fôlego no final da reta.',
  },
  {
    id: 'camelo', name: 'Camelo X', color: '#8e24aa',
    topSpeed: REFERENCE_SPEED * 0.975, accel: 700, brake: 2600, handling: 0.7, fuelPerUnit: BASE_FUEL * 0.68,
    blurb: 'Econômico: quase nunca precisa de box. Modesto no resto.',
  },
];

export function carDef(id: string): CarDef {
  const c = CARS.find((x) => x.id === id);
  if (!c) throw new Error(`Carro desconhecido: ${id}`);
  return c;
}
