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
| **ACBr API** ← **ESCOLHIDA** | **Ganha em todos os cenários, por 2 a 3 vezes** (tabela do console conferida em 24/08/2026 — ver a comparação abaixo). Crédito pré-pago **sem mensalidade**, **créditos que não expiram**, **CNPJs ilimitados em TODAS as faixas** e **guarda de XML inclusa** — este último resolve sozinho o risco nº 5 deste documento. Do Projeto ACBr, referência em componente fiscal no Brasil, e sucessora indicada pela Nuvem Fiscal (API compatível). Emite NF-e/NFC-e, NFS-e, CT-e/MDF-e, DC-e + consulta CNPJ e CEP. Conta gratuita com sandbox. |
| **Focus NFe** (plano B) | A engrenagem do Gripo, provada no nicho, com preço público e previsível — e por isso é a **régua de negociação**. CNPJs ilimitados só a partir do Growth (R$ 548) e do Retail+ (R$ 629,90). Perde para a ACBr em toda a faixa que nos interessa, mas é o porto seguro se a ACBr decepcionar no piloto. 3.000+ municípios; município novo por R$ 199 fixo. |
| **PlugNotas** (Tecnospeed) | Feita pra software house (2.000+), cobra por nota, Padrão Nacional em 2.000+ cidades. **Preço não publicado**: pedir proposta. |
| ~~Nuvem Fiscal~~ ❌ | **SERVIÇO DESATIVADO EM 31/07/2026** (comunicado de 22/04, 90 dias de prazo). Era a primeira recomendação deste documento e não existe mais — ver o aviso abaixo. |
| NFE.io | Tabela conferida: plano Base R$ 1.825/ano = R$ 152/mês para 250 notas com CNPJ ilimitado → **R$ 0,61 por nota**, contra R$ 0,14 da Focus no Growth. Quatro vezes mais cara na nossa faixa. Descartada. |

> ⚠️ **CORREÇÃO DE 24/08/2026 — a primeira recomendação deste documento morreu antes de ser
> contratada.** A Nuvem Fiscal anunciou em 22/04/2026 a desativação do serviço, efetivada em
> **31/07/2026**, com migração indicada para a **ACBr API**. Duas leituras:
>
> **(1) O risco que estava anotado se materializou** — a linha original dizia "player mais
> novo: validar SLA na proposta". Era exatamente esse o perigo, e ele aconteceu. Bom sinal de
> que a régua de avaliação está certa.
>
> **(2) Não custou nada, e é por isso que a ordem do plano importa.** A escolha do provedor
> ficou para depois do portão dos 3 clubes; se tivéssemos integrado "para adiantar", teríamos
> agora um trabalho a refazer. A defesa da interface `IEmissorFiscal` continua valendo — e
> este episódio é a prova de que ela não é preciosismo.

### A tabela real da Focus NFe (conferida em 24/08/2026)

| Plano | Preço | CNPJs | Notas incluídas | Nota adicional |
|---|---|---|---|---|
| Solo | R$ 89,90/mês | 1 | 100 | R$ 0,10 |
| Start | R$ 113,90/mês | 3 (+R$ 37,90 por CNPJ extra) | **100 por CNPJ** | R$ 0,10 |
| **Growth** | R$ 548,00/mês | **ilimitados** | **4.000** | R$ 0,12 |
| Enterprise | consultar | ilimitados | acima de 50 mil notas/mês | — |

Sem taxa de setup, sem fidelidade, 30 dias de teste. Emite NF-e, NFS-e, NFC-e, CT-e, MDF-e,
NFCom e DC-e. ⚠️ **Cada nota emitida OU RECEBIDA conta como uma unidade do plano.**

### A economia unitária, com números de verdade

Premissa: 250 notas por clube/mês, plano Fiscal a R$ 199.

| Clubes | Plano certo | Custo/mês | Custo por clube | Receita | **Margem** |
|---|---|---|---|---|---|
| 1 (piloto) | Start | R$ 128,90 | R$ 128,90 | R$ 199 | 35% |
| 3 (o portão) | Start | R$ 158,90 | R$ 52,97 | R$ 597 | **73%** |
| 10 | Start | R$ 529,20 | R$ 52,92 | R$ 1.990 | **73%** |
| 15 | Growth | R$ 548,00 | R$ 36,53 | R$ 2.985 | **82%** |
| 25 | Growth | R$ 818,00 | R$ 32,72 | R$ 4.975 | **84%** |
| 50 | Growth | R$ 1.568,00 | R$ 31,36 | R$ 9.950 | **84%** |

Três leituras que só apareceram com o número real na mão:

1. **A estimativa de 70–90% de margem estava certa** — o real dá 73% no portão e 84% na
   escala. A conta do plano se sustenta.
2. **O ponto de virada é 11 clubes**: até lá o Start sai mais barato; do 11º em diante o
   Growth ganha, e ele cobre até **16 clubes sem um centavo de excedente**. Ou seja, entre o
   11º e o 16º clube a receita cresce e o custo NÃO se mexe — é a faixa mais lucrativa do
   plano inteiro.
3. **O piloto caberia em R$ 128,90/mês** no Start — 3 CNPJs, e a franquia é de 100 notas
   **por CNPJ** (não poolável): 250 notas do clube = 150 excedentes = R$ 15, que a própria
   linha da tabela já cobra. Cabe o clube piloto e o CNPJ do Padelizou no mesmo plano.
   (Superado pela escolha da ACBr, fica como registro da régua.)

### A segunda tabela: Cupons Fiscais (a do bar)

Conferida em 24/08/2026. A suspeita estava certa — **a NFC-e tem tabela própria, e ela é a
metade do preço**:

| Plano | Preço | CNPJs | Incluído | NFC-e adicional |
|---|---|---|---|---|
| Retail | R$ 59,90/mês | 1 | 500 NFC-e + 100 NF-e | **R$ 0,05** |
| **Retail+** | R$ 629,90/mês | **ilimitados** | **9.000 NFC-e** + 1.000 NF-e | **R$ 0,06** |

⚠️ **MAS ELA NÃO TEM NFS-e.** A lista dos planos Retail é "NFC-e, CF-e S@T e CF-e MF" — o
documento de serviço (aula, quadra, mensalidade) não está lá. Ou seja: **precisamos dos DOIS**
— um plano de Documentos Fiscais para a NFS-e e um Retail para a NFC-e do bar.
⚠️ *Corrigido pela auditoria de 24/08*: a regra "split sempre ganha" é falsa em volume baixo —
o plano Documentos também emite NFC-e, e jogar tudo num plano só é mais barato até o cenário
conservador a 15 clubes (all-in-Growth: R$ 878 contra R$ 1.177,90 do split). A regra
verdadeira: **plano único em volume baixo; split Documentos+Cupons do cenário médio×15 pra
cima.** Irrelevante na prática — a ACBr ganha das duas combinações — mas o número da Focus
usado como régua tem que ser o ótimo dela, senão a comparação nos lisonjeia.

### A conta com as duas tabelas — e o achado que mudou o jogo

⚠️ Aqui aparece a variável que passou a ser **a mais importante de todo o plano**, e não é o
preço do provedor: **quantas notas um clube emite de verdade por mês.** A premissa antiga
("250 notas/clube") era o total; com o bar emitindo um cupom por comanda, o número real é
bem maior — e é o bar que domina o volume.

Três cenários (NFS-e = reservas + aulas + mensalidades; NFC-e = comandas do bar), com a
franquia do plano Fiscal em 100 NFS-e + 400 NFC-e e excedente de R$ 0,30 cobrado do clube:

| Cenário (por clube/mês) | 3 clubes | 15 clubes | Planos na Focus |
|---|---|---|---|
| Conservador — 150 NFS-e + 300 NFC-e | 52% | **63%** | Start+Retail → Growth+Retail+ |
| Médio — 250 NFS-e + 600 NFC-e | 61% | **74%** | Start+Retail → Growth+Retail+ |
| Alto — 400 NFS-e + 1.200 NFC-e | 69% | **75%** | Start+Retail → Growth+Retail+ |

**A FRANQUIA É O QUE SALVA O PLANO, e agora dá pra provar.** Sem ela — R$ 199 fixo com tudo
incluso — a margem a 15 clubes cai de 74% para 60% no cenário médio e **despenca para 34% no
cenário alto**. Com a franquia, ela fica entre 63% e 75% nos três cenários, ou seja: **o plano
para de depender de adivinhar o volume.** Era uma decisão de produto tomada no escuro; virou
a defesa mais importante da margem.

E a franquia está bem calibrada por acidente feliz: cobramos **R$ 0,30** por nota excedente e
pagamos **R$ 0,06** pela NFC-e no Retail+ — cinco vezes de folga no documento que mais sai.

⚠️ **O que o piloto tem que medir, além de funcionar**: quantas NFS-e e quantas NFC-e o clube
emite por mês. É esse número que confirma (ou corrige) a franquia de 100+400 e o preço de
R$ 199 — e um mês de piloto responde melhor do que qualquer estimativa daqui.

## A ESCOLHA DO PROVEDOR: ACBr API (24/08/2026)

Tabela do console (`console.acbr.api.br/financeiro/comprar-creditos`), crédito pré-pago:

| Pacote | Preço | Por crédito | | Pacote | Preço | Por crédito |
|---|---|---|---|---|---|---|
| 1K | R$ 240 | R$ 0,24 | | 50K | R$ 2.500 | R$ 0,05 |
| 2K | R$ 360 | R$ 0,18 | | 100K | R$ 4.000 | R$ 0,04 |
| 5K | R$ 600 | R$ 0,12 | | 200K | R$ 6.000 | R$ 0,03 |
| 10K | R$ 900 | R$ 0,09 | | 500K | R$ 11.500 | R$ 0,023 |
| 20K | R$ 1.400 | R$ 0,07 | | 1M | R$ 20.000 | R$ 0,02 |

**Sem mensalidade · créditos não expiram · sem custo adicional · CNPJs ilimitados em todas as
faixas · guarda de XML inclusa · suporte e consultoria inclusos.**

### A comparação, cenário por cenário

Margem do plano Fiscal (R$ 199 + franquia 100 NFS-e/400 NFC-e, excedente R$ 0,30):

| Cenário | Clubes | Volume/mês | Custo ACBr | Custo Focus | **Margem ACBr** | Margem Focus |
|---|---|---|---|---|---|---|
| Conservador | 3 | 1.350 | R$ 94,50 | R$ 308,60 | **85%** | 52% |
| Conservador | 15 | 6.750 | R$ 270,00 | R$ 1.177,90 | **92%** | 63% |
| Médio | 1 (piloto) | 850 | R$ 59,50 | R$ 193,80 | **80%** | 36% |
| Médio | 3 (o portão) | 2.550 | R$ 127,50 | R$ 353,60 | **86%** | 61% |
| Médio | 15 | 12.750 | R$ 382,50 | R$ 1.177,90 | **92%** | 74% |
| Alto | 15 | 24.000 | R$ 552,00 | R$ 1.957,90 | **93%** | 75% |
| Alto | 50 | 80.000 | R$ 1.600,00 | R$ 6.157,90 | **94%** | 77% |

**A ACBr ganha em todos os doze cruzamentos, por 2 a 3 vezes.** E o motivo é estrutural, não
promocional: ela não cobra mensalidade e não trava CNPJ ilimitado atrás de um plano caro —
que era exatamente o critério nº 1 da nossa régua ("taxa fixa por CNPJ mata a margem em clube
pequeno"). O modelo dela é o único dos quatro desenhado para software house de verdade.

### O que muda no dia a dia

- **Não há custo fixo.** Mês em que o clube não emitir, não se gasta nada — o crédito fica
  parado esperando. Com a Focus, R$ 173,80/mês saem mesmo com o bar fechado.
- **Vira capital de giro, não despesa.** A R$ 6.000 por 200K créditos parece muito, mas são
  ~16 meses de 15 clubes, e o crédito não vence. O desembolso é adiantado; o custo, não.
- **A guarda de XML vem junto** — o risco nº 5 deste documento (exportação dos XMLs, guarda de
  5 anos) sai resolvido de fábrica em vez de virar cláusula de contrato.

### Como começar, sem desembolso grande

1. **Sandbox primeiro** (o console já tem o seletor Produção/Sandbox): a integração inteira da
   Fase 2 se desenvolve sem gastar crédito nenhum.
2. **Piloto**: comprar o pacote de **5K por R$ 600** (R$ 0,12/crédito) — dá ~6 meses de um
   clube no cenário médio. É a compra pequena que valida antes de comprometer capital.
3. **Depois do portão dos 3 clubes**: subir para 50K (R$ 2.500, R$ 0,05) — ~20 meses.
4. **A partir de ~10 clubes**: 200K (R$ 6.000, R$ 0,03).

### A resposta da pergunta em aberto: 1 crédito = 1 REQUISIÇÃO, não 1 emissão

Lido na documentação (`dev.acbr.api.br/docs`) em 24/08/2026. O consumo padrão é **1 crédito
por requisição**; alguns endpoints isentam a primeira e cobram as seguintes; consulta de CNPJ
custa **0,1 crédito por estabelecimento retornado**. Emissão, cancelamento, carta de correção
e consulta são requisições — cada uma conta.

⚠️ **Isso vira uma decisão de ARQUITETURA, não de compra**: se a integração ficar consultando
o status de cada nota em laço (*polling*), o consumo dobra. Com **webhook** — o provedor avisa
quando a nota é autorizada — fica perto de 1 crédito por venda. A diferença entre os dois
desenhos, a 15 clubes no cenário médio, é **R$ 382 contra R$ 586 por mês**.

Sensibilidade (cenário médio, 15 clubes, contra os R$ 1.177,90 da Focus):

| Requisições por venda | Desenho | Custo/mês | Margem |
|---|---|---|---|
| 1,0× | webhook, sem polling | R$ 382,50 | **92%** |
| 1,3× | webhook + consulta eventual | R$ 497,25 | **89%** |
| 2,0× | polling em toda venda | R$ 586,50 | **87%** |

**A conclusão, qualificada pela auditoria de 24/08**: na escala, a ACBr ganha até no desenho
ruim (a 15 clubes com pacote 200K, polling custa R$ 765 — ainda bem abaixo da Focus). Mas
**no piloto o desenho ruim PERDE**: com o pacote 5K (R$ 0,12/crédito) e polling a 2×, o custo
vai a R$ 204/mês — acima da melhor combinação da Focus (R$ 164,90). Ou seja, **webhook não é
otimização, é condição necessária já na Fase 2.** E a economia do desenho bom a 15 clubes é
~R$ 380/mês, não ~R$ 200. Requisitos gêmeos registrados: **webhook, nunca polling** e **cap
de 3 tentativas em emissão rejeitada, com queda pra fila manual** — NCM errado num clube
movimentado não pode virar queima de crédito em loop (rejeição também consome crédito).

### O resto do que a documentação respondeu

- **Autenticação**: OAuth 2.0 `client_credentials` em `auth.acbr.api.br` (Keycloak), com
  ambientes de Produção e Homologação separados.
- **Escopos por documento**: o token precisa carregar o escopo `nfse` ou `nfce`. Bom para o
  desenho do `IEmissorFiscal` — dá para pedir só o escopo que o plano do clube contratou.
- **Operações cobertas**: emissão, cancelamento, carta de correção e manifestação do
  destinatário. O cancelamento amarrado ao cancelamento de comanda (Fase 3) tem endpoint.
- ⚠️ **NFS-e continua sendo município a município**: a ACBr trabalha com *providers* distintos
  por prefeitura (WEBISS, FIORILLI, Padrão Nacional). Ou seja, o risco de cobertura municipal
  não desaparece com a escolha do provedor — **confirmar o município do clube piloto antes de
  prometer NFS-e a ele.**

⚠️ **A Focus continua no documento de propósito**: é o plano B se a ACBr decepcionar no
piloto, e é a régua de negociação — preço público, previsível e provado no nicho pelo Gripo.

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
  fiscal amarrado ao cancelamento de comanda (prazo curto, ~30 min na maioria dos estados).
  ⚠️ *Promessa rebaixada em 24/08*: era "contingência offline", mas contingência offline de
  verdade (emitir sem internet) é estruturalmente impossível via API na nuvem — em QUALQUER
  provedor. O que se entrega é **fila de pendências + reemissão automática** (que já era a
  regra de desenho nº 2 dos riscos); confirmar com o suporte da ACBr se existe modo EPEC no
  serviço hospedado. Piloto em 1 clube real no RS antes de abrir estado a estado.
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
| **Clube Fiscal** | R$ 199/mês ou R$ 1.990/ano | Gestão + NFS-e e NFC-e com franquia **150 NFS-e + 600 NFC-e/mês**; excedente **R$ 0,30 (NFS-e) / R$ 0,15 (NFC-e)**. A1 por conta do clube. |

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

## ⚖️ O VEREDITO — a melhor proposta (24/08/2026, após revisão adversarial)

A proposta inteira foi posta sob ataque por três frentes independentes: uma auditoria que
re-derivou toda a matemática do zero em código, um advogado do diabo contra a escolha da ACBr
(com busca de evidência real) e outro contra o desenho comercial. **Veredito das três:
SUSTENTA COM AJUSTES.** O que segue é a proposta final — o que sobreviveu, já com as correções.

### A proposta fechada

| Componente | Decisão final |
|---|---|
| **Provedor** | **ACBr API** (Focus NFe como plano B com gatilhos de fuga definidos) |
| **Planos** | Rede grátis · Gestão R$ 99 · **Fiscal R$ 199** (anual = 10× mensal) |
| **Franquia do Fiscal** | **150 NFS-e + 600 NFC-e/mês** — 25% mais documentos que o Gripo (100+500) |
| **Excedente** | **NFS-e R$ 0,30 · NFC-e R$ 0,15** (separados: são economias diferentes) |
| **Arquitetura** | Webhook (nunca polling) + cap de 3 retries com fila manual + venda nunca trava |
| **Compra de créditos** | 5K no piloto; **teto de ~6 meses de consumo em saldo**; 200K adiado |

**A frase de venda que esses números compram**: *"150 notas de serviço + 600 cupons por
R$ 199 — contra 100+500 por R$ 219 mais módulos à parte no Gripo. E com torneios, ranking e
rede de jogadores que ele não tem."*

### Por que a franquia mudou de 100+400 para 150+600 — o achado mais valioso do ataque

O red team comercial derrubou o número antigo com uma conta simples: **o cliente típico do
nosso próprio cenário médio nunca pagaria R$ 199 — pagaria R$ 304** (105 de excedente todo
mês), acima da manchete de R$ 219 do Gripo. Pior: ~35% da receita modelada era excedente, ou
seja, a âncora quebrava na primeira fatura e armava o vendedor do Gripo duas vezes ("eles dão
400 cupons, nós 500" e "o 199 deles vira 300"). Na estrutura da ACBr — sem custo fixo, crédito
só quando usa — dar 600 cupons em vez de 400 custa **R$ 6–12 por clube**, e o cenário
conservador passa a caber inteiro nos R$ 199. Custa 2–3 pontos de uma margem de ~90 e fecha o
flanco inteiro.

### As margens honestas (com o pacote da fase certa, não o da escala)

A auditoria pegou o quadro anterior usando preços de pacotes que a escada de compra ainda não
tinha comprado. Números corrigidos, cenário médio, franquia nova:

| Fase | Pacote ACBr | Custo/mês | Receita/mês | **Margem** |
|---|---|---|---|---|
| Piloto (1 clube) | 5K a R$ 0,12 | R$ 102 | R$ 229 | **55%** |
| Portão (3 clubes) | 10–20K a R$ 0,07–0,09 | R$ 179–230 | R$ 687 | **67–74%** |
| 15 clubes | 50K a R$ 0,05 | R$ 638 | R$ 3.435 | **81%** |
| 50 clubes | 200K a R$ 0,03 | R$ 1.275 | R$ 11.450 | **89%** |

Menos vistosas que os 92% de antes — e são as verdadeiras. A decisão não muda: a ACBr vence a
melhor combinação da Focus em **todas** as células (1,1× a 4,3×), e o break-even exigiria
crédito acima de R$ 0,077 — qualquer pacote ≥ 20K está muito abaixo.

### As mitigações que viraram OBRIGATÓRIAS (red team ACBr)

O fato central que o ataque estabeleceu: **o serviço hospedado ACBr API tem ~4 meses de vida**
(lançado em 29/04/2026, sete dias após o comunicado de morte da Nuvem Fiscal, da qual é a
sucessora byte-compatível — provavelmente a mesma plataforma, relançada como serviço oficial
do Projeto ACBr). Maturidade técnica provável; histórico comercial, nenhum. Portanto:

1. **Teto de exposição pré-paga: nunca manter em créditos mais que ~6 meses de consumo.**
   Piloto com 5K (R$ 600) ok; 50K só depois de a ACBr API completar ~12 meses de operação E o
   piloto rodar estável; **o degrau de 200K sai do plano por ora** — a economia de R$ 0,05→0,03
   não paga o risco de crédito que o precedente Nuvem Fiscal (90 dias de aviso) demonstrou.
2. **Contrato, não checkout**, antes de qualquer compra acima do piloto: créditos sem
   expiração POR ESCRITO (o site oficial fala em "modelo anual" — contradição a resolver),
   reembolso pro-rata em caso de descontinuação, aviso mínimo de 90 dias, exportação integral
   dos XMLs, e **identificar o CNPJ da entidade operadora**.
3. **Gatilhos objetivos de fuga pra Focus**, definidos ANTES do piloto: ex. 2 indisponibilidades
   em horário de pico num trimestre, ou incidente sem resposta em 1 dia útil.
4. **Monitorar a status page da ACBr por 30+ dias antes do go-live** — construir o histórico
   de uptime que o fornecedor não tem idade pra ter. E **testar o suporte na prática**: abrir
   um chamado real numa sexta à noite, na ACBr e na Focus, e cronometrar (nenhuma das duas
   publica plantão de fim de semana — o desenho assíncrono continua sendo a única defesa real).

### O fato regulatório que o plano ignorava: CGSN 191/2026

**A partir de 01/11/2026, ME/EPP do Simples emite NFS-e obrigatoriamente pelo Emissor
Nacional** (Resolução CGSN 191/2026, que reeditou a 189/2026 revogada). Nossos clubes-alvo são
exatamente ME/Simples. Duas consequências: **(boa)** o risco de cobertura municipal esvazia —
os "3.000+ municípios" da Focus deixam de ser diferencial; **(atenção)** nasce dependência
única do SEFIN Nacional, que acumula relatos de instabilidade desde 10/08/2026 no próprio
fórum ACBr — risco igual para todos os provedores, e mais um motivo pros requisitos de
webhook + retry com cap + fila. O cronograma do piloto cruza 01/11/2026: confirmar no sandbox
a emissão via Padrão Nacional pro município do piloto **antes** de prometer NFS-e.

### Regras da franquia — escrever na tela e no contrato, não só decidir

A franquia é **mensal e não acumula** (inclusive no plano anual); baldes de NFS-e e NFC-e são
**separados, sem compensação**; só nota **autorizada** consome franquia; **cancelamento não
devolve**; excedente do plano anual é faturado **mensalmente via Pix** (o billing da Fase 4a
já cobra Pix direto). O piloto mede a **curva** mensal (dezembro/janeiro em especial), não só
a média — franquia definitiva se confirma com 3 meses de dados, e os números vivem em
configuração.

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

1. ✅ **Provedor escolhido: ACBr API** (24/08/2026) — ganha da Focus por 2 a 3 vezes em todos
   os cenários, sem mensalidade e com CNPJs ilimitados em qualquer faixa. A conta gratuita já
   está criada e o sandbox já dá pra desenvolver a Fase 2 inteira sem gastar crédito.
   **Falta só uma pergunta ao suporte deles**: 1 crédito = 1 emissão? O e-mail do apêndice
   serve, reduzido a essa pergunta. As propostas de PlugNotas e Focus deixam de ser urgentes
   — a Focus fica como plano B, com preço público que já conhecemos.
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

---

## Apêndice — os e-mails de proposta

> ⚠️ **Parcialmente superado em 24/08/2026.** Os preços da ACBr e da Focus vieram direto do
> site e do console, e a escolha já está feita (ACBr API). Este apêndice fica por dois
> motivos: as perguntas 2 a 8 continuam valendo como **checklist de due diligence** antes de
> assinar, e o texto serve se algum dia for preciso reabrir a comparação — por exemplo se a
> ACBr decepcionar no piloto.

Escritos em 24/08/2026. As mesmas perguntas nos três, para as respostas serem comparáveis
lado a lado — é isso que transforma três e-mails numa decisão.

⚠️ Os contatos abaixo foram apurados por busca (os sites estão bloqueados no ambiente de
desenvolvimento). Se algum voltar, use o formulário do site — o texto serve igual.

| Provedor | Canal apurado |
|---|---|
| **PlugNotas** (Tecnospeed) | `comercial@tecnospeed.com.br` · 0800 006 9500 · (44) 3037-9500 · 8h–18h |
| **Focus NFe** | `contato@focusnfe.com.br` · formulário em focusnfe.com.br/contato |
| **ACBr API** | acbr.api.br e projetoacbr.com.br/api (contato pelo site) |

### Assunto

> Proposta comercial — software house de gestão de clubes esportivos (multi-CNPJ)

### Corpo (trocar só o nome do produto na primeira linha)

```
Olá,

Sou o Felipe, da Bonamigo Systems (CNPJ 68.185.754/0001-05), desenvolvedor do Padelizou
(padelizou.com.br) — sistema de gestão para clubes e arenas de padel e beach tennis.

Estamos avaliando integrar emissão fiscal ao nosso produto e gostaria de receber uma
proposta comercial do [PlugNotas / Focus NFe / ACBr API]. Nosso cenário:

CENÁRIO
- Multi-CNPJ: cada clube cliente é um emitente próprio (CNPJ, certificado e
  responsabilidade tributária dele). Nós somos a software house integradora.
- Documentos: NFS-e (aula, reserva de quadra, mensalidade) e NFC-e (bar/balcão do clube).
  NF-e não é prioridade.
- Volume: começamos com 1 clube piloto no RS; projeção de 15 clubes no primeiro ano e ~50
  na escala, com média estimada de 250 notas por clube/mês.
- Estamos em fase de escolha de fornecedor — a integração ainda não foi iniciada.

PERGUNTAS
1. Preço em multi-CNPJ: há taxa fixa por CNPJ cadastrado ou a cobrança é só por documento
   emitido? Qual o preço por nota nas faixas de volume acima?
   (Para a Focus, que já publica a tabela: confirmar se a NFC-e do bar entra no plano
   "Documentos Fiscais" ou tem tabela própria em "Cupons Fiscais".)
2. Ambiente de testes: existe sandbox gratuito para desenvolvimento e homologação? Qual o
   limite?
3. NFS-e: qual a cobertura de municípios e o suporte ao Padrão Nacional? Quando o município
   do cliente ainda não está homologado, qual o prazo e o custo?
4. NFC-e: há contingência offline? Como funciona o cancelamento dentro do prazo legal?
5. White-label: podemos operar a emissão de forma transparente, sem que o clube precise
   acessar um painel de vocês?
6. Certificado digital A1: como é o envio e o armazenamento? Nossa premissa é NÃO armazenar
   a chave privada dos clientes do nosso lado.
7. SLA: qual o compromisso de disponibilidade e qual o canal e horário de suporte? Nosso
   pico de uso é sexta e sábado à noite.
8. XMLs: em caso de encerramento de contrato, como funciona a exportação dos XMLs já
   emitidos? (a guarda de 5 anos é obrigação do nosso cliente)

Posso detalhar a parte técnica por telefone, se for mais prático.

Obrigado,
Felipe Bonamigo
Bonamigo Systems — Padelizou
padelizou.com.br
```

### Como comparar as respostas

Só três números decidem, e nenhum deles é a mensalidade:

1. **Custo por nota na faixa de 15 clubes** (~3.750 notas/mês). É ele que define a margem do
   plano Fiscal a R$ 199. **A régua a bater é a Focus: R$ 548/mês fixo com CNPJs ilimitados,
   ou R$ 0,14 por nota.** Proposta que não chegar perto disso está fora.
2. **Existe taxa por CNPJ?** Se existir, some-a ao custo por clube — é o item que mata a
   margem em clube pequeno, e o motivo de a Focus NFe ser a terceira da lista.
3. **Sandbox e SLA.** Sandbox ruim atrasa a Fase 2; SLA ruim vira o chamado de sábado à
   noite que o item 2 dos riscos descreve.

A pergunta 8 (XMLs) não muda o preço, mas é a que teria evitado dor se a Nuvem Fiscal já
estivesse contratada quando anunciou o encerramento.
