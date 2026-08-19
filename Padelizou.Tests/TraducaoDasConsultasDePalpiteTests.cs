using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// AS CONSULTAS DO PALPITRÔMETRO REALMENTE VIRAM SQL?
//
// 🕳️ O buraco que estes testes fecham: **o banco InMemory do resto da suíte não traduz nada**.
// Lá tudo é objeto em memória, então um método nosso dentro de um `Select` (um
// `NomeBonito.ComApelido(...)`, por exemplo) roda numa boa — e em produção, no Postgres, a
// mesma linha estoura com "could not be translated" na PRIMEIRA visita à página. É a categoria
// de erro que passa por 4 mil testes verdes e cai no colo do usuário.
//
// 🎯 O TRUQUE: o EF traduz a consulta ANTES de abrir conexão. Rodando contra um provedor
// Npgsql de verdade, apontado pra um servidor que não existe:
//   • consulta que NÃO traduz  → `InvalidOperationException` (a falha que queremos pegar);
//   • consulta que traduz      → erro de CONEXÃO, que é o que provamos aqui.
// Ou seja: chegar no erro de conexão É a aprovação. Nenhum banco precisa existir.
public class TraducaoDasConsultasDePalpiteTests
{
    // Porta onde não há ninguém ouvindo, e timeout curtíssimo: o teste quer a recusa, não a
    // espera. `Host=127.0.0.1` e não `localhost` pra não cair no socket unix.
    private static DbPadelContext ContextoPostgres()
    {
        var options = new DbContextOptionsBuilder<DbPadelContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=nao_existe;Username=x;Password=x;Timeout=1;Command Timeout=1")
            .Options;
        return new DbPadelContext(options);
    }

    private static async Task TraduzAsync(Func<DbPadelContext, Task> consulta)
    {
        using var ctx = ContextoPostgres();
        var erro = await Record.ExceptionAsync(() => consulta(ctx));

        // Sem exceção nenhuma seria surpresa (não há banco), mas não é falha de tradução.
        if (erro == null) return;

        // ⚠️ A falha que este teste existe pra pegar. A mensagem entra no assert de propósito:
        // sem ela, quem vir o teste vermelho não saberia que o problema é a consulta, e não o
        // ambiente sem Postgres.
        Assert.False(erro is InvalidOperationException && erro.Message.Contains("could not be translated"),
            "Esta consulta não vira SQL: " + erro.Message);

        // Qualquer outro erro é o de conexão que a gente PEDIU — a tradução passou.
    }

    [Fact]
    public Task O_ranking_de_um_torneio_vira_SQL() =>
        TraduzAsync(ctx => RankingDePalpiteiros.DoTorneioAsync(ctx, torneioId: 1, olhandoId: null));

    [Fact]
    public Task O_ranking_geral_vira_SQL() =>
        TraduzAsync(ctx => RankingDePalpiteiros.GeralAsync(ctx, doLocal: null));

    [Fact]
    public Task O_ranking_geral_COM_filtro_regional_vira_SQL() =>
        TraduzAsync(ctx => RankingDePalpiteiros.GeralAsync(ctx, doLocal: new HashSet<int> { 1, 2, 3 }));

    [Fact]
    public Task O_selo_do_perfil_vira_SQL() =>
        TraduzAsync(ctx => RankingDePalpiteiros.DoJogadorAsync(ctx, jogadorId: 1));

    [Fact]
    public Task O_resumo_do_palpitrometro_vira_SQL() =>
        TraduzAsync(ctx => new PalpiteService(ctx).ObterResumosAsync(new[] { 1, 2 }, jogadorId: 3));

    [Fact]
    public Task Quem_votou_em_quem_vira_SQL() =>
        TraduzAsync(ctx => new PalpiteService(ctx).ObterVotantesAsync(partidaId: 1));
}
