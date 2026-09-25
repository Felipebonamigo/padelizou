// Lado Electron (desktop/): arquivos no userData (storage.cjs), a ponte preload ↔ main ↔ desktop.ts e a
// configuração do empacotamento. Roda em Node puro — o Electron não é carregado; os .cjs são lidos como
// módulos (storage) ou como texto (main/preload/package.json).
import { mkdtempSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { API_FUNCTIONS } from '../src/game/desktop';

interface StorageModule {
  isSaveKey(key: unknown): boolean;
  readAllSaves(dir: string): Record<string, string>;
  writeSave(dir: string, key: unknown, json: unknown): boolean;
  appendLog(dir: string, text: unknown, maxBytes?: number): boolean;
  LOG_MAX_BYTES: number;
  MAX_SAVE_BYTES: number;
  LOG_ENTRY_MAX_CHARS: number;
}

const require = createRequire(import.meta.url);
const storage = require('../desktop/storage.cjs') as StorageModule;
const DESKTOP = join(__dirname, '..', 'desktop');
const read = (file: string) => readFileSync(join(DESKTOP, file), 'utf8');

interface DesktopPackage {
  name: string;
  productName?: string;
  build: { productName?: string; files: Array<string | { from: string; to: string; filter?: string[] }>; linux?: { executableName?: string } };
}
const pkg = JSON.parse(read('package.json')) as DesktopPackage;

describe('saves em arquivo (desktop/storage.cjs)', () => {
  let dir: string;
  beforeEach(() => { dir = mkdtempSync(join(tmpdir(), 'nc-saves-')); });
  afterEach(() => { rmSync(dir, { recursive: true, force: true }); });

  it('grava e lê de volta, um arquivo .json por chave, sem sobrar temporário', () => {
    expect(storage.writeSave(dir, 'nitro-crew.save', '{"racesRun":3}')).toBe(true);
    expect(storage.writeSave(dir, 'nitro-crew.settings', '{"language":"en"}')).toBe(true);
    expect(storage.writeSave(dir, 'nitro-crew.save', '{"racesRun":4}')).toBe(true);
    expect(readdirSync(dir).sort()).toEqual(['nitro-crew.save.json', 'nitro-crew.settings.json']);
    expect(storage.readAllSaves(dir)).toEqual({ 'nitro-crew.save': '{"racesRun":4}', 'nitro-crew.settings': '{"language":"en"}' });
  });

  it('pasta que não existe: leitura vazia, e a gravação cria a pasta', () => {
    const sub = join(dir, 'a', 'saves');
    expect(storage.readAllSaves(sub)).toEqual({});
    expect(storage.writeSave(sub, 'nitro-crew.save', '{}')).toBe(true);
    expect(storage.readAllSaves(sub)).toEqual({ 'nitro-crew.save': '{}' });
  });

  it('recusa chave que escapa da pasta ou não é do jogo, e texto que não é JSON', () => {
    for (const key of ['../x', 'nitro-crew./x', 'nitro-crew.a/b', 'nitro-crew.', 'outra.chave', 'nitro-crew..a', '', 42, null, `nitro-crew.${'a'.repeat(80)}`]) {
      expect(storage.isSaveKey(key), String(key)).toBe(false);
      expect(storage.writeSave(dir, key, '{}'), String(key)).toBe(false);
    }
    expect(storage.writeSave(dir, 'nitro-crew.save', '{quebrado')).toBe(false);
    expect(storage.writeSave(dir, 'nitro-crew.save', 7)).toBe(false);
    expect(storage.writeSave(dir, 'nitro-crew.save', `"${'x'.repeat(storage.MAX_SAVE_BYTES)}"`)).toBe(false);
    expect(readdirSync(dir)).toEqual([]);
    expect(storage.isSaveKey('nitro-crew.career.slot-1')).toBe(true);
  });

  it('leitura ignora arquivo de fora do padrão, temporário e grande demais', () => {
    writeFileSync(join(dir, 'nitro-crew.save.json'), '{"ok":1}');
    writeFileSync(join(dir, 'nitro-crew.save.json.tmp'), '{"meio":1');
    writeFileSync(join(dir, 'leia-me.json'), '{}');
    writeFileSync(join(dir, 'nitro-crew.big.json'), 'x'.repeat(storage.MAX_SAVE_BYTES + 1));
    expect(storage.readAllSaves(dir)).toEqual({ 'nitro-crew.save': '{"ok":1}' });
  });
});

describe('log de erros (desktop/storage.cjs)', () => {
  let dir: string;
  beforeEach(() => { dir = mkdtempSync(join(tmpdir(), 'nc-logs-')); });
  afterEach(() => { rmSync(dir, { recursive: true, force: true }); });

  it('acrescenta e gira para errors.1.log ao passar do limite', () => {
    const line = `${'a'.repeat(99)}\n`; // 100 bytes
    for (let i = 0; i < 5; i++) expect(storage.appendLog(dir, line, 450)).toBe(true);
    expect(statSync(join(dir, 'errors.log')).size).toBe(100); // a 5ª gira: 400 + 100 > 450
    expect(statSync(join(dir, 'errors.1.log')).size).toBe(400);
    for (let i = 0; i < 5; i++) storage.appendLog(dir, line, 450);
    // Só uma geração antiga: disco nunca passa de ~2× o limite.
    expect(readdirSync(dir).sort()).toEqual(['errors.1.log', 'errors.log']);
    expect(statSync(join(dir, 'errors.1.log')).size + statSync(join(dir, 'errors.log')).size).toBeLessThanOrEqual(900);
  });

  it('corta entrada enorme e recusa o que não é texto', () => {
    expect(storage.appendLog(dir, 'x'.repeat(storage.LOG_ENTRY_MAX_CHARS * 3))).toBe(true);
    expect(readFileSync(join(dir, 'errors.log'), 'utf8').length).toBeLessThan(storage.LOG_ENTRY_MAX_CHARS + 10);
    expect(storage.appendLog(dir, '')).toBe(false);
    expect(storage.appendLog(dir, { a: 1 })).toBe(false);
  });
});

describe('ponte preload ↔ main ↔ desktop.ts', () => {
  const preload = read('preload.cjs');
  const main = read('main.cjs');

  it('o preload expõe exatamente as funções de DesktopApi', () => {
    const exposed = [...preload.matchAll(/^\s{2}(\w+): \(/gm)].map((m) => m[1]).sort();
    expect(exposed).toEqual([...API_FUNCTIONS].sort());
  });

  it('todo canal que o preload chama tem handler no main', () => {
    const channels = [...preload.matchAll(/ipcRenderer\.invoke\('([^']+)'/g)].map((m) => m[1]);
    expect(channels.length).toBeGreaterThan(10);
    for (const ch of channels) expect(main, `ipcMain.handle('${ch}')`).toContain(`ipcMain.handle('${ch}'`);
  });
});

describe('empacotamento (desktop/package.json)', () => {
  it('todo .cjs que o main carrega entra no pacote', () => {
    const main = read('main.cjs');
    const required = [...main.matchAll(/require\('\.\/([^']+)'\)/g)].map((m) => m[1]);
    expect(required).toContain('storage.cjs');
    for (const file of required) expect(pkg.build.files, file).toContain(file);
    expect(pkg.build.files).toContain('preload.cjs');
  });

  it('o jogo entra em app/ sem os source maps', () => {
    const game = pkg.build.files.find((f) => typeof f === 'object');
    expect(game).toEqual({ from: '../dist', to: 'app', filter: ['**/*', '!**/*.map'] });
  });

  // Defeito de 25/09: sem productName no topo, o Electron usava o `name` ("nitro-crew-desktop") como pasta
  // de dados — %APPDATA%\nitro-crew-desktop —, e não a "Nitro Crew" que o Steam Cloud documentado espera.
  it('a pasta de dados (userData) é "Nitro Crew", a mesma documentada para o Steam Cloud', () => {
    const electronAppName = pkg.productName ?? pkg.name; // regra do Electron para app.getName()
    expect(electronAppName).toBe('Nitro Crew');
    expect(read('main.cjs')).toMatch(/USER_DATA_DIR_NAME = 'Nitro Crew'[\s\S]*app\.setPath\('userData'[\s\S]*requestSingleInstanceLock/);
  });

  // Defeito de 25/09: o README manda executar release/linux-unpacked/nitro-crew, mas o binário saía "nitro-crew-desktop".
  it('o executável Linux tem o nome que o README e o depósito da Steam usam', () => {
    expect(pkg.build.linux?.executableName).toBe('nitro-crew');
    expect(read('README.md')).toContain('release/linux-unpacked/nitro-crew');
  });
});
