using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// AS CONSULTAS DO LEMBRETE DE AULA REALMENTE VIRAM SQL?
//
// 🕳️ O banco InMemory do resto da suíte NÃO TRADUZ NADA: lá tudo é objeto em memória, então uma
// consulta que o Postgres recusaria passa lisa por 7 mil testes verdes e estoura no primeiro
// tick em produção — com a diferença de que este tick roda num BackgroundService, onde a falha
// vira uma linha de log que ninguém está olhando. Aqui é pior que a página 500 de 19/08/2026:
// o lembrete simplesmente não sairia, calado.
//
// 🎯 `ToQueryString()` compila a consulta contra um provedor Npgsql de verdade e devolve o SQL
// sem abrir conexão nenhuma. Cada consulta é compilada SOZINHA — ver a nota do irmão
// TraducaoDasConsultasDePalpiteTests sobre por que "chamar o serviço e ver se explode" é cego.
public class TraducaoDoLembreteDeAulaTests
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

    [Fact]
    public void As_aulas_da_janela_viram_SQL() =>
        // Três `Include` e um filtro por status + duas comparações de data. O `Include` do Aluno
        // é o mais arriscado: `AlunoId` é anulável, então o EF precisa montar um LEFT JOIN.
        Traduz(ctx => LembreteDaAulaBackgroundService.ConsultaDasAulas(ctx, new DateTime(2026, 9, 18, 19, 0, 0)));

    [Fact]
    public void Os_jogos_aula_da_janela_viram_SQL() =>
        Traduz(ctx => LembreteDaAulaBackgroundService.ConsultaDosJogosAula(ctx, new DateTime(2026, 9, 18, 19, 0, 0)));

    [Fact]
    public void Os_inscritos_de_um_jogo_aula_viram_SQL() =>
        // ⚠️ `InscricaoJogoAula` tem CHAVE COMPOSTA (JogoAulaId + JogadorId) — é o tipo de
        // entidade em que uma consulta mal montada só reclama do lado do Postgres.
        Traduz(ctx => LembreteDaAulaBackgroundService.ConsultaDosInscritos(ctx, jogoAulaId: 1));
}
