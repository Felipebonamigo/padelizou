import { describe, it, expect } from 'vitest';
import { SEGMENT_LENGTH } from '../src/core/constants';
import { carDef } from '../src/core/data/cars';
import { holdableSpeedFraction } from '../src/core/sim/physics';
import { SPRITE_HALF_WIDTH } from '../src/core/sim/collisions';
import { BRAZIL_REGIONS, CUPS, stageCups, stateCup } from '../src/core/data/cups';
import { EXTRA_BRAZIL_PLACES, TRACK_PLACES } from '../src/core/data/places';
import { trackOutline } from '../src/render/minimap';
import { buildTrack, maxCurveAhead, segmentAt } from '../src/core/track/builder';
import { TRACKS, trackDef } from '../src/core/track/tracks';
import { getTrack } from '../src/core/track';
import type { TrackDef } from '../src/core/types';
import { setLanguage, t } from '../src/i18n';
import '../src/i18n/core';
import { fitArt, report, resampleLoop } from '../scripts/shape-to-track';
import { ART } from '../scripts/track-art';

describe('pistas', () => {
  it('todas montam, com tamanho entre 1.500 e 3.000 segmentos e ids únicos', () => {
    const ids = new Set<string>();
    for (const def of TRACKS) {
      const t = buildTrack(def);
      expect(t.segments.length, def.id).toBeGreaterThanOrEqual(1500);
      expect(t.segments.length, def.id).toBeLessThanOrEqual(3000);
      expect(t.length).toBe(t.segments.length * SEGMENT_LENGTH);
      expect(ids.has(def.id), `id repetido ${def.id}`).toBe(false);
      ids.add(def.id);
    }
  });

  // ~200 mil conferências nas 109 pistas: 2,2 s com a máquina carregada, perto demais dos 5 s padrão.
  it('a elevação fecha: o fim da pista volta à altura do começo, e os segmentos são contíguos', () => {
    for (const def of TRACKS) {
      const t = buildTrack(def);
      const first = t.segments[0]; const last = t.segments[t.segments.length - 1];
      expect(Math.abs(last.y1 - first.y0), def.id).toBeLessThan(1e-6);
      for (let i = 1; i < t.segments.length; i++) expect(t.segments[i].y0).toBeCloseTo(t.segments[i - 1].y1, 6);
    }
  }, 20_000);

  it('toda pista tem um trecho de box de pelo menos 20 segmentos na reta de largada', () => {
    for (const def of TRACKS) {
      const t = buildTrack(def);
      const pit = t.segments.filter((s) => s.pit);
      expect(pit.length, def.id).toBeGreaterThanOrEqual(20);
      expect(Math.abs(pit[0].curve), def.id).toBeLessThan(1e-6);
      // Logo depois da linha: o aviso de combustível e a IA contam com isso (src/core/sim/fuel.ts).
      expect(pit[0].index, def.id).toBeLessThanOrEqual(4);
    }
  });

  it('nenhum sprite sólido fica em cima do asfalto — nem a BORDA dele (largura × escala)', () => {
    // 25/09: prédios de escala 2,6 com centro em x 2,2 tinham a borda interna em -0,14, dentro da pista.
    for (const def of TRACKS) {
      const t = buildTrack(def);
      for (const s of t.segments) for (const sp of s.sprites) {
        if (!sp.solid) continue;
        const innerEdge = Math.abs(sp.x) - SPRITE_HALF_WIDTH[sp.kind] * sp.scale;
        expect(innerEdge, `${def.id} seg ${s.index} ${sp.kind} x=${sp.x.toFixed(2)} escala=${sp.scale.toFixed(2)}`).toBeGreaterThanOrEqual(1.2);
      }
    }
  });

  it('curvas fortes ganham placas do lado de fora', () => {
    const t = buildTrack(trackDef('roma'));
    const signs = t.segments.flatMap((s) => s.sprites.filter((sp) => sp.kind === 'sign_left' || sp.kind === 'sign_right'));
    expect(signs.length).toBeGreaterThan(10);
    for (const s of t.segments) for (const sp of s.sprites) {
      if (sp.kind === 'sign_right') expect(sp.x).toBeLessThan(0);
      if (sp.kind === 'sign_left') expect(sp.x).toBeGreaterThan(0);
    }
  });

  it('a mesma definição monta a mesma pista (cenário determinístico)', () => {
    const a = buildTrack(trackDef('serra_do_mar')); const b = buildTrack(trackDef('serra_do_mar'));
    expect(JSON.stringify(a.segments)).toBe(JSON.stringify(b.segments));
  });

  it('segmentAt e maxCurveAhead dão a volta na pista', () => {
    const t = getTrack('copacabana');
    expect(segmentAt(t, t.length + 10).index).toBe(0);
    expect(segmentAt(t, -10).index).toBe(t.segments.length - 1);
    // a partir do último segmento, os 50 à frente são ele e os de 0 a 48
    expect(maxCurveAhead(t, t.length - 100, 50)).toBe(Math.max(...t.segments.slice(0, 49).map((s) => Math.abs(s.curve)), Math.abs(t.segments[t.segments.length - 1].curve)));
  });

  it('as copas apontam para pistas existentes e a cadeia de destravamento fecha', () => {
    const ids = new Set(TRACKS.map((t) => t.id));
    for (const c of CUPS) {
      for (const id of c.trackIds) expect(ids.has(id), `${c.id} → ${id}`).toBe(true);
      if (c.requires) expect(CUPS.some((x) => x.id === c.requires), c.id).toBe(true);
    }
    expect(CUPS.filter((c) => c.requires === null).length).toBe(1);
  });
});

/**
 * Quanto a pista exige, medido no traçado: perda média de velocidade nas curvas (o quanto o
 * carro de referência precisa aliviar, em %) mais a inclinação média × 20 (morros escondem a
 * curva seguinte). Não entra no jogo — serve para o rótulo de dificuldade não mentir.
 */
function technicalIndex(def: TrackDef): number {
  const t = buildTrack(def);
  const car = carDef('falcao');
  let loss = 0; let slope = 0;
  for (const s of t.segments) {
    loss += 1 - holdableSpeedFraction(car, s.curve);
    slope += Math.abs(s.y1 - s.y0) / SEGMENT_LENGTH;
  }
  const n = t.segments.length;
  return (100 * loss) / n + (20 * slope) / n;
}

describe('catálogo: Expedição Brasil (27 estados × 3) e Mundial (7 países × 4)', () => {
  const cupTracks = (cupId: string) => (CUPS.find((c) => c.id === cupId)?.trackIds ?? []).map((id) => trackDef(id));
  const mean = (xs: number[]) => xs.reduce((a, b) => a + b, 0) / xs.length;
  const place = (id: string) => TRACK_PLACES[id] ?? EXTRA_BRAZIL_PLACES[id];
  const STATES = ['RJ', 'SP', 'MG', 'ES', 'PR', 'SC', 'RS', 'DF', 'GO', 'MS', 'MT', 'BA', 'SE', 'AL', 'PE', 'PB', 'RN', 'CE', 'PI', 'MA', 'PA', 'AM', 'AP', 'RR', 'RO', 'AC', 'TO'];

  it('109 pistas: 27 copas de estado de 3 pistas, depois 7 copas de país de 4; toda pista em exatamente uma copa', () => {
    expect(TRACKS).toHaveLength(109);
    expect(stageCups('brasil')).toHaveLength(27);
    expect(stageCups('mundial')).toHaveLength(7);
    // A Expedição vem inteira antes do Mundial.
    expect(CUPS.map((c) => c.stage)).toEqual([...new Array(27).fill('brasil'), ...new Array(7).fill('mundial')]);
    const seen = new Map<string, string>();
    for (const c of CUPS) {
      expect(c.trackIds, c.id).toHaveLength(c.stage === 'brasil' ? 3 : 4);
      for (const id of c.trackIds) {
        expect(seen.get(id), `${id} está em ${seen.get(id)} e em ${c.id}`).toBeUndefined();
        seen.set(id, c.id);
      }
    }
    for (const t of TRACKS) expect(seen.has(t.id), `${t.id} não está em copa nenhuma`).toBe(true);
  });

  it('Expedição: uma copa br_<uf> por estado, região por região (Sudeste → Sul → Centro-Oeste → Nordeste → Norte), pistas do próprio estado', () => {
    const br = stageCups('brasil');
    expect(br.map((c) => c.state)).toEqual(STATES);
    expect(BRAZIL_REGIONS.map((r) => r.id)).toEqual(['sudeste', 'sul', 'centro_oeste', 'nordeste', 'norte']);
    expect(BRAZIL_REGIONS.flatMap((r) => r.states)).toEqual(STATES);
    for (const c of br) {
      expect(c.id).toBe(`br_${(c.state ?? '').toLowerCase()}`);
      expect(BRAZIL_REGIONS.find((r) => r.id === c.region)?.states, c.id).toContain(c.state);
      expect(stateCup(c.state ?? '')).toBe(c);
      for (const id of c.trackIds) expect(place(id)?.state, `${id} em ${c.id}`).toBe(c.state);
    }
    for (const c of stageCups('mundial')) { expect(c.region, c.id).toBeUndefined(); expect(c.state, c.id).toBeUndefined(); }
    // Todo lugar do contrato (places.ts) virou pista: 27 da primeira leva + 54 de EXTRA_BRAZIL_PLACES.
    for (const id of [...Object.keys(TRACK_PLACES), ...Object.keys(EXTRA_BRAZIL_PLACES)]) expect(TRACKS.some((t) => t.id === id), id).toBe(true);
  });

  it('ids em ASCII minúsculo (a copa vira a conquista COPA_<ID>), nomes únicos, país da pista = país da copa', () => {
    for (const c of CUPS) expect(c.id, c.id).toMatch(/^[a-z][a-z0-9_]*$/);
    for (const t of TRACKS) expect(t.id, t.id).toMatch(/^[a-z][a-z0-9_]*$/);
    expect(new Set(TRACKS.map((t) => t.name)).size).toBe(TRACKS.length);
    expect(new Set(CUPS.map((c) => c.name)).size).toBe(CUPS.length);
    expect(new Set(stageCups('mundial').map((c) => c.country)).size).toBe(7);
    for (const c of CUPS) for (const t of cupTracks(c.id)) expect(t.country, `${t.id} em ${c.id}`).toBe(c.country);
  });

  it('destravamento linear na ordem da lista: a Expedição do RJ ao TO, e o Mundial ao terminar a Expedição', () => {
    expect(CUPS[0].id).toBe('br_rj');
    CUPS.forEach((c, i) => expect(c.requires, c.id).toBe(i === 0 ? null : CUPS[i - 1].id));
    expect(stageCups('mundial')[0].requires).toBe('br_to');
  });

  it('voltas de 3 a 5, dificuldade inteira de 1 a 5', () => {
    for (const t of TRACKS) {
      expect(t.laps, t.id).toBeGreaterThanOrEqual(3); expect(t.laps, t.id).toBeLessThanOrEqual(5);
      expect(Number.isInteger(t.difficulty) && t.difficulty >= 1 && t.difficulty <= 5, `${t.id}: ${t.difficulty}`).toBe(true);
    }
  });

  // Rampa por etapa: 27 copas de 3 pistas não cabem numa média que sobe sempre (a média só anda de 1/3 em 1/3),
  // então na Expedição a média não cai e vai de 1 a 5; quem sobe a cada copa é o índice técnico (contínuo).
  it('rampa por etapa: a dificuldade não cai dentro da copa nem de uma copa para a seguinte, e vai do fácil ao difícil', () => {
    for (const stage of ['brasil', 'mundial'] as const) {
      let prev = 0;
      const cups = stageCups(stage);
      for (const c of cups) {
        const d = cupTracks(c.id).map((t) => t.difficulty);
        for (let i = 1; i < d.length; i++) expect(d[i], `${c.id}: ${d.join(',')}`).toBeGreaterThanOrEqual(d[i - 1]);
        if (stage === 'mundial') expect(mean(d), `${c.id}: média ${mean(d)} depois de ${prev}`).toBeGreaterThan(prev);
        else expect(mean(d), `${c.id}: média ${mean(d)} depois de ${prev}`).toBeGreaterThanOrEqual(prev);
        prev = mean(d);
      }
      expect(mean(cupTracks(cups[0].id).map((t) => t.difficulty)), stage).toBeLessThanOrEqual(2.25);
      expect(mean(cupTracks(cups[cups.length - 1].id).map((t) => t.difficulty)), stage).toBeGreaterThanOrEqual(4.75);
    }
  });

  it('rampa por etapa, medida no traçado: o índice técnico médio sobe de uma copa para a seguinte', () => {
    for (const stage of ['brasil', 'mundial'] as const) {
      let prev = -1;
      for (const c of stageCups(stage)) {
        const m = mean(cupTracks(c.id).map(technicalIndex));
        expect(m, `${c.id}: índice médio ${m.toFixed(1)} depois de ${prev.toFixed(1)}`).toBeGreaterThan(prev);
        prev = m;
      }
    }
  }, 30_000);

  it('copa, país, região, estado e pista têm nome em PT e em EN', () => {
    const keys = [
      ...CUPS.map((c) => `core.cup.${c.id}`), ...CUPS.map((c) => `core.country.${c.country}`),
      ...BRAZIL_REGIONS.map((r) => `core.region.${r.id}`), ...STATES.map((s) => `core.state.${s}`),
      ...TRACKS.map((tr) => `core.track.${tr.id}`), 'core.stage.brasil', 'core.stage.mundial',
    ];
    for (const lang of ['pt', 'en'] as const) {
      setLanguage(lang);
      for (const k of keys) expect(t(k), `${lang} ${k}`).not.toBe(k);
    }
    setLanguage('pt');
    // O nome em PT é o da definição (o nome curto que o resto do jogo já mostra).
    for (const tr of TRACKS) expect(t(`core.track.${tr.id}`), tr.id).toBe(tr.name);
    expect(t('core.state.RS')).toBe('Rio Grande do Sul');
  });

  it('toda copa tem ao menos uma pista de entardecer ou de noite', () => {
    for (const c of CUPS) expect(cupTracks(c.id).some((t) => t.timeOfDay !== 'day'), c.id).toBe(true);
  });

  it('o rótulo de dificuldade não mente: dois níveis acima exige traçado mais técnico', () => {
    const index = new Map(TRACKS.map((t) => [t.id, technicalIndex(t)]));
    for (const a of TRACKS) for (const b of TRACKS) {
      if (b.difficulty - a.difficulty < 2) continue;
      const ia = index.get(a.id) ?? 0; const ib = index.get(b.id) ?? 0;
      expect(ib, `${b.id} (${b.difficulty}, índice ${ib.toFixed(1)}) × ${a.id} (${a.difficulty}, índice ${ia.toFixed(1)})`).toBeGreaterThan(ia);
    }
  }, 30_000);
});

describe('Cuia da Serra Gaúcha: o minimapa desenha uma cuia de chimarrão', () => {
  // Pedido do dono (docs/PISTAS-TURISMO.md): fundo redondo, cintura estreita, boca larga em cima. O contorno é o do
  // minimapa (trackOutline). Largura = extensão em x dos pontos do contorno numa faixa de altura (y cresce para baixo).
  const outline = trackOutline(getTrack('cuia_gaucha'), 100);
  const ys = outline.map((p) => p[1]);
  const top = Math.min(...ys); const height = Math.max(...ys) - top;
  const widthAt = (from: number, to: number) => {
    const xs = outline.filter((p) => p[1] >= top + from * height && p[1] <= top + to * height).map((p) => p[0]);
    return Math.max(...xs) - Math.min(...xs);
  };
  const bands = (from: number, to: number) => Array.from({ length: Math.round((to - from) / 0.05) }, (_, i) => widthAt(from + i * 0.05, from + (i + 1) * 0.05));
  const mouth = widthAt(0, 0.08);
  const waist = Math.min(...bands(0.1, 0.4)); // a cintura: a faixa mais estreita entre a boca e o bojo
  const belly = Math.max(...bands(0.45, 0.85));

  it('bojo embaixo mais largo que a boca, boca bem mais larga que a cintura', () => {
    expect(belly, `bojo ${belly.toFixed(1)} × boca ${mouth.toFixed(1)}`).toBeGreaterThan(mouth * 1.2);
    expect(mouth, `boca ${mouth.toFixed(1)} × cintura ${waist.toFixed(1)}`).toBeGreaterThan(waist * 2.5);
    expect(belly, `bojo ${belly.toFixed(1)} × cintura ${waist.toFixed(1)}`).toBeGreaterThan(waist * 3);
  });

  it('em pé (boca em cima), mais alta que larga sem virar um tubo, e simétrica', () => {
    const xs = outline.map((p) => p[0]);
    const width = Math.max(...xs) - Math.min(...xs);
    expect(height / width).toBeGreaterThan(1.1); expect(height / width).toBeLessThan(1.8);
    // Simetria: o centro da boca e o do bojo quase na mesma vertical.
    const center = (from: number, to: number) => {
      const s = outline.filter((p) => p[1] >= top + from * height && p[1] <= top + to * height).map((p) => p[0]);
      return (Math.max(...s) + Math.min(...s)) / 2;
    };
    expect(Math.abs(center(0, 0.08) - center(0.55, 0.8)), 'eixo torto').toBeLessThan(width * 0.1);
  });

  it('o fundo é redondo: a largura cai aos poucos do meio do bojo até embaixo, sem quina', () => {
    const rows = [0.6, 0.7, 0.8, 0.9, 0.97].map((f) => widthAt(f - 0.03, f + 0.03));
    for (let i = 1; i < rows.length; i++) expect(rows[i], rows.map((w) => w.toFixed(1)).join(' > ')).toBeLessThanOrEqual(rows[i - 1] + 0.5);
    expect(rows[rows.length - 1]).toBeLessThan(rows[0] * 0.7);
  });
});

// ───────────────────────── Pistas com desenho (docs/PISTAS.md, "Pistas com desenho") ─────────────────────────
// Pedido do dono: uma pista por estado desenha no minimapa o símbolo do lugar (a cuia do RS foi a primeira). Cada
// desenho é travado por propriedades do contorno que o tornam reconhecível; os traçados saem de um polígono-alvo
// (scripts/track-shapes.ts) pela ferramenta scripts/shape-to-track.ts.

/** Medidas do contorno do minimapa de uma pista (o mesmo `trackOutline`; y cresce para baixo). */
function drawing(id: string) {
  const outline = trackOutline(getTrack(id), 100);
  const xs = outline.map((p) => p[0]); const ys = outline.map((p) => p[1]);
  const left = Math.min(...xs); const top = Math.min(...ys);
  const width = Math.max(...xs) - left; const height = Math.max(...ys) - top;
  const span = (vals: number[]) => (vals.length ? Math.max(...vals) - Math.min(...vals) : 0);
  const mid = (vals: number[]) => (vals.length ? (Math.max(...vals) + Math.min(...vals)) / 2 : NaN);
  /** Pontos numa faixa horizontal (fração da altura, de cima) ou vertical (fração da largura, da esquerda). */
  const row = (from: number, to: number) => outline.filter((p) => p[1] >= top + from * height && p[1] <= top + to * height);
  const col = (from: number, to: number) => outline.filter((p) => p[0] >= left + from * width && p[0] <= left + to * width);
  return {
    outline, left, top, width, height,
    /** Largura da faixa horizontal (de `from` a `to` da altura). */
    widthAt: (from: number, to: number) => span(row(from, to).map((p) => p[0])),
    /** Centro (x) da faixa horizontal. */
    centerAt: (from: number, to: number) => mid(row(from, to).map((p) => p[0])),
    /** Altura da faixa vertical (de `from` a `to` da largura). */
    heightAt: (from: number, to: number) => span(col(from, to).map((p) => p[1])),
    /** Topo (menor y) e base (maior y) da faixa vertical, em fração da altura. */
    topAt: (from: number, to: number) => (Math.min(...col(from, to).map((p) => p[1])) - top) / height,
    bottomAt: (from: number, to: number) => (Math.max(...col(from, to).map((p) => p[1])) - top) / height,
    /** Menor largura entre as faixas de 5% de `from` a `to`, e a maior. */
    narrowest: (from: number, to: number) => Math.min(...bands(from, to).map(([a, b]) => span(row(a, b).map((p) => p[0])))),
    widest: (from: number, to: number) => Math.max(...bands(from, to).map(([a, b]) => span(row(a, b).map((p) => p[0])))),
    /** Ponta esquerda (ou direita) da faixa horizontal, em fração da largura (0 = esquerda). */
    leftAt: (from: number, to: number) => (Math.min(...row(from, to).map((p) => p[0])) - left) / width,
    rightAt: (from: number, to: number) => (Math.max(...row(from, to).map((p) => p[0])) - left) / width,
    /** Topo (ou base) de cada uma de `n` colunas de `from` a `to` da largura, em fração da altura (0 = topo). */
    tops: (n: number, from = 0, to = 1) => Array.from({ length: n }, (_, i) => {
      const c = col(from + ((to - from) * i) / n, from + ((to - from) * (i + 1)) / n);
      return c.length ? (Math.min(...c.map((p) => p[1])) - top) / height : NaN;
    }),
    bottoms: (n: number, from = 0, to = 1) => Array.from({ length: n }, (_, i) => {
      const c = col(from + ((to - from) * i) / n, from + ((to - from) * (i + 1)) / n);
      return c.length ? (Math.max(...c.map((p) => p[1])) - top) / height : NaN;
    }),
    /** Borda esquerda (ou direita, espelhada: 0 = borda direita) de cada uma de `n` faixas horizontais. */
    lefts: (n: number, from = 0, to = 1) => Array.from({ length: n }, (_, i) => {
      const r = row(from + ((to - from) * i) / n, from + ((to - from) * (i + 1)) / n);
      return r.length ? (Math.min(...r.map((p) => p[0])) - left) / width : NaN;
    }),
    rights: (n: number, from = 0, to = 1) => Array.from({ length: n }, (_, i) => {
      const r = row(from + ((to - from) * i) / n, from + ((to - from) * (i + 1)) / n);
      return r.length ? 1 - (Math.max(...r.map((p) => p[0])) - left) / width : NaN;
    }),
  };
}

/**
 * Corcovas de um perfil (de cima: menor = mais alto): os mínimos locais separados por um vale pelo menos `prom` mais
 * baixo dos dois lados. Devolve [coluna, altura] da esquerda para a direita.
 */
function humps(profile: number[], prom: number): Array<[number, number]> {
  const out: Array<[number, number]> = [];
  const n = profile.length;
  for (let i = 0; i < n; i++) {
    const v = profile[i];
    if (Number.isNaN(v) || (i > 0 && profile[i - 1] < v) || (i < n - 1 && profile[i + 1] <= v)) continue;
    // Quanto o perfil desce (y sobe) de cada lado antes de achar um ponto mais alto que este.
    const side = (dir: number) => {
      let worst = v;
      for (let j = i + dir; j >= 0 && j < n && !(profile[j] < v); j += dir) worst = Math.max(worst, profile[j]);
      return worst - v;
    };
    if (Math.min(side(-1), side(1)) >= prom) out.push([i, v]);
  }
  return out;
}
const bands = (from: number, to: number): Array<[number, number]> =>
  Array.from({ length: Math.max(1, Math.round((to - from) / 0.05)) }, (_, i) => [from + i * 0.05, from + (i + 1) * 0.05]);

/** Pares de trechos do contorno que se cruzam. */
function crossings(pts: Array<[number, number]>): number {
  const n = pts.length; let count = 0;
  const cross = (o: number[], a: number[], b: number[]) => (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0]);
  for (let i = 0; i < n; i++) for (let j = i + 2; j < n; j++) {
    if (i === 0 && j === n - 1) continue;
    const a = pts[i]; const b = pts[(i + 1) % n]; const c = pts[j]; const d = pts[(j + 1) % n];
    const d1 = cross(c, d, a); const d2 = cross(c, d, b); const d3 = cross(a, b, c); const d4 = cross(a, b, d);
    if (d1 * d2 < 0 && d3 * d4 < 0) count++;
  }
  return count;
}

/** As pistas que desenham (estado → pista); a cuia tem o bloco próprio acima. */
const DRAWN: Record<string, string> = {
  RJ: 'copacabana', SP: 'sampa_noite', MG: 'pampulha', ES: 'convento_penha', PR: 'curitiba', SC: 'floripa', DF: 'brasilia',
  GO: 'caldas_novas', MS: 'bonito', MT: 'transpantaneira', BA: 'porto_seguro', SE: 'aracaju', AL: 'maceio', PE: 'olinda',
  PB: 'campina_grande', RN: 'cajueiro_pirangi', CE: 'canoa_quebrada', PI: 'serra_capivara', MA: 'sao_luis', PA: 'belem',
  AM: 'manaus', AP: 'macapa', RR: 'monte_roraima', RO: 'porto_velho', AC: 'rio_branco', TO: 'palmas',
};

describe('Pistas com desenho: contorno limpo', () => {
  for (const [uf, id] of Object.entries(DRAWN)) {
    it(`${uf} ${id}: o contorno não se cruza nem encosta em si mesmo, cabe no minimapa e as curvas ficam no teto 6`, () => {
      const d = drawing(id);
      expect(crossings(d.outline), 'cruzamentos').toBe(0);
      // Dois trechos que não são vizinhos na volta ficam a pelo menos 1,2% do minimapa um do outro.
      const pts = d.outline; const n = pts.length; let gap = Infinity;
      for (let i = 0; i < n; i++) for (let j = i + 12; j < n; j++) {
        if (n - (j - i) < 12) continue;
        gap = Math.min(gap, Math.hypot(pts[i][0] - pts[j][0], pts[i][1] - pts[j][1]));
      }
      expect(gap, 'traço encostando').toBeGreaterThan(1.2);
      expect(d.width / d.height, 'proporção').toBeGreaterThan(0.4);
      expect(d.width / d.height, 'proporção').toBeLessThan(2.5);
      for (const s of getTrack(id).segments) expect(Math.abs(s.curve), `${id} seg ${s.index}`).toBeLessThanOrEqual(6);
    });
  }
});

describe('RJ · Orla de Copacabana desenha o Cristo Redentor', () => {
  // De pé no pedestal, de braços abertos: a envergadura é quase a altura; a cabeça (estreita, no meio) acima da linha
  // dos ombros; a túnica bem mais estreita que os braços; o pedestal um degrau mais largo que a barra da túnica.
  const d = drawing('copacabana');

  it('braços abertos: a faixa dos braços é a mais larga e vai quase de ponta a ponta; o corpo é bem mais estreito', () => {
    const arms = d.widest(0.1, 0.3);
    const body = d.widest(0.4, 0.75);
    expect(arms, `braços ${arms.toFixed(1)} × largura ${d.width.toFixed(1)}`).toBeGreaterThan(d.width * 0.95);
    expect(body, `corpo ${body.toFixed(1)} × braços ${arms.toFixed(1)}`).toBeLessThan(arms * 0.4);
    expect(d.width / d.height, 'envergadura ÷ altura').toBeGreaterThan(0.75);
    expect(d.width / d.height, 'envergadura ÷ altura').toBeLessThan(1.25);
  });

  it('a cabeça fica no alto, no meio, acima dos ombros: estreita, mas não um espinho', () => {
    const head = d.widthAt(0, 0.07);
    expect(head, `cabeça ${head.toFixed(1)}`).toBeLessThan(d.width * 0.2);
    expect(head, `cabeça ${head.toFixed(1)}`).toBeGreaterThan(d.width * 0.06);
    expect(Math.abs(d.centerAt(0, 0.07) - d.centerAt(0.4, 0.75)), 'cabeça fora do eixo').toBeLessThan(d.width * 0.04);
  });

  it('simétrico: os dois braços do mesmo tamanho (o eixo dos braços é o do corpo)', () => {
    expect(Math.abs(d.centerAt(0.1, 0.25) - d.centerAt(0.4, 0.75)), 'braço torto').toBeLessThan(d.width * 0.04);
  });

  it('em pé no pedestal: a base é um degrau mais larga que a barra da túnica', () => {
    const pedestal = d.widthAt(0.9, 0.97);
    const robe = d.widthAt(0.62, 0.75);
    expect(pedestal, `pedestal ${pedestal.toFixed(1)} × túnica ${robe.toFixed(1)}`).toBeGreaterThan(robe * 1.15);
    expect(pedestal, `pedestal ${pedestal.toFixed(1)} × braços`).toBeLessThan(d.width * 0.55);
  });
});

/** Fração das colunas em que o valor passa no teste. */
const share = (vals: number[], ok: (v: number) => boolean) => vals.filter(ok).length / vals.length;

describe('SP · Noite em Sampa desenha o MASP', () => {
  // A caixa suspensa nos dois pórticos, de lado: o teto reto de ponta a ponta, os pilares das pontas até o chão e o
  // vão livre embaixo da caixa, entre eles.
  const d = drawing('sampa_noite');

  it('mais larga que alta, com o teto reto de ponta a ponta', () => {
    expect(d.width / d.height).toBeGreaterThan(1.3); expect(d.width / d.height).toBeLessThan(2.4);
    expect(Math.max(...d.tops(20, 0.05, 0.95)), 'teto torto').toBeLessThan(0.06);
  });

  it('os pilares das pontas vão até o chão; entre eles, o vão livre embaixo da caixa', () => {
    expect(d.bottomAt(0, 0.05)).toBeGreaterThan(0.97); expect(d.bottomAt(0.95, 1)).toBeGreaterThan(0.97);
    const belly = d.bottomAt(0.3, 0.7);
    expect(belly, 'o vão livre').toBeLessThan(0.72);
    expect(belly, 'a caixa é grossa').toBeGreaterThan(0.4);
  });

  it('pilares finos: o vão livre ocupa quase toda a largura, e é simétrico', () => {
    const b = d.bottoms(40);
    expect(share(b, (v) => v < 0.8), 'colunas no vão').toBeGreaterThan(0.7);
    expect(Math.abs(b.findIndex((v) => v < 0.8) - [...b].reverse().findIndex((v) => v < 0.8)), 'um pilar mais grosso').toBeLessThanOrEqual(1);
  });
});

describe('MG · Lagoa da Pampulha desenha a igreja de Niemeyer', () => {
  // As abóbadas onduladas de lado: a grande (a nave) de um lado e as corcovas cada vez menores, no chão reto.
  const d = drawing('pampulha');

  it('no chão: a base é reta de ponta a ponta', () => {
    expect(Math.min(...d.bottoms(30, 0.03, 0.97)), 'base torta').toBeGreaterThan(0.95);
    expect(d.width / d.height).toBeGreaterThan(1.1); expect(d.width / d.height).toBeLessThan(2.2);
  });

  it('cinco abóbadas: a primeira é a mais alta e as outras ficam cada vez mais baixas', () => {
    const h = humps(d.tops(60), 0.04);
    expect(h.length, `corcovas ${JSON.stringify(h)}`).toBe(5);
    expect(h[0][1], 'a nave chega ao topo').toBeLessThan(0.03);
    for (let i = 1; i < h.length; i++) expect(h[i][1], `corcova ${i}`).toBeGreaterThan(h[i - 1][1] + 0.05);
  });

  it('a nave é a abóbada mais larga: ocupa mais de um terço do desenho', () => {
    // A cintura entre a nave e a primeira corcova pequena (o vale mais à esquerda) fica além de 1/3 da largura.
    const t = d.tops(60); const h = humps(t, 0.04);
    const valley = h.length > 1 ? t.slice(h[0][0], h[1][0]).indexOf(Math.max(...t.slice(h[0][0], h[1][0]))) + h[0][0] : 0;
    expect(valley / 60).toBeGreaterThan(0.33);
  });
});

describe('ES · Convento da Penha desenha o convento no alto do penhasco', () => {
  // O morro de pedra (largo no pé, afinando para cima, mais íngreme do lado do mar) e o convento no alto: um bloco
  // estreito de paredes retas em cima do topo do morro.
  const d = drawing('convento_penha');

  it('morro: largo no pé e cada vez mais estreito para cima, com a base reta', () => {
    const w = [0.9, 0.7, 0.5, 0.3, 0.15].map((f) => d.widthAt(f - 0.04, f + 0.04));
    for (let i = 1; i < w.length; i++) expect(w[i], w.map((x) => x.toFixed(1)).join(' > ')).toBeLessThan(w[i - 1] + 0.5);
    expect(w[0]).toBeGreaterThan(d.width * 0.85);
    expect(Math.min(...d.bottoms(30, 0.03, 0.97)), 'base torta').toBeGreaterThan(0.95);
  });

  it('o convento no alto: um bloco bem mais estreito que o topo do morro onde está', () => {
    expect(d.widthAt(0, 0.08)).toBeLessThan(d.width * 0.25);
    expect(d.widthAt(0, 0.08)).toBeLessThan(d.widthAt(0.16, 0.24) * 0.6);
    // Paredes retas: o bloco tem a mesma largura no alto e no pé.
    expect(Math.abs(d.widthAt(0.01, 0.04) - d.widthAt(0.06, 0.09))).toBeLessThan(d.width * 0.04);
  });

  it('penhasco: o topo fica para o lado do mar (direita), com a encosta desse lado mais curta', () => {
    const summit = d.centerAt(0, 0.08);
    expect((summit - d.left) / d.width).toBeGreaterThan(0.55);
    expect((summit - d.left) / d.width).toBeLessThan(0.8);
  });
});

describe('SC · Ponte Hercílio Luz desenha a ponte pênsil', () => {
  // De lado: as duas torres, o cabo em curva entre elas descendo até o tabuleiro no meio, e os estais das pontas
  // (faixas finas) descendo das torres até as ancoragens no chão, com o vão lateral aberto embaixo — preenchido, o
  // triângulo do estai fazia a ponte parecer duas montanhas.
  const d = drawing('floripa');

  it('comprida: bem mais larga que alta; tabuleiro reto entre as torres e ancoragens no chão nas pontas', () => {
    expect(d.width / d.height).toBeGreaterThan(1.5); expect(d.width / d.height).toBeLessThan(2.5);
    expect(Math.min(...d.bottoms(20, 0.3, 0.7)), 'tabuleiro torto').toBeGreaterThan(0.95);
    expect(d.bottomAt(0, 0.05), 'ancoragem esquerda').toBeGreaterThan(0.95);
    expect(d.bottomAt(0.95, 1), 'ancoragem direita').toBeGreaterThan(0.95);
  });

  it('os vãos laterais ficam abertos embaixo do estai (o estai é uma faixa fina, não um morro)', () => {
    expect(d.bottomAt(0.14, 0.2), 'vão esquerdo').toBeLessThan(0.85);
    expect(d.bottomAt(0.8, 0.86), 'vão direito').toBeLessThan(0.85);
  });

  it('duas torres no alto, uma de cada lado; o cabo afunda entre elas até perto do tabuleiro', () => {
    expect(d.topAt(0.18, 0.32), 'torre esquerda').toBeLessThan(0.04);
    expect(d.topAt(0.68, 0.82), 'torre direita').toBeLessThan(0.04);
    expect(d.topAt(0.47, 0.53), 'meio do vão').toBeGreaterThan(0.6);
    // O cabo é curvo: a um quarto do vão ele está no meio do caminho entre a torre e o fundo.
    const q = d.topAt(0.36, 0.38);
    expect(q).toBeGreaterThan(0.15); expect(q).toBeLessThan(0.6);
  });

  it('as pontas descem até as ancoragens, e as torres ficam simétricas', () => {
    expect(d.topAt(0, 0.04)).toBeGreaterThan(0.6); expect(d.topAt(0.96, 1)).toBeGreaterThan(0.6);
    const t = d.tops(50);
    const left = t.indexOf(Math.min(...t.slice(0, 25))); const right = 25 + t.slice(25).indexOf(Math.min(...t.slice(25)));
    expect(Math.abs(left + right - 49), `torres nas colunas ${left} e ${right}`).toBeLessThanOrEqual(2);
  });
});

describe('DF · Eixo Monumental desenha o avião do Plano Piloto', () => {
  // Visto de cima, bico para cima: o eixo (a fuselagem) estreito; as asas arqueadas para trás, com envergadura maior
  // que o comprimento; a cauda embaixo.
  const d = drawing('brasilia');

  it('envergadura maior que o comprimento: as asas vão de ponta a ponta', () => {
    expect(d.width / d.height).toBeGreaterThan(1.05); expect(d.width / d.height).toBeLessThan(1.8);
    expect(d.widest(0.25, 0.75)).toBeGreaterThan(d.width * 0.95);
  });

  it('o eixo: bico e fuselagem estreitos, no meio', () => {
    expect(d.widthAt(0, 0.1), 'bico').toBeLessThan(d.width * 0.1);
    expect(d.narrowest(0.72, 0.86), 'fuselagem').toBeLessThan(d.width * 0.1);
    expect(Math.abs(d.centerAt(0, 0.1) - d.centerAt(0.72, 0.86)), 'eixo torto').toBeLessThan(d.width * 0.03);
  });

  it('as asas arqueiam para trás: a ponta fica bem mais baixa que a raiz, dos dois lados', () => {
    expect(d.topAt(0, 0.05) - d.topAt(0.36, 0.41)).toBeGreaterThan(0.12);
    expect(d.topAt(0.95, 1) - d.topAt(0.59, 0.64)).toBeGreaterThan(0.12);
  });

  it('a cauda embaixo é mais larga que a fuselagem, no eixo', () => {
    expect(d.widthAt(0.93, 1)).toBeGreaterThan(d.narrowest(0.72, 0.86) * 2);
    expect(Math.abs(d.centerAt(0.93, 1) - d.centerAt(0, 0.1))).toBeLessThan(d.width * 0.03);
  });
});

describe('PR · Ópera de Arame desenha uma araucária', () => {
  // O candelabro: tronco reto e fino, e a copa em taça achatada no alto — larga em cima, afinando até o tronco, com
  // os tufos dos galhos formando o topo quase plano.
  const d = drawing('curitiba');

  it('tronco fino e comprido embaixo, no eixo da copa', () => {
    expect(d.narrowest(0.6, 0.95), 'tronco').toBeLessThan(d.width * 0.12);
    expect(d.widest(0.6, 0.95), 'tronco sem galho').toBeLessThan(d.width * 0.15);
    expect(Math.abs(d.centerAt(0.7, 0.9) - d.centerAt(0, 0.2)), 'tronco torto').toBeLessThan(d.width * 0.03);
  });

  it('a copa é uma taça: a mais larga em cima e afinando para baixo até o tronco', () => {
    expect(d.widest(0, 0.25)).toBeGreaterThan(d.width * 0.9);
    const w = [0.2, 0.3, 0.4].map((f) => d.widthAt(f - 0.02, f + 0.02));
    expect(w[1], w.map((x) => x.toFixed(1)).join(' > ')).toBeLessThan(w[0]);
    expect(w[2], w.map((x) => x.toFixed(1)).join(' > ')).toBeLessThan(w[1]);
    expect(d.width / d.height).toBeGreaterThan(0.8); expect(d.width / d.height).toBeLessThan(1.4);
  });

  it('o topo são os tufos dos galhos: várias corcovas quase na mesma altura (copa achatada)', () => {
    const h = humps(d.tops(60), 0.025);
    expect(h.length, `tufos ${JSON.stringify(h)}`).toBeGreaterThanOrEqual(5);
    const hs = h.map(([, y]) => y);
    expect(Math.max(...hs) - Math.min(...hs), 'copa torta').toBeLessThan(0.1);
  });
});

describe('GO · Águas de Caldas Novas desenha uma gota d\'água', () => {
  // A gota: ponta fina em cima, os lados abrindo sem cintura e o fundo redondo.
  const d = drawing('caldas_novas');

  it('em pé, mais alta que larga, simétrica', () => {
    expect(d.height / d.width).toBeGreaterThan(1.15); expect(d.height / d.width).toBeLessThan(1.7);
    expect(Math.abs(d.centerAt(0, 0.08) - d.centerAt(0.55, 0.75)), 'torta').toBeLessThan(d.width * 0.03);
  });

  it('a ponta em cima é fina e os lados só abrem até a parte mais larga, embaixo do meio (sem cintura)', () => {
    expect(d.widthAt(0, 0.06), 'ponta').toBeLessThan(d.width * 0.12);
    const w = [0.1, 0.2, 0.3, 0.4, 0.5, 0.6].map((f) => d.widthAt(f - 0.02, f + 0.02));
    for (let i = 1; i < w.length; i++) expect(w[i], w.map((x) => x.toFixed(1)).join(' < ')).toBeGreaterThan(w[i - 1]);
    expect(d.widest(0.55, 0.8)).toBeGreaterThan(d.width * 0.97);
  });

  it('o fundo é redondo: a largura cai aos poucos até embaixo, sem quina', () => {
    const rows = [0.7, 0.8, 0.88, 0.94, 0.98].map((f) => d.widthAt(f - 0.015, f + 0.015));
    for (let i = 1; i < rows.length; i++) expect(rows[i], rows.map((w) => w.toFixed(1)).join(' > ')).toBeLessThan(rows[i - 1]);
    expect(rows[rows.length - 1]).toBeLessThan(d.width * 0.5);
  });
});

describe('MS · Rios de Bonito desenha um peixe (a piraputanga)', () => {
  // De lado, nadando para a direita: a cauda em V à esquerda (dois lobos e o entalhe), o pedúnculo fino, o corpo
  // alto no meio, a barbatana nas costas e a cabeça afinando até o focinho.
  const d = drawing('bonito');

  it('comprido: mais largo que alto', () => {
    expect(d.width / d.height).toBeGreaterThan(1.4); expect(d.width / d.height).toBeLessThan(2.4);
  });

  it('cauda em V: os dois lobos chegam à ponta esquerda e o entalhe entre eles entra no meio', () => {
    expect(d.leftAt(0.04, 0.16), 'lobo de cima').toBeLessThan(0.04);
    expect(d.leftAt(0.84, 0.96), 'lobo de baixo').toBeLessThan(0.04);
    expect(d.leftAt(0.47, 0.53), 'entalhe').toBeGreaterThan(0.1);
  });

  it('pedúnculo fino antes da cauda, corpo alto no meio, e a cabeça afinando até o focinho', () => {
    const body = d.heightAt(0.45, 0.55);
    expect(d.heightAt(0.2, 0.24), 'pedúnculo').toBeLessThan(body * 0.45);
    expect(d.heightAt(0.95, 1), 'focinho').toBeLessThan(body * 0.35);
  });

  it('a barbatana das costas é o ponto mais alto, fina, sobre o corpo', () => {
    const top = d.tops(40);
    const at = top.indexOf(Math.min(...top)) / 40;
    expect(at, 'barbatana fora do corpo').toBeGreaterThan(0.35); expect(at).toBeLessThan(0.7);
    expect(d.widthAt(0, 0.08), 'barbatana larga demais').toBeLessThan(d.width * 0.15);
  });
});

describe('MT · Transpantaneira desenha um jacaré visto de cima', () => {
  // Cabeça para cima: o focinho comprido e fino, as quatro patas abertas (as da frente e as de trás), o corpo entre
  // elas e o rabo comprido afinando até a ponta.
  const d = drawing('transpantaneira');

  it('comprido: bem mais alto que largo', () => {
    expect(d.height / d.width).toBeGreaterThan(1.3); expect(d.height / d.width).toBeLessThan(2.4);
  });

  it('focinho fino em cima e rabo fino embaixo', () => {
    expect(d.widthAt(0, 0.06), 'focinho').toBeLessThan(d.width * 0.25);
    expect(d.widthAt(0.95, 1), 'ponta do rabo').toBeLessThan(d.width * 0.12);
    expect(d.widest(0.75, 0.95), 'rabo grosso').toBeLessThan(d.width * 0.35);
  });

  it('quatro patas: as da frente e as de trás abrem quase a largura toda; o corpo entre elas é estreito', () => {
    expect(d.widest(0.15, 0.4), 'patas da frente').toBeGreaterThan(d.width * 0.85);
    expect(d.widest(0.45, 0.75), 'patas de trás').toBeGreaterThan(d.width * 0.85);
    expect(d.narrowest(0.3, 0.6), 'corpo').toBeLessThan(d.width * 0.55);
  });
});

describe('BA · Costa de Porto Seguro desenha a caravela de Cabral', () => {
  // De lado: o casco comprido embaixo (popa alta à esquerda, proa à direita) e as três velas em cima, a do meio a
  // mais alta, separadas até perto do convés.
  const d = drawing('porto_seguro');

  it('o casco embaixo é a parte mais larga', () => {
    expect(d.widest(0.8, 0.95)).toBeGreaterThan(d.width * 0.75);
    expect(d.width / d.height).toBeGreaterThan(0.9); expect(d.width / d.height).toBeLessThan(1.7);
  });

  it('três velas em cima, a do meio a mais alta', () => {
    const h = humps(d.tops(60), 0.12);
    expect(h.length, `velas ${JSON.stringify(h)}`).toBe(3);
    expect(h[1][1], 'a vela do meio').toBeLessThan(Math.min(h[0][1], h[2][1]));
  });

  it('as velas são separadas: os vãos entre elas descem até perto do convés', () => {
    const t = d.tops(60); const h = humps(t, 0.12);
    for (let i = 1; i < h.length; i++) {
      const valley = Math.max(...t.slice(h[i - 1][0], h[i][0]));
      expect(valley, `vão ${i}`).toBeGreaterThan(0.45);
    }
  });
});

/** Corcovas de um perfil de baixo (base de cada coluna) ou de borda: os vales viram corcovas invertendo o perfil. */
const dips = (profile: number[], prom: number) => humps(profile.map((v) => 1 - v), prom);

describe('SE · Orla de Atalaia desenha um caranguejo', () => {
  // Visto de cima, garras para cima: o corpo largo, as duas garras abertas (dois dedos cada) uma de cada lado, e as
  // patas espetadas para os lados, embaixo.
  const d = drawing('aracaju');

  it('mais largo que alto e simétrico', () => {
    expect(d.width / d.height).toBeGreaterThan(1.0); expect(d.width / d.height).toBeLessThan(1.7);
    expect(Math.abs(d.centerAt(0.4, 0.7) - (d.left + d.width / 2)), 'torto').toBeLessThan(d.width * 0.03);
  });

  it('as duas garras são o alto do desenho, nas pontas; entre elas (os olhos) fica mais baixo', () => {
    expect(Math.min(...d.tops(10, 0, 0.35)), 'garra esquerda').toBeLessThan(0.05);
    expect(Math.min(...d.tops(10, 0.65, 1)), 'garra direita').toBeLessThan(0.05);
    expect(Math.min(...d.tops(10, 0.4, 0.6)), 'o meio').toBeGreaterThan(0.25);
  });

  it('cada garra está aberta: dois dedos com o vão entre eles', () => {
    expect(humps(d.tops(30, 0, 0.4), 0.06).length, 'dedos da esquerda').toBeGreaterThanOrEqual(2);
    expect(humps(d.tops(30, 0.6, 1), 0.06).length, 'dedos da direita').toBeGreaterThanOrEqual(2);
  });

  it('patas: pelo menos três pontas de cada lado, na metade de baixo', () => {
    // A ponta da pata é onde a borda chega mais perto da lateral do desenho: um mínimo do perfil da borda.
    expect(humps(d.lefts(40, 0.45, 1), 0.04).length, 'patas da esquerda').toBeGreaterThanOrEqual(3);
    expect(humps(d.rights(40, 0.45, 1), 0.04).length, 'patas da direita').toBeGreaterThanOrEqual(3);
  });
});

describe('AL · Orla de Maceió desenha uma jangada', () => {
  // A vela triangular alta presa no mastro (lado reto, em pé), a jangada comprida e baixa embaixo, nas ondas.
  const d = drawing('maceio');

  it('a vela: triangular, fina no alto e abrindo para baixo, com o mastro reto do lado', () => {
    const w = [0.12, 0.35, 0.6].map((f) => d.widthAt(f - 0.03, f + 0.03));
    expect(w[0], w.map((x) => x.toFixed(1)).join(' < ')).toBeLessThan(w[1]);
    expect(w[1], w.map((x) => x.toFixed(1)).join(' < ')).toBeLessThan(w[2]);
    const mast = d.lefts(10, 0.1, 0.65);
    expect(Math.max(...mast) - Math.min(...mast), 'mastro torto').toBeLessThan(0.03);
    expect(Math.min(...mast), 'o mastro fica sobre a jangada, não na ponta').toBeGreaterThan(0.2);
  });

  it('a jangada embaixo é a parte mais larga, baixa e comprida', () => {
    expect(d.widest(0.8, 1)).toBeGreaterThan(d.width * 0.9);
    expect(d.widthAt(0.6, 0.7), 'a vela é mais estreita que a jangada').toBeLessThan(d.width * 0.75);
    expect(d.height / d.width).toBeGreaterThan(0.8); expect(d.height / d.width).toBeLessThan(1.4);
  });

  it('as ondas: a base é ondulada (pelo menos três cavas)', () => {
    expect(dips(d.bottoms(40, 0.05, 0.95), 0.025).length).toBeGreaterThanOrEqual(3);
  });
});

describe('PE · Ladeiras de Olinda desenha uma sombrinha de frevo', () => {
  // A cúpula redonda em cima, a borda recortada em gomos entre as varetas, e o cabo fino no meio, embaixo.
  const d = drawing('olinda');

  it('a cúpula é a parte larga em cima; o cabo, fino e no meio, embaixo', () => {
    expect(d.widest(0.2, 0.55)).toBeGreaterThan(d.width * 0.95);
    expect(d.narrowest(0.7, 0.95), 'cabo').toBeLessThan(d.width * 0.1);
    expect(Math.abs(d.centerAt(0.75, 0.95) - d.centerAt(0, 0.1)), 'cabo torto').toBeLessThan(d.width * 0.03);
    expect(d.height / d.width).toBeGreaterThan(0.75); expect(d.height / d.width).toBeLessThan(1.3);
  });

  it('a cúpula é redonda: alta no meio e descendo para as pontas', () => {
    const t = d.tops(20);
    expect(t[10], 'o alto da cúpula').toBeLessThan(0.06);
    expect(t[2] - t[10]).toBeGreaterThan(0.15); expect(t[17] - t[10]).toBeGreaterThan(0.15);
  });

  it('a borda é recortada: gomos entre as varetas dos dois lados do cabo', () => {
    expect(dips(d.bottoms(40, 0.03, 0.44), 0.02).length, 'gomos da esquerda').toBeGreaterThanOrEqual(3);
    expect(dips(d.bottoms(40, 0.56, 0.97), 0.02).length, 'gomos da direita').toBeGreaterThanOrEqual(3);
  });
});

describe('PB · Parque do Povo desenha um balão junino', () => {
  // A ponta em cima, o bojo largo no alto do meio com a franja de papel recortada embaixo, afinando até a boca, e a
  // bucha acesa pendurada embaixo.
  const d = drawing('campina_grande');

  it('ponta fina em cima e o bojo largo logo abaixo, simétrico', () => {
    expect(d.widthAt(0, 0.06), 'ponta').toBeLessThan(d.width * 0.15);
    expect(d.widest(0.25, 0.5)).toBeGreaterThan(d.width * 0.95);
    expect(Math.abs(d.centerAt(0, 0.06) - d.centerAt(0.88, 1)), 'torto').toBeLessThan(d.width * 0.03);
    expect(d.height / d.width).toBeGreaterThan(1.0); expect(d.height / d.width).toBeLessThan(1.7);
  });

  it('afina até a boca, e a bucha pendurada embaixo é estreita', () => {
    expect(d.widthAt(0.74, 0.8), 'boca').toBeLessThan(d.width * 0.3);
    expect(d.widthAt(0.9, 1), 'bucha').toBeLessThan(d.width * 0.2);
  });

  it('a franja de papel: dentes pendurados dos dois lados, embaixo do bojo', () => {
    expect(dips(d.bottoms(30, 0, 0.32), 0.03).length, 'franja da esquerda').toBeGreaterThanOrEqual(2);
    expect(dips(d.bottoms(30, 0.68, 1), 0.03).length, 'franja da direita').toBeGreaterThanOrEqual(2);
  });
});

describe('RN · Cajueiro de Pirangi desenha o maior cajueiro do mundo', () => {
  // O caju (fruto com a castanha) não se lê numa linha só: em três tentativas saiu bolota, sino e coelho (docs/PISTAS.md,
  // "Pistas com desenho"). Fica a árvore da própria pista: a copa larga e baixa cheia de tufos, mais alta no meio, e
  // os galhos que descem até o chão, com os vãos em arco embaixo.
  const d = drawing('cajueiro_pirangi');

  it('larga e baixa: a árvore se espalha para os lados', () => {
    expect(d.width / d.height).toBeGreaterThan(1.8); expect(d.width / d.height).toBeLessThan(2.5);
  });

  it('a copa cheia de tufos, mais alta no meio e descendo para as pontas', () => {
    expect(humps(d.tops(60), 0.03).length, 'tufos').toBeGreaterThanOrEqual(5);
    const t = d.tops(20);
    expect(Math.min(...t.slice(8, 12)), 'o alto da copa').toBeLessThan(0.06);
    expect(t[1] - Math.min(...t.slice(8, 12))).toBeGreaterThan(0.2);
    expect(t[18] - Math.min(...t.slice(8, 12))).toBeGreaterThan(0.2);
  });

  it('os galhos descem até o chão: embaixo, pés no chão entre vãos em arco (pelo menos três vãos)', () => {
    const b = d.bottoms(60);
    expect(share(b, (v) => v > 0.97), 'pés no chão').toBeGreaterThan(0.15);
    expect(humps(b, 0.15).length, 'vãos').toBeGreaterThanOrEqual(3);
  });
});

describe('CE · Falésias de Canoa Quebrada desenha a lua crescente', () => {
  // O símbolo das falésias: a lua crescente com as pontas para a direita — o dorso redondo à esquerda e o vão aberto.
  const d = drawing('canoa_quebrada');

  it('em pé, mais alta que larga', () => {
    expect(d.height / d.width).toBeGreaterThan(1.0); expect(d.height / d.width).toBeLessThan(2.0);
  });

  it('as duas pontas chegam à direita, em cima e embaixo, simétricas; no meio, o vão aberto', () => {
    expect(d.rightAt(0, 0.1), 'ponta de cima').toBeGreaterThan(0.8);
    expect(d.rightAt(0.9, 1), 'ponta de baixo').toBeGreaterThan(0.8);
    expect(Math.abs(d.rightAt(0, 0.1) - d.rightAt(0.9, 1)), 'torta').toBeLessThan(0.06);
    expect(d.rightAt(0.45, 0.55), 'o vão').toBeLessThan(0.45);
  });

  it('o dorso é redondo: encosta na esquerda no meio e se afasta dela para as pontas', () => {
    const l = [0.5, 0.3, 0.15, 0.05].map((f) => d.leftAt(f - 0.02, f + 0.02));
    expect(l[0]).toBeLessThan(0.03);
    for (let i = 1; i < l.length; i++) expect(l[i], l.map((x) => x.toFixed(2)).join(' < ')).toBeGreaterThan(l[i - 1]);
  });
});

describe('PI · Serra da Capivara desenha uma capivara', () => {
  // De lado, olhando para a direita: o corpo de barril, a cabeça grande de focinho rombudo, a orelhinha em cima e as
  // quatro patas curtas embaixo.
  const d = drawing('serra_capivara');

  it('de lado: mais comprida que alta, o corpo um bloco largo', () => {
    expect(d.width / d.height).toBeGreaterThan(1.0); expect(d.width / d.height).toBeLessThan(1.8);
    expect(d.widest(0.3, 0.6)).toBeGreaterThan(d.width * 0.85);
  });

  it('quatro patas embaixo', () => {
    expect(dips(d.bottoms(60), 0.05).length, 'patas').toBeGreaterThanOrEqual(4);
  });

  it('a cabeça à direita, de focinho rombudo (a frente reta e alta), com a orelha no alto', () => {
    expect(d.heightAt(0.97, 1), 'focinho').toBeGreaterThan(0.18);
    const t = d.tops(40);
    const ear = t.indexOf(Math.min(...t)) / 40;
    expect(ear, 'a orelha (o ponto mais alto) fica na cabeça').toBeGreaterThan(0.6);
  });
});

describe('MA · Casarões de São Luís desenha a cabeça do boi (bumba-meu-boi)', () => {
  // De frente: os chifres abrindo para os lados e subindo até as pontas, as orelhas para fora embaixo deles, a cara
  // comprida afinando até o focinho.
  const d = drawing('sao_luis');

  it('os chifres: as pontas no alto, nos dois cantos; a testa no meio fica mais baixa', () => {
    expect(Math.min(...d.tops(10, 0, 0.2)), 'chifre esquerdo').toBeLessThan(0.05);
    expect(Math.min(...d.tops(10, 0.8, 1)), 'chifre direito').toBeLessThan(0.05);
    expect(Math.min(...d.tops(10, 0.4, 0.6)), 'testa').toBeGreaterThan(0.2);
  });

  it('as orelhas saem para os lados e a cara afina embaixo até o focinho', () => {
    expect(d.widest(0.3, 0.55), 'orelhas').toBeGreaterThan(d.width * 0.6);
    expect(d.widest(0.65, 0.85), 'cara').toBeLessThan(d.width * 0.35);
    expect(d.widthAt(0.92, 1), 'focinho').toBeLessThan(d.width * 0.3);
    expect(d.widthAt(0.92, 1), 'focinho').toBeGreaterThan(d.width * 0.1);
  });

  it('simétrica', () => {
    expect(Math.abs(d.centerAt(0.7, 1) - (d.left + d.width / 2)), 'torta').toBeLessThan(d.width * 0.03);
  });
});

describe('PA · Ver-o-Peso desenha o Mercado de Ferro', () => {
  // De frente: o prédio comprido e baixo, de base reta, com as quatro torres de telhado pontudo acima do telhado.
  const d = drawing('belem');

  it('comprido, de base reta', () => {
    expect(d.width / d.height).toBeGreaterThan(1.2); expect(d.width / d.height).toBeLessThan(2.2);
    expect(Math.min(...d.bottoms(30, 0.03, 0.97)), 'base torta').toBeGreaterThan(0.95);
  });

  it('quatro torres acima do telhado', () => {
    const h = humps(d.tops(60), 0.15);
    expect(h.length, `torres ${JSON.stringify(h)}`).toBe(4);
  });

  it('o telhado entre as torres é reto e baixo', () => {
    const t = d.tops(60); const h = humps(t, 0.15);
    const valleys = h.slice(1).map(([i], k) => Math.max(...t.slice(h[k][0], i)));
    expect(Math.max(...valleys) - Math.min(...valleys), 'telhado torto').toBeLessThan(0.06);
    expect(Math.min(...valleys), 'telhado alto demais').toBeGreaterThan(0.3);
  });
});

describe('AM · Encontro das Águas desenha a cúpula do Teatro Amazonas', () => {
  // A cúpula redonda (em sino) no meio, com a lanterna e a agulha em cima, sobre o tambor e o prédio largo embaixo.
  const d = drawing('manaus');

  it('o prédio embaixo é largo, de base reta', () => {
    expect(d.widest(0.8, 1)).toBeGreaterThan(d.width * 0.95);
    expect(Math.min(...d.bottoms(30, 0.03, 0.97)), 'base torta').toBeGreaterThan(0.95);
  });

  it('a cúpula no meio: agulha fina no alto, e a cúpula abrindo para baixo até o tambor', () => {
    expect(d.widthAt(0, 0.06), 'agulha').toBeLessThan(d.width * 0.08);
    expect(Math.abs(d.centerAt(0, 0.06) - (d.left + d.width / 2)), 'fora do meio').toBeLessThan(d.width * 0.03);
    const w = [0.22, 0.32, 0.42].map((f) => d.widthAt(f - 0.02, f + 0.02));
    expect(w[1], w.map((x) => x.toFixed(1)).join(' < ')).toBeGreaterThan(w[0]);
    expect(w[2], w.map((x) => x.toFixed(1)).join(' < ')).toBeGreaterThan(w[1]);
    expect(w[2], 'a cúpula é mais estreita que o prédio').toBeLessThan(d.width * 0.6);
  });
});

describe('AP · Marco Zero do Equador desenha a Fortaleza de São José', () => {
  // A planta da fortaleza: o quadrado das muralhas com os quatro baluartes em ponta de lança nos cantos.
  const d = drawing('macapa');
  const near = (fx: number, fy: number) => Math.min(...d.outline.map((p) => Math.hypot((p[0] - d.left) / d.width - fx, (p[1] - d.top) / d.height - fy)));

  it('quadrada e simétrica', () => {
    expect(d.width / d.height).toBeGreaterThan(0.9); expect(d.width / d.height).toBeLessThan(1.1);
    expect(Math.abs(d.centerAt(0.4, 0.6) - (d.left + d.width / 2)), 'torta').toBeLessThan(d.width * 0.03);
  });

  it('as pontas dos quatro baluartes chegam aos quatro cantos', () => {
    for (const [fx, fy] of [[0, 0], [1, 0], [1, 1], [0, 1]]) expect(near(fx, fy), `canto ${fx},${fy}`).toBeLessThan(0.08);
  });

  it('as muralhas entre os baluartes ficam recolhidas, nos quatro lados', () => {
    expect(d.leftAt(0.45, 0.55), 'oeste').toBeGreaterThan(0.15);
    expect(d.rightAt(0.45, 0.55), 'leste').toBeLessThan(0.85);
    expect(d.topAt(0.45, 0.55), 'norte').toBeGreaterThan(0.15);
    expect(d.bottomAt(0.45, 0.55), 'sul').toBeLessThan(0.85);
  });
});

describe('RR · Monte Roraima desenha o tepui', () => {
  // A montanha de topo achatado: o planalto largo e reto em cima, os paredões quase verticais, e as encostas abrindo
  // até a base reta.
  const d = drawing('monte_roraima');

  it('comprido e de base reta', () => {
    expect(d.width / d.height).toBeGreaterThan(1.4); expect(d.width / d.height).toBeLessThan(2.4);
    expect(Math.min(...d.bottoms(30, 0.03, 0.97)), 'base torta').toBeGreaterThan(0.95);
    expect(d.widest(0.85, 1)).toBeGreaterThan(d.width * 0.95);
  });

  it('o topo achatado: largo e reto (só rochas pequenas)', () => {
    expect(Math.max(...d.tops(20, 0.3, 0.7)), 'topo torto').toBeLessThan(0.1);
    expect(d.widthAt(0, 0.12), 'planalto estreito').toBeGreaterThan(d.width * 0.5);
  });

  it('os paredões: quase verticais no alto, a montanha só abre embaixo', () => {
    expect(d.widthAt(0.3, 0.35) - d.widthAt(0.05, 0.1), 'paredão inclinado').toBeLessThan(d.width * 0.12);
    expect(d.widthAt(0.85, 0.9) - d.widthAt(0.3, 0.35), 'sem encosta').toBeGreaterThan(d.width * 0.2);
  });
});

describe('RO · Madeira-Mamoré desenha a locomotiva', () => {
  // De lado, indo para a direita: a cabine alta atrás, a caldeira comprida e baixa, a chaminé na frente (o ponto mais
  // alto), o limpa-trilhos e as rodas grandes embaixo.
  const d = drawing('porto_velho');

  it('de lado: mais comprida que alta', () => {
    expect(d.width / d.height).toBeGreaterThan(1.1); expect(d.width / d.height).toBeLessThan(1.8);
  });

  it('a chaminé na frente é o alto; a cabine atrás é alta; a caldeira no meio, mais baixa', () => {
    const t = d.tops(40);
    const chimney = t.indexOf(Math.min(...t)) / 40;
    expect(chimney, 'chaminé').toBeGreaterThan(0.6); expect(chimney).toBeLessThan(0.9);
    expect(d.topAt(0.05, 0.25), 'cabine').toBeLessThan(0.15);
    expect(d.topAt(0.38, 0.45), 'caldeira').toBeGreaterThan(0.25);
  });

  it('as rodas embaixo: pelo menos três', () => {
    expect(dips(d.bottoms(60, 0.15, 0.95), 0.04).length, 'rodas').toBeGreaterThanOrEqual(3);
  });
});

describe('AC · Gameleira de Rio Branco desenha a gameleira', () => {
  // A folha de seringueira (três folíolos) foi a primeira escolha, mas o desenho é de pontas finas e lados retos e a
  // pista é de mata: sem trecho longo em curva, nada afasta as árvores da beira e o cenário estoura o orçamento
  // (tests/scenery-forma.test.ts; docs/PISTAS.md, "Pistas com desenho"). Fica a árvore que dá nome à pista: a copa em
  // domo cheia de tufos, o tronco grosso no meio e a base alargando nas raízes.
  const d = drawing('rio_branco');

  it('em pé, a copa em domo: tão larga quanto alta, o alto no meio e descendo para os lados', () => {
    expect(d.width / d.height).toBeGreaterThan(0.85); expect(d.width / d.height).toBeLessThan(1.2);
    const t = d.tops(20);
    const crown = Math.min(...t.slice(8, 12));
    expect(crown, 'o alto da copa').toBeLessThan(0.05);
    expect(t[1] - crown, 'lado esquerdo').toBeGreaterThan(0.2);
    expect(t[18] - crown, 'lado direito').toBeGreaterThan(0.2);
  });

  it('a copa cheia de tufos: em volta do domo, o contorno sobe e desce', () => {
    // Perfil polar a partir do meio da copa, de um lado ao outro por cima (no domo, a coluna de cima não acha o tufo
    // que está na encosta).
    const cx = d.left + d.width / 2; const cy = d.top + 0.36 * d.height;
    const crown = d.outline.filter((p) => p[1] < d.top + 0.65 * d.height);
    const polar = Array.from({ length: 81 }, (_, i) => {
      const ang = Math.PI + (i / 80) * Math.PI; // 180° (esquerda) → 270° (em cima) → 360° (direita)
      let best = 0;
      for (const p of crown) {
        const da = Math.abs(((((Math.atan2(p[1] - cy, p[0] - cx) - ang) % (2 * Math.PI)) + 3 * Math.PI) % (2 * Math.PI)) - Math.PI);
        if (da < Math.PI / 120) best = Math.max(best, Math.hypot(p[0] - cx, p[1] - cy));
      }
      return best;
    });
    const rmax = Math.max(...polar);
    expect(humps(polar.map((r) => 1 - r / rmax), 0.03).length, 'tufos').toBeGreaterThanOrEqual(6);
  });

  it('o tronco grosso embaixo da copa, no meio, e a base alargando nas raízes', () => {
    const trunk = d.narrowest(0.72, 0.85);
    expect(trunk / d.width, 'tronco').toBeGreaterThan(0.12); expect(trunk / d.width).toBeLessThan(0.3);
    expect(Math.abs(d.centerAt(0.74, 0.84) - (d.left + d.width / 2)), 'tronco torto').toBeLessThan(d.width * 0.03);
    expect(d.widthAt(0.95, 1), 'raízes').toBeGreaterThan(trunk * 2);
  });
});

describe('TO · Ponte de Palmas desenha o sol com raios (o da bandeira do Tocantins)', () => {
  // O disco com os raios em ponta em volta, por igual.
  const d = drawing('palmas');
  const cx = d.left + d.width / 2; const cy = d.top + d.height / 2;
  const polar = Array.from({ length: 360 }, (_, a) => {
    const ang = (a * Math.PI) / 180;
    let best = 0;
    for (const p of d.outline) {
      const pa = Math.atan2(p[1] - cy, p[0] - cx);
      // Distância angular em [0, π] (atan2 dá −π..π; o ângulo procurado vai de 0 a 2π).
      const da = Math.abs(((((pa - ang) % (2 * Math.PI)) + 3 * Math.PI) % (2 * Math.PI)) - Math.PI);
      if (da < Math.PI / 180) best = Math.max(best, Math.hypot(p[0] - cx, p[1] - cy));
    }
    return best;
  });
  const rmax = Math.max(...polar);

  it('redondo: tão alto quanto largo', () => {
    expect(d.width / d.height).toBeGreaterThan(0.9); expect(d.width / d.height).toBeLessThan(1.1);
  });

  it('pelo menos dez raios em ponta em volta do disco', () => {
    // Raio = máximo local do perfil polar com vale de pelo menos 15% de cada lado (perfil circular: dá a volta).
    const p = polar.map((r) => 1 - r / rmax);
    const start = p.indexOf(Math.max(...p));
    const rot = [...p.slice(start), ...p.slice(0, start)];
    expect(humps(rot, 0.15).length).toBeGreaterThanOrEqual(10);
  });

  it('os raios saem de um disco: os vales ficam longe do centro, mas bem dentro das pontas', () => {
    const rmin = Math.min(...polar.filter((r) => r > 0));
    expect(rmin / rmax).toBeGreaterThan(0.5); expect(rmin / rmax).toBeLessThan(0.8);
  });
});

// ───────── Trecho `bend` e desenhos em curva (docs/PISTAS.md, "Desenhos em cartum"; onda K, frente K5) ─────────

describe('trecho bend: curva constante, sem rampa', () => {
  const def = (ops: TrackDef['ops']): TrackDef => ({ ...trackDef('copacabana'), id: 'teste-bend', ops });

  it('bend(L, c) gera L segmentos de curva exatamente c — sem a entrada e a saída do cv', () => {
    const t = buildTrack(def([{ op: 'pit', length: 40 }, { op: 'bend', length: 10, curve: 2.5 }, { op: 'straight', length: 50 }]));
    expect(t.segments.length).toBe(100);
    expect(t.segments.slice(40, 50).map((s) => s.curve)).toEqual(new Array(10).fill(2.5));
    expect(t.segments[39].curve).toBe(0);
    expect(t.segments[50].curve).toBe(0);
  });

  it('bend com hill sobe o desnível dentro do trecho, como o cv', () => {
    const t = buildTrack(def([{ op: 'pit', length: 40 }, { op: 'bend', length: 20, curve: -1, hill: 6 }, { op: 'bend', length: 20, curve: 1, hill: -6 }, { op: 'straight', length: 40 }]));
    expect(t.segments.length).toBe(120);
    expect(t.segments[59].y1 - t.segments[40].y0).toBeGreaterThan(0);
    expect(t.segments.slice(40, 60).every((s) => s.curve === -1)).toBe(true);
  });
});

describe('desenho em curva: o traço vira bend (ferramenta scripts/shape-to-track.ts)', () => {
  it('o Cristo é codificado só em pit, reta e bend: nenhum cv, pico ≤ cmax e a volta com os segmentos pedidos', () => {
    // sigma 6: o padrão da ferramenta (solveArt), o mesmo do --apply da copacabana.
    const fit = fitArt(ART.copacabana(), 1800, 3, 6);
    expect(fit.ops.filter((o) => o.op === 'curve')).toEqual([]);
    expect(fit.ops.some((o) => o.op === 'bend')).toBe(true);
    expect(fit.ops.reduce((a, o) => a + o.length, 0)).toBe(1800);
    expect(fit.cmax).toBeLessThanOrEqual(3 + 1e-9);
  });
});

/**
 * Desenhos em curva já aplicados em tracks.ts. Um desenho novo em ART (a rodada em andamento) só entra aqui no
 * `--apply` da pista dele (a onda O aplica os outros 26 de uma vez).
 */
const APPLIED_ART = ['copacabana'];

describe('fidelidade ao desenho: o minimapa segue o traço de ART', () => {
  for (const id of APPLIED_ART) {
    it(`${id}: erro médio ≤ 1,0, máximo ≤ 2,5 e fechamento ≤ 3 (em 100) contra o traço normalizado`, () => {
      expect(ART[id], `${id} sem desenho em scripts/track-art.ts`).toBeDefined();
      const def = trackDef(id);
      const n = buildTrack(def).segments.length;
      const art = ART[id]();
      // A normalização do relatório (`report`): o mesmo ponto de largada e a mesma escala que `fitArt` usa.
      const { q, perim } = resampleLoop(art.pts, art.start, n);
      const r = report(def, { poly: art.pts, start: q[0], scale: n / perim });
      expect(r.fit?.mean ?? Infinity, 'erro médio').toBeLessThanOrEqual(1.0);
      expect(r.fit?.max ?? Infinity, 'erro máximo').toBeLessThanOrEqual(2.5);
      expect(r.drift, 'desvio de fechamento').toBeLessThanOrEqual(3);
    });
  }
});

describe('placas de curva: só em curva forte de verdade', () => {
  // A quina curta (desenho de polígono) jogava a IA para fora bem em cima das 3 placas (tests/batidas-cenario.test.ts).
  it('curva forte longa ganha as 3 placas; quina curta (menos de 12 segmentos acima de 3) não ganha nenhuma', () => {
    const signsIn = (ops: TrackDef['ops']) => buildTrack({ ...trackDef('copacabana'), id: 'teste-placas', ops }).segments
      .flatMap((s) => s.sprites).filter((sp) => sp.kind === 'sign_left' || sp.kind === 'sign_right').length;
    expect(signsIn([{ op: 'pit', length: 40 }, { op: 'straight', length: 200 }, { op: 'curve', length: 40, curve: 5 }, { op: 'straight', length: 200 }])).toBe(3);
    expect(signsIn([{ op: 'pit', length: 40 }, { op: 'straight', length: 200 }, { op: 'curve', length: 8, curve: 5 }, { op: 'straight', length: 200 }])).toBe(0);
  });
});
