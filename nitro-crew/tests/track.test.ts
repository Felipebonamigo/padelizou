import { describe, it, expect } from 'vitest';
import { SEGMENT_LENGTH } from '../src/core/constants';
import { CUPS } from '../src/core/data/cups';
import { buildTrack, maxCurveAhead, segmentAt } from '../src/core/track/builder';
import { TRACKS, trackDef } from '../src/core/track/tracks';
import { getTrack } from '../src/core/track';

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

  it('a elevação fecha: o fim da pista volta à altura do começo, e os segmentos são contíguos', () => {
    for (const def of TRACKS) {
      const t = buildTrack(def);
      const first = t.segments[0]; const last = t.segments[t.segments.length - 1];
      expect(Math.abs(last.y1 - first.y0), def.id).toBeLessThan(1e-6);
      for (let i = 1; i < t.segments.length; i++) expect(t.segments[i].y0).toBeCloseTo(t.segments[i - 1].y1, 6);
    }
  });

  it('toda pista tem um trecho de box de pelo menos 20 segmentos na reta de largada', () => {
    for (const def of TRACKS) {
      const t = buildTrack(def);
      const pit = t.segments.filter((s) => s.pit);
      expect(pit.length, def.id).toBeGreaterThanOrEqual(20);
      expect(Math.abs(pit[0].curve), def.id).toBeLessThan(1e-6);
    }
  });

  it('nenhum sprite sólido fica em cima do asfalto', () => {
    for (const def of TRACKS) {
      const t = buildTrack(def);
      for (const s of t.segments) for (const sp of s.sprites) {
        if (sp.solid) expect(Math.abs(sp.x), `${def.id} seg ${s.index} ${sp.kind}`).toBeGreaterThanOrEqual(1.3);
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
