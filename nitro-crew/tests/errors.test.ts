// Relatório de erros (src/game/errors.ts): partes puras e o relator com armazenamento, log e telemetria falsos.
import { afterEach, describe, expect, it } from 'vitest';
import {
  createErrorReporter, currentReportText, describeError, ERRORS_KEY, formatReport, GAME_VERSION, installGlobalHandlers,
  logText, pushEntry, reportError, RING_SIZE, sanitizeRing, scrubPaths, setActiveReporter, setTelemetryEndpoint,
  telemetryPayload, type ErrorContext, type ErrorEntry, type ErrorReporter, type ReporterDeps, type StorageLike,
} from '../src/game/errors';
import { isWebGlFailure, LAUNCH_FLAG, splitLaunchFlag } from '../src/errors/fatal';
import { errorCountText } from '../src/errors/options';
import { setLanguage, t } from '../src/i18n';
import pkg from '../package.json';

function memoryStorage(): StorageLike & { data: Map<string, string> } {
  const data = new Map<string, string>();
  return { data, getItem: (k) => data.get(k) ?? null, setItem: (k, v) => { data.set(k, v); } };
}

function entry(over: Partial<ErrorEntry> = {}): ErrorEntry {
  return {
    time: '2026-09-25T12:00:00.000Z', last: '2026-09-25T12:00:00.000Z', version: '0.1.0', kind: 'error',
    message: 'Error: x', stack: '', count: 1, mode: null, track: null, screen: null, ...over,
  };
}

interface Harness { reporter: ErrorReporter; storage: ReturnType<typeof memoryStorage>; logs: string[]; sent: string[]; news: number[]; ctx: ErrorContext; telemetry: { on: boolean } }

function harness(over: Partial<ReporterDeps> = {}, storage = memoryStorage()): Harness {
  const logs: string[] = [];
  const sent: string[] = [];
  const news: number[] = [];
  const ctx: ErrorContext = { mode: 'quick', track: 'copacabana', screen: null };
  const telemetry = { on: false };
  let clock = Date.parse('2026-09-25T12:00:00Z');
  const reporter = createErrorReporter({
    version: '9.9.9',
    now: () => new Date((clock += 1000)),
    context: () => ctx,
    storage,
    logAppend: (text) => { logs.push(text); return Promise.resolve(true); },
    telemetryEnabled: () => telemetry.on,
    send: (_url, body) => { sent.push(body); },
    onNew: (_e, total) => { news.push(total); },
    ...over,
  });
  return { reporter, storage, logs, sent, news, ctx, telemetry };
}

afterEach(() => {
  setTelemetryEndpoint(null);
  setActiveReporter(null);
  setLanguage('pt');
});

describe('versão do jogo', () => {
  it('vem do package.json da raiz', () => {
    expect(GAME_VERSION).toBe(pkg.version);
    expect(GAME_VERSION).toMatch(/^\d+\.\d+\.\d+/);
  });
});

describe('describeError e scrubPaths', () => {
  it('Error: nome + mensagem, pilha sem repetir a primeira linha', () => {
    const e = new TypeError('boom');
    e.stack = 'TypeError: boom\n    at f (file:///home/joao/jogo/resources/app.asar/app/assets/index-abc.js:1:200)';
    const d = describeError(e);
    expect(d.message).toBe('TypeError: boom');
    expect(d.stack).toBe('    at f (app/assets/index-abc.js:1:200)');
  });

  it('texto, objeto, undefined e objeto circular viram mensagem sem lançar', () => {
    expect(describeError('falhou').message).toBe('falhou');
    expect(describeError({ code: 7 }).message).toBe('{"code":7}');
    expect(describeError(undefined).message).toBe('undefined');
    const circ: Record<string, unknown> = {};
    circ.self = circ;
    expect(describeError(circ).message).toBe('[object Object]');
  });

  it('mensagem e pilha enormes são cortadas', () => {
    const e = new Error('m'.repeat(5000));
    e.stack = `Error: ${'m'.repeat(5000)}\n${'    at x\n'.repeat(2000)}`;
    const d = describeError(e);
    expect(d.message.length).toBeLessThanOrEqual(501);
    expect(d.stack.length).toBeLessThanOrEqual(4001);
  });

  it('tira pasta pessoal e caminho de instalação (Windows, Linux, macOS)', () => {
    expect(scrubPaths('at x (file:///C:/Users/Joao/AppData/Local/Nitro%20Crew/resources/app.asar/app/assets/index.js:3:9)')).toBe('at x (app/assets/index.js:3:9)');
    expect(scrubPaths('file:///home/ana/.steam/steam/steamapps/common/Nitro%20Crew/resources/app.asar/app/assets/a.js')).toBe('app/assets/a.js');
    // Caminho padrão da Steam no Windows: parênteses dentro da URL.
    expect(scrubPaths('at g (file:///C:/Program%20Files%20(x86)/Steam/steamapps/common/Nitro%20Crew/resources/app.asar/app/assets/i.js:9:1)')).toBe('at g (app/assets/i.js:9:1)');
    // Navegador: dist servido por arquivo.
    expect(scrubPaths('file:///home/ana/nitro-crew/dist/assets/i.js:1:1')).toBe('assets/i.js:1:1');
    expect(scrubPaths('ENOENT: /home/ana/.config/Nitro Crew/saves')).toBe('ENOENT: /home/~/.config/Nitro Crew/saves');
    expect(scrubPaths('open C:\\Users\\Ana\\AppData\\Roaming\\Nitro Crew')).toBe('open C:\\Users\\~\\AppData\\Roaming\\Nitro Crew');
    expect(scrubPaths('/Users/bia/Library/Application Support')).toBe('/Users/~/Library/Application Support');
    expect(scrubPaths('http://localhost:4186/assets/index.js:1:1')).toBe('http://localhost:4186/assets/index.js:1:1');
  });
});

describe('anel de erros', () => {
  it('o mesmo erro soma no contador e vai para o fim; erro novo entra', () => {
    const ring: ErrorEntry[] = [];
    expect(pushEntry(ring, entry({ message: 'A' })).isNew).toBe(true);
    expect(pushEntry(ring, entry({ message: 'B' })).isNew).toBe(true);
    const again = pushEntry(ring, entry({ message: 'A', last: '2026-09-25T13:00:00.000Z' }));
    expect(again.isNew).toBe(false);
    expect(ring.map((e) => `${e.message}${e.count}`)).toEqual(['B1', 'A2']);
    expect(ring[1].last).toBe('2026-09-25T13:00:00.000Z');
    expect(ring[1].time).toBe('2026-09-25T12:00:00.000Z');
    // Tipo diferente com a mesma mensagem é outro erro.
    expect(pushEntry(ring, entry({ message: 'A', kind: 'loop' })).isNew).toBe(true);
  });

  it(`guarda só os últimos ${RING_SIZE}`, () => {
    const ring: ErrorEntry[] = [];
    for (let i = 0; i < RING_SIZE + 7; i++) pushEntry(ring, entry({ message: `E${i}` }));
    expect(ring.length).toBe(RING_SIZE);
    expect(ring[0].message).toBe('E7');
    expect(ring[RING_SIZE - 1].message).toBe(`E${RING_SIZE + 6}`);
  });

  it('sanitizeRing descarta lixo e completa campos', () => {
    const got = sanitizeRing([null, 3, { time: 'x' }, { time: 't', message: 'm', kind: 'nada' }, { time: 't', message: 'ok', kind: 'loop', count: -2, track: 5 }]);
    expect(got).toEqual([{ time: 't', last: 't', version: '?', kind: 'loop', message: 'ok', stack: '', mode: null, track: null, screen: null, count: 1 }]);
    expect(sanitizeRing('não é lista')).toEqual([]);
  });
});

describe('formato do relatório e do log', () => {
  it('cabeçalho com versão e plataforma; entradas da mais recente para a mais antiga', () => {
    const text = formatReport([entry({ message: 'Velho' }), entry({ message: 'Novo', kind: 'loop', count: 12, mode: 'cup', track: 'interlagos', stack: '    at a\n    at b' })], {
      version: '1.2.3', generated: '2026-09-25T15:00:00.000Z', userAgent: 'Mozilla/5.0 Electron/44', desktop: true, extra: { language: 'pt' },
    });
    expect(text).toContain('version: 1.2.3');
    expect(text).toContain('desktop: yes');
    expect(text).toContain('language: pt');
    expect(text).toContain('errors: 2');
    expect(text.indexOf('Novo')).toBeLessThan(text.indexOf('Velho'));
    expect(text).toContain('#1 · loop · 2026-09-25T12:00:00.000Z · x12');
    expect(text).toContain('where: mode=cup track=interlagos screen=-');
    expect(text).toContain('      at b');
  });

  it('sem erros, diz que não há erros', () => {
    const text = formatReport([], { version: '1', generated: 'g', userAgent: 'ua', desktop: false });
    expect(text).toContain('errors: 0');
    expect(text).toContain('No errors recorded.');
  });

  it('linha de log tem data, versão, tipo e onde', () => {
    expect(logText(entry({ kind: 'rejection', mode: 'quick', track: 't1', count: 3, last: 'L' }))).toBe(
      '[2026-09-25T12:00:00.000Z] v0.1.0 rejection mode=quick track=t1 screen=- x3 (last L)\nError: x\n\n',
    );
  });
});

describe('relator', () => {
  it('registra com versão, data e contexto; grava no localStorage e no log; avisa erro novo', () => {
    const h = harness();
    h.reporter.report(new Error('falhou'), 'loop');
    const [e] = h.reporter.entries();
    expect(e).toMatchObject({ version: '9.9.9', kind: 'loop', message: 'Error: falhou', mode: 'quick', track: 'copacabana', screen: null, count: 1 });
    expect(e.time).toBe('2026-09-25T12:00:01.000Z');
    expect(JSON.parse(h.storage.data.get(ERRORS_KEY) ?? '[]')).toHaveLength(1);
    expect(h.logs).toHaveLength(1);
    expect(h.logs[0]).toContain('v9.9.9 loop mode=quick track=copacabana');
    expect(h.news).toEqual([1]);
  });

  it('erro a cada quadro: uma entrada, e disco/aviso só no 1º, 10º, 100º…', () => {
    const h = harness();
    for (let i = 0; i < 150; i++) h.reporter.report(new Error('todo quadro'), 'loop');
    expect(h.reporter.entries()).toHaveLength(1);
    expect(h.reporter.entries()[0].count).toBe(150);
    expect(h.logs).toHaveLength(3); // 1, 10, 100
    expect(h.logs[2]).toContain('x100');
    expect(h.news).toEqual([1]);
  });

  it('o anel sobrevive ao reinício (lido do localStorage)', () => {
    const storage = memoryStorage();
    harness({}, storage).reporter.report(new Error('antes de fechar'));
    const depois = harness({}, storage);
    expect(depois.reporter.entries().map((e) => e.message)).toEqual(['Error: antes de fechar']);
    depois.reporter.clear();
    expect(harness({}, storage).reporter.entries()).toEqual([]);
  });

  it('nunca lança: contexto, armazenamento, log e aviso quebrados', () => {
    const broken: StorageLike = { getItem: () => { throw new Error('sem acesso'); }, setItem: () => { throw new Error('cota'); } };
    const h = harness({
      context: () => { throw new Error('sessão a meio caminho'); },
      logAppend: () => Promise.reject(new Error('IPC morreu')),
      onNew: () => { throw new Error('DOM sumiu'); },
    }, broken as ReturnType<typeof memoryStorage>);
    expect(() => h.reporter.report(new Error('x'))).not.toThrow();
    expect(h.reporter.entries()[0]).toMatchObject({ message: 'Error: x', mode: null, track: null });
  });

  it('sem armazenamento nem log (navegador restrito) continua na memória', () => {
    const h = harness({ storage: null, logAppend: null });
    h.reporter.report('texto');
    expect(h.reporter.entries()).toHaveLength(1);
  });

  it('texto do relatório usa a versão do relator e o anel atual', () => {
    const h = harness();
    h.reporter.report(new RangeError('fora'));
    const text = h.reporter.text({ userAgent: 'UA', desktop: false });
    expect(text).toContain('version: 9.9.9');
    expect(text).toContain('RangeError: fora');
  });
});

describe('telemetria (opt-in, sem servidor)', () => {
  it('desligada: nada é enviado, mesmo com coletor', () => {
    setTelemetryEndpoint('https://coletor.invalid/e');
    const h = harness();
    h.reporter.report(new Error('x'));
    expect(h.sent).toEqual([]);
  });

  it('ligada mas sem coletor (o caso de hoje): nada é enviado', () => {
    const h = harness();
    h.telemetry.on = true;
    h.reporter.report(new Error('x'));
    expect(h.sent).toEqual([]);
  });

  it('ligada e com coletor: manda só o resumo anônimo, uma vez por erro novo', () => {
    setTelemetryEndpoint('https://coletor.invalid/e');
    const h = harness();
    h.telemetry.on = true;
    const same = new Error('x'); // mesmo ponto de lançamento = mesma pilha = mesmo erro
    h.reporter.report(same);
    h.reporter.report(same);
    expect(h.sent).toHaveLength(1);
    const body = JSON.parse(h.sent[0]) as Record<string, unknown>;
    expect(Object.keys(body).sort()).toEqual(['count', 'kind', 'message', 'mode', 'stack', 'track', 'v']);
  });

  it('o resumo não leva nome de jogador nem caminho e corta a pilha', () => {
    const p = telemetryPayload(entry({ stack: Array.from({ length: 20 }, (_, i) => `at f${i}`).join('\n') }));
    expect(String(p.stack).split('\n')).toHaveLength(8);
    expect(JSON.stringify(p)).not.toMatch(/name|userAgent|screen/);
  });
});

describe('ganchos globais', () => {
  it('error e unhandledrejection da janela chegam ao relator; desligar remove', () => {
    const handlers = new Map<string, (e: unknown) => void>();
    const target = {
      addEventListener: (type: string, fn: (e: unknown) => void) => { handlers.set(type, fn); },
      removeEventListener: (type: string) => { handlers.delete(type); },
    } as unknown as Pick<Window, 'addEventListener' | 'removeEventListener'>;
    const h = harness();
    const off = installGlobalHandlers(target, h.reporter);
    handlers.get('error')?.({ error: new Error('global'), message: 'global' });
    handlers.get('error')?.({ error: null, message: 'Script error.' });
    handlers.get('unhandledrejection')?.({ reason: 'promessa' });
    expect(h.reporter.entries().map((e) => `${e.kind}:${e.message}`)).toEqual(['error:Error: global', 'error:Script error.', 'rejection:promessa']);
    off();
    expect(handlers.size).toBe(0);
  });

  it('reportError sem relator não faz nada; com relator, relata', () => {
    expect(() => reportError(new Error('solto'))).not.toThrow();
    const h = harness();
    setActiveReporter(h.reporter);
    reportError(new Error('laço'), 'loop');
    expect(h.reporter.entries()[0]).toMatchObject({ kind: 'loop', message: 'Error: laço' });
  });

  it('currentReportText sem relator ainda dá o cabeçalho', () => {
    const text = currentReportText({ stage: 'boot' });
    expect(text).toContain(`version: ${GAME_VERSION}`);
    expect(text).toContain('stage: boot');
    expect(text).toContain('desktop: no');
    expect(text).toContain('No errors recorded.');
  });
});

describe('textos da interface', () => {
  it('contagem no singular e no plural, nos dois idiomas', () => {
    expect([0, 1, 3].map(errorCountText)).toEqual(['Nenhum erro', '1 erro', '3 erros']);
    setLanguage('en');
    expect([0, 1, 3].map(errorCountText)).toEqual(['No errors', '1 error', '3 errors']);
  });

  it('a tela fatal reconhece a falha de WebGL do Three.js', () => {
    expect(isWebGlFailure('Error: THREE.WebGLRenderer: Error creating WebGL context.')).toBe(true);
    expect(isWebGlFailure('TypeError: x is undefined')).toBe(false);
  });

  // Defeito visto na captura do pacote Electron: a linha quebrava entre "--" e "ignore-gpu-blocklist", e o
  // jogador copiava a opção errada. A opção sai inteira, num elemento que não quebra.
  it('a opção de inicialização da tela fatal fica inteira (PT e EN)', () => {
    for (const lang of ['pt', 'en'] as const) {
      setLanguage(lang);
      const text = t('errors.fatal.webgl');
      const parts = splitLaunchFlag(text);
      expect(parts, lang).not.toBeNull();
      expect(parts?.flag).toBe(LAUNCH_FLAG);
      expect(`${parts?.before}${parts?.flag}${parts?.after}`).toBe(text);
    }
    expect(splitLaunchFlag('sem opção nenhuma')).toBeNull();
  });
});
