// Aviso discreto no canto quando um erro NOVO acontece: some sozinho em 6 s, não recebe clique e não pausa
// nada — o jogo continua. Erro repetido não reabre o aviso (o relator só chama `show` para erro novo).
import { t } from '../i18n';
import './errors.css';
import './strings';

const VISIBLE_MS = 6000;

export interface ErrorToast {
  /** Mostra (ou renova) o aviso; `total` é quantos erros há no relatório. */
  show(total: number): void;
  dispose(): void;
}

export function createErrorToast(parent: HTMLElement): ErrorToast {
  const el = document.createElement('div');
  el.className = 'nc-error-toast';
  el.setAttribute('role', 'status');
  el.setAttribute('aria-live', 'polite');
  const ico = document.createElement('span');
  ico.className = 'err-ico';
  ico.textContent = '!';
  const body = document.createElement('span');
  const title = document.createElement('span');
  title.className = 'err-title';
  const hint = document.createElement('span');
  hint.className = 'err-hint';
  body.append(title, hint);
  el.append(ico, body);
  parent.appendChild(el);
  let timer: ReturnType<typeof setTimeout> | undefined;

  return {
    show(total) {
      title.textContent = t('errors.toast.title');
      hint.textContent = total > 1 ? `${t('errors.toast.hint')} · ${t('errors.toast.count', { n: total })}` : t('errors.toast.hint');
      el.classList.add('show');
      if (timer !== undefined) clearTimeout(timer);
      timer = setTimeout(() => el.classList.remove('show'), VISIBLE_MS);
    },
    dispose() {
      if (timer !== undefined) clearTimeout(timer);
      el.remove();
    },
  };
}
