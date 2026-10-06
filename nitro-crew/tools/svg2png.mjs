// SVG → PNG pelo Chromium (para as folhas de desenho):  node tools/svg2png.mjs entrada.svg saida.png
import { chromium } from 'playwright';
import { readFileSync } from 'node:fs';
const [inp, out] = process.argv.slice(2);
const svg = readFileSync(inp, 'utf8');
const w = Number(/width="(\d+)"/.exec(svg)?.[1] ?? 800); const h = Number(/height="(\d+)"/.exec(svg)?.[1] ?? 600);
const b = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium-1194/chrome-linux/chrome' });
const p = await b.newPage({ viewport: { width: w, height: h } });
await p.setContent(`<body style="margin:0">${svg}</body>`);
await p.screenshot({ path: out });
await b.close();
console.log('ok', out);
