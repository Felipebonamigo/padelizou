// Kit dos marcos turísticos: um acumulador de peças por material (liso, luz, baliza, fachadas) e as formas que os
// marcos repetem — viga entre dois pontos (cabo, pilar inclinado, braço), sólido de revolução (cúpula, cuia, torre
// redonda), fachada com janelas que acendem à noite, morro facetado com saia abaixo do chão, paredão de rocha em
// estratos (falésia, cânion, cachoeira: `cliff`) e duna de crista viva (`dune`). Puro: só Three.
// A convenção do modelo está em types.ts (origem no centro da base, frente para +X, metros reais).
import * as THREE from 'three';
import { fbm, hash2, valueNoise } from '../../noise';
import { FACADE_TILE } from '../structures';
import { jitter, merge, paint, shadeY, speckle, tintUp, type Geo, type MatKey, type Model, type ModelPart } from '../geom';

type FacadeStyle = keyof typeof FACADE_TILE;

/** Peças de um marco por material; `model()` funde cada grupo numa parte (uma geometria por lote). */
export class Kit {
  readonly flat: Geo[] = [];
  readonly glow: Geo[] = [];
  readonly beacon: Geo[] = [];
  private readonly facades = new Map<FacadeStyle, Geo[]>();

  /** Peça lisa já pintada (paint). */
  add(...g: Geo[]): this { this.flat.push(...g); return this; }
  light(...g: Geo[]): this { this.glow.push(...g); return this; }
  blink(...g: Geo[]): this { this.beacon.push(...g); return this; }
  facade(style: FacadeStyle, g: Geo): this {
    const list = this.facades.get(style) ?? [];
    list.push(g);
    this.facades.set(style, list);
    return this;
  }

  /**
   * Funde. `shade` = [y0, y1] do sombreado por altura (pé mais escuro, topo mais claro); `seed`/`speck` variam o tom
   * de cada face (pedra, mata).
   */
  model(shade: [number, number], speck = 0, seed = 1): Model {
    const parts: ModelPart[] = [];
    if (this.flat.length) {
      let g = shadeY(merge(this.flat), shade[0], shade[1], 0.8, 1.06);
      if (speck > 0) g = speckle(g, speck, seed);
      parts.push({ geometry: g, mat: 'flat' });
    }
    for (const [style, list] of this.facades) parts.push({ geometry: merge(list), mat: style as MatKey });
    if (this.glow.length) parts.push({ geometry: merge(this.glow), mat: 'glow' });
    if (this.beacon.length) parts.push({ geometry: merge(this.beacon), mat: 'beacon' });
    return { parts, blob: 0 };
  }
}

const UP = new THREE.Vector3(0, 1, 0);
const tmpA = new THREE.Vector3();
const tmpB = new THREE.Vector3();
const tmpQ = new THREE.Quaternion();

/** Matriz que leva o eixo +Y (de 0 a 1) ao segmento de `a` a `b`. */
function along(a: [number, number, number], b: [number, number, number], w: number, d: number): THREE.Matrix4 {
  tmpA.set(a[0], a[1], a[2]); tmpB.set(b[0], b[1], b[2]);
  const dir = tmpB.clone().sub(tmpA);
  const len = dir.length();
  tmpQ.setFromUnitVectors(UP, dir.normalize());
  const mid = tmpA.add(tmpB).multiplyScalar(0.5);
  return new THREE.Matrix4().compose(mid, tmpQ, new THREE.Vector3(w, len, d));
}

/** Viga de seção retangular `w` × `d` entre dois pontos (12 triângulos). */
export function beam(a: [number, number, number], b: [number, number, number], w: number, color: THREE.ColorRepresentation, d = w): Geo {
  return paint(new THREE.BoxGeometry(1, 1, 1), color, along(a, b, w, d));
}

/** Cabo: prisma triangular aberto entre dois pontos (6 triângulos), espessura `t`. */
export function cable(a: [number, number, number], b: [number, number, number], t: number, color: THREE.ColorRepresentation): Geo {
  return paint(new THREE.CylinderGeometry(0.5, 0.5, 1, 3, 1, true), color, along(a, b, t, t));
}

/** Sólido de revolução em torno de Y: `profile` = [raio, altura] de baixo para cima; `seg` lados. */
export function lathe(profile: Array<[number, number]>, seg: number, color: THREE.ColorRepresentation, m?: THREE.Matrix4): Geo {
  const pts = profile.map(([r, y]) => new THREE.Vector2(Math.max(0.0001, r), y));
  return paint(new THREE.LatheGeometry(pts, seg), color, m);
}

/** Caixa com uv em metros para a textura de fachada (`structures.FACADE_TILE`): janelas de ~3 m que acendem à noite. */
export function facadeBox(style: FacadeStyle, w: number, h: number, d: number, color: THREE.ColorRepresentation, m: THREE.Matrix4, vOffset = 0): Geo {
  const g = new THREE.BoxGeometry(w, h, d);
  const uv = g.attributes.uv as THREE.BufferAttribute;
  const [tu, tv] = FACADE_TILE[style];
  const faces: Array<[number, number] | null> = [[d, h], [d, h], null, null, [w, h], [w, h]];
  for (let f = 0; f < 6; f++) {
    const s = faces[f];
    for (let k = 0; k < 4; k++) {
      const i = f * 4 + k;
      if (!s) uv.setXY(i, 0.01, 0.01);
      else uv.setXY(i, (uv.getX(i) * s[0]) / tu, (uv.getY(i) * s[1] + vOffset) / tv);
    }
  }
  return paint(g, color, m, true);
}

/**
 * Morro facetado: icosaedro deformado por hash, achatado em `rx` × `h` × `rz`, com a base `skirt` metros abaixo do
 * chão (pousa em declive sem mostrar o fundo). `top` pinta as faces de cima (mata, grama), `rock` as encostas.
 */
export function hill(rx: number, h: number, rz: number, seed: number, rock: string, top: string | null, detail = 1, amount = 0.16, skirt = 0.25, m?: THREE.Matrix4): Geo {
  const g = jitter(new THREE.IcosahedronGeometry(1, detail), amount, seed);
  // Meia esfera de cima vira o morro; a de baixo, a saia (achatada para baixo do chão).
  const p = g.attributes.position as THREE.BufferAttribute;
  for (let i = 0; i < p.count; i++) {
    const y = p.getY(i);
    p.setXYZ(i, p.getX(i) * rx, y >= 0 ? y * h : y * h * skirt, p.getZ(i) * rz);
  }
  const out = paint(g, rock, m);
  if (top) tintUp(out, top, 0.55);
  return out;
}

// ───────────────────────── Paredão de rocha (falésia, cânion) e duna ─────────────────────────
// Superfícies contínuas de faces planas, pintadas por triângulo (não caixas empilhadas): a pedra lê como pedra
// de longe — estratos, cornijas com a linha de sombra, sulcos de erosão, borda de cima quebrada, talude no pé —
// e a duna como duna — crista viva, barlavento com as ondulações, face de avalanche lisa e mais escura.

/** Acumula o quadrilátero a, b, c, d (dois triângulos) numa cor. */
function pushQuad(pos: number[], col: number[], a: readonly number[], b: readonly number[], c: readonly number[], d: readonly number[], color: THREE.Color): void {
  pos.push(a[0], a[1], a[2], b[0], b[1], b[2], c[0], c[1], c[2], a[0], a[1], a[2], c[0], c[1], c[2], d[0], d[1], d[2]);
  for (let i = 0; i < 6; i++) col.push(color.r, color.g, color.b);
}

/** Geometria não indexada a partir das posições e cores por vértice (e a transformação). */
function coloredGeo(pos: number[], col: number[], m?: THREE.Matrix4): Geo {
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  g.setAttribute('color', new THREE.Float32BufferAttribute(col, 3));
  if (m) g.applyMatrix4(m);
  g.computeVertexNormals();
  return g;
}

const smooth01 = (t: number): number => { const c = Math.min(1, Math.max(0, t)); return c * c * (3 - 2 * c); };
const tone = (c: THREE.Color, f: number): THREE.Color => c.clone().multiplyScalar(f);

export interface CliffOptions {
  /** Comprimento em Z (centrado na origem) e altura média do topo (m). A face olha para +X, o pé em x ≈ 0. */
  len: number;
  H: number;
  /** Cores dos estratos de baixo para cima (repetem); `layers` camadas de espessura desigual, contínuas no paredão. */
  strata: readonly string[];
  layers: number;
  seed: number;
  /** Colunas ao longo de Z (a resolução da face; padrão ≈ len / 10, entre 6 e 48). */
  cols?: number;
  /** Fundo do bloco (m, da face para trás): fechado atrás e nas pontas, sem lado oco. */
  depth?: number;
  /** Recuo da face por metro de altura (0 = a prumo). */
  batter?: number;
  /** Sulcos verticais de erosão: profundidade (m) e espaçamento médio (m). */
  gully?: number;
  gullyLen?: number;
  /** Baías e pontas na planta (m). */
  bay?: number;
  /** Borda de cima quebrada: variação do topo (fração de H). */
  ragged?: number;
  /** Saliência das camadas duras (m): as cornijas que fazem as linhas de luz e de sombra dos estratos. */
  ledge?: number;
  /** Talude de detritos no pé: altura média (m; 0 = sem) e cor. */
  talus?: number;
  talusColor?: string;
  /** Cor do topo (campo, mato, caatinga; null = a rocha de cima) e das prateleiras das cornijas (null = rocha clara). */
  top?: string | null;
  ledgeTop?: string | null;
  /**
   * Fração do comprimento em que as pontas baixam e recuam (o bloco termina sem corte reto; 0 = corte reto); um par
   * dá a ponta de −Z e a de +Z (corte reto onde outro paredão encosta, como a boca de um cânion).
   */
  ends?: number | readonly [number, number];
  /** Vão (cachoeira, boca do cânion): no trecho |z − z0| < half o topo baixa para `h` e a face recua `recess` m. */
  notch?: { z: number; half: number; h: number; recess: number };
  /** Quanto o pé desce abaixo do chão (pousa em declive). */
  skirt?: number;
}

export interface Cliff {
  geo: Geo;
  /** Altura do topo em z (no referencial do paredão, antes de `m`). */
  topAt: (z: number) => number;
  /** x da quina de cima (borda da face) em z. */
  lipAt: (z: number) => number;
  /** x da face no pé (acima do talude) em z. */
  footAt: (z: number) => number;
}

/**
 * Paredão de rocha ao longo de Z com a face para +X: um bloco sólido de colunas irregulares cujo perfil sobe do
 * talude, passa pelos estratos (camadas duras salientes com a parte de baixo na sombra, moles recuadas com a
 * prateleira de cima na luz), chega à quina quebrada do topo e desce atrás. Sulcos verticais (mais fundos no alto)
 * escurecem a cor; cada coluna tem um tom próprio (as manchas verticais da rocha). Uma geometria pintada (`flat`).
 */
export function cliff(o: CliffOptions, m?: THREE.Matrix4): Cliff {
  const s = o.seed; const half = o.len / 2; const L = Math.max(1, o.layers);
  const cols = o.cols ?? Math.max(6, Math.min(48, Math.round(o.len / 10)));
  const batter = o.batter ?? 0.1;
  const gully = o.gully ?? o.H * 0.1;
  const gullyLen = o.gullyLen ?? Math.max(10, o.len / 12);
  const bay = o.bay ?? o.len * 0.025;
  const ragged = o.ragged ?? 0.1;
  const ledge = o.ledge ?? Math.max(0.4, o.H * 0.03);
  const talus = o.talus ?? o.H * 0.2;
  const [endA, endB] = typeof o.ends === 'number' ? [o.ends, o.ends] : o.ends ?? [0.14, 0.14];
  const skirt = o.skirt ?? 1;
  const depth = Math.max(o.depth ?? o.H * 0.8, (batter * o.H + gully) * 2.6 + 4);
  // Camadas: espessura desigual, a última passa do topo mais alto (a borda quebrada corta as de cima).
  const ys: number[] = [0]; const thick: number[] = []; let sum = 0;
  for (let i = 0; i < L; i++) { const t = 0.55 + hash2(s, 40 + i) * 0.9; thick.push(t); sum += t; }
  for (let i = 0; i < L; i++) ys.push(ys[i] + (thick[i] / sum) * o.H * (1 + ragged * 1.2));
  const hard = (i: number): boolean => hash2(s, 60 + i) < 0.55;
  const endF = (z: number): number => { const e = z < 0 ? endA : endB; return e > 0 ? smooth01((Math.abs(z) / half - (1 - e)) / e) : 0; };
  const notchF = (z: number): number => (o.notch ? 1 - smooth01((Math.abs(z - o.notch.z) - o.notch.half) / (o.notch.half * 0.35 + 2)) : 0);
  const planX = (z: number): number => bay * fbm(s + 1, z / Math.max(30, o.len * 0.2) + 3.1, 2) - endF(z) * depth * 0.45 - notchF(z) * (o.notch?.recess ?? 0);
  const topAt = (z: number): number => {
    let h = o.H * (1 + ragged * fbm(s + 2, z / Math.max(10, o.len * 0.035) + 7.3, 4));
    h *= 1 - 0.85 * endF(z);
    if (o.notch) h += (o.notch.h - h) * notchF(z);
    return Math.max(0.6, h);
  };
  const gullyAt = (z: number): number =>
    gully * (0.8 * (0.5 + 0.5 * valueNoise(s + 3, z / gullyLen)) ** 2.2 + 0.2 * (0.5 + 0.5 * valueNoise(s + 4, z / (gullyLen * 0.37))));
  const wallX = (z: number, y: number, g: number): number => planX(z) - batter * y - g * (0.35 + 0.65 * Math.min(1, y / o.H));
  const lipAt = (z: number): number => wallX(z, topAt(z), gullyAt(z));
  const talusAt = (j: number, H: number): number => (talus > 0 ? Math.min(H * 0.7, talus * (0.55 + 0.9 * hash2(s + 6, j))) : 0);
  const footAt = (z: number): number => wallX(z, talus, gullyAt(z));
  // As divisas das camadas ondulam um pouco (a camada não é régua) e cada ponto da face sai um pouco do prumo.
  const wave = (0.2 * o.H) / L;
  const layerY = (i: number, z: number): number => (i === 0 ? 0 : ys[i] + wave * valueNoise(s + 20 + i, z / (gullyLen * 2.5)));
  const nudge = ledge * 0.8 + gully * 0.08;

  type Kind = 'talus' | 'face' | 'under' | 'shelf' | 'top' | 'back';
  const stepKind = (i: number): Kind => (i === 0 ? 'face' : hard(i) && !hard(i - 1) ? 'under' : !hard(i) && hard(i - 1) ? 'shelf' : 'face');
  const kinds: Kind[] = ['talus']; const bands: number[] = [0];
  for (let i = 0; i < L; i++) { kinds.push(stepKind(i), 'face'); bands.push(i, i); }
  kinds.push('top', 'top', 'back'); bands.push(L - 1, L - 1, 0);

  const zs: number[] = [];
  for (let j = 0; j <= cols; j++) zs.push(-half + ((j + (j > 0 && j < cols ? (hash2(s + 5, j) - 0.5) * 0.5 : 0)) / cols) * o.len);
  const gs: number[] = [];
  const prof: Array<Array<[number, number]>> = zs.map((z, j) => {
    const H = topAt(z); const g = gullyAt(z); gs.push(g);
    const tH = talusAt(j, H);
    const pts: Array<[number, number]> = [];
    pts.push([wallX(z, 0, g) + (talus > 0 ? tH * (1.3 + 0.6 * hash2(s + 7, j)) : ledge), -skirt]);
    pts.push([wallX(z, tH, g) + ledge * 0.6, tH]);
    let last = pts[1];
    for (let i = 0; i < L; i++) {
      const y0 = Math.min(H, Math.max(tH, layerY(i, z))); const y1 = Math.min(H, Math.max(tH, layerY(i + 1, z)));
      if (y1 - y0 < 0.01) { pts.push(last, last); continue; }
      // Cornija dura saliente (em trechos quase rente: a linha de sombra quebra) ou camada mole recuada.
      const hh = valueNoise(s + 30 + i, z / (gullyLen * 0.8)) * 0.5 + 0.5;
      const off = hard(i) ? ledge * (0.15 + 1.35 * hh * hh) : -ledge * 0.5 * hash2(s + 9, j * 17 + i);
      const a: [number, number] = [wallX(z, y0, g) + off + (hash2(s + 14, j * 61 + i * 2) - 0.5) * nudge, y0 + Math.min(0.3, (y1 - y0) * 0.06)];
      const b: [number, number] = [wallX(z, y1, g) + off + (hash2(s + 14, j * 61 + i * 2 + 1) - 0.5) * nudge, y1];
      pts.push(a, b); last = b;
    }
    const back = planX(z) - depth;
    pts.push([last[0] - (last[0] - back) * (0.3 + 0.15 * hash2(s + 10, j)), H + o.H * ragged * 0.15 * (hash2(s + 11, j) - 0.5)]);
    pts.push([back, H * 0.96]);
    pts.push([back - Math.max(4, H * 0.35), -skirt]);
    return pts;
  });

  const strata = o.strata.map((c) => new THREE.Color(c));
  const band = (i: number): THREE.Color => strata[((i % strata.length) + strata.length) % strata.length];
  const cTalus = new THREE.Color(o.talusColor ?? o.strata[0]);
  const cTop = o.top ? new THREE.Color(o.top) : null;
  const cShelf = o.ledgeTop ? new THREE.Color(o.ledgeTop) : null;
  const pos: number[] = []; const col: number[] = [];
  const same = (p: [number, number], q: [number, number]): boolean => Math.abs(p[0] - q[0]) < 1e-4 && Math.abs(p[1] - q[1]) < 1e-4;
  for (let j = 0; j < cols; j++) {
    const za = zs[j]; const zb = zs[j + 1]; const A = prof[j]; const B = prof[j + 1];
    const ao = 1 - 0.38 * ((gs[j] + gs[j + 1]) / 2 / Math.max(1e-6, gully));
    const streak = 0.9 + 0.2 * hash2(s + 12, j);
    for (let q = 0; q < kinds.length; q++) {
      if (same(A[q], A[q + 1]) && same(B[q], B[q + 1])) continue;
      const h = hash2(s + 13, j * 97 + q);
      const kind = kinds[q]; const bi = bands[q];
      let c: THREE.Color;
      if (kind === 'talus') c = tone(cTalus, 0.86 + 0.24 * h);
      else if (kind === 'face') c = tone(band(bi), (0.9 + 0.16 * h) * ao * streak);
      else if (kind === 'under') c = tone(band(bi), 0.64 * ao);
      else if (kind === 'shelf') c = cShelf ? tone(cShelf, 0.88 + 0.22 * h) : tone(band(bi - 1), 1.14);
      else if (kind === 'top') c = cTop ? tone(cTop, 0.86 + 0.24 * h) : tone(band(L - 1), 1.04 + 0.1 * h);
      else c = tone(band(0), 0.72);
      pushQuad(pos, col, [A[q][0], A[q][1], za], [A[q + 1][0], A[q + 1][1], za], [B[q + 1][0], B[q + 1][1], zb], [B[q][0], B[q][1], zb], c);
    }
  }
  // Tampas das pontas: leque a partir de um ponto de dentro do bloco (o perfil fecha pelo fundo, abaixo do chão); a
  // de −Z percorre o perfil ao contrário (normal para fora).
  for (const j of [0, cols]) {
    const P = prof[j]; const z = zs[j];
    const cx = (P[P.length - 4][0] + P[P.length - 2][0]) / 2; const cy = topAt(z) * 0.35;
    for (let q = 0; q < P.length; q++) {
      const a = P[q]; const b = P[(q + 1) % P.length];
      if (same(a, b)) continue;
      const c = tone(band(Math.floor(((a[1] + b[1]) / 2 / o.H) * L)), 0.7);
      if (j === 0) pos.push(cx, cy, z, b[0], b[1], z, a[0], a[1], z);
      else pos.push(cx, cy, z, a[0], a[1], z, b[0], b[1], z);
      for (let k = 0; k < 3; k++) col.push(c.r, c.g, c.b);
    }
  }
  return { geo: coloredGeo(pos, col, m), topAt, lipAt, footAt };
}

export interface DuneOptions {
  /** Comprimento da crista (Z), altura da crista e fundo do barlavento (m, da crista para −X). */
  len: number;
  H: number;
  back: number;
  seed: number;
  /** Barlavento (claro, com as ondulações) e face de avalanche (lisa, mais escura). */
  sand: string;
  slip: string;
  cols?: number;
  /** Faixas do barlavento (as ímpares são as ondulações). */
  rows?: number;
  /** Quanto a crista serpenteia em X e quanto as pontas avançam a favor do vento (barcana), em m. */
  sinuous?: number;
  horns?: number;
  /** Contraste das ondulações (fração do tom). */
  ripple?: number;
}

export interface Dune {
  geo: Geo;
  /** Ponto da crista em z: [x, y] (antes de `m`). */
  crestAt: (z: number) => [number, number];
  /** x do pé da face de avalanche em z. */
  toeAt: (z: number) => number;
}

/**
 * Duna com o vento soprando para +X: barlavento suave de −X até a crista viva (faixas de ondulação paralelas à
 * crista, levemente em relevo e de tom alternado) e a face de avalanche a ~32° para +X (lisa, mais escura). A
 * altura cai a zero nas pontas (fecha sem tampa); `horns` curva as pontas para a frente. Uma geometria pintada.
 */
export function dune(o: DuneOptions, m?: THREE.Matrix4): Dune {
  const s = o.seed; const half = o.len / 2;
  const cols = o.cols ?? Math.max(8, Math.min(28, Math.round(o.len / 12)));
  const rows = o.rows ?? 8;
  const sinuous = o.sinuous ?? o.len * 0.06;
  const horns = o.horns ?? 0;
  const ripple = o.ripple ?? 0.06;
  const slipRun = 1.6; // face de avalanche a ~32°
  const shape = (z: number): number => Math.max(0, 1 - Math.min(1, Math.abs(z) / half) ** 2) ** 0.7;
  const crestX = (z: number): number => sinuous * fbm(s + 1, z / Math.max(20, o.len * 0.3) + 1.7, 2) + horns * Math.min(1, Math.abs(z) / half) ** 2;
  const crestH = (z: number): number => o.H * shape(z) * (1 + 0.12 * fbm(s + 2, z / Math.max(15, o.len * 0.15) + 4.1, 2));
  const prof = (t: number): number => t ** 1.25 * (1.3 - 0.3 * t);
  const crestAt = (z: number): [number, number] => [crestX(z), crestH(z)];
  const toeAt = (z: number): number => crestX(z) + crestH(z) * slipRun + 0.8;
  const zs: number[] = [];
  for (let j = 0; j <= cols; j++) zs.push(-half + ((j + (j > 0 && j < cols ? (hash2(s + 5, j) - 0.5) * 0.4 : 0)) / cols) * o.len);
  const rowsAt = zs.map((z) => {
    const h = crestH(z); const xc = crestX(z);
    const xb = xc - o.back * (0.55 + 0.45 * shape(z));
    const pts: Array<[number, number]> = [];
    for (let u = 0; u <= rows; u++) {
      const t = u / rows;
      const rip = u > 0 && u < rows && u % 2 ? h * 0.012 + 0.06 : 0;
      pts.push([xb + (xc - xb) * t, u === 0 ? -0.5 : h * prof(t) + rip]);
    }
    pts.push([xc + h * slipRun * 0.52, h * 0.44]);
    pts.push([xc + h * slipRun + 0.8, -0.5]);
    return pts;
  });
  const cSand = new THREE.Color(o.sand); const cSlip = new THREE.Color(o.slip);
  const pos: number[] = []; const col: number[] = [];
  for (let j = 0; j < cols; j++) {
    const A = rowsAt[j]; const B = rowsAt[j + 1]; const za = zs[j]; const zb = zs[j + 1];
    for (let q = 0; q < A.length - 1; q++) {
      const h = hash2(s + 7, j * 53 + q);
      let c: THREE.Color;
      if (q < rows) c = tone(cSand, (q % 2 ? 1 - ripple : 1 + ripple * 0.4) * (0.97 + 0.05 * h) * (q === rows - 1 ? 1.05 : 1));
      else c = tone(cSlip, (q === rows ? 1 : 0.93) * (0.96 + 0.05 * h));
      // O perfil anda de −X para +X (ao contrário do paredão): a ordem dos cantos deixa a normal para fora.
      pushQuad(pos, col, [A[q][0], A[q][1], za], [B[q][0], B[q][1], zb], [B[q + 1][0], B[q + 1][1], zb], [A[q + 1][0], A[q + 1][1], za], c);
    }
  }
  return { geo: coloredGeo(pos, col, m), crestAt, toeAt };
}
