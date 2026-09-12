# ProSmart — o placar da quadra e a TV

> 🟡 **DESENHO. NADA IMPLEMENTADO.** Escrito em 12/09/2026. **Zero linha de código existe.**
> Este documento está aqui pra ser aprovado *antes* de virar código — é o que a régua do
> `CLAUDE.md` exige: a integração gera **migration** e cria um **segundo escritor de placar**
> por fora de `PodeControlarPlacarAsync`. Os dois sozinhos já a classificam como
> **architectural**.

## O que a ProSmart é

`tmprosmart.com.br` vende o placar eletrônico que fica **na quadra**, com uma TV ao fundo. O Er
Padel já tem. Eles abriram duas portas:

| Direção | Endereço | O que passa |
|---|---|---|
| **Leitura** | `GET placar.tmprosmart.com.br/pontuation/getCourtInfo?club=<slug>&court=<n>` | status, hora de início, jogadores (nome + foto), placar |
| **Envio** | *(doc ainda não chegou)* | nomes dos jogadores, fotos por URL, categoria, fase, modo de jogo |

O `club` é um slug criado por eles na ativação do cliente (`rs-erpadel`). O `court` é um número
que só eles sabem a qual quadra física corresponde.

⚠️ **A consulta é liberada POR IP**, e o IP é o do nosso VPS (`179.197.233.184`) — o mesmo que
serve `dev` e `prod`. **Não dá pra chamar do localhost nem de uma sessão da web.** Pra ver o
payload de verdade, o `curl` roda de dentro do VPS:

```bash
ssh root@179.197.233.184 \
  'curl -s "https://placar.tmprosmart.com.br/pontuation/getCourtInfo?club=rs-erpadel&court=1"'
```

## A ideia central: o ENVIO é pré-requisito da LEITURA

Parecem duas features independentes. Não são, e a ordem importa.

Se somos **nós** que mandamos os nomes das duplas pra TV, **nós somos donos da string**. Aí a
leitura de volta deixa de ser adivinhação e vira **conferência**: o `players` que o
`getCourtInfo` devolve tem que ser o que mandamos pra aquela quadra. Não bateu → a TV está
mostrando outro jogo → **o robô não escreve nada**.

⚠️ **Sem o envio, casar nome de jogador que veio de fora com `Dupla` do nosso banco é
comparação difusa de string** — "J. Silva" contra "João da Silva Neto" —, e errar significa
gravar o placar de um jogo na partida de outro, calado. **Não implementar a leitura sozinha.**

## As quatro decisões (Felipe, 12/09/2026)

### 1. O `completed` deles NÃO finaliza a partida aqui

O placar vem deles; **o encerramento continua sendo um toque do organizador na Mesa**, que vê
"a quadra diz que acabou: 6/4 6/3 — confirmar?".

⚠️ **Por quê:** `Services/EncerramentoDaPartida` é funil único de propósito ("*quem finaliza
chama isto e pronto*") e faz três coisas de uma vez — chama o `RoboDoChaveamento` (monta a
rodada seguinte, coroa campeão), move o **Padelímetro** dos 4 jogadores e dispara o push "seu
jogo é o próximo". Pendurar isso num campo de terceiro é dar a um sistema de fora o poder de
avançar a chave e mexer no nível dos jogadores. E o caminho de volta do Padelímetro não é
botão, é recálculo.

### 2. A quadra manda até o organizador tocar

Enquanto ninguém mexe na Mesa, o placar da quadra é a verdade. **No instante em que o
organizador digita um placar, a Mesa manda até o fim daquele jogo.**

`Partida.PlacarMarcadoEm` já existe pra essa disputa (nasceu pro "último estado vence" entre
aparelhos offline) e é onde a régua se apoia. Placar que "corrige sozinho" o que o organizador
acabou de digitar é a pior sensação possível no meio do torneio.

### 3. Escolha por torneio, desligada por padrão

O clube ter ProSmart não quer dizer que aquele torneio quer placar automático. Um campo no
torneio; o primeiro que liga é o Felipe, no Er.

### 4. Os nomes sobem pra TV quando o jogo é chamado na Mesa

É o instante em que a Mesa já sabe quadra, duplas, categoria e fase — e é um clique que o
organizador **já dá hoje**. Nada novo pra ele fazer no dia com menos mão livre.

## A máquina de estados é um PAR, nunca um campo só

🛑 **`matchStatus` sozinho NÃO separa quadra vazia de jogo rolando** — `inProgress` cobre os
dois. Quem separa é o `startTime`:

| `matchStatus` | `startTime` | significa |
|---|---|---|
| `inWarmUp` | — | aquecendo |
| `inProgress` | **null** | **não começou** |
| `inProgress` | preenchido | rolando de verdade |
| `completed` | — | acabou |

⚠️ **O estrago de ler só o `matchStatus` não é o placar** — 0x0 numa partida que não começou é
quase inofensivo. É o **`Partida.HorarioInicioReal`**: carimbar "começou" numa quadra vazia
dispara o push *"seu jogo é o próximo"*, o card do Ao Vivo e o aviso de atraso
(`AvisoAtrasoEnviadoEm`), sozinho, no sábado de manhã. Este é o primeiro teste a nascer
vermelho.

## A hora vem em epoch UTC e este projeto grava hora LOCAL

Eles mandam milissegundos desde a época, em UTC (`1789067564021` = 10/09/2026 19:12:44 UTC =
16:12:44 de Brasília — conferido). Aqui as colunas são `timestamp without time zone` no modo
legado do Npgsql, e `DateTime.Now` é hora **local**.

🛑 **A conversão NÃO pode ser `ToLocalTime()`.** O `CLAUDE.md` diz que o fuso do VPS *"não está
garantido em lugar nenhum do código, só documentado no `infra/vps/README.md`"* — então
`ToLocalTime()` amarra a correção do dado a uma configuração de máquina que ninguém verifica.
O fuso de São Paulo entra **explícito no código**, com comentário dizendo por quê. No dia em
que o servidor for reprovisionado, o horário de início dos jogos não entra 3 horas errado.

## O que muda no banco (a migration)

Duas colunas, as duas anuláveis (nulo = "este clube/quadra não tem ProSmart", que é o caso de
todo mundo hoje):

- **`Clube.CodigoProSmart`** (`string?`) — o slug da ativação (`rs-erpadel`).
- **`Quadra.NumeroProSmart`** (`int?`) — o `court` daquela quadra.

⚠️ **Anuláveis, e não `int` liso** — a lição que `Clube.Selecionavel` já pagou uma vez: um `int`
nasce **zero** no banco, e zero não é quadra nenhuma.

E um campo no torneio pra decisão 3 (liga/desliga).

### A lista de quadras NÃO vem por e-mail — é tela

O mapeamento `court=1` → "Arena 1" é **configuração do organizador**, não um dado que a ProSmart
manda. A tela mostra, ao lado de cada quadra, **quem está na TV daquele `court` agora**, pra ele
conferir com o olho. É mais confiável que uma lista trocada por mensagem, que envelhece na
primeira quadra renomeada — e errar isso deixa tudo funcionando mostrando o **jogo da outra
quadra**, que só aparece quando um jogador reclama.

## O robô que puxa

No molde de `Services/RankingRsService`, cuja regra de ouro vale inteira aqui: **nunca lançar,
nunca decidir por conta própria.** Servidor deles fora do ar, JSON diferente, nome que não bate
— tudo isso é "não sei", e "não sei" não escreve.

- Ritmo aprovado por eles: ~48 requisições/minuto (4 quadras a cada 5s).
- ⚠️ **Só roda enquanto existe torneio com jogo acontecendo.** Um `BackgroundService` que puxa
  sempre são 48 req/min contra o servidor deles, 24 horas por dia, pra nada.
- Eles ofereceram **webhook**. Fica pra depois: receber webhook é abrir uma porta que **escreve
  placar** no nosso banco — Regra 0, fronteira de confiança nova. Puxar é mais simples e mais
  seguro pra primeira versão.

## O que eu ainda NÃO sei (e o que trava)

| Falta | Trava o quê | Contorno |
|---|---|---|
| **O payload de exemplo com jogo rolando** | o parser e os testes | **o `curl` por SSH acima** — não depende deles responderem |
| **A doc do endpoint de envio** | a metade do envio, e portanto a leitura inteira | nenhum: endereço e corpo não se inventam |
| **Como se autentica o ENVIO** | idem | a leitura é por IP; escrever na TV de um clube precisa de chave com dono |
| **Uma quadra/clube de teste do lado deles** | só o envio | a leitura é read-only e não mexe na TV de ninguém — pode ser desenvolvida sem risco |

⚠️ **Sem o payload, o parser é escrito contra a descrição em vez do JSON** — e aí o teste
confirma o que eu imaginei em vez de travar o que eles mandam, que é exatamente o que a Regra 1
do `CLAUDE.md` proíbe. O `curl` por SSH resolve isso em um minuto; é o primeiro passo.

🛑 **A Regra 3 não tem equivalente do outro lado.** "Testar em `dev` antes de produção" vale pro
nosso lado, mas **a TV deles é sempre real, com gente jogando na frente**. Sem uma quadra de
teste, o primeiro ensaio do envio acontece ao vivo, num sábado. É o motivo de pedir uma.

## Os testes que precisam nascer vermelhos

1. `inProgress` + `startTime` null **não** carimba `HorarioInicioReal` (e portanto não dispara
   push nenhum).
2. `completed` **não** chama `EncerramentoDaPartida` — só oferece a confirmação na Mesa.
3. Placar digitado na Mesa **não** é sobrescrito pela quadra até o fim daquele jogo.
4. `players` que não bate com o que enviamos → **nada é gravado**.
5. Epoch UTC → hora de Brasília, com o fuso da máquina de teste **mudado de propósito** (é o
   único jeito de ver a diferença entre o explícito e o `ToLocalTime()`).
6. Servidor deles fora do ar / JSON estranho → a partida segue como estava, sem exceção.
7. Torneio com a integração desligada → nenhuma requisição sai.

## Histórico

- **10/09/2026** — a ProSmart mandou o GET. Sete respostas deles: `startTime` null = não
  iniciado; `players` traz nome e foto; ~48 req/min liberado; webhook possível; slug criado por
  eles na ativação; consulta fechada pelo nosso IP; e o endpoint de envio existe.
- **10/09/2026** — mais quatro: os valores de `matchStatus`; hora em epoch UTC; o envio
  substitui e não empilha, com comando de limpar separado; IP confirmado.
- **12/09/2026** — as quatro decisões de desenho. Este documento.
