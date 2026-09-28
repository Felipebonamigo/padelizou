// Corridas de referência da simulação: as mesmas servem para medir o custo por tick (scripts/perf-sim.ts)
// e para provar que uma otimização não muda nada (tests/sim-golden.test.ts compara a impressão digital
// de cada corrida inteira com um valor fixo). Os humanos são pilotos de roteiro determinístico — sem
// `car.ai` — para a direção assistida, o câmbio manual e a tomada pela IA passarem pelo caminho real.
import { GEAR_TOP } from '../src/core/constants';
import { CUP_RIVALS, seatColor } from '../src/core/data/drivers';
import { hashRace, serializeRace } from '../src/core/serialize';
import { brakingTarget, counterSteer } from '../src/core/sim/ai';
import { createRace, stepRace } from '../src/core/sim/race';
import { getTrack, segmentAt } from '../src/core/track';
import type { AssistLevel, CoopAssists, CoreMode, Difficulty, HumanEntry, PlayerInput, RaceConfig, RaceState, Track } from '../src/core/types';

export const ALL_ASSISTS: CoopAssists = { sharedNitro: true, tow: true, teamDraft: true, catchup: true };
export const NO_ASSISTS: CoopAssists = { sharedNitro: false, tow: false, teamDraft: false, catchup: false };

const CARS_BY_SEAT = ['falcao', 'trovao', 'tornado', 'camelo'];
/** Faixa de cada assento, para os humanos não andarem em fila indiana. */
const LANE_BY_SEAT = [-0.45, 0.45, -0.15, 0.15];

export interface SimScenario {
  name: string;
  trackId: string;
  humans: number;
  difficulty?: Difficulty;
  assists?: CoopAssists;
  /** Direção assistida de cada assento (ausente = 'none'). */
  assistLevels?: AssistLevel[];
  /** Times diferentes por assento (versus); ausente = todos no time 0 (co-op). */
  versus?: boolean;
  manualGear?: boolean;
  mode?: CoreMode;
  timeTrial?: boolean;
  aiLevel?: number;
  rival?: string;
  /** Tira a personalidade de toda a IA depois do grid (mede o custo delas). */
  noPersonalities?: boolean;
  /** Assento que a IA assume (online) a partir do tick dado. */
  takeover?: { seat: number; tick: number };
  laps?: number;
  seed: number;
}

export function scenarioConfig(s: SimScenario, track: Track): RaceConfig {
  const humans: HumanEntry[] = Array.from({ length: s.humans }, (_, seat) => ({
    seat, name: `P${seat + 1}`, carId: CARS_BY_SEAT[seat], teamId: s.versus ? seat : 0, color: seatColor(seat),
    assist: s.assistLevels?.[seat],
  }));
  return {
    trackId: track.def.id, laps: s.laps ?? 2, humans, totalCars: 20, difficulty: s.difficulty ?? 'profissional',
    manualGear: s.manualGear ?? false, assists: s.assists ?? NO_ASSISTS, seed: s.seed, timeTrial: s.timeTrial,
    aiLevel: s.aiLevel, rival: s.rival, mode: s.mode,
  };
}

export function startScenario(s: SimScenario): { state: RaceState; track: Track } {
  const track = getTrack(s.trackId);
  const state = createRace(scenarioConfig(s, track), track);
  if (s.noPersonalities) for (const c of state.cars) if (c.ai) delete c.ai.personality;
  return { state, track };
}

function clamp(v: number, lo: number, hi: number): number {
  return v < lo ? lo : v > hi ? hi : v;
}

/**
 * Piloto de roteiro de um assento: segue a própria faixa com contra-esterço, freia no ponto da
 * direção assistida (um pouco otimista, para errar às vezes), troca de marcha perto do corte e
 * usa o nitro de tempos em tempos. Só lê o estado; nada de sorteio.
 */
export function scriptedInput(state: RaceState, track: Track, seat: number, s: SimScenario): PlayerInput {
  const input: PlayerInput = { steer: 0, throttle: true, brake: false, nitro: false, gearUp: false, gearDown: false };
  if (s.takeover && seat === s.takeover.seat && state.tick >= s.takeover.tick) input.takeover = true;
  const car = state.cars.find((c) => c.seat === seat);
  if (!car) return input;
  const def = car.stats;
  const sf = Math.min(1, car.speed / def.topSpeed);
  const seg = segmentAt(track, car.z);
  input.steer = clamp((LANE_BY_SEAT[seat] - car.x) * 4 + counterSteer(def, sf, seg.curve), -1, 1);
  const target = brakingTarget(track, car.z, def, 1.08, 20 + Math.floor(sf * 30), 2) * def.topSpeed;
  if (car.speed > target * 1.08) { input.throttle = false; input.brake = true; }
  input.nitro = (state.tick + seat * 97) % 700 === 0;
  if (s.manualGear) {
    const gearFrac = car.speed / (def.topSpeed * GEAR_TOP[car.gear]);
    if (gearFrac > 0.95 && car.gear < GEAR_TOP.length - 1) input.gearUp = true;
    else if (car.gear > 0 && car.speed < def.topSpeed * GEAR_TOP[car.gear - 1] * 0.7) input.gearDown = true;
  }
  return input;
}

/** Entradas de todos os assentos neste tick. */
export function scenarioInputs(state: RaceState, track: Track, s: SimScenario): PlayerInput[] {
  const inputs: PlayerInput[] = [];
  for (let seat = 0; seat < s.humans; seat++) inputs[seat] = scriptedInput(state, track, seat, s);
  return inputs;
}

/** Continua um FNV-1a de 32 bits (o mesmo do rng.hashString) com mais texto. */
function fnv(h: number, text: string): number {
  for (let i = 0; i < text.length; i++) { h ^= text.charCodeAt(i); h = Math.imul(h, 0x01000193) >>> 0; }
  return h >>> 0;
}

/** Limite de ticks de uma corrida de referência (15 min de corrida). */
export const SCENARIO_MAX_TICKS = 60 * 60 * 15;

export interface ScenarioRun {
  /** Impressão digital da corrida inteira: hash de cada tick, todos os eventos e o estado completo a cada 600 ticks e no fim. */
  fingerprint: string;
  ticks: number;
  finished: boolean;
}

/** Roda a corrida inteira e devolve a impressão digital (tests/sim-golden.test.ts). */
export function fingerprintScenario(s: SimScenario): ScenarioRun {
  const { state, track } = startScenario(s);
  let h = 0x811c9dc5;
  let ticks = 0;
  while (state.phase !== 'finished' && ticks < SCENARIO_MAX_TICKS) {
    stepRace(state, track, scenarioInputs(state, track, s));
    ticks++;
    h = fnv(h, String(hashRace(state)));
    if (state.events.length) h = fnv(h, JSON.stringify(state.events));
    if (ticks % 600 === 0) h = fnv(h, serializeRace(state));
  }
  h = fnv(h, serializeRace(state));
  return { fingerprint: h.toString(16).padStart(8, '0'), ticks, finished: state.phase === 'finished' };
}

/** As corridas de referência: um pouco de tudo que o stepRace sabe fazer. */
export const GOLDEN_SCENARIOS: readonly SimScenario[] = [
  { name: 'solo-sem-assistencias', trackId: 'copacabana', humans: 1, seed: 101 },
  {
    name: 'coop4-tudo', trackId: 'serra_do_mar', humans: 4, difficulty: 'campeao', assists: ALL_ASSISTS,
    assistLevels: ['none', 'brake', 'steer', 'full'], aiLevel: 1.5, rival: CUP_RIVALS.brasil, seed: 202,
  },
  { name: 'versus-cambio-manual', trackId: 'sampa_noite', humans: 2, versus: true, manualGear: true, difficulty: 'amador', seed: 303 },
  { name: 'escolta', trackId: 'rota_66', humans: 2, mode: 'escort', assists: ALL_ASSISTS, assistLevels: ['full', 'none'], seed: 404 },
  { name: 'revezamento', trackId: 'monte_fuji', humans: 4, mode: 'relay', assists: ALL_ASSISTS, laps: 3, seed: 505 },
  { name: 'tomada-pela-ia', trackId: 'las_vegas', humans: 3, assists: ALL_ASSISTS, takeover: { seat: 1, tick: 900 }, seed: 606 },
  { name: 'contra-relogio', trackId: 'passo_alpino', humans: 1, timeTrial: true, seed: 707 },
  { name: 'sem-personalidades', trackId: 'daintree', humans: 2, noPersonalities: true, difficulty: 'campeao', seed: 808 },
];
