using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// A CONSULTA DA FAIXA "VOCÊ FOI INSCRITO POR FULANO" VIRA SQL DE VERDADE?
//
// 🕳️ O banco InMemory do resto da suíte NÃO TRADUZ NADA — lá tudo é objeto em memória. Uma
// consulta que o Postgres recusa passa lisa por 7 mil testes verdes e só estoura em produção
// (aconteceu em 19/08/2026). E esta roda na ABERTURA DA TELA DO TORNEIO, a página mais visitada
// do site: traduzir errado aqui não é um lembrete que não sai, é a página inteira em 500.
//
// 🎯 O risco concreto: `p.Dupla.Categoria.TorneioId` atravessa DOIS níveis de navegação a partir
// de uma entidade de CHAVE COMPOSTA. `ToQueryString()` compila contra um provedor Npgsql de
// verdade e devolve o SQL sem abrir conexão nenhuma.
public class TraducaoDasPerguntasDeInscricaoTests
{
    private static DbPadelContext ContextoPostgres()
    {
        var options = new DbContextOptionsBuilder<DbPadelContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=nao_existe;Username=x;Password=x")
            .Options;
        return new DbPadelContext(options);
    }

    [Fact]
    public void As_perguntas_abertas_do_torneio_viram_SQL()
    {
        using var ctx = ContextoPostgres();

        var sql = InscricaoDeOutraPessoa
            .PerguntasAbertasNoTorneio(ctx, jogadorId: 7, torneioId: 3)
            .Select(p => new { p.DuplaId, p.InscritoPorId })
            .ToQueryString();

        Assert.Contains("SELECT", sql);
        // A navegação de dois níveis tem que ter virado JOIN de verdade, e não sumido num
        // filtro que o provedor resolveria em memória.
        Assert.Contains("JOIN", sql);
    }
}
