// Metadados e arquivos do pacote desktop (desktop/package.json → electron-builder). Só lê arquivos: o
// pacote de verdade é conferido por desktop/check-package.mjs e desktop/e2e.mjs depois do build.
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { join } from 'node:path';
import { inflateSync } from 'node:zlib';
import { describe, expect, it } from 'vitest';

const ROOT = join(__dirname, '..');
const DESKTOP = join(ROOT, 'desktop');
const readJson = (file: string): unknown => JSON.parse(readFileSync(file, 'utf8'));

interface FileSet { from: string; to: string; filter?: string[] }
interface DesktopPackage {
  version: string;
  productName?: string;
  author?: string;
  scripts: Record<string, string>;
  build: {
    productName?: string;
    copyright?: string;
    files: Array<string | FileSet>;
    extraFiles?: Array<string | FileSet>;
    directories?: { buildResources?: string };
  };
}
const pkg = readJson(join(DESKTOP, 'package.json')) as DesktopPackage;
const rootPkg = readJson(join(ROOT, 'package.json')) as { version: string };

/** Largura e altura de um PNG (cabeçalho IHDR), ou null se não for PNG. */
function pngSize(b: Buffer): { w: number; h: number } | null {
  const sig = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
  if (b.length < 24 || sig.some((v, i) => b[i] !== v) || b.toString('ascii', 12, 16) !== 'IHDR') return null;
  return { w: b.readUInt32BE(16), h: b.readUInt32BE(20) };
}

describe('pacote desktop: metadados', () => {
  // A versão do relatório de erros vem da raiz; a do .exe (Propriedades → Detalhes) vem do desktop.
  it('versão do desktop igual à da raiz do jogo', () => {
    expect(pkg.version).toBe(rootPkg.version);
  });

  it('nome "Nitro Crew" e copyright com o ano e o autor', () => {
    expect(pkg.productName).toBe('Nitro Crew');
    expect(pkg.build.copyright).toMatch(/^Copyright © 20\d\d /);
    expect(pkg.build.copyright).toContain(pkg.author ?? '?');
  });
});

describe('pacote desktop: arquivos', () => {
  // Defeito de 28/09: o steam_appid.txt ia só para dentro do app.asar, onde o SteamAPI_Init não procura — ele
  // lê o arquivo da pasta de trabalho do processo, que é a do executável. O .exe aberto com dois cliques (fora
  // da Steam) não achava o App ID e o overlay/conquistas não ligavam no teste local.
  it('steam_appid.txt vai para a pasta do executável (extraFiles), não para o asar', () => {
    expect(pkg.build.extraFiles ?? []).toContain('steam_appid.txt');
    expect(pkg.build.files).not.toContain('steam_appid.txt');
  });

  // Ícone provisório (desktop/make-icon.cjs) até a arte da Fase 2; sem ele o electron-builder usa o do Electron.
  it('todo predist:* gera o ícone em build/ (buildResources) sem sobrescrever a arte que já estiver lá', () => {
    expect(pkg.build.directories?.buildResources).toBe('build');
    for (const os of ['win', 'linux', 'mac']) expect(pkg.scripts[`predist:${os}`], os).toContain('make-icon.cjs --if-missing');
  });
});

interface IconModule {
  ICO_SIZES: number[];
  ICNS_TYPES: Array<[string, number]>;
  drawIcon(size: number): Uint8Array;
  encodePng(size: number, rgba: Uint8Array): Buffer;
  encodeIco(pngs: Array<{ size: number; png: Buffer }>): Buffer;
  encodeIcns(entries: Array<{ type: string; png: Buffer }>): Buffer;
  crc32(buf: Uint8Array): number;
}
const icon = createRequire(import.meta.url)('../desktop/make-icon.cjs') as IconModule;

describe('ícone provisório (desktop/make-icon.cjs)', () => {
  it('desenho com cantos transparentes e miolo opaco', () => {
    const rgba = icon.drawIcon(32);
    expect(rgba.length).toBe(32 * 32 * 4);
    expect(rgba[3]).toBe(0); // canto superior esquerdo
    expect(rgba[(16 * 32 + 16) * 4 + 3]).toBe(255);
  });

  it('PNG válido: cabeçalho com o tamanho, CRC certo e os pixels de volta ao descompactar', () => {
    const rgba = icon.drawIcon(20);
    const png = icon.encodePng(20, rgba);
    expect(pngSize(png)).toEqual({ w: 20, h: 20 });
    expect(icon.crc32(Buffer.from('IEND'))).toBe(0xae426082); // CRC conhecido do chunk IEND vazio
    const ihdrCrc = png.readUInt32BE(8 + 8 + 13);
    expect(ihdrCrc).toBe(icon.crc32(png.subarray(12, 12 + 4 + 13)));
    const idatLen = png.readUInt32BE(33);
    const raw = inflateSync(png.subarray(41, 41 + idatLen));
    expect(raw.length).toBe(20 * (20 * 4 + 1));
    expect([...raw.subarray(1, 1 + 20 * 4)]).toEqual([...rgba.subarray(0, 20 * 4)]);
  });

  it('.ico com todos os tamanhos até o 256 que o electron-builder exige, cada um apontando para um PNG', () => {
    expect(icon.ICO_SIZES).toContain(256);
    const pngs = [16, 256].map((size) => ({ size, png: icon.encodePng(size, icon.drawIcon(size)) }));
    const ico = icon.encodeIco(pngs);
    expect(ico.readUInt16LE(2)).toBe(1);
    expect(ico.readUInt16LE(4)).toBe(2);
    expect([ico[6], ico[6 + 16]]).toEqual([16, 0]); // 0 = 256
    for (let i = 0; i < 2; i++) {
      const len = ico.readUInt32LE(6 + i * 16 + 8);
      const off = ico.readUInt32LE(6 + i * 16 + 12);
      expect(ico.subarray(off, off + len).equals(pngs[i].png)).toBe(true);
    }
  });

  it('.icns com o 1024 (ic10) e o tamanho total no cabeçalho', () => {
    expect(icon.ICNS_TYPES).toContainEqual(['ic10', 1024]);
    const png = icon.encodePng(8, icon.drawIcon(8));
    const icns = icon.encodeIcns([{ type: 'ic07', png }]);
    expect(icns.toString('ascii', 0, 4)).toBe('icns');
    expect(icns.readUInt32BE(4)).toBe(icns.length);
    expect(icns.toString('ascii', 8, 12)).toBe('ic07');
    expect(icns.readUInt32BE(12)).toBe(png.length + 8);
  });
});
