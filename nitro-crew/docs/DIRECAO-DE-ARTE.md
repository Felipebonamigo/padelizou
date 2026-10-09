# Direção de arte — "jogo de última geração, cartunesco"

Esta é a **bíblia de estilo** do Nitro Crew. Ela diz o que o jogador tem de ver, com critérios que se verificam numa
captura, e não depende do motor: vale igual para o Three.js (WebGL) de hoje e para um eventual Unity 6 URP, porque descreve
o resultado (forma, luz, números, câmeras) e não a API que o produz. O motor é decidido por um duelo antes da onda L
(decisão nº 27 de `docs/CRONOGRAMA.md`); o formato do asset está em `docs/CONTRATO-DO-ASSET.md`.

## 0. Diretriz do dono

**Diretriz do dono (06/10/2026), vale sobre todas as anteriores:** o Nitro Crew tem de **parecer um jogo de última
geração, cartunesco** — estilizado de alto acabamento, não o low-poly de faces chapadas. Ele aceita refazer o jogo
inteiro (direção de arte e pipeline gráfico) para chegar lá. Pedidos que levaram a isso: "deixar o jogo visualmente
mais bonito, menos quadrado, que dê para reconhecer bem as artes e os locais do jogo"; os desenhos do minimapa "podem
ser cartoonados, mas visualmente bonitos".

**Decisões do dono de 09/10/2026 que esta bíblia incorpora** (`docs/CRONOGRAMA.md`, "Revisão visual", R1–R6, e as decisões
nº 27 e 28):
1. **Duas réguas de desempenho (R1).** O piso continua o Steam Deck e o PC médio a 60 fps. Acima dele existe o nível
   **Ultra**, para RTX (alvo: 4070 Ti a 1440p com ≥ 120 fps). O Ultra é onde o jogo mostra o máximo; os outros níveis descem dele.
2. **Luz assada por pista (R2).** Oclusão de ambiente e mapa de luz das peças estáticas saem prontos de fora, em lote.
3. **Não há artista.** A arte sai de **modelos prontos CC0**, mais o **Meshy só para o que não existir pronto**
   (lista em `docs/MESHY-LISTA.md`; nada é gerado sem o dono ver a lista; nº 28), mais **limpeza no Blender**. Isso vale
   para os carros também, que precisam de rodas separadas, camadas de pintura, nós `kit_*` e o ponto `seat`, validados por
   `npm run check-car` (R4; `docs/CONTRATO-DO-ASSET.md`).
4. **O spike WebGPU decidiu ficar no WebGL** (seção 6, "3b").
5. **O motor** (Unity 6 URP × Three.js Ultra) **será decidido por um duelo antes da L** (nº 27); Unreal está fora (R6). Por
   isso esta bíblia e o contrato não citam API de motor.
6. **O banco de prova existe** (`src/bench/`, `docs/DESEMPENHO.md` §4). Os números de FPS reais só vêm do dono, na máquina
   dele; enquanto isso, todo número de desempenho desta bíblia que não sai do código está marcado **provisório**.

As ondas que fazem isso estão em `docs/CRONOGRAMA.md`; o detalhe técnico de cada passo, nos cartões de `docs/ondas/`.

---

## 1. Referências

- **Principal: Mario Kart 8 Deluxe.** É a régua de acabamento: forma cheia e redonda, cor saturada e harmônica, luz limpa,
  pista legível a 60 fps num hardware modesto.
- **Crash Team Racing Nitro-Fueled**, para o **mundo**: cenário, marcos e vegetação com personalidade e proporção de brinquedo.
- **Hot Wheels Unleashed**, **só para o verniz**: como a tinta, o cromo e o metal do carro pegam luz e reflexo.
- **Sonic Racing** e **Fall Guys**, secundárias, para forma e cor.
- **Horizon Chase Turbo** fica **abaixo do alvo** em acabamento (era a referência antiga).
- **Imagens de terceiros ficam fora do repositório** (`docs/CRONOGRAMA.md`, princípio 8): a referência é o que o dono e o
  agente olham, nunca o que se commita.
- O **quadro de referências** é a saída do `tools/referencias.mjs` (K4): 5 câmeras de pista × 3 períodos, a garagem, a escolha
  de carro e 3 folhas de carros, com saturação e contraste medidos só como alerta.

## 2. Critérios de pronto (o que "cartunesco de última geração" quer dizer aqui)

Uma mudança visual só está pronta se a captura (antes × depois, mesma câmera, `tools/capture.mjs`) mostra:

1. **Forma redonda e volumosa** — nenhuma faceta visível em carro, marco, pedra, árvore ou personagem na distância de
   jogo; bordas com bisel; silhuetas cheias e exageradas (proporções de brinquedo: rodas grandes, cabines bojudas,
   árvores de copa redonda). Faces chapadas novas não entram.
2. **Material com vida** — tinta de carro com reflexo do ambiente e brilho especular; metal, vidro e água que refletem;
   cores saturadas e harmônicas por bioma (paleta em `src/render/palette.ts`), nunca cinza "de protótipo".
3. **Luz rica** — sombra suave, oclusão de ambiente nos cantos, luz de borda separando o carro do fundo, céu com
   gradiente e nuvens volumosas; noite com luzes que brilham (bloom) sem estourar. A forma da luz está na seção 3.
4. **Pós-processamento** — bloom, gradação de cor por bioma/período, vinheta leve; desfoque de movimento e de
   profundidade onde ajudam (velocidade, menus); antisserrilhado.
5. **Mundo animado** — vegetação que balança, água que corre, bandeiras, público, partículas (poeira, faíscas, nitro,
   respingos), placas e luzes que piscam.
6. **Lugar reconhecível** — todo marco passa pela régua de leitura (`tests/front-view.ts`, `docs/VISUAL.md`, "Leitura")
   e aparece bem enquadrado; a legenda nomeia o lugar; o desenho do minimapa é um ícone de cartum bonito.
7. **Interface com movimento** — transições, botões que respondem, números que contam, cartões de recompensa.
8. **Desempenho em duas réguas** — **60 fps no Steam Deck e em PC médio nos níveis Baixa e Média** (o piso; nível padrão
   é a Média) e, no nível **Ultra**, alvo de **≥ 120 fps a 1440p numa 4070 Ti** (R1; `src/bench/ultra.ts`; medido só pelo
   dono, até lá **provisório**). Níveis de qualidade para o resto (`docs/DESEMPENHO.md`). Nenhum ganho visual vale travar o
   jogo no piso.

## 3. Forma da luz

A luz é o que separa "cartum bonito" de "plástico chapado". Todos os níveis seguem estas cinco regras; o Ultra acrescenta
os efeitos da R1 por cima (oclusão de ambiente GTAO, reflexo em tela na água e no asfalto molhado, raios de sol
volumétricos, profundidade de campo nos menus e no pódio, desfoque de movimento, sombras em cascata, antisserrilhado temporal).

1. **Difuso em rampa suave e contínua**, **sem degraus de cel**: a luz escurece o volume em gradiente, não em faixas.
2. **O terminador tingido** (a borda entre luz e sombra): a passagem da luz para a sombra leva a **cor do bioma** (calor no pôr do sol, azul-verde na
   mata), nunca um cinza.
3. **Luz de borda** separando o carro do fundo, em qualquer período.
4. **Sombra projetada nítida, em 2 tons, na cor do bioma**: nunca preta nem cinza.
5. **Tone mapping** sai do ACES para **Neutral ou AgX**, com **gradação por bioma × período** (L1).

**Luz assada (R2).** Oclusão de ambiente e mapa de luz das peças estáticas (pista, cenário, marcos) são assados fora do
jogo, por pista e período, numa 2ª UV (`lightMap`/`aoMap` no glTF). O que se move (carros, partículas) recebe luz em tempo real
com as cinco regras acima. A luz assada sai do Blender ou do Unity em lote e é o maior salto de acabamento por real gasto,
em qualquer motor (começa em Copacabana na L e vai às 109 pistas na M).

## 4. Números

Nenhum número abaixo foi inventado: ou sai do código/teste citado, ou está **provisório** com a origem de onde virá.

| Número | Valor | Origem |
|---|---|---|
| Roda | **provisório**; hoje ~16,7% do comprimento (`docs/CRONOGRAMA.md`, linha do "Mais bonito, menos quadrado") | o alvo sai da decisão nº 7, no Marco 2, pela folha da L3 |
| Faceta visível | ≤ 15° | o teste da M3 mede |
| Saturação | o carro é o objeto mais saturado do quadro | alerta do `tools/referencias.mjs`, nunca meta (princípio 6) |
| Névoa | nenhuma até ~80 m | — |
| Casco do carro | ≤ 3.500 triângulos | `CAR_LIMITS.maxShellTriangles`, `src/render/cars/check.ts` |
| Cenário | ≤ 900 triângulos por segmento | `tests/scenery-forma.test.ts` (custo do cenário por segmento) |
| Marcos baixados | perto 2.500 (teto 3.500); longe 4.000 (5.000); horizonte 2.000 (2.500) | `docs/ARTE.md`, "Marcos baixados" |
| Piso de desempenho | 60 fps, Steam Deck e PC médio, níveis Baixa e Média | `docs/DESEMPENHO.md` §4 (o bench dá o veredito) |
| Alvo Ultra | ≥ 120 fps, 4070 Ti, 1440p — **provisório** (meta registrada, não medida) | `src/bench/ultra.ts`; o medido vem do `--bench` do dono |
| Tetos por qualidade | a preencher com o bench da K4 e o do dono | o gate da L2 os usa |
| Teto do marco-herói | a confirmar no bench da L | `docs/CONTRATO-DO-ASSET.md` |

## 5. As 7 câmeras de referência

- Captura no Chromium headless (swiftshader), antes × depois, nas **7 câmeras de referência** abaixo. As câmeras 1–5
  precisam do **vite de desenvolvimento** no ar (`npx vite --port <porta> --strictPort &`): o `tools/capture.mjs` abre
  `tools/render-harness.html`, que o `vite preview` não serve (o `dist/` só tem `index.html` e `assets/`), e a captura sai
  em branco sem erro. As câmeras 6–7 precisam de `npm run build` e de `npx vite preview --port <porta> --strictPort &`.
  Cada frente usa a sua porta (`docs/ondas/<onda>.md`).

| # | Câmera | Comando |
|---|---|---|
| 1 | Copacabana, coast, dia. Seg. 70 fixo: a entrada da fatia da L ("do seg. 70 até o Cristo"); não segue o Cristo (seg. 250 hoje) | `PORT=<porta> node tools/capture.mjs "ref-rio::track=copacabana&seg=70&hud=0"` |
| 2 | Foz do Iguaçu, tropical, dia (Cataratas no seg. 182) | `PORT=<porta> node tools/capture.mjs "ref-foz::track=foz_do_iguacu&seg=105&hud=0"` |
| 3 | Noite em Sampa, city_night (MASP no seg. 1029; na largada a captura sai preta) | `PORT=<porta> node tools/capture.mjs "ref-sampa::track=sampa_noite&seg=970&hud=0"` |
| 4 | Deserto: Cânions do Xingó (cânion no seg. 158) | `PORT=<porta> node tools/capture.mjs "ref-xingo::track=xingo&seg=110&hud=0"` |
| 5 | Neve: Passo Alpino (Matterhorn no seg. 234) | `PORT=<porta> node tools/capture.mjs "ref-alpes::track=passo_alpino&seg=180&hud=0"` |
| 6 | Garagem, 2 pilotos (tela `garage-2` do playtest de layout) | comando abaixo → `<prefixo>-garage-2-1280x720.png` |
| 7 | Escolha de carro, lobby com 1 piloto (tela `lobby-1`) | comando abaixo → `<prefixo>-lobby-1-1280x720.png` |

Câmeras 6 e 7, numa execução só: `NC_LAYOUT_ONLY=garage-2,lobby-1 NC_LAYOUT_RES=1280x720 NC_LAYOUT_TEXT=normal
NC_LAYOUT_SKIP_HUD=1 NC_LAYOUT_SHOTS=1 node scripts/playtest-layout.mjs http://localhost:<porta>/ scratch/<prefixo>`.
Tela com defeito de layout sai como `<prefixo>-FAIL-<tela>-1280x720.png`; ela grava também `<prefixo>-tight-…` (ignore).
Regra dos segmentos 2–5: 40–80 antes do marco que `npx tsx tools/landmark-sight.ts <pista>` aponta (77, 59, 48, 54).

O `tools/referencias.mjs` (K4) captura estas mesmas câmeras, nos 3 períodos, e mede saturação e contraste como alerta.

## 6. Base gráfica

**Hoje e para a L1/L2: WebGL (Three.js) com `onBeforeCompile`**, sem trocar o renderizador. O que vale para qualquer base
(WebGL, ou o motor que o duelo da nº 27 escolher) é o que está nas seções 2 a 5; a base só decide como se chega lá.

**Banco de prova.** O `?bench=1` no navegador (ou `--bench` no Electron) mede 6 cenas × 4 níveis (baixa, média, alta e
Ultra) e grava `nitro-crew-bench/1` com `warnings[]`, `verdicts[]` (meta × medido) e `runs[]`; o roteiro e a leitura do
JSON estão em `docs/DESEMPENHO.md` §4 (código em `src/bench/`). No swiftshader da nuvem ele só prova o formato: **os números
de FPS reais só vêm do dono**, e por isso todo número de desempenho desta bíblia é **provisório** até o JSON dele chegar.

### 3b. Spike WebGPU (K4, 09/10/2026)

(O número "3b" é o de antes da reescrita desta bíblia: outros documentos citam a seção assim.)

**Ficar no WebGL com `onBeforeCompile`, que é o plano da L1 como está.** Regra de migrar só se as três passarem: (1) ganho de desempenho medido, (2) custo ≤ 3 dias-base, (3) o WebGPU funciona de verdade no Electron do Deck. A (2) **falhou**: o inventário dá 5–7 dias, porque o `WebGPURenderer` não aceita `ShaderMaterial` nem `onBeforeCompile` (7 materiais, o carro, a névoa do cenário, o pós, o PMREM e o teste que lê GLSL). Isso decide sozinho, então a decisão é firme para a L1 e a L2. A (1) e a (3) ficam abertas: no Electron 44 sem `enable-unsafe-webgpu` o backend cai calado para WebGL2 (`adapter: null`), e com a flag funciona só com 1 viewport (com 4, o swiftshader perdeu o dispositivo); o ganho não se mede sem GPU. O `env.webgpu` do bench do dono (PC e Deck) só serve para reabrir a questão se a L2 achar caro demais fazer GTAO, SSR e TAA no WebGL. O WebGPU real exige Chromium ≥ ~152 (o 141 do Playwright recusa `swizzle`). A decisão de motor maior (Unity × Three.js) é outra: `docs/CRONOGRAMA.md`, "Revisão visual" e nº 27.

## 7. Caminhos permitidos

- **Código** (three e os addons do próprio pacote `three/examples/jsm/...`, que não são dependência nova):
  materiais físicos com mapa de ambiente (PMREM), sombras, pós-processamento (EffectComposer, SSAO/GTAO, bloom),
  malhas procedurais mais densas e suaves (subdivisão, bisel), shaders de água/vegetação, partículas. **Contorno:
  decisão nº 8, pela folha A/B/C da L2; o padrão é sem traço preto.**
- **Arte pronta, de uso livre (CC0)** — o caminho principal, porque **não há artista** (dono, 09/10/2026): pacotes
  estilizados (Kenney, Quaternius, Poly Pizza), a galeria da comunidade do Meshy marcada CC0, e Poly Haven / ambientCG para
  texturas e céus. Convertidos e validados (`tools/convert-landmark.mjs` para marco e peça; Blender + `npm run check-car` para
  carro). Sempre conferir e registrar a licença de cada arquivo (`docs/ARTE.md`).
- **Meshy, só para o que não existir pronto** (decisão nº 28): procurar pronto primeiro; o que faltar entra em
  `docs/MESHY-LISTA.md`, e **nada é gerado sem o dono ver a lista** (gasta créditos dele), com teto de créditos, sem carros no
  piloto, e nunca em tempo de jogo. O resultado passa pelo Blender (limpeza, remalha, rodas separadas) e pela régua de leitura.
- **Blender** (local, ou por MCP numa sessão local) para limpar, remalhar e assar a luz (R2); **Figma**, já conectado,
  para style frames, bíblia visual e desenho de HUD e menus (R5).
- **Arte contratada**: **fora do plano por ora** (não há artista; decisão do dono, 09/10/2026). Só volta se o dono mudar.
- **Proibido**: dependência de produção nova além do `three` (hoje); geração por IA em tempo de jogo; geração pela API do
  Meshy fora da lista aprovada pelo dono.
- **Em avaliação (09/10/2026)**: o duelo Unity 6 URP × Three.js Ultra antes da fatia vertical — `docs/CRONOGRAMA.md`,
  "Revisão visual" (R1–R6) e decisão nº 27. Esta bíblia vale nos dois resultados.

## 8. Como provar

- Captura no Chromium headless (swiftshader), antes × depois, nas 7 câmeras de referência da **seção 5**.
- Folha de contato dos marcos (`tools/landmark-sheet.mjs`) e dos carros (`?showroom=1` no harness).
- Medida de desempenho do `docs/DESEMPENHO.md` (tempo de quadro, triângulos, chamadas de desenho) antes × depois, e o
  banco de prova (`?bench=1`, seção 6) na máquina do dono para os números de FPS.
- O dono é o juiz final: mande as capturas e pergunte; "bonito" é dele.

## 9. Desenhos do minimapa (pistas com desenho)

Ícones de cartum em traço contínuo e curvo (Bézier), bonitos e proporcionais — não polígonos com quinas. O plano técnico
e o trabalho em andamento estão em `docs/PISTAS.md`, "Desenhos em cartum (em andamento)". A rodada 1 (os 9 desenhos da onda K,
mais o Cristo) está em `scripts/track-art.ts`, com o teste de alvo em `tests/track-art.test.ts` e a folha em `scripts/art-sheet.ts`.
