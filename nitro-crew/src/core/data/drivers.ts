// Pilotos e equipes controlados pela IA. Nomes inventados.
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

/** Paleta das cores dos jogadores (opções → acessibilidade). */
export type ColorPalette = 'default' | 'deutan' | 'tritan';
export const COLOR_PALETTES: readonly ColorPalette[] = ['default', 'deutan', 'tritan'];

/**
 * Cores dos assentos humanos (P1..P4) por paleta. As de daltonismo foram escolhidas maximizando a
 * menor distância entre as quatro (ΔE em Lab) na visão simulada — deutera/protanopia e tritanopia,
 * Machado 2009 — sem perder a distância na visão normal nem do cinza dos carros da IA no minimapa;
 * tests/assist.test.ts confere os mínimos. Todas claras, para o vidro escuro do HUD.
 */
const SEAT_PALETTES: Readonly<Record<ColorPalette, readonly string[]>> = {
  default: ['#ffd23f', '#3ddc84', '#4fc3f7', '#ff7ab6'],
  deutan: ['#f7ee2a', '#2ef5c6', '#2f8fff', '#ff5c5c'],
  tritan: ['#f7ee2a', '#5cf52a', '#d45cff', '#ff5c5c'],
};

/** As quatro cores dos assentos na paleta dada. Toda cor de jogador passa por aqui ou por seatColor. */
export function seatColors(palette: ColorPalette = 'default'): readonly string[] {
  return SEAT_PALETTES[palette] ?? SEAT_PALETTES.default;
}

/** Cor do assento `seat` (0..3) na paleta dada; fora da faixa, branco. */
export function seatColor(seat: number, palette: ColorPalette = 'default'): string {
  return seatColors(palette)[seat] ?? '#ffffff';
}

/** Nome do time humano no modo cooperativo. */
export const HUMAN_TEAM_NAME = 'Sua equipe';
export const HUMAN_TEAM_ID = 0;
export const AI_TEAM_ID_BASE = 100;
