// As pistas: 8 copas × 4, na ordem das copas (data/cups.ts). Comprimentos em segmentos (200
// unidades cada); ~1.800 segmentos ≈ 60–70 s por volta a 300 km/h. Curva: 2 fácil, 4 média, 6 forte
// (negativa = esquerda). Altura em segmentos. `difficulty` cresce dentro da copa e de uma copa para a
// outra; tests/track.test.ts confere o rótulo contra o traçado. Pista nova: docs/PISTAS.md.
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
  // Estrada de terra reta no Pantanal alagado: retas longas, curvas abertas e as pontes de madeira como lombadas curtas.
  {
    id: 'transpantaneira', name: 'Transpantaneira', country: 'Brasil', scenery: 'savanna', timeOfDay: 'dusk', laps: 3, difficulty: 1,
    ops: [pit(40), st(160), hl(30, 4), st(140), cv(140, 2), st(120), hl(30, 4), st(100), cv(160, 3), st(200), hl(30, 4), st(100), cv(120, -2), st(150), cv(140, 3), st(120), hl(30, 4), st(80), cv(120, 2), st(120)],
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
  // Rodovia de montanha: subidas e descidas grandes com curvas longas de média força.
  {
    id: 'rochosas', name: 'Montanhas Rochosas', country: 'Estados Unidos', scenery: 'alpine', timeOfDay: 'day', laps: 3, difficulty: 2,
    ops: [pit(40), st(100), hl(160, 50), cv(140, 3, 20), st(120), cv(120, -4), hl(140, 40), st(100), cv(160, 3, -25), st(140), ss(200, 3), st(100), cv(120, 4, 15), hl(120, 35), st(80), cv(120, 3), st(120)],
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
  // Estrada na mata subtropical de Okinawa: esses encadeados e curvas médias entre morrotes.
  {
    id: 'yanbaru', name: 'Floresta de Yanbaru', country: 'Japão', scenery: 'tropical', timeOfDay: 'day', laps: 3, difficulty: 3,
    ops: [pit(40), st(80), cv(100, 4), st(60), ss(160, 4), hl(80, 20), cv(90, 5), st(80), cv(110, 4, 15), ss(160, 3), st(120), cv(80, -5), hl(100, 25), st(70), cv(120, 4), ss(160, 4), st(100), cv(90, 5, -15), st(140)],
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
  // Avenidas retas cortadas por esquinas de 90°, a Champs-Élysées subindo até a rotatória do Arco e o cais do Sena.
  {
    id: 'paris', name: 'Boulevards de Paris', country: 'Europa', scenery: 'city_night', timeOfDay: 'dusk', laps: 4, difficulty: 3,
    ops: [pit(40), st(120), cv(60, 5), st(140), cv(60, -5), st(100), cv(60, 6), st(260), hl(80, 12), cv(140, 4), st(120), ss(160, 3), st(100), cv(60, 5), st(100), ss(120, 5), st(100), cv(70, 6), st(120)],
  },
  {
    id: 'passo_alpino', name: 'Passo Alpino', country: 'Europa', scenery: 'alpine', timeOfDay: 'day', laps: 3, difficulty: 5,
    ops: [pit(40), st(40), hl(120, 60), cv(80, 6, 20), cv(80, -6, 20), hl(100, 40), ss(160, 5), st(60), cv(100, -5, -30), hl(120, 50), cv(90, 6), st(80), ss(200, 4), cv(80, -6, -20), hl(100, 30), st(120)],
  },
  {
    id: 'monaco_noite', name: 'Porto de Mônaco', country: 'Europa', scenery: 'coast', timeOfDay: 'night', laps: 4, difficulty: 5,
    ops: [pit(40), st(60), cv(60, -6), st(80), ss(160, 5), cv(70, 6, 15), st(100), cv(60, -6), hl(80, 20), st(120), ss(200, 5), cv(80, 6), st(80), cv(60, -5), st(100), ss(160, 6), st(140), cv(90, 4), st(100)],
  },
  // ───────── África do Sul ─────────
  // Estrada de safári: retas longas na savana, curvas médias e algumas fortes, ondulações suaves.
  {
    id: 'kruger', name: 'Savana do Kruger', country: 'África do Sul', scenery: 'savanna', timeOfDay: 'day', laps: 3, difficulty: 3,
    ops: [pit(40), st(150), cv(100, 4), hl(100, 15), st(140), cv(90, -5), st(130), cv(140, 4), hl(80, 12), st(150), ss(160, 4), cv(100, 5), st(120), cv(100, -5), st(120), cv(80, 5), st(100), cv(100, 4), st(110)],
  },
  // Semideserto: retas enormes com lombadas cegas, e curva forte logo depois da crista.
  {
    id: 'karoo', name: 'Deserto do Karoo', country: 'África do Sul', scenery: 'desert', timeOfDay: 'dusk', laps: 3, difficulty: 4,
    ops: [pit(40), st(120), hl(140, 45), cv(80, 6), st(140), hl(140, 45), cv(120, 4, 20), st(110), cv(80, 6), hl(120, 40), st(120), ss(160, 5), hl(100, 30), cv(90, -5), st(120), cv(80, 6), st(100), cv(110, 4, -20), st(120)],
  },
  // Passo de montanha (curvas à esquerda, subindo): grampos em aclive e esses no alto.
  {
    id: 'drakensberg', name: 'Serra do Drakensberg', country: 'África do Sul', scenery: 'alpine', timeOfDay: 'day', laps: 3, difficulty: 4,
    ops: [pit(40), st(80), hl(140, 55), cv(90, -5, 25), st(60), cv(80, -6, 20), st(70), ss(160, 4), hl(120, 45), cv(100, -4), st(100), cv(80, 6, -25), st(80), hl(100, 35), ss(160, 4), cv(90, -5, -20), st(120), cv(100, -4), st(140)],
  },
  // Estrada do penhasco sobre o Atlântico, à noite: esses fortes e curvas fechadas à beira-mar.
  {
    id: 'boa_esperanca', name: 'Cabo da Boa Esperança', country: 'África do Sul', scenery: 'coast', timeOfDay: 'night', laps: 4, difficulty: 5,
    ops: [pit(40), st(100), cv(70, 6), st(80), ss(160, 5), hl(90, 25), cv(80, 6), st(90), ss(140, 6), st(70), cv(90, 5, 20), cv(80, 6, -20), st(120), ss(160, 5), cv(70, 6), st(90), cv(80, 5), st(150)],
  },
  // ───────── Austrália ─────────
  // Rodovia do deserto: retas sem fim, valas de enchente (baixadas) e curva forte no fim da reta.
  {
    id: 'outback', name: 'Poeira do Outback', country: 'Austrália', scenery: 'desert', timeOfDay: 'day', laps: 3, difficulty: 3,
    ops: [pit(40), st(200), hl(60, -10), st(140), cv(80, 6), st(170), hl(60, -12), st(140), cv(140, 3), st(140), cv(90, -5), hl(60, -10), st(140), cv(100, 5), ss(160, 4), st(120), cv(120, 4), st(130)],
  },
  // Estrada costeira de falésias ao entardecer: curvas e esses médios a fortes com morros.
  {
    id: 'great_ocean', name: 'Great Ocean Road', country: 'Austrália', scenery: 'coast', timeOfDay: 'dusk', laps: 3, difficulty: 4,
    ops: [pit(40), st(100), cv(110, 4, 15), st(70), ss(160, 5), hl(100, 25), cv(90, 5), st(80), cv(120, 4, -15), ss(160, 4), st(100), cv(80, 5), hl(90, 20), cv(100, -4), st(90), ss(160, 5), st(80), cv(100, 5, 10), st(140)],
  },
  // Estrada estreita na floresta tropical: esses e curvas fortes quase sem reta.
  {
    id: 'daintree', name: 'Selva de Daintree', country: 'Austrália', scenery: 'tropical', timeOfDay: 'day', laps: 3, difficulty: 5,
    ops: [pit(40), st(60), ss(140, 6), st(50), cv(80, 6, 15), cv(70, 5), st(60), hl(80, 25), ss(160, 5), cv(90, 6), st(70), cv(80, 6, -15), ss(140, 6), st(60), cv(90, 5), hl(80, 20), cv(70, -6), st(80), ss(140, 5), st(120)],
  },
  // Circuito de rua na baía, à noite: esquinas fortes, esses e a ponte (lombada longa) sobre a água.
  {
    id: 'sydney', name: 'Ponte de Sydney', country: 'Austrália', scenery: 'city_night', timeOfDay: 'night', laps: 4, difficulty: 5,
    ops: [pit(40), st(80), cv(60, 6), st(100), ss(160, 6), st(80), cv(60, 6), st(60), hl(160, 22), st(60), cv(70, 6), st(90), ss(140, 6), st(80), cv(60, 5), st(90), cv(70, 6), ss(160, 5), cv(60, 6), st(120)],
  },
  // ───────── Escandinávia ─────────
  // Rodovia que salta de ilhota em ilhota: pontes como lombadas íngremes (a maior é a Storseisundet) e curvas entre elas.
  {
    id: 'atlantico', name: 'Estrada do Atlântico', country: 'Escandinávia', scenery: 'coast', timeOfDay: 'day', laps: 3, difficulty: 4,
    ops: [pit(40), st(100), hl(50, 18), cv(90, 4), st(70), hl(60, 22), cv(80, 5), ss(160, 4), hl(50, 16), st(80), cv(100, 5), st(90), hl(70, 28), cv(90, -4), ss(140, 5), st(80), hl(50, 16), cv(100, 5), st(100), cv(80, 4), st(130)],
  },
  // Floresta de pinheiros sob o sol da meia-noite: retas com cristas, curvas rápidas e algumas fortes.
  {
    id: 'laponia', name: 'Meia-Noite na Lapônia', country: 'Escandinávia', scenery: 'alpine', timeOfDay: 'dusk', laps: 3, difficulty: 4,
    ops: [pit(40), st(140), hl(120, 35), cv(120, 4), st(120), cv(80, 6), hl(100, 30), st(120), ss(160, 5), st(100), cv(100, 5, 20), hl(120, 35), cv(80, 6), st(120), cv(60, -4), st(80), ss(140, 5), cv(120, 4, -15), st(110)],
  },
  // A escada dos trolls: grampos alternados subindo a encosta, depois esses e curvas fortes descendo.
  {
    id: 'trollstigen', name: 'Trollstigen', country: 'Escandinávia', scenery: 'alpine', timeOfDay: 'day', laps: 3, difficulty: 5,
    ops: [pit(40), st(60), hl(100, 50), cv(70, 6, 25), st(40), cv(70, -6, 25), st(40), cv(70, 6, 25), st(40), cv(70, -6, 25), hl(100, 40), cv(120, 5), st(60), ss(140, 5), cv(80, 6, -30), st(60), cv(80, 6, -30), hl(90, 30), cv(160, 4), st(80), cv(80, -5, -20), st(140)],
  },
  // Cidade-ilha no Ártico, à noite: a ponte sobre o fiorde, esses e curvas fortes à beira-mar.
  {
    id: 'tromso', name: 'Aurora de Tromsø', country: 'Escandinávia', scenery: 'coast', timeOfDay: 'night', laps: 4, difficulty: 5,
    ops: [pit(40), st(90), cv(70, 6), st(80), hl(140, 26), st(60), ss(160, 5), cv(80, 6), st(70), cv(90, 5, 15), ss(140, 6), st(80), cv(70, 6), hl(80, 18), cv(80, -5), st(90), ss(160, 5), st(70), cv(80, 6), st(140)],
  },
  // ───────── Mediterrâneo ─────────
  // Estrada pendurada no penhasco: esses sem fim com morros, curvas fortes entre as vilas.
  {
    id: 'amalfi', name: 'Costa Amalfitana', country: 'Mediterrâneo', scenery: 'coast', timeOfDay: 'day', laps: 3, difficulty: 4,
    ops: [pit(40), st(90), ss(180, 4), hl(90, 25), cv(90, 5), st(60), ss(160, 5), cv(80, 6), st(70), hl(100, 30), ss(180, 4), cv(100, 5, -20), st(80), ss(160, 5), st(70), cv(90, 5), cv(100, 4, 15), st(140)],
  },
  // Estrada da caldeira ao pôr do sol: grampos subindo do porto e esses fortes no alto do penhasco.
  {
    id: 'santorini', name: 'Caldeira de Santorini', country: 'Mediterrâneo', scenery: 'coast', timeOfDay: 'dusk', laps: 3, difficulty: 5,
    ops: [pit(40), st(80), cv(70, 6, 20), st(40), cv(70, -6, 20), st(40), cv(70, 6, 20), hl(90, 20), ss(180, 5), st(70), cv(90, 5), st(100), ss(160, 6), cv(80, -5, -25), st(60), cv(80, 6, -25), st(80), ss(180, 5), cv(90, 5), st(140)],
  },
  // Vulcão: subida longa com grampos no campo de lava e descida rápida com esses.
  {
    id: 'etna', name: 'Vulcão Etna', country: 'Mediterrâneo', scenery: 'desert', timeOfDay: 'day', laps: 3, difficulty: 5,
    ops: [pit(40), st(100), hl(160, 60), cv(90, 6, 30), st(50), cv(90, 6, 30), hl(120, 50), ss(140, 6), st(60), cv(100, 5, -30), st(80), cv(80, 6, -30), ss(180, 5), st(100), cv(90, -5), hl(100, 30), cv(90, 6), st(140)],
  },
  // Ruas de pedra à noite: esquinas fortes, esses, a volta longa do Coliseu e a reta dos Fóruns.
  {
    id: 'roma', name: 'Noite em Roma', country: 'Mediterrâneo', scenery: 'city_night', timeOfDay: 'night', laps: 4, difficulty: 5,
    ops: [pit(40), st(80), cv(60, 6), st(70), ss(140, 6), st(60), cv(60, 6), st(80), cv(160, 5), st(200), cv(60, 6), ss(160, 6), st(60), cv(60, -6), st(70), ss(160, 5), cv(60, 6), st(90), cv(70, 5), st(130)],
  },
];

export function trackDef(id: string): TrackDef {
  const def = TRACKS.find((t) => t.id === id);
  if (!def) throw new Error(`Pista desconhecida: ${id}`);
  return def;
}
