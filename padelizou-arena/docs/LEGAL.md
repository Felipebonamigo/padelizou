# Textos legais — Padelizou Arena

> ## RASCUNHO — NÃO PUBLICAR
>
> **Nada deste arquivo vale como termo aceito enquanto este aviso estiver no topo.** Escrito em 25/09/2026
> (CRONOGRAMA.md, M5: "Textos legais: política de privacidade, EULA, LGPD/GDPR"), por IA, sem revisão jurídica.
> As citações de lei foram escritas de memória e **não foram conferidas** — o advogado confere cada uma (seção 6).
> Onde há dúvida jurídica real, o texto diz "dúvida" e aponta o item da seção 6.
> A política descreve o jogo **como ele vai sair** (M5/M6); a seção 7 lista o que o código ainda precisa fazer
> pra cada frase ser verdade. Frase sem código que a cumpra não vai pro ar.

**Marcadores a preencher:** `[EMAIL DE CONTATO]` · `[NOME COMPLETO]` · `[CIDADE/UF]` · `[DATA DE VIGÊNCIA]` ·
`[LOCAL DO SERVIDOR]` e `[PROVEDOR DO SERVIDOR]` (onde roda a telemetria) · `[PROVEDOR DE E-MAIL]` ·
`[CLASSIFICAÇÃO INDICATIVA]` · `[DECIDIR: …]` (decisão de produto, não jurídica).

**Onde cada texto aparece:** política e EULA no menu do jogo ("Privacidade e termos") e nos campos próprios do
Steamworks (conferir os nomes dos campos no painel); EULA com aceite no primeiro início (ou pelo EULA do
Steamworks — dúvida A1). Vale igual pra demo, Playtest e Early Access.

**Coerência:** a seção de telemetria (1.5 / 2.5) resume [`TELEMETRIA.md`](TELEMETRIA.md). Mudou um campo lá,
muda aqui na mesma alteração.

---

## 1. Política de privacidade (PT-BR)

Versão 0 — [DATA DE VIGÊNCIA].

**Em resumo:** o jogo guarda quase tudo no seu computador. O desenvolvedor só recebe dado seu se você ligar a
telemetria (desligada por padrão; sem nome, SteamID nem IP), jogar o ranqueado (que precisa do SteamID) ou escrever
pra gente — ou se outro jogador mandar algo em que você aparece: uma denúncia (seção 5) ou o log dele, que traz os
nomes da sala e, na conexão por IP, o IP de quem hospedou. No online pela Steam, os outros jogadores da sala
recebem o seu nome e o seu SteamID. Conta e pagamento são da Valve.

### 1.1 Quem é o responsável (controlador)

[NOME COMPLETO], pessoa física, [CIDADE/UF], Brasil, desenvolvedor do Padelizou Arena. Contato pra tudo desta
política: **[EMAIL DE CONTATO]**. Não há encarregado (DPO) nomeado — como agente de tratamento de pequeno porte
(Resolução CD/ANPD nº 2/2022), o e-mail acima é o canal de comunicação (dúvida A2).

O Padelizou Arena é separado do site padelizou.com.br: os dados do jogo não vão pra base do site e a conta do
site não é ligada ao jogo. Se um dia for, esta política muda **antes**, e a ligação será opcional.

### 1.2 O que o jogo trata

| O quê | Onde fica | Quem vê ou recebe | Pra quê e base legal | Por quanto tempo |
|---|---|---|---|---|
| **Preferências**: dificuldade, formato, modo de golpe, mão (destro/canhoto), volume, o nome que você escolhe, o último endereço de sala digitado (na conexão por IP, é o IP de quem hospeda — dado de outra pessoa) | `configuracao.json` no seu computador; **não** vai pro Steam Cloud | Só você | Lembrar suas escolhas — execução de contrato (o endereço de quem hospeda: legítimo interesse, pra não digitar de novo) | Até você apagar |
| **Carreira e perfil**: etapas, duplas, resultados, ranking do circuito, conquistas, estatísticas | `carreira.json` e arquivos do perfil no seu computador; Steam Cloud | Só você (e a Valve) | Guardar seu progresso — execução de contrato | Até você apagar |
| **Registro técnico (log)**: versão, erros, placares, os nomes de quem estava na sala, o endereço e a porta da sala em que você entrou por IP (o IP de quem hospedou) e, em mensagens de erro, o caminho da pasta do jogo (que pode conter seu nome de usuário do sistema) | Pasta `logs/` no seu computador; não vai pro Steam Cloud | Ninguém — só sai se você anexar num relato de defeito (é texto: dá pra ler e apagar linhas antes) | Gravar: diagnóstico de defeito — legítimo interesse, sem sair do computador (dúvida A7). Mandar: só por ato seu (consentimento) | O jogo mantém os 5 mais recentes |
| **Partida online**: seu nome no jogo (vai pra todos da sala); os comandos do controle — direção e botões (vão pra quem hospeda); pela Steam, o seu SteamID (lobby e conexão); na conexão por IP, o seu IP | Memória dos computadores da sala, durante a partida. O ping é medido e mostrado só no seu computador | Os outros jogadores da sala; pelo SteamID, chegam ao seu perfil Steam, até onde a privacidade dele deixar | Jogar a partida — execução de contrato | Não fica guardado depois da partida (fora o log e as preferências acima) |
| **Ranqueado** (quando existir): SteamID, nível no Padelímetro, número de partidas, resultados | [DECIDIR: placar de líderes da Steam ou servidor do desenvolvedor] | Público: o ranking mostra nome e nível | Manter o ranking — execução de contrato | [DECIDIR] |
| **Telemetria** (só se você ligar) — ver 1.5 | Servidor do desenvolvedor em [LOCAL DO SERVIDOR]; no seu computador, o resumo dos últimos registros, a fila de envio, os identificadores e o marcador de sessão aberta | O desenvolvedor (servidor); só você (o que fica no computador) | Balancear o jogo e achar defeito — consentimento | Servidor: brutos 90 dias, depois só contagens agregadas anônimas. Computador: últimos 50 registros; apagado ao desligar (1.5) |
| **E-mails**: suporte e pedidos de privacidade, com o que vier anexado (log, print, vídeo — podem trazer nomes de outros jogadores e, na conexão por IP, o IP de quem hospedou) | Caixa de [EMAIL DE CONTATO] ([PROVEDOR DE E-MAIL]) | Só o desenvolvedor | Responder — legítimo interesse; atender pedido de direito — obrigação legal | 12 meses depois da última mensagem |
| **Denúncias e sanções**: de quem denuncia, o e-mail e o que mandou; de quem é denunciado, o nome que apareceu na tela, o SteamID (se houver), prints e vídeos; a decisão, o motivo e o prazo da sanção | Caixa de [EMAIL DE CONTATO] e a lista de sanções do desenvolvedor | Só o desenvolvedor; a Valve, quando a denúncia é repassada a ela | Manter o online justo e responder a contestação — legítimo interesse (dúvida A11) | Denúncia sem sanção: 12 meses depois de fechada. Com sanção: a denúncia, a prova e a linha da lista ficam enquanto a sanção valer e mais 12 meses (prazo pra contestar) |
| **Conta Steam**: compra, pagamento, SteamID, nome de perfil, amigos, conquistas, Steam Cloud, lobby, retransmissão da conexão, Rich Presence, Remote Play | Valve | Valve (e, no lobby, os outros jogadores da sala — ver "Partida online") | Política de privacidade da Valve. O jogo decide o texto do Rich Presence e o que vai no lobby (1.4) | Política da Valve |

Nome no online: [DECIDIR: o nome digitado no jogo (até 16 caracteres) ou o nome do seu perfil Steam]. Prazo de cada
sanção (seção 5): [DECIDIR].

### 1.3 O que o jogo não coleta

Nome real, e-mail, telefone, CPF, endereço, dados de pagamento (é a Valve), localização, contatos, voz ou texto (o
jogo não tem chat), identificadores de hardware ou de publicidade. O jogo não grava o IP de ninguém, com uma exceção:
na conexão por IP (1.4), o endereço que você digita pra entrar — o IP de quem hospeda — fica nas suas preferências
e no seu log (tabela 1.2). Sem anúncio, sem venda ou cessão de dados, sem perfil pra marketing.

### 1.4 Jogar online

- **Pela Steam (padrão):** a conexão passa pela rede de retransmissão da Valve (Steam Datagram Relay) e os outros
  jogadores não veem o seu IP. Mas a sala é um lobby da Steam: todos da sala recebem o SteamID uns dos outros e, por
  ele, chegam ao perfil Steam (nome, avatar e o que mais a privacidade do perfil deixar) — mesmo que o nome no jogo
  seja outro. Um jogador hospeda a partida: recebe os seus comandos e manda pra todos o estado do jogo e os nomes da
  sala. Nada disso fica guardado depois da partida, fora o registro técnico (tabela 1.2).
- **Conexão por IP** (rede local e versões de teste): quem hospeda vê o IP de quem entra, e quem entra digita o IP
  de quem hospeda, que fica nas preferências e no log de quem entrou (tabela 1.2). Use só com quem você conhece.
- **Rich Presence:** seus amigos da Steam podem ver o que você faz no jogo (ex.: "Jogando um set, 4-3"), conforme a
  privacidade do seu perfil Steam.

### 1.5 Telemetria (desligada por padrão)

O jogo pergunta uma vez, depois da primeira partida, se você quer guardar e enviar dados de partida. Os dois botões
têm o mesmo peso; "não" é a resposta padrão. Muda quando quiser em **Opções → Privacidade**, onde também dá pra ver
o último registro enviado e os seus identificadores.

- **O que vai (lista fechada):** versão do jogo; sistema (Windows ou Linux, e se é Steam Deck); idioma do jogo;
  número da sessão e da partida; o dia (sem hora); se a sessão anterior fechou com erro, em que tela e depois de
  quanto tempo (em faixas). Por partida: modo, se você hospedou, quantas pessoas jogavam, formato, dificuldade da
  IA, se a assistência de golpe estava ligada, duração (arredondada a 10 s), como terminou pra você (completa, você
  saiu, ou a ligação com quem hospedava caiu — sem dizer de que lado), se venceu, o placar em games da sua partida,
  contagem de golpes por tipo — **só dos jogadores deste computador** — e dos pontos que o seu time perdeu, por
  motivo (rede, fora, dois quiques…) — **só quando ninguém do seu time joga de outro computador**: o jogo conta
  esses pontos por time, sem saber quem errou, e com um parceiro em outro computador a conta levaria os erros
  dele —; se você entrou na sala de alguém, o seu ping até quem hospeda, a perda de pacotes e as correções da bola.
- **Partida ranqueada não gera registro.** O ranking já guarda o resultado com o seu SteamID; um registro da mesma
  partida deixaria ligar o identificador a você.
- **O que nunca vai:** SteamID, nome de ninguém, IP, e-mail, identificador de hardware, localização, texto livre,
  caminho de arquivo, hora exata, nada atribuído a outro jogador (golpes, pontos perdidos, ping ou saída dele).
- **Identificador:** um número aleatório criado quando você liga e trocado a cada 30 dias. O jogo guarda os usados
  nos últimos 90 dias (o tempo que o servidor guarda os registros) e mostra todos em Opções → Privacidade.
- **Desligar** para a coleta na hora, apaga o que a telemetria guardava no computador e pede ao servidor pra apagar
  os registros feitos com todos esses identificadores. Sem internet, o jogo repete o pedido a cada início até o
  servidor confirmar, e só então esquece os identificadores; Opções → Privacidade mostra se o pedido está pendente
  ou confirmado.
- **O servidor** recebe o seu IP pela conexão (é como a internet funciona) e não grava.
- **Retenção:** registros brutos por 90 dias; depois só contagens agregadas por semana, sem identificador e com pelo
  menos 10 registros em cada número guardado.
- **No seu computador, só com a telemetria ligada:** os últimos 50 registros (sessões e partidas), sem
  identificador, pra você anexar num relato de defeito se quiser, e um marcador que o jogo apaga ao fechar
  normalmente (é assim que ele sabe que a sessão anterior caiu). Desligada, o jogo não grava nada disso.

### 1.6 Bases legais

- **Execução de contrato** (LGPD art. 7º, V; GDPR art. 6(1)(b)): preferências, carreira, jogar online, ranking,
  Steam Cloud.
- **Consentimento** (LGPD art. 7º, I; GDPR art. 6(1)(a); na União Europeia também o art. 5(3) da Diretiva
  ePrivacy, porque a telemetria grava no seu dispositivo): telemetria — inclusive o que ela guarda no seu computador
  — e arquivos que você nos manda.
- **Legítimo interesse** (LGPD art. 7º, IX; GDPR art. 6(1)(f)): o registro técnico (log) no seu computador (dúvida
  A7); responder e-mails; apurar denúncia e trapaça e manter a lista de sanções, pra o online ser justo (dúvida
  A11).
- **Obrigação legal** (LGPD art. 7º, II; GDPR art. 6(1)(c)): atender e registrar pedidos sobre seus dados.

### 1.7 Quem recebe

- **Valve Corporation** (EUA): tudo o que é da Steam (tabela 1.2) e as denúncias que repassamos a ela. A Valve trata
  esses dados pela política de privacidade dela (dúvida A5 sobre o papel dela nos recursos que o jogo usa).
- **Os outros jogadores da partida:** seu nome e o que acontece na partida; pela Steam, o seu SteamID (e, por ele, o
  seu perfil Steam, conforme a privacidade dele); na conexão por IP, quem hospeda recebe o seu IP, e quem entra
  guarda o de quem hospeda; no ranqueado, seu nível no ranking.
- **[PROVEDOR DE E-MAIL]:** guarda os e-mails que você manda.
- **[PROVEDOR DO SERVIDOR]:** hospeda o servidor de telemetria (e o do ranking, se for do desenvolvedor).
- **Autoridades:** só com ordem judicial ou obrigação legal.

Ninguém mais. Não vendemos dados.

### 1.8 Transferência internacional

A Steam é contratada por você direto com a Valve, nos EUA. Os dados que o desenvolvedor recebe ficam em [LOCAL DO
SERVIDOR] e no [PROVEDOR DE E-MAIL], que pode guardar fora do Brasil. Se você está no Espaço Econômico Europeu ou no
Reino Unido, o que você nos manda vem pro Brasil, onde o controlador está (dúvida A4 sobre o mecanismo).

### 1.9 Onde ficam os arquivos no seu computador

- Windows: `%APPDATA%\Godot\app_userdata\Padelizou Arena\`
- Linux e Steam Deck: `~/.local/share/godot/app_userdata/Padelizou Arena/`

Desinstalar pela Steam pode não apagar essa pasta. Apagá-la apaga preferências, carreira, perfil, log e o que a
telemetria guardava no computador — mas não o que ela já enviou: pra isso, desligue a telemetria antes (1.10). O
Steam Cloud pode restaurar a carreira e o perfil se estiver ligado.

### 1.10 Seus direitos

Você pode (LGPD art. 18; GDPR arts. 15 a 22): confirmar se tratamos dados seus e acessá-los; corrigir; pedir
anonimização, bloqueio ou eliminação do que for desnecessário ou excessivo; portabilidade; eliminar o que foi
tratado com consentimento; saber com quem compartilhamos; revogar o consentimento; e se opor ao que tratamos por
legítimo interesse.

- **Sozinho, na hora:** desligar a telemetria em Opções → Privacidade (o jogo pede ao servidor pra apagar e mostra
  quando ele confirmou); apagar a pasta da 1.9 — **depois** de desligar a telemetria: com ela ligada, apagar a pasta
  leva embora os identificadores, e aí nem você nem nós achamos os seus registros no servidor, que somem sozinhos em
  até 90 dias; desligar o Steam Cloud pro jogo no cliente da Steam. Dados da conta Steam: com a Valve.
- **Por e-mail:** [EMAIL DE CONTATO], assunto "Privacidade". Diga o que quer e, se for o caso, os identificadores de
  telemetria (Opções → Privacidade mostra todos os dos últimos 90 dias) ou o seu SteamID (ranking, denúncias e
  sanções). Podemos pedir que confirme que a conta é sua. Resposta em até 15 dias, sem custo.
- **Reclamar:** ANPD (Brasil); na UE/EEE, a autoridade de proteção de dados do seu país; no Reino Unido, o ICO.

### 1.11 Crianças e adolescentes

Classificação indicativa: [CLASSIFICAÇÃO INDICATIVA] (dúvida A6). O jogo precisa de conta Steam, e a Steam exige
pelo menos 13 anos pra ter conta. Não pedimos idade nem coletamos dado pra descobri-la. Se você é menor de idade,
fale com um responsável antes de ligar a telemetria ou de nos escrever. Responsável que achar que recebemos dados
de uma criança: escreva pra [EMAIL DE CONTATO] e apagamos.

### 1.12 Segurança e incidentes

Guardamos o mínimo, transmitimos por HTTPS, e só o desenvolvedor acessa o servidor. Se um incidente puder causar
risco ou dano relevante, comunicamos a ANPD e os afetados (LGPD art. 48) — pelos dados de contato que tivermos e,
se não tivermos, pelas notícias do jogo na Steam.

### 1.13 Mudanças

Publicadas no menu do jogo e nas notícias da página da Steam, com data. Mudança que colete dado novo ou use um dado
pra outro fim pede o seu consentimento de novo, antes.

---

## 2. Privacy Policy (EN)

Version 0 — [DATA DE VIGÊNCIA].

**In short:** the game keeps almost everything on your computer. The developer only receives data about you if you
turn on telemetry (off by default; no name, SteamID or IP), play ranked (which needs your SteamID) or write to us —
or if another player sends something you appear in: a report (section 5) or their log, which has the names of the
players in the room and, for a direct IP connection, the host's IP. When playing online through Steam, the other
players in the room receive your name and your SteamID. Your account and payment belong to Valve.

### 2.1 Who is responsible (controller)

[NOME COMPLETO], an individual in [CIDADE/UF], Brazil, developer of Padelizou Arena. Contact for everything in this
policy: **[EMAIL DE CONTATO]**. No data protection officer is appointed — as a small-scale processing agent under
Brazilian rules (ANPD Resolution 2/2022), the e-mail above is the contact channel (open question A2).

Padelizou Arena is separate from the padelizou.com.br website: game data does not go into the website's database
and the website account is not linked to the game. If that ever changes, this policy changes **first**, and linking
will be optional.

### 2.2 What the game processes

| What | Where it is kept | Who sees or receives it | Purpose and legal basis | How long |
|---|---|---|---|---|
| **Preferences**: difficulty, match format, shot mode, handedness, volume, the name you choose, the last room address you typed (for a direct IP connection, that is the host's IP — someone else's data) | `configuracao.json` on your computer; **not** in Steam Cloud | Only you | Remember your choices — performance of contract (the host's address: legitimate interest, so you do not type it again) | Until you delete it |
| **Career and profile**: stages, teams, results, circuit ranking, achievements, statistics | `carreira.json` and profile files on your computer; Steam Cloud | Only you (and Valve) | Keep your progress — performance of contract | Until you delete it |
| **Technical log**: version, errors, scores, the names of the players in the room, the address and port of the room you joined by IP (the host's IP) and, in error messages, the game folder path (which may contain your OS user name) | `logs/` folder on your computer; not in Steam Cloud | No one — it only leaves if you attach it to a bug report (it is plain text: you can read it and delete lines first) | Writing it: bug diagnosis — legitimate interest, without leaving your computer (open question A7). Sending it: only by your own action (consent) | The game keeps the 5 most recent |
| **Online match**: your in-game name (sent to everyone in the room); your controller input — direction and buttons (sent to the host); through Steam, your SteamID (lobby and connection); for a direct IP connection, your IP | Memory of the computers in the room, during the match. Ping is measured and shown only on your computer | The other players in the room; through your SteamID they can reach your Steam profile, as far as its privacy settings allow | Playing the match — performance of contract | Not kept after the match (except the log and preferences above) |
| **Ranked** (once it exists): SteamID, Padelímetro rating, number of matches, results | [DECIDIR: Steam leaderboard or developer server] | Public: the ranking shows name and rating | Running the ranking — performance of contract | [DECIDIR] |
| **Telemetry** (only if you turn it on) — see 2.5 | Developer server in [LOCAL DO SERVIDOR]; on your computer, the summary of the latest records, the send queue, the identifiers and the open-session marker | The developer (server); only you (what stays on your computer) | Balancing the game and finding bugs — consent | Server: raw records 90 days, then only anonymous aggregate counts. Computer: last 50 records; deleted when you opt out (2.5) |
| **E-mails**: support and privacy requests, with anything attached (log, screenshot, video — may include other players' names and, for a direct IP connection, the host's IP) | [EMAIL DE CONTATO] mailbox ([PROVEDOR DE E-MAIL]) | Only the developer | Answering — legitimate interest; handling rights requests — legal obligation | 12 months after the last message |
| **Reports and penalties**: from the reporter, their e-mail and what they sent; about the reported player, the name shown on screen, the SteamID (if any), screenshots and videos; the decision, the reason and the length of the penalty | [EMAIL DE CONTATO] mailbox and the developer's penalty list | Only the developer; Valve, when a report is forwarded to it | Keeping online play fair and answering appeals — legitimate interest (open question A11) | Report without penalty: 12 months after it is closed. With a penalty: the report, the evidence and the list entry are kept while the penalty lasts plus 12 months (the appeal period) |
| **Steam account**: purchase, payment, SteamID, profile name, friends, achievements, Steam Cloud, lobby, connection relay, Rich Presence, Remote Play | Valve | Valve (and, in the lobby, the other players in the room — see "Online match") | Valve's privacy policy. The game decides the Rich Presence text and what goes into the lobby (2.4) | Valve's policy |

Online name: [DECIDIR: the name typed in the game (up to 16 characters) or your Steam profile name]. Length of each
penalty (section 5): [DECIDIR].

### 2.3 What the game does not collect

Real name, e-mail, phone, tax ID, address, payment data (Valve handles it), location, contacts, voice or text (the
game has no chat), hardware or advertising identifiers. The game stores no one's IP, with one exception: for a
direct IP connection (2.4), the address you type to join — the host's IP — stays in your preferences and in your
log (table 2.2). No ads, no selling or sharing of data, no marketing profiles.

### 2.4 Playing online

- **Through Steam (default):** the connection goes through Valve's relay network (Steam Datagram Relay) and other
  players do not see your IP. But the room is a Steam lobby: everyone in it receives each other's SteamIDs and,
  through them, can reach the Steam profile (name, avatar and whatever else the profile's privacy allows) — even if
  the in-game name is different. One player hosts the match: they receive your input and send everyone the game
  state and the names in the room. None of it is kept after the match, except in the technical log (table 2.2).
- **Direct IP connection** (local network and test builds): the host sees the IP of whoever joins, and whoever joins
  types the host's IP, which stays in the joiner's preferences and log (table 2.2). Only use it with people you
  know.
- **Rich Presence:** your Steam friends may see what you are doing in the game (e.g. "Playing a set, 4-3"),
  depending on your Steam profile privacy.

### 2.5 Telemetry (off by default)

The game asks once, after your first match, whether you want to keep and send match data. Both buttons carry the
same weight; "no" is the default. Change it any time in **Options → Privacy**, where you can also see the last
record sent and your identifiers.

- **What is sent (closed list):** game version; system (Windows or Linux, and whether it is a Steam Deck); game
  language; session and match number; the day (no time); whether the previous session closed with an error, on
  which screen and after how long (in ranges). Per match: mode, whether you hosted, how many people played, format,
  AI difficulty, whether shot assist was on, duration (rounded to 10 s), how it ended for you (completed, you left,
  or the connection to the host dropped — without saying on which side), whether you won, your match's score in
  games, number of shots by type — **only for the players on this computer** — and of the points your team lost,
  by reason (net, out, double bounce…) — **only when nobody on your team plays from another computer**: the game
  counts these points per team, without knowing who made the error, and with a partner on another computer the
  count would include their errors —; if you joined someone's room, your ping to the host, packet loss and ball
  corrections.
- **Ranked matches produce no record.** The ranking already keeps the result with your SteamID; a record of the same
  match would allow linking the identifier to you.
- **Never sent:** SteamID, anyone's name, IP, e-mail, hardware identifiers, location, free text, file paths, exact
  time, anything attributed to another player (their shots, points lost, ping or leaving).
- **Identifier:** a random number created when you opt in and replaced every 30 days. The game keeps those used in
  the last 90 days (as long as the server keeps records) and shows all of them in Options → Privacy.
- **Opting out** stops collection at once, deletes what telemetry kept on your computer and asks the server to
  delete the records made with all those identifiers. Without internet, the game repeats the request at every launch
  until the server confirms, and only then forgets the identifiers; Options → Privacy shows whether the request is
  pending or confirmed.
- **The server** receives your IP through the connection (that is how the internet works) and does not store it.
- **Retention:** raw records for 90 days; after that only weekly aggregate counts, without identifier and with at
  least 10 records behind each stored number.
- **On your computer, only while telemetry is on:** the last 50 records (sessions and matches), without identifier,
  so you can attach them to a bug report if you want, and a marker the game deletes when it closes normally (that is
  how it knows the previous session crashed). When off, the game stores none of this.

### 2.6 Legal bases

- **Performance of contract** (LGPD art. 7, V; GDPR art. 6(1)(b)): preferences, career, online play, ranking,
  Steam Cloud.
- **Consent** (LGPD art. 7, I; GDPR art. 6(1)(a); in the EU also art. 5(3) of the ePrivacy Directive, since
  telemetry stores data on your device): telemetry — including what it keeps on your computer — and files you send
  us.
- **Legitimate interest** (LGPD art. 7, IX; GDPR art. 6(1)(f)): the technical log on your computer (open question
  A7); answering e-mails; investigating reports and cheating and keeping the penalty list, to keep online play fair
  (open question A11).
- **Legal obligation** (LGPD art. 7, II; GDPR art. 6(1)(c)): handling and recording requests about your data.

### 2.7 Who receives data

- **Valve Corporation** (USA): everything that belongs to Steam (table 2.2) and the reports we forward to it, under
  Valve's own privacy policy (open question A5 on Valve's role in the features the game uses).
- **Other players in the match:** your name and what happens in the match; through Steam, your SteamID (and, through
  it, your Steam profile, as its privacy settings allow); for a direct IP connection, the host receives your IP and
  whoever joins keeps the host's; in ranked, your rating.
- **[PROVEDOR DE E-MAIL]:** stores the e-mails you send.
- **[PROVEDOR DO SERVIDOR]:** hosts the telemetry server (and the ranking server, if it is the developer's).
- **Authorities:** only under court order or legal obligation.

No one else. We do not sell data.

### 2.8 International transfers

You contract Steam directly with Valve, in the USA. Data the developer receives is stored in [LOCAL DO SERVIDOR] and
with [PROVEDOR DE E-MAIL], which may store it outside Brazil. If you are in the European Economic Area or the United
Kingdom, what you send us comes to Brazil, where the controller is (open question A4 on the legal mechanism).

### 2.9 Where the files are on your computer

- Windows: `%APPDATA%\Godot\app_userdata\Padelizou Arena\`
- Linux and Steam Deck: `~/.local/share/godot/app_userdata/Padelizou Arena/`

Uninstalling through Steam may not delete this folder. Deleting it removes preferences, career, profile, log and
what telemetry kept on your computer — but not what it already sent: for that, turn telemetry off first (2.10).
Steam Cloud may restore career and profile if enabled.

### 2.10 Your rights

You may (LGPD art. 18; GDPR arts. 15–22): confirm whether we process your data and access it; correct it; ask for
anonymisation, blocking or deletion of anything unnecessary or excessive; portability; delete what was processed
based on consent; know who we share it with; withdraw consent; and object to processing based on legitimate
interest.

- **Yourself, right away:** turn telemetry off in Options → Privacy (the game asks the server to delete and shows
  when it confirmed); delete the folder in 2.9 — **after** turning telemetry off: with it on, deleting the folder
  takes the identifiers with it, and then neither you nor we can find your records on the server, which disappear on
  their own within 90 days; turn Steam Cloud off for the game in the Steam client. Steam account data: with Valve.
- **By e-mail:** [EMAIL DE CONTATO], subject "Privacy". Say what you want and, if relevant, your telemetry
  identifiers (Options → Privacy shows all of those from the last 90 days) or your SteamID (ranking, reports and
  penalties). We may ask you to confirm the account is yours. Answer within 15 days, free of charge.
- **Complaints:** ANPD (Brazil); in the EU/EEA, your country's data protection authority; in the UK, the ICO.

### 2.11 Children and teenagers

Age rating: [CLASSIFICAÇÃO INDICATIVA] (open question A6). The game needs a Steam account, and Steam requires users
to be at least 13. We do not ask for age or collect data to infer it. If you are a minor, talk to a parent or
guardian before turning telemetry on or writing to us. A parent who believes we received a child's data: write to
[EMAIL DE CONTATO] and we will delete it.

### 2.12 Security and incidents

We keep the minimum, transmit over HTTPS, and only the developer accesses the server. If an incident may cause
relevant risk or harm, we notify the ANPD and those affected (LGPD art. 48) — using any contact details we have and,
failing that, through the game's news on Steam.

### 2.13 Changes

Published in the game menu and in the Steam page news, with a date. Any change that collects new data or uses data
for a new purpose asks for your consent again, beforehand.

---

## 3. Contrato de licença de usuário final — EULA (PT-BR)

Versão 0 — [DATA DE VIGÊNCIA]. Entre você e [NOME COMPLETO], pessoa física, [CIDADE/UF], Brasil ("desenvolvedor").
Vale junto com o Acordo de Assinatura do Steam; no que for da Steam (conta, compra, biblioteca), vale o da Valve.

1. **Licença.** Ao comprar pela Steam você recebe uma licença pessoal, não exclusiva e intransferível pra jogar o
   Padelizou Arena, sem fim comercial, nos dispositivos que a sua conta Steam permitir. O jogo, o código, a arte e
   as marcas continuam do desenvolvedor e dos licenciantes.
2. **Pode:** jogar; gravar, transmitir e publicar vídeo e imagem do jogo, inclusive monetizados, desde que não diga
   que é oficial ou patrocinado; modificar o jogo pra uso próprio **fora do online** (sem suporte, por sua conta).
3. **Não pode:**
   a. trapacear no online: alterar o jogo, a memória, os arquivos ou os pacotes de rede pra ter vantagem; usar
      programa de automação (bot, macro, o piloto automático de teste) em partida online contra pessoas;
   b. fazer engenharia reversa, descompilar ou modificar o cliente ou o protocolo de rede pra trapacear, atacar ou
      derrubar partidas — ressalvado o que a lei garante mesmo contra contrato (dúvida A10);
   c. atacar outros jogadores ou quem hospeda (sobrecarga, tentar descobrir IP, derrubar conexão);
   d. combinar resultado, usar contas extras ou explorar defeito de propósito pra mexer no ranking;
   e. vender, alugar ou sublicenciar o jogo fora da Steam;
   f. usar nome ofensivo ou se passar por outra pessoa (seção 5).
4. **Consequências.** Quem descumprir o item 3 pode ter resultados removidos, o nível zerado ou ser excluído do
   ranqueado, e ser denunciado à Valve. A compra e a biblioteca são da Valve e não mudam por decisão nossa. Você
   pode contestar pelo [EMAIL DE CONTATO].
5. **Acesso Antecipado.** O jogo está em desenvolvimento: funções mudam, entram e saem. Uma carreira salva pode não
   abrir numa versão nova (o jogo avisa e oferece recomeçar) e o ranking pode ser zerado entre temporadas. O online
   depende da Steam e de um jogador hospedar; não há servidor dedicado. Se o online for encerrado, avisamos com pelo
   menos 90 dias de antecedência [DECIDIR o prazo].
6. **Garantia e responsabilidade.** O jogo é fornecido no estado em que se encontra, na medida máxima que a lei
   permite. Nada aqui tira direitos que a lei do consumidor garante (no Brasil, o Código de Defesa do Consumidor; na
   UE e no Reino Unido, as regras de conformidade de conteúdo digital). Fora o que a lei impõe, não respondemos por
   falhas da Steam, da sua internet ou de outros jogadores.
7. **Reembolso.** Pela política de reembolso da Steam.
8. **Componentes de terceiros.** O jogo usa software de código aberto (entre eles o Godot Engine e o
   Facepunch.Steamworks, ambos sob licença MIT), listado com as licenças na tela de Créditos. As licenças deles
   valem pra eles.
9. **Mudanças neste contrato.** Avisadas no jogo; mudança relevante pede novo aceite no início seguinte. Se não
   aceitar, você pode deixar de usar o jogo.
10. **Lei e foro.** Leis do Brasil. Se você é consumidor no Brasil, pode entrar na Justiça no seu domicílio (CDC,
    art. 101, I). Se mora em outro país, continua com as proteções obrigatórias do consumidor de lá. Nos demais
    casos, foro da comarca de [CIDADE/UF] (dúvida A9).
11. **Contato:** [EMAIL DE CONTATO].

---

## 4. End User License Agreement — EULA (EN)

Version 0 — [DATA DE VIGÊNCIA]. Between you and [NOME COMPLETO], an individual in [CIDADE/UF], Brazil ("developer").
It applies together with the Steam Subscriber Agreement; for anything that belongs to Steam (account, purchase,
library), Valve's agreement prevails.

1. **License.** Buying through Steam gives you a personal, non-exclusive, non-transferable license to play Padelizou
   Arena, for non-commercial use, on the devices your Steam account allows. The game, code, art and trademarks
   remain the property of the developer and its licensors.
2. **You may:** play; record, stream and publish video and images of the game, including monetized, as long as you
   do not claim it is official or sponsored; modify the game for your own use **outside online play** (unsupported,
   at your own risk).
3. **You may not:**
   a. cheat online: alter the game, memory, files or network packets to gain an advantage; use automation software
      (bots, macros, the test autopilot) in online matches against people;
   b. reverse engineer, decompile or modify the client or the network protocol to cheat, attack or crash matches —
      except where the law grants that right regardless of contract (open question A10);
   c. attack other players or the host (flooding, trying to find IPs, dropping connections);
   d. fix results, use extra accounts or deliberately exploit bugs to manipulate the ranking;
   e. sell, rent or sublicense the game outside Steam;
   f. use an offensive name or impersonate someone else (section 5).
4. **Consequences.** Breaking item 3 may lead to removal of results, rating reset or exclusion from ranked play, and
   a report to Valve. Your purchase and library belong to Valve and do not change by our decision. You may contest
   at [EMAIL DE CONTATO].
5. **Early Access.** The game is in development: features change, arrive and leave. A saved career may not open in
   a new version (the game warns you and offers to start over) and the ranking may be reset between seasons. Online
   play depends on Steam and on a player hosting; there are no dedicated servers. If online play is shut down, we
   will give at least 90 days' notice [DECIDIR o prazo].
6. **Warranty and liability.** The game is provided "as is", to the maximum extent permitted by law. Nothing here
   removes rights granted by consumer law (in Brazil, the Consumer Defense Code; in the EU and UK, the rules on
   conformity of digital content). Except where the law requires otherwise, we are not liable for failures of
   Steam, your internet connection or other players.
7. **Refunds.** Under Steam's refund policy.
8. **Third-party components.** The game uses open-source software (including Godot Engine and Facepunch.Steamworks,
   both MIT-licensed), listed with their licenses on the Credits screen. Their licenses apply to them.
9. **Changes to this agreement.** Announced in the game; relevant changes ask for new acceptance at the next launch.
   If you do not accept, you may stop using the game.
10. **Governing law and venue.** Brazilian law. If you live in another country, you keep the mandatory consumer
    protections of that country. For consumers in Brazil, the courts of their domicile (Consumer Defense Code,
    art. 101, I); otherwise, the courts of [CIDADE/UF] (open question A9).
11. **Contact:** [EMAIL DE CONTATO].

---

## 5. Conduta no online / Online conduct

O jogo não tem chat: o que um jogador mostra aos outros é o **nome** e o **jeito de jogar**. As regras são sobre isso.

**Nome.** Proibido: ofensa, discriminação, conteúdo sexual, ameaça; dado pessoal seu ou de outros (nome completo,
telefone, @ de rede social); link ou propaganda; se passar por outra pessoa — jogador profissional, "Padelizou",
"Admin", "Moderador".

**Jogo.** Proibido: trapaça (EULA 3.a a 3.c); abandonar de propósito ou, hospedando, derrubar a partida que está
perdendo; combinar resultado ou subir com conta extra (EULA 3.d).

**Como denunciar.**
1. **Perfil da Steam** (nome, avatar, mensagem no chat da Steam): botão "Denunciar" no perfil da pessoa, no cliente
   da Steam — quem age é a Valve. O cliente da Steam também deixa bloquear.
2. **No jogo** (trapaça, nome ofensivo, abandono): e-mail pra [EMAIL DE CONTATO], assunto "Denúncia", com dia e hora
   aproximados, modo, o nome que apareceu na tela, o que aconteceu e, se tiver, print ou vídeo.
3. **Ameaça, crime ou risco a criança:** polícia; no Brasil, também a Central de Denúncias da SaferNet Brasil.

**O que acontece.** Toda denúncia é lida. Sem servidor dedicado, a prova é o que você manda — sem prova, o normal é
não punir. Sanções possíveis: aviso, remover resultado, zerar o nível, excluir do ranqueado, denunciar à Valve;
cada uma com prazo ([DECIDIR]). Quem denunciou não é identificado pra quem foi denunciado. Quem foi punido pode
contestar pelo mesmo e-mail e pedir acesso ao que guardamos sobre ele, sem a identidade de quem denunciou. O que
guardamos e por quanto tempo: política, 1.2, linha "Denúncias e sanções".

**EN.** The game has no chat: what a player shows others is their **name** and **how they play**. **Names** must not
be offensive, discriminatory, sexual or threatening; must not contain personal data (yours or anyone's), links or
ads; must not impersonate anyone (pro players, "Padelizou", "Admin", "Moderator"). **Play:** no cheating (EULA
3.a–3.c), no deliberate quitting or, as host, dropping a match you are losing, no result fixing or smurf accounts
(EULA 3.d). **To report:** Steam profile issues (name, avatar, Steam chat) through "Report" on the Steam profile —
Valve acts, and the Steam client lets you block; in-game issues by e-mail to [EMAIL DE CONTATO], subject "Report",
with approximate day and time, mode, the name shown, what happened and any screenshot or video; threats, crimes or
risk to a child: the police (in Brazil, also SaferNet Brasil's reporting center). **What happens:** every report is
read; without dedicated servers, the evidence is what you send — no evidence usually means no penalty. Possible
penalties: warning, result removal, rating reset, exclusion from ranked, report to Valve; each with a set length
([DECIDIR]). Reporters are not identified to the reported player, who may contest by the same e-mail and ask for
access to what we keep about them, without the reporter's identity. What we keep and for how long: privacy policy,
2.2, row "Reports and penalties".

---

## 6. O que precisa de advogado antes de publicar

Cada item é uma dúvida real, não formalidade. O orçamento prevê R$ 1.000–3.000 de jurídico (CRONOGRAMA.md); esta
lista é o escopo dessa consulta.

| # | Dúvida | Por que importa |
|---|---|---|
| A1 | Revisão dos dois textos, nas duas línguas; qual versão prevalece; se o aceite vale pelo EULA do Steamworks, por tela no primeiro início, ou pelos dois | Aceite mal feito = EULA que não vincula |
| A2 | Controlador pessoa física: precisa publicar endereço (GDPR art. 13(1)(a) pede "identidade e contatos")? A dispensa de encarregado da Res. CD/ANPD nº 2/2022 vale aqui? Publicar como empresa muda o quê? (com contador) | Expõe o endereço residencial do Felipe; muda o item A8 |
| A3 | Representante na UE e no Reino Unido (GDPR e UK GDPR, art. 27): cabe a exceção do art. 27(2) (tratamento ocasional e de baixo risco)? | Jogo à venda na Europa com online contínuo pode não ser "ocasional" |
| A4 | Transferência internacional: coleta direta de jogador do EEE/UK por controlador no Brasil é "transferência" do Cap. V do GDPR? (EDPB, Diretrizes 05/2021, diz que não — confirmar); adequação UE–Brasil na data; LGPD art. 33 e Res. CD/ANPD nº 19/2024 pro provedor de e-mail e pro servidor se ficarem fora do Brasil | Define o que a seção 1.8 pode afirmar |
| A5 | Papel da Valve nos recursos Steamworks que o jogo configura (Cloud, conquistas, placares, estatísticas): controladora independente ou operadora? O que o Steam Distribution Agreement exige do desenvolvedor sobre dados de usuário e SteamID? | Se for operadora, a política tem de dizer outra coisa |
| A6 | Crianças e adolescentes: classificação indicativa pra vender no Brasil pela Steam (Portaria MJSP nº 502/2021 — conferir se é a vigente — e como a Steam coleta isso); **ECA Digital (Lei nº 15.211/2025)**: o jogo tem interação online e é de acesso provável por adolescentes — quais obrigações valem (supervisão parental, configuração padrão protetiva; o jogo não tem caixa de recompensa); idade de consentimento pra telemetria (GDPR art. 8, 13 a 16 anos conforme o país; LGPD art. 14 e Enunciado CD/ANPD nº 1/2023) | A lei nova é a que mais pode mudar o produto |
| A7 | Telemetria e log. (a) O identificador aleatório por instalação é dado pessoal (LGPD arts. 12 e 13; pseudônimo no GDPR)? O desenho de [`TELEMETRIA.md`](TELEMETRIA.md) assume que sim e pede opt-in pra tudo da telemetria, inclusive o que fica só no computador (resumo local e marcador de queda); revisar o texto da tela de consentimento. (b) O log do Godot, gravado sempre no computador do jogador (sem identificador; só sai por ato dele; traz os nomes da sala e, na conexão por IP, o IP de quem hospeda), cabe em legítimo interesse e na exceção de "estritamente necessário" do art. 5(3) da ePrivacy, ou pede consentimento? (c) Reidentificação: se o ranking morar no servidor do desenvolvedor, o mesmo controlador guarda resultados por SteamID. O desenho tira a partida ranqueada da telemetria e só guarda agregado com 10+ registros por número; basta, ou o risco que sobra (ex.: queda no meio de um ranqueado num dia de pouco movimento) pede mais? (d) Registros cujo identificador o jogador perdeu (apagou a pasta antes de desligar): o GDPR art. 11 dispensa manter informação pra identificar, e o prazo de 90 dias resolve? | Se o identificador não for dado pessoal, o opt-in continua certo pelo ePrivacy, mas a política pode ficar mais simples; se o log precisar de consentimento, o diagnóstico de defeito muda; se o cruzamento com o ranking não estiver resolvido, a telemetria não liga enquanto o ranking for do desenvolvedor |
| A8 | Marco Civil da Internet (Lei nº 12.965/2014), art. 15: se quem publica virar pessoa jurídica, guardar registro de acesso (IP, data e hora) por 6 meses pode valer pro servidor de telemetria e de ranking — e contradiz "o servidor não grava IP" (1.5) | Decidir antes de ligar o servidor |
| A9 | EULA x consumidor: CDC (foro, art. 101, I; cláusulas abusivas, art. 51; limite de responsabilidade; arrependimento do art. 49 em compra digital — discussão aberta, e a Steam tem a política dela); na UE/UK, Diretiva (UE) 2019/770 e Roma I, art. 6; validade da escolha de lei e foro | Cláusula nula derruba a confiança no resto |
| A10 | Engenharia reversa: redação compatível com a Lei nº 9.609/1998 (art. 6º) e a Diretiva 2009/24/CE (arts. 5º, 6º e 8º) | O contrato não pode proibir o que a lei garante |
| A11 | Sanções no online: base pra excluir do ranqueado sem servidor e sem prova técnica; como fica a contestação; base e prazo pra manter a lista de sanções e a prova; como informar quem foi denunciado (GDPR art. 14) sem expor quem denunciou | Evita punição que vira processo |
| A12 | Propriedade intelectual e imagem: registro do nome definitivo (INPI; EUIPO se a Europa vender); cessão de imagem e de performance do atleta do mocap; clubes e patrocinadores reais nos painéis (autorização escrita); nomes de jogadores reais no circuito (Código Civil, arts. 16 a 20); licenças de música, fonte e asset cobrem venda em jogo comercial; "Premier Padel" e FIP são marcas de terceiros — só referência interna | Qualquer um desses tira o jogo da loja |
| A13 | Incidente de segurança: roteiro mínimo e prazos (LGPD art. 48 e Res. CD/ANPD nº 15/2024; GDPR arts. 33 e 34, 72 h) | Prazo curto; melhor ter o roteiro antes |
| A14 | Integração com o Padelizou (pós-Early Access: ranking cruzado, perfil compartilhado, jogador real): liga o jogo a uma pessoa identificada — revisão completa **antes** de qualquer código | Muda a política inteira |

---

## 7. O que o código precisa cumprir pra este texto ser verdade

Política que promete o que o código não faz é pior que política nenhuma. Fora a C6, nenhuma linha abaixo está
cumprida hoje; cada uma é pré-requisito de publicar o texto.

| # | Promessa | O que falta |
|---|---|---|
| C1 | Telemetria (1.5): opt-in, pergunta depois da primeira partida, "não" padrão; nada gravado antes do aceite; partida ranqueada fora; golpes só dos jogadores deste computador e pontos perdidos só quando o time não tem humano de outro computador (TELEMETRIA.md, seção 2); Opções → Privacidade (ver os identificadores dos últimos 90 dias, desligar, ver último registro, estado do pedido de apagar); pedido de apagar repetido até o servidor confirmar | Tudo — desenho em [`TELEMETRIA.md`](TELEMETRIA.md) |
| C2 | Log sem o nome dos outros jogadores e sem o IP de quem hospeda (hoje a política descreve o log como ele é; o desejável é menos) | `Padel.Godot/scripts/Sessao/SessaoHost.cs` escreve os nomes da sala ("Rede: partida iniciada com … na sala (…)"); `Padel.Godot/scripts/PartidaNode.cs` escreve "cliente de IP:porta" na linha "partida criada". No build de release, trocar por contagem e por "cliente por IP", e tirar os dois das tabelas 1.2 e 2.2, da 1.3 e do "Em resumo" |
| C3 | "Os outros jogadores não veem o seu IP" (1.4) | No transporte da Steam, configurar o P2P pra não abrir conexão direta com IP público (opção de transporte ICE das Steam Networking Sockets) e conferir com dois PCs em redes diferentes |
| C4 | Conexão por IP avisada (1.4) | Aviso na tela antes de hospedar ou entrar por IP; ou deixar só pra rede local no release |
| C5 | Automação proibida no online (EULA 3.a) | Build de release recusa humano simulado (`--bot`, `--auto`) em partida ranqueada |
| C6 | Caminhos da 1.9 e 2.9 | Corretos hoje (padrão do Godot, sem `application/config/use_custom_user_dir` no `project.godot`). Se ligar essa opção, atualizar as duas seções |
| C7 | Política e EULA acessíveis | Tela "Privacidade e termos" no menu; campos de política e EULA preenchidos no Steamworks |
| C8 | Créditos com licenças (EULA 8) | Tela de Créditos com os avisos do Godot e dos componentes dele, do Facepunch.Steamworks e dos assets comprados |
| C9 | Nome no online (1.2 e seção 5) | [DECIDIR] nome digitado ou nome da Steam. Se digitado (hoje: 16 caracteres no jogo, até 32 aceitos pelo host, sem filtro), no mínimo um filtro de palavras e a opção "esconder o nome dos outros". Nos dois casos o SteamID vai pelo lobby (1.4) |
| C10 | Servidor que "não grava IP", apaga em 90 dias e atende o pedido de apagar (1.5) | Rota de telemetria sem log de acesso com IP no Caddy; dados fora do backup externo do padelizou.com.br; apagamento aos 90 dias por agendamento; o `esquecer` apaga antes de responder `204` e anota só data e quantidade |
| C11 | "O jogo avisa e oferece recomeçar" (EULA 5) | **Hoje não avisa, e apaga.** Com `carreira.json` de outra versão, `EstadoDaCarreira.Carregar` devolve `false` (o aviso vai só pro log), `CarreiraNode._Ready` chama `Nova(...)` na hora, e `Nova` salva por cima do arquivo antigo. Falta: aviso na tela, escolha do jogador e cópia do arquivo antigo antes de recomeçar |
| C12 | Steam Cloud só com carreira e perfil (1.2, 1.9; [`STEAM.md`](STEAM.md)) | Configurar no Steamworks (Auto-Cloud) só `carreira.json` e os arquivos do perfil — nunca `configuracao.json` (tem o IP de quem hospeda), `logs/` nem `telemetria/` |
