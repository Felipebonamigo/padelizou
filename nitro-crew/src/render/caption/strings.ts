// Textos da legenda dos marcos turísticos (caption.ts, docs/VISUAL.md "Legenda dos marcos"): a opção, os nomes dos
// marcos do Mundial (os do Brasil já vêm do passaporte, src/ui/passport/strings.ts) e onde fica cada pista. Nome
// próprio de lugar não se traduz (o vocabulário do passaporte); o EN traduz a descrição e o país.
import { registerStrings } from '../../i18n';

/** Marcos do Mundial: [id, PT, EN]. Os do Brasil são os do passaporte. */
const WORLD_LANDMARKS: ReadonlyArray<readonly [string, string, string]> = [
  // Estados Unidos
  ['placa_rota_66', 'Placa da Rota 66', 'Route 66 shield'],
  ['diner_neon', 'Lanchonete de neon', 'Neon diner'],
  ['log_lodge', 'Cabana de troncos', 'Log lodge'],
  ['ponte_trelica', 'Ponte de treliça', 'Truss bridge'],
  ['represa_hoover', 'Represa Hoover', 'Hoover Dam'],
  ['placa_las_vegas', 'Letreiro de Las Vegas', 'Las Vegas welcome sign'],
  ['piramide_luxor', 'Pirâmide do Luxor', 'Luxor pyramid'],
  ['torre_stratosphere', 'Torre Stratosphere', 'Stratosphere Tower'],
  // Japão
  ['rainbow_bridge', 'Rainbow Bridge', 'Rainbow Bridge'],
  ['torre_toquio', 'Torre de Tóquio', 'Tokyo Tower'],
  ['portao_shurei', 'Portão Shureimon', 'Shureimon Gate'],
  ['shisa', 'Shisas, os leões-guardiões', 'Shisa guardian lions'],
  ['monte_fuji_pico', 'Monte Fuji', 'Mount Fuji'],
  ['pagode_chureito', 'Pagode Chureito', 'Chureito Pagoda'],
  ['castelo_osaka', 'Castelo de Osaka', 'Osaka Castle'],
  ['tsutenkaku', 'Torre Tsutenkaku', 'Tsutenkaku Tower'],
  // Europa
  ['castelo_neuschwanstein', 'Castelo de Neuschwanstein', 'Neuschwanstein Castle'],
  ['torre_eiffel', 'Torre Eiffel', 'Eiffel Tower'],
  ['arco_triunfo', 'Arco do Triunfo', 'Arc de Triomphe'],
  ['matterhorn', 'Matterhorn', 'Matterhorn'],
  ['capela_alpina', 'Capela alpina', 'Alpine chapel'],
  ['cassino_monte_carlo', 'Cassino de Monte Carlo', 'Monte Carlo Casino'],
  // África do Sul
  ['portao_kruger', 'Portão do Parque Kruger', 'Kruger Park gate'],
  ['girafa', 'Girafas', 'Giraffes'],
  ['igreja_karoo', 'Igreja do Karoo', 'Karoo church'],
  ['anfiteatro_drakensberg', 'Anfiteatro do Drakensberg', 'Drakensberg Amphitheatre'],
  ['farol_cape_point', 'Farol de Cape Point', 'Cape Point lighthouse'],
  ['table_mountain', 'Montanha da Mesa', 'Table Mountain'],
  // Austrália
  ['uluru', 'Uluru', 'Uluru'],
  ['arco_great_ocean', 'Arco da Great Ocean Road', 'Great Ocean Road arch'],
  ['passarela_daintree', 'Passarela nas copas do Daintree', 'Daintree canopy walkway'],
  ['opera_sydney', 'Ópera de Sydney', 'Sydney Opera House'],
  ['harbour_bridge', 'Ponte da Baía de Sydney', 'Sydney Harbour Bridge'],
  // Escandinávia
  ['ponte_storseisundet', 'Ponte Storseisundet', 'Storseisundet Bridge'],
  ['vila_lapponia', 'Vila da Lapônia', 'Lapland village'],
  ['placa_trolls', 'Placa dos trolls', 'Troll crossing sign'],
  ['cachoeira_stigfossen', 'Cachoeira Stigfossen', 'Stigfossen waterfall'],
  ['catedral_artica', 'Catedral Ártica', 'Arctic Cathedral'],
  // Mediterrâneo
  ['positano', 'Positano', 'Positano'],
  ['cupula_azul', 'Cúpulas azuis', 'Blue domes'],
  ['moinho_santorini', 'Moinhos de Santorini', 'Santorini windmills'],
  ['vulcao_etna', 'Vulcão Etna', 'Mount Etna'],
  ['coliseu', 'Coliseu', 'Colosseum'],
  ['cupula_sao_pedro', 'Cúpula de São Pedro', 'St. Peter\'s Dome'],
];

/**
 * Onde fica cada pista do Brasil: a cidade (ou a região, quando a pista não é de uma cidade só) — a legenda junta a UF
 * do places.ts: "Foz do Iguaçu · PR". Nome próprio: igual em PT e EN.
 */
const BRAZIL_TOWNS: Readonly<Record<string, string>> = {
  // Sudeste
  copacabana: 'Rio de Janeiro', paraty: 'Paraty', serra_dos_orgaos: 'Teresópolis',
  ilhabela: 'Ilhabela', campos_do_jordao: 'Campos do Jordão', sampa_noite: 'São Paulo',
  pampulha: 'Belo Horizonte', ouro_preto: 'Ouro Preto', serra_da_canastra: 'Serra da Canastra',
  itaunas: 'Conceição da Barra', convento_penha: 'Vila Velha', pedra_azul: 'Domingos Martins',
  // Sul
  foz_do_iguacu: 'Foz do Iguaçu', serra_do_mar: 'Serra do Mar', curitiba: 'Curitiba',
  floripa: 'Florianópolis', camboriu: 'Balneário Camboriú', rio_do_rastro: 'Bom Jardim da Serra',
  orla_guaiba: 'Porto Alegre', cuia_gaucha: 'Serra Gaúcha', aparados_da_serra: 'Cambará do Sul',
  // Centro-Oeste
  lago_paranoa: 'Brasília', torre_de_tv: 'Brasília', brasilia: 'Brasília',
  pirenopolis: 'Pirenópolis', caldas_novas: 'Caldas Novas', chapada_veadeiros: 'Chapada dos Veadeiros',
  bonito: 'Bonito', campo_grande: 'Campo Grande', estrada_parque: 'Corumbá',
  transpantaneira: 'Poconé', cuiaba: 'Cuiabá', chapada_guimaraes: 'Chapada dos Guimarães',
  // Nordeste
  porto_seguro: 'Porto Seguro', salvador: 'Salvador', chapada_diamantina: 'Chapada Diamantina',
  aracaju: 'Aracaju', sao_cristovao: 'São Cristóvão', xingo: 'Canindé de São Francisco',
  maragogi: 'Maragogi', maceio: 'Maceió', foz_sao_francisco: 'Piaçabuçu',
  recife_antigo: 'Recife', olinda: 'Olinda', noronha: 'Fernando de Noronha',
  joao_pessoa: 'João Pessoa', campina_grande: 'Campina Grande', pedra_da_boca: 'Araruna',
  natal: 'Natal', ponta_negra: 'Natal', cajueiro_pirangi: 'Parnamirim',
  jericoacoara: 'Jericoacoara', fortaleza_beira_mar: 'Fortaleza', canoa_quebrada: 'Aracati',
  delta_parnaiba: 'Parnaíba', sete_cidades: 'Piracuruca', serra_capivara: 'São Raimundo Nonato',
  sao_luis: 'São Luís', chapada_das_mesas: 'Carolina', lencois: 'Barreirinhas',
  // Norte
  alter_do_chao: 'Alter do Chão', belem: 'Belém', marajo: 'Ilha de Marajó',
  manaus: 'Manaus', ponte_rio_negro: 'Manaus', parintins: 'Parintins',
  macapa: 'Macapá', pororoca_araguari: 'Rio Araguari', serra_do_navio: 'Serra do Navio',
  boa_vista: 'Boa Vista', monte_roraima: 'Monte Roraima', lago_caracarana: 'Normandia',
  porto_velho: 'Porto Velho', forte_principe: 'Costa Marques', vale_guapore: 'Vale do Guaporé',
  rio_branco: 'Rio Branco', geoglifos: 'Rio Branco', estrada_pacifico: 'Assis Brasil',
  jalapao: 'Jalapão', palmas: 'Palmas', ilha_do_bananal: 'Ilha do Bananal',
};

/** Onde fica cada pista do Mundial: [pista, PT, EN] — lugar · país. */
const WORLD_PLACES: ReadonlyArray<readonly [string, string, string]> = [
  ['rota_66', 'Arizona · Estados Unidos', 'Arizona · United States'],
  ['rochosas', 'Colorado · Estados Unidos', 'Colorado · United States'],
  ['canion', 'Nevada · Estados Unidos', 'Nevada · United States'],
  ['las_vegas', 'Las Vegas · Estados Unidos', 'Las Vegas · United States'],
  ['baia_toquio', 'Tóquio · Japão', 'Tokyo · Japan'],
  ['yanbaru', 'Okinawa · Japão', 'Okinawa · Japan'],
  ['monte_fuji', 'Yamanashi · Japão', 'Yamanashi · Japan'],
  ['osaka_neon', 'Osaka · Japão', 'Osaka · Japan'],
  ['autobahn', 'Baviera · Alemanha', 'Bavaria · Germany'],
  ['paris', 'Paris · França', 'Paris · France'],
  ['passo_alpino', 'Zermatt · Suíça', 'Zermatt · Switzerland'],
  ['monaco_noite', 'Monte Carlo · Mônaco', 'Monte Carlo · Monaco'],
  ['kruger', 'Parque Kruger · África do Sul', 'Kruger Park · South Africa'],
  ['karoo', 'Karoo · África do Sul', 'Karoo · South Africa'],
  ['drakensberg', 'KwaZulu-Natal · África do Sul', 'KwaZulu-Natal · South Africa'],
  ['boa_esperanca', 'Cidade do Cabo · África do Sul', 'Cape Town · South Africa'],
  ['outback', 'Território do Norte · Austrália', 'Northern Territory · Australia'],
  ['great_ocean', 'Victoria · Austrália', 'Victoria · Australia'],
  ['daintree', 'Queensland · Austrália', 'Queensland · Australia'],
  ['sydney', 'Sydney · Austrália', 'Sydney · Australia'],
  ['atlantico', 'Averøy · Noruega', 'Averøy · Norway'],
  ['laponia', 'Lapônia · Escandinávia', 'Lapland · Scandinavia'],
  ['trollstigen', 'Rauma · Noruega', 'Rauma · Norway'],
  ['tromso', 'Tromsø · Noruega', 'Tromsø · Norway'],
  ['amalfi', 'Costa Amalfitana · Itália', 'Amalfi Coast · Italy'],
  ['santorini', 'Santorini · Grécia', 'Santorini · Greece'],
  ['etna', 'Sicília · Itália', 'Sicily · Italy'],
  ['roma', 'Roma · Itália', 'Rome · Italy'],
];

const pt: Record<string, string> = {
  'option': 'Legenda dos marcos',
  'desc': 'Quando um ponto turístico aparece bem na sua tela, o nome dele e onde ele fica entram por 3 segundos no pé da tela.',
  'where.br': '{town} · {uf}',
};
const en: Record<string, string> = {
  'option': 'Landmark captions',
  'desc': 'When a landmark comes into full view on your screen, its name and where it is show at the bottom for 3 seconds.',
  'where.br': '{town} · {uf}',
};
for (const [id, p, e] of WORLD_LANDMARKS) { pt[`landmark.${id}`] = p; en[`landmark.${id}`] = e; }
for (const [id, town] of Object.entries(BRAZIL_TOWNS)) { pt[`town.${id}`] = town; en[`town.${id}`] = town; }
for (const [id, p, e] of WORLD_PLACES) { pt[`place.${id}`] = p; en[`place.${id}`] = e; }

registerStrings('caption', { pt, en });
