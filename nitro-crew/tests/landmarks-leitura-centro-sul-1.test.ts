// Leitura dos marcos do Sudeste, Sul e Centro-Oeste da primeira leva (docs/VISUAL.md, "Marcos turísticos" › "Leitura"):
// pedido do dono (04/10/2026) — "mais nítido as coisas de cada cidade (...) e as referências ficarem também bonitas e
// entendíveis". Cada marco foi avaliado como o jogador o vê (folha de contato de longe e captura no ponto de
// aproximação, a 150–250 m, na névoa); os de nota ≤ 3 foram refeitos, e aqui fica travado o que os fazia ilegíveis.
//
// A régua é a vista de frente (o lado que o jogador vê, +X; z-buffer; uma célula por `cell` metros), como em
// tests/front-view.ts, mas guardando também o material da face da frente: a luz (`glow`) é o que se vê à noite. A
// conta de tela por trás dos limites: com a câmera de perseguição (FOV vertical ~72°, 720 linhas), 1 m a 250 m são
// ~2 px e a 150 m ~3,3 px — uma peça de menos de ~1,5 m some de longe, e um marco de perto com menos de ~20 m de
// altura cabe em ~40 px.
import * as THREE from 'three';
import { describe, expect, it } from 'vitest';
import type { Model } from '../src/render/scenery/geom';
import { LANDMARKS } from '../src/render/scenery/landmarks';

interface Cell { color: THREE.Color; mat: string }
interface View { cells: Array<Cell | null>; cols: number; rows: number; cell: number; z0: number; y0: number; at(c: number, r: number): Cell | null }

/** Vista de frente (cópia de tests/front-view.ts com o material da face): todas as partes que pintam o modelo. */
function view(model: Model, cell = 1): View {
  const parts = model.parts.filter((p) => p.mat !== 'beacon' && p.mat !== 'cone');
  const box = new THREE.Box3();
  for (const p of parts) { p.geometry.computeBoundingBox(); if (p.geometry.boundingBox) box.union(p.geometry.boundingBox); }
  const z0 = Math.floor(box.min.z); const y0 = Math.floor(Math.max(0, box.min.y));
  const cols = Math.ceil((box.max.z - z0) / cell) + 1; const rows = Math.ceil((box.max.y - y0) / cell) + 1;
  const depth = new Float32Array(cols * rows).fill(-Infinity);
  const cells: Array<Cell | null> = new Array(cols * rows).fill(null);
  const a = new THREE.Vector3(); const b = new THREE.Vector3(); const c = new THREE.Vector3();
  for (const p of parts) {
    const pos = p.geometry.getAttribute('position'); const col = p.geometry.getAttribute('color');
    const idx = p.geometry.index; const n = idx ? idx.count : pos.count;
    for (let t = 0; t + 2 < n; t += 3) {
      const i0 = idx ? idx.getX(t) : t; const i1 = idx ? idx.getX(t + 1) : t + 1; const i2 = idx ? idx.getX(t + 2) : t + 2;
      a.fromBufferAttribute(pos, i0); b.fromBufferAttribute(pos, i1); c.fromBufferAttribute(pos, i2);
      const u = [(a.z - z0) / cell, (b.z - z0) / cell, (c.z - z0) / cell];
      const v = [(a.y - y0) / cell, (b.y - y0) / cell, (c.y - y0) / cell];
      const den = (v[1] - v[2]) * (u[0] - u[2]) + (u[2] - u[1]) * (v[0] - v[2]);
      if (Math.abs(den) < 1e-9) continue;
      const cMin = Math.max(0, Math.floor(Math.min(...u))); const cMax = Math.min(cols - 1, Math.ceil(Math.max(...u)));
      const rMin = Math.max(0, Math.floor(Math.min(...v))); const rMax = Math.min(rows - 1, Math.ceil(Math.max(...v)));
      const color = col ? new THREE.Color().fromBufferAttribute(col as THREE.BufferAttribute, i0).convertLinearToSRGB() : new THREE.Color(1, 0, 1);
      const face: Cell = { color, mat: p.mat };
      for (let r = rMin; r <= rMax; r++) {
        for (let q = cMin; q <= cMax; q++) {
          const pu = q + 0.5; const pv = r + 0.5;
          const w0 = ((v[1] - v[2]) * (pu - u[2]) + (u[2] - u[1]) * (pv - v[2])) / den;
          const w1 = ((v[2] - v[0]) * (pu - u[2]) + (u[0] - u[2]) * (pv - v[2])) / den;
          const w2 = 1 - w0 - w1;
          if (w0 < 0 || w1 < 0 || w2 < 0) continue;
          const x = w0 * a.x + w1 * b.x + w2 * c.x; const k = r * cols + q;
          if (x > depth[k]) { depth[k] = x; cells[k] = face; }
        }
      }
    }
  }
  return { cells, cols, rows, cell, z0, y0, at: (q, r) => cells[r * cols + q] };
}

const views = new Map<string, View>();
/** Vista de frente do marco (guardada: cada teste mede várias coisas na mesma). */
function front(id: string, cell = 1): View {
  const key = `${id}@${cell}`;
  let v = views.get(key);
  if (!v) { v = view(LANDMARKS[id].build(), cell); views.set(key, v); }
  return v;
}

function hsl(c: THREE.Color): { h: number; s: number; l: number } {
  const o = { h: 0, s: 0, l: 0 };
  new THREE.Color().setRGB(c.r, c.g, c.b, THREE.LinearSRGBColorSpace).getHSL(o, THREE.LinearSRGBColorSpace);
  return o;
}

type Is = (c: Cell) => boolean;
const any: Is = () => true;
const red: Is = (c) => { const { h, s, l } = hsl(c.color); return (h < 0.04 || h > 0.95) && s > 0.45 && l > 0.25; };
/** Branco: claro e de pouca cor (pela diferença entre os canais: perto do branco o S do HSL infla). */
const white: Is = (c) => { const { r, g, b } = c.color; return hsl(c.color).l > 0.75 && Math.max(r, g, b) - Math.min(r, g, b) < 0.12; };
const green: Is = (c) => { const { h, s } = hsl(c.color); return h > 0.17 && h < 0.45 && s > 0.15; };
const blue: Is = (c) => { const { h, s, l } = hsl(c.color); return h > 0.5 && h < 0.66 && s > 0.45 && l < 0.75; };
const dark: Is = (c) => hsl(c.color).l < 0.15;
const tile: Is = (c) => { const { h, s, l } = hsl(c.color); return h < 0.08 && s > 0.35 && l > 0.3 && l < 0.7; };
const lit: Is = (c) => c.mat === 'glow';
/** Água (a régua das Cataratas, tests/landmarks-leitura.test.ts): branco ou azul-claro pouco saturado. */
const water: Is = (c) => { const { h, s, l } = hsl(c.color); return l >= 0.8 || (h > 0.48 && h < 0.64 && l > 0.68 && s < 0.8); };

/** Área (m²) das células da vista que passam no filtro. */
function area(v: View, f: Is): number {
  let n = 0;
  for (const c of v.cells) if (c && f(c)) n++;
  return n * v.cell * v.cell;
}
const frac = (v: View, f: Is): number => area(v, f) / area(v, any);

/** Altura (m) da célula mais alta da silhueta. */
function top(v: View): number {
  for (let r = v.rows - 1; r >= 0; r--) for (let q = 0; q < v.cols; q++) if (v.at(q, r)) return v.y0 + (r + 1) * v.cell;
  return 0;
}

/** Maior corrida horizontal (m) de células que passam no filtro na altura y, entre z0 e z1. */
function widthAt(v: View, y: number, f: Is, z0 = -Infinity, z1 = Infinity): number {
  const r = Math.floor((y - v.y0) / v.cell);
  let best = 0; let run = 0;
  for (let q = 0; q < v.cols; q++) {
    const z = v.z0 + (q + 0.5) * v.cell; const c = z >= z0 && z <= z1 ? v.at(q, r) : null;
    if (c && f(c)) { run++; best = Math.max(best, run); } else run = 0;
  }
  return best * v.cell;
}

/** Maior largura (m) de células que passam no filtro, em qualquer altura. */
function maxWidth(v: View, f: Is): number {
  let best = 0;
  for (let r = 0; r < v.rows; r++) best = Math.max(best, widthAt(v, v.y0 + (r + 0.5) * v.cell, f));
  return best;
}

/** Topo de cada coluna ocupada (m). */
function tops(v: View): number[] {
  const out: number[] = [];
  for (let q = 0; q < v.cols; q++) for (let r = v.rows - 1; r >= 0; r--) if (v.at(q, r)) { out.push(v.y0 + (r + 1) * v.cell); break; }
  return out;
}

/** Fração das colunas ocupadas cuja célula de cima passa no filtro. */
function topIs(v: View, f: Is): number {
  let n = 0; let ok = 0;
  for (let q = 0; q < v.cols; q++) {
    for (let r = v.rows - 1; r >= 0; r--) {
      const c = v.at(q, r);
      if (!c) continue;
      n++; if (f(c)) ok++;
      break;
    }
  }
  return ok / n;
}

const median = (xs: number[]): number => { const s = [...xs].sort((p, q) => p - q); return s[Math.floor(s.length / 2)] ?? 0; };

describe('leitura dos marcos do centro-sul: o que faz cada um ser reconhecido de longe', () => {
  it('MASP (Sampa, à noite): o pórtico vermelho é luz — apagado, a caixa de janelas acesas lia como mais um prédio', () => {
    const v = front('masp');
    expect(frac(v, (c) => red(c) && lit(c)), 'vermelho aceso na face (antes 0)').toBeGreaterThanOrEqual(0.3);
    expect(frac(v, red), 'de dia o vermelho continua dominando (antes 0,41)').toBeGreaterThanOrEqual(0.35);
    // O vão livre: debaixo da caixa, entre os pilares, quase só vazio (a praça e os canteiros ficam abaixo de 2 m).
    let n = 0; let empty = 0;
    for (let y = 2.5; y < 7; y++) for (let z = -30; z <= 30; z++) { n++; if (!v.at(Math.floor(z - v.z0), Math.floor(y - v.y0))) empty++; }
    expect(empty / n, 'o vão livre debaixo da caixa').toBeGreaterThanOrEqual(0.7);
  });

  it('Convento da Penha: o convento branco coroa o penhasco — antes, 20 m de telhado sobre 330 m de pedra (lia como mais um morro)', () => {
    const v = front('convento_penha');
    const building: Is = (c) => white(c) || tile(c);
    expect(area(v, building), 'parede branca e telhado na face, m² (antes 336)').toBeGreaterThanOrEqual(900);
    expect(maxWidth(v, building), 'largura do convento, m (antes 32)').toBeGreaterThanOrEqual(45);
    // O alto da silhueta (os 10 m de cima) é o convento, não a pedra.
    const t = top(v); let n = 0; let b = 0;
    for (let r = Math.floor(t - 10 - v.y0); r < v.rows; r++) for (let q = 0; q < v.cols; q++) { const c = v.at(q, r); if (!c) continue; n++; if (building(c)) b++; }
    expect(b / n, 'parede e telhado nos 10 m de cima').toBeGreaterThanOrEqual(0.5);
  });

  it('Ponte Hercílio Luz: as torres e a corrente grossas o bastante para não sumirem a 300 m', () => {
    const v = front('ponte_hercilio_luz', 0.25);
    // A corrente é o que fica por cima entre as torres: a espessura da corrida mais alta de cada coluna (a mediana
    // tira os tirantes, que descem dela até o tabuleiro).
    let towerQ = 0; let towerTop = 0;
    for (let q = 0; q < v.cols; q++) for (let r = v.rows - 1; r >= 0; r--) if (v.at(q, r)) { if (r > towerTop) { towerTop = r; towerQ = q; } break; }
    const towerZ = Math.abs(v.z0 + (towerQ + 0.5) * v.cell);
    const runs: number[] = [];
    for (let q = 0; q < v.cols; q++) {
      const z = Math.abs(v.z0 + (q + 0.5) * v.cell);
      if (z < towerZ * 0.25 || z > towerZ * 0.85) continue;
      let r = v.rows - 1;
      while (r >= 0 && !v.at(q, r)) r--;
      let n = 0;
      while (r >= 0 && v.at(q, r)) { n++; r--; }
      runs.push(n * v.cell);
    }
    expect(median(runs), 'espessura da corrente, m (antes 1,25)').toBeGreaterThanOrEqual(2.2);
    // A torre treliçada: de uma perna à outra (pode ser vazada), a 60% da altura, perto da coluna mais alta.
    const H = top(v); const r = Math.floor((H * 0.6 - v.y0) / v.cell);
    let first = Infinity; let last = -Infinity;
    for (let q = 0; q < v.cols; q++) {
      if (Math.abs(q - towerQ) * v.cell > 15 || !v.at(q, r)) continue;
      first = Math.min(first, q); last = Math.max(last, q);
    }
    expect((last - first + 1) * v.cell, 'largura da torre a 60% da altura, m (antes 3,5)').toBeGreaterThanOrEqual(6);
  });

  it('Cuia de chimarrão: grande, com a bomba que se vê — antes 17 m e a bomba de 0,4 m (sumia: um vaso)', () => {
    const v = front('cuia_chimarrao', 0.25);
    const H = top(v);
    expect(H, 'altura, m (antes 17,3)').toBeGreaterThanOrEqual(24);
    expect(widthAt(v, H * 0.9, any), 'a bomba no alto, m de largura (antes 0,5)').toBeGreaterThanOrEqual(1.4);
  });

  it('Araucária: a copa em taça domina (antes troncos finos com tufos soltos: lia como pinheiro qualquer)', () => {
    const v = front('araucaria');
    expect(frac(v, green), 'copa verde na silhueta (antes 0,52)').toBeGreaterThanOrEqual(0.7);
    expect(area(v, any), 'silhueta, m² (antes 171)').toBeGreaterThanOrEqual(450);
  });

  it('Cachoeira dos Veadeiros: a água domina a face, riscada, e o alto é cerrado — antes um paredão em caixa com uma risca de 9 m', () => {
    const v = front('cachoeira_veadeiros');
    expect(frac(v, water), 'água na face (antes 0,07)').toBeGreaterThanOrEqual(0.25);
    expect(topIs(v, green), 'colunas com cerrado no topo (antes 0,74)').toBeGreaterThanOrEqual(0.85);
    const tones = new Map<string, number>(); let w = 0;
    for (const c of v.cells) if (c && water(c)) { w++; const t = c.color.getHexString(); tones.set(t, (tones.get(t) ?? 0) + 1); }
    expect(Math.max(...tones.values()) / w, 'o tom de água mais comum (painel liso = um tom só)').toBeLessThanOrEqual(0.5);
  });

  it('Gruta do Lago Azul: o azul da boca se vê, e o morro não é caixa (antes topo reto a 33–34 m e 33 m² de azul)', () => {
    const v = front('gruta_lago_azul');
    expect(frac(v, blue), 'azul do lago na face (antes 0,016)').toBeGreaterThanOrEqual(0.06);
    expect(frac(v, dark), 'a boca escura (antes 0,13: o lago aceso agora ocupa o pé dela)').toBeGreaterThanOrEqual(0.08);
    const ts = tops(v); const mid = ts.slice(Math.floor(ts.length * 0.1), Math.ceil(ts.length * 0.9));
    const mean = mid.reduce((s, x) => s + x, 0) / mid.length;
    const sd = Math.sqrt(mid.reduce((s, x) => s + (x - mean) ** 2, 0) / mid.length);
    expect(sd, 'desvio do topo no miolo, m (caixa: 0,5)').toBeGreaterThanOrEqual(2);
  });

  it('Portal da Transpantaneira: o pórtico de troncos tem corpo de longe (antes 13,5 m e 182 m² de silhueta)', () => {
    const v = front('portal_transpantaneira', 0.25);
    expect(top(v), 'altura, m').toBeGreaterThanOrEqual(16);
    expect(area(v, any), 'silhueta, m²').toBeGreaterThanOrEqual(400);
    expect(widthAt(v, 6, any, -40, 40), 'o tronco do pilar, m (antes 2)').toBeGreaterThanOrEqual(2.4);
  });

  it('Ninho de tuiuiú: a árvore e os tuiuiús brancos do tamanho de ler a 150 m (antes 99 m² e 5,5 m² de branco)', () => {
    const v = front('tuiuiu_ninho', 0.25);
    expect(area(v, any), 'silhueta, m²').toBeGreaterThanOrEqual(220);
    expect(area(v, white), 'branco dos tuiuiús, m²').toBeGreaterThanOrEqual(12);
  });

  it('Buritizal: a vereda de buritis enche o quadro (antes 4 palmeiras em 32 m, 306 m²)', () => {
    const v = front('buriti');
    expect(area(v, any), 'silhueta, m²').toBeGreaterThanOrEqual(600);
    expect(tops(v).length * v.cell, 'largura ocupada (colunas com alguma coisa), m').toBeGreaterThanOrEqual(50);
  });

  it('Estufa do Jardim Botânico: o ferro branco desenha a estufa e o vidro não some no céu (antes 81% azul-céu)', () => {
    const v = front('estufa_jardim_botanico', 0.5);
    const sky: Is = (c) => { const { h, l } = hsl(c.color); return h > 0.45 && h < 0.62 && l > 0.7; };
    expect(frac(v, white), 'ferro branco na face (antes 0,11)').toBeGreaterThanOrEqual(0.25);
    expect(frac(v, sky), 'vidro azul-claro da cor do céu (antes 0,81)').toBeLessThanOrEqual(0.5);
    expect(top(v), 'altura, m (antes 21,5)').toBeGreaterThanOrEqual(27);
  });

  it('Serra Verde Express: o trem amarelo e verde em cima do viaduto se vê de longe (antes 269 m² de trem)', () => {
    const v = front('trem_serra_verde', 0.5);
    const train: Is = (c) => {
      const { h, s, l } = hsl(c.color);
      return (h > 0.08 && h < 0.17 && s > 0.6 && l > 0.35) || (h > 0.37 && h < 0.45 && s > 0.3 && l < 0.4) || (h > 0.08 && h < 0.17 && s > 0.3 && l > 0.75);
    };
    expect(area(v, train), 'trem na face, m²').toBeGreaterThanOrEqual(700);
  });

  it('Igreja açoriana: maior e com o azul das molduras de longe (antes 182 m², 16% de azul)', () => {
    const v = front('igreja_acoriana');
    expect(area(v, any), 'silhueta, m²').toBeGreaterThanOrEqual(330);
    expect(frac(v, blue), 'azul na face').toBeGreaterThanOrEqual(0.2);
  });

  it('Casario colonial: os sobrados sobem a ladeira acima da mata da beira (antes 17 m de altura)', () => {
    const v = front('casario_colonial');
    // O telhado de barro mais alto (o fim da ladeira), não o morro de trás.
    let roof = 0;
    for (let r = 0; r < v.rows; r++) for (let q = 0; q < v.cols; q++) { const c = v.at(q, r); if (c && tile(c)) roof = Math.max(roof, v.y0 + (r + 1) * v.cell); }
    expect(roof, 'altura do telhado mais alto, m').toBeGreaterThanOrEqual(24);
    expect(area(v, any), 'silhueta, m² (antes 849)').toBeGreaterThanOrEqual(1100);
  });

  it('Catedral de Brasília: a coroa acende por dentro ao entardecer e à noite (antes só o anel do topo)', () => {
    const v = front('catedral_brasilia');
    expect(frac(v, lit), 'face acesa (antes 0,008)').toBeGreaterThanOrEqual(0.35);
  });
});
