import type { BrazilRegion, CupDef, CupStage } from '../types';

// As copas, em duas etapas (docs/PISTAS.md, docs/PISTAS-TURISMO.md):
// - Expedição Brasil: 27 copas de 3 pistas, uma por estado (`br_<uf>`), região por região — Sudeste → Sul →
//   Centro-Oeste → Nordeste → Norte. Vencer a copa de um estado carimba o passaporte (save.stamps).
// - Mundial: as 7 copas de país, de 4 pistas; a primeira destrava ao terminar a Expedição Brasil.
// Destravam em sequência (cada uma exige a anterior). Dentro de cada etapa a dificuldade não cai dentro da copa e
// sobe de uma copa para a seguinte (tests/track.test.ts confere a rampa por etapa). Entre pistas do mesmo nível, a
// ordem é a medida (npm run balance -- corrida …; docs/PISTAS.md, "Balanceamento"): a mais difícil fecha a copa —
// em geral a de cidade à noite, como no Mundial. Regiões com mais de um país
// usam a bandeira de onde fica a maioria das pistas: Escandinávia → Noruega (Atlântico, Trollstigen, Tromsø; a
// Lapônia é de três países), Mediterrâneo → Itália (Amalfi, Etna, Roma; Santorini é grega).
export const CUPS: CupDef[] = [
  // ── Expedição Brasil
  { id: 'br_rj', name: 'Copa Rio de Janeiro', country: 'Brasil', flag: '🇧🇷', stage: 'brasil', region: 'sudeste', state: 'RJ', trackIds: ['copacabana', 'paraty', 'serra_dos_orgaos'], requires: null },
  { id: 'br_sp', name: 'Copa São Paulo', country: 'Brasil', flag: '🇧🇷', stage: 'brasil', region: 'sudeste', state: 'SP', trackIds: ['ilhabela', 'campos_do_jordao', 'sampa_noite'], requires: 'br_rj' },
  { id: 'br_mg', name: 'Copa Minas Gerais', country: 'Brasil', flag: '🇧🇷', stage: 'brasil', region: 'sudeste', state: 'MG', trackIds: ['pampulha', 'ouro_preto', 'serra_da_canastra'], requires: 'br_sp' },
  { id: 'br_es', name: 'Copa Espírito Santo', country: 'Brasil', flag: '🇧🇷', stage: 'brasil', region: 'sudeste', state: 'ES', trackIds: ['itaunas', 'convento_penha', 'pedra_azul'], requires: 'br_mg' },
  { id: 'br_pr', name: 'Copa Paraná', country: 'Brasil', flag: '🇧🇷', stage: 'brasil', region: 'sul', state: 'PR', trackIds: ['foz_do_iguacu', 'serra_do_mar', 'curitiba'], requires: 'br_es' },
  { id: 'br_sc', name: 'Copa Santa Catarina', country: 'Brasil', flag: '🇧🇷', stage: 'brasil', region: 'sul', state: 'SC', trackIds: ['floripa', 'camboriu', 'rio_do_rastro'], requires: 'br_pr' },
  { id: 'br_rs', name: 'Copa Rio Grande do Sul', country: 'Brasil', flag: '🇧🇷', stage: 'brasil', region: 'sul', state: 'RS', trackIds: ['orla_guaiba', 'aparados_da_serra', 'cuia_gaucha'], requires: 'br_sc' },
  { id: 'br_df', name: 'Copa Distrito Federal', country: 'Brasil', flag: '🇧🇷', stage: 'brasil', region: 'centro_oeste', state: 'DF', trackIds: ['lago_paranoa', 'brasilia', 'torre_de_tv'], requires: 'br_rs' },
  { id: 'br_go', name: 'Copa Goiás', country: 'Brasil', flag: '🇧🇷', stage: 'brasil', region: 'centro_oeste', state: 'GO', trackIds: ['pirenopolis', 'chapada_veadeiros', 'caldas_novas'], requires: 'br_df' },
  { id: 'br_ms', name: 'Copa Mato Grosso do Sul', country: 'Brasil', flag: '🇧🇷', stage: 'brasil', region: 'centro_oeste', state: 'MS', trackIds: ['bonito', 'campo_grande', 'estrada_parque'], requires: 'br_go' },
  { id: 'br_mt', name: 'Copa Mato Grosso', country: 'Brasil', flag: '🇧🇷', stage: 'brasil', region: 'centro_oeste', state: 'MT', trackIds: ['transpantaneira', 'chapada_guimaraes', 'cuiaba'], requires: 'br_ms' },
  { id: 'br_ba', name: 'Copa Bahia', country: 'Brasil', flag: '🇧🇷', stage: 'brasil', region: 'nordeste', state: 'BA', trackIds: ['porto_seguro', 'salvador', 'chapada_diamantina'], requires: 'br_mt' },
  { id: 'br_se', name: 'Copa Sergipe', country: 'Brasil', flag: '🇧🇷', stage: 'brasil', region: 'nordeste', state: 'SE', trackIds: ['aracaju', 'sao_cristovao', 'xingo'], requires: 'br_ba' },
  { id: 'br_al', name: 'Copa Alagoas', country: 'Brasil', flag: '🇧🇷', stage: 'brasil', region: 'nordeste', state: 'AL', trackIds: ['maragogi', 'foz_sao_francisco', 'maceio'], requires: 'br_se' },
  { id: 'br_pe', name: 'Copa Pernambuco', country: 'Brasil', flag: '🇧🇷', stage: 'brasil', region: 'nordeste', state: 'PE', trackIds: ['recife_antigo', 'olinda', 'noronha'], requires: 'br_al' },
  { id: 'br_pb', name: 'Copa Paraíba', country: 'Brasil', flag: '🇧🇷', stage: 'brasil', region: 'nordeste', state: 'PB', trackIds: ['joao_pessoa', 'pedra_da_boca', 'campina_grande'], requires: 'br_pe' },
  { id: 'br_rn', name: 'Copa Rio Grande do Norte', country: 'Brasil', flag: '🇧🇷', stage: 'brasil', region: 'nordeste', state: 'RN', trackIds: ['natal', 'ponta_negra', 'cajueiro_pirangi'], requires: 'br_pb' },
  { id: 'br_ce', name: 'Copa Ceará', country: 'Brasil', flag: '🇧🇷', stage: 'brasil', region: 'nordeste', state: 'CE', trackIds: ['jericoacoara', 'canoa_quebrada', 'fortaleza_beira_mar'], requires: 'br_rn' },
  { id: 'br_pi', name: 'Copa Piauí', country: 'Brasil', flag: '🇧🇷', stage: 'brasil', region: 'nordeste', state: 'PI', trackIds: ['delta_parnaiba', 'sete_cidades', 'serra_capivara'], requires: 'br_ce' },
  { id: 'br_ma', name: 'Copa Maranhão', country: 'Brasil', flag: '🇧🇷', stage: 'brasil', region: 'nordeste', state: 'MA', trackIds: ['lencois', 'chapada_das_mesas', 'sao_luis'], requires: 'br_pi' },
  { id: 'br_pa', name: 'Copa Pará', country: 'Brasil', flag: '🇧🇷', stage: 'brasil', region: 'norte', state: 'PA', trackIds: ['alter_do_chao', 'belem', 'marajo'], requires: 'br_ma' },
  { id: 'br_am', name: 'Copa Amazonas', country: 'Brasil', flag: '🇧🇷', stage: 'brasil', region: 'norte', state: 'AM', trackIds: ['manaus', 'ponte_rio_negro', 'parintins'], requires: 'br_pa' },
  { id: 'br_ap', name: 'Copa Amapá', country: 'Brasil', flag: '🇧🇷', stage: 'brasil', region: 'norte', state: 'AP', trackIds: ['macapa', 'pororoca_araguari', 'serra_do_navio'], requires: 'br_am' },
  { id: 'br_rr', name: 'Copa Roraima', country: 'Brasil', flag: '🇧🇷', stage: 'brasil', region: 'norte', state: 'RR', trackIds: ['boa_vista', 'monte_roraima', 'lago_caracarana'], requires: 'br_ap' },
  { id: 'br_ro', name: 'Copa Rondônia', country: 'Brasil', flag: '🇧🇷', stage: 'brasil', region: 'norte', state: 'RO', trackIds: ['porto_velho', 'vale_guapore', 'forte_principe'], requires: 'br_rr' },
  { id: 'br_ac', name: 'Copa Acre', country: 'Brasil', flag: '🇧🇷', stage: 'brasil', region: 'norte', state: 'AC', trackIds: ['rio_branco', 'geoglifos', 'estrada_pacifico'], requires: 'br_ro' },
  { id: 'br_to', name: 'Copa Tocantins', country: 'Brasil', flag: '🇧🇷', stage: 'brasil', region: 'norte', state: 'TO', trackIds: ['jalapao', 'palmas', 'ilha_do_bananal'], requires: 'br_ac' },
  // ── Mundial
  { id: 'eua', name: 'Copa Estados Unidos', country: 'Estados Unidos', flag: '🇺🇸', stage: 'mundial', trackIds: ['rota_66', 'rochosas', 'canion', 'las_vegas'], requires: 'br_to' },
  { id: 'japao', name: 'Copa Japão', country: 'Japão', flag: '🇯🇵', stage: 'mundial', trackIds: ['baia_toquio', 'yanbaru', 'monte_fuji', 'osaka_neon'], requires: 'eua' },
  { id: 'europa', name: 'Copa Europa', country: 'Europa', flag: '🇪🇺', stage: 'mundial', trackIds: ['autobahn', 'paris', 'passo_alpino', 'monaco_noite'], requires: 'japao' },
  { id: 'africa_do_sul', name: 'Copa África do Sul', country: 'África do Sul', flag: '🇿🇦', stage: 'mundial', trackIds: ['kruger', 'karoo', 'drakensberg', 'boa_esperanca'], requires: 'europa' },
  { id: 'australia', name: 'Copa Austrália', country: 'Austrália', flag: '🇦🇺', stage: 'mundial', trackIds: ['outback', 'great_ocean', 'daintree', 'sydney'], requires: 'africa_do_sul' },
  { id: 'escandinavia', name: 'Copa Escandinávia', country: 'Escandinávia', flag: '🇳🇴', stage: 'mundial', trackIds: ['atlantico', 'laponia', 'trollstigen', 'tromso'], requires: 'australia' },
  { id: 'mediterraneo', name: 'Copa Mediterrâneo', country: 'Mediterrâneo', flag: '🇮🇹', stage: 'mundial', trackIds: ['amalfi', 'santorini', 'etna', 'roma'], requires: 'escandinavia' },
];

/** Regiões da Expedição Brasil, na ordem da expedição, com os estados de cada uma (a ordem das copas). */
export const BRAZIL_REGIONS: ReadonlyArray<{ id: BrazilRegion; states: readonly string[] }> = [
  { id: 'sudeste', states: ['RJ', 'SP', 'MG', 'ES'] },
  { id: 'sul', states: ['PR', 'SC', 'RS'] },
  { id: 'centro_oeste', states: ['DF', 'GO', 'MS', 'MT'] },
  { id: 'nordeste', states: ['BA', 'SE', 'AL', 'PE', 'PB', 'RN', 'CE', 'PI', 'MA'] },
  { id: 'norte', states: ['PA', 'AM', 'AP', 'RR', 'RO', 'AC', 'TO'] },
];

/** Copas de uma etapa, na ordem. */
export function stageCups(stage: CupStage, cups: readonly CupDef[] = CUPS): CupDef[] {
  return cups.filter((c) => c.stage === stage);
}

/** Copa da Expedição Brasil de um estado (sigla), ou undefined. */
export function stateCup(state: string, cups: readonly CupDef[] = CUPS): CupDef | undefined {
  return cups.find((c) => c.stage === 'brasil' && c.state === state);
}

/**
 * Copas que deixaram de existir → a que ficou no lugar delas. Save, carreira e conquista antigos passam por aqui na
 * leitura (src/game/save.ts, career-save.ts). A Copa Brasil (copacabana, transpantaneira, serra_do_mar,
 * sampa_noite) virou a Copa Rio de Janeiro, a primeira da Expedição: quem a venceu ganha o carimbo do RJ e segue
 * para São Paulo. Campeonato em andamento numa copa que sumiu recomeça do zero (as corridas não batem mais).
 */
export const LEGACY_CUP_IDS: Readonly<Record<string, string>> = { brasil: 'br_rj' };

/** Id de copa atual para um id possivelmente antigo (o próprio id quando não é legado). */
export function currentCupId(id: string): string {
  return Object.prototype.hasOwnProperty.call(LEGACY_CUP_IDS, id) ? LEGACY_CUP_IDS[id] : id;
}

/**
 * Copas que uma copa antiga concluída abria e que hoje exigem outra coisa. Até a onda G, vencer a Copa Brasil abria
 * os Estados Unidos; hoje os EUA exigem a última da Expedição (br_to). A migração do save guarda essa abertura em
 * `save.cupsUnlocked` (src/game/save.ts) — quem já corria nos EUA não passa a precisar de 27 estados para voltar lá.
 * As outras copas do Mundial exigem a mesma de antes (eua → japao → … → mediterraneo): nada a herdar.
 */
export const LEGACY_CUP_OPENS: Readonly<Record<string, readonly string[]>> = { brasil: ['eua'] };

/** Copas que a copa antiga `id`, concluída, abria e hoje não abre mais ([] para id que não é legado). */
export function legacyCupOpens(id: string): readonly string[] {
  return Object.prototype.hasOwnProperty.call(LEGACY_CUP_OPENS, id) ? LEGACY_CUP_OPENS[id] : [];
}

export function cupDef(id: string): CupDef {
  const c = CUPS.find((x) => x.id === id);
  if (!c) throw new Error(`Copa desconhecida: ${id}`);
  return c;
}
