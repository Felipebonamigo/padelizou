# Lista de reservas — design para aprovação

> **Status: DESENHO, nada codado.** Este documento existe porque a coisa gera migration e mexe
> em quem enxerga uma turma fechada — pelo critério do `CLAUDE.md` isso é `architectural`, e
> `architectural` é design escrito e aprovado ANTES de qualquer código.
>
> **Decisão já tomada pelo Felipe (25/08/2026):** quando faltar gente, o sistema **sugere a um
> membro chamar** — nunca chama sozinho. Ninguém entra na terça de uma panelinha sem alguém de
> dentro ter aprovado. Tudo aqui obedece a isso.

---

## De onde veio

Um amigo do Felipe mandou duas propostas que andam juntas:

| Proposta | Problema que ele descreve |
|---|---|
| **Preencher vaga** | "Turma fechada não alcança gente de fora quando falta jogador." |
| **Lista de reservas da turma** | "Jogador fora do grupo não tem como se candidatar quando falta gente. Sem lista, não há quem chamar." |

O diagnóstico está certo, e é o único ponto da lista dele em que o grupo fechado de WhatsApp é
uma limitação real que o Padelizou resolve.

---

## O que JÁ existe (e por que isso muda o desenho)

Antes de inventar tabela, o que está no ar hoje:

- **`GruposController.Convidar`** já faz o *matching* que a proposta pede: filtra jogadores por
  **categoria** (`JogadorCategoria`), **clube** (`JogadorClube`) e **dia + período**
  (`JogadorDiaHorario`), e ainda deixa achar alguém específico por CPF ou login.
- **`Jogador.AceitaConvitesJogo`** já é o interruptor de "quero ser chamado pra jogo", e ele é
  respeitado tanto na lista quanto no POST.
- **`JogadorDiaHorario`** já registra "eu jogo terça à noite", e é lido em **três** telas
  (Convidar da panelinha, Raquete Livre e Aviso de jogo). Sem linha nenhuma = sem restrição.
- **Lista de espera com promoção automática** já é padrão da casa (`EmListaDeEspera` em duplas
  de torneio, americano e jogo-aula) — o código de "fulano saiu, chama o próximo" existe e foi
  exercitado.
- **Três murais públicos de "tem jogo, vem"**: `/Avisos` (`AvisoJogo`), `/RaqueteLivre` e o
  jogo-aula.

⚠️ **A metade que falta é sempre a mesma: o lado de quem se OFERECE.** Tudo acima é *empurrar* —
alguém de dentro precisa lembrar, abrir a tela e chamar um por um. Não existe nenhum lugar onde
uma pessoa de fora diga "eu topo jogar" e seja encontrada.

⚠️ **E uma pergunta honesta antes de construir:** se os três murais que já existem não estão
preenchendo vaga hoje, vale saber por quê. Se o motivo é que ninguém abre aquelas telas, uma
lista nova terá o mesmo destino. Se o motivo é que nenhum deles está amarrado ao **jogo fixo da
turma**, aí a proposta faz sentido — e é essa a hipótese que este desenho assume.

---

## A pergunta que decide tudo: a lista é de QUEM?

O pedido diz "lista de reservas **da turma**". Isso pode virar duas coisas bem diferentes, e a
escolha muda o custo por um fator de três.

### Caminho A — a lista pertence à TURMA

Cada panelinha tem a sua lista de suplentes. Quem quiser entrar precisa **achar a turma**.

- Tabela nova `ReservaDaTurma` (`GrupoId` + `JogadorId`, PK composta — o índice único é o
  anti-duplicata, como em `TorneioMarcador`).
- **Exige resolver a descoberta:** hoje `GrupoPrivado` é fechado por `CodigoConvite` e não tem
  **nenhuma** coluna de visibilidade. Sem uma vitrine, ninguém de fora sabe que a turma existe —
  e a lista nasce vazia pra sempre.
- Ou seja: puxa junto o item "visibilidade de turmas externas" da outra proposta. Vira coluna
  de visibilidade + vitrine + política de o que é público de uma turma privada (clube, dia,
  horário, categoria — nunca a lista de membros).
- Serve **só àquela turma**.

### Caminho B — a lista pertence ao HORÁRIO *(recomendado)*

O jogador declara "**estou disponível terça, 25/08**", e **toda** turma que joga naquele dia com
vaga aberta o enxerga na tela de Convidar que já existe.

- Tabela nova `JogadorDisponivel` (`JogadorId` + `Data`, PK composta, mais um `ClubeId?`
  opcional e `CriadoEm`). **Uma tabela, sem nenhuma coluna nova em `GrupoPrivado`.**
- **Não expõe turma nenhuma** — o grupo continua fechado por `CodigoConvite`, e o problema de
  visibilidade some do caminho crítico.
- **Reusa a tela `Convidar` inteira**: ela já lista elegíveis com o matching certo. A mudança é
  ordenar primeiro quem se declarou disponível *naquela data*, com um selo.
- Serve a **todas** as turmas de uma vez — e de quebra ao Raquete Livre e ao Aviso de jogo, que
  leem os mesmos filtros.

### Recomendação

**Caminho B.** Ele resolve a mesma dor ("falta um, quem eu chamo?"), custa uma tabela em vez de
uma tabela + visibilidade + vitrine, e não precisa de nenhuma decisão nova sobre privacidade de
grupo. O Caminho A continua possível depois, em cima do B, se a vitrine de turmas virar
prioridade por outro motivo.

⚠️ **O Caminho B tem uma fraqueza declarada:** ele é *genérico*. A proposta original tinha um
componente de vínculo — "sou reserva **daquela** turma, jogo com eles quando falta". No B, quem
se declara disponível é candidato pra qualquer turma daquele horário. Se o que o Felipe quer é
justamente o vínculo com a turma, o A é o caminho, e o custo maior é o preço disso.

---

## Desenho do Caminho B

### Dados

```
JogadorDisponivel
  JogadorId  int      PK, FK -> Jogador
  Data       date     PK          -- o DIA, não o horário: a turma já tem o horário fixo
  ClubeId    int?     FK -> Clube -- opcional: "só nesse clube". Nulo = qualquer um
  CriadoEm   timestamp
```

- **PK composta (`JogadorId`, `Data`)** — é o banco impedindo duplicata, não um `if` em C#
  (degrau 4 da escada do `CLAUDE.md`, o mesmo truque da PK de `TorneioMarcador` contra o clique
  duplo).
- **`Data` e não `DateTime`**: a pessoa se declara disponível *no dia*. O horário é da turma.
- ⚠️ **Sem `Status`.** Cancelar é apagar a linha. Um campo de status abriria "disponível =
  false", que é indistinguível de não ter linha nenhuma e vira duas verdades pra mesma pergunta.

**Limpeza:** linha com `Data` no passado não serve pra nada. Ela é filtrada na leitura
(`Data >= hoje`) e **não** precisa de job de limpeza — o projeto não tem job de background pra
isso e uma tabela dessas cresce devagar. Se um dia incomodar, é um `DELETE` no deploy.

### Fluxo

1. **O jogador se oferece.** Botão "Topo jogar neste dia" no `/Grupos` (a tela que ele já abre) e
   na Home, com um seletor de data curto (os próximos 7 dias). Uma linha por dia escolhido.
2. **A turma vê.** Na tela `Convidar`, quem está disponível *naquela data* sobe pro topo com um
   selo ("topou jogar hoje"). O resto da lista continua igual, na mesma ordem de sempre.
3. **Um membro chama.** O botão é o `ConvidarJogador` que já existe — grava a
   `ConfirmacaoSessao` com `Avulso = true`, `Status = "Pendente"`, manda push e devolve o link
   do WhatsApp. **Nada muda aqui**, e é isso que faz a régua do Felipe valer: quem decide
   continua sendo uma pessoa da turma.
4. **A tela da Semana avisa que há gente disponível.** Onde hoje diz "faltam 2 pra fechar"
   (feito em 25/08), passa a dizer "faltam 2 · 3 jogadores toparam hoje", com link direto.

### Autorização e privacidade

| Ação | Quem pode | Régua |
|---|---|---|
| Criar/apagar a própria disponibilidade | o próprio jogador | `[HttpPost]` + `[Authorize]` + `JogadorId == User` |
| Ver quem está disponível | **membro de alguma turma com jogo naquela data** | a mesma checagem de `Convidar` (`JogadoresGrupo.Any`) |

⚠️ **A lista de disponíveis NÃO é pública.** Uma página aberta com "quem está livre hoje" é uma
lista de pessoas com nome, nível e horário — para qualquer conta logada. A porta é a mesma da
tela `Convidar`: só quem é de uma turma que joga naquele dia.

⚠️ **`AceitaConvitesJogo` continua mandando.** Quem desligou convites no perfil não aparece nem
declarando disponibilidade — senão a chave dele viraria decoração, e o caminho lateral que a
ignora é o que esvazia a chave. A trava vai na leitura **e** no POST, porque a lista some da
vista mas o POST não.

⚠️ **Nada de contato na tela.** Nome e nível, que já são públicos no perfil. Celular e Instagram
seguem a régua de 15/08: só aparecem em quem não tem `PerfilPrivado`, e o `wa.me` sai do
`ConvidarJogador`, depois do convite gravado — nunca da lista.

### Avisos

- **Quem se declara disponível não recebe nada** até ser chamado. Aviso de "surgiu uma vaga" é
  proporcional ao tamanho da base e cai na régua de 09/08 (a cota de e-mail estourou por causa
  de um aviso assim): *a pessoa já sabe disso? ela faz algo por causa dele?*
- **Ser chamado já avisa** — o `ConvidarJogador` de hoje manda push e entra na caixa de avisos.
  Nada novo.

### Testes que precisam existir (Regra 1: escritos antes)

1. Duplicata no mesmo dia é recusada **pelo banco**, não por `if`.
2. Quem desligou `AceitaConvitesJogo` não aparece na lista **e** não passa pelo POST.
3. Quem não é membro de nenhuma turma daquele dia recebe 404 na lista de disponíveis.
4. Disponibilidade de ontem não aparece hoje.
5. Disponível sobe pro topo da tela `Convidar` sem furar o filtro de categoria/clube.
6. A conta de "N toparam hoje" bate com a lista mostrada.
7. Tradução da consulta pro Postgres (`ToQueryString()`) — o InMemory não valida SQL, e a
   comparação de `date` é justamente onde isso morde.

---

## Se for o Caminho A (lista por turma)

Fica registrado o que ele exige, porque a diferença de custo é a informação que decide:

1. `ReservaDaTurma` (`GrupoId` + `JogadorId`, PK composta) — **migration**.
2. **Coluna de visibilidade em `GrupoPrivado`** — outra migration, e é ela que muda a natureza
   do produto: hoje a turma é fechada por construção.
3. **Vitrine de turmas** (`/Turmas`, no molde de `/Professores`), com a decisão do que é público:
   clube, dia, horário, categoria e se aceita reservas — **nunca** a lista de membros.
4. **Fila de entrada na lista**: quem pede pra ser reserva precisa de aprovação de um membro?
   (Pela régua do Felipe, sim — senão qualquer um entra na lista de qualquer turma.) Isso é uma
   terceira tela e um segundo estado.
5. As mesmas travas de autorização do Caminho B, mais a de "quem aprova reserva".

**Estimativa relativa:** ~3× o Caminho B, e duas das quatro peças (visibilidade + vitrine) são
decisões de produto que o Felipe ainda não tomou.

---

## O que NÃO está neste desenho, de propósito

- **Chamada automática do primeiro da fila.** Decisão do Felipe: o sistema sugere, uma pessoa
  chama.
- **Feed / rede social.** Outra proposta, outro documento, e a trava dela é dado, não código.
- **Pagamento do avulso.** `GrupoPrivado.ValorAvulso` já existe e já aparece no convite; cobrar
  por dentro é assunto de dinheiro, e dinheiro é `architectural` por conta própria.
- **Reserva com prioridade / ordem de fila.** No B a ordenação é por relevância do matching, não
  por quem chegou primeiro. Fila numerada é do Caminho A.

---

## Decisões pendentes do Felipe

1. **Caminho A ou B?** (recomendação: B)
2. **A janela de "próximos dias" é 7?** Menos que isso não pega quem se organiza na semana;
   muito mais vira lista de intenção que ninguém mantém.
3. **O jogador pode limitar por clube?** (o `ClubeId?` opcional acima). Simplifica a v1 tirar, e
   é uma coluna a menos.

Aprovado o caminho, o próximo passo é a migration em worktree limpo + `dotnet ef migrations
has-pending-model-changes`, como manda o `CLAUDE.md`.
