// A largada: o trecho logo depois da linha em que a vista dos lados está tomada — arquibancadas, o trecho sem cenário
// comum e o box que começa nela (o muro e as garagens à direita). O builder monta a largada com estas constantes; as
// praças (plazas.ts) e os marcos (render/scenery/layout.ts) contam "perto da largada" a partir do fim dela.
// Puro e sem trigonometria (o builder chama durante a montagem da pista).
import type { Track } from '../types';

/** Arquibancadas dos dois lados (o lado do box, não) até este segmento depois da largada (builder.ts). */
export const START_STANDS = 24;
/** Segmentos depois da largada sem o cenário comum da receita (builder.ts). */
export const START_CLEAR = 30;

/**
 * Janela do primeiro de cada marco turístico (segmentos depois do FIM da largada): o mais importante da pista e os
 * outros (o do horizonte, mais 150 — render/scenery/layout.ts). Existe para o marco ser visto cedo na volta 1; logo
 * depois da linha a vista dos lados é das arquibancadas e das garagens, por isso conta do fim da largada.
 */
export const FIRST_WINDOW = { primary: 150, other: 300 } as const;

/**
 * Fim da largada, em segmentos depois de `startIndex`: o maior entre as arquibancadas, o trecho sem cenário e o box,
 * se ele começa dentro desse trecho (é o caso de todas as pistas de hoje: box nos segmentos 0–39 → 40).
 */
export function startZoneEnd(track: Track): number {
  const segs = track.segments; const n = segs.length;
  const at = (d: number) => segs[(track.startIndex + d) % n];
  let end = Math.max(START_STANDS + 1, START_CLEAR);
  let pitInZone = false;
  for (let d = 0; d < end; d++) if (at(d).pit) { pitInZone = true; break; }
  if (pitInZone) while (end < n / 2 && at(end).pit) end++;
  return end;
}
