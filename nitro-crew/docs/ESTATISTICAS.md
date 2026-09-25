# Estatísticas e conquistas (passo 3.6)

Estatísticas por jogador, 8 conquistas novas (20 no total) e a tela de recordes com três abas.
Nada disso toca o núcleo: tudo sai dos `SimEvent`s e do estado que `stepRace` já produz.

## Onde está cada coisa

| Arquivo | O quê |
|---|---|
| `src/game/stats.ts` | Tipos (`PlayerStats`, `StatsData`), contribuição de uma corrida, acumulação no save, saneamento, formatação (km, h:mm:ss) |
| `src/game/achievements.ts` | Telemetria por tick (`observeTick`), regras das 20 conquistas (`unlockAchievements`), mensagens de HUD |
| `src/game/desktop.ts` | `ACHIEVEMENTS`: ids da Steam com nome PT/EN |
| `src/stats/strings.ts` | Descrições das conquistas (PT/EN) e textos da tela de recordes. As `COPA_<ID>` não têm string própria: `achievementDescription` monta "Concluir a {copa}" com o nome de `core.cup.<id>`, e copa nova em `data/cups.ts` já nasce com descrição |
| `src/game/raceEnd.ts` | Os dois ganchos da sessão, testáveis em Node: `stepObserved` (`stepRace` + `observeTick` antes de tratar os eventos) e `settleRace` (fecha as contas no `race_over`) |
| `src/game/session.ts` | Só liga os ganchos e os efeitos: Steam, mensagem no HUD, gravar o save |
| `src/ui/screens/info.ts` + `records.css` | Tela de recordes: Pistas · Jogadores · Conquistas |
| `src/ui/screens/results.ts` | Quadro "Conquistas desbloqueadas" no resultado |
| `tests/stats.test.ts` | Corrida simulada, contatos e batidas, cada conquista nova (dispara e não dispara), save corrompido, limite de perfis, textos com número |
| `tests/raceEnd.test.ts` | Corrida dirigida como a sessão: ordem dos ganchos, mensagem `good`, idempotência, exceção no meio do fechamento |
| `scripts/playtest.mjs` | No fluxo real: a corrida soma uma vez no save (perfil, total, conquistas) e o resultado com 20 carros e 14 conquistas não corta cartão nem esconde a tabela em 720p |
| `scripts/playtest-records.mjs` | Tela de recordes com 32 pistas em 720p, pelo controle e pelo teclado (exige `npm run dev`) |

## Estatísticas

Guardadas em `SaveData.stats`: `totals` (soma de todos) e `players` (um perfil por nome do lobby).

- **Perfil = nome do lobby**, sem diferenciar maiúsculas nem espaços ("Ana", " ana " e "ANA" são a mesma
  pessoa; fica a grafia mais recente). Dois assentos com o mesmo nome somam no mesmo perfil.
- **Limite de 32 perfis** (`MAX_PROFILES`): ficam os nomes que correram mais recentemente; o total não perde
  nada quando um nome sai da lista.
- Contadores: corridas, vitórias, pódios, vitórias em co-op, voltas, distância, tempo de corrida, nitros,
  empurrões dados e recebidos, colisões, batidas no cenário, paradas no box; mais a melhor posição por pista.
- **Distância** pela escala do velocímetro: 6000 u/s = 300 km/h, então 1 u = 1/72 m (`METERS_PER_UNIT`).
  Conta do grid até a linha de chegada — o piloto automático depois dela não é do jogador.
- **Tempo de corrida**: o tempo final de quem terminou; para quem não terminou, do "JÁ" até o fim da corrida.
- **Vitória, pódio e melhor posição** não contam no contra-relógio (sozinho na pista, a posição é sempre 1).
  Pelo mesmo motivo `recordRaceResults` deixou de contar vitória no contra-relógio (`racesWon`).
- **Vitória em co-op**: vencer uma corrida em que 2+ humanos estão na mesma equipe (`isCoop`).
- **Colisões**: contatos carro-carro vistos pelo estado a cada tick (`observeTick`): as caixas do núcleo
  (`CAR_LENGTH` × `2·CAR_HALF_WIDTH`) se sobrepõem, batida por trás ou raspão lado a lado, e os dois lados contam.
  Não dá para contar só o evento `collision`: o núcleo só o emite com os dois carros fora do cooldown, mas
  aplica a batida mesmo assim (bater numa IA que acabou de bater em outra não gerava evento). Um contato que
  continua, ou outro com o mesmo carro em até `COLLISION_COOLDOWN_TICKS` (20), é a mesma colisão. A caixa
  tem folga lateral de 0,06 (`CONTACT_LATERAL_MARGIN`) porque o núcleo separa os dois carros no mesmo tick;
  o custo aceito é que passar a menos de 0,06 de outro carro também conta.
- Só corrida que chega ao fim conta: sair pelo menu de pausa ou reiniciar no meio descarta a corrida.
- **Save anterior ao passo 3.6 começa as estatísticas do zero** (de propósito): `racesRun`/`racesWon` não viram
  `totals`, porque `racesRun` conta corridas e não jogadores, e o `racesWon` antigo contava o contra-relógio
  como vitória — semear `totals.wins` com ele daria `DEZ_VITORIAS` sem dez vitórias. Nenhum save de jogador
  existia ainda quando o passo entrou. Por isso também o cabeçalho da tela (uma por corrida) e o total de
  "Todos os jogadores" (um por jogador) contam diferente; a nota do total explica.
- Save adulterado: contador negativo, texto, NaN ou infinito vira 0; posição fora de 1..`MAX_CARS` (20) some; perfil sem
  nome, repetido ou além do limite some; nome passa pelo mesmo limite do lobby (12). Nunca lança.

## As 8 conquistas novas

| ID | Regra exata |
|---|---|
| `PODIO_DE_EQUIPE` | Três humanos no 1º, 2º e 3º numa corrida com IA. Vai para os três. |
| `DO_ULTIMO_AO_PRIMEIRO` | Vencer tendo fechado a primeira volta em último (posição = número de carros). Corrida de 1 volta não serve. |
| `SEM_ARRANHAO` | Terminar uma corrida com IA sem nenhum contato carro-carro (ver Colisões) nem batida no cenário. |
| `MARATONA` | Distância somada de todos os jogadores ≥ 1.000 km (`MARATHON_METERS`). Vai para todos da corrida. |
| `MESTRE_DO_VACUO` | 60 s de vácuo numa mesma corrida (`DRAFT_MASTER_TICKS`), antes da chegada. |
| `NITRO_NA_BANDEIRA` | Cruzar a chegada com o nitro ligado. Vale no contra-relógio. |
| `DEZ_VITORIAS` | Vitórias somadas de todos os jogadores ≥ 10 (contra-relógio não conta). Vai para quem venceu agora. |
| `GIRO_COMPLETO` | Ter resultado fora do contra-relógio (com ou sem IA) em todas as pistas de `TRACKS` — o número sai da lista, nunca é fixo. |

**Vácuo sem mexer no núcleo.** O núcleo não emite evento de vácuo; `observeTick` chama a mesma função pura que a
física usa (`computeModifiers` em `src/core/sim/coop.ts`) sobre o estado depois do passo. Só lê; o estado e o
hash da corrida ficam iguais.

As cumulativas (`MARATONA`, `DEZ_VITORIAS`, `GIRO_COMPLETO`) leem o save já com a corrida somada — a sessão grava as
estatísticas antes de avaliar as conquistas.

## Onde a conquista aparece

1. No tick do `race_over`, `settleRace` fecha recordes, copa, estatísticas e conquistas e grava o save.
   O resultado é marcado antes de qualquer efeito e o save é gravado num `finally`: se um passo lançar, a
   sessão (que chama de novo no quadro seguinte) recebe o que já foi fechado, nada soma duas vezes e a tela
   de resultado aparece.
2. Cada assento que ganhou algo recebe uma mensagem `good` no próprio HUD ("CONQUISTA: Nome"; várias viram
   uma linha só, com o excedente contado, porque o centro do HUD tem 3 vagas).
3. A tela de resultado mostra o quadro "Conquistas desbloqueadas", com a cor de quem ganhou cada uma.
4. Steam: `getDesktop()?.achievement(id)`, como antes.

## Tela de recordes

- **◀ ▶ trocam de aba** (as abas são listas verticais; esquerda/direita não têm outro uso ali).
- **Pistas**: cada pista com recorde é uma linha focável; ↑↓ (controle ou teclado) movem o foco e a lista rola
  até a linha. `scripts/playtest-records.mjs` confere com recorde em 32 pistas em 1280×720 (completa `TRACKS`
  com pistas fictícias se o jogo tiver menos; roda contra `npm run dev`, que serve os módulos-fonte): cada linha
  fica inteira à vista, só a lista rola, o Voltar fica na tela, e o controle alcança a última pista, a última
  linha da grade de melhor posição e a última conquista.
- **Jogadores**: a primeira linha é o total; o detalhe acompanha o foco. A lista e o detalhe rolam cada um por si.
  A melhor posição por pista é uma grade de **todas** as pistas do jogo (as não corridas com "—"), em linhas de
  3 (`BEST_COLS`); depois do último jogador o foco desce por essas linhas e então chega ao Voltar. Com 32 pistas
  são 11 linhas, que não cabem em 720p: sem uma linha focável por vez, o controle não alcançaria as últimas.
  Como a grade lista todas as pistas, o número de linhas não depende do jogador. Ao voltar à lista, o detalhe
  volta ao topo.
- **Conquistas**: barra de progresso, desbloqueadas primeiro, bloqueadas depois, cada uma com a descrição.
