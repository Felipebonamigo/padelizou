using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// TRETA (25/11/2026) — dois jeitos novos de NÃO pontuar, ver TRETA.md:
//
//  1. o torneio inteiro marcado `ForaDoRanking` (🗣️ organizador do Treta: "não" conta);
//  2. a categoria com papel (DTN ou Finalíssima), mesmo num torneio que conta. As duplas dela
//     são CÓPIAS das do módulo: sem isto, cada atleta levaria ponto de participação duas vezes,
//     e um "Final" no DTN pagaria os pontos de finalista a quem PERDEU no grupo.
//
// ⚠️ A régua tem várias escritas (entidade, projeção enxuta, expressão SQL, selo de movimento,
// gráfico), e cada uma é perguntada aqui — é o mesmo cuidado do TorneioDeUmTimeSoNaoPontuaTests.
public class TretaForaDoRankingTests
{
    public enum Caso { Aberto, TorneioForaDoRanking, SoNaDtn }

    private static async Task<DbPadelContext> MontarAsync(Caso caso)
    {
        var ctx = TestInfra.NovoContexto();

        ctx.Jogadores.AddRange(
            new Jogador { Id = 1, Nome = "Atleta", Cpf = "11144477735" },
            new Jogador { Id = 2, Nome = "Parceiro", Cpf = "52998224725" });

        ctx.Torneios.Add(new Torneio
        {
            Id = 1, Nome = "Treta", Codigo = "TRETA1",
            ForaDoRanking = caso == Caso.TorneioForaDoRanking,
            DataInicio = new DateTime(2026, 7, 1),
            Status = "Finalizado",
        });
        ctx.Categorias.Add(new Categoria { Id = 1, TorneioId = 1, Nome = "Branco", Codigo = "BR" });
        ctx.Categorias.Add(new Categoria
        {
            Id = 2, TorneioId = 1, Nome = "DTN Branco", Codigo = "BRDTN",
            PapelDaChave = ChaveDtn.Dtn, CategoriaDeOrigemId = 1,
        });

        // No caso SoNaDtn a única participação do atleta é uma dupla de DTN que "chegou à
        // final" (perdeu a semi invertida = ganhou o jogo). Nos outros, ele é campeão do módulo.
        if (caso == Caso.SoNaDtn)
            ctx.Duplas.Add(new Dupla { Id = 1, CategoriaId = 2, Jogador1Id = 1, Jogador2Id = 2, UltimaFase = "Final" });
        else
            ctx.Duplas.Add(new Dupla { Id = 1, CategoriaId = 1, Jogador1Id = 1, Jogador2Id = 2, UltimaFase = "Campeao" });

        // Enchimento até 5 duplas em cada categoria: 5 é a calibração em que campeão vale 100.
        for (int i = 0; i < 8; i++)
        {
            var a = new Jogador { Id = 10 + i, Nome = $"Enchimento {i}A", Cpf = $"9990001{i:00}1" };
            var b = new Jogador { Id = 30 + i, Nome = $"Enchimento {i}B", Cpf = $"9990002{i:00}2" };
            ctx.Jogadores.AddRange(a, b);
            ctx.Duplas.Add(new Dupla
            {
                Id = 10 + i, CategoriaId = i < 4 ? 1 : 2, Jogador1Id = a.Id, Jogador2Id = b.Id, UltimaFase = "Grupos",
            });
        }

        await ctx.SaveChangesAsync();
        return ctx;
    }

    // ── A régua pura ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Torneio_fora_do_ranking_nao_conta_na_regra_da_entidade()
    {
        Assert.True(EstatisticasService.ContaNoRanking(new Torneio { Nome = "x", Codigo = "x" }));
        Assert.False(EstatisticasService.ContaNoRanking(new Torneio { Nome = "x", Codigo = "x", ForaDoRanking = true }));
    }

    [Fact]
    public void Torneio_fora_do_ranking_nao_conta_na_regra_dos_campos()
    {
        Assert.True(EstatisticasService.ContaNoRanking(
            restrito: false, timeExclusivoId: null, formato: "Padrao", foraDoRanking: false));
        Assert.False(EstatisticasService.ContaNoRanking(
            restrito: false, timeExclusivoId: null, formato: "Padrao", foraDoRanking: true));
    }

    [Fact]
    public void Categoria_com_papel_nao_conta_mesmo_em_torneio_que_conta()
    {
        var torneio = new Torneio { Nome = "x", Codigo = "x" };
        Assert.True(EstatisticasService.CategoriaContaNoRanking(new Categoria { Nome = "B", Codigo = "B", Torneio = torneio }));
        Assert.False(EstatisticasService.CategoriaContaNoRanking(
            new Categoria { Nome = "D", Codigo = "D", Torneio = torneio, PapelDaChave = ChaveDtn.Dtn }));
        Assert.False(EstatisticasService.CategoriaContaNoRanking(
            new Categoria { Nome = "F", Codigo = "F", Torneio = torneio, PapelDaChave = ChaveDtn.Finalissima }));
    }

    [Fact]
    public void A_regra_escrita_pra_rodar_no_banco_diz_a_mesma_coisa()
    {
        foreach (var fora in new[] { false, true })
        foreach (var papel in new string?[] { null, ChaveDtn.Dtn })
        {
            var torneio = new Torneio { Nome = "x", Codigo = "x", ForaDoRanking = fora };
            var categoria = new Categoria { Nome = "B", Codigo = "B", Torneio = torneio, PapelDaChave = papel };
            var dupla = new Dupla { Categoria = categoria };

            Assert.Equal(
                EstatisticasService.CategoriaContaNoRanking(categoria),
                EstatisticasService.DuplaContaNoRanking.Compile()(dupla));
        }
    }

    // ── Os caminhos de verdade ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Aberto_pontua_como_sempre()
    {
        using var ctx = await MontarAsync(Caso.Aberto);
        var resumo = await new EstatisticasService(ctx).ObterResumoJogadorAsync(1);
        Assert.Equal(100, resumo.Pontos);
    }

    [Theory]
    [InlineData(Caso.TorneioForaDoRanking)]
    [InlineData(Caso.SoNaDtn)]
    public async Task Nao_da_ponto_no_perfil(Caso caso)
    {
        using var ctx = await MontarAsync(caso);
        var resumo = await new EstatisticasService(ctx).ObterResumoJogadorAsync(1);
        Assert.Equal(0, resumo.Pontos);
    }

    [Theory]
    [InlineData(Caso.TorneioForaDoRanking)]
    [InlineData(Caso.SoNaDtn)]
    public async Task Nao_entra_no_ranking_por_categoria(Caso caso)
    {
        using var ctx = await MontarAsync(caso);
        var ranking = await new EstatisticasService(ctx).ObterRankingPorCategoriaAsync();
        Assert.DoesNotContain(ranking.SelectMany(r => r.Linhas), l => l.Jogador.Id == 1);
    }

    [Theory]
    [InlineData(Caso.TorneioForaDoRanking)]
    [InlineData(Caso.SoNaDtn)]
    public async Task Nao_vira_cabeca_de_chave(Caso caso)
    {
        using var ctx = await MontarAsync(caso);
        var pontos = await new EstatisticasService(ctx).ObterPontosPorJogadorAsync(new[] { 1, 2 });
        Assert.Equal(0, pontos[1]);
    }

    [Theory]
    [InlineData(Caso.TorneioForaDoRanking)]
    [InlineData(Caso.SoNaDtn)]
    public async Task A_evolucao_termina_no_mesmo_total_do_perfil(Caso caso)
    {
        using var ctx = await MontarAsync(caso);
        var servico = new EstatisticasService(ctx);
        var resumo = await servico.ObterResumoJogadorAsync(1);
        var evolucao = await servico.ObterEvolucaoJogadorAsync(1);
        Assert.Equal(resumo.Pontos, evolucao.Meses.LastOrDefault()?.Acumulado ?? 0);
    }

    [Fact]
    public async Task O_selo_de_movimento_ignora_torneio_fora_do_ranking()
    {
        var agora = new DateTime(2026, 7, 3);
        using (var aberto = await MontarAsync(Caso.Aberto))
            Assert.NotNull(await MovimentoNoRanking.DoOficialAsync(aberto, agora));

        using var ctx = await MontarAsync(Caso.TorneioForaDoRanking);
        Assert.Null(await MovimentoNoRanking.DoOficialAsync(ctx, agora));
    }

    // ── Padelímetro ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(false, null, true)]
    [InlineData(true, null, false)]
    [InlineData(false, ChaveDtn.Dtn, false)]
    [InlineData(false, ChaveDtn.Finalissima, false)]
    public void Padelimetro_so_conta_jogo_que_conta_no_ranking(bool foraDoRanking, string? papel, bool esperado)
    {
        var torneio = new Torneio { Nome = "x", Codigo = "x", ForaDoRanking = foraDoRanking };
        var categoria = new Categoria { Nome = "B", Codigo = "B", Torneio = torneio, PapelDaChave = papel };
        var d1 = new Dupla { Id = 1, Jogador1Id = 1, Jogador2Id = 2 };
        var d2 = new Dupla { Id = 2, Jogador1Id = 3, Jogador2Id = 4 };
        var partida = new Partida { Status = "Finalizada", GamesDupla1 = 6, GamesDupla2 = 3, VencedorId = 1 };

        Assert.Equal(esperado, PadelimetroService.Conta(partida, categoria, d1, d2));
    }
}
