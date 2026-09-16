# Lembrete da aula (24h e 1h antes) — desenho, decisões e o que foi construído

> **Status: APROVADO e IMPLEMENTADO em 16/09/2026.** Nasceu como desenho para aprovação — gera
> migration, e pelo critério do `CLAUDE.md` isso é `architectural`, que é design escrito e
> aprovado ANTES de qualquer código. **As 4 decisões foram tomadas pelo Felipe no mesmo dia**
> (tabela abaixo) e só então o código saiu.

---

## De onde veio

Maickel, 16/09/2026, pelo WhatsApp:

> *"Só talvez faria um 'push' — avisando 24hs e 1hora antes da aula — para as opções de aula de padel."*

Era a única parte do sistema com hora marcada que **não avisava ninguém antes**. O torneio tem
lembrete de inscrição não paga, o jogo fixo da panelinha tem o de 24h, a cobrança tem o de 6h — a
aula, que é o compromisso mais pessoal de todos, marcava e calava até o dia.

---

## As 4 decisões do Felipe

| # | Pergunta | Decisão | O que isso mudou no desenho |
|---|---|---|---|
| 1 | **Escopo** | **Aula + jogo-aula** (Raquete Livre fica pra depois) | Duas varreduras e duas colunas de marco, não uma. Eu tinha recomendado só a `Aula` |
| 2 | **Quem recebe** | **Aluno e professor** | O professor recebe **UM aviso por horário**, não um por aluno da turma |
| 3 | **Canal** | **Só o app** (caixa de avisos + push) — `AlcanceDoAviso.AppSemEmail` | Literalmente o que o Maickel pediu. Eu tinha recomendado WhatsApp no de 24h; a escolha é mais conservadora e **não arrisca o chip nem a cota de e-mail** |
| 4 | **Opt-out** | **Sim, nascendo ligado** | `Jogador.NotificarLembreteDeAula`, na tela de Preferências, na mesma migration |

⚠️ **A decisão 3 tem um custo conhecido, e ele está escrito aqui pra não virar surpresa:** push
sozinho alcança **5 aparelhos em 154** (medição de 08/2026). A caixa de avisos (`/Notificacoes`)
alcança todo mundo que **abrir o app** — é o canal que não depende de entrega nenhuma —, mas
ninguém é *avisado* de que ela tem coisa nova. Na prática o lembrete só toca o celular de quem
instalou o app. Se um dia a queixa for "não recebi", o conserto é a decisão 3, não o código.

---

## O que foi construído

### A régua — `Services/LembreteDaAula.cs` (pura, sem banco)

- `Marcos = { 24, 1 }` em horas. A varredura lê `Marcos.Max()` pra limitar a consulta: marco novo
  aqui estica a janela sozinho.
- `MarcoDevido(quando, agora, ultimoMarcoEnviado)` — **quando os dois vencem de uma vez** (aula
  marcada faltando 40 minutos), vale o **mais urgente** e o outro é dado por cumprido. Senão a
  pessoa levaria dois avisos em quinze minutos, um por tick.
- **Madrugada:** o marco de **24h** só sai entre **7h e 22h**; o de **1h não tem janela** — quem
  tem aula às 6h já vai acordar às 5h de qualquer jeito, e segurar esse aviso é não mandá-lo.
- **O texto conta o tempo de verdade, nunca o número do marco.** Quem entra pelo marco de 24h com
  a aula em 15 horas (porque o marco caiu de madrugada e foi segurado) lê *"hoje"*, não *"amanhã"*.

### A varredura — `Services/LembreteDaAulaBackgroundService.cs`

Tick de **15 minutos** (o marco de 1h precisa; o de 24h não se importa), mais uma passada na
subida do app. `VarrerAsync(context, push, agora, ct)` é estática e recebe o `agora` **por
parâmetro** — é o que permite exercitar o percurso inteiro em horas que ainda não chegaram.

| Regra | Por quê |
|---|---|
| Só `Status == Confirmada` | `PoliticaAula.ContaComoAtiva` também aceita "Pendente", e pendente é *"o professor ainda não aceitou"*. Prometer uma aula que pode ser recusada é prometer o que o sistema não tem |
| Agrupa por `(ProfessorId, DataHora)` | A turma são **3 linhas de `Aula`** no mesmo horário. Sem agrupar, o professor levaria 3 avisos idênticos no mesmo minuto |
| O marco é gravado mesmo sem ninguém pra avisar | Aluno avulso, conta excluída, preferência desligada — sem gravar, o varredor tentaria de novo a cada 15 minutos até a aula acontecer |
| Jogo-aula **sem inscrito** não avisa, e o marco fica **nulo** | Não há quem lembrar; e se alguém se inscrever depois, o lembrete de 1h ainda sai |
| Lista de espera do jogo-aula fica de fora | Quem está na espera não tem vaga |
| Horário novo **zera** o marco (`AulasController.Editar`) | A aula que já levou o "é amanhã" e foi remarcada precisa do aviso de novo. Sem zerar, ela ficaria marcada como avisada pra sempre — **e ninguém reclama de um aviso que não chegou** |

### As colunas — migration `20260916131627_LembreteDaAula`

`Aula.UltimoLembreteEnviado` (`int?`), `JogoAula.UltimoLembreteEnviado` (`int?`) e
`Jogador.NotificarLembreteDeAula` (`bool`).

⚠️ **É o MARCO, não a data do envio** — com a data, *"já mandei o de 24h?"* viraria conta de
relógio a cada passada, e um deploy no meio da janela reenviaria tudo. Mesma escolha de
`UltimoLembreteDePagamento`.

⚠️ **O `defaultValue` da preferência foi trocado À MÃO pra `true`**, e é a lição do `= 60` de
10/08/2026: o EF gerou `false`, e `false` é o valor que **toda conta já existente** receberia — a
base inteira nasceria com o lembrete desligado, o contrário da decisão 4. O `= true` do C# só vale
pra objeto novo.

⚠️ **A caixa de Preferências leva um `<input type="hidden" value="false"> DEPOIS dela` e o
parâmetro do POST é `bool?`** — a preferência nasce ligada, e caixa desmarcada não vai no POST.
Sem o par, uma aba aberta antes do deploy religaria o lembrete de quem desligou, a cada
salvamento de qualquer outra preferência. Mesma armadilha do `VerPalpitometro`.

---

## O que fica de fora (de propósito)

- **Raquete Livre** — decisão 1. Reusa a mesma régua no dia em que for pedida.
- **Aula `Pendente` que o professor não respondeu.** Cutucar o professor sobre solicitação parada
  é outro aviso, com outra régua (e o push de "nova solicitação" já existe). Achado registrado.
- **Antecedência configurável por professor** ("eu quero 2h, não 1h"). Configuração que ninguém
  pediu é a complexidade que o `CLAUDE.md` manda pular no degrau 1.
- **Aluno avulso** (sem conta) não recebe e não tem como receber: não tem push, caixa nem
  preferência. Quem cobre esse caso é o botão manual do professor (`Services/ConviteDaAulaMarcada`).

---

## Como isto foi testado

**42 testes novos**, todos vistos vermelhos antes (*"não existe"*, no build) — e os da varredura
conferidos **por mutação**: tirar o `Include` do aluno, afrouxar o filtro de status, remover o
filtro da lista de espera e desfazer o agrupamento da turma deixam **10 testes vermelhos**. Teste
que passa sem testar nada já aconteceu neste projeto.

- `LembreteDaAulaTests` — a régua: marcos, janela da madrugada, texto que conta o tempo de verdade.
- `VarreduraDoLembreteDeAulaTests` — a ligação, **com DOIS CONTEXTOS sobre o mesmo banco**. Com um
  só, o EF InMemory costura `aula.Aluno` e `aula.LocalAula` sozinho pelo rastreador e a varredura
  passaria verde **sem os `Include`** (lição de 16/09/2026).
- `TraducaoDoLembreteDeAulaTests` — as três consultas compiladas contra um provedor **Npgsql** de
  verdade via `ToQueryString()`. O InMemory não traduz SQL, e aqui a falha seria pior que uma
  página 500: o lembrete simplesmente não sairia, calado num log que ninguém olha.

⚠️ **NÃO conferido no app rodando**: esta sessão não subiu o app com banco. O que sustenta é a
suíte (**7.332 verdes**), os 12 conferidores JS e a migration com `has-pending-model-changes`
limpo. **No primeiro deploy vale olhar uma vez**: uma aula marcada pra dali a ~23h deve gerar a
linha em `/Notificacoes` do aluno e do professor, e **não** gerar de novo no tick seguinte.
