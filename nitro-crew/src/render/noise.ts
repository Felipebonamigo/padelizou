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

/**
 * Ruído de valor 3D suave em [-1, 1]: grade de passo 1, interpolação trilinear com smoothstep. Contínuo — pontos
 * coincidentes (e vizinhos) recebem valores iguais (e próximos): cor e deformação por ponto sem degrau entre faces.
 */
export function valueNoise3(seed: number, x: number, y: number, z: number): number {
  const xi = Math.floor(x); const yi = Math.floor(y); const zi = Math.floor(z);
  const fx = x - xi; const fy = y - yi; const fz = z - zi;
  const u = fx * fx * (3 - 2 * fx); const v = fy * fy * (3 - 2 * fy); const w = fz * fz * (3 - 2 * fz);
  const s = Math.imul(seed | 0, 0x5bd1e995);
  const h = (i: number, j: number, k: number): number => hash3((xi + i) ^ s, yi + j, zi + k) * 2 - 1;
  const x00 = h(0, 0, 0) + (h(1, 0, 0) - h(0, 0, 0)) * u;
  const x10 = h(0, 1, 0) + (h(1, 1, 0) - h(0, 1, 0)) * u;
  const x01 = h(0, 0, 1) + (h(1, 0, 1) - h(0, 0, 1)) * u;
  const x11 = h(0, 1, 1) + (h(1, 1, 1) - h(0, 1, 1)) * u;
  const y0 = x00 + (x10 - x00) * v;
  const y1 = x01 + (x11 - x01) * v;
  return y0 + (y1 - y0) * w;
}
