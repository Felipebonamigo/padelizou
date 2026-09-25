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

/** Cores dos assentos humanos (P1..P4). */
export const SEAT_COLORS: readonly string[] = ['#ffd23f', '#3ddc84', '#4fc3f7', '#ff7ab6'];

/** Nome do time humano no modo cooperativo. */
export const HUMAN_TEAM_NAME = 'Sua equipe';
export const HUMAN_TEAM_ID = 0;
export const AI_TEAM_ID_BASE = 100;
