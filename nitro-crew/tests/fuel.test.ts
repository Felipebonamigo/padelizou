// Combustível nas pistas longas: o aviso ao jogador chega antes do último box que ainda salva a
// corrida. O box fica logo depois da linha, então quem seca na volta N só podia ter parado na
// passagem pela linha que abriu essa volta.
import { describe, expect, it } from 'vitest';
import { FUEL_LOW_LAPS, FUEL_PIT_MARGIN, TICK_RATE } from '../src/core/constants';
import { CARS } from '../src/core/data/cars';
import { aiInput } from '../src/core/sim/ai';
import { fuelLowLevel, fuelTight, fuelToSkipPit, markFuel, measuredBurn, PIT_LOOKAHEAD, pitStillAhead } from '../src/core/sim/fuel';
import { getTrack } from '../src/core/track';
import { TRACKS } from '../src/core/track/tracks';
import type { AiBrain, PlayerInput, RaceState, Track } from '../src/core/types';
import { human, humanCar, quickRace, run } from './helpers';

/** Antecedência mínima do aviso antes da linha (e do box logo depois dela): 10% da volta, ~8 s. */
const LEAD_SHARE = 0.1;

interface Log { lowAt: { progress: number; lap: number }[]; emptyAt: number | null }

/**
 * Corrida inteira com um humano de cada carro que IGNORA o box (o volante vem do piloto automático,
 * que enxerga tanque cheio): mede onde o aviso veio e onde o tanque secou. Dois jeitos de guiar:
 * `competente` freia para as curvas como a IA; `pe_no_fundo` nunca tira o pé nem freia e aperta o
 * nitro sempre que pode — o jogador de arcade típico, que gasta ~0,95–1,0 do consumo de aceleração
 * total por volta (a IA fica em 0,60–0,72).
 */
type Style = 'competente' | 'pe_no_fundo';
function raceIgnoringPit(track: Track, style: Style): { state: RaceState; logs: Log[] } {
  const humans = CARS.map((c, i) => human(i, 0, c.id));
  const { state } = quickRace({ track, humans, totalCars: humans.length, laps: track.def.laps, difficulty: 'profissional', seed: 3 });
  const brains: AiBrain[] = humans.map((_, i) => ({ skill: 0.97, laneX: 0.2 * (i - 1.5), laneUntil: 0, lookahead: 28, aggression: 0.6 }));
  const autopilot = (s: RaceState, seat: number): PlayerInput => {
    const car = humanCar(s, seat);
    const fuel = car.fuel;
    car.fuel = 1; car.ai = brains[seat];
    const input = aiInput(s, track, car);
    car.fuel = fuel; car.ai = null;
    return style === 'competente' ? input : { ...input, throttle: true, brake: false, nitro: car.nitroTicks === 0 };
  };
  const logs: Log[] = humans.map(() => ({ lowAt: [], emptyAt: null }));
  for (let i = 0; i < TICK_RATE * 900 && state.phase !== 'finished'; i++) {
    run(state, track, 1, autopilot);
    for (const e of state.events) {
      if (e.type !== 'fuel_low' && e.type !== 'fuel_empty') continue;
      const car = state.cars[e.carId];
      if (car.seat < 0 || car.finished) continue;
      if (e.type === 'fuel_low') logs[car.seat].lowAt.push({ progress: car.progress, lap: car.lap });
      else logs[car.seat].emptyAt = car.progress;
    }
  }
  return { state, logs };
}

describe('combustível: aviso ao jogador', () => {
  const cases = TRACKS.flatMap((d) => (['competente', 'pe_no_fundo'] as const).map((style) => [d.id, style] as const));
  it.each(cases)('%s (%s): quem ignora o box recebe o aviso antes da última passagem pelo box que ainda salva a corrida', (id, style) => {
    const track = getTrack(id);
    const { state, logs } = raceIgnoringPit(track, style);
    const laps = state.config.laps;
    const problems: string[] = [];
    logs.forEach((log, seat) => {
      const who = humanCar(state, seat).carId;
      if (log.emptyAt !== null) {
        const lastLine = Math.floor(log.emptyAt / track.length) * track.length;
        const lead = log.lowAt.length ? (lastLine - log.lowAt[0].progress) / track.length : null;
        if (lead === null) problems.push(`${who}: secou sem aviso`);
        else if (lead < LEAD_SHARE) problems.push(`${who}: aviso só ${lead.toFixed(2)} volta antes do último box (mínimo ${LEAD_SHARE})`);
      }
      // "ENTRE NO BOX" na última volta não tem box a que obedecer antes da chegada.
      for (const w of log.lowAt) if (w.lap >= laps) problems.push(`${who}: aviso na última volta`);
    });
    expect(problems, id).toEqual([]);
  }, 30_000);

  // Sem isto o teste acima podia passar sem ninguém secar. A Kruger é a da revisão de 25/09: o Trovão
  // chega à última volta com ~0,31 e a volta pede ~0,33.
  it('o cenário existe: na Kruger, o Trovão que ignora o box seca antes da chegada (e foi avisado)', () => {
    const { state, logs } = raceIgnoringPit(getTrack('kruger'), 'competente');
    const trovao = humanCar(state, CARS.findIndex((c) => c.id === 'trovao'));
    expect(logs[trovao.seat].emptyAt).not.toBeNull();
    expect(logs[trovao.seat].lowAt.length).toBeGreaterThan(0);
  }, 30_000);
});

describe('combustível: regras puras (sim/fuel.ts)', () => {
  const trovao = CARS.find((c) => c.id === 'trovao')!;
  const brain = (): AiBrain => ({ skill: 1, laneX: 0, laneUntil: 0, lookahead: 20, aggression: 0 });
  const car = (fuel: number, progress: number, inPit = false) => ({ fuel, progress, inPit }) as RaceState['cars'][number];

  it('o aviso é proporcional à volta: volta duas vezes maior, aviso duas vezes mais cedo', () => {
    expect(fuelLowLevel(trovao.fuelPerUnit, 400_000)).toBeCloseTo(FUEL_LOW_LAPS * trovao.fuelPerUnit * 400_000, 12);
    expect(fuelLowLevel(trovao.fuelPerUnit, 800_000)).toBeCloseTo(2 * fuelLowLevel(trovao.fuelPerUnit, 400_000), 12);
  });

  it('nenhum carro recebe o aviso nem para no box com o tanque cheio, em nenhuma pista (pior caso: aceleração total)', () => {
    for (const d of TRACKS) {
      const t = getTrack(d.id);
      for (const c of CARS) {
        expect(fuelLowLevel(c.fuelPerUnit, t.length), `${d.id} ${c.id}`).toBeLessThan(1);
        expect(fuelToSkipPit(c.fuelPerUnit, t.length, d.laps * t.length), `${d.id} ${c.id}`).toBeLessThan(1);
      }
    }
  });

  it('passar reto pede combustível até o próximo box ou até a chegada, o que vier antes, com folga', () => {
    const lap = 400_000; const burn = 1e-6;
    expect(fuelToSkipPit(burn, lap, 3 * lap)).toBeCloseTo(burn * (lap + PIT_LOOKAHEAD) * FUEL_PIT_MARGIN, 12);
    expect(fuelToSkipPit(burn, lap, lap / 2)).toBeCloseTo(burn * (lap / 2) * FUEL_PIT_MARGIN, 12);
    expect(fuelToSkipPit(burn, lap, -10)).toBeLessThanOrEqual(0); // já cruzou a chegada
  });

  it('consumo medido: pior caso até meia volta; depois, o maior entre a última volta inteira e a volta em curso', () => {
    const lap = 400_000; const b = brain(); const worst = trovao.fuelPerUnit;
    const step = (fuel: number, progress: number, inPit = false) => { const c = car(fuel, progress, inPit); markFuel(b, c, lap); return c; };
    expect(measuredBurn(b, step(1, -600), worst, lap)).toBe(worst);
    expect(measuredBurn(b, step(0.9, 100_000), worst, lap)).toBe(worst);
    // Meia volta medida: vale o gasto real da volta em curso.
    expect(measuredBurn(b, step(0.8, 239_400), worst, lap)).toBeCloseTo(0.2 / 240_000, 12);
    // Uma volta inteira: fecha a medida (0,25 na volta) e começa outra no mesmo ponto.
    expect(measuredBurn(b, step(0.75, 399_400), worst, lap)).toBeCloseTo(0.25 / 400_000, 12);
    expect([b.fuelMark, b.progressMark]).toEqual([0.75, 399_400]);
    // A volta seguinte gasta mais (0,37 no ritmo de agora): passada meia volta, é ela que vale.
    expect(measuredBurn(b, step(0.6, 559_400), worst, lap)).toBeCloseTo(0.25 / 400_000, 12);
    expect(measuredBurn(b, step(0.5285, 609_400), worst, lap)).toBeCloseTo(0.2215 / 210_000, 12);
    // Volta que gasta menos não apaga a anterior: vale a maior das duas.
    const b2 = brain(); const m = (f: number, p: number) => markFuel(b2, car(f, p), lap);
    m(1, 0); m(0.6, 400_000); m(0.45, 700_000);
    expect(measuredBurn(b2, car(0.45, 700_000), worst, lap)).toBeCloseTo(0.4 / 400_000, 12);
  });

  it('no box a marca acompanha o carro e recomeça na saída; a última volta medida continua valendo', () => {
    const lap = 400_000; const b = brain(); const m = (f: number, p: number, inPit = false) => markFuel(b, car(f, p, inPit), lap);
    m(1, 0); m(0.64, 400_000);
    m(0.3, 790_000); m(0.5, 800_000, true); m(1, 805_000, true); m(0.999, 808_000);
    expect([b.fuelMark, b.progressMark]).toEqual([1, 805_000]);
    expect(measuredBurn(b, car(0.95, 900_000), trovao.fuelPerUnit, lap)).toBeCloseTo(0.36 / 400_000, 12);
    // Combustível acima da marca sem passar pelo box (ajustado à mão, como no teste de box da IA)
    // também recomeça a medida.
    const c = brain();
    markFuel(c, car(0.5, 1000), lap);
    markFuel(c, car(0.7, 2000), lap);
    expect([c.fuelMark, c.progressMark]).toEqual([0.7, 2000]);
  });

  it('tanque justo: não garante, em aceleração total, chegar à próxima linha (o box vem logo depois) ou à chegada', () => {
    const track = getTrack('kruger');
    const { state } = quickRace({ track, laps: 3 });
    const at = (lap: number, z: number, fuel: number) => ({ ...humanCar(state), lap, z, progress: (lap - 1) * track.length + z, fuel });
    const full = trovao.fuelPerUnit * track.length; // ~0,55: uma volta inteira em aceleração total
    expect(fuelTight(state, track, at(2, 1000, full), trovao.fuelPerUnit)).toBe(false);
    expect(fuelTight(state, track, at(2, 1000, full * 0.9), trovao.fuelPerUnit)).toBe(true);
    // Perto da linha falta pouco até o box: não é justo nem com o tanque quase vazio.
    expect(fuelTight(state, track, at(2, track.length - 3000, 0.05), trovao.fuelPerUnit)).toBe(false);
    // Na última volta conta a chegada; depois dela, nada é justo.
    expect(fuelTight(state, track, at(3, track.length / 2, full * 0.45), trovao.fuelPerUnit)).toBe(true);
    expect(fuelTight(state, track, at(3, track.length / 2, full * 0.55), trovao.fuelPerUnit)).toBe(false);
    expect(fuelTight(state, track, at(4, 500, 0), trovao.fuelPerUnit)).toBe(false);
  });

  it('"entre no box" só enquanto há box antes da chegada: não na última volta', () => {
    const { state } = quickRace({ laps: 3 });
    const c = humanCar(state);
    expect([0, 1, 2, 3].map((lap) => pitStillAhead(state, { ...c, lap }))).toEqual([true, true, true, false]);
  });
});
