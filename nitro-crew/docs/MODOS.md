# Modos de festa (passo 3.5)

Menu principal → **Festa** → Torneio de sofá, Escolta ou Revezamento. Os três são **só locais** (um computador,
controles e teclados ligados nele): o online continua correndo só a corrida rápida (ver "Online" abaixo).

| Modo | Pessoas | Onde mora a regra | Resultado |
|---|---|---|---|
| Torneio de sofá | 2–8, revezando 1–4 controles | `src/core/tournament.ts` (puro) | classificação por pontos + final |
| Escolta | 1–4 em equipe | `src/core/modes.ts` (no `stepRace`) | posição do VIP (top 3 = vitória) |
| Revezamento | 2 ou 4, em duplas | `src/core/modes.ts` (no `stepRace`) | posição do carro da dupla |

## Torneio de sofá

**Fluxo**: Festa → Torneio → lobby (cada assento é um *controle* que vai passar de mão em mão: sem nome nem carro)
→ inscrição → "Passe o controle" → bateria → resultado → classificação do torneio → "Passe o controle" → … → final
→ campeão.

**Inscrição** (`src/ui/screens/party.ts`): 2 a 8 pilotos, cada um com nome (Enter edita) e carro (← →). Começa com os
nomes mais recentes das estatísticas (perfis) e completa com "Piloto N". Nomes vazios ou repetidos (sem diferenciar
maiúsculas) não deixam começar. Opções: rodadas classificatórias (1–3), a copa cujas pistas o torneio usa (só copas
destravadas), voltas por bateria (as da corrida rápida) — mais dificuldade, câmbio e carros na pista, que vêm do lobby.

**Chaves** (`heatSizes`, `planRound`):
- Cada pessoa corre **uma bateria por rodada**. Uma bateria tem no máximo tantos humanos quantos controles houver
  (até 4) e o resto do grid é IA. As baterias são as mínimas que cabem, do tamanho mais igual possível
  (8 pessoas e 4 controles: 4+4; 6: 3+3; 7: 4+3; 5 com 2 controles: 2+2+1).
- Rodada 1: na ordem de inscrição. Rodadas seguintes: pela classificação (os líderes correm juntos, como no sistema
  suíço), para as baterias ficarem disputadas.
- Todas as baterias de uma rodada correm **a mesma pista** (a rodada *r* usa a pista *r* da copa); a final usa a
  última pista da copa. Dentro da bateria, o assento é a ordem da chave: o primeiro larga na frente dos outros humanos
  (na final, o melhor classificado).

**Pontos**: pela posição na corrida (contra a IA também), na tabela de sempre `20-15-12-10-8-6-4-3-2-1`; do 11º em
diante, zero. Somam de rodada em rodada.

**Desempate** (`compareRows`), nesta ordem: mais pontos → melhor colocação numa bateria → menor tempo somado das
baterias (quem não terminou alguma fica atrás de quem terminou todas; o tempo é comparável porque todos correram as
mesmas pistas) → ordem de inscrição. A regra aparece escrita na tela da classificação.

**Final**: os 4 melhores da classificatória (ou menos, se houver menos controles ou menos pessoas). A **final decide o
pódio** entre os finalistas (a ordem de chegada entre eles); os outros ficam pela classificatória. Com 1 controle não
há final (seria uma pessoa sozinha): vale a classificatória.

**Entre baterias**: a tela "Passe o controle" mostra, para cada controle (com o nome do dispositivo e a cor do
assento), "Passe o controle 2 para Ana" — ou "Ana fica com o controle 2" quando é a mesma pessoa da bateria anterior.
Dali: LARGAR, ver a classificação ou abandonar o torneio. O torneio vive só na sessão (não vai para o save): sair para o
menu o encerra.

**Estatísticas e conquistas**: cada bateria é uma corrida rápida comum para o save — conta corrida, vitória, pódio,
distância etc. no perfil **do nome inscrito** (o perfil das estatísticas é por nome), vale recorde e conquista. O lobby
lembra, por assento, o nome da última bateria.

## Escolta

A equipe protege um **VIP** da IA: um Falcão GT na equipe humana, com etiqueta dourada "VIP" em cima do carro, ponto
dourado no minimapa e linha fixa no HUD de cada jogador ("VIP em 4º · meta: top 3"). **Vale a posição do VIP**: terminar
entre os 3 primeiros (`ESCORT_GOAL_POSITION`) é vitória; o resultado mostra o veredito e destaca a linha do VIP.

- **Grid**: VIP na pole, a equipe logo atrás dele, a IA atrás de todos (`arrangeGrid`). O VIP ocupa a vaga de uma IA
  (o total de carros não muda).
- **Mais lento**: velocidade máxima × `ESCORT_VIP_TOP_FACTOR` (0,98) e cérebro fixo, sem sorteio, com a habilidade do
  *melhor* rival da dificuldade, faixa do meio e agressividade zero. Ele usa box e nitro como a IA.
- **Empurrão** (`applyEscortPush`, chamado de `applyTow` em `sim/coop.ts`): um humano da equipe colado atrás do VIP
  (até `ESCORT_PUSH_DISTANCE` = 2 segmentos, lateral < `ESCORT_PUSH_LATERAL`) e mais rápido que ele leva o VIP a
  `ESCORT_PUSH_SPEED_FACTOR` (97%) da própria velocidade, até `ESCORT_PUSH_TOP_CAP` (112%) da máxima do VIP. Vale
  sempre na escolta (é a mecânica do modo, não a assistência); o evento `tow` (e a mensagem "Empurrando o VIP!") sai
  só quando um empurrão começa. Com a assistência "Empurrão" ligada, o VIP parado também é socorrido como um
  companheiro.
- **Bloquear**: não há regra nova — as colisões de sempre (quem bate por trás perde velocidade) fazem o bloqueio.
- **Elástico da IA**: na escolta ele mira o VIP, não o melhor humano (rivais muito à frente do VIP aliviam, os muito
  atrás apertam). Sem isso, a equipe que disparava na frente fazia a IA inteira acelerar e passar o VIP.
- **Chegada**: o VIP conta como humano para o fim da corrida (todos terminaram, ou 45 s depois do primeiro deles).

**Calibragem** (`npx tsx scripts/escort-balance.ts <dificuldade>`, 3 voltas, 20 carros, 8 pistas × 2 sementes; o
"empurrador" é um robô simples que só segue o VIP — gente de verdade também bloqueia):

| Dificuldade | VIP sem ajuda: top 3 | VIP com o empurrador: top 3 |
|---|---|---|
| Amador | 4/16 | 9/16 |
| Profissional | 1/16 | 10/16 |
| Campeão | 0/16 | 2/16 |

**Recalibragem da onda C** (humano "médio" simulado: freio automático, volante reativo com atraso, lapsos; 20 carros,
3 voltas, 32 corridas por célula, ±8 p.p.). O VIP usava a habilidade do melhor rival e largava na pole: amador e
profissional passavam de 88%. Agora a habilidade dele é um ponto fixo da faixa (`ESCORT_VIP_SKILL`: amador 0,8,
profissional 0,7, campeão 1) e `ESCORT_VIP_TOP_FACTOR` foi de 0,98 para 0,99 (só pesa no campeão). VIP no top 3:

| Dificuldade | 1 humano | 2 humanos | 3 humanos |
|---|---|---|---|
| Amador | 100% | 94% | 91% |
| Profissional | 45% | 56% | 69% |
| Campeão | 34% | 41% | 47% |

**Estatísticas e conquistas**: tudo conta como numa corrida normal para os humanos (a posição deles é a deles; o VIP é
da IA e não soma perfil). Recordes valem.

## Revezamento

**Um carro por dupla**: os humanos em ordem de assento, de dois em dois (P1 + P2, P3 + P4; com assentos salteados, os
dois primeiros ocupados formam a primeira dupla). O lobby exige 2 ou 4 jogadores e mostra "Dupla 1/2"; o segundo de
cada dupla não escolhe carro — a dupla corre no carro de quem larga, com os dois nomes ("Ana / Bia"). Com 4
jogadores, o seletor co-op/versus decide se as duas duplas são uma equipe ou rivais.

**A troca** (`updateRelay`, a cada tick depois das posições):
1. Fechar uma volta (sem ser a última) libera a troca (`relay_due`).
2. Com a troca liberada, **entrar no box** passa o controle ao parceiro (`relay_swap`): o `seat` do carro muda, e o
   `stepRace` passa a ler a entrada do outro assento no tick seguinte. A entrada de quem espera **nunca** é lida.
3. Passar pelo trecho do box sem entrar perde a troca desta volta (`relay_missed`): quem dirige segue mais uma volta.
Antes de fechar a primeira volta o box não troca ninguém.

**Tela**: os dois da dupla têm viewport; o de quem espera mostra o carro da dupla com "Sua vez na próxima troca".
Com a troca liberada, quem dirige vê "Box! Passe o controle para Bia". Na troca: "SUA VEZ!" para quem entra e
"Controle com Bia" para quem sai. O resultado mostra quantas trocas cada dupla fez.

**Estatísticas e conquistas**: os dois da dupla levam o **resultado** do carro (corrida, vitória, pódio, vitória
co-op, melhor posição na pista); **voltas, distância e tempo** ficam só com quem largou, para o total não contar o
mesmo carro duas vezes. Nitros, box, batidas, empurrões são de quem estava ao volante. **Recordes não valem** (a volta
e o tempo são de dois pilotos). As conquistas de resultado (primeira vitória, sem box…) vão para quem cruzou a linha.

## Núcleo, estado e determinismo

- `RaceConfig.mode?: 'escort' | 'relay'` (ausente = corrida normal). O estado do modo fica em `RaceState.party`
  (`vipId`, `pushBy`, `relay[]`), JSON puro; `deserializeRace` completa campos que faltarem (`normalizeParty`).
- Tudo acontece dentro do `stepRace`: grid em `createRace` (`humanDrivers`, `extraCars`, `arrangeGrid`), empurrão em
  `applyTow`, troca em `updateModes`, chegada em `checkRaceOver`. Nada de sorteio: o VIP tem cérebro fixo.
- Corrida sem `mode` é idêntica à de antes (mesma ordem de carros, mesmos ids, mesmo consumo do `rng`).
- O torneio não mexe no núcleo: cada bateria é uma corrida rápida (`heatHumans` monta os humanos: cada um por si,
  time = assento, cor do controle).

## Online

Os três modos são só locais. O online não tem como pedi-los: a largada em rede (`StartConfig` → `raceConfigFrom`) não
carrega modo, e a Festa não aparece no lobby online. Por garantia, `beginRace` recusa uma corrida em rede com modo de
festa (volta ao menu, com o aviso `party.onlineRefused` no console), e `onlineAllowed(config)` diz se uma configuração
pode ir para a rede. Suportar online exigiria levar o modo no protocolo e validar as duplas/VIP dos dois lados — não
saiu de graça, então ficou de fora.

## Arquivos

| Arquivo | O quê |
|---|---|
| `src/core/tournament.ts` | chaves, pontos, desempate, final (puro) |
| `src/core/modes.ts` | escolta e revezamento no núcleo |
| `src/game/party-session.ts` | liga os modos à sessão: largadas, bateria do torneio, HUD fixo, mensagens de evento, resultado |
| `src/party/rules.ts`, `src/party/strings.ts` | o que o lobby mostra/exige por modo; textos PT/EN |
| `src/ui/screens/party.ts` + `party.css` | Festa, inscrição, "Passe o controle", classificação, pedaço do resultado |
| `tests/modes.test.ts` | torneio (6 e 8 pessoas, desempate, final), escolta, revezamento, estatísticas |

Ganchos nos arquivos compartilhados: `core/types.ts` (config, estado, eventos), `sim/race.ts` (grid e `updateModes`),
`sim/coop.ts` (empurrão no VIP), `sim/positions.ts` (VIP na chegada), `sim/ai.ts` (elástico mira o VIP),
`serialize.ts`, `constants.ts`, `game/contracts.ts` (telas, eventos, `RaceMode`, `PartyResultsInfo`, `ctx.party`),
`game/session.ts`, `game/stats.ts`, `game/save.ts` (sem recorde no revezamento), `ui/menus.ts`,
`ui/screens/{simple,lobby,select,results}.ts`, `render/cars.ts` (etiqueta do VIP), `render/hud.ts` (VIP no
minimapa; parceiro do revezamento não aparece como companheiro).
