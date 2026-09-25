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
| Furacão V10 | 22.000 | 336 | 660 | 2.500 | 0,58 | 1,40 | a maior reta × curva e consumo |
| Boitatá GT | 30.000 | 321 | 780 | 2.800 | 0,84 | 0,90 | forte em tudo × o mais caro |

Cada carro novo é o melhor (ou empatado) em algum atributo e perde para um original em outro (há
teste). Nomes inventados, sem marca.

### Melhorias (por carro, nível 0–3)

| Peça | Efeito por nível | Nível 1 / 2 / 3 | Total |
|---|---|---|---|
| Motor | velocidade máxima +2,5% | 2.400 / 4.200 / 6.600 | 13.200 |
| Turbo | aceleração +8% | 2.000 / 3.500 / 5.500 | 11.000 |
| Pneus | dirigibilidade +0,04 (teto 1,0) | 2.000 / 3.500 / 5.500 | 11.000 |
| Freios | frenagem +12% | 1.600 / 2.800 / 4.400 | 8.800 |
| Tanque | consumo −10% | 1.400 / 2.500 / 3.800 | 7.700 |
| Nitro | +1 carga (3 → 6) | 2.200 / 3.900 / 6.100 | 12.200 |

Um carro completo custa $ 63.900 — de propósito, mais do que um piloto médio junta na carreira:
é preciso escolher. As melhorias são **do carro**: trocar de carro não leva as peças (o carro novo
começa no nível 0), o que dá peso à decisão de comprar um carro caro no meio da carreira.

Compra recusada não muda nada e diz por quê: sem saldo, nível máximo, carro já possuído, carro que
não é seu (`PurchaseResult`).

### Calibragem

Meta: um piloto médio compra 1–2 itens por corrida e chega ao fim da carreira. Um piloto que chega
sempre em 4º junta ~$ 46.000 em 4 copas (12 corridas) além dos $ 2.500 iniciais — ~$ 3.900 por
corrida, contra itens de $ 1.400 a $ 6.600. O teste "calibragem" simula comprar sempre o item mais
barato e exige de 1 a 2 itens por corrida com 4 e com 8 copas (o multiplicador de prêmio depende da
posição relativa da copa, não da quantidade). Quem vence sempre junta o dobro e pode trocar de carro.

## Rivais que evoluem

O nível da IA vai de 0 na primeira copa a 3 na última, linear entre elas (`careerAiLevel`, pode ser
fracionário). Ele aplica motor, turbo, pneus e freios no mesmo nível a todos os carros da IA (tanque
e nitro ficam de fábrica). O nível 3 da IA equivale a $ 44.000 de melhorias por carro — mais ou
menos o que o piloto médio compra na carreira inteira, então a última copa pede um carro bem
montado. Fora da carreira `aiLevel` é ausente (0) e nada muda.

## Eliminação

A mesma regra da copa normal: co-op passa se a equipe ficar entre as 3 melhores equipes da corrida;
versus/solo passa se algum humano chegar no top 5. Eliminado:

- **a corrida que eliminou não paga** (nem prêmio nem bônus); o que já foi ganho fica, e as compras
  também;
- **a copa recomeça da primeira pista** (tentativa 2, 3, …, sem limite e sem taxa), com a IA no
  mesmo nível;
- a garagem abre com a faixa "ELIMINADO" e dá para gastar antes de tentar de novo.

Por que assim: perder a carreira inteira numa corrida ruim é duro demais para jogar no sofá com
crianças; refazer a copa sem o prêmio custa tempo e dinheiro, que é punição suficiente.

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
  comprados, carro escolhido — cai num original se não for seu —, níveis presos a 0–3, só de carros
  seus), carteiras coerentes com o modo (1 no co-op, 1 por piloto no versus), copa atual (copa
  desconhecida volta à primeira), copa em andamento (descartada se não bater com a atual ou já tiver
  acabado), tentativas, contagem de corridas e o relatório da última corrida. Sem piloto
  aproveitável → `null`.
- `cupInProgress` (`SavedCup`): descartado inteiro se a copa, a semente ou os humanos (assentos
  0..n-1) não fecharem.

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
- **Testes**: `tests/career.test.ts`.

## Fora desta versão

- **Senha** (o "senha/continuar" do roteiro): o save cobre o continuar; senha só faria sentido para
  levar a carreira a outra máquina, e o Steam Cloud (5.4) resolve isso melhor.
- Um espaço de carreira só; vender carro ou peça; a IA comprando carros novos; rival principal por
  copa (é o passo 3.4).
