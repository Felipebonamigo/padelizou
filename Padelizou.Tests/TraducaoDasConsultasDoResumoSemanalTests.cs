using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// AS CONSULTAS DO RESUMO SEMANAL REALMENTE VIRAM SQL?
//
// 💥 O DEFEITO QUE ESTES TESTES FECHAM É REAL, e passou por 5.083 testes verdes. A primeira
// versão do varredor pedia os ids assim:
//
//     context.Desafios.SelectMany(d => new[] { d.DesafianteJogador1Id, ... })
//
// O Npgsql recusa: *"The LINQ expression 'd => new int[]{ ... }' could not be translated"*.
// Como o resto da suíte roda em EF InMemory — que não traduz nada —, a rotina passava lisa e
// só estouraria na PRIMEIRA quinta-feira em produção, dentro de um `catch` que vira LogError.
// Ninguém receberia o resumo, para sempre, e a suíte continuaria verde.
//
// 🎯 `ToQueryString()` COMPILA a consulta e devolve o SQL sem abrir conexão. Consulta que não
// traduz estoura ali mesmo. Cada uma é compilada SOZINHA: apontando pra um Postgres
// inexistente, a primeira consulta de um método falharia por CONEXÃO e as seguintes nunca
// seriam compiladas — foi assim que um defeito parecido escapou dos testes do Palpitômetro.
public class TraducaoDasConsultasDoResumoSemanalTests
{
    // Provedor Npgsql de verdade (é ele que traduz), apontado pra lugar nenhum.
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
        // Quem reprova é o próprio `ToQueryString`, que ESTOURA na consulta que não traduz.
        var sql = consulta(ctx).ToQueryString();
        Assert.Contains("SELECT", sql);
    }

    [Fact]
    public void Os_anuncios_no_mural_viram_SQL() =>
        Traduz(ctx => ConsultasDoResumoSemanal.NoMural(ctx, new DateTime(2026, 8, 20, 9, 0, 0)));

    [Fact]
    public void As_duplas_dos_anuncios_viram_SQL() =>
        // ⚠️ Esta é uma das duas que estouravam. O par de ids sai como COLUNAS num tipo
        // anônimo; virar `new[] { ... }` de novo reprova aqui na hora.
        Traduz(ConsultasDoResumoSemanal.DuplasDosAnuncios);

    [Fact]
    public void Os_envolvidos_dos_desafios_viram_SQL() =>
        // ⚠️ A outra. E o motivo de ela existir como consulta própria: reusar
        // `Desafio.Envolvidos` aqui traria de volta o defeito — a propriedade é `[NotMapped]` e
        // devolve exatamente o array literal que o provedor recusa.
        Traduz(ConsultasDoResumoSemanal.EnvolvidosDosDesafios);

    [Fact]
    public void Os_candidatos_a_receber_viram_SQL()
    {
        // O `Contains` sobre lista de ids — o filtro que monta a lista final de quem recebe.
        var ids = new List<int> { 1, 2, 3 };
        Traduz(ctx => ctx.Jogadores.AsNoTracking().Where(j => ids.Contains(j.Id)));
    }

    [Fact]
    public void A_marca_do_ultimo_envio_vira_SQL() =>
        Traduz(ctx => ctx.ConfiguracoesDoSistema.AsNoTracking()
            .Where(c => c.Chave == ResumoSemanalDoMural.ChaveDoUltimoEnvio));
}
