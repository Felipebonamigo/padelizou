// Fantasma do contra-relógio (src/game/ghost.ts, ghost-store.ts, ghost-session.ts): codificação com erro
// limitado pela quantização, tamanho por volta, interpolação por tempo, descarte por limite, dado
// corrompido ignorado sem lançar e diferença ao vivo numa volta simulada.
import { describe, expect, it } from 'vitest';
import { NO_UPGRADES } from '../src/core/career';
import { TICK_RATE } from '../src/core/constants';
import { stepRace } from '../src/core/sim/race';
import { getTrack, TRACKS } from '../src/core/track';
import { NEUTRAL_INPUT, type PlayerInput, type RaceState, type Track } from '../src/core/types';
import {
  checkGhost, createGhostRun, decodeTrace, encodeTrace, formatGhostDelta, ghostFileText, ghostPoseAt, ghostTicksAtZ, makeGhostRecord,
  parseGhostFile, sanitizeGhostRecord, traceEnd, GHOST_FILE_FORMAT, GHOST_SAMPLE_TICKS, SPEED_STEP, X_STEP, Z_STEP,
  type GhostLapEvent, type GhostRecord, type GhostSample, type GhostTrace,
} from '../src/game/ghost';
import {
  emptyGhostStore, ghostFor, ghostStoreSize, judgeGhostFile, loadGhostStore, pruneGhostStore, putGhost, sanitizeGhostStore, saveGhostStore,
  GHOST_STORE_KEY,
  type GhostStore,
} from '../src/game/ghost-store';
import { lapMessage, startGhost } from '../src/game/ghost-session';
import { lapFingerprint } from '../src/game/content-version';
import { sanitizeSettings } from '../src/game/settings';
import { setLanguage } from '../src/i18n';
import { human, quickRace, syntheticTrack } from './helpers';

/** Gerador determinístico simples (LCG) para as amostras sintéticas. */
function lcg(seed: number): () => number {
  let s = seed >>> 0;
  return () => { s = (s * 1664525 + 1013904223) >>> 0; return s / 0x100000000; };
}

/** Volta sintética: z crescente com aceleração variável, x/velocidade ruidosos, volante e nitro trocando. */
function syntheticTrace(n: number, seed = 7): GhostTrace {
  const rnd = lcg(seed);
  const samples: GhostSample[] = [];
  let z = 37.3; let v = 4000; let x = 0;
  for (let i = 0; i < n; i++) {
    v = Math.max(0, Math.min(8200, v + (rnd() - 0.4) * 90));
    z += v * GHOST_SAMPLE_TICKS / TICK_RATE;
    x = Math.max(-1.8, Math.min(1.8, x + (rnd() - 0.5) * 0.06));
    samples.push({ z, x, speed: v, steer: Math.floor(rnd() * 3) - 1, nitro: rnd() < 0.2 });
  }
  return { sampleTicks: GHOST_SAMPLE_TICKS, t0: 1, samples };
}

type Driver = (state: RaceState) => PlayerInput;
const aiDriver: Driver = () => ({ ...NEUTRAL_INPUT, takeover: true });
/** Pior caso para o tamanho: volante batendo de um lado para o outro (vai para a grama e a volta fica longa). */
const zigzagDriver: Driver = (s) => ({ ...NEUTRAL_INPUT, throttle: true, steer: (Math.floor(s.tick / 7) % 3) - 1, nitro: s.tick % 400 === 0 });

/** Contra-relógio de 1 carro até acabar; devolve os eventos do fantasma e o estado a cada tick (via `each`). */
function runTimeTrial(
  track: Track, laps: number, driver: Driver, best: GhostRecord | null,
  each?: (s: RaceState, ghostRunFrame: ReturnType<ReturnType<typeof createGhostRun>['frame']>) => void, carId = 'falcao',
) {
  const humans = [human(0, 0, carId)];
  const { state } = quickRace({ track, humans, timeTrial: true, totalCars: 1, laps });
  const run = createGhostRun(track.def.id, humans, best, () => '2026-09-28T00:00:00.000Z');
  const events: GhostLapEvent[] = [];
  for (let n = 0; state.phase !== 'finished' && n < 60000; n++) {
    stepRace(state, track, [driver(state)]);
    events.push(...run.afterTick(state));
    each?.(state, run.frame(state));
  }
  return { state, events, run };
}

describe('fantasma: codificação', () => {
  it('ida e volta com erro máximo de meio passo de quantização (e bandeiras exatas)', () => {
    const trace = syntheticTrace(3000);
    const decoded = decodeTrace(encodeTrace(trace));
    expect(decoded).not.toBeNull();
    if (!decoded) return;
    expect(decoded.sampleTicks).toBe(trace.sampleTicks);
    expect(decoded.t0).toBe(trace.t0);
    expect(decoded.samples.length).toBe(trace.samples.length);
    let ez = 0; let ex = 0; let ev = 0;
    trace.samples.forEach((s, i) => {
      const d = decoded.samples[i];
      ez = Math.max(ez, Math.abs(d.z - s.z)); ex = Math.max(ex, Math.abs(d.x - s.x)); ev = Math.max(ev, Math.abs(d.speed - s.speed));
      expect(d.steer).toBe(s.steer);
      expect(d.nitro).toBe(s.nitro);
    });
    expect(ez).toBeLessThanOrEqual(Z_STEP / 2 + 1e-9);
    expect(ex).toBeLessThanOrEqual(X_STEP / 2 + 1e-9);
    expect(ev).toBeLessThanOrEqual(SPEED_STEP / 2 + 1e-9);
  });

  it('string compacta: só caracteres base64url, e reta a velocidade constante vira quase nada', () => {
    const s = encodeTrace(syntheticTrace(500));
    expect(s).toMatch(/^[A-Za-z0-9_-]+$/);
    const flat: GhostSample[] = Array.from({ length: 2000 }, (_, i) => ({ z: i * 300, x: 0.25, speed: 6000, steer: 0, nitro: false }));
    expect(encodeTrace({ sampleTicks: 3, t0: 1, samples: flat }).length).toBeLessThan(40);
  });

  it('volta de verdade em todas as pistas fica abaixo de 20 KB — IA e o pior caso (ziguezague na grama)', () => {
    for (const def of TRACKS) {
      const track = getTrack(def.id);
      for (const driver of [aiDriver, zigzagDriver]) {
        const { events } = runTimeTrial(track, 1, driver, null);
        expect(events.length, def.id).toBe(1);
        const rec = events[0].newBest;
        expect(rec, def.id).not.toBeNull();
        if (!rec) continue;
        expect(new TextEncoder().encode(JSON.stringify(rec)).length, def.id).toBeLessThan(20 * 1024);
        expect(sanitizeGhostRecord(rec), def.id).toEqual(rec);
        // A volta de verdade cabe na própria pista e leva a impressão desta versão (sem falso "outra pista").
        expect(checkGhost(rec), def.id).toBe('ok');
      }
    }
  }, 30_000); // ~2,5 s sozinho; passava dos 5 s padrão com a máquina carregada

  it('dado corrompido é ignorado sem lançar', () => {
    const good = encodeTrace(syntheticTrace(400));
    const bad: unknown[] = [
      undefined, null, 42, {}, [], '', '!!!!', 'A', good.slice(0, good.length - 3), good + 'A', good.replace(/.$/, '*'),
      'BAAA' + good.slice(4), good.slice(1), 'x'.repeat(100_000),
    ];
    // Um caractere trocado em várias posições: ou decodifica em outra volta, ou devolve null — nunca lança.
    for (let i = 0; i < good.length; i += 17) bad.push(good.slice(0, i) + (good[i] === 'A' ? 'B' : 'A') + good.slice(i + 1));
    for (const b of bad) {
      expect(() => decodeTrace(b)).not.toThrow();
      expect(() => sanitizeGhostRecord({ trackId: 'copacabana', ticks: 1200, name: 'P1', carId: 'falcao', date: '', data: b })).not.toThrow();
      expect(() => parseGhostFile(b)).not.toThrow();
    }
    expect(decodeTrace(good.slice(0, good.length - 3))).toBeNull();
    expect(decodeTrace(good + 'A')).toBeNull();
    expect(decodeTrace('!!!!')).toBeNull();
    expect(parseGhostFile('{"format":"outro"}')).toBeNull();
    expect(parseGhostFile('não é json')).toBeNull();
    expect(parseGhostFile('x'.repeat(200_000))).toBeNull();
  });

  it('registro e arquivo: o tempo declarado tem que bater com a volta; o arquivo exportado volta igual', () => {
    const trace = syntheticTrace(400);
    const ticks = traceEnd(trace) + 1;
    const rec = makeGhostRecord('copacabana', ticks, trace, { name: 'Ana', carId: 'falcao' }, '2026-09-28');
    expect(rec).not.toBeNull();
    if (!rec) return;
    expect(sanitizeGhostRecord(rec)).toEqual(rec);
    expect(sanitizeGhostRecord({ ...rec, ticks: ticks + 50 })).toBeNull();
    expect(sanitizeGhostRecord({ ...rec, trackId: '../x' })).toBeNull();
    expect(sanitizeGhostRecord({ ...rec, name: '   ' })).toBeNull();
    expect(parseGhostFile(ghostFileText(rec))).toEqual(rec);
  });
});

describe('fantasma: reprodução', () => {
  it('interpola pelo tempo da volta; antes da 1ª amostra fica nela; depois da última some', () => {
    const samples: GhostSample[] = [
      { z: 100, x: 0, speed: 3000, steer: -1, nitro: false },
      { z: 400, x: 0.3, speed: 3600, steer: 1, nitro: true },
      { z: 1000, x: -0.3, speed: 6000, steer: 0, nitro: false },
    ];
    const trace: GhostTrace = { sampleTicks: 3, t0: 1, samples };
    expect(ghostPoseAt(trace, 0)).toMatchObject({ z: 100, steerPose: -1 });
    expect(ghostPoseAt(trace, 1)).toMatchObject({ z: 100, x: 0 });
    const mid = ghostPoseAt(trace, 2);
    expect(mid?.z).toBeCloseTo(200, 9);
    expect(mid?.x).toBeCloseTo(0.1, 9);
    expect(mid?.speed).toBeCloseTo(3200, 9);
    expect(mid?.steerPose).toBe(-1);
    const late = ghostPoseAt(trace, 3.5);
    expect(late?.z).toBeCloseTo(350, 9);
    expect(late?.steerPose).toBe(1);
    expect(late?.nitro).toBe(true);
    expect(ghostPoseAt(trace, 7)?.z).toBe(1000);
    // Até um passo depois da última amostra, segue pela velocidade dela; depois, a volta dele acabou.
    expect(ghostPoseAt(trace, 9)?.z).toBeCloseTo(1000 + 2 * 6000 / TICK_RATE, 9);
    expect(ghostPoseAt(trace, 11)).toBeNull();
    // O inverso: em que tick ele passou por z.
    expect(ghostTicksAtZ(trace, 250)).toBeCloseTo(2.5, 9);
    expect(ghostTicksAtZ(trace, 700)).toBeCloseTo(5.5, 9);
    expect(ghostTicksAtZ(trace, 1100)).toBeCloseTo(7 + 100 / (6000 / TICK_RATE), 9);
    expect(ghostTicksAtZ(trace, 50)).toBeCloseTo(1 - 50 / (3000 / TICK_RATE), 9);
  });

  it('formata a diferença com sinal e o separador do idioma', () => {
    expect(formatGhostDelta(0.42)).toBe('+0,42');
    expect(formatGhostDelta(-0.15)).toBe('−0,15');
    expect(formatGhostDelta(-0.004)).toBe('+0,00');
    expect(formatGhostDelta(-12.345, '.')).toBe('−12.35');
    expect(formatGhostDelta(65.5)).toBe('+65,50');
  });

  it('diferença ao vivo correta numa volta simulada, e a do fechamento é a diferença exata dos tempos', () => {
    // Reta longa, acelerador no fundo (a IA assumiria o carro e ignoraria o freio da segunda corrida).
    const track = syntheticTrack([{ op: 'straight', length: 900 }], 'reta');
    const throttle: Driver = () => ({ ...NEUTRAL_INPUT, throttle: true });
    // Volta de referência: uma volta só. Guarda a posição crua por tick (a verdade para comparar).
    const truth: number[] = [];
    const first = runTimeTrial(track, 1, throttle, null, (s) => {
      const car = s.cars[0];
      if (car.lap === 1 && !car.finished) truth[s.tick - car.lapStartTick] = car.z;
    });
    const ghost = first.events[0].newBest;
    expect(ghost).not.toBeNull();
    if (!ghost) return;
    const ghostTicks = first.events[0].lapTicks;
    expect(ghost.ticks).toBe(ghostTicks);
    const truthAtZ = (z: number): number => {
      for (let e = 2; e < truth.length; e++) if (truth[e] !== undefined && truth[e - 1] !== undefined && truth[e] >= z && truth[e - 1] < z) {
        return e - 1 + (z - truth[e - 1]) / (truth[e] - truth[e - 1]);
      }
      return Number.NaN;
    };
    // Segunda corrida: a mesma pilotagem, mas freando um segundo no meio da volta — perde tempo ali e não recupera.
    let worst = 0; let before = 0; let after = 0; let samples = 0;
    const brakeFrom = 500; const brakeTo = brakeFrom + TICK_RATE;
    const second = runTimeTrial(track, 1, (s) => {
      const car = s.cars[0];
      const e = s.tick - car.lapStartTick;
      return car.lap === 1 && e >= brakeFrom && e < brakeTo ? { ...NEUTRAL_INPUT, brake: true } : throttle(s);
    }, ghost, (s, frame) => {
      const car = s.cars[0];
      if (car.lap !== 1 || car.finished || !frame || frame.delta === null) return;
      const e = s.tick - car.lapStartTick;
      const expected = (e - truthAtZ(car.z)) / TICK_RATE;
      if (Number.isNaN(expected)) return;
      worst = Math.max(worst, Math.abs(frame.delta - expected));
      samples++;
      if (e === brakeFrom - 10) before = frame.delta;
      if (e === brakeTo + 300) after = frame.delta;
      // A pose do fantasma é a da volta gravada naquele tempo.
      if (frame.pose && truth[e] !== undefined) expect(Math.abs(frame.pose.z - truth[e])).toBeLessThan(3);
    });
    expect(samples).toBeGreaterThan(1000);
    expect(worst).toBeLessThan(0.5 / TICK_RATE); // meio tick
    expect(Math.abs(before)).toBeLessThan(0.02); // mesma pilotagem até ali
    expect(after).toBeGreaterThan(0.3); // perdeu tempo freando
    const lap = second.events[0];
    expect(lap.deltaTicks).toBe(lap.lapTicks - ghostTicks);
    expect(lap.deltaTicks).toBeGreaterThan(0);
    expect(lap.newBest).toBeNull(); // mais lenta: o fantasma continua o mesmo
    expect(second.run.current()).toEqual(ghost);
  });

  it('volta mais rápida vira o fantasma na hora; voltas depois da bandeirada não contam', () => {
    const track = getTrack('rota_66');
    const { events, run } = runTimeTrial(track, 3, aiDriver, null);
    expect(events.map((e) => e.lapTicks).length).toBe(3);
    expect(events[0].deltaTicks).toBeNull();
    expect(events[0].newBest).not.toBeNull();
    // A volta 2 sai lançada: mais rápida que a 1 (que larga do grid) — vira o fantasma.
    expect(events[1].deltaTicks).toBe(events[1].lapTicks - events[0].lapTicks);
    expect(events[1].newBest?.ticks).toBe(events[1].lapTicks);
    const best = Math.min(...events.map((e) => e.lapTicks));
    expect(run.current()?.ticks).toBe(best);
  });

  it('fantasma de outra pista ou corrompido é ignorado; carro de carreira com melhorias não grava', () => {
    const track = getTrack('rota_66');
    const trace = syntheticTrace(300);
    const other = makeGhostRecord('copacabana', traceEnd(trace), trace, { name: 'X', carId: 'falcao' }, '');
    expect(createGhostRun('rota_66', [human(0)], other, () => '').current()).toBeNull();
    expect(createGhostRun('rota_66', [human(0)], other && { ...other, trackId: 'rota_66', data: 'lixo' }, () => '').current()).toBeNull();
    const upgraded = { ...human(0), upgrades: { ...NO_UPGRADES, engine: 1 } };
    const { state } = quickRace({ track, humans: [upgraded], timeTrial: true, totalCars: 1, laps: 1 });
    const run = createGhostRun('rota_66', [upgraded], null, () => '');
    let n = 0;
    while (state.phase !== 'finished' && n++ < 20000) { stepRace(state, track, [aiDriver(state)]); expect(run.afterTick(state)).toEqual([]); }
  });
});

describe('fantasma: armazenamento', () => {
  function recordFor(trackId: string, n: number): GhostRecord {
    const trace = syntheticTrace(n, trackId.length);
    const rec = makeGhostRecord(trackId, traceEnd(trace), trace, { name: 'P1', carId: 'falcao' }, '2026-09-01');
    if (!rec) throw new Error('registro');
    return rec;
  }

  it('passou do limite: descarta os gravados há mais tempo, até caber', () => {
    const store = emptyGhostStore();
    const ids = ['a1', 'b2', 'c3', 'd4', 'e5'];
    ids.forEach((id, i) => putGhost(store, recordFor(id, 300), `2026-09-0${i + 1}T00:00:00Z`));
    const one = ghostStoreSize(store) / ids.length;
    const removed = pruneGhostStore(store, Math.floor(one * 3.5));
    expect(removed).toEqual(['a1', 'b2']);
    expect(Object.keys(store.ghosts).sort()).toEqual(['c3', 'd4', 'e5']);
    expect(ghostStoreSize(store)).toBeLessThanOrEqual(Math.floor(one * 3.5));
  });

  it('localStorage cheio: descarta o mais antigo e tenta de novo, sem lançar; sem espaço nenhum devolve null', () => {
    const store = emptyGhostStore();
    ['a1', 'b2', 'c3'].forEach((id, i) => putGhost(store, recordFor(id, 300), `2026-09-0${i + 1}`));
    const limit = ghostStoreSize(store) - 100; // não cabe tudo: o primeiro a sair é o a1
    const written: string[] = [];
    const quota = (_key: string, value: unknown) => {
      const json = JSON.stringify(value);
      if (json.length > limit) throw new Error('QuotaExceededError');
      written.push(json);
      return true;
    };
    const saved = saveGhostStore(store, quota);
    expect(saved && Object.keys(saved.ghosts).sort()).toEqual(['b2', 'c3']);
    expect(Object.keys(store.ghosts).length).toBe(3); // a loja recebida não muda
    expect(written.length).toBe(1);
    expect(saveGhostStore(store, () => false)).toBeNull();
    expect(() => saveGhostStore(store, () => { throw new Error('x'); })).not.toThrow();
  });

  it('loja corrompida ou com lixo: só os fantasmas válidos, cada um na própria pista', () => {
    const good = recordFor('copacabana', 200);
    const store = sanitizeGhostStore({
      ghosts: {
        copacabana: { ...good, savedAt: '2026-09-02' },
        rota_66: { ...good, savedAt: 'x' }, // chave diferente da pista do registro
        paris: { ...good, trackId: 'paris', data: 'quebrado' },
        roma: 'lixo',
      },
    });
    expect(Object.keys(store.ghosts)).toEqual(['copacabana']);
    // A volta sintética não cabe em Copacabana: a loja a guarda (estrutura válida), mas ghostFor não a entrega
    // como rival (versão do conteúdo, mais abaixo).
    expect(store.ghosts.copacabana).toEqual({ ...good, savedAt: '2026-09-02' });
    expect(ghostFor(store, 'paris')).toBeNull();
    for (const junk of [null, 'x', 3, [], { ghosts: 'x' }, { ghosts: [] }]) expect(sanitizeGhostStore(junk)).toEqual(emptyGhostStore());
    expect(loadGhostStore(() => { throw new Error('x'); })).toEqual(emptyGhostStore());
    expect(loadGhostStore((key) => (key === GHOST_STORE_KEY ? store : undefined))).toEqual(store);
  });
});

describe('fantasma: sessão', () => {
  it('só no contra-relógio local; grava a volta nova, mostra a diferença e respeita a opção', () => {
    expect(startGhost('quick', false, 'rota_66', [human(0)], { settings: { ghost: true }, hud: () => undefined })).toBeNull();
    expect(startGhost('timetrial', true, 'rota_66', [human(0)], { settings: { ghost: true }, hud: () => undefined })).toBeNull();
    setLanguage('pt');
    let disk: GhostStore = emptyGhostStore();
    const messages: Array<{ seat: number; text: string; kind: string }> = [];
    const settings = { ghost: true };
    const track = getTrack('rota_66');
    const race = (laps: number) => {
      const hooks = startGhost('timetrial', false, 'rota_66', [human(0)], {
        settings, hud: (seat, m) => messages.push({ seat, text: m.text, kind: m.kind }),
        load: () => sanitizeGhostStore(JSON.parse(JSON.stringify(disk))), save: (s) => { disk = s; return s; }, now: () => '2026-09-28',
      });
      if (!hooks) throw new Error('sem ganchos');
      const { state } = quickRace({ track, humans: [human(0)], timeTrial: true, totalCars: 1, laps });
      const frames: Array<ReturnType<typeof hooks.frame>> = [];
      while (state.phase !== 'finished') { stepRace(state, track, [aiDriver(state)]); hooks.afterTick(state); frames.push(hooks.frame(state)); }
      return frames;
    };
    const f1 = race(1);
    expect(f1.every((f) => f === undefined || f.pose === null)).toBe(true); // sem fantasma ainda
    expect(messages).toEqual([{ seat: 0, text: 'FANTASMA GRAVADO', kind: 'good' }]);
    expect(ghostFor(disk, 'rota_66')).not.toBeNull();
    messages.length = 0;
    const f2 = race(2);
    expect(f2.some((f) => f?.pose && f.delta !== null)).toBe(true);
    expect(messages[0].text).toMatch(/^(NOVO FANTASMA [−+]\d+,\d\d|FANTASMA [−+]\d+,\d\d)$/);
    settings.ghost = false;
    expect(race(1).every((f) => f === undefined)).toBe(true);
  });

  it('mensagem do fechamento: verde quando mais rápido, amarela quando mais lento', () => {
    setLanguage('pt');
    expect(lapMessage({ seat: 0, lapTicks: 100, deltaTicks: -9, newBest: null }, true)).toMatchObject({ text: 'FANTASMA −0,15', kind: 'good' });
    expect(lapMessage({ seat: 0, lapTicks: 100, deltaTicks: 25, newBest: null }, true)).toMatchObject({ text: 'FANTASMA +0,42', kind: 'warn' });
    expect(lapMessage({ seat: 0, lapTicks: 100, deltaTicks: 25, newBest: null }, false)).toBeNull();
    setLanguage('en');
    expect(lapMessage({ seat: 0, lapTicks: 100, deltaTicks: 25, newBest: null }, true)?.text).toBe('GHOST +0.42');
    setLanguage('pt');
  });

  it('a opção liga/desliga entra nas opções saneadas (padrão ligada)', () => {
    expect(sanitizeSettings({}).ghost).toBe(true);
    expect(sanitizeSettings({ ghost: false }).ghost).toBe(false);
    expect(sanitizeSettings({ ghost: 'x' }).ghost).toBe(true);
  });
});

describe('fantasma: versão do conteúdo (física, carro e pista)', () => {
  /** Uma volta de verdade (a IA pilotando o Trovão em Copacabana), gravada uma vez para os testes abaixo. */
  let cached: GhostRecord | null = null;
  function realLap(): GhostRecord {
    if (!cached) cached = runTimeTrial(getTrack('copacabana'), 1, aiDriver, null, undefined, 'trovao').events[0]?.newBest ?? null;
    if (!cached) throw new Error('a volta não virou fantasma');
    return cached;
  }
  const withoutFp = (rec: GhostRecord): GhostRecord => {
    const { fp: _fp, ...rest } = rec;
    return rest;
  };

  it('a volta gravada leva a impressão desta versão (da pista e do carro de quem fez) e sai no arquivo v2', () => {
    const rec = realLap();
    expect(rec.fp).toBe(lapFingerprint('copacabana', 'trovao'));
    expect(rec.fp).not.toBe(lapFingerprint('copacabana', 'falcao'));
    expect(checkGhost(rec)).toBe('ok');
    expect(JSON.parse(ghostFileText(rec))).toMatchObject({ format: GHOST_FILE_FORMAT, v: 2, trackId: 'copacabana', fp: rec.fp });
    expect(parseGhostFile(ghostFileText(rec))).toEqual(rec);
    expect(sanitizeGhostRecord(rec)).toEqual(rec);
  });

  it('impressão diferente ou ausente = outra versão; volta que não cabe na pista = outra pista; pista que o jogo não tem = desconhecida', () => {
    const rec = realLap();
    expect(checkGhost({ ...rec, fp: 'deadbeef' })).toBe('otherVersion');
    expect(checkGhost(withoutFp(rec))).toBe('otherVersion'); // loja ou arquivo de antes da impressão: versão desconhecida
    expect(checkGhost({ ...rec, carId: 'carro_que_nao_existe' })).toBe('otherVersion');
    // Rota 66 tem o mesmo comprimento de Copacabana: a troca de pista aparece pela impressão.
    expect(checkGhost({ ...rec, trackId: 'rota_66' })).toBe('otherVersion');
    // O caso da onda C: a volta de Copacabana como se fosse de Mônaco (mais longa) — nem com a impressão de Mônaco ela cabe.
    expect(checkGhost({ ...rec, trackId: 'monaco_noite', fp: lapFingerprint('monaco_noite', 'trovao') ?? '' })).toBe('otherTrack');
    expect(checkGhost({ ...rec, trackId: 'passo_alpino', fp: lapFingerprint('passo_alpino', 'trovao') ?? '' })).toBe('otherTrack'); // mais curta
    expect(checkGhost({ ...rec, trackId: 'pista_que_nao_existe' })).toBe('unknownTrack');
  });

  it('importar: arquivo v1 (sem impressão), de outra versão ou que não cabe na pista dá o motivo e não vira fantasma', () => {
    const rec = realLap();
    // Exatamente o que a versão anterior exportava (v: 1, sem impressão): ainda é lido, e conta como versão desconhecida.
    const v1 = JSON.stringify({ format: GHOST_FILE_FORMAT, v: 1, ...withoutFp(rec) });
    expect(parseGhostFile(v1)).toEqual(withoutFp(rec));
    expect(judgeGhostFile(v1)).toBe('otherVersion');
    expect(judgeGhostFile(ghostFileText({ ...rec, fp: 'deadbeef' }))).toBe('otherVersion');
    expect(judgeGhostFile(ghostFileText({ ...rec, trackId: 'monaco_noite' }))).toBe('otherTrack');
    expect(judgeGhostFile(ghostFileText({ ...rec, trackId: 'pista_nova' }))).toBe('unknownTrack');
    expect(judgeGhostFile(JSON.stringify({ format: GHOST_FILE_FORMAT, v: 2, ...withoutFp(rec) }))).toBe('invalid'); // v2 exige a impressão
    expect(judgeGhostFile(JSON.stringify({ format: GHOST_FILE_FORMAT, v: 3, ...rec }))).toBe('invalid');
    expect(judgeGhostFile('não é json')).toBe('invalid');
    expect(judgeGhostFile(ghostFileText(rec))).toEqual(rec);
  });

  it('loja antiga (sem impressão) ou de outra versão carrega sem lançar; a pista fica sem fantasma, as outras seguem', () => {
    const rec = realLap();
    const rota = runTimeTrial(getTrack('rota_66'), 1, aiDriver, null).events[0]?.newBest;
    if (!rota) throw new Error('rota_66');
    const raw = { ghosts: { copacabana: { ...withoutFp(rec), savedAt: '2026-09-01' }, rota_66: { ...rota, savedAt: '2026-09-02' } } };
    let store: GhostStore = emptyGhostStore();
    expect(() => { store = loadGhostStore((key) => (key === GHOST_STORE_KEY ? raw : undefined)); }).not.toThrow();
    expect(Object.keys(store.ghosts).sort()).toEqual(['copacabana', 'rota_66']);
    expect(ghostFor(store, 'copacabana')).toBeNull();
    expect(ghostFor(store, 'rota_66')).toEqual(rota);
    expect(ghostFor({ ghosts: { copacabana: { ...rec, fp: 'deadbeef', savedAt: '' } } }, 'copacabana')).toBeNull();
    expect(ghostFor({ ghosts: { copacabana: { ...rec, savedAt: '' } } }, 'copacabana')).toEqual(rec);
  });

  it('fantasma de outra impressão não é usado como rival: a pista corre sem ele e a volta nova o substitui', () => {
    setLanguage('pt');
    const stale: GhostRecord = { ...realLap(), fp: 'deadbeef' };
    let disk: GhostStore = { ghosts: { copacabana: { ...stale, savedAt: '2026-09-01' } } };
    const messages: string[] = [];
    const track = getTrack('copacabana');
    const trovao = [human(0, 0, 'trovao')];
    const race = () => {
      const hooks = startGhost('timetrial', false, 'copacabana', trovao, {
        settings: { ghost: true }, hud: (_seat, m) => messages.push(m.text),
        load: () => sanitizeGhostStore(JSON.parse(JSON.stringify(disk))), save: (s) => { disk = s; return s; }, now: () => '2026-09-29',
      });
      if (!hooks) throw new Error('sem ganchos');
      const { state } = quickRace({ track, humans: trovao, timeTrial: true, totalCars: 1, laps: 1 });
      const frames: Array<ReturnType<typeof hooks.frame>> = [];
      while (state.phase !== 'finished') { stepRace(state, track, [aiDriver(state)]); hooks.afterTick(state); frames.push(hooks.frame(state)); }
      return frames;
    };
    const first = race();
    expect(first.every((f) => f === undefined || f.pose === null)).toBe(true); // nenhum carro-fantasma na pista
    expect(messages).toEqual(['FANTASMA GRAVADO']); // sem diferença: não houve rival
    expect(disk.ghosts.copacabana.fp).toBe(lapFingerprint('copacabana', 'trovao'));
    expect(disk.ghosts.copacabana.savedAt).toBe('2026-09-29');
    messages.length = 0;
    const second = race();
    expect(second.some((f) => f?.pose && f.delta !== null)).toBe(true); // o novo vale
    expect(messages[0]).toMatch(/^FANTASMA [−+]\d+,\d\d$/);
  });
});
