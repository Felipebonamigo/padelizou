// Marcos da segunda leva do Brasil (EXTRA_BRAZIL_PLACES em src/core/data/places.ts), Sudeste, Sul e Centro-Oeste.
// Convenção dos modelos: landmarks/types.ts; ferramentas: landmarks/kit.ts. Low-poly de faces planas e cor chapada,
// metros reais (skyline: escala grande), lido de longe pela silhueta; frente para +X, comprimento em Z.
import * as THREE from 'three';
import { hash2 } from '../../noise';
import { box, cone, cyl, gable, hip, ico, jitter, paint, sphere, tf, tintUp, tris, type Geo, type Model } from '../geom';
import { beam, cable, cliff, dune, facadeBox, hill, Kit, landmarkPart, lathe } from './kit';
import type { LandmarkRegistry } from './types';

type V3 = [number, number, number];

// Cores que se repetem.
const WHITE = '#f4f2ea';
const TILE = '#b5583a'; // telhado de barro
const STONE = '#9a9282';
const WOOD = '#6e4a2c';
const LAMP = '#ffd98a';
const WATER_FALL = '#eef7ff';

/** Janelinha escura (vidro/abertura) encostada na face +X em (x, y, z). */
function win(x: number, y: number, z: number, w: number, h: number, color = '#283038'): Geo {
  return paint(box(0.25, h, w), color, tf(x, y, z));
}

/** Quadrilátero a, b, c, d (dois triângulos) acrescentado à lista. */
function quad(out: number[], a: V3, b: V3, c: V3, d: V3): void {
  out.push(...a, ...b, ...c, ...a, ...c, ...d);
}

/** Triângulos soltos com as duas faces (vela, bandeirola): cada um repetido na ordem inversa. */
function twoSided(p: number[]): Geo {
  const out = [...p];
  for (let i = 0; i < p.length; i += 9) out.push(p[i], p[i + 1], p[i + 2], p[i + 6], p[i + 7], p[i + 8], p[i + 3], p[i + 4], p[i + 5]);
  return tris(out);
}

/** Tons de água das quedas (do branco ao azul-claro): as faixas de tons diferentes são os riscos da queda. */
const FALL_TONES = ['#ffffff', '#eaf5fc', '#d6eaf5', '#f4fbff', '#c8e2ef'];
/** Névoa e espuma: luz (sem sombra) — acesa pelo sol, a névoa virava pedra cinza. */
const MIST = '#e4eef2';

/**
 * Véu d'água diante de um paredão que olha para +X: do lábio (x0, yTop) ao pé (x1, yBot), centrado em z, abrindo de
 * `wTop` a `wBot`; `B` faixas verticais de tons (os riscos: "queda d'água", não "painel") e `R` fileiras (a barriga
 * que se lança do lábio). Material de luz: brilha à noite e não pega a sombra do paredão.
 */
function veil(k: Kit, seed: number, z: number, x0: number, x1: number, yTop: number, yBot: number, wTop: number, wBot: number, B = 6, R = 3): void {
  const P = (s: number, t: number): V3 => [x0 + (x1 - x0) * Math.sqrt(t), yTop - (yTop - yBot) * t, z + s * (wTop + (wBot - wTop) * t)];
  for (let b = 0; b < B; b++) {
    const out: number[] = [];
    const s0 = -0.5 + b / B; const s1 = -0.5 + (b + 1) / B;
    for (let j = 0; j < R; j++) quad(out, P(s0, j / R), P(s1, j / R), P(s1, (j + 1) / R), P(s0, (j + 1) / R));
    k.light(paint(tris(out), FALL_TONES[Math.floor(hash2(seed, b) * FALL_TONES.length)]));
  }
}

/**
 * Casco de barco ao longo de Z (proa em +Z): costado `upper` do convés até o bojo, obras vivas `lower` do bojo à
 * quilha, convés `deck`. `B` = boca, `D` = altura do convés sobre a água, `draft` = calado.
 */
function hull(L: number, B: number, D: number, upper: string, lower: string, deck: string, m: THREE.Matrix4, draft = 0.8, sheer = 0.5): Geo[] {
  const N = 6;
  const st = (i: number): { z: number; b: number; yd: number; dr: number } => {
    const t = i / N;
    const b = (B / 2) * (t < 0.55 ? 0.8 + 0.2 * Math.sin((t / 0.55) * Math.PI / 2) : Math.sqrt(Math.max(0, (1 - t) / 0.45)));
    return { z: -L / 2 + t * L, b, yd: D + sheer * t * t, dr: -draft * (t < 0.8 ? 1 : (1 - t) / 0.2) };
  };
  const up: number[] = []; const lo: number[] = []; const dk: number[] = [];
  for (let i = 0; i < N; i++) {
    const a = st(i); const c = st(i + 1);
    for (const s of [-1, 1]) {
      const dA: V3 = [s * a.b, a.yd, a.z]; const dC: V3 = [s * c.b, c.yd, c.z];
      const cA: V3 = [s * a.b * 0.86, 0.1, a.z]; const cC: V3 = [s * c.b * 0.86, 0.1, c.z];
      // Normais para fora (o casco é visto dos dois lados: a ordem troca no bordo de −X).
      if (s > 0) { quad(up, dA, dC, cC, cA); quad(lo, cA, cC, [0, c.dr, c.z], [0, a.dr, a.z]); }
      else { quad(up, cA, cC, dC, dA); quad(lo, [0, a.dr, a.z], [0, c.dr, c.z], cC, cA); }
    }
    quad(dk, [-c.b, c.yd, c.z], [c.b, c.yd, c.z], [a.b, a.yd, a.z], [-a.b, a.yd, a.z]);
  }
  // Painel de popa (espelho).
  const s0 = st(0);
  up.push(-s0.b, s0.yd, s0.z, s0.b, s0.yd, s0.z, s0.b * 0.86, 0.1, s0.z, -s0.b, s0.yd, s0.z, s0.b * 0.86, 0.1, s0.z, -s0.b * 0.86, 0.1, s0.z);
  lo.push(-s0.b * 0.86, 0.1, s0.z, s0.b * 0.86, 0.1, s0.z, 0, s0.dr, s0.z);
  return [paint(tris(up), upper, m), paint(tris(lo), lower, m), paint(tris(dk), deck, m)];
}

/** Abóbada parabólica (casca sem tampas): eixo em X (`len`), vão `span` em Z, flecha `rise`. */
function vaultShell(span: number, rise: number, len: number, seg: number): number[] {
  const out: number[] = [];
  const P = (i: number): [number, number] => { const z = -span / 2 + (span * i) / seg; return [z, rise * (1 - (2 * z / span) ** 2)]; };
  for (let i = 0; i < seg; i++) {
    const [z0, y0] = P(i); const [z1, y1] = P(i + 1);
    quad(out, [-len / 2, y1, z1], [len / 2, y1, z1], [len / 2, y0, z0], [-len / 2, y0, z0]);
  }
  return out;
}

/** Tampa da abóbada (leque parabólico) no plano x, de frente para `face` × X. */
function vaultCap(span: number, rise: number, seg: number, x: number, face = 1): number[] {
  const out: number[] = [];
  for (let i = 0; i < seg; i++) {
    let z0 = -span / 2 + (span * i) / seg; let z1 = -span / 2 + (span * (i + 1)) / seg;
    if (face > 0) [z0, z1] = [z1, z0];
    out.push(x, 0, 0, x, rise * (1 - (2 * z1 / span) ** 2), z1, x, rise * (1 - (2 * z0 / span) ** 2), z0);
  }
  return out;
}

/** Bloco de rocha: caixa unitária subdividida, deformada por hash e levada a `m` (escala nela). */
function rock(seed: number, color: string, m: THREE.Matrix4, seg: [number, number, number] = [1, 2, 2], amount = 0.08): Geo {
  return paint(jitter(new THREE.BoxGeometry(1, 1, 1, seg[0], seg[1], seg[2]), amount, seed), color, m);
}

/** Moita/copa: icosaedro deformado. */
function blob(seed: number, color: string, x: number, y: number, z: number, sx: number, sy: number, sz: number): Geo {
  return paint(jitter(ico(1, 0), 0.2, seed), color, tf(x, y, z, sx, sy, sz));
}

// ───────────────────────────── Sudeste ─────────────────────────────

/**
 * Casario de Paraty: sobrados brancos de cunhais, barrados e portas coloridos sobre o pé de moleque, e a Igreja de
 * Santa Rita (branca, torre de lado com o coruchéu) no fim da rua.
 */
function casarioParaty(): Model {
  const k = new Kit();
  const trims = ['#2f5fa8', '#e2b23a', '#3d8a5a', '#b8402e', '#2a8a9a', '#e2b23a'];
  const W = 8.4; const D = 10; const n = 6; const z0 = -34;
  for (let i = 0; i < n; i++) {
    const z = z0 + W / 2 + i * W;
    const two = i % 3 !== 1;
    const h = two ? 7 : 4.4;
    const trim = trims[i];
    k.add(paint(box(D, 2.2, W), STONE, tf(0, -1, z)));
    k.facade('house', facadeBox('house', D, h, W - 0.1, '#f6f4ec', tf(0, h / 2, z), hash2(i, 5) * 6));
    // Cunhais e barrado coloridos, cimalha branca, portas pintadas.
    for (const dz of [-W / 2 + 0.3, W / 2 - 0.3]) k.add(paint(box(0.6, h, 0.6), trim, tf(D / 2 + 0.05, h / 2, z + dz)));
    k.add(paint(box(0.3, 0.9, W - 0.6), trim, tf(D / 2 + 0.1, 0.45, z)));
    k.add(paint(box(0.7, 0.45, W), WHITE, tf(D / 2 + 0.1, h - 0.2, z)));
    for (const dz of two ? [-2, 2] : [-2.4, 0, 2.4]) k.add(win(D / 2 + 0.15, 1.45, z + dz, 1.3, 2.5, trim));
    if (two) {
      for (const dz of [-2, 2]) k.add(paint(box(0.9, 0.12, 1.8), '#2a2a2a', tf(D / 2 + 0.45, 3.9, z + dz)), paint(box(0.06, 0.9, 1.8), '#2a2a2a', tf(D / 2 + 0.9, 4.35, z + dz)));
    }
    k.add(paint(gable(D, 3.2, W, 0.7), TILE, tf(0, h, z)));
    k.light(paint(box(0.4, 0.5, 0.3), LAMP, tf(D / 2 + 0.4, 2.9, z)));
  }
  // Pé de moleque na frente (pedras grandes irregulares) e o meio-fio.
  const L = n * W + 22;
  k.add(paint(box(7, 0.3, L), '#8a8072', tf(D / 2 + 3.5, 0.15, z0 + L / 2)));
  for (let s = 0; s < 10; s++) k.add(paint(jitter(ico(1, 0), 0.25, 300 + s), '#a39a8a', tf(D / 2 + 1.5 + hash2(s, 301) * 5, 0.25, z0 + 2 + s * 6.4, 0.9, 0.25, 0.9)));
  // Igreja de Santa Rita: nave, frontão com volutas e cantaria, a torre recuada do lado.
  const zc = 28; const fx = 6;
  k.add(paint(box(26, 0.6, 15), STONE, tf(fx - 9, 0.3, zc)));
  k.add(paint(box(22, 10, 12), WHITE, tf(fx - 11, 5, zc)), paint(gable(12, 4.6, 22, 0.5), TILE, tf(fx - 11, 10, zc, 1, 1, 1, 0, Math.PI / 2, 0)));
  k.add(paint(box(1.4, 11.5, 12.4), WHITE, tf(fx, 5.75, zc)));
  k.add(paint(gable(12.4, 4.8, 1.4, 0), WHITE, tf(fx, 11.5, zc, 1, 1, 1, 0, Math.PI / 2, 0)));
  k.add(beam([fx + 0.75, 11.5, zc - 6.4], [fx + 0.75, 16.3, zc], 0.55, STONE), beam([fx + 0.75, 11.5, zc + 6.4], [fx + 0.75, 16.3, zc], 0.55, STONE));
  k.add(paint(box(0.6, 0.6, 13.4), STONE, tf(fx + 0.75, 11.4, zc)));
  for (const s of [-1, 1]) {
    k.add(paint(box(0.8, 11.5, 0.8), STONE, tf(fx + 0.55, 5.75, zc + s * 5.9)));
    k.add(paint(cyl(0.9, 0.9, 0.6, 8, false), STONE, tf(fx + 0.6, 12, zc + s * 7, 1, 1, 1, 0, 0, Math.PI / 2))); // voluta
  }
  k.add(paint(box(0.5, 2.6, 0.5), STONE, tf(fx + 0.4, 17.6, zc)), paint(box(0.5, 0.5, 1.8), STONE, tf(fx + 0.4, 18.2, zc)));
  k.add(paint(box(0.4, 5.6, 3.4), STONE, tf(fx + 0.8, 2.8, zc)), win(fx + 1.05, 2.5, zc, 2.4, 4.8, '#4a2e1a'));
  for (const dz of [-3.6, 3.6]) k.add(win(fx + 0.8, 8.2, zc + dz, 1.3, 2.4, '#2a3038'), paint(box(0.3, 2.8, 1.7), STONE, tf(fx + 0.72, 8.2, zc + dz)));
  // Torre: branca, cunhais de pedra, sineira e coruchéu branco piramidal.
  const tx = fx - 4; const tz = zc + 8.6;
  k.add(paint(box(5, 16, 5), WHITE, tf(tx, 8, tz)));
  for (const dx of [-2.3, 2.3]) for (const dz of [-2.3, 2.3]) k.add(paint(box(0.6, 16, 0.6), STONE, tf(tx + dx, 8, tz + dz)));
  k.add(paint(box(5.6, 0.6, 5.6), STONE, tf(tx, 16.1, tz)), win(tx + 2.55, 13.6, tz, 1.4, 2.8, '#1e2226'));
  k.add(paint(hip(5.2, 6, 5.2), '#ece8de', tf(tx, 16.4, tz)), paint(sphere(0.35, 6, 4), STONE, tf(tx, 22.6, tz)));
  k.light(paint(box(0.3, 1.4, 0.8), LAMP, tf(tx + 2.65, 13.4, tz)), paint(box(0.4, 0.5, 0.4), LAMP, tf(fx + 1.2, 4.6, zc - 2.2)), paint(box(0.4, 0.5, 0.4), LAMP, tf(fx + 1.2, 4.6, zc + 2.2)));
  return k.model([-1, 22], 0.03, 301);
}

/**
 * Escuna de passeio de Paraty: casco colorido, toldo listrado no convés de cima, dois mastros com as velas
 * enroladas, o gurupés e o cordão de lâmpadas (sai ao entardecer); uma canoa caiçara ao lado.
 */
function escuna(): Model {
  const k = new Kit();
  const L = 22; const D = 2.2;
  k.add(...hull(L, 6, D, '#f0c23a', '#2f6fb0', '#a8784a', tf(0, 0, 0), 1, 0.7));
  // Faixa (verdugo) e linha d'água.
  k.add(paint(box(6.3, 0.35, L * 0.62), '#c8402e', tf(0, D - 0.3, -2.5)));
  // Convés: cabine branca, toldo listrado sobre pilares, o deque de cima com a balaustrada.
  const yd = D + 0.1;
  k.add(paint(box(4.6, 2.3, 12), WHITE, tf(0, yd + 1.15, -2.2)));
  for (let w = 0; w < 5; w++) for (const s of [-1, 1]) k.add(win(s * 2.3, yd + 1.4, -6.8 + w * 2.3, 1.3, 0.9, '#2a3a48'));
  k.add(paint(box(5.2, 0.3, 13), '#a8784a', tf(0, yd + 2.45, -2.2)));
  for (const z of [-8.4, -4.2, 0, 3.8]) for (const s of [-1, 1]) k.add(paint(box(0.18, 2.4, 0.18), WHITE, tf(s * 2.5, yd + 3.7, z)));
  for (const s of [-1, 1]) k.add(paint(box(0.1, 0.9, 13), WHITE, tf(s * 2.55, yd + 3.1, -2.2)));
  for (let i = 0; i < 6; i++) k.add(paint(box(5.6, 0.25, 2.2), i % 2 ? '#f2f0e8' : '#d9483b', tf(0, yd + 4.95, -7.7 + i * 2.2)));
  // Mastros, retrancas com a vela enrolada, gurupés e o estai.
  const mast = (z: number, h: number): V3 => {
    k.add(paint(cyl(0.14, 0.2, h, 6), '#7a5230', tf(0, yd + h / 2, z)));
    k.add(beam([0, yd + 3.4, z], [0, yd + 3.0, z - 5.5], 0.3, '#7a5230'), beam([0, yd + 3.75, z - 0.3], [0, yd + 3.35, z - 5.2], 0.55, '#efe8d6'));
    return [0, yd + h, z];
  };
  const fore = mast(6, 10.5); const main = mast(-0.4, 12);
  k.add(beam([0, yd + 0.6, L / 2 - 0.5], [0, yd + 1.6, L / 2 + 4], 0.25, '#7a5230'));
  k.add(cable(fore, [0, yd + 1.6, L / 2 + 4], 0.08, '#2a2a2a'), cable(main, fore, 0.08, '#2a2a2a'), cable(main, [0, yd + 0.5, -L / 2 + 0.6], 0.08, '#2a2a2a'));
  // Bandeirolas no topo dos mastros.
  for (const p of [fore, main]) k.add(paint(box(0.05, 0.7, 1.4), '#2a8a4a', tf(p[0], p[1] - 0.4, p[2] - 0.75)));
  // Cordão de lâmpadas do gurupés ao mastro de vante, ao mastro grande e à popa.
  const bulbs = (a: V3, b: V3, n: number): void => {
    for (let i = 1; i < n; i++) { const t = i / n; k.light(paint(box(0.25, 0.25, 0.25), LAMP, tf(a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t - Math.sin(t * Math.PI) * 0.6, a[2] + (b[2] - a[2]) * t))); }
  };
  bulbs([0, yd + 1.6, L / 2 + 4], fore, 7); bulbs(fore, main, 5); bulbs(main, [0, yd + 0.5, -L / 2 + 0.6], 8);
  // Pneus de defensa no costado e a canoa caiçara.
  for (const z of [-5, 0, 5]) k.add(paint(cyl(0.45, 0.45, 0.3, 6), '#1e1e1e', tf(3.05, 1.2, z, 1, 1, 1, 0, 0, Math.PI / 2)));
  k.add(...hull(7, 1.4, 0.7, '#e8e4d8', '#c8402e', '#8a6a4a', tf(6.5, 0, -12), 0.3, 0.3));
  return k.model([-1, 14], 0.02, 311);
}

/**
 * Dedo de Deus (Serra dos Órgãos, skyline): o dedo de granito — coluna de ponta arredondada, com o nó e inclinado —
 * sozinho acima do ombro da serra de mata, o polegar encostado, o Escalavrado largo e o Nariz do Frade bem mais baixos.
 * Era um cone entre picos da mesma altura: de longe, três pedras quaisquer (tests/landmarks-leitura-centro-sul-2.test.ts).
 */
function dedoDeDeus(): Model {
  const k = new Kit();
  const granite = '#7d7e77';
  k.add(hill(330, 115, 280, 201, '#2f6a33', '#3f7f3a', 1, 0.14, 1.0, tf(-70, 0, 0)));
  // O ombro da serra, bem abaixo do dedo: o Escalavrado largo, o polegar encostado e o Nariz do Frade.
  k.add(tintUp(hill(120, 195, 100, 202, '#7a7b74', null, 1, 0.12, 0.4, tf(-50, 0, -175)), '#46803f', 0.5, 0.9));
  k.add(tintUp(hill(70, 165, 60, 203, '#73776f', null, 1, 0.1, 0.4, tf(-30, 0, 48)), '#3f7a3a', 0.45, 1));
  k.add(tintUp(hill(36, 175, 32, 205, granite, null, 1, 0.08, 0.3, tf(-24, 0, 64, 1, 1, 1, 0.2, 0, 0)), '#4a8340', 0.7, 0.6));
  k.add(tintUp(hill(40, 175, 36, 206, '#7f7f77', null, 1, 0.1, 0.3, tf(-60, 0, 180, 1, 1, 1, -0.08, 0, 0)), '#4a8340', 0.6, 0.8));
  // O dedo (o traço do lugar): uma coluna de granito quase da mesma largura até a ponta arredondada, sozinho acima do
  // ombro, com a dobra do nó e inclinado para +Z (lê de perfil para quem vem pela pista). Era um cone entre picos.
  // A rugosidade (jitter) no referencial da peça, antes de levar ao lugar: as duas metades continuam encostadas.
  const lower = jitter(lathe([[34, -20], [31, 60], [28, 150], [26.5, 232]], 8, granite), 0.03, 207).applyMatrix4(tf(-14, 0, 6, 1, 1, 1, 0.07, 0, 0));
  const upper = jitter(lathe([[27, -4], [25.5, 70], [23.5, 120], [20.5, 146], [15, 158], [6, 165], [0.5, 166]], 8, granite), 0.015, 208)
    .applyMatrix4(tf(-14, 228 * Math.cos(0.07), 6 + 228 * Math.sin(0.07), 1, 1, 1, 0.16, 0, 0));
  for (const g of [lower, upper]) k.add(tintUp(g, '#4f8442', 0.82, 0.55));
  return k.model([0, 390], 0.06, 201);
}

/** Farol de Ilhabela: torre branca de faixa vermelha na ponta de pedra, a casa do faroleiro e a espuma em volta. */
function farolIlhabela(): Model {
  const k = new Kit();
  k.add(paint(cyl(30, 30, 0.25, 14), '#e6f0f2', tf(0, 0.05, 0)));
  k.add(hill(27, 10, 23, 211, '#5d5850', '#4f7a3a', 1, 0.2, 0.8));
  for (let i = 0; i < 6; i++) k.add(paint(jitter(ico(1, 0), 0.25, 212 + i), '#4f4a44', tf(Math.cos(i * 1.1) * 24, 0.5, Math.sin(i * 1.1) * 21, 3, 2, 3)));
  // Mata rala no alto da pedra.
  for (let i = 0; i < 5; i++) k.add(blob(218 + i, i % 2 ? '#3f7a3a' : '#4f8a40', -8 + hash2(i, 219) * 6, 8.5, -10 + i * 5, 3, 2.2, 3));
  const y0 = 8.5;
  k.add(paint(cyl(3.4, 3.6, 1.6, 10), '#d8d4c8', tf(0, y0 + 0.4, 0)));
  k.add(lathe([[2.7, 0], [2.4, 9], [2.05, 17], [2.0, 17.6]], 10, WHITE, tf(0, y0, 0)));
  for (const [y, r] of [[5.5, 2.58], [12.5, 2.28]] as Array<[number, number]>) k.add(paint(cyl(r, r + 0.04, 2.6, 10), '#c8302a', tf(0, y0 + y, 0)));
  for (let i = 0; i < 3; i++) k.add(win(2.3 - i * 0.12, y0 + 4 + i * 5, 0, 0.7, 1.2, '#283038'));
  const yt = y0 + 17.6;
  k.add(paint(cyl(2.8, 2.6, 0.5, 10), '#2a2a2a', tf(0, yt + 0.25, 0)));
  k.light(paint(cyl(1.4, 1.4, 2.2, 10), '#fff3c0', tf(0, yt + 1.6, 0)));
  for (let i = 0; i < 6; i++) k.add(paint(box(0.12, 2.2, 0.12), '#2a2a2a', tf(Math.cos(i * 1.05) * 1.45, yt + 1.6, Math.sin(i * 1.05) * 1.45)));
  k.add(paint(cone(1.8, 1.6, 10), '#c8302a', tf(0, yt + 3.5, 0)), paint(box(0.12, 1.2, 0.12), '#2a2a2a', tf(0, yt + 4.8, 0)));
  k.blink(paint(sphere(0.9, 6, 4), '#fff2b0', tf(0, yt + 1.6, 0, 1.7, 1, 1.7)));
  // Casa do faroleiro (paredes descem até a pedra).
  k.add(paint(box(6, 6, 9), WHITE, tf(-4.5, y0 + 0.4, 7.5)), paint(hip(6.8, 2.4, 9.8), '#c8302a', tf(-4.5, y0 + 3.4, 7.5)));
  k.add(win(-1.45, y0 + 1.6, 6, 1.1, 1.4), win(-1.45, y0 + 1.6, 9, 1.1, 1.4));
  return k.model([0, yt + 5], 0.04, 211);
}

/** Veleiros no Canal de São Sebastião (Ilhabela): três barcos adernados, velas grande e genoa, um balão colorido. */
function veleiroCanal(): Model {
  const k = new Kit();
  const boat = (x: number, z: number, S: number, hullC: string, sail: string, heel: number, yaw: number, spin: string | null): void => {
    const M = tf(x, 0, z, S, S, S, 0, yaw, heel);
    const at = (m: THREE.Matrix4): THREE.Matrix4 => M.clone().multiply(m);
    k.add(...hull(11, 3.6, 1.2, hullC, '#2a2a30', '#d8d2c2', M, 1.3, 0.25));
    k.add(paint(box(2.2, 0.8, 3.4), '#f2f2ee', at(tf(0, 1.6, -1.6))), paint(box(2.3, 0.3, 2.6), '#2a3a48', at(tf(0, 1.75, -1.4))));
    // Mastro e retranca; vela grande e genoa (triângulos, material liso dos dois lados).
    k.add(paint(cyl(0.09, 0.12, 15, 5), '#cfd2d6', at(tf(0, 8.6, 0.8))), paint(cyl(0.08, 0.08, 4.6, 5), '#cfd2d6', at(tf(0, 2.5, -1.5, 1, 1, 1, Math.PI / 2, 0, 0))));
    k.add(paint(twoSided([0, 2.6, 0.7, 0, 15.6, 0.7, 0, 2.6, -3.9]), sail, M.clone()));
    if (!spin) k.add(paint(twoSided([0.12, 1.6, 5.2, 0.12, 13.2, 1.0, 0.12, 1.9, -0.4]), sail, M.clone()));
    k.add(cable([0, 1.4, 5.4], [0, 15.8, 0.8], 0.05, '#3a3a3a').applyMatrix4(M));
    if (spin) {
      // Balão (spinnaker) inflado na proa, em gomos de duas cores.
      for (let g = 0; g < 4; g++) k.add(paint(sphere(1, 6, 5, g * Math.PI / 2, Math.PI / 2, 0, Math.PI), g % 2 ? spin : '#f4f2ea', at(tf(0.5, 8.6, 4.6, 1.5, 5.4, 1.8))));
    }
    // Esteira branca atrás.
    k.add(paint(tris([-1.4, 0.06, -5, 1.4, 0.06, -5, 0, 0.06, -16, -1.2, 0.06, 4.5, -1.5, 0.06, -5, -3, 0.06, -9]), '#f2f8fa', tf(x, 0, z, S, 1, S, 0, yaw, 0)));
  };
  boat(-3, -18, 1, '#f4f2ea', '#f4f2ea', 0.13, 0.08, null);
  boat(5, 7, 1.15, '#1f3a6a', '#f4f2ea', 0.18, -0.05, '#e8403a');
  boat(-9, 28, 0.9, '#c8402e', '#efe6cc', 0.1, 0.12, null);
  // Boia de regata.
  k.add(paint(cone(0.9, 2.2, 6), '#f28a1a', tf(9, 1.1, -4)), paint(cyl(0.9, 0.9, 0.6, 6), '#f28a1a', tf(9, 0, -4)));
  return k.model([-1, 18], 0, 221);
}

/**
 * Chalé enxaimel de Campos do Jordão: térreo de pedra, andar e oitão brancos com a trama de madeira escura (pilares,
 * frechais e mãos-francesas), telhado íngreme, sacada com floreiras, chaminé.
 */
function chaleEnxaimel(): Model {
  const k = new Kit();
  const T = '#4a2e1c'; const wall = '#f3efe4'; const fx = 6.3;
  k.add(paint(box(12.4, 2, 10.4), '#7f776a', tf(0, -0.6, 0)));
  k.add(paint(box(12, 3.2, 10), '#8a8174', tf(0, 1.6, 0)));
  k.add(paint(box(12.6, 5.6, 10.6), wall, tf(0, 6, 0)));
  k.add(paint(gable(11.8, 7.2, 14, 0.6), '#5e3a2e', tf(0, 8.8, 0, 1, 1, 1, 0, Math.PI / 2, 0)));
  k.add(paint(gable(10.6, 6.7, 0.3, 0), wall, tf(fx + 0.05, 8.8, 0, 1, 1, 1, 0, Math.PI / 2, 0)));
  // Trama da fachada (+X): pilares, frechais, diagonais nos painéis de canto e a do oitão.
  for (const z of [-5.15, -2.6, 0, 2.6, 5.15]) k.add(paint(box(0.3, 5.6, 0.35), T, tf(fx + 0.1, 6, z)));
  for (const y of [3.35, 6, 8.65]) k.add(paint(box(0.3, 0.35, 10.6), T, tf(fx + 0.1, y, 0)));
  for (const s of [-1, 1]) {
    k.add(beam([fx + 0.12, 3.4, s * 5.1], [fx + 0.12, 6, s * 2.6], 0.3, T), beam([fx + 0.12, 6, s * 5.1], [fx + 0.12, 8.6, s * 2.6], 0.3, T));
    k.add(beam([fx + 0.18, 8.8, s * 5.2], [fx + 0.18, 15.4, 0], 0.4, T));
    k.add(beam([fx + 0.12, 8.8, s * 2.6], [fx + 0.12, 11.6, s * 1.6], 0.28, T));
  }
  k.add(paint(box(0.3, 6.6, 0.35), T, tf(fx + 0.14, 12.1, 0)), paint(box(0.3, 0.35, 6), T, tf(fx + 0.14, 11.6, 0)));
  // Laterais (±Z): pilares e frechais.
  for (const s of [-1, 1]) {
    for (const x of [-6.1, -3, 0, 3, 6.1]) k.add(paint(box(0.35, 5.6, 0.3), T, tf(x, 6, s * 5.35)));
    for (const y of [3.35, 8.65]) k.add(paint(box(12.6, 0.35, 0.3), T, tf(0, y, s * 5.35)));
    for (const x of [-4.5, 1.5]) k.add(paint(box(1.3, 1.5, 0.2), '#2a3038', tf(x, 6, s * 5.4)));
  }
  // Janelas com venezianas verdes e floreiras vermelhas; a do sótão acesa.
  for (const z of [-1.3, 1.3, -3.9, 3.9]) {
    k.add(win(fx + 0.2, 5.2, z, 1.1, 1.5));
    k.add(paint(box(0.2, 1.5, 0.45), '#3b6b3a', tf(fx + 0.25, 5.2, z - 0.8)), paint(box(0.2, 1.5, 0.45), '#3b6b3a', tf(fx + 0.25, 5.2, z + 0.8)));
  }
  k.light(paint(box(0.2, 1.2, 1), '#ffd98a', tf(fx + 0.25, 10.2, 0)));
  // Sacada de madeira no andar, com floreira de gerânios.
  k.add(paint(box(1.6, 0.3, 8.4), T, tf(fx + 0.8, 3.4, 0)));
  k.add(paint(box(0.15, 1.1, 8.4), '#6a4a2e', tf(fx + 1.55, 4.1, 0)));
  for (let i = 0; i < 9; i++) k.add(paint(box(0.08, 1.1, 0.1), '#6a4a2e', tf(fx + 1.55, 4.1, -4 + i)));
  k.add(paint(box(0.5, 0.4, 8.2), '#7a4a2a', tf(fx + 1.75, 4.8, 0)));
  for (let i = 0; i < 8; i++) k.add(blob(400 + i, i % 2 ? '#d8344a' : '#e85a8a', fx + 1.8, 5.15, -3.6 + i * 1.05, 0.45, 0.35, 0.5));
  // Térreo: porta de madeira, janelas, lampiões.
  k.add(win(fx + 0.05, 1.3, 2.6, 1.6, 2.6, '#5a3a22'), win(fx + 0.05, 1.7, -2, 1.4, 1.3), win(fx + 0.05, 1.7, -4.2, 1.2, 1.3));
  k.light(paint(box(0.4, 0.55, 0.4), LAMP, tf(fx + 0.4, 2.7, 4)), paint(box(0.4, 0.55, 0.4), LAMP, tf(fx + 0.4, 2.7, 1.2)));
  // Chaminé de pedra.
  k.add(paint(box(1.3, 6, 1.3), '#8a8174', tf(-3, 12, 3.2)), paint(box(1.6, 0.4, 1.6), '#5a544c', tf(-3, 15.1, 3.2)));
  return k.model([0, 16], 0.03, 401);
}

/**
 * Igreja da Pampulha (Niemeyer, ×1,5): a onda de abóbadas brancas — a da nave e três menores, lado a lado —, cada
 * tampa com o painel de azulejos azuis de Portinari emoldurado pela borda da casca, a torre em trapézio invertido
 * ligada pela marquise inclinada; a lagoa atrás. Era um borrão branco de 14 m entre os prédios: a abóbada grande
 * escondia as pequenas e o azulejo era um xadrez solto (tests/landmarks-leitura-centro-sul-2.test.ts).
 */
function igrejaPampulha(): Model {
  const k = new Kit();
  const shell = '#f2f1ec'; const blue = '#1f4f9e'; const blue2 = '#3a74c4'; const fx = 8;
  k.add(paint(box(70, 0.2, 104), '#3f78a0', tf(-52, 0.1, 2))); // lagoa
  k.add(paint(box(22, 0.25, 84), '#5c9a48', tf(19, 0.12, 4))); // jardim
  for (let i = 0; i < 5; i++) k.add(blob(410 + i, '#3f7a36', 22 + (i % 2) * 5, 0.6, -26 + i * 13, 2.8, 0.9, 3.6)); // canteiros
  // A onda de abóbadas (o traço do lugar, ×1,5): lado a lado em Z, eixo em X, da nave às menores — o perfil de cima
  // sobe e desce quatro vezes. Na face da pista, a tampa de cada uma é o painel de azulejos azuis, emoldurado pela
  // borda branca da casca.
  const vaults: Array<[number, number, number, number]> = [ // z, vão, flecha, comprimento
    [-16, 20, 22, 34], [-0.5, 11, 14, 16], [10.5, 11, 11.5, 14], [21, 10, 9, 12],
  ];
  vaults.forEach(([z, sp, rise, len], i) => {
    k.add(paint(tris(vaultShell(sp, rise, len, 12)), shell, tf(fx - len / 2, 0, z)));
    // Borda da casca (1 m de espessura vista de frente) e o painel recuado dentro dela.
    const rim: number[] = []; const N = 12; const w = 1.1;
    for (let j = 0; j < N; j++) {
      const P = (t: number, d: number): V3 => {
        const zz = -sp / 2 + sp * t; const h = rise * (1 - (2 * zz / sp) ** 2);
        const nz = (8 * rise * zz) / (sp * sp); const n = Math.hypot(nz, 1);
        return [fx + 0.06, h - d / n, z + zz - (d * nz) / n];
      };
      quad(rim, P(j / N, 0), P((j + 1) / N, 0), P((j + 1) / N, w), P(j / N, w));
    }
    k.add(paint(tris(rim), shell));
    k.add(paint(tris(vaultCap(sp - 2 * w, rise - w, 12, 0)), i === 0 ? blue : blue2, tf(fx, 0, z)));
    k.add(paint(tris(vaultCap(sp, rise, 12, 0, -1)), '#8ab0c8', tf(fx - len, 0, z))); // vidro para a lagoa
    // As figuras do painel (Portinari): manchas de azulejo claro e branco, poucas, dentro da parábola.
    for (let a = 0; a < 5; a++) {
      const zz = -sp / 2 + w + 0.8 + ((a + 0.5) / 5) * (sp - 2 * w - 1.6); const yy = 1 + hash2(i * 7 + a, 412) * rise * 0.45;
      const hh = 1.4 + hash2(i, a + 413) * 2.4;
      if (yy + hh > (rise - w) * (1 - (2 * (Math.abs(zz) + 0.8) / (sp - 2 * w)) ** 2)) continue;
      k.add(paint(box(0.2, hh, 1.2), hash2(a, i + 414) < 0.5 ? '#f4f6fa' : '#8fb4e6', tf(fx + 0.1, yy + hh / 2, z + zz)));
    }
  });
  // Torre sineira: trapézio invertido (mais larga no alto), com a cruz; marquise inclinada até as abóbadas.
  const tx = fx - 2; const tz = 36;
  k.add(paint(hip(2.4, 23, 3.4, 5.2, 6.2), shell, tf(tx, 0, tz)));
  k.add(paint(box(0.35, 3.2, 0.35), '#3a3a3a', tf(tx, 24.6, tz)), paint(box(0.35, 0.35, 1.8), '#3a3a3a', tf(tx, 25.4, tz)));
  k.add(beam([tx - 1, 6, tz - 2.5], [fx - 6, 4.6, 26], 0.5, shell, 5));
  for (const t of [0.35, 0.7]) k.add(paint(cyl(0.18, 0.18, 5.2, 5), shell, tf(tx - 1 + (fx - 6 - tx + 1) * t, 2.6, tz - 2.5 + (26 - tz + 2.5) * t)));
  // Luz no pé do painel (acende à noite).
  k.light(paint(box(0.3, 0.3, 58), '#fff0d0', tf(fx + 2.6, 0.3, 2)), paint(box(0.3, 0.3, 2), '#fff0d0', tf(tx + 2.4, 0.3, tz)));
  return k.model([0, 22], 0, 411);
}

/**
 * Cachoeira Casca d'Anta (Serra da Canastra): a escarpa de quartzito cinza em estratos, com mata nas saliências e no
 * talude e o campo dourado do chapadão em cima; a queda larga que se lança do vão da borda e abre em riscos até a
 * névoa, o poço e o começo do São Francisco. Era um bloco cinza (torre) entre dois morros com um fio d'água de 10 m,
 * que de quem chega nem aparecia (tests/landmarks-leitura-centro-sul-2.test.ts).
 */
function cascaDanta(): Model {
  const k = new Kit();
  const H = 140; const xw = -36;
  // A escarpa da Canastra: quartzito cinza em estratos, mata nas saliências e no talude, o campo dourado do chapadão
  // em cima; no meio, o vão que o rio cortou na borda (o topo baixa, a face recua).
  const wall = cliff({
    len: 380, H, strata: ['#8a8478', '#76706a', '#9a9386', '#6c665e', '#857f72'], layers: 6, seed: 221, cols: 26, depth: 90,
    batter: 0.05, gully: 9, gullyLen: 28, bay: 16, ragged: 0.05, ledge: 3, talus: 30, talusColor: '#3f6a3a', top: '#b4ad62',
    ledgeTop: '#5a7f3e', ends: 0.24, notch: { z: 0, half: 18, h: H - 12, recess: 7 },
  }, tf(xw, 0, 0));
  k.add(wall.geo);
  // As pontas da escarpa descem em encosta de mata (o corte, visto de lado, lia como rampa).
  for (const s of [-1, 1]) k.add(hill(48, 84, 58, 222 + s, '#3f6a3a', '#5a7f3e', 1, 0.12, 0.25, tf(xw - 22, 0, s * 178)));
  // Capões no chapadão, atrás da borda.
  for (let i = 0; i < 8; i++) {
    const z = -160 + i * 44 + hash2(i, 225) * 12;
    if (Math.abs(z) < 30) continue;
    k.add(blob(224 + i, i % 2 ? '#5f7a3a' : '#6f8a42', xw + wall.lipAt(z) - 16 - hash2(i, 226) * 24, wall.topAt(z) + 2.5, z, 9, 4, 9));
  }
  // A queda: lança do lábio do vão e abre em riscos (24 → 48 m) até a névoa, na frente do talude.
  const lip = xw + wall.lipAt(0); const top = wall.topAt(0);
  veil(k, 227, 0, lip + 6, xw + wall.footAt(0) + 22, top - 1, 6, 26, 50, 7, 3);
  // Névoa no pé (luz), o poço e o rio saindo para a frente entre pedras.
  const fx = xw + wall.footAt(0) + 30;
  for (let i = 0; i < 4; i++) k.light(paint(jitter(ico(1, 1), 0.14, 230 + i), MIST, tf(fx + (i % 2) * 6, 7 + (i >> 1) * 6, (i - 1.5) * 16, 18, 11, 18)));
  k.add(paint(cyl(30, 32, 0.6, 12), '#3f8ab0', tf(fx + 20, 0.3, 0, 1, 1, 1.3)));
  k.add(paint(box(40, 0.5, 14), '#4a90b4', tf(fx + 60, 0.25, 4)));
  for (let i = 0; i < 6; i++) k.add(paint(jitter(ico(1, 0), 0.25, 234 + i), '#6a6458', tf(fx + 45 + i * 6, 0.8, (i % 2 ? 11 : -3) + hash2(i, 228) * 3, 3, 1.6, 3)));
  // Mata no pé da escarpa, dos dois lados do poço.
  for (let i = 0; i < 10; i++) {
    const z = (i < 5 ? -1 : 1) * (48 + (i % 5) * 30);
    k.add(blob(240 + i, i % 2 ? '#2f6a33' : '#3f7a3a', xw + wall.footAt(z) + 26 + hash2(i, 241) * 10, 6, z, 14, 10, 14));
  }
  return k.model([-10, H], 0.04, 221);
}

/** Pedra Azul (ES, skyline): o domo de granito azulado de paredes quase verticais, o Lagarto e a Pedra das Flores. */
function pedraAzul(): Model {
  const k = new Kit();
  k.add(hill(310, 70, 270, 231, '#3f7038', '#4f8442', 1, 0.14, 1));
  k.add(tintUp(hill(125, 195, 110, 232, '#6a86ac', null, 2, 0.06, 0.3, tf(-40, 0, 160)), '#5a8a48', 0.55, 0.85));
  const p = hill(98, 330, 88, 233, '#5578b0', null, 2, 0.05, 0.3, tf(-20, 0, -40));
  tintUp(p, '#7f9fd0', 0.8, 0.6);
  k.add(p);
  // O Lagarto: a saliência alongada subindo pela face, mais escura. O azul é saturado (a cor do nome): com 0,15 de
  // saturação a pedra lia cinza na névoa.
  k.add(paint(jitter(ico(1, 1), 0.08, 234), '#46628e', tf(60, 200, -46, 12, 50, 16, 0, 0, 0.3)));
  k.add(paint(jitter(ico(1, 0), 0.08, 235), '#46628e', tf(45, 246, -48, 10, 13, 12)));
  // Mata no pé do domo.
  k.add(tintUp(hill(150, 55, 130, 236, '#2f6a33', '#3f7a3a', 1, 0.16, 0.4, tf(20, 0, -40)), '#3f7a3a', 0.3, 1));
  return k.model([0, 330], 0.04, 231);
}

/**
 * Dunas de Itaúnas: o campo de dunas de areia clara (barlavento suave, a face de avalanche íngreme para a pista), a
 * restinga no pé, a passarela de madeira subindo e a torre da igreja velha que a areia engoliu. A duna grande tem
 * 50 m (×1,6) e 210 m de crista: com 30 m em 260 m, a 210 m da pista era uma faixa bege rente ao chão.
 */
function dunasItaunas(): Model {
  const k = new Kit();
  const slip = '#d6b484';
  // A duna grande avançando para a pista (face de avalanche lisa e mais escura, crista viva) e duas de través nos
  // lados, que mostram o barlavento claro com as ondulações e a sombra da face de avalanche.
  const X0 = -60;
  const big = dune({ len: 210, H: 50, back: 125, seed: 241, sand: '#f2e2b4', slip, cols: 24, rows: 8, sinuous: 10, horns: 16 }, tf(X0, 0, 0));
  k.add(big.geo);
  k.add(dune({ len: 130, H: 27, back: 70, seed: 242, sand: '#efdcac', slip: '#d0ac7a', cols: 14, rows: 8, horns: 20, sinuous: 6 }, tf(X0 + 4, 0, -96, 1, 1, 1, 0, 0.95, 0)).geo);
  k.add(dune({ len: 110, H: 22, back: 56, seed: 243, sand: '#f4e6bc', slip, cols: 12, rows: 8, horns: 16, sinuous: 5 }, tf(X0 + 22, 0, 92, 1, 1, 1, 0, -1.0, 0)).geo);
  // A igreja velha que a areia engoliu, no pé da face de avalanche: a torre (sineira aberta, cornijas, coruchéu e
  // cruz) e o frontão da fachada saindo da areia, de frente para a pista.
  const cz = -22; const toe = X0 + big.toeAt(cz);
  const sandAt = (x: number): number => Math.max(0, (toe - x) / 1.6);
  const WALL = '#f4f1e8'; const TRIM = '#c9a46a';
  const tx = toe - 15; const tw = 5.6; const tTop = sandAt(tx - tw / 2) + 11;
  k.add(paint(box(tw, tTop + 1, tw), WALL, tf(tx, (tTop - 1) / 2, cz)));
  for (const y of [tTop - 5.2, tTop]) k.add(paint(box(tw + 0.7, 0.6, tw + 0.7), TRIM, tf(tx, y, cz)));
  // Sineira: vãos escuros em arco nas três faces que a pista vê.
  k.add(win(tx + tw / 2 + 0.05, tTop - 2.6, cz, 2.2, 3.4, '#1e2228'));
  for (const s of [-1, 1]) k.add(paint(box(2.2, 3.4, 0.25), '#1e2228', tf(tx, tTop - 2.6, cz + s * (tw / 2 + 0.05))));
  k.add(paint(hip(tw + 0.4, 4.6, tw + 0.4), TILE, tf(tx, tTop + 0.3, cz)));
  k.add(paint(box(0.35, 2.6, 0.35), '#4a3e32', tf(tx, tTop + 6, cz)), paint(box(0.35, 0.35, 1.6), '#4a3e32', tf(tx, tTop + 6.6, cz)));
  // Frontão da fachada ao lado da torre: o oitão com a janela do coro e as pilastras, enterrado até o meio.
  const fx = toe - 11; const fz = cz + tw / 2 + 5.4; const fw = 10; const fy = sandAt(fx) + 4.5;
  k.add(paint(box(1.4, fy + 1, fw), WALL, tf(fx, (fy - 1) / 2, fz)));
  k.add(paint(gable(fw + 0.6, 3.6, 1.4), WALL, tf(fx, fy, fz, 1, 1, 1, 0, Math.PI / 2, 0)));
  k.add(paint(box(1.6, 0.5, fw + 1), TRIM, tf(fx, fy, fz)));
  for (const s of [-1, 1]) k.add(paint(box(1.6, fy, 0.8), TRIM, tf(fx + 0.1, fy / 2, fz + s * (fw / 2 - 0.3))));
  k.add(win(fx + 0.75, fy - 1.6, fz, 1.6, 2.2, '#1e2228'));
  // Restinga (moitas e capim) no pé, do lado da pista.
  for (let i = 0; i < 16; i++) k.add(blob(244 + i, i % 3 ? '#5a8a3a' : '#7a9a44', toe + 8 + hash2(i, 245) * 22, 0.7, -124 + i * 16.5, 3 + hash2(i, 246) * 2.4, 1.5, 3.4));
  // Passarela de madeira do pé até o alto, sobre estacas.
  const pz = 58; const [cxp, cyp] = big.crestAt(pz);
  const p0: V3 = [toe + 14, 1.2, pz - 6]; const p1: V3 = [X0 + cxp + 4, cyp - 1.5, pz];
  k.add(beam(p0, p1, 2.2, WOOD, 0.3));
  for (let i = 0; i <= 6; i++) {
    const t = i / 6; const x = p0[0] + (p1[0] - p0[0]) * t; const y = p0[1] + (p1[1] - p0[1]) * t; const z = p0[2] + (p1[2] - p0[2]) * t;
    k.add(paint(box(0.25, 1.1, 0.25), '#5a3e26', tf(x, y + 0.5, z + 1)), paint(box(0.25, 1.1, 0.25), '#5a3e26', tf(x, y + 0.5, z - 1)));
  }
  return k.model([-2, 52], 0.03, 241);
}

// ───────────────────────────── Sul ─────────────────────────────

/**
 * Cataratas do Iguaçu, vistas de dentro da ferradura (a boca para +X, a pista): as quedas em dois degraus — cortinas
 * brancas riscadas que abrem para baixo, quase sem rocha à mostra —, a Garganta do Diabo no meio numa queda só com a
 * nuvem de névoa e o arco-íris, a mata fechada na borda de cima, a névoa cobrindo o pé, o rio e a passarela. Sem a
 * torre do elevador: de longe ela e os painéis retos liam como prédios (tests/landmarks-leitura.test.ts).
 */
function cataratasIguacu(): Model {
  const k = new Kit();
  const C: V3 = [40, 0, 0]; const R = 130; const N = 14; const A = 1.05;
  const H1 = 64; const H2 = 30; // topo do planalto e o degrau do meio
  const at = (r: number, a: number): [number, number] => [C[0] - r * Math.cos(a), r * Math.sin(a)];
  const basalt = '#46503a'; // basalto escuro tomado de musgo
  const greens = ['#2f6a33', '#3f7a3a', '#2a5e30', '#4a8a3c'];
  const tones = ['#ffffff', '#eaf5fc', '#d6eaf5', '#f4fbff', '#c8e2ef'];
  // Cortina d'água: do lábio (raio r0, altura yTop) até o pé (yBot), lançada para a frente no alto e abrindo para
  // baixo; faixas verticais de tons diferentes (os riscos) e duas fileiras (a barriga da queda).
  const curtain = (seed: number, a: number, r0: number, yTop: number, yBot: number, wTop: number, wBot: number, lip: number): void => {
    const out: number[] = [];
    const B = 5; const T = [0, 0.3, 1];
    const tx = Math.sin(a); const tz = Math.cos(a);
    const P = (s: number, t: number): V3 => {
      const r = r0 - lip * Math.sqrt(t); const w = wTop + (wBot - wTop) * t;
      const [x, z] = at(r, a);
      return [x + tx * s * w, yTop - (yTop - yBot) * t, z + tz * s * w];
    };
    const nx = Math.cos(a); const nz = -Math.sin(a); // para dentro da ferradura (para quem olha)
    for (let b = 0; b < B; b++) {
      const band: number[] = [];
      for (let j = 0; j < T.length - 1; j++) {
        const s0 = -0.5 + b / B; const s1 = -0.5 + (b + 1) / B;
        const p00 = P(s0, T[j]); const p10 = P(s1, T[j]); const p11 = P(s1, T[j + 1]); const p01 = P(s0, T[j + 1]);
        // Vira o quadrilátero para quem olha (o material de luz tem uma face só).
        const ux = p10[0] - p00[0]; const uy = p10[1] - p00[1]; const uz = p10[2] - p00[2];
        const vx = p01[0] - p00[0]; const vy = p01[1] - p00[1]; const vz = p01[2] - p00[2];
        const facing = (uy * vz - uz * vy) * nx + (ux * vy - uy * vx) * nz;
        if (facing >= 0) quad(band, p00, p10, p11, p01); else quad(band, p00, p01, p11, p10);
      }
      out.push(...band);
      k.light(paint(tris(band), tones[Math.floor(hash2(seed, b) * tones.length)]));
    }
  };
  // Rio na frente (verde-barrento, mas de água) com a espuma.
  k.add(paint(box(116, 0.6, 250), '#5d8a7a', tf(-56, 0.3, 0)));
  for (let i = 0; i < N; i++) {
    const a = -A + ((i + 0.5) / N) * 2 * A;
    const chord = 2 * R * Math.sin(A / N) + 2;
    const center = Math.abs(a) < 0.2;
    // Rocha atrás das quedas: o planalto (até H1) e o degrau do meio (até H2), quase toda escondida pela água.
    const [ux, uz] = at(R + 16, a);
    k.add(tintUp(rock(250 + i, basalt, tf(ux, H1 / 2, uz, 32, H1, chord, 0, a, 0), [1, 1, 1], 0.05), greens[i % 4], 0.6, 1));
    if (!center) {
      const [lx, lz] = at(R - 16, a);
      k.add(tintUp(rock(270 + i, basalt, tf(lx, H2 / 2, lz, 30, H2, chord, 0, a, 0), [1, 1, 1], 0.05), greens[(i + 1) % 4], 0.7, 1));
    }
    // Quedas: largas (a água é o que se vê); uma ou outra estreita, com a ilha de mata do lado.
    const narrow = hash2(i, 290) < 0.15 && !center;
    const w = chord * (center ? 1.0 : narrow ? 0.55 : 0.92 + hash2(i, 291) * 0.12);
    if (center) curtain(300 + i, a, R - 1, H1, 0, w, w * 1.25, 10);
    else {
      curtain(300 + i, a, R - 1, H1, H2 - 1, w, w * 1.15, 5);
      curtain(320 + i, a, R - 31, H2, 0, w * 0.95, w * 1.2, 4);
    }
    if (narrow) {
      const [bx, bz] = at(R - 4, a + (chord * 0.32) / R);
      k.add(paint(jitter(ico(1, 1), 0.18, 292 + i), greens[i % 4], tf(bx, H2 + 14, bz, 9, 16, chord * 0.25)));
    }
    // Mata fechada no alto da borda: copas redondas acima do lábio (a silhueta de cima é floresta).
    const [tx, tz] = at(R + 20, a); // atrás do lábio: a copa não cobre o alto da queda
    k.add(paint(jitter(ico(1, 1), 0.16, 340 + i), greens[i % 4], tf(tx, H1 + 3, tz, 18, 9, chord * 0.62)));
    if (!center) { // e no degrau do meio, entre as duas quedas
      const [mx, mz] = at(R - 22, a);
      k.add(paint(jitter(ico(1, 0), 0.2, 380 + i), greens[(i + 2) % 4], tf(mx, H2 + 0.5, mz, 7, 2.5, chord * 0.5)));
    }
  }
  // As pontas da ferradura, que se veem de lado: a encosta de mata desce do planalto até o rio (sem paredão à mostra).
  for (const s of [-1, 1]) {
    const [ex, ez] = at(R + 6, s * (A + 0.06));
    k.add(hill(16, H1 + 6, 16, s > 0 ? 361 : 362, '#3f6a36', greens[1], 1, 0.12, 0.25, tf(ex, 0, ez)));
  }
  // Névoa: a faixa branca que esconde o pé das quedas e a nuvem que sobe da Garganta.
  // Luz (sem sombra): de longe a névoa é uma mancha clara e macia; acesa pelo sol, virava pedra cinza.
  const MIST = '#e4eef2';
  for (let i = 0; i < 10; i++) {
    const a = -A - 0.12 + ((i + 0.5) / 10) * (2 * A + 0.24);
    const [mx, mz] = at(R - 44, a);
    k.light(paint(jitter(ico(1, 1), 0.14, 400 + i), MIST, tf(mx, 6, mz, 16, 13, 2 * (R - 44) * Math.sin(A / 10) + 12)));
  }
  for (const s of [-1, 1]) { // a névoa que o vento leva para o pé das pontas
    const [mx, mz] = at(R - 4, s * (A + 0.06));
    k.light(paint(jitter(ico(1, 1), 0.14, s > 0 ? 418 : 419), MIST, tf(mx, 3, mz, 20, 9, 22)));
  }
  for (let i = 0; i < 4; i++) {
    const [gx, gz] = at(R - 26 - i * 6, (hash2(i, 410) - 0.5) * 0.2);
    k.light(paint(jitter(ico(1, 1), 0.14, 412 + i), MIST, tf(gx, 12 + i * 9, gz, 12 - i, 9, 16 - i * 2)));
  }
  // Arco-íris na névoa da frente da Garganta.
  const bow = ['#e8483a', '#f29a2e', '#f2d23a', '#5ab84a', '#3a7ad8'];
  bow.forEach((c, b) => {
    const r = 34 - b * 1.4; const out: number[] = []; const S = 18;
    for (let s = 0; s < S; s++) {
      const t0 = (Math.PI * s) / S; const t1 = (Math.PI * (s + 1)) / S;
      const p = (t: number, rr: number): V3 => [-10, 6 + rr * Math.sin(t), -rr * Math.cos(t)];
      quad(out, p(t0, r), p(t1, r), p(t1, r - 1.4), p(t0, r - 1.4));
    }
    k.light(paint(tris(out), c));
  });
  // Passarela até a Garganta (lado brasileiro).
  k.add(beam([0, 4, 62], [-48, 4, 28], 2.6, '#cfcfc8', 0.5));
  for (let i = 0; i <= 4; i++) k.add(paint(box(0.6, 4, 0.6), '#9a9a94', tf(-i * 12, 2, 62 - i * 8.5)));
  return k.model([0, H1], 0.05, 251);
}

/**
 * Ponte da Amizade (Foz do Iguaçu): o arco de concreto de 300 m sobre o cânion do Paraná — grosso (9–13 m, a face de
 * baixo na sombra), encostando no tabuleiro no meio —, os montantes, os viadutos de acesso nas cabeceiras de rocha e os
 * postes acesos. O arco tinha 4 m: a 270 m sobrava o tabuleiro reto (tests/landmarks-leitura-centro-sul-2.test.ts).
 */
function ponteAmizade(): Model {
  const k = new Kit();
  const deckY = 62; const half = 280; const S = 150;
  const con = '#d6d2c8'; const under = '#a8a49a';
  // Cabeceiras de rocha avermelhada com mata e o rio no fundo do cânion.
  for (const s of [-1, 1]) k.add(tintUp(hill(74, deckY - 4, 120, 330 + s, '#8a5a3e', null, 1, 0.12, 0.4, tf(-6, 0, s * (S + 90))), '#4a8a3c', 0.5, 1));
  k.add(paint(box(70, 0.5, 2 * S - 20), '#4a7a6a', tf(0, 0.25, 0)));
  // O arco (o traço do lugar, ×2 na espessura: a 270 m, 4 m sumiam): costela de concreto de 9 m no fecho a 13 m nas
  // nascenças, 13 m de largura, encostando no tabuleiro no meio; a face de baixo mais escura (a sombra que desenha a curva).
  const T = (z: number): number => 9 + 4 * (z / S) ** 2;
  const A = deckY - 1.3 - T(0) / 2;
  const y = (z: number): number => A * (1 - (z / S) ** 2) + 4;
  const NS = 16;
  for (let i = 0; i < NS; i++) {
    const z0 = -S + (2 * S * i) / NS; const z1 = -S + (2 * S * (i + 1)) / NS;
    const t = T((z0 + z1) / 2);
    k.add(beam([0, y(z0), z0], [0, y(z1), z1], 13, con, t));
    k.add(beam([0, y(z0) - t / 2 - 0.3, z0], [0, y(z1) - t / 2 - 0.3, z1], 12, under, 0.8));
  }
  // Nascenças em sapatas de rocha.
  for (const s of [-1, 1]) k.add(paint(box(18, 8, 14), '#7a5a46', tf(0, 4, s * (S + 2))));
  // Montantes do arco ao tabuleiro (dois por linha).
  for (let z = -S + 18; z <= S - 18; z += 18) {
    const h = deckY - 1.3 - (y(z) + T(z) / 2);
    if (h < 2) continue;
    for (const x of [-4.5, 4.5]) k.add(paint(box(2.4, h, 2.4), con, tf(x, y(z) + T(z) / 2 + h / 2, z)));
  }
  // Tabuleiro, guarda-corpo e os pilares dos acessos.
  k.add(paint(box(17, 2.6, 2 * half), '#dedad0', tf(0, deckY, 0)));
  for (const x of [-8.3, 8.3]) k.add(paint(box(0.5, 1.2, 2 * half), '#9a9690', tf(x, deckY + 1.8, 0)));
  for (const s of [-1, 1]) for (const z of [S + 30, S + 75]) for (const x of [-5, 5]) k.add(paint(box(3.4, deckY + 10, 3.4), con, tf(x, deckY / 2 - 5, s * z)));
  for (let z = -half + 15; z < half; z += 45) for (const x of [-8.6, 8.6]) k.light(paint(box(0.5, 0.5, 0.5), LAMP, tf(x, deckY + 5, z)), paint(box(0.2, 3.6, 0.2), '#5a5a5a', tf(x, deckY + 3, z)));
  return k.model([-4, deckY], 0, 331);
}

/**
 * Ópera de Arame (Curitiba): o teatro de tubos de aço e vidro na pedreira alagada — a gaiola de tubos brancos grossos
 * sobre o vidro escuro, cúpula de nervuras e a lanterna —, a ponte de entrada sobre o lago e o paredão da pedreira em
 * estratos, mais baixo que o teatro, com mata. Os tubos acendem à noite. Era um cilindro de vidro claro com tubos de
 * 0,36 m diante de um paredão de blocos mais alto que ele: lia como estufa (tests/landmarks-leitura-centro-sul-2.test.ts).
 */
function operaDeArame(): Model {
  const k = new Kit();
  // ×1,3: o teatro passa da pedreira. A gaiola de tubos brancos grossos (o traço do lugar) sobre vidro escuro — de
  // dia o vidro mostra o escuro de dentro e os tubos riscam por cima; à noite os tubos acendem (luz).
  const R = 16; const Hw = 14; const Hd = 10; const n = 28; const y0 = 1.4;
  const tube = '#f4f6fa';
  k.add(paint(cyl(R + 16, R + 16, 0.3, 20), '#2f6f78', tf(0, 0.15, 0, 1, 1, 1.25)));
  k.add(paint(cyl(R + 0.8, R + 1, y0, 20), '#9a948a', tf(0, y0 / 2, 0)));
  // Vidro escuro (o interior) e a cúpula.
  k.add(paint(cyl(R - 0.3, R - 0.3, Hw, 14, true), '#34474e', tf(0, y0 + Hw / 2, 0)));
  k.add(paint(sphere(R - 0.3, 14, 4, 0, Math.PI * 2, 0, Math.PI / 2), '#3e555c', tf(0, Hw + y0, 0, 1, Hd / R, 1)));
  // Tubos: pilares grossos, anéis, as nervuras da cúpula e os X das faces (luz).
  for (let i = 0; i < n; i++) {
    const a = (i / n) * Math.PI * 2; const c = Math.cos(a); const s = Math.sin(a);
    k.light(paint(cyl(0.45, 0.45, Hw, 4, true), tube, tf(c * R, y0 + Hw / 2, s * R)));
    let prev: V3 = [c * R, Hw + y0, s * R];
    for (let j = 1; j <= 3; j++) {
      const t = (j / 3) * Math.PI / 2;
      const p: V3 = [c * R * Math.cos(t) * (j === 3 ? 0.12 : 1), Hw + y0 + Hd * Math.sin(t), s * R * Math.cos(t) * (j === 3 ? 0.12 : 1)];
      k.light(cable(prev, p, 0.7, tube));
      prev = p;
    }
  }
  for (const y of [y0 + 0.3, y0 + Hw * 0.36, y0 + Hw * 0.7, y0 + Hw]) k.light(paint(cyl(R + 0.3, R + 0.3, 0.6, n, true), tube, tf(0, y, 0)));
  k.light(paint(cyl(R * 0.72, R * 0.72, 0.5, n, true), tube, tf(0, Hw + y0 + Hd * 0.7, 0)));
  // A lanterna do topo.
  k.light(paint(cyl(1.6, 2, 2.4, 8, true), tube, tf(0, Hw + Hd + y0 + 1, 0)));
  k.add(paint(cone(2.2, 2.2, 8), '#d8dce2', tf(0, Hw + Hd + y0 + 3.3, 0)));
  // Ponte de entrada de tubos, sobre o lago.
  const bx0 = R + 0.5; const bx1 = R + 17;
  k.add(paint(box(bx1 - bx0, 0.5, 3.6), '#8a8a86', tf((bx0 + bx1) / 2, 1.4, 0)));
  for (const s of [-1, 1]) {
    k.light(paint(box(bx1 - bx0, 0.3, 0.3), tube, tf((bx0 + bx1) / 2, 2.8, s * 1.8)));
    for (let i = 0; i <= 5; i++) k.light(paint(cyl(0.18, 0.18, 1.4, 4, true), tube, tf(bx0 + i * (bx1 - bx0) / 5, 2.1, s * 1.8)));
    k.add(paint(box(0.5, 1.6, 0.5), '#8a8a86', tf(bx0 + 3, 0.6, s * 1.5)), paint(box(0.5, 1.6, 0.5), '#8a8a86', tf(bx1 - 3, 0.6, s * 1.5)));
  }
  // Pedreira: o paredão em arco atrás, mais baixo que o teatro (em estratos, não em blocos), mata no alto e a cascatinha.
  const r = R + 26;
  for (let i = 0; i < 3; i++) {
    const a = Math.PI * (0.62 + i * 0.38);
    const q = cliff({
      len: 34, H: 15, strata: ['#8a7f70', '#7a705f', '#958a78'], layers: 4, seed: 340 + i, cols: 6, depth: 14, batter: 0.08,
      gully: 1.6, ragged: 0.12, ledge: 0.7, talus: 2.5, talusColor: '#4f6a3a', top: '#3f7a3a', ledgeTop: '#4f7a3a',
      ends: i === 0 ? [0, 0.3] : i === 2 ? [0.3, 0] : 0,
    }, tf(Math.cos(a) * r, 0, Math.sin(a) * r, 1, 1, 1, 0, Math.PI - a, 0));
    k.add(q.geo);
  }
  for (let i = 0; i < 6; i++) {
    const a = Math.PI * 0.62 + (i / 5) * Math.PI * 0.76;
    k.add(blob(350 + i, i % 2 ? '#2f6a33' : '#3f7a3a', Math.cos(a) * (r + 5), 16, Math.sin(a) * (r + 5), 8, 3.5, 8));
  }
  k.light(paint(box(0.5, 13, 2.6), '#dff0ff', tf(-r + 1.5, 7, 0)));
  return k.model([0, 26], 0.03, 341);
}

/**
 * Prédios de Balneário Camboriú (skyline): as torres residenciais mais altas do país em fileira na orla, com as
 * coroas que acendem à noite, as torres gêmeas ligadas no alto, e os morros de mata atrás.
 */
function prediosCamboriu(): Model {
  const k = new Kit();
  k.add(hill(230, 130, 420, 361, '#2a5a30', '#3a6a38', 1, 0.14, 0.6, tf(-280, 0, 0)));
  const towers: Array<[number, number, number, number, number, string]> = [
    // z, x, largura (Z), profundidade (X), altura, cor
    [-210, 0, 26, 24, 150, '#e8e4da'], [-170, -30, 22, 22, 205, '#cfd8e0'], [-128, 4, 24, 22, 262, '#f0ece2'],
    [-92, -26, 20, 20, 180, '#d8d0c4'], [-56, 6, 24, 24, 290, '#c8d4e0'], [-18, -22, 22, 20, 235, '#ece6da'],
    [22, 4, 20, 22, 281, '#dfe6ee'], [52, 4, 20, 22, 281, '#dfe6ee'], [90, -28, 22, 22, 196, '#f0ece2'],
    [126, 2, 24, 24, 238, '#d8d0c4'], [166, -20, 20, 20, 170, '#cfd8e0'], [204, 6, 24, 22, 140, '#ece6da'],
    [-150, -70, 26, 26, 120, '#d0ccc0'], [-40, -70, 26, 26, 110, '#d8d4c8'], [70, -70, 26, 26, 125, '#d0ccc0'], [180, -64, 26, 26, 100, '#d8d4c8'],
  ];
  const crowns = ['#ff4fa8', '#4fc8ff', '#ffd24a', '#8a5aff', '#4fffb0'];
  towers.forEach(([z, x, w, d, h, color], i) => {
    k.facade('apartment', facadeBox('apartment', d, h, w, color, tf(x, h / 2, z), hash2(i, 362) * 12));
    // Coroa: degrau recuado e o topo; as altas têm agulha e luz de topo.
    k.add(paint(box(d * 0.8, 8, w * 0.8), '#bfc4c8', tf(x, h + 4, z)));
    if (h > 220) {
      k.add(paint(hip(d * 0.7, 18, w * 0.7), '#d8dce0', tf(x, h + 8, z)), paint(cyl(0.6, 0.8, 22, 5), '#c8ccd0', tf(x, h + 30, z)));
      k.blink(paint(sphere(1.6, 6, 4), '#ff3030', tf(x, h + 42, z)));
    }
    k.light(paint(box(d * 0.82, 1.6, w * 0.82), crowns[i % crowns.length], tf(x, h + 7.2, z)));
  });
  // As gêmeas ligadas no alto (ponte de dois andares).
  k.facade('apartment', facadeBox('apartment', 18, 10, 12, '#dfe6ee', tf(4, 262, 37)));
  return k.model([0, 300], 0, 361);
}

/**
 * Roda-gigante de Balneário Camboriú (65 m): dois aros de aço com os raios, 32 gôndolas fechadas, os cavaletes em A
 * dos dois lados do eixo e o aro de luz colorida que acende à noite.
 */
function rodaGigante(): Model {
  const k = new Kit();
  const R = 31; const ax = 35; const wx = 3.2; const N = 32;
  const steel = '#e6e8ec';
  const P = (x: number, r: number, i: number): V3 => [x, ax + r * Math.cos((i / N) * Math.PI * 2), r * Math.sin((i / N) * Math.PI * 2)];
  k.add(paint(box(22, 1.2, 46), '#a8a8a4', tf(0, 0.6, 0)));
  // Cavaletes: duas pernas de cada lado, abertas em Z e em X.
  for (const s of [-1, 1]) for (const zs of [-1, 1]) k.add(beam([s * (wx + 8), 0.8, zs * 17], [s * (wx + 1.4), ax, 0], 1.7, steel, 1.3));
  for (const s of [-1, 1]) k.add(paint(box(1, 1, 30), steel, tf(s * (wx + 6.6), 8.5, 0)));
  k.add(paint(cyl(2.2, 2.2, 2 * wx + 4, 10), '#c8ccd0', tf(0, ax, 0, 1, 1, 1, 0, 0, Math.PI / 2)));
  for (const s of [-1, 1]) {
    for (let i = 0; i < N; i++) {
      k.add(beam(P(s * wx, R, i), P(s * wx, R, i + 1), 0.9, steel, 1.1));
      const c = new THREE.Color().setHSL(i / N, 0.85, 0.6);
      k.light(cable(P(s * (wx + 0.7), R - 0.4, i), P(s * (wx + 0.7), R - 0.4, i + 1), 0.45, c));
      if (i % 2 === 0) k.add(cable([s * (wx + 0.5), ax, 0], P(s * wx, R, i), 0.35, steel));
    }
  }
  // Gôndolas penduradas entre os aros, com a faixa de vidro (acende à noite).
  for (let i = 0; i < N; i++) {
    const p = P(0, R, i);
    k.add(paint(box(2 * wx, 0.35, 0.35), steel, tf(0, p[1], p[2])));
    k.add(paint(box(3.4, 2.8, 3), '#f4f4f2', tf(0, p[1] - 2.2, p[2])));
    k.light(paint(box(3.5, 1.0, 3.1), new THREE.Color().setHSL((i * 5) / N, 0.6, 0.72), tf(0, p[1] - 2.0, p[2])));
  }
  // Bilheteria.
  k.add(paint(box(5, 3, 7), '#2f6fb0', tf(7, 2.7, 16)), paint(box(5.6, 0.4, 7.6), WHITE, tf(7, 4.4, 16)));
  return k.model([0, ax + R + 1], 0, 371);
}

/**
 * Serra do Rio do Rastro (skyline): a escarpa de mata de 260 m, de borda quebrada, com o paredão de rocha no alto e a
 * estrada em zigue-zague que sobe a face — asfalto claro e largo com os grampos, iluminada à noite (a imagem famosa) —
 * e o mirante na borda do campo. A estrada era uma linha de 4 m quase do tom da mata: a 600 m sobrava uma caixa verde
 * (tests/landmarks-leitura-centro-sul-2.test.ts).
 */
function miranteRioDoRastro(): Model {
  const k = new Kit();
  const H = 260; const L = 700; const XB = -260;
  const front = (y: number): number => 120 - (140 * Math.max(0, y)) / H;
  const ripple = (z: number): number => 14 * Math.sin(z / 55);
  const g = new THREE.BoxGeometry(1, 1, 1, 3, 8, 14);
  const p = g.attributes.position as THREE.BufferAttribute;
  for (let i = 0; i < p.count; i++) {
    const u = p.getX(i) + 0.5; const v = p.getY(i) + 0.5; const w = p.getZ(i);
    // A borda de cima quebrada (picos e selas da serra, não uma régua) e as pontas baixando.
    const z = w * L; const edge = 1 - 0.45 * Math.max(0, Math.abs(w) * 2 - 0.6) / 0.4;
    const crest = (H + 20) * edge + (v > 0.99 ? 18 * Math.sin(z / 41 + 1.3) + 10 * Math.sin(z / 17) : 0);
    const y = v * crest - 20;
    const xf = front(y) + ripple(z);
    p.setXYZ(i, XB + u * (xf - XB), y, z);
  }
  const massif = paint(g, '#3f6a38');
  tintUp(massif, '#8a9a58', 0.85, 1);
  k.add(massif);
  k.add(rock(381, '#7c7a70', tf(-12, 226, 0, 34, 52, L * 0.62), [1, 2, 8], 0.12));
  for (const s of [-1, 1]) k.add(hill(160, 190, 130, 382 + s, '#3f6a38', '#7f9a52', 1, 0.12, 0.3, tf(-50, 0, s * 360)));
  // Estrada em zigue-zague pela face (o traço do lugar): asfalto claro e largo (10 m na vertical, ×3: a 600 m a
  // pista de verdade some), a faixa de luz por cima (a foto famosa é à noite) e os grampos nas pontas.
  const legs = 9; const sub = 4; const ROAD = '#c8c6c0';
  const zEnd = (j: number): number => (j % 2 ? 1 : -1) * (120 + hash2(j, 383) * 40);
  const roadAt = (y: number, z: number): V3 => [front(y) + ripple(z) + 5, y, z];
  for (let j = 0; j < legs; j++) {
    const y0 = 12 + j * 26; const y1 = y0 + 26; const za = zEnd(j); const zb = zEnd(j + 1);
    for (let s = 0; s < sub; s++) {
      const t0 = s / sub; const t1 = (s + 1) / sub;
      const a = roadAt(y0 + (y1 - y0) * t0, za + (zb - za) * t0); const b = roadAt(y0 + (y1 - y0) * t1, za + (zb - za) * t1);
      k.add(beam(a, b, 7, ROAD, 10));
      k.light(cable([a[0] + 3.8, a[1] + 3, a[2]], [b[0] + 3.8, b[1] + 3, b[2]], 2.4, '#ffe2a0'));
    }
    // O grampo: a curva fechada na ponta, um degrau largo do mesmo asfalto.
    const e = roadAt(y1, zb);
    k.add(paint(cyl(9, 9, 10, 8), ROAD, tf(e[0] - 2, e[1], e[2] + Math.sign(zb) * 3)));
  }
  // Mirante na borda de cima: plataforma, guarda-corpo e a luz.
  const mx = front(H) + 6;
  k.add(paint(box(18, 4, 36), '#b8b4aa', tf(mx, H - 1, 60)), paint(box(0.6, 1.6, 36), '#5a5a5a', tf(mx + 9, H + 1.8, 60)));
  k.light(paint(box(1, 1, 30), '#ffe2a0', tf(mx + 8, H + 3, 60)));
  return k.model([0, H], 0.05, 381);
}

/**
 * Usina do Gasômetro (Porto Alegre): o prédio de tijolo à vista com as janelas em arco e os pilares brancos, a
 * chaminé de 107 m e o cais de pedra na beira do Guaíba (o lago fica atrás).
 */
function usinaGasometro(): Model {
  const k = new Kit();
  const brick = '#b8603a'; const trim = '#efe6d4';
  k.add(paint(box(64, 7, 100), '#8d8a82', tf(4, -3, 0)));
  // Corpo principal, a nave alta com lanternim e o anexo mais baixo.
  k.add(paint(box(34, 22, 74), brick, tf(-2, 11.5, -6)));
  k.add(paint(gable(34, 5, 74, 0.6), '#6a5e58', tf(-2, 22.5, -6)), paint(box(10, 3.4, 60), '#7a6e66', tf(-2, 26.5, -6)));
  k.add(paint(box(26, 14, 26), brick, tf(-6, 7.5, 44)), paint(box(27, 1, 27), trim, tf(-6, 14.6, 44)));
  // Fachada para a pista: pilastras brancas, janelas altas em arco (algumas acesas), cimalha.
  const fx = 15.05;
  for (let i = 0; i <= 9; i++) k.add(paint(box(0.6, 22, 1.4), trim, tf(fx + 0.1, 11.5, -42 + i * 8)));
  k.add(paint(box(0.8, 1.2, 75), trim, tf(fx + 0.2, 22, -6)), paint(box(0.8, 1, 75), trim, tf(fx + 0.2, 11.6, -6)));
  for (let i = 0; i < 9; i++) for (const [y, h] of [[5.4, 7.6], [16.6, 7.6]] as Array<[number, number]>) {
    const z = -38 + i * 8;
    k.add(win(fx + 0.1, y, z, 3.2, h, '#2a3038'), paint(cyl(1.6, 1.6, 0.3, 8), trim, tf(fx + 0.12, y + h / 2, z, 1, 1, 1, 0, 0, Math.PI / 2)));
    if (hash2(i, y) < 0.45) k.light(paint(box(0.2, h * 0.7, 2.4), '#ffcf80', tf(fx + 0.3, y, z)));
  }
  // Chaminé de tijolo afinando, anéis brancos, coroamento e a luz de topo.
  const cx = -4; const cz = 30;
  k.add(paint(box(10, 10, 10), brick, tf(cx, 5.5, cz)));
  k.add(lathe([[4.6, 0], [4.0, 40], [3.4, 80], [3.0, 104], [3.5, 105], [3.5, 107], [2.8, 107]], 10, '#b05a36', tf(cx, 10, cz)));
  for (const [y, r] of [[30, 4.2], [62, 3.75], [92, 3.25]] as Array<[number, number]>) k.add(paint(cyl(r, r + 0.05, 1.6, 10), trim, tf(cx, 10 + y, cz)));
  k.blink(paint(sphere(0.8, 6, 4), '#ff3030', tf(cx, 118, cz)));
  return k.model([0, 30], 0.03, 391);
}

/**
 * Cânion do Itaimbezinho (Aparados da Serra, skyline): a escarpa de basalto a pique dos dois lados da boca do
 * cânion — derrames empilhados com a mata nas prateleiras, sulcos, a borda quebrada e o campo de cima com os capões
 * de araucária —, a garganta estreita entrando no planalto (paredões de dentro, a cabeceira ao fundo, a mata
 * escura no fundo), o Véu de Noiva e a cascata das Andorinhas despencando da borda e a mata atlântica no pé.
 */
function canionItaimbezinho(): Model {
  const k = new Kit();
  // A boca abre em V para quem chega (240 m entre as escarpas) e a garganta afunila até ~60 m na cabeceira.
  const H = 230; const gap = 240; const back = 520; const len = 470; const zf = 70; const zb = 30;
  const basalt = ['#5c5650', '#6e6153', '#4e4a46', '#7a6656', '#625a52'];
  const base = { strata: basalt, layers: 5, H, batter: 0.06, gully: 34, gullyLen: 70, ragged: 0.08, ledge: 8, talus: 46, talusColor: '#35583a', top: '#a4b46a', ledgeTop: '#4f7040' } as const;
  // Escarpa da frente (os Aparados): um bloco de cada lado da boca, corte reto do lado da boca.
  for (const s of [-1, 1]) {
    const c = cliff({ ...base, len, seed: 402 + s, cols: 15, depth: back, bay: 60, ends: s < 0 ? [0.3, 0] : [0, 0.3] }, tf(0, 0, s * (gap / 2 + len / 2)));
    k.add(c.geo);
    // Capões de araucária no campo de cima, perto da borda.
    for (let i = 0; i < 5; i++) {
      const z = s * (gap / 2 + 50 + i * 85 + hash2(411, i + s) * 30);
      k.add(blob(410 + i + s * 10, i % 2 ? '#2f5a34' : '#3a6638', c.lipAt(z - s * (gap / 2 + len / 2)) - 30 - hash2(412, i) * 140, H + 5, z, 14 + hash2(413, i) * 8, 8, 14 + hash2(414, i) * 8));
    }
  }
  // Paredões de dentro da garganta (a face olha para o meio dela), da boca até a cabeceira.
  const inner: Array<{ m: THREE.Matrix4; c: ReturnType<typeof cliff> }> = [];
  for (const s of [-1, 1]) {
    const flare = Math.atan((zf - zb) / back);
    const m = tf(-back / 2, 0, s * (zf + zb) / 2, 1, 1, 1, 0, s * (Math.PI / 2 - flare), 0);
    const c = cliff({ ...base, len: back / Math.cos(flare), seed: 405 + s, cols: 10, depth: 90, bay: 14, talus: 30, ends: s < 0 ? [0, 0.12] : [0.12, 0] }, m);
    k.add(c.geo);
    inner.push({ m, c });
  }
  // A cabeceira ao fundo da garganta e a mata escura lá embaixo.
  k.add(cliff({ ...base, len: zb * 4, seed: 408, cols: 6, depth: 80, bay: 6, ends: 0 }, tf(-back * 0.92, 0, 0)).geo);
  k.add(hill(back * 0.5, 34, zf + 10, 421, '#203f26', '#264a2c', 1, 0.15, 0.4, tf(-back * 0.45, 0, 0)));
  // Véu de Noiva (paredão de dentro, à esquerda) e Andorinhas (à direita): fios brancos da borda ao fundo.
  const fall = (j: number, zl: number, w: number): void => {
    const { m, c } = inner[j];
    const top = new THREE.Vector3(c.lipAt(zl) + 1.5, c.topAt(zl) - 1, zl).applyMatrix4(m);
    const foot = new THREE.Vector3(c.footAt(zl) + 14, 40, zl).applyMatrix4(m);
    k.light(beam([top.x, top.y, top.z], [foot.x, foot.y, foot.z], w, WATER_FALL, 2));
    k.add(paint(jitter(ico(1, 0), 0.2, 415 + j), '#eef4f6', tf(foot.x, foot.y - 4, foot.z, 14, 9, 14)));
  };
  fall(0, -back * 0.22, 9); fall(1, back * 0.1, 6);
  // Mata atlântica no pé da escarpa.
  k.add(hill(300, 40, 640, 422, '#2f5a32', '#3a6a38', 1, 0.12, 0.4, tf(60, 0, 0)));
  return k.model([0, H], 0.04, 401);
}

// ───────────────────────────── Centro-Oeste ─────────────────────────────

/**
 * Ponte JK (Lago Paranoá): os três arcos de aço assimétricos que saltam o tabuleiro na diagonal — com 5,6 m de altura
 * de seção (×2: a 250 m, 2,6 m clareavam contra o céu) —, os estais em leque de cada arco até as duas bordas, os
 * pilares na água e os arcos acesos à noite.
 */
function ponteJk(): Model {
  const k = new Kit();
  const deckY = 12; const half = 420; const span = 240; const top = 61;
  const steel = '#f2f2ee';
  k.add(paint(box(24, 2.4, 2 * half), '#d8d6d0', tf(0, deckY, 0)));
  for (const x of [-12.3, 12.3]) k.add(paint(box(0.5, 1.1, 2 * half), '#9a9a96', tf(x, deckY + 1.7, 0)));
  for (let z = -half + 20; z < half; z += 40) k.add(paint(box(5, deckY + 2, 3), '#c8c6c0', tf(0, deckY / 2 - 1, z)));
  for (let a = 0; a < 3; a++) {
    const zc = (a - 1) * span;
    const sx = a % 2 ? -1 : 1;
    const A: V3 = [sx * 15, 0, zc - span / 2]; const B: V3 = [-sx * 15, 0, zc + span / 2];
    const P = (t: number): V3 => [A[0] + (B[0] - A[0]) * t, top * 4 * t * (1 - t) ** 1.08, A[2] + (B[2] - A[2]) * t];
    const NS = 12;
    for (let i = 0; i < NS; i++) {
      const p0 = P(i / NS); const p1 = P((i + 1) / NS);
      k.add(beam(p0, p1, 4.6, steel, 5.6)); // ×2 na espessura: a 250 m, 4 m clareavam contra o céu
      k.light(cable([p0[0], p0[1] + 3.2, p0[2]], [p1[0], p1[1] + 3.2, p1[2]], 1.1, '#e8f0ff'));
    }
    for (const t of [0, 1]) { const p = P(t); k.add(paint(box(9, 6, 9), '#c8c6c0', tf(p[0], 1, p[2]))); }
    // Estais: de cada ponto do arco às duas bordas do tabuleiro (cruzam, como na ponte de verdade).
    for (let i = 2; i <= 10; i++) {
      const p = P(i / 12);
      if (p[1] < deckY + 4) continue;
      k.add(cable(p, [11.5, deckY + 1, p[2] + 6], 0.25, '#d8d8d4'), cable(p, [-11.5, deckY + 1, p[2] - 6], 0.25, '#d8d8d4'));
    }
  }
  for (let z = -half + 30; z < half; z += 60) for (const x of [-12.6, 12.6]) k.light(paint(box(0.5, 0.5, 0.5), LAMP, tf(x, deckY + 4.4, z)));
  return k.model([0, top], 0, 431);
}

/**
 * Torre de TV de Brasília (224 m): a base de concreto de quatro pernas, o mirante envidraçado a 75 m, a torre
 * treliçada de aço até a antena, as luzes de balizamento e as barracas da feira no pé.
 */
function torreTvBrasilia(): Model {
  const k = new Kit();
  const con = '#d8d4c8'; const steel = '#9aa0a8';
  k.add(paint(cyl(46, 46, 0.4, 16), '#c8c2b4', tf(0, 0.2, 0)));
  // Pernas de concreto e os quadros de travamento.
  const legs: V3[] = [];
  for (const sx of [-1, 1]) for (const sz of [-1, 1]) {
    k.add(beam([sx * 18, 0, sz * 18], [sx * 6, 60, sz * 6], 3.2, con));
    legs.push([sx, 0, sz]);
  }
  for (const [y, r] of [[22, 13.6], [44, 9.2]] as Array<[number, number]>) {
    for (const s of [-1, 1]) k.add(paint(box(2 * r, 1.6, 1.6), con, tf(0, y, s * r)), paint(box(1.6, 1.6, 2 * r), con, tf(s * r, y, 0)));
  }
  // Mirante: caixa com a faixa de vidro (acende), laje e o restaurante.
  k.add(paint(box(20, 2, 20), con, tf(0, 60, 0)));
  k.add(paint(box(18, 6, 18), '#5a6a7a', tf(0, 64, 0)));
  k.light(paint(box(18.4, 2.2, 18.4), '#ffe8c0', tf(0, 64, 0)));
  k.add(paint(box(22, 1.6, 22), con, tf(0, 67.8, 0)), paint(box(12, 7, 12), con, tf(0, 72, 0)));
  // Torre de aço: quatro montantes afinando e os X de cada face, por andar.
  const yb = 75; const yt = 200;
  const half = (y: number): number => 6 + (1.6 - 6) * ((y - yb) / (yt - yb));
  for (const sx of [-1, 1]) for (const sz of [-1, 1]) k.add(beam([sx * half(yb), yb, sz * half(yb)], [sx * half(yt), yt, sz * half(yt)], 0.9, steel));
  const levels = 7;
  for (let l = 0; l < levels; l++) {
    const y0 = yb + ((yt - yb) * l) / levels; const y1 = yb + ((yt - yb) * (l + 1)) / levels;
    const h0 = half(y0); const h1 = half(y1);
    for (const s of [-1, 1]) {
      k.add(cable([s * h0, y0, -h0], [s * h1, y1, h1], 0.35, steel), cable([s * h0, y0, h0], [s * h1, y1, -h1], 0.35, steel));
      k.add(cable([-h0, y0, s * h0], [h1, y1, s * h1], 0.35, steel), cable([h0, y0, s * h0], [-h1, y1, s * h1], 0.35, steel));
    }
    k.add(paint(box(2 * h1, 0.5, 2 * h1), steel, tf(0, y1, 0)));
  }
  k.add(paint(cyl(0.5, 0.9, 24, 6), '#c8ccd0', tf(0, yt + 12, 0)));
  // Luzes: as quinas da torre de aço acesas e o balizamento vermelho.
  k.light(cable([half(yb) + 0.6, yb, 0], [half(yt) + 0.6, yt, 0], 0.5, '#d8e8ff'), cable([-half(yb) - 0.6, yb, 0], [-half(yt) - 0.6, yt, 0], 0.5, '#d8e8ff'));
  for (const y of [120, 170, yt + 24]) k.blink(paint(sphere(1.1, 6, 4), '#ff3030', tf(0, y + 0.5, y > yt ? 0 : half(y) + 0.8)));
  // Feira da Torre: barracas coloridas em volta da base.
  for (let i = 0; i < 8; i++) {
    const a = (i / 8) * Math.PI * 2 + 0.2; const r = 32;
    k.add(paint(box(4, 2.4, 4), '#efe8d8', tf(Math.cos(a) * r, 1.2, Math.sin(a) * r)), paint(hip(5, 2, 5), ['#e8402a', '#2a7ad8', '#f2c230', '#3aa84a'][i % 4], tf(Math.cos(a) * r, 2.4, Math.sin(a) * r)));
  }
  return k.model([0, 90], 0, 441);
}

/**
 * Matriz de Pirenópolis: fachada branca larga com frontão triangular e cimalhas ocre, duas torres baixas de telhado
 * piramidal, portas de madeira e o cruzeiro no adro de pedra.
 */
function igrejaPirenopolis(): Model {
  const k = new Kit();
  const ochre = '#c8963a'; const blue = '#2f5f9a'; const fx = 10;
  k.add(paint(box(48, 0.7, 34), STONE, tf(2, 0.15, 0)));
  k.add(paint(box(30, 12, 18), WHITE, tf(fx - 16, 6, 0)), paint(gable(18, 6, 30, 0.6), TILE, tf(fx - 16, 12, 0, 1, 1, 1, 0, Math.PI / 2, 0)));
  k.add(paint(box(10, 9, 12), WHITE, tf(fx - 35, 4.5, 0)), paint(gable(12, 4, 10, 0.5), TILE, tf(fx - 35, 9, 0, 1, 1, 1, 0, Math.PI / 2, 0)));
  // Fachada e o frontão.
  k.add(paint(box(1.6, 13, 18.4), WHITE, tf(fx, 6.5, 0)));
  k.add(paint(gable(18.4, 5.6, 1.6, 0), WHITE, tf(fx, 13, 0, 1, 1, 1, 0, Math.PI / 2, 0)));
  k.add(beam([fx + 0.85, 13, -9.4], [fx + 0.85, 18.7, 0], 0.6, ochre), beam([fx + 0.85, 13, 9.4], [fx + 0.85, 18.7, 0], 0.6, ochre));
  k.add(paint(box(0.7, 0.7, 19.2), ochre, tf(fx + 0.85, 13, 0)), paint(box(0.7, 0.7, 19.2), ochre, tf(fx + 0.85, 0.6, 0)));
  for (const z of [-8.8, -3.2, 3.2, 8.8]) k.add(paint(box(0.7, 13, 0.7), ochre, tf(fx + 0.85, 6.5, z)));
  k.add(paint(cyl(1.1, 1.1, 0.4, 8), ochre, tf(fx + 0.85, 15.4, 0, 1, 1, 1, 0, 0, Math.PI / 2)), win(fx + 1.05, 15.4, 0, 1.2, 1.2, '#2a3038'));
  k.add(paint(box(0.4, 3, 0.4), '#3a3a3a', tf(fx, 20.2, 0)), paint(box(0.4, 0.4, 1.8), '#3a3a3a', tf(fx, 20.8, 0)));
  // Três portas de madeira com moldura ocre e as janelas do coro (folhas azuis).
  for (const z of [-6, 0, 6]) {
    const big = z === 0;
    k.add(paint(box(0.35, big ? 5.4 : 4.4, big ? 3.4 : 2.6), ochre, tf(fx + 0.85, big ? 2.7 : 2.2, z)), win(fx + 1.05, big ? 2.5 : 2.0, z, big ? 2.6 : 1.9, big ? 4.8 : 3.8, '#6a3e22'));
    k.add(win(fx + 0.95, 9.2, z, 1.4, 2.4, '#2a3038'), paint(box(0.25, 2.4, 0.5), blue, tf(fx + 1.0, 9.2, z - 1.0)), paint(box(0.25, 2.4, 0.5), blue, tf(fx + 1.0, 9.2, z + 1.0)));
  }
  // Torres laterais, recuadas, com a sineira e o telhado piramidal de barro.
  for (const s of [-1, 1]) {
    const tz = s * 11.6;
    k.add(paint(box(5, 17, 5), WHITE, tf(fx - 1.6, 8.5, tz)));
    for (const dz of [-2.3, 2.3]) k.add(paint(box(0.6, 17, 0.6), ochre, tf(fx + 0.9, 8.5, tz + dz)));
    k.add(paint(box(5.6, 0.6, 5.6), ochre, tf(fx - 1.6, 17, tz)), win(fx + 0.95, 14.6, tz, 1.6, 2.8, '#1e2226'));
    k.add(paint(hip(5.8, 4.2, 5.8), TILE, tf(fx - 1.6, 17.3, tz)));
    k.light(paint(box(0.3, 1.4, 0.9), LAMP, tf(fx + 1.05, 14.4, tz)));
  }
  // Cruzeiro de madeira no adro.
  k.add(paint(box(2.4, 1.2, 2.4), STONE, tf(fx + 12, 0.6, 0)), paint(box(0.5, 6.5, 0.5), '#5a3a22', tf(fx + 12, 4.4, 0)), paint(box(0.5, 0.5, 2.8), '#5a3a22', tf(fx + 12, 6.2, 0)));
  k.light(paint(box(0.4, 0.6, 0.4), LAMP, tf(fx + 1.3, 5.6, -3)), paint(box(0.4, 0.6, 0.4), LAMP, tf(fx + 1.3, 5.6, 3)));
  return k.model([0, 21], 0.03, 451);
}

/**
 * Cavalhadas de Pirenópolis: o campo de terra com os dois castelos inteiros na cor do lado (cristão azul, mouro
 * vermelho), os cavaleiros em carga com as lanças e os cavalos de caparazão da mesma cor, dois mascarados, os camarotes
 * coloridos e as bandeirolas. De longe, os lados se leem pela cor (eram fortinhos brancos de ameias coloridas).
 */
function cavalhada(): Model {
  const k = new Kit();
  const BLUE = '#2a56c8'; const RED = '#d02a2a'; const GOLD = '#e8c040';
  k.add(paint(box(44, 0.2, 94), '#dcbc8a', tf(2, 0.1, 0)));
  // Camarotes no fundo (−X): caixas de madeira sobre estacas, panos coloridos e telhado.
  const cloths = ['#d02a2a', '#2a56c8', '#f2c230', '#2a9a4a', '#f2f0e8', '#e86aa0'];
  for (let i = 0; i < 9; i++) {
    const z = -32 + i * 8;
    for (const dz of [-3.6, 3.6]) for (const dx of [-1.8, 1.8]) k.add(paint(box(0.3, 2.6, 0.3), WOOD, tf(-22 + dx, 1.3, z + dz)));
    k.add(paint(box(4, 2.6, 7.6), '#8a6a4a', tf(-22, 3.9, z)));
    k.add(paint(box(0.3, 1.2, 7.6), cloths[i % cloths.length], tf(-19.9, 3.2, z)));
    k.add(paint(gable(4.6, 1.6, 7.8, 0.2), cloths[(i + 3) % cloths.length], tf(-22, 5.2, z)));
  }
  // Castelos nas cabeceiras do campo, cada um inteiro na cor do seu lado (de longe, o lado se lê pela cor): torre
  // com ameias brancas, o portão, a faixa e a bandeira.
  for (const [s, color] of [[-1, BLUE], [1, RED]] as Array<[number, string]>) {
    const z = s * 41;
    k.add(paint(box(10, 13, 10), color, tf(2, 6.5, z)));
    for (const dx of [-4, 0, 4]) for (const dz of [-4, 4]) k.add(paint(box(1.6, 1.8, 1.6), WHITE, tf(2 + dx, 13.9, z + dz)));
    k.add(paint(box(0.3, 1, 10.2), WHITE, tf(7.1, 10, z)), win(7.15, 2.3, z, 3, 4.6, '#3a2a1a'));
    k.add(paint(cyl(0.16, 0.16, 7, 5), '#3a3a3a', tf(2, 16.5, z)), paint(box(0.1, 2.8, 4.4), color, tf(2, 18.6, z - s * 2.2)));
  }
  // Mastro do Divino no meio do campo: a bandeira vermelha com a pomba branca.
  k.add(paint(cyl(0.16, 0.2, 15, 6), '#e8e4da', tf(-6, 7.5, 0)));
  k.add(paint(box(0.12, 3.2, 4.8), '#c82828', tf(-6, 13.2, 2.5)), paint(new THREE.OctahedronGeometry(0.7, 0), '#f8f8f4', tf(-5.9, 13.2, 2.5, 0.4, 0.8, 1.4)));
  // Cavaleiros: cavalo com manta da cor do time, cavaleiro de capa e chapéu de pluma, lança para a frente.
  // O cavalo baixado (peça `cavalo`, parts.ts) entra no lugar do de caixas: encaixado pelo lombo (a sela) na altura do
  // lombo do procedural (2,2 × S), o cavaleiro procedural senta nele como antes; a manta e a faixa dourada do time
  // ficam (o caparazão). A cabeça (+X da peça) para a carga (+Z × dir).
  const horsePart = landmarkPart('cavalo');
  const knight = (x: number, z: number, dir: number, color: string, horse: string, S: number): void => {
    const P = (a: number, y: number, b = 0): V3 => [x + a * S, y * S, z + dir * b * S];
    const L: (a: V3, b: V3, w: number, c: string) => void = (a, b, w, c) => { k.add(beam(a, b, w * S, c)); };
    if (horsePart) k.add(horsePart.at({ back: 2.2 * S }, x, 0, z, -dir * Math.PI / 2));
    else k.add(paint(box(1, 1.1, 2.4), horse, tf(...P(0, 1.65), S, S, S)));
    k.add(paint(box(1.25, 1.05, 2.5), color, tf(...P(0, 1.45), S, S, S)));
    k.add(paint(box(1.2, 0.18, 2.2), GOLD, tf(...P(0, 1.05), S, S, S)));
    if (!horsePart) {
      L(P(0, 2.0, 1.0), P(0, 2.9, 1.7), 0.55, horse);
      k.add(paint(box(0.5, 0.5, 1.1), horse, tf(...P(0, 2.95, 2.0), S, S, S, dir * 0.5, 0, 0)));
      for (const [a, b, c] of [[-0.35, 0.9, 1.6], [0.35, 0.9, 0.9], [-0.35, -0.9, -0.3], [0.35, -0.9, -1.4]] as Array<[number, number, number]>) L(P(a, 1.2, b), P(a, 0.05, c), 0.22, horse);
      L(P(0, 1.9, -1.2), P(0, 1.0, -1.7), 0.25, '#3a2a1a');
    }
    // Cavaleiro.
    k.add(paint(box(0.55, 0.9, 0.45), color, tf(...P(0, 2.75, -0.1), S, S, S)));
    k.add(paint(box(0.7, 0.5, 0.5), GOLD, tf(...P(0, 2.4, -0.1), S, S, S)));
    k.add(paint(ico(0.24, 0), '#e0b090', tf(...P(0, 3.4, -0.1), S, S, S)));
    k.add(paint(cyl(0.38, 0.38, 0.12, 6), color, tf(...P(0, 3.62, -0.1), S, S, S)), paint(cone(0.12, 0.6, 4), '#f4f2ea', tf(...P(0, 3.95, -0.25), S, S, S)));
    L(P(0.35, 2.6, -1.2), P(0.35, 3.6, 3.2), 0.08, '#d8d0c0');
    k.add(paint(box(0.04, 0.4, 0.6), color, tf(...P(0.35, 3.5, 2.8), S, S, S)));
  };
  // Dois esquadrões de quatro em carga um contra o outro (×2,6: o cavaleiro com o cavalo passa de 9 m; o caparazão
  // cobre o cavalo na cor do lado). Com o cavalo baixado (mais caro que o de caixas), três de cada lado: o último de
  // cada esquadrão sai (orçamento do marco).
  const S = 2.6;
  knight(4, -11, 1, BLUE, '#f2f0ea', S); knight(12, -19, 1, BLUE, '#7a5a3a', S); knight(-4, -21, 1, BLUE, '#f2f0ea', S); if (!horsePart) knight(5, -30, 1, BLUE, '#3a2a22', S);
  knight(4, 11, -1, RED, '#3a2a22', S); knight(12, 19, -1, RED, '#f2f0ea', S); knight(-4, 21, -1, RED, '#7a5a3a', S); if (!horsePart) knight(5, 30, -1, RED, '#f2f0ea', S);
  // Mascarados: roupa colorida e a máscara de boi branca com chifres e flores.
  for (const [x, z, c] of [[16, -4, '#e86aa0'], [15, 5, '#2a9a4a']] as Array<[number, number, string]>) {
    k.add(paint(cone(0.8, 1.9, 6), c, tf(x, 0.95, z)), paint(box(0.5, 0.8, 0.5), c, tf(x, 2.2, z)));
    k.add(paint(box(0.8, 0.7, 0.7), '#f4f2ea', tf(x + 0.1, 2.9, z)), paint(box(0.5, 0.4, 0.5), '#f4f2ea', tf(x + 0.5, 2.75, z)));
    k.add(beam([x, 3.2, z - 0.35], [x - 0.1, 3.8, z - 0.8], 0.12, '#f2e6c8'), beam([x, 3.2, z + 0.35], [x - 0.1, 3.8, z + 0.8], 0.12, '#f2e6c8'));
    k.add(paint(ico(0.18, 0), '#e8402a', tf(x + 0.45, 3.3, z - 0.2)), paint(ico(0.18, 0), '#f2c230', tf(x + 0.45, 3.3, z + 0.2)));
  }
  // Mastros com bandeirolas triangulares ao longo do campo.
  for (const z of [-28, 0, 28]) k.add(paint(cyl(0.14, 0.16, 9, 5), '#e8e4da', tf(-15, 4.5, z)));
  for (let i = 0; i < 28; i++) {
    const z = -27 + i * 2; const y = 8.4 - Math.sin(((i % 14) / 14) * Math.PI) * 1.2;
    k.add(paint(twoSided([-15, y, z - 0.6, -15, y, z + 0.6, -15, y - 1, z]), cloths[i % cloths.length]));
  }
  k.add(cable([-15, 8.6, -28], [-15, 8.6, 28], 0.06, '#3a3a3a'));
  k.light(paint(box(0.4, 0.4, 66), LAMP, tf(-19.8, 4.4, 0)));
  return k.model([0, 10], 0.03, 461);
}

/**
 * Parque aquático de Caldas Novas: a piscina de água quente azul-turquesa com o deque, a torre dos toboáguas e três
 * escorregadores — o kamikaze reto, o caracol vermelho e o ondulado azul —, guarda-sóis e coqueiros.
 */
function parqueAquatico(): Model {
  const k = new Kit();
  k.add(paint(box(36, 0.4, 56), '#e8e2d2', tf(2, 0.2, 0)));
  k.add(paint(box(22, 0.45, 34), '#3cc0d8', tf(6, 0.25, 4)));
  k.add(paint(cyl(7, 7, 0.45, 12), '#5ad0e4', tf(10, 0.25, -20)));
  // Torre: quatro pilares, a plataforma, a cobertura amarela e a escada.
  const tx = -11; const tz = -12; const H = 16;
  for (const dx of [-3, 3]) for (const dz of [-3, 3]) k.add(paint(box(0.6, H + 2.6, 0.6), '#e8e4dc', tf(tx + dx, (H + 2.6) / 2, tz + dz)));
  k.add(paint(box(8, 0.5, 8), '#d8d2c4', tf(tx, H, tz)), paint(hip(9, 2.4, 9), '#f2c230', tf(tx, H + 2.6, tz)));
  for (let i = 0; i < 4; i++) k.add(beam([tx - 4.2, i * 4, tz + (i % 2 ? 3 : -3)], [tx - 4.2, (i + 1) * 4, tz + (i % 2 ? -3 : 3)], 1.2, '#b8b4ac', 0.3));
  // Kamikaze: reto e íngreme até a piscina.
  const ka: V3 = [tx + 3.5, H + 0.4, tz - 2]; const kb: V3 = [7, 1, tz - 2];
  k.add(beam(ka, kb, 1.8, '#f2c230', 0.5));
  for (const s of [-1, 1]) k.add(beam([ka[0], ka[1] + 0.5, ka[2] + s * 0.9], [kb[0], kb[1] + 0.5, kb[2] + s * 0.9], 0.2, '#e8a020', 0.8));
  for (const t of [0.3, 0.6]) k.add(paint(box(0.4, ka[1] + (kb[1] - ka[1]) * t, 0.4), '#b8b4ac', tf(ka[0] + (kb[0] - ka[0]) * t, (ka[1] + (kb[1] - ka[1]) * t) / 2, ka[2])));
  // Caracol vermelho: hélice de 2,5 voltas descendo ao lado da torre.
  const hc: V3 = [tx - 2, 0, tz + 12]; const hr = 5; const turns = 2.5; const NS = 24;
  let prev: V3 = [tx + 1, H, tz + 3.6];
  for (let i = 0; i <= NS; i++) {
    const t = i / NS; const a = -Math.PI / 2 + t * turns * Math.PI * 2;
    const p: V3 = [hc[0] + Math.cos(a) * hr, H - 0.5 - t * (H - 2.5), hc[2] + Math.sin(a) * hr];
    k.add(beam(prev, p, 1.7, '#e8402a', 1.7));
    if (i % 4 === 2) k.add(paint(box(0.35, p[1], 0.35), '#b8b4ac', tf(p[0], p[1] / 2, p[2])));
    prev = p;
  }
  k.add(beam(prev, [2, 1, hc[2] + 2], 1.7, '#e8402a', 1.7));
  // Ondulado azul: desce em S pela lateral até a piscina.
  prev = [tx, H, tz - 4];
  for (let i = 1; i <= 10; i++) {
    const t = i / 10;
    const p: V3 = [tx + t * 15 + Math.sin(t * Math.PI * 3) * 3, H - t * (H - 1.2) + Math.sin(t * Math.PI * 6) * 0.6 * (1 - t), tz - 4 - t * 14];
    k.add(beam(prev, p, 1.8, '#2f7fd8', 0.6));
    if (i % 3 === 0) k.add(paint(box(0.35, p[1], 0.35), '#b8b4ac', tf(p[0], p[1] / 2, p[2])));
    prev = p;
  }
  // Guarda-sóis e coqueiros no deque.
  for (let i = 0; i < 6; i++) {
    const z = -22 + i * 9; const x = 18.5;
    k.add(paint(cyl(0.06, 0.06, 2.4, 4), '#e8e4dc', tf(x, 1.4, z)), paint(cone(1.6, 0.7, 8), ['#e8402a', '#f2c230', '#2f7fd8'][i % 3], tf(x, 2.8, z)));
  }
  for (const [x, z] of [[-14, 14], [-15, 22]] as Array<[number, number]>) {
    k.add(paint(cyl(0.25, 0.35, 9, 5), '#8a7a5a', tf(x, 4.5, z, 1, 1, 1, 0, 0, 0.08)));
    for (let f = 0; f < 6; f++) { const a = (f / 6) * Math.PI * 2; k.add(beam([x + 0.3, 9, z], [x + 0.3 + Math.cos(a) * 3.6, 7.6, z + Math.sin(a) * 3.6], 0.9, '#4f8f3a', 0.15)); }
  }
  return k.model([0, 19], 0.02, 471);
}

/**
 * Jacarés da Estrada Parque (Pantanal): a baía com os tapetes de aguapé, a prainha de lama clara na margem de cá onde
 * o bando toma sol no alto dela — os jacarés com a proporção de verdade (2,7 m) ×3,15, escuros contra a lama clara, na
 * diagonal da pista (de lado para quem passa e para quem chega) e quase todos de cabeça erguida e boca aberta (o
 * amarelo da boca lê de longe; ×2,1 no chão da prainha, a 170 m, sumiam) —, dois nadando com só o dorso
 * de fora, a ponte de madeira da estrada atravessando o corixo, as capivaras e a árvore seca com as garças e o biguá.
 */
function jacare(): Model {
  const k = new Kit();
  // Margem de lama clara, a baía (rasa e esverdeada na borda, funda no meio) e a prainha elevada de cá.
  k.add(paint(cyl(26, 27, 0.2, 14), '#d2bc90', tf(0, 0.1, 0, 1, 1, 1.5)));
  k.add(paint(cyl(21, 21, 0.3, 14), '#5f8a6a', tf(-5, 0.18, 0, 1, 1, 1.5)), paint(cyl(15, 15, 0.36, 14), '#47705a', tf(-7, 0.2, 0, 1, 1, 1.5)));
  k.add(hill(12, 2.6, 46, 480, '#dcc69c', null, 1, 0.08, 0.4, tf(16, 0, 2)));
  // Placa de "cuidado, jacarés" da Estrada Parque na frente: losango amarelo com o bicho em preto (sem letras), ×2,2
  // (losango de 7 m a 7,5 m do chão): a 190 m é o que lê primeiro — os bichos, mesmo maiores, dão poucos pixels.
  const SG = tf(25, 0, 30, 2.2, 2.2, 2.2).multiply(tf(-25, 0, -30));
  const sg = (g: Geo): Geo => g.applyMatrix4(SG);
  k.add(sg(paint(box(0.22, 3.4, 0.22), '#8a8a86', tf(25, 1.7, 30))));
  k.add(sg(paint(box(0.12, 2.6, 2.6), '#2a2a2a', tf(25.1, 3.9, 30, 1, 1, 1, Math.PI / 4, 0, 0))), sg(paint(box(0.14, 2.3, 2.3), '#f2c42a', tf(25.15, 3.9, 30, 1, 1, 1, Math.PI / 4, 0, 0))));
  k.add(sg(paint(box(0.16, 0.3, 1.5), '#1a1a1a', tf(25.25, 3.85, 30))), sg(paint(box(0.16, 0.16, 0.55), '#1a1a1a', tf(25.25, 3.82, 30.95))), sg(paint(box(0.16, 0.2, 0.6), '#1a1a1a', tf(25.25, 3.78, 29.0, 1, 1, 1, 0.3, 0, 0))));
  for (const dz of [-0.45, 0.45]) k.add(sg(paint(box(0.16, 0.36, 0.12), '#1a1a1a', tf(25.25, 3.6, 30 + dz))));
  for (let i = 0; i < 8; i++) {
    const x = -16 + hash2(i, 481) * 16; const z = -26 + i * 7.5;
    k.add(paint(jitter(ico(1, 0), 0.2, 482 + i), i % 2 ? '#5aa040' : '#4f9238', tf(x, 0.4, z, 3.2, 0.32, 2.6)));
    if (i % 4 === 1) k.add(paint(box(0.6, 0.5, 0.6), '#c890d8', tf(x + 0.6, 0.8, z)));
  }
  // Jacaré de 2,7 m na escala `S`: corpo, ventre claro, cauda em três gomos com a crista, patas abertas, cabeça
  // (erguida `lift` rad) com o focinho comprido e, aberta, a boca amarela por dentro; `y0` = chão dele (na água, só o
  // dorso fica de fora).
  const caiman = (x: number, z: number, yaw: number, S: number, open: boolean, lift = 0.25, y0 = 0.95): void => {
    const M = tf(x, y0, z, S, S, S, 0, yaw, 0);
    const at = (m: THREE.Matrix4): THREE.Matrix4 => M.clone().multiply(m);
    const back = '#3a4430'; const flank = '#5a5e3e'; const belly = '#a89c6c';
    k.add(paint(ico(1, 0), back, at(tf(0, 0.2, 0, 0.3, 0.19, 0.58))));
    k.add(paint(box(0.5, 0.08, 0.95), belly, at(tf(0, 0.05, 0))));
    const tail: V3[] = [[0, 0.2, -0.48], [0.06, 0.16, -0.95], [0.18, 0.11, -1.35], [0.34, 0.06, -1.7]];
    for (let i = 0; i < 3; i++) k.add(beam(tail[i], tail[i + 1], 0.34 - i * 0.09, i ? flank : back, 0.24 - i * 0.055).applyMatrix4(M));
    k.add(beam([0, 0.4, 0.3], [0, 0.3, -1.3], 0.07, '#2a3020', 0.07).applyMatrix4(M));
    for (const dz of [0.3, -0.35]) k.add(beam([-0.42, 0.04, dz + 0.1], [0.42, 0.04, dz + 0.1], 0.12, flank).applyMatrix4(M));
    // Cabeça: pescoço e crânio, mandíbula de baixo, focinho de cima (abre para cima).
    const head = at(tf(0, 0.2, 0.55, 1, 1, 1, -lift, 0, 0));
    k.add(paint(box(0.3, 0.16, 0.32), back, head.clone().multiply(tf(0, 0.02, 0.12))));
    k.add(paint(box(0.2, 0.06, 0.48), belly, head.clone().multiply(tf(0, -0.04, 0.5))));
    k.add(paint(box(0.2, 0.08, 0.48), back, head.clone().multiply(tf(0, open ? 0.12 : 0.04, 0.5, 1, 1, 1, open ? -0.45 : 0, 0, 0))));
    if (open) k.add(paint(box(0.17, 0.03, 0.4), '#f0d48a', head.clone().multiply(tf(0, 0.0, 0.48))));
  };
  // ×3,15 (era ×2,1: a 170 m, o bando somava 26 m² de frente e sumia): 8,5 m de jacaré, no alto da prainha.
  const S = 3.15;
  // O bando na prainha: na diagonal da pista (yaw ≈ ±π/4, ±3π/4) — de lado tanto para quem passa quanto para quem
  // chega —, cabeças para a água ou para os lados.
  const Q = Math.PI / 4;
  const bask: Array<[number, number, number, boolean]> = [
    [17, -36, 3 * Q, true], [14, -24, -Q, false], [19, -12, Q + 0.2, true], [13, -1, -3 * Q, true],
    [19, 10, -Q, false], [14, 22, 3 * Q + 0.2, true], [19, 33, -Q, true], [11, 41, Q, false],
    [9, -30, -3 * Q + 0.2, true], [22, 0, Q - 0.2, true], [9, 12, Q, true],
  ];
  // Em cima da prainha (2,6 m no meio): de longe, o bando fica acima do capim da margem.
  const bankY = (x: number): number => Math.max(0.5, 2.6 * (1 - ((x - 16) / 12) ** 2) - 0.35);
  // O jacaré baixado (peça `jacare`, parts.ts): 2,7 m de verdade × S, a cabeça (+X da peça) para onde a procedural
  // olha (+Z local girado `yaw`). Mais detalhado e mais caro que o procedural: 6 do bando na prainha (os da frente) e
  // os 2 nadando (só o dorso de fora) — 8 no orçamento do marco; o da margem do fundo some.
  const part = landmarkPart('jacare');
  if (part) {
    const put = (x: number, z: number, yaw: number, y0: number): void => { k.add(part.at({ length: 2.7 * S }, x, y0, z, yaw - Math.PI / 2)); };
    for (const [x, z, yaw] of bask.slice(0, 6)) put(x, z, yaw, bankY(x));
    put(-2, -6, 2.2, -0.15);
    put(2, 12, -0.6, -0.15);
  } else {
    for (const [x, z, yaw, open] of bask) caiman(x, z, yaw, S, open, 0.3, bankY(x));
    // Dois nadando (só o dorso e os olhos de fora) e um na margem do fundo.
    caiman(-2, -6, 2.2, S, false, 0, 0.05);
    caiman(2, 12, -0.6, S, false, 0, 0.05);
    caiman(-17, 20, 1.2, S, true, 0.25, 0.15);
  }
  // Ponte de madeira da Estrada Parque atravessando o corixo: tabuleiro, estacas e guarda-corpo.
  const bx = -17;
  k.add(paint(box(5.2, 0.45, 56), '#8a6a48', tf(bx, 2.2, 0)));
  for (let i = 0; i <= 7; i++) for (const s of [-1, 1]) k.add(paint(box(0.4, 4, 0.4), '#5a3e26', tf(bx + s * 2.4, 1.3, -28 + i * 8)));
  for (const s of [-1, 1]) k.add(paint(box(0.2, 0.22, 56), '#6a4e34', tf(bx + s * 2.5, 3.5, 0)), paint(box(0.18, 0.18, 56), '#6a4e34', tf(bx + s * 2.5, 2.9, 0)));
  // Capivaras na margem.
  for (const [x, z, a] of [[22, 33, 0.3], [24, 36, -0.4], [20, 37, 1.2]] as Array<[number, number, number]>) {
    const m = tf(x, 0, z, 1.6, 1.6, 1.6, 0, a, 0);
    k.add(paint(box(0.7, 0.6, 1.2), '#8a6a42', m.clone().multiply(tf(0, 0.6, 0))), paint(box(0.45, 0.45, 0.6), '#7a5a36', m.clone().multiply(tf(0, 0.75, 0.85))));
    k.add(paint(box(0.6, 0.35, 1.0), '#6a4a2a', m.clone().multiply(tf(0, 0.18, 0))));
  }
  // Árvore seca com garças e o biguá.
  const bark = '#7a6a5a'; const tx = 6; const tz = -34; const H = 11;
  k.add(paint(cyl(0.45, 0.7, H, 6), bark, tf(tx, H / 2, tz)));
  const branches: Array<[V3, V3]> = [[[tx, H - 0.5, tz], [tx + 3.2, H + 2.2, tz + 0.8]], [[tx, H - 0.3, tz], [tx - 2.6, H + 2.6, tz - 1.4]], [[tx, H, tz], [tx + 0.5, H + 3.4, tz + 2.2]], [[tx, H * 0.6, tz], [tx + 3.4, H * 0.6 + 1.6, tz - 1.8]]];
  for (const [a, b] of branches) k.add(beam(a, b, 0.25, bark));
  const egret = (p: V3, c: string): void => {
    k.add(paint(ico(1, 0), c, tf(p[0], p[1] + 0.55, p[2], 0.4, 0.38, 0.7)));
    k.add(beam([p[0], p[1] + 0.75, p[2] + 0.45], [p[0], p[1] + 1.55, p[2] + 0.55], 0.13, c));
    k.add(beam([p[0], p[1] + 1.55, p[2] + 0.55], [p[0], p[1] + 1.5, p[2] + 1.15], 0.08, c === '#1e1e22' ? '#3a3a3a' : '#e0b030'));
  };
  for (const [, b] of branches) egret(b, '#f6f6f2');
  egret([tx, H + 0.1, tz], '#1e1e22');
  // Garças na lama da prainha.
  for (const [x, z] of [[21, -13], [22, 3], [20, 19]] as Array<[number, number]>) {
    k.add(paint(ico(1, 0), '#f6f6f2', tf(x, 2.2, z, 0.6, 0.55, 1.0)));
    k.add(beam([x, 2.5, z + 0.6], [x, 3.7, z + 0.75], 0.18, '#f6f6f2'), beam([x, 3.7, z + 0.75], [x, 3.6, z + 1.6], 0.11, '#e0b030'));
    for (const b of [-0.15, 0.15]) k.add(beam([x + b, 1.9, z], [x + b, 1.1, z], 0.08, '#2a2a2a'));
  }
  k.add(blob(489, '#5a8a3a', tx - 3, 0.6, tz + 3, 3, 1.2, 3), blob(490, '#6a9a44', 24, 0.5, -34, 2.6, 1, 2.6));
  return k.model([0, 10], 0.04, 481);
}

/**
 * Relógio da 14 de Julho (Campo Grande): a torre art déco de base escalonada com os quatro mostradores acesos, na
 * rotatória de canteiros com ipês amarelos e roxo e os postes. A torre em ×1,45 (30 m, mostrador de 4,6 m): com 20 m
 * e 3,2 m, a 150–250 m sumia entre os prédios da praça (tests/landmarks-leitura-centro-sul-2.test.ts).
 */
function monumentoCampoGrande(): Model {
  const k = new Kit();
  const cream = '#efe6d2'; const accent = '#8a5a3a';
  const U = tf(0, 0, 0, 1.45, 1.45, 1.45);
  const up = (g: Geo): Geo => g.applyMatrix4(U);
  k.add(paint(cyl(18, 18, 0.3, 16), '#5c9a48', tf(0, 0.15, 0)), paint(cyl(11, 11, 0.4, 16), '#c8c2b4', tf(0, 0.2, 0)));
  for (const [w, h, y] of [[7, 0.8, 0.4], [5.6, 0.8, 1.2]] as Array<[number, number, number]>) k.add(up(paint(box(w, h, w), '#b8b0a0', tf(0, y + h / 2, 0))));
  // Fuste escalonado com as caneluras e a faixa de cor.
  k.add(up(paint(box(3.6, 10, 3.6), cream, tf(0, 7, 0))));
  for (const s of [-1, 1]) for (const d of [-1, 1]) k.add(up(paint(box(0.7, 11, 0.7), cream, tf(s * 1.8, 7.5, d * 1.8))));
  for (const s of [-1, 1]) { k.add(up(paint(box(0.2, 8, 0.5), accent, tf(s * 1.85, 6.5, 0))), up(paint(box(0.5, 8, 0.2), accent, tf(0, 6.5, s * 1.85)))); }
  // Cabeça do relógio, os quatro mostradores (luz) e os ponteiros.
  const yc = 14.6;
  k.add(up(paint(box(4.4, 4.4, 4.4), cream, tf(0, yc, 0))), up(paint(box(4.8, 0.5, 4.8), accent, tf(0, yc - 2.4, 0))), up(paint(box(4.8, 0.5, 4.8), accent, tf(0, yc + 2.4, 0))));
  for (let f = 0; f < 4; f++) {
    const R = new THREE.Matrix4().makeRotationY((f * Math.PI) / 2);
    k.light(up(paint(cyl(1.6, 1.6, 0.2, 12), '#fff6d8', R.clone().multiply(tf(2.25, yc, 0, 1, 1, 1, 0, 0, Math.PI / 2)))));
    k.add(up(paint(box(0.12, 1.2, 0.18), '#1e1e22', R.clone().multiply(tf(2.38, yc + 0.5, 0)))));
    k.add(up(paint(box(0.12, 0.18, 0.9), '#1e1e22', R.clone().multiply(tf(2.38, yc, 0.4)))));
  }
  // Coroamento escalonado e o mastro.
  k.add(up(paint(box(3.4, 1.2, 3.4), cream, tf(0, yc + 3.2, 0))), up(paint(box(2.2, 1.2, 2.2), cream, tf(0, yc + 4.4, 0))), up(paint(box(1.2, 1, 1.2), accent, tf(0, yc + 5.5, 0))));
  k.add(up(paint(cyl(0.08, 0.08, 3, 4), '#3a3a3a', tf(0, yc + 7.5, 0))));
  // Ipês floridos (a cidade dos ipês) e postes.
  for (const [x, z, c] of [[-12, -11, '#f2c230'], [-11, 12, '#f2c230'], [-15, 1, '#c86ab8']] as Array<[number, number, string]>) {
    k.add(paint(cyl(0.35, 0.55, 6, 6), '#5a4838', tf(x, 3, z)));
    k.add(blob(491 + x, c, x, 7.6, z, 4.2, 2.6, 4.2), blob(492 + z, c, x + 1.6, 6.6, z - 1.3, 2.9, 2, 2.9));
  }
  for (let i = 0; i < 4; i++) {
    const a = (i / 4) * Math.PI * 2 + Math.PI / 4;
    k.add(paint(box(0.2, 5, 0.2), '#2a2a2a', tf(Math.cos(a) * 10, 2.5, Math.sin(a) * 10)));
    k.light(paint(sphere(0.4, 6, 4), LAMP, tf(Math.cos(a) * 10, 5.2, Math.sin(a) * 10)));
  }
  return k.model([0, 29], 0.02, 491);
}

/**
 * Cachoeira Véu de Noiva (Chapada dos Guimarães): o anfiteatro de arenito vermelho em estratos — o paredão do fundo e
 * as duas abas abertas para quem chega —, o cerrado na borda da chapada, o véu de 86 m que se lança do lábio e abre
 * em riscos brancos até a nuvem de névoa, o poço verde e a mata no pé. Era um muro de blocos retos com um fio d'água
 * de 2 m (lia como fachada, e a aba escondia a queda de quem chega; tests/landmarks-leitura-centro-sul-2.test.ts).
 */
function veuDeNoiva(): Model {
  const k = new Kit();
  const H = 92;
  const base = {
    H, strata: ['#b5552f', '#c96e3e', '#9c4328', '#d27e4a', '#ac4f2d'], layers: 6, batter: 0.05, gully: 8, gullyLen: 20,
    ragged: 0.08, ledge: 2.4, talus: 16, talusColor: '#3a6a34', top: '#7a8f3e', ledgeTop: '#6f8a3a',
  } as const;
  // O paredão do fundo (o véu cai do meio dele) e as abas, abertas 0,6 rad: de quem chega, a queda fica à vista.
  const xb = -40; const lenB = 90; const lenW = 92; const open = 0.6;
  const back = cliff({ ...base, len: lenB, seed: 501, cols: 14, depth: 46, ends: 0, bay: 5 }, tf(xb, 0, 0));
  k.add(back.geo);
  for (const s of [-1, 1]) {
    // A aba de −Z olha para +Z+X (giro −0,6): a ponta de dentro encosta na do paredão do fundo, a de fora baixa e recua.
    const sn = Math.sin(open); const cs = Math.cos(open);
    const cx = xb + 4 + (sn * lenW) / 2; const cz = s * (lenB / 2 + (cs * lenW) / 2);
    const w = cliff({ ...base, H: H * 0.84, ragged: 0.1, len: lenW, seed: 502 + s, cols: 12, depth: 46, ends: s < 0 ? [0.32, 0] : [0, 0.32], bay: 6 }, tf(cx, 0, cz, 1, 1, 1, 0, s * open, 0));
    k.add(w.geo);
    // A ponta de fora desce em encosta de mata (o corte do paredão, visto de lado, lia como rampa).
    k.add(hill(30, 52, 36, 509 + s, '#3a6a34', '#4f8a3c', 1, 0.12, 0.25, tf(cx + (lenW / 2) * sn - 22, 0, cz + s * ((lenW / 2) * cs - 4))));
    // Cerrado na borda de cima das abas (um pouco atrás do lábio).
    for (let i = 0; i < 4; i++) {
      const along = s * (-lenW / 2 + ((i + 0.3) / 4) * lenW);
      const lx = w.lipAt(along) - 9;
      k.add(blob(520 + i + (s > 0 ? 10 : 0), i % 2 ? '#6a7a32' : '#7d8c3c', cx + along * s * sn + lx * cs, w.topAt(along) + 1.5, cz + along * cs - lx * s * sn, 7, 3, 7));
    }
  }
  for (let i = 0; i < 4; i++) k.add(blob(530 + i, i % 2 ? '#6a7a32' : '#7d8c3c', xb + back.lipAt(-40 + i * 27) - 14, back.topAt(-40 + i * 27) + 1.5, -40 + i * 27, 8, 3, 8));
  // O véu: lança do lábio (um ressalto de pedra) e abre em riscos até a névoa, na frente do talude de mata.
  const lip = xb + back.lipAt(0); const top = back.topAt(0);
  k.add(paint(box(9, 3, 12), '#9a4426', tf(lip + 1.5, top - 1.5, 0)));
  veil(k, 505, 0, lip + 7, xb + back.footAt(0) + 18, top - 2, 4, 9, 22, 6, 3);
  // Névoa no pé (luz: mancha clara e macia) e o poço verde.
  const fx = xb + back.footAt(0) + 18;
  for (let i = 0; i < 3; i++) k.light(paint(jitter(ico(1, 1), 0.14, 540 + i), MIST, tf(fx + 4 + i * 3, 6 + i * 3, (i - 1) * 11, 16 - i * 2, 11 - i, 15)));
  k.add(paint(cyl(24, 26, 0.6, 12), '#3a8a7a', tf(fx + 16, 0.3, 0)));
  // Mata no fundo do anfiteatro, dos dois lados do poço.
  for (let i = 0; i < 8; i++) {
    const z = (i < 4 ? -1 : 1) * (22 + (i % 4) * 16);
    k.add(blob(550 + i, i % 2 ? '#2f6a33' : '#3f7a3a', fx - 4 + (i % 4) * 7, 4, z, 9, 7, 9));
  }
  return k.model([0, H], 0.04, 501);
}

/**
 * Centro Geodésico da América do Sul (Cuiabá): o obelisco branco na base escalonada, o globo azul com o continente,
 * a praça redonda, os mastros e os postes acesos. Obelisco e globo em ×1,45 (26 m e 5,5 m): com 19 m e 3,8 m, de longe
 * lia "um poste com uma bola" (tests/landmarks-leitura-centro-sul-2.test.ts).
 */
function centroGeodesico(): Model {
  const k = new Kit();
  const U = tf(0, 0, 0, 1.45, 1.45, 1.45);
  const up = (g: Geo): Geo => g.applyMatrix4(U);
  k.add(paint(cyl(17, 17, 0.3, 16), '#5c9a48', tf(0, 0.15, 0)), paint(cyl(12, 12, 0.4, 16), '#d4cebe', tf(0, 0.2, 0)));
  for (const [w, y] of [[7, 0.4], [5.4, 1.0], [3.8, 1.6]] as Array<[number, number]>) k.add(up(paint(box(w, 0.6, w), '#b8b0a0', tf(0, y + 0.3, 0))));
  k.add(up(paint(hip(2.6, 15, 2.6, 1.4, 1.4), '#ece8dc', tf(0, 2.2, 0))), up(paint(hip(1.4, 1.6, 1.4), '#d8c8a0', tf(0, 17.2, 0))));
  k.add(up(paint(box(0.12, 1.4, 1.4), '#8a6a3a', tf(1.28, 4, 0)))); // placa de bronze (sem letras)
  // Globo com a América do Sul na frente, num pedestal.
  const gx = 6.5;
  k.add(up(paint(cyl(0.9, 1.1, 1.6, 8), '#b8b0a0', tf(gx, 0.8, 0))));
  // O globo é o que diz "centro da América do Sul": ainda maior que o resto (raio 2,3 × 1,45).
  const G = (m: THREE.Matrix4): THREE.Matrix4 => tf(gx, 3.9, 0, 1.21, 1.21, 1.21).multiply(m);
  k.add(up(paint(sphere(1.9, 14, 10), '#2f6fc0', G(tf(0, 0, 0)))));
  k.add(up(paint(jitter(ico(1, 0), 0.15, 551), '#4f9a48', G(tf(1.55, -0.2, 0.1, 0.5, 1.2, 0.75, 0, 0, 0.2)))));
  k.add(up(paint(jitter(ico(1, 0), 0.15, 552), '#4f9a48', G(tf(1.35, 1.1, -0.4, 0.45, 0.6, 0.8)))));
  // O anel do equador é luz: Cuiabá corre à noite, e o globo azul sumia no escuro — o anel aceso o desenha.
  k.light(up(paint(cyl(2.3, 2.3, 0.22, 16, true), '#d8c070', G(tf(0, 0, 0)))));
  // Mastros (bandeiras lisas) atrás e os postes.
  for (const [z, c] of [[-4, '#2a9a4a'], [0, '#2a56c8'], [4, '#f2c230']] as Array<[number, string]>) {
    k.add(paint(cyl(0.1, 0.12, 14, 5), '#d8d8d8', tf(-8.5, 7, z * 1.3)), paint(box(0.06, 2, 3), c, tf(-8.5, 12.8, z * 1.3 + 1.55)));
  }
  for (let i = 0; i < 4; i++) {
    const a = (i / 4) * Math.PI * 2 + Math.PI / 4;
    k.add(paint(box(0.22, 5, 0.22), '#2a2a2a', tf(Math.cos(a) * 11, 2.5, Math.sin(a) * 11)));
    k.light(paint(sphere(0.45, 6, 4), LAMP, tf(Math.cos(a) * 11, 5.2, Math.sin(a) * 11)));
  }
  k.light(paint(box(0.3, 0.3, 5.2), '#fff0d0', tf(3.4, 2.9, 0)));
  return k.model([0, 27], 0.02, 551);
}

export const LANDMARKS_BRASIL_2_CENTRO_SUL: LandmarkRegistry = {
  // Sudeste
  casario_paraty: { build: casarioParaty, place: 'near', side: 'any', perLap: 3, turn: 0.15 },
  escuna: { build: escuna, place: 'near', side: 'sea', perLap: 2 },
  dedo_de_deus: { build: dedoDeDeus, place: 'skyline', side: 'land', perLap: 2 },
  farol_ilhabela: { build: farolIlhabela, place: 'near', side: 'sea', perLap: 2 },
  veleiro_canal: { build: veleiroCanal, place: 'near', side: 'sea', perLap: 3 },
  chale_enxaimel: { build: chaleEnxaimel, place: 'near', side: 'any', perLap: 2 },
  igreja_pampulha: { build: igrejaPampulha, place: 'near', side: 'any', perLap: 2 },
  casca_danta: { build: cascaDanta, place: 'far', side: 'any', perLap: 2, turn: 0.35 },
  pedra_azul: { build: pedraAzul, place: 'skyline', side: 'land', perLap: 2 },
  dunas_itaunas: { build: dunasItaunas, place: 'far', side: 'land', perLap: 2, turn: 0.2 },
  // Sul
  cataratas_iguacu: { build: cataratasIguacu, place: 'far', side: 'any', perLap: 2, turn: 0.3 },
  ponte_amizade: { build: ponteAmizade, place: 'far', side: 'any', perLap: 1, turn: 0.15 },
  opera_de_arame: { build: operaDeArame, place: 'near', side: 'any', perLap: 2 },
  predios_camboriu: { build: prediosCamboriu, place: 'skyline', side: 'any', perLap: 2 },
  roda_gigante: { build: rodaGigante, place: 'near', side: 'any', perLap: 2, turn: 0.2 },
  mirante_rio_do_rastro: { build: miranteRioDoRastro, place: 'skyline', side: 'land', perLap: 1 },
  usina_gasometro: { build: usinaGasometro, place: 'far', side: 'sea', perLap: 2 },
  canion_itaimbezinho: { build: canionItaimbezinho, place: 'skyline', side: 'land', perLap: 1 },
  // Centro-Oeste
  ponte_jk: { build: ponteJk, place: 'far', side: 'sea', perLap: 1, turn: 0.1 },
  torre_tv_brasilia: { build: torreTvBrasilia, place: 'far', side: 'any', perLap: 2 },
  igreja_pirenopolis: { build: igrejaPirenopolis, place: 'near', side: 'any', perLap: 2 },
  cavalhada: { build: cavalhada, place: 'near', side: 'any', perLap: 2, turn: 0.2 },
  parque_aquatico: { build: parqueAquatico, place: 'near', side: 'any', perLap: 2 },
  jacare: { build: jacare, place: 'near', side: 'any', perLap: 2 },
  monumento_campo_grande: { build: monumentoCampoGrande, place: 'near', side: 'any', perLap: 2 },
  veu_de_noiva: { build: veuDeNoiva, place: 'far', side: 'any', perLap: 2, turn: 0.35 },
  centro_geodesico: { build: centroGeodesico, place: 'near', side: 'any', perLap: 2 },
};
