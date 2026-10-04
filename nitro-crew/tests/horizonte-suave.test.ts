// Nuvens e montanhas do horizonte suaves (pedido do dono, 04/10): o shader calculava a normal pela face (dFdx/dFdy)
// e cada triângulo pegava uma luz só — nuvem e serra facetadas. Agora a normal vem do vértice, suave, e o contorno
// das montanhas tem o dobro de pontos.
import * as THREE from 'three';
import { describe, expect, it } from 'vitest';
import { buildClouds, CLOUD_FRAG } from '../src/render/sky';
import { buildRing, RING_FRAG, ringProfile } from '../src/render/terrain';

/** Fração dos cantos cuja normal se afasta > 2° da normal da própria face (0 = tudo facetado). */
function smoothFraction(g: THREE.BufferGeometry): number {
  const pos = g.getAttribute('position'); const nor = g.getAttribute('normal');
  if (!nor) return 0;
  const a = new THREE.Vector3(); const b = new THREE.Vector3(); const c = new THREE.Vector3(); const v = new THREE.Vector3();
  let bent = 0; let total = 0;
  for (let i = 0; i + 2 < pos.count; i += 3) {
    a.fromBufferAttribute(pos, i); b.fromBufferAttribute(pos, i + 1); c.fromBufferAttribute(pos, i + 2);
    const fn = b.sub(a).cross(c.sub(a));
    if (fn.lengthSq() < 1e-12) continue;
    fn.normalize();
    for (let k = 0; k < 3; k++) { total++; if (v.fromBufferAttribute(nor, i + k).dot(fn) < Math.cos(2 * Math.PI / 180)) bent++; }
  }
  return bent / total;
}

describe('horizonte suave', () => {
  it('os shaders das nuvens e das montanhas usam a normal do vértice, não a da face', () => {
    expect(CLOUD_FRAG).not.toMatch(/dFdx/);
    expect(RING_FRAG).not.toMatch(/dFdx/);
  });

  it('nuvens (alta e média/baixa) saem com normal suave', () => {
    for (const detail of [0, 1]) expect(smoothFraction(buildClouds(detail)), `detalhe ${detail}`).toBeGreaterThan(0.5);
  });

  it('montanhas: normal suave e contorno com ≥ 300 pontos por volta', () => {
    const c = new THREE.Color('#5a7a6a'); const f = new THREE.Color('#c0d0e0');
    const g = buildRing('alpine', 1, 660, 150, c, f, 0.3, new THREE.Color('#ffffff'));
    expect(smoothFraction(g)).toBeGreaterThan(0.5);
    expect(g.getAttribute('position').count / 12).toBeGreaterThanOrEqual(300);
  });

  // Na captura das Rochosas, com a normal suave, os picos em agulha (1 − |ruído| e 64–256 ondulações por volta) viraram
  // colunas claras. Montanha larga e de crista redonda: a maior dobra do contorno, em metros reais, fica abaixo de 60°
  // (era 116–154° no alpino, 63–102° no tropical, litoral e savana). O deserto fica de fora: a mesa tem parede reta.
  it('cristas arredondadas: nenhuma dobra do contorno passa de 60° (alpino, tropical, litoral, savana)', () => {
    const LAYERS = { alpine: [70, 150, 265], tropical: [62, 120, 205], coast: [34, 70, 110], savanna: [26, 52, 80] } as const;
    const RADII = [470, 660, 920];
    for (const [biome, heights] of Object.entries(LAYERS)) heights.forEach((H, layer) => {
      const N = 300; const R = RADII[layer];
      const pts = Array.from({ length: N + 2 }, (_, i) => [(i / N) * 2 * Math.PI * R, H * ringProfile(biome as keyof typeof LAYERS, layer, (i % N) / N)]);
      let worst = 0;
      for (let i = 1; i <= N; i++) {
        const a = Math.atan2(pts[i][1] - pts[i - 1][1], pts[i][0] - pts[i - 1][0]);
        const b = Math.atan2(pts[i + 1][1] - pts[i][1], pts[i + 1][0] - pts[i][0]);
        worst = Math.max(worst, (Math.abs(a - b) * 180) / Math.PI);
      }
      expect(worst, `${biome} camada ${layer}`).toBeLessThan(60);
    });
  });
});
