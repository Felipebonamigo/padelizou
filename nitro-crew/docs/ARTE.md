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
tuiuiú (de cegonha), jacaré, búfalo (de boi), garça, girafa, rena, cavalo, troll, shisa, garimpeiro — que não são
marcos inteiros, e sim **peças** de cenas que já existem (o tuiuiú no ninho, a manada de búfalos…): o mesmo conversor no
modo `--part`, seção "Peças baixadas", abaixo. Este caminho (o arquivo no lugar do marco inteiro) continua valendo.

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

| Marco (id) ou peça | Origem (página, autor) | Licença | Data |
|---|---|---|---|
| — | nenhum instalado | | |

## Peças baixadas (o bicho ou a estátua dentro de um marco)
A primeira leva do Meshy (04/10) não substitui marcos: entra como **peça** de uma cena procedural que já existe — o
tuiuiú de pé no ninho da árvore, os jacarés na baía, a manada de búfalos com as garças no lombo, o par de girafas, as
renas da vila da Lapônia, o cavalo debaixo do cavaleiro da Cavalhada, o troll ao lado da placa, o par de shisas no
muro, o garimpeiro de bronze no pedestal. O resto da cena (árvore, baía, curral, cavaleiro, pedestal) continua em
código; sem o arquivo da peça, o marco usa o bicho procedural de sempre (byte a byte o de antes).

**Convenção da peça** (`src/render/scenery/landmarks/parts.ts`): metros, +Y para cima, base em y = 0, pegada
centrada na origem, a **frente (a cabeça, o rosto) em +X**; uma parte lisa só (`flat`, nada brilha), malha não
indexada, cor chapada por face (COLOR_0). Um arquivo por peça: `src/assets/landmarks/parts/<peça>.glb`.

1. **Arquivo bruto**: `art/raw/<peça>.glb` (fora do repositório, como os marcos). Anote a origem e a licença.
2. **Converter no modo peça** (o mesmo `tools/convert-landmark.mjs`, com `--part`): grava `scratch/pecas/<peça>.glb`,
   `.png` (a prévia: em cima o original, embaixo o convertido; "de frente" é a cabeça, +X) e `.json`; com `--install`,
   se o validador da peça aceitar, copia para `src/assets/landmarks/parts/<peça>.glb`. Sem `--tris`, o alvo é o da
   peça (tabela abaixo); sem `--height`/`--length`, a medida de verdade dela.
   - `--length <m>`: a medida é o comprimento ao longo da frente (X), não a altura — o jacaré.
   - `--paint '<condições>:<cor>'` (repete): pinta as faces cujo centro cai na região. Condições `x`, `y`, `z` com
     `<`, `>`, `<=`, `>=` e um número de 0 a 1 — a fração da caixa final: `y` 0 = o chão, 1 = o topo; `x` 0 = a
     traseira, 1 = a frente (a cabeça); `z` 0 = −Z, 1 = +Z. Separe as condições por vírgula; as regras valem em ordem
     e **a última que casa vence** (pinte o geral primeiro e o detalhe depois). As cores entram na paleta do relatório.
   - `--palette '#a,#b,…' --by-light`: cada face vai para o tom da paleta de **mesma claridade relativa** (o escuro no
     tom mais escuro, o claro no mais claro; o matiz não conta) — a estátua de bronze, o boi de ardósia. Sem
     `--by-light`, `--palette` continua mandando cada face para a cor mais perto.
   - `--front`: a frente do glTF é +Z (o padrão) e é como o Meshy costuma exportar; se na prévia o bicho aparecer de
     lado ou de costas em "de frente (+X, a cabeça)", troque para `-z`, `+x` ou `-x`.
3. **Os comandos da primeira leva** (prontos para quando os arquivos chegarem; as faixas do `--paint` são o ponto de
   partida — confira a prévia `scratch/pecas/<peça>.png` e ajuste os números):
   ```
   # Tuiuiú (de cegonha): pernas pretas, corpo todo branco (some o preto das asas da cegonha), cabeça e pescoço pelados
   # pretos (o bico também) e o colar vermelho na base do pescoço — só na metade da frente (x > 0,45), onde fica o pescoço.
   node tools/convert-landmark.mjs art/raw/tuiuiu.glb --part tuiuiu --height 1.6 --tris 600 --front +z \
     --paint 'y<0.4:#1b1b1b' --paint 'y>=0.4:#f4f2ea' --paint 'y>0.62,x>0.45:#1b1b1b' --paint 'y>0.58,y<=0.62,x>0.45:#c8202a' --install
   # Jacaré: 2,7 m do focinho à cauda (pelo comprimento).
   node tools/convert-landmark.mjs art/raw/jacare.glb --part jacare --length 2.7 --tris 220 --front +z --colors 6 --install
   # Búfalo (de boi): ardósia escura pela claridade (o claro do boi vira o tom mais claro da ardósia, chifres incluídos).
   node tools/convert-landmark.mjs art/raw/bufalo.glb --part bufalo --height 1.7 --tris 190 --front +z \
     --palette '#1e2124,#2e3237,#40454b,#5a6066' --by-light --install
   # Garça-branca: 1 m; pernas pretas.
   node tools/convert-landmark.mjs art/raw/garca.glb --part garca --height 1.0 --tris 150 --front +z --colors 4 --paint 'y<0.35:#2a2a2a' --install
   # Girafa: 5 m (até os ossicones); as manchas ficam na paleta (12 cores).
   node tools/convert-landmark.mjs art/raw/girafa.glb --part girafa --height 5 --tris 1200 --front +z --install
   # Rena: 1,3 m na cernelha; a altura total, com a cabeça e a galhada, ~2,1 m.
   node tools/convert-landmark.mjs art/raw/rena.glb --part rena --height 2.1 --tris 500 --front +z --install
   # Cavalo branco com sela: 1,6 m na cernelha; ~2,3 m até as orelhas.
   node tools/convert-landmark.mjs art/raw/cavalo.glb --part cavalo --height 2.3 --tris 190 --front +z --install
   # Troll (estátua): 3–4 m.
   node tools/convert-landmark.mjs art/raw/troll.glb --part troll --height 3.5 --tris 1500 --front +z --install
   # Shisa (estátua colorida, com a base dela): 1,5–2 m.
   node tools/convert-landmark.mjs art/raw/shisa.glb --part shisa --height 1.8 --tris 1200 --front +z --install
   # Garimpeiro (estátua de bronze): 2,5 m, quatro tons de bronze pela claridade.
   node tools/convert-landmark.mjs art/raw/garimpeiro.glb --part garimpeiro --height 2.5 --tris 2000 --front +z \
     --palette '#4a3520,#6e5030,#9a7444,#c8a060' --by-light --install
   ```
   Prova sem baixar nada: `--make-synthetic scratch/boi.glb --kind boi` (ou `girafa`; `estatua` é a ave no pedestal) e
   converta como acima.
4. **No jogo**: `loadLandmarkParts` (`landmarks/assets.ts`) carrega as peças antes do renderizador (`main.ts` e os
   harnesses, logo antes dos marcos inteiros; embutidas como data URL), confere cada uma com o validador da peça
   (`check.ts`, `checkLandmarkPart`: o nome é de uma peça; malha não indexada, com cor por vértice; triângulos até o
   teto da peça; a medida entre metade e o dobro da de verdade — pega centímetros; base em y = 0; pegada centrada) e
   registra as aceitas; recusada = a cena segue procedural e o motivo vai para o console (`[peças] …`; os harnesses
   expõem `window.landmarkParts`). Se uma peça mudar depois de um marco montado, o catálogo esquece os marcos e as
   medidas deles (`onLandmarkPartsChange`). O marco montado com a peça passa pelo `smoothModel` como sempre (o bicho
   curvo sai liso, o vinco de marco de 45°).
5. **Como os construtores usam a peça** (`landmarkPart(nome)` pelo `kit.ts`; `peça.at(encaixe, x, y, z, giro)` devolve
   uma cópia): a peça entra **no tamanho do bicho procedural que ela substitui** — pela altura, pelo comprimento ou pelo
   lombo (o ponto mais alto da faixa do meio do comprimento: onde o cavaleiro senta) —, então a cena fica com as
   proporções em que foi desenhada qualquer que seja a altura usada na conversão; e com a cabeça (+X) para onde o
   procedural olhava. Determinístico (nada sorteado).

   | peça | marco (cena) | instâncias | encaixe | alvo / teto de triângulos |
   |---|---|---|---|---|
   | tuiuiu | `tuiuiu_ninho` | 3 (os de pé: o do ninho e os dois pescando; o de asas abertas e o em voo seguem procedurais) | altura 1,6 m × 2,2 | 600 / 800 |
   | jacare | `jacare` | 8 (6 do bando na prainha + 2 nadando; os outros 6 saem) | comprimento 2,7 m × 2,1 | 220 / 250 |
   | bufalo | `bufalo` | 9 (8 da manada + o do vaqueiro; a manada de 11 fica em 8) | lombo 1,925 × 1,7 (o vaqueiro senta) | 190 / 220 |
   | garca | `bufalo` | 3 (a vaqueira no lombo de 3 búfalos escolhidos; as do alagado seguem procedurais) | altura 0,5 m × 1,7 | 150 / 160 |
   | girafa | `girafa` | 2 (a menor × 0,7) | altura 6,2 m | 1.200 / 1.600 |
   | rena | `vila_lapponia` | 3 | lombo 1,475 × 1,3 | 500 / 600 |
   | cavalo | `cavalhada` | 6 (3 por esquadrão; o 4º de cada sai) | lombo (a sela) 2,2 × 2 (o cavaleiro procedural senta; a manta do time fica) | 190 / 215 |
   | troll | `placa_trolls` | 1 | altura 4,3 × 1,1 | 1.500 / 2.000 |
   | shisa | `shisa` | 2 (nos pilares, um pouco viradas uma para a outra) | altura 2,6 × 1,35 sobre o pilar | 1.200 / 1.600 |
   | garimpeiro | `monumento_garimpeiro` | 1 (no pedestal) | altura 4,05 × 2,3 | 2.000 / 2.500 |

   **Orçamento** (marco de perto: 3.500 triângulos): o teto de cada peça sai do resto da cena dividido pelas
   instâncias, e o teste monta cada marco com todas as peças no teto — tuiuiu_ninho 828 + 3 × 800 = 3.228; jacare
   1.424 + 8 × 250 = 3.424; bufalo 1.012 + 9 × 220 + 3 × 160 = 3.472; girafa 2 × 1.600 = 3.200; vila_lapponia 1.526 +
   3 × 600 = 3.326; placa_trolls 90 + 2.000 = 2.090; shisa 184 + 2 × 1.600 = 3.384; cavalhada 2.158 + 6 × 215 = 3.448;
   monumento_garimpeiro 740 + 2.500 = 3.240. Peça acima do teto é recusada com o `--tris` a usar.
6. **Conferir no jogo**: com a peça em `src/assets/landmarks/parts/` e `npx vite --port <porta>` no ar (nunca 5174,
   4174 ou 5601): `node tools/scenery-showroom.mjs <porta> antes.png lm:<marco> '&gap=60&nolm=1'` (procedural) e
   `… depois.png lm:<marco> '&gap=60'`. Provado em 04/10 com peças sintéticas (boi → búfalo de ardósia, girafa)
   instaladas e tiradas depois.
7. Testes: `tests/landmark-parts.test.ts` (validador, carregador, os 9 construtores com e sem a peça, orçamento,
   caches, sombreado; no conversor, o modo peça, `--length`, `--paint` e `--by-light`).
