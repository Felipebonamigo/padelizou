// Seleção de copa e de pista, depois do lobby (docs/TELAS.md). Copas em duas etapas (onda G): a Expedição Brasil,
// 27 copas de 3 pistas agrupadas pelas 5 regiões, e o Mundial, 7 copas de 4. A tela de copas tem uma aba por região
// e uma do Mundial (←→ trocam de aba), com a lista da aba e o detalhe da copa em foco ao lado. A de pistas é uma
// grade de quatro colunas com uma seção por copa (de 3 ou de 4 pistas), que rola acompanhando o foco (↑↓ trocam de
// copa, ←→ de pista).
import { formatTicks } from '../../core/sim/race';
import { BRAZIL_REGIONS } from '../../core/data/cups';
import type { BrazilRegion, CupDef, CupStage, TrackDef } from '../../core/types';
import type { MenuContext } from '../../game/contracts';
import { t } from '../../i18n';
import { blurActive, countryName, createFocusList, dayIcon, dots, h, lapsText, listNav, screenFrame, trackName, trackThumb, type FocusItem, type FocusList, type ScreenApi, type ScreenInstance } from './common';
import { icon } from './icons';
import { lobbyHumans } from './lobby';
import { cupRivalBlock } from './rival';
import './passport.css';
import './select.css';

/**
 * Colunas da grade de pistas: uma copa por linha, e por isso ↑↓ trocam de copa mantendo a coluna (presa à última
 * pista numa copa de 3: `raggedGridMove`). Nenhuma copa pode ter mais pistas que isto (tests/select.test.ts confere).
 */
export const TRACK_GRID_COLS = 4;

/** Linhas da grade de pistas: quantas pistas cada copa mostra, na ordem das copas. */
export function trackGridRows(ctx: Pick<MenuContext, 'tracks'>, cups: readonly CupDef[]): number[] {
  return cups.map((cup) => cupTracks(ctx, cup).length);
}

/**
 * Miniaturas desenhadas neste tamanho (px) e reduzidas pelo CSS para `calc(N * var(--u))`: ficam
 * nítidas de 720p a 1440p sem redesenhar quando a janela muda de tamanho.
 */
const THUMB_DRAW_PX = 112;

function scalableThumb(ctx: MenuContext, def: TrackDef): HTMLCanvasElement {
  const c = trackThumb(ctx, def, THUMB_DRAW_PX);
  c.style.removeProperty('width');
  c.style.removeProperty('height');
  return c;
}

/** Pistas da copa, na ordem da copa (ids desconhecidos ficam de fora). */
export function cupTracks(ctx: Pick<MenuContext, 'tracks'>, cup: CupDef): TrackDef[] {
  return cup.trackIds.map((id) => ctx.tracks.find((x) => x.id === id)).filter((x): x is TrackDef => x !== undefined);
}

export type CupStatus = 'done' | 'open' | 'locked';

export function cupStatus(ctx: Pick<MenuContext, 'isCupUnlocked' | 'save'>, cup: CupDef): CupStatus {
  if (!ctx.isCupUnlocked(cup.id)) return 'locked';
  return ctx.save.cupsCompleted.includes(cup.id) ? 'done' : 'open';
}

/**
 * Onde o cursor começa na tela de copas: a primeira copa aberta e ainda não concluída (a
 * "fronteira" do jogador); se todas as abertas já foram concluídas, a última aberta.
 */
export function frontierCupIndex(statuses: readonly CupStatus[]): number {
  const open = statuses.indexOf('open');
  if (open >= 0) return open;
  const lastDone = statuses.lastIndexOf('done');
  return lastDone >= 0 ? lastDone : 0;
}

/** Dificuldade média da copa, arredondada para os cinco pontos. */
export function cupDifficulty(tracks: readonly TrackDef[]): number {
  if (tracks.length === 0) return 0;
  return Math.round(tracks.reduce((a, d) => a + d.difficulty, 0) / tracks.length);
}

/**
 * Voltas que o cartão da pista mostra: as da corrida que ele larga. Corrida rápida e contra-relógio
 * usam as voltas da corrida rápida (session.ts: startQuick/startTimeTrial), não as da pista — só a
 * copa corre as voltas da pista.
 */
export function trackCardLaps(ctx: Pick<MenuContext, 'settings'>): number {
  return ctx.settings.quickLaps;
}

function statusBadge(status: CupStatus): HTMLElement | null {
  if (status === 'done') return h('span', { class: 'cup-status done', title: t('ui.cups.done') }, icon('check'));
  if (status === 'locked') return h('span', { class: 'cup-status locked' }, icon('lock'));
  return null;
}

function bestLapText(ctx: MenuContext, id: string): string {
  const best = ctx.save.bestLaps[id];
  return best ? formatTicks(best.ticks) : '—';
}

// ───────────────────────────── Copas: Expedição Brasil (5 regiões) e Mundial ─────────────────────────────

/** Uma aba da tela de copas: uma região da Expedição Brasil, ou o Mundial. */
export interface CupTab {
  id: BrazilRegion | 'mundial';
  stage: CupStage;
  cups: CupDef[];
}

/** As abas na ordem da expedição: as 5 regiões (com os estados na ordem de BRAZIL_REGIONS) e o Mundial por último. */
export function cupTabs(cups: readonly CupDef[]): CupTab[] {
  const tabs: CupTab[] = BRAZIL_REGIONS.map((r) => ({
    id: r.id, stage: 'brasil' as const,
    cups: r.states.map((st) => cups.find((c) => c.stage === 'brasil' && c.state === st)).filter((c): c is CupDef => c !== undefined),
  }));
  // Copa da Expedição sem região conhecida (não deve existir) não some: vai para o fim da primeira aba.
  const placed = new Set(tabs.flatMap((x) => x.cups.map((c) => c.id)));
  for (const c of cups) if (c.stage === 'brasil' && !placed.has(c.id)) tabs[0].cups.push(c);
  tabs.push({ id: 'mundial', stage: 'mundial', cups: cups.filter((c) => c.stage !== 'brasil') });
  return tabs;
}

/** Aba e copa onde o cursor começa: a fronteira do jogador (frontierCupIndex) sobre todas as copas, aba por aba. */
export function startTab(tabs: readonly CupTab[], ctx: Pick<MenuContext, 'isCupUnlocked' | 'save'>): { tab: number; cup: number } {
  const flat = tabs.flatMap((tab, ti) => tab.cups.map((cup, ci) => ({ ti, ci, status: cupStatus(ctx, cup) })));
  const at = flat[frontierCupIndex(flat.map((x) => x.status))];
  return at ? { tab: at.ti, cup: at.ci } : { tab: 0, cup: 0 };
}

function tabLabel(tab: CupTab): string {
  return tab.id === 'mundial' ? t('core.stage.mundial') : t(`core.region.${tab.id}`);
}

/** Carimbo pequeno com a sigla do estado (lista e detalhe das copas; o passaporte usa o grande). */
export function ufBadge(cup: CupDef, stamped: boolean, cls = ''): HTMLElement {
  return h('span', { class: `uf-badge${stamped ? ' on' : ''} ${cls}`.trim(), attrs: { 'data-region': cup.region ?? '' }, text: cup.state ?? '' });
}

function isStamped(ctx: MenuContext, cup: CupDef): boolean {
  return cup.stage === 'brasil' && !!cup.state && ctx.save.stamps.includes(cup.state);
}

function cupDetail(ctx: MenuContext, cup: CupDef, status: CupStatus): HTMLElement {
  const tracks = cupTracks(ctx, cup);
  const brasil = cup.stage === 'brasil';
  let chip: HTMLElement;
  if (status === 'locked') {
    const required = cup.requires ? t(`core.cup.${cup.requires}`) : '';
    chip = h('span', { class: 'cup-chip locked' }, icon('lock'), h('span', { text: t('ui.cups.locked', { cup: required }) }));
  } else if (status === 'done') {
    chip = h('span', { class: 'cup-chip done' }, icon('trophy'), h('span', { text: brasil ? t('ui.cups.stamped') : t('ui.cups.done') }));
  } else {
    chip = h('span', { class: 'cup-chip open' }, icon('flag'), h('span', { text: t('ui.cups.open') }));
  }
  const where = brasil && cup.region ? t(`core.region.${cup.region}`) : countryName(cup.country);
  return h('div', { class: `cup-detail glass${status === 'locked' ? ' locked' : ''}` },
    h('div', { class: 'cup-detail-head' },
      brasil ? ufBadge(cup, isStamped(ctx, cup), 'big') : h('span', { class: 'cup-detail-flag', text: cup.flag }),
      h('div', { class: 'cup-detail-title' },
        h('strong', { text: t(`core.cup.${cup.id}`) }),
        h('span', { class: 'meta' },
          h('span', { text: where }),
          h('span', { class: 'sep', text: '·' }),
          h('span', { text: t('ui.cups.races', { n: tracks.length }) }),
          h('span', { class: 'sep', text: '·' }),
          dots(cupDifficulty(tracks)),
        ),
        chip,
      ),
    ),
    cupRivalBlock(cup.id),
    h('ol', { class: 'cup-detail-tracks' }, tracks.map((def, i) => h('li', { class: 'cup-race' },
      h('span', { class: 'cup-race-n mono', text: String(i + 1) }),
      scalableThumb(ctx, def),
      h('div', { class: 'cup-race-text' },
        h('span', { class: 'cup-race-name', text: trackName(def.id, def.name) }),
        h('span', { class: 'meta' },
          dayIcon(def.timeOfDay), h('span', { text: t(`core.time.${def.timeOfDay}`) }),
          h('span', { class: 'sep', text: '·' }),
          h('span', { text: lapsText(def.laps) }),
        ),
      ),
      h('span', { class: 'cup-race-side' },
        dots(def.difficulty),
        h('span', { class: 'track-best mono' }, icon('timer'), h('span', { text: bestLapText(ctx, def.id) })),
      ),
    ))),
  );
}

export function cupsScreen(api: ScreenApi): ScreenInstance {
  const { ctx } = api;
  const tabs = cupTabs(ctx.cups);
  const start = startTab(tabs, ctx);
  let tabIndex = start.tab;
  const brasilCups = tabs.filter((x) => x.stage === 'brasil').flatMap((x) => x.cups);

  // Barra de abas em duas etapas: Expedição Brasil (as 5 regiões, com os carimbos de cada uma) e Mundial.
  const tabEls = tabs.map((tab, i) => {
    let count: HTMLElement;
    if (tab.stage === 'brasil') {
      const n = tab.cups.filter((c) => isStamped(ctx, c)).length;
      count = h('span', { class: 'cup-tab-count mono', text: `${n}/${tab.cups.length}` });
    } else if (tab.cups[0] && cupStatus(ctx, tab.cups[0]) === 'locked') {
      count = h('span', { class: 'cup-tab-count' }, icon('lock'));
    } else {
      const n = tab.cups.filter((c) => cupStatus(ctx, c) === 'done').length;
      count = h('span', { class: 'cup-tab-count mono', text: `${n}/${tab.cups.length}` });
    }
    // A aba do Mundial fica embaixo do nome da etapa: diz quantos países, não "Mundial" de novo.
    const name = tab.stage === 'mundial' ? t('ui.cups.countries', { n: tab.cups.length }) : tabLabel(tab);
    return h('button', {
      class: `cup-tab${tab.stage === 'mundial' ? ' mundial' : ''}`,
      attrs: { type: 'button', 'data-tab': tab.id },
      on: { click: () => { blurActive(); if (selectTab(i)) api.sfx('move'); } },
    }, h('span', { class: 'cup-tab-name', text: name }), count);
  });
  const stamps = brasilCups.filter((c) => isStamped(ctx, c)).length;
  const bar = h('div', { class: 'cup-tabs' },
    h('div', { class: 'cup-stage' },
      h('span', { class: 'cup-stage-name' },
        h('span', { text: t('core.stage.brasil') }),
        h('span', { class: 'cup-stage-count', text: t('core.passport.stamps', { n: stamps, total: brasilCups.length }) }),
      ),
      h('div', { class: 'cup-stage-tabs' }, tabEls.filter((_, i) => tabs[i].stage === 'brasil')),
    ),
    h('div', { class: 'cup-stage mundial' },
      h('span', { class: 'cup-stage-name' }, h('span', { text: t('core.stage.mundial') })),
      h('div', { class: 'cup-stage-tabs' }, tabEls.filter((_, i) => tabs[i].stage !== 'brasil')),
    ),
  );

  const rowsBox = h('div', { class: 'cup-rows glass' });
  const detailSlot = h('div', { class: 'cup-detail-slot' });
  let list: FocusList = createFocusList([], { sfx: api.sfx });
  let tabCups: CupDef[] = [];
  let statuses: CupStatus[] = [];
  let shown = -1;

  function buildTab(focus: number | null): void {
    const tab = tabs[tabIndex];
    tabCups = tab.cups;
    statuses = tabCups.map((cup) => cupStatus(ctx, cup));
    tabEls.forEach((el, i) => el.classList.toggle('active', i === tabIndex));
    const items: FocusItem[] = tabCups.map((cup, i) => {
      const status = statuses[i];
      const el = h('div', { class: `cup-row${status === 'locked' ? ' locked' : ''}`, attrs: { 'data-cup': cup.id } },
        cup.stage === 'brasil' ? ufBadge(cup, isStamped(ctx, cup)) : h('span', { class: 'cup-flag', text: cup.flag }),
        h('span', { class: 'cup-row-name', text: t(`core.cup.${cup.id}`) }),
        statusBadge(status),
      );
      return {
        el,
        activate: () => {
          if (status === 'locked') { api.sfx('back'); return; }
          api.emit({ type: 'startCup', cupId: cup.id, humans: lobbyHumans(api) });
        },
      };
    });
    // Mundial fechado: diz por quê em cima da lista (o detalhe de cada copa diz qual concluir).
    const first = tabCups[0];
    const lockNote = tab.stage === 'mundial' && first && statuses[0] === 'locked'
      ? h('p', { class: 'cup-stage-lock' }, icon('lock'), h('span', { text: t('ui.cups.mundialLocked', { cup: first.requires ? t(`core.cup.${first.requires}`) : '' }) }))
      : null;
    rowsBox.replaceChildren(...(lockNote ? [lockNote] : []), ...items.map((i) => i.el));
    list = createFocusList(items, { sfx: api.sfx, start: focus ?? frontierCupIndex(statuses) });
    shown = -1;
    sync();
  }

  function selectTab(i: number): boolean {
    const next = Math.max(0, Math.min(tabs.length - 1, i));
    if (next === tabIndex) return false;
    tabIndex = next;
    buildTab(null);
    return true;
  }

  // O detalhe acompanha o cursor, venha ele do teclado, do controle ou do mouse.
  function sync(): void {
    if (list.index === shown || list.index < 0) return;
    shown = list.index;
    detailSlot.replaceChildren(cupDetail(ctx, tabCups[shown], statuses[shown]));
  }
  buildTab(start.cup);

  const el = screenFrame('cups', t('ui.cups.title'),
    bar,
    h('div', { class: 'cups-layout' }, rowsBox, detailSlot),
    h('p', { class: 'hint', text: t('ui.cups.hint') }),
  );
  return {
    el,
    nav: (nav) => {
      // ← → trocam de aba (região/Mundial); ↑ ↓ andam na lista da aba.
      if (nav.left && selectTab(tabIndex - 1)) api.sfx('move');
      if (nav.right && selectTab(tabIndex + 1)) api.sfx('move');
      listNav(list, { ...nav, left: false, right: false }, api.sfx, () => api.back());
      sync();
    },
    update: sync,
  };
}

export function tracksScreen(api: ScreenApi): ScreenInstance {
  const { ctx, lobby } = api;
  const timeTrial = lobby.mode === 'timetrial';
  const items: FocusItem[] = [];
  const rows: HTMLElement[] = [];
  // As copas na ordem das abas da tela de copas (região por região, depois o Mundial), com o nome da etapa e da
  // região em cima da primeira copa de cada uma.
  const tabs = cupTabs(ctx.cups);
  const cups = tabs.flatMap((x) => x.cups);
  for (const tab of tabs) {
    tab.cups.forEach((cup, ci) => {
      const cards = cupTracks(ctx, cup).map((def) => {
        const el = h('div', { class: 'track-card glass', attrs: { 'data-track': def.id } },
          scalableThumb(ctx, def),
          h('div', { class: 'track-info' },
            h('strong', { class: 'track-name', text: trackName(def.id, def.name) }),
            h('span', { class: 'meta' }, dayIcon(def.timeOfDay), h('span', { text: lapsText(trackCardLaps(ctx)) })),
            h('span', { class: 'track-foot' },
              dots(def.difficulty),
              h('span', { class: 'track-best mono' }, icon('timer'), h('span', { text: bestLapText(ctx, def.id) })),
            ),
          ),
        );
        const item: FocusItem = {
          el,
          activate: () => {
            const humans = lobbyHumans(api);
            if (timeTrial) api.emit({ type: 'startTimeTrial', trackId: def.id, humans });
            else if (lobby.mode === 'escort' || lobby.mode === 'relay') api.emit({ type: 'startParty', mode: lobby.mode, trackId: def.id, laps: ctx.settings.quickLaps, humans });
            else api.emit({ type: 'startQuick', trackId: def.id, laps: ctx.settings.quickLaps, humans });
          },
        };
        items.push(item);
        return el;
      });
      const group = ci === 0
        ? h('div', { class: 'track-group-head' },
          h('span', { text: t(`core.stage.${tab.stage}`) }),
          tab.stage === 'brasil' ? h('span', { class: 'track-group-region', text: tabLabel(tab) }) : null)
        : null;
      rows.push(h('section', { class: 'track-section', attrs: { 'data-cup': cup.id } },
        group,
        // Bandeira (ou a sigla do estado) e copa: todo nome de copa já leva o lugar ("Copa Bahia").
        h('h2', { class: 'track-section-head' },
          cup.stage === 'brasil' ? ufBadge(cup, isStamped(ctx, cup)) : h('span', { class: 'cup-flag', text: cup.flag }),
          h('span', { class: 'track-section-cup', text: t(`core.cup.${cup.id}`) }),
        ),
        h('div', { class: 'track-grid', style: `--cols:${Math.max(1, cards.length)}` }, cards),
      ));
    });
  }
  const list = createFocusList(items, { rows: trackGridRows(ctx, cups), sfx: api.sfx });
  const el = screenFrame('tracks', t('ui.tracks.title'),
    h('div', { class: 'track-scroll' }, rows),
    h('p', { class: 'hint', text: t('ui.tracks.hint') }),
  );
  return { el, nav: (nav) => listNav(list, nav, api.sfx, () => api.back()) };
}
