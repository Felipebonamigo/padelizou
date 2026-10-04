// Leitura dos marcos da segunda leva do Sudeste, Sul e Centro-Oeste (docs/VISUAL.md, "Marcos turísticos" › "Leitura").
// Pedido do dono (04/10/2026): "as referências ficarem também bonitas e entendíveis" — correu em Foz e não achou as
// cataratas: o modelo estava à vista, mas lia como prédios. A revisão passou os 26 outros marcos de
// landmarks/brasil-2-centro-sul.ts pela mesma régua (folha de contato com a câmera da pista e captura no jogo no ponto
// de aproximação) e refez os que um brasileiro não reconhece em 2 s, a 150–250 m, na névoa. Cada teste mede, na vista
// de frente (tests/front-view.ts) e, quando o problema era o ângulo de quem chega, no modelo girado, a propriedade que
// deixava o marco ilegível — não o desenho do modelo.
import * as THREE from 'three';
import { describe, expect, it } from 'vitest';
import { LANDMARKS } from '../src/render/scenery/landmarks';
import type { Model } from '../src/render/scenery/geom';
import { frontView, hsl, type FrontView } from './front-view';

type Pred = (c: THREE.Color | null) => boolean;
const solid: Pred = (c) => c !== null;
/** Água, espuma, névoa e tubo branco: muito claro (como na régua das Cataratas) ou azul-claro de água. */
const white: Pred = (c) => {
  if (!c) return false;
  const { h, s, l } = hsl(c);
  return l >= 0.8 || (h > 0.48 && h < 0.64 && l > 0.68 && s < 0.8);
};
const green: Pred = (c) => { if (!c) return false; const { h, s, l } = hsl(c); return h > 0.17 && h < 0.45 && s > 0.15 && l < 0.8; };
/** Azul de azulejo (saturado, não o azul-claro da água). */
const tileBlue: Pred = (c) => { if (!c) return false; const { h, s, l } = hsl(c); return h >= 0.55 && h < 0.72 && s > 0.3 && l < 0.75; };

/** O modelo girado `yaw` em Y: a vista de frente passa a ser a de quem olha de (cos yaw, 0, sen yaw). */
function turned(m: Model, yaw: number): Model {
  const r = new THREE.Matrix4().makeRotationY(yaw);
  return { ...m, parts: m.parts.map((p) => ({ ...p, geometry: p.geometry.clone().applyMatrix4(r) })) };
}

function view(id: string, cell: number, yaw = 0): FrontView {
  const m = LANDMARKS[id].build();
  return frontView(yaw ? turned(m, yaw) : m, cell);
}

interface Bounds { r0: number; r1: number; c0: number; c1: number; n: number }
function bounds(fv: FrontView): Bounds {
  const b = { r0: fv.rows, r1: -1, c0: fv.cols, c1: -1, n: 0 };
  for (let r = 0; r < fv.rows; r++) {
    for (let q = 0; q < fv.cols; q++) {
      if (!fv.at(q, r)) continue;
      b.n++; b.r0 = Math.min(b.r0, r); b.r1 = Math.max(b.r1, r); b.c0 = Math.min(b.c0, q); b.c1 = Math.max(b.c1, q);
    }
  }
  return b;
}
/** Linha a uma fração da altura da silhueta. */
const rowAt = (b: Bounds, f: number): number => Math.round(b.r0 + f * (b.r1 - b.r0));
/** Fração das células da silhueta (na faixa de colunas/linhas) que casam com `p`. */
function frac(fv: FrontView, p: Pred, q0 = 0, q1 = fv.cols - 1, r0 = 0, r1 = fv.rows - 1): number {
  let all = 0; let hit = 0;
  for (let r = r0; r <= r1; r++) for (let q = q0; q <= q1; q++) { const c = fv.at(q, r); if (!c) continue; all++; if (p(c)) hit++; }
  return hit / Math.max(1, all);
}
/** Trechos contínuos (em células) de `p` ao longo de uma linha. */
function rowRuns(fv: FrontView, r: number, p: Pred, q0 = 0, q1 = fv.cols - 1): number[] {
  const out: number[] = []; let len = 0;
  for (let q = q0; q <= q1; q++) { if (p(fv.at(q, r))) len++; else if (len) { out.push(len); len = 0; } }
  if (len) out.push(len);
  return out;
}
/** Trechos contínuos (em células) de `p` ao longo de uma coluna, de baixo para cima. */
function colRuns(fv: FrontView, q: number, p: Pred, r0 = 0, r1 = fv.rows - 1): number[] {
  const out: number[] = []; let len = 0;
  for (let r = r0; r <= r1; r++) { if (p(fv.at(q, r))) len++; else if (len) { out.push(len); len = 0; } }
  if (len) out.push(len);
  return out;
}
/** Altura (m) do topo da silhueta em cada coluna (0 = céu). */
function profile(fv: FrontView): number[] {
  const out: number[] = [];
  for (let q = 0; q < fv.cols; q++) {
    let t = 0;
    for (let r = fv.rows - 1; r >= 0; r--) if (fv.at(q, r)) { t = fv.y0 + (r + 1) * fv.cell; break; }
    out.push(t);
  }
  return out;
}
/**
 * Cristas do perfil de cima: picos com proeminência ≥ `minProm` m (o quanto descem dos dois lados antes de um ponto
 * tão alto ou mais — num platô de picos empatados, cada um conta só a sela até o vizinho) e largura ≥ `minWidth` m na
 * meia proeminência — uma abóbada, não uma torre fina nem um degrau. `q` = a coluna onde a crista começa.
 */
function crests(p: number[], cell: number, minProm: number, minWidth: number): Array<{ h: number; w: number; q: number }> {
  const out: Array<{ h: number; w: number; q: number }> = [];
  const n = p.length;
  for (let i = 0; i < n;) {
    let j = i; while (j + 1 < n && p[j + 1] === p[i]) j++;
    const h = p[i];
    if ((i === 0 || p[i - 1] < h) && (j === n - 1 || p[j + 1] < h)) {
      let lmin = h; for (let k = i - 1; k >= 0 && p[k] < h; k--) lmin = Math.min(lmin, p[k]);
      let rmin = h; for (let k = j + 1; k < n && p[k] < h; k++) rmin = Math.min(rmin, p[k]);
      if (i === 0) lmin = 0;
      if (j === n - 1) rmin = 0;
      const prom = h - Math.max(lmin, rmin);
      let a = i; while (a > 0 && p[a - 1] >= h - prom / 2) a--;
      let b = j; while (b < n - 1 && p[b + 1] >= h - prom / 2) b++;
      const w = (b - a + 1) * cell;
      if (prom >= minProm && w >= minWidth) out.push({ h, w, q: i });
    }
    i = j + 1;
  }
  return out;
}
const median = (a: number[]): number => { const s = [...a].sort((x, y) => x - y); return s.length ? s[Math.floor(s.length / 2)] : 0; };
const lightness = (c: THREE.Color): number => hsl(c).l;

describe('leitura dos marcos: Sudeste, Sul e Centro-Oeste (2ª leva)', () => {
  it('Igreja da Pampulha: as abóbadas em onda, grandes, com o painel de azulejos azuis na face', () => {
    // Era um borrão branco de 14 m entre os prédios da Pampulha à noite: a abóbada grande escondia as pequenas (3
    // cristas de frente) e o azulejo era um xadrez solto (11% da face).
    const fv = view('igreja_pampulha', 0.5);
    const humps = crests(profile(fv), fv.cell, 2.5, 5);
    expect(humps.length, 'abóbadas que se veem como ondas no perfil de cima').toBeGreaterThanOrEqual(4);
    expect(Math.max(...humps.map((c) => c.h)), 'altura da maior abóbada (m)').toBeGreaterThanOrEqual(18);
    expect(frac(fv, tileBlue), 'fração da face em azulejo azul').toBeGreaterThanOrEqual(0.25);
  });

  it('Véu de Noiva: o véu se vê de frente e de quem chega, saindo de um paredão de arenito (não de um muro)', () => {
    // Era um muro vermelho de blocos com estratos em faixas retas (a silhueta enchia 98% da caixa, como fachada) e
    // um fio d'água de 2 m; de quem chega, a aba do anfiteatro escondia a queda.
    const fv = view('veu_de_noiva', 1);
    const b = bounds(fv);
    expect(b.n / ((b.r1 - b.r0 + 1) * (b.c1 - b.c0 + 1)), 'quanto da caixa a silhueta enche (muro = 1)').toBeLessThanOrEqual(0.85);
    expect(frac(fv, white), 'fração da face que é água/névoa').toBeGreaterThanOrEqual(0.06);
    expect(Math.max(0, ...rowRuns(fv, rowAt(b, 0.5), white)) * fv.cell, 'largura do véu a meia altura (m)').toBeGreaterThanOrEqual(8);
    for (const yaw of [-0.8, 0.8]) {
      expect(frac(view('veu_de_noiva', 1, yaw), white), `água à vista de quem chega (giro ${yaw})`).toBeGreaterThanOrEqual(0.03);
    }
  });

  it('Casca d\'Anta: queda larga, com névoa no pé, numa escarpa larga (não numa torre de pedra entre dois morros)', () => {
    // Era um bloco cinza de 130 m (uma torre retangular) entre dois morros, com um fio d'água de 10 m (2,5% da face).
    const fv = view('casca_danta', 1);
    const b = bounds(fv);
    const W = (b.c1 - b.c0 + 1) * fv.cell;
    const mid = rowAt(b, 0.5);
    const fall = Math.max(0, ...rowRuns(fv, mid, white)) * fv.cell;
    expect(frac(fv, white), 'fração da face que é água/névoa').toBeGreaterThanOrEqual(0.08);
    expect(fall, 'largura da queda a meia altura (m)').toBeGreaterThanOrEqual(16);
    const rock = rowRuns(fv, mid, (c) => c !== null && !green(c) && !white(c)).reduce((s, x) => s + x, 0) * fv.cell;
    expect(rock / W, 'paredão de rocha a meia altura / largura da silhueta').toBeGreaterThanOrEqual(0.45);
    expect(Math.max(0, ...rowRuns(fv, rowAt(b, 0.08), white)) * fv.cell, 'névoa no pé mais larga que a queda').toBeGreaterThan(fall);
    for (const yaw of [-0.8, 0.8]) {
      expect(frac(view('casca_danta', 1, yaw), white), `água à vista de quem chega (giro ${yaw})`).toBeGreaterThanOrEqual(0.04);
    }
  });

  it('Ponte da Amizade: o arco de concreto grosso sob o tabuleiro, de cabeceira a cabeceira do vão', () => {
    // O arco tinha 4 m de espessura a 270 m da pista (2 px na tela): de longe sobrava o tabuleiro reto sobre dois morros.
    const fv = view('ponte_amizade', 1);
    const b = bounds(fv);
    const w = b.c1 - b.c0 + 1;
    const qa = b.c0 + Math.round(w * 0.35); const qb = b.c0 + Math.round(w * 0.65);
    const arch: number[] = [];
    for (let q = qa; q <= qb; q++) {
      const runs = colRuns(fv, q, solid, Math.round(5 / fv.cell));
      if (runs.length >= 2) arch.push(runs[0] * fv.cell); // o de baixo é o arco; o de cima, o tabuleiro
    }
    expect(arch.length / (qb - qa + 1), 'colunas do meio do vão com o arco sob o tabuleiro').toBeGreaterThanOrEqual(0.5);
    expect(median(arch), 'espessura do arco (m)').toBeGreaterThanOrEqual(8);
  });

  it('Ponte JK: os três arcos saltando o tabuleiro, grossos o bastante para não sumir na névoa', () => {
    // Lia bem no jogo (nota 4), mas os arcos tinham 4 m de espessura na vista de frente: a 250 m, 8 px que a névoa
    // clareia contra o céu. Melhora barata: o arco mais grosso, o mesmo desenho.
    const fv = view('ponte_jk', 1);
    const p = profile(fv);
    const tops = crests(p, fv.cell, 20, 20).sort((a, b) => b.h - a.h).slice(0, 3);
    expect(tops.length, 'arcos no perfil de cima').toBe(3);
    for (const c of tops) {
      const r = Math.round((c.h - fv.y0) / fv.cell) - 1;
      let t = 0; while (r - t >= 0 && fv.at(c.q, r - t)) t++;
      expect(t * fv.cell, `espessura do arco no alto (${c.h.toFixed(0)} m)`).toBeGreaterThanOrEqual(6);
    }
  });

  it('Ópera de Arame: a gaiola de tubos brancos se destaca, mais alta que a pedreira atrás', () => {
    // Era um cilindro de vidro azul-claro com tubos de 0,36 m (somem de longe) diante de um paredão de pedra mais alto
    // que ele: lia como estufa num muro.
    const fv = view('opera_de_arame', 0.5);
    const b = bounds(fv);
    const w = b.c1 - b.c0 + 1;
    const qa = b.c0 + Math.round(w * 0.35); const qb = b.c0 + Math.round(w * 0.65);
    const p = profile(fv);
    let inner = 0; let outer = 0;
    p.forEach((t, q) => { if (q >= qa && q <= qb) inner = Math.max(inner, t); else outer = Math.max(outer, t); });
    expect(inner - outer, 'o teatro passa da pedreira (m)').toBeGreaterThanOrEqual(2);
    expect(rowRuns(fv, rowAt(b, 0.3), white, qa, qb).length, 'tubos brancos cruzando a meia altura da parede').toBeGreaterThanOrEqual(8);
    expect(frac(fv, white, qa, qb), 'fração branca (tubos) no meio da vista').toBeGreaterThanOrEqual(0.35);
  });

  it('Serra do Rio do Rastro: a estrada em zigue-zague, clara e larga contra a mata da escarpa', () => {
    // A estrada era uma linha de 4–6 m de asfalto cinza quase do tom da mata (diferença de claridade 0,13): a 600 m
    // sobrava uma caixa verde.
    const fv = view('mirante_rio_do_rastro', 2);
    const b = bounds(fv);
    const r1 = rowAt(b, 0.85);
    let lg = 0; let ng = 0; let lr = 0; let nr = 0;
    for (let r = b.r0 + 2; r <= r1; r++) {
      for (let q = b.c0; q <= b.c1; q++) {
        const c = fv.at(q, r);
        if (!c) continue;
        if (green(c)) { lg += lightness(c); ng++; } else { lr += lightness(c); nr++; }
      }
    }
    expect(lr / Math.max(1, nr) - lg / Math.max(1, ng), 'claridade da estrada − claridade da mata').toBeGreaterThanOrEqual(0.25);
    const road = colRuns(fv, Math.round((b.c0 + b.c1) / 2), (c) => c !== null && !green(c), b.r0 + 2, r1);
    expect(road.length, 'trechos da estrada cruzando o meio da escarpa').toBeGreaterThanOrEqual(6);
    expect(median(road) * fv.cell, 'espessura de cada trecho (m)').toBeGreaterThanOrEqual(8);
  });

  it('Pedra Azul: a pedra é azul (a cor-assinatura do nome, contra a mata)', () => {
    // Lia como um domo de granito (nota 4), mas cinza: o "azul" tinha saturação 0,15–0,2 e na névoa virava pedra
    // qualquer. Melhora barata: o mesmo domo, a cor do nome.
    const fv = view('pedra_azul', 4);
    const blueStone: Pred = (c) => { if (!c) return false; const { h, s } = hsl(c); return h >= 0.55 && h < 0.7 && s > 0.3; };
    expect(frac(fv, blueStone), 'fração azul da face').toBeGreaterThanOrEqual(0.3);
  });

  it('Dunas de Itaúnas: a duna se levanta (não uma faixa de areia no horizonte)', () => {
    // A 210 m da pista, a duna de 32 m numa silhueta de 318 m (altura/largura 0,10) era uma faixa bege rente ao chão;
    // de quem chegava, lia-se a torre da igreja num morrinho, não as dunas.
    const fv = view('dunas_itaunas', 2);
    const b = bounds(fv);
    const H = (b.r1 - b.r0 + 1) * fv.cell; const W = (b.c1 - b.c0 + 1) * fv.cell;
    expect(H, 'altura da duna (m)').toBeGreaterThanOrEqual(42);
    expect(H / W, 'altura/largura da silhueta').toBeGreaterThanOrEqual(0.15);
  });

  it('Jacarés da Estrada Parque: o bando escuro na prainha se vê de frente e de quem chega, de cabeça erguida', () => {
    // Na captura do jogo, a 170 m, só se viam a árvore seca e o guarda-corpo da ponte: os jacarés somavam 26 m² de
    // frente (16 m² de quem chega) e não passavam de 2,1 m do chão.
    const reptile: Pred = (c) => { if (!c) return false; const { h, l } = hsl(c); return l < 0.36 && h > 0.14 && h < 0.35; };
    const area = (yaw: number): { m2: number; top: number } => {
      const fv = view('jacare', 0.25, yaw);
      let n = 0; let top = 0;
      for (let r = 0; r < fv.rows; r++) {
        for (let q = 0; q < fv.cols; q++) {
          const y = fv.y0 + (r + 0.5) * fv.cell;
          if (y < 6 && reptile(fv.at(q, r))) { n++; top = Math.max(top, y); }
        }
      }
      return { m2: n * fv.cell * fv.cell, top };
    };
    const front = area(0);
    expect(front.m2, 'jacarés de frente (m²)').toBeGreaterThanOrEqual(50);
    expect(front.top, 'altura das cabeças erguidas (m)').toBeGreaterThanOrEqual(3);
    for (const yaw of [-0.8, 0.8]) expect(area(yaw).m2, `jacarés de quem chega (giro ${yaw}, m²)`).toBeGreaterThanOrEqual(40);
    // Mesmo maiores, a 190 m os bichos dão ~4 px de altura (captura no jogo): a placa amarela de "cuidado, jacarés"
    // da Estrada Parque é o que lê primeiro — um losango de ≥ 5 m.
    const sign: Pred = (c) => { if (!c) return false; const { h, s, l } = hsl(c); return h > 0.11 && h < 0.17 && s > 0.6 && l > 0.45; };
    const fs = view('jacare', 0.25);
    let widest = 0;
    for (let r = 0; r < fs.rows; r++) widest = Math.max(widest, ...rowRuns(fs, r, sign), 0);
    expect(widest * fs.cell, 'largura da placa amarela (m)').toBeGreaterThanOrEqual(5);
  });

  it('Cavalhadas: os dois lados se leem pela cor — o castelo e os cavaleiros cristãos azuis, os mouros vermelhos', () => {
    // De longe sobravam dois fortinhos brancos de ameias coloridas e um borrão de bandeirolas: azul e vermelho eram
    // 13% e 14% da face cada um.
    const fv = view('cavalhada', 0.5);
    const red: Pred = (c) => { if (!c) return false; const { h, s, l } = hsl(c); return (h < 0.04 || h > 0.94) && s > 0.45 && l < 0.75; };
    expect(frac(fv, tileBlue), 'fração azul (cristãos)').toBeGreaterThanOrEqual(0.18);
    expect(frac(fv, red), 'fração vermelha (mouros)').toBeGreaterThanOrEqual(0.18);
  });

  it('Relógio da 14 de Julho: o mostrador se lê de longe, no alto de uma torre que passa das copas', () => {
    // A 150–250 m, o mostrador de 3,2 m dava 6–10 px e a torre de 20 m sumia entre os prédios da praça.
    const fv = view('monumento_campo_grande', 0.25);
    const b = bounds(fv);
    // O mostrador é luz (acende à noite): na vista só das partes de luz, o maior disco da metade de cima.
    const lit = frontView(LANDMARKS.monumento_campo_grande.build(), 0.25, ['glow']);
    const lb = bounds(lit);
    let widest = 0;
    for (let r = rowAt(lb, 0.5); r <= lb.r1; r++) widest = Math.max(widest, ...rowRuns(lit, r, solid), 0);
    expect(widest * lit.cell, 'diâmetro do mostrador (m)').toBeGreaterThanOrEqual(4.5);
    expect((b.r1 + 1) * fv.cell + fv.y0, 'altura da torre (m)').toBeGreaterThanOrEqual(27);
  });

  it('Centro Geodésico: o globo da América do Sul e o obelisco grandes o bastante para ler', () => {
    // O globo tinha 3,8 m (12 px a 150 m) e o obelisco 19 m: lia "um poste com uma bola".
    const fv = view('centro_geodesico', 0.25);
    const b = bounds(fv);
    const globe: Pred = (c) => { if (!c) return false; const { h, s } = hsl(c); return h >= 0.55 && h < 0.7 && s > 0.4; };
    let widest = 0;
    for (let r = b.r0; r <= b.r1; r++) widest = Math.max(widest, ...rowRuns(fv, r, globe), 0);
    expect(widest * fv.cell, 'diâmetro do globo azul (m)').toBeGreaterThanOrEqual(5.5);
    expect((b.r1 + 1) * fv.cell + fv.y0, 'altura do obelisco (m)').toBeGreaterThanOrEqual(25);
    // Cuiabá corre à noite: o globo azul some no escuro (captura no jogo). O anel do globo acende (luz) e o desenha.
    const lit = frontView(LANDMARKS.centro_geodesico.build(), 0.25, ['glow']);
    let ring = 0;
    for (let r = 0; r < lit.rows; r++) {
      const y = lit.y0 + (r + 0.5) * lit.cell;
      if (y > 3.5 && y < 10) ring = Math.max(ring, ...rowRuns(lit, r, solid), 0);
    }
    expect(ring * lit.cell, 'largura do anel aceso em volta do globo (m)').toBeGreaterThanOrEqual(5);
  });

  it('Dedo de Deus: um dedo sozinho acima do ombro da serra, de ponta grossa (não um cone entre picos)', () => {
    // Era uma agulha em cone (44 m a 60% da altura, 8 m a 95%) com o Escalavrado e o polegar também acima de 60%:
    // de longe, três picos de pedra quaisquer.
    const fv = view('dedo_de_deus', 2);
    const b = bounds(fv);
    for (let r = rowAt(b, 0.6); r <= b.r1; r++) {
      expect(rowRuns(fv, r, solid).length, `picos na linha a ${(((r - b.r0) / (b.r1 - b.r0)) * 100).toFixed(0)}% da altura`).toBe(1);
    }
    const width = (f: number): number => Math.max(...rowRuns(fv, rowAt(b, f), solid)) * fv.cell;
    expect(width(0.9) / width(0.7), 'largura a 90% / a 70% (dedo ≈ 1, cone ≈ 0,4)').toBeGreaterThanOrEqual(0.6);
  });
});
