# Desempenho (passo 1.6)

O que dá para medir sem GPU de verdade: o custo de CPU da simulação e os vazamentos numa sessão longa.
O que depende de placa de vídeo (quadros por segundo com 4 viewports a 1080p/1440p, notebook com gráfico
integrado, Steam Deck) fica para o Felipe medir na máquina dele — o roteiro está no fim.

Medido em 28/09/2026 num Xeon de 2,1 GHz com 4 núcleos, **dividido com outras sessões** (carga média de 13 a
18 durante as medições): os tempos de parede aqui oscilam muito, por isso as ferramentas separam o custo do
próprio código do que o sistema operacional tira do processo (ver "intrínseco" abaixo).

## 1. Simulação (CPU)

`npx tsx scripts/perf-sim.ts` mede cada `stepRace` em corridas inteiras de 20 carros (2 voltas em Copacabana,
Noite em Sampa e Daintree), com 1 a 4 humanos de roteiro (sem `car.ai`, então direção assistida, câmbio e tomada
pela IA passam pelo caminho real) e cinco variações: **básico** (IA sem personalidade, sem assistências),
**personalidades**, **assistências** (co-op todas + direção assistida mista), **assist. completa** (todos em
`full`) e **modo de festa** (escolta com 1 e 3 humanos, revezamento com 2 e 4).

Duas leituras por tick:
- **intrínseco** — a mesma corrida roda 3 vezes (é determinística: os mesmos ticks) e fica o menor tempo de
  cada tick. Tira a preempção e o GC; o p99 e o máximo dessa leitura são os ticks realmente caros.
- **bruto** — o tempo da primeira repetição, com tudo. Nesta máquina ocupada ele chega a dezenas de ms num
  tick isolado: é o SO parando o processo, não o código (a pausa de GC observada na mesma medição é de 0 a 1
  por corrida, a maior de 0,6 a 17 ms).

Resultado depois da otimização (µs por tick, intrínseco):

| variação | hum. | média | p50 | p99 | p99,9 | máx. | 4 ticks seguidos |
|---|---|---|---|---|---|---|---|
| básico | 1 | 37 | 35 | 81 | 93 | 104 | 384 |
| básico | 4 | 30 | 30 | 43 | 58 | 68 | 236 |
| personalidades | 1 | 36 | 36 | 60 | 76 | 98 | 295 |
| personalidades | 4 | 30 | 30 | 39 | 45 | 68 | 185 |
| assistências | 2 | 37 | 37 | 63 | 75 | 87 | 298 |
| assist. completa | 4 | 34 | 34 | 45 | 60 | 82 | 263 |
| modo de festa (revezamento) | 4 | 36 | 35 | 63 | 69 | 82 | 280 |

(tabela completa: rode o script; as 20 linhas ficam entre 30 e 37 µs de média e 39 a 81 µs de p99.)

**Pior quadro**: o laço de passo fixo roda até 4 ticks num quadro atrasado; o pior caso medido foi **0,5 ms**
dos 16,7 ms de um quadro a 60 Hz (3%). A simulação não é o gargalo em nenhuma combinação — nem com 4 humanos,
assistência completa e revezamento. No navegador (Chromium, sessão inteira: `stepRace` + telemetria das
conquistas + eventos + vibração), `debugStep(2400)` com 4 humanos levou 448 ms = **187 µs por tick**
(medido com o Profiler do CDP em volta do `debugStep`): 4 ticks ≈ 0,75 ms por quadro no pior caso.

### O que foi otimizado (sem mudar um bit)

Perfil (`node --cpu-prof` no bundle do `perf-sim`, tempo próprio por linha): o `%` de ponto flutuante (fmod,
que o V8 não faz em linha) era o custo número um — `segmentAt` (chamada ~60 vezes por carro por tick no laço
de frenagem da IA), `wrappedDelta` (20×20 por tick no vácuo, mais as colisões e o desvio) e `maxCurveAhead`.

- `fmodFast(v, len)` em `track/builder.ts`: devolve `v` em (−len, len) e `v − len` em [len, 2·len) — os dois
  casos em que o fmod dá exatamente isso (a subtração é exata pelo lema de Sterbenz) — e cai no `%` fora
  deles. `segmentAt`, `maxCurveAhead` e `wrappedDelta` passam por ele; o `% n` inteiro do índice só roda
  quando o índice passa de `n`.
- `stepRace` não lê mais `inputs[-1]` para a IA (índice negativo vira busca de propriedade por nome).

Ganho medido em A/B intercalado — base (1605294) e otimizado alternados, `perf-sim --quick` (Copacabana, as 20
linhas), 2 rodadas de cada lado, fica o menor valor de cada: **−40% na média (45 → 27 µs por tick), −35% no p99
(60 → 39 µs) e −34% no pior quadro de 4 ticks (272 → 179 µs)**, médias das 20 linhas; nenhuma linha piorou, e o
pior quadro de todos caiu de 375 para 233 µs. (A tabela acima foi medida antes, com a máquina mais carregada e em
três pistas: os números absolutos das duas medições não se comparam, só os do A/B entre si.) O que sobra no perfil é
o trabalho de verdade (o laço de frenagem da IA lendo os segmentos, a raiz quadrada por curva).

### A trava: corridas inteiras com impressão digital fixa

`tests/sim-golden.test.ts` roda 8 corridas inteiras (`scripts/sim-scenarios.ts`: solo sem assistências; co-op
de 4 com tudo e rival; versus com câmbio manual; escolta; revezamento; tomada pela IA no tick 900;
contra-relógio; IA sem personalidade) e compara um hash de cada tick, de todos os eventos e do estado completo
a cada 600 ticks e no fim com valores gravados **antes** da otimização. Conferido que pega mudança mínima:
somar 1e-7 à `CENTRIFUGAL` derruba as 8 corridas. Também confere `fmodFast`, `segmentAt`, `maxCurveAhead` e
`wrappedDelta` contra as fórmulas originais bit a bit (`Object.is`), com bordas (±0, ±len, 2·len, o double
logo abaixo de len, NaN, ±∞) e 18 mil valores.

Mudou a jogabilidade de propósito? O hash muda: `npx tsx scripts/perf-sim.ts --fingerprints` imprime os novos
valores; confira que só mudaram as corridas que deviam e atualize o teste.

### Onda F: colisão do tamanho do carro (`docs/FISICA.md`)

Só simulação (o renderizador não mudou: draw calls, triângulos e texturas iguais). `perf-sim --quick`, base
(e3e63b1) e depois, um em seguida do outro na mesma máquina carregada (leitura intrínseca, 3 repetições): média
por tick +2 a +5 µs nas 20 linhas (ex.: básico com 1 humano 43 → 46 µs; modo de festa com 2, 46 → 51), ~+7% — a IA agora
olha quem está do lado antes de mudar de faixa (`sideBlocker`, um laço pelos 20 carros) e a resolução de colisões
faz uma segunda passada nos ticks com contato. Pior p99 81 → 75 µs; pior quadro de 4 ticks 399 → 397 µs, de
16.667 µs a 60 Hz.

## 2. Vazamentos numa sessão longa

`node scripts/playtest-memoria.mjs <url> <prefixo> [blocos=36]` (com `npx vite build && npx vite preview`):
Chromium headless com `--enable-precise-memory-info --js-flags=--expose-gc`, 36 blocos = **72 corridas**, uns
20 minutos aqui. Cada bloco larga uma corrida rápida de 1 volta, anda 260 ticks (`debugStep`), desenha,
**reinicia**, anda de novo; a cada 4 blocos vai **até o resultado** (P1 no teclado com direção assistida
completa); volta ao **menu principal** e abre 9 telas (lobby, copas, pistas, opções, controles, recordes,
créditos, festa, acessibilidade). Troca de **pista** a cada bloco (6 cenários/horários), de **1 a 4 jogadores**
e de qualidade (baixa, baixa, **alta**) — o roteiro se repete a cada 12 blocos. Os quadros são desenhados pelo
roteiro (`session.frame`), com o laço do requestAnimationFrame parado.

As corridas são as mesmas a cada ciclo: a semente da corrida é `Date.now() ^ Math.random()`, então o roteiro
fixa o `Date.now` da página (só a semente o usa) e dá semente ao `Math.random`, posta de novo na mesma chamada
que larga, reinicia ou volta ao menu — os sons também sorteiam, em tempo real, e uma semente posta noutra
chamada já chegaria gasta. (Sem isso as corridas mudavam de um ciclo para o outro, e numa rodada geometrias e
materiais subiram 1 no terceiro ciclo sem se repetir — o mais provável é algo do cenário desenhado pela primeira
vez; com as corridas fixas, sumiu.)

No fim de cada bloco, no menu principal, depois de coletar o lixo (`gc()` e `HeapProfiler.collectGarbage`):
heap JS (`performance.memory.usedJSHeapSize`), `renderer.info.memory` (geometrias e texturas),
`renderer.info.programs` (programas na GPU), **materiais vivos** no renderizador, objetos na cena, nós do DOM
(vivos e os que o Chrome ainda guarda, `Memory.getDOMCounters`), ouvintes de evento e os filhos do `#hud` e do
`#ui`. **Veredito**: o primeiro ciclo é aquecimento; o último ciclo inteiro é comparado com o anterior, posição a
posição (24 corridas depois, mesmo ponto do roteiro). Contador que cresce nas 12 posições cresce sem parar —
**falha**. Um degrau isolado de até 3 (algo desenhado pela primeira vez: o fundo dos menus anda com o relógio
acumulado e pode mostrar um trecho de cenário novo) sai como aviso. O heap falha se crescer mais de 1,5 MB.

Duas escolhas de medição, porque as óbvias davam alarme falso:
- **Materiais**: o three não os conta. O roteiro envolve `renderer.properties` e conta entradas de material
  criadas menos removidas — um material ganha entrada ao ser desenhado e só a perde no `dispose()`. Material
  largado sem `dispose()` some do heap (a entrada é de um WeakMap), mas o programa dele fica preso na GPU, e
  essa conta cresce. A primeira versão somava o `usedTimes` dos programas e acusava um crescimento de 1 a 5 por
  ciclo mesmo depois das correções: rastreando cada material, eram sempre os mesmos (placas do cenário,
  rótulos dos carros, estrelas, prédios) ganhando um programa por configuração nova em que são desenhados
  (com/sem sombra, na tela/no alvo do bloom — até 4 cada, no que foi medido), sem nenhum material a mais. Isso
  tem teto e não vaza; a soma de `usedTimes` saiu do veredito.
- **Ouvintes de evento**: cada nota da música é um nó de áudio com ouvinte de `ended`, e o sequenciador
  agenda notas 100 ms à frente sem parar — com a música tocando, a contagem variava de 50 a 72 conforme o
  instante. A música cala durante a medição (2,5 s) e volta em seguida; no resto do bloco ela toca.

### Antes da correção (o roteiro falhando)

Com o build de 1605294:

```
ciclo dos blocos 12–23 contra o dos blocos 24–35 (24 corridas depois):
✗ textures: 332 → 475 no fim do ciclo; cresceu em 12 de 12 posições, no máximo +143
✗ materials: 304 → 445 no fim do ciclo; cresceu em 12 de 12 posições, no máximo +141
✓ geometries 108 → 108 · programs 103 → 103 · objects 127 → 127 · domLive 528 → 528 · domNodes 753 → 753
✓ listeners 47 → 47 · hud 490 → 490 · ui 21 → 21 · heap JS 25,34 → 25,91 MB (+586 KB em 24 corridas)
```

Cada ciclo deixava 143 texturas (13 composers recriados por ciclo × 11 alvos do bloom) e 141 materiais (os do
bloom e da saída de cada composer, mais o dos anéis a cada troca de pista) na GPU, sem parar: 17 texturas no
início, 475 depois de 72 corridas.

### Os defeitos

1. **Pós-processamento da qualidade alta** (`render/renderer.ts`). Cada viewport tem um `EffectComposer`
   (render → bloom → saída), recriado quando muda o tamanho do viewport (lotação 1→4, janela) e descartado ao
   trocar de qualidade. O `EffectComposer.dispose()` do three só libera os dois alvos dele: o
   `UnrealBloomPass` (11 alvos de render — 5 níveis × 2 direções + o brilho — e 9 materiais) e o
   `OutputPass` ficavam na GPU. E o `dispose()` do próprio `UnrealBloomPass` (three r186) esquece o material do
   filtro de brilho (`materialHighPassFilter`). Numa noite de sofá trocando de 1 para 4 jogadores em qualidade alta, isso é
   memória de vídeo que só cresce (cada alvo de bloom é meia-precisão do tamanho do viewport). Correção:
   `disposeComposer` libera os passes (e o filtro de brilho) antes do composer.
2. **Material dos anéis do horizonte** (`render/terrain.ts`, `buildFar`): um material novo a cada troca de
   pista, e só as geometrias eram liberadas. O material antigo não pesa no heap (o three guarda as
   propriedades num WeakMap), mas o programa dele nunca era solto (`usedTimes` só sobe). Correção: libera o
   material antigo junto.

### Depois da correção

Mesmo roteiro, build com as duas correções:

```
ciclo dos blocos 12–23 contra o dos blocos 24–35 (24 corridas depois):
✓ geometries 108 → 108 · textures 79 → 79 · programs 103 → 103 · materials 50 → 50 · objects 127 → 127
✓ domLive 528 → 528 · domNodes 753 → 753 · listeners 47 → 47 · hud 490 → 490 · ui 21 → 21
  (todos: cresceu em 0 de 12 posições)
✓ heap JS: 24,60 → 24,73 MB no fim de cada ciclo (+129 KB em 24 corridas; folga 1536 KB)
✓ sem erros na página (0)
Sem crescimento entre ciclos.
```

Do bloco 10 em diante, cada contador repete exatamente o valor do mesmo ponto do ciclo anterior (qualidade
baixa: 27 texturas e 14 materiais; alta com 4 viewports: 79 e 50). O heap sobe ~130 KB por ciclo e desacelera
(caches do V8, compilação); a folga de 1,5 MB é 12 vezes isso. A tela final, depois das 72 corridas, é o menu
principal desenhado normalmente.

### O que foi olhado e está limpo

HUD (nós criados uma vez por assento e reusados; 490 nós com 4 jogadores, estável), menus (cada tela
desmonta com `destroy`, os ouvintes de `window` da tela de controles saem), rótulos dos carros (a textura
antiga é liberada ao trocar o nome), partículas (pools fixos), céu (o env map do PMREM é liberado a cada
pista), cenário (conjuntos instanciados criados uma vez). Observação sem vazamento: `Road.setPalette` libera
a textura xadrez da largada que o material ainda usa e cria outra que ninguém usa — o three reenvia a antiga
na próxima vez que desenha, então o número não cresce; fica anotado para a limpeza da Fase 2.

## 3. O que não dá para medir aqui

- **Quadros por segundo de verdade.** O Chromium daqui desenha por software (swiftshader, ~1 quadro/s a
  1280×720): não diz nada sobre GPU, sombras, bloom ou 4 viewports a 1080p/1440p.
- **Steam Deck e notebook com gráfico integrado**, memória de vídeo, temperatura/estrangulamento em 1 hora.
- **Memória de vídeo em si**: o roteiro conta objetos do three (texturas, geometrias, materiais, programas), não
  bytes na GPU.

## 4. Roteiro para o Felipe medir na máquina dele

1. `npm run build && npm run preview` e abra `http://localhost:4174/` no Chrome (ou o build do Electron).
2. Contador de quadros: DevTools → ⋮ → More tools → Rendering → **Frame Rendering Stats**. No console,
   `window.nc.session.renderer.debugInfo()` dá chamadas de desenho, triângulos e o tempo de CPU do último
   quadro.
3. Para cada qualidade (Opções → baixa/média/alta) e cada lotação (1, 2, 3, 4 jogadores; tela cheia a 1080p
   e, se tiver, 1440p): uma corrida de Noite em Sampa (cidade à noite, a mais pesada) e uma de Copacabana.
   Anote o FPS típico e o pior nos trechos cheios (largada, pelotão numa curva).
4. **Steam Deck**: o mesmo em 1280×800, 1 e 2 jogadores, qualidade baixa e média; anote também o consumo
   (menu de desempenho do Deck, nível 4).
5. **Uma hora**: deixe uma copa inteira rodando (ou várias corridas seguidas trocando de pista e de
   lotação) com o DevTools → Memory → "Performance monitor" aberto (JS heap size, DOM Nodes, JS event
   listeners). Crescimento contínuo nesses três, ou o jogo ficando mais lento, é defeito — mande o número.
6. Para repetir a medição de CPU da simulação: `npx tsx scripts/perf-sim.ts` (uns 3 minutos) e
   `node scripts/playtest-memoria.mjs http://localhost:4174/ scratch/mem` (uns 20 minutos no headless).

**Banco de prova (`--bench`, onda K, K4).** Windows: feche o jogo (instância única) e, na pasta do jogo, rode `"Nitro Crew.exe" --bench`. Abre em tela cheia, mede 6 cenas × 4 níveis (baixa, média, alta e **Ultra**) em uns 5–12 min, sem teto de quadros; não toque em nada. No fim o painel mostra o arquivo e os botões "Abrir pasta" e "Sair". O JSON fica em `%APPDATA%\Nitro Crew\bench\bench-AAAAMMDD-HHMMSS.json`: mande-o. Para medir tudo em 1440p: `"Nitro Crew.exe" "--bench=res=2560x1440"`; para uma parte: `"Nitro Crew.exe" "--bench=cenas=copa-1p,copa-4p&q=medium"`. Steam Deck (Modo Desktop): `./nitro-crew --bench=q=low,medium,high` (se não abrir, `--no-sandbox --bench=…`; arquivo em `~/.config/Nitro Crew/bench/`), na tomada e no perfil padrão.

**Como ler o JSON:** 1º `warnings[]` ("teto de quadros em N fps" = o número não vale); 2º `verdicts[]` (meta × medido: Baixa e Média 60 fps, Ultra 120 fps a 1440p, a Alta sem meta); 3º `runs[]` (`avgFps`, `low1Fps`, `calls`, `triangles`, `renderW×renderH`, `viewports[]`); 4º `env` (`gl` = a GPU, `webgpu`, `uncapped`). **Ultra:** meta 4070 Ti a 1440p com ≥ 120 fps (`src/bench/ultra.ts`). Enquanto a L2 não implementa os 7 efeitos (GTAO, reflexo em tela, raios volumétricos, profundidade de campo, desfoque de movimento, sombras em cascata de 4K, antisserrilhado temporal), o Ultra mede a Alta desenhada em 1440p: é a folga que sobra antes dos efeitos (`ultra.effectsActive` vazio). Quem implementar um efeito liga o `implemented` dele.

Meta sugerida para o passo 1.6: 60 FPS com 2 jogadores na média numa placa integrada recente em qualidade
média; 4 jogadores podem cair para baixa. Se não bater, o que mais pesa por viewport são as sombras (média e
alta) e o bloom (alta) — os ajustes ficam para a Fase 2.

## 5. Carros com modelo por estilo (onda F)

Os 20 carros deixaram de ser um modelo só recolorido: são 13 modelos (`docs/CARROS.md`, seção "Modelos"),
agrupados por estilo — cada estilo presente é **uma** chamada de desenho para todos os carros dele, e cada
desenho de roda presente é outra. Carroceria, cabine, vidro, cromo, faróis e lanternas ficaram na mesma
malha e no mesmo material (antes eram 6 peças instanciadas separadas), e o freio é um bit por instância
(antes, uma malha a mais para as lanternas acesas).

**Orçamento** (travado em `tests/car-models.test.ts`; números da onda F — a onda I subiu, ver abaixo):
carroceria ≤ 2.000 triângulos (os 13 ficam entre 1.420 e 1.860), roda ≤ 360 (os 9 desenhos: 278–344) e a roda
simples ≤ 130 (112). A roda simples é a da
qualidade baixa e a dos carros a mais de 30 m à frente do carro do viewport (roda de ~10 px na tela: o
desenho do aro não aparece), todas numa chamada. Rodas não fazem sombra (ficam dentro da sombra da
carroceria e da de contato). Chamadas por viewport para os carros: estilos presentes + desenhos de roda perto
+ 1 roda simples + sombra de contato + chama do nitro; na sombra, só os estilos. Numa corrida com a IA (os
4 estilos do `AI_CAR_POOL`): 8 a 11 chamadas e ~16–45 mil triângulos por viewport na alta.

Medido com `tools/render-harness.html` (1280×720, Copacabana, tick 1500) no build de e3e63b1 (antes) e no
desta onda (depois). "Total" é `renderer.info` do quadro inteiro (todos os viewports, com sombra e bloom);
"carros" é só o grupo dos carros no último viewport desenhado (principal; sombra):

| cena | total antes | total depois | carros antes | carros depois |
|---|---|---|---|---|
| 1 jogador, alta | 88 chamadas, 104.510 tri | 89, 115.862 (+11%) | 7 ch, 8.418 tri; 3 ch, 8.064 | 8 ch, 16.298; 3 ch, 11.536 |
| 4 jogadores, alta | 364, 499.504 | 373, 608.424 (+22%) | 7, 22.854; 3, 21.888 | 11, 43.658; 4, 31.652 |
| 4 jogadores, média | 290, 427.492 | 299, 536.412 (+25%) | 7, 22.854; 3, 21.888 | 11, 43.658; 4, 31.652 |
| 4 jogadores, baixa | 192, 203.528 | **189**, 267.244 (+31%) | 7, 22.854 (sem sombra) | 7, 40.826 (sem sombra) |

Geometrias / texturas / programas na GPU: 1 jogador alta 61 / 26 / 35 → 62 / 26 / **32**; 4 jogadores alta
63 / 68 / 36 → 67 / 68 / **33**; baixa 59 / 13 / 20 → 59 / 13 / **19** (um material para todos os carros:
menos programas). As 13 carrocerias e as 10 rodas são montadas uma vez no construtor e só vão para a GPU
quando um carro daquele estilo aparece (o `geometries` do three conta só as enviadas).

Sessão longa (`scripts/playtest-memoria.mjs`, 36 blocos = 72 corridas, com este build): **sem crescimento
entre ciclos** — geometrias 112 → 112, texturas 79 → 79, programas 99 → 99, materiais 45 → 45 (eram 108, 79,
103 e 50 na medição da seção 2: +4 geometrias de carro enviadas, −5 materiais), heap +175 KB em 24 corridas
(folga 1.536 KB), sem erros na página. (Uma primeira rodada perdeu o navegador no bloco 18 — "Target page,
context or browser has been closed", máquina com carga ~40 —; a segunda foi até o fim.)

Leitura: as chamadas quase não mudam (+1 a +9 no quadro inteiro; na baixa, −3); os triângulos sobem de 11% a
31%, todos nos carros — ~600 mil triângulos com 4 viewports na alta é pouco para qualquer placa de vídeo
dos últimos dez anos, e o que pesa no Deck (sombras, bloom, preenchimento) não mudou. Atalho deliberado:
**sem LOD da carroceria** — se os carros distantes pesarem numa máquina fraca, a saída é uma versão de
loft grosso por estilo (menos estações nos arcos, sem faixas) para os carros a mais de ~40 m, uma chamada a
mais por estilo presente.

### Onda I: carros menos quadrados pela geometria

Bico e traseira redondos, ombro em arco, arco da roda de 10 facetas, borda do teto e dobras do para-brisa
arredondadas, peças chanfradas e pneu de 18 lados (`docs/VISUAL.md`, "Carros menos quadrados pela geometria").
**Orçamento novo** (`CAR_LIMITS.maxShellTriangles`, `tests/car-models.test.ts`): casco ≤ **3.500** (era 2.000), roda
≤ 480 (era 360), roda simples ≤ 150 (era 130). O validador do glTF (`npm run check-car`) usa o mesmo teto.

| estilo | gt | muscle | hatch | sedan | electric | rally | hyper | classic | wedge | pickup | prototype | micro | roadster |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| antes | 1.856 | 1.640 | 1.560 | 1.612 | 1.420 | 1.708 | 1.736 | 1.780 | 1.728 | 1.556 | 1.628 | 1.512 | 1.854 |
| depois | 3.388 | 3.196 | 3.072 | 3.024 | 2.608 | 3.284 | 3.244 | 3.160 | 2.974 | 2.797 | 3.276 | 2.748 | 3.310 |

Rodas: sport5 278 → 374, mag 302 → 422, multi 308 → 404, hubcap 300 → 426, aero 312 → 438, dish 288 → 414, center
320 → 416, wire 344 → 464, steel 300 → 426; roda simples 120 → 144. Onde foram os triângulos de casco: ~2/3 no loft da
carroceria (anel de 22 → 26 pontos e ~30 → ~44 estações: 6 nas pontas redondas e 8 a mais nos arcos), o resto nas
peças chanfradas (uma barra arredondada custa 48 triângulos, a caixa 12) e na cabine (borda e dobras redondas).

Medido com `tools/render-harness.html` (1280×720, alta salvo indicação, quadro inteiro: todos os viewports, com sombra e
bloom), antes (aa7b66f) e depois, os 20 carros na pista:

| cena | antes | depois |
|---|---|---|
| Copacabana, 1 jogador | 56 chamadas, 133.263 tri | 56, 158.589 (+19%) |
| Sampa noite, 1 jogador | 66, 153.112 | 66, 200.366 (+31%) |
| Copacabana, 4 jogadores | 218, 568.131 | 218, 707.203 (+24%) |
| Copacabana, 4 jogadores, baixa | 118, 280.213 | 118, 349.065 (+25%) |

As **chamadas de desenho não mudam** (o casco continua uma malha por estilo, a roda uma por desenho); geometrias,
texturas e programas também não. Sobem só os triângulos, todos nos carros: ~700 mil com 4 viewports na alta é pouco
para qualquer placa dos últimos dez anos (o Deck faz milhões), e o que pesa nele — sombra, bloom, preenchimento — não
mudou (os carros cobrem a mesma área da tela). O tempo do quadro no swiftshader (CPU, máquina com carga 6–8) ficou
dentro do ruído e não serve de medida. A montagem dos 13 cascos e 10 rodas, uma vez no construtor do renderizador
(Node, mediana de 15): ~43 → ~80 ms; a primeira, com o JIT frio, ~220 → ~300 ms. Se um dia pesar numa máquina fraca,
a saída anotada acima continua valendo: LOD de loft grosso (sem as estações das pontas e com o arco de 6 facetas)
para os carros a mais de ~40 m.

## 6. Pista, céu e luz (onda F)

O que a tarefa de chão/atmosfera mudou no custo do quadro (detalhes em `docs/VISUAL.md`). Medido com
`renderer.info` do último quadro (todas as viewports, passada de sombra e bloom incluídas) nas cenas do harness,
1280×720, dpr 1, 6 quadros — antes (e3e63b1) e depois, na mesma máquina carregada. A coluna de tempo não entra: o
swiftshader com a máquina dividida não diz nada sobre GPU.

| cena | qualidade | jog. | chamadas antes → depois | triângulos antes → depois | geometrias | texturas |
|---|---|---|---|---|---|---|
| Copacabana (litoral, dia) | alta | 1 | 88 → 84 | 104.510 → 114.314 | 61 → 57 | 26 → 28 |
| Great Ocean (litoral, entardecer) | alta | 1 | 103 → 99 | 110.938 → 121.478 | 68 → 64 | 28 → 30 |
| Mônaco (litoral, noite) | alta | 1 | 90 → 93 | 122.688 → 129.062 | 58 → 61 | 28 → 31 |
| Kruger (savana, dia) | alta | 1 | 74 → 69 | 58.426 → 67.366 | 54 → 49 | 26 → 28 |
| Transpantaneira (savana, entardecer) | alta | 1 | 74 → 69 | 54.984 → 63.780 | 52 → 47 | 25 → 27 |
| Serra do Mar (mata, dia) | alta | 1 | 83 → 78 | 125.444 → 134.968 | 58 → 53 | 27 → 29 |
| Sampa (cidade, noite) | alta | 1 | 103 → 104 | 79.726 → 83.952 | 67 → 68 | 30 → 33 |
| Paris (cidade, entardecer) | alta | 1 | 108 → 102 | 82.216 → 90.524 | 72 → 66 | 30 → 32 |
| Rota 66 (deserto, dia) | alta | 1 | 74 → 69 | 84.360 → 93.004 | 54 → 49 | 23 → 25 |
| Cânion (deserto, entardecer) | alta | 1 | 80 → 75 | 89.318 → 98.546 | 56 → 51 | 25 → 27 |
| Monte Fuji (montanha, dia) | alta | 1 | 81 → 76 | 72.514 → 81.814 | 56 → 51 | 27 → 29 |
| Lapônia (montanha, entardecer) | alta | 1 | 80 → 75 | 70.240 → 79.132 | 56 → 51 | 27 → 29 |
| Copacabana | alta | 4 | 364 → 348 | 498.776 → 537.184 | 63 → 59 | 68 → 70 |
| Sampa | alta | 4 | 404 → 408 | 340.272 → 357.148 | 68 → 69 | 72 → 75 |
| Serra do Mar | média | 1 | 64 → 59 | 105.970 → 111.150 | 55 → 50 | 13 → 15 |
| Serra do Mar | baixa | 1 | 42 → 37 | 48.322 → 52.934 | 53 → 48 | 10 → 12 |
| Sampa | baixa | 1 | 55 → 56 | 41.164 → 43.636 | 62 → 63 | 13 → 16 |
| Monte Fuji | baixa | 4 | 155 → 135 | 147.674 → 164.746 | 51 → 46 | 13 → 15 |

(As linhas de alta com 1 jogador, menos Copacabana, foram medidas antes do último ajuste — anel do horizonte de 180
para 150 lados e nuvens simples na média/baixa —, que só tira triângulos: ~360 a menos por viewport na alta.)

Leitura:
- **Chamadas de desenho caem ~5 por viewport** (20 a menos com 4 jogadores na baixa): as 7 nuvens viraram uma
  malha só e a linha de largada entrou no decalque do grid. À noite sobem 1 (as poças de luz, instanciadas) e no
  trecho da largada 1 (o decalque). Zebra, acostamento, box e decalque são uma chamada cada, com qualquer número de
  curvas na janela.
- **Triângulos sobem ~4–5 mil por viewport na baixa/média e ~9 mil na alta**: duas colunas a mais no terreno (a
  faixa de transição), zebras em relevo nas curvas, três planos no horizonte em vez de dois e, na alta, nuvens
  arredondadas (a média e a baixa usam a versão facetada, com ¼ dos triângulos). É geometria barata (sem textura,
  sem sombra projetada); o que pesa por viewport continua sendo sombra e bloom, que não mudaram de custo.
- **Texturas +2 a +3**: asfalto e o mapa de brilho das marcações, zebra, cascalho do acostamento, box, decalque da
  largada e a poça de luz (7, contra 4 antes, sendo que uma das 4 era o xadrez órfão que o `Road.setPalette` criava
  e ninguém usava — anotado na seção 2; sumiu). As do chão são recriadas só na troca de pista.
- **CPU do terreno por quadro cai**: altura, cor e inclinação de cada (segmento, lado, coluna) saem de uma tabela
  montada na troca de pista (`Terrain.buildTables`); antes o ruído do relevo e da cor rodava por vértice, por
  viewport, por quadro. O custo foi para a troca de pista (a tabela e as texturas do asfalto: dezenas de ms, uma vez).
- **Sessão longa**: `scripts/playtest-memoria.mjs` (36 blocos, 72 corridas, trocando pista/lotação/qualidade),
  ciclo dos blocos 12–23 contra 24–35: geometrias 106 → 106, texturas 81 → 81, programas 117 → 117, materiais
  51 → 51, objetos 123 → 123 (todos: cresceu em 0 de 12 posições); heap JS 26,98 → 27,11 MB (+137 KB em 24
  corridas); 0 erros; "Sem crescimento entre ciclos". Os três planos do horizonte dividem um material que vive a
  sessão inteira (antes era um por troca de pista) e as texturas do chão são liberadas a cada troca. (Na alta com 4
  viewports o teto subiu de 79 para 81 texturas e de 103 para 117 programas: as texturas novas do chão e os
  shaders novos — nuvem, planos do horizonte, espuma — em cada configuração de sombra/bloom; têm teto e não crescem.)

## 7. Cenário da pista (onda F)

O cenário (`src/render/scenery/`, ver `docs/VISUAL.md`, seção "Cenário") passou de um `InstancedMesh` por
modelo e parte (71 malhas, 18 a 37 visíveis por viewport, cada uma com o seu passe de sombra) para **um
`BatchedMesh` por material** (6 a 11 lotes). Em troca, há muito mais coisa na tela: matas com clareiras,
forração, cercas na divisa, postes com fios, pontos de referência por país, prédios com térreo e telhado — e a
vegetação e as pedras viram silhueta de poucos triângulos longe (LOD).

Medido com `tools/scenery-harness.mjs` (Chromium headless com swiftshader, 1280×720, qualidade alta,
`renderer.info` do 10º quadro). "Cenário" é a diferença entre o mesmo quadro desenhado com e sem o grupo
`scenery` — inclui o passe de sombra. Antes = e3e63b1; depois = esta tarefa; as mesmas cenas e ticks:

| cena | jog. | chamadas total | triângulos total | chamadas do cenário | triângulos do cenário | geometrias | texturas |
|---|---|---|---|---|---|---|---|
| Copacabana (litoral, dia) | 1 | 89 → **53** | 104 k → **96 k** | 47 → **11** | 72 k → **63 k** | 62 → 45 | 26 → 46 |
| Copacabana (litoral, dia) | 4 | 368 → **218** | 502 k → **463 k** | 194 → **44** | 279 k → **240 k** | 64 → 46 | 68 → 88 |
| Baía de Tóquio (litoral, entardecer) | 1 | 93 → **53** | 101 k → **83 k** | 51 → **11** | 69 k → **50 k** | 64 → 45 | 26 → 46 |
| Baía de Tóquio (litoral, entardecer) | 4 | 398 → **216** | 493 k → **412 k** | 226 → **44** | 280 k → **199 k** | 70 → 46 | 70 → 88 |
| Porto de Mônaco (litoral, noite) | 1 | 88 → **49** | 122 k → **102 k** | 54 → **15** | 76 k → **56 k** | 59 → 39 | 28 → 51 |
| Porto de Mônaco (litoral, noite) | 4 | 375 → **210** | 542 k → **446 k** | 225 → **60** | 311 k → **214 k** | 62 → 42 | 71 → 94 |
| Kruger (savana, dia) | 1 | 75 → **48** | 59 k → **81 k** | 33 → **6** | 21 k → **44 k** | 55 → 42 | 26 → 36 |
| Kruger (savana, dia) | 4 | 309 → **198** | 305 k → **394 k** | 135 → **24** | 84 k → **173 k** | 56 → 43 | 68 → 78 |
| Transpantaneira (savana, entardecer) | 1 | 75 → **50** | 55 k → **88 k** | 33 → **8** | 20 k → **52 k** | 53 → 43 | 25 → 40 |
| Transpantaneira (savana, entardecer) | 4 | 306 → **206** | 290 k → **416 k** | 132 → **32** | 82 k → **207 k** | 54 → 44 | 67 → 82 |
| Serra do Mar (tropical, dia) | 1 | 83 → **49** | 125 k → **125 k** | 42 → **8** | 87 k → **88 k** | 58 → 42 | 27 → 39 |
| Serra do Mar (tropical, dia) | 4 | 339 → **203** | 557 k → **558 k** | 168 → **32** | 350 k → **351 k** | 60 → 44 | 70 → 82 |
| Noite em Sampa (cidade, noite) | 1 | 103 → **54** | 80 k → **124 k** | 64 → **15** | 24 k → **68 k** | 67 → 44 | 30 → 53 |
| Noite em Sampa (cidade, noite) | 4 | 408 → **230** | 340 k → **508 k** | 238 → **60** | 98 k → **266 k** | 69 → 47 | 72 → 96 |
| Paris (cidade, entardecer) | 1 | 106 → **58** | 82 k → **171 k** | 61 → **13** | 25 k → **114 k** | 73 → 49 | 30 → 51 |
| Paris (cidade, entardecer) | 4 | 447 → **246** | 339 k → **689 k** | 253 → **52** | 103 k → **453 k** | 76 → 52 | 73 → 94 |
| Rota 66 (deserto, dia) | 1 | 75 → **48** | 84 k → **92 k** | 33 → **6** | 49 k → **57 k** | 55 → 42 | 23 → 36 |
| Rota 66 (deserto, dia) | 4 | 309 → **198** | 402 k → **429 k** | 135 → **24** | 195 k → **222 k** | 57 → 43 | 66 → 78 |
| Cânion de Nevada (deserto, entardecer) | 1 | 80 → **46** | 89 k → **96 k** | 40 → **6** | 45 k → **52 k** | 56 → 40 | 25 → 35 |
| Cânion de Nevada (deserto, entardecer) | 4 | 331 → **190** | 401 k → **430 k** | 165 → **24** | 178 k → **207 k** | 60 → 41 | 68 → 77 |
| Monte Fuji (montanha, dia) | 1 | 82 → **48** | 73 k → **96 k** | 42 → **8** | 33 k → **56 k** | 57 → 41 | 27 → 39 |
| Monte Fuji (montanha, dia) | 4 | 324 → **198** | 325 k → **430 k** | 158 → **32** | 128 k → **233 k** | 58 → 42 | 69 → 81 |
| Lapônia (montanha, entardecer) | 1 | 81 → **50** | 70 k → **99 k** | 39 → **8** | 33 k → **62 k** | 57 → 43 | 27 → 40 |
| Lapônia (montanha, entardecer) | 4 | 343 → **207** | 363 k → **474 k** | 168 → **32** | 133 k → **243 k** | 59 → 44 | 71 → 83 |
| largada de Copacabana | 1 | 111 → **53** | 133 k → **155 k** | 71 → **13** | 70 k → **91 k** | 72 → 44 | 30 → 50 |
| largada de Copacabana | 4 | 452 → **220** | 526 k → **611 k** | 284 → **52** | 282 k → **367 k** | 73 → 45 | 73 → 93 |
| box (fundo do menu) | 1 | 102 → **44** | 90 k → **101 k** | 71 → **13** | 73 k → **85 k** | 66 → 38 | 29 → 49 |

Leitura:
- **Chamadas de desenho do cenário: de 4 a 7 vezes menos** (com 4 jogadores, de 132–284 para 24–60); o quadro
  inteiro caiu ~40% (4 jogadores: 306–452 → 190–246).
- **Triângulos**: caíram no litoral (antes: prédios e palmeiras pesadas), empataram no tropical e subiram onde antes
  quase não havia nada (savana 2–2,5×, montanha ~1,8×) e na cidade (2,7–4,4×: prédio com térreo de lojas, cornija e
  mansarda em vez de caixa). O pior caso é Paris com 4 jogadores: 0,69 milhão de triângulos por quadro (antes 0,34).
  Numa placa de vídeo de verdade isso é pouco; se pesar no Deck, o próximo passo é uma silhueta de longe também
  para prédios (hoje só vegetação e pedra têm).
- **Geometrias**: menos (cada lote é uma geometria). **Texturas**: +10 a +20 — cada lote tem três `DataTexture`
  pequenas (matrizes, índice e cor por instância), recriadas a cada troca de pista e liberadas com
  `BatchedMesh.dispose()`.

**CPU** (`Scenery.update`: as matrizes da janela, por viewport; medido em Node sem GPU, com a máquina já mais
livre — carga ~4): **0,16 a 0,41 ms por viewport** (Kruger a Serra do Mar), contra 0,09 a 0,34 ms do cenário antigo,
com 5 a 10 vezes mais objetos — ≤ 1,6 ms com 4 jogadores. O que segura: o rumo de cada objeto sai de cos/sen já
calculados por ponto da janela (identidade trigonométrica, sem `Math.cos` por objeto), a cor por instância só é
escrita quando muda e a mancha de sombra só vai até 60 segmentos. Montar o layout de uma pista (uma vez, na largada):
80 a 300 ms; a primeira pista de cada bioma também monta os modelos (cache válido o jogo inteiro).

**Qualidades baixa e média**: o renderizador pede uma janela menor (140 e 200 segmentos à frente, contra 260 na
alta) e o cenário acompanha: desenha só a fração correspondente dos enfeites dispensáveis (forração, mata e soltos,
por sorteio fixo de cada um; nunca os sprites da física, cercas, postes ou pontos de referência) e nada além do
ponto em que a névoa apaga tudo. Sombra de verdade só dos sprites e só nas qualidades com sombra; a mancha no chão
vale em todas. Serra do Mar (a pista mais densa):

| qualidade | jog. | chamadas total | triângulos total | chamadas do cenário | triângulos do cenário |
|---|---|---|---|---|---|
| baixa | 1 | 42 → **30** | 48 k → **67 k** | 18 → **6** | 26 k → **45 k** |
| baixa | 4 | 173 → **127** | 223 k → **299 k** | 70 → **24** | 106 k → **183 k** |
| média | 1 | 64 → **35** | 105 k → **111 k** | 37 → **8** | 71 k → **76 k** |
| média | 4 | 260 → **147** | 485 k → **501 k** | 145 → **32** | 289 k → **305 k** |

**Memória** (`scripts/playtest-memoria.mjs`, 36 blocos = 72 corridas, com o cenário novo): sem crescimento entre
ciclos — geometrias 53 → 53, texturas 97 → 97, programas 113 → 113, materiais 45 → 45, objetos 66 → 66 no fim do
ciclo, 0 de 12 posições crescendo; heap JS 31,82 → 31,97 MB (+157 KB em 24 corridas; folga 1,5 MB). A seção 2 registrava
~24,7 MB na onda D; parte da diferença é do cenário (cache de modelos, layout da pista atual, cópias em CPU dos
lotes), mas a base e3e63b1 não foi medida de novo nesta rodada — o que vale aqui é que não cresce.

## 8. Marcos turísticos (onda G)

Os marcos (`src/render/scenery/landmarks/`, `docs/VISUAL.md`, "Marcos turísticos") entram nos lotes que o cenário já
tem (liso sem sombra, luz, fachadas, baliza): **nenhuma chamada de desenho nova**, salvo o lote `haze` (névoa mais
rala) das pistas com marco no horizonte — **+1 por viewport**. Sem sombra própria, sem mancha, sem LOD (são poucos e
leves). Montagem: 30–400 ms a mais no layout da pista (uma vez, na largada; a busca confere a pegada contra ±300
segmentos da linha central).

**Orçamento de triângulos por marco** (`tests/landmarks.test.ts`): perto ≤ 3.500 (visto de perto: porta, janela,
telhado), longe ≤ 5.000 (ponte com cabos, morro com convento — grande, mas a 120–400 m), horizonte ≤ 2.500 (silhueta
na névoa). Uma pista tem 2–3 marcos × 1–4 por volta, e só 1–3 instâncias cabem na janela de ~1 km: no pior caso,
≤ ~15 mil triângulos por viewport, contra 50–450 mil do resto do cenário. Os 22 do Sudeste, Sul e Centro-Oeste:

| marco | lugar | por volta | triângulos | partes |
|---|---|---|---|---|
| `cristo_redentor` | skyline | 2 | 376 | flat |
| `pao_de_acucar` | skyline | 2 | 436 | flat |
| `masp` | near | 2 | 180 | flat, office, glow |
| `ponte_estaiada` | far | 2 | 1.032 | flat, glow, beacon |
| `igreja_barroca` | near | 2 | 1.052 | flat, glow |
| `casario_colonial` | near | 3 | 704 | flat, house, glow |
| `convento_penha` | far | 2 | 352 | flat, glow |
| `terceira_ponte` | far | 1 | 1.464 | flat, glow |
| `trem_serra_verde` | far | 2 | 1.144 | flat |
| `estufa_jardim_botanico` | near | 1 | 1.434 | flat, glow |
| `ponte_hercilio_luz` | far | 2 | 1.248 | flat, glow |
| `igreja_acoriana` | near | 2 | 404 | flat, glow |
| `catedral_de_pedra` | near | 1 | 768 | flat, glow |
| `cuia_chimarrao` | near | 2 | 348 | flat |
| `araucaria` | near | 4 | 1.940 | flat |
| `congresso_nacional` | far | 2 | 594 | flat, office, glow, beacon |
| `catedral_brasilia` | near | 2 | 1.416 | flat, glow |
| `cachoeira_veadeiros` | far | 2 | 960 | flat, glow |
| `buriti` | near | 4 | 1.412 | flat |
| `gruta_lago_azul` | near | 2 | 468 | flat, glow |
| `tuiuiu_ninho` | near | 3 | 548 | flat |
| `portal_transpantaneira` | near | 2 | 436 | flat, glow |

**Medido** (`tools/scenery-harness.mjs`, 1280×720, qualidade alta, 1 jogador). "Antes" é o mesmo quadro com
`&nolm=1`, que tira os marcos de `places.ts`:

| cena | chamadas total | triângulos total | chamadas do cenário | triângulos do cenário |
|---|---|---|---|---|
| largada de Copacabana (Cristo na janela) | 60 → **61** | 200,6 k → **201,0 k** | 13 → **14** | 94,2 k → **94,5 k** |
| Copacabana, tick 1500 | 57 → 57 | 129,8 k → 129,8 k | 11 → 11 | 69,8 k → 69,8 k |
| Transpantaneira, tick 1500 | 51 → 51 | 140,7 k → 140,7 k | 8 → 8 | 51,9 k → 51,9 k |
| Noite em Sampa, tick 1400 | 67 → 67 | 159,1 k → 159,1 k | 15 → 15 | 65,9 k → 65,9 k |

Nas cenas de tick 1500 nenhum marco está na janela (é a posição fixa do harness), então o custo é zero. Com um marco
à vista, o custo é o do modelo (centenas a ~2 mil triângulos) e, só no horizonte, a chamada do lote `haze`.

## 9. Cenário redondo (onda I)

Copa, pedra, moita, cacto e conífera ganharam silhueta redonda e cor por ponto (`docs/VISUAL.md`, "Forma redonda").
Só a geometria dos modelos mudou: **mesmas instâncias e mesmas chamadas de desenho** (o layout e o runtime não foram
tocados). O que segura os triângulos: a parte de uma bolha enterrada em outra sai na montagem (`cullInside`), e o
tronco e os galhos perderam as tampas (o pé no chão, a ponta na copa).

Medido com `tools/scenery-harness.mjs` (1280×720, qualidade alta, `renderer.info` do 10º quadro), antes = aa7b66f,
mesmas cenas e ticks. "Cenário" é a diferença do mesmo quadro com e sem o grupo `scenery` (inclui o passe de sombra):

| cena | chamadas total | triângulos total | chamadas do cenário | triângulos do cenário | instâncias (último viewport) |
|---|---|---|---|---|---|
| Copacabana (litoral, dia), 1 jog. | 57 → 57 | 129,8 k → **140,7 k** (+8%) | 11 → 11 | 69,8 k → **80,7 k** (+16%) | 685 → 685 |
| Copacabana, 4 jog. | 218 → 218 | 559,8 k → **603,9 k** (+8%) | 44 → 44 | 274,5 k → **318,6 k** (+16%) | 692 → 692 |
| Transpantaneira (savana, entardecer), 1 | 51 → 51 | 140,7 k → **154,5 k** (+10%) | 8 → 8 | 51,9 k → **65,7 k** (+27%) | 598 → 598 |
| Transpantaneira, 4 | 212 → 212 | 521,6 k → **576,6 k** (+11%) | 32 → 32 | 208,5 k → **263,5 k** (+26%) | 600 → 600 |
| Serra do Mar (tropical, dia), 1 | 51 → 51 | 165,8 k → **174,7 k** (+5%) | 8 → 8 | 89,2 k → **98,1 k** (+10%) | 893 → 893 |
| Serra do Mar, 4 | 201 → 201 | 646,9 k → **682,1 k** (+5%) | 32 → 32 | 358,3 k → **393,5 k** (+10%) | 885 → 885 |
| Noite em Sampa (cidade, noite), 1 | 67 → 67 | 159,1 k → **160,5 k** (+1%) | 15 → 15 | 65,9 k → **67,3 k** (+2%) | 680 → 680 |
| Noite em Sampa, 4 | 269 → 269 | 624,5 k → **629,9 k** (+1%) | 60 → 60 | 265,0 k → **270,4 k** (+2%) | 691 → 691 |
| Rota 66 (deserto, dia), 1 | 50 → 50 | 134,8 k → **149,0 k** (+11%) | 6 → 6 | 54,3 k → **68,5 k** (+26%) | 518 → 518 |
| Rota 66, 4 | 196 → 196 | 516,4 k → **573,2 k** (+11%) | 24 → 24 | 222,6 k → **279,4 k** (+26%) | 513 → 513 |
| Cânion de Nevada (deserto, entardecer), 1 | 47 → 47 | 139,8 k → **153,3 k** (+10%) | 6 → 6 | 52,4 k → **65,9 k** (+26%) | 554 → 554 |
| Cânion de Nevada, 4 | 200 → 200 | 529,0 k → **580,6 k** (+10%) | 24 → 24 | 213,9 k → **265,5 k** (+24%) | 532 → 532 |
| Monte Fuji (montanha, dia), 1 | 53 → 53 | 125,1 k → **140,2 k** (+12%) | 8 → 8 | 57,4 k → **72,6 k** (+26%) | 1047 → 1047 |
| Monte Fuji, 4 | 199 → 199 | 515,7 k → **575,8 k** (+12%) | 32 → 32 | 229,1 k → **289,2 k** (+26%) | 1050 → 1050 |

Leitura: o quadro inteiro sobe 1% a 12%; o cenário 2% (cidade) a 27% (savana, deserto, montanha — onde moita, pedra,
cacto e conífera pesam mais). O pior quadro medido continua a Serra do Mar com 4 jogadores, **682 mil** triângulos —
abaixo do pior caso da seção 7 (Paris, 689 mil), que não muda (prédio não foi tocado).

**Por modelo** (triângulos): árvore de copa redonda 354 → 429, gigante 303 → 415, acácia 341 → 317, bananeira 238 → 160,
oliveira 354 → 558, eucalipto 274 → 466, baobá 276 → 600, conífera 104–132 → 132–172, coqueiro 406 → 428, imperial
352 → 340, saguaro 270 → 340, barril 370 → 350, eufórbia 340 → 510, cupinzeiro 308 → 360, moita 80–89 → 189–202,
cerca-viva 30 → 56, matacão 156 → 264, arenito 124 → 192, penedo do mar 80 → 168, mesa 160 → 240. **Por pista** (a soma
dos triângulos do modelo de perto de cada objeto ÷ segmentos — o que acompanha o quadro): média das 109 pistas
405 → 484, a pior 764 → 856 (Pororoca do Araguari). Travas em `tests/scenery-forma.test.ts`: modelo redondo ≤ 700
(pedra ≤ 360, mesa e coluna do mar ≤ 600) e ≤ 900 por segmento em toda pista; `tests/scenery.test.ts` (sprite ≤ 1.600,
longe ≤ 60, forração ≤ 40) não mudou.

**Montagem** (Node, frio, os 274 modelos de cenário das 109 pistas, sem marcos): ~500–620 ms antes, ~550–580 ms
depois — dentro do ruído da máquina (o `cullInside` e o `tintUpSoft` são lineares; a normal suave já era o grosso).
A troca de pista (layout + modelos, uma vez na largada) fica na mesma ordem: Copacabana ~550 → ~600 ms, Rochosas
~265 → ~315 ms, medidos com outra carga na máquina.

### Onda I somada (carros redondos + cenário redondo, medido depois da mescla)
`tools/scenery-harness.mjs`, 4 jogadores, qualidade alta: coast-day 218 chamadas / **743 mil** triângulos; tropical-day
201 / **827 mil**; city-night 269 / **829 mil** (antes da onda I o pior caso era Paris 4p, 689 mil: +20%). Chamadas,
geometrias, texturas e programas iguais — só triângulos sobem. Continua barato para qualquer placa recente; a medição
no Steam Deck (roteiro acima) segue com o Felipe.
