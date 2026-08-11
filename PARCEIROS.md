# Programa de Parceiros: quem traz cliente ganha % da comissão

🟢 **CONSTRUÍDO EM 11/08/2026, AINDA NÃO PUBLICADO.** A regra está escrita aqui e implementada em
`/Admin/Leads` (o registro da indicação) e `/Admin/Comissoes` (a conta). Falta o botão de "paguei"
e o deploy.

O problema que ele resolve: o Felipe faz o sistema, mas vender não é o forte dele. O pipeline
atual (6 grupos de torneio, 3 clubes, 2 professores) veio todo de relação pessoal, e relação
pessoal não escala. A saída é pagar quem sabe vender — **só quando a venda vira dinheiro na
conta**.

⚠️ **A comissão do parceiro sai da fatia do Padelizou, NUNCA do preço do cliente.** O organizador
que paga 10% continua pagando 10%, com ou sem parceiro no meio. Um produto com dois preços é um
produto que perde o cliente que descobre o outro preço.

---

## A base de cálculo é `Pagamento.Comissao`, não "lucro"

Foi a primeira decisão, e é a que evita briga todo mês. "Percentual do lucro" exige subtrair
custos, e a discussão sobre o que entra na conta nunca acaba. A comissão da plataforma já é uma
**coluna do banco**, gravada no instante em que o pagamento confirma, igual pros dois lados.

⚠️ **A comissão bruta não é o que entra.** Num torneio de 60 duplas × R$ 150 (R$ 9.000), os 10%
são R$ 900 — mas são **60 cobranças Pix a R$ 1,99 = R$ 119** que saem da nossa fatia, não da do
organizador. Entram R$ 781. A mordida do gateway é de ~13% da comissão no Pix e pior no cartão. **O
parceiro é pago sobre a comissão BRUTA** (é o número que ele consegue conferir), e os percentuais
abaixo já foram escolhidos sabendo disso.

---

## Tabela de comissão

**Toda venda é a mesma régua: 20% na entrada, 10% pelos 12 meses seguintes.** O que muda de um
produto pro outro é só o que conta como "a entrada".

| Produto | Sobre o quê | A entrada é… | 1ª venda | Recorrente | Prazo |
|---|---|---|---|---|---|
| **Torneio** | inscrições e taxa do externo | a 1ª **edição** (não a 1ª cobrança) | **20%** da comissão dela | **10%** das edições seguintes | **12 meses** |
| **Professor** | **só a mensalidade** | a 1ª **mensalidade** paga | **20%** da comissão dela | **10%** das seguintes | **12 meses** |
| **Clube** | **só a mensalidade** | a 1ª **mensalidade** paga | **20%** da comissão dela | **10%** das seguintes | **12 meses** |

O dobro na entrada paga o trabalho de verdade, que é a primeira conversa. O 10% depois é a cauda —
não é salário, é o lembrete de que aquele cliente é dele.

⚠️ **A COMISSÃO É SÓ DA FRENTE QUE ELE VENDEU.** Quem vendeu um **torneio** não ganha da
mensalidade de professor da mesma pessoa — aquela venda não foi dele, e pode ter sido de outro
parceiro. Um organizador que também dá aula gera duas receitas independentes, e cada uma pertence
a quem a trouxe (ou a ninguém).

⚠️ **NO PROFESSOR E NO CLUBE, A FRENTE É SÓ A MENSALIDADE.** Taxa de aula e taxa de reserva **não
entram**: o que o parceiro vendeu foi a assinatura, não o movimento de alunos ou de quadra do
cliente. Duas consequências a aceitar de olhos abertos:

- **Um professor vale ~R$ 65 ao parceiro no ano inteiro** (20% de R$ 49,90 + 10% de onze
  mensalidades), contra **R$ 135 a R$ 330 de UM torneio**. Os parceiros vão atrás de torneio — e
  é onde está o dinheiro mesmo.
- 🛑 **Lead de clube fechado hoje rende ZERO**, porque **a mensalidade de clube não existe no
  código** (o preço nem foi fechado). Não é bug: não há o que comissionar. Quem vender clube antes
  do plano existir precisa saber disso — ou o acerto é combinado por fora.

⚠️ **E só do que o cliente USOU.** A comissão nasce do pagamento **confirmado**, nunca do faturado,
e **estorno derruba a parte do parceiro junto** — inclusive o parcial, na proporção do que voltou.

📌 **Professor que fica no Avulso pra sempre não rende nada.** Ele nunca paga mensalidade, e a
taxa das aulas dele não é do parceiro. A tela mostra zero, que é a verdade.

📌 **Por que 20% e não 30% (decisão de 11/08/2026).** A régua nasceu 30/10 e foi corrigida no mesmo
dia, e **o que mudou não foi o percentual — foi o tamanho do torneio.** A conta original usava um
torneio de 32 duplas, que na prática quase não existe: **os reais têm de 45 a 110 duplas**. Nesse
tamanho, 20% da estreia dá **R$ 135 a R$ 330**, e o parceiro continua bem pago com metade do
desembolso no momento em que ainda não se sabe se o cliente vinga.

⚠️ **NADA É VITALÍCIO (decisão de 11/08/2026).** Os 12 meses contam do **primeiro pagamento
confirmado** daquele cliente e valem para os três produtos. Um cliente trazido em março de 2027
para de render em março de 2028, continue ele ativo ou não. A razão é simples: comissão sem fim
transfere para sempre parte da margem de um cliente que a plataforma é quem sustenta — suporte,
servidor e produto seguem custando no ano 3, a venda não.

**O que um torneio vale pro parceiro** (comissão bruta de 10%, "só Pix", inscrição de R$ 150 a
dupla — tamanhos reais, informados pelo Felipe):

| Tamanho | Inscrições | Comissão bruta | Parceiro na 1ª | Parceiro nas seguintes |
|---|---|---|---|---|
| 45 duplas | R$ 6.750 | R$ 675 | R$ 135 | R$ 67,50 |
| 60 duplas | R$ 9.000 | R$ 900 | R$ 180 | R$ 90 |
| 110 duplas | R$ 16.500 | R$ 1.650 | R$ 330 | R$ 165 |

Um grupo de 60 duplas que roda 4 torneios no primeiro ano rende **R$ 450 ao parceiro** (R$ 180 da
estreia + 3 × R$ 90) — e para aí. Dez grupos trazidos ao longo de um ano valem cerca de R$ 4.500,
mas essa esteira **não se acumula sozinha**: pra ganhar de novo no ano seguinte, o parceiro
precisa trazer clientes novos. É de propósito. No "externo 5%" tudo isso cai pela metade; no
"todas as formas 15%", sobe ~50%.

📌 **Ponto a revisar depois de 6 meses:** o recorrente do professor a 10% dá **R$ 5/mês**. Isso
não segura ninguém ligando pra cobrar renovação — e a mensalidade do professor **não é
recorrente** no gateway, ou seja, quem não volta na tela cai pros 10% de taxa sozinho. Se a
retenção de professor virar problema, o recorrente dele é a alavanca óbvia pra mexer, não o
bônus de ativação.

---

## Atribuição: as cinco regras que evitam briga com amigo

Essa é a parte que quebra amizade. Fechada **antes** de convidar a primeira pessoa:

1. **Lead registrado ANTES do contato.** O parceiro cadastra nome + telefone + tipo (torneio /
   professor / clube) no painel *antes* de falar com a pessoa. Quem registra primeiro, leva.
   **Sem registro prévio não existe comissão** — reivindicação retroativa nunca é aceita, mesmo
   sendo verdade. A regra só vale se não tiver exceção.
2. **O pipeline atual não é comissionável.** Loberos, Corneteiros, Golden Point, Nata Padel,
   Chakra, Er Padel, Jonatas Portal, Gabriel "Índio" Reis, Batata Padel. Essa lista entra no
   contrato **no dia em que o programa abrir**, congelada. Sem ela, o primeiro parceiro reivindica
   gente que já era nossa.
3. **Lead vence em 90 dias.** Não fechou, volta pro pote e outro pode registrar.
4. **Quem se cadastra sozinho não tem dono.** Cadastro orgânico, busca no Google, indicação de
   jogador: da casa.
5. **Um cliente, um parceiro.** Sem divisão, sem meio a meio, sem "mas eu também falei com ele".

---

## Quando a comissão nasce e quando o dinheiro sai

- **Só nasce de dinheiro recebido** (`Pagamento.Status == "Confirmado"`), nunca de faturado.
  Cobrança gerada e não paga não vale nada.
- **Estorno derruba a comissão proporcional** — inclusive o parcial (`ValorEstornado`).
- **Fecha dia 30, paga dia 10** do mês seguinte. Os 10 dias são a carência pra estorno aparecer
  antes de o dinheiro sair.
- **Piso de R$ 50 pra sacar.** Abaixo disso acumula pro mês seguinte — ninguém quer fazer Pix de
  R$ 7 e o parceiro não quer receber R$ 7.

⚠️ **BURACO CONHECIDO, e ele é mais estreito do que parecia.** Conferido no código em 11/08: o
"externo 5%" **gera `Pagamento` sim** quando o organizador paga pelo site — é o tipo `TaxaTorneio`,
com `RecebedorId` nulo e o organizador como pagador, e a conta enxerga isso normalmente. O que
**não** aparece é o externo que o admin marca como **pago ou negociado na mão** em
`/Admin/Financeiro` (`Torneio.TaxaExternoPagaEm` / `TaxaExternoNegociadaEm`): aí não nasce
pagamento nenhum, e o acerto com o parceiro é manual. O aviso está escrito na própria tela de
comissões.

---

## Programa de indicação dos próprios clientes (moeda diferente)

O melhor vendedor não é vendedor: é o organizador que já usa e conversa com outro organizador.
Mesmas regras de atribuição, **recompensa diferente**: em vez de dinheiro, **abatimento na
própria taxa**. Indicou um torneio que fechou? A próxima edição dele sai 5% em vez de 10%.

Custo fiscal zero, sem Pix, sem recibo, sem teto do MEI — e converte melhor que parceiro externo,
porque é o Jonatas falando com outro professor sobre uma coisa que ele usa.

---

## Dois avisos práticos

**Teto do MEI.** São R$ 81.000/ano de comissão (só a comissão conta como faturamento, por causa
do split), e **pagar parceiro não abate nada** — MEI não deduz despesa. O programa leva ao teto
mais rápido ganhando menos por real recebido. Não é motivo pra não fazer: é motivo pra tratar o
alerta de 70% do `AlertaMeiBackgroundService` como **hora de migrar pra ME no Simples**, e não
como susto. Ver [[project_padelizou_pagamentos_mei]].

**Vínculo empregatício.** O parceiro é parceiro comercial, não vendedor CLT: sem exclusividade,
sem horário, sem meta obrigatória, sem subordinação. ⚠️ **Como formalizar o pagamento (recibo,
RPA ou nota do parceiro) é pergunta pro contador** — este documento não decide isso.

---

## O contrato de uma página (rascunho pra revisar com o contador)

> **Programa de Parceiros Padelizou**
>
> 1. O Parceiro indica clientes ao Padelizou e recebe percentual **sobre a comissão que o
>    Padelizou efetivamente receber** desses clientes. Nada é cobrado do cliente por causa da
>    indicação.
> 2. **Toda venda paga 20% da comissão da entrada e 10% do que vier depois**, por **12 meses
>    contados do primeiro pagamento confirmado** do cliente. Não há comissão vitalícia.
>    A entrada é a **primeira edição** no torneio e a **primeira mensalidade** no professor e no
>    clube.
> 2.1. A comissão vale **apenas para a frente que o Parceiro vendeu**: quem indicou um
>    organizador de torneio não recebe sobre aulas, mensalidades ou reservas da mesma pessoa.
>    No Professor e no Clube, a comissão incide **somente sobre a mensalidade** — taxas de aula
>    e de reserva de quadra não entram.
> 3. A indicação só é válida se o lead for **registrado no painel antes do primeiro contato**.
>    Leads vencem em 90 dias. Clientes que se cadastram por conta própria não geram comissão.
>    Um cliente tem um único Parceiro.
> 4. Os clientes da lista anexa (pipeline existente em 11/08/2026) **não geram comissão**.
> 5. A comissão nasce quando o pagamento é confirmado e é **cancelada proporcionalmente em caso
>    de estorno**.
> 6. Fechamento no dia 30, pagamento até o dia 10 do mês seguinte, com valor mínimo de R$ 50
>    (abaixo disso acumula).
> 7. **Não há exclusividade, jornada, meta obrigatória nem subordinação.** O Parceiro atua por
>    conta própria e é responsável pelos próprios tributos.
> 8. Qualquer das partes pode encerrar a qualquer momento. As comissões recorrentes já geradas
>    seguem sendo pagas até o fim do prazo de cada cliente.

---

## O que já existe (11/08/2026)

✅ **`/Admin/Leads`** — o registro da indicação, com a regra do "quem registra primeiro leva".
✅ **`/Admin/Comissoes`** — a conta: o já fechado, o que ainda corre no mês, e quanto falta pro fim
dos 12 meses de cada cliente. Regras em `Services/ComissaoDoParceiro`.
✅ **Perfil `IsParceiroComercial`** — o parceiro entra em `admin.padelizou.com.br`, cai direto no
extrato dele e **não alcança mais nada do painel**. Liberado em `/Admin/Administradores`.

⚠️ **De quem é o cliente num pagamento tem DUAS respostas**, e o cálculo depende disso: em
inscrição, aula e quadra o cliente é o **`RecebedorId`** (o organizador, o professor, o dono do
clube); em mensalidade e taxa do externo o `RecebedorId` é **nulo** (o valor inteiro é nosso) e o
cliente é **quem pagou**. Buscar só por um dos dois perderia frentes inteiras em silêncio.

✅ **Botão "Paguei"** — registra o repasse e derruba o saldo. 💸 **Ele NÃO envia dinheiro**: o Pix
é feito no banco, à mão, e a tela diz isso com todas as letras. O acerto é um **saldo**
(ganhou − recebeu), não uma marcação por pagamento: a comissão pinga o mês inteiro e o repasse
cobre tudo que fechou.

⚠️ **O "a pagar agora" NÃO é o saldo** — o mês corrente fica de fora. Ele ainda pode crescer, ou
**encolher** se entrar um estorno antes do fim do mês, e pagar adiantado cria um crédito que só
aparece dois meses depois. Quem recebeu a mais aparece como **adiantado**, nunca como saldo
negativo (negativo faria parecer dívida).

### O que ainda falta

1. **A "1ª mensalidade cheia" do clube** nunca dispara, porque **não existe plano de clube no
   código**. Quando existir, basta o tipo dela entrar em `ComissaoDoParceiro.TiposDeMensalidade`.
2. **A tela do parceiro é só leitura e só dele** — não há como ele registrar o próprio lead. Com
   dois ou três parceiros o registro chega por WhatsApp e o Felipe lança; com cinco, não dá.

---

## Kit do parceiro (o que já existe)

- `Padelizou-Apresentacao.pdf` — 12 páginas, o sistema inteiro por papel (jogador, organizador,
  professor, clube). Gerado em 07/08/2026.
- `Padelizou-Professores.pdf` — material específico de professor.
- **Falta:** roteiro de objeções (principalmente contra o **Gripo**, que é ~2,5× mais barato no
  torneio — ver [[reference_padelizou_concorrente_gripo]]) e um link de torneio-demo pra mostrar
  no celular durante a conversa.
