// Carroceria por seções: o "loft" da parte de baixo (do fundo ao capô/tampa) com os arcos das rodas
// recortados de verdade (poço escuro, arco em 10 facetas, para-lama que alarga e sobe), e o da cabine
// (para-brisa, teto, vidro traseiro). Cada estilo descreve só as seções-chave; as estações dos arcos e das
// pontas redondas entram sozinhas. `BodyShape` responde onde fica a superfície, para os detalhes pousarem nela.
// Onda I ("menos quadrado" pela geometria): bico e traseira em quarto de elipse em planta e de perfil, ombro
// em arco de três pontos, arco da roda de 10 facetas, borda do teto e dobras do para-brisa arredondadas.
import { loft, type Brush, type MeshBuilder, type P2, type P3 } from './kit';

/** Seção-chave da carroceria (metros). */
export interface Sec {
  z: number;
  /** Fundo (soleira) e topo na borda do capô/tampa. */
  yb: number; yt: number;
  /** Meia largura no vinco (a parte mais larga). */
  hw: number;
  /** Quanto o centro do topo sobe acima da borda (abaulado). */
  crown?: number;
  /** Recolhimento da soleira e recuo do ombro. */
  tuck?: number; sh?: number;
  /** Altura do vinco entre o fundo e o topo (0..1). */
  belt?: number;
}

export interface Axle {
  /** Centro da roda em z, raio, meia bitola (centro da roda em x) e largura do pneu. */
  z: number; r: number; x: number; w: number;
}

export type Region = 'under' | 'well' | 'sill' | 'lower' | 'band' | 'upper' | 'shoulder' | 'top' | 'stripe' | 'center';
/** Região de cada lado do anel (metade direita, de baixo para cima: 13 lados; a esquerda é o espelho). */
const REGIONS: Region[] = ['under', 'well', 'well', 'sill', 'lower', 'band', 'upper', 'upper', 'shoulder', 'shoulder', 'top', 'stripe', 'center'];
/** Pontos do anel: fundo, 12 de cada lado, centro do topo. */
export const RING_POINTS = 26;
/** Facetas do semicírculo do arco da roda. */
export const ARCH_FACETS = 10;
/** Pontos de cada ponta redonda (bico e traseira): o quarto de elipse em 4 passos de 22,5°. */
const END_ANGLES = [22.5, 45, 67.5].map((a) => (a * Math.PI) / 180);

export interface BodySpec {
  secs: Sec[];
  axles: readonly [Axle, Axle];
  /** Folga do arco sobre o pneu. */
  clear?: number;
  /** Alargamento do para-lama no arco (dianteiro, traseiro) e quanto a borda do para-lama sobe. */
  flare?: readonly [number, number];
  rise?: readonly [number, number];
  /** Altura da faixa lateral (camada B) acima do vinco. */
  band?: number;
  /** Faixas do capô/teto (camada A): x externo e interno. */
  stripe?: readonly [number, number];
  /**
   * Quanto o bico e a traseira arredondam entre as duas primeiras e as duas últimas seções (0 = reta entre elas,
   * como até a onda H; 1 = quarto de elipse, a superfície sai da tampa tangente a ela). Padrão 1.
   */
  round?: number;
  brush: (region: Region, z: number, inArch: boolean) => Brush | null;
  capFront: Brush; capBack: Brush;
}

interface Station { z: number; archY: number | null; bump: number; axle: number }

/** Fração da seção-chave i à i+1 em z, já com a ponta redonda (quarto de elipse) na primeira e na última. */
function easedT(secs: readonly Sec[], i: number, t: number, round: number): number {
  const n = secs.length;
  if (n < 3 || round <= 0) return t;
  if (i === 0) return t + (Math.sqrt(Math.max(0, 1 - (1 - t) ** 2)) - t) * round;
  if (i === n - 2) return t + (1 - Math.sqrt(Math.max(0, 1 - t * t)) - t) * round;
  return t;
}

function lerpSec(secs: readonly Sec[], z: number, round: number): Required<Sec> {
  let i = 0;
  while (i < secs.length - 2 && z > secs[i + 1].z) i++;
  const a = secs[i]; const b = secs[Math.min(i + 1, secs.length - 1)];
  const t0 = b.z === a.z ? 0 : Math.max(0, Math.min(1, (z - a.z) / (b.z - a.z)));
  const t = easedT(secs, i, t0, round);
  const l = (x: number | undefined, y: number | undefined, d: number) => (x ?? d) + ((y ?? d) - (x ?? d)) * t;
  return {
    z, yb: l(a.yb, b.yb, 0), yt: l(a.yt, b.yt, 0), hw: l(a.hw, b.hw, 0), crown: l(a.crown, b.crown, 0.03),
    tuck: l(a.tuck, b.tuck, 0.07), sh: l(a.sh, b.sh, 0.12), belt: l(a.belt, b.belt, 0.5),
  };
}

export class BodyShape {
  readonly stations: Station[] = [];
  readonly zFront: number; readonly zBack: number;
  private readonly round: number;

  constructor(readonly spec: BodySpec) {
    const secs = spec.secs;
    this.round = spec.round ?? 1;
    this.zFront = secs[0].z; this.zBack = secs[secs.length - 1].z;
    const st: Station[] = secs.map((s) => ({ z: s.z, archY: null, bump: 0, axle: -1 }));
    // Pontas redondas: estações no quarto de elipse entre as duas primeiras e as duas últimas seções.
    if (secs.length >= 3 && this.round > 0) {
      const [a, b] = [secs[0], secs[1]]; const [c, d] = [secs[secs.length - 2], secs[secs.length - 1]];
      for (const th of END_ANGLES) {
        st.push({ z: a.z + (b.z - a.z) * (1 - Math.cos(th)), archY: null, bump: 0, axle: -1 });
        st.push({ z: c.z + (d.z - c.z) * Math.sin(th), archY: null, bump: 0, axle: -1 });
      }
    }
    const clear = spec.clear ?? 0.06;
    spec.axles.forEach((ax, ai) => {
      const ra = ax.r + clear;
      // Estações do arco: rampa do para-lama, parede vertical, semicírculo em `ARCH_FACETS` facetas.
      st.push({ z: ax.z - ra * 1.45, archY: null, bump: 0, axle: ai }, { z: ax.z + ra * 1.45, archY: null, bump: 0, axle: ai });
      st.push({ z: ax.z - ra, archY: null, bump: 0, axle: ai }, { z: ax.z - ra, archY: ax.r, bump: 0, axle: ai });
      for (let k = ARCH_FACETS - 1; k >= 1; k--) {
        const rad = (k / ARCH_FACETS) * Math.PI;
        st.push({ z: ax.z + Math.cos(rad) * ra, archY: ax.r + Math.sin(rad) * ra, bump: 0, axle: ai });
      }
      st.push({ z: ax.z + ra, archY: ax.r, bump: 0, axle: ai }, { z: ax.z + ra, archY: null, bump: 0, axle: ai });
    });
    // Seções-chave (e estações das pontas) dentro de um arco seguem a curva do arco.
    for (const s of st) {
      spec.axles.forEach((ax, ai) => {
        const ra = ax.r + clear; const dz = s.z - ax.z;
        if (s.axle === -1 && Math.abs(dz) < ra) { s.archY = ax.r + Math.sqrt(ra * ra - dz * dz); s.axle = ai; }
        const u = Math.min(1, Math.abs(dz) / (ra * 1.45));
        s.bump = Math.max(s.bump, 0.5 + 0.5 * Math.cos(Math.PI * u));
      });
    }
    // Ordena por z; nas paredes do arco (mesmo z) a de fora vem antes na frente e depois atrás.
    st.sort((a, b) => a.z - b.z || this.wallOrder(a, b));
    this.stations = st.filter((s) => s.z >= this.zFront - 1e-9 && s.z <= this.zBack + 1e-9);
  }

  private wallOrder(a: Station, b: Station): number {
    const ax = this.spec.axles[Math.max(0, a.axle)];
    const front = a.z < ax.z;
    const ka = a.archY === null ? 0 : 1; const kb = b.archY === null ? 0 : 1;
    return front ? ka - kb : kb - ka;
  }

  /** Parâmetros efetivos numa estação (com arco, alargamento e subida do para-lama). */
  params(s: Station) {
    const sec = lerpSec(this.spec.secs, s.z, this.round);
    const axleIdx = s.axle >= 0 ? s.axle : (Math.abs(s.z - this.spec.axles[0].z) < Math.abs(s.z - this.spec.axles[1].z) ? 0 : 1);
    const flare = (this.spec.flare?.[axleIdx] ?? 0) * s.bump;
    const rise = (this.spec.rise?.[axleIdx] ?? 0) * s.bump;
    return { sec, flare, rise, yw: Math.max(sec.yb, s.archY ?? sec.yb) };
  }

  /** Anel de `RING_POINTS` pontos (anti-horário visto de trás). */
  ring(s: Station): P2[] {
    const { sec, flare, rise, yw: yw0 } = this.params(s);
    const { yb, yt, hw, crown, tuck, sh, belt } = sec;
    const ax = this.spec.axles[0];
    const band = this.spec.band ?? 0.06;
    const hwf = hw + flare;
    const yte = yt + rise;
    const xin = Math.min(ax.x - ax.w / 2 - 0.05, hwf - tuck - 0.03);
    // Sobre o arco o para-lama espreme vinco e faixa (a borda do para-lama fica logo acima do arco).
    const yw = Math.min(yw0, yte - 0.06);
    let ybelt = Math.max(yb + (yt - yb) * belt, yw + 0.03);
    const bandEff = Math.max(0.004, Math.min(band, (yte - ybelt) * 0.35));
    ybelt = Math.min(ybelt, yte - bandEff - 0.02);
    const top = hw - sh;
    const [sx1r, sx2r] = this.spec.stripe ?? [0.3, 0.1];
    const sx1 = Math.min(sx1r, top * 0.8); const sx2 = Math.min(sx2r, sx1 * 0.4);
    const topY = (x: number) => yte + (yt + crown - yte) * (1 - (x / top) ** 2);
    // Ombro: quarto de elipse do alto da faixa (tangente vertical) à borda do capô (tangente horizontal), com os
    // três pontos onde a tangente vale 67,5°, 45° e 22,5° — dobras de ~22° (lisas no vinco de carro).
    const x0 = hwf - 0.006; const y0 = ybelt + bandEff;
    const ea = Math.max(1e-4, x0 - top); const eb = Math.max(1e-4, yte - y0);
    const shoulder: P2[] = [67.5, 45, 22.5].map((deg) => {
      const phi = Math.atan2(eb, ea * Math.tan((deg * Math.PI) / 180));
      return [top + ea * Math.cos(phi), y0 + eb * Math.sin(phi)];
    });
    const right: P2[] = [
      [xin, yb], [xin, yw], [hwf - tuck, yw], [hwf - tuck * 0.3, yw + (ybelt - yw) * 0.4], [hwf, ybelt], [x0, y0],
      ...shoulder, [top, yte], [sx1, topY(sx1)], [sx2, topY(sx2)],
    ];
    const left = right.map(([x, y]) => [-x, y] as P2).reverse();
    return [[0, yb], ...right, [0, yt + crown], ...left];
  }

  build(b: MeshBuilder): void {
    const rings = this.stations.map((s) => this.ring(s));
    const zs = this.stations.map((s) => s.z);
    const half = RING_POINTS / 2;
    loft(b, rings, zs, (k, i) => {
      const e = k < half ? k : RING_POINTS - 1 - k;
      const s0 = this.stations[i]; const s1 = this.stations[i + 1];
      const inArch = s0.archY !== null && s1.archY !== null;
      return this.spec.brush(REGIONS[e], (s0.z + s1.z) / 2, inArch);
    }, true, this.spec.capFront, this.spec.capBack);
  }

  /** Meia largura (no vinco) e topo na borda e no centro numa posição z qualquer. */
  at(z: number): Required<Sec> { return lerpSec(this.spec.secs, z, this.round); }

  /** Estação avulsa em z (sem arco), para consultar a forma com a subida e o alargamento dos para-lamas. */
  private probe(z: number): Station {
    const clear = this.spec.clear ?? 0.06;
    let bump = 0; let axle = 0; let best = Infinity;
    this.spec.axles.forEach((ax, ai) => {
      const ra = ax.r + clear; const dz = z - ax.z;
      bump = Math.max(bump, 0.5 + 0.5 * Math.cos(Math.PI * Math.min(1, Math.abs(dz) / (ra * 1.45))));
      if (Math.abs(dz) < best) { best = Math.abs(dz); axle = ai; }
    });
    return { z, archY: null, bump, axle };
  }

  /** Topo da borda do para-lama (com a subida sobre as rodas) e meia largura (com o alargamento) em z. */
  edgeTop(z: number): number { const p = this.params(this.probe(z)); return p.sec.yt + p.rise; }
  halfWidth(z: number): number { const p = this.params(this.probe(z)); return p.sec.hw + p.flare; }

  /** Altura da superfície de cima em (x, z), fora dos para-lamas. */
  topAt(z: number, x: number): number {
    const p = this.params(this.probe(z));
    const s = p.sec;
    const top = s.hw - s.sh;
    const u = Math.min(1, Math.abs(x) / top);
    const yte = s.yt + p.rise;
    return yte + (s.yt + s.crown - yte) * (1 - u * u);
  }
}

/** Seção da cabine: base (na linha de cintura) e teto. */
export interface CabSec { z: number; hwb: number; yb: number; hwt: number; yt: number; crown?: number }

export interface CabinSpec {
  secs: CabSec[];
  /** Pincel de cima por vão (0 = para-brisa ... último = vidro traseiro) e da lateral por vão. */
  top: (gap: number, region: 'roof' | 'stripe' | 'center') => Brush;
  side: (gap: number) => Brush;
  stripe?: readonly [number, number];
  /** Raio da borda do teto (onde o vidro lateral vira teto) e o recuo das dobras do para-brisa e do vidro de trás. */
  edge?: number; bend?: number;
  /** Tampa da última seção (a parede de trás da cabine da picape), no contorno da cabine. */
  capBack?: Brush;
}

const lerpCab = (a: CabSec, b: CabSec, t: number): CabSec => ({
  z: a.z + (b.z - a.z) * t, hwb: a.hwb + (b.hwb - a.hwb) * t, yb: a.yb + (b.yb - a.yb) * t,
  hwt: a.hwt + (b.hwt - a.hwt) * t, yt: a.yt + (b.yt - a.yt) * t, crown: (a.crown ?? 0.03) + ((b.crown ?? 0.03) - (a.crown ?? 0.03)) * t,
});

/** Estações da cabine: as pontas como estão; cada seção do meio vira três (Bézier quadrática com o canto de controle). */
function cabinStations(spec: CabinSpec): { secs: CabSec[]; gaps: number[] } {
  const bend = spec.bend ?? 0.1;
  const ks = spec.secs;
  const secs: CabSec[] = [ks[0]]; const gaps: number[] = [];
  for (let i = 1; i < ks.length - 1; i++) {
    const p = ks[i - 1]; const s = ks[i]; const n = ks[i + 1];
    const fa = Math.min(0.3, bend / Math.max(1e-6, s.z - p.z)); const fb = Math.min(0.3, bend / Math.max(1e-6, n.z - s.z));
    const A = lerpCab(s, p, fa); const B = lerpCab(s, n, fb);
    const M = lerpCab(lerpCab(A, s, 0.5), lerpCab(s, B, 0.5), 0.5);
    const g = i >= 2 ? i - 1 : i; // o arco da dobra fica com o vão do teto
    gaps.push(i - 1, g, g);
    secs.push(A, M, B);
  }
  gaps.push(ks.length - 2);
  secs.push(ks[ks.length - 1]);
  return { secs, gaps };
}

/** Metade direita do anel da cabine: base, a borda do teto em arco (w1 = alto do vidro, meio, w2) e as faixas. */
function cabinRight(spec: CabinSpec, s: CabSec): P2[] {
  const [sx1r, sx2r] = spec.stripe ?? [0.3, 0.1];
  const edge = spec.edge ?? 0.07;
  const c = s.crown ?? 0.03;
  const sx1 = Math.min(sx1r, s.hwt * 0.8); const sx2 = Math.min(sx2r, sx1 * 0.4);
  const y = (x: number) => s.yt + c * (1 - (x / Math.max(1e-3, s.hwt)) ** 2);
  // Borda do teto: o canto (hwt, yt) vira um arco de três pontos (corta d de cada lado e o meio da curva).
  const base: P2 = [s.hwb, s.yb]; const w: P2 = [s.hwt, s.yt]; const s1: P2 = [sx1, y(sx1)];
  const ls = Math.hypot(w[0] - base[0], w[1] - base[1]); const lr = Math.hypot(s1[0] - w[0], s1[1] - w[1]);
  const d = Math.min(edge, ls * 0.45, lr * 0.45);
  const w1: P2 = ls > 1e-9 ? [w[0] + ((base[0] - w[0]) / ls) * d, w[1] + ((base[1] - w[1]) / ls) * d] : w;
  const w2: P2 = lr > 1e-9 ? [w[0] + ((s1[0] - w[0]) / lr) * d, w[1] + ((s1[1] - w[1]) / lr) * d] : w;
  const wm: P2 = [0.25 * w1[0] + 0.5 * w[0] + 0.25 * w2[0], 0.25 * w1[1] + 0.5 * w[1] + 0.25 * w2[1]];
  return [base, w1, wm, w2, s1, [sx2, y(sx2)]];
}

/**
 * Alto do vidro lateral (lado direito) em z: onde o vidro encontra a borda arredondada do teto. Para as colunas e
 * os frisos pousarem na cabine (o canto (hwt, yt) da seção-chave fica por fora do arco da borda).
 */
export function cabinGlassTop(spec: CabinSpec, z: number): P3 {
  const { secs } = cabinStations(spec);
  let i = 0;
  while (i < secs.length - 2 && z > secs[i + 1].z) i++;
  const a = secs[i]; const b = secs[i + 1];
  const t = b.z === a.z ? 0 : Math.max(0, Math.min(1, (z - a.z) / (b.z - a.z)));
  const pa = cabinRight(spec, a)[1]; const pb = cabinRight(spec, b)[1];
  return [pa[0] + (pb[0] - pa[0]) * t, pa[1] + (pb[1] - pa[1]) * t, a.z + (b.z - a.z) * t];
}

/**
 * Cabine: loft aberto embaixo (assenta na carroceria), anel de 13 pontos com a borda do teto arredondada, e as
 * dobras do meio (para-brisa → teto, teto → vidro de trás) trocadas por um arco de três estações. O vão de cada
 * pincel continua o da seção-chave (0 = para-brisa…); o arco de uma dobra fica com o vão do teto.
 */
export function buildCabin(b: MeshBuilder, spec: CabinSpec): void {
  const { secs, gaps } = cabinStations(spec);
  const rings = secs.map((s) => {
    const right = cabinRight(spec, s);
    const left = right.map(([x, yy]) => [-x, yy] as P2).reverse();
    return [...right, [0, s.yt + (s.crown ?? 0.03)] as P2, ...left];
  });
  const regions: Array<'side' | 'roof' | 'stripe' | 'center'> = ['side', 'roof', 'roof', 'roof', 'stripe', 'center'];
  loft(b, rings, secs.map((s) => s.z), (k, i) => {
    const e = k <= 5 ? k : 11 - k;
    const r = regions[e];
    const gap = gaps[i];
    return r === 'side' ? spec.side(gap) : spec.top(gap, r);
  }, false, null, spec.capBack ?? null);
}
