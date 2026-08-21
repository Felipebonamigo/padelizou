using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Tests;

// A consulta por jogador que EstatisticasService.CarregarPartidasFinalizadasAsync passou a
// fazer em 21/08/2026 (perfil: Confrontos e Parceiros) REALMENTE vira SQL?
//
// Mesmo buraco que TraducaoDasConsultasDePalpiteTests fecha do lado do palpitrômetro: o banco
// InMemory do resto da suíte não traduz nada — uma consulta que o Postgres recusaria passa
// lisa por 4 mil testes verdes e só estoura na primeira visita real ao perfil. `ToQueryString()`
// compila a consulta sem abrir conexão nenhuma; ver o cabeçalho daquele arquivo para o porquê
// de cada consulta ser compilada SOZINHA, e não "chamar o serviço e ver se ele explode".
public class TraducaoDeConsultasDoPerfilTests
{
    private static DbPadelContext ContextoPostgres()
    {
        var options = new DbContextOptionsBuilder<DbPadelContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=nao_existe;Username=x;Password=x")
            .Options;
        return new DbPadelContext(options);
    }

    [Fact]
    public void Partidas_finalizadas_de_UM_JOGADOR_via_navegacao_de_dupla_viram_SQL()
    {
        using var ctx = ContextoPostgres();
        int jogadorId = 42;

        // Mesma forma exata de EstatisticasService.CarregarPartidasFinalizadasAsync(jogadorId:).
        var consulta = ctx.Partidas
            .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1)
            .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2)
            .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1)
            .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2)
            .Where(p => p.VencedorId != null)
            .Where(p => p.Dupla1.Jogador1Id == jogadorId || p.Dupla1.Jogador2Id == jogadorId ||
                        p.Dupla2.Jogador1Id == jogadorId || p.Dupla2.Jogador2Id == jogadorId);

        var sql = consulta.ToQueryString();
        Assert.Contains("SELECT", sql);
    }

    [Fact]
    public void Ranking_por_categoria_com_AsNoTracking_continua_traduzindo()
    {
        using var ctx = ContextoPostgres();

        var consulta = ctx.Duplas
            .AsNoTracking()
            .Include(d => d.Categoria).ThenInclude(c => c.Torneio)
            .Include(d => d.Jogador1)
            .Include(d => d.Jogador2);

        var sql = consulta.ToQueryString();
        Assert.Contains("SELECT", sql);
    }
}
