# Pistas e copas — 32 pistas em 8 copas de 4 (passo 3.1 do roteiro)

Como o original: oito copas de quatro pistas, destravadas em sequência. As quatro copas da Fase 0
(Brasil, Estados Unidos, Japão, Europa) ganharam uma pista cada; as quatro novas são África do Sul,
Austrália, Escandinávia e Mediterrâneo. Tudo é dado: pistas em `src/core/track/tracks.ts` (DSL
`straight/curve/hill/s/pit`), copas em `src/core/data/cups.ts`.

## Regras do catálogo (e o teste que segura cada uma)

| Regra | Onde é conferida |
|---|---|
| 8 copas × 4 pistas; toda pista em exatamente uma copa | `tests/track.test.ts` (catálogo) |
| Ids ASCII minúsculos (`^[a-z][a-z0-9_]*$`): o id da copa vira a conquista `COPA_<ID>` | `tests/track.test.ts`, `tests/desktop.test.ts` |
| Destravamento linear na ordem da lista (brasil → eua → japao → europa → africa_do_sul → australia → escandinavia → mediterraneo) | `tests/track.test.ts` |
| Dificuldade (1–5) não cai dentro da copa, e a média sobe de uma copa para a seguinte | `tests/track.test.ts` |
| O índice técnico médio (medido no traçado) também sobe de copa para copa | `tests/track.test.ts` |
| Rótulo não mente: pista dois níveis acima tem traçado mais técnico | `tests/track.test.ts` |
| 1.500–3.000 segmentos, 3–5 voltas; pelo menos uma pista de entardecer ou noite por copa | `tests/track.test.ts` |
| Copa e país com nome em PT e EN (`core.cup.<id>`, `core.country.<País>`) | `tests/track.test.ts`, `tests/i18n.test.ts` |
| Uma conquista `COPA_<ID>` para cada copa de `CUPS` | `tests/desktop.test.ts` |
| IA completa volta sem travar em toda pista; fica na pista nas de dificuldade 5 | `tests/ai.test.ts` (um teste por pista) |
| IA termina a corrida INTEIRA (todas as voltas) sem ficar sem combustível, em toda pista | `tests/ai.test.ts` (corrida inteira, um teste por pista) |
| Aviso de combustível ao jogador chega ≥ 0,1 volta antes do último box que salva a corrida, guiando como a IA ou de pé no fundo, e nunca na última volta | `tests/fuel.test.ts` (dois estilos por pista) |
| Box logo depois da linha de chegada (o aviso e a IA contam com isso) | `tests/track.test.ts` |
| Toda copa com exatamente 4 pistas (a grade de pistas usa uma linha por copa) | `tests/select.test.ts` |

**Índice técnico** (só nos testes, não entra no jogo): perda média de velocidade nas curvas, em %,
do carro de referência (`falcao`, via `holdableSpeedFraction`) + inclinação média × 20. Média por
copa hoje: Brasil 3,0 · EUA 4,7 · Japão 7,5 · Europa 8,6 · África do Sul 10,3 · Austrália 10,9 ·
Escandinávia 12,2 · Mediterrâneo 14,1.

## Catálogo

Só cenários que já existem (`tropical, desert, city_night, alpine, coast, savanna`) × `day/dusk/night`;
cenário novo (neve, vulcão, fiorde) é trabalho gráfico e fica para a Fase 2. Os nomes usam lugares
reais só como referência geográfica, sem marca nenhuma.

| Copa | Pista (id) | Nome | Cenário | Período | Voltas | Dif. | Segm. | Índice | Identidade |
|---|---|---|---|---|---|---|---|---|---|
| Brasil | `copacabana` | Orla de Copacabana | coast | dia | 3 | 1 | 1800 | 0,6 | orla, curvas abertas |
| Brasil | `transpantaneira` ★ | Transpantaneira | savanna | entardecer | 3 | 1 | 2130 | 0,3 | estrada de terra reta no Pantanal; pontes de madeira como lombadas curtas |
| Brasil | `serra_do_mar` | Serra do Mar | tropical | dia | 3 | 2 | 1730 | 4,9 | serra com morros |
| Brasil | `sampa_noite` | Noite em Sampa | city_night | noite | 4 | 3 | 1850 | 6,1 | cidade |
| EUA | `rota_66` | Rota 66 | desert | dia | 3 | 1 | 1800 | 0,3 | rodovia de retas longas |
| EUA | `rochosas` ★ | Montanhas Rochosas | alpine | dia | 3 | 2 | 2080 | 4,3 | rodovia de montanha: subidas e descidas grandes, curvas longas médias |
| EUA | `canion` | Cânion de Nevada | desert | entardecer | 3 | 3 | 1660 | 9,3 | cânion |
| EUA | `las_vegas` | Strip de Las Vegas | city_night | noite | 4 | 3 | 1760 | 5,0 | cidade |
| Japão | `baia_toquio` | Baía de Tóquio | coast | entardecer | 3 | 2 | 1800 | 1,5 | baía |
| Japão | `yanbaru` ★ | Floresta de Yanbaru | tropical | dia | 3 | 3 | 1940 | 7,6 | mata subtropical de Okinawa: esses encadeados entre morrotes |
| Japão | `monte_fuji` | Monte Fuji | alpine | dia | 3 | 4 | 1670 | 11,0 | montanha |
| Japão | `osaka_neon` | Neon de Osaka | city_night | noite | 4 | 4 | 1800 | 10,0 | cidade |
| Europa | `autobahn` | Autobahn | savanna | dia | 3 | 2 | 2010 | 0,0 | rodovia |
| Europa | `paris` ★ | Boulevards de Paris | city_night | entardecer | 4 | 3 | 2010 | 5,8 | avenidas retas cortadas por esquinas de 90°, subida da Champs-Élysées |
| Europa | `passo_alpino` | Passo Alpino | alpine | dia | 3 | 5 | 1570 | 16,2 | passo de montanha |
| Europa | `monaco_noite` | Porto de Mônaco | coast | noite | 4 | 5 | 1840 | 12,2 | circuito de rua no porto |
| África do Sul | `kruger` ★ | Savana do Kruger | savanna | dia | 3 | 3 | 2110 | 6,6 | estrada de safári: retas longas, curvas médias, ondulações |
| África do Sul | `karoo` ★ | Deserto do Karoo | desert | entardecer | 3 | 4 | 2090 | 10,2 | retas enormes com lombadas cegas e curva forte depois da crista |
| África do Sul | `drakensberg` ★ | Serra do Drakensberg | alpine | dia | 3 | 4 | 1910 | 10,9 | grampos em aclive, esses no alto |
| África do Sul | `boa_esperanca` ★ | Cabo da Boa Esperança | coast | noite | 4 | 5 | 1760 | 13,5 | penhasco sobre o Atlântico: esses fortes à beira-mar |
| Austrália | `outback` ★ | Poeira do Outback | desert | dia | 3 | 3 | 2090 | 4,9 | retas sem fim, valas de enchente (baixadas), curva forte no fim da reta |
| Austrália | `great_ocean` ★ | Great Ocean Road | coast | entardecer | 3 | 4 | 1970 | 10,1 | estrada de falésias: curvas e esses com morros |
| Austrália | `daintree` ★ | Selva de Daintree | tropical | dia | 3 | 5 | 1760 | 16,1 | estrada estreita na floresta: esses e curvas fortes quase sem reta |
| Austrália | `sydney` ★ | Ponte de Sydney | city_night | noite | 4 | 5 | 1800 | 12,4 | rua na baía: esquinas, esses e a ponte (lombada longa) |
| Escandinávia | `atlantico` ★ | Estrada do Atlântico | coast | dia | 3 | 4 | 1810 | 9,4 | de ilhota em ilhota: pontes como lombadas íngremes |
| Escandinávia | `laponia` ★ | Meia-Noite na Lapônia | alpine | entardecer | 3 | 4 | 2030 | 10,1 | pinheiros sob o sol da meia-noite: retas com cristas e curvas rápidas |
| Escandinávia | `trollstigen` ★ | Trollstigen | alpine | dia | 3 | 5 | 1790 | 16,4 | a escada dos trolls: grampos alternados subindo, esses descendo |
| Escandinávia | `tromso` ★ | Aurora de Tromsø | coast | noite | 4 | 5 | 1870 | 13,0 | cidade-ilha no Ártico: ponte sobre o fiorde, esses à beira-mar |
| Mediterrâneo | `amalfi` ★ | Costa Amalfitana | coast | dia | 3 | 4 | 1880 | 11,9 | estrada pendurada no penhasco: esses sem fim com morros |
| Mediterrâneo | `santorini` ★ | Caldeira de Santorini | coast | entardecer | 3 | 5 | 1810 | 15,6 | grampos subindo do porto, esses no alto da caldeira |
| Mediterrâneo | `etna` ★ | Vulcão Etna | desert | dia | 3 | 5 | 1810 | 16,0 | subida longa com grampos no campo de lava, descida com esses |
| Mediterrâneo | `roma` ★ | Noite em Roma | city_night | noite | 4 | 5 | 1870 | 13,0 | ruas de pedra: esquinas, esses, a volta do Coliseu e a reta dos Fóruns |

★ = pista nova (20). O cenário é o mais próximo que existe: o Pantanal usa a savana; o campo de lava
do Etna usa o deserto; a Lapônia e o Trollstigen usam o alpino.

**Bandeiras de regiões.** Escandinávia usa 🇳🇴 (três das quatro pistas são na Noruega — Atlântico,
Trollstigen, Tromsø; a Lapônia é de três países). Mediterrâneo usa 🇮🇹 (Amalfi, Etna e Roma; Santorini
é grega). A Europa segue com 🇪🇺.

## Balanceamento (dados, 25/09/2026)

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

## Interface (src/ui/screens/select.ts + select.css)

- **Copas**: lista das 8 à esquerda (bandeira, nome, ✓ concluída / cadeado) e o detalhe da copa em
  foco à direita (país, número de corridas, dificuldade média, selo Aberta/Concluída/"Conclua a …" e
  as 4 pistas com período, voltas, dificuldade e melhor volta). O cursor começa na primeira copa
  aberta ainda não concluída. Cabe sem rolar em 1280×720 e 1920×1080.
- **Pistas**: grade com uma seção por copa (cabeçalho com bandeira e nome da copa) e uma linha de 4
  pistas por copa. ↑↓ trocam de copa na mesma coluna, ←→ andam na copa; a área rola acompanhando o
  foco (`scrollIntoView` + `scroll-margin` que traz o cabeçalho da copa junto). Mouse e roda também.
- Classificação da copa, "Próxima corrida: N de M" e colunas por corrida já saíam de
  `cup.trackIds.length`; nada assumia 3 pistas.

- Telas mais altas que 16:9 (Steam Deck 1280×800, 1024×600): a letra cresce com a altura e a
  largura não. A lista de copas nunca fica mais estreita que o nome mais longo
  (`minmax(max-content, …)`), e nome, voltas e recorde do cartão de pista encolhem juntos com o
  cartão (container query, `cqi`) — "Transpantaneira" é uma palavra só e não quebra.
- O cartão mostra as voltas com que a corrida larga: corrida rápida e contra-relógio usam as voltas
  da corrida rápida (`session.ts`); só a copa corre as voltas da pista.

Roteiro de verificação: `scripts/pistas-ui.mjs` (Playwright, fluxo real de teclado e controle, em
1280×720 e 1920×1080; contra-relógio; e as duas telas em 1280×800, 1366×768, 1600×900 e 1024×600
sem nome cortado). `node scripts/pistas-ui.mjs <url> [prefixo] [pasta] [--tamanhos]`.

## Pista nova — passo a passo

1. Escreva a pista em `tracks.ts`, na posição da copa, com um comentário de identidade (o que o
   traçado representa). Curva 2 fácil / 4 média / 6 forte; lombada = `hl(comprimento, altura)`.
2. Ponha o id em `trackIds` da copa (`cups.ts`). Copa nova: id ASCII minúsculo, `requires` = a copa
   anterior, `core.cup.<id>` e `core.country.<País>` em `src/i18n/core.ts`, conquista `COPA_<ID>` em
   `ACHIEVEMENTS` (`src/game/desktop.ts`) e na tabela de `desktop/README.md` (Steamworks).
3. `npx vitest run tests/track.test.ts tests/ai.test.ts tests/fuel.test.ts` e
   `npx tsx scripts/balance.ts 150 profissional 11 <id>` (IA completa a volta, grama < 1%, melhor volta
   ~1:00–1:40). Se o índice técnico contradisser o rótulo, ajuste o traçado ou a dificuldade — não o
   teste. Volta muito mais longa que 2.100 segmentos pede olhar o combustível: os testes de corrida
   inteira e de aviso dizem se o tanque ainda fecha.
