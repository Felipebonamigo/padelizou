// Relatório de erros (src/game/errors.ts): partes puras e o relator com armazenamento, log e telemetria falsos.
import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  createErrorReporter, currentReportText, describeError, ERRORS_KEY, formatReport, GAME_VERSION, installGlobalHandlers,
  logText, NO_CONTEXT, PERSIST_EVERY_MS, pushEntry, reportError, RING_SIZE, sanitizeRing, scrubPaths, setActiveReporter, setTelemetryEndpoint,
  TELEMETRY_TERMS, telemetryConsented, telemetryPayload, type ErrorContext, type ErrorEntry, type ErrorReporter, type ReporterDeps,
  type StorageLike,
} from '../src/game/errors';
import { createFatalPadNav, fatalPadAction, isWebGlFailure, LAUNCH_FLAG, splitLaunchFlag, type PadLike } from '../src/errors/fatal';
import { errorCountText, optionsFooter } from '../src/errors/options';
import { DEFAULT_SETTINGS, type Settings } from '../src/game/contracts';
import { sanitizeSettings } from '../src/game/settings';
import { setLanguage, t } from '../src/i18n';
import { mapGamepad, NEUTRAL_RAW } from '../src/ui/input';
import type { FocusItem, ScreenApi } from '../src/ui/screens/common';
import { optionsScreen } from '../src/ui/screens/options';
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
  vi.unstubAllGlobals();
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

  // Revisão: conta local do Windows pode ter espaço no nome ("Ana Maria"), e o corte no primeiro espaço deixava
  // o sobrenome no relatório — a política de privacidade promete que o nome de usuário sai.
  it('nome de usuário do Windows com espaço sai inteiro', () => {
    expect(scrubPaths('open C:\\Users\\Ana Maria\\AppData\\Roaming\\Nitro Crew\\saves')).toBe('open C:\\Users\\~\\AppData\\Roaming\\Nitro Crew\\saves');
    expect(scrubPaths("EPERM: operation not permitted, open 'C:\\Users\\Ana Maria de Souza'")).toBe("EPERM: operation not permitted, open 'C:\\Users\\~'");
    expect(scrubPaths('C:/Users/Ana Maria/Documents/x.nitro.json')).toBe('C:/Users/~/Documents/x.nitro.json');
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
    // Nomes só com letras: mensagens que diferem só nos números são o mesmo erro (ver "rajada de erros").
    const name = (i: number) => `E${String.fromCharCode(65 + Math.floor(i / 26))}${String.fromCharCode(65 + (i % 26))}`;
    const ring: ErrorEntry[] = [];
    for (let i = 0; i < RING_SIZE + 7; i++) pushEntry(ring, entry({ message: name(i) }));
    expect(ring.length).toBe(RING_SIZE);
    expect(ring[0].message).toBe(name(7));
    expect(ring[RING_SIZE - 1].message).toBe(name(RING_SIZE + 6));
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

// ───────────────────────────── Revisão independente (achados reproduzidos) ─────────────────────────────

/** O erro "do jogo": sempre o mesmo ponto de lançamento, logo a mesma pilha a cada chamada. */
function sameThrowSite(): Error { return new Error('mesmo erro'); }

interface SessionRun { version: string; ctx: ErrorContext; at: string; kind: 'loop' | 'fatal' }

/** Uma "abertura do jogo" por item: relator novo lendo o mesmo localStorage, e o erro pelo mesmo caminho. */
function runSessions(storage: StorageLike, runs: readonly SessionRun[]): Array<{ reporter: ErrorReporter; logs: string[] }> {
  const out: Array<{ reporter: ErrorReporter; logs: string[] }> = [];
  for (const run of runs) {
    const logs: string[] = [];
    let clock = Date.parse(run.at);
    const reporter = createErrorReporter({
      version: run.version, now: () => new Date((clock += 1000)), context: () => run.ctx, storage,
      logAppend: (text) => { logs.push(text); return Promise.resolve(true); }, telemetryEnabled: () => false,
    });
    reporter.report(sameThrowSite(), run.kind);
    out.push({ reporter, logs });
  }
  return out;
}

const savedRing = (s: ReturnType<typeof memoryStorage>) => JSON.parse(s.data.get(ERRORS_KEY) ?? '[]') as ErrorEntry[];

describe('erro que volta em outra sessão', () => {
  // Achado 1: o erro repetido só voltava ao disco no 10º/100º, e o contador recomeçava do valor salvo (1) a
  // cada abertura — nunca chegava a 10. O relatório mostrava a versão e a pista da PRIMEIRA vez: depois de uma
  // atualização, um defeito que continua parecia corrigido.
  it('a 2ª sessão grava de novo, com a versão, o modo, a pista e o contador de agora', () => {
    const storage = memoryStorage();
    const [, s2] = runSessions(storage, [
      { version: '0.1.0', ctx: { mode: 'quick', track: 'copacabana', screen: null }, at: '2026-09-20T10:00:00Z', kind: 'loop' },
      { version: '0.2.0', ctx: { mode: 'cup', track: 'monaco_noite', screen: null }, at: '2026-09-25T10:00:00Z', kind: 'loop' },
    ]);
    const expected = { version: '0.2.0', mode: 'cup', track: 'monaco_noite', count: 2, time: '2026-09-20T10:00:01.000Z', last: '2026-09-25T10:00:01.000Z' };
    expect(s2.reporter.entries()[0]).toMatchObject(expected);
    expect(savedRing(storage)[0]).toMatchObject(expected);
    expect(s2.logs).toHaveLength(1);
    expect(s2.logs[0]).toContain('v0.2.0 loop mode=cup track=monaco_noite');
    expect(s2.logs[0]).toContain('x2');
  });

  it('erro fatal em toda abertura: uma linha de log por abertura e o contador certo no localStorage', () => {
    const storage = memoryStorage();
    const runs = [20, 21, 22, 23, 24].map((d): SessionRun => ({ version: '0.1.0', ctx: { ...NO_CONTEXT }, at: `2026-09-${d}T10:00:00Z`, kind: 'fatal' }));
    const sessions = runSessions(storage, runs);
    expect(sessions.flatMap((s) => s.logs)).toHaveLength(5);
    expect(savedRing(storage)[0].count).toBe(5);
    expect(savedRing(storage)[0].last).toBe('2026-09-24T10:00:01.000Z');
  });

  it('erro repetido na mesma sessão: o contador chega ao localStorage a cada poucos segundos, e flush() grava na hora', () => {
    const storage = memoryStorage();
    let clock = Date.parse('2026-09-25T12:00:00Z');
    const r = createErrorReporter({ version: '1', now: () => new Date(clock), context: () => NO_CONTEXT, storage, logAppend: null, telemetryEnabled: () => false });
    const err = new Error('repetido');
    for (let i = 0; i < 7; i++) { clock += 1000; r.report(err, 'loop'); }
    expect(savedRing(storage)[0].count).toBeGreaterThanOrEqual(6);
    clock += 100;
    r.report(err, 'loop');
    r.flush();
    expect(savedRing(storage)[0].count).toBe(8);
  });
});

describe('rajada de erros', () => {
  // Achado 4: erro por quadro com mensagem que muda (índice, valor) virava erro NOVO a cada quadro — anel
  // inteiro no localStorage, IPC de log e aviso 60 vezes por segundo.
  it('mensagem que só muda nos números, na mesma pilha, é o mesmo erro', () => {
    const ring: ErrorEntry[] = [];
    const at = '    at f (app/assets/i.js:1:2)';
    expect(pushEntry(ring, entry({ message: 'RangeError: índice 12 fora da pista', stack: at })).isNew).toBe(true);
    expect(pushEntry(ring, entry({ message: 'RangeError: índice 13 fora da pista', stack: at })).isNew).toBe(false);
    expect(ring).toHaveLength(1);
    expect(ring[0].count).toBe(2);
    // Outra pilha continua sendo outro erro.
    expect(pushEntry(ring, entry({ message: 'RangeError: índice 14 fora da pista', stack: '    at g (app/assets/i.js:9:9)' })).isNew).toBe(true);
  });

  it('600 erros diferentes em 10 s: disco, log e aviso ficam limitados, e nada se perde da memória', () => {
    const storage = memoryStorage();
    let writes = 0;
    const counting: StorageLike = { getItem: (k) => storage.getItem(k), setItem: (k, v) => { writes++; storage.setItem(k, v); } };
    const logs: string[] = [];
    let toasts = 0;
    let clock = Date.parse('2026-09-25T12:00:00Z');
    const r = createErrorReporter({
      version: '1', now: () => new Date(clock), context: () => NO_CONTEXT, storage: counting,
      logAppend: (text) => { logs.push(text); return Promise.resolve(true); }, telemetryEnabled: () => false, onNew: () => { toasts++; },
    });
    // Só letras: cada mensagem é mesmo outro erro (números seriam agrupados).
    const name = (i: number) => `Erro ${String.fromCharCode(65 + (i % 26))}${String.fromCharCode(65 + (Math.floor(i / 26) % 26))}`;
    for (let frame = 0; frame < 600; frame++) { clock += 16; r.report(new Error(name(frame)), 'loop'); }
    expect(writes).toBeLessThanOrEqual(15);
    expect(logs.length).toBeLessThanOrEqual(15);
    expect(toasts).toBeLessThanOrEqual(10);
    expect(r.entries()).toHaveLength(RING_SIZE);
    expect(logs.some((l) => /not logged one by one/.test(l))).toBe(true);
    r.flush();
    expect(savedRing(storage)).toEqual(r.entries());
  });

  // A rajada acaba e não vem mais erro: sem um passo agendado, o localStorage ficava para trás até o pagehide —
  // e uma queda do processo da página (render-process-gone) não dispara pagehide.
  it('depois da rajada, um passo agendado grava o que ficou só na memória', () => {
    const storage = memoryStorage();
    const scheduled: Array<{ fn: () => void; ms: number }> = [];
    let clock = Date.parse('2026-09-25T12:00:00Z');
    const r = createErrorReporter({
      version: '1', now: () => new Date(clock), context: () => NO_CONTEXT, storage, logAppend: null, telemetryEnabled: () => false,
      schedule: (fn, ms) => { scheduled.push({ fn, ms }); },
    });
    const name = (i: number) => `Erro ${String.fromCharCode(65 + (i % 26))}${String.fromCharCode(65 + (Math.floor(i / 26) % 26))}`;
    for (let i = 0; i < 30; i++) { clock += 10; r.report(new Error(name(i)), 'loop'); }
    expect(savedRing(storage)).toHaveLength(10);
    expect(scheduled).toHaveLength(1); // um só, por mais erros que venham
    expect(scheduled[0].ms).toBe(PERSIST_EVERY_MS);
    clock += PERSIST_EVERY_MS;
    scheduled[0].fn();
    expect(savedRing(storage)).toEqual(r.entries());
    expect(savedRing(storage)).toHaveLength(30);
  });
});

describe('consentimento da telemetria', () => {
  // Achado 6: um booleano não distingue quem ligou quando a opção "não envia nada" de quem ligou sabendo do
  // envio; a política promete pedir de novo quando o envio for ligado.
  it('vale só para os termos em vigor', () => {
    expect(telemetryConsented(0)).toBe(false);
    expect(telemetryConsented(TELEMETRY_TERMS)).toBe(true);
    expect(telemetryConsented(1, 2)).toBe(false); // ligou nos termos 1 ("não envia nada"); os termos 2 ligam o envio
    expect(telemetryConsented(2, 2)).toBe(true);
  });

  it('vem desligada; o booleano antigo não vale como consentimento', () => {
    expect(DEFAULT_SETTINGS.telemetryConsent).toBe(0);
    expect(sanitizeSettings({ telemetry: true }).telemetryConsent).toBe(0);
    expect(sanitizeSettings({ telemetryConsent: TELEMETRY_TERMS }).telemetryConsent).toBe(TELEMETRY_TERMS);
    expect(sanitizeSettings({ telemetryConsent: -3 }).telemetryConsent).toBe(0);
    expect(sanitizeSettings({ telemetryConsent: 'sim' }).telemetryConsent).toBe(0);
  });
});

// ───────────────────────────── DOM falso (o vitest roda em Node, sem jsdom) ─────────────────────────────

class FakeEl {
  children: unknown[] = [];
  className = '';
  textContent = '';
  title = '';
  innerHTML = '';
  isConnected = false;
  style: Record<string, string> = {};
  dataset: Record<string, string> = {};
  attrs: Record<string, string> = {};
  listeners: Record<string, Array<() => void>> = {};
  content?: { firstElementChild: FakeEl };
  private readonly classes = new Set<string>();
  classList = {
    add: (...c: string[]) => { for (const x of c) this.classes.add(x); },
    remove: (...c: string[]) => { for (const x of c) this.classes.delete(x); },
    toggle: (c: string, force?: boolean) => { const on = force ?? !this.classes.has(c); if (on) this.classes.add(c); else this.classes.delete(c); return on; },
    contains: (c: string) => this.classes.has(c),
  };
  constructor(readonly tag: string) {}
  setAttribute(k: string, v: string): void { this.attrs[k] = v; }
  getAttribute(k: string): string | null { return this.attrs[k] ?? null; }
  appendChild<T>(c: T): T { this.children.push(c); return c; }
  append(...c: unknown[]): void { this.children.push(...c); }
  addEventListener(type: string, fn: () => void): void { (this.listeners[type] ??= []).push(fn); }
  removeEventListener(): void { /* não usado */ }
}

function installFakeDocument(): void {
  vi.stubGlobal('document', {
    createElement: (tag: string) => {
      const el = new FakeEl(tag);
      if (tag === 'template') el.content = { firstElementChild: new FakeEl('svg') };
      return el;
    },
    createElementNS: (_ns: string, tag: string) => new FakeEl(tag),
    createTextNode: (text: string) => ({ text }),
  });
}

/** Relator de verdade embrulhado para contar assinaturas vivas. */
function countingReporter(): { reporter: ErrorReporter; live: () => number } {
  const real = harness().reporter;
  let live = 0;
  const reporter: ErrorReporter = { ...real, subscribe(fn) { live++; const off = real.subscribe(fn); return () => { live--; off(); }; } };
  return { reporter, live: () => live };
}

function fakeScreenApi(settings: Settings): ScreenApi {
  return {
    ctx: { settings, isDesktop: false, audio: { musicList: () => [] } },
    lobby: { mode: 'quick', versus: false, seats: [null, null, null, null] },
    go: () => undefined, back: () => undefined, emit: () => undefined, sfx: () => undefined, refresh: () => undefined,
  } as unknown as ScreenApi;
}

describe('Opções: itens do relatório de erros', () => {
  // Achado 3: cada abertura de Opções (e cada troca de idioma) deixava uma assinatura viva segurando a tela
  // inteira desmontada até o próximo erro.
  it('fechar a tela desfaz a assinatura do relator', () => {
    installFakeDocument();
    const { reporter, live } = countingReporter();
    setActiveReporter(reporter);
    const back: FocusItem = { el: new FakeEl('button') as unknown as HTMLElement, activate: () => undefined };
    for (let i = 0; i < 5; i++) optionsFooter(fakeScreenApi(structuredClone(DEFAULT_SETTINGS) as Settings), () => undefined, back).destroy();
    expect(live()).toBe(0);
  });

  it('a tela de Opções devolve destroy, que o menu chama ao desmontar', () => {
    installFakeDocument();
    const { reporter, live } = countingReporter();
    setActiveReporter(reporter);
    const screen = optionsScreen(fakeScreenApi(structuredClone(DEFAULT_SETTINGS) as Settings));
    expect(live()).toBe(1);
    screen.destroy?.();
    expect(live()).toBe(0);
  });

  it('ligar a telemetria grava o consentimento dos termos atuais; desligar zera', () => {
    installFakeDocument();
    const settings = structuredClone(DEFAULT_SETTINGS) as Settings;
    let commits = 0;
    const back: FocusItem = { el: new FakeEl('button') as unknown as HTMLElement, activate: () => undefined };
    const footer = optionsFooter(fakeScreenApi(settings), () => { commits++; }, back);
    const telemetry = footer.items[2];
    telemetry.activate?.();
    expect(settings.telemetryConsent).toBe(TELEMETRY_TERMS);
    telemetry.activate?.();
    expect(settings.telemetryConsent).toBe(0);
    expect(commits).toBe(2);
    footer.destroy();
  });
});

describe('tela de erro fatal com controle', () => {
  // Achado 7: a tela fatal só tinha botões de DOM; sem a sessão, ninguém lia a Gamepad API, e quem joga só
  // de controle (sofá, Steam Deck) não copiava o relatório nem tentava de novo.
  const raw = (pressed: number[] = [], axes: number[] = [0, 0]) => mapGamepad(Array.from({ length: 17 }, (_, i) => pressed.includes(i)), axes);

  it('direcional e analógico movem, A ou Start apertam, segurar não repete', () => {
    expect(fatalPadAction(NEUTRAL_RAW, raw([15]))).toBe('next');
    expect(fatalPadAction(NEUTRAL_RAW, raw([13]))).toBe('next');
    expect(fatalPadAction(NEUTRAL_RAW, raw([14]))).toBe('prev');
    expect(fatalPadAction(NEUTRAL_RAW, raw([12]))).toBe('prev');
    expect(fatalPadAction(NEUTRAL_RAW, raw([], [0.9, 0]))).toBe('next');
    expect(fatalPadAction(NEUTRAL_RAW, raw([0]))).toBe('activate');
    expect(fatalPadAction(NEUTRAL_RAW, raw([9]))).toBe('activate');
    expect(fatalPadAction(raw([0]), raw([0]))).toBeNull();
    expect(fatalPadAction(NEUTRAL_RAW, NEUTRAL_RAW)).toBeNull();
  });

  it('o controle anda entre Copiar e Tentar de novo e aperta o botão em foco', () => {
    const clicks: string[] = [];
    let focused = '';
    const button = (name: string) => {
      const on: Record<string, () => void> = {};
      return {
        addEventListener: (type: string, fn: () => void) => { on[type] = fn; },
        focus: () => { focused = name; on.focus?.(); },
        click: () => { clicks.push(name); },
      };
    };
    const copy = button('copy');
    const retry = button('retry');
    let pad: PadLike = { connected: true, buttons: [], axes: [0, 0] };
    const press = (...ids: number[]) => {
      pad = { connected: true, axes: [0, 0], buttons: Array.from({ length: 17 }, (_, i) => ({ pressed: ids.includes(i), value: ids.includes(i) ? 1 : 0 })) };
    };
    const nav = createFatalPadNav([copy, retry] as unknown as HTMLElement[], () => [null, pad]); // controle no índice 1
    press(0); nav.poll(); nav.poll(); press(); nav.poll();
    expect(clicks).toEqual(['copy']);
    press(15); nav.poll(); press(); nav.poll();
    expect(focused).toBe('retry');
    press(0); nav.poll();
    expect(clicks).toEqual(['copy', 'retry']);
    // Mouse ou Tab mudando o foco também vale para o controle.
    press(); nav.poll();
    copy.focus();
    press(9); nav.poll();
    expect(clicks).toEqual(['copy', 'retry', 'copy']);
  });
});
