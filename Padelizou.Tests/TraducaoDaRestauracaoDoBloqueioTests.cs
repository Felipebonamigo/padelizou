using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// As consultas de RestauracaoDoCancelamentoPorBloqueio REALMENTE viram SQL? O InMemory do resto
// da suíte não traduz nada (ver TraducaoDeConsultasDoPerfilTests pro porquê do padrão).
public class TraducaoDaRestauracaoDoBloqueioTests
{
    private static DbPadelContext ContextoPostgres() => new(new DbContextOptionsBuilder<DbPadelContext>()
        .UseNpgsql("Host=127.0.0.1;Port=59999;Database=nao_existe;Username=x;Password=x")
        .Options);

    [Fact]
    public void Candidatas_viram_SQL()
    {
        using var ctx = ContextoPostgres();
        var agora = new DateTime(2026, 10, 28);

        var sql = RestauracaoDoCancelamentoPorBloqueio
            .ConsultaDasCandidatas(ctx, agora.AddDays(-7), agora.AddDays(17))
            .ToQueryString();

        Assert.Contains("SELECT", sql);
    }

    [Fact]
    public void Series_sem_fim_viram_SQL()
    {
        using var ctx = ContextoPostgres();

        var sql = RestauracaoDoCancelamentoPorBloqueio
            .ConsultaDasSeriesSemFim(ctx, new List<Guid> { Guid.NewGuid() }, new List<int> { 1, 2 })
            .ToQueryString();

        Assert.Contains("DISTINCT", sql);
    }
}
