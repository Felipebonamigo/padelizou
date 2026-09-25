// Menus em DOM dentro de `ctx.root` (#ui): uma tela por vez, com histórico para "voltar".
// Teclado e mouse são tratados aqui mesmo; o gamepad chega por `navigate()` da sessão.
import type { DeviceId, MenuContext, MenuNav, MenuScreen, Menus } from '../game/contracts';
import { isKeyboard, isEditableTarget } from './input';
import { type LobbyState, type ScreenApi, type ScreenData, type ScreenFactory, type ScreenInstance } from './screens/common';
import { controlsScreen } from './screens/controls';
import { recordsScreen } from './screens/info';
import { lobbyScreen } from './screens/lobby';
import { optionsScreen } from './screens/options';
import { resultsScreen, standingsScreen } from './screens/results';
import { cupsScreen, tracksScreen } from './screens/select';
import { creditsScreen, loadingScreen, mainScreen, pauseScreen, titleScreen } from './screens/simple';
import './strings';

/** Tecla física → borda de navegação e o "dispositivo" de teclado a que pertence. */
export const KEY_NAV: Readonly<Record<string, { key: keyof Omit<MenuNav, 'device'>; device: DeviceId }>> = {
  ArrowUp: { key: 'up', device: 'kb1' }, ArrowDown: { key: 'down', device: 'kb1' },
  ArrowLeft: { key: 'left', device: 'kb1' }, ArrowRight: { key: 'right', device: 'kb1' },
  Enter: { key: 'confirm', device: 'kb1' }, NumpadEnter: { key: 'confirm', device: 'kb1' }, Space: { key: 'confirm', device: 'kb1' },
  Escape: { key: 'back', device: 'kb1' }, Backspace: { key: 'back', device: 'kb1' },
  KeyW: { key: 'up', device: 'kb2' }, KeyS: { key: 'down', device: 'kb2' },
  KeyA: { key: 'left', device: 'kb2' }, KeyD: { key: 'right', device: 'kb2' },
  KeyF: { key: 'confirm', device: 'kb2' },
};

export function emptyNav(device: DeviceId | null = null): MenuNav {
  return { up: false, down: false, left: false, right: false, confirm: false, back: false, start: false, device };
}

export function hasEdge(nav: MenuNav): boolean {
  return nav.up || nav.down || nav.left || nav.right || nav.confirm || nav.back || nav.start;
}

const FACTORIES: Readonly<Record<MenuScreen, ScreenFactory>> = {
  title: titleScreen,
  main: mainScreen,
  lobby: lobbyScreen,
  cups: cupsScreen,
  tracks: tracksScreen,
  results: resultsScreen,
  standings: standingsScreen,
  pause: pauseScreen,
  options: optionsScreen,
  controls: controlsScreen,
  records: recordsScreen,
  credits: creditsScreen,
  loading: loadingScreen,
};

export function createMenus(ctx: MenuContext): Menus {
  const root = ctx.root;
  root.classList.add('nc-ui');
  const lobby: LobbyState = { mode: 'quick', versus: false, seats: [null, null, null, null] };

  let current: MenuScreen | null = null;
  let currentData: ScreenData;
  let instance: ScreenInstance | null = null;
  const history: Array<{ screen: MenuScreen; data: ScreenData }> = [];

  function unmount(): void {
    instance?.destroy?.();
    instance = null;
    root.replaceChildren();
  }

  function mount(screen: MenuScreen, data: ScreenData): void {
    unmount();
    current = screen;
    currentData = data;
    instance = FACTORIES[screen](api, data);
    root.appendChild(instance.el);
    root.classList.add('open');
    root.dataset.screen = screen;
  }

  const api: ScreenApi = {
    ctx,
    lobby,
    emit: (event) => ctx.onEvent(event),
    sfx: (kind) => ctx.audio.ui(kind),
    go(screen, data) {
      if (current) history.push({ screen: current, data: currentData });
      mount(screen, data);
    },
    back() {
      const prev = history.pop();
      if (prev) mount(prev.screen, prev.data);
      else mount('main', undefined);
    },
    refresh() {
      if (current) mount(current, currentData);
    },
  };

  const onKeyDown = (e: KeyboardEvent) => {
    if (!instance) return;
    if (e.ctrlKey || e.metaKey || e.altKey) return;
    ctx.audio.unlock();
    if (isEditableTarget(e.target)) {
      // Digitando o nome no lobby: Enter/Esc encerram a edição; o resto é texto.
      if (e.code === 'Enter' || e.code === 'NumpadEnter' || e.code === 'Escape') {
        (e.target as HTMLElement).blur();
        e.preventDefault();
      }
      return;
    }
    const m = KEY_NAV[e.code];
    if (!m) return;
    e.preventDefault();
    const nav = emptyNav(m.device);
    nav[m.key] = true;
    instance.nav(nav);
  };
  const onPointerDown = () => { ctx.audio.unlock(); };
  document.addEventListener('keydown', onKeyDown);
  root.addEventListener('pointerdown', onPointerDown);

  const menus: Menus = {
    show(screen: MenuScreen, data?: ScreenData) {
      history.length = 0;
      mount(screen, data);
    },
    hide() {
      unmount();
      history.length = 0;
      current = null;
      currentData = undefined;
      root.classList.remove('open');
      delete root.dataset.screen;
    },
    current: () => current,
    navigate(nav: MenuNav) {
      // O teclado já chegou pelo keydown; aqui só o que o DOM não vê (gamepads).
      if (!instance || !nav.device || isKeyboard(nav.device) || !hasEdge(nav)) return;
      instance.nav(nav);
    },
    update(dt: number) {
      instance?.update?.(dt);
    },
    refreshLanguage() {
      if (typeof document !== 'undefined') document.documentElement.lang = ctx.settings.language === 'pt' ? 'pt-BR' : 'en';
      api.refresh();
    },
  };
  return menus;
}
