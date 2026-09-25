# Modo Carreira (3.3) e campeonato salvo (1.7a)

Decisões e números do Modo Carreira e do "Continuar" do campeonato normal. As constantes moram no
código (`src/core/career.ts` para dinheiro e preços, `src/core/constants.ts` para o efeito das
melhorias); este arquivo explica o porquê. Mudou um número, atualize a tabela e rode
`npx vitest run tests/career.test.ts` (a calibragem tem teste).

## Fluxo

```
Menu principal → Carreira → (Continuar carreira | Nova carreira)
  → lobby (1–4 humanos, co-op ou versus)
  → garagem (carro, melhorias, dinheiro, atributos antes → depois)
  → corrida da copa atual → resultado → classificação → garagem → …
  → … até a última copa de CUPS (quantas houver: nada assume 4).
```

- **Nova carreira** com uma salva em andamento pede confirmação (o botão vira "Apagar a salva e
  começar outra?"). Há um espaço de carreira só.
- **Lobby da carreira**: entra quem quiser (1–4), escolhe co-op ou versus. O carro não se escolhe
  ali ("Carro e melhorias: na garagem"); a carreira nasce com o carro do lobby se ele for um dos
  quatro originais. O modo (co-op/versus) fica fixo pela carreira inteira. Dificuldade, câmbio,
  carros na pista e assistências continuam sendo as opções do lobby (valem para toda corrida, como
  no Campeonato); a carreira soma o nível da IA por cima da dificuldade.
- **Continuar carreira**: o lobby exige os mesmos N pilotos, sentados a partir do P1, com nome e
  modo fixos (vêm do save). Depois vai direto para a garagem.
- **Garagem**: um painel por piloto, cada um navegado pelo próprio controle (como no lobby).
  ←→ troca de carro (carro seu já fica escolhido; carro à venda aparece na vitrine com preço e
  quanto falta), Enter compra o carro ou a melhoria em foco, PRONTO trava o painel. Com todos
  prontos, a corrida começa. Esc do P1 volta ao menu (a carreira já está salva: cada compra grava).
  Com um piloto só, o botão é CORRER. Com 1–2 pilotos cada painel tem carro e atributos à esquerda e
  melhorias à direita; com 3–4, uma coluna por piloto, que cabe sem rolar em
  16:9, 16:10 e 4:3.
- **Prévia**: com o foco numa melhoria, os atributos mostram antes → depois (barra fantasma verde e
  o número novo); com o foco num carro à venda, mostram o seu carro atual → o carro à venda.
- **Resultado e classificação** são as telas da copa normal; na carreira o botão da classificação é
  GARAGEM, e a garagem abre com o relatório da corrida (prêmio de cada um, bônus da equipe,
  eliminação ou copa concluída).
- **Sair no meio da corrida** (pausa → menu) não conta nada: a corrida volta a ser a próxima da
  copa. É a mesma regra da copa normal; não há punição por desistir.

## Dinheiro

| | Valor |
|---|---|
| Dinheiro inicial | $ 2.500 por piloto (co-op: o cofre começa com $ 2.500 × pilotos) |
| Prêmio por posição (1ª copa) | 1º 6.000 · 2º 4.500 · 3º 3.500 · 4º 2.800 · 5º 2.200 · 6º 1.700 · 7º 1.300 · 8º 1.000 · 9º 800 · 10º 600 · 11º em diante 400 |
| Bônus de equipe (só co-op) | equipe em 1º 3.000 · 2º 2.000 · 3º 1.000 (colocação da equipe na corrida, a mesma régua da classificação) |
| Crescimento por copa | prêmio e bônus × 1 na primeira copa até × 1,75 na última, linear entre elas (`prizeMultiplier`), arredondado a $ 10 |

- **Co-op** (dois ou mais humanos no mesmo time): uma carteira só, o **cofre da equipe**. O prêmio
  de cada piloto e o bônus entram nele, e qualquer um compra com ele. Isso empurra a conversa do
  sofá ("gasta no teu motor, que eu fico com o nitro") em vez de cada um olhar só para o seu saldo.
- **Versus e solo**: uma carteira por assento; ninguém paga pelo outro, não há bônus.
- `earnings` de cada piloto guarda o que ele ganhou (estatística; o bônus da equipe não entra).

## Garagem: carros e melhorias

### Carros (8)

Os quatro originais são de todos e sempre liberados. Os quatro novos se compram na carreira;
comprado, o carro vai para a garagem daquele piloto **e** fica liberado em todas as outras
modalidades (`SaveData.carsUnlocked`), sem as melhorias. A IA corre só com os originais, para o
elenco e o balanceamento das pistas não mudarem.

| Carro | Preço | Vel. máx. | Acel. | Freio | Curvas | Consumo | Troca |
|---|---|---|---|---|---|---|---|
| Falcão GT | — | 300 km/h | 700 | 2.600 | 0,75 | 1,00 | equilibrado |
| Trovão V12 | — | 318 | 640 | 2.400 | 0,60 | 1,25 | reta × curva e consumo |
| Tornado RS | — | 285 | 830 | 2.800 | 0,92 | 0,95 | curva e arrancada × reta |
| Camelo X | — | 293 | 700 | 2.600 | 0,70 | 0,68 | economia × resto |
| Sucuri E | 16.000 | 309 | 640 | 2.700 | 0,80 | 0,50 | tanque para a corrida toda × arrancada |
| Carcará RS | 20.000 | 294 | 900 | 3.000 | 0,97 | 1,05 | arrancada e curva × fim de reta |
| Pororoca V10 | 22.000 | 336 | 660 | 2.500 | 0,58 | 1,40 | a maior reta × curva e consumo |
| Boitatá GT | 30.000 | 321 | 780 | 2.800 | 0,84 | 0,90 | forte em tudo × o mais caro |

Cada carro novo é o melhor (ou empatado) em algum atributo e perde para um original em outro (há
teste). Nomes inventados, sem marca, no tema de bicho e lenda brasileira (Sucuri, Carcará, Pororoca,
Boitatá). O Pororoca se chamou "Furacão V10" até a revisão: é a tradução literal do Lamborghini
Huracán V10. Um teste recusa, nos carros à venda, nome de marca, de modelo ou a tradução dele.

### Melhorias (por carro, nível 0–3)

| Peça | Efeito por nível | Nível 1 / 2 / 3 | Total |
|---|---|---|---|
| Motor | velocidade máxima +2,5% | 2.400 / 4.200 / 6.600 | 13.200 |
| Turbo | aceleração +8% | 2.000 / 3.500 / 5.500 | 11.000 |
| Pneus | dirigibilidade +0,04 (teto 1,0; ver abaixo) | 2.000 / 3.500 / 5.500 | 11.000 |
| Freios | frenagem +12% | 1.600 / 2.800 / 4.400 | 8.800 |
| Tanque | consumo −10% | 1.400 / 2.500 / 3.800 | 7.700 |
| Nitro | +1 carga (3 → 6) | 2.200 / 3.900 / 6.100 | 12.200 |

Um carro completo custa $ 63.900 — de propósito, mais do que um piloto médio junta na carreira:
é preciso escolher. As melhorias são **do carro**: trocar de carro não leva as peças (o carro novo
começa no nível 0), o que dá peso à decisão de comprar um carro caro no meio da carreira.

Compra recusada não muda nada e diz por quê: sem saldo, nível máximo, carro já possuído, carro que
não é seu (`PurchaseResult`).

**Teto por carro.** Só se vende nível que muda alguma coisa (`upgradeCap` em `src/core/sim/stats.ts`,
`partMaxLevel`/`upgradePrice` em `src/core/career.ts`). Na prática isso só pega os pneus dos carros de
dirigibilidade alta, que batem no teto de 1,0 antes do nível 3: o **Tornado RS** (0,92 → 0,96 → 1,00)
para no nível 2 e o **Carcará RS** (0,97 → 1,00) no nível 1. Antes da revisão a garagem cobrava
$ 5.500 (Tornado) e $ 9.000 (Carcará) por níveis sem efeito nenhum. Na garagem, o nível fora de venda
aparece riscado, a peça diz "no teto deste carro" e o preço vira MÁX. Save antigo com nível acima do
teto volta ao teto (o efeito era o mesmo; o dinheiro não volta).

### Calibragem

Meta: um piloto médio compra 1–2 itens por corrida e chega ao fim da carreira. Um piloto que chega
sempre em 4º junta ~$ 46.000 em 4 copas (12 corridas) além dos $ 2.500 iniciais — ~$ 3.900 por
corrida, contra itens de $ 1.400 a $ 6.600. O teste "calibragem" simula comprar sempre o item mais
barato e exige de 1 a 2 itens por corrida com 4 e com 8 copas (o multiplicador de prêmio depende da
posição relativa da copa, não da quantidade). Quem vence sempre junta o dobro e pode trocar de carro.

Dinheiro sozinho não prova que ele **chega**: isso depende da IA que evolui. Por isso há uma segunda
calibragem, com corridas inteiras (`tests/career-balance.test.ts`; sonda copa a copa em
`npx tsx scripts/career-balance.ts [habilidade] [sementes] [máximo da IA]`):

- **O piloto médio** é o cérebro da IA com habilidade fixa (`PROXY_SKILL` = 0,97) no assento humano,
  20 carros, profissional, assistências padrão. 0,97 é a habilidade que chega por volta de 4º na
  primeira copa, de fábrica, contra a IA nível 0 — o mesmo piloto que a calibragem de dinheiro supõe.
- **As melhorias dele** em cada copa são as que essa calibragem lhe dá (sempre 4º, compra a peça mais
  barata do Falcão): a pior estratégia de desempenho (tanque e freios antes do motor). Se ela chega,
  uma compra pensada chega com folga.
- **O teste** exige: a âncora (primeira copa, de fábrica) com média entre 2,5 e 5,5; e na última copa,
  com as melhorias da calibragem e contra o nível da IA daquela copa, média até 5,5 e top 5 em pelo
  menos metade das corridas.

Medido na revisão (piloto 0,97, 5 sementes × 3 pistas por copa; posição média e corridas no top 5):

| Máximo da IA | Brasil (IA 0) | EUA | Japão | Europa |
|---|---|---|---|---|
| 3 (antes) | 4,4 · 12/15 | 7,6 · 1/9 | 9,3 · 0/9 | **14,6 · 0/9** |
| 2 | — | 5,7 · 4/9 | 5,9 · 5/9 | 7,9 · 1/9 |
| 1,5 | — | 5,0 · 9/15 | 4,0 · 14/15 | 6,0 · 8/15 |
| **1,25 (atual)** | 4,4 · 12/15 | 4,7 · 9/15 | 3,5 · 14/15 | 4,7 · 12/15 |

(As linhas 3 e 2 foram com 3 sementes.) Com 1,25 o piloto médio fica por volta de 4º–5º do começo
ao fim, que é o que a economia supõe. Uma carreira inteira simulada com ele (compras reais entre as
corridas, save ida e volta a cada corrida) terminou em 13 corridas, com uma eliminação nos EUA.

## Rivais que evoluem

O nível da IA vai de 0 na primeira copa a `CAREER_AI_LEVEL_MAX` = **1,25** na última, linear entre
elas (`careerAiLevel`, fracionário: 0 · 0,42 · 0,83 · 1,25 com 4 copas). Ele aplica motor, turbo,
pneus e freios no mesmo nível a todos os carros da IA (tanque e nitro ficam de fábrica): na última
copa, +3,1% de velocidade máxima, +10% de aceleração, +0,05 de dirigibilidade e +15% de freio.
Fora da carreira `aiLevel` é ausente (0) e nada muda.

Até a revisão o máximo era 3 (= $ 44.000 de melhorias por carro da IA, tudo o que o piloto médio
junta na carreira inteira). Medido com corridas inteiras, isso levava o piloto médio do pódio na
primeira copa para o fim do grid na última (média 14,6), e nem o Falcão com as seis peças no máximo
passava da primeira corrida da Europa com um piloto de habilidade 0,95. O 1,25 saiu da tabela da calibragem acima.

Com 8 copas, o máximo continua na última (a curva é pela posição relativa da copa, como o prêmio), e
o piloto médio chega lá com mais corridas de dinheiro: a última copa fica mais fácil que com 4. Se
isso sobrar, suba `CAREER_AI_LEVEL_MAX` e rode `scripts/career-balance.ts` e o teste de novo.

## Eliminação

A mesma regra da copa normal: co-op passa se a equipe ficar entre as 3 melhores equipes da corrida;
versus/solo passa se algum humano chegar no top 5. Eliminado:

- **a corrida que eliminou paga só a ajuda de custo**: metade do prêmio da posição
  (`ELIMINATED_PRIZE_SHARE`), nunca menos que o prêmio de participação ($ 400 × fator da copa), e sem
  bônus de equipe; o que já foi ganho fica, e as compras também;
- **a copa recomeça da primeira pista** (tentativa 2, 3, …, sem limite e sem taxa), com a IA no
  mesmo nível;
- a garagem abre com a faixa "ELIMINADO" e dá para gastar antes de tentar de novo.

Por que assim: perder a carreira inteira numa corrida ruim é duro demais para jogar no sofá com
crianças; refazer a copa com metade do prêmio custa tempo e dinheiro, que é punição suficiente. A
primeira versão não pagava nada na eliminação: somado à IA forte demais, uma equipe co-op um pouco
abaixo da média que tinha gastado tudo ficou 24 corridas seguidas eliminada com o cofre em $ 0 (a
revisão simulou). Com a ajuda de custo, até uma dupla no fundo do grid junta $ 800 por tentativa e
compra a peça mais barata em duas (há teste); cada tentativa deixa o carro um pouco melhor. Na
mesma simulação da revisão (dupla co-op de habilidade 0,93 — sozinha, ~9º na primeira copa —, no
profissional, gastando tudo), a equipe agora sai: 23 tentativas na primeira copa, depois termina a
carreira em 40 corridas. É um caminho longo de propósito; para esse jogador a saída natural é o amador.

Copa concluída: ela conta também como concluída no Campeonato normal (libera a próxima copa lá), a
IA sobe de nível e o prêmio cresce. A última copa concluída encerra a carreira ("CARREIRA
CONCLUÍDA" na garagem e na tela Carreira; a garagem não vende mais nada e o foco já começa no botão MENU).

## Recordes

Recorde por pista é de **carro de fábrica**: volta ou corrida feita com algum nível de melhoria
conta corrida e vitória nas estatísticas, mas não entra nos recordes (`recordRaceResults` filtra
por `hasUpgrades`). Sem isso, a tabela de recordes viraria uma tabela de quem gastou mais na
carreira. Os carros comprados, que ficam liberados nas outras modalidades sem melhorias, podem
bater recorde normalmente.

## Campeonato salvo no meio (1.7a)

- A copa normal é gravada na largada e depois de cada corrida (`SaveData.cupInProgress`: a
  classificação, a semente do elenco e os humanos). Copa concluída ou eliminada sai do save.
- O menu principal mostra **Continuar** em primeiro quando há copa salva ("Copa Brasil — corrida 2
  de 3"). Ele abre o lobby no modo "Continuar": os mesmos N pilotos a partir do P1, com o nome e o
  carro da copa salva e o modo fixo; INICIAR corre a próxima pista com o mesmo elenco da IA.
- Começar outra copa pelo item Campeonato substitui a salva (uma copa em andamento por vez).
- Assentos com buraco no lobby (P1 e P3) viram contíguos (P1 e P2) antes de salvar, levando o
  controle junto (`compactHumans`), porque o "Continuar" sempre religa a partir do P1.

## Save

`SaveData` ganhou três campos, saneados em `src/game/career-save.ts` (chamado por `sanitizeSave`),
com a regra de sempre: lixo vira ausente, campo ruim é consertado, nunca lança.

- `carsUnlocked`: só ids de carros à venda, sem repetição.
- `career` (`CareerState`, ver `src/core/career.ts`): pilotos (máx. 4) com garagem (carros
  comprados, carro escolhido — cai num original se não for seu —, níveis presos de 0 ao teto da peça
  naquele carro, só de carros seus), carteiras coerentes com o modo (1 no co-op, 1 por piloto no
  versus), copa atual (copa desconhecida volta à primeira), copa em andamento (descartada se não bater
  com a atual, se já tiver acabado ou se não tiver corrida por correr — nesse caso a copa recomeça),
  tentativas, contagem de corridas e o relatório da última corrida. Sem piloto aproveitável → `null`.
- `cupInProgress` (`SavedCup`): descartado inteiro se a copa, a semente ou os humanos (assentos
  0..n-1) não fecharem.
- Copa (`ChampionshipState`) em andamento — nem concluída nem eliminada — com o índice da corrida no
  fim (`raceIndex` = número de pistas) é lixo: antes passava e prendia a carreira na garagem (cada
  PRONTO voltava para ela) e deixava "Continuar — corrida 4 de 3" para sempre no menu.

## Onde está cada coisa

- **Regras puras**: `src/core/career.ts` (economia, compras, avanço de copa, eliminação, nível da IA).
- **Núcleo**: `RaceConfig.humans[].upgrades` e `RaceConfig.aiLevel` entram na corrida; `createRace`
  calcula os atributos efetivos de cada carro (`src/core/sim/stats.ts`) e guarda em `CarState.stats`
  (JSON). Física, IA, colisões, co-op — e fora do núcleo o HUD, o áudio e a câmera — leem
  `car.stats`; cor e nome continuam do `CarDef`. Estado antigo sem `stats` ganha o padrão em
  `deserializeRace`. As cargas extras de nitro entram também no cofre de nitro da equipe.
  Determinismo: tudo é calculado da configuração, sem aleatoriedade nova (há teste com melhorias e
  IA evoluída).
- **Sessão**: `src/game/career-session.ts` (lobby → garagem → corrida → resultado); em
  `src/game/session.ts` só os ganchos (eventos `startCareer`, `careerRace`, `continueCup`, o fim da
  corrida no modo `career` e a gravação da copa normal).
- **Save**: `src/game/career-save.ts`.
- **Telas**: `src/ui/screens/garage.ts` + `garage.css` (tela Carreira e Garagem); itens no menu
  principal (`simple.ts`); modo carreira e "Continuar" no lobby (`lobby.ts`); botão GARAGEM na
  classificação (`results.ts`). Textos PT/EN em `src/career/strings.ts`.
- **Testes**: `tests/career.test.ts` (regras, núcleo, save; colisões e reboque com `car.stats`),
  `tests/career-session.test.ts` (a sessão de verdade com dublês de renderizador, entrada, áudio e
  menus: começar a copa grava, correr grava, reabrir e Continuar segue com o mesmo elenco, sair pela
  pausa mantém o índice; na carreira, da classificação volta à garagem) e
  `tests/career-balance.test.ts` (calibragem contra a IA com corridas inteiras).

## Fora desta versão

- **Senha** (o "senha/continuar" do roteiro): o save cobre o continuar; senha só faria sentido para
  levar a carreira a outra máquina, e o Steam Cloud (5.4) resolve isso melhor.
- Um espaço de carreira só; vender carro ou peça; a IA comprando carros novos; rival principal por
  copa (é o passo 3.4).
