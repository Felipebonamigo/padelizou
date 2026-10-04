# Arte em glTF (passo 2.4)

Briefing de arte (direção, paleta, carros, cenário, especificação, lotes): https://claude.ai/code/artifact/049e138a-cb57-4adb-baef-d3084b4f0d03

**Decisão (dono, 04/10/2026): não gerar 3D por IA neste jogo; baixar o orgânico pronto.** O estilo é low-poly
(referência Horizon Chase Turbo) e o código dá conta dele — prédios, pontes, torres, relevo. As formas orgânicas
(bichos, estátuas), onde o código fica quadrado, vêm de modelos **prontos da galeria da comunidade do Meshy (CC0)**,
convertidos para o estilo (seção "Marcos baixados"). **Nada de gerar pela API** — gasta créditos do dono (custou 200 em
04/10 por engano). A conta assinada é Pro (downloads da comunidade ilimitados); o download é pelo navegador logado do
dono (sessão local com o Claude in Chrome), não por aqui.

## Carros — pronto
1. **Modelo-base**: `art/templates/cars/<estilo>.glb` (13), o carro atual do jogo na convenção. `npm run car-templates` regera.
2. **Redesenhar no Blender** mantendo a convenção: metros, +Y para cima, frente em +Z; nós `body`, `wheel_fl`,
   `wheel_fr`, `wheel_rl`, `wheel_rr`, vazios `exhaust_l`/`exhaust_r`; materiais pelo nome:
   - `paint*`, `accent*`, `stripe_a*`, `stripe_b*`, `stripe_ab*`: recebem a cor do carro; a cor do material é o tom
     (branco = cor pura, cinza = um tom abaixo);
   - `headlight*`, `taillight*`: acendem; força = emissivo ÷ cor (sem emissivo = 1);
   - "popup" no nome: sobe com o farol escamoteável;
   - qualquer outro nome: peça fixa com a cor, rugosidade e metal do material. **Sem textura** nos carros.
3. **Conferir**: `npm run check-car -- arquivo.glb` (mesmo validador do jogo: pegada, rodas, luzes, escape, ≤ 3.500
   triângulos no casco — eram 2.000 até a onda I, que arredondou os carros do jogo para 2.600–3.400 —, faixas que os
   carros do estilo usam).
4. **Pôr no jogo**: copiar para `src/assets/cars/<estilo>.glb`. Entra no navegador, no build e no Electron
   (embutido como data URL: `src/render/cars/assets.ts`, atalho com teto anotado). Recusado = fica procedural, motivo no console.

Por ora a malha das rodas do .glb só dá posição e tamanho; o desenho continua o do estilo.

Código: `src/render/cars/gltf.ts` (leitura), `check.ts` (validador), `template.ts` (modelo-base), `assets.ts` (carga).
Testes: `tests/car-gltf.test.ts` — os 13 modelos-base voltam idênticos com e sem os extras (como saem do Blender).
Provado em 02/10: `gt.glb` no navegador (0 pixels diferentes do procedural), no build (`vite preview`) e no Electron (`file://`).

## Cenário e horizonte — falta (os marcos turísticos já têm o caminho: abaixo)
Mesmo caminho (convenção na seção Especificação do briefing); para árvores, prédios e horizonte o carregador ainda não existe.

## Marcos baixados (Meshy, CC0)
Um modelo pronto, realista e texturizado (dezenas a centenas de milhares de triângulos) vira o marco do jogo: low-poly,
cor chapada por face, na convenção de `src/render/scenery/landmarks/types.ts`. **Não gera nada por IA nem gasta
crédito** — traz um arquivo já pronto para o estilo do jogo (decisão de 04/10 no topo). Primeira leva escolhida:
tuiuiú (de cegonha), jacaré, búfalo (de boi), garça, girafa, rena, cavalo, troll, shisa, garimpeiro.

1. **Arquivo bruto**: em `art/raw/` — **fora do repositório** (`.gitignore`). Baixe o **GLB** (o conversor lê `.glb` e
   `.gltf` autocontido; FBX e OBJ não). Anote a página de origem e a licença (ver "Licença", abaixo).
2. **Converter** (sobe um Vite numa porta livre e usa o Chromium sem janela; ~2 s para 40 mil triângulos):
   ```
   node tools/convert-landmark.mjs art/raw/tuiuiu.glb --id tuiuiu_ninho
   ```
   Grava `scratch/marcos/<id>.glb`, `<id>.png` (prévia: o original em cima, o convertido embaixo, de frente, chegando e
   de trás, com a paleta) e `<id>.json` (triângulos antes/depois, método, escala, caixa, paleta com a fração da área de
   cada cor, avisos e problemas). Sai 0 = aceito, 2 = recusado pelo validador (motivos impressos), 1 = erro.
   - **Por id**: o marco do registro dá o lugar e, com ele, o alvo de triângulos — perto **2.500** (teto 3.500), longe
     **4.000** (teto 5.000), horizonte **2.000** (teto 2.500) — e a altura padrão, que é a do marco procedural do mesmo
     id (o arquivo entra do tamanho do que substitui). `--tris`, `--height <m>` ou `--scale <k>` mudam.
   - **Orientação**: `--front +z` (padrão: a frente do glTF; `-z`, `+x`, `-x`) diz qual lado do arquivo vai para +X, a
     pista; `--up z` para arquivo exportado com Z para cima; `--yaw <graus>` gira mais (anti-horário visto de cima).
     O comprido vai ao longo da pista (Z): se a prévia mostrar o marco de ponta para a pista, gire 90°.
   - **Cor**: `--colors 12` (tamanho da paleta), `--merge 0.12` (tons mais próximos que isso, em sRGB 0–1, viram um só;
     0 desliga), `--palette '#f2efe6,#1c1c1c,…'` (paleta fixa: cada face vai para a cor mais perto).
   - **Luz**: material emissivo vira a parte `glow` (a cor pintada que acende à noite; `--no-emissive` desliga);
     `--glow '#ffd23f'` manda as faces dessa cor da paleta para ela; `--glow-material janela` pelo nome do material.
   - `--install`: se aceito, copia para `src/assets/landmarks/<id>.glb`. `--out <pasta>` muda a saída.
   - Prova sem baixar nada: `node tools/convert-landmark.mjs --make-synthetic scratch/sintetico.glb` (estátua de ave
     num pedestal, 39 mil triângulos, textura com ruído) e converta-o como acima.
3. **Como funciona** (`scripts/lib/landmark-convert.ts`, puro e testado no Node): junta as malhas com a transformação
   dos nós; cada triângulo pega a cor da textura sob ele (média de 4 amostras, sRGB → linear) × cor do material × cor
   dos vértices; põe em pé, frente em +X, na altura, pegada centrada e base em y = 0; faz a paleta (k-means pesado pela
   área, determinístico, e junta tons quase iguais); reduz os triângulos com o simplificador do meshoptimizer que vem
   com o three (o do `SimplifyModifier`; preserva a silhueta pelo erro quadrático; com muitas peças soltas some com as
   minúsculas e, em último caso, agrupa vértices); e cada face reduzida fica com a cor de **maior área** dos triângulos
   originais mais próximos dela (moda, não média: a média de branco e preto seria cinza). Sem textura nem uv na saída.
4. **Conferir**: olhe a prévia (`scratch/marcos/<id>.png`). Depois, no jogo, com o arquivo em `src/assets/landmarks/` e
   `npx vite --port <porta>` no ar: `node tools/scenery-showroom.mjs <porta> antes.png lm:<id> '&gap=40&nolm=1'` e
   `… depois.png lm:<id> '&gap=40'`; na pista, `tools/render-harness.html?track=<pista>&seg=<n>&hud=0&pause=1` (o
   segmento do marco sai de `sceneryLayout`; o layout recalcula a posição com a caixa nova). Os harnesses expõem
   `window.landmarkAssets` (carregados e recusados).
5. **No jogo** (`src/render/scenery/landmarks/assets.ts`): cada `src/assets/landmarks/<id>.glb` substitui a GEOMETRIA
   do marco procedural `<id>`; o registro (lugar, lado, vezes por volta, giro) continua o do `LANDMARKS`. Carrega antes
   do renderizador (`main.ts` e os harnesses), embutido como data URL como os carros. Arquivo recusado = o marco segue
   procedural e o motivo vai para o console (`[marcos] …`). O validador (`landmarks/check.ts`, o mesmo que os 161
   marcos procedurais passam) confere: até 4 partes, uma por material (`flat`, `glow`, `beacon`, `cone`, fachadas
   `office`/`apartment`/`classic`/`house` com uv; o nome do material diz qual, o resto é `flat`); malha não indexada;
   cor por vértice (COLOR_0, vezes a cor do material); orçamento de triângulos do lugar; altura mínima (perto 6 m, longe
   25 m, horizonte 90 m) e máxima (150 / 1.500 / 3.000 m: pega arquivo em centímetros); pegada até 1,5 / 3 / 6 km; base
   tocando y = 0 (o que fica abaixo, até a altura do marco, cobre o declive); a origem dentro da pegada. A frente em +X
   não dá para medir: confira na prévia. Testes: `tests/landmark-gltf.test.ts`.
6. **Licença**: o CC0 da galeria (confira na página de CADA modelo — o arquivo baixado não traz a licença) cobre o
   direito autoral do arquivo; **não cobre marca nem direito de imagem**. Nada de logotipo ou marca (fachada, placa,
   carro), nada de cópia de carro real. **Cristo Redentor**: os direitos de imagem são da Arquidiocese do Rio de
   Janeiro — o modelo (procedural ou baixado) precisa de consulta jurídica antes da venda; o mesmo cuidado vale para
   outro monumento com dono da imagem. Ao instalar, registre na tabela abaixo.
7. **Limites**: uma cor por face (detalhe menor que um triângulo some — a prévia mostra); transparência da textura
   (folhas recortadas) é ignorada; `.gltf` com arquivos separados não abre (use o GLB); a prévia é do swiftshader.

| Marco (id) | Origem (página, autor) | Licença | Data |
|---|---|---|---|
| — | nenhum instalado | | |
