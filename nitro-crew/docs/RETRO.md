# Modo Retrô (pseudo-3D)

Visual opcional no estilo Top Gear / Out Run, pedido pelo dono em 30/09/2026 **ao lado** do 3D, não no lugar
dele. Liga em **Opções › Visual › Retrô**; a troca é na hora (inclusive no meio de uma corrida) e fica salva
(`Settings.renderStyle`: `'modern'` padrão, `'retro'`). O núcleo, o HUD, a entrada, o áudio e o online não mudam:
o Retrô é só outra implementação da interface `Renderer` (`src/game/contracts.ts`).

## Arquivos (`src/render-pseudo3d/`)

| Arquivo | O que tem |
|---|---|
| `projection.ts` | Matemática pura do Javascript Racer: constantes da câmera, `projectPoint`, `projectRoad`, `placeOnRoad`, `roadYAt`. Sem DOM — testada em `tests/retro-projection.test.ts`. |
| `palette.ts` | Cores chapadas por cenário (`SceneryId`) e período (`TimeOfDay`): céu, chão, zebra, asfalto, névoa, silhuetas. Separada da paleta do 3D. |
| `sprites.ts` | Pixel art procedural num canvas pequeno: 15 tipos de objeto do cenário (`SpriteKind`) e a traseira dos carros por carroceria (`CarBody`), em 3 poses de curva, com lanterna de freio e faróis à noite. `SpriteAtlas` guarda o que já desenhou. |
| `renderer.ts` | `RetroRenderer implements Renderer` e `createRetroRenderer(canvas, hudRoot)`. |

## Como desenha um quadro

Por viewport (1 a 4, retângulos de `viewportRects`, os mesmos do 3D):

1. **Câmera** `PLAYER_Z` atrás do carro do jogador, na altura da pista (`roadYAt`) + `CAMERA_HEIGHT`, deslocada em
   `x` pelo `car.x` (−1..1 da meia-largura) × `ROAD_WIDTH`.
2. **Projeção** (`projectRoad`, da frente para trás): a partir de `segmentAt(câmera.z)`, cada segmento vira um trapézio
   entre `p1` (borda perto, `y0`) e `p2` (borda longe, `y1`). A curva é acumulada como no JR: `x += dx; dx += curve`,
   começando com `dx = −curve × fração já percorrida` para a curva não "pular" na troca de segmento. `maxY` recorta o
   que está escondido atrás de uma crista. Névoa exponencial pela distância.
3. **Pintura de trás para a frente** (algoritmo do pintor) numa tela pequena de 180/224/270 linhas (qualidade
   baixa/média/alta): fundo (degradê do céu, estrelas, sol/lua, cordilheira e morros ou a silhueta da cidade, em
   paralaxe pelo `absoluteHeading` da pista), e depois, do segmento mais longe ao mais perto: chão, zebras, asfalto,
   faixa do box e marcações — alternando as cores pela `band` do segmento —, os sprites daquele segmento (ancorados no
   pé, em `p1`) e os carros que estão nele (o mais longe primeiro). Assim o que está perto cobre o que está atrás,
   inclusive morro na frente de carro.
4. **Ampliação** da tela pequena para o retângulo do viewport sem suavização (pixel grande de propósito).

O HUD é o mesmo do 3D (`src/render/hud.ts`, DOM por cima). O fantasma do contra-relógio sai translúcido no primeiro
viewport. Outros humanos ganham a etiqueta P2/P3/P4 na cor do assento.

## Troca em `session.ts`

`makeRenderer(style, canvas, hudRoot)` escolhe entre `createRenderer` (3D) e `createRetroRenderer`. Quando
`settings.renderStyle` muda, `swapRenderer()` descarta o renderizador atual, **troca o `<canvas id="game">` por um
novo** (um canvas que já deu `getContext('webgl2')` não aceita `'2d'`, e vice-versa) e cria o outro. Quem guardou o
renderizador pega sempre o atual pelo getter `session.renderer`.

`Hud.dispose()` remove só os nós que o próprio HUD criou: o painel do tutorial e o HUD do online moram no mesmo
`#hud` e sobrevivem à troca (defeito visto no playtest antes da correção).

O seletor **Visual** fica em Opções › Geral, logo abaixo de Qualidade (que no Retrô escolhe as linhas da tela
pequena e a distância de desenho). Para caber — cada coluna comporta dez linhas, `docs/TELAS.md` —, o "Tremor de
tela" foi para Opções › Acessibilidade, ao lado de "Reduzir efeitos".

## Provas

- `tests/retro-projection.test.ts` — projeção: fórmula clássica, reta centrada que afina até o horizonte, curva
  positiva desloca para a direita, subida levanta a estrada e a crista esconde a descida, câmera deslocada, carro do
  jogador em x = +1 na borda direita, fim da volta sem salto.
- `tests/render-style.test.ts` — `renderStyle`: padrão moderno, `'retro'` guardado, valor inválido volta ao padrão.
- `scripts/playtest-retro.mjs` (com `npm run preview` no ar): Opções › Visual → Retrô pelo teclado, troca na hora,
  canvas novo, HUD de fora intacto, fundo dos menus, quatro cenários (dia, noite na cidade, crista no cânion, neve),
  tela dividida em 2 e 4 com o HUD de cada um, volta ao Moderno e escolha salva. Capturas em `scratch/pr-*.png`.

## Limites conhecidos

- O fundo (montanhas, cidade) é gerado por pista e não segue a geografia do cenário 3D.
- Carros e objetos são pixel art procedural; arte desenhada à mão entra trocando `sprites.ts`, sem mexer no resto.
- Sem sombra, reflexo, chuva ou partículas além de poeira e chama do nitro — de propósito, é o visual de 16 bits.
