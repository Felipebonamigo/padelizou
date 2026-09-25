// Save em arquivo para o Steam Cloud (src/game/cloudsave.ts): quem vale na inicialização e o espelho das gravações.
// O disco é um falso em memória; a regra de verdade dos arquivos (atômico, chaves válidas) está em desktop-storage.test.ts.
import { afterEach, describe, expect, it, vi } from 'vitest';
import { chooseSource, hydrateFromDisk, installSaveMirror, isCloudKey, PENDING_KEY, type KeyedStorage } from '../src/game/cloudsave';
import { ERRORS_KEY } from '../src/game/errors';
import { loadSave, saveSave, SAVE_KEY } from '../src/game/save';
import { writeJson } from '../src/game/settings';

class MemoryStorage implements KeyedStorage {
  readonly data = new Map<string, string>();
  get length(): number { return this.data.size; }
  key(i: number): string | null { return [...this.data.keys()][i] ?? null; }
  getItem(k: string): string | null { return this.data.get(k) ?? null; }
  setItem(k: string, v: string): void { this.data.set(k, v); }
  removeItem(k: string): void { this.data.delete(k); }
  clear(): void { this.data.clear(); }
}

function fakeDisk(initial: Record<string, string> = {}, opts: { failWrites?: boolean; readRejects?: boolean } = {}) {
  const files = { ...initial };
  const writes: string[] = [];
  return {
    files, writes,
    storeReadAll: () => (opts.readRejects ? Promise.reject(new Error('IPC morto')) : Promise.resolve({ ...files })),
    storeWrite: (key: string, json: string) => {
      writes.push(key);
      if (opts.failWrites) return Promise.resolve(false);
      files[key] = json;
      return Promise.resolve(true);
    },
  };
}

const pending = (s: KeyedStorage) => JSON.parse(s.getItem(PENDING_KEY) ?? '[]') as string[];

afterEach(() => { vi.unstubAllGlobals(); });

describe('quem vale para cada chave', () => {
  it('matriz de chooseSource', () => {
    // Arquivo vindo da nuvem (outro PC) vence o localStorage antigo.
    expect(chooseSource('{"a":1}', '{"a":2}', false)).toBe('disk');
    // Iguais: nada a fazer.
    expect(chooseSource('{"a":1}', '{"a":1}', false)).toBe('none');
    // Última gravação local não chegou ao disco: o local vence.
    expect(chooseSource('{"a":3}', '{"a":2}', true)).toBe('local');
    // Sem arquivo (jogador de versão anterior): migra o local para o disco.
    expect(chooseSource('{"a":1}', undefined, false)).toBe('local');
    // Arquivo corrompido nunca vence local válido; local corrompido nunca vence arquivo válido, nem pendente.
    expect(chooseSource('{"a":1}', '{quebrado', false)).toBe('local');
    expect(chooseSource('{quebr', '{"a":2}', true)).toBe('disk');
    // Nada em lugar nenhum.
    expect(chooseSource(null, undefined, false)).toBe('none');
    expect(chooseSource(null, undefined, true)).toBe('none');
  });

  it('só chaves do jogo vão para a nuvem; log de erros e marcação ficam no computador', () => {
    expect(isCloudKey('nitro-crew.save')).toBe(true);
    expect(isCloudKey('nitro-crew.settings')).toBe(true);
    expect(isCloudKey(ERRORS_KEY)).toBe(false);
    expect(isCloudKey(PENDING_KEY)).toBe(false);
    expect(isCloudKey('outra-coisa')).toBe(false);
  });
});

describe('hydrateFromDisk (inicialização no Electron)', () => {
  it('save da nuvem substitui o local; save só local vai para o disco; lixo fica de fora', async () => {
    const storage = new MemoryStorage();
    storage.setItem('nitro-crew.save', '{"racesRun":1}');
    storage.setItem('nitro-crew.settings', '{"language":"en"}');
    storage.setItem(ERRORS_KEY, '[]');
    storage.setItem('outro-app', 'x');
    const disk = fakeDisk({ 'nitro-crew.save': '{"racesRun":9}', 'nitro-crew.errors': '[1]', 'lixo': '{}' });
    const report = await hydrateFromDisk(disk, storage);
    expect(report).toEqual({ fromDisk: ['nitro-crew.save'], toDisk: ['nitro-crew.settings'], failed: [] });
    expect(storage.getItem('nitro-crew.save')).toBe('{"racesRun":9}');
    expect(disk.files['nitro-crew.settings']).toBe('{"language":"en"}');
    expect(storage.getItem(ERRORS_KEY)).toBe('[]');
    expect(disk.writes).toEqual(['nitro-crew.settings']);
    expect(pending(storage)).toEqual([]);
  });

  it('gravação pendente vence o arquivo e é regravada', async () => {
    const storage = new MemoryStorage();
    storage.setItem('nitro-crew.save', '{"racesRun":5}');
    storage.setItem(PENDING_KEY, '["nitro-crew.save"]');
    const disk = fakeDisk({ 'nitro-crew.save': '{"racesRun":4}' });
    const report = await hydrateFromDisk(disk, storage);
    expect(report.toDisk).toEqual(['nitro-crew.save']);
    expect(disk.files['nitro-crew.save']).toBe('{"racesRun":5}');
    expect(storage.getItem('nitro-crew.save')).toBe('{"racesRun":5}');
    expect(pending(storage)).toEqual([]);
  });

  it('disco recusando a gravação: a chave continua pendente (tenta de novo na próxima vez)', async () => {
    const storage = new MemoryStorage();
    storage.setItem('nitro-crew.save', '{"racesRun":2}');
    const report = await hydrateFromDisk(fakeDisk({}, { failWrites: true }), storage);
    expect(report.failed).toEqual(['nitro-crew.save']);
    expect(pending(storage)).toEqual(['nitro-crew.save']);
    expect(storage.getItem('nitro-crew.save')).toBe('{"racesRun":2}');
  });

  it('IPC que falha ou não responde: segue com o localStorage intacto', async () => {
    const storage = new MemoryStorage();
    storage.setItem('nitro-crew.save', '{"racesRun":2}');
    await hydrateFromDisk(fakeDisk({}, { readRejects: true, failWrites: true }), storage);
    expect(storage.getItem('nitro-crew.save')).toBe('{"racesRun":2}');
    const silent = { storeReadAll: () => new Promise<Record<string, string>>(() => undefined), storeWrite: () => new Promise<boolean>(() => undefined) };
    const report = await hydrateFromDisk(silent, storage, 20);
    expect(report).toEqual({ fromDisk: [], toDisk: [], failed: ['nitro-crew.save'] });
    expect(storage.getItem('nitro-crew.save')).toBe('{"racesRun":2}');
  });

  it('resposta de preload desatualizado (não é objeto de textos) é ignorada', async () => {
    const storage = new MemoryStorage();
    const odd = { storeReadAll: () => Promise.resolve({ 'nitro-crew.save': 42 } as unknown as Record<string, string>), storeWrite: () => Promise.resolve(true) };
    expect(await hydrateFromDisk(odd, storage)).toEqual({ fromDisk: [], toDisk: [], failed: [] });
  });

  it('progresso de outro computador chega à sessão pelo loadSave de sempre', async () => {
    const storage = new MemoryStorage();
    vi.stubGlobal('localStorage', storage);
    await hydrateFromDisk(fakeDisk({ [SAVE_KEY]: JSON.stringify({ racesRun: 7, cupsCompleted: ['brasil'] }) }), storage);
    const save = loadSave();
    expect(save.racesRun).toBe(7);
    expect(save.cupsCompleted).toEqual(['brasil']);
  });
});

describe('espelho das gravações', () => {
  it('toda gravação do jogo também vai para o disco; confirmada, deixa de ser pendente', async () => {
    const storage = new MemoryStorage();
    vi.stubGlobal('localStorage', storage);
    const disk = fakeDisk();
    const off = installSaveMirror(disk, storage);
    const save = loadSave();
    save.racesRun = 3;
    saveSave(save);
    expect(pending(storage)).toEqual([SAVE_KEY]); // antes de o disco responder
    await Promise.resolve();
    await Promise.resolve();
    expect(JSON.parse(disk.files[SAVE_KEY]).racesRun).toBe(3);
    expect(pending(storage)).toEqual([]);
    off();
    writeJson('nitro-crew.save', { racesRun: 99 });
    expect(JSON.parse(disk.files[SAVE_KEY]).racesRun).toBe(3); // desligado: só o localStorage
  });

  it('só a confirmação da gravação MAIS RECENTE tira a pendência', async () => {
    const storage = new MemoryStorage();
    vi.stubGlobal('localStorage', storage);
    const resolvers: Array<(ok: boolean) => void> = [];
    const off = installSaveMirror({ storeWrite: () => new Promise<boolean>((r) => { resolvers.push(r); }) }, storage);
    writeJson('nitro-crew.save', { racesRun: 1 });
    writeJson('nitro-crew.save', { racesRun: 2 });
    resolvers[0](true); // a antiga confirmou, a nova ainda não
    await Promise.resolve();
    expect(pending(storage)).toEqual(['nitro-crew.save']);
    resolvers[1](true);
    await Promise.resolve();
    expect(pending(storage)).toEqual([]);
    off();
  });

  it('disco falhando deixa pendente, e o espelho nunca derruba a gravação local', async () => {
    const storage = new MemoryStorage();
    vi.stubGlobal('localStorage', storage);
    const off = installSaveMirror({ storeWrite: () => Promise.reject(new Error('disco cheio')) }, storage);
    expect(writeJson('nitro-crew.save', { racesRun: 4 })).toBe(true);
    await Promise.resolve();
    expect(storage.getItem('nitro-crew.save')).toBe('{"racesRun":4}');
    expect(pending(storage)).toEqual(['nitro-crew.save']);
    off();
    const off2 = installSaveMirror({ storeWrite: () => { throw new Error('preload quebrado'); } }, storage);
    expect(writeJson('nitro-crew.settings', { language: 'pt' })).toBe(true);
    off2();
  });

  it('chave que não é da nuvem não é espelhada', () => {
    const storage = new MemoryStorage();
    vi.stubGlobal('localStorage', storage);
    const writes: string[] = [];
    const off = installSaveMirror({ storeWrite: (k) => { writes.push(k); return Promise.resolve(true); } }, storage);
    writeJson(ERRORS_KEY, []);
    writeJson('outro', 1);
    expect(writes).toEqual([]);
    off();
  });
});
