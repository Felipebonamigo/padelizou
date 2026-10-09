// A sessão: liga simulação, entrada, renderização, áudio e menus. Laço de passo fixo (60 Hz)
// com renderização a cada quadro. Nada aqui é determinístico por obrigação — só o núcleo é.
import { COUNTDOWN_TICKS, TICK_RATE } from '../core/constants';
import { createChampionship, nextTrackId } from '../core/championship';
import { CARS } from '../core/data/cars';
import { CUPS, cupDef } from '../core/data/cups';
import { seatColor } from '../core/data/drivers';
import { createRace, formatTicks } from '../core/sim/race';
import { advanceAfterFlag, type AfterFlag } from './after-flag';
import { finishMessage } from './finish-message';
import { getTrack, TRACKS, trackDef } from '../core/track';
import { hashString } from '../core/rng';
import type { ChampionshipState, HumanEntry, PlayerInput, RaceConfig, RaceState, SimEvent, Track } from '../core/types';
import { NEUTRAL_INPUT } from '../core/types';
import { setLanguage, t } from '../i18n';
import '../i18n/core';
import './strings';
import { applyAccessibility } from '../access/apply';
import { assistedHumans, withRaceAssists } from '../access/humans';
import { createAudio } from '../audio/audio';
import { songForScenery } from '../audio/music';
import { createRenderer } from '../render/renderer';
import { createRetroRenderer } from '../render-pseudo3d/renderer';
import { trackOutline } from '../render/minimap';
import { createInput } from '../ui/input';
import { createMenus } from '../ui/menus';
import { createOnlineHud } from '../ui/screens/online';
import { createTutorialPanel } from '../ui/screens/tutorial';
import { newTelemetry, type RaceTelemetry } from './achievements';
import { saveCupProgress } from './career-save';
import { compactHumans, createCareerSession } from './career-session';
import type { AudioEngine, CarColors, HudMessage, InputProvider, MenuEvent, Menus, RaceDriver, RaceMode, RenderFrame, Renderer, RenderStyle, Settings, ViewportSpec } from './contracts';
import { getDesktop, isDesktop, setFullscreen } from './desktop';
import { reportError } from './errors';
import { startGhost, type GhostHooks } from './ghost-session';
import { gridRival } from './rivals';
import { createOnlineController, type OnlineController } from './online-session';
import { createPartySession, isPartyRaceMode } from './party-session';
import { racePaints, withoutPaint } from './paints';
import { carIndexOfSeat } from '../core/modes';
import { createFeelTracker, observeFeel, type FeelTracker } from './race-feel';
import { getActivePlaylog, playlogRace } from './playlog';
import { settleRace, stepObserved, type RaceOutcome } from './raceEnd';
import { newRumbleMemory, rumbleCues } from './rumble';
import { isCupUnlocked, loadSave, saveSave } from './save';
import { loadSettings, saveSettings } from './settings';
import { createTutorialSession, type TutorialSession } from './tutorial-session';

const DT = 1 / TICK_RATE;
const MAX_STEPS_PER_FRAME = 4;
/** Depois do último humano cruzar a linha, quanto tempo a corrida fica na tela antes do resultado. */
const RESULTS_DELAY = 3.0;
/** Conquista no HUD: fica até o resultado cobrir a corrida. */
const ACHIEVEMENT_TTL = RESULTS_DELAY + 0.5;

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
  /** Contas fechadas no tick em que a corrida acabou (raceEnd.ts: settleRace); o resultado aparece depois. */
  outcome: RaceOutcome | null;
  /** Fonte das entradas por tick: null = controles deste computador; online = lockstep. */
  driver: RaceDriver | null;
  /** Assentos jogados neste computador (os únicos com viewport, HUD e som de jogador). */
  localSeats: number[];
  /** Fantasma do contra-relógio local (ghost-session.ts); null nas outras corridas. */
  ghost: GhostHooks | null;
  /** Pintura de cada carro (índice = state.cars), montada na largada a partir de `humans` (paints.ts: racePaints). */
  paints: Array<CarColors | null>;
  /** Bordas (nitro, marchas) lidas num quadro de tela em que não coube nenhum passo da física; valem no próximo passo. */
  /** Cópia só de exibição que segue andando depois da bandeirada (o estado real fica parado); some com a corrida. */
  afterFlag?: AfterFlag;
  /** Termômetro de emoção por carro humano local (race-feel.ts) e se a corrida já foi para o diário (playlog.ts). */
  feel: Record<number, FeelTracker>;
  logged: boolean;
  heldEdges?: Array<Pick<PlayerInput, 'nitro' | 'gearUp' | 'gearDown'> | undefined>;
}

export interface Session {
  readonly settings: Settings;
  readonly renderer: Renderer;
  readonly input: InputProvider;
  readonly audio: AudioEngine;
  readonly online: OnlineController;
  /** Preenchido logo depois da criação (os menus precisam da sessão para emitir eventos). */
  menus: Menus;
  /** Tutorial de 90 segundos ("Como jogar"); preenchido junto com os menus. */
  tutorial: TutorialSession;
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
  /** Só para o playtest: avança N ticks da corrida agora, com a entrada atual, sem esperar quadros. */
  debugStep(ticks: number): void;
}

function randomSeed(): number {
  return (Date.now() ^ Math.floor(Math.random() * 0x7fffffff)) >>> 0;
}

/** O renderizador do estilo escolhido: o 3D moderno (Three.js) ou o pseudo-3D Retrô (canvas 2D). */
function makeRenderer(style: RenderStyle, canvas: HTMLCanvasElement, hudRoot: HTMLElement): Renderer {
  return style === 'retro' ? createRetroRenderer(canvas, hudRoot) : createRenderer(canvas, hudRoot);
}

export function createSession(canvas: HTMLCanvasElement, hudRoot: HTMLElement, uiRoot: HTMLElement): Session {
  const settings = loadSettings();
  const save = loadSave();
  setLanguage(settings.language);

  let renderer: Renderer = makeRenderer(settings.renderStyle, canvas, hudRoot);
  /**
   * Estilo do renderizador em uso. A tela de Opções edita o próprio objeto `settings` antes de avisar, então
   * comparar o que chega com `settings` nunca vê a troca: compara-se com o que está desenhando.
   */
  let activeStyle = settings.renderStyle;
  applyAccessibility(settings, hudRoot, uiRoot);
  const input = createInput(window, { bindings: () => settings.controls, vibration: () => settings.vibration });
  const rumbleMemory = newRumbleMemory();
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

  // Online: a sessão empresta a corrida (beginRace com um driver de lockstep) e os menus.
  const online = createOnlineController({
    settings, save, input,
    startRace: (config, localSeats, driver, state, humans) => beginRace(config, 'quick', humans ?? config.humans, { driver, localSeats, state }),
    raceState: () => session.race?.state ?? null,
    clearRace: () => { if (session.race) logRace(session.race); session.race = null; session.paused = false; },
    showScreen: () => menus.show('online'),
    hideScreen: () => menus.hide(),
    menuOpen: () => menus.current() !== null,
    exitToMain: () => toMain(),
    persistSettings: () => saveSettings(settings),
    persistSave: () => saveSave(save),
    // Aba em segundo plano/janela minimizada: sem requestAnimationFrame, o online anda pela rede.
    hidden: () => document.visibilityState === 'hidden',
    runHidden: (dt) => { const r = session.race; if (r?.driver) r.driver.advance(dt, readInputs(r), (i) => stepOnce(r, i)); },
  }, { hud: createOnlineHud });

  const session: Session = {
    settings, input, audio, online,
    get renderer() { return renderer; },
    menus: null as unknown as Menus,
    tutorial: null as unknown as TutorialSession,
    race: null, paused: false, speed: 1,
    start, stop, frame, handleMenuEvent, startQuick, startCup,
    debugBind(seat, device) { input.bindSeat(seat, device); },
    debugStep(ticks) {
      const r = session.race;
      if (!r || session.paused) return;
      input.poll();
      const inputs = readInputs(r);
      if (r.driver) { r.driver.force(ticks, inputs, (i) => stepOnce(r, i)); for (const list of r.messages.values()) list.length = 0; return; }
      for (let i = 0; i < ticks && r.state.phase !== 'finished'; i++) {
        stepOnce(r, inputs);
        for (const h of r.humans) { const inp = inputs[h.seat]; if (inp && inp !== NEUTRAL_INPUT) inputs[h.seat] = { ...inp, nitro: false, gearUp: false, gearDown: false }; }
      }
      for (const list of r.messages.values()) list.length = 0;
    },
  };

  // Festa: os menus leem o torneio daqui; os menus em si nascem depois (o host os busca na hora).
  const party = createPartySession({
    get menus() { return menus; }, settings, input, baseConfig, randomSeed,
    beginRace: (config, mode, humans) => beginRace(config, mode, humans),
  });
  const menus = createMenus({
    root: uiRoot, input, settings, save, cups: CUPS, tracks: TRACKS, cars: CARS,
    isCupUnlocked: (cupId) => isCupUnlocked(save, cupId, CUPS),
    trackOutline, audio, isDesktop: isDesktop(), online, party,
    onEvent: handleMenuEvent,
  });
  session.menus = menus;
  const career = createCareerSession({
    save, settings, menus, input, baseConfig, beginRace, toIdle, randomSeed, persist: () => saveSave(save),
  });
  session.tutorial = createTutorialSession({
    save, settings, menus, input, toIdle, startCup, randomSeed, persist: () => saveSave(save),
    beginRace: (config, humans, driver, state) => beginRace(config, 'quick', humans, { driver, localSeats: humans.map((h) => h.seat), state }),
    hud: pushMessage,
    panel: createTutorialPanel(hudRoot),
  });

  /**
   * Opções › Visual: troca o renderizador na hora (moderno ↔ Retrô). Um canvas não aceita WebGL e 2D ao
   * mesmo tempo, então o antigo é descartado e um canvas novo toma o lugar dele no DOM.
   */
  function swapRenderer(): void {
    const old = renderer.canvas;
    renderer.dispose();
    const fresh = document.createElement('canvas');
    fresh.id = old.id;
    fresh.className = old.className;
    old.replaceWith(fresh);
    renderer = makeRenderer(settings.renderStyle, fresh, hudRoot);
    activeStyle = settings.renderStyle;
    resize();
  }

  function resize(): void {
    renderer.resize(window.innerWidth, window.innerHeight, Math.min(2, window.devicePixelRatio || 1));
  }
  window.addEventListener('resize', resize);
  // Fechar a aba ou a janela no meio da corrida a registra como abandonada (o localStorage é síncrono).
  window.addEventListener('pagehide', () => { if (session.race) logRace(session.race); });
  resize();

  // ───────────────────────────── Corrida ─────────────────────────────

  function baseConfig(trackId: string, laps: number, humans: HumanEntry[], seed: number): RaceConfig {
    return {
      // A pintura fica com os humanos da sessão (r.humans → RenderFrame.paints): a config e o estado não a carregam.
      trackId, laps, humans: assistedHumans(withoutPaint(humans), settings.seatAssists), totalCars: Math.max(humans.length, settings.totalCars),
      difficulty: settings.difficulty, manualGear: settings.manualGear, assists: { ...settings.assists }, seed,
    };
  }

  function beginRace(config: RaceConfig, mode: RaceMode, humans: HumanEntry[], net?: { driver: RaceDriver; localSeats: number[]; state?: RaceState }): void {
    // Os modos de festa são só locais: uma corrida em rede nunca nasce com eles (docs/MODOS.md).
    if (net && (isPartyRaceMode(mode) || config.mode)) { net.driver.dispose(); console.warn(t('party.onlineRefused')); toMain(); return; }
    if (session.race) logRace(session.race);
    const track = getTrack(config.trackId);
    const state = net?.state ?? createRace(config, track);
    const localSeats = net ? net.localSeats : humans.map((h) => h.seat);
    const messages = new Map<number, HudMessage[]>();
    for (const seat of localSeats) messages.set(seat, []);
    const ghost = startGhost(mode, net !== undefined, config.trackId, humans, { settings, hud: (seat, m) => pushMessage(seat, m.text, m.kind, m.ttl) });
    session.race = {
      state, track, mode, humans, messages, telemetry: newTelemetry(), overFor: 0, seed: config.seed, outcome: null, driver: net?.driver ?? null, localSeats, ghost,
      paints: racePaints(state.cars, humans),
      feel: Object.fromEntries(localSeats.map((s) => carIndexOfSeat(state, s)).filter((c) => c >= 0).map((c) => [c, createFeelTracker(c)])), logged: false,
    };
    getActivePlaylog()?.raceStarted({ mode, trackId: config.trackId, humans: localSeats.length });
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

  function startCup(cupId: string, lobbyHumans: HumanEntry[]): void {
    const cup = cupDef(cupId);
    // Assentos contíguos: a copa fica salva (1.7a) e o "Continuar" religa a partir do P1.
    const humans = compactHumans(input, lobbyHumans, settings.seatAssists);
    champ = createChampionship(cupId, humans);
    cupSeed = hashString(cupId + ':' + randomSeed());
    saveCupProgress(save, champ, cupSeed, humans);
    saveSave(save);
    const trackId = cup.trackIds[0];
    const laps = trackDef(trackId).laps;
    const config = baseConfig(trackId, laps, humans, randomSeed());
    config.rosterSeed = cupSeed;
    config.rival = gridRival(champ);
    beginRace(config, 'cup', humans);
  }

  /** Menu principal → Continuar: retoma a copa salva na próxima corrida (o lobby já religou os assentos). */
  function continueCup(): void {
    const saved = save.cupInProgress;
    if (!saved) { toMain(); return; }
    champ = JSON.parse(JSON.stringify(saved.champ)) as ChampionshipState;
    cupSeed = saved.cupSeed;
    nextCupRace(saved.humans);
  }

  function nextCupRace(humans: HumanEntry[] = session.race?.humans ?? []): void {
    if (!champ) return;
    const trackId = nextTrackId(champ);
    if (!trackId || champ.eliminated) { toMain(); return; }
    const config = baseConfig(trackId, trackDef(trackId).laps, humans, randomSeed());
    config.rosterSeed = cupSeed;
    config.rival = gridRival(champ);
    beginRace(config, 'cup', humans);
  }

  function retryRace(): void {
    const r = session.race;
    if (!r) { toMain(); return; }
    const config: RaceConfig = { ...r.state.config, seed: randomSeed() };
    beginRace(config, r.mode, r.humans);
  }

  /** Sai da corrida para os menus (fundo animado e música de menu), sem mexer nos assentos; fecha o online se houver. */
  function toIdle(): void {
    if (session.race) logRace(session.race);
    const driver = session.race?.driver;
    session.race = null;
    driver?.dispose();
    session.paused = false;
    champ = null;
    idleTrack = getTrack(TRACKS[Math.floor(Math.random() * TRACKS.length)].id);
    audio.setMusic(settings.music === 'auto' ? songForScenery('coast', 'dusk') : settings.music === 'off' ? null : settings.music);
  }

  function toMain(): void {
    party.abandon();
    toIdle();
    for (let seat = 0; seat < 4; seat++) input.unbindSeat(seat);
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
      case 'race_over': audio.onEvent(e, 0); settle(r); return;
      default: break;
    }
    for (const m of party.eventMessages(r.mode, state, e)) pushMessage(m.seat, m.message.text, m.message.kind, m.message.ttl);
    const carSeat = 'carId' in e ? seatOf(state, e.carId) : -1;
    // Online, o carro de outro computador soa como o de um adversário.
    const seat = r.localSeats.includes(carSeat) ? carSeat : -1;
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
        pushMessage(seat, finishMessage(state, e.carId, e.position), 'big', 4);
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

  /**
   * Fecha as contas no race_over (raceEnd.ts). As conquistas vão já para o HUD de quem as
   * ganhou — a corrida ainda fica RESULTS_DELAY na tela antes do resultado. Idempotente.
   */
  function settle(r: ActiveRace): RaceOutcome {
    return settleRace(save, r, {
      champ, difficulty: settings.difficulty, hudTtl: ACHIEVEMENT_TTL,
      effects: {
        achievement: (id) => { getDesktop()?.achievement(id).catch(() => undefined); },
        hud: (seat, m) => pushMessage(seat, m.text, m.kind, m.ttl),
        persist: () => saveSave(save),
      },
      localSeats: r.driver ? r.localSeats : null,
      afterCup: (c) => saveCupProgress(save, c, cupSeed, r.humans),
      careerChamp: () => champ,
      careerFinished: (results) => {
        const out = career.raceFinished(results);
        champ = out.champ;
        return out.cupCompleted;
      },
    });
  }

  /** Registra a corrida no diário uma vez só; o que não chegou ao race_over entra como abandonada. */
  function logRace(r: ActiveRace): void {
    if (r.logged) return;
    r.logged = true;
    getActivePlaylog()?.raceEnded(playlogRace(r, { abandoned: r.state.phase !== 'finished', champ }));
  }

  function finishRace(r: ActiveRace): void {
    const { newRecords, achievements, newStamp } = settle(r);
    logRace(r);
    const data = { mode: r.mode, trackDef: r.track.def, results: r.state.results ?? [], humans: withRaceAssists(r.humans, r.state.config.humans), champ, newRecords, achievements, party: party.raceFinished(r.mode, r.state), newStamp: newStamp ?? null };
    if (r.driver) r.driver.finished(data);
    else menus.show('results', data);
    audio.update(null, 0);
  }

  // ───────────────────────────── Laço ─────────────────────────────

  function readInputs(r: ActiveRace): PlayerInput[] {
    const inputs: PlayerInput[] = [];
    for (const seat of r.localSeats) inputs[seat] = session.paused ? NEUTRAL_INPUT : input.readSeat(seat);
    return inputs;
  }

  function stepOnce(r: ActiveRace, inputs: PlayerInput[]): void {
    const events = stepObserved(r, inputs); // observa antes: o tick que fecha a corrida já conta
    for (const f of Object.values(r.feel)) observeFeel(f, r.state);
    for (const e of events) handleEvent(r, e);
    r.ghost?.afterTick(r.state);
    for (const c of rumbleCues(r.state, rumbleMemory)) input.rumble(c.seat, c.strength, c.ms);
  }

  function buildFrame(r: ActiveRace): RenderFrame {
    const viewports: ViewportSpec[] = r.humans
      .filter((h) => r.localSeats.includes(h.seat))
      .sort((a, b) => a.seat - b.seat)
      .map((h) => {
        const own = r.messages.get(h.seat) ?? [];
        const fixed = party.hudLines(r.mode, r.state, h.seat);
        return {
          seat: h.seat, carIndex: carIndexOfSeat(r.state, h.seat), color: seatColor(h.seat, settings.colorPalette), name: h.name,
          messages: fixed.length ? [...fixed, ...own] : own,
        };
      });
    return {
      state: r.afterFlag?.display ?? r.state, track: r.track, viewports,
      options: { quality: settings.quality, showMinimap: settings.showMinimap, screenShake: settings.screenShake, reduceEffects: settings.reduceEffects, palette: settings.colorPalette, landmarkCaptions: settings.landmarkCaptions },
      time: elapsed, paused: session.paused, coop: r.humans.length >= 2 && r.humans.every((h) => h.teamId === r.humans[0].teamId),
      showHud: menus.current() === null || menus.current() === 'pause' || (r.driver !== null && online.quitOpen),
      ghost: r.ghost?.frame(r.state),
      paints: r.paints,
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
        if (r.driver) r.driver.pauseKey(); // online não pausa: pergunta se quer sair
        else { session.paused = true; menus.show('pause'); }
      }
    }

    if (r && !session.paused) {
      // Online, o lockstep decide quantos ticks rodam (e espera a rede); o acumulador local fica parado.
      if (r.driver) r.driver.advance(dtRaw * session.speed, readInputs(r), (i) => stepOnce(r, i));
      else accumulator += dtRaw * session.speed;
      let steps = 0;
      const inputs = readInputs(r);
      // O monitor rápido tem quadros sem passo (144 Hz: 2,4 quadros por passo): sem isto o aperto do nitro morria ali.
      for (const h of r.humans) {
        const held = r.heldEdges?.[h.seat]; const i = inputs[h.seat];
        if (held && i && i !== NEUTRAL_INPUT) inputs[h.seat] = { ...i, nitro: i.nitro || held.nitro, gearUp: i.gearUp || held.gearUp, gearDown: i.gearDown || held.gearDown };
      }
      while (accumulator >= DT && steps < MAX_STEPS_PER_FRAME * session.speed) {
        stepOnce(r, inputs);
        // As bordas (nitro, marcha) valem só no primeiro passo do quadro.
        for (const h of r.humans) { const i = inputs[h.seat]; if (i && i !== NEUTRAL_INPUT) inputs[h.seat] = { ...i, nitro: false, gearUp: false, gearDown: false }; }
        accumulator -= DT;
        steps++;
      }
      if (steps === MAX_STEPS_PER_FRAME * session.speed) accumulator = 0; // não tenta recuperar quadros perdidos
      r.heldEdges = [];
      if (steps === 0 && !r.driver) for (const h of r.humans) { const i = inputs[h.seat]; if (i && i !== NEUTRAL_INPUT && (i.nitro || i.gearUp || i.gearDown)) r.heldEdges[h.seat] = { nitro: i.nitro, gearUp: i.gearUp, gearDown: i.gearDown }; }
      for (const list of r.messages.values()) {
        for (let i = list.length - 1; i >= 0; i--) { list[i].ttl -= dtRaw; if (list[i].ttl <= 0) list.splice(i, 1); }
      }
      if (r.state.phase === 'finished' && menus.current() === null) {
        r.overFor += dtRaw;
        r.afterFlag = advanceAfterFlag(r.afterFlag, r.state, r.track, dtRaw * session.speed);
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

    // Só o menu que já estava aberto recebe as bordas deste quadro: a pausa (ou o resultado) que
    // acabou de abrir não pode receber o mesmo botão — com a pausa no A ou no B, ela fecharia na hora.
    if (menuOpen) menus.navigate(input.menuNav());
    menus.update(dtRaw);
  }

  function loop(now: number): void {
    if (!running) return;
    try { frame(now); } catch (err) { console.error(err); reportError(err, 'loop'); }
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
    Object.assign(settings, next, { assists: { ...next.assists }, seatAssists: [...next.seatAssists] });
    saveSettings(settings);
    applyAccessibility(settings, hudRoot, uiRoot);
    audio.setVolumes(settings.masterVolume, settings.musicVolume, settings.sfxVolume);
    if (languageChanged) { setLanguage(settings.language); menus.refreshLanguage(); }
    if (fullscreenChanged) setFullscreen(settings.fullscreen).catch(() => undefined);
    if (settings.renderStyle !== activeStyle) swapRenderer();
    if (!session.race) {
      audio.setMusic(settings.music === 'auto' ? songForScenery('coast', 'dusk') : settings.music === 'off' ? null : settings.music);
    } else if (settings.music !== 'auto') {
      audio.setMusic(settings.music === 'off' ? null : settings.music);
    }
  }

  function handleMenuEvent(e: MenuEvent): void {
    if (getActivePlaylog()?.askSurveyBefore(e, session.race !== null && session.race.state.phase !== 'finished')) { menus.show('survey'); return; }
    switch (e.type) {
      case 'startCup': startCup(e.cupId, e.humans); break;
      case 'startQuick': startQuick(e.trackId, e.laps, e.humans); break;
      case 'startTimeTrial': startQuick(e.trackId, settings.quickLaps, e.humans, true); break;
      case 'nextRace': if (session.race?.mode === 'career') career.showGarage(); else nextCupRace(); break;
      case 'continueCup': continueCup(); break;
      case 'startParty': champ = null; party.startMode(e.mode, e.trackId, e.laps, e.humans); break;
      case 'startTournament': champ = null; toIdle(); party.startTournament(e.setup, e.seats); break;
      case 'tournamentHeat': party.runHeat(); break;
      case 'startCareer': career.start(e.humans, e.resume); break;
      case 'careerRace': career.race(); break;
      case 'startTutorial': session.tutorial.start(e.humans); break;
      case 'tutorialFirstCup': session.tutorial.firstCup(); break;
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
