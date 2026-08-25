# Recebimento da aula — desenho

> **Status: IMPLEMENTADO em 25/08/2026.** Mexe em dinheiro e gera migration — pelas réguas do
> `CLAUDE.md` é `architectural`, então o desenho foi escrito primeiro e aprovado pelo Felipe com
> as três decisões do fim do documento. O que está abaixo é o que foi construído.

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

**1. Agenda (a do print).** `Concluir` virou duas ações no mesmo lugar:

- **`✓✓ Concluir e recebi`** — grava `Realizada` + `PagaEm = agora`. É o clique de hoje.
- **`✓ Concluir, receber depois`** — grava `Realizada` com `PagaEm` nulo.

E na aula **já concluída** a folha ganhou **`💵 Recebi`** (e `↩ Não recebi`, pra desfazer) — porque
o momento de registrar o Pix da sexta é a sexta, não a terça. Uma etiqueta ao lado do status diz
qual dos dois estados vale: verde "Recebido em 18/08", ou âmbar "A receber" (âmbar e não vermelho —
não é erro nem atraso, é conta aberta).

⚠️ **Turma (`Aula.TurmaId`) é o caso que quebra se ninguém pensar nele.** A tela mostra UM card pra
turma inteira (`Services/AgendaDeTurma.Colapsar`, **preço somado**) e o `AtualizarStatus` já espalha
pras N linhas. Mas **recebimento não se espalha**: dos três alunos, dois pagaram e um não — cada um
tem a própria linha de `Aula` com o próprio `Preco`, que é justamente o que faz a cobrança
individual de 22/08 funcionar. Então: **`Concluir` continua valendo pra turma toda; `Recebi` é por
aluno.** Onde isso se resolve **não é na folha** — é na lista de "quem está devendo" do Financeiro,
onde cada aluno da turma já aparece na PRÓPRIA linha, com as próprias aulas em aberto e o próprio
botão. A folha continua sendo o card da sessão; a lista é que é por pessoa. Foi mais barato e mais
honesto do que abrir N linhas dentro do modal.

**2. Financeiro.** `Recebido` passou a somar o que tem `PagaEm`; "A cobrar" e a lista de devedores
passaram a incluir a aula dada e não paga — antes eram só falta cobrável. Cada devedor ganhou um
botão **`💵`** que dá baixa nas N aulas em aberto dele de uma vez (os ids vão no formulário: dar
baixa não pode depender de o servidor recalcular o grupo no POST, que é como a lista da tela e a do
servidor divergem).

⚠️ **E as OUTRAS TRÊS telas que diziam "recebido" foram junto**: o Relatório, o Painel do Professor
na tela inicial e a tabela por local. As três somavam aula DADA sob um rótulo de dinheiro — o mesmo
defeito, por outras portas. Deixá-las como estavam poria dois "Recebido" diferentes pro mesmo mês na
frente do professor, que é como ele conclui que o sistema perdeu dinheiro dele. O total da tabela por
local agora **bate com o card do topo**, e tem teste travando isso.

**3. Faturamento.** O fluxo não muda. O que muda é que marcar a conta do mês como `Paga` **carimba
o `PagaEm` das aulas dela**, e `ReabrirFatura` apaga o carimbo. Uma verdade só na tela: sem isso, a
conta de abril diz "paga" e as oito aulas de abril continuam dizendo "a receber", cada uma.

## A armadilha: cobrar duas vezes

O professor marca "recebi" numa aula solta, e no fim do mês fecha a competência. `EntraNaConta` hoje
aceita toda `Realizada` — **a aula paga em dinheiro entraria de novo na conta do mês.** O comentário
do `FechamentoDoMes` chama isso de *"o erro mais caro possível nesta tela"*, e o desenho não pode
criá-lo.

→ **`FechamentoDoMes.EntraNaConta` passou a exigir `PagaEm == null`.** A conta do mês fica "6 aulas,
R$ 660" em vez de "8 aulas, R$ 880" quando duas já foram acertadas por fora — que é exatamente o
valor a cobrar. E se o mês inteiro já foi pago por fora, não nasce conta nenhuma: uma conta de
R$ 0,00 na tela é pior que nenhuma.

🐛 **E o teste dessa fronteira pegou um defeito meu antes de ele existir em produção.** A primeira
versão do carimbo marcava TODAS as aulas do aluno naquela competência — inclusive a que ele já tinha
acertado em dinheiro ANTES do fechamento, e que por isso nem entrou na conta. Reabrir a conta depois
apagava aquele Pix, que aconteceu de verdade. Agora o carimbo só alcança as que estavam em aberto, e
o reabrir só apaga as que carregam o `PagaEm` **daquela** baixa (a marca de tempo é a mesma da conta,
e é ela que identifica o grupo).

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

## As três decisões, e o que o Felipe escolheu

1. **Backfill** → *carimbar tudo que está `Realizada`, MENOS o que está numa conta do mês ainda
   `Aberta`*. Essas o professor sabe que não recebeu — foi ele quem fechou a conta —, e carimbá-las
   apagaria dívida real de mensalista.
2. **O botão** → *dois botões lado a lado*. O clique de fechar a aula é o mesmo em que ele sabe se o
   dinheiro entrou; um passo a mais depois é um passo que ninguém dá.
3. **Padrão no perfil do professor** ("minhas aulas nascem pagas") → *não fazer agora*. São dois
   botões do mesmo tamanho, um do lado do outro, e uma preferência escondida no perfil é como
   "recebido" volta a mentir sem ninguém perceber.

## A migration foi conferida contra um Postgres DE VERDADE, com dados

O `EF InMemory` da suíte não roda migration nenhuma, então o backfill é a única parte deste trabalho
que teste algum não alcança. Foi conferido subindo um Postgres 16, semeando **nove linhas no schema
ANTIGO** e aplicando a migration por cima. Os nove casos saíram como o desenho manda:

| Caso | Esperado | Saiu |
|---|---|---|
| Dada, sem conta do mês | carimba | ✅ |
| Dada, dentro de conta **Aberta** | **fica em aberto** | ✅ |
| Dada, dentro de conta **Paga** | carimba | ✅ |
| Falta cobrável | fica em aberto | ✅ |
| Confirmada (ainda vai acontecer) | não carimba | ✅ |
| Reposição (R$ 0, aponta pra outra) | não carimba | ✅ |
| Dada de graça (R$ 0) | não carimba | ✅ |
| Aluno **com conta**, conta Aberta do mês dele | fica em aberto | ✅ |
| Aluno **com conta**, mês sem conta fechada | carimba | ✅ |

As duas últimas linhas são as que provam a identidade: casar por `AlunoId` e por `NomeAvulso` na
mesma consulta, sem uma pegar a linha da outra. O `Down` também foi rodado — derruba a coluna e
preserva as nove aulas — e o `has-pending-model-changes` volta limpo.
