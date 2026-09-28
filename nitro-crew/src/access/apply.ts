// Aplica as opções de acessibilidade que são só de apresentação: variáveis CSS e classes no #hud
// (tamanho do HUD, reduzir efeitos) e no #ui (texto grande). A sessão chama na criação e a cada
// mudança de opções. As regras que usam isto moram em access.css.
import './access.css';
import './strings';
import type { Settings } from '../game/contracts';

/** Fator do texto dos menus com "Texto grande". */
export const LARGE_TEXT_SCALE = 1.2;

type Styled = Pick<HTMLElement, 'style' | 'classList'>;

/** Nunca lança: sem DOM de verdade (sessão nos testes em Node), não faz nada. */
export function applyAccessibility(settings: Pick<Settings, 'hudScale' | 'largeText' | 'reduceEffects'>, hudRoot: Styled, uiRoot: Styled): void {
  const set = (root: Styled, prop: string, value: string) => root.style?.setProperty(prop, value);
  const toggle = (root: Styled, cls: string, on: boolean) => root.classList?.toggle(cls, on);
  set(hudRoot, '--hud-scale', String(settings.hudScale));
  set(uiRoot, '--text-scale', String(settings.largeText ? LARGE_TEXT_SCALE : 1));
  toggle(uiRoot, 'large-text', settings.largeText);
  toggle(hudRoot, 'reduce-fx', settings.reduceEffects);
  toggle(uiRoot, 'reduce-fx', settings.reduceEffects);
}
