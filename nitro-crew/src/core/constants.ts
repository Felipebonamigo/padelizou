// Constantes do núcleo. Tudo em unidades de mundo: um segmento de pista tem SEGMENT_LENGTH
// unidades; velocidades são unidades por segundo; o passo da simulação é fixo (TICK_RATE).
export const TICK_RATE = 60;
export const DT = 1 / TICK_RATE;

export const SEGMENT_LENGTH = 200;
/** Metade da largura da estrada. `x` normalizado: -1 e +1 são as bordas do asfalto. */
export const ROAD_HALF_WIDTH = 2000;
/** Velocidade de referência: 6000 u/s = 30 segmentos por segundo = 300 km/h no velocímetro. */
export const REFERENCE_SPEED = 6000;
/** Velocidade → km/h no velocímetro. */
export const SPEED_TO_KMH = 300 / REFERENCE_SPEED;

export const MAX_CARS = 20;
export const MAX_SEATS = 4;

/** Comprimento de um carro em unidades de mundo (para colisão longitudinal). */
export const CAR_LENGTH = 120;
/** Meia largura de um carro em `x` normalizado (colisão lateral). */
export const CAR_HALF_WIDTH = 0.22;

export const CENTRIFUGAL = 0.3;
export const OFFROAD_LIMIT_FACTOR = 0.35; // velocidade máxima fora da pista, fração da máxima
export const OFFROAD_DECEL_FACTOR = 0.6;  // desaceleração fora da pista, fração da máxima por s
export const OFFROAD_X = 1.05;            // |x| a partir do qual o carro está na grama

export const NITRO_PER_RACE = 3;
export const NITRO_DURATION_TICKS = 150;  // 2,5 s
export const NITRO_SPEED_MULT = 1.28;
export const NITRO_ACCEL_MULT = 2.2;

export const GEAR_COUNT = 5;
/** Fração da velocidade máxima que cada marcha alcança (manual). */
export const GEAR_TOP = [0.22, 0.42, 0.62, 0.82, 1.0];
/** Multiplicador de aceleração por marcha (marcha baixa acelera mais). */
export const GEAR_ACCEL = [1.7, 1.45, 1.2, 1.0, 0.85];

export const COUNTDOWN_TICKS = 3 * TICK_RATE + 30; // 3 s de contagem + meio segundo de "JÁ"
export const FUEL_CAPACITY = 1;
export const FUEL_EMPTY_SPEED_FACTOR = 0.2;
export const PIT_X = 1.55;                 // centro do box, em `x` normalizado
export const PIT_SPEED_LIMIT_FACTOR = 0.25;
export const PIT_REFUEL_PER_SECOND = 0.35;

export const DRAFT_DISTANCE = SEGMENT_LENGTH * 6;
export const DRAFT_LATERAL = 0.3;
export const DRAFT_ACCEL_MULT = 1.35;
export const DRAFT_TOP_MULT = 1.03;
export const TEAM_DRAFT_TOP_MULT = 1.06;

export const TOW_MIN_SPEED_FACTOR = 0.2;   // abaixo disso o companheiro parado aceita empurrão
export const TOW_DISTANCE = SEGMENT_LENGTH * 2;
export const TOW_LATERAL = 0.7;
export const TOW_SPEED_FACTOR = 0.8;
export const TOW_COOLDOWN_TICKS = 3 * TICK_RATE;

export const CATCHUP_DISTANCE = SEGMENT_LENGTH * 60;
export const CATCHUP_TOP_MULT = 1.05;

export const COLLISION_COOLDOWN_TICKS = 20;
export const SPRITE_CRASH_SPEED_FACTOR = 0.25;

/** Pontos por posição (1º…10º); a partir do 11º, zero. */
export const POINTS_TABLE = [20, 15, 12, 10, 8, 6, 4, 3, 2, 1];
/** Posição mínima para seguir na copa (modo solo/versus). */
export const QUALIFY_POSITION = 5;
/** Colocação mínima da equipe entre as equipes para seguir na copa (modo cooperativo). */
export const TEAM_QUALIFY_RANK = 3;
/** Depois que o primeiro humano cruza a linha, os outros têm este tempo para terminar. */
export const FINISH_GRACE_TICKS = 45 * TICK_RATE;

// Melhorias da carreira, por nível (0..UPGRADE_MAX_LEVEL). Preços e prêmios moram em career.ts.
export const UPGRADE_MAX_LEVEL = 3;
/** Motor: velocidade máxima +2,5% por nível. */
export const UPGRADE_ENGINE_TOP = 0.025;
/** Turbo: aceleração +8% por nível. */
export const UPGRADE_TURBO_ACCEL = 0.08;
/** Pneus: dirigibilidade +0,04 por nível, até HANDLING_MAX. */
export const UPGRADE_TIRES_HANDLING = 0.04;
export const HANDLING_MAX = 1;
/** Freios: frenagem +12% por nível. */
export const UPGRADE_BRAKES = 0.12;
/** Tanque: consumo −10% por nível. */
export const UPGRADE_TANK_FUEL = 0.1;
/** Nitro: +1 carga por nível. */
export const UPGRADE_NITRO_CHARGES = 1;
