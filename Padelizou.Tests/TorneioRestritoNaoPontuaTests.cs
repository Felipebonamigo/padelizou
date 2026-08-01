using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Decisão do Felipe (31/07/2026): torneio RESTRITO não conta pontos pro ranking.
//
// Restrito é o evento fechado — entra quem tem a chave. Pontuar evento fechado faria o
// ranking medir acesso a torneio privado em vez de padel jogado. O que continua valendo é
// a história: participação, título e jogos seguem no perfil.
public class TorneioRestritoNaoPontuaTests
{
    private static async Task<(DbPadelContext ctx, Jogador jogador)> MontarAsync(
        bool restrito, string ultimaFase = "Campeao")
    {
        var ctx = TestInfra.NovoContexto();

        var jogador = new Jogador { Id = 1, Nome = "Campeão", Cpf = "99900000001" };
        var parceiro = new Jogador { Id = 2, Nome = "Parceiro", Cpf = "99900000002" };
        ctx.Jogadores.AddRange(jogador, parceiro);

        var torneio = new Torneio
        {
            Id = 1, Nome = "Interno", Codigo = "INT1",
            Restrito = restrito, ChaveAcesso = restrito ? "virgili10" : null,
            DataInicio = new DateTime(2026, 7, 1),
        };
        ctx.Torneios.Add(torneio);

        var categoria = new Categoria { Id = 1, TorneioId = 1, Nome = "4ª Categoria Masculina", Codigo = "4M" };
        ctx.Categorias.Add(categoria);

        ctx.Duplas.Add(new Dupla
        {
            Id = 1, CategoriaId = 1, Jogador1Id = 1, Jogador2Id = 2, UltimaFase = ultimaFase,
        });

        await ctx.SaveChangesAsync();
        return (ctx, jogador);
    }

    [Fact]
    public async Task Torneio_aberto_pontua_como_sempre()
    {
        var (ctx, _) = await MontarAsync(restrito: false);
        using var _ctx = ctx;

        var resumo = await new EstatisticasService(ctx).ObterResumoJogadorAsync(1);

        Assert.Equal(100, resumo.Pontos);   // Campeão = 100
        Assert.Equal(1, resumo.Titulos);
    }

    [Fact]
    public async Task Torneio_restrito_nao_da_ponto_nenhum()
    {
        var (ctx, _) = await MontarAsync(restrito: true);
        using var _ctx = ctx;

        var resumo = await new EstatisticasService(ctx).ObterResumoJogadorAsync(1);

        Assert.Equal(0, resumo.Pontos);
    }

    [Fact]
    public async Task Mas_o_titulo_e_a_participacao_do_restrito_continuam_no_perfil()
    {
        // O torneio aconteceu e a pessoa foi campeã: apagar isso do perfil seria apagar
        // história. O que não existe é PONTO de ranking.
        var (ctx, _) = await MontarAsync(restrito: true);
        using var _ctx = ctx;

        var resumo = await new EstatisticasService(ctx).ObterResumoJogadorAsync(1);

        Assert.Equal(1, resumo.Titulos);
        Assert.Equal(1, resumo.TotalTorneios);
    }

    [Fact]
    public async Task O_ranking_por_categoria_nao_lista_quem_so_jogou_restrito()
    {
        var (ctx, _) = await MontarAsync(restrito: true);
        using var _ctx = ctx;

        var ranking = await new EstatisticasService(ctx).ObterRankingPorCategoriaAsync();

        // Nenhuma linha: a única participação da base é de torneio fechado.
        Assert.DoesNotContain(ranking.SelectMany(r => r.Linhas), l => l.Jogador.Id == 1);
    }

    [Fact]
    public async Task Os_pontos_do_sorteio_e_da_busca_tambem_ignoram_o_restrito()
    {
        // Mesma régua em ObterPontosPorJogadorAsync: é a que define cabeça de chave. Sem
        // isto, quem joga muitos internos viraria cabeça de chave por fora do ranking.
        var (ctx, _) = await MontarAsync(restrito: true);
        using var _ctx = ctx;

        var pontos = await new EstatisticasService(ctx).ObterPontosPorJogadorAsync(new[] { 1, 2 });

        Assert.Equal(0, pontos[1]);
        Assert.Equal(0, pontos[2]);
    }

    [Fact]
    public async Task A_evolucao_do_grafico_termina_no_mesmo_total_do_perfil()
    {
        // O gráfico e o número do perfil vinham de contas separadas; divergir faria a
        // pessoa ver dois totais diferentes na mesma tela.
        var (ctx, _) = await MontarAsync(restrito: true);
        using var _ctx = ctx;

        var servico = new EstatisticasService(ctx);
        var resumo = await servico.ObterResumoJogadorAsync(1);
        var evolucao = await servico.ObterEvolucaoJogadorAsync(1);

        Assert.Equal(resumo.Pontos, evolucao.Meses.LastOrDefault()?.Acumulado ?? 0);
    }

    [Fact]
    public void A_regra_pura_diz_o_que_conta()
    {
        Assert.True(EstatisticasService.ContaNoRanking(new Torneio { Nome = "x", Codigo = "x" }));
        Assert.False(EstatisticasService.ContaNoRanking(new Torneio { Nome = "x", Codigo = "x", Restrito = true }));
        // Dupla sem torneio carregado (consulta sem Include) não pode sumir do ranking.
        Assert.True(EstatisticasService.ContaNoRanking(null));
    }
}
