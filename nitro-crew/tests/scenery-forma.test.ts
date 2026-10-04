// Cenário "menos quadrado" pela forma e pela cor (onda I, depois das normais suaves): as famílias redondas
// (árvore, conífera, palmeira, cacto, moita, pedra e as silhuetas de longe) não mostram as faces — nem pela cor
// (a mancha por face, `speckle`, acendia face sim, face não mesmo com a luz lisa) nem por gomos largos (copa de
// icosaedro de 20 faces, tronco de 6 lados). E o custo continua no orçamento. Tudo no Node, sem DOM.
import * as THREE from 'three';
import { describe, expect, it } from 'vitest';
import { getTrack, TRACKS } from '../src/core/track';
import { getModel } from '../src/render/scenery/catalog';
import { sceneryLayout } from '../src/render/scenery/layout';
import { sceneryShading } from '../src/render/scenery/smooth';
import { modelTriangles } from '../src/render/scenery/vegetation';

/** Famílias de forma redonda (o vinco de planta ou de pedra): o resto (construção, objeto de pista) pode ser caixa. */
const ROUND = /^(tree|pine|palm|cactus|bush|rock|termite|stack|searock|mesa|far):/;

const layouts = TRACKS.map((d) => ({ id: d.id, segs: getTrack(d.id).segments.length, layout: sceneryLayout(getTrack(d.id)) }));

const ids = (() => {
  const s = new Set<string>();
  for (const { layout } of layouts) for (const id of layout.models) if (ROUND.test(id) || id === 'termite') s.add(id);
  return [...s].sort();
})();

interface EdgeStats {
  /** Arestas lisas (duas faces, dobra menor que o vinco da família). */
  smooth: number;
  /** Maior salto de cor (relativo, por canal) entre as duas faces de uma aresta lisa, nos dois pontos dela. */
  maxJump: number;
}

/** Arestas lisas de uma malha não indexada (pontos soldados a 0,1 mm), com o salto de cor e a dobra de cada uma. */
function edgeStats(g: THREE.BufferGeometry, creaseDeg: number): EdgeStats {
  const pos = g.getAttribute('position');
  const col = g.getAttribute('color');
  const n = pos.count - (pos.count % 3);
  const key = (i: number) => `${Math.round(pos.getX(i) * 1e4)},${Math.round(pos.getY(i) * 1e4)},${Math.round(pos.getZ(i) * 1e4)}`;
  const ids = new Map<string, number>();
  const pid = new Int32Array(n);
  for (let i = 0; i < n; i++) {
    const k = key(i);
    let v = ids.get(k);
    if (v === undefined) { v = ids.size; ids.set(k, v); }
    pid[i] = v;
  }
  const fn: THREE.Vector3[] = [];
  const a = new THREE.Vector3(); const b = new THREE.Vector3(); const c = new THREE.Vector3();
  for (let f = 0; f < n / 3; f++) {
    a.fromBufferAttribute(pos, f * 3); b.fromBufferAttribute(pos, f * 3 + 1); c.fromBufferAttribute(pos, f * 3 + 2);
    fn.push(b.sub(a).cross(c.sub(a)));
  }
  // Aresta (pontos soldados) → os cantos de cada face, no sentido em que a face a percorre.
  const edges = new Map<string, Array<[number, number]>>();
  for (let f = 0; f < n / 3; f++) {
    if (fn[f].lengthSq() < 1e-14) continue; // face degenerada não conta
    for (let k = 0; k < 3; k++) {
      const i = f * 3 + k; const j = f * 3 + ((k + 1) % 3);
      const e = pid[i] < pid[j] ? `${pid[i]}:${pid[j]}` : `${pid[j]}:${pid[i]}`;
      const list = edges.get(e) ?? [];
      list.push([i, j]);
      edges.set(e, list);
    }
  }
  let smooth = 0; let maxJump = 0;
  const cos = Math.cos((creaseDeg * Math.PI) / 180);
  for (const list of edges.values()) {
    if (list.length !== 2) continue;
    const [[i0, j0], [i1, j1]] = list;
    const fa = fn[Math.floor(i0 / 3)].clone().normalize(); const fb = fn[Math.floor(i1 / 3)].clone().normalize();
    // Enrolamento coerente percorre a aresta em sentidos opostos; no mesmo sentido, a vizinha entra virada.
    const sameDir = pid[i0] === pid[i1];
    if (sameDir) fb.negate();
    const d = Math.max(-1, Math.min(1, fa.dot(fb)));
    if (d < cos) continue;
    smooth++;
    const pairs: Array<[number, number]> = sameDir ? [[i0, i1], [j0, j1]] : [[i0, j1], [j0, i1]];
    for (const [u, v] of pairs) {
      for (let ch = 0; ch < 3; ch++) {
        const cu = col.getComponent(u, ch); const cv = col.getComponent(v, ch);
        maxJump = Math.max(maxJump, Math.abs(cu - cv) / Math.max(cu, cv, 0.02));
      }
    }
  }
  return { smooth, maxJump };
}

function statsOf(id: string): EdgeStats {
  const crease = sceneryShading(id).crease;
  const out: EdgeStats = { smooth: 0, maxJump: 0 };
  for (const p of getModel(id).parts) {
    if (p.mat !== 'flat') continue;
    const s = edgeStats(p.geometry, crease);
    out.smooth += s.smooth; out.maxJump = Math.max(out.maxJump, s.maxJump);
  }
  return out;
}

/**
 * Quanto a luz lisa "mente" sobre a forma: o ângulo médio (graus) entre a normal suave de cada canto liso e a normal
 * da própria face. Malha grossa sob luz lisa é redonda por dentro e de gomos no contorno: icosaedro de 20 faces 37°,
 * tronco de 6 lados 30°; icosaedro subdividido ~19°, cilindro de 12 lados 15°. Cada canto pesa a área da face × o
 * ângulo dele — o mesmo peso da normal suave (`smoothNormals`): o bico de um cone (o canto fino de 10 faces em leque)
 * tem a normal para cima em qualquer resolução, e isso é a ponta arredondada à luz, não gomo; pela área só, a camada de
 * conífera de 10 lados "mentia" mais que a de 7.
 */
function normalLie(parts: ReadonlyArray<{ geometry: THREE.BufferGeometry; mat: string }>): number {
  let w = 0; let sum = 0;
  const P = [new THREE.Vector3(), new THREE.Vector3(), new THREE.Vector3()];
  const fn = new THREE.Vector3(); const e1 = new THREE.Vector3(); const e2 = new THREE.Vector3(); const nv = new THREE.Vector3();
  for (const p of parts) {
    if (p.mat !== 'flat') continue;
    const pos = p.geometry.getAttribute('position'); const nor = p.geometry.getAttribute('normal');
    for (let f = 0; f < pos.count / 3; f++) {
      for (let k = 0; k < 3; k++) P[k].fromBufferAttribute(pos, f * 3 + k);
      fn.crossVectors(e1.subVectors(P[1], P[0]), e2.subVectors(P[2], P[0]));
      const area = fn.length() / 2;
      if (area < 1e-12) continue;
      fn.normalize();
      for (let k = 0; k < 3; k++) {
        const ang = (Math.acos(Math.min(1, Math.abs(nv.fromBufferAttribute(nor, f * 3 + k).dot(fn)))) * 180) / Math.PI;
        if (ang < 0.5) continue; // canto vivo (caixa, beiral): fora da conta
        const corner = e1.subVectors(P[(k + 1) % 3], P[k]).angleTo(e2.subVectors(P[(k + 2) % 3], P[k]));
        w += area * corner; sum += ang * area * corner;
      }
    }
  }
  return w > 0 ? sum / w : 0;
}

describe('cenário redondo: cor', () => {
  it('nas famílias redondas a cor corre lisa entre faces vizinhas (sem mancha por face nas arestas lisas)', () => {
    expect(ids.length).toBeGreaterThan(60);
    const bad: string[] = [];
    for (const id of ids) {
      const s = statsOf(id);
      if (s.maxJump > 0.01) bad.push(`${id}: salto de ${(s.maxJump * 100).toFixed(1)}% em aresta lisa`);
    }
    expect(bad, `${bad.length} modelos`).toEqual([]);
  }, 60000);

  it('o detector reprova a mancha por face (speckle) e aceita a mancha por ponto', async () => {
    const { ico, paint, speckle, mottle } = await import('../src/render/scenery/geom');
    expect(edgeStats(speckle(paint(ico(1, 1), '#3a8a3a'), 0.05, 7), 60).maxJump).toBeGreaterThan(0.02);
    expect(edgeStats(mottle(paint(ico(1, 1), '#3a8a3a'), 0.08, 7), 60).maxJump).toBeLessThan(1e-4);
  });
});

describe('cenário redondo: forma', () => {
  it('as famílias redondas não mostram gomos largos (a normal lisa fica, em média, a ≤ 22° da face; pedra ≤ 18°)', () => {
    const bad: string[] = [];
    for (const id of ids) {
      if (id.startsWith('far:')) continue; // silhueta de longe: poucos pixels, ≤ 60 triângulos
      const lie = normalLie(getModel(id).parts);
      const cap = /^(rock|searock):/.test(id) ? 18 : 22;
      if (lie > cap) bad.push(`${id}: ${lie.toFixed(1)}° (máx. ${cap}°)`);
    }
    expect(bad, `${bad.length} modelos`).toEqual([]);
  }, 60000);

  it('o detector separa a malha grossa da fina (icosaedro de 20 faces × subdividido)', async () => {
    const { ico, paint } = await import('../src/render/scenery/geom');
    const { smoothNormals } = await import('../src/render/normals');
    const lieOf = (detail: number) => normalLie([{ geometry: smoothNormals(paint(ico(1, detail), '#3a8a3a'), 60), mat: 'flat' }]);
    expect(lieOf(0)).toBeGreaterThan(30);
    expect(lieOf(1)).toBeLessThan(22);
  });
});

describe('cenário redondo: orçamento', () => {
  it('custo do cenário por segmento (triângulos do modelo de perto de cada objeto, sem marcos) ≤ 900 em toda pista', () => {
    // A medida que acompanha o quadro (docs/DESEMPENHO.md, "Cenário redondo"): antes da onda I a pior pista era
    // 764 (Pororoca), média 405; depois 856, média 484. O teto segura a próxima rodada de "mais redondo".
    const tris = new Map<string, number>();
    const bad: string[] = [];
    for (const { id, segs, layout } of layouts) {
      let sum = 0;
      for (const list of layout.bySeg) for (const p of list) {
        const m = layout.models[p.model];
        if (m.startsWith('lm:')) continue;
        let t = tris.get(m);
        if (t === undefined) { t = modelTriangles(getModel(m)); tris.set(m, t); }
        sum += t;
      }
      if (sum / segs > 900) bad.push(`${id}: ${(sum / segs).toFixed(0)}`);
    }
    expect(bad).toEqual([]);
  }, 60000);

  it('modelos redondos de sprite e de mata ≤ 700 triângulos (pedra ≤ 360)', () => {
    for (const id of ids) {
      if (id.startsWith('far:')) continue;
      const cap = /^(rock|searock):/.test(id) ? 360 : /^(mesa|stack):/.test(id) ? 600 : 700;
      expect(modelTriangles(getModel(id)), id).toBeLessThanOrEqual(cap);
    }
  });
});
