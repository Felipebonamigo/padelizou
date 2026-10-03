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
  sete livres. O modo (co-op/versus) fica fixo pela carreira inteira. Dificuldade, câmbio,
  carros na pista e assistências continuam sendo as opções do lobby (valem para toda corrida, como
  no Campeonato); a carreira soma o nível da IA por cima da dificuldade.
- **Continuar carreira**: o lobby exige os mesmos N pilotos, sentados a partir do P1, com nome e
  modo fixos (vêm do save). Depois vai direto para a garagem.
- **Garagem**: um painel por piloto, cada um navegado pelo próprio controle (como no lobby).
  ←→ troca de carro (carro seu já fica escolhido; carro à venda aparece na vitrine com preço e
  quanto falta), Enter compra o carro ou a melhoria em foco, PRONTO trava o painel. Com todos
  prontos, a corrida começa. Esc do P1 volta ao menu (a carreira já está salva: cada compra grava).
  Com um piloto só, o botão é CORRER. Com 1 piloto o painel tem carro e atributos à esquerda e
  melhorias à direita (e a frase do carro embaixo dos atributos); com 2–4, uma coluna por piloto, que
  cabe sem rolar em 16:9, 16:10 e 4:3 — com 2, o painel de duas colunas cortava nomes de carro e de peça
  em toda resolução (onda F, cena `garage-2` do `playtest-layout.mjs`). Com 2–4 a vitrine mostra só o
  preço (em vermelho se não dá para comprar) e o carro não leva o contador "5/14".
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

### Carros (14)

Sete são de todos e sempre liberados: os quatro originais e três da onda F (Saci Mirim, Tatu 4x4,
Boto Luxo). Os outros sete se compram na carreira; comprado, o carro vai para a garagem daquele
piloto **e** fica liberado em todas as outras modalidades (`SaveData.carsUnlocked`, lobby, torneio e
online), sem as melhorias. A IA corre com os sete livres, nunca com um carro à venda. Atributos,
medidas e o porquê de cada número: `docs/CARROS.md`.

| Carro | Preço | Vel. máx. | Acel. | Freio | Curvas | Consumo | Troca |
|---|---|---|---|---|---|---|---|
| Falcão GT | — | 300 km/h | 700 | 2.600 | 0,75 | 1,00 | equilibrado |
| Trovão V12 | — | 318 | 640 | 2.400 | 0,60 | 1,25 | reta × curva e consumo |
| Tornado RS | — | 285 | 830 | 2.800 | 0,92 | 0,95 | curva e arrancada × reta |
| Camelo X | — | 293 | 700 | 2.600 | 0,70 | 0,68 | economia × resto |
| Saci Mirim | — | 276 | 800 | 3.000 | 0,96 | 0,55 | curva e economia × a menor reta |
| Tatu 4x4 | — | 306 | 820 | 2.300 | 0,66 | 1,35 | arrancada e reta × curva, freio e consumo |
| Boto Luxo | — | 306 | 600 | 2.200 | 0,70 | 0,80 | reta e economia × arrancada e freio |
| Curupira S | 12.000 | 291 | 800 | 3.200 | 0,90 | 0,80 | o melhor freio e curva × reta |
| Sucuri E | 16.000 | 309 | 640 | 2.700 | 0,80 | 0,50 | tanque para a corrida toda × arrancada |
| Carcará RS | 20.000 | 294 | 900 | 3.000 | 0,97 | 1,05 | arrancada e curva × fim de reta |
| Pororoca V10 | 22.000 | 336 | 660 | 2.500 | 0,58 | 1,40 | a maior reta × curva e consumo |
| Iara Turbo | 24.000 | 327 | 700 | 2.300 | 0,62 | 0,85 | reta quase do Pororoca bebendo pouco × curva e freio |
| Boitatá GT | 30.000 | 321 | 780 | 2.800 | 0,84 | 0,90 | forte em tudo × caro |
| Beija-Flor | 40.000 | 312 | 800 | 3.100 | 1,00 | 1,35 | a melhor curva × consumo, reta e o preço |

Cada carro à venda é o melhor (ou empatado) dos livres em algum atributo e perde para um livre em
outro (há teste; o freio conta, é o trunfo do Curupira). Nomes inventados, sem marca, no tema de
bicho e lenda brasileira. O Pororoca se chamou "Furacão V10" até a revisão: é a tradução literal do
Lamborghini Huracán V10. Um teste recusa, nos carros à venda e nos da onda F, nome de marca, de
modelo ou a tradução dele (Jacaré = Cayman, Arraia = Stingray…).

**A vitrine da onda F.** O Curupira S ($ 12.000) é o primeiro degrau: o piloto médio (sempre 4º)
junta $ 13.700 na 1ª copa ($ 2.500 + 4 × $ 2.800) e o compra ali, sem gastar em mais nada; antes,
o degrau mais baixo era o Sucuri ($ 16.000), uma corrida depois. A Iara Turbo ($ 24.000) fica entre o
Pororoca e o Boitatá; o Beija-Flor ($ 40.000) é o topo para a carreira de 8 copas, que paga mais que
a de 4 para a qual os preços antigos foram feitos (o piloto médio junta ~$ 125 mil em 32 corridas;
quem vence sempre, o dobro). A ordem da vitrine na garagem (←→) é a de preço.

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
para no nível 2, o **Carcará RS** (0,97 → 1,00) e o **Saci Mirim** (0,96 → 1,00) no nível 1, e o
**Beija-Flor** (1,00) já nasce no teto: pneus não se vendem para ele. Antes da revisão a garagem cobrava
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

### Onda G: 34 copas, 109 corridas (02/10/2026)

A carreira passa por todas as copas da lista: a Expedição Brasil (27 estados × 3 corridas) e o Mundial (7 × 4) —
**109 corridas** em vez de 32. O prêmio continua pela posição relativa da copa (`prizeMultiplier`), então o piloto
médio (sempre 4º, compra a peça mais barata) junta ~$ 426.000 na carreira e compra **102 itens em 109 corridas
(0,94 por corrida)**; ainda há o que comprar no Mundial (teste "catálogo de verdade" em `tests/career.test.ts`).
O Falcão dele fecha as 18 melhorias por volta da 8ª copa (DF).

**O nível da IA deixou de ser linear.** Medido com a sonda (piloto 0,97, 2 sementes, todas as pistas da copa), a
rampa antiga (0 → 1,25, linear nas 34 copas) deixava a IA muito atrás: o piloto médio vencia **todas** as corridas
do RS em diante (média 1,0 até no Mediterrâneo), porque com 109 corridas ele tem o Falcão no máximo desde a 8ª copa.
Mesmo com a IA no teto (nível 3 = motor, turbo, pneus e freios no máximo em todos os carros; `clampLevel` não
passa disso) o Falcão no máximo do piloto médio fica em ~2º na última copa: **a medida atual é bem mais fácil para
o jogador do que a tabela de 26/09 dizia** (4,7 na Europa com 1,25), provavelmente pelas mudanças de física e
colisão das ondas seguintes. Agora:

- `CAREER_AI_LEVEL_MAX` = **3** (o teto das melhorias) na última copa;
- entre a primeira (0) e a última, `careerAiLevel` = máximo × (`CAREER_AI_FOLLOW` (0,8) × fração das melhorias do
  Falcão que o piloto médio já comprou + 0,2 × posição da copa). A IA acompanha o dinheiro dele em vez de chegar
  20 copas atrasada, e sobe sempre um pouco (o teste exige subida estrita).

Sonda (`npx tsx scripts/career-balance.ts`, aqui com 2 sementes; posição média e corridas no top 5):

| Copa | IA | Melhorias do piloto | Posições | Média | Top 5 |
|---|---|---|---|---|---|
| br_rj | 0,00 | 000000 | 2,3,2,4,7,3 | 3,50 | 5/6 |
| br_sp | 0,68 | 011111 | 2,2,3,5,5,6 | 3,83 | 5/6 |
| br_mg | 1,10 | 111221 | 2,2,2,4,5,4 | 3,17 | 6/6 |
| br_es | 1,52 | 122231 | 2,2,2,5,4,3 | 3,00 | 6/6 |
| br_pr | 1,81 | 222232 | 2,2,2,4,3,3 | 2,67 | 6/6 |
| br_sc | 2,09 | 232332 | 2,2,2,3,3,4 | 2,67 | 6/6 |
| br_rs | 2,24 | 233332 | 2,2,2,4,3,3 | 2,67 | 6/6 |
| br_df | 2,53 | 333333 | 2,1,1,3,1,2 | 1,67 | 6/6 |
| br_go | 2,55 | 333333 | 1,1,1,3,2,3 | 1,83 | 6/6 |
| br_ms | 2,56 | 333333 | 1,1,2,2,2,2 | 1,67 | 6/6 |
| br_mt | 2,58 | 333333 | 2,1,1,3,2,2 | 1,83 | 6/6 |
| br_ba | 2,60 | 333333 | 2,1,1,2,2,2 | 1,67 | 6/6 |
| br_se | 2,62 | 333333 | 2,1,2,2,2,2 | 1,83 | 6/6 |
| br_al | 2,64 | 333333 | 2,1,1,3,1,2 | 1,67 | 6/6 |
| br_pe | 2,65 | 333333 | 1,2,1,1,1,2 | 1,33 | 6/6 |
| br_pb | 2,67 | 333333 | 1,1,1,2,1,1 | 1,17 | 6/6 |
| br_rn | 2,69 | 333333 | 2,1,1,3,2,1 | 1,67 | 6/6 |
| br_ce | 2,71 | 333333 | 1,1,2,2,1,2 | 1,50 | 6/6 |
| br_pi | 2,73 | 333333 | 2,2,1,2,2,3 | 2,00 | 6/6 |
| br_ma | 2,75 | 333333 | 1,1,1,1,3,3 | 1,67 | 6/6 |
| br_pa | 2,76 | 333333 | 1,1,1,2,2,2 | 1,50 | 6/6 |
| br_am | 2,78 | 333333 | 2,1,1,2,1,1 | 1,33 | 6/6 |
| br_ap | 2,80 | 333333 | 2,2,2,2,2,3 | 2,17 | 6/6 |
| br_rr | 2,82 | 333333 | 1,1,1,3,4,1 | 1,83 | 6/6 |
| br_ro | 2,84 | 333333 | 1,1,2,3,2,2 | 1,83 | 6/6 |
| br_ac | 2,85 | 333333 | 2,1,2,2,2,2 | 1,83 | 6/6 |
| br_to | 2,87 | 333333 | 2,1,1,4,2,4 | 2,33 | 6/6 |
| eua | 2,89 | 333333 | 2,2,2,2,5,3,4,3 | 2,88 | 8/8 |
| japao | 2,91 | 333333 | 2,2,2,1,3,3,2,2 | 2,13 | 8/8 |
| europa | 2,93 | 333333 | 1,2,2,2,3,3,4,3 | 2,50 | 8/8 |
| africa_do_sul | 2,95 | 333333 | 2,2,2,1,3,3,2,2 | 2,13 | 8/8 |
| australia | 2,96 | 333333 | 2,1,1,2,3,2,5,2 | 2,25 | 8/8 |
| escandinavia | 2,98 | 333333 | 1,1,1,1,3,2,4,3 | 2,00 | 8/8 |
| mediterraneo | 3,00 | 333333 | 1,1,1,2,2,6,4,2 | 2,38 | 7/8 |

Linear 0 → 1,25 (antes), mesmas sementes: RJ 2,67 · MG 1,67 · RS 1,00 · MT 1,00 · PE 1,00 · CE 1,00 · PA 1,00 ·
TO 1,00 · Europa 1,25 · Mediterrâneo 1,00. Com o máximo fixo em 2 ou 2,5 o Mediterrâneo continuava em 1,0–1,4.

Leitura: o piloto médio fica entre 3º e 4º nas 7 primeiras copas (sem o Falcão completo) e entre 1º e 2º do DF
em diante — a IA já está quase no teto e não tem mais o que comprar. Para a segunda metade ficar mais dura do que
isso, a alavanca não é mais `aiLevel` (está no teto): seria habilidade da IA por copa, ou melhorias que passem do
nível 3 — decisão de design em aberto. Anotar também: a âncora (RJ, de fábrica, IA 0) deu 3,5, dentro da faixa do
teste (2,5–5,5); o `PROXY_SKILL` 0,97 continua valendo.

### Dinheiro e rivais nas 34 copas, medidos (03/10/2026)

`npx tsx scripts/career-balance.ts` agora imprime primeiro a economia (instantânea, sem corrida) e depois a sonda;
`sementes=0` só a economia, e dá para sondar outra dificuldade a partir de uma copa
(`npx tsx scripts/career-balance.ts 0.97 2 - campeao br_ba`). A sonda no profissional repetiu a tabela de 02/10
número por número. A de baixo junta tudo — prêmio da copa para quem chega sempre em 4º, o acumulado guardando tudo,
as melhorias do Falcão do piloto médio (a peça mais barata depois de cada corrida) no início da copa, o nível da
IA, e a posição média do piloto médio (0,97, 2 sementes × as pistas da copa) no profissional e no campeão. PE, MA e
AM já com as 3 voltas novas de Recife, São Luís e Ponte do Rio Negro (`docs/PISTAS.md`).

| # | Copa | × prêmio | 4º na copa | Acumulado (4º, guardando) | Melhorias do piloto médio | IA | Profissional: média · top 5 | Campeão: média · top 5 |
|---|---|---|---|---|---|---|---|---|
| 1 | br_rj | 1,00 | $ 8.400 | $ 10.900 | 0/18 | 0,00 | 3,50 · 5/6 | 9,67 · 1/6 |
| 2 | br_sp | 1,02 | $ 8.580 | $ 19.480 | 5/18 | 0,68 | 3,83 · 5/6 | 12,00 · 0/6 |
| 3 | br_mg | 1,05 | $ 8.790 | $ 28.270 | 8/18 | 1,10 | 3,17 · 6/6 | 7,00 · 2/6 |
| 4 | br_es | 1,07 | $ 8.970 | $ 37.240 | 11/18 | 1,52 | 3,00 · 6/6 | 8,83 · 0/6 |
| 5 | br_pr | 1,09 | $ 9.150 | $ 46.390 | 13/18 | 1,81 | 2,67 · 6/6 | 5,83 · 4/6 |
| 6 | br_sc | 1,11 | $ 9.360 | $ 55.750 | 15/18 | 2,09 | 2,67 · 6/6 | 6,17 · 2/6 |
| 7 | br_rs | 1,14 | $ 9.540 | $ 65.290 | 16/18 | 2,24 | 2,67 · 6/6 | 6,00 · 3/6 |
| 8 | br_df | 1,16 | $ 9.750 | $ 75.040 | 18/18 | 2,53 | 1,67 · 6/6 | 2,83 · 6/6 |
| 9 | br_go | 1,18 | $ 9.930 | $ 84.970 | 18/18 | 2,55 | 1,83 · 6/6 | 3,50 · 6/6 |
| 10 | br_ms | 1,20 | $ 10.110 | $ 95.080 | 18/18 | 2,56 | 1,67 · 6/6 | 3,67 · 6/6 |
| 11 | br_mt | 1,23 | $ 10.320 | $ 105.400 | 18/18 | 2,58 | 1,83 · 6/6 | 4,00 · 5/6 |
| 12 | br_ba | 1,25 | $ 10.500 | $ 115.900 | 18/18 | 2,60 | 1,67 · 6/6 | 3,33 · 6/6 |
| 13 | br_se | 1,27 | $ 10.680 | $ 126.580 | 18/18 | 2,62 | 1,83 · 6/6 | 3,17 · 5/6 |
| 14 | br_al | 1,30 | $ 10.890 | $ 137.470 | 18/18 | 2,64 | 1,67 · 6/6 | 3,67 · 4/6 |
| 15 | br_pe | 1,32 | $ 11.070 | $ 148.540 | 18/18 | 2,65 | 1,50 · 6/6 | 3,33 · 6/6 |
| 16 | br_pb | 1,34 | $ 11.250 | $ 159.790 | 18/18 | 2,67 | 1,17 · 6/6 | 3,17 · 6/6 |
| 17 | br_rn | 1,36 | $ 11.460 | $ 171.250 | 18/18 | 2,69 | 1,67 · 6/6 | 3,33 · 6/6 |
| 18 | br_ce | 1,39 | $ 11.640 | $ 182.890 | 18/18 | 2,71 | 1,50 · 6/6 | 3,67 · 5/6 |
| 19 | br_pi | 1,41 | $ 11.850 | $ 194.740 | 18/18 | 2,73 | 2,00 · 6/6 | 5,00 · 3/6 |
| 20 | br_ma | 1,43 | $ 12.030 | $ 206.770 | 18/18 | 2,75 | 1,83 · 6/6 | 4,67 · 4/6 |
| 21 | br_pa | 1,45 | $ 12.210 | $ 218.980 | 18/18 | 2,76 | 1,50 · 6/6 | 6,33 · 3/6 |
| 22 | br_am | 1,48 | $ 12.420 | $ 231.400 | 18/18 | 2,78 | 1,50 · 6/6 | 5,67 · 3/6 |
| 23 | br_ap | 1,50 | $ 12.600 | $ 244.000 | 18/18 | 2,80 | 2,17 · 6/6 | 5,33 · 3/6 |
| 24 | br_rr | 1,52 | $ 12.780 | $ 256.780 | 18/18 | 2,82 | 1,83 · 6/6 | 5,33 · 3/6 |
| 25 | br_ro | 1,55 | $ 12.990 | $ 269.770 | 18/18 | 2,84 | 1,83 · 6/6 | 6,17 · 3/6 |
| 26 | br_ac | 1,57 | $ 13.170 | $ 282.940 | 18/18 | 2,85 | 1,83 · 6/6 | 4,67 · 5/6 |
| 27 | br_to | 1,59 | $ 13.350 | $ 296.290 | 18/18 | 2,87 | 2,33 · 6/6 | 5,67 · 4/6 |
| 28 | eua | 1,61 | $ 18.080 | $ 314.370 | 18/18 | 2,89 | 2,88 · 8/8 | 6,13 · 3/8 |
| 29 | japao | 1,64 | $ 18.320 | $ 332.690 | 18/18 | 2,91 | 2,13 · 8/8 | 5,38 · 5/8 |
| 30 | europa | 1,66 | $ 18.600 | $ 351.290 | 18/18 | 2,93 | 2,50 · 8/8 | 6,38 · 3/8 |
| 31 | africa_do_sul | 1,68 | $ 18.840 | $ 370.130 | 18/18 | 2,95 | 2,13 · 8/8 | 4,88 · 7/8 |
| 32 | australia | 1,70 | $ 19.080 | $ 389.210 | 18/18 | 2,96 | 2,25 · 8/8 | 6,50 · 4/8 |
| 33 | escandinavia | 1,73 | $ 19.360 | $ 408.570 | 18/18 | 2,98 | 2,00 · 8/8 | 5,25 · 5/8 |
| 34 | mediterraneo | 1,75 | $ 19.600 | $ 428.170 | 18/18 | 3,00 | 2,38 · 7/8 | 8,00 · 1/8 |

Quem chega sempre em 1º junta $ 914.630 na carreira; em 8º, $ 154.520. Carros à venda — em que copa o preço cabe:

| Carro | Preço | 1º, guardando | 4º, guardando | 4º, depois de completar o Falcão | Melhorias todas | Carro + melhorias, 4º guardando |
|---|---|---|---|---|---|---|
| Curupira S | $ 12.000 | RJ (corrida 2) | SP (4) | GO (25) | $ 63.900 | GO (25) |
| Sucuri E | $ 16.000 | RJ (3) | SP (5) | GO (26) | $ 63.900 | GO (26) |
| Carcará RS | $ 20.000 | RJ (3) | MG (7) | GO (27) | $ 54.900 | DF (24) |
| Pororoca V10 | $ 22.000 | SP (4) | MG (7) | MS (28) | $ 63.900 | MS (28) |
| Iara Turbo | $ 24.000 | SP (4) | MG (8) | MS (28) | $ 63.900 | MS (28) |
| Boitatá GT | $ 30.000 | SP (5) | ES (10) | MS (30) | $ 63.900 | MS (30) |
| Beija-Flor | $ 40.000 | MG (7) | PR (13) | MT (33) | $ 52.900 | MS (30) |

Nada está fora de alcance: o piloto médio completa o Falcão ao fim do RS (copa 7, corrida 21) e, depois disso, compra
qualquer carro à venda até o MT (copa 11, corrida 33 de 109). Da 12ª copa em diante o dinheiro não tem mais o que
mudar no carro dele: ele junta mais $ 323.000 até o fim, o bastante para colecionar a vitrine inteira com melhorias.
Nenhuma constante de economia mudou (nada quebrado; é a carreira longa).

**A segunda metade, em números (decisão do dono, `CAREER_AI_LEVEL_MAX` não mudou):**

- No profissional, o piloto médio fica em 2,7–3,8 nas 7 primeiras copas (o que a economia supõe, ~4º) e em
  **1,2–2,9 do DF ao Mediterrâneo**, no top 5 em 175 das 176 corridas da sonda. Do DF em diante a IA sobe só de 2,53
  para 3,00 em 27 copas, e o Falcão dele está completo desde o DF: nada mais muda dos dois lados, e as pistas mais
  difíceis sozinhas não seguram (em Palmas, a mais dura do Brasil, ele chega em 1º e 2º).
- **Subir `CAREER_AI_LEVEL_MAX` não muda nada**: o nível já está em 2,5–3,0 desde o DF, e `clampLevel` corta em 3
  (`UPGRADE_MAX_LEVEL`). Medido com o máximo em 4 (nível 3,85 → cortado em 3): EUA 2,88 e Japão 2,13 — os mesmos
  números do máximo 3.
- O que mexe é o ritmo da IA. No **campeão**, a mesma sonda dá 5,8–12,0 nas 7 primeiras copas (duro demais para ele),
  2,8–4,0 do DF ao Ceará, 4,7–6,3 do Piauí ao Tocantins e 4,9–6,5 no Mundial, com o Mediterrâneo em 8,0 (1 corrida
  em 8 no top 5).

Recomendação: deixar `CAREER_AI_LEVEL_MAX` em 3 (é o teto efetivo) e, se a segunda metade tiver de pesar, dar à
carreira um ritmo de IA que sai do profissional no DF e chega perto do campeão no Mundial. O que está medido é o
campeão inteiro na segunda metade: o piloto médio volta a ~3º–6,5º do DF à Escandinávia, perto do 4º da economia. A
rampa em si não foi medida, e o Mediterrâneo no campeão inteiro (8,0) passa do ponto: o topo dela fica um pouco abaixo
do campeão, ou o fim é conferido de novo com a sonda. Sem mudança, a segunda metade é uma
volta da vitória (1º–2º); quem quiser mais já pode escolher o campeão no lobby, mas aí paga com uma primeira metade
dura (5,8–12,0). Melhorias da IA acima do nível 3 seriam a outra alavanca: não medida, e pede constantes novas.

**Carreira de antes da onda G parada no Mundial**: o nível da IA salta (EUA: 0,18 → 2,89) e o piloto médio com o
Falcão 111111 de então vai de 1,75 para 9,38 de média (`docs/SAVE.md`, "Save de antes da onda G").

## Rivais que evoluem

(Até a onda G; a regra de agora está em "Onda G" acima.) O nível da IA ia de 0 na primeira copa a `CAREER_AI_LEVEL_MAX` = **1,25** na última, linear entre
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
  tentativas, contagem de corridas e o relatório da última corrida (o de uma copa que não existe mais sai: save de
  antes da onda G, `docs/SAVE.md`). Sem piloto aproveitável → `null`.
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
- Um espaço de carreira só; vender carro ou peça; a IA comprando carros novos. (O rival principal por copa
  entrou no passo 3.4 — `docs/RIVAIS.md`.)
