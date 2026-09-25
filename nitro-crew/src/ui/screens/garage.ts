// Tela "Carreira" (continuar / nova) e a Garagem. A garagem tem um painel por piloto, cada um
// navegado pelo próprio dispositivo (como no lobby): carro (←→ troca, Enter compra ou escolhe),
// atributos com antes → depois do item em foco, as seis melhorias e PRONTO. As compras chamam as
// regras puras de src/core/career.ts e gravam o save na hora; com todos prontos, a sessão corre.
import '../../career/strings';
import './garage.css';
import {
  buyCar, buyUpgrade, cupIndexOf, levelsOf, NO_UPGRADES, ownsCar, partMaxLevel, selectCar, UPGRADE_PARTS, upgradePrice, walletOf,
  type CareerState, type PurchaseResult,
} from '../../core/career';
import { nextTrackId } from '../../core/championship';
import { SPEED_TO_KMH, UPGRADE_MAX_LEVEL } from '../../core/constants';
import { SEAT_COLORS } from '../../core/data/drivers';
import { effectiveStats } from '../../core/sim/stats';
import type { CarDef, CarStats, UpgradeLevels, UpgradePart } from '../../core/types';
import type { DeviceId, MenuContext, MenuNav } from '../../game/contracts';
import { unlockCar } from '../../game/career-save';
import { saveSave } from '../../game/save';
import { getLanguage, t } from '../../i18n';
import { arrowButton, button, createFocusList, h, listNav, screenFrame, type FocusItem, type FocusList, type ScreenApi, type ScreenInstance } from './common';
import { carSilhouette, icon } from './icons';
import { startCursor } from './lobby';

// ───────────────────────────── Números para a tela ─────────────────────────────

export function formatMoney(v: number): string {
  return t('career.money', { v: Math.round(v).toLocaleString(getLanguage() === 'pt' ? 'pt-BR' : 'en-US') });
}

export type GarageStat = 'topSpeed' | 'accel' | 'handling' | 'brake' | 'economy';
export const GARAGE_STATS: readonly GarageStat[] = ['topSpeed', 'accel', 'handling', 'brake', 'economy'];
const FULL: UpgradeLevels = { engine: UPGRADE_MAX_LEVEL, turbo: UPGRADE_MAX_LEVEL, tires: UPGRADE_MAX_LEVEL, brakes: UPGRADE_MAX_LEVEL, tank: UPGRADE_MAX_LEVEL, nitro: UPGRADE_MAX_LEVEL };

function statValue(s: CarStats, k: GarageStat): number {
  return k === 'economy' ? 1 / s.fuelPerUnit : s[k];
}

/** Faixa de cada atributo: do pior carro de fábrica ao melhor carro com tudo no máximo. */
function statRanges(cars: readonly CarDef[]): Record<GarageStat, [number, number]> {
  const out = {} as Record<GarageStat, [number, number]>;
  for (const k of GARAGE_STATS) {
    const lo = Math.min(...cars.map((c) => statValue(effectiveStats(c, null), k)));
    const hi = Math.max(...cars.map((c) => statValue(effectiveStats(c, FULL), k)));
    out[k] = [lo, hi];
  }
  return out;
}

/** 0..1 para a barra (o pior de fábrica fica em 0,12 para não sumir). */
export function statFraction(ranges: Record<GarageStat, [number, number]>, k: GarageStat, v: number): number {
  const [lo, hi] = ranges[k];
  if (!(hi > lo)) return 1;
  return 0.12 + 0.88 * Math.min(1, Math.max(0, (v - lo) / (hi - lo)));
}

/** Velocidade em km/h (a unidade some no "antes → depois", que precisa caber ao lado da barra); o resto de 0 a 100. */
function statText(ranges: Record<GarageStat, [number, number]>, k: GarageStat, v: number, unit = true): string {
  return k === 'topSpeed' ? `${Math.round(v * SPEED_TO_KMH)}${unit ? ' km/h' : ''}` : String(Math.round(statFraction(ranges, k, v) * 100));
}

function levelsPlus(levels: UpgradeLevels, part: UpgradePart): UpgradeLevels {
  return { ...levels, [part]: levels[part] + 1 };
}

function cupName(id: string): string {
  return t(`core.cup.${id}`);
}

// ───────────────────────────── Tela "Carreira" ─────────────────────────────

function modeText(career: CareerState): string {
  if (career.coop) return t('career.mode.coop');
  return career.drivers.length > 1 ? t('career.mode.versus') : t('career.mode.solo');
}

/** Faixa com as copas da carreira: feitas, a atual e as que faltam. */
function cupStrip(ctx: MenuContext, career: CareerState | null): HTMLElement {
  const current = career ? cupIndexOf(career, ctx.cups) : -1;
  return h('div', { class: 'cup-strip' }, ctx.cups.map((c, i) => {
    const done = career !== null && (career.completed || i < current);
    const now = career !== null && !career.completed && i === current;
    return h('span', { class: `cup-pip${done ? ' done' : ''}${now ? ' now' : ''}`, title: cupName(c.id) },
      h('span', { class: 'cup-pip-flag', text: c.flag }), done ? icon('check') : null);
  }));
}

function careerSummary(ctx: MenuContext, career: CareerState): HTMLElement {
  const cupIdx = cupIndexOf(career, ctx.cups);
  const cup = ctx.cups[cupIdx];
  const raceCount = cup?.trackIds.length ?? 0;
  const raceN = (career.champ?.raceIndex ?? 0) + 1;
  const money = career.coop
    ? h('span', { class: 'mono strong', text: formatMoney(career.wallets[0] ?? 0) })
    : h('span', { class: 'cs-list' }, career.drivers.map((d, i) => h('span', { class: 'cs-chip', style: `--seat:${SEAT_COLORS[i]}` },
      h('b', { text: `P${i + 1}` }), h('span', { class: 'mono', text: formatMoney(career.wallets[i] ?? 0) }))));
  const bought = new Set(career.drivers.flatMap((d) => d.garage.owned)).size;
  const row = (label: string, ...value: Array<HTMLElement | string>) => h('div', { class: 'cs-row' }, h('span', { class: 'cs-label', text: label }), h('span', { class: 'cs-value' }, value));
  return h('div', { class: 'career-summary' },
    career.completed ? h('div', { class: 'verdict good' }, icon('trophy'), h('span', { text: t('career.hub.completed') })) : null,
    row(t('career.hub.mode'), `${modeText(career)} · ${career.drivers.length}P`),
    row(t('career.hub.drivers'), h('span', { class: 'cs-list' }, career.drivers.map((d, i) => h('span', { class: 'cs-chip', style: `--seat:${SEAT_COLORS[i]}` },
      h('b', { text: `P${i + 1}` }), h('span', { text: d.name }), h('span', { class: 'muted-inline', text: ctx.cars.find((c) => c.id === d.garage.carId)?.name ?? '' }))))),
    career.completed ? null : row(t('career.hub.cup'),
      h('span', { class: 'cs-cup' }, `${cup?.flag ?? ''} ${cup ? cupName(cup.id) : ''} — ${t('career.hub.race', { n: Math.min(raceN, raceCount), m: raceCount })}`),
      career.attempts > 1 ? h('span', { class: 'muted-inline', text: ` · ${t('career.hub.attempt', { n: career.attempts })}` }) : ''),
    row(career.coop ? t('career.garage.teamWallet') : t('career.hub.money'), money),
    row(t('career.hub.cars'), String(bought)),
    row(t('career.hub.races'), String(career.racesRun)),
    cupStrip(ctx, career),
  );
}

export function careerScreen(api: ScreenApi): ScreenInstance {
  const { ctx } = api;
  const career = ctx.save.career;
  const ongoing = career !== null && !career.completed;
  const openLobby = (resume: boolean) => {
    api.lobby.mode = 'career';
    api.lobby.resume = resume;
    for (const seat of api.lobby.seats) if (seat) { seat.ready = false; seat.cursor = startCursor(api.lobby); }
    api.go('lobby');
  };
  let confirming = false;
  const items: FocusItem[] = [];
  if (ongoing) items.push(button(t('career.hub.continue'), () => openLobby(true), 'btn-primary'));
  const fresh = button(t('career.hub.new'), () => {
    if (ongoing && !confirming) {
      confirming = true;
      fresh.el.textContent = t('career.hub.confirmNew');
      fresh.el.classList.add('btn-danger');
      return;
    }
    openLobby(false);
  }, ongoing ? '' : 'btn-primary');
  items.push(fresh, button(t('ui.common.back'), () => api.back()));
  const list = createFocusList(items, { sfx: api.sfx });
  const el = screenFrame('career', t('career.hub.title'),
    h('div', { class: 'career-hub glass' },
      career ? careerSummary(ctx, career) : h('div', { class: 'career-intro' },
        h('p', { text: t('career.hub.intro') }),
        h('p', { class: 'muted-inline', text: t('career.hub.introCoop') }),
        h('p', { class: 'muted-inline', text: t('career.hub.none') }),
        cupStrip(ctx, null),
      ),
      h('div', { class: 'menu-list career-actions' }, items.map((i) => i.el)),
    ),
  );
  return { el, nav: (nav) => listNav(list, nav, api.sfx, () => api.back()) };
}

// ───────────────────────────── Garagem ─────────────────────────────

interface SeatUi {
  /** Índice em ctx.cars do carro à mostra (dono: é o escolhido; à venda: vitrine). */
  view: number;
  ready: boolean;
  cursor: number;
  msg: string;
  msgKind: 'good' | 'bad';
  msgTtl: number;
}

const MSG_SECONDS = 1.8;

export function garageScreen(api: ScreenApi): ScreenInstance {
  const { ctx } = api;
  const cars = ctx.cars;
  const ranges = statRanges(cars);
  const careerOrNull = ctx.save.career;
  if (!careerOrNull) {
    const back = button(t('ui.common.back'), () => api.emit({ type: 'toMain' }));
    const list = createFocusList([back], { sfx: api.sfx });
    return { el: screenFrame('garage', t('career.garage.title'), h('p', { class: 'hint', text: t('career.hub.none') }), back.el), nav: (nav) => listNav(list, nav, api.sfx) };
  }
  const career: CareerState = careerOrNull;
  const n = career.drivers.length;
  const carIndex = (id: string) => Math.max(0, cars.findIndex((c) => c.id === id));
  // Carreira concluída: não há o que comprar; o foco começa no MENU (o último item).
  const uis: SeatUi[] = career.drivers.map((d) => ({ view: carIndex(d.garage.carId), ready: false, cursor: career.completed ? Number.MAX_SAFE_INTEGER : 0, msg: '', msgKind: 'good', msgTtl: 0 }));
  let lists: Array<FocusList | null> = [];
  let previews: Array<() => void> = [];
  let msgEls: HTMLElement[] = [];
  let started = false;

  const persist = () => saveSave(ctx.save);

  const say = (seat: number, text: string, kind: SeatUi['msgKind']) => {
    const ui = uis[seat];
    ui.msg = text; ui.msgKind = kind; ui.msgTtl = MSG_SECONDS;
  };

  const purchaseMessage = (r: PurchaseResult): [string, SeatUi['msgKind']] => {
    if (r === 'ok') return [t('career.garage.msg.bought'), 'good'];
    if (r === 'noMoney') return [t('career.garage.msg.noMoney'), 'bad'];
    if (r === 'maxLevel') return [t('career.garage.msg.maxLevel'), 'bad'];
    return [t('career.garage.msg.notOwned'), 'bad'];
  };

  function allReady(): boolean {
    return uis.every((u) => u.ready);
  }

  function toggleReady(seat: number): void {
    if (career.completed) { api.sfx('confirm'); api.emit({ type: 'toMain' }); return; }
    const ui = uis[seat];
    ui.ready = !ui.ready;
    // Pronto vale para o carro escolhido: a vitrine volta para ele.
    if (ui.ready) ui.view = carIndex(career.drivers[seat].garage.carId);
    api.sfx(ui.ready ? 'confirm' : 'back');
    if (ui.ready && allReady() && !started) {
      started = true;
      api.emit({ type: 'careerRace' });
      return;
    }
    render();
  }

  function changeView(seat: number, dir: -1 | 1): void {
    const ui = uis[seat];
    if (career.completed) { api.sfx('back'); return; }
    if (ui.ready) { say(seat, t('career.garage.msg.locked'), 'bad'); render(); return; }
    ui.view = (ui.view + dir + cars.length) % cars.length;
    // Passear pelos carros da garagem já escolhe o carro; os à venda ficam só na vitrine.
    if (selectCar(career, seat, cars[ui.view].id)) persist();
    render();
  }

  function activateCar(seat: number): void {
    const ui = uis[seat];
    if (career.completed) { api.sfx('back'); return; }
    if (ui.ready) { say(seat, t('career.garage.msg.locked'), 'bad'); render(); return; }
    const car = cars[ui.view];
    const g = career.drivers[seat].garage;
    if (ownsCar(g, car.id)) {
      selectCar(career, seat, car.id);
      persist();
      render();
      return;
    }
    const r = buyCar(career, seat, car.id);
    if (r === 'ok') { unlockCar(ctx.save, car.id); persist(); }
    const [text, kind] = purchaseMessage(r);
    say(seat, text, kind);
    api.sfx(r === 'ok' ? 'confirm' : 'back');
    render();
  }

  function activatePart(seat: number, part: UpgradePart): void {
    const ui = uis[seat];
    if (career.completed) { api.sfx('back'); return; }
    if (ui.ready) { say(seat, t('career.garage.msg.locked'), 'bad'); render(); return; }
    const r = buyUpgrade(career, seat, part, cars[ui.view].id);
    if (r === 'ok') persist();
    const [text, kind] = purchaseMessage(r);
    say(seat, text, kind);
    api.sfx(r === 'ok' ? 'confirm' : 'back');
    render();
  }

  // ── Blocos de um painel ──

  function statsBlock(base: CarStats, after: CarStats | null): HTMLElement {
    const rows = GARAGE_STATS.map((k) => {
      const b = statValue(base, k);
      const a = after ? statValue(after, k) : b;
      const fb = statFraction(ranges, k, b);
      const fa = statFraction(ranges, k, a);
      const diff = Math.abs(fa - fb) > 1e-4;
      const up = fa > fb;
      const lo = Math.min(fa, fb);
      return h('div', { class: 'gs-row' },
        h('span', { class: 'gs-label', text: t(`career.garage.stat.${k}`) }),
        h('span', { class: 'gs-track' },
          h('span', { class: 'gs-fill', style: `width:${(diff ? lo : fb) * 100}%` }),
          diff ? h('span', { class: `gs-ghost ${up ? 'up' : 'down'}`, style: `left:${lo * 100}%;width:${Math.abs(fa - fb) * 100}%` }) : null,
        ),
        h('span', { class: `gs-value mono${diff ? (up ? ' up' : ' down') : ''}` },
          diff ? `${statText(ranges, k, b, false)} → ${statText(ranges, k, a, false)}` : statText(ranges, k, b)),
      );
    });
    const nitroDiff = after !== null && after.nitro !== base.nitro;
    rows.push(h('div', { class: 'gs-row' },
      h('span', { class: 'gs-label', text: t('career.garage.stat.nitro') }),
      h('span', { class: 'gs-nitro' }, Array.from({ length: Math.max(base.nitro, after?.nitro ?? 0) }, (_, i) => h('i', { class: i < base.nitro ? 'on' : 'new' }))),
      h('span', { class: `gs-value mono${nitroDiff ? ((after?.nitro ?? 0) > base.nitro ? ' up' : ' down') : ''}`, text: nitroDiff ? `${base.nitro} → ${after?.nitro ?? 0}` : String(base.nitro) }),
    ));
    return h('div', { class: 'gs' }, rows);
  }

  function panel(seat: number): { el: HTMLElement; items: FocusItem[]; preview: () => void; msgEl: HTMLElement } {
    const d = career.drivers[seat];
    const g = d.garage;
    const ui = uis[seat];
    const car = cars[ui.view];
    const owned = ownsCar(g, car.id);
    const selected = owned && g.carId === car.id;
    const levels = owned ? levelsOf(g, car.id) : { ...NO_UPGRADES };
    const money = walletOf(career, seat);
    const shown = effectiveStats(car, levels);
    const mineDef = cars.find((c) => c.id === g.carId) ?? cars[0];
    const mine = effectiveStats(mineDef, levelsOf(g, mineDef.id));
    const locked = ui.ready || career.completed;

    // Carro
    let status: HTMLElement;
    if (selected) status = h('span', { class: 'gp-tag ok' }, icon('check'), h('span', { text: t('career.garage.selected') }));
    else if (owned) status = h('span', { class: 'gp-tag', text: t('career.garage.owned') });
    else {
      const short = car.price - money;
      status = h('span', { class: `gp-tag sale${short > 0 ? ' cant' : ''}` },
        h('span', { class: 'gp-tag-line' }, h('small', { class: 'gp-tag-k', text: t('career.garage.forSale') }), h('b', { class: 'mono', text: formatMoney(car.price) })),
        h('small', { class: 'gp-tag-need', text: short > 0 ? t('career.garage.need', { price: formatMoney(short) }) : t('career.garage.buyHint') }));
    }
    const carItem: FocusItem = {
      el: h('div', { class: `gp-car${locked ? ' locked' : ''}` },
        arrowButton(-1, () => { changeView(seat, -1); api.sfx('move'); }),
        h('div', { class: 'gp-car-body' },
          h('div', { class: 'gp-car-name', text: car.name }),
          h('div', { class: 'gp-car-visual' }, carSilhouette(car.color)),
          status,
        ),
        arrowButton(1, () => { changeView(seat, 1); api.sfx('move'); }),
      ),
      adjust: (dir) => changeView(seat, dir),
      activate: () => activateCar(seat),
    };

    // Melhorias
    const partItems: FocusItem[] = UPGRADE_PARTS.map((part) => {
      const level = levels[part];
      const cap = partMaxLevel(car.id, part);
      const price = upgradePrice(part, level, car.id);
      const cls = !owned ? ' disabled' : price === null ? ' max' : price > money ? ' cant' : '';
      // Nível que não mudaria nada neste carro (pneus no teto de dirigibilidade) aparece riscado e não se vende.
      const capped = owned && cap < UPGRADE_MAX_LEVEL && level >= cap;
      return {
        el: h('div', { class: `gp-part${cls}` },
          h('span', { class: 'gp-part-name' }, h('b', { text: t(`career.garage.part.${part}`) }), h('small', { text: capped ? t('career.garage.capped') : t(`career.garage.effect.${part}`) })),
          h('span', { class: 'pips' }, Array.from({ length: UPGRADE_MAX_LEVEL }, (_, i) => h('i', { class: i < level ? 'on' : i >= cap ? 'cap' : '' }))),
          h('span', { class: 'gp-part-price mono', text: !owned ? '—' : price === null ? t('career.garage.max') : formatMoney(price) }),
        ),
        activate: () => activatePart(seat, part),
      };
    });

    const readyLabel = career.completed ? t('career.garage.menu') : n === 1 ? t('career.garage.race') : t('career.garage.ready');
    const ready = button(readyLabel, () => toggleReady(seat), `btn-ready gp-ready${ui.ready ? ' on' : ''}${n === 1 || career.completed ? ' btn-primary' : ''}`);
    if (ui.ready) ready.el.prepend(icon('check'));

    const statsHost = h('div', { class: 'gp-stats' });
    const msgEl = h('div', { class: `gp-msg ${ui.msgTtl > 0 ? ui.msgKind : ''}`, text: ui.msgTtl > 0 ? ui.msg : (ui.ready && !allReady() ? t('career.garage.waiting') : '') });
    const items = [carItem, ...partItems, ready];

    const preview = () => {
      const list = lists[seat];
      const focus = list ? list.index : -1;
      let base = shown;
      let after: CarStats | null = null;
      if (focus === 0 && !owned) { base = mine; after = shown; }
      else if (focus >= 1 && focus <= UPGRADE_PARTS.length && owned && !locked) {
        const part = UPGRADE_PARTS[focus - 1];
        if (levels[part] < partMaxLevel(car.id, part)) after = effectiveStats(car, levelsPlus(levels, part));
      }
      statsHost.replaceChildren(statsBlock(base, after));
    };

    const walletLabel = career.coop ? t('career.garage.teamWallet') : t('career.garage.wallet');
    const el = h('div', { class: `gp glass${ui.ready ? ' ready' : ''}`, style: `--seat:${SEAT_COLORS[seat] ?? '#fff'}` },
      h('div', { class: 'gp-head' },
        h('span', { class: 'seat-badge', text: `P${seat + 1}` }),
        h('span', { class: 'gp-name', text: d.name }),
        h('span', { class: 'gp-money' }, h('small', { text: walletLabel }), h('b', { class: 'mono', text: formatMoney(money) })),
      ),
      h('div', { class: 'gp-left' }, carItem.el, statsHost),
      h('div', { class: 'gp-right' },
        h('div', { class: 'gp-parts' }, h('h2', { class: 'sub-title', text: t('career.garage.upgrades') }), partItems.map((p) => p.el)),
        ready.el,
        msgEl,
      ),
    );
    return { el, items, preview, msgEl };
  }

  // ── Cabeçalho e relatório da última corrida ──

  const cupIdx = cupIndexOf(career, ctx.cups);
  const cup = ctx.cups[cupIdx];
  const trackName = (id: string) => ctx.tracks.find((x) => x.id === id)?.name ?? id;

  function headInfo(): string {
    if (career.completed || !cup) return '';
    const raceIndex = career.champ?.raceIndex ?? 0;
    const trackId = career.champ ? nextTrackId(career.champ) : cup.trackIds[0];
    return t('career.garage.next', { track: trackName(trackId ?? cup.trackIds[0]), n: raceIndex + 1, m: cup.trackIds.length });
  }

  function reportBlock(): HTMLElement | null {
    const r = career.lastReport;
    if (career.completed) {
      return h('div', { class: 'garage-report glass' }, h('div', { class: 'verdict good' }, icon('trophy'), h('span', { text: t('career.garage.report.careerDone') })));
    }
    // Boas-vindas só antes da primeira corrida (sem relatório depois disso = relatório descartado pelo save).
    if (!r) return career.racesRun > 0 ? null : h('div', { class: 'garage-report glass' }, h('span', { class: 'gr-line', text: t(n === 1 ? 'career.garage.welcomeSolo' : 'career.garage.welcome') }));
    const lines: HTMLElement[] = [h('strong', { class: 'gr-title', text: t('career.garage.report.title', { track: trackName(r.trackId) }) })];
    for (const row of r.rows) {
      const name = career.drivers[row.driver]?.name ?? `P${row.driver + 1}`;
      lines.push(h('span', { class: 'gr-chip mono', style: `--seat:${SEAT_COLORS[row.driver] ?? '#fff'}`, text: t('career.garage.report.row', { name, pos: row.position, prize: formatMoney(row.prize) }) }));
    }
    if (r.teamBonus > 0) lines.push(h('span', { class: 'gr-chip team mono', text: t('career.garage.report.team', { rank: r.teamRank, prize: formatMoney(r.teamBonus) }) }));
    let banner: HTMLElement | null = null;
    if (r.verdict === 'eliminated') banner = h('div', { class: 'gr-banner bad', text: t('career.garage.report.eliminated', { cup: cupName(r.cupId), n: career.attempts }) });
    else if (r.cupCompleted && cup) banner = h('div', { class: 'gr-banner good' }, icon('trophy'), h('span', { text: t('career.garage.report.cupDone', { cup: cupName(r.cupId), next: `${cup.flag} ${cupName(cup.id)}` }) }));
    return h('div', { class: 'garage-report glass' }, h('div', { class: 'gr-lines' }, lines), banner);
  }

  const panelsEl = h('div', { class: `garage-panels n-${n}` });
  const reportHost = h('div', { class: 'garage-report-host' });
  const el = screenFrame('garage', null,
    h('div', { class: 'garage-head' },
      h('h1', { class: 'screen-title', text: t('career.garage.title') }),
      cup && !career.completed ? h('span', { class: 'chip', text: `${cup.flag} ${cupName(cup.id)}` }) : null,
      h('span', { class: 'garage-progress muted-inline', text: career.completed ? '' : t('career.garage.progress', { i: cupIdx + 1, n: ctx.cups.length }) }),
      h('span', { class: 'garage-next', text: headInfo() }),
    ),
    reportHost,
    panelsEl,
    h('p', { class: 'hint', text: t('career.garage.hint') }),
  );

  function render(): void {
    lists.forEach((l, i) => { if (l && l.index >= 0) uis[i].cursor = l.index; });
    const built = career.drivers.map((_, seat) => panel(seat));
    lists = built.map((b, seat) => createFocusList(b.items, { start: Math.min(uis[seat].cursor, b.items.length - 1), sfx: api.sfx }));
    previews = built.map((b) => b.preview);
    msgEls = built.map((b) => b.msgEl);
    built.forEach((b, seat) => {
      for (const it of b.items) it.el.addEventListener('mousemove', () => { uis[seat].cursor = lists[seat]?.index ?? 0; previews[seat](); });
      b.preview();
    });
    const report = reportBlock();
    reportHost.replaceChildren(...(report ? [report] : []));
    panelsEl.replaceChildren(...built.map((b) => b.el));
  }

  /** Assento do dispositivo; sem ninguém ligado ao P1 (garagem aberta direto), o teclado 1 comanda o P1. */
  function seatFor(device: DeviceId | null): number {
    if (!device) return -1;
    const seat = career.drivers.findIndex((_, i) => ctx.input.seatDevice(i) === device);
    if (seat >= 0) return seat;
    return ctx.input.seatDevice(0) === null && device === 'kb1' ? 0 : -1;
  }

  render();

  return {
    el,
    nav(nav: MenuNav) {
      if (started) return;
      const seat = seatFor(nav.device);
      if (seat < 0) return;
      if (nav.back) {
        if (uis[seat].ready) toggleReady(seat);
        else if (seat === 0) { api.sfx('back'); api.emit({ type: 'toMain' }); }
        return;
      }
      if (nav.start) { toggleReady(seat); return; }
      const list = lists[seat];
      if (!list) return;
      // Confirmar: cada ação toca o próprio som (compra recusada soa diferente de compra feita).
      if (nav.confirm) { list.activate(); return; }
      listNav(list, nav, api.sfx);
      uis[seat].cursor = list.index;
      previews[seat]?.();
    },
    update(dt: number) {
      uis.forEach((ui, seat) => {
        if (ui.msgTtl <= 0) return;
        ui.msgTtl -= dt;
        if (ui.msgTtl <= 0) {
          const m = msgEls[seat];
          if (m) { m.textContent = ui.ready && !allReady() ? t('career.garage.waiting') : ''; m.className = 'gp-msg'; }
        }
      });
    },
  };
}
