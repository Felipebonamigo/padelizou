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

Meta sugerida para o passo 1.6: 60 FPS com 2 jogadores na média numa placa integrada recente em qualidade
média; 4 jogadores podem cair para baixa. Se não bater, o que mais pesa por viewport são as sombras (média e
alta) e o bloom (alta) — os ajustes ficam para a Fase 2.

## 5. Pista, céu e luz (onda F)

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
