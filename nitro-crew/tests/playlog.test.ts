// Diário de jogo (src/game/playlog.ts): funil, corridas, sessões e questionário, tudo no computador do jogador.
// Relógio, leitura e gravação são injetados (fakeDeps); as corridas "de verdade" andam pelo mesmo passo da sessão
// (stepObserved) e pelo mesmo termômetro (race-feel.ts). Nada aqui toca em DOM nem em armazenamento.
import { createRequire } from 'node:module';
import { describe, expect, it } from 'vitest';
import { createChampionship } from '../src/core/championship';
import { carIndexOfSeat } from '../src/core/modes';
import { createRace } from '../src/core/sim/race';
import { NEUTRAL_INPUT, type CoreMode, type HumanEntry, type PlayerInput } from '../src/core/types';
import { newTelemetry } from '../src/game/achievements';
import { isCloudKey } from '../src/game/cloudsave';
import {
  createPlaylog, formatPlaylogReport, MAX_RACES, MAX_SESSIONS, MAX_SURVEYS, PLAYLOG_KEY, PLAYLOG_MAX_CHARS, playlogRace,
  sanitizePlaylog, summarizePlaylog, type PlaylogSeat,
} from '../src/game/playlog';
import { stepObserved } from '../src/game/raceEnd';
import { createFeelTracker, observeFeel, type FeelTracker, type RaceFeel } from '../src/game/race-feel';
import { fakeDeps, fakeRace } from './playlog-fixtures';
import { human, NO_ASSISTS, skipCountdown, syntheticTrack } from './helpers';

const require = createRequire(import.meta.url);
const desktopStorage = require('../desktop/storage.cjs') as { isSaveKey(key: string): boolean; MAX_SAVE_BYTES: number };

const EMPTY = { format: 1, nextSession: 1, sessions: [], races: [], surveys: [] };
const DAY = 86_400_000;
const HOUR = 3_600_000;

/** Corrida curta de verdade: reta de 300 segmentos, 1 volta, 4 carros, acelerador cheio, passo igual ao da sessão. */
function shortRace(humans: HumanEntry[] = [human(0)], mode?: CoreMode) {
  const track = syntheticTrack([{ op: 'straight', length: 300 }]);
  const state = createRace({ trackId: track.def.id, laps: 1, humans, totalCars: 4, difficulty: 'profissional', manualGear: false, assists: NO_ASSISTS, seed: 3, mode }, track);
  const telemetry = newTelemetry();
  const feel: Record<number, FeelTracker> = {};
  for (const h of humans) { const c = carIndexOfSeat(state, h.seat); if (c >= 0) feel[c] ??= createFeelTracker(c); }
  const inputs: PlayerInput[] = [];
  for (const h of humans) inputs[h.seat] = { ...NEUTRAL_INPUT, throttle: true };
  const step = (n = 60 * 120) => {
    for (let i = 0; i < n && state.phase !== 'finished'; i++) {
      stepObserved({ state, track, telemetry }, inputs);
      for (const f of Object.values(feel)) observeFeel(f, state);
    }
  };
  return { state, track, telemetry, feel, step };
}

/** Diário aberto, com `n` corridas completas na sessão. */
function logWithRaces(n: number, start?: string) {
  const f = fakeDeps(start);
  const log = createPlaylog(f.deps);
  log.sessionStart();
  for (let i = 0; i < n; i++) log.raceEnded(fakeRace());
  return { ...f, log };
}

describe('diário de jogo', () => {
  it('funil: do título à 1ª largada guarda tempo e entradas, uma vez por sessão', () => {
    const { deps, clock } = fakeDeps();
    const log = createPlaylog(deps);
    log.sessionStart();
    for (let i = 0; i < 4; i++) log.input();
    clock.now += 42_000;
    log.raceStarted({ mode: 'quick', trackId: 'copacabana', humans: 1 });
    expect(log.data().sessions[0].funnel).toEqual({ ms: 42_000, inputs: 4, mode: 'quick' });
    clock.now += 5_000;
    log.input();
    log.raceStarted({ mode: 'cup', trackId: 'copacabana', humans: 2 });
    expect(log.data().sessions[0].funnel).toEqual({ ms: 42_000, inputs: 4, mode: 'quick' });
    log.sessionStart();
    log.raceStarted({ mode: 'quick', trackId: 'tutorial', humans: 1 });
    expect(log.data().sessions[1].funnel?.mode).toBe('tutorial');
  });

  it('só abrir a sessão, largar, terminar e responder gravam; contar entradas e chamar antes da sessão não', () => {
    const { deps, writes } = fakeDeps();
    const log = createPlaylog(deps);
    log.input();
    log.raceEnded(fakeRace());
    log.raceStarted({ mode: 'quick', trackId: 'copacabana', humans: 1 });
    expect(writes).toHaveLength(0);
    expect(log.data().races).toHaveLength(0);
    log.sessionStart();
    expect(writes).toHaveLength(1);
    for (let i = 0; i < 50; i++) log.input();
    expect(writes).toHaveLength(1);
  });

  it('fim de corrida: modo, pista, posição, duração, eliminação, assistência, box, tanque vazio, batidas e termômetro por assento', () => {
    const r = shortRace();
    r.step();
    expect(r.state.phase).toBe('finished');
    const champ = { ...createChampionship('br_rj', [human(0)]), eliminated: true };
    const race = playlogRace({ state: r.state, track: r.track, mode: 'cup', telemetry: r.telemetry, localSeats: [0], driver: null, feel: r.feel }, { abandoned: false, champ });
    const car = carIndexOfSeat(r.state, 0);
    const row = r.state.results?.find((x) => x.carId === car);
    const tel = r.telemetry.seats.get(0);
    expect(row).toBeTruthy();
    expect(tel).toBeTruthy();
    expect(race.seats[0].car).toBe(car);
    expect(race.seats[0].position).toBe(row?.position);
    expect(race.seats[0].finished).toBe(true);
    expect(race.seats[0].contacts).toBe(tel?.collisions);
    expect(race.seats[0].crashes).toBe(tel?.crashes);
    expect(race.seats[0].pits).toBe(tel?.pitStops);
    expect(race.seats[0].assist).toBe('none');
    expect(race.seats[0].feel?.seconds).toBeGreaterThan(0);
    expect(race.seconds).toBe(race.seats[0].feel?.seconds);
    expect(race).toMatchObject({ eliminated: true, cupCompleted: false, cup: 'br_rj', online: false, track: 'sintetica', laps: 1, field: 4, mode: 'cup', abandoned: false });
  });

  it('desistência: sair no meio marca abandonada, sem posição nem eliminação', () => {
    const r = shortRace();
    skipCountdown(r.state, r.track);
    r.step(100);
    const champ = { ...createChampionship('br_rj', [human(0)]), eliminated: true };
    const race = playlogRace({ state: r.state, track: r.track, mode: 'cup', telemetry: r.telemetry, localSeats: [0], driver: null, feel: r.feel }, { abandoned: true, champ });
    expect(race.abandoned).toBe(true);
    expect(race.seats[0].position).toBe(0);
    expect(race.seats[0].finished).toBe(false);
    expect(race.eliminated).toBe(false);
  });

  it('anel com teto: fica com as últimas MAX_RACES corridas e MAX_SESSIONS sessões', () => {
    const { deps } = fakeDeps();
    const log = createPlaylog(deps);
    log.sessionStart();
    for (let i = 0; i < MAX_RACES + 10; i++) log.raceEnded(fakeRace({ seconds: i }));
    expect(log.data().races).toHaveLength(MAX_RACES);
    expect(log.data().races[0].seconds).toBe(10);
    const other = createPlaylog(fakeDeps().deps);
    for (let i = 0; i < MAX_SESSIONS + 5; i++) other.sessionStart();
    expect(other.data().sessions).toHaveLength(MAX_SESSIONS);
  });

  it('pior caso cabe em PLAYLOG_MAX_CHARS sem cortar corrida, e abaixo do arquivo do Electron', () => {
    const worstFeel: RaceFeel = { seconds: 1234.56, overtakes: 123, overtakesPerMin: 12.34, battleSeconds: 1234.56, aloneSeconds: 1234.56, flatOutPct: 99.99, nitroPct: 99.99, scrapes: 123, rearHits: 123 };
    const worstSeat = (seat: number): PlaylogSeat => ({ seat, car: 19, position: 20, finished: false, assist: 'brake', crashes: 99, contacts: 999, pits: 9, fuelEmpty: false, feel: worstFeel });
    const worst = fakeRace({ mode: 'tournament', track: 'fortaleza_beira_mar', cup: 'africa_do_sul', laps: 5, field: 20, seconds: 1234.56, seats: [0, 1, 2, 3].map(worstSeat) });
    const { deps, clock } = fakeDeps();
    const log = createPlaylog({ ...deps, write: () => true });
    for (let i = 0; i < MAX_SESSIONS; i++) {
      clock.now += DAY;
      log.sessionStart();
      log.raceStarted({ mode: 'tournament', trackId: 'fortaleza_beira_mar', humans: 4 });
      if (i < MAX_SURVEYS) {
        log.raceEnded(worst);
        log.askSurveyBefore({ type: 'quitApp' }, false);
        log.answerSurvey({ again: 5, difficulty: 'right', hurdle: 'performance' });
      }
    }
    for (let i = 0; i < MAX_RACES; i++) log.raceEnded(worst);
    const d = log.data();
    expect(d.races).toHaveLength(MAX_RACES);
    expect(d.sessions).toHaveLength(MAX_SESSIONS);
    expect(d.surveys).toHaveLength(MAX_SURVEYS);
    expect(JSON.stringify(d).length).toBeLessThanOrEqual(PLAYLOG_MAX_CHARS);
    expect(PLAYLOG_MAX_CHARS).toBeLessThan(desktopStorage.MAX_SAVE_BYTES);
  }, 30_000);

  it('grava na chave nitro-crew.playlog, que o espelho do Electron leva para saves/', () => {
    const { deps, writes } = fakeDeps();
    const log = createPlaylog(deps);
    log.sessionStart();
    log.raceEnded(fakeRace());
    const last = writes[writes.length - 1];
    expect(last[0]).toBe(PLAYLOG_KEY);
    expect(PLAYLOG_KEY).toBe('nitro-crew.playlog');
    expect(last[1]).toEqual(log.data());
    expect(isCloudKey(PLAYLOG_KEY)).toBe(true);
    expect(desktopStorage.isSaveKey(PLAYLOG_KEY)).toBe(true);
  });

  it('lixo no armazenamento vira diário vazio; o que é válido volta igual', () => {
    expect(sanitizePlaylog(null)).toEqual(EMPTY);
    expect(sanitizePlaylog('x')).toEqual(EMPTY);
    expect(sanitizePlaylog({ format: 9 })).toEqual(EMPTY);
    const feel: RaceFeel = { seconds: 229.15, overtakes: 28, overtakesPerMin: 7.33, battleSeconds: 119.78, aloneSeconds: 38.98, flatOutPct: 85.1, nitroPct: 3.3, scrapes: 20, rearHits: 15 };
    const { log } = logWithRaces(0);
    log.raceEnded(fakeRace({ seats: [{ ...fakeRace().seats[0], feel }] }));
    log.raceEnded(fakeRace());
    log.askSurveyBefore({ type: 'quitApp' }, false);
    log.answerSurvey({ again: 4, difficulty: 'right', hurdle: 'none' });
    const d = log.data();
    const back = sanitizePlaylog(JSON.parse(JSON.stringify(d)));
    expect(back).toEqual(d);
    const bad = JSON.parse(JSON.stringify(d));
    bad.races[0].laps = 'x';
    expect(sanitizePlaylog(bad).races).toHaveLength(d.races.length - 1);
  });

  it('questionário depois da 3ª corrida completa, uma vez por sessão e por dia', () => {
    const { log, deps, clock } = logWithRaces(2);
    expect(log.askSurveyBefore({ type: 'nextRace' }, false)).toBe(false);
    log.raceEnded(fakeRace({ abandoned: true }));
    expect(log.askSurveyBefore({ type: 'nextRace' }, false)).toBe(false);
    log.raceEnded(fakeRace());
    expect(log.askSurveyBefore({ type: 'nextRace' }, true)).toBe(false);
    expect(log.askSurveyBefore({ type: 'resume' }, false)).toBe(false);
    expect(log.askSurveyBefore({ type: 'nextRace' }, false)).toBe(true);
    expect(log.pendingSurvey()).toEqual({ reason: 'races', next: { type: 'nextRace' } });
    expect(log.askSurveyBefore({ type: 'nextRace' }, false)).toBe(false);
    log.answerSurvey({ again: 4, difficulty: 'right', hurdle: 'none' });
    expect(log.data().surveys[0]).toMatchObject({ reason: 'races', skipped: false, again: 4, difficulty: 'right', hurdle: 'none', session: 1 });
    expect(log.pendingSurvey()).toBeNull();
    log.raceEnded(fakeRace());
    expect(log.askSurveyBefore({ type: 'nextRace' }, false)).toBe(false);
    // Outra sessão no mesmo dia (UTC): já perguntou hoje.
    clock.now += HOUR;
    log.sessionStart();
    for (let i = 0; i < 3; i++) log.raceEnded(fakeRace());
    expect(log.askSurveyBefore({ type: 'nextRace' }, false)).toBe(false);
    expect(deps.version).toBe('0.1.0');
  });

  it('questionário ao sair, pular, sem pedido e desligado', () => {
    const { deps, clock, writes } = fakeDeps();
    const log = createPlaylog(deps);
    log.sessionStart();
    log.raceEnded(fakeRace({ abandoned: true }));
    expect(log.askSurveyBefore({ type: 'quitApp' }, false)).toBe(true);
    expect(log.pendingSurvey()?.reason).toBe('quit');
    log.answerSurvey(null);
    expect(log.data().surveys[0]).toMatchObject({ skipped: true, again: null, difficulty: null, hurdle: null });
    // O pulado conta como a resposta do dia.
    clock.now += HOUR;
    log.sessionStart();
    for (let i = 0; i < 3; i++) log.raceEnded(fakeRace());
    expect(log.askSurveyBefore({ type: 'nextRace' }, false)).toBe(false);
    // Dia seguinte, sessão sem corrida: nada a perguntar ao sair.
    clock.now += DAY;
    log.sessionStart();
    expect(log.askSurveyBefore({ type: 'quitApp' }, false)).toBe(false);
    // Sem pedido, a resposta não grava.
    const surveys = log.data().surveys.length;
    const written = writes.length;
    log.answerSurvey({ again: 1, difficulty: 'easy', hurdle: 'other' });
    expect(log.data().surveys).toHaveLength(surveys);
    expect(writes).toHaveLength(written);
    // Desligado (?nosurvey=1): nunca pergunta.
    const off = createPlaylog({ ...fakeDeps().deps, survey: false });
    off.sessionStart();
    for (let i = 0; i < 3; i++) off.raceEnded(fakeRace());
    expect(off.askSurveyBefore({ type: 'nextRace' }, false)).toBe(false);
    expect(off.askSurveyBefore({ type: 'quitApp' }, false)).toBe(false);
  });

  it('resumo e relatório: métricas da §6, sem nome de jogador', () => {
    const { deps, clock } = fakeDeps();
    const log = createPlaylog(deps);
    log.sessionStart();
    log.raceEnded(fakeRace({ mode: 'cup', cup: 'br_rj', eliminated: true }));
    log.raceEnded(fakeRace({ mode: 'cup', cup: 'br_rj' }));
    log.askSurveyBefore({ type: 'quitApp' }, false);
    log.answerSurvey({ again: 4, difficulty: 'right', hurdle: 'none' });
    clock.now += DAY;
    log.sessionStart();
    log.raceEnded(fakeRace());
    log.askSurveyBefore({ type: 'quitApp' }, false);
    log.answerSurvey({ again: 5, difficulty: 'hard', hurdle: 'controls' });
    expect(summarizePlaylog(log.data())).toMatchObject({
      sessions: 2, days: 2, races: 3, abandoned: 0, eliminated: 1, retriedAfterElimination: 1, racesPerSession: 1.5,
      surveysAnswered: 2, surveysSkipped: 0, againMean: 4.5, firstRaceFinished: true,
    });
    const text = log.reportText();
    expect(text.startsWith('Nitro Crew — play log\n')).toBe(true);
    expect(JSON.parse(text.split('--- json ---\n')[1])).toEqual(log.data());
    expect(text).toBe(formatPlaylogReport(log.data(), { version: '0.1.0', generated: new Date(clock.now).toISOString() }));

    const priv = createPlaylog(fakeDeps().deps);
    priv.sessionStart();
    const r = shortRace([{ ...human(0), name: 'Ana Maria' }]);
    r.step();
    priv.raceEnded(playlogRace({ state: r.state, track: r.track, mode: 'quick', telemetry: r.telemetry, localSeats: [0], driver: null, feel: r.feel }, { abandoned: false, champ: null }));
    expect(priv.data().races).toHaveLength(1);
    expect(priv.reportText()).not.toContain('Ana Maria');
  });

  it('revezamento: os dois assentos da dupla herdam posição e chegada do carro, e o termômetro conta uma vez', () => {
    const r = shortRace([human(0), human(1)], 'relay');
    r.step();
    expect(r.state.phase).toBe('finished');
    const relay = playlogRace({ state: r.state, track: r.track, mode: 'relay', telemetry: r.telemetry, localSeats: [0, 1], driver: null, feel: r.feel }, { abandoned: false, champ: null });
    expect(relay.seats[0].car).toBe(relay.seats[1].car);
    expect(relay.seats[1].position).toBe(relay.seats[0].position);
    expect(relay.seats[0].position).toBeGreaterThan(0);
    expect(relay.seats[0].finished).toBe(true);
    expect(relay.seats[1].finished).toBe(true);
    expect(relay.seats[1].feel).toEqual(relay.seats[0].feel);
    const F: RaceFeel = { seconds: 100, overtakes: 5, overtakesPerMin: 3, battleSeconds: 50, aloneSeconds: 10, flatOutPct: 40, nitroPct: 2, scrapes: 3, rearHits: 1 };
    const { log } = logWithRaces(0);
    log.raceEnded(relay);
    log.raceEnded(fakeRace({ seats: [{ ...fakeRace().seats[0], car: 7, feel: F }] }));
    const mean = summarizePlaylog(log.data()).feelMean;
    expect(relay.seats[0].feel).not.toBeNull();
    expect(mean?.seconds).toBe(Math.round((((relay.seats[0].feel?.seconds ?? 0) + 100) / 2) * 100) / 100);
  });
});
