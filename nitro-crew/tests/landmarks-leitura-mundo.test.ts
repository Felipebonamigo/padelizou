// Leitura dos marcos do Mundial (docs/VISUAL.md, "Marcos turísticos" › "Leitura"): o pedido do dono de 04/10/2026 —
// "as coisas de cada cidade mais nítidas, as referências bonitas e entendíveis". Estar na tela não basta: o jogador tem
// de reconhecer o marco em ~2 s, a 150–250 m, na névoa, sem legenda. A régua é a das Cataratas
// (tests/landmarks-leitura.test.ts): a vista de frente (tests/front-view.ts, o lado que o jogador vê, com z-buffer)
// mede o traço que fazia o marco ler errado — cada caso abaixo nasceu de um marco que, na folha de contato e na
// captura no jogo, lia como outra coisa (ou não lia).
import * as THREE from 'three';
import { describe, expect, it } from 'vitest';
import type { Model } from '../src/render/scenery/geom';
import { LANDMARKS_MUNDO } from '../src/render/scenery/landmarks/mundo';
import { frontView, hsl, type FrontView } from './front-view';

/** Faixas de colunas ocupadas (z), separadas por pelo menos `gap` colunas vazias: os bichos de um par, as torres. */
function columnGroups(fv: FrontView, gap: number): Array<[number, number]> {
  const out: Array<[number, number]> = [];
  let start = -1; let empty = 0;
  for (let q = 0; q < fv.cols; q++) {
    let any = false;
    for (let r = 0; r < fv.rows && !any; r++) if (fv.at(q, r)) any = true;
    if (any) { if (start < 0) start = q; empty = 0; } else if (start >= 0 && ++empty > gap) { out.push([start, q - empty]); start = -1; empty = 0; }
  }
  if (start >= 0) out.push([start, fv.cols - 1 - empty]);
  return out;
}

/** Linha mais alta ocupada nas colunas [c0, c1]. */
function topRow(fv: FrontView, c0: number, c1: number): number {
  for (let r = fv.rows - 1; r >= 0; r--) for (let q = c0; q <= c1; q++) if (fv.at(q, r)) return r;
  return -1;
}

const lightness = (c: THREE.Color): number => hsl(c).l;

/** Verde de mata/grama (o mesmo critério das Cataratas). */
const isGreen = (c: THREE.Color): boolean => { const { h, s } = hsl(c); return h > 0.17 && h < 0.45 && s > 0.15; };

describe('leitura dos marcos do mundo: o que sai da mata', () => {
  // Visto na captura em Daintree: a torre de observação era uma treliça de barras de 0,3 m, mais baixa que as copas das
  // duas árvores gigantes, do mesmo verde da floresta tropical em volta — de longe, só mais duas árvores. A torre da
  // copa existe para sair ACIMA da mata: o que passa da copa mais alta é dela, e tem corpo (numa grade de 1 m, como de
  // longe: uma barra fina não acerta o centro da célula).
  it('passarela de Daintree: a torre sai ≥ 4 m acima das copas, com ≥ 5 m de largura lá em cima', () => {
    const fv = frontView(LANDMARKS_MUNDO.passarela_daintree.build(), 1);
    let canopy = -1;
    for (let r = fv.rows - 1; r >= 0 && canopy < 0; r--) for (let q = 0; q < fv.cols; q++) { const c = fv.at(q, r); if (c && isGreen(c)) { canopy = r; break; } }
    let rowsAbove = 0; let widest = 0;
    for (let r = canopy + 1; r < fv.rows; r++) {
      let w = 0;
      for (let q = 0; q < fv.cols; q++) if (fv.at(q, r)) w++;
      if (w > 0) rowsAbove++;
      widest = Math.max(widest, w);
    }
    expect.soft(rowsAbove, 'metros de torre acima da copa mais alta').toBeGreaterThanOrEqual(4);
    expect.soft(widest, 'largura (m) da torre acima da copa').toBeGreaterThanOrEqual(5);
  });
});

/** Área de frente (m²) e extensão em Z (m) das células cuja cor passa no filtro. */
function colorArea(fv: FrontView, pick: (c: THREE.Color) => boolean): { area: number; zLen: number } {
  let n = 0; let q0 = Infinity; let q1 = -Infinity;
  for (let r = 0; r < fv.rows; r++) for (let q = 0; q < fv.cols; q++) { const c = fv.at(q, r); if (c && pick(c)) { n++; q0 = Math.min(q0, q); q1 = Math.max(q1, q); } }
  return { area: n * fv.cell * fv.cell, zLen: n ? (q1 - q0 + 1) * fv.cell : 0 };
}

/** Topo da silhueta (m) por coluna, só nas colunas ocupadas. */
function topProfile(fv: FrontView): number[] {
  const out: number[] = [];
  for (let q = 0; q < fv.cols; q++) for (let r = fv.rows - 1; r >= 0; r--) if (fv.at(q, r)) { out.push(fv.y0 + (r + 1) * fv.cell); break; }
  return out;
}
const median = (v: number[]): number => { const s = [...v].sort((a, b) => a - b); return s[Math.floor(s.length / 2)]; };

describe('leitura dos marcos do mundo: montanhas', () => {
  // O Anfiteatro do Drakensberg, de frente, era um bloco marrom de topo reto em faixas horizontais (±16 m de recorte
  // num paredão de 240 m) com a cachoeira do Tugela num fio de 7 m: lia como uma mesa — a mesma leitura da Table
  // Mountain, na copa vizinha. O que o diferencia é a crista recortada, com os contrafortes (o Sentinel e o Eastern
  // Buttress) subindo nas pontas, e o fio branco da queda no paredão.
  it('Anfiteatro do Drakensberg: crista recortada com os picos das pontas e a queda do Tugela à vista', () => {
    const cell = 3;
    const fv = frontView(LANDMARKS_MUNDO.anfiteatro_drakensberg.build(), cell);
    const tops = topProfile(fv);
    const rim = median(tops);
    expect.soft((Math.max(...tops) - rim) / rim, 'o pico mais alto acima da borda mediana (fração)').toBeGreaterThanOrEqual(0.2);
    const white = colorArea(fv, (c) => hsl(c).l >= 0.85);
    expect.soft(white.area, 'área de frente da queda branca (m²)').toBeGreaterThanOrEqual(2000);
  });
});

describe('leitura dos marcos do mundo: pontes', () => {
  // Visto na captura nas Rochosas: a ponte de treliça, a 190 m de lado, era um risco cinza-rosado na névoa — barras de
  // 0,6 m (a 300 m, menos de um pixel) — e não lia como a ponte de aço vermelho sobre o desfiladeiro. A treliça precisa
  // de corpo: numa grade de 1 m (uma barra fina não acerta o centro da célula), aço vermelho por metro de vão.
  it('ponte de treliça: o aço vermelho tem corpo de longe (≥ 2,5 m² por metro de vão, grade de 1 m)', () => {
    const fv = frontView(LANDMARKS_MUNDO.ponte_trelica.build(), 1);
    const red = (c: THREE.Color): boolean => { const { h, s } = hsl(c); return (h < 0.06 || h > 0.95) && s > 0.4; };
    const { area, zLen } = colorArea(fv, red);
    expect(zLen, 'vão de aço (m)').toBeGreaterThan(100);
    expect(area / zLen, 'm² de aço vermelho por metro de vão').toBeGreaterThanOrEqual(2.5);
  });

  // Storseisundet: a ponte da Estrada do Atlântico é lembrada pela corcova — a estrada que sobe íngreme e parece
  // acabar no ar. Com 27 m de altura em 365 m (as ilhotas inclusas), vista de lado a 200 m era uma linha baixa sobre o
  // mar, igual a qualquer ponte. A corcova exagerada (o cartão-postal) tem de passar de 12% do comprimento.
  it('Storseisundet: a corcova lê — altura ≥ 12% do comprimento da silhueta', () => {
    const fv = frontView(LANDMARKS_MUNDO.ponte_storseisundet.build(), 1);
    let minR = fv.rows; let maxR = -1; let q0 = fv.cols; let q1 = -1;
    for (let r = 0; r < fv.rows; r++) for (let q = 0; q < fv.cols; q++) if (fv.at(q, r)) { minR = Math.min(minR, r); maxR = Math.max(maxR, r); q0 = Math.min(q0, q); q1 = Math.max(q1, q); }
    expect((maxR - minR + 1) / (q1 - q0 + 1), 'altura / comprimento').toBeGreaterThanOrEqual(0.12);
  });
});

describe('leitura dos marcos do mundo: à noite', () => {
  // Las Vegas é à noite: a pirâmide de vidro negro contra o céu escuro some — da luz só havia a ponta e uma faixa no
  // pé (o facho no céu é outro material, e fica alto demais para o quadro). O Luxor de verdade se lê à noite pelas
  // arestas acesas: elas desenham o triângulo inteiro, do pé até a ponta.
  it('pirâmide do Luxor: a luz (glow) desenha o triângulo — do pé ao topo e de uma ponta à outra da base', () => {
    const cell = 2;
    const all = frontView(LANDMARKS_MUNDO.piramide_luxor.build(), cell);
    const lit = frontView(LANDMARKS_MUNDO.piramide_luxor.build(), cell, ['glow']);
    // A altura e a base da silhueta inteira (a pirâmide manda nela; a esfinge e o obelisco são baixos) contra as da luz.
    let minR = all.rows; let maxR = -1;
    for (let r = 0; r < all.rows; r++) for (let q = 0; q < all.cols; q++) if (all.at(q, r)) { minR = Math.min(minR, r); maxR = Math.max(maxR, r); }
    const rowsLit = new Set<number>(); let litMinZ = Infinity; let litMaxZ = -Infinity;
    for (let r = 0; r < lit.rows; r++) for (let q = 0; q < lit.cols; q++) if (lit.at(q, r)) { rowsLit.add(r); const z = lit.z0 + q * cell; litMinZ = Math.min(litMinZ, z); litMaxZ = Math.max(litMaxZ, z); }
    let silMinZ = Infinity; let silMaxZ = -Infinity;
    for (let r = 0; r < all.rows; r++) for (let q = 0; q < all.cols; q++) if (all.at(q, r)) { const z = all.z0 + q * cell; silMinZ = Math.min(silMinZ, z); silMaxZ = Math.max(silMaxZ, z); }
    expect.soft(rowsLit.size / (maxR - minR + 1), 'fração da altura com luz').toBeGreaterThanOrEqual(0.8);
    expect.soft((litMaxZ - litMinZ) / (silMaxZ - silMinZ), 'fração da base com luz').toBeGreaterThanOrEqual(0.8);
  });
});

/**
 * Vista de quem chega: o modelo girado em Y para que a direção do jogador (em graus a partir de +X; + para +Z) vire
 * +X, a direção da vista de frente. O layout (scenery/layout.ts) põe +X para a pista e gira o marco `turn` para quem
 * chega; do lado esquerdo da pista quem chega fica para +Z do modelo, do lado direito para −Z. Um marco de perto a
 * ~35 m de lado, visto a ~100 m, com o giro de 0,30 rad: o jogador fica a ~54° da frente, de um lado ou do outro.
 */
function viewFrom(model: Model, deg: number, cell: number): FrontView {
  return frontView(turned(model, deg), cell);
}

/** O modelo girado em Y (graus), para a vista de frente mostrar o que o jogador vê chegando de lado. */
function turned(model: Model, deg: number): Model {
  const rot = new THREE.Matrix4().makeRotationY((deg * Math.PI) / 180);
  return { ...model, parts: model.parts.map((p) => ({ ...p, geometry: p.geometry.clone().applyMatrix4(rot) })) };
}

/** Área de frente (m²) do que fica acima de `y` (a placa, sem o poste). */
function areaAbove(fv: FrontView, y: number): number {
  let n = 0;
  for (let r = 0; r < fv.rows; r++) if (fv.y0 + (r + 0.5) * fv.cell >= y) for (let q = 0; q < fv.cols; q++) if (fv.at(q, r)) n++;
  return n * fv.cell * fv.cell;
}

describe('leitura dos marcos do mundo: placas', () => {
  // Visto na captura na Rota 66: as duas placas da pista ficam do lado direito, e o escudo, virado 30° para +Z "para
  // quem vem", do lado direito vira para LONGE de quem vem — a 100 m, de faca: um poste com um risco em cima. A placa
  // dos trolls tinha o mesmo giro (a primeira de Trollstigen fica à direita). Placa tem de ser lida pelos dois lados.
  it.each([
    ['placa_rota_66', 6.5],
    ['placa_trolls', 4.0],
  ] as const)('%s: de quem chega pela esquerda e pela direita, a placa mostra ≥ 0,45 da área de frente', (id, y) => {
    const model = LANDMARKS_MUNDO[id].build();
    const cell = 0.1;
    const front = areaAbove(viewFrom(model, 0, cell), y);
    const left = areaAbove(viewFrom(model, 54, cell), y);
    const right = areaAbove(viewFrom(model, -54, cell), y);
    expect.soft(left / front, `${id}: quem chega pela esquerda (área / de frente)`).toBeGreaterThanOrEqual(0.45);
    expect.soft(right / front, `${id}: quem chega pela direita (área / de frente)`).toBeGreaterThanOrEqual(0.45);
  });
});

describe('leitura dos marcos do mundo: o letreiro da beira da estrada', () => {
  // Visto na captura na Rota 66: o diner (6 m de vagão inox) e o letreiro EAT num poste de 10 m sumiam atrás do
  // outdoor da beira (~9 m) — a 200 m não havia nada dele à vista. O diner de beira de estrada se anuncia pelo letreiro
  // alto: ele tem de passar dos outdoors, com placa de cor (não só o poste).
  it('diner: ≥ 12 m² de letreiro colorido acima de 10 m (a altura dos outdoors da beira)', () => {
    const fv = frontView(LANDMARKS_MUNDO.diner_neon.build(), 0.1);
    const colored = (c: THREE.Color): boolean => hsl(c).s > 0.35;
    let n = 0;
    for (let r = 0; r < fv.rows; r++) if (fv.y0 + (r + 0.5) * fv.cell >= 10) for (let q = 0; q < fv.cols; q++) { const c = fv.at(q, r); if (c && colored(c)) n++; }
    expect(n * fv.cell * fv.cell, 'letreiro colorido acima de 10 m (m²)').toBeGreaterThanOrEqual(12);
  });
});

describe('leitura dos marcos do mundo: o pagode', () => {
  // O pagode Chureito, de frente, era 7% de vermelho num morro verde de 104 m que tomava três quartos da silhueta: a
  // 190 m de lado, um morrinho com um risco vermelho. O cartão-postal é o pagode de cinco telhados: ele tem de mandar.
  it('pagode Chureito: o pagode (vermelho e os telhados escuros) é ≥ 25% da silhueta', () => {
    const fv = frontView(LANDMARKS_MUNDO.pagode_chureito.build(), 0.5);
    let sil = 0; let pagoda = 0;
    for (let r = 0; r < fv.rows; r++) for (let q = 0; q < fv.cols; q++) {
      const c = fv.at(q, r); if (!c) continue;
      sil++;
      const { h, s, l } = hsl(c);
      if (((h < 0.05 || h > 0.95) && s > 0.45 && l < 0.7) || (s < 0.2 && l < 0.32)) pagoda++;
    }
    expect(pagoda / sil, 'fração da silhueta que é o pagode').toBeGreaterThanOrEqual(0.25);
  });
});

describe('leitura dos marcos do mundo: contraste com o bioma', () => {
  // Visto na captura no Kruger: o portão (sapé dourado, pilares de pedra parda, guarita) a 100 m era um borrão da cor
  // do capim da savana — a mesma claridade do chão (o capim do bioma: #b5b547). Portão de parque na savana se lê pelo
  // contraste: a cal das paredes, o sapé escuro, a placa.
  it('portão do Kruger: ≥ 45% da frente contrasta em claridade com o capim da savana', () => {
    const fv = frontView(LANDMARKS_MUNDO.portao_kruger.build(), 0.25);
    const grass = hsl(new THREE.Color('#b5b547').convertLinearToSRGB()).l; // a cor guardada é linear; a vista, sRGB
    let sil = 0; let contrast = 0;
    for (let r = 0; r < fv.rows; r++) for (let q = 0; q < fv.cols; q++) { const c = fv.at(q, r); if (!c) continue; sil++; if (Math.abs(hsl(c).l - grass) >= 0.2) contrast++; }
    expect(contrast / sil, 'fração da frente que contrasta com o capim').toBeGreaterThanOrEqual(0.45);
  });
});

describe('leitura dos marcos do mundo: vilas', () => {
  // A vila da Lapônia lia como estação de esqui: casarão e cabanas de telhado nevado, abetos — e as duas lavvu (as
  // tendas sami, o que diz "Lapônia") pequenas no meio delas, mais baixas que os abetos e o casarão. A tenda tem de
  // mandar: a lona clara grande e a ponta das varas no alto da silhueta.
  it('vila da Lapônia: as lavvu mandam — lona ≥ 60 m² de frente e a ponta delas é o topo da vila', () => {
    const cell = 0.25;
    const fv = frontView(LANDMARKS_MUNDO.vila_lapponia.build(), cell);
    // Lona: clara e quente (a neve e os telhados nevados são frios, azulados; as toras, escuras).
    const canvas = (c: THREE.Color): boolean => { const { h, s, l } = hsl(c); return h > 0.04 && h < 0.17 && s > 0.2 && l > 0.6; };
    const { area } = colorArea(fv, canvas);
    let canvasTop = -1; let top = -1;
    for (let r = fv.rows - 1; r >= 0 && (canvasTop < 0 || top < 0); r--) {
      for (let q = 0; q < fv.cols; q++) {
        const c = fv.at(q, r);
        if (!c) continue;
        if (top < 0) top = r;
        if (canvasTop < 0 && canvas(c)) canvasTop = r;
      }
    }
    expect.soft(area, 'lona das lavvu (m² de frente)').toBeGreaterThanOrEqual(60);
    // Acima da lona só as varas da lavvu (≤ 3 m).
    expect.soft((top - canvasTop) * cell, 'o que passa acima da lona mais alta (m)').toBeLessThanOrEqual(3);
  });
});

/**
 * A vista de frente inteira e, por célula, se o que se vê ali é luz (material `glow`): a célula da vista completa (com
 * z-buffer) tem a cor que veio da luz. As duas grades começam em cantos diferentes (a de luz só cobre a luz).
 */
function litView(model: Model, cell: number): { all: FrontView; lit: (q: number, r: number) => boolean } {
  const all = frontView(model, cell);
  const glow = frontView(model, cell, ['glow']);
  const dq = Math.round((all.z0 - glow.z0) / cell); const dr = Math.round((all.y0 - glow.y0) / cell);
  return {
    all,
    lit: (q, r) => {
      const c = q >= 0 && q < all.cols && r >= 0 && r < all.rows ? all.at(q, r) : null;
      const ql = q + dq; const rl = r + dr;
      const g = ql >= 0 && ql < glow.cols && rl >= 0 && rl < glow.rows ? glow.at(ql, rl) : null;
      return !!c && !!g && g.equals(c);
    },
  };
}

/** Fração da silhueta acima de `yFrac` × a altura do marco que é luz à vista. */
function litFraction(model: Model, cell: number, yFrac: number): number {
  const { all, lit } = litView(model, cell);
  let top = 0;
  for (let r = all.rows - 1; r >= 0 && !top; r--) for (let q = 0; q < all.cols; q++) if (all.at(q, r)) { top = all.y0 + (r + 1) * cell; break; }
  const y = top * yFrac;
  let sil = 0; let glowing = 0;
  for (let r = 0; r < all.rows; r++) {
    if (all.y0 + (r + 0.5) * cell < y) continue;
    for (let q = 0; q < all.cols; q++) { if (!all.at(q, r)) continue; sil++; if (lit(q, r)) glowing++; }
  }
  return glowing / sil;
}

describe('leitura dos marcos do mundo: à noite, aceso', () => {
  // Las Vegas é à noite. Na captura a 100 m a placa era um losango escuro contra as janelas acesas dos prédios — só as
  // lâmpadas do contorno brilhavam. A placa de verdade é iluminada: a face (o losango branco e os discos das letras)
  // tem de ser luz, para ler à noite.
  it('placa de Las Vegas: a face acende à noite (≥ 60% da placa, acima dos postes, é luz)', () => {
    expect(litFraction(LANDMARKS_MUNDO.placa_las_vegas.build(), 0.1, 0.45), 'fração da placa que é luz').toBeGreaterThanOrEqual(0.6);
  });

  // Osaka é à noite. Na captura a Tsutenkaku era uma torre roxo-escura atrás dos prédios do fundo, da mesma cor deles
  // — da luz, só a cúpula e um fio ciano nas arestas. A torre de verdade é toda acesa (as faixas de néon, o painel
  // luminoso no fuste, o mirante): o corpo dela tem de ser luz.
  it('Tsutenkaku: o corpo da torre acende à noite (≥ 35% da silhueta acima das pernas é luz)', () => {
    expect(litFraction(LANDMARKS_MUNDO.tsutenkaku.build(), 0.5, 0.22), 'fração da torre que é luz').toBeGreaterThanOrEqual(0.35);
  });

  // Sydney é à noite. Na captura o arco da Harbour Bridge era um vulto roxo-escuro atrás dos prédios do fundo, com
  // contas de luz a cada 19 m — o "cabide" não se desenhava. A ponte de verdade tem o arco aceso de ponta a ponta.
  it('Harbour Bridge: o contorno de cima do arco é luz (≥ 70% das colunas do vão)', () => {
    const cell = 1;
    const { all, lit } = litView(LANDMARKS_MUNDO.harbour_bridge.build(), cell);
    // O vão do arco: as colunas em que a silhueta passa de 50 m (os pilones de granito ficam em 64 m, fora do vão).
    let span = 0; let litTop = 0;
    for (let q = 0; q < all.cols; q++) {
      const z = all.z0 + (q + 0.5) * cell;
      if (Math.abs(z) > 140) continue;
      for (let r = all.rows - 1; r >= 0; r--) {
        const c = all.at(q, r); if (!c) continue;
        if (all.y0 + r * cell < 50) break;
        span++;
        // Topo aceso: a luz à vista no topo ou logo abaixo dele (1 m).
        if (lit(q, r) || lit(q, r - 1)) litTop++;
        break;
      }
    }
    expect(span, 'colunas do vão').toBeGreaterThan(200);
    expect(litTop / span, 'colunas do vão com o topo aceso').toBeGreaterThanOrEqual(0.7);
  });

  // Tromsø é à noite. Na captura a 300 m a Catedral Ártica era um triangulozinho morno atrás de uma árvore: 36 m, e de
  // quem chega (de lado, ~40° da frente) o que se vê são os painéis brancos escalonados — escuros à noite; só a frente
  // de vidro acendia. A catedral de verdade é iluminada: os painéis em luz (o triângulo branco é o cartão-postal), e
  // maior (1,3×).
  it('Catedral Ártica: de quem chega pelos dois lados, à noite ≥ 50% dela é luz; e tem ≥ 44 m de altura', () => {
    const model = LANDMARKS_MUNDO.catedral_artica.build();
    expect.soft(litFraction(turned(model, 40), 0.5, 0), 'chegando pela esquerda: fração acesa').toBeGreaterThanOrEqual(0.5);
    expect.soft(litFraction(turned(model, -40), 0.5, 0), 'chegando pela direita: fração acesa').toBeGreaterThanOrEqual(0.5);
    const fv = frontView(model, 0.5);
    expect.soft(fv.y0 + fv.rows * fv.cell, 'altura (m)').toBeGreaterThanOrEqual(44);
  });
});

describe('leitura dos marcos do mundo: quedas d\'água', () => {
  // Visto na captura em Trollstigen: a 300 m, a queda do Stigfossen era uma faixa só um pouco mais clara que o
  // paredão lavado pela névoa — a água pintada (`flat`), acesa pelo sol como a rocha. A régua das Cataratas: a água é
  // luz (não pega sombra; branca na névoa) e larga.
  it('Stigfossen: a queda é luz e larga (≥ 2.400 m² de água acesa de frente)', () => {
    const { all, lit } = litView(LANDMARKS_MUNDO.cachoeira_stigfossen.build(), 2);
    let n = 0;
    for (let r = 0; r < all.rows; r++) for (let q = 0; q < all.cols; q++) { const c = all.at(q, r); if (c && lit(q, r) && hsl(c).l >= 0.85) n++; }
    expect(n * 4, 'água acesa à vista (m²)').toBeGreaterThanOrEqual(2400);
  });
});

describe('leitura dos marcos do mundo: bichos', () => {
  // Defeito visto na folha de contato: o pescoço da girafa procedural tinha 2,6× o comprimento (a peça de altura 2,6 m
  // esticada pelo comprimento do segmento) e passava da cabeça — 6,9 m de topo com a cabeça a 5,8 m, e a ponta de
  // baixo furando o peito. De longe, cada girafa era um bicho com um poste espetado: lia como guindaste.
  it('girafa: a cabeça é o topo de cada bicho — acima dela só os ossicones (o pescoço não passa)', () => {
    const cell = 0.1;
    const fv = frontView(LANDMARKS_MUNDO.girafa.build(), cell);
    const animals = columnGroups(fv, 3);
    expect(animals.length, 'as duas girafas, de perfil para a pista').toBe(2);
    for (const [c0, c1] of animals) {
      const top = topRow(fv, c0, c1);
      // Os 0,3 m de cima da girafa: as pontas escuras dos ossicones têm de estar ali (o pescoço comprido punha
      // pelagem até 1,1 m acima delas).
      let tips = 0;
      for (let r = top; r > top - Math.round(0.3 / cell); r--) {
        for (let q = c0; q <= c1; q++) { const c = fv.at(q, r); if (c && lightness(c) < 0.25) tips++; }
      }
      expect(tips, `girafa nas colunas ${c0}–${c1}: ossicones no topo (${((fv.y0 + top * cell)).toFixed(1)} m)`).toBeGreaterThan(0);
    }
  });

  // Visto na captura em Yanbaru: o par de shisas eram dois bonecos de 3,5 m sobre pilares, no fim de um muro baixo de
  // 23 m — a 100 m, dois pontinhos marrons em postes. A figura de terracota é o marco; ela tem de mandar na silhueta.
  it('shisa: as duas figuras de terracota são grandes (≥ 4,5 m de altura e ≥ 12 m² de frente cada)', () => {
    const cell = 0.25;
    const fv = frontView(LANDMARKS_MUNDO.shisa.build(), cell);
    // Terracota: laranja-avermelhado saturado, nem claro (a pedra coral, a cal) nem escuro (olhos, boca).
    const terracotta = (c: THREE.Color | null): boolean => { if (!c) return false; const { h, s, l } = hsl(c); return (h < 0.1 || h > 0.97) && s > 0.35 && l > 0.2 && l < 0.62; };
    const groups: Array<{ cells: number; minR: number; maxR: number }> = [];
    for (const [c0, c1] of columnGroups({ ...fv, at: (q, r) => (terracotta(fv.at(q, r)) ? fv.at(q, r) : null) } as FrontView, 4)) {
      let cells = 0; let minR = fv.rows; let maxR = -1;
      for (let r = 0; r < fv.rows; r++) for (let q = c0; q <= c1; q++) if (terracotta(fv.at(q, r))) { cells++; minR = Math.min(minR, r); maxR = Math.max(maxR, r); }
      if (cells * cell * cell >= 1) groups.push({ cells, minR, maxR });
    }
    expect(groups.length, 'duas figuras').toBe(2);
    for (const g of groups) {
      expect((g.maxR - g.minR + 1) * cell, 'altura da figura (m)').toBeGreaterThanOrEqual(4.5);
      expect(g.cells * cell * cell, 'área de frente da figura (m²)').toBeGreaterThanOrEqual(12);
    }
  });
});
