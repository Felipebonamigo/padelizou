// HUD da corrida em DOM, por viewport: velocímetro com arco de RPM (SVG), marcha, volta,
// posição, tempos, combustível, nitro (ou o cofre da equipe), mensagens animadas, minimapa
// SVG, etiqueta do jogador, companheiros no co-op, contagem regressiva e pausa. Com 3
// jogadores a célula livre mostra a classificação completa. Os nós são criados uma vez e só
// `textContent`/atributos mudam por quadro (cada escrita é comparada antes).
import './hud.css';
import './strings';
import { COUNTDOWN_TICKS, GEAR_TOP, NITRO_DURATION_TICKS, TICK_RATE } from '../core/constants';
import { SEAT_COLORS } from '../core/data/drivers';
import { formatTicks } from '../core/sim/race';
import type { CarState, RaceState, Track } from '../core/types';
import type { HudMessage, RenderFrame, ViewportSpec } from '../game/contracts';
import { t } from '../i18n';
import { spareCell, uiScale, viewportRects, type Rect } from './layout';
import { outlinePoint, trackOutline, type Outline } from './minimap';
import { ordinalSuffix } from './strings';

const SVG = 'http://www.w3.org/2000/svg';
const MAX_CARS = 20;
const MAP_SIZE = 120;
const RPM_LEN = 2 * Math.PI * 42 * 0.75;

function el(tag: string, cls: string, parent: HTMLElement): HTMLElement {
  const e = document.createElement(tag);
  e.className = cls;
  parent.appendChild(e);
  return e;
}

function svg<K extends keyof SVGElementTagNameMap>(tag: K, parent: Element, attrs: Record<string, string> = {}): SVGElementTagNameMap[K] {
  const e = document.createElementNS(SVG, tag);
  for (const [k, v] of Object.entries(attrs)) e.setAttribute(k, v);
  parent.appendChild(e);
  return e;
}

function setText(e: Element, s: string): void { if (e.textContent !== s) e.textContent = s; }
function setClass(e: Element, cls: string, on: boolean): void { if (e.classList.contains(cls) !== on) e.classList.toggle(cls, on); }
function setStyle(e: HTMLElement, prop: string, value: string): void { if (e.style.getPropertyValue(prop) !== value) e.style.setProperty(prop, value); }
function setAttr(e: Element, name: string, value: string): void { if (e.getAttribute(name) !== value) e.setAttribute(name, value); }

function ordinal(n: number): string { return t('hud.ordinal', { n, s: ordinalSuffix(n) }); }

/** Minimapa: contorno + um ponto por carro. */
class MiniMap {
  readonly root: HTMLElement;
  private readonly path: SVGPathElement;
  private readonly dots: SVGCircleElement[] = [];
  private outline: Outline = [];
  private trackId = '';
  private readonly pt: [number, number] = [0, 0];

  constructor(parent: HTMLElement, cls: string) {
    this.root = el('div', cls, parent);
    const s = svg('svg', this.root, { viewBox: `0 0 ${MAP_SIZE} ${MAP_SIZE}` });
    this.path = svg('path', s, { d: '' });
    for (let i = 0; i < MAX_CARS; i++) this.dots.push(svg('circle', s, { r: '3', cx: '0', cy: '0', class: 'ai', visibility: 'hidden' }));
  }

  update(track: Track, state: RaceState, ownIndex: number, viewports: ViewportSpec[]): void {
    if (track.def.id !== this.trackId) {
      this.trackId = track.def.id;
      this.outline = trackOutline(track, MAP_SIZE);
      this.path.setAttribute('d', this.outline.map(([x, y], i) => `${i ? 'L' : 'M'}${x.toFixed(1)} ${y.toFixed(1)}`).join('') + 'Z');
    }
    const cars = state.cars;
    for (let i = 0; i < MAX_CARS; i++) {
      const dot = this.dots[i];
      const c = cars[i];
      if (!c) { setAttr(dot, 'visibility', 'hidden'); continue; }
      setAttr(dot, 'visibility', 'visible');
      outlinePoint(this.outline, track, c.z, this.pt);
      setAttr(dot, 'cx', this.pt[0].toFixed(1)); setAttr(dot, 'cy', this.pt[1].toFixed(1));
      const own = i === ownIndex;
      if (c.seat >= 0) {
        const vp = viewports.find((v) => v.seat === c.seat);
        setAttr(dot, 'fill', vp ? vp.color : SEAT_COLORS[c.seat % SEAT_COLORS.length]);
        setAttr(dot, 'r', own ? '5' : '4');
        setAttr(dot, 'class', own ? 'me' : 'human');
      } else {
        setAttr(dot, 'fill', '#9aa3b5'); setAttr(dot, 'r', '2.6'); setAttr(dot, 'class', 'ai');
      }
    }
  }
}

/** Fila de mensagens: nós reutilizados; a animação reinicia quando a mensagem muda. */
class MessageSlots {
  private readonly nodes: HTMLElement[] = [];
  private readonly shown: Array<HudMessage | null> = [];
  constructor(parent: HTMLElement, count: number) {
    for (let i = 0; i < count; i++) { this.nodes.push(el('div', 'msg', parent)); this.shown.push(null); }
  }
  update(messages: HudMessage[]): void {
    let k = 0;
    for (const m of messages) {
      if (k >= this.nodes.length) break;
      const node = this.nodes[k];
      if (this.shown[k] !== m) {
        this.shown[k] = m;
        node.className = `msg ${m.kind} show`;
        node.textContent = m.text;
        node.style.animation = 'none';
        void node.offsetWidth;
        node.style.animation = '';
      }
      setStyle(node, 'opacity', m.ttl < 0.3 ? (m.ttl / 0.3).toFixed(2) : '1');
      k++;
    }
    for (; k < this.nodes.length; k++) { if (this.shown[k]) { this.shown[k] = null; setClass(this.nodes[k], 'show', false); } }
  }
}

class SeatHud {
  readonly root: HTMLElement;
  private readonly tagName: HTMLElement;
  private readonly posN: HTMLElement; private readonly posOf: HTMLElement; private readonly lap: HTMLElement;
  private readonly time: HTMLElement; private readonly lastLabel: HTMLElement; private readonly lastVal: HTMLElement; private readonly bestLabel: HTMLElement; private readonly bestVal: HTMLElement;
  private readonly mates: HTMLElement; private readonly mateRows: Array<{ row: HTMLElement; dot: HTMLElement; name: HTMLElement; pos: HTMLElement }> = [];
  private readonly fuel: HTMLElement; private readonly fuelLabel: HTMLElement; private readonly fuelFill: HTMLElement;
  private readonly nitroLabel: HTMLElement; private readonly caps: HTMLElement[] = []; private readonly extra: HTMLElement;
  private readonly nbar: HTMLElement; private readonly nfill: HTMLElement;
  private readonly rpmFg: SVGPathElement; private readonly kmh: HTMLElement; private readonly unit: HTMLElement; private readonly gear: HTMLElement; private readonly gearLabel: HTMLElement;
  private readonly mini: MiniMap;
  private readonly topMsgs: MessageSlots; private readonly centerMsgs: MessageSlots;
  private readonly count: HTMLElement; private lastCount = '';
  private readonly pause: HTMLElement; private readonly lines: HTMLElement;
  private readonly topList: HudMessage[] = []; private readonly centerList: HudMessage[] = [];

  constructor(parent: HTMLElement) {
    this.root = el('div', 'vp', parent);
    this.lines = el('div', 'lines', this.root);
    const tl = el('div', 'tl glass', this.root);
    const tag = el('div', 'tag', tl); el('i', 'dot', tag); this.tagName = el('span', 'name', tag);
    const pos = el('div', 'pos', tl); this.posN = el('span', 'pos-n', pos); this.posOf = el('span', 'pos-of', pos);
    this.lap = el('div', 'lap', tl);
    const tr = el('div', 'tr glass', this.root);
    this.time = el('div', 'time', tr);
    const laps = el('div', 'laps', tr);
    const last = el('span', 'last', laps); this.lastLabel = el('span', '', last); this.lastVal = el('b', '', last);
    const best = el('span', 'best', laps); this.bestLabel = el('span', '', best); this.bestVal = el('b', '', best);
    this.mates = el('div', 'mates glass', this.root);
    for (let i = 0; i < 3; i++) {
      const row = el('div', 'mate', this.mates); const dot = el('i', '', row); const name = el('span', 'mn', row); const p = el('span', 'mp', row);
      row.style.display = 'none';
      this.mateRows.push({ row, dot, name, pos: p });
    }
    const bl = el('div', 'bl glass', this.root);
    this.fuel = el('div', 'fuel', bl); this.fuelLabel = el('div', 'label', this.fuel); const fbar = el('div', 'bar', this.fuel); this.fuelFill = el('div', 'fill', fbar);
    const nitro = el('div', 'nitro', bl); this.nitroLabel = el('div', 'label', nitro); const caps = el('div', 'caps', nitro);
    for (let i = 0; i < 3; i++) this.caps.push(el('i', '', caps));
    this.extra = el('span', 'extra', caps);
    this.nbar = el('div', 'nbar', nitro); this.nfill = el('div', 'fill', this.nbar);
    const br = el('div', 'br glass', this.root);
    const rpm = svg('svg', br, { class: 'rpm', viewBox: '0 0 100 100' });
    const d = 'M 20.3 79.7 A 42 42 0 1 1 79.7 79.7';
    svg('path', rpm, { class: 'bg', d });
    this.rpmFg = svg('path', rpm, { class: 'fg', d, 'stroke-dasharray': `${RPM_LEN} ${RPM_LEN}`, 'stroke-dashoffset': `${RPM_LEN}` });
    const speed = el('div', 'speed', br); this.kmh = el('span', 'kmh', speed); this.unit = el('span', 'unit', speed);
    this.gear = el('div', 'gear', br); this.gearLabel = el('small', '', this.gear); this.gear.appendChild(document.createTextNode(''));
    this.mini = new MiniMap(this.root, 'mini glass');
    const top = el('div', 'top-msgs', this.root); this.topMsgs = new MessageSlots(top, 2);
    const center = el('div', 'center', this.root); this.count = el('div', 'count', center); this.centerMsgs = new MessageSlots(center, 3);
    this.pause = el('div', 'pause', this.root);
  }

  update(frame: RenderFrame, vp: ViewportSpec, rect: Rect): void {
    const { state, track } = frame;
    const car: CarState | undefined = state.cars[vp.carIndex];
    const r = this.root;
    setStyle(r, 'left', `${rect.x}px`); setStyle(r, 'top', `${rect.y}px`); setStyle(r, 'width', `${rect.w}px`); setStyle(r, 'height', `${rect.h}px`);
    setStyle(r, '--s', uiScale(rect).toFixed(3)); setStyle(r, '--accent', vp.color);
    setText(this.tagName, vp.name);
    if (!car) return;
    const laps = state.config.laps;
    const total = state.cars.length;
    // Posição e volta.
    setText(this.posN, ordinal(car.position));
    setText(this.posOf, t('hud.of', { total }));
    const lapN = Math.max(1, Math.min(laps, car.lap));
    setText(this.lap, car.finished ? t('hud.finished') : car.lap >= laps ? t('hud.lastLap') : t('hud.lapOf', { n: lapN, total: laps }));
    setClass(this.lap, 'final', !car.finished && car.lap >= laps);
    setClass(this.lap, 'done', car.finished);
    // Tempos.
    const raceTicks = car.finished ? car.finishTick - state.startTick : Math.max(0, state.tick - state.startTick);
    setText(this.time, formatTicks(state.phase === 'countdown' ? 0 : raceTicks));
    const lastLap = car.lapTicks.length ? car.lapTicks[car.lapTicks.length - 1] : -1;
    const bestLap = car.lapTicks.length ? Math.min(...car.lapTicks) : -1;
    setText(this.lastLabel, `${t('hud.last')} `); setText(this.lastVal, formatTicks(lastLap));
    setText(this.bestLabel, `${t('hud.best')} `); setText(this.bestVal, formatTicks(bestLap));
    // Companheiros (co-op).
    let m = 0;
    if (frame.coop) {
      for (const other of frame.viewports) {
        if (other.seat === vp.seat || m >= this.mateRows.length) continue;
        const oc = state.cars[other.carIndex];
        if (!oc) continue;
        const row = this.mateRows[m++];
        setStyle(row.row, 'display', 'flex');
        setStyle(row.dot, 'background', other.color);
        setText(row.name, other.name);
        setText(row.pos, ordinal(oc.position));
      }
    }
    for (; m < this.mateRows.length; m++) setStyle(this.mateRows[m].row, 'display', 'none');
    // Combustível.
    setClass(this.fuel, 'hidden', state.config.timeTrial === true);
    setText(this.fuelLabel, t('hud.fuel'));
    setStyle(this.fuelFill, 'clip-path', `inset(0 ${((1 - car.fuel) * 100).toFixed(1)}% 0 0)`);
    setClass(this.fuel, 'low', car.fuel < 0.25);
    // Nitro: cofre da equipe ou cargas próprias.
    // Mesma regra da física: com sharedNitro o humano gasta do cofre do time, mesmo sozinho.
    const shared = state.config.assists.sharedNitro;
    const charges = shared ? (state.teamNitro[car.teamId] ?? 0) : car.nitroLeft;
    setText(this.nitroLabel, shared && frame.coop ? `${t('hud.nitro')} · ${t('hud.team')}` : t('hud.nitro'));
    for (let i = 0; i < 3; i++) setClass(this.caps[i], 'on', i < charges);
    setText(this.extra, charges > 3 ? `×${charges}` : '');
    setClass(this.nbar, 'on', car.nitroTicks > 0);
    setStyle(this.nfill, 'width', `${((car.nitroTicks / NITRO_DURATION_TICKS) * 100).toFixed(1)}%`);
    setClass(this.lines, 'on', car.nitroTicks > 0 && !frame.paused);
    // Velocímetro: RPM = posição da velocidade dentro da marcha.
    const sf = car.speed / car.stats.topSpeed;
    const lo = car.gear > 0 ? GEAR_TOP[car.gear - 1] : 0;
    const hi = GEAR_TOP[Math.min(car.gear, GEAR_TOP.length - 1)];
    const rpm = Math.max(0, Math.min(1, (sf - lo) / Math.max(0.01, hi - lo)));
    setAttr(this.rpmFg, 'stroke-dashoffset', (RPM_LEN * (1 - rpm)).toFixed(1));
    setClass(this.rpmFg, 'red', rpm > 0.92);
    setText(this.kmh, String(Math.round(car.speed * 300 / 6000)));
    setText(this.unit, t('hud.kmh'));
    setText(this.gearLabel, t('hud.gear'));
    const gearText = this.gear.lastChild;
    if (gearText && gearText.textContent !== String(car.gear + 1)) gearText.textContent = String(car.gear + 1);
    // Minimapa.
    setClass(this.mini.root, 'hidden', !frame.options.showMinimap);
    if (frame.options.showMinimap) this.mini.update(track, state, vp.carIndex, frame.viewports);
    // Mensagens: 'info' no topo; as outras no centro. Na contagem, as "big" com o número
    // ficam de fora (a contagem grande já cobre).
    this.topList.length = 0; this.centerList.length = 0;
    const counting = state.phase === 'countdown';
    for (const msg of vp.messages) {
      if (msg.kind === 'info') this.topList.push(msg);
      else if (!(counting && msg.kind === 'big')) this.centerList.push(msg);
    }
    this.topMsgs.update(this.topList);
    this.centerMsgs.update(this.centerList);
    // Contagem regressiva.
    if (counting) {
      const n = Math.ceil((COUNTDOWN_TICKS - state.tick) / TICK_RATE);
      const text = n > 3 ? t('hud.countdown') : String(Math.max(1, n));
      if (text !== this.lastCount) {
        this.lastCount = text;
        this.count.textContent = text;
        this.count.className = `count show${n > 3 ? ' ready' : ''}`;
        this.count.style.animation = 'none'; void this.count.offsetWidth; this.count.style.animation = '';
      }
    } else if (this.lastCount) { this.lastCount = ''; setClass(this.count, 'show', false); }
    setText(this.pause, t('hud.pause'));
    setClass(this.pause, 'show', frame.paused);
  }
}

/** Célula livre (3 jogadores): classificação completa + minimapa grande. */
class SparePanel {
  readonly root: HTMLElement;
  private readonly title: HTMLElement;
  private readonly rows: Array<{ row: HTMLElement; p: HTMLElement; n: HTMLElement; l: HTMLElement }> = [];
  private readonly mini: MiniMap;
  private readonly order: number[] = [];

  constructor(parent: HTMLElement) {
    this.root = el('div', 'spare', parent);
    const st = el('div', 'standings glass', this.root);
    this.title = el('div', 'label', st);
    for (let i = 0; i < MAX_CARS; i++) {
      const row = el('div', 'row', st);
      this.rows.push({ row, p: el('span', 'p', row), n: el('span', 'n', row), l: el('span', 'l', row) });
    }
    this.mini = new MiniMap(this.root, 'bigmap glass');
  }

  update(frame: RenderFrame, rect: Rect): void {
    const { state, track } = frame;
    const r = this.root;
    setStyle(r, 'left', `${rect.x}px`); setStyle(r, 'top', `${rect.y}px`); setStyle(r, 'width', `${rect.w}px`); setStyle(r, 'height', `${rect.h}px`);
    setStyle(r, '--s', uiScale(rect).toFixed(3));
    setText(this.title, t('hud.standings'));
    this.order.length = 0;
    for (let i = 0; i < state.cars.length; i++) this.order.push(i);
    this.order.sort((a, b) => state.cars[a].position - state.cars[b].position);
    for (let k = 0; k < MAX_CARS; k++) {
      const row = this.rows[k];
      const idx = this.order[k];
      if (idx === undefined) { setStyle(row.row, 'display', 'none'); continue; }
      const c = state.cars[idx];
      setStyle(row.row, 'display', 'flex');
      setText(row.p, String(c.position));
      setText(row.n, c.name);
      setText(row.l, c.finished ? t('hud.finished') : t('hud.lapShort', { n: Math.max(1, Math.min(state.config.laps, c.lap)) }));
      setClass(row.row, 'human', c.seat >= 0);
      const vp = c.seat >= 0 ? frame.viewports.find((v) => v.seat === c.seat) : undefined;
      setStyle(row.n, 'color', vp ? vp.color : '');
    }
    this.mini.update(track, state, -1, frame.viewports);
  }
}

export class Hud {
  private readonly seats: SeatHud[] = [];
  private spare: SparePanel | null = null;
  private visible = true;

  constructor(private readonly root: HTMLElement) {
    root.classList.add('nc-hud');
  }

  update(frame: RenderFrame, width: number, height: number): void {
    if (!this.visible) { this.visible = true; this.root.classList.remove('hidden'); }
    const n = frame.viewports.length;
    while (this.seats.length < n) this.seats.push(new SeatHud(this.root));
    for (let i = n; i < this.seats.length; i++) setStyle(this.seats[i].root, 'display', 'none');
    const rects = viewportRects(n, width, height);
    for (let i = 0; i < n; i++) {
      setStyle(this.seats[i].root, 'display', 'block');
      this.seats[i].update(frame, frame.viewports[i], rects[i]);
    }
    const spare = spareCell(n, width, height);
    if (spare) {
      if (!this.spare) this.spare = new SparePanel(this.root);
      setStyle(this.spare.root, 'display', 'flex');
      this.spare.update(frame, spare);
    } else if (this.spare) setStyle(this.spare.root, 'display', 'none');
  }

  hide(): void {
    if (this.visible) { this.visible = false; this.root.classList.add('hidden'); }
  }

  dispose(): void {
    this.root.innerHTML = '';
    this.root.classList.remove('nc-hud', 'hidden');
  }
}
