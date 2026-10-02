// Lugar e marcos turísticos de cada pista (contrato da onda G, docs/PISTAS-TURISMO.md). O núcleo só guarda
// o dado: os marcos são visuais (src/render/scenery/landmarks/), sem colisão e fora do alcance do carro.
// `state` = estado brasileiro (sigla) quando a pista é do Brasil.
export interface TrackPlace {
  state?: string;
  /** Ids dos marcos (src/render/scenery/landmarks/index.ts), do mais importante para o menos. */
  landmarks: readonly string[];
}

export const TRACK_PLACES: Readonly<Record<string, TrackPlace>> = {
  // ── Brasil · Sudeste
  copacabana: { state: 'RJ', landmarks: ['cristo_redentor', 'pao_de_acucar'] },
  sampa_noite: { state: 'SP', landmarks: ['masp', 'ponte_estaiada'] },
  ouro_preto: { state: 'MG', landmarks: ['igreja_barroca', 'casario_colonial'] },
  convento_penha: { state: 'ES', landmarks: ['convento_penha', 'terceira_ponte'] },
  // ── Brasil · Sul
  serra_do_mar: { state: 'PR', landmarks: ['trem_serra_verde', 'estufa_jardim_botanico'] },
  floripa: { state: 'SC', landmarks: ['ponte_hercilio_luz', 'igreja_acoriana'] },
  cuia_gaucha: { state: 'RS', landmarks: ['catedral_de_pedra', 'cuia_chimarrao', 'araucaria'] },
  // ── Brasil · Centro-Oeste
  brasilia: { state: 'DF', landmarks: ['congresso_nacional', 'catedral_brasilia'] },
  chapada_veadeiros: { state: 'GO', landmarks: ['cachoeira_veadeiros', 'buriti'] },
  bonito: { state: 'MS', landmarks: ['gruta_lago_azul', 'buriti'] },
  transpantaneira: { state: 'MT', landmarks: ['tuiuiu_ninho', 'portal_transpantaneira'] },
  // ── Brasil · Nordeste
  salvador: { state: 'BA', landmarks: ['farol_da_barra', 'elevador_lacerda', 'casario_pelourinho'] },
  aracaju: { state: 'SE', landmarks: ['arcos_atalaia', 'ponte_aracaju'] },
  maragogi: { state: 'AL', landmarks: ['jangada', 'coqueiral'] },
  olinda: { state: 'PE', landmarks: ['igrejas_olinda', 'bonecos_olinda'] },
  joao_pessoa: { state: 'PB', landmarks: ['farol_cabo_branco', 'estacao_cabo_branco'] },
  natal: { state: 'RN', landmarks: ['forte_reis_magos', 'ponte_newton_navarro'] },
  jericoacoara: { state: 'CE', landmarks: ['pedra_furada_jeri', 'duna_por_do_sol'] },
  serra_capivara: { state: 'PI', landmarks: ['pedra_furada_capivara', 'pinturas_rupestres'] },
  lencois: { state: 'MA', landmarks: ['lagoas_lencois', 'farol_preguicas'] },
  // ── Brasil · Norte
  belem: { state: 'PA', landmarks: ['ver_o_peso', 'estacao_docas'] },
  manaus: { state: 'AM', landmarks: ['teatro_amazonas', 'barco_regional'] },
  macapa: { state: 'AP', landmarks: ['marco_zero_equador', 'fortaleza_macapa'] },
  monte_roraima: { state: 'RR', landmarks: ['tepui', 'maloca'] },
  porto_velho: { state: 'RO', landmarks: ['locomotiva_mamore', 'caixas_dagua'] },
  rio_branco: { state: 'AC', landmarks: ['gameleira', 'palacio_rio_branco'] },
  jalapao: { state: 'TO', landmarks: ['serra_espirito_santo', 'dunas_jalapao'] },
  // ── Estados Unidos
  rota_66: { landmarks: ['placa_rota_66', 'diner_neon'] },
  rochosas: { landmarks: ['log_lodge', 'ponte_trelica'] },
  canion: { landmarks: ['represa_hoover'] },
  las_vegas: { landmarks: ['placa_las_vegas', 'piramide_luxor', 'torre_stratosphere'] },
  // ── Japão
  baia_toquio: { landmarks: ['rainbow_bridge', 'torre_toquio'] },
  yanbaru: { landmarks: ['portao_shurei', 'shisa'] },
  monte_fuji: { landmarks: ['monte_fuji_pico', 'pagode_chureito'] },
  osaka_neon: { landmarks: ['castelo_osaka', 'tsutenkaku'] },
  // ── Europa
  autobahn: { landmarks: ['castelo_neuschwanstein'] },
  paris: { landmarks: ['torre_eiffel', 'arco_triunfo'] },
  passo_alpino: { landmarks: ['matterhorn', 'capela_alpina'] },
  monaco_noite: { landmarks: ['cassino_monte_carlo'] },
  // ── África do Sul
  kruger: { landmarks: ['portao_kruger', 'girafa'] },
  karoo: { landmarks: ['igreja_karoo'] },
  drakensberg: { landmarks: ['anfiteatro_drakensberg'] },
  boa_esperanca: { landmarks: ['farol_cape_point', 'table_mountain'] },
  // ── Austrália
  outback: { landmarks: ['uluru'] },
  great_ocean: { landmarks: ['arco_great_ocean'] },
  daintree: { landmarks: ['passarela_daintree'] },
  sydney: { landmarks: ['opera_sydney', 'harbour_bridge'] },
  // ── Escandinávia
  atlantico: { landmarks: ['ponte_storseisundet'] },
  laponia: { landmarks: ['vila_lapponia'] },
  trollstigen: { landmarks: ['placa_trolls', 'cachoeira_stigfossen'] },
  tromso: { landmarks: ['catedral_artica'] },
  // ── Mediterrâneo
  amalfi: { landmarks: ['positano'] },
  santorini: { landmarks: ['cupula_azul', 'moinho_santorini'] },
  etna: { landmarks: ['vulcao_etna'] },
  roma: { landmarks: ['coliseu', 'cupula_sao_pedro'] },
};
