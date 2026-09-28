import { buildTrack } from './builder';
import { TRACKS, trackDef } from './tracks';
import type { Track, TrackDef } from '../types';

const cache = new Map<string, Track>();
/** Pistas fora das copas e de TRACKS (a do tutorial): só getTrack as acha — seleção, recordes e testes que varrem TRACKS não. */
const extraDefs: Record<string, TrackDef> = {};

/** Registra uma pista fora de TRACKS (idempotente para a mesma definição). */
export function registerExtraTrack(def: TrackDef): void {
  if (TRACKS.some((t) => t.id === def.id)) throw new Error(`Pista já existe em TRACKS: ${def.id}`);
  extraDefs[def.id] = def;
}

/** Pista montada (com cache: a montagem é determinística e a pista é imutável). */
export function getTrack(id: string): Track {
  let t = cache.get(id);
  if (!t) {
    const extra = Object.prototype.hasOwnProperty.call(extraDefs, id) ? extraDefs[id] : undefined;
    t = buildTrack(extra ?? trackDef(id));
    cache.set(id, t);
  }
  return t;
}

export { buildTrack, segmentAt, maxCurveAhead } from './builder';
export { TRACKS, trackDef } from './tracks';
