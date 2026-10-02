// Marcos turísticos (onda G, docs/PISTAS-TURISMO.md; docs/VISUAL.md, "Marcos turísticos"): cada marco
// registrado aparece nas pistas que o pedem, quantas vezes por volta o contrato diz, perto da largada, longe do
// alcance do carro (≥ 26 m do centro de qualquer trecho à vista), no chão, de frente para a pista e sem esconder
// curva; e cabe no orçamento de triângulos do lugar dele.
import { describe, expect, it } from 'vitest';
import { TRACK_PLACES } from '../src/core/data/places';
import { getTrack, TRACKS } from '../src/core/track';
import type { Track } from '../src/core/types';
import { getModel, LANDMARK_PREFIX, modelBounds } from '../src/render/scenery/catalog';
import { groundOffset, isSeaSide, seaLevelOffset } from '../src/render/scenery/ground';
import { LANDMARKS } from '../src/render/scenery/landmarks';
import { LANDMARKS_BRASIL_CENTRO_SUL } from '../src/render/scenery/landmarks/brasil-centro-sul';
import type { LandmarkDef } from '../src/render/scenery/landmarks/types';
import { innerCurve, LANDMARK_CLEAR_M, sceneryLayout, type Layout, type Placement } from '../src/render/scenery/layout';
import { modelTriangles } from '../src/render/scenery/vegetation';
import { HEADING_PER_CURVE, SEGMENT_M, Y_SCALE } from '../src/render/units';

/**
 * Pistas da onda G que ainda não existem neste ramo: o marco é posto numa pista do mesmo bioma (o do contrato).
 * Quando a pista nova entrar em TRACKS, a linha dela deixa de ser usada.
 */
const STAND_IN: Record<string, string> = {
  ouro_preto: 'serra_do_mar', convento_penha: 'copacabana', floripa: 'copacabana', cuia_gaucha: 'rochosas',
  brasilia: 'sampa_noite', chapada_veadeiros: 'transpantaneira', bonito: 'serra_do_mar',
  salvador: 'copacabana', aracaju: 'monaco_noite', maragogi: 'copacabana', olinda: 'copacabana', joao_pessoa: 'baia_toquio',
  natal: 'copacabana', jericoacoara: 'great_ocean', serra_capivara: 'canion', lencois: 'rota_66',
  belem: 'baia_toquio', manaus: 'copacabana', macapa: 'serra_do_mar', monte_roraima: 'transpantaneira',
  porto_velho: 'yanbaru', rio_branco: 'serra_do_mar', jalapao: 'canion',
};

/** Os 22 marcos desta tarefa (Sudeste, Sul, Centro-Oeste): todos têm de ter modelo. */
const CENTRO_SUL = [
  'cristo_redentor', 'pao_de_acucar', 'masp', 'ponte_estaiada', 'igreja_barroca', 'casario_colonial', 'convento_penha',
  'terceira_ponte', 'trem_serra_verde', 'estufa_jardim_botanico', 'ponte_hercilio_luz', 'igreja_acoriana',
  'catedral_de_pedra', 'cuia_chimarrao', 'araucaria', 'congresso_nacional', 'catedral_brasilia', 'cachoeira_veadeiros',
  'buriti', 'gruta_lago_azul', 'tuiuiu_ninho', 'portal_transpantaneira',
];

/**
 * Orçamento de triângulos por marco (ver docs/DESEMPENHO.md, "Marcos turísticos"): near é visto de perto e pede
 * detalhe (porta, janela, telhado); far é grande (ponte com cabos, morro com convento) mas visto a 120–400 m;
 * skyline é silhueta na névoa. Uma pista tem 2–3 marcos × 1–4 por volta, e só 1–3 instâncias cabem na janela de
 * 1 km: ≤ ~15 mil triângulos por viewport no pior caso, contra 50–450 mil do resto do cenário.
 */
const BUDGET: Record<LandmarkDef['place'], number> = { near: 3500, far: 5000, skyline: 2500 };

const trackOf = (placeId: string): Track | null => {
  if (TRACKS.some((t) => t.id === placeId)) return getTrack(placeId);
  const stand = STAND_IN[placeId];
  return stand ? getTrack(stand) : null;
};

interface Case { placeId: string; track: Track; ids: readonly string[]; layout: Layout }
let cases: Case[] | null = null;
function allCases(): Case[] {
  if (cases) return cases;
  cases = [];
  for (const [placeId, place] of Object.entries(TRACK_PLACES)) {
    const track = trackOf(placeId);
    if (!track) continue;
    const ids = place.landmarks.filter((id) => Object.prototype.hasOwnProperty.call(LANDMARKS, id));
    if (ids.length === 0) continue;
    cases.push({ placeId, track, ids, layout: sceneryLayout(track, place.landmarks) });
  }
  return cases;
}

function landmarkPlacements(layout: Layout): Array<{ seg: number; p: Placement; id: string }> {
  const out: Array<{ seg: number; p: Placement; id: string }> = [];
  layout.bySeg.forEach((list, seg) => {
    for (const p of list) {
      const id = layout.models[p.model];
      if (id.startsWith(LANDMARK_PREFIX)) out.push({ seg, p, id: id.slice(LANDMARK_PREFIX.length) });
    }
  });
  return out;
}

/** Linha central desenrolada em três voltas (a pista do DSL não fecha: só vale a vizinhança). */
function unrolled(track: Track): { px: Float64Array; pz: Float64Array; hd: Float64Array } {
  const n = track.segments.length;
  const px = new Float64Array(3 * n + 1); const pz = new Float64Array(3 * n + 1); const hd = new Float64Array(3 * n + 1);
  for (let k = 0; k < 3 * n; k++) {
    const d = track.segments[k % n].curve * HEADING_PER_CURVE;
    const a = hd[k] + d / 2;
    px[k + 1] = px[k] + SEGMENT_M * Math.sin(a); pz[k + 1] = pz[k] - SEGMENT_M * Math.cos(a); hd[k + 1] = hd[k] + d;
  }
  return { px, pz, hd };
}

describe('marcos turísticos: registro', () => {
  it('os 22 marcos do Sudeste, Sul e Centro-Oeste têm modelo (brasil-centro-sul.ts)', () => {
    const missing = CENTRO_SUL.filter((id) => !Object.prototype.hasOwnProperty.call(LANDMARKS_BRASIL_CENTRO_SUL, id));
    expect(missing).toEqual([]);
  });

  it('todo id registrado é um marco de places.ts (nada sobra no registro)', () => {
    const wanted = new Set(Object.values(TRACK_PLACES).flatMap((p) => p.landmarks));
    expect(Object.keys(LANDMARKS).filter((id) => !wanted.has(id))).toEqual([]);
  });

  it('toda pista de places.ts existe ou tem uma pista do mesmo bioma para o teste', () => {
    expect(Object.keys(TRACK_PLACES).filter((id) => !trackOf(id))).toEqual([]);
  });

  it('perLap ≥ 1 e o lugar é válido', () => {
    for (const [id, def] of Object.entries(LANDMARKS)) {
      expect(def.perLap, id).toBeGreaterThanOrEqual(1);
      expect(def.perLap, id).toBeLessThanOrEqual(6);
      expect(['near', 'far', 'skyline'], id).toContain(def.place);
    }
  });

  // Pendências até o merge da onda G: os modelos do Norte/Nordeste (brasil-norte-nordeste.ts) e do mundo (mundo.ts)
  // são de outras tarefas. Quando os três arquivos estiverem no mesmo ramo, este vira `it` (e a lista abaixo, vazia).
  const pending = [...new Set(Object.values(TRACK_PLACES).flatMap((p) => p.landmarks))].filter((id) => !Object.prototype.hasOwnProperty.call(LANDMARKS, id));
  it('pendentes: só ids dos outros dois arquivos de modelos (nenhum do Sudeste/Sul/Centro-Oeste)', () => {
    expect(pending.filter((id) => CENTRO_SUL.includes(id))).toEqual([]);
  });
  if (pending.length > 0) it.todo(`todo id de places.ts tem modelo — faltam ${pending.length}: ${pending.join(', ')}`);
  else it('todo id de places.ts tem modelo', () => expect(pending).toEqual([]));
});

describe('marcos turísticos: posição', () => {
  it('cada marco registrado aparece perLap vezes por volta em cada pista que o pede', () => {
    const bad: string[] = [];
    for (const c of allCases()) {
      const got = landmarkPlacements(c.layout);
      for (const id of c.ids) {
        const n = got.filter((g) => g.id === id).length;
        if (n !== LANDMARKS[id].perLap) bad.push(`${c.placeId} (${c.track.def.id}) ${id}: ${n} de ${LANDMARKS[id].perLap}`);
      }
    }
    expect(bad).toEqual([]);
  }, 120000);

  it('marco cujo id não tem modelo é ignorado sem erro', () => {
    const layout = sceneryLayout(getTrack('copacabana'), ['nao_existe', 'cristo_redentor']);
    const got = landmarkPlacements(layout);
    expect(got.some((g) => g.id === 'nao_existe')).toBe(false);
    expect(got.filter((g) => g.id === 'cristo_redentor').length).toBe(LANDMARKS.cristo_redentor.perLap);
  });

  it('o primeiro de cada marco fica perto da largada (o mais importante em ≤ 150 segmentos, os outros em ≤ 300)', () => {
    const bad: string[] = [];
    for (const c of allCases()) {
      const n = c.track.segments.length;
      const got = landmarkPlacements(c.layout);
      c.ids.forEach((id, k) => {
        const ahead = Math.min(...got.filter((g) => g.id === id).map((g) => (g.seg - c.track.startIndex + n) % n));
        const cap = k === 0 ? 150 : 300;
        if (!(ahead <= cap)) bad.push(`${c.placeId} ${id}: primeiro a ${ahead} segmentos da largada (máx. ${cap})`);
      });
    }
    expect(bad).toEqual([]);
  }, 120000);

  it('instâncias do mesmo marco ficam espalhadas pela volta (≥ 1/(2·perLap) da volta entre elas)', () => {
    const bad: string[] = [];
    for (const c of allCases()) {
      const n = c.track.segments.length;
      const got = landmarkPlacements(c.layout);
      for (const id of c.ids) {
        const segs = got.filter((g) => g.id === id).map((g) => g.seg).sort((a, b) => a - b);
        for (let k = 0; k < segs.length; k++) {
          const gap = k + 1 < segs.length ? segs[k + 1] - segs[k] : segs[0] + n - segs[k];
          if (segs.length > 1 && gap < n / (2 * segs.length)) bad.push(`${c.placeId} ${id}: ${gap} segmentos entre duas`);
        }
      }
    }
    expect(bad).toEqual([]);
  }, 120000);

  it(`a pegada inteira fica a ≥ ${LANDMARK_CLEAR_M} m do centro de todo trecho à vista (o próprio e os vizinhos)`, () => {
    expect(LANDMARK_CLEAR_M).toBeGreaterThanOrEqual(26);
    const bad: string[] = [];
    for (const c of allCases()) {
      const n = c.track.segments.length;
      const { px, pz, hd } = unrolled(c.track);
      for (const { seg, p, id } of landmarkPlacements(c.layout)) {
        const b = modelBounds(LANDMARK_PREFIX + id);
        const k = seg + n;
        const t = k + p.f;
        const cx = px[k] + (px[k + 1] - px[k]) * p.f + p.x * Math.cos(hd[k]);
        const cz = pz[k] + (pz[k + 1] - pz[k]) * p.f + p.x * Math.sin(hd[k]);
        const th = p.yaw - hd[k];
        const ct = Math.cos(th); const st = Math.sin(th);
        let worst = Infinity; let at = -1;
        for (let q = Math.floor(t) - 300; q <= Math.floor(t) + 300; q++) {
          const dx = px[q] - cx; const dz = pz[q] - cz;
          // Para o referencial do modelo (giro do runtime: x' = x·cos θ + z·sen θ, z' = −x·sen θ + z·cos θ).
          const lx = dx * ct - dz * st; const lz = dx * st + dz * ct;
          const ex = Math.max(b.minX * p.sx - lx, 0, lx - b.maxX * p.sx);
          const ez = Math.max(b.minZ * p.sz - lz, 0, lz - b.maxZ * p.sz);
          const d = Math.hypot(ex, ez);
          if (d < worst) { worst = d; at = q % n; }
        }
        if (worst < LANDMARK_CLEAR_M) bad.push(`${c.placeId} ${id}#${seg}: ${worst.toFixed(1)} m do centro do trecho ${at}`);
      }
    }
    expect(bad.slice(0, 12), `${bad.length} marcos perto demais`).toEqual([]);
  }, 120000);

  it('no chão: pousa no ponto mais baixo do chão sob a pegada (nada flutua); no mar, no nível da água', () => {
    const bad: string[] = [];
    for (const c of allCases()) {
      const track = c.track; const segs = track.segments; const n = segs.length;
      for (const { seg, p, id } of landmarkPlacements(c.layout)) {
        const side = p.x < 0 ? -1 : 1;
        if (LANDMARKS[id].side === 'sea' && isSeaSide(track.def.scenery, side)) {
          if (Math.abs(p.y - seaLevelOffset(track, segs[seg])) > 0.05) bad.push(`${c.placeId} ${id}#${seg}: ${p.y.toFixed(2)} fora do nível do mar`);
          continue;
        }
        const b = modelBounds(LANDMARK_PREFIX + id);
        const ct = Math.cos(p.yaw); const st = Math.sin(p.yaw);
        let lo = Infinity;
        for (const [lx, lz] of [[0, 0], [b.minX, b.minZ], [b.minX, b.maxZ], [b.maxX, b.minZ], [b.maxX, b.maxZ], [b.minX, 0], [b.maxX, 0]]) {
          // Ponto da pegada em (lateral, ao longo) do segmento do marco (giro relativo ao rumo da pista).
          const lat = p.x + (lx * p.sx) * ct + (lz * p.sz) * st;
          const along = -(-(lx * p.sx) * st + (lz * p.sz) * ct); // −Z é para a frente
          const fs = seg + p.f + along / SEGMENT_M;
          const j = ((Math.floor(fs) % n) + n) % n;
          const rise = (segs[j].y0 - segs[seg].y0) * Y_SCALE;
          const g = groundOffset(track, j, fs - Math.floor(fs), lat < 0 ? -1 : 1, Math.abs(lat)) + rise;
          if (Math.abs(lat) >= 26) lo = Math.min(lo, g);
        }
        if (Math.abs(p.y - lo) > 0.6) bad.push(`${c.placeId} ${id}#${seg}: base em ${p.y.toFixed(2)} m, chão mais baixo em ${lo.toFixed(2)} m`);
        if (b.minY > 0.01) bad.push(`${id}: a base do modelo começa a ${b.minY.toFixed(2)} m (tem de tocar y = 0)`);
      }
    }
    expect(bad.slice(0, 12), `${bad.length} marcos fora do chão`).toEqual([]);
  }, 120000);

  it('de frente para a pista (+X do modelo aponta para o centro) e girado para quem vem chegando', () => {
    for (const c of allCases()) {
      for (const { seg, p, id } of landmarkPlacements(c.layout)) {
        const side = p.x < 0 ? -1 : 1;
        // +X do modelo no referencial da pista: (cos yaw) na lateral e (−sen yaw) em Z (+Z = de onde o carro vem).
        expect(Math.cos(p.yaw) * side, `${c.placeId} ${id}#${seg}`).toBeLessThan(0);
        expect(-Math.sin(p.yaw), `${c.placeId} ${id}#${seg}`).toBeGreaterThanOrEqual(-1e-9);
      }
    }
  }, 120000);

  it('nada alto (> 8 m) perto ou longe do lado de dentro de curva próxima (não esconde a pista)', () => {
    const bad: string[] = [];
    for (const c of allCases()) {
      for (const { seg, p, id } of landmarkPlacements(c.layout)) {
        if (LANDMARKS[id].place === 'skyline') continue;
        const h = modelBounds(LANDMARK_PREFIX + id).maxY * p.sy;
        if (h > 8 && innerCurve(c.track, seg, p.x < 0 ? -1 : 1)) bad.push(`${c.placeId} ${id}#${seg} (${h.toFixed(0)} m)`);
      }
    }
    expect(bad).toEqual([]);
  }, 120000);

  it('distância do centro da pista conforme o lugar (near 30–80 m na borda, far 120–400 m, skyline ≥ 400 m)', () => {
    const bad: string[] = [];
    for (const c of allCases()) {
      for (const { seg, p, id } of landmarkPlacements(c.layout)) {
        const d = LANDMARKS[id];
        const lat = Math.abs(p.x);
        const ok = d.place === 'near' ? lat >= 30 && lat <= 80 + 60 : d.place === 'far' ? lat >= 120 && lat <= 400 : lat >= 400;
        if (!ok) bad.push(`${c.placeId} ${id}#${seg}: ${d.place} a ${lat.toFixed(0)} m`);
      }
    }
    expect(bad).toEqual([]);
  }, 120000);

  it('é determinístico e as pistas existentes mostram os seus marcos (Copacabana: Cristo e Pão de Açúcar)', () => {
    const a = sceneryLayout(getTrack('copacabana'));
    const b = sceneryLayout(getTrack('copacabana'));
    expect(JSON.stringify(a)).toBe(JSON.stringify(b));
    const ids = new Set(landmarkPlacements(a).map((g) => g.id));
    expect(ids.has('cristo_redentor')).toBe(true);
    expect(ids.has('pao_de_acucar')).toBe(true);
  }, 60000);
});

describe('marcos turísticos: modelos', () => {
  it(`orçamento de triângulos: near ≤ ${BUDGET.near}, far ≤ ${BUDGET.far}, skyline ≤ ${BUDGET.skyline}`, () => {
    for (const [id, def] of Object.entries(LANDMARKS)) {
      expect(modelTriangles(getModel(LANDMARK_PREFIX + id)), id).toBeLessThanOrEqual(BUDGET[def.place]);
    }
  });

  it('modelos com as partes num material válido, sem sombra própria e com cor por vértice', () => {
    for (const id of Object.keys(LANDMARKS)) {
      const m = getModel(LANDMARK_PREFIX + id);
      expect(m.parts.length, id).toBeGreaterThan(0);
      expect(m.parts.length, id).toBeLessThanOrEqual(4);
      for (const part of m.parts) {
        expect(['flat', 'glow', 'beacon', 'office', 'apartment', 'classic', 'house'], id).toContain(part.mat);
        expect(part.geometry.getAttribute('color'), `${id} ${part.mat}`).toBeDefined();
        expect(part.geometry.index, `${id} ${part.mat}`).toBeNull();
        if (part.mat !== 'flat' && part.mat !== 'glow' && part.mat !== 'beacon') expect(part.geometry.getAttribute('uv'), `${id} ${part.mat}`).toBeDefined();
      }
    }
  });

  it('tamanho do lugar: near ≥ 6 m de altura, far ≥ 25 m, skyline ≥ 90 m (lido de longe)', () => {
    for (const [id, def] of Object.entries(LANDMARKS)) {
      const h = modelBounds(LANDMARK_PREFIX + id).maxY;
      const min = def.place === 'near' ? 6 : def.place === 'far' ? 25 : 90;
      expect(h, id).toBeGreaterThanOrEqual(min);
    }
  });
});
