// Folha dos desenhos das pistas (só o traço do alvo, antes de virar pista):
//   CELL=420 npx tsx scripts/art-sheet.ts scratch/art.svg [ids...] && node tools/svg2png.mjs scratch/art.svg scratch/art.png
import { writeFileSync } from 'node:fs';
import { ART } from './track-art';
const [out = 'scratch/art.svg', ...only] = process.argv.slice(2);
const ids = only.length ? only : Object.keys(ART);
const cols = Math.min(6, ids.length); const cell = Number(process.env.CELL ?? 200); const rows = Math.ceil(ids.length / cols);
let body = '';
ids.forEach((id, i) => {
  const a = ART[id]();
  const xs = a.pts.map((p) => p[0]); const ys = a.pts.map((p) => p[1]);
  const minX = Math.min(...xs); const maxX = Math.max(...xs); const minY = Math.min(...ys); const maxY = Math.max(...ys);
  const s = (cell - 50) / Math.max(maxX - minX, maxY - minY);
  const ox = (i % cols) * cell + 25 + (cell - 50 - (maxX - minX) * s) / 2; const oy = Math.floor(i / cols) * cell + 12 + (cell - 50 - (maxY - minY) * s) / 2;
  const d = a.pts.map(([x, y], j) => `${j ? 'L' : 'M'}${(ox + (x - minX) * s).toFixed(1)},${(oy + (y - minY) * s).toFixed(1)}`).join('') + 'Z';
  const sx = ox + (a.start[0] - minX) * s; const sy = oy + (a.start[1] - minY) * s;
  const cx = (i % cols) * cell; const cy = Math.floor(i / cols) * cell;
  body += `<rect x="${cx + 6}" y="${cy + 4}" width="${cell - 12}" height="${cell - 8}" rx="12" fill="#16283a"/>`
    + `<path d="${d}" fill="#ffd54a22" stroke="#ffd54a" stroke-width="3" stroke-linejoin="round"/>`
    + `<circle cx="${sx.toFixed(1)}" cy="${sy.toFixed(1)}" r="4" fill="#ff5a4a"/>`
    + `<text x="${cx + cell / 2}" y="${cy + cell - 14}" font-size="12" font-weight="700" fill="#fff" text-anchor="middle">${a.state} · ${a.what}</text>`;
});
const W = cols * cell; const H = rows * cell;
writeFileSync(out, `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${W} ${H}" width="${W}" height="${H}" font-family="sans-serif"><rect width="${W}" height="${H}" fill="#0b1724"/>${body}</svg>\n`);
console.log('ok', out);
