// Tutorial de 90 segundos na sessão: monta a corrida guiada (pista própria, sem IA, tanque baixo)
// e a conduz como um RaceDriver — o mesmo gancho que o online usa: o driver decide os ticks, passa
// as entradas pela máquina de passos (o carro parado do passo 6 fica no freio), lê o estado depois
// de cada tick e cuida do fim (concluído, pulado com Esc/Start ou sem tempo). A regra dos passos é
// pura e mora em tutorial.ts; aqui ficam os efeitos (HUD, painel, save, menus). Em session.ts ficam
// só os ganchos que chamam este módulo. Só local: o tutorial não existe no online.
import { DT } from '../core/constants';
import { CUPS } from '../core/data/cups';
import { createRace } from '../core/sim/race';
import type { HumanEntry, PlayerInput, RaceConfig, RaceState } from '../core/types';
import { NEUTRAL_INPUT } from '../core/types';
import { t } from '../i18n';
import '../tutorial/strings';
import type { BindAction, ControlBindings } from '../ui/remap/bindings';
import { actionLabel, buttonLabel, codesLabel, keyLabel, padStyleOf, type LayoutMap } from '../ui/remap/labels';
import { compactHumans, type SeatBinder } from './career-session';
import type { DeviceInfo, HudMessage, Menus, RaceDriver, SaveData, Settings } from './contracts';
import {
  newTutorial, skipTutorial, START_FUEL, TARGET_KMH, tutorialInput, tutorialView, updateTutorial,
  type TutorialEvent, type TutorialState, type TutorialView,
} from './tutorial';
import { TUTORIAL_LAPS, TUTORIAL_TRACK_ID, tutorialTrack } from './tutorial-track';

/** "Tutorial concluído!" fica na tela este tanto antes da tela final. */
export const END_DELAY_TICKS = 150;
/** Teto de ticks por quadro (quadro lento não vira uma rajada de simulação). */
const MAX_TICKS_PER_FRAME = 8;

/** Uma tecla/botão a mostrar no painel: o que faz e o que apertar. */
export interface PanelKey { label: string; keys: string }

/** O painel de um assento, já com os textos no idioma atual. */
export interface PanelSeat {
  seat: number;
  color: string;
  name: string;
  view: TutorialView;
  /** "PASSO 2/6" (vazio no fim). */
  counter: string;
  title: string;
  text: string;
  keys: PanelKey[];
  /** Aviso de repetir ("rápido demais…"); null sem aviso. */
  retry: string | null;
  /** "Esc: pular" / "Start: pular". */
  skip: string;
}

/** O painel DOM por cima do HUD (src/ui/screens/tutorial.ts); a sessão de teste passa null. */
export interface TutorialPanel {
  update(seats: PanelSeat[]): void;
  hide(): void;
  /** Mapa do layout do teclado do sistema, quando o navegador informa (AZERTY, ABNT2…). */
  layout(): LayoutMap | null;
}

export interface TutorialHost {
  save: SaveData;
  settings: Settings;
  menus: Pick<Menus, 'show'>;
  input: SeatBinder & { devices(): DeviceInfo[] };
  /** Começa a corrida com este driver e este estado (session.ts: beginRace com `net`). */
  beginRace(config: RaceConfig, humans: HumanEntry[], driver: RaceDriver, state: RaceState): void;
  /** Sai da corrida para o fundo dos menus, sem desligar os assentos (chama driver.dispose). */
  toIdle(): void;
  startCup(cupId: string, humans: HumanEntry[]): void;
  randomSeed(): number;
  /** Grava o save. */
  persist(): void;
  hud(seat: number, text: string, kind: HudMessage['kind'], ttl: number): void;
  panel: TutorialPanel | null;
}

export interface TutorialSession {
  /** "Como jogar" → Começar: corrida guiada com estes jogadores (assentos já ligados). */
  start(humans: HumanEntry[]): void;
  /** Tela final → primeira copa, com os mesmos jogadores. */
  firstCup(): void;
  /** Máquina da corrida guiada em curso (playtest); null fora dela. */
  current(): TutorialState | null;
}

// ───────────────────────────── Textos do painel ─────────────────────────────

/** Teclas/botões de uma ação no dispositivo do jogador, com o remapeamento das opções. */
export function actionKeys(device: string | null, action: BindAction, controls: ControlBindings, padName = '', layout: LayoutMap | null = null): string {
  if (device && device.startsWith('gp')) return codesLabel(controls.gamepad[action], padStyleOf(padName));
  return codesLabel(controls[device === 'kb2' ? 'kb2' : 'kb1'][action], 'xbox', layout);
}

function panelKeys(actions: readonly BindAction[], device: string | null, controls: ControlBindings, padName: string, layout: LayoutMap | null): PanelKey[] {
  const out: PanelKey[] = [];
  const pad = device !== null && device.startsWith('gp');
  for (const a of actions) {
    if (a === 'right' && actions.includes('left')) continue; // esquerda e direita num chip só
    if (a === 'left' && actions.includes('right')) {
      const keys = `${actionKeys(device, 'left', controls, padName, layout)} ${actionKeys(device, 'right', controls, padName, layout)}`;
      out.push({ label: t('tutorial.key.steer'), keys: pad ? `${t('tutorial.key.stick')} / ${keys}` : keys });
      continue;
    }
    out.push({ label: actionLabel(a), keys: actionKeys(device, a, controls, padName, layout) });
  }
  return out;
}

/** Monta o painel de um assento: passo, instrução, teclas do dispositivo dele e o aviso de repetir. */
export function describeSeat(
  view: TutorialView, human: Pick<HumanEntry, 'seat' | 'name' | 'color'>, towName: string,
  device: string | null, controls: ControlBindings, padName = '', layout: LayoutMap | null = null,
): PanelSeat {
  const pad = device !== null && device.startsWith('gp');
  const params = { kmh: TARGET_KMH, name: towName };
  let text = t(`tutorial.step.${view.step}.text`, params);
  if (view.step === 'tow' && !view.parked) text = t('tutorial.step.tow.before', params);
  if (view.step === 'stalled' && view.parked) text = t('tutorial.step.stalled.parked', params);
  return {
    seat: human.seat, color: human.color, name: human.name, view,
    counter: view.step === 'done' ? '' : t('tutorial.counter', { n: view.index, total: view.total }),
    title: t(`tutorial.step.${view.step}.title`),
    text,
    keys: panelKeys(view.actions, device, controls, padName, layout),
    retry: view.retry ? t(`tutorial.retry.${view.retry}`) : null,
    skip: view.step === 'done' ? '' : t('tutorial.skip', { key: pad ? buttonLabel(9, padStyleOf(padName)) : keyLabel('Escape') }),
  };
}

// ───────────────────────────── Sessão ─────────────────────────────

interface Run {
  tut: TutorialState;
  state: RaceState;
  humans: HumanEntry[];
  /** Tick em que a tela final aparece (depois do "concluído!"); −1 = ainda não. */
  endAt: number;
  over: boolean;
}

function withoutEdges(inputs: Array<PlayerInput | undefined>): Array<PlayerInput | undefined> {
  return inputs.map((i) => (i && i !== NEUTRAL_INPUT ? { ...i, nitro: false, gearUp: false, gearDown: false } : i));
}

export function createTutorialSession(host: TutorialHost): TutorialSession {
  let run: Run | null = null;
  let lastHumans: HumanEntry[] = [];

  function nameOf(r: Run, seat: number): string {
    return r.humans.find((h) => h.seat === seat)?.name ?? '?';
  }

  function finish(r: Run): void {
    if (r.over) return;
    r.over = true;
    const completed = r.tut.phase === 'done';
    if (r.tut.phase === 'running') skipTutorial(r.tut);
    host.save.tutorialDone = true;
    host.persist();
    host.toIdle();
    if (run === r) run = null;
    host.menus.show('tutorialDone', { completed, players: r.humans.length });
  }

  function handle(r: Run, events: readonly TutorialEvent[]): void {
    for (const e of events) {
      switch (e.type) {
        case 'stepDone': if (e.step !== 'tow') host.hud(e.seat, t('tutorial.ok'), 'good', 1.5); break;
        case 'retry': host.hud(e.seat, t(`tutorial.retry.${e.reason}`), 'warn', 3); break;
        case 'noNitro': host.hud(e.seat, t('tutorial.noNitro'), 'warn', 2.5); break;
        case 'parked':
          for (const h of r.humans) {
            if (h.seat === e.seat) host.hud(h.seat, t('tutorial.parkedYou'), 'warn', 3);
            else host.hud(h.seat, t('tutorial.parkedMate', { name: nameOf(r, e.seat) }), 'info', 3);
          }
          break;
        case 'towed': if (e.bySeat >= 0) host.hud(e.bySeat, t('tutorial.towGiven'), 'good', 2); break;
        case 'towMissed': host.hud(e.seat, t('tutorial.towMissed'), 'warn', 3); break;
        case 'done':
          for (const h of r.humans) host.hud(h.seat, t('tutorial.finished'), 'big', END_DELAY_TICKS * DT);
          r.endAt = r.state.tick + END_DELAY_TICKS;
          break;
        case 'timeout': finish(r); return;
      }
    }
  }

  function showPanel(r: Run): void {
    const panel = host.panel;
    if (!panel) return;
    const devices = host.input.devices();
    const layout = panel.layout();
    const towName = r.tut.towSeat >= 0 ? nameOf(r, r.tut.towSeat) : '';
    panel.update(r.humans.slice().sort((a, b) => a.seat - b.seat).map((h) => {
      const device = host.input.seatDevice(h.seat);
      const padName = devices.find((d) => d.id === device)?.label ?? '';
      return describeSeat(tutorialView(r.tut, r.state, h.seat), h, towName, device, host.settings.controls, padName, layout);
    }));
  }

  function driverFor(r: Run): RaceDriver {
    let acc = 0;
    const tick = (inputs: Array<PlayerInput | undefined>, step: (inputs: PlayerInput[]) => void): void => {
      const cmd: PlayerInput[] = [];
      for (const h of r.humans) cmd[h.seat] = tutorialInput(r.tut, h.seat, inputs[h.seat] ?? NEUTRAL_INPUT);
      step(cmd);
      handle(r, updateTutorial(r.tut, r.state));
      if (!r.over && r.endAt >= 0 && r.state.tick >= r.endAt) finish(r);
    };
    return {
      advance(dt, local, step) {
        if (r.over) return 0;
        acc += dt;
        let inputs: Array<PlayerInput | undefined> = local.slice();
        let n = 0;
        while (acc >= DT && n < MAX_TICKS_PER_FRAME && !r.over) {
          tick(inputs, step);
          inputs = withoutEdges(inputs); // nitro e marcha valem só no primeiro tick do quadro
          acc -= DT;
          n++;
        }
        if (n >= MAX_TICKS_PER_FRAME) acc = 0;
        if (!r.over) showPanel(r);
        return n;
      },
      force(ticks, local, step) {
        let inputs: Array<PlayerInput | undefined> = local.slice();
        let n = 0;
        for (; n < ticks && !r.over; n++) { tick(inputs, step); inputs = withoutEdges(inputs); }
        if (!r.over) showPanel(r);
        return n;
      },
      // Esc/Start: pular. A tela final mostra as regras de ouro.
      pauseKey() { skipTutorial(r.tut); finish(r); },
      // A corrida acabou antes do tutorial (não deveria: ele encerra quando alguém começa a última volta).
      finished() { finish(r); },
      dispose() { r.over = true; if (run === r) run = null; host.panel?.hide(); },
    };
  }

  function start(humans: HumanEntry[]): void {
    // Co-op sempre (o empurrão é entre companheiros), carros de fábrica, assentos contíguos.
    const hs = compactHumans(host.input, humans).map((h): HumanEntry => ({ seat: h.seat, name: h.name, carId: h.carId, teamId: 0, color: h.color }));
    if (hs.length === 0) return;
    lastHumans = hs;
    const config: RaceConfig = {
      trackId: TUTORIAL_TRACK_ID, laps: TUTORIAL_LAPS, humans: hs, totalCars: hs.length,
      difficulty: host.settings.difficulty, manualGear: false,
      assists: { sharedNitro: true, tow: true, teamDraft: true, catchup: true }, seed: host.randomSeed(),
    };
    const state = createRace(config, tutorialTrack());
    for (const c of state.cars) if (c.seat >= 0) c.fuel = START_FUEL; // o passo 5 é abastecer
    const r: Run = { tut: newTutorial(hs.map((h) => h.seat)), state, humans: hs, endAt: -1, over: false };
    run = r;
    host.beginRace(config, hs, driverFor(r), state);
    showPanel(r);
  }

  function firstCup(): void {
    const cup = CUPS[0];
    if (cup && lastHumans.length > 0) host.startCup(cup.id, lastHumans);
  }

  return { start, firstCup, current: () => run?.tut ?? null };
}
