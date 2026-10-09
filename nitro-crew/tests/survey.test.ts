// Questionário de 3 perguntas (src/ui/screens/survey.ts): aparece antes do próximo evento do menu, no controle:
// ↓ anda entre as perguntas, ← → escolhem, "Salvar respostas" grava e segue com o evento guardado; Voltar (Esc/B)
// pula e também segue. Aberta sem pedido (layout, playtest), "Salvar" só volta para a tela anterior.
// DOM falso mínimo copiado de tests/paint-screens.test.ts (o que `h`, a lista de foco e a tela usam).
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { DEFAULT_SETTINGS, type MenuEvent, type MenuNav } from '../src/game/contracts';
import { createPlaylog, setActivePlaylog } from '../src/game/playlog';
import type { ScreenApi, ScreenInstance } from '../src/ui/screens/common';
import { surveyScreen } from '../src/ui/screens/survey';
import { fakeDeps, fakeRace } from './playlog-fixtures';

class FakeEl {
  children: unknown[] = [];
  className = '';
  textContent = '';
  title = '';
  innerHTML = '';
  value = '';
  disabled = false;
  style = { setProperty() {} };
  attrs: Record<string, string> = {};
  listeners: Record<string, Array<(ev: unknown) => void>> = {};
  private readonly classes = new Set<string>();
  classList = {
    add: (...c: string[]) => { for (const x of c) this.classes.add(x); },
    remove: (...c: string[]) => { for (const x of c) this.classes.delete(x); },
    toggle: (c: string, force?: boolean) => { const on = force ?? !this.classes.has(c); if (on) this.classes.add(c); else this.classes.delete(c); return on; },
    contains: (c: string) => this.classes.has(c),
  };
  constructor(readonly tag: string) {}
  /** `<template>` de icons.ts: o SVG sai com a marcação que recebeu. */
  get content(): { firstElementChild: FakeEl } {
    const svg = new FakeEl('svg');
    svg.innerHTML = this.innerHTML;
    return { firstElementChild: svg };
  }
  setAttribute(k: string, v: string): void { this.attrs[k] = v; }
  getAttribute(k: string): string | null { return this.attrs[k] ?? null; }
  appendChild<T>(c: T): T { this.children.push(c); return c; }
  append(...c: unknown[]): void { this.children.push(...c); }
  prepend(...c: unknown[]): void { this.children.unshift(...c); }
  replaceChildren(...c: unknown[]): void { this.children = [...c]; }
  addEventListener(type: string, fn: (ev: unknown) => void): void { (this.listeners[type] ??= []).push(fn); }
  removeEventListener(): void { /* não usado */ }
  focus(): void {}
  select(): void {}
  blur(): void {}
  closest(): null { return null; }
}

function all(root: unknown, pred: (el: FakeEl) => boolean, out: FakeEl[] = []): FakeEl[] {
  if (!(root instanceof FakeEl)) return out;
  if (pred(root)) out.push(root);
  for (const c of root.children) all(c, pred, out);
  return out;
}

const hasClass = (cls: string) => (el: FakeEl) => el.className.split(/\s+/).includes(cls);

const NAV: MenuNav = { up: false, down: false, left: false, right: false, confirm: false, back: false, start: false, device: null };
const go = (s: ScreenInstance, key: keyof Omit<MenuNav, 'device'>) => s.nav({ ...NAV, [key]: true, device: 'kb1' });

beforeEach(() => {
  vi.stubGlobal('document', {
    activeElement: null,
    createElement: (tag: string) => new FakeEl(tag),
    createElementNS: (_ns: string, tag: string) => new FakeEl(tag),
    createTextNode: (t: string) => ({ text: t }),
  });
});
afterEach(() => {
  vi.unstubAllGlobals();
  setActivePlaylog(null);
});

/** A tela com a api de mentira; `backs` e `emitted` mostram para onde ela mandou o jogador. */
function open() {
  const emitted: MenuEvent[] = [];
  const state = { backs: 0 };
  const api = {
    ctx: { settings: structuredClone(DEFAULT_SETTINGS) },
    lobby: { mode: 'quick', versus: false, seats: [null, null, null, null] },
    go: () => undefined, back: () => { state.backs++; }, emit: (e: MenuEvent) => { emitted.push(e); }, sfx: () => undefined, refresh: () => undefined,
  } as unknown as ScreenApi;
  return { screen: surveyScreen(api), emitted, state };
}

/** Diário com o pedido feito (3 corridas completas e o próximo evento é `nextRace`), já como o ativo. */
function pendingLog() {
  const log = createPlaylog(fakeDeps().deps);
  log.sessionStart();
  for (let i = 0; i < 3; i++) log.raceEnded(fakeRace());
  log.askSurveyBefore({ type: 'nextRace' }, false);
  setActivePlaylog(log);
  return log;
}

describe('questionário', () => {
  it('três perguntas no controle: ↓ anda, ←→ escolhe, Salvar grava e segue com o evento guardado', () => {
    const log = pendingLog();
    const { screen, emitted } = open();
    expect(all(screen.el, hasClass('sel'))).toHaveLength(3);
    expect(all(screen.el, (e) => e.tag === 'button')).toHaveLength(2);
    for (let i = 0; i < 4; i++) go(screen, 'right'); // 1ª pergunta: 1 → 2 → 3 → 4 (o 1º → vai à opção 1)
    go(screen, 'down');
    for (let i = 0; i < 2; i++) go(screen, 'right'); // difícil: fácil → no ponto
    go(screen, 'down');
    go(screen, 'right'); // o que atrapalhou: nada
    go(screen, 'down'); // Salvar respostas
    go(screen, 'confirm');
    expect(log.data().surveys[0]).toMatchObject({ reason: 'races', skipped: false, again: 4, difficulty: 'right', hurdle: 'none' });
    expect(emitted).toEqual([{ type: 'nextRace' }]);
  });

  it('Voltar (Esc/B) pula: grava como pulado e segue', () => {
    const log = pendingLog();
    const { screen, emitted } = open();
    go(screen, 'back');
    expect(log.data().surveys[0]).toMatchObject({ skipped: true, again: null, difficulty: null, hurdle: null });
    expect(emitted).toEqual([{ type: 'nextRace' }]);
  });

  it('aberta sem pedido (layout, playtest): Salvar volta para a tela anterior', () => {
    const log = createPlaylog(fakeDeps().deps);
    log.sessionStart();
    setActivePlaylog(log);
    const { screen, emitted, state } = open();
    for (let i = 0; i < 3; i++) go(screen, 'down');
    go(screen, 'confirm');
    expect(log.data().surveys).toHaveLength(0);
    expect(state.backs).toBe(1);
    expect(emitted).toEqual([]);
  });
});
