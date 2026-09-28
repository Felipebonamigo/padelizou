// Tutorial de 90 segundos (passo 1.9): pista própria, máquina de passos, sessão (driver) e save.
import { describe, expect, it } from 'vitest';
import { PIT_X, SEGMENT_LENGTH, SPEED_TO_KMH, TICK_RATE, TOW_MIN_SPEED_FACTOR } from '../src/core/constants';
import { carDef } from '../src/core/data/cars';
import { CUPS } from '../src/core/data/cups';
import { centrifugalRate, holdableSpeedFraction, steerRate } from '../src/core/sim/physics';
import { createRace, stepRace } from '../src/core/sim/race';
import { carStats } from '../src/core/sim/stats';
import { getTrack, TRACKS, trackDef } from '../src/core/track';
import { NEUTRAL_INPUT, type CarState, type HumanEntry, type PlayerInput, type RaceConfig, type RaceState, type Track } from '../src/core/types';
import { DEFAULT_SAVE, DEFAULT_SETTINGS, type DeviceInfo, type RaceDriver, type SaveData, type TutorialDoneData } from '../src/game/contracts';
import { sanitizeSave } from '../src/game/save';
import {
  newTutorial, REFUEL_TARGET, skipTutorial, START_FUEL, strongCurveEntryLimit, TARGET_KMH, tutorialInput, tutorialView,
  updateTutorial, type TutorialEvent, type TutorialState,
} from '../src/game/tutorial';
import { actionKeys, createTutorialSession, describeSeat, END_DELAY_TICKS, type TutorialHost } from '../src/game/tutorial-session';
import { inSection, MEDIUM_CURVE, STRONG_CURVE, TUTORIAL_LAPS, TUTORIAL_SECTIONS, TUTORIAL_TRACK_ID, tutorialTrack } from '../src/game/tutorial-track';
import { setLanguage } from '../src/i18n';
import { DEFAULT_BINDINGS, type ControlBindings } from '../src/ui/remap/bindings';
import { shouldOfferTutorial, tutorialHumans } from '../src/ui/screens/tutorial';
import { ALL_ASSISTS, human, humanCar, skipCountdown } from './helpers';

setLanguage('pt');

function tutorialRace(players = 1): { state: RaceState; track: Track } {
  const track = tutorialTrack();
  const humans = Array.from({ length: players }, (_, i) => human(i));
  const config: RaceConfig = {
    trackId: TUTORIAL_TRACK_ID, laps: TUTORIAL_LAPS, humans, totalCars: players, difficulty: 'profissional',
    manualGear: false, assists: ALL_ASSISTS, seed: 5,
  };
  const state = createRace(config, track);
  skipCountdown(state, track);
  return { state, track };
}

/** Põe o carro no segmento dado (com o que mais for preciso) e roda um tick da máquina, sem eventos da simulação. */
function at(tut: TutorialState, state: RaceState, seat: number, seg: number, patch: Partial<CarState> = {}): TutorialEvent[] {
  const car = humanCar(state, seat);
  Object.assign(car, { z: seg * SEGMENT_LENGTH + 20, x: 0, inPit: false }, patch);
  state.events = [];
  return updateTutorial(tut, state);
}

const kmh = (v: number) => v / SPEED_TO_KMH;
const types = (evs: TutorialEvent[]) => evs.map((e) => e.type);

// ───────────────────────────── Pista ─────────────────────────────

describe('pista do tutorial', () => {
  it('fica fora de TRACKS e das copas, mas getTrack a monta', () => {
    expect(TRACKS.some((d) => d.id === TUTORIAL_TRACK_ID)).toBe(false);
    expect(CUPS.some((c) => c.trackIds.includes(TUTORIAL_TRACK_ID))).toBe(false);
    expect(() => trackDef(TUTORIAL_TRACK_ID)).toThrow();
    expect(getTrack(TUTORIAL_TRACK_ID)).toBe(tutorialTrack());
  });

  it('trechos contíguos, na ordem das lições, com o traçado que cada uma pede', () => {
    const t = tutorialTrack();
    const order = ['accel', 'curve', 'brakeZone', 'strong', 'nitro', 'pit', 'grid'] as const;
    let at0 = 0;
    for (const id of order) { expect(TUTORIAL_SECTIONS[id].start, id).toBe(at0); at0 = TUTORIAL_SECTIONS[id].end; }
    expect(at0).toBe(t.segments.length);
    const curveOf = (id: typeof order[number]) => t.segments.slice(TUTORIAL_SECTIONS[id].start, TUTORIAL_SECTIONS[id].end).map((s) => s.curve);
    expect(Math.max(...curveOf('curve'))).toBe(MEDIUM_CURVE);
    expect(Math.min(...curveOf('strong'))).toBe(STRONG_CURVE);
    for (const id of ['accel', 'brakeZone', 'nitro', 'pit', 'grid'] as const) expect(curveOf(id).every((c) => c === 0), id).toBe(true);
    t.segments.forEach((s, i) => expect(s.pit, `segmento ${i}`).toBe(inSection('pit', i)));
  });

  it('curta (volta de ~30 s a toda) e com a curva média segura sem frear, e a forte não', () => {
    const t = tutorialTrack();
    expect(t.segments.length * SEGMENT_LENGTH / 6000).toBeLessThan(25);
    const falcao = carDef('falcao');
    expect(holdableSpeedFraction(falcao, MEDIUM_CURVE)).toBeGreaterThan(0.75);
    expect(holdableSpeedFraction(falcao, STRONG_CURVE)).toBeLessThan(0.6);
  });

  it('o grid de até 4 jogadores fica na reta depois do box, antes da linha', () => {
    const { state } = tutorialRace(4);
    for (const c of state.cars) expect(inSection('grid', Math.floor(c.z / SEGMENT_LENGTH)), `${c.name} z=${c.z}`).toBe(true);
  });

  it('a IA completa uma volta sem travar (3 carros de IA e um humano parado)', () => {
    const track = tutorialTrack();
    const config: RaceConfig = {
      trackId: TUTORIAL_TRACK_ID, laps: 3, humans: [human(0)], totalCars: 4, difficulty: 'amador',
      manualGear: false, assists: ALL_ASSISTS, seed: 3,
    };
    const state = createRace(config, track);
    const ai = state.cars.filter((c) => c.seat < 0);
    let stuck = 0;
    for (let i = 0; i < TICK_RATE * 60 && !ai.every((c) => c.lap >= 2); i++) {
      stepRace(state, track, [NEUTRAL_INPUT]);
      if (state.phase === 'racing' && state.tick > state.startTick + TICK_RATE * 6) for (const c of ai) if (c.speed < c.stats.topSpeed * 0.1) stuck++;
    }
    expect(ai.map((c) => c.lap)).toEqual(ai.map(() => 2));
    expect(stuck).toBeLessThan(ai.length * 30);
  });
});

// ───────────────────────────── Máquina de passos ─────────────────────────────

describe('máquina de passos', () => {
  it('passo 1: não avança abaixo de 150 km/h; avança ao chegar', () => {
    const { state } = tutorialRace();
    const tut = newTutorial([0]);
    expect(types(at(tut, state, 0, 10, { speed: kmh(TARGET_KMH - 1) }))).toEqual([]);
    expect(tut.seats[0].step).toBe(0);
    expect(types(at(tut, state, 0, 11, { speed: kmh(TARGET_KMH) }))).toEqual(['stepDone']);
    expect(tutorialView(tut, state, 0).step).toBe('curve');
  });

  it('passo 2: sair do asfalto na curva manda repetir; passagem limpa do começo ao fim avança', () => {
    const { state } = tutorialRace();
    const tut = newTutorial([0]);
    tut.seats[0].step = 1;
    const c = TUTORIAL_SECTIONS.curve;
    at(tut, state, 0, c.start - 1, { speed: 4000 });
    at(tut, state, 0, c.start);
    const evs = at(tut, state, 0, c.start + 30, { x: -1.2 });
    expect(evs).toEqual([{ type: 'retry', seat: 0, step: 'curve', reason: 'offroad' }]);
    expect(types(at(tut, state, 0, c.end))).toEqual([]); // saiu da curva: nada conta
    expect(tut.seats[0].step).toBe(1);
    expect(tutorialView(tut, state, 0).retry).toBe('offroad');
    // Próxima volta: limpa.
    at(tut, state, 0, c.start - 1);
    for (let s = c.start; s < c.end; s += 10) at(tut, state, 0, s, { x: 0.3 });
    expect(types(at(tut, state, 0, c.end))).toEqual(['stepDone']);
    expect(tutorialView(tut, state, 0).retry).toBeNull();
  });

  it('passo 2: ativado no meio da curva, espera a próxima passagem inteira', () => {
    const { state } = tutorialRace();
    const tut = newTutorial([0]);
    at(tut, state, 0, TUTORIAL_SECTIONS.curve.start + 20, { speed: kmh(200) }); // passo 1 cumprido já dentro da curva
    expect(tut.seats[0].step).toBe(1);
    expect(types(at(tut, state, 0, TUTORIAL_SECTIONS.curve.end))).toEqual([]);
    expect(tut.seats[0].step).toBe(1);
  });

  it('passo 3: entrar rápido demais na curva forte ensina de novo; freando antes, avança', () => {
    const { state } = tutorialRace();
    const tut = newTutorial([0]);
    tut.seats[0].step = 2;
    const car = humanCar(state, 0);
    const s = TUTORIAL_SECTIONS.strong;
    const limit = strongCurveEntryLimit(car);
    at(tut, state, 0, s.start - 1, { speed: limit * 1.4 });
    expect(at(tut, state, 0, s.start, { speed: limit * 1.4 })).toEqual([{ type: 'retry', seat: 0, step: 'brake', reason: 'fast' }]);
    expect(types(at(tut, state, 0, s.end, { speed: 3000 }))).toEqual([]);
    at(tut, state, 0, s.start - 1, { speed: limit * 0.9 });
    expect(types(at(tut, state, 0, s.start, { speed: limit * 0.9 }))).toEqual([]);
    expect(types(at(tut, state, 0, s.end))).toEqual(['stepDone']);
    expect(tutorialView(tut, state, 0).step).toBe('nitro');
  });

  it('passo 4: só o nitro disparado conta', () => {
    const { state } = tutorialRace();
    const tut = newTutorial([0]);
    tut.seats[0].step = 3;
    expect(types(at(tut, state, 0, TUTORIAL_SECTIONS.nitro.start + 5, { speed: 5000 }))).toEqual([]);
    const car = humanCar(state, 0);
    state.events = [{ type: 'nitro', carId: car.id }];
    expect(types(updateTutorial(tut, state))).toEqual(['stepDone']);
  });

  it('passo 4 sem carga de nitro: avisa e segue para o box', () => {
    const { state } = tutorialRace();
    const tut = newTutorial([0]);
    tut.seats[0].step = 3;
    state.teamNitro[0] = 0;
    expect(types(at(tut, state, 0, TUTORIAL_SECTIONS.nitro.start + 5))).toEqual(['noNitro', 'stepDone']);
  });

  it('passo 5: abastecer no box conclui (sozinho, é o último); passar reto manda repetir', () => {
    const { state } = tutorialRace();
    const tut = newTutorial([0]);
    tut.seats[0].step = 4;
    const p = TUTORIAL_SECTIONS.pit;
    at(tut, state, 0, p.start + 2, { fuel: START_FUEL });
    expect(types(at(tut, state, 0, p.end, { fuel: START_FUEL }))).toEqual(['retry']);
    expect(tutorialView(tut, state, 0).retry).toBe('pitMissed');
    // Entrou na faixa mas atravessou embalado: saiu sem abastecer.
    at(tut, state, 0, p.start + 5, { x: PIT_X, inPit: true, fuel: 0.4 });
    expect(at(tut, state, 0, p.end, { fuel: 0.45 })).toEqual([{ type: 'retry', seat: 0, step: 'pit', reason: 'pitShort' }]);
    at(tut, state, 0, p.start - 3);
    expect(types(at(tut, state, 0, p.start + 5, { x: PIT_X, inPit: true, fuel: REFUEL_TARGET - 0.01 }))).toEqual([]);
    expect(types(at(tut, state, 0, p.start + 6, { x: PIT_X, inPit: true, fuel: REFUEL_TARGET }))).toEqual(['stepDone', 'done']);
    expect(tut.phase).toBe('done');
    expect(tutorialView(tut, state, 0).step).toBe('done');
  });

  it('com 1 jogador o passo 6 (empurrão) não existe: 5 passos', () => {
    const { state } = tutorialRace(1);
    const tut = newTutorial([0]);
    expect(tut.total).toBe(5);
    expect(tutorialView(tut, state, 0)).toMatchObject({ index: 1, total: 5 });
    expect(newTutorial([0, 1]).total).toBe(6);
  });

  it('passo 6 com 2 jogadores: espera a equipe, para o líder na reta e conclui com o empurrão', () => {
    const { state } = tutorialRace(2);
    const tut = newTutorial([0, 1]);
    tut.seats[0].step = 5;
    tut.seats[1].step = 4;
    expect(tutorialView(tut, state, 0).step).toBe('wait');
    const p1 = humanCar(state, 0); const p2 = humanCar(state, 1);
    // P2 abastece: todos passaram do box. P1 está à frente → é quem vai parar.
    Object.assign(p1, { z: (TUTORIAL_SECTIONS.grid.start + 5) * SEGMENT_LENGTH, x: 0 });
    Object.assign(p2, { z: (TUTORIAL_SECTIONS.pit.start + 5) * SEGMENT_LENGTH, x: PIT_X, inPit: true, fuel: 0.9 });
    state.events = [];
    expect(types(updateTutorial(tut, state))).toEqual(['stepDone']);
    expect(tut.towSeat).toBe(0);
    expect(tut.parked).toBe(false);
    expect(tutorialView(tut, state, 0).step).toBe('stalled');
    expect(tutorialView(tut, state, 1)).toMatchObject({ step: 'tow', index: 6, total: 6, parked: false });
    const throttle: PlayerInput = { ...NEUTRAL_INPUT, throttle: true };
    expect(tutorialInput(tut, 0, throttle)).toBe(throttle);
    // Na reta, no asfalto: para.
    p2.inPit = false;
    expect(at(tut, state, 0, TUTORIAL_SECTIONS.accel.start + 3)).toEqual([{ type: 'parked', seat: 0 }]);
    expect(tutorialInput(tut, 0, throttle)).toEqual({ ...NEUTRAL_INPUT, brake: true });
    expect(tutorialInput(tut, 1, throttle)).toBe(throttle);
    expect(tutorialView(tut, state, 0)).toMatchObject({ step: 'stalled', parked: true });
    // Empurrão de outro carro não conta; o do companheiro no carro parado conclui.
    state.events = [{ type: 'tow', carId: p2.id, byId: p1.id }];
    expect(types(updateTutorial(tut, state))).toEqual([]);
    state.events = [{ type: 'tow', carId: p1.id, byId: p2.id }];
    expect(types(updateTutorial(tut, state))).toEqual(['towed', 'stepDone', 'stepDone', 'done']);
    expect(tutorialInput(tut, 0, throttle)).toBe(throttle);
  });

  it('passo 6: companheiro que passa direto pelo carro parado é avisado', () => {
    const { state } = tutorialRace(2);
    const tut = newTutorial([0, 1]);
    tut.seats.forEach((s) => { s.step = 5; });
    const p1 = humanCar(state, 0);
    Object.assign(p1, { z: 30 * SEGMENT_LENGTH, x: 0, progress: 99_999 });
    at(tut, state, 0, 30);
    expect(tut.parked).toBe(true);
    at(tut, state, 1, 25);
    expect(types(at(tut, state, 1, 28))).toEqual([]);
    expect(at(tut, state, 1, 32)).toEqual([{ type: 'towMissed', seat: 1 }]);
    expect(types(at(tut, state, 1, 40))).toEqual([]);
  });

  it('pular encerra: nada mais avança', () => {
    const { state } = tutorialRace();
    const tut = newTutorial([0]);
    skipTutorial(tut);
    expect(tut.phase).toBe('skipped');
    expect(at(tut, state, 0, 10, { speed: kmh(200) })).toEqual([]);
    expect(tut.seats[0].step).toBe(0);
  });

  it('quem começa a última volta sem a equipe terminar encerra por tempo (ninguém cruza a chegada)', () => {
    const { state } = tutorialRace();
    const tut = newTutorial([0]);
    expect(types(at(tut, state, 0, 10, { lap: TUTORIAL_LAPS - 1 }))).toEqual([]);
    expect(types(at(tut, state, 0, 10, { lap: TUTORIAL_LAPS }))).toEqual(['timeout']);
    expect(tut.phase).toBe('timeout');
  });
});

// ───────────────────────────── Painel ─────────────────────────────

describe('painel: teclas do dispositivo de cada jogador', () => {
  const view = (step: 'throttle' | 'curve' | 'nitro') => ({ step, index: 1, total: 6, actions: step === 'curve' ? ['left', 'right'] as const : [step] as const, retry: null, progress: null, towSeat: -1, parked: false });
  const who = { seat: 0, name: 'Ana', color: '#fff' };

  it('teclado 1 e 2 com o padrão; controle com nomes Xbox ou PlayStation', () => {
    expect(actionKeys('kb1', 'throttle', DEFAULT_BINDINGS)).toBe('↑');
    expect(actionKeys('kb2', 'nitro', DEFAULT_BINDINGS)).toBe('F');
    expect(actionKeys('gp0', 'nitro', DEFAULT_BINDINGS, 'Controle 1 — Xbox Wireless Controller')).toBe('RB');
    expect(actionKeys('gp1', 'nitro', DEFAULT_BINDINGS, 'Controle 2 — DualSense Wireless Controller')).toBe('R1');
    expect(describeSeat({ ...view('nitro'), actions: ['nitro'] }, who, '', 'kb1', DEFAULT_BINDINGS).skip).toBe('Esc: pular tutorial');
    expect(describeSeat({ ...view('nitro'), actions: ['nitro'] }, who, '', 'gp0', DEFAULT_BINDINGS, 'Xbox').skip).toBe('Start: pular tutorial');
  });

  it('segue o remapeamento das opções', () => {
    const controls: ControlBindings = JSON.parse(JSON.stringify(DEFAULT_BINDINGS)) as ControlBindings;
    controls.kb2.nitro = ['KeyR'];
    controls.gamepad.nitro = [3];
    const kb2 = describeSeat({ ...view('nitro'), actions: ['nitro'] }, who, '', 'kb2', controls);
    expect(kb2.keys).toEqual([{ label: 'Nitro', keys: 'R' }]);
    expect(describeSeat({ ...view('nitro'), actions: ['nitro'] }, who, '', 'gp0', controls, 'Xbox').keys[0].keys).toBe('Y');
  });

  it('esquerda e direita num chip só ("Virar"); o controle lembra do analógico', () => {
    const kb = describeSeat({ ...view('curve'), actions: ['left', 'right'] }, who, '', 'kb1', DEFAULT_BINDINGS);
    expect(kb.keys).toEqual([{ label: 'Virar', keys: '← →' }]);
    const pad = describeSeat({ ...view('curve'), actions: ['left', 'right'] }, who, '', 'gp0', DEFAULT_BINDINGS, 'Xbox');
    expect(pad.keys[0].keys).toBe('Analógico / D-pad ← D-pad →');
    expect(kb.counter).toBe('PASSO 1/6');
    expect(kb.title).toBe('Curva média');
  });

  it('textos do passo 6 mudam antes e depois de o carro parar, com o nome de quem parou', () => {
    const base = { index: 6, total: 6, actions: [] as never[], retry: null, progress: null, towSeat: 0 };
    expect(describeSeat({ ...base, step: 'tow', parked: false }, who, 'Bia', 'kb1', DEFAULT_BINDINGS).text).toContain('fique atrás');
    expect(describeSeat({ ...base, step: 'tow', parked: true }, who, 'Bia', 'kb1', DEFAULT_BINDINGS).text).toContain('Bia parou');
    expect(describeSeat({ ...base, step: 'stalled', parked: true }, who, 'Bia', 'kb1', DEFAULT_BINDINGS).text).toContain('Seu carro parou');
  });
});

// ───────────────────────────── Sessão (driver) e save ─────────────────────────────

interface FakeRun {
  host: TutorialHost;
  shown: Array<{ screen: string; data: TutorialDoneData | undefined }>;
  hud: Array<{ seat: number; text: string; kind: string }>;
  persisted: number;
  cups: Array<{ cupId: string; humans: HumanEntry[] }>;
  race: { driver: RaceDriver; state: RaceState; track: Track } | null;
  idled: number;
}

function fakeHost(save: SaveData = sanitizeSave({})): FakeRun {
  const seats: Array<string | null> = ['kb1', 'kb2', null, null];
  const devices: DeviceInfo[] = [];
  const run: FakeRun = { host: null as unknown as TutorialHost, shown: [], hud: [], persisted: 0, cups: [], race: null, idled: 0 };
  run.host = {
    save, settings: { ...DEFAULT_SETTINGS },
    menus: { show: ((screen: string, data?: TutorialDoneData) => { run.shown.push({ screen, data }); }) as TutorialHost['menus']['show'] },
    input: {
      seatDevice: (s) => seats[s] ?? null,
      bindSeat: (s, d) => { seats[s] = d; },
      unbindSeat: (s) => { seats[s] = null; },
      devices: () => devices,
    },
    beginRace: (_config, _humans, driver, state) => { run.race = { driver, state, track: tutorialTrack() }; },
    toIdle: () => { run.idled++; run.race?.driver.dispose(); run.race = null; },
    startCup: (cupId, humans) => { run.cups.push({ cupId, humans }); },
    randomSeed: () => 9,
    persist: () => { run.persisted++; },
    hud: (seat, text, kind) => { run.hud.push({ seat, text, kind }); },
    panel: null,
  };
  return run;
}

/**
 * Piloto roteirizado que faz o que o painel pede: acelera, compensa as curvas, freia antes da curva
 * forte, nitro na reta, entra no box, e no passo 6 fica atrás do carro parado até ele parar e então
 * passa colado acelerando.
 */
function botInput(state: RaceState, track: Track, tut: TutorialState, seat: number, delayTicks: number): PlayerInput {
  const car = humanCar(state, seat);
  const s = carStats(car);
  const seg = Math.floor(car.z / SEGMENT_LENGTH);
  const view = tutorialView(tut, state, seat);
  if (state.tick < state.startTick + delayTicks) return NEUTRAL_INPUT;
  let lane = 0;
  let throttle = true;
  let brake = false;
  const strong = TUTORIAL_SECTIONS.strong;
  const limit = holdableSpeedFraction(s, STRONG_CURVE) * s.topSpeed;
  if (seg >= strong.start - 30 && seg < strong.end - 10 && car.speed > limit) { throttle = false; brake = true; }
  const nitro = view.step === 'nitro' && car.nitroTicks === 0 && state.tick % 20 === 0;
  if (view.step === 'pit' && seg >= TUTORIAL_SECTIONS.pit.start - 25 && seg < TUTORIAL_SECTIONS.pit.end) {
    if (seg >= TUTORIAL_SECTIONS.pit.start - 4) lane = PIT_X;
    if (car.speed > s.topSpeed * 0.3) { throttle = false; brake = true; }
  }
  if (view.step === 'tow' && tut.towSeat >= 0) {
    const target = humanCar(state, tut.towSeat);
    lane = target.x;
    const gap = target.progress - car.progress;
    if (tut.parked && target.speed > target.stats.topSpeed * TOW_MIN_SPEED_FACTOR && gap > 0 && gap < SEGMENT_LENGTH * 25) { throttle = false; brake = car.speed > 1500; }
  }
  const sf = Math.min(1, car.speed / s.topSpeed);
  const curve = track.segments[seg % track.segments.length].curve;
  const counter = sf > 0.05 ? (centrifugalRate(s) * sf * curve) / steerRate(s) : 0;
  const steer = Math.max(-1, Math.min(1, (lane - car.x) * 5 + counter));
  return { steer, throttle, brake, nitro, gearUp: false, gearDown: false };
}

function playThrough(players: number, maxSeconds: number): { run: FakeRun; seconds: number; tut: TutorialState } {
  const run = fakeHost();
  const session = createTutorialSession(run.host);
  session.start(Array.from({ length: players }, (_, i) => human(i)));
  const race = run.race;
  if (!race) throw new Error('o tutorial não começou a corrida');
  const tut = session.current();
  if (!tut) throw new Error('sem máquina');
  const { state, track } = race;
  while (run.race && state.tick < state.startTick + maxSeconds * TICK_RATE) {
    const local: PlayerInput[] = [];
    for (let seat = 0; seat < players; seat++) local[seat] = botInput(state, track, tut, seat, seat * 50);
    race.driver.force(1, local, (inputs) => stepRace(state, track, inputs));
  }
  return { run, seconds: (state.tick - state.startTick) / TICK_RATE, tut };
}

describe('sessão do tutorial', () => {
  it('monta a corrida: pista do tutorial, sem IA, tanque baixo, co-op, câmbio automático', () => {
    const run = fakeHost();
    const session = createTutorialSession(run.host);
    run.host.settings.manualGear = true;
    session.start([human(0), { ...human(1), teamId: 1 }]);
    const state = run.race?.state;
    expect(state?.trackId).toBe(TUTORIAL_TRACK_ID);
    expect(state?.cars.length).toBe(2);
    expect(state?.cars.every((c) => c.seat >= 0 && c.fuel === START_FUEL && c.teamId === 0)).toBe(true);
    expect(state?.config.manualGear).toBe(false);
    expect(state?.config.assists.tow).toBe(true);
  });

  it('1 jogador: um piloto que segue o painel conclui os 5 passos em menos de 90 s; tela final e flag no save', () => {
    const { run, seconds, tut } = playThrough(1, 120);
    expect(tut.phase).toBe('done');
    expect(seconds).toBeLessThan(90);
    expect(run.shown).toEqual([{ screen: 'tutorialDone', data: { completed: true, players: 1 } }]);
    expect(run.host.save.tutorialDone).toBe(true);
    expect(run.persisted).toBeGreaterThan(0);
    expect(run.race).toBeNull();
    expect(run.hud.some((m) => m.kind === 'big' && m.text === 'TUTORIAL CONCLUÍDO!')).toBe(true);
  }, 30_000);

  it('2 jogadores: inclui o empurrão no companheiro parado, em menos de 90 s', () => {
    const { run, seconds, tut } = playThrough(2, 150);
    expect(tut.phase, `passos ${tut.seats.map((s) => s.step).join(',')} towSeat ${tut.towSeat} parked ${tut.parked}`).toBe('done');
    expect(seconds).toBeLessThan(90);
    expect(run.shown[0]).toEqual({ screen: 'tutorialDone', data: { completed: true, players: 2 } });
  }, 30_000);

  it('a tela final espera o "concluído!" na tela antes de aparecer', () => {
    const run = fakeHost();
    const session = createTutorialSession(run.host);
    session.start([human(0)]);
    const race = run.race;
    const tut = session.current();
    if (!race || !tut) throw new Error('sem corrida');
    tut.seats[0].step = 4;
    const car = humanCar(race.state, 0);
    Object.assign(car, { z: (TUTORIAL_SECTIONS.pit.start + 5) * SEGMENT_LENGTH, x: PIT_X, fuel: 0.95 });
    race.driver.force(COUNTDOWN_PLUS, [NEUTRAL_INPUT], (i) => stepRace(race.state, race.track, i));
    expect(tut.phase).toBe('done');
    expect(run.shown).toEqual([]);
    const stopped = race.state.tick;
    race.driver.force(END_DELAY_TICKS + 5, [NEUTRAL_INPUT], (i) => stepRace(race.state, race.track, i));
    expect(race.state.tick - stopped).toBeLessThanOrEqual(END_DELAY_TICKS);
    expect(run.shown).toEqual([{ screen: 'tutorialDone', data: { completed: true, players: 1 } }]);
  });

  it('Esc/Start pula: encerra na hora, grava a flag e mostra as regras de ouro (não concluído)', () => {
    const run = fakeHost();
    const session = createTutorialSession(run.host);
    session.start([human(0)]);
    run.race?.driver.pauseKey();
    expect(run.race).toBeNull();
    expect(run.idled).toBe(1);
    expect(run.host.save.tutorialDone).toBe(true);
    expect(run.persisted).toBe(1);
    expect(run.shown).toEqual([{ screen: 'tutorialDone', data: { completed: false, players: 1 } }]);
    expect(session.current()).toBeNull();
  });

  it('driver descartado (menu principal) não roda mais ticks', () => {
    const run = fakeHost();
    const session = createTutorialSession(run.host);
    session.start([human(0)]);
    const race = run.race;
    if (!race) throw new Error('sem corrida');
    race.driver.dispose();
    let steps = 0;
    expect(race.driver.advance(1, [NEUTRAL_INPUT], () => { steps++; })).toBe(0);
    expect(steps).toBe(0);
  });

  it('"Primeira copa" começa a primeira copa com os mesmos jogadores', () => {
    const run = fakeHost();
    const session = createTutorialSession(run.host);
    session.firstCup(); // sem tutorial antes: nada
    expect(run.cups).toEqual([]);
    session.start([human(0), human(1)]);
    run.race?.driver.pauseKey();
    session.firstCup();
    expect(run.cups.map((c) => [c.cupId, c.humans.map((h) => h.seat)])).toEqual([[CUPS[0].id, [0, 1]]]);
  });
});

const COUNTDOWN_PLUS = 60 * 4;

describe('save e oferta no menu', () => {
  it('flag tutorialDone: padrão falso, só true vale', () => {
    expect(DEFAULT_SAVE.tutorialDone).toBe(false);
    expect(sanitizeSave({}).tutorialDone).toBe(false);
    expect(sanitizeSave({ tutorialDone: true }).tutorialDone).toBe(true);
    expect(sanitizeSave({ tutorialDone: 'sim' }).tutorialDone).toBe(false);
  });

  it('o menu oferece o tutorial só na primeira abertura: nunca correu e nunca fez/pulou', () => {
    expect(shouldOfferTutorial({ tutorialDone: false, racesRun: 0 })).toBe(true);
    expect(shouldOfferTutorial({ tutorialDone: true, racesRun: 0 })).toBe(false);
    expect(shouldOfferTutorial({ tutorialDone: false, racesRun: 3 })).toBe(false);
  });

  it('jogadores do tutorial: nome e carro lembrados por assento, todos no mesmo time', () => {
    const save = sanitizeSave({ seatNames: ['Ana', 'Bia'], seatCars: ['tornado', 'sucuri'] });
    const cars = [
      { id: 'falcao', price: 0 }, { id: 'tornado', price: 0 }, { id: 'sucuri', price: 16000 },
    ] as unknown as Parameters<typeof tutorialHumans>[0]['cars'];
    const hs = tutorialHumans({ cars, save }, [1, 0]);
    expect(hs.map((h) => [h.seat, h.name, h.carId, h.teamId])).toEqual([[0, 'Ana', 'tornado', 0], [1, 'Bia', 'falcao', 0]]);
  });
});
