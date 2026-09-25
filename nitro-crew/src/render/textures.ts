// Texturas procedurais compartilhadas (canvas → CanvasTexture), criadas uma vez e cacheadas.
import * as THREE from 'three';
import { hash2 } from './noise';

export function canvas2d(w: number, h: number): [HTMLCanvasElement, CanvasRenderingContext2D] {
  const c = document.createElement('canvas');
  c.width = w; c.height = h;
  const ctx = c.getContext('2d');
  if (!ctx) throw new Error('sem canvas 2D');
  return [c, ctx];
}

export function texture(c: HTMLCanvasElement, repeat = false): THREE.CanvasTexture {
  const t = new THREE.CanvasTexture(c);
  t.colorSpace = THREE.SRGBColorSpace;
  if (repeat) { t.wrapS = THREE.RepeatWrapping; t.wrapT = THREE.RepeatWrapping; }
  t.anisotropy = 4;
  return t;
}

export interface WindowTextures { wall: THREE.CanvasTexture; win: THREE.CanvasTexture }

let windows: WindowTextures | null = null;

/**
 * Azulejo de janelas (uma repetição = 6 m × 6 m: 2 janelas por andar, 2 andares). `wall` é a
 * cor difusa (parede branca, vidro escuro; multiplicada pela cor do prédio); `win` é o mapa
 * emissivo (só as janelas acesas, algumas apagadas).
 */
export function windowTextures(): WindowTextures {
  if (windows) return windows;
  const S = 128;
  const [wc, wctx] = canvas2d(S, S);
  const [ec, ectx] = canvas2d(S, S);
  wctx.fillStyle = '#ffffff'; wctx.fillRect(0, 0, S, S);
  ectx.fillStyle = '#000000'; ectx.fillRect(0, 0, S, S);
  for (let r = 0; r < 2; r++) for (let c = 0; c < 2; c++) {
    const x = 14 + c * 64; const y = 12 + r * 64;
    wctx.fillStyle = '#2a3340';
    wctx.fillRect(x, y, 36, 40);
    const lit = hash2(r * 7 + c * 13, 5) > 0.3;
    ectx.fillStyle = lit ? (hash2(r, c) > 0.5 ? '#ffd58a' : '#ffe9c0') : '#101010';
    ectx.fillRect(x + 2, y + 2, 32, 36);
  }
  windows = { wall: texture(wc, true), win: texture(ec, true) };
  return windows;
}

/** Texto centralizado num painel colorido (outdoor, placa de box, arco de largada). */
export function textPanel(text: string, bg: string, fg: string, w = 512, h = 256, accent?: string): THREE.CanvasTexture {
  const [c, ctx] = canvas2d(w, h);
  ctx.fillStyle = bg; ctx.fillRect(0, 0, w, h);
  if (accent) {
    ctx.fillStyle = accent;
    ctx.fillRect(0, 0, w, h * 0.09);
    ctx.fillRect(0, h * 0.91, w, h * 0.09);
  }
  ctx.fillStyle = fg;
  ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
  let size = h * 0.5;
  ctx.font = `900 ${size}px "Segoe UI", Roboto, Arial, sans-serif`;
  while (ctx.measureText(text).width > w * 0.88 && size > 10) { size *= 0.92; ctx.font = `900 ${size}px "Segoe UI", Roboto, Arial, sans-serif`; }
  ctx.fillText(text, w / 2, h / 2);
  return texture(c);
}

/** Placa de curva: chevrons brancos sobre vermelho apontando para `dir` (−1 esquerda, 1 direita). */
export function chevronPanel(dir: -1 | 1): THREE.CanvasTexture {
  const w = 384; const h = 128;
  const [c, ctx] = canvas2d(w, h);
  ctx.fillStyle = '#d9262b'; ctx.fillRect(0, 0, w, h);
  ctx.strokeStyle = '#ffffff'; ctx.lineWidth = 16; ctx.lineJoin = 'miter';
  for (let k = 0; k < 3; k++) {
    const cx = w / 2 + (k - 1) * 110;
    ctx.beginPath();
    ctx.moveTo(cx - dir * 30, 22); ctx.lineTo(cx + dir * 30, h / 2); ctx.lineTo(cx - dir * 30, h - 22);
    ctx.stroke();
  }
  return texture(c);
}

/** Sombra de contato: blob radial escuro. */
export function blobTexture(): THREE.CanvasTexture {
  const S = 128;
  const [c, ctx] = canvas2d(S, S);
  const g = ctx.createRadialGradient(S / 2, S / 2, 6, S / 2, S / 2, S / 2);
  g.addColorStop(0, 'rgba(0,0,0,0.65)'); g.addColorStop(0.55, 'rgba(0,0,0,0.35)'); g.addColorStop(1, 'rgba(0,0,0,0)');
  ctx.fillStyle = g; ctx.fillRect(0, 0, S, S);
  const t = new THREE.CanvasTexture(c);
  return t;
}

/** Brilho radial (chama do nitro, luzes). */
export function glowTexture(color: string): THREE.CanvasTexture {
  const S = 128;
  const [c, ctx] = canvas2d(S, S);
  const g = ctx.createRadialGradient(S / 2, S / 2, 2, S / 2, S / 2, S / 2);
  g.addColorStop(0, '#ffffff'); g.addColorStop(0.25, color); g.addColorStop(1, 'rgba(0,0,0,0)');
  ctx.fillStyle = g; ctx.fillRect(0, 0, S, S);
  return texture(c);
}

/** Etiqueta de jogador: nome numa pílula na cor do assento. */
export function labelTexture(name: string, color: string): THREE.CanvasTexture {
  const w = 256; const h = 80;
  const [c, ctx] = canvas2d(w, h);
  ctx.fillStyle = 'rgba(8,10,16,0.8)';
  ctx.beginPath(); ctx.roundRect(4, 4, w - 8, h - 8, 36); ctx.fill();
  ctx.strokeStyle = color; ctx.lineWidth = 6; ctx.stroke();
  ctx.fillStyle = color; ctx.beginPath(); ctx.arc(34, h / 2, 14, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = '#ffffff'; ctx.textAlign = 'left'; ctx.textBaseline = 'middle';
  ctx.font = '700 38px "Segoe UI", Roboto, Arial, sans-serif';
  ctx.fillText(name.slice(0, 12), 58, h / 2 + 2);
  return texture(c);
}
