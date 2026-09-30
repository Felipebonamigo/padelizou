// Cenário (src/render/scenery): o visual dos sprites sólidos bate com a colisão do núcleo; a decoração só
// visual nunca fica onde o carro chega; o layout é determinístico; o chão do cenário é o mesmo do terreno.
import * as THREE from 'three';
import { describe, expect, it } from 'vitest';
import { SPRITE_HALF_WIDTH } from '../src/core/track/sprites';
import { getTrack, TRACKS } from '../src/core/track';
import type { SpriteKind } from '../src/core/types';
import { palette } from '../src/render/palette';
import { buildRoadFrame } from '../src/render/roadframe';
import { CAR_BAND, getModel, spriteVisual } from '../src/render/scenery/catalog';
import { bandPoints } from '../src/render/scenery/geom';
import { columnDistance, columnHeight, groundAt, TERRAIN_COLS } from '../src/render/scenery/ground';
import { CAR_REACH_M, sceneryLayout, type Placement } from '../src/render/scenery/layout';
import { modelTriangles } from '../src/render/scenery/vegetation';
import { HEADING_PER_CURVE, ROAD_HALF_WIDTH_M, SEGMENT_M } from '../src/render/units';

const SOLID_PART = new Set(['flat', 'office', 'apartment', 'classic', 'house', 'panel']);

/**
 * Distância lateral (m, do centro da pista) do ponto do objeto mais perto da pista, na altura do carro,
 * com o objeto posto como o renderizador põe (lateral, giro, escala). Infinity se nada toca a faixa.
 */
function innerEdgeOf(geoms: THREE.BufferGeometry[], p: Pick<Placement, 'x' | 'yaw' | 'sx' | 'sy' | 'sz'>): number {
  const side = p.x < 0 ? -1 : 1;
  const c = Math.cos(p.yaw); const s = Math.sin(p.yaw);
  let best = Infinity;
  for (const g of geoms) {
    // Giro em Y (three: x' = x·cos + z·sin); o eixo lateral do quadro é o X depois do giro de rumo.
    bandPoints(g, CAR_BAND[0], CAR_BAND[1], (lx, _y, lz) => {
      const wx = p.x + c * lx + s * lz;
      if (wx * side < best) best = wx * side;
    }, p.sx, p.sy, p.sz);
  }
  return best;
}

const lowCache = new Map<string, THREE.BufferGeometry[]>();

/**
 * Partes sólidas do modelo, só com os triângulos perto do chão (y < 2,6 m na escala 1: a faixa do carro
 * com a menor escala vertical usada). Cacheado: os testes medem milhares de objetos.
 */
function solidGeoms(id: string): THREE.BufferGeometry[] {
  const hit = lowCache.get(id);
  if (hit) return hit;
  const out: THREE.BufferGeometry[] = [];
  for (const pt of getModel(id).parts) {
    if (!SOLID_PART.has(pt.mat)) continue;
    const pos = pt.geometry.attributes.position;
    const keep: number[] = [];
    for (let i = 0; i + 2 < pos.count; i += 3) {
      if (Math.min(pos.getY(i), pos.getY(i + 1), pos.getY(i + 2)) > 2.6) continue;
      for (let k = 0; k < 3; k++) keep.push(pos.getX(i + k), pos.getY(i + k), pos.getZ(i + k));
    }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(keep, 3));
    out.push(g);
  }
  lowCache.set(id, out);
  return out;
}

const layoutCache = new Map<string, ReturnType<typeof sceneryLayout>>();
function layoutOf(track: ReturnType<typeof getTrack>): ReturnType<typeof sceneryLayout> {
  let l = layoutCache.get(track.def.id);
  if (!l) { l = sceneryLayout(track); layoutCache.set(track.def.id, l); }
  return l;
}

/** Borda visual (m) contra a borda de colisão (m): passa se o visual encosta nela (±8% da meia largura + 15 cm). */
function matches(visual: number, collision: number, halfM: number): boolean {
  const tol = 0.15 + halfM * 0.08;
  return Math.abs(visual - collision) <= tol;
}

const tracks = TRACKS.map((d) => getTrack(d.id));

describe('cenário: visual dos sprites sólidos × colisão do núcleo', () => {
  it('o detector reprova o defeito antigo (copa no alto, tronco fino; prédio empurrado para 11 m)', () => {
    // Árvore antiga: tronco de raio 0,26 m até 2,6 m e a copa começando a ~1,9 m — na altura do carro, só o tronco.
    const oldTree = new THREE.CylinderGeometry(0.16, 0.26, 2.6, 7).translate(0, 1.3, 0);
    const half = SPRITE_HALF_WIDTH.tree * ROAD_HALF_WIDTH_M;
    const x = 12;
    const visual = innerEdgeOf([oldTree], { x, yaw: 0, sx: 1, sy: 1, sz: 1 });
    expect(matches(visual, x - half, half), `borda visual ${visual.toFixed(2)} m, colisão ${(x - half).toFixed(2)} m`).toBe(false);
    // Prédio antigo: se a borda de colisão ficava a menos de 11 m, o renderizador empurrava o modelo para 11 m.
    const bHalf = SPRITE_HALF_WIDTH.building * 1.3 * ROAD_HALF_WIDTH_M;
    const colEdge = 1.3 * ROAD_HALF_WIDTH_M;
    const pushed = Math.max(colEdge, 11);
    expect(matches(pushed, colEdge, bHalf)).toBe(false);
  });

  it('em todas as pistas, a borda de cada sprite sólido (na altura do carro) fica na borda de colisão', () => {
    const bad: string[] = [];
    for (const track of tracks) {
      const layout = layoutOf(track);
      track.segments.forEach((seg, i) => {
        const placed = layout.bySeg[i].filter((p) => p.sprite);
        let cursor = 0;
        for (const sp of seg.sprites) {
          const vis = spriteVisual(sp.kind, sp.variant, sp.scale, track.def);
          const mine = placed.slice(cursor, cursor + vis.models.length);
          cursor += vis.models.length;
          if (!sp.solid) continue;
          const p = mine[0];
          const half = SPRITE_HALF_WIDTH[sp.kind] * sp.scale * ROAD_HALF_WIDTH_M;
          const collision = Math.abs(sp.x) * ROAD_HALF_WIDTH_M - half;
          const visual = innerEdgeOf(solidGeoms(layout.models[p.model]), p);
          // Giro livre (árvore, pedra): a borda muda com o giro; o pior caso tem de encostar também.
          if (!matches(visual, collision, half)) bad.push(`${track.def.id}#${i} ${sp.kind}/${layout.models[p.model]}: visual ${visual.toFixed(2)} m × colisão ${collision.toFixed(2)} m`);
        }
      });
    }
    expect(bad.slice(0, 12), `${bad.length} sprites fora`).toEqual([]);
  }, 120000);

  it('modelos redondos alcançam a pegada em qualquer giro (a borda não depende do sorteio do giro)', () => {
    const kinds: SpriteKind[] = ['tree', 'pine', 'palm', 'cactus', 'boulder', 'tower'];
    const bad: string[] = [];
    const seen = new Set<string>();
    for (const track of tracks) for (const kind of kinds) for (let v = 0; v < 4; v++) {
      const vis = spriteVisual(kind, v, 1, track.def);
      const id = vis.models[0].id;
      if (seen.has(id)) continue;
      seen.add(id);
      const half = SPRITE_HALF_WIDTH[kind] * ROAD_HALF_WIDTH_M;
      const x = 20;
      for (let k = 0; k < 16; k++) {
        const yaw = (k / 16 - 0.5) * 2 * vis.yawJitter;
        const visual = innerEdgeOf(solidGeoms(id), { x, yaw, sx: 1, sy: 1, sz: 1 });
        if (!matches(visual, x - half, half)) { bad.push(`${id} giro ${yaw.toFixed(2)}: ${(x - visual).toFixed(2)} m de ${half.toFixed(2)}`); break; }
      }
    }
    expect(bad).toEqual([]);
  }, 60000);
});

describe('cenário: decoração só visual', () => {
  it('nada alto nasce onde o carro chega (|x| ≤ 3,2 + meia largura do carro), em nenhuma pista', () => {
    const bad: string[] = [];
    for (const track of tracks) {
      const layout = layoutOf(track);
      layout.bySeg.forEach((list, i) => {
        for (const p of list) {
          if (p.sprite || p.linkSeg > 0) continue;
          const id = layout.models[p.model];
          // Forração é baixa (atravessável): fica fora da regra.
          if (id.startsWith('tuft:') || id.startsWith('flowers:') || id.startsWith('pebbles:') || id.startsWith('bush:')) continue;
          const edge = innerEdgeOf(solidGeoms(id), p);
          if (edge < CAR_REACH_M - 0.05) bad.push(`${track.def.id}#${i} ${id} a ${edge.toFixed(2)} m`);
        }
      });
    }
    expect(bad.slice(0, 10), `${bad.length} itens`).toEqual([]);
  }, 120000);

  it('a forração na faixa do carro e as moitas (sprite não sólido) são baixas (≤ 1,1 m)', () => {
    let checked = 0;
    for (const track of tracks) {
      const layout = layoutOf(track);
      for (const list of layout.bySeg) for (const p of list) {
        const bushSprite = p.sprite && layout.models[p.model].startsWith('bush:');
        if (!bushSprite && (p.sprite || Math.abs(p.x) >= CAR_REACH_M)) continue;
        const g = getModel(layout.models[p.model]).parts[0].geometry;
        g.computeBoundingBox();
        const top = (g.boundingBox?.max.y ?? 0) * p.sy;
        expect(top, `${track.def.id} ${layout.models[p.model]}`).toBeLessThanOrEqual(1.1);
        checked++;
      }
    }
    expect(checked).toBeGreaterThan(1000);
  }, 120000);

  it('é determinístico: a mesma pista monta o mesmo cenário', () => {
    for (const id of ['copacabana', 'sampa_noite', 'monte_fuji', 'kruger', 'outback', 'santorini']) {
      const a = sceneryLayout(getTrack(id));
      const b = sceneryLayout(getTrack(id));
      expect(JSON.stringify(a)).toBe(JSON.stringify(b));
    }
  }, 60000);

  it('nenhum objeto alto cai na pista de outro trecho (grampos e curvas fechadas)', () => {
    const bad: string[] = [];
    for (const track of tracks) {
      const n = track.segments.length;
      // Linha central desenrolada em duas voltas (a pista do DSL não fecha: só vale a vizinhança).
      const px = new Float64Array(2 * n + 1); const pz = new Float64Array(2 * n + 1); const hd = new Float64Array(2 * n + 1);
      for (let k = 0; k < 2 * n; k++) {
        const d = track.segments[k % n].curve * HEADING_PER_CURVE;
        const a = hd[k] + d / 2;
        px[k + 1] = px[k] + SEGMENT_M * Math.sin(a); pz[k + 1] = pz[k] - SEGMENT_M * Math.cos(a); hd[k + 1] = hd[k] + d;
      }
      const layout = layoutOf(track);
      layout.bySeg.forEach((list, i) => {
        for (const p of list) {
          if (p.sprite || Math.abs(p.x) < CAR_REACH_M) continue;
          const k0 = i + n;
          const k = k0 < 2 * n ? k0 : i;
          const x = px[k] + p.x * Math.cos(hd[k]); const z = pz[k] + p.x * Math.sin(hd[k]);
          for (let q = Math.max(0, k - 180); q <= Math.min(2 * n, k + 180); q += 3) {
            const d = Math.hypot(px[q] - x, pz[q] - z);
            if (d < ROAD_HALF_WIDTH_M + 3) { bad.push(`${track.def.id}#${i} ${layout.models[p.model]} a ${d.toFixed(1)} m do trecho ${q % n}`); break; }
          }
        }
      });
    }
    expect(bad.slice(0, 10), `${bad.length} itens`).toEqual([]);
  }, 60000);
});

describe('cenário: orçamento', () => {
  it('modelos de sprite ≤ 3.500 triângulos; silhuetas de longe ≤ 60; forração ≤ 40', () => {
    const ids = new Set<string>();
    for (const track of tracks) for (const id of layoutOf(track).models) ids.add(id);
    for (const id of ids) {
      const tris = modelTriangles(getModel(id));
      const cap = id.startsWith('far:') ? 60 : id.startsWith('tuft:') || id.startsWith('flowers:') || id.startsWith('pebbles:') ? 40 : id.startsWith('mesa:') || id.startsWith('bld:') || id.startsWith('stand:') || id.startsWith('house:') || id === 'pagoda' ? 3500 : 1600;
      expect(tris, id).toBeLessThanOrEqual(cap);
    }
  }, 60000);

  it('densidade média ≤ 16 objetos por segmento em toda pista', () => {
    for (const track of tracks) {
      const layout = layoutOf(track);
      let total = 0;
      for (const list of layout.bySeg) total += list.length;
      expect(total / track.segments.length, track.def.id).toBeLessThanOrEqual(16);
    }
  }, 60000);
});

describe('cenário: chão igual ao do terreno', () => {
  it('groundAt reproduz as alturas das colunas do Terrain (todas as pistas, os dois lados)', async () => {
    // O Terrain cria texturas por canvas: um documento mínimo basta (nada é desenhado).
    const ctx = new Proxy({}, { get: () => () => ({ addColorStop: () => undefined }), set: () => true });
    const g = globalThis as { document?: unknown };
    const had = g.document;
    g.document = { createElement: () => ({ width: 0, height: 0, getContext: () => ctx }) };
    try {
      const { Terrain } = await import('../src/render/terrain');
      const terrain = new Terrain(300);
      for (const track of tracks.filter((_, k) => k % 3 === 0)) {
        const p = palette(track.def.scenery, track.def.timeOfDay);
        terrain.setTrack(track, p, `${track.def.id}:test`);
        const frame = buildRoadFrame(track, track.length * 0.37, 30, 200);
        terrain.update(frame, track, 0, new THREE.Vector3(0, 1, 0), 0);
        const strips = terrain.group.children.slice(0, 2) as THREE.Mesh[];
        const lanes = TERRAIN_COLS.length;
        for (const j of [0, 31, 77, 150, 229]) {
          const seg = track.segments[frame.segIndex[j]];
          for (const side of [-1, 1]) {
            const pos = strips[side < 0 ? 0 : 1].geometry.attributes.position;
            for (let c = 0; c < lanes; c++) {
              const lane = side < 0 ? lanes - 1 - c : c;
              const y = pos.getY(j * lanes + lane);
              const mine = frame.py[j] + columnHeight(track, seg, side, c);
              expect(Math.abs(y - mine), `${track.def.id} ponto ${j} lado ${side} coluna ${c}`).toBeLessThan(1e-3);
              // Entre colunas, a interpolação (só onde a curva não empilhou colunas na mesma distância).
              const d = columnDistance(seg, side, c);
              if (c === 0 || d > columnDistance(seg, side, c - 1) + 1e-3) {
                expect(Math.abs(frame.py[j] + groundAt(track, seg, side, d) - y), `interp ${track.def.id} ${j} ${side} ${c}`).toBeLessThan(1e-3);
              }
            }
          }
        }
      }
      terrain.dispose();
    } finally {
      g.document = had;
    }
  }, 60000);
});


describe('cenário: luz do poste', () => {
  // A poça de luz no asfalto (road.ts, onda F pista) e o poste (scenery, onda F cenário) nasceram em tarefas
  // separadas: a poça supunha o braço antigo, de 4,4 m, e o poste novo tem a luminária mais perto do pé.
  it('a poça de luz no asfalto fica sob a luminária do poste (Sampa à noite, todos os postes)', async () => {
    const { POOL_OFFSET_M } = await import('../src/render/road');
    const track = getTrack('sampa_noite');
    const layout = layoutOf(track);
    const lampModel = layout.models.indexOf('lamp');
    expect(lampModel).toBeGreaterThanOrEqual(0);
    const glow = getModel('lamp').parts.find((p) => p.mat === 'glow');
    expect(glow).toBeDefined();
    const box = new THREE.Box3().setFromBufferAttribute(glow!.geometry.attributes.position as THREE.BufferAttribute);
    const headX = (box.min.x + box.max.x) / 2;
    let checked = 0;
    track.segments.forEach((s, i) => {
      const sprite = s.sprites.find((sp) => sp.kind === 'lamp');
      if (!sprite) return;
      const p = layout.bySeg[i].find((q) => q.model === lampModel);
      expect(p, `poste do segmento ${i}`).toBeDefined();
      // Luminária no mundo (o giro é 0 ou π: o +X do modelo aponta para a pista) contra a poça.
      const lampHead = p!.x + headX * p!.yawC * p!.sx;
      const pool = sprite.x * ROAD_HALF_WIDTH_M + POOL_OFFSET_M;
      expect(Math.abs(pool - lampHead), `segmento ${i}: poça em ${pool.toFixed(2)} m, luminária em ${lampHead.toFixed(2)} m`).toBeLessThan(0.3);
      checked++;
    });
    expect(checked).toBeGreaterThan(10);
  });
});
