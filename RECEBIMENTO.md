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
- **No Pix o dinheiro cai na hora.** Débito 3 dias, crédito **32 dias**. (Boleto foi desligado
  em 10/08/2026 — o sistema não emite mais.)
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
| Pelo site, todas as formas | 15% (mas quem pagar por Pix custa 10%) |

Aula e jogo: 10%, mínimo R$ 1. Torneio tem mínimo de R$ 4.

---

## 2. O Período de Avaliação — resolvido, com um limite que sobra

**Conferido em 03/08/2026 na API de produção:** nossa conta está `APPROVED` nos quatro itens
(comercial, bancário, documentação e geral). Não há teto de emissão pesando sobre ela.

⚠️ **Mas ter a conta aprovada NÃO isenta do Período de Avaliação de subcontas** — a aprovação
é o **pré-requisito de acesso** ao recurso, e o período começa quando a **primeira subconta**
é criada. Isso foi confirmado pelo próprio suporte do Asaas depois de eu ter concluído o
contrário lendo a documentação.

Os números oficiais, contados a partir da primeira subconta:

| Limite | Valor |
|---|---|
| Subcontas que a conta-mãe pode criar | **10** |
| Cobranças **emitidas** por subconta | **R$ 2.000** |
| Duração | **60 dias** |

### O que disso pega o Padelizou, e o que não pega

**Os R$ 2.000 NÃO pegam.** O texto do Asaas diz "a subconta que atingir R$ 2.000,00 não
poderá **emitir** novas cobranças" — e no Padelizou o organizador **nunca emite nada**. Quem
emite é a conta-mãe, com split pra carteira dele. Um torneio de R$ 9.600 deixa o contador da
subconta dele em zero.

Testado no sandbox em 03/08: **R$ 3.600 de split acumulados** numa subconta nova sem nenhum
documento enviado, sem uma recusa. (Sandbox pode não aplicar o período — é evidência forte,
não prova. O que sustenta de verdade é a palavra "emitir" no texto oficial.)

A única interferência possível: se a subconta ficar bloqueada porque o organizador emitiu
cobranças **por conta própria**, fora do Padelizou, aí o split pra ela falha.

**As 10 subcontas PEGAM.** É o limite real: do 11º organizador em diante a criação automática
falha nos primeiros 60 dias. A tela cai sozinha no caminho manual (ver
[MANUAL-CONTA-RECEBIMENTO.md](MANUAL-CONTA-RECEBIMENTO.md)), e há teste cobrindo isso.

**Não criar subconta "pra começar a contagem":** os 60 dias correm a partir da primeira, e o
prazo vencendo bloqueia por si só. A primeira deve ser de um organizador real, pra que a
análise do Asaas comece com cenário de verdade.

Também confirmado: encerrar o Período de Avaliação **não bloqueia saldo nem impede saque** —
no pior caso a subconta para de emitir cobrança nova; o que já entrou continua dela.

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

### O que ainda vale perguntar

1. Depois dos 60 dias e da análise, **o teto de 10 subcontas sobe pra quanto?** É o número
   que decide se o caminho automático serve pro lançamento ou só pros primeiros.
2. **White label** (o titular não ver a marca do Asaas em lugar nenhum) exige aprovação do
   gerente — vale pedir junto.
3. O envio de documentos pode ser **embutido na nossa tela**, ou o titular sempre recebe um
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
