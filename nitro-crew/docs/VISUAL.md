# Visual (Fase 2, onda F)

## Pista, céu e luz

Referência: Horizon Chase Turbo (e Art of Rally). Tudo procedural, sem asset nem dependência nova. Arquivos:
`src/render/palette.ts` (as cores e a luz de cada bioma × período), `road.ts` + `road-textures.ts` (o chão da
pista), `terrain.ts` (terreno, mar, horizonte), `sky.ts` (céu, nuvens, sol/lua, luzes, névoa), `effects.ts` (cor
da poeira) e `renderer.ts` (tone mapping, bloom). Testes das partes puras: `tests/render-ground.test.ts`.
Capturas antes/depois: `tools/render-harness.mjs` (com `npm run dev` no ar), nas 12 combinações bioma × período
que as pistas usam, com 1 e 4 jogadores e qualidade alta e baixa.

### Paleta (`palette.ts`)
Uma base diurna por bioma (asfalto, acostamento, faixa de transição, chão baixo/alto, rocha, areia, três planos do
horizonte, água) e uma gradação por período. Os céus do entardecer são escritos **por bioma** (o laranja do cânion,
o rosa da Lapônia, o dourado do Pantanal, o rosa-lilás do litoral) e os da noite também (a cidade tem o horizonte
roxo da poluição luminosa; o litoral, luar azul). A paleta traz a luz inteira: cor e força do sol/lua, céu e chão da
hemisférica, força do reflexo do céu, exposição, densidade da névoa, bloom, nuvens, poça de luz dos postes e o
brilho das marcações. À noite o albedo azula mas não apaga (quem escurece a cena é a luz): a cena não vira borrão.

Regras que os testes travam (`tests/render-ground.test.ts`):
- névoa sempre com cor (saturação HSL > 0,18) — nunca cinza morto;
- céu em gradiente forte: horizonte pelo menos 0,2 de luminância acima do topo nas combinações usadas;
- borda da pista legível em todas as 18 combinações: asfalto × faixa de transição ≥ 0,12 de luminância, zebra
  branco × vermelho ≥ 0,35, faixa pintada × asfalto ≥ 0,45, linha amarela × box ≥ 0,3;
- à noite: marcações com brilho, poças de luz, sem nuvens; limiar do bloom em 1,6, entre as fachadas acesas do
  cenário (1,25, não brilham) e as luminárias (2,0, brilham) — com 1,0 a cidade virava um clarão no merge da onda F; de dia o limiar do bloom fica ≥ 1 (branco ao sol não
  "estoura").

### Pista (`road.ts`, `road-textures.ts`)
- **Asfalto**: textura de 24 m (granulado fino, manchas largas que fecham a repetição sem emenda, trilhas de pneu,
  remendos discretos), faixas de bordo e tracejado central pintados nela, com mipmap e anisotropia — o detalhe fino
  some na distância em vez de cintilar. As marcações têm um mapa emissivo próprio: à noite a "tinta refletiva"
  acende de leve e a curva se lê de longe. Faixas de 3 segmentos um pouco mais claras/escuras dão o ritmo de
  velocidade (como antes).
- **Acostamento** de 1,4 m (cascalho, terra, areia ou calçada, pela paleta, com a borda de dentro escurecida pelo
  pó de pneu) e, no terreno, a **faixa de transição** até 9,6 m com um degradê curto até o chão em 10,8 m e borda
  irregular: o asfalto não encosta direto na grama. Terra vermelha na mata, areia no deserto e no litoral, terra
  na savana, cascalho na montanha, calçada na cidade.
- **Zebras** em relevo (7 cm de crista, seção de 3 vértices com sombreamento plano) nas curvas de |curve| ≥ 2, dos
  dois lados, em blocos de 1 m vermelho/branco com friso escuro; são quadriláteros soltos por segmento (reta não
  tem, e duas curvas na mesma janela não ficam ligadas por um quad esticado).
- **Box**: divisa zebrada amarela na faixa que a física trata como grama (x 1,02–1,25), linha amarela contínua,
  concreto claro com a vaga em U e "BOX" pintado no chão, linha branca na borda de fora. `PIT_LANE_X` segue
  `sim/physics.ts` (1,25 < x < 1,95); o teste confere que a faixa pintada contém a da física.
- **Largada**: decalque de 92 m (22 segmentos antes da linha e 1 depois) com o quadriculado (3 fileiras de 0,6 m entre duas linhas brancas) e um colchete
  branco na frente de cada posição do grid — as posições vêm de `gridMarks`, que o teste confere contra o
  `createRace` de verdade. As posições e o comprimento do decalque saem das constantes do núcleo
  (`GRID_FRONT_GAP`, `GRID_ROW_GAP`, `GRID_LANE_X`): no merge da onda F o grid se espaçou (5,2 → 8 m) e a pintura,
  que repetia os números, ficou para trás — o teste pegou.
- **Noite**: poça de luz aditiva sob cada poste (`lamp` do cenário), no asfalto, e asfalto acetinado que pega o
  brilho dos faróis. O poste é modelo do cenário (`scenery/props.ts`); a poça fica sob a luminária dele
  (`LAMP_HEAD_X`, 2,35 m do pé, lido pelo `road.ts` — era um 4,4 m do braço antigo, e o teste "luz do poste" pegou).

### Terreno e horizonte (`terrain.ts`)
- Chão plano até 26 m da pista (o cenário mais afastado fica a ~37 m e nunca flutua; teste); dali desce para o
  **fundo do vale** quando a pista está alta (a estrada de montanha olha o vale lá embaixo) e sobe em relevo por
  bioma (cristas na montanha, dunas no deserto, morros no litoral e na mata, planície na savana). A última coluna
  volta para perto do fundo: a borda da faixa não vira degrau sobre o disco de chão.
- **Cor por altura e inclinação**: prado → mata → rocha nas encostas → neve acima da linha (com borda irregular);
  areia dourada → rocha vermelha no deserto; capim seco em manchas na savana; praia seca → molhada → penhasco no
  litoral; quadras na cidade. Leve variação por vértice: chão "pintado", não de plástico.
- **Normais suaves** calculadas da própria tabela de alturas (inclinação para fora e ao longo): o sombreamento
  plano de triângulos longos e finos fazia listras nos morros com o sol baixo.
- Altura, cor e inclinação dependem só do índice do segmento: ficam numa **tabela por pista** montada na troca de
  pista; o quadro só copia (com 4 viewports isso é mais barato que o ruído por vértice de antes).
- **Mar**: turquesa perto, azul fundo longe, faixas largas de onda, pouco céu refletido (a câmera baixa pegava
  fresnel em tudo e o mar ficava branco), caminho de brilho do sol/lua, névoa mais leve que a do chão; as ondas
  somem com a distância (sem cintilar). **Espuma** na linha d'água (onde o chão cruza o nível do mar, calculado por
  ponto) que vai e volta, com uma faixa rasa turquesa mar adentro.
- **Horizonte em três planos** (470, 660 e 920 m) sem névoa de shader: cada plano já traz a névoa na cor (o de longe
  mais claro, a base de todos dentro da névoa), com **encostas lisas** (normal suave do vértice, 300 pontos por volta;
  até 04/10 eram faces planas pela derivada, e a serra saía facetada) que o sol acende conforme o bioma; **morro largo
  de crista redonda** (20–40 ondulações por volta, 16–22 no alpino com |r| suave — eram 64–256 e picos em agulha;
  `tests/horizonte-suave.test.ts` trava a dobra do contorno abaixo de 60°, menos as mesas do deserto); neve pela
  altura de cada vértice (faixa de 50–85%; decidida por coluna, fazia faixas verticais na encosta lisa); mesas no deserto; ilhas e cabos no litoral; skyline instanciada na cidade. Um material só para
  os três (antes era um material novo a cada pista).

### Céu e luz (`sky.ts`, `renderer.ts`)
- Domo com gradiente forte e o horizonte do lado do sol puxando para o brilho do período; abaixo do horizonte, a
  cor da névoa (o chão distante some sem emenda).
- **Sol** a 34° de dia (sombras longas que desenham o relevo), **grande e baixo** no entardecer (9°, à frente na
  largada, disco laranja e não branco); **lua** grande a 20° com crateras no shader. Estrelas com brilho e tom
  variados, mais densas perto do horizonte.
- **Nuvens** estilizadas numa malha só (1 chamada de desenho em vez de 7): cúmulos de barriga reta e faixas finas
  e baixas no horizonte, dois tons pelo sol, borda acesa no entardecer, derretendo na cor do horizonte. Na alta as
  bolhas são lisas (normal suave do vértice, icosaedro de 320 faces; na média e na baixa, de 80 — a mesma silhueta de
  longe). Até 04/10 a normal vinha da face e a nuvem saía facetada.
- **Luz**: direcional com a cor do período + hemisférica cujo céu é azul-claro (dia), roxo (entardecer) ou
  azul-noite: é ela que pinta as sombras (coloridas, não pretas); `shadow.intensity` 0,9 (0,7 à noite).
- **Tone mapping ACES** (mantido: os carros foram calibrados com ele) com a exposição da paleta (0,98 dia, 1,0
  entardecer, 1,12 noite). **Bloom** só na alta, por paleta: de dia limiar 2,6 (só o disco do sol e os emissivos —
  com 0,85 o branco da pintura ao sol "acendia", e o reflexo do sol baixo na lataria ainda passava de 1,8), entardecer 1,6, noite 1,0 e mais forte (faróis, postes, neon).

### Desempenho
Ver `docs/DESEMPENHO.md`, seção "Pista, céu e luz (onda F)": chamadas de desenho caem ~5 por viewport (nuvens
numa malha, largada no decalque), triângulos sobem ~8–10 mil por viewport (planos do horizonte, nuvens mais
redondas, zebras, duas colunas de terreno), a CPU do terreno por quadro cai (tabela por pista). Baixa e média não
ganham nada caro: o bloom continua só na alta; sombras como antes.

## Sombreamento: normais suaves com vinco

Pedido do dono (04/10/2026): o jogo inteiro "menos quadrado". Até aqui todo material do cenário e dos carros usava
`flatShading` — cada triângulo uma luz só, então cilindro, cúpula, copa, pedra, para-lama e pneu saíam facetados
mesmo quando a silhueta já era redonda. Agora a luz é suave onde a superfície é curva e a aresta continua viva onde
ela é de verdade (caixa, beiral, vinco da lataria). A geometria não mudou: mesma malha, mesmos triângulos.

- **A conta** (`src/render/normals.ts`, `smoothNormals(geo, vinco)`): a malha continua **não indexada** — cor,
  pintura e material seguem por face, intactos. Os pontos coincidentes são soldados (0,1 mm, para as contas em
  float32 de peças fundidas); aresta de exatamente duas faces com dobra menor que o vinco é lisa, o resto (dobra
  maior, borda solta, aresta de 3+ faces) é viva. Em cada ponto, as faces ligadas por arestas lisas formam um leque
  e dividem **uma** normal (média ponderada pelo ângulo do canto), como o "auto smooth" dos editores 3D — sem
  costura de luz dentro de uma região lisa. (A primeira versão fazia a média por face, só com as vizinhas dentro do
  vinco; num morro ou copa irregular cada canto ficava com uma normal e a luz mostrava costuras. Teste "sem costura".)
- **Telhado** (`ROOF_TURN_DEG`, só construções e marcos): as águas de um telhado baixo de quatro águas dobram só
  ~28° entre si — menos que o gomo de um cilindro de 10 lados — e pelo vinco viravam travesseiro. Com a regra, a
  aresta entre duas faces inclinadas que viram 75° ou mais em planta fica viva (o gomo de uma cúpula de 8 vira 45°),
  e também a entre uma água e uma face plana de verdade (a cumeeira do telhado truncado: senão o leque do canto de
  cima passava de uma água para a outra através dela).
- **Face degenerada** (a ponta colapsada de um `hip()`, área zero) não conta na aresta: antes de corrigir, ela fazia a
  aresta "ter 3 faces" e a travava viva.
- **Enrolamento trocado**, que o sombreado plano escondia (ele tira a normal das derivadas da tela, sempre para a
  câmera): a vizinha que percorre a aresta no mesmo sentido entra **virada** na média e cada face fica com a normal
  do próprio lado. O diagnóstico achou 28 partes assim (marcos do Norte/Nordeste como o coqueiral e a palafita,
  palmeiras e samambaias). Com o material de dois lados do cenário, a luz sai certa dos dois lados.
- **Onde** (uma vez por modelo, nunca por instância nem por quadro): cenário e marcos em `catalog.ts` `getModel` →
  `scenery/smooth.ts` (vinco pela família do id; só as partes `flat` e de fachada — `glow`, `beacon` e `cone` são
  luz, sem normal, e `panel` é placa plana; malha indexada é arte glTF com normal própria e fica como veio); carros em
  `MeshBuilder.build()` (`cars/kit.ts`) — o casco procedural e o carregado de um .glb passam pelo mesmo caminho e saem
  iguais — e rodas com o vinco delas (`wheels.ts`). Os materiais liso, decoração, horizonte (`haze`), fachadas,
  carros e fantasma não usam mais `flatShading`. Os showrooms (`tools/scenery-showroom.html`,
  `landmark-showroom-*.html`) mostram como no jogo; `scenery-showroom.html?…&flat=1` mostra o antes.
- **Continua plano de propósito**: acostamento, zebras e box (`road.ts`: a crista de 3 vértices da zebra é o
  desenho), os planos do horizonte (montanha facetada que o sol acende) e o modo Retrô (`src/render-pseudo3d/`).
  O terreno já tinha normais suaves.

| família (`CREASE`) | vinco | por quê (capturas `ondai-suave-*`) |
|---|---|---|
| carro (`car`) | 45° | lataria e para-lama lisos, para-brisa sem facetas; para-choque, grade, soleira, vinco de cintura e moldura do vidro vivos. 40° e 50° saíram quase iguais nas folhas de contato |
| roda (`wheel`) | 50° | o pneu (12 lados na época, 30°; 18 desde a onda I) e o ombro ficam redondos; raios e porcas (caixas) continuam caixas |
| vegetação (`plant`: tree, pine, palm, cactus, bush, tuft, flowers, far redondos/cones/palmeiras) | 60° | copa de icosaedro (41,8°), tronco e cacto de 6 lados (60°) e o cone da conífera redondos; 75° não mudava nada à vista |
| pedra (`rock`: rock, searock, stack, mesa, termite, pebbles, far:rock/boulder) | 50° | lisa, mas o dodecaedro (63,4°) e a quina da laje ainda mostram a lasca; com 65° virava seixo de rio |
| marcos (`landmark`, `lm:*`) + regra do telhado | 45° | cúpula, torre redonda (igreja barroca), cuia e vidro da Catedral lisos; caixa, telhado e pináculo de 4 lados vivos |
| construções e objetos de pista (`built`, o resto) + regra do telhado | 40° | poste, caixa-d'água e torre de 10+ lados arredondam; casa, beiral, telhado (casa de fazenda, casa japonesa, pagode) e fachada (caixa) saem como antes |

**Custo**: chamadas de desenho, triângulos e instâncias **iguais** (render-harness e scenery-harness, antes e depois:
Copacabana 56 / 133.263, Sampa noite 66 / 153.112, Rochosas 52 / 153.412, Transpantaneira 50 / 141.044, Amalfi 53 /
130.362; o cenário isolado nas 4 cenas × 1 e 4 jogadores do scenery-harness, idem): o quadro não muda, a normal já era
um atributo do lote. A montagem, uma vez (Node, frio, antes → depois): carros (13 cascos + 9 rodas) ~35 → ~46 ms de
mediana (o primeiro, com o JIT frio, ~95 → ~160 ms); o catálogo inteiro do cenário (435 modelos, 278 mil triângulos)
~830 → ~1.100 ms; a troca de pista (layout + modelos) Copacabana ~285 → ~345 ms e Sampa ~195 → ~265 ms. O
`smoothNormals` sozinho custa ~0,23 µs por canto (750 mil cantos do catálogo em ~175 ms).

**Ainda parece quadrado** (próximo passo é geometria, não luz): a cor por face (`speckle`, as manchas das copas, das
pedras e dos morros) continua mostrando as faces de propósito; silhuetas de poucos lados (tronco de 6, pneu de 12, o
arco da roda em 6 facetas, a coroa da conífera); os detalhes de carro que são caixas (para-choque, retrovisor,
aerofólio, grade) e as quinas da carroceria sem chanfro. Saída: chanfro (bevel) nas quinas de lataria e para-choque,
mais lados nas silhuetas que aparecem perto (pneu, tronco, arco), e menos `speckle` onde a forma já é lisa.
(O cenário fez a parte dele na onda I: "Forma redonda", abaixo. Os carros continuam como estão.)

### Carros menos quadrados pela geometria (onda I)

O passo seguinte, nos carros (o cenário fica para outra rodada). Referência Horizon Chase Turbo: limpo, parrudo, liso,
não realista. Capturas `ondai-carros-*-antes/depois` (folhas de contato de dia e de noite, showroom, perto de cada
estilo e duas corridas com a câmera de perseguição).

- **Quina em dois passos de 30°** (`kit.ts`): o chanfro de 45° caía no fio do vinco de carro (45°) — liso ou vivo
  conforme o arredondamento do float32, a luz manchada. Com três dobras de 30° (lado → chanfro → chanfro → topo) a
  quina fica lisa com folga e lê redonda, com metade dos triângulos de um arco. É o perfil da barra arredondada
  (`roundBar`: para-choques, carcaças de farol e lanterna, grade e moldura, retrovisores, placas da asa, painéis de
  número), da caixa arredondada (`roundBox`: tomadas de ar do capô e do teto) e da viga (`roundBeam`: para-choques
  cromados do clássico, bigode do micro). Perfis 2D ganham filete em arco de passos ≤ 30° (`fillet`: lâmina da asa em
  perfil de asa, ducktail, spoiler, corcovas do roadster).
- **Carroceria** (`body.ts`): bico e traseira em quarto de elipse, em planta e de perfil (a superfície sai da tampa
  tangente a ela; a tampa e a pegada não mudam); ombro do vinco ao capô em quarto de elipse de 4 passos (pontos
  pela tangente, dobras de ~22°); arco da roda de 6 para 10 facetas.
- **Cabine**: a borda do teto (vidro lateral → teto) virou um arco de três pontos; as dobras para-brisa → teto e
  teto → vidro traseiro, um arco de três estações. Colunas e frisos pousam no alto do vidro (`cabinGlassTop`).
- **Rodas**: pneu de 12 para 18 lados; roda simples de 10 para 12; lâmpadas redondas de 10–12 para 14–16; escapes de
  8 para 10; capacete do roadster de 8 gomos para 12 × 6.
- **O que não mudou**: os pincéis de farol, lanterna, escamoteável, faixa A/B e acento de cada estilo (teste com a
  lista de antes e as áreas na mesma ordem), a pegada (mesmo validador), o modo Retrô.
- **Defeito achado nas capturas**: o pneu dianteiro da cunha furava o capô baixo (vinha de antes). Os para-lamas da
  frente agora sobem sobre a roda (`rise` 0,27), e o teste "o pneu não fura o capô" vale para os 13.

Custo e medidas: `docs/DESEMPENHO.md`, seção 5. Testes: `tests/car-round.test.ts`.

**Ainda quadrado nos carros**: as tampas do bico e da traseira são planas (o arredondado é a borda delas), os vidros são
planos de propósito, as peças finas continuam caixas (frisos, venezianas, placas, barras da grade, difusor, para-barros)
e a cunha e a picape guardam as quinas que são o desenho delas (caçamba, santantônio, grade traseira).

Testes: `tests/render-normals.test.ts` (a conta: cilindro, caixa, icosaedro, sem costura, enrolamento trocado, telhado
com e sem cumeeira, cor por face e determinismo; e a garantia de que carros, rodas, o carro do glTF, os modelos do
cenário e os materiais saem lisos — uma volta ao `flatShading` quebra ali).

## Forma redonda: geometria e cor do cenário (onda I)

Depois das normais suaves, o que ainda desenhava as facetas na beira da pista era a **cor por face** (a mancha
`speckle` das copas e pedras e o `tintUp` da neve e do musgo acendiam face sim, face não, mesmo com a luz lisa) e as
**silhuetas de poucos lados** (copa de icosaedro de 20 faces — 12 pontos, contorno de hexágono —, tronco de 6, camada de
conífera de 7, cacto de 6–8, moita de caixa). Referência: Horizon Chase Turbo — macio, redondo, cor limpa. Mudou só a
geometria e a cor dos modelos de vegetação e pedra (`vegetation.ts`), a cerca-viva e o penedo do mar
(`structures.ts`); marcos, carros, núcleo, layout e runtime ficaram como estavam — mesmas instâncias, mesmas
chamadas de desenho.

- **Cor por PONTO, nunca por face** (`geom.ts`): `mottle` varia o tom com um ruído 3D suave da posição
  (`valueNoise3`, `noise.ts`) — pontos coincidentes têm o mesmo fator, a cor corre contínua de uma face para a outra,
  manchas largas e macias; `tintUpSoft` é o `tintUp` com o peso vindo da normal média do ponto (soldado pela posição)
  numa rampa — a neve e o musgo escorrem pela pedra. O gradiente de altura (`shadeY`: pé escuro, topo claro) continua;
  cada camada de conífera ganhou o próprio (saia escura, bico claro). `speckle`, `tintUp` e `jitter` continuam no kit
  para quem quer as faces à mostra (marcos, construções).
- **Bolha macia** (`lumpy`): o ponto anda na direção da origem da peça por ruído 3D suave da direção — o `jitter`
  (sorteio por vértice) no icosaedro subdividido virava pedra lascada; este vira batata. `horizontal` só desloca em X/Z
  (corpo de cilindro, o pé no chão).
- **Copa** de icosaedro subdividido (80 faces; < 0,45 m de raio fica com o de 20); poplar e cipreste, esticados
  3,4–4×, em esfera de 10 × 8 (o icosaedro esticado mostrava faces compridas). **Tronco** de 8 lados (gigante 9,
  baobá 12) e **sem tampas** — o pé está no chão e a ponta dentro da copa; as tampas eram 1/3 dos triângulos dele
  (a oliveira, baixa, fica fechada). Galhos de 6, abertos.
- **Moita do pé** (a pegada das árvores e palmeiras): meia esfera de 12 lados (≥ 96% do raio entre os vértices) com
  bolhas que sobem e a segunda cor onde sobem (a flor de buganvília no litoral) — eram 10 lados mais quatro tufos de
  icosaedro de 20 faces (130 triângulos; 60 agora).
- **Conífera** de 10 lados por camada (eram 7), bico de 8. **Palmeiras**: tronco de 8–10 lados, folha do coqueiro e da
  imperial com 4 gomos (a curva da folha que cai sai curva), cocos redondos. **Cactos**: saguaro de 12 com braços de 8,
  barril de 10 com o topo em cúpula (era tampa de panela), eufórbia de 10 com as pontas redondas, cupinzeiro de 12 em
  bolhas, folhas de babosa/agave de 7 lados (eram pirâmides de 4).
- **Pedras**: o matacão é uma esfera de 14 × 7 em bolhas, achatada (era o icosaedro de 80 faces com gomos na silhueta), e
  as duas pedras de cima são redondas (eram blocos de 20 e 12 faces, lascas; a terceira, pequena, saiu); arenito em
  camadas de 12 lados (eram 10 e 7); penedo do mar, mesa e coluna do mar mais redondos (14 × 7, 12 e 10 lados).
- **Moitas** em três bolhas de 80 faces (eram quatro de 20); as flores pousam na pele de uma bolha (antes algumas
  ficavam soltas no ar). **Cerca-viva** (sprite e cerca de divisa): barra de quinas arredondadas (`roundedBar`, seção
  que não muda ao longo do lance — os lances emendam sem degrau) em vez da caixa.
- **O que paga a conta** (`cullInside`): a parte de uma bolha enterrada em outra (copa de 5 bolhas, moita de 3, a pedra
  de cima no matacão, o fundo da moita no chão) nunca aparece e sai na montagem — os três cantos dentro do sólido
  garantido da outra peça (o elipsoide dela encolhido pela deformação e pela corda das faces). A copa redonda sai com
  ~1/4 a mais de triângulos em vez do dobro; a pedra, ~70% a mais em vez do dobro.

Custo (medido; `docs/DESEMPENHO.md`, seção 9): mesmas chamadas de desenho e instâncias; triângulos do cenário +10% a
+27% por viewport nas cenas de vegetação, nenhuma cena acima do pior caso de antes. Por modelo: árvore de copa redonda
354 → 429, gigante 303 → 415, coqueiro 406 → 428, bananeira 238 → 160, conífera 104 → 132, moita 80 → 189,
saguaro 270 → 340, matacão 156 → 264.

**Ainda parece quadrado** (depois desta onda): as silhuetas de longe (`far:*`, ≤ 60 triângulos — a copa de longe é um
icosaedro de 20 faces; de 200 m+ são poucos pixels, mas a troca de modelo a 52/90 segmentos ficou mais visível); a
moita do pé é mais lisa que os tufos de antes (lê como um tapete baixo visto de cima; da câmera de perseguição, como
monte); as nuvens e os planos do horizonte (facetados de propósito: "Sombreamento", acima — `uFacet` em
`terrain.ts`, uma linha se o dono quiser mais macio); props de pista (poste, cerca, placa, arquibancada), que são
caixas por natureza.

Testes: `tests/scenery-forma.test.ts` — nas famílias redondas, a cor não salta (> 1%) entre as duas faces de uma aresta
lisa (o `speckle` saltava até 23%, o `tintUp` até 77%); a normal lisa fica, em média, a ≤ 22° da face (pedra ≤ 18°;
icosaedro de 20 faces dá 37°, o subdividido ~19°; cada canto pesa a área × o ângulo, como na normal suave — senão o
bico de cone, que aponta para cima em qualquer resolução, contava como gomo); modelo redondo ≤ 700 triângulos (pedra
≤ 360, mesa e coluna ≤ 600) e o custo do cenário por segmento ≤ 900 triângulos em toda pista (antes da onda o pior era
764, depois 856). Na base de antes a cor reprovava 78 modelos e a forma 55.

## Cenário

O que aparece na beira da pista: árvores, pedras, prédios, placas, arquibancadas, box, cercas, postes e os
pontos de referência de cada país. Tudo procedural (Three.js + canvas), low-poly de cor por face (luz suave
com vinco: "Sombreamento", acima) e cor saturada, na linha de Horizon Chase Turbo. Código em
`src/render/scenery/` (a porta de entrada continua `src/render/scenery.ts`, com o mesmo contrato para o renderizador: `new Scenery()`, `group`, `setNight()`,
`update(frame, track, time)`, `dispose()`).

| arquivo | o quê |
|---|---|
| `geom.ts` | kit de modelagem: primitivas com cor por vértice, `tf`/`lean`, fusão, deformação por hash (`jitter`) e em bolhas macias (`lumpy`), cor por face (`speckle`, `tintUp`) e por ponto (`mottle`, `tintUpSoft`), sombreado por altura, barra arredondada, `cullInside` (o que fica enterrado em outra peça sai), `bandPoints` (a fatia do modelo na altura do carro) |
| `vegetation.ts` | árvores, coníferas, palmeiras, cactos (e o que os substitui por país), moitas, pedras, forração, silhuetas de longe |
| `structures.ts` | prédios com fachada texturizada, casas por região, torres (TV, mastro, farol, caixa-d'água, cata-vento), cercas, postes e fios, turbina, torii, pagode, capela, barcos, molhe, mesas |
| `props.ts` | poste de luz, outdoor, placa de curva, arquibancada com público, pórtico de largada, garagens do box, placa do box, cone; o mapa de UV do atlas de painéis |
| `textures.ts` | fachadas (4 estilos, 4 × 4 janelas por repetição, com o mapa das janelas acesas) e o atlas de painéis (8 outdoors de marcas inventadas, largada, box, chevrons, xadrez) |
| `catalog.ts` | que modelo cada sprite vira por bioma e país, as receitas de decoração, o registro de modelos (montados uma vez) |
| `ground.ts` | altura do chão — espelho do relevo de `terrain.ts` (ver "Chão") |
| `layout.ts` | onde cada objeto fica, uma vez por pista e determinístico (inclusive os marcos turísticos) |
| `landmarks/` | marcos turísticos (onda G): registro, contrato, kit e os modelos por região — ver "Marcos turísticos" |
| `runtime.ts` | os lotes instanciados e a pose por quadro, por viewport |
| `smooth.ts` | o vinco da normal suave de cada família de modelo, aplicado pelo `getModel` (ver "Sombreamento") |

### Duas fontes de objetos

1. **Sprites do núcleo** (`track.segments[i].sprites`, do `builder.ts`): árvore, pinheiro, palmeira, cacto, moita,
   pedra, prédio, torre, poste, outdoor, placa, arquibancada, pórtico, muro do box, placa do box, cone. A posição
   e a largura são as da colisão (física); o renderizador só escolhe o modelo. O `builder` só mudou nas **praças da
   cidade e mirantes do litoral** (ver "Marcos turísticos").
2. **Decoração só visual** (`layout.ts`), que a física não vê:
   - **forração** na faixa em que o carro anda (9,6 a 23 m do centro): capim, flores, pedrinhas, moitas baixas —
     no máximo 1,05 m de altura, então passar por cima não parece atravessar nada;
   - **cerca de divisa** a 23,6 m: é onde o carro para de andar para o lado (`|x| ≤ 3,2` em `sim/physics.ts` =
     22,4 m, mais a meia largura visual do carro). A trava lateral da física, que antes parava o carro no nada,
     agora tem uma cerca (curral, cerca de tábuas, guard-rail, muro de pedra, gradil ou cerca-viva, por região);
   - **mata com clareiras** além da cerca: ruído baixo ao longo da pista decide onde é bosque e onde é clareira;
     espécies por bioma e país; três faixas de distância (26–42, 42–82 e 82–142 m); silhueta de poucos
     triângulos a partir de 52 segmentos (≈ 200 m);
   - **postes com fios** de um lado a cada 12 segmentos (48 m) nas estradas de fazenda e de deserto — o ritmo
     deles dá a sensação de velocidade;
   - **soltos** (pedras, moitas, cupinzeiros, cactos), **barcos** balançando no mar, **guarda-sóis** na beira
     d'água (Copacabana, Mediterrâneo), **colunas de arenito** no mar (Great Ocean Road);
   - **pontos de referência** raros por país: turbinas eólicas girando (Europa), torii e pagode (Japão), mesas
     e buttes (EUA), cata-ventos de fazenda (Karoo, Outback), capelas e casas de fazenda (Brasil, Alpes),
     chalés, rondavéis (Drakensberg), casinhas vermelhas (Escandinávia), casas cúbicas brancas e moinhos de
     pedra (Santorini/Amalfi).

### Por bioma e país

| bioma | árvore do sprite | outras espécies | cerca | pontos de referência |
|---|---|---|---|---|
| tropical (BR, JP, AU) | gigante com raízes tabulares, mangueira, bananeira, samambaiaçu | coqueiros, palmeira-imperial, helicônia | — (JP: cerca-viva) | capela e fazenda (BR), torii (JP) |
| savana: Kruger | acácia-guarda-chuva, baobá, marula | capim dourado, cupinzeiros, kopjes | curral | rondavéis |
| savana: Autobahn | carvalho, choupo, tília | campo com flores | tábuas | turbinas eólicas, fazendas, capela |
| savana: Pantanal | ipê-rosa, ipê-amarelo, mangueira | palmeiras | curral | fazenda, capela |
| deserto: EUA | (cacto) saguaros, barril, palma | pedras de arenito | curral + postes | mesas, caixas-d'água, cata-ventos |
| deserto: Outback | cupinzeiro, spinifex | eucalipto | curral + postes | cata-ventos |
| deserto: Karoo | eufórbia-candelabro, aloe | moitas | curral + postes | cata-ventos, mesas baixas |
| deserto: Etna | palma, agave | oliveira, cipreste, lava | muro de pedra | moinho de pedra |
| montanha | álamo dourado (EUA), bordo vermelho (JP), faia de outono (Europa), bétula (NO) | pinheiros, abetos, pinheiros com neve, cedros (JP), lariços dourados (NO) | guard-rail | chalés, capelas, pagode, rondavéis |
| litoral | (palmeira) coqueiros; fora dos trópicos o sprite `palm` vira pinheiro/bétula (NO), pinheiro-negro (JP), eucalipto (AU), palmeira-de-leque (Mediterrâneo) | oliveiras e ciprestes (Med) | gradil ou muro de pedra (lado de terra) | barcos, farol, casas cúbicas, casinhas vermelhas |
| cidade | árvore de calçada | cerca-viva | gradil | prédios por cidade (vidro, apartamentos com sacada, bulevar com mansarda, tijolo com neon, torre dourada em Las Vegas) |

Prédios têm fachada com janelas de 3 m (4 × 4 por repetição), térreo próprio (portaria, lojas com toldo),
cornija, telhado (mansarda com lucarnas, duas águas, quatro águas) e equipamentos no teto; à noite as janelas
acendem (≈ 50% acesas, quentes e algumas frias; no entardecer com 60% da força), o letreiro de neon brilha, a luz
de balizamento das torres pisca e os postes ganham o facho de luz (só na noite fechada). Outdoors, placas e pórtico vêm de um atlas (`panelAtlas`) com marcas inventadas —
nada de logotipo real; a única marca de verdade é a PADELIZOU, do dono.

### Física × visual (a colisão com o cenário)

O núcleo bate num sprite sólido quando o carro está no mesmo segmento e `|x_sprite − x_carro| < SPRITE_HALF_WIDTH × escala + CAR_HALF_WIDTH`
(`sim/collisions.ts`). A regra do visual: **o ponto do modelo mais perto da pista, na altura do carro
(0,05–1,3 m), fica na borda de colisão** (`|x| × 7 m − meia largura × escala × 7 m`). Nada de valor em
`sprites.ts` mudou — foi o visual que passou a obedecer à física:

- **árvores e palmeiras**: a copa fica alta, então a pegada é uma moita contínua em volta do pé (meia esfera
  de 12 lados com o raio de colisão, ≥ 96% dele entre os vértices) ou raízes tabulares/tronco (baobá). Antes,
  na altura do carro só havia o tronco (0,26 m de raio contra 1,75 m de colisão): a batida vinha 1,5 m antes;
- **pinheiros**: o degrau de baixo nasce a 0,3 m com o raio de colisão;
- **cactos**: os braços do saguaro saem baixo (0,7–0,8 m) e alinhados com a pista (giro ±0,3 rad), então a
  largura na pista é a de colisão mesmo no saguaro mais alto; moitas de cacto e spinifex têm um monte baixo;
- **pedras**: um matacão largo cuja cintura, na altura do carro, é o raio de colisão em qualquer giro;
- **torres**: pés a 45° na distância `R·√2` — a largura na pista é `R` (as torres não giram);
- **prédios, arquibancadas, postes, garagens**: o layout mede o ponto mais à frente do modelo na altura do
  carro (`modelFrontX`) e o encosta na borda de colisão. Antes, o prédio era empurrado para 11 m (a colisão
  começava a 8,75 m: batia-se 2,25 m antes) e a arquibancada tinha o público a 13,7 m com a colisão a 9,8 m
  (agora o muro com alambrado fica na borda);
- **outdoor e placa de curva**: as pernas estão na largura de colisão; o painel de chevrons desceu para a altura
  do para-choque;
- **moita** (o único sprite de vegetação que não é sólido): no máximo 1,1 m de altura, para o carro passar por
  ela sem parecer atravessar um muro.

`tests/scenery.test.ts` mede isso em **todos os sprites sólidos das 32 pistas** (fatia exata dos triângulos na
altura do carro, com giro e escala de cada um) e reprova quem ficar a mais de 15 cm + 8% da meia largura da
borda de colisão; mede também os modelos redondos em 16 giros, e tem um caso com a geometria antiga (tronco
fino, prédio empurrado) que o detector tem de reprovar.

**O que continua fora (é da física, não do visual)**: (1) a colisão vale só no segmento do sprite (4 m ao
longo da pista): a ponta de uma fachada de prédio (12–21 m), da arquibancada (14,4 m a cada 16 m) ou da
fileira de garagens (12 m a cada 12 m, colisão num segmento de 3) pode ser atravessada por quem anda pela
grama rente a ela. (A folga do carro, `CAR_HALF_WIDTH` 0,22 = 1,54 m contra ≈ 0,95 m visuais, saiu no merge da
onda F: a colisão ficou do tamanho do carro, 0,13 = 0,91 m — `docs/FISICA.md`.)

### Chão

O cenário pousa no relevo do terreno (morros, dunas, a praia e o mar do litoral). `ground.ts` usa a conta que o
`terrain.ts` exporta (`columnRelief`, `columnDrop`, `columnDistance`: colunas, relevo, descida ao fundo do vale, a
dobra do lado de dentro da curva, o mar); o teste monta um `Terrain` de verdade e compara vértice a vértice. Era uma
cópia, e no merge da onda F o terreno novo (plano até 26 m, 12 colunas) deixou os objetos 1,25 m fora do chão.

### Composição

- **Não esconder a curva**: do lado de dentro de uma curva que vem (≥ 18 dos 80 segmentos em volta com
  curvatura > 1,2 para aquele lado) não nasce nada alto até 95 m (pontos de referência: 120 m). Do lado de
  fora a mata fica — ela desenha a curva.
- **Nada na pista de outro trecho**: o teste desenrola a linha central e confere que nenhum objeto alto cai a
  menos de 10 m do centro de nenhum trecho vizinho (grampos, curvas em S).
- **Ocupação**: a decoração não nasce em cima de sprites (grade de 2 m × 1 segmento por lado), e itens grandes
  guardam espaço entre si.
- **Sombra**: sprites (perto da pista) projetam sombra de verdade nas qualidades média e alta; tudo (sprite e
  decoração, até 60 segmentos) tem mancha de sombra no chão, azulada de dia, roxa no entardecer — que também vale
  na qualidade baixa.
- **Hora do dia**: o material do cenário recebe o mesmo filtro de cor da paleta (entardecer mais quente, noite
  azulada), para casar com o chão.

### Desempenho

Um `THREE.BatchedMesh` por material: liso com sombra (sprites), liso sem sombra (decoração), 4 fachadas,
luz, baliza, facho, painéis e manchas — **≈ 6 a 15 chamadas de desenho por viewport** para o cenário inteiro,
qualquer que seja o bioma (antes: 33 a 71, um `InstancedMesh` por modelo e parte). Cada modelo é uma
geometria do lote; cada geometria reserva o máximo de instâncias que cabem numa janela de 300 segmentos
(calculado por pista). A cada quadro, por viewport, a janela do `RoadFrame` vira matrizes; o que a névoa já
apagou nem entra. LOD: silhueta de longe para vegetação e pedra (sprites a partir de 90 segmentos, decoração
a partir de 52), forração só até 34 segmentos, cerca até 70, fios até 60. Nas qualidades baixa e média (janela
menor) só a fração correspondente dos enfeites dispensáveis entra (forração, mata, soltos — cada um com um sorteio
fixo). Números medidos em `docs/DESEMPENHO.md` (seção 7, "Cenário da pista").

### Marcos turísticos (onda G)

Cada pista tem os seus pontos turísticos (`src/core/data/places.ts`, contrato em `docs/PISTAS-TURISMO.md`): o Cristo e
o Pão de Açúcar em Copacabana, o MASP e a Ponte Estaiada em Sampa, o trem da Serra Verde na Serra do Mar, o ninho de
tuiuiú e o portal na Transpantaneira… São modelos procedurais como o resto do cenário, só visuais (sem colisão), num
registro por região: `landmarks/brasil-centro-sul.ts` (Sudeste, Sul, Centro-Oeste), `brasil-norte-nordeste.ts` e
`mundo.ts`, juntos em `landmarks/index.ts` (`LANDMARKS`). O contrato de cada um (`landmarks/types.ts`, `LandmarkDef`):
`place` (near / far / skyline), `side` (land / sea / any), `perLap` e, opcional, `turn`.

**O modelo** (convenção comum às três tarefas de modelos): origem no centro da pegada, `y = 0` no chão (alicerce ou saia
de pedra abaixo de 0 para pousar em declive), frente para `+X` (o lado que olha a pista), o comprimento ao longo de `Z`
(paralelo à pista), com `side: 'sea'` o mar fica atrás (`−X`); metros reais (skyline já na escala grande). Peças por
material: `flat` (cor por face, normal suave com o vinco de marco, 45°), `glow` (luz pintada: janelas, holofotes, cabos da Estaiada, a água
da cachoeira, o lago azul da gruta — brilha à noite), fachadas com janelas que acendem à noite (`office` na caixa do
MASP e nas torres do Congresso, `house` no casario), `beacon` (luz de topo que pisca). O kit em `landmarks/kit.ts`:
`Kit` (acumula por material e funde), `beam`/`cable` (viga ou cabo entre dois pontos), `lathe` (cúpula, cuia,
torre redonda), `facadeBox`, `hill` (morro facetado com saia). E `landmarkPart(nome)` (`landmarks/parts.ts`): a peça baixada (o tuiuiú do ninho, a
manada de búfalos, as girafas, o garimpeiro…) que o construtor põe no lugar do bicho procedural, no tamanho dele, quando o
arquivo existe (`docs/ARTE.md`, "Peças baixadas"); o marco montado passa pelo `smoothModel` com a peça dentro.

**Onde ele aparece** (`layout.ts`, `placeLandmarks`, uma vez por pista, depois dos sprites e antes da decoração):

| lugar | borda de dentro da pegada | giro para quem chega | primeiro depois da largada |
|---|---|---|---|
| near (igreja, casario, portal, árvore-símbolo) | 30–80 m | 0,30 rad | 48 segmentos (+64 por marco seguinte) |
| far (ponte, convento no penhasco, viaduto) | 120–330 m | 0,45 rad | +30 |
| skyline (Cristo, Pão de Açúcar, montanha) | 220–300 m (o pico fica a ~400 m+) | 0,35 rad | +150 (só entra na tela a ~30° uns 150 segmentos antes do ponto em que fica ao lado) |

O primeiro de cada marco fica numa **janela perto da largada** (`FIRST_WINDOW`, `core/track/startzone.ts`): o mais
importante da pista a ≤ 150 segmentos, os outros a ≤ 300 (o do horizonte, +150), contados do **fim da largada** —
arquibancadas (até o segmento 24), o trecho sem cenário (30) e o box que começa na linha (0–39 em todas as pistas de
hoje: `startZoneEnd` = 40). Contava da linha, e logo depois dela a vista dos lados é das arquibancadas e das garagens.

- **A faixa mede a borda de dentro** (o ponto do modelo mais perto da pista), não o centro: uma ponte de 1 km ou um
  tepui de 1 × 2 km continuam do lado de fora. Modelo comprido gira menos (a ponta avança no máximo 60 m).
- **Nunca no alcance do carro**: a pegada inteira (caixa do modelo girada) fica a ≥ 26 m (`LANDMARK_CLEAR_M`) do centro
  de todo trecho de pista à vista — o próprio e os vizinhos de grampo e curva em S (a linha central desenrolada,
  ±300 segmentos).
- **Praças da cidade e mirantes do litoral** (`core/track/plazas.ts`, `landmarkPlazas`): no `city_night` a receita
  enche os dois lados de prédio e torre na beira da pista — um paredão contínuo, e todo marco de perto e de longe ficava
  atrás dele (o MASP a 75 m, a Ópera de Arame, a Torre de TV… invisíveis da pista). No `coast` a receita também põe
  prédio e torre dos dois lados (um a cada ~17 segmentos por lado, mais o prédio/torre a cada 90 e os outdoors), e o
  marco de perto só aparecia nas brechas (a jangada de Maceió 0,3 s na tela, o cassino de Mônaco 0,8). São os dois
  biomas cuja receita usa prédio e torre de enchimento (`fillers`); nos outros eles são raros (um a cada 90
  segmentos). O núcleo abre, por instância de cada marco de perto/longe, um trecho sem prédio, torre nem outdoor (poste,
  moita, palmeira e placa de curva continuam) **do tamanho da linha de visada**:
  - **a conta** (`core/track/sightline.ts`, sem trigonometria — o builder roda no lockstep: soma, produto, raiz e o giro
    por segmento em série de Taylor): a câmera vem pela linha central desenrolada até um ponto do marco numa lateral de
    referência (perto 50 m, longe 150 m) e conta os passos em que ele está no quadro (os 80% do meio, como o `sight.ts`)
    e na névoa do período (490/545/380 m de dia/entardecer/noite) até somar o mínimo da tela com 10% de folga
    (2,5 s × 96 m/s × 1,1 = 264 m) — no primeiro de cada marco, só com a câmera depois da largada. Cada visada marca os
    segmentos em que cruza a faixa dos prédios da beira (9–45 m de lado), **dos dois lados**: o trecho aberto do lado
    do marco vai da primeira visada (ou da frente: 48 segmentos antes do de perto — os ~120 m em que ele cresce no
    quadro —, 24 antes do de longe) até 12 depois dele; quando a aproximação faz curva, a visada atravessa a parte de
    dentro dela e o outro lado também abre (`across`). As constantes vêm do renderizador, e o teste confere cada uma
    (unidades, quadro, névoa e a linha central contra a do `sight.ts`: < 1 cm em duas voltas);
  - **o lugar**: o primeiro de cada marco na janela do primeiro (o ideal: perto a 64 e longe a 100 segmentos do fim da
    largada, e 64 depois do primeiro do marco anterior), as outras instâncias a cada 1/praças da volta; anda de 8 em 8
    (as repetições até ±128 e no máximo 1/(4·praças) da volta, o primeiro só dentro da janela) e, na cidade, pelos dois lados — fora do box, do lado de
    dentro de curva (marco alto esconderia a pista) e da frente de outra praça do mesmo lado. Dos lugares que sobram
    fica o que mostra o marco o mínimo abrindo menos prédio (os dois lados somados, mais meio segmento por segmento
    longe do ideal); se nenhum chega lá, o que mostra mais. Sem lugar, sem praça (o layout acha outro). No litoral o
    lado é o do registro: o mar (direita) para o marco `sea`, a terra para o resto;
  - **por que a visada e não um comprimento fixo**: com 48 segmentos antes do marco o de longe ficava 0,4–1,5 s na tela
    — a visada até ele, a 150–250 m de lado, cruza a beira perto do CARRO, a (1 − 9/lateral) do caminho: com a câmera a
    380 m, a 350 m do marco. Com 100 fixos ele passava numa reta, mas numa aproximação em curva a visada cruza a beira
    antes do começo da praça (o domo de São Pedro e a Tsutenkaku depois das curvas fortes de Roma e Osaka: 0 s), ou
    cruza a beira do outro lado (a Catedral de Brasília, o Centro Geodésico de Cuiabá: 2,2–2,4 s). Medindo a visada, a
    escolha do lugar prefere a aproximação reta, e o trecho aberto sai do tamanho dela: 61–99 segmentos no de perto
    (mediana 76) e 52–162 no de longe (mediana 95), nas 132 praças; só 3 abrem também o outro lado;
  - os sorteios do `builder` são os mesmos (o número é tirado, só o sprite não entra): fora das praças a pista é
    idêntica. Quantas praças cada marco pede, de perto ou de longe e de que lado fica numa tabela do núcleo
    (`LANDMARK_PLAZAS`, o núcleo não importa o renderizador) que `tests/landmarks-pracas.test.ts` confere contra o
    registro — marco novo numa pista de cidade ou de litoral entra lá; o do horizonte não pede praça. O `placeLandmarks`
    põe o marco na praça dele (±4 segmentos, só o lado dela) e só sem lugar ali cai na busca de sempre; o teste acusa:
    todo marco de perto/longe da cidade e do litoral fica na praça dele, e na frente dele, do lado dele, nenhum prédio,
    torre ou outdoor;
  - **a arrumação**: a praça da cidade e o mirante do lado de terra do litoral viram praça (`dressPlaza`): cerca-viva de
    0,9 m logo além do gradil com uma passagem a cada 10 segmentos, canteiros de flores até ~30 m e, no marco de perto
    com espaço, uma cerca-viva emoldurando a frente — nada alto. O mirante do lado do mar é a praia aberta com um píer
    de madeira (40 m, guarda-corpo de 1,1 m, deque 1 m acima d'água) saindo da areia no caminho de quem chega
    (`dressBeachMirante`; a cerca-viva e os canteiros ficariam dentro d'água); os guarda-sóis já vêm da decoração;
  - **a colisão** muda em 48 pistas: as 20 de cidade com marcos e 28 de litoral (Copacabana não: só tem marcos do
    horizonte). No litoral só sai prédio, torre e outdoor — 1,4% a 22,5% dos prédios e torres (Maceió e Maragogi, com 10
    marcos de perto por volta — jangada ×4 e coqueiral ×6 —, os 22%); na cidade as praças mudaram de lugar e de tamanho
    (de −3,6% a +1,7%). Impressões das voltas, fantasmas e recordes dessas pistas ficam "de outra versão" (das 32 de
    antes da onda G: Sampa, Las Vegas, Baía de Tóquio, Osaka, Paris, Mônaco, Boa Esperança, Great Ocean, Sydney,
    Atlântico, Tromsø, Amalfi, Santorini e Roma); a impressão do online também muda. As 8 corridas de
    `tests/sim-golden.test.ts` não mudaram (as de Sampa e Las Vegas não encostam no que mudou). Voltas da IA
    (profissional, semente 1, 420 s, builder de antes × agora): iguais em 46 das 48 pistas, Fortaleza +0,27% e Osaka
    +0,02%; média 90,21 → 90,22 s. Montar as 109 pistas: 38 → 174 ms (a mais lenta, Maragogi, 11 ms; cada pista é
    montada uma vez e guardada).
- **Não esconde a pista**: nada com mais de 8 m do lado de dentro de curva próxima (a mesma regra da mata), e o
  marco perto pede o corredor entre a cerca e ele livre de prédio e arquibancada (senão some atrás deles, como o
  MASP atrás da fileira de prédios de Sampa); a ocupação dele e do corredor fica marcada, e a mata não nasce na frente.
- **No chão**: pousa no ponto mais baixo do chão (`groundOffset`) sob a pegada — nada flutua, a parte de cima do declive
  enterra; a primeira passada da busca prefere chão quase plano. `side: 'sea'` no litoral vai para o mar (direita),
  no nível da água; fora do litoral, qualquer lado; no litoral, o resto fica do lado de terra.
- **Espalhado**: o primeiro de cada marco logo depois da largada (o jogador vê logo), os outros a cada `1/perLap` da
  volta. A busca anda ±4 segmentos de cada vez a partir do alvo (até ¼ do espaço entre instâncias), nos dois lados
  (`any`, sorteio por hash), e numa última passada estende a faixa até o dobro; sem lugar, o marco não entra (não
  acontece nas pistas de hoje: `tests/landmarks.test.ts`). Entre os lugares válidos fica o que passa mais tempo na
  tela (ver "Na tela", abaixo). Determinístico (hash do id da pista), sem `Math.random`.
- **Id sem modelo** no registro: ignorado sem erro (as três tarefas de modelos rodam em paralelo).

**Névoa do horizonte**: o skyline fica a 400 m+ e é visto a 600–1.000 m — com a névoa do resto do cenário (0,0019/m)
chegaria 70–95% apagado. Ele vai para um lote próprio (`haze`, `runtime.ts`) cujo material conta a distância × 0,55
na névoa (`HAZE_FOG_SCALE`): entra na névoa (40–65%) como os planos do horizonte do terreno. Custa uma chamada de
desenho a mais por viewport, só nas pistas com skyline.

**Na tela** (onda H, `scenery/sight.ts`): nas capturas, boa parte dos marcos quase nunca entrava no quadro — o
primeiro de Itaúnas (dunas) e de Parintins (Bumbódromo) ficava logo depois da largada, à esquerda, atrás das
arquibancadas, e a 200 m+ de lado só entrava na beirada do quadro já perto; Storseisundet, Stigfossen, Cape Point e o
arco da Great Ocean (170–265 m de lado) na beirada do quadro ou atrás do box; o Uluru em parte atrás das mesas. A busca
pegava o **primeiro** lugar válido; agora mede todos e fica com o que a câmera mostra por mais tempo.

- **A conta** (`sightSeconds`, pura e determinística): a câmera de perseguição de verdade (`CHASE_CAMERA` de
  `camera.ts`: 2,05 m acima da pista, 7,8 m atrás do carro, olhando 18 m à frente; FOV vertical de 62° + 13° × a fração
  da velocidade) anda pela linha central a **96 m/s** (0,8 da máxima de referência: a IA profissional corre a 0,55–0,88
  em média; FOV ≈ 72°), em passos de **0,1 s**, por toda a janela em que o runtime desenha o marco (os 260 segmentos
  à frente da qualidade alta, cortados pela névoa, até 4 segmentos depois dele). Conta o instante em que **um** de 7
  pontos de amostra (o centro a 20/50/85% da altura; a meio caminho das faces que quem chega vê — a da pista, três ao
  longo dela, e a de quem vem — a 40%) está ao mesmo tempo: nos **80% do meio da largura** do quadro (16:9, o mais
  estreito do jogo — 2 jogadores são 32:9); antes de a névoa passar de **60%** (`FogExp2` 1 − e^−(ρd)², ρ = 0,0019 ×
  o do período: ~500 m de dia, ~390 m à noite; o skyline com a névoa × 0,55 do lote `haze`, ~920 m); e sem nada alto
  na linha de visada perto da pista — uma grade de alturas (`block`) com a pegada de verdade de prédio, arquibancada,
  garagem do box e outdoor (torres, finas, ficam de fora). Relevo do terreno e mata não entram (a mata é posta depois;
  ver a cunha abaixo). **O primeiro de cada marco conta a partir da largada**: ele existe para ser visto já na volta 1.
- **A meta** (`LANDMARK_SIGHT_MIN`): 2,5 s para perto e longe (≈ 240 m de pista: dá para notar e reconhecer sem tirar o
  olho da pista), 4 s para o horizonte (o cartão-postal, grande e visto de longe na névoa fina). Cada passada da busca
  mede todos os lugares válidos dela (o primeiro lateral de cada segmento × lado, como antes) e fica com o melhor; quase
  empate (0,25 s) fica com o mais perto do alvo, e um lugar com o dobro da meta encerra a busca (procurar mais só
  afastaria o marco do alvo). Abaixo da meta, a passada seguinte (com declive, sem o corredor de
  quem chega) também é medida, na faixa da primeira: o enquadramento troca chão plano por vista, nunca distância. As
  regras de antes valem todas (26 m da pista, nada alto do lado de dentro de curva, faixas, chão, praças, janela perto
  da largada).
- **A vista sem mata**: longe e horizonte ganham a cunha que as linhas de visada até a borda de dentro varrem (do
  carro a ~125 segmentos, a névoa, até onde o marco sai do quadro) livre de mata, pedras e mesas (a ocupação da
  decoração; só até 180 m, o fim da grade) — a duna de Itaúnas, baixa, sumia atrás dos coqueiros. Marcada depois de
  todos os marcos: tira a decoração, não o lugar de outro marco. O de perto já tinha o mirante dos ~160 m antes.
- **Antes × depois** (o enquadramento; as 109 pistas, 292 marcos): abaixo da meta, perto 82 → 49 de 191, longe 50 → 23 de 76, horizonte
  10 → 5 de 25 (medianas 2,7 → 4,1 s, 1,45 → 3,55 s, 4,4 → 8,0 s); só um marco perde (araucária na Cuia, 5,6 → 5,5 s). Fora do litoral e da cidade, 43 → 1. Itaúnas 0,6 →
  3,8 s, Parintins 0,5 → 3,9, Xingó 1,3 → 4,0, Piaçabuçu 1,1 → 4,0, Storseisundet 0,5 → 2,5, placa de Trollstigen
  0,1 → 4,9, Uluru 5,7 → 8,0, Pão de Açúcar 3,1 → 5,5; o Cristo (3,3 s) e o MASP (2,5 s) não mudam. Montar o layout:
  3,97 → 5,16 s nas 109 pistas (média 36 → 47 ms por pista, a mais lenta 93 → 122 ms; acima de 2× só em Camboriú,
  17 → 52 ms, Copacabana, Piaçabuçu e Jericoacoara, onde a busca mede todos os lugares porque nenhum chega ao dobro
  da meta).
- **Antes × depois** (praças do tamanho da visada, mirantes do litoral e a janela do fim da largada): abaixo da meta,
  77 → 11 de 292 — perto 49 → 1 de 191, longe 23 → 5 de 76, horizonte 5 → 5 de 25 (medianas 4,1 → 4,9 s, 3,6 → 4,1 s,
  8,0 → 8,0 s); litoral 46 → 6, cidade 30 → 4. Litoral: jangada de Maceió 0,3 → 3,3 s, coqueiral de Maragogi 0,6 →
  3,2, de Porto Seguro 0,7 → 5,1, Ver-o-Peso 0,8 → 2,8, cassino de Mônaco 0,8 → 4,0, cúpula de Santorini 0,9 → 4,4,
  farol de Ilhabela 1,3 → 3,4, forte de Natal 1,3 → 3,0. Cidade, de longe: Coliseu 0,8 → 2,6, castelo de Osaka 0,9 →
  2,6, Stratosphere 0,5 → 2,6, ponte do Rio Negro 0,4 → 2,4, Ópera de Sydney 1,1 → 2,5, Torre Eiffel 1,5 → 3,3, Torre
  de TV 1,0–1,3 → 2,7–2,8. Quinze instâncias perdem tempo (outro lugar da praça), nenhuma abaixo da meta: a menor fica
  com 2,5 s (Convento da Penha, 2,7 → 2,5). Os de longe da cidade à noite ficam perto da meta (2,5–2,8 s): a névoa
  a 390 m e o marco a 150–230 m de lado deixam ~240 m de aproximação numa reta.

**Leitura** (onda J, `tests/landmarks-leitura.test.ts`): estar na tela não basta — o marco tem de ser reconhecido. O
dono correu em Foz e "não achou as cataratas": o modelo ficava 4,4 s à vista, mas de frente era um paredão de blocos
de rocha com painéis brancos retos (caixas de 0,6 m) e a torre do elevador, a peça mais alta e clara — lia como
prédios. A vista de frente (`tests/front-view.ts`: projeção ortográfica de +X, o lado que o jogador vê, com z-buffer;
cada célula de 1 m guarda a cor sRGB da face da frente) mede o que faz uma queda d'água ser reconhecida de longe:

- a água domina a face (≥ 45% da silhueta; antes 32%, agora 50%);
- em cortinas riscadas, não painel liso — nenhum tom de água passa de 45% da água (antes um tom tinha 59%);
- a névoa cobre o pé (≥ 75% das colunas do quarto de baixo; antes 55%, agora 95%);
- a borda de cima é mata (≥ 90% das colunas têm topo verde; a torre do elevador quebrava isso de longe).

O modelo novo: cortinas que se lançam do lábio e abrem para baixo, em 5 faixas de tons de branco-azulado (os riscos),
em dois degraus e a Garganta numa queda só; rocha de basalto tomada de musgo quase toda escondida atrás da água; copas
redondas atrás do lábio; as pontas da ferradura descem em encosta de mata (o paredão visto de lado lia como muro); a
névoa é luz (`glow`, sem sombra — acesa pelo sol virava pedra cinza); o arco-íris na névoa da Garganta; sem a torre.
4.376 triângulos (teto de longe: 5.000). Os outros marcos passam pela mesma régua na revisão de legibilidade.

**Mundial** (`tests/landmarks-leitura-mundo.test.ts`): os 44 marcos do mundo passaram pela régua — folha de contato com
a câmera da pista (el 0,04, zoom 1,6) e captura no jogo a ~100 m (perto) e ~300 m (longe). 26 liam em 2 s (Torre
Eiffel, Coliseu, Ópera, Uluru, Fuji, Matterhorn…); 18 não, por quatro motivos, cada um com o seu teste: **à noite,
apagado** — a placa de Las Vegas, a pirâmide do Luxor, a Tsutenkaku, o arco da Harbour Bridge e a Catedral Ártica eram
vultos escuros contra as janelas acesas; a face, as arestas, o fuste, o arco e os painéis viraram luz (`glow`), e a
água do Stigfossen também, como a das Cataratas; **de faca ou pequeno** — o escudo da Rota 66 e a placa dos trolls,
virados 30° para +Z "para quem vem", ficavam de faca para quem chega pela direita (agora de frente: o layout já gira o
marco para quem chega), as shisas de 3,5 m foram a 5,2 m em pedestais, as girafas a 8 e 5,5 m, a Catedral 1,3×; **o
traço errado mandando** — o morro de 104 m do pagode Chureito, as copas acima da torre de Daintree, os abetos acima das
lavvu da Lapônia, o vagão baixo do diner atrás do outdoor (agora o pagode 1,35× num morro baixo, a torre acima da mata
com o telhado vermelho, as lavvu de 12 m com as faixas sami, o letreiro DINER de 17 m), e a girafa, cujo pescoço tinha
2,6× o comprimento e passava da cabeça; **sem silhueta própria** — o Drakensberg lia como a Table Mountain (crista
recortada com os contrafortes e a queda do Tugela em luz), Storseisundet era uma linha baixa (a corcova de 40 m em
260 m), a treliça das Rochosas, de barras de 0,6 m, sumia (banzos de 1,4 m em aço vermelho), o portão do Kruger tinha
a claridade do capim (cal e sapé escuro). Tempo à vista igual (±0,4 s); triângulos dentro do teto (o diner caiu de
2.764 para 1.812: as letras só na face da pista). Pendente fora dos modelos: a Tsutenkaku a 300 m e parte do arco da
Harbour Bridge ficam atrás das quadras de fundo do terreno.
- **O que a busca não alcança** (`tests/landmarks-enquadramento.test.ts` documenta, com o número de cada um):
  - **A janela perto da largada** (≤ 150/300 segmentos do fim da largada): os do mar no litoral com curva para o lado
    do mar logo depois da largada — o marco alto não fica do lado de dentro dela —, onde o único lugar na janela é antes
    da curva, com a aproximação ainda no box: farol de João Pessoa 1,9 s, Cape Point 1,6 (à noite), arco da Great Ocean
    2,3; e a praia de Alter do Chão 2,3 (tropical, sem prédio: só a janela) e a ponte do Rio Negro 2,4 (à noite). Com a
    janela 150 segmentos maior: 3,3, 2,2, 3,3, 5,4 e 2,6 s.
  - **A pirâmide do Luxor** (1,5 s, à noite, em qualquer lugar da volta): o facho de luz no céu leva a caixa do modelo a
    807 m de altura e os pontos de amostra a 160–690 m; com a névoa a 390 m só o mais baixo cabe no quadro. Seria preciso
    amostrar o modelo sem o facho.
  - **O horizonte no litoral e na cidade** não ganha mirante: a 400 m+ de lado, a visada cruza a beira junto do carro
    durante os ~400 m em que ele está no quadro — o mirante seria uma avenida de ~100 segmentos sem prédio de um lado
    por instância. O Cristo fica com 3,2–3,3 s, o morro do Pico 3,4, os prédios de Camboriú 2,7–3,2 (o teste exige
    2,5 s ali). Proposta: mirante do horizonte com a mesma conta (`sightline.ts`, lateral ~400 m, a névoa do lote
    `haze`) — muda a colisão de Copacabana, a pista da primeira corrida de referência.
  - A mata e as mesas não entram na conta (são postas depois): o de perto tem o mirante, o de longe a cunha — que só
    vai até 180 m; uma mesa além disso ainda pode ficar na frente de um marco do horizonte. As quadras de fundo da
    cidade (terreno) também não: a clareira delas vai até ~50 segmentos antes do marco.

Ver: `node tools/scenery-showroom.mjs <porta> saida.png lm:cristo_redentor,lm:masp "&gap=100&yaw=-1.1&fog=0.001"`
(ids com o prefixo `lm:`; `fog` e `ground` novos no showroom, para modelos grandes) e
`tools/render-harness.html?track=copacabana&seg=60` (o carro vai para o segmento `seg`).

### Como ver

Com `npm run dev` no ar: `node tools/scenery-harness.mjs 5174 scratch/cen [cenas] [1,4]` tira as capturas por
bioma × período (1 e 4 jogadores) e imprime o custo do cenário isolado (o mesmo quadro com e sem o grupo
`scenery`); `node tools/scenery-showroom.mjs 5174 scratch/sr.png tree:baobab,pine:snow,...` mostra modelos
soltos num gramado, sem a pista (ids em `catalog.ts`).
