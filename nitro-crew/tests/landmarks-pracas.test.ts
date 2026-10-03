// Praças da cidade e mirantes do litoral (docs/VISUAL.md, "Marcos turísticos"): nas pistas city_night o núcleo enche
// os dois lados de prédios e torres na beira da pista (a receita da cidade), e o MASP, a Ópera de Arame, a Torre de
// TV… ficavam atrás desse paredão — invisíveis da pista (visto nas capturas da onda G). No litoral a receita põe prédio
// e torre dos dois lados também (menos denso), e a jangada de Maceió, o coqueiral de Maragogi, o Ver-o-Peso, o cassino
// de Mônaco… só apareciam nas brechas (onda H, "Na tela"). O núcleo abre praças e mirantes (trechos sem prédio, torre e
// outdoor onde a linha de visada de quem chega cruza a beira da pista) e o layout põe cada marco perto e longe num
// deles: quem chega vê o marco. Quanto tempo ele fica na tela é tests/landmarks-enquadramento.test.ts.
import { describe, expect, it } from 'vitest';
import { placeOf } from '../src/core/data/places';
import { getTrack, TRACKS } from '../src/core/track';
import { LANDMARK_PLAZAS, landmarkPlazas, PLAZA_FRONT, PLAZA_LAT } from '../src/core/track/plazas';
import { centerline, SIGHT_MODEL } from '../src/core/track/sightline';
import { START_STANDS, startZoneEnd } from '../src/core/track/startzone';
import type { Segment, Track } from '../src/core/types';
import { LANDMARK_PREFIX } from '../src/render/scenery/catalog';
import { LANDMARKS } from '../src/render/scenery/landmarks';
import { sceneryLayout } from '../src/render/scenery/layout';
import { LANDMARK_SIGHT_MIN, SIGHT_SPEED_MS, SIGHT_TAN_H, sightRoad, unrollTrack } from '../src/render/scenery/sight';
import { HEADING_PER_CURVE, SEGMENT_M } from '../src/render/units';

/**
 * A frente de cada praça, do lado do marco, sem prédio (a folga de ±4 segmentos da busca do layout descontada): o de
 * perto, os ~120 m (30 segmentos) em que ele cresce no quadro mais meia fachada; o de longe, a vizinhança — a visada
 * até ele cruza a beira perto do carro, e isso é o tempo na tela que confere.
 */
const APPROACH = { near: PLAZA_FRONT.near - 4, far: PLAZA_FRONT.far - 4 } as const;
const AFTER = 3;
const BLOCKERS = new Set(['building', 'tower', 'billboard']);

/** Biomas em que a receita do núcleo (track/builder.ts) põe prédio e torre na beira da pista: praças e mirantes. */
const ROADSIDE = new Set(['city_night', 'coast']);
const plazaTracks = TRACKS.filter((t) => ROADSIDE.has(t.scenery) && (placeOf(t.id)?.landmarks.length ?? 0) > 0);

describe('praças e mirantes: os marcos à vista de quem chega', () => {
  it('há pistas de cidade e de litoral com marcos para conferir', () => {
    expect(plazaTracks.filter((t) => t.scenery === 'city_night').length).toBeGreaterThanOrEqual(15);
    expect(plazaTracks.filter((t) => t.scenery === 'coast').length).toBeGreaterThanOrEqual(25);
  });

  it('na frente de cada marco perto/longe, do lado dele, nenhum prédio, torre ou outdoor do núcleo na beira da pista', () => {
    expect(APPROACH.near, 'os ~120 m de quem chega ao de perto').toBeGreaterThanOrEqual(40);
    const bad: string[] = [];
    for (const def of plazaTracks) {
      const track = getTrack(def.id);
      const n = track.segments.length;
      const layout = sceneryLayout(track);
      layout.bySeg.forEach((list, seg) => {
        for (const p of list) {
          const model = layout.models[p.model];
          if (!model.startsWith(LANDMARK_PREFIX)) continue;
          const id = model.slice(LANDMARK_PREFIX.length);
          const place = LANDMARKS[id].place;
          if (place === 'skyline') continue;
          const side = p.x < 0 ? -1 : 1;
          const hits: string[] = [];
          // O primeiro de cada marco: a largada (arquibancadas, box) não é das praças.
          const from = Math.max(-APPROACH[place], -(((seg - track.startIndex) % n + n) % n - START_STANDS));
          for (let d = from; d <= AFTER; d++) {
            const j = ((seg + d) % n + n) % n;
            for (const sp of track.segments[j].sprites) if (BLOCKERS.has(sp.kind) && sp.x * side > 0) hits.push(`${sp.kind}@${j}`);
          }
          if (hits.length) bad.push(`${def.id} ${id}#${seg} (${place}): ${hits.slice(0, 4).join(' ')}${hits.length > 4 ? ` +${hits.length - 4}` : ''}`);
        }
      });
    }
    expect(bad).toEqual([]);
  }, 180000);

  it('todo marco perto/longe da cidade e do litoral fica na praça dele (o layout não precisou procurar outro lugar)', () => {
    const bad: string[] = [];
    for (const def of plazaTracks) {
      const track = getTrack(def.id);
      const n = track.segments.length;
      const plazas = landmarkPlazas(track);
      const layout = sceneryLayout(track);
      layout.bySeg.forEach((list, seg) => {
        for (const p of list) {
          const model = layout.models[p.model];
          if (!model.startsWith(LANDMARK_PREFIX)) continue;
          const id = model.slice(LANDMARK_PREFIX.length);
          if (LANDMARKS[id].place === 'skyline') continue;
          const side = p.x < 0 ? -1 : 1;
          if (!plazas.some((q) => q.landmark === id && q.side === side && Math.abs(((seg - q.at + n + n / 2) % n) - n / 2) <= 4)) bad.push(`${def.id} ${id}#${seg}`);
        }
      });
    }
    expect(bad).toEqual([]);
  }, 180000);

  it('o núcleo sabe quantas praças cada marco pede, de perto ou de longe e de que lado (= o registro; o do horizonte, nenhuma)', () => {
    const bad: string[] = [];
    for (const def of plazaTracks) {
      for (const id of placeOf(def.id)?.landmarks ?? []) {
        const lm = LANDMARKS[id];
        if (!lm) continue;
        const want = lm.place === 'skyline' ? { count: 0, place: 'skyline', side: 'any' } : { count: lm.perLap, place: lm.place, side: lm.side };
        const got = LANDMARK_PLAZAS[id];
        if (JSON.stringify(got) !== JSON.stringify(want)) bad.push(`${def.id} ${id}: ${JSON.stringify(got)}, o registro pede ${JSON.stringify(want)}`);
      }
    }
    expect(bad).toEqual([]);
  });

  it('só a cidade e o litoral com marcos têm praça; a frente de uma não pega a de outra do mesmo lado, nem a largada', () => {
    for (const def of TRACKS) {
      const track = getTrack(def.id);
      const plazas = landmarkPlazas(track);
      const wants = ROADSIDE.has(def.scenery) && (placeOf(def.id)?.landmarks ?? []).some((id) => LANDMARKS[id] && LANDMARKS[id].place !== 'skyline');
      if (!wants) { expect(plazas, def.id).toEqual([]); continue; }
      const n = track.segments.length;
      expect(plazas.length, def.id).toBeGreaterThan(0);
      const used = [new Array<string>(n).fill(''), new Array<string>(n).fill('')];
      for (const a of plazas) {
        // Nada aberto nas arquibancadas (até o segmento 24 da largada): nem do lado do marco, nem do outro.
        for (const from of [a.from, ...(a.across ? [a.across.from] : [])]) expect(((from - track.startIndex) % n + n) % n, `${def.id} ${a.landmark}/${a.rep} na largada`).toBeGreaterThan(START_STANDS + 1);
        expect(a.to - a.from, `${def.id} ${a.landmark}/${a.rep}`).toBeGreaterThanOrEqual(PLAZA_FRONT[a.place]);
        for (let i = a.at - PLAZA_FRONT[a.place]; i <= a.at + 12; i++) {
          const j = ((i % n) + n) % n;
          const u = used[a.side < 0 ? 0 : 1];
          expect(u[j], `${def.id} ${a.landmark}/${a.rep} no segmento ${j}`).toBe('');
          u[j] = `${a.landmark}/${a.rep}`;
        }
        // O marco (±4 segmentos do lugar da praça) nunca no box; a praça pode passar por ele (do lado dele o builder não põe nada).
        for (let d = -4; d <= 4; d++) expect(track.segments[((a.at + d) % n + n) % n].pit, `${def.id} ${a.landmark}/${a.rep}: marco no box`).toBe(false);
        // No litoral o lado é o do registro: o mar à direita, o resto em terra.
        if (def.scenery === 'coast') expect(a.side, `${def.id} ${a.landmark}`).toBe(LANDMARKS[a.landmark].side === 'sea' ? 1 : -1);
      }
    }
  });
});

describe('a visada do núcleo (core/track/sightline.ts) é a conta do renderizador (render/scenery/sight.ts)', () => {
  const fake = (timeOfDay: 'day' | 'dusk' | 'night'): Track => {
    const segments: Segment[] = [];
    for (let i = 0; i < 400; i++) segments.push({ index: i, z: i * 200, curve: 0, y0: 0, y1: 0, band: 0, pit: false, sprites: [] });
    return { def: { id: 'reta', scenery: 'city_night', timeOfDay }, segments, length: 400 * 200, startIndex: 0 } as unknown as Track;
  };

  it('as mesmas unidades, o quadro no máximo o que conta, a névoa aquém da do período e o mínimo da tela com 10% de folga', () => {
    expect(SIGHT_MODEL.segmentM).toBe(SEGMENT_M);
    expect(SIGHT_MODEL.headingPerCurve).toBe(HEADING_PER_CURVE);
    expect(SIGHT_MODEL.tanH).toBeLessThanOrEqual(SIGHT_TAN_H);
    expect(SIGHT_MODEL.tanH).toBeGreaterThan(SIGHT_TAN_H - 0.01);
    for (const time of ['day', 'dusk', 'night'] as const) {
      const fogM = sightRoad(fake(time), () => 0, 0, () => false).fogM;
      expect(SIGHT_MODEL.depthM[time], time).toBeLessThanOrEqual(fogM);
      expect(SIGHT_MODEL.depthM[time], time).toBeGreaterThan(fogM - 15);
    }
    expect(SIGHT_MODEL.needM).toBeCloseTo(1.1 * LANDMARK_SIGHT_MIN.far * SIGHT_SPEED_MS, 0);
    // A lateral de referência do de longe: numa reta de dia ele sai do quadro e fica o mínimo antes disso dentro da
    // névoa (à noite, a mais fechada, só com a pista fazendo curva para o outro lado: ver o enquadramento).
    expect(PLAZA_LAT.far / SIGHT_MODEL.tanH + SIGHT_MODEL.needM).toBeLessThan(SIGHT_MODEL.depthM.day);
  });

  it('a linha central sem trigonometria é a do renderizador (≤ 1 cm em duas voltas de pistas de verdade)', () => {
    for (const id of ['monaco_noite', 'roma', 'sampa_noite', 'maceio']) {
      const track = getTrack(id);
      const a = centerline(track); const b = unrollTrack(track);
      let worst = 0;
      for (let k = 0; k < a.px.length; k++) {
        worst = Math.max(worst, Math.abs(a.px[k] - b.px[k]), Math.abs(a.pz[k] - b.pz[k]));
        worst = Math.max(worst, Math.abs(a.hs[k] - Math.sin(b.hd[k])) * 100, Math.abs(a.hc[k] - Math.cos(b.hd[k])) * 100);
      }
      expect(worst, id).toBeLessThan(0.01);
    }
  });
});

describe('fim da largada', () => {
  it('arquibancadas, o trecho sem cenário e o box que começa nela: segmento 40 em todas as pistas de hoje (box 0–39)', () => {
    for (const def of TRACKS) expect(startZoneEnd(getTrack(def.id)), def.id).toBe(40);
  });

  it('sem box na largada, é o trecho sem cenário (30 segmentos)', () => {
    const segments: Segment[] = [];
    for (let i = 0; i < 400; i++) segments.push({ index: i, z: i * 200, curve: 0, y0: 0, y1: 0, band: 0, pit: i >= 200 && i < 240, sprites: [] });
    expect(startZoneEnd({ def: { id: 'x' }, segments, length: 400 * 200, startIndex: 0 } as unknown as Track)).toBe(30);
  });
});
