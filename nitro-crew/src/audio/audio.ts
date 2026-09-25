// Implementação do AudioEngine: 100% procedural com WebAudio. O AudioContext só nasce em
// unlock() (gesto do usuário); antes disso — e em Node, onde não existe — tudo é no-op
// silencioso que guarda o que precisa (volumes, música escolhida) para aplicar depois.
//
// Grafo:
//   motores ─┐
//   efeitos ─┴→ sfxGain ─┐
//   jukebox ──→ musicGain ┴→ duckFilter → duckGain → masterGain → compressor → destino
//   interface ─→ uiGain ─────────────────────────────→ masterGain
// O abafamento da pausa (filtro + duck) não pega os sons de interface, que precisam soar
// limpos justamente no menu de pausa.
import type { SimEvent, TrackDef } from '../core/types';
import { DEFAULT_SETTINGS, type AudioEngine, type MusicInfo, type RenderFrame } from '../game/contracts';
import { EngineBank } from './engine';
import { Jukebox, musicInfos, songById, songForScenery, SONGS } from './music';
import { createSfx, type Sfx } from './sfx';
import { glide } from './synth';

const PAUSE_FILTER_HZ = 500;
const OPEN_FILTER_HZ = 20000;
const PAUSE_DUCK = 0.45;

interface Graph {
  ctx: AudioContext;
  master: GainNode;
  music: GainNode;
  sfx: GainNode;
  ui: GainNode;
  duckFilter: BiquadFilterNode;
  duckGain: GainNode;
  engines: EngineBank;
  jukebox: Jukebox;
  effects: Sfx;
}

function audioContextClass(): (new () => AudioContext) | null {
  if (typeof window === 'undefined') return null;
  const AC = window.AudioContext;
  return typeof AC === 'function' ? AC : null;
}

export function createAudio(): AudioEngine {
  let graph: Graph | null = null;
  const volumes = { master: DEFAULT_SETTINGS.masterVolume, music: DEFAULT_SETTINGS.musicVolume, sfx: DEFAULT_SETTINGS.sfxVolume };
  /** O que foi pedido em setMusic: id, 'auto' ou null. */
  let wanted: string | null = null;
  let lastTrack: TrackDef | null = null;
  let paused = false;

  function resolveWanted(): string | null {
    if (wanted === null) return null;
    if (wanted !== 'auto' && songById(wanted)) return wanted;
    // 'auto' (ou id desconhecido, de um save antigo): escolhe pela pista em cena.
    if (lastTrack) return songForScenery(lastTrack.scenery, lastTrack.timeOfDay);
    return SONGS[0].id;
  }

  function applyMusic(): void {
    if (!graph) return;
    const id = resolveWanted();
    if (id === null) graph.jukebox.stop();
    else graph.jukebox.play(id);
  }

  function applyVolumes(): void {
    if (!graph) return;
    const now = graph.ctx.currentTime;
    glide(graph.master.gain, volumes.master, now, 0.02);
    glide(graph.music.gain, volumes.music, now, 0.02);
    glide(graph.sfx.gain, volumes.sfx, now, 0.02);
    glide(graph.ui.gain, volumes.sfx, now, 0.02);
  }

  function applyPause(): void {
    if (!graph) return;
    const now = graph.ctx.currentTime;
    glide(graph.duckFilter.frequency, paused ? PAUSE_FILTER_HZ : OPEN_FILTER_HZ, now, 0.08);
    glide(graph.duckGain.gain, paused ? PAUSE_DUCK : 1, now, 0.08);
  }

  function build(AC: new () => AudioContext): Graph {
    const ctx = new AC();
    const master = ctx.createGain();
    const compressor = ctx.createDynamicsCompressor();
    compressor.threshold.value = -12;
    compressor.ratio.value = 4;
    master.connect(compressor);
    compressor.connect(ctx.destination);

    const duckGain = ctx.createGain();
    const duckFilter = ctx.createBiquadFilter();
    duckFilter.type = 'lowpass';
    duckFilter.frequency.value = OPEN_FILTER_HZ;
    duckFilter.connect(duckGain);
    duckGain.connect(master);

    const music = ctx.createGain();
    const sfx = ctx.createGain();
    const ui = ctx.createGain();
    music.connect(duckFilter);
    sfx.connect(duckFilter);
    ui.connect(master);

    return {
      ctx, master, music, sfx, ui, duckFilter, duckGain,
      engines: new EngineBank(ctx, sfx),
      jukebox: new Jukebox(ctx, music),
      effects: createSfx(ctx, sfx, ui),
    };
  }

  function unlock(): void {
    if (!graph) {
      const AC = audioContextClass();
      if (!AC) return;
      try {
        graph = build(AC);
      } catch {
        graph = null; // sem áudio neste ambiente: segue mudo
        return;
      }
      applyVolumes();
      applyPause();
      applyMusic();
    }
    if (graph.ctx.state === 'suspended') void graph.ctx.resume().catch(() => { /* sem gesto válido ainda */ });
  }

  function update(frame: RenderFrame | null, dt: number): void {
    if (frame) {
      const def = frame.track.def;
      if (def !== lastTrack) {
        lastTrack = def;
        if (wanted === 'auto') applyMusic();
      }
    }
    if (!graph) return;
    const nowPaused = frame?.paused ?? false;
    if (nowPaused !== paused) {
      paused = nowPaused;
      applyPause();
    }
    graph.engines.update(frame, dt);
  }

  function onEvent(event: SimEvent, localSeat: number): void {
    graph?.effects.play(event, localSeat);
  }

  function setMusic(id: string | null): void {
    wanted = id;
    applyMusic();
  }

  function currentMusic(): string | null {
    return resolveWanted();
  }

  function setVolumes(master: number, music: number, sfx: number): void {
    volumes.master = clamp01(master);
    volumes.music = clamp01(music);
    volumes.sfx = clamp01(sfx);
    applyVolumes();
  }

  function musicList(): MusicInfo[] {
    return musicInfos();
  }

  function ui(kind: 'move' | 'confirm' | 'back'): void {
    graph?.effects.ui(kind);
  }

  return { unlock, update, onEvent, setMusic, currentMusic, setVolumes, musicList, ui };
}

function clamp01(v: number): number {
  return Number.isFinite(v) ? Math.max(0, Math.min(1, v)) : 0;
}
