// Questionário de 3 perguntas (docs/ondas/K/K2.md): "Jogaria de novo amanhã?", a dificuldade e o que mais atrapalhou.
// Abre ANTES do próximo evento do menu (a sessão pergunta ao diário, `askSurveyBefore`, na 1ª linha de handleMenuEvent):
// depois da 3ª corrida completa, ou ao sair do jogo, uma vez por sessão e por dia. Tudo no controle: ↓ anda entre as
// perguntas, ← → escolhem, "Salvar respostas" grava e segue com o evento guardado; Voltar (Esc/B) pula e também
// segue. As respostas só vão para o diário local (src/game/playlog.ts) e para o relatório que o jogador copiar.
// Aberta sem pedido (playtest-layout, `menus.show('survey')`), não grava nada e "Salvar"/"Pular" voltam à tela anterior.
import { getActivePlaylog, type SurveyAnswers, type SurveyDifficulty, type SurveyHurdle } from '../../game/playlog';
import { t } from '../../i18n';
import '../../playlog/strings';
import { button, createFocusList, h, listNav, screenFrame, selector, type FocusItem, type ScreenApi, type ScreenInstance } from './common';
import './survey.css';

const AGAIN: readonly number[] = [1, 2, 3, 4, 5];
const DIFFICULTY: readonly SurveyDifficulty[] = ['easy', 'right', 'hard'];
const HURDLE: readonly SurveyHurdle[] = ['none', 'menus', 'controls', 'seeing', 'performance', 'other'];

/** Sem resposta, → vai para a 1ª opção e ← para a última; depois circula. */
function step<T>(list: readonly T[], cur: T | null, dir: -1 | 1): T {
  if (cur === null) return dir > 0 ? list[0] : list[list.length - 1];
  const i = list.indexOf(cur);
  return list[(i + dir + list.length) % list.length];
}

export function surveyScreen(api: ScreenApi): ScreenInstance {
  const log = getActivePlaylog();
  const pending = log?.pendingSurvey() ?? null;
  let again: number | null = null;
  let difficulty: SurveyDifficulty | null = null;
  let hurdle: SurveyHurdle | null = null;
  let done = false;

  /** Só roda uma vez: Enter e clique no mesmo quadro não respondem duas vezes. */
  const finish = (a: SurveyAnswers | null): void => {
    if (done) return;
    done = true;
    if (log && pending) {
      log.answerSurvey(a);
      api.emit(pending.next);
    } else {
      api.back();
    }
  };

  const items: FocusItem[] = [
    selector(t('playlog.survey.again'), () => (again === null ? t('playlog.survey.unanswered') : t(`playlog.survey.again.${again}`)), (d) => { again = step(AGAIN, again, d); }, { sfx: api.sfx }),
    selector(t('playlog.survey.difficulty'), () => (difficulty === null ? t('playlog.survey.unanswered') : t(`playlog.survey.difficulty.${difficulty}`)), (d) => { difficulty = step(DIFFICULTY, difficulty, d); }, { sfx: api.sfx }),
    selector(t('playlog.survey.hurdle'), () => (hurdle === null ? t('playlog.survey.unanswered') : t(`playlog.survey.hurdle.${hurdle}`)), (d) => { hurdle = step(HURDLE, hurdle, d); }, { sfx: api.sfx }),
    button(t('playlog.survey.save'), () => finish({ again, difficulty, hurdle }), 'btn-primary'),
    button(t('playlog.survey.skip'), () => finish(null)),
  ];
  const list = createFocusList(items, { sfx: api.sfx });

  const el = screenFrame('survey', null,
    h('div', { class: 'survey-panel glass' },
      h('h1', { class: 'screen-title', text: t('playlog.survey.title') }),
      h('p', { class: 'survey-note', text: t('playlog.survey.note') }),
      h('div', { class: 'menu-list' }, items.map((i) => i.el)),
    ),
  );
  return { el, nav: (n) => listNav(list, n, api.sfx, () => finish(null)) };
}
