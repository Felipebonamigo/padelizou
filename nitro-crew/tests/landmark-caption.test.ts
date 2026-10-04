// Legenda dos marcos turísticos (docs/VISUAL.md, "Legenda dos marcos"): o dono correu em Foz do Iguaçu e não achou as
// cataratas. Quando um marco entra bem à vista da câmera de um jogador, o HUD dele mostra o nome do marco e onde ele
// fica ("Cataratas do Iguaçu" / "Foz do Iguaçu · PR") por ~3 s — uma vez por marco por volta, uma legenda de cada vez.
// A visibilidade é a mesma conta do enquadramento (scenery/sight.ts), com a câmera daquele jogador (posição, lateral,
// FOV e proporção do viewport) e um tamanho mínimo na tela.
import { describe, expect, it } from 'vitest';
import { SEGMENT_LENGTH } from '../src/core/constants';
import { ALL_PLACES, placeOf } from '../src/core/data/places';
import { getTrack, TRACKS } from '../src/core/track';
import type { Segment, Track } from '../src/core/types';
import { setLanguage } from '../src/i18n';
import '../src/i18n/core';
import {
  CAPTION_FOG_SCALE, CAPTION_GAP, CAPTION_MIN_SECONDS, CAPTION_MIN_SHARE, CAPTION_SECONDS, CaptionDirector, captionScene, captionSpots, pickCaption, referenceView,
  spotShare, type CaptionScene, type CaptionSpot,
} from '../src/render/caption/caption';
import { captionName, captionPlace } from '../src/render/caption/names';
import { landmarkSightRoad, sceneryLayout } from '../src/render/scenery/layout';
import { SCENERY_BEHIND_DRAW, SIGHT_DT, SIGHT_SPEED_MS, sightRoad, sightSeconds, type BlockHeight } from '../src/render/scenery/sight';
import { SEGMENT_M } from '../src/render/units';

/** Pista sintética: `n` segmentos retos e planos, savana de dia (névoa: 504 m; a do horizonte, 916 m). */
function fakeTrack(n: number): Track {
  const segments: Segment[] = [];
  for (let i = 0; i < n; i++) segments.push({ index: i, z: i * 200, curve: 0, y0: 0, y1: 0, band: 0, pit: false, sprites: [] });
  return { def: { id: 'reta', scenery: 'savanna', timeOfDay: 'day' }, segments, length: n * 200, startIndex: 0 } as unknown as Track;
}
const nothing: BlockHeight = () => 0;
const box = (w: number, h: number, d: number) => ({ minX: -w / 2, maxX: w / 2, minZ: -d / 2, maxZ: d / 2, maxY: h });
/** Marco de teste no meio do segmento `seg`, a `x` m do centro, de frente para a pista. */
function spot(id: string, seg: number, x: number, b: CaptionSpot['bounds'], haze = false): CaptionSpot {
  return { id, seg, f: 0.5, x, y: 0, yaw: x < 0 ? -0.3 : Math.PI + 0.3, bounds: b, haze };
}
/** A câmera de referência (a do sight.ts: 0,8 da máxima, 16:9, na linha central) com o carro em `seg` (segmentos). */
const at = (seg: number) => referenceView(seg * SEGMENT_LENGTH);

const N = 1000;
const straight = fakeTrack(N);
const open = sightRoad(straight, nothing, 0, () => false);

describe('legenda dos marcos: o marco está bem à vista?', () => {
  const scene = captionScene(straight, [spot('igreja', 500, -60, box(20, 20, 20))], open);

  it('só dentro da névoa, à frente e no quadro; cresce quando o carro chega perto', () => {
    expect(spotShare(scene, 0, at(500 - 200))).toBe(0); // 800 m: além da névoa (504 m)
    const longe = spotShare(scene, 0, at(500 - 100)); // ~400 m
    const perto = spotShare(scene, 0, at(500 - 30)); // ~120 m
    expect(longe).toBeGreaterThan(0);
    expect(longe).toBeLessThan(0.06);
    expect(perto).toBeGreaterThan(2.5 * longe);
    expect(spotShare(scene, 0, at(500 - 3))).toBe(0); // ao lado: já saiu dos 80% do meio do quadro
    expect(spotShare(scene, 0, at(510))).toBe(0); // passou
  });

  it('o mesmo marco do outro lado da linha de chegada (a volta dá a volta)', () => {
    const wrap = captionScene(straight, [spot('igreja', 20, -60, box(20, 20, 20))], open);
    expect(spotShare(wrap, 0, at(N - 10))).toBeGreaterThan(0);
    expect(spotShare(wrap, 0, at(N - 10))).toBeCloseTo(spotShare(scene, 0, at(470)), 6);
  });

  it('prédio alto na beira, entre o carro e o marco, esconde (a grade de alturas do layout)', () => {
    const wall = sightRoad(straight, (_seg, side, lat) => (side < 0 && lat >= 10 && lat <= 36 ? 30 : 0), 36, () => true);
    const both = [spot('igreja', 500, -60, box(20, 20, 20)), spot('capela', 500, 60, box(20, 20, 20))];
    const walled = captionScene(straight, both, wall);
    expect(spotShare(walled, 0, at(470))).toBe(0);
    expect(spotShare(walled, 1, at(470))).toBeCloseTo(spotShare(captionScene(straight, both, open), 1, at(470)), 6);
  });

  it('a câmera é a do jogador: o viewport largo (2 jogadores, 32:9) vê mais de lado; o carro na beira muda o ângulo', () => {
    // 16:9: o marco a 60 m de lado sai dos 80% do meio a ~58 m da câmera; 32:9, a ~29 m.
    const d = 500 - 9;
    expect(spotShare(scene, 0, at(d))).toBe(0);
    expect(spotShare(scene, 0, { ...at(d), aspect: 32 / 9 })).toBeGreaterThan(0);
    // O carro na beira da direita (x = +1: 7 m) vê o marco da esquerda mais de lado: ele sai do quadro antes.
    let edge = 0;
    for (let seg = 470; seg < 500 && !edge; seg += 0.25) if (spotShare(scene, 0, at(seg)) > 0 && spotShare(scene, 0, { ...at(seg), x: 1 }) === 0) edge = seg;
    expect(edge).toBeGreaterThan(0);
    expect(spotShare(scene, 0, { ...at(edge), x: -1 })).toBeGreaterThan(0);
  });

  it('o tamanho é o do pedaço no quadro: a ponte comprida passando do lado conta pelo comprimento, não pelas pontas', () => {
    // Ponte de 720 m a 220 m de lado (a de Palmas): com o carro a 160 m do meio dela, a ponta de trás já ficou para trás
    // e a da frente está a ~520 m; o vão no quadro vai da beirada da tela até um terço dela. Contando só os cantos à
    // frente da câmera, sobravam as duas pontas de longe: 4% da tela (Palmas: 6–7% com o vão inteiro passando do lado).
    const bridge = captionScene(straight, [spot('ponte', 600, 220, { minX: -30, maxX: 30, minZ: -360, maxZ: 360, maxY: 24 })], open);
    expect(spotShare(bridge, 0, at(600 - 40))).toBeGreaterThan(0.25);
    // E uma caixa inteira no quadro mede o mesmo que antes: a altura dela na tela.
    const tanV = Math.tan((referenceView(0).fov * Math.PI) / 360);
    const near = spotShare(scene, 0, at(500 - 30));
    expect(near).toBeGreaterThan(20 / ((30 * 4 + 7.8 + 12) * 2 * tanV));
    expect(near).toBeLessThan(20 / ((30 * 4 + 7.8 - 12) * 2 * tanV));
  });

  it('o do horizonte (névoa × 0,55) é visto de mais longe', () => {
    const big = box(300, 300, 300);
    const sky = captionScene(straight, [spot('morro', 600, -450, big, true), spot('morro', 600, -450, big, false)], open);
    expect(spotShare(sky, 0, at(600 - 170))).toBeGreaterThan(0);
    expect(spotShare(sky, 1, at(600 - 170))).toBe(0);
  });

  it('com a câmera de referência, o tempo em que o marco conta como à vista é o do sight.ts (a mesma conta)', () => {
    const step = (SIGHT_SPEED_MS * SIGHT_DT) / SEGMENT_M;
    const swept = (sc: CaptionScene, i: number): number => {
      const sp = sc.spots[i];
      let seen = 0;
      for (let s = sp.seg - sc.road.ahead; s < sp.seg + SCENERY_BEHIND_DRAW + 1; s += step) if (spotShare(sc, i, at(s)) > 0) seen += SIGHT_DT;
      return seen;
    };
    const synthetic = captionScene(straight, [spot('a', 500, -60, box(20, 20, 20)), spot('b', 500, 150, box(40, 40, 40)), spot('c', 600, -450, box(300, 300, 300), true)], open);
    // As pistas de verdade: o do horizonte (o Cristo), os de longe (as cataratas) e os da cidade (praças entre prédios).
    const real = ['copacabana', 'foz_do_iguacu', 'sampa_noite'].map((id) => {
      const track = getTrack(id);
      return captionScene(track, captionSpots(sceneryLayout(track)), landmarkSightRoad(track));
    });
    for (const sc of [synthetic, ...real]) {
      expect(sc.spots.length).toBeGreaterThan(0);
      sc.spots.forEach((sp, i) => {
        const ref = sightSeconds(sc.road, sp.seg + sc.n, sp.f, sp.x, sp.y, sp.yaw, sp.bounds, sp.haze);
        expect(Math.abs(swept(sc, i) - ref), `${sc.trackId} ${sp.id}#${sp.seg}: ${swept(sc, i)} × ${ref}`).toBeLessThanOrEqual(2 * SIGHT_DT + 1e-9);
      });
    }
  }, 120000);
});

describe('legenda dos marcos: qual anunciar agora', () => {
  // Dois marcos que aparecem juntos: um pequeno à esquerda, um grande à direita, no mesmo trecho.
  const pair = captionScene(straight, [spot('igreja', 500, -60, box(20, 20, 20)), spot('ponte', 505, 160, box(60, 50, 300))], open);
  const igreja = 0; const ponte = 1;

  it('o que sai do quadro primeiro vem primeiro (o de perto antes do grande de longe); o já anunciado fica de fora até a volta seguinte', () => {
    // A ponte, comprida e a 160 m de lado, é a maior na tela e fica nela até o carro chegar a ~150 m dela; a igreja, a
    // 60 m de lado, sai do quadro a ~58 m: ela primeiro, senão só teria a vez já passando (Rochosas: a cabana de troncos
    // perdia a vez para a ponte de treliça e ficava sem legenda).
    const view = at(470);
    expect(spotShare(pair, igreja, view, CAPTION_FOG_SCALE)).toBeGreaterThan(CAPTION_MIN_SHARE);
    expect(spotShare(pair, ponte, view, CAPTION_FOG_SCALE)).toBeGreaterThan(spotShare(pair, igreja, view, CAPTION_FOG_SCALE));
    const announced = new Int32Array(pair.ids.length);
    expect(pickCaption(pair, view, announced, 1)).toBe(igreja);
    announced[pair.spotId[igreja]] = 1;
    expect(pickCaption(pair, view, announced, 1)).toBe(ponte);
    announced[pair.spotId[ponte]] = 1;
    expect(pickCaption(pair, view, announced, 1)).toBe(-1);
    expect(pickCaption(pair, view, announced, 2)).toBe(igreja);
  });

  it('a névoa da legenda é mais curta que a do sight.ts: o marco na névoa de 35–60% conta como à vista, mas não é anunciado', () => {
    const solo = captionScene(straight, [spot('morro', 600, -100, box(200, 120, 200))], open);
    // ~420 m: dentro dos 504 m (60%) do sight.ts, fora dos ~345 m (35%) da legenda.
    const view = at(600 - 103);
    expect(spotShare(solo, 0, view)).toBeGreaterThan(CAPTION_MIN_SHARE);
    expect(spotShare(solo, 0, view, CAPTION_FOG_SCALE)).toBe(0);
    expect(pickCaption(solo, view, new Int32Array(1), 1)).toBe(-1);
    expect(pickCaption(solo, at(600 - 70), new Int32Array(1), 1)).toBe(0);
  });

  it('pequeno demais na tela não anuncia, mesmo nítido; perto o bastante, sim', () => {
    // Uma placa de 4 m a 30 m de lado: a ~190 m está na névoa da legenda, mas é um risco na tela.
    const sign = captionScene(straight, [spot('placa', 500, -30, box(3, 4, 1))], open);
    const far = at(500 - 45);
    expect(spotShare(sign, 0, far, CAPTION_FOG_SCALE)).toBeGreaterThan(0);
    expect(spotShare(sign, 0, far, CAPTION_FOG_SCALE)).toBeLessThan(CAPTION_MIN_SHARE);
    expect(pickCaption(sign, far, new Int32Array(1), 1)).toBe(-1);
    let picked = 0;
    for (let seg = 455; seg < 500 && !picked; seg += 0.5) if (pickCaption(sign, at(seg), new Int32Array(1), 1) === 0) picked = seg;
    // Anunciada ainda no quadro (ela sai dos 80% do meio a ~29 m da câmera, que fica 7,8 m atrás do carro).
    expect(picked).toBeGreaterThan(0);
    expect((500.5 - picked) * 4 + 7.8).toBeGreaterThan(29);
  });

  it('duas instâncias do mesmo marco na volta: anuncia a primeira que aparecer, e a segunda não repete', () => {
    const twice = captionScene(straight, [spot('jangada', 300, -50, box(12, 10, 12)), spot('jangada', 800, -50, box(12, 10, 12))], open);
    expect(twice.ids).toEqual(['jangada']);
    const announced = new Int32Array(twice.ids.length);
    expect(pickCaption(twice, at(285), announced, 1)).toBe(0);
    announced[twice.spotId[0]] = 1;
    expect(pickCaption(twice, at(785), announced, 1)).toBe(-1);
    expect(pickCaption(twice, at(785), announced, 2)).toBe(1);
  });
});

/** Carro andando pela pista a 96 m/s (a velocidade da conta), quadro a quadro: o que a legenda mostrou e quando. */
function drive(scene: CaptionScene, fromSeg: number, segs: number, opts: { lap?: (seg: number) => number; enabled?: (t: number) => boolean } = {}) {
  const dir = new CaptionDirector();
  const shows: Array<{ id: string; from: number; to: number; seg: number }> = [];
  const dt = 1 / 60;
  const v = SIGHT_SPEED_MS / SEGMENT_M; // segmentos por segundo
  let open: (typeof shows)[number] | null = null;
  for (let t = 0; t * v <= segs; t += dt) {
    const seg = fromSeg + t * v;
    dir.update(scene, at(seg), opts.lap ? opts.lap(seg) : 1, t, opts.enabled ? opts.enabled(t) : true);
    const id = dir.showing ? dir.landmark : null;
    if (open && id !== open.id) { open.to = t; open = null; }
    if (id && !open) { open = { id, from: t, to: Infinity, seg }; shows.push(open); }
  }
  return shows;
}

describe('legenda dos marcos: uma de cada vez, ~3 s, uma vez por volta', () => {
  it('sozinha, dura CAPTION_SECONDS', () => {
    const one = captionScene(straight, [spot('igreja', 500, -60, box(20, 20, 20))], open);
    const shows = drive(one, 300, 300);
    expect(shows).toHaveLength(1);
    expect(shows[0].to - shows[0].from).toBeCloseTo(CAPTION_SECONDS, 1);
  });

  it('dois juntos, um de cada vez: com o outro esperando, a primeira encurta para CAPTION_MIN_SECONDS, e o intervalo', () => {
    // A ponte (comprida) entra grande na tela já na névoa; a igreja passa do mínimo enquanto a legenda da ponte está
    // na tela. Com 3 s cheios para a ponte, a igreja só teria a vez já saindo do quadro: o segundo marco de muitas
    // pistas (64 segmentos depois do primeiro, layout.ts) ficava sem legenda.
    const pair = captionScene(straight, [spot('igreja', 500, -60, box(40, 40, 40)), spot('ponte', 505, 160, box(60, 50, 300))], open);
    const igreja = 0; const ponte = 1;
    const shows = drive(pair, 300, 300);
    expect(shows.map((s) => s.id)).toEqual(['ponte', 'igreja']);
    expect(shows[0].to - shows[0].from).toBeGreaterThanOrEqual(CAPTION_MIN_SECONDS - 1e-9);
    expect(shows[0].to - shows[0].from).toBeLessThan(CAPTION_SECONDS - 0.3);
    expect(shows[1].from - shows[0].to).toBeGreaterThanOrEqual(CAPTION_GAP - 1e-9);
    // Quando a ponte saiu, a igreja já esperava (à vista, grande o bastante); quando entrou, ainda estava à vista.
    const announced = new Int32Array(pair.ids.length);
    announced[pair.spotId[ponte]] = 1;
    const segAt = (t: number) => 300 + t * (SIGHT_SPEED_MS / SEGMENT_M);
    expect(pickCaption(pair, at(segAt(shows[0].to - 1 / 60)), announced, 1)).toBe(igreja);
    expect(spotShare(pair, igreja, at(shows[1].seg))).toBeGreaterThan(0);
    expect(shows[1].to - shows[1].from).toBeCloseTo(CAPTION_SECONDS, 1);
  });

  it('uma vez por marco por volta: a volta seguinte anuncia de novo', () => {
    const one = captionScene(straight, [spot('igreja', 500, -60, box(20, 20, 20))], open);
    // Duas passagens pelo mesmo marco (o carro anda 2 voltas da pista sintética): a 2ª volta começa no segmento N.
    const shows = drive(one, 300, 2 * N, { lap: (seg) => 1 + Math.floor(seg / N) });
    expect(shows.map((s) => s.id)).toEqual(['igreja', 'igreja']);
    expect(shows[1].seg - shows[0].seg).toBeCloseTo(N, -1);
    // Na mesma volta, sem repetir (uma volta só, o marco passa uma vez e a legenda não volta).
    expect(drive(one, 300, 600)).toHaveLength(1);
  });

  it('desligada (pausa, contagem, opção, fim da corrida): nada aparece, e a que estava some', () => {
    const one = captionScene(straight, [spot('igreja', 500, -60, box(20, 20, 20))], open);
    expect(drive(one, 300, 300, { enabled: () => false })).toEqual([]);
    // Desliga 1 s depois de a legenda entrar: ela sai na hora, e não volta (o marco já foi anunciado nesta volta).
    const stop = drive(one, 300, 300)[0].from + 1;
    const cut = drive(one, 300, 300, { enabled: (t) => t < stop });
    expect(cut).toHaveLength(1);
    expect(cut[0].to).toBeLessThanOrEqual(stop + 1 / 60 + 1e-9);
  });

  it('corrida nova (o relógio volta para trás) ou pista nova zera o que já foi anunciado', () => {
    const one = captionScene(straight, [spot('igreja', 500, -60, box(20, 20, 20))], open);
    const dir = new CaptionDirector();
    let t = 0;
    const run = (sc: CaptionScene) => { let shown = false; for (let seg = 300; seg < 620; seg += 0.4) { t += 1 / 60; dir.update(sc, at(seg), 1, t, true); shown ||= dir.showing; } return shown; };
    expect(run(one)).toBe(true);
    expect(run(one)).toBe(false); // mesma volta: já anunciado
    t = 0; // corrida nova: os ticks recomeçam do zero
    expect(run(one)).toBe(true);
    const other = captionScene(straight, [spot('igreja', 500, -60, box(20, 20, 20))], open);
    expect(run(other)).toBe(true); // outra cena (outra pista)
  });
});

describe('legenda dos marcos: as pistas de verdade', () => {
  it('na volta 1, com a câmera de referência, todo marco de toda pista é anunciado', () => {
    const missed: string[] = [];
    let total = 0;
    for (const def of TRACKS) {
      const ids = placeOf(def.id)?.landmarks ?? [];
      if (!ids.length) continue;
      const track = getTrack(def.id);
      const scene = captionScene(track, captionSpots(sceneryLayout(track)), landmarkSightRoad(track));
      const n = track.segments.length;
      // Da linha de chegada até a linha de novo (a volta 1); o que já se vê da largada conta.
      const shown = new Set(drive(scene, track.startIndex, n).map((s) => s.id));
      for (const id of ids) { total++; if (!shown.has(id)) missed.push(`${def.id} ${id}`); }
    }
    expect(total).toBeGreaterThanOrEqual(161);
    expect(missed, `${missed.length} marcos sem legenda na volta 1`).toEqual([]);
  }, 240000);

  it('o layout entrega a pista da conta com a grade de alturas dele: a legenda não refaz os sprites na largada', () => {
    const track = getTrack('sampa_noite');
    const layout = sceneryLayout(track, undefined, true);
    const own = layout.sight;
    expect(own).toBeDefined();
    if (!own) return;
    const a = captionScene(track, captionSpots(layout), own);
    const b = captionScene(track, captionSpots(layout), landmarkSightRoad(track));
    expect(Array.from(a.lastSeen)).toEqual(Array.from(b.lastSeen));
    for (let seg = 0; seg < a.n; seg += 7) a.spots.forEach((_, i) => expect(spotShare(a, i, at(seg))).toBe(spotShare(b, i, at(seg))));
    // Sem pedir (os testes de layout comparam o JSON dele), ou na pista sem marco: sem a conta.
    expect(sceneryLayout(track).sight).toBeUndefined();
    expect(sceneryLayout(track, [], true).sight).toBeUndefined();
  }, 60000);

  it('Foz do Iguaçu: as cataratas são anunciadas (e as instâncias delas vêm do layout)', () => {
    const track = getTrack('foz_do_iguacu');
    const scene = captionScene(track, captionSpots(sceneryLayout(track)), landmarkSightRoad(track));
    expect(scene.ids).toContain('cataratas_iguacu');
    const shows = drive(scene, track.startIndex, track.segments.length);
    expect(shows.find((s) => s.id === 'cataratas_iguacu')).toBeDefined();
  }, 60000);
});

describe('legenda dos marcos: os textos', () => {
  it('todo marco tem nome em PT e EN, e toda pista com marco diz onde fica', () => {
    const ids = new Set(Object.values(ALL_PLACES).flatMap((p) => p.landmarks));
    for (const lang of ['pt', 'en'] as const) {
      setLanguage(lang);
      for (const id of ids) {
        const name = captionName(id);
        expect(name, `${lang} ${id}`).not.toMatch(/landmark\.|^$/);
      }
      for (const trackId of Object.keys(ALL_PLACES)) expect(captionPlace(trackId), `${lang} ${trackId}`).toMatch(/^[^.]+ · [^.]+$/);
    }
    setLanguage('pt');
  });

  it('o nome do passaporte para os marcos do Brasil; cidade · UF no Brasil, lugar · país no mundo', () => {
    setLanguage('pt');
    expect(captionName('cataratas_iguacu')).toBe('Cataratas do Iguaçu');
    expect(captionPlace('foz_do_iguacu')).toBe('Foz do Iguaçu · PR');
    expect(captionName('cristo_redentor')).toBe('Cristo Redentor');
    expect(captionPlace('copacabana')).toBe('Rio de Janeiro · RJ');
    expect(captionName('torre_eiffel')).toBe('Torre Eiffel');
    expect(captionPlace('paris')).toBe('Paris · França');
    setLanguage('en');
    expect(captionName('cataratas_iguacu')).toBe('Iguaçu Falls');
    expect(captionPlace('foz_do_iguacu')).toBe('Foz do Iguaçu · PR');
    expect(captionName('torre_eiffel')).toBe('Eiffel Tower');
    expect(captionPlace('paris')).toBe('Paris · France');
    setLanguage('pt');
  });
});
