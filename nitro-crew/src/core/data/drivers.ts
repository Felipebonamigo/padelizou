// Pilotos e equipes controlados pela IA. Nomes inventados.
import type { Personality } from '../types';

export const AI_DRIVERS: readonly string[] = [
  'Tico Ramos', 'Duda Ferraz', 'Kenji Sato', 'Lars Berg', 'Nina Costa', 'Rafa Moura', 'Hugo Klein', 'Bianca Rey',
  'Marco Rossi', 'Léo Prado', 'Yuki Mori', 'Zé Turbo', 'Ana Volpi', 'Pierre Duval', 'Caio Brasa', 'Sasha Kova',
  'Tom Blake', 'Iris Lund', 'Dani Souza', 'Otto Weiss',
];

/** Times da IA: os pilotos entram aos pares (índices 0-1, 2-3, …). */
export const AI_TEAMS: readonly string[] = [
  'Escuderia Sol', 'Garagem 21', 'Turbo Norte', 'Vento Sul', 'Neon Motors', 'Rocha Racing',
  'Maré Alta', 'Cometa', 'Pantera', 'Relâmpago',
];

/** Cores dos assentos humanos (P1..P4). */
export const SEAT_COLORS: readonly string[] = ['#ffd23f', '#3ddc84', '#4fc3f7', '#ff7ab6'];

/** Nome do time humano no modo cooperativo. */
export const HUMAN_TEAM_NAME = 'Sua equipe';
export const HUMAN_TEAM_ID = 0;
export const AI_TEAM_ID_BASE = 100;

// ───────────────────────────── Personalidades ─────────────────────────────

/**
 * Personalidade fixa de cada piloto (docs/RIVAIS.md). Cinco de cada: a média do grid fica perto do
 * piloto neutro de antes (o agressivo ganha tempo na frenagem, o errático perde na grama).
 */
export const DRIVER_PERSONALITY: Readonly<Record<string, Personality>> = {
  'Kenji Sato': 'clean', 'Iris Lund': 'clean', 'Pierre Duval': 'clean', 'Nina Costa': 'clean', 'Ana Volpi': 'clean',
  'Zé Turbo': 'aggressive', 'Sasha Kova': 'aggressive', 'Caio Brasa': 'aggressive', 'Rafa Moura': 'aggressive', 'Marco Rossi': 'aggressive',
  'Tom Blake': 'blocker', 'Hugo Klein': 'blocker', 'Otto Weiss': 'blocker', 'Duda Ferraz': 'blocker', 'Lars Berg': 'blocker',
  'Bianca Rey': 'erratic', 'Tico Ramos': 'erratic', 'Léo Prado': 'erratic', 'Dani Souza': 'erratic', 'Yuki Mori': 'erratic',
};

/** Personalidade do piloto pelo nome; nome desconhecido (ou humano) = null, o piloto neutro. */
export function personalityOf(name: string): Personality | null {
  return Object.prototype.hasOwnProperty.call(DRIVER_PERSONALITY, name) ? DRIVER_PERSONALITY[name] : null;
}

/**
 * Como cada personalidade pilota (sim/ai.ts). O piloto neutro (sem personalidade) usa NEUTRAL_TUNING,
 * que reproduz a IA de antes das personalidades — multiplicadores 1 e os números antigos.
 */
export interface PersonalityTuning {
  /** Multiplica a desaceleração que a IA supõe ao calcular o ponto de frenagem (>1 freia mais tarde). */
  brakeLate: number;
  /** Multiplica a velocidade que a IA acha que segura em cada curva. */
  cornerSpeed: number;
  /** Faixa por dentro da curva: quanto se desloca para o lado de dentro. */
  insideLine: number;
  /** Atrás de um carro andando: iguala a velocidade dele vezes isto (>1 cola na traseira e bate). */
  follow: number;
  /** Distância, em segmentos, em que passa a desviar de quem está na frente (menos = chega mais perto). */
  avoidRange: number;
  /** Distância lateral com que passa por quem está na frente. */
  passOffset: number;
  /** Ticks mínimos entre duas trocas de faixa para desviar. */
  laneHold: number;
  /** Chance, por tick, de trocar de faixa por conta própria. */
  wander: number;
  /** Chance de nitro por tick numa reta livre: base + agressividade do cérebro × isto. */
  nitroBase: number;
  nitroAggression: number;
  /** Fração da velocidade máxima a partir da qual usa nitro. */
  nitroMinSpeed: number;
  /** Maior curva tolerada à frente para usar nitro, e quantos segmentos ele olha. */
  nitroCurveMax: number;
  nitroLook: number;
  /** Chance, por trecho de curva e por volta, de errar o ponto de frenagem (sim/personality.ts). */
  mistake: number;
  /** Fecha a porta de um humano logo atrás (sim/personality.ts). */
  blocks: boolean;
}

export const NEUTRAL_TUNING: Readonly<PersonalityTuning> = Object.freeze({
  brakeLate: 1, cornerSpeed: 1, insideLine: 0.35, follow: 1.02, avoidRange: 5, passOffset: 0.55, laneHold: 45, wander: 0.004,
  nitroBase: 0.015, nitroAggression: 0.02, nitroMinSpeed: 0.85, nitroCurveMax: 2, nitroLook: 12, mistake: 0, blocks: false,
});

export const PERSONALITY_TUNING: Readonly<Record<Personality, Readonly<PersonalityTuning>>> = {
  // Traçado ideal (mais por dentro), passa largo, não cola em ninguém, nitro só em reta longa.
  clean: { ...NEUTRAL_TUNING, cornerSpeed: 0.99, insideLine: 0.45, follow: 0.98, avoidRange: 6, passOffset: 0.65, laneHold: 60, wander: 0.002, nitroCurveMax: 1, nitroLook: 40 },
  // Freia mais tarde, cola na traseira, passa raspando, troca de faixa toda hora, mais nitro — e erra mais.
  aggressive: { ...NEUTRAL_TUNING, brakeLate: 1.12, cornerSpeed: 1.015, follow: 1.04, avoidRange: 3, passOffset: 0.42, laneHold: 25, wander: 0.008, nitroBase: 0.04, nitroAggression: 0.04, nitroMinSpeed: 0.75, nitroCurveMax: 3, mistake: 0.05 },
  // Pilota como o neutro; a diferença é fechar a porta (sim/personality.ts).
  blocker: { ...NEUTRAL_TUNING, blocks: true },
  // Às vezes erra o ponto de frenagem e vai à grama; troca de faixa sem motivo.
  erratic: { ...NEUTRAL_TUNING, wander: 0.008, mistake: 0.12 },
};

// ───────────────────────────── Rivais ─────────────────────────────

/**
 * Rival principal de cada copa (src/game/rivals.ts). Fixo por copa para a tela de copas poder mostrá-lo
 * antes da largada (o elenco sorteado só nasce na primeira corrida); createRace o põe no grid da copa.
 */
export const CUP_RIVALS: Readonly<Record<string, string>> = {
  brasil: 'Zé Turbo', eua: 'Tom Blake', japao: 'Kenji Sato', europa: 'Hugo Klein',
  africa_do_sul: 'Bianca Rey', australia: 'Sasha Kova', escandinavia: 'Iris Lund', mediterraneo: 'Marco Rossi',
};

/**
 * Deslocamento do elenco (os `count` pilotos seguidos a partir de `offset`) que inclui `name`: o mesmo
 * deslocamento se ele já está lá; senão, o que o põe na última vaga da IA — larga logo à frente dos humanos.
 */
export function rosterOffsetWith(offset: number, count: number, name: string | undefined): number {
  const n = AI_DRIVERS.length;
  const index = name === undefined ? -1 : AI_DRIVERS.indexOf(name);
  if (index < 0 || count <= 0) return offset;
  if ((index - offset + n) % n < count) return offset;
  return (((index - (count - 1)) % n) + n) % n;
}
