import { buildTrack } from './builder';
import { trackDef } from './tracks';
import type { Track } from '../types';

const cache = new Map<string, Track>();

/** Pista montada (com cache: a montagem é determinística e a pista é imutável). */
export function getTrack(id: string): Track {
  let t = cache.get(id);
  if (!t) { t = buildTrack(trackDef(id)); cache.set(id, t); }
  return t;
}

export { buildTrack, segmentAt, maxCurveAhead } from './builder';
export { TRACKS, trackDef } from './tracks';
