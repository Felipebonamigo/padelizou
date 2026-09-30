// Texturas do cenário (canvas → CanvasTexture): fachadas de 4 × 4 janelas por repetição (cor + mapa
// de luz das janelas acesas à noite) e o atlas de painéis (8 outdoors de marcas inventadas, faixa da
// largada, placa do box, chevrons, xadrez). Criadas uma vez por Scenery e liberadas no dispose.
// Marcas: todas fictícias (a exceção é a PADELIZOU, do próprio dono) — nada de logotipo real.
import * as THREE from 'three';
import { hash2 } from '../noise';
import { canvas2d } from '../textures';
import { PANEL_ATLAS_SIZE, PANEL_RECTS } from './props';

export interface FacadeTextures { map: THREE.CanvasTexture; light: THREE.CanvasTexture }

function tex(c: HTMLCanvasElement, repeat: boolean, srgb = true): THREE.CanvasTexture {
  const t = new THREE.CanvasTexture(c);
  if (srgb) t.colorSpace = THREE.SRGBColorSpace;
  if (repeat) { t.wrapS = THREE.RepeatWrapping; t.wrapT = THREE.RepeatWrapping; }
  t.anisotropy = 8;
  t.generateMipmaps = true;
  t.minFilter = THREE.LinearMipmapLinearFilter;
  return t;
}

const S = 256;
const CELL = S / 4;

/** Luz de uma janela acesa: quente na maioria, algumas frias (TV, escritório), algumas meio abertas. */
function lightColor(seed: number): string {
  const h = hash2(seed, 91);
  return h < 0.62 ? '#ffd79a' : h < 0.82 ? '#ffe8c4' : '#cfe4ff';
}

export function facadeTextures(style: 'office' | 'apartment' | 'classic' | 'house'): FacadeTextures {
  const [mc, m] = canvas2d(S, S);
  const [lc, l] = canvas2d(S, S);
  l.fillStyle = '#000000'; l.fillRect(0, 0, S, S);
  const seedBase = { office: 11, apartment: 23, classic: 37, house: 53 }[style];
  for (let r = 0; r < 4; r++) for (let c = 0; c < 4; c++) {
    const x = c * CELL; const y = r * CELL;
    const seed = seedBase * 100 + r * 4 + c;
    const lit = hash2(seed, 7) < (style === 'office' ? 0.55 : 0.5);
    if (style === 'office') {
      if (r === 0 && c === 0) { m.fillStyle = '#c9d0d8'; m.fillRect(0, 0, S, S); }
      const g = m.createLinearGradient(0, y, 0, y + CELL);
      g.addColorStop(0, '#46607e'); g.addColorStop(0.55, '#6a86a6'); g.addColorStop(1, '#9ab2cc');
      m.fillStyle = g; m.fillRect(x + 3, y + 3, CELL - 6, CELL - 14);
      m.fillStyle = '#8f9aa8'; m.fillRect(x, y + CELL - 11, CELL, 8);
      m.fillStyle = 'rgba(255,255,255,0.18)'; m.fillRect(x + 6, y + 6, 6, CELL - 22);
      if (lit) {
        l.fillStyle = lightColor(seed);
        const part = hash2(seed, 13) < 0.25 ? 0.45 : 1;
        l.fillRect(x + 4, y + 4 + (CELL - 16) * (1 - part), CELL - 8, (CELL - 16) * part);
      }
    } else if (style === 'apartment') {
      if (r === 0 && c === 0) { m.fillStyle = '#f2f0ea'; m.fillRect(0, 0, S, S); }
      const wx = x + 13; const wy = y + 12; const ww = CELL - 26; const wh = CELL - 26;
      m.fillStyle = '#d8d4cc'; m.fillRect(wx - 3, wy - 3, ww + 6, wh + 6);
      m.fillStyle = '#2a3340'; m.fillRect(wx, wy, ww, wh);
      m.fillStyle = '#4a5a6c'; m.fillRect(wx, wy, ww, 6);
      if (lit) { l.fillStyle = lightColor(seed); l.fillRect(wx + 1, wy + 1, ww - 2, wh - 2); }
      // Sacada: guarda-corpo na frente da metade de baixo, em colunas alternadas.
      if ((c + r) % 2 === 0) {
        m.fillStyle = '#6a7078';
        for (let k = 0; k < 7; k++) m.fillRect(x + 6 + k * 8, y + CELL - 26, 2, 20);
        m.fillRect(x + 4, y + CELL - 27, CELL - 8, 3);
        l.fillStyle = '#000000';
        for (let k = 0; k < 7; k++) l.fillRect(x + 6 + k * 8, y + CELL - 26, 2, 20);
      }
      m.fillStyle = '#c8c4bc'; m.fillRect(x, y + CELL - 5, CELL, 5);
    } else if (style === 'classic') {
      if (r === 0 && c === 0) {
        m.fillStyle = '#efe6d4'; m.fillRect(0, 0, S, S);
        m.fillStyle = 'rgba(120,100,70,0.12)';
        for (let k = 0; k < S; k += 16) m.fillRect(0, k, S, 1);
      }
      const wx = x + 18; const wy = y + 8; const ww = CELL - 36; const wh = CELL - 18;
      m.fillStyle = '#f8f2e6'; m.fillRect(wx - 4, wy - 6, ww + 8, wh + 8);
      m.fillStyle = '#d8ccb4'; m.fillRect(wx - 6, wy - 9, ww + 12, 4);
      m.fillStyle = '#283036'; m.fillRect(wx, wy, ww, wh);
      m.fillStyle = '#f8f2e6'; m.fillRect(wx + ww / 2 - 1, wy, 2, wh); m.fillRect(wx, wy + wh * 0.4, ww, 2);
      if (lit) { l.fillStyle = lightColor(seed); l.fillRect(wx + 1, wy + 1, ww - 2, wh - 2); l.fillStyle = '#000'; l.fillRect(wx + ww / 2 - 1, wy, 2, wh); }
      if (r % 2 === 0) {
        m.fillStyle = '#2a2a2a'; m.fillRect(wx - 6, wy + wh - 10, ww + 12, 2);
        for (let k = 0; k < 6; k++) m.fillRect(wx - 5 + k * ((ww + 10) / 5), wy + wh - 10, 1.5, 10);
      }
    } else {
      if (r === 0 && c === 0) { m.fillStyle = '#f4f0e8'; m.fillRect(0, 0, S, S); }
      if ((c + r * 3) % 4 === 3) continue; // parede cega aqui e ali
      const wx = x + 20; const wy = y + 16; const ww = CELL - 40; const wh = CELL - 30;
      m.fillStyle = '#ffffff'; m.fillRect(wx - 3, wy - 3, ww + 6, wh + 6);
      m.fillStyle = '#2c3440'; m.fillRect(wx, wy, ww, wh);
      m.fillStyle = '#ffffff'; m.fillRect(wx + ww / 2 - 1, wy, 2, wh);
      m.fillStyle = '#4a6a8a'; m.fillRect(wx - 11, wy - 2, 8, wh + 4); m.fillRect(wx + ww + 3, wy - 2, 8, wh + 4);
      if (lit) { l.fillStyle = lightColor(seed); l.fillRect(wx + 1, wy + 1, ww - 2, wh - 2); l.fillStyle = '#000'; l.fillRect(wx + ww / 2 - 1, wy, 2, wh); }
    }
  }
  return { map: tex(mc, true), light: tex(lc, true) };
}

// ───────────────────────────── Atlas de painéis ─────────────────────────────

interface BillboardDesign { brand: string; tag: string; bg: string; fg: string; accent: string; motif: 'stripes' | 'sun' | 'bubbles' | 'bolt' | 'tire' | 'wave' | 'check' | 'rings' }

const DESIGNS: BillboardDesign[] = [
  { brand: 'NITRO', tag: '', bg: '#e8262c', fg: '#ffffff', accent: '#ffd23f', motif: 'stripes' },
  { brand: 'CREW', tag: 'RACING', bg: '#1b5fc4', fg: '#ffffff', accent: '#9fd4ff', motif: 'check' },
  { brand: 'PADELIZOU', tag: '', bg: '#0f8b5f', fg: '#ffffff', accent: '#b8f06a', motif: 'rings' },
  { brand: 'APEX', tag: '', bg: '#15171b', fg: '#ffd23f', accent: '#ffd23f', motif: 'tire' },
  { brand: 'VOLTZ', tag: '', bg: '#5b2bb5', fg: '#fff34a', accent: '#ff6ad5', motif: 'bolt' },
  { brand: 'SOLARA', tag: '', bg: '#ff8a1f', fg: '#ffffff', accent: '#ffe066', motif: 'sun' },
  { brand: 'FIZZ!', tag: '', bg: '#2fc27a', fg: '#ffffff', accent: '#d9ffe6', motif: 'bubbles' },
  { brand: '66 FM', tag: '', bg: '#101a3a', fg: '#ff5fb0', accent: '#46e0ff', motif: 'wave' },
];

function fitText(ctx: CanvasRenderingContext2D, text: string, maxW: number, size: number, weight = '900'): number {
  let s = size;
  for (;;) {
    ctx.font = `${weight} ${s}px "Arial Black", "Helvetica Neue", Arial, sans-serif`;
    if (ctx.measureText(text).width <= maxW || s < 12) return s;
    s -= 4;
  }
}

function drawBillboard(ctx: CanvasRenderingContext2D, x: number, y: number, w: number, h: number, d: BillboardDesign): void {
  ctx.save();
  ctx.beginPath(); ctx.rect(x, y, w, h); ctx.clip();
  ctx.fillStyle = d.bg; ctx.fillRect(x, y, w, h);
  ctx.fillStyle = d.accent;
  switch (d.motif) {
    case 'stripes':
      for (let k = 0; k < 5; k++) { ctx.beginPath(); ctx.moveTo(x + w * 0.62 + k * 34, y + h); ctx.lineTo(x + w * 0.72 + k * 34, y); ctx.lineTo(x + w * 0.72 + k * 34 + 16, y); ctx.lineTo(x + w * 0.62 + k * 34 + 16, y + h); ctx.fill(); }
      break;
    case 'check':
      for (let r = 0; r < 6; r++) for (let c = 0; c < 4; c++) if ((r + c) % 2 === 0) ctx.fillRect(x + w - 4 * 32 + c * 32, y + r * 32, 32, 32);
      break;
    case 'rings':
      ctx.lineWidth = 10; ctx.strokeStyle = d.accent;
      for (let k = 0; k < 3; k++) { ctx.beginPath(); ctx.arc(x + w * 0.86, y + h * 0.5, 30 + k * 26, 0, Math.PI * 2); ctx.stroke(); }
      break;
    case 'tire':
      ctx.beginPath(); ctx.arc(x + w * 0.84, y + h * 0.5, 70, 0, Math.PI * 2); ctx.fillStyle = '#2a2d33'; ctx.fill();
      ctx.lineWidth = 14; ctx.strokeStyle = d.accent; ctx.beginPath(); ctx.arc(x + w * 0.84, y + h * 0.5, 44, 0, Math.PI * 2); ctx.stroke();
      ctx.fillStyle = d.accent; ctx.fillRect(x, y + h - 16, w, 16);
      break;
    case 'bolt':
      ctx.beginPath(); ctx.moveTo(x + w * 0.86, y + 10); ctx.lineTo(x + w * 0.76, y + h * 0.55); ctx.lineTo(x + w * 0.84, y + h * 0.55); ctx.lineTo(x + w * 0.78, y + h - 10); ctx.lineTo(x + w * 0.94, y + h * 0.42); ctx.lineTo(x + w * 0.86, y + h * 0.42); ctx.lineTo(x + w * 0.93, y + 10); ctx.fill();
      break;
    case 'sun':
      ctx.beginPath(); ctx.arc(x + w * 0.85, y + h * 0.95, 80, 0, Math.PI * 2); ctx.fill();
      for (let k = 0; k < 9; k++) { const a = Math.PI + (k / 8) * Math.PI; ctx.fillRect(x + w * 0.85 + Math.cos(a) * 100, y + h * 0.95 + Math.sin(a) * 100, 10, 10); }
      break;
    case 'bubbles':
      for (let k = 0; k < 9; k++) { ctx.beginPath(); ctx.arc(x + w * (0.7 + hash2(k, 1) * 0.28), y + h * hash2(k, 2), 8 + hash2(k, 3) * 18, 0, Math.PI * 2); ctx.fill(); }
      break;
    case 'wave':
      ctx.lineWidth = 8; ctx.strokeStyle = d.accent; ctx.beginPath();
      for (let k = 0; k <= 40; k++) { const px = x + w * 0.62 + k * 4.6; const py = y + h * 0.5 + Math.sin(k * 0.7) * 40 * Math.sin(k * 0.08); if (k === 0) ctx.moveTo(px, py); else ctx.lineTo(px, py); }
      ctx.stroke();
      break;
  }
  ctx.fillStyle = d.fg;
  ctx.textAlign = 'left'; ctx.textBaseline = 'middle';
  const maxW = w * (d.motif === 'check' || d.motif === 'rings' || d.motif === 'tire' ? 0.64 : 0.6);
  const size = fitText(ctx, d.brand, maxW, 118);
  ctx.fillText(d.brand, x + 26, y + h * (d.tag ? 0.42 : 0.52));
  if (d.tag) { fitText(ctx, d.tag, maxW, 44, '700'); ctx.fillStyle = d.accent; ctx.fillText(d.tag, x + 30, y + h * 0.42 + size * 0.62); }
  // Borda escura: o painel descola da moldura mesmo contra o céu.
  ctx.strokeStyle = 'rgba(0,0,0,0.35)'; ctx.lineWidth = 8; ctx.strokeRect(x + 4, y + 4, w - 8, h - 8);
  ctx.restore();
}

function drawChevrons(ctx: CanvasRenderingContext2D, x: number, y: number, w: number, h: number, dir: -1 | 1): void {
  ctx.fillStyle = '#ffd21f'; ctx.fillRect(x, y, w, h);
  ctx.fillStyle = '#141414';
  for (let k = 0; k < 3; k++) {
    const cx = x + w * (0.22 + k * 0.28);
    const tip = cx + dir * 22; const back = cx - dir * 22;
    ctx.beginPath();
    ctx.moveTo(back, y + 8); ctx.lineTo(back + dir * 20, y + 8); ctx.lineTo(tip + dir * 20, y + h / 2); ctx.lineTo(back + dir * 20, y + h - 8); ctx.lineTo(back, y + h - 8); ctx.lineTo(tip, y + h / 2);
    ctx.fill();
  }
}

export function panelAtlas(): THREE.CanvasTexture {
  const [c, ctx] = canvas2d(PANEL_ATLAS_SIZE, PANEL_ATLAS_SIZE);
  ctx.fillStyle = '#202020'; ctx.fillRect(0, 0, PANEL_ATLAS_SIZE, PANEL_ATLAS_SIZE);
  PANEL_RECTS.billboard.forEach(([x, y, w, h], i) => drawBillboard(ctx, x, y, w, h, DESIGNS[i]));
  {
    const [x, y, w, h] = PANEL_RECTS.banner;
    ctx.fillStyle = '#111111'; ctx.fillRect(x, y, w, h);
    for (let r = 0; r < 4; r++) for (let k = 0; k < 4; k++) {
      ctx.fillStyle = (r + k) % 2 ? '#ffffff' : '#111111';
      ctx.fillRect(x + k * 32, y + r * 32, 32, 32); ctx.fillRect(x + w - 128 + k * 32, y + r * 32, 32, 32);
    }
    ctx.fillStyle = '#ffd23f'; ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
    fitText(ctx, 'NITRO CREW', w - 300, 96);
    ctx.fillText('NITRO CREW', x + w / 2, y + h / 2 + 4);
    ctx.fillStyle = '#ff3b3b'; ctx.fillRect(x + 128, y, w - 256, 8); ctx.fillRect(x + 128, y + h - 8, w - 256, 8);
  }
  {
    const [x, y, w, h] = PANEL_RECTS.pit;
    ctx.fillStyle = '#ffd23f'; ctx.fillRect(x, y, w, h);
    ctx.fillStyle = '#111111'; ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
    fitText(ctx, 'BOX →', w - 30, 80);
    ctx.fillText('BOX →', x + w / 2, y + h / 2 + 3);
  }
  { const [x, y, w, h] = PANEL_RECTS.chevronLeft; drawChevrons(ctx, x, y, w, h, -1); }
  { const [x, y, w, h] = PANEL_RECTS.chevronRight; drawChevrons(ctx, x, y, w, h, 1); }
  {
    const [x, y, w, h] = PANEL_RECTS.checker;
    for (let r = 0; r < 4; r++) for (let k = 0; k < 8; k++) { ctx.fillStyle = (r + k) % 2 ? '#ffffff' : '#111111'; ctx.fillRect(x + k * (w / 8), y + r * (h / 4), w / 8, h / 4); }
  }
  return tex(c, false);
}
