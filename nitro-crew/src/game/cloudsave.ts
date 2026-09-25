// Save em arquivo para o Steam Auto-Cloud (só no Electron).
// O jogo grava opções e progresso no localStorage (settings.ts → writeJson). No Electron isso mora num LevelDB
// do Chromium — arquivos com nome que muda, trava e compactação — que o Auto-Cloud não sincroniza direito.
// Por isso cada gravação também vai para `<userData>/saves/<chave>.json` (atômico, desktop/storage.cjs), e é
// essa pasta que o Auto-Cloud sincroniza (desktop/README.md tem a configuração).
// Na inicialização, ANTES de a sessão ler opções e progresso, `hydrateFromDisk` decide chave a chave quem vale:
//   • o arquivo — é a fonte da verdade, e pode ter chegado da nuvem vindo de outro computador;
//   • o localStorage — quando a última gravação local não chegou ao disco (marcada como pendente), ou quando
//     ainda não existe arquivo (jogador de uma versão anterior, migração);
// e o arquivo corrompido nunca vence um localStorage válido.
// Sem leitura confirmada — o IPC falhou ou passou do limite, ou o arquivo existe mas não deu para ler (null do
// storage.cjs) — "não sei o que tem no disco" NÃO é "não há arquivo": o localStorage segue valendo para a sessão,
// mas nada é gravado por cima do arquivo (ele pode ser o mais novo, da nuvem), exceto a chave pendente, cujo
// local é sabidamente mais novo. Limite conhecido: o espelho (installSaveMirror) continua ligado, então a próxima
// gravação do jogo nesta sessão vai para o arquivo — como aconteceria com o save de qualquer jogo aberto.
import type { DesktopApi } from './desktop';
import { ERRORS_KEY } from './errors';
import { setStorageMirror } from './settings';

export const KEY_PREFIX = 'nitro-crew.';
/** Chaves cuja última gravação local ainda não foi confirmada no disco (JSON: string[]). */
export const PENDING_KEY = 'nitro-crew.__pending';
/** Ficam só neste computador: a marcação de pendência e o log de erros. */
export const LOCAL_ONLY_KEYS: ReadonlySet<string> = new Set([PENDING_KEY, ERRORS_KEY]);
const IPC_TIMEOUT_MS = 3000;

/** O pedaço do Storage que este módulo usa (o localStorage, ou um falso nos testes). */
export interface KeyedStorage {
  readonly length: number;
  key(index: number): string | null;
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
}

export type SaveSource = 'disk' | 'local' | 'none';

export function isCloudKey(key: string): boolean {
  return key.startsWith(KEY_PREFIX) && !LOCAL_ONLY_KEYS.has(key);
}

function isJson(text: string | null | undefined): text is string {
  if (typeof text !== 'string') return false;
  try { JSON.parse(text); return true; } catch { return false; }
}

/**
 * Quem vale para uma chave. `local` é o texto do localStorage (null se não há), `disk` o do arquivo
 * (undefined se não há), `pending` se a última gravação local não foi confirmada no disco.
 */
export function chooseSource(local: string | null, disk: string | undefined, pending: boolean): SaveSource {
  const localOk = isJson(local);
  if (pending && localOk) return 'local';
  if (isJson(disk)) return disk === local ? 'none' : 'disk';
  return localOk ? 'local' : 'none';
}

function readPending(storage: KeyedStorage): Set<string> {
  try {
    const raw: unknown = JSON.parse(storage.getItem(PENDING_KEY) ?? '[]');
    return new Set(Array.isArray(raw) ? raw.filter((k): k is string => typeof k === 'string') : []);
  } catch {
    return new Set();
  }
}

function writePending(storage: KeyedStorage, pending: Set<string>): void {
  try { storage.setItem(PENDING_KEY, JSON.stringify([...pending].sort())); } catch { /* sem cota: fica como estava */ }
}

function localCloudKeys(storage: KeyedStorage): string[] {
  const out: string[] = [];
  for (let i = 0; i < storage.length; i++) {
    const k = storage.key(i);
    if (k !== null && isCloudKey(k)) out.push(k);
  }
  return out;
}

function withTimeout<T>(p: Promise<T>, ms: number): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error(`IPC sem resposta em ${ms} ms`)), ms);
    p.then((v) => { clearTimeout(timer); resolve(v); }, (e: unknown) => { clearTimeout(timer); reject(e); });
  });
}

export interface HydrateReport {
  /** Chaves copiadas do arquivo para o localStorage. */
  fromDisk: string[];
  /** Chaves gravadas do localStorage para o arquivo. */
  toDisk: string[];
  /** Chaves que deveriam ir para o disco e não foram (continuam pendentes). */
  failed: string[];
  /** Chaves deixadas como estavam porque não deu para ler o disco (nem arquivo nem pendência mudaram). */
  unknown: string[];
}

export async function hydrateFromDisk(
  api: Pick<DesktopApi, 'storeReadAll' | 'storeWrite'>, storage: KeyedStorage, timeoutMs = IPC_TIMEOUT_MS,
): Promise<HydrateReport> {
  const report: HydrateReport = { fromDisk: [], toDisk: [], failed: [], unknown: [] };
  const disk: Record<string, string> = {};
  /** Chaves cujo arquivo não foi lido: todas, se a leitura falhou; senão as que o storage.cjs devolveu null. */
  const unread = new Set<string>();
  let readOk = true;
  try {
    const raw: unknown = await withTimeout(api.storeReadAll(), timeoutMs);
    if (typeof raw === 'object' && raw !== null) {
      for (const [k, v] of Object.entries(raw as Record<string, unknown>)) {
        if (!isCloudKey(k)) continue;
        if (typeof v === 'string') disk[k] = v;
        else if (v === null) unread.add(k);
      }
    }
  } catch {
    readOk = false;
  }
  const pending = readPending(storage);
  const keys = [...new Set([...Object.keys(disk), ...unread, ...localCloudKeys(storage)])].sort();
  for (const key of keys) {
    let local: string | null = null;
    try { local = storage.getItem(key); } catch { local = null; }
    if (!readOk || unread.has(key)) {
      // Disco desconhecido: só a gravação pendente (local sabidamente mais novo) vai para o arquivo.
      if (!(pending.has(key) && isJson(local))) { report.unknown.push(key); continue; }
    }
    const source = chooseSource(local, disk[key], pending.has(key));
    if (source === 'disk') {
      try { storage.setItem(key, disk[key]); report.fromDisk.push(key); pending.delete(key); } catch { /* cota: a sessão lê o padrão */ }
    } else if (source === 'local' && local !== null) {
      let ok = false;
      try { ok = await withTimeout(api.storeWrite(key, local), timeoutMs); } catch { ok = false; }
      if (ok) { pending.delete(key); report.toDisk.push(key); } else { pending.add(key); report.failed.push(key); }
    } else {
      pending.delete(key);
    }
  }
  writePending(storage, pending);
  return report;
}

/**
 * Daqui em diante, toda gravação do jogo (writeJson) também vai para o arquivo. A chave fica pendente até o
 * disco confirmar a gravação MAIS RECENTE dela; devolve a função que desliga o espelho.
 */
export function installSaveMirror(api: Pick<DesktopApi, 'storeWrite'>, storage: KeyedStorage): () => void {
  const seq = new Map<string, number>();
  const setPending = (key: string, on: boolean) => {
    const pending = readPending(storage);
    if (pending.has(key) === on) return;
    if (on) pending.add(key); else pending.delete(key);
    writePending(storage, pending);
  };
  setStorageMirror((key, json) => {
    if (!isCloudKey(key)) return;
    const n = (seq.get(key) ?? 0) + 1;
    seq.set(key, n);
    setPending(key, true);
    api.storeWrite(key, json).then((ok) => { if (ok && seq.get(key) === n) setPending(key, false); }, () => undefined);
  });
  return () => setStorageMirror(null);
}
