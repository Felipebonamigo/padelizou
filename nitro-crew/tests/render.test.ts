import { describe, it, expect } from 'vitest';
import { TRACKS } from '../src/core/track/tracks';
import { getTrack } from '../src/core/track';
import { spareCell, viewportRects } from '../src/render/layout';
import { outlinePoint, trackOutline } from '../src/render/minimap';
import { hexToRgb, mix, palette, SCENERIES, shade, TIMES_OF_DAY } from '../src/render/palette';
import { syntheticTrack } from './helpers';

const HEX = /^#[0-9a-f]{6}$/;

describe('viewportRects', () => {
  function area(rects: Array<{ w: number; h: number }>): number { return rects.reduce((a, r) => a + r.w * r.h, 0); }
  function overlaps(a: { x: number; y: number; w: number; h: number }, b: { x: number; y: number; w: number; h: number }): boolean {
    return a.x < b.x + b.w && b.x < a.x + a.w && a.y < b.y + b.h && b.y < a.y + a.h;
  }

  it('1 → tela cheia', () => {
    expect(viewportRects(1, 1920, 1080)).toEqual([{ x: 0, y: 0, w: 1920, h: 1080 }]);
  });

  it('2 → em cima/embaixo com largura total', () => {
    const r = viewportRects(2, 1920, 1080);
    expect(r).toHaveLength(2);
    expect(r[0].w).toBe(1920); expect(r[1].w).toBe(1920);
    expect(r[0].y).toBe(0); expect(r[1].y).toBe(r[0].h);
    expect(r[0].h + r[1].h).toBe(1080);
  });

  it('3 e 4 → grade 2×2; com 3 sobra a célula inferior direita', () => {
    const r4 = viewportRects(4, 1920, 1080);
    expect(r4).toHaveLength(4);
    expect(new Set(r4.map((r) => `${r.x},${r.y}`)).size).toBe(4);
    for (const r of r4) { expect(r.w).toBe(960); expect(r.h).toBe(540); }
    const r3 = viewportRects(3, 1920, 1080);
    expect(r3).toEqual(r4.slice(0, 3));
    expect(spareCell(3, 1920, 1080)).toEqual(r4[3]);
    expect(spareCell(4, 1920, 1080)).toBeNull();
    expect(spareCell(2, 1920, 1080)).toBeNull();
  });

  it('cobre a tela sem sobreposição em 1/2/3/4 (com tamanhos ímpares também)', () => {
    for (const [w, h] of [[1920, 1080], [1366, 768], [1001, 601], [640, 361]] as const) {
      for (const count of [1, 2, 3, 4]) {
        const rects = viewportRects(count, w, h);
        expect(rects).toHaveLength(count);
        for (const r of rects) {
          expect(r.x).toBeGreaterThanOrEqual(0); expect(r.y).toBeGreaterThanOrEqual(0);
          expect(r.x + r.w).toBeLessThanOrEqual(w); expect(r.y + r.h).toBeLessThanOrEqual(h);
          expect(Number.isInteger(r.x) && Number.isInteger(r.y) && Number.isInteger(r.w) && Number.isInteger(r.h)).toBe(true);
        }
        for (let i = 0; i < rects.length; i++) for (let j = i + 1; j < rects.length; j++) expect(overlaps(rects[i], rects[j]), `${count} em ${w}×${h}: ${i} e ${j}`).toBe(false);
        const spare = spareCell(count, w, h);
        expect(area(spare ? [...rects, spare] : rects)).toBe(w * h);
      }
    }
  });

  it('valores fora de 1..4 são presos ao intervalo', () => {
    expect(viewportRects(0, 100, 100)).toHaveLength(1);
    expect(viewportRects(9, 100, 100)).toHaveLength(4);
  });
});

describe('trackOutline', () => {
  it('cabe na caixa, tem tamanho razoável e é determinístico, para todas as pistas', () => {
    for (const def of TRACKS) {
      const track = getTrack(def.id);
      const size = 120;
      const a = trackOutline(track, size);
      const b = trackOutline(track, size);
      expect(a.length, def.id).toBeGreaterThanOrEqual(300);
      expect(a.length, def.id).toBeLessThanOrEqual(800);
      expect(JSON.stringify(a)).toBe(JSON.stringify(b));
      let minX = Infinity; let maxX = -Infinity; let minY = Infinity; let maxY = -Infinity;
      for (const [x, y] of a) {
        expect(x, def.id).toBeGreaterThanOrEqual(0); expect(x, def.id).toBeLessThanOrEqual(size);
        expect(y, def.id).toBeGreaterThanOrEqual(0); expect(y, def.id).toBeLessThanOrEqual(size);
        expect(Number.isFinite(x) && Number.isFinite(y), def.id).toBe(true);
        minX = Math.min(minX, x); maxX = Math.max(maxX, x); minY = Math.min(minY, y); maxY = Math.max(maxY, y);
      }
      // Usa a caixa: o maior lado ocupa a área útil (size menos a margem de 8% de cada lado).
      expect(Math.max(maxX - minX, maxY - minY), def.id).toBeCloseTo(size * 0.84, 6);
      expect(minX, def.id).toBeGreaterThanOrEqual(size * 0.08 - 1e-6);
      expect(minY, def.id).toBeGreaterThanOrEqual(size * 0.08 - 1e-6);
    }
  });

  it('o traçado fecha: o último ponto volta perto do primeiro', () => {
    for (const def of TRACKS) {
      const track = getTrack(def.id);
      const size = 200;
      const pts = trackOutline(track, size);
      const [x0, y0] = pts[0];
      const [x1, y1] = pts[pts.length - 1];
      // O último ponto está um passo antes do fechamento; a distância é da ordem de um passo.
      const stepPx = (size * 0.84) / (pts.length / 4);
      expect(Math.hypot(x1 - x0, y1 - y0), def.id).toBeLessThan(stepPx * 2);
    }
  });

  it('escala com o tamanho pedido e aceita pista sem curva', () => {
    const track = getTrack('copacabana');
    const small = trackOutline(track, 50);
    const big = trackOutline(track, 500);
    expect(small.length).toBe(big.length);
    for (let i = 0; i < small.length; i++) {
      expect(big[i][0]).toBeCloseTo(small[i][0] * 10, 6);
      expect(big[i][1]).toBeCloseTo(small[i][1] * 10, 6);
    }
    const straight = trackOutline(syntheticTrack(), 100);
    expect(straight.length).toBeGreaterThan(10);
    for (const [x, y] of straight) { expect(x).toBeGreaterThanOrEqual(0); expect(x).toBeLessThanOrEqual(100); expect(y).toBeGreaterThanOrEqual(0); expect(y).toBeLessThanOrEqual(100); }
    expect(trackOutline(track, 0)).toEqual([]);
  });

  it('outlinePoint dá a volta na pista e interpola', () => {
    const track = getTrack('rota_66');
    const pts = trackOutline(track, 100);
    const out: [number, number] = [0, 0];
    outlinePoint(pts, track, 0, out);
    expect(out).toEqual(pts[0]);
    outlinePoint(pts, track, track.length, out);
    expect(out[0]).toBeCloseTo(pts[0][0], 6);
    outlinePoint(pts, track, -1, out);
    expect(Number.isFinite(out[0]) && Number.isFinite(out[1])).toBe(true);
  });
});

describe('palette', () => {
  it('toda combinação cenário × período existe, com cores hex válidas', () => {
    expect(SCENERIES).toHaveLength(6);
    expect(TIMES_OF_DAY).toEqual(['day', 'dusk', 'night']);
    for (const sc of SCENERIES) for (const tod of TIMES_OF_DAY) {
      const p = palette(sc, tod);
      for (const c of [...p.sky, p.grassLight, p.grassDark, p.rumbleLight, p.rumbleDark, p.roadLight, p.roadDark, p.lane, p.fog, p.far, p.near, p.pit, p.pitLine]) {
        expect(c, `${sc}/${tod}`).toMatch(HEX);
      }
      expect(p.light).toBeGreaterThan(0); expect(p.light).toBeLessThanOrEqual(1);
      expect(p.grassLight).not.toBe(p.grassDark);
      expect(p.roadLight).not.toBe(p.roadDark);
      expect(palette(sc, tod)).toBe(p); // cache
    }
  });

  it('noite tem lua e estrelas e é mais escura que o dia; dia tem sol', () => {
    for (const sc of SCENERIES) {
      const day = palette(sc, 'day'); const night = palette(sc, 'night'); const dusk = palette(sc, 'dusk');
      expect(day.sun).not.toBeNull(); expect(day.moon).toBeNull(); expect(day.stars).toBe(false);
      expect(night.sun).toBeNull(); expect(night.moon).not.toBeNull(); expect(night.stars).toBe(true);
      expect(dusk.sun).not.toBeNull();
      const lum = (hex: string) => hexToRgb(hex).reduce((a, b) => a + b, 0);
      expect(lum(night.grassLight), sc).toBeLessThan(lum(day.grassLight));
      expect(lum(night.roadLight), sc).toBeLessThan(lum(day.roadLight));
      expect(night.light).toBeLessThan(dusk.light); expect(dusk.light).toBeLessThan(day.light);
    }
  });

  it('utilitários de cor', () => {
    expect(hexToRgb('#ff8000')).toEqual([255, 128, 0]);
    expect(hexToRgb('#fff')).toEqual([255, 255, 255]);
    expect(mix('#000000', '#ffffff', 0.5)).toBe('#808080');
    expect(shade('#808080', 2)).toBe('#ffffff');
    expect(shade('#808080', 0.5)).toBe('#404040');
  });
});
