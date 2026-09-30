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
- à noite: marcações com brilho, poças de luz, sem nuvens; de dia o limiar do bloom fica ≥ 1 (branco ao sol não
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
- **Largada**: decalque de 76 m com o quadriculado (3 fileiras de 0,6 m entre duas linhas brancas) e um colchete
  branco na frente de cada posição do grid — as posições vêm de `gridMarks`, que o teste confere contra o
  `createRace` de verdade (se o grid mudar em `sim/race.ts`, o teste quebra).
- **Noite**: poça de luz aditiva sob cada poste (`lamp` do cenário), no asfalto, e asfalto acetinado que pega o
  brilho dos faróis. O braço do poste é modelo do `scenery.ts`; a poça se posiciona pelo sprite (x do poste + 4,4 m).

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
