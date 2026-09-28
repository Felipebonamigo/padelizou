# Tutorial de 90 segundos (passo 1.9)

Uma volta guiada numa pista própria, sem adversários, que ensina o essencial: acelerar, fazer
curva, frear antes da curva forte, nitro, box e — com 2 ou mais jogadores — empurrar o
companheiro parado. Só local (não existe no online).

## Onde fica
| Arquivo | O quê |
|---|---|
| `src/game/tutorial-track.ts` | A pista (`tutorial`, "Autódromo-Escola") e os trechos de cada lição, calculados das operações do DSL |
| `src/core/track/index.ts` | `registerExtraTrack`: pista fora de `TRACKS` que só o `getTrack` acha (seleção, recordes, copas e os testes que varrem `TRACKS` não a veem) |
| `src/game/tutorial.ts` | A máquina de passos, pura: `updateTutorial(tut, state)` depois de cada tick → eventos; `tutorialView` → o que o painel mostra; `tutorialInput` → o comando do carro que o passo 6 para |
| `src/game/tutorial-session.ts` | A corrida guiada na sessão, como um `RaceDriver` (o mesmo gancho do online); textos do painel com as teclas de cada jogador; fim, pular e flag no save |
| `src/ui/screens/tutorial.ts` + `.css` | "Como jogar" (entrada dos jogadores), tela final, cartão "Primeira vez?" do menu principal e o painel por cima do HUD |
| `src/tutorial/strings.ts` | Textos PT/EN |
| `tests/tutorial.test.ts` | Pista, máquina, painel, sessão (piloto roteirizado faz o tutorial inteiro), save |
| `scripts/playtest-tutorial.mjs` | Playtest no Chromium pelo teclado, com uma captura por passo |

Ganchos nos arquivos compartilhados: `session.ts` (cria a sessão do tutorial e trata
`startTutorial`/`tutorialFirstCup`), `contracts.ts` (telas `tutorial`/`tutorialDone`, os dois eventos,
`TutorialDoneData`, `SaveData.tutorialDone`), `save.ts` (saneia a flag), `menus.ts` (as duas telas),
`simple.ts` (item "Como jogar" e o cartão de oferta), `common.ts` (`ScreenData` aceita `TutorialDoneData`).

## A pista
640 segmentos (~27 s por volta a toda), na ordem das lições; o grid fica na reta curta depois do box:

| Trecho | Segmentos | Operação | Lição |
|---|---|---|---|
| `accel` | 0–119 | reta 120 | 1: acelerar até 150 km/h (e onde o passo 6 para o carro) |
| `curve` | 120–219 | curva 100, força 4 | 2: curva média no asfalto (dá sem frear: o Falcão segura ~80% da máxima) |
| `brakeZone` | 220–309 | reta 90 | tempo de ler o passo 3 e frear |
| `strong` | 310–399 | curva 90, força −6 | 3: frear antes (o Falcão segura ~53%) |
| `nitro` | 400–559 | reta 160 | 4: nitro |
| `pit` | 560–609 | box 50 | 5: abastecer |
| `grid` | 610–639 | reta 30 | grid de até 4 carros |

Corrida de 6 voltas (`TUTORIAL_LAPS`) só para dar margem: quando alguém **começa** a última volta
sem a equipe ter terminado, o tutorial acaba ("sem tempo") antes de qualquer um cruzar a chegada —
o `race_over` nunca acontece e nada vai para recordes, estatísticas ou conquistas.

## Os passos
Cada jogador anda pelos passos 1–5 no próprio ritmo; os de trecho só contam uma **passagem inteira**
(entrou pelo começo, saiu pelo fim). Errou → aviso na hora e o passo espera a próxima volta.

1. **Acelere** — `speed ≥ 150 km/h`.
2. **Curva média** — atravessar `curve` sem `|x| > 1,05`. Saiu do asfalto → `offroad`.
3. **Curva forte** — entrar em `strong` a no máximo `holdableSpeedFraction(carro, 6) × máxima × 1,15`
   (senão `fast`, "rápido demais", já na entrada) e atravessá-la no asfalto.
4. **Nitro** — o evento `nitro` do carro. Sem carga nenhuma (gastou antes), avisa e segue.
5. **Box** — o tanque começa em 22% (`START_FUEL`: o HUD já pisca e o aviso de combustível do jogo
   aparece); conta estar na faixa do box com ≥ 60%. Passou pelo trecho sem entrar → `pitMissed`;
   entrou mas atravessou embalado (o limitador do box só freia aos poucos) → `pitShort`. O texto
   pede para frear antes.
6. **Empurrão** (só com 2+) — quando todos passaram do box, o líder é escolhido; ao chegar na reta
   `accel` (no asfalto), o carro dele fica no freio (`tutorialInput`) até o evento `tow` nele.
   Quem passa direto pelo carro parado sem empurrar recebe um aviso (`towMissed`). O empurrão exige
   o parado abaixo de 20% da máxima e o companheiro acima de 40% (`sim/coop.ts`), então o texto pede
   para ficar atrás até ele parar e passar colado acelerando.

Com 1 jogador são 5 passos. No fim: "TUTORIAL CONCLUÍDO!" por 2,5 s e a tela final.

Configuração da corrida: só humanos (sem IA), co-op (todos no time 0), todas as assistências
ligadas, câmbio automático (mesmo com o manual nas opções), carros de fábrica.

## Painel
Um cartão por viewport, embaixo e no meio (entre combustível/nitro e o velocímetro, sem cobrir o
carro), na mesma divisão de tela do renderizador (`viewportRects`/`uiScale`): "PASSO n/total", título,
instrução, barra de progresso (velocidade no passo 1, tanque no 5), as teclas do dispositivo daquele
jogador (teclado 1, teclado 2 ou controle, com o remapeamento de `Settings.controls` e os nomes de
`src/ui/remap/labels.ts` — Xbox ou PlayStation pelo nome do controle; o controle lembra do analógico)
e "Esc/Start: pular tutorial". O aviso de repetir flutua acima do cartão até o passo ser cumprido.
O DOM só nasce no primeiro uso (os testes de sessão em Node criam a sessão sem `document`).

## Entrada, saída e save
- **Menu principal → Como jogar** (depois de Controles). A tela funciona como um lobby enxuto:
  Enter/Espaço (teclado 1), F (teclado 2) ou A (controle) entra; Esc/B sai do assento; COMEÇAR
  (ou Start) com pelo menos 1 jogador.
- **Primeira abertura**: enquanto `save.tutorialDone` é falso **e** `racesRun` é 0, o menu principal
  mostra o cartão "NOVO · Primeira vez?" (clicável) apontando para "Como jogar". Não é um diálogo que
  bloqueia: os playtests (e quem já sabe jogar) passam direto do título para o Campeonato.
- **Pular**: Esc/Start a qualquer momento (`RaceDriver.pauseKey`) encerra na hora.
- **Tela final**: "Parabéns!" (concluído) ou "Regras de ouro" (pulado/sem tempo), com as regras de
  ouro (a do empurrão só com 2+) e os botões "Correr a Copa Brasil" (primeira copa, mesmos jogadores
  e assentos) e "Menu principal".
- `save.tutorialDone = true` ao concluir, pular ou acabar o tempo.

## Verificação
`tests/tutorial.test.ts` (31 testes) e `node scripts/playtest-tutorial.mjs http://localhost:<porta>/ scratch/tut`
com `npx vite preview` no ar: 2 jogadores pelo teclado fazem o tutorial inteiro (capturas de cada
passo, do carro parado e da tela final), "Correr a Copa Brasil", depois 1 jogador pula com Esc.
