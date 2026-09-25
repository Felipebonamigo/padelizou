# Estatísticas e conquistas (passo 3.6)

Estatísticas por jogador, 8 conquistas novas (20 no total) e a tela de recordes com três abas.
Nada disso toca o núcleo: tudo sai dos `SimEvent`s e do estado que `stepRace` já produz.

## Onde está cada coisa

| Arquivo | O quê |
|---|---|
| `src/game/stats.ts` | Tipos (`PlayerStats`, `StatsData`), contribuição de uma corrida, acumulação no save, saneamento, formatação (km, h:mm:ss) |
| `src/game/achievements.ts` | Telemetria por tick (`observeTick`), regras das 20 conquistas (`unlockAchievements`), mensagens de HUD |
| `src/game/desktop.ts` | `ACHIEVEMENTS`: ids da Steam com nome PT/EN |
| `src/stats/strings.ts` | Descrições das conquistas (PT/EN) e textos da tela de recordes |
| `src/game/session.ts` | Ganchos: `observeTick` depois de cada `stepRace`; `settleRace` no evento `race_over` |
| `src/ui/screens/info.ts` + `records.css` | Tela de recordes: Pistas · Jogadores · Conquistas |
| `src/ui/screens/results.ts` | Quadro "Conquistas desbloqueadas" no resultado |
| `tests/stats.test.ts` | Corrida simulada, cada conquista nova (dispara e não dispara), save corrompido, limite de perfis |

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
- **Colisões**: batidas carro-carro que o núcleo registra (evento `collision`; os dois lados contam).
  Raspão lado a lado não gera evento e não conta.
- Só corrida que chega ao fim conta: sair pelo menu de pausa ou reiniciar no meio descarta a corrida.
- Save adulterado: contador negativo, texto, NaN ou infinito vira 0; posição fora de 1..`MAX_CARS` (20) some; perfil sem
  nome, repetido ou além do limite some; nome passa pelo mesmo limite do lobby (12). Nunca lança.

## As 8 conquistas novas

| ID | Regra exata |
|---|---|
| `PODIO_DE_EQUIPE` | Três humanos no 1º, 2º e 3º numa corrida com IA. Vai para os três. |
| `DO_ULTIMO_AO_PRIMEIRO` | Vencer tendo fechado a primeira volta em último (posição = número de carros). Corrida de 1 volta não serve. |
| `SEM_ARRANHAO` | Terminar uma corrida com IA sem nenhuma colisão carro-carro nem batida no cenário. |
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
2. Cada assento que ganhou algo recebe uma mensagem `good` no próprio HUD ("CONQUISTA: Nome"; várias viram
   uma linha só, com o excedente contado, porque o centro do HUD tem 3 vagas).
3. A tela de resultado mostra o quadro "Conquistas desbloqueadas", com a cor de quem ganhou cada uma.
4. Steam: `getDesktop()?.achievement(id)`, como antes.

## Tela de recordes

- **◀ ▶ trocam de aba** (as abas são listas verticais; esquerda/direita não têm outro uso ali).
- **Jogadores**: a primeira linha é o total; o detalhe acompanha o foco. A lista e o detalhe rolam cada um por si;
  a melhor posição por pista é um item de foco próprio, entre o último jogador e o Voltar, para o controle
  alcançá-la quando o detalhe não cabe na tela (720p com muitas pistas).
- **Conquistas**: barra de progresso, desbloqueadas primeiro, bloqueadas depois, cada uma com a descrição.
