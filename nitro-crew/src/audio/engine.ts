// Som de motor por jogador local: uma "voz" por viewport (até 4). Cada voz é um par de
// osciladores (dente de serra + quadrada uma oitava abaixo) num passa-baixa cuja frequência e
// abertura seguem o RPM; por cima, camadas de ruído para cascalho (grama), "whoosh" de nitro
// e a pneumática do box. Os nós vivem o tempo todo e só os ganhos mudam, sempre por
// setTargetAtTime — é isso que evita clique.
import { GEAR_TOP } from '../core/constants';
import { carDef } from '../core/data/cars';
import type { CarState } from '../core/types';
import type { RenderFrame } from '../game/contracts';
import { glide, makeFilter, noiseBurst, noiseSource } from './synth';

export const ENGINE_MIN_HZ = 55;
export const ENGINE_MAX_HZ = 260;
/** Quanto o nitro sobe o tom do motor. */
export const NITRO_PITCH_MULT = 1.25;
/** Ganho dos motores que não são o do primeiro viewport. */
export const SECONDARY_ENGINE_GAIN = 0.45;
/** RPM normalizado mínimo: parado (marcha 0) e logo depois de engatar uma marcha acima. */
const IDLE_RPM = 0.05;
const AFTER_SHIFT_RPM = 0.4;
/** Duração da queda de RPM ao trocar de marcha. */
const SHIFT_DIP_SECONDS = 0.14;
const SHIFT_DIP_FACTOR = 0.7;
/** Velocidade com que o RPM ouvido persegue o RPM alvo (por segundo). */
const RPM_FOLLOW_RATE = 10;

/**
 * Fração 0..1 da velocidade dentro da faixa da marcha atual: 0 no piso da marcha (topo da
 * anterior), 1 no teto dela (`GEAR_TOP[gear] * topSpeed`). Acima do teto (nitro, vácuo,
 * reduzir marcha) satura em 1.
 */
export function rpmFraction(speed: number, gear: number, topSpeed: number): number {
  if (!(topSpeed > 0)) return 0;
  const g = Math.max(0, Math.min(GEAR_TOP.length - 1, Math.floor(gear)));
  const lo = g > 0 ? GEAR_TOP[g - 1] * topSpeed : 0;
  const hi = GEAR_TOP[g] * topSpeed;
  if (hi <= lo) return 0;
  return Math.max(0, Math.min(1, (speed - lo) / (hi - lo)));
}

/** RPM normalizado ouvido: a fração dentro da marcha, com piso maior nas marchas altas. */
export function engineRpm(speed: number, gear: number, topSpeed: number): number {
  const floor = gear > 0 ? AFTER_SHIFT_RPM : IDLE_RPM;
  return floor + (1 - floor) * rpmFraction(speed, gear, topSpeed);
}

/** Frequência fundamental do motor (Hz) para um RPM normalizado; o nitro sobe o tom. */
export function engineFrequency(rpm: number, nitro: boolean): number {
  const r = Math.max(0, Math.min(1, rpm));
  const base = ENGINE_MIN_HZ + (ENGINE_MAX_HZ - ENGINE_MIN_HZ) * r;
  return nitro ? base * NITRO_PITCH_MULT : base;
}

class EngineVoice {
  private readonly saw: OscillatorNode;
  private readonly square: OscillatorNode;
  private readonly filter: BiquadFilterNode;
  private readonly gain: GainNode;
  private readonly gravel: AudioBufferSourceNode;
  private readonly gravelGain: GainNode;
  private readonly whoosh: AudioBufferSourceNode;
  private readonly whooshFilter: BiquadFilterNode;
  private readonly whooshGain: GainNode;
  private rpm = IDLE_RPM;
  private shiftTimer = 0;
  private lastGear = -1;
  private lastNitroTicks = 0;
  private wasInPit = false;

  constructor(private readonly ctx: AudioContext, private readonly dest: AudioNode, private readonly slotGain: number) {
    const now = ctx.currentTime;
    this.saw = ctx.createOscillator();
    this.saw.type = 'sawtooth';
    this.square = ctx.createOscillator();
    this.square.type = 'square';
    const mix = ctx.createGain();
    mix.gain.value = 0.5;
    this.filter = makeFilter(ctx, { type: 'lowpass', freq: 300, q: 1.2 });
    this.gain = ctx.createGain();
    this.gain.gain.value = 0;
    this.saw.connect(mix);
    this.square.connect(mix);
    mix.connect(this.filter);
    this.filter.connect(this.gain);
    this.gain.connect(dest);
    this.saw.frequency.value = ENGINE_MIN_HZ;
    this.square.frequency.value = ENGINE_MIN_HZ / 2;
    this.saw.start(now);
    this.square.start(now);

    this.gravel = noiseSource(ctx, true);
    const gravelFilter = makeFilter(ctx, { type: 'bandpass', freq: 700, q: 0.5 });
    this.gravelGain = ctx.createGain();
    this.gravelGain.gain.value = 0;
    this.gravel.connect(gravelFilter);
    gravelFilter.connect(this.gravelGain);
    this.gravelGain.connect(dest);
    this.gravel.start(now, Math.random());

    this.whoosh = noiseSource(ctx, true);
    this.whooshFilter = makeFilter(ctx, { type: 'bandpass', freq: 300, q: 1 });
    this.whooshGain = ctx.createGain();
    this.whooshGain.gain.value = 0;
    this.whoosh.connect(this.whooshFilter);
    this.whooshFilter.connect(this.whooshGain);
    this.whooshGain.connect(dest);
    this.whoosh.start(now, Math.random());
  }

  update(car: CarState, dt: number): void {
    const now = this.ctx.currentTime;
    const def = carDef(car.carId);
    const nitro = car.nitroTicks > 0;

    // Troca de marcha: o RPM cai por um instante e volta a subir.
    if (this.lastGear >= 0 && car.gear !== this.lastGear) this.shiftTimer = SHIFT_DIP_SECONDS;
    this.lastGear = car.gear;
    let target = engineRpm(car.speed, car.gear, def.topSpeed);
    if (this.shiftTimer > 0) {
      this.shiftTimer -= dt;
      target *= SHIFT_DIP_FACTOR;
    }
    this.rpm += (target - this.rpm) * (1 - Math.exp(-dt * RPM_FOLLOW_RATE));

    const freq = engineFrequency(this.rpm, nitro);
    glide(this.saw.frequency, freq, now, 0.03);
    glide(this.square.frequency, freq / 2, now, 0.03);
    glide(this.filter.frequency, 250 + this.rpm * 1400 + (nitro ? 600 : 0), now, 0.05);
    glide(this.gain.gain, this.slotGain * (0.12 + 0.08 * this.rpm + (nitro ? 0.03 : 0)), now, 0.08);

    // Grama: cascalho enquanto durar a derrapagem, mais alto quanto mais rápido.
    const speedFrac = Math.min(1, car.speed / def.topSpeed);
    glide(this.gravelGain.gain, car.skidTicks > 0 ? this.slotGain * (0.1 + 0.08 * speedFrac) : 0, now, 0.05);

    // Nitro: começou agora (o contador só cresce quando dispara) → varredura do filtro.
    if (car.nitroTicks > this.lastNitroTicks) {
      this.whooshFilter.frequency.cancelScheduledValues(now);
      this.whooshFilter.frequency.setValueAtTime(200, now);
      this.whooshFilter.frequency.exponentialRampToValueAtTime(2500, now + 0.7);
    }
    this.lastNitroTicks = car.nitroTicks;
    glide(this.whooshGain.gain, nitro ? this.slotGain * 0.09 : 0, now, nitro ? 0.05 : 0.15);

    // Box: pneumática curta (três estalos de parafusadeira) ao entrar.
    if (car.inPit && !this.wasInPit) {
      for (let i = 0; i < 3; i++) {
        noiseBurst(this.ctx, this.dest, {
          dur: 0.05, gain: this.slotGain * 0.22, at: now + i * 0.09, filter: { type: 'highpass', freq: 2500, q: 0.7 },
        });
      }
    }
    this.wasInPit = car.inPit;
  }

  /** Cala a voz com um fade curto (menus, viewport que sumiu); os nós continuam vivos. */
  silence(): void {
    const now = this.ctx.currentTime;
    glide(this.gain.gain, 0, now, 0.08);
    glide(this.gravelGain.gain, 0, now, 0.08);
    glide(this.whooshGain.gain, 0, now, 0.08);
    this.rpm = IDLE_RPM;
    this.shiftTimer = 0;
    this.lastGear = -1;
    this.lastNitroTicks = 0;
    this.wasInPit = false;
  }

  dispose(): void {
    for (const src of [this.saw, this.square, this.gravel, this.whoosh]) {
      try { src.stop(); } catch { /* já parado */ }
      src.disconnect();
    }
    this.gain.disconnect();
    this.gravelGain.disconnect();
    this.whooshGain.disconnect();
  }
}

/** Até quatro motores, um por viewport; o primeiro com ganho cheio, os demais reduzidos. */
export class EngineBank {
  private readonly voices: Array<EngineVoice | null> = [null, null, null, null];

  constructor(private readonly ctx: AudioContext, private readonly dest: AudioNode) {}

  update(frame: RenderFrame | null, dt: number): void {
    for (let i = 0; i < this.voices.length; i++) {
      const vp = frame ? frame.viewports[i] : undefined;
      const car = frame && vp ? frame.state.cars[vp.carIndex] : undefined;
      if (!car) {
        this.voices[i]?.silence();
        continue;
      }
      let voice = this.voices[i];
      if (!voice) {
        voice = new EngineVoice(this.ctx, this.dest, i === 0 ? 1 : SECONDARY_ENGINE_GAIN);
        this.voices[i] = voice;
      }
      voice.update(car, dt);
    }
  }

  dispose(): void {
    for (let i = 0; i < this.voices.length; i++) {
      this.voices[i]?.dispose();
      this.voices[i] = null;
    }
  }
}
