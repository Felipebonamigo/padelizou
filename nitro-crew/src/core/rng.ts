// Gerador determinístico (xorshift32 com mistura), com estado serializável como um número.
export interface RngState { s: number }

export function createRng(seed: number): RngState {
  let s = (seed >>> 0) || 0x9e3779b9;
  // Mistura inicial (hash de 32 bits) para sementes pequenas (0, 1, 2…) não começarem parecidas.
  s = Math.imul(s ^ (s >>> 16), 0x45d9f3b) >>> 0;
  s = Math.imul(s ^ (s >>> 16), 0x45d9f3b) >>> 0;
  s ^= s >>> 16;
  return { s: (s >>> 0) || 0x2545f491 };
}

/** Próximo número em [0, 1). Avança o estado. */
export function nextFloat(r: RngState): number {
  let s = r.s;
  s ^= s << 13; s >>>= 0;
  s ^= s >>> 17;
  s ^= s << 5; s >>>= 0;
  r.s = s || 0x2545f491;
  return (r.s >>> 0) / 4294967296;
}

/** Inteiro em [min, max]. */
export function nextInt(r: RngState, min: number, max: number): number {
  return min + Math.floor(nextFloat(r) * (max - min + 1));
}

/** Número em [min, max). */
export function nextRange(r: RngState, min: number, max: number): number {
  return min + nextFloat(r) * (max - min);
}

export function pick<T>(r: RngState, items: readonly T[]): T {
  return items[Math.floor(nextFloat(r) * items.length)];
}

/** Semente numérica estável a partir de um texto (FNV-1a). */
export function hashString(text: string): number {
  let h = 0x811c9dc5;
  for (let i = 0; i < text.length; i++) { h ^= text.charCodeAt(i); h = Math.imul(h, 0x01000193) >>> 0; }
  return h >>> 0;
}
