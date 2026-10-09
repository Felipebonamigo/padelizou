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

Tudo é dado: pistas em `src/core/track/tracks.ts` (DSL `straight/curve/hill/s/pit/bend`; `bend(L, curva[, hill])` é a curva constante, sem a rampa do `cv`, atalho `bd`), copas em `src/core/data/cups.ts`
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
| Uma pista por estado desenha o símbolo do lugar no minimapa (o Cristo, o MASP…), reconhecível pelas propriedades do contorno; contorno sem cruzar nem encostar em si, proporção que cabe no minimapa, curvas no teto 6 | `tests/track.test.ts` ("Pistas com desenho") |
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

- Expedição Brasil: RJ 1,4 (dif. 1,33) · SP 3,4 (dif. 2,00) · MG 3,5 (dif. 2,00) · ES 3,7 (dif. 2,00) · PR 4,2 (dif. 2,33) · SC 4,4 (dif. 2,33) · RS 6,1 (dif. 2,67) · DF 6,4 (dif. 2,67) · GO 6,6 (dif. 2,67) · MS 7,1 (dif. 3,00) · MT 7,3 (dif. 3,00) · BA 7,9 (dif. 3,00) · SE 8,1 (dif. 3,33) · AL 8,3 (dif. 3,33) · PE 9,2 (dif. 3,67) · PB 9,5 (dif. 3,67) · RN 9,6 (dif. 3,67) · CE 10,0 (dif. 4,00) · PI 10,2 (dif. 4,00) · MA 10,3 (dif. 4,00) · PA 11,2 (dif. 4,33) · AM 11,4 (dif. 4,33) · AP 11,5 (dif. 4,33) · RR 12,4 (dif. 4,67) · RO 12,8 (dif. 4,67) · AC 13,0 (dif. 4,67) · TO 13,8 (dif. 5,00)
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

## Pistas com desenho (04/10/2026)

Uma pista por estado desenha no minimapa o símbolo do lugar: a cuia do RS (acima, à mão) e mais 26, geradas a partir
de um polígono. O comentário em cima de cada uma em `tracks.ts` conta o desenho; `tests/track.test.ts` trava a forma.
`npx tsx scripts/shape-to-track.ts --sheet scratch/desenhos.svg` desenha a folha com os 27 contornos.

| UF | Pista | Desenho | UF | Pista | Desenho |
|---|---|---|---|---|---|
| RJ | `copacabana` | Cristo Redentor | SE | `aracaju` | caranguejo |
| SP | `sampa_noite` | MASP (a caixa sobre os pilares, o vão livre) | AL | `maceio` | jangada (vela, casco, ondas) |
| MG | `pampulha` | Igreja da Pampulha (as cinco abóbadas) | PE | `olinda` | sombrinha de frevo |
| ES | `convento_penha` | convento no alto do penhasco | PB | `campina_grande` | balão junino |
| SC | `floripa` | Ponte Hercílio Luz | RN | `cajueiro_pirangi` | **cajueiro de Pirangi** (no lugar do caju) |
| DF | `brasilia` | avião do Plano Piloto | CE | `canoa_quebrada` | lua crescente |
| PR | `curitiba` | araucária | PI | `serra_capivara` | capivara |
| GO | `caldas_novas` | gota d'água | MA | `sao_luis` | cabeça do boi (bumba-meu-boi) |
| MS | `bonito` | peixe (piraputanga, com as quatro barbatanas) | PA | `belem` | Mercado de Ferro do Ver-o-Peso (4 torres) |
| MT | `transpantaneira` | jacaré visto de cima | AM | `manaus` | cúpula do Teatro Amazonas |
| BA | `porto_seguro` | caravela de Cabral | AP | `macapa` | Fortaleza de São José (4 baluartes) |
| RS | `cuia_gaucha` | cuia (à mão, de antes) | RR | `monte_roraima` | tepui |
| | | | RO | `porto_velho` | locomotiva da Madeira-Mamoré |
| | | | AC | `rio_branco` | **gameleira** (no lugar da folha de seringueira) |
| | | | TO | `palmas` | sol com raios (o da bandeira) |

**Troca: o caju do RN virou o cajueiro de Pirangi** (o maior cajueiro do mundo, na mesma pista). O caju — castanha
em cima da fruta — numa linha só saiu bolota, sino ou coelho nas três tentativas: a castanha encostada na fruta vira
uma cintura, e cintura é o que todos esses desenhos têm. A árvore (copa larga de tufos, galhos que descem até o chão em
arcos) se lê de primeira e é o cartão-postal de Pirangi do mesmo jeito.

**Troca: a folha de seringueira do AC virou a gameleira** (a figueira que dá nome à pista, `Gameleira de Rio Branco`, e
é o marco dela). A folha se desenhava — três folíolos, entalhes, pecíolo —, mas não cabia no orçamento do cenário (logo
abaixo): pontas finas e lados quase retos não têm curva longa, e só as pontas e os entalhes afastam a mata; arredondando
tudo o que dava, ficava em 920–960 triângulos por segmento, acima do teto de 900. A copa de tufos da gameleira tem curva
em volta inteira.

### Desenhos em cartum (em andamento, 06/10/2026)

**O dono achou os 27 desenhos mal feitos** ("tipo do Cristo Redentor, pode ser cartoonado, mas visualmente bonito, e o
mesmo para os demais"). A causa: o alvo de cada um é um **polígono** com as quinas arredondadas (`scripts/track-shapes.ts`),
então tudo sai reto e anguloso — o Cristo era um boneco de palito. O redesenho troca o alvo por **silhuetas de cartum em
traço contínuo** (Bézier e arcos), como um ícone ilustrado, e a pista passa a seguir a curvatura do traço inteiro.

**Estado (onda K, K5):** o trecho `bend` (curva constante, sem rampa) existe no DSL (`applyOp` em `builder.ts`; atalho `bd` em `tracks.ts`) e o `encodeCurves` de `scripts/shape-to-track.ts` agora o emite: segmentos de curva parecida (a no máximo max(0,05; 6% × |c|) da do começo do trecho) viram um `bend` com a integral preservada, e a escala do `fitArt` é direta (o pico dos `bend` vale `cmax`). `artHills` e `formatOps` aceitam `bend`. O Cristo está aplicado em `copacabana` (`--apply`, sigma 6, `ART_CFG` vazio): 314 `bd`, curva de pico 2,92, erro médio 0,80 e máx. 2,12 (em 100), desvio de fechamento 2,7 (com `cv`: 1,49 / 5,22 / 46,4), menor vão 5,9 e 433 triângulos de cenário por segmento. Testes: `tests/track.test.ts` ("trecho bend", "o traço vira bend", "fidelidade ao desenho", com a lista `APPLIED_ART`). O Cristo em `landmark-sight`: 3,7 s e 4,0 s à vista. Os outros 26 desenhos usam o mesmo caminho e entram em `APPLIED_ART` no `--apply` de cada um (onda O).
`Pen` com `L`/`C`/`Q`/`arc`, `sym` para desenho simétrico; só o **Cristo** desenhado e aprovado no visual), o modo de
traço em curva em `scripts/shape-to-track.ts` (`fitArt`, `solveArt`, `artHills`, `ART_CFG`; a CLI usa o desenho de
`ART` quando existe) e `scripts/art-sheet.ts` (folha dos alvos). **Nenhuma pista mudou ainda.**

**O que o primeiro teste mostrou (e o próximo passo técnico):** codificar a curvatura do traço em `cv` não funciona
bem — cada `cv` entra e sai da curva a partir do zero (rampas quadráticas de 1/4 do comprimento, `builder.ts`), então
um trecho de curva constante vira uma série de pulsos: o Cristo saiu com desvio de fechamento 68 e erro médio 2,4 (em
100), e dirigindo a curva "ondularia". A solução planejada:

1. **Trecho novo no DSL: `bend` — curva constante, sem rampa.** Em `src/core/types.ts`:
   `| { op: 'bend'; length: number; curve: number; hill?: number }`; em `src/core/track/builder.ts`, no `applyOp`:
   `case 'bend': addRoad(b, 0, op.length, 0, op.curve, op.hill ?? 0); break;`; em `src/core/track/tracks.ts`, o atalho
   `const bd = (length, curve, hill?) => ({ op: 'bend', length, curve, hill })`; em `scripts/shape-to-track.ts`,
   `formatOps` com `bd(...)`. Nenhuma pista existente muda (o `sim-golden` não muda até alguma pista usar `bend`).
   Testes antes: um `bend` de comprimento L e curva c gera L segmentos de curva exatamente c; o tutorial e o resto
   que fazem `switch` em `op.op` continuam compilando.
2. **Codificar em `bend`** (`encodeCurves` em `scripts/shape-to-track.ts`): agrupar segmentos seguidos cuja curva fica
   a no máximo `max(0,05; 6% × |c|)` da do começo do grupo, e emitir `bd(L, média)` preservando a integral do grupo
   com o resto passado para o seguinte (o rumo nas emendas fica exato). Sem rampa não há pico acima da média: a escala
   é direta (`g = cmax ÷ maior giro`). Curva arredondada para 0,00 vira reta (`st`).
3. **Desenhar os outros 26** em `scripts/track-art.ts` (e a cuia, que também é redesenhada). Estilo: ícone de cartum —
   formas cheias e redondas, proporções de brinquedo, traço liso, simetria quando o objeto é simétrico, nada de
   pescoço fino demais (dois trechos da pista não podem encostar no minimapa). A largada (`start`) num trecho RETO que
   SOBE na vertical, com folga de uns 3% do perímetro (o box são 40 segmentos retos). Notas por desenho:
   - **RS cuia**: bojo redondo, cintura, boca com lábio largo e a **bomba** saindo em diagonal (é ela que diz
     "chimarrão"); pé reto para a largada.
   - **RJ Cristo** (feito): cabeça redonda, braços retos com mãos redondas, mangas caindo em curva até a cintura, manto
     abrindo até o pedestal; largada na lateral esquerda do pedestal.
   - **SP MASP**: a caixa larga suspensa nos dois pórticos (Π), vão livre embaixo; cantos levemente arredondados.
   - **MG Pampulha**: perfil de lado — a abóbada parabólica grande e três menores em onda, a torre em trapézio
     invertido ligada pela marquise; base reta.
   - **ES Convento da Penha**: morro em domo com o paredão íngreme de um lado e o convento (caixa + torre com ponta)
     no topo.
   - **SC Hercílio Luz**: tabuleiro, duas torres com capitel, a corrente que sobe às torres e desce até tocar o
     tabuleiro no meio (o "M" da ponte), ancoragens nas pontas.
   - **DF avião**: avião de cartum visto de cima, nariz redondo, asas enflechadas com pontas redondas, cauda; largada
     na lateral reta da fuselagem.
   - **PR araucária**: tronco reto e a copa em taça (candelabro) com tufos redondos no alto e a borda levemente
     côncava; um par de galhos menores mais embaixo.
   - **GO gota**: gota d'água elegante (ponta curva no alto, fundo redondo).
   - **MS peixe**: corpo oval, cauda bifurcada, barbatana dorsal e anal, boca; peixe de desenho animado.
   - **MT jacaré**: visto de cima, de pé (cabeça para cima): focinho comprido e redondo, quatro patas curtas dobradas,
     cauda que afina em S; largada no flanco reto do corpo.
   - **BA caravela**: casco em sorriso com castelo de popa, três mastros com velas bojudas (redondas) ligadas por
     mastros finos, flâmula no alto; largada na popa.
   - **SE caranguejo**: corpo oval largo, olhos nas hastes, duas garras grandes abertas para cima, três patas de cada
     lado na parte de baixo; flanco reto entre a garra e as patas para a largada.
   - **AL jangada**: vela triangular bojuda no mastro, jangada de toras embaixo, flâmula; largada no mastro.
   - **PE sombrinha de frevo**: cúpula com a borda em 6 recortes redondos, ponteira no alto, cabo com o gancho em J.
   - **PB balão junino**: balão de topo redondo afinando até a boca, com a bucha embaixo.
   - **RN**: o **caju** (fruta em sino + castanha em feijão, com uma folha no talo) — tentar de novo em curva, que é
     onde o polígono falhou; se não ler, fica o cajueiro (copa larga e baixa de tufos).
   - **CE lua crescente**: crescente gordo com pontas finas e curvas.
   - **PI capivara**: de perfil, barril redondo, focinho largo e rombudo, orelha pequena, quatro patinhas curtas;
     largada na traseira.
   - **MA boi**: cabeça de frente — chifres largos em curva para cima, orelhas para os lados, focinho arredondado.
   - **PA Ver-o-Peso**: o Mercado de Ferro com as quatro torres de ponta cônica e o corpo entre elas.
   - **AM Teatro Amazonas**: o prédio com a cúpula em sino, o tambor e a lanterna no alto, frontão no meio.
   - **AP Fortaleza de São José**: planta estrelada — quadrado com quatro baluartes em ponta de flecha, regular.
   - **RR tepui**: mesa de topo achatado (borda levemente irregular), paredões, talude largo na base.
   - **RO locomotiva**: maria-fumaça de perfil — cabine, caldeira redonda, chaminé em funil (com uma nuvem de fumaça
     de cartum), limpa-trilhos, três rodas grandes em arco.
   - **AC gameleira**: copa de nuvem com tufos redondos, tronco grosso com raízes abertas.
   - **TO sol**: disco com 12 raios de ponta arredondada (um raio com o lado vertical para a largada).
4. **Para cada pista**: `npx tsx scripts/shape-to-track.ts <pista>` e conferir no relatório — erro médio ≤ 0,8 e
   máximo ≤ 2,5 (em 100) contra o desenho, cruzamentos 0, menor vão ≥ 1,2, cenário ≤ 900 triângulos por segmento,
   índice técnico dentro da faixa da copa (o `index` de `SHAPES`/`ART_CFG`). Folha dos alvos com `scripts/art-sheet.ts`
   e a dos contornos com `--sheet`; só depois `--apply`.
5. **Dificuldade**: medir a velocidade média da IA (`npm run balance -- corrida profissional 11|12|13 <pista>`) e
   acertar o `cmax` em `ART_CFG` para voltar à dificuldade **de antes dos desenhos** (coluna "Medido" antes da onda
   G desta seção; Caldas Novas e Canoa Quebrada ficaram fáceis demais com os desenhos de polígono: 3,9 → 1,9 e
   4,0 → 2,1).
6. **Testes**: trocar os blocos por estado de `tests/track.test.ts` por um teste de **fidelidade ao desenho** (o
   contorno do minimapa fica a no máximo X do traço de `ART`, normalizado como em `report`) mais as propriedades
   genéricas (sem cruzar, folga, proporção, |curva| ≤ 6) — escrever antes do `--apply` e ver falhar no traçado de
   hoje. Rodar `tests/landmarks*.test.ts` (o traçado muda onde os marcos ficam: a Pampulha já caiu abaixo da meta uma
   vez) e `tests/scenery-forma.test.ts`. `sim-golden`: copacabana e sampa_noite estão lá (atualizar `EXPECTED`).
7. **Mostrar ao dono** a folha dos 27 (alvo e contorno) antes de dar por pronto: o critério é ele achar bonito.

### Como uma pista vira desenho

- **O desenho** está em `scripts/track-shapes.ts`: um polígono horário por pista, num quadro de ~100, começando no pé
  do lado da largada (que sobe na vertical: o minimapa começa apontando para cima), com o raio de cada quina (padrão
  `radius`, ou um terceiro número no ponto). Ajudantes: `bulge`/`bumps` (arcos estufados: tufos, rodas, vãos),
  `circle`, `leaf`, `sun`.
- **A ferramenta** `scripts/shape-to-track.ts` gera os `ops` (o cabeçalho dela explica a conta): cada quina vira um
  `cv` cujo comprimento é raio × ângulo, a curva sai do ângulo ÷ esse comprimento, e o deslocamento que a entrada e a
  saída suaves do `cv` causam é medido no próprio perfil do builder e descontado das retas vizinhas — o contorno cai
  em cima do polígono. As curvas vão a 2 casas, com o erro de arredondamento passado para a quina seguinte (com 1
  casa o rumo entortava e o desenho não fechava). O `pit(40)` abre a volta, no lado da largada (`startAt`).
- `npx tsx scripts/shape-to-track.ts <pista…>` mostra o resultado (ASCII, cruzamentos, folga mínima, índice);
  `--svg x.svg` a sobreposição contorno × alvo; `--current` mede a pista como está em `tracks.ts`; `--apply` grava a
  linha de `ops` da pista. **Mexer no desenho e regenerar, nunca nos números à mão.**
- Comprimento e voltas: cada pista ficou com o mesmo número de segmentos de antes (comprimento da volta igual, não só
  dentro de ±10%); voltas, cenário, período e dificuldade não mudaram.
- **Orçamento do cenário** (`tests/scenery-forma.test.ts`: ≤ 900 triângulos por segmento): do lado de dentro de curva
  longa — 18 de 80 segmentos acima de 1,2 (`innerCurve`, `src/render/scenery/layout.ts`) — a mata não nasce perto da
  pista. Os traçados de antes tinham isso em 20–40% da volta; desenho de quinas curtas e retas longas tem quase 0%, e na
  mata (tropical) a pista fica com todas as árvores: Bonito foi a 1.010, Rio Branco a 965, Porto Velho a 915 e Macapá a
  900. Consertado no desenho: quinas arredondadas (`round` 2,4 no peixe, que ganhou a adiposa e a pélvica; 2 na
  fortaleza; 1,25 na locomotiva, com o beiral e o vão entre as rodas mais largos) e a troca da folha pela gameleira. Com
  `cmax` refeito pela velocidade da IA, ficaram em 874 / 878 / 862 / 864. Em todas as 26 o custo subiu um pouco (menos
  curva longa); a ferramenta mostra o custo no relatório e avisa acima do teto.

### Desenho × dificuldade (o que se aprendeu)

Escalar todas as curvas não muda o desenho (o minimapa normaliza a soma), então a força da curva é livre: é a
dificuldade. A primeira tentativa acertou só o **índice técnico** de antes com as curvas — e a IA ficou muito mais
lenta (Sampa de medido 3,0 para 4,3): o índice não vê que muitas quinas curtas de curva 6 fazem a IA frear e
reacelerar sem parar. Então:

1. `cmax` (a curva da quina mais forte; as outras saem na proporção ângulo ÷ raio) foi escolhido em cada pista pela
   **velocidade média da IA** medida na pista de antes (a mesma régua do "medido" em "Balanceamento");
2. o resto do índice técnico de antes virou **morro** (`hl` nas retas e desnível dentro das curvas longas): altura
   não muda o contorno nem a IA (o núcleo não lê `y`), e o índice fica onde estava, na faixa da copa.

Antes × depois — corrida inteira (`scripts/balance.ts` "corrida": 20 carros, piloto médio, sem assistências),
profissional, sementes 11–13, medido nas duas versões com o mesmo código. Medido limitado a 1–5 (Palmas fica acima
de 5 nas duas: v média 0,697 → 0,687):

| UF | Pista | Dif. | Índice antes → depois (curva + morro) | Medido | Corrida (pro) | Batidas no cenário | `cmax` · `round` |
|---|---|---|---|---|---|---|---|
| RJ | `copacabana` | 1 | 0,65 (0,0 + 0,6) → 0,67 (0,0 + 0,7) | 1,0 → 1,0 | 3:21 → 3:21 | 3,3 → 1,7 | 3 |
| SP | `sampa_noite` | 3 | 6,13 (6,1 + 0,0) → 6,12 (1,7 + 4,4) | 3,0 → 3,0 | 5:01 → 4:59 | 25,7 → 17,0 | 5,7 |
| MG | `pampulha` | 1 | 1,17 (1,2 + 0,0) → 1,17 (0,2 + 1,0) | 1,0 → 1,0 | 3:48 → 3:48 | 4,3 → 4,3 | 3,4 |
| ES | `convento_penha` | 2 | 3,96 (2,5 + 1,5) → 3,96 (0,9 + 3,0) | 2,1 → 2,2 | 3:45 → 3:47 | 1,7 → 0,0 | 4,7 |
| SC | `floripa` | 2 | 3,00 (1,5 + 1,5) → 3,01 (1,3 + 1,7) | 1,5 → 2,0 | 3:46 → 3:50 | 8,7 → 7,0 | 4,4 |
| DF | `brasilia` | 3 | 7,74 (7,6 + 0,2) → 7,73 (2,0 + 5,7) | 3,0 → 3,4 | 4:18 → 4:21 | 3,7 → 2,3 | 5,9 |
| PR | `curitiba` | 3 | 5,63 (5,3 + 0,3) → 5,63 (2,4 + 3,2) | 2,6 → 3,0 | 5:15 → 5:21 | 3,0 → 2,0 | 5,1 |
| GO | `caldas_novas` | 3 | 7,21 (6,7 + 0,5) → 7,20 (0,3 + 6,9) | **3,9 → 1,9** | 4:07 → 3:44 | 6,0 → 2,7 | 6 |
| MS | `bonito` | 3 | 7,05 (6,5 + 0,5) → 7,04 (4,2 + 2,8) | 2,6 → 2,7 | 3:57 → 4:00 | 3,0 → 2,0 | 4,5 · 2,4 |
| MT | `transpantaneira` | 1 | 0,30 (0,0 + 0,3) → 0,30 (0,0 + 0,3) | 1,1 → 1,4 | 3:59 → 4:00 | 0,0 → 1,3 | 3 |
| BA | `porto_seguro` | 2 | 5,02 (4,3 + 0,7) → 5,01 (1,9 + 3,1) | 2,4 → 2,5 | 3:38 → 3:39 | 5,0 → 4,0 | 4,9 |
| SE | `aracaju` | 3 | 6,84 (6,6 + 0,3) → 6,81 (3,3 + 3,5) | 2,9 → 3,4 | 4:48 → 4:52 | 10,0 → 10,0 | 5,3 |
| AL | `maceio` | 4 | 10,63 (9,1 + 1,6) → 10,62 (3,9 + 6,7) | 4,7 → 4,6 | 5:11 → 5:07 | **5,7 → 32,0** | 6 · 1,25 |
| PE | `olinda` | 4 | 9,97 (8,8 + 1,2) → 9,95 (4,0 + 5,9) | 3,9 → 4,4 | 3:40 → 3:46 | 8,7 → 4,7 | 5,8 |
| PB | `campina_grande` | 4 | 10,02 (10,0 + 0,0) → 10,01 (4,8 + 5,2) | 4,7 → 4,7 | 5:23 → 5:22 | 2,0 → 3,0 | 6 |
| RN | `cajueiro_pirangi` | 4 | 10,42 (9,7 + 0,7) → 10,40 (7,1 + 3,3) | 4,4 → 4,4 | 4:20 → 4:19 | 13,7 → 5,7 | 5,1 |
| CE | `canoa_quebrada` | 4 | 10,03 (6,4 + 3,6) → 9,98 (0,5 + 9,5) | **4,0 → 2,1** | 4:04 → 3:43 | 0,7 → 0,0 | 6 · 1,25 |
| PI | `serra_capivara` | 4 | 10,29 (9,4 + 0,8) → 10,28 (4,5 + 5,8) | 4,4 → 4,7 | 4:04 → 4:08 | 2,3 → 1,0 | 6 · 1,25 |
| MA | `sao_luis` | 4 | 10,24 (9,5 + 0,7) → 10,22 (4,0 + 6,3) | 4,4 → 4,7 | 4:25 → 4:32 | 11,0 → 10,7 | 6 · 1,25 |
| PA | `belem` | 4 | 10,62 (10,6 + 0,0) → 10,61 (5,1 + 5,5) | 4,9 → 4,8 | 4:02 → 3:59 | 4,7 → 3,7 | 6 · 2 |
| AM | `manaus` | 4 | 10,64 (9,8 + 0,8) → 10,63 (3,8 + 6,9) | 4,1 → 4,4 | 4:27 → 4:34 | 6,3 → 4,3 | 6 · 1,75 |
| AP | `macapa` | 3 | 8,75 (8,7 + 0,0) → 8,74 (4,8 + 4,0) | 4,2 → 4,2 | 3:41 → 3:41 | 5,3 → 7,3 | 5,4 · 2 |
| RR | `monte_roraima` | 5 | 13,01 (12,1 + 0,9) → 12,99 (4,9 + 8,1) | 5,0 → 4,6 | 4:40 → 4:28 | **8,0 → 17,3** | 6 · 1,25 |
| RO | `porto_velho` | 4 | 11,33 (11,0 + 0,3) → 11,33 (6,0 + 5,3) | 4,8 → 4,9 | 4:22 → 4:20 | 2,3 → 0,3 | 5,8 · 1,25 |
| AC | `rio_branco` | 4 | 11,45 (10,8 + 0,7) → 11,42 (8,0 + 3,4) | 4,3 → 4,4 | 4:26 → 4:23 | **1,0 → 16,0** | 5 · 1,75 |
| TO | `palmas` | 5 | 13,84 (13,8 + 0,0) → 13,77 (7,7 + 6,1) | 5,0 → 5,0 | 5:37 → 5:34 | **29,3 → 55,7** | 6 · 1,25 |

**O que não fechou** (decisão do dono, se incomodar):

- **Duas ficaram mais fáceis do que eram**, com `cmax` já no teto 6: a gota (`caldas_novas`, 3,9 → 1,9) e a lua
  (`canoa_quebrada`, 4,0 → 2,1). São desenhos de curva longa e suave com uma ou duas pontas: só as pontas freiam. Arredondar mais (mais trecho em curva) não cabe no desenho (as retas acabam). O
  índice continua na faixa (o morro completa), mas a IA anda mais solta. Saída possível: chicanes curtas (pares
  esquerda-direita) que quase não aparecem no minimapa — não feito, muda o caráter da pista. A ordem dentro da copa
  sente isso: em GO a Caldas Novas, que fecha a copa, passa a ser a mais leve das três (Pirenópolis 2,2, Veadeiros
  2,4); no CE a Canoa (2ª) fica abaixo da Jericoacoara (1ª, 3,7). A ordem não foi mexida.
- **Batidas no cenário (corrigidas na K5).** A causa eram as placas de curva do `decorate` (`builder.ts`): 3 placas sólidas no começo de toda curva com |curva| ≥ 3, e os desenhos de polígono encheram quatro pistas de quinas curtas de curva forte. Regra nova: só ganha placa a curva forte com ≥ 12 segmentos acima de 3 (`SIGN_MIN_STRONG`, local no `builder.ts`). Batidas por corrida (média das sementes 11–13, profissional): Maceió 32,0 → 1,0, Palmas 55,7 → 28,7, Rio Branco 16,0 → 3,0, Monte Roraima 17,3 → 6,3; velocidade média das 26 pistas de polígono dentro de ±0,005. Travado por `tests/batidas-cenario.test.ts`. 23 pistas perdem as placas das quinas curtas (sampa_noite fica sem nenhuma).
  Rio Branco (1,0 → 16,0) — muitas quinas seguidas perto do paredão ou da mata (a jangada, o sol e os tufos da copa são
  quinas por natureza). A corrida dura o mesmo.
- **Marcos**: o enquadramento (`landmarkSight`, ≥ 2,5 s) depende de onde caem as praças e mirantes, que dependem do
  traçado. Pampulha, Curitiba, Brasília e Bonito precisaram mudar o ponto da largada no 1º lado (`startAt`) para o
  marco voltar a ficar à vista; o Convento da Penha fica nos mesmos 2,50 s de antes.
- **Aviso de combustível no tick da linha**: com `cmax` 5,3 na fortaleza, um carro de pé no fundo recebia o aviso
  ("entre no box") exatamente no tick em que cruzava a linha para a última volta — `tests/fuel.test.ts` pegou. O
  núcleo confere `pitStillAhead` com a volta de antes do tick, e a contagem da volta vem depois (`physics.ts` ×
  `positions.ts`). Ficou 5,4 (mesma velocidade da IA); o defeito do núcleo é de antes e vale para qualquer pista.
- **Recordes e fantasmas** de 24 pistas (a copacabana, que mudou de traçado, e 23 que perderam placas) ficam como "versão anterior": a impressão do traçado entra na versão do conteúdo (`content-version.ts`, `trackLayout`), e a placa é sprite sólido. A `onlineFingerprint` também muda. `caldas_novas`, `canoa_quebrada` e as outras 83 pistas não mudam. No `sim-golden` mudam só `solo-sem-assistencias` e `versus-cambio-manual` (sampa_noite sem placas).
- As tabelas de "Balanceamento" abaixo são de 03/10, **antes dos desenhos**; para as 26, vale a tabela acima.

### Os testes

`tests/track.test.ts`, "Pistas com desenho": um teste genérico (contorno sem cruzar nem encostar em si — folga > 1,2
—, proporção entre 0,4 e 2,5, |curva| ≤ 6) e um bloco por estado que reconhece o desenho por propriedades do
contorno, nunca por coordenada: largura por faixa de altura, corcovas e cavas de um perfil, simetria, pontas nos
cantos (o Cristo: faixa dos braços a mais larga e de ponta a ponta, cabeça estreita no alto, pedestal; o MASP: mais
largo que alto, teto reto, vão livre entre os pilares; o sol: ≥ 10 raios no perfil polar). Escritos antes do
redesenho e vistos falhar no traçado antigo: 88 dos 129 testes do arquivo falham com o `tracks.ts` de antes, e todo
bloco de estado tem pelo menos dois testes vermelhos ali. Três blocos foram reescritos no meio (e vistos falhar de novo
no traçado antigo): a ponte (o tabuleiro reto entre as torres, ancoragens nas pontas), o RN (caju → cajueiro) e o AC
(folha → gameleira: domo, tufos no perfil polar da copa, tronco grosso, raízes; os três vermelhos também no traçado da
folha). O orçamento do cenário já tinha teste (`tests/scenery-forma.test.ts`), que pegou Bonito, Porto Velho e Rio
Branco.

## Migração (save de antes da onda G)

`LEGACY_CUP_IDS` (`cups.ts`): `brasil` → `br_rj`. Na leitura do save (`src/game/save.ts`, `career-save.ts`):

| O que | Vira |
|---|---|
| `cupsCompleted: ['brasil', …]` | `br_rj` (e o carimbo do RJ); e os EUA, que a Copa Brasil abria, continuam abertos (`save.cupsUnlocked`, `legacyCupOpens`) |
| conquista `COPA_BRASIL` | `COPA_BR_RJ` |
| carreira com `cupId: 'brasil'` | carreira na `br_rj`, com dinheiro, garagem e estatísticas; a copa em andamento e a contagem de tentativas recomeçam (as corridas eram outras) |
| relatório da garagem de uma corrida da `brasil` | sai (a garagem mostraria a chave `core.cup.brasil`) |
| campeonato normal em andamento na `brasil` | some do save (sem erro; o menu deixa de oferecer "Continuar") |
| campeonato normal ou carreira no meio de uma copa do Mundial | continua da mesma corrida |
| recordes, fantasmas, estatísticas | ficam (são por id de pista, e nenhum id mudou); nas 14 pistas de antes com praça ou mirante a colisão mudou — as 6 de cidade (praças) e, desde a onda H, 8 de litoral (mirantes: Baía de Tóquio, Mônaco, Boa Esperança, Great Ocean, Atlântico, Tromsø, Amalfi, Santorini): recorde com a marca "versão anterior", fantasma fora como rival |
| quem já tinha vencido copas do Mundial | elas continuam abertas (`isCupUnlocked`: copa concluída fica aberta), e a seguinte também; a Expedição começa em SP |

Carreira antiga numa copa do Mundial (eua, japao…) continua nela: o nível da IA e o prêmio passam a ser os da posição
nova da copa (mais altos), e a carreira termina no Mediterrâneo sem passar pelo Brasil. Nos EUA isso leva o piloto médio
de 1,75 para 9,38 de média (`docs/SAVE.md`, "Save de antes da onda G", com a tabela inteira e os dois defeitos achados
em 03/10). Teste: `tests/migration-brasil.test.ts` (saves gravados como eram antes).

## Catálogo

Só cenários que já existem (`tropical, desert, city_night, alpine, coast, savanna`) × `day/dusk/night`. Os nomes usam
lugares reais só como referência geográfica, sem marca nenhuma; o nome em EN traduz a descrição, não o lugar.

Na cidade (`city_night`) os dois lados da pista são um paredão de prédios e torres na beira (a receita do `builder`), e
no litoral (`coast`) a receita também põe prédio e torre dos dois lados, mais espaçados. Nas pistas desses dois biomas
com marcos turísticos de perto ou de longe o núcleo abre **praças** (cidade) e **mirantes** (litoral) — trechos sem
prédio, torre nem outdoor onde a linha de visada de quem chega cruza a beira da pista, do lado do marco e, numa
aproximação em curva, também do outro lado (de ~50 a ~160 segmentos) — e o renderizador põe cada marco de perto/longe
no seu, para ele ficar à vista o mínimo de 2,5 s (`core/track/plazas.ts` e `sightline.ts`; `docs/VISUAL.md`, "Marcos
turísticos"). No litoral o lado é o do marco: o mar (à direita) ou a terra. O primeiro de cada marco fica a ≤ 150
segmentos (o mais importante) ou ≤ 300 do **fim da largada** — arquibancadas, trecho sem cenário e box, segmento 40 em
todas as pistas de hoje (`core/track/startzone.ts`).

| Copa | Pista (id) | Nome | Nome (EN) | Cenário | Período | Voltas | Dif. | Segm. | Índice |
|---|---|---|---|---|---|---|---|---|---|
| RJ | `copacabana` | Orla de Copacabana | Copacabana Beachfront | coast | dia | 3 | 1 | 1800 | 0,7 |
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
| RS | `aparados_da_serra` | Aparados da Serra | Aparados Canyons | alpine | dia | 3 | 3 | 1860 | 7,5 |
| RS | `cuia_gaucha` | Cuia da Serra Gaúcha | Gaúcha Gourd Circuit | alpine | entardecer | 3 | 3 | 1970 | 7,0 |
| DF | `lago_paranoa` | Lago Paranoá | Paranoá Lake | coast | dia | 3 | 2 | 1770 | 4,5 |
| DF | `brasilia` | Eixo Monumental | Monumental Axis | city_night | entardecer | 3 | 3 | 2120 | 7,7 |
| DF | `torre_de_tv` | Torre de TV | TV Tower | city_night | noite | 4 | 3 | 1720 | 7,0 |
| GO | `pirenopolis` | Ruas de Pirenópolis | Pirenópolis Streets | tropical | entardecer | 3 | 2 | 1840 | 4,9 |
| GO | `chapada_veadeiros` | Chapada dos Veadeiros | Veadeiros Plateau | savanna | dia | 3 | 3 | 1940 | 7,8 |
| GO | `caldas_novas` | Águas de Caldas Novas | Caldas Novas Springs | savanna | dia | 3 | 3 | 1990 | 7,2 |
| MS | `bonito` | Rios de Bonito | Bonito Rivers | tropical | dia | 3 | 3 | 2020 | 7,0 |
| MS | `campo_grande` | Avenidas de Campo Grande | Campo Grande Avenues | city_night | entardecer | 3 | 3 | 1820 | 7,0 |
| MS | `estrada_parque` | Estrada Parque do Pantanal | Pantanal Park Road | savanna | entardecer | 3 | 3 | 1990 | 7,2 |
| MT | `transpantaneira` | Transpantaneira | Transpantaneira | savanna | entardecer | 3 | 1 | 2130 | 0,3 |
| MT | `chapada_guimaraes` | Chapada dos Guimarães | Guimarães Plateau | savanna | dia | 3 | 4 | 1900 | 11,0 |
| MT | `cuiaba` | Centro Geodésico de Cuiabá | Cuiabá Geodesic Center | city_night | noite | 4 | 4 | 1770 | 10,7 |
| BA | `porto_seguro` | Costa de Porto Seguro | Porto Seguro Coast | coast | dia | 3 | 2 | 1870 | 5,0 |
| BA | `salvador` | Orla de Salvador | Salvador Waterfront | coast | entardecer | 3 | 3 | 1980 | 8,9 |
| BA | `chapada_diamantina` | Chapada Diamantina | Diamantina Plateau | savanna | dia | 3 | 4 | 2040 | 9,9 |
| SE | `aracaju` | Orla de Atalaia | Atalaia Beach | coast | noite | 4 | 3 | 1790 | 6,8 |
| SE | `sao_cristovao` | Praça de São Cristóvão | São Cristóvão Square | tropical | entardecer | 3 | 3 | 1850 | 7,4 |
| SE | `xingo` | Cânions do Xingó | Xingó Canyons | desert | dia | 3 | 4 | 1890 | 10,0 |
| AL | `maragogi` | Piscinas de Maragogi | Maragogi Reefs | coast | dia | 3 | 2 | 1930 | 3,3 |
| AL | `foz_sao_francisco` | Foz do São Francisco | São Francisco River Mouth | desert | entardecer | 3 | 4 | 1950 | 11,0 |
| AL | `maceio` | Orla de Maceió | Maceió Waterfront | coast | noite | 4 | 4 | 1760 | 10,6 |
| PE | `recife_antigo` | Recife Antigo | Old Recife | city_night | noite | 3 | 3 | 2070 | 7,6 |
| PE | `olinda` | Ladeiras de Olinda | Olinda Hills | coast | dia | 3 | 4 | 1770 | 10,0 |
| PE | `noronha` | Fernando de Noronha | Fernando de Noronha | coast | entardecer | 3 | 4 | 1970 | 10,0 |
| PB | `joao_pessoa` | Ponta do Seixas | Easternmost Point | coast | entardecer | 3 | 3 | 1930 | 8,2 |
| PB | `pedra_da_boca` | Pedra da Boca | Mouth Rock | desert | dia | 3 | 4 | 1960 | 10,3 |
| PB | `campina_grande` | Parque do Povo | People's Park | city_night | noite | 4 | 4 | 1820 | 10,0 |
| RN | `natal` | Dunas de Genipabu | Genipabu Dunes | coast | dia | 3 | 3 | 1910 | 8,0 |
| RN | `ponta_negra` | Morro do Careca | Bald Hill Beach | coast | entardecer | 3 | 4 | 1950 | 10,2 |
| RN | `cajueiro_pirangi` | Cajueiro de Pirangi | Pirangi Cashew Tree | tropical | dia | 3 | 4 | 2030 | 10,4 |
| CE | `jericoacoara` | Jericoacoara | Jericoacoara | coast | entardecer | 3 | 4 | 1890 | 10,1 |
| CE | `canoa_quebrada` | Falésias de Canoa Quebrada | Canoa Quebrada Cliffs | desert | dia | 3 | 4 | 1950 | 10,0 |
| CE | `fortaleza_beira_mar` | Beira-Mar de Fortaleza | Fortaleza Seafront | city_night | noite | 4 | 4 | 1680 | 9,9 |
| PI | `delta_parnaiba` | Delta do Parnaíba | Parnaíba Delta | tropical | dia | 3 | 4 | 1930 | 10,0 |
| PI | `sete_cidades` | Pedras de Sete Cidades | Seven Cities Rocks | savanna | entardecer | 3 | 4 | 2090 | 10,2 |
| PI | `serra_capivara` | Serra da Capivara | Capivara Range | desert | entardecer | 3 | 4 | 1890 | 10,3 |
| MA | `lencois` | Lençóis Maranhenses | Lençóis Maranhenses | desert | entardecer | 3 | 4 | 1700 | 10,5 |
| MA | `chapada_das_mesas` | Chapada das Mesas | Mesas Plateau | savanna | dia | 3 | 4 | 1720 | 10,3 |
| MA | `sao_luis` | Casarões de São Luís | São Luís Tiles | city_night | noite | 3 | 4 | 2070 | 10,2 |
| PA | `alter_do_chao` | Praias de Alter do Chão | Alter do Chão Beaches | tropical | dia | 3 | 4 | 1920 | 10,4 |
| PA | `belem` | Ver-o-Peso | Belém Docks | coast | entardecer | 3 | 4 | 1800 | 10,6 |
| PA | `marajo` | Campos do Marajó | Marajó Fields | savanna | entardecer | 3 | 5 | 1940 | 12,7 |
| AM | `manaus` | Encontro das Águas | Meeting of Waters | coast | dia | 3 | 4 | 2110 | 10,6 |
| AM | `ponte_rio_negro` | Ponte do Rio Negro | Rio Negro Bridge | city_night | noite | 3 | 4 | 1990 | 10,8 |
| AM | `parintins` | Bumbódromo de Parintins | Parintins Arena | tropical | entardecer | 3 | 5 | 1930 | 12,9 |
| AP | `macapa` | Marco Zero do Equador | Equator Line | tropical | dia | 3 | 3 | 1750 | 8,7 |
| AP | `pororoca_araguari` | Pororoca do Araguari | Araguari Tidal Bore | tropical | entardecer | 3 | 5 | 1900 | 12,8 |
| AP | `serra_do_navio` | Serra do Navio | Navio Range | tropical | dia | 3 | 5 | 1800 | 13,0 |
| RR | `boa_vista` | Avenidas de Boa Vista | Boa Vista Avenues | city_night | noite | 4 | 4 | 1620 | 11,0 |
| RR | `monte_roraima` | Monte Roraima | Mount Roraima | savanna | dia | 3 | 5 | 2040 | 13,0 |
| RR | `lago_caracarana` | Lago Caracaranã | Caracaranã Lake | savanna | entardecer | 3 | 5 | 1880 | 13,3 |
| RO | `porto_velho` | Madeira-Mamoré | Madeira-Mamoré Railway | tropical | entardecer | 3 | 4 | 1960 | 11,3 |
| RO | `vale_guapore` | Vale do Guaporé | Guaporé Valley | savanna | entardecer | 3 | 5 | 1920 | 13,6 |
| RO | `forte_principe` | Forte Príncipe da Beira | Prince of Beira Fort | tropical | dia | 3 | 5 | 1820 | 13,4 |
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

## Balanceamento

### As 109 pistas, corrida inteira (03/10/2026)

⚠️ Medição de antes das pistas com desenho (04/10): nas 26 redesenhadas os números de hoje (profissional) estão em
"Pistas com desenho", acima; nas outras 83 nada mudou.

Medido com `npm run balance -- corrida <dificuldade> <semente>` — o modo novo do `scripts/balance.ts`: a corrida
inteira (as voltas da pista), 20 carros, sem assistências, com o piloto médio da calibragem (`PROXY_SKILL` 0,97,
Falcão de fábrica) no lugar do carro parado. Sete rodadas × 109 pistas: profissional nas sementes 11, 12 e 13, amador
e campeão nas 11 e 12. E o modo de sempre (`npm run balance -- 150 profissional 11` e `150 amador 12`): a IA completa
a volta nas 109, grama ≤ 0,6% no profissional (0,7% no amador), melhor volta no profissional entre 1:07 e 1:37. Na
corrida inteira a grama chega a 1,4% (Roma): a faixa do box conta como grama, e as de 4 voltas param mais no box.

**Duração da corrida — o alvo saiu das 32 antigas.** No profissional elas vão de 3:18 (Serra do Mar) a 5:45 (Roma),
média 4:15; as de 3 voltas, 3:18–4:22, as de 4 voltas, 4:44–5:45. Três novas passavam do teto — mais longas que
qualquer corrida de antes, inclusive a última do jogo — e foram de 4 para 3 voltas (só `laps`; traçado igual):

| Pista | Copa | Volta (pro) | Corrida antes, 4 voltas (am. / pro / camp.) | Depois, 3 voltas | Batidas no cenário por corrida |
|---|---|---|---|---|---|
| `recife_antigo` | PE (dif. 3) | 1:31 | 6:19 / 6:01 / 5:42 | 4:45 / 4:31 / 4:18 | 31,7 → 18,7 |
| `sao_luis` | MA (dif. 4) | 1:30 | 6:15 / 5:54 / 5:35 | 4:43 / 4:25 / 4:11 | 22,7 → 11,0 |
| `ponte_rio_negro` | AM (dif. 4) | 1:31 | 6:13 / 5:57 / 5:40 | 4:38 / 4:26 / 4:14 | 14,7 → 6,3 |

Com 3 voltas o piloto médio tem menos tempo para subir do fundo do grid: no campeão ele chega 2–4 posições pior
nessas três (Recife 7,5º → 12,0º). As outras 74 novas ficam entre 3:21 e 5:37 no profissional.

**Dificuldade medida.** "Medido" é a velocidade média da IA (÷ velocidade máxima do carro, em todos os ticks da
corrida; profissional, 3 sementes) posta na escala das 32 antigas, onde cada nível tem a sua média: 1 → 0,877 ·
2 → 0,871 · 3 → 0,824 · 4 → 0,798 · 5 → 0,730 (linear entre elas). Nas 32 antigas o medido fica a menos de 1 nível do
rótulo em todas, menos a Autobahn (2 → 1,0: é reta pura). Entre 1 e 2 a velocidade quase não separa: ali vale o
índice técnico. Os outros sinais: batidas no cenário por corrida e a posição do piloto médio no campeão. No
profissional ele chega entre 1º e 7º em toda pista: não separa nada.

**Ordem dentro da copa — o que mudou.** Regra: duas pistas do mesmo nível fora de ordem (a anterior medida ≥ 0,5 mais
difícil, e o piloto médio no campeão chegando pior nela) trocam de lugar. Os rótulos não mudam, e a rampa dos testes
continua valendo. Em 6 das 9 copas a que vai para o fim é a de noite (5 de cidade e Maceió), como no Mundial (lá a pista
de noite fecha toda copa):

| Copa | Antes | Depois | Medido · piloto médio no campeão · batidas no cenário |
|---|---|---|---|
| RS | Guaíba, **Cuia**, Aparados | Guaíba, Aparados, **Cuia** | Cuia 4,1 · 4,5º · 6,7 × Aparados 2,4 · 3,5º · 5,7 |
| DF | Paranoá, **Torre de TV**, Brasília | Paranoá, Brasília, **Torre de TV** | Torre 4,3 · 12,0º · 17,3 × Brasília 3,0 · 11,5º · 3,7 |
| GO | Pirenópolis, **Caldas Novas**, Veadeiros | Pirenópolis, Veadeiros, **Caldas Novas** | Caldas 3,8 · 8,5º · 6,0 × Veadeiros 2,4 · 5,0º · 0,0 |
| MT | Transpantaneira, **Cuiabá**, Guimarães | Transpantaneira, Guimarães, **Cuiabá** | Cuiabá 5,0 · 10,0º · 13,0 × Guimarães 4,1 · 4,0º · 1,3 |
| AL | Maragogi, **Maceió**, Foz | Maragogi, Foz, **Maceió** | Maceió 4,7 · 10,0º · 5,7 × Foz 4,1 · 3,5º · 0,7 |
| PB | João Pessoa, **Campina Grande**, Pedra da Boca | João Pessoa, Pedra da Boca, **Campina Grande** | Campina 4,8 · 11,0º · 2,0 × Pedra 3,0 · 3,5º · 6,3 |
| CE | Jericoacoara, **Fortaleza**, Canoa | Jericoacoara, Canoa, **Fortaleza** | Fortaleza 4,9 · 12,0º · 17,3 × Canoa 4,0 · 5,5º · 0,7 |
| MA | São Luís, Mesas, **Lençóis** | **Lençóis**, Mesas, São Luís | Lençóis 2,8 · 4,0º · 1,3: era a última e é a mais fácil; São Luís (4,4 · 10,5º · 11,0) fecha |
| RO | Porto Velho, **Forte Príncipe**, Guaporé | Porto Velho, Guaporé, **Forte Príncipe** | Forte 5,3 · 7,0º · 7,7 × Guaporé 4,7 · 4,5º · 0,3 |

Ficaram como estavam: AP (Pororoca × Serra do Navio: os sinais se contradizem) e TO (Palmas é a pista mais difícil
do Brasil por todos os sinais — medido 5,4, piloto médio 7,3º no profissional, 29 batidas por corrida —, mas fica
só 0,4 acima da Ilha do Bananal, abaixo da regra; trocar as duas põe a mais dura no fim da Expedição, se o dono quiser).

**Rótulo que não bate com o medido (≥ 1 nível) e que só dado não conserta.** A rampa da Expedição (a média da copa não
cai) trava o relabel. As de cidade à noite jogam ~1 nível acima do rótulo: o índice técnico (curvas e morros) não vê o
paredão de prédios, onde a IA bate 13–32 vezes por corrida. As dunas, o contrário: o morro conta no índice e quase
não freia ninguém. Consertar pede outro traçado (fora desta tarefa) ou outra regra de rampa (decisão do dono).

| Pista | Dif. | Medido | Por que o rótulo ficou |
|---|---|---|---|
| `pedra_azul` (ES) | 3 | 1,6 | rebaixar põe ES (1,67) abaixo de MG (2,0) |
| `cuia_gaucha` (RS) | 3 | 4,1 | subir põe RS (3,0) acima de DF (2,67) |
| `torre_de_tv` (DF) | 3 | 4,3 | DF (3,0) acima de GO (2,67) |
| `campo_grande`, `estrada_parque` (MS) | 3 | 4,2 · 4,0 | MS acima de MT (3,0) |
| `cuiaba` (MT) | 4 | 5,0 | MT (3,33) acima de BA (3,0) |
| `recife_antigo` (PE) | 3 | 4,6 | PE (4,0) acima de PB (3,67); e é a 1ª da copa com rótulo menor, então também não troca de lugar |
| `lencois` (MA) | 4 | 2,8 | MA (3,67) abaixo de PI (4,0) |
| `macapa` (AP) | 3 | 4,1 | subir daria (AP 4,67 = RR); ficou porque o índice técnico (8,7) e o piloto médio no profissional (2º) dizem 3 |
| `boa_vista` (RR) | 4 | 5,0 | RR (5,0) acima de RO (4,67) |

**Regiões.** Média medida: Sudeste 1,55 → Sul 2,35 → Centro-Oeste 3,23 → Nordeste 3,71 → Norte 4,76; o Mundial vai de
2,27 (EUA) a 4,87 (Mediterrâneo). A região sempre sobe. Dentro dela a copa oscila ±0,5; a Bahia (2,8, depois de MT
3,4) abre o Nordeste mais leve.

**Combustível.** Toda pista fecha a corrida inteira: `tests/ai.test.ts` (corrida inteira no campeão, 10 carros, um teste
por pista, nenhum carro da IA sem combustível) e `tests/fuel.test.ts` (aviso nos dois estilos) passam nas 109, com as
voltas novas (03/10).

As 77 novas, na ordem das copas de hoje (medido e batidas: profissional, 3 sementes; corrida: média das sementes de
cada dificuldade; piloto médio: campeão, 2 sementes):

| Copa | Pista | Dif. | Medido | Voltas | Corrida (am./pro/camp.) | Volta (pro) | Batidas no cenário | v média | Piloto médio no campeão |
|---|---|---|---|---|---|---|---|---|---|
| RJ | `paraty` | 1 | 1,0 | 3 | 4:14 / 3:50 / 3:32 | 1:19 | 2,7 | 0,88 | 8,0º |
| RJ | `serra_dos_orgaos` | 2 | 1,6 | 3 | 4:03 / 3:41 / 3:24 | 1:16 | 9,0 | 0,87 | 7,0º |
| SP | `ilhabela` | 1 | 1,0 | 3 | 3:43 / 3:21 / 3:03 | 1:09 | 2,7 | 0,88 | 7,5º |
| SP | `campos_do_jordao` | 2 | 1,6 | 3 | 3:54 / 3:33 / 3:19 | 1:13 | 1,3 | 0,87 | 5,0º |
| MG | `pampulha` | 1 | 0,4 | 3 | 4:13 / 3:48 / 3:33 | 1:19 | 4,3 | 0,88 | 9,5º |
| MG | `ouro_preto` | 2 | 1,6 | 3 | 3:51 / 3:30 / 3:14 | 1:12 | 2,0 | 0,87 | 7,0º |
| MG | `serra_da_canastra` | 3 | 2,8 | 3 | 4:07 / 3:50 / 3:39 | 1:18 | 0,0 | 0,83 | 5,5º |
| ES | `itaunas` | 1 | 1,0 | 3 | 3:46 / 3:25 / 3:10 | 1:11 | 5,3 | 0,88 | 8,5º |
| ES | `convento_penha` | 2 | 2,1 | 3 | 4:06 / 3:45 / 3:31 | 1:17 | 1,7 | 0,87 | 5,0º |
| ES | `pedra_azul` | 3 | 1,6 | 3 | 3:49 / 3:31 / 3:22 | 1:12 | 5,3 | 0,87 | 2,0º |
| PR | `foz_do_iguacu` | 2 | 1,6 | 3 | 4:01 / 3:41 / 3:24 | 1:16 | 3,3 | 0,87 | 6,5º |
| PR | `curitiba` | 3 | 2,6 | 4 | 5:35 / 5:15 / 5:01 | 1:20 | 3,3 | 0,84 | 7,5º |
| SC | `floripa` | 2 | 1,6 | 3 | 4:09 / 3:46 / 3:30 | 1:18 | 8,7 | 0,87 | 9,0º |
| SC | `camboriu` | 2 | 2,2 | 4 | 4:56 / 4:32 / 4:18 | 1:09 | 4,7 | 0,86 | 10,0º |
| SC | `rio_do_rastro` | 3 | 2,5 | 3 | 4:17 / 3:60 / 3:49 | 1:22 | 4,0 | 0,85 | 13,0º |
| RS | `orla_guaiba` | 2 | 2,0 | 3 | 4:08 / 3:49 / 3:37 | 1:19 | 1,7 | 0,87 | 4,0º |
| RS | `aparados_da_serra` | 3 | 2,4 | 3 | 3:52 / 3:37 / 3:27 | 1:14 | 5,7 | 0,85 | 3,5º |
| RS | `cuia_gaucha` | 3 | 4,1 | 3 | 4:24 / 4:07 / 3:56 | 1:24 | 6,7 | 0,79 | 4,5º |
| DF | `lago_paranoa` | 2 | 2,2 | 3 | 3:42 / 3:24 / 3:13 | 1:10 | 4,3 | 0,86 | 8,0º |
| DF | `brasilia` | 3 | 3,0 | 3 | 4:32 / 4:18 / 4:06 | 1:27 | 3,7 | 0,82 | 11,5º |
| DF | `torre_de_tv` | 3 | 4,3 | 4 | 5:12 / 4:55 / 4:40 | 1:14 | 17,3 | 0,78 | 12,0º |
| GO | `pirenopolis` | 2 | 2,2 | 3 | 3:48 / 3:31 / 3:22 | 1:12 | 2,3 | 0,86 | 4,0º |
| GO | `chapada_veadeiros` | 3 | 2,4 | 3 | 4:04 / 3:47 / 3:37 | 1:17 | 0,0 | 0,85 | 5,0º |
| GO | `caldas_novas` | 3 | 3,8 | 3 | 4:24 / 4:07 / 3:57 | 1:24 | 6,0 | 0,80 | 8,5º |
| MS | `bonito` | 3 | 2,5 | 3 | 4:14 / 3:57 / 3:50 | 1:21 | 3,0 | 0,85 | 10,5º |
| MS | `campo_grande` | 3 | 4,2 | 3 | 4:05 / 3:50 / 3:39 | 1:18 | 5,7 | 0,79 | 7,0º |
| MS | `estrada_parque` | 3 | 4,0 | 3 | 4:26 / 4:08 / 3:55 | 1:24 | 0,7 | 0,80 | 6,5º |
| MT | `chapada_guimaraes` | 4 | 4,1 | 3 | 4:14 / 3:59 / 3:48 | 1:21 | 1,3 | 0,79 | 4,0º |
| MT | `cuiaba` | 4 | 5,0 | 4 | 5:40 / 5:22 / 5:06 | 1:22 | 13,0 | 0,73 | 10,0º |
| BA | `porto_seguro` | 2 | 2,4 | 3 | 3:55 / 3:38 / 3:27 | 1:14 | 5,0 | 0,85 | 5,0º |
| BA | `salvador` | 3 | 2,8 | 3 | 4:13 / 3:57 / 3:45 | 1:20 | 9,0 | 0,83 | 4,0º |
| BA | `chapada_diamantina` | 4 | 3,3 | 3 | 4:19 / 4:09 / 3:54 | 1:24 | 4,3 | 0,82 | 15,0º |
| SE | `aracaju` | 3 | 2,9 | 4 | 5:06 / 4:48 / 4:33 | 1:13 | 10,0 | 0,83 | 10,0º |
| SE | `sao_cristovao` | 3 | 2,9 | 3 | 3:55 / 3:43 / 3:31 | 1:16 | 2,3 | 0,83 | 6,5º |
| SE | `xingo` | 4 | 3,0 | 3 | 4:05 / 3:48 / 3:39 | 1:18 | 7,3 | 0,82 | 2,0º |
| AL | `maragogi` | 2 | 2,2 | 3 | 4:01 / 3:41 / 3:29 | 1:16 | 2,0 | 0,86 | 4,0º |
| AL | `foz_sao_francisco` | 4 | 4,1 | 3 | 4:23 / 4:07 / 3:56 | 1:24 | 0,7 | 0,79 | 3,5º |
| AL | `maceio` | 4 | 4,7 | 4 | 5:28 / 5:11 / 4:55 | 1:18 | 5,7 | 0,75 | 10,0º |
| PE | `recife_antigo` | 3 | 4,6 | 3 | 4:45 / 4:31 / 4:18 | 1:31 | 18,7 | 0,76 | 12,0º |
| PE | `olinda` | 4 | 3,9 | 3 | 3:53 / 3:39 / 3:28 | 1:15 | 8,7 | 0,80 | 6,0º |
| PE | `noronha` | 4 | 4,2 | 3 | 4:21 / 4:08 / 3:54 | 1:25 | 3,3 | 0,79 | 7,5º |
| PB | `joao_pessoa` | 3 | 2,9 | 3 | 4:06 / 3:52 / 3:40 | 1:19 | 8,0 | 0,83 | 6,5º |
| PB | `pedra_da_boca` | 4 | 3,0 | 3 | 4:14 / 3:57 / 3:46 | 1:21 | 6,3 | 0,82 | 3,5º |
| PB | `campina_grande` | 4 | 4,8 | 4 | 5:39 / 5:23 / 5:06 | 1:22 | 2,0 | 0,75 | 11,0º |
| RN | `natal` | 3 | 2,4 | 3 | 4:01 / 3:43 / 3:31 | 1:16 | 3,3 | 0,85 | 4,5º |
| RN | `ponta_negra` | 4 | 4,3 | 3 | 4:25 / 4:09 / 3:57 | 1:25 | 7,3 | 0,78 | 4,5º |
| RN | `cajueiro_pirangi` | 4 | 4,4 | 3 | 4:32 / 4:20 / 4:06 | 1:28 | 13,7 | 0,77 | 12,5º |
| CE | `jericoacoara` | 4 | 3,7 | 3 | 4:07 / 3:53 / 3:44 | 1:19 | 4,0 | 0,81 | 3,5º |
| CE | `canoa_quebrada` | 4 | 4,0 | 3 | 4:21 / 4:04 / 3:52 | 1:23 | 0,7 | 0,80 | 5,5º |
| CE | `fortaleza_beira_mar` | 4 | 4,9 | 4 | 5:20 / 5:03 / 4:49 | 1:17 | 17,3 | 0,73 | 12,0º |
| PI | `delta_parnaiba` | 4 | 4,1 | 3 | 4:19 / 4:03 / 3:54 | 1:23 | 2,0 | 0,79 | 3,0º |
| PI | `sete_cidades` | 4 | 4,1 | 3 | 4:39 / 4:24 / 4:10 | 1:29 | 1,3 | 0,79 | 8,5º |
| PI | `serra_capivara` | 4 | 4,4 | 3 | 4:17 / 4:04 / 3:52 | 1:23 | 2,3 | 0,77 | 6,0º |
| MA | `lencois` | 4 | 2,8 | 3 | 3:37 / 3:23 / 3:13 | 1:09 | 1,3 | 0,83 | 4,0º |
| MA | `chapada_das_mesas` | 4 | 4,8 | 3 | 4:02 / 3:49 / 3:39 | 1:18 | 0,7 | 0,74 | 5,0º |
| MA | `sao_luis` | 4 | 4,4 | 3 | 4:43 / 4:25 / 4:11 | 1:30 | 11,0 | 0,77 | 10,5º |
| PA | `alter_do_chao` | 4 | 4,3 | 3 | 4:18 / 4:03 / 3:52 | 1:23 | 8,3 | 0,78 | 5,5º |
| PA | `belem` | 4 | 4,9 | 3 | 4:15 / 4:02 / 3:50 | 1:23 | 4,7 | 0,73 | 8,0º |
| PA | `marajo` | 5 | 4,8 | 3 | 4:32 / 4:19 / 4:05 | 1:28 | 0,7 | 0,74 | 7,0º |
| AM | `manaus` | 4 | 4,1 | 3 | 4:39 / 4:27 / 4:11 | 1:30 | 6,3 | 0,79 | 12,0º |
| AM | `ponte_rio_negro` | 4 | 4,8 | 3 | 4:38 / 4:26 / 4:14 | 1:31 | 6,3 | 0,74 | 13,5º |
| AM | `parintins` | 5 | 4,9 | 3 | 4:34 / 4:19 / 4:06 | 1:29 | 1,7 | 0,74 | 8,5º |
| AP | `macapa` | 3 | 4,1 | 3 | 3:54 / 3:41 / 3:30 | 1:15 | 5,3 | 0,79 | 4,5º |
| AP | `pororoca_araguari` | 5 | 4,8 | 3 | 4:27 / 4:13 / 4:03 | 1:26 | 3,3 | 0,74 | 5,0º |
| AP | `serra_do_navio` | 5 | 4,2 | 3 | 4:02 / 3:49 / 3:37 | 1:18 | 1,0 | 0,78 | 6,0º |
| RR | `boa_vista` | 4 | 5,0 | 4 | 5:05 / 4:54 / 4:37 | 1:15 | 15,3 | 0,73 | 10,0º |
| RR | `monte_roraima` | 5 | 5,1 | 3 | 4:54 / 4:40 / 4:26 | 1:35 | 8,0 | 0,72 | 10,5º |
| RR | `lago_caracarana` | 5 | 4,8 | 3 | 4:25 / 4:12 / 4:00 | 1:26 | 2,7 | 0,74 | 5,0º |
| RO | `porto_velho` | 4 | 4,8 | 3 | 4:38 / 4:22 / 4:10 | 1:30 | 2,3 | 0,74 | 11,5º |
| RO | `vale_guapore` | 5 | 4,7 | 3 | 4:29 / 4:15 / 4:03 | 1:27 | 0,3 | 0,75 | 4,5º |
| RO | `forte_principe` | 5 | 5,3 | 3 | 4:26 / 4:12 / 4:01 | 1:26 | 7,7 | 0,71 | 7,0º |
| AC | `rio_branco` | 4 | 4,4 | 3 | 4:40 / 4:26 / 4:14 | 1:30 | 1,0 | 0,77 | 9,5º |
| AC | `geoglifos` | 5 | 4,9 | 3 | 4:39 / 4:22 / 4:09 | 1:30 | 2,3 | 0,73 | 5,5º |
| AC | `estrada_pacifico` | 5 | 4,6 | 3 | 4:24 / 4:09 / 3:59 | 1:25 | 5,7 | 0,76 | 5,0º |
| TO | `jalapao` | 5 | 4,6 | 3 | 4:23 / 4:08 / 3:57 | 1:24 | 2,7 | 0,76 | 5,0º |
| TO | `palmas` | 5 | 5,4 | 4 | 5:51 / 5:37 / 5:21 | 1:26 | 29,3 | 0,70 | 9,5º |
| TO | `ilha_do_bananal` | 5 | 5,0 | 3 | 4:15 / 4:00 / 3:48 | 1:22 | 0,7 | 0,73 | 5,0º |

As 32 de antes, na mesma rodada (a régua do "medido" e da duração):

| Copa | Pista | Dif. | Medido | Voltas | Corrida (am./pro/camp.) | Volta (pro) | Batidas no cenário | v média | Piloto médio no campeão |
|---|---|---|---|---|---|---|---|---|---|
| RJ | `copacabana` | 1 | 1,0 | 3 | 3:43 / 3:21 / 3:01 | 1:09 | 3,3 | 0,88 | 9,5º |
| SP | `sampa_noite` | 3 | 3,0 | 4 | 5:17 / 5:01 / 4:44 | 1:15 | 25,7 | 0,82 | 12,0º |
| PR | `serra_do_mar` | 2 | 2,2 | 3 | 3:36 / 3:18 / 3:06 | 1:08 | 2,3 | 0,86 | 7,0º |
| MT | `transpantaneira` | 1 | 1,0 | 3 | 4:25 / 3:59 / 3:32 | 1:22 | 0,0 | 0,88 | 12,5º |
| EUA | `rota_66` | 1 | 1,0 | 3 | 3:42 / 3:20 / 2:60 | 1:09 | 1,7 | 0,88 | 9,0º |
| EUA | `rochosas` | 2 | 2,0 | 3 | 4:17 / 3:55 / 3:35 | 1:20 | 11,0 | 0,87 | 11,5º |
| EUA | `canion` | 3 | 3,0 | 3 | 3:35 / 3:20 / 3:10 | 1:08 | 1,7 | 0,82 | 6,5º |
| EUA | `las_vegas` | 3 | 3,0 | 4 | 5:05 / 4:44 / 4:29 | 1:12 | 19,0 | 0,82 | 13,0º |
| JAPAO | `baia_toquio` | 2 | 1,6 | 3 | 3:43 / 3:22 / 3:05 | 1:10 | 4,7 | 0,87 | 8,0º |
| JAPAO | `yanbaru` | 3 | 2,8 | 3 | 4:07 / 3:51 / 3:42 | 1:19 | 6,3 | 0,83 | 8,0º |
| JAPAO | `monte_fuji` | 4 | 3,3 | 3 | 3:38 / 3:23 / 3:12 | 1:09 | 8,3 | 0,82 | 4,0º |
| JAPAO | `osaka_neon` | 4 | 4,5 | 4 | 5:28 / 5:13 / 4:55 | 1:19 | 26,0 | 0,77 | 11,0º |
| EUROPA | `autobahn` | 2 | 1,0 | 3 | 4:08 / 3:45 / 3:21 | 1:17 | 3,0 | 0,88 | 10,0º |
| EUROPA | `paris` | 3 | 3,6 | 4 | 5:52 / 5:31 / 5:18 | 1:23 | 24,7 | 0,81 | 12,5º |
| EUROPA | `passo_alpino` | 5 | 4,6 | 3 | 3:38 / 3:25 / 3:15 | 1:10 | 3,7 | 0,76 | 6,0º |
| EUROPA | `monaco_noite` | 5 | 4,8 | 4 | 5:48 / 5:30 / 5:14 | 1:24 | 3,0 | 0,74 | 10,5º |
| AFRICA_DO_SUL | `kruger` | 3 | 3,0 | 3 | 4:31 / 4:15 / 4:03 | 1:26 | 2,3 | 0,82 | 12,0º |
| AFRICA_DO_SUL | `karoo` | 4 | 4,0 | 3 | 4:37 / 4:22 / 4:09 | 1:28 | 1,7 | 0,80 | 9,5º |
| AFRICA_DO_SUL | `drakensberg` | 4 | 3,6 | 3 | 4:09 / 3:54 / 3:42 | 1:20 | 4,3 | 0,81 | 7,0º |
| AFRICA_DO_SUL | `boa_esperanca` | 5 | 5,0 | 4 | 5:34 / 5:20 / 5:03 | 1:21 | 2,0 | 0,73 | 9,5º |
| AUSTRALIA | `outback` | 3 | 2,8 | 3 | 4:26 / 4:10 / 3:54 | 1:24 | 2,7 | 0,83 | 14,0º |
| AUSTRALIA | `great_ocean` | 4 | 3,8 | 3 | 4:17 / 4:04 / 3:51 | 1:23 | 7,0 | 0,80 | 12,0º |
| AUSTRALIA | `daintree` | 5 | 5,3 | 3 | 4:20 / 4:05 / 3:54 | 1:24 | 2,7 | 0,71 | 8,0º |
| AUSTRALIA | `sydney` | 5 | 5,1 | 4 | 5:47 / 5:31 / 5:14 | 1:24 | 25,0 | 0,72 | 11,0º |
| ESCANDINAVIA | `atlantico` | 4 | 3,3 | 3 | 3:54 / 3:41 / 3:29 | 1:15 | 4,7 | 0,82 | 5,0º |
| ESCANDINAVIA | `laponia` | 4 | 4,1 | 3 | 4:28 / 4:15 / 4:04 | 1:26 | 13,0 | 0,79 | 9,0º |
| ESCANDINAVIA | `trollstigen` | 5 | 4,8 | 3 | 4:12 / 3:59 / 3:47 | 1:22 | 0,7 | 0,74 | 8,0º |
| ESCANDINAVIA | `tromso` | 5 | 5,0 | 4 | 5:54 / 5:39 / 5:23 | 1:26 | 18,0 | 0,73 | 12,5º |
| MEDITERRANEO | `amalfi` | 4 | 4,2 | 3 | 4:11 / 3:57 / 3:45 | 1:21 | 2,7 | 0,78 | 7,0º |
| MEDITERRANEO | `santorini` | 5 | 5,1 | 3 | 4:21 / 4:08 / 3:57 | 1:25 | 3,7 | 0,72 | 9,0º |
| MEDITERRANEO | `etna` | 5 | 4,9 | 3 | 4:17 / 4:03 / 3:49 | 1:23 | 3,3 | 0,74 | 6,5º |
| MEDITERRANEO | `roma` | 5 | 5,2 | 4 | 6:02 / 5:45 / 5:29 | 1:28 | 25,3 | 0,71 | 10,5º |

### Antes da onda G (25/09/2026, 32 pistas)

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
- **Destravamento**: copa já concluída fica aberta (`isCupUnlocked`), e a herdada do save antigo também (os EUA de quem
  venceu a Copa Brasil, `save.cupsUnlocked`); com save antigo, mais de uma copa pode estar "aberta" ao mesmo tempo (o
  cursor começa na primeira aberta não concluída — `frontierCupIndex` já faz isso).
- `scripts/pistas-ui.mjs`, `playtest-layout.mjs`, `playtest-records.mjs` e `playtest.mjs` ainda usam o id `brasil`
  e `COPA_BRASIL` (o save de teste passa pela migração, mas o roteiro que espera o foco em "brasil" vai falhar).

Como era até a onda F (8 copas de 4): lista de copas à esquerda com o detalhe à direita; grade de pistas com uma
linha de 4 por copa, ↑↓ trocando de copa na mesma coluna; cabe sem rolar em 1280×720 e 1920×1080; Steam Deck e
1024×600 conferidos com `scripts/pistas-ui.mjs`.

## Pista nova — passo a passo

1. Escreva a pista em `tracks.ts`, na posição da copa, com um comentário de identidade (o que o
   traçado representa). Curva 2 fácil / 4 média / 6 forte; lombada = `hl(comprimento, altura)`. Volta que não gira
   para lado nenhum (soma das curvas ~0) vira um risco no minimapa. Pista que desenha alguma coisa no minimapa: o
   polígono vai em `scripts/track-shapes.ts` e os `ops` saem de `scripts/shape-to-track.ts --apply` ("Pistas com
   desenho"), com um bloco em `tests/track.test.ts` escrito antes e visto falhar.
2. Ponha o id em `trackIds` da copa (`cups.ts`) e o lugar em `places.ts`. Nome em `core.track.<id>` (PT e EN). Copa
   nova: id ASCII minúsculo, `stage` (e `region`/`state` na Expedição), `requires` = a copa anterior,
   `core.cup.<id>` e `core.country.<País>` em `src/i18n/core.ts`, conquista `COPA_<ID>` em `ACHIEVEMENTS`
   (`src/game/desktop.ts`), na tabela de `desktop/README.md` (Steamworks) e um rival em `CUP_RIVALS`.
3. `npx vitest run tests/track.test.ts tests/ai.test.ts tests/fuel.test.ts` e
   `npx tsx scripts/balance.ts 150 profissional 11 <id>` (IA completa a volta, grama < 1%, melhor volta
   ~1:00–1:40) e `npx tsx scripts/balance.ts corrida profissional 11 <id>` (corrida inteira entre 3:18 e 5:45, a
   faixa das 32 antigas; v média na escala de "Balanceamento" perto do rótulo; mais batidas no cenário que as
   vizinhas de copa = ela fecha a copa). Se o índice técnico contradisser o rótulo ou a rampa da etapa, ajuste o
   traçado ou a dificuldade — não o teste. Volta muito mais longa que 2.100 segmentos pede olhar o combustível: os testes de corrida
   inteira e de aviso dizem se o tanque ainda fecha.
