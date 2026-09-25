// Relatório de erros: guarda os últimos 50 erros — com versão do jogo, data, modo e pista do momento — para o
// jogador copiar em Opções › "Copiar relatório de erros" e mandar ao desenvolvedor.
// Fontes: window 'error', promessas rejeitadas sem tratamento e o catch do laço da sessão (`reportError(err, 'loop')`).
// Onde fica: localStorage (`nitro-crew.errors`, só neste computador — fora do Steam Cloud) e, no Electron, também
// `<userData>/logs/errors.log` pelo IPC `logAppend` (limite de tamanho e rotação em desktop/storage.cjs).
// Nunca lança e nunca trava o jogo:
//   • um erro que se repete (mesma pilha; números da mensagem não contam) vira UMA entrada com contador, que
//     guarda a 1ª vez (`time`) e o contexto da ÚLTIMA — versão, data, modo e pista de agora;
//   • log e localStorage recebem o erro novo, a 1ª repetição em cada sessão (um defeito de toda abertura aparece
//     a cada abertura) e o contador em 10, 100, 1000…; fora isso, o contador vai ao localStorage no máximo a
//     cada PERSIST_EVERY_MS — um passo agendado (`schedule`) grava o que sobrou quando os erros param, e
//     `flush()` (a página vai fechar) grava na hora;
//   • rajada de erros diferentes: no máximo LOUD_MAX gravações completas por LOUD_WINDOW_MS; o resto fica na
//     memória (e no localStorage pelo passo acima), com uma linha no log dizendo quantos não foram um a um.
// Privacidade: caminhos de arquivo são reduzidos a `app/assets/…` e pastas pessoais viram `~` (scrubPaths).
// Telemetria: opt-in (Settings.telemetryConsent, desligada por padrão) e SEM servidor hoje — `telemetryEndpoint`
// é nulo, então nada sai do computador. Quando houver coletor, `setTelemetryEndpoint(url)` liga o envio de
// `telemetryPayload(entry)` (sem nomes de jogador, sem caminhos) para quem aceitou os termos EM VIGOR
// (TELEMETRY_TERMS — suba o número no mesmo commit que ligar o envio). Ver docs/legal/PRIVACIDADE.md.
import { version as packageVersion } from '../../package.json';
import { getDesktop, isDesktop } from './desktop';

export const GAME_VERSION: string = packageVersion;
export const ERRORS_KEY = 'nitro-crew.errors';
export const RING_SIZE = 50;
const MESSAGE_MAX = 500;
const STACK_MAX = 4000;
/** Contadores em que um erro repetido volta a ser gravado (log e localStorage). */
const REPEAT_MILESTONES: ReadonlySet<number> = new Set([10, 100, 1000, 10000]);
/** O que só mudou na memória (contador de erro repetido, rajada) vai ao localStorage no máximo a cada isto. */
export const PERSIST_EVERY_MS = 5000;
/** Rajada: no máximo LOUD_MAX gravações completas (localStorage + log + aviso) a cada LOUD_WINDOW_MS. */
export const LOUD_MAX = 10;
export const LOUD_WINDOW_MS = 10_000;

export type ErrorKind = 'error' | 'rejection' | 'loop' | 'fatal';
const KINDS: readonly ErrorKind[] = ['error', 'rejection', 'loop', 'fatal'];

/** Onde o jogador estava: modo da corrida, pista e tela de menu aberta (null = nenhum). */
export interface ErrorContext {
  mode: string | null;
  track: string | null;
  screen: string | null;
}

export interface ErrorEntry extends ErrorContext {
  /** Primeira vez que apareceu (ISO). */
  time: string;
  /** Última vez (ISO); igual a `time` quando `count` é 1. */
  last: string;
  version: string;
  kind: ErrorKind;
  message: string;
  stack: string;
  count: number;
}

export const NO_CONTEXT: Readonly<ErrorContext> = Object.freeze({ mode: null, track: null, screen: null });

/** O pedaço do Storage que o módulo usa (o localStorage, ou um falso nos testes). */
export interface StorageLike {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
}

// ───────────────────────────── Funções puras ─────────────────────────────

function clip(s: string, max: number): string {
  return s.length > max ? `${s.slice(0, max)}…` : s;
}

/**
 * Tira do texto o que identifica o computador: `file:///…/app.asar/app/assets/x.js` vira `app/assets/x.js`,
 * e `/home/<nome>`, `/Users/<nome>`, `C:\Users\<nome>` viram `…/~`.
 */
export function scrubPaths(text: string): string {
  return text
    // \S e não [^()]: o caminho padrão da Steam no Windows tem parênteses ("Program%20Files%20(x86)").
    .replace(/file:\/\/\S*?\/((?:app\/)?assets\/)/g, '$1')
    // Linux e macOS: nome de usuário sem espaço (o da pasta pessoal é o nome curto).
    .replace(/(\/home\/|\/Users\/)[^\\/\s'"()]+/g, '$1~')
    // Windows: a pasta do perfil pode ter espaço ("Ana Maria"); vai até a próxima barra, aspas, parêntese ou fim
    // da linha — sem barra depois, corta o resto da linha (melhor sobrar de menos que vazar o sobrenome).
    .replace(/([A-Za-z]:[\\/]Users[\\/])[^\\/:*?"<>|'()\r\n]+/g, '$1~');
}

/** Mensagem e pilha de qualquer coisa que tenha sido lançada (Error, texto, objeto, undefined). */
export function describeError(err: unknown): { message: string; stack: string } {
  if (err instanceof Error) {
    const head = `${err.name}: ${err.message}`;
    let stack = typeof err.stack === 'string' ? err.stack : '';
    // O V8 repete "Nome: mensagem" na primeira linha da pilha; fica só a mensagem no campo dela.
    if (stack.startsWith(head)) stack = stack.slice(head.length).replace(/^\n/, '');
    return { message: clip(scrubPaths(head), MESSAGE_MAX), stack: clip(scrubPaths(stack), STACK_MAX) };
  }
  if (typeof err === 'string') return { message: clip(scrubPaths(err), MESSAGE_MAX), stack: '' };
  let text: string;
  try { text = JSON.stringify(err) ?? String(err); } catch { text = String(err); }
  return { message: clip(scrubPaths(text), MESSAGE_MAX), stack: '' };
}

/** Números da mensagem (índice, valor, id) não separam um erro de outro: "índice 12" e "índice 13" na mesma pilha são o mesmo defeito. */
function messageKey(message: string): string {
  return message.replace(/\d+/g, '#');
}

function sameError(a: ErrorEntry, b: ErrorEntry): boolean {
  return a.kind === b.kind && a.stack === b.stack && messageKey(a.message) === messageKey(b.message);
}

/**
 * Põe `entry` no anel. Se o mesmo erro (tipo, pilha e mensagem a menos dos números) já está lá, soma no
 * contador dele, fica com a versão, a data, a mensagem e o contexto de agora (a 1ª vez continua em `time`) e o
 * traz para o fim — um erro por quadro não expulsa os outros. Devolve a entrada que ficou no anel e se ela é nova.
 */
export function pushEntry(ring: ErrorEntry[], entry: ErrorEntry, max = RING_SIZE): { entry: ErrorEntry; isNew: boolean } {
  const i = ring.findIndex((e) => sameError(e, entry));
  if (i >= 0) {
    const old = ring[i];
    ring.splice(i, 1);
    old.count += entry.count;
    old.last = entry.last;
    old.version = entry.version;
    old.message = entry.message;
    old.mode = entry.mode;
    old.track = entry.track;
    old.screen = entry.screen;
    ring.push(old);
    return { entry: old, isNew: false };
  }
  ring.push(entry);
  if (ring.length > max) ring.splice(0, ring.length - max);
  return { entry, isNew: true };
}

function str(v: unknown, max: number): string | null {
  return typeof v === 'string' ? clip(v, max) : null;
}

/** Anel lido do localStorage: descarta o que não é uma entrada válida e fica com as últimas `max`. */
export function sanitizeRing(raw: unknown, max = RING_SIZE): ErrorEntry[] {
  if (!Array.isArray(raw)) return [];
  const out: ErrorEntry[] = [];
  for (const r of raw) {
    if (typeof r !== 'object' || r === null) continue;
    const o = r as Record<string, unknown>;
    const time = str(o.time, 40);
    const message = str(o.message, MESSAGE_MAX + 1);
    const kind = typeof o.kind === 'string' && (KINDS as readonly string[]).includes(o.kind) ? (o.kind as ErrorKind) : null;
    if (!time || message === null || !kind) continue;
    out.push({
      time, last: str(o.last, 40) ?? time, version: str(o.version, 32) ?? '?', kind, message,
      stack: str(o.stack, STACK_MAX + 1) ?? '',
      mode: str(o.mode, 32), track: str(o.track, 64), screen: str(o.screen, 32),
      count: typeof o.count === 'number' && Number.isFinite(o.count) && o.count >= 1 ? Math.floor(o.count) : 1,
    });
  }
  return out.slice(-max);
}

function where(e: ErrorContext): string {
  return `mode=${e.mode ?? '-'} track=${e.track ?? '-'} screen=${e.screen ?? '-'}`;
}

/** Uma entrada no formato do arquivo de log (uma linha de cabeçalho, mensagem, pilha, linha em branco). */
export function logText(e: ErrorEntry): string {
  const repeat = e.count > 1 ? ` x${e.count} (last ${e.last})` : '';
  return `[${e.time}] v${e.version} ${e.kind} ${where(e)}${repeat}\n${e.message}\n${e.stack ? `${e.stack}\n` : ''}\n`;
}

export interface ReportMeta {
  version: string;
  /** Data de geração (ISO). */
  generated: string;
  userAgent: string;
  desktop: boolean;
  /** Linhas extras "chave: valor" (idioma, qualidade…). */
  extra?: Record<string, string>;
}

/**
 * O texto que vai para a área de transferência. Formato estável em inglês (é um artefato técnico, lido pelo
 * desenvolvedor e às vezes colado num fórum), do mais recente para o mais antigo.
 */
export function formatReport(entries: readonly ErrorEntry[], meta: ReportMeta): string {
  const lines = [
    'Nitro Crew — error report',
    `version: ${meta.version}`,
    `generated: ${meta.generated}`,
    `platform: ${scrubPaths(meta.userAgent)}`,
    `desktop: ${meta.desktop ? 'yes' : 'no'}`,
  ];
  for (const [k, v] of Object.entries(meta.extra ?? {})) lines.push(`${k}: ${v}`);
  lines.push(`errors: ${entries.length} (last ${RING_SIZE} kept)`, '');
  if (entries.length === 0) lines.push('No errors recorded.');
  const recentFirst = [...entries].reverse();
  recentFirst.forEach((e, i) => {
    const repeat = e.count > 1 ? ` · x${e.count} · last ${e.last}` : '';
    lines.push(`#${i + 1} · ${e.kind} · ${e.time}${repeat}`, `  where: ${where(e)} · v${e.version}`, `  ${e.message}`);
    if (e.stack) for (const l of e.stack.split('\n')) lines.push(`  ${l.trimEnd()}`);
    lines.push('');
  });
  return `${lines.join('\n').trimEnd()}\n`;
}

/** O que a telemetria mandaria de um erro: nada de nome de jogador, caminho ou navegador completo. */
export function telemetryPayload(e: ErrorEntry): Record<string, string | number> {
  return {
    v: e.version, kind: e.kind, message: e.message, stack: e.stack.split('\n').slice(0, 8).join('\n'),
    mode: e.mode ?? '', track: e.track ?? '', count: e.count,
  };
}

// ───────────────────────────── Telemetria (sem servidor) ─────────────────────────────

/**
 * Versão dos termos da telemetria. Ligar a opção grava em `Settings.telemetryConsent` a versão aceita (0 =
 * desligada). Termos 1: "hoje não envia nada". Quem ligar o envio de verdade SOBE este número no mesmo commit:
 * quem ligou nos termos antigos volta a ver a opção desligada e decide de novo (PRIVACIDADE.md, seção 11).
 */
export const TELEMETRY_TERMS = 1;

/** O consentimento guardado vale para os termos em vigor? */
export function telemetryConsented(consent: number, current: number = TELEMETRY_TERMS): boolean {
  return consent > 0 && consent === current;
}

/** Coletor da telemetria anônima. Nulo de propósito: não há servidor, e com nulo nada é enviado. */
let telemetryEndpoint: string | null = null;

export function setTelemetryEndpoint(url: string | null): void { telemetryEndpoint = url; }

// ───────────────────────────── O relator ─────────────────────────────

export interface ReporterDeps {
  version: string;
  now: () => Date;
  context: () => ErrorContext;
  /** localStorage (anel persistente); null sem armazenamento. */
  storage: StorageLike | null;
  /** Log em arquivo do Electron (`DesktopApi.logAppend`); null no navegador. */
  logAppend: ((text: string) => Promise<boolean>) | null;
  /** Opção do jogador: `telemetryConsented(Settings.telemetryConsent)`. */
  telemetryEnabled: () => boolean;
  /** Transporte da telemetria (navigator.sendBeacon); injetável nos testes. */
  send?: (url: string, body: string) => void;
  /** Um erro NOVO entrou no anel (a sessão mostra o aviso no canto). */
  onNew?: (entry: ErrorEntry, total: number) => void;
  /** Agenda a gravação do que ficou só na memória (setTimeout no jogo); sem ele, só `flush()` e o próximo erro gravam. */
  schedule?: (fn: () => void, ms: number) => void;
}

export interface ErrorReporter {
  report(err: unknown, kind?: ErrorKind): void;
  /** Do mais antigo para o mais recente. */
  entries(): readonly ErrorEntry[];
  clear(): void;
  /** Texto do relatório com o que já está no anel. */
  text(meta: Omit<ReportMeta, 'version' | 'generated'>): string;
  /** Avisa quando o anel é gravado (erro novo, repetição, limpeza); devolve quem desliga. */
  subscribe(fn: () => void): () => void;
  /** Grava no localStorage o que ainda só está na memória (a página vai fechar: `pagehide`). */
  flush(): void;
}

function loadRing(storage: StorageLike | null): ErrorEntry[] {
  if (!storage) return [];
  try {
    const raw = storage.getItem(ERRORS_KEY);
    return raw ? sanitizeRing(JSON.parse(raw) as unknown) : [];
  } catch {
    return [];
  }
}

export function createErrorReporter(deps: ReporterDeps): ErrorReporter {
  const ring = loadRing(deps.storage);
  const listeners = new Set<() => void>();
  /** Entradas já vistas NESTA sessão: a 1ª repetição de um erro de outra sessão volta ao log e ao localStorage. */
  const seen = new WeakSet<ErrorEntry>();
  let busy = false;
  /** O anel mudou desde a última gravação no localStorage. */
  let dirty = false;
  let lastPersist = Number.NEGATIVE_INFINITY;
  let loudWindowStart = Number.NEGATIVE_INFINITY;
  let loudInWindow = 0;
  /** Erros que iriam ao log um a um mas passaram do limite da rajada. */
  let unlogged = 0;
  let flushScheduled = false;

  function notify(): void {
    for (const fn of [...listeners]) {
      try { fn(); } catch { /* quem ouve não derruba quem relata */ }
    }
  }

  function log(text: string): void {
    try { deps.logAppend?.(text).catch(() => undefined); } catch { /* IPC quebrado: fica o localStorage */ }
  }

  /** Grava o anel no localStorage e, se a rajada deixou erros fora do log, uma linha dizendo quantos. */
  function persist(at: Date): void {
    dirty = false;
    lastPersist = at.getTime();
    try { deps.storage?.setItem(ERRORS_KEY, JSON.stringify(ring)); } catch { /* cota cheia: fica só na memória */ }
    if (unlogged > 0) {
      log(`[${at.toISOString()}] v${deps.version} ${unlogged} more error(s) not logged one by one (burst); the in-game report keeps the last ${RING_SIZE}\n\n`);
      unlogged = 0;
    }
    notify();
  }

  /** Cabe mais uma gravação completa nesta janela da rajada? */
  function takeLoud(t: number): boolean {
    if (t - loudWindowStart >= LOUD_WINDOW_MS) { loudWindowStart = t; loudInWindow = 0; }
    if (loudInWindow >= LOUD_MAX) return false;
    loudInWindow++;
    return true;
  }

  function context(): ErrorContext {
    try {
      const c = deps.context();
      return { mode: c.mode ?? null, track: c.track ?? null, screen: c.screen ?? null };
    } catch {
      return { ...NO_CONTEXT };
    }
  }

  function report(err: unknown, kind: ErrorKind = 'error'): void {
    if (busy) return; // um erro dentro do próprio relator não entra em laço
    busy = true;
    try {
      const at = deps.now();
      const time = at.toISOString();
      const { message, stack } = describeError(err);
      const { entry, isNew } = pushEntry(ring, { time, last: time, version: deps.version, kind, message, stack, count: 1, ...context() });
      const firstThisSession = !seen.has(entry);
      seen.add(entry);
      dirty = true;
      const important = firstThisSession || REPEAT_MILESTONES.has(entry.count);
      if (important && takeLoud(at.getTime())) {
        persist(at);
        log(logText(entry));
        if (!isNew) return;
        if (deps.telemetryEnabled() && telemetryEndpoint && deps.send) deps.send(telemetryEndpoint, JSON.stringify(telemetryPayload(entry)));
        deps.onNew?.(entry, ring.length);
        return;
      }
      if (important) unlogged++;
      if (at.getTime() - lastPersist >= PERSIST_EVERY_MS) persist(at);
      else if (!flushScheduled && deps.schedule) {
        flushScheduled = true;
        deps.schedule(() => { flushScheduled = false; flush(); }, PERSIST_EVERY_MS);
      }
    } catch {
      // Relatar nunca pode derrubar o jogo.
    } finally {
      busy = false;
    }
  }

  function flush(): void {
    if (!dirty && unlogged === 0) return;
    try { persist(deps.now()); } catch { /* relógio quebrado: fica na memória */ }
  }

  return {
    report,
    entries: () => ring,
    clear() { ring.length = 0; persist(deps.now()); },
    text: (meta) => formatReport(ring, { ...meta, version: deps.version, generated: deps.now().toISOString() }),
    subscribe(fn) {
      listeners.add(fn);
      return () => { listeners.delete(fn); };
    },
    flush,
  };
}

// ───────────────────────────── Instância do jogo ─────────────────────────────

let active: ErrorReporter | null = null;

export function setActiveReporter(r: ErrorReporter | null): void { active = r; }
export function getActiveReporter(): ErrorReporter | null { return active; }

/** Gancho para quem só quer relatar (o catch do laço da sessão). Sem relator instalado, não faz nada. */
export function reportError(err: unknown, kind: ErrorKind = 'error'): void {
  active?.report(err, kind);
}

/** O relatório de agora (do relator instalado; sem relator, só o cabeçalho), com `extra` no cabeçalho. */
export function currentReportText(extra: Record<string, string> = {}): string {
  const meta = { userAgent: typeof navigator === 'undefined' ? 'node' : navigator.userAgent, desktop: isDesktop(), extra };
  return active ? active.text(meta) : formatReport([], { ...meta, version: GAME_VERSION, generated: new Date().toISOString() });
}

type ListenerTarget = Pick<Window, 'addEventListener' | 'removeEventListener'>;

/** Liga `error` e `unhandledrejection` da janela ao relator; devolve a função que desliga. */
export function installGlobalHandlers(target: ListenerTarget, reporter: ErrorReporter): () => void {
  const onError = (e: ErrorEvent) => reporter.report(e.error ?? e.message, 'error');
  const onRejection = (e: PromiseRejectionEvent) => reporter.report(e.reason, 'rejection');
  target.addEventListener('error', onError);
  target.addEventListener('unhandledrejection', onRejection);
  return () => {
    target.removeEventListener('error', onError);
    target.removeEventListener('unhandledrejection', onRejection);
  };
}

/**
 * Copia para a área de transferência: pelo Electron (o preload em sandbox não tem `clipboard`), senão pela
 * Clipboard API do navegador, senão pelo velho `execCommand('copy')` (páginas sem contexto seguro).
 */
export async function copyToClipboard(text: string): Promise<boolean> {
  const d = getDesktop();
  if (d) {
    try { if (await d.copyText(text)) return true; } catch { /* tenta o do navegador */ }
  }
  if (typeof navigator !== 'undefined' && navigator.clipboard && typeof navigator.clipboard.writeText === 'function') {
    try { await navigator.clipboard.writeText(text); return true; } catch { /* sem permissão ou sem foco */ }
  }
  if (typeof document === 'undefined') return false;
  try {
    const ta = document.createElement('textarea');
    ta.value = text;
    ta.setAttribute('readonly', '');
    ta.style.cssText = 'position:fixed;left:-9999px;top:0;opacity:0';
    document.body.appendChild(ta);
    ta.select();
    const ok = document.execCommand('copy');
    ta.remove();
    return ok;
  } catch {
    return false;
  }
}
