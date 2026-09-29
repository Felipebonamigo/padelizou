import { afterEach, describe, expect, it } from 'vitest';
import { formatTicks } from '../src/core/sim/race';
import { setLanguage } from '../src/i18n';
import { finishMessage } from '../src/game/finish-message';
import { human, humanCar, quickRace, run, skipCountdown } from './helpers';

afterEach(() => setLanguage('pt'));

describe('mensagem de chegada', () => {
  it('no contra-relógio mostra o tempo, não "VITÓRIA!" (quem corre sozinho chega sempre em 1º)', () => {
    const { state, track } = quickRace({ humans: [human(0)], timeTrial: true, laps: 1 });
    skipCountdown(state, track);
    run(state, track, 600);
    const car = humanCar(state, 0);
    car.finished = true; car.finishTick = state.tick;
    const text = finishMessage(state, car.id, 1);
    expect(text).toContain(formatTicks(car.finishTick - state.startTick));
    expect(text).not.toContain('VITÓRIA');
  });

  it('na corrida normal segue a posição: VITÓRIA! em 1º, CHEGADA — 3º', () => {
    const { state } = quickRace({ humans: [human(0)] });
    expect(finishMessage(state, 0, 1)).toBe('VITÓRIA!');
    expect(finishMessage(state, 0, 3)).toBe('CHEGADA — 3º');
  });
});
