# Snapshot de um torneio — DESENHO, aguardando aprovação

> ⚠️ **Nada disto está implementado.** É o desenho escrito que a regra `architectural` do
> `CLAUDE.md` exige antes do código: o script escreve no banco de **produção**.
> 🗣️ Felipe, 10/09/2026: *"como esta nosso backup, como eu montei todo torneio do er, nao
> podemos perder essa chave de nenhum jeito"*.

## O problema, em uma linha

O backup de 4h/16h protege contra o **VPS morrer**. Ele não desfaz um erro na chave do Er sem
desfazer junto tudo o que entrou depois — inscrição, pagamento, placar de outro torneio. E não
existe caminho de volta pra dentro do `prod`: o `copiar-torneio.sh` recusa `--para db_padel`, de
propósito. Falta a peça do meio: **guardar UM torneio e devolver UM torneio.**

## O que "a chave" é, em colunas — lido dos dois botões

| Botão | O que ele destrói (`TorneiosController.Chaves.cs`) |
|---|---|
| **Refazer grade** | Zera `Partida.HorarioPrevisto` e `Partida.NomeQuadra` de tudo que está `Agendada`, reencaixa, e **apaga todas as `ReservaDeHorario`** do torneio. **Não apaga jogo nenhum** — os Ids continuam os mesmos. |
| **Desfazer sorteio** | Apaga as `Partida` e as `GrupoTorneio`, solta `Dupla.GrupoTorneioId`, apaga as reservas, volta o `Status` pra "Chaves em Sorteio" e pode zerar `TaxaExternoAdiadaEm` (o fiado — **dinheiro**). |

São dois estragos de tamanhos muito diferentes, e é daí que sai a decisão 1.

## Decisão 1 — dois comandos, e a restauração tem DUAS portas

```bash
snapshot-torneio.sh ERPADEL                          # grava
restaurar-torneio.sh <arquivo> --grade  [--aplicar]  # só o slot dos jogos + as reservas
restaurar-torneio.sh <arquivo> --chave  [--aplicar]  # o sorteio inteiro de volta
```

**`--grade`** desfaz o *Refazer grade*: escreve `HorarioPrevisto`, `NomeQuadra` e `ClubeId` de
cada `Partida` pelo Id, e repõe as `ReservaDeHorario`. **Nenhum INSERT, nenhum DELETE — só
UPDATE por Id.** É o caso provável, e é o único que roda sem apagar uma linha sequer.

**`--chave`** desfaz o *Desfazer sorteio*: reinsere `Partida` e `GrupoTorneio` **com os Ids
originais**, repõe `Dupla.GrupoTorneioId`, as reservas e o `Status`.

É a escada do `CLAUDE.md`: o degrau que resolve o caso comum não paga o preço do caso raro.

## Decisão 2 — o snapshot guarda TUDO; quem escolhe o escopo é a restauração

O dump do banco inteiro tem ~96 KB. Guardar o torneio inteiro custa nada e evita o pior
resultado possível: "eu tinha guardado, mas não a coluna de que eu precisava".

**Mecânica, reusando o que já está provado no `copiar-torneio.sh`:** schema de trabalho com
`s_<Tabela> (LIKE public."<Tabela>")`, preenchido pelo mesmo `COPY (filtro) TO STDOUT`, e o
arquivo é um `pg_dump --data-only --schema=snapshot | gzip`. Restaurar é ler o arquivo de volta
pro schema e escrever a partir dele.

Duas propriedades que vêm de graça dessa escolha:

- **O arquivo é um `pg_dump` comum.** Se o script quebrar um dia, dá pra ler e restaurar na mão.
- **Sobrevive a migration.** O `pg_dump` escreve a lista de colunas dentro do `COPY`; um
  snapshot de ontem carregado num schema criado hoje entra com as colunas que existiam, e as
  novas ficam no default. Colar lista de coluna à mão não teria essa propriedade — é o mesmo
  motivo pelo qual `copia.inserir` lê o `information_schema` em vez de uma lista escrita.

### 🐛 E aqui aparece um defeito que já existe hoje

`copiar-torneio.sh` foi escrito em 09/09; `ReservaDeHorario` nasceu em 10/09. **A lista
`TABELAS` dele não tem a tabela nova** — o ensaio do Er no `dev` está indo sem as reservas de
horário das finais. A lista de tabelas e o `filtro()` saem dos dois scripts pra um
`torneio-tabelas.sh` compartilhado, e isso conserta o `copiar-torneio.sh` no mesmo movimento.
Duas listas divergiriam de novo na próxima tabela.

## Decisão 3 — a lista de recusa: o que a restauração NUNCA toca

| Nunca | Por quê |
|---|---|
| `Pagamento` | Dinheiro. Nem lido, nem escrito, nem contado como "do torneio". |
| `Torneio.TaxaExternoAdiadaEm` | O carimbo do fiado é dívida. O `--chave` devolve o `Status`, **não** o carimbo: ele imprime o valor do snapshot e o de agora e manda resolver na tela do financeiro. Script não restaura dívida. |
| `Dupla` como linha (INSERT/DELETE) | Dupla que se inscreveu depois do snapshot é **inscrição paga**; apagá-la é perder dinheiro. Só `GrupoTorneioId`/`Grupo` são escritos. |
| `Jogador`, `Clubes` | Mesmo banco, mesmos Ids. Não há o que remapear. |
| `PalpitePartida`, `VotoDeMvp`, `SeguidorTorneio`, `SeguidorDePartida`, `AvaliacaoDoTorneio`, `ChamadoDoMural`, `HistoricoDePadelimetro`, `AcertoRankingRs`, `InscricaoAmericana`, `SolicitacaoRegistroResultados` | Apontam pra dentro do torneio e não são a chave. Como `--grade` só faz UPDATE, e `--chave` só reinsere Ids que o próprio app já tinha apagado, nenhum é tocado. |

**A trava que sustenta a linha de baixo da tabela:** antes de qualquer DELETE, o script pergunta
ao catálogo (`pg_constraint`) quem referencia as tabelas que ele vai mexer. Se aparecer uma FK
que não está na lista conhecida, ele **recusa e diz o nome dela**. Este projeto ganha tabela toda
semana; uma lista escrita à mão silenciosamente desatualizada é como se perde dado.

## Decisão 4 — as travas

1. **`--conferir` é o padrão.** Sem `--aplicar` ele não grava: lista quantos jogos mudam de
   horário, quais e quais reservas voltam.
2. **Recusa torneio trocado.** Confere `Torneio.Id` **e** `Codigo` do snapshot contra o banco.
3. **Uma transação só.** Restauração pela metade é pior que nenhuma.
4. **`--grade` recusa mexer em jogo com `Status != 'Agendada'`.** Placar de jogo já jogado não
   volta pro horário antigo — é a mesma régua que o próprio *Refazer grade* usa.
5. **Snapshot automático antes de restaurar.** A primeira coisa que se quer quando a
   restauração sai errada é desfazê-la.
6. **Confere as duas pontas no fim** e sai com erro se não bater, como o `copiar-torneio.sh`.
   "Não deu erro" não é "voltou inteiro".

## Decisão 5 — onde o arquivo mora

`/var/backups/padelizou/torneios/<CODIGO>_<stamp>.sql.gz`, e **sobe pro B2 na hora**, dentro do
`snapshot-torneio.sh`. Esperar a rodada das 4h30 perderia o ponto: o valor dele é "vou apertar
Refazer grade **agora**". Duas linhas no `backup-offsite.sh` pra ele entrar também na rodada
diária.

**Sem retenção automática.** Os `find -mtime +14 -delete` que já existem não pegam esse nome, e é
melhor assim: apagar sozinho o único registro de uma chave montada à mão é exatamente o risco
que este script existe pra cobrir.

## O que fica de fora, de propósito

- **Botão na tela.** Vira feature no app, com `[HttpPost]` + `[Authorize]` + checagem de
  organizador (Regra 0) e desenho próprio. O script é infra, roda por SSH, e resolve o fim de
  semana do Er.
- **Snapshot automático antes do Refazer grade.** É a versão em C# disto. Depois.

## Como eu provo que funciona — antes do Er, no `dev`

O Er já está no `dev` (`copiar-torneio.sh ERPADEL` rodou em 09/09).

1. Snapshot do Er no `dev`.
2. **Refazer grade** no `dev` → confirmar que a grade mudou.
3. `restaurar --grade --conferir` → tem que listar **exatamente** os jogos que mudaram.
4. `--aplicar` → `HorarioPrevisto`, `NomeQuadra` e `ClubeId` idênticos ao snapshot em 100% dos
   jogos, e as reservas de volta.
5. Rodar de novo: idempotente, segunda vez muda **0 linhas**.
6. O mesmo roteiro pro `--chave`, depois de um **Desfazer sorteio**.
7. **Teste negativo:** contagem e soma de `Pagamento` do torneio idênticas antes e depois.

⚠️ **Sinceridade sobre a Regra 1:** isto é shell, e a suíte do projeto é xUnit — **não há teste
de regressão em C# que cubra este script.** A prova é o roteiro medido acima, e é por isso que
ele tem passo negativo e passo de idempotência em vez de terminar no "rodou".

## Custo

~200 linhas de shell reusando mecânica já provada, mais duas linhas no `backup-offsite.sh`, mais
a extração do `filtro()` pro arquivo compartilhado — que de quebra conserta a `ReservaDeHorario`
faltando no `copiar-torneio.sh`.
