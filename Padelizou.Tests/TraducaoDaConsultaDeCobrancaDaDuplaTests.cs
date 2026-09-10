using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// A CONSULTA DE CobrancaDaDupla.AtivaDe REALMENTE VIRA SQL?
//
// Mesmo padrão de TraducaoDasConsultasDePalpiteTests.cs: o InMemory do resto da suíte não
// traduz nada, então uma consulta que o Postgres recusa passaria lisa por 4 mil testes verdes
// e só estouraria em produção. A versão original desta consulta usava `p.Status is "A" or
// "B"` (pattern-matching de árvore de expressão) — o COMPILADOR já recusou isso (CS8122) antes
// de chegar a rodar, mas a régua do projeto é confirmar com um provedor Npgsql de verdade
// mesmo assim, porque nem todo erro de tradução é um erro de compilação.
public class TraducaoDaConsultaDeCobrancaDaDuplaTests
{
    private static DbPadelContext ContextoPostgres()
    {
        var options = new DbContextOptionsBuilder<DbPadelContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=nao_existe;Username=x;Password=x")
            .Options;
        return new DbPadelContext(options);
    }

    [Fact]
    public void A_cobranca_ativa_da_dupla_vira_SQL()
    {
        using var ctx = ContextoPostgres();
        var sql = CobrancaDaDupla.AtivaDe(ctx, duplaId: 1).ToQueryString();
        Assert.Contains("SELECT", sql);
    }

    [Fact]
    public void As_faturas_abertas_do_pagar_depois_viram_SQL()
    {
        // O irmão que o AtivaDe não cobre: a fatura que nunca confirmou não tem ReferenciaId,
        // e é ela que precisa morrer quando o organizador cancela a inscrição sem parceiro.
        using var ctx = ContextoPostgres();
        var sql = CobrancaDaDupla.PendentesDoPagarDepois(ctx, torneioId: 1).ToQueryString();
        Assert.Contains("SELECT", sql);
    }
}
