// Os oito carros. Os quatro originais, no espírito dos quatro do Top Gear, estão sempre liberados;
// os quatro novos se compram na carreira (e depois ficam liberados em todo modo). Cada um troca
// uma coisa por outra — nenhum é melhor em tudo. Nomes inventados, sem marca de verdade.
import { REFERENCE_SPEED } from '../constants';
import type { CarDef } from '../types';

/** Consumo de referência: um tanque dura ~2,4 voltas de 400.000 unidades em aceleração total. */
const BASE_FUEL = 1 / (2.4 * 400_000);

export const CARS: CarDef[] = [
  {
    id: 'falcao', name: 'Falcão GT', color: '#f2f2f2', price: 0,
    topSpeed: REFERENCE_SPEED * 1.0, accel: 700, brake: 2600, handling: 0.75, fuelPerUnit: BASE_FUEL * 1.0,
    blurb: 'Equilibrado. Bom em tudo, excelente em nada.',
  },
  {
    id: 'trovao', name: 'Trovão V12', color: '#e53935', price: 0,
    topSpeed: REFERENCE_SPEED * 1.06, accel: 640, brake: 2400, handling: 0.6, fuelPerUnit: BASE_FUEL * 1.25,
    blurb: 'O mais rápido na reta. Bebe muito e sofre nas curvas.',
  },
  {
    id: 'tornado', name: 'Tornado RS', color: '#1e88e5', price: 0,
    topSpeed: REFERENCE_SPEED * 0.95, accel: 830, brake: 2800, handling: 0.92, fuelPerUnit: BASE_FUEL * 0.95,
    blurb: 'Arranca e faz curva como ninguém. Falta fôlego no final da reta.',
  },
  {
    id: 'camelo', name: 'Camelo X', color: '#8e24aa', price: 0,
    topSpeed: REFERENCE_SPEED * 0.975, accel: 700, brake: 2600, handling: 0.7, fuelPerUnit: BASE_FUEL * 0.68,
    blurb: 'Econômico: quase nunca precisa de box. Modesto no resto.',
  },
  // ── À venda na carreira ──
  {
    id: 'sucuri', name: 'Sucuri E', color: '#2e7d32', price: 16_000,
    topSpeed: REFERENCE_SPEED * 1.03, accel: 640, brake: 2700, handling: 0.8, fuelPerUnit: BASE_FUEL * 0.5,
    blurb: 'Um tanque para a corrida inteira e boa velocidade. Pesado para arrancar.',
  },
  {
    id: 'carcara', name: 'Carcará RS', color: '#f9a825', price: 20_000,
    topSpeed: REFERENCE_SPEED * 0.98, accel: 900, brake: 3000, handling: 0.97, fuelPerUnit: BASE_FUEL * 1.05,
    blurb: 'Dispara na saída e cola na curva. Perde para todos no fim da reta longa.',
  },
  {
    id: 'pororoca', name: 'Pororoca V10', color: '#ff6d00', price: 22_000,
    topSpeed: REFERENCE_SPEED * 1.12, accel: 660, brake: 2500, handling: 0.58, fuelPerUnit: BASE_FUEL * 1.4,
    blurb: 'Ninguém anda mais na reta. Arisco nas curvas e bebe como nenhum outro.',
  },
  {
    id: 'boitata', name: 'Boitatá GT', color: '#00acc1', price: 30_000,
    topSpeed: REFERENCE_SPEED * 1.07, accel: 780, brake: 2800, handling: 0.84, fuelPerUnit: BASE_FUEL * 0.9,
    blurb: 'Forte em tudo, sem ponto fraco. O preço é o mais alto da garagem.',
  },
];

/** Carros da IA: só os originais, para o elenco e o balanceamento não mudarem com os carros à venda. */
export const AI_CAR_POOL: readonly CarDef[] = CARS.filter((c) => c.price === 0);

export function carDef(id: string): CarDef {
  const c = CARS.find((x) => x.id === id);
  if (!c) throw new Error(`Carro desconhecido: ${id}`);
  return c;
}
