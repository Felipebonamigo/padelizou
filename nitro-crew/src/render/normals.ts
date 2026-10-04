// Normais suaves com vinco (auto smooth) para malhas NÃO indexadas: o cenário e os carros guardam a cor (e a
// pintura, o material) por face, então a malha não pode compartilhar vértices — mas a luz pode. Aresta com dobra
// menor que `creaseDeg` (o ângulo entre as normais das duas faces) é lisa; mais que isso, viva. Em cada ponto, as
// faces ligadas por arestas lisas formam um leque e dividem UMA normal (a média das normais delas, ponderada pelo
// ângulo do canto): sem costura de luz dentro de uma região lisa, como o "auto smooth" dos editores 3D. Cilindro,
// cúpula, copa e para-lama ficam redondos à luz; caixa (90°), beiral e vinco de lataria não.
// Só a normal muda: posição, cor e os outros atributos ficam como estavam, face por face. Determinístico (a
// mesma malha dá a mesma normal, bit a bit) e puro (só Three, sem DOM). Ver docs/VISUAL.md, "Sombreamento".
import * as THREE from 'three';

/** Tolerância de solda (m): pontos mais perto que isso são o mesmo ponto (contas em float32 de peças fundidas). */
export const WELD_EPS = 1e-4;

/**
 * Giro em planta (graus) a partir do qual a aresta entre duas águas inclinadas fica viva, qualquer que seja o vinco:
 * as águas de um telhado baixo de quatro águas dobram só ~28° entre si (menos que um cilindro de 10 lados) e
 * viravam travesseiro; o que as separa do gomo de uma cúpula (45° com 8 gomos) é que viram 90° em planta.
 */
export const ROOF_TURN_DEG = 75;

export interface SmoothOptions {
  /** Tolerância de solda (m); padrão `WELD_EPS`. */
  eps?: number;
  /** Liga a regra do telhado (`ROOF_TURN_DEG`): construções e marcos. */
  roofTurnDeg?: number;
}

/**
 * Vinco (graus) por família, escolhido nas capturas antes/depois (docs/VISUAL.md, "Sombreamento"). Abaixo de 90
 * sempre: caixa, beiral e degrau continuam vivos. A família de cada modelo do cenário: `scenery/smooth.ts`.
 */
export const CREASE = {
  /** Carroceria (procedural e glTF): painéis e para-lamas lisos; vinco de cintura, para-choque e vidro vivos. */
  car: 45,
  /** Rodas: o pneu de 12 lados (30°) fica redondo; raios e porcas (caixas) continuam caixas. */
  wheel: 50,
  /** Vegetação: copa de icosaedro (41,8°), tronco e cacto de 6 lados (60°) e cone de conífera redondos. */
  plant: 60,
  /** Pedras e rochedos: lisas, mas o dodecaedro (63,4°) e a quina da laje ainda mostram a lasca. */
  rock: 50,
  /** Marcos turísticos: cúpula, torre redonda, cuia e vidro lisos; caixa viva, telhado vivo pela `ROOF_TURN_DEG`. */
  landmark: 45,
  /** Construções e objetos de pista: poste, caixa-d'água e torre de 10+ lados arredondam; casa e telhado vivos. */
  built: 40,
} as const;

const H1 = 73856093; const H2 = 19349663; const H3 = 83492791;
const cellHash = (x: number, y: number, z: number): number => (Math.imul(x, H1) ^ Math.imul(y, H2) ^ Math.imul(z, H3)) | 0;

/**
 * Recalcula a normal de cada vértice da malha não indexada `geo` com o vinco `creaseDeg` (0 = faces planas,
 * como `computeVertexNormals`). Só aresta de exatamente duas faces pode ser lisa (borda solta e aresta de três
 * ou mais faces ficam vivas). A normal de cada canto segue o sentido da própria face (anti-horário visto de fora);
 * vizinha de enrolamento trocado entra virada na média. Com `roofTurnDeg`, a aresta entre duas faces inclinadas
 * (nem parede nem chão) que viram mais que isso em planta fica viva (a quina do telhado), e também a entre uma face
 * inclinada e uma plana (a água e a cumeeira). Devolve `geo`.
 */
export function smoothNormals(geo: THREE.BufferGeometry, creaseDeg: number, opts: SmoothOptions = {}): THREE.BufferGeometry {
  const eps = opts.eps ?? WELD_EPS;
  if (geo.index) throw new Error('smoothNormals: a malha tem de ser não indexada (a cor é por face)');
  const pos = geo.getAttribute('position');
  const n = pos.count - (pos.count % 3);
  const out = new Float32Array(pos.count * 3);
  const P = new Float64Array(n * 3);
  for (let i = 0; i < n; i++) { P[i * 3] = pos.getX(i); P[i * 3 + 1] = pos.getY(i); P[i * 3 + 2] = pos.getZ(i); }

  // Normal unitária de cada face (0 na degenerada) e ângulo de cada canto (o peso dele na média).
  const faces = n / 3;
  const FN = new Float64Array(faces * 3);
  const ang = new Float64Array(n);
  for (let f = 0; f < faces; f++) {
    const a = f * 9;
    const ux = P[a + 3] - P[a]; const uy = P[a + 4] - P[a + 1]; const uz = P[a + 5] - P[a + 2];
    const vx = P[a + 6] - P[a]; const vy = P[a + 7] - P[a + 1]; const vz = P[a + 8] - P[a + 2];
    const nx = uy * vz - uz * vy; const ny = uz * vx - ux * vz; const nz = ux * vy - uy * vx;
    const len = Math.sqrt(nx * nx + ny * ny + nz * nz);
    if (len < 1e-12) continue;
    FN[f * 3] = nx / len; FN[f * 3 + 1] = ny / len; FN[f * 3 + 2] = nz / len;
    for (let k = 0; k < 3; k++) {
      const c = a + k * 3; const p = a + ((k + 1) % 3) * 3; const q = a + ((k + 2) % 3) * 3;
      const ex = P[p] - P[c]; const ey = P[p + 1] - P[c + 1]; const ez = P[p + 2] - P[c + 2];
      const fx = P[q] - P[c]; const fy = P[q + 1] - P[c + 1]; const fz = P[q + 2] - P[c + 2];
      const d = Math.sqrt((ex * ex + ey * ey + ez * ez) * (fx * fx + fy * fy + fz * fz));
      ang[f * 3 + k] = d > 0 ? Math.acos(Math.max(-1, Math.min(1, (ex * fx + ey * fy + ez * fz) / d))) : 0;
    }
  }

  // Solda: cada canto aponta para um ponto único. Grade com hash, célula de 16 × `eps`: procura na célula do canto e
  // só nas vizinhas de que ele está a menos de `eps` (dois pontos quase iguais podem cair dos dois lados da divisa).
  const cell = eps * 16;
  const inv = 1 / cell;
  const rep = new Int32Array(n);
  const ux: number[] = []; const uy: number[] = []; const uz: number[] = [];
  const next: number[] = [];
  const head = new Map<number, number>();
  const findIn = (h: number, x: number, y: number, z: number): number => {
    for (let u = head.get(h) ?? -1; u >= 0; u = next[u]) {
      if (Math.abs(ux[u] - x) <= eps && Math.abs(uy[u] - y) <= eps && Math.abs(uz[u] - z) <= eps) return u;
    }
    return -1;
  };
  for (let i = 0; i < n; i++) {
    const x = P[i * 3]; const y = P[i * 3 + 1]; const z = P[i * 3 + 2];
    const cx = Math.floor(x * inv); const cy = Math.floor(y * inv); const cz = Math.floor(z * inv);
    const own = cellHash(cx, cy, cz);
    let u = findIn(own, x, y, z);
    if (u < 0) {
      const x0 = x - cx * cell <= eps ? -1 : 0; const x1 = (cx + 1) * cell - x <= eps ? 1 : 0;
      const y0 = y - cy * cell <= eps ? -1 : 0; const y1 = (cy + 1) * cell - y <= eps ? 1 : 0;
      const z0 = z - cz * cell <= eps ? -1 : 0; const z1 = (cz + 1) * cell - z <= eps ? 1 : 0;
      for (let dx = x0; u < 0 && dx <= x1; dx++) {
        for (let dy = y0; u < 0 && dy <= y1; dy++) {
          for (let dz = z0; u < 0 && dz <= z1; dz++) if (dx || dy || dz) u = findIn(cellHash(cx + dx, cy + dy, cz + dz), x, y, z);
        }
      }
    }
    if (u < 0) {
      u = ux.length;
      ux.push(x); uy.push(y); uz.push(z);
      next.push(head.get(own) ?? -1);
      head.set(own, u);
    }
    rep[i] = u;
  }

  // Arestas: cada lado de face (canto k → k+1) pela dupla de pontos soldados. Aresta lisa = exatamente duas faces,
  // dobra menor que o vinco. Se as duas percorrem a aresta no mesmo sentido (enrolamento trocado: o sombreado plano
  // escondia), a vizinha entra virada — a superfície continua lisa e cada face fica com a normal do próprio lado.
  const U = ux.length;
  const cosC = Math.cos((Math.min(180, Math.max(0, creaseDeg)) * Math.PI) / 180) - 1e-9;
  // `mate[i]`: o lado de face que divide a aresta com o lado i (−1 = nenhum ou aresta de 3+ faces).
  const firstOf = new Map<number, number>();
  const mate = new Int32Array(n).fill(-1);
  const crowded = new Uint8Array(n);
  const isFlat = (f: number) => FN[f * 3] === 0 && FN[f * 3 + 1] === 0 && FN[f * 3 + 2] === 0;
  for (let i = 0; i < n; i++) {
    const a = rep[i]; const b = rep[i - (i % 3) + ((i % 3) + 1) % 3];
    // Face degenerada (a ponta colapsada de um `hip`, por exemplo) não conta: senão a aresta "tem 3 faces" e trava.
    if (a === b || isFlat((i / 3) | 0)) continue;
    const key = a < b ? a * U + b : b * U + a;
    const f0 = firstOf.get(key);
    if (f0 === undefined) { firstOf.set(key, i); continue; }
    if (mate[f0] < 0 && !crowded[f0]) { mate[f0] = i; mate[i] = f0; continue; }
    if (mate[f0] >= 0) mate[mate[f0]] = -1;
    mate[f0] = -1; crowded[f0] = 1;
  }
  // Leque: cantos no mesmo ponto ligados por arestas lisas (união com paridade: 1 = a face entra virada).
  const parent = new Int32Array(n); const flip = new Uint8Array(n);
  for (let i = 0; i < n; i++) parent[i] = i;
  let fp = 0;
  const find = (x: number): number => {
    let p = 0; let r = x;
    while (parent[r] !== r) { p ^= flip[r]; r = parent[r]; }
    // Encurta o caminho (com a paridade acumulada até a raiz).
    let y = x; let py = p;
    while (parent[y] !== y) { const nx = parent[y]; const pn = py ^ flip[y]; parent[y] = r; flip[y] = py; y = nx; py = pn; }
    fp = p;
    return r;
  };
  const join = (x: number, y: number, p: number): void => {
    const rx = find(x); const px = fp; const ry = find(y); const py = fp;
    if (rx === ry) return;
    parent[ry] = rx; flip[ry] = px ^ py ^ p;
  };
  const roofCos = opts.roofTurnDeg === undefined ? 2 : Math.cos((opts.roofTurnDeg * Math.PI) / 180);
  const sloped = (y: number) => Math.abs(y) > 0.2 && Math.abs(y) < 0.98; // água: entre ~11° e ~78° de inclinação
  // Cumeeira, laje, tampa: plana de verdade (o topo de uma cúpula é uma roda de triângulos inclinados, não entra).
  const level = (y: number) => Math.abs(y) >= 0.9999;
  for (let e1 = 0; e1 < n; e1++) {
    const e2 = mate[e1];
    if (e2 < e1) continue; // borda solta ou aresta de 3+ faces: viva (e cada par uma vez só)
    const f = (e1 / 3) | 0; const g = (e2 / 3) | 0;
    if (f === g || isFlat(f) || isFlat(g)) continue;
    const same = rep[e1] === rep[e2]; // mesmo sentido = enrolamento trocado
    const s = same ? -1 : 1;
    if (s * (FN[f * 3] * FN[g * 3] + FN[f * 3 + 1] * FN[g * 3 + 1] + FN[f * 3 + 2] * FN[g * 3 + 2]) < cosC) continue;
    if (roofCos < 2) {
      const fy = FN[f * 3 + 1]; const gy = FN[g * 3 + 1];
      // A água não se funde com a tira plana da cumeeira: o leque do canto passaria de uma água à outra por ela.
      if ((sloped(fy) && level(gy)) || (level(fy) && sloped(gy))) continue;
      if (sloped(fy) && sloped(gy)) {
        const hx = FN[f * 3]; const hz = FN[f * 3 + 2]; const gx = s * FN[g * 3]; const gz = s * FN[g * 3 + 2];
        if (hx * gx + hz * gz <= roofCos * Math.sqrt((hx * hx + hz * hz) * (gx * gx + gz * gz))) continue; // quina de telhado
      }
    }
    const e1n = e1 - (e1 % 3) + ((e1 % 3) + 1) % 3; const e2n = e2 - (e2 % 3) + ((e2 % 3) + 1) % 3;
    // Canto de f em a (e1) e em b (e1n); o de g no mesmo ponto depende do sentido.
    join(e1, same ? e2 : e2n, same ? 1 : 0);
    join(e1n, same ? e2n : e2, same ? 1 : 0);
  }
  // Soma por leque (ângulo do canto como peso), no sentido da raiz.
  const sum = new Float64Array(n * 3);
  const sign = new Int8Array(n);
  for (let i = 0; i < n; i++) {
    const f = (i / 3) | 0;
    const r = find(i); const sg = fp ? -1 : 1;
    sign[i] = sg;
    const w = ang[i] * sg;
    sum[r * 3] += FN[f * 3] * w; sum[r * 3 + 1] += FN[f * 3 + 1] * w; sum[r * 3 + 2] += FN[f * 3 + 2] * w;
  }
  for (let i = 0; i < n; i++) {
    const f = (i / 3) | 0;
    if (isFlat(f)) { out[i * 3 + 1] = 1; continue; } // degenerada: não aparece
    const r = find(i);
    const sx = sum[r * 3] * sign[i]; const sy = sum[r * 3 + 1] * sign[i]; const sz = sum[r * 3 + 2] * sign[i];
    const len = Math.sqrt(sx * sx + sy * sy + sz * sz);
    if (len < 1e-12) { out[i * 3] = FN[f * 3]; out[i * 3 + 1] = FN[f * 3 + 1]; out[i * 3 + 2] = FN[f * 3 + 2]; continue; }
    out[i * 3] = sx / len; out[i * 3 + 1] = sy / len; out[i * 3 + 2] = sz / len;
  }
  for (let i = n; i < pos.count; i++) out[i * 3 + 1] = 1;
  geo.setAttribute('normal', new THREE.BufferAttribute(out, 3));
  return geo;
}
