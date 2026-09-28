# Rivais com personalidade (passo 3.4)

Cada piloto da IA tem uma **personalidade fixa** que muda como ele pilota, e cada copa tem um **rival
principal** que aparece nas telas, provoca no resultado e vale uma conquista. Tudo determinístico: a
personalidade vai no cérebro da IA (estado JSON) e funciona igual no online.

## Personalidades

| Personalidade | Como pilota | Pilotos |
|---|---|---|
| **Limpo** (`clean`) | Traçado mais por dentro, passa largo, não cola em ninguém (iguala 98% da velocidade de quem está na frente), troca pouco de faixa, nitro só com 40 segmentos de reta à frente | Kenji Sato, Iris Lund, Pierre Duval, Nina Costa, Ana Volpi |
| **Agressivo** (`aggressive`) | Freia mais tarde (supõe 12% a mais de freio), entra 1,5% mais rápido, cola na traseira (104%) e só desvia a 3 segmentos, passa raspando, troca de faixa toda hora, nitro mais cedo e mais vezes, erra a frenagem em 5% dos trechos de curva | Zé Turbo, Sasha Kova, Caio Brasa, Rafa Moura, Marco Rossi |
| **Bloqueador** (`blocker`) | Pilota como o neutro; com um **humano** logo atrás, leva a faixa para a frente dele | Tom Blake, Hugo Klein, Otto Weiss, Duda Ferraz, Lars Berg |
| **Errático** (`erratic`) | Em 12% dos trechos de curva (por volta) erra o ponto de frenagem: entra rápido, abre para fora e passa pela grama; troca de faixa sem motivo | Bianca Rey, Tico Ramos, Léo Prado, Dani Souza, Yuki Mori |

Cérebro **sem** personalidade (o piloto automático do humano que terminou, a IA que assume o assento de
quem caiu no online, o piloto-proxy da calibragem da carreira, estado antigo) usa `NEUTRAL_TUNING`, que
reproduz exatamente a IA de antes — os números antigos e multiplicadores 1.

### Bloqueio (regras de justiça)
- Só humanos: outro carro da IA atrás nunca é bloqueado.
- Janela: humano até 3 segmentos atrás e até 0,9 de distância lateral.
- A faixa anda no máximo 0,012 por tick (0,72/s) — dá para enganar e passar.
- Nunca passa de |x| 0,7: fica no asfalto e não empurra ninguém para a grama.
- **Respeita o desvio**: com o humano a menos de 1,5 carro (bico do lado), não fecha mais; e desviar de
  quem está na frente dele vem antes de bloquear.
- **Limite de tempo**: 4 s fechando a porta numa investida; depois descansa 8 s sem bloquear ninguém. O
  relógio da investida só volta devagar (1 tick por tick sem ninguém para fechar).

### Erro de frenagem
O trecho de curva é a sequência de segmentos com curva ≥ 1,5 (a mesma régua da frenagem da IA). A decisão
de errar sai de um hash de (semente, carro, volta, primeiro segmento do trecho) — não usa `state.rng`,
então não muda o acaso dos outros pilotos. Errando, o piloto acha que a curva segura 20% a mais, freia
2× mais tarde e, nos primeiros 20 segmentos do trecho, abre até x = 1,15 (a grama começa em 1,05).

Números em `src/core/constants.ts` (`BLOCK_*`, `MISTAKE_*`, `RIVAL_SKILL_BONUS`, `AI_BRAKE_CURVE`) e a
tabela de cada personalidade em `src/core/data/drivers.ts` (`PERSONALITY_TUNING`).

### Medido (tests/rivals.test.ts, Passo Alpino, 11 carros da IA todos com a mesma personalidade, 150 s, semente 5)

| | nitros nos 1ºs 45 s | nitros com curva à frente | batidas | trocas de faixa | grama | ticks à frente do humano | maior bloqueio | volta média |
|---|---|---|---|---|---|---|---|---|
| neutro | 25 | 13 | 71 | 582 | 0,00% | 2298 | — | 67,97 s |
| limpo | 24 | **0** | **43** | **435** | 0,00% | 1098 | — | 67,60 s |
| agressivo | **29** | 11 | **190** | **870** | 0,52% | 2902 | — | 68,69 s |
| bloqueador | 23 | 10 | 133 | 622 | 0,00% | **4399** | 239 ticks (< 240) | 68,39 s |
| errático | 27 | 12 | 70 | 666 | **1,27%** | 1082 | — | 68,53 s |

Justo: nenhuma personalidade fica a mais de 1,1% do neutro na volta média.

### Balanceamento (`npm run balance`, 150 s, profissional)
Média geral das voltas da IA nas 32 pistas, antes → depois: semente 11 (a padrão) 86,23 → 86,61 s
(+0,4%, maior diferença por pista 2,6%); semente 12 85,79 → 86,57 s (+0,9%); semente 13 85,56 →
87,14 s (+1,9%). Grama da IA (semente 11): 0,0–0,1% → 0,1–0,8% por pista; batidas carro-carro sobem ~20% (o agressivo cola). A calibragem da carreira
(`tests/career-balance.test.ts`) e as corridas inteiras de IA e combustível por pista seguem verdes.

## Rival principal da copa

| Copa | Rival | Personalidade |
|---|---|---|
| Brasil | Zé Turbo | agressivo |
| Estados Unidos | Tom Blake | bloqueador |
| Japão | Kenji Sato | limpo |
| Europa | Hugo Klein | bloqueador |
| África do Sul | Bianca Rey | errático |
| Austrália | Sasha Kova | agressivo |
| Escandinávia | Iris Lund | limpo |
| Mediterrâneo | Marco Rossi | agressivo |

- **Fixo por copa** (`CUP_RIVALS` em `data/drivers.ts`; `cupRival()` em `src/game/rivals.ts`), e não
  sorteado do elenco da corrida: o elenco nasce com a semente da copa na primeira corrida, mas a tela de
  copas precisa mostrar o rival antes disso. Copa nova sem entrada na tabela ganha um piloto pelo hash do id.
- **No grid**: a copa normal e a carreira põem `config.rival`; `createRace` garante que ele está no elenco
  (`rosterOffsetWith`: se o deslocamento sorteado não o incluía, ele entra na última vaga da IA — larga logo à
  frente dos humanos), sem mudar os sorteios de ninguém, e soma `RIVAL_SKILL_BONUS` (0,02) à habilidade.
- **Carreira**: a mesma escolha — a corrida da carreira leva o rival da copa atual.
- **Falas** (`src/rivals/strings.ts`, PT e EN): apresentação e três do duelo, próprias de cada rival; um
  piloto sem falas próprias usa as da personalidade.
  - provocação: ele terminou à frente do melhor humano;
  - revanche: ele terminou à frente depois de ter perdido o duelo da corrida anterior;
  - respeito: algum humano terminou à frente dele.
- **Onde aparece**: detalhe da copa (rival, personalidade e apresentação); resultado da corrida da copa ou
  da carreira ("Rival: <nome> — <posição>", a fala, linha marcada na tabela); classificação ("… — <posição>
  na copa", a fala conforme quem está à frente nos pontos, linha marcada e mostrada mesmo fora do top 10);
  garagem da carreira (rival da copa atual e a apresentação ou a fala do último duelo); HUD: ponto vermelho
  maior no minimapa e etiqueta RIVAL na classificação da célula livre (3 jogadores). Corrida rápida,
  contra-relógio e online não têm rival (as personalidades valem em todos).

## Conquista `RIVAL_DERROTADO`
Concluir uma copa (normal ou da carreira) com algum humano à frente do rival em **todas** as corridas.
Avaliada no fechamento da corrida que conclui a copa (`unlockAchievements`, com a copa já somada; na
carreira, `SettleOptions.careerChamp` entrega a copa depois de `careerFinished`). Vai para todos os humanos.

## Onde está cada coisa
- `src/core/data/drivers.ts` — personalidade de cada piloto, `PERSONALITY_TUNING`, `CUP_RIVALS`, `rosterOffsetWith`.
- `src/core/sim/personality.ts` — tabela do cérebro, trecho de curva e erro de frenagem, bloqueio.
- `src/core/sim/ai.ts` — ganchos: frenagem, faixa, desvio, bloqueio, troca de faixa, nitro; `createBrain` com perfil.
- `src/core/sim/race.ts` — rival no grid e personalidade de cada piloto.
- `src/game/rivals.ts` — rival da copa, falas, duelos, resumo do resultado e da classificação, regra da conquista.
- `src/ui/screens/rival.ts` + `rival.css` — cartões e blocos; `src/render/hud-rival.css` — HUD.
- `tests/rivals.test.ts` — métricas por personalidade, bloqueio, determinismo, rival, falas, duelo e conquista.
- Roteiro de tela: `scratch/rivais-ui.mjs` (não versionado; o `scratch/` fica fora do git).
