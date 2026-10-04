// Leitura dos marcos do Norte e do Nordeste (docs/VISUAL.md, "Marcos turísticos" › "Leitura"), a revisão pedida pelo
// dono em 04/10/2026: "as coisas de cada cidade mais nítidas e as referências bonitas e entendíveis". A régua é a das
// Cataratas (tests/landmarks-leitura.test.ts): a vista de frente (tests/front-view.ts, o lado que o jogador vê, cor por
// célula de 1 m) mede o que fazia cada marco ser lido como outra coisa — a escarpa do Elevador Lacerda que era uma
// placa verde lisa, os fortes que de longe eram um muro baixo, a Pedra Furada cujo furo fechava para quem chega de
// lado, as pinturas rupestres do tamanho de um palmo numa caixa de pedra, as lagoas dos Lençóis deitadas no chão
// (invisíveis da pista), a falésia do Cabo Branco em bolo de caixas, a jangada do tamanho (e da cor) do veleiro do
// cenário. Trava o que faz ler, não como o modelo é feito.
import * as THREE from 'three';
import { describe, expect, it } from 'vitest';
import type { Model } from '../src/render/scenery/geom';
import { LANDMARKS_BRASIL_NORTE_NORDESTE as LM } from '../src/render/scenery/landmarks/brasil-norte-nordeste';
import { frontView, hsl, type FrontView } from './front-view';

/** As partes que pintam o modelo de dia, fachadas incluídas (o casario, a torre do elevador). */
const MATS = ['flat', 'glow', 'classic', 'house', 'office', 'apartment'] as const;

/**
 * O modelo visto de outro ângulo pela mesma vista de frente: `az` gira em torno de Y (quem chega de lado vê a frente a
 * `az` rad da normal, para +Z). Cópia: o modelo do registro não muda.
 */
function turned(m: Model, az: number): Model {
  const rot = new THREE.Matrix4().makeRotationY(az);
  return { ...m, parts: m.parts.map((p) => ({ ...p, geometry: p.geometry.clone().applyMatrix4(rot) })) };
}

function view(id: string, o: { az?: number } = {}): FrontView {
  return frontView(turned(LM[id].build(), o.az ?? 0), 1, MATS);
}

interface Sil { tops: number[]; minC: number; maxC: number; minR: number; maxR: number; cells: number }
/** Silhueta: o topo de cada coluna (−1 = céu), a caixa e a área (células). */
function sil(fv: FrontView): Sil {
  const tops: number[] = []; let minC = fv.cols; let maxC = -1; let minR = fv.rows; let maxR = -1; let cells = 0;
  for (let q = 0; q < fv.cols; q++) {
    let top = -1;
    for (let r = 0; r < fv.rows; r++) if (fv.at(q, r)) { top = r; cells++; minR = Math.min(minR, r); maxR = Math.max(maxR, r); }
    tops.push(top);
    if (top >= 0) { minC = Math.min(minC, q); maxC = Math.max(maxC, q); }
  }
  return { tops, minC, maxC, minR, maxR, cells };
}

const median = (v: number[]): number => { const s = [...v].sort((a, b) => a - b); return s[Math.floor(s.length / 2)]; };
const isRed = (c: THREE.Color): boolean => { const { h, s, l } = hsl(c); return (h < 0.045 || h > 0.955) && s > 0.4 && l > 0.2 && l < 0.62; };
const isWater = (c: THREE.Color): boolean => { const { h, s, l } = hsl(c); return h > 0.45 && h < 0.62 && s > 0.35 && l > 0.3 && l < 0.8; };
const isLight = (c: THREE.Color): boolean => hsl(c).l >= 0.75;
const differ = (a: THREE.Color, b: THREE.Color): boolean => Math.abs(a.r - b.r) + Math.abs(a.g - b.g) + Math.abs(a.b - b.b) > 0.06;

/** Trocas de cor ao longo da linha `r` entre as colunas c0..c1 (só entre células cheias): a face varia ou é placa. */
function changesAlong(fv: FrontView, r: number, c0: number, c1: number): { changes: number; span: number } {
  let changes = 0; let span = 0;
  for (let q = c0; q < c1; q++) {
    const a = fv.at(q, r); const b = fv.at(q + 1, r);
    if (!a || !b) continue;
    span++;
    if (differ(a, b)) changes++;
  }
  return { changes, span };
}

/** Componentes conexos (4-vizinhos) das células que passam em `pred`: tamanhos, do maior para o menor. */
function blobs(fv: FrontView, pred: (c: THREE.Color) => boolean): number[] {
  const seen = new Uint8Array(fv.cols * fv.rows); const out: number[] = [];
  for (let i = 0; i < seen.length; i++) {
    const c0 = fv.cells[i];
    if (seen[i] || !c0 || !pred(c0)) continue;
    let n = 0; const stack = [i]; seen[i] = 1;
    while (stack.length) {
      const k = stack.pop() as number; n++;
      const q = k % fv.cols; const r = (k - q) / fv.cols;
      for (const [dq, dr] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
        const qq = q + dq; const rr = r + dr;
        if (qq < 0 || rr < 0 || qq >= fv.cols || rr >= fv.rows) continue;
        const j = rr * fv.cols + qq; const c = fv.cells[j];
        if (!seen[j] && c && pred(c)) { seen[j] = 1; stack.push(j); }
      }
    }
    out.push(n);
  }
  return out.sort((a, b) => b - a);
}

/**
 * Céu cercado de pedra (o furo do arco): células vazias que não chegam por céu à borda de cima nem às dos lados — o
 * chão fecha o pé (o arco de Jeri abre até a areia).
 */
function enclosedSky(fv: FrontView): number {
  const out = new Uint8Array(fv.cols * fv.rows); const stack: number[] = [];
  const push = (q: number, r: number): void => { const k = r * fv.cols + q; if (!out[k] && !fv.cells[k]) { out[k] = 1; stack.push(k); } };
  for (let q = 0; q < fv.cols; q++) push(q, fv.rows - 1);
  for (let r = 0; r < fv.rows; r++) { push(0, r); push(fv.cols - 1, r); }
  while (stack.length) {
    const k = stack.pop() as number; const q = k % fv.cols; const r = (k - q) / fv.cols;
    if (q > 0) push(q - 1, r); if (q < fv.cols - 1) push(q + 1, r); if (r > 0) push(q, r - 1); if (r < fv.rows - 1) push(q, r + 1);
  }
  let n = 0;
  for (let k = 0; k < out.length; k++) if (!out[k] && !fv.cells[k]) n++;
  return n;
}

/**
 * Picos claros acima do muro (as guaritas dos fortes): trechos de ≥ 2 colunas cujo topo passa o muro (a mediana dos
 * topos) em ≥ 2,5 m e é claro.
 */
function lightTurrets(fv: FrontView): number {
  const s = sil(fv);
  const wall = median(s.tops.filter((t) => t >= 0));
  let runs = 0; let run = 0;
  for (let q = 0; q <= fv.cols; q++) {
    const t = q < fv.cols ? s.tops[q] : -1;
    let up = false;
    if (t >= wall + 2.5) for (let r = t; r >= wall + 2.5; r--) { const c = fv.at(q, r); if (c && isLight(c)) { up = true; break; } }
    if (up) run++;
    else { if (run >= 2) runs++; run = 0; }
  }
  return runs;
}

describe('leitura dos marcos do Norte e do Nordeste (1)', () => {
  it('Elevador Lacerda: a torre sobe acima de tudo, e a escarpa atrás é encosta (não uma placa lisa)', () => {
    const fv = view('elevador_lacerda');
    const s = sil(fv);
    const top = Math.max(...s.tops);
    // A torre: as colunas em volta do pico que passam de 85% dele (o poço e a casa de máquinas); o resto, 2 m além.
    let t0 = s.tops.indexOf(top); let t1 = t0;
    while (t0 > 0 && s.tops[t0 - 1] >= top * 0.85) t0--;
    while (t1 < fv.cols - 1 && s.tops[t1 + 1] >= top * 0.85) t1++;
    expect(t1 - t0 + 1, 'largura da torre (m): é torre, não bloco').toBeLessThanOrEqual(25);
    const rest = s.tops.filter((t, q) => t >= 0 && (q < t0 - 2 || q > t1 + 2));
    expect(top - Math.max(...rest), 'quanto a torre passa do resto (m)').toBeGreaterThanOrEqual(15);
    // Uma linha a 40% da altura da escarpa, fora da torre: a encosta muda de tom (sulcos, mata, rocha) a cada ≤ 12 m.
    const r = Math.round(median(rest) * 0.4);
    let changes = 0; let span = 0;
    for (const [c0, c1] of [[s.minC, t0 - 3], [t1 + 3, s.maxC]]) { const a = changesAlong(fv, r, c0, c1); changes += a.changes; span += a.span; }
    expect(span / Math.max(1, changes), 'metros de encosta por troca de tom').toBeLessThanOrEqual(12);
  });

  for (const id of ['forte_reis_magos', 'fortaleza_macapa']) {
    it(`${id}: guaritas claras sobre a muralha e muralha alta o bastante para não ser uma faixa no chão`, () => {
      const fv = view(id);
      expect(lightTurrets(fv), 'guaritas (picos claros ≥ 2,5 m acima do muro)').toBeGreaterThanOrEqual(3);
      const s = sil(fv);
      expect(median(s.tops.filter((t) => t >= 0)) / (s.maxC - s.minC + 1), 'altura do muro / largura').toBeGreaterThanOrEqual(0.12);
    });
  }

  it('Pedra Furada (Serra da Capivara): o furo se vê de frente e de quem chega de lado, e a pedra tem manchas verticais', () => {
    expect(enclosedSky(view('pedra_furada_capivara')), 'furo de frente (m²)').toBeGreaterThanOrEqual(250);
    for (const az of [-0.6, 0.6]) expect(enclosedSky(view('pedra_furada_capivara', { az })), `furo a ${az} rad (m²)`).toBeGreaterThanOrEqual(120);
    const fv = view('pedra_furada_capivara');
    const s = sil(fv);
    const a = changesAlong(fv, Math.round(s.maxR * 0.3), s.minC, s.maxC);
    expect(a.span / Math.max(1, a.changes), 'metros de paredão por troca de tom (faixa de caixa = nenhuma)').toBeLessThanOrEqual(12);
  });

  it('Pinturas rupestres: figuras vermelhas grandes na parede clara', () => {
    const fv = view('pinturas_rupestres');
    const s = sil(fv);
    let red = 0;
    for (const c of fv.cells) if (c && isRed(c)) red++;
    expect(red / s.cells, 'fração vermelho-ocre da face').toBeGreaterThanOrEqual(0.08);
    expect(blobs(fv, isRed)[0] ?? 0, 'a maior figura (m²)').toBeGreaterThanOrEqual(10);
  });

  it('Lençóis Maranhenses: da pista (de frente, sem olhar de cima) a água azul aparece entre as dunas brancas', () => {
    const fv = view('lagoas_lencois');
    const s = sil(fv);
    let water = 0; let light = 0;
    for (const c of fv.cells) if (c) { if (isWater(c)) water++; else if (isLight(c)) light++; }
    expect(water / s.cells, 'fração de lagoa na vista da pista').toBeGreaterThanOrEqual(0.15);
    expect(light / s.cells, 'fração de duna branca').toBeGreaterThanOrEqual(0.45);
    expect(blobs(fv, isWater).filter((n) => n >= 20).length, 'lagoas separadas').toBeGreaterThanOrEqual(3);
  });

  it('Farol do Cabo Branco: a falésia é paredão de sedimento (não bolo de caixas) e a torre sobe dela', () => {
    const fv = view('farol_cabo_branco');
    const s = sil(fv);
    // A falésia: as colunas fora da torre; a linha a meia altura dela.
    const cliffTop = median(s.tops.filter((t) => t >= 0));
    const a = changesAlong(fv, Math.round(cliffTop * 0.5), s.minC, s.maxC);
    expect(a.span / Math.max(1, a.changes), 'metros de falésia por troca de tom').toBeLessThanOrEqual(12);
    expect(Math.max(...s.tops) - cliffTop, 'a torre acima da borda (m)').toBeGreaterThanOrEqual(24);
  });

  it('Jangada: maior que o veleiro do cenário (10,4 m) e de velas coloridas (o veleiro é branco)', () => {
    const fv = view('jangada');
    const s = sil(fv);
    expect(s.maxR + 1, 'altura (m)').toBeGreaterThanOrEqual(13);
    let sail = 0; let colored = 0;
    for (let r = 3; r < fv.rows; r++) for (let q = 0; q < fv.cols; q++) {
      const c = fv.at(q, r);
      if (!c) continue;
      sail++;
      const { s: sat, l } = hsl(c);
      if (sat >= 0.45 && l >= 0.35 && l <= 0.8) colored++;
    }
    expect(colored / sail, 'fração colorida das velas').toBeGreaterThanOrEqual(0.5);
  });

  it('Coqueiral: um bosque alto e cheio (a copa corrida acima dos coqueiros de 7–9 m do cenário), não uns paus soltos', () => {
    const fv = view('coqueiral');
    const s = sil(fv);
    expect(s.maxR + 1, 'altura (m)').toBeGreaterThanOrEqual(24);
    const cols = s.tops.filter((t) => t >= 0);
    expect(cols.filter((t) => t >= 15).length / cols.length, 'colunas com copa acima de 15 m').toBeGreaterThanOrEqual(0.6);
    expect(s.cells / ((s.maxC - s.minC + 1) * (s.maxR - s.minR + 1)), 'enchimento da silhueta').toBeGreaterThanOrEqual(0.3);
  });

  it('Bonecos de Olinda: gigantes de verdade (≥ 9,5 m, a cabeçorra acima da multidão) e de roupa colorida', () => {
    const fv = view('bonecos_olinda');
    const s = sil(fv);
    expect(s.maxR + 1, 'altura (m)').toBeGreaterThanOrEqual(9.5);
    let colored = 0;
    for (const c of fv.cells) if (c) { const { s: sat, l } = hsl(c); if (sat >= 0.45 && l >= 0.3 && l <= 0.8) colored++; }
    expect(colored / s.cells, 'fração saturada (as roupas, as sombrinhas)').toBeGreaterThanOrEqual(0.4);
  });

  it('Pedra Furada de Jeri: o furo do arco se vê de quem chega (a ~1 rad da frente, dos dois lados), não só de frente', () => {
    expect(enclosedSky(view('pedra_furada_jeri')), 'furo de frente (m²)').toBeGreaterThanOrEqual(150);
    for (const az of [-1, 1]) expect(enclosedSky(view('pedra_furada_jeri', { az })), `furo a ${az} rad (m²)`).toBeGreaterThanOrEqual(25);
  });

  it('Marco Zero do Equador: o furo do relógio de sol no alto do obelisco se vê de frente e de quem chega', () => {
    expect(enclosedSky(view('marco_zero_equador')), 'furo de frente (m²)').toBeGreaterThanOrEqual(18);
    for (const az of [-1, 1]) expect(enclosedSky(view('marco_zero_equador', { az })), `furo a ${az} rad (m²)`).toBeGreaterThanOrEqual(4);
  });

  it('Duna do Pôr do Sol: areia clara e dourada que salta do verde e dos morros (não um morro marrom na névoa)', () => {
    const fv = view('duna_por_do_sol');
    let n = 0; let sand = 0; let light = 0;
    for (const c of fv.cells) {
      if (!c) continue;
      n++;
      const { h, l } = hsl(c);
      light += l;
      if (h > 0.08 && h < 0.16 && l >= 0.7) sand++;
    }
    expect(light / n, 'claridade média da silhueta').toBeGreaterThanOrEqual(0.74);
    expect(sand / n, 'fração de areia clara').toBeGreaterThanOrEqual(0.75);
  });

  it("Caixas-d'água de Porto Velho: as pernas treliçadas não somem de longe (os tanques não flutuam)", () => {
    const fv = view('caixas_dagua');
    const runs = (r: number): number => {
      let n = 0; let inRun = false;
      for (let q = 0; q < fv.cols; q++) { const f = !!fv.at(q, r); if (f && !inRun) n++; inRun = f; }
      return n;
    };
    // Do chão até 55% da altura (abaixo dos tanques): em quase toda linha, as três torres aparecem (≥ 3 trechos).
    const top = Math.floor((sil(fv).maxR + 1) * 0.55);
    let ok = 0;
    for (let r = 1; r < top; r++) if (runs(r) >= 3) ok++;
    expect(ok / (top - 1), 'linhas com as três torres, do chão aos tanques').toBeGreaterThanOrEqual(0.8);
  });

  // Marcos de perto pequenos demais para a pista: a 150 m, 6 m de altura são ~2° do quadro (~20 px numa tela de 720
  // linhas) — a locomotiva da Madeira-Mamoré, o barco regional e a maloca sumiam entre a cerca, as moitas e as árvores.
  it('Locomotiva da Madeira-Mamoré: maria-fumaça do tamanho de quem passa a 100 km/h (≥ 9 m com a fumaça, ≥ 24 m de comprido)', () => {
    const s = sil(view('locomotiva_mamore'));
    expect(s.maxR + 1, 'altura (m)').toBeGreaterThanOrEqual(9);
    expect(s.maxC - s.minC + 1, 'comprimento da silhueta (m)').toBeGreaterThanOrEqual(24);
  });

  it('Barco regional: o gaiola de dois conveses grande o bastante para ler da pista (≥ 14 m, ≥ 30 m)', () => {
    const s = sil(view('barco_regional'));
    expect(s.maxR + 1, 'altura (m)').toBeGreaterThanOrEqual(14);
    expect(s.maxC - s.minC + 1, 'comprimento da silhueta (m)').toBeGreaterThanOrEqual(30);
  });

  it('Palácio Rio Branco: o palácio neoclássico com a bandeira do Acre (verde e amarela, a estrela vermelha) que se vê da pista', () => {
    const fv = view('palacio_rio_branco');
    let flag = 0;
    for (const c of fv.cells) {
      if (!c) continue;
      const { h, s, l } = hsl(c);
      if ((h > 0.11 && h < 0.18 && s >= 0.6 && l >= 0.4 && l <= 0.75) || (h > 0.25 && h < 0.45 && s >= 0.4 && l >= 0.2 && l <= 0.6)) flag++;
    }
    expect(flag, 'bandeira verde e amarela (m²)').toBeGreaterThanOrEqual(25);
    expect(blobs(fv, isRed)[0] ?? 0, 'a estrela vermelha (m²)').toBeGreaterThanOrEqual(1);
  });

  it('Teatro Amazonas: a cúpula de azulejos verde, amarela e azul é grande (≥ 22 m) e sobe bem acima do corpo', () => {
    const fv = view('teatro_amazonas');
    // Azulejos da cúpula: verde, amarelo e azul saturados.
    const tile = (c: THREE.Color): boolean => { const { h, s, l } = hsl(c); return s >= 0.45 && l >= 0.25 && l <= 0.75 && ((h > 0.11 && h < 0.18) || (h > 0.3 && h < 0.45) || (h > 0.55 && h < 0.7)); };
    let c0 = fv.cols; let c1 = -1; let r0 = fv.rows; let r1 = -1;
    for (let r = 0; r < fv.rows; r++) for (let q = 0; q < fv.cols; q++) { const c = fv.at(q, r); if (c && tile(c)) { c0 = Math.min(c0, q); c1 = Math.max(c1, q); r0 = Math.min(r0, r); r1 = Math.max(r1, r); } }
    expect(c1 - c0 + 1, 'largura da cúpula (m)').toBeGreaterThanOrEqual(22);
    expect(r1 - r0 + 1, 'altura da cúpula (m)').toBeGreaterThanOrEqual(14);
  });

  it('Maloca: a casa de palha cônica grande o bastante para não ser uma moita na beira da pista (≥ 17 m)', () => {
    expect(sil(view('maloca')).maxR + 1, 'altura (m)').toBeGreaterThanOrEqual(17);
  });

  it('Gameleira: a árvore-símbolo é bem maior que as do cenário — a copa larga de figueira velha (≥ 50 × 34 m)', () => {
    const s = sil(view('gameleira'));
    expect(s.maxC - s.minC + 1, 'largura da copa (m)').toBeGreaterThanOrEqual(50);
    expect(s.maxR + 1, 'altura (m)').toBeGreaterThanOrEqual(34);
  });

  it('Dunas do Jalapão: mais claras que o chão laranja do deserto (#e9b464, l 0,65) e com a vereda verde de buritis no pé', () => {
    const fv = view('dunas_jalapao');
    let n = 0; let light = 0; let green = 0;
    for (const c of fv.cells) {
      if (!c) continue;
      n++;
      const { h, s, l } = hsl(c);
      light += l;
      if (h > 0.2 && h < 0.45 && s > 0.25) green++;
    }
    expect(light / n, 'claridade média (o chão do deserto: 0,58–0,65)').toBeGreaterThanOrEqual(0.72);
    expect(green / n, 'fração verde (a vereda)').toBeGreaterThanOrEqual(0.06);
  });

  it('Ponte de Aracaju (corrida à noite): as torres e os estais se desenham em luz, não só a fileira de postes do tabuleiro', () => {
    const fv = frontView(LM.ponte_aracaju.build(), 1, ['glow']);
    const all = sil(view('ponte_aracaju'));
    let lit = 0;
    // A grade só de luz começa em fv.y0 (o tabuleiro): a linha de 40% da altura da ponte, nela.
    for (let r = Math.max(0, Math.ceil((all.maxR + 1) * 0.4 - fv.y0)); r < fv.rows; r++) for (let q = 0; q < fv.cols; q++) if (fv.at(q, r)) lit++;
    expect(lit, 'luz acima de 40% da altura (m²)').toBeGreaterThanOrEqual(300);
  });
});
