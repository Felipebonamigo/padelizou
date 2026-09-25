// Divisão da tela entre jogadores locais. Funções puras (sem DOM) para os testes e para a
// sessão poder posicionar coisas por cima (pausa, avisos) sem perguntar ao renderizador.

export interface Rect { x: number; y: number; w: number; h: number }

/** Espessura da borda desenhada por cima das emendas entre viewports (px de dispositivo). */
export const VIEWPORT_SEAM = 2;

/**
 * Retângulos dos viewports, um por jogador, em ordem de assento. Cobrem a tela sem sobreposição:
 * 1 → tela cheia; 2 → em cima/embaixo com largura total (como no Top Gear); 3 e 4 → grade 2×2.
 * Com 3 jogadores a 4ª célula (inferior direita) fica livre: ver `spareCell`.
 * As metades usam `floor` na primeira e o resto na segunda, para os inteiros somarem exato.
 */
export function viewportRects(count: number, width: number, height: number): Rect[] {
  const n = Math.max(1, Math.min(4, Math.floor(count)));
  const w = Math.max(0, Math.floor(width));
  const h = Math.max(0, Math.floor(height));
  if (n === 1) return [{ x: 0, y: 0, w, h }];
  const halfH = Math.floor(h / 2);
  if (n === 2) {
    return [
      { x: 0, y: 0, w, h: halfH },
      { x: 0, y: halfH, w, h: h - halfH },
    ];
  }
  const halfW = Math.floor(w / 2);
  const cells: Rect[] = [
    { x: 0, y: 0, w: halfW, h: halfH },
    { x: halfW, y: 0, w: w - halfW, h: halfH },
    { x: 0, y: halfH, w: halfW, h: h - halfH },
    { x: halfW, y: halfH, w: w - halfW, h: h - halfH },
  ];
  return cells.slice(0, n);
}

/** Célula sobrando na grade 2×2 quando há 3 jogadores (painel de classificação + minimapa); null nos outros casos. */
export function spareCell(count: number, width: number, height: number): Rect | null {
  if (Math.floor(count) !== 3) return null;
  const w = Math.max(0, Math.floor(width));
  const h = Math.max(0, Math.floor(height));
  const halfW = Math.floor(w / 2);
  const halfH = Math.floor(h / 2);
  return { x: halfW, y: halfH, w: w - halfW, h: h - halfH };
}

/** Escala da interface por viewport: 1 numa célula de 540 px de altura (grade 2×2 em 1080p). */
export function uiScale(rect: Rect): number {
  const byHeight = rect.h / 540;
  const byWidth = rect.w / 700;
  return Math.max(0.45, Math.min(3, Math.min(byHeight, byWidth)));
}
