// O estado é JSON puro, então serializar é trivial. O hash serve ao lockstep (comparar máquinas).
import { hashString } from './rng';
import { statsFor } from './sim/stats';
import type { RaceState } from './types';

export function serializeRace(state: RaceState): string {
  return JSON.stringify(state);
}

export function deserializeRace(json: string): RaceState {
  const s = JSON.parse(json) as RaceState;
  if (!Array.isArray(s.cars) || typeof s.tick !== 'number') throw new Error('Estado de corrida inválido');
  s.events = s.events ?? [];
  s.teamNitro = s.teamNitro ?? {};
  // Estado anterior às melhorias da carreira: atributos recalculados da configuração.
  for (const c of s.cars) if (!c.stats) c.stats = statsFor(s.config, c);
  return s;
}

/** Hash barato do que importa para detectar dessincronização entre clientes. */
export function hashRace(state: RaceState): number {
  const parts: string[] = [String(state.tick), state.phase, String(state.rng.s)];
  for (const c of state.cars) parts.push(`${c.id}:${c.z.toFixed(3)}:${c.x.toFixed(4)}:${c.speed.toFixed(3)}:${c.lap}:${c.fuel.toFixed(5)}:${c.nitroLeft}`);
  return hashString(parts.join('|'));
}
