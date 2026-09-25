// Infraestrutura comum das telas: construção de DOM, lista de foco (um cursor por tela,
// navegável por teclado, gamepad e mouse), seletores ‹ valor › e miniaturas de pista.
import { getTrack } from '../../core/track';
import type { CarDef, TimeOfDay, TrackDef } from '../../core/types';
import type { DeviceId, MenuContext, MenuEvent, MenuNav, MenuScreen, RaceMode, ResultsScreenData, StandingsScreenData } from '../../game/contracts';
import { t } from '../../i18n';
import { carSilhouette, icon } from './icons';

// ───────────────────────────── Estado compartilhado do lobby ─────────────────────────────

export interface LobbySeat {
  seat: number;
  device: DeviceId;
  name: string;
  carIndex: number;
  ready: boolean;
  /** Índice do cursor deste assento (cada jogador navega o próprio slot). */
  cursor: number;
}

export interface LobbyState {
  mode: RaceMode;
  versus: boolean;
  seats: Array<LobbySeat | null>;
}

export type ScreenData = ResultsScreenData | StandingsScreenData | undefined;

/** O que cada tela recebe do `createMenus`. */
export interface ScreenApi {
  ctx: MenuContext;
  lobby: LobbyState;
  /** Abre outra tela guardando a atual no histórico (para `back()`). */
  go(screen: MenuScreen, data?: ScreenData): void;
  /** Volta para a tela anterior do histórico; sem histórico, vai para o menu principal. */
  back(): void;
  emit(event: MenuEvent): void;
  sfx(kind: 'move' | 'confirm' | 'back'): void;
  /** Reconstrói a tela atual (troca de idioma). */
  refresh(): void;
}

export interface ScreenInstance {
  el: HTMLElement;
  nav(nav: MenuNav): void;
  update?(dt: number): void;
  destroy?(): void;
}

export type ScreenFactory = (api: ScreenApi, data?: ScreenData) => ScreenInstance;

// ───────────────────────────── DOM ─────────────────────────────

export type Child = Node | string | number | null | undefined | false | Child[];

export interface Attrs {
  class?: string;
  text?: string;
  title?: string;
  style?: string;
  attrs?: Record<string, string>;
  on?: { [K in keyof HTMLElementEventMap]?: (ev: HTMLElementEventMap[K]) => void };
}

export function h<K extends keyof HTMLElementTagNameMap>(tag: K, attrs: Attrs = {}, ...children: Child[]): HTMLElementTagNameMap[K] {
  const el = document.createElement(tag);
  if (attrs.class) el.className = attrs.class;
  if (attrs.text !== undefined) el.textContent = attrs.text;
  if (attrs.title) el.title = attrs.title;
  if (attrs.style) el.setAttribute('style', attrs.style);
  if (attrs.attrs) for (const [k, v] of Object.entries(attrs.attrs)) el.setAttribute(k, v);
  if (attrs.on) {
    for (const [name, fn] of Object.entries(attrs.on)) {
      if (fn) el.addEventListener(name, fn as EventListener);
    }
  }
  append(el, children);
  return el;
}

export function append(el: HTMLElement, children: Child[]): void {
  for (const c of children) {
    if (c === null || c === undefined || c === false) continue;
    if (Array.isArray(c)) append(el, c);
    else if (typeof c === 'string' || typeof c === 'number') el.appendChild(document.createTextNode(String(c)));
    else el.appendChild(c);
  }
}

export function screenFrame(name: string, title: string | null, ...children: Child[]): HTMLElement {
  return h('section', { class: `screen scr-${name}`, attrs: { 'data-screen': name } },
    title === null ? null : h('h1', { class: 'screen-title', text: title }),
    ...children,
  );
}

/** Tira o foco nativo de um botão clicado: senão o Enter seguinte dispara um clique nativo além do nosso. */
export function blurActive(): void {
  const active = document.activeElement;
  if (active instanceof HTMLElement && !isTextField(active)) active.blur();
}

export function isTextField(el: Element | null): boolean {
  return el instanceof HTMLInputElement || el instanceof HTMLTextAreaElement;
}

// ───────────────────────────── Lista de foco ─────────────────────────────

export type NavDir = 'up' | 'down' | 'left' | 'right';

export interface FocusItem {
  el: HTMLElement;
  activate?: () => void;
  /** ← → mudam o valor em vez de mover o cursor. */
  adjust?: (dir: -1 | 1) => void;
  disabled?: boolean;
}

export interface FocusList {
  readonly items: FocusItem[];
  index: number;
  set(i: number): boolean;
  current(): FocusItem | null;
  move(dir: NavDir): boolean;
  activate(): boolean;
  adjust(dir: -1 | 1): boolean;
}

export interface FocusListOptions {
  /** Colunas da grade; 1 = lista vertical (com volta nas pontas). */
  cols?: number;
  start?: number;
  sfx?: ScreenApi['sfx'];
}

/**
 * Última posição do ponteiro vista por um item. O Chrome sintetiza eventos de mouse quando o DOM
 * muda debaixo de um ponteiro parado (o lobby se redesenha a cada entrada/saída): sem esta
 * comparação, um mouse esquecido sobre a tela roubaria o cursor de quem joga de controle.
 */
let lastPointer = { x: Number.NaN, y: Number.NaN };

export function pointerMoved(ev: MouseEvent): boolean {
  if (ev.clientX === lastPointer.x && ev.clientY === lastPointer.y) return false;
  lastPointer = { x: ev.clientX, y: ev.clientY };
  return true;
}

export function createFocusList(items: FocusItem[], opts: FocusListOptions = {}): FocusList {
  const cols = Math.max(1, opts.cols ?? 1);
  const sfx = opts.sfx ?? (() => undefined);
  const list: FocusList = {
    items,
    index: -1,
    set(i) {
      if (items.length === 0) return false;
      const next = Math.min(items.length - 1, Math.max(0, i));
      if (next === list.index) return false;
      if (list.index >= 0) items[list.index]?.el.classList.remove('focus');
      list.index = next;
      const el = items[next].el;
      el.classList.add('focus');
      if (typeof el.scrollIntoView === 'function') {
        try { el.scrollIntoView({ block: 'nearest', inline: 'nearest' }); } catch { /* jsdom/antigos */ }
      }
      return true;
    },
    current() {
      return list.index >= 0 ? items[list.index] ?? null : null;
    },
    move(dir) {
      const n = items.length;
      if (n === 0) return false;
      if (list.index < 0) return list.set(0);
      const i = list.index;
      let next = i;
      if (cols === 1) {
        if (dir === 'up') next = (i - 1 + n) % n;
        else if (dir === 'down') next = (i + 1) % n;
        else return false;
      } else {
        const row = Math.floor(i / cols);
        const col = i % cols;
        if (dir === 'left') next = col > 0 ? i - 1 : i;
        else if (dir === 'right') next = col < cols - 1 && i + 1 < n ? i + 1 : i;
        else if (dir === 'up') next = row > 0 ? i - cols : i;
        else if (dir === 'down') next = i + cols < n ? i + cols : Math.min(n - 1, (row + 1) * cols < n ? n - 1 : i);
      }
      return list.set(next);
    },
    activate() {
      const it = list.current();
      if (!it || it.disabled || !it.activate) return false;
      it.activate();
      return true;
    },
    adjust(dir) {
      const it = list.current();
      if (!it || it.disabled || !it.adjust) return false;
      it.adjust(dir);
      return true;
    },
  };
  items.forEach((it, i) => {
    it.el.classList.add('focusable');
    it.el.addEventListener('mousemove', (ev) => { if (pointerMoved(ev) && list.set(i)) sfx('move'); });
    it.el.addEventListener('click', (ev) => {
      // Cliques nas setas ‹ › do seletor têm handler próprio.
      if ((ev.target as HTMLElement | null)?.closest('.sel-arrow')) return;
      list.set(i);
      blurActive();
      if (it.disabled) return;
      if (it.activate) { it.activate(); sfx('confirm'); }
    });
  });
  list.set(opts.start ?? 0);
  return list;
}

/** Navegação padrão de uma tela com uma lista: setas movem/ajustam, confirmar ativa, voltar chama `onBack`. */
export function listNav(list: FocusList, nav: MenuNav, sfx: ScreenApi['sfx'], onBack?: () => void): void {
  if (nav.up && list.move('up')) sfx('move');
  if (nav.down && list.move('down')) sfx('move');
  if (nav.left && (list.adjust(-1) || list.move('left'))) sfx('move');
  if (nav.right && (list.adjust(1) || list.move('right'))) sfx('move');
  if (nav.confirm && list.activate()) sfx('confirm');
  if (nav.back && onBack) { sfx('back'); onBack(); }
}

// ───────────────────────────── Widgets ─────────────────────────────

export function button(label: string, onActivate: () => void, cls = ''): FocusItem {
  const el = h('button', { class: `btn ${cls}`.trim(), text: label, attrs: { type: 'button' } });
  return { el, activate: onActivate };
}

export interface Selector extends FocusItem {
  refresh(): void;
}

/** Seta ‹ › de um seletor: clique ajusta sem mover o cursor da lista. */
export function arrowButton(dir: -1 | 1, onClick: () => void): HTMLElement {
  return h('span', {
    class: `sel-arrow ${dir < 0 ? 'prev' : 'next'}`,
    on: { click: (ev) => { ev.stopPropagation(); blurActive(); onClick(); } },
  }, icon(dir < 0 ? 'chevron-left' : 'chevron-right'));
}

/** Linha "Rótulo   ‹ valor ›": ← → (ou clique nas setas) chamam `onAdjust`; `value()` dá o texto atual. */
export function selector(label: string, value: () => string, onAdjust: (dir: -1 | 1) => void, opts: { sfx?: ScreenApi['sfx']; onActivate?: () => void; cls?: string } = {}): Selector {
  const valueEl = h('span', { class: 'sel-value', text: value() });
  const arrow = (dir: -1 | 1) => arrowButton(dir, () => { onAdjust(dir); sel.refresh(); opts.sfx?.('move'); });
  const el = h('div', { class: `sel ${opts.cls ?? ''}`.trim() },
    h('span', { class: 'sel-label', text: label }),
    h('span', { class: 'sel-box' }, arrow(-1), valueEl, arrow(1)),
  );
  const sel: Selector = {
    el,
    adjust: (dir) => { onAdjust(dir); sel.refresh(); },
    activate: opts.onActivate ?? (() => { onAdjust(1); sel.refresh(); }),
    refresh: () => { valueEl.textContent = value(); },
  };
  return sel;
}

/** Dificuldade em cinco pontos (os acesos na cor de destaque). */
export function dots(difficulty: number): HTMLElement {
  const n = Math.max(0, Math.min(5, Math.round(difficulty)));
  return h('span', { class: 'dots', attrs: { 'aria-label': `${n}/5` } },
    Array.from({ length: 5 }, (_, i) => h('i', { class: i < n ? 'on' : '' })),
  );
}

export function stars(difficulty: number): string {
  const n = Math.max(0, Math.min(5, Math.round(difficulty)));
  return '★'.repeat(n) + '☆'.repeat(5 - n);
}

export function onOff(v: boolean): string {
  return v ? t('ui.common.on') : t('ui.common.off');
}

export function countryName(country: string): string {
  return t(`core.country.${country}`);
}

/** Bandeira do país pela copa que o representa (as pistas e as copas usam o mesmo nome de país). */
export function flagFor(ctx: MenuContext, country: string): string {
  return ctx.cups.find((c) => c.country === country)?.flag ?? '';
}

export function dayIcon(timeOfDay: TimeOfDay): SVGSVGElement {
  return icon(timeOfDay === 'day' ? 'sun' : timeOfDay === 'dusk' ? 'dusk' : 'moon', `day-${timeOfDay}`);
}

/** Miniatura do contorno da pista num canvas quadrado de `size` px CSS (nítida em telas HiDPI). */
export function trackThumb(ctx: MenuContext, def: TrackDef, size: number, color = 'rgba(255,255,255,0.92)'): HTMLCanvasElement {
  const dpr = typeof devicePixelRatio === 'number' && devicePixelRatio > 0 ? Math.min(3, devicePixelRatio) : 1;
  const canvas = h('canvas', { class: 'track-thumb', attrs: { width: String(Math.round(size * dpr)), height: String(Math.round(size * dpr)), 'aria-label': def.name } });
  canvas.style.width = `${size}px`;
  canvas.style.height = `${size}px`;
  let points: Array<[number, number]> = [];
  try {
    points = ctx.trackOutline(getTrack(def.id), size);
  } catch {
    points = [];
  }
  const g = canvas.getContext('2d');
  if (!g || points.length < 2) return canvas;
  g.scale(dpr, dpr);
  g.lineJoin = 'round';
  g.lineCap = 'round';
  g.lineWidth = Math.max(3, size / 16);
  g.strokeStyle = 'rgba(0,0,0,0.45)';
  g.beginPath();
  points.forEach(([x, y], i) => (i === 0 ? g.moveTo(x, y) : g.lineTo(x, y)));
  g.closePath();
  g.stroke();
  g.lineWidth = Math.max(1.5, size / 36);
  g.strokeStyle = color;
  g.stroke();
  // Largada, na cor de destaque.
  const [sx, sy] = points[0];
  g.fillStyle = '#ff5a36';
  g.beginPath();
  g.arc(sx, sy, Math.max(2.5, size / 22), 0, Math.PI * 2);
  g.fill();
  return canvas;
}

// ───────────────────────────── Carros ─────────────────────────────

export type CarStatKey = 'topSpeed' | 'accel' | 'handling' | 'economy';
export const CAR_STAT_KEYS: readonly CarStatKey[] = ['topSpeed', 'accel', 'handling', 'economy'];

function carStat(car: CarDef, key: CarStatKey): number {
  return key === 'economy' ? 1 / car.fuelPerUnit : car[key];
}

/** Barras 0..1 de um carro, normalizadas entre todos os carros (o pior fica em 0,25 para não sumir). */
export function carBars(car: CarDef, cars: readonly CarDef[]): Record<CarStatKey, number> {
  const out = { topSpeed: 0, accel: 0, handling: 0, economy: 0 };
  for (const key of CAR_STAT_KEYS) {
    const values = cars.map((c) => carStat(c, key));
    const min = Math.min(...values);
    const max = Math.max(...values);
    const v = carStat(car, key);
    out[key] = max > min ? 0.25 + 0.75 * ((v - min) / (max - min)) : 1;
  }
  return out;
}

/** Cartão do carro: silhueta na cor, nome, quatro barras (animadas ao entrar) e a frase de apresentação. */
export function carCard(car: CarDef, cars: readonly CarDef[]): HTMLElement {
  const bars = carBars(car, cars);
  return h('div', { class: 'car-card' },
    h('div', { class: 'car-name', text: car.name }),
    h('div', { class: 'car-visual' }, carSilhouette(car.color)),
    h('div', { class: 'car-bars' }, CAR_STAT_KEYS.map((key, i) =>
      h('div', { class: 'car-bar' },
        h('span', { class: 'car-bar-label', text: t(`ui.lobby.stat.${key}`) }),
        h('span', { class: 'car-bar-track' }, h('span', { class: 'car-bar-fill', style: `width:${Math.round(bars[key] * 100)}%;animation-delay:${i * 40}ms` })),
      ),
    )),
    h('p', { class: 'car-blurb', text: t(`core.car.${car.id}.blurb`) }),
  );
}
