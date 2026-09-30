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
  mais claro, a base de todos dentro da névoa), com faces planas que o sol acende conforme o bioma; neve só nos
  picos da montanha; mesas no deserto; ilhas e cabos no litoral; skyline instanciada na cidade. Um material só para
  os três (antes era um material novo a cada pista).

### Céu e luz (`sky.ts`, `renderer.ts`)
- Domo com gradiente forte e o horizonte do lado do sol puxando para o brilho do período; abaixo do horizonte, a
  cor da névoa (o chão distante some sem emenda).
- **Sol** a 34° de dia (sombras longas que desenham o relevo), **grande e baixo** no entardecer (9°, à frente na
  largada, disco laranja e não branco); **lua** grande a 20° com crateras no shader. Estrelas com brilho e tom
  variados, mais densas perto do horizonte.
- **Nuvens** estilizadas numa malha só (1 chamada de desenho em vez de 7): cúmulos de barriga reta e faixas finas
  e baixas no horizonte, dois tons pelo sol, borda acesa no entardecer, derretendo na cor do horizonte. Na alta as
  bolhas são arredondadas; na média e na baixa, facetadas (¼ dos triângulos, a mesma silhueta de longe).
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

## Cenário

O que aparece na beira da pista: árvores, pedras, prédios, placas, arquibancadas, box, cercas, postes e os
pontos de referência de cada país. Tudo procedural (Three.js + canvas), low-poly de faces planas e cor
saturada, na linha de Horizon Chase Turbo. Código em `src/render/scenery/` (a porta de entrada continua
`src/render/scenery.ts`, com o mesmo contrato para o renderizador: `new Scenery()`, `group`, `setNight()`,
`update(frame, track, time)`, `dispose()`).

| arquivo | o quê |
|---|---|
| `geom.ts` | kit de modelagem: primitivas com cor por vértice, `tf`/`lean`, fusão, deformação por hash, sombreado por altura, `bandPoints` (a fatia do modelo na altura do carro) |
| `vegetation.ts` | árvores, coníferas, palmeiras, cactos (e o que os substitui por país), moitas, pedras, forração, silhuetas de longe |
| `structures.ts` | prédios com fachada texturizada, casas por região, torres (TV, mastro, farol, caixa-d'água, cata-vento), cercas, postes e fios, turbina, torii, pagode, capela, barcos, molhe, mesas |
| `props.ts` | poste de luz, outdoor, placa de curva, arquibancada com público, pórtico de largada, garagens do box, placa do box, cone; o mapa de UV do atlas de painéis |
| `textures.ts` | fachadas (4 estilos, 4 × 4 janelas por repetição, com o mapa das janelas acesas) e o atlas de painéis (8 outdoors de marcas inventadas, largada, box, chevrons, xadrez) |
| `catalog.ts` | que modelo cada sprite vira por bioma e país, as receitas de decoração, o registro de modelos (montados uma vez) |
| `ground.ts` | altura do chão — espelho do relevo de `terrain.ts` (ver "Chão") |
| `layout.ts` | onde cada objeto fica, uma vez por pista e determinístico |
| `runtime.ts` | os lotes instanciados e a pose por quadro, por viewport |

### Duas fontes de objetos

1. **Sprites do núcleo** (`track.segments[i].sprites`, do `builder.ts`): árvore, pinheiro, palmeira, cacto, moita,
   pedra, prédio, torre, poste, outdoor, placa, arquibancada, pórtico, muro do box, placa do box, cone. A posição
   e a largura são as da colisão (física); o renderizador só escolhe o modelo. O `builder` não mudou.
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
  de 10 lados com o raio de colisão, ≥ 95% dele entre os vértices) ou raízes tabulares/tronco (baobá). Antes,
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

### Como ver

Com `npm run dev` no ar: `node tools/scenery-harness.mjs 5174 scratch/cen [cenas] [1,4]` tira as capturas por
bioma × período (1 e 4 jogadores) e imprime o custo do cenário isolado (o mesmo quadro com e sem o grupo
`scenery`); `node tools/scenery-showroom.mjs 5174 scratch/sr.png tree:baobab,pine:snow,...` mostra modelos
soltos num gramado, sem a pista (ids em `catalog.ts`).
