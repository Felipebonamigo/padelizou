// Os desenhos das pistas: o polígono-alvo de cada uma, que scripts/shape-to-track.ts transforma nos `ops` de
// src/core/track/tracks.ts (docs/PISTAS.md, "Pistas com desenho"). Coordenadas da tela (x para a direita, y para
// BAIXO), num quadro de ~100; o polígono é HORÁRIO e começa no pé do lado onde fica a largada, que sobe na
// vertical (o minimapa começa apontando para cima). Um terceiro número num ponto é o raio da quina ali (na mesma
// unidade, e também multiplicado por `round`); sem ele vale `radius`. Mexeu num desenho: rode a ferramenta com
// --apply, confira os testes (forma, marcos, orçamento do cenário em tests/scenery-forma.test.ts — o relatório já
// mostra) e a velocidade média da IA (`npm run balance -- corrida profissional <semente> <pista>`): mais quinas, mais
// freadas.

export interface Shape {
  /** Estado (sigla) e o que o desenho representa — vão para a folha de desenhos. */
  state: string;
  what: string;
  /** Vértices [x, y] ou [x, y, raio da quina]. */
  pts: Array<[number, number] | [number, number, number]>;
  /** Raio padrão das quinas, em unidades do desenho. */
  radius: number;
  /** Comprimento da volta em segmentos (o da pista de antes, ±10%). */
  segments: number;
  /**
   * A curva da quina mais forte (|curva| ≤ 6) — as outras saem na proporção do ângulo ÷ raio. Não muda o desenho:
   * é a dificuldade de verdade (quanto a IA freia), acertada pela velocidade média medida da pista de antes.
   */
  cmax: number;
  /** Arredondamento das quinas (× `radius`; padrão 1): mais trecho em curva, mais freada, desenho mais redondo. */
  round?: number;
  /** O índice técnico total da pista (o de antes): o que a curva não dá vira morro (`hills` à mão, ou automático). */
  index: number;
  /** Fração do 1º lado (depois da quina 0) em que fica a linha de largada. */
  startAt?: number;
  /** Morros nas retas, à mão: [lado, comprimento, altura]. Não mexem no desenho nem na IA; acertam o índice técnico. */
  hills?: Array<[number, number, number]>;
}

type P = [number, number];

/**
 * Os pontos de um arco de círculo de `a` até `b` (sem as pontas), estufado `lift` para a ESQUERDA de quem vai de `a`
 * para `b` (negativo: para a direita). No polígono horário, esquerda é para fora: um tufo de copa em cima, indo da
 * esquerda para a direita, é `lift` positivo; um vão em arco embaixo, voltando da direita para a esquerda, negativo.
 */
function bulge(a: P, b: P, lift: number, n = 4): P[] {
  const dx = b[0] - a[0]; const dy = b[1] - a[1];
  const c = Math.hypot(dx, dy); const ux = dx / c; const uy = dy / c;
  const h = Math.abs(lift); const r = (c * c / 4 + h * h) / (2 * h);
  const s = Math.sign(lift);
  const out: P[] = [];
  for (let i = 1; i <= n; i++) {
    const t = i / (n + 1);
    const f = Math.sqrt(r * r - (c * (t - 0.5)) ** 2) - (r - h);
    out.push([a[0] + dx * t + uy * f * s, a[1] + dy * t - ux * f * s]);
  }
  return out;
}

/** Um caminho que liga os pontos com arcos estufados entre eles: [ponto, estufamento até o próximo, ponto, ...]. */
function bumps(path: P[], lift: number, n = 3): P[] {
  const out: P[] = [];
  for (let i = 0; i < path.length; i++) {
    out.push(path[i]);
    if (i + 1 < path.length) out.push(...bulge(path[i], path[i + 1], lift, n));
  }
  return out;
}

/** `n` pontos de um círculo de `from` a `to` graus, pontas incluídas (0° = direita, 90° = baixo: horário na tela). */
function circle(cx: number, cy: number, r: number, from: number, to: number, n: number, radius?: number): Array<[number, number, number] | P> {
  return Array.from({ length: n }, (_, i) => {
    const a = ((from + ((to - from) * i) / (n - 1)) * Math.PI) / 180;
    const p: P = [Math.round((cx + r * Math.cos(a)) * 100) / 100, Math.round((cy + r * Math.sin(a)) * 100) / 100];
    return radius === undefined ? p : [p[0], p[1], radius];
  });
}

/**
 * Sol de `n` raios em ponta (comprimentos alternando `long`/`short`) sobre um disco de raio `disc`; cada raio tem a
 * base de ±`half` graus. O primeiro raio é girado para que a borda que sobe até a ponta dele seja vertical — é ali a
 * largada (o polígono começa no pé dessa borda).
 */
function sun(cx: number, cy: number, disc: number, long: number, short: number, n: number, half: number): P[] {
  const rad = (d: number) => (d * Math.PI) / 180;
  // Ângulo φ do 1º raio (apontando para cima e à esquerda) em que disc·cos(φ − half) = long·cos(φ): borda vertical.
  let lo = 180; let hi = 270 - half;
  for (let i = 0; i < 60; i++) {
    const mid = (lo + hi) / 2;
    if (disc * Math.cos(rad(mid - half)) - long * Math.cos(rad(mid)) > 0) lo = mid; else hi = mid;
  }
  const phi0 = (lo + hi) / 2;
  const at = (r: number, a: number): P => [cx + r * Math.cos(rad(a)), cy + r * Math.sin(rad(a))];
  const out: P[] = [];
  for (let k = 0; k < n; k++) {
    const phi = phi0 + (360 / n) * k;
    out.push(at(disc, phi - half), at(k % 2 === 0 ? long : short, phi), at(disc, phi + half));
  }
  out[1] = [out[0][0], out[1][1]]; // a borda da largada exatamente vertical
  return out.map(([x, y]) => [Math.round(x * 1000) / 1000, Math.round(y * 1000) / 1000]);
}

export const SHAPES: Record<string, Shape> = {
  // RJ · Cristo Redentor de braços abertos no pedestal. Largada no lado esquerdo do pedestal.
  copacabana: {
    state: 'RJ', what: 'Cristo Redentor', radius: 1.6, segments: 1800, cmax: 3, index: 0.649,
    hills: [[2, 100, 15], [16, 100, 15]], // as duas ladeiras da túnica
    pts: [
      [33, 98], [33, 80], [37, 80], [39, 31], // pedestal e o lado esquerdo da túnica
      [5, 24], [5, 16], // braço esquerdo (a manga caída embaixo)
      [47, 16], [47, 13, 0.8], [44, 13, 0.8], [44, 2, 3], [56, 2, 3], [56, 13, 0.8], [53, 13, 0.8], [53, 16], // cabeça e pescoço
      [95, 16], [95, 24], // braço direito
      [61, 31], [63, 80], [67, 80], [67, 98], // túnica e pedestal
    ],
  },
  // SP · MASP de lado: a caixa suspensa nos dois pórticos, o vão livre embaixo. Largada no pilar esquerdo.
  sampa_noite: {
    state: 'SP', what: 'MASP', radius: 1.6, segments: 1850, cmax: 5.7, index: 6.128,
    pts: [[2, 80], [2, 25], [98, 25], [98, 80], [88, 80], [88, 56], [12, 56], [12, 80]],
  },
  // MG · Igreja da Pampulha de lado: a abóbada grande da nave e quatro corcovas cada vez menores, no chão.
  pampulha: {
    state: 'MG', what: 'Igreja da Pampulha', radius: 1.6, segments: 2050, cmax: 3.4, index: 1.174, startAt: 0.6,
    pts: [
      [3, 70], [3, 42], [6, 28, 8], [12, 18, 8], [22, 9, 8], [32, 18, 8], [38, 32, 8], [41, 48, 4], // a nave
      [43, 62], [45, 48, 4], [51, 38, 4], [57, 48, 4], // 1ª corcova
      [59, 63], [61, 53, 3], [66, 45, 3], [71, 53, 3], // 2ª
      [73, 64], [75, 57, 2.5], [79, 51, 2.5], [83, 57, 2.5], // 3ª
      [85, 65], [88, 59, 2], [92, 57, 2], [95, 61, 2], // 4ª
      [97, 70],
    ],
  },
  // ES · Convento da Penha: o morro de pedra (encosta suave à esquerda, penhasco do lado do mar) e o convento no alto.
  convento_penha: {
    state: 'ES', what: 'Convento da Penha', radius: 1.6, segments: 1980, cmax: 4.7, index: 3.96,
    pts: [
      [2, 90], [2, 80], [8, 66, 8], [16, 54, 8], [26, 44, 8], [36, 35, 6], [46, 30, 4], // a encosta de pedra
      [58, 30], [58, 12], [72, 12], [72, 30], // o convento
      [80, 30, 3], [86, 36, 4], [89, 50, 6], [92, 66, 6], [95, 81, 4], [97, 90], // o penhasco
    ],
  },
  // SC · Ponte Hercílio Luz de lado: as duas torres, o cabo pênsil descendo até o tabuleiro, os estais das pontas.
  floripa: {
    state: 'SC', what: 'Ponte Hercílio Luz', radius: 1.4, segments: 2010, cmax: 4.4, index: 3.002,
    // Largada no pé da torre esquerda, do lado de fora; o estai é uma faixa fina até o bloco de ancoragem no chão.
    pts: [
      [27, 64], [27, 23], [8, 58], [8, 64], [0, 64], [0, 58], [4, 58], [27, 15], // torre e estai da esquerda
      [27, 6], [31, 6], [31, 15], // topo da torre esquerda
      [33.5, 24.6], [37, 35.7], [41, 45.2], [45.5, 51.8], [50, 54], [54.5, 51.8], [59, 45.2], [63, 35.7], [66.5, 24.6], // o cabo
      [69, 15], [69, 6], [73, 6], [73, 15], // topo da torre direita
      [96, 58], [100, 58], [100, 64], [92, 64], [92, 58], [73, 23], [73, 64], // estai e ancoragem da direita
    ],
  },
  // DF · O avião do Plano Piloto visto de cima, bico para cima: o Eixo Monumental (fuselagem), as asas arqueadas para
  // trás (o Eixo Rodoviário) e a cauda. Largada na fuselagem, atrás da asa esquerda.
  brasilia: {
    state: 'DF', what: 'avião do Plano Piloto', radius: 1.4, segments: 2120, cmax: 5.9, index: 7.745, startAt: 0.6,
    pts: [
      [46, 78], [46, 46], [36, 48], [25, 52], [14, 58], [6, 66], [2, 52], [11, 45], [22, 37], [34, 32], // asa esquerda
      [46, 30], [46, 20], [50, 4], [54, 20], [54, 30], // fuselagem e bico
      [66, 32], [78, 37], [89, 45], [98, 52], [94, 66], [86, 58], [75, 52], [64, 48], [54, 46], // asa direita
      [54, 78], [68, 86], [68, 95], [54, 92], [46, 92], [32, 95], [32, 86], // cauda
    ],
  },
  // PR · Araucária: tronco reto e a copa em taça achatada, com os tufos dos galhos no alto. Largada no tronco.
  curitiba: {
    state: 'PR', what: 'araucária', radius: 1.4, segments: 1990, cmax: 5.1, index: 5.628, startAt: 0.3,
    pts: [
      [46, 96], [46, 54], [34, 46], [21, 38], [11, 30], [3, 22], // tronco e a taça, lado esquerdo
      [9, 12], [15, 17], [22, 9], [29, 15], [36, 8], [43, 13], [50, 6], [57, 13], [64, 8], [71, 15], [78, 9], [85, 17], [91, 12], // tufos
      [97, 22], [89, 30], [79, 38], [66, 46], [54, 54], [54, 96], // a taça, lado direito, e o tronco
    ],
  },
  // GO · Gota d'água (as águas termais): ponta fina em cima, fundo redondo. Largada no lado esquerdo, na parte larga.
  caldas_novas: {
    state: 'GO', what: 'gota d\'água', radius: 12, segments: 1990, cmax: 6, index: 7.214,
    pts: [
      [20, 68], [20, 58], [22, 47], [27, 36], [34, 25], [41, 16], [46, 8], [50, 1, 1.2], [54, 8], [59, 16], [66, 25], [73, 36], [78, 47], [80, 58],
      [80, 68, 22], [74, 82, 22], [62, 91, 22], [50, 94, 22], [38, 91, 22], [26, 82, 22],
    ],
  },
  // MS · Piraputanga de lado, nadando para a direita: cauda em V (largada no lobo de baixo), corpo, as barbatanas
  // (dorsal e adiposa em cima, anal e pélvica embaixo). Quinas arredondadas (round): pista de mata, e sem curva longa
  // as árvores ficam todas na beira e o cenário passa do orçamento (docs/PISTAS.md, "Pistas com desenho").
  bonito: {
    state: 'MS', what: 'peixe (piraputanga)', radius: 1.4, segments: 2020, cmax: 4.5, round: 2.4, index: 7.05, startAt: 0.1,
    pts: [
      [4, 76], [4, 59], [25, 47], [4, 35], [4, 18], // a cauda
      [24, 38], [28, 35], [33, 26], [39, 32], [47, 24], [52, 9], [62, 21], [74, 24], [88, 31], [101, 42], // costas, adiposa e dorsal
      [102, 48], [95, 56], [82, 62], [68, 66], [62, 75], [54, 68], [45, 66], [39, 73], [33, 62], [24, 55], // focinho, barriga, anal e pélvica
    ],
  },
  // MT · Jacaré visto de cima, cabeça para cima: focinho, olhos, as quatro patas, o rabo. Largada no lado esquerdo do corpo.
  transpantaneira: {
    state: 'MT', what: 'jacaré visto de cima', radius: 1.2, segments: 2130, cmax: 3, index: 0.297,
    pts: [
      [37, 58], [37, 42], [27, 41], [22, 38], [19, 33], [22, 30], [26, 31], [37, 35], // corpo e pata da frente esquerda
      [40, 30], [37, 24], [39, 19], [43, 16], [45, 4], [47, 2], [53, 2], [55, 4], [57, 16], [61, 19], [63, 24], [60, 30], // cabeça
      [63, 35], [74, 31], [78, 30], [81, 33], [78, 38], [73, 41], [63, 42], // pata da frente direita
      [63, 58], [74, 60], [79, 63], [81, 68], [78, 71], [73, 68], [62, 66], // pata de trás direita
      [60, 72], [57, 86], [53, 97], [49, 97], [45, 86], [40, 72], // rabo
      [38, 66], [27, 68], [22, 71], [19, 68], [21, 63], [26, 60], // pata de trás esquerda
    ],
  },
  // BA · Caravela de lado: casco com a popa alta (largada na popa) e a proa com o gurupés; três velas, a do meio maior.
  porto_seguro: {
    state: 'BA', what: 'caravela de Cabral', radius: 1.4, segments: 1870, cmax: 4.9, index: 5.019,
    pts: [
      [8, 78], [8, 50], [22, 50], [22, 56], // popa e o castelo
      [25, 56], [32, 30], [39, 54], // vela latina, triangular, atrás
      [47, 56], [45, 50], [44, 38], [45, 26], [58, 22], [70, 26], [71, 38], [70, 50], [68, 56], // vela grande, enfunada
      [80, 56], [78, 50], [77, 42], [78, 34], [85, 31], [92, 34], [93, 42], [92, 50], [90, 56], // vela da frente
      [95, 53], [101, 44], [98, 58], [90, 70], [78, 80], [60, 86], [36, 86], [18, 83], // proa, gurupés e casco
    ],
  },
  // SE · Caranguejo visto de cima, garras para cima: as garras abertas, os olhos, o corpo e três patas de cada lado.
  // Largada no lado de fora da garra esquerda.
  aracaju: {
    state: 'SE', what: 'caranguejo', radius: 1.3, segments: 1790, cmax: 5.3, index: 6.842,
    pts: [
      [6, 30], [6, 16], [10, 8], [15, 16], [17, 26], [22, 10], [26, 20], [25, 30], [31, 38], [35, 46], // garra esquerda
      [38, 46], [39, 39], [43, 39], [44, 46], [56, 46], [57, 39], [61, 39], [62, 46], [65, 46], // os olhos
      [69, 38], [75, 30], [74, 20], [78, 10], [83, 26], [85, 16], [90, 8], [94, 16], [94, 30], [86, 36], [76, 42], // garra direita
      [76, 50], [95, 50], [77, 58], [95, 63], [76, 66], [89, 78], [70, 74], // patas da direita
      [60, 84], [40, 84], [30, 74], [11, 78], [24, 66], [5, 63], [23, 58], [5, 50], [24, 50], [24, 42], [14, 36], // patas e braço da esquerda
    ],
  },
  // AL · Jangada: a vela triangular presa no mastro (largada no pé do mastro) e o casco baixo nas ondas.
  maceio: {
    state: 'AL', what: 'jangada', radius: 1.4, segments: 1760, cmax: 6, round: 1.25, index: 10.629,
    pts: [
      [38, 70], [38, 6], [42, 6], [52, 14], [62, 26], [71, 40], [78, 54], [82, 64], [82, 70], // mastro e vela
      [92, 70], [97, 65], [95, 76], [92, 82], // a proa
      [86, 88], [80, 82], [72, 88], [64, 82], [56, 88], [48, 82], [40, 88], [32, 82], [24, 88], [16, 82], [10, 88], // as ondas
      [5, 80], [5, 74], [8, 70], // a popa
    ],
  },
  // PE · Sombrinha de frevo: a cúpula com a ponteira, a borda recortada em gomos entre as varetas e o cabo (largada).
  olinda: {
    state: 'PE', what: 'sombrinha de frevo', radius: 1.4, segments: 1770, cmax: 5.8, index: 9.967,
    pts: [
      [47, 96], [47, 58], [41.5, 50], [36, 57], [30.5, 49], [25, 56], [19.5, 47], [14, 53], [9.5, 43], [5, 48], // gomos à esquerda
      [4, 36], [8, 27], [16, 19], [27, 12], [38, 7], [46, 5], [50, 1], [54, 5], [62, 7], [73, 12], [84, 19], [92, 27], [96, 36], // a cúpula
      [95, 48], [90.5, 43], [86, 53], [80.5, 47], [75, 56], [69.5, 49], [64, 57], [58.5, 50], [53, 58], [53, 96], // gomos e o cabo
    ],
  },
  // PB · Balão junino: a ponta, o corpo de papel com a franja recortada embaixo, a boca e a bucha acesa pendurada.
  // Largada no lado esquerdo do corpo.
  campina_grande: {
    state: 'PB', what: 'balão junino', radius: 1.4, segments: 1820, cmax: 6, index: 10.021,
    pts: [
      [20, 54], [20, 26], [32, 18], [45, 10], [50, 2], [55, 10], [68, 18], [80, 26], [80, 54], // a ponta e o corpo
      [77, 62], [73, 56], [69, 62], [65, 56], [56, 76], [56, 80], [52, 80], // franja direita e a boca
      [55, 87], [54, 92], [50, 97], [46, 92], [45, 87], [48, 80], // a bucha acesa
      [44, 80], [44, 76], [35, 56], [31, 62], [27, 56], [23, 62], // a boca e a franja esquerda
    ],
  },
  // RN · O maior cajueiro do mundo, de lado: a copa larga e baixa cheia de tufos e os galhos que descem até o chão, com
  // os vãos em arco embaixo. (O caju — fruto e castanha — não se leu numa linha só: saiu bolota, sino e coelho.)
  // Largada no pé da ponta esquerda.
  cajueiro_pirangi: {
    state: 'RN', what: 'cajueiro de Pirangi', radius: 2.4, segments: 2030, cmax: 5.1, index: 10.424,
    pts: [
      [2, 48], [2, 32], // a ponta esquerda
      ...bumps([[5, 24], [19, 21], [34, 14], [50, 10], [66, 14], [81, 21], [95, 24]], 7), // os tufos da copa
      [98, 32], [98, 48], // a ponta direita
      [94, 48], ...bulge([94, 48], [83, 48], -7), [83, 48], [77, 48], ...bulge([77, 48], [59, 48], -13), [59, 48], [44, 48], // vãos e o tronco
      ...bulge([44, 48], [27, 48], -12), [27, 48], [21, 48], ...bulge([21, 48], [9, 48], -7), [9, 48],
    ],
  },
  // CE · A lua crescente das falésias, pontas para a direita: o dorso é um arco do círculo de fora e o vão, um arco
  // de um círculo deslocado para a direita; as duas pontas são as quinas vivas. Largada no dorso, à esquerda.
  canoa_quebrada: {
    state: 'CE', what: 'lua crescente', radius: 6, segments: 1950, cmax: 6, round: 1.25, index: 10.034,
    pts: [
      [5, 57], [5, 43], ...circle(50, 50, 45, 195, 300, 8), [78.93, 15.53, 0.8], // o dorso de cima e a ponta
      ...circle(64, 50, 37.55, 283, 77, 15), [78.93, 84.47, 0.8], ...circle(50, 50, 45, 60, 165, 8), // o vão e o dorso de baixo
    ],
  },
  // PI · Capivara de lado, olhando para a direita: corpo de barril, cabeça de focinho rombudo, a orelhinha e quatro
  // patas curtas. Largada no traseiro.
  serra_capivara: {
    state: 'PI', what: 'capivara', radius: 1.4, segments: 1890, cmax: 6, round: 1.25, index: 10.29,
    pts: [
      [8, 62], [8, 42], [12, 33], [22, 27], [40, 25], [56, 26], [64, 24], [67, 18], [72, 18], [74, 24], [84, 25], // costas e orelha
      [93, 29], [97, 36], [97, 52], [92, 58], [80, 61], [74, 67], // a cabeça e o focinho
      [72, 90], [65, 90], [65, 74], [60, 74], [60, 90], [54, 90], [54, 74], // patas da frente
      [33, 76], [33, 90], [27, 90], [27, 76], [21, 76], [21, 90], [14, 90], [14, 74], [9, 69], // barriga e patas de trás
    ],
  },
  // MA · Cabeça do boi (bumba-meu-boi) de frente: os chifres abrindo e subindo, as orelhas para fora, a cara comprida
  // até o focinho. Largada na bochecha esquerda.
  sao_luis: {
    state: 'MA', what: 'cabeça de boi (bumba-meu-boi)', radius: 1.4, segments: 2070, cmax: 6, round: 1.25, index: 10.237,
    pts: [
      [38, 60], [38, 47], [22, 47], [15, 43], [21, 39], [36, 39], // bochecha e orelha esquerda
      [28, 35], [18, 29], [11, 19], [9, 9], [11, 3], [15, 10], [20, 20], [28, 26], [39, 29], // chifre esquerdo
      [50, 27], [61, 29], [72, 26], [80, 20], [85, 10], [89, 3], [91, 9], [89, 19], [82, 29], [72, 35], // testa e chifre direito
      [64, 39], [79, 39], [85, 43], [78, 47], [62, 47], [62, 60], // orelha e bochecha direita
      [58, 76], [60, 84], [55, 92], [45, 92], [40, 84], [42, 76], // a cara e o focinho
    ],
  },
  // PA · Mercado de Ferro do Ver-o-Peso de frente: o prédio comprido e as quatro torres de telhado pontudo. Largada na
  // parede da esquerda.
  belem: {
    state: 'PA', what: 'Mercado de Ferro do Ver-o-Peso', radius: 1.4, segments: 1800, cmax: 6, round: 2, index: 10.617,
    pts: [
      [3, 88], [3, 52], [9, 52], [9, 30], [13.5, 12], [18, 30], [18, 52], // parede e torre da esquerda
      [33, 52], [33, 34], [37.5, 16], [42, 34], [42, 52], [58, 52], [58, 34], [62.5, 16], [67, 34], [67, 52], // torres do meio
      [82, 52], [82, 30], [86.5, 12], [91, 30], [91, 52], [97, 52], [97, 88], // torre e parede da direita
    ],
  },
  // AM · Teatro Amazonas: a cúpula em sino com a lanterna e a agulha, sobre o tambor e o prédio largo. Largada na parede
  // da esquerda.
  manaus: {
    state: 'AM', what: 'cúpula do Teatro Amazonas', radius: 1.4, segments: 2110, cmax: 6, round: 1.75, index: 10.637,
    pts: [
      [4, 90], [4, 62], [32, 62], [32, 52], // prédio e tambor
      [30, 50], [29, 42], [31, 33], [36, 25], [42, 19], [45, 16], [45, 10], [50, 0], [55, 10], [55, 16], // cúpula, lanterna e agulha
      [58, 19], [64, 25], [69, 33], [71, 42], [70, 50], [68, 52], [68, 62], [96, 62], [96, 90], // cúpula e prédio
    ],
  },
  // AP · Planta da Fortaleza de São José: o quadrado das muralhas e os quatro baluartes em ponta de lança nos cantos
  // (flanco, face, ponta, face, flanco). Largada na muralha oeste.
  macapa: {
    state: 'AP', what: 'Fortaleza de São José', radius: 1.4, segments: 1750, cmax: 5.4, round: 2, index: 8.746,
    pts: [
      [22, 66], [22, 34], [12, 34], [4, 4], [34, 12], [34, 22], // muralha oeste e baluarte noroeste
      [66, 22], [66, 12], [96, 4], [88, 34], [78, 34], // baluarte nordeste
      [78, 66], [88, 66], [96, 96], [66, 88], [66, 78], // baluarte sudeste
      [34, 78], [34, 88], [4, 96], [12, 66], // baluarte sudoeste
    ],
  },
  // RR · Tepui: o planalto achatado com as rochas, os paredões com uma saliência e as encostas até a base. Largada no
  // paredão oeste.
  monte_roraima: {
    state: 'RR', what: 'tepui (Monte Roraima)', radius: 1.4, segments: 2040, cmax: 6, round: 1.25, index: 13.011,
    pts: [
      [16, 56], [16, 40], [20, 40], [20, 22], // paredão oeste
      [31, 22], [33, 27], [36, 22], [44, 22], [46, 25], [49, 22], [58, 22], [60, 27], [63, 22], [71, 22], [73, 25], [76, 22], // o planalto
      [81, 22], [81, 32], [85, 32], [85, 54], // paredão leste
      [88, 60], [93, 72], [98, 88], [2, 88], [6, 76], [11, 64], [14, 58], // encostas e base
    ],
  },
  // RO · Locomotiva da Madeira-Mamoré de lado, indo para a direita: cabine, caldeira com o domo, chaminé, limpa-trilhos
  // e as rodas. Largada na traseira da cabine.
  porto_velho: {
    state: 'RO', what: 'locomotiva da Madeira-Mamoré', radius: 1.4, segments: 1960, cmax: 5.8, round: 1.25, index: 11.331,
    pts: [
      [6, 76], [6, 30], [2, 30], [2, 26], [32, 26], [32, 30], [28, 30], [28, 46], // cabine
      [50, 46], ...bulge([50, 46], [58, 46], 5, 3), [58, 46], [72, 46], // caldeira e domo
      [73, 28], [70, 24], [70, 20], [82, 20], [82, 24], [79, 28], [80, 46], // chaminé
      [86, 46], [90, 50], [91, 70], [98, 86], [88, 86], [88, 76], // frente e limpa-trilhos
      [84, 76], ...bulge([84, 76], [74, 76], 6, 3), [74, 76], [70, 76], ...bulge([70, 76], [55, 76], 9, 4), [55, 76], // rodas
      [51, 76], ...bulge([51, 76], [36, 76], 9, 4), [36, 76], [32, 76], ...bulge([32, 76], [17, 76], 9, 4), [17, 76],
    ],
  },
  // AC · A gameleira de Rio Branco (a figueira centenária que dá nome à pista): copa em domo cheia de tufos, tronco
  // grosso e a base alargando nas raízes, com os vãos entre elas. Largada no tronco, do lado esquerdo.
  rio_branco: {
    state: 'AC', what: 'gameleira', radius: 1.4, segments: 2080, cmax: 5, round: 1.75, index: 11.448,
    pts: [
      [41, 82], [41, 64], // o tronco, lado esquerdo
      ...bumps([[35, 65], [24, 64], [13, 59], [6, 48], [6, 33], [14, 19], [28, 9], [44, 4], [56, 4], [72, 9], [86, 19], [94, 33],
        [94, 48], [87, 59], [76, 64], [65, 65]], 6), // a copa
      [59, 64], [59, 82], [66, 90], [78, 96], [64, 96], ...bulge([64, 96], [57, 96], -4, 3), [57, 96], // tronco, raiz e vão da direita
      [43, 96], ...bulge([43, 96], [36, 96], -4, 3), [36, 96], [22, 96], [34, 90], // vão e raiz da esquerda
    ],
  },
  // TO · O sol da bandeira do Tocantins: doze raios em ponta, compridos e curtos alternados, em volta do disco.
  // Largada na borda de um raio (o sol é girado para ela ficar vertical).
  palmas: {
    state: 'TO', what: 'sol do Tocantins', radius: 1.2, segments: 1780, cmax: 6, round: 1.25, index: 13.84,
    pts: sun(50, 50, 31, 47, 40, 16, 7),
  },
};
