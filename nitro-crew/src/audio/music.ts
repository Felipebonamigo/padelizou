// Jukebox procedural: quatro músicas escritas como dados (padrões de 16 semicolcheias por
// instrumento — baixo, lead, arpejo e bateria) e um sequenciador com agendamento antecipado
// no estilo "A Tale of Two Clocks": um setTimeout a cada 25 ms empurra para o AudioContext
// tudo o que cai nos próximos 100 ms. Não depende de requestAnimationFrame, então a música
// não engasga quando a aba perde o foco.
import type { SceneryId, TimeOfDay } from '../core/types';
import type { MusicInfo } from '../game/contracts';
import { ENV_PLUCK, noiseBurst, playTone } from './synth';

export const STEPS_PER_PATTERN = 16;
export const MIDI_MIN = 24;
export const MIDI_MAX = 96;
export const BPM_MIN = 90;
export const BPM_MAX = 200;
/** Quanto do futuro cada passada do sequenciador agenda, e de quanto em quanto ela roda. */
export const LOOKAHEAD_SECONDS = 0.1;
export const SCHEDULER_INTERVAL_MS = 25;
export const FADE_SECONDS = 0.5;

export interface Pattern {
  /** 16 notas MIDI (0 = pausa). */
  bass: number[];
  lead: number[];
  arp: number[];
  /** 16 caracteres: 'x' toca, '.' cala. */
  kick: string;
  snare: string;
  hat: string;
}

export interface SongDef {
  id: string;
  title: string;
  author: string;
  bpm: number;
  patterns: Record<string, Pattern>;
  /** Nomes de padrões, tocados em ordem e em loop. */
  sequence: string[];
}

export function midiToFreq(n: number): number {
  return 440 * Math.pow(2, (n - 69) / 12);
}

const NOTE_INDEX: Record<string, number> = { C: 0, D: 2, E: 4, F: 5, G: 7, A: 9, B: 11 };

/** "C4" → 60, "F#2" → 42, "Bb3" → 58. */
export function noteToMidi(name: string): number {
  const m = /^([A-Ga-g])([#b]?)(-?\d)$/.exec(name);
  if (!m) throw new Error(`Nota inválida: ${name}`);
  const semi = NOTE_INDEX[m[1].toUpperCase()] + (m[2] === '#' ? 1 : m[2] === 'b' ? -1 : 0);
  return (parseInt(m[3], 10) + 1) * 12 + semi;
}

// ───────────────────────────── Notação compacta ─────────────────────────────

/** Linha melódica: 16 tokens separados por espaço, "." é pausa. */
function line(s: string): number[] {
  return s.trim().split(/\s+/).map((tok) => (tok === '.' ? 0 : noteToMidi(tok)));
}

/** Repete as notas dadas até preencher 16 passos (arpejos). */
function cycle(s: string): number[] {
  const notes = line(s);
  return Array.from({ length: STEPS_PER_PATTERN }, (_, i) => notes[i % notes.length]);
}

/** Baixo "bombado" de synthwave: semicolcheias na fundamental com a oitava nos contratempos. */
function pump(root: string): number[] {
  const r = noteToMidi(root);
  const o = r + 12;
  return [r, r, r, r, r, r, o, r, r, r, r, r, r, r, o, o];
}

/** Baixo em colcheias, com a oitava no fim de cada metade (rock). */
function eighths(root: string): number[] {
  const r = noteToMidi(root);
  const o = r + 12;
  return [r, 0, r, 0, r, 0, o, 0, r, 0, r, 0, r, 0, o, 0];
}

type Drums = readonly [kick: string, snare: string, hat: string];

const D_INTRO: Drums = ['x...x...x...x...', '................', '..x...x...x...x.'];
const D_FOUR: Drums = ['x...x...x...x...', '....x.......x...', 'x.x.x.x.x.x.x.x.'];
const D_FOUR16: Drums = ['x...x...x...x...', '....x.......x...', 'xxxxxxxxxxxxxxxx'];
const D_ROCK: Drums = ['x..x..x.x..x..x.', '....x.......x...', 'x.x.x.x.x.x.x.x.'];
const D_ROCK16: Drums = ['x..x..x.x..x..x.', '....x.......x..x', 'xxxxxxxxxxxxxxxx'];
const D_NEON: Drums = ['x.....x...x.....', '....x.......x...', 'x.x.x.x.x.x.x.x.'];
const D_NEON16: Drums = ['x.....x...x.....', '....x.......x...', 'x.xxx.xxx.xxx.xx'];
const D_BREAK: Drums = ['x.......x.......', '....x..x....x...', '................'];
const D_FILL: Drums = ['x...x...x...x...', '....x.......xxxx', 'x.x.x.x.x.x.x.x.'];

function bar(bass: number[], arp: number[], lead: string | null, drums: Drums): Pattern {
  return {
    bass, arp, lead: lead ? line(lead) : new Array<number>(STEPS_PER_PATTERN).fill(0),
    kick: drums[0], snare: drums[1], hat: drums[2],
  };
}

// ───────────────────────────── As músicas ─────────────────────────────

/** Lá menor, Am–F–C–G: a reta final iluminada, o hino da corrida. */
const RETA_FINAL: SongDef = {
  id: 'reta_final', title: 'Reta Final', author: 'procedural', bpm: 140,
  patterns: {
    introAm: bar(pump('A2'), cycle('A3 C4 E4 A4'), null, D_INTRO),
    introG: bar(pump('G2'), cycle('G3 B3 D4 G4'), null, D_INTRO),
    introF: bar(pump('F2'), cycle('F3 A3 C4 F4'), null, D_INTRO),
    introGfill: bar(pump('G2'), cycle('G3 B3 D4 G4'), '. . . . . . . . . . . . E5 . D5 .', D_FILL),
    vAm: bar(pump('A2'), cycle('A3 C4 E4 A4'), 'E5 . . . C5 . E5 . A4 . . . . . C5 D5', D_FOUR),
    vF: bar(pump('F2'), cycle('F3 A3 C4 F4'), 'E5 . . . . . C5 . A4 . . . . . . .', D_FOUR),
    vC: bar(pump('C3'), cycle('C4 E4 G4 C5'), 'G4 . . . E5 . . . D5 . C5 . . . . .', D_FOUR),
    vG: bar(pump('G2'), cycle('G3 B3 D4 G4'), 'D5 . . . B4 . . . G4 . . . . . B4 .', D_FOUR),
    cAm: bar(pump('A2'), cycle('A3 C4 E4 A4'), 'A5 . G5 . E5 . . . C5 . D5 . E5 . . .', D_FOUR16),
    cF: bar(pump('F2'), cycle('F3 A3 C4 F4'), 'F5 . E5 . C5 . . . A4 . . . C5 . D5 .', D_FOUR16),
    cC: bar(pump('C3'), cycle('C4 E4 G4 C5'), 'E5 . . . G5 . . . E5 . D5 . C5 . . .', D_FOUR16),
    cG: bar(pump('G2'), cycle('G3 B3 D4 G4'), 'D5 . . . B4 . D5 . G5 . . . . . . .', D_FOUR16),
    cGfill: bar(pump('G2'), cycle('G3 B3 D4 G4'), 'D5 . . . B4 . D5 . G5 . . . A5 . B5 .', D_FILL),
  },
  sequence: [
    'introAm', 'introG', 'introF', 'introGfill',
    'vAm', 'vF', 'vC', 'vG', 'vAm', 'vF', 'vC', 'vG',
    'cAm', 'cF', 'cC', 'cG', 'cAm', 'cF', 'cC', 'cGfill',
  ],
};

/** Mi menor, Em–G–D–C, mais lento: luzes de cidade à noite. */
const NEON_NOTURNO: SongDef = {
  id: 'neon_noturno', title: 'Neon Noturno', author: 'procedural', bpm: 130,
  patterns: {
    introEm: bar(eighths('E2'), cycle('E3 G3 B3 E4 B3 G3'), null, D_INTRO),
    introC: bar(eighths('C2'), cycle('C3 E3 G3 C4 G3 E3'), null, D_INTRO),
    vEm: bar(pump('E2'), cycle('E3 G3 B3 E4 B3 G3'), 'B4 . . . E5 . . . D5 . B4 . . . . .', D_NEON),
    vG: bar(pump('G2'), cycle('G3 B3 D4 G4 D4 B3'), 'G4 . . . B4 . . . D5 . . . . . . .', D_NEON),
    vD: bar(pump('D2'), cycle('D3 F#3 A3 D4 A3 F#3'), 'A4 . . . F#4 . . . D5 . . . A4 . . .', D_NEON),
    vC: bar(pump('C2'), cycle('C3 E3 G3 C4 G3 E3'), 'G4 . . . E4 . . . . . G4 . B4 . D5 .', D_NEON),
    cEm: bar(pump('E2'), cycle('E3 G3 B3 E4 B3 G3'), 'E5 . . . D5 . E5 . G5 . . . E5 . D5 .', D_NEON16),
    cG: bar(pump('G2'), cycle('G3 B3 D4 G4 D4 B3'), 'B4 . . . D5 . . . G5 . . . F#5 . D5 .', D_NEON16),
    cD: bar(pump('D2'), cycle('D3 F#3 A3 D4 A3 F#3'), 'A4 . . . D5 . . . F#5 . . . A5 . . .', D_NEON16),
    cC: bar(pump('C2'), cycle('C3 E3 G3 C4 G3 E3'), 'G5 . . . E5 . . . D5 . B4 . . . . .', D_NEON16),
    breakEm: bar(eighths('E2'), cycle('E3 G3 B3 E4 B3 G3'), 'E5 . . . . . . . . . . . . . . .', D_BREAK),
    breakC: bar(eighths('C2'), cycle('C3 E3 G3 C4 G3 E3'), '. . . . . . . . G4 . B4 . D5 . E5 .', D_BREAK),
  },
  sequence: [
    'introEm', 'introC', 'introEm', 'introC',
    'vEm', 'vG', 'vD', 'vC', 'vEm', 'vG', 'vD', 'vC',
    'cEm', 'cG', 'cD', 'cC', 'cEm', 'cG', 'cD', 'cC',
    'breakEm', 'breakC',
  ],
};

/** Ré menor, Dm–Bb–F–C, rock rápido: subindo a serra de pé no acelerador. */
const SERRA_ACIMA: SongDef = {
  id: 'serra_acima', title: 'Serra Acima', author: 'procedural', bpm: 150,
  patterns: {
    introDm: bar(eighths('D2'), cycle('D3 D3 A3 D3'), null, D_INTRO),
    introBb: bar(eighths('Bb1'), cycle('Bb2 Bb2 F3 Bb2'), null, D_INTRO),
    introFill: bar(eighths('C2'), cycle('C3 C3 G3 C3'), '. . . . . . . . A4 . C5 . D5 . F5 .', D_FILL),
    vDm: bar(eighths('D2'), cycle('D3 D3 A3 D3'), 'D5 . D5 . F5 . D5 . C5 . A4 . . . C5 .', D_ROCK),
    vBb: bar(eighths('Bb1'), cycle('Bb2 Bb2 F3 Bb2'), 'D5 . D5 . F5 . D5 . Bb4 . . . . . . .', D_ROCK),
    vF: bar(eighths('F2'), cycle('F3 F3 C4 F3'), 'A4 . A4 . C5 . A4 . F5 . . . E5 . C5 .', D_ROCK),
    vC: bar(eighths('C2'), cycle('C3 C3 G3 C3'), 'G4 . G4 . Bb4 . G4 . E5 . . . . . . .', D_ROCK),
    cDm: bar(pump('D2'), cycle('D3 F3 A3 D4'), 'F5 . . . D5 . F5 . A5 . . . G5 . F5 .', D_ROCK16),
    cBb: bar(pump('Bb1'), cycle('Bb2 D3 F3 Bb3'), 'D5 . . . F5 . . . Bb4 . . . D5 . F5 .', D_ROCK16),
    cF: bar(pump('F2'), cycle('F3 A3 C4 F4'), 'C5 . . . A4 . C5 . F5 . . . E5 . C5 .', D_ROCK16),
    cC: bar(pump('C2'), cycle('C3 E3 G3 C4'), 'G5 . . . E5 . . . C5 . . . D5 . E5 .', D_ROCK16),
  },
  sequence: [
    'introDm', 'introBb', 'introDm', 'introFill',
    'vDm', 'vBb', 'vF', 'vC', 'vDm', 'vBb', 'vF', 'vC',
    'cDm', 'cBb', 'cF', 'cC', 'cDm', 'cBb', 'cF', 'cC',
  ],
};

/** Dó menor, Cm–Ab–Fm–G: tensão de quem está colado no carro da frente. */
const VACUO: SongDef = {
  id: 'vacuo', title: 'Vácuo', author: 'procedural', bpm: 145,
  patterns: {
    introCm: bar(pump('C2'), cycle('C3 Eb3 G3 C4 G3 Eb3'), null, D_INTRO),
    introAb: bar(pump('Ab1'), cycle('Ab2 C3 Eb3 Ab3 Eb3 C3'), null, D_INTRO),
    introG: bar(pump('G1'), cycle('G2 B2 D3 G3 D3 B2'), '. . . . . . . . . . . . D5 . B4 .', D_FILL),
    vCm: bar(pump('C2'), cycle('C3 Eb3 G3 C4 G3 Eb3'), 'G4 . . . . . Ab4 . G4 . . . Eb4 . . .', D_FOUR),
    vAb: bar(pump('Ab1'), cycle('Ab2 C3 Eb3 Ab3 Eb3 C3'), 'C5 . . . . . Ab4 . G4 . . . . . . .', D_FOUR),
    vFm: bar(pump('F2'), cycle('F3 Ab3 C4 F4 C4 Ab3'), 'F4 . . . Ab4 . . . C5 . . . Ab4 . G4 .', D_FOUR),
    vG: bar(pump('G1'), cycle('G2 B2 D3 G3 D3 B2'), 'D4 . . . . . F4 . G4 . . . B4 . D5 .', D_FOUR),
    cCm: bar(pump('C2'), cycle('C3 Eb3 G3 C4 G3 Eb3'), 'C5 . . . Eb5 . D5 . C5 . . . G4 . Ab4 .', D_FOUR16),
    cAb: bar(pump('Ab1'), cycle('Ab2 C3 Eb3 Ab3 Eb3 C3'), 'Ab4 . . . C5 . . . Eb5 . D5 . C5 . . .', D_FOUR16),
    cFm: bar(pump('F2'), cycle('F3 Ab3 C4 F4 C4 Ab3'), 'F5 . . . Eb5 . C5 . Ab4 . . . C5 . . .', D_FOUR16),
    cG: bar(pump('G1'), cycle('G2 B2 D3 G3 D3 B2'), 'D5 . . . B4 . D5 . G5 . . . F5 . D5 .', D_FOUR16),
    breakCm: bar(pump('C2'), cycle('C3 Eb3 G3 C4 G3 Eb3'), 'C5 . . . . . . . . . . . . . . .', D_BREAK),
    breakG: bar(pump('G1'), cycle('G2 B2 D3 G3 D3 B2'), '. . . . . . . . G4 . B4 . D5 . F5 .', D_BREAK),
  },
  sequence: [
    'introCm', 'introAb', 'introCm', 'introG',
    'vCm', 'vAb', 'vFm', 'vG', 'vCm', 'vAb', 'vFm', 'vG',
    'cCm', 'cAb', 'cFm', 'cG', 'cCm', 'cAb', 'cFm', 'cG',
    'breakCm', 'breakG',
  ],
};

export const SONGS: readonly SongDef[] = Object.freeze([RETA_FINAL, NEON_NOTURNO, SERRA_ACIMA, VACUO]);

export function songById(id: string): SongDef | undefined {
  return SONGS.find((s) => s.id === id);
}

export function musicInfos(): MusicInfo[] {
  return SONGS.map((s) => ({ id: s.id, title: s.title, author: s.author }));
}

/** Lista de problemas da música (vazia = válida). */
export function validateSong(song: SongDef): string[] {
  const problems: string[] = [];
  if (!(song.bpm >= BPM_MIN && song.bpm <= BPM_MAX)) problems.push(`bpm ${song.bpm} fora de ${BPM_MIN}..${BPM_MAX}`);
  if (song.sequence.length === 0) problems.push('sequência vazia');
  for (const [name, p] of Object.entries(song.patterns)) {
    for (const inst of ['bass', 'lead', 'arp'] as const) {
      const notes = p[inst];
      if (notes.length !== STEPS_PER_PATTERN) problems.push(`${name}.${inst}: ${notes.length} passos (esperado ${STEPS_PER_PATTERN})`);
      notes.forEach((n, i) => {
        if (n !== 0 && (n < MIDI_MIN || n > MIDI_MAX || !Number.isInteger(n))) problems.push(`${name}.${inst}[${i}]: nota ${n} fora de ${MIDI_MIN}..${MIDI_MAX}`);
      });
    }
    for (const inst of ['kick', 'snare', 'hat'] as const) {
      const s = p[inst];
      if (s.length !== STEPS_PER_PATTERN) problems.push(`${name}.${inst}: ${s.length} passos (esperado ${STEPS_PER_PATTERN})`);
      if (/[^x.]/.test(s)) problems.push(`${name}.${inst}: só 'x' e '.' são aceitos`);
    }
  }
  song.sequence.forEach((name, i) => {
    if (!(name in song.patterns)) problems.push(`sequence[${i}]: padrão "${name}" não existe`);
  });
  return problems;
}

/** Qual música combina com o cenário: noite é sempre neon; serra e mata sobem; deserto é vácuo. */
export function songForScenery(scenery: SceneryId, timeOfDay: TimeOfDay): string {
  if (timeOfDay === 'night' || scenery === 'city_night') return NEON_NOTURNO.id;
  switch (scenery) {
    case 'alpine':
    case 'tropical':
      return SERRA_ACIMA.id;
    case 'desert':
    case 'savanna':
      return VACUO.id;
    case 'coast':
      return RETA_FINAL.id;
  }
}

// ───────────────────────────── Sequenciador ─────────────────────────────

/** Duração de uma semicolcheia em segundos. */
export function stepDuration(bpm: number): number {
  return 60 / bpm / 4;
}

export interface StepEvents {
  bass: number;
  lead: number;
  /** Quantos passos a nota do lead segura (até a próxima nota ou o fim do padrão). */
  leadSteps: number;
  arp: number;
  kick: boolean;
  snare: boolean;
  hat: boolean;
  /** Passo cai num tempo forte (acento do chimbal). */
  beat: boolean;
}

/** O que toca no passo global `step` (a sequência loopa). Função pura. */
export function stepEvents(song: SongDef, step: number): StepEvents {
  const total = song.sequence.length * STEPS_PER_PATTERN;
  const pos = ((step % total) + total) % total;
  const pattern = song.patterns[song.sequence[Math.floor(pos / STEPS_PER_PATTERN)]];
  const i = pos % STEPS_PER_PATTERN;
  let leadSteps = 0;
  if (pattern.lead[i]) {
    leadSteps = 1;
    while (i + leadSteps < STEPS_PER_PATTERN && pattern.lead[i + leadSteps] === 0 && leadSteps < 6) leadSteps++;
  }
  return {
    bass: pattern.bass[i], lead: pattern.lead[i], leadSteps, arp: pattern.arp[i],
    kick: pattern.kick[i] === 'x', snare: pattern.snare[i] === 'x', hat: pattern.hat[i] === 'x', beat: i % 4 === 0,
  };
}

interface Playing {
  song: SongDef;
  gain: GainNode;
  step: number;
  nextTime: number;
  timer: ReturnType<typeof setTimeout> | null;
}

export class Jukebox {
  private playing: Playing | null = null;

  constructor(private readonly ctx: AudioContext, private readonly dest: AudioNode) {}

  current(): string | null {
    return this.playing?.song.id ?? null;
  }

  /** Troca com fade; mesma música em curso é ignorada. */
  play(id: string): void {
    const song = songById(id);
    if (!song) return;
    if (this.playing?.song.id === id) return;
    this.stop();
    const now = this.ctx.currentTime;
    const gain = this.ctx.createGain();
    gain.gain.setValueAtTime(0, now);
    gain.gain.linearRampToValueAtTime(1, now + FADE_SECONDS);
    gain.connect(this.dest);
    this.playing = { song, gain, step: 0, nextTime: now + 0.05, timer: null };
    this.schedule();
  }

  /** Silencia com fade curto; o que já foi agendado termina sozinho. */
  stop(): void {
    const p = this.playing;
    if (!p) return;
    this.playing = null;
    if (p.timer !== null) clearTimeout(p.timer);
    const now = this.ctx.currentTime;
    p.gain.gain.cancelScheduledValues(now);
    p.gain.gain.setValueAtTime(p.gain.gain.value, now);
    p.gain.gain.linearRampToValueAtTime(0, now + FADE_SECONDS);
    setTimeout(() => p.gain.disconnect(), (FADE_SECONDS + LOOKAHEAD_SECONDS + 1) * 1000);
  }

  dispose(): void {
    this.stop();
  }

  private schedule(): void {
    const p = this.playing;
    if (!p) return;
    const sd = stepDuration(p.song.bpm);
    const now = this.ctx.currentTime;
    // Se o timer foi estrangulado (aba em segundo plano), pula os passos perdidos em vez de
    // despejá-los todos de uma vez.
    if (p.nextTime < now - sd) {
      const missed = Math.ceil((now - p.nextTime) / sd);
      p.step += missed;
      p.nextTime += missed * sd;
    }
    while (p.nextTime < now + LOOKAHEAD_SECONDS) {
      this.scheduleStep(p, p.step, p.nextTime);
      p.step++;
      p.nextTime += sd;
    }
    p.timer = setTimeout(() => this.schedule(), SCHEDULER_INTERVAL_MS);
  }

  private scheduleStep(p: Playing, step: number, t: number): void {
    const ev = stepEvents(p.song, step);
    const sd = stepDuration(p.song.bpm);
    const ctx = this.ctx;
    const out = p.gain;
    if (ev.kick) {
      playTone(ctx, out, { freq: 150, freqEnd: 45, dur: 0.1, type: 'sine', gain: 0.6, at: t, env: { attack: 0.002, decay: 0.12, sustain: 0.3, release: 0.1 } });
      noiseBurst(ctx, out, { dur: 0.01, gain: 0.15, at: t, filter: { type: 'highpass', freq: 2000 } });
    }
    if (ev.snare) {
      noiseBurst(ctx, out, { dur: 0.05, gain: 0.28, at: t, filter: { type: 'bandpass', freq: 1800, q: 0.7 }, env: { attack: 0.002, decay: 0.09, sustain: 0.1, release: 0.08 } });
      playTone(ctx, out, { freq: 190, dur: 0.05, type: 'triangle', gain: 0.25, at: t, env: { attack: 0.002, decay: 0.05, sustain: 0, release: 0.03 } });
    }
    if (ev.hat) {
      noiseBurst(ctx, out, { dur: 0.015, gain: ev.beat ? 0.09 : 0.055, at: t, filter: { type: 'highpass', freq: 7000 } });
    }
    if (ev.bass) {
      const f = midiToFreq(ev.bass);
      playTone(ctx, out, { freq: f, dur: sd * 0.85, type: 'sawtooth', gain: 0.2, at: t, filter: { type: 'lowpass', freq: 480, q: 1.5 }, env: { attack: 0.004, decay: 0.06, sustain: 0.55, release: 0.04 } });
      playTone(ctx, out, { freq: f, dur: sd * 0.85, type: 'triangle', gain: 0.12, at: t, env: { attack: 0.004, decay: 0.06, sustain: 0.6, release: 0.04 } });
    }
    if (ev.arp) {
      playTone(ctx, out, { freq: midiToFreq(ev.arp), dur: sd * 0.7, type: 'triangle', gain: 0.07, at: t, env: ENV_PLUCK });
    }
    if (ev.lead) {
      const f = midiToFreq(ev.lead);
      const dur = sd * ev.leadSteps * 0.9;
      const env = { attack: 0.01, decay: 0.06, sustain: 0.7, release: 0.08 };
      playTone(ctx, out, { freq: f, dur, type: 'square', gain: 0.075, at: t, detune: -7, env, filter: { type: 'lowpass', freq: 2600 } });
      playTone(ctx, out, { freq: f, dur, type: 'square', gain: 0.075, at: t, detune: 7, env, filter: { type: 'lowpass', freq: 2600 } });
    }
  }
}
