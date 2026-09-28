// Armazenamento dos fantasmas: a melhor volta por pista, numa chave própria (fora do save, que fica
// pequeno), com teto de tamanho total — passou dele, saem as gravadas há mais tempo. Tolerante a
// localStorage cheio: se a gravação falha, descarta a mais antiga e tenta de novo, sem nunca lançar.
// Pela mesma writeJson do resto do jogo, então no Electron também vai para arquivo (Steam Cloud,
// cloudsave.ts). Exportar/importar (desafiar um amigo): no Electron pelo diálogo do sistema
// (file:save / file:open do preload); no navegador por download e escolha de arquivo.
import { getDesktop } from './desktop';
import { ghostFileText, parseGhostFile, sanitizeGhostRecord, MAX_GHOST_FILE_CHARS, type GhostRecord } from './ghost';
import { isRecord, readJson, writeJson } from './settings';

export const GHOST_STORE_KEY = 'nitro-crew.ghosts';
/** Teto do JSON inteiro (~30 voltas típicas de ~5 KB; o arquivo espelhado do Electron aceita até 1 MB). */
export const GHOST_STORE_MAX_CHARS = 160_000;

/** Fantasma guardado: o registro e quando entrou neste computador (critério do descarte). */
export interface StoredGhost extends GhostRecord {
  savedAt: string;
}

export interface GhostStore {
  ghosts: Record<string, StoredGhost>;
}

export function emptyGhostStore(): GhostStore {
  return { ghosts: {} };
}

/** Só os fantasmas válidos, cada um sob o id da própria pista; lixo vira loja vazia. Nunca lança. */
export function sanitizeGhostStore(raw: unknown): GhostStore {
  const out = emptyGhostStore();
  const ghosts = isRecord(raw) && isRecord(raw.ghosts) ? raw.ghosts : {};
  for (const [trackId, value] of Object.entries(ghosts)) {
    const rec = sanitizeGhostRecord(value);
    if (!rec || rec.trackId !== trackId) continue;
    const savedAt = isRecord(value) && typeof value.savedAt === 'string' ? value.savedAt.slice(0, 40) : '';
    out.ghosts[trackId] = { ...rec, savedAt };
  }
  return out;
}

export function ghostStoreSize(store: GhostStore): number {
  return JSON.stringify(store).length;
}

/** Do mais antigo ao mais novo (empate: id da pista, para o descarte não depender da ordem das chaves). */
function oldestFirst(store: GhostStore): string[] {
  return Object.values(store.ghosts)
    .sort((a, b) => (a.savedAt < b.savedAt ? -1 : a.savedAt > b.savedAt ? 1 : a.trackId < b.trackId ? -1 : 1))
    .map((g) => g.trackId);
}

/** Tira os mais antigos até caber em `maxChars`; devolve as pistas descartadas. */
export function pruneGhostStore(store: GhostStore, maxChars = GHOST_STORE_MAX_CHARS): string[] {
  const removed: string[] = [];
  const order = oldestFirst(store);
  while (order.length > 0 && ghostStoreSize(store) > maxChars) {
    const id = order.shift() as string;
    delete store.ghosts[id];
    removed.push(id);
  }
  return removed;
}

export function ghostFor(store: GhostStore, trackId: string): GhostRecord | null {
  const g = store.ghosts[trackId];
  if (!g) return null;
  const { savedAt: _savedAt, ...rec } = g;
  return rec;
}

/** Guarda (substituindo o da pista). `savedAt` = agora, em ISO. */
export function putGhost(store: GhostStore, rec: GhostRecord, savedAt: string): void {
  store.ghosts[rec.trackId] = { ...rec, savedAt };
}

export function loadGhostStore(read: (key: string) => unknown = readJson): GhostStore {
  try { return sanitizeGhostStore(read(GHOST_STORE_KEY)); } catch { return emptyGhostStore(); }
}

/**
 * Grava uma cópia da loja podada ao teto; se a gravação falhar (localStorage cheio), descarta a mais
 * antiga e tenta de novo, até a loja vazia. Devolve a loja que foi gravada, ou null se nada coube
 * (ou não há armazenamento). Nunca lança; a loja recebida não muda.
 */
export function saveGhostStore(
  store: GhostStore, write: (key: string, value: unknown) => boolean = writeJson, maxChars = GHOST_STORE_MAX_CHARS,
): GhostStore | null {
  const copy: GhostStore = { ghosts: { ...store.ghosts } };
  pruneGhostStore(copy, maxChars);
  const order = oldestFirst(copy);
  for (;;) {
    let ok = false;
    try { ok = write(GHOST_STORE_KEY, copy); } catch { ok = false; }
    if (ok) return copy;
    const id = order.shift();
    if (id === undefined) return null;
    delete copy.ghosts[id];
  }
}

// ───────────────────────────── Arquivo (desafiar um amigo) ─────────────────────────────

export function ghostFileName(rec: GhostRecord): string {
  return `fantasma-${rec.trackId}`;
}

/** Exporta o fantasma como arquivo `.nitro.json`; false se cancelado ou se não deu. */
export async function exportGhostFile(rec: GhostRecord): Promise<boolean> {
  const text = ghostFileText(rec);
  const desktop = getDesktop();
  try {
    if (desktop) return await desktop.saveFile(ghostFileName(rec), text);
    if (typeof document === 'undefined') return false;
    const url = URL.createObjectURL(new Blob([text], { type: 'application/json' }));
    const a = document.createElement('a');
    a.href = url;
    a.download = `${ghostFileName(rec)}.nitro.json`;
    a.style.display = 'none';
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 10_000);
    return true;
  } catch {
    return false;
  }
}

/** Resultado de uma importação: o registro válido, 'cancel' (nada escolhido) ou 'invalid' (não é um fantasma). */
export type GhostImport = GhostRecord | 'cancel' | 'invalid';

/** Abre o diálogo de arquivo e lê um fantasma. Nunca lança. */
export async function importGhostFile(): Promise<GhostImport> {
  let text: string | null;
  try {
    const desktop = getDesktop();
    text = desktop ? await desktop.openFile() : await pickBrowserFile();
  } catch {
    return 'invalid';
  }
  if (text === null) return 'cancel';
  return parseGhostFile(text) ?? 'invalid';
}

/** Navegador: <input type=file> escondido; null se o jogador cancelar. Arquivo grande demais nem é lido. */
function pickBrowserFile(): Promise<string | null> {
  if (typeof document === 'undefined') return Promise.resolve(null);
  // Sem gesto do usuário (botão do gamepad, lido por quadro) o navegador não abre o seletor e nem avisa.
  if (typeof navigator !== 'undefined' && navigator.userActivation && !navigator.userActivation.isActive) return Promise.resolve(null);
  return new Promise((resolve) => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.json,application/json';
    input.style.display = 'none';
    const done = (v: string | null) => { input.remove(); resolve(v); };
    input.addEventListener('cancel', () => done(null));
    input.addEventListener('change', () => {
      const file = input.files?.[0];
      if (!file) { done(null); return; }
      if (file.size > MAX_GHOST_FILE_CHARS * 4) { done(''); return; }
      file.text().then((t) => done(t), () => done(''));
    });
    document.body.appendChild(input);
    input.click();
  });
}
