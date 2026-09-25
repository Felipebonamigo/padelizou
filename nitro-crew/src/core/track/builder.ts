// Constrói a pista (lista de segmentos) a partir das operações do TrackDef e decora com o
// cenário. Sem trigonometria: as suavizações são polinomiais, para o resultado ser idêntico em
// qualquer máquina (multiplayer em lockstep compara hashes do estado).
import { SEGMENT_LENGTH } from '../constants';
import { createRng, hashString, nextFloat, nextInt, pick, type RngState } from '../rng';
import type { SceneryId, Segment, SpriteKind, SpriteRef, Track, TrackDef, TrackOp } from '../types';

function easeIn(a: number, b: number, p: number): number { return a + (b - a) * p * p; }
function easeOut(a: number, b: number, p: number): number { const q = 1 - p; return a + (b - a) * (1 - q * q); }
function easeInOut(a: number, b: number, p: number): number { return a + (b - a) * (p * p * (3 - 2 * p)); }

interface Builder { segments: Segment[]; lastY: number }

function addSegment(b: Builder, curve: number, y: number, pit = false): void {
  const index = b.segments.length;
  b.segments.push({
    index, z: index * SEGMENT_LENGTH, curve, y0: b.lastY, y1: y,
    band: (Math.floor(index / 3) % 2) as 0 | 1, pit, sprites: [],
  });
  b.lastY = y;
}

/** Trecho clássico: entrada, sustentação e saída da curva, com variação de altura distribuída. */
function addRoad(b: Builder, enter: number, hold: number, leave: number, curve: number, dy: number): void {
  const startY = b.lastY;
  const endY = startY + dy * SEGMENT_LENGTH;
  const total = enter + hold + leave;
  for (let n = 0; n < enter; n++) addSegment(b, easeIn(0, curve, n / enter), easeInOut(startY, endY, n / total));
  for (let n = 0; n < hold; n++) addSegment(b, curve, easeInOut(startY, endY, (enter + n) / total));
  for (let n = 0; n < leave; n++) addSegment(b, easeOut(curve, 0, n / leave), easeInOut(startY, endY, (enter + hold + n) / total));
}

function applyOp(b: Builder, op: TrackOp): void {
  switch (op.op) {
    case 'straight': addRoad(b, 0, op.length, 0, 0, 0); break;
    case 'curve': {
      const enter = Math.max(1, Math.floor(op.length / 4));
      const leave = enter;
      addRoad(b, enter, Math.max(1, op.length - enter - leave), leave, op.curve, op.hill ?? 0);
      break;
    }
    case 'hill': {
      const half = Math.max(1, Math.floor(op.length / 2));
      addRoad(b, 0, half, 0, 0, op.height);
      addRoad(b, 0, Math.max(1, op.length - half), 0, 0, -op.height);
      break;
    }
    case 's': {
      const half = Math.max(2, Math.floor(op.length / 2));
      const e = Math.max(1, Math.floor(half / 4));
      addRoad(b, e, Math.max(1, half - 2 * e), e, op.curve, 0);
      addRoad(b, e, Math.max(1, op.length - half - 2 * e), e, -op.curve, 0);
      break;
    }
    case 'pit': {
      const start = b.segments.length;
      addRoad(b, 0, op.length, 0, 0, 0);
      for (let i = start; i < b.segments.length; i++) b.segments[i].pit = true;
      break;
    }
  }
}

/** Fecha a elevação: a última altura precisa voltar à primeira para a pista dar a volta. */
function closeElevation(segments: Segment[]): void {
  const n = segments.length;
  if (n === 0) return;
  const drift = segments[n - 1].y1 - segments[0].y0;
  if (drift === 0) return;
  for (let i = 0; i < n; i++) {
    const s = segments[i];
    s.y0 -= drift * (i / n);
    s.y1 -= drift * ((i + 1) / n);
  }
}

// ───────────────────────────── Cenário ─────────────────────────────

interface SceneryRecipe {
  /** Sprites comuns de beira de pista e o peso de cada um. */
  fillers: Array<[SpriteKind, number]>;
  /** Chance por lado, por segmento, de nascer um sprite comum. */
  density: number;
  /** A cada N segmentos nasce um poste/torre (0 = nunca). */
  lampEvery: number;
  /** Chance de outdoor a cada 40 segmentos. */
  billboardChance: number;
  /** Sprites grandes que ficam mais longe da pista (prédios, torres). */
  landmarks: SpriteKind[];
}

const RECIPES: Record<SceneryId, SceneryRecipe> = {
  tropical: { fillers: [['palm', 5], ['tree', 3], ['bush', 4]], density: 0.28, lampEvery: 0, billboardChance: 0.4, landmarks: ['building'] },
  desert: { fillers: [['cactus', 5], ['boulder', 3], ['bush', 2]], density: 0.18, lampEvery: 0, billboardChance: 0.6, landmarks: ['tower'] },
  city_night: { fillers: [['building', 6], ['tower', 2], ['bush', 1]], density: 0.4, lampEvery: 7, billboardChance: 0.9, landmarks: ['tower', 'building'] },
  alpine: { fillers: [['pine', 7], ['boulder', 2], ['tree', 1]], density: 0.36, lampEvery: 0, billboardChance: 0.3, landmarks: ['building'] },
  coast: { fillers: [['palm', 4], ['bush', 3], ['building', 2]], density: 0.26, lampEvery: 11, billboardChance: 0.5, landmarks: ['building', 'tower'] },
  savanna: { fillers: [['tree', 4], ['bush', 4], ['boulder', 2]], density: 0.2, lampEvery: 0, billboardChance: 0.3, landmarks: ['boulder'] },
};

function weightedPick(r: RngState, items: Array<[SpriteKind, number]>): SpriteKind {
  let total = 0;
  for (const [, w] of items) total += w;
  let roll = nextFloat(r) * total;
  for (const [kind, w] of items) { roll -= w; if (roll <= 0) return kind; }
  return items[items.length - 1][0];
}

function sprite(kind: SpriteKind, x: number, scale: number, solid: boolean, variant = 0): SpriteRef {
  return { kind, x, scale, solid, variant };
}

function decorate(track: Track, seed: number): void {
  const r = createRng(seed);
  const recipe = RECIPES[track.def.scenery];
  const segs = track.segments;
  const n = segs.length;

  // Largada: faixa, arquibancadas e muros do box.
  segs[track.startIndex].sprites.push(sprite('banner_start', 0, 1, false));
  for (let i = 1; i <= 24; i += 4) {
    const s = segs[(track.startIndex + i) % n];
    s.sprites.push(sprite('grandstand', -2.4, 1, true, i % 8 === 1 ? 0 : 1));
    if (!s.pit) s.sprites.push(sprite('grandstand', 2.4, 1, true, 1));
  }

  // Trecho de box: muro entre a pista e o box, placa na entrada, cones na saída.
  let pitStart = -1;
  for (let i = 0; i < n; i++) {
    const s = segs[i];
    if (s.pit && pitStart < 0) pitStart = i;
    if (s.pit && i % 3 === 0) s.sprites.push(sprite('pit_wall', 2.3, 1, true));
    if (s.pit && !segs[(i + 1) % n].pit) {
      s.sprites.push(sprite('cone', 1.25, 1, false));
    }
  }
  if (pitStart >= 0) segs[Math.max(0, pitStart - 6)].sprites.push(sprite('pit_sign', 1.6, 1, false));

  // Placas de curva do lado de fora, no começo de cada curva forte.
  for (let i = 1; i < n; i++) {
    const s = segs[i];
    const prev = segs[i - 1];
    if (Math.abs(s.curve) >= 3 && Math.abs(prev.curve) < 3) {
      const outer = s.curve > 0 ? -1.5 : 1.5;
      for (let k = 0; k < 3; k++) {
        segs[(i + k * 4) % n].sprites.push(sprite(s.curve > 0 ? 'sign_right' : 'sign_left', outer, 1, true));
      }
    }
  }

  // Preenchimento por lado.
  for (let i = 0; i < n; i++) {
    const s = segs[i];
    const nearStart = ((i - track.startIndex + n) % n) < 30;
    for (const side of [-1, 1] as const) {
      if (side === 1 && s.pit) continue;
      if (nearStart) continue;
      if (nextFloat(r) < recipe.density) {
        const kind = weightedPick(r, recipe.fillers);
        const far = kind === 'building' || kind === 'tower';
        const x = side * (far ? 2.2 + nextFloat(r) * 1.4 : 1.35 + nextFloat(r) * 1.6);
        const scale = far ? 1.2 + nextFloat(r) * 1.4 : 0.8 + nextFloat(r) * 0.6;
        s.sprites.push(sprite(kind, x, scale, kind !== 'bush', nextInt(r, 0, 3)));
      }
      if (recipe.lampEvery > 0 && i % recipe.lampEvery === 0 && side === -1) {
        s.sprites.push(sprite('lamp', -1.3, 1, true, 0));
      }
    }
    if (i % 40 === 20 && nextFloat(r) < recipe.billboardChance) {
      const side = nextFloat(r) < 0.5 ? -1 : 1;
      if (!(side === 1 && s.pit)) s.sprites.push(sprite('billboard', side * 1.7, 1, true, nextInt(r, 0, 7)));
    }
    if (i % 90 === 45 && recipe.landmarks.length > 0) {
      const side = nextFloat(r) < 0.5 ? -1 : 1;
      s.sprites.push(sprite(pick(r, recipe.landmarks), side * 3.2, 2.2 + nextFloat(r), true, nextInt(r, 0, 3)));
    }
  }
}

/** Monta a pista a partir da definição. Determinístico: mesma definição → mesma pista. */
export function buildTrack(def: TrackDef): Track {
  const b: Builder = { segments: [], lastY: 0 };
  for (const op of def.ops) applyOp(b, op);
  // Box automático na reta de largada quando a pista não define um.
  if (!b.segments.some((s) => s.pit)) {
    for (let i = 4; i < Math.min(40, b.segments.length); i++) b.segments[i].pit = true;
  }
  closeElevation(b.segments);
  const track: Track = { def, segments: b.segments, length: b.segments.length * SEGMENT_LENGTH, startIndex: 0 };
  decorate(track, hashString(def.id));
  return track;
}

/** Segmento que contém a posição z (com volta). */
export function segmentAt(track: Track, z: number): Segment {
  const len = track.length;
  let zz = z % len;
  if (zz < 0) zz += len;
  return track.segments[Math.floor(zz / SEGMENT_LENGTH) % track.segments.length];
}

/** Maior curvatura absoluta nos próximos `count` segmentos a partir de z. */
export function maxCurveAhead(track: Track, z: number, count: number): number {
  const start = Math.floor(((z % track.length) + track.length) % track.length / SEGMENT_LENGTH);
  let max = 0;
  const n = track.segments.length;
  for (let i = 0; i < count; i++) {
    const c = Math.abs(track.segments[(start + i) % n].curve);
    if (c > max) max = c;
  }
  return max;
}
