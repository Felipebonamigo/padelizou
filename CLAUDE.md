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
1. **Todo defeito corrigido vira teste.**
2. **Nada é publicado com teste vermelho.**
3. **Testar em `dev` antes de produção.**
4. **Fechou um trabalho, commit + push.**
5. **Uma coisa de cada vez, até o fim.**

## Plugins deste projeto

`superpowers@claude-plugins-official` está instalado em **escopo de projeto** (`.claude/settings.json`),
não no `~/.claude/` de ninguém — assim ele vale também nas sessões da web, que nascem de um clone
limpo. São 14 skills de processo (brainstorming, writing-plans, systematic-debugging,
test-driven-development, verification-before-completion e outras), ~688 tokens sempre ligados.
O plugin vem do marketplace oficial da Anthropic, mas o código é de terceiro
(`github.com/obra/superpowers`), fixado num SHA pelo marketplace.

⚠️ **Este arquivo vence as skills dele.** É a própria regra do plugin: instrução explícita do
projeto tem prioridade. O hook de SessionStart dele injeta um bloco `<EXTREMELY_IMPORTANT>` —
a ênfase é do plugin, não uma promoção acima das regras daqui.

Desinstalar: tirar `enabledPlugins` e `extraKnownMarketplaces` do `.claude/settings.json`.

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
- **Ação que grava dado precisa de `[HttpPost]` + `[Authorize]` + checagem de dono/organizador.**
  O Acesso Antecipado NÃO é autorização.
- **DateTime.Now é hora LOCAL** (colunas `timestamp without time zone`, modo legado do
  Npgsql). O fuso do VPS precisa ser America/Sao_Paulo — não está garantido em lugar nenhum
  do código, só documentado em `infra/vps/README.md`.
- **Migration**: gerar sempre em worktree limpo; conferir com
  `dotnet ef migrations has-pending-model-changes` antes de commitar (o CI já roda isso).
- **Todo defeito corrigido vira teste de regressão** — é assim que a suíte foi de ~1000 pra
  ~4750 testes, e é o que faz o STATUS.md confiável: se algo quebrou antes, tem teste hoje.

## Publicar

Ver `infra/vps/README.md`. Resumo: PR → CI verde → merge no `main` → o `ci.yml` publica um
release `build-N-sha7` → workflow `Deploy` (GitHub Actions → Deploy → Run workflow) instala
em `dev` ou `prod` via SSH, com rollback automático se o `/healthz` não responder 200.
