// Aviso discreto no canto quando um erro NOVO acontece: some sozinho em 6 s, não recebe clique e não pausa
// nada — o jogo continua. Erro repetido não reabre o aviso (o relator só chama `show` para erro novo).
// O mesmo aviso, no mesmo canto, diz quando o progresso não pôde ser salvo (src/game/save-notice.ts decide quando).
import { t } from '../i18n';
import './errors.css';
import './strings';

const VISIBLE_MS = 6000;
const SAVE_VISIBLE_MS = 9000;

export interface ErrorToast {
  /** Mostra (ou renova) o aviso; `total` é quantos erros há no relatório. */
  show(total: number): void;
  dispose(): void;
}

export interface SaveToast {
  show(): void;
  dispose(): void;
}

function buildToast(parent: HTMLElement, extraClass = '') {
  const el = document.createElement('div');
  el.className = `nc-error-toast ${extraClass}`.trim();
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
    title, hint,
    flash(ms: number) {
      el.classList.add('show');
      if (timer !== undefined) clearTimeout(timer);
      timer = setTimeout(() => el.classList.remove('show'), ms);
    },
    dispose() {
      if (timer !== undefined) clearTimeout(timer);
      el.remove();
    },
  };
}

export function createErrorToast(parent: HTMLElement): ErrorToast {
  const toast = buildToast(parent);
  return {
    show(total) {
      toast.title.textContent = t('errors.toast.title');
      toast.hint.textContent = total > 1 ? `${t('errors.toast.hint')} · ${t('errors.toast.count', { n: total })}` : t('errors.toast.hint');
      toast.flash(VISIBLE_MS);
    },
    dispose: toast.dispose,
  };
}

export function createSaveToast(parent: HTMLElement): SaveToast {
  const toast = buildToast(parent, 'nc-save-toast');
  return {
    show() {
      toast.title.textContent = t('errors.save.title');
      toast.hint.textContent = t('errors.save.hint');
      toast.flash(SAVE_VISIBLE_MS);
    },
    dispose: toast.dispose,
  };
}
