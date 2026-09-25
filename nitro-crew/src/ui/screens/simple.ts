// Telas simples: título, menu principal, pausa, créditos e carregando.
import type { MenuScreen, RaceMode } from '../../game/contracts';
import { t } from '../../i18n';
import { button, createFocusList, h, listNav, screenFrame, type FocusItem, type ScreenApi, type ScreenInstance } from './common';

function wordmark(cls: string): HTMLElement {
  return h('div', { class: `wordmark ${cls}`.trim() },
    h('span', { class: 'wm-nitro', text: 'NITRO' }),
    h('span', { class: 'wm-crew', text: 'CREW' }),
  );
}

export function titleScreen(api: ScreenApi): ScreenInstance {
  const enter = () => { api.sfx('confirm'); api.go('main'); };
  const el = screenFrame('title', null,
    h('div', { class: 'title-center' },
      wordmark('wordmark-big'),
      h('p', { class: 'title-sub', text: t('ui.title.subtitle') }),
    ),
    h('p', { class: 'title-press pulse', text: t('ui.title.press') }),
    h('p', { class: 'title-foot', text: t('ui.title.foot') }),
  );
  el.addEventListener('click', enter);
  return {
    el,
    nav(nav) {
      if (nav.confirm || nav.start) enter();
    },
  };
}

export function mainScreen(api: ScreenApi): ScreenInstance {
  const mode = (m: RaceMode) => () => {
    api.lobby.mode = m;
    // Lobby novo: quem continua sentado desde a última corrida volta como "não pronto" (senão
    // um Enter distraído no menu já cairia em INICIAR).
    for (const seat of api.lobby.seats) if (seat) { seat.ready = false; seat.cursor = 1; }
    api.go('lobby');
  };
  const open = (s: MenuScreen) => () => api.go(s);
  const entries: Array<{ label: string; hint: string; run: () => void; cls?: string }> = [
    { label: t('ui.main.cup'), hint: t('ui.main.hint.cup'), run: mode('cup') },
    { label: t('ui.main.quick'), hint: t('ui.main.hint.quick'), run: mode('quick') },
    { label: t('ui.main.timetrial'), hint: t('ui.main.hint.timetrial'), run: mode('timetrial') },
    { label: t('ui.main.records'), hint: t('ui.main.hint.records'), run: open('records') },
    { label: t('ui.main.options'), hint: t('ui.main.hint.options'), run: open('options') },
    { label: t('ui.main.controls'), hint: t('ui.main.hint.controls'), run: open('controls') },
    { label: t('ui.main.credits'), hint: t('ui.main.hint.credits'), run: open('credits') },
  ];
  if (api.ctx.isDesktop) entries.push({ label: t('ui.main.quit'), hint: t('ui.main.hint.quit'), run: () => api.emit({ type: 'quitApp' }), cls: 'btn-quit' });
  const items: FocusItem[] = entries.map((e) => button(e.label, e.run, `btn-main ${e.cls ?? ''}`.trim()));
  const heroTitle = h('h2', { class: 'main-hero-title', text: entries[0].label });
  const heroHint = h('p', { class: 'main-hint', text: entries[0].hint });
  const list = createFocusList(items, { sfx: api.sfx });
  const syncHint = () => {
    const e = entries[list.index];
    if (!e) return;
    heroTitle.textContent = e.label;
    heroHint.textContent = e.hint;
  };
  for (const it of items) it.el.addEventListener('mousemove', syncHint);
  const el = screenFrame('main', null,
    h('div', { class: 'main-layout' },
      h('div', { class: 'main-left glass' },
        wordmark('wordmark-small'),
        h('div', { class: 'menu-list' }, items.map((i) => i.el)),
      ),
      h('div', { class: 'main-right' }, heroTitle, heroHint),
    ),
  );
  return {
    el,
    nav(nav) {
      listNav(list, nav, api.sfx, () => api.go('title'));
      syncHint();
    },
  };
}

export function pauseScreen(api: ScreenApi): ScreenInstance {
  const items: FocusItem[] = [
    button(t('ui.pause.resume'), () => api.emit({ type: 'resume' }), 'btn-primary'),
    button(t('ui.pause.restart'), () => api.emit({ type: 'restart' })),
    button(t('ui.pause.options'), () => api.go('options')),
    button(t('ui.pause.quit'), () => api.emit({ type: 'toMain' }), 'btn-danger'),
  ];
  const list = createFocusList(items, { sfx: api.sfx });
  const el = screenFrame('pause', null,
    h('div', { class: 'pause-panel glass' },
      h('h1', { class: 'screen-title', text: t('ui.pause.title') }),
      h('div', { class: 'menu-list' }, items.map((i) => i.el)),
    ),
  );
  return {
    el,
    nav(nav) {
      listNav(list, nav, api.sfx, () => api.emit({ type: 'resume' }));
    },
  };
}

export function creditsScreen(api: ScreenApi): ScreenInstance {
  const back = button(t('ui.common.back'), () => api.back());
  const list = createFocusList([back], { sfx: api.sfx });
  const el = screenFrame('credits', t('ui.credits.title'),
    h('div', { class: 'credits-body glass' },
      wordmark('wordmark-small'),
      h('p', { class: 'credits-big', text: t('ui.credits.by') }),
      h('p', { text: t('ui.credits.inspired') }),
      h('p', { text: t('ui.credits.tech') }),
      h('p', { class: 'credits-small', text: t('ui.credits.assets') }),
    ),
    h('div', { class: 'actions' }, back.el),
  );
  return { el, nav: (nav) => listNav(list, nav, api.sfx, () => api.back()) };
}

export function loadingScreen(): ScreenInstance {
  const el = screenFrame('loading', null, h('p', { class: 'loading-text pulse', text: t('ui.loading') }));
  return { el, nav: () => undefined };
}
