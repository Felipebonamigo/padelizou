// Opções: cada mudança grava no localStorage e avisa a sessão (`settingsChanged`).
import type { CoopAssists } from '../../core/types';
import type { Settings } from '../../game/contracts';
import { DIFFICULTIES, QUALITIES, QUICK_LAPS_MAX, QUICK_LAPS_MIN, TOTAL_CARS_MAX, TOTAL_CARS_MIN, saveSettings } from '../../game/settings';
import { getLanguage, setLanguage, t, type Lang } from '../../i18n';
import { button, createFocusList, h, listNav, onOff, screenFrame, selector, type FocusItem, type ScreenApi, type ScreenInstance, type Selector } from './common';

const ASSIST_KEYS: ReadonlyArray<keyof CoopAssists> = ['sharedNitro', 'tow', 'teamDraft', 'catchup'];

function cycle<T>(list: readonly T[], current: T, dir: -1 | 1): T {
  const i = list.indexOf(current);
  return list[(i + dir + list.length) % list.length];
}

function stepNumber(v: number, dir: -1 | 1, min: number, max: number, step: number): number {
  return Math.min(max, Math.max(min, Math.round((v + dir * step) / step) * step));
}

export function percent(v: number): string {
  return `${Math.round(v * 100)}%`;
}

/** Aplica a tela cheia pelo DOM (precisa de gesto do usuário; o Electron também aceita). */
function applyFullscreen(on: boolean): void {
  if (typeof document === 'undefined') return;
  try {
    if (on && !document.fullscreenElement) void document.documentElement.requestFullscreen?.();
    else if (!on && document.fullscreenElement) void document.exitFullscreen?.();
  } catch { /* navegador sem suporte ou fora de gesto */ }
}

/** Constrói os seletores compartilhados com o lobby (dificuldade, câmbio, carros, voltas, assistências). */
export function raceOptionSelectors(api: ScreenApi, commit: () => void, opts: { difficulty?: boolean; gear?: boolean; totalCars?: boolean; quickLaps?: boolean; assists?: boolean }): Selector[] {
  const s = api.ctx.settings;
  const out: Selector[] = [];
  const sfx = api.sfx;
  if (opts.difficulty) out.push(selector(t('ui.options.difficulty'), () => t(`core.difficulty.${s.difficulty}`), (d) => { s.difficulty = cycle(DIFFICULTIES, s.difficulty, d); commit(); }, { sfx }));
  if (opts.gear) out.push(selector(t('ui.options.gear'), () => (s.manualGear ? t('ui.options.gear.manual') : t('ui.options.gear.auto')), () => { s.manualGear = !s.manualGear; commit(); }, { sfx }));
  if (opts.totalCars) out.push(selector(t('ui.options.totalCars'), () => String(s.totalCars), (d) => { s.totalCars = stepNumber(s.totalCars, d, TOTAL_CARS_MIN, TOTAL_CARS_MAX, 1); commit(); }, { sfx }));
  if (opts.quickLaps) out.push(selector(t('ui.options.quickLaps'), () => String(s.quickLaps), (d) => { s.quickLaps = stepNumber(s.quickLaps, d, QUICK_LAPS_MIN, QUICK_LAPS_MAX, 1); commit(); }, { sfx }));
  if (opts.assists) {
    for (const key of ASSIST_KEYS) {
      out.push(selector(t(`ui.options.assist.${key}`), () => onOff(s.assists[key]), () => { s.assists[key] = !s.assists[key]; commit(); }, { sfx }));
    }
  }
  return out;
}

export function commitSettings(api: ScreenApi): void {
  saveSettings(api.ctx.settings);
  api.emit({ type: 'settingsChanged', settings: api.ctx.settings });
}

export function optionsScreen(api: ScreenApi): ScreenInstance {
  const s: Settings = api.ctx.settings;
  const sfx = api.sfx;
  const commit = () => commitSettings(api);
  const musicIds = ['auto', ...api.ctx.audio.musicList().map((m) => m.id)];
  const musicTitle = (id: string) => (id === 'auto' ? t('ui.options.musicAuto') : (api.ctx.audio.musicList().find((m) => m.id === id)?.title ?? id));
  if (!musicIds.includes(s.music)) s.music = 'auto';

  const volume = (label: string, get: () => number, set: (v: number) => void) =>
    selector(label, () => percent(get()), (d) => { set(stepNumber(get(), d, 0, 1, 0.1)); commit(); }, { sfx });

  const general: FocusItem[] = [
    selector(t('ui.options.language'), () => (getLanguage() === 'pt' ? 'Português' : 'English'), () => {
      const lang: Lang = getLanguage() === 'pt' ? 'en' : 'pt';
      s.language = lang;
      setLanguage(lang);
      commit();
      api.refresh();
    }, { sfx }),
    volume(t('ui.options.master'), () => s.masterVolume, (v) => { s.masterVolume = v; }),
    volume(t('ui.options.music'), () => s.musicVolume, (v) => { s.musicVolume = v; }),
    volume(t('ui.options.sfx'), () => s.sfxVolume, (v) => { s.sfxVolume = v; }),
    // No Electron a sessão aplica pelo desktop.ts (settingsChanged); no navegador só um gesto do
    // usuário pode pedir tela cheia, e este handler de teclado/clique é o gesto.
    selector(t('ui.options.fullscreen'), () => onOff(s.fullscreen), () => { s.fullscreen = !s.fullscreen; if (!api.ctx.isDesktop) applyFullscreen(s.fullscreen); commit(); }, { sfx }),
    selector(t('ui.options.quality'), () => t(`ui.options.quality.${s.quality}`), (d) => { s.quality = cycle(QUALITIES, s.quality, d); commit(); }, { sfx }),
    selector(t('ui.options.minimap'), () => onOff(s.showMinimap), () => { s.showMinimap = !s.showMinimap; commit(); }, { sfx }),
    selector(t('ui.options.shake'), () => onOff(s.screenShake), () => { s.screenShake = !s.screenShake; commit(); }, { sfx }),
    selector(t('ui.options.track'), () => musicTitle(s.music), (d) => { s.music = cycle(musicIds, s.music, d); commit(); }, { sfx }),
  ];
  const race = raceOptionSelectors(api, commit, { difficulty: true, gear: true, totalCars: true, quickLaps: true, assists: true });
  const back = button(t('ui.common.back'), () => api.back());
  const list = createFocusList([...general, ...race, back], { sfx });
  const el = screenFrame('options', t('ui.options.title'),
    h('div', { class: 'options-columns' },
      h('div', { class: 'options-col' }, h('h2', { class: 'sub-title', text: t('ui.options.general') }), general.map((i) => i.el)),
      h('div', { class: 'options-col' }, h('h2', { class: 'sub-title', text: t('ui.options.race') }), race.map((i) => i.el)),
    ),
    back.el,
  );
  return { el, nav: (nav) => listNav(list, nav, sfx, () => api.back()) };
}
