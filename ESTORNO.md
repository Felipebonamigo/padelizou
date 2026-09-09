# Estorno de inscrição paga — o que fazer

> Escrito em 30/07/2026, antes do primeiro torneio real. É o roteiro pra quando alguém
> que **já pagou** desiste — o caso que vai aparecer no primeiro evento.
>
> **Atualizado em 09/09/2026**: a régua "estorno NÃO tira a inscrição" deixou de valer pra
> metade dos torneios — ver o passo 3 e "Como isso ficou resolvido", no fim.

## Resumo em três linhas

1. O organizador estorna sozinho, na tela: **Pagamentos → Meus → botão de estornar** na linha da cobrança.
2. O sistema **devolve o dinheiro** (ou cancela a cobrança, se ainda não tinha sido paga).
3. **Se o torneio é "só confirmo depois de pago", a inscrição some sozinha junto com o dinheiro.**
   Se é "garante a vaga, acerta depois", o estorno mexe só no dinheiro — a dupla continua
   inscrita e marcada como paga, e tem que ser removida à mão, na página do torneio.

---

## Passo a passo

### 1. Achar a cobrança
`Pagamentos → Meus`, filtrando pelo período. Cada linha mostra de onde veio (torneio, categoria)
e o status. Só dá pra estornar cobrança com status **Confirmado** (já paga) ou **Pendente**
(gerada e não paga) — qualquer outro status a tela recusa.

### 2. Estornar
O botão faz uma coisa diferente em cada caso, e a diferença importa:

| Status antes | O que o sistema faz | Status depois |
|---|---|---|
| **Confirmado** (pago) | pede a devolução ao meio de pagamento | **Estornado** |
| **Pendente** (não pago) | apaga a cobrança, então o link de pagamento morre | **Cancelado** |

Só o **dono do torneio/aula** consegue estornar — a checagem é na gravação, não só na tela.

### 3. Tirar a inscrição — só em "garante a vaga, acerta depois"

⚠️ Isto era o passo 3 de **todo** estorno até 09/09/2026. Não é mais.

No torneio **"só confirmo a inscrição depois de pago"**, a dupla só passa a existir quando o
pagamento confirma — é assim que o webhook cria a inscrição (`EfetivarTorneioAsync`). Por
simetria, quando o dinheiro volta (`PAYMENT_REFUNDED`), a inscrição **desfaz sozinha**
(`PagamentoInscricaoService.DesfazerAsync`). Nada a fazer aqui.

No torneio **"garante a vaga, acerta depois"**, a inscrição já existia ANTES de alguém pagar —
o estorno não pode apagar o que não nasceu do pagamento. Aí sim, à mão:

- Página do torneio → **Remover dupla** (se a pessoa desistiu de verdade), **ou**
- Página do torneio → **marcar como não paga** (se ela vai jogar e pagar por fora).

Removendo a dupla, quem estava na **lista de espera é promovido automaticamente** — é por isso
que este passo não pode ser esquecido: enquanto a vaga estiver ocupada por quem desistiu, a
próxima pessoa da fila não entra. (No caminho automático a promoção também acontece sozinha,
dentro do próprio `DesfazerAsync`.)

### Um terceiro caminho: cancelar quem ficou SEM PARCEIRO, na hora de sortear

Desde 09/09/2026, a tela do torneio tem uma saída mais estreita, só pra esse caso: na janela
**"Chaves em Sorteio"**, o organizador cancela ali mesmo a inscrição de quem ficou sem
parceiro (`TorneiosController.CancelarSemParceiro`). Cancela e estorna **na mesma
requisição**, sem esperar o webhook do Asaas: pede a devolução ao gateway
(`PagamentoInscricaoService.EstornarTotalAsync`) e só depois remove a dupla. Sem cobrança real
pra estornar (pago por fora, marcado na mão), só cancela e avisa o organizador pra acertar a
devolução fora do sistema. Fora dessa dupla incompleta e dessa janela, o caminho continua
sendo `Pagamentos → Meus`.

---

## O que esperar do dinheiro

- **Pix:** a devolução costuma cair em minutos, direto na conta de quem pagou.
- **Cartão:** volta na fatura, e o prazo é do banco do jogador — pode levar até duas faturas.
  Não há como acelerar por aqui.
- **Boleto pago:** a devolução vai pra conta bancária do pagador e depende dos dados dele.
  Vale só pras cobranças antigas — boleto foi desligado em 10/08/2026 e não nasce mais nenhuma.
- **A taxa do Padelizou volta junto?** O estorno é do valor **cheio** que o jogador pagou.
  O custo fixo da transação (centavos) não é devolvido pelo meio de pagamento — na prática é
  o nosso prejuízo no cancelamento, não do organizador.
- **Cobrança que nunca foi paga** não movimenta dinheiro nenhum: só deixa de existir.

## Se der errado

- **"O gateway recusou o estorno"** — quase sempre é saldo: a devolução sai do saldo da conta,
  e no cartão o dinheiro só é liberado em ~32 dias. Se a cobrança é recente e paga no cartão,
  provavelmente ainda não há saldo pra devolver. Tentar de novo depois resolve.
- **"Cobrança sem identificação no gateway"** — é cobrança antiga ou registrada à mão, sem
  vínculo com o meio de pagamento. Nesse caso o acerto é por fora (Pix direto pro jogador) e
  a inscrição se resolve na página do torneio.
- **O status não mudou na tela** — o aviso de estorno também chega pelo webhook. Se o botão
  respondeu com sucesso, o estorno foi pedido; recarregar a tela mostra o status novo.

## Onde isso está no código

- Ação: `PagamentosController.Estornar` — checa dono, escolhe devolver × cancelar, grava o status.
  O caminho de estorno TOTAL (chamar o gateway, trocar o status) foi extraído pra
  `PagamentoInscricaoService.EstornarTotalAsync` em 09/09/2026, reusado também por
  `TorneiosController.CancelarSemParceiro` — só o estorno PARCIAL continua só nesta tela.
- Chamada ao meio de pagamento: `AsaasService.EstornarAsync` (`POST /refund` se pago, `DELETE` se pendente).
- Webhook: `PAYMENT_REFUNDED` → Estornado **e** `PagamentoInscricaoService.DesfazerAsync`, que
  desfaz a inscrição — mas só no tipo "confirma com o pagamento" (`TorneioDupla`/
  `TorneioAmericano`). No tipo "garante a vaga, acerta depois" (`TorneioPagarDepois`),
  `DesfazerAsync` só destrava o `Pago` (`DesfazerPagamentoDeInscricaoAsync`) — a inscrição fica
  de pé de propósito. `PAYMENT_DELETED`/`PAYMENT_OVERDUE` → Cancelado.

## Como a decisão pendente foi resolvida

Este documento listava, desde 30/07/2026, uma decisão em aberto: **estornar deveria remover a
inscrição sozinho, ou ficar manual?** Achada em código em 09/09/2026, sem data exata de quando
foi decidida — ela virou as **duas coisas**, uma pra cada tipo de torneio:

- **"Só confirmo depois de pago"**: estornar desfaz a inscrição sozinho. Faz sentido — ela não
  existia antes do dinheiro entrar, então não devia sobreviver a ele saindo.
- **"Garante a vaga, acerta depois"**: continua manual, de propósito — é a regra que sustenta
  exatamente o caso que esta decisão citava como risco, "devolvi por cortesia e ele joga de
  graça". Aqui, estornar não pode tirar a vaga de quem a garantiu antes de pagar.

Ou seja: as duas leituras que este documento apresentava como alternativas venceram as duas,
cada uma no torneio a que ela se aplica.
