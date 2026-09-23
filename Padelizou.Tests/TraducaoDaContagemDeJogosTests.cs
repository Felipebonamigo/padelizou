using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A CONTAGEM DE JOGOS VIRA SQL DE VERDADE?
//
// 🕳️ O banco InMemory do resto da suíte NÃO TRADUZ NADA — uma consulta que o Postgres recusa
// passa lisa por 7 mil testes verdes e só estoura em produção (aconteceu em 19/08/2026).
//
// 🎯 O risco concreto aqui: duas das três consultas filtram por `Categoria.TorneioId`, que é
// navegação, dentro de um `Contains` sobre lista. E desde 23/09/2026 elas são o PREÇO do
// pacote de registro de resultados — quebrar a tradução derruba a tela do organizador (onde
// ele pede) e o painel do admin (onde a gente responde), não um número torto.
public class TraducaoDaContagemDeJogosTests
{
    private static DbPadelContext ContextoPostgres()
    {
        var options = new DbContextOptionsBuilder<DbPadelContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=nao_existe;Username=x;Password=x")
            .Options;
        return new DbPadelContext(options);
    }

    [Fact]
    public void As_tres_consultas_da_contagem_viram_SQL()
    {
        using var ctx = ContextoPostgres();
        var ids = new[] { 3, 7 };

        Assert.Contains("SELECT", JogosDoTorneio.CategoriasDos(ctx, ids).ToQueryString());

        // Estas duas atravessam a navegação: tem que virar JOIN de verdade, e não sumir num
        // filtro que o provedor resolveria em memória.
        var duplas = JogosDoTorneio.DuplasDos(ctx, ids).ToQueryString();
        Assert.Contains("JOIN", duplas);

        var americanas = JogosDoTorneio.AmericanasDos(ctx, ids).ToQueryString();
        Assert.Contains("JOIN", americanas);
    }
}
