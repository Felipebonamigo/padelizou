using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Tests;

// AS CONSULTAS DO FILTRO DA TELA DE ERROS REALMENTE VIRAM SQL?
//
// Mesma razão de `TraducaoDasConsultasDePalpiteTests`: o InMemory do resto da suíte **não
// traduz nada**, então uma consulta que o Postgres recusa passa verde aqui e responde 500 na
// primeira visita. E a página em questão é a **tela de erros** — o lugar mais irônico possível
// pra um 500, porque é pra ela que o aviso de erro manda a pessoa.
//
// As formas conferidas são as três que o filtro trouxe (`AdminController.Erros`): o `Where`
// com o filtro opcional, o `Distinct` que monta as caixinhas e o `Count` da janela de 24h.
// Cada uma é compilada SOZINHA — chamar a action inteira faria a primeira falhar por conexão
// e as outras nunca chegariam a ser traduzidas, que foi a cegueira de 19/08.
public class TraducaoDasConsultasDaTelaDeErrosTests
{
    private static DbPadelContext ContextoPostgres()
    {
        var options = new DbContextOptionsBuilder<DbPadelContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=nao_existe;Username=x;Password=x")
            .Options;
        return new DbPadelContext(options);
    }

    private static void Traduz(Func<DbPadelContext, IQueryable> consulta)
    {
        using var ctx = ContextoPostgres();
        var sql = consulta(ctx).ToQueryString();
        Assert.Contains("SELECT", sql);
    }

    // O recorte da tela, na forma exata do controller: filtro opcional em duas colunas.
    private static IQueryable<ErroDoSistema> Recorte(DbPadelContext ctx, string? tipo, string? caminho) =>
        ctx.ErrosDoSistema
            .Where(e => tipo == null || e.Tipo == tipo)
            .Where(e => caminho == null || e.Caminho == caminho);

    [Fact]
    public void O_recorte_SEM_filtro_vira_SQL() =>
        Traduz(ctx => Recorte(ctx, null, null).OrderByDescending(e => e.QuandoEm).Take(100));

    [Fact]
    public void O_recorte_COM_os_dois_filtros_vira_SQL() =>
        Traduz(ctx => Recorte(ctx, "DbUpdateException", "/Partidas/Votar")
            .OrderByDescending(e => e.QuandoEm).Take(100));

    [Fact]
    public void As_opcoes_das_caixinhas_viram_SQL()
    {
        Traduz(ctx => ctx.ErrosDoSistema.Select(e => e.Tipo).Distinct().OrderBy(t => t));
        Traduz(ctx => ctx.ErrosDoSistema.Select(e => e.Caminho).Distinct().OrderBy(c => c));
    }

    [Fact]
    public void A_contagem_das_ultimas_24h_vira_SQL()
    {
        // `CountAsync(predicado)` não devolve IQueryable — o que se compila é o mesmo recorte
        // com o `Where` da janela, que é o SQL que ele gera.
        var ontem = new DateTime(2026, 9, 10, 15, 0, 0);
        Traduz(ctx => Recorte(ctx, "DbUpdateException", "/Partidas/Votar")
            .Where(e => e.QuandoEm >= ontem));
    }
}
