// Contrato dos marcos turísticos (onda G, docs/PISTAS-TURISMO.md): o modelo e onde ele aparece.
// Marcos são só visuais: nunca dentro do alcance do carro (|x| > 26 m), nunca escondendo a pista.
//
// Como o modelo é montado (o layout conta com isto — docs/VISUAL.md, "Marcos turísticos"):
// - origem no centro da base, y = 0 no chão; o que fica abaixo de 0 (alicerce, saia de pedra) cobre o declive —
//   o layout pousa o marco no ponto MAIS BAIXO do chão sob a pegada, então a parte de cima do declive enterra;
// - a frente (o lado que o jogador deve ver) olha para +X; o comprimento ao longo da pista vai em Z (paralelo à
//   pista); com side 'sea', o mar fica atrás (−X). O layout gira o modelo pelo lado da pista (+X sempre para ela);
// - tamanho livre: pontes de ~1 km em Z e montanhas de 1 × 2 km cabem — a faixa de `place` mede a BORDA DE DENTRO
//   da pegada (o ponto mais perto da pista), não o centro, e um modelo comprido gira menos (a ponta avança ≤ 60 m);
// - metros reais (skyline: já na escala grande — o layout não escala);
// - partes por material de `geom.ts`: 'flat' (uma só, fundida), 'glow' (luz: janelas e holofotes que brilham à noite;
//   de dia ficam na cor pintada), fachadas ('classic', 'house', 'office', 'apartment', com uv) para janelas que
//   acendem só à noite; sem `shadow` (o marco é longe demais para o mapa de sombra) e sem `blob`.
import type { Model } from '../geom';

export interface LandmarkDef {
  build: () => Model;
  /**
   * near: 30–80 m da pista (igreja, placa, casario, portal), tamanho real;
   * far: 120–400 m (ponte, castelo, farol no morro), tamanho real;
   * skyline: no horizonte, 400 m+ (montanha, estátua no alto do morro, vulcão), escala grande. Usa uma névoa mais
   * rala que o resto do cenário (lê de longe, como os planos do horizonte do terreno).
   */
  place: 'near' | 'far' | 'skyline';
  /** Lado: 'sea' só no lado do mar do litoral (direita), 'land' do lado de terra, 'any' qualquer. */
  side: 'land' | 'sea' | 'any';
  /** Quantas vezes aparece por volta (o primeiro sempre perto da largada). */
  perLap: number;
  /**
   * Opcional: quanto o marco gira (rad) da pista para quem vem chegando (0 = de frente para a pista, de lado para
   * quem chega). Padrão por `place` em layout.ts (LANDMARK_TURN). Ponte comprida quer 0–0,15 (a ponta não avança).
   */
  turn?: number;
}

export type LandmarkRegistry = Readonly<Record<string, LandmarkDef>>;
