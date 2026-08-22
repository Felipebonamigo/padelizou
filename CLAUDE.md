# Padelizou

Plataforma de torneios e aulas de padel (padelizou.com.br), operada por uma pessoa (Felipe).
.NET 10 / ASP.NET Core MVC + PostgreSQL. **Leia isto antes de mexer em qualquer coisa** — é o
resumo do que o STATUS.md guarda em milhares.

## 🔒 Regras para não regredir

A memória mais barata do projeto. Ficam aqui, e não no `STATUS.md`, porque este arquivo é
lido sozinho no início de toda sessão — regra que depende de alguém achar a linha 3855 de um
arquivo de 3.900 linhas é regra que uma sessão com pressa pula.

0. **Ação que grava dado precisa de `[HttpPost]` + `[Authorize]` + checagem de dono/organizador.**
   Dois buracos em 26/07 vieram da falta disso. O gate de Acesso Antecipado *não* é
   autorização — ele some no dia em que o sistema abrir pro público.
   A parte do `[Authorize]` tem gate mecânico desde 21/08: `GateDeAutorizacaoDosPostsTests`
   varre o assembly e quebra se um POST novo atender sem login. **A checagem de dono continua
   sem gate** — é julgamento, e é trabalho do teste da área.
1. **Todo defeito corrigido vira teste — escrito ANTES da correção, e visto falhar.**
   Rodar e confirmar que falha pelo motivo certo ("não existe", "resultado errado"), nunca
   por typo ou config: teste que passa de primeira não prova nada, porque você nunca o viu
   falhar. Se a correção saiu antes do teste, **apague a correção e recomece** — não adapte
   escrevendo o teste depois, olhando pro código pronto: isso produz teste que confirma o
   comportamento em vez de travá-lo.
2. **Nada é publicado com teste vermelho.**
3. **Testar em `dev` antes de produção.**
4. **Fechou um trabalho, commit + push.**
5. **Uma coisa de cada vez, até o fim.**
6. **Três correções seguidas falhando: pare e questione a arquitetura.** Não tente a quarta.
   Três tentativas erradas quase sempre significam que o problema não está onde se procura —
   a data de 20/08 estava no Razor, não no campo; o desconto do carrinho está no recálculo,
   não no motor. Volte pra causa raiz: reproduza, veja o que mudou, e instrumente **cada
   fronteira** entre camadas pra achar onde o valor certo vira errado, em vez de chutar.

## Plugins deste projeto

Quatro plugins em **escopo de projeto** (`.claude/settings.json`), não no `~/.claude/` de
ninguém — assim valem também nas sessões da web, que nascem de um clone limpo. Todos do
marketplace oficial da Anthropic, com SHA fixado. Custo somado: **~3,2k tokens sempre ligados**.

| Plugin | O que traz | Sempre ligado |
|---|---|---|
| `superpowers` | 14 skills de processo: brainstorming, writing-plans, systematic-debugging, TDD, verification-before-completion | ~688 tok |
| `pr-review-toolkit` | 6 agentes revisores + `/review-pr`. O `silent-failure-hunter` caça defeito que falha calado | ~2.033 tok |
| `claude-md-management` | `/revise-claude-md` — audita este arquivo e captura aprendizado de sessão | ~175 tok |
| `hookify` | Cria hooks a partir de regra em `.local.md`. Os 4 hooks dele são fail-open e no-op sem regra escrita | ~292 tok |

⚠️ **Este arquivo vence as skills deles.** É a própria regra do superpowers: instrução explícita
do projeto tem prioridade. O hook de SessionStart dele injeta um bloco `<EXTREMELY_IMPORTANT>` —
a ênfase é do plugin, não uma promoção acima das regras daqui.

⚠️ **`superpowers` é de terceiro** (`github.com/obra/superpowers`), distribuído e fixado pela
Anthropic. Os outros três são internos ao repositório dela.

Desinstalar um: tirar a linha dele de `enabledPlugins` no `.claude/settings.json`.

## Quanto cerimonial cada pedido merece

O `brainstorming` do Superpowers classifica todo pedido em spike / bounded / architectural, e
o tamanho do ritual sai daí. Sem um critério escrito, ele reclassifica o mesmo tipo de tarefa
a cada pedido — então aqui o critério é este, e é por área de risco, não por tamanho do diff:

- **architectural** (design escrito e aprovado antes de qualquer código) — qualquer coisa que
  gere **migration**; que toque uma das quatro réguas de autorização (`EhOrganizadorAsync`,
  `UsuarioEhOrganizadorAsync`, `PodeControlarPlacarAsync`, `PodeOperarODiaDeJogoAsync`); que
  mexa em **dinheiro** (taxa, estorno, recebimento, comissão); que mude o contrato da **API de
  torneios** (tem parceiro externo consumindo — ver `API-TORNEIOS.md`); ou que crie **papel de
  acesso novo**.
- **bounded** (duas perguntas, design de duas frases, aprovação, implementa) — mudança num
  fluxo que já existe, sem nada da lista acima. É o caso da maioria.
- **spike** — "dá pra fazer X?". O resultado é uma resposta, não código que fica.

**O ajuste é de mão única:** complexidade que aparece no meio sobe o nível, nunca desce. Um
`bounded` que revelou precisar de migration virou `architectural` naquele instante.

## Quanto código o pedido merece

Antes de escrever, suba esta escada e **pare no primeiro degrau que resolve** — não continue
subindo "por garantia". Os degraus estão escritos com o que este projeto tem, não em genérico:

1. **Isso precisa existir?** Se é especulativo, pule e diga em uma linha que pulou.
2. **Já existe algo equivalente aqui?** São 69 controllers e ~4.800 testes — reimplementar o
   que está a dois arquivos de distância é a forma mais comum de complexidade inútil. O card
   de marcadores reusou `_BuscaOrganizador` no modo genérico em vez de nascer de novo.
3. **A BCL do .NET já faz?** LINQ, `decimal`, `TimeSpan`, `Uri`, `StringComparison`.
4. **Um recurso nativo da plataforma cobre?** É o degrau mais esquecido e o que mais rende
   aqui: **chave/índice do banco** em vez de checar duplicata em C# — a PK composta de
   `TorneioMarcador` é o que segura o clique duplo no adicionar, sem uma linha de C# pra isso;
   `DataAnnotations` em vez de `if` de validação à mão;
   cascade/`Restrict` do EF em vez de apagar filho na unha; Tag Helper do Razor em vez de
   HTML montado em string; `<input type="date">` em vez de biblioteca de calendário.
5. **Uma dependência já instalada resolve?** QRCoder, SkiaSharp, MailKit, WebPush,
   Google.Apis.Calendar. **Não adicione pacote novo pro que uma delas já faz.**
6. **Cabe numa linha?** Escreva a linha.
7. Só então: o mínimo de código novo que funciona de verdade.

**A escada encurta a SOLUÇÃO, nunca a LEITURA.** Diff pequeno no lugar errado não é preguiça,
é o segundo bug — leia o fluxo inteiro antes de escolher o degrau (ver Regra 6).

**Ela nunca se aplica a:** Regra 0 (`[Authorize]` + checagem de dono nunca são simplificados),
dinheiro (`decimal`, nunca `double`/`float`), validação em fronteira de confiança, tratamento
de erro que evita perda de dado, e qualquer coisa que o Felipe pediu explicitamente.

Atalho deliberado ganha comentário nomeando o teto e a saída — senão a próxima sessão lê como
esquecimento em vez de escolha: `// atalho: consulta sem paginação; paginar acima de ~500 duplas`.

## Como fechar um bloco de trabalho

Termine com **um** destes, explícito, e não com um resumo em prosa — `DONE_WITH_CONCERNS` é
informação que um parágrafo esconde:

`DONE` · `DONE_WITH_CONCERNS` (feito, mas com ressalva que precisa ser lida) ·
`NEEDS_CONTEXT` (falta uma decisão do Felipe) · `BLOCKED` (não dá pra seguir, e por quê).

E antes de qualquer um deles: nenhuma alegação de "pronto", "corrigido" ou "os testes passam"
sem ter rodado o comando **naquele mesmo turno** e lido a saída. "Deveria funcionar" não conta.

## Antes de codar

1. **Leia o TOPO do `STATUS.md`** (as primeiras ~50 linhas) — é um diário em ordem
   cronológica reversa: o que foi feito por último, o que ficou pendente, o que foi
   publicado e em que build. Se o pedido tocar numa área específica, procure por ela no
   arquivo (busca de texto, não leitura linear — ele é grande).
2. Área específica: `RANKING.md` (Padelímetro/pontuação), `ESTORNO.md`, `RECEBIMENTO.md`
   (pagamentos/Asaas), `WHATSAPP.md`, `EMAIL.md`, `ANDROID.md`, `PARCEIROS.md`,
   `API-TORNEIOS.md`, `AMBIENTE-LOCAL.md`, `TRABALHAR-FORA.md`, `infra/vps/README.md`
   (deploy/backup/VPS).

## Rodar

```bash
dotnet build Padelizou.slnx -c Release --nologo
dotnet test Padelizou.slnx -c Release --no-build --nologo   # ~35-40s, ~4750 testes
```

Zero teste vermelho pra commitar. Sem terminal/browser pra ver a UI nesta sessão — não
declare "funciona" sem rodar a suíte.

## O que é verdade estrutural aqui (não repita a descoberta)

- **EF InMemory nos testes NÃO valida SQL.** Uma consulta que o Postgres recusa passa lisa
  pela suíte inteira e só estoura em produção (aconteceu em 19/08). Ao escrever uma consulta
  LINQ nova e não trivial (`Where` depois de projeção, navegação através de vários níveis),
  confira com `ToQueryString()` contra um provedor Npgsql apontado pra lugar nenhum — ver
  `TraducaoDasConsultasDePalpiteTests.cs` e `TraducaoDeConsultasDoPerfilTests.cs` pro padrão.
- **Autorização de organizador está em 3 lugares que precisam andar juntos**:
  `TorneiosController.EhOrganizadorAsync`, `DuplasController.UsuarioEhOrganizadorAsync`,
  `PartidasController.PodeControlarPlacarAsync` (mais `PodeOperarODiaDeJogoAsync` pro
  marcador). Uma dessincronia já quebrou a Mesa de Controle em 31/07.
- **DateTime.Now é hora LOCAL** (colunas `timestamp without time zone`, modo legado do
  Npgsql). O fuso do VPS precisa ser America/Sao_Paulo — não está garantido em lugar nenhum
  do código, só documentado em `infra/vps/README.md`.
- **CS8602 é ERRO, não aviso** (`Directory.Build.props`, desde 22/08). Eram 42 avisos; foram
  a zero e a regra subiu no mesmo dia. Um CS8602 novo **não se resolve com `!`** — o operador
  cala o compilador sem mudar o risco. Ou se prova que não é nulo, ou se trata o nulo. E
  atenção ao padrão que gerou 6 dos 7 de produção: um `bool` que captura `x != null` não
  ensina nada ao compilador; a checagem tem que ser direta no ponto de uso.
- **Migration**: gerar sempre em worktree limpo; conferir com
  `dotnet ef migrations has-pending-model-changes` antes de commitar (o CI já roda isso).
- **Todo defeito corrigido vira teste de regressão** — é assim que a suíte foi de ~1000 pra
  ~4750 testes, e é o que faz o STATUS.md confiável: se algo quebrou antes, tem teste hoje.

## Publicar

Ver `infra/vps/README.md`. Resumo: PR → CI verde → merge no `main` → o `ci.yml` publica um
release `build-N-sha7` → workflow `Deploy` (GitHub Actions → Deploy → Run workflow) instala
em `dev` ou `prod` via SSH, com rollback automático se o `/healthz` não responder 200.
