// Ícone PROVISÓRIO do app (até a arte da Fase 2): desenhado por código, sem dependência — só `zlib` do Node.
// Uma estrada em perspectiva rumo ao sol do entardecer, com a faixa central na cor de destaque do jogo
// (#ff5a36). Gera em build/ o que o electron-builder procura (buildResources):
//   icon.png  1024×1024 (Linux e fonte geral; ele pede ≥ 512)
//   icon.ico  16…256 (Windows; ele exige o 256)
//   icon.icns 16…1024 com as versões @2x (macOS)
// Determinístico: a mesma versão do script gera os mesmos bytes. `npm run icon` (os `predist:*` já chamam).
// Trocar pela arte: pôr os três arquivos finais em build/ e tirar o `npm run icon` dos `predist:*`.
const zlib = require('node:zlib');
const fs = require('node:fs');
const path = require('node:path');

const ICO_SIZES = [16, 24, 32, 48, 64, 128, 256];
/** Tipos do .icns com PNG dentro: [código, lado em pixels]. icp4/icp5 (16/32 em PNG) falham em macOS antigo. */
const ICNS_TYPES = [['ic11', 32], ['ic12', 64], ['ic07', 128], ['ic13', 256], ['ic08', 256], ['ic14', 512], ['ic09', 512], ['ic10', 1024]];

const lerp = (a, b, t) => a + (b - a) * t;
const mix = (c1, c2, t) => [lerp(c1[0], c2[0], t), lerp(c1[1], c2[1], t), lerp(c1[2], c2[2], t)];
const clamp01 = (v) => (v < 0 ? 0 : v > 1 ? 1 : v);

const SKY_TOP = [0x1b, 0x14, 0x3a];
const SKY_LOW = [0xff, 0x7a, 0x5c];
const SUN = [0xff, 0xd1, 0x66];
const GROUND = [0x12, 0x18, 0x26];
const ROAD = [0x2b, 0x2f, 0x3a];
const EDGE = [0xf4, 0xf6, 0xfa];
const ACCENT = [0xff, 0x5a, 0x36];
const HORIZON = 0.47;

/** Cor (RGB) e cobertura (0/1) de um ponto em coordenadas 0..1 do quadrado (y para baixo). */
function sample(x, y) {
  // Quadrado de cantos redondos (raio 22%, como os ícones de app do macOS e do Steam Deck).
  const r = 0.22;
  const cx = x < r ? r : x > 1 - r ? 1 - r : x;
  const cy = y < r ? r : y > 1 - r ? 1 - r : y;
  if ((x - cx) ** 2 + (y - cy) ** 2 > r * r) return null;
  if (y < HORIZON) {
    // Céu em degradê e o sol meio escondido no horizonte.
    const sunD = Math.sqrt((x - 0.5) ** 2 + (y - HORIZON) ** 2);
    if (sunD < 0.2) return SUN;
    return mix(SKY_TOP, SKY_LOW, clamp01(y / HORIZON) ** 1.6);
  }
  // Estrada: trapézio do horizonte (bem fino) até a base (quase a largura toda).
  const t = (y - HORIZON) / (1 - HORIZON); // 0 no horizonte, 1 embaixo
  const half = lerp(0.015, 0.42, t);
  const dx = Math.abs(x - 0.5);
  if (dx > half) return GROUND;
  const edge = lerp(0.002, 0.045, t);
  if (dx > half - edge) return EDGE;
  // Faixa central tracejada: a distância no chão é proporcional a 1/t, então os traços crescem para baixo.
  const lane = lerp(0.002, 0.03, t);
  if (dx < lane && t > 0.08 && (0.9 / t) % 1 < 0.5) return ACCENT;
  return ROAD;
}

/** RGBA (Uint8Array, size×size×4) com 4×4 amostras por pixel para bordas suaves. */
function drawIcon(size) {
  const out = new Uint8Array(size * size * 4);
  const n = 4;
  for (let py = 0; py < size; py++) {
    for (let px = 0; px < size; px++) {
      let r = 0, g = 0, b = 0, a = 0;
      for (let sy = 0; sy < n; sy++) {
        for (let sx = 0; sx < n; sx++) {
          const c = sample((px + (sx + 0.5) / n) / size, (py + (sy + 0.5) / n) / size);
          if (!c) continue;
          r += c[0]; g += c[1]; b += c[2]; a++;
        }
      }
      const i = (py * size + px) * 4;
      if (a > 0) { out[i] = Math.round(r / a); out[i + 1] = Math.round(g / a); out[i + 2] = Math.round(b / a); }
      out[i + 3] = Math.round((a / (n * n)) * 255);
    }
  }
  return out;
}

const CRC_TABLE = (() => {
  const t = new Uint32Array(256);
  for (let i = 0; i < 256; i++) {
    let c = i;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    t[i] = c >>> 0;
  }
  return t;
})();
function crc32(buf) {
  let c = 0xffffffff;
  for (const byte of buf) c = CRC_TABLE[(c ^ byte) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}
function chunk(type, data) {
  const len = Buffer.alloc(4);
  len.writeUInt32BE(data.length);
  const body = Buffer.concat([Buffer.from(type, 'ascii'), data]);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(body));
  return Buffer.concat([len, body, crc]);
}

/** PNG RGBA 8 bits, sem filtro por linha (o deflate dá conta). */
function encodePng(size, rgba) {
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(size, 0);
  ihdr.writeUInt32BE(size, 4);
  ihdr[8] = 8;  // bits por canal
  ihdr[9] = 6;  // RGBA
  const raw = Buffer.alloc(size * (size * 4 + 1));
  for (let y = 0; y < size; y++) Buffer.from(rgba.buffer, rgba.byteOffset + y * size * 4, size * 4).copy(raw, y * (size * 4 + 1) + 1);
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', ihdr),
    chunk('IDAT', zlib.deflateSync(raw, { level: 9 })),
    chunk('IEND', Buffer.alloc(0)),
  ]);
}

/** .ico com PNG dentro de cada entrada (aceito desde o Windows Vista). `pngs`: [{ size, png }]. */
function encodeIco(pngs) {
  const header = Buffer.alloc(6);
  header.writeUInt16LE(0, 0);
  header.writeUInt16LE(1, 2); // 1 = ícone
  header.writeUInt16LE(pngs.length, 4);
  const dir = Buffer.alloc(16 * pngs.length);
  let offset = 6 + dir.length;
  pngs.forEach(({ size, png }, i) => {
    const o = i * 16;
    dir[o] = size >= 256 ? 0 : size; // 0 = 256
    dir[o + 1] = size >= 256 ? 0 : size;
    dir.writeUInt16LE(1, o + 4);  // planos
    dir.writeUInt16LE(32, o + 6); // bits por pixel
    dir.writeUInt32LE(png.length, o + 8);
    dir.writeUInt32LE(offset, o + 12);
    offset += png.length;
  });
  return Buffer.concat([header, dir, ...pngs.map((p) => p.png)]);
}

/** .icns: cabeçalho "icns" + entradas [tipo, tamanho com cabeçalho, PNG]. `entries`: [{ type, png }]. */
function encodeIcns(entries) {
  const parts = entries.map(({ type, png }) => {
    const h = Buffer.alloc(8);
    h.write(type, 0, 'ascii');
    h.writeUInt32BE(png.length + 8, 4);
    return Buffer.concat([h, png]);
  });
  const head = Buffer.alloc(8);
  head.write('icns', 0, 'ascii');
  head.writeUInt32BE(8 + parts.reduce((s, p) => s + p.length, 0), 4);
  return Buffer.concat([head, ...parts]);
}

/** Gera os três arquivos em `dir`; devolve { arquivo: bytes }. */
function writeIcons(dir) {
  const cache = new Map();
  const png = (size) => {
    if (!cache.has(size)) cache.set(size, encodePng(size, drawIcon(size)));
    return cache.get(size);
  };
  const files = {
    'icon.png': png(1024),
    'icon.ico': encodeIco(ICO_SIZES.map((size) => ({ size, png: png(size) }))),
    'icon.icns': encodeIcns(ICNS_TYPES.map(([type, size]) => ({ type, png: png(size) }))),
  };
  fs.mkdirSync(dir, { recursive: true });
  for (const [name, bytes] of Object.entries(files)) fs.writeFileSync(path.join(dir, name), bytes);
  return Object.fromEntries(Object.entries(files).map(([k, v]) => [k, v.length]));
}

module.exports = { ICO_SIZES, ICNS_TYPES, drawIcon, encodePng, encodeIco, encodeIcns, writeIcons, crc32 };

if (require.main === module) {
  // `--if-missing` (usado pelos predist:*): não mexe em build/ se já houver ícone — a arte final posta lá vence.
  const args = process.argv.slice(2);
  const ifMissing = args.includes('--if-missing');
  const dir = args.find((a) => !a.startsWith('--')) ?? path.join(__dirname, 'build');
  if (ifMissing && fs.existsSync(path.join(dir, 'icon.png'))) process.exit(0);
  const sizes = writeIcons(dir);
  console.log(`ícone provisório em ${path.relative(process.cwd(), dir) || '.'}: ${Object.entries(sizes).map(([k, v]) => `${k} ${(v / 1024).toFixed(0)} KB`).join(' · ')}`);
}
