// Marcos turísticos do mundo (onda G, src/render/scenery/landmarks/mundo.ts): todo id do contrato existe,
// monta, cabe no orçamento de triângulos do seu alcance, pousa no chão e tem um tamanho plausível.
import * as THREE from 'three';
import { describe, expect, it } from 'vitest';
import { TRACK_PLACES } from '../src/core/data/places';
import { FOUNDATION_M, LANDMARKS_MUNDO } from '../src/render/scenery/landmarks/mundo';
import type { Model } from '../src/render/scenery/geom';

const WORLD_IDS = [
  'placa_rota_66', 'diner_neon', 'log_lodge', 'ponte_trelica', 'represa_hoover', 'placa_las_vegas', 'piramide_luxor',
  'torre_stratosphere', 'rainbow_bridge', 'torre_toquio', 'portao_shurei', 'shisa', 'monte_fuji_pico', 'pagode_chureito',
  'castelo_osaka', 'tsutenkaku', 'castelo_neuschwanstein', 'torre_eiffel', 'arco_triunfo', 'matterhorn', 'capela_alpina',
  'cassino_monte_carlo', 'portao_kruger', 'girafa', 'igreja_karoo', 'anfiteatro_drakensberg', 'farol_cape_point',
  'table_mountain', 'uluru', 'arco_great_ocean', 'passarela_daintree', 'opera_sydney', 'harbour_bridge',
  'ponte_storseisundet', 'vila_lapponia', 'placa_trolls', 'cachoeira_stigfossen', 'catedral_artica', 'positano',
  'cupula_azul', 'moinho_santorini', 'vulcao_etna', 'coliseu', 'cupula_sao_pedro',
];

const BUDGET = { near: 3500, far: 5000, skyline: 2500 } as const;
/** Caixa máxima (m): [largura em X, altura, comprimento em Z] por alcance (sem o facho de luz, que é só luz). */
const MAX_BOX = { near: [70, 50, 80], far: [260, 360, 520], skyline: [720, 420, 960] } as const;

function triangles(m: Model): number {
  return m.parts.reduce((t, p) => t + p.geometry.attributes.position.count / 3, 0);
}

function bounds(m: Model): THREE.Box3 {
  const b = new THREE.Box3();
  for (const p of m.parts) { if (p.mat === 'cone') continue; p.geometry.computeBoundingBox(); b.union(p.geometry.boundingBox as THREE.Box3); }
  return b;
}

const built = new Map<string, Model>();
const model = (id: string): Model => {
  let m = built.get(id);
  if (!m) { m = LANDMARKS_MUNDO[id].build(); built.set(id, m); }
  return m;
};

describe('marcos do mundo', () => {
  it('cobre exatamente os ids do mundo no contrato (places.ts)', () => {
    expect(Object.keys(LANDMARKS_MUNDO).sort()).toEqual([...WORLD_IDS].sort());
    const used = new Set(Object.values(TRACK_PLACES).filter((p) => !p.state).flatMap((p) => p.landmarks));
    expect([...used].sort()).toEqual([...WORLD_IDS].sort());
  });

  it.each(WORLD_IDS)('%s monta, fica no orçamento, pousa no chão e cabe na caixa', (id) => {
    const def = LANDMARKS_MUNDO[id];
    expect(def.perLap).toBeGreaterThanOrEqual(1);
    const m = model(id);
    expect(m.parts.length).toBeGreaterThan(0);
    for (const p of m.parts) {
      const pos = p.geometry.attributes.position;
      expect(pos.count % 3).toBe(0);
      for (let i = 0; i < pos.array.length; i++) expect(Number.isFinite(pos.array[i])).toBe(true);
      if (p.mat !== 'cone') expect(p.geometry.attributes.color).toBeDefined();
    }
    expect(triangles(m)).toBeLessThanOrEqual(BUDGET[def.place]);
    const b = bounds(m);
    // Base: nada abaixo da fundação, e o modelo encosta no chão (não flutua).
    expect(b.min.y).toBeGreaterThanOrEqual(-FOUNDATION_M - 1e-3);
    expect(b.min.y).toBeLessThanOrEqual(0.5);
    const size = b.getSize(new THREE.Vector3());
    const [mx, my, mz] = MAX_BOX[def.place];
    expect(size.x).toBeLessThanOrEqual(mx);
    expect(size.y).toBeLessThanOrEqual(my);
    expect(size.z).toBeLessThanOrEqual(mz);
    expect(size.y).toBeGreaterThan(3);
  });

  it('é determinístico (sem sorteio): duas montagens dão a mesma geometria', () => {
    for (const id of ['girafa', 'matterhorn', 'positano']) {
      const a = LANDMARKS_MUNDO[id].build(); const b = LANDMARKS_MUNDO[id].build();
      expect(Array.from(a.parts[0].geometry.attributes.position.array)).toEqual(Array.from(b.parts[0].geometry.attributes.position.array));
    }
  });
});
