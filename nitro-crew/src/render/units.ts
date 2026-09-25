// Unidades: a simulação fala em "unidades de mundo" (segmento = 200, meia pista = 2000, y em
// unidades); o renderizador fala em metros. Escala arcade deliberada: 6000 u/s (300 km/h no
// velocímetro) viram 120 m/s na tela, para a sensação de velocidade ser exagerada.
import { SEGMENT_LENGTH } from '../core/constants';

/** x normalizado ±1 → ±7 m: pista de 14 m. */
export const ROAD_HALF_WIDTH_M = 7;
/** Um segmento (200 unidades) mede 4 m. */
export const SEGMENT_M = 4;
/**
 * Elevação: unidades de `y0`/`y1` → metros. 0,006 (o valor do pseudo-3D) dá rampas de 45% que
 * empinam a câmera de perseguição; 0,0025 dá 10–16%, morro de pista de corrida arcade.
 */
export const Y_SCALE = 0.0025;
/** Radianos de giro por unidade de `curve` por segmento (uma `curve: 4` de 100 segmentos ≈ 55°). */
export const HEADING_PER_CURVE = 0.0035;

const Z_SCALE = SEGMENT_M / SEGMENT_LENGTH;

export function xToMeters(x: number): number { return x * ROAD_HALF_WIDTH_M; }
export function zToMeters(z: number): number { return z * Z_SCALE; }
export function yToMeters(y: number): number { return y * Y_SCALE; }
