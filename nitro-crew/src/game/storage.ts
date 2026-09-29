// Onde o jogo grava opções e progresso (docs/SAVE.md). Três lugares, do mais durável ao mais efêmero:
//   • o espelho — no Electron, o arquivo em userData que o Steam Cloud sincroniza (cloudsave.ts); null no navegador;
//   • o localStorage — onde o jogo lê e grava durante a partida;
//   • a memória desta sessão — o que o localStorage recusou (cota da origem cheia, armazenamento bloqueado).
// Toda gravação vai ao localStorage E ao espelho, mesmo quando o localStorage recusa: o progresso novo nunca fica
// preso atrás de uma cota. O que o localStorage recusou fica na memória e é o que `readJson` devolve — a sessão
// continua vendo o que gravou (a loja de fantasmas, por exemplo, é relida a cada volta nova).
// Sem espelho e sem espaço, antes de desistir do localStorage tenta abrir espaço com o que é descartável
// (`setSpaceFreers`: log de erros, fantasmas mais antigos) — nunca com save, carreira, estatísticas ou opções.
// Não coube em lugar nenhum: a chave entra em `saveHealth().lost` e o jogador é avisado (save-notice.ts).
// Nada aqui lança.

/** Quem recebe cada gravação além do localStorage; devolve a promessa da gravação, ou null se esta chave não vai para lá. */
export type StorageMirror = (key: string, json: string, localOk: boolean) => Promise<boolean> | null;

/** Abre espaço no localStorage para `forKey` descartando algo; devolve se descartou (vale tentar de novo). */
export type SpaceFreer = (forKey: string) => boolean;

/** Chaves cuja última gravação não ficou em lugar nenhum, e quantas gravações se perderam nesta sessão. */
export interface SaveHealth {
  lost: readonly string[];
  failures: number;
}

/** Teto de descartes por liberador numa gravação: um liberador com defeito nunca prende o jogo num laço. */
const MAX_FREES_PER_FREER = 64;

let storageMirror: StorageMirror | null = null;
let freers: readonly SpaceFreer[] = [];
let freeing = false;
/** O que o localStorage recusou nesta sessão (texto JSON por chave): vale mais que ele na leitura. */
const memory = new Map<string, string>();
/** Número da gravação mais recente de cada chave: só a resposta dela, do espelho, decide se a chave se perdeu. */
const writeSeq = new Map<string, number>();
const lost = new Set<string>();
let failures = 0;

function storage(): Storage | null {
  try {
    return typeof localStorage === 'undefined' ? null : localStorage;
  } catch {
    return null;
  }
}

/** A recusa é de cota (e não de armazenamento bloqueado)? Chromium e Safari: QuotaExceededError (22); Firefox antigo: 1014. */
export function isQuotaError(e: unknown): boolean {
  if (typeof e !== 'object' || e === null) return false;
  const { name, code } = e as { name?: unknown; code?: unknown };
  return name === 'QuotaExceededError' || name === 'NS_ERROR_DOM_QUOTA_REACHED' || code === 22 || code === 1014;
}

/** JSON gravado sob `key` (o da memória da sessão primeiro), ou `undefined` se não houver chave ou JSON válido. */
export function readJson(key: string): unknown {
  try {
    const raw = memory.get(key) ?? storage()?.getItem(key);
    return raw == null ? undefined : (JSON.parse(raw) as unknown);
  } catch {
    return undefined;
  }
}

/** No Electron, cloudsave.ts liga o espelho em arquivo; null desliga. */
export function setStorageMirror(fn: StorageMirror | null): void {
  storageMirror = fn;
}

/** Quem pode abrir espaço, na ordem em que é tentado (main.ts liga). */
export function setSpaceFreers(list: readonly SpaceFreer[]): void {
  freers = list;
}

/**
 * A abertura (cloudsave.ts) decidiu que o arquivo vale para `key`, mas o localStorage não o aceitou: a sessão lê
 * da memória. O arquivo já tem esse texto — nada a espelhar, nada perdido.
 */
export function keepInMemory(key: string, json: string): void {
  memory.set(key, json);
}

export function saveHealth(): SaveHealth {
  return { lost: [...lost].sort(), failures };
}

function markLost(key: string): void {
  lost.add(key);
  failures++;
}

type LocalResult = 'ok' | 'quota' | 'refused';

function setLocal(s: Storage, key: string, json: string): LocalResult {
  try {
    s.setItem(key, json);
    return 'ok';
  } catch (e) {
    return isQuotaError(e) ? 'quota' : 'refused';
  }
}

/** Descarta o que os liberadores oferecem, um de cada vez, até a gravação caber. Nunca em cascata (um liberador que grava). */
function freeAndRetry(s: Storage, key: string, json: string): boolean {
  if (freeing) return false;
  freeing = true;
  try {
    for (const free of freers) {
      for (let i = 0; i < MAX_FREES_PER_FREER; i++) {
        let freed = false;
        try { freed = free(key); } catch { freed = false; }
        if (!freed) break;
        if (setLocal(s, key, json) === 'ok') return true;
      }
    }
    return false;
  } finally {
    freeing = false;
  }
}

/**
 * Grava `value` como JSON no localStorage e no espelho. Devolve verdadeiro quando ficou em algum lugar (localStorage,
 * ou espelho aceitando a gravação — a resposta do disco chega depois e, se for recusa, marca a chave em `lost`);
 * falso quando não ficou em lugar nenhum (a sessão segue com o valor na memória).
 */
export function writeJson(key: string, value: unknown): boolean {
  let json: string;
  try {
    json = JSON.stringify(value);
  } catch {
    return false;
  }
  const s = storage();
  let local: LocalResult = s ? setLocal(s, key, json) : 'refused';
  const seq = (writeSeq.get(key) ?? 0) + 1;
  writeSeq.set(key, seq);
  let mirrored: Promise<boolean> | null = null;
  try { mirrored = storageMirror?.(key, json, local === 'ok') ?? null; } catch { mirrored = null; /* o espelho nunca derruba a gravação */ }
  // Só descarta para não perder o progresso: com o espelho recebendo, o localStorage atrasado não custa nada.
  if (s && local === 'quota' && mirrored === null && freeAndRetry(s, key, json)) local = 'ok';
  if (local === 'ok') memory.delete(key); else memory.set(key, json);
  if (local === 'ok' || mirrored !== null) lost.delete(key); else markLost(key);
  if (mirrored !== null) {
    const localOk = local === 'ok';
    const reply = (ok: boolean) => {
      if (writeSeq.get(key) !== seq || localOk) return;
      if (ok) lost.delete(key); else markLost(key);
    };
    mirrored.then(reply, () => reply(false));
  }
  return local === 'ok' || mirrored !== null;
}
