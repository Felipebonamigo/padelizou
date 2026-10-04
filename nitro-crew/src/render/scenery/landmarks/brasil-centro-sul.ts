// Marcos turísticos: ver docs/PISTAS-TURISMO.md (onda G). Um construtor por id de src/core/data/places.ts.
// Sudeste, Sul e Centro-Oeste. Low-poly de faces planas e cor chapada, escala real (skyline: grande), lido de longe
// pela silhueta. Convenção do modelo em types.ts: origem no centro da base, frente para +X, comprimento em Z.
import * as THREE from 'three';
import { hash2 } from '../../noise';
import { box, cone, cyl, dodeca, gable, hip, ico, jitter, paint, sphere, tf, tintUp, tris, type Geo } from '../geom';
import { beam, cable, cliff, facadeBox, hill, Kit, landmarkPart, lathe } from './kit';
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

/**
 * Luz que se vê dos dois lados: o material de luz tem uma face só, e uma superfície aberta (o vidro da Catedral, a
 * cortina d'água) sumiria do lado de trás. Acrescenta os triângulos com a volta invertida (dobra os triângulos).
 */
function bothSides(g: Geo): Geo {
  const pos = g.getAttribute('position'); const col = g.getAttribute('color');
  const n = pos.count;
  const p = new Float32Array(n * 6); const c = new Float32Array(n * 6);
  for (let i = 0; i < n; i++) {
    const back = n + i - (i % 3) + (2 - (i % 3)); // o vértice a, b, c do triângulo vai para c, b, a na cópia
    for (let k = 0; k < 3; k++) {
      p[i * 3 + k] = p[back * 3 + k] = pos.getComponent(i, k);
      c[i * 3 + k] = c[back * 3 + k] = col.getComponent(i, k);
    }
  }
  const out = new THREE.BufferGeometry();
  out.setAttribute('position', new THREE.BufferAttribute(p, 3));
  out.setAttribute('color', new THREE.BufferAttribute(c, 3));
  out.computeVertexNormals();
  g.dispose();
  return out;
}

/** Escala o marco montado inteiro (a mesma forma, maior: para ler de longe) a partir da origem da base. */
function scaled(m: ReturnType<Kit['model']>, s: number): ReturnType<Kit['model']> {
  const t = new THREE.Matrix4().makeScale(s, s, s);
  for (const p of m.parts) p.geometry.applyMatrix4(t);
  return m;
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

/** Cristo Redentor no alto do Corcovado (skyline): morro de mata, agulha de granito e a estátua (×3, lida a 600 m+). */
function cristoRedentor(): ReturnType<Kit['model']> {
  const k = new Kit();
  // Maciço com mata (largo e baixo) e a agulha de pedra por cima, mais para o fundo (a face íngreme olha a pista).
  k.add(hill(190, 62, 150, 11, '#356f34', '#4a8a3c', 1, 0.14, 1.2, tf(-50, 0, 10)));
  // A agulha: estreita e alta, face íngreme de granito, um pouco de mata nas saliências.
  k.add(tintUp(hill(54, 190, 46, 12, '#8b8a82', null, 1, 0.1, 0.5, tf(-14, 0, 6)), '#4a8340', 0.7, 0.85));
  k.add(tintUp(hill(80, 120, 70, 13, '#6f7a68', null, 1, 0.12, 0.6, tf(-40, 0, -20)), '#3f7a3a', 0.45, 1));
  const top = 186;
  k.add(paint(cyl(20, 26, 8, 8), '#cfc8b8', tf(-14, top + 2, 6))); // mirante
  // Estátua: pedestal, túnica afinando para cima, braços abertos ao longo de Z (a cruz lê de frente), cabeça.
  const S = 3; const sx = -14; const sz = 6; const y0 = top + 6;
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

/**
 * MASP: a caixa de vidro suspensa por dois pórticos vermelhos sobre o vão livre (Av. Paulista). Só aparece em Sampa,
 * à noite: os pórticos são luz (o vermelho iluminado do cartão-postal) — pintados no material liso, apagavam de noite e
 * a caixa de janelas acesas lia como mais um prédio da avenida. O vidro é escuro, para o vermelho saltar de dia também.
 */
function masp(): ReturnType<Kit['model']> {
  const k = new Kit();
  const red = '#c81e14'; // aceso (×2 à noite) continua vermelho; mais claro, o mapeamento de tom puxava para salmão
  const L = 74; const xP = 12.5; const H = 22.5;
  k.add(paint(box(34, 0.5, L + 14), '#b8b4ac', tf(0, 0.25, 0))); // praça do vão livre
  for (const x of [-xP, xP]) {
    for (const z of [-L / 2 - 1.5, L / 2 + 1.5]) k.light(paint(box(3.2, H, 3.2), red, tf(x, H / 2, z)));
    k.light(paint(box(3.2, 3.4, L + 6.4), red, tf(x, H - 1.7, 0))); // viga do teto
    k.light(paint(box(3.2, 2.4, L + 6.4), red, tf(x, 8.2, 0))); // viga de baixo
  }
  // Caixa: laje de concreto e o vidro escuro com caixilhos (janelas acendem à noite).
  k.add(paint(box(26, 1.2, L), '#6c6f74', tf(0, 9.3, 0)));
  k.add(paint(box(26, 1, L), '#6c6f74', tf(0, 19.9, 0)));
  k.facade('office', facadeBox('office', 25.6, 9.4, L - 0.4, '#5a6672', tf(0, 14.6, 0)));
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

/**
 * Casario colonial (Ouro Preto): sobrados geminados coloridos subindo a ladeira — o serrilhado dos telhados de barro
 * que sobe o morro é o desenho da cidade —, cada um no seu patamar de pedra, o morro verde atrás. Antes a ladeira subia
 * 0,7 m por casa (4 m no total, telhado mais alto a 17 m): de longe, uma fileira baixa de casas atrás da mata da beira.
 */
function casarioColonial(): ReturnType<Kit['model']> {
  const k = new Kit();
  const walls = ['#f2c94c', '#6f9ad8', '#e98ba0', '#83c27f', '#ffffff', '#f19a52', '#c9a0dc'];
  const W = 8.4; const n = 9; const D = 11;
  // O morro atrás das casas de cima, com umas copas.
  k.add(hill(26, 30, 44, 44, '#5f7f45', '#4f8a3c', 1, 0.14, 0.3, tf(-40, 0, 16)));
  for (let i = 0; i < 4; i++) {
    const z = i * 10; const x = -40 + (hash2(i, 47) - 0.5) * 8;
    k.add(paint(jitter(ico(1, 0), 0.2, 46 + i), i % 2 ? '#3f7a38' : '#4a8a3c', tf(x, 30 * Math.sqrt(1 - ((z - 16) / 44) ** 2) - 2, z, 4.5, 4, 4.5)));
  }
  for (let i = 0; i < n; i++) {
    const z = (i - (n - 1) / 2) * W;
    const step = i * 2.3;
    const floors = hash2(i, 41) < 0.35 ? 3 : 2;
    const h = floors * 3.6;
    const wall = walls[(i * 3) % walls.length];
    // Patamar de pedra (cobre o declive e a ladeira) e a casa com a fachada de janelas.
    k.add(paint(box(D + 1, 3 + step, W), STONE, tf(-0.5, (step - 3) / 2, z)));
    // A ladeira de pé de moleque na porta de cada casa e, na frente dela, o barranco de capim até a rua de baixo (só o
    // alto do muro de pedra aparece).
    k.add(paint(box(3.4, 0.6, W), '#8a8278', tf(D / 2 + 1.7, step - 0.3, z)));
    if (step > 0.5) {
      const x0 = D / 2 + 3.4; const run = 2 + step * 0.6; const z0 = z - W / 2; const z1 = z + W / 2; const t = step - 0.1;
      k.add(paint(tris([
        x0, t, z0, x0 + run, -0.2, z0, x0 + run, -0.2, z1, x0, t, z0, x0 + run, -0.2, z1, x0, t, z1,
        x0, t, z0, x0, -0.2, z0, x0 + run, -0.2, z0, x0, t, z1, x0 + run, -0.2, z1, x0, -0.2, z1,
      ]), i % 2 ? '#6f9a48' : '#64903f'));
    }
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
  return k.model([-3, 32], 0.03, 7);
}

/**
 * Convento da Penha (Vila Velha): o convento branco coroando o penhasco de granito, mata no pé. O penhasco tem o alto
 * largo (o convento ocupa o topo inteiro, como no cartão-postal) e o convento é exagerado ~2×: com 20 m de telhado
 * sobre 330 m de morro, de 400 m ele era um ponto em cima de mais um morro de pedra.
 */
function conventoPenha(): ReturnType<Kit['model']> {
  const k = new Kit();
  k.add(hill(150, 55, 120, 31, '#3a7838', '#4a8a40', 1, 0.12, 0.5));
  // Penhasco: o morro de granito facetado de paredes íngremes, com o alto aplainado (o convento ocupa o topo inteiro);
  // mata nas saliências.
  const rock = hill(70, 158, 60, 32, '#8a8780', null, 1, 0.1, 0.2);
  const rp = rock.attributes.position as THREE.BufferAttribute;
  for (let i = 0; i < rp.count; i++) if (rp.getY(i) > 136) rp.setY(i, 136 + (rp.getY(i) - 136) * 0.12);
  rock.computeVertexNormals();
  tintUp(rock, '#4f8a40', 0.72, 0.75);
  k.add(rock);
  const y = 140;
  k.add(paint(cyl(40, 42, 6, 11), '#8a8780', tf(0, y - 2.5, 0)));
  // Muralha branca na borda da frente, o claustro (quadra branca de telhado de quatro águas), a igreja de frente para a
  // pista com o frontão e a torre no canto, com a cúpula de telha e a cruz.
  k.add(paint(box(1.6, 3.4, 72), WHITE, tf(30, y + 1.7, -2)));
  k.add(paint(box(30, 14, 50), WHITE, tf(-8, y + 7, 4)), paint(hip(31.5, 7, 51.5), TILE, tf(-8, y + 14, 4)));
  k.add(paint(box(14, 17, 36), WHITE, tf(14, y + 8.5, -8)), paint(gable(14, 7, 36, 0.8), TILE, tf(14, y + 17, -8)));
  k.add(paint(gable(17, 6, 1.2, 0), WHITE, tf(21.4, y + 17, -8, 1, 1, 1, 0, Math.PI / 2, 0))); // frontão
  k.add(paint(box(9, 32, 9), WHITE, tf(16, y + 16, -30)), paint(box(10, 1.2, 10), STONE, tf(16, y + 26, -30)));
  k.add(paint(hip(10, 9, 10), TILE, tf(16, y + 32, -30)), paint(box(0.9, 5, 0.9), '#30302c', tf(16, y + 43, -30)), paint(box(0.9, 0.9, 3.4), '#30302c', tf(16, y + 44, -30)));
  // Janelas escuras em fileira, a porta da igreja e a sineira acesa.
  for (let i = 0; i < 6; i++) k.add(win(21.1, y + 11, -22 + i * 5.6, 1.8, 3.2), win(21.1, y + 4.6, -22 + i * 5.6, 1.8, 3.2));
  k.add(win(21.1, y + 3.2, -8, 3.4, 6, '#5a3a22'));
  k.add(win(20.6, y + 24, -30, 2.6, 4.4, '#202428'));
  k.light(paint(box(0.3, 2.2, 1.4), LAMP, tf(20.8, y + 24, -30)));
  return k.model([0, y + 44], 0.04, 31);
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

/**
 * Arcada de pedra (parede extrudada em X, largura `w`): retângulo de `z0` a `z1` e de `y0` a `y1` com `n` vãos de
 * arco pleno entre pilares de `pier` m; o vão vai de `y0` até o arranque `spring` e fecha num semicírculo.
 */
function arcade(z0: number, z1: number, y0: number, y1: number, n: number, pier: number, spring: number, w: number, color: string): Geo {
  const shape = new THREE.Shape([new THREE.Vector2(z0, y0), new THREE.Vector2(z1, y0), new THREE.Vector2(z1, y1), new THREE.Vector2(z0, y1)]);
  const bay = (z1 - z0) / n; const r = (bay - pier) / 2;
  for (let i = 0; i < n; i++) {
    const zc = z0 + (i + 0.5) * bay;
    const hole = new THREE.Path();
    hole.moveTo(zc - r, y0 + 0.02);
    hole.lineTo(zc + r, y0 + 0.02);
    hole.lineTo(zc + r, spring);
    for (let q = 1; q < 6; q++) { const a = (q / 6) * Math.PI; hole.lineTo(zc + Math.cos(a) * r, spring + Math.sin(a) * r); }
    hole.lineTo(zc - r, spring);
    shape.holes.push(hole);
  }
  const g = new THREE.ExtrudeGeometry(shape, { depth: w, bevelEnabled: false, curveSegments: 1 });
  // Giro de 90° em Y (sem espelhar: a normal continua para fora): o perfil fica no plano ZY (simétrico em z) e a
  // extrusão vai para +X, centrada.
  return paint(g, color, new THREE.Matrix4().makeRotationY(Math.PI / 2).setPosition(-w / 2, 0, 0));
}

/**
 * Serra Verde Express (Serra do Mar, PR): o trem sobre o viaduto de pedra de dois andares de arcos atravessando a
 * grota — o tabuleiro a 58 m, acima da mata da beira da pista —, as encostas íngremes de mata nas cabeceiras, o rio
 * e a cascata lá embaixo, a locomotiva amarela e os carros verdes de janela creme.
 */
function tremSerraVerde(): ReturnType<Kit['model']> {
  const k = new Kit();
  const L = 210; const H = 58; const spans = 7; const W = 8; const tier = 31;
  const stone = '#8c7d6a'; const dark = '#6e6152';
  // Encostas da grota nas duas cabeceiras, mais altas que o tabuleiro, com a mata e a pedra aparecendo.
  for (const s of [-1, 1]) {
    k.add(hill(44, H + 24, 58, 41 + s, '#3f7a38', '#4a8a3c', 1, 0.14, 0.6, tf(-4, 0, s * (L / 2 + 36))));
    for (let i = 0; i < 5; i++) k.add(paint(jitter(ico(1, 0), 0.2, 45 + i + s * 10), i % 2 ? '#2f6a30' : '#3a7a36', tf(14 + hash2(46, i + s) * 10, 8 + i * 9, s * (L / 2 + 6 + i * 6), 7, 6, 7)));
  }
  // O rio no fundo da grota (atravessa por baixo do viaduto) e a cascata descendo a encosta.
  k.add(paint(box(90, 0.3, 16), '#4f8a86', tf(-6, 0.15, 12)));
  k.light(beam([-18, 46, L / 2 + 6], [-8, 1, L / 2 - 6], 3.2, '#eef7ff', 0.6));
  // Viaduto: arcada de baixo (arcos grandes, base enterrada), cornija, arcada de cima (dois arcos por vão).
  k.add(arcade(-L / 2, L / 2, -12, tier, spans, 7, 14, W + 2, stone));
  k.add(paint(box(W + 3, 1.4, L + 2), dark, tf(0, tier + 0.4, 0)));
  k.add(arcade(-L / 2, L / 2, tier + 1, H - 1.2, spans * 2, 4, H - 13, W, stone));
  // Tabuleiro, parapeito e trilhos.
  k.add(paint(box(W + 1.2, 1.6, L + 4), dark, tf(0, H, 0)));
  for (const x of [-1, 1]) k.add(paint(box(0.5, 1.1, L + 4), stone, tf(x * (W / 2 + 0.35), H + 1.3, 0)));
  k.add(paint(box(0.3, 0.3, L), '#4a4a4a', tf(0.9, H + 1, 0)), paint(box(0.3, 0.3, L), '#4a4a4a', tf(-0.9, H + 1, 0)));
  // Trem: locomotiva amarela com faixa verde e seis carros verdes de faixa creme — 1,6× o de verdade (o trem é o que
  // diz "Serra Verde Express"; em tamanho real, a 210 m de lado, eram 6 px de altura e o viaduto lia como aqueduto).
  const T = 1.6; const yt = H + 1.1;
  let z = -86;
  k.add(paint(box(3.2 * T, 4.2 * T, 16 * T), '#e8b018', tf(0, yt + 2.3 * T, z)), paint(box(3.3 * T, 0.8 * T, 16.1 * T), '#2f6b46', tf(0, yt + 1.6 * T, z)));
  k.add(paint(box(3 * T, 1.4 * T, 4 * T), '#1e2a30', tf(0, yt + 4.2 * T, z - 5.5 * T)));
  k.light(paint(box(0.4, 0.8, 0.8), LAMP, tf(0, yt + 3.2 * T, z - 8 * T - 0.2)));
  z += 17.5 * T;
  for (let c = 0; c < 6; c++) {
    const zc = z + c * 18 * T;
    k.add(paint(box(3.1 * T, 3.6 * T, 17 * T), '#2f6b46', tf(0, yt + 2.2 * T, zc)));
    k.add(paint(box(3.2 * T, 1.2 * T, 16 * T), '#efe4c4', tf(0, yt + 2.8 * T, zc)));
    for (let w = 0; w < 5; w++) k.add(paint(box(3.25 * T, 0.7 * T, 1.6 * T), '#2a3238', tf(0, yt + 2.8 * T, zc - 6 * T + w * 3 * T)));
    k.add(paint(box(3.2 * T, 0.5 * T, 17.2 * T), '#3a3a3a', tf(0, yt + 4.2 * T, zc)));
  }
  return k.model([-12, H + 8], 0.04, 41);
}

/**
 * Estufa do Jardim Botânico de Curitiba: vidro e ferro branco, cúpula central e alas em abóbada, jardim francês. O ferro
 * branco é o desenho — montantes a cada ~1,5 m, as costelas da cúpula e os arcos das alas, grossos para ler de longe —,
 * e o vidro é verde-claro (as plantas atrás dele): azul-claro, ele sumia no céu, e a estufa virava uma bolha pálida.
 * Montada em 1/1,3 e escalada (~29 m).
 */
function estufaJardimBotanico(): ReturnType<Kit['model']> {
  const k = new Kit();
  const glass = '#9cc8ae'; const glassLow = '#7fb08e'; const iron = '#fbfbf6';
  // Corpo central: base de vidro e a cúpula alongada; alas com abóbada em Z.
  k.add(paint(box(14, 9, 14), glassLow, tf(0, 4.5, 0)));
  k.add(paint(sphere(1, 12, 6, 0, Math.PI * 2, 0, Math.PI / 2), glass, tf(0, 9, 0, 7.2, 9, 7.2)));
  k.add(paint(cyl(1.4, 1.6, 2.6, 8), iron, tf(0, 18.9, 0)), paint(cone(1.7, 2.6, 8), iron, tf(0, 21.4, 0)));
  for (let a = 0; a < 12; a++) {
    const ang = (a / 12) * Math.PI * 2;
    let prev: V3 | null = null;
    for (let s = 0; s <= 4; s++) {
      const t = (s / 4) * Math.PI / 2;
      const p: V3 = [Math.cos(ang) * 7.35 * Math.cos(t), 9 + 9.1 * Math.sin(t), Math.sin(ang) * 7.35 * Math.cos(t)];
      if (prev) k.add(beam(prev, p, 0.6, iron, 0.4));
      prev = p;
    }
  }
  for (const y of [9, 13.5]) k.add(paint(cyl(y === 9 ? 7.5 : 6.5, y === 9 ? 7.5 : 6.5, 0.6, 12, true), iron, tf(0, y, 0)));
  for (const s of [-1, 1]) {
    const zc = s * 12.5;
    k.add(paint(box(11, 6, 11), glassLow, tf(0, 3, zc)));
    k.add(paint(cyl(5.5, 5.5, 11, 10), glass, tf(0, 6, zc, 1, 0.9, 1, Math.PI / 2, 0, 0)));
    for (let r = 0; r <= 6; r++) k.add(paint(cyl(5.7, 5.7, 0.55, 10, true), iron, tf(0, 6, zc - 5.4 + r * 1.8, 1, 0.9, 1, Math.PI / 2, 0, 0)));
    for (const x of [-5.6, 5.6]) for (let r = 0; r <= 6; r++) k.add(paint(box(0.5, 6, 0.55), iron, tf(x, 3, zc - 5.4 + r * 1.8)));
    for (const x of [-5.6, 5.6]) k.add(paint(box(0.5, 0.6, 11.4), iron, tf(x, 6, zc)));
    // A ponta da ala: o arco de ferro da testa.
    k.add(paint(cyl(5.7, 5.7, 0.6, 10, true), iron, tf(0, 6, zc + s * 5.5, 1, 0.9, 1, Math.PI / 2, 0, 0)));
  }
  for (const x of [-7.1, 7.1]) for (let r = 0; r <= 9; r++) k.add(paint(box(0.5, 9, 0.55), iron, tf(x, 4.5, -7 + r * (14 / 9))));
  for (const x of [-7.1, 7.1]) for (const y of [4.5, 8.8]) k.add(paint(box(0.5, 0.6, 14.4), iron, tf(x, y, 0)));
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
  return scaled(k.model([0, 22], 0.02, 13), 1.3);
}

/**
 * Ponte Hercílio Luz (Florianópolis): pênsil, duas torres treliçadas e a corrente de barras que desce das torres e, no
 * meio do vão, vira o banzo de cima da treliça (o desenho dela). Vista a ~170 m de lado e de 300 m+, à tarde: torre de
 * 3,4 m e corrente de 1,2 m sumiam (1–2 px) — a ponte lia como um risco. A torre é uma treliça de 9 m (afina para 5 m
 * no alto) com os X de lado, a corrente tem 2,8 m e a treliça do meio tem montantes e diagonais.
 */
function ponteHercilioLuz(): ReturnType<Kit['model']> {
  const k = new Kit();
  const steel = '#e8edf0'; const deckY = 32; const tz = 170; const top = 84; const half = 250;
  // Tabuleiro, guarda-corpo e os vãos de acesso sobre pilares.
  k.add(paint(box(14, 2.6, half * 2), '#c4c8cc', tf(0, deckY, 0)));
  for (const x of [-7, 7]) k.add(paint(box(0.8, 2.4, half * 2), steel, tf(x, deckY + 2.4, 0)));
  for (const z of [-half + 10, -half + 45, half - 45, half - 10]) for (const x of [-5, 5]) k.add(paint(box(3.4, deckY + 8, 3.4), '#b8bcc0', tf(x, (deckY - 8) / 2, z)));
  // Torres: em cada lado do tabuleiro uma perna treliçada (dois montantes que afinam para cima, os X entre eles e as
  // travessas) e, de um lado ao outro, as vigas do topo e do tabuleiro; a sapata de pedra no pé.
  const yb = -6;
  for (const zt of [-tz, tz]) {
    for (const x of [-7.5, 7.5]) {
      const w = (y: number): number => 4.5 - ((y - yb) / (top - yb)) * 2; // meia largura em z
      for (const s of [-1, 1]) k.add(beam([x, yb, zt + s * w(yb)], [x, top, zt + s * w(top)], 2, steel, 2));
      const levels = [yb, deckY + 3, 48, 62, 74, top - 1];
      for (let i = 0; i < levels.length - 1; i++) {
        const y0 = levels[i]; const y1 = levels[i + 1];
        k.add(beam([x, y0, zt - w(y0)], [x, y1, zt + w(y1)], 1.1, steel), beam([x, y0, zt + w(y0)], [x, y1, zt - w(y1)], 1.1, steel));
        k.add(paint(box(1.4, 1.2, w(y1) * 2 + 1), steel, tf(x, y1, zt)));
      }
    }
    k.add(paint(box(18, 2.8, 5.4), steel, tf(0, top, zt)), paint(box(18, 1.8, 6), steel, tf(0, deckY + 6, zt)));
    k.add(beam([-7.5, deckY + 7, zt], [7.5, top - 2, zt], 1, steel), beam([7.5, deckY + 7, zt], [-7.5, top - 2, zt], 1, steel));
    k.add(paint(box(20, 10, 12), '#a8a49c', tf(0, -4, zt)));
  }
  // Correntes: da torre descem até o banzo da treliça do meio; montantes e diagonais entre a corrente e o tabuleiro;
  // as luzes ao longo (anoitecer).
  const N = 16;
  const chainY = (z: number): number => deckY + 8 + (top - deckY - 8) * Math.pow(Math.abs(z) / tz, 1.8);
  for (const x of [-7.5, 7.5]) {
    for (let i = 0; i < N; i++) {
      const z0 = -tz + (i / N) * 2 * tz; const z1 = -tz + ((i + 1) / N) * 2 * tz;
      k.add(beam([x, chainY(z0), z0], [x, chainY(z1), z1], 1.6, steel, 2.8));
      if (i > 0) k.add(beam([x, chainY(z0) - 1, z0], [x, deckY + 2, z0], 0.7, steel));
      // Diagonal do painel (zigue-zague): só onde a corrente já está perto do tabuleiro (a treliça do meio).
      if (Math.abs(z0 + z1) / 2 < tz * 0.6) {
        const up = i % 2 === 0;
        k.add(beam([x, up ? deckY + 2 : chainY(z0) - 1, z0], [x, up ? chainY(z1) - 1 : deckY + 2, z1], 0.6, steel));
      }
      k.light(paint(box(0.8, 0.8, 0.8), '#fff1c0', tf(x * 1.1, chainY(z0) + 1.6, z0)));
    }
    for (const s of [-1, 1]) {
      k.add(beam([x, top, s * tz], [x, deckY + 3, s * (half - 6)], 1.6, steel, 2.8));
      for (let c = 1; c < 4; c++) {
        const t = c / 4;
        k.light(paint(box(0.8, 0.8, 0.8), '#fff1c0', tf(x * 1.1, top + (deckY + 3 - top) * t + 1.6, s * (tz + (half - 6 - tz) * t))));
      }
    }
  }
  return k.model([-8, top], 0, 15);
}

/**
 * Igreja açoriana (Santo Antônio de Lisboa, SC): branca com quinas, molduras, barrado e porta azuis, torre no meio da
 * fachada. Montada em 1/1,4 e escalada (~36 m de torre): com 18 m de frente ela cabia em ~40 px a 200 m, e o azul
 * fino das molduras sumia.
 */
function igrejaAcoriana(): ReturnType<Kit['model']> {
  const k = new Kit();
  const blue = '#2457b8'; const fx = 11;
  // Barrado azul no pé das paredes e da torre (a faixa que a casa açoriana pinta).
  k.add(paint(box(0.4, 1.4, 12.4), blue, tf(fx + 0.05, 0.7, 0)), paint(box(22.4, 1.4, 0.4), blue, tf(0, 0.7, 6.05)), paint(box(22.4, 1.4, 0.4), blue, tf(0, 0.7, -6.05)));
  k.add(paint(box(0.3, 1.4, 4.6), blue, tf(fx + 3.75, 0.7, 0)));
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
  return scaled(k.model([0, 25], 0.02, 17), 1.4);
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

/**
 * Cuia de chimarrão gigante com a bomba (monumento gaúcho). O que diz "chimarrão" é a bomba de prata enfiada na erva,
 * inclinada para o lado (lê de frente): grossa, para não sumir de longe — com 0,4 m ela sumia e a cuia lia como um vaso.
 * Montada em 1/1,5 e escalada: ~26 m, acima da mata da beira.
 */
function cuiaChimarrao(): ReturnType<Kit['model']> {
  const k = new Kit();
  k.add(paint(cyl(5.4, 6, 1.4, 8), '#9a9488', tf(0, 0.7, 0)), paint(cyl(4.6, 5, 0.6, 8), '#b8b2a4', tf(0, 1.7, 0)));
  const y = 2;
  k.add(lathe([[0.1, 0], [2.4, 0.2], [3.6, 1.4], [4.0, 3.0], [3.7, 4.6], [2.9, 5.8], [2.35, 6.6], [2.55, 7.4], [3.2, 8.4], [3.4, 9]], 12, '#8a5a26', tf(0, y, 0)));
  // Bocal de prata e a faixa de prata da cintura (o enfeite da cuia gaúcha).
  k.add(lathe([[3.25, 0], [3.55, 0], [3.55, 0.9], [3.3, 0.9]], 12, '#dfe3e8', tf(0, y + 8.3, 0)));
  k.add(lathe([[2.4, 0], [2.62, 0], [2.62, 0.55], [2.4, 0.55]], 12, '#dfe3e8', tf(0, y + 6.3, 0)));
  // Erva: monte verde-vivo na boca, com o pé da bomba enfiado.
  k.add(paint(cone(3.2, 1.6, 10), '#6aa82e', tf(0, y + 9, 0, 1, 1, 1, 0.15, 0, 0)));
  // Bomba: o tubo de prata inclinado para +Z (de frente para a pista vê-se a diagonal) e o bico dourado dobrado.
  const foot: V3 = [0.3, y + 7.6, -0.6]; const bend: V3 = [-0.2, y + 15.6, 2.6]; const tip: V3 = [-0.3, y + 16.1, 3.5];
  k.add(beam(foot, bend, 0.85, '#e4e8ec'));
  k.add(beam(bend, tip, 0.85, '#d8b048'));
  return scaled(k.model([0, 17], 0.04, 23), 1.5);
}

/**
 * Araucárias: o capão de pinheiros-do-paraná de alturas diferentes, tronco reto e nu e a copa em taça — os galhos em
 * candelabro sobem em duas coroas e terminam em tufos largos e achatados que se juntam numa mesa verde-escura de topo
 * reto. Antes três árvores de tronco fino com tufos soltos (metade da silhueta era tronco e galho): lia como pinheiro
 * qualquer. A copa é o desenho: larga (~60% da altura) e cheia.
 */
function araucaria(): ReturnType<Kit['model']> {
  const k = new Kit();
  const trees: Array<[number, number, number]> = [[0, 0, 31], [-8, -15, 25], [-5, 14, 27], [-14, -1, 21], [-2, 27, 18]];
  const bark = '#6a4a34';
  trees.forEach(([x, z, H], t) => {
    const seed = 50 + t;
    const R = H * 0.33; // raio da copa
    k.add(paint(cyl(0.36, 0.62, H, 7), bark, tf(x, H / 2, z)));
    // Duas coroas de galhos: a de baixo mais aberta, a de cima mais curta (a taça); o galho sobe em diagonal do tronco
    // até debaixo do tufo.
    for (const [ring, n, rise] of [[0, 8, 0], [1, 6, 1]] as Array<[number, number, number]>) {
      for (let b = 0; b < n; b++) {
        const a = (b / n) * Math.PI * 2 + hash2(seed, b + ring * 20) * 0.5 + ring * 0.4;
        const out = R * (ring ? 0.6 : 1) * (0.85 + hash2(seed, b + 40) * 0.2);
        const yb = H - R * 0.55 + rise * R * 0.24;
        const p2: V3 = [x + Math.cos(a) * out, yb + R * 0.32, z + Math.sin(a) * out];
        k.add(beam([x, yb, z], p2, 0.3, bark));
        // Tufo largo e achatado na ponta (o "pinho" da araucária); os tufos vizinhos se encostam.
        k.add(paint(jitter(ico(1, 0), 0.14, seed + b + ring * 20), (b + ring) % 2 ? '#24502e' : '#2c5c34', tf(p2[0], p2[1] + 0.1, p2[2], R * 0.46, R * 0.22, R * 0.46)));
      }
    }
    k.add(paint(jitter(ico(1, 0), 0.12, seed + 30), '#2a5832', tf(x, H - R * 0.02, z, R * 0.5, R * 0.2, R * 0.5)));
    // Galho seco mais abaixo (a araucária perde os de baixo).
    const a = hash2(seed, 41) * 6.28; const yb = H * 0.55;
    k.add(beam([x, yb, z], [x + Math.cos(a) * 2.6, yb + 1, z + Math.sin(a) * 2.6], 0.16, '#4a3a2e'));
  });
  // Campo no pé: capim e moitas.
  for (let i = 0; i < 3; i++) k.add(paint(jitter(ico(1, 0), 0.2, 59 + i), '#5a8a3a', tf(-4 + i * 2, 0.3, -12 + i * 12, 6, 1, 5)));
  return k.model([0, 33], 0.06, 51);
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

/**
 * Catedral de Brasília: coroa de 16 pilares curvos (hiperboloide), vidro azul entre eles, campanário ao lado. O vidro é
 * luz: ao entardecer (Brasília) e à noite (Torre de TV) a coroa acende por dentro, azul atrás dos pilares brancos — o
 * cartão-postal da noite; no material liso ele apagava, e só o anel do topo brilhava.
 */
function catedralBrasilia(): ReturnType<Kit['model']> {
  const k = new Kit();
  const w = '#f4f4f0';
  k.add(paint(cyl(36, 36, 0.4, 16), '#c8c4b8', tf(0, 0.2, 0)));
  k.light(bothSides(lathe([[29, 0.3], [21, 10], [14.5, 20], [11, 29], [10.6, 33]], 16, '#86bede')));
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

/**
 * Cachoeira da Chapada dos Veadeiros (os Saltos do Rio Preto): o paredão de quartzito em camadas cor de ferrugem com o
 * cerrado no alto, o salto grande caindo do vão da borda e o menor ao lado, a névoa no pé e o poço verde-escuro. A
 * régua das Cataratas (tests/landmarks-leitura.test.ts): antes era uma caixa de pedra de 170 m com uma risca branca de
 * 9 m — de longe, um paredão (um prédio de tijolo); agora a água, em cortinas riscadas que abrem para baixo, é ~1/3
 * da face.
 */
function cachoeiraVeadeiros(): ReturnType<Kit['model']> {
  const k = new Kit();
  const H = 92; const wx = -18; const wz = 6;
  const wall = cliff({
    len: 150, H, strata: ['#a8704a', '#c48e60', '#94603e', '#d2a274', '#b27c52'], layers: 7, seed: 61, cols: 30, depth: 50,
    batter: 0.05, gully: 4, gullyLen: 14, bay: 8, ragged: 0.07, ledge: 1.4, talus: 6, talusColor: '#8a6a50', top: '#7f8f3a',
    ledgeTop: '#8a9a48', ends: 0.16, notch: { z: wz, half: 15, h: H - 7, recess: 9 },
  }, tf(wx, 0, 0));
  k.add(wall.geo);
  // Cortina d'água: do lábio (x, yTop) ao pé, lançada para a frente e abrindo para baixo; faixas de tons (os riscos).
  const tones = ['#ffffff', '#eaf5fc', '#d6eaf5', '#f4fbff', '#c8e2ef'];
  const curtain = (seed: number, z: number, xTop: number, yTop: number, xBot: number, wTop: number, wBot: number): void => {
    const B = 5;
    for (let b = 0; b < B; b++) {
      const out: number[] = [];
      const s0 = -0.5 + b / B; const s1 = -0.5 + (b + 1) / B;
      const rows: Array<[number, number, number]> = [[xTop, yTop, wTop], [xTop + (xBot - xTop) * 0.45, yTop * 0.55, wTop + (wBot - wTop) * 0.55], [xBot, -0.3, wBot]];
      for (let j = 0; j < rows.length - 1; j++) {
        const [xa, ya, wa] = rows[j]; const [xb, yb, wb] = rows[j + 1];
        out.push(xa, ya, z + s0 * wa, xb, yb, z + s0 * wb, xb, yb, z + s1 * wb, xa, ya, z + s0 * wa, xb, yb, z + s1 * wb, xa, ya, z + s1 * wa);
      }
      k.light(bothSides(paint(tris(out), tones[Math.floor(hash2(seed, b) * tones.length)])));
    }
  };
  const lip = wx + wall.lipAt(wz); const foot = wx + wall.footAt(wz);
  curtain(611, wz, lip + 1.2, wall.topAt(wz) + 0.4, foot + 9, 26, 34);
  // O salto menor, sobre a borda, à esquerda.
  const z2 = -44;
  curtain(612, z2, wx + wall.lipAt(z2) + 1, wall.topAt(z2) + 0.3, wx + wall.footAt(z2) + 7, 11, 16);
  // Névoa no pé das quedas (luz: de longe é uma mancha clara e macia) e o poço verde-escuro com a espuma.
  const MIST = '#e4eef2';
  for (let i = 0; i < 5; i++) k.light(paint(jitter(ico(1, 1), 0.14, 620 + i), MIST, tf(foot + 10 + hash2(621, i) * 6, 5 + (i % 2) * 4, wz - 18 + i * 9, 9, 8, 9)));
  k.light(paint(jitter(ico(1, 1), 0.14, 626), MIST, tf(wx + wall.footAt(z2) + 8, 4, z2, 7, 6, 9)));
  k.add(paint(cyl(1, 1, 0.6, 14), '#2f6f6a', tf(foot + 30, 0.3, wz - 8, 26, 1, 46)), paint(cyl(1, 1, 0.66, 14), '#3f8a80', tf(foot + 24, 0.33, wz - 6, 14, 1, 30)));
  for (let i = 0; i < 6; i++) k.add(paint(jitter(dodeca(1), 0.2, 65 + i), '#7a6656', tf(foot + 30 + (hash2(i, 66) - 0.5) * 34, 0.8, wz - 40 + i * 13, 3.4, 1.8, 3.4)));
  // Cerrado no alto: tufos e arvoretas atrás da quina (também atrás do vão: a silhueta de cima é verde).
  for (let i = 0; i < 14; i++) {
    const z = -70 + i * 10.6 + (hash2(i, 63) - 0.5) * 4;
    const s = 4.5 + hash2(i, 64) * 3;
    k.add(paint(jitter(ico(1, 0), 0.2, 62 + i), i % 3 ? '#6f8a3a' : '#5a7f32', tf(wx + wall.lipAt(z) - 6 - hash2(i, 65) * 14, Math.max(wall.topAt(z), H * 0.96) + s * 0.4, z, s * 1.2, s * 0.8, s * 1.2)));
  }
  return k.model([-10, H], 0.05, 61);
}

/**
 * Buritizal (vereda): a fileira de buritis ao longo do córrego, cada um com o tronco liso e alto e a copa redonda de
 * leques (os de baixo caídos, a saia de folhas secas) — a vereda do cerrado. Antes eram quatro num bolo de 32 m, que
 * de longe lia como um coqueiral qualquer; a fileira de sete, de alturas diferentes, enche o quadro com o desenho dela.
 */
function buriti(): ReturnType<Kit['model']> {
  const k = new Kit();
  const palms: Array<[number, number, number]> = [[-2, -33, 19], [1, -22, 26], [-4, -11, 22], [0, 0, 29], [-3, 11, 24], [1, 22, 20], [-2, 33, 27]];
  palms.forEach(([x, z, H], t) => {
    const seed = 70 + t;
    k.add(paint(cyl(0.45, 0.6, H, 7), '#8a857a', tf(x, H / 2, z)));
    k.add(paint(cone(1.9, 4.2, 7), '#8a6a3a', tf(x, H - 2.1, z, 1, -1, 1)));
    const n = 13;
    for (let b = 0; b < n; b++) {
      const a = (b / n) * Math.PI * 2 + hash2(seed, b) * 0.4;
      // Inclinação a partir da vertical: dos leques de pé (0,3) aos caídos (1,9) — a copa fica redonda.
      const up = 0.3 + ((b * 7) % n) / (n - 1) * 1.6 + (hash2(seed, b + 9) - 0.5) * 0.2;
      const L = 3;
      const tip: V3 = [x + Math.cos(a) * L * Math.sin(up), H + L * Math.cos(up), z + Math.sin(a) * L * Math.sin(up)];
      k.add(beam([x, H, z], tip, 0.18, '#6a8a3a'));
      // Leque perpendicular ao pecíolo, inclinado para fora.
      const m = new THREE.Matrix4().compose(new THREE.Vector3(...tip), new THREE.Quaternion().setFromEuler(new THREE.Euler(0, -a + Math.PI / 2, -(up - 0.35), 'YXZ')), new THREE.Vector3(1, 1, 1));
      k.add(fanLeaf(4.8, 7, b % 3 === 0 ? '#3f7f34' : b % 2 ? '#4f8f3a' : '#5ea044', m));
    }
  });
  // A vereda: o capim úmido verde-vivo em faixa, o córrego e as moitas no pé.
  k.add(paint(box(16, 0.3, 84), '#6aa844', tf(-1, 0.15, 0)), paint(box(3.2, 0.36, 80), '#4f8aa0', tf(3, 0.18, 0)));
  for (let i = 0; i < 7; i++) k.add(paint(jitter(ico(1, 0), 0.2, 80 + i), i % 2 ? '#5a9a3a' : '#6aa844', tf(-3 + (hash2(i, 81) - 0.5) * 8, 0.4, (i - 3) * 11 + 5, 3.4, 1.3, 3.4)));
  return k.model([0, 30], 0.05, 71);
}

/**
 * Gruta do Lago Azul (Bonito): o morro de calcário claro tomado de mata, a boca grande e escura da gruta na encosta e,
 * lá dentro, o azul do lago (luz: brilha no escuro da boca). Antes era uma caixa de pedra de topo reto com uma boca
 * pequena, e o azul — 33 m² virados para dentro da pedra — não se via.
 */
function grutaLagoAzul(): ReturnType<Kit['model']> {
  const k = new Kit();
  const H = 30; const wx = -10;
  const wall = cliff({ len: 84, H, strata: ['#cfc4a6', '#bdb092', '#d8cfb4', '#ada084'], layers: 5, seed: 91, cols: 20, depth: 34, batter: 0.06, gully: 2.4, gullyLen: 9, bay: 5, ragged: 0.16, ledge: 0.8, talus: 3, talusColor: '#a49a80', top: '#4f8a3a', ledgeTop: '#5a8f3e', ends: 0.22 }, tf(wx, 0, 0));
  k.add(wall.geo);
  // Mata fechada no alto (copas redondas acima da quina, o topo não é reta) e moitas no pé dos lados.
  for (let i = 0; i < 11; i++) {
    const z = -36 + i * 7.2 + (hash2(i, 93) - 0.5) * 3;
    const s = 4 + hash2(i, 94) * 2.6;
    k.add(paint(jitter(ico(1, 0), 0.18, 92 + i), i % 2 ? '#2f7a36' : '#3f8a3c', tf(wx + wall.lipAt(z) - 3 - hash2(i, 95) * 10, wall.topAt(z) + s * 0.5, z, s * 1.1, s, s * 1.1)));
  }
  for (const s of [-1, 1]) k.add(paint(jitter(ico(1, 0), 0.18, 102 + s), '#2f7a36', tf(wx + wall.footAt(s * 30) + 3, 5, s * 30, 6, 7, 6)));
  // Boca da gruta: arco escuro na face (um pouco à frente dela), as estalactites e o azul do lago no fundo, aceso.
  const R = 13.5; const cx = wx + wall.footAt(0) + 3.6; const cy = 7;
  const arch: number[] = [];
  for (let a = 0; a < 10; a++) {
    const t0 = (a / 10) * Math.PI; const t1 = ((a + 1) / 10) * Math.PI;
    arch.push(cx, cy, 0, cx, cy + Math.sin(t0) * R * 1.3, Math.cos(t0) * R, cx, cy + Math.sin(t1) * R * 1.3, Math.cos(t1) * R);
  }
  arch.push(cx, 0.3, -R, cx, 0.3, R, cx, cy, R, cx, 0.3, -R, cx, cy, R, cx, cy, -R);
  k.add(paint(tris(arch), '#101418'));
  // Estalactites penduradas no alto da boca: é gruta, não túnel.
  for (let i = 0; i < 6; i++) {
    const t = Math.PI * (0.28 + (i / 5) * 0.44); const len = 2.6 + hash2(i, 96) * 2.2;
    k.add(paint(cone(0.9, len, 5), '#d8cfb4', tf(cx + 0.3, cy + Math.sin(t) * R * 1.3 - len / 2 - 0.3, Math.cos(t) * R * 0.94, 1, 1, 1, Math.PI, 0, 0)));
  }
  const lake: number[] = [];
  const L = (y: number): number => Math.sqrt(Math.max(0, 1 - ((y - cy) / (R * 1.3)) ** 2)) * R * 0.86; // meia largura dentro do arco
  for (const [y0, y1] of [[0.4, 4], [4, 8], [8, 12]] as Array<[number, number]>) {
    const w0 = y0 < cy ? R * 0.86 : L(y0); const w1 = y1 < cy ? R * 0.86 : L(y1);
    lake.push(cx + 0.15, y0, -w0, cx + 0.15, y0, w0, cx + 0.15, y1, w1, cx + 0.15, y0, -w0, cx + 0.15, y1, w1, cx + 0.15, y1, -w1);
  }
  k.light(bothSides(paint(tris(lake), '#1a86ff')));
  // Passarela de madeira descendo até a boca.
  k.add(beam([cx + 14, 0.6, -14], [cx + 1, 1, -6], 2.2, WOOD, 0.3));
  for (let i = 0; i < 4; i++) k.add(paint(box(0.2, 1.1, 0.2), WOOD, tf(cx + 14 - i * 4.3, 1.2, -14 + i * 2.6)));
  return k.model([0, H + 8], 0.05, 91);
}

/**
 * Tuiuiú (jaburu) com proporção de verdade — 1,4 m de pé, 2,6 m de asa aberta — vezes `S`, de frente para `face`
 * (rad, 0 = +X): corpo branco, pescoço preto com o papo vermelho, bico longo preto. `pose`: de pé, de asas abertas
 * no ninho ou voando (corpo deitado, pescoço e pernas esticados, asas abertas).
 */
function jabiru(k: Kit, x: number, y: number, z: number, face: number, S: number, pose: 'stand' | 'wings' | 'fly'): void {
  // O tuiuiú baixado (peça `tuiuiu`, parts.ts) entra no lugar dos de pé: 1,6 m de verdade × S, a cabeça (+X da peça)
  // para `face`, como o procedural. O de asas abertas e o em voo continuam procedurais (a peça tem uma pose só).
  const part = pose === 'stand' ? landmarkPart('tuiuiu') : null;
  if (part) { k.add(part.at({ height: 1.6 * S }, x, y, z, -face)); return; }
  const c = Math.cos(face); const s = Math.sin(face);
  const P = (a: number, h: number, b = 0): V3 => [x + (a * c - b * s) * S, y + h * S, z + (a * s + b * c) * S];
  const M = (a: number, h: number, b: number, sx: number, sy: number, sz: number, tilt = 0): THREE.Matrix4 => tf(...P(a, h, b), sx * S, sy * S, sz * S, 0, -face, tilt);
  const BLACK = '#16161a'; const WHITE = '#f8f8f4';
  const fly = pose === 'fly';
  // Corpo (comprido em +X) e as asas. Em voo, as asas no plano do corpo; de asas abertas no ninho, as asas levantadas em
  // V e de frente para quem olha (a vela branca que se vê de longe; deitadas, de frente eram um risco).
  k.add(paint(ico(1, 0), WHITE, M(0, fly ? 0 : 0.95, 0, 0.42, 0.24, 0.22)));
  if (fly) for (const b of [-1, 1]) k.add(paint(box(0.5, 0.05, 1.25), WHITE, tf(...P(-0.05, 0.036, b * 0.68), S, S, S, -b * 0.12, -face, 0)));
  if (pose === 'wings') {
    for (const b of [-1, 1]) k.add(paint(box(0.06, 0.55, 1.25), WHITE, tf(...P(-0.05, 1.25, b * 0.62), S, S, S, -b * 0.5, -face, 0)));
  }
  if (fly) {
    // Pescoço e cabeça esticados para a frente, pernas para trás.
    k.add(beam(P(0.35, 0.02), P(0.85, 0.06), 0.11 * S, BLACK));
    k.add(paint(cyl(0.1, 0.12, 0.16, 6), '#d42828', M(0.38, 0.02, 0, 1, 1, 1, Math.PI / 2)));
    k.add(paint(ico(0.11, 0), BLACK, M(0.9, 0.07, 0, 1, 1, 1)));
    k.add(beam(P(0.95, 0.07), P(1.32, 0.02), 0.06 * S, BLACK));
    for (const b of [-0.05, 0.05]) k.add(beam(P(-0.35, -0.02, b), P(-1.0, -0.06, b), 0.04 * S, BLACK));
    return;
  }
  k.add(beam(P(0.28, 1.05), P(0.34, 1.5), 0.11 * S, BLACK));
  k.add(paint(cyl(0.1, 0.13, 0.16, 6), '#d42828', M(0.29, 1.13, 0, 1, 1, 1)));
  k.add(paint(ico(0.11, 0), BLACK, M(0.36, 1.56, 0, 1, 1, 1)));
  k.add(beam(P(0.42, 1.56), P(0.8, 1.6), 0.06 * S, BLACK));
  for (const b of [-0.07, 0.07]) k.add(beam(P(0, 0.78, b), P(0.02, 0, b), 0.04 * S, BLACK));
}

/**
 * Ninho de tuiuiú: a árvore alta da beira da baía com a copa escura e o ninho largo de gravetos no topo — um tuiuiú de
 * pé e outro de asas abertas nele, um terceiro chegando em voo —, a baía rasa embaixo com dois pescando. Os tuiuiús
 * têm a proporção de verdade ×3 e ficam de perfil para a pista (de frente, um tuiuiú é um palito), brancos contra a
 * copa escura e o céu. Antes, árvore de 16 m com tufos ralos e tuiuiús ×2,2 de frente: 99 m² de silhueta, um graveto.
 */
function tuiuiuNinho(): ReturnType<Kit['model']> {
  const k = new Kit();
  const bark = '#5e5040'; const H = 19; const S = 3;
  // Baía rasa ao lado da árvore (clara na borda, escura no meio) com a orla de capim, comprida ao longo da pista.
  k.add(paint(cyl(1, 1, 0.16, 14), '#8aa850', tf(-1, 0.08, 9, 11, 1, 19)));
  k.add(paint(cyl(1, 1, 0.2, 14), '#7aa6a0', tf(-1.3, 0.1, 9.5, 8.8, 1, 16)), paint(cyl(1, 1, 0.24, 14), '#4f7f80', tf(-2, 0.12, 10, 5.6, 1, 11)));
  // Tronco grosso, forquilha no alto e a copa: galhos com tufos largos e escuros abaixo do ninho (o ninho fica por cima).
  k.add(paint(cyl(0.85, 1.3, H, 7), bark, tf(0, H / 2, 0)));
  const forks: V3[] = [[2.8, H + 2.8, 1.2], [-2.8, H + 2.6, -1.4], [0.5, H + 3.2, 2.8], [-0.5, H + 2.8, -3]];
  for (const f of forks) k.add(beam([0, H - 0.4, 0], f, 0.6, bark));
  for (let b = 0; b < 7; b++) {
    const a = (b / 7) * 6.28 + hash2(111, b) * 0.6; const y = 8 + (b % 4) * 2.2;
    const tip: V3 = [Math.cos(a) * 4.6, y + 2.2, Math.sin(a) * 6.2];
    k.add(beam([0, y, 0], tip, 0.32, bark), paint(jitter(ico(1, 0), 0.2, 112 + b), b % 2 ? '#3e6e30' : '#4a7a34', tf(tip[0], tip[1] + 0.6, tip[2], 4, 2.4, 4.4)));
  }
  // Ninho: prato largo de gravetos sobre a forquilha, a borda desfiada.
  const ny = H + 3;
  k.add(paint(jitter(cyl(4.6, 2.8, 2, 10), 0.12, 113), '#6e5232', tf(0, ny, 0)));
  k.add(paint(cyl(3.7, 3.7, 0.3, 10), '#4e3a24', tf(0, ny + 0.92, 0)));
  for (let i = 0; i < 6; i++) { const a = i * 1.05 + 0.3; k.add(beam([Math.cos(a) * 3.3, ny + 0.2, Math.sin(a) * 3.3], [Math.cos(a) * 5.6, ny - 0.6, Math.sin(a) * 5.6], 0.24, '#7a5c38')); }
  // Casal no ninho (de perfil para a pista; o de asas abertas de frente, as asas em V) e o terceiro chegando.
  jabiru(k, 0.4, ny + 1, -1.8, Math.PI / 2, S, 'stand');
  jabiru(k, -0.8, ny + 1, 1.8, 0.1, S, 'wings');
  jabiru(k, 3, H + 9, -10, 0.35, S, 'fly');
  // Dois pescando na baía, de perfil.
  jabiru(k, 1.5, 0, 5, Math.PI / 2 + 0.3, S, 'stand');
  jabiru(k, 0, 0, 15, -Math.PI / 2 - 0.2, S, 'stand');
  return k.model([0, H + 8], 0.05, 111);
}

/**
 * Portal da Transpantaneira: o pórtico de troncos grossos (~17 m) com a travessa dupla, a tábua clara do nome (faixa
 * entalhada, sem letras) e o telhadinho, as lanternas, a cerca de curral de três réguas dos dois lados e a estrada de
 * terra que passa por ele e segue pela primeira das pontes de madeira, sobre o corixo com aguapés. Montado em 1/1,4 e
 * escalado, com troncos mais grossos e a tábua maior: com 13,5 m e troncos de 1,9 m, de 150 m era um risco marrom.
 */
function portalTranspantaneira(): ReturnType<Kit['model']> {
  const k = new Kit();
  const log = '#7a5230'; const span = 16; const H = 11;
  // Estrada de terra (clara) passando pelo portal até a ponte.
  k.add(paint(box(66, 0.25, 7.5), '#c8a070', tf(-24, 0.12, 0)));
  // Corixo com aguapés atravessando a estrada e a ponte de madeira sobre ele.
  k.add(paint(cyl(1, 1, 0.22, 12), '#6a9a88', tf(-40, 0.1, 0, 8, 1, 24)), paint(cyl(1, 1, 0.26, 12), '#4f7f74', tf(-40, 0.12, 0, 5, 1, 18)));
  for (let i = 0; i < 5; i++) k.add(paint(jitter(ico(1, 0), 0.2, 122 + i), '#5aa040', tf(-40 + (hash2(123, i) - 0.5) * 6, 0.35, (i < 3 ? -1 : 1) * (8 + hash2(124, i) * 10), 2.2, 0.3, 1.8)));
  k.add(paint(box(18, 0.5, 6.4), '#9a7a52', tf(-40, 1.5, 0)));
  for (const x of [-48, -43, -38, -33]) for (const s of [-1, 1]) k.add(paint(box(0.4, 3, 0.4), '#5a3e26', tf(x, 0.6, s * 3)));
  for (const s of [-1, 1]) k.add(paint(box(18, 0.2, 0.2), '#6a4a2a', tf(-40, 2.6, s * 3.1)));
  for (const s of [-1, 1]) {
    // Pilar de tronco grosso com a mão-francesa.
    k.add(paint(cyl(1.05, 1.2, H + 1.6, 8), log, tf(0, (H + 1.6) / 2, s * span / 2)));
    k.add(beam([0, 0.2, s * (span / 2 + 3.4)], [0, H * 0.6, s * span / 2], 0.55, log));
    k.add(beam([-3, 0.2, s * span / 2], [0, H * 0.55, s * span / 2], 0.55, log));
  }
  k.add(paint(cyl(0.85, 0.85, span + 5, 8), log, tf(0, H + 0.4, 0, 1, 1, 1, Math.PI / 2, 0, 0)));
  k.add(paint(cyl(0.6, 0.6, span, 8), log, tf(0, H - 3.9, 0, 1, 1, 1, Math.PI / 2, 0, 0)));
  // Tábua do nome: grande e clara, com a faixa entalhada escura e os cravos.
  k.add(paint(box(0.4, 3.0, 13.4), '#e2c488', tf(0.35, H - 1.85, 0)), paint(box(0.45, 1.3, 11.6), '#5a3a22', tf(0.4, H - 1.85, 0)));
  for (const dz of [-6.1, 6.1]) k.add(paint(box(0.5, 0.5, 0.5), '#3a2a1a', tf(0.45, H - 1.85, dz)));
  k.add(paint(gable(3.4, 1.8, span + 6, 0.2), '#6a4428', tf(0, H + 1.1, 0)));
  // Lanternas acesas nos pilares.
  for (const s of [-1, 1]) k.light(paint(box(0.5, 0.7, 0.5), LAMP, tf(1.0, H - 4.6, s * (span / 2 - 0.2))));
  // Cerca de curral de três réguas dos dois lados.
  for (const s of [-1, 1]) {
    for (let i = 0; i < 6; i++) k.add(paint(box(0.3, 2, 0.3), log, tf(0, 1, s * (span / 2 + 3.2 + i * 3))));
    for (const y of [0.55, 1.15, 1.75]) k.add(paint(box(0.14, 0.22, 15.4), '#9a7048', tf(0.12, y, s * (span / 2 + 10.7))));
  }
  return scaled(k.model([0, H + 2], 0.06, 121), 1.4);
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
