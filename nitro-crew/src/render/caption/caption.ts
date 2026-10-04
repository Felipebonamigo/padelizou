// Legenda dos marcos turísticos (docs/VISUAL.md, "Legenda dos marcos"): o dono correu em Foz do Iguaçu e "não achou as
// referências das cataratas". Quando um marco entra BEM À VISTA da câmera de um jogador, o HUD daquele viewport mostra
// o nome dele e onde fica ("Cataratas do Iguaçu" / "Foz do Iguaçu · PR") por CAPTION_SECONDS — uma vez por marco por
// volta (a primeira instância que aparecer) e uma legenda de cada vez.
//
// "Bem à vista" é a conta do enquadramento (scenery/sight.ts, a que o layout usa para escolher o lugar do marco), feita
// com a câmera DAQUELE jogador — onde o carro está, a lateral dele, o FOV do momento e a proporção do viewport (2
// jogadores: 32:9) — num instante só: um dos 7 pontos de amostra do marco nos 80% do meio da largura do quadro, à frente,
// sem prédio, arquibancada, box ou outdoor na linha de visada e numa névoa mais rala que a do sight.ts (CAPTION_FOG:
// 35% em vez de 60%); e o marco grande o bastante na tela (CAPTION_MIN_SHARE: o pedaço da caixa dele que está no
// quadro). A conta roda a 1/CAPTION_CHECK Hz por viewport (CaptionDirector), sobre tabelas montadas uma vez por pista
// (`captionScene`): nada alocado por quadro. Puro: sem Three nem DOM (o HUD desenha, hud.ts).
import { SEGMENT_LENGTH } from '../../core/constants';
import type { Track } from '../../core/types';
import { CHASE_CAMERA } from '../camera';
import { LANDMARK_PREFIX, landmarkOf, modelBounds } from '../scenery/catalog';
import type { Layout } from '../scenery/layout';
import { blocked, roadY, SCENERY_BEHIND_DRAW, SIGHT_ASPECT, SIGHT_FOG, SIGHT_FRAME_X, SIGHT_SPEED_FRAC, type SightBounds, type SightRoad } from '../scenery/sight';
import { ROAD_HALF_WIDTH_M, SEGMENT_M } from '../units';

/** Quanto tempo (s) a legenda fica na tela (da entrada ao começo da saída). */
export const CAPTION_SECONDS = 3;
/**
 * Com outro marco esperando a vez (à vista, grande o bastante, ainda não anunciado), a legenda sai depois deste mínimo
 * (s) em vez dos 3: o segundo marco de muitas pistas fica 64 segmentos depois do primeiro (layout.ts) e, com 3 s cheios
 * para o primeiro, só tinha a vez já saindo do quadro.
 */
export const CAPTION_MIN_SECONDS = 2;
/** Intervalo (s) entre o começo da saída de uma legenda e a entrada da seguinte: a saída (CSS) termina antes. */
export const CAPTION_GAP = 0.5;
/** De quanto em quanto tempo (s) a visibilidade é conferida: 5 Hz. */
export const CAPTION_CHECK = 0.2;
/**
 * Tamanho mínimo do marco na tela para anunciar: o lado maior do retângulo do pedaço dele no quadro, em fração da altura
 * (ou da largura) do viewport — 5% são 36 px de altura em 720p. Os grandes (pontes, montanhas, catedrais) passam disto
 * de longe, e quem decide é a névoa da legenda (CAPTION_FOG); os pequenos de perto (placa da Rota 66, shisas, girafas:
 * 3–5 m) só perto: com 8%, entravam já saindo do quadro (0,1–0,3 s antes). Nas 109 pistas (scratch de calibragem e
 * tests/landmark-caption.test.ts), todo marco é anunciado na volta 1.
 */
export const CAPTION_MIN_SHARE = 0.05;
/**
 * Névoa máxima (FogExp2: 1 − e^−(ρd)²) com que o marco é anunciado. O sight.ts conta o marco à vista até 60% — de dia,
 * ~500 m —, e anunciar ali era cedo demais: as cataratas de Foz, a 540 m, eram um vulto claro atrás dos coqueiros com
 * a legenda já na tela. Com 35% (~345 m de dia, ~270 m à noite; o horizonte, ~630 m) ele entra nítido.
 */
export const CAPTION_FOG = 0.35;
/** A distância da névoa da legenda em relação à do sight.ts (as duas na mesma conta do FogExp2). */
export const CAPTION_FOG_SCALE = Math.sqrt(Math.log(1 - CAPTION_FOG) / Math.log(1 - SIGHT_FOG));

/** FOV vertical (°) da câmera de referência do sight.ts: 0,8 da velocidade máxima. */
export const REFERENCE_FOV = CHASE_CAMERA.baseFov + CHASE_CAMERA.speedFov * Math.min(1.2, SIGHT_SPEED_FRAC);

/** Uma instância de marco no layout da pista (`Placement` de um modelo "lm:<id>"). */
export interface CaptionSpot {
  readonly id: string;
  /** Segmento, fração dentro dele, lateral (m, + = direita), altura da base (m) e giro relativo à pista — os do layout. */
  readonly seg: number; readonly f: number; readonly x: number; readonly y: number; readonly yaw: number;
  readonly bounds: SightBounds;
  /** Do horizonte: a névoa mais rala do lote `haze`. */
  readonly haze: boolean;
}

/** Os marcos de um layout pronto (o mesmo que o cenário desenha), na ordem dos segmentos. */
export function captionSpots(layout: Layout): CaptionSpot[] {
  const out: CaptionSpot[] = [];
  layout.bySeg.forEach((list, seg) => {
    for (const p of list) {
      const modelId = layout.models[p.model];
      const def = landmarkOf(modelId);
      if (!def) continue;
      out.push({ id: modelId.slice(LANDMARK_PREFIX.length), seg, f: p.f, x: p.x, y: p.y, yaw: p.yaw, bounds: modelBounds(modelId), haze: def.place === 'skyline' });
    }
  });
  return out;
}

/** A pista pronta para a legenda: montada uma vez por pista, lida a cada conferência. */
export interface CaptionScene {
  readonly trackId: string;
  /** Segmentos da pista. */
  readonly n: number;
  readonly road: SightRoad;
  readonly spots: readonly CaptionSpot[];
  /** Ids distintos dos marcos e o índice do id de cada instância (o "já anunciado nesta volta" é por id). */
  readonly ids: readonly string[];
  readonly spotId: Int32Array;
  /** Os 7 pontos de amostra do sight.ts por instância (x, y, z na volta do meio da linha desenrolada). */
  readonly samples: Float64Array;
  /** Os 8 cantos da caixa (do chão ao topo) por instância (x, y, z). */
  readonly corners: Float64Array;
  /** Último segmento (desenrolado) em que a visada é conferida contra a grade de alturas, por instância. */
  readonly reachEnd: Int32Array;
  /**
   * Até onde (posição do carro na volta, em segmentos) a câmera de referência ainda vê cada instância: a ordem da vez
   * (`pickCaption`: o que sai do quadro primeiro vem primeiro), medida uma vez — a ponte comprida fica até o fim dela.
   */
  readonly lastSeen: Float64Array;
}

const SAMPLES = 7;
const CORNERS = 8;

/**
 * Monta as tabelas da legenda: as instâncias, os ids e os pontos de cada uma no referencial da linha desenrolada de
 * `road` (`Layout.sight`, ou `landmarkSightRoad` a partir da pista) — a mesma conta de `sightSeconds`: centro e giro como
 * em runtime.place.
 */
export function captionScene(track: Track, spots: readonly CaptionSpot[], road: SightRoad): CaptionScene {
  const n = track.segments.length;
  const ids: string[] = [];
  const spotId = new Int32Array(spots.length);
  const samples = new Float64Array(spots.length * SAMPLES * 3);
  const corners = new Float64Array(spots.length * CORNERS * 3);
  const reachEnd = new Int32Array(spots.length);
  const { px, pz, hd, py } = road;
  spots.forEach((sp, i) => {
    let id = ids.indexOf(sp.id);
    if (id < 0) { id = ids.length; ids.push(sp.id); }
    spotId[i] = id;
    const k = sp.seg + n; const b = sp.bounds; const x = sp.x; const f = sp.f;
    const ax = px[k] + x * Math.cos(hd[k]); const az = pz[k] + x * Math.sin(hd[k]);
    const X = ax + (px[k + 1] + x * Math.cos(hd[k + 1]) - ax) * f;
    const Z = az + (pz[k + 1] + x * Math.sin(hd[k + 1]) - az) * f;
    const Y = py[k] + sp.y;
    const th = sp.yaw - (hd[k] + (hd[k + 1] - hd[k]) * f);
    const ct = Math.cos(th); const st = Math.sin(th);
    const H = b.maxY;
    const mx = (b.minX + b.maxX) / 2; const mz = (b.minZ + b.maxZ) / 2;
    const fx = (mx + b.maxX) / 2; const zAhead = (mz + b.minZ) / 2; const zBack = (mz + b.maxZ) / 2;
    // Os pontos de amostra do sight.ts (o centro a 20/50/85% da altura; a meio caminho das faces que quem chega vê).
    const local: ReadonlyArray<readonly [number, number, number]> = [[mx, 0.2 * H, mz], [mx, 0.5 * H, mz], [mx, 0.85 * H, mz], [fx, 0.4 * H, mz], [fx, 0.4 * H, zAhead], [fx, 0.4 * H, zBack], [mx, 0.4 * H, zBack]];
    local.forEach(([lx, ly, lz], m) => {
      const o = (i * SAMPLES + m) * 3;
      samples[o] = X + ct * lx + st * lz; samples[o + 1] = Y + ly; samples[o + 2] = Z - st * lx + ct * lz;
    });
    for (let c = 0; c < CORNERS; c++) {
      const lx = c & 1 ? b.maxX : b.minX; const ly = c & 2 ? H : 0; const lz = c & 4 ? b.maxZ : b.minZ;
      const o = (i * CORNERS + c) * 3;
      corners[o] = X + ct * lx + st * lz; corners[o + 1] = Y + ly; corners[o + 2] = Z - st * lx + ct * lz;
    }
    reachEnd[i] = Math.ceil(k + f) + Math.ceil(Math.max(-b.minX, b.maxX, -b.minZ, b.maxZ) / SEGMENT_M) + 2;
  });
  const lastSeen = new Float64Array(spots.length);
  const scene: CaptionScene = { trackId: track.def.id, n, road, spots, ids, spotId, samples, corners, reachEnd, lastSeen };
  // De onde o carro já passou do marco para trás, até a câmera de referência vê-lo: meio segmento de passo.
  const view = referenceView(0);
  spots.forEach((sp, i) => {
    lastSeen[i] = sp.seg;
    for (let s = sp.seg + SCENERY_BEHIND_DRAW + 1; s >= sp.seg - road.ahead; s -= 0.5) {
      view.z = ((s % n) + n) % n * SEGMENT_LENGTH;
      if (spotShare(scene, i, view) > 0) { lastSeen[i] = s; break; }
    }
  });
  return scene;
}

/** A câmera de um jogador, no que importa para a conta. */
export interface CaptionView {
  /** Posição do carro ao longo da pista (unidades de mundo, `CarState.z`) e a lateral normalizada (`CarState.x`). */
  z: number;
  x: number;
  /** FOV vertical (°) da câmera de perseguição agora (abre com a velocidade e o nitro) e a proporção do viewport. */
  fov: number;
  aspect: number;
  /** Segmentos à frente em que o renderizador desenha o cenário na qualidade atual (FRAME_AHEAD): além disso não há marco. */
  ahead: number;
}

/** A câmera de referência do sight.ts (centro da pista, 0,8 da máxima, 16:9) com o carro em `z`. */
export function referenceView(z: number): CaptionView {
  return { z, x: 0, fov: REFERENCE_FOV, aspect: SIGHT_ASPECT, ahead: Infinity };
}

/**
 * Quanto do viewport o marco `i` ocupa (0 = não está à vista): o lado maior do retângulo do pedaço da caixa dele que
 * está no quadro, em fração da altura (vertical) ou da largura (horizontal).
 * Só conta se um dos pontos de amostra está à vista pela regra do sight.ts (`fogScale` < 1 encurta a névoa: a da
 * legenda é CAPTION_FOG_SCALE). A câmera é a de `camera.ts` posta como a do sight.ts (no carro, `CHASE_CAMERA.back`
 * atrás no rumo dele, olhando `lookAhead` à frente), deslocada para a lateral do carro — a câmera do jogo segue o x do
 * carro.
 */
export function spotShare(scene: CaptionScene, i: number, view: CaptionView, fogScale = 1): number {
  const { road, n } = scene;
  const sp = scene.spots[i];
  // Segmentos do carro até o marco pelo caminho mais curto da volta; a janela do runtime: até `ahead` à frente e
  // SCENERY_BEHIND_DRAW atrás (o mesmo laço do sightSeconds).
  let d = (sp.seg - view.z / SEGMENT_LENGTH) % n;
  if (d < 0) d += n;
  if (d > n / 2) d -= n;
  if (d > Math.min(road.ahead, view.ahead) || d <= -(SCENERY_BEHIND_DRAW + 1)) return 0;
  // A câmera na volta do meio da linha desenrolada, como o sight.ts.
  const s = sp.seg + n - d;
  const { px, pz, hd, py } = road;
  const j = Math.floor(s); const u = s - j;
  const h = hd[j] + (hd[j + 1] - hd[j]) * u;
  const sh = Math.sin(h); const ch = Math.cos(h);
  const xm = view.x * ROAD_HALF_WIDTH_M;
  const cx = px[j] + (px[j + 1] - px[j]) * u + xm * ch;
  const cz = pz[j] + (pz[j + 1] - pz[j]) * u + xm * sh;
  const ry = roadY(py, s);
  const backS = CHASE_CAMERA.back / SEGMENT_M; const lookS = CHASE_CAMERA.lookAhead / SEGMENT_M;
  const camY = roadY(py, s - backS) + CHASE_CAMERA.height;
  const lookY = ry + CHASE_CAMERA.lookY + Math.max(-4, Math.min(4, (roadY(py, s + lookS) - ry) * 0.6));
  const camX = cx - CHASE_CAMERA.back * sh; const camZ = cz + CHASE_CAMERA.back * ch;
  const lookDist = CHASE_CAMERA.back + CHASE_CAMERA.lookAhead;
  const dyLook = lookY - camY;
  const inv = 1 / Math.hypot(lookDist, dyLook);
  const Dx = sh * lookDist * inv; const Dy = dyLook * inv; const Dz = -ch * lookDist * inv;
  const Rx = ch; const Rz = sh;
  const Ux = -Rz * Dy; const Uy = Rz * Dx - Rx * Dz; const Uz = Rx * Dy;
  const tanV = Math.tan((view.fov * Math.PI) / 360);
  const tanW = tanV * view.aspect;
  const tanH = tanW * SIGHT_FRAME_X;
  const fogM = (sp.haze ? road.hazeM : road.fogM) * fogScale;
  const fog2 = fogM * fogM;
  const camSeg = Math.floor(s - backS);
  const S = scene.samples;
  let seen = false;
  for (let m = 0; m < SAMPLES && !seen; m++) {
    const o = (i * SAMPLES + m) * 3;
    const dx = S[o] - camX; const dy = S[o + 1] - camY; const dz = S[o + 2] - camZ;
    const zc = dx * Dx + dy * Dy + dz * Dz;
    if (zc < 0.3 || zc * zc > fog2) continue;
    if (Math.abs(dx * Rx + dz * Rz) > zc * tanH) continue;
    if (Math.abs(dx * Ux + dy * Uy + dz * Uz) > zc * tanV) continue;
    if (blocked(road, camX, camY, camZ, S[o], S[o + 1], S[o + 2], camSeg, scene.reachEnd[i])) continue;
    seen = true;
  }
  if (!seen) return 0;
  // Tamanho na tela: o retângulo do pedaço da caixa que está no quadro — as 12 arestas cortadas pela pirâmide da câmera
  // (perto, lados, cima e baixo), projetadas em coordenadas normalizadas (−1..1). Os cantos atrás da câmera não entram,
  // mas a aresta que vem deles sim: a ponte de 720 m passando do lado conta pelo vão no quadro, não pelas pontas.
  const C = scene.corners;
  for (let c = 0; c < CORNERS; c++) {
    const o = (i * CORNERS + c) * 3;
    const dx = C[o] - camX; const dy = C[o + 1] - camY; const dz = C[o + 2] - camZ;
    CAM[c * 3] = dx * Rx + dz * Rz; CAM[c * 3 + 1] = dx * Ux + dy * Uy + dz * Uz; CAM[c * 3 + 2] = dx * Dx + dy * Dy + dz * Dz;
  }
  RECT[0] = Infinity; RECT[1] = -Infinity; RECT[2] = Infinity; RECT[3] = -Infinity;
  for (let e = 0; e < EDGES.length; e += 2) clipEdge(EDGES[e] * 3, EDGES[e + 1] * 3, tanW, tanV);
  // Nenhuma aresta no quadro com um ponto do marco nele: a caixa envolve a câmera (montanha enchendo a tela).
  if (RECT[0] > RECT[1]) return 1;
  // Visível já foi provado pelos pontos de amostra: o tamanho nunca zera aqui.
  return Math.max(1e-6, (RECT[1] - RECT[0]) / 2, (RECT[3] - RECT[2]) / 2);
}

/** Cantos da caixa no referencial da câmera (x à direita, y para cima, z à frente) e o retângulo na tela: x0, x1, y0, y1. */
const CAM = new Float64Array(CORNERS * 3);
const RECT = new Float64Array(4);
/** As 12 arestas da caixa (pares de cantos que diferem num eixo: bit 1 = x, 2 = y, 4 = z). */
const EDGES: readonly number[] = [0, 1, 2, 3, 4, 5, 6, 7, 0, 2, 1, 3, 4, 6, 5, 7, 0, 4, 1, 5, 2, 6, 3, 7];
const NEAR = 0.3;

/**
 * Corta a aresta entre dois cantos (índices em CAM) pela pirâmide da câmera (Liang–Barsky: cada plano é uma desigualdade
 * linear f ≥ 0) e soma as pontas que sobram, projetadas, ao retângulo.
 */
function clipEdge(a: number, b: number, tanW: number, tanV: number): void {
  const ax = CAM[a]; const ay = CAM[a + 1]; const az = CAM[a + 2];
  const bx = CAM[b]; const by = CAM[b + 1]; const bz = CAM[b + 2];
  let t0 = 0; let t1 = 1;
  for (let p = 0; p < 5 && t0 <= t1; p++) {
    // perto: z − NEAR; direita/esquerda: z·tanW ∓ x; cima/baixo: z·tanV ∓ y.
    const fa = p === 0 ? az - NEAR : p === 1 ? az * tanW - ax : p === 2 ? az * tanW + ax : p === 3 ? az * tanV - ay : az * tanV + ay;
    const fb = p === 0 ? bz - NEAR : p === 1 ? bz * tanW - bx : p === 2 ? bz * tanW + bx : p === 3 ? bz * tanV - by : bz * tanV + by;
    if (fa < 0 && fb < 0) return;
    if (fa < 0) t0 = Math.max(t0, fa / (fa - fb));
    else if (fb < 0) t1 = Math.min(t1, fa / (fa - fb));
  }
  if (t0 > t1) return;
  for (let k = 0; k < 2; k++) {
    const tt = k === 0 ? t0 : t1;
    const x = ax + (bx - ax) * tt; const y = ay + (by - ay) * tt; const z = az + (bz - az) * tt;
    const nx = Math.max(-1, Math.min(1, x / (z * tanW))); const ny = Math.max(-1, Math.min(1, y / (z * tanV)));
    if (nx < RECT[0]) RECT[0] = nx; if (nx > RECT[1]) RECT[1] = nx;
    if (ny < RECT[2]) RECT[2] = ny; if (ny > RECT[3]) RECT[3] = ny;
  }
}

/** Quando anunciar: o tamanho mínimo na tela e a névoa (fração da distância do sight.ts). */
export interface CaptionRule { minShare: number; fogScale: number }
export const CAPTION_RULE: Readonly<CaptionRule> = { minShare: CAPTION_MIN_SHARE, fogScale: CAPTION_FOG_SCALE };

/** Quantos metros o carro anda até a câmera de referência perder o marco `i` de vista (`lastSeen`). Negativo: já perdeu. */
function exitMeters(scene: CaptionScene, i: number, view: CaptionView): number {
  let d = (scene.lastSeen[i] - view.z / SEGMENT_LENGTH) % scene.n;
  if (d < 0) d += scene.n;
  if (d > scene.n / 2) d -= scene.n;
  return d * SEGMENT_M;
}

/**
 * O marco a anunciar agora (índice em `scene.spots`, −1 = nenhum): entre os que estão à vista na névoa da legenda,
 * grandes o bastante e ainda não anunciados nesta volta (`announced[id] === lap`), o que sai do quadro primeiro (o de
 * perto antes do grande de longe, que continua na tela e tem a vez depois); no empate, o maior na tela. Função pura:
 * quem guarda o "já anunciado" e a vez de cada legenda é o `CaptionDirector`.
 */
export function pickCaption(scene: CaptionScene, view: CaptionView, announced: Int32Array, lap: number, rule: Readonly<CaptionRule> = CAPTION_RULE): number {
  let best = -1; let bestExit = Infinity; let bestShare = 0;
  for (let i = 0; i < scene.spots.length; i++) {
    if (announced[scene.spotId[i]] === lap) continue;
    const share = spotShare(scene, i, view, rule.fogScale);
    if (share < rule.minShare) continue;
    const exit = exitMeters(scene, i, view);
    if (exit < bestExit - 0.5 || (exit <= bestExit + 0.5 && share > bestShare)) { best = i; bestExit = exit; bestShare = share; }
  }
  return best;
}

const IDLE = 0; const SHOW = 1; const GAP = 2;

/**
 * A legenda de um viewport: confere a visibilidade a cada CAPTION_CHECK s, mostra um marco por CAPTION_SECONDS (ou
 * CAPTION_MIN_SECONDS, se outro já espera a vez), espera CAPTION_GAP e só então mostra o seguinte (não empilha: dois
 * marcos juntos saem um depois do outro, se o segundo ainda estiver à vista). O relógio é o da corrida (ticks): parado
 * na pausa; voltou para trás = corrida nova. Nada alocado depois do primeiro `update` de cada pista.
 */
export class CaptionDirector {
  /** O marco da legenda atual (continua depois que ela sai, para o texto não sumir durante a animação de saída). */
  landmark: string | null = null;
  /** A legenda está na tela (o HUD anima a entrada e a saída). */
  showing = false;
  private scene: CaptionScene | null = null;
  private announced = new Int32Array(0);
  private phase = IDLE;
  private since = 0;
  private lastCheck = -Infinity;
  private lastNow = -Infinity;

  constructor(private readonly rule: Readonly<CaptionRule> = CAPTION_RULE) {}

  /**
   * `lap`: a volta do carro (1 = primeira); `now`: segundos da corrida; `enabled`: falso na contagem, depois da chegada
   * ou com a opção desligada — some a que estiver na tela e não anuncia nada.
   */
  update(scene: CaptionScene | null, view: CaptionView, lap: number, now: number, enabled: boolean): void {
    if (scene !== this.scene || now < this.lastNow) this.reset(scene);
    this.lastNow = now;
    if (!scene) return;
    if (this.phase === SHOW) {
      const shown = now - this.since;
      // Outro marco esperando a vez encurta esta para o mínimo (conferido no mesmo ritmo da procura).
      const waiting = enabled && shown >= CAPTION_MIN_SECONDS && shown < CAPTION_SECONDS && now - this.lastCheck >= CAPTION_CHECK
        && this.check(scene, view, lap, now) >= 0;
      if (!enabled || waiting || shown >= CAPTION_SECONDS) { this.phase = GAP; this.since = now; this.showing = false; }
    }
    if (this.phase === GAP) {
      if (now - this.since < CAPTION_GAP) return;
      this.phase = IDLE;
    }
    if (this.phase !== IDLE || !enabled || now - this.lastCheck < CAPTION_CHECK) return;
    const pick = this.check(scene, view, lap, now);
    if (pick < 0) return;
    this.announced[scene.spotId[pick]] = lap;
    this.landmark = scene.spots[pick].id;
    this.showing = true;
    this.phase = SHOW;
    this.since = now;
  }

  private check(scene: CaptionScene, view: CaptionView, lap: number, now: number): number {
    this.lastCheck = now;
    return pickCaption(scene, view, this.announced, lap, this.rule);
  }

  private reset(scene: CaptionScene | null): void {
    this.scene = scene;
    if (this.announced.length !== (scene?.ids.length ?? 0)) this.announced = new Int32Array(scene?.ids.length ?? 0);
    else this.announced.fill(0);
    this.phase = IDLE; this.since = 0; this.lastCheck = -Infinity; this.lastNow = -Infinity;
    this.landmark = null; this.showing = false;
  }
}
