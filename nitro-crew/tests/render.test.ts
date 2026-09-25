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

// ───────────────────────────── Referencial local da pista ─────────────────────────────
import { absoluteHeading, buildRoadFrame, frameYAt, locateOnFrame, type RoadFrame } from '../src/render/roadframe';
import { HEADING_PER_CURVE, ROAD_HALF_WIDTH_M, SEGMENT_M, xToMeters, yToMeters, Y_SCALE, zToMeters } from '../src/render/units';
import { SEGMENT_LENGTH } from '../src/core/constants';

describe('units', () => {
  it('converte unidades de mundo em metros', () => {
    expect(xToMeters(1)).toBe(ROAD_HALF_WIDTH_M);
    expect(zToMeters(SEGMENT_LENGTH)).toBe(SEGMENT_M);
    expect(yToMeters(1000)).toBeCloseTo(1000 * Y_SCALE, 9);
  });
});

describe('buildRoadFrame', () => {
  it('reta: px = 0 e pz cai 4 m por ponto, origem no baseZ', () => {
    const track = syntheticTrack();
    const f = buildRoadFrame(track, 20 * SEGMENT_LENGTH, 5, 10);
    expect(f.count).toBe(16);
    expect(f.baseIndex).toBe(20);
    for (let j = 0; j < f.count; j++) {
      expect(f.px[j]).toBeCloseTo(0, 6);
      expect(f.py[j]).toBeCloseTo(0, 6);
      expect(f.heading[j]).toBeCloseTo(0, 9);
      expect(f.pz[j]).toBeCloseTo(-(j - 5) * SEGMENT_M, 5);
      expect(f.segIndex[j]).toBe(15 + j);
    }
    // No meio do segmento a origem continua no carro: o ponto do segmento base fica 2 m atrás.
    const g = buildRoadFrame(track, 20 * SEGMENT_LENGTH + SEGMENT_LENGTH / 2, 5, 10);
    expect(g.pz[5]).toBeCloseTo(SEGMENT_M / 2, 5);
    expect(g.pz[6]).toBeCloseTo(-SEGMENT_M / 2, 5);
  });

  it('curva constante: rumo cresce linearmente e px é monotônico à frente', () => {
    const track = syntheticTrack([{ op: 'curve', length: 400, curve: 4 }]);
    // Trecho de sustentação: segmentos 100..299 têm curve = 4.
    const f = buildRoadFrame(track, 150 * SEGMENT_LENGTH, 10, 40);
    const step = 4 * HEADING_PER_CURVE;
    for (let j = 10; j < f.count; j++) {
      expect(f.heading[j]).toBeCloseTo((j - 10) * step, 6); // float32
      if (j > 10) expect(f.px[j]).toBeGreaterThan(f.px[j - 1]);
    }
    expect(f.px[10]).toBeCloseTo(0, 6);
    expect(f.heading[0]).toBeCloseTo(-10 * step, 6);
    // Curva à direita: o traçado entorta para +x, também atrás.
    expect(f.px[0]).toBeGreaterThan(0);
  });

  it('elevação acompanha y0/y1 × Y_SCALE', () => {
    const track = syntheticTrack([{ op: 'hill', length: 100, height: 20 }, { op: 'straight', length: 100 }]);
    const b = 10;
    const f = buildRoadFrame(track, b * SEGMENT_LENGTH, 3, 20);
    const seg = track.segments[b];
    const originY = seg.y0 * Y_SCALE;
    expect(f.py[3]).toBeCloseTo(0, 9);
    for (let j = 0; j < f.count; j++) {
      const s = track.segments[f.segIndex[j]];
      expect(f.py[j]).toBeCloseTo(s.y0 * Y_SCALE - originY, 6);
    }
    expect(f.py[4]).toBeCloseTo((seg.y1 - seg.y0) * Y_SCALE, 6);
    expect(f.py[4]).toBeGreaterThan(0);
  });

  it('frameYAt mede a partir da origem (o carro), não do início do segmento base', () => {
    const track = syntheticTrack([{ op: 'hill', length: 100, height: 20 }, { op: 'straight', length: 100 }]);
    // Meio do segmento 10, numa rampa: a altura na origem é zero por definição.
    const f = buildRoadFrame(track, 10 * SEGMENT_LENGTH + SEGMENT_LENGTH * 0.5, 5, 20);
    expect(frameYAt(f, 0)).toBeCloseTo(0, 5);
    // Um segmento à frente da origem = metade do segmento 10 + metade do 11.
    const s10 = track.segments[10]; const s11 = track.segments[11];
    const originY = (s10.y0 + (s10.y1 - s10.y0) * 0.5) * Y_SCALE;
    const expected = (s11.y0 + (s11.y1 - s11.y0) * 0.5) * Y_SCALE - originY;
    expect(frameYAt(f, SEGMENT_M)).toBeCloseTo(expected, 5);
    expect(frameYAt(f, -SEGMENT_M)).toBeCloseTo((s10.y0 - (s10.y0 - track.segments[9].y0) * 0.5) * Y_SCALE - originY, 5);
  });

  it('reutiliza a janela passada em `out` sem alocar', () => {
    const track = syntheticTrack();
    const a = buildRoadFrame(track, 0, 5, 10);
    const px = a.px;
    const b = buildRoadFrame(track, 3 * SEGMENT_LENGTH, 5, 10, a);
    expect(b).toBe(a);
    expect(b.px).toBe(px);
    expect(b.baseIndex).toBe(3);
  });
});

describe('locateOnFrame', () => {
  const out = { x: 0, y: 0, z: 0, heading: 0 };

  it('na origem devolve (0,0,0) e o x lateral vira metros', () => {
    const track = syntheticTrack([{ op: 'curve', length: 400, curve: 4 }]);
    const z = 150 * SEGMENT_LENGTH + 77;
    const f = buildRoadFrame(track, z, 10, 40);
    expect(locateOnFrame(f, track, z, 0, out)).toBe(true);
    expect(out.x).toBeCloseTo(0, 5); expect(out.y).toBeCloseTo(0, 5); expect(out.z).toBeCloseTo(0, 5); // float32
    expect(out.heading).toBeCloseTo(0, 6);
    expect(locateOnFrame(f, track, z, 1, out)).toBe(true);
    expect(out.x).toBeCloseTo(ROAD_HALF_WIDTH_M, 6);
    expect(out.z).toBeCloseTo(0, 6);
  });

  it('carro à frente da linha de chegada (com volta) e fora da janela', () => {
    const track = syntheticTrack();
    const n = track.segments.length;
    const f = buildRoadFrame(track, (n - 2) * SEGMENT_LENGTH, 5, 20);
    expect(locateOnFrame(f, track, SEGMENT_LENGTH, 0, out)).toBe(true);
    expect(out.z).toBeCloseTo(-3 * SEGMENT_M, 6);
    // Meio do segmento: interpola.
    expect(locateOnFrame(f, track, SEGMENT_LENGTH * 1.5, 0, out)).toBe(true);
    expect(out.z).toBeCloseTo(-3.5 * SEGMENT_M, 6);
    // Atrás demais / à frente demais.
    expect(locateOnFrame(f, track, (n - 10) * SEGMENT_LENGTH, 0, out)).toBe(false);
    expect(locateOnFrame(f, track, 30 * SEGMENT_LENGTH, 0, out)).toBe(false);
  });
});

describe('absoluteHeading', () => {
  it('reta é zero; curva acumula curve × HEADING_PER_CURVE por segmento', () => {
    expect(absoluteHeading(syntheticTrack(), 12345)).toBe(0);
    const track = syntheticTrack([{ op: 'curve', length: 400, curve: 4 }]);
    let sum = 0;
    for (let i = 0; i < 200; i++) sum += track.segments[i].curve * HEADING_PER_CURVE;
    expect(absoluteHeading(track, 200 * SEGMENT_LENGTH)).toBeCloseTo(sum, 9);
    // Meio do segmento 200: metade do giro dele.
    expect(absoluteHeading(track, 200.5 * SEGMENT_LENGTH)).toBeCloseTo(sum + 0.5 * track.segments[200].curve * HEADING_PER_CURVE, 9);
    const frame: RoadFrame = buildRoadFrame(track, 0, 1, 1);
    expect(frame.count).toBe(3);
  });
});
