// Telas do tutorial de 90 segundos: "Como jogar" (quem vai jogar entra, como no lobby, e começa),
// a tela final (parabéns ou só as regras de ouro, com o botão para a primeira copa), o cartão que
// oferece o tutorial no menu principal enquanto o jogador nunca correu, e o painel da corrida
// guiada por cima do HUD — um cartão por viewport, na mesma divisão de tela do renderizador.
import './tutorial.css';
import '../../tutorial/strings';
import { seatColor } from '../../core/data/drivers';
import type { HumanEntry } from '../../core/types';
import type { DeviceId, MenuContext, MenuNav, SaveData, TutorialDoneData } from '../../game/contracts';
import type { PanelSeat, TutorialPanel } from '../../game/tutorial-session';
import { t } from '../../i18n';
import { uiScale, viewportRects } from '../../render/layout';
import { isKeyboard } from '../input';
import type { LayoutMap } from '../remap/labels';
import { button, createFocusList, h, listNav, screenFrame, type ScreenApi, type ScreenData, type ScreenInstance } from './common';
import { icon } from './icons';
import { availableCars, LOBBY_SEATS } from './lobby';

/** O menu principal oferece o tutorial enquanto o jogador não o fez (nem pulou) e nunca correu. */
export function shouldOfferTutorial(save: Pick<SaveData, 'tutorialDone' | 'racesRun'>): boolean {
  return !save.tutorialDone && save.racesRun === 0;
}

/** Jogadores do tutorial: nome e carro que o lobby lembra de cada assento, todos na mesma equipe. */
export function tutorialHumans(ctx: Pick<MenuContext, 'cars' | 'save' | 'settings'>, seats: readonly number[]): HumanEntry[] {
  const cars = availableCars(ctx);
  return seats.slice().sort((a, b) => a - b).map((seat) => {
    const saved = ctx.save.seatCars[seat];
    const car = cars.find((c) => c.id === saved) ?? cars[0];
    return { seat, name: ctx.save.seatNames[seat] ?? `P${seat + 1}`, carId: car ? car.id : 'falcao', teamId: 0, color: seatColor(seat, ctx.settings.colorPalette) };
  });
}

/** Cartão "Primeira vez?" do menu principal; clicar abre "Como jogar". */
export function tutorialOfferCard(api: ScreenApi): HTMLElement {
  const card = h('div', { class: 'tut-offer glass' },
    h('span', { class: 'tut-offer-badge', text: t('tutorial.offer.badge') }),
    h('strong', { class: 'tut-offer-title', text: t('tutorial.offer.title') }),
    h('span', { class: 'tut-offer-text', text: t('tutorial.offer.text') }),
  );
  card.addEventListener('click', () => { api.sfx('confirm'); api.go('tutorial'); });
  return card;
}

// ───────────────────────────── Como jogar ─────────────────────────────

export function tutorialScreen(api: ScreenApi): ScreenInstance {
  const { ctx } = api;
  const { input } = ctx;
  const seatOf = (device: DeviceId): number => {
    for (let s = 0; s < LOBBY_SEATS; s++) if (input.seatDevice(s) === device) return s;
    return -1;
  };
  const seated = (): number[] => {
    const out: number[] = [];
    for (let s = 0; s < LOBBY_SEATS; s++) if (input.seatDevice(s)) out.push(s);
    return out;
  };
  const deviceLabel = (device: string) => input.devices().find((d) => d.id === device)?.label ?? device;

  const start = () => {
    const seats = seated();
    if (seats.length === 0) { api.sfx('back'); return; }
    api.sfx('confirm');
    api.emit({ type: 'startTutorial', humans: tutorialHumans(ctx, seats) });
  };
  const leaveScreen = () => { api.sfx('back'); api.back(); };

  const slots = h('div', { class: 'tut-slots' });
  const startBtn = button(t('tutorial.join.start'), start, 'btn-primary btn-start');
  const backBtn = button(t('ui.common.back'), leaveScreen);
  const list = createFocusList([startBtn, backBtn], { sfx: api.sfx });
  const help = h('p', { class: 'hint' });

  function render(): void {
    const seats = seated();
    slots.replaceChildren(...Array.from({ length: LOBBY_SEATS }, (_, seat) => {
      const device = input.seatDevice(seat);
      const color = seatColor(seat, ctx.settings.colorPalette);
      if (!device) {
        return h('div', { class: 'tut-slot empty glass', style: `--seat:${color}` },
          h('span', { class: 'seat-badge', text: `P${seat + 1}` }),
          h('span', { class: 'tut-slot-text', text: t('tutorial.join.empty') }),
        );
      }
      return h('div', { class: 'tut-slot on glass', style: `--seat:${color}` },
        h('span', { class: 'seat-badge', text: `P${seat + 1}` }),
        h('strong', { class: 'tut-slot-name', text: ctx.save.seatNames[seat] ?? `P${seat + 1}` }),
        h('span', { class: 'slot-device' }, icon(isKeyboard(device as DeviceId) ? 'keyboard' : 'gamepad'), h('span', { text: deviceLabel(device) })),
      );
    }));
    startBtn.disabled = seats.length === 0;
    startBtn.el.classList.toggle('disabled', seats.length === 0);
    help.textContent = seats.length === 0 ? t('tutorial.join.need') : t('tutorial.join.leave');
  }

  const el = screenFrame('tutorial', t('tutorial.join.title'),
    h('p', { class: 'tut-sub', text: t('tutorial.join.sub') }),
    h('div', { class: 'tut-join glass' },
      h('p', { class: 'tut-join-hint' }, icon('keyboard'), icon('gamepad'), h('span', { text: t('tutorial.join.hint') })),
      slots,
      h('p', { class: 'hint', text: t('tutorial.join.coop') }),
      help,
      h('div', { class: 'actions' }, startBtn.el, backBtn.el),
    ),
  );
  render();
  // A borda que abriu a tela (Enter no menu) ainda está fechada no primeiro quadro: não entra ninguém nela.
  let armed = false;

  return {
    el,
    nav(nav: MenuNav) {
      const device = nav.device;
      if (!device) return;
      if (seatOf(device) < 0) {
        // Sem assento: confirmar é entrar (update), e voltar só sai da tela com ela vazia.
        if (nav.back && seated().length === 0) leaveScreen();
        return;
      }
      if (nav.start) { start(); return; }
      // Voltar de quem tem assento é sair do assento (leavePressed, no update).
      listNav(list, { ...nav, back: false }, api.sfx);
    },
    update() {
      if (!armed) { armed = true; return; }
      const joining = input.joinPressed();
      if (joining) {
        const free = Array.from({ length: LOBBY_SEATS }, (_, s) => s).find((s) => !input.seatDevice(s));
        if (free !== undefined) { input.bindSeat(free, joining); api.sfx('confirm'); render(); }
      }
      const leaving = input.leavePressed();
      if (leaving) {
        const seat = seatOf(leaving);
        if (seat >= 0) { input.unbindSeat(seat); api.sfx('back'); render(); }
      }
    },
  };
}

// ───────────────────────────── Tela final ─────────────────────────────

function isDoneData(data: ScreenData): data is TutorialDoneData {
  return typeof data === 'object' && data !== null && 'completed' in data && 'players' in data;
}

export function tutorialDoneScreen(api: ScreenApi, data?: ScreenData): ScreenInstance {
  const info: TutorialDoneData = isDoneData(data) ? data : { completed: false, players: 1 };
  const cup = api.ctx.cups[0];
  const toMain = () => { api.sfx('back'); api.emit({ type: 'toMain' }); };
  const items = [
    ...(cup ? [button(t('tutorial.done.cup', { cup: t(`core.cup.${cup.id}`) }), () => api.emit({ type: 'tutorialFirstCup' }), 'btn-primary')] : []),
    button(t('tutorial.done.main'), toMain),
  ];
  const list = createFocusList(items, { sfx: api.sfx });
  const rules = ['brake', 'grass', 'nitro', 'pit', ...(info.players >= 2 ? ['tow'] : [])];
  const el = screenFrame('tutorialDone', null,
    h('div', { class: `tut-done glass${info.completed ? ' completed' : ''}` },
      info.completed ? h('span', { class: 'tut-done-icon' }, icon('trophy')) : null,
      h('h1', { class: 'screen-title', text: t(info.completed ? 'tutorial.done.title' : 'tutorial.done.skippedTitle') }),
      h('p', { class: 'tut-sub', text: t(info.completed ? 'tutorial.done.sub' : 'tutorial.done.skippedSub') }),
      h('ol', { class: 'tut-rules' }, rules.map((r) => h('li', { text: t(`tutorial.rule.${r}`) }))),
      h('div', { class: 'actions' }, items.map((i) => i.el)),
    ),
  );
  return { el, nav: (nav) => listNav(list, nav, api.sfx, toMain) };
}

// ───────────────────────────── Painel da corrida ─────────────────────────────

interface Card {
  vp: HTMLElement;
  counter: HTMLElement;
  dotsEl: HTMLElement;
  dots: HTMLElement[];
  title: HTMLElement;
  text: HTMLElement;
  bar: HTMLElement;
  fill: HTMLElement;
  keys: HTMLElement;
  retry: HTMLElement;
  skip: HTMLElement;
  keysSig: string;
}

function setText(el: HTMLElement, text: string): void {
  if (el.textContent !== text) el.textContent = text;
}

let layoutMap: LayoutMap | null = null;
let layoutAsked = false;

/** Mapa do layout do teclado do sistema (Chromium/Electron), pedido uma vez; null sem suporte. */
function askLayout(): void {
  if (layoutAsked) return;
  layoutAsked = true;
  const kb = typeof navigator === 'undefined' ? undefined : (navigator as Navigator & { keyboard?: { getLayoutMap?: () => Promise<LayoutMap> } }).keyboard;
  if (kb && typeof kb.getLayoutMap === 'function') kb.getLayoutMap().then((m) => { layoutMap = m; }).catch(() => undefined);
}

/**
 * Painel do tutorial dentro de `root` (o #hud): um cartão por jogador, no viewport dele. O DOM só
 * nasce no primeiro uso — a sessão cria o painel sempre, inclusive nos testes em Node.
 */
export function createTutorialPanel(root: HTMLElement): TutorialPanel {
  let el: HTMLElement | null = null;
  let cards: Card[] = [];

  function build(count: number): void {
    if (!el) { askLayout(); el = h('div', { class: 'nc-tut hidden' }); root.appendChild(el); }
    const host = el;
    host.replaceChildren();
    cards = Array.from({ length: count }, () => {
      const counter = h('span', { class: 'tut-counter' });
      const dotsEl = h('span', { class: 'tut-dots' });
      const title = h('div', { class: 'tut-title' });
      const text = h('div', { class: 'tut-text' });
      const fill = h('i');
      const bar = h('div', { class: 'tut-bar' }, fill);
      const keys = h('div', { class: 'tut-keys' });
      const retry = h('div', { class: 'tut-retry' });
      const skip = h('div', { class: 'tut-skip' });
      // Compacto (3 linhas): o cartão fica embaixo, entre o combustível e o velocímetro, sem cobrir o carro;
      // o aviso de repetir flutua logo acima dele.
      const vp = h('div', { class: 'tut-vp' },
        h('div', { class: 'tut-card' },
          retry,
          h('div', { class: 'tut-head' }, counter, title, dotsEl),
          text, bar,
          h('div', { class: 'tut-foot' }, keys, skip),
        ),
      );
      host.appendChild(vp);
      return { vp, counter, dotsEl, dots: [], title, text, bar, fill, keys, retry, skip, keysSig: '' };
    });
  }

  return {
    update(seats: PanelSeat[]) {
      if (!el || seats.length !== cards.length) build(seats.length);
      el?.classList.remove('hidden');
      const rects = viewportRects(seats.length, window.innerWidth, window.innerHeight);
      seats.forEach((s, i) => {
        const c = cards[i];
        const rect = rects[i];
        if (!c || !rect) return;
        const st = c.vp.style;
        st.left = `${rect.x}px`; st.top = `${rect.y}px`; st.width = `${rect.w}px`; st.height = `${rect.h}px`;
        st.setProperty('--s', uiScale(rect).toFixed(3));
        st.setProperty('--seat', s.color);
        c.vp.dataset.step = s.view.step;
        c.vp.classList.toggle('done', s.view.step === 'done');
        setText(c.counter, s.counter);
        if (c.dots.length !== s.view.total) {
          c.dots = Array.from({ length: s.view.total }, () => h('i'));
          c.dotsEl.replaceChildren(...c.dots);
        }
        c.dots.forEach((d, k) => { d.className = k + 1 < s.view.index || s.view.step === 'done' ? 'on' : k + 1 === s.view.index ? 'now' : ''; });
        setText(c.title, s.title);
        setText(c.text, s.text);
        c.bar.classList.toggle('show', s.view.progress !== null && s.view.step !== 'done');
        c.fill.style.width = `${Math.round((s.view.progress ?? 0) * 100)}%`;
        const sig = s.keys.map((k) => `${k.label}=${k.keys}`).join('|');
        if (sig !== c.keysSig) {
          c.keysSig = sig;
          c.keys.replaceChildren(...s.keys.map((k) => h('span', { class: 'tut-key' }, h('kbd', { text: k.keys }), h('span', { text: k.label }))));
        }
        setText(c.retry, s.retry ?? '');
        c.retry.classList.toggle('show', s.retry !== null);
        setText(c.skip, s.skip);
      });
    },
    hide() { el?.classList.add('hidden'); },
    layout: () => layoutMap,
  };
}
