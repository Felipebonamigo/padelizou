// Tela de erro fatal: o jogo não chegou a começar (o caso real é o WebGL recusado por driver ou GPU na lista de
// bloqueio do Chromium). Em vez de uma janela preta, explica o que houve e oferece copiar o relatório e tentar
// de novo. DOM puro, sem os menus — eles podem ser justamente o que falhou.
import { copyToClipboard, currentReportText, describeError } from '../game/errors';
import { loadSettings } from '../game/settings';
import { setLanguage, t } from '../i18n';
import './errors.css';
import './strings';

export function isWebGlFailure(message: string): boolean {
  return /webgl/i.test(message);
}

export function showFatal(parent: HTMLElement, err: unknown): HTMLElement {
  setLanguage(loadSettings().language);
  const { message } = describeError(err);
  const root = document.createElement('div');
  root.className = 'nc-fatal';
  root.setAttribute('role', 'alertdialog');
  const panel = document.createElement('div');
  panel.className = 'nc-fatal-panel';
  const title = document.createElement('h1');
  title.textContent = t('errors.fatal.title');
  const text = document.createElement('p');
  text.textContent = isWebGlFailure(message) ? t('errors.fatal.webgl') : t('errors.fatal.generic');
  const detail = document.createElement('pre');
  detail.textContent = message;
  const actions = document.createElement('div');
  actions.className = 'nc-fatal-actions';
  const copy = document.createElement('button');
  copy.type = 'button';
  copy.className = 'btn btn-primary';
  copy.textContent = t('errors.fatal.copy');
  copy.addEventListener('click', () => {
    void copyToClipboard(currentReportText({ stage: 'boot' })).then((ok) => {
      copy.textContent = ok ? t('errors.options.copied') : t('errors.options.copyFailed');
      copy.classList.toggle('ok', ok);
    });
  });
  const retry = document.createElement('button');
  retry.type = 'button';
  retry.className = 'btn';
  retry.textContent = t('errors.fatal.retry');
  retry.addEventListener('click', () => location.reload());
  actions.append(copy, retry);
  panel.append(title, text, detail, actions);
  root.appendChild(panel);
  parent.appendChild(root);
  copy.focus();
  return root;
}
