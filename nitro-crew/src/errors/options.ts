// Os dois itens do relatório de erros na tela de Opções, no rodapé, na mesma linha do "Voltar":
//   [ Copiar relatório de erros · 1 erro ]   [ VOLTAR ]   [ Telemetria anônima ‹ Desligado › ]
// No rodapé e não numa coluna porque a tela já ocupa a altura toda em 1600×900: uma linha a mais fazia a tela
// rolar e escondia o "Voltar" (medido com scratch/measure-options.mjs de 1024×640 a 2560×1440).
//   • Copiar relatório — Enter/A/clique copia; o valor mostra quantos erros há (ao vivo) e, por 2,5 s,
//     "Copiado ✓" ou "Não deu para copiar".
//   • Telemetria anônima — liga/desliga (padrão desligado). Hoje só grava a preferência: não há servidor
//     (src/game/errors.ts, `telemetryEndpoint` nulo).
import { copyToClipboard, currentReportText, getActiveReporter } from '../game/errors';
import { t } from '../i18n';
import { h, onOff, selector, type FocusItem, type ScreenApi } from '../ui/screens/common';
import './errors.css';
import './strings';

const FEEDBACK_MS = 2500;

export function errorCountText(n: number): string {
  if (n === 0) return t('errors.options.none');
  return n === 1 ? t('errors.options.one') : t('errors.options.many', { n });
}

function reportItem(api: ScreenApi): FocusItem {
  const s = api.ctx.settings;
  const count = () => errorCountText(getActiveReporter()?.entries().length ?? 0);
  const value = h('span', { class: 'sel-value', text: count() });
  const el = h('div', { class: 'sel sel-action', attrs: { 'data-item': 'error-report' } },
    h('span', { class: 'sel-label', text: t('errors.options.report') }),
    h('span', { class: 'sel-box' }, value),
  );
  let timer: ReturnType<typeof setTimeout> | undefined;
  // Erro que acontece com a tela aberta atualiza a contagem; tela fechada, a assinatura se desfaz sozinha.
  const unsubscribe = getActiveReporter()?.subscribe(() => {
    if (!el.isConnected) { unsubscribe?.(); return; }
    if (timer === undefined) value.textContent = count();
  });
  return {
    el,
    activate: () => {
      const text = currentReportText({ language: s.language, quality: s.quality, telemetry: s.telemetry ? 'on' : 'off' });
      void copyToClipboard(text).then((ok) => {
        value.textContent = ok ? t('errors.options.copied') : t('errors.options.copyFailed');
        value.classList.toggle('ok', ok);
        value.classList.toggle('fail', !ok);
        if (timer !== undefined) clearTimeout(timer);
        timer = setTimeout(() => { timer = undefined; value.textContent = count(); value.classList.remove('ok', 'fail'); }, FEEDBACK_MS);
      });
    },
  };
}

/** Rodapé da tela de Opções: relatório à esquerda, `back` no meio, telemetria à direita (ordem do cursor também). */
export function optionsFooter(api: ScreenApi, commit: () => void, back: FocusItem): { el: HTMLElement; items: FocusItem[] } {
  const s = api.ctx.settings;
  const report = reportItem(api);
  const telemetry = selector(t('errors.options.telemetry'), () => onOff(s.telemetry), () => { s.telemetry = !s.telemetry; commit(); }, { sfx: api.sfx });
  telemetry.el.dataset.item = 'telemetry';
  const el = h('div', { class: 'options-footer' }, report.el, back.el, telemetry.el);
  return { el, items: [report, back, telemetry] };
}
