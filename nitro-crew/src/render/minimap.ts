// Contorno da pista para o minimapa e para os menus. Funções puras, sem DOM: a pista não
// guarda coordenadas 2D — só curvatura por segmento —, então o contorno é a integral do rumo:
// cada segmento gira o rumo por `curve × k` e anda um passo. Quem desenha (3D ou 2D) recebe
// os pontos já normalizados.
import { SEGMENT_LENGTH } from '../core/constants';
import type { Track } from '../core/types';

export type Outline = Array<[number, number]>;

/** Quantos segmentos cada ponto do contorno representa (o contorno tem ~400 pontos). */
export function outlineStep(track: Track): number {
  return Math.max(1, Math.floor(track.segments.length / 400));
}

/**
 * Contorno da pista, normalizado para caber em `size`×`size` com margem, começando no
 * segmento 0. `k` é escolhido para a soma das curvas fechar exatamente uma volta (±2π); o
 * erro de posição que sobra é distribuído ao longo dos pontos (como `closeElevation` faz
 * com a altura), para o traçado fechar sem dente. Determinístico.
 */
export function trackOutline(track: Track, size: number): Outline {
  const segs = track.segments;
  const n = segs.length;
  if (n === 0 || size <= 0) return [];
  let total = 0;
  for (const s of segs) total += s.curve;
  const k = Math.abs(total) > 1e-9 ? (2 * Math.PI) / Math.abs(total) : 0;
  const step = outlineStep(track);

  const raw: Array<[number, number]> = [];
  const at: number[] = [];
  let heading = -Math.PI / 2; // para cima na tela (y cresce para baixo)
  let x = 0;
  let y = 0;
  for (let i = 0; i < n; i++) {
    if (i % step === 0) { raw.push([x, y]); at.push(i); }
    heading += segs[i].curve * k;
    x += Math.cos(heading);
    y += Math.sin(heading);
  }
  // Fecha o laço: o fim deveria voltar a (0, 0).
  const driftX = x;
  const driftY = y;
  for (let j = 0; j < raw.length; j++) {
    const f = at[j] / n;
    raw[j][0] -= driftX * f;
    raw[j][1] -= driftY * f;
  }

  let minX = Infinity; let minY = Infinity; let maxX = -Infinity; let maxY = -Infinity;
  for (const [px, py] of raw) {
    if (px < minX) minX = px; if (px > maxX) maxX = px;
    if (py < minY) minY = py; if (py > maxY) maxY = py;
  }
  const margin = size * 0.08;
  const range = Math.max(maxX - minX, maxY - minY, 1e-9);
  const scale = (size - 2 * margin) / range;
  const offX = (size - (maxX - minX) * scale) / 2;
  const offY = (size - (maxY - minY) * scale) / 2;
  return raw.map(([px, py]) => [offX + (px - minX) * scale, offY + (py - minY) * scale]);
}

/** Ponto do contorno correspondente à posição z (interpolado entre os dois pontos vizinhos). */
export function outlinePoint(outline: Outline, track: Track, z: number, out: [number, number]): void {
  const m = outline.length;
  if (m === 0) { out[0] = 0; out[1] = 0; return; }
  const step = outlineStep(track);
  const len = track.length;
  let zz = z % len;
  if (zz < 0) zz += len;
  const t = zz / SEGMENT_LENGTH / step;
  const i0 = Math.floor(t) % m;
  const i1 = (i0 + 1) % m;
  const f = t - Math.floor(t);
  out[0] = outline[i0][0] + (outline[i1][0] - outline[i0][0]) * f;
  out[1] = outline[i0][1] + (outline[i1][1] - outline[i0][1]) * f;
}
