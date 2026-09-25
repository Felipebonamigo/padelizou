// Tijolos de síntese usados pelo motor, pelos efeitos e pela música. Tudo aqui recebe o
// AudioContext e o nó de destino explicitamente: nada global e nada criado antes do unlock().
// Cada nota é um oscilador descartável com envelope ADSR no ganho; ruído sai de um único
// buffer branco cacheado por contexto.

export interface Envelope {
  /** Segundos até o pico. */
  attack: number;
  /** Segundos do pico até o nível de sustentação. */
  decay: number;
  /** 0..1, fração do pico mantida até o fim da nota. */
  sustain: number;
  /** Segundos do fim da nota até o silêncio. */
  release: number;
}

/** Nota curta e percussiva (arpejo, blip de interface). */
export const ENV_PLUCK: Readonly<Envelope> = Object.freeze({ attack: 0.004, decay: 0.08, sustain: 0.35, release: 0.06 });
/** Nota sustentada (lead, acorde, fanfarra). */
export const ENV_PAD: Readonly<Envelope> = Object.freeze({ attack: 0.02, decay: 0.08, sustain: 0.8, release: 0.12 });
/** Batida: sobe instantâneo e cai sem sustentação. */
export const ENV_HIT: Readonly<Envelope> = Object.freeze({ attack: 0.002, decay: 0.06, sustain: 0, release: 0.04 });

export interface FilterSpec {
  type: BiquadFilterType;
  freq: number;
  q?: number;
  /** Varredura exponencial até esta frequência ao longo da nota. */
  freqEnd?: number;
}

export interface ToneOpts {
  freq: number;
  /** Duração em segundos até começar o release. */
  dur: number;
  type?: OscillatorType;
  gain?: number;
  env?: Envelope;
  /** Instante de início (`ctx.currentTime` por padrão). */
  at?: number;
  /** Deslizamento exponencial de `freq` até `freqEnd` ao longo de `dur`. */
  freqEnd?: number;
  /** Desafinação em cents (para engrossar com dois osciladores). */
  detune?: number;
  filter?: FilterSpec;
}

export interface NoiseOpts {
  dur: number;
  gain?: number;
  env?: Envelope;
  at?: number;
  filter?: FilterSpec;
}

/**
 * Agenda um ADSR no parâmetro (ganho) a partir de `at`. Devolve o instante em que a cauda
 * termina — é quando a fonte pode ser parada.
 */
export function applyEnvelope(param: AudioParam, at: number, peak: number, env: Envelope, dur: number): number {
  const a = Math.max(0.001, env.attack);
  const d = Math.max(0.001, env.decay);
  const r = Math.max(0.005, env.release);
  const sustain = Math.max(0, Math.min(1, env.sustain)) * peak;
  param.cancelScheduledValues(at);
  param.setValueAtTime(0, at);
  param.linearRampToValueAtTime(peak, at + a);
  param.linearRampToValueAtTime(sustain, at + a + d);
  const releaseStart = at + Math.max(dur, a + d);
  param.setValueAtTime(sustain, releaseStart);
  param.linearRampToValueAtTime(0, releaseStart + r);
  return releaseStart + r;
}

/** Transição suave (sem clique) de um parâmetro contínuo — motor, filtros, abafamento. */
export function glide(param: AudioParam, value: number, at: number, timeConstant = 0.05): void {
  param.setTargetAtTime(value, at, timeConstant);
}

export function makeFilter(ctx: BaseAudioContext, spec: FilterSpec, at?: number, end?: number): BiquadFilterNode {
  const f = ctx.createBiquadFilter();
  f.type = spec.type;
  f.Q.value = spec.q ?? 1;
  if (spec.freqEnd !== undefined && at !== undefined && end !== undefined && end > at) {
    f.frequency.setValueAtTime(Math.max(10, spec.freq), at);
    f.frequency.exponentialRampToValueAtTime(Math.max(10, spec.freqEnd), end);
  } else {
    f.frequency.value = spec.freq;
  }
  return f;
}

/** Oscilador descartável com envelope; desconecta sozinho ao terminar. */
export function playTone(ctx: BaseAudioContext, dest: AudioNode, o: ToneOpts): void {
  const at = o.at ?? ctx.currentTime;
  const osc = ctx.createOscillator();
  osc.type = o.type ?? 'square';
  osc.frequency.setValueAtTime(Math.max(1, o.freq), at);
  if (o.freqEnd !== undefined) osc.frequency.exponentialRampToValueAtTime(Math.max(1, o.freqEnd), at + o.dur);
  if (o.detune) osc.detune.setValueAtTime(o.detune, at);
  const amp = ctx.createGain();
  const end = applyEnvelope(amp.gain, at, o.gain ?? 0.2, o.env ?? ENV_PLUCK, o.dur);
  let head: AudioNode = osc;
  if (o.filter) {
    const f = makeFilter(ctx, o.filter, at, end);
    head.connect(f);
    head = f;
  }
  head.connect(amp);
  amp.connect(dest);
  osc.start(at);
  osc.stop(end + 0.01);
  osc.onended = () => { osc.disconnect(); amp.disconnect(); };
}

const noiseCache = new WeakMap<BaseAudioContext, AudioBuffer>();

/** Dois segundos de ruído branco, gerados uma vez por contexto. */
export function noiseBuffer(ctx: BaseAudioContext): AudioBuffer {
  let buf = noiseCache.get(ctx);
  if (!buf) {
    const n = Math.floor(ctx.sampleRate * 2);
    buf = ctx.createBuffer(1, n, ctx.sampleRate);
    const data = buf.getChannelData(0);
    for (let i = 0; i < n; i++) data[i] = Math.random() * 2 - 1;
    noiseCache.set(ctx, buf);
  }
  return buf;
}

/** Fonte de ruído (em loop por padrão) ainda não iniciada — para camadas contínuas como cascalho. */
export function noiseSource(ctx: BaseAudioContext, loop = true): AudioBufferSourceNode {
  const src = ctx.createBufferSource();
  src.buffer = noiseBuffer(ctx);
  src.loop = loop;
  return src;
}

/** Rajada de ruído com envelope e filtro opcional; desconecta sozinha ao terminar. */
export function noiseBurst(ctx: BaseAudioContext, dest: AudioNode, o: NoiseOpts): void {
  const at = o.at ?? ctx.currentTime;
  const src = noiseSource(ctx, true);
  const amp = ctx.createGain();
  const end = applyEnvelope(amp.gain, at, o.gain ?? 0.2, o.env ?? ENV_HIT, o.dur);
  let head: AudioNode = src;
  if (o.filter) {
    const f = makeFilter(ctx, o.filter, at, end);
    head.connect(f);
    head = f;
  }
  head.connect(amp);
  amp.connect(dest);
  // Começa num ponto aleatório do buffer para duas batidas seguidas não soarem idênticas.
  src.start(at, Math.random() * 1.5);
  src.stop(end + 0.01);
  src.onended = () => { src.disconnect(); amp.disconnect(); };
}
