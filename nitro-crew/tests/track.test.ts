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
