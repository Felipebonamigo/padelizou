// Texturas procedurais do chão da pista (canvas → CanvasTexture): asfalto com marcações e o
// mapa de brilho delas, zebra, cascalho do acostamento, box, decalque da largada (quadriculado
// + marcas do grid) e a poça de luz dos postes. Tudo com mipmap: detalhe fino some na
// distância em vez de cintilar. Ficam fora do textures.ts (que cenário e carros também usam).
import * as THREE from 'three';
import { GRID_FRONT_GAP, GRID_LANE_X, GRID_ROW_GAP, MAX_CARS, SEGMENT_LENGTH } from '../core/constants';
import { fbm, hash2 } from './noise';
import { hexToRgb, mix, shade, type Palette } from './palette';
import { ROAD_HALF_WIDTH_M, SEGMENT_M } from './units';

function canvas(w: number, h: number): [HTMLCanvasElement, CanvasRenderingContext2D] {
  const c = document.createElement('canvas');
  c.width = w; c.height = h;
  const ctx = c.getContext('2d');
  if (!ctx) throw new Error('sem canvas 2D');
  return [c, ctx];
}

function makeTexture(c: HTMLCanvasElement, anisotropy: number, wrapS: THREE.Wrapping = THREE.ClampToEdgeWrapping): THREE.CanvasTexture {
  const t = new THREE.CanvasTexture(c);
  t.wrapS = wrapS; t.wrapT = THREE.RepeatWrapping;
  t.colorSpace = THREE.SRGBColorSpace;
  t.anisotropy = anisotropy;
  t.minFilter = THREE.LinearMipmapLinearFilter;
  return t;
}

/**
 * Granulado fino por pixel + manchas de baixa frequência sobre a cor já pintada (determinístico).
 * As manchas vêm de uma grade grossa interpolada (barato) e fecham a repetição ao longo sem emenda.
 */
function grain(ctx: CanvasRenderingContext2D, W: number, H: number, fine: number, blotch: number, seed: number, wrapV: number): void {
  const img = ctx.getImageData(0, 0, W, H);
  const d = img.data;
  const GW = 17; const GH = 65;
  const grid = new Float32Array(GW * GH);
  if (blotch > 0) {
    for (let gy = 0; gy < GH; gy++) for (let gx = 0; gx < GW; gx++) {
      const tv = (gy / (GH - 1)) * wrapV; const s = gy / (GH - 1);
      const tu = (gx / (GW - 1)) * 3.7;
      grid[gy * GW + gx] = (fbm(seed + 3, tv + tu, 3) * (1 - s) + fbm(seed + 3, tv - wrapV + tu, 3) * s) * blotch;
    }
  }
  let s = seed >>> 0;
  for (let y = 0; y < H; y++) {
    const fy = (y / (H - 1)) * (GH - 1); const iy = Math.min(GH - 2, Math.floor(fy)); const ty = fy - iy;
    for (let x = 0; x < W; x++) {
      s = (Math.imul(s, 1664525) + 1013904223) >>> 0;
      const g = ((s >>> 8) / 16777216 - 0.5) * fine;
      let b = 0;
      if (blotch > 0) {
        const fx = (x / (W - 1)) * (GW - 1); const ix = Math.min(GW - 2, Math.floor(fx)); const tx = fx - ix;
        const a0 = grid[iy * GW + ix] + (grid[iy * GW + ix + 1] - grid[iy * GW + ix]) * tx;
        const a1 = grid[(iy + 1) * GW + ix] + (grid[(iy + 1) * GW + ix + 1] - grid[(iy + 1) * GW + ix]) * tx;
        b = a0 + (a1 - a0) * ty;
      }
      const k = (y * W + x) * 4;
      d[k] = Math.max(0, Math.min(255, d[k] + g + b));
      d[k + 1] = Math.max(0, Math.min(255, d[k + 1] + g + b));
      d[k + 2] = Math.max(0, Math.min(255, d[k + 2] + g + b));
    }
  }
  ctx.putImageData(img, 0, 0);
}

/** Metros de pista por repetição da textura do asfalto (3 traços do tracejado central). */
export const ASPHALT_REPEAT_M = 24;
/** Marcações em metros: linha de bordo (a 0,3 m da borda, 0,25 m de largura), tracejado central. */
const EDGE_LINE = [0.3, 0.25] as const;
const DASH = { width: 0.2, length: 3, period: 8 };

function paintMarkings(ctx: CanvasRenderingContext2D, W: number, H: number, color: string): void {
  const road = ROAD_HALF_WIDTH_M * 2;
  const px = (m: number) => (m / road) * W;
  ctx.fillStyle = color;
  ctx.fillRect(Math.round(px(EDGE_LINE[0])), 0, Math.max(3, Math.round(px(EDGE_LINE[1]))), H);
  ctx.fillRect(Math.round(W - px(EDGE_LINE[0] + EDGE_LINE[1])), 0, Math.max(3, Math.round(px(EDGE_LINE[1]))), H);
  const per = H / (ASPHALT_REPEAT_M / DASH.period);
  const dw = Math.max(3, Math.round(px(DASH.width)));
  for (let k = 0; k < ASPHALT_REPEAT_M / DASH.period; k++) {
    ctx.fillRect(Math.round(W / 2 - dw / 2), Math.round(k * per), dw, Math.round(per * DASH.length / DASH.period));
  }
}

/**
 * Asfalto: cor base do bioma, granulado fino, manchas largas (remendos, desgaste), trilhas de
 * pneu mais escuras no meio de cada faixa e as marcações. `glow` é o mapa emissivo (só as
 * marcações): à noite a tinta refletiva acende um pouco e a curva se lê de longe.
 */
export function paintAsphalt(p: Palette, anisotropy: number): { map: THREE.CanvasTexture; glow: THREE.CanvasTexture } {
  const W = 256; const H = 2048;
  const [c, ctx] = canvas(W, H);
  ctx.fillStyle = p.asphalt;
  ctx.fillRect(0, 0, W, H);
  // Trilhas dos pneus: duas por faixa, suaves.
  const [r, g, b] = hexToRgb(shade(p.asphalt, 0.86));
  for (const u of [0.18, 0.32, 0.68, 0.82]) {
    const grd = ctx.createLinearGradient((u - 0.05) * W, 0, (u + 0.05) * W, 0);
    grd.addColorStop(0, `rgba(${r},${g},${b},0)`); grd.addColorStop(0.5, `rgba(${r},${g},${b},0.55)`); grd.addColorStop(1, `rgba(${r},${g},${b},0)`);
    ctx.fillStyle = grd;
    ctx.fillRect((u - 0.05) * W, 0, 0.1 * W, H);
  }
  grain(ctx, W, H, 20, 9, 1234567, 6);
  // Remendos: retângulos um pouco mais escuros ou claros (asfalto refeito), poucos e grandes.
  for (let k = 0; k < 5; k++) {
    const x = hash2(k, 1) * W * 0.8; const y = hash2(k, 2) * H;
    const w = (0.12 + hash2(k, 3) * 0.25) * W; const h = (0.04 + hash2(k, 4) * 0.08) * H;
    ctx.fillStyle = hash2(k, 5) > 0.5 ? 'rgba(0,0,0,0.035)' : 'rgba(255,255,255,0.025)';
    ctx.fillRect(x, y, w, h);
    if (y + h > H) ctx.fillRect(x, y - H, w, h); // repete sem emenda
  }
  paintMarkings(ctx, W, H, mix(p.lane, p.asphalt, 0.08));
  const [gc, gctx] = canvas(64, 512);
  gctx.fillStyle = '#000000'; gctx.fillRect(0, 0, 64, 512);
  paintMarkings(gctx, 64, 512, '#ffffff');
  return { map: makeTexture(c, anisotropy), glow: makeTexture(gc, anisotropy) };
}

/** Zebra: blocos vermelho/branco de 1 m (uma repetição = 2 m), com o topo um pouco mais claro. */
export const KERB_REPEAT_M = 2;
export function paintKerb(p: Palette, anisotropy: number): THREE.CanvasTexture {
  const W = 32; const H = 128;
  const [c, ctx] = canvas(W, H);
  ctx.fillStyle = p.rumbleDark; ctx.fillRect(0, 0, W, H / 2);
  ctx.fillStyle = p.rumbleLight; ctx.fillRect(0, H / 2, W, H / 2);
  // Friso escuro entre os blocos (desenha o bloco mesmo de longe, sem virar serrilhado).
  ctx.fillStyle = 'rgba(0,0,0,0.22)';
  ctx.fillRect(0, H / 2 - 2, W, 3); ctx.fillRect(0, H - 2, W, 2); ctx.fillRect(0, 0, W, 1);
  grain(ctx, W, H, 10, 0, 99, 1);
  return makeTexture(c, anisotropy);
}

/** Acostamento: cascalho/terra (cor da paleta) com pedrisco claro e escuro. */
export const SHOULDER_REPEAT_M = 6;
export function paintShoulder(p: Palette, anisotropy: number): THREE.CanvasTexture {
  const W = 64; const H = 256;
  const [c, ctx] = canvas(W, H);
  ctx.fillStyle = p.shoulder; ctx.fillRect(0, 0, W, H);
  grain(ctx, W, H, 34, 12, 4242, 3);
  // A borda de dentro, junto do asfalto, um pouco mais escura (pó de pneu).
  const grd = ctx.createLinearGradient(0, 0, W * 0.35, 0);
  grd.addColorStop(0, 'rgba(0,0,0,0.25)'); grd.addColorStop(1, 'rgba(0,0,0,0)');
  ctx.fillStyle = grd; ctx.fillRect(0, 0, W * 0.35, H);
  return makeTexture(c, anisotropy);
}

/** Largura do box em x normalizado (a divisa listrada é a faixa que a física trata como grama). */
export const PIT_X0 = 1.02;
export const PIT_X1 = 2.06;
/** Onde a física põe a faixa do box (sim/physics.ts: 1,25 < x < 1,95). */
export const PIT_LANE_X: readonly [number, number] = [1.25, 1.95];
export const PIT_REPEAT_M = 16;

/**
 * Box: divisa zebrada (branco/amarelo em diagonal) até a linha amarela, faixa de concreto claro
 * com as vagas contornadas em branco e "BOX" pintado no chão, linha branca na borda de fora.
 */
export function paintPit(p: Palette, anisotropy: number): THREE.CanvasTexture {
  const W = 256; const H = 512;
  const [c, ctx] = canvas(W, H);
  const u = (x: number) => ((x - PIT_X0) / (PIT_X1 - PIT_X0)) * W;
  ctx.fillStyle = p.pit; ctx.fillRect(0, 0, W, H);
  grain(ctx, W, H, 14, 6, 777, 2);
  const lane0 = u(PIT_LANE_X[0]); const lane1 = u(PIT_LANE_X[1]);
  // Divisa: diagonais amarelas sobre o asfalto da pista.
  ctx.save();
  ctx.beginPath(); ctx.rect(0, 0, lane0 - 6, H); ctx.clip();
  ctx.fillStyle = p.asphalt; ctx.fillRect(0, 0, lane0, H);
  ctx.strokeStyle = p.pitLine; ctx.lineWidth = 9;
  for (let y = -H; y < H * 2; y += 44) { ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(lane0, y + lane0 * 0.8); ctx.stroke(); }
  ctx.restore();
  ctx.fillStyle = p.pitLine;
  ctx.fillRect(lane0 - 8, 0, 9, H);
  ctx.fillStyle = mix(p.lane, p.pit, 0.1);
  ctx.fillRect(lane1, 0, 6, H);
  // Vaga: contorno em U aberto para a pista.
  const x0 = lane0 + 18; const x1 = lane1 - 14;
  ctx.strokeStyle = mix(p.lane, p.pit, 0.15); ctx.lineWidth = 5;
  ctx.beginPath(); ctx.moveTo(x0, 40); ctx.lineTo(x1, 40); ctx.lineTo(x1, 300); ctx.lineTo(x0, 300); ctx.stroke();
  // "BOX" no chão, lido por quem chega (de baixo para cima na textura = para a frente na pista).
  ctx.save();
  ctx.translate((x0 + x1) / 2, 410);
  ctx.fillStyle = mix(p.pitLine, p.pit, 0.1);
  ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
  ctx.font = '900 64px "Segoe UI", Roboto, Arial, sans-serif';
  ctx.scale(1, 1.6);
  ctx.fillText('BOX', 0, 0);
  ctx.restore();
  return makeTexture(c, anisotropy);
}

/** Onde fica a última fila do grid cheio (unidades antes da linha). */
const GRID_LAST_ROW = GRID_FRONT_GAP + (Math.ceil(MAX_CARS / 2) - 1) * GRID_ROW_GAP;
/**
 * Decalque da largada: cobre `START_BEHIND` segmentos antes da linha e `START_AHEAD` depois. Sai do grid do núcleo
 * (mais um segmento para a traseira do último carro): era um 18 fixo, e o grid que se espaçou na onda F saiu dele.
 */
export const START_BEHIND = Math.ceil(GRID_LAST_ROW / SEGMENT_LENGTH) + 1;
export const START_AHEAD = 1;

/** Marca de uma posição de grid: distância (m) antes da linha e x (meia-larguras). */
export interface GridMark { behindM: number; x: number }

/**
 * Posições do grid como sim/race.ts as monta (2 por fila, humanos no fim), das mesmas constantes do núcleo
 * (GRID_FRONT_GAP, GRID_ROW_GAP, GRID_LANE_X). tests/render-ground.test.ts confere contra o `createRace` de verdade.
 */
export function gridMarks(cars: number): GridMark[] {
  const marks: GridMark[] = [];
  for (let i = 0; i < cars; i++) {
    const row = Math.floor(i / 2);
    marks.push({ behindM: (GRID_FRONT_GAP + row * GRID_ROW_GAP) * (SEGMENT_M / SEGMENT_LENGTH), x: i % 2 === 0 ? -GRID_LANE_X : GRID_LANE_X });
  }
  return marks;
}

export function paintStartDecal(p: Palette, segmentM: number, anisotropy: number): THREE.CanvasTexture {
  const W = 256; const H = 1024;
  const [c, ctx] = canvas(W, H);
  ctx.clearRect(0, 0, W, H);
  const total = (START_BEHIND + START_AHEAD) * segmentM;
  const lineAt = START_BEHIND * segmentM; // metros desde o começo do decalque
  const road = ROAD_HALF_WIDTH_M * 2;
  const X = (xm: number) => ((xm + ROAD_HALF_WIDTH_M) / road) * W;
  const Y = (m: number) => (1 - m / total) * H;
  const white = mix(p.lane, '#ffffff', 0.5);
  // Quadriculado: 3 fileiras de 0,6 m, 22 colunas, entre duas linhas brancas.
  const rows = 3; const cell = 0.6; const cols = 22;
  const y0 = lineAt - (rows * cell) / 2;
  for (let r = 0; r < rows; r++) for (let k = 0; k < cols; k++) {
    ctx.fillStyle = (r + k) % 2 === 0 ? '#f4f4f4' : '#161616';
    const xa = X(-ROAD_HALF_WIDTH_M + (k * road) / cols); const xb = X(-ROAD_HALF_WIDTH_M + ((k + 1) * road) / cols);
    const ya = Y(y0 + (r + 1) * cell); const yb = Y(y0 + r * cell);
    ctx.fillRect(Math.floor(xa), Math.floor(ya), Math.ceil(xb - xa), Math.ceil(yb - ya));
  }
  ctx.fillStyle = white;
  for (const m of [y0 - 0.35, y0 + rows * cell + 0.2]) ctx.fillRect(0, Math.round(Y(m + 0.15)), W, Math.max(3, Math.round((0.15 / total) * H)));
  // Marcas do grid: um colchete na frente de cada carro (o bico fica ~2,4 m à frente do centro).
  ctx.strokeStyle = white; ctx.lineWidth = 4; ctx.lineCap = 'butt';
  for (const g of gridMarks(20)) {
    const m = lineAt - g.behindM + 2.6;
    if (m < 1) continue;
    const xc = g.x * ROAD_HALF_WIDTH_M;
    ctx.beginPath();
    ctx.moveTo(X(xc - 1.25), Y(m - 1.1)); ctx.lineTo(X(xc - 1.25), Y(m)); ctx.lineTo(X(xc + 1.25), Y(m)); ctx.lineTo(X(xc + 1.25), Y(m - 1.1));
    ctx.stroke();
  }
  const t = makeTexture(c, anisotropy);
  t.wrapT = THREE.ClampToEdgeWrapping;
  return t;
}

/** Poça de luz (radial, branca; a cor vem do material). */
export function paintPool(): THREE.CanvasTexture {
  const S = 64;
  const [c, ctx] = canvas(S, S);
  const g = ctx.createRadialGradient(S / 2, S / 2, 1, S / 2, S / 2, S / 2);
  g.addColorStop(0, 'rgba(255,255,255,1)'); g.addColorStop(0.45, 'rgba(255,255,255,0.55)'); g.addColorStop(1, 'rgba(255,255,255,0)');
  ctx.fillStyle = g; ctx.fillRect(0, 0, S, S);
  const t = new THREE.CanvasTexture(c);
  t.colorSpace = THREE.SRGBColorSpace;
  return t;
}
