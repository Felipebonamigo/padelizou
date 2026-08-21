# Padelizou

Plataforma de torneios e aulas de padel (padelizou.com.br), operada por uma pessoa (Felipe).
.NET 10 / ASP.NET Core MVC + PostgreSQL. **Leia isto antes de mexer em qualquer coisa** — é o
resumo de ~30 linhas do que o STATUS.md guarda em milhares.

## Antes de codar

1. **Leia `STATUS.md`, seção `## 🔒 Regras para não regredir`** (perto do fim do arquivo) —
   cinco regras curtas, a memória mais barata do projeto.
2. **Leia o TOPO do `STATUS.md`** (as primeiras ~50 linhas) — é um diário em ordem
   cronológica reversa: o que foi feito por último, o que ficou pendente, o que foi
   publicado e em que build. Se o pedido tocar numa área específica, procure por ela no
   arquivo (busca de texto, não leitura linear — ele é grande).
3. Área específica: `RANKING.md` (Padelímetro/pontuação), `ESTORNO.md`, `RECEBIMENTO.md`
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
