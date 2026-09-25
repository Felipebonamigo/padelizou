// Telas simples: título, menu principal, pausa, créditos e carregando.
import type { MenuScreen, RaceMode } from '../../game/contracts';
import { t } from '../../i18n';
import { button, createFocusList, h, listNav, screenFrame, type FocusItem, type ScreenApi, type ScreenInstance } from './common';

export function titleScreen(api: ScreenApi): ScreenInstance {
  const enter = () => { api.sfx('confirm'); api.go('main'); };
  const el = screenFrame('title', null,
    h('div', { class: 'title-logo' },
      h('span', { class: 'title-nitro', text: 'NITRO' }),
      h('span', { class: 'title-crew', text: 'CREW' }),
    ),
    h('p', { class: 'title-sub', text: t('ui.title.subtitle') }),
    h('p', { class: 'title-press blink', text: t('ui.title.press') }),
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
  const items: FocusItem[] = [
    button(t('ui.main.cup'), mode('cup'), 'btn-main'),
    button(t('ui.main.quick'), mode('quick'), 'btn-main'),
    button(t('ui.main.timetrial'), mode('timetrial'), 'btn-main'),
    button(t('ui.main.records'), open('records'), 'btn-main'),
    button(t('ui.main.options'), open('options'), 'btn-main'),
    button(t('ui.main.controls'), open('controls'), 'btn-main'),
    button(t('ui.main.credits'), open('credits'), 'btn-main'),
  ];
  if (api.ctx.isDesktop) items.push(button(t('ui.main.quit'), () => api.emit({ type: 'quitApp' }), 'btn-main btn-quit'));
  const hints = [t('ui.main.hint.cup'), t('ui.main.hint.quick'), t('ui.main.hint.timetrial'), t('ui.main.hint.records'), t('ui.main.hint.options'), t('ui.main.hint.controls'), t('ui.main.hint.credits'), t('ui.main.hint.quit')];
  const hint = h('p', { class: 'main-hint', text: hints[0] });
  const list = createFocusList(items, { sfx: api.sfx });
  const syncHint = () => { hint.textContent = hints[list.index] ?? ''; };
  for (const it of items) it.el.addEventListener('mousemove', syncHint);
  const el = screenFrame('main', null,
    h('div', { class: 'main-logo' }, h('span', { class: 'title-nitro', text: 'NITRO' }), h('span', { class: 'title-crew', text: 'CREW' })),
    h('div', { class: 'menu-list' }, items.map((i) => i.el)),
    hint,
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
    button(t('ui.pause.resume'), () => api.emit({ type: 'resume' })),
    button(t('ui.pause.restart'), () => api.emit({ type: 'restart' })),
    button(t('ui.pause.options'), () => api.go('options')),
    button(t('ui.pause.quit'), () => api.emit({ type: 'toMain' }), 'btn-danger'),
  ];
  const list = createFocusList(items, { sfx: api.sfx });
  const el = screenFrame('pause', t('ui.pause.title'), h('div', { class: 'menu-list' }, items.map((i) => i.el)));
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
    h('div', { class: 'credits-body' },
      h('p', { class: 'credits-big', text: t('ui.credits.by') }),
      h('p', { text: t('ui.credits.inspired') }),
      h('p', { text: t('ui.credits.tech') }),
      h('p', { class: 'credits-small', text: t('ui.credits.assets') }),
    ),
    back.el,
  );
  return { el, nav: (nav) => listNav(list, nav, api.sfx, () => api.back()) };
}

export function loadingScreen(): ScreenInstance {
  const el = screenFrame('loading', null, h('p', { class: 'loading-text blink', text: t('ui.loading') }));
  return { el, nav: () => undefined };
}
