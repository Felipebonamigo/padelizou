// Vista de frente de um modelo, para testes de leitura (docs/VISUAL.md, "Marcos turísticos" › "Leitura"): projeção
// ortográfica olhando de +X para −X (o lado que o jogador vê, pela convenção dos marcos), com z-buffer. Cada célula
// guarda a cor sRGB da face mais próxima de quem olha. Só as partes que pintam o modelo de dia ('flat' e 'glow').
import * as THREE from 'three';
import type { Model } from '../src/render/scenery/geom';

export interface FrontView {
  /** Colunas (Z) × linhas (Y), linha 0 embaixo. `null` = céu. */
  cells: Array<THREE.Color | null>;
  cols: number;
  rows: number;
  /** Lado da célula (m). */
  cell: number;
  /** Z e Y do canto de baixo à esquerda da grade. */
  z0: number;
  y0: number;
  at(c: number, r: number): THREE.Color | null;
}

export function frontView(model: Model, cell = 1, mats: readonly string[] = ['flat', 'glow']): FrontView {
  const parts = model.parts.filter((p) => mats.includes(p.mat));
  const box = new THREE.Box3();
  for (const p of parts) { p.geometry.computeBoundingBox(); if (p.geometry.boundingBox) box.union(p.geometry.boundingBox); }
  const z0 = Math.floor(box.min.z); const y0 = Math.floor(Math.max(0, box.min.y));
  const cols = Math.ceil((box.max.z - z0) / cell) + 1; const rows = Math.ceil((box.max.y - y0) / cell) + 1;
  const depth = new Float32Array(cols * rows).fill(-Infinity);
  const cells: Array<THREE.Color | null> = new Array(cols * rows).fill(null);
  const a = new THREE.Vector3(); const b = new THREE.Vector3(); const c = new THREE.Vector3();
  for (const p of parts) {
    const pos = p.geometry.getAttribute('position'); const col = p.geometry.getAttribute('color');
    const idx = p.geometry.index;
    const n = idx ? idx.count : pos.count;
    for (let t = 0; t + 2 < n; t += 3) {
      const i0 = idx ? idx.getX(t) : t; const i1 = idx ? idx.getX(t + 1) : t + 1; const i2 = idx ? idx.getX(t + 2) : t + 2;
      a.fromBufferAttribute(pos, i0); b.fromBufferAttribute(pos, i1); c.fromBufferAttribute(pos, i2);
      // Grade: u = coluna (z), v = linha (y), nos centros das células.
      const u = [(a.z - z0) / cell, (b.z - z0) / cell, (c.z - z0) / cell];
      const v = [(a.y - y0) / cell, (b.y - y0) / cell, (c.y - y0) / cell];
      const den = (v[1] - v[2]) * (u[0] - u[2]) + (u[2] - u[1]) * (v[0] - v[2]);
      if (Math.abs(den) < 1e-9) continue;
      const cMin = Math.max(0, Math.floor(Math.min(...u))); const cMax = Math.min(cols - 1, Math.ceil(Math.max(...u)));
      const rMin = Math.max(0, Math.floor(Math.min(...v))); const rMax = Math.min(rows - 1, Math.ceil(Math.max(...v)));
      const color = col ? new THREE.Color().fromBufferAttribute(col as THREE.BufferAttribute, i0).convertLinearToSRGB() : new THREE.Color(1, 0, 1);
      for (let r = rMin; r <= rMax; r++) {
        for (let q = cMin; q <= cMax; q++) {
          const pu = q + 0.5; const pv = r + 0.5;
          const w0 = ((v[1] - v[2]) * (pu - u[2]) + (u[2] - u[1]) * (pv - v[2])) / den;
          const w1 = ((v[2] - v[0]) * (pu - u[2]) + (u[0] - u[2]) * (pv - v[2])) / den;
          const w2 = 1 - w0 - w1;
          if (w0 < 0 || w1 < 0 || w2 < 0) continue;
          const x = w0 * a.x + w1 * b.x + w2 * c.x;
          const k = r * cols + q;
          if (x > depth[k]) { depth[k] = x; cells[k] = color; }
        }
      }
    }
  }
  return { cells, cols, rows, cell, z0, y0, at: (q, r) => cells[r * cols + q] };
}

/** Tom e saturação (HSL) de uma cor sRGB. */
export function hsl(c: THREE.Color): { h: number; s: number; l: number } {
  const o = { h: 0, s: 0, l: 0 };
  // `c` já está em sRGB: lê os números como estão, sem conversão de espaço.
  new THREE.Color().setRGB(c.r, c.g, c.b, THREE.LinearSRGBColorSpace).getHSL(o, THREE.LinearSRGBColorSpace);
  return o;
}
