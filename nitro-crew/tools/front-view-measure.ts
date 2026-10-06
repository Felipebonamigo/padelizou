// Vista de frente de um marco em ASCII (tests/front-view.ts) com a fração de água (w), mata (g) e rocha/resto (r):
//   npx tsx tools/front-view-measure.ts cataratas_iguacu
// Serve para escrever o teste de leitura de um marco (tests/landmarks-leitura*.test.ts) antes de mexer no modelo.
import { LANDMARKS } from '../src/render/scenery/landmarks/index';
import { frontView, hsl } from "../tests/front-view";
const id = process.argv[2] ?? 'cataratas_iguacu';
const fv = frontView(LANDMARKS[id].build(), 1);
const cls = (c: any) => { if (!c) return ' '; const { h, s, l } = hsl(c); if (l >= 0.8 || (h > 0.48 && h < 0.64 && l > 0.68 && s < 0.8)) return 'w'; if (h > 0.17 && h < 0.45 && s > 0.15) return 'g'; return 'r'; };
let sil = 0, w = 0; const tones = new Map<string, number>();
for (let r = 0; r < fv.rows; r++) for (let q = 0; q < fv.cols; q++) { const c = fv.at(q, r); const k = cls(c); if (k !== ' ') sil++; if (k === 'w') { w++; const t = c!.getHexString(); tones.set(t, (tones.get(t) ?? 0) + 1); } }
let topG = 0, colsN = 0; for (let q = 0; q < fv.cols; q++) { for (let r = fv.rows - 1; r >= 0; r--) { const k = cls(fv.at(q, r)); if (k !== ' ') { colsN++; if (k === 'g') topG++; break; } } }
console.log(id, `grid ${fv.cols}x${fv.rows}`, 'sil', sil, 'water%', (100 * w / sil).toFixed(1), 'topGreen%', (100 * topG / colsN).toFixed(1));
console.log('tones', [...tones.entries()].sort((a, b) => b[1] - a[1]).slice(0, 8).map(([t, n]) => `${t}:${(100 * n / w).toFixed(0)}%`).join(' '));
const step = Math.max(1, Math.ceil(fv.cols / 150));
for (let r = fv.rows - 1; r >= 0; r -= 2 * step) { let s = ''; for (let q = 0; q < fv.cols; q += step) s += cls(fv.at(q, r)); console.log(s); }
// pé: quarto de baixo da silhueta
let minR = fv.rows, maxR = 0; for (let r = 0; r < fv.rows; r++) for (let q = 0; q < fv.cols; q++) if (cls(fv.at(q, r)) !== ' ') { minR = Math.min(minR, r); maxR = Math.max(maxR, r); }
const footTop = minR + Math.round((maxR - minR) / 4);
let occ = 0, mist = 0; for (let q = 0; q < fv.cols; q++) { let o = false, m = false; for (let r = minR; r <= footTop; r++) { const k = cls(fv.at(q, r)); if (k !== ' ') o = true; if (k === 'w') m = true; } if (o) occ++; if (m) mist++; }
console.log('foot rows', minR, footTop, 'mist cols%', (100 * mist / occ).toFixed(1));
