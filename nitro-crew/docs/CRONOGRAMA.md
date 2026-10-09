# Cronograma: do jogo de hoje ao jogo viciante, competitivo e de última geração

> **Versão 2 (06/10/2026), aguardando aprovação do dono.** Substitui o aviso "em elaboração" e o rascunho v1. O v1 passou por três revisões adversariais: cobertura do pedido, viabilidade técnica contra o código e produto/sequência. O que mudou por causa delas está em "Por que esta ordem".
>
> Como usar:
> - Antes de abrir uma onda, o orquestrador escreve o cartão em `docs/ondas/<onda>.md`. O cartão fecha três coisas: a lista exata de arquivos de cada frente, **montada por grep dos símbolos e nunca de memória**; os testes que vêm primeiro; e as capturas pedidas.
> - A execução segue o `docs/PLAYBOOK.md`. O visual segue a bíblia de estilo (`docs/DIRECAO-DE-ARTE.md`, reescrita na onda K).
> - Ao fechar uma onda, ela é marcada aqui com a data e o commit.

**Pedido do dono (06/10/2026), palavra por palavra:** "vamos pensar, montar um cronograma, com várias melhorias possíveis para deixar o jogo mais legal, que deixe as pessoas viciadas em jogar, com competição, compra de carros, com o andamento comprar melhoria para os carros, poder trocar de cor dos carros, deixar o jogo visualmente mais bonito, menos quadrado, que dê para reconhecer bem as artes e os locais do jogo, vamos montar um cronograma, para depois executarmos, pense em tudo isso, nem que tenhamos que modificar todo o jogo". E logo depois: "lembre-se, tem que parecer um jogo de última geração, cartoonado".

## Resumo das ondas

| Onda | Objetivo | Duração (dias-base de agente) | O que o jogador sente |
|---|---|---|---|
| **K** | Mais uma corrida, e medida: laço sem atrito, diário de jogo, rampa da IA, banco de prova, bíblia de estilo, primeiros desenhos. Fecha com a **versão 0.1.1** (linha de base) | 1,5 | "Perdi? Recomeço com um botão." O fim da carreira deixa de ser passeio. O Cristo aparece desenhado em Copacabana. |
| **L** | **Fatia vertical de última geração**: Copacabana, um carro da arte nova e o HUD no acabamento final, com o pipeline de arte pronto. Teste cego com gente de fora | 2,5 | "Isso é outro jogo", provado numa pista antes de espalhar para as outras. |
| **M** | Derrapar com mini-turbo, e o visual novo em todas as 109 pistas: chão, céu, cenário e os **161 marcos** sem quinas, carros e efeitos | 2,5 | Cada curva vira um desafio, e nada mais parece quadrado. |
| **N** | Nitro que se ganha pilotando e largada-foguete. Comprar dá vontade: garagem 3D, visual do carro, pagamento contando na tela, Expedição como caminho padrão | 2,5 | "Faltam $ 3.400 para o Curupira, e eu quero o perolado." |
| **O** | **Congelado v1**: os 27 desenhos nas pistas, uma recalibração única, a 1ª corrida que ensina, estrelas e rival com placar, progresso no sofá | 2 | "Cada corrida rende algo, e o Zé Turbo está 3 × 2." |
| **P** | O laço completo e a economia: sempre há algo para comprar, classes, carros para conquistar, melhoria que aparece no carro. Fecha com a **versão 0.2** | 2 | "O carro mostra o que eu comprei, e ainda falta o carro do rival." |
| **Q** | O Brasil que se reconhece: ícone do estado, carimbo que bate, minimapa que se desenha, ambiente regional, paisagem do lugar, 10 marcos-herói vivos | 2 | "Sei onde estou mesmo sem o marco na tela." |
| **R** | Cerimônia e som do lugar: postal renderizado, sobrevoo de abertura, motor com voz própria, música que reage e levada regional | 1,5 | O postal do lugar, um forró no sertão e o rival soando atrás. |
| **S** | Competição justa offline: recorde por categoria, medalhas por classe, rivalidade do sofá, vários fantasmas, desafio do dia. Fecha com a **versão 0.3** e o Steam Playtest | 2 | "Só mais uma volta para o ouro do Zé Turbo." |
| **T** | Online com amigos: Série da Noite, placar de amigos, conquistas de disputa, Deck pronto para revisão, modos rápidos | 1,5 (+ dono) | "Convidei meu amigo pela Steam e jogamos a série da noite." |
| **U** | Arte final: carros e marcos-herói da arte externa, piloto, decalques, festa e clima | 2,5 (+ arte externa) | O bondinho sobe, e "o 27 de Pernambuco é o meu". |
| **V** | Fim de jogo que não acaba: convites, duelo com o rival, Carreira Lendária, Liga dos Rivais | 1,5 | "Terminei a Expedição e ainda tenho o que buscar." |
| **W** | Lançamento: fatia revisada, demo, trailer por replay, loja e QA | 1,5 (+ dono) | Quem ainda não comprou vê o jogo de verdade. |

**Total: ~25 dias-base de agente.** A execução segue num modo mais econômico, então a margem é de 1,5 a 2×: **~38–50 dias de agente**.

**Escala de esforço** (base, antes da margem): P = 1–2 h · M = 3–4 h · G = 6–8 h · GG = mais de um dia, ocupa a onda.

**Calendário** (uma onda por semana, com a revisão do dono):

| Semana (segunda) | Onda | Versão ou marco |
|---|---|---|
| 12/10 | K | **0.1.1** para 5–8 amigos (linha de base) · Marco 1 |
| 19/10 | L | Teste cego visual · Marco 2 (orçamento de arte decidido) |
| 26/10 | M | — |
| 02/11 | N | Marco 3 · página "Em breve" na Steam |
| 09/11 | O | Noite de sofá · Marco 4 (**congelado v1**) |
| 16/11 | P | **0.2** · Marco 5 |
| 23/11 | Q | — |
| 30/11 | R | Teste cego de lugar · Marco 6 |
| 07/12 | S | **0.3** e Steam Playtest público · Marco 7 |
| 14/12 | T | Marco 8 |
| 21/12 a 03/01 | pausa de fim de ano | — |
| jan/2027 | V (ou U, se a arte externa já tiver chegado) | Marco 10 |
| fev/2027 | U (depende da entrega do artista) | Marco 9 |
| fev–mar/2027 | W | Marco 11 · Early Access ou 1.0, conforme a decisão do dono |
| jun/2027 | Steam Next Fest (datas a confirmar no Steamworks) | Demo |

Se cada onda passar de uma semana, o W cai em abril e o Next Fest de junho continua possível. O calendário real depende de três coisas fora das mãos do agente:
- o tempo do dono (estimado por marco na seção 5);
- as contas (Steamworks e o VPS do relay);
- o prazo do artista: de 2 a 4 meses a partir do Marco 2.

### Por que esta ordem (o que mudou desde o rascunho v1)

1. **Gente de fora desde a 1ª semana.** A 0.1.1 sai no fim da K como linha de base, com relatório exportado em arquivo. A 0.2 só sai quando o laço que ela deve testar já existe. Cada marco visual tem teste cego com pessoas que nunca viram o jogo.
2. **Fatia vertical antes de escalar.** A L leva uma pista (Copacabana) ao acabamento final e prova com teste cego. Só depois a M espalha o resultado pelas 109 pistas.
3. **O pipeline de arte vem cedo.** glTF com textura, modelo por carro e carga por arquivo entram na L, com um carro CC0 estilizado de ponta a ponta. O dono decide o orçamento de arte no Marco 2, vendo o resultado. A arte externa corre de outubro a fevereiro sobre um contrato escrito na K.
4. **A física muda antes de calibrar a economia.** A derrapagem entra na M e o nitro na N. A O recalibra tudo uma única vez e congela. Só então vêm a economia (P) e as medalhas (S).
5. **O que o jogador sente não espera a física.** Garagem 3D, visual do carro, pagamento e o visual novo rodam ao lado do núcleo, em arquivos disjuntos.
6. **O mundo inteiro fica redondo, não só 10 heróis no fim.** Os 161 marcos e todo o cenário recebem bisel na M. A paisagem própria de cada lugar entra na Q.
7. **As medalhas não quebram a cada ajuste.** A `lapFingerprint` passa a levar só uma lista explícita de constantes, `LANDMARK_PLAZAS` fica congelado e as medalhas valem por classe de carro.

### Pedido do dono → onde está no cronograma

| Pedido | Hoje | O que entra (onda/frente) |
|---|---|---|
| **Viciar** ("só mais uma") | Eliminado ou campeão só vê MENU, e o `toMain` desliga os assentos. A 1ª recompensa chega com ~11 min só de corrida. Nenhuma meta aparece na tela. | K1 (mais uma a um botão), N4 (pagamento contando, com orçamento de tempo entre corridas), N5 (Expedição e próximo objetivo), O3 (1ª corrida que ensina), O4 (estrelas), O5 (nível de piloto em todos os modos), S5 (desafio do dia sem FOMO), V (fim de jogo) |
| **Competição** | Online pronto, mas sem servidor no ar. Recordes só locais e sem categoria. Nenhuma medalha, placar ou desafio. O rival só aparece antes e depois da corrida. | L5 (disputa que se vê, rival na pista), O4 (rival com placar e retrato), S (recorde justo, medalhas por classe, rivalidade do sofá, fantasmas, desafio), trilha S (relay e convite pela Steam), T (Série da Noite, placar de amigos), V3 (Liga dos Rivais) |
| **Compra de carros** | 14 carros (7 à venda, de $ 12 mil a $ 40 mil), mostrados em silhueta SVG de ~90 px. A vitrine acaba na corrida 33 de 109. Fora da carreira, o carro trancado fica invisível. | L3 (um modelo por carro), N2 (garagem 3D e revelação da compra), N5 (compra no caminho padrão), O5 (carro com cadeado), P1 (os 7 carros espalhados até a corrida ~80), P2 (classes, carro do rival e lendários), P4 (test-drive), V1 (convites, duelo, lendários restantes) |
| **Melhorias com o andamento** | 6 peças × 3 níveis, que são só número e não aparecem. A IA para no teto, e a 2ª metade da carreira fica fácil (média de 1,2º a 2,9º). | K3 e O2 (rampa da IA: a melhoria volta a contar), N1 (a peça de nitro vira barra), P2 (classes D–S: "subiu para A!"), P3 (a melhoria aparece no carro, inclusive no da IA) |
| **Trocar a cor** | **Já existe** (onda J): 16 pinturas (cor + detalhe) por assento × carro, grátis, no lobby, na garagem e no online. | N3 (acabamentos metálico, perolado, fosco, cromado e camaleão; cor livre por camada; 9 rodas; cor do aro; faixa; cor da chama), P1 e P4 (oficina com preço, o destino dos $ 323 mil parados), U4 (número, placa, adesivos de folclore, pinturas-troféu, visual dos rivais) |
| **Mais bonito, menos quadrado, última geração cartunesca** | PBR comum com ACES, sem rampa de luz, luz de borda, oclusão de ambiente nem gradação. Carros com proporção realista (roda de ~16,7% do comprimento). Chão chapado. Marcos e prédios extrudados sem bisel. HUD com fonte do sistema. | K4 (bíblia, quadro de referências, banco de prova, decisão WebGL × WebGPU), L (fatia vertical e pipeline de arte), M (escala, cenário e marcos redondos, efeitos, carros), N2 (palco 3D), Q4 e Q5 (paisagem e heróis), R1 (postal), U (arte final) |
| **Reconhecer as artes e os locais** | 161 marcos com régua de leitura e legenda. As 81 pistas do Brasil usam uma receita só (`br`). Desenhos do minimapa reprovados. Postal em CSS. Carimbo só com a sigla. | Trilha D (27 desenhos: K5 e O1), L4 (Copacabana no padrão final, com a paisagem do Rio), M3 (flora do Brasil, marcos redondos), Q (ícone, carimbo, álbum, minimapa, sub-regiões, horizonte, heróis), R (postal, sobrevoo, som e música regionais), U2 e U5 (heróis da leva 2, festa e clima) |

### Ajustes anotados pelo dono (09/10/2026) → onde entram

Os 13 cabem no cronograma: 5 entram já na K (frente nova **K6**), 5 nas frentes novas **M6** (traçado = mapa, corrida que muda) e **N6** (motor, buzina, provocação) e 3 em frentes que já existiam (L3/N2, N2, Q). A coluna do meio foi conferida no código em 09/10/2026; os dois defeitos (6 e 11) foram achados lendo o código, e o teste visto falhar vem na K6.

| # | Ajuste | O que o código mostra | Onde entra |
|---|---|---|---|
| 1 | As curvas não respondem ao mapa | O minimapa normaliza a soma das curvas para uma volta (360°); o 3D gira um ângulo fixo por unidade de curva (`HEADING_PER_CURVE = 0.0035`, `src/render/units.ts:16`). A mesma curva que no mapa tem 90° pode ter 40° ou 150° na pista, e a pista 3D não fecha. Os desenhos ainda usam rampas (`cv`) que deformam o traçado. | K5 (o trecho `bend`, curva constante) e **M6 Traçado = mapa** (nova) |
| 2 | Os carros não estão de acordo com o desenho | Na escolha aparece uma silhueta SVG de ~90 px desenhada à mão; na pista, o modelo 3D procedural do estilo. São dois desenhos diferentes do mesmo carro. | L3 (um modelo por carro) e N2 (escolha e garagem mostram o próprio modelo 3D). Ver a pergunta nº 23 |
| 3 | Buzina, cada carro com a sua | Não existe. | **N6 Voz do carro** (nova) |
| 4 | Provocar o adversário com balão de mensagem pronta | Não existe; o rival só fala antes e depois da corrida. | **N6**, com as frases de brincadeira e sem palavrão (decisão nº 24) |
| 5 | Cada carro com seu barulho de motor | Um timbre só: `engineFrequency(rpm, nitro)` (`src/audio/engine.ts:47`) não recebe o carro. | Sai da R3 e vem para a **N6** |
| 6 | O nitro nem sempre funciona de primeira | **Defeito achado.** O aperto é borda e se fecha a cada quadro de tela (`poll()` em `src/ui/input.ts`), mas a física roda a 60 passos/s. Num quadro em que não cabe nenhum passo, o aperto se perde (`src/game/session.ts:470–479`): no monitor de 120/144 Hz, cerca de metade dos apertos some; no de 60 Hz, os quadros com oscilação. Apertar com o nitro ainda aceso é recusado ("sem nitro"), o que também parece falha. | Defeito: **K6** (teste antes). Ser "mais parecido com fogo": **M5** (chama em malha) e a cor da chama na N3 |
| 7 | Loja de carros separada | A compra fica dentro da garagem. | **N2**: a Concessionária vira tela própria no menu, separada da Garagem |
| 8 | Mudanças na corrida conforme o tempo passa | Período (dia, entardecer, noite) e clima são fixos por pista. | **M6**: o sol desce ao longo da corrida e a cidade acende as luzes (só visual). Chuva que muda a aderência mexe na física: decisão nº 25 |
| 9 | As pistas estão muito iguais | As 81 do Brasil usam uma receita só de cenário (`br`), com os marcos por cima. | M3, Q3 e Q4, com meta medida na Q: teste que falha se duas pistas da mesma copa tiverem o mesmo conjunto de solo + vegetação + horizonte, e ao menos um elemento de traçado só da pista (ponte, túnel, terra, subida, beira-mar) |
| 10 | Nome da corrida = cidade; mais pontos turísticos, vegetação e solo de cada cidade | O nome é o do trecho ("Orla de Copacabana"); o lugar (`TrackPlace`, `src/core/data/places.ts:4`) só tem estado e marcos, sem cidade. | **K6**: campo `city` nos 109 lugares e o título "Rio de Janeiro · Orla de Copacabana". Pontos turísticos, solo e flora por cidade: Q (leva 2 de marcos, sub-regiões) |
| 11 | Ao terminar, o carro fica parado acelerando | **Defeito achado.** Quando todos terminam, `stepRace` para de andar (`src/core/sim/race.ts:70`: `phase === 'finished'` só conta o tick), os carros congelam e o som do motor fica no último giro, durante os `RESULTS_DELAY` antes do resultado. O piloto automático de quem terminou (`cruiseInput`) só vale enquanto a corrida não acabou. | **K6**: depois da bandeirada, todos seguem andando sozinhos, desacelerando, com o motor coerente (só visual: o resultado e o `hashRace` não mudam) |
| 12 | Nome do mapa no minimapa | O minimapa (`src/render/minimap.ts`) só desenha o traçado. | **K6** |
| 13 | Banners do Padelizou, Atomatiza e Galeria Atomo | PADELIZOU já está nas placas (`src/render/scenery/textures.ts:105`); faltam os outros dois. | **K6**: ATOMATIZA e GALERIA ATOMO na lista. Com logo enviado pelo dono (SVG ou PNG), vira textura em vez de texto |

---

## 1. Visão

O Nitro Crew que queremos é uma corrida arcade de sofá com cara de jogo de última geração cartunesco, na linha de Mario Kart 8 Deluxe. Os carros são gordinhos, de rodas grandes, com verniz que reflete o céu. A luz é quente e a sombra é colorida. Cada lugar tem a própria paisagem: os morros do Rio e o Pão de Açúcar na baía, a névoa das Cataratas, o mandacaru no sertão. A palmeira balança e o bondinho sobe. Cada corrida de 2 a 4 minutos responde a tudo o que o piloto faz: a derrapagem carrega o mini-turbo, o nitro se ganha pilotando, a ultrapassagem salta "+1" na tela e o raspão solta faísca. O fim é um pagamento que se sente: pódio 3D, dinheiro contando no cofre, estrela nova. A um botão, já aparece a próxima coisa quase ao alcance: a peça que se vê no carro, o acabamento perolado, o carro do rival, o estado que fecha a região, a revanche com o Zé Turbo, que lembra o placar e tem rosto. A disputa acontece em vários lugares. No sofá, a rivalidade entre amigos fica salva. Contra pilotos com nome e cara, há medalhas por classe e a Liga dos Rivais. Contra o próprio fantasma e, pela Steam, contra os amigos à distância. É sempre justa: o oficial é só carro sem assistência, nada se compra com dinheiro real e nada vem de sorteio. A progressão é a Expedição. Cada estado tem uma pista que desenha o seu símbolo no minimapa, um ambiente que diz onde se está e um marco-herói vivo, e cada copa vencida dá um postal e um carimbo que batem na tela. Completar o mapa do Brasil, o álbum de 161 marcos e a garagem é o "só mais uma copa" que dura meses.

## 2. Princípios

1. **Vício saudável, nunca escuro.** Tudo se ganha jogando ou se compra com o dinheiro do jogo, com o preço à vista. Não há:
   - caixa com sorteio (o ECA Digital, Lei 15.211/2025, restringe isso em jogos acessíveis a menores);
   - microtransação nem moeda paga;
   - nada que castigue quem faltou:
     - não existe sequência que se perde;
     - os selos são contados sem o denominador de dias ("12 selos", nunca "12/31");
     - o cosmético da semana volta em rodízio ou fica à venda depois na Oficina;
     - semana não jogada não rebaixa ninguém na Liga.

   Essas regras viram teste (S5, V3). Toda cena de recompensa é pulável e respeita "Reduzir efeitos".
2. **Sem pagar para ganhar.** Cosmético nunca dá desempenho. Recorde oficial, medalha, placar e conquista de tempo valem só sem direção assistida e são separados por classe de carro (P2, S2): um carro de $ 40 mil não compra o ouro. A assistida tem tabela própria e não some.
3. **Núcleo determinístico, e o que o visual pode mudar.** Em `src/core`, regra nova entra dentro do `stepRace`, com `state.rng` e aritmética simples. Nada de `Math.random`, trigonometria, `Date.now`, `Map` ou `Set` no estado (`tests/determinism.test.ts`). Todo campo novo do `CarState` tem padrão em `deserializeRace` e **entra no `hashRace`**. O renderizador nunca altera o estado. Cosmético viaja como a pintura viaja hoje (`HumanEntry` e `RenderFrame`), retirado antes da `RaceConfig` (padrão `withoutPaint`). Trabalho só de render não toca o `sim-golden`, os recordes nem o online, **desde que**:
   - a pegada sólida do cenário continue a mesma (pegada = colisão);
   - `LANDMARK_PLAZAS` (`src/core/track/plazas.ts`: `perLap`, `place` e `side` de cada marco) não mude, porque as praças abrem a colisão das pistas de cidade e de litoral;
   - nenhuma constante entre em `src/core/constants.ts`.

   Quem quebra uma dessas condições está mudando conteúdo, e isso vai para uma onda de núcleo.
4. **Teste antes, visto falhar pelo motivo certo.** Todo defeito vira um teste escrito antes da correção, e o mesmo vale para as metas de produto ("sempre há algo para comprar", roda ≥ meta da bíblia, toda pista do Brasil com sub-região). Nada vai para o remoto com teste vermelho. Se três tentativas seguidas falham, para tudo e volta à causa raiz.
5. **Versão do conteúdo como decisão consciente, uma vez só.** Antes do lançamento, invalidar recordes custa pouco, porque ninguém de fora tem recorde. Por isso todas as mudanças de física e de traçado se concentram nas ondas M, N e O. Na O:
   - a `lapContent` (`src/game/content-version.ts`), que hoje põe **todas** as constantes na impressão da volta, passa a levar uma **lista explícita** das constantes que um carro sozinho lê. Um teste exige que mudar uma constante de IA ou de modo não mude a `lapFingerprint`. Essas constantes vão para módulos próprios, que continuam na `onlineFingerprint`;
   - `LANDMARK_PLAZAS` congela, com teste.

   Daí em diante vale **"Física e traçado congelados v1"**. Depois do lançamento, mexer numa constante de física é decisão do dono e abre uma temporada nova.
6. **Medir com dados, com amostra declarada.** Cada coisa tem a sua régua:
   - economia: sonda (`scripts/career-balance.ts`);
   - disputa: termômetro (`src/game/race-feel.ts`), nas corridas da IA e **também nas corridas humanas do diário**;
   - comportamento: diário de jogo local (`src/game/playlog.ts`);
   - desempenho: banco de prova (`?bench=1`) e o gate de contagem por viewport;
   - visual e reconhecimento: teste cego com pessoas que nunca jogaram.

   As metas são escritas em contagem ("9 de 10 testadores") e valem só com a amostra mínima; abaixo dela, são indicativas. Saturação de imagem é alerta, nunca meta. O dono aprova, mas não é cobaia.
7. **A beleza vem de estilo, não de força bruta.** Forma, cor saturada, luz de borda e gradação, com 60 fps no Steam Deck e no co-op de 4. Toda peça visual tem degrau por qualidade. A única dependência de produção é `three`; os addons de `three/examples/jsm` não contam como dependência nova.
8. **Arte: o pipeline vem primeiro, e a arte segue um contrato.** É proibido gerar modelos pela API do Meshy. Os caminhos são três: procedural em código, pacotes CC0 com a licença registrada por arquivo e arte contratada que segue a bíblia e o **contrato do asset** escrito na K. Nada de logotipo, marca ou cópia de carro real. Imagem de terceiros, como as referências de MK8 e CTR, fica fora do repositório.
9. **Sofá primeiro.** Tudo cabe de 1 a 4 painéis, nas 7 resoluções e no Deck (piso de 12/10 px no HUD), e é navegável por controle. Todo texto existe em PT e EN (`tests/i18n.test.ts`).
10. **Orçamento de tempo entre corridas.** Da chegada à próxima largada: no máximo 15 s sem tocar em nada (avanço automático) e no máximo 2 entradas pulando (segurar A pula tudo). O teste fica no `scripts/playtest.mjs` desde a N e roda no "pronto" de toda onda que acrescentar tela entre corridas.
11. **Uma coisa de cada vez; em paralelo, só o que não se pisa.**
    - No máximo 5 frentes por onda, com arquivos disjuntos.
    - Cada área central (física, IA, economia/save) tem no máximo uma frente por onda.
    - **No máximo 3 frentes capturando imagem ao mesmo tempo**, com fila de capturas: a máquina tem 4 núcleos e o swiftshader é lento.
    - **Contratos primeiro**: os tipos e campos de save que mais de uma frente usa são commitados pelo orquestrador antes de a onda abrir.
    - **Cada item tem um dono.** Animação de marco fica na Q5/U2; onda de choque e marcas de pneu, na M5; céu, névoa e sombra, na L1 e na M2; tutorial, só na O3; o cadastro das conquistas na Steam é feito uma vez, depois da T.
    - Agente não edita doc compartilhado: manda o parágrafo pronto, e o orquestrador cola.
12. **O modo Retrô continua funcionando** e não recebe as novidades, que ficam declaradas como só-3D. As exceções são a pintura, a cor da chama e a pose de derrapagem, que cabem no sprite.

## 3. O laço do jogo

**Laço curto: uma corrida, de 2 a 4 min.**

```
  ┌─► Sobrevoo do lugar + rival da copa ─► largada-foguete
  │      │
  │   Corrida: derrapar (mini-turbo) · vácuo · raspão · ultrapassar (+1) → enche o nitro
  │      │
  │   Chegada → pódio 3D → PAGAMENTO ($ contando · ★ estrelas · rival · recorde)
  │      │                                   (≤ 15 s até a próxima largada)
  │   Garagem 3D: peça nova aparece no carro · acabamento · roda · carro novo
  │      │
  └── "Próxima corrida" (1 botão)        meta na tela: "faltam $ 3.400 para…"
```

**Laço médio: uma copa, de 10 a 15 min.** São 3 corridas. Depois vem o troféu (bronze, prata ou ouro), o carimbo bate, o postal entra no passaporte e a região se acende, e o botão é "Próxima copa". Quem é eliminado recomeça a copa com um botão, sem refazer o lobby.

**Laço longo: semanas e meses.**

```
Passaporte 27 + vistos 7 ─┐
Álbum de 161 marcos ──────┤
Caderno de 27 desenhos ───┤
Garagem 14 + rival + raros┼──► próximo objetivo sempre na tela inicial
Oficina de estilo ────────┤     (curto · médio · longo)
Estrelas · medalhas/classe┤
Nível de piloto (sofá) ───┤
Rivais com placar · Liga ─┤
Desafio do dia (sem FOMO) ┤
Carreira Lendária ────────┘
```

---

## 4. Ondas

### Regras comuns a todas as ondas

1. **Cartão.** O orquestrador escreve `docs/ondas/<onda>.md` a partir desta seção. As listas de arquivos saem de grep dos símbolos, e o cartão diz **quem é dono de cada arquivo disputado** naquela onda.
2. **Contratos.** O orquestrador commita o que está em "Antes de abrir".
3. **Agentes.** Até 5, em worktree, com o modelo de instrução do PLAYBOOK §4. A mescla é uma frente por vez. O que uma frente entrega para outra ligar entra na mescla.
4. **Fecho de onda:**
   - suíte completa (`NC_REQUIRE_RELAY=1 npx vitest run --testTimeout=180000`) e `npm run build`;
   - teste de compilação de todos os programas de shader em cada qualidade (criado na L2);
   - contagem de testes e estado no `CLAUDE.md`, e a onda marcada aqui;
   - capturas antes × depois, numa folha única por marco, com a recomendação já marcada;
   - fecho com DONE, DONE_WITH_CONCERNS, NEEDS_CONTEXT ou BLOCKED.
5. **Ondas visuais (L, M, Q, U).** Além das 7 câmeras de referência, uma folha de contato com uma captura por copa (34), gerada em segundo plano. Mais o gate de contagem por viewport (L2). O dono confere a build Windows e a do Deck.
6. **Estabilização.** Meio dia de caça a bugs por lentes (fluxo, save, online, render) antes de cada versão de teste (0.1.1, 0.2, 0.3) e antes do congelamento (O).

### Trilhas contínuas (correm ao lado das ondas)

- **Trilha D: desenhos do minimapa em cartum.** É o trabalho em andamento, parado para este cronograma (`docs/PISTAS.md`, "Desenhos em cartum"). Na K (frente K5) entram o trecho `bend`, o teste de fidelidade e o Cristo aplicado em `copacabana`. O orquestrador desenha as rodadas só em `scripts/track-art.ts`, e cada rodada vai ao dono numa folha (`scripts/art-sheet.ts`):
  - **Rodada 1, na K:** SP (MASP), MG (Pampulha), ES (Convento da Penha), SC (Hercílio Luz), RS (cuia refeita), PR (araucária), DF (avião), GO (gota), MS (peixe).
  - **Rodada 2, na L:** MT (jacaré), BA (caravela), SE (caranguejo), AL (jangada), PE (sombrinha de frevo), PB (balão), RN (caju ou cajueiro), CE (lua), PI (Serra da Capivara).
  - **Rodada 3, na M:** MA, PA, AM, AP, RR, RO, AC, TO.

  Na **O (frente O1)**, as 26 aprovadas e a cuia refeita vão para as pistas de uma vez, antes do congelamento.
- **Trilha A: arte externa**, que depende das decisões do dono.
  - **Marco 1:** encomendar 3 ou 4 *style frames* a um ilustrador (pinturas por cima das capturas de Copacabana, da garagem e de Foz; gasto pequeno).
  - **Marco 1 ou 2:** encomendar **1 carro-herói de teste e o Cristo-régua** pelo contrato do asset.
  - **Marco 2:** orçamento cheio, se aprovado. O artista faz os 14 carros, os ~10 marcos-herói e os **retratos 2D dos 12 rivais e dos 20 pilotos** (3 expressões cada), e o trabalho dele entra na U.
  - Música e efeitos licenciados ou contratados, se aprovados no Marco 2.
- **Trilha S: Steam e online.**
  - **Relay no ar** assim que o VPS for aprovado. Pode sair junto da 0.1.1. Leva:
    - wss pelo roteiro systemd + Caddy de `docs/ONLINE.md`;
    - o endereço dele como padrão, no lugar de `ws://localhost:8787`;
    - a mensagem "npm run relay" fora da tela do jogador;
    - o `server/relay.mjs` aceitando uma **faixa** de `PROTOCOL_VERSION`, porque hoje recusa qualquer outra que não 1, deixando a separação de builds com a `onlineFingerprint`;
    - redeploy, com `scripts/playtest-online.mjs` contra o VPS, a cada onda que mudar o protocolo (M).
  - **Convite pela Steam** assim que existir o App ID. O lobby da Steam serve só como convite: `createLobby` mais `setData('sala', código)`, botão "Convidar amigo" e entrada pelo callback `GameLobbyJoinRequested`. A **leitura de `+connect_lobby` no argv do `desktop/main.cjs`** cobre o jogo fechado. O Rich Presence usa `steam_display` e o arquivo de localização, **sem a chave `connect`**: o steamworks.js 0.4.0 não tem `GameRichPresenceJoinRequested`, então o "Entrar no jogo" pela lista de amigos cairia num callback que o jogo nunca recebe. O Rich Presence é limpo ao voltar ao menu. Teste com uma DesktopApi falsa nos dois caminhos.
- **Trilha C: comunidade (dono).** Página "Em breve" na Steam logo depois do Marco 3, com as capturas da cara nova; precisa do nome, do logo e do App ID. Devlog quinzenal e Discord desde então. Steam Playtest público a partir da 0.3. No vão de janeiro a junho, festivais e a demo.

---

### Onda K: "Mais uma corrida, e medida"

**Objetivo.** Tirar o atrito entre uma corrida e a próxima, começar a medir o que as pessoas fazem e pôr gente de fora para jogar já na 1ª semana. A onda também acaba com a "volta da vitória" da 2ª metade da carreira e deixa prontos a régua, o banco de prova e o contrato da arte nova.

**Antes de abrir:**
- Decisões do dono:
  - nº 1: aprovar o plano e as horas semanais;
  - nº 2: repositório e licença, **antes de qualquer arte ou versão nova**;
  - nº 3: Steamworks, se der.
- Registrar no `docs/QA.md` §5 o que os amigos disseram da 0.1, que hoje está vazio.
- Contratos em `src/core/types.ts`: `RaceConfig.aiPace?: number` (ausente vale 0, o comportamento de hoje) e o `TrackOp` `{ op: 'bend'; length; curve; hill? }`, os dois com padrão em `deserializeRace`.

**Trabalho do orquestrador nesta onda** (não é frente):
- A **bíblia de estilo** em `docs/DIRECAO-DE-ARTE.md`, com:
  - **referência principal declarada**: Mario Kart 8 Deluxe; CTR Nitro-Fueled para o mundo; Hot Wheels Unleashed só para o verniz;
  - **forma da luz**: difuso em rampa suave e contínua, com terminador tingido, sem degraus de cel; sombra projetada nítida, em 2 tons, na cor do bioma;
  - números: roda conforme a decisão de proporção, faceta visível ≤ 15°, carro como o objeto mais saturado do quadro, névoa só depois de ~80 m, tetos de triângulos por qualidade;
  - as 7 câmeras de referência.
- O **contrato do asset** para o artista:
  - carro: camadas de pintura, detalhe, faixa A/B e aro; emissivos de farol, lanterna e escamoteável; ponto `seat`; nós `kit_<peça>_<nível>`; UV com atlas de paleta 256² e normal map opcional; LOD0/LOD1; teto de triângulos; pegada;
  - marco-herói: tetos, LOD, nós animáveis e o contrato do marco a 26 m da pista.
- Os docs alinhados ao alvo novo: `ARTE.md` (sai "sem textura" e "cor chapada por face"), `VISUAL.md`, `ROADMAP.md` (ondas E–J, passos 2.1 e 2.2, custos de arte reestimados para cartum), `DESIGN.md`, `ARQUITETURA.md` e a descrição do `package.json`.
- `docs/ondas/` com os cartões da L à N, e a **rodada 1** dos desenhos.

| Frente | Entregas | Esforço | Arquivos (dono nesta onda) |
|---|---|---|---|
| **K1 Fluxo sem atrito** | (a) Enter ou A no menu já senta o P1. Depois do PRONTO, o foco vai para INICIAR, e Enter no carro, na pintura ou na direção não desfaz mais o PRONTO; hoje a dica "Enter ou Start" tira o PRONTO de quem joga no teclado. Segurar Start ou Enter larga, e a dica fica certa para cada dispositivo. (b) Com 1 assento, somem o Modo e as 4 assistências de co-op. (c) Eliminado: "Recomeçar a copa" em foco. Campeão: "Próxima copa". Corrida rápida: "Mais uma?" com a próxima pista. Assentos, carros e pinturas continuam. (d) O cartão "Primeira vez?" passa a receber o foco. (e) RECORDE só aparece quando uma marca anterior é batida; na 1ª vez, "Primeira marca" (hoje sai RECORDE até em 20º). (f) Créditos corrigidos: Three.js/WebGL, arte CC0. | M | `src/ui/screens/lobby.ts`, `results.ts`, `tutorial.ts`, `src/ui/menus.ts`, `src/ui/strings.ts`, `src/game/session.ts` (`toMain`, recomeço), `src/game/save.ts` (`recordRaceResults` informa se superou), `scripts/playtest.mjs` |
| **K2 Medir** | (a) **Diário de jogo local** (`src/game/playlog.ts`): funil título → largada (tempo e entradas); por corrida, modo, pista, posição, duração, eliminação, desistência, assistência, box ou tanque vazio, batidas e **o termômetro de emoção do humano**; sessões datadas. Fica num anel com teto. (b) **Relatório exportado em arquivo** pelo `writeJson` que já existe, mais "Copiar relatório"; o botão "Abrir pasta" entra na mescla, pelo `main.cjs` da K4. Nada sai do computador. (c) **Questionário de 3 perguntas** depois da 3ª corrida ou ao sair, numa tela nova (`src/ui/screens/survey.ts`) registrada na mescla. (d) **Termômetro de emoção** (`src/game/race-feel.ts`, puro): ultrapassagens por minuto, segundos em disputa, segundos sozinho, % de pé no fundo, % com nitro, raspões. É usado pelo `scripts/balance.ts -- corrida` e grava a linha de base das 109 pistas. | M+M | `src/game/playlog.ts`, `src/game/race-feel.ts`, `src/ui/screens/survey.ts`, `src/errors/options.ts`, `scripts/balance.ts` |
| **K3 Ritmo da IA e sonda 2.0** (área IA/economia) | (a) `careerAiPace(career)` gera o `RaceConfig.aiPace`: começa meio degrau abaixo da dificuldade escolhida e, do DF em diante, sobe até ~0,8 degrau acima no Mundial, interpolando `DIFFICULTY_SPEED`/`DIFFICULTY_SKILL` em `ai.ts`. Aqui sai o **mecanismo e uma calibragem provisória**; a definitiva é na O2, sobre a física final. (b) A sonda simula 5 perfis (focado, colecionador, vaidoso, sempre 1º, sempre 8º) e mostra, por copa, o dinheiro parado e as corridas sem compra. Fica preparada para receber perfis com habilidade de derrapagem e de nitro (O2). | M+M | `src/core/career.ts`, `src/core/sim/ai.ts`, `src/core/constants.ts`, `src/core/serialize.ts`, `src/game/career-session.ts`, `scripts/career-balance*.ts`, `tests/career-balance.test.ts` |
| **K4 Banco de prova e base gráfica** | (a) `?bench=1` e `--bench` gravam um JSON por cena e por qualidade: FPS médio, 1% low, chamadas, triângulos, passes e pixels de alvo de render por viewport. (b) `tools/referencias.mjs`: as 7 câmeras × 3 períodos, as folhas frente34, tras34 e lado dos 13 carros depois da onda I (a última é de 30/09) e a montagem antes × depois. Saturação e contraste entram só como alerta. (c) **Spike WebGPURenderer + TSL**, em branch descartável: Copacabana medida no swiftshader e no Electron do Linux, mais o custo de migrar os shaders de carro e de cenário. Sai um parágrafo com a decisão: WebGL com `onBeforeCompile` ou a migração, e o porquê. (d) Botão "Abrir pasta" do relatório. | M+M | `src/main.ts` (modo bench), `desktop/main.cjs`, `tools/referencias.mjs`, `tools/render-harness.*` |
| **K5 Desenhos D1 e as batidas no cenário** (trilha D) | O trecho `bend` (curva constante, sem rampa) no DSL e o `encodeCurves`. O **teste de fidelidade**, escrito antes, compara o contorno com o `ART` normalizado. O **Cristo aplicado em `copacabana`**. Um teste escrito antes trava as batidas no cenário que os desenhos de polígono criaram, e a correção as devolve ao nível anterior: Maceió 32 → ~5,7, Palmas 56 → ~29, Rio Branco 16 → ~1, Monte Roraima 17 → ~8 (a causa está nas quinas da jangada, do sol e dos tufos; os arquivos saem do grep no cartão). | M | `src/core/track/builder.ts`, `src/core/track/tracks.ts`, `scripts/shape-to-track.ts`, `scripts/track-art.ts`, `scripts/art-sheet.ts`, `tests/track.test.ts`, o `EXPECTED` de copacabana |
| **K6 Ajustes do dono** (09/10/2026) | (a) **Nitro que falha**: o aperto de borda (nitro e marchas) fica guardado até um passo da física usá-lo, em vez de morrer no quadro sem passo. Teste antes, visto falhar: quadros de 1/144 s com um aperto só disparam o nitro. (b) **Carro parado acelerando**: depois da bandeirada, a cena segue andando até o resultado. Os carros correm em piloto automático numa cópia só de exibição (o estado da corrida e o `hashRace` não mudam), e o motor acompanha. Teste antes: entre o `race_over` e o resultado, o z do carro mostrado cresce. (c) **Nome da cidade**: `city` em `TrackPlace` para os 109 lugares (teste: nenhum vazio), e o título "Cidade · trecho" na escolha de pista, na largada e no resultado. (d) **Nome no minimapa**: a cidade em cima do minimapa, no estilo do HUD. (e) **Banners**: ATOMATIZA e GALERIA ATOMO na lista de placas, nas cores das marcas (logo vira textura quando chegar). | M | `src/ui/input.ts`, `src/game/session.ts` (laço do quadro e fim de corrida), `src/core/data/places.ts`, `src/render/minimap.ts`, `src/render/hud.ts`, `src/render/scenery/textures.ts`, telas de pista/largada/resultado, `tests/` novos |

**Fecho:** estabilização e **versão 0.1.1** para 5 a 8 amigos, com o LEIA-ME e o roteiro de 30 minutos de `docs/QA.md` atualizados e o relatório exportado. É a **linha de base** de todas as metas. Se o VPS já estiver aprovado, o relay vai junto (trilha S).

**Pronto quando:**
- O `scripts/playtest.mjs` conta as entradas: do menu à contagem, no máximo 4 (hoje são ~9); do resultado à próxima largada, no máximo 2.
- Três testes foram vistos falhar e agora passam: Enter depois do PRONTO não o desfaz; recomeçar a copa mantém os mesmos humanos; a 1ª corrida em 20º não mostra RECORDE.
- O `tests/career-balance.test.ts` inclui br_pb e br_pe exigindo média de 2,5 a 5,5 (hoje 1,17 e 1,50), visto falhar antes. Com `aiPace` ausente, o `sim-golden` não muda.
- Cristo com erro médio ≤ 1,0 e fechamento ≤ 3 em 100 (eram 2,4 e 68). O `perf-sim --fingerprints` muda só copacabana e as 4 pistas das batidas.
- O bench grava um JSON válido no swiftshader. **O dono roda o bench no PC e no Deck.** Se não tiver um Deck, escolhe outro aparelho de referência e diz isso. **Sem esses números, a L não abre.**
- K6: os dois testes de defeito (nitro no quadro sem passo; carro andando depois da bandeirada) foram vistos falhar e agora passam; nenhum lugar sem cidade; a captura mostra a cidade no minimapa e as placas novas.
- A decisão do spike está registrada, e a 0.1.1 foi entregue.

**Duração:** 2 dias-base (1,5 + 0,5 da K6). **Depende de:** nada. **O dono vê:** o fluxo novo, a bíblia e o quadro de referências (aprova), a folha da rodada 1 e o Cristo na pista, o relatório da 0.1.1.

---

### Onda L: "Fatia vertical de última geração"

**Objetivo.** Levar ao acabamento final um pedaço do jogo: a Orla de Copacabana do seg. 70 até o Cristo, um carro da arte nova pelo pipeline novo, o HUD e a tela de título. A prova é o teste cego com gente de fora, e só então o resultado se espalha (M). O dono decide o orçamento de arte vendo isso.

**Antes de abrir:**
- Os números de FPS do PC e do Deck (K4), a decisão do spike e a bíblia aprovada.
- Contratos:
  - API do `stylize` (`applyStylize(material, família)`, `gradingFor(paleta)`);
  - `Quality` com um auxiliar `atLeast(q, nível)` no lugar dos `=== 'high'` (em `renderer.ts`, `cars.ts`, `cars/material.ts` e `sky.ts`) e todos os `Record<Quality, …>` atualizados (`sky.ts` `FOG_DENSITY`, `roadframe.ts` `FRAME_AHEAD`, `render-pseudo3d/renderer.ts`);
  - os atributos `aAO` e o bit de vidro em `aMat`;
  - o contrato do asset da K levado ao `check.ts`.

| Frente | Entregas | Esforço | Arquivos (dono nesta onda) |
|---|---|---|---|
| **L1 Luz, cor, chão e céu** | `src/render/stylize.ts`, injetado por `onBeforeCompile` **compondo com o callback que já existe** (o dos carros, `nitro-car-v1`, e o da névoa do cenário, `scenery-haze`) e com **chave de programa nova**. Vale para os materiais com luz (~15 `MeshStandardMaterial` e 1 `MeshPhysicalMaterial`; o cartão confere a lista por tipo). Os `ShaderMaterial` (domo, nuvens, mar, espuma, anel, chama, partículas) e os `MeshBasic` recebem só a gradação, por um trecho próprio. O tone mapping sai do ACES para Neutral ou AgX, com `CustomToneMapping` e gradação por bioma × período. Nesta onda são calibrados coast-dia (Copacabana) e city_night (Sampa, por causa do bloom × janelas). Chão: detalhe pintado em coordenada de pista, asfalto ardósia, zebra, acostamento e box **sem `flatShading`**, névoa por perspectiva aérea (nada até ~80 m), sombra nítida em 2 tons. Céu pintado, nuvens com borda acesa, mar com espuma. A **fórmula de névoa do `sight.ts` e do `caption.ts`** é atualizada junto, com um teste que compara as duas. O tom do carro vem pelo `applyStylize`, que a L3 liga. | GG | `src/render/stylize.ts`, `palette.ts`, `road.ts`, `road-textures.ts`, `terrain.ts`, `sky.ts`, `scenery/runtime.ts` (só material), `scenery/sight.ts`, `caption/caption.ts` (névoa), `tests/render-ground.test.ts` (reescrito de propósito) |
| **L2 Pós, qualidade e o gate de desempenho** | Degraus de qualidade:<br>– **Baixa**: sem pós, com gradação e **sombra de contato sob o carro**;<br>– **Média** (padrão do Deck): bloom em meia resolução e FXAA ou SMAA;<br>– **Alta**: MSAA, bloom, desfoque radial no nitro e acima de 85% da velocidade, GTAO em meia resolução até 2 jogadores;<br>– **Ultra** só se o bench mostrar ganho visível (sombra 2048 em cascata, GTAO em resolução cheia); se não mostrar, é cortado.<br>Também: sombra mais barata em tela dividida (mapa em quadros alternados por viewport com 3 ou 4 jogadores; o mundo é reposicionado por viewport, por isso não dá para compartilhar o mapa); modo opcional de 30 fps travados na Média para 3 e 4 jogadores. **Resolução dinâmica dentro de um alvo de tamanho fixo**, com degraus e histerese longa, sem recriar o composer (hoje o `resize()` recria o EffectComposer e os 11 alvos do bloom). Qualidade automática na 1ª abertura. As duas coisas ficam desligáveis no harness e nos playtests. **Teste que compila todos os programas** em cada qualidade (`renderer.compile` + `getProgramInfoLog` vazio). **Gate automático por contagem**: chamadas, triângulos, passes e pixels de alvo por viewport, por qualidade. Folha A/B/C de contorno para o dono. Fundo do menu com carros correndo (hoje o `renderIdle` os esconde). | G+M | `src/render/renderer.ts`, `src/render/postfx/` (novo), `src/game/contracts.ts` (Quality), `src/ui/screens/options.ts`, `src/game/settings.ts`, `scripts/playtest-memoria.mjs`, `tools/render-harness.*` (parâmetros) |
| **L3 Pipeline de arte e o carro-régua** | O `gltf.ts` passa a ler UV, `map` (atlas de paleta 256²) e normal map opcional. **A roda vem do .glb.** LOD0/LOD1. **Um modelo por carro**: o instanciamento agrupa por modelo, e não por `CarDef.body`, então o Falcão deixa de dividir a forma com o Boitatá. **Carga por arquivo no Electron** por protocolo próprio, no lugar da data URL. O `check.ts` valida o contrato do asset (`kit_*`, `seat`, `decal_*`). **Um carro CC0 estilizado de ponta a ponta**, com a licença registrada, na corrida e no estúdio do harness. Dois estilos procedurais (GT e micro) na proporção da bíblia, como reserva e LOD: roda maior, traseira-vitrine, cabine bolha, vidro com reflexo pintado e AO por vértice. A **folha "mesma pegada × curto e gordo"** serve à decisão de proporção. | GG | `src/render/cars/gltf.ts`, `assets.ts`, `check.ts`, `models.ts`, `src/render/cars.ts`, `cars/material.ts`, `kit.ts`, `body.ts`, `styles/gt.ts`, `styles/micro.ts`, `desktop/main.cjs`, `tests/car-gltf`, `tests/car-models` |
| **L4 Copacabana no acabamento final** | **Kit redondo** em `scenery/geom.ts`: caixa com bisel, `ExtrudeGeometry` com `bevelEnabled` e `curveSegments` ≥ 6, AO por vértice. Aplicado às peças do trecho: prédios aptbeach com relevo, quiosques, calçadão em ondas, palmeiras de cartum com copa de normais esferizadas e tufos com recorte alfa. **Cristo e Pão de Açúcar refeitos como heróis-régua**, procedurais no padrão novo até a arte contratada chegar: só a malha muda, `perLap`, `place` e `side` ficam iguais, com teste de `LANDMARK_PLAZAS` inalterado. **Paisagem do Rio**: os morros e o Pão de Açúcar na baía, num módulo novo `src/render/horizon.ts` ligado ao `terrain.ts` na mescla. A pegada continua igual à colisão. | GG | `src/render/scenery/geom.ts`, `structures.ts`, `props.ts`, `vegetation.ts`, `scenery/landmarks/brasil-centro-sul.ts`, `src/render/horizon.ts` (novo), `tests/scenery*`, `tests/landmarks-leitura-*` |
| **L5 HUD, título e disputa que se vê** | Fonte de exibição OFL embutida (o dono escolhe entre Fredoka, Baloo 2 e Lilita One), painéis gordos com contorno e sombra colorida, logo SVG provisório na tela de título. Posição com mola e troca de cor, "+1/+2", distância em segundos para o carro da frente e o de trás, faixa ÚLTIMA VOLTA, ícone VÁCUO, minimapa maior, combustível e nitro legíveis sem depender de vermelho e verde. **Raspão** derivado fora do núcleo (`race-feel.ts`): faísca lateral, metal arrastando e vibração. "Zum" na ultrapassagem colada. **Fala do rival durante a corrida** (os textos já existem em `rivals.ts`), com intervalo mínimo. **Efeitos sonoros principais em amostras CC0 ou royalty-free** (impacto, pneu, zum, interface, metal, torcida), com a licença registrada por arquivo e carregados por `decodeAudioData`, sem pacote novo. | G | `src/render/hud.ts`, `hud.css`, `hud-rival.css`, `src/ui/styles.css`, `src/ui/screens/simple.ts` (título), `src/render/strings.ts`, `src/game/session.ts` (`handleEvent`), `src/audio/sfx.ts`, `src/game/rumble.ts`, `src/render/effects.ts` (faísca do raspão), `src/assets/fonts/`, `src/assets/audio/` |

**Fecho: teste cego visual.** No mínimo 5 pessoas que nunca viram o jogo recebem capturas misturadas, sem saber qual é qual: o jogo de hoje (linha de base da K), a fatia nova (com e sem HUD) e capturas de referência. Pergunta-se "parece um jogo de que geração? (antiga / anterior / atual)" e pede-se uma nota de 1 a 5 para "parece de estúdio". **Meta da fatia: pelo menos 4 de 5 dizem "atual" e a nota média é de 3,5 ou mais.** Isso mais a aprovação do dono abrem o Marco 2.

**Pronto quando:**
- Testes vistos falhar antes: todo material com luz recebe o trecho de luz; `LANDMARK_PLAZAS` não muda; a fórmula de névoa do `sight.ts` bate com a do render.
- Passam o teste de compilação de programas e o `render-ground` reescrito.
- O `car-gltf` cobre UV, roda do .glb, LOD e modelo por carro. O carro CC0 passa no `check-car`.
- O gate fica dentro do orçamento. No bench do Deck com a fatia: Média com 1 e 2 jogadores a ≥ 60 fps (1% low ≥ 50); 4 jogadores a ≥ 60 na Baixa, ou a 30 travados na Média.
- O `sim-golden` e o `hashRace` ficam intactos.
- O `playtest-layout` passa, com o HUD de 2 e 4 jogadores no Deck.
- A contagem derivada de raspões bate com o `sideContact` real (diferença ≤ 5%).

**Duração:** 2,5 dias-base. **Depende de:** K. **O dono vê:** a fatia, a folha A/B/C de contorno, as 3 fontes, a folha de proporção do carro e o resultado do teste cego. **Decide:** orçamento de arte, música e personagem; contorno; fonte; proporção.

---

### Onda M: "Derrapar, e o visual em todas as pistas"

**Objetivo.** Pôr o laço de habilidade de Mario Kart 8 e CTR em cada curva. Ao mesmo tempo, levar o visual aprovado na fatia às 109 pistas, ao cenário inteiro, aos **161 marcos** e aos 13 carros procedurais.

**Antes de abrir:**
- Decisões: botão de derrapar e mapa padrão completo (nº 9); impulsores e saltos (nº 11); proporção do carro (nº 7).
- Contratos:
  - bit de derrapagem no `PlayerInput`;
  - `driftDir`, `driftTicks` e `driftCharge` em `CarState`, com padrão em `deserializeRace` **e dentro do `hashRace`**, senão a checagem de dessincronia a cada 60 ticks não os vê;
  - eventos `drift_charge` e `drift_boost`;
  - `PROTOCOL_VERSION` 2, com o relay aceitando a faixa (trilha S).

| Frente | Entregas | Esforço | Arquivos (dono nesta onda) |
|---|---|---|---|
| **M1 Núcleo da derrapagem** (área física) | Estado de derrapagem no `stepRace`: centrífuga ×~0,55, volante puxando para dentro e perda pequena de velocidade. A carga sobe em 3 níveis (azul, laranja, roxa), só em curva acima de um limiar. Ao soltar, impulso de 0,5, 1,0 ou 1,5 s, menor que o nitro. A IA e a direção assistida derrapam nas curvas ≥ `AI_BRAKE_CURVE`. O bit vai na rede. O remapeamento passa de 8 para 10 ações (derrapar e olhar para trás; olhar para trás é só render), com **teste de conflito kb1 × kb2 e migração dos mapas salvos**. Se a decisão for "curto e gordo", `CAR_LENGTH` e a colisão mudam aqui. O combustível das 109 pistas segue verde, com calibragem provisória; a final é na O2. Pose de derrapagem no sprite do Retrô. | GG | `src/core/types.ts`, `constants.ts`, `sim/physics.ts`, `sim/ai.ts`, `sim/assist.ts`, `src/core/serialize.ts`, `src/net/protocol.ts`, `src/net/lockstep.ts`, `server/relay.mjs`, `src/ui/remap/bindings.ts`, `src/game/content-version.ts`, `src/render-pseudo3d/sprites.ts`, `EXPECTED` do `sim-golden` |
| **M2 Luz, chão e céu nas 109 pistas** | Calibrar as 18 combinações (6 biomas × 3 períodos). Céu e mar por bioma, reflexo de lente no entardecer (39 pistas). Folha de contato com 1 captura por copa (34), feita em segundo plano. | G | `src/render/palette.ts`, `stylize.ts`, `terrain.ts`, `road.ts`, `sky.ts` |
| **M3 Cenário e marcos redondos** | O kit redondo da L4 aplicado a tudo:<br>– prédios em 4 estilos, com cornija, varanda, toldo e quina chanfrada;<br>– props (poste, cerca, placa, arquibancada);<br>– vegetação-nuvem;<br>– **os 161 marcos**, com `ExtrudeGeometry` com bisel nos 4 arquivos que hoje usam `bevelEnabled:false` (`brasil-centro-sul.ts:338`, `brasil-norte-nordeste.ts:89`, `brasil-2-norte-nordeste.ts:91`, `mundo.ts:225`) e caixa com bisel no kit de marcos.<br>**Teste de faceta da bíblia sobre todos os modelos**, escrito antes (falha hoje). **Flora do Brasil**: `CACTI.br` (mandacaru, xique-xique, coroa-de-frade, palma) e `TREES.desert.br` (carnaúba, juazeiro, umbuzeiro) nas 7 pistas de deserto do Brasil, que hoje têm saguaro, marula e acácia. **Vento** por vertex shader, com fase por instância tirada de um hash da posição na pista (assim o balanço não escorrega com o carro nem difere entre as telas) e amplitude zero abaixo de 1,5 m. Impostores e LOD de longe pagam os triângulos (≤ 900 por segmento). A régua de leitura dos marcos é reescrita de propósito antes, onde precisar. `perLap`, `place` e `side` não mudam. | GG | `src/render/scenery/geom.ts`, `structures.ts`, `props.ts`, `vegetation.ts`, `catalog.ts`, `runtime.ts`, `scenery/landmarks/*`, `tests/scenery*`, `tests/landmarks*`, `tests/front-view.ts` |
| **M4 Carros na pista** | Os 13 estilos procedurais na proporção da bíblia, como **reserva e LOD**; o caminho principal é o .glb da arte. Entram roda grande, traseira-vitrine (lanternas grandes, escape, difusor, aerofólio de brinquedo), cabine bolha, vidro com reflexo e LOD de casco. O teto do casco sobe de propósito, com o teste escrito antes. Guinada da derrapagem (~0,35 rad) e faíscas nas rodas nas 3 cores. **Chama do nitro em malha**, com cor por instância pronta para o visual do carro. Rodopio, quique, squash & stretch e estrelinhas na batida. Cristas de morro só visuais, de 1 a 4 por volta. Os nomes Falcão GT e Tornado mudam, se o dono decidir. | G | `src/render/cars.ts`, `cars/styles/*.ts`, `body.ts`, `kit.ts`, `details.ts`, `wheels.ts`, `flame.ts`, `check.ts` (CAR_LIMITS), `tests/car-models`, `car-round`, `tests/estilo-carro.test.ts` (novo) |
| **M5 Efeitos e câmera** | Fumaça e poeira em bolha, faíscas esticadas, estrelinhas e "POW". **Onda de choque** na largada e no nitro (a N só liga a ela os eventos novos). **Marcas de pneu** num buffer em coordenada de pista, reprojetado por viewport e com teto de pontos. Linhas de velocidade 3D acima de 85% (sai o gradiente CSS `.lines`). Túnel de ar do vácuo, vindo de `computeModifiers`. Câmera: mola de FOV no engate do nitro, tremor fino acima de 90%, câmera mais baixa em velocidade, órbita ao cruzar em 1º. **Os deslocamentos dinâmicos têm limite e são modelados no `sight.ts`**, com teste. A base de `CHASE_CAMERA` não muda. | G | `src/render/effects.ts`, `src/render/camera.ts`, `src/render/hud.css` (só `.lines`), `src/render/scenery/sight.ts` |
| **M6 Traçado = mapa, e a corrida que muda** (09/10/2026; arquitetural) | (a) O 3D passa a girar pela escala de cada pista que faz a soma das curvas dar 360°, a mesma do minimapa: a curva da pista é a curva do mapa, e a pista fecha. Só renderização: física, recordes e `sim-golden` não mudam. Teste antes: o rumo 3D acumulado de cada pista fecha em 360° ± 2°, e o ângulo de cada curva bate com o do minimapa. Revisar o cenário que passa a se cruzar (pista fechada pode se sobrepor) com o teste de batidas da K5. (b) **Corrida que muda**: o período anda durante a corrida (dia → entardecer na última volta; entardecer → noite, com as luzes da cidade acendendo), interpolando as combinações calibradas na M2. Só visual. | G | `src/render/units.ts`, `road.ts`, `roadframe.ts`, `scenery.ts`, `sky.ts`, `palette.ts`, `tests/` novos |

Capturas: M2, M3, M4 e M5 capturam, então entram numa **fila, com no máximo 3 ao mesmo tempo**.

**Pronto quando:**
- Testes do núcleo escritos antes: a carga sobe nos 3 níveis em curva acima do limiar e não sobe em reta; soltar dá o impulso certo; o resultado é determinístico; o bit atravessa a rede (`net-lockstep`). Mais os testes de conflito do remapeamento e de migração.
- O relay aceita a v2 e o `playtest-online` passa.
- `npm run balance`: a IA usa a derrapagem nas curvas fortes. O termômetro, comparado à linha de base, mostra os segundos em disputa e as ultrapassagens subindo, sem aumento de batidas traseiras.
- Testes de faceta e de flora vistos falhar e agora verdes; vento zero entre 0,05 e 1,3 m; `scenery-forma` ≤ 900 por segmento; leitura dos marcos verde; `landmark-sight` ≥ 2,5 s; o `estilo-carro` cumpre a meta da bíblia.
- Folha das 34 copas. Gate e bench do Deck dentro do orçamento. O `playtest-memoria` não mostra crescimento.
- Teste cego visual nº 2, com capturas de 6 pistas: mesma meta da L.

**Duração:** 2,5 dias-base. **Depende de:** L e das decisões nº 7, 9 e 11. **O dono vê:** a derrapagem e o jogo inteiro com a cara nova. Joga uma corrida no sofá.

---

### Onda N: "Nitro que se ganha, e comprar dá vontade"

**Objetivo.** Pilotar bem vira velocidade, e quem larga em 20º ganha um momento de glória já no segundo zero. Do outro lado, comprar carro, comprar peça e trocar a cor viram prazer visível, e o dinheiro passa a estar no caminho padrão.

**Antes de abrir:** a decisão nº 12 (Expedição). Contratos:
- `CarState.nitroMeter` no lugar do `nitroLeft`, com padrão em `deserializeRace` e dentro do `hashRace` (o `nitroLeft` é usado em serialize, tutorial, HUD, IA, física, race e stats);
- os eventos de ganho de nitro;
- o **CarLook**: `HumanEntry.look`, `RenderFrame.looks`, `LobbyPlayer.look` opcional, `SaveData.seatLooks` e `cosmeticsOwned`, mais a assinatura de `createCarMaterial` e `buildWheel`;
- `Renderer.renderShowroom` e `renderPodium`;
- o formato de `CareerReport.lines`.

| Frente | Entregas | Esforço | Arquivos (dono nesta onda) |
|---|---|---|---|
| **N1 Nitro que se ganha e largada-foguete** (área física) | **Barra `nitroMeter`**: larga com 1 carga cheia e enche com o mini-turbo, o **estilingue do vácuo** (depois de 1,5 s no vácuo), o raspão sem bater, a ultrapassagem limpa (agora calculada no núcleo) e a largada perfeita. **Largada-foguete**: no online, as luzes da contagem são desenhadas adiantadas em *d* ticks (o atraso de entrada), e um **teste com atraso 3 e 12 exige o mesmo resultado** para o mesmo instante físico do aperto. A IA acerta conforme a habilidade, via `state.rng`. A peça de nitro da carreira vira "barra enche +X%". O cofre de nitro do co-op vira barra da equipe. A barra no HUD, o selo LARGADA PERFEITA e o motor subindo na contagem também são desta frente. | G | `src/core/types.ts`, `constants.ts`, `sim/physics.ts`, `sim/race.ts`, `sim/coop.ts`, `sim/positions.ts`, `sim/stats.ts`, `sim/ai.ts`, `serialize.ts`, `src/core/career.ts` (só a peça nitro), `src/render/hud.ts`, `hud.css`, `src/audio/engine.ts` |
| **N2 Palco 3D** | `src/render/stage.ts`: prato giratório, `RoomEnvironment` no PMREM, luz de 3 pontos com borda, sombra de contato. Desenha de 1 a 4 painéis, por scissor, nos retângulos da garagem. A câmera vai ao detalhe em foco. **A compra de carro vira revelação** (holofote, capa saindo, faróis acendendo, confete, "NOVO!"), e a compra de peça vira oficina (faísca, o carro afundando como mola). Faz também o **pódio 3D** com os 3 primeiros pintados. **A garagem tem qualidade própria**, por ser uma cena só, fora da corrida; a silhueta SVG fica só no Retrô. **A Concessionária vira tela própria** no menu (vitrine dos carros à venda, girando no palco), separada da Garagem (os seus carros, peças e pintura) — pedido do dono, 09/10/2026. Exporta `mountShowroom(painel)` para o lobby, ligado na mescla. | G | `src/render/stage.ts` (novo), `src/render/renderer.ts`, `src/ui/screens/garage.ts`, `garage.css`, `icons.ts`, `common.ts`, `src/render-pseudo3d/` (queda) |
| **N3 O visual do carro (CarLook)** | O pacote de aparência por assento × carro, que viaja pelo caminho da pintura: `withoutLook` antes da `RaceConfig`, validação em `parseServerMessage`, save com saneador e migração de `seatPaints`. **Acabamentos** metálico com flocos, perolado (iridescência), fosco, cromado e camaleão, em bits de instância, **com uma aproximação barata na Média** (o verniz hoje só existe na Alta, e a Média é o padrão do Deck). **Cor livre** por camada, com saturação mínima para não sair da paleta. **As 9 rodas** de `WHEEL_DESIGNS`, com uma lista por estilo, e 4 cores de aro. **Faixa** nenhuma, A, B ou A+B, separando o bit do aro do bit da faixa B. **Cor da chama.** O `racePaints` passa a desempatar também pela faixa. O catálogo fica em `src/game/cosmetics.ts`, ainda sem preço. | G | `src/game/cosmetics.ts` (novo, com saneador), `src/game/paints.ts`, `src/net/protocol.ts`, `src/game/online-session.ts`, `src/render/cars.ts`, `cars/material.ts`, `cars/wheels.ts`, `cars/paints.ts`, `cars/flame.ts`, `src/ui/screens/paint.ts`, `lobby.ts`, `online.ts` |
| **N4 Pagamento e o tempo entre corridas** | Tela **Pagamento** (`payout.ts`) com as linhas que já existem (posição, equipe, eliminação): o número conta, a moeda cai no cofre e aparece "Já dá para: Turbo nível 2!". No co-op, uma coluna por piloto e a linha do cofre; no versus, uma carteira por assento. A tabela de 20 linhas fica recolhida (top 3 mais a sua linha). O sequenciador do pós-corrida (`src/game/post-race.ts`) cuida de chegada → pódio → pagamento → próxima, **com avanço automático, até 15 s, e segurar A para pular tudo**. O teste de orçamento de tempo entra no `playtest.mjs`. As linhas novas de dinheiro ficam para a P1. | M+M | `src/ui/screens/payout.ts` (novo) e css, `results.ts`, `src/game/post-race.ts` (novo), `src/audio/sfx.ts`, `src/career/strings.ts`, `scripts/playtest.mjs` |
| **N5 Expedição como caminho padrão** (área economia/save) | Campeonato e Carreira viram um modo só, a **Expedição**, com dinheiro, garagem e compra desde a 1ª corrida. O menu de 12 a 14 itens vira **5 cartões**: Continuar/Expedição, Jogar junto, Jogar livre (rápida, contra-relógio, online, copa avulsa), Garagem e Passaporte. Opções, Controles e Créditos vão para um canto. **Próximo objetivo**: `src/game/goals.ts`, puro, com metas curta, média e longa que respeitam `walletOf` (cofre no co-op, carteira por assento no versus). Quadro "Desde a última vez". Migração de 3 tipos de save: só campeonato, só carreira, os dois. Conquistas COPA_* e passaporte continuam contando. | GG | `src/ui/screens/simple.ts`, `src/game/session.ts`, `src/game/career-session.ts`, `src/game/career-save.ts`, `src/game/save.ts`, `src/game/goals.ts` (novo) |
| **N6 Voz do carro** (09/10/2026) | (a) **Motor por carro** (14 timbres, em síntese, pela carroceria e pelo carro: V8 grave, 4 cilindros agudo, elétrico com zumbido) — veio da R3. (b) **Buzina por carro**, num botão próprio (controle: L3; teclado: H e G), ouvida por todos no sofá e no online: vai como entrada (`PlayerInput.horn`, padrão falso no `deserializeRace`) e não mexe na física nem no `sim-golden`. (c) **Balão de provocação**: segurar a buzina abre uma roda com 8 frases prontas; o balão aparece sobre o carro por 2 s, com limite de 1 a cada 5 s. Frases de brincadeira, sem palavrão (decisão nº 24); no online, silenciar por jogador. O rival da IA responde de vez em quando (liga com a L5). | M+M | `src/audio/engine.ts`, `src/audio/sfx.ts`, `src/core/types.ts` (só o campo), `src/ui/input.ts`, `src/render/` (balão), strings, `tests/` novos |

**Pronto quando:**
- Um teste por fonte de nitro, escrito antes; teto respeitado; a barra da equipe soma.
- No termômetro, o tempo com nitro sobe de 2–4% para 10–15%.
- O teste da largada passa com atraso 3 e 12.
- Teste: qualquer CarLook deixa o `hashRace` e o `sim-golden` idênticos. Save antigo sem `seatLooks` abre igual. Look inválido é recusado na sala online.
- Capturas de cada acabamento **na Média e na Alta**, da garagem com 1, 2 e 4 painéis, e de `?paintsheet=` com acabamentos × rodas.
- O `playtest-memoria` abre e fecha a garagem 36 vezes sem crescimento.
- Teste do orçamento de tempo entre corridas verde. Migração dos 3 saves verde. O `playtest-layout` passa.

**Duração:** 2,5 dias-base. **Depende de:** M. **O dono vê:** o nitro que se ganha, a garagem 3D, a revelação da compra, os acabamentos, o pagamento contando e o menu de cartões. **Decide:** nome e logo (nº 14), para a página "Em breve".

---

### Onda O: "Congelado v1" (traçado, física e a 1ª corrida)

**Objetivo.** Concentrar numa onda só toda mudança de conteúdo que falta, recalibrar uma única vez e congelar. Junto, ensinar o jogo na 1ª corrida e dar a cada corrida estrelas e um rival com história.

**Antes de abrir:**
- Decisões nº 10 (1ª corrida, duração e ordem das pistas), nº 11 (pads e rampas, se aprovados) e nº 13 (consulta sobre o Cristo, com a variante sem o Cristo pronta).
- Contratos: os campos de save de estrelas, troféus, placar de rival e progressão; a assinatura nova da `lapContent`.

| Frente | Entregas | Esforço | Arquivos (dono nesta onda) |
|---|---|---|---|
| **O1 Os 27 desenhos nas pistas** (trilha D) | Aplica as rodadas aprovadas: 26 pistas e a cuia refeita, com `bend` e um teste de fidelidade por desenho. Caldas Novas (1,9) e Canoa Quebrada (2,1) voltam à faixa de dificuldade da copa. **Se a decisão nº 11 for sim**, faixas de impulso e rampas com turbo de manobra em 1 pista por copa: os ops do DSL e o tempo no ar (`airTicks`) no núcleo, a IA olhando à frente e o desenho no render e no Retrô. | G (+G se pads) | `src/core/track/tracks.ts`, `builder.ts`, `src/core/types.ts` (só se pads), `tests/track.test.ts`, `EXPECTED` de sampa_noite |
| **O2 Recalibrar e congelar** (área IA; entra depois da mescla da O1) | **`lapContent` com lista explícita** das constantes que um carro sozinho lê. As de IA e as de modo vão para módulos próprios, e um teste prova que mudá-las não muda a `lapFingerprint`. **`LANDMARK_PLAZAS` congelado**, com teste. IA de ataque e defesa de última volta, por personalidade. Recalibra numa passada: a rampa da K3, o elástico, a escolta (VIP no top 3), o combustível das 109 pistas e o `career-balance`, com a **sonda ganhando perfis com habilidade de derrapagem e nitro**. Duração da corrida e ordem das pistas em `cups.ts` (por exemplo, 2 voltas nas 7 primeiras copas; Great Ocean e Amalfi para o fim; a regra das cidades à noite), mais as voltas da 1ª corrida. Sobe o `PHYSICS_REVISION`. | G+M | `src/core/constants.ts` (e os novos `constants-ai.ts` e `constants-modes.ts`), `src/game/content-version.ts`, `src/core/sim/ai.ts`, `personality.ts`, `coop.ts`, `src/core/data/cups.ts`, `src/core/career.ts` (rampa), `scripts/*balance*.ts`, `tests/career*`, `tests/content-version.test.ts`, `tests/sim-golden.test.ts` |
| **O3 A primeira corrida ensina** | Na 1ª abertura (`racesRun = 0`), o jogo vai direto à Orla de Copacabana, ou à variante sem o Cristo se a consulta pedir. As dicas aparecem na hora, por assento e com as teclas reais (`remap/labels.ts`): segure ↑ na contagem (largada-foguete), freie antes da curva forte, derrape aqui, o nitro enche quando você…, e uma **seta para o box**. Depois de 2 eliminações seguidas, o jogo oferece Freio automático ou Amador, recusável e sem voltar a cada corrida. O "Como jogar" (Autódromo-Escola) passa a ensinar derrapagem e largada. **Este é o único lugar onde o tutorial é escrito.** | G | `src/game/tutorial.ts`, `tutorial-session.ts`, `src/render/hud-tips.ts` (novo), `src/ui/remap/labels.ts`, `src/ui/screens/tutorial.ts`, `src/game/session.ts` |
| **O4 Estrelas, troféu e o rival com rosto** | Até **3 estrelas por corrida**: classificar, vencer o rival da copa e cumprir o objetivo da pista. O objetivo sai de um hash do trackId sobre a `SeatTelemetry` (sem batida, volta perfeita, 20 s no vácuo, sem box, do último ao top 3, nitro na bandeira) e aparece antes da largada e no pagamento. **Esta frente não mexe em dinheiro**: o bônus fica na P1. Troféu bronze, prata ou ouro por copa e o contador "x/327". **Placar persistente por rival e por perfil** ("Zé Turbo 7 × 5 você"), com as falas escolhidas pelo placar e o balão da corrida citando o placar. **Retratos dos 12 rivais e dos 20 pilotos** com 3 expressões (provoca, venceu, perdeu), em SVG procedural provisório até a encomenda 2D da trilha A. Os retratos aparecem no cartão do rival, no balão, no pagamento e, depois, na Liga. | G | `src/game/stars.ts` (novo, com saneador), `src/game/rivals.ts`, `src/core/data/drivers.ts` (só falas), `src/ui/screens/rival.ts`, `select.ts`, `payout.ts`, `src/ui/art/portraits.ts` (novo) |
| **O5 Progresso que fica e o primeiro minuto** | **Nível de piloto e maestria por carro em todos os modos** (rápida, festa, contra-relógio, online), por perfil e **sem dinheiro**. Libera títulos, molduras, acabamentos e aros especiais. Conquistas novas: 3 ou 4 que caem nos primeiros 15 minutos, as de economia e coleção, e NITRO_TRIPLO e NITRO_NA_BANDEIRA reescritas, com os IDs em `desktop/README.md`. **Reenvio à Steam ao abrir** das conquistas salvas (é defeito; o teste vem antes, com uma DesktopApi falsa). Idioma detectado (Steam, depois `app.getLocale`, depois `navigator.language`). Nome do P1 vindo da Steam. **Carro com cadeado** e "Compre na Expedição por $ X" no lobby, na festa e no online, em vez de sumir. | G | `src/game/progression.ts` (novo), `src/game/raceEnd.ts`, `src/game/achievements.ts`, `src/game/desktop.ts`, `src/game/settings.ts`, `desktop/main.cjs`, `desktop/README.md`, `src/ui/screens/lobby.ts`, `src/ui/strings.ts` |

**Fecho:** estabilização e **noite de sofá do dono com amigos**. Com a aprovação dele, fica declarado **"Física e traçado congelados v1"**.

**Pronto quando:**
- O `perf-sim --fingerprints` muda só as corridas esperadas. Os 27 testes de fidelidade passam. O `npm run balance -- corrida` deixa o índice técnico na faixa de cada copa.
- Testes vistos falhar: mudar uma constante de IA ou de modo não muda a `lapFingerprint`; `LANDMARK_PLAZAS` fica igual; reenvio à Steam; carro com cadeado não pode ser escolhido.
- O `career-balance` passa sobre a física final: o piloto médio fica com média de 3º a 5º em todas as copas. A escolta foi recalibrada. O `playtest-online` passa com atraso 3 e 12.
- O roteiro "primeiros 15 minutos" no `playtest.mjs` mostra o 1º dinheiro na tela em **menos de 4 minutos**.
- Sonda de estrelas: a 1ª estrela sai em ~80% das corridas, e as 3 em 15–25%.

**Duração:** 2 dias-base. **Depende de:** N e das decisões nº 10, 11 e 13. **O dono vê:** o jogo completo de pilotar, a 1ª corrida de um jogador novo e as estrelas. **Aprova o congelamento.**

---

### Onda P: "O laço completo e a economia" (versão 0.2)

**Objetivo.** Calibrar a economia uma única vez, sobre a física congelada. O dinheiro passa a ter destino até a última copa, a compra de carro dura a Expedição inteira e a melhoria aparece no carro.

**Antes de abrir:** a decisão nº 20 (estrelas e preços). Contratos: a assinatura de `performanceIndex` e o formato das condições de desbloqueio.

| Frente | Entregas | Esforço | Arquivos (dono nesta onda) |
|---|---|---|---|
| **P1 Economia única** (área economia) | `raceEarnings()` com as linhas novas: carros ultrapassados desde a largada, rival vencido, volta mais rápida, corrida limpa, troféu da copa e **bônus de estrela pago uma vez** (10–15% do prêmio). `PRIZE_BY_POSITION` recalibrado. "Revisitar copa" paga 50%. Preços da Oficina, num catálogo de ~$ 250–350 mil. **Preço e liberação dos 7 carros à venda espalhados pela Expedição, com o último perto da corrida ~80** (hoje a vitrine acaba na 33). O **teste "sempre há algo para comprar"** é escrito antes e falha hoje a partir do MT. Até a penúltima copa, o perfil do 4º nunca passa mais de 3 corridas sem poder comprar algo, e o dinheiro parado fica abaixo do prêmio de uma copa. O perfil do 1º só completa o catálogo depois de ~75% da carreira. | G | `src/core/career.ts`, `src/core/data/cars.ts` (preços), `src/game/cosmetics.ts` (preços), `scripts/career-balance*.ts`, `tests/career.test.ts` |
| **P2 Classes e carros para conquistar** | `performanceIndex(stats)` de 100 a 999 e classes D, C, B, A e S. Os pesos são medidos pelo `scripts/car-balance.ts`, com Spearman ≥ 0,9 contra a volta média nos 14 carros, de fábrica e completos. **O carro do rival de cada região** (uma variante dentro da faixa de ID, com o visual do rival) e **2 ou 3 lendários** (Boitatá Noturno, Saci Turbo…), com a condição de desbloqueio à mostra. Ficam fora do `AI_CAR_POOL`. Os nomes passam no teste de nomes reais. | G | `src/core/sim/stats.ts` (`performanceIndex`), `src/core/data/rare-cars.ts` (novo, juntado ao `CARS` na mescla), `src/game/unlocks.ts` (novo, com saneador), `scripts/car-balance.ts`, `tests/cars.test.ts` |
| **P3 A melhoria aparece no carro** | Cada nível vira uma peça visível pelos nós `kit_<peça>_<nível>`, que servem ao .glb da arte e ao procedural. Motor: tomada de ar e escape duplo. Turbo: intercooler. Pneus: mais largos, com letra colorida. Freios: pinça colorida e disco ventilado. Nitro: garrafas e chama maior. Tanque: bocal de corrida. Tudo no nível 3: kit de carroceria e aerofólio. As peças acendem por bits de instância, sem chamada de desenho a mais. **A IA mostra o kit do nível da copa**, e o rival que evolui passa a ser visto. O teto de triângulos é revisto de propósito. | G | `src/render/cars.ts`, `cars/styles/*.ts`, `details.ts`, `kit.ts`, `material.ts`, `gltf.ts` (nós `kit_`), `check.ts`, `tests/car-models.test.ts` |
| **P4 Garagem: oficina, test-drive e classe** | Aba **Oficina** na garagem: o item é provado no palco antes da compra. **Test-drive** de 1 volta com um carro à venda, sem prêmio. Selo de classe antes → depois ("Classe B → A!"). As linhas novas do pagamento aparecem na tela. "Revisitar copa" no hub. | M+M | `src/ui/screens/garage.ts`, `garage.css`, `payout.ts`, `src/career/strings.ts`, `src/game/career-session.ts` |

**Fecho:** estabilização e **versão 0.2** para no mínimo 8 testadores, incluindo quem não jogou a 0.1. Vai com relatório exportado e, se a decisão nº 17 sair, envio opcional com consentimento. Vai também o relay no ar, se o VPS tiver sido aprovado. As **metas com gente (seção 6) são da 0.2**, não critério para fechar a onda.

**Pronto quando:**
- O teste "sempre há algo para comprar" passa (falhava no MT).
- A soma das linhas de `raceEarnings` é igual à variação da carteira em `settleCareerRace`. O `tests/career.test.ts` continua verde.
- A correlação do ID passa.
- Folha `?carview=rear34` de cada estilo, nível 0 × nível 3.
- Os testes de tempo entre corridas e de "primeiros 15 minutos" (1ª compra) passam.
- Os saves antigos abrem.

**Duração:** 2 dias-base. **Depende de:** O. **O dono vê:** a 2ª metade da carreira com o que comprar e o que temer. Distribui a 0.2.

---

### Onda Q: "O Brasil que se reconhece"

**Objetivo.** Cada pista tem de parecer o lugar do começo ao fim, mesmo sem o marco na tela, e o lugar vira uma recompensa que se guarda.

**Antes de abrir:** contratos dos campos de save `drawingsDone`, `landmarksSeen` e `visas`, e do mapa de sub-regiões (usado também pela R3). A tabela "fonte por herói" para os 10 da leva 1:

| Herói | Fonte |
|---|---|
| Cristo | procedural refeito na L4, ou o Cristo-régua da arte (depende do jurídico) |
| MASP, Congresso | procedural |
| Hercílio Luz | procedural |
| Cataratas | procedural, com shader de água |
| Teatro Amazonas, Elevador Lacerda | procedural com peças CC0 |
| Torre Eiffel, Fuji, Ópera de Sydney | procedural ou artista, conforme o orçamento |

| Frente | Entregas | Esforço | Arquivos (dono nesta onda) |
|---|---|---|---|
| **Q1 Ícone do estado, carimbo que bate e álbum** | `src/ui/art/state-icons.ts`, gerado do traço de `track-art.ts` (degradê da região, contorno grosso, bisel), mais 7 ícones de país. O ícone vai para o **carimbo** (sigla e desenho), o **selo da legenda** (no lugar do alfinete genérico), a **miniatura** de copa e de pista e o **caderno de desenhos**. **Tela da carimbada**: o passaporte abre, o carimbo bate com tinta, som e vibração, e a região se acende. **Vistos do Mundial** (7). **Álbum dos 161 marcos**, registrado quando a legenda anuncia; o que falta aparece como silhueta com a dica da pista. | G | `src/ui/art/` (novo), `src/ui/screens/passport.ts` e css, `src/ui/passport/strings.ts`, `src/ui/screens/results.ts` (`stampNews` vira tela), `select.ts`, `common.ts` (`trackThumb`), `src/render/caption/` (selo e evento de anúncio) |
| **Q2 O minimapa que se desenha** | Nas 27 pistas com desenho, traço de cartum grosso. Na volta 1, o trecho percorrido acende atrás do carro. Ao fechar a volta, o desenho se completa com "Você desenhou o MASP!" e entra no caderno. Respeita o piso do Deck e "Reduzir efeitos". | M | `src/render/hud.ts` (MiniMap), `hud.css`, `src/game/session.ts` (evento de volta) |
| **Q3 O Brasil deixa de ser uma região só** | `scenery/ambience.ts` divide o Brasil em 8 sub-regiões, por estado e com exceção por pista:<br>– **colonial**: casario colorido, calçada de pedra, lampião;<br>– **sertão**: taipa, cerca de varas;<br>– **litoral nordestino**: barraca, jangada na areia;<br>– **Amazônia**: palafita, barco de linha;<br>– **cerrado e Pantanal**: buriti, ipê, cupinzeiro;<br>– **serra do Sul**: araucária, enxaimel;<br>– **pampa**: coxilha, figueira;<br>– **metrópole**.<br>Entram também placas verdes com nomes reais ("Ouro Preto 12 km"), o portal "Bem-vindo a…", **torcida em impostor** e o cenário de largada e chegada de cada lugar. Cada objeto reaproveita um tipo de sprite com a mesma pegada, dentro de ≤ 900 triângulos por segmento. | G | `src/render/scenery/catalog.ts`, `ambience.ts` (novo), `structures.ts`, `vegetation.ts`, `props.ts`, `scenery/textures.ts`, `src/render/road-textures.ts` |
| **Q4 A paisagem do lugar** | **Horizonte próprio nas 34 pistas de abertura de copa**: silhueta pintada ou autoral nos anéis (`horizon.ts`, da L4), por exemplo o Dedo de Deus, a Esplanada, o skyline da Paulista. **Skyline da cidade com o recorte do lugar**, no lugar das 160 caixas instanciadas aleatórias (`terrain.ts`, ~linha 468). | G | `src/render/horizon.ts`, `src/render/terrain.ts` |
| **Q5 Marcos-herói, leva 1** | Os 10 da tabela, no padrão novo: material da L1, forma inflada, luz própria à noite e ~10–12 mil triângulos perto, com LOD. **Animação**: bondinho subindo, água das Cataratas com UV rolando e névoa de partículas, roda-gigante, barcos. **Só a malha muda**: `perLap`, `place` e `side` ficam congelados, com teste. As réguas de leitura e de orçamento são reescritas antes, de propósito. | GG | `src/render/scenery/landmarks/*`, `landmarks/anim.ts` (novo), `scenery/runtime.ts` (animação), `tests/landmarks*.test.ts`, `tests/front-view.ts` |

**Pronto quando:**
- Teste visto falhar antes: toda pista do Brasil resolve para uma sub-região, e Ouro Preto, Olinda, Paraty, São Luís e Pirenópolis resolvem para "colonial".
- Todo estado e todo país têm ícone. O álbum é gravado no save, com teste de save antigo.
- Seguem verdes `landmark-sight` (≥ 2,5 s), `landmark-caption` (os 161 anunciados na volta 1), o teste de `LANDMARK_PLAZAS` e o `scenery-forma`.
- Folha das 34 copas e dos 10 heróis antes × depois. Bench do Deck na pista de cada herói, com 4 jogadores.
- O teste cego de lugar vai na R (Marco 6).

**Duração:** 2 dias-base. **Depende de:** M (kit redondo) e L4 (`horizon.ts`). **O dono vê:** os ícones (34, em 32, 64 e 256 px), a carimbada e os heróis.

---

### Onda R: "Cerimônia e som do lugar"

**Objetivo.** O lugar se apresenta antes da largada, se guarda num postal e se ouve.

**Antes de abrir:** `Renderer.renderPostcard`, a fase de abertura na sessão (fora do `stepRace`) e o campo de sobrevoo no `StartConfig` do online.

| Frente | Entregas | Esforço | Arquivos (dono nesta onda) |
|---|---|---|---|
| **R1 O postal renderizado** | `src/render/postcard/`: o marco principal com o chão do bioma, o céu do período e uma câmera de postal por marco, num RenderTarget de 512×320 com cache. São 34 lugares. A imagem vai para o passaporte, a escolha de pista, a tela de carregamento, o resultado e o álbum. Na Baixa e no Retrô, imagem gerada antes pelo harness. | G | `src/render/postcard/` (novo), `src/render/renderer.ts`, `src/ui/screens/simple.ts` (`loadingScreen`) |
| **R2 Sobrevoo de abertura** | De 4 a 6 s até o marco, com o título "Orla de Copacabana · RJ" e o rival da copa (`IntroCamera`). **Offline**: pulável e mais curto na 2ª vez na mesma copa. **Online**: duração fixa decidida pelo anfitrião no `StartConfig` e sem botão de pular, ou desligado. O teste em `net-session` exige a largada no mesmo tick. Não muda o `hashRace`. | M | `src/render/camera.ts`, `src/game/session.ts`, `src/game/online-session.ts`, `src/net/protocol.ts` |
| **R3 O som do carro e do mundo** | Motor por carro já veio na N6; aqui ele passa a amostras se a decisão do Marco 2 permitir. **Os 2 carros da IA mais próximos ficam audíveis** por assento, com ganho pela distância, pan pelo x e Doppler calculado à mão, com teto de ~6 vozes. Pneu cantando, carga da derrapagem subindo, vento proporcional à velocidade. **Ambiente por sub-região** (ondas, cigarras, chuva, sinos), usando o mapa da Q3. | G | `src/audio/engine.ts`, `src/audio/ambience.ts` (novo), `src/audio/audio.ts` |
| **R4 Música que reage e a levada de cada região** | A seção da música sai do estado da corrida: refrão na última volta, camada extra ao liderar, filtro no nitro, respiro no box, fanfarra emendada na chegada, sempre trocando no fim do compasso. `songForTrack`: samba no RJ, frevo em PE, forró e baião no sertão, axé na BA, carimbó e boi-bumbá no Norte, vanerão no Sul, moda de viola no Centro-Oeste, e uma levada por país. Se a trilha contratada já existir, esta frente é só a **lógica de mistura sobre as faixas**. Se não, a versão procedural é provisória e serve de briefing. | G | `src/audio/music.ts`, `src/audio/synth.ts` |
| **R5 Curiosidades** (fora do caminho crítico) | De 2 a 3 fatos por estado e 2 por país (~95 textos em PT e EN, até 120 caracteres), com a fonte anotada num doc interno. Entram quando o dono aprovar a lista. | P | `src/ui/places/strings.ts` (novo) |

**Fecho: teste cego de lugar.** No mínimo 5 pessoas que nunca jogaram veem 15 capturas sem HUD e sem legenda (meta: acertam em média **≥ 10 de 15 lugares**) e 8 trechos sem marco à vista (meta: **≥ 5 de 8 regiões**). O dono só desempata.

**Pronto quando:**
- Testes escritos antes: a fase de abertura não muda o `hashRace`; `songForTrack` cobre as 109 pistas; a tabela de timbres cobre os 13 estilos; o Doppler tem o sinal e a magnitude certos.
- O `playtest-online` passa com o sobrevoo ligado.
- Medidas a CPU de áudio com 4 jogadores e a memória do cache de postais no Deck.
- O orçamento de tempo entre corridas continua verde, agora com o sobrevoo.

**Duração:** 1,5 dia-base. **Depende de:** Q. **O dono vê:** a Expedição como viagem (sobrevoo, postal, carimbo), e ouve 30 s de 6 pistas.

---

### Onda S: "Competição justa" (offline; versão 0.3)

**Objetivo.** Toda corrida passa a ter alguém com nome para vencer, sem depender de servidor, com medalhas que um carro caro não compra.

**Antes de abrir:** a decisão nº 18 (direção assistida nos recordes). Contratos: `BestLap` com categoria e nível de assistência, e o formato de `src/core/data/medals.ts` **por classe**.

| Frente | Entregas | Esforço | Arquivos (dono nesta onda) |
|---|---|---|---|
| **S1 Recorde justo** | Três categorias:<br>– **Oficial**: sem assistência, por classe de carro; é a única que vale para medalha, conquista de tempo e placar;<br>– **Assistida**: com o selo ASSIST e tabela própria;<br>– **Recorde do carro**: melhor volta por pista × carro.<br>Os recordes antigos ficam marcados "anterior às categorias", e nada é apagado. | M | `src/game/save.ts`, `src/game/contracts.ts`, `src/ui/screens/info.ts` |
| **S2 Medalhas com dono, por classe** | `scripts/medal-times.ts` gera bronze, prata, ouro e Nitro **para cada classe**. Cada tempo é a volta de um piloto da IA com nome: um amador do grid, um profissional, o rival da região e "a Lenda". Se as classes não fecharem, a medalha passa a ser por carro: o fantasma do dono é recriado na hora com o carro escolhido. A tabela é travada pela `lapFingerprint`, que agora ignora constantes de IA e de modo. Contador "x/436 por classe" na escolha de pista e no passaporte. Medalha 3D que gira: "PRATA! Faltam 0,84 s para o ouro do Zé Turbo". As medalhas liberam cosméticos-troféu. | G | `scripts/medal-times.ts`, `src/core/data/medals.ts`, `src/game/medals.ts` (novo), `src/ui/screens/select.ts`, `passport.ts` |
| **S3 Rivalidade do sofá que fica** | Confronto direto por par de perfis ("Ana 12 × 9 Bia"), sequência, nêmesis e "quem tem mais ouro", no lobby e no resultado. O perfil passa a ser escolhido de uma lista. O torneio de sofá grava o **hall da fama**. **Conquistas por perfil**: hoje o P2 nunca ganha PRIMEIRA_VITORIA depois do P1, e o teste vem antes. | M | `src/game/stats.ts`, `src/game/party-session.ts`, `src/game/achievements.ts`, `src/ui/screens/lobby.ts`, `party.ts`, `results.ts` |
| **S4 Vários fantasmas e a corrida gravada** | De 4 a 8 fantasmas com nome e pintura: o seu, os donos das medalhas e o do amigo, com o alvo destacado. Modo de festa **"Bata meu tempo"**, com a tela "Passe o controle". A loja de fantasmas vai para um arquivo próprio no Electron, com cota (hoje cabem ~30 voltas para 109 pistas). **Gravação das entradas da corrida local**, no mesmo formato `InputRecord` do lockstep, com um teste de reprodução que chega ao mesmo `hashRace`. É a base do trailer (W3) e do replay futuro. | G | `src/game/ghost.ts`, `ghost-store.ts`, `ghost-session.ts`, `src/core/beat-my-time.ts` (novo), `src/game/storage.ts`, `cloudsave.ts`, `src/game/race-record.ts` (novo), `src/ui/screens/ghost-records.ts` |
| **S5 Desafio do dia e da semana, sem FOMO** | `src/core/challenge.ts`, puro, sorteia pela semente da data: pista, carro (às vezes um não comprado, como test-drive), voltas e condição. Tentativas ilimitadas. O prêmio é dinheiro com teto e um selo. **Regras com teste**: nenhuma sequência que se perde; selos contados sem denominador; o cosmético da semana volta em rodízio ou fica à venda na Oficina depois. Placar local entre os perfis do sofá. O cartão aparece na tela inicial. | M | `src/core/challenge.ts` (novo), `src/game/challenge-session.ts` (novo), `src/ui/screens/simple.ts` (cartão) |

**Fecho:** estabilização e **versão 0.3**, mais o **Steam Playtest público** se houver App ID. A partir daqui o D1/D7 passa a ser medido pela % das conquistas-marco.

**Pronto quando:**
- Testes escritos antes: corrida com a assistência Completa não grava recorde oficial; o P2 ganha PRIMEIRA_VITORIA depois do P1.
- A tabela de medalhas casa com a `lapFingerprint` das 109 pistas e fica vermelha se ficar velha. Por classe, a IA campeã chega ao ouro em 109 de 109 pistas e a amadora em nenhuma. O piloto médio, no primeiro contato, fica com a maioria em prata.
- A mesma data gera o mesmo desafio em 30 datas fixas, e a IA campeã cumpre 365 desafios sorteados.
- Custo medido de 8 fantasmas.

**Duração:** 2 dias-base. **Depende de:** O (congelado) e P2 (classes). **O dono vê:** as medalhas, o desafio do dia e o placar do sofá.

---

### Onda T: "Online com amigos, Steam e Deck"

**Objetivo.** Levar a disputa do sofá para a distância. O relay e o convite já estão no ar pela trilha S; aqui entra a camada de jogo. Também deixar o Deck pronto para a revisão da Valve.

**Antes de abrir:** a decisão nº 22 (`--no-sandbox`). Contrato: o modo e o estado da série no `StartConfig`, sobrevivendo à troca de anfitrião.

| Frente | Entregas | Esforço | Arquivos (dono nesta onda) |
|---|---|---|---|
| **T1 Série da Noite online** | A sala acumula pontos corrida após corrida (20-15-12…, com o desempate de `tournament.ts`) e mostra o placar da noite, o botão "Revanche" e a votação da próxima pista. No fim, troféu e hall da fama. Quem cai e vira IA pontua com a etiqueta IA. | M | `src/game/online-session.ts`, `src/net/protocol.ts`, `src/ui/screens/online.ts` |
| **T2 Placar de amigos por pista** | Primeiro um **spike**: leaderboard do ISteamUserStats por uma versão nova do steamworks.js ou por um binding mínimo em `desktop/` (opcional, fora do jogo); a 0.4.0 não tem leaderboard. Depois, o placar de amigos por pista, na categoria Oficial por classe, com o fantasma do amigo anexado se o binding permitir. Confiar no cliente é aceitável entre amigos. O placar global verificado fica para depois do lançamento. | M+M | `desktop/main.cjs`, `desktop/preload`, `src/game/desktop.ts`, `src/ui/screens/ghost-records.ts` |
| **T3 Conquistas de disputa** | PRIMEIRO_OURO, CEM_OUROS, NITRO_<REGIÃO>, FANTASMA_DA_LENDA, FREGUES, REVANCHE, CAMPEAO_DA_NOITE e VIRADA_ONLINE, com progresso parcial por `stats.setInt`. Nada depende de população online. **O cadastro de todas as conquistas na Steam é feito uma vez, no fecho desta onda.** | M | `src/game/achievements.ts`, `src/game/strings.ts`, `desktop/README.md` |
| **T4 Deck pronto para a revisão** | Ícones de botão Xbox, PS ou Deck em todas as dicas (hoje são texto fixo). Teclado flutuante no campo de nome (o IPC entra pelo `main.cjs` da T2, na mescla). Suspender e retomar. Roteiro de `DESEMPENHO.md` §4 e a matriz de `QA.md` no aparelho. O Remote Play Together com 4 pessoas é testado pelo dono. | M | `src/ui/remap/labels.ts`, `src/ui/strings.ts`, `docs/QA.md` (parágrafo) |
| **T5 Modos rápidos** (área núcleo de modos) | **Eliminação**: a cada quarto de volta, o último sai e vira fantasma. **Contra-relógio arcade**: tempo inicial e checkpoints que somam segundos, com o tempo da equipe no co-op. As constantes ficam no módulo de modos, fora da `lapContent`, então medalhas e recordes não mudam. Um cenário novo de golden por modo, com os 8 de hoje intactos. | M+M | `src/core/modes.ts`, `src/core/constants-modes.ts`, `src/core/sim/race.ts`, `sim/collisions.ts`, `sim/positions.ts`, `src/game/party-session.ts`, `scripts/sim-scenarios.ts` |

**Pronto quando:**
- Uma série de 3 corridas sobrevive à queda do anfitrião no meio (`net-session`).
- O roteiro manual com duas contas Steam fica registrado em `QA.md`: convite, `+connect_lobby` com o jogo fechado e o Rich Presence aparecendo.
- Os fps do Deck com 1, 2 e 4 jogadores estão na tabela da §5.
- Testes puros dos dois modos. O `lapFingerprint` não muda com as constantes dos modos.

**Duração:** 1,5 dia-base. **Depende de:** S, do App ID e do VPS. **O dono vê:** a série online e o convite.

---

### Onda U: "Arte final"

**Objetivo.** Integrar a arte externa e o acabamento que o código sozinho não dá. Se a arte atrasar, a V vem antes desta onda.

**Antes de abrir:** a entrega do artista (ou a decisão de seguir com CC0 e procedural) e o jurídico do Cristo. Contratos: o atributo de decalque e o lote do piloto.

| Frente | Entregas | Esforço | Arquivos (dono nesta onda) |
|---|---|---|---|
| **U1 Os carros da arte** | Os 14 modelos por carro, do artista ou da reserva CC0/procedural, pelo pipeline da L3: LOD, nós `kit_` (P3), camadas do CarLook, validados no `check-car`. Captura na corrida, na vitrine e no pódio. | G | `src/assets/cars/`, `src/render/cars/models.ts`, `assets.ts`, licenças em `docs/ARTE.md` |
| **U2 Marcos-herói, leva 2** | Os outros 24 heróis (um por copa), com a tabela "fonte por herói" fechada no cartão. Estimativa: ~1 h por herói procedural e ~0,5 h por herói integrado da arte. A 2ª leva de secundários (as 44 pistas com um marco só e os 12 repetidos) entra **só com marcos de horizonte ou de longe, que não abrem praça**, com o teste de `LANDMARK_PLAZAS` igual. O resto vai para a Temporada 2. | GG | `src/render/scenery/landmarks/*`, `landmarks/anim.ts`, `src/core/data/places.ts` (só dado) |
| **U3 Piloto e cartão do piloto** | Boneco de cartum procedural (cabeça grande, capacete na cor de detalhe), num lote único: +1 chamada de desenho, ligado ao `cars.ts` na mescla. Inclina na curva, vira no raspão, levanta o braço em 1º. **Cartão do piloto** (nome, estado, número, capacete, título ganho) no lobby, na sala online, no resultado e no pódio. Se o dono aprovar o mascote do folclore no Marco 2, o mesmo ponto `seat` recebe o modelo da arte. | G | `src/render/cars/driver.ts` (novo), `src/game/stats.ts` (perfil), `src/ui/screens/lobby.ts`, `src/render/textures.ts` (etiqueta) |
| **U4 Decalques, pinturas-troféu e o visual dos rivais** | Camada de decalque com um atlas de 1024²: números de 0 a 99, **placa Mercosul cartunesca com a sigla do estado** e ~24 adesivos de folclore e bandeiras estilizadas. Pinturas-troféu: 27 dos estados (pelo carimbo), 12 dos rivais (ao vencê-los) e o Ouro da Expedição. Cada rival corre com o seu visual. A IA ganha variação de pintura derivada da semente (`hash2`), igual em todo computador. | G | `src/render/cars/decals.ts` (novo), `src/render/cars.ts`, `cars/material.ts`, `src/game/cosmetics.ts`, `src/game/rivals.ts` |
| **U5 Festa e clima de cada lugar** (só visual) | São João em campina_grande, Carnaval em olinda, boi em parintins, Círio em belem. Garoa em sampa_noite com o neon refletindo no asfalto molhado, neblina na serra (nunca sobre o herói), chuva em manaus, neve no Passo Alpino. Faróis projetados no asfalto à noite. A aderência não muda. | G | `src/render/scenery/festas.ts` (novo), `src/render/effects.ts`, `palette.ts`, `road.ts`, `sky.ts` |

**Pronto quando:**
- Os carros da arte passam no `check-car`. O e2e do Electron carrega os assets por arquivo.
- O dono aprova os 34 heróis, um a um. Seguem verdes `landmark-sight`, `landmark-caption` e `front-view`.
- Testes: o piloto fica dentro da cabine; festa e clima não mudam o `hashRace`; "Reduzir efeitos" desliga as partículas novas.
- Folha das 34 copas. Teste cego visual nº 3, com meta de nota média ≥ 4,0.

**Duração:** 2,5 dias-base, mais a arte externa. **Depende de:** trilha A, Q5 e P3.

---

### Onda V: "Fim de jogo que não acaba"

**Objetivo.** Quebrar a fila única de 109 corridas e não deixar a carreira morrer no fim.

**Antes de abrir:** a decisão nº 19 (exceção ao `AI_CAR_POOL`). Contratos: `RaceConfig.aiCars?` (só eventos), `EventDef` e `careers[3]`.

| Frente | Entregas | Esforço | Arquivos (dono nesta onda) |
|---|---|---|---|
| **V1 Convites, duelo com o rival e lendários restantes** (área economia) | **Convites de patrocinador**, repetíveis com prêmio menor: copa de classe, temáticas por carroceria (Elétricos com o Sucuri E, Clássicos com o Boto Luxo, Picapes do Pantanal, Pequenos) e exibição de 1 volta. **Duelo 1×1 no fim da região**, com o rival num carro à venda: é a exceção ao `AI_CAR_POOL`, só ali e nos convites. Os lendários que faltam. | G | `src/core/data/events.ts` (novo, com saneador próprio), `src/core/career.ts`, `src/core/championship.ts`, `src/core/types.ts`, `src/game/career-session.ts` |
| **V2 Carreira Lendária, três espaços e venda** | Ao terminar a carreira, uma nova carreira+ que leva a coleção, a maestria e os carros raros, com a IA um degrau acima e o prêmio × 1,5 a 2. **3 espaços de carreira**, com migração do espaço único. **Venda de carro por 50%.** | M | `src/game/career-save.ts`, `src/game/save.ts`, `src/ui/screens/career-slots.ts` (novo) |
| **V3 Liga dos Rivais** | Divisões Bronze, Prata, Ouro, Nitro e Lenda, com os 20 pilotos e os 12 rivais (e os retratos da O4). 5 pistas por semana ISO e o top 3 sobe. **Semana não jogada não rebaixa**, com teste. Os tempos reais podem entrar na mesma tabela depois. | M | `src/core/league.ts` (novo), `src/ui/screens/league.ts` (novo) |

**Pronto quando:**
- Na sonda com os convites, o dinheiro ocioso do completista chega perto de zero.
- A IA corre com cada carro à venda sem sair do asfalto e sem ficar sem combustível. Teste: a exceção ao `AI_CAR_POOL` só vale no duelo e nos convites.
- A migração para 3 espaços preserva a carreira atual.
- Na simulação da Liga, o piloto médio chega à Ouro em 6–10 semanas.

**Duração:** 1,5 dia-base. **Depende de:** P e S. Pode vir antes da U.

---

### Onda W: "Lançamento"

| Frente | Entregas | Esforço | Arquivos |
|---|---|---|---|
| **W1 Fatia revisada** | Os primeiros 30 s e a Copa RJ conferidos contra os critérios da bíblia, nas câmeras de referência. **Critério:** teste cego final com no mínimo 8 pessoas que nunca jogaram, com ≥ 6 de 8 respondendo "atual" e nota média ≥ 4,0. | M | ajustes pontuais pelo cartão |
| **W2 Demo do Next Fest** | A build DEMO **exclui do pacote** os dados das copas fora da demo (Copa RJ, Copa EUA com Las Vegas à noite, Festa rápida, 3 ou 4 carros, garagem até a 1ª compra). Termina em "Adicione à lista de desejos". **A demo é outro App ID e o Steam Cloud é separado**, então o save da demo é importado explicitamente pelo jogo completo, a partir do mesmo `userData`, com teste. As conquistas não passam para o jogo completo. | M | configuração de build, `src/core/data/cups.ts` (filtro), `src/game/save.ts`, `desktop/` |
| **W3 Trailer e capturas por replay** | Uma ferramenta de diretor (`tools/diretor.mjs`) re-renderiza corridas gravadas (S4) a 1920×1080, em passo fixo, com câmeras de cinema. O ffmpeg da máquina junta os quadros; não é dependência do jogo. Trailers de 30 s e de 6 s, e 8 screenshots por idioma. | G | `tools/diretor.mjs` (novo), `tools/render-harness.html` |
| **W4 Loja, legal e textos** | `docs/LOJA.md`: 14 carros, 109 pistas, 34 copas e as tags Online. `PRIVACIDADE.md`: nome da Steam, relay e diário. Créditos com as licenças CC0, da fonte e do áudio. `IMPRENSA.md`. | P | docs |
| **W5 QA de lançamento** | A matriz de `docs/QA.md` (10 máquinas, 10 combinações de controle, 4 idiomas), o Steam Cloud em dois computadores, a build por SteamPipe e a submissão do Deck para a revisão da Valve, com o prazo dela no calendário do dono. | M | `docs/QA.md`, `desktop/` |

**Pronto quando:**
- O replay re-renderizado chega ao mesmo `hashRace` da corrida gravada.
- A DEMO lista só o previsto, e o save dela é importado.
- O funil da demo foi medido com pelo menos 20 jogadores do Steam Playtest.
- A tabela de QA está completa.

**Duração:** 1,5 dia-base, mais o dono (Next Fest, preço, empresa). **Depende de:** todas as anteriores e do nome e do logo.

---

## 5. Marcos

| Marco | Depois de | O que o dono joga ou vê | O que decide ali | Tempo do dono (estimado) |
|---|---|---|---|---|
| **1. "Mais uma a um botão"** | K | Do menu à largada em 4 botões; o eliminado recomeça sem lobby; a IA acompanha até o Mundial; o Cristo desenhado; o relatório da 0.1.1; os números de FPS. | Aprova a bíblia e o quadro de referências, os desenhos da rodada 1 e o contrato do asset. **Encomenda os style frames e o carro-herói de teste.** | 3–4 h (inclui ~1 h medindo no PC e no Deck e enviar a 0.1.1) |
| **2. "A fatia de última geração"** | L | Copacabana no acabamento final, o carro CC0 de ponta a ponta, o HUD novo, o resultado do teste cego. | **Orçamento de arte, música e personagem**, contorno, fonte, proporção do carro. | 2–3 h |
| **3. "Derrapar e comprar"** | M + N | Derrapagem, nitro ganho, visual novo nas 109 pistas, garagem 3D, acabamentos, menu de cartões. | **Nome e logo**, para a página "Em breve". | 2 h + 1 h de sofá |
| **4. "Congelado v1"** | O | Uma noite de sofá com amigos, a 1ª corrida de quem nunca jogou, estrelas e rival com retrato. | **Aprova o congelamento**, a duração da corrida e a ordem das pistas. | 3–4 h |
| **5. "Laço completo"** (0.2) | P | A carreira inteira com o que comprar, a melhoria visível, os carros para conquistar. | Distribui a 0.2. Telemetria com consentimento ou não. | 1–2 h |
| **6. "O Brasil reconhecível e com som"** | Q + R | Ícones, carimbo, minimapa, ambiente regional, heróis vivos, postal, sobrevoo, música regional, o teste cego de lugar. | Ícones (34), postais e curiosidades aprovados. | 3–4 h |
| **7. "Competição"** (0.3) | S | Medalhas por classe, desafio do dia, placar do sofá, fantasmas. | Playtest público. | 2 h |
| **8. "Online com amigos"** | T | Série da Noite, convite pela Steam, placar de amigos. | `--no-sandbox`. | 2 h + contas |
| **9. "Arte final"** | U | Carros e heróis da arte, piloto, decalques, a garoa de Sampa e o São João. | Os heróis, um a um. | 3–4 h |
| **10. "Conteúdo completo"** | V | Convites, duelo, Carreira Lendária, Liga. | Early Access ou 1.0, preço. | 2 h |
| **11. "Lançamento"** | W | Demo, trailer, loja, QA. | Data e Next Fest. | 6+ h |

Cada marco chega ao dono como **uma folha única**: capturas, números e decisões, com a recomendação já marcada.

## 6. Métricas: como saber se está viciando

Amostra mínima: as metas com gente valem a partir de **8 testadores numa versão**. Abaixo disso, são indicativas e registradas por testador no `docs/QA.md` §5.

| Métrica | Meta | Como medir | A partir de |
|---|---|---|---|
| Título → 1ª largada | ≤ 4 entradas e ≤ 60 s (hoje ~9 entradas) | `playlog` (funil) e contagem no `scripts/playtest.mjs` | K |
| Chegada → próxima largada | ≤ 15 s sem tocar; ≤ 2 entradas pulando | `playtest.mjs` cronometrado, em toda onda que acrescenta tela | N |
| Termina a 1ª corrida | ≥ 9 de 10 testadores | `playlog`: abandono pela pausa, tanque vazio | 0.1.1 (base) → 0.2 |
| Tempo até a 1ª recompensa | < 4 min (hoje ~11 min só de corrida) | Roteiro "primeiros 15 minutos" no `playtest.mjs` e `playlog` | O (roteiro), 0.2 (gente) |
| 1ª compra na 1ª sessão | ≥ 6 de 10 | `playlog` | 0.2 |
| Termina a Copa SP (2ª copa) classificado | ≥ 7 de 10 na 1ª ou 2ª sessão (a Copa RJ não serve: fica sem eliminação) | `playlog` | 0.2 |
| Tenta de novo depois de eliminado, na mesma sessão | ≥ 7 de 10 eliminados | `playlog` | 0.1.1 → 0.2 |
| Corridas por sessão | ≥ 4 sozinho, ≥ 6 no sofá | `playlog` (sessão = abrir até fechar) | 0.1.1 → 0.2 |
| "Jogaria de novo amanhã?" | média ≥ 4 de 5 | Questionário no jogo (K2) | 0.1.1 |
| Volta no dia seguinte e em 7 dias (D1/D7) | subindo entre versões públicas | % global das conquistas-marco na Steam; antes disso, sessões datadas do `playlog`, só indicativas | 0.3 / Steam Playtest |
| Dificuldade da carreira | Piloto médio com média entre 3º e 5º em todas as 34 copas e top 5 em ≥ 50% das corridas (hoje de 1,2º a 2,9º do DF em diante) | Sonda e `tests/career-balance.test.ts` | K (provisório), O (final) |
| Dinheiro com destino | Dinheiro parado do 4º abaixo do prêmio de uma copa; ≤ 3 corridas sem poder comprar algo; o 1º só completa o catálogo depois de ~75% da carreira; o último carro à venda perto da corrida ~80 | Sonda 2.0 e o teste da P1 | P |
| Emoção na pista | Ultrapassagens por minuto e segundos em disputa subindo, segundos sozinho caindo; nitro em 10–15% do tempo; raspões sem subir a batida traseira | `race-feel.ts` nas corridas da IA (`npm run balance -- corrida`) **e nas corridas humanas do `playlog`** | K (base), M, N |
| Estrelas e medalhas | 1ª estrela em ~80% das corridas; 3 estrelas em 15–25%; maioria em prata no 1º contato, ouro difícil, Nitro rara | Sonda de estrelas e `scripts/medal-times.ts` | O, S |
| Visual "de última geração" | Teste cego: ≥ 4 de 5 dizem "atual" e nota ≥ 3,5 (L, M); nota ≥ 4,0 (U, W) | Teste cego com quem nunca jogou | L |
| Reconhecer o lugar | Média ≥ 10 de 15 marcos sem legenda; ≥ 5 de 8 regiões sem marco à vista | Teste cego com ≥ 5 pessoas que nunca jogaram | R |
| Desempenho | Deck na Média com 1–2 jogadores ≥ 60 fps (1% low ≥ 50); 4 jogadores ≥ 60 na Baixa ou 30 travados na Média | Bench (`?bench=1`/`--bench`) no aparelho e gate de contagem por viewport no harness | K (base), L em diante |
| Saturação e contraste da imagem | só alerta, nunca meta | `tools/referencias.mjs` | K |

## 7. Decisões do dono

| # | Decisão | Precisa até | Recomendação |
|---|---|---|---|
| 1 | **Aprovar este cronograma e as horas semanais** | antes da K | Reservar 3–4 h nas semanas de marco e ~1 h nas outras. "15–30 min por semana" não basta para o que os marcos pedem (seção 5). As revisões vêm agrupadas numa folha por marco. |
| 2 | **Repositório público e licença MIT** | **antes da K** (todo commit sai público) | Tornar o repositório privado ou mudar para licença proprietária antes de qualquer arte nova e da 0.1.1. |
| 3 | **Conta Steamworks e App ID** | já | Criar já (US$ 100). Destrava a página "Em breve", as listas de desejo, o Playtest, os convites, as conquistas e o placar de amigos. |
| 4 | O que os amigos disseram da 0.1 | antes do cartão da K | Uma conversa curta, registrada no `QA.md` §5. |
| 5 | Referência visual principal, style frames e carro-herói de teste | Marco 1 | Mario Kart 8 Deluxe como referência principal. Encomendar 3–4 style frames a um ilustrador e 1 carro-herói de teste mais o Cristo-régua pelo contrato do asset. É gasto pequeno e dá o caminho ao artista. |
| 6 | **Orçamento de arte, música e personagem** | Marco 2 (com a fatia e o teste cego em mãos) | Artista para os 14 carros, ~10 marcos-herói e os retratos 2D dos 32 pilotos. Música regional e efeitos licenciados ou contratados. Mascote do folclore ao volante só se couber; senão, capacete procedural (U3). Só com código, o jogo para em "low-poly polido de alto nível" (M, Q). |
| 7 | Proporção do carro: mesma pegada × curto e gordo | Marco 2 (antes da M) | Decidir pela folha da L3. Se for o curto, entra na M1, a única janela antes do congelamento. |
| 8 | Contorno e fonte | Marco 2 | Sem traço preto (as referências não usam), decidido pela folha A/B/C. Fonte OFL arredondada. |
| 9 | **Botão de derrapar e mapa padrão** | antes da M | Controle: derrapar no RB, nitro no LB (e no Y), câmbio manual no D-pad ↑/↓ (o automático é o padrão), olhar para trás no R3. Teclado 1: derrapar no Shift direito; teclado 2: no Shift esquerdo. Hoje o RB é o nitro e o LB/Y é o câmbio; os mapas salvos migram com teste. Freio + volante deixaria a freada ambígua. |
| 10 | Primeiro contato, duração da corrida e ordem das pistas | antes da O | 1ª corrida com 2 voltas e Copa RJ sem eliminação; daí em diante, largar em último e corte no top 5 (identidade Top Gear). 2 voltas nas 7 primeiras copas, decidido com o termômetro. Great Ocean e Amalfi para o fim. |
| 11 | **Impulsores, saltos e itens** | antes da M | Faixas de impulso e rampas com turbo de manobra em 1 pista por copa, antes do congelamento (O1). Atalhos de terra na Temporada 2. **Itens e armas: não** (identidade Top Gear, arcade honesto). |
| 12 | **Unificar Campeonato e Carreira na Expedição** | antes da N | Sim. A copa avulsa sem dinheiro vai para "Jogar livre". |
| 13 | Jurídico do Cristo Redentor e de outros monumentos com dono da imagem | antes da O (a 1ª corrida é Copacabana) | Consultar, com a variante sem o Cristo pronta para a 1ª corrida, o carimbo e a loja. |
| 14 | **Nome definitivo e logo** | antes do Marco 3 | Decidir para abrir a página "Em breve" e a busca no INPI. Logo provisório até lá. |
| 15 | Página "Em breve", devlog e Discord | logo depois do Marco 3 | Sim, com as capturas da cara nova. Plano para o vão de janeiro a junho: Playtest público, demo, festivais. |
| 16 | VPS do relay (R$ 30–100/mês) | quando quiser; o ideal é antes da 0.2 | Sim. Plano B: Remote Play Together. |
| 17 | Telemetria enviada | antes da 0.2 | Na 0.1.1, só o relatório exportado. A partir da 0.2, envio opcional com consentimento, se o VPS e a revisão do `PRIVACIDADE.md` saírem. Se não saírem, metas qualitativas com amostra declarada. |
| 18 | **Direção assistida nos recordes** | antes da S | Tabela separada: o oficial é sem assistência e por classe; a assistida tem selo e placar próprios. |
| 19 | Exceção ao `AI_CAR_POOL` | antes da V | Sim, só nos duelos de região e nos convites. |
| 20 | Estrelas e preços | antes da P | Estrela paga pouco (10–15% do prêmio, uma vez só) e libera itens-troféu. Catálogo de ~$ 250–350 mil. Carros à venda espalhados até a corrida ~80. |
| 21 | Nomes Falcão GT e Tornado (lembram modelos reais) | antes da M4 | Renomear junto com o redesenho. |
| 22 | `--no-sandbox` no Deck | antes da T | Decidir com o teste no aparelho (`desktop/README.md`). |
| 23 | **(aberta)** "Os carros não estão de acordo com o desenho": é a imagem da escolha diferente do carro na pista, ou o carro não parece o modelo real/ilustração que o dono tem em mente? | antes da L3 | Se for a primeira, a N2 resolve mostrando o próprio modelo 3D em toda tela. Se for a segunda, o dono manda as referências e elas entram no contrato do asset (K0). |
| 24 | **Adotada ("pode fazer tudo", 09/10)**: Tom do balão de provocação | antes da N | Só frases de brincadeira ("Come poeira!", "Tchau, tartaruga!", "Segura essa!"), sem palavrão: o público é família, palavrão sobe a classificação etária na Steam e o online exige moderação. |
| 25 | **Adotada (09/10)**: só a mudança visual entra (M6); chuva com aderência fica para a Temporada 2. Clima que muda durante a corrida | antes da O (física congela) | Visual (período andando, M6) já entra. Chuva com menos aderência só se entrar antes do congelamento, em 1 pista por copa; senão, Temporada 2. |
| 26 | **Parcial (09/10)**: placas em texto já no jogo; o dono ainda manda os logos e confirma o uso das marcas. Logos do Atomatiza e da Galeria Atomo | durante a K | Mandar SVG ou PNG; até lá, placa em texto nas cores das marcas. Confirmar que as marcas podem aparecer no jogo. |

## 8. Fora do escopo por enquanto

| Item | Por que fica para depois | Quando reabrir |
|---|---|---|
| Online ranqueado com matchmaking e nota de habilidade | Com a população pequena de um indie brasileiro premium, a fila vazia afasta quem chega. | Depois do lançamento, com o número real de jogadores simultâneos na Steam. |
| Placar global verificado, temporadas e fantasma v3 por entradas | Exige um serviço de conferência no servidor, a Web API de publicador e a revisão da LGPD. O placar de amigos (T2) e a Liga (V3) seguram a disputa até lá. | Depois do lançamento, com a física congelada. |
| Replay dentro do jogo | A gravação das entradas já existe (S4), mas a tela de replay é polimento. O trailer usa a ferramenta própria (W3). | Junto do placar verificado. |
| Peças de Corrida nível 4 e 5 com ramos | Mexem no balanço inteiro (`clampLevel`, `upgradeCap`, IA, combustível). A rampa, a Oficina, as classes e os convites resolvem a 2ª metade da carreira. | Se a sonda mostrar falta de gasto com efeito na pista. |
| Ajuste fino de marchas e asa | Complexo para criança no sofá; exige campo novo no protocolo; um ajuste acabaria dominando. | Depois do lançamento, escondido em "Avançado". |
| Atalhos de terra e salto com `y` real no núcleo | Mudam conteúdo e física depois do congelamento. O impulso e as rampas com `airTicks` entram na O1, se aprovados. | Temporada 2. |
| Marcos dirigíveis (passar pela Hercílio Luz, sob o MASP) | Arquitetural, com alto custo de modelagem; quebra o contrato do marco a 26 m da pista. | Piloto em 2 pistas na Temporada 2, se o dono quiser. |
| 2ª leva de marcos secundários que abrem praça | Muda `LANDMARK_PLAZAS` e, com isso, a colisão e as medalhas. | Temporada 2 (os de horizonte entram na U2). |
| Caça ao Saci e Resistência do dia à noite | Modos grandes, com IA de perseguição nova. | Depois dos modos rápidos (T5), se o diário mostrar uso da Festa. |
| Clima com aderência (chuva na física) | Mexe na física, na IA e nos recordes por condição. O visual entra na U5. | Temporada 2. |
| Mascote do folclore animado ao volante | Exige artista, esqueleto e animação, e muda a direção do jogo. | Com o orçamento aprovado no Marco 2 (decisão nº 6). |
| Mapa ilustrado do Brasil como tela da Expedição | Bom gancho, mas compete com o passaporte e depende dos ícones (Q1). | Depois da Q, se o diário mostrar que o passaporte motiva. |
| Modo foto, estante de troféus, apresentação do grid | Vêm depois do palco e do postal. A apresentação do grid disputaria tempo com o sobrevoo, contra o orçamento de tempo entre corridas. | Depois do lançamento, ou na W se sobrar fôlego. |
| "Tentar só esta corrida" | Muda a regra Top Gear de recomeçar a copa e abre a porta para farmar prêmio. | Só se o diário mostrar abandono na eliminação mesmo com "Recomeçar a copa". |
| Contorno preto como padrão | As referências não usam. | Só se a folha A/B/C da L2 convencer o dono. |
| Novidades no modo Retrô | Ele continua funcionando, declarado só-3D (salvo pintura, chama e pose de derrapagem). | Não está previsto. |
| Venda de peças e economia online | Fácil de trapacear sem um servidor que mande no resultado. | Não está previsto. |
| Itens e armas | Fogem da identidade Top Gear e do "arcade honesto" (decisão nº 11). | Não está previsto. |
| Localização ES/DE/FR/JA, Workshop e editor de pistas, DLC de países, planetas, modo espectador | Pós-lançamento, como já está no `ROADMAP.md`. | Depois do lançamento. |
