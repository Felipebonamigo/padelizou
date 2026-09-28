// Pista do tutorial (passo 1.9): curta, fora das copas e de TRACKS — só getTrack a acha, pelo
// registro de pistas extras (src/core/track/index.ts). Uma volta tem, em ordem, um trecho para cada
// lição: reta para acelerar, curva média, reta de frenagem, curva forte, reta do nitro e o box logo
// antes da linha (o grid fica na reta curta depois dele). Os trechos são índices de segmento
// calculados das próprias operações, então mudar um comprimento aqui move a lição junto.
import { getTrack, registerExtraTrack } from '../core/track';
import type { Track, TrackDef, TrackOp } from '../core/types';

export const TUTORIAL_TRACK_ID = 'tutorial';
/** Voltas da corrida do tutorial: quem começa a última sem ter terminado as lições encerra o tutorial (ninguém cruza a chegada). */
export const TUTORIAL_LAPS = 6;
/** Força das curvas (convenção de tracks.ts: 2 fácil, 4 média, 6 forte). */
export const MEDIUM_CURVE = 4;
export const STRONG_CURVE = -6;

export type SectionId = 'accel' | 'curve' | 'brakeZone' | 'strong' | 'nitro' | 'pit' | 'grid';

const LAYOUT: ReadonlyArray<{ id: SectionId; op: TrackOp }> = [
  { id: 'accel', op: { op: 'straight', length: 120 } },
  { id: 'curve', op: { op: 'curve', length: 100, curve: MEDIUM_CURVE } },
  { id: 'brakeZone', op: { op: 'straight', length: 90 } },
  { id: 'strong', op: { op: 'curve', length: 90, curve: STRONG_CURVE } },
  { id: 'nitro', op: { op: 'straight', length: 160 } },
  { id: 'pit', op: { op: 'pit', length: 50 } },
  { id: 'grid', op: { op: 'straight', length: 30 } },
];

export const TUTORIAL_TRACK: Readonly<TrackDef> = Object.freeze({
  id: TUTORIAL_TRACK_ID, name: 'Autódromo-Escola', country: 'Brasil', scenery: 'coast', timeOfDay: 'day',
  laps: TUTORIAL_LAPS, difficulty: 1, ops: LAYOUT.map((l) => l.op),
});

/** Segmentos [start, end) de cada trecho. */
export interface Section { start: number; end: number }

export const TUTORIAL_SECTIONS: Readonly<Record<SectionId, Section>> = (() => {
  const out = {} as Record<SectionId, Section>;
  let at = 0;
  for (const l of LAYOUT) { out[l.id] = { start: at, end: at + l.op.length }; at += l.op.length; }
  return out;
})();

export function inSection(id: SectionId, segment: number): boolean {
  const s = TUTORIAL_SECTIONS[id];
  return segment >= s.start && segment < s.end;
}

registerExtraTrack(TUTORIAL_TRACK);

export function tutorialTrack(): Track {
  return getTrack(TUTORIAL_TRACK_ID);
}
