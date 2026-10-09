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

// ── Turnê Brasil: 3 pistas por estado (pedido de 02/10, como as regiões do Horizon Chase Turbo). As 54 abaixo
// completam as 27 de cima; marcos novos = segunda leva de modelos (docs/PISTAS-TURISMO.md, "3 por estado").
export const EXTRA_BRAZIL_PLACES: Readonly<Record<string, TrackPlace>> = {
  paraty: { state: 'RJ', landmarks: ['casario_paraty', 'escuna'] },
  serra_dos_orgaos: { state: 'RJ', landmarks: ['dedo_de_deus'] },
  ilhabela: { state: 'SP', landmarks: ['farol_ilhabela', 'veleiro_canal'] },
  campos_do_jordao: { state: 'SP', landmarks: ['chale_enxaimel', 'araucaria'] },
  pampulha: { state: 'MG', landmarks: ['igreja_pampulha'] },
  serra_da_canastra: { state: 'MG', landmarks: ['casca_danta'] },
  pedra_azul: { state: 'ES', landmarks: ['pedra_azul'] },
  itaunas: { state: 'ES', landmarks: ['dunas_itaunas'] },
  foz_do_iguacu: { state: 'PR', landmarks: ['cataratas_iguacu', 'ponte_amizade'] },
  curitiba: { state: 'PR', landmarks: ['opera_de_arame'] },
  camboriu: { state: 'SC', landmarks: ['predios_camboriu', 'roda_gigante'] },
  rio_do_rastro: { state: 'SC', landmarks: ['mirante_rio_do_rastro'] },
  orla_guaiba: { state: 'RS', landmarks: ['usina_gasometro'] },
  aparados_da_serra: { state: 'RS', landmarks: ['canion_itaimbezinho'] },
  lago_paranoa: { state: 'DF', landmarks: ['ponte_jk'] },
  torre_de_tv: { state: 'DF', landmarks: ['torre_tv_brasilia', 'catedral_brasilia'] },
  pirenopolis: { state: 'GO', landmarks: ['igreja_pirenopolis', 'cavalhada'] },
  caldas_novas: { state: 'GO', landmarks: ['parque_aquatico'] },
  estrada_parque: { state: 'MS', landmarks: ['tuiuiu_ninho', 'jacare'] },
  campo_grande: { state: 'MS', landmarks: ['monumento_campo_grande'] },
  chapada_guimaraes: { state: 'MT', landmarks: ['veu_de_noiva'] },
  cuiaba: { state: 'MT', landmarks: ['centro_geodesico'] },
  chapada_diamantina: { state: 'BA', landmarks: ['morro_pai_inacio'] },
  porto_seguro: { state: 'BA', landmarks: ['igreja_quadrado', 'coqueiral'] },
  xingo: { state: 'SE', landmarks: ['canion_xingo', 'catamara'] },
  sao_cristovao: { state: 'SE', landmarks: ['praca_sao_francisco'] },
  maceio: { state: 'AL', landmarks: ['jangada', 'coqueiral'] },
  foz_sao_francisco: { state: 'AL', landmarks: ['farol_piacabucu', 'dunas_piacabucu'] },
  recife_antigo: { state: 'PE', landmarks: ['marco_zero_recife', 'ponte_mauricio'] },
  noronha: { state: 'PE', landmarks: ['morro_do_pico', 'dois_irmaos'] },
  campina_grande: { state: 'PB', landmarks: ['parque_do_povo'] },
  pedra_da_boca: { state: 'PB', landmarks: ['pedra_da_boca'] },
  ponta_negra: { state: 'RN', landmarks: ['morro_do_careca'] },
  cajueiro_pirangi: { state: 'RN', landmarks: ['cajueiro_gigante'] },
  fortaleza_beira_mar: { state: 'CE', landmarks: ['ponte_dos_ingleses', 'jangada'] },
  canoa_quebrada: { state: 'CE', landmarks: ['falesias_canoa'] },
  delta_parnaiba: { state: 'PI', landmarks: ['manguezal_delta'] },
  sete_cidades: { state: 'PI', landmarks: ['pedras_sete_cidades'] },
  sao_luis: { state: 'MA', landmarks: ['casario_azulejos'] },
  chapada_das_mesas: { state: 'MA', landmarks: ['morro_das_mesas', 'cachoeira_sao_romao'] },
  alter_do_chao: { state: 'PA', landmarks: ['praia_de_rio', 'barco_regional'] },
  marajo: { state: 'PA', landmarks: ['bufalo', 'palafita'] },
  ponte_rio_negro: { state: 'AM', landmarks: ['ponte_rio_negro'] },
  parintins: { state: 'AM', landmarks: ['bumbodromo'] },
  pororoca_araguari: { state: 'AP', landmarks: ['palafita'] },
  serra_do_navio: { state: 'AP', landmarks: ['vila_serra_do_navio'] },
  boa_vista: { state: 'RR', landmarks: ['monumento_garimpeiro'] },
  lago_caracarana: { state: 'RR', landmarks: ['buriti', 'tepui'] },
  forte_principe: { state: 'RO', landmarks: ['forte_principe_beira'] },
  vale_guapore: { state: 'RO', landmarks: ['palafita', 'jacare'] },
  geoglifos: { state: 'AC', landmarks: ['geoglifo'] },
  estrada_pacifico: { state: 'AC', landmarks: ['ponte_rio_acre', 'castanheira'] },
  palmas: { state: 'TO', landmarks: ['palacio_araguaia', 'ponte_palmas'] },
  ilha_do_bananal: { state: 'TO', landmarks: ['praia_de_rio', 'maloca'] },
};

/** O lugar de uma pista, da primeira leva ou da segunda. */
export function placeOf(trackId: string): TrackPlace | undefined {
  return TRACK_PLACES[trackId] ?? EXTRA_BRAZIL_PLACES[trackId];
}

/** Todos os lugares (primeira e segunda leva). */
export const ALL_PLACES: Readonly<Record<string, TrackPlace>> = { ...TRACK_PLACES, ...EXTRA_BRAZIL_PLACES };

// ── Cidade de cada pista (pedido do dono, 09/10/2026: o nome da corrida é o da cidade). Quando o trecho fica fora de
// uma cidade (serra, cânion, parque), vale o município onde ele está. Nome em PT; só os que mudam em EN levam o par.
// ⚠️ Conferir os municípios das pistas de natureza antes da 0.1.1 (docs/CRONOGRAMA.md, K6).
type CityName = string | readonly [pt: string, en: string];
const CITY_OF: Readonly<Record<string, CityName>> = {
  copacabana: 'Rio de Janeiro', paraty: 'Paraty', serra_dos_orgaos: 'Teresópolis',
  ilhabela: 'Ilhabela', campos_do_jordao: 'Campos do Jordão', sampa_noite: 'São Paulo',
  pampulha: 'Belo Horizonte', ouro_preto: 'Ouro Preto', serra_da_canastra: 'São Roque de Minas',
  itaunas: 'Conceição da Barra', convento_penha: 'Vila Velha', pedra_azul: 'Domingos Martins',
  foz_do_iguacu: 'Foz do Iguaçu', serra_do_mar: 'Morretes', curitiba: 'Curitiba',
  floripa: 'Florianópolis', camboriu: 'Balneário Camboriú', rio_do_rastro: 'Bom Jardim da Serra',
  orla_guaiba: 'Porto Alegre', cuia_gaucha: 'Gramado', aparados_da_serra: 'Cambará do Sul',
  lago_paranoa: 'Brasília', torre_de_tv: 'Brasília', brasilia: 'Brasília',
  pirenopolis: 'Pirenópolis', caldas_novas: 'Caldas Novas', chapada_veadeiros: 'Alto Paraíso de Goiás',
  bonito: 'Bonito', campo_grande: 'Campo Grande', estrada_parque: 'Corumbá',
  transpantaneira: 'Poconé', cuiaba: 'Cuiabá', chapada_guimaraes: 'Chapada dos Guimarães',
  porto_seguro: 'Porto Seguro', salvador: 'Salvador', chapada_diamantina: 'Lençóis',
  aracaju: 'Aracaju', sao_cristovao: 'São Cristóvão', xingo: 'Canindé de São Francisco',
  maragogi: 'Maragogi', maceio: 'Maceió', foz_sao_francisco: 'Piaçabuçu',
  recife_antigo: 'Recife', olinda: 'Olinda', noronha: 'Fernando de Noronha',
  joao_pessoa: 'João Pessoa', campina_grande: 'Campina Grande', pedra_da_boca: 'Araruna',
  natal: 'Natal', ponta_negra: 'Natal', cajueiro_pirangi: 'Parnamirim',
  jericoacoara: 'Jijoca de Jericoacoara', fortaleza_beira_mar: 'Fortaleza', canoa_quebrada: 'Aracati',
  delta_parnaiba: 'Parnaíba', sete_cidades: 'Piracuruca', serra_capivara: 'São Raimundo Nonato',
  sao_luis: 'São Luís', chapada_das_mesas: 'Carolina', lencois: 'Barreirinhas',
  alter_do_chao: 'Santarém', belem: 'Belém', marajo: 'Soure',
  manaus: 'Manaus', ponte_rio_negro: 'Manaus', parintins: 'Parintins',
  macapa: 'Macapá', pororoca_araguari: 'Cutias', serra_do_navio: 'Serra do Navio',
  boa_vista: 'Boa Vista', monte_roraima: 'Uiramutã', lago_caracarana: 'Normandia',
  porto_velho: 'Porto Velho', forte_principe: 'Costa Marques', vale_guapore: 'Costa Marques',
  rio_branco: 'Rio Branco', geoglifos: 'Plácido de Castro', estrada_pacifico: 'Assis Brasil',
  jalapao: 'Mateiros', palmas: 'Palmas', ilha_do_bananal: 'Formoso do Araguaia',
  rota_66: 'Flagstaff', rochosas: 'Denver', canion: 'Mesquite', las_vegas: 'Las Vegas',
  baia_toquio: ['Tóquio', 'Tokyo'], yanbaru: 'Kunigami', monte_fuji: 'Fujiyoshida', osaka_neon: 'Osaka',
  autobahn: 'Frankfurt', paris: 'Paris', passo_alpino: 'Andermatt', monaco_noite: ['Mônaco', 'Monaco'],
  kruger: 'Hoedspruit', karoo: 'Beaufort West', drakensberg: 'Bergville', boa_esperanca: ['Cidade do Cabo', 'Cape Town'],
  outback: 'Alice Springs', great_ocean: 'Apollo Bay', daintree: 'Daintree', sydney: 'Sydney',
  atlantico: 'Kristiansund', laponia: 'Rovaniemi', trollstigen: 'Åndalsnes', tromso: 'Tromsø',
  amalfi: 'Amalfi', santorini: 'Santorini', etna: ['Catânia', 'Catania'], roma: ['Roma', 'Rome'],
};

/** Cidade da pista no idioma pedido; undefined para pista sem lugar (a fictícia de um teste). */
export function cityOf(trackId: string, lang: 'pt' | 'en'): string | undefined {
  const c = CITY_OF[trackId];
  if (c === undefined) return undefined;
  return typeof c === 'string' ? c : lang === 'en' ? c[1] : c[0];
}

/** "Cidade · trecho"; o trecho sozinho quando já traz a cidade ("Fernando de Noronha") ou a pista não tem cidade. */
export function trackTitle(trackId: string, name: string, lang: 'pt' | 'en'): string {
  const city = cityOf(trackId, lang);
  return !city || name.includes(city) ? name : `${city} · ${name}`;
}
