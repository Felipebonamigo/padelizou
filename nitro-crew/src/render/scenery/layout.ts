// Onde cada objeto do cenário fica, calculado UMA vez por pista (não por quadro): os sprites do núcleo
// (posição = a da colisão) e a decoração só visual — forração na faixa onde o carro anda, cerca na
// divisa do alcance do carro (|x| = 3,2 da física), matas com clareiras além dela, postes com fios,
// barcos no mar e pontos de referência por país. Determinístico (hash do id da pista + segmento):
// a mesma pista tem sempre o mesmo visual. Puro: sem Three nem DOM.
import { placeOf } from '../../core/data/places';
import { hashString } from '../../core/rng';
import { landmarkPlazas, PLAZA_SLACK } from '../../core/track/plazas';
import { FIRST_WINDOW, startZoneEnd } from '../../core/track/startzone';
import { SPRITE_HALF_WIDTH } from '../../core/track/sprites';
import type { SpriteRef, Track } from '../../core/types';
import { hash2, hash3, valueNoise } from '../noise';
import { HEADING_PER_CURVE, ROAD_HALF_WIDTH_M, SEGMENT_M, Y_SCALE } from '../units';
import { dressingRecipe, farModelFor, LANDMARK_PREFIX, landmarkOf, modelBandRadius, modelBounds, modelFrontX, modelHeight, spriteVisual, type DressingRecipe } from './catalog';
import { setClearings, type Clearing } from './clearings';
import { groundOffset, isSeaSide, seaLevelOffset } from './ground';
import type { LandmarkDef } from './landmarks/types';
import { LANDMARK_SIGHT_MIN, SIGHT_TAN_H, sightRoad, sightSeconds, type SightRoad } from './sight';

/** |x| máximo do carro na física (sim/physics.ts) em metros, mais a meia largura visual dele. */
export const CAR_REACH_M = 3.2 * ROAD_HALF_WIDTH_M + 0.95;
/** Face da cerca de divisa (m do centro): logo além de onde o carro consegue chegar. */
export const FENCE_M = CAR_REACH_M + 0.25;

export interface Placement {
  /** Índice em `Layout.models`. */
  model: number;
  /** Modelo de longe (−1 = nenhum) e a partir de quantos segmentos à frente ele entra. */
  far: number;
  farFrom: number;
  /** Até quantos segmentos à frente aparece. */
  maxAhead: number;
  /** Fração ao longo do segmento, lateral (m, + = direita) e altura (m) relativa ao ponto de início do segmento. */
  f: number; x: number; y: number;
  /** Giro relativo ao rumo da pista (0: +X do modelo à direita, −Z para a frente) e inclinação (cerca em rampa). */
  yaw: number; pitch: number;
  /** cos e sen de `yaw` (o runtime soma com o rumo da pista sem trigonometria por quadro). */
  yawC: number; yawS: number;
  sx: number; sy: number; sz: number;
  /** Deslocamento local antes do giro (peças acopladas: cata-vento, facho do farol). */
  ox: number; oy: number; oz: number;
  /** Giro contínuo (rad/s) em torno de Z local (rotor) e de Y (facho), com fase inicial. */
  spin: number; spinY: number; phase: number;
  /** Balanço de barco (rad de amplitude). */
  bob: number;
  /** Tom por instância (1 = cor do modelo). */
  r: number; g: number; b: number;
  /** Sprite do núcleo: vai para o lote com sombra de verdade. */
  sprite: boolean;
  /** Fios: segmentos até o próximo poste (0 = não é fio) e o ponto dele. */
  linkSeg: number; linkF: number; linkX: number; linkY: number;
  /**
   * Enfeite dispensável (0 = sempre; forração, mata e soltos têm um sorteio em (0, 1)): com a janela curta das
   * qualidades baixa e média o runtime desenha só os de nota ≤ a fração da janela (menos vista, menos enfeite).
   */
  rank: number;
}

export interface Layout {
  models: string[];
  bySeg: Placement[][];
  /**
   * A pista da conta de enquadramento (sight.ts) com a grade de alturas dos sprites — a mesma com que `placeLandmarks`
   * escolheu o lugar dos marcos —, só se pedida (`sceneryLayout(…, keepSight)`: ~0,7 MB por pista). A legenda dos marcos
   * (render/caption/) mede com ela, sem refazer os sprites. Ausente na pista sem marco.
   */
  sight?: SightRoad;
}

class ModelTable {
  readonly ids: string[] = [];
  private readonly index = new Map<string, number>();
  of(id: string): number {
    let i = this.index.get(id);
    if (i === undefined) { i = this.ids.length; this.ids.push(id); this.index.set(id, i); }
    return i;
  }
}

function base(model: number): Placement {
  return {
    model, far: -1, farFrom: 1e9, maxAhead: 1e9, f: 0.5, x: 0, y: 0, yaw: 0, pitch: 0, yawC: 1, yawS: 0, sx: 1, sy: 1, sz: 1,
    ox: 0, oy: 0, oz: 0, spin: 0, spinY: 0, phase: 0, bob: 0, r: 1, g: 1, b: 1, sprite: false, linkSeg: 0, linkF: 0, linkX: 0, linkY: 0, rank: 0,
  };
}

// ───────────────────────────── Ocupação (para a decoração não entrar nos sprites) ─────────────────────────────

const BIN_M = 2;
const BINS = 90; // 180 m de cada lado

class Occupancy {
  private readonly grid: Uint8Array;
  /** Maior lateral (m) marcada. */
  maxLat = 0;
  constructor(private readonly n: number) { this.grid = new Uint8Array(n * 2 * BINS); }
  private idx(seg: number, side: number, bin: number): number { return ((seg * 2) + (side > 0 ? 1 : 0)) * BINS + bin; }
  /** Marca [lat0, lat1] (m, positivos) de `seg - span` a `seg + span`, com o valor `v` (1..255; fica o maior). */
  mark(seg: number, side: number, lat0: number, lat1: number, span: number, v = 1): void {
    const b0 = Math.max(0, Math.floor(lat0 / BIN_M)); const b1 = Math.min(BINS - 1, Math.floor(lat1 / BIN_M));
    this.maxLat = Math.max(this.maxLat, Math.min(lat1, BINS * BIN_M));
    for (let d = -span; d <= span; d++) {
      const s = ((seg + d) % this.n + this.n) % this.n;
      for (let b = b0; b <= b1; b++) { const k = this.idx(s, side, b); if (this.grid[k] < v) this.grid[k] = v; }
    }
  }
  /** Valor da célula (0 = livre). */
  at(seg: number, side: number, lat: number): number {
    const b = Math.floor(lat / BIN_M);
    return b < BINS ? this.grid[this.idx(seg, side, b)] : 0;
  }
  /** Algo marcado no segmento (qualquer lado)? */
  any(seg: number): boolean {
    const o = seg * 2 * BINS;
    for (let b = 0; b < 2 * BINS; b++) if (this.grid[o + b]) return true;
    return false;
  }
  free(seg: number, side: number, lat0: number, lat1: number, span: number): boolean {
    const b0 = Math.max(0, Math.floor(lat0 / BIN_M)); const b1 = Math.min(BINS - 1, Math.floor(lat1 / BIN_M));
    for (let d = -span; d <= span; d++) {
      const s = ((seg + d) % this.n + this.n) % this.n;
      for (let b = b0; b <= b1; b++) if (this.grid[this.idx(s, side, b)]) return false;
    }
    return true;
  }
}

// ───────────────────────────── Sprites ─────────────────────────────

function tintFor(seed: number, amount: number): [number, number, number] {
  const k = 1 + (hash2(seed, 1) - 0.5) * 2 * amount;
  const warm = (hash2(seed, 2) - 0.5) * amount;
  return [k * (1 + warm * 0.5), k, k * (1 - warm * 0.6)];
}

/**
 * `tall` marca, da pista até a borda de fora, o que esconderia um marco de perto (o corredor dele tem de ficar livre);
 * `block` marca só a pegada dos sólidos largos entre eles (prédio, arquibancada, box, outdoor), com a altura (m): é o que
 * tapa a linha de visada (sight.ts).
 */
function placeSprites(track: Track, table: ModelTable, out: Placement[][], occ: Occupancy, tall: Occupancy, block: Occupancy): void {
  const segs = track.segments;
  const def = track.def;
  for (let i = 0; i < segs.length; i++) {
    const sprites = segs[i].sprites;
    for (let k = 0; k < sprites.length; k++) {
      const sp: SpriteRef = sprites[k];
      const vis = spriteVisual(sp.kind, sp.variant, sp.scale, def);
      const side = sp.x < 0 ? -1 : 1;
      const half = SPRITE_HALF_WIDTH[sp.kind] * sp.scale * ROAD_HALF_WIDTH_M;
      const xm = sp.x * ROAD_HALF_WIDTH_M;
      const seed = i * 31 + k * 7 + 3;
      let x = xm; let yaw = 0; let s = sp.scale; let sy = sp.scale;
      if (vis.mode === 'round') {
        yaw = (hash2(seed, 5) - 0.5) * 2 * vis.yawJitter;
        sy = sp.scale * (1 + (hash2(seed, 6) - 0.5) * 2 * vis.heightJitter);
        // Moita não é sólida: fica baixa (≤ 1,1 m), para o carro passar por ela sem parecer atravessar um muro.
        if (!sp.solid) sy = Math.min(sy, 1.1 / Math.max(0.1, modelHeight(vis.models[0].id)));
      } else if (vis.mode === 'faces' || vis.mode === 'building') {
        // O ponto do modelo mais perto da pista, na altura do carro, vai exatamente na borda de colisão.
        yaw = side > 0 ? Math.PI : 0;
        if (vis.mode === 'building') { s = 1; sy = 1; }
        const edge = Math.abs(xm) - half;
        x = side * (edge + modelFrontX(vis.models[0].id) * s);
      }
      // Torres: a pegada segue a colisão (x, z), a altura cresce menos (um farol de escala 3 não vira um prédio de 60 m).
      if (vis.uniform && vis.mode !== 'building') sy = 1 + (s - 1) * 0.45;
      const lat = Math.abs(x);
      const [r, g, b] = vis.mode === 'round' && vis.heightJitter > 0 ? tintFor(seed, 0.07) : [1, 1, 1];
      // Chão: no centro do objeto (prédio: na fachada — a fundação cobre o declive).
      const gy = groundOffset(track, i, 0.5, side, lat);
      // LOD: vegetação e pedra viram silhueta a partir de ~90 segmentos; o que é pequeno some aos ~110.
      const farId = vis.mode === 'round' ? farModelFor(vis.models[0].id) : null;
      const small = vis.mode === 'round' && !farId && sp.kind !== 'tower';
      for (const m of vis.models) {
        const p = base(table.of(m.id));
        p.x = x; p.y = gy; p.yaw = yaw; p.sx = s; p.sy = sy; p.sz = s; p.sprite = true;
        if (farId && m === vis.models[0]) { p.far = table.of(farId); p.farFrom = 90; }
        if (small) p.maxAhead = 110;
        p.ox = m.dx ?? 0; p.oy = m.dy ?? 0; p.oz = m.dz ?? 0; p.spin = m.spin ?? 0; p.spinY = m.spinY ?? 0;
        p.r = r; p.g = g; p.b = b;
        out[i].push(p);
      }
      // Ocupação visual (m ao longo da pista → segmentos).
      if (sp.kind === 'banner_start') continue;
      const alongHalf = vis.mode === 'building' ? 11 : sp.kind === 'grandstand' ? 7.5 : sp.kind === 'pit_wall' ? 6.5 : vis.mode === 'panel' ? 1 : Math.max(2, half * 1.3);
      const edgeM = Math.abs(xm) - half;
      const lat0 = vis.mode === 'building' ? edgeM : lat - Math.max(half, 1) * 1.3;
      const lat1 = vis.mode === 'building' ? edgeM + 26 : lat + Math.max(half, 1) * 1.3;
      occ.mark(i, side, Math.max(0, lat0), lat1, Math.ceil(alongHalf / SEGMENT_M));
      // O que tapa a vista de um marco (prédio, torre, arquibancada, box, outdoor): grade à parte, da pista para fora.
      if (sp.kind === 'building' || sp.kind === 'tower' || sp.kind === 'grandstand' || sp.kind === 'pit_wall' || sp.kind === 'billboard') tall.mark(i, side, 0, lat1, Math.ceil(alongHalf / SEGMENT_M));
      // O que tapa a linha de visada (sight.ts): a pegada de verdade do modelo — fachada virada para a pista (+X do
      // modelo), painel de frente para quem vem (X na lateral) — com a altura. Torre (mastro e caixa-d'água de treliça,
      // farol de 6 m) é fina: tapa o marco só um instante, fica de fora.
      if (sp.kind === 'building' || sp.kind === 'grandstand' || sp.kind === 'pit_wall' || sp.kind === 'billboard') {
        const mb = modelBounds(vis.models[0].id);
        const in0 = vis.mode === 'panel' ? lat + mb.minX * s : lat - mb.maxX * s;
        const in1 = vis.mode === 'panel' ? lat + mb.maxX * s : lat - mb.minX * s;
        const halfZ = Math.max(-mb.minZ, mb.maxZ) * s;
        const top = mb.maxY * sy;
        block.mark(i, side, Math.max(0, in0), in1, Math.ceil(halfZ / SEGMENT_M), Math.max(1, Math.min(255, Math.ceil(top))));
      }
    }
  }
}

// ───────────────────────────── Marcos turísticos ─────────────────────────────
// Os marcos de places.ts (landmarks/), postos uma vez por pista depois dos sprites e antes da decoração (que
// respeita a ocupação deles). Regras: pegada inteira a ≥ LANDMARK_CLEAR_M do centro de todo trecho à vista
// (o próprio e os vizinhos de grampo); nada alto do lado de dentro de curva próxima; de frente para a pista e
// girado para quem chega; pousado no ponto mais baixo do chão sob a pegada; o primeiro de cada marco logo depois
// da largada e os outros espalhados pela volta. Id sem modelo no registro: ignorado.

type LandmarkPlace = 'near' | 'far' | 'skyline';

/** Nenhuma parte de um marco a menos disto (m) do centro de qualquer trecho de pista à vista. */
export const LANDMARK_CLEAR_M = 26;
/**
 * Faixa lateral (m) e passo da busca, medidos na BORDA DE DENTRO da pegada (o ponto do modelo mais perto da pista):
 * assim um tepui de 1 km de fundo ou uma ponte de 1 km continuam do lado de fora. Sem lugar na faixa, a última
 * passada estende a faixa até o dobro (antes de desistir).
 */
const LANDMARK_LAT: Record<LandmarkPlace, [number, number, number]> = { near: [30, 80, 5], far: [120, 330, 15], skyline: [220, 300, 10] };
/** Quanto (m) a ponta de um modelo comprido pode avançar para a pista por causa do giro `turn` (limita o giro). */
const LANDMARK_TURN_SWEEP_M = 60;
/** Giro padrão (rad) da pista para quem vem chegando (LandmarkDef.turn muda). */
export const LANDMARK_TURN: Record<LandmarkPlace, number> = { near: 0.3, far: 0.45, skyline: 0.35 };
/** O primeiro de cada marco: segmentos depois da largada (o mais importante) e o passo entre um marco e o seguinte. */
const LANDMARK_FIRST = 48;
const LANDMARK_FIRST_STEP = 64;
/**
 * Quanto mais adiante vai o primeiro de cada lugar: o marco fica no segmento em que está AO LADO da pista, e só é
 * visto de frente uns 100–250 segmentos antes — o do horizonte, a 400 m+, entra na tela a ~30° só bem antes disso.
 */
const LANDMARK_FIRST_AHEAD: Record<LandmarkPlace, number> = { near: 0, far: 30, skyline: 150 };
/**
 * Até onde o primeiro de cada marco pode ir: o mais importante, os outros; o do horizonte, + o AHEAD dele. Contado do
 * FIM DA LARGADA (core/track/startzone.ts: arquibancadas, trecho sem cenário e box — segmento 40 nas pistas de hoje):
 * logo depois da linha a vista dos lados é das arquibancadas e das garagens, e a janela existe para o marco ser visto
 * cedo na volta 1, não para ele ficar atrás delas. As praças do núcleo respeitam a mesma janela.
 */
export const LANDMARK_FIRST_MAX = FIRST_WINDOW;
/** Trechos da linha central conferidos (o que está à vista enquanto o marco está na janela do RoadFrame). */
const LANDMARK_VIEW = 300;

/**
 * Dois lugares válidos que ficam na tela quase o mesmo tempo (s): fica o primeiro da busca (o mais perto do alvo, que é
 * o mais perto da largada no primeiro de cada marco).
 */
const SIGHT_TIE = 0.25;
/**
 * Lugar que fica na tela o dobro da meta já basta: a busca para ali (procurar mais só afasta o marco do alvo — e da
 * largada — e custa tempo de montagem).
 */
const SIGHT_PLENTY = 2;

/** Lugar candidato de um marco (o melhor da busca até aqui) e quanto tempo ele fica na tela (`seen`, sight.ts). */
interface LandmarkSpot {
  i: number; k: number; side: number; L: number; latA: number; latB: number; span: number; yaw: number; y: number;
  cx: number; cz: number; ct: number; st: number; corridor: boolean; plaza: boolean; seen: number;
}

/** Pegada de um marco já posto: centro, raio do círculo que o contém e a caixa no referencial dele (ct/st do giro). */
interface LandmarkFoot { k: number; cx: number; cz: number; r: number; ct: number; st: number; b: { minX: number; maxX: number; minZ: number; maxZ: number } }

/** Distância de um ponto do mundo à caixa de uma pegada (0 dentro). */
function distToFoot(f: Pick<LandmarkFoot, 'cx' | 'cz' | 'ct' | 'st' | 'b'>, x: number, z: number): number {
  const dx = x - f.cx; const dz = z - f.cz;
  const lx = dx * f.ct - dz * f.st; const lz = dx * f.st + dz * f.ct;
  return Math.hypot(Math.max(f.b.minX - lx, 0, lx - f.b.maxX), Math.max(f.b.minZ - lz, 0, lz - f.b.maxZ));
}

/** Devolve a pista da conta de enquadramento que usou (`Layout.sight`), ou nada sem marco. */
function placeLandmarks(track: Track, ids: readonly string[], table: ModelTable, out: Placement[][], occ: Occupancy, tall: Occupancy, block: Occupancy): SightRoad | undefined {
  const segs = track.segments; const n = segs.length;
  const entries: Array<{ id: string; def: LandmarkDef }> = [];
  for (const id of ids) {
    const def = landmarkOf(LANDMARK_PREFIX + id);
    if (def) entries.push({ id, def });
  }
  if (entries.length === 0) { setClearings(track, []); return undefined; }
  const biome = track.def.scenery;
  const seed = hashString(track.def.id) & 0xffff;
  // Linha central desenrolada em três voltas (o marco do segmento i é medido na volta do meio, k = i + n) e a conta de
  // enquadramento (sight.ts): quanto tempo cada lugar candidato fica na tela de quem chega.
  const road = sightRoadOf(track, block);
  const { px, pz, hd } = road;
  const placed: LandmarkFoot[] = [];
  const clearings: Clearing[] = [];
  const views: Array<{ i: number; side: number; latA: number; haze: boolean }> = [];
  const plazas = landmarkPlazas(track);
  const zoneEnd = startZoneEnd(track);
  // Fila: primeiro o primeiro de cada marco (na ordem de importância), depois as repetições.
  const jobs: Array<{ id: string; def: LandmarkDef; m: number; target: number; first: boolean; window: number }> = [];
  const firstAt = (e: (typeof entries)[number], j: number): number => track.startIndex + LANDMARK_FIRST + j * LANDMARK_FIRST_STEP + LANDMARK_FIRST_AHEAD[e.def.place];
  // O primeiro procura antes dentro da janela perto da largada (todas as passadas) e só depois fora dela.
  entries.forEach((e, j) => {
    const max = (j === 0 ? LANDMARK_FIRST_MAX.primary : LANDMARK_FIRST_MAX.other) + (e.def.place === 'skyline' ? LANDMARK_FIRST_AHEAD.skyline : 0);
    jobs.push({ ...e, m: 0, target: firstAt(e, j), first: true, window: track.startIndex + zoneEnd + max - firstAt(e, j) });
  });
  entries.forEach((e, j) => {
    const per = Math.max(1, Math.round(e.def.perLap));
    for (let m = 1; m < per; m++) jobs.push({ ...e, m, target: firstAt(e, j) + Math.round((m * n) / per), first: false, window: Infinity });
  });
  for (const job of jobs) {
    const { id, def } = job;
    const modelId = LANDMARK_PREFIX + id;
    const b = modelBounds(modelId);
    const height = b.maxY;
    const place = def.place;
    // Modelo comprido (ponte de 1 km) gira menos: a ponta não pode avançar mais que LANDMARK_TURN_SWEEP_M.
    const halfLen = Math.max(-b.minZ, b.maxZ);
    const turn = Math.min(def.turn ?? LANDMARK_TURN[place], Math.asin(Math.min(1, LANDMARK_TURN_SWEEP_M / Math.max(1, halfLen))));
    const [lat0, lat1, latStep] = LANDMARK_LAT[place];
    const coast = biome === 'coast';
    // 'sea' fica no mar do litoral (direita); fora do litoral, qualquer lado. No litoral, o resto fica em terra.
    const anySides: number[] = def.side === 'sea' ? (coast ? [1] : [-1, 1]) : coast ? [-1] : hash3(seed, job.m, id.length) < 0.5 ? [-1, 1] : [1, -1];
    // Cidade e litoral: o marco de perto/longe vai para a praça (ou o mirante) dele (core/track/plazas.ts — o lado sem
    // prédio na beira da pista); só sem lugar nela cai na busca de sempre (atrás dos prédios, o que o teste das praças
    // acusa).
    const plaza = place === 'skyline' ? undefined : plazas.find((q) => q.landmark === id && q.rep === job.m);
    const per = Math.max(1, Math.round(def.perLap));
    const reach = Math.max(8, Math.min(200, Math.floor(n / (4 * per))));
    const corners: Array<[number, number]> = [[b.minX, b.minZ], [b.minX, b.maxZ], [b.maxX, b.minZ], [b.maxX, b.maxZ]];
    const samples: Array<[number, number]> = [[0, 0], ...corners, [b.minX, 0], [b.maxX, 0]];
    const radius = Math.hypot(Math.max(-b.minX, b.maxX), Math.max(-b.minZ, b.maxZ));
    const goal = LANDMARK_SIGHT_MIN[place];
    let best: LandmarkSpot | null = null;
    const measured = new Map<number, number>();
    // Três passadas: a primeira exige chão quase plano (longe: só na metade de dentro da faixa) e (perto) a vista livre
    // de quem chega; a segunda aceita declive e só pede o corredor livre na frente; a terceira estende a faixa até o dobro.
    // Cada passada mede TODOS os lugares válidos dela (o primeiro lateral válido de cada segmento × lado, como antes) e
    // fica com o que passa mais tempo na tela (sight.ts); quase empate (SIGHT_TIE) fica com o mais perto do alvo. Se o
    // melhor da passada não chega à meta (LANDMARK_SIGHT_MIN), a seguinte também é medida — só a segunda, e na faixa da
    // primeira: o enquadramento troca chão plano e corredor por vista, nunca distância. A terceira só sem lugar nenhum.
    for (let mode = plaza ? 0 : 1; mode < 2 && !best; mode++)
    for (let attempt = 0; attempt < (job.first && mode === 1 ? 2 : 1) && !best; attempt++)
    for (let pass = 0; pass < 3; pass++) {
      if (best && (pass === 2 || best.seen >= goal)) break;
      // Longe: a passada de chão plano fica na metade de dentro da faixa — senão a pegada grande (duna, cânion de
      // 300 m) só achava chão plano a 285–330 m e sumia na névoa; perto com declive (o lado de cima enterra) lê melhor.
      const top = pass === 2 ? lat1 * 2 : place === 'far' && (pass === 0 || best) ? (lat0 + lat1) / 2 : lat1;
      const sides = mode === 0 && plaza ? [plaza.side] : anySides;
      const target = mode === 0 && plaza ? plaza.at : job.target;
      for (let o = 0; o <= (mode === 0 ? 2 * PLAZA_SLACK : 2 * reach) && !(best && best.seen >= SIGHT_PLENTY * goal); o += 4) {
        // 0, +4, −4, +8, −8…; o primeiro de cada marco não volta para antes da largada.
        const off = o === 0 ? 0 : (o % 8 === 4 ? 1 : -1) * Math.ceil(o / 8) * 4;
        if (mode === 1 && job.first && off < -20) continue;
        if (mode === 1 && attempt === 0 && off > job.window) continue;
        const i = (((target + off) % n) + n) % n;
        if (segs[i].pit) continue;
        for (const side of sides) {
          if (height > 8 && place !== 'skyline' && innerCurve(track, i, side)) continue;
          const sea = def.side === 'sea' && isSeaSide(biome, side);
          // Giro relativo ao rumo da pista: +X do modelo para a pista, e `turn` para quem vem chegando (+Z).
          const yaw = side > 0 ? Math.PI + turn : -turn;
          const c = Math.cos(yaw); const s = Math.sin(yaw);
          let inMin = Infinity; let inMax = -Infinity; let alMax = 0;
          for (const [lx, lz] of corners) {
            const lo = side * (c * lx + s * lz);
            inMin = Math.min(inMin, lo); inMax = Math.max(inMax, lo); alMax = Math.max(alMax, Math.abs(s * lx - c * lz));
          }
          const span = Math.ceil(alMax / SEGMENT_M);
          for (let v = lat0; v <= top; v += latStep * (pass === 2 ? 2 : 1)) {
            const L = v - inMin;
            const latA = L + inMin; const latB = L + inMax;
            if (latA < LANDMARK_CLEAR_M) continue;
            // Perto: o corredor entre a cerca e o marco também livre (prédio ou arquibancada na frente o esconderia);
            // a última passada abre mão disso.
            const corridor = place === 'near' && pass < 2;
            if (!occ.free(i, side, corridor ? FENCE_M : latA, latB, span)) continue;
            // …e o caminho de quem chega (os ~120 m antes da pegada) sem sprite entre a cerca e ele, nem nada alto (prédio de
            // Sampa, arquibancada, box, outdoor) da pista até ele: senão só se vê o marco com o carro do lado dele.
            if (corridor && pass === 0 && (!occ.free(((i - span - 16) % n + n) % n, side, FENCE_M, latA, 15) || !tall.free(((i - span - 16) % n + n) % n, side, 0, latA, 15))) continue;
            // Pegada contra a linha central à vista (inclusive trechos vizinhos de grampo e curva em S).
            const k = i + n;
            const cx = (px[k] + px[k + 1]) / 2 + side * L * Math.cos(hd[k]);
            const cz = (pz[k] + pz[k + 1]) / 2 + side * L * Math.sin(hd[k]);
            const th = yaw - hd[k];
            const ct = Math.cos(th); const st = Math.sin(th);
            const far = radius + LANDMARK_CLEAR_M + 2;
            let clear = true;
            for (let q = k - LANDMARK_VIEW; q <= k + LANDMARK_VIEW; q++) {
              const dx = px[q] - cx; const dz = pz[q] - cz;
              if (Math.abs(dx) > far || Math.abs(dz) > far) continue;
              const lx = dx * ct - dz * st; const lz = dx * st + dz * ct;
              const ex = Math.max(b.minX - lx, 0, lx - b.maxX); const ez = Math.max(b.minZ - lz, 0, lz - b.maxZ);
              if (ex * ex + ez * ez < (LANDMARK_CLEAR_M + 0.5) ** 2) { clear = false; break; }
            }
            if (!clear) continue;
            // Outro marco já posto ali (a grade de ocupação só vai até 180 m).
            // O menor (pelo círculo) contra a caixa do maior: o círculo de um tepui de 2 km bloqueava meia volta.
            const me = { cx, cz, ct, st, b };
            if (placed.some((f) => Math.abs(f.k - k) < LANDMARK_VIEW * 2 && (radius <= f.r ? distToFoot(f, cx, cz) < radius : distToFoot(me, f.cx, f.cz) < f.r))) continue;
            // Chão: o ponto mais baixo sob a pegada (nada flutua; o lado de cima do declive enterra).
            let y: number;
            if (sea) y = seaLevelOffset(track, segs[i]);
            else {
              let lo = Infinity; let hi = -Infinity;
              for (const [lx, lz] of samples) {
                const lat = side * L + c * lx + s * lz;
                const fs = i + 0.5 + (s * lx - c * lz) / SEGMENT_M;
                const j = ((Math.floor(fs) % n) + n) % n;
                const g = groundOffset(track, j, fs - Math.floor(fs), lat < 0 ? -1 : 1, Math.abs(lat)) + (segs[j].y0 - segs[i].y0) * Y_SCALE;
                lo = Math.min(lo, g); hi = Math.max(hi, g);
              }
              if (pass === 0 && hi - lo > Math.max(3, height * 0.12)) continue;
              y = lo;
            }
            // Lugar válido: quanto tempo fica na tela de quem chega.
            // O primeiro de cada marco conta a partir da largada (a volta 1); os outros, a aproximação inteira. O mesmo lugar
            // achado de novo na passada seguinte não é medido de novo.
            const key = (i * 2 + (side > 0 ? 1 : 0)) * 4096 + Math.round(L * 4);
            let seen = measured.get(key);
            if (seen === undefined) {
              seen = sightSeconds(road, k, 0.5, side * L, y, yaw, b, place === 'skyline', job.first ? k - ((i - track.startIndex + n) % n) : -Infinity);
              measured.set(key, seen);
            }
            if (!best || seen > best.seen + SIGHT_TIE) best = { i, k, side, L, latA, latB, span, yaw, y, cx, cz, ct, st, corridor, plaza: mode === 0, seen };
            break;
          }
        }
      }
    }
    if (!best) continue;
    const { i, k, side, L, latA, latB, span, yaw, y, cx, cz, ct, st, corridor } = best;
    const p = base(table.of(modelId));
    p.x = side * L; p.f = 0.5; p.y = y; p.yaw = yaw;
    out[i].push(p);
    // Praça: arrumada antes de marcar a ocupação do marco (a arrumação fica do lado de cá da borda de dentro). No mar do
    // litoral, o mirante é a praia com um píer (a cerca-viva e os canteiros da praça ficariam dentro d'água).
    if (best.plaza && isSeaSide(biome, side)) dressBeachMirante(track, table, out, occ, i, side, span);
    else if (best.plaza) dressPlaza(track, table, out, occ, i, side, latA, span, seed);
    occ.mark(i, side, corridor ? FENCE_M + 2.5 : Math.max(0, latA - 2), latB + 2, span + 1);
    // Perto: o caminho de quem chega (~160 m antes) fica sem mata entre a cerca e o marco — um mirante, senão o
    // bosque de 26–142 m o esconde até o carro estar do lado dele.
    if (place === 'near') for (let d = span + 1; d <= span + 40; d += 2) occ.mark(((i - d) % n + n) % n, side, FENCE_M + 2.5, latA, 1);
    if (place !== 'near') views.push({ i, side, latA, haze: place === 'skyline' });
    placed.push({ k, cx, cz, r: radius, ct, st, b });
    // Quadras de fundo da cidade (terreno) fora da pegada e do caminho de quem chega (~200 m antes).
    clearings.push({ seg: i, side, lat: latB + 15, back: span + 50, ahead: span + 4 });
  }
  // Longe e horizonte: a vista de quem chega também sem mata (bosque de 26–142 m, pedras e mesas de longe: a duna de
  // Itaúnas, baixa, sumia atrás dos coqueiros) — só a cunha que as linhas de visada até a borda de dentro varrem, com o
  // carro de `dFar` segmentos antes (névoa, janela do runtime) até `dNear`, onde o marco sai do quadro. Marcada depois
  // de todos os marcos: tira a decoração, não o lugar de outro marco (a cunha do Camboriú empurrava a roda-gigante).
  for (const { i, side, latA, haze } of views) {
    const dNear = latA / (SEGMENT_M * SIGHT_TAN_H);
    const dFar = Math.min(road.ahead, (haze ? road.hazeM : road.fogM) / SEGMENT_M);
    for (let u = 1; u <= dFar; u++) {
      const lo = Math.max(FENCE_M + 2.5, latA * (1 - u / dNear)); const hi = Math.min(latA, latA * (1 - u / dFar));
      if (hi > lo) occ.mark(((i - u) % n + n) % n, side, lo, hi, 0);
    }
  }
  setClearings(track, clearings);
  return road;
}

/**
 * Mirante do litoral do lado do mar (core/track/plazas.ts): a praia aberta, com um píer de madeira (40 m, 1,1 m de
 * guarda-corpo) saindo da areia para o mar no caminho de quem chega — de onde se olharia o marco. Nada alto: o píer fica
 * além do alcance do carro e o deque, 1 m acima d'água. Os guarda-sóis da areia já vêm da decoração (`dress`).
 */
function dressBeachMirante(track: Track, table: ModelTable, out: Placement[][], occ: Occupancy, i: number, side: number, span: number): void {
  const n = track.segments.length;
  const j = ((i - span - 18) % n + n) % n;
  const lat0 = FENCE_M + 0.4;
  if (!occ.free(j, side, lat0, lat0 + 41, 1)) return;
  const p = base(table.of('pier'));
  // +X do modelo é a ponta da areia (o deque vai de 0 a −40 em X): girado de 180° à direita, ele avança para o mar.
  p.x = side * lat0; p.f = 0.5; p.yaw = side > 0 ? Math.PI : 0; p.maxAhead = 160;
  p.y = seaLevelOffset(track, track.segments[j]) + 1;
  out[j].push(p);
  occ.mark(j, side, lat0, lat0 + 41, 1);
}

/**
 * Praça da cidade (core/track/plazas.ts) arrumada como praça, não como buraco no paredão de prédios: cerca-viva baixa
 * logo além do gradil, com uma passagem a cada 10 segmentos, canteiros de flores no gramado (até ~30 m da cerca-viva)
 * e, no marco de perto, uma cerca-viva emoldurando a frente dele. Tudo ≤ 0,9 m (nada tapa o marco). Do caminho de
 * quem chega (~36 segmentos antes da pegada) até logo depois dela. Respeita a ocupação (a pegada do marco, a cerca
 * da divisa e a praça vizinha, que pode se encostar nesta) e a marca, para a decoração de depois não nascer em cima.
 */
function dressPlaza(track: Track, table: ModelTable, out: Placement[][], occ: Occupancy, i: number, side: number, latA: number, span: number, seed: number): void {
  const n = track.segments.length;
  const hedge = 'bush:hedge'; const flowers = 'flowers:meadow';
  const hedgeSy = 0.9 / Math.max(0.1, modelHeight(hedge));
  const put = (seg: number, id: string, lat: number, half: number, opts: Partial<Placement>): void => {
    const j = ((seg % n) + n) % n;
    if (!occ.free(j, side, lat - half, lat + half, 0)) return;
    const p = base(table.of(id));
    Object.assign(p, opts);
    p.x = side * lat; p.y = groundOffset(track, j, p.f, side, lat);
    out[j].push(p);
    occ.mark(j, side, lat - half, lat + half, 0);
  };
  // Cerca-viva corrida ao longo da pista, um passo além do gradil (o comprimento do modelo vai em X: girada de 90° fica
  // paralela à pista).
  const row = FENCE_M + 3.6;
  for (let d = -(span + 36); d <= span + 2; d++) {
    if (((d % 10) + 10) % 10 === 0) continue; // passagem
    put(i + d, hedge, row, 0.9, { f: 0.5, yaw: Math.PI / 2, sx: 1.55, sy: hedgeSy, sz: 1, maxAhead: 120 });
  }
  // Moldura na frente do marco de perto, quando há espaço entre ela e a cerca-viva da beira.
  const front = latA - 3;
  const framed = front > row + 6 && front < 80;
  if (framed) for (let d = -span; d <= span; d++) put(i + d, hedge, front, 0.9, { f: 0.5, yaw: Math.PI / 2, sx: 1.55, sy: hedgeSy * 0.85, sz: 1, maxAhead: 120 });
  // Canteiros de flores no gramado entre as duas (sorteio por hash: determinístico).
  const inner = Math.min(framed ? front - 2 : latA - 2, row + 30);
  if (inner - row < 5) return;
  for (let d = -(span + 36); d <= span + 2; d++) {
    const h = (k: number) => hash3(seed + 9001, i + d, k);
    if (h(1) > 0.55) continue;
    const sc = 1.6 + h(2) * 1.2;
    put(i + d, flowers, row + 2.5 + h(3) * (inner - row - 3), 1, { f: h(4), yaw: h(5) * 6.28, sx: sc, sy: Math.min(sc, 0.5 / Math.max(0.1, modelHeight(flowers))), sz: sc, maxAhead: 80, rank: 0.02 + h(6) * 0.98 });
  }
}

// ───────────────────────────── Decoração ─────────────────────────────

/** Lado de dentro de curva logo à frente (as árvores altas ali esconderiam a pista). */
export function innerCurve(track: Track, i: number, side: number): boolean {
  const segs = track.segments; const n = segs.length;
  let count = 0;
  for (let d = -25; d < 55; d++) {
    const c = segs[((i + d) % n + n) % n].curve;
    if (c * side > 1.2) count++;
  }
  return count >= 18;
}

function pickWeighted<T>(items: Array<[T, number]>, u: number): T {
  let total = 0;
  for (const [, w] of items) total += w;
  let roll = u * total;
  for (const [it, w] of items) { roll -= w; if (roll <= 0) return it; }
  return items[items.length - 1][0];
}

/** Raio (m) que cada item ocupa no chão (tronco + saia), para não nascer um dentro do outro. */
function footprintOf(id: string): number {
  if (id.startsWith('house:') || id === 'pagoda' || id === 'chapel:white' || id === 'chapel:red') return 12;
  if (id.startsWith('mesa:')) return 60;
  if (id === 'turbine' || id === 'windmill' || id.startsWith('tower:')) return 5;
  if (id === 'torii') return 7;
  if (id.startsWith('far:rock')) return 8;
  if (id.startsWith('rock:')) return 2.4;
  if (id.startsWith('tree:') || id.startsWith('palm:') || id.startsWith('pine:')) return 1.8;
  return 1.2;
}

function dress(track: Track, recipe: DressingRecipe, table: ModelTable, out: Placement[][], occ: Occupancy): void {
  const segs = track.segments; const n = segs.length;
  const biome = track.def.scenery;
  const seed = hashString(track.def.id) & 0xffff;
  const startIndex = track.startIndex;
  const poleSide = hash2(seed, 404) < 0.5 ? -1 : 1;
  let lastPole: Placement | null = null; let lastPoleSeg = -1;
  let firstPole: Placement | null = null; let firstPoleSeg = -1;

  const put = (i: number, id: string, x: number, f: number, opts: Partial<Placement> = {}): Placement => {
    const side = x < 0 ? -1 : 1;
    const p = base(table.of(id));
    Object.assign(p, opts);
    // Garantia: nada que gira livre (árvore, pedra, moita solta) encosta na faixa onde o carro anda.
    if (Math.abs(x) >= CAR_REACH_M && p.yaw !== 0 && !id.startsWith('fence:') && !id.startsWith('umbrella:')) {
      const r = modelBandRadius(id) * Math.max(p.sx, p.sz);
      if (Math.abs(x) - r < CAR_REACH_M + 0.1) x = side * (CAR_REACH_M + 0.1 + r);
    }
    p.x = x; p.f = f; p.y = groundOffset(track, i, f, side, Math.abs(x));
    if (opts.y !== undefined) p.y = opts.y;
    out[i].push(p);
    return p;
  };

  for (let i = 0; i < n; i++) {
    const s = segs[i];
    const nearStart = ((i - startIndex + n) % n) < 34 || ((startIndex - i + n) % n) < 6;
    for (const side of [-1, 1] as const) {
      const h = (k: number) => hash3(seed + i, side + 3, k);
      const sea = isSeaSide(biome, side);
      const pitSide = s.pit && side > 0;
      const inner = innerCurve(track, i, side);
      // Forração na faixa em que o carro anda (baixa: passa-se por cima).
      if (!pitSide && !nearStart && recipe.cover.length) {
        const count = Math.floor(recipe.coverDensity + h(1));
        for (let c = 0; c < count; c++) {
          const lat = 9.6 + h(10 + c) * ((sea ? 13 : 13.5));
          if (!occ.free(i, side, lat - 0.5, lat + 0.5, 0)) continue;
          const id = pickWeighted(recipe.cover, h(20 + c));
          const sc = 0.8 + h(30 + c) * 0.6;
          // Baixa de verdade: o carro passa por cima sem parecer atravessar uma moita da altura dele.
          const sy = Math.min(sc * (0.85 + h(60 + c) * 0.3), 1.05 / Math.max(0.1, modelHeight(id)));
          put(i, id, side * lat, h(40 + c), { yaw: h(50 + c) * 6.28, sx: sc, sy, sz: sc, maxAhead: 34, rank: 0.02 + h(45 + c) * 0.98 });
        }
      }
      // Guarda-sóis na areia, na beira d'água (além do alcance do carro).
      if (sea && recipe.umbrellas && !nearStart && h(70) < 0.22) {
        // A espreguiçadeira fica do lado de fora (+X do modelo para longe da pista); a copa passa por cima do carro.
        put(i, `umbrella:${Math.floor(h(71) * 3)}`, side * (FENCE_M + 0.35 + h(72) * 0.4), h(73), { yaw: side > 0 ? 0 : Math.PI, maxAhead: 90 });
      }
      // Cerca na divisa do alcance do carro.
      if (recipe.fence && !sea && !pitSide && !nearStart && occ.free(i, side, FENCE_M - 0.5, FENCE_M + 1, 0)) {
        const y0 = groundOffset(track, i, 0, side, FENCE_M + 0.3);
        const y1 = groundOffset(track, i, 1, side, FENCE_M + 0.3);
        // Comprimento do lance acompanha a curva (do lado de fora o arco é mais longo).
        const len = (SEGMENT_M - side * (FENCE_M + 0.3) * s.curve * HEADING_PER_CURVE) / SEGMENT_M;
        put(i, `fence:${recipe.fence}`, side * (FENCE_M + 0.3), 0.5, { yaw: side > 0 ? Math.PI : 0, pitch: side > 0 ? -Math.atan2(y1 - y0, SEGMENT_M) : Math.atan2(y1 - y0, SEGMENT_M), sz: len, y: (y0 + y1) / 2, maxAhead: 70 });
      }
      if (sea) {
        // Mar: barcos balançando, pedras, colunas de arenito (Austrália), molhe de vez em quando.
        if (recipe.boats.length && h(80) < 0.05) {
          const lat = 45 + h(81) * 140;
          const p = put(i, recipe.boats[Math.floor(h(82) * recipe.boats.length)], side * lat, h(83), { yaw: h(84) * 6.28, bob: 0.03 + h(85) * 0.03 });
          p.y = seaLevelOffset(track, s) + 0.05;
        }
        if (track.def.id === 'great_ocean' && h(86) < 0.025) {
          const p = put(i, `stack:${h(87) < 0.5 ? 0 : 1}`, side * (60 + h(88) * 120), h(89), { yaw: h(90) * 6.28, sx: 0.8 + h(91) * 0.6, sz: 0.8 + h(91) * 0.6 });
          p.y = seaLevelOffset(track, s);
        } else if (h(92) < 0.02) {
          const p = put(i, h(93) < 0.5 ? 'searock:dark' : 'searock:sand', side * (32 + h(94) * 60), h(95), { yaw: h(96) * 6.28, sx: 0.7 + h(97), sy: 0.7 + h(97), sz: 0.7 + h(97) });
          p.y = seaLevelOffset(track, s) - 0.5;
        }
        continue;
      }
      if (pitSide) continue;
      // Postes com fios, de um lado só, a cada 12 segmentos.
      if (recipe.poles && side === poleSide && i % 12 === 0 && !nearStart && occ.free(i, side, 26, 28, 0)) {
        const lat = 27;
        const p = put(i, 'pole', side * lat, 0.5, { maxAhead: 130 });
        if (lastPole && i - lastPoleSeg <= 24) {
          const w = put(lastPoleSeg, 'wires', lastPole.x, lastPole.f, { maxAhead: 60 });
          w.y = lastPole.y; w.linkSeg = i - lastPoleSeg; w.linkF = p.f; w.linkX = p.x; w.linkY = p.y;
        }
        if (!firstPole) { firstPole = p; firstPoleSeg = i; }
        lastPole = p; lastPoleSeg = i;
      }
      if (nearStart) continue;
      // Mata/bosque com clareiras (ruído baixo ao longo da pista) além da cerca.
      const forest = valueNoise(seed + (side > 0 ? 17 : 29), i * 0.022) * 0.5 + 0.5;
      const inForest = forest > recipe.clearing;
      if (recipe.groves.length && inForest) {
        const dens = recipe.groveDensity * Math.min(1, (forest - recipe.clearing) / 0.25 + 0.3);
        const count = Math.floor(dens + h(100));
        for (let c = 0; c < count; c++) {
          const u = h(110 + c);
          const lat = biome === 'city_night' ? 27 + u * 12 : u < 0.55 ? 26.5 + h(120 + c) * 16 : u < 0.88 ? 42 + h(120 + c) * 40 : 82 + h(120 + c) * 60;
          if (inner && lat < 95) continue;
          const r = 1.8;
          if (!occ.free(i, side, lat - r, lat + r, 0)) continue;
          const grove = pickWeighted(recipe.groves.map((g) => [g, g.weight] as [typeof g, number]), h(130 + c));
          const id = grove.near[Math.floor(h(140 + c) * grove.near.length)];
          const sc = 0.85 + h(150 + c) * 0.5;
          const [tr, tg, tb] = tintFor(seed + i * 13 + c, 0.09);
          put(i, id, side * lat, h(160 + c), { yaw: h(170 + c) * 6.28, sx: sc, sy: sc * (0.9 + h(180 + c) * 0.3), sz: sc, far: table.of(grove.far), farFrom: 52, r: tr, g: tg, b: tb, rank: 0.02 + h(190 + c) * 0.98 });
          occ.mark(i, side, lat - 1.2, lat + 1.2, 0);
        }
      }
      // Soltos: pedras, moitas, cupinzeiros.
      if (recipe.scatter.length && h(200) < recipe.scatterDensity * (inForest ? 0.6 : 1.4)) {
        const lat = 26 + h(201) * 50;
        const id = pickWeighted(recipe.scatter, h(202));
        const r = footprintOf(id);
        if (!(inner && lat < 60 && (id.startsWith('cactus:saguaro') || id === 'termite')) && occ.free(i, side, lat - r, lat + r, 0)) {
          const sc = 0.7 + h(203) * 0.6;
          put(i, id, side * lat, h(204), { yaw: h(205) * 6.28, sx: sc, sy: sc, sz: sc, maxAhead: 150, rank: 0.02 + h(206) * 0.98 });
          occ.mark(i, side, lat - r, lat + r, 0);
        }
      }
      // Pontos de referência (raros): ids com espaço próprio; nunca do lado de dentro de curva perto.
      if (recipe.landmarks.length && hash3(seed + 7, i, side + 11) < 1 / recipe.landmarkEvery) {
        const [id, , minLat, maxLat] = pickWeighted(recipe.landmarks.map((l) => [l, l[1]] as [typeof l, number]), h(300));
        const lat = minLat + h(301) * (maxLat - minLat);
        const r = footprintOf(id);
        const span = Math.ceil(r / SEGMENT_M);
        if (!(inner && lat < 120) && occ.free(i, side, lat - r, lat + r, span)) {
          const opts: Partial<Placement> = { yaw: id.startsWith('house:') ? (side > 0 ? Math.PI : 0) + (h(302) - 0.5) * 0.5 : h(302) * 6.28 };
          if (id.startsWith('far:rock') || id.startsWith('mesa:')) { const sc = 0.8 + h(303) * 0.7; Object.assign(opts, { sx: sc, sy: sc * (0.8 + h(304) * 0.4), sz: sc }); }
          const p = put(i, id, side * lat, h(305), opts);
          if (id === 'turbine') {
            // Rotor olhando a pista, girando devagar (fase por turbina).
            const rotor = put(i, 'rotor', p.x, p.f, { yaw: 0, spin: 1.1 + h(306) * 0.5, phase: h(307) * 6.28 });
            rotor.y = p.y; rotor.oy = 63; rotor.oz = 2.6;
            p.yaw = 0;
          }
          occ.mark(i, side, lat - r, lat + r, span);
        }
      }
    }
  }
  // Fecha a linha de postes na volta.
  if (lastPole && firstPole && (firstPoleSeg + n - lastPoleSeg) <= 24) {
    const w = put(lastPoleSeg, 'wires', lastPole.x, lastPole.f, { maxAhead: 60 });
    w.y = lastPole.y; w.linkSeg = firstPoleSeg + n - lastPoleSeg; w.linkF = firstPole.f; w.linkX = firstPole.x; w.linkY = firstPole.y;
  }
}

/**
 * Layout do cenário da pista. Sem cache aqui de propósito: são ~10 mil posições por pista, e guardar as
 * 32 pistas pesaria no heap da sessão inteira — quem desenha (runtime.ts) guarda só o da pista atual.
 * `landmarks`: ids dos marcos turísticos (padrão: os da pista em places.ts; os testes passam outros). `keepSight`: guarda
 * no layout a pista da conta de enquadramento (`Layout.sight`), para a legenda dos marcos (o runtime pede).
 */
export function sceneryLayout(track: Track, landmarks: readonly string[] = placeOf(track.def.id)?.landmarks ?? [], keepSight = false): Layout {
  const table = new ModelTable();
  const bySeg: Placement[][] = track.segments.map(() => []);
  const occ = new Occupancy(track.segments.length);
  const tall = new Occupancy(track.segments.length);
  const block = new Occupancy(track.segments.length);
  placeSprites(track, table, bySeg, occ, tall, block);
  const sight = placeLandmarks(track, landmarks, table, bySeg, occ, tall, block);
  dress(track, dressingRecipe(track.def), table, bySeg, occ);
  for (const list of bySeg) for (const p of list) { p.yawC = Math.cos(p.yaw); p.yawS = Math.sin(p.yaw); }
  return keepSight && sight ? { models: table.ids, bySeg, sight } : { models: table.ids, bySeg };
}

/** A pista pronta para a conta de enquadramento (sight.ts), com a grade do que tapa a vista (prédios, arquibancadas, box, outdoors). */
function sightRoadOf(track: Track, block: Occupancy): SightRoad {
  return sightRoad(track, (seg, side, lat) => block.at(seg, side, lat), block.maxLat, (seg) => block.any(seg));
}

/**
 * A pista pronta para a conta de enquadramento (sight.ts) com a grade do que tapa a vista — a mesma que `placeLandmarks`
 * usa (prédios, arquibancadas, box e outdoors dos sprites), refeita a partir da pista: para medir de novo um layout
 * pronto (aqui e nos testes). Quem tem o layout usa `Layout.sight`, sem refazer os sprites.
 */
export function landmarkSightRoad(track: Track): SightRoad {
  const n = track.segments.length;
  const block = new Occupancy(n);
  placeSprites(track, new ModelTable(), track.segments.map(() => []), new Occupancy(n), new Occupancy(n), block);
  return sightRoadOf(track, block);
}

/** Um marco do layout e quanto tempo ele fica na tela (`landmarkSight`). */
export interface LandmarkSight {
  id: string; seg: number; x: number; place: LandmarkPlace;
  /** O primeiro depois da largada (medido da largada: a volta 1) e se está na praça ou no mirante dele (plazas.ts). */
  first: boolean; plaza: boolean;
  seen: number;
}

/**
 * Quanto tempo (s) cada marco turístico do layout fica à vista da câmera de perseguição (sight.ts) — o número que
 * `placeLandmarks` maximiza, medido de novo a partir das posições prontas (os testes conferem com ele): na aproximação
 * inteira, e o primeiro de cada marco a partir da largada (ele existe para ser visto logo, já na primeira volta).
 */
export function landmarkSight(track: Track, layout: Layout): LandmarkSight[] {
  const n = track.segments.length;
  const road = landmarkSightRoad(track);
  const plazas = landmarkPlazas(track);
  const out: LandmarkSight[] = [];
  const firstSeg = new Map<string, number>();
  layout.bySeg.forEach((list, seg) => {
    for (const p of list) {
      const modelId = layout.models[p.model];
      if (!landmarkOf(modelId)) continue;
      const id = modelId.slice(LANDMARK_PREFIX.length);
      const ahead = (seg - track.startIndex + n) % n;
      if (ahead < (firstSeg.get(id) ?? Infinity)) firstSeg.set(id, ahead);
    }
  });
  layout.bySeg.forEach((list, seg) => {
    for (const p of list) {
      const modelId = layout.models[p.model];
      const def = landmarkOf(modelId);
      if (!def) continue;
      const id = modelId.slice(LANDMARK_PREFIX.length);
      const ahead = (seg - track.startIndex + n) % n;
      const first = ahead === firstSeg.get(id);
      const side = p.x < 0 ? -1 : 1;
      const plaza = def.place !== 'skyline' && plazas.some((q) => q.landmark === id && q.side === side && Math.abs(((seg - q.at + n + n / 2) % n) - n / 2) <= PLAZA_SLACK);
      const seen = sightSeconds(road, seg + n, p.f, p.x, p.y, p.yaw, modelBounds(modelId), def.place === 'skyline', first ? seg + n - ahead : -Infinity);
      out.push({ id, seg, x: p.x, place: def.place, first, plaza, seen });
    }
  });
  return out;
}
