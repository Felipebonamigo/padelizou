// Os catorze carros, um ou mais por estilo de carroceria. Sete estão sempre liberados — os quatro
// originais, no espírito dos quatro do Top Gear, e três da onda F (micro, picape, clássico) —; os outros
// sete se compram na carreira (e depois ficam liberados em todo modo). Cada um troca uma coisa por outra —
// nenhum é melhor em tudo. Nomes inventados (bicho e lenda brasileira), sem marca de verdade. Os números
// saíram de medida, não de chute: docs/CARROS.md (tabela e porquê) e scripts/car-balance.ts.
import { REFERENCE_SPEED } from '../constants';
import type { CarBody, CarDef } from '../types';

/** Consumo de referência: um tanque dura ~2,4 voltas de 400.000 unidades em aceleração total. */
const BASE_FUEL = 1 / (2.4 * 400_000);

export const CARS: CarDef[] = [
  {
    id: 'falcao', name: 'Falcão GT', color: '#f2f2f2', price: 0, body: 'gt',
    topSpeed: REFERENCE_SPEED * 1.0, accel: 700, brake: 2600, handling: 0.75, fuelPerUnit: BASE_FUEL * 1.0,
    blurb: 'Equilibrado. Bom em tudo, excelente em nada.',
  },
  {
    id: 'trovao', name: 'Trovão V12', color: '#e53935', price: 0, body: 'muscle',
    topSpeed: REFERENCE_SPEED * 1.06, accel: 640, brake: 2400, handling: 0.6, fuelPerUnit: BASE_FUEL * 1.25,
    blurb: 'O mais rápido na reta. Bebe muito e sofre nas curvas.',
  },
  {
    id: 'tornado', name: 'Tornado RS', color: '#1e88e5', price: 0, body: 'hatch',
    topSpeed: REFERENCE_SPEED * 0.95, accel: 830, brake: 2800, handling: 0.92, fuelPerUnit: BASE_FUEL * 0.95,
    blurb: 'Arranca como ninguém e faz curva muito bem. Falta fôlego no fim da reta.',
  },
  {
    id: 'camelo', name: 'Camelo X', color: '#8e24aa', price: 0, body: 'sedan',
    topSpeed: REFERENCE_SPEED * 0.975, accel: 700, brake: 2600, handling: 0.7, fuelPerUnit: BASE_FUEL * 0.68,
    blurb: 'Econômico: quase nunca precisa de box. Modesto no resto.',
  },
  // ── Livres da onda F ──
  {
    id: 'saci', name: 'Saci Mirim', color: '#8bc34a', accent: '#e53935', price: 0, body: 'micro',
    topSpeed: REFERENCE_SPEED * 0.92, accel: 800, brake: 3000, handling: 0.96, fuelPerUnit: BASE_FUEL * 0.55,
    blurb: 'Pequeno, arisco e quase não bebe. Some nas curvas, sofre na reta.',
  },
  {
    id: 'tatu', name: 'Tatu 4x4', color: '#9e2a2b', accent: '#e0c9a6', price: 0, body: 'pickup',
    topSpeed: REFERENCE_SPEED * 1.02, accel: 820, brake: 2300, handling: 0.66, fuelPerUnit: BASE_FUEL * 1.35,
    blurb: 'Arranca e retoma forte. Pesado na curva e no freio, e bebe muito.',
  },
  {
    id: 'boto', name: 'Boto Luxo', color: '#f06292', accent: '#fff3e0', price: 0, body: 'classic',
    topSpeed: REFERENCE_SPEED * 1.02, accel: 600, brake: 2200, handling: 0.7, fuelPerUnit: BASE_FUEL * 0.8,
    blurb: 'Cruzador: desliza na reta e economiza. Demora a embalar e freia mal.',
  },
  // ── À venda na carreira ──
  {
    id: 'curupira', name: 'Curupira S', color: '#455a64', accent: '#ff5722', price: 12_000, body: 'roadster',
    topSpeed: REFERENCE_SPEED * 0.97, accel: 800, brake: 3200, handling: 0.9, fuelPerUnit: BASE_FUEL * 0.8,
    blurb: 'Leve e sem teto: o melhor freio da garagem e curva fácil. Pouca reta.',
  },
  {
    id: 'sucuri', name: 'Sucuri E', color: '#2e7d32', price: 16_000, body: 'electric',
    topSpeed: REFERENCE_SPEED * 1.03, accel: 640, brake: 2700, handling: 0.8, fuelPerUnit: BASE_FUEL * 0.5,
    blurb: 'Um tanque para a corrida inteira e boa velocidade. Pesado para arrancar.',
  },
  {
    id: 'carcara', name: 'Carcará RS', color: '#f9a825', price: 20_000, body: 'rally',
    topSpeed: REFERENCE_SPEED * 0.98, accel: 900, brake: 3000, handling: 0.97, fuelPerUnit: BASE_FUEL * 1.05,
    blurb: 'Dispara na saída e cola na curva. Perde para todos no fim da reta longa.',
  },
  {
    id: 'pororoca', name: 'Pororoca V10', color: '#ff6d00', price: 22_000, body: 'hyper',
    topSpeed: REFERENCE_SPEED * 1.12, accel: 660, brake: 2500, handling: 0.58, fuelPerUnit: BASE_FUEL * 1.4,
    blurb: 'Ninguém anda mais na reta. Arisco nas curvas e bebe como nenhum outro.',
  },
  {
    id: 'iara', name: 'Iara Turbo', color: '#3949ab', accent: '#ff4081', price: 24_000, body: 'wedge',
    topSpeed: REFERENCE_SPEED * 1.09, accel: 700, brake: 2300, handling: 0.62, fuelPerUnit: BASE_FUEL * 0.85,
    blurb: 'Corta o ar: quase a reta do Pororoca, bebendo bem menos. Arisca na curva.',
  },
  {
    id: 'boitata', name: 'Boitatá GT', color: '#00acc1', price: 30_000, body: 'gt',
    topSpeed: REFERENCE_SPEED * 1.07, accel: 780, brake: 2800, handling: 0.84, fuelPerUnit: BASE_FUEL * 0.9,
    blurb: 'Forte em tudo, sem ponto fraco. Custa caro.',
  },
  {
    id: 'beijaflor', name: 'Beija-Flor', color: '#1de9b6', accent: '#7c4dff', price: 40_000, body: 'prototype',
    topSpeed: REFERENCE_SPEED * 1.04, accel: 800, brake: 3100, handling: 1, fuelPerUnit: BASE_FUEL * 1.35,
    blurb: 'Protótipo: ninguém faz curva igual. Bebe muito e não é o mais rápido na reta.',
  },
];

/** Os estilos de carroceria que o renderizador sabe desenhar (um modelo cada). */
export const CAR_BODIES: readonly CarBody[] = [
  'gt', 'muscle', 'hatch', 'sedan', 'electric', 'rally', 'hyper', 'classic', 'wedge', 'pickup', 'prototype', 'micro', 'roadster',
];

/**
 * Carros da IA: os livres (os 4 originais e os 3 da onda F), nunca um à venda — o carro comprado é do jogador.
 * Com os 3 novos a grade mostra 7 estilos de carroceria em vez de 4; medido (docs/CARROS.md): a volta média da
 * IA foi de 86,20 s a 87,33 s e a calibragem da carreira seguiu valendo.
 */
export const AI_CAR_POOL: readonly CarDef[] = CARS.filter((c) => c.price === 0);

export function carDef(id: string): CarDef {
  const c = CARS.find((x) => x.id === id);
  if (!c) throw new Error(`Carro desconhecido: ${id}`);
  return c;
}
