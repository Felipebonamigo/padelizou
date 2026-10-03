// Praças da cidade (docs/VISUAL.md, "Marcos turísticos"): nas pistas city_night o núcleo enche os dois lados de
// prédios e torres na beira da pista (a receita da cidade), e o MASP, a Ópera de Arame, a Torre de TV… ficavam
// atrás desse paredão — invisíveis da pista (visto nas capturas da onda G). O núcleo abre praças (trechos de um
// lado sem prédio, torre e outdoor) e o layout põe cada marco perto e longe numa delas: quem chega vê o marco.
import { describe, expect, it } from 'vitest';
import { placeOf } from '../src/core/data/places';
import { getTrack, TRACKS } from '../src/core/track';
import { cityPlazas, LANDMARK_PLAZAS } from '../src/core/track/plazas';
import { LANDMARK_PREFIX } from '../src/render/scenery/catalog';
import { LANDMARKS } from '../src/render/scenery/landmarks';
import { sceneryLayout } from '../src/render/scenery/layout';

/** Os ~120 m (30 segmentos) antes do marco, com folga da meia pegada de um prédio (3 segmentos) e do próprio marco. */
const APPROACH = 40;
const AFTER = 3;
const BLOCKERS = new Set(['building', 'tower', 'billboard']);

const cityTracks = TRACKS.filter((t) => t.scenery === 'city_night' && (placeOf(t.id)?.landmarks.length ?? 0) > 0);

describe('praças da cidade: os marcos à vista de quem chega', () => {
  it('há pistas de cidade com marcos para conferir', () => {
    expect(cityTracks.length).toBeGreaterThanOrEqual(15);
  });

  it('nos ~120 m antes de cada marco perto/longe, do lado dele, nenhum prédio, torre ou outdoor do núcleo na beira da pista', () => {
    const bad: string[] = [];
    for (const def of cityTracks) {
      const track = getTrack(def.id);
      const n = track.segments.length;
      const layout = sceneryLayout(track);
      layout.bySeg.forEach((list, seg) => {
        for (const p of list) {
          const model = layout.models[p.model];
          if (!model.startsWith(LANDMARK_PREFIX)) continue;
          const id = model.slice(LANDMARK_PREFIX.length);
          if (LANDMARKS[id].place === 'skyline') continue;
          const side = p.x < 0 ? -1 : 1;
          const hits: string[] = [];
          for (let d = -APPROACH; d <= AFTER; d++) {
            const j = ((seg + d) % n + n) % n;
            for (const sp of track.segments[j].sprites) if (BLOCKERS.has(sp.kind) && sp.x * side > 0) hits.push(`${sp.kind}@${j}`);
          }
          if (hits.length) bad.push(`${def.id} ${id}#${seg}: ${hits.slice(0, 4).join(' ')}${hits.length > 4 ? ` +${hits.length - 4}` : ''}`);
        }
      });
    }
    expect(bad).toEqual([]);
  }, 120000);

  it('o núcleo sabe quantas praças cada marco da cidade pede (= perLap do registro; o do horizonte, nenhuma)', () => {
    const bad: string[] = [];
    for (const def of cityTracks) {
      for (const id of placeOf(def.id)?.landmarks ?? []) {
        const lm = LANDMARKS[id];
        if (!lm) continue;
        const want = lm.place === 'skyline' ? 0 : lm.perLap;
        if (LANDMARK_PLAZAS[id] !== want) bad.push(`${def.id} ${id}: ${LANDMARK_PLAZAS[id]} praças, o registro pede ${want}`);
      }
    }
    expect(bad).toEqual([]);
  });

  it('só a cidade com marcos tem praça; elas não se sobrepõem nem pegam a largada', () => {
    for (const def of TRACKS) {
      const track = getTrack(def.id);
      const plazas = cityPlazas(track);
      if (def.scenery !== 'city_night' || !placeOf(def.id)?.landmarks.length) { expect(plazas, def.id).toEqual([]); continue; }
      const n = track.segments.length;
      expect(plazas.length, def.id).toBeGreaterThan(0);
      const used: string[] = new Array(n).fill('');
      for (const a of plazas) {
        // Fora das arquibancadas e do trecho sem prédio da largada (30 segmentos).
        expect(((a.from - track.startIndex) % n + n) % n, `${def.id} largada`).toBeGreaterThanOrEqual(26);
        for (let i = a.from; i <= a.to; i++) {
          const j = ((i % n) + n) % n;
          expect(used[j], `${def.id} ${a.landmark}/${a.rep} no segmento ${j}`).toBe('');
          used[j] = `${a.landmark}/${a.rep}`;
        }
      }
    }
  });
});
