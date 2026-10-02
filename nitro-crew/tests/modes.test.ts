import { describe, it, expect } from 'vitest';
import { POINTS_TABLE } from '../src/core/constants';
import {
  compareRows, createTournament, currentHeat, finalistCount, heatHumans, heatSizes, qualifyingHeatCount, recordHeat, setupError,
  tournamentRanking, tournamentStandings, type TournamentRow, type TournamentSetup, type TournamentState,
} from '../src/core/tournament';
import { ESCORT_GOAL_POSITION, ESCORT_PUSH_TOP_CAP, ESCORT_VIP_TOP_FACTOR, FINISH_GRACE_TICKS } from '../src/core/constants';
import { carDef } from '../src/core/data/cars';
import { aiStats } from '../src/core/sim/stats';
import { carIndexOfSeat, escortOutcome, isVip, onlineAllowed, relayPairs, vipCar } from '../src/core/modes';
import { createRace, stepRace } from '../src/core/sim/race';
import { deserializeRace, hashRace, serializeRace } from '../src/core/serialize';
import { getTrack } from '../src/core/track';
import { NEUTRAL_INPUT, type CoreMode, type HumanEntry, type RaceConfig, type RaceResultRow, type RaceState, type Track } from '../src/core/types';
import { raceConfigFrom } from '../src/game/online-session';
import { newTelemetry, observeTick } from '../src/game/achievements';
import { ALL_ASSISTS, NO_ASSISTS, human, humanCar, run, skipCountdown, syntheticTrack } from './helpers';

// ───────────────────────────── Torneio de sofá ─────────────────────────────

const NAMES = ['Ana', 'Bia', 'Caio', 'Duda', 'Edu', 'Fê', 'Gabi', 'Hugo'];

function setup(n: number, controllers = 4, rounds = 2): TournamentSetup {
  return {
    players: NAMES.slice(0, n).map((name) => ({ name, carId: 'falcao' })),
    controllers, rounds, trackIds: ['copacabana', 'rota_66', 'serra_do_mar', 'sampa_noite'], laps: 2,
  };
}

/** Resultado de corrida em que os humanos chegam nas posições dadas (por assento), no meio de 12 carros. */
function heatResults(positions: number[], opts: { dnf?: number[]; ticks?: number[] } = {}): RaceResultRow[] {
  return positions.map((position, seat) => {
    const finished = !(opts.dnf ?? []).includes(seat);
    return {
      carId: 100 + seat, seat, name: `S${seat}`, teamId: seat, carDefId: 'falcao', position, finished,
      totalTicks: finished ? (opts.ticks?.[seat] ?? 10_000 + position) : -1, bestLapTicks: 3000, points: POINTS_TABLE[position - 1] ?? 0,
    };
  });
}

/** Roda a bateria atual com as posições por jogador (índice de inscrição). */
function runHeat(t: TournamentState, byPlayer: (player: number) => number, opts: { dnf?: number[]; ticks?: (player: number) => number } = {}): void {
  const info = currentHeat(t);
  if (!info) throw new Error('sem bateria');
  const positions = info.players.map(byPlayer);
  const ticks = opts.ticks ? info.players.map(opts.ticks) : undefined;
  expect(recordHeat(t, heatResults(positions, { dnf: opts.dnf, ticks }))).toBe(true);
}

describe('torneio: chaves', () => {
  it('baterias do tamanho dos controles, o mais iguais possível', () => {
    expect(heatSizes(8, 4)).toEqual([4, 4]);
    expect(heatSizes(6, 4)).toEqual([3, 3]);
    expect(heatSizes(7, 4)).toEqual([4, 3]);
    expect(heatSizes(5, 4)).toEqual([3, 2]);
    expect(heatSizes(4, 4)).toEqual([4]);
    expect(heatSizes(6, 2)).toEqual([2, 2, 2]);
    expect(heatSizes(3, 1)).toEqual([1, 1, 1]);
  });

  it('6 pessoas e 4 controles: 2 baterias de 3 por rodada, cada pessoa corre uma vez por rodada', () => {
    const t = createTournament(setup(6));
    expect(qualifyingHeatCount(t.setup)).toBe(4);
    for (let round = 0; round < 2; round++) {
      const seen: number[] = [];
      for (let heat = 0; heat < 2; heat++) {
        const info = currentHeat(t);
        expect(info).toMatchObject({ round, heat, heatCount: 2, final: false, trackId: t.setup.trackIds[round] });
        expect(info?.players.length).toBe(3);
        seen.push(...(info?.players ?? []));
        runHeat(t, (p) => p + 1);
      }
      expect([...seen].sort()).toEqual([0, 1, 2, 3, 4, 5]);
    }
    // Classificatória acabou: a final tem os 4 melhores, na pista da final.
    const final = currentHeat(t);
    expect(final).toMatchObject({ final: true, heatCount: 1, trackId: 'sampa_noite' });
    expect(final?.players.length).toBe(4);
  });

  it('8 pessoas: 2 baterias de 4; a segunda rodada junta os líderes (suíço) e a final tem os 4 melhores', () => {
    const t = createTournament(setup(8));
    expect(currentHeat(t)?.players).toEqual([0, 1, 2, 3]);
    runHeat(t, (p) => [5, 6, 7, 8][p]);           // bateria 1: Ana 5º … Duda 8º
    expect(currentHeat(t)?.players).toEqual([4, 5, 6, 7]);
    runHeat(t, (p) => [1, 2, 3, 4][p - 4]);       // bateria 2: Edu 1º … Hugo 4º
    // Rodada 2: pela classificação — os quatro da bateria 2 (mais pontos) correm juntos.
    expect(currentHeat(t)?.players).toEqual([4, 5, 6, 7]);
    runHeat(t, (p) => [4, 3, 2, 1][p - 4]);
    expect(currentHeat(t)?.players).toEqual([0, 1, 2, 3]);
    runHeat(t, (p) => [1, 2, 3, 4][p]);
    const standings = tournamentStandings(t);
    const finalists = standings.slice(0, 4).map((r) => r.player);
    expect(t.finalists).toEqual(finalists);
    expect(currentHeat(t)?.players).toEqual(finalists);
    expect(currentHeat(t)?.final).toBe(true);
  });

  it('os humanos da bateria são cada um por si, com o assento na ordem da chave', () => {
    const t = createTournament(setup(6));
    const info = currentHeat(t);
    if (!info) throw new Error('sem bateria');
    const humans = heatHumans(t, info);
    expect(humans.map((h) => h.seat)).toEqual([0, 1, 2]);
    expect(humans.map((h) => h.name)).toEqual(['Ana', 'Bia', 'Caio']);
    expect(new Set(humans.map((h) => h.teamId)).size).toBe(3);
  });

  it('pontos pela posição na corrida (contra a IA também), acumulados entre as rodadas', () => {
    const t = createTournament(setup(4, 4, 2));
    runHeat(t, (p) => [1, 5, 11, 12][p]);
    runHeat(t, (p) => [2, 5, 11, 12][p]);
    const rows = tournamentStandings(t);
    expect(rows[0]).toMatchObject({ name: 'Ana', points: 20 + 15, heats: 2, best: 1 });
    expect(rows[1]).toMatchObject({ name: 'Bia', points: 16, best: 5 });
  });

  it('a mesma bateria não entra duas vezes', () => {
    const t = createTournament(setup(4, 4, 1));
    runHeat(t, (p) => p + 1);
    // A classificatória acabou; a bateria atual é a final. Registrar a final e depois repetir não muda nada.
    runHeat(t, (p) => p + 1);
    expect(t.done).toBe(true);
    expect(recordHeat(t, heatResults([1, 2, 3, 4]))).toBe(false);
    expect(t.history.length).toBe(2);
  });

  it('configuração inválida é recusada com o motivo', () => {
    expect(setupError(setup(1))).toBe('party.t.err.fewPlayers');
    expect(setupError({ ...setup(8), players: [...setup(8).players, { name: 'Iara', carId: 'falcao' }] })).toBe('party.t.err.manyPlayers');
    expect(setupError({ ...setup(3), players: [{ name: 'Ana', carId: 'falcao' }, { name: ' ana ', carId: 'falcao' }] })).toBe('party.t.err.sameName');
    expect(setupError({ ...setup(3), rounds: 3 })).toBeNull();
    expect(setupError({ ...setup(3), rounds: 3, trackIds: ['a', 'b', 'c'] })).toBe('party.t.err.tracks');
    expect(() => createTournament(setup(1))).toThrow();
  });
});

describe('torneio: desempate', () => {
  const row = (player: number, points: number, best: number, ticks: number): TournamentRow => ({ player, name: `J${player}`, points, heats: 2, best, ticks, finalPosition: 0 });

  it('pontos → melhor colocação → menor tempo somado → ordem de inscrição', () => {
    expect(compareRows(row(1, 30, 3, 100), row(0, 20, 1, 50))).toBeLessThan(0);     // mais pontos vence
    expect(compareRows(row(1, 20, 2, 100), row(0, 20, 3, 50))).toBeLessThan(0);     // empate: melhor colocação
    expect(compareRows(row(1, 20, 2, 90), row(0, 20, 2, 100))).toBeLessThan(0);     // depois: menor tempo
    expect(compareRows(row(1, 20, 2, 100), row(0, 20, 2, -1))).toBeLessThan(0);     // quem não terminou perde
    expect(compareRows(row(0, 20, 2, 100), row(1, 20, 2, 100))).toBeLessThan(0);    // tudo igual: inscrição
  });

  it('dois empatados em pontos no corte da final: passa quem tem a melhor colocação', () => {
    // 5 pessoas, 4 controles, 1 rodada: baterias de 3 e 2. Fora do top 10 ninguém pontua.
    const t = createTournament(setup(5, 4, 1));
    runHeat(t, (p) => [1, 2, 13][p]);          // Ana, Bia, Caio (13º, 0 pt)
    runHeat(t, (p) => [3, 12][p - 3]);         // Duda 3º, Edu 12º (0 pt)
    // Caio e Edu empatam com 0 pontos; Edu foi 12º e Caio 13º: Edu vai à final.
    expect(t.finalists).toEqual([0, 1, 3, 4]);
    expect(currentHeat(t)?.players).toEqual([0, 1, 3, 4]);
  });

  it('empate em pontos e colocação: decide o tempo somado', () => {
    const t = createTournament(setup(2, 1, 1));  // 1 controle: uma pessoa por bateria, sem final
    runHeat(t, () => 4, { ticks: () => 9000 });
    runHeat(t, () => 4, { ticks: () => 8000 });
    expect(finalistCount(t.setup)).toBe(0);
    expect(t.done).toBe(true);
    expect(tournamentRanking(t).map((r) => r.name)).toEqual(['Bia', 'Ana']);
  });
});

describe('torneio: final', () => {
  it('a final decide o pódio entre os finalistas; o resto segue a classificatória', () => {
    const t = createTournament(setup(6, 4, 1));
    runHeat(t, (p) => [1, 2, 3][p]);           // Ana, Bia, Caio
    runHeat(t, (p) => [4, 5, 6][p - 3]);       // Duda, Edu, Fê
    expect(t.finalists).toEqual([0, 1, 2, 3]);
    // Na final, Duda (4ª da classificatória, larga atrás) vence.
    runHeat(t, (p) => ({ 0: 3, 1: 4, 2: 2, 3: 1 } as Record<number, number>)[p]);
    expect(t.done).toBe(true);
    expect(currentHeat(t)).toBeNull();
    expect(tournamentRanking(t).map((r) => r.name)).toEqual(['Duda', 'Caio', 'Ana', 'Bia', 'Edu', 'Fê']);
    // A final não soma pontos na classificatória.
    expect(tournamentStandings(t).find((r) => r.name === 'Duda')?.points).toBe(POINTS_TABLE[3]);
  });

  it('com 2 controles a final tem 2; com 1 controle não há final', () => {
    expect(finalistCount(setup(8, 2))).toBe(2);
    expect(finalistCount(setup(3, 4))).toBe(3);
    expect(finalistCount(setup(8, 1))).toBe(0);
  });
});

describe('torneio: inscrição e "passe o controle" (partes puras da tela)', async () => {
  const { defaultPlayers, draftSetup, previousHolders, tournamentTracks } = await import('../src/ui/screens/party');
  const { CUPS } = await import('../src/core/data/cups');

  it('pistas: uma por rodada na ordem da copa e a última da copa na final', () => {
    // Copa de 4 pistas (Mundial). As de estado têm 3: lá o torneio cabe em até 2 rodadas + final (teste abaixo).
    const cup = CUPS.find((c) => c.trackIds.length === 4)!;
    expect(tournamentTracks(cup, 1)).toEqual([cup.trackIds[0], cup.trackIds[3]]);
    expect(tournamentTracks(cup, 3)).toEqual(cup.trackIds);
    expect(setupError(draftSetup({ players: setup(3).players, rounds: 3, cupId: cup.id }, cup, 2, 3))).toBeNull();
  });

  // Onda G: copa de estado tem 3 pistas. Com 3 rodadas pedidas (o seletor ia até 3 em qualquer copa) o torneio
  // pedia 4 pistas e a inscrição travava em "Esta copa não tem pistas suficientes". O teto de rodadas é o da copa:
  // rodadas + final = número de pistas.
  it('copa de 3 pistas: as rodadas param em 2 (+ final) e a inscrição não trava', async () => {
    const { tournamentMaxRounds } = await import('../src/ui/screens/party');
    const small = CUPS.find((c) => c.trackIds.length === 3)!;
    const big = CUPS.find((c) => c.trackIds.length === 4)!;
    expect(tournamentMaxRounds(small)).toBe(2);
    expect(tournamentMaxRounds(big)).toBe(3);
    const s = draftSetup({ players: setup(3).players, rounds: 3, cupId: small.id }, small, 2, 3);
    expect(s.rounds).toBe(2);
    expect(s.trackIds).toEqual(small.trackIds);
    expect(setupError(s)).toBeNull();
  });

  it('pilotos iniciais: nomes recentes sem repetir, depois "Piloto N"', () => {
    const players = defaultPlayers(3, ['Ana', 'ana', ' Bia '], ['falcao', 'trovao']);
    expect(players.map((p) => p.name)).toEqual(['Ana', 'Bia', 'Piloto 1']);
    expect(players.map((p) => p.carId)).toEqual(['falcao', 'trovao', 'falcao']);
    expect(setupError({ ...setup(3), players })).toBeNull();
  });

  it('quem segurava cada controle na bateria anterior', () => {
    const t = createTournament(setup(4, 2, 1));
    expect(previousHolders(t)).toEqual([-1, -1, -1, -1]);
    runHeat(t, (p) => p + 1);
    expect(previousHolders(t)).toEqual([0, 1, -1, -1]);
  });
});

// ───────────────────────────── Escolta ─────────────────────────────

function modeRace(mode: CoreMode, opts: { humans?: HumanEntry[]; totalCars?: number; track?: Track; seed?: number; assists?: typeof ALL_ASSISTS; laps?: number } = {}): { state: RaceState; track: Track } {
  const track = opts.track ?? getTrack('copacabana');
  const config: RaceConfig = {
    trackId: track.def.id, laps: opts.laps ?? 2, humans: opts.humans ?? [human(0), human(1)], totalCars: opts.totalCars ?? 10,
    difficulty: 'profissional', manualGear: false, assists: opts.assists ?? NO_ASSISTS, seed: opts.seed ?? 42, mode,
  };
  return { state: createRace(config, track), track };
}

function vip(state: RaceState) {
  const v = vipCar(state);
  if (!v) throw new Error('sem VIP');
  return v;
}

describe('escolta', () => {
  it('o VIP é um carro da IA na equipe humana, mais lento, na pole; a equipe logo atrás e a IA no fundo', () => {
    const { state } = modeRace('escort', { totalCars: 10 });
    expect(state.cars.length).toBe(10);
    const v = vip(state);
    expect(v.id).toBe(0);
    expect(v.seat).toBe(-1);
    expect(v.ai).not.toBeNull();
    expect(v.teamId).toBe(0);
    expect(v.name).toBe('VIP');
    expect(v.stats.topSpeed).toBeCloseTo(aiStats(carDef(v.carId), 0).topSpeed * ESCORT_VIP_TOP_FACTOR);
    // Ids = índice; grid: VIP na frente, depois os humanos, depois a IA.
    state.cars.forEach((c, i) => expect(c.id).toBe(i));
    const humans = state.cars.filter((c) => c.seat >= 0);
    const rivals = state.cars.filter((c) => c.seat < 0 && !isVip(state, c));
    expect(rivals.length).toBe(7);
    for (const h of humans) expect(h.z).toBeLessThan(v.z + 1);
    const lastHuman = Math.min(...humans.map((h) => h.z));
    for (const r of rivals) expect(r.z).toBeLessThanOrEqual(lastHuman);
  });

  it('corrida normal não tem VIP nem estado de festa', () => {
    const track = getTrack('copacabana');
    const state = createRace({ trackId: track.def.id, laps: 2, humans: [human(0)], totalCars: 8, difficulty: 'profissional', manualGear: false, assists: NO_ASSISTS, seed: 1 }, track);
    expect(state.party).toBeUndefined();
    expect(vipCar(state)).toBeNull();
  });

  it('o VIP recebe o empurrão da equipe por trás, mesmo andando e sem a assistência', () => {
    const { state, track } = modeRace('escort', { track: syntheticTrack(), totalCars: 3, humans: [human(0)], assists: NO_ASSISTS });
    skipCountdown(state, track);
    const v = vip(state); const a = humanCar(state, 0);
    v.z = 6000; v.x = 0; v.speed = 3000;
    a.z = 5750; a.x = 0.1; a.speed = 5800;
    run(state, track, 1, () => ({ ...NEUTRAL_INPUT, throttle: true }));
    expect(state.events.some((e) => e.type === 'tow' && e.carId === v.id && e.byId === a.id)).toBe(true);
    expect(v.speed).toBeGreaterThan(5000);
    expect(v.speed).toBeLessThanOrEqual(v.stats.topSpeed * ESCORT_PUSH_TOP_CAP + 1e-6);
    expect(state.party?.pushBy).toBe(a.id);
    // O evento sai só no começo do empurrão.
    run(state, track, 1, () => ({ ...NEUTRAL_INPUT, throttle: true }));
    expect(state.events.some((e) => e.type === 'tow')).toBe(false);
  });

  it('longe, fora da faixa ou mais lento não empurra', () => {
    for (const [dz, dx, speed] of [[900, 0, 5800], [250, 0.8, 5800], [250, 0, 2000]]) {
      const { state, track } = modeRace('escort', { track: syntheticTrack(), totalCars: 3, humans: [human(0)] });
      skipCountdown(state, track);
      const v = vip(state); const a = humanCar(state, 0);
      v.z = 6000; v.x = 0; v.speed = 3000;
      a.z = 6000 - dz; a.x = dx; a.speed = speed;
      run(state, track, 1, () => ({ ...NEUTRAL_INPUT, throttle: true }));
      expect(state.events.some((e) => e.type === 'tow'), `dz ${dz} dx ${dx} v ${speed}`).toBe(false);
      expect(v.speed).toBeLessThan(3100);
    }
  });

  it('um rival da IA atrás do VIP não empurra (só a equipe)', () => {
    const { state, track } = modeRace('escort', { track: syntheticTrack(), totalCars: 3, humans: [human(0)] });
    skipCountdown(state, track);
    const v = vip(state); const rival = state.cars.find((c) => c.seat < 0 && c !== v);
    if (!rival) throw new Error('sem rival');
    humanCar(state, 0).z = 1000;
    v.z = 6000; v.x = 0; v.speed = 3000;
    rival.z = 5750; rival.x = 0; rival.speed = 5800;
    run(state, track, 1, idleThrottle);
    expect(state.events.some((e) => e.type === 'tow')).toBe(false);
  });

  it('VIP parado é socorrido pela equipe como um companheiro (assistência "empurrão")', () => {
    const { state, track } = modeRace('escort', { track: syntheticTrack(), totalCars: 2, humans: [human(0)], assists: ALL_ASSISTS });
    skipCountdown(state, track);
    const v = vip(state); const a = humanCar(state, 0);
    v.speed = 0; v.z = 5000; v.x = 0.6;
    a.speed = 5800; a.z = 5300; a.x = 0.1; // já passou: não é empurrão por trás, é o socorro
    run(state, track, 1, idleThrottle);
    expect(state.events.some((e) => e.type === 'tow' && e.carId === v.id)).toBe(true);
    expect(v.speed).toBeGreaterThan(v.stats.topSpeed * 0.5);
  });

  it('vale a posição do VIP: entre os 3 primeiros é vitória', () => {
    const { state } = modeRace('escort');
    const v = vip(state);
    const rows = (vipPos: number): RaceResultRow[] => state.cars.map((c, i) => ({
      carId: c.id, seat: c.seat, name: c.name, teamId: c.teamId, carDefId: c.carId, finished: true, totalTicks: 1000 + i, bestLapTicks: 500, points: 0,
      position: c.id === v.id ? vipPos : c.id < vipPos ? c.id : c.id + 1,
    }));
    state.results = rows(ESCORT_GOAL_POSITION);
    expect(escortOutcome(state)).toEqual({ vipPosition: 3, success: true });
    state.results = rows(ESCORT_GOAL_POSITION + 1);
    expect(escortOutcome(state)).toEqual({ vipPosition: 4, success: false });
  });

  it('a chegada espera o VIP: humanos terminaram, o VIP não — a corrida só acaba na tolerância', () => {
    const { state, track } = modeRace('escort', { track: syntheticTrack([{ op: 'straight', length: 100 }]), totalCars: 6, humans: [human(0)], laps: 1 });
    skipCountdown(state, track);
    const v = vip(state);
    v.ai = { skill: 0, laneX: 0, laneUntil: 0, lookahead: 20, aggression: 0 }; // parado de propósito
    let ticks = 0;
    while (!humanCar(state, 0).finished && ticks < 5000) { run(state, track, 1, idleThrottle); ticks++; }
    expect(humanCar(state, 0).finished).toBe(true);
    run(state, track, 60, idleThrottle);
    expect(state.phase).toBe('racing');
    run(state, track, FINISH_GRACE_TICKS, idleThrottle);
    expect(state.phase).toBe('finished');
    expect(escortOutcome(state)).toEqual({ vipPosition: 6, success: false });
  });

  it('determinismo: duas escoltas com a mesma semente são idênticas, e o estado sobrevive a serializar', () => {
    const steer = (tick: number, seat: number) => (((tick + seat * 53) % 240) < 120 ? 0.3 : -0.3);
    const input = (s: RaceState, seat: number) => ({ ...NEUTRAL_INPUT, throttle: true, steer: steer(s.tick, seat), nitro: s.tick % 700 === 300 + seat });
    const a = modeRace('escort', { seed: 9, totalCars: 12, assists: ALL_ASSISTS });
    const b = modeRace('escort', { seed: 9, totalCars: 12, assists: ALL_ASSISTS });
    run(a.state, a.track, 2500, input); run(b.state, b.track, 2500, input);
    expect(serializeRace(a.state)).toBe(serializeRace(b.state));
    const copy = deserializeRace(serializeRace(a.state));
    expect(copy.party).toEqual(a.state.party);
    run(a.state, a.track, 1500, input); run(copy, a.track, 1500, input);
    expect(hashRace(copy)).toBe(hashRace(a.state));
    expect(serializeRace(copy)).toBe(serializeRace(a.state));
  }, 30_000);

  it('só local: o online nunca monta uma corrida de festa', () => {
    expect(onlineAllowed({ mode: 'escort' })).toBe(false);
    expect(onlineAllowed({ mode: 'relay' })).toBe(false);
    expect(onlineAllowed({})).toBe(true);
    const cfg = raceConfigFrom({ trackId: 'copacabana', laps: 2, seats: [{ seat: 0, name: 'A', car: 'falcao', client: 1 }], totalCars: 8, difficulty: 'amador', manualGear: false, assists: ALL_ASSISTS, seed: 1, versus: false } as unknown as Parameters<typeof raceConfigFrom>[0]);
    expect(cfg.mode).toBeUndefined();
    expect(onlineAllowed(cfg)).toBe(true);
  });
});

describe('escolta na sessão (partes puras)', async () => {
  const { partyEventMessages, partyHudTexts } = await import('../src/game/party-session');
  it('o HUD mostra a posição do VIP e a meta; quem empurra recebe o aviso', () => {
    const { state } = modeRace('escort');
    const v = vip(state);
    v.position = 4;
    expect(partyHudTexts('escort', state, 0)).toEqual(['VIP em 4º · meta: top 3']);
    expect(partyHudTexts('quick', state, 0)).toEqual([]);
    const a = humanCar(state, 1);
    const out = partyEventMessages('escort', state, { type: 'tow', carId: v.id, byId: a.id });
    expect(out.map((m) => m.seat)).toEqual([1]);
    expect(out[0].message.text).toBe('Empurrando o VIP!');
    // Empurrão comum entre humanos não gera o aviso do VIP.
    expect(partyEventMessages('escort', state, { type: 'tow', carId: humanCar(state, 0).id, byId: a.id })).toEqual([]);
  });
});

function idleThrottle() { return { ...NEUTRAL_INPUT, throttle: true }; }
void stepRace;

// ───────────────────────────── Revezamento ─────────────────────────────

const PIT_TRACK = () => syntheticTrack([{ op: 'pit', length: 40 }, { op: 'straight', length: 260 }], 'sintetica-box');

/** Leva o carro da dupla até cruzar a linha fechando a volta 1 (a troca fica liberada). */
function closeFirstLap(state: RaceState, track: Track, carId: number, input: (s: RaceState, seat: number) => ReturnType<typeof idleThrottle> = idleThrottle): void {
  const car = state.cars[carId];
  car.lap = 1; car.z = track.length - 250; car.x = 0; car.speed = 3000;
  for (let i = 0; i < 30 && car.lap < 2; i++) run(state, track, 1, input);
  expect(car.lap).toBe(2);
}

describe('revezamento', () => {
  it('um carro por dupla, com os dois nomes; quem espera vê o carro da dupla', () => {
    const { state } = modeRace('relay', { humans: [human(0), human(1), human(2), human(3)], totalCars: 10 });
    expect(state.cars.length).toBe(10);
    const humans = state.cars.filter((c) => c.seat >= 0);
    expect(humans.map((c) => [c.seat, c.name])).toEqual([[0, 'P1 / P2'], [2, 'P3 / P4']]);
    expect(state.party?.relay.map((r) => r.seats)).toEqual([[0, 1], [2, 3]]);
    expect(carIndexOfSeat(state, 1)).toBe(carIndexOfSeat(state, 0));
    expect(carIndexOfSeat(state, 3)).toBe(carIndexOfSeat(state, 2));
    expect(carIndexOfSeat(state, 1)).not.toBe(carIndexOfSeat(state, 3));
    // Assentos salteados: os dois primeiros ocupados formam a dupla.
    expect(relayPairs([human(3), human(0)]).map((p) => p.map((h) => h.seat))).toEqual([[0, 3]]);
  });

  it('a entrada de quem não controla é ignorada', () => {
    const junk = (s: RaceState, seat: number) => (seat === 1
      ? { ...NEUTRAL_INPUT, steer: 1, brake: true, nitro: s.tick % 7 === 0, gearUp: true }
      : { ...NEUTRAL_INPUT, throttle: true, steer: ((s.tick % 180) < 90 ? 0.3 : -0.3) });
    const clean = (s: RaceState, seat: number) => (seat === 1 ? NEUTRAL_INPUT : junk(s, 0));
    const a = modeRace('relay', { seed: 5 }); const b = modeRace('relay', { seed: 5 });
    const drive = (r: { state: RaceState; track: Track }, fn: typeof junk) => {
      for (let i = 0; i < 900; i++) { const inputs = [fn(r.state, 0), fn(r.state, 1)]; stepRace(r.state, r.track, inputs); }
    };
    drive(a, junk); drive(b, clean);
    expect(serializeRace(a.state)).toBe(serializeRace(b.state));
    expect(a.state.cars.find((c) => c.seat === 0)?.speed).toBeGreaterThan(0);
  });

  it('com a direção assistida completa e mãos fora do volante, a troca pendente leva o carro ao box', () => {
    // O doc da assistência diz que quem usa a completa "só acelera e usa o nitro": sem isso, só havia troca
    // quando coincidia com a parada para abastecer e o parceiro quase não jogava.
    const track = getTrack('copacabana');
    const humans: HumanEntry[] = [0, 1].map((seat) => ({ ...human(seat), assist: 'full' as const }));
    const state = createRace({ trackId: 'copacabana', laps: 4, humans, totalCars: 8, difficulty: 'profissional', manualGear: false,
      assists: ALL_ASSISTS, seed: 9, mode: 'relay' }, track);
    const count: Record<string, number> = {};
    for (let t = 0; t < 60 * 900 && state.phase !== 'finished'; t++) {
      stepRace(state, track, [{ ...NEUTRAL_INPUT, throttle: true }, { ...NEUTRAL_INPUT, throttle: true }]);
      for (const e of state.events) count[e.type] = (count[e.type] ?? 0) + 1;
    }
    expect(state.phase).toBe('finished');
    expect(count.relay_due).toBe(3);
    expect(count.relay_missed ?? 0).toBe(0);
    expect(count.relay_swap).toBe(3);
  });

  it('a entrada no box conta para quem estava ao volante; a troca vem no tick seguinte', () => {
    const { state, track } = modeRace('relay', { track: PIT_TRACK(), totalCars: 2, laps: 3 });
    skipCountdown(state, track);
    const car = humanCar(state, 0);
    closeFirstLap(state, track, car.id);
    car.x = 1.55;
    const tel = newTelemetry();
    let entered = false;
    for (let i = 0; i < 5 && !entered; i++) {
      run(state, track, 1, idleThrottle);
      observeTick(tel, state, track);
      entered = state.events.some((e) => e.type === 'pit_enter' && e.carId === car.id);
    }
    expect(entered).toBe(true);
    // No tick da entrada, quem dirige ainda é o assento 0 (estatística, SEM_BOX e "Entrando no box" são dele).
    expect(car.seat).toBe(0);
    expect(tel.seats.get(0)?.pitStops).toBe(1);
    expect(tel.seats.get(1)?.pitStops ?? 0).toBe(0);
    run(state, track, 1, idleThrottle);
    expect(state.events.some((e) => e.type === 'relay_swap')).toBe(true);
    expect(car.seat).toBe(1);
  });

  it('a troca só acontece passando pelo box: entrou, o controle passa ao parceiro', () => {
    const { state, track } = modeRace('relay', { track: PIT_TRACK(), totalCars: 2, laps: 3 });
    skipCountdown(state, track);
    const car = humanCar(state, 0);
    closeFirstLap(state, track, car.id);
    expect(state.party?.relay[0].due).toBe(true);
    expect(car.seat).toBe(0);
    // Na faixa do box.
    car.x = 1.55;
    let swapped = false;
    for (let i = 0; i < 5 && !swapped; i++) { run(state, track, 1, idleThrottle); swapped = state.events.some((e) => e.type === 'relay_swap'); }
    expect(swapped).toBe(true);
    expect(car.seat).toBe(1);
    expect(state.party?.relay[0]).toMatchObject({ active: 1, due: false, swaps: 1 });
    // Agora quem manda é o assento 1: freio do 1 para o carro; o acelerador do 0 não faz nada.
    const speed = car.speed;
    run(state, track, 30, (_, seat) => (seat === 1 ? { ...NEUTRAL_INPUT, brake: true } : { ...NEUTRAL_INPUT, throttle: true }));
    expect(car.speed).toBeLessThan(speed);
    // Dentro do box de novo, sem volta fechada, não troca de volta.
    run(state, track, 30, idleThrottle);
    expect(car.seat).toBe(1);
  });

  it('quem não entra no box continua: a troca desta volta se perde', () => {
    const { state, track } = modeRace('relay', { track: PIT_TRACK(), totalCars: 2, laps: 3 });
    skipCountdown(state, track);
    const car = humanCar(state, 0);
    closeFirstLap(state, track, car.id);
    let missed = false;
    for (let i = 0; i < 60 * 20 && !missed; i++) { run(state, track, 1, idleThrottle); missed = state.events.some((e) => e.type === 'relay_missed'); }
    expect(missed).toBe(true);
    expect(car.seat).toBe(0);
    expect(state.party?.relay[0]).toMatchObject({ due: false, swaps: 0 });
    // Entrar no box depois de perder a troca não passa o controle.
    car.x = 1.55; car.z = 200;
    run(state, track, 5, idleThrottle);
    expect(car.seat).toBe(0);
  });

  it('antes de fechar a primeira volta o box não troca ninguém', () => {
    const { state, track } = modeRace('relay', { track: PIT_TRACK(), totalCars: 2, laps: 3 });
    skipCountdown(state, track);
    const car = humanCar(state, 0);
    car.lap = 1; car.z = 400; car.x = 1.55; car.speed = 1000;
    run(state, track, 30, idleThrottle);
    expect(car.inPit).toBe(true);
    expect(car.seat).toBe(0);
  });

  it('determinismo: dois revezamentos com a mesma semente são idênticos, com trocas, e o estado sobrevive a serializar', () => {
    // Pilotos que entram no box sempre que a troca está liberada.
    const input = (s: RaceState, seat: number) => {
      const rc = s.party?.relay.find((r) => r.seats.includes(seat));
      const car = rc ? s.cars[rc.carId] : undefined;
      const wantPit = rc?.due === true;
      const target = wantPit ? 1.55 : 0;
      return { ...NEUTRAL_INPUT, throttle: !(wantPit && car && car.speed > 1500), steer: car ? Math.max(-1, Math.min(1, (target - car.x) * 4)) : 0 };
    };
    const opts = { track: PIT_TRACK(), totalCars: 6, laps: 3, seed: 11, humans: [human(0), human(1), human(2), human(3)] };
    const a = modeRace('relay', opts); const b = modeRace('relay', opts);
    run(a.state, a.track, 2400, input); run(b.state, b.track, 2400, input);
    expect(a.state.party?.relay.some((r) => r.swaps > 0)).toBe(true);
    expect(serializeRace(a.state)).toBe(serializeRace(b.state));
    const copy = deserializeRace(serializeRace(a.state));
    run(a.state, a.track, 1200, input); run(copy, a.track, 1200, input);
    expect(serializeRace(copy)).toBe(serializeRace(a.state));
  }, 30_000);
});

describe('revezamento: estatísticas, recordes e HUD', async () => {
  const { newTelemetry } = await import('../src/game/achievements');
  const { stepObserved } = await import('../src/game/raceEnd');
  const { raceContributions } = await import('../src/game/stats');
  const { recordRaceResults } = await import('../src/game/save');
  const { DEFAULT_SAVE } = await import('../src/game/contracts');
  const { partyEventMessages, partyHudTexts } = await import('../src/game/party-session');

  /** Pilotos que entram no box sempre que a troca está liberada. */
  const pitter = (s: RaceState, seat: number) => {
    const rc = s.party?.relay.find((r) => r.seats.includes(seat));
    const car = rc ? s.cars[rc.carId] : undefined;
    const target = rc?.due ? 1.55 : 0;
    return { ...NEUTRAL_INPUT, throttle: !(rc?.due && car && car.speed > 1500), steer: car ? Math.max(-1, Math.min(1, (target - car.x) * 4)) : 0 };
  };

  it('a dupla leva o resultado; voltas e distância ficam com quem largou; sem recorde', () => {
    const humans = [{ ...human(0), name: 'Ana' }, { ...human(1), name: 'Bia' }];
    const r = { ...modeRace('relay', { track: PIT_TRACK(), totalCars: 3, laps: 3, humans }), telemetry: newTelemetry() };
    let guard = 0;
    while (r.state.phase !== 'finished' && guard++ < 60 * 300) {
      const inputs = [pitter(r.state, 0), pitter(r.state, 1)];
      stepObserved(r, inputs);
    }
    expect(r.state.phase).toBe('finished');
    expect(r.state.party?.relay[0].swaps).toBe(2);
    const results = r.state.results ?? [];
    const list = raceContributions({ mode: 'relay', state: r.state, results, humans, telemetry: r.telemetry });
    expect(list.map((c) => c.name)).toEqual(['Ana', 'Bia']);
    const [ana, bia] = list.map((c) => c.stats);
    expect(ana.races).toBe(1); expect(bia.races).toBe(1);
    expect(bia.wins).toBe(ana.wins); expect(bia.podiums).toBe(ana.podiums);
    expect(ana.laps).toBe(3); expect(ana.meters).toBeGreaterThan(0);
    expect(bia.laps).toBe(0); expect(bia.meters).toBe(0); expect(bia.raceTicks).toBe(0);
    // Cada um conta os próprios box: Ana entrou para passar o controle, Bia também.
    expect(ana.pitStops + bia.pitStops).toBeGreaterThanOrEqual(2);
    const save = JSON.parse(JSON.stringify(DEFAULT_SAVE));
    expect(recordRaceResults(save, results, humans, r.track.def.id, 3, 'relay')).toEqual([]);
    expect(save.bestLaps).toEqual({});
    expect(save.racesRun).toBe(1);
  }, 30_000);

  it('HUD: quem espera vê "sua vez na próxima troca"; com a troca liberada, quem dirige é chamado ao box', () => {
    const humans = [{ ...human(0), name: 'Ana' }, { ...human(1), name: 'Bia' }];
    const { state, track } = modeRace('relay', { track: PIT_TRACK(), totalCars: 2, laps: 3, humans });
    skipCountdown(state, track);
    expect(partyHudTexts('relay', state, 1)).toEqual(['Sua vez na próxima troca']);
    expect(partyHudTexts('relay', state, 0)).toEqual([]);
    closeFirstLap(state, track, humanCar(state, 0).id);
    expect(partyHudTexts('relay', state, 0)).toEqual(['Box! Passe o controle para Bia']);
    const swap = partyEventMessages('relay', state, { type: 'relay_swap', carId: 0, fromSeat: 0, toSeat: 1 });
    expect(swap.map((m) => [m.seat, m.message.text])).toEqual([[1, 'SUA VEZ!'], [0, 'Controle com Bia']]);
    const missed = partyEventMessages('relay', state, { type: 'relay_missed', carId: humanCar(state, 0).id });
    expect(missed.map((m) => m.seat)).toEqual([0, 1]);
  });
});

describe('lobby dos modos de festa (regras puras)', async () => {
  const { isPartyMode, lobbyHidesCar, lobbyHidesDriver, partySeatsProblem, versusAllowed } = await import('../src/party/rules');
  it('o que cada modo exige e mostra', () => {
    expect(['tournament', 'escort', 'relay', 'quick'].map((m) => isPartyMode(m as 'quick'))).toEqual([true, true, true, false]);
    expect(partySeatsProblem('relay', 3)).toMatch(/2 ou 4/);
    expect(partySeatsProblem('relay', 1)).not.toBeNull();
    expect(partySeatsProblem('relay', 2)).toBeNull();
    expect(partySeatsProblem('relay', 4)).toBeNull();
    expect(partySeatsProblem('escort', 1)).toBeNull();
    expect(versusAllowed('escort', 4)).toBe(false);
    expect(versusAllowed('tournament', 4)).toBe(false);
    expect(versusAllowed('relay', 2)).toBe(false);
    expect(versusAllowed('relay', 4)).toBe(true);
    expect(versusAllowed('quick', 2)).toBe(true);
    expect(lobbyHidesDriver('tournament')).toBe(true);
    expect([0, 1, 2, 3].map((r) => lobbyHidesCar('relay', r))).toEqual([false, true, false, true]);
    expect(lobbyHidesCar('quick', 1)).toBe(false);
  });
});
