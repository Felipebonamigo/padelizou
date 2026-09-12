using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// AS CONSULTAS DA VARREDURA REALMENTE VIRAM SQL?
//
// ⚠️ REGRA DA CASA (CLAUDE.md): consulta LINQ nova e não trivial — `Where` depois de projeção,
// navegação através de vários níveis — se confere com `ToQueryString()` contra um provedor
// Npgsql apontado pra lugar nenhum. O EF InMemory do resto da suíte **não traduz nada**: uma
// consulta que o Postgres recusa passa lisa pelos 6.900 testes e estoura na primeira execução
// em produção (aconteceu em 19/08/2026).
//
// A varredura tem exatamente a forma perigosa: ela navega `p.Categoria.TorneioId` — dois níveis
// — de propósito, porque `Partida.TorneioId` é anulável e a categoria é obrigatória.
public class TraducaoDaVarreduraDaChaveTests
{
    private static DbPadelContext ContextoPostgres()
    {
        var options = new DbContextOptionsBuilder<DbPadelContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=nao_existe;Username=x;Password=x")
            .Options;
        return new DbPadelContext(options);
    }

    // Cada consulta é compilada SOZINHA: `ToQueryString()` gera o SQL sem abrir conexão, então
    // nada aqui espera rede. (Chamar o serviço inteiro seria cego — a primeira consulta falharia
    // por conexão e as seguintes nunca chegariam a ser compiladas.)
    private static void Traduz(Func<DbPadelContext, IQueryable> consulta)
    {
        using var ctx = ContextoPostgres();
        _ = consulta(ctx).ToQueryString();
    }

    [Fact]
    public void A_lista_de_torneios_a_varrer_traduz()
    {
        Traduz(ctx => ctx.Torneios
            .Where(t => t.Status != AprovacaoDeChaves.Pendente
                     && t.Status != "Finalizado"
                     && t.Status != "Inscrições Abertas"
                     && !t.Status.StartsWith("Cancelado"))
            .Select(t => t.Id));
    }

    [Fact]
    public void As_partidas_pelo_caminho_da_categoria_traduzem()
    {
        // A que importa: `p.Categoria.TorneioId` é o join que o InMemory faria em memória sem
        // reclamar de nada.
        Traduz(ctx => ctx.Partidas
            .Where(p => p.Categoria.TorneioId == 1)
            .Select(p => new { p.CategoriaId, p.Fase, p.Status }));
    }

    [Fact]
    public void A_busca_dos_nomes_dos_byes_traduz()
    {
        // A outra consulta nova do dia: os nomes das duplas que folgaram, pra projeção
        // (TorneiosController.ProjetarProximasFasesAsync). Navega pros dois jogadores.
        var ids = new List<int> { 1, 2, 3 };
        Traduz(ctx => ctx.Duplas
            .Include(d => d.Jogador1)
            .Include(d => d.Jogador2)
            .Where(d => ids.Contains(d.Id)));
    }
}
