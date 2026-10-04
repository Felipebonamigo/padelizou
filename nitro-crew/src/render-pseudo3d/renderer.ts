// Modo Retrô: renderizador pseudo-3D (estilo Top Gear / Out Run) que implementa a mesma interface
// `Renderer` do 3D. Troca de um pelo outro em session.ts (Opções › Visual); o núcleo não muda nada.
//
// Por viewport (tela dividida de 1 a 4 via viewportRects):
//   1. câmera PLAYER_Z atrás do carro do jogador, na altura da pista + CAMERA_HEIGHT;
//   2. projectRoad (projection.ts): segmentos a partir de segmentAt(câmera), curva acumulada (x += dx; dx += curve),
//      altura por y0/y1 — o algoritmo do Javascript Racer;
//   3. pinta numa tela pequena (180–270 linhas, pixel grande de propósito) DE TRÁS PARA A FRENTE (algoritmo do
//      pintor): fundo (céu, sol/lua, montanhas em paralaxe), e para cada segmento, do mais longe ao mais perto,
//      grama/zebra/asfalto/faixas (alternando pela `band` do segmento), depois os sprites daquele segmento e os
//      carros que estão nele — o que está mais perto cobre o que está atrás, inclusive atrás de morro;
//   4. amplia a tela pequena para o retângulo do viewport sem suavização.
// O HUD é o mesmo do 3D (DOM por cima, src/render/hud.ts).
import { MAX_CARS } from '../core/constants';
import { seatColor } from '../core/data/drivers';
import type { RaceState, Track } from '../core/types';
import type { Quality, RenderFrame, Renderer, ViewportSpec } from '../game/contracts';
import { Hud } from '../render/hud';
import { VIEWPORT_SEAM, viewportRects, type Rect } from '../render/layout';
import { absoluteHeading } from '../render/roadframe';
import { mix, retroPalette, type RetroPalette } from './palette';
import {
  CAMERA_HEIGHT, PLAYER_Z, ROAD_WIDTH, placeOnRoad, projectRoad, roadYAt, screenSize, type ProjectedSegment, type RoadCamera, type Viewport,
} from './projection';
import { CAR_WORLD_WIDTH, SpriteAtlas, spriteAspect, spriteWorldWidth } from './sprites';

/** Linhas da tela pequena por qualidade (a largura acompanha a proporção do viewport). */
const LINES: Record<Quality, number> = { low: 180, medium: 224, high: 270 };
/** Segmentos desenhados à frente por qualidade. */
const DRAW_DISTANCE: Record<Quality, number> = { low: 120, medium: 170, high: 220 };
/** Largura (px da tela pequena) de um "azulejo" do fundo; uma volta completa de rumo = um azulejo. */
const BG_TILE = 640;
/** Faixas de rodagem desenhadas (as marcações ficam entre elas). */
const LANES = 3;

/** Um carro a desenhar dentro de um segmento projetado. */
interface CarDraw { index: number; x: number; y: number; scale: number; z: number }

/** Recursos de um viewport, reaproveitados entre quadros. */
interface ViewState {
  canvas: HTMLCanvasElement;
  ctx: CanvasRenderingContext2D;
  projected: ProjectedSegment[];
  buckets: CarDraw[][];
}

/** Camadas do fundo de uma pista (montanhas longe e morros/silhuetas perto), em azulejos horizontais. */
interface Background { far: HTMLCanvasElement; near: HTMLCanvasElement; height: number }

function make2d(w: number, h: number): [HTMLCanvasElement, CanvasRenderingContext2D] {
  const c = document.createElement('canvas');
  c.width = Math.max(1, Math.round(w)); c.height = Math.max(1, Math.round(h));
  const ctx = c.getContext('2d');
  if (!ctx) throw new Error('canvas 2d indisponível');
  ctx.imageSmoothingEnabled = false;
  return [c, ctx];
}

/** Ruído 1D suave e periódico (período = `period`), só para o desenho do fundo. */
function ridge(seed: number, t: number, period: number): number {
  let v = 0;
  for (let k = 1; k <= 4; k++) v += Math.sin((t / period) * Math.PI * 2 * k * (k === 1 ? 1 : k + 1) + seed * (k * 1.7)) / (k * 1.3);
  return v;
}

function seedOf(id: string): number {
  let h = 0;
  for (let i = 0; i < id.length; i++) h = (h * 31 + id.charCodeAt(i)) % 100_000;
  return h / 997;
}

export class RetroRenderer implements Renderer {
  readonly canvas: HTMLCanvasElement;
  private readonly ctx: CanvasRenderingContext2D;
  private readonly hud: Hud;
  private readonly atlas = new SpriteAtlas();
  private readonly views: ViewState[] = [];
  private readonly backgrounds = new Map<string, Background>();
  private cssWidth = 1;
  private cssHeight = 1;
  /** Freio aparente de cada carro: a velocidade caiu desde o último tick → lanternas acesas por alguns ticks. */
  private readonly lastSpeed = new Float64Array(MAX_CARS);
  private readonly brakeUntil = new Int32Array(MAX_CARS);
  private lastTick = -1;
  private stats = { segments: 0, sprites: 0, cars: 0 };

  constructor(canvas: HTMLCanvasElement, hudRoot: HTMLElement) {
    this.canvas = canvas;
    const ctx = canvas.getContext('2d');
    if (!ctx) throw new Error('O modo Retrô precisa de canvas 2D');
    this.ctx = ctx;
    this.hud = new Hud(hudRoot);
  }

  resize(width: number, height: number, dpr: number): void {
    this.cssWidth = Math.max(1, width);
    this.cssHeight = Math.max(1, height);
    this.canvas.width = Math.max(1, Math.round(width * dpr));
    this.canvas.height = Math.max(1, Math.round(height * dpr));
    this.canvas.style.width = `${width}px`;
    this.canvas.style.height = `${height}px`;
  }

  render(frame: RenderFrame): void {
    const { state, track, viewports } = frame;
    this.trackBrakes(state);
    this.stats = { segments: 0, sprites: 0, cars: 0 };
    const rects = viewportRects(viewports.length, this.canvas.width, this.canvas.height);
    for (let i = 0; i < viewports.length; i++) {
      const vp = viewports[i];
      const car = state.cars[vp.carIndex];
      if (!car) continue;
      const cam: RoadCamera = { z: car.z - PLAYER_Z, x: car.x * ROAD_WIDTH, y: roadYAt(track, car.z) + CAMERA_HEIGHT };
      this.drawView(i, rects[i], track, cam, frame.options.quality, frame.time, state, vp, frame, i === 0);
    }
    this.drawSeams(rects);
    if (frame.showHud) this.hud.update(frame, this.cssWidth, this.cssHeight); else this.hud.hide();
  }

  renderIdle(time: number, track: Track): void {
    // Fundo dos menus: câmera voando sozinha pela pista, desviando devagar de um lado para o outro.
    const z = (time * 2600) % track.length;
    const cam: RoadCamera = { z, x: Math.sin(time * 0.25) * 0.35 * ROAD_WIDTH, y: roadYAt(track, z + PLAYER_Z) + CAMERA_HEIGHT };
    this.drawView(0, { x: 0, y: 0, w: this.canvas.width, h: this.canvas.height }, track, cam, 'medium', time, null, null, null, false);
    this.hud.hide();
  }

  dispose(): void {
    this.hud.dispose();
    this.atlas.clear();
    this.backgrounds.clear();
    this.views.length = 0;
  }

  /** Números para capturas e testes manuais (o harness do 3D imprime algo parecido). */
  debugInfo(): { mode: 'retro'; segments: number; sprites: number; cars: number } {
    return { mode: 'retro', ...this.stats };
  }

  // ───────────────────────────── Um viewport ─────────────────────────────

  private drawView(
    index: number, rect: Rect, track: Track, cam: RoadCamera, quality: Quality, time: number,
    state: RaceState | null, vp: ViewportSpec | null, frame: RenderFrame | null, withGhost: boolean,
  ): void {
    const lines = LINES[quality];
    const view: Viewport = { width: Math.max(1, Math.round(lines * (rect.w / Math.max(1, rect.h)))), height: lines };
    const vs = this.viewState(index, view);
    const ctx = vs.ctx;
    const pal = retroPalette(track.def.scenery, track.def.timeOfDay);
    const night = pal.lights;

    // 1. Projeção (da frente para trás) e carros separados por segmento projetado.
    const projected = projectRoad(track, cam, view, DRAW_DISTANCE[quality], vs.projected);
    vs.projected = projected;
    for (const b of vs.buckets) b.length = 0;
    while (vs.buckets.length < projected.length) vs.buckets.push([]);
    if (state) {
      for (let i = 0; i < state.cars.length; i++) {
        const c = state.cars[i];
        const at = placeOnRoad(projected, track, c.z, c.x, view);
        if (at) vs.buckets[at.n].push({ index: i, x: at.x, y: at.y, scale: at.scale, z: c.z });
      }
    }

    // 2. Fundo.
    this.drawBackground(ctx, view, track, pal, cam, time);

    // 3. Pista, sprites e carros de trás para a frente (algoritmo do pintor).
    for (let n = projected.length - 1; n >= 0; n--) {
      const item = projected[n];
      if (item.visible) { this.drawSegment(ctx, view, item, pal); this.stats.segments++; }
      if (!item.inFront) continue;
      for (const s of item.segment.sprites) {
        const worldW = spriteWorldWidth(s.kind, s.scale);
        const w = screenSize(worldW, item.p1.scale, view);
        if (w < 1) continue;
        const h = w * spriteAspect(s.kind, s.variant);
        const x = item.p1.x + item.p1.scale * (s.kind === 'banner_start' ? 0 : s.x) * ROAD_WIDTH * (view.width / 2);
        if (x + w / 2 < 0 || x - w / 2 > view.width) continue;
        ctx.drawImage(this.atlas.scenery(s.kind, s.variant, night), Math.round(x - w / 2), Math.round(item.p1.y - h), Math.round(w), Math.round(h));
        this.stats.sprites++;
      }
      const bucket = vs.buckets[n];
      if (bucket.length > 1) bucket.sort((a, b) => b.z - a.z); // dentro do segmento, o mais longe primeiro
      for (const d of bucket) if (state) this.drawCar(ctx, view, state, d, vp, frame, time, night);
    }

    // 4. Fantasma do contra-relógio (só no primeiro viewport), translúcido.
    const ghost = withGhost ? frame?.ghost?.pose : null;
    if (ghost && frame) {
      const at = placeOnRoad(projected, track, ghost.z, ghost.x, view);
      if (at) {
        const w = screenSize(CAR_WORLD_WIDTH, at.scale, view);
        ctx.globalAlpha = 0.45;
        ctx.drawImage(this.atlas.car(frame.ghost?.carId ?? 'falcao', ghost.steerPose, false, night), Math.round(at.x - w / 2), Math.round(at.y - w * SpriteAtlas.carAspect), Math.round(w), Math.round(w * SpriteAtlas.carAspect));
        ctx.globalAlpha = 1;
      }
    }

    // 5. Amplia para a tela de verdade, pixel grande.
    this.ctx.imageSmoothingEnabled = false;
    this.ctx.drawImage(vs.canvas, rect.x, rect.y, rect.w, rect.h);
  }

  private viewState(index: number, view: Viewport): ViewState {
    let vs = this.views[index];
    if (!vs) {
      const [canvas, ctx] = make2d(view.width, view.height);
      vs = { canvas, ctx, projected: [], buckets: [] };
      this.views[index] = vs;
    }
    if (vs.canvas.width !== view.width || vs.canvas.height !== view.height) {
      vs.canvas.width = view.width; vs.canvas.height = view.height;
      vs.ctx.imageSmoothingEnabled = false;
    }
    return vs;
  }

  // ───────────────────────────── Fundo ─────────────────────────────

  private drawBackground(ctx: CanvasRenderingContext2D, view: Viewport, track: Track, pal: RetroPalette, cam: RoadCamera, time: number): void {
    const horizon = Math.round(view.height / 2);
    const sky = ctx.createLinearGradient(0, 0, 0, horizon);
    sky.addColorStop(0, pal.skyTop);
    sky.addColorStop(1, pal.skyBottom);
    ctx.fillStyle = sky;
    ctx.fillRect(0, 0, view.width, horizon + 1);
    ctx.fillStyle = pal.grass[1];
    ctx.fillRect(0, horizon, view.width, view.height - horizon);

    // Rumo absoluto da pista: o que está "no mundo" desliza na tela quando a estrada vira.
    const heading = absoluteHeading(track, cam.z);
    const turn = heading / (Math.PI * 2);
    // A câmera sobe e desce com os morros: o fundo desce e sobe um pouco (paralaxe vertical).
    const lift = Math.max(-view.height * 0.12, Math.min(view.height * 0.12, (cam.y - CAMERA_HEIGHT) * 0.0006));

    if (pal.stars) {
      ctx.fillStyle = '#ffffff';
      for (let i = 0; i < 60; i++) {
        const sx = ((i * 97.3 + turn * BG_TILE * 0.3) % view.width + view.width) % view.width;
        const sy = (i * 53.7) % (horizon * 0.8);
        if ((i + Math.floor(time * 2)) % 7 !== 0) ctx.fillRect(Math.round(sx), Math.round(sy), 1, 1);
      }
    }
    const orb = pal.sun ?? pal.moon;
    if (orb) {
      const ox = ((view.width * 0.7 - turn * BG_TILE * 0.5) % view.width + view.width) % view.width;
      const oy = horizon * (pal.sun ? 0.42 : 0.3) + lift;
      const r = view.height * 0.07;
      ctx.fillStyle = mix(orb, pal.skyBottom, 0.6);
      ctx.beginPath(); ctx.arc(ox, oy, r * 1.6, 0, Math.PI * 2); ctx.fill();
      ctx.fillStyle = orb;
      ctx.beginPath(); ctx.arc(ox, oy, r, 0, Math.PI * 2); ctx.fill();
    }

    const bg = this.background(track, pal, view.height);
    this.tile(ctx, bg.far, view, -turn * BG_TILE * 0.6, horizon - bg.height + lift * 0.6);
    this.tile(ctx, bg.near, view, -turn * BG_TILE * 1.2, horizon - bg.height + lift);
  }

  /** Repete um azulejo horizontal deslocado por `offset` px. */
  private tile(ctx: CanvasRenderingContext2D, img: HTMLCanvasElement, view: Viewport, offset: number, y: number): void {
    const w = img.width;
    let x = ((offset % w) + w) % w - w;
    for (; x < view.width; x += w) ctx.drawImage(img, Math.round(x), Math.round(y));
  }

  /** Monta (e guarda) as camadas do fundo de uma pista: cordilheira ao longe e morros/cidade perto. */
  private background(track: Track, pal: RetroPalette, lines: number): Background {
    const key = `${track.def.id}:${lines}`;
    const cached = this.backgrounds.get(key);
    if (cached) return cached;
    const height = Math.round(lines * 0.34);
    const seed = seedOf(track.def.id);
    const [far, fctx] = make2d(BG_TILE, height);
    const [near, nctx] = make2d(BG_TILE, height);
    const city = track.def.scenery === 'city_night';
    const peak = track.def.scenery === 'alpine' ? 0.95 : track.def.scenery === 'desert' ? 0.55 : 0.75;
    // Longe: cordilheira (neve no topo nas montanhas).
    fctx.fillStyle = pal.far;
    for (let x = 0; x < BG_TILE; x++) {
      const top = height * (1 - peak * (0.45 + 0.25 * ridge(seed, x, BG_TILE)));
      fctx.fillRect(x, Math.round(top), 1, height);
      if (track.def.scenery === 'alpine' && top < height * 0.3) { fctx.fillStyle = '#f4f8ff'; fctx.fillRect(x, Math.round(top), 1, 3); fctx.fillStyle = pal.far; }
    }
    // Perto: morros baixos, ou a silhueta da cidade com janelas acesas.
    if (city) {
      let x = 0; let k = 0;
      while (x < BG_TILE) {
        const w = 14 + ((k * 37) % 22); const h = height * (0.35 + (((k * 53) % 60) / 100));
        nctx.fillStyle = pal.near; nctx.fillRect(x, Math.round(height - h), w, Math.ceil(h));
        nctx.fillStyle = '#ffd86a';
        for (let wy = height - h + 3; wy < height - 2; wy += 4) for (let wx = x + 2; wx < x + w - 2; wx += 4) if (((wx * 7 + wy * 3 + k) % 5) !== 0) nctx.fillRect(wx, Math.round(wy), 1, 1);
        x += w + 1; k++;
      }
    } else {
      nctx.fillStyle = pal.near;
      for (let x = 0; x < BG_TILE; x++) {
        const top = height * (1 - 0.35 * (0.6 + 0.4 * ridge(seed + 3.1, x, BG_TILE / 2)));
        nctx.fillRect(x, Math.round(top), 1, height);
      }
    }
    const bg = { far, near, height };
    this.backgrounds.set(key, bg);
    return bg;
  }

  // ───────────────────────────── Pista ─────────────────────────────

  /**
   * Um segmento: faixa de chão na largura toda entre as duas bordas, zebras dos dois lados, asfalto, box à
   * direita e marcações nas bandas claras — tudo com as cores alternadas pela `band` do segmento, e névoa.
   */
  private drawSegment(ctx: CanvasRenderingContext2D, view: Viewport, item: ProjectedSegment, pal: RetroPalette): void {
    const { p1, p2, segment, fog } = item;
    const band = segment.band;
    const fogged = (c: string) => (fog > 0.01 ? mix(c, pal.fog, fog) : c);
    const x1 = p1.x; const y1 = p1.y; const w1 = p1.w;
    const x2 = p2.x; const y2 = p2.y; const w2 = p2.w;
    const r1 = w1 / 6; const r2 = w2 / 6;      // largura da zebra
    ctx.fillStyle = fogged(pal.grass[band]);
    ctx.fillRect(0, y2, view.width, y1 - y2);
    const quad = (color: string, ax: number, bx: number, cx: number, dx: number) => {
      ctx.fillStyle = color;
      ctx.beginPath();
      ctx.moveTo(ax, y1); ctx.lineTo(bx, y1); ctx.lineTo(cx, y2); ctx.lineTo(dx, y2);
      ctx.closePath(); ctx.fill();
    };
    if (segment.pit) quad(fogged(pal.pit), x1 + w1 + r1, x1 + w1 * 1.95, x2 + w2 * 1.95, x2 + w2 + r2);
    quad(fogged(pal.rumble[band]), x1 - w1 - r1, x1 - w1, x2 - w2, x2 - w2 - r2);
    quad(fogged(pal.rumble[band]), x1 + w1 + r1, x1 + w1, x2 + w2, x2 + w2 + r2);
    quad(fogged(pal.road[band]), x1 - w1, x1 + w1, x2 + w2, x2 - w2);
    if (band === 0) {
      const l1 = w1 / 32; const l2 = w2 / 32;   // largura da marcação
      for (let k = 1; k < LANES; k++) {
        const f = -1 + (2 * k) / LANES;
        const lx1 = x1 + w1 * f; const lx2 = x2 + w2 * f;
        quad(fogged(pal.lane), lx1 - l1, lx1 + l1, lx2 + l2, lx2 - l2);
      }
    }
  }

  // ───────────────────────────── Carros ─────────────────────────────

  private trackBrakes(state: RaceState): void {
    if (state.tick === this.lastTick) return;
    for (const c of state.cars) {
      if (c.speed < this.lastSpeed[c.id] - 20) this.brakeUntil[c.id] = state.tick + 8;
      this.lastSpeed[c.id] = c.speed;
    }
    this.lastTick = state.tick;
  }

  private drawCar(
    ctx: CanvasRenderingContext2D, view: Viewport, state: RaceState, d: CarDraw, vp: ViewportSpec | null,
    frame: RenderFrame | null, time: number, night: boolean,
  ): void {
    const car = state.cars[d.index];
    const w = screenSize(CAR_WORLD_WIDTH, d.scale, view);
    if (w < 1) return;
    const h = w * SpriteAtlas.carAspect;
    const own = vp !== null && d.index === vp.carIndex;
    // O carro do jogador treme de leve com a velocidade (a vibração clássica do motor).
    const bounce = own && car.speed > 0 ? Math.round(Math.sin(time * 40) * Math.min(1, car.speed / 4000)) : 0;
    const braking = state.tick < this.brakeUntil[car.id];
    const x = Math.round(d.x - w / 2); const y = Math.round(d.y - h) + bounce;
    // Poeira na grama e chama do nitro saem por baixo/atrás do carro.
    if (car.skidTicks > 0 && car.speed > 500) {
      ctx.fillStyle = 'rgba(210,190,150,0.7)';
      for (let k = 0; k < 3; k++) ctx.fillRect(Math.round(x + w * (0.1 + 0.35 * k) + Math.sin(time * 30 + k) * 2), Math.round(d.y - h * 0.15), Math.max(1, Math.round(w * 0.14)), Math.max(1, Math.round(h * 0.15)));
    }
    ctx.drawImage(this.atlas.car(car.carId, car.steerPose, braking, night, frame?.paints?.[d.index]), x, y, Math.round(w), Math.round(h));
    if (car.nitroTicks > 0) {
      const flick = 0.7 + 0.3 * Math.sin(time * 60);
      ctx.fillStyle = '#ffd23f';
      ctx.fillRect(Math.round(d.x - w * 0.12), Math.round(d.y - h * 0.08), Math.max(1, Math.round(w * 0.08)), Math.max(1, Math.round(h * 0.22 * flick)));
      ctx.fillStyle = '#ff6a1a';
      ctx.fillRect(Math.round(d.x + w * 0.04), Math.round(d.y - h * 0.08), Math.max(1, Math.round(w * 0.08)), Math.max(1, Math.round(h * 0.22 * flick)));
    }
    // Etiqueta dos outros humanos (co-op): P2, P3… na cor do assento.
    if (!own && car.seat >= 0 && w > 6 && frame) {
      const color = seatColor(car.seat, frame.options.palette);
      const label = `P${car.seat + 1}`;
      const size = Math.max(6, Math.round(Math.min(10, w * 0.35)));
      ctx.font = `bold ${size}px monospace`;
      const tw = ctx.measureText(label).width;
      ctx.fillStyle = 'rgba(0,0,0,0.6)';
      ctx.fillRect(Math.round(d.x - tw / 2 - 2), Math.round(y - size - 3), Math.round(tw + 4), size + 2);
      ctx.fillStyle = color;
      ctx.fillText(label, Math.round(d.x - tw / 2), Math.round(y - 3));
    }
    this.stats.cars++;
  }

  /** Linha escura entre as telas divididas (a mesma espessura do 3D). */
  private drawSeams(rects: Rect[]): void {
    if (rects.length < 2) return;
    this.ctx.fillStyle = '#05070c';
    for (const r of rects) {
      if (r.x > 0) this.ctx.fillRect(r.x - VIEWPORT_SEAM / 2, r.y, VIEWPORT_SEAM, r.h);
      if (r.y > 0) this.ctx.fillRect(r.x, r.y - VIEWPORT_SEAM / 2, r.w, VIEWPORT_SEAM);
    }
  }
}

/** Cria o renderizador Retrô (o equivalente do `createRenderer` do 3D). */
export function createRetroRenderer(canvas: HTMLCanvasElement, hudRoot: HTMLElement): RetroRenderer {
  return new RetroRenderer(canvas, hudRoot);
}
