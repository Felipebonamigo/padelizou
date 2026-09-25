# Conquistas, perfil e Rich Presence

> Marco M4 do `CRONOGRAMA.md`: *"Conquistas (20), Steam Cloud (perfil), Rich Presence"*. Escrito em 25/09/2026.
> A lógica inteira está em `Padel.Core/Perfil/` (namespace `Padel.Core.Perfil`), pura e testada
> (`ConquistasTests.cs`, `PerfilTests.cs`). A camada Godot/Steam só lê e grava o que está definido aqui.

## As 20 conquistas: o que se cadastra no Steamworks

O **ID é o "API name" da Steam e não muda depois de publicado**: a Steam guarda o desbloqueio de cada jogador por
esse nome, e renomear apaga a conquista de quem já a tinha. Nome e descrição podem mudar à vontade. A fonte da verdade
é `CatalogoDeConquistas` e um teste confere esta tabela contra ele (IDs, ordem, textos, secreta, meta). Se o catálogo
mudar, a tabela muda junto.

"Vencedor" e os outros termos da coluna de condição estão definidos logo abaixo da tabela.

| ID | Nome (PT · EN · ES) | Descrição (PT · EN · ES) | Secreta | Condição exata | Progresso na Steam |
|---|---|---|---|---|---|
| `PRIMEIRO_PONTO` | Primeiro ponto · First Point · Primer punto | Ganhe o seu primeiro ponto numa partida. · Win your first point in a match. · Gana tu primer punto en un partido. | não | O time do perfil ganha ≥ 1 ponto numa partida fora do treino (mesmo se ela for abandonada depois) | — |
| `PRIMEIRA_VITORIA` | Primeira vitória · First Win · Primera victoria | Vença uma partida. · Win a match. · Gana un partido. | não | Partida terminada e vencida, fora do treino; ou o evento `VitoriaOnline` (com ou sem rival humano) | — |
| `PNEU` | Pneu · Bagel · Rosco | Vença um set por 6-0. · Win a set 6-0. · Gana un set 6-0. | não | Um set encerrado 6-0 a favor do time do perfil (6-1 não conta, 0-6 não conta) | — |
| `PONTO_DE_OURO` | Ponto de ouro · Golden Point · Punto de oro | Vença um ponto de ouro, no 40-40. · Win a golden point at 40-40. · Gana un punto de oro con 40-40. | não | O time do perfil vence ≥ 1 ponto jogado em 40-40 com ponto de ouro ligado (fora do tie-break) | — |
| `TIE_BREAK` | Tie-break · Tiebreak · Tie-break | Vença um set no tie-break. · Win a set in a tiebreak. · Gana un set en el tie-break. | não | Um set vencido pelo time do perfil que foi decidido no tie-break (7-6) | — |
| `VIRADA` | Virada · Comeback · Remontada | Vença um set em que esteve 3 games atrás. · Win a set after trailing by 3 games. · Gana un set tras ir 3 juegos por detrás. | não | Num set vencido, o time esteve ≥ 3 games atrás (0-3, 1-4, 2-5...); 2 atrás não conta | — |
| `BANDEJA_100` | Cem bandejas · 100 Bandejas · Cien bandejas | Bata 100 bandejas, somando todas as partidas. · Hit 100 bandejas across all your matches. · Pega 100 bandejas, sumando todos los partidos. | não | Soma de golpes do tipo Bandeja dos jogadores do perfil, todas as partidas e o treino; 99 não, 100 sim | `BANDEJAS` ≥ 100 |
| `VIBORA_VENCEDORA` | Víbora vencedora · Winning Víbora · Víbora ganadora | Ganhe um ponto com uma víbora. · Win a point with a víbora. · Gana un punto con una víbora. | não | ≥ 1 vencedor com Víbora | — |
| `POR_3` | Por 3 · Por 3 · Por 3 | Ganhe um ponto com um remate por 3, pra fora pela lateral. · Win a point with a smash out over the side wall (por 3). · Gana un punto con un remate por 3, fuera por la lateral. | não | ≥ 1 vencedor com SmashPor3 (smash comum vencedor não conta) | — |
| `POR_4` | Por 4 · Por 4 · Por 4 | Ganhe um ponto com um remate por 4, pra fora por cima do fundo. · Win a point with a smash out over the back wall (por 4). · Gana un punto con un remate por 4, fuera por encima del fondo. | sim | ≥ 1 vencedor com SmashPor4 | — |
| `CHIQUITA_VENCEDORA` | Chiquita vencedora · Winning Chiquita · Chiquita ganadora | Ganhe um ponto com uma chiquita. · Win a point with a chiquita. · Gana un punto con una chiquita. | não | ≥ 1 vencedor com Chiquita | — |
| `CONTRAPARED` | Contrapared · Off the Back Glass · Contrapared | Ganhe um ponto com uma contrapared, batendo no seu próprio vidro. · Win a point with a contrapared, hitting off your own back glass. · Gana un punto con una contrapared, golpeando contra tu propio cristal. | sim | ≥ 1 vencedor com Contrapared | — |
| `PELA_PORTA` | Pela porta · Through the Door · Por la puerta | Ganhe um ponto com a bola saindo pela porta. · Win a point with the ball going out through the door. · Gana un punto con la bola saliendo por la puerta. | não | ≥ 1 vencedor do perfil em que a bola quicou do outro lado e saiu pela porta | — |
| `RALLY_30` | Rally de 30 · 30-Shot Rally · Peloteo de 30 | Jogue um ponto de 30 golpes ou mais. · Play a point of 30 or more shots. · Juega un punto de 30 golpes o más. | não | Um ponto com ≥ 30 golpes (os quatro jogadores, saque incluído), ganho ou perdido; 29 não | — |
| `VITORIA_NO_DIFICIL` | Vitória no difícil · Hard Win · Victoria en difícil | Vença uma partida contra a IA no difícil. · Beat the AI on Hard. · Gana un partido contra la IA en difícil. | não | Partida terminada e vencida, dificuldade Difícil, os dois rivais da IA desde o começo (`RivalDaIA`), em qualquer modo menos o treino: sala online sem rival humano conta; versus entre humanos não | — |
| `SEM_BOLA_NA_REDE` | Sem bola na rede · Clean Net · Sin bola a la red | Vença uma partida sem mandar nenhuma bola na rede. · Win a match without hitting a single ball into the net. · Gana un partido sin mandar ninguna bola a la red. | não | Partida terminada e vencida com 0 golpes do perfil na rede, faltas de saque incluídas | — |
| `CAMPEAO_DE_ETAPA` | Campeão de etapa · Stage Champion · Campeón de etapa | Vença uma etapa do circuito na carreira. · Win a stage of the career circuit. · Gana una etapa del circuito en la carrera. | não | O evento `EtapaVencida` | — |
| `NUMERO_1` | Número 1 · Number 1 · Número 1 | Termine o circuito da carreira em 1º no ranking. · Finish the career circuit ranked 1st. · Termina el circuito de la carrera en el 1.º puesto del ranking. | não | O evento `CircuitoEncerrado(1)`; posição 2 ou pior não | — |
| `VITORIA_ONLINE` | Vitória online · Online Win · Victoria en línea | Vença uma partida online contra outro jogador. · Win an online match against another player. · Gana un partido en línea contra otro jugador. | não | Partida online terminada e vencida com ao menos um rival humano desde o começo: no host, o resumo `Online` com `RivalDaIA = false`; no cliente, o evento `VitoriaOnline(humanos, vaga)` com `AlgumRivalHumano`. Sala sem rival humano não conta; rival que cai no meio e vira IA continua contando | — |
| `MARATONA` | Maratona · Marathon · Maratón | Jogue 50 partidas até o fim. · Finish 50 matches. · Termina 50 partidos. | não | 50 partidas terminadas fora do treino (abandonada não conta); 49 não, 50 sim | `PARTIDAS` ≥ 50 |

### O que cada termo quer dizer (é o que o `ColetorDaPartida` mede)

- **Jogador do perfil**: o índice (`time*2 + índice`, de 0 a 3) passado ao coletor. No coop são os dois do mesmo time,
  e os números deles somam. Times diferentes são recusados, porque vitória e placar são de um time só.
- **Vencedor**: ponto que o time do perfil ganhou tendo sido um jogador do perfil o **último a bater**. O rival não
  devolveu: dois quiques, bola que saiu depois de quicar, ou que voltou pelo vidro. Conta pelo tipo do último golpe
  (o saque vencedor, o ace, conta como Saque).
- **Erro**: ponto que o time perdeu tendo sido um jogador do perfil o último a bater, contado pelo `Motivo` do árbitro
  (Rede, NaoPassou, ParedeSemQuicar, Fora, DuplaFalta, BolaMorta).
  Numa partida, vencedores + erros dos quatro jogadores = pontos jogados (há teste).
- **Bola na rede**: golpe do perfil que tocou a rede e morreu, seja num ponto perdido (`Motivo.Rede`), numa falta de
  saque na rede ou numa dupla falta cuja segunda bola foi na rede. Na física daqui a bola que toca a rede volta, então
  todo toque na rede é erro de quem bateu. Bola que quica do próprio lado sem tocar a rede (`NaoPassou`) não conta como "na rede".
- **Saída pela porta**: a bola do vencedor saiu pela porta, que fica na lateral, com |y| de 0,45 a 1,25 m e abaixo de
  2 m. É a mesma regra da física, `Quadra.NaPorta`. O evento `Saiu` da `Partida` não diz por onde a bola saiu (o da
  `Bola` diz, mas a tradução perde o campo), então o coletor lê a `Bola`, que fica parada no ponto de saída. Bola que
  sai pela porta **sem quicar** é erro de quem bateu e não conta.
- **Ponto de ouro**: ponto jogado com o placar em 40-40, ponto de ouro ligado e fora do tie-break. É o placar de
  **antes** do ponto: os eventos chegam com o placar já atualizado, então o coletor guarda o estado de depois do ponto
  anterior. Falta e let não mexem no placar.
- **Desvantagem revertida**: num set que o time venceu, o maior valor de (games do rival − games do time) depois de
  cada game.
- **Rally**: golpes de um ponto, os quatro jogadores e o saque incluídos. É o mesmo número da `Estatisticas.MaiorRally`.
- **Rival da IA** (`RivalDaIA`): os dois rivais eram da IA quando a partida começou, pelo `OpcoesDaPartida.Humanos`
  (não pelo `Jogador.Humano`, que muda no meio). O online nem sempre tem gente do outro lado: o host inicia a sala
  com quem estiver nela quando a espera acaba, e vaga vazia é IA (`ServidorDaPartida.Iniciar`). O rival humano que cai
  no meio e vira IA (`ServidorDaPartida`, 3 s calado) continua contando como humano: a partida começou contra gente.

### Ajustes em relação à proposta, e por quê

- **VIRADA = 3 games.** Estar 2 atrás é uma quebra mais um game de saque segurado, e acontece o tempo todo. Em 400
  partidas IA × IA com semente, o time 0 venceu 11 sets depois de estar 2 atrás e nenhum depois de estar 3. Para um
  humano é raro, mas alcançável. O número está em `CatalogoDeConquistas.DesvantagemDaVirada`.
- **CONTRAPARED = vencedor com contrapared.** Nas mesmas 400 partidas saíram 206 vencedores de contrapared (os
  quatro jogadores somados), então é alcançável e cabe como secreta.
- **PELA_PORTA = a bola sai pela porta**, não o jogador. A "saída pela porta" do padel de verdade, em que o jogador
  sai da quadra para buscar a bola, não existe na física: o corpo fica em |x| ≤ 4,7 (`Jogador.Limitar`). Se um dia
  existir, é conquista nova com ID novo. A bola saindo pela porta acontece: 17 saídas em 400 partidas, entre remates e
  bolas anguladas.
- **POR_3 e POR_4 = vencedor com o golpe especial.** O solucionador (`GolpesEspeciais`) só devolve o remate se a bola
  quica e sai do jeito pedido, mesmo com pequenas variações. Se o rival intercepta, o último golpe passa a ser dele e o
  ponto não conta.
- **Treino** conta golpes e tempo, e mais nada: nem partida, nem vitória, nem conquista de partida. Bandeja batida no
  treino conta para `BANDEJA_100`, porque é onde se treina bandeja.
- **VITORIA_ONLINE pede um rival humano.** Sem isso, abrir uma sala, não esperar ninguém e ganhar da IA dava "Vença
  uma partida online". Por isso a descrição diz "contra outro jogador". Tem dois caminhos, e desbloquear é
  idempotente, então os dois juntos não duplicam nada. No host, o resumo com modo `Online` e `RivalDaIA = false`. No
  cliente online não existe `Partida` (só a `VisaoDaPartida` da rede), então quem avisa é a camada online, com o evento
  `VitoriaOnline(humanos, vaga)`. O evento leva quem era humano e a vaga do cliente, e o Core decide se havia rival
  humano (`AlgumRivalHumano`): o cliente parceiro do host pode ter jogado contra duas IAs (o rival que tinha entrado
  saiu antes do começo). Sem rival humano, o evento dá só `PRIMEIRA_VITORIA`.
- **VITORIA_NO_DIFICIL = `RivalDaIA`, em qualquer modo menos o treino**, sala online sem rival humano inclusive. A
  primeira versão deste documento dizia "online não", partindo de que no online o rival é sempre gente, o que não é
  verdade. A sala vazia é a IA das mesmas Opções do jogo local (o `PartidaNode` passa `Configuracao.Dificuldade` ao
  host), e a descrição promete "contra a IA no difícil". Um versus entre humanos no difícil não conta: a dificuldade é
  só da IA. Numa mesma partida, `VITORIA_ONLINE` e `VITORIA_NO_DIFICIL` se excluem (há teste).

### Estatísticas da Steam (cadastrar antes das conquistas cumulativas)

| API name (tipo) | Valor gravado | Conquista |
|---|---|---|
| `BANDEJAS` (INT, só cresce) | `CatalogoDeConquistas.ValorDaEstatistica(perfil, "BANDEJAS")` | `BANDEJA_100`, progresso de 0 a 100 |
| `PARTIDAS` (INT, só cresce) | `CatalogoDeConquistas.ValorDaEstatistica(perfil, "PARTIDAS")` | `MARATONA`, progresso de 0 a 50 |

Os API names das estatísticas também não mudam depois de publicados. No Steamworks, cada conquista cumulativa aponta
para a estatística dela como "progress stat", com mínimo 0 e máximo igual à meta. Faltam os ícones de cada conquista,
nas versões desbloqueada e bloqueada. Confira o tamanho e o formato atuais no painel antes de desenhar.

## O perfil (Steam Cloud)

`PerfilDoJogador` é imutável e guarda:

- o nome e a mão (`Destro`);
- `PreferenciasAlteradasEm`, o instante em que nome ou mão mudaram;
- partidas, vitórias, maior rally e segundos de jogo;
- golpes e vencedores por tipo;
- as conquistas, cada uma com o ID e o instante do desbloqueio, em UTC.

Nenhum instante vem do relógio dentro do Core: a camada de cima passa o "agora". A igualdade é pelo conteúdo, e um
perfil impossível nem chega a existir, como uma contagem negativa, um tempo `NaN` ou um ID fora do formato. Assim tudo
o que o `Salvar` escreve, o `Carregar` lê.

- **Arquivo**: `PersistenciaDoPerfil.Salvar/Carregar`, JSON com `"Versao": 1`. O texto é canônico (mesmo perfil,
  mesmo texto). Uma conquista que este jogo não conhece, gravada por uma versão mais nova, é preservada. Um tipo de
  golpe novo exige subir a versão do arquivo: o jogo antigo recusa nome de golpe desconhecido para não apagar a
  contagem dele do Cloud.
- **Erros**: `PerfilIlegivelException` com `Motivo`:
  - `Corrompido`: não é JSON, falta campo, há número negativo e afins. A camada de cima pode voltar ao perfil
    padrão, mas guarda antes o arquivo ruim ao lado (`perfil.json.corrompido`), para ter como investigar.
  - `VersaoDesconhecida`: se `VeioDeUmJogoMaisNovo`, **não sobrescrever**. Jogue sem salvar e peça para atualizar o
    jogo; senão o Cloud espalha o perfil zerado por cima do de verdade.
- **Conflito (dois PCs)**: `PerfilDoJogador.Mesclar(a, b)`. Sem uma versão-base comum não dá para saber o que cada
  lado somou, então as regras são estas:
  - cada contador fica com o **máximo** dos dois; nunca conta em dobro e perde no máximo o que um lado jogou offline;
  - as conquistas ficam com a **união**, cada uma com o instante **mais antigo**;
  - nome e mão vêm de quem os **mudou por último** (`PreferenciasAlteradasEm`). Jogar uma partida não conta como
    mudança, então jogar num PC não desfaz a troca de nome feita no outro. Empate: o nome que vem primeiro na ordem ordinal.

  A mesclagem é comutativa e idempotente: os dois PCs chegam ao mesmo arquivo.

## Rich Presence

`PresencaNaSteam.Texto(estado, idioma)` é pura. Estes são os textos em português; o inglês e o espanhol estão no
código e nos testes:

| Estado | Português |
|---|---|
| `NoMenu` | No menu |
| `JogandoUmSet(4, 3)` | Jogando um set, 4-3 |
| `JogandoUmSet(2, 5, Set: 2)` | Jogando o set 2, 2-5 |
| `NaCarreira(3, Semifinal)` | Carreira: etapa 3, semifinal |
| `NoOnline(2)` | Online 2x2 |
| `Treinando` | Treinando |

Os games aparecem do ponto de vista do jogador. Um estado impossível, como games fora de 0 a 7 ou etapa 0, é recusado
na construção. A Steam corta valores com mais de 256 caracteres, e há teste de que nenhum texto passa disso.

## O que o jogo precisa ligar (camada Godot/Steam)

1. **Ao criar a `Partida`** (local, coop, carreira, treino, e no online só no host), crie o coletor logo em seguida,
   antes do primeiro `Avancar`: `var coletor = new ColetorDaPartida(partida, modo, indices)`. Os índices são o do
   humano (em geral 0) ou `0, 1` no coop.
2. **No fim da partida** (`Partida.Acabou`, evento `Fim`) **ou no abandono** (voltar ao menu no meio):
   - pegue `var resumo = coletor.Resumo()` e chame `coletor.Dispose()`;
   - aplique `var r = AvaliadorDeConquistas.Aplicar(perfil, resumo, DateTimeOffset.UtcNow)`. O relógio é lido aqui,
     fora do Core. Aplique **uma vez por partida**: as conquistas são idempotentes, as estatísticas não. Na
     abandonada, `Terminada` sai `false`: conta golpes e tempo, mas não conta partida.
   - **salve** o perfil: `PersistenciaDoPerfil.Salvar`, escrevendo num arquivo temporário e renomeando por cima, para
     um corte de energia não deixar arquivo pela metade;
   - **chame a Steam**:
     - `SetAchievement(c.Id)` para cada `c` em `r.Novas`;
     - `SetStat("BANDEJAS", ...)` e `SetStat("PARTIDAS", ...)` com `ValorDaEstatistica`;
     - `StoreStats()`.
3. **Carreira**: depois de `Carreira.FecharEtapa()`, se o campeão é a dupla do jogador, aplique `EtapaVencida`. Quando
   não sobrar etapa, aplique `CircuitoEncerrado(posição da dupla no Ranking())`. Depois salve e chame a Steam, como no
   passo 2.
4. **Online**: no cliente, ao fim de uma partida vencida, aplique `new VitoriaOnline(cliente.Humanos, cliente.Indice)`,
   com o que o `Comecou` do host trouxe, e não com quem ainda é humano no fim. No host, o coletor com
   `ModoDaPartida.Online` já basta, e o evento extra não duplica nada.
5. **Ao abrir o jogo**, rode `PersistenciaDoPerfil.Carregar`, trate os erros como na seção do perfil e sincronize:
   toda conquista do perfil que a Steam ainda não tem recebe `SetAchievement`. É o caso da conquista que desbloqueou
   com a Steam fechada ou em outro PC. Grave também as duas estatísticas.
6. **Conflito do Cloud**: quando houver as duas versões, a local e a da nuvem, `Mesclar` e salve.
7. **Rich Presence**: a cada mudança de estado (menu, fim de cada game, fase da carreira, entrada no online), chame
   `SetRichPresence("status", PresencaNaSteam.Texto(estado, idioma))`. Para aparecer na lista de amigos
   (`steam_display`), o arquivo de localização de Rich Presence do Steamworks precisa dos mesmos textos em tokens,
   nas três línguas. A língua vem da Steam: `brazilian` ou `portuguese` → Português, `spanish` ou `latam` → Espanhol,
   o resto → Inglês.

## Pendências

- **Ligado no jogo em 25/09** (`Padel.Godot/scripts/Perfil/PerfilLocal.cs`, `PartidaNode.LigarOPerfil`,
  `CarreiraNode.RegistrarNoPerfil`, a tela *Perfil* do menu): passos 1 a 4 feitos — coletor na partida local, no coop,
  na carreira e no host; a vitória online do cliente pelo evento; etapa e circuito da carreira; gravação atômica em
  `user://perfil.json`, arquivo ilegível guardado ao lado (nunca apagado), e arquivo de versão mais nova intocado. As
  conquistas novas aparecem na tela de fim. **Falta a Steam** (passos 2, 5, 6 e 7: `SetAchievement`, estatísticas,
  sincronia na abertura, mesclagem do Cloud e Rich Presence), que entra com o Facepunch.Steamworks (D5).

- **Coletor no cliente online**: o cliente não tem `Partida`, então golpes e vencedores de uma partida online não
  entram no perfil do cliente. Só a vitória entra, pelo evento. O caminho é o host mandar o resumo de cada jogador no
  fim, ou um coletor sobre os eventos da `VisaoDaPartida`.
- Cadastrar as 20 conquistas, as 2 estatísticas e os tokens de Rich Presence no Steamworks. Desenhar os ícones.
- A raridade real de `RALLY_30`, `VIRADA` e `PELA_PORTA` só aparece no playtest (M5). Mexer na meta é mudar o número
  **e** o teste; o ID nunca muda.
