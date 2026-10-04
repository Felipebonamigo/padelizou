// Leitura dos marcos da segunda leva do Norte e do Nordeste (docs/VISUAL.md, "Marcos turísticos" › "Leitura"): o dono
// correu em Foz e não reconheceu as Cataratas — o modelo estava à vista, mas lia como prédios. A revisão passou os 35
// marcos de `brasil-2-norte-nordeste.ts` pela mesma régua (folha de contato de longe e captura no jogo, no ponto de
// aproximação) e refez os que um brasileiro não reconheceria em 2 s, a 150–250 m, na névoa e sem legenda. Cada teste
// abaixo trava o que fazia o marco ilegível, medido na vista de frente (tests/front-view.ts: projeção ortográfica de
// +X, o lado do jogador, cor da face mais próxima por célula) — girada pelo giro do layout para quem chega quando é o
// que conta — e não o desenho: a fração da cor-assinatura, o tamanho da silhueta, a peça fina que some de longe, o que
// acende à noite nas pistas noturnas.
import * as THREE from 'three';
import { describe, expect, it } from 'vitest';
import type { Model } from '../src/render/scenery/geom';
import { LANDMARKS_BRASIL_2_NORTE_NORDESTE as LM } from '../src/render/scenery/landmarks/brasil-2-norte-nordeste';
import { frontView, hsl, type FrontView } from './front-view';

/** Classe de cor de uma célula (sRGB). `lit` = face de luz (`glow`), marcada antes da vista. */
type K = 'sky' | 'white' | 'gray' | 'dark' | 'red' | 'orange' | 'brown' | 'yellow' | 'green' | 'cyan' | 'blue' | 'purple' | 'lit';
function kind(c: THREE.Color | null): K {
  if (!c) return 'sky';
  if (c.r > 0.99 && c.g < 0.01 && c.b > 0.99) return 'lit';
  const { h, s, l } = hsl(c);
  if (l >= 0.85) return 'white';
  if (l < 0.16) return 'dark';
  if (s < 0.14) return l > 0.6 ? 'white' : 'gray';
  const H = h * 360;
  if (H < 15 || H >= 340) return 'red';
  if (H < 45) return l > 0.55 ? 'orange' : 'brown';
  if (H < 70) return 'yellow';
  if (H < 165) return 'green';
  if (H < 200) return 'cyan';
  if (H < 260) return 'blue';
  return 'purple';
}

const ALL_MATS = ['flat', 'glow', 'classic', 'house', 'office', 'apartment'];

/**
 * Vista de frente do marco girado `yaw` em Y (o giro do layout para quem chega: perto 0,30, longe 0,45; positivo leva a
 * frente para +Z, o lado de quem vem). `lit`: as faces de luz viram magenta puro (a classe `lit`), para medir o que
 * acende à noite; senão ficam com a cor de dia.
 */
function look(id: string, o: { yaw?: number; lit?: boolean; cell?: number; mats?: string[] } = {}): FrontView {
  const m = LM[id].build();
  const turned: Model = {
    ...m,
    parts: m.parts.map((p) => {
      const g = p.geometry.clone();
      if (o.yaw) g.rotateY(o.yaw);
      if (o.lit && p.mat === 'glow') { const c = g.getAttribute('color') as THREE.BufferAttribute; for (let i = 0; i < c.count; i++) c.setXYZ(i, 1, 0, 1); }
      return { ...p, geometry: g };
    }),
  };
  return frontView(turned, o.cell ?? 1, o.mats ?? ALL_MATS);
}

interface Census {
  /** Células da silhueta (não céu). */
  sil: number;
  /** Fração da silhueta nas classes dadas. */
  frac: (...k: K[]) => number;
  /** Largura e altura da silhueta (m). */
  w: number;
  h: number;
  minR: number; maxR: number; minC: number; maxC: number;
}
function census(fv: FrontView, r0 = 0, r1 = Infinity): Census {
  const counts = new Map<K, number>(); let sil = 0; let minR = fv.rows; let maxR = -1; let minC = fv.cols; let maxC = -1;
  for (let r = Math.max(0, r0); r < Math.min(fv.rows, r1); r++) {
    for (let q = 0; q < fv.cols; q++) {
      const k = kind(fv.at(q, r)); if (k === 'sky') continue;
      sil++; counts.set(k, (counts.get(k) ?? 0) + 1);
      minR = Math.min(minR, r); maxR = Math.max(maxR, r); minC = Math.min(minC, q); maxC = Math.max(maxC, q);
    }
  }
  return { sil, frac: (...k) => k.reduce((a, x) => a + (counts.get(x) ?? 0), 0) / Math.max(1, sil), w: (maxC - minC + 1) * fv.cell, h: (maxR - minR + 1) * fv.cell, minR, maxR, minC, maxC };
}

/** Colunas (de `c0` a `c1`) em que alguma célula entre as linhas r0 e r1 é de uma das classes. */
function colsWith(fv: FrontView, c0: number, c1: number, r0: number, r1: number, ...ks: K[]): number {
  let n = 0;
  for (let q = Math.max(0, c0); q <= Math.min(fv.cols - 1, c1); q++) {
    for (let r = Math.max(0, r0); r <= Math.min(fv.rows - 1, r1); r++) if (ks.includes(kind(fv.at(q, r)))) { n++; break; }
  }
  return n;
}

/** Células de uma classe num retângulo de colunas × linhas (inclusivo). */
function cellsIn(fv: FrontView, c0: number, c1: number, r0: number, r1: number, ...ks: K[]): number {
  let n = 0;
  for (let q = Math.max(0, c0); q <= Math.min(fv.cols - 1, c1); q++) for (let r = Math.max(0, r0); r <= Math.min(fv.rows - 1, r1); r++) if (ks.includes(kind(fv.at(q, r)))) n++;
  return n;
}

/**
 * Altura (m) da linha mais alta em que a silhueta ainda ocupa ≥ `occ` das colunas dela: a altura do "corpo" que se vê
 * de longe (o mastro e o pináculo finos não contam).
 */
function bodyHeight(fv: FrontView, occ: number): number {
  const c = census(fv);
  let best = 0;
  for (let r = c.minR; r <= c.maxR; r++) {
    let n = 0;
    for (let q = c.minC; q <= c.maxC; q++) if (kind(fv.at(q, r)) !== 'sky') n++;
    if (n >= occ * (c.maxC - c.minC + 1)) best = r;
  }
  return (best - c.minR + 1) * fv.cell;
}

/** O trecho contínuo mais comprido (células) de uma classe numa linha. */
function longestRun(fv: FrontView, r: number, ...ks: K[]): number {
  let best = 0; let n = 0;
  for (let q = 0; q < fv.cols; q++) { n = ks.includes(kind(fv.at(q, r))) ? n + 1 : 0; best = Math.max(best, n); }
  return best;
}

/** Trechos contínuos de células de uma classe numa linha. */
function runs(fv: FrontView, r: number, ...ks: K[]): number {
  let n = 0; let on = false;
  for (let q = 0; q < fv.cols; q++) { const hit = ks.includes(kind(fv.at(q, r))); if (hit && !on) n++; on = hit; }
  return n;
}

/**
 * Leitura de queda d'água (a régua das Cataratas, tests/landmarks-leitura.test.ts, copiada): fração de água na silhueta,
 * o tom de água mais comum (painel liso = um tom só), colunas do quarto de baixo com névoa/espuma e colunas com mata no topo.
 */
function readFalls(fv: FrontView): { water: number; topTone: number; footMist: number; topGreen: number } {
  const wk = (c: THREE.Color | null): 'sky' | 'water' | 'green' | 'rock' => {
    if (!c) return 'sky';
    const { h, s, l } = hsl(c);
    if (l >= 0.8 || (h > 0.48 && h < 0.64 && l > 0.68 && s < 0.8)) return 'water';
    if (h > 0.17 && h < 0.45 && s > 0.15) return 'green';
    return 'rock';
  };
  let sil = 0; let water = 0; const tones = new Map<string, number>(); let minR = fv.rows; let maxR = 0;
  for (let r = 0; r < fv.rows; r++) {
    for (let q = 0; q < fv.cols; q++) {
      const c = fv.at(q, r); const k = wk(c);
      if (k === 'sky') continue;
      sil++; minR = Math.min(minR, r); maxR = Math.max(maxR, r);
      if (k === 'water' && c) { water++; const t = c.getHexString(); tones.set(t, (tones.get(t) ?? 0) + 1); }
    }
  }
  const footTop = minR + Math.round((maxR - minR) / 4);
  let occ = 0; let mist = 0; let cols = 0; let topGreen = 0;
  for (let q = 0; q < fv.cols; q++) {
    let o = false; let m = false;
    for (let r = minR; r <= footTop; r++) { const k = wk(fv.at(q, r)); if (k !== 'sky') o = true; if (k === 'water') m = true; }
    if (o) occ++;
    if (m) mist++;
    for (let r = fv.rows - 1; r >= 0; r--) { const k = wk(fv.at(q, r)); if (k === 'sky') continue; cols++; if (k === 'green') topGreen++; break; }
  }
  return { water: water / sil, topTone: Math.max(0, ...tones.values()) / Math.max(1, water), footMist: mist / occ, topGreen: topGreen / cols };
}

describe('leitura dos marcos do Norte e do Nordeste (segunda leva)', () => {
  it('cajueiro de Pirangi: uma copa só, alta o bastante para não sumir na mata, que desce até o chão nas bordas, com cajus à vista', () => {
    // Antes: copa em placas soltas a 4–9 m, com o vão escuro dos troncos embaixo — de 87 m da pista lia como moitas.
    const fv = look('cajueiro_gigante', { yaw: 0.3 });
    const c = census(fv);
    expect.soft(c.h, 'altura da copa (m)').toBeGreaterThanOrEqual(15);
    expect.soft(c.w, 'largura da copa (m)').toBeGreaterThanOrEqual(80);
    expect.soft(c.frac('green'), 'a face é folha').toBeGreaterThanOrEqual(0.7);
    // Os galhos que tocam o chão: nas pontas da copa (o quinto de cada lado), folha a menos de 3 m do chão.
    const edge = Math.round((c.maxC - c.minC) / 5);
    const low = colsWith(fv, c.minC, c.minC + edge, c.minR, c.minR + 2, 'green') + colsWith(fv, c.maxC - edge, c.maxC, c.minR, c.minR + 2, 'green');
    expect.soft(low / (2 * (edge + 1)), 'colunas das pontas com folha junto do chão').toBeGreaterThanOrEqual(0.6);
    expect.soft(c.frac('red', 'orange', 'yellow'), 'cajus vermelhos e amarelos na face').toBeGreaterThanOrEqual(0.015);
  });

  it('manguezal do Delta do Parnaíba: os guarás vermelhos pontilham a copa inteira do mangue e voam em bando por cima', () => {
    // Antes: guarás de 0,35 m (0,2% da face, nenhum de longe) — de 80 m era uma moita verde-escura na água.
    const fv = look('manguezal_delta', { yaw: 0.3 });
    const c = census(fv);
    expect.soft(c.frac('red'), 'vermelho dos guarás na face').toBeGreaterThanOrEqual(0.06);
    let greenTop = -1;
    for (let r = 0; r < fv.rows; r++) for (let q = 0; q < fv.cols; q++) if (kind(fv.at(q, r)) === 'green') greenTop = Math.max(greenTop, r);
    expect.soft(colsWith(fv, c.minC, c.maxC, 0, fv.rows - 1, 'red') / (c.maxC - c.minC + 1), 'colunas com guará (espalhados, não um cacho)').toBeGreaterThanOrEqual(0.4);
    expect.soft(cellsIn(fv, 0, fv.cols - 1, greenTop + 1, fv.rows - 1, 'red'), 'guarás no céu acima da copa (o bando)').toBeGreaterThanOrEqual(12);
  });

  it('geoglifo do Acre: as valas do quadrado e do círculo se veem da pista (o desenho na encosta, não rente ao chão)', () => {
    // Antes: valas de 0,2 m de altura no pasto plano — da pista, a câmera a 2 m via uma linha; só a torre lia.
    const fv = look('geoglifo', { yaw: 0.3 });
    // A vala (terra marrom) desenha linhas compridas na horizontal (os lados do quadrado e os arcos do círculo); tronco e
    // torre são riscos em pé e não contam.
    const lines: number[] = [];
    for (let r = 2; r < fv.rows; r++) if (longestRun(fv, r, 'brown') >= 15) lines.push(r);
    expect.soft(lines.length, 'linhas da vista (m) com um traço de terra de 15 m ou mais, acima de 2 m').toBeGreaterThanOrEqual(4);
    expect.soft(Math.max(0, ...lines) * fv.cell, 'altura até onde o desenho sobe (m)').toBeGreaterThanOrEqual(8);
    let wide = 0;
    for (const r of lines) wide = Math.max(wide, longestRun(fv, r, 'brown') * fv.cell);
    expect.soft(wide, 'o traço mais comprido (m)').toBeGreaterThanOrEqual(45);
  });

  it('Real Forte Príncipe da Beira: a muralha de pedra alta e clara, com as guaritas brancas nas pontas dos baluartes', () => {
    // Antes: muralha de 10 m a 170 m+ da pista, pedra cinza-escura e guaritas de 2,6 m — uma faixa baixa na mata.
    const fv = look('forte_principe_beira', { yaw: 0.3 });
    const c = census(fv);
    const wall = bodyHeight(fv, 0.6);
    expect.soft(wall, 'altura da muralha que ocupa 60% da largura (m)').toBeGreaterThanOrEqual(14);
    expect.soft(c.frac('white', 'orange', 'yellow'), 'pedra clara (contra a mata)').toBeGreaterThanOrEqual(0.45);
    let guaritas = 0;
    for (let r = c.minR + wall; r < c.minR + wall + 6; r++) guaritas = Math.max(guaritas, runs(fv, r, 'white'));
    expect.soft(guaritas, 'guaritas brancas acima do parapeito').toBeGreaterThanOrEqual(3);
  });

  it('Parque do Povo (São João de Campina Grande, à noite): a Pirâmide contornada de luz e o arraial de bandeirinhas coloridas aceso', () => {
    // Antes: de noite, um telhado escuro de pavilhão com o palco aceso embaixo; as bandeirinhas de 0,35 m sumiam.
    const lit = look('parque_do_povo', { yaw: 0.3, lit: true });
    const c = census(lit);
    expect.soft(c.frac('lit'), 'fração da face acesa à noite').toBeGreaterThanOrEqual(0.2);
    expect.soft(colsWith(lit, c.minC, c.maxC, 0, lit.rows - 1, 'lit') / (c.maxC - c.minC + 1), 'colunas com luz (o arraial inteiro, não só o palco)').toBeGreaterThanOrEqual(0.6);
    const mid = c.minR + Math.floor((c.maxR - c.minR) / 2);
    expect.soft(cellsIn(lit, 0, lit.cols - 1, mid, lit.rows - 1, 'lit'), 'luz na metade de cima (o contorno da Pirâmide)').toBeGreaterThanOrEqual(30);
    const day = census(look('parque_do_povo', { yaw: 0.3 }));
    const hues = (['red', 'yellow', 'green', 'blue', 'purple', 'cyan'] as K[]).filter((k) => day.frac(k) >= 0.01);
    expect.soft(hues.length, `cores das bandeirinhas com ≥ 1% da face (${hues.join(', ')})`).toBeGreaterThanOrEqual(4);
  });

  it('ponte de Palmas (à noite): a fileira de luzes corre a ponte inteira sobre o lago', () => {
    // Antes: luminárias de 1 × 0,35 m a 220 m da pista — de noite a ponte não acendia nada que se visse.
    const lit = look('ponte_palmas', { yaw: 0.1, lit: true });
    const c = census(lit);
    expect.soft(colsWith(lit, c.minC, c.maxC, 0, lit.rows - 1, 'lit') / (c.maxC - c.minC + 1), 'colunas acesas ao longo da ponte').toBeGreaterThanOrEqual(0.8);
    expect.soft(c.frac('lit'), 'fração da face acesa').toBeGreaterThanOrEqual(0.06);
  });

  it('Pedra da Boca: a boca escura e larga no alto do domo de granito, à vista de quem chega pelos dois lados', () => {
    // Antes: a cavidade escura ocupava 3–4% da face vista de quem chega (giro do horizonte + o ângulo da aproximação) e,
    // na névoa do horizonte, sumia — de longe era só um domo cinza. Quem chega vê +X e +Z com o marco à esquerda da
    // pista e +X e −Z com ele à direita (o layout gira sem espelhar): a boca tem de ler dos dois lados.
    for (const yaw of [0.75, -0.75]) {
      const fv = look('pedra_da_boca', { yaw, cell: 2 });
      expect.soft(census(fv).frac('dark'), `fração escura (a boca) vista a ${yaw} rad`).toBeGreaterThanOrEqual(0.06);
      let wide = 0;
      for (let r = 0; r < fv.rows; r++) wide = Math.max(wide, longestRun(fv, r, 'dark') * fv.cell);
      expect.soft(wide, `largura da boca vista a ${yaw} rad (m)`).toBeGreaterThanOrEqual(60);
    }
  });

  it('Ponte dos Ingleses (à noite): o píer comprido aceso de ponta a ponta, visto de lado por quem chega, e o mirante na ponta', () => {
    // Antes: o píer saía reto para o mar (de ponta para quem vem pela orla) e acendia postes de 0,6 m: de noite, 3% das
    // colunas tinham luz — dois pontinhos no escuro.
    const lit = look('ponte_dos_ingleses', { yaw: 0.8, lit: true });
    const c = census(lit);
    expect.soft(colsWith(lit, c.minC, c.maxC, 2, lit.rows - 1, 'lit') * lit.cell, 'largura acesa acima do mar (m)').toBeGreaterThanOrEqual(60);
    expect.soft(c.frac('lit'), 'fração da face acesa').toBeGreaterThanOrEqual(0.08);
  });

  it('Marco Zero do Recife (à noite): o casario colorido do Recife Antigo iluminado e a Torre de Cristal acesa acima dele', () => {
    // Antes: sobrados de janela acesa (como os prédios da cidade em volta) e uma coluna branca de 38 m — de noite, 2% da
    // face acesa: lia como mais um quarteirão da pista de cidade.
    const lit = look('marco_zero_recife', { yaw: 0.3, lit: true });
    const c = census(lit);
    expect.soft(c.frac('lit'), 'fração da face acesa (fachadas iluminadas e a torre)').toBeGreaterThanOrEqual(0.3);
    expect.soft(colsWith(lit, c.minC, c.maxC, 0, lit.rows - 1, 'lit') / (c.maxC - c.minC + 1), 'colunas acesas').toBeGreaterThanOrEqual(0.7);
    expect.soft(c.h, 'altura da Torre de Cristal (m)').toBeGreaterThanOrEqual(44);
    const day = census(look('marco_zero_recife', { yaw: 0.3 }));
    const hues = (['red', 'orange', 'yellow', 'green', 'blue', 'purple', 'cyan'] as K[]).filter((k) => day.frac(k) >= 0.02);
    expect.soft(hues.length, `cores do casario com ≥ 2% da face (${hues.join(', ')})`).toBeGreaterThanOrEqual(4);
  });

  it('cachoeira de São Romão: a cortina larga de água domina o paredão, riscada, com a névoa no pé e a mata no alto', () => {
    // Antes: um paredão de arenito de 230 m com a queda num vão de 56 m — de frente, 20% de água, névoa em 26% do pé e
    // mata em 29% do topo: lia como um morro vermelho com uma mancha branca.
    const r = readFalls(look('cachoeira_sao_romao', { mats: ['flat', 'glow'] }));
    expect.soft(r.water, 'fração da face que é água').toBeGreaterThanOrEqual(0.4);
    expect.soft(r.topTone, 'o tom de água mais comum (cortina riscada, não painel liso)').toBeLessThanOrEqual(0.45);
    expect.soft(r.footMist, 'colunas do quarto de baixo com névoa/espuma').toBeGreaterThanOrEqual(0.6);
    expect.soft(r.topGreen, 'colunas cujo topo é mata').toBeGreaterThanOrEqual(0.6);
  });

  it('falésias de Canoa Quebrada: o paredão alto em faixas vermelhas e brancas, a lua e a estrela claras na face, as jangadas', () => {
    // Antes: estratos de ocre e laranja (63% da face) como as mesas do deserto em volta, 35 m a 280 m da pista, a lua e a
    // estrela num sulco marrom: de longe, mais uma mesa do bioma.
    const fv = look('falesias_canoa', { yaw: 0.45 });
    const c = census(fv);
    expect.soft(c.h, 'altura do paredão (m)').toBeGreaterThanOrEqual(40);
    expect.soft(c.frac('red'), 'vermelho dos estratos').toBeGreaterThanOrEqual(0.15);
    expect.soft(c.frac('white'), 'branco dos estratos, da lua e da estrela e das velas').toBeGreaterThanOrEqual(0.12);
    // A lua e a estrela: um trecho branco largo no alto da face (fora das faixas, que são finas).
    const mid = c.minR + Math.floor((c.maxR - c.minR) * 0.4);
    let tall = 0;
    for (let q = c.minC; q <= c.maxC; q++) {
      let n = 0; let best = 0;
      for (let r = mid; r <= c.maxR; r++) { n = kind(fv.at(q, r)) === 'white' ? n + 1 : 0; best = Math.max(best, n); }
      if (best >= 6) tall++;
    }
    expect.soft(tall, 'colunas com 6 m ou mais de branco seguido na metade de cima (a lua e a estrela)').toBeGreaterThanOrEqual(10);
  });

  it('ponte Maurício de Nassau (à noite): os cinco arcos desenhados em luz sobre o Capibaribe, os globos e as estátuas acesas', () => {
    // Antes: arcos escuros sobre a água escura e luminárias de 0,6 m — de noite, 1,4% da face acesa e nenhum arco.
    const lit = look('ponte_mauricio', { yaw: 0.3, lit: true });
    const c = census(lit);
    expect.soft(c.frac('lit'), 'fração da face acesa').toBeGreaterThanOrEqual(0.08);
    // Na linha a ~4 m (o meio dos arcos), cada arco aceso dá dois riscos de luz (as duas pernas).
    let best = 0;
    for (let r = c.minR + 3; r <= c.minR + 6; r++) best = Math.max(best, runs(lit, r, 'lit'));
    expect.soft(best, 'riscos de luz numa linha à meia altura dos arcos').toBeGreaterThanOrEqual(8);
    expect.soft(colsWith(lit, c.minC, c.maxC, 0, lit.rows - 1, 'lit') / (c.maxC - c.minC + 1), 'colunas acesas').toBeGreaterThanOrEqual(0.5);
  });

  it('casario de azulejos de São Luís (à noite): as fachadas de azulejo iluminadas, em xadrez de duas cores', () => {
    // Antes: sobrados de cor lisa com a textura de janelas que só acende à noite — a cor do azulejo sumia no escuro e o
    // casario lia como mais uma fileira de prédios da cidade (0,3% da face acesa).
    const lit = look('casario_azulejos', { yaw: 0.3, lit: true });
    expect.soft(census(lit).frac('lit'), 'fração da face acesa (as fachadas iluminadas)').toBeGreaterThanOrEqual(0.35);
    // O azulejo: na linha a ~2,5 m (o térreo), a cor troca a cada ladrilho (xadrez), não uma cor lisa por sobrado.
    const day = look('casario_azulejos', { yaw: 0.3 });
    const c = census(day);
    let changes = 0;
    for (const r of [c.minR + 2, c.minR + 3]) {
      let prev: K = 'sky';
      for (let q = c.minC; q <= c.maxC; q++) { const k = kind(day.at(q, r)); if (k !== 'sky' && prev !== 'sky' && k !== prev) changes++; prev = k; }
    }
    expect.soft(changes / 2, 'trocas de cor por linha no térreo (o xadrez do azulejo)').toBeGreaterThanOrEqual(30);
  });

  it('praia de rio de Alter do Chão: a faixa de areia branca na frente, com as barracas e os guarda-sóis, e a serra atrás', () => {
    // Antes: o banco de areia tinha 2 m e ficava atrás da linha d'água — de frente, a serra (olivácea) era 86% da face e a
    // areia, uma lasca: lia como um morro na mata.
    const fv = look('praia_de_rio', { yaw: 0.3 });
    const c = census(fv);
    expect.soft(c.frac('white'), 'areia branca na face').toBeGreaterThanOrEqual(0.18);
    expect.soft(colsWith(fv, c.minC, c.maxC, 0, fv.rows - 1, 'white') / (c.maxC - c.minC + 1), 'a praia corre a frente (colunas com areia branca)').toBeGreaterThanOrEqual(0.6);
    expect.soft(c.frac('red', 'blue'), 'guarda-sóis e barcos coloridos').toBeGreaterThanOrEqual(0.01);
  });

  it('búfalos do Marajó: a manada escura no alagado, alta o bastante para ler a 70 m da pista', () => {
    // Antes: búfalos de 2,3 m de altura (× 1,7) num campo de 90 m — de longe, uma faixa escura de duas linhas. O couro
    // conta como escuro ou cinza (a peça baixada é o boi recolorido de ardósia).
    const fv = look('bufalo', { yaw: 0.3 });
    let top = -1;
    for (let r = 0; r < fv.rows; r++) {
      let n = 0;
      for (let q = 0; q < fv.cols; q++) if (['dark', 'gray'].includes(kind(fv.at(q, r)))) n++;
      if (n >= 12) top = r;
    }
    expect.soft((top + 1) * fv.cell, 'altura até onde a manada escura ocupa 12 m ou mais da linha (m)').toBeGreaterThanOrEqual(4);
    expect.soft(census(fv).frac('dark', 'gray'), 'o escuro dos búfalos na face').toBeGreaterThanOrEqual(0.25);
  });

  it('palafitas: as casas coloridas no alto das estacas, com o vão de água embaixo', () => {
    // Antes: o assoalho a 2,6 m — de frente, a parede começava logo acima da passarela e as estacas não liam.
    const fv = look('palafita', { yaw: 0.3 });
    let floor = Infinity;
    for (let r = 0; r < fv.rows && floor === Infinity; r++) for (let q = 0; q < fv.cols; q++) if (['red', 'blue', 'green', 'orange', 'yellow', 'purple'].includes(kind(fv.at(q, r)))) { floor = r; break; }
    expect.soft(floor * fv.cell, 'altura da parede mais baixa das casas (m)').toBeGreaterThanOrEqual(4);
    expect.soft(census(fv).frac('red', 'blue', 'green', 'orange', 'yellow', 'purple'), 'cor das casas na face').toBeGreaterThanOrEqual(0.3);
  });

  it('Vila Serra do Navio: a fileira de casas modernistas brancas sobre pilotis, de frente para a pista, e a caixa-d’água alta', () => {
    // Antes: casas térreas de 3 m atrás de uma fileira de árvores (a mata era metade da face) — lia como sítio na mata.
    const fv = look('vila_serra_do_navio', { yaw: 0.3 });
    const c = census(fv);
    expect.soft(c.frac('green'), 'mata na face (não pode esconder a vila)').toBeLessThanOrEqual(0.3);
    let rows = 0;
    for (let r = 0; r < fv.rows; r++) { let n = 0; for (let q = 0; q < fv.cols; q++) if (kind(fv.at(q, r)) === 'white') n++; if (n >= 30) rows++; }
    expect.soft(rows, 'linhas (m) em que o branco das casas ocupa 30 m ou mais').toBeGreaterThanOrEqual(4);
    expect.soft(c.h, 'altura da caixa-d’água (m)').toBeGreaterThanOrEqual(28);
  });

  it('Monumento ao Garimpeiro (à noite): a estátua grande acesa pelos holofotes sobre o pedestal, no espelho d’água iluminado', () => {
    // Antes: a estátua de ~9 m em bronze escuro e quatro holofotes de 0,6 m no pé — de noite, uma sombra (1,4% aceso).
    const lit = look('monumento_garimpeiro', { yaw: 0.3, lit: true });
    const c = census(lit);
    expect.soft(c.frac('lit'), 'fração da face acesa (a estátua e o pedestal iluminados)').toBeGreaterThanOrEqual(0.35);
    const mid = c.minR + Math.floor((c.maxR - c.minR) / 2);
    expect.soft(cellsIn(lit, 0, lit.cols - 1, mid, lit.rows - 1, 'lit'), 'luz na metade de cima (a figura do garimpeiro)').toBeGreaterThanOrEqual(25);
    expect.soft(bodyHeight(look('monumento_garimpeiro', { yaw: 0.3, mats: ['flat', 'glow'] }), 0.08), 'altura do monumento que ocupa 8% da largura (m)').toBeGreaterThanOrEqual(15);
  });

  it('catamarã do Xingó: o barco branco de toldo vermelho grande na frente do paredão vermelho', () => {
    // Antes: o catamarã de 18 m era 3,5% da face — de 75 m da pista lia só o paredão, sem o passeio de barco.
    const c = census(look('catamara', { yaw: 0.3 }));
    expect.soft(c.frac('white'), 'o branco do catamarã').toBeGreaterThanOrEqual(0.1);
    expect.soft(c.frac('red'), 'o toldo vermelho').toBeGreaterThanOrEqual(0.015);
  });

  it('Quadrado de Trancoso: a igrejinha branca alta no fim do gramado, acima do casario colorido', () => {
    // Antes: a igreja em escala 0,85 (22 m com a torre) — de 57 m da pista, do tamanho das casinhas e das palmeiras.
    const c = census(look('igreja_quadrado', { yaw: 0.3 }));
    expect.soft(c.h, 'altura da igreja com a torre (m)').toBeGreaterThanOrEqual(28);
    expect.soft(c.frac('white'), 'o branco da igreja na face').toBeGreaterThanOrEqual(0.3);
    const hues = (['red', 'orange', 'yellow', 'blue', 'cyan', 'purple'] as K[]).filter((k) => c.frac(k) >= 0.015);
    expect.soft(hues.length, `cores do casario com ≥ 1,5% da face (${hues.join(', ')})`).toBeGreaterThanOrEqual(4);
  });

  it('Praça São Francisco (São Cristóvão): a fachada branca da igreja franciscana e o convento de arcadas dominam a praça', () => {
    // Antes: igreja e convento creme sob o sombreado (lidos como ocre, 20% de branco) e do tamanho dos sobrados.
    const c = census(look('praca_sao_francisco', { yaw: 0.3 }));
    expect.soft(c.frac('white'), 'o branco da igreja e do convento na face').toBeGreaterThanOrEqual(0.35);
    expect.soft(c.h, 'altura da torre (m)').toBeGreaterThanOrEqual(30);
  });

  it('Pedras de Sete Cidades: torres de arenito maciças (a "cidade" em ruínas) e o Arco do Triunfo vazado', () => {
    // Antes: pilhas de discos achatados com o céu entre eles (lia como pratos empilhados) e o arco no canto, pequeno.
    const yaw = 0.45;
    const fv = look('pedras_sete_cidades', { yaw });
    const c = census(fv);
    // Céu preso dentro da silhueta (pedra em cima e embaixo na mesma coluna), no arco e fora dele.
    const archZ = -4 * Math.sin(yaw) + 61 * Math.cos(yaw);
    const a0 = Math.floor((archZ - 14 - fv.z0) / fv.cell); const a1 = Math.floor((archZ + 14 - fv.z0) / fv.cell);
    let inArch = 0; let elsewhere = 0;
    for (let q = 0; q < fv.cols; q++) {
      let lo = -1; let hi = -1;
      for (let r = 0; r < fv.rows; r++) if (kind(fv.at(q, r)) !== 'sky') { if (lo < 0) lo = r; hi = r; }
      for (let r = lo + 1; r < hi; r++) if (kind(fv.at(q, r)) === 'sky') { if (q >= a0 && q <= a1) inArch++; else elsewhere++; }
    }
    expect.soft(inArch, 'céu sob a laje do Arco do Triunfo (células)').toBeGreaterThanOrEqual(40);
    expect.soft(elsewhere / c.sil, 'céu preso entre as camadas das torres (pilhas de discos)').toBeLessThanOrEqual(0.03);
    expect.soft(c.h, 'altura da torre mais alta (m)').toBeGreaterThanOrEqual(46);
  });

  it('Palácio Araguaia (à noite): a colunata branca banhada de luz, a cúpula acesa e a escadaria, não um prédio de escritórios', () => {
    // Antes: de noite, o bloco de janelas acesas (a textura de escritório) com colunas finas escuras na frente — lia como
    // mais um prédio da pista de cidade (7% aceso, nenhuma coluna).
    const lit = look('palacio_araguaia', { yaw: 0.3, lit: true });
    const c = census(lit);
    expect.soft(c.frac('lit'), 'fração da face acesa (a colunata e a cúpula iluminadas)').toBeGreaterThanOrEqual(0.3);
    let cols = 0;
    for (let r = c.minR + 5; r <= c.minR + 12; r++) cols = Math.max(cols, runs(lit, r, 'lit'));
    expect.soft(cols, 'colunas acesas contadas numa linha à meia altura').toBeGreaterThanOrEqual(10);
  });

  it('dunas de Piaçabuçu: dunas altas de areia clara (não o ocre do deserto em volta), com a lagoa e o coqueiral no pé', () => {
    // Antes: 98% da face no mesmo tom de areia do chão do bioma (deserto) e 36 m — de longe, um morrote do próprio chão.
    const c = census(look('dunas_piacabucu', { yaw: 0.45 }));
    expect.soft(c.frac('white'), 'areia clara (branca) das dunas').toBeGreaterThanOrEqual(0.4);
    expect.soft(c.h, 'altura do cordão de dunas (m)').toBeGreaterThanOrEqual(42);
    expect.soft(c.frac('green'), 'o verde do coqueiral e da restinga no pé').toBeGreaterThanOrEqual(0.02);
  });

  it('cânion do Xingó: o paredão alto de arenito vermelho-vivo (não o marrom das mesas do deserto em volta)', () => {
    // Antes: estratos marrom-ferrugem (83% da face na classe marrom) de 60 m a 220 m da pista — na névoa do deserto,
    // um paredão pálido igual às mesas do fundo do bioma.
    const c = census(look('canion_xingo', { yaw: 0.45, mats: ['flat', 'glow'] }));
    expect.soft(c.frac('red', 'orange'), 'vermelho e laranja vivos do arenito').toBeGreaterThanOrEqual(0.45);
    expect.soft(c.h, 'altura do paredão (m)').toBeGreaterThanOrEqual(70);
  });
});
