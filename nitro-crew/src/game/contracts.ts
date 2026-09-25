// Contratos entre as camadas. O núcleo (src/core) não conhece nada disto; a sessão
// (src/game/session.ts) liga as implementações. Cada camada mora numa pasta própria:
//   src/render  → Renderer            src/ui     → Menus + InputProvider
//   src/audio   → AudioEngine         src/game   → settings.ts, save.ts, desktop.ts, session.ts
import type {
  CarDef, ChampionshipState, CoopAssists, CupDef, Difficulty, HumanEntry, PlayerInput, RaceResultRow, RaceState,
  SimEvent, Track, TrackDef,
} from '../core/types';
import type { Lang } from '../i18n';
import { DEFAULT_BINDINGS, type ControlBindings } from '../ui/remap/bindings';

// ───────────────────────────── Entrada ─────────────────────────────

/** `kb1` = setas + espaço/etc; `kb2` = WASD + etc; `gpN` = gamepad de índice N da Gamepad API. */
export type DeviceId = 'kb1' | 'kb2' | `gp${number}`;

export interface DeviceInfo {
  id: DeviceId;
  /** Texto para a interface: "Teclado (setas)", "Controle 1 — Xbox…" */
  label: string;
  connected: boolean;
  boundSeat: number | null;
}

/** Bordas de navegação de menu (verdadeiro só no quadro em que foi apertado), somadas de todos os dispositivos. */
export interface MenuNav {
  up: boolean; down: boolean; left: boolean; right: boolean;
  confirm: boolean; back: boolean; start: boolean;
  /** Dispositivo que gerou a última borda deste quadro (para saber quem apertou no lobby). */
  device: DeviceId | null;
}

export interface InputProvider {
  /** Chamar uma vez por quadro, antes de ler qualquer coisa (lê gamepads, fecha bordas). */
  poll(): void;
  /** Comando do assento; neutro se não houver dispositivo ligado a ele. `nitro`, `gearUp`, `gearDown` são bordas. */
  readSeat(seat: number): PlayerInput;
  bindSeat(seat: number, device: DeviceId): void;
  unbindSeat(seat: number): void;
  seatDevice(seat: number): DeviceId | null;
  devices(): DeviceInfo[];
  /** Dispositivo ainda sem assento cujo botão de confirmar foi apertado neste quadro (entrar no lobby). */
  joinPressed(): DeviceId | null;
  /** Dispositivo ligado a um assento cujo botão de voltar foi apertado neste quadro (sair do lobby). */
  leavePressed(): DeviceId | null;
  menuNav(): MenuNav;
  /** Assento cujo botão de pausa (Esc/Start) foi apertado neste quadro; -1 se nenhum. Esc sem assento vale como assento 0. */
  pausePressed(): number;
  /** Estado segurado de um dispositivo com o mapeamento atual (teste de entrada e captura da tela de controles); null se desconhecido. */
  peek(device: DeviceId): DevicePeek | null;
  /** Vibra o gamepad do assento (`strength` 0..1, `ms` de duração). No-op sem gamepad, sem suporte ou com a vibração desligada. */
  rumble(seat: number, strength: number, ms: number): void;
  dispose(): void;
}

export interface DevicePeek {
  steer: number;
  throttle: boolean; brake: boolean; nitro: boolean; gearUp: boolean; gearDown: boolean; pause: boolean;
  /** Botões "standard" apertados agora (só gamepads; vazio nos teclados). */
  buttons: number[];
}

// ───────────────────────────── Opções e progresso ─────────────────────────────

export type Quality = 'low' | 'medium' | 'high';

export interface Settings {
  language: Lang;
  masterVolume: number; // 0..1
  musicVolume: number;
  sfxVolume: number;
  fullscreen: boolean;
  quality: Quality;
  showMinimap: boolean;
  screenShake: boolean;
  difficulty: Difficulty;
  manualGear: boolean;
  assists: CoopAssists;
  /** Carros na pista, humanos incluídos (8..20). */
  totalCars: number;
  /** Voltas da corrida rápida (2..8). */
  quickLaps: number;
  /** Música escolhida no jukebox (id) ou 'auto'. */
  music: string;
  /** Tecla/botão por ação de pilotagem, por dispositivo (src/ui/remap/bindings.ts). */
  controls: ControlBindings;
  /** Vibração dos gamepads (batidas, nitro, grama, largada). */
  vibration: boolean;
}

export const DEFAULT_SETTINGS: Readonly<Settings> = Object.freeze({
  language: 'pt', masterVolume: 0.8, musicVolume: 0.6, sfxVolume: 0.9, fullscreen: false, quality: 'high',
  showMinimap: true, screenShake: true, difficulty: 'profissional', manualGear: false,
  assists: { sharedNitro: true, tow: true, teamDraft: true, catchup: true }, totalCars: 20, quickLaps: 3, music: 'auto',
  controls: DEFAULT_BINDINGS, vibration: true,
});

export interface BestLap { ticks: number; name: string; carId: string; date: string }

export interface SaveData {
  /** Copas concluídas (destravam a seguinte). */
  cupsCompleted: string[];
  bestLaps: Record<string, BestLap>;
  /** Melhor tempo total de corrida por pista, chave `${trackId}:${laps}`. */
  bestRaces: Record<string, BestLap>;
  achievements: string[];
  racesRun: number;
  racesWon: number;
  /** Nomes usados por assento na última sessão (P1..P4). */
  seatNames: string[];
  /** Carro escolhido por assento na última sessão. */
  seatCars: string[];
}

export const DEFAULT_SAVE: Readonly<SaveData> = Object.freeze({
  cupsCompleted: [], bestLaps: {}, bestRaces: {}, achievements: [], racesRun: 0, racesWon: 0,
  seatNames: ['P1', 'P2', 'P3', 'P4'], seatCars: ['falcao', 'trovao', 'tornado', 'camelo'],
});

// ───────────────────────────── Renderização ─────────────────────────────

export interface RenderOptions {
  quality: Quality;
  showMinimap: boolean;
  screenShake: boolean;
}

export interface HudMessage {
  text: string;
  /** Segundos restantes; a sessão decrementa e remove. */
  ttl: number;
  kind: 'info' | 'big' | 'warn' | 'good';
}

export interface ViewportSpec {
  seat: number;
  /** Índice em `state.cars`. */
  carIndex: number;
  color: string;
  name: string;
  messages: HudMessage[];
}

export interface RenderFrame {
  state: RaceState;
  track: Track;
  /** Um por jogador local, em ordem de assento. 1 = tela cheia, 2 = em cima/embaixo, 3–4 = 2×2. */
  viewports: ViewportSpec[];
  options: RenderOptions;
  /** Segundos desde o início da sessão (animações). */
  time: number;
  paused: boolean;
  /** Modo cooperativo (para o HUD mostrar cofre de nitro e companheiros). */
  coop: boolean;
  /** Falso enquanto uma tela de menu cobre a corrida (resultado, classificação): o HUD some. */
  showHud: boolean;
}

/**
 * Renderizador 3D (Three.js/WebGL) — `createRenderer(canvas, hudRoot)`. O canvas recebe o mundo 3D
 * (uma câmera por viewport, tela dividida por scissor); o HUD é DOM dentro de `hudRoot`, por cima
 * do canvas e por baixo dos menus (`#ui`).
 */
export interface Renderer {
  readonly canvas: HTMLCanvasElement;
  /** Tamanho em pixels CSS e razão de pixels; o renderizador cuida do backing store. */
  resize(width: number, height: number, dpr: number): void;
  render(frame: RenderFrame): void;
  /** Fundo animado atrás dos menus (câmera automática voando pela pista), sem HUD. */
  renderIdle(time: number, track: Track): void;
  /** Libera GPU e DOM do HUD. */
  dispose(): void;
}

/** Contorno da pista para o minimapa e para as telas de menu (função pura, exportada por src/render/minimap.ts). */
export type TrackOutlineFn = (track: Track, size: number) => Array<[number, number]>;

// ───────────────────────────── Áudio ─────────────────────────────

export interface MusicInfo { id: string; title: string; author: string }

export interface AudioEngine {
  /** Chamar a partir de um gesto do usuário (clique/tecla): cria/destrava o AudioContext. */
  unlock(): void;
  /** Uma vez por quadro. `frame` nulo fora da corrida (menus). */
  update(frame: RenderFrame | null, dt: number): void;
  /** Evento da simulação. `localSeat` é o assento local a quem ele diz respeito (-1 = carro da IA). */
  onEvent(event: SimEvent, localSeat: number): void;
  /** `id` de MusicInfo, 'auto' (a sessão escolhe por pista) ou null para silêncio. */
  setMusic(id: string | null): void;
  currentMusic(): string | null;
  setVolumes(master: number, music: number, sfx: number): void;
  musicList(): MusicInfo[];
  /** Som de interface (navegar/confirmar/voltar). */
  ui(kind: 'move' | 'confirm' | 'back'): void;
}

// ───────────────────────────── Menus ─────────────────────────────

export type MenuScreen = 'title' | 'main' | 'lobby' | 'cups' | 'tracks' | 'results' | 'standings' | 'pause' | 'options' | 'controls' | 'records' | 'credits' | 'loading';

export type RaceMode = 'cup' | 'quick' | 'timetrial';

export type MenuEvent =
  | { type: 'startCup'; cupId: string; humans: HumanEntry[] }
  | { type: 'startQuick'; trackId: string; laps: number; humans: HumanEntry[] }
  | { type: 'startTimeTrial'; trackId: string; humans: HumanEntry[] }
  | { type: 'nextRace' }
  | { type: 'retryRace' }
  | { type: 'toMain' }
  | { type: 'resume' }
  | { type: 'restart' }
  | { type: 'settingsChanged'; settings: Settings }
  | { type: 'quitApp' };

export interface ResultsScreenData {
  mode: RaceMode;
  trackDef: TrackDef;
  results: RaceResultRow[];
  humans: HumanEntry[];
  champ: ChampionshipState | null;
  /** Recordes batidos nesta corrida (por assento), para destacar. */
  newRecords: Array<{ seat: number; kind: 'lap' | 'race' }>;
}

export interface StandingsScreenData {
  champ: ChampionshipState;
  humans: HumanEntry[];
  cup: CupDef;
}

export interface MenuContext {
  root: HTMLElement;
  input: InputProvider;
  settings: Settings;
  save: SaveData;
  cups: CupDef[];
  tracks: TrackDef[];
  cars: CarDef[];
  isCupUnlocked(cupId: string): boolean;
  trackOutline: TrackOutlineFn;
  audio: AudioEngine;
  /** Rodando dentro do Electron (mostra "Sair"). */
  isDesktop: boolean;
  onEvent(event: MenuEvent): void;
}

export interface Menus {
  show(screen: 'results', data: ResultsScreenData): void;
  show(screen: 'standings', data: StandingsScreenData): void;
  show(screen: Exclude<MenuScreen, 'results' | 'standings'>): void;
  hide(): void;
  current(): MenuScreen | null;
  /** Navegação por gamepad, chamada a cada quadro pela sessão (o teclado e o mouse os menus tratam sozinhos). */
  navigate(nav: MenuNav): void;
  /** Uma vez por quadro (animações, entrada de jogadores no lobby via input.joinPressed()). */
  update(dt: number): void;
  /** Reaplica os textos no idioma atual. */
  refreshLanguage(): void;
}
