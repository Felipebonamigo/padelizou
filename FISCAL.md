# FISCAL.md — Plano para cobrir o Gripo na parte fiscal

> Escrito em 19/08/2026. Estado de partida: cobertura fiscal ZERO por decisão registrada
> (31/07, "o clube segue emitindo nota pelo que já usa" — `BarController.cs:17-20` e STATUS.md).
> Este documento é o plano para reverter essa decisão de forma consciente e virar receita.
>
> **Fases 1 e 4a já estão em código** (ver "Estado atual" no fim). A análise de riscos, com a
> defesa de cada perigo, também está lá — é a parte a levar para a conversa com a contadora e
> com os clubes.

## ✅ DECISÕES DO FELIPE (19/08/2026)

- **Plano aprovado**: seguir o caminho A (API white-label), com o portão dos 3 clubes
  comprometidos antes de construir as Fases 2–3.
- **Contador**: resolvido em família — a contadora é praticamente uma "segunda mãe".
  Custo desse item: zero. Ela cuida da NFS-e das comissões próprias e, na hora certa,
  da migração.
- **MEI→ME só perto do teto**: a migração NÃO é antecipada. O gatilho é o alerta de 70%
  que o sistema já manda sozinho (`AlertaMeiBackgroundService`) — quando disparar,
  significa que vários clubes estão assinando, que é exatamente o bom sinal. Ao receber
  o alerta: conversa com a contadora, não pânico (estouro até 20% do teto vira ME em
  1º de janeiro com DAS complementar; acima de 20% retroage — regra a confirmar com ela
  na hora).

## A decisão em uma frase

Não construir motor fiscal próprio: contratar uma **API de emissão white-label** (o
próprio Gripo usa a Focus NFe por trás — está nos termos "tenant-nf" deles), ligar no
módulo de bar/comanda que **já está pronto em código**, e vender como plano de
assinatura de clube em dois degraus — **Gestão** (R$ 99) e **Fiscal** (R$ 199).

## A régua (o que o Gripo faz)

- NFC-e (bar), NFS-e (serviços) e NF-e integradas ao CNPJ do clube — via Focus NFe.
- Plano Professional: R$ 219/clube/mês + módulos, com franquia de 100 NFS-e + 500
  NFC-e/mês; excedente por emissão. Basic tem só NFS-e "parcial".
- TEF integrado (Stone, Sicredi, Vero, Cielo), totem, cozinha, facial.
- O que ele NÃO tem: professor, rede de jogadores, torneios como motor — nossa vantagem.

## Provedor de emissão

Critérios, na ordem: preço por nota em multi-CNPJ (taxa fixa por CNPJ mata a margem em
clube pequeno), NFS-e Padrão Nacional, NFC-e com contingência, sandbox grátis, white-label.

| Provedor | Leitura |
|---|---|
| **Nuvem Fiscal** ← começar | Cota única pra todos os docs, multi-CNPJ, tier grátis (~20–50 notas/mês) pra desenvolver e pilotar. Player mais novo: validar SLA na proposta. |
| **PlugNotas** (Tecnospeed) | Feita pra software house (2.000+), cobra por nota, Padrão Nacional em 2.000+ cidades. Plano B forte / provável escolha na escala. |
| **Focus NFe** | A engrenagem do Gripo; 3.000+ municípios, município novo por R$ 199 fixo. Modelo por CNPJ tende a custar mais no nosso caso. |
| eNotas / NFE.io | Fortes em NFS-e de infoproduto; fracas pro PDV de balcão. Descartadas por ora. |

Faixa de mercado apurada (ago/2026, via busca — sites bloqueados no ambiente, **confirmar
em proposta comercial**): entrada R$ 89–129/mês com 100–250 notas e excedente
R$ 0,60–0,75; em volume negociado o custo por nota cai pra ~R$ 0,10–0,40.

⚠️ Desenhar a integração atrás de `IEmissorFiscal` própria — trocar de provedor sem
reescrever o produto.

## Pré-requisitos do clube (não são nossos)

- CNPJ ativo com atividade compatível. Clube MEI na prática não emite NFC-e: o plano
  Fiscal mira clube ME/Simples — que é quem tem bar de verdade.
- Certificado digital A1 (e-CNPJ, ~R$ 150–250/ano, custo do clube).
- Credenciamento NFC-e na SEFAZ do estado + CSC; inscrição municipal pra NFS-e. O
  contador do clube faz; a gente entrega o passo a passo (1 página por estado, RS primeiro).

## Roadmap em fases

- **Fase 0 — a casa própria (~1 semana + contadora).** NFS-e das comissões e mensalidades
  do PRÓPRIO Padelizou (como MEI sai grátis pelo Emissor Nacional; automatizável depois no
  webhook `PAYMENT_CONFIRMED`). Pauta com a contadora: nota das comissões, formalização do
  repasse a parceiros e a cláusula de responsabilidade do contrato do plano Fiscal.
  ⚠️ **A migração MEI→ME NÃO entra aqui** — decisão do Felipe: ela só acontece perto do teto,
  e o gatilho é o alerta de 70% que o `AlertaMeiBackgroundService` já manda sozinho. Se ele
  disparar, é porque vários clubes assinaram — o problema certo pra se ter.
- **Fase 1 — dados fiscais. ✅ FEITA em 19/08/2026** (migration `CadastroFiscalDoClubeEDoProduto`,
  22 testes novos). Entregue: CNPJ com dígito verificador, razão social, IE/IM, regime e
  endereço fiscal no `Clube`; `TipoFiscal` + NCM/CFOP/CEST/GTIN/unidade/origem/CSOSN no
  `ProdutoBar`; "CPF na nota" na `Comanda`; checklist "falta o quê pra emitir" por documento;
  palpite de NCM por marca; código IBGE do município vindo do CEP; aba fiscal atrás do
  `Fiscal__Habilitado`. Três decisões tomadas no caminho, todas registradas no código:
  **(1)** certificado A1 nunca é guardado aqui — sobe direto pro provedor;
  **(2)** dois interruptores separados (`Bar__Habilitado` e `Fiscal__Habilitado`), porque são
  dois planos de assinatura; **(3)** o sistema SUGERE e nunca decide tributação — a
  responsabilidade é do clube, e isso está escrito na tela, não só no contrato.
- **Fase 2 — NFS-e dos serviços (~2–3 semanas).** Reserva, aula, mensalidade, no evento de
  pagamento. Padrão Nacional torna essa a parte mais fácil. Arrumar o lastro que falta:
  valor no mensalista e na `MensalidadeGrupo`, tomador na reserva de balcão.
- **Fase 3 — NFC-e do bar (~4–6 semanas).** Emissão no fechamento da comanda (opcional por
  venda), DANFE-NFC-e térmico/browser com QR, série/numeração por clube, cancelamento
  fiscal amarrado ao cancelamento de comanda (prazo curto, ~30 min na maioria dos estados),
  contingência offline. Piloto em 1 clube real no RS antes de abrir estado a estado.
- **Fase 4a — BILLING DA ASSINATURA. ✅ FEITO em 19/08/2026** (migration `AssinaturaDoClube`,
  35 testes novos). Planos Rede (grátis) / Gestão / Fiscal, com 15 dias de teste e 7 de
  carência; cobrança por Pix direto (sem taxa de gateway) ou fatura; tela do plano pro dono;
  registro manual pro admin; avisos de vencimento por push. **O bar virou plano pago**: quem
  não assinou vai pra tela de assinatura em vez de um 403.
  Decisões registradas no código: **(1)** quatro colunas no `Clube` e não uma tabela nova —
  o histórico de ciclos já é a tabela `Pagamento`; **(2)** a conta de tempo virou
  `CicloDeAssinatura`, compartilhada com o professor, pra não haver duas contas de dinheiro;
  **(3)** a cobrança aberta é do CLUBE e não de quem clicou, senão dono e sócio geram uma
  cada e pagam as duas; **(4)** "em construção" nunca vira "sem plano" — não se vende um
  Fiscal que ainda não emite.
- **Fase 4b — pacote do contador (~1–2 semanas).** Export mensal por clube: CSV de vendas do
  bar (hoje não existe), ZIP de XMLs, relatório de notas emitidas/canceladas/rejeitadas.
  Medidor de franquia de notas e bloqueio suave no excedente.
- **Fase 5 — TEF: adiar.** Item mais caro do catálogo do Gripo e o menos pedido em clube
  pequeno. Reavaliar quando cliente pagante pedir.

## Planos de assinatura

| Plano | Preço | O que tem |
|---|---|---|
| Clube Rede (atual) | R$ 0 | Torneios, ranking, reservas, rede — o motor de aquisição segue grátis. |
| Clube Gestão | R$ 99/mês ou R$ 990/ano | Bar completo + financeiro. Margem ~100%. |
| **Clube Fiscal** | R$ 199/mês ou R$ 1.990/ano | Gestão + NFS-e e NFC-e com franquia 100 NFS-e + 400 NFC-e/mês; excedente R$ 0,30/nota. A1 por conta do clube. |

Os preços vivem em configuração (`PlanoClube__MensalidadeGestao` e vizinhos, ver
`Services/PlanoDoClube`) — renegociar com um clube não exige republicar o site. O anual dá
dois meses de desconto nos dois planos, e a economia é **calculada**, nunca escrita na tela.

Ancoragem: Gripo Professional R$ 219 + módulos à parte. Nosso Fiscal com o fiscal DENTRO
e a rede junto é comparável no preço e maior em valor. Não competir baixando o % do
torneio (decisão já registrada) — competir empacotando o que o Gripo não tem.

## Economia unitária (cenário, validar com proposta)

Assumindo ~250 notas/clube/mês a R$ 0,10–0,40 e plano médio R$ 200: margem bruta ~70–90%.
15 clubes Fiscal = R$ 3.000/mês. 50 clubes = R$ 10.000/mês e contrato de volume melhor.

⚠️ **Consequência tributária planejada**: 15 clubes = R$ 36 mil/ano de assinatura somados
à comissão → o teto do MEI (R$ 81 mil) estoura POR DESIGN. Migração pra ME no Simples
(~6–15,5% conforme anexo/fator R — validar com contador) entra no custo desde o dia 1.

## Riscos — e a defesa de cada um

Listar os perigos aqui não é desaconselhar o plano: é o mapa por onde ele já foi desenhado.
Cada linha abaixo tem uma defesa que já existe no código ou está fixada no roadmap. **O custo
real do plano Fiscal é operacional e jurídico, não técnico** — e é por isso que ele vale
R$ 199/mês: se emitir nota fosse fácil e sem perigo, todo sistema de quadra faria, e não seria
diferencial de ninguém. É a chatice que constrói o fosso.

### 1. Um bug nosso vira multa DELES — o perigo nº 1

Não tem paralelo no resto do sistema. Padelímetro errado gera reclamação no WhatsApp; nota
duplicada, valor errado ou cancelamento fora da janela da NFC-e (~30 min) gera **multa no CNPJ
do clube**, e isso não se resolve com pedido de desculpas.

- **Defesa**: emissão idempotente (mesma disciplina do webhook do Asaas, que já é idempotente
  por `ReferenciaId`); **piloto único no RS por pelo menos um mês** antes do segundo clube;
  nunca abrir NFC-e para todos de uma vez.

### 2. O suporte acontece no horário em que o Felipe não trabalha

Bar de clube fatura sexta e sábado à noite — exatamente quando a SEFAZ cai, o certificado
vence e ninguém do provedor atende.

- **Defesa, e é REGRA DE DESENHO da Fase 3**: **a venda nunca trava por causa da nota.** A
  comanda fecha sempre; a nota sai de forma assíncrona e, se falhar, cai numa fila de
  pendências para resolver na segunda. Acoplar venda e emissão transformaria o Felipe em
  plantonista fiscal de todos os clubes.

### 3. A confusão de responsabilidade

Juridicamente NCM, alíquota e regime são do contribuinte (o clube). Na cabeça do cliente, "o
sistema emitiu errado". Nossos palpites de NCM ajudam a vender e criam exposição.

- **Defesa**: o sistema **sugere e nunca decide** (`FiscalDoProduto`, com o aviso na TELA e
  não só no contrato); contrato do plano Fiscal revisado pela contadora **antes do primeiro
  cliente pagante**; e a resposta padrão para dúvida tributária do clube é sempre "confirme
  com o seu contador" — nós não orientamos tributação.

### 4. O cliente vai pedir o que não podemos dar

Mais cedo ou mais tarde um clube pergunta se dá para "emitir só metade". A emissão é opcional
por venda de propósito (quem decide o que emite é o contribuinte), mas o Padelizou **não pode
aconselhar nem automatizar subemissão**.

- **Defesa**: cláusula explícita no contrato do plano.

### 5. Dependência do provedor e o chão mudando

Preço, SLA ou fim do provedor; e a reforma tributária (CBS/IBS) muda layouts de nota em fases
nos próximos anos.

- **Defesa**: interface `IEmissorFiscal` própria (trocar sem reescrever) + **exigir no
  contrato do provedor a exportação dos XMLs** (guarda obrigatória de 5 anos é do clube). A
  manutenção recorrente de layout é custo previsto, não surpresa.

### 6. Contaminação da marca

O fiscal é o módulo com maior chance de gerar ligação brava — e a raiva não mira "o módulo
fiscal", mira "o Padelizou". Problema de nota mal resolvido pode custar o cliente de torneio
junto.

- **Defesa**: a mesma do item 1 — errar pequeno, perto e com cliente que conhece o Felipe.

### 7. Riscos menores, já cobertos

- **NFC-e é homologação POR ESTADO**: RS primeiro, abrir estado a estado conforme cliente
  pagante — nunca "Brasil inteiro" de largada.
- **LGPD**: CPF na nota exige política de retenção. ✅ O certificado A1 **não é guardado por
  nós** (sobe direto pro provedor) — essa exposição foi eliminada no desenho da Fase 1.
- **Franquia de notas sem medidor** (Fase 4b): um clube que emitir muito além da franquia come
  a margem. Não é risco no piloto (volume conhecido); **não ligar o Fiscal para vários clubes
  antes da 4b**.
- **Valores de mercado não confirmados** (sites bloqueados na pesquisa): nada de fixar preço
  antes das propostas comerciais.

### As três defesas inegociáveis antes do primeiro cliente pagante

1. Contrato revisado pela contadora (responsabilidade tributária + subemissão).
2. Desenho assíncrono: **a venda nunca trava por causa da nota**.
3. Piloto único no RS rodando **um mês** antes de abrir o segundo clube.

### O que mudaria a recomendação

- **Os 3 clubes não assinarem** → o mercado respondeu; fica a Gestão a R$ 99 (margem ~100%,
  risco quase zero) e nada foi perdido. É para isso que o portão existe.
- **O Felipe não querer conviver com chamado de fim de semana** → é o único custo que não se
  configura. Aí a opção honesta é Gestão sozinha + export pro contador, sabendo que ela não
  cobre o Gripo por inteiro.
- **A contadora reprovar o contrato** → ela tem veto.

## Estado atual (19/08/2026)

✅ **Fase 1** — cadastro fiscal do clube e do cardápio (migration `CadastroFiscalDoClubeEDoProduto`).
✅ **Fase 4a** — billing da assinatura de clube (migration `AssinaturaDoClube`).
⏸️ **Fases 2, 3 e 4b** — travadas no portão dos 3 clubes comprometidos por escrito.

Nada disso está visível em produção: as colunas são todas nulas e as telas ficam atrás de
`Bar__Habilitado` e `Fiscal__Habilitado`.

## Próximos passos

1. **Pedir as 3 propostas comerciais** (Nuvem Fiscal, PlugNotas, Focus NFe): multi-CNPJ, preço
   por nota em volume, tier grátis, white-label, SLA e exportação dos XMLs.
2. **Conversa com a contadora**: NFS-e das comissões próprias (Fase 0) e o contrato do plano
   Fiscal — responsabilidade tributária e subemissão.
3. **Escolher o clube piloto no RS** (CNPJ ME, bar ativo, gente que conhece o Felipe).
4. **Vender o plano Gestão (R$ 99)** — não depende de nada disso: o código está pronto, é
   ligar `Bar__Habilitado` quando o piloto estiver combinado.
5. **Buscar os 3 compromissos por escrito** do plano Fiscal. Sem eles, as Fases 2–3 não começam.

### Decisões ainda abertas

- **Comissão de parceiro sobre assinatura de clube**: hoje o programa paga 20%/10% sobre a
  comissão do Padelizou, e a assinatura de clube ficou **de fora** de propósito — mudar a
  economia dos parceiros por conta própria seria errado. Se um parceiro trouxer um clube que
  assina R$ 199/mês, ele ganha? Decisão do Felipe (é uma linha de código).
- **Preço por clube × por quadra**: o STATUS.md tinha âncora de R$ 59–99 **por quadra**; o
  código foi para preço fixo por clube (mais simples de vender, é o modelo do Gripo). Para uma
  arena de 8 quadras, R$ 199 fixo pode estar barato. Reavaliar quando aparecer a primeira
  arena grande — o registro manual já permite cobrar diferente de quem negociar.
- **Se os 3 clubes pedirem desconto**: dar **desconto de fundador com prazo** (ex.: R$ 99 nos
  3 primeiros meses do Fiscal), nunca rebaixar a tabela. Desconto expira; tabela rebaixada
  nunca mais sobe.
