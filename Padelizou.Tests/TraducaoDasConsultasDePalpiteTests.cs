using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// AS CONSULTAS DO PALPITRÔMETRO REALMENTE VIRAM SQL?
//
// 🕳️ O buraco que estes testes fecham: **o banco InMemory do resto da suíte não traduz nada**.
// Lá tudo é objeto em memória, então uma consulta que o Postgres recusaria passa lisa — e
// estoura em produção na PRIMEIRA visita à página. É a categoria de erro que atravessa 4 mil
// testes verdes e cai no colo do usuário.
//
// 💥 E ELA JÁ ACONTECEU AQUI, em 19/08/2026: `ConsultaDePartidas` filtrava DEPOIS de projetar
// pro record (`.Select(...).Where(p => p.TorneioId == id)`), e o EF não sabe achar a coluna a
// partir de um record projetado. A página `/Torneios/Palpiteiros` respondia **500** com a
// suíte inteira verde. O filtro passou pra dentro da consulta, antes do `Select`.
//
// 🎯 COMO ESTES TESTES FUNCIONAM: `ToQueryString()` **compila** a consulta e devolve o SQL sem
// abrir conexão nenhuma. Consulta que não traduz estoura ali mesmo; nenhum banco precisa
// existir, e não há espera de rede.
//
// ⚠️ POR QUE NÃO "CHAMAR O SERVIÇO E VER SE ELE EXPLODE": foi a primeira versão destes testes,
// e ela era **cega**. Apontando pra um Postgres inexistente, a PRIMEIRA consulta do método
// falha por CONEXÃO e o método aborta — as outras nunca chegam a ser compiladas. Foi
// exatamente assim que o `Where` sobre record projetado passou por eles: ele morava na segunda
// consulta do método. Por isso aqui cada consulta é compilada **sozinha**.
public class TraducaoDasConsultasDePalpiteTests
{
    // Um provedor Npgsql de verdade (é ele que traduz), apontado pra lugar nenhum — nada aqui
    // chega a conectar.
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
        // ⚠️ Quem reprova é o próprio `ToQueryString`, que ESTOURA na consulta que não traduz.
        // O assert é só a confirmação de que saiu SQL — e é `Contains`, não `StartsWith`,
        // porque o EF prefixa os parâmetros como comentário (`-- @ids={ '7' }`).
        var sql = consulta(ctx).ToQueryString();
        Assert.Contains("SELECT", sql);
    }

    [Fact]
    public void As_partidas_de_UM_TORNEIO_viram_SQL() =>
        Traduz(ctx => RankingDePalpiteiros.ConsultaDePartidas(ctx, p => p.TorneioId == 1));

    [Fact]
    public void As_partidas_de_uma_LISTA_DE_IDS_viram_SQL()
    {
        // O caminho do hub e do perfil: parte-se dos palpites e buscam-se aquelas partidas.
        var ids = new List<int> { 1, 2, 3 };
        Traduz(ctx => RankingDePalpiteiros.ConsultaDePartidas(ctx, p => ids.Contains(p.Id)));
    }

    [Fact]
    public void Os_palpites_de_UMA_LISTA_DE_PARTIDAS_viram_SQL()
    {
        var ids = new List<int> { 1, 2, 3 };
        Traduz(ctx => RankingDePalpiteiros.ConsultaDePalpites(ctx, v => ids.Contains(v.PartidaId)));
    }

    [Fact]
    public void Os_palpites_de_UM_JOGADOR_viram_SQL()
    {
        var ids = new List<int> { 7 };
        Traduz(ctx => RankingDePalpiteiros.ConsultaDePalpites(ctx, v => ids.Contains(v.JogadorId)));
    }

    [Fact]
    public void TODOS_os_palpites_viram_SQL() =>
        // O caminho da aba do hub sem filtro regional.
        Traduz(ctx => RankingDePalpiteiros.ConsultaDePalpites(ctx, v => true));

    // ⚠️ Este é o teste que teria pego o 500. Ele imita o erro: filtrar DEPOIS de projetar.
    // Se um dia o EF passar a traduzir isso, o teste vira vermelho e alguém relê a régua — que
    // é exatamente o que se quer de um teste que guarda uma limitação de fora.
    [Fact]
    public void Filtrar_DEPOIS_de_projetar_pro_record_NAO_traduz()
    {
        using var ctx = ContextoPostgres();

        var consultaTorta = ctx.Partidas
            .Select(p => new PartidaApurada(p.Id, p.TorneioId, p.VencedorId, p.Dupla1Id, p.Dupla2Id,
                p.GamesDupla1, p.GamesDupla2, p.SetsDupla1, p.SetsDupla2, p.MotivoDoEncerramento))
            .Where(p => p.TorneioId == 1);

        var erro = Assert.Throws<InvalidOperationException>(() => consultaTorta.ToQueryString());
        Assert.Contains("could not be translated", erro.Message);
    }
}
