// Junta capturas numa grade (para comparar antes × depois):  node tools/tile.mjs saida.png <colunas> a.png b.png ...
import { chromium } from 'playwright';
import { readFileSync } from 'node:fs';
const [out, cols, ...files] = process.argv.slice(2);
if (!out || !files.length) { console.log('uso: node tools/tile.mjs saida.png <colunas> a.png b.png ...'); process.exit(1); }
const b = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium-1194/chrome-linux/chrome' });
const p = await b.newPage({ viewport: { width: 1280, height: 720 } });
const cells = files.map((f) => `<div><img src="data:image/png;base64,${readFileSync(f).toString('base64')}"><span>${f.split('/').pop()}</span></div>`).join('');
await p.setContent(`<style>body{margin:0;background:#111}.g{display:grid;grid-template-columns:repeat(${cols},1fr)}.g div{position:relative}img{width:100%;display:block}span{position:absolute;left:4px;top:2px;color:#fff;font:12px sans-serif;background:#0008;padding:1px 4px}</style><div class=g>${cells}</div>`);
await p.screenshot({ path: out, fullPage: true });
await b.close();
console.log('ok', out);
