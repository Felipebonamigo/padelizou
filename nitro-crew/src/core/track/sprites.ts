// Meia largura de cada sprite em `x` normalizado (escala 1): vale para a colisão (sim) e para a
// distribuição do cenário (builder), que posiciona pela BORDA interna, nunca pelo centro.
import type { SpriteKind } from '../types';

export const SPRITE_HALF_WIDTH: Record<SpriteKind, number> = {
  tree: 0.25, pine: 0.22, palm: 0.18, cactus: 0.14, bush: 0.2, boulder: 0.32, building: 0.9, tower: 0.45,
  lamp: 0.07, billboard: 0.55, sign_left: 0.22, sign_right: 0.22, grandstand: 1.0, banner_start: 0,
  pit_wall: 0.18, pit_sign: 0.12, cone: 0.05,
};

/** Menor distância entre o centro da pista e a borda interna de um sprite sólido (asfalto vai até 1). */
export const SPRITE_MIN_EDGE = 1.25;

/** `x` (positivo) que deixa a borda interna do sprite a `margin` do centro da pista. */
export function spriteX(kind: SpriteKind, scale: number, margin: number): number {
  return margin + SPRITE_HALF_WIDTH[kind] * scale;
}
