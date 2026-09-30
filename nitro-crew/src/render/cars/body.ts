// Carroceria por seções: o "loft" da parte de baixo (do fundo ao capô/tampa) com os arcos das rodas
// recortados de verdade (poço escuro, arco facetado, para-lama que alarga e sobe), e o da cabine
// (para-brisa, teto, vidro traseiro). Cada estilo descreve só as seções-chave; as estações dos arcos
// entram sozinhas. `BodyShape` responde onde fica a superfície, para os detalhes pousarem nela.
import { loft, type Brush, type MeshBuilder, type P2 } from './kit';

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
const REGIONS: Region[] = ['under', 'well', 'well', 'sill', 'lower', 'band', 'upper', 'shoulder', 'top', 'stripe', 'center'];

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
  brush: (region: Region, z: number, inArch: boolean) => Brush | null;
  capFront: Brush; capBack: Brush;
}

interface Station { z: number; archY: number | null; bump: number; axle: number }

function lerpSec(secs: readonly Sec[], z: number): Required<Sec> {
  let i = 0;
  while (i < secs.length - 2 && z > secs[i + 1].z) i++;
  const a = secs[i]; const b = secs[Math.min(i + 1, secs.length - 1)];
  const t = b.z === a.z ? 0 : Math.max(0, Math.min(1, (z - a.z) / (b.z - a.z)));
  const l = (x: number | undefined, y: number | undefined, d: number) => (x ?? d) + ((y ?? d) - (x ?? d)) * t;
  return {
    z, yb: l(a.yb, b.yb, 0), yt: l(a.yt, b.yt, 0), hw: l(a.hw, b.hw, 0), crown: l(a.crown, b.crown, 0.03),
    tuck: l(a.tuck, b.tuck, 0.07), sh: l(a.sh, b.sh, 0.12), belt: l(a.belt, b.belt, 0.5),
  };
}

export class BodyShape {
  readonly stations: Station[] = [];
  readonly zFront: number; readonly zBack: number;

  constructor(readonly spec: BodySpec) {
    const secs = spec.secs;
    this.zFront = secs[0].z; this.zBack = secs[secs.length - 1].z;
    const st: Station[] = secs.map((s) => ({ z: s.z, archY: null, bump: 0, axle: -1 }));
    const clear = spec.clear ?? 0.06;
    spec.axles.forEach((ax, ai) => {
      const ra = ax.r + clear;
      // Estações do arco: rampa do para-lama, parede vertical, semicírculo em 6 facetas.
      const angles = [150, 120, 90, 60, 30];
      st.push({ z: ax.z - ra * 1.45, archY: null, bump: 0, axle: ai }, { z: ax.z + ra * 1.45, archY: null, bump: 0, axle: ai });
      st.push({ z: ax.z - ra, archY: null, bump: 0, axle: ai }, { z: ax.z - ra, archY: ax.r, bump: 0, axle: ai });
      for (const a of angles) {
        const rad = (a * Math.PI) / 180;
        st.push({ z: ax.z + Math.cos(rad) * ra, archY: ax.r + Math.sin(rad) * ra, bump: 0, axle: ai });
      }
      st.push({ z: ax.z + ra, archY: ax.r, bump: 0, axle: ai }, { z: ax.z + ra, archY: null, bump: 0, axle: ai });
    });
    // Seções-chave dentro de um arco seguem a curva do arco.
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
    const sec = lerpSec(this.spec.secs, s.z);
    const axleIdx = s.axle >= 0 ? s.axle : (Math.abs(s.z - this.spec.axles[0].z) < Math.abs(s.z - this.spec.axles[1].z) ? 0 : 1);
    const flare = (this.spec.flare?.[axleIdx] ?? 0) * s.bump;
    const rise = (this.spec.rise?.[axleIdx] ?? 0) * s.bump;
    return { sec, flare, rise, yw: Math.max(sec.yb, s.archY ?? sec.yb) };
  }

  /** Anel de 22 pontos (anti-horário visto de trás). */
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
    const right: P2[] = [
      [xin, yb], [xin, yw], [hwf - tuck, yw], [hwf - tuck * 0.3, yw + (ybelt - yw) * 0.4], [hwf, ybelt], [hwf - 0.006, ybelt + bandEff],
      [hw - sh * 0.45, ybelt + bandEff + (yte - ybelt - bandEff) * 0.62], [top, yte], [sx1, topY(sx1)], [sx2, topY(sx2)],
    ];
    const left = right.map(([x, y]) => [-x, y] as P2).reverse();
    return [[0, yb], ...right, [0, yt + crown], ...left];
  }

  build(b: MeshBuilder): void {
    const rings = this.stations.map((s) => this.ring(s));
    const zs = this.stations.map((s) => s.z);
    loft(b, rings, zs, (k, i) => {
      const e = k <= 10 ? k : 21 - k;
      const s0 = this.stations[i]; const s1 = this.stations[i + 1];
      const inArch = s0.archY !== null && s1.archY !== null;
      return this.spec.brush(REGIONS[e], (s0.z + s1.z) / 2, inArch);
    }, true, this.spec.capFront, this.spec.capBack);
  }

  /** Meia largura (no vinco) e topo na borda e no centro numa posição z qualquer. */
  at(z: number): Required<Sec> { return lerpSec(this.spec.secs, z); }

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
}

/** Cabine: loft aberto embaixo (assenta na carroceria), anel de 9 pontos. */
export function buildCabin(b: MeshBuilder, spec: CabinSpec): void {
  const [sx1r, sx2r] = spec.stripe ?? [0.3, 0.1];
  const rings = spec.secs.map((s) => {
    const c = s.crown ?? 0.03;
    const sx1 = Math.min(sx1r, s.hwt * 0.8); const sx2 = Math.min(sx2r, sx1 * 0.4);
    const y = (x: number) => s.yt + c * (1 - (x / Math.max(1e-3, s.hwt)) ** 2);
    const right: P2[] = [[s.hwb, s.yb], [s.hwt, s.yt], [sx1, y(sx1)], [sx2, y(sx2)]];
    const left = right.map(([x, yy]) => [-x, yy] as P2).reverse();
    return [...right, [0, s.yt + c] as P2, ...left];
  });
  const regions: Array<'side' | 'roof' | 'stripe' | 'center'> = ['side', 'roof', 'stripe', 'center'];
  loft(b, rings, spec.secs.map((s) => s.z), (k, i) => {
    const e = k <= 3 ? k : 7 - k;
    const r = regions[e];
    return r === 'side' ? spec.side(i) : spec.top(i, r);
  }, false, null, null);
}
