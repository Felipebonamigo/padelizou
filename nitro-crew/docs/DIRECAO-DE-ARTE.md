# Direção de arte — "jogo de última geração, cartunesco"

**Diretriz do dono (06/10/2026), vale sobre todas as anteriores:** o Nitro Crew tem de **parecer um jogo de última
geração, cartunesco** — estilizado de alto acabamento, não o low-poly de faces chapadas. Ele aceita refazer o jogo
inteiro (direção de arte e pipeline gráfico) para chegar lá. Pedidos que levaram a isso: "deixar o jogo visualmente
mais bonito, menos quadrado, que dê para reconhecer bem as artes e os locais do jogo"; os desenhos do minimapa "podem
ser cartoonados, mas visualmente bonitos".

Referências de acabamento (o alvo, não para copiar): **Mario Kart 8 Deluxe**, **Crash Team Racing Nitro-Fueled**,
**Sonic Racing**, **Hot Wheels Unleashed**, **Fall Guys** (forma e cor). A referência antiga, Horizon Chase Turbo,
fica abaixo do novo alvo em acabamento.

As ondas que fazem isso estão em `docs/CRONOGRAMA.md`; o detalhe técnico de cada passo, nos cartões de `docs/ondas/`.

---

## 1. O que "cartunesco de última geração" quer dizer aqui (critérios verificáveis)

Uma mudança visual só está pronta se a captura (antes × depois, mesma câmera, `tools/capture.mjs`) mostra:

1. **Forma redonda e volumosa** — nenhuma faceta visível em carro, marco, pedra, árvore ou personagem na distância de
   jogo; bordas com bisel; silhuetas cheias e exageradas (proporções de brinquedo: rodas grandes, cabines bojudas,
   árvores de copa redonda). Faces chapadas novas não entram.
2. **Material com vida** — tinta de carro com reflexo do ambiente e brilho especular; metal, vidro e água que refletem;
   cores saturadas e harmônicas por bioma (paleta em `src/render/palette.ts`), nunca cinza "de protótipo".
3. **Luz rica** — sombra suave, oclusão de ambiente nos cantos, luz de borda separando o carro do fundo, céu com
   gradiente e nuvens volumosas; noite com luzes que brilham (bloom) sem estourar.
4. **Pós-processamento** — bloom, gradação de cor por bioma/período, vinheta leve; desfoque de movimento e de
   profundidade onde ajudam (velocidade, menus); antisserrilhado.
5. **Mundo animado** — vegetação que balança, água que corre, bandeiras, público, partículas (poeira, faíscas, nitro,
   respingos), placas e luzes que piscam.
6. **Lugar reconhecível** — todo marco passa pela régua de leitura (`tests/front-view.ts`, `docs/VISUAL.md`, "Leitura")
   e aparece bem enquadrado; a legenda nomeia o lugar; o desenho do minimapa é um ícone de cartum bonito.
7. **Interface com movimento** — transições, botões que respondem, números que contam, cartões de recompensa.
8. **Desempenho** — 60 fps no Steam Deck e em PC médio no nível de qualidade padrão; níveis de qualidade para o resto
   (`docs/DESEMPENHO.md`). Nenhum ganho visual vale travar o jogo.

## 2. Como chegar lá (caminhos permitidos)

- **Código** (three e os addons do próprio pacote `three/examples/jsm/...`, que não são dependência nova):
  materiais físicos com mapa de ambiente (PMREM), sombras, pós-processamento (EffectComposer, SSAO/GTAO, bloom,
  OutlinePass para contorno cartum se a direção pedir), malhas procedurais mais densas e suaves (subdivisão, bisel),
  shaders de água/vegetação, partículas.
- **Arte pronta, de uso livre (CC0)**: pacotes estilizados (ex.: Kenney, Quaternius, Poly Pizza CC0) e modelos da
  galeria da comunidade do Meshy marcados CC0, convertidos e validados (`tools/convert-landmark.mjs`; carros pelo
  pipeline glTF de `docs/ARTE.md`). Sempre conferir e registrar a licença de cada arquivo.
- **Arte contratada**: depende do orçamento de arte (decisão pendente do dono).
- **Proibido**: gerar modelos pela API do Meshy (gasta créditos do dono) **até a decisão nº 28 do cronograma** (piloto
  de 10 peças de cenário com teto de créditos; nunca carros, nunca em tempo de jogo); dependência de produção nova além do `three`.
- **Em avaliação (09/10/2026)**: nível Ultra para RTX, luz assada por pista e o duelo Unity 6 URP × Three.js Ultra antes
  da fatia vertical — `docs/CRONOGRAMA.md`, "Revisão visual" (R1–R6) e decisão nº 27.

## 3. Como provar

- Captura no Chromium headless (swiftshader), antes × depois, mesmas pistas e câmeras de referência: Copacabana
  (segmento 70), Foz do Iguaçu (105), Sampa à noite, uma de deserto, uma de neve, a garagem e a escolha de carro.
- Folha de contato dos marcos (`tools/landmark-sheet.mjs`) e dos carros (`?showroom=1` no harness).
- Medida de desempenho do `docs/DESEMPENHO.md` (tempo de quadro, triângulos, chamadas de desenho) antes × depois.
- O dono é o juiz final: mande as capturas e pergunte; "bonito" é dele.

## 4. Desenhos do minimapa (pistas com desenho)

Ícones de cartum em traço contínuo e curvo (Bézier), bonitos e proporcionais — não polígonos com quinas. O plano técnico
e o trabalho em andamento estão em `docs/PISTAS.md`, "Desenhos em cartum (em andamento)".
