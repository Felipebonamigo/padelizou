// Marcos turísticos: ver docs/PISTAS-TURISMO.md (onda G). Um construtor por id de src/core/data/places.ts.
// Sudeste, Sul e Centro-Oeste. Low-poly de faces planas e cor chapada, escala real (skyline: grande), lido de longe
// pela silhueta. Convenção do modelo em types.ts: origem no centro da base, frente para +X, comprimento em Z.
import * as THREE from 'three';
import { hash2 } from '../../noise';
import { box, cone, cyl, dodeca, gable, hip, ico, jitter, paint, sphere, tf, tintUp, tris, type Geo } from '../geom';
import { beam, cable, facadeBox, hill, Kit, lathe } from './kit';
import type { LandmarkRegistry } from './types';

type V3 = [number, number, number];

// Cores que se repetem.
const WHITE = '#f4f2ea';
const TILE = '#b5583a'; // telhado de barro
const STONE = '#9a9282';
const SOAP = '#7f8270'; // pedra-sabão (Ouro Preto)
const WOOD = '#6e4a2c';
const LAMP = '#ffd98a';

/** Janelinha escura (vidro/abertura) encostada na face +X em (x, y, z). */
function win(x: number, y: number, z: number, w: number, h: number, color = '#283038'): Geo {
  return paint(box(0.25, h, w), color, tf(x, y, z));
}

/** Folha em leque (buriti): meia roda de `n` gomos plissados no plano XY, apontando para +Y, raio `r`. */
function fanLeaf(r: number, n: number, color: string, m: THREE.Matrix4): Geo {
  const out: number[] = [];
  for (let k = 0; k < n; k++) {
    const a0 = -1.35 + (k / n) * 2.7; const a1 = -1.35 + ((k + 1) / n) * 2.7;
    const am = (a0 + a1) / 2;
    const p0: V3 = [Math.sin(a0) * r, Math.cos(a0) * r, 0];
    const p1: V3 = [Math.sin(a1) * r, Math.cos(a1) * r, 0];
    const pm: V3 = [Math.sin(am) * r * 1.08, Math.cos(am) * r * 1.08, (k % 2 ? 1 : -1) * r * 0.08];
    out.push(0, 0, 0, ...p0, ...pm, 0, 0, 0, ...pm, ...p1);
  }
  return paint(tris(out), color, m);
}

// ───────────────────────────── Sudeste ─────────────────────────────

/** Cristo Redentor no alto do Corcovado (skyline): morro de mata, agulha de granito e a estátua (×2,5). */
function cristoRedentor(): ReturnType<Kit['model']> {
  const k = new Kit();
  // Maciço com mata (largo e baixo) e a agulha de pedra por cima, mais para o fundo (a face íngreme olha a pista).
  k.add(hill(200, 120, 150, 11, '#356f34', '#4a8a3c', 1, 0.14, 0.6));
  k.add(tintUp(hill(62, 190, 52, 12, '#8b8a82', null, 1, 0.12, 0.3, tf(-14, 0, 6)), '#4a8340', 0.62, 0.9));
  const top = 186;
  k.add(paint(cyl(20, 26, 8, 8), '#cfc8b8', tf(-14, top + 2, 6))); // mirante
  // Estátua: pedestal, túnica afinando para cima, braços abertos ao longo de Z (a cruz lê de frente), cabeça.
  const S = 2.5; const sx = -14; const sz = 6; const y0 = top + 6;
  const stone = '#ecebe2';
  k.add(paint(box(8 * S, 8 * S, 8 * S), '#dcd8cc', tf(sx, y0 + 4 * S, sz)));
  const yb = y0 + 8 * S;
  k.add(lathe([[2.6 * S, 0], [2.2 * S, 10 * S], [1.75 * S, 20 * S], [1.5 * S, 22 * S], [0.6 * S, 23 * S]], 6, stone, tf(sx, yb, sz)));
  const ya = yb + 20.5 * S;
  k.add(paint(box(2.2 * S, 2.6 * S, 28 * S), stone, tf(sx, ya, sz)));
  k.add(paint(box(2.6 * S, 4 * S, 3.4 * S), stone, tf(sx, ya - 1.6 * S, sz))); // peito
  k.add(paint(ico(1.5 * S, 0), stone, tf(sx + 0.2 * S, ya + 3.2 * S, sz, 1, 1.2, 1)));
  return k.model([0, top + 30 * S], 0.05, 11);
}

/** Pão de Açúcar e Urca no mar (skyline), com o bondinho entre os dois. */
function paoDeAcucar(): ReturnType<Kit['model']> {
  const k = new Kit();
  const granite = '#7f7c74';
  // Pão: um pão de pedra alto e inclinado para o mar; Urca: mais baixo e largo. Mata no pé e no topo.
  k.add(tintUp(hill(78, 175, 92, 21, granite, null, 2, 0.06, 0.4, tf(-10, 0, -120, 1, 1, 1, 0, 0, -0.08)), '#4c8a3e', 0.72, 1));
  k.add(tintUp(hill(115, 92, 95, 22, granite, null, 1, 0.1, 0.4, tf(0, 0, 135)), '#4c8a3e', 0.5, 1));
  // Saia de mata no pé dos dois (onde a pedra encontra o mar).
  k.add(hill(150, 26, 230, 23, '#3f7c3a', '#4f8f42', 1, 0.1, 0.8));
  // Bondinho: cabos da praia (no pé da Urca) ao topo da Urca e dali ao Pão; as cabines vermelhas.
  const beach: V3 = [60, 6, 240]; const urcaTop: V3 = [0, 90, 135]; const paoTop: V3 = [-10, 172, -118];
  for (const dx of [-2.2, 2.2]) {
    k.add(cable([beach[0] + dx, beach[1], beach[2]], [urcaTop[0] + dx, urcaTop[1], urcaTop[2]], 1.4, '#2e3238'));
    k.add(cable([urcaTop[0] + dx, urcaTop[1], urcaTop[2]], [paoTop[0] + dx, paoTop[1], paoTop[2]], 1.4, '#2e3238'));
  }
  const car = (t: number, a: V3, b: V3): void => {
    const p: V3 = [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t - 5, a[2] + (b[2] - a[2]) * t];
    k.add(paint(box(7, 5, 10), '#e8402a', tf(p[0], p[1], p[2])));
    k.add(paint(box(7.2, 1.6, 10.2), '#bfe6f2', tf(p[0], p[1] + 0.8, p[2])));
  };
  car(0.45, urcaTop, paoTop); car(0.6, beach, urcaTop);
  k.add(paint(box(18, 10, 14), '#d8d4c8', tf(-4, 88, 140))); // estação da Urca
  k.add(paint(box(16, 9, 14), '#d8d4c8', tf(-12, 168, -112))); // estação do Pão
  return k.model([-20, 180], 0.06, 21);
}

/** MASP: a caixa de vidro suspensa por dois pórticos vermelhos sobre o vão livre (Av. Paulista). */
function masp(): ReturnType<Kit['model']> {
  const k = new Kit();
  const red = '#d4271c';
  const L = 74; const xP = 12.5; const H = 22.5;
  k.add(paint(box(34, 0.5, L + 14), '#b8b4ac', tf(0, 0.25, 0))); // praça do vão livre
  for (const x of [-xP, xP]) {
    for (const z of [-L / 2 - 1.5, L / 2 + 1.5]) k.add(paint(box(3, H, 3), red, tf(x, H / 2, z)));
    k.add(paint(box(3, 3.2, L + 6), red, tf(x, H - 1.6, 0))); // viga do teto
    k.add(paint(box(3, 2.2, L + 6), red, tf(x, 8.2, 0))); // viga de baixo
  }
  // Caixa: laje de concreto e o vidro escuro com caixilhos (janelas acendem à noite).
  k.add(paint(box(26, 1.2, L), '#7c7f84', tf(0, 9.3, 0)));
  k.add(paint(box(26, 1, L), '#7c7f84', tf(0, 19.9, 0)));
  k.facade('office', facadeBox('office', 25.6, 9.4, L - 0.4, '#9aa6b4', tf(0, 14.6, 0)));
  // Escada e o vão: gente não, mas o piso e dois canteiros.
  k.add(paint(box(6, 1.2, 10), '#8a9a5a', tf(10, 0.6, -20)), paint(box(6, 1.2, 10), '#8a9a5a', tf(10, 0.6, 22)));
  k.light(paint(box(0.4, 0.4, L - 6), '#fff0d0', tf(13.2, 8.6, 0)));
  return k.model([0, H], 0, 3);
}

/** Ponte Estaiada (Octávio Frias): mastro em X de 138 m, dois tabuleiros curvos e os leques de cabos (acesos à noite). */
function ponteEstaiada(): ReturnType<Kit['model']> {
  const k = new Kit();
  const white = '#f2f2ee';
  const cross = 38;
  // X no plano da pista (Z-Y): lê de frente para quem passa. Pernas de baixo e braços de cima.
  for (const s of [-1, 1]) {
    k.add(beam([0, -4, s * 20], [0, cross, 0], 5.5, white, 4));
    k.add(beam([0, cross, 0], [0, 138, -s * 27], 5, white, 3.6));
  }
  k.blink(paint(sphere(1.4, 6, 4), '#ff3030', tf(0, 139, -27)), paint(sphere(1.4, 6, 4), '#ff3030', tf(0, 139, 27)));
  // Tabuleiros: curvos em planta (um para cada lado), descendo às pontas; pilares sob cada trecho.
  const N = 10; const half = 150;
  for (const [dx, h, bend] of [[0, 21, 16], [0, 27, -16]] as Array<[number, number, number]>) {
    const pts: V3[] = [];
    for (let i = 0; i <= N; i++) {
      const t = (i / N) * 2 - 1;
      pts.push([dx + bend * t * t, h * (1 - Math.pow(Math.abs(t), 5)) + 0.5, t * half]);
    }
    for (let i = 0; i < N; i++) {
      k.add(beam(pts[i], pts[i + 1], 11, '#c9c9c4', 2.4));
      const m = pts[i + 1];
      if (i < N - 1 && m[1] > 4) k.add(paint(box(2.4, m[1], 2.4), '#b8b8b2', tf(m[0], m[1] / 2, m[2])));
      k.light(paint(box(0.3, 0.3, Math.hypot(pts[i + 1][2] - pts[i][2], pts[i + 1][0] - pts[i][0])), '#fff2c8', tf((pts[i][0] + m[0]) / 2 + 5.6, (pts[i][1] + m[1]) / 2 + 1.4, (pts[i][2] + m[2]) / 2, 1, 1, 1, 0, Math.atan2(m[0] - pts[i][0], m[2] - pts[i][2]), 0)));
    }
    // Leques de cabos dos braços de cima até o tabuleiro (os dois lados do mastro).
    for (const s of [-1, 1]) {
      for (let c = 0; c < 9; c++) {
        const u = 0.42 + (c / 8) * 0.56;
        const top: V3 = [0, cross + (138 - cross) * u, -s * 27 * u];
        const t = 0.12 + (c / 8) * 0.62;
        const at: V3 = [dx + bend * t * t, h * (1 - Math.pow(t, 5)) + 1.5, s * t * half];
        k.light(cable(top, at, 0.7, '#cfe0ff'));
      }
    }
  }
  return k.model([0, 140], 0, 4);
}

/** Igreja barroca de Ouro Preto: duas torres redondas com cúpula, frontão curvo, pedra-sabão nas quinas. */
function igrejaBarroca(): ReturnType<Kit['model']> {
  const k = new Kit();
  const fx = 10; // fachada
  k.add(paint(box(34, 0.8, 30), STONE, tf(fx - 4, 0.4, 0))); // adro
  k.add(paint(box(6, 0.6, 14), STONE, tf(fx + 5, 0.3, 0)), paint(box(4, 1.2, 12), STONE, tf(fx + 3, 0.6, 0)));
  // Nave e capela-mor (mais baixa), telhado de barro.
  k.add(paint(box(30, 13, 14), WHITE, tf(fx - 15, 6.5, 0)));
  k.add(paint(gable(14, 5.5, 30, 0.6), TILE, tf(fx - 15, 13, 0, 1, 1, 1, 0, Math.PI / 2, 0)));
  k.add(paint(box(10, 10, 10), WHITE, tf(fx - 34, 5, 0)), paint(gable(10, 4, 10, 0.5), TILE, tf(fx - 34, 10, 0, 1, 1, 1, 0, Math.PI / 2, 0)));
  // Frontispício: parede da fachada mais alta, frontão curvo (meio cilindro) e cruz.
  k.add(paint(box(2, 17, 14), WHITE, tf(fx, 8.5, 0)));
  k.add(paint(cyl(5.2, 5.2, 2, 10, false), WHITE, tf(fx, 17, 0, 1, 1, 1, 0, 0, Math.PI / 2)));
  k.add(paint(box(2.3, 0.8, 14.6), SOAP, tf(fx + 0.1, 17, 0)));
  k.add(paint(box(0.6, 3.6, 0.6), SOAP, tf(fx, 23.6, 0)), paint(box(0.6, 0.6, 2.2), SOAP, tf(fx, 24.4, 0)));
  // Portada de pedra-sabão, porta e óculo.
  k.add(paint(box(0.5, 7.4, 4.8), SOAP, tf(fx + 1.1, 3.7, 0)), win(fx + 1.35, 2.8, 0, 2.8, 5.2, '#4a2e1a'));
  k.add(paint(cyl(1.5, 1.5, 0.6, 8), SOAP, tf(fx + 1.1, 12.2, 0, 1, 1, 1, 0, 0, Math.PI / 2)));
  k.add(win(fx + 1.15, 9, -4, 1.4, 2.4), win(fx + 1.15, 9, 4, 1.4, 2.4));
  // Torres: cilindro branco com cintas de pedra, sineira, cúpula bulbosa e cruz.
  for (const z of [-8.6, 8.6]) {
    k.add(paint(cyl(3, 3.2, 20, 10), WHITE, tf(fx - 1, 10, z)));
    for (const y of [0.4, 7, 13.6, 20]) k.add(paint(cyl(3.35, 3.35, 0.8, 10), SOAP, tf(fx - 1, y, z)));
    k.add(win(fx + 2.05, 17, z, 1.4, 3.2, '#1e2226'), win(fx + 1.4, 9, z, 1, 2));
    k.add(lathe([[3.2, 0], [3.4, 1.2], [2.6, 3.4], [1, 4.8], [0.6, 5.6], [0.9, 6.2], [0.05, 7.6]], 10, WHITE, tf(fx - 1, 20.4, z)));
    k.add(paint(box(0.4, 2.6, 0.4), SOAP, tf(fx - 1, 29, z)), paint(box(0.4, 0.4, 1.6), SOAP, tf(fx - 1, 29.4, z)));
    k.light(paint(box(0.3, 1.6, 0.9), LAMP, tf(fx + 2.15, 16.8, z)));
  }
  // Quinas (cunhais) de pedra.
  for (const z of [-7, 7]) k.add(paint(box(0.8, 17, 0.8), SOAP, tf(fx + 0.8, 8.5, z)));
  k.light(paint(box(0.4, 0.6, 0.4), LAMP, tf(fx + 1.5, 6, -3)), paint(box(0.4, 0.6, 0.4), LAMP, tf(fx + 1.5, 6, 3)));
  return k.model([0, 30], 0.03, 5);
}

/** Casario colonial: sobrados geminados coloridos subindo a ladeira, telhado de barro, janelas que acendem. */
function casarioColonial(): ReturnType<Kit['model']> {
  const k = new Kit();
  const walls = ['#f2c94c', '#6f9ad8', '#e98ba0', '#83c27f', '#ffffff', '#f19a52', '#c9a0dc'];
  const W = 8; const n = 7; const D = 11;
  for (let i = 0; i < n; i++) {
    const z = (i - (n - 1) / 2) * W;
    const step = i * 0.7;
    const floors = hash2(i, 41) < 0.35 ? 3 : 2;
    const h = floors * 3.6;
    const wall = walls[(i * 3) % walls.length];
    // Alicerce de pedra (cobre o declive) e a casa com a fachada de janelas.
    k.add(paint(box(D, 3 + step, W), STONE, tf(0, (step - 3) / 2 + step / 2, z)));
    k.facade('house', facadeBox('house', D, h, W - 0.1, wall, tf(0, step + h / 2, z), hash2(i, 43) * 6));
    // Cunhais e cimalha brancos, porta, telhado de duas águas com beiral.
    for (const dz of [-W / 2 + 0.25, W / 2 - 0.25]) k.add(paint(box(0.5, h, 0.5), WHITE, tf(D / 2 + 0.05, step + h / 2, z + dz)));
    k.add(paint(box(0.7, 0.5, W), WHITE, tf(D / 2 + 0.1, step + h - 0.2, z)));
    k.add(win(D / 2 + 0.1, step + 1.3, z + (hash2(i, 45) < 0.5 ? -1.8 : 1.8), 1.4, 2.6, '#5a3a22'));
    k.add(paint(gable(D, 3, W, 0.6), TILE, tf(0, step + h, z, 1, 1, 1, 0, 0, 0)));
    // Varanda de ferro no segundo andar de algumas.
    if (hash2(i, 47) < 0.5) k.add(paint(box(1, 0.15, W * 0.6), '#2a2a2a', tf(D / 2 + 0.5, step + 3.8, z)), paint(box(0.08, 1, W * 0.6), '#2a2a2a', tf(D / 2 + 1, step + 4.3, z)));
    k.light(paint(box(0.4, 0.5, 0.3), LAMP, tf(D / 2 + 0.4, step + 2.8, z)));
  }
  // Calçada de pé de moleque na frente.
  k.add(paint(box(4, 0.4, n * W + 2), '#8a8278', tf(D / 2 + 2, 0.2, 0)));
  return k.model([-3, 14], 0.03, 7);
}

/** Convento da Penha (Vila Velha): o convento branco no alto do penhasco de granito, mata no pé. */
function conventoPenha(): ReturnType<Kit['model']> {
  const k = new Kit();
  k.add(hill(150, 55, 120, 31, '#3a7838', '#4a8a40', 1, 0.12, 0.5));
  const rock = hill(64, 150, 54, 32, '#8a8780', null, 1, 0.1, 0.2);
  tintUp(rock, '#4f8a40', 0.75, 0.8);
  k.add(rock);
  const y = 146;
  k.add(paint(cyl(30, 36, 8, 8), '#8a8780', tf(0, y - 2, 0)));
  // Claustro (quadra branca com telhado), igreja com a torre e a escadaria.
  k.add(paint(box(18, 9, 26), WHITE, tf(-4, y + 4.5, 0)), paint(hip(19, 4, 27), TILE, tf(-4, y + 9, 0)));
  k.add(paint(box(20, 11, 9), WHITE, tf(4, y + 5.5, -14)), paint(gable(9, 4, 20, 0.4), TILE, tf(4, y + 11, -14, 1, 1, 1, 0, Math.PI / 2, 0)));
  k.add(paint(box(5, 18, 5), WHITE, tf(14, y + 9, -14)), paint(hip(5.6, 5, 5.6), TILE, tf(14, y + 18, -14)));
  k.add(win(16.6, y + 14, -14, 1.4, 2.4, '#202428'), win(14.6, y + 3, -14, 2.2, 4, '#5a3a22'));
  for (let i = 0; i < 4; i++) k.add(win(5.1, y + 4, -10 + i * 6, 1.4, 2.4));
  k.light(paint(box(0.3, 1.2, 0.8), LAMP, tf(16.7, y + 14, -14)));
  return k.model([0, y + 20], 0.04, 31);
}

/** Terceira Ponte (Vitória): o tabuleiro sobe em arco sobre a baía, apoiado em pares de pilares. */
function terceiraPonte(): ReturnType<Kit['model']> {
  const k = new Kit();
  const half = 260; const N = 18;
  const y = (t: number): number => 8 + 64 * (1 - t * t);
  for (let i = 0; i < N; i++) {
    const t0 = (i / N) * 2 - 1; const t1 = ((i + 1) / N) * 2 - 1;
    k.add(beam([0, y(t0), t0 * half], [0, y(t1), t1 * half], 16, '#d6d5ce', 3));
    k.add(beam([7.6, y(t0) + 2, t0 * half], [7.6, y(t1) + 2, t1 * half], 0.4, '#8a8d92', 1));
    k.add(beam([-7.6, y(t0) + 2, t0 * half], [-7.6, y(t1) + 2, t1 * half], 0.4, '#8a8d92', 1));
    if (i > 0) {
      const yy = y(t0) - 1.5;
      const w = Math.abs(t0) < 0.15 ? 5 : 3;
      for (const x of [-5, 5]) k.add(paint(box(w, yy + 6, w), '#c4c3bc', tf(x, (yy - 6) / 2, t0 * half)));
      k.add(paint(box(13, 2, w), '#c4c3bc', tf(0, yy - 1, t0 * half)));
      k.light(paint(box(0.4, 0.4, 0.4), LAMP, tf(7.8, y(t0) + 4, t0 * half)));
    }
  }
  return k.model([-6, 75], 0, 9);
}

// ───────────────────────────── Sul ─────────────────────────────

/** Serra Verde Express: o trem sobre o viaduto de pedra em arcos (Serra do Mar, PR). */
function tremSerraVerde(): ReturnType<Kit['model']> {
  const k = new Kit();
  const L = 160; const H = 34; const spans = 6; const pierW = 5;
  const stone = '#8c7d6a';
  const span = L / spans;
  // Encostas verdes nas duas cabeceiras (o viaduto atravessa uma grota).
  k.add(hill(30, 40, 40, 41, '#3a7a36', '#4a8a3c', 1, 0.12, 0.6, tf(0, 0, -L / 2 - 20)));
  k.add(hill(30, 40, 40, 42, '#3a7a36', '#4a8a3c', 1, 0.12, 0.6, tf(0, 0, L / 2 + 20)));
  for (let i = 0; i <= spans; i++) {
    const z = -L / 2 + i * span;
    k.add(paint(box(8, H + 24, pierW), stone, tf(0, (H - 24) / 2, z)));
  }
  // Arcos: o intradorso em semicírculo (vigas) e o tímpano cheio por cima.
  for (let i = 0; i < spans; i++) {
    const zc = -L / 2 + (i + 0.5) * span;
    const r = (span - pierW) / 2;
    const yc = H - 9;
    let prev: V3 | null = null;
    for (let a = 0; a <= 6; a++) {
      const ang = Math.PI - (a / 6) * Math.PI;
      const p: V3 = [0, yc + Math.sin(ang) * r * 0.9, zc + Math.cos(ang) * r];
      if (prev) k.add(beam(prev, p, 1.6, '#6e6152', 8.2));
      prev = p;
    }
  }
  k.add(paint(box(8, 8, L + pierW), stone, tf(0, H - 4, 0)));
  k.add(paint(box(9, 1, L + 4), '#6e6152', tf(0, H + 0.5, 0)));
  k.add(paint(box(0.4, 1, L), '#5a5a5a', tf(4.2, H + 1.5, 0)), paint(box(0.4, 1, L), '#5a5a5a', tf(-4.2, H + 1.5, 0)));
  // Trem: locomotiva amarela com faixa verde e quatro carros verdes de janela creme.
  const yt = H + 1;
  let z = -52;
  k.add(paint(box(3.2, 4.2, 16), '#e0a820', tf(0, yt + 2.5, z)), paint(box(3.3, 0.8, 16.1), '#2f6b46', tf(0, yt + 1.8, z)));
  k.add(paint(box(3, 1.4, 4), '#1e2a30', tf(0, yt + 4.2, z - 5.5)));
  z += 17.5;
  for (let c = 0; c < 4; c++) {
    k.add(paint(box(3.1, 3.6, 17), '#2f6b46', tf(0, yt + 2.4, z + c * 18)));
    k.add(paint(box(3.2, 1.1, 16), '#efe4c4', tf(0, yt + 3, z + c * 18)));
    for (let w = 0; w < 5; w++) k.add(paint(box(3.25, 0.8, 1.6), '#2a3238', tf(0, yt + 3, z + c * 18 - 6 + w * 3)));
    k.add(paint(box(3.2, 0.5, 17.2), '#3a3a3a', tf(0, yt + 4.4, z + c * 18)));
  }
  return k.model([-24, H + 6], 0.04, 41);
}

/** Estufa do Jardim Botânico de Curitiba: vidro e ferro branco, cúpula central e alas em abóbada, jardim francês. */
function estufaJardimBotanico(): ReturnType<Kit['model']> {
  const k = new Kit();
  const glass = '#bfe5ee'; const iron = '#fbfbf6';
  // Corpo central: base de vidro e a cúpula alongada; alas com abóbada em Z.
  k.add(paint(box(14, 9, 14), glass, tf(0, 4.5, 0)));
  k.add(paint(sphere(1, 10, 6, 0, Math.PI * 2, 0, Math.PI / 2), glass, tf(0, 9, 0, 7.2, 9, 7.2)));
  k.add(paint(cyl(1.2, 1.4, 2.4, 8), iron, tf(0, 18.8, 0)), paint(cone(1.5, 2.2, 8), iron, tf(0, 21, 0)));
  for (let a = 0; a < 8; a++) {
    const ang = (a / 8) * Math.PI * 2;
    let prev: V3 | null = null;
    for (let s = 0; s <= 4; s++) {
      const t = (s / 4) * Math.PI / 2;
      const p: V3 = [Math.cos(ang) * 7.3 * Math.cos(t), 9 + 9.05 * Math.sin(t), Math.sin(ang) * 7.3 * Math.cos(t)];
      if (prev) k.add(beam(prev, p, 0.35, iron));
      prev = p;
    }
  }
  for (const s of [-1, 1]) {
    const zc = s * 12.5;
    k.add(paint(box(11, 6, 11), glass, tf(0, 3, zc)));
    k.add(paint(cyl(5.5, 5.5, 11, 8), glass, tf(0, 6, zc, 1, 0.9, 1, Math.PI / 2, 0, 0)));
    for (let r = 0; r <= 4; r++) k.add(paint(cyl(5.65, 5.65, 0.3, 8, true), iron, tf(0, 6, zc - 5.4 + r * 2.7, 1, 0.9, 1, Math.PI / 2, 0, 0)));
    for (const x of [-5.5, 5.5]) for (let r = 0; r <= 4; r++) k.add(paint(box(0.3, 6, 0.3), iron, tf(x, 3, zc - 5.4 + r * 2.7)));
  }
  for (const x of [-7, 7]) for (let r = 0; r <= 4; r++) k.add(paint(box(0.3, 9, 0.3), iron, tf(x, 4.5, -7 + r * 3.5)));
  k.add(paint(box(15, 0.6, 37), iron, tf(0, 0.3, 0)));
  // Jardim francês na frente: caminho branco, canteiros geométricos (cerca-viva e flores) e a fonte.
  const gx = 22;
  k.add(paint(box(28, 0.2, 40), '#e8e2d2', tf(gx, 0.1, 0)));
  for (const s of [-1, 1]) for (const r of [0, 1]) {
    const cx = gx - 7 + r * 13; const cz = s * 11;
    k.add(paint(box(9, 0.9, 14), '#3f8a3a', tf(cx, 0.45, cz)));
    k.add(paint(box(5.5, 1.0, 9), r ? '#e04a3a' : '#f2c33a', tf(cx, 0.5, cz)));
  }
  k.add(paint(cyl(3.2, 3.4, 0.8, 10), '#d8d2c2', tf(gx, 0.4, 0)), paint(cyl(2.8, 2.8, 0.85, 10), '#6ab4d8', tf(gx, 0.45, 0)));
  k.add(paint(cyl(0.4, 0.6, 2.2, 6), '#d8d2c2', tf(gx, 1.5, 0)));
  for (const z of [-6, 6]) k.light(paint(box(0.4, 0.5, 0.4), LAMP, tf(9, 3.2, z)), paint(box(0.12, 2.8, 0.12), '#2a2a2a', tf(9, 1.4, z)));
  return k.model([0, 22], 0.02, 13);
}

/** Ponte Hercílio Luz (Florianópolis): pênsil, duas torres treliçadas, a corrente que desce até o meio do vão. */
function ponteHercilioLuz(): ReturnType<Kit['model']> {
  const k = new Kit();
  const steel = '#e6ebee'; const deckY = 32; const tz = 170; const top = 78; const half = 250;
  // Tabuleiro com treliça baixa e os vãos de acesso sobre pilares.
  k.add(paint(box(14, 2.4, half * 2), '#c8ccd0', tf(0, deckY, 0)));
  for (const x of [-7, 7]) k.add(paint(box(0.8, 4, half * 2), steel, tf(x, deckY + 3, 0)));
  for (const z of [-half + 10, -half + 45, half - 45, half - 10]) for (const x of [-5, 5]) k.add(paint(box(3, deckY + 8, 3), '#b8bcc0', tf(x, (deckY - 8) / 2, z)));
  // Torres: duas pernas treliçadas por torre, com travessas em X.
  for (const zt of [-tz, tz]) {
    for (const x of [-7.5, 7.5]) k.add(paint(box(2.6, top + 8, 3.4), steel, tf(x, (top - 8) / 2, zt)));
    k.add(paint(box(18, 2.4, 4), steel, tf(0, top, zt)), paint(box(18, 1.6, 4), steel, tf(0, deckY + 6, zt)));
    for (const [y0, y1] of [[deckY + 7, 56], [56, top - 1]]) {
      k.add(beam([-7.5, y0, zt], [7.5, y1, zt], 0.9, steel), beam([7.5, y0, zt], [-7.5, y1, zt], 0.9, steel));
    }
    k.add(paint(box(18, 8, 8), '#a8acb0', tf(0, -3, zt)));
  }
  // Correntes: da torre descem até o banzo do meio (forma da Hercílio Luz) e os tirantes; luzes ao longo (anoitecer).
  const N = 12;
  const chainY = (z: number): number => deckY + 5 + (top - deckY - 5) * Math.pow(Math.abs(z) / tz, 1.6);
  for (const x of [-7.5, 7.5]) {
    for (let i = 0; i < N; i++) {
      const z0 = -tz + (i / N) * 2 * tz; const z1 = -tz + ((i + 1) / N) * 2 * tz;
      k.add(beam([x, chainY(z0), z0], [x, chainY(z1), z1], 1.2, steel, 1.2));
      if (i > 0) k.add(cable([x, chainY(z0), z0], [x, deckY + 4, z0], 0.35, steel));
      k.light(paint(box(0.7, 0.7, 0.7), '#fff1c0', tf(x * 1.08, chainY(z0) + 0.9, z0)));
    }
    for (const s of [-1, 1]) {
      k.add(beam([x, top, s * tz], [x, deckY + 3, s * (half - 6)], 1.2, steel, 1.2));
      for (let c = 1; c < 4; c++) {
        const t = c / 4;
        k.light(paint(box(0.7, 0.7, 0.7), '#fff1c0', tf(x * 1.08, top + (deckY + 3 - top) * t + 0.9, s * (tz + (half - 6 - tz) * t))));
      }
    }
  }
  return k.model([-8, top], 0, 15);
}

/** Igreja açoriana (Santo Antônio de Lisboa, SC): branca com quinas, molduras e porta azuis, torre no meio da fachada. */
function igrejaAcoriana(): ReturnType<Kit['model']> {
  const k = new Kit();
  const blue = '#2b5fb4'; const fx = 11;
  k.add(paint(box(26, 0.6, 18), STONE, tf(1, 0.3, 0)));
  k.add(paint(box(22, 9, 12), WHITE, tf(0, 4.5, 0)), paint(gable(12, 4.4, 22, 0.5), TILE, tf(0, 9, 0, 1, 1, 1, 0, Math.PI / 2, 0)));
  // Frontão triangular com moldura azul e a torre central na frente.
  k.add(paint(gable(12, 4.6, 1, 0), WHITE, tf(fx, 9, 0, 1, 1, 1, 0, Math.PI / 2, 0)));
  k.add(beam([fx + 0.3, 9, -6.2], [fx + 0.3, 13.6, 0], 0.6, blue), beam([fx + 0.3, 9, 6.2], [fx + 0.3, 13.6, 0], 0.6, blue));
  k.add(paint(box(0.6, 0.6, 12.6), blue, tf(fx + 0.3, 9, 0)));
  k.add(paint(box(4.4, 20, 4.4), WHITE, tf(fx + 1.5, 10, 0)));
  k.add(paint(hip(5, 3.6, 5), blue, tf(fx + 1.5, 20, 0)), paint(box(0.3, 2.2, 0.3), '#2a2a2a', tf(fx + 1.5, 24.6, 0)), paint(box(0.3, 0.3, 1.2), '#2a2a2a', tf(fx + 1.5, 24.9, 0)));
  for (const y of [0.3, 10, 19.7]) k.add(paint(box(4.9, 0.6, 4.9), blue, tf(fx + 1.5, y, 0)));
  for (const dz of [-2, 2]) k.add(paint(box(0.5, 20, 0.5), blue, tf(fx + 3.6, 10, dz)));
  for (const z of [-6, 6]) k.add(paint(box(0.6, 9, 0.6), blue, tf(fx + 0.1, 4.5, z)));
  // Porta e janelas com moldura azul.
  k.add(paint(box(0.3, 4.6, 2.8), blue, tf(fx + 3.75, 2.3, 0)), win(fx + 3.8, 2.1, 0, 2, 3.8, '#1e3e7a'));
  k.add(win(fx + 3.8, 14.6, 0, 1.4, 2.4, '#20252c'), paint(box(0.25, 2.8, 1.8), blue, tf(fx + 3.72, 14.6, 0)));
  for (const z of [-4, 4]) k.add(paint(box(0.3, 2.6, 1.6), blue, tf(fx + 0.15, 5.5, z)), win(fx + 0.3, 5.5, z, 1.1, 2.1));
  for (const x of [-6, 0, 6]) k.add(paint(box(1.6, 2.6, 0.3), blue, tf(x, 5.5, 6.1)), paint(box(1.6, 2.6, 0.3), blue, tf(x, 5.5, -6.1)));
  k.light(paint(box(0.3, 1.4, 0.8), LAMP, tf(fx + 3.9, 14.6, 0)), paint(box(0.4, 0.5, 0.4), LAMP, tf(fx + 3.9, 5.2, -1.8)));
  return k.model([0, 25], 0.02, 17);
}

/** Catedral de Pedra (Canela, RS): gótica de basalto escuro, uma torre com flecha de 65 m, vitrais acesos. */
function catedralDePedra(): ReturnType<Kit['model']> {
  const k = new Kit();
  const stone = '#6a6460'; const dark = '#3e4248'; const fx = 22;
  k.add(paint(box(48, 0.6, 26), '#8a847c', tf(0, 0.3, 0)));
  k.add(paint(box(40, 15, 18), stone, tf(-2, 7.5, 0)), paint(gable(18, 11, 40, 0.4), dark, tf(-2, 15, 0, 1, 1, 1, 0, Math.PI / 2, 0)));
  // Contrafortes ao longo da nave, com pináculos.
  for (let i = 0; i < 6; i++) for (const s of [-1, 1]) {
    const x = -20 + i * 7.2;
    k.add(paint(box(1.6, 13, 1.6), stone, tf(x, 6.5, s * 9.6)), paint(cone(0.9, 3, 4), stone, tf(x, 14.5, s * 9.6, 1, 1, 1, 0, Math.PI / 4, 0)));
    if (i < 5) k.add(win(x + 3.6, 8, s * 9.05, 2.2, 7, '#2a2e36'));
    if (i < 5) k.light(paint(box(0.2, 5, 1.4), i % 2 ? '#ffb45a' : '#e88a4a', tf(x + 3.6, 8, s * 9.2)));
  }
  // Torre: quatro estágios afinando, a flecha piramidal e os pináculos dos cantos.
  const tx = fx - 3;
  k.add(paint(box(8, 40, 8), stone, tf(tx, 20, 0)), paint(box(9, 1, 9), dark, tf(tx, 40, 0)));
  k.add(paint(box(6.6, 8, 6.6), stone, tf(tx, 44.5, 0)));
  k.add(paint(hip(6.6, 18, 6.6), dark, tf(tx, 48.5, 0)), paint(box(0.4, 3, 0.4), '#2a2a2a', tf(tx, 67.5, 0)));
  for (const dx of [-3.6, 3.6]) for (const dz of [-3.6, 3.6]) k.add(paint(cone(0.8, 5, 4), stone, tf(tx + dx, 43, dz, 1, 1, 1, 0, Math.PI / 4, 0)));
  // Portal ogival, rosácea e as janelas da torre.
  k.add(paint(box(1.6, 9, 6), '#5a5450', tf(tx + 4.4, 4.5, 0)), win(tx + 5.25, 3.6, 0, 3.4, 7, '#2a1e18'));
  k.add(paint(cone(3, 3, 4), '#5a5450', tf(tx + 4.4, 10.5, 0, 0.4, 1, 1, 0, Math.PI / 4, 0)));
  k.add(paint(cyl(2.4, 2.4, 0.5, 10), '#3a3e46', tf(tx + 4.2, 18, 0, 1, 1, 1, 0, 0, Math.PI / 2)));
  k.light(paint(cyl(1.8, 1.8, 0.3, 10), '#ffb45a', tf(tx + 4.45, 18, 0, 1, 1, 1, 0, 0, Math.PI / 2)));
  for (const y of [28, 36, 45]) k.add(win(tx + 4.05, y, 0, 1.6, 4.5, '#20242a'));
  k.light(paint(box(0.2, 3.4, 1.1), '#ffcf80', tf(tx + 4.15, 36, 0)));
  return k.model([0, 68], 0.06, 19);
}

/** Cuia de chimarrão gigante com a bomba (monumento gaúcho). */
function cuiaChimarrao(): ReturnType<Kit['model']> {
  const k = new Kit();
  k.add(paint(cyl(5.4, 6, 1.4, 8), '#9a9488', tf(0, 0.7, 0)), paint(cyl(4.6, 5, 0.6, 8), '#b8b2a4', tf(0, 1.7, 0)));
  const y = 2;
  k.add(lathe([[0.1, 0], [2.4, 0.2], [3.6, 1.4], [4.0, 3.0], [3.7, 4.6], [2.9, 5.8], [2.35, 6.6], [2.55, 7.4], [3.2, 8.4], [3.4, 9]], 10, '#7d5a2c', tf(0, y, 0)));
  k.add(lathe([[3.25, 0], [3.5, 0], [3.5, 0.8], [3.3, 0.8]], 10, '#c9ced4', tf(0, y + 8.4, 0)));
  // Erva: monte verde inclinado na boca, com o pé da bomba enfiado.
  k.add(paint(cone(3.1, 1.4, 10), '#5f8f2a', tf(0, y + 8.9, 0, 1, 1, 1, 0.2, 0, 0)));
  k.add(beam([0.6, y + 7.5, 0.2], [-1.9, y + 14.5, 0.6], 0.42, '#d8dce0'));
  k.add(beam([-1.9, y + 14.5, 0.6], [-2.5, y + 15, 0.7], 0.42, '#c9a040'));
  return k.model([0, 17], 0.04, 23);
}

/** Araucárias: três pinheiros-do-paraná de tamanhos diferentes, galhos em candelabro e copa em taça. */
function araucaria(): ReturnType<Kit['model']> {
  const k = new Kit();
  const trees: Array<[number, number, number]> = [[0, 0, 26], [-7, -11, 21], [-4, 12, 17]];
  trees.forEach(([x, z, H], t) => {
    const seed = 50 + t;
    k.add(paint(cyl(0.38, 0.62, H, 7), '#5c4838', tf(x, H / 2, z)));
    const n = 9;
    for (let b = 0; b < n; b++) {
      const a = (b / n) * Math.PI * 2 + hash2(seed, b) * 0.5;
      const yb = H - 3.6 + (b % 3) * 1.1;
      const out = H * 0.22 + hash2(seed, b + 20) * 1.4;
      const p0: V3 = [x, yb, z];
      const p1: V3 = [x + Math.cos(a) * out * 0.75, yb + 0.6, z + Math.sin(a) * out * 0.75];
      const p2: V3 = [x + Math.cos(a) * out, yb + 2.4, z + Math.sin(a) * out];
      k.add(beam(p0, p1, 0.26, '#5c4838'), beam(p1, p2, 0.22, '#5c4838'));
      k.add(paint(jitter(dodeca(1), 0.12, seed + b), b % 2 ? '#2c5a34' : '#336a3a', tf(p2[0], p2[1] + 0.5, p2[2], 1.9, 0.75, 1.9)));
    }
    k.add(paint(jitter(dodeca(1), 0.1, seed + 30), '#2f6036', tf(x, H + 0.6, z, 2.2, 0.8, 2.2)));
    // Galhos secos mais abaixo (a araucária perde os de baixo).
    for (let b = 0; b < 3; b++) {
      const a = hash2(seed, b + 40) * 6.28; const yb = H * (0.45 + b * 0.12);
      k.add(beam([x, yb, z], [x + Math.cos(a) * 2.4, yb + 0.8, z + Math.sin(a) * 2.4], 0.14, '#4a3a2e'));
    }
  });
  // Capim e uma moita no pé.
  k.add(paint(jitter(ico(1, 0), 0.2, 59), '#5a8a3a', tf(-2, 0.3, 2, 6, 1, 5)));
  return k.model([0, 28], 0.06, 51);
}

// ───────────────────────────── Centro-Oeste ─────────────────────────────

/** Congresso Nacional: a plataforma, as duas torres (janelas que acendem), a cúpula do Senado e a cuia da Câmara. */
function congressoNacional(): ReturnType<Kit['model']> {
  const k = new Kit();
  const w = '#f0f0ec';
  // Espelho d'água na frente e o gramado.
  k.add(paint(box(40, 0.3, 230), '#7aa84a', tf(46, 0.15, 0)));
  k.add(paint(box(24, 0.5, 200), '#3f78b0', tf(44, 0.3, 0)));
  // Plataforma baixa com a faixa de vidro.
  k.add(paint(box(64, 10, 200), w, tf(0, 5, 0)));
  k.facade('office', facadeBox('office', 64.4, 4, 196, '#8ca0b4', tf(0, 4.4, 0)));
  k.add(paint(box(66, 1, 202), '#dcdcd6', tf(0, 10.4, 0)));
  // Rampa que sobe da frente à cobertura.
  k.add(beam([40, 0.2, 18], [32, 10.6, 18], 0.8, '#dcdcd6', 10));
  // Torres gêmeas (28 andares) com a ligação no meio, atrás da plataforma.
  for (const z of [-12.5, 12.5]) {
    k.facade('office', facadeBox('office', 10, 100, 20, '#e8eef4', tf(-22, 50, z)));
    k.add(paint(box(10.6, 1.4, 20.6), w, tf(-22, 100.7, z)));
  }
  k.facade('office', facadeBox('office', 8, 12, 5, '#e8eef4', tf(-22, 48, 0)));
  k.blink(paint(sphere(0.8, 6, 4), '#ff3030', tf(-22, 102, -12.5)), paint(sphere(0.8, 6, 4), '#ff3030', tf(-22, 102, 12.5)));
  // Cúpula do Senado e a cuia da Câmara (aberta para cima).
  k.add(paint(sphere(1, 14, 6, 0, Math.PI * 2, 0, Math.PI / 2), w, tf(0, 10.9, -58, 19, 13, 19)));
  k.add(lathe([[5, 0], [9, 3.5], [18, 9], [28, 13.5], [33, 15], [32, 15.4]], 16, w, tf(0, 10.9, 58)));
  k.add(paint(cyl(31, 31, 0.6, 16, false), '#c8c8c2', tf(0, 25.4, 58)));
  k.light(paint(box(0.3, 0.4, 190), '#fff0d0', tf(32.2, 9.4, 0)));
  return k.model([0, 100], 0, 27);
}

/** Catedral de Brasília: coroa de 16 pilares curvos (hiperboloide), vidro azul entre eles, campanário ao lado. */
function catedralBrasilia(): ReturnType<Kit['model']> {
  const k = new Kit();
  const w = '#f4f4f0';
  k.add(paint(cyl(36, 36, 0.4, 16), '#c8c4b8', tf(0, 0.2, 0)));
  k.add(lathe([[29, 0.3], [21, 10], [14.5, 20], [11, 29], [10.6, 33]], 16, '#8cc0dc'));
  for (let i = 0; i < 16; i++) {
    const a = (i / 16) * Math.PI * 2;
    const c = Math.cos(a); const s = Math.sin(a);
    const P = (r: number, y: number): V3 => [c * r, y, s * r];
    const pts = [P(31, 0), P(22, 10.5), P(15, 21), P(11.5, 30), P(12.4, 36), P(15.5, 42)];
    for (let j = 0; j < pts.length - 1; j++) {
      const wdt = j < 3 ? 2.6 - j * 0.4 : 1.4 - (j - 3) * 0.3;
      k.add(beam(pts[j], pts[j + 1], wdt, w, 1.6));
    }
  }
  // Campanário (quatro sinos na lâmina) e o espelho d'água em volta.
  k.add(paint(box(1.6, 20, 1.2), w, tf(14, 10, 40)), paint(box(1.2, 0.8, 6), w, tf(14, 19.2, 40)));
  for (let b = 0; b < 4; b++) k.add(paint(cone(0.7, 1.2, 6), '#b08a3a', tf(14.9, 15.5 - b * 1.7, 40 + (b % 2 ? 1 : -1))));
  k.add(lathe([[34, 0], [42, 0], [42, 0.5], [34, 0.5]], 16, '#4a8ac0'));
  k.light(lathe([[10.4, 33], [11, 33.2], [10.8, 33.5]], 16, '#bfe0ff'), paint(cyl(29.4, 29.4, 0.4, 16, true), '#ffe8c0', tf(0, 1.2, 0)));
  return k.model([0, 42], 0, 29);
}

/** Cachoeira da Chapada dos Veadeiros: paredão de arenito, a queda d'água branca e o poço azul. */
function cachoeiraVeadeiros(): ReturnType<Kit['model']> {
  const k = new Kit();
  const W = 170; const H = 92;
  const wall = jitter(new THREE.BoxGeometry(48, H + 30, W, 3, 6, 8), 0.06, 61);
  const g = paint(wall, '#a8704a', tf(-24, (H - 30) / 2, 0));
  tintUp(g, '#7f8f3a', 0.65, 1);
  k.add(g);
  // Faixas de estrato no paredão.
  for (let i = 0; i < 4; i++) k.add(paint(box(1.2, 3 + (i % 2) * 2, W * 0.9), i % 2 ? '#8a5a3a' : '#c48a5a', tf(1.4, 20 + i * 18, (hash2(61, i) - 0.5) * 12)));
  // Topo de cerrado: tufos verdes.
  for (let i = 0; i < 8; i++) k.add(paint(jitter(ico(1, 0), 0.2, 62 + i), '#5a7f32', tf(-10 - hash2(i, 63) * 24, H + 1.5, -W / 2 + 12 + i * 20, 5, 2.4, 5)));
  // A queda (faixas de água que brilham claras), a espuma e o poço.
  const wz = 8;
  k.add(paint(box(6, 10, 14), '#7a5034', tf(-1, H - 2, wz)));
  for (const [dz, wd] of [[0, 9], [-4, 3], [4.5, 3]] as Array<[number, number]>) k.light(paint(box(0.6, H + 2, wd), dz === 0 ? '#eaf6ff' : '#c2e4f6', tf(2.6 + Math.abs(dz) * 0.1, H / 2, wz + dz)));
  k.add(paint(cyl(26, 28, 1, 12), '#2f86b0', tf(16, 0.5, wz, 1, 1, 0.8)));
  k.add(paint(jitter(ico(1, 1), 0.15, 64), '#f2f8ff', tf(6, 2, wz, 7, 4, 9)));
  for (let i = 0; i < 6; i++) k.add(paint(jitter(dodeca(1), 0.2, 65 + i), '#8a7a6a', tf(14 + (hash2(i, 66) - 0.5) * 30, 0.8, wz + (i - 2.5) * 9, 3, 1.6, 3)));
  return k.model([-10, H], 0.06, 61);
}

/** Buritizal (vereda): quatro buritis de leque com a saia de folhas secas e o capim úmido no pé. */
function buriti(): ReturnType<Kit['model']> {
  const k = new Kit();
  const palms: Array<[number, number, number]> = [[0, 0, 22], [-6, -9, 18], [-3, 10, 25], [-12, 3, 15]];
  palms.forEach(([x, z, H], t) => {
    const seed = 70 + t;
    k.add(paint(cyl(0.42, 0.55, H, 7), '#8a857a', tf(x, H / 2, z)));
    k.add(paint(cone(1.6, 3.2, 7), '#8a6a3a', tf(x, H - 1.6, z, 1, -1, 1)));
    const n = 11;
    for (let b = 0; b < n; b++) {
      const a = (b / n) * Math.PI * 2 + hash2(seed, b) * 0.4;
      const up = b % 3 === 0 ? 1.2 : 0.55 + hash2(seed, b + 9) * 0.4;
      const tip: V3 = [x + Math.cos(a) * 2.6 * Math.sin(up), H + 2.6 * Math.cos(up), z + Math.sin(a) * 2.6 * Math.sin(up)];
      k.add(beam([x, H, z], tip, 0.16, '#6a8a3a'));
      // Leque perpendicular ao pecíolo, inclinado para fora.
      const m = new THREE.Matrix4().compose(new THREE.Vector3(...tip), new THREE.Quaternion().setFromEuler(new THREE.Euler(0, -a + Math.PI / 2, -(up - 0.35), 'YXZ')), new THREE.Vector3(1, 1, 1));
      k.add(fanLeaf(3.2, 6, b % 2 ? '#4f8f3a' : '#5ea044', m));
    }
  });
  for (let i = 0; i < 5; i++) k.add(paint(jitter(ico(1, 0), 0.2, 80 + i), i % 2 ? '#5a9a3a' : '#6aa844', tf(-5 + (hash2(i, 81) - 0.5) * 16, 0.4, (i - 2) * 6, 3.2, 1.2, 3.2)));
  return k.model([0, 26], 0.05, 71);
}

/** Gruta do Lago Azul (Bonito): paredão de calcário com mata, a boca escura da gruta e o azul lá dentro. */
function grutaLagoAzul(): ReturnType<Kit['model']> {
  const k = new Kit();
  const W = 56; const H = 30;
  const rock = jitter(new THREE.BoxGeometry(26, H + 8, W, 2, 4, 6), 0.08, 91);
  const g = paint(rock, '#b8ab90', tf(-13, (H - 8) / 2, 0));
  tintUp(g, '#4f8a3a', 0.6, 1);
  k.add(g);
  // Mata por cima e dos lados.
  for (let i = 0; i < 9; i++) k.add(paint(jitter(ico(1, 0), 0.18, 92 + i), i % 2 ? '#2f7a36' : '#3f8a3c', tf(-6 - hash2(i, 93) * 16, H + 2, -W / 2 + 4 + i * 6, 4.5, 3.4, 4.5)));
  for (const s of [-1, 1]) k.add(paint(jitter(ico(1, 0), 0.18, 102 + s), '#2f7a36', tf(2, 5, s * (W / 2 + 3), 6, 7, 5)));
  // Boca da gruta: arco escuro na face, e o azul do lago no fundo (brilha).
  const arch: number[] = [];
  const R = 9; const cx = 0.6; const cy = 9;
  for (let a = 0; a < 8; a++) {
    const t0 = (a / 8) * Math.PI; const t1 = ((a + 1) / 8) * Math.PI;
    arch.push(cx, cy, 0, cx, cy + Math.sin(t0) * R * 1.3, Math.cos(t0) * R, cx, cy + Math.sin(t1) * R * 1.3, Math.cos(t1) * R);
  }
  arch.push(cx, 0.4, -R, cx, 0.4, R, cx, cy, R, cx, 0.4, -R, cx, cy, R, cx, cy, -R);
  k.add(paint(tris(arch), '#12161c'));
  k.light(paint(tris([cx + 0.1, 0.5, -6, cx + 0.1, 0.5, 6, cx + 0.1, 4.2, 4, cx + 0.1, 0.5, -6, cx + 0.1, 4.2, 4, cx + 0.1, 4.2, -4]), '#1f8cff'));
  // Passarela de madeira descendo até a boca.
  k.add(beam([14, 0.6, -10], [2, 1, -3], 2, WOOD, 0.3));
  for (let i = 0; i < 4; i++) k.add(paint(box(0.2, 1.1, 0.2), WOOD, tf(14 - i * 4, 1.2, -10 + i * 2.3)));
  return k.model([0, H], 0.07, 91);
}

/** Ninho de tuiuiú: árvore alta e rala com o ninho de gravetos no topo e um casal de tuiuiús (×1,6). */
function tuiuiuNinho(): ReturnType<Kit['model']> {
  const k = new Kit();
  const bark = '#6a5a48'; const H = 13;
  k.add(paint(cyl(0.55, 0.8, H, 7), bark, tf(0, H / 2, 0)));
  const forks: V3[] = [[2.6, H + 3.4, 0.8], [-2.2, H + 3.2, -1.4], [0.4, H + 3.8, 2.4]];
  for (const f of forks) k.add(beam([0, H - 0.2, 0], f, 0.4, bark));
  for (let b = 0; b < 5; b++) {
    const a = hash2(111, b) * 6.28; const y = 6 + b * 1.4;
    const tip: V3 = [Math.cos(a) * 4, y + 1.8, Math.sin(a) * 4];
    k.add(beam([0, y, 0], tip, 0.2, bark), paint(jitter(ico(1, 0), 0.2, 112 + b), '#5a8a3a', tf(tip[0], tip[1] + 0.4, tip[2], 2.2, 1.3, 2.2)));
  }
  // Ninho: prato largo de gravetos sobre a forquilha.
  const ny = H + 3.4;
  k.add(paint(jitter(cyl(2.9, 1.8, 1.4, 10), 0.12, 113), '#7a5a36', tf(0, ny, 0)));
  k.add(paint(cyl(2.3, 2.3, 0.3, 10), '#5a4428', tf(0, ny + 0.62, 0)));
  // Tuiuiús: corpo branco, pescoço preto com o colar vermelho, bico longo preto (olhando a pista, +X).
  const bird = (x: number, z: number, face: number, S: number): void => {
    const c = Math.cos(face); const s = Math.sin(face);
    const P = (a: number, y: number, b = 0): V3 => [x + a * c - b * s, ny + 0.7 + y, z + a * s + b * c];
    const body = P(0, 1.0 * S);
    k.add(paint(ico(1, 0), '#f6f6f2', tf(body[0], body[1], body[2], 0.55 * S, 0.42 * S, 0.36 * S, 0, -face, 0)));
    k.add(paint(cone(0.34 * S, 0.5 * S, 5), '#1e1e22', tf(...P(-0.55 * S, 0.95 * S), 1, 1, 1, 0, -face, Math.PI / 2)));
    const neck0 = P(0.35 * S, 1.25 * S); const neck1 = P(0.42 * S, 2.1 * S);
    k.add(beam(neck0, neck1, 0.2 * S, '#1e1e22'));
    k.add(paint(cyl(0.16 * S, 0.2 * S, 0.22 * S, 6), '#d42828', tf(neck0[0], neck0[1] + 0.12 * S, neck0[2])));
    k.add(paint(ico(0.18 * S, 0), '#1e1e22', tf(...neck1)));
    k.add(beam(neck1, P(1.25 * S, 2.25 * S), 0.11 * S, '#16161a'));
    for (const b of [-0.12, 0.12]) k.add(beam(P(0, 0.62 * S, b * S), P(0.02 * S, -0.1, b * S), 0.07 * S, '#16161a'));
  };
  bird(0.6, -0.8, 0, 1.6); bird(-0.7, 0.9, 0.9, 1.6);
  return k.model([0, 20], 0.05, 111);
}

/** Portal da Transpantaneira: pórtico de troncos com a tábua, cerca de curral e a estradinha de terra que passa por ele. */
function portalTranspantaneira(): ReturnType<Kit['model']> {
  const k = new Kit();
  const log = '#7a5230'; const span = 13; const H = 8.5;
  k.add(paint(box(40, 0.25, 7), '#a8865a', tf(-14, 0.12, 0))); // estradinha de terra
  for (const s of [-1, 1]) {
    k.add(paint(cyl(0.65, 0.75, H + 0.8, 8), log, tf(0, (H + 0.8) / 2, s * span / 2)));
    k.add(beam([0, 0.2, s * (span / 2 + 2.6)], [0, H * 0.62, s * span / 2], 0.32, log));
  }
  k.add(paint(cyl(0.55, 0.55, span + 3.4, 8), log, tf(0, H + 0.2, 0, 1, 1, 1, Math.PI / 2, 0, 0)));
  k.add(paint(cyl(0.4, 0.4, span, 8), log, tf(0, H - 2.4, 0, 1, 1, 1, Math.PI / 2, 0, 0)));
  // Tábua do nome (escura, com a faixa entalhada clara) presa entre as duas travessas.
  k.add(paint(box(0.35, 1.7, 9.6), '#4e3420', tf(0.3, H - 1.1, 0)), paint(box(0.4, 0.9, 8.4), '#c9a466', tf(0.35, H - 1.1, 0)));
  for (const dz of [-3.3, -1.2, 1.2, 3.3]) k.add(paint(box(0.45, 0.5, 1.1), '#4e3420', tf(0.4, H - 1.1, dz)));
  // Telhadinho de duas águas por cima.
  k.add(paint(gable(2.6, 1.2, span + 4, 0.2), '#7a4a2a', tf(0, H + 0.6, 0)));
  // Cerca de curral dos dois lados.
  for (const s of [-1, 1]) {
    for (let i = 0; i < 5; i++) k.add(paint(box(0.25, 1.6, 0.25), log, tf(0, 0.8, s * (span / 2 + 2.5 + i * 3))));
    for (const y of [0.6, 1.3]) k.add(paint(box(0.12, 0.2, 12.4), '#8a6240', tf(0.1, y, s * (span / 2 + 8.5))));
  }
  for (const s of [-1, 1]) k.light(paint(box(0.35, 0.5, 0.35), LAMP, tf(0.8, H - 3.2, s * (span / 2 - 0.2))));
  return k.model([0, H + 2], 0.06, 121);
}

export const LANDMARKS_BRASIL_CENTRO_SUL: LandmarkRegistry = {
  // Sudeste
  cristo_redentor: { build: cristoRedentor, place: 'skyline', side: 'land', perLap: 2 },
  pao_de_acucar: { build: paoDeAcucar, place: 'skyline', side: 'sea', perLap: 2 },
  masp: { build: masp, place: 'near', side: 'any', perLap: 2, turn: 0.2 },
  ponte_estaiada: { build: ponteEstaiada, place: 'far', side: 'any', perLap: 2, turn: 0.15 },
  igreja_barroca: { build: igrejaBarroca, place: 'near', side: 'any', perLap: 2 },
  casario_colonial: { build: casarioColonial, place: 'near', side: 'any', perLap: 3, turn: 0.15 },
  convento_penha: { build: conventoPenha, place: 'far', side: 'land', perLap: 2 },
  terceira_ponte: { build: terceiraPonte, place: 'far', side: 'sea', perLap: 1, turn: 0.15 },
  // Sul
  trem_serra_verde: { build: tremSerraVerde, place: 'far', side: 'any', perLap: 2, turn: 0.25 },
  estufa_jardim_botanico: { build: estufaJardimBotanico, place: 'near', side: 'any', perLap: 1 },
  ponte_hercilio_luz: { build: ponteHercilioLuz, place: 'far', side: 'sea', perLap: 2, turn: 0.15 },
  igreja_acoriana: { build: igrejaAcoriana, place: 'near', side: 'any', perLap: 2 },
  catedral_de_pedra: { build: catedralDePedra, place: 'near', side: 'any', perLap: 1 },
  cuia_chimarrao: { build: cuiaChimarrao, place: 'near', side: 'any', perLap: 2 },
  araucaria: { build: araucaria, place: 'near', side: 'any', perLap: 4 },
  // Centro-Oeste
  congresso_nacional: { build: congressoNacional, place: 'far', side: 'any', perLap: 2, turn: 0.2 },
  catedral_brasilia: { build: catedralBrasilia, place: 'near', side: 'any', perLap: 2 },
  cachoeira_veadeiros: { build: cachoeiraVeadeiros, place: 'far', side: 'any', perLap: 2, turn: 0.35 },
  buriti: { build: buriti, place: 'near', side: 'any', perLap: 4 },
  gruta_lago_azul: { build: grutaLagoAzul, place: 'near', side: 'any', perLap: 2 },
  tuiuiu_ninho: { build: tuiuiuNinho, place: 'near', side: 'any', perLap: 3 },
  portal_transpantaneira: { build: portalTranspantaneira, place: 'near', side: 'any', perLap: 2 },
};
