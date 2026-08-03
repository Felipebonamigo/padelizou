# Como o organizador (e o professor, e o clube) recebe o dinheiro

Documento de trabalho. Duas partes: **como funciona hoje** e **o que falta perguntar ao
gerente de conta** antes de mexer no assunto — inclusive um risco com data marcada.

---

## 1. Como funciona hoje

A cobrança nasce na conta do Padelizou e um **split por valor fixo** manda a fatia do
organizador direto pra carteira dele. Só a comissão entra como faturamento nosso — é isso
que segura o MEI dentro do limite anual.

Três consequências que valem escrever, porque não são óbvias:

- **O organizador recebe o mesmo valor em qualquer forma de pagamento.** O split é por valor
  fixo, então o custo do meio de pagamento sai do NOSSO lado. Muda só o prazo.
- **No Pix o dinheiro cai na hora.** Boleto 1 dia útil, débito 3 dias, crédito **32 dias**.
- **O dinheiro cai na carteira dele dentro do meio de pagamento, não na conta do banco.**
  Passar pro banco é um saque — dá pra configurar transferência automática diária, e vale
  dizer isso pra ele, senão vê "recebido" no nosso extrato e não vê nada no banco.

### O que ele precisa fazer

1. Abrir conta no Asaas (grátis, aceita CPF, não precisa de CNPJ)
2. Passar pela verificação de identidade deles
3. Copiar o Wallet ID em Configurações › Integrações
4. Colar em **Perfil › Meus Pagamentos › Receber pelo app**

São 5 passos, 2 deles fora do nosso site, 1 com espera de aprovação. É muita fricção pra
quem organiza padel — e é o motivo do item 3 deste documento.

### Nossa taxa (torneio)

| Forma escolhida pelo organizador | Taxa |
|---|---|
| Por fora (não tocamos no dinheiro) | 5% |
| Pelo site, só Pix | 10% |
| Pelo site, todas as formas | 15% (mas quem pagar por Pix ou boleto custa 10%) |

Aula e jogo: 10%, mínimo R$ 1. Torneio tem mínimo de R$ 4.

---

## 2. ⚠️ O risco com data: período regulatório

A documentação do Asaas descreve um **período regulatório** para contas novas, com teto de:

- **10 subcontas** de titulares diferentes
- **R$ 2.000 emitidos em cobranças por subconta**

E diz que, atingido o teto, **bloqueia** novas cobranças.

Nossa conta foi aberta em **23/07/2026**, então está dentro dessa janela. Um torneio de 32
duplas a R$ 150 por pessoa são R$ 9.600 — muito acima de R$ 2.000.

**Não deu pra apurar na documentação se esse teto pega a nossa conta raiz emitindo com
split**, que é a nossa arquitetura (a subconta não emite nada; quem emite somos nós). Pode
ser que não pegue. Mas se pegar, o primeiro torneio pago trava no meio, com gente inscrita.

### Perguntas exatas pro gerente de conta

1. **Qual o teto de emissão da nossa conta hoje?** Em valor e em quantidade, e até quando
   vale o período de avaliação.
2. Esse teto conta o **valor bruto** das cobranças que emitimos, mesmo com split mandando a
   maior parte pra carteira de terceiro?
3. O que acontece exatamente quando bate o teto — recusa a cobrança nova, ou suspende a
   conta?
4. Dá pra **antecipar a liberação** mandando documentação agora, antes de 08/08?

---

## 3. O caminho pra diminuir os passos: subconta por API

`POST /v3/accounts` cria uma subconta e devolve `walletId` **e** `apiKey` (a apiKey só volta
UMA vez — tem que guardar na hora). Campos obrigatórios: `name`, `email`, `cpfCnpj`,
`mobilePhone`, `incomeValue`, `address`, `addressNumber`, `province`, `postalCode`.

**Nós qualificamos**: só conta com CNPJ pode criar subconta, e o MEI está aberto. Conta de
CPF não poderia.

Com isso o organizador preencheria um formulário **na nossa tela** e nunca sairia do
Padelizou pra caçar um código. Cinco passos viram um.

### Mas não é mágica, e é importante não vender como se fosse

Mesmo com subconta, o titular ainda:

- ativa a conta por um **e-mail** que ele recebe
- envia os **documentos** dele
- espera a avaliação regulatória

O que a subconta elimina é ele **sair do site, achar e digitar um código** — que já é a maior
parte da desistência, mas não é "um clique e pronto".

### O que mais perguntar

5. Nossa conta tem liberação pra **criar subcontas por API**? Precisa de aprovação?
6. E pra **white label** (o titular não ver a marca do Asaas em lugar nenhum)? A
   documentação diz que isso exige aprovação do gerente.
7. O envio de documentos pode ser **embutido na nossa tela**, ou o titular sempre recebe um
   link/e-mail do Asaas?

### O custo do nosso lado

Passaríamos a coletar **documento e endereço** do organizador dentro do Padelizou — hoje só
guardamos CPF. Isso é responsabilidade nova de LGPD e precisa de decisão consciente, não de
"já que estamos mexendo".

---

## 4. O que já foi feito (03/08/2026)

O buraco que existia: **"pelo site" vinha marcado por padrão e nada checava se o organizador
tinha conta conectada.** Sem conta, `PagamentoInscricaoService.PodeCobrar` devolve `false`, a
cobrança não nasce e o torneio roda sem cobrar ninguém — sem erro, sem aviso. Ele descobria
ao ir procurar o dinheiro.

- **Torneio:** as opções "pelo site" nascem **travadas** pra quem não conectou conta, "Por
  fora" já vem marcado, e o servidor **recusa** mesmo que alguém mande o formulário na mão.
- **Aula e quadra:** aqui NÃO se bloqueia — combinar o pagamento na quadra é o jeito normal
  de dar aula. O que faltava era dizer qual dos dois vai acontecer, e agora a tela diz.
- **Tela de recebimento** reescrita: passo a passo numerado, prazo de cada forma de
  pagamento, e o campo deixou de se chamar "Wallet ID".

Regra viva em `Services/ContaDeRecebimento.cs`; testes em `ContaDeRecebimentoTests.cs`.
