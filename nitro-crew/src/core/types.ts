// Tipos compartilhados do núcleo. O estado da corrida é JSON puro (sem Map/Set/classes) para
// que serializar seja `JSON.stringify` e o multiplayer em lockstep possa comparar hashes.
import type { RngState } from './rng';

// ───────────────────────────── Pista ─────────────────────────────

export type SceneryId = 'tropical' | 'desert' | 'city_night' | 'alpine' | 'coast' | 'savanna';
export type TimeOfDay = 'day' | 'dusk' | 'night';

/** Operações do "DSL" de pista. Comprimentos em segmentos. */
export type TrackOp =
  | { op: 'straight'; length: number }
  | { op: 'curve'; length: number; curve: number; hill?: number }
  | { op: 'hill'; length: number; height: number }
  | { op: 's'; length: number; curve: number }
  | { op: 'pit'; length: number };

export interface TrackDef {
  id: string;
  name: string;
  country: string;
  scenery: SceneryId;
  timeOfDay: TimeOfDay;
  laps: number;
  /** 1 (fácil) a 5 (difícil), só informativo para a interface. */
  difficulty: number;
  ops: TrackOp[];
}

export type SpriteKind =
  | 'tree' | 'pine' | 'palm' | 'cactus' | 'bush' | 'boulder' | 'building' | 'tower'
  | 'lamp' | 'billboard' | 'sign_left' | 'sign_right' | 'grandstand' | 'banner_start'
  | 'pit_wall' | 'pit_sign' | 'cone';

export interface SpriteRef {
  kind: SpriteKind;
  /** Deslocamento lateral em meias-larguras de pista: -1 e 1 são as bordas do asfalto. */
  x: number;
  /** Escala relativa (1 = tamanho padrão do sprite). */
  scale: number;
  /** Bater nele derruba a velocidade. */
  solid: boolean;
  /** Variante visual (0..n), para texto de outdoor, cor de prédio etc. */
  variant: number;
}

export interface Segment {
  index: number;
  /** Posição do início do segmento ao longo da pista. */
  z: number;
  /** Curvatura: negativo vira à esquerda, positivo à direita. */
  curve: number;
  /** Elevação no início e no fim do segmento. */
  y0: number;
  y1: number;
  /** Alternância de cor (faixas de zebra). */
  band: 0 | 1;
  /** Este trecho tem box (lateral direita, x ≥ PIT_X). */
  pit: boolean;
  sprites: SpriteRef[];
}

export interface Track {
  def: TrackDef;
  segments: Segment[];
  /** Comprimento total (segments.length * SEGMENT_LENGTH). */
  length: number;
  /** Segmento em que fica a linha de largada/chegada. */
  startIndex: number;
}

// ───────────────────────────── Carros e pilotos ─────────────────────────────

export interface CarDef {
  id: string;
  name: string;
  /** Cor base em hex (#rrggbb), usada pelo renderizador procedural. */
  color: string;
  /** Velocidade máxima em unidades por segundo (REFERENCE_SPEED = 300 km/h). */
  topSpeed: number;
  /** Aceleração em unidades por segundo ao quadrado. */
  accel: number;
  /** Força de frenagem (positiva). */
  brake: number;
  /** 0..1: quanto o carro responde ao volante e resiste à força centrífuga. */
  handling: number;
  /** Combustível gasto por unidade de distância na velocidade máxima (tanque = 1). */
  fuelPerUnit: number;
  /** Texto curto de apresentação. */
  blurb: string;
}

export type Difficulty = 'amador' | 'profissional' | 'campeao';

export interface CoopAssists {
  /** Todos os nitros da equipe vão para um cofre comum. */
  sharedNitro: boolean;
  /** Companheiro que passa perto de um parado dá um empurrão. */
  tow: boolean;
  /** Vácuo atrás de companheiro rende mais. */
  teamDraft: boolean;
  /** O último humano da equipe ganha um pouco de velocidade quando fica longe. */
  catchup: boolean;
}

export interface HumanEntry {
  /** Assento local 0..3. */
  seat: number;
  name: string;
  carId: string;
  /** Todos os humanos no mesmo time = cooperativo; times diferentes = versus. */
  teamId: number;
  /** Cor do jogador na interface (hex). */
  color: string;
}

export interface RaceConfig {
  trackId: string;
  laps: number;
  humans: HumanEntry[];
  /** Total de carros na pista, humanos incluídos. */
  totalCars: number;
  difficulty: Difficulty;
  manualGear: boolean;
  assists: CoopAssists;
  seed: number;
  /** Semente do elenco da IA (nomes e carros). Fixa por copa, para a classificação fazer sentido. */
  rosterSeed?: number;
  /** Contra-relógio: só humanos, sem combustível. */
  timeTrial?: boolean;
}

// ───────────────────────────── Estado ─────────────────────────────

export interface PlayerInput {
  /** -1 (esquerda) a 1 (direita). */
  steer: number;
  throttle: boolean;
  brake: boolean;
  /** Borda: verdadeiro só no tick em que foi apertado. */
  nitro: boolean;
  gearUp: boolean;
  gearDown: boolean;
}

export const NEUTRAL_INPUT: Readonly<PlayerInput> = Object.freeze({
  steer: 0, throttle: false, brake: false, nitro: false, gearUp: false, gearDown: false,
});

export interface AiBrain {
  /** 0..1, multiplica a velocidade-alvo. */
  skill: number;
  /** Faixa preferida (-0.7..0.7). */
  laneX: number;
  /** Tick em que pode mudar de faixa de novo. */
  laneUntil: number;
  /** Quantos ticks de pista à frente ele olha para frear em curva. */
  lookahead: number;
  aggression: number;
}

export interface CarState {
  id: number;
  /** Assento do humano (0..3) ou -1 para a IA. */
  seat: number;
  name: string;
  teamId: number;
  carId: string;
  /** Posição ao longo da pista, 0 ≤ z < track.length. */
  z: number;
  /** Lateral normalizado (-1..1 asfalto). */
  x: number;
  speed: number;
  gear: number;
  fuel: number;
  nitroLeft: number;
  nitroTicks: number;
  /** Volta atual (1 = primeira). */
  lap: number;
  /** Ticks das voltas completadas. */
  lapTicks: number[];
  lapStartTick: number;
  finished: boolean;
  finishTick: number;
  /** Posição na corrida (1 = líder), atualizada a cada tick. */
  position: number;
  /** Progresso total = (lap - 1) * length + z, para ordenar. */
  progress: number;
  inPit: boolean;
  collisionCooldown: number;
  towCooldown: number;
  /** Ticks restantes do "empurrão"/derrapagem para efeitos visuais. */
  skidTicks: number;
  /** Última direção do volante (-1, 0, 1) para a pose do sprite. */
  steerPose: number;
  ai: AiBrain | null;
}

export type RacePhase = 'countdown' | 'racing' | 'finished';

export type SimEvent =
  | { type: 'countdown'; value: number }
  | { type: 'go' }
  | { type: 'lap'; carId: number; lap: number; lapTicks: number; best: boolean }
  | { type: 'finish'; carId: number; position: number }
  | { type: 'nitro'; carId: number }
  | { type: 'nitro_denied'; carId: number }
  | { type: 'collision'; carId: number; otherId: number; strength: number }
  | { type: 'crash'; carId: number; sprite: SpriteKind }
  | { type: 'offroad'; carId: number; entering: boolean }
  | { type: 'tow'; carId: number; byId: number }
  | { type: 'pit_enter'; carId: number }
  | { type: 'pit_exit'; carId: number }
  | { type: 'fuel_low'; carId: number }
  | { type: 'fuel_empty'; carId: number }
  | { type: 'gear'; carId: number; gear: number }
  | { type: 'race_over' };

export interface RaceResultRow {
  carId: number;
  seat: number;
  name: string;
  teamId: number;
  carDefId: string;
  position: number;
  finished: boolean;
  totalTicks: number;
  bestLapTicks: number;
  points: number;
}

export interface RaceState {
  tick: number;
  phase: RacePhase;
  config: RaceConfig;
  trackId: string;
  trackLength: number;
  cars: CarState[];
  rng: RngState;
  /** Cofre de nitro por time (só usado com assists.sharedNitro). */
  teamNitro: Record<number, number>;
  /** Tick em que a corrida começou de fato (fim da contagem). */
  startTick: number;
  /** Tick em que o primeiro humano terminou (-1 se nenhum). */
  firstHumanFinishTick: number;
  /** Eventos gerados no último tick (limpos a cada passo). */
  events: SimEvent[];
  results: RaceResultRow[] | null;
}

// ───────────────────────────── Campeonato ─────────────────────────────

export interface CupDef {
  id: string;
  name: string;
  country: string;
  flag: string;
  trackIds: string[];
  /** Copa que precisa ter sido vencida (ou qualificada) para destravar esta. */
  requires: string | null;
}

export interface StandingRow {
  /** Nome estável do participante (humanos: "seat:N"; IA: nome do piloto). */
  key: string;
  name: string;
  seat: number;
  teamId: number;
  points: number;
  wins: number;
  /** Posições por corrida (0 = não correu). */
  positions: number[];
}

export interface TeamStandingRow {
  teamId: number;
  name: string;
  points: number;
  isHuman: boolean;
}

export interface ChampionshipState {
  cupId: string;
  /** Índice da próxima corrida a disputar. */
  raceIndex: number;
  standings: StandingRow[];
  teams: TeamStandingRow[];
  /** Modo cooperativo: todos os humanos no time 0. */
  coop: boolean;
  /** Eliminado (não cumpriu a regra de classificação). */
  eliminated: boolean;
  /** Copa concluída (todas as corridas disputadas sem eliminação). */
  completed: boolean;
  /** Resultado da última corrida (para a tela de resultados). */
  lastRace: RaceResultRow[] | null;
  /** Qual regra derrubou o jogador na última corrida, para a mensagem da interface. */
  lastVerdict: 'qualified' | 'eliminated' | null;
}
