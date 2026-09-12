using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Tests;

// A CONSULTA DE PRESENÇA REALMENTE VIRA SQL? (12/09/2026)
//
// Quando a chamada passou a ser por JOGO (Models/PresencaNoJogo), a leitura deixou de ser
// `p.TorneioId == id` — uma coluna da própria tabela — e virou `p.Partida.Categoria.TorneioId`:
// **duas navegações encadeadas** dentro do `Where`. É exatamente a forma que o CLAUDE.md manda
// conferir: o banco InMemory do resto da suíte NÃO traduz nada, e uma consulta que o Postgres
// recusaria passaria lisa por sete mil testes verdes pra estourar na primeira visita real à
// página mais acessada do site.
//
// `ToQueryString()` compila a consulta sem abrir conexão nenhuma — o host/porta abaixo apontam
// pra lugar nenhum de propósito.
public class TraducaoDaPresencaPorJogoTests
{
    private static DbPadelContext ContextoPostgres()
    {
        var options = new DbContextOptionsBuilder<DbPadelContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=nao_existe;Username=x;Password=x")
            .Options;
        return new DbPadelContext(options);
    }

    [Fact]
    public void As_chegadas_do_torneio_por_duas_navegacoes_viram_SQL()
    {
        using var ctx = ContextoPostgres();
        int torneioId = 901;

        // A MESMA FORMA de TorneiosController.ChegadasDoTorneioAsync e da tela de Check-in.
        var consulta = ctx.Presencas.Where(p => p.Partida.Categoria.TorneioId == torneioId);

        var sql = consulta.ToQueryString();

        Assert.Contains("SELECT", sql);
        // Duas navegações = dois JOINs. Se um dia isto virar consulta N+1 ou avaliação no
        // cliente, o SQL deixa de casar aqui antes de a página ficar lenta em produção.
        Assert.Contains("PresencaNoJogo", sql);
        Assert.Contains("Categoria", sql);
    }

    [Fact]
    public void A_leitura_de_UM_check_pelo_par_da_chave_vira_SQL()
    {
        using var ctx = ContextoPostgres();

        // A MESMA FORMA do MarcarCheckIn, que lê antes de gravar pra deixar o clique duplo
        // idempotente.
        var consulta = ctx.Presencas.Where(p => p.PartidaId == 10 && p.JogadorId == 20);

        Assert.Contains("SELECT", consulta.ToQueryString());
    }
}
