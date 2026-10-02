# Onda G — pistas por país com pontos turísticos (02/10/2026)

Pedido do dono: **27 pistas do Brasil (uma por estado + DF)**, os 8 países mantidos, cada pista com o seu ponto
turístico; planetas (como no Top Gear 3000) ficam para uma etapa seguinte. **No RS, a pista tem o traçado em
forma de cuia de chimarrão** (fundo redondo, cintura, boca larga em cima — o minimapa tem de mostrar isso).

Contrato: `src/core/data/places.ts` (estado e marcos de cada pista, por id) e
`src/render/scenery/landmarks/types.ts` (o que é um marco). Marcos são só visuais: fora do alcance do carro,
sem colisão, nunca escondendo a pista.

## Copas (12, alternando Brasil e mundo para a dificuldade subir aos poucos)

| # | Copa (id) | Pistas |
|---|---|---|
| 1 | Copa Sudeste (`sudeste`) | copacabana, sampa_noite, ouro_preto, convento_penha |
| 2 | Copa Estados Unidos (`eua`) | rota_66, rochosas, canion, las_vegas |
| 3 | Copa Sul (`sul`) | serra_do_mar, floripa, cuia_gaucha |
| 4 | Copa Japão (`japao`) | baia_toquio, yanbaru, monte_fuji, osaka_neon |
| 5 | Copa Centro-Oeste (`centro_oeste`) | transpantaneira, brasilia, bonito, chapada_veadeiros |
| 6 | Copa Europa (`europa`) | autobahn, paris, passo_alpino, monaco_noite |
| 7 | Copa Nordeste (`nordeste`) | maragogi, natal, joao_pessoa, aracaju, salvador, olinda, jericoacoara, lencois, serra_capivara |
| 8 | Copa África do Sul (`africa_do_sul`) | kruger, karoo, drakensberg, boa_esperanca |
| 9 | Copa Norte (`norte`) | macapa, belem, manaus, rio_branco, porto_velho, jalapao, monte_roraima |
| 10 | Copa Austrália (`australia`) | outback, great_ocean, daintree, sydney |
| 11 | Copa Escandinávia (`escandinavia`) | atlantico, laponia, trollstigen, tromso |
| 12 | Copa Mediterrâneo (`mediterraneo`) | amalfi, santorini, etna, roma |

A ordem dentro de cada copa é sugestão (a do teste de dificuldade manda). A antiga `brasil` some: save,
campeonato em andamento, recordes e conquista `COPA_BRASIL` antigos têm de continuar abrindo (migração).

## As 23 pistas novas

| Estado | id | Nome (PT / EN) | Cenário | Período | Identidade do traçado |
|---|---|---|---|---|---|
| MG | `ouro_preto` | Ladeiras de Ouro Preto / Ouro Preto Hills | tropical | entardecer | sobe e desce de cidade colonial em morro: rampas curtas e fortes, esses fechados |
| ES | `convento_penha` | Convento da Penha / Penha Convent | coast | dia | orla com a pedra do convento: curvas médias, uma subida longa |
| SC | `floripa` | Ponte Hercílio Luz / Hercílio Luz Bridge | coast | entardecer | ilha: beira-mar, curvas abertas, morrotes |
| RS | `cuia_gaucha` | Cuia da Serra Gaúcha / Gaúcha Gourd Circuit | alpine | entardecer | **traçado em forma de cuia de chimarrão**; serra com araucárias e parreirais |
| DF | `brasilia` | Eixo Monumental / Monumental Axis | city_night | entardecer | avenidas retas enormes e tesourinhas (alças em 270°) |
| GO | `chapada_veadeiros` | Chapada dos Veadeiros / Veadeiros Plateau | savanna | dia | cerrado com chapadões: retas, ondulações, mirantes |
| MS | `bonito` | Rios de Bonito / Bonito Rivers | tropical | dia | estradas de terra entre matas e rios: curvas médias encadeadas |
| BA | `salvador` | Orla de Salvador / Salvador Waterfront | coast | entardecer | Cidade Alta e Baixa: subida forte, orla curva até o farol |
| SE | `aracaju` | Orla de Atalaia / Atalaia Beach | coast | noite | orla iluminada: retas e curvas abertas |
| AL | `maragogi` | Piscinas de Maragogi / Maragogi Reefs | coast | dia | beira-mar plano com coqueirais: fácil |
| PE | `olinda` | Ladeiras de Olinda / Olinda Hills | coast | dia | colinas coloniais sobre o mar: esses curtos |
| PB | `joao_pessoa` | Ponta do Seixas / Easternmost Point | coast | entardecer | falésias: curvas médias à beira-mar |
| RN | `natal` | Dunas de Genipabu / Genipabu Dunes | coast | dia | dunas: lombadas e descidas como de buggy |
| CE | `jericoacoara` | Jericoacoara | coast | entardecer | areia e dunas, curvas largas |
| PI | `serra_capivara` | Serra da Capivara / Capivara Range | desert | entardecer | caatinga e paredões: retas e curvas fortes no pé da serra |
| MA | `lencois` | Lençóis Maranhenses | desert | dia | dunas brancas e lagoas: ondulação constante |
| PA | `belem` | Ver-o-Peso / Belém Docks | coast | entardecer | cais e baía: retas e esquinas |
| AM | `manaus` | Encontro das Águas / Meeting of Waters | coast | dia | margem do rio: curvas longas |
| AP | `macapa` | Marco Zero do Equador / Equator Line | tropical | dia | plano e reto com curvas abertas |
| RR | `monte_roraima` | Monte Roraima / Mount Roraima | savanna | dia | lavrado (savana) com o tepui ao fundo: rápido, difícil no fim |
| RO | `porto_velho` | Madeira-Mamoré / Madeira-Mamoré Railway | tropical | entardecer | ao longo da ferrovia: curvas médias e retas |
| AC | `rio_branco` | Gameleira de Rio Branco / Rio Branco Riverside | tropical | dia | margem do rio Acre: curvas suaves |
| TO | `jalapao` | Dunas do Jalapão / Jalapão Dunes | desert | entardecer | areia laranja e serra: retas com lombadas |

Pistas que já existiam e ficam com o estado: `copacabana` (RJ), `sampa_noite` (SP), `serra_do_mar` (PR),
`transpantaneira` (MT). Ids das existentes não mudam (recordes e fantasmas são por id).

## Os marcos (id → o que é)

Cada marco: modelo low-poly procedural no estilo do jogo (faces planas, cor chapada), escala real, lido de
longe. `place`/`side`/`perLap` em `landmarks/types.ts`.

**Brasil · Sudeste, Sul, Centro-Oeste** (`landmarks/brasil-centro-sul.ts`): `cristo_redentor` (estátua de braços
abertos no alto de um morro, skyline) · `pao_de_acucar` (dois morros arredondados com o bondinho, skyline, mar) ·
`masp` (caixa vermelha suspensa em 4 pilares) · `ponte_estaiada` (mastro em X com cabos) · `igreja_barroca` (duas
torres, branca com pedra) · `casario_colonial` (sobrados coloridos, telhado de barro) · `convento_penha` (convento
no alto de um penhasco) · `terceira_ponte` (ponte alta em arco sobre a baía) · `trem_serra_verde` (trem num
viaduto de pedra) · `estufa_jardim_botanico` (estufa de vidro e ferro de Curitiba) · `ponte_hercilio_luz` (ponte
pênsil branca com duas torres) · `igreja_acoriana` (igreja açoriana branca e azul) · `catedral_de_pedra` (gótica
de pedra, uma torre) · `cuia_chimarrao` (cuia gigante com a bomba, monumento) · `araucaria` (pinheiro em
candelabro) · `congresso_nacional` (duas torres, cúpula e cuia invertida) · `catedral_brasilia` (coroa de pilares
curvos) · `cachoeira_veadeiros` (queda d'água num paredão) · `buriti` (palmeira de leque) · `gruta_lago_azul`
(boca de gruta num paredão) · `tuiuiu_ninho` (ninho de tuiuiú no alto de uma árvore) · `portal_transpantaneira`
(portal de madeira).

**Brasil · Norte e Nordeste** (`landmarks/brasil-norte-nordeste.ts`): `farol_da_barra` (farol sobre o forte) ·
`elevador_lacerda` (torre art déco) · `casario_pelourinho` (casario colorido em ladeira) · `arcos_atalaia` (arcos da
orla) · `ponte_aracaju` (ponte estaiada) · `jangada` (jangadas de vela triangular) · `coqueiral` (coqueiros
densos) · `igrejas_olinda` (igrejas brancas no morro) · `bonecos_olinda` (bonecos gigantes de carnaval) ·
`farol_cabo_branco` (farol triangular modernista) · `estacao_cabo_branco` (prédio curvo de Niemeyer) ·
`forte_reis_magos` (forte em estrela na praia) · `ponte_newton_navarro` (ponte estaiada alta) ·
`pedra_furada_jeri` (arco de pedra na praia) · `duna_por_do_sol` (duna grande) · `pedra_furada_capivara` (arco
gigante no paredão) · `pinturas_rupestres` (paredão com pinturas vermelhas) · `lagoas_lencois` (dunas brancas
com lagoas azuis) · `farol_preguicas` (farol listrado vermelho e branco) · `ver_o_peso` (mercado com torres
azuis) · `estacao_docas` (galpões do porto) · `teatro_amazonas` (teatro rosa com cúpula colorida) ·
`barco_regional` (barco de dois andares) · `marco_zero_equador` (monumento-relógio de sol) ·
`fortaleza_macapa` (fortaleza em estrela) · `tepui` (montanha de topo plano, skyline) · `maloca` (casa redonda de
palha) · `locomotiva_mamore` (locomotiva a vapor antiga) · `caixas_dagua` (três caixas-d'água de ferro) ·
`gameleira` (árvore gigante) · `palacio_rio_branco` (palácio neoclássico) · `serra_espirito_santo` (paredão de
mesa laranja, skyline) · `dunas_jalapao` (dunas laranja).

**Mundo** (`landmarks/mundo.ts`): `placa_rota_66` · `diner_neon` · `log_lodge` · `ponte_trelica` ·
`represa_hoover` · `placa_las_vegas` · `piramide_luxor` · `torre_stratosphere` · `rainbow_bridge` ·
`torre_toquio` · `portao_shurei` · `shisa` · `monte_fuji_pico` · `pagode_chureito` · `castelo_osaka` ·
`tsutenkaku` · `castelo_neuschwanstein` · `torre_eiffel` · `arco_triunfo` · `matterhorn` · `capela_alpina` ·
`cassino_monte_carlo` · `portao_kruger` · `girafa` · `igreja_karoo` · `anfiteatro_drakensberg` ·
`farol_cape_point` · `table_mountain` · `uluru` · `arco_great_ocean` · `passarela_daintree` · `opera_sydney` ·
`harbour_bridge` · `ponte_storseisundet` · `vila_lapponia` · `placa_trolls` · `cachoeira_stigfossen` ·
`catedral_artica` · `positano` · `cupula_azul` · `moinho_santorini` · `vulcao_etna` · `coliseu` ·
`cupula_sao_pedro`. Marcas reais (letreiros com logotipo) ficam sem a marca: a placa de Las Vegas diz
"WELCOME", a de Osaka não tem o corredor da marca.

## Mudança de 02/10: 3 pistas por estado (como as regiões do Horizon Chase Turbo)

O dono pediu para subdividir por estado, com cerca de 3 pistas cada. **Isto substitui as 12 copas acima.**

- **Turnê Brasil** (etapa 1): 27 copas, uma por estado, de 3 pistas cada (81 pistas), em ordem de região:
  Sudeste (RJ, SP, MG, ES) → Sul (PR, SC, RS) → Centro-Oeste (DF, GO, MS, MT) → Nordeste (BA, SE, AL, PE, PB,
  RN, CE, PI, MA) → Norte (PA, AM, AP, RR, RO, AC, TO). Dificuldade sobe ao longo da turnê (1 → 5); a região é
  o agrupamento na tela, a copa é o estado. Copa id = `br_<uf>` (ex.: `br_rj`).
- **Mundial** (etapa 2, destrava ao terminar a Turnê Brasil): as 8 copas de país de hoje, menos a Brasil — 7
  copas de 4 pistas, com a sua própria rampa de dificuldade.
- **Planetas** (etapa 3): depois.

As 27 pistas da tabela acima são a primeira de cada estado; as outras 54 estão em
`EXTRA_BRAZIL_PLACES` (`src/core/data/places.ts`), com o lugar no id e os marcos (ids novos = segunda leva de
modelos, que entra depois do merge desta onda). Lugares: RJ Paraty, Serra dos Órgãos · SP Ilhabela, Campos do
Jordão · MG Pampulha, Serra da Canastra · ES Pedra Azul, Itaúnas · PR Foz do Iguaçu, Curitiba · SC Balneário
Camboriú, Serra do Rio do Rastro · RS Orla do Guaíba, Aparados da Serra · DF Lago Paranoá, Torre de TV · GO
Pirenópolis, Caldas Novas · MS Estrada Parque, Campo Grande · MT Chapada dos Guimarães, Cuiabá · BA Chapada
Diamantina, Porto Seguro · SE Xingó, São Cristóvão · AL Maceió, Foz do São Francisco · PE Recife Antigo, Fernando
de Noronha · PB Campina Grande, Pedra da Boca · RN Ponta Negra, Cajueiro de Pirangi · CE Fortaleza, Canoa
Quebrada · PI Delta do Parnaíba, Sete Cidades · MA São Luís, Chapada das Mesas · PA Alter do Chão, Marajó · AM
Ponte Rio Negro, Parintins · AP Pororoca do Araguari, Serra do Navio · RR Boa Vista, Lago Caracaranã · RO Forte
Príncipe da Beira, Vale do Guaporé · AC Geoglifos, Estrada do Pacífico · TO Palmas, Ilha do Bananal.

## Identidade (pedido do dono: "não pode ser igual ao Horizon Chase")

O formato região → copas curtas é só referência. O que é nosso, e tem de aparecer nas telas e nos textos:
- **Expedição Brasil** (não "turnê", não "world tour"): o jogador tem um **passaporte**; vencer a copa de um
  estado **carimba** o passaporte (carimbo com a sigla e o marco do estado) e mostra o **cartão-postal** do
  lugar. Região completa = conquista. Os nomes de tela, conquistas e textos usam esse vocabulário.
- Lugares e marcos reais de cada estado; co-op de equipe (cofre de nitro, vácuo, revezamento) no centro.
- Depois do Brasil, o **Mundial**; depois, os **planetas**.
