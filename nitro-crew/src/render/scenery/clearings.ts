// Clareiras dos marcos turísticos para o terreno: os prédios de fundo da cidade (terrain.ts, quadras de 45 a 285 m)
// não nascem em cima de um marco nem entre ele e quem chega (o MASP e a Ponte Estaiada sumiam atrás das quadras de
// Sampa). O layout do cenário (layout.ts) preenche ao montar a pista; o terreno só lê. Módulo sem dependências: o
// terreno não pode importar o layout (o layout já importa o chão, que importa o terreno).
import type { Track } from '../../core/types';

export interface Clearing {
  /** Segmento do marco, lado (−1 / +1) e até onde (m do centro) fica livre. */
  seg: number;
  side: number;
  lat: number;
  /** Segmentos livres antes (o caminho de quem chega) e depois do marco. */
  back: number;
  ahead: number;
}

const byTrack = new WeakMap<Track, Clearing[]>();

export function setClearings(track: Track, list: Clearing[]): void {
  byTrack.set(track, list);
}

/** O segmento `seg`, do lado `side`, a `dist` m do centro, está numa clareira de marco? */
export function inClearing(track: Track, seg: number, side: number, dist: number): boolean {
  const list = byTrack.get(track);
  if (!list) return false;
  const n = track.segments.length;
  for (const c of list) {
    if (c.side !== side || dist > c.lat) continue;
    const d = ((seg - c.seg) % n + n) % n;
    if (d <= c.ahead || n - d <= c.back) return true;
  }
  return false;
}
