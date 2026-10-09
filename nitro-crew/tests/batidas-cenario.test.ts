// Batidas da IA no cenário (docs/PISTAS.md, "Desenho × dificuldade"; onda K, frente K5). Os desenhos de polígono
// (onda J) encheram quatro pistas de quinas curtas de curva forte; cada uma ganhava as 3 placas de curva do lado de
// fora (builder.ts, "Placas de curva"), e a IA, jogada para fora na quina, batia nelas. A conta é a de
// `npm run balance -- corrida profissional <semente> <pista>` (coluna "cenário"), na média das sementes 11–13.
import { describe, expect, it } from 'vitest';
import { TICK_RATE } from '../src/core/constants';
import { createRace, stepRace } from '../src/core/sim/race';
import { getTrack } from '../src/core/track';
import { PROXY_SKILL } from '../scripts/career-balance-lib';

/** Média, nas sementes 11–13, das batidas no cenário (evento `crash`) numa corrida inteira de 20 carros, profissional. */
function sceneryCrashes(trackId: string): number {
  const track = getTrack(trackId);
  let crashes = 0;
  for (const seed of [11, 12, 13]) {
    const state = createRace({
      trackId, laps: track.def.laps, humans: [{ seat: 0, name: 'P1', carId: 'falcao', teamId: 0, color: '#fff' }], totalCars: 20,
      difficulty: 'profissional', manualGear: false, assists: { sharedNitro: false, tow: false, teamDraft: false, catchup: false }, seed,
    }, track);
    const me = state.cars.find((c) => c.seat === 0);
    if (me) me.ai = { skill: PROXY_SKILL, laneX: 0.3, laneUntil: 0, lookahead: 32, aggression: 0.5 };
    for (let i = 0; i < TICK_RATE * 60 * 20 && state.phase !== 'finished'; i++) {
      stepRace(state, track, []);
      for (const e of state.events) if (e.type === 'crash') crashes++;
    }
  }
  return crashes / 3;
}

// Antes dos desenhos (commit 34206e1) → com os desenhos de polígono: Maceió 5,7 → 32,0 · Palmas 29,3 → 55,7 ·
// Rio Branco 1,0 → 16,0 · Monte Roraima 8,0 → 17,3. O teto é o nível de antes com folga para o sorteio.
const LIMIT: Record<string, number> = { maceio: 7, palmas: 33, rio_branco: 4, monte_roraima: 9 };

describe('batidas no cenário voltam ao nível de antes dos desenhos', () => {
  for (const [id, limit] of Object.entries(LIMIT)) {
    it(`${id}: no máximo ${limit} batidas por corrida (média das sementes 11–13)`, () => {
      expect(sceneryCrashes(id)).toBeLessThanOrEqual(limit);
    }, 120_000);
  }
});
