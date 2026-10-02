// Desenha o contorno de uma pista (o mesmo `trackOutline` do minimapa) em ASCII no terminal, ou em SVG.
// Serve para conferir a forma de um traçado sem abrir o jogo — a cuia gaúcha nasceu iterando com ele.
// Uso: npx tsx scripts/track-outline.ts <pista> [colunas=60] [--svg arquivo.svg]
import { writeFileSync } from 'node:fs';
import { getTrack } from '../src/core/track';
import { trackOutline } from '../src/render/minimap';

const id = process.argv[2] ?? 'cuia_gaucha';
const cols = Number(process.argv[3] && !process.argv[3].startsWith('--') ? process.argv[3] : 60);
const svgAt = process.argv.indexOf('--svg');
const track = getTrack(id);
const pts = trackOutline(track, 100);

if (svgAt > 0) {
  const file = process.argv[svgAt + 1] ?? `${id}.svg`;
  const d = pts.map(([x, y], i) => `${i === 0 ? 'M' : 'L'}${x.toFixed(2)},${y.toFixed(2)}`).join(' ') + ' Z';
  const [sx, sy] = pts[0];
  writeFileSync(file, `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100" width="400" height="400">`
    + `<rect width="100" height="100" fill="#123"/><path d="${d}" fill="none" stroke="#fd3" stroke-width="2"/>`
    + `<circle cx="${sx}" cy="${sy}" r="2.5" fill="#f44"/></svg>\n`);
  console.log(`SVG em ${file}`);
}

// ASCII: o caractere é ~2× mais alto que largo, então a grade tem metade das linhas.
const rows = Math.round(cols / 2);
const grid = Array.from({ length: rows }, () => new Array<string>(cols).fill(' '));
const cell = (x: number, y: number): [number, number] => [
  Math.min(rows - 1, Math.max(0, Math.floor((y / 100) * rows))), Math.min(cols - 1, Math.max(0, Math.floor((x / 100) * cols))),
];
for (let i = 0; i < pts.length; i++) {
  const [x0, y0] = pts[i];
  const [x1, y1] = pts[(i + 1) % pts.length];
  for (let k = 0; k <= 8; k++) {
    const [r, c] = cell(x0 + ((x1 - x0) * k) / 8, y0 + ((y1 - y0) * k) / 8);
    grid[r][c] = '#';
  }
}
const [sr, sc] = cell(pts[0][0], pts[0][1]);
grid[sr][sc] = 'S';
console.log(`${id}: ${track.segments.length} segmentos (S = largada)`);
console.log(grid.map((r) => r.join('').replace(/\s+$/, '')).join('\n'));
