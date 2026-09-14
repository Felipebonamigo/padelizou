using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Tests;

// AS CONSULTAS DA ARTE DO JOGO REALMENTE VIRAM SQL?
//
// Mesmo buraco que TraducaoDeConsultasDoPerfilTests e TraducaoDasConsultasDePalpiteTests
// fecham: o banco InMemory do resto da suíte NÃO TRADUZ NADA — uma consulta que o Postgres
// recusaria passa lisa por 7 mil testes verdes e só estoura na primeira visita real (aconteceu
// em 19/08/2026). `ToQueryString()` compila a consulta sem abrir conexão nenhuma.
//
// Aqui a consulta é do tipo que merece a conferência: navega Partida → Dupla → Jogador em dois
// níveis nas DUAS duplas, e ordena por uma coalescência de duas datas nuláveis.
public class TraducaoDasConsultasDaArteTests
{
    private static DbPadelContext ContextoPostgres()
    {
        var options = new DbContextOptionsBuilder<DbPadelContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=nao_existe;Username=x;Password=x")
            .Options;
        return new DbPadelContext(options);
    }

    // Mesma forma exata de JogosParaArte.DoJogoAsync.
    [Fact]
    public void O_jogo_com_os_quatro_jogadores_das_duas_duplas_vira_SQL()
    {
        using var ctx = ContextoPostgres();
        int partidaId = 7;

        var consulta = ctx.Partidas
            .AsNoTracking()
            .Include(p => p.Categoria)
            .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1)
            .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2)
            .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1)
            .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2)
            .Where(p => p.Id == partidaId);

        Assert.Contains("SELECT", consulta.ToQueryString());
    }

    // Mesma forma exata de JogosParaArte.DoTorneioAsync: o jogo mais recente em cima, que é o
    // que você acabou de ver ser jogado e vai fotografar.
    [Fact]
    public void A_lista_ordenada_pelo_jogo_mais_recente_vira_SQL()
    {
        using var ctx = ContextoPostgres();
        int torneioId = 3;

        var consulta = ctx.Partidas
            .AsNoTracking()
            .Include(p => p.Categoria)
            .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1)
            .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2)
            .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1)
            .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2)
            .Where(p => p.TorneioId == torneioId)
            .OrderByDescending(p => p.HorarioInicioReal ?? p.HorarioPrevisto)
            .ThenByDescending(p => p.Id);

        Assert.Contains("SELECT", consulta.ToQueryString());
    }
}
