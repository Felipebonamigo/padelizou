// Estatísticas por jogador, conquistas novas e o saneamento do que vai para o save.
import { describe, expect, it } from 'vitest';
import { TICK_RATE } from '../src/core/constants';
import { stepRace } from '../src/core/sim/race';
import { TRACKS } from '../src/core/track';
import { NEUTRAL_INPUT, type HumanEntry, type PlayerInput, type RaceResultRow, type RaceState, type Track } from '../src/core/types';
import { setLanguage } from '../src/i18n';
import {
  achievementMessages, DRAFT_MASTER_TICKS, MARATHON_METERS, newTelemetry, observeTick, unlockAchievements, WINS_TARGET,
  type RaceTelemetry, type SeatTelemetry,
} from '../src/game/achievements';
import type { RaceMode, SaveData } from '../src/game/contracts';
import { NAME_MAX_LENGTH, recordRaceResults, sanitizeSave } from '../src/game/save';
import {
  EMPTY_STATS, formatDistance, formatDuration, MAX_PROFILES, METERS_PER_UNIT, PROFILE_NAME_MAX, raceContributions, recordRaceStats,
  sanitizeStats, type StatsData,
} from '../src/game/stats';
import { ALL_ASSISTS, human, quickRace, skipCountdown, syntheticTrack } from './helpers';

function row(seat: number, position: number, totalTicks: number, bestLapTicks: number, finished = true): RaceResultRow {
  return { carId: seat >= 0 ? 100 + seat : position, seat, name: seat >= 0 ? `P${seat + 1}` : `IA${position}`, teamId: seat >= 0 ? 0 : 100, carDefId: 'falcao', position, finished, totalTicks, bestLapTicks, points: 0 };
}
const solo: HumanEntry[] = [{ seat: 0, name: 'Ana', carId: 'falcao', teamId: 0, color: '#fff' }];

describe('save: contagem de vitórias', () => {
  it('contra-relógio conta como corrida, mas não como vitória (sozinho na pista, a posição é sempre 1)', () => {
    const save = sanitizeSave({});
    recordRaceResults(save, [row(0, 1, 9000, 2800)], solo, 'copacabana', 3, 'timetrial');
    expect(save.racesRun).toBe(1);
    expect(save.racesWon).toBe(0);
    recordRaceResults(save, [row(0, 1, 9000, 2800), row(-1, 2, 9100, 2900)], solo, 'copacabana', 3, 'quick');
    expect(save.racesWon).toBe(1);
  });
});

// ───────────────────────────── Corrida observada ─────────────────────────────

type SeatInput = (state: RaceState, seat: number) => PlayerInput;

/** Corre até o fim observando cada tick, como a sessão faz. */
function runObserved(state: RaceState, track: Track, tel: RaceTelemetry, input: SeatInput, maxTicks = TICK_RATE * 240): void {
  for (let i = 0; i < maxTicks && state.phase !== 'finished'; i++) {
    const inputs: PlayerInput[] = [];
    for (const c of state.cars) if (c.seat >= 0) inputs[c.seat] = input(state, c.seat);
    stepRace(state, track, inputs);
    observeTick(tel, state, track);
  }
}

const throttle: SeatInput = () => ({ ...NEUTRAL_INPUT, throttle: true });

describe('estatísticas de uma corrida simulada', () => {
  const humans = [human(0), human(1, 0, 'trovao')];
  const straight = syntheticTrack([{ op: 'straight', length: 300 }]);
  const { state, track } = quickRace({ track: straight, totalCars: 4, laps: 2, humans, assists: ALL_ASSISTS, seed: 11 });
  const tel = newTelemetry();
  // P1 aperta o nitro uma vez, 2 s depois da largada.
  const nitroTick = state.startTick + TICK_RATE * 2;
  runObserved(state, track, tel, (s, seat) => ({ ...NEUTRAL_INPUT, throttle: true, nitro: seat === 0 && s.tick === nitroTick }));
  const results = state.results ?? [];

  it('a corrida termina com os dois humanos classificados', () => {
    expect(state.phase).toBe('finished');
    expect(results.filter((r) => r.seat >= 0 && r.finished)).toHaveLength(2);
  });

  it('cada humano soma corrida, voltas, distância até a chegada, tempo, nitro e posição', () => {
    const list = raceContributions({ mode: 'quick', state, results, humans, telemetry: tel });
    expect(list.map((c) => c.seat)).toEqual([0, 1]);
    for (const c of list) {
      const r = results.find((x) => x.seat === c.seat);
      const seat = tel.seats.get(c.seat);
      if (!r || !seat) throw new Error('sem linha ou telemetria');
      expect(c.stats.races).toBe(1);
      expect(c.stats.laps).toBe(2);
      // Do grid até a linha de chegada: o piloto automático depois dela não conta.
      expect(c.stats.meters).toBe(Math.round((2 * track.length - seat.startProgress) * METERS_PER_UNIT));
      expect(c.stats.meters).toBeGreaterThan(2 * track.length * METERS_PER_UNIT);
      expect(c.stats.meters).toBeLessThan((2 * track.length + 4000) * METERS_PER_UNIT);
      expect(c.stats.raceTicks).toBe(r.totalTicks);
      expect(c.stats.wins).toBe(r.position === 1 ? 1 : 0);
      expect(c.stats.coopWins).toBe(r.position === 1 ? 1 : 0); // os dois na mesma equipe: co-op
      expect(c.stats.podiums).toBe(r.position <= 3 ? 1 : 0);
      expect(c.stats.bestPositions).toEqual({ sintetica: r.position });
      expect(c.stats.collisions).toBe(seat.collisions);
      expect(c.stats.crashes).toBe(seat.crashes);
    }
    expect(list[0].stats.nitros).toBe(1);
    expect(list[1].stats.nitros).toBe(0);
    // Uma pista reta de 300 segmentos tem 60.000 u: duas voltas são ~1,7 km.
    expect(list[0].stats.meters).toBeGreaterThan(1600);
    expect(list[0].stats.meters).toBeLessThan(1760);
  });

  it('acumula no total e nos perfis (P1 no topo) e soma de novo na corrida seguinte', () => {
    const stats = sanitizeStats({});
    const first = recordRaceStats(stats, { mode: 'quick', state, results, humans, telemetry: tel });
    expect(stats.players.map((p) => p.name)).toEqual(['P1', 'P2']);
    expect(stats.totals.races).toBe(2);
    expect(stats.totals.laps).toBe(4);
    expect(stats.totals.meters).toBe(first[0].stats.meters + first[1].stats.meters);
    expect(stats.totals.wins).toBe(results.some((r) => r.seat >= 0 && r.position === 1) ? 1 : 0);
    recordRaceStats(stats, { mode: 'quick', state, results, humans, telemetry: tel });
    expect(stats.totals.races).toBe(4);
    expect(stats.players[0].races).toBe(2);
    expect(stats.players[0].meters).toBe(2 * first[0].stats.meters);
    expect(stats.players[0].bestPositions.sintetica).toBe(first[0].stats.bestPositions.sintetica);
    // O save guarda e relê igual.
    expect(sanitizeStats(JSON.parse(JSON.stringify(stats)))).toEqual(stats);
  });

  it('contra-relógio soma corrida, voltas e distância, mas não vitória, pódio nem melhor posição', () => {
    const tt = quickRace({ track: straight, humans: [human(0)], laps: 1, timeTrial: true });
    const tTel = newTelemetry();
    runObserved(tt.state, tt.track, tTel, throttle);
    const [c] = raceContributions({ mode: 'timetrial', state: tt.state, results: tt.state.results ?? [], humans: [human(0)], telemetry: tTel });
    expect(c.stats).toMatchObject({ races: 1, wins: 0, podiums: 0, coopWins: 0, laps: 1, bestPositions: {} });
    expect(c.stats.meters).toBeGreaterThan(track.length * METERS_PER_UNIT);
  });

  it('melhor posição conta também sem IA na pista (versus só de humanos)', () => {
    const duo = [human(0, 0), human(1, 1)];
    const vs = quickRace({ humans: duo, totalCars: 2 });
    const list = raceContributions({ mode: 'quick', state: vs.state, results: [row(0, 2, 9000, 3000), row(1, 1, 8900, 2900)], humans: duo, telemetry: newTelemetry() });
    expect(list.map((c) => c.stats.bestPositions[vs.state.trackId])).toEqual([2, 1]);
    expect(list.map((c) => c.stats.wins)).toEqual([0, 1]);
    expect(list.map((c) => c.stats.coopWins)).toEqual([0, 0]); // equipes diferentes: versus, não co-op
  });

  it('quem não terminou soma o tempo até o fim da corrida', () => {
    const idle = quickRace({ track: straight, humans: [human(0), human(1)], totalCars: 4, laps: 1, seed: 5 });
    const iTel = newTelemetry();
    // P2 fica parado: a corrida acaba 45 s depois da chegada de P1.
    runObserved(idle.state, idle.track, iTel, (_s, seat) => (seat === 0 ? { ...NEUTRAL_INPUT, throttle: true } : NEUTRAL_INPUT));
    const res = idle.state.results ?? [];
    const p2 = res.find((r) => r.seat === 1);
    expect(p2?.finished).toBe(false);
    const list = raceContributions({ mode: 'quick', state: idle.state, results: res, humans: [human(0), human(1)], telemetry: iTel });
    const c2 = list.find((c) => c.seat === 1);
    expect(c2?.stats.raceTicks).toBe(idle.state.tick - 1 - idle.state.startTick);
    expect(c2?.stats.laps).toBe(0);
    // Parado no grid, só anda o que as batidas por trás empurram (quem é batido ganha velocidade).
    expect(c2?.stats.meters).toBeLessThan(50);
  });
});

// ───────────────────────────── Observação por tick ─────────────────────────────

describe('observeTick', () => {
  function racing(humans = [human(0)], totalCars = 4) {
    const r = quickRace({ track: syntheticTrack([{ op: 'straight', length: 300 }]), humans, totalCars, assists: ALL_ASSISTS });
    skipCountdown(r.state, r.track);
    return r;
  }

  it('conta vácuo atrás de um carro rápido e nada sem ninguém na frente', () => {
    const { state, track } = racing();
    const tel = newTelemetry();
    const me = state.cars.find((c) => c.seat === 0);
    const ai = state.cars.find((c) => c.seat < 0);
    if (!me || !ai) throw new Error('grid');
    for (const c of state.cars) { c.z = 1000; c.x = -0.9; c.speed = 0; }
    me.z = 20000; me.x = 0; me.speed = 4000;
    ai.z = 20600; ai.x = 0.1; ai.speed = 4000;
    state.events = [];
    observeTick(tel, state, track);
    expect(tel.seats.get(0)?.draftTicks).toBe(1);
    ai.z = 40000; // longe demais
    observeTick(tel, state, track);
    expect(tel.seats.get(0)?.draftTicks).toBe(1);
  });

  it('eventos: nitro, empurrão dado e recebido, colisão dos dois lados, batida, box', () => {
    const { state, track } = racing([human(0), human(1)]);
    const tel = newTelemetry();
    const p1 = state.cars.findIndex((c) => c.seat === 0);
    const p2 = state.cars.findIndex((c) => c.seat === 1);
    const ai = state.cars.findIndex((c) => c.seat < 0);
    state.events = [
      { type: 'nitro', carId: p1 }, { type: 'nitro', carId: ai },
      { type: 'tow', carId: p2, byId: p1 },
      { type: 'collision', carId: ai, otherId: p1, strength: 0.5 },
      { type: 'crash', carId: p2, sprite: 'tree' },
      { type: 'pit_enter', carId: p2 },
    ];
    observeTick(tel, state, track);
    expect(tel.seats.get(0)).toMatchObject({ nitros: 1, towsGiven: 1, towsReceived: 0, collisions: 1, crashes: 0, pitStops: 0 });
    expect(tel.seats.get(1)).toMatchObject({ nitros: 0, towsGiven: 0, towsReceived: 1, collisions: 0, crashes: 1, pitStops: 1 });
  });

  it('depois da chegada o piloto automático não soma nada para o jogador', () => {
    const { state, track } = racing();
    const tel = newTelemetry();
    observeTick(tel, state, track);
    const p1 = state.cars.findIndex((c) => c.seat === 0);
    state.cars[p1].finished = true;
    state.events = [{ type: 'crash', carId: p1, sprite: 'tree' }, { type: 'nitro', carId: p1 }];
    observeTick(tel, state, track);
    expect(tel.seats.get(0)).toMatchObject({ crashes: 0, nitros: 0 });
  });

  it('nitro ligado na chegada; e último ao fechar a primeira volta', () => {
    const { state, track } = racing([human(0), human(1)]);
    const tel = newTelemetry();
    const p1 = state.cars.findIndex((c) => c.seat === 0);
    const p2 = state.cars.findIndex((c) => c.seat === 1);
    state.cars[p1].finished = true; state.cars[p1].nitroTicks = 40;
    state.cars[p2].position = state.cars.length;
    state.events = [{ type: 'finish', carId: p1, position: 1 }, { type: 'lap', carId: p2, lap: 2, lapTicks: 900, best: true }];
    observeTick(tel, state, track);
    expect(tel.seats.get(0)?.nitroAtFinish).toBe(true);
    expect(tel.seats.get(1)?.lastAfterFirstLap).toBe(true);
  });

  it('sem nitro na chegada, ou em último só numa volta depois da primeira, não marca', () => {
    const { state, track } = racing([human(0), human(1)]);
    const tel = newTelemetry();
    const p1 = state.cars.findIndex((c) => c.seat === 0);
    const p2 = state.cars.findIndex((c) => c.seat === 1);
    state.cars[p1].finished = true; state.cars[p1].nitroTicks = 0;
    state.cars[p2].position = state.cars.length;
    state.events = [{ type: 'finish', carId: p1, position: 1 }, { type: 'lap', carId: p2, lap: 3, lapTicks: 900, best: true }];
    observeTick(tel, state, track);
    expect(tel.seats.get(0)?.nitroAtFinish).toBe(false);
    expect(tel.seats.get(1)?.lastAfterFirstLap).toBe(false);
    // Fechou a primeira volta em penúltimo: também não.
    state.cars[p2].position = state.cars.length - 1;
    state.events = [{ type: 'lap', carId: p2, lap: 2, lapTicks: 900, best: true }];
    observeTick(tel, state, track);
    expect(tel.seats.get(1)?.lastAfterFirstLap).toBe(false);
  });
});

// ───────────────────────────── Conquistas novas ─────────────────────────────

interface Scenario {
  mode?: RaceMode;
  humans?: HumanEntry[];
  /** Humanos (assento → posição); a IA preenche o resto. */
  positions?: Record<number, number>;
  finished?: boolean;
  seats?: Record<number, Partial<SeatTelemetry>>;
  stats?: StatsData;
  achievements?: string[];
  withAi?: boolean;
}

function evaluate(sc: Scenario) {
  const humans = sc.humans ?? [human(0)];
  const withAi = sc.withAi ?? true;
  const { state } = quickRace({ humans, totalCars: withAi ? 8 : humans.length, timeTrial: sc.mode === 'timetrial' });
  const positions = sc.positions ?? { 0: 1 };
  const taken = new Set(Object.values(positions));
  const rows: RaceResultRow[] = humans.map((h) => row(h.seat, positions[h.seat] ?? 8, 9000, 3000, sc.finished ?? true));
  let next = 1;
  for (let i = 0; withAi && i < 8 - humans.length; i++) {
    while (taken.has(next)) next++;
    rows.push(row(-1, next, 9100, 3100));
    taken.add(next);
  }
  const tel = newTelemetry();
  for (const h of humans) {
    tel.seats.set(h.seat, {
      nitros: 0, towsGiven: 0, towsReceived: 0, collisions: 0, crashes: 0, pitStops: 0, draftTicks: 0, startProgress: 0,
      nitroAtFinish: false, lastAfterFirstLap: false, ...(sc.seats?.[h.seat] ?? {}),
    });
    tel.pitted.add(h.seat); // fora do caminho de SEM_BOX
  }
  const save: SaveData = { ...sanitizeSave({}), achievements: sc.achievements ?? [], stats: sc.stats ?? sanitizeStats({}) };
  const unlocks = unlockAchievements(save, sc.mode ?? 'quick', state, rows, humans, tel, false, null, 'profissional');
  return { ids: unlocks.map((u) => u.id), unlocks };
}

function statsWith(totals: Partial<StatsData['totals']>): StatsData {
  const s = sanitizeStats({});
  Object.assign(s.totals, totals);
  return s;
}

const three = [human(0), human(1), human(2)];

describe('conquistas novas: disparam e não disparam', () => {
  it('PODIO_DE_EQUIPE: três humanos no 1º, 2º e 3º contra a IA', () => {
    const got = evaluate({ humans: three, positions: { 0: 2, 1: 1, 2: 3 } });
    expect(got.ids).toContain('PODIO_DE_EQUIPE');
    expect(got.unlocks.find((u) => u.id === 'PODIO_DE_EQUIPE')?.seats).toEqual([0, 1, 2]);
    expect(evaluate({ humans: three, positions: { 0: 1, 1: 2, 2: 4 } }).ids).not.toContain('PODIO_DE_EQUIPE');
    expect(evaluate({ humans: three, positions: { 0: 1, 1: 2, 2: 3 }, withAi: false }).ids).not.toContain('PODIO_DE_EQUIPE');
  });

  it('DO_ULTIMO_AO_PRIMEIRO: vencer depois de fechar a primeira volta em último', () => {
    expect(evaluate({ positions: { 0: 1 }, seats: { 0: { lastAfterFirstLap: true } } }).ids).toContain('DO_ULTIMO_AO_PRIMEIRO');
    expect(evaluate({ positions: { 0: 2 }, seats: { 0: { lastAfterFirstLap: true } } }).ids).not.toContain('DO_ULTIMO_AO_PRIMEIRO');
    expect(evaluate({ positions: { 0: 1 } }).ids).not.toContain('DO_ULTIMO_AO_PRIMEIRO');
  });

  it('SEM_ARRANHAO: terminar contra a IA sem colisão nem batida', () => {
    expect(evaluate({ positions: { 0: 6 } }).ids).toContain('SEM_ARRANHAO');
    expect(evaluate({ positions: { 0: 6 }, seats: { 0: { collisions: 1 } } }).ids).not.toContain('SEM_ARRANHAO');
    expect(evaluate({ positions: { 0: 6 }, seats: { 0: { crashes: 1 } } }).ids).not.toContain('SEM_ARRANHAO');
    expect(evaluate({ positions: { 0: 6 }, finished: false }).ids).not.toContain('SEM_ARRANHAO');
    expect(evaluate({ mode: 'timetrial', withAi: false }).ids).not.toContain('SEM_ARRANHAO');
  });

  it('MARATONA: 1.000 km somados no total', () => {
    expect(evaluate({ positions: { 0: 5 }, stats: statsWith({ meters: MARATHON_METERS }) }).ids).toContain('MARATONA');
    expect(evaluate({ positions: { 0: 5 }, stats: statsWith({ meters: MARATHON_METERS - 1 }) }).ids).not.toContain('MARATONA');
  });

  it('MESTRE_DO_VACUO: 60 s de vácuo numa corrida', () => {
    expect(DRAFT_MASTER_TICKS).toBe(60 * TICK_RATE);
    expect(evaluate({ positions: { 0: 5 }, seats: { 0: { draftTicks: DRAFT_MASTER_TICKS } } }).ids).toContain('MESTRE_DO_VACUO');
    expect(evaluate({ positions: { 0: 5 }, seats: { 0: { draftTicks: DRAFT_MASTER_TICKS - 1 } } }).ids).not.toContain('MESTRE_DO_VACUO');
  });

  it('NITRO_NA_BANDEIRA: cruzar a chegada com nitro ligado (vale no contra-relógio também)', () => {
    expect(evaluate({ positions: { 0: 4 }, seats: { 0: { nitroAtFinish: true } } }).ids).toContain('NITRO_NA_BANDEIRA');
    expect(evaluate({ mode: 'timetrial', withAi: false, seats: { 0: { nitroAtFinish: true } } }).ids).toContain('NITRO_NA_BANDEIRA');
    expect(evaluate({ positions: { 0: 4 } }).ids).not.toContain('NITRO_NA_BANDEIRA');
  });

  it('DEZ_VITORIAS: 10 vitórias no total, creditada a quem venceu agora', () => {
    const duo = [human(0), human(1)];
    const got = evaluate({ humans: duo, positions: { 0: 3, 1: 1 }, stats: statsWith({ wins: WINS_TARGET }) });
    expect(got.unlocks.find((u) => u.id === 'DEZ_VITORIAS')?.seats).toEqual([1]);
    expect(evaluate({ positions: { 0: 1 }, stats: statsWith({ wins: WINS_TARGET - 1 }) }).ids).not.toContain('DEZ_VITORIAS');
  });

  it('GIRO_COMPLETO: resultado fora do contra-relógio em todas as pistas de TRACKS (quantas houver)', () => {
    const all = Object.fromEntries(TRACKS.map((d) => [d.id, 5]));
    expect(evaluate({ positions: { 0: 5 }, stats: statsWith({ bestPositions: all }) }).ids).toContain('GIRO_COMPLETO');
    const missing = { ...all };
    delete missing[TRACKS[TRACKS.length - 1].id];
    expect(evaluate({ positions: { 0: 5 }, stats: statsWith({ bestPositions: missing }) }).ids).not.toContain('GIRO_COMPLETO');
  });

  it('já desbloqueada não volta, e cada conquista sai uma vez com todos os assentos que a ganharam', () => {
    const got = evaluate({ humans: three, positions: { 0: 1, 1: 2, 2: 3 }, stats: statsWith({ meters: MARATHON_METERS }) });
    expect(got.unlocks.find((u) => u.id === 'SEM_ARRANHAO')?.seats).toEqual([0, 1, 2]);
    expect(new Set(got.ids).size).toBe(got.ids.length);
    const again = evaluate({ humans: three, positions: { 0: 1, 1: 2, 2: 3 }, stats: statsWith({ meters: MARATHON_METERS }), achievements: got.ids });
    expect(again.ids).toEqual([]);
  });
});

describe('mensagens de conquista no HUD', () => {
  it('uma por assento: uma conquista pelo nome; várias numa linha com o excedente contado', () => {
    setLanguage('pt');
    const msgs = achievementMessages([
      { id: 'PRIMEIRA_VITORIA', seats: [0] },
      { id: 'SEM_ARRANHAO', seats: [1] }, { id: 'MARATONA', seats: [1] }, { id: 'MESTRE_DO_VACUO', seats: [1] },
    ]);
    expect(msgs.get(0)).toBe('CONQUISTA: Primeira vitória');
    expect(msgs.get(1)).toBe('CONQUISTAS: Sem um arranhão · Maratona +1');
    expect(msgs.has(2)).toBe(false);
    setLanguage('en');
    expect(achievementMessages([{ id: 'MARATONA', seats: [3] }]).get(3)).toBe('ACHIEVEMENT: Marathon');
    setLanguage('pt');
  });
});

// ───────────────────────────── Save ─────────────────────────────

describe('estatísticas no save', () => {
  it('lixo vira o padrão, e o padrão é congelado (acumular exige um save saneado)', () => {
    for (const raw of [null, undefined, 3, 'x', [], {}, { totals: 'x', players: 'y' }]) expect(sanitizeStats(raw)).toEqual(EMPTY_STATS);
    expect(sanitizeSave({ stats: 42 }).stats).toEqual(EMPTY_STATS);
    expect(Object.isFrozen(EMPTY_STATS.totals)).toBe(true);
    expect(() => recordRaceStats(EMPTY_STATS, { mode: 'quick', state: quickRace().state, results: [row(0, 1, 9000, 3000)], humans: solo, telemetry: newTelemetry() })).toThrow();
  });

  it('stats corrompidos: contadores negativos, texto, NaN e posições fora da faixa são descartados', () => {
    const s = sanitizeSave({
      stats: {
        totals: { races: -3, wins: 'muitas', laps: 12.6, meters: Number.NaN, nitros: Infinity, bestPositions: { copacabana: 3, rota_66: 0, canion: 25, x: 'a', ['p'.repeat(80)]: 2 } },
        players: [
          { name: 'Ana', races: 2, bestPositions: [1, 2] },
          { name: ' ana ', races: 9 },
          { name: '' }, 42, null, { name: 7 }, 'Bia',
          { name: 'Um nome comprido demais', towsGiven: 4 },
        ],
      },
    }).stats;
    expect(s.totals.races).toBe(0);
    expect(s.totals.wins).toBe(0);
    expect(s.totals.laps).toBe(13);
    expect(s.totals.meters).toBe(0);
    expect(s.totals.nitros).toBe(0);
    expect(s.totals.bestPositions).toEqual({ copacabana: 3 });
    // " ana " repete "Ana" (fica o primeiro, o mais recente); nome vira o do limite do lobby.
    expect(s.players.map((p) => p.name)).toEqual(['Ana', 'Um nome comp']);
    expect(s.players[0].races).toBe(2);
    expect(s.players[0].bestPositions).toEqual({});
    expect(s.players[1].towsGiven).toBe(4);
  });

  it('o limite de nome do perfil é o mesmo do lobby', () => {
    expect(PROFILE_NAME_MAX).toBe(NAME_MAX_LENGTH);
  });
});

describe('limite de perfis', () => {
  function oneRace(stats: StatsData, name: string): void {
    const who: HumanEntry[] = [{ seat: 0, name, carId: 'falcao', teamId: 0, color: '#fff' }];
    const { state } = quickRace({ humans: who });
    recordRaceStats(stats, { mode: 'quick', state, results: [row(0, 2, 9000, 3000)], humans: who, telemetry: newTelemetry() });
  }

  it(`guarda os ${MAX_PROFILES} nomes mais recentes; o total conta todos`, () => {
    const stats = sanitizeStats({});
    for (let i = 1; i <= 40; i++) oneRace(stats, `J${i}`);
    expect(stats.players).toHaveLength(MAX_PROFILES);
    expect(stats.players[0].name).toBe('J40');
    expect(stats.players[MAX_PROFILES - 1].name).toBe('J9');
    expect(stats.totals.races).toBe(40);
    // Um nome antigo que volta a correr sobe para o topo; o mais antigo sai.
    oneRace(stats, 'J9');
    expect(stats.players[0]).toMatchObject({ name: 'J9', races: 2 });
    expect(stats.players).toHaveLength(MAX_PROFILES);
    oneRace(stats, 'J1');
    expect(stats.players[0]).toMatchObject({ name: 'J1', races: 1 });
    expect(stats.players.some((p) => p.name === 'J10')).toBe(false);
  });

  it('o saneamento também corta no limite', () => {
    const players = Array.from({ length: 50 }, (_, i) => ({ name: `N${i}`, races: 1 }));
    expect(sanitizeStats({ players }).players).toHaveLength(MAX_PROFILES);
  });

  it('maiúsculas e espaços não criam perfil novo; a grafia mais recente fica', () => {
    const stats = sanitizeStats({});
    oneRace(stats, 'ana');
    oneRace(stats, '  ANA ');
    expect(stats.players).toHaveLength(1);
    expect(stats.players[0]).toMatchObject({ name: 'ANA', races: 2 });
  });
});

describe('formatação', () => {
  it('distância em km com o separador do idioma e tempo acumulado', () => {
    expect(formatDistance(1234, 'pt')).toBe('1,2 km');
    expect(formatDistance(1_234_567, 'pt')).toBe('1.235 km');
    expect(formatDistance(1_234_567, 'en')).toBe('1,235 km');
    expect(formatDuration(42 * TICK_RATE)).toBe('0:42');
    expect(formatDuration((12 * 60 + 5) * TICK_RATE)).toBe('12:05');
    expect(formatDuration((3 * 3600 + 7 * 60 + 45) * TICK_RATE + 30)).toBe('3:07:45');
  });
  it('a escala é a do velocímetro: 6000 u/s por uma hora = 300 km', () => {
    expect(Math.round(6000 * 3600 * METERS_PER_UNIT)).toBe(300_000);
  });
});
