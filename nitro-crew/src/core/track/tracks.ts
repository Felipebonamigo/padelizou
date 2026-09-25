// As pistas. Comprimentos em segmentos (200 unidades cada); ~1.800 segmentos ≈ 60–70 s por
// volta a 300 km/h. Curva: 2 fácil, 4 média, 6 forte (negativa = esquerda). Altura em segmentos.
import type { TrackDef, TrackOp } from '../types';

const st = (length: number): TrackOp => ({ op: 'straight', length });
const cv = (length: number, curve: number, hill?: number): TrackOp => ({ op: 'curve', length, curve, hill });
const hl = (length: number, height: number): TrackOp => ({ op: 'hill', length, height });
const ss = (length: number, curve: number): TrackOp => ({ op: 's', length, curve });
const pit = (length: number): TrackOp => ({ op: 'pit', length });

export const TRACKS: TrackDef[] = [
  // ───────── Brasil ─────────
  {
    id: 'copacabana', name: 'Orla de Copacabana', country: 'Brasil', scenery: 'coast', timeOfDay: 'day', laps: 3, difficulty: 1,
    ops: [pit(40), st(80), cv(80, 2), st(100), cv(100, -2), hl(80, 20), st(150), cv(120, 3), st(200), ss(160, 2), cv(90, -3), st(140), cv(100, 2, 10), st(120), cv(80, -2), st(160)],
  },
  {
    id: 'serra_do_mar', name: 'Serra do Mar', country: 'Brasil', scenery: 'tropical', timeOfDay: 'day', laps: 3, difficulty: 2,
    ops: [pit(40), st(60), hl(120, 40), cv(100, 4, 15), st(80), cv(120, -4), hl(100, 30), ss(200, 3), st(120), cv(80, 5), st(100), cv(140, -3, -20), hl(90, 25), st(160), cv(100, 3), st(120)],
  },
  {
    id: 'sampa_noite', name: 'Noite em Sampa', country: 'Brasil', scenery: 'city_night', timeOfDay: 'night', laps: 4, difficulty: 3,
    ops: [pit(40), st(60), ss(160, 4), st(80), cv(60, -5), st(120), cv(100, 4), ss(200, 3), st(150), cv(80, -6), st(100), cv(120, 5), st(90), ss(180, 4), st(100), cv(70, -4), st(140)],
  },
  // ───────── Estados Unidos ─────────
  {
    id: 'rota_66', name: 'Rota 66', country: 'Estados Unidos', scenery: 'desert', timeOfDay: 'day', laps: 3, difficulty: 1,
    ops: [pit(40), st(210), cv(120, 2), st(300), cv(100, -2), st(200), cv(140, 3), hl(100, 15), st(250), cv(120, -3), st(220)],
  },
  {
    id: 'canion', name: 'Cânion de Nevada', country: 'Estados Unidos', scenery: 'desert', timeOfDay: 'dusk', laps: 3, difficulty: 3,
    ops: [pit(40), st(60), hl(120, 50), cv(100, 5, 20), st(80), cv(80, -5), hl(140, 40), ss(180, 4), st(100), cv(120, 4, -25), st(90), cv(100, -6), hl(100, 30), st(160), cv(90, 3), st(100)],
  },
  {
    id: 'las_vegas', name: 'Strip de Las Vegas', country: 'Estados Unidos', scenery: 'city_night', timeOfDay: 'night', laps: 4, difficulty: 3,
    ops: [pit(40), st(160), cv(80, -4), st(150), ss(160, 5), st(120), cv(100, 4), st(200), cv(60, -6), st(100), ss(180, 3), st(160), cv(100, 5), st(150)],
  },
  // ───────── Japão ─────────
  {
    id: 'baia_toquio', name: 'Baía de Tóquio', country: 'Japão', scenery: 'coast', timeOfDay: 'dusk', laps: 3, difficulty: 2,
    ops: [pit(40), st(110), cv(120, 3), st(100), cv(80, -4), hl(100, 20), st(160), ss(200, 3), st(120), cv(100, 4), st(200), cv(120, -3), st(120), cv(90, 2), st(140)],
  },
  {
    id: 'monte_fuji', name: 'Monte Fuji', country: 'Japão', scenery: 'alpine', timeOfDay: 'day', laps: 3, difficulty: 4,
    ops: [pit(40), st(60), hl(150, 60), cv(120, -4, 30), st(60), cv(100, 5), hl(120, 45), ss(180, 4), cv(80, -6), st(100), hl(100, 30), cv(120, 4, -30), st(120), cv(100, -5), hl(90, 25), st(130)],
  },
  {
    id: 'osaka_neon', name: 'Neon de Osaka', country: 'Japão', scenery: 'city_night', timeOfDay: 'night', laps: 4, difficulty: 4,
    ops: [pit(40), st(80), ss(200, 5), st(80), cv(60, -6), st(100), cv(80, 6), ss(160, 4), st(140), cv(100, -5), st(80), ss(180, 5), st(100), cv(70, 6), st(120), cv(80, -4), st(130)],
  },
  // ───────── Europa ─────────
  {
    id: 'autobahn', name: 'Autobahn', country: 'Europa', scenery: 'savanna', timeOfDay: 'day', laps: 3, difficulty: 2,
    ops: [pit(40), st(310), cv(150, 2), st(300), cv(120, -3), st(250), ss(200, 2), st(300), cv(140, 3), st(200)],
  },
  {
    id: 'passo_alpino', name: 'Passo Alpino', country: 'Europa', scenery: 'alpine', timeOfDay: 'day', laps: 3, difficulty: 5,
    ops: [pit(40), st(40), hl(120, 60), cv(80, 6, 20), cv(80, -6, 20), hl(100, 40), ss(160, 5), st(60), cv(100, -5, -30), hl(120, 50), cv(90, 6), st(80), ss(200, 4), cv(80, -6, -20), hl(100, 30), st(120)],
  },
  {
    id: 'monaco_noite', name: 'Porto de Mônaco', country: 'Europa', scenery: 'coast', timeOfDay: 'night', laps: 4, difficulty: 5,
    ops: [pit(40), st(60), cv(60, -6), st(80), ss(160, 5), cv(70, 6, 15), st(100), cv(60, -6), hl(80, 20), st(120), ss(200, 5), cv(80, 6), st(80), cv(60, -5), st(100), ss(160, 6), st(140), cv(90, 4), st(100)],
  },
];

export function trackDef(id: string): TrackDef {
  const def = TRACKS.find((t) => t.id === id);
  if (!def) throw new Error(`Pista desconhecida: ${id}`);
  return def;
}
