# Estorno de inscrição paga — o que fazer

> Escrito em 30/07/2026, antes do primeiro torneio real. É o roteiro pra quando alguém
> que **já pagou** desiste — o caso que vai aparecer no primeiro evento.

## Resumo em três linhas

1. O organizador estorna sozinho, na tela: **Pagamentos → Meus → botão de estornar** na linha da cobrança.
2. O sistema **devolve o dinheiro** (ou cancela a cobrança, se ainda não tinha sido paga).
3. ⚠️ **O estorno NÃO tira a inscrição.** Quem devolveu o dinheiro tem que remover a dupla à mão, na página do torneio.

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

### 3. Tirar a inscrição (o passo que o sistema NÃO faz)
Estornar mexe **só no dinheiro**. A dupla continua inscrita e marcada como paga, então:

- Página do torneio → **Remover dupla** (se a pessoa desistiu de verdade), **ou**
- Página do torneio → **marcar como não paga** (se ela vai jogar e pagar por fora).

Removendo a dupla, quem estava na **lista de espera é promovido automaticamente** — é por isso
que este passo não pode ser esquecido: enquanto a vaga estiver ocupada por quem desistiu, a
próxima pessoa da fila não entra.

---

## O que esperar do dinheiro

- **Pix:** a devolução costuma cair em minutos, direto na conta de quem pagou.
- **Cartão:** volta na fatura, e o prazo é do banco do jogador — pode levar até duas faturas.
  Não há como acelerar por aqui.
- **Boleto pago:** a devolução vai pra conta bancária do pagador e depende dos dados dele.
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
- Chamada ao meio de pagamento: `AsaasService.EstornarAsync` (`POST /refund` se pago, `DELETE` se pendente).
- Webhook: `PAYMENT_REFUNDED` → Estornado; `PAYMENT_DELETED`/`PAYMENT_OVERDUE` → Cancelado.

## ⏳ Decisão pendente do Felipe

Hoje o estorno e a inscrição são **duas ações separadas**, de propósito nenhum — é assim porque
nunca foi decidido. As duas leituras possíveis:

- **Estornar deveria remover a inscrição sozinho?** Fica consistente (dinheiro devolvido = vaga
  livre) e a lista de espera anda na hora. Mas tira do organizador o caso "devolvi por cortesia
  e ele joga de graça".
- **Ou seguir manual, com um aviso na tela** ("essa inscrição continua valendo — remover?").
  Mais passos, menos surpresa.

Enquanto não for decidido, vale a regra deste documento: **estornou, vá remover a dupla**.
