// Efeitos por evento da simulação e sons de interface. Cada efeito é uma receita curta de
// playTone/noiseBurst agendada no tempo do contexto; nada fica vivo depois da cauda.
import type { SimEvent, SpriteKind } from '../core/types';
import { ENV_HIT, ENV_PAD, ENV_PLUCK, noiseBurst, playTone } from './synth';

export interface Sfx {
  /** `localSeat` ≥ 0 = evento de um jogador local; -1 = carro da IA (só alguns tocam, baixinho). */
  play(event: SimEvent, localSeat: number): void;
  ui(kind: 'move' | 'confirm' | 'back'): void;
}

/** Ganho dos eventos de carros da IA em relação aos dos jogadores. */
export const AI_EVENT_GAIN = 0.3;
/** Eventos da IA que chegam ao alto-falante; os demais são ignorados. */
export const AI_AUDIBLE_EVENTS: ReadonlySet<SimEvent['type']> = new Set(['collision', 'crash', 'nitro']);
/** Intervalo mínimo entre dois eventos iguais da IA, para uma batida em cadeia não virar metralhadora. */
const AI_THROTTLE_SECONDS = 0.12;

// Frequências das notas usadas nas fanfarras (Hz).
const C4 = 261.63, A4 = 440.0, B4 = 493.88;
const C5 = 523.25, E5 = 659.25, G5 = 783.99, A5 = 880.0, C6 = 1046.5, E6 = 1318.5;

/** Colisão com o que quebra (vidro/metal) soa mais agudo do que madeira e pedra. */
const GLASSY: ReadonlySet<SpriteKind> = new Set(['building', 'tower', 'lamp', 'billboard', 'sign_left', 'sign_right', 'pit_sign']);

export function createSfx(ctx: AudioContext, dest: AudioNode, uiDest: AudioNode): Sfx {
  const lastAiAt = new Map<SimEvent['type'], number>();

  const tone = (freq: number, dur: number, gain: number, at: number, type: OscillatorType = 'square', extra: Partial<Parameters<typeof playTone>[2]> = {}) =>
    playTone(ctx, dest, { freq, dur, gain, at, type, ...extra });

  function countdown(at: number, g: number): void {
    tone(C4, 0.12, 0.25 * g, at, 'square', { env: ENV_PAD });
  }

  function go(at: number, g: number): void {
    tone(C5, 0.55, 0.28 * g, at, 'square', { env: ENV_PAD });
    tone(C5 * 1.005, 0.55, 0.12 * g, at, 'sawtooth', { env: ENV_PAD, filter: { type: 'lowpass', freq: 2500 } });
  }

  function lap(at: number, g: number, best: boolean): void {
    const notes = best ? [E5, G5, C6] : [E5, A5];
    notes.forEach((f, i) => {
      tone(f, 0.1, 0.2 * g, at + i * 0.11, 'triangle', { env: ENV_PLUCK });
      tone(f * 2, 0.1, 0.06 * g, at + i * 0.11, 'square', { env: ENV_PLUCK });
    });
    if (best) tone(E6, 0.35, 0.16 * g, at + 0.33, 'triangle', { env: ENV_PAD });
  }

  function finish(at: number, g: number, position: number): void {
    if (position === 1) {
      // Fanfarra longa: arpejo subindo, repique e acorde final.
      const melody = [C5, E5, G5, C6];
      melody.forEach((f, i) => tone(f, 0.12, 0.22 * g, at + i * 0.14, 'square', { env: ENV_PLUCK }));
      tone(G5, 0.12, 0.22 * g, at + 0.62, 'square', { env: ENV_PLUCK });
      tone(C6, 0.12, 0.22 * g, at + 0.76, 'square', { env: ENV_PLUCK });
      const chordAt = at + 0.92;
      for (const f of [C5, E5, G5, C6]) {
        tone(f, 0.9, 0.12 * g, chordAt, 'square', { env: ENV_PAD, detune: -6 });
        tone(f, 0.9, 0.12 * g, chordAt, 'triangle', { env: ENV_PAD, detune: 6 });
      }
      for (let i = 0; i < 4; i++) snare(at + i * 0.14, 0.18 * g);
      snare(chordAt, 0.3 * g);
      return;
    }
    // Pódio: acorde maior curto; fora dele, acorde menor.
    const chord = position <= 3 ? [C5, E5, G5] : [A4, C5, E5];
    for (const f of chord) tone(f, 0.35, 0.14 * g, at, 'square', { env: ENV_PAD });
  }

  function snare(at: number, gain: number): void {
    noiseBurst(ctx, dest, { dur: 0.06, gain, at, filter: { type: 'bandpass', freq: 1800, q: 0.8 }, env: { attack: 0.002, decay: 0.08, sustain: 0, release: 0.06 } });
  }

  function nitro(at: number, g: number): void {
    // "Chunk" do engate e o whoosh de ar que abre.
    tone(90, 0.06, 0.3 * g, at, 'square', { env: ENV_HIT, freqEnd: 40 });
    noiseBurst(ctx, dest, { dur: 0.03, gain: 0.25 * g, at, filter: { type: 'highpass', freq: 3000 } });
    noiseBurst(ctx, dest, {
      dur: 0.45, gain: 0.22 * g, at: at + 0.02, filter: { type: 'bandpass', freq: 400, q: 1.2, freqEnd: 4000 },
      env: { attack: 0.05, decay: 0.2, sustain: 0.5, release: 0.25 },
    });
  }

  function nitroDenied(at: number, g: number): void {
    tone(150, 0.09, 0.22 * g, at, 'square', { env: ENV_HIT });
    tone(120, 0.09, 0.22 * g, at + 0.1, 'square', { env: ENV_HIT });
  }

  function thud(at: number, gain: number): void {
    tone(80, 0.16, gain, at, 'sine', { env: { attack: 0.003, decay: 0.12, sustain: 0.2, release: 0.08 }, freqEnd: 38 });
  }

  function collision(at: number, g: number, strength: number): void {
    const s = Math.max(0.2, Math.min(1, strength));
    thud(at, 0.5 * s * g);
    noiseBurst(ctx, dest, { dur: 0.08, gain: 0.25 * s * g, at, filter: { type: 'lowpass', freq: 900 } });
  }

  function crash(at: number, g: number, sprite: SpriteKind): void {
    const glassy = GLASSY.has(sprite);
    thud(at, 0.55 * g);
    noiseBurst(ctx, dest, {
      dur: 0.3, gain: 0.3 * g, at, filter: { type: 'bandpass', freq: glassy ? 3200 : 1400, q: 0.7 },
      env: { attack: 0.003, decay: 0.2, sustain: 0.25, release: 0.15 },
    });
    if (glassy) {
      // Cacos: estalos agudos espalhados.
      for (let i = 0; i < 5; i++) {
        noiseBurst(ctx, dest, { dur: 0.02, gain: 0.12 * g, at: at + 0.04 + i * 0.05 + Math.random() * 0.02, filter: { type: 'highpass', freq: 5000 } });
      }
    }
  }

  function gravel(at: number, g: number): void {
    noiseBurst(ctx, dest, {
      dur: 0.22, gain: 0.2 * g, at, filter: { type: 'bandpass', freq: 900, q: 0.5 },
      env: { attack: 0.01, decay: 0.1, sustain: 0.5, release: 0.12 },
    });
  }

  function tow(at: number, g: number): void {
    // "Boost" ascendente e uma fanfarrinha de equipe.
    tone(200, 0.4, 0.2 * g, at, 'triangle', { env: ENV_PAD, freqEnd: 800 });
    noiseBurst(ctx, dest, { dur: 0.3, gain: 0.12 * g, at, filter: { type: 'bandpass', freq: 500, q: 1, freqEnd: 3000 }, env: ENV_PAD });
    tone(G5, 0.1, 0.18 * g, at + 0.42, 'square', { env: ENV_PLUCK });
    tone(C6, 0.2, 0.18 * g, at + 0.54, 'square', { env: ENV_PLUCK });
  }

  function pitChime(at: number, g: number, entering: boolean): void {
    const [a, b] = entering ? [A5, E5] : [E5, A5];
    tone(a, 0.1, 0.18 * g, at, 'triangle', { env: ENV_PLUCK });
    tone(b, 0.16, 0.18 * g, at + 0.12, 'triangle', { env: ENV_PLUCK });
    if (!entering) {
      // Parafusadeira solta a última roda ao sair.
      noiseBurst(ctx, dest, { dur: 0.08, gain: 0.15 * g, at: at + 0.3, filter: { type: 'highpass', freq: 2500 } });
    }
  }

  function fuelLow(at: number, g: number): void {
    tone(A5, 0.08, 0.2 * g, at, 'square', { env: ENV_HIT });
    tone(A5, 0.08, 0.2 * g, at + 0.14, 'square', { env: ENV_HIT });
  }

  function fuelEmpty(at: number, g: number): void {
    // Motor engasgando: três tremulações descendo.
    const freqs = [90, 72, 55];
    freqs.forEach((f, i) => {
      tone(f, 0.1, 0.25 * g, at + i * 0.17, 'sawtooth', {
        env: { attack: 0.01, decay: 0.05, sustain: 0.6, release: 0.05 }, freqEnd: f * 0.8, filter: { type: 'lowpass', freq: 600 },
      });
    });
  }

  function gear(at: number, g: number): void {
    tone(600, 0.03, 0.08 * g, at, 'square', { env: ENV_HIT });
  }

  function play(event: SimEvent, localSeat: number): void {
    const now = ctx.currentTime;
    let g = 1;
    if (localSeat < 0) {
      if (!AI_AUDIBLE_EVENTS.has(event.type)) return;
      const last = lastAiAt.get(event.type) ?? -1;
      if (now - last < AI_THROTTLE_SECONDS) return;
      lastAiAt.set(event.type, now);
      g = AI_EVENT_GAIN;
    }
    switch (event.type) {
      case 'countdown': countdown(now, g); break;
      case 'go': go(now, g); break;
      case 'lap': lap(now, g, event.best); break;
      case 'finish': finish(now, g, event.position); break;
      case 'nitro': nitro(now, g); break;
      case 'nitro_denied': nitroDenied(now, g); break;
      case 'collision': collision(now, g, event.strength); break;
      case 'crash': crash(now, g, event.sprite); break;
      case 'offroad': if (event.entering) gravel(now, g); break;
      case 'tow': tow(now, g); break;
      case 'pit_enter': pitChime(now, g, true); break;
      case 'pit_exit': pitChime(now, g, false); break;
      case 'fuel_low': fuelLow(now, g); break;
      case 'fuel_empty': fuelEmpty(now, g); break;
      case 'gear': gear(now, g); break;
      case 'race_over': break; // a fanfarra vem do finish
    }
  }

  function ui(kind: 'move' | 'confirm' | 'back'): void {
    const now = ctx.currentTime;
    switch (kind) {
      case 'move':
        playTone(ctx, uiDest, { freq: A5, dur: 0.03, gain: 0.12, at: now, type: 'triangle', env: ENV_HIT });
        break;
      case 'confirm':
        playTone(ctx, uiDest, { freq: C5, dur: 0.14, gain: 0.14, at: now, type: 'triangle', env: ENV_PLUCK });
        playTone(ctx, uiDest, { freq: G5, dur: 0.14, gain: 0.14, at: now, type: 'triangle', env: ENV_PLUCK });
        break;
      case 'back':
        playTone(ctx, uiDest, { freq: E5, dur: 0.16, gain: 0.14, at: now, type: 'triangle', env: ENV_PLUCK, freqEnd: B4 / 2 });
        break;
    }
  }

  return { play, ui };
}
