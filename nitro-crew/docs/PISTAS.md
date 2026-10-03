# Pistas e copas — 109 pistas: Expedição Brasil (27 estados × 3) e Mundial (7 países × 4)

Onda G (02/10/2026, pedido do dono; contrato em `docs/PISTAS-TURISMO.md`): **3 pistas por estado do Brasil**, uma copa
por estado, e os 7 países de antes. Duas etapas, destravadas em sequência:

- **Expedição Brasil** — 27 copas de 3 pistas (`br_<uf>`, ex. `br_rj`), região por região: Sudeste (RJ, SP, MG, ES)
  → Sul (PR, SC, RS) → Centro-Oeste (DF, GO, MS, MT) → Nordeste (BA, SE, AL, PE, PB, RN, CE, PI, MA) → Norte (PA, AM,
  AP, RR, RO, AC, TO). Vencer a copa de um estado **carimba o passaporte** (`save.stamps`); a região inteira
  carimbada dá `REGIAO_<REGIÃO>`, os 27 estados dão `PASSAPORTE_COMPLETO`. Vocabulário: passaporte, carimbo,
  cartão-postal — nunca "turnê"/"world tour".
- **Mundial** — Estados Unidos, Japão, Europa, África do Sul, Austrália, Escandinávia, Mediterrâneo (4 pistas cada);
  a primeira exige a última copa da Expedição (`br_to`).
- Planetas (etapa 3): depois.

Tudo é dado: pistas em `src/core/track/tracks.ts` (DSL `straight/curve/hill/s/pit`), copas em `src/core/data/cups.ts`
(`stage`, `region`, `state`; `BRAZIL_REGIONS`, `stageCups`, `stateCup`), lugar e marcos de cada pista em
`src/core/data/places.ts`. As 4 pistas do Brasil que já existiam (Copacabana, Noite em Sampa, Serra do Mar,
Transpantaneira) mantiveram id e traçado; a Copa Brasil antiga virou a Copa Rio de Janeiro (migração abaixo).

## Regras do catálogo (e o teste que segura cada uma)

| Regra | Onde é conferida |
|---|---|
| 109 pistas; 27 copas `brasil` de 3 pistas e depois 7 copas `mundial` de 4; toda pista em exatamente uma copa | `tests/track.test.ts` (catálogo) |
| Uma copa `br_<uf>` por estado, na ordem das regiões; cada pista é do estado da copa (`places.ts`); todo lugar do contrato virou pista | `tests/track.test.ts` |
| Ids ASCII minúsculos (`^[a-z][a-z0-9_]*$`): o id da copa vira a conquista `COPA_<ID>` | `tests/track.test.ts`, `tests/desktop.test.ts` |
| Destravamento linear na ordem da lista (br_rj → … → br_to → eua → … → mediterraneo); copa já concluída fica aberta (save antigo) | `tests/track.test.ts`, `tests/ui.test.ts`, `tests/migration-brasil.test.ts` |
| **Rampa por etapa**: a dificuldade (1–5) não cai dentro da copa; a média por copa não cai na Expedição e sobe a cada copa no Mundial; cada etapa começa ≤ 2,25 e termina ≥ 4,75 | `tests/track.test.ts` |
| O índice técnico médio (medido no traçado) sobe de uma copa para a seguinte, em cada etapa | `tests/track.test.ts` |
| Rótulo não mente: pista dois níveis acima tem traçado mais técnico (em todas as 109) | `tests/track.test.ts` |
| 1.500–3.000 segmentos, 3–5 voltas; pelo menos uma pista de entardecer ou noite por copa | `tests/track.test.ts` |
| Copa, país, região, estado e pista com nome em PT e EN (`core.cup.<id>`, `core.country.<País>`, `core.region.<id>`, `core.state.<UF>`, `core.track.<id>` — PT = `name` da pista) | `tests/track.test.ts`, `tests/i18n.test.ts` |
| A Cuia da Serra Gaúcha desenha uma cuia no minimapa (bojo > boca > cintura, de pé, simétrica, fundo redondo) | `tests/track.test.ts` (Cuia) |
| Uma conquista `COPA_<ID>` para cada copa, `REGIAO_<ID>` por região, `PASSAPORTE_COMPLETO` | `tests/desktop.test.ts` |
| Um rival por região na Expedição (o mesmo nos estados dela) e um por país no Mundial, todos diferentes, com falas próprias | `tests/rivals.test.ts` |
| IA completa volta sem travar em toda pista; fica na pista nas de dificuldade 5 | `tests/ai.test.ts` (um teste por pista) |
| IA termina a corrida INTEIRA (todas as voltas) sem ficar sem combustível, em toda pista | `tests/ai.test.ts` (corrida inteira, um teste por pista) |
| Aviso de combustível ao jogador chega ≥ 0,1 volta antes do último box que salva a corrida, guiando como a IA ou de pé no fundo, e nunca na última volta | `tests/fuel.test.ts` (dois estilos por pista) |
| Box logo depois da linha de chegada (o aviso e a IA contam com isso) | `tests/track.test.ts` |
| Toda copa cabe numa linha da grade de pistas (3 a `TRACK_GRID_COLS` = 4) | `tests/select.test.ts` |

A regra antiga "8 copas × 4, média de dificuldade sempre subindo" não cabe em 27 copas de 3 pistas: a média só anda
de 1/3 em 1/3, então de 1 a 5 há 13 valores para 27 copas. Na Expedição a média **não cai**; quem sobe a cada copa é o
índice técnico, que é contínuo.

**Índice técnico** (só nos testes, não entra no jogo): perda média de velocidade nas curvas, em %, do carro de
referência (`falcao`, via `holdableSpeedFraction`) + inclinação média × 20. Faixas por nível hoje: 1 → 0,3–1,2 ·
2 → 0,0–5,0 · 3 → 4,9–9,3 · 4 → 9,4–11,9 · 5 → 12,2–16,4.

Média por copa (índice · dificuldade média):

- Expedição Brasil: RJ 1,4 (dif. 1,33) · SP 3,4 (dif. 2,00) · MG 3,5 (dif. 2,00) · ES 3,7 (dif. 2,00) · PR 4,2 (dif. 2,33) · SC 4,4 (dif. 2,33) · RS 6,1 (dif. 2,67) · DF 6,4 (dif. 2,67) · GO 6,7 (dif. 2,67) · MS 7,1 (dif. 3,00) · MT 7,3 (dif. 3,00) · BA 7,9 (dif. 3,00) · SE 8,1 (dif. 3,33) · AL 8,3 (dif. 3,33) · PE 9,2 (dif. 3,67) · PB 9,5 (dif. 3,67) · RN 9,6 (dif. 3,67) · CE 10,0 (dif. 4,00) · PI 10,2 (dif. 4,00) · MA 10,3 (dif. 4,00) · PA 11,2 (dif. 4,33) · AM 11,4 (dif. 4,33) · AP 11,5 (dif. 4,33) · RR 12,4 (dif. 4,67) · RO 12,8 (dif. 4,67) · AC 13,0 (dif. 4,67) · TO 13,8 (dif. 5,00)
- Mundial: eua 4,7 (dif. 2,25) · japao 7,5 (dif. 3,25) · europa 8,6 (dif. 3,75) · africa_do_sul 10,3 (dif. 4,00) · australia 10,9 (dif. 4,25) · escandinavia 12,2 (dif. 4,50) · mediterraneo 14,1 (dif. 4,75)

## Como as 77 pistas novas foram traçadas

Cada pista tem um **traçado-base** com a identidade do lugar (comentário em cima dela em `tracks.ts`): as 23 da
primeira leva foram escritas à mão a partir da tabela do contrato; as 54 de `EXTRA_BRAZIL_PLACES` saíram de um
estilo por lugar — orla (retas e curvas abertas), serra (morros e grampos), cidade (esquinas fortes), dunas
(lombadas em sequência), rio (curvas longas), estrada (retas com ondulações), esses. Depois cada traçado foi
**ajustado ao índice técnico alvo da sua copa** escalando as curvas (teto 6) e os morros, e, quando não bastava,
encurtando as retas — a rampa acima é o resultado. Volta inteira não gira para lado nenhum (soma das curvas ~0)
desenha um risco no minimapa: os traçados novos giram de verdade para um lado.

**A cuia (RS, `cuia_gaucha`).** O minimapa integra o rumo (`trackOutline`, `src/render/minimap.ts`) normalizando a
soma das curvas para uma volta: o desenho depende só da proporção entre as curvas. A cuia é horária, largando no
lado esquerdo do bojo: arco aberto de 1,4 subindo o bojo, curva à esquerda de -6 (a cintura), reta inclinada para
fora (o gargalo abrindo), cotovelo de 6, a borda da boca, cotovelo de 6, gargalo descendo, -6 de novo, e quatro arcos
de 1,4 dão a volta no bojo. Separar o fundo em vários arcos (cada operação de curva tem entrada e saída suaves)
foi o que fez o bojo ficar redondo — um arco só de 180° desenhava um "U" alto. Os morros (serra) só entram nas
retas e como subida das curvas: altura não muda o contorno. `npx tsx scripts/track-outline.ts cuia_gaucha` desenha
(ASCII ou `--svg`):

```


                    ####################
                    #                  ##
                    ##                  #
                     ####            ####
                        ####       ###
                           ##   ####
                           ##   #
                       #####    ##
                   #####         #####
                 ###                 #####
               ###                       ###
               #                           ##
              ##                            ##
              #                              #
              #                              #
              #                              ##
              S                               #
             ##                               #
             #                               ##
             ##                              #
              ##                            ##
               ##                          ##
                ###                      ###
                  ####                ####
                     ####         #####
                        ###########
```

## Migração (save de antes da onda G)

`LEGACY_CUP_IDS` (`cups.ts`): `brasil` → `br_rj`. Na leitura do save (`src/game/save.ts`, `career-save.ts`):

| O que | Vira |
|---|---|
| `cupsCompleted: ['brasil', …]` | `br_rj` (e o carimbo do RJ) |
| conquista `COPA_BRASIL` | `COPA_BR_RJ` |
| carreira com `cupId: 'brasil'` | carreira na `br_rj`, com dinheiro, garagem e estatísticas; a copa em andamento e a contagem de tentativas recomeçam (as corridas eram outras) |
| campeonato normal em andamento na `brasil` | some do save (sem erro; o menu deixa de oferecer "Continuar") |
| recordes, fantasmas, estatísticas | nada muda: são por id de pista, e nenhum id mudou |
| quem já tinha vencido copas do Mundial | elas continuam abertas (`isCupUnlocked`: copa concluída fica aberta), e a seguinte também; a Expedição começa em SP |

Carreira antiga numa copa do Mundial (eua, japao…) continua nela: o nível da IA e o prêmio passam a ser os da posição
nova da copa (mais altos), e a carreira termina no Mediterrâneo sem passar pelo Brasil. Teste:
`tests/migration-brasil.test.ts` (save gravado como era antes).

## Catálogo

Só cenários que já existem (`tropical, desert, city_night, alpine, coast, savanna`) × `day/dusk/night`. Os nomes usam
lugares reais só como referência geográfica, sem marca nenhuma; o nome em EN traduz a descrição, não o lugar.

Na cidade (`city_night`) os dois lados da pista são um paredão de prédios e torres na beira (a receita do `builder`);
nas pistas de cidade com marcos turísticos o núcleo abre **praças** — trechos de ~244 m de um lado sem prédio, torre
nem outdoor — onde o renderizador põe cada marco de perto/longe, para ele ser visto de quem chega
(`core/track/plazas.ts`; `docs/VISUAL.md`, "Marcos turísticos").

| Copa | Pista (id) | Nome | Nome (EN) | Cenário | Período | Voltas | Dif. | Segm. | Índice |
|---|---|---|---|---|---|---|---|---|---|
| RJ | `copacabana` | Orla de Copacabana | Copacabana Beachfront | coast | dia | 3 | 1 | 1800 | 0,6 |
| RJ | `paraty` | Caminho do Ouro de Paraty | Paraty Gold Trail | coast | entardecer | 3 | 1 | 2060 | 1,2 |
| RJ | `serra_dos_orgaos` | Serra dos Órgãos | Organ Mountains | tropical | dia | 3 | 2 | 1970 | 2,5 |
| SP | `ilhabela` | Canal de Ilhabela | Ilhabela Channel | coast | dia | 3 | 1 | 1800 | 1,0 |
| SP | `campos_do_jordao` | Campos do Jordão | Campos do Jordão | alpine | entardecer | 3 | 2 | 1890 | 3,0 |
| SP | `sampa_noite` | Noite em Sampa | São Paulo Nights | city_night | noite | 4 | 3 | 1850 | 6,1 |
| MG | `pampulha` | Lagoa da Pampulha | Pampulha Lake | city_night | entardecer | 3 | 1 | 2050 | 1,2 |
| MG | `ouro_preto` | Ladeiras de Ouro Preto | Ouro Preto Hills | tropical | entardecer | 3 | 2 | 1870 | 3,6 |
| MG | `serra_da_canastra` | Serra da Canastra | Canastra Range | savanna | dia | 3 | 3 | 1930 | 5,8 |
| ES | `itaunas` | Dunas de Itaúnas | Itaúnas Dunes | coast | dia | 3 | 1 | 1830 | 1,2 |
| ES | `convento_penha` | Convento da Penha | Penha Convent | coast | dia | 3 | 2 | 1980 | 4,0 |
| ES | `pedra_azul` | Pedra Azul | Blue Rock | alpine | entardecer | 3 | 3 | 1860 | 6,0 |
| PR | `foz_do_iguacu` | Cataratas do Iguaçu | Iguaçu Falls | tropical | dia | 3 | 2 | 1960 | 2,1 |
| PR | `serra_do_mar` | Serra do Mar | Serra do Mar | tropical | dia | 3 | 2 | 1730 | 4,9 |
| PR | `curitiba` | Ópera de Arame | Wire Opera House | city_night | noite | 4 | 3 | 1990 | 5,6 |
| SC | `floripa` | Ponte Hercílio Luz | Hercílio Luz Bridge | coast | entardecer | 3 | 2 | 2010 | 3,0 |
| SC | `camboriu` | Avenida Atlântica de Camboriú | Camboriú Beachfront | city_night | noite | 4 | 2 | 1780 | 4,1 |
| SC | `rio_do_rastro` | Serra do Rio do Rastro | Rio do Rastro Pass | alpine | dia | 3 | 3 | 2040 | 6,1 |
| RS | `orla_guaiba` | Orla do Guaíba | Guaíba Waterfront | coast | entardecer | 3 | 2 | 2020 | 3,7 |
| RS | `cuia_gaucha` | Cuia da Serra Gaúcha | Gaúcha Gourd Circuit | alpine | entardecer | 3 | 3 | 1970 | 7,0 |
| RS | `aparados_da_serra` | Aparados da Serra | Aparados Canyons | alpine | dia | 3 | 3 | 1860 | 7,5 |
| DF | `lago_paranoa` | Lago Paranoá | Paranoá Lake | coast | dia | 3 | 2 | 1770 | 4,5 |
| DF | `torre_de_tv` | Torre de TV | TV Tower | city_night | noite | 4 | 3 | 1720 | 7,0 |
| DF | `brasilia` | Eixo Monumental | Monumental Axis | city_night | entardecer | 3 | 3 | 2120 | 7,7 |
| GO | `pirenopolis` | Ruas de Pirenópolis | Pirenópolis Streets | tropical | entardecer | 3 | 2 | 1840 | 4,9 |
| GO | `caldas_novas` | Águas de Caldas Novas | Caldas Novas Springs | savanna | dia | 3 | 3 | 1990 | 7,2 |
| GO | `chapada_veadeiros` | Chapada dos Veadeiros | Veadeiros Plateau | savanna | dia | 3 | 3 | 1940 | 7,8 |
| MS | `bonito` | Rios de Bonito | Bonito Rivers | tropical | dia | 3 | 3 | 2020 | 7,0 |
| MS | `campo_grande` | Avenidas de Campo Grande | Campo Grande Avenues | city_night | entardecer | 3 | 3 | 1820 | 7,0 |
| MS | `estrada_parque` | Estrada Parque do Pantanal | Pantanal Park Road | savanna | entardecer | 3 | 3 | 1990 | 7,2 |
| MT | `transpantaneira` | Transpantaneira | Transpantaneira | savanna | entardecer | 3 | 1 | 2130 | 0,3 |
| MT | `cuiaba` | Centro Geodésico de Cuiabá | Cuiabá Geodesic Center | city_night | noite | 4 | 4 | 1770 | 10,7 |
| MT | `chapada_guimaraes` | Chapada dos Guimarães | Guimarães Plateau | savanna | dia | 3 | 4 | 1900 | 11,0 |
| BA | `porto_seguro` | Costa de Porto Seguro | Porto Seguro Coast | coast | dia | 3 | 2 | 1870 | 5,0 |
| BA | `salvador` | Orla de Salvador | Salvador Waterfront | coast | entardecer | 3 | 3 | 1980 | 8,9 |
| BA | `chapada_diamantina` | Chapada Diamantina | Diamantina Plateau | savanna | dia | 3 | 4 | 2040 | 9,9 |
| SE | `aracaju` | Orla de Atalaia | Atalaia Beach | coast | noite | 4 | 3 | 1790 | 6,8 |
| SE | `sao_cristovao` | Praça de São Cristóvão | São Cristóvão Square | tropical | entardecer | 3 | 3 | 1850 | 7,4 |
| SE | `xingo` | Cânions do Xingó | Xingó Canyons | desert | dia | 3 | 4 | 1890 | 10,0 |
| AL | `maragogi` | Piscinas de Maragogi | Maragogi Reefs | coast | dia | 3 | 2 | 1930 | 3,3 |
| AL | `maceio` | Orla de Maceió | Maceió Waterfront | coast | noite | 4 | 4 | 1760 | 10,6 |
| AL | `foz_sao_francisco` | Foz do São Francisco | São Francisco River Mouth | desert | entardecer | 3 | 4 | 1950 | 11,0 |
| PE | `recife_antigo` | Recife Antigo | Old Recife | city_night | noite | 4 | 3 | 2070 | 7,6 |
| PE | `olinda` | Ladeiras de Olinda | Olinda Hills | coast | dia | 3 | 4 | 1770 | 10,0 |
| PE | `noronha` | Fernando de Noronha | Fernando de Noronha | coast | entardecer | 3 | 4 | 1970 | 10,0 |
| PB | `joao_pessoa` | Ponta do Seixas | Easternmost Point | coast | entardecer | 3 | 3 | 1930 | 8,2 |
| PB | `campina_grande` | Parque do Povo | People's Park | city_night | noite | 4 | 4 | 1820 | 10,0 |
| PB | `pedra_da_boca` | Pedra da Boca | Mouth Rock | desert | dia | 3 | 4 | 1960 | 10,3 |
| RN | `natal` | Dunas de Genipabu | Genipabu Dunes | coast | dia | 3 | 3 | 1910 | 8,0 |
| RN | `ponta_negra` | Morro do Careca | Bald Hill Beach | coast | entardecer | 3 | 4 | 1950 | 10,2 |
| RN | `cajueiro_pirangi` | Cajueiro de Pirangi | Pirangi Cashew Tree | tropical | dia | 3 | 4 | 2030 | 10,4 |
| CE | `jericoacoara` | Jericoacoara | Jericoacoara | coast | entardecer | 3 | 4 | 1890 | 10,1 |
| CE | `fortaleza_beira_mar` | Beira-Mar de Fortaleza | Fortaleza Seafront | city_night | noite | 4 | 4 | 1680 | 9,9 |
| CE | `canoa_quebrada` | Falésias de Canoa Quebrada | Canoa Quebrada Cliffs | desert | dia | 3 | 4 | 1950 | 10,0 |
| PI | `delta_parnaiba` | Delta do Parnaíba | Parnaíba Delta | tropical | dia | 3 | 4 | 1930 | 10,0 |
| PI | `sete_cidades` | Pedras de Sete Cidades | Seven Cities Rocks | savanna | entardecer | 3 | 4 | 2090 | 10,2 |
| PI | `serra_capivara` | Serra da Capivara | Capivara Range | desert | entardecer | 3 | 4 | 1890 | 10,3 |
| MA | `sao_luis` | Casarões de São Luís | São Luís Tiles | city_night | noite | 4 | 4 | 2070 | 10,2 |
| MA | `chapada_das_mesas` | Chapada das Mesas | Mesas Plateau | savanna | dia | 3 | 4 | 1720 | 10,3 |
| MA | `lencois` | Lençóis Maranhenses | Lençóis Maranhenses | desert | entardecer | 3 | 4 | 1700 | 10,5 |
| PA | `alter_do_chao` | Praias de Alter do Chão | Alter do Chão Beaches | tropical | dia | 3 | 4 | 1920 | 10,4 |
| PA | `belem` | Ver-o-Peso | Belém Docks | coast | entardecer | 3 | 4 | 1800 | 10,6 |
| PA | `marajo` | Campos do Marajó | Marajó Fields | savanna | entardecer | 3 | 5 | 1940 | 12,7 |
| AM | `manaus` | Encontro das Águas | Meeting of Waters | coast | dia | 3 | 4 | 2110 | 10,6 |
| AM | `ponte_rio_negro` | Ponte do Rio Negro | Rio Negro Bridge | city_night | noite | 4 | 4 | 1990 | 10,8 |
| AM | `parintins` | Bumbódromo de Parintins | Parintins Arena | tropical | entardecer | 3 | 5 | 1930 | 12,9 |
| AP | `macapa` | Marco Zero do Equador | Equator Line | tropical | dia | 3 | 3 | 1750 | 8,7 |
| AP | `pororoca_araguari` | Pororoca do Araguari | Araguari Tidal Bore | tropical | entardecer | 3 | 5 | 1900 | 12,8 |
| AP | `serra_do_navio` | Serra do Navio | Navio Range | tropical | dia | 3 | 5 | 1800 | 13,0 |
| RR | `boa_vista` | Avenidas de Boa Vista | Boa Vista Avenues | city_night | noite | 4 | 4 | 1620 | 11,0 |
| RR | `monte_roraima` | Monte Roraima | Mount Roraima | savanna | dia | 3 | 5 | 2040 | 13,0 |
| RR | `lago_caracarana` | Lago Caracaranã | Caracaranã Lake | savanna | entardecer | 3 | 5 | 1880 | 13,3 |
| RO | `porto_velho` | Madeira-Mamoré | Madeira-Mamoré Railway | tropical | entardecer | 3 | 4 | 1960 | 11,3 |
| RO | `forte_principe` | Forte Príncipe da Beira | Prince of Beira Fort | tropical | dia | 3 | 5 | 1820 | 13,4 |
| RO | `vale_guapore` | Vale do Guaporé | Guaporé Valley | savanna | entardecer | 3 | 5 | 1920 | 13,6 |
| AC | `rio_branco` | Gameleira de Rio Branco | Rio Branco Riverside | tropical | dia | 3 | 4 | 2080 | 11,4 |
| AC | `geoglifos` | Geoglifos do Acre | Acre Geoglyphs | savanna | entardecer | 3 | 5 | 1940 | 13,6 |
| AC | `estrada_pacifico` | Estrada do Pacífico | Pacific Highway | tropical | dia | 3 | 5 | 1890 | 13,8 |
| TO | `jalapao` | Dunas do Jalapão | Jalapão Dunes | desert | entardecer | 3 | 5 | 1890 | 13,6 |
| TO | `palmas` | Ponte de Palmas | Palmas Bridge | city_night | noite | 4 | 5 | 1780 | 13,8 |
| TO | `ilha_do_bananal` | Ilha do Bananal | Bananal Island | savanna | dia | 3 | 5 | 1770 | 14,0 |
| Estados Unidos | `rota_66` | Rota 66 | Route 66 | desert | dia | 3 | 1 | 1800 | 0,3 |
| Estados Unidos | `rochosas` | Montanhas Rochosas | Rocky Mountains | alpine | dia | 3 | 2 | 2080 | 4,3 |
| Estados Unidos | `canion` | Cânion de Nevada | Nevada Canyon | desert | entardecer | 3 | 3 | 1660 | 9,3 |
| Estados Unidos | `las_vegas` | Strip de Las Vegas | Las Vegas Strip | city_night | noite | 4 | 3 | 1760 | 5,0 |
| Japão | `baia_toquio` | Baía de Tóquio | Tokyo Bay | coast | entardecer | 3 | 2 | 1800 | 1,5 |
| Japão | `yanbaru` | Floresta de Yanbaru | Yanbaru Forest | tropical | dia | 3 | 3 | 1940 | 7,6 |
| Japão | `monte_fuji` | Monte Fuji | Mount Fuji | alpine | dia | 3 | 4 | 1670 | 11,0 |
| Japão | `osaka_neon` | Neon de Osaka | Osaka Neon | city_night | noite | 4 | 4 | 1800 | 10,0 |
| Europa | `autobahn` | Autobahn | Autobahn | savanna | dia | 3 | 2 | 2010 | 0,0 |
| Europa | `paris` | Boulevards de Paris | Paris Boulevards | city_night | entardecer | 4 | 3 | 2010 | 5,8 |
| Europa | `passo_alpino` | Passo Alpino | Alpine Pass | alpine | dia | 3 | 5 | 1570 | 16,2 |
| Europa | `monaco_noite` | Porto de Mônaco | Monaco Harbour | coast | noite | 4 | 5 | 1840 | 12,2 |
| África do Sul | `kruger` | Savana do Kruger | Kruger Savanna | savanna | dia | 3 | 3 | 2110 | 6,6 |
| África do Sul | `karoo` | Deserto do Karoo | Karoo Desert | desert | entardecer | 3 | 4 | 2090 | 10,2 |
| África do Sul | `drakensberg` | Serra do Drakensberg | Drakensberg Range | alpine | dia | 3 | 4 | 1910 | 10,9 |
| África do Sul | `boa_esperanca` | Cabo da Boa Esperança | Cape of Good Hope | coast | noite | 4 | 5 | 1760 | 13,5 |
| Austrália | `outback` | Poeira do Outback | Outback Dust | desert | dia | 3 | 3 | 2090 | 4,9 |
| Austrália | `great_ocean` | Great Ocean Road | Great Ocean Road | coast | entardecer | 3 | 4 | 1970 | 10,1 |
| Austrália | `daintree` | Selva de Daintree | Daintree Rainforest | tropical | dia | 3 | 5 | 1760 | 16,1 |
| Austrália | `sydney` | Ponte de Sydney | Sydney Bridge | city_night | noite | 4 | 5 | 1800 | 12,4 |
| Escandinávia | `atlantico` | Estrada do Atlântico | Atlantic Road | coast | dia | 3 | 4 | 1810 | 9,4 |
| Escandinávia | `laponia` | Meia-Noite na Lapônia | Lapland Midnight | alpine | entardecer | 3 | 4 | 2030 | 10,1 |
| Escandinávia | `trollstigen` | Trollstigen | Trollstigen | alpine | dia | 3 | 5 | 1790 | 16,4 |
| Escandinávia | `tromso` | Aurora de Tromsø | Tromsø Aurora | coast | noite | 4 | 5 | 1870 | 13,0 |
| Mediterrâneo | `amalfi` | Costa Amalfitana | Amalfi Coast | coast | dia | 3 | 4 | 1880 | 11,9 |
| Mediterrâneo | `santorini` | Caldeira de Santorini | Santorini Caldera | coast | entardecer | 3 | 5 | 1810 | 15,6 |
| Mediterrâneo | `etna` | Vulcão Etna | Mount Etna | desert | dia | 3 | 5 | 1810 | 16,0 |
| Mediterrâneo | `roma` | Noite em Roma | Rome by Night | city_night | noite | 4 | 5 | 1870 | 13,0 |

O cenário é o mais próximo que existe: o Pantanal usa a savana; o campo de lava do Etna usa o deserto; a Lapônia e o
Trollstigen usam o alpino; dunas do Nordeste e do Jalapão usam o deserto; o lavrado de Roraima e o cerrado, a savana.

**Bandeiras de regiões.** Escandinávia usa 🇳🇴 (três das quatro pistas são na Noruega — Atlântico, Trollstigen,
Tromsø; a Lapônia é de três países). Mediterrâneo usa 🇮🇹 (Amalfi, Etna e Roma; Santorini é grega). A Europa segue
com 🇪🇺; as 27 copas de estado usam 🇧🇷 (a sigla do estado é o carimbo).

## Balanceamento (dados, 25/09/2026)

As tabelas abaixo são das 32 pistas de antes da onda G. As 77 novas passam pelos mesmos testes por pista (IA
completa a volta sem travar e fica na pista nas de nível 5; corrida inteira sem pane seca; aviso de combustível nos
dois estilos — `tests/ai.test.ts` 251 testes, `tests/fuel.test.ts` 227, todos verdes em 02/10), mas a rodada de
`scripts/balance.ts` (melhor volta, grama, batidas) ainda não foi feita nelas.

`npx tsx scripts/balance.ts 150 profissional 11 <id>`: 19 carros de IA + 1 humano parado, 150 s de
corrida. "Voltas IA 2–2" = toda a IA completou a primeira volta (as pistas novas, mais longas, não
fecham a segunda em 150 s). Critério: IA completa volta, grama < 1%, melhor volta entre ~1:00 e ~1:40.

| Pista | Segm. | Voltas IA | Melhor volta | Pior volta | Grama | Batidas | Cenário | v média |
|---|---|---|---|---|---|---|---|---|
| transpantaneira | 2130 | 2–2 | 1:21.46 | 1:35.10 | 0,0% | 102 | 0 | 0,83 |
| rochosas | 2080 | 2–2 | 1:22.43 | 1:34.36 | 0,0% | 127 | 0 | 0,79 |
| yanbaru | 1940 | 2–2 | 1:21.43 | 1:29.90 | 0,0% | 131 | 0 | 0,77 |
| paris | 2010 | 2–2 | 1:27.40 | 1:36.95 | 0,0% | 157 | 0 | 0,82 |
| kruger | 2110 | 2–2 | 1:29.38 | 1:37.95 | 0,0% | 122 | 0 | 0,73 |
| karoo | 2090 | 2–2 | 1:32.23 | 1:41.01 | 0,0% | 160 | 0 | 0,78 |
| drakensberg | 1910 | 2–2 | 1:23.98 | 1:29.50 | 0,0% | 157 | 0 | 0,77 |
| boa_esperanca | 1760 | 2–2 | 1:25.86 | 1:32.06 | 0,0% | 248 | 2 | 0,66 |
| outback | 2090 | 2–2 | 1:26.78 | 1:36.23 | 0,0% | 163 | 0 | 0,78 |
| great_ocean | 1970 | 2–2 | 1:28.71 | 1:34.18 | 0,1% | 169 | 0 | 0,72 |
| daintree | 1760 | 2–2 | 1:27.75 | 1:35.76 | 0,0% | 211 | 0 | 0,55 |
| sydney | 1800 | 2–2 | 1:28.78 | 1:36.23 | 0,0% | 191 | 0 | 0,60 |
| atlantico | 1810 | 2–2 | 1:19.18 | 1:24.46 | 0,0% | 172 | 0 | 0,70 |
| laponia | 2030 | 2–2 | 1:33.36 | 1:37.28 | 0,0% | 207 | 0 | 0,62 |
| trollstigen | 1790 | 2–2 | 1:25.21 | 1:34.08 | 0,0% | 194 | 0 | 0,60 |
| tromso | 1870 | 2–2 | 1:30.63 | 1:38.41 | 0,0% | 148 | 0 | 0,67 |
| amalfi | 1880 | 2–2 | 1:25.68 | 1:31.21 | 0,0% | 187 | 1 | 0,74 |
| santorini | 1810 | 2–2 | 1:28.10 | 1:36.95 | 0,1% | 203 | 0 | 0,57 |
| etna | 1810 | 2–2 | 1:27.35 | 1:33.26 | 0,0% | 162 | 0 | 0,60 |
| roma | 1870 | 2–2 | 1:33.28 | 1:39.06 | 0,0% | 192 | 1 | 0,62 |

As 12 pistas antigas, na mesma rodada, ficam entre 1:04 e 1:28 (Copacabana 1:04.26, Mônaco 1:28.25):
as novas são mais longas por volta — as copas do fim pedem mais tempo de concentração, como no original.

Três dificuldades nas mesmas três pistas novas (uma de cada ponta):

| Pista | Amador | Profissional | Campeão |
|---|---|---|---|
| transpantaneira (dif. 1) | 1:34.68 · grama 0,0% | 1:21.46 · 0,0% | 1:09.08 · 0,8% (8 paradas no box) |
| kruger (dif. 3) | 1:39.01 · 0,0% | 1:29.38 · 0,0% | 1:22.36 · 0,0% |
| sydney (dif. 5) | 1:33.36 · 0,0% | 1:28.78 · 0,0% | 1:24.28 · 0,0% |

A ordem Amador > Profissional > Campeão vale nas três; na pista travada (Sydney) a diferença encolhe,
porque ali quem manda é a curva, não a velocidade máxima. Depois da regra de combustível (abaixo) a
rodada de 150 s em Profissional deu os mesmos números nas 32 pistas (a 1ª volta não muda); no Campeão
da Transpantaneira, quem fecha a 2ª volta em 150 s decide pelo consumo medido e para no box antes da
última quando ele não garante a volta (eram 2 paradas, são 8), e a entrada na faixa do box, que conta
como grama, sobe a grama de 0,3% para 0,8%.

**Onda C (28/09, humano "médio" simulado, 32 pistas × 3 sementes)**: a posição dele depende quase só do nível
da pista (profissional: 1,7º nas de nível 1, 16,6º nas de nível 5). **Great Ocean e Amalfi** (nível 4) se
comportam como nível 5 (15,7º e 16,0º, contra 10,8º de média do nível 4); reclassificar exige reordenar a copa
delas (a dificuldade cresce dentro de cada copa, e um teste segura isso) — decisão de design em aberto.
Daintree, Santorini e Roma são as únicas em que ele não vence nem no amador (~7º).

## Combustível nas voltas longas

O tanque (`data/cars.ts`) foi calibrado para voltas de ~400.000 unidades; as pistas novas vão até
426.000 (Transpantaneira, 2.130 segmentos). Com as regras antigas, de nível fixo, isso quebrava a
corrida inteira — o que o balance de 150 s (só a 1ª volta) não mostra:

- a IA só entrava no box abaixo de 22%: o Trovão chegava à última volta da Kruger com ~0,33, não
  parava e secava antes da chegada (andando a 20% da velocidade);
- o aviso "COMBUSTÍVEL BAIXO — ENTRE NO BOX" vinha em 25%, já depois do último box que salvava a
  corrida — e também na última volta, quando não há mais box antes da chegada.

Regras de agora (`src/core/sim/fuel.ts`, constantes em `constants.ts`):

| Quem | Regra |
|---|---|
| IA — box | Com o box à frente, para se o que resta não chega à próxima passagem por ele (uma volta e a aproximação) ou à chegada, o que vier antes, no consumo **medido volta a volta** × `FUEL_PIT_MARGIN` (1,15). O consumo é o maior entre a última volta inteira medida e a volta em curso (depois de meia volta); sem nada medido (largada, IA que assumiu o carro agora), o pior caso: aceleração total. |
| IA — elástico | Atrás do humano o elástico a empurra e ela acelera o tempo todo (~50% a mais por volta que o medido). Com o tanque justo — que não garante, em aceleração total, chegar à próxima linha ou à chegada — ela não aceita o empurrão. |
| Jogador — aviso | Quando o tanque já não garante `FUEL_LOW_LAPS` (1,15) voltas em aceleração total, e só enquanto ainda há box antes da chegada. Quem vai de pé no fundo gasta 0,95–1,0 disso por volta; a sobra é o tempo de ver o aviso e ir para o box. |
| Jogador — barra | A barra do HUD continua vermelha abaixo de 25% (tanque quase vazio): é medidor, não conselho de box. |

Corrida inteira (voltas da pista) nas 32 pistas, 6 sementes (42, 7, 99, 1, 2, 3), montagem do
`scripts/smoke.ts` (2 humanos em piloto automático que param no box + 18 IA, todas as assistências):
carros que ficaram sem combustível ANTES da chegada, somados.

| Dificuldade | Sem combustível, antes (22%) | agora | Paradas no box, antes | agora |
|---|---|---|---|---|
| Amador | 17 | 1 | 503 | 384 |
| Profissional | 153 | 0 | 1.388 | 714 |
| Campeão | 680 | 0 | 1.914 | 1.528 |

Menos paradas e nenhuma pane: a regra antiga parava quem não precisava (abaixo de 22% na última
passagem, com a chegada logo ali) e deixava passar quem precisava. No Campeão as panes vinham também
das 12 pistas antigas — o problema só ficou visível com as voltas longas.

O caso que sobrou (Amador, Paris, semente 2): o Trovão decidiu parar certo na última passagem, mas
um carro colado do lado direito o impediu de chegar à faixa do box. É a entrada do box no trânsito
(comportamento anterior a este passo), não a decisão. Com 8 voltas (o máximo da corrida rápida) em
cinco pistas longas, 2 sementes, profissional e campeão: 0, com duas ou três paradas por carro.

## Interface (src/ui/screens/select.ts + select.css) — o que a onda G pede às telas

As telas são de outra tarefa; o núcleo já entrega o dado. O que mudou para elas:

- **34 copas em vez de 8, em duas etapas.** `cup.stage` (`'brasil'` | `'mundial'`); na Expedição, `cup.region` e
  `cup.state` (sigla). `BRAZIL_REGIONS` dá a ordem das regiões e dos estados; `stageCups(stage)`, `stateCup(uf)`.
  A lista de copas precisa agrupar por etapa e, na Expedição, por região (27 linhas não cabem sem rolar em 720p).
- **Copas de 3 pistas** (estados) ao lado de copas de 4 (Mundial). A grade de pistas com uma copa por linha
  continua valendo, mas a linha de estado deixa a 4ª coluna vazia: ↑↓ vindo da 4ª coluna para uma copa de 3 tem de
  parar na 3ª (`tests/select.test.ts` agora exige só "cabe numa linha": 3 a `TRACK_GRID_COLS`). A dificuldade média
  da copa e o "Próxima corrida: N de M" já saem de `trackIds.length`. Torneio (modo de festa) numa copa de 3 pistas
  só cabe em até 2 rodadas + final (`tournamentTracks`/`setupError` recusam 3 rodadas); a tela devia limitar.
- **Nomes**: `core.track.<id>` (PT = `def.name`, EN traduzido), `core.cup.<id>`, `core.region.<id>`,
  `core.state.<UF>`, `core.stage.brasil` ("Expedição Brasil" / "Brazil Expedition"), `core.stage.mundial`
  ("Mundial" / "World Circuit"). Até aqui a tela mostrava `def.name` nos dois idiomas.
- **Passaporte**: `save.stamps` (siglas, na ordem em que foram carimbadas); `markCupCompleted` carimba. Textos
  prontos em `core.passport.*` (título, carimbo, cartão-postal, "{state} carimbado no passaporte!", "{n} de {total}
  estados carimbados", "Região {region} completa no passaporte!"). Marcos para o cartão-postal: `places.ts`.
- **Destravamento**: copa já concluída fica aberta (`isCupUnlocked`); com save antigo, mais de uma copa pode estar
  "aberta" ao mesmo tempo (o cursor começa na primeira aberta não concluída — `frontierCupIndex` já faz isso).
- `scripts/pistas-ui.mjs`, `playtest-layout.mjs`, `playtest-records.mjs` e `playtest.mjs` ainda usam o id `brasil`
  e `COPA_BRASIL` (o save de teste passa pela migração, mas o roteiro que espera o foco em "brasil" vai falhar).

Como era até a onda F (8 copas de 4): lista de copas à esquerda com o detalhe à direita; grade de pistas com uma
linha de 4 por copa, ↑↓ trocando de copa na mesma coluna; cabe sem rolar em 1280×720 e 1920×1080; Steam Deck e
1024×600 conferidos com `scripts/pistas-ui.mjs`.

## Pista nova — passo a passo

1. Escreva a pista em `tracks.ts`, na posição da copa, com um comentário de identidade (o que o
   traçado representa). Curva 2 fácil / 4 média / 6 forte; lombada = `hl(comprimento, altura)`. Volta que não gira
   para lado nenhum (soma das curvas ~0) vira um risco no minimapa.
2. Ponha o id em `trackIds` da copa (`cups.ts`) e o lugar em `places.ts`. Nome em `core.track.<id>` (PT e EN). Copa
   nova: id ASCII minúsculo, `stage` (e `region`/`state` na Expedição), `requires` = a copa anterior,
   `core.cup.<id>` e `core.country.<País>` em `src/i18n/core.ts`, conquista `COPA_<ID>` em `ACHIEVEMENTS`
   (`src/game/desktop.ts`), na tabela de `desktop/README.md` (Steamworks) e um rival em `CUP_RIVALS`.
3. `npx vitest run tests/track.test.ts tests/ai.test.ts tests/fuel.test.ts` e
   `npx tsx scripts/balance.ts 150 profissional 11 <id>` (IA completa a volta, grama < 1%, melhor volta
   ~1:00–1:40). Se o índice técnico contradisser o rótulo ou a rampa da etapa, ajuste o traçado ou a dificuldade —
   não o teste. Volta muito mais longa que 2.100 segmentos pede olhar o combustível: os testes de corrida
   inteira e de aviso dizem se o tanque ainda fecha.
