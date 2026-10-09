// Contratos entre as camadas. O núcleo (src/core) não conhece nada disto; a sessão
// (src/game/session.ts) liga as implementações. Cada camada mora numa pasta própria:
//   src/render  → Renderer            src/ui     → Menus + InputProvider
//   src/audio   → AudioEngine         src/game   → settings.ts, save.ts, desktop.ts, session.ts
import type { ColorPalette } from '../core/data/drivers';
import type {
  AssistLevel, CarDef, ChampionshipState, CoopAssists, CupDef, Difficulty, HumanEntry, PlayerInput, RaceResultRow, RaceState,
  SimEvent, Track, TrackDef,
} from '../core/types';
import type { CareerState } from '../core/career';
import type { Lang } from '../i18n';
import type { AchievementUnlock } from './achievements';
import { EMPTY_STATS, type StatsData } from './stats';
import { DEFAULT_BINDINGS, type ControlBindings } from '../ui/remap/bindings';
import type { OnlineController } from './online-session';
import type { PartyController } from './party-session';
import type { TournamentSetup } from '../core/tournament';

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
/** Estilo visual: o 3D moderno (src/render) ou o pseudo-3D Retrô (src/render-pseudo3d). */
export type RenderStyle = 'modern' | 'retro';

export interface Settings {
  language: Lang;
  masterVolume: number; // 0..1
  musicVolume: number;
  sfxVolume: number;
  fullscreen: boolean;
  quality: Quality;
  /** Visual da corrida: moderno (3D) ou Retrô (pseudo-3D, estilo Top Gear). */
  renderStyle: RenderStyle;
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
  /**
   * Telemetria anônima de erros (opt-in): versão dos termos que o jogador aceitou ao ligar; 0 = desligada (padrão).
   * Só vale se for a versão em vigor — `telemetryConsented` em src/game/errors.ts; ver docs/legal/PRIVACIDADE.md.
   */
  telemetryConsent: number;
  /** Tecla/botão por ação de pilotagem, por dispositivo (src/ui/remap/bindings.ts). */
  controls: ControlBindings;
  /** Vibração dos gamepads (batidas, nitro, grama, largada). */
  vibration: boolean;
  /** Endereço do servidor de retransmissão do online (ws:// ou wss://). */
  serverUrl: string;
  /** Fantasma da melhor volta no contra-relógio (carro translúcido e diferença no HUD); a gravação segue mesmo desligado. */
  ghost: boolean;
  // Acessibilidade (docs/ASSISTENCIAS.md; aplicada por src/access/apply.ts).
  /** Direção assistida por assento (P1..P4), escolhida no lobby ou nas opções. */
  seatAssists: AssistLevel[];
  /** Paleta das cores dos jogadores (daltonismo). */
  colorPalette: ColorPalette;
  /** Tamanho do HUD, 0,8..1,5 (80–150%). */
  hudScale: number;
  /** Texto maior nos menus. */
  largeText: boolean;
  /** Sem tremor de câmera, linhas de velocidade, piscadas e faíscas. */
  reduceEffects: boolean;
  /** Legenda com o nome do marco turístico quando ele entra bem à vista (src/render/caption/, Opções › Acessibilidade). */
  landmarkCaptions: boolean;
}

export const DEFAULT_SETTINGS: Readonly<Settings> = Object.freeze({
  language: 'pt', masterVolume: 0.8, musicVolume: 0.6, sfxVolume: 0.9, fullscreen: false, quality: 'high', renderStyle: 'modern',
  showMinimap: true, screenShake: true, difficulty: 'profissional', manualGear: false,
  assists: { sharedNitro: true, tow: true, teamDraft: true, catchup: true }, totalCars: 20, quickLaps: 3, music: 'auto',
  telemetryConsent: 0,
  controls: DEFAULT_BINDINGS, vibration: true,
  serverUrl: 'ws://localhost:8787',
  ghost: true,
  seatAssists: ['none', 'none', 'none', 'none'] as AssistLevel[], colorPalette: 'default' as ColorPalette, hudScale: 1, largeText: false, reduceEffects: false,
  landmarkCaptions: true,
});

export interface BestLap {
  ticks: number;
  name: string;
  carId: string;
  date: string;
  /** Impressão da pista e do carro quando o recorde foi feito (content-version.ts); ausente nos de antes dela. */
  fp?: string;
}

export interface SaveData {
  /** Copas concluídas (destravam a seguinte). */
  cupsCompleted: string[];
  /**
   * Copas abertas sem a anterior concluída: herança de save antigo (antes da onda G, vencer a Copa Brasil abria os
   * EUA, que hoje exigem a Expedição inteira). Só a migração do save põe copa aqui (`legacyCupOpens`, save.ts).
   */
  cupsUnlocked: string[];
  /** Passaporte da Expedição Brasil: siglas dos estados carimbados (copa do estado vencida), na ordem em que vieram. */
  stamps: string[];
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
  /**
   * Pintura escolhida por assento (P1..P4) para cada carro: carro → id da paleta (src/game/paints.ts). Carro
   * ausente = a Original (a de fábrica). Gravada na hora da escolha (lobby, garagem, sala online). Ver docs/SAVE.md.
   */
  seatPaints: Array<Record<string, string>>;
  /** Estatísticas por jogador (nome do lobby) e totais — src/game/stats.ts. */
  stats: StatsData;
  /** Carros comprados em alguma carreira: ficam liberados em todas as modalidades. */
  carsUnlocked: string[];
  /** Carreira salva (em andamento ou concluída); null = nenhuma. Ver docs/CARREIRA.md. */
  career: CareerState | null;
  /** Campeonato normal em andamento, salvo a cada corrida (menu principal → Continuar); null = nenhum. */
  cupInProgress: SavedCup | null;
  /** Tutorial concluído ou pulado: o menu principal para de oferecê-lo (src/game/tutorial-session.ts). */
  tutorialDone: boolean;
}

/** Copa normal salva: a classificação até aqui, a semente do elenco e os humanos (assentos 0..n-1). */
export interface SavedCup {
  champ: ChampionshipState;
  cupSeed: number;
  humans: HumanEntry[];
}

export const DEFAULT_SAVE: Readonly<SaveData> = Object.freeze({
  cupsCompleted: [], cupsUnlocked: [], stamps: [], bestLaps: {}, bestRaces: {}, achievements: [], racesRun: 0, racesWon: 0,
  seatNames: ['P1', 'P2', 'P3', 'P4'], seatCars: ['falcao', 'trovao', 'tornado', 'camelo'], seatPaints: [{}, {}, {}, {}],
  stats: EMPTY_STATS,
  carsUnlocked: [], career: null, cupInProgress: null, tutorialDone: false,
});

// ───────────────────────────── Renderização ─────────────────────────────

export interface RenderOptions {
  quality: Quality;
  showMinimap: boolean;
  screenShake: boolean;
  /** Reduzir efeitos (acessibilidade): sem tremor, linhas de velocidade, piscadas e faíscas. */
  reduceEffects: boolean;
  /** Paleta das cores dos jogadores (os de outro computador, no online, não têm viewport aqui). */
  palette: ColorPalette;
  /** Legenda dos marcos turísticos (src/render/caption/); ausente = ligada. O modo Retrô não tem marcos nem legenda. */
  landmarkCaptions?: boolean;
}

/** Cores de um carro na pista quando o jogador o pintou (src/game/paints.ts): carroceria e segunda cor. */
export interface CarColors {
  color: string;
  accent: string;
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

/** Pose do fantasma do contra-relógio num instante (src/game/ghost.ts: ghostPoseAt). */
export interface GhostPose {
  z: number;
  x: number;
  speed: number;
  /** -1, 0, 1, como CarState.steerPose. */
  steerPose: number;
  nitro: boolean;
}

/** O fantasma na tela: só visual, fora do estado da corrida e sem colisão. */
export interface GhostFrame {
  /** Onde ele está agora; null fora da volta cronometrada (largada, depois de ele cruzar a linha, corrida acabada). */
  pose: GhostPose | null;
  carId: string;
  /** Diferença ao vivo em segundos para o carro do primeiro viewport (positivo = atrás do fantasma); null sem referência. */
  delta: number | null;
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
  /** Fantasma do contra-relógio (ausente fora dele ou com a opção desligada). */
  ghost?: GhostFrame;
  /**
   * Pintura de cada carro (índice = `state.cars`); null ou ausente = a de fábrica (CarDef.color/accent). Só
   * aparência, montada pela sessão na largada (racePaints em src/game/paints.ts): o estado da corrida não a tem.
   */
  paints?: ReadonlyArray<CarColors | null>;
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

// ───────────────────────────── Fonte das entradas por tick ─────────────────────────────

/**
 * Quem decide quando cada tick da corrida roda e com que entradas. Sem driver, a sessão lê os
 * controles deste computador a cada tick (sofá). Online, o lockstep (src/game/online-session.ts)
 * só libera o tick quando chegou a entrada de todos os assentos.
 */
export interface RaceDriver {
  /**
   * Um quadro: `dt` em segundos, `local[seat]` = o que os assentos deste computador apertam agora.
   * Chama `step` uma vez por tick liberado; devolve quantos rodaram.
   */
  advance(dt: number, local: PlayerInput[], step: (inputs: PlayerInput[]) => void): number;
  /** Como `advance`, mas tenta rodar até `ticks` agora, sem olhar o relógio (playtest). */
  force(ticks: number, local: PlayerInput[], step: (inputs: PlayerInput[]) => void): number;
  /** Esc/Start durante a corrida (o online não pausa). */
  pauseKey(): void;
  /** A corrida acabou: o driver mostra o resultado do jeito dele. */
  finished(data: ResultsScreenData): void;
  /** A sessão largou a corrida (menu principal). */
  dispose(): void;
}

// ───────────────────────────── Menus ─────────────────────────────

export type MenuScreen = 'title' | 'main' | 'lobby' | 'cups' | 'tracks' | 'results' | 'standings' | 'pause' | 'options' | 'controls' | 'records' | 'credits' | 'loading' | 'career' | 'garage' | 'online'
  | 'party' | 'tournament' | 'handoff' | 'tournamentTable' | 'access'
  | 'tutorial' | 'tutorialDone' | 'passport';

/** Modos de festa, só locais (docs/MODOS.md; src/game/party-session.ts). */
export type PartyMode = 'tournament' | 'escort' | 'relay';

export type RaceMode = 'cup' | 'quick' | 'timetrial' | 'career' | PartyMode;

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
  | { type: 'quitApp' }
  /** Lobby da carreira pronto: começa uma carreira nova ou continua a salva (assentos já ligados). */
  | { type: 'startCareer'; humans: HumanEntry[]; resume: boolean }
  /** Garagem: todos prontos, corre a próxima corrida da copa atual. */
  | { type: 'careerRace' }
  /** Lobby de "Continuar": retoma o campeonato normal salvo. */
  | { type: 'continueCup' }
  /** Festa: escolta ou revezamento na pista escolhida (lobby → pistas). */
  | { type: 'startParty'; mode: 'escort' | 'relay'; trackId: string; laps: number; humans: HumanEntry[] }
  /** Torneio de sofá: inscrição pronta; `seats` = assentos com controle ligado no lobby. */
  | { type: 'startTournament'; setup: TournamentSetup; seats: number[] }
  /** Tela "passe o controle": todos com o controle na mão, larga a bateria atual do torneio. */
  | { type: 'tournamentHeat' }
  /** "Como jogar": corrida guiada com quem entrou na tela do tutorial (assentos já ligados). */
  | { type: 'startTutorial'; humans: HumanEntry[] }
  /** Fim do tutorial → "Primeira copa", com os mesmos jogadores. */
  | { type: 'tutorialFirstCup' };

/** O que o resultado mostra de um modo de festa (preenchido pela sessão da festa no fim da corrida). */
export type PartyResultsInfo =
  | { kind: 'escort'; vipPosition: number; success: boolean; vipCarId: number }
  | { kind: 'relay'; swaps: Array<{ name: string; swaps: number }> }
  | { kind: 'tournament'; label: string };

export interface ResultsScreenData {
  mode: RaceMode;
  trackDef: TrackDef;
  results: RaceResultRow[];
  humans: HumanEntry[];
  champ: ChampionshipState | null;
  /** Recordes batidos nesta corrida (por assento), para destacar. */
  newRecords: Array<{ seat: number; kind: 'lap' | 'race'; first?: boolean }>;
  /** Conquistas desbloqueadas nesta corrida e quem as ganhou (opcional: ausente = nenhuma). */
  achievements?: AchievementUnlock[];
  /** Modo de festa: veredito da escolta, trocas do revezamento, bateria do torneio. */
  party?: PartyResultsInfo;
  /** Expedição Brasil: sigla do estado carimbado no passaporte nesta corrida (o resultado anuncia). */
  newStamp?: string | null;
}

/** Tela final do tutorial: concluído (parabéns) ou pulado/sem tempo (só as regras de ouro). */
export interface TutorialDoneData {
  completed: boolean;
  players: number;
}

export interface StandingsScreenData {
  champ: ChampionshipState;
  humans: HumanEntry[];
  cup: CupDef;
  /** Copa da carreira: o botão leva à garagem. */
  career?: boolean;
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
  /** Online (sala, lobby em rede, corrida em lockstep); ausente onde não há sessão completa. */
  online?: OnlineController;
  /** Modos de festa (torneio em andamento, inscrição); ausente onde não há sessão completa. */
  party?: PartyController;
  onEvent(event: MenuEvent): void;
}

export interface Menus {
  show(screen: 'results', data: ResultsScreenData): void;
  show(screen: 'standings', data: StandingsScreenData): void;
  show(screen: 'tutorialDone', data: TutorialDoneData): void;
  show(screen: Exclude<MenuScreen, 'results' | 'standings' | 'tutorialDone'>): void;
  hide(): void;
  current(): MenuScreen | null;
  /** Navegação por gamepad, chamada a cada quadro pela sessão (o teclado e o mouse os menus tratam sozinhos). */
  navigate(nav: MenuNav): void;
  /** Uma vez por quadro (animações, entrada de jogadores no lobby via input.joinPressed()). */
  update(dt: number): void;
  /** Reaplica os textos no idioma atual. */
  refreshLanguage(): void;
}
