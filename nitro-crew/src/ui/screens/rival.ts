// Rival da copa nas telas (docs/RIVAIS.md): cartão do duelo no resultado e na classificação, bloco do
// rival no detalhe da copa e na garagem da carreira. As regras moram em src/game/rivals.ts.
import type { CareerState } from '../../core/career';
import type { ChampionshipState, RaceResultRow } from '../../core/types';
import {
  cupDuels, cupRival, duelLineKind, personalityName, rivalLine, rivalRaceSummary, rivalStandingsSummary, type RivalSummary,
} from '../../game/rivals';
import { t } from '../../i18n';
import { h } from './common';
import { icon } from './icons';
import './rival.css';

/** "Rival: nome — posição" e a fala; a cor segue o duelo (ele à frente = vermelho, atrás = verde). */
export function rivalCard(s: RivalSummary): HTMLElement {
  return h('div', { class: `rival-card glass ${s.rivalAhead ? 'ahead' : 'behind'}`, attrs: { 'data-rival': s.rival.name, 'data-kind': s.kind } },
    h('span', { class: 'rival-card-head' }, icon('swords'), h('strong', { text: s.title }),
      h('span', { class: 'rival-pers', text: personalityName(s.rival.personality) })),
    h('q', { class: 'rival-quote', text: s.line }),
  );
}

/** Resultado de uma corrida da copa (normal ou carreira): o cartão, ou null sem rival no grid. */
export function rivalRaceCard(champ: ChampionshipState | null, results: readonly RaceResultRow[]): HTMLElement | null {
  const s = champ ? rivalRaceSummary(champ, results) : null;
  return s ? rivalCard(s) : null;
}

export function rivalStandingsCard(champ: ChampionshipState): HTMLElement | null {
  const s = rivalStandingsSummary(champ);
  return s ? rivalCard(s) : null;
}

/** Etiqueta "RIVAL" ao lado do nome na tabela. */
export function rivalTag(): HTMLElement {
  return h('span', { class: 'rival-tag', text: t('rivals.hudTag') });
}

/** Detalhe da copa: quem é o rival, a personalidade e a frase de apresentação. */
export function cupRivalBlock(cupId: string): HTMLElement {
  const r = cupRival(cupId);
  return h('div', { class: 'cup-rival', attrs: { 'data-rival': r.name } },
    h('span', { class: 'cup-rival-head' }, icon('swords'),
      h('span', { class: 'cup-rival-k', text: t('rivals.cupRival') }),
      h('strong', { text: r.name }),
      h('span', { class: 'rival-pers', text: personalityName(r.personality) })),
    h('q', { class: 'rival-quote', text: rivalLine(r, 'intro') }),
  );
}

/**
 * Garagem da carreira: o rival da copa atual e a fala — a apresentação antes da primeira corrida da
 * copa, depois a do último duelo. Null com a carreira concluída.
 */
export function garageRivalBlock(career: CareerState): HTMLElement | null {
  if (career.completed) return null;
  const r = cupRival(career.cupId);
  const duels = career.champ ? cupDuels(career.champ) : [];
  const last = duels[duels.length - 1];
  const line = last === undefined || last === null ? rivalLine(r, 'intro') : rivalLine(r, duelLineKind(last, duels[duels.length - 2]));
  return h('div', { class: 'garage-rival glass', attrs: { 'data-rival': r.name } },
    icon('swords'),
    h('strong', { text: t('rivals.garage', { name: r.name, personality: personalityName(r.personality) }) }),
    h('q', { class: 'rival-quote', text: line }),
  );
}
