// Traça uma pista a partir de um DESENHO: recebe um polígono-alvo fechado (scripts/track-shapes.ts) e gera os `ops`
// (`pit`/`st`/`cv`/`hl`) cuja volta o minimapa desenha com essa forma; mostra a sobreposição contorno × alvo.
//
// Como o minimapa desenha (src/render/minimap.ts, `trackOutline`): integra o rumo segmento a segmento, girando
// `curve × k` e andando um passo, com `k` escolhido para a soma das curvas fechar uma volta (2π). Daí:
//   - cada quina do polígono vira um `cv`; o ângulo dela no desenho é proporcional à INTEGRAL da curva (curva ×
//     comprimento efetivo — o `cv` tem entrada e saída suaves de 1/4 do comprimento cada, ver builder.ts);
//   - escalar todas as curvas por um fator não muda o desenho (o k compensa); escalar todos os comprimentos também
//     não (a normalização compensa). Então o DESENHO (proporção entre os ângulos e entre os comprimentos) e a
//     DIFICULDADE (força da curva, `cmax`, e morros) se ajustam separadamente;
//   - o raio da quina no desenho é o comprimento do `cv` ÷ o ângulo: quina viva = `cv` curto (raio pequeno). E a curva
//     de cada quina sai inversa ao raio: quina mais redonda que as outras tem curva menor (abaixo de ~3,2, não freia);
//   - altura (`hl`, ou o desnível dentro do `cv`) não muda o contorno nem a IA: os morros acertam o índice técnico.
// Cada `cv` desloca a pista em relação à quina viva do polígono; o deslocamento (as "tangentes" da quina) é medido
// simulando o próprio perfil do builder e descontado das retas vizinhas, para o desenho cair em cima do alvo; o erro
// de arredondamento das curvas passa de uma quina para a seguinte, para o rumo de cada lado não entortar.
//
// Dificuldade: `cmax` (a curva da quina mais forte) foi escolhido em cada desenho pela velocidade média da IA medida na
// pista de antes (docs/PISTAS.md, "Pistas com desenho") — muitas quinas curtas de curva 6 fazem a IA frear e
// reacelerar sem parar, e a pista fica mais dura do que o índice técnico diz. `index` é o índice técnico total da
// pista de antes: o que a curva não dá vira morro.
//
// Cenário: na mata (tropical), desenho de quinas curtas e retas longas deixa as árvores todas na beira e passa do
// orçamento de triângulos (`sceneryCost`, abaixo); o relatório mostra o custo e avisa acima do teto.
//
// Uso:
//   npx tsx scripts/shape-to-track.ts <pista>              gera os ops a partir do desenho e mostra o resultado
//   npx tsx scripts/shape-to-track.ts <pista> --svg x.svg  idem, com a sobreposição em SVG
//   npx tsx scripts/shape-to-track.ts <pista> --apply      grava os ops na pista em src/core/track/tracks.ts
//   npx tsx scripts/shape-to-track.ts <pista> --current    mede a pista como está em tracks.ts (sem gerar)
//   npx tsx scripts/shape-to-track.ts --sheet x.svg        folha com o contorno de todas as pistas com desenho
import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { SEGMENT_LENGTH } from '../src/core/constants';
import { carDef } from '../src/core/data/cars';
import { holdableSpeedFraction } from '../src/core/sim/physics';
import { buildTrack } from '../src/core/track/builder';
import { trackDef } from '../src/core/track/tracks';
import type { Track, TrackDef, TrackOp } from '../src/core/types';
import { trackOutline } from '../src/render/minimap';
import { getModel } from '../src/render/scenery/catalog';
import { sceneryLayout } from '../src/render/scenery/layout';
import { modelTriangles } from '../src/render/scenery/vegetation';
import { SHAPES, type Shape } from './track-shapes';

type V = [number, number];

// ───────────────────────── perfil do `cv` (o mesmo de builder.ts) ─────────────────────────

/** Curvatura de cada segmento de um `cv(length, curve)`, como `addRoad` monta (entrada e saída quadráticas). */
export function curveProfile(length: number, curve: number): number[] {
  const enter = Math.max(1, Math.floor(length / 4));
  const hold = Math.max(1, length - 2 * enter);
  const out: number[] = [];
  for (let n = 0; n < enter; n++) out.push(curve * (n / enter) ** 2);
  for (let n = 0; n < hold; n++) out.push(curve);
  for (let n = 0; n < enter; n++) out.push(curve * (1 - n / enter) ** 2);
  return out;
}

/** Integral da curva de um `cv` de curva 1 (o "comprimento efetivo"). */
function effLength(length: number): number {
  return curveProfile(length, 1).reduce((a, b) => a + b, 0);
}

/**
 * Tangentes de uma quina: andando o perfil a partir do rumo 0 com o `k` da volta, quanto a pista avança no rumo de
 * entrada (`a`) e no de saída (`b`) até sair da curva — o lugar da quina viva fica a `a` do começo e `b` do fim.
 */
function cornerTangents(profile: number[], k: number): { a: number; b: number } {
  let h = 0; let x = 0; let y = 0;
  for (const c of profile) { h += c * k; x += Math.cos(h); y += Math.sin(h); }
  const s = Math.sin(h);
  if (Math.abs(s) < 1e-6) return { a: x / 2, b: x / 2 }; // reta (não acontece numa quina)
  const b = y / s;
  return { a: x - b * Math.cos(h), b };
}

// ───────────────────────── do desenho aos ops ─────────────────────────

export interface Plan {
  /** Comprimento e curva do `cv` de cada quina (0 = sem quina) e a reta de cada lado. */
  L: number[];
  C: number[];
  st: number[];
  /** A reta do 1º lado partida na largada: antes da linha (vai para o fim da volta) e depois dela (com o box). */
  before: number;
  after: number;
  /** Escala: segmentos por unidade do desenho. */
  scale: number;
  /** Ponto de largada, em unidades do desenho. */
  start: V;
  warnings: string[];
}

const angleOf = (d: V) => Math.atan2(d[1], d[0]);
const wrap = (a: number) => { while (a <= -Math.PI) a += 2 * Math.PI; while (a > Math.PI) a -= 2 * Math.PI; return a; };

/** O polígono do desenho: ângulo de cada quina, comprimento de cada lado e o raio pedido em cada quina. */
function geometry(shape: Shape) {
  const pts = shape.pts.map((p) => [p[0], p[1]] as V);
  const radii = shape.pts.map((p) => (p.length > 2 && p[2] !== undefined ? p[2] : shape.radius));
  const m = pts.length;
  const edge = (j: number): V => { const a = pts[j]; const b = pts[(j + 1) % m]; return [b[0] - a[0], b[1] - a[1]]; };
  const e0 = edge(0);
  if (Math.abs(e0[0]) > 1e-9 || e0[1] >= 0) throw new Error(`${shape.what}: o 1º lado (largada) tem de subir na vertical`);
  // Quina j: entre o lado j-1 e o lado j. Positivo = à direita (horário na tela, y para baixo).
  const theta = pts.map((_, j) => wrap(angleOf(edge(j)) - angleOf(edge((j - 1 + m) % m))));
  const total = theta.reduce((a, b) => a + b, 0);
  if (Math.abs(total - 2 * Math.PI) > 1e-6) throw new Error(`${shape.what}: o polígono tem de ser horário (giro total ${(total * 180 / Math.PI).toFixed(1)}°)`);
  for (let j = 0; j < m; j++) if (Math.abs(Math.abs(theta[j]) - Math.PI) < 0.02) throw new Error(`${shape.what}: quina ${j} dá meia-volta; use duas quinas`);
  const ell = pts.map((_, j) => Math.hypot(...edge(j)));
  return { pts, radii, m, theta, ell, perim: ell.reduce((a, b) => a + b, 0), e0 };
}

/**
 * Do polígono aos `cv` e retas. `radiusScale` multiplica todos os raios (quinas mais redondas = mais trecho em
 * curva); `cmax` é a curva da quina mais forte. Nenhum dos dois muda a proporção do desenho além do arredondamento.
 */
export function planShape(shape: Shape, radiusScale = 1, cmax = shape.cmax): Plan {
  const { pts, radii, m, theta, ell, perim, e0 } = geometry(shape);
  const warnings: string[] = [];
  // Todas as quinas crescem juntas: a curva é inversa ao raio no desenho (curva ∝ 1 ÷ raio), então uma quina mais
  // redonda que as outras fica com curva menor e, abaixo de ~3,2, deixa de contar no índice técnico.
  const r = radii.map((x) => x * radiusScale);
  // 1) Comprimento de cada `cv`: raio × ângulo, em segmentos (a escala S, segmentos por unidade, é estimada aqui e
  //    resolvida exata no passo 3; o raio no desenho fica um tiquinho diferente, sem importância).
  let S = shape.segments / perim;
  let L: number[] = [];
  for (let iter = 0; iter < 4; iter++) {
    L = theta.map((t, j) => (Math.abs(t) < 1e-9 ? 0 : Math.max(4, Math.round(r[j] * Math.abs(t) * S))));
    // Quina de raio r: a curva (r·θ) no lugar das duas tangentes (2·r·tg(θ/2)) que ela corta do polígono.
    const cut = theta.reduce((a, t, j) => a + (L[j] === 0 ? 0 : 2 * r[j] * S * Math.tan(Math.abs(t) / 2) - L[j]), 0);
    S = (shape.segments + cut) / perim;
  }
  // 2) Curva de cada quina: proporcional ao ângulo ÷ comprimento efetivo, escalada para a mais forte valer cmax, com
  //    duas casas decimais. O que vira ângulo no desenho é a integral (curva × comprimento efetivo); o arredondamento
  //    é compensado no comprimento e o erro que sobra passa para a quina seguinte (difusão de erro): o rumo de cada
  //    lado fica certo e o erro não acumula ao longo da volta.
  const ideal = theta.map((t, j) => (L[j] === 0 ? 0 : t / effLength(L[j])));
  const g = cmax / Math.max(...ideal.map(Math.abs));
  const C = ideal.map((x) => x * g);
  let wantCum = 0; let gotCum = 0;
  for (let j = 1; j <= m; j++) {
    const jj = j % m; // a quina 0 fecha a volta
    if (L[jj] === 0) continue;
    wantCum += C[jj] * effLength(L[jj]);
    const want = wantCum - gotCum;
    let best = { e: Infinity, l: L[jj], c: C[jj] };
    for (let l = Math.max(4, L[jj] - 3); l <= L[jj] + 3; l++) {
      const exact = want / effLength(l);
      for (const c of [Math.floor(exact * 100) / 100, Math.ceil(exact * 100) / 100]) {
        if (c === 0 || Math.abs(c) > cmax + 1e-9 || Math.sign(c) !== Math.sign(C[jj])) continue;
        const e = Math.abs(c * effLength(l) - want) + 0.05 * Math.abs(l - L[jj]);
        if (e < best.e) best = { e, l, c };
      }
    }
    L[jj] = best.l; C[jj] = best.c;
    gotCum += best.c * effLength(best.l);
  }
  // 3) Tangentes de cada quina (com o k da volta) e a escala exata para dar o comprimento pedido.
  const T = C.reduce((acc, c, j) => acc + (L[j] === 0 ? 0 : c * effLength(L[j])), 0);
  const k = (2 * Math.PI) / T;
  const tang = L.map((l, j) => (l === 0 ? { a: 0, b: 0 } : cornerTangents(curveProfile(l, C[j]), k)));
  const arcs = L.reduce((a, b) => a + b, 0);
  const cuts = tang.reduce((a, t) => a + t.a + t.b, 0);
  S = (shape.segments - arcs + cuts) / perim;
  const straights = ell.map((l, j) => l * S - tang[j].b - tang[(j + 1) % m].a);
  // 4) Retas inteiras somando o comprimento exato: arredonda e distribui a sobra pelos maiores restos.
  let st = straights.map((s) => Math.floor(s));
  let missing = shape.segments - arcs - st.reduce((a, b) => a + b, 0);
  const order = straights.map((s, j) => [s - Math.floor(s), j] as [number, number]).sort((a, b) => b[0] - a[0]);
  for (let i = 0; missing > 0; i = (i + 1) % m) { st[order[i][1]] += 1; missing--; }
  st.forEach((s, j) => { if (s < 0) warnings.push(`lado ${j} ficou com reta ${s} (quinas grandes demais para ele)`); });
  st = st.map((s) => Math.max(0, s));

  // Largada no 1º lado, numa fração dele; o box (pit 40) logo depois da linha.
  const before = Math.round(st[0] * (shape.startAt ?? 0));
  const after = st[0] - before;
  if (after < 40) warnings.push(`a reta da largada tem ${after} segmentos depois da linha (o box pede 40)`);
  // Onde a largada cai no desenho: depois da quina 0 (tangente b) e da parte "antes" da reta.
  const u0: V = [e0[0] / ell[0], e0[1] / ell[0]];
  const d0 = (tang[0].b + before) / S;
  return { L, C, st, before, after, scale: S, start: [pts[0][0] + u0[0] * d0, pts[0][1] + u0[1] * d0], warnings };
}

/**
 * Morros: [lado, comprimento, altura] na reta do lado, espaçados por igual; e `curveHills` (quina → desnível), a subida
 * ou descida dentro do próprio `cv`. Nenhum dos dois mexe no desenho.
 */
export function emitOps(plan: Plan, hillList: Array<[number, number, number]>, curveHills: Record<number, number> = {}): TrackOp[] {
  const { L, C, st, before, after } = plan;
  const m = L.length;
  const hills = new Map<number, Array<[number, number]>>();
  for (const [j, l, h] of hillList) { const list = hills.get(j) ?? []; list.push([l, h]); hills.set(j, list); }
  const straightOps = (j: number, length: number): TrackOp[] => {
    const list = hills.get(j) ?? [];
    const out: TrackOp[] = [];
    const hl = list.reduce((a, [l]) => a + l, 0);
    if (hl > length) { plan.warnings.push(`morros do lado ${j} (${hl}) não cabem na reta (${length})`); return length > 0 ? [{ op: 'straight', length }] : []; }
    const free = length - hl;
    let used = 0;
    for (let i = 0; i < list.length; i++) {
      const gap = Math.floor(free / (list.length + 1));
      if (gap > 0) out.push({ op: 'straight', length: gap });
      out.push({ op: 'hill', length: list[i][0], height: list[i][1] });
      used += gap + list[i][0];
    }
    if (length - used > 0) out.push({ op: 'straight', length: length - used });
    return out;
  };
  const cvOp = (j: number): TrackOp[] => (L[j] === 0 ? [] : [curveHills[j]
    ? { op: 'curve', length: L[j], curve: C[j], hill: curveHills[j] }
    : { op: 'curve', length: L[j], curve: C[j] }]);
  const ops: TrackOp[] = [{ op: 'pit', length: 40 }];
  if (after - 40 > 0) ops.push(...straightOps(0, after - 40));
  for (let j = 1; j < m; j++) { ops.push(...cvOp(j)); ops.push(...straightOps(j, st[j])); }
  ops.push(...cvOp(0));
  if (before > 0) ops.push({ op: 'straight', length: before });
  return ops;
}

/**
 * Morros automáticos para a parte de inclinação do índice (`slopeIndex`): `hl` nas retas mais longas, de até 120
 * segmentos e no máximo 0,45 de altura por comprimento (a mais íngreme do jogo é 0,47) — o índice soma 40 × altura ÷
 * volta. Altura não muda o desenho nem a física (a IA não vê morro): só a vista de quem dirige. Desenho quase sem
 * reta (a lua, a gota): o que faltar sobe e desce dentro das curvas mais longas, em pares (sobe numa, desce na
 * seguinte), até 0,4 de altura por comprimento — cada uma soma 20 × desnível ÷ volta.
 */
function autoHills(shape: Shape, plan: Plan, slopeIndex: number): { hills: Array<[number, number, number]>; curveHills: Record<number, number> } {
  const curveHills: Record<number, number> = {};
  const hills = straightHills(shape, plan, slopeIndex);
  const got = hills.reduce((a, [, , h]) => a + 2 * h, 0); // Σ|desnível| das retas
  let rest = Math.round((slopeIndex * shape.segments) / 20) - got;
  if (rest > 2) {
    const curves = plan.L.map((l, j) => [l, j] as [number, number]).filter(([l]) => l >= 8).sort((a, b) => b[0] - a[0]);
    for (let i = 0; i + 1 < curves.length && rest > 1; i += 2) {
      const h = Math.min(Math.floor(0.4 * curves[i][0]), Math.floor(0.4 * curves[i + 1][0]), Math.ceil(rest / 2));
      if (h <= 0) break;
      curveHills[curves[i][1]] = h; curveHills[curves[i + 1][1]] = -h;
      rest -= 2 * h;
    }
    if (rest > 2) plan.warnings.push(`nem com as curvas os morros dão a inclinação pedida (faltam ${rest} de desnível)`);
  }
  return { hills, curveHills };
}

function straightHills(shape: Shape, plan: Plan, slopeIndex: number): Array<[number, number, number]> {
  let need = Math.round((slopeIndex * shape.segments) / 40);
  if (need <= 0) return [];
  // Vagas: um morro a cada ~126 segmentos de reta (até 3 por reta); na reta da largada, só depois do box.
  const slots: Array<[number, number, number]> = [];
  plan.st.forEach((s, j) => {
    const free = j === 0 ? plan.after - 40 : s;
    if (free < 24) return;
    const k = Math.min(3, Math.max(1, Math.floor((free - 6) / 126)));
    const l = Math.min(120, Math.floor((free - 6) / k)) & ~1;
    for (let i = 0; i < k; i++) slots.push([j, l, Math.floor(0.45 * l)]);
  });
  slots.sort((a, b) => b[1] - a[1]);
  let n = Math.min(slots.length, 2);
  while (n < slots.length && slots.slice(0, n).reduce((a, [, , h]) => a + h, 0) < need) n++;
  const out: Array<[number, number, number]> = [];
  for (let i = 0; i < n && need > 0; i++) {
    const share = Math.min(slots[i][2], Math.ceil((need * slots[i][2]) / slots.slice(i, n).reduce((a, [, , h]) => a + h, 0)));
    if (share > 0) out.push([slots[i][0], slots[i][1], share]);
    need -= share;
  }
  return out;
}

/**
 * O desenho com a dificuldade acertada. A força da quina mais forte (`cmax`) e o arredondamento (`round` × o raio) vêm
 * do desenho e da dificuldade MEDIDA (a velocidade média da IA na corrida inteira, docs/PISTAS.md, "Balanceamento"):
 * quina curta de curva 6 faz a IA frear e reacelerar a cada uma, então um desenho cheio de quinas fica bem mais duro
 * do que o índice técnico diz — com a curva das quinas acertada pela velocidade de antes, a pista joga como jogava.
 * O índice técnico total (`index`, o da pista antes do desenho, que segura a rampa das copas) é completado com morros:
 * o que a curva não dá, a inclinação dá (morro conta no índice, mas não freia a IA).
 */
export function solveShape(id: string, shape: Shape): { plan: Plan; ops: TrackOp[]; radiusScale: number; cmax: number; curve: number; slope: number } {
  const rs = shape.round ?? 1;
  const plan = planShape(shape, rs, shape.cmax);
  const curve = technicalIndex({ ...trackDef(id), ops: emitOps(plan, []) }).curve;
  const slope = Math.max(0, shape.index - curve);
  if (curve > shape.index + 0.05) plan.warnings.push(`só a curva já dá ${curve.toFixed(2)} de índice (o pedido é ${shape.index})`);
  const auto = shape.hills ? { hills: shape.hills, curveHills: {} } : slope > 0.005 ? autoHills(shape, plan, slope) : { hills: [], curveHills: {} };
  return { plan, ops: emitOps(plan, auto.hills, auto.curveHills), radiusScale: rs, cmax: shape.cmax, curve, slope };
}

// ───────────────────────── medidas ─────────────────────────

/** Índice técnico (o mesmo de tests/track.test.ts), separado em curva e inclinação. */
export function technicalIndex(def: TrackDef): { total: number; curve: number; slope: number } {
  const t = buildTrack(def);
  const car = carDef('falcao');
  let loss = 0; let slope = 0;
  for (const s of t.segments) { loss += 1 - holdableSpeedFraction(car, s.curve); slope += Math.abs(s.y1 - s.y0) / SEGMENT_LENGTH; }
  const n = t.segments.length;
  return { total: (100 * loss) / n + (20 * slope) / n, curve: (100 * loss) / n, slope: (20 * slope) / n };
}

/** O contorno cru (antes de normalizar) e a transformação que o minimapa aplica — para pôr o alvo no mesmo quadro. */
function rawFrame(def: TrackDef, size: number) {
  const segs = buildTrack(def).segments;
  const n = segs.length;
  let total = 0; for (const s of segs) total += s.curve;
  const k = (2 * Math.PI) / Math.abs(total);
  let h = -Math.PI / 2; let x = 0; let y = 0;
  const raw: V[] = [];
  for (let i = 0; i < n; i++) { raw.push([x, y]); h += segs[i].curve * k; x += Math.cos(h); y += Math.sin(h); }
  const fixed = raw.map(([px, py], i) => [px - (x * i) / n, py - (y * i) / n] as V);
  const xs = fixed.map((p) => p[0]); const ys = fixed.map((p) => p[1]);
  const minX = Math.min(...xs); const maxX = Math.max(...xs); const minY = Math.min(...ys); const maxY = Math.max(...ys);
  const margin = size * 0.08;
  const scale = (size - 2 * margin) / Math.max(maxX - minX, maxY - minY);
  const offX = (size - (maxX - minX) * scale) / 2; const offY = (size - (maxY - minY) * scale) / 2;
  return { drift: Math.hypot(x, y), map: ([px, py]: V): V => [offX + (px - minX) * scale, offY + (py - minY) * scale] };
}

const distToSeg = (p: V, a: V, b: V) => {
  const dx = b[0] - a[0]; const dy = b[1] - a[1];
  const t = Math.max(0, Math.min(1, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / (dx * dx + dy * dy || 1)));
  return Math.hypot(p[0] - a[0] - t * dx, p[1] - a[1] - t * dy);
};
const distToPoly = (p: V, poly: V[]) => Math.min(...poly.map((a, i) => distToSeg(p, a, poly[(i + 1) % poly.length])));

/** Pares de trechos do contorno que se cruzam (contorno limpo = nenhum). */
export function selfCrossings(pts: V[]): number {
  const n = pts.length; let count = 0;
  const cross = (o: V, a: V, b: V) => (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0]);
  for (let i = 0; i < n; i++) {
    const a = pts[i]; const b = pts[(i + 1) % n];
    for (let j = i + 2; j < n; j++) {
      if (i === 0 && j === n - 1) continue;
      const c = pts[j]; const d = pts[(j + 1) % n];
      if (cross(c, d, a) * cross(c, d, b) < 0 && cross(a, b, c) * cross(a, b, d) < 0) count++;
    }
  }
  return count;
}

/** Menor distância entre dois pontos do contorno que estão longe um do outro na volta (traço que quase encosta). */
export function nearestGap(pts: V[], apart = 12): number {
  const n = pts.length; let best = Infinity;
  for (let i = 0; i < n; i++) for (let j = i + apart; j < n; j++) {
    if (n - (j - i) < apart) continue;
    const d = Math.hypot(pts[i][0] - pts[j][0], pts[i][1] - pts[j][1]);
    if (d < best) best = d;
  }
  return best;
}

export function formatOps(ops: TrackOp[]): string {
  return ops.map((o) => {
    switch (o.op) {
      case 'pit': return `pit(${o.length})`;
      case 'straight': return `st(${o.length})`;
      case 'hill': return `hl(${o.length}, ${o.height})`;
      case 'curve': return o.hill ? `cv(${o.length}, ${o.curve}, ${o.hill})` : `cv(${o.length}, ${o.curve})`;
      case 's': return `ss(${o.length}, ${o.curve})`;
    }
  }).join(', ');
}

const pathOf = (pts: V[], dx = 0, dy = 0) => pts.map(([x, y], i) => `${i === 0 ? 'M' : 'L'}${(x + dx).toFixed(2)},${(y + dy).toFixed(2)}`).join(' ') + ' Z';

function svgOf(outline: V[], target: V[] | null, title: string): string {
  const t = target ? `<path d="${pathOf(target)}" fill="none" stroke="#4cf" stroke-width="0.6" stroke-dasharray="1.5 1"/>` : '';
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100" width="500" height="500"><title>${title}</title>`
    + `<rect width="100" height="100" fill="#123"/>${t}<path d="${pathOf(outline)}" fill="none" stroke="#fd3" stroke-width="1.6" stroke-linejoin="round"/>`
    + `<circle cx="${outline[0][0]}" cy="${outline[0][1]}" r="2" fill="#f44"/></svg>\n`;
}

function ascii(outline: V[], target: V[] | null, cols = 70): string {
  const rows = Math.round(cols / 2);
  const grid = Array.from({ length: rows }, () => new Array<string>(cols).fill(' '));
  const cell = (x: number, y: number): [number, number] => [
    Math.min(rows - 1, Math.max(0, Math.floor((y / 100) * rows))), Math.min(cols - 1, Math.max(0, Math.floor((x / 100) * cols))),
  ];
  const draw = (pts: V[], ch: string) => {
    for (let i = 0; i < pts.length; i++) {
      const [x0, y0] = pts[i]; const [x1, y1] = pts[(i + 1) % pts.length];
      const steps = Math.max(8, Math.ceil(Math.hypot(x1 - x0, y1 - y0)));
      for (let k = 0; k <= steps; k++) { const [r, c] = cell(x0 + ((x1 - x0) * k) / steps, y0 + ((y1 - y0) * k) / steps); grid[r][c] = ch; }
    }
  };
  if (target) draw(target, '.');
  draw(outline, '#');
  const [sr, sc] = cell(outline[0][0], outline[0][1]);
  grid[sr][sc] = 'S';
  return grid.map((r) => r.join('').replace(/\s+$/, '')).join('\n');
}

/** Relatório de uma definição: comprimento, índice, curva máxima, cruzamentos, proporção e (com alvo) o erro. */
export function report(def: TrackDef, target?: { poly: V[]; start: V; scale: number }) {
  const track = buildTrack(def);
  const outline = trackOutline(track, 100) as V[];
  const idx = technicalIndex(def);
  const xs = outline.map((p) => p[0]); const ys = outline.map((p) => p[1]);
  const w = Math.max(...xs) - Math.min(...xs); const h = Math.max(...ys) - Math.min(...ys);
  const f = rawFrame(def, 100);
  let poly: V[] | null = null; let fit: { mean: number; max: number } | null = null;
  if (target) {
    // O minimapa começa para cima no segmento 0 = a largada do desenho.
    poly = target.poly.map(([x, y]) => f.map([(x - target.start[0]) * target.scale, (y - target.start[1]) * target.scale]));
    const ds = outline.map((p) => distToPoly(p, poly as V[]));
    fit = { mean: ds.reduce((a, b) => a + b, 0) / ds.length, max: Math.max(...ds) };
  }
  const maxCurve = Math.max(...track.segments.map((s) => Math.abs(s.curve)));
  return { track, outline, poly, idx, w, h, fit, maxCurve, drift: f.drift, crossings: selfCrossings(outline), gap: nearestGap(outline) };
}

/** Teto do custo do cenário por segmento (a trava de tests/scenery-forma.test.ts, "orçamento"). */
const SCENERY_BUDGET = 900;

/**
 * Custo do cenário por segmento: triângulos do modelo de perto de cada objeto, sem marcos (a conta do teste). Em bioma
 * de mata ele depende do traçado: do lado de dentro de curva longa (≥ 18 de 80 segmentos acima de 1,2) as árvores
 * perto da pista não nascem (`innerCurve` em src/render/scenery/layout.ts). Desenho de quinas curtas e retas longas
 * não tem curva assim e fica com a mata inteira — arredondar as quinas (`round`) ou juntar detalhes resolve.
 */
export function sceneryCost(track: Track): number {
  const layout = sceneryLayout(track);
  const tris = new Map<string, number>();
  let sum = 0;
  for (const list of layout.bySeg) for (const p of list) {
    const m = layout.models[p.model];
    if (m.startsWith('lm:')) continue;
    let t = tris.get(m);
    if (t === undefined) { t = modelTriangles(getModel(m)); tris.set(m, t); }
    sum += t;
  }
  return sum / track.segments.length;
}

function printReport(id: string, r: ReturnType<typeof report>, quiet = false) {
  if (!quiet) console.log(ascii(r.outline, r.poly));
  console.log(`${id}: ${r.track.segments.length} segmentos · índice ${r.idx.total.toFixed(2)} (curvas ${r.idx.curve.toFixed(2)} + morros ${r.idx.slope.toFixed(2)})`
    + ` · curva máx. ${r.maxCurve.toFixed(1)} · largura/altura ${(r.w / r.h).toFixed(2)} · cruzamentos ${r.crossings}`
    + ` · menor vão ${r.gap.toFixed(1)} · desvio de fechamento ${r.drift.toFixed(1)}${r.fit ? ` · erro médio ${r.fit.mean.toFixed(2)} máx. ${r.fit.max.toFixed(2)} (em 100)` : ''}`);
}

/** Grava `ops` na linha da pista em tracks.ts. */
function applyOps(id: string, ops: TrackOp[]): void {
  const file = fileURLToPath(new URL('../src/core/track/tracks.ts', import.meta.url));
  const lines = readFileSync(file, 'utf8').split('\n');
  const at = lines.findIndex((l) => l.includes(`id: '${id}',`));
  if (at < 0) throw new Error(`pista ${id} não achada em tracks.ts`);
  if (!/^\s*ops: \[.*\],\s*$/.test(lines[at + 1])) throw new Error(`a linha de ops de ${id} não está logo depois do id`);
  lines[at + 1] = lines[at + 1].replace(/ops: \[.*\],\s*$/, `ops: [${formatOps(ops)}],`);
  writeFileSync(file, lines.join('\n'));
}

function sheet(file: string): void {
  const ids = ['cuia_gaucha', ...Object.keys(SHAPES)];
  const cols = 6; const cell = 190; const rows = Math.ceil(ids.length / cols);
  let body = '';
  ids.forEach((id, i) => {
    const def = trackDef(id);
    const pts = trackOutline(buildTrack(def), 140) as V[];
    const ox = (i % cols) * cell + 25; const oy = Math.floor(i / cols) * cell + 12;
    const what = id === 'cuia_gaucha' ? 'cuia de chimarrão' : SHAPES[id].what;
    const state = id === 'cuia_gaucha' ? 'RS' : SHAPES[id].state;
    body += `<rect x="${ox - 17}" y="${oy - 6}" width="${cell - 16}" height="${cell - 14}" rx="10" fill="#16283a"/>`
      + `<path d="${pathOf(pts, ox, oy)}" fill="none" stroke="#ffd54a" stroke-width="2.6" stroke-linejoin="round"/>`
      + `<circle cx="${(ox + pts[0][0]).toFixed(1)}" cy="${(oy + pts[0][1]).toFixed(1)}" r="3.5" fill="#ff5a4a"/>`
      + `<text x="${ox + 70}" y="${oy + 152}" font-size="12" font-weight="700" fill="#fff" text-anchor="middle">${state} · ${def.name}</text>`
      + `<text x="${ox + 70}" y="${oy + 166}" font-size="11" fill="#9cc3e6" text-anchor="middle">${what}</text>`;
  });
  const W = cols * cell + 10; const H = rows * cell + 46;
  writeFileSync(file, `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${W} ${H}" width="${W}" height="${H}" font-family="sans-serif">`
    + `<rect width="${W}" height="${H}" fill="#0b1724"/><text x="${W / 2}" y="${H - 16}" font-size="14" fill="#cfe3f5" text-anchor="middle">`
    + `Nitro Crew · Expedição Brasil: o minimapa de uma pista por estado (ponto vermelho = largada)</text>${body}</svg>\n`);
  console.log(`folha em ${file} (${ids.length} pistas)`);
}

function main(): void {
  const args = process.argv.slice(2);
  const opt = (name: string) => { const i = args.indexOf(name); return i >= 0 ? args[i + 1] : undefined; };
  if (args.includes('--sheet')) { sheet(opt('--sheet') ?? 'desenhos.svg'); return; }
  const valued = new Set(['--svg', '--overlay', '--sheet']);
  const ids = args.filter((a, i) => !a.startsWith('--') && !valued.has(args[i - 1] ?? ''));
  if (ids.length === 0) { console.log(`pistas com desenho: ${Object.keys(SHAPES).join(', ')}`); return; }
  const quiet = args.includes('--quiet') || ids.length > 1;
  const tiles: string[] = [];
  for (const id of ids) {
    const before = trackDef(id);
    if (args.includes('--current')) {
      const r = report(before);
      printReport(id, r, quiet);
      tiles.push(svgOf(r.outline, null, id));
      continue;
    }
    const shape = SHAPES[id];
    if (!shape) throw new Error(`sem desenho para ${id} em scripts/track-shapes.ts`);
    const { plan, ops, radiusScale, cmax, curve, slope } = solveShape(id, shape);
    const def: TrackDef = { ...before, ops };
    const r = report(def, { poly: shape.pts.map((p) => [p[0], p[1]] as V), start: plan.start, scale: plan.scale });
    printReport(id, r, quiet);
    const was = technicalIndex(before);
    console.log(`  em tracks.ts: ${buildTrack(before).segments.length} segmentos · índice ${was.total.toFixed(2)} (curvas ${was.curve.toFixed(2)} + morros ${was.slope.toFixed(2)})`);
    console.log(`  raio × ${radiusScale.toFixed(2)} · curva da quina mais forte ${cmax} · ${(100 * plan.L.reduce((a, b) => a + b, 0) / shape.segments).toFixed(0)}% da volta em curva · índice pedido ${shape.index}: curva ${curve.toFixed(2)} + morros ${slope.toFixed(2)}`);
    const cost = sceneryCost(r.track);
    console.log(`  cenário: ${cost.toFixed(0)} triângulos por segmento (teto ${SCENERY_BUDGET})`);
    if (cost > SCENERY_BUDGET) plan.warnings.push(`o cenário passa do orçamento (${cost.toFixed(0)} > ${SCENERY_BUDGET} por segmento): arredonde as quinas (round) ou junte detalhes`);
    for (const w of plan.warnings) console.log(`  AVISO: ${w}`);
    if (!quiet) console.log(`ops: [${formatOps(ops)}],`);
    tiles.push(svgOf(r.outline, r.poly, id));
    if (args.includes('--apply')) { applyOps(id, ops); console.log(`  gravado em tracks.ts (${id})`); }
  }
  const svg = opt('--svg'); if (svg) { writeFileSync(svg, tiles[0]); console.log(`SVG em ${svg}`); }
  // Várias pistas lado a lado (contorno em amarelo, alvo tracejado), para conferir de uma vez.
  const overlay = opt('--overlay');
  if (overlay) {
    const cols = Math.min(4, tiles.length);
    const body = tiles.map((t, i) => t
      .replace('viewBox="0 0 100 100" width="500" height="500"', `x="${(i % cols) * 102}" y="${Math.floor(i / cols) * 112}" viewBox="0 0 100 110" width="100" height="110"`)
      .replace(/<title>(.*?)<\/title>/, '<rect width="100" height="110" fill="#123"/><text x="50" y="106" font-size="5" fill="#fff" text-anchor="middle">$1</text>')).join('');
    const rows = Math.ceil(tiles.length / cols);
    writeFileSync(overlay, `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${cols * 102} ${rows * 112}" width="${cols * 300}" height="${rows * 330}" font-family="sans-serif">${body}</svg>\n`);
    console.log(`sobreposição em ${overlay}`);
  }
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) main();
