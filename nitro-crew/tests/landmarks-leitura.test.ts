// Leitura dos marcos (docs/VISUAL.md, "Marcos turísticos" › "Leitura"): defeito relatado pelo dono em 04/10/2026 —
// correu em Foz do Iguaçu e "não achou as cataratas". O modelo estava lá, à vista, mas de frente era uma parede de
// rocha em blocos com painéis brancos retos e a torre do elevador: lia como prédios. A vista de frente (o lado que o
// jogador vê, tests/front-view.ts) mede o que faz uma queda d'água ser reconhecida de longe: a água domina a face, em
// cortinas com riscos (não painel liso de um tom só), a névoa branca cobre o pé, e a borda de cima é mata.
import { describe, expect, it } from 'vitest';
import { LANDMARKS } from '../src/render/scenery/landmarks';
import { frontView, hsl, type FrontView } from './front-view';
import type * as THREE from 'three';

type Kind = 'sky' | 'water' | 'green' | 'rock';
function kind(c: THREE.Color | null): Kind {
  if (!c) return 'sky';
  const { h, s, l } = hsl(c);
  if (l >= 0.8 || (h > 0.48 && h < 0.64 && l > 0.68 && s < 0.8)) return 'water';
  if (h > 0.17 && h < 0.45 && s > 0.15) return 'green';
  return 'rock';
}

interface Reading { water: number; topTone: number; footMist: number; topGreen: number }
function read(fv: FrontView): Reading {
  let sil = 0; let water = 0; const tones = new Map<string, number>();
  let minR = fv.rows; let maxR = 0;
  for (let r = 0; r < fv.rows; r++) {
    for (let q = 0; q < fv.cols; q++) {
      const c = fv.at(q, r); const k = kind(c);
      if (k === 'sky') continue;
      sil++; minR = Math.min(minR, r); maxR = Math.max(maxR, r);
      if (k === 'water' && c) { water++; const t = c.getHexString(); tones.set(t, (tones.get(t) ?? 0) + 1); }
    }
  }
  const footTop = minR + Math.round((maxR - minR) / 4);
  let occ = 0; let mist = 0; let cols = 0; let topGreen = 0;
  for (let q = 0; q < fv.cols; q++) {
    let o = false; let m = false;
    for (let r = minR; r <= footTop; r++) { const k = kind(fv.at(q, r)); if (k !== 'sky') o = true; if (k === 'water') m = true; }
    if (o) occ++;
    if (m) mist++;
    for (let r = fv.rows - 1; r >= 0; r--) {
      const k = kind(fv.at(q, r));
      if (k === 'sky') continue;
      cols++; if (k === 'green') topGreen++;
      break;
    }
  }
  return { water: water / sil, topTone: Math.max(...tones.values()) / water, footMist: mist / occ, topGreen: topGreen / cols };
}

describe('leitura dos marcos: quedas d\'água', () => {
  it('Cataratas do Iguaçu: de frente, a água domina, em cortinas riscadas, com névoa no pé e mata no alto', () => {
    const r = read(frontView(LANDMARKS.cataratas_iguacu.build(), 1));
    expect(r.water, 'fração da face que é água').toBeGreaterThanOrEqual(0.45);
    expect(r.topTone, 'o tom de água mais comum (painel liso = um tom só)').toBeLessThanOrEqual(0.45);
    expect(r.footMist, 'colunas do quarto de baixo com névoa/espuma').toBeGreaterThanOrEqual(0.75);
    expect(r.topGreen, 'colunas cujo topo é mata (torre ou prédio no alto = lê como cidade)').toBeGreaterThanOrEqual(0.9);
  });
});
