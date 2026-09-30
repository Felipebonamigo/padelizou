# Visual (Fase 2, onda F)

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
grama rente a ela; (2) `CAR_HALF_WIDTH` (0,22 = 1,54 m) é maior que a meia largura visual do carro (≈ 0,95 m),
então o carro bate com 0,6 m de folga visual — é o mesmo defeito que o dono viu entre carros (tarefa dos carros).

### Chão

O cenário pousa no relevo do terreno (morros, dunas, a praia e o mar do litoral). `ground.ts` repete a conta
de `terrain.ts` (colunas, `relief`, a dobra do lado de dentro da curva, o mar); o teste monta um `Terrain` de
verdade e compara vértice a vértice. Se o relevo mudar lá, o teste aponta para cá — o certo é o `terrain.ts`
exportar a função e o `ground.ts` usá-la.

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
fixo). Números medidos em `docs/DESEMPENHO.md` (seção 5).

### Como ver

Com `npm run dev` no ar: `node tools/scenery-harness.mjs 5174 scratch/cen [cenas] [1,4]` tira as capturas por
bioma × período (1 e 4 jogadores) e imprime o custo do cenário isolado (o mesmo quadro com e sem o grupo
`scenery`); `node tools/scenery-showroom.mjs 5174 scratch/sr.png tree:baobab,pine:snow,...` mostra modelos
soltos num gramado, sem a pista (ids em `catalog.ts`).
