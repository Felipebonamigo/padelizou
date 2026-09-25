// A sessão: liga simulação, entrada, renderização, áudio e menus. Laço de passo fixo (60 Hz)
// com renderização a cada quadro. Nada aqui é determinístico por obrigação — só o núcleo é.
import { COUNTDOWN_TICKS, TICK_RATE } from '../core/constants';
import { applyRaceResult, createChampionship, nextTrackId } from '../core/championship';
import { CARS } from '../core/data/cars';
import { CUPS, cupDef } from '../core/data/cups';
import { createRace, formatTicks, stepRace } from '../core/sim/race';
import { getTrack, TRACKS, trackDef } from '../core/track';
import { hashString } from '../core/rng';
import type { ChampionshipState, HumanEntry, PlayerInput, RaceConfig, RaceState, SimEvent, Track } from '../core/types';
import { NEUTRAL_INPUT } from '../core/types';
import { setLanguage, t } from '../i18n';
import '../i18n/core';
import './strings';
import { createAudio } from '../audio/audio';
import { songForScenery } from '../audio/music';
import { createRenderer } from '../render/renderer';
import { trackOutline } from '../render/minimap';
import { createInput } from '../ui/input';
import { createMenus } from '../ui/menus';
import { evaluateAchievements, newTelemetry, type RaceTelemetry } from './achievements';
import type { AudioEngine, HudMessage, InputProvider, MenuEvent, Menus, RaceMode, RenderFrame, Renderer, Settings, ViewportSpec } from './contracts';
import { ACHIEVEMENTS, getDesktop, isDesktop, setFullscreen } from './desktop';
import { isCupUnlocked, loadSave, markCupCompleted, recordRaceResults, rememberLobby, saveSave } from './save';
import { loadSettings, saveSettings } from './settings';

const DT = 1 / TICK_RATE;
const MAX_STEPS_PER_FRAME = 4;
/** Depois do último humano cruzar a linha, quanto tempo a corrida fica na tela antes do resultado. */
const RESULTS_DELAY = 3.0;

interface ActiveRace {
  state: RaceState;
  track: Track;
  mode: RaceMode;
  humans: HumanEntry[];
  messages: Map<number, HudMessage[]>;
  telemetry: RaceTelemetry;
  /** Segundos desde o fim da corrida (para o atraso do resultado). */
  overFor: number;
  seed: number;
}

export interface Session {
  readonly settings: Settings;
  readonly renderer: Renderer;
  readonly input: InputProvider;
  readonly audio: AudioEngine;
  /** Preenchido logo depois da criação (os menus precisam da sessão para emitir eventos). */
  menus: Menus;
  race: ActiveRace | null;
  paused: boolean;
  /** Multiplicador de velocidade da simulação (playtest). */
  speed: number;
  start(): void;
  stop(): void;
  frame(now: number): void;
  handleMenuEvent(e: MenuEvent): void;
  startQuick(trackId: string, laps: number, humans: HumanEntry[], timeTrial?: boolean): void;
  startCup(cupId: string, humans: HumanEntry[]): void;
  /** Só para o playtest automatizado: liga assentos ao teclado sem passar pelo lobby. */
  debugBind(seat: number, device: 'kb1' | 'kb2'): void;
}

function randomSeed(): number {
  return (Date.now() ^ Math.floor(Math.random() * 0x7fffffff)) >>> 0;
}

export function createSession(canvas: HTMLCanvasElement, hudRoot: HTMLElement, uiRoot: HTMLElement): Session {
  const settings = loadSettings();
  const save = loadSave();
  setLanguage(settings.language);

  const renderer = createRenderer(canvas, hudRoot);
  const input = createInput(window);
  const audio = createAudio();
  audio.setVolumes(settings.masterVolume, settings.musicVolume, settings.sfxVolume);

  let champ: ChampionshipState | null = null;
  let cupSeed = 0;
  let lastNow = 0;
  let accumulator = 0;
  let elapsed = 0;
  let running = false;
  let rafId = 0;
  let idleTrack: Track = getTrack(TRACKS[0].id);
  let idleTimer = 0;

  const session: Session = {
    settings, renderer, input, audio,
    menus: null as unknown as Menus,
    race: null, paused: false, speed: 1,
    start, stop, frame, handleMenuEvent, startQuick, startCup,
    debugBind(seat, device) { input.bindSeat(seat, device); },
  };

  const menus = createMenus({
    root: uiRoot, input, settings, save, cups: CUPS, tracks: TRACKS, cars: CARS,
    isCupUnlocked: (cupId) => isCupUnlocked(save, cupId, CUPS),
    trackOutline, audio, isDesktop: isDesktop(),
    onEvent: handleMenuEvent,
  });
  session.menus = menus;

  function resize(): void {
    renderer.resize(window.innerWidth, window.innerHeight, Math.min(2, window.devicePixelRatio || 1));
  }
  window.addEventListener('resize', resize);
  resize();

  // ───────────────────────────── Corrida ─────────────────────────────

  function baseConfig(trackId: string, laps: number, humans: HumanEntry[], seed: number): RaceConfig {
    return {
      trackId, laps, humans, totalCars: Math.max(humans.length, settings.totalCars),
      difficulty: settings.difficulty, manualGear: settings.manualGear, assists: { ...settings.assists }, seed,
    };
  }

  function beginRace(config: RaceConfig, mode: RaceMode, humans: HumanEntry[]): void {
    const track = getTrack(config.trackId);
    const state = createRace(config, track);
    const messages = new Map<number, HudMessage[]>();
    for (const h of humans) messages.set(h.seat, []);
    session.race = { state, track, mode, humans, messages, telemetry: newTelemetry(), overFor: 0, seed: config.seed };
    session.paused = false;
    accumulator = 0;
    menus.hide();
    const music = settings.music === 'auto' ? songForScenery(track.def.scenery, track.def.timeOfDay) : settings.music;
    audio.setMusic(music === 'off' ? null : music);
    getDesktop()?.richPresence(`${track.def.name} · ${humans.length}P`).catch(() => undefined);
  }

  function startQuick(trackId: string, laps: number, humans: HumanEntry[], timeTrial = false): void {
    trackDef(trackId);
    champ = null;
    const config = baseConfig(trackId, laps, humans, randomSeed());
    if (timeTrial) { config.timeTrial = true; config.totalCars = humans.length; }
    beginRace(config, timeTrial ? 'timetrial' : 'quick', humans);
  }

  function startCup(cupId: string, humans: HumanEntry[]): void {
    const cup = cupDef(cupId);
    champ = createChampionship(cupId, humans);
    cupSeed = hashString(cupId + ':' + randomSeed());
    const trackId = cup.trackIds[0];
    const laps = trackDef(trackId).laps;
    const config = baseConfig(trackId, laps, humans, randomSeed());
    config.rosterSeed = cupSeed;
    beginRace(config, 'cup', humans);
  }

  function nextCupRace(): void {
    if (!champ) return;
    const trackId = nextTrackId(champ);
    if (!trackId || champ.eliminated) { toMain(); return; }
    const humans = session.race?.humans ?? [];
    const config = baseConfig(trackId, trackDef(trackId).laps, humans, randomSeed());
    config.rosterSeed = cupSeed;
    beginRace(config, 'cup', humans);
  }

  function retryRace(): void {
    const r = session.race;
    if (!r) { toMain(); return; }
    const config: RaceConfig = { ...r.state.config, seed: randomSeed() };
    beginRace(config, r.mode, r.humans);
  }

  function toMain(): void {
    session.race = null;
    session.paused = false;
    champ = null;
    for (let seat = 0; seat < 4; seat++) input.unbindSeat(seat);
    idleTrack = getTrack(TRACKS[Math.floor(Math.random() * TRACKS.length)].id);
    audio.setMusic(settings.music === 'auto' ? songForScenery('coast', 'dusk') : settings.music === 'off' ? null : settings.music);
    menus.show('main');
  }

  function pushMessage(seat: number, text: string, kind: HudMessage['kind'], ttl: number): void {
    const list = session.race?.messages.get(seat);
    if (!list) return;
    // Uma "big" por vez: a nova substitui a anterior.
    if (kind === 'big') for (let i = list.length - 1; i >= 0; i--) if (list[i].kind === 'big') list.splice(i, 1);
    list.push({ text, kind, ttl });
  }

  function broadcast(text: string, kind: HudMessage['kind'], ttl: number): void {
    for (const seat of session.race?.messages.keys() ?? []) pushMessage(seat, text, kind, ttl);
  }

  function seatOf(state: RaceState, carId: number): number {
    const car = state.cars[carId];
    return car ? car.seat : -1;
  }

  function handleEvent(r: ActiveRace, e: SimEvent): void {
    const { state } = r;
    const laps = state.config.laps;
    switch (e.type) {
      case 'countdown': broadcast(String(e.value), 'big', 0.9); audio.onEvent(e, 0); return;
      case 'go': broadcast(t('session.go'), 'big', 0.8); audio.onEvent(e, 0); return;
      case 'race_over': audio.onEvent(e, 0); return;
      default: break;
    }
    const seat = 'carId' in e ? seatOf(state, e.carId) : -1;
    audio.onEvent(e, seat);
    if (seat < 0) return;
    switch (e.type) {
      case 'lap': {
        r.telemetry.nitrosThisLap.set(seat, 0);
        if (!r.telemetry.offroadThisLap.has(seat) && e.lapTicks > 0) r.telemetry.perfectLap.add(seat);
        r.telemetry.offroadThisLap.delete(seat);
        if (e.lap <= laps) pushMessage(seat, e.lap === laps ? t('session.lastLap') : t('session.lap', { n: e.lap, total: laps }), 'big', 1.5);
        if (e.best && e.lap > 2) pushMessage(seat, t('session.bestLap', { time: formatTicks(e.lapTicks) }), 'good', 2);
        break;
      }
      case 'finish':
        pushMessage(seat, e.position === 1 ? t('session.finishFirst') : t('session.finish', { pos: e.position }), 'big', 4);
        break;
      case 'nitro': {
        const n = (r.telemetry.nitrosThisLap.get(seat) ?? 0) + 1;
        r.telemetry.nitrosThisLap.set(seat, n);
        r.telemetry.maxNitrosInLap.set(seat, Math.max(n, r.telemetry.maxNitrosInLap.get(seat) ?? 0));
        break;
      }
      case 'nitro_denied': pushMessage(seat, t('session.noNitro'), 'warn', 0.8); break;
      case 'tow': {
        const by = state.cars[e.byId];
        pushMessage(seat, t('session.tow', { name: by?.name ?? '?' }), 'good', 2);
        if (by && by.seat >= 0) r.telemetry.gaveTow.add(by.seat);
        break;
      }
      case 'fuel_low': pushMessage(seat, t('session.fuelLow'), 'warn', 3); break;
      case 'fuel_empty': pushMessage(seat, t('session.fuelEmpty'), 'warn', 3); break;
      case 'pit_enter': r.telemetry.pitted.add(seat); pushMessage(seat, t('session.pitEnter'), 'info', 2); break;
      case 'pit_exit': pushMessage(seat, t('session.pitExit'), 'info', 1.5); break;
      case 'offroad': if (e.entering) r.telemetry.offroadThisLap.add(seat); break;
      default: break;
    }
  }

  function finishRace(r: ActiveRace): void {
    const results = r.state.results ?? [];
    const newRecords = recordRaceResults(save, results, r.humans, r.track.def.id, r.state.config.laps);
    rememberLobby(save, r.humans);
    let cupJustCompleted: string | null = null;
    if (champ && r.mode === 'cup') {
      applyRaceResult(champ, results, r.humans);
      if (champ.completed) { markCupCompleted(save, champ.cupId); cupJustCompleted = champ.cupId; }
    }
    const unlocked = evaluateAchievements(save, r.mode, r.state, results, r.humans, r.telemetry, r.track.def.timeOfDay === 'night', cupJustCompleted, settings.difficulty);
    for (const id of unlocked) {
      save.achievements.push(id);
      getDesktop()?.achievement(id).catch(() => undefined);
    }
    saveSave(save);
    menus.show('results', { mode: r.mode, trackDef: r.track.def, results, humans: r.humans, champ, newRecords });
    audio.update(null, 0);
    if (unlocked.length) {
      const names = unlocked.map((id) => ACHIEVEMENTS.find((a) => a.id === id)?.[settings.language] ?? id);
      console.info('Conquistas:', names.join(', '));
    }
  }

  // ───────────────────────────── Laço ─────────────────────────────

  function readInputs(r: ActiveRace): PlayerInput[] {
    const inputs: PlayerInput[] = [];
    for (const h of r.humans) inputs[h.seat] = session.paused ? NEUTRAL_INPUT : input.readSeat(h.seat);
    return inputs;
  }

  function stepOnce(r: ActiveRace, inputs: PlayerInput[]): void {
    stepRace(r.state, r.track, inputs);
    for (const e of r.state.events) handleEvent(r, e);
  }

  function buildFrame(r: ActiveRace): RenderFrame {
    const viewports: ViewportSpec[] = r.humans
      .slice()
      .sort((a, b) => a.seat - b.seat)
      .map((h) => ({
        seat: h.seat, carIndex: r.state.cars.findIndex((c) => c.seat === h.seat), color: h.color, name: h.name,
        messages: r.messages.get(h.seat) ?? [],
      }));
    return {
      state: r.state, track: r.track, viewports,
      options: { quality: settings.quality, showMinimap: settings.showMinimap, screenShake: settings.screenShake },
      time: elapsed, paused: session.paused, coop: r.humans.length >= 2 && r.humans.every((h) => h.teamId === r.humans[0].teamId),
    };
  }

  function frame(now: number): void {
    const dtRaw = lastNow ? Math.min(0.25, (now - lastNow) / 1000) : DT;
    lastNow = now;
    elapsed += dtRaw;
    input.poll();

    const r = session.race;
    const menuOpen = menus.current() !== null;

    if (r && !menuOpen && !session.paused) {
      const seat = input.pausePressed();
      if (seat >= 0 && r.state.phase !== 'finished') {
        session.paused = true;
        menus.show('pause');
      }
    }

    if (r && !session.paused) {
      accumulator += dtRaw * session.speed;
      let steps = 0;
      const inputs = readInputs(r);
      while (accumulator >= DT && steps < MAX_STEPS_PER_FRAME * session.speed) {
        stepOnce(r, inputs);
        // As bordas (nitro, marcha) valem só no primeiro passo do quadro.
        for (const h of r.humans) { const i = inputs[h.seat]; if (i && i !== NEUTRAL_INPUT) inputs[h.seat] = { ...i, nitro: false, gearUp: false, gearDown: false }; }
        accumulator -= DT;
        steps++;
      }
      if (steps === MAX_STEPS_PER_FRAME * session.speed) accumulator = 0; // não tenta recuperar quadros perdidos
      for (const list of r.messages.values()) {
        for (let i = list.length - 1; i >= 0; i--) { list[i].ttl -= dtRaw; if (list[i].ttl <= 0) list.splice(i, 1); }
      }
      if (r.state.phase === 'finished' && menus.current() === null) {
        r.overFor += dtRaw;
        if (r.overFor >= RESULTS_DELAY) finishRace(r);
      }
    }

    if (r) {
      const f = buildFrame(r);
      renderer.render(f);
      audio.update(session.paused ? { ...f, paused: true } : f, dtRaw);
    } else {
      idleTimer += dtRaw;
      renderer.renderIdle(idleTimer, idleTrack);
      audio.update(null, dtRaw);
    }

    if (menuOpen || menus.current() !== null) menus.navigate(input.menuNav());
    menus.update(dtRaw);
  }

  function loop(now: number): void {
    if (!running) return;
    try { frame(now); } catch (err) { console.error(err); }
    rafId = requestAnimationFrame(loop);
  }

  function start(): void {
    if (running) return;
    running = true;
    menus.show('title');
    rafId = requestAnimationFrame(loop);
  }

  function stop(): void {
    running = false;
    cancelAnimationFrame(rafId);
  }

  // ───────────────────────────── Eventos dos menus ─────────────────────────────

  function applySettings(next: Settings): void {
    const languageChanged = next.language !== settings.language;
    const fullscreenChanged = next.fullscreen !== settings.fullscreen;
    Object.assign(settings, next, { assists: { ...next.assists } });
    saveSettings(settings);
    audio.setVolumes(settings.masterVolume, settings.musicVolume, settings.sfxVolume);
    if (languageChanged) { setLanguage(settings.language); menus.refreshLanguage(); }
    if (fullscreenChanged) setFullscreen(settings.fullscreen).catch(() => undefined);
    if (!session.race) {
      audio.setMusic(settings.music === 'auto' ? songForScenery('coast', 'dusk') : settings.music === 'off' ? null : settings.music);
    } else if (settings.music !== 'auto') {
      audio.setMusic(settings.music === 'off' ? null : settings.music);
    }
  }

  function handleMenuEvent(e: MenuEvent): void {
    switch (e.type) {
      case 'startCup': startCup(e.cupId, e.humans); break;
      case 'startQuick': startQuick(e.trackId, e.laps, e.humans); break;
      case 'startTimeTrial': startQuick(e.trackId, settings.quickLaps, e.humans, true); break;
      case 'nextRace': nextCupRace(); break;
      case 'retryRace': retryRace(); break;
      case 'restart': retryRace(); break;
      case 'resume': session.paused = false; menus.hide(); break;
      case 'toMain': toMain(); break;
      case 'settingsChanged': applySettings(e.settings); break;
      case 'quitApp': getDesktop()?.quit().catch(() => undefined); break;
      default: break;
    }
  }

  return session;
}

/** Exposto para o playtest automatizado (window.nc). */
export const SESSION_CONSTANTS = { COUNTDOWN_TICKS, TICK_RATE };
