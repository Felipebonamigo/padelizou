// Pista, céu e luz (onda F): as partes puras do chão e da atmosfera — paleta por bioma × período
// (contraste, névoa com cor, luz), relevo que não mexe onde o cenário está, planos do horizonte
// que fecham a volta e o decalque do grid alinhado com o grid de verdade.
import { describe, expect, it } from 'vitest';
import * as THREE from 'three';
import { getTrack } from '../src/core/track';
import { createRace } from '../src/core/sim/race';
import { TRACKS } from '../src/core/track/tracks';
import { luminance, palette, SCENERIES, TIMES_OF_DAY } from '../src/render/palette';
import { gridMarks, PIT_LANE_X, PIT_X0, PIT_X1, START_BEHIND } from '../src/render/road-textures';
import { hasKerb, SHOULDER_M } from '../src/render/road';
import { sunSetup } from '../src/render/sky';
import { COLS, relief, ringProfile } from '../src/render/terrain';
import { ROAD_HALF_WIDTH_M, SEGMENT_M } from '../src/render/units';

const HEX = /^#[0-9a-f]{6}$/;

function saturation(hex: string): number {
  const hsl = { h: 0, s: 0, l: 0 };
  new THREE.Color(hex).getHSL(hsl);
  return hsl.s;
}

/** As combinações que as pistas do jogo usam de fato (track/tracks.ts). */
const USED = [...new Set(TRACKS.map((t) => `${t.scenery}:${t.timeOfDay}`))];

describe('paleta do chão, céu e luz', () => {
  it('toda combinação tem os campos novos válidos', () => {
    for (const sc of SCENERIES) for (const tod of TIMES_OF_DAY) {
      const p = palette(sc, tod);
      const colors = [p.skyGlow, p.sunLight, p.hemiSky, p.hemiGround, p.cloudShade, p.asphalt, p.shoulder, p.verge, p.groundLow, p.groundHigh,
        p.rock, p.snow, p.sand, ...p.layers, p.waterDeep, p.waterShallow, p.foam];
      for (const c of colors) expect(c, `${sc}/${tod}`).toMatch(HEX);
      if (p.cloudLit !== null) expect(p.cloudLit).toMatch(HEX);
      if (p.lampPool !== null) expect(p.lampPool).toMatch(HEX);
      expect(p.exposure).toBeGreaterThan(0.8); expect(p.exposure).toBeLessThan(1.3);
      for (const k of p.layerHaze) { expect(k).toBeGreaterThanOrEqual(0); expect(k).toBeLessThanOrEqual(1); }
      expect(p.layerHaze[0]).toBeLessThan(p.layerHaze[2]); // o plano de longe está mais dentro da névoa
    }
  });

  it('névoa com cor (nunca cinza morto) em todas as combinações', () => {
    for (const sc of SCENERIES) for (const tod of TIMES_OF_DAY) {
      expect(saturation(palette(sc, tod).fog), `${sc}/${tod}`).toBeGreaterThan(0.18);
    }
  });

  it('céu em gradiente forte: o topo é bem mais escuro que o horizonte', () => {
    for (const key of USED) {
      const [sc, tod] = key.split(':') as [typeof SCENERIES[number], typeof TIMES_OF_DAY[number]];
      const p = palette(sc, tod);
      expect(luminance(p.sky[2]) - luminance(p.sky[0]), key).toBeGreaterThan(0.2);
    }
  });

  it('a borda da pista se lê: asfalto × faixa de transição e zebra vermelho × branco com contraste', () => {
    for (const sc of SCENERIES) for (const tod of TIMES_OF_DAY) {
      const p = palette(sc, tod);
      const key = `${sc}/${tod}`;
      expect(Math.abs(luminance(p.verge) - luminance(p.asphalt)), key).toBeGreaterThan(0.12);
      expect(luminance(p.rumbleLight) - luminance(p.rumbleDark), key).toBeGreaterThan(0.35);
      expect(luminance(p.lane) - luminance(p.asphalt), key).toBeGreaterThan(0.45);
      expect(luminance(p.pitLine) - luminance(p.pit), key).toBeGreaterThan(0.3);
    }
  });

  it('noite: marcações com brilho, poças de luz dos postes, sem nuvens; de dia nada disso', () => {
    for (const sc of SCENERIES) {
      const n = palette(sc, 'night'); const d = palette(sc, 'day');
      expect(n.markingGlow).toBeGreaterThan(0); expect(d.markingGlow).toBe(0);
      expect(n.lampPool).not.toBeNull(); expect(d.lampPool).toBeNull();
      expect(n.cloudLit).toBeNull(); expect(d.cloudLit).not.toBeNull();
      expect(n.sunIntensity).toBeLessThan(d.sunIntensity);
    }
  });

  it('bloom de dia só acima do branco iluminado (limiar ≥ 1); mais forte à noite', () => {
    for (const sc of SCENERIES) {
      const d = palette(sc, 'day'); const n = palette(sc, 'night');
      expect(d.bloom[2]).toBeGreaterThanOrEqual(1);
      expect(n.bloom[0]).toBeGreaterThan(d.bloom[0]);
      expect(n.bloom[2]).toBeLessThan(d.bloom[2]);
    }
  });
});

describe('terreno', () => {
  it('o terreno começa onde o acostamento termina (sem fresta entre pista e chão)', () => {
    expect(COLS[0]).toBeCloseTo(ROAD_HALF_WIDTH_M + SHOULDER_M, 6);
    for (let c = 1; c < COLS.length; c++) expect(COLS[c]).toBeGreaterThan(COLS[c - 1]);
  });

  it('relevo zero até 26 m da pista (o cenário mais afastado, ~37 m de centro, fica no chão plano ou acima)', () => {
    // Um expect só no fim: milhares de expects por valor estouravam o tempo com a máquina carregada.
    const bad: string[] = [];
    for (const sc of SCENERIES) for (let seg = 0; seg < 2000; seg += 17) for (const side of [-1, 1]) {
      for (const dist of COLS) {
        const h = relief(sc, seg, dist, side);
        if ((dist <= 26 && h !== 0) || !Number.isFinite(h) || h < 0 || h >= 80) bad.push(`${sc} seg ${seg} ${dist} m: ${h}`);
      }
    }
    expect(bad).toEqual([]);
  });

  it('relevo é função só do segmento (o mesmo morro sempre no mesmo lugar)', () => {
    expect(relief('alpine', 123, 180, 1)).toBe(relief('alpine', 123, 180, 1));
    expect(relief('alpine', 123, 180, 1)).not.toBe(relief('alpine', 124, 180, 1));
  });

  it('planos do horizonte fecham a volta sem emenda e ficam no intervalo', () => {
    const bad: string[] = [];
    for (const sc of SCENERIES) for (let l = 0; l < 3; l++) {
      if (Math.abs(ringProfile(sc, l, 0) - ringProfile(sc, l, 1)) > 1e-9) bad.push(`${sc} ${l}: emenda`);
      for (let i = 0; i < 360; i++) {
        const h = ringProfile(sc, l, i / 360);
        if (!(h >= 0 && h <= 1.2)) bad.push(`${sc} ${l} ${i}: ${h}`);
      }
    }
    expect(bad).toEqual([]);
  });
});

describe('pista: zebra, box e decalque da largada', () => {
  it('zebra só em curva média ou forte', () => {
    const seg = (curve: number) => ({ index: 0, z: 0, curve, y0: 0, y1: 0, band: 0 as const, pit: false, sprites: [] });
    expect(hasKerb(seg(0))).toBe(false); expect(hasKerb(seg(1.5))).toBe(false);
    expect(hasKerb(seg(2))).toBe(true); expect(hasKerb(seg(-6))).toBe(true);
  });

  it('as marcas do grid pintadas batem com o grid que o createRace monta', () => {
    const track = getTrack('copacabana');
    const humans = [{ seat: 0, name: 'P1', carId: 'falcao', teamId: 0, color: '#ff0000' }];
    const state = createRace({ trackId: 'copacabana', laps: 2, humans, totalCars: 20, difficulty: 'profissional', manualGear: false,
      assists: { sharedNitro: true, tow: true, teamDraft: true, catchup: true }, seed: 7 }, track);
    const real = state.cars.map((c) => `${(((track.length - c.z) * SEGMENT_M) / 200).toFixed(3)}:${c.x}`).sort();
    const painted = gridMarks(state.cars.length).map((g) => `${g.behindM.toFixed(3)}:${g.x}`).sort();
    expect(painted).toEqual(real);
    // O decalque cobre o grid inteiro de 20 carros (o último com a traseira a ~2,2 m do centro).
    const last = Math.max(...gridMarks(20).map((g) => g.behindM));
    expect(START_BEHIND * SEGMENT_M).toBeGreaterThan(last + 2.2);
  });

  it('a faixa do box pintada contém a faixa do box da física', () => {
    expect(PIT_X0).toBeLessThan(PIT_LANE_X[0]);
    expect(PIT_X1).toBeGreaterThan(PIT_LANE_X[1]);
  });
});

describe('sol e lua', () => {
  it('entardecer com o sol baixo (na tela); dia alto o bastante para sombra curta; lua acima do horizonte', () => {
    const elev = (v: THREE.Vector3) => Math.asin(v.y) * 180 / Math.PI;
    expect(elev(sunSetup('dusk').dir)).toBeLessThan(15);
    expect(elev(sunSetup('dusk').dir)).toBeGreaterThan(3);
    expect(elev(sunSetup('day').dir)).toBeGreaterThan(25);
    expect(elev(sunSetup('night').dir)).toBeGreaterThan(8);
    expect(sunSetup('night').moon).toBe(true);
    expect(sunSetup('dusk').size).toBeGreaterThan(sunSetup('day').size); // sol grande no entardecer
  });
});
