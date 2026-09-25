// Ruído determinístico (por índice, nunca por quadro) para relevo, variação de cor e
// deformação de modelos. Sem Math.random: o mesmo segmento tem sempre o mesmo morro.

/** Hash de inteiros → [0, 1). */
export function hash2(a: number, b: number): number {
  let h = Math.imul(a | 0, 0x27d4eb2d) ^ Math.imul((b | 0) + 0x165667b1, 0x9e3779b1);
  h ^= h >>> 15; h = Math.imul(h, 0x2c1b3c6d); h ^= h >>> 12; h = Math.imul(h, 0x297a2d39); h ^= h >>> 15;
  return (h >>> 0) / 4294967296;
}

export function hash3(a: number, b: number, c: number): number {
  return hash2(a, Math.imul(b | 0, 1013) ^ Math.imul(c | 0, 0x68e31da4));
}

/** Ruído de valor 1D suave em [-1, 1], com período infinito. */
export function valueNoise(seed: number, t: number): number {
  const i = Math.floor(t);
  const f = t - i;
  const s = f * f * (3 - 2 * f);
  const a = hash2(seed, i) * 2 - 1;
  const b = hash2(seed, i + 1) * 2 - 1;
  return a + (b - a) * s;
}

/** Soma de oitavas (fractal) em [-1, 1] aproximadamente. */
export function fbm(seed: number, t: number, octaves: number): number {
  let sum = 0; let amp = 1; let norm = 0; let freq = 1;
  for (let o = 0; o < octaves; o++) {
    sum += valueNoise(seed + o * 101, t * freq) * amp;
    norm += amp; amp *= 0.5; freq *= 2.1;
  }
  return sum / norm;
}
