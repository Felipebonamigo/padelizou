import { describe, it, expect } from 'vitest';
import { createAudio } from '../src/audio/audio';
import { ENGINE_MAX_HZ, ENGINE_MIN_HZ, engineFrequency, engineRpm, rpmFraction } from '../src/audio/engine';
import {
  BPM_MAX, MIDI_MAX, midiToFreq, noteToMidi, SONGS, songForScenery, STEPS_PER_PATTERN, stepDuration, stepEvents, validateSong,
  type SongDef,
} from '../src/audio/music';
import { AI_AUDIBLE_EVENTS } from '../src/audio/sfx';
import { GEAR_TOP } from '../src/core/constants';
import { CARS } from '../src/core/data/cars';
import type { SceneryId, SimEvent, TimeOfDay } from '../src/core/types';
import { DEFAULT_SETTINGS, type RenderFrame } from '../src/game/contracts';
import { quickRace, run, skipCountdown } from './helpers';

const ALL_EVENTS: SimEvent[] = [
  { type: 'countdown', value: 3 }, { type: 'go' },
  { type: 'lap', carId: 0, lap: 2, lapTicks: 3000, best: true },
  { type: 'finish', carId: 0, position: 1 }, { type: 'finish', carId: 0, position: 7 },
  { type: 'nitro', carId: 0 }, { type: 'nitro_denied', carId: 0 },
  { type: 'collision', carId: 0, otherId: 1, strength: 0.8 },
  { type: 'crash', carId: 0, sprite: 'tree' }, { type: 'crash', carId: 0, sprite: 'lamp' },
  { type: 'offroad', carId: 0, entering: true }, { type: 'offroad', carId: 0, entering: false },
  { type: 'tow', carId: 0, byId: 1 }, { type: 'pit_enter', carId: 0 }, { type: 'pit_exit', carId: 0 },
  { type: 'fuel_low', carId: 0 }, { type: 'fuel_empty', carId: 0 }, { type: 'gear', carId: 0, gear: 2 },
  { type: 'race_over' },
];

function frameFor(paused = false): RenderFrame {
  const { state, track } = quickRace({ totalCars: 4 });
  skipCountdown(state, track);
  run(state, track, 30);
  const carIndex = state.cars.findIndex((c) => c.seat === 0);
  return {
    state, track, paused, time: 1, coop: false, showHud: true,
    viewports: [{ seat: 0, carIndex, color: '#fff', name: 'P1', messages: [] }],
    options: { quality: 'high', showMinimap: true, screenShake: true },
  };
}

describe('áudio sem AudioContext (Node)', () => {
  it('createAudio funciona e todos os métodos são no-op seguros antes do unlock()', () => {
    expect(typeof window).toBe('undefined');
    const audio = createAudio();
    expect(() => audio.unlock()).not.toThrow();
    expect(() => audio.update(null, 1 / 60)).not.toThrow();
    expect(() => audio.update(frameFor(), 1 / 60)).not.toThrow();
    expect(() => audio.update(frameFor(true), 1 / 60)).not.toThrow();
    for (const e of ALL_EVENTS) {
      expect(() => audio.onEvent(e, 0)).not.toThrow();
      expect(() => audio.onEvent(e, -1)).not.toThrow();
    }
    expect(() => audio.setVolumes(0.5, 0.5, 0.5)).not.toThrow();
    expect(() => audio.ui('move')).not.toThrow();
    expect(() => audio.ui('confirm')).not.toThrow();
    expect(() => audio.ui('back')).not.toThrow();
    expect(() => audio.unlock()).not.toThrow();
  });

  it('guarda a música pedida antes do unlock e traduz auto pela pista', () => {
    const audio = createAudio();
    expect(audio.currentMusic()).toBeNull();
    audio.setMusic('serra_acima');
    expect(audio.currentMusic()).toBe('serra_acima');
    audio.setMusic(null);
    expect(audio.currentMusic()).toBeNull();
    audio.setMusic('auto');
    expect(audio.currentMusic()).toBe(SONGS[0].id);
    audio.update(frameFor(), 1 / 60); // copacabana: costa de dia
    expect(audio.currentMusic()).toBe(songForScenery('coast', 'day'));
    audio.setMusic('id_que_nao_existe');
    expect(SONGS.some((s) => s.id === audio.currentMusic())).toBe(true);
  });

  it('musicList lista as 4 músicas com autor procedural', () => {
    const list = createAudio().musicList();
    expect(list.map((m) => m.id)).toEqual(SONGS.map((s) => s.id));
    expect(list).toHaveLength(4);
    for (const m of list) {
      expect(m.author).toBe('procedural');
      expect(m.title.length).toBeGreaterThan(0);
    }
    expect(DEFAULT_SETTINGS.music).toBe('auto');
  });
});

describe('músicas', () => {
  it('SONGS tem 4 músicas válidas com ids únicos', () => {
    expect(SONGS).toHaveLength(4);
    expect(new Set(SONGS.map((s) => s.id)).size).toBe(4);
    for (const s of SONGS) {
      expect(validateSong(s), s.id).toEqual([]);
      expect(s.sequence.length).toBeGreaterThanOrEqual(4);
      expect(Object.keys(s.patterns).length).toBeGreaterThanOrEqual(4);
      expect(s.bpm).toBeGreaterThanOrEqual(130);
      expect(s.bpm).toBeLessThanOrEqual(150);
    }
  });

  it('validateSong acha cada tipo de problema', () => {
    const base = SONGS[0];
    const pattern = base.patterns[base.sequence[0]];
    const broken: SongDef = {
      id: 'x', title: 'x', author: 'x', bpm: BPM_MAX + 1,
      patterns: { a: { ...pattern, bass: pattern.bass.slice(0, 15), lead: [MIDI_MAX + 1, ...pattern.lead.slice(1)], hat: 'x.x.' } },
      sequence: ['a', 'nao_existe'],
    };
    const problems = validateSong(broken);
    expect(problems.some((p) => p.includes('bpm'))).toBe(true);
    expect(problems.some((p) => p.includes('a.bass') && p.includes('15'))).toBe(true);
    expect(problems.some((p) => p.includes('a.lead[0]'))).toBe(true);
    expect(problems.some((p) => p.includes('a.hat'))).toBe(true);
    expect(problems.some((p) => p.includes('nao_existe'))).toBe(true);
    expect(validateSong({ ...base, sequence: [] })).toContain('sequência vazia');
  });

  it('midiToFreq e noteToMidi', () => {
    expect(midiToFreq(69)).toBe(440);
    expect(midiToFreq(81)).toBeCloseTo(880, 6);
    expect(midiToFreq(57)).toBeCloseTo(220, 6);
    expect(noteToMidi('A4')).toBe(69);
    expect(noteToMidi('C4')).toBe(60);
    expect(noteToMidi('F#2')).toBe(42);
    expect(noteToMidi('Bb3')).toBe(58);
    expect(() => noteToMidi('H2')).toThrow();
  });

  it('songForScenery devolve id existente para todas as combinações e usa as 4 músicas', () => {
    const sceneries: SceneryId[] = ['tropical', 'desert', 'city_night', 'alpine', 'coast', 'savanna'];
    const times: TimeOfDay[] = ['day', 'dusk', 'night'];
    const ids = new Set(SONGS.map((s) => s.id));
    const used = new Set<string>();
    for (const sc of sceneries) for (const tm of times) {
      const id = songForScenery(sc, tm);
      expect(ids.has(id), `${sc}/${tm} → ${id}`).toBe(true);
      used.add(id);
    }
    expect(used.size).toBe(4);
    expect(songForScenery('coast', 'night')).toBe('neon_noturno');
  });

  it('stepEvents percorre a sequência em loop e mede a duração do lead', () => {
    const song = SONGS[0];
    const total = song.sequence.length * STEPS_PER_PATTERN;
    const first = song.patterns[song.sequence[0]];
    expect(stepEvents(song, 0).bass).toBe(first.bass[0]);
    expect(stepEvents(song, 0).kick).toBe(first.kick[0] === 'x');
    expect(stepEvents(song, total)).toEqual(stepEvents(song, 0));
    expect(stepEvents(song, total + 5)).toEqual(stepEvents(song, 5));
    // "vAm": E5 . . . C5 … → a primeira nota segura 4 passos, a segunda 2.
    const vAm = song.sequence.indexOf('vAm') * STEPS_PER_PATTERN;
    expect(stepEvents(song, vAm).leadSteps).toBe(4);
    expect(stepEvents(song, vAm + 4).leadSteps).toBe(2);
    expect(stepEvents(song, vAm + 1).lead).toBe(0);
    expect(stepEvents(song, vAm + 1).leadSteps).toBe(0);
    expect(stepDuration(120)).toBeCloseTo(0.125, 9);
  });
});

describe('motor', () => {
  it('rpmFraction sobe de 0 a 1 dentro de cada marcha e satura', () => {
    const top = CARS[0].topSpeed;
    expect(rpmFraction(0, 0, top)).toBe(0);
    expect(rpmFraction(GEAR_TOP[0] * top, 0, top)).toBe(1);
    expect(rpmFraction(GEAR_TOP[0] * top, 1, top)).toBe(0);
    expect(rpmFraction((GEAR_TOP[0] + GEAR_TOP[1]) / 2 * top, 1, top)).toBeCloseTo(0.5, 9);
    expect(rpmFraction(top, 4, top)).toBe(1);
    expect(rpmFraction(top * 1.28, 4, top)).toBe(1); // nitro acima do teto
    expect(rpmFraction(top, 1, top)).toBe(1);        // reduzir marcha em alta: estoura no talo
    expect(rpmFraction(100, 9, top)).toBeGreaterThanOrEqual(0); // marcha fora da faixa não quebra
    expect(rpmFraction(100, 0, 0)).toBe(0);
  });

  it('engineRpm cai ao trocar de marcha para cima e engineFrequency fica na faixa', () => {
    const top = CARS[0].topSpeed;
    const speed = GEAR_TOP[0] * top * 0.99;
    const before = engineRpm(speed, 0, top);
    const after = engineRpm(speed, 1, top);
    expect(after).toBeLessThan(before);
    expect(after).toBeGreaterThan(0.2);
    for (const rpm of [0, 0.3, 0.7, 1]) {
      const f = engineFrequency(rpm, false);
      expect(f).toBeGreaterThanOrEqual(ENGINE_MIN_HZ);
      expect(f).toBeLessThanOrEqual(ENGINE_MAX_HZ);
      expect(engineFrequency(rpm, true)).toBeGreaterThan(f);
    }
    expect(engineFrequency(0, false)).toBe(ENGINE_MIN_HZ);
    expect(engineFrequency(1, false)).toBe(ENGINE_MAX_HZ);
  });
});

describe('efeitos', () => {
  it('da IA só passam colisão, batida e nitro', () => {
    expect([...AI_AUDIBLE_EVENTS].sort()).toEqual(['collision', 'crash', 'nitro']);
  });
});
