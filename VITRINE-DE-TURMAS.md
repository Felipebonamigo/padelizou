# Vitrine de turmas, pedido de entrada e feed — design para aprovação

> **Status: DESENHO, nada codado.** Gera migration, cria vitrine sobre um grupo hoje fechado por
> construção e (na segunda entrega) cria papel de acesso novo. Pelo critério do `CLAUDE.md` isso é
> `architectural` três vezes — e `architectural` é design escrito e aprovado ANTES de qualquer código.
>
> **As 6 pendências foram fechadas em 01/09/2026**, delegadas por ele — ver a seção final. Três
> mudaram depois de conferir o código, e uma delas (a FK) evitava uma migration que não compila.
>
> **Sucede o `RESERVAS-DA-TURMA.md`**, que recomendava o Caminho B. O Felipe escolheu o **Caminho A**
> em 01/09/2026: a lista de reservas pertence à TURMA. O `JogadorDisponivel` do Caminho B **não será
> construído** — ver "A troca de B para A", abaixo.

---

## De onde veio

Duas fontes, no mesmo dia (31/08–01/09/2026):

**O Rafael Paim**, em cinco cards: preencher vaga · lista de reservas da turma · social/feed
esportivo ("baseado em atividade, não rede social genérica") · visibilidade de turmas externas ·
compartilhamento no WhatsApp. E a frase que resume o diagnóstico dele:

> *"A ideia é que o Padelizou funcione como um meio de chegar em jogos, aulas e torneios. E se só
> vejo os grupos que eu já faço parte não tenho muita margem. Na prática, jogar em grupos que já
> existem é mais fácil do que montar jogo aleatório."*

**O Felipe**, em quatro linhas: grupos visíveis com cara de feed · tela "Meus grupos / Todos os
grupos" (com membros, data, local e hora) filtrável por clube e cidade · "pedir pra participar" ·
o dono aceita ou recusa.

---

## Decisões já tomadas (01/09/2026) — a régua deste documento

| # | Decisão do Felipe | Consequência no desenho |
|---|---|---|
| 1 | **Caminho A** | A reserva é da turma, não do horário. `JogadorDisponivel` engavetado |
| 2 | **Mostra os nomes dos membros** | O card lista membros com `ComoChamar`. Revoga, aqui, a régua de "roster não vaza" de 25/08 |
| 3 | **`Listado` nasce ligado; `AceitaPedidos` nasce desligado** | Todo grupo existente entra na vitrine no primeiro deploy |
| 4 | **Recusado é permanente até o dono desfazer** | Tela de recusados, com desfazer |
| 5 | **Dono + admins respondem** — e o dono pode nomear admin | Papel de acesso novo. **Entrega separada** |
| 6 | **Sem valores no card** | `ValorAvulso`/`ValorMensalidade` ficam fora |
| 7 | **Medir antes de decidir o feed** | Consulta em produção é pré-requisito da fase 3 |

**Continua valendo, de 25/08/2026:** *o sistema sugere a um membro chamar — nunca chama sozinho.*
Ninguém entra numa turma sem alguém de dentro ter aprovado. Todo fluxo aqui obedece a isso.

---

## A troca de B para A, escrita por extenso

O `RESERVAS-DA-TURMA.md` recomendava o **Caminho B** (`JogadorDisponivel`: o jogador declara
"topo jogar dia X" e toda turma daquele dia o vê) **porque o A exigia vitrine, e vitrine era uma
decisão de produto não tomada**. O mesmo documento previu a saída:

> *"O Caminho A continua possível depois, em cima do B, se a vitrine de turmas virar prioridade
> por outro motivo."*

O pedido do Felipe **é** esse outro motivo. Com a vitrine paga pelo pedido principal, o custo
marginal do A cai para uma tabela — e ele é melhor para o usuário: um pedido de entrada é
**declaração única** ("quero jogar com essa turma"), enquanto o B exigiria declarar disponibilidade
data a data, toda semana, para sempre. O próprio doc anterior já registrava essa fraqueza.

⚠️ **`JogadorDisponivel` não se constrói.** Fica engavetado, e só volta se aparecer demanda por
"topo jogar dia X" **sem** vínculo com turma nenhuma. Construir os dois é ter duas listas de
candidatos que envelhecem em ritmos diferentes na mesma tela `Convidar`.

---

## O que JÁ existe (e por isso não se reimplementa)

- **`GrupoPrivado` já tem tudo que a vitrine mostra**: `Nome`, `ClubeId`, `DiaSemanaFixo`,
  `HorarioFixo`, `CategoriaPadraoId`, `VagasMaximas`. A vitrine é projeção + filtro, não estrutura.
- **`Clube.CidadeId`** dá o filtro por cidade sem coluna nova; **`CidadesSemRepetir.Agrupar`** já
  resolve "Gravataí" × "gravatai" × "Gravatai " (usado em `MarcarJogoController`).
- **A tela `Convidar`** já faz o matching que o card 1 do Rafael pede (categoria, clube, dia+período,
  `AceitaConvitesJogo`) e o `ConvidarJogador` já grava a `ConfirmacaoSessao`, manda push e devolve o
  `wa.me`. **Nada disso muda.**
- **`EntrarNoGrupo`** já sabe criar `JogadorGrupo` com pontuação zero.
- **`PushNotificationService.EnviarParaJogadorAsync`** enfileira sem I/O na requisição; o alcance
  `SoApp` é o padrão (`AlcanceDoAviso`).
- **O perfil do jogador já é público, inclusive sem login** — nome, categoria, títulos, ranking.

---

## Modelo de visibilidade

### Quem decide
O **dono** do grupo, em `Configuracoes`. Dois interruptores independentes:

- **`Listado`** — a turma aparece na vitrine. **Nasce ligado** (decisão 3).
- **`AceitaPedidos`** — a turma recebe pedidos de entrada. **Nasce desligado.**

Separados de propósito: turma cheia pode ser achável ("existimos, jogamos terça no OK") sem abrir
fila. Ligar um não liga o outro.

### O que o card mostra
Nome · clube e cidade · dia da semana e horário fixos · categoria · **nomes dos membros**
(`ComoChamar`, com link para o perfil público) · nº de membros · jogos nos últimos 30 dias
(diferencia turma viva de turma morta) · se aceita pedidos.

⚠️ **Nomes usam `ComoChamar`, nunca `SoOApelido`.** A decisão de 26/08 é que apelido é linguagem
*interna* da turma — fora dela vale a forma pública, pela razão de 06/08: *"'Zeca' pode ser três
pessoas no mesmo torneio"*. Numa vitrine com dezenas de turmas isso é pior, não melhor.

### O que NUNCA aparece
- **`CodigoConvite`** — é **credencial**: quem tem, entra. Vitrine com código é porta sem fechadura.
- **Ranking interno e pontuação**, jogos com quem jogou, **presenças** (inclusive o estado "o admin
  me tirou"), mensalidades pagas/não pagas.
- **`ValorAvulso` / `ValorMensalidade`** (decisão 6).
- **CPF e celular** — nunca chegaram a tela nenhuma, e continuam assim (`ContatoDoJogador`).

### O que a decisão 2 realmente expõe — registrado para não virar surpresa
O perfil de cada jogador **já é público hoje**, então mostrar o nome não revela uma identidade nova.
O que passa a ser público é o **vínculo**: *fulano joga com sicrano, nesse clube, terça às 20h*.
Isso é o padrão semanal de localização de pessoas físicas, e não existe hoje em lugar nenhum do
sistema. Vale para os membros de todas as turmas que já existem, que entraram quando o grupo era
fechado por construção. **É decisão do Felipe, tomada com o efeito na mesa** — o que este documento
faz é registrá-lo e cercá-lo com o que segue:

- A vitrine fica atrás de **`[Authorize]`** — nada anônimo. O `GruposController` inteiro já é assim.
- **Todo dono é avisado** no deploy (ver "Avisos"), com o link direto para desligar.
- **Um membro pode sair da vitrine sem sair da turma**: `JogadorGrupo.OcultoNaVitrine` (bool, nasce
  `false`). É a única concessão que este desenho faz sem custo de tela — um botão em `Detalhes`, e o
  nome vira só a contagem. Sem isso, a única saída de quem não quer aparecer é abandonar a panelinha.

⚠️ **O gate de Acesso Antecipado não é autorização** (Regra 0). Cada POST novo aqui tem checagem
real — nada pode quebrar no dia em que o portão abrir.

---

## Dados

### Entrega 1 — uma migration

```
GrupoPrivado
  + Listado        bool NOT NULL  DEFAULT true    -- ⚠️ defaultValue: true ESCRITO À MÃO
  + AceitaPedidos  bool NOT NULL  DEFAULT false

JogadorGrupo
  + OcultoNaVitrine bool NOT NULL DEFAULT false

PedidoDeEntrada
  GrupoId       int    PK, FK -> GrupoPrivado, ON DELETE CASCADE
  JogadorId     int    PK, FK -> Jogador,      ON DELETE RESTRICT
  Status        int    -- 0 Pendente · 1 Aceito · 2 Recusado · 3 Reserva
  CriadoEm      timestamp
  DecididoEm    timestamp?
  DecididoPorId int?   FK -> Jogador,          ON DELETE RESTRICT
```

⚠️ **As duas FKs de `Jogador` são `Restrict`, e isso não é escolha de gosto.** `GrupoPrivado` já
cascateia a partir de `Jogador` (pelo `AdministradorId`), então um segundo caminho direto de
`Jogador` até uma tabela filha do grupo é o **conflito de múltiplos caminhos de cascade** — o mesmo
que `JogadorGrupo`, `JogoSemanal`, `CandidaturaParceiro` e `PalpitePartida` já resolveram assim, com
o motivo escrito no `DbPadelContext`. `GrupoId` em `Cascade`, como em `JogadorGrupo`: apagou o grupo,
o pedido não sobrevive.

⚠️ **`defaultValue: true` precisa estar escrito à mão na migration.** Coluna `bool` nova do EF nasce
`false` no banco; o `= true` em C# só vale para objeto NOVO. Sem isso, todos os grupos que já existem
nascem `Listado = false` e a vitrine abre **vazia** — o oposto exato da decisão 3. Essa lição já foi
paga uma vez neste projeto, em `Clube.Selecionavel`, e está escrita lá no comentário.

⚠️ **PK composta (`GrupoId`, `JogadorId`)** é o anti-duplicata: clique duplo e re-pedido morrem no
banco, sem um `if` em C#. Mesmo truque de `TorneioMarcador` (degrau 4 da escada do `CLAUDE.md`).

⚠️ **Uma tabela, dois papéis.** `Status = Reserva` **é** a lista de reservas do card 2 do Rafael. Não
existe `ReservaDaTurma` — seria uma segunda tabela para o mesmo par (grupo, jogador), com duas
verdades possíveis sobre a mesma pessoa.

⚠️ **`Recusado` não some** (decisão 4): a linha fica, a PK barra o novo pedido, e a tela de recusados
deixa o dono desfazer. Desfazer = apagar a linha, que reabre a porta.

### Entrega 2 — admins do grupo (migration própria)

```
AdministradorDoGrupo
  GrupoId    int PK, FK -> GrupoPrivado
  JogadorId  int PK, FK -> Jogador
  CriadoEm   timestamp
```

`GrupoPrivado.AdministradorId` **continua existindo** e continua sendo o dono — quem cria o grupo,
quem nomeia admins, e o único que não pode ser removido. Admin é acréscimo, não substituição.

### Feed — zero tabela
Se existir (fase 3), é **read-model**: uma consulta que projeta eventos que o sistema já grava.
Nenhuma tabela de eventos, nenhum job de fan-out. Apagar o feed é apagar uma action.

---

## Fluxos

**1. A vitrine.** `/Grupos` ganha duas abas: **Meus grupos** (a tela de hoje, intocada) e **Todos os
grupos** — action nova `GruposController.Vitrine`, com filtro por cidade e clube. Só grupos
`Listado`. Cartão como descrito acima.

**2. Pedir pra participar.** POST `PedirParaParticipar` grava `PedidoDeEntrada` (Pendente) e avisa
dono e admins por `EnviarParaJogadorAsync` com alcance **`SoApp`** — poucos destinatários, ação
clara. O botão só aparece (e o POST só aceita) se `AceitaPedidos` e o solicitante não é membro.
**Sem campo de mensagem livre**: texto de estranho para o dono é superfície de assédio sem fluxo de
denúncia, e o link do perfil já dá todo o contexto (categoria, histórico, cidade).

**3. O dono decide.** Seção "Pedidos" em `Detalhes`, visível a dono e admins, com os três botões que
o Rafael pediu:

- **Aceitar** → cria `JogadorGrupo` direto, com `PontuacaoInterna = 0`, pela mesma lógica do
  `EntrarNoGrupo`. ⚠️ **Nunca enviando o link `Entrar?codigo=`**: o código é credencial permanente e
  repassável — aceitar um pedido não pode entregar a chave da porta a quem estava fora. Push ao
  aceito (`SoApp`).
- **Recusar** → `Status = Recusado`, **em silêncio** (mesma filosofia do deixar de seguir, que já é
  silencioso desde 10/08). A pessoa vê o estado em "Meus pedidos".
- **Reserva** → `Status = Reserva`. Não avisa ninguém ainda.

**4. A reserva vira jogo.** Na tela `Convidar` que já existe, quem é `Reserva` daquele grupo sobe ao
topo com selo "reserva da turma", **sem furar os filtros de categoria/clube/dia**. Quem chama
continua sendo um membro, pelo `ConvidarJogador` de sempre. A régua de 25/08 fica intacta: o sistema
sugere, uma pessoa chama.

**5. O lado de quem pede.** Seção "Meus pedidos" em `/Grupos`: estado de cada pedido (pendente desde
X · você é reserva de Y) e botão de **cancelar o próprio pedido**. Sem isso, quem pede — que é o
usuário que motivou tudo — fica sem nenhuma tela e sem saída.

**6. Recusados.** Aba na `Detalhes` (dono e admins) listando quem foi recusado, com **desfazer**.

---

## Autorização (Regra 0)

| Ação | Verbo | Quem pode | Como checa |
|---|---|---|---|
| `Vitrine` | GET | qualquer logado | `[Authorize]` de classe; consulta filtra `Listado` |
| `PedirParaParticipar` | POST | logado, **não-membro** | `[Authorize]` + antiforgery + `grupo.AceitaPedidos` + `!JogadoresGrupo.AnyAsync(...)`; PK composta barra duplicata |
| `CancelarMeuPedido` | POST | o próprio solicitante | `pedido.JogadorId == userId` |
| `AceitarPedido` / `RecusarPedido` / `MandarParaReserva` / `DesfazerRecusa` | POST | dono (entrega 1); dono ou admin (entrega 2) | `EhAdminDoGrupoAsync(grupoId, userId)` |
| `Listado` / `AceitaPedidos` | POST | dono | `grupo.AdministradorId == userId`, padrão de `Configuracoes` |
| `OcultarMeuNomeNaVitrine` | POST | o próprio membro | `JogadorGrupo` do par (grupo, usuário) |
| `AdicionarAdmin` / `RemoverAdmin` (entrega 2) | POST | **só o dono** | `grupo.AdministradorId == userId` — admin não nomeia admin |
| Chamar reserva pro jogo | POST `ConvidarJogador` | membro | inalterado |

O `GateDeAutorizacaoDosPostsTests` pega o `[Authorize]` sozinho. **A checagem de dono não tem gate
mecânico** — é julgamento, e é trabalho do teste desta área: cada POST acima ganha um teste de 403
para quem não é dono/admin.

⚠️ **A entrega 2 cria a quinta régua de autorização do projeto.** Hoje `AdministradorId` é checado em
**13 pontos** do `GruposController` mais 1 view. Todos passam a chamar um único
`EhAdminDoGrupoAsync` — a dessincronia entre réguas já quebrou a Mesa de Controle em 31/07, e as
quatro atuais (`EhOrganizadorAsync`, `UsuarioEhOrganizadorAsync`, `PodeControlarPlacarAsync`,
`PodeOperarODiaDeJogoAsync`) estão no `CLAUDE.md` justamente por isso. Pelo `ONDAS-PARALELAS.md`,
régua de autorização é **sempre tarefa sozinha** — daí as duas entregas serem dois PRs.

---

## Avisos

| Evento | Quem recebe | Alcance |
|---|---|---|
| Pedido novo | dono + admins | `SoApp` |
| Pedido aceito | o solicitante | `SoApp` |
| Pedido recusado | **ninguém** | — |
| Virou reserva | **ninguém** até ser chamado | — |
| Chamado pro jogo | o convidado | inalterado (`ConvidarJogador`) |
| **Turma entrou na vitrine** | **só os donos**, 1x no deploy | `SoApp` |

⚠️ **O aviso do deploy vai só para os DONOS, não para a base inteira.** São poucas dezenas de
destinatários, e o aviso é acionável ("sua turma aparece na vitrine; desligue aqui se não quiser").
Avisar todos os membros seria push para a base inteira num deploy — exatamente o que o comentário em
`GrupoPrivado.EnviarLembrete24h` recusou fazer em 21/08, e o que estourou a cota de e-mail em 09/08.
A régua daquele dia decide: *"a pessoa faz alguma coisa por causa do aviso?"* — o dono faz, o membro
não.

---

## Testes que precisam existir (Regra 1: escritos antes, e vistos falhar)

1. Grupo que já existia nasce `Listado = true` **depois da migration** — o teste que prova o
   `defaultValue` à mão, não o `= true` do C#.
2. Grupo com `Listado = false` não aparece na vitrine.
3. Pedido duplicado é recusado **pelo banco**, não por `if`.
4. Não-membro com `AceitaPedidos = false` recebe 403 no POST (não só botão escondido).
5. Membro não consegue pedir para entrar no próprio grupo.
6. Não-dono recebe 403 em aceitar / recusar / reserva / desfazer.
7. Aceitar cria `JogadorGrupo` com pontuação 0 **e não expõe `CodigoConvite` em lugar nenhum** da
   resposta — o teste que trava o buraco.
8. Recusado não consegue pedir de novo; depois do desfazer, consegue.
9. Cancelar pedido alheio dá 403.
10. Membro com `OcultoNaVitrine` não aparece na lista de nomes, mas continua na contagem.
11. A vitrine nunca traz `CodigoConvite`, pontuação, presença ou valores na projeção.
12. Reserva sobe ao topo do `Convidar` sem furar o filtro de categoria/clube/dia.
13. **Tradução da consulta para o Postgres (`ToQueryString()`)** — a vitrine atravessa
    `GrupoPrivado → Clube → Cidade` e agrega jogos por janela de data; o InMemory não valida SQL, e
    isso é a receita exata do estouro de 19/08. Padrão: `TraducaoDasConsultasDePalpiteTests`.
14. (Entrega 2) Admin responde pedido; admin **não** nomeia outro admin; remover admin não remove
    o membro do grupo.
15. Pedido de conta anonimizada (`ExcluidoEm != null`) não aparece na fila do dono **nem** sobe como
    reserva no `Convidar` — a conta é anonimizada e não apagada, então a linha continua lá e só o
    filtro a esconde.

---

## Entregas

**PR 1 — Vitrine + pedido de entrada.** Migration única (2 colunas em `GrupoPrivado`, 1 em
`JogadorGrupo`, tabela `PedidoDeEntrada`), aba "Todos os grupos" com filtro por cidade/clube, os três
botões do dono, "Meus pedidos", recusados com desfazer, reserva no topo do `Convidar`, aviso aos
donos no deploy. Fecha os cards 1 (com o matching que já existe), 2 e 4 do Rafael.

**PR 2 — Admins do grupo.** `AdministradorDoGrupo` + `EhAdminDoGrupoAsync` substituindo as 13
checagens. Sozinho, sem nada em paralelo.

**PR 3 — WhatsApp (card 5).** Botão `wa.me` com texto + link ao lado dos cartões de resultado e
ranking da turma, que hoje só existem como imagem, sem link. Barato e independente dos outros dois.
⚠️ Enquanto o Acesso Antecipado estiver fechado, quem clica de fora bate no portão: isso é
"mostrar onde já se conversa", **não** canal de aquisição. Vira aquisição no dia em que o gate abrir.

**Fase 4 — feed, só depois de medir** (decisão 7). A objeção de 25/08 ("feed de atividade sem
atividade abre vazio") foi medida sobre **partidas de ranking**; o feed que o Rafael descreve é
agregador de outra coisa — avisos de jogo, raquete livre, aulas, turmas que abriram vaga, torneios.
Antes de desenhar, contar em produção quantos desses eventos entram por semana. O número decide, e
fica escrito no design como gatilho, não como promessa.

**Critério de sucesso do PR 1**, observável e sem query manual: **um pedido real aceito numa turma
real** (Sub 90 ou Pinel Gravataí). Se em duas semanas não houver nenhum, o problema não é o próximo
recurso — é a descoberta da própria funcionalidade.

---

## O que NÃO está aqui, de propósito

- **Comentar no feed.** Multiplica moderação por item × dia, para um operador solo. Texto livre já
  tem casa no `ComentarioPerfil`, com denúncia e fluxo de moderação prontos.
- **Reagir.** Só faz sentido depois que o feed existir e tiver o que reagir. Se vier, é catálogo
  fixo como `CatalogoElogios`, 1 por pessoa por item (PK composta) — nunca texto livre.
- **Mensagem no pedido de entrada.** Ver fluxo 2.
- **Chamada automática do primeiro da reserva.** Vetada em 25/08 e não se discute aqui.
- **Vitrine sem login.** O `GruposController` inteiro exige login; abrir uma porta anônima é decisão
  estrutural que só faz sentido junto com a abertura do Acesso Antecipado.
- **`JogadorDisponivel`.** Engavetado com a escolha do Caminho A.

---

## Decisões fechadas (delegadas a mim em 01/09/2026)

**1. `OcultoNaVitrine` entra no PR 1 — sim.** É um `bool` e um botão. Fora do PR 1 ele não existe
quando a vitrine liga, e a única saída de quem não quer o vínculo público vira abandonar a turma —
que é perder um membro para não perder a privacidade. Entra junto ou não vale.

**2. A fila de pedidos é vista só por dono e admins.** Quem pede escolheu se expor ao *dono*, não à
turma inteira; e o recusado, a ninguém. Consequência que fica registrada: quando um `Reserva` sobe ao
topo da tela `Convidar`, **todos os membros veem que aquela pessoa é reserva** — isso é inerente ao
recurso (alguém precisa chamar), e é o único vazamento aceito, porque é o que faz a reserva servir
para algo. Pendente e recusado nunca aparecem ali.

**3. FK: `GrupoId` Cascade, `JogadorId` Restrict.** Corrigido no bloco de dados acima — minha
recomendação anterior ("Cascade") estava certa só para a metade do grupo. `Cascade` nas duas quebra a
migration pelo conflito de múltiplos caminhos, que o `DbPadelContext` já documenta em quatro tabelas.

**4. Conta excluída: nada é apagado, e o filtro resolve.** A pergunta partia de uma premissa errada
minha — **`ExclusaoDeConta` anonimiza, não deleta** (é o único caminho correto: `Pagamento` é
registro fiscal do MEI, `Dupla` recusa apagar quem já jogou, e o resultado de uma partida é dado de
quatro pessoas). Então não há linha órfã e não existe expurgo a escrever. O que precisa existir é
**um filtro `ExcluidoEm == null` na leitura dos pedidos**, exatamente como o
`LembreteJogoBackgroundService` já filtra. Sem ele, a fila do dono enche de "Jogador removido"
pendente, e uma reserva de conta encerrada apareceria como chamável no `Convidar`. Vira o teste 15.

**5. Denúncia de nome de grupo: não entra agora — e o motivo está no código.** Eu havia recomendado
"reusar `/Admin/Denuncias`", mas fui conferir: aquela tela é **específica de `ComentarioPerfil`**
(`AdminController.Denuncias` consulta comentários com `DenunciadoEm != null`). Reusar significa
generalizá-la para um segundo tipo de item — trabalho real disfarçado de reuso, o degrau 2 aplicado
errado. O que vale hoje: a base é beta fechado, o Felipe conhece os donos, e **desligar `Listado`
daquele grupo pelo `/Admin` já é o botão de moderação** — um `UPDATE` numa coluna que o PR 1 cria de
qualquer jeito. **Gatilho para construir a denúncia de verdade:** o dia em que o Acesso Antecipado
abrir, ou a vitrine passar de ~50 turmas — o que vier primeiro. Fica escrito aqui para a sessão
futura não tratar isso como esquecimento.

**6. Ordem: quem aceita pedidos primeiro, depois por jogos nos últimos 30 dias, depois por nome.**
Os dois primeiros critérios põem no topo o que responde à pergunta do Rafael ("onde eu consigo
entrar, e a turma está viva?"); o terceiro existe só para a ordem não mudar de um F5 para o outro
quando os dois primeiros empatam — que é o mesmo desempate que os jogos recentes da `Detalhes` já
precisaram fazer pelo `Id`. **Sem paginação na v1**, com o comentário nomeando o teto:
`// atalho: vitrine sem paginação; paginar acima de ~200 turmas`.

---

## O que continua realmente em aberto

Nada bloqueia o PR 1. Duas coisas dependem de dado que só produção tem, e estão marcadas como
gatilho, não como pendência: **o volume de eventos por semana** (decide o feed, decisão 7) e o
**critério de sucesso de duas semanas** — um pedido real aceito numa turma real.
