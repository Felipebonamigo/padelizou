// Marcos turísticos (onda G, docs/PISTAS-TURISMO.md; docs/VISUAL.md, "Marcos turísticos"): cada marco
// registrado aparece nas pistas que o pedem, quantas vezes por volta o contrato diz, perto da largada, longe do
// alcance do carro (≥ 26 m do centro de qualquer trecho à vista), no chão, de frente para a pista e sem esconder
// curva; e cabe no orçamento de triângulos do lugar dele.
import { describe, expect, it } from 'vitest';
import { ALL_PLACES as TRACK_PLACES, EXTRA_BRAZIL_PLACES, TRACK_PLACES as FIRST_PLACES } from '../src/core/data/places';
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
 * Pista do mesmo bioma para um lugar sem pista em TRACKS (hoje todos têm: a linha só vale se uma pista sair).
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

  it('as 54 pistas da segunda leva (EXTRA_BRAZIL_PLACES) também ganham os marcos delas', () => {
    const got = landmarkPlacements(sceneryLayout(getTrack('campos_do_jordao')));
    expect(got.map((g) => g.id)).toContain('araucaria');
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

  const missingIn = (places: typeof TRACK_PLACES) => [...new Set(Object.values(places).flatMap((p) => p.landmarks))].filter((id) => !Object.prototype.hasOwnProperty.call(LANDMARKS, id));
  it('todo id da primeira leva (TRACK_PLACES) tem modelo', () => expect(missingIn(FIRST_PLACES)).toEqual([]));
  it('todo id da segunda leva (EXTRA_BRAZIL_PLACES) tem modelo', () => expect(missingIn(EXTRA_BRAZIL_PLACES)).toEqual([]));
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

  it('modelos do tamanho dos das tarefas irmãs (ponte de 1 km, tepui de 1 × 2 km) também cabem, fora do alcance', async () => {
    const THREE = await import('three');
    const { paint } = await import('../src/render/scenery/geom');
    const reg = LANDMARKS as Record<string, LandmarkDef>;
    reg.__ponte_1km = { build: () => ({ parts: [{ geometry: paint(new THREE.BoxGeometry(30, 60, 1025).translate(0, 30, 0), '#cccccc'), mat: 'flat' }] }), place: 'far', side: 'sea', perLap: 2 };
    reg.__tepui = { build: () => ({ parts: [{ geometry: paint(new THREE.BoxGeometry(1000, 800, 2050).translate(0, 400, 0), '#886644'), mat: 'flat' }] }), place: 'skyline', side: 'any', perLap: 1 };
    try {
      for (const [track, id] of [['copacabana', '__ponte_1km'], ['transpantaneira', '__tepui'], ['rota_66', '__tepui']] as const) {
        const t = getTrack(track);
        const got = landmarkPlacements(sceneryLayout(t, [id]));
        expect(got.length, `${track} ${id}`).toBe(reg[id].perLap);
        for (const { p } of got) {
          const b = modelBounds(LANDMARK_PREFIX + id);
          const side = p.x < 0 ? -1 : 1;
          let edge = Infinity;
          for (const [lx, lz] of [[b.minX, b.minZ], [b.minX, b.maxZ], [b.maxX, b.minZ], [b.maxX, b.maxZ]]) edge = Math.min(edge, Math.abs(p.x) + side * (Math.cos(p.yaw) * lx + Math.sin(p.yaw) * lz));
          expect(edge, `${track} ${id}`).toBeGreaterThanOrEqual(LANDMARK_CLEAR_M);
        }
      }
    } finally {
      delete reg.__ponte_1km; delete reg.__tepui;
    }
  }, 60000);

  it('marco cujo id não tem modelo é ignorado sem erro', () => {
    const layout = sceneryLayout(getTrack('copacabana'), ['nao_existe', 'cristo_redentor']);
    const got = landmarkPlacements(layout);
    expect(got.some((g) => g.id === 'nao_existe')).toBe(false);
    expect(got.filter((g) => g.id === 'cristo_redentor').length).toBe(LANDMARKS.cristo_redentor.perLap);
  });

  it('o primeiro de cada marco fica perto da largada (o mais importante em ≤ 150 segmentos, os outros em ≤ 300; o do horizonte, +150)', () => {
    const bad: string[] = [];
    for (const c of allCases()) {
      const n = c.track.segments.length;
      const got = landmarkPlacements(c.layout);
      c.ids.forEach((id, k) => {
        const ahead = Math.min(...got.filter((g) => g.id === id).map((g) => (g.seg - c.track.startIndex + n) % n));
        // O do horizonte fica mais adiante: só entra na tela (a ~30°) uns 150 segmentos antes do ponto em que está ao lado.
        const cap = (k === 0 ? 150 : 300) + (LANDMARKS[id].place === 'skyline' ? 150 : 0);
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

  it('de frente para a pista (+X do modelo aponta para o centro), girado para quem vem chegando, sem espelhar', () => {
    for (const c of allCases()) {
      for (const { seg, p, id } of landmarkPlacements(c.layout)) {
        const side = p.x < 0 ? -1 : 1;
        // +X do modelo no referencial da pista: (cos yaw) na lateral e (−sen yaw) em Z (+Z = de onde o carro vem).
        expect(Math.cos(p.yaw) * side, `${c.placeId} ${id}#${seg}`).toBeLessThan(0);
        expect(-Math.sin(p.yaw), `${c.placeId} ${id}#${seg}`).toBeGreaterThanOrEqual(-1e-9);
        // Lado oposto da pista = giro, nunca espelho (escala negativa inverteria as letras das placas do mundo).
        expect([p.sx, p.sy, p.sz], `${c.placeId} ${id}#${seg}`).toEqual([1, 1, 1]);
        expect(p.pitch, `${c.placeId} ${id}#${seg}`).toBe(0);
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

  it('borda de dentro conforme o lugar (near 30–80 m, far 120–330 m, skyline 220–300 m; até o dobro sem lugar)', () => {
    const bad: string[] = [];
    for (const c of allCases()) {
      for (const { seg, p, id } of landmarkPlacements(c.layout)) {
        const d = LANDMARKS[id];
        // Borda de dentro da pegada (o ponto do modelo mais perto da pista).
        const b = modelBounds(LANDMARK_PREFIX + id);
        const side = p.x < 0 ? -1 : 1;
        let lat = Infinity;
        for (const [lx, lz] of [[b.minX, b.minZ], [b.minX, b.maxZ], [b.maxX, b.minZ], [b.maxX, b.maxZ]]) lat = Math.min(lat, Math.abs(p.x) + side * (Math.cos(p.yaw) * lx + Math.sin(p.yaw) * lz));
        // A faixa do lugar, ou até o dobro dela quando a pista não deixou lugar na faixa (a última passada da busca).
        lat = Math.round(lat * 100) / 100;
        const ok = d.place === 'near' ? lat >= 30 && lat <= 160 : d.place === 'far' ? lat >= 120 && lat <= 660 : lat >= 220 && lat <= 600;
        if (!ok) bad.push(`${c.placeId} ${id}#${seg}: ${d.place} com a borda a ${lat.toFixed(0)} m`);
      }
    }
    expect(bad).toEqual([]);
  }, 120000);

  // Defeito (onda H, capturas do polimento): dunas, cânions e o Bumbódromo, pegadas de 150–390 m, só achavam chão
  // plano no alto da faixa (borda a 285–330 m, o resto a 450–630 m) e sumiam na névoa; perto, com um pouco de declive,
  // cabiam. O longe fica na metade de dentro da faixa.
  it('marco longe fica com a borda de dentro a ≤ 250 m (não é empurrado para o fim da faixa pelo declive)', () => {
    const bad: string[] = [];
    for (const c of allCases()) {
      for (const { seg, p, id } of landmarkPlacements(c.layout)) {
        if (LANDMARKS[id].place !== 'far') continue;
        const b = modelBounds(LANDMARK_PREFIX + id);
        const side = p.x < 0 ? -1 : 1;
        let lat = Infinity;
        for (const [lx, lz] of [[b.minX, b.minZ], [b.minX, b.maxZ], [b.maxX, b.minZ], [b.maxX, b.maxZ]]) lat = Math.min(lat, Math.abs(p.x) + side * (Math.cos(p.yaw) * lx + Math.sin(p.yaw) * lz));
        if (lat > 250) bad.push(`${c.placeId} ${id}#${seg}: borda a ${lat.toFixed(0)} m`);
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
        // `cone` é o facho de luz da noite fechada (só posição e normal: a cor vem do material).
        expect(['flat', 'glow', 'beacon', 'cone', 'office', 'apartment', 'classic', 'house'], id).toContain(part.mat);
        if (part.mat !== 'cone') expect(part.geometry.getAttribute('color'), `${id} ${part.mat}`).toBeDefined();
        expect(part.geometry.index, `${id} ${part.mat}`).toBeNull();
        if (part.mat !== 'flat' && part.mat !== 'glow' && part.mat !== 'beacon' && part.mat !== 'cone') expect(part.geometry.getAttribute('uv'), `${id} ${part.mat}`).toBeDefined();
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

describe('marcos turísticos: terreno', () => {
  // Defeito visto na captura (onda G): em Sampa as quadras de fundo do terreno (45–285 m) nasciam em cima do MASP e
  // da Ponte Estaiada e na frente deles — os dois sumiam. As quadras respeitam as clareiras que o layout marca.
  it('as quadras de fundo da cidade não nascem na pegada nem na frente de um marco (Sampa: MASP e Ponte Estaiada)', async () => {
    const THREE = await import('three');
    const ctx = new Proxy({}, { get: () => () => ({ addColorStop: () => undefined }), set: () => true });
    const g = globalThis as { document?: unknown };
    const had = g.document;
    g.document = { createElement: () => ({ width: 0, height: 0, getContext: () => ctx }) };
    try {
      const { Terrain } = await import('../src/render/terrain');
      const { palette } = await import('../src/render/palette');
      const { buildRoadFrame } = await import('../src/render/roadframe');
      const track = getTrack('sampa_noite');
      const layout = sceneryLayout(track);
      const marks = landmarkPlacements(layout);
      expect(marks.length).toBeGreaterThan(0);
      const terrain = new Terrain(300);
      terrain.setTrack(track, palette(track.def.scenery, track.def.timeOfDay), 'sampa:test');
      const bad: string[] = [];
      const m = new THREE.Matrix4(); const pos = new THREE.Vector3();
      for (const { seg, p, id } of marks) {
        // O carro 40 segmentos antes do marco: o que está entre ele e o marco, e o próprio lugar do marco.
        const frame = buildRoadFrame(track, ((seg - 40 + track.segments.length) % track.segments.length) * 200, 30, 200);
        terrain.update(frame, track, 0, new THREE.Vector3(0, 1, 0), 0);
        const j = frame.behind + 40;
        const side = p.x < 0 ? -1 : 1;
        const b = modelBounds(LANDMARK_PREFIX + id);
        // Até a borda de fora da pegada (o que fica atrás do marco é fundo, pode ter quadra).
        let far = 0;
        for (const [lx, lz] of [[b.minX, b.minZ], [b.minX, b.maxZ], [b.maxX, b.minZ], [b.maxX, b.maxZ]]) far = Math.max(far, Math.abs(p.x) + side * (Math.cos(p.yaw) * lx + Math.sin(p.yaw) * lz));
        for (const im of terrain.group.children) {
          if (!(im instanceof THREE.InstancedMesh) || !im.visible || im.count === 0) continue;
          if (im.geometry.attributes.position.count !== 24) continue;
          for (let k = 0; k < im.count; k++) {
            im.getMatrixAt(k, m); pos.setFromMatrixPosition(m);
            // Para o referencial do segmento do marco: lateral e ao longo.
            const h = frame.heading[j];
            const dx = pos.x - frame.px[j]; const dz = pos.z - frame.pz[j];
            const lat = dx * Math.cos(h) + dz * Math.sin(h);
            const along = dx * Math.sin(h) - dz * Math.cos(h); // + = à frente
            if (lat * side > 30 && lat * side < far && along > -160 && along < 20) bad.push(`${id}#${seg}: quadra a ${(lat * side).toFixed(0)} m, ${along.toFixed(0)} m`);
          }
        }
      }
      expect(bad.slice(0, 6), `${bad.length} quadras`).toEqual([]);
      terrain.dispose();
    } finally {
      g.document = had;
    }
  }, 60000);
});

