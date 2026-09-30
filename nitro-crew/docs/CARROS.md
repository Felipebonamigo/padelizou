# Carros

## Os carros

Catorze carros, pelo menos um por estilo de carroceria (`CarDef.body`, 13 estilos em `CAR_BODIES`;
o Boitatá divide o `gt` com o Falcão). Sete são de todos desde o começo e sete se compram na
carreira (`docs/CARREIRA.md`); comprado, o carro fica liberado no lobby, no torneio e no online. Os
dados moram em `src/core/data/cars.ts`; os textos em PT e EN em `src/i18n/core.ts` (livres) e
`src/career/strings.ts` (à venda). Nenhum carro é melhor em tudo, e os números saíram de medida
(`npx tsx scripts/car-balance.ts`), não de chute.

### A tabela

Velocidade em km/h (300 = `REFERENCE_SPEED`); consumo relativo ao Falcão (1,00 = um tanque dura
~2,4 voltas de 400.000 unidades a fundo). **Solo**: o tempo da corrida inteira, sozinho na pista, com
combustível e box, relativo ao do Falcão GT, na média das 32 pistas (menor = mais rápido; entre
parênteses, em quantas pistas foi o mais rápido de todos). **Grade**: posição média de chegada numa
corrida de 20 carros no profissional contra a IA (16 pistas × 2 sementes). O piloto é o mesmo em
todos os carros: o cérebro da IA no assento humano (habilidade 1 no solo, 0,97 na grade — o
"piloto médio" da carreira).

| Carro | Estilo | Preço | Vel. | Acel. | Freio | Curvas | Consumo | Solo | Grade | O porquê |
|---|---|---|---|---|---|---|---|---|---|---|
| Falcão GT | gt | — | 300 | 700 | 2.600 | 0,75 | 1,00 | 1,000 | 4,19 | a régua: equilibrado |
| Trovão V12 | muscle | — | 318 | 640 | 2.400 | 0,60 | 1,25 | 1,017 | 6,38 | reta × curva e consumo |
| Tornado RS | hatch | — | 285 | 830 | 2.800 | 0,92 | 0,95 | 0,984 | 4,91 | arrancada e curva × reta |
| Camelo X | sedan | — | 293 | 700 | 2.600 | 0,70 | 0,68 | 1,025 | 8,88 | economia × resto (ver "Achados") |
| **Saci Mirim** | micro | — | 276 | 800 | 3.000 | 0,96 | 0,55 | 0,991 | 8,28 | curva, freio e economia × a menor reta do jogo |
| **Tatu 4x4** | pickup | — | 306 | 820 | 2.300 | 0,66 | 1,35 | 1,012 | 6,44 | arranca e retoma × curva, freio e sede |
| **Boto Luxo** | classic | — | 306 | 600 | 2.200 | 0,70 | 0,80 | 1,003 | 4,13 | reta e economia × a pior arrancada e o pior freio |
| **Curupira S** | roadster | 12.000 | 291 | 800 | 3.200 | 0,90 | 0,80 | 0,965 | 3,09 | o melhor freio e boa curva × reta |
| Sucuri E | electric | 16.000 | 309 | 640 | 2.700 | 0,80 | 0,50 | 0,949 | 1,84 | tanque para a corrida toda × arrancada |
| Carcará RS | rally | 20.000 | 294 | 900 | 3.000 | 0,97 | 1,05 | 0,944 | 2,44 | arrancada e curva × fim de reta |
| Pororoca V10 | hyper | 22.000 | 336 | 660 | 2.500 | 0,58 | 1,40 | 0,981 (2) | 3,38 | a maior reta × curva e consumo |
| **Iara Turbo** | wedge | 24.000 | 327 | 700 | 2.300 | 0,62 | 0,85 | 0,967 (3) | 2,25 | quase a reta do Pororoca bebendo 40% menos × curva e freio |
| Boitatá GT | gt | 30.000 | 321 | 780 | 2.800 | 0,84 | 0,90 | 0,905 (6) | 1,00 | forte em tudo × caro |
| **Beija-Flor** | prototype | 40.000 | 312 | 800 | 3.100 | 1,00 | 1,35 | 0,899 (21) | 1,47 | a melhor curva × sede, reta e preço |

Em negrito, os seis da onda F. Cada um é o melhor ou está entre os melhores em alguma coisa e paga por
isso em outra:

- **Saci Mirim** (micro, livre): o carro pequeno — responde ao volante como poucos (0,96), freia forte e
  quase não bebe (0,55: nenhuma parada em corrida nenhuma), mas é o mais lento na reta (276 km/h). No
  solo é o 2º livre mais rápido e o melhor dos livres nas pistas mais travadas (Roma, Boa Esperança,
  Sydney, Tromsø, Osaka); na grade sofre, porque não passa ninguém na reta. É o carro de quem está
  aprendendo: perdoa o erro na curva e nunca pede box.
- **Tatu 4x4** (picape, livre): parrudo — arrancada de 820 (só o Tornado arranca mais entre os livres),
  boa reta (306), mas pesado: o freio e a dirigibilidade estão entre os piores e bebe 35% mais que o
  Falcão (até duas paradas onde o Falcão faz uma). "Retoma forte" é a aceleração: depois de uma
  pancada ou da grama ele volta ao ritmo antes dos outros. Fica no meio dos livres nas duas medidas:
  anda mais que o Falcão nas pistas de reta e menos nas travadas, sem ser o melhor livre em nenhuma.
- **Boto Luxo** (clássico, livre): o cruzador — reta boa (306) e econômico (0,80: nas pistas de 3
  voltas só para nas mais longas), com a pior arrancada (600) e o pior freio (2.200) do jogo. É o
  melhor livre nas pistas de reta com curvas abertas (Serra do Mar, Baía de Tóquio), 2º do Trovão nas
  de reta pura, e perde nas travadas.
- **Curupira S** (roadster, $ 12.000): o primeiro degrau da vitrine — leve, o melhor freio do jogo
  (3.200) e curva boa (0,90), pouca reta (291) e consumo baixo. Anda mais que todo livre na média, sem
  ganhar de todos em toda pista (perde do Trovão, do Boto e do Tatu nas retas longas).
- **Iara Turbo** (cunha, $ 24.000): corta o ar — a segunda maior reta do jogo (327 km/h) bebendo 40% menos
  que o Pororoca (0,85 contra 1,40: uma parada a menos na maioria das pistas), arisca na curva (0,62)
  e freio fraco (2.300). Divide com o Pororoca as pistas de reta (cada um é o mais rápido de todos em
  2–3 delas) e é mais constante que ele nas outras; por isso custa $ 2.000 a mais. Com 330 km/h (1,10)
  e aceleração 720 ela ganhava do Pororoca em toda pista (o Pororoca ficava inútil); com 327 e 700 ele
  segue dono de algumas retas.
- **Beija-Flor** (protótipo, $ 40.000): o topo — a melhor curva do jogo (1,00, já no teto: pneus não se
  vendem), freio forte e boa arrancada; bebe muito (1,35) e na reta perde para o Pororoca, a Iara, o
  Boitatá e o Trovão. Divide as pistas com o Boitatá: no solo ele é o mais rápido de todos em 21 das 32
  (as de curva), o Boitatá nas de reta; na grade o Boitatá segue à frente. Com 315 km/h (1,05),
  aceleração de 820 e consumo 1,30 ele era o mais rápido em 25 pistas e o Boitatá de $ 30.000 perdia o
  sentido.

### Livres e à venda: por quê

Até aqui eram 4 + 4; a onda F põe mais 3 + 3 (7 livres, 7 à venda):

- **Livres (micro, picape, clássico)**: o pedido do dono é variedade ("mais opções de carro", "estão
  todos iguais"), e a variedade que conta é a da corrida rápida, do co-op no sofá e da grade da IA —
  que só usa carro livre. Os três ficam **na faixa dos livres originais** nas duas medidas (solo entre
  0,984 e 1,025, com teste; grade de 4,1 a 8,9 — o Boto empata com o Falcão, 4,13 contra 4,19, dentro
  do ruído de 32 corridas), então nenhum deles vira a escolha óbvia nem a inútil, e a IA com eles segue
  no mesmo nível (ver abaixo). São carros de personagem, com uma fraqueza clara.
- **À venda (roadster, cunha, protótipo)**: os estilos "de desejo" viram metas da carreira, e cada um
  preenche um vão da curva de preço: um degrau antes do Sucuri ($ 12 mil), um entre o Pororoca e o
  Boitatá ($ 24 mil) e um topo acima do Boitatá ($ 40 mil) para a carreira de 8 copas, que paga ~$ 125
  mil ao piloto médio (os preços antigos são da carreira de 4 copas). Na média, cada um anda mais que
  todo livre; nenhum ganha de um carro mais caro em todas as pistas.

**A curva de preço**, na média das duas medidas, sobe com o preço entre os carros da onda F e encaixa
entre os antigos: Curupira ($ 12 mil) acima de todo livre e abaixo do Sucuri e do Carcará; Iara ($ 24
mil) acima do Pororoca ($ 22 mil) e abaixo do Boitatá; Beija-Flor ($ 40 mil) empatado com o Boitatá na
soma (melhor no solo, pior na grade) — o preço paga a especialidade, não um carro melhor em tudo. Os
antigos já não formavam curva: o Sucuri de $ 16 mil anda mais que o Pororoca de $ 22 mil nas duas
medidas e que o Carcará de $ 20 mil na grade; o Pororoca, dono das retas, fica na média abaixo até do
Curupira (ver "Achados").

### A IA com os carros novos

A IA sorteia o carro entre os livres (`AI_CAR_POOL`, `price === 0`): com a onda F, 7 em vez de 4, e a
grade mostra 7 estilos de carroceria em vez de 4. Medido antes de decidir:

| IA com | Volta média da IA (`npm run balance -- 150 profissional 11`) |
|---|---|
| os 4 originais (antes) | 86,20 s |
| originais + Saci | 86,65 s |
| originais + Tatu | 86,37 s |
| originais + Boto | 86,54 s |
| **os 7 livres (agora)** | **87,33 s** (+1,3%) |

A volta média da IA fica ~1 s mais lenta. Cada carro novo sozinho já pesa (o Saci mais, o Tatu menos),
e a hipótese é o pelotão: com perfis diferentes, quem vem atrás fica mais tempo preso (o Saci é o mais
lento na reta, o Boto o mais lento na saída de curva). Quem decide a dificuldade, porém, é a frente do
pelotão contra o jogador, e a sonda da carreira (piloto médio, 3 sementes × 4 pistas por copa) ficou no
ruído de 12 corridas — a maior diferença, no Brasil, vai no sentido de a IA ficar um pouco mais
difícil, não mais fácil:

| Copa | Brasil | EUA | Japão | Europa | África do Sul | Austrália | Escandinávia | Mediterrâneo |
|---|---|---|---|---|---|---|---|---|
| IA com os 4 originais | 3,67 | 1,92 | 1,25 | 1,25 | 1,00 | 1,00 | 1,17 | 1,00 |
| IA com os 7 livres | 4,58 | 1,67 | 1,17 | 1,25 | 1,08 | 1,00 | 1,00 | 1,00 |

(`npx tsx scripts/career-balance.ts 0.97 3`; posição média do piloto médio.) `tests/career-balance.test.ts`
passa. `tests/sim-golden.test.ts` foi regravado: toda corrida com IA mudou de elenco; o contra-relógio
(sem IA) ficou bit a bit igual, então `PHYSICS_REVISION` não sobe. `tests/ai.test.ts` (nenhum carro da IA
seca o tanque em nenhuma pista) passa com o Tatu, o mais sedento da IA agora (1,35).

### A escolha de carro com 14

- **Lobby**: ←→ percorre os livres e os comprados, com "5/14" embaixo do desenho (com 14, quem escolhe
  quer saber onde está; ao lado do nome, o "POROROCA V10" não cabia na grade 2×2); a silhueta muda com
  o estilo.
- **Garagem**: a vitrine percorre os 14 em ordem (livres, depois do mais barato ao mais caro). Com um
  piloto, o contador ao lado do nome e a frase do carro (a troca dele) embaixo dos atributos; com 2–4,
  uma coluna por piloto e a etiqueta só com o preço (`docs/CARREIRA.md`, `docs/TELAS.md`).
- **Online**: oferecia os 14 a qualquer um, inclusive os 7 à venda nunca comprados (defeito da carreira,
  que nasceu com o online já pronto). Agora oferece os mesmos do lobby (`carAvailable` em
  `src/game/career-save.ts`); o carro que chega de outro computador continua valendo qualquer um
  (`tests/online-cars.test.ts`).
- **Torneio**: usa a lista do lobby.

### Silhuetas 2D

`carSilhouette(car)` (`src/ui/screens/icons.ts`) desenha um perfil por estilo, na cor do carro e com a
cor de destaque (`accent`; sem ela, um tom mais escuro da cor). Frente à direita, linhas retas e
faces planas como o low-poly do jogo. O que distingue cada um de perfil:

| Estilo | Perfil |
|---|---|
| gt | capô longo, cabine recuada, traseira fastback com rabeta; faixa lateral |
| muscle | caixote de três volumes, capô comprido, tomada de ar, faixas por cima (teto, capô, tampa) |
| hatch | dois volumes, curto, tampa traseira quase vertical, aerofólio de teto |
| sedan | três volumes, quatro portas (duas janelas, linhas de porta), porta-malas saliente |
| electric | uma gota lisa de curvas, sem grade, faixa de luz na frente e atrás, rodas carenadas |
| rally | hatch alto, aerofólio grande, tomada no teto, número na porta, para-barros, faróis de milha, pneu de cravos |
| hyper | rente ao chão e comprido, entrada de ar lateral, asa enorme, difusor, aro escuro |
| classic | comprido e arredondado, rabo de peixe, teto em duas cores, para-choques cromados, pneu faixa branca |
| wedge | uma reta só do bico baixo à traseira alta e reta, venezianas no vidro de trás, faixa em diagonal |
| pickup | cabine alta, caçamba aberta com santantônio, vão livre alto, pneu de cravos, saia em duas cores |
| prototype | baixíssimo, bolha de cabine, para-lamas saltados, barbatana e asa na cauda longa |
| micro | metade do comprimento, uma bolha alta, rodinhas nos cantos, teto na cor de destaque (o gorro do Saci) |
| roadster | sem teto: quadro do para-brisa, santantônio, piloto de capacete (na cor de destaque) |

Cada desenho tem o gradiente de brilho com id próprio (antes todos usavam `#carShine`, e a página
com vários carros tinha ids repetidos). Há teste: 13 desenhos diferentes, na cor e no destaque.

### Nomes e cores

Bicho e lenda brasileira, como os antigos: **Saci** (o menino de uma perna só e gorro vermelho; o
teto do micro é o gorro), **Tatu** (a couraça do bicho = a picape parruda), **Boto** (o boto-cor-de-rosa
que vira um moço elegante de terno branco nas festas: o clássico rosa com teto creme), **Curupira**
(o guardião da mata de cabelo de fogo: grafite com o destaque laranja), **Iara** (a sereia dos rios:
a cunha anos 80 anil com néon rosa) e **Beija-Flor** (o que faz a curva mais fechada do céu e gasta a
energia mais rápido: verde-água com violeta). Nenhum remete a modelo de verdade nem é tradução de um
(fora ficaram Jacaré = Cayman, Arraia = Stingray, Puma, Corcel, Toro…); `tests/cars.test.ts` recusa a
lista.

### Desempenho

Nada aqui toca o renderizador nem o custo da simulação por tick (o número de carros na pista é o
mesmo). O que muda para quem desenha: a grade da IA passa a ter até 7 estilos de carroceria (eram 4),
mais os dos humanos: até 11 estilos diferentes numa corrida (7 da IA + 4 humanos), contra até 8 antes.
Se os modelos forem instanciados por estilo, é esse o teto de malhas por peça. As silhuetas são SVG no DOM dos menus (uma por cartão). Nos testes,
`tests/fuel.test.ts` corre um humano de cada carro: 14 em vez de 8 por pista.

### Achados (fora do escopo desta onda)

As duas medidas mostram desequilíbrios que já existiam antes dos carros novos:

- **Camelo X** perde para o Falcão GT em todas as 32 pistas no solo e é o último da grade (a economia
  não compensa 293 km/h e curva 0,70). É a única exceção da regra "nenhum carro novo ganha em toda
  pista de um carro de preço igual ou maior" (Boto e Tatu também ganham dele em todas). Mexer nele
  muda a IA, os recordes e os fantasmas do Camelo; fica para uma rodada própria (subir a velocidade
  para ~300 ou baixar o consumo, a medir).
- **Trovão V12** é, depois do Camelo, o original mais fraco na grade (6,38): bebe (1,25) e segura mal a
  curva, e a reta não basta no pelotão.
- **Boitatá GT** vence todas as corridas da grade com o piloto médio, e o **Sucuri E** ($ 16.000) é o
  2º da grade: anda mais que o Pororoca ($ 22.000) nas duas medidas e que o Carcará ($ 20.000) na grade.
- Na carreira de 8 copas o piloto médio vence quase tudo da 2ª copa em diante (sonda acima, antes e
  depois): a calibragem (`CAREER_AI_LEVEL_MAX`) foi feita para 4 copas; `docs/CARREIRA.md` já previa
  subir o teto "se isso sobrar".

### Como medir de novo

```
npx tsx scripts/car-balance.ts                 # solo, todos os carros, 32 pistas (~30 s)
# grade (~3 s por corrida): a tabela usou 2 sementes e estas 16 pistas (duas por copa), em 4 processos
npx tsx scripts/car-balance.ts grade 2 falcao,trovao,tornado,camelo copacabana,serra_do_mar,rota_66,canion,baia_toquio,monte_fuji,autobahn,passo_alpino,kruger,drakensberg,outback,daintree,atlantico,trollstigen,amalfi,etna
npx vitest run tests/cars.test.ts              # as regras (inútil, domina, faixa dos livres, silhuetas)
```

## Modelos

Cada um dos 13 estilos de carroceria (`CarDef.body`, lista em `CAR_BODIES`) tem um modelo 3D procedural
próprio, com silhueta que se reconhece de longe. Não existe modelo genérico de reserva: estilo sem
construtor é erro (`buildModel`), e `tests/car-models.test.ts` confere que as 13 malhas são diferentes.

| Estilo | Silhueta | O que o denuncia de longe | Roda |
|---|---|---|---|
| `gt` | grã-turismo de motor dianteiro, fastback | capô longo entre para-lamas que sobem, "ducktail", 4 lanternas redondas, saída de ar atrás da roda | `sport5` |
| `muscle` | muscle car | frente alta e chata com grade de ponta a ponta e faróis redondos cromados, tomada de ar no capô, traseira curta, pneus traseiros maiores | `mag` |
| `hatch` | hot hatch | cabine alta até o fim do carro, tampa quase vertical, aerofólio grande no teto, para-lamas alargados com borda preta, escape central duplo | `multi` |
| `sedan` | sedã econômico | três volumes (capô, cabine, porta-malas), friso cromado das janelas, placas, calotas | `hubcap` |
| `electric` | fastback elétrico | arco único e liso, bico fechado, teto de vidro, faixa de luz na frente e atrás, sem escapamento | `aero` |
| `rally` | carro de rali | suspensão alta, para-lamas em caixa, asa na tampa, 4 faróis de milha, painel de número, para-barros | `dish` (branca) |
| `hyper` | hipercarro em cunha | muito baixo, cabine-bolha avançada, entradas laterais enormes, asa em pescoço de cisne, difusor | `center` |
| `classic` | esportivo dos anos 60 | capô longo abaulado, para-lamas bojudos, boca oval cromada, para-choques cromados partidos, faixa branca no pneu | `wire` |
| `wedge` | cunha dos anos 80 | planos retos, faróis escamoteáveis (sobem à noite), frisos nas portas, venezianas e grade preta atrás | `aero` |
| `pickup` | picape | cabine simples alta, caçamba aberta (com o interior), santantônio com faróis, grade e para-choques cromados | `steel` |
| `prototype` | protótipo de endurance | bico rente entre para-lamas altos, cockpit-bolha, barbatana de tubarão, asa de ponta a ponta | `center` |
| `micro` | microcarro | 3,4 m (o único mais curto), cabine alta e redonda, "olhos" redondos, teto em outra cor | `hubcap` |
| `roadster` | conversível de dois lugares | sem teto: cockpit aberto com bancos, volante e piloto de capacete, para-brisa baixo, santantônio, corcovas | `mag` |

Todos ocupam a pegada de colisão (~4,4 × 1,9 m; o teste prende o casco em |x| ≤ 1,0 m e |z| ≤ 2,35 m,
com as rodas dentro da largura).

### Como um modelo é feito (`src/render/cars/`)

- `kit.ts` — o construtor de malha (`MeshBuilder`) e as primitivas fechadas com as faces para fora:
  cuboide de 8 cantos, caixa, viga, torno, cilindro, prisma de perfil (`extrudeZY`), caixa por dentro
  (caçamba, cockpit) e o loft de anéis. Faces planas (sombreado plano), triângulo degenerado descartado.
- `body.ts` — a carroceria por **seções-chave** (`Sec`: fundo, topo, meia largura, abaulado, ombro,
  vinco). O loft de 22 pontos por anel já sai com os **arcos das rodas recortados** (poço escuro, arco
  de 6 facetas), o para-lama que alarga (`flare`) e sobe (`rise`) sobre a roda, a faixa lateral (camada
  B) no vinco e as duas faixas do capô (camada A). A cabine é outro loft (para-brisa, teto, vidro
  traseiro). `BodyShape.topAt` responde a altura da superfície para os detalhes pousarem nela.
- `details.ts` — peças comuns: faróis e lanternas (caixa inclinada ou redondos), grade, retrovisores,
  escapamento, aerofólio com suportes, placa, pinça de freio, difusor, divisor, peças deitadas no capô.
- `wheels.ts` — 9 desenhos de roda (pneu com ombro e flanco, aro, poço e o desenho), em escala unitária.
- `styles/<estilo>.ts` — um arquivo por estilo: só números (seções, eixos) e a lista de peças.
  É a porta para a arte final: trocar um arquivo por um glTF não mexe no resto.
- `paints.ts` — os pincéis (pintura, acento, faixas, cromo, vidro, lentes, borracha) e a escolha pura
  da segunda cor e da pintura de cada carro. `material.ts` — o material único. `flame.ts` — a chama.

### Cor, segunda cor e pintura

- A cor do carro (`CarDef.color`) pinta a carroceria; a segunda cor (`CarDef.accent`, ou, sem ela,
  `defaultAccent`: azul-marinho no branco, quase preto nas cores quentes, branco nas frias) pinta
  aerofólio, asa, trava das rodas, pinças, capacete e as faixas.
- **Pintura (camadas A e B)**: faces marcadas com a camada A (faixas duplas do capô ao teto, teto do
  micro) ou B (faixa lateral no vinco, aro de algumas rodas) viram a segunda cor quando o carro usa a
  camada. O n-ésimo carro de um estilo (na ordem de `CARS`) usa a n-ésima pintura da lista do modelo —
  por isso o **Falcão GT** (1º GT) sai com faixas no capô e o **Boitatá GT** (2º) com a faixa lateral.
  Carro novo de um estilo já usado ganha a próxima pintura da lista sem mudar nada no renderizador.

### Material (um programa para todos os carros)

Um `MeshPhysicalMaterial` com o shader estendido (`material.ts`) serve carroceria, vidro, cromo, lentes
e rodas de todos os estilos. Por vértice: cor, se recebe pintura/acento, camada, rugosidade, metal,
emissivo de farol e de lanterna, farol escamoteável. Por instância: cor, segunda cor e bits (freio,
camada A, camada B). O freio acende as lanternas da própria instância (sem trocar malha). Verniz
(clearcoat) só nas faces pintadas e só na qualidade alta; o reflexo do céu vem do env map (PMREM) que o
céu já monta, reforçado nas peças metálicas e no vidro e contido na pintura (a cor manda).

### Instanciamento e animação

Os carros são agrupados por estilo: cada estilo presente é **uma** chamada de desenho para todos os
carros dele, cada desenho de roda presente é outra, mais a sombra de contato e a chama (uma cada). Na
qualidade baixa e nos carros a mais de 30 m à frente, as rodas viram a roda simples (pneu e disco liso,
112 triângulos), todas numa chamada; rodas não fazem sombra. Orçamento e medidas antes/depois em
`docs/DESEMPENHO.md` (seção 5, "Carros com modelo por estilo").
Giro de roda (limitado a 40% do passo do desenho por quadro, para os raios não "andarem para trás"),
rolagem, mergulho no freio, chacoalho na derrapagem, chama do nitro (saindo dos escapes de cada modelo;
no elétrico, do difusor), etiquetas de nome (acima do teto de cada modelo) e o fantasma translúcido (com
a malha do estilo do carro dele) continuam como antes.

### Como ver

`npm run dev` e `node tools/render-harness.mjs <porta> <filtro>`:
- `showroom` / `showroom_tras` — os 13 estilos em grade na pista (`?showroom=1[&carview=rear34]`);
- `folha_frente34`, `folha_tras34`, `folha_lado`, `folha_noite` — folha de contato com os 13 estilos e o
  2º GT (`?sheet=front34|rear34|side|rear`, 1920×1080);
- `carro_<estilo>_<vista>` — um estilo de perto (`?carview=front34|rear34|side|rear&body=<estilo>`; ou
  `&car=<id>`). Estilos sem carro nos dados ganham um carro de demonstração só no harness.
