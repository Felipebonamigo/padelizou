// Ponte tipada com o Electron (desktop/preload.cjs expõe `window.desktop`). Fora do Electron — navegador,
// Node nos testes — `getDesktop()` devolve null e o jogo segue com a Fullscreen API do próprio navegador.
// A lista de funções aqui e a do preload.cjs têm que andar juntas.

export interface DesktopApi {
  toggleFullscreen(): Promise<void>;
  setFullscreen(v: boolean): Promise<void>;
  isFullscreen(): Promise<boolean>;
  quit(): Promise<void>;
  /** Nome do jogador na Steam; null fora dela. */
  steamName(): Promise<string | null>;
  /** Desbloqueia a conquista; true se a Steam aceitou. */
  achievement(id: string): Promise<boolean>;
  /** Texto na lista de amigos da Steam ("Correndo em Copacabana"); vazio limpa. */
  richPresence(text: string): Promise<void>;
  /** Diálogo de salvar em Documentos, extensão `.nitro.json`; false se cancelado. */
  saveFile(name: string, content: string): Promise<boolean>;
  /** Diálogo de abrir; null se cancelado. */
  openFile(): Promise<string | null>;
  /** Saves em `<userData>/saves/<chave>.json` (pasta do Steam Auto-Cloud): chave → texto JSON. */
  storeReadAll(): Promise<Record<string, string>>;
  /** Grava o save da chave de forma atômica; false se a chave ou o JSON forem inválidos ou o disco falhar. */
  storeWrite(key: string, json: string): Promise<boolean>;
  /** Acrescenta ao log de erros `<userData>/logs/errors.log` (gira em 512 KB); false se falhou. */
  logAppend(text: string): Promise<boolean>;
  /** Copia para a área de transferência do sistema (o preload em sandbox não tem `clipboard`). */
  copyText(text: string): Promise<boolean>;
  /** Avisa quando a tela cheia muda (inclusive por F11 tratado no processo principal). */
  onFullscreen(cb: (v: boolean) => void): void;
}

declare global {
  interface Window { desktop?: DesktopApi }
}

/** Exportada para tests/desktop-storage.test.ts conferir que o preload.cjs expõe exatamente estas. */
export const API_FUNCTIONS: ReadonlyArray<keyof DesktopApi> = [
  'toggleFullscreen', 'setFullscreen', 'isFullscreen', 'quit', 'steamName', 'achievement', 'richPresence',
  'saveFile', 'openFile', 'storeReadAll', 'storeWrite', 'logAppend', 'copyText', 'onFullscreen',
];

/** Verdadeiro só se o objeto tem TODAS as funções — um preload desatualizado não passa. */
function isDesktopApi(v: unknown): v is DesktopApi {
  if (typeof v !== 'object' || v === null) return false;
  const o = v as Record<string, unknown>;
  return API_FUNCTIONS.every((k) => typeof o[k] === 'function');
}

/** A ponte do Electron, ou null fora dele (navegador ou Node). */
export function getDesktop(): DesktopApi | null {
  if (typeof window === 'undefined') return null;
  const d: unknown = window.desktop;
  return isDesktopApi(d) ? d : null;
}

export function isDesktop(): boolean { return getDesktop() !== null; }

/**
 * Tela cheia: pelo Electron quando existe, senão pela Fullscreen API do navegador. Tolerante a rejeição
 * (o navegador recusa fora de um gesto do usuário) e a ambientes sem `document` (Node).
 */
export async function setFullscreen(v: boolean): Promise<void> {
  const d = getDesktop();
  if (d) { await d.setFullscreen(v); return; }
  if (typeof document === 'undefined') return;
  try {
    if (v) {
      if (!document.fullscreenElement && typeof document.documentElement.requestFullscreen === 'function') {
        await document.documentElement.requestFullscreen();
      }
    } else if (document.fullscreenElement && typeof document.exitFullscreen === 'function') {
      await document.exitFullscreen();
    }
  } catch {
    // Recusa do navegador (sem gesto do usuário, iframe sem permissão): o jogo segue em janela.
  }
}

// ───────────────────────────── Conquistas ─────────────────────────────

export interface AchievementDef { id: string; pt: string; en: string }

/** IDs cadastrados na Steam (desktop/README.md tem a descrição de cada um). */
export const ACHIEVEMENTS = [
  { id: 'PRIMEIRA_VITORIA', pt: 'Primeira vitória', en: 'First Win' },
  { id: 'COPA_BRASIL', pt: 'Copa Brasil', en: 'Brazil Cup' },
  { id: 'COPA_EUA', pt: 'Copa Estados Unidos', en: 'USA Cup' },
  { id: 'COPA_JAPAO', pt: 'Copa Japão', en: 'Japan Cup' },
  { id: 'COPA_EUROPA', pt: 'Copa Europa', en: 'Europe Cup' },
  { id: 'COPA_AFRICA_DO_SUL', pt: 'Copa África do Sul', en: 'South Africa Cup' },
  { id: 'COPA_AUSTRALIA', pt: 'Copa Austrália', en: 'Australia Cup' },
  { id: 'COPA_ESCANDINAVIA', pt: 'Copa Escandinávia', en: 'Scandinavia Cup' },
  { id: 'COPA_MEDITERRANEO', pt: 'Copa Mediterrâneo', en: 'Mediterranean Cup' },
  { id: 'EQUIPE_COMPLETA', pt: 'Equipe completa', en: 'Full Crew' },
  { id: 'SEM_BOX', pt: 'Sem box', en: 'No Pit Stop' },
  { id: 'NITRO_TRIPLO', pt: 'Nitro triplo', en: 'Triple Nitro' },
  { id: 'EMPURRAO', pt: 'Empurrão', en: 'Push' },
  { id: 'VOLTA_PERFEITA', pt: 'Volta perfeita', en: 'Perfect Lap' },
  { id: 'CAMPEAO', pt: 'Campeão', en: 'Champion' },
  { id: 'MADRUGADA', pt: 'Madrugada', en: 'Night Owl' },
] as const satisfies ReadonlyArray<AchievementDef>;

export type AchievementId = (typeof ACHIEVEMENTS)[number]['id'];
