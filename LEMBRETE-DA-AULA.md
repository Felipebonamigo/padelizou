# Lembrete da aula (24h e 1h antes) — design para aprovação

> **Status: DESENHO, nada codado.** Gera migration (uma coluna em `Aula` pra guardar o marco já
> enviado). Pelo critério do `CLAUDE.md` isso é `architectural`, e `architectural` é design escrito
> e aprovado ANTES de qualquer código.
>
> **Depende de 4 decisões do Felipe** — a tabela no fim. Nada começa antes delas: as duas primeiras
> mudam o que a varredura lê, e a terceira decide se o aviso alcança 4 aparelhos ou 154 celulares.

---

## De onde veio

Maickel, 16/09/2026, pelo WhatsApp:

> *"Só talvez faria um 'push' — avisando 24hs e 1hora antes da aula — para as opções de aula de padel."*

É a única parte do sistema com hora marcada que **não avisa ninguém antes**. O torneio tem lembrete
de inscrição não paga, o jogo fixo da panelinha tem o de 24h, a cobrança tem o de 6h — a aula, que é
o compromisso mais pessoal de todos, marca e cala até o dia.

---

## O que JÁ existe (e é por isso que isto é pequeno)

| Peça | Onde | O que resolve daqui |
|---|---|---|
| Funil de avisos | `Services/PushNotificationService.EnviarParaJogadorAsync` | Caixa de entrada + push + e-mail + WhatsApp num lugar só. **Nada de canal novo precisa ser escrito** |
| Varredura com marco gravado | `Services/LembreteDeInscricaoNaoPaga` + `…BackgroundService` | O padrão inteiro: marcos, `VarrerAsync(context, push, agora, ct)` estático, `agora` por parâmetro pro teste |
| Varredura de 24h com janela | `Services/LembreteJogoBackgroundService` | O tick de 15 min e a lição do aviso duplicado (WhatsApp direto + funil) |
| Régua de status da aula | `Services/PoliticaAula` | `Confirmada`, `Pendente`, `Cancelada`, `A recuperar`… e `AindaVaiAcontecer` |
| Régua de canal | `Services/AlcanceDoAviso` | Os três critérios do WhatsApp: pessoal, urgente, acionável |

**O que NÃO existe e é o motivo da migration:** `Aula` não tem onde anotar "já avisei". Sem isso, a
varredura reenviaria a cada tick, e um deploy no meio da janela reenviaria de novo — a lição já
escrita em `LembreteDeInscricaoNaoPaga`: *"o marco vai gravado na inscrição e não a data do envio;
com data, um restart no meio reenviaria"*.

⚠️ `Aula` **também não tem `CriadoEm`** — não dá pra saber se a aula foi marcada ontem ou há 20
minutos. É o que decide o caso da aula marcada em cima da hora (ver "Um aviso, não dois").

---

## O desenho

### 1. A coluna

`Aula.UltimoLembreteEnviado` — `int?`, **nullable e sem `defaultValue`** (a lição do `= 60`: default
no banco carimba a base inteira). Guarda o marco em HORAS, não a data do envio: `24`, depois `1`.
Nulo = nunca avisado. Migration `LembreteDaAula`, gerada em worktree limpo, conferida com
`dotnet ef migrations has-pending-model-changes`.

### 2. A régua — `Services/LembreteDaAula.cs` (pura, sem banco)

```csharp
public static readonly int[] Marcos = { 24, 1 };   // horas que faltam

int? MarcoDevido(DateTime dataHoraDaAula, DateTime agora, int? ultimoMarcoEnviado)
string Titulo(bool paraOProfessor, int marco)
string Frase(...)        // conta as horas DE VERDADE, nunca o número do marco
bool HoraCivilizada(DateTime agora)   // só o marco de 24h obedece
```

**Um aviso, não dois.** Quando os dois marcos vencem de uma vez (aula marcada faltando 40 minutos),
vale o **mais urgente** e o outro é dado por cumprido — mesma regra do lembrete de inscrição, e o
motivo aqui é o mesmo: senão a pessoa leva dois avisos em 15 minutos, um por tick.

**O texto conta o tempo de verdade.** Quem entra pelo marco de 24h com a aula em 15 horas (porque o
marco caiu de madrugada e foi segurado) não pode ouvir "amanhã" se a aula é hoje. A frase é montada
de `dataHoraDaAula - agora`, não do número do marco.

**Madrugada:** o marco de **24h** só sai entre **7h e 22h** — aula de sábado às 22h tem marco na
sexta às 22h, e ninguém é acordado por isso; segurar até as 7h ainda dá 15 horas de aviso. O marco
de **1h não tem janela**: quem tem aula às 6h já vai acordar às 5h de qualquer jeito, e segurar esse
aviso é a mesma coisa que não mandá-lo.

### 3. A varredura — `Services/LembreteDaAulaBackgroundService.cs`

Tick de **15 minutos** (o marco de 1h precisa; o de 24h não se importa). Varre na subida também.
`public static async Task<int> VarrerAsync(context, push, agora, ct)` — estático e com `agora` por
parâmetro, que é o que permite exercitar a varredura inteira no teste em vez de conferir só a régua.

```csharp
var aulas = await context.Aulas
    .Include(a => a.Professor).Include(a => a.LocalAula).Include(a => a.Aluno)
    .Where(a => a.Status == PoliticaAula.Confirmada
             && a.DataHora > agora
             && a.DataHora <= agora.AddHours(24)
             && a.AlunoId != null
             && a.Aluno!.ExcluidoEm == null)
    .ToListAsync(ct);
```

⚠️ **`Confirmada` e só ela.** `PoliticaAula.ContaComoAtiva` inclui `Pendente`, e `Pendente` aqui é
"o professor ainda não aceitou" — mandar "sua aula é amanhã" pra uma aula que pode ser recusada é
prometer o que o sistema não tem. `Cancelada`, `Recusada`, `A recuperar` e `Faltou` ficam fora por
construção.

⚠️ **Conferir a consulta com `ToQueryString()` contra Npgsql** antes de commitar: o EF InMemory não
valida SQL, e esta navega por `a.Aluno.ExcluidoEm`. Padrão em `TraducaoDasConsultasDePalpiteTests`.

⚠️ **Turma não vira enxurrada pro professor.** Uma turma de 3 alunos são **3 linhas de `Aula`** com
o mesmo `TurmaId`, mesmo horário e mesmo professor. Cada aluno recebe o dele; o professor recebe
**um** por horário — agrupado por `(ProfessorId, DataHora)`. Sem isso, a turma de terça manda 3
avisos idênticos pro mesmo celular.

⚠️ **Aluno avulso (`AlunoId == null`) não recebe nada** e não tem como receber: ele não tem conta,
não tem push, não tem caixa de entrada. Quem cobre esse caso hoje é o botão manual do professor
(`Services/ConviteDaAulaMarcada`), e ele continua sendo a resposta.

### 4. Os textos

| Marco | Pra quem | Título | Corpo |
|---|---|---|---|
| 24h | Aluno | Sua aula é amanhã | Aula com {professor} amanhã, {dd/MM} às {HH:mm}, em {local}. Se não puder ir, desmarque pelo app. |
| 24h | Professor | Você tem aula amanhã | {aluno} amanhã, {dd/MM} às {HH:mm}, em {local}. |
| 1h | Aluno | Sua aula é daqui a pouco | Aula com {professor} às {HH:mm}, em {local}. |
| 1h | Professor | Sua próxima aula é daqui a pouco | {aluno} às {HH:mm}, em {local}. |

Link: `/Aulas/MinhasAulas` (aluno) e `/Aulas/MinhaAgenda` (professor) — é onde está o botão de
desmarcar, e aviso que leva pra tela sem o botão não é aviso (lição de 05/08).

---

## As 4 decisões que são suas

| # | Pergunta | O que eu recomendo, e por quê |
|---|---|---|
| 1 | **Escopo**: só a aula marcada com professor (`Aula`), ou também o jogo-aula (`JogoAula`) e a Raquete Livre? | **Só `Aula` agora.** É onde alguém combina hora com outra pessoa e o furo custa o horário do professor. Jogo-aula e Raquete Livre reusam a mesma régua depois, cada um com a sua coluna — mas entram como segunda entrega, não de brinde |
| 2 | **Quem recebe**: só o aluno, ou aluno + professor? | **Os dois, com o professor agrupado por horário.** O professor é quem perde o horário quando o aluno não aparece, e o de 1h é justamente o que faz ele sair de casa. Se achar demais, o professor recebe só o de 24h |
| 3 | **Canal** | **24h no app + WhatsApp (`AppEWhatsApp`); 1h só no app (`AppSemEmail`).** O de 24h passa nos três critérios (pessoal, urgente, acionável: dá pra desmarcar dentro do prazo) e é o único que alcança quem não instalou o app — **push sozinho chega a 5 aparelhos em 154**. O de 1h não tem nada pra decidir, então não vale o canal caro. **Sem e-mail nos dois**: a pessoa marcou a aula, ela já sabe que existe |
| 4 | **Opt-out**: preferência nova (`NotificarLembreteDeAula`) na tela de Preferências, na mesma migration? | **Sim, nascendo ligada.** Aviso que não se desliga é o que faz a pessoa desligar TODOS — e a coluna sai na mesma migration, custo zero agora e migration nova depois |

---

## O que fica de fora (de propósito)

- **Aula `Pendente` que o professor não respondeu.** Cutucar o professor sobre solicitação parada é
  outro aviso, com outra régua (e o push de "nova solicitação" já existe). Achado registrado, não
  resolvido aqui.
- **Escolher a antecedência por professor** ("eu quero 2h, não 1h"). Marco fixo até alguém pedir:
  configuração que ninguém pediu é a complexidade que o `CLAUDE.md` manda pular no degrau 1.
- **Reenvio quando a aula muda de horário.** Editar a aula já avisa o aluno na hora
  (`EdicaoDeAula.PrecisaAvisarAluno`). ⚠️ **Mas o marco gravado continua lá**: aula que mudou de
  amanhã pra semana que vem ficaria com `UltimoLembreteEnviado = 24` e **não levaria o lembrete
  novo**. Resolve-se zerando a coluna quando `DataHora` muda — uma linha na edição, e ela entra
  nesta entrega.

---

## Plano de execução (depois do aprovado)

Na ordem, e cada teste **visto vermelho antes** (Regra 1):

1. `LembreteDaAulaTests` — a régua: marco devido, marco já enviado não repete, dois marcos vencidos
   viram um só, janela civilizada só no de 24h, texto que conta o tempo de verdade.
2. `Services/LembreteDaAula.cs` — a régua.
3. `VarreduraDoLembreteDeAulaTests` — a varredura inteira: acha só a `Confirmada`, ignora cancelada
   e pendente, avisa os dois lados, **um aviso por horário pro professor numa turma de 3**, grava o
   marco, não reenvia na passada seguinte, ignora aluno avulso e conta excluída.
4. A coluna + migration `LembreteDaAula` (worktree limpo, `has-pending-model-changes` limpo).
5. `Services/LembreteDaAulaBackgroundService.cs` + registro no `Program.cs`.
6. Zerar o marco na edição de horário + o teste que trava isso.
7. `ToQueryString()` contra Npgsql na consulta nova.
8. Suíte inteira verde + os conferidores JS, `STATUS.md` atualizado, PR.

Estimativa: ~15 testes novos, ~250 linhas de produção, 1 migration de 1 coluna (2 se a decisão 4 for
"sim").
