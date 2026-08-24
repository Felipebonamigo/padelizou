# Ondas paralelas

Como rodar várias tarefas ao mesmo tempo sem que dois agentes se atropelem. Leia antes de
paralelizar qualquer coisa — as duas condições da seção "Formação da onda" são o que torna
isso seguro, e pular uma delas é como se cria o bug que este documento existe pra evitar.

## Antes de tudo: isso vale a pena aqui?

Multiagente custa **3 a 10 vezes mais tokens** que um agente só resolvendo a mesma coisa
(número do post [Multi-Agent Systems](https://claude.com/blog/building-multi-agent-systems-when-and-how-to-use-them),
da Anthropic). Esse custo só se paga em três cenários:

- **Proteção de contexto** — isolar investigação barulhenta numa instância separada.
- **Paralelização real** — trabalho genuinamente independente.
- **Especialização genuína** — a tarefa exige um checklist que um generalista não carrega.

Num projeto de uma pessoa, o caso comum é uma fila de correções independentes: três defeitos
sem relação entre si, em arquivos diferentes. Fora disso, serial costuma sair mais barato e
igualmente rápido. **Na dúvida, serial.**

Decomponha por **fronteira de contexto isolável** — o que cada tarefa precisa saber pra
trabalhar sozinha — e não por área temática. Os anti-padrões nomeados no mesmo post: dividir
por assunto ("um pro banco, um pra tela"), separar plano/implementação/teste em agentes
distintos, e paralelizar trabalho que depende de estado compartilhado mutável.

## Marcação das tarefas

Toda tarefa planejada recebe dois campos, antes de qualquer despacho:

- **`Files:`** — os caminhos exatos que ela cria ou modifica. Na dúvida, liste mais, não
  menos. `Files: vários arquivos de torneio` não é marcação, é ausência de marcação.
- **`Depends-on:`** — os IDs das tarefas cujo resultado ela consome, ou `nenhuma`.

**Incerteza real sobre escopo vira `Depends-on: tudo que já foi listado.`** É a rede de
segurança: tarefa mal especificada degrada pra execução serial em vez de virar paralelismo
falso. O erro seguro é o único permitido.

## Formação da onda

Duas tarefas entram na mesma onda **se e somente se as duas condições valerem**:

1. Nenhuma depende da outra, nem transitivamente.
2. Os conjuntos de `Files:` são totalmente disjuntos — zero arquivo em comum.

Falhou uma, vão pra ondas diferentes. **"Mesmo arquivo" desqualifica mesmo que sejam seções
diferentes do arquivo** — a regra é por arquivo, não por linha.

Onda de uma tarefa só é o resultado correto quando é isso que a regra dá, não uma falha.
E quando duas tarefas tocam o mesmo arquivo por coincidência, **fundir as duas numa só**
costuma ser melhor que adiar uma — evita até o commit extra.

## Laço de execução, por onda

1. Escreva um arquivo de instruções por tarefa da onda.
2. **Despache todos os implementadores da onda numa única mensagem.** Esse é o único ponto
   em que o paralelismo de fato acontece; um despacho por mensagem roda em série.
3. **Nenhum implementador commita.** Cada um edita, verifica o próprio trabalho e reporta
   exatamente quais arquivos mudou. Não abra exceção pro "só dessa vez" — é assim que a
   disputa de commit volta a existir.
4. **Quem orquestra commita**, um por tarefa, em ordem fixa, **capturando o `HEAD` atual
   imediatamente antes de cada commit** — nunca um `HEAD` capturado no início da onda, que
   já está velho depois do primeiro commit.
5. Só então despache os revisores da onda, também juntos. É seguro porque revisão é leitura.
6. **Um único registro por onda**, nunca um por tarefa — dois agentes escrevendo no mesmo
   arquivo de acompanhamento é a mesma classe de bug que dois disputando commit.

## Válvula de escape

Quando duas tarefas genuinamente não dão pra separar, isole cada implementador na própria
worktree e no próprio branch — aí commitar sozinho volta a ser seguro. É caro (disco e setup
por agente) e é último recurso, nunca o padrão.

## O que é específico daqui

- **As quatro réguas de autorização são UMA tarefa, nunca uma por arquivo.**
  `TorneiosController.EhOrganizadorAsync`, `DuplasController.UsuarioEhOrganizadorAsync`,
  `PartidasController.PodeControlarPlacarAsync` e `PodeOperarODiaDeJogoAsync` precisam andar
  juntas — uma dessincronia já quebrou a Mesa de Controle em 31/07. Elas passam nas duas
  condições da formação de onda (arquivos disjuntos, sem dependência declarada) e mesmo
  assim **não podem** ser paralelizadas: a dependência é semântica, não de arquivo.
- **O `STATUS.md` é o registro da onda** — uma escrita por onda, feita por quem orquestra,
  depois que todos os commits existem.
- **Tarefa que gera migration nunca entra em onda.** Migration se gera em worktree limpo, e
  `dotnet ef migrations has-pending-model-changes` compara contra o modelo inteiro — outro
  agente editando entidade ao mesmo tempo envenena a checagem.
- **A suíte roda uma vez, no fim da onda**, não uma por tarefa: são ~50s e um mesmo build.

## Em outro projeto

Nada acima depende de .NET. Copie este arquivo, troque a seção "O que é específico daqui"
pelas armadilhas de lá, e aponte uma linha do `CLAUDE.md` pra ele.
