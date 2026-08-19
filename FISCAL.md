# FISCAL.md — Plano para cobrir o Gripo na parte fiscal

> Escrito em 19/08/2026. Estado de partida: cobertura fiscal ZERO por decisão registrada
> (31/07, "o clube segue emitindo nota pelo que já usa" — `BarController.cs:17-20` e STATUS.md).
> Este documento é o plano para reverter essa decisão de forma consciente e virar receita.

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
assinatura de clube em dois degraus — **Gestão** (R$ 99) e **Fiscal** (R$ 199–229).

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

## Riscos

- **É reverter decisão de produto** (31/07: "PDV fiscal é outro produto"). O custo real
  não é código, é SUPORTE: nota rejeitada na SEFAZ às 21h de sábado vira chamado nosso.
- NCM/alíquota errados são responsabilidade do contribuinte (clube) — contrato do plano
  precisa dizer isso com todas as letras (contador/advogado).
- NFC-e é homologação POR ESTADO: RS primeiro, abrir conforme cliente pagante.
- LGPD: CPF na nota e A1 armazenado exigem cifra e política de retenção.
- Valores de mercado não confirmados (sites bloqueados na pesquisa): nada de fixar preço
  de plano antes das propostas comerciais.

## Próximos 14 dias

1. Decisão do Felipe: reverter (ou não) a decisão de 31/07. Sem isso o resto é gaveta.
2. Proposta comercial: Nuvem Fiscal, PlugNotas e Focus NFe (multi-CNPJ, volume, white-label, SLA).
3. Contador: migração MEI→ME, NFS-e das comissões próprias, contrato do plano Fiscal.
4. Escolher 1 clube piloto no RS (CNPJ ME, bar ativo) e combinar o teste.
5. Começar a Fase 1 (dados fiscais no cadastro) — não depende de provedor nem de contador.
