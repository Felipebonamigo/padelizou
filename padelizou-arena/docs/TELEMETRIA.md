# Telemetria mínima

> **Proposta pro M5 (CRONOGRAMA.md: "duração de partida, abandono, ping, golpe mais usado — sem dado pessoal").
> Nada disto está implementado.** A política de privacidade em [`LEGAL.md`](LEGAL.md) (seções 1.5 e 2.5) resume
> este documento, e o revisor cruza os dois: **campo novo, prazo novo ou destino novo muda os dois na mesma
> alteração**, e sobe o número do esquema.

## As decisões, em oito linhas

1. **Opt-in**, desligada por padrão; pergunta uma vez, depois da primeira partida (seção 5). Antes do aceite, nada
   de telemetria é gravado — nem no computador.
2. **Lista fechada**: três eventos e um de controle (seção 3). O servidor recusa campo que não está nela.
3. **Nada que aponte pra uma pessoa**: sem SteamID, nome, IP gravado, hardware, local, texto livre, hora exata — e
   **sem partida ranqueada**, que o ranking guarda com o SteamID e deixaria cruzar (seção 2).
4. **Identificador aleatório por instalação**, criado no aceite, trocado a cada 30 dias; os dos últimos 90 dias
   ficam à vista do jogador, pra pedir a eliminação (seção 6).
5. **Só deste computador**: nada atribuído a outro jogador do online — nem golpes, nem pontos perdidos, nem ping,
   nem quem saiu. Quem hospeda não manda dado de rede, porque o único que tem é a conexão dos outros; e os pontos
   perdidos, que o jogo só conta por time, não vão quando o time tem humano de outro computador (seção 2).
6. **Onde**: com aceite, arquivo local (pro relato de defeito) e servidor próprio (seção 7).
7. **Retenção**: brutos 90 dias; depois só contagens por semana, com 10+ registros por número (seções 8 e 9).
8. **Desligar apaga de verdade**: o pedido ao servidor se repete até ele confirmar (seção 6).

## 1. Pra que serve: cada pergunta, a decisão que ela muda, e os campos que a respondem

Campo que não responde uma pergunta desta tabela não entra.

| Pergunta | Decisão que muda | Campos |
|---|---|---|
| A taxa de queda é < 0,5 % por sessão? | O "pronto" do M5 — libera ou segura o Early Access (junto com as outras fontes, abaixo) | `sessao`, `sessao_anterior_caiu` |
| Quanto dura uma partida em cada formato e modo? | Formato padrão (set único x melhor de 3, ponto de ouro); tamanho das etapas da carreira | `partida.duracao_s`, `sets_para_vencer`, `ponto_de_ouro`, `modo` |
| Quem sai no meio, quando e em que situação? | Penalidade de abandono no ranqueado (que o próprio ranking mede; aqui entra o resto); se a reconexão de 30 s (M2) vale o trabalho | `partida.fim` (só o de quem manda), `games` até ali, `papel` |
| O jogo é justo a 150 ms? Quem hospeda vence mais? | D2 (host autoritativo) segura até o 1.0 ou o servidor dedicado sobe de prioridade | `partida.rede.*` (só de quem entrou na sala), `papel`, `venceu` |
| Qual golpe mais se usa e qual mais erra? | Balanceamento dos golpes (víbora forte demais? chiquita que ninguém usa?) e o tutorial | `partida.golpes`, `pontos_perdidos_por` |
| O humano vence quanto em cada dificuldade? | Curva da IA e da carreira | `dificuldade_ia`, `venceu`, `games` |
| Quantos jogam com o golpe automático? | Padrão do modo de golpe; se o tutorial ensina o timing | `modo_de_golpe` |
| Linux/Deck caem mais que Windows? | Prioridade de correção por plataforma; verificação do Deck | `so`, `deck` (no envelope) |

**Fora daqui:** "o ranqueado está equilibrado por faixa?" se responde com o próprio ranking, que já tem resultado e
nível de cada partida — não com a telemetria (seção 2).

**Como ler os números.** A amostra é só de quem aceitou: é tendência, não censo — cruzar com os relatos. E a taxa
de queda precisa de volume: 0,5 % de 2.000 sessões são 10 quedas, e o intervalo de 95 % de 10 eventos vai de ~5 a
~18 (0,24 % a 0,92 %). Regra: o número da telemetria só conta a favor quando o **limite superior** do intervalo
ficar abaixo de 0,5 % (por exemplo, 15 quedas em 5.000 sessões: 0,17 % a 0,49 %).

**A taxa de queda da telemetria não é teto nem piso.** Conta a mais o que fecha sem passar pela saída normal:
processo morto pelo sistema, falta de luz. Conta a menos — e é o lado que pesa — quem cai antes de aceitar (a
pergunta vem depois da primeira partida, então a queda no primeiro início, por driver, shader ou placa sem suporte,
nunca entra) e quem cai e não abre mais o jogo (o evento só sai na sessão seguinte). São justo as quedas que mais
viram reembolso. Adiantar a pergunta não resolve: a queda antes do menu continua de fora. Por isso o "< 0,5 %" do M5
exige as três coisas:

1. o limite superior do intervalo de 95 % da telemetria abaixo de 0,5 %;
2. o primeiro início do build candidato conferido à mão em cada alvo (Windows, Linux, Steam Deck e uma placa antiga
   com o mínimo dos requisitos) — o que a telemetria não enxerga;
3. nenhum sinal de queda no início nos motivos de reembolso e nas avaliações da Steam (conferir no painel do
   Steamworks o que ele mostra) nem nos relatos.

Um sinal em (2) ou (3) segura o M5, seja qual for o número de (1).

## 2. Nunca entra

| Dado | Por quê |
|---|---|
| SteamID, nome de qualquer jogador, e-mail | Identifica a pessoa. O ranking já tem o SteamID onde precisa; a telemetria não |
| IP | O servidor o recebe na conexão e não grava (seção 7) |
| Modelo de GPU/CPU, resolução, identificador de hardware ou de publicidade | Combinados, viram impressão digital da máquina |
| Hora exata, fuso, região, relay da Steam usado | Localizam a pessoa. O dia (UTC, sem hora) basta pra separar versões |
| Pilha de erro (stack trace), caminho de arquivo, texto de log | Trazem `C:\Users\<nome>\…`, os nomes da sala e, na conexão por IP, o IP de quem hospeda (`PartidaNode.cs` loga "cliente de IP:porta"). Quem quiser mandar o log anexa à mão |
| Texto livre de qualquer tipo | Não dá pra garantir o que o jogador escreve |
| Qualquer coisa atribuída a **outro** jogador do online: golpes, pontos perdidos, ping, conexão, se ele saiu | Não consentiram. Por isso quem hospeda não manda `rede`: o único ping que o host tem é o dos outros (`TransporteEnet.PingDoEnetMs` é a ida e volta até o primeiro cliente, e `ServidorDaPartida.EntradasDe` descreve como chegam as entradas de cada cliente). E `fim` só diz o que aconteceu com quem manda, e `pontos_perdidos_por` vai `null` quando o time tem humano de outro computador (abaixo) |
| Partida ranqueada, e com ela a faixa do Padelímetro | O ranking guarda cada resultado com o SteamID. Dia, placar, duração e papel de uma partida ranqueada acham a mesma partida no ranking e ligam o `id_instalacao` a um SteamID — com os 30 dias de registros daquele identificador. O equilíbrio por faixa se mede no próprio ranking |

**O que entra que envolve os outros, e por quê.** O placar (`games`), `venceu` e `humanos_total` descrevem a
partida de quem manda, não uma pessoa: não dá pra dizer "perdi de 4-6" sem o 6, e nenhum deles diz quem estava do
outro lado. `fim: conexao_perdida` diz que a ligação acabou, não de que lado. Fora do ranqueado, nenhum outro
registro do desenvolvedor tem essas partidas com nome ou SteamID.

**`pontos_perdidos_por` não é só o resultado: conta como o time perdeu cada ponto (na maioria, erros de quem
jogava), e o jogo só sabe isso por time.** O árbitro dá o ponto a um time, sem jogador
(`Decisao(Tipo, Para, Motivo)`, em `Arbitro.cs`), e `Partida.EncerrarPonto` emite o evento de ponto só com `Time` e
`Motivo` — no online, o `EventoNumerado` chega ao cliente com `Jogador = -1`. Adivinhar quem errou pelo último golpe
não fecha: em `DoisQuiques`, quem do time não chegou na bola? Com um parceiro humano em outro computador, a conta do
time levaria os erros dele. Por isso o campo vai **`null` quando o time deste computador começou a partida com um
humano de outro computador**; com o parceiro na mesma tela ou IA, vai (a IA não é pessoa). Olhar o início basta:
ninguém entra depois de a partida começar (`ServidorDaPartida` recusa com `PartidaEmAndamento`), e quem sai só vira
IA. No online cada computador tem um jogador só (`SessaoHost` e `SessaoCliente`), então o campo vai no 1x1 e no 2x2
com parceiro IA, e fica `null` no 2x2 com parceiro humano. O cliente sabe pelo `ClienteDaPartida.Humanos` (vem no
`Comecou`) na vaga do parceiro, `Indice ^ 1`; o host, pelas `Opcoes.Humanos` da partida.

**Considerado e deixado de fora (volta se uma pergunta da seção 1 pedir):** fps e preset gráfico (os requisitos
de sistema se medem com o build candidato, não com a base inteira), quadra escolhida, tempo em menu, etapa e
categoria da carreira.

## 3. A lista fechada de eventos

Todo lote vai com um **envelope**; cada evento vai dentro dele. Chaves de `golpes` são os nomes de `TipoDeGolpe`
(`Padel.Core/Jogadores.cs`); as de `pontos_perdidos_por`, os de `Motivo` (`Padel.Core/Arbitro.cs`). Renomear um
desses enums é mudar o esquema.

**Envelope**

| Campo | Valores | Por quê |
|---|---|---|
| `esquema` | inteiro (hoje `1`) | O servidor recusa esquema que não conhece |
| `id_instalacao` | 32 hex aleatórios | Seção 6 |
| `versao` | "0.9.3" | Tudo se compara por versão |
| `so` | `windows` \| `linux` | Queda por plataforma |
| `deck` | booleano | O Deck é o Linux que mais importa |
| `idioma` | `pt-BR` \| `en` \| `es` (o do jogo, não o do sistema) | Onde a tradução falha; de onde vêm os abandonos |

**`sessao`** — a cada início do jogo: `dia` (AAAA-MM-DD, UTC), `seq` (número da sessão desde o identificador atual).

**`sessao_anterior_caiu`** — no início, se a sessão anterior não fechou limpa. Com a telemetria ligada, o jogo grava
um marcador (`user://telemetria/sessao-aberta`, com versão e tela) ao abrir e ao trocar de tela, e apaga ao sair
normalmente. Conta a mais (processo morto pelo sistema, falta de luz) e a menos (queda antes do aceite, queda de quem
não volta): não é teto — ver "Como ler os números", na seção 1.

| Campo | Valores |
|---|---|
| `dia`, `seq` | da sessão que caiu |
| `versao_que_caiu` | a versão daquela sessão |
| `onde` | `menu` \| `partida` \| `carreira` \| `replay` \| `sala` |
| `minutos` | `0-1` \| `1-5` \| `5-15` \| `15-60` \| `60+` |

**`partida`** — ao fim de toda partida **não ranqueada** com pelo menos um humano **neste computador** (IA x IA e
`--auto` não contam). Partida ranqueada não gera evento nem conta no `n`, pra a lacuna na numeração não denunciar
que ela existiu.

| Campo | Valores |
|---|---|
| `dia`, `seq` | |
| `n` | número da partida dentro da sessão (1, 2, …), contando só as que geram evento |
| `modo` | `local` \| `coop_local` \| `online` \| `carreira` |
| `papel` | `local` \| `host` \| `cliente` |
| `humanos_locais` | 1 \| 2 |
| `humanos_total` | 1 a 4, no início da partida |
| `sets_para_vencer`, `ponto_de_ouro` | 1 \| 2; booleano |
| `dificuldade_ia` | `Facil` \| `Medio` \| `Dificil` \| `null` (sem IA) |
| `modo_de_golpe` | `Manual` \| `Automatico` |
| `duracao_s` | inteiro, arredondado a 10 s, 0 a 14.400 |
| `fim` | `completa` \| `eu_sai` \| `conexao_perdida` (só `cliente`: a ligação com quem hospeda acabou antes do fim, de qualquer lado). No online, quem sai vira IA e a partida segue (`ServidorDaPartida`), então a saída de outro jogador não encerra nada e não aparece aqui |
| `venceu` | booleano \| `null` (sem resultado) |
| `games` | `[[meu_time, adversario], …]` por set, até onde foi |
| `golpes` | contagem por `TipoDeGolpe`, somando os humanos deste computador |
| `pontos_perdidos_por` | contagem por `Motivo` dos pontos que o time deste computador perdeu (o motivo é do ponto e inclui mérito do adversário, como `VoltouPeloVidro`); `null` se o time começou a partida com humano de outro computador (seção 2) |
| `rede` | só com `papel` = `cliente`, medido neste computador: `ping_p50_ms`, `ping_p95_ms` (ida e volta até quem hospeda, das amostras que o `ClienteDaPartida` já mede; múltiplos de 5), `perda_pct` (instantâneos do host que não chegaram, uma casa), `correcoes_da_bola` (correções do instantâneo acima de um limiar a definir no código). `null` com `papel` = `host` ou `local` |

**`esquecer`** — controle, não medida. Mandado ao desligar a telemetria: `{"esquema":1,"evento":"esquecer","ids":[…]}`
com o identificador atual e os antigos ainda guardados (seção 6). O servidor apaga todo registro bruto com eles e
**só depois** responde `204`; o jogo repete o pedido a cada início até receber o `204`.

## 4. O formato de um lote

Um `POST` HTTPS com JSON, até 32 KB e 100 eventos. Exemplo no tamanho que se espera (sem espaços, ~700 bytes; o
evento `partida` sozinho, ~520):

```json
{
  "esquema": 1,
  "id_instalacao": "3f9c0a7e5b1d4c2e8a6f0b9d7c5e3a1f",
  "versao": "0.9.3",
  "so": "linux",
  "deck": true,
  "idioma": "pt-BR",
  "eventos": [
    { "evento": "sessao", "dia": "2026-11-03", "seq": 14 },
    {
      "evento": "partida", "dia": "2026-11-03", "seq": 14, "n": 1,
      "modo": "online", "papel": "cliente",
      "humanos_locais": 1, "humanos_total": 2,
      "sets_para_vencer": 1, "ponto_de_ouro": true,
      "dificuldade_ia": "Medio", "modo_de_golpe": "Manual",
      "duracao_s": 1270, "fim": "completa", "venceu": false,
      "games": [[4, 6]],
      "golpes": { "Saque": 9, "Normal": 41, "Lob": 12, "Bandeja": 8, "Vibora": 3, "Smash": 2, "Chiquita": 1, "Erro": 5 },
      "pontos_perdidos_por": { "Rede": 6, "Fora": 4, "DoisQuiques": 11 },
      "rede": { "ping_p50_ms": 85, "ping_p95_ms": 140, "perda_pct": 0.8, "correcoes_da_bola": 3 }
    }
  ]
}
```

É um 1x1 online visto por quem entrou na sala: o parceiro é IA, então `pontos_perdidos_por` vai. No 2x2 com quatro
humanos, o mesmo evento iria com `"pontos_perdidos_por": null` (seção 2).

**O servidor valida na entrada** (é fronteira de confiança — qualquer um pode mandar um `POST`): esquema conhecido;
evento na lista; **campo desconhecido recusa o evento inteiro** (é assim que a lista continua fechada); enums e
faixas conferidos; `rede` não nulo fora de `papel` = `cliente` recusa o evento; `pontos_perdidos_por` não nulo com
`modo` = `online` e `humanos_total` = 4 recusa o evento (um jogador por computador: o parceiro é sempre de outro;
com 2 ou 3 humanos depende da vaga, e o servidor não tem como conferir); `dia` entre hoje − 30 e hoje + 1;
evento repetido — mesmo `id_instalacao`, `evento`, `seq` e `n` — é ignorado, pra o reenvio não contar duas vezes.
Limite de taxa por IP **em memória**, nunca em disco. Responde `204` sem corpo. Pros lotes de medida o jogo não
insiste — o que falhou vai de novo na sessão seguinte; o `esquecer` é a exceção (seção 6).

Cada sessão gera o próprio lote, com o envelope daquela sessão: evento que ficou na fila não herda a versão (nem o
identificador) de depois.

## 5. Opt-in, não opt-out — por quê

**Recomendação: opt-in, pra tudo da telemetria** — o envio e o que ela guarda no computador (resumo local e
marcador de queda). Pergunta depois da primeira partida (antes disso a pessoa não sabe o que está emprestando), com
os dois botões do mesmo tamanho e cor, e um "ver o que vai" que mostra o registro da partida que acabou de jogar.
Muda a qualquer hora em Opções → Privacidade. Desligar é tão fácil quanto ligar.

1. **O identificador torna os registros ligáveis entre si.** Isso se parece mais com pseudonimização (LGPD
   art. 13, § 4º) que com anonimização (art. 12); o art. 12, § 2º, ainda trata como pessoal o dado usado pra perfil
   de comportamento de pessoa identificada — aqui não há identificação direta, mas o servidor vê o IP no instante
   do envio. Na dúvida, o desenho assume que **é dado pessoal** e escolhe a base legal com isso em mente (dúvida A7
   do LEGAL.md).
2. **Legítimo interesse (art. 7º, IX, e art. 10) até caberia no Brasil**, com teste de balanceamento documentado e
   opção de sair. Mas não cobre a Europa: o art. 5(3) da Diretiva ePrivacy exige consentimento pra gravar ou ler
   informação no dispositivo do usuário quando não é estritamente necessário pro serviço pedido — e telemetria não
   é. O argumento é sobre **gravar no dispositivo**, não sobre enviar: vale igual pro `local.jsonl` e pro marcador
   `sessao-aberta`, que servem à mesma medida. Por isso, sem aceite, nada disso é gravado. Um fluxo só, igual pra
   todo mundo, é mais simples que um por país.
3. **Consentimento tem regras que o desenho já cumpre** (LGPD art. 8º): finalidade determinada (§ 4º — a tela diz
   "balancear o jogo e achar defeito", não "melhorar a experiência"), revogação gratuita e facilitada (§ 5º), e
   informação clara antes (art. 9º).
4. **Adolescentes jogam isto** (a Steam aceita a partir de 13). O melhor interesse (LGPD art. 14) pesa contra
   coletar por padrão. Opt-in mais um desenho que não identifica é o lado seguro.
5. **O custo é amostra menor.** Aceitável: as decisões da seção 1 pedem tendência, não total. No Playtest fechado
   (200 pessoas) o convite pede explicitamente pra ligar.

O log do Godot (`logs/`), que o jogo grava sempre, é outra coisa: diagnóstico do próprio jogo, não medida, sem
identificador e sem envio. A política o declara com base própria, e se ele cabe na exceção do "estritamente
necessário" é a dúvida A7 (b) do LEGAL.md. É ele — e não a telemetria — o piso do relato de defeito de quem não
aceitou.

## 6. O identificador

- **Criado no aceite**, na camada Godot: 16 bytes de `System.Security.Cryptography.RandomNumberGenerator`, em hex.
  **Nunca no `Padel.Core`** (lá não entra relógio nem sorteio — o replay e o online são determinísticos) e nunca
  derivado de SteamID, máquina ou instalação da Steam.
- **Guardado** em `user://telemetria/id`, com a data de criação. Antes do aceite, nada é gravado.
- **Trocado a cada 30 dias** por outro aleatório; `seq` recomeça. Janela suficiente pra "partidas por instalação por
  semana" e retenção de 7 dias; o que for mais longo que isso, a Steam já mede (jogadores e tempo de jogo).
- **Os antigos ficam até 90 dias depois do último uso** — o tempo dos brutos no servidor — em
  `user://telemetria/ids-antigos`, só pra eliminação. **Opções → Privacidade mostra o atual e os antigos**, com um botão de copiar, pra quem preferir pedir
  por e-mail (LEGAL.md 1.10). Mostrar só o atual alcançaria só os registros dos últimos 30 dias, cerca de um terço
  do que o servidor guarda.
- **Desligar** para a coleta na hora, apaga a fila, o resumo local e o marcador, e manda `esquecer` com o atual e os
  antigos. **A lista só é apagada quando o servidor responde `204`.** Sem internet, o jogo repete o pedido a cada
  início, e Opções → Privacidade mostra "pedido de apagar: pendente" ou "confirmado em DD/MM". Passados 90 dias sem
  confirmação, o servidor já apagou tudo pela retenção, e a lista vai embora sozinha. Religar cria um identificador
  novo.
- **Apagar a pasta do jogo com a telemetria ligada** leva a lista antes do pedido: os registros no servidor ficam sem
  dono conhecido e somem em até 90 dias. A política diz isso ao jogador (LEGAL.md 1.9 e 1.10): desligar antes.
- **O que não volta:** o que já virou contagem agregada sem identificador (seção 8) não se separa por pessoa — e
  não é mais dado de ninguém.
- **Por que ter um**: sem ele não dá pra saber se as quedas são de poucas máquinas ou de todas, nem se os abandonos
  são de poucas pessoas — são decisões diferentes. Alternativa considerada: sem identificador, só um número aleatório
  por lote (pra não contar duas vezes o reenvio). Responde só totais; fica como plano B se o advogado vetar o
  identificador.

## 7. Onde guardar

| Opção | Custo | O que responde | Risco e obrigações | Veredito |
|---|---|---|---|---|
| **Nada** | Zero | Só o que a Steam já dá (vendas, jogadores, tempo de jogo, reembolsos, reviews) e os relatos | Nenhum | Não mede o "pronto" do M5 (taxa de queda por sessão) |
| **Arquivo local anexado no relato de defeito** | ~1 dia | O contexto de cada defeito relatado; nenhuma taxa (só vê quem relata) | Quase nenhum: só sai por ato do jogador; gravar pede o aceite (seção 5) | **Sim, com o mesmo aceite** |
| **Servidor próprio** | Poucos dias + operação; o VPS do padelizou.com.br já está pago | Toda a seção 1 | O IP chega na requisição (não gravar); segurança e incidente; Marco Civil art. 15 se virar empresa (dúvida A8) | **Sim, opt-in, a partir do Playtest do M5** |
| **Estatísticas agregadas da Steam** (`ISteamUserStats`) | Baixo, sem servidor | Totais (golpes por tipo, partidas por modo); sem distribuição, sem ping p95, sem partida a partida | Os números ficam presos ao SteamID na Valve — deixa de ser anônimo | Não pra telemetria; serve pras conquistas |
| SDK de analytics de terceiro | Baixo | Tudo | Pacote novo, terceiro recebendo, transferência internacional, costuma coletar ID de dispositivo | Descartado |

**Recomendação.**

- **Arquivo local, com aceite** (sem identificador): `user://telemetria/local.jsonl` com os últimos 50 eventos. A
  tela de relato de defeito mostra o arquivo e deixa anexar. Não sai sozinho. Sem aceite, o relato leva só o log do
  Godot, se o jogador anexar.
- **Servidor próprio, com aceite:** um processo pequeno e **separado do app do padelizou.com.br**, atrás do mesmo
  Caddy, que valida (seção 4) e acrescenta cada evento num arquivo JSONL por dia — sem banco. Os cuidados que a
  política promete (LEGAL.md, C10): a rota não grava log de acesso com IP; os arquivos **ficam fora** do backup
  externo do padelizou.com.br (B2 e Google Drive guardariam além dos 90 dias e fora do país); um agendamento apaga o
  que passou de 90 dias; o `esquecer` apaga antes de responder e anota só data e quantidade.
- **Se o servidor não ficar pronto até a semana 39**, o M5 sai só com o arquivo local — a pergunta de aceite diz só
  "guardar neste computador", e pergunta de novo quando o servidor chegar, porque enviar é finalidade nova (LEGAL.md
  1.13). O "pronto quando" de queda vira estimativa pelos relatos e pelas fontes (2) e (3) da seção 1 — escrito como
  estimativa, não como número.

Volume: ~720 bytes por partida no pior caso (todos os tipos de golpe e de motivo preenchidos, três sets). Com 10 mil
jogadores por dia, 3 partidas cada e todos com telemetria ligada (o teto), são ~22 MB por dia e ~2 GB nos 90 dias.
Cabe no VPS.

## 8. Agregação

Uma vez por dia, um script lê os JSONL e soma em contadores **sem identificador**, por **semana** × versão. Cada
contador responde uma pergunta da seção 1 e cruza só os campos dela (ex.: duração por `modo` × `sets_para_vencer`
× `ponto_de_ouro`; vitórias por `dificuldade_ia`; quedas por `so` × `deck`) — nunca o cruzamento de todos, que no
Playtest (200 pessoas) deixaria célula com uma pessoa só. Conta: sessões, quedas, partidas, saídas por tipo de
`fim`, golpes por tipo, pontos perdidos por motivo, e histogramas de duração e de ping (faixas fixas). **Célula com
menos de 10 eventos vai pra "outros" antes de gravar**, não só na hora de publicar. As perguntas por instalação
(quedas concentradas? abandonos repetidos?) se respondem nos brutos, dentro dos 90 dias.

Só o agregado montado assim fica sem prazo.

## 9. Retenção

| O quê | Onde | Quanto tempo |
|---|---|---|
| Fila de envio | Computador do jogador (só com aceite) | Até ir; descartada após 30 dias sem conseguir enviar, ou ao desligar |
| Resumo local e marcador de sessão aberta | Computador do jogador (só com aceite) | Últimos 50 eventos; o marcador some ao fechar normalmente; tudo apagado ao desligar |
| Identificador atual | Computador do jogador | 30 dias; ao desligar, vai pra lista dos antigos |
| Identificadores antigos (pro `esquecer` e pro pedido por e-mail) | Computador do jogador | 90 dias depois do último uso; depois de desligar, até o servidor confirmar o `esquecer` (no máximo 90 dias) |
| Registros brutos | Servidor | 90 dias, ou até um `esquecer` / pedido por e-mail |
| Anotação de cada `esquecer` | Servidor | Só data e quantidade apagada, sem identificador |
| Agregados sem identificador | Servidor | Indeterminado, se montados como na seção 8 |
| IP | — | Não gravado |

90 dias cobrem o Playtest (6 semanas), um ciclo de balanceamento e o patch seguinte, e ainda deixam comparar
antes e depois.

## 10. Onde mora no código

- **`Padel.Core`** só expõe contadores que já existem ou são pura função da partida (golpes por tipo, motivo do
  ponto, placar). Não sabe o que é telemetria, não lê relógio, não sorteia nada.
- **`Padel.Godot`** monta o evento no fim da partida, grava o arquivo local, cuida do aceite, do identificador e do
  envio (fora da thread do jogo, uma vez por sessão, no início, com o que ficou pendente).
- O servidor é um projeto à parte (fora do `Padel.Core`, que não referencia nada além da BCL).

## Pendências

- Implementar tudo (LEGAL.md C1 e C10). Até lá, a política não pode ir pro ar com a seção de telemetria — ou vai sem
  ela.
- No cliente: guardar as amostras de ida e volta (hoje o `ClienteDaPartida` só expõe a média suavizada `Ping`),
  contar os instantâneos que não chegaram e definir o limiar de `correcoes_da_bola` (quanto a bola "pula" pra
  contar).
- O advogado responder A7 e A8 do LEGAL.md antes de ligar o servidor.
