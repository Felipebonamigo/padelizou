// Marcos turísticos do Norte e do Nordeste (onda G, docs/PISTAS-TURISMO.md): todo id do contrato existe,
// monta sem erro, cabe no orçamento de triângulos do seu `place`, tem a base no chão e o tamanho do tipo.
import * as THREE from 'three';
import { describe, expect, it } from 'vitest';
import { LANDMARKS_BRASIL_NORTE_NORDESTE } from '../src/render/scenery/landmarks/brasil-norte-nordeste';
import { modelTriangles } from '../src/render/scenery/vegetation';

const IDS = [
  'farol_da_barra', 'elevador_lacerda', 'casario_pelourinho', 'arcos_atalaia', 'ponte_aracaju', 'jangada', 'coqueiral',
  'igrejas_olinda', 'bonecos_olinda', 'farol_cabo_branco', 'estacao_cabo_branco', 'forte_reis_magos', 'ponte_newton_navarro',
  'pedra_furada_jeri', 'duna_por_do_sol', 'pedra_furada_capivara', 'pinturas_rupestres', 'lagoas_lencois', 'farol_preguicas',
  'ver_o_peso', 'estacao_docas', 'teatro_amazonas', 'barco_regional', 'marco_zero_equador', 'fortaleza_macapa', 'tepui',
  'maloca', 'locomotiva_mamore', 'caixas_dagua', 'gameleira', 'palacio_rio_branco', 'serra_espirito_santo', 'dunas_jalapao',
];

/** Orçamento proposto por `place` (o teste final de orçamento é de outra tarefa). */
const BUDGET = { near: 3500, far: 5000, skyline: 2500 } as const;
/** Caixa razoável por `place`: [maior dimensão mínima (lado ou altura), maior lado máximo, altura máxima] em metros. */
const SIZE = { near: [4, 130, 60], far: [25, 1200, 160], skyline: [300, 3000, 1200] } as const;

function bounds(id: string): THREE.Box3 {
  const m = LANDMARKS_BRASIL_NORTE_NORDESTE[id].build();
  const b = new THREE.Box3();
  for (const p of m.parts) { p.geometry.computeBoundingBox(); if (p.geometry.boundingBox) b.union(p.geometry.boundingBox); }
  return b;
}

describe('marcos do Norte e do Nordeste', () => {
  it('cada id do contrato tem modelo, e só eles', () => {
    expect(Object.keys(LANDMARKS_BRASIL_NORTE_NORDESTE).sort()).toEqual([...IDS].sort());
  });

  it.each(IDS)('%s monta sem erro, com geometria válida e no orçamento', (id) => {
    const d = LANDMARKS_BRASIL_NORTE_NORDESTE[id];
    const m = d.build();
    expect(m.parts.length).toBeGreaterThan(0);
    for (const p of m.parts) {
      const pos = p.geometry.attributes.position;
      expect(pos.count % 3).toBe(0);
      expect(p.geometry.attributes.color, `${id}/${p.mat} sem cor`).toBeDefined();
      expect(p.geometry.attributes.normal, `${id}/${p.mat} sem normal`).toBeDefined();
      const arr = pos.array as Float32Array;
      for (let i = 0; i < arr.length; i++) if (!Number.isFinite(arr[i])) throw new Error(`${id}: vértice não finito`);
    }
    expect(modelTriangles(m)).toBeLessThanOrEqual(BUDGET[d.place]);
    expect(d.perLap).toBeGreaterThanOrEqual(1);
    expect(d.perLap).toBeLessThanOrEqual(6);
  });

  it.each(IDS)('%s tem a base no chão e o tamanho do seu tipo', (id) => {
    const d = LANDMARKS_BRASIL_NORTE_NORDESTE[id];
    const b = bounds(id);
    // Base em y ≈ 0: fundações e pilares podem entrar até 3 m no chão (terreno inclinado), nunca flutuar.
    expect(b.min.y).toBeGreaterThanOrEqual(-3);
    expect(b.min.y).toBeLessThanOrEqual(0.3);
    const [minSide, maxSide, maxH] = SIZE[d.place];
    const side = Math.max(b.max.x - b.min.x, b.max.z - b.min.z);
    expect(Math.max(side, b.max.y)).toBeGreaterThanOrEqual(minSide);
    expect(side).toBeLessThanOrEqual(maxSide);
    expect(b.max.y).toBeLessThanOrEqual(maxH);
    // Centrado: a origem fica dentro da pegada.
    expect(b.min.x).toBeLessThan(0); expect(b.max.x).toBeGreaterThan(0);
    expect(b.min.z).toBeLessThan(0); expect(b.max.z).toBeGreaterThan(0);
  });

  it('é determinístico (sem Math.random): montar duas vezes dá a mesma geometria', () => {
    for (const id of IDS) {
      const a = LANDMARKS_BRASIL_NORTE_NORDESTE[id].build();
      const b = LANDMARKS_BRASIL_NORTE_NORDESTE[id].build();
      expect(a.parts.length).toBe(b.parts.length);
      a.parts.forEach((p, i) => expect(Array.from(p.geometry.attributes.position.array)).toEqual(Array.from(b.parts[i].geometry.attributes.position.array)));
    }
  });

  it('marcos de corrida à noite/entardecer têm luz que acende', () => {
    const lit = ['farol_da_barra', 'elevador_lacerda', 'casario_pelourinho', 'arcos_atalaia', 'ponte_aracaju', 'ver_o_peso', 'estacao_docas', 'farol_cabo_branco'];
    for (const id of lit) {
      const mats = LANDMARKS_BRASIL_NORTE_NORDESTE[id].build().parts.map((p) => p.mat);
      expect(mats.some((m) => m === 'glow' || m === 'classic' || m === 'house'), id).toBe(true);
    }
  });
});
