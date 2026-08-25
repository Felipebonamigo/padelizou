# Recebimento da aula — desenho

> **Status: desenho escrito, nada codado.** Mexe em dinheiro e gera migration — pelas réguas do
> `CLAUDE.md` é `architectural`, e `architectural` é desenho aprovado antes de qualquer código.
> **Três decisões pendentes do Felipe no fim do documento.**

## O pedido

Felipe, 25/08/2026, num print da folha de detalhe da "Minha Agenda" (a que tem *Chamar aluno ·
Anotações · Editar · Concluir · Vai recuperar · Cancelar · Apagar*):

> *"aqui também, permita o professor colocar como aula concluída mas ainda não paga, tem alunos
> que pagam depois ou por mês, pense nisso"*

## Metade disso já existe — e a outra metade não existe nada

**"Pagam por mês" está pronto desde 19/08/2026.** `Models/FaturaDoAluno` + `Services/FechamentoDoMes`
fecham a competência, geram uma conta por aluno (`Aberta`/`Paga`/`Cancelada`), dão vencimento, e a
tela `/Aulas/Faturamento` marca paga à mão ou emite cobrança pelo app. O mensalista está resolvido
no **fim do mês**.

**"Pagam depois" não tem nada.** O aluno avulso que dá a aula na terça e manda o Pix na sexta não
tem onde ser registrado: não existe uma marca de recebimento por aula em lugar nenhum do sistema.

## O defeito de verdade: "Concluir" hoje quer dizer duas coisas

`AulasController.Financeiro.cs:51` diz, no comentário do próprio código:

```csharp
// "Recebido" = aula que aconteceu. Falta cobrável conta como devida, não recebida.
var realizadas = doPeriodo.Where(a => a.Status == PoliticaAula.Realizada).ToList();
```

Ou seja: apertar **Concluir** grava, na mesma tacada, *"a aula aconteceu"* **e** *"o dinheiro
entrou"*. E o outro lado da conta confirma — a lista de devedores (`vm.Devedores`) é montada só de
`CobrarMesmoFaltando`, com o comentário *"sem controle de quitação por aula, o critério é 'aconteceu
e é cobrável'"*.

⚠️ **A consequência é que aula dada e não paga é INVISÍVEL hoje**: está somada em "Recebido", não
está em "A receber", e não aparece em "quem está devendo". O professor não tem como ver a diferença
— que é exatamente o que o Felipe está pedindo.

**Seis lugares somam dinheiro pelo status**, e todos herdam a confusão:

| Onde | Linha | O que soma hoje |
|---|---|---|
| `Financeiro` — "Recebido" | `Financeiro.cs:55` | `Realizada` do período |
| `Financeiro` — "Quem está devendo" | `Financeiro.cs:73` | só `CobrarMesmoFaltando` |
| `Financeiro` — por local | `Financeiro.cs:87` | `Realizada` |
| `Financeiro` — meses/semanas/anos | `:107`, `:118`, `:130` | `Realizada` |
| `Relatorio` + Painel do Professor | `Financeiro.cs:282` | `Realizada` → `TotalRecebido` |
| Home do professor | `HomeController.cs:357` | `Realizada` do mês |

## Decisão de forma: **pago NÃO é status**

O caminho barato seria um `PoliticaAula.Paga` ao lado de `Realizada`. **É errado, e por dois
motivos concretos:**

1. **`Faltou` + `CobrarMesmoFaltando` também pode ser pago.** Um status só não consegue dizer
   "faltou, foi cobrada, e o aluno já pagou" — e essa é a linha mais comum do mensalista.
2. **Perde o fato da agenda.** `Realizada` responde a agenda (`EdicaoDeAula`, `Reposicao`,
   `_CardDeAula`, a cor da grade, a estatística do aluno). Trocá-lo por `Paga` faz a aula sumir
   de tudo isso.

É a mesma armadilha de **18/08**, agora em versão de dinheiro: `PodeOlharTudo` significava ao mesmo
tempo "enxerga a operação?" e "enxerga o caixa?", e a correção foi separar as duas perguntas em duas
réguas. Aqui: **status responde o que aconteceu com a AULA; recebimento responde se o DINHEIRO
entrou.** São eixos ortogonais.

→ **`Aula.PagaEm` (`DateTime?`)**, nulo = não recebido. Nome idêntico ao `FaturaDoAluno.PagaEm`, que
já significa exatamente isto no sistema — duas grafias pro mesmo conceito é como nasce a divergência
das duas grafias de fase.

## A migration, e a linha que não pode faltar

Coluna nova nasce nula. **Nulo em toda aula já dada faz o Financeiro abrir em "Recebido R$ 0,00"
com o histórico inteiro na lista de devedores** — o professor acha que o sistema perdeu o dinheiro
dele.

Até hoje `Realizada` **significava** recebido. O backfill preserva a verdade atual, não inventa uma
nova:

```sql
UPDATE "Aula" SET "PagaEm" = "DataHora"
WHERE "Status" = 'Realizada'
  AND NOT EXISTS (SELECT 1 FROM "FaturaDoAluno" f
                  WHERE f."ProfessorId" = "Aula"."ProfessorId"
                    AND f."Status" = 'Aberta'
                    AND f."Ano" = EXTRACT(YEAR FROM "Aula"."DataHora")
                    AND f."Mes" = EXTRACT(MONTH FROM "Aula"."DataHora")
                    AND (f."AlunoId" = "Aula"."AlunoId" OR f."NomeAvulso" = "Aula"."NomeAlunoAvulso"));
```

O `NOT EXISTS` é a única parte discutível — ver decisão 1. Sem ele, a aula de um mensalista com
conta ABERTA nasceria marcada como paga, e o professor perderia de vista dinheiro que ele sabe que
não recebeu.

## As telas

**1. Agenda (a do print).** `Concluir` vira duas ações no mesmo lugar:

- **`✓✓ Concluir e recebi`** — grava `Realizada` + `PagaEm = agora`. É o clique de hoje.
- **`✓ Concluir, receber depois`** — grava `Realizada` com `PagaEm` nulo.

E na aula **já concluída** a folha ganha **`💵 Recebi`** (ou `↩ Não recebi`, pra desfazer) — porque
o momento de registrar o Pix da sexta é a sexta, não a terça.

⚠️ **Turma (`Aula.TurmaId`) é o caso que quebra se ninguém pensar nele.** A tela mostra UM card pra
turma inteira (`Services/AgendaDeTurma.Colapsar`, **preço somado**) e o `AtualizarStatus` já espalha
pras N linhas. Mas **recebimento não se espalha**: dos três alunos, dois pagaram e um não — cada um
tem a própria linha de `Aula` com o próprio `Preco`, que é justamente o que faz a cobrança
individual de 22/08 funcionar. Então: **`Concluir` continua valendo pra turma toda; `Recebi` é por
aluno.** Na folha da turma, uma linha por aluno com o botão de cada um — e o preço somado do card
deixa de ser a régua do que entrou.

**2. Financeiro.** `Recebido` passa a somar `PagaEm != null`. Nasce **"A receber"** com as dadas e
não pagas, e a lista de devedores passa a incluí-las — deixa de ser só falta cobrável.

**3. Faturamento.** O fluxo não muda. O que muda é que marcar a conta do mês como `Paga` **carimba
o `PagaEm` das aulas dela**, e `ReabrirFatura` apaga o carimbo. Uma verdade só na tela: sem isso, a
conta de abril diz "paga" e as oito aulas de abril continuam dizendo "a receber", cada uma.

## A armadilha: cobrar duas vezes

O professor marca "recebi" numa aula solta, e no fim do mês fecha a competência. `EntraNaConta` hoje
aceita toda `Realizada` — **a aula paga em dinheiro entraria de novo na conta do mês.** O comentário
do `FechamentoDoMes` chama isso de *"o erro mais caro possível nesta tela"*, e o desenho não pode
criá-lo.

→ **`FechamentoDoMes.EntraNaConta` passa a exigir `PagaEm == null`.** A conta do mês fica "6 aulas,
R$ 660" em vez de "8 aulas, R$ 880" quando duas já foram acertadas por fora — que é exatamente o
valor a cobrar. A tela do fechamento diz quantas ficaram de fora e por quê, senão o professor lê o
número menor como defeito.

## O que fica de fora, de propósito

- **Reposição** (`RecuperaAulaId != null`, preço 0): nunca entra em "a receber". Ela já foi paga no
  mês da aula original — é a mesma razão pela qual ela nasceu sem preço.
- **Pagamento parcial** ("metade agora, metade depois"): não. Guardar um `decimal` a menos é abrir
  uma segunda contabilidade dentro da `Aula`, com todas as somas do sistema tendo que aprender a
  diferença entre preço e saldo.
- **Faturamento por competência** (`UltimosMeses`/`UltimasSemanas`/`UltimosAnos`): continuam contando
  aula DADA. É faturamento, não caixa — misturar os dois faria o gráfico de tendência mudar de forma
  toda vez que um aluno atrasasse um Pix.
- **Forma de pagamento** (Pix/dinheiro/cartão): não. Um campo que ninguém consulta é um campo que
  todo mundo preenche errado.

## Custo

Uma coluna, uma migration com backfill, uma régua nova (`Services/RecebimentoDaAula`, no molde do
`VagasDaSessao`: função pura, sem EF), os dois botões na folha da agenda, e as seis somas acima
escolhendo qual pergunta respondem. **Sem tabela nova, sem papel de acesso novo, sem tocar nas
quatro réguas de autorização.**

## Decisões pendentes do Felipe

1. **Backfill.** Carimbar como pago tudo que está `Realizada` (simples, e é o que a tela diz hoje),
   ou preservar o `NOT EXISTS` e deixar em aberto o que está dentro de uma conta do mês ainda
   `Aberta`? — **Recomendo com o `NOT EXISTS`**: são poucas contas, e a alternativa apaga dívida
   real de mensalista.
2. **O botão.** Dois botões lado a lado (`Concluir e recebi` / `Concluir, receber depois`), ou um
   `Concluir` só + um `Recebi` que aparece depois? — **Recomendo os dois botões**: o clique de
   fechar a aula é o mesmo em que o professor sabe se o dinheiro entrou, e um passo a mais depois é
   um passo que ninguém dá.
3. **O padrão de quem só recebe na hora.** Vale um ajuste no perfil do professor
   (*"minhas aulas nascem pagas"*) pra ele não ter que escolher toda vez? — **Recomendo NÃO fazer
   agora**: são dois botões do mesmo tamanho, e uma preferência escondida no perfil é como
   "recebido" volta a mentir sem ninguém perceber.
