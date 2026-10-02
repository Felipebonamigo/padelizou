// localStorage cheio (cota da origem): o progresso novo não pode sumir calado. No Electron ele vai para o arquivo
// em userData (Steam Cloud) mesmo com o localStorage recusando, a sessão continua lendo o que gravou e a próxima
// abertura lê o arquivo. No navegador (sem arquivo), o descartável abre espaço antes de o jogo desistir, e o
// jogador fica sabendo quando nada foi gravado. O localStorage falso tem cota fixa e lança como o Chromium.
// Cada teste carrega os módulos do zero (`boot`): a memória da sessão (o que o localStorage recusou) é estado de
// módulo, e "a próxima abertura" é justamente uma carga nova.
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createChampionship } from '../src/core/championship';
import { GHOST_SAMPLE_TICKS, makeGhostRecord, traceEnd, type GhostSample } from '../src/game/ghost';
import type { SaveData } from '../src/game/contracts';
import { NEUTRAL_INPUT } from '../src/core/types';
import { TICK_RATE } from '../src/core/constants';
import { human, quickRace, syntheticTrack } from './helpers';

/** localStorage com cota fixa, contada como no Chromium (caracteres de chave + valor); estourou, lança como ele. */
class QuotaStorage {
  readonly data = new Map<string, string>();
  constructor(readonly quota: number) {}
  get used(): number {
    let n = 0;
    for (const [k, v] of this.data) n += k.length + v.length;
    return n;
  }
  get length(): number { return this.data.size; }
  key(i: number): string | null { return [...this.data.keys()][i] ?? null; }
  getItem(k: string): string | null { return this.data.get(k) ?? null; }
  setItem(k: string, v: string): void {
    const old = this.data.get(k);
    const next = this.used - (old === undefined ? 0 : k.length + old.length) + k.length + v.length;
    if (next > this.quota) throw new DOMException(`Setting the value of '${k}' exceeded the quota.`, 'QuotaExceededError');
    this.data.set(k, v);
  }
  removeItem(k: string): void { this.data.delete(k); }
  clear(): void { this.data.clear(); }
  /** Outro site da mesma origem ocupa tudo o que sobrou, menos `spare` caracteres. */
  fill(spare = 0): void {
    const key = 'outro-site';
    this.data.delete(key);
    this.data.set(key, 'x'.repeat(Math.max(0, this.quota - this.used - key.length - spare)));
  }
}

/** O arquivo em userData (desktop/storage.cjs) visto pelo IPC. `hang`: grava, mas a resposta nunca chega (o jogo fechou antes). */
function fakeDisk(initial: Record<string, string> = {}, opts: { hang?: boolean; fail?: boolean } = {}) {
  const files: Record<string, string> = { ...initial };
  return {
    files,
    storeReadAll: () => Promise.resolve({ ...files }),
    storeWrite: (key: string, json: string) => {
      if (opts.fail) return Promise.resolve(false);
      files[key] = json;
      return opts.hang ? new Promise<boolean>(() => undefined) : Promise.resolve(true);
    },
  };
}

const settle = () => new Promise<void>((resolve) => { setTimeout(resolve, 0); });

/** Uma abertura do jogo: módulos carregados do zero, com o localStorage dado. */
async function boot(storage: QuotaStorage) {
  vi.resetModules();
  vi.stubGlobal('localStorage', storage);
  return {
    cloud: await import('../src/game/cloudsave'),
    save: await import('../src/game/save'),
    settings: await import('../src/game/settings'),
    storage: await import('../src/game/storage'),
    raceEnd: await import('../src/game/raceEnd'),
    achievements: await import('../src/game/achievements'),
    ghosts: await import('../src/game/ghost-store'),
    errors: await import('../src/game/errors'),
    notice: await import('../src/game/save-notice'),
  };
}
type Game = Awaited<ReturnType<typeof boot>>;

const SAVE_KEY = 'nitro-crew.save';
const SETTINGS_KEY = 'nitro-crew.settings';

/** Última corrida da Copa Brasil, fechada como a sessão fecha (settleRace no race_over, gravando pelo saveSave). */
function winLastCupRace(g: Game, save: SaveData): void {
  const humans = [human(0)];
  const { state, track } = quickRace({ track: syntheticTrack([{ op: 'straight', length: 300 }]), humans, totalCars: 1, laps: 1, seed: 5 });
  const champ = createChampionship('br_rj', humans);
  champ.raceIndex = 3;
  const r = { state, track, mode: 'cup' as const, humans, telemetry: g.achievements.newTelemetry(), outcome: null };
  const effects = { achievement: () => undefined, hud: () => undefined, persist: () => g.save.saveSave(save) };
  for (let i = 0; i < 60 * 240 && state.phase !== 'finished'; i++) {
    for (const e of g.raceEnd.stepObserved(r, [{ ...NEUTRAL_INPUT, throttle: true }])) {
      if (e.type === 'race_over') g.raceEnd.settleRace(save, r, { champ, difficulty: 'profissional', hudTtl: 3, effects });
    }
  }
}

function saveJson(g: Game, raw: Record<string, unknown>): string {
  return JSON.stringify(g.save.sanitizeSave(raw));
}

/** Fantasma válido pequeno (volta sintética de `n` amostras). */
function ghostRecord(trackId: string, n = 40) {
  const samples: GhostSample[] = [];
  let z = 10;
  for (let i = 0; i < n; i++) { z += 4000 * GHOST_SAMPLE_TICKS / TICK_RATE; samples.push({ z, x: 0, speed: 4000, steer: 0, nitro: false }); }
  const trace = { sampleTicks: GHOST_SAMPLE_TICKS, t0: 1, samples };
  const rec = makeGhostRecord(trackId, traceEnd(trace), trace, { name: 'P1', carId: 'falcao' }, '2026-09-01');
  if (!rec) throw new Error('fantasma');
  return rec;
}

afterEach(() => { vi.unstubAllGlobals(); vi.resetModules(); });

describe('localStorage cheio no Electron', () => {
  it('a corrida e a copa concluída vão para o arquivo, e a próxima abertura lê o arquivo', async () => {
    const storage = new QuotaStorage(20_000);
    let g = await boot(storage);
    const before = saveJson(g, { racesRun: 3 });
    storage.setItem(SAVE_KEY, before);
    storage.fill();
    const disk = fakeDisk({ [SAVE_KEY]: before });
    g.cloud.installSaveMirror(disk, storage);

    const save = g.save.loadSave();
    winLastCupRace(g, save);
    expect(save.cupsCompleted).toEqual(['br_rj']);
    expect(storage.getItem(SAVE_KEY)).toBe(before); // o cenário é mesmo de cota: o localStorage recusou
    await settle();
    const onDisk = JSON.parse(disk.files[SAVE_KEY]) as SaveData;
    expect(onDisk.cupsCompleted).toEqual(['br_rj']);
    expect(onDisk.racesRun).toBe(4);
    expect(onDisk.stats.totals.races).toBe(1);
    // Com o arquivo recebendo, nada a avisar.
    expect(g.storage.saveHealth().lost).toEqual([]);

    // Reabertura: nada na memória da sessão anterior, o localStorage continua cheio.
    g = await boot(storage);
    await g.cloud.hydrateFromDisk(disk, storage);
    const next = g.save.loadSave();
    expect(next.cupsCompleted).toEqual(['br_rj']);
    expect(next.racesRun).toBe(4);
    expect(next.achievements).toContain('COPA_BR_RJ');
  });

  it('abertura com o localStorage cheio: o arquivo mais novo vale para a sessão mesmo sem caber no local', async () => {
    const storage = new QuotaStorage(20_000);
    const g = await boot(storage);
    storage.setItem(SAVE_KEY, saveJson(g, { racesRun: 3 }));
    storage.fill();
    const newer = saveJson(g, { racesRun: 9, cupsCompleted: ['br_rj', 'eua'] });
    const report = await g.cloud.hydrateFromDisk(fakeDisk({ [SAVE_KEY]: newer }), storage);
    expect(report.fromDisk).toEqual([SAVE_KEY]);
    const save = g.save.loadSave();
    expect(save.racesRun).toBe(9);
    expect(save.cupsCompleted).toEqual(['br_rj', 'eua']);
  });

  it('a sessão relê o que acabou de gravar, com ou sem o espelho', async () => {
    for (const mirror of [true, false]) {
      const storage = new QuotaStorage(5_000);
      storage.setItem('nitro-crew.ghosts', '{"ghosts":{}}');
      storage.fill();
      const g = await boot(storage);
      if (mirror) g.cloud.installSaveMirror(fakeDisk(), storage);
      g.settings.writeJson('nitro-crew.ghosts', { ghosts: { copacabana: { ticks: 100 } } });
      expect(g.settings.readJson('nitro-crew.ghosts'), mirror ? 'com espelho' : 'sem espelho').toEqual({ ghosts: { copacabana: { ticks: 100 } } });
    }
  });

  // Pendente quer dizer "o localStorage é mais novo que o arquivo". Com o localStorage recusando a gravação ele é o
  // MAIS VELHO dos dois: marcado como pendente, a abertura seguinte gravava o save antigo por cima do arquivo novo.
  it('gravação que o localStorage recusou não fica pendente: o save antigo nunca volta por cima do arquivo', async () => {
    const storage = new QuotaStorage(20_000);
    let g = await boot(storage);
    const before = saveJson(g, { racesRun: 3 });
    storage.setItem(SAVE_KEY, before);
    storage.fill(g.cloud.PENDING_KEY.length + JSON.stringify([SAVE_KEY]).length); // cabe a marcação, não cabe o save maior
    const disk = fakeDisk({ [SAVE_KEY]: before }, { hang: true });
    g.cloud.installSaveMirror(disk, storage);
    const save = g.save.loadSave();
    save.racesRun = 4;
    save.cupsCompleted.push('br_rj');
    save.bestLaps.copacabana = { ticks: 4321, name: 'P1', carId: 'falcao', date: '2026-09-29T10:00:00.000Z' };
    g.save.saveSave(save);
    expect(storage.getItem(SAVE_KEY)).toBe(before);
    expect(JSON.parse(disk.files[SAVE_KEY]).racesRun).toBe(4);

    g = await boot(storage);
    await g.cloud.hydrateFromDisk(fakeDisk(disk.files), storage);
    expect(g.save.loadSave().racesRun).toBe(4);
  });

  it('localStorage e arquivo recusando: a gravação conta como perdida; sem nada descartado para abrir espaço', async () => {
    const storage = new QuotaStorage(20_000);
    const g = await boot(storage);
    storage.setItem(SAVE_KEY, saveJson(g, { racesRun: 3 }));
    storage.setItem(g.errors.ERRORS_KEY, '[{"time":"2026-09-01","message":"x","kind":"error"}]');
    storage.fill();
    g.storage.setSpaceFreers([(k) => g.errors.dropStoredErrors(storage, k)]);
    g.cloud.installSaveMirror(fakeDisk({}, { fail: true }), storage);
    const save = g.save.loadSave();
    save.cupsCompleted.push('br_rj');
    g.save.saveSave(save);
    expect(g.storage.saveHealth().lost).toEqual([]); // até o disco responder, conta como gravado
    await settle();
    expect(g.storage.saveHealth()).toEqual({ lost: [SAVE_KEY], failures: 1 });
    // Com o arquivo no jogo, o localStorage não é podado: o log de erros continua lá.
    expect(storage.getItem(g.errors.ERRORS_KEY)).not.toBeNull();
    // A sessão segue com o progresso na memória.
    expect(g.save.loadSave().cupsCompleted).toEqual(['br_rj']);
  });

  it('só a resposta da gravação mais recente decide o aviso', async () => {
    const storage = new QuotaStorage(5_000);
    storage.fill();
    const g = await boot(storage);
    const replies: Array<(ok: boolean) => void> = [];
    g.cloud.installSaveMirror({ storeWrite: () => new Promise<boolean>((resolve) => { replies.push(resolve); }) }, storage);
    g.settings.writeJson(SAVE_KEY, { racesRun: 1 });
    g.settings.writeJson(SAVE_KEY, { racesRun: 2 });
    replies[1](true);
    replies[0](false); // a antiga falha depois de a nova gravar: o arquivo tem a nova
    await settle();
    expect(g.storage.saveHealth().lost).toEqual([]);
    g.settings.writeJson(SAVE_KEY, { racesRun: 3 });
    replies[2](false);
    await settle();
    expect(g.storage.saveHealth()).toEqual({ lost: [SAVE_KEY], failures: 1 });
  });
});

describe('localStorage cheio no navegador (sem arquivo)', () => {
  it('abre espaço com o descartável — log de erros, depois o fantasma mais antigo — e nunca com save ou opções', async () => {
    const storage = new QuotaStorage(40_000);
    const g = await boot(storage);
    const settings = '{"language":"en"}';
    storage.setItem(SETTINGS_KEY, settings);
    storage.setItem(SAVE_KEY, saveJson(g, { racesRun: 3 }));
    const errors = JSON.stringify([{ time: '2026-09-01T00:00:00Z', message: 'x'.repeat(400), kind: 'error' }]);
    storage.setItem(g.errors.ERRORS_KEY, errors);
    const store = g.ghosts.emptyGhostStore();
    ['a1', 'b2', 'c3'].forEach((id, i) => g.ghosts.putGhost(store, ghostRecord(id), `2026-09-0${i + 1}T00:00:00Z`));
    storage.setItem(g.ghosts.GHOST_STORE_KEY, JSON.stringify(store));
    const oneGhost = JSON.stringify(store.ghosts.a1).length;
    storage.fill();
    g.storage.setSpaceFreers([(k) => g.errors.dropStoredErrors(storage, k), g.ghosts.dropOldestGhost]);

    // Cresce mais que o log de erros libera, menos que o log mais um fantasma.
    const save = g.save.loadSave();
    save.cupsCompleted.push('c'.repeat(errors.length + Math.floor(oneGhost / 2)));
    expect(g.settings.writeJson(SAVE_KEY, save)).toBe(true);
    expect(JSON.parse(storage.getItem(SAVE_KEY) ?? '{}').cupsCompleted).toEqual(save.cupsCompleted);
    expect(storage.getItem(g.errors.ERRORS_KEY)).toBeNull();
    expect(Object.keys(g.ghosts.loadGhostStore().ghosts).sort()).toEqual(['b2', 'c3']);
    expect(storage.getItem(SETTINGS_KEY)).toBe(settings);
    expect(g.storage.saveHealth().lost).toEqual([]);

    // Maior que tudo o que é descartável: desiste do local sem tocar em save nem opções, e a sessão fica sabendo.
    const saved = storage.getItem(SAVE_KEY);
    save.cupsCompleted.push('d'.repeat(storage.quota / 4));
    expect(g.settings.writeJson(SAVE_KEY, save)).toBe(false);
    expect(storage.getItem(SAVE_KEY)).toBe(saved);
    expect(storage.getItem(SETTINGS_KEY)).toBe(settings);
    expect(g.storage.saveHealth()).toEqual({ lost: [SAVE_KEY], failures: 1 });
    expect(g.save.loadSave().cupsCompleted).toHaveLength(2); // a sessão segue com o progresso na memória

    // O jogador liberou espaço: a próxima gravação leva tudo, e o aviso deixa de valer.
    storage.removeItem('outro-site');
    g.save.saveSave(save);
    expect(JSON.parse(storage.getItem(SAVE_KEY) ?? '{}').cupsCompleted).toHaveLength(2);
    expect(g.storage.saveHealth()).toEqual({ lost: [], failures: 1 });
  });

  it('a loja de fantasmas cheia se poda sozinha (saveGhostStore), sem o descarte comer fantasma a mais', async () => {
    const storage = new QuotaStorage(30_000);
    const g = await boot(storage);
    const store = g.ghosts.emptyGhostStore();
    ['a1', 'b2'].forEach((id, i) => g.ghosts.putGhost(store, ghostRecord(id), `2026-09-0${i + 1}T00:00:00Z`));
    storage.setItem(g.ghosts.GHOST_STORE_KEY, JSON.stringify(store));
    storage.fill();
    g.storage.setSpaceFreers([g.ghosts.dropOldestGhost]);
    g.ghosts.putGhost(store, ghostRecord('c3'), '2026-09-03T00:00:00Z');
    const saved = g.ghosts.saveGhostStore(store);
    expect(saved && Object.keys(saved.ghosts).sort()).toEqual(['b2', 'c3']);
    expect(Object.keys(g.ghosts.loadGhostStore().ghosts).sort()).toEqual(['b2', 'c3']);
    expect(g.storage.saveHealth().lost).toEqual([]);
  });

  it('um erro que não é de cota (armazenamento bloqueado) não descarta nada', async () => {
    const storage = new QuotaStorage(10_000);
    const g = await boot(storage);
    storage.setItem(g.errors.ERRORS_KEY, '[]');
    storage.setItem = () => { throw new DOMException('The operation is insecure.', 'SecurityError'); };
    g.storage.setSpaceFreers([(k) => g.errors.dropStoredErrors(storage, k)]);
    expect(g.settings.writeJson(SAVE_KEY, { racesRun: 1 })).toBe(false);
    expect(storage.getItem(g.errors.ERRORS_KEY)).toBe('[]');
    expect(g.storage.saveHealth().lost).toEqual([SAVE_KEY]);
  });
});

describe('aviso ao jogador', () => {
  it('só no menu principal ou no resultado, uma vez por gravação perdida, e nunca com tudo gravado', async () => {
    const g = await boot(new QuotaStorage(1000));
    let health = { lost: [] as string[], failures: 0 };
    let shown = 0;
    const notice = g.notice.createSaveNotice(() => { shown++; }, () => health);
    notice.update('main');
    expect(shown).toBe(0);
    health = { lost: [SAVE_KEY], failures: 1 };
    notice.update(null); // em corrida
    notice.update('options');
    notice.update('pause');
    expect(shown).toBe(0);
    for (let i = 0; i < 120; i++) notice.update('results');
    notice.update('main');
    expect(shown).toBe(1);
    health = { lost: [SAVE_KEY], failures: 2 }; // a corrida seguinte também não gravou
    notice.update('main');
    expect(shown).toBe(2);
    health = { lost: [], failures: 3 }; // perdeu uma e a seguinte gravou: nada a dizer
    notice.update('results');
    expect(shown).toBe(2);
  });

  it('textos em PT e EN', async () => {
    const g = await boot(new QuotaStorage(1000));
    const i18n = await import('../src/i18n');
    await import('../src/errors/strings');
    for (const lang of ['pt', 'en'] as const) {
      i18n.setLanguage(lang);
      expect(i18n.t('errors.save.title')).not.toBe('errors.save.title');
      expect(i18n.t('errors.save.hint')).not.toBe('errors.save.hint');
    }
    expect(g.notice.saveNoticeDue({ lost: [SAVE_KEY], failures: 1 }, 0, 'main')).toBe(true);
  });
});
