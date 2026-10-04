// Conversor de marcos baixados (docs/ARTE.md, "Marcos baixados"): um .glb realista e texturizado (ex.: galeria CC0
// do Meshy) → o marco do jogo, low-poly de cor chapada, na convenção de landmarks/types.ts. Sobe um servidor do Vite
// numa porta livre, abre tools/convert-landmark.html no Chromium sem janela (as texturas se leem num canvas) e grava
// <saída>/<id>.glb, <id>.png (prévia: original em cima, convertido embaixo, de três lados) e <id>.json (relatório).
//
// Modo PEÇA (--part): o bicho ou a estátua que entra num marco procedural (landmarks/parts.ts: tuiuiu, jacare,
// bufalo, garca, girafa, rena, cavalo, troll, shisa, garimpeiro) → <saída>/<peça>.glb (+ .png, .json), uma parte
// lisa só, frente (a cabeça) em +X, base em y = 0, pegada centrada, metros; o alvo de triângulos é o da peça.
//
// Uso:
//   node tools/convert-landmark.mjs art/raw/tuiuiu.glb --id tuiuiu_ninho [opções]
//   node tools/convert-landmark.mjs art/raw/girafa.glb --part girafa --height 5 [opções]
//   node tools/convert-landmark.mjs --make-synthetic scratch/sintetico.glb [--detail 1] [--kind estatua|boi|girafa]
// Opções:
//   --id <id>               marco que o arquivo substitui (landmarks/index.ts); dá o lugar e a altura padrão
//   --part <peça>           modo peça (parts.ts): o arquivo vira a peça, não o marco inteiro
//   --place near|far|skyline   lugar (só para id fora do registro)
//   --tris <n>              alvo de triângulos (padrão: near 2500, far 4000, skyline 2000; a peça, o dela)
//   --height <m>            altura final (padrão: a do marco procedural do mesmo id; a peça, a dela)
//   --length <m>            comprimento final ao longo da frente (X), no lugar da altura (o jacaré: 2,7)
//   --scale <k>             escala uniforme, no lugar da altura
//   --front +z|-z|+x|-x     eixo do arquivo que é a frente (vai para +X: a pista; na peça, a cabeça); padrão +z (glTF)
//   --up y|z                eixo de cima do arquivo; padrão y
//   --yaw <graus>           giro extra em Y, anti-horário visto de cima
//   --colors <k>            cores da paleta (padrão 12)
//   --palette '#a,#b,…'     paleta fixa (cada face vai para a cor mais perto)
//   --by-light              com --palette: cada face vai para o tom de mesma claridade (bronze, ardósia)
//   --paint 'y>0.62:#1b1b1b'   pinta a região (x, y, z de 0 a 1 na caixa final; x = 1 é a frente); repete; a última vence
//   --merge <d>             tons a menos de d (sRGB 0–1) viram um só (padrão 0.12; 0 desliga)
//   --glow '#ffd23f,…'      cores da paleta que brilham à noite (parte glow; não na peça)
//   --glow-material <regex> materiais (pelo nome) que brilham à noite; material emissivo já brilha (não na peça)
//   --no-emissive           material emissivo NÃO vira luz
//   --out <pasta>           saída (padrão scratch/marcos; a peça, scratch/pecas)
//   --install               aceito pelo validador: copia para src/assets/landmarks/<id>.glb (a peça: …/parts/<peça>.glb)
// Saída 0 = aceito; 2 = recusado pelo validador do jogo (motivos no relatório); 1 = erro.
import { copyFileSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { createServer as netServer } from 'node:net';
import { basename, dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright';
import { createServer } from 'vite';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const RESERVED = new Set([5174, 4174, 5601]); // dev, preview e a do padelizou: nunca

function parse(argv) {
  const o = { input: null, flags: {} };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (!a.startsWith('--')) { o.input = a; continue; }
    const k = a.slice(2);
    if (['install', 'no-emissive', 'help', 'by-light'].includes(k)) { o.flags[k] = true; continue; }
    const v = argv[++i];
    if (v === undefined) throw new Error(`falta o valor de --${k}`);
    if (k === 'paint') (o.flags.paint ??= []).push(v); // repete: uma regra por --paint
    else o.flags[k] = v;
  }
  return o;
}

async function freePort() {
  for (;;) {
    const port = await new Promise((ok, fail) => {
      const s = netServer();
      s.once('error', fail);
      s.listen(0, '127.0.0.1', () => { const p = s.address().port; s.close(() => ok(p)); });
    });
    if (!RESERVED.has(port)) return port;
  }
}

const num = (v, name) => {
  if (v === undefined) return undefined;
  const n = Number(v);
  if (!Number.isFinite(n)) throw new Error(`--${name} precisa de um número (veio "${v}")`);
  return n;
};

async function main() {
  const { input, flags } = parse(process.argv.slice(2));
  if (flags.help || (!input && !flags['make-synthetic'])) {
    const lines = readFileSync(fileURLToPath(import.meta.url), 'utf8').split('\n');
    console.log(lines.slice(0, lines.findIndex((l) => !l.startsWith('//'))).map((l) => l.slice(3)).join('\n'));
    return flags.help ? 0 : 1;
  }
  if (flags.kind && !['estatua', 'boi', 'girafa'].includes(flags.kind)) throw new Error('--kind é estatua, boi ou girafa');
  if (flags['by-light'] && !flags.palette) throw new Error('--by-light pede uma paleta (--palette)');
  const part = flags.part;
  const port = await freePort();
  const server = await createServer({ root: ROOT, logLevel: 'error', clearScreen: false, server: { port, strictPort: true, host: '127.0.0.1' } });
  await server.listen();
  const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
  try {
    const page = await browser.newPage({ viewport: { width: 1200, height: 820 } });
    page.setDefaultTimeout(600000);
    const errors = [];
    page.on('pageerror', (e) => errors.push(e.message));
    page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
    if (input) {
      const bytes = readFileSync(resolve(input));
      await page.route('**/__entrada__', (route) => route.fulfill({ status: 200, body: bytes, contentType: 'application/octet-stream' }));
    }
    await page.goto(`http://127.0.0.1:${port}/tools/convert-landmark.html`, { waitUntil: 'load' });
    await page.waitForFunction(() => window.ready === true);

    if (flags['make-synthetic']) {
      const out = resolve(flags['make-synthetic']);
      const r = await page.evaluate(([d, kind]) => window.makeSynthetic(d, kind), [num(flags.detail, 'detail') ?? 1, flags.kind ?? 'estatua']);
      mkdirSync(dirname(out), { recursive: true });
      writeFileSync(out, Buffer.from(r.glb, 'base64'));
      console.log(`modelo sintético (${flags.kind ?? 'estatua'}): ${out} (${r.triangles.toLocaleString('pt-BR')} triângulos, textura 256 × 256)`);
      return 0;
    }

    // Peça: o nome é o da peça (parts.ts); marco: o id do registro (ou o nome do arquivo).
    const id = part ?? flags.id ?? basename(input).replace(/\.(glb|gltf)$/i, '');
    if (!/\.(glb|gltf)$/i.test(input)) throw new Error('entrada em .glb ou .gltf (autocontido); baixe o GLB da galeria');
    const opts = {
      id, part, place: flags.place, tris: num(flags.tris, 'tris'), height: num(flags.height, 'height'), length: num(flags.length, 'length'), scale: num(flags.scale, 'scale'),
      front: flags.front, up: flags.up, yaw: num(flags.yaw, 'yaw'), colors: num(flags.colors, 'colors'), merge: num(flags.merge, 'merge'),
      palette: flags.palette?.split(',').map((s) => s.trim()).filter(Boolean), byLight: flags['by-light'] ? true : undefined,
      paint: flags.paint,
      glowColors: flags.glow?.split(',').map((s) => s.trim()).filter(Boolean),
      glowMaterials: flags['glow-material'], emissiveGlow: !flags['no-emissive'],
    };
    for (const k of Object.keys(opts)) if (opts[k] === undefined) delete opts[k];
    if (opts.front && !['+z', '-z', '+x', '-x'].includes(opts.front)) throw new Error('--front é +z, -z, +x ou -x');
    if (opts.up && !['y', 'z'].includes(opts.up)) throw new Error('--up é y ou z');
    const t0 = Date.now();
    const { glb, report } = await page.evaluate((o) => window.convert(o), opts);
    const outDir = resolve(flags.out ?? join(ROOT, 'scratch', part ? 'pecas' : 'marcos'));
    mkdirSync(outDir, { recursive: true });
    const glbPath = join(outDir, `${id}.glb`);
    writeFileSync(glbPath, Buffer.from(glb, 'base64'));
    writeFileSync(join(outDir, `${id}.json`), `${JSON.stringify(report, null, 2)}\n`);
    await page.screenshot({ path: join(outDir, `${id}.png`) });
    const pal = report.palette.map((p) => `${p.hex}${p.glow ? '*' : ''} ${(p.share * 100).toFixed(0)}%`).join(', ');
    console.log(`${id} (${part ? 'peça' : report.place}): ${report.trianglesIn.toLocaleString('pt-BR')} → ${report.trianglesOut.toLocaleString('pt-BR')} triângulos (${report.method}), ` +
      `altura ${report.bounds.maxY.toFixed(1)} m, pegada ${(report.bounds.maxX - report.bounds.minX).toFixed(1)} × ${(report.bounds.maxZ - report.bounds.minZ).toFixed(1)} m, ${(report.bytes / 1024).toFixed(0)} KB, ${((Date.now() - t0) / 1000).toFixed(1)} s`);
    console.log(`paleta (${report.palette.length}; * = brilha à noite): ${pal}`);
    for (const w of report.warnings) console.log(`aviso: ${w}`);
    console.log(`gravado: ${glbPath} (+ .png, .json)`);
    if (errors.length) console.log('erros na página:', errors.slice(0, 5).join('\n'));
    if (report.problems.length) {
      console.log(`RECUSADO pelo validador do jogo:\n- ${report.problems.join('\n- ')}`);
      return 2;
    }
    console.log('aceito pelo validador do jogo');
    if (flags.install) {
      const dst = part ? join(ROOT, 'src', 'assets', 'landmarks', 'parts', `${part}.glb`) : join(ROOT, 'src', 'assets', 'landmarks', `${id}.glb`);
      mkdirSync(dirname(dst), { recursive: true });
      copyFileSync(glbPath, dst);
      console.log(`instalado: ${dst}`);
    }
    return 0;
  } finally {
    await browser.close();
    await server.close();
  }
}

main().then((code) => process.exit(code), (e) => { console.error(`erro: ${e instanceof Error ? e.message : String(e)}`); process.exit(1); });
