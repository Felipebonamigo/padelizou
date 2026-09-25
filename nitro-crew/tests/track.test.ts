import { describe, it, expect } from 'vitest';
import { SEGMENT_LENGTH } from '../src/core/constants';
import { carDef } from '../src/core/data/cars';
import { holdableSpeedFraction } from '../src/core/sim/physics';
import { SPRITE_HALF_WIDTH } from '../src/core/sim/collisions';
import { CUPS } from '../src/core/data/cups';
import { buildTrack, maxCurveAhead, segmentAt } from '../src/core/track/builder';
import { TRACKS, trackDef } from '../src/core/track/tracks';
import { getTrack } from '../src/core/track';
import type { TrackDef } from '../src/core/types';
import { setLanguage, t } from '../src/i18n';
import '../src/i18n/core';

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

  // ~60 mil conferências nas 32 pistas: 2,2 s com a máquina carregada, perto demais dos 5 s padrão.
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
    const t = buildTrack(trackDef('sampa_noite'));
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
    expect(maxCurveAhead(t, t.length - 100, 50)).toBe(Math.max(...t.segments.slice(0, 50).map((s) => Math.abs(s.curve)), Math.abs(t.segments[t.segments.length - 1].curve)));
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

describe('catálogo: 32 pistas em 8 copas de 4, como o original', () => {
  const cupTracks = (cupId: string) => (CUPS.find((c) => c.id === cupId)?.trackIds ?? []).map((id) => trackDef(id));
  const mean = (xs: number[]) => xs.reduce((a, b) => a + b, 0) / xs.length;

  it('8 copas de 4 pistas; toda pista está em exatamente uma copa', () => {
    expect(CUPS).toHaveLength(8);
    expect(TRACKS).toHaveLength(32);
    const seen = new Map<string, string>();
    for (const c of CUPS) {
      expect(c.trackIds, c.id).toHaveLength(4);
      for (const id of c.trackIds) {
        expect(seen.get(id), `${id} está em ${seen.get(id)} e em ${c.id}`).toBeUndefined();
        seen.set(id, c.id);
      }
    }
    for (const t of TRACKS) expect(seen.has(t.id), `${t.id} não está em copa nenhuma`).toBe(true);
  });

  it('ids em ASCII minúsculo (a copa vira a conquista COPA_<ID>), nomes únicos, país da pista = país da copa', () => {
    for (const c of CUPS) expect(c.id, c.id).toMatch(/^[a-z][a-z0-9_]*$/);
    for (const t of TRACKS) expect(t.id, t.id).toMatch(/^[a-z][a-z0-9_]*$/);
    expect(new Set(TRACKS.map((t) => t.name)).size).toBe(TRACKS.length);
    expect(new Set(CUPS.map((c) => c.country)).size).toBe(CUPS.length);
    for (const c of CUPS) for (const t of cupTracks(c.id)) expect(t.country, `${t.id} em ${c.id}`).toBe(c.country);
  });

  it('destravamento linear na ordem da lista: brasil → eua → japao → europa → as quatro novas', () => {
    expect(CUPS.slice(0, 4).map((c) => c.id)).toEqual(['brasil', 'eua', 'japao', 'europa']);
    CUPS.forEach((c, i) => expect(c.requires, c.id).toBe(i === 0 ? null : CUPS[i - 1].id));
  });

  it('voltas de 3 a 5, dificuldade inteira de 1 a 5', () => {
    for (const t of TRACKS) {
      expect(t.laps, t.id).toBeGreaterThanOrEqual(3); expect(t.laps, t.id).toBeLessThanOrEqual(5);
      expect(Number.isInteger(t.difficulty) && t.difficulty >= 1 && t.difficulty <= 5, `${t.id}: ${t.difficulty}`).toBe(true);
    }
  });

  it('dificuldade cresce dentro da copa e a média cresce de uma copa para a seguinte', () => {
    let prev = 0;
    for (const c of CUPS) {
      const d = cupTracks(c.id).map((t) => t.difficulty);
      for (let i = 1; i < d.length; i++) expect(d[i], `${c.id}: ${d.join(',')}`).toBeGreaterThanOrEqual(d[i - 1]);
      expect(mean(d), `${c.id}: média ${mean(d)} depois de ${prev}`).toBeGreaterThan(prev);
      prev = mean(d);
    }
  });

  it('medida no traçado, a copa seguinte também exige mais que a anterior (média do índice técnico)', () => {
    let prev = -1;
    for (const c of CUPS) {
      const m = mean(cupTracks(c.id).map(technicalIndex));
      expect(m, `${c.id}: índice médio ${m.toFixed(1)} depois de ${prev.toFixed(1)}`).toBeGreaterThan(prev);
      prev = m;
    }
  });

  it('copa e país têm nome em PT e em EN', () => {
    for (const lang of ['pt', 'en'] as const) {
      setLanguage(lang);
      for (const c of CUPS) {
        expect(t(`core.cup.${c.id}`), `${lang} core.cup.${c.id}`).not.toBe(`core.cup.${c.id}`);
        expect(t(`core.country.${c.country}`), `${lang} core.country.${c.country}`).not.toBe(`core.country.${c.country}`);
      }
    }
    setLanguage('pt');
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
  });
});
