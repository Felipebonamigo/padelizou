// Os desenhos das pistas em curva (docs/PISTAS.md, "Pistas com desenho"): silhuetas de cartum em traço contínuo —
// arcos e Béziers, como um ícone ilustrado — que scripts/shape-to-track.ts transforma nos `ops` de
// src/core/track/tracks.ts seguindo a curvatura do traço inteiro (não só as quinas de um polígono). Coordenadas da tela
// (x para a direita, y para BAIXO), num quadro de ~100; o contorno é HORÁRIO e `start` é um ponto num trecho reto que
// SOBE na vertical (o minimapa começa apontando para cima; os 40 segmentos do box ficam ali). Desenho simétrico: só a
// metade da direita, do alto do eixo (x = 50) até o pé dele, e `sym` espelha.

export type P = [number, number];

/** Caneta: traço denso (de ~0,25 em 0,25) que os comandos acrescentam a partir do ponto atual. */
export class Pen {
  readonly pts: P[];
  constructor(x: number, y: number) { this.pts = [[x, y]]; }
  private get at(): P { return this.pts[this.pts.length - 1]; }
  /** Reta até (x, y). */
  L(x: number, y: number): this {
    const [x0, y0] = this.at;
    const n = Math.max(1, Math.ceil(Math.hypot(x - x0, y - y0) / 0.25));
    for (let i = 1; i <= n; i++) this.pts.push([x0 + ((x - x0) * i) / n, y0 + ((y - y0) * i) / n]);
    return this;
  }
  /** Bézier cúbica com os controles (x1, y1) e (x2, y2) até (x, y). */
  C(x1: number, y1: number, x2: number, y2: number, x: number, y: number): this {
    const [x0, y0] = this.at;
    const n = Math.max(16, Math.ceil((Math.hypot(x1 - x0, y1 - y0) + Math.hypot(x2 - x1, y2 - y1) + Math.hypot(x - x2, y - y2)) / 0.25));
    for (let i = 1; i <= n; i++) {
      const t = i / n; const u = 1 - t;
      this.pts.push([
        u * u * u * x0 + 3 * u * u * t * x1 + 3 * u * t * t * x2 + t * t * t * x,
        u * u * u * y0 + 3 * u * u * t * y1 + 3 * u * t * t * y2 + t * t * t * y,
      ]);
    }
    return this;
  }
  /** Bézier quadrática com o controle (x1, y1) até (x, y). */
  Q(x1: number, y1: number, x: number, y: number): this {
    const [x0, y0] = this.at;
    return this.C(x0 + ((x1 - x0) * 2) / 3, y0 + ((y1 - y0) * 2) / 3, x + ((x1 - x) * 2) / 3, y + ((y1 - y) * 2) / 3, x, y);
  }
  /**
   * Arco de círculo de centro (cx, cy) e raio r, do ângulo `a0` ao `a1` (graus; 0 = direita, 90 = BAIXO: crescente é
   * horário na tela). Começa no ponto atual (liga com uma reta se não estiver lá).
   */
  arc(cx: number, cy: number, r: number, a0: number, a1: number): this {
    const p0: P = [cx + r * Math.cos((a0 * Math.PI) / 180), cy + r * Math.sin((a0 * Math.PI) / 180)];
    if (Math.hypot(p0[0] - this.at[0], p0[1] - this.at[1]) > 1e-6) this.L(p0[0], p0[1]);
    const n = Math.max(4, Math.ceil(Math.abs(a1 - a0) / 1.5));
    for (let i = 1; i <= n; i++) {
      const a = ((a0 + ((a1 - a0) * i) / n) * Math.PI) / 180;
      this.pts.push([cx + r * Math.cos(a), cy + r * Math.sin(a)]);
    }
    return this;
  }
}

/**
 * Desenho simétrico no eixo x = 50: `right` é a metade da direita, do alto do eixo até o pé dele (horário); a da
 * esquerda é o espelho, de volta do pé até o alto.
 */
export function sym(right: P[]): P[] {
  const left = right.slice(1, -1).reverse().map(([x, y]) => [100 - x, y] as P);
  return [...right, ...left];
}

/**
 * Polígono fechado de quinas arredondadas: cada vértice [x, y, raio?] vira uma curva (quadrática) que começa `raio` antes
 * dele e termina `raio` depois; o raio padrão é `r`. Serve aos objetos de arestas retas (o MASP, a fortaleza, o
 * convento) que o cartum só amacia. O primeiro vértice não pode ficar no meio de uma reta: o traço começa logo depois
 * da quina dele e fecha no mesmo ponto.
 */
export function softPoly(corners: Array<[number, number, number?]>, r: number): P[] {
  const n = corners.length;
  const cut = (i: number) => {
    const [x, y, ri = r] = corners[i];
    const [px, py] = corners[(i + n - 1) % n]; const [nx, ny] = corners[(i + 1) % n];
    const din = Math.hypot(x - px, y - py); const dout = Math.hypot(nx - x, ny - y);
    const rr = Math.min(ri, din / 2, dout / 2);
    return { c: [x, y] as P, a: [x - ((x - px) / din) * rr, y - ((y - py) / din) * rr] as P, b: [x + ((nx - x) / dout) * rr, y + ((ny - y) / dout) * rr] as P };
  };
  const first = cut(0);
  const pen = new Pen(first.b[0], first.b[1]);
  for (let i = 1; i < n; i++) { const k = cut(i); pen.L(k.a[0], k.a[1]).Q(k.c[0], k.c[1], k.b[0], k.b[1]); }
  pen.L(first.a[0], first.a[1]).Q(first.c[0], first.c[1], first.b[0], first.b[1]);
  return pen.pts;
}

export interface Art {
  /** Estado (sigla) e o que o desenho representa — vão para a folha de desenhos. */
  state: string;
  what: string;
  /** O traço fechado, horário. */
  pts: P[];
  /** Onde fica a largada: o ponto do traço mais perto dele, num trecho reto que sobe. */
  start: P;
}

// ───────────────────────────── os desenhos ─────────────────────────────

/** RJ · Cristo Redentor: braços abertos com a túnica caindo das mangas, cabeça redonda, o manto que abre até o pedestal. */
function cristo(): Art {
  const r = new Pen(50, 7.5)
    .arc(50, 16, 8.5, -90, 56) // cabeça
    .C(55.2, 25.4, 56.5, 26.4, 59, 26.9) // pescoço até o ombro
    .C(69, 27.1, 80, 27.2, 88.5, 27.6) // braço, por cima
    .C(93.8, 27.8, 94.6, 35.6, 88.6, 35.8) // a mão
    .C(77, 36.4, 59.5, 41, 58.3, 55) // a manga caindo até a cintura
    .C(57.6, 64, 59.8, 76, 61.6, 85.5) // o manto abrindo
    .L(64, 85.5).L(64, 100.5).L(50, 100.5); // pedestal (reta de 15: os 40 segmentos do box cabem retos)
  return { state: 'RJ', what: 'Cristo Redentor', pts: sym(r.pts), start: [36, 97] };
}

/**
 * SP · MASP: a caixa larga suspensa nos dois pórticos, com o vão livre embaixo. A viga do teto passa um pouco dos pilares
 * (o que faz o prédio ler como prédio e não como mesa). Largada no pilar esquerdo, do lado de fora.
 */
function masp(): Art {
  const pts = softPoly([
    [4, 84], [4, 25], [0, 25, 1.8], [0, 17, 2.4], [100, 17, 2.4], [100, 25, 1.8], [96, 25], // viga do teto e o pilar direito
    [96, 84], [87, 84], [87, 62, 4], [13, 62, 4], [13, 84], // o vão livre
  ], 3.4);
  return { state: 'SP', what: 'MASP', pts, start: [4, 76] };
}

/**
 * MG · Igreja da Pampulha, de perfil: a abóbada parabólica da nave e as três menores, em onda, com vãos arredondados
 * entre elas; a marquise fina que leva à torre em trapézio invertido (com o vão do chão por baixo). Largada na parede
 * esquerda da nave.
 */
function pampulha(): Art {
  const r = new Pen(3, 72).L(3, 52)
    .C(3, 0, 35, 0, 35, 52) // a nave
    .L(35, 58.9).arc(37.1, 58.9, 2.1, 180, 0) // o vão entre a nave e a 1ª menor
    .L(39.2, 50).C(39.2, 24, 51.2, 24, 51.2, 50) // 1ª menor
    .L(51.2, 61).arc(53.3, 61, 2.1, 180, 0)
    .L(55.4, 57).C(55.4, 38, 64.4, 38, 64.4, 57) // 2ª
    .L(64.4, 63.5).arc(66.5, 63.5, 2.1, 180, 0)
    .L(68.6, 60).C(68.6, 48, 75.6, 48, 75.6, 60) // 3ª
    .L(83.5, 60) // a marquise, por cima
    .L(81, 19).Q(80.6, 15, 84.5, 15).L(93.5, 15).Q(97.4, 15, 97, 19) // a torre: topo largo
    .L(92.5, 75.5).L(88.8, 75.5) // até o pé, estreito
    .L(84.5, 65).L(77, 65).L(77, 75.5) // o vão por baixo da marquise
    .L(6, 75.5).Q(3, 75.5, 3, 72);
  return { state: 'MG', what: 'Igreja da Pampulha', pts: r.pts, start: [3, 68] };
}

export const ART: Readonly<Record<string, () => Art>> = {
  copacabana: cristo,
  sampa_noite: masp,
  pampulha,
};
