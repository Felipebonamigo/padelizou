using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// EstatisticasService com banco em memória: ranking por categoria, ranking de um
// torneio, nível comprovado (anti-sandbagging) e filtro regional multi-cidade.
public class EstatisticasServiceTests
{
    // Cenário base: 1 torneio, 1 categoria "2ª Categoria Masculina", 2 duplas.
    // Dupla A (J1/J2) campeã com 1 vitória sobre a Dupla B (J3/J4, vice).
    private static (DbPadelContext ctx, EstatisticasService svc, Torneio torneio,
        Jogador j1, Jogador j3) CenarioCampeaoEVice(DateTime? dataInicio = null)
    {
        var ctx = TestInfra.NovoContexto();

        var j1 = new Jogador { Nome = "J1", Cpf = "99900000001", Cidade = "Gravataí", Estado = "RS" };
        var j2 = new Jogador { Nome = "J2", Cpf = "99900000002", Cidade = "Gravataí", Estado = "RS" };
        var j3 = new Jogador { Nome = "J3", Cpf = "99900000003", Cidade = "Canoas", Estado = "RS" };
        var j4 = new Jogador { Nome = "J4", Cpf = "99900000004", Cidade = "São Paulo", Estado = "SP" };
        ctx.Jogadores.AddRange(j1, j2, j3, j4);

        var torneio = new Torneio
        {
            Nome = "Copa Teste",
            Codigo = "CPT001",
            Status = "Finalizado",
            DataInicio = dataInicio ?? new DateTime(2026, 6, 1),
        };
        var cat = new Categoria { Nome = "2ª Categoria Masculina", Codigo = "C2M", Torneio = torneio };
        ctx.AddRange(torneio, cat);

        var duplaA = new Dupla { Categoria = cat, Jogador1 = j1, Jogador2 = j2, UltimaFase = "Campeao" };
        var duplaB = new Dupla { Categoria = cat, Jogador1 = j3, Jogador2 = j4, UltimaFase = "Final" };
        ctx.Duplas.AddRange(duplaA, duplaB);
        ctx.SaveChanges();

        ctx.Partidas.Add(new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = cat.Id,
            Dupla1Id = duplaA.Id,
            Dupla2Id = duplaB.Id,
            Fase = "Final",
            Status = "Finalizada",
            Codigo = "FIN001",
            VencedorId = duplaA.Id,
            GamesDupla1 = 9,
            GamesDupla2 = 5,
        });
        ctx.SaveChanges();

        return (ctx, new EstatisticasService(ctx), torneio, j1, j3);
    }

    [Fact]
    public async Task Ranking_do_torneio_agrega_pontos_jogos_vitorias_e_titulos_por_jogador()
    {
        var (ctx, svc, torneio, _, _) = CenarioCampeaoEVice();
        using var _ctx = ctx;

        var ranking = await svc.ObterRankingDoTorneioAsync(torneio.Id);

        Assert.Equal(4, ranking.Count); // 4 jogadores
        var campeao = ranking.First();
        Assert.Equal(100, campeao.Pontos);   // Campeao = 100
        Assert.Equal(1, campeao.Titulos);
        Assert.Equal(1, campeao.Jogos);
        Assert.Equal(1, campeao.Vitorias);

        var vice = ranking.First(l => l.Pontos == 60); // Final = 60
        Assert.Equal(0, vice.Titulos);
        Assert.Equal(1, vice.Jogos);
        Assert.Equal(0, vice.Vitorias);
    }

    [Fact]
    public async Task Ranking_por_categoria_soma_pontos_de_ambos_os_jogadores_da_dupla()
    {
        var (ctx, svc, _, _, _) = CenarioCampeaoEVice();
        using var _ctx = ctx;

        var ranking = await svc.ObterRankingPorCategoriaAsync();

        var cat = Assert.Single(ranking);
        Assert.Equal("2ª Categoria Masculina", cat.Categoria);
        Assert.Equal(4, cat.Linhas.Count);
        Assert.Equal(100, cat.Linhas[0].Pontos); // campeões na frente
        Assert.Equal(1, cat.Linhas[0].Titulos);
        Assert.Contains(cat.Linhas, l => l.Pontos == 60 && l.Finais == 1);
    }

    [Fact]
    public async Task Ranking_por_categoria_respeita_cortes_de_periodo()
    {
        var (ctx, svc, _, _, _) = CenarioCampeaoEVice(dataInicio: new DateTime(2026, 6, 1));
        using var _ctx = ctx;

        // Torneio de junho: fora de um corte "só até maio", dentro de um corte "a partir de maio".
        Assert.Empty(await svc.ObterRankingPorCategoriaAsync(ate: new DateTime(2026, 5, 31)));
        Assert.Single(await svc.ObterRankingPorCategoriaAsync(de: new DateTime(2026, 5, 1)));
        Assert.Empty(await svc.ObterRankingPorCategoriaAsync(de: new DateTime(2026, 7, 1)));
    }

    [Fact]
    public async Task Nivel_comprovado_e_a_categoria_mais_forte_com_final_ou_titulo()
    {
        using var ctx = TestInfra.NovoContexto();
        var j = new Jogador { Nome = "Atleta", Cpf = "99900000010" };
        var p = new Jogador { Nome = "Parceiro", Cpf = "99900000011" };
        ctx.Jogadores.AddRange(j, p);

        var torneio = new Torneio { Nome = "T", Codigo = "T1", DataInicio = new DateTime(2026, 1, 1) };
        var cat5 = new Categoria { Nome = "5ª Categoria Masculina", Codigo = "C5", Torneio = torneio };
        var cat3 = new Categoria { Nome = "3ª Categoria Masculina", Codigo = "C3", Torneio = torneio };
        ctx.AddRange(torneio, cat5, cat3);

        // Campeão na 5ª (fraca) e vice na 3ª (mais forte): o nível comprovado é a 3ª.
        ctx.Duplas.Add(new Dupla { Categoria = cat5, Jogador1 = j, Jogador2 = p, UltimaFase = "Campeao" });
        ctx.Duplas.Add(new Dupla { Categoria = cat3, Jogador1 = j, Jogador2 = p, UltimaFase = "Final" });
        ctx.SaveChanges();

        var svc = new EstatisticasService(ctx);
        var niveis = await svc.ObterNiveisComprovadosAsync("Final");

        Assert.Equal("3ª Categoria Masculina", niveis[j.Id].Categoria);

        // Com gatilho "Livre" ninguém trava.
        Assert.Empty(await svc.ObterNiveisComprovadosAsync("Livre"));

        // O método de perfil devolve o mesmo nível.
        var doPerfil = await svc.ObterNivelComprovadoJogadorAsync(j.Id);
        Assert.NotNull(doPerfil);
        Assert.Equal("3ª Categoria Masculina", doPerfil!.Categoria);
    }

    [Fact]
    public async Task Semifinalista_nao_comprova_nivel_com_gatilho_Final()
    {
        using var ctx = TestInfra.NovoContexto();
        var j = new Jogador { Nome = "Semi", Cpf = "99900000012" };
        var p = new Jogador { Nome = "Par", Cpf = "99900000013" };
        ctx.Jogadores.AddRange(j, p);
        var torneio = new Torneio { Nome = "T", Codigo = "T2" };
        var cat = new Categoria { Nome = "Categoria Open Masculino", Codigo = "CO", Torneio = torneio };
        ctx.AddRange(torneio, cat);
        ctx.Duplas.Add(new Dupla { Categoria = cat, Jogador1 = j, Jogador2 = p, UltimaFase = "Semifinal" });
        ctx.SaveChanges();

        var svc = new EstatisticasService(ctx);
        Assert.False((await svc.ObterNiveisComprovadosAsync("Final")).ContainsKey(j.Id));
        Assert.True((await svc.ObterNiveisComprovadosAsync("Semifinal")).ContainsKey(j.Id));
    }

    [Fact]
    public async Task Filtro_regional_aceita_varias_cidades_como_uniao()
    {
        var (ctx, svc, _, _, _) = CenarioCampeaoEVice();
        using var _ctx = ctx;

        // Sem filtro nenhum → null (país todo).
        Assert.Null(await svc.ObterJogadoresDoLocalAsync(null, null));
        Assert.Null(await svc.ObterJogadoresDoLocalAsync(Array.Empty<string>(), null));

        // Uma cidade só.
        var soGravatai = await svc.ObterJogadoresDoLocalAsync(new[] { "Gravataí" }, "RS");
        Assert.Equal(2, soGravatai!.Count); // J1 e J2

        // Duas cidades = união.
        var duasCidades = await svc.ObterJogadoresDoLocalAsync(new[] { "Gravataí", "Canoas" }, "RS");
        Assert.Equal(3, duasCidades!.Count); // + J3

        // Estado limita a união (J4 é de SP e fica de fora mesmo sem cidade).
        var soRs = await svc.ObterJogadoresDoLocalAsync(null, "RS");
        Assert.Equal(3, soRs!.Count);

        // Caixa diferente não muda o resultado.
        var caixaDiferente = await svc.ObterJogadoresDoLocalAsync(new[] { "GRAVATAÍ" }, "rs");
        Assert.Equal(2, caixaDiferente!.Count);
    }

    [Fact]
    public async Task Hub_do_ranking_monta_secoes_com_vitorias_e_respeita_periodo()
    {
        // Primeiro dia do mês atual: garante que o torneio conta no período "mes" em qualquer data de execução.
        var (ctx, svc, _, _, _) = CenarioCampeaoEVice(dataInicio: new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1));
        using var _ctx = ctx;

        var hub = await svc.ObterRankingHubAsync();

        // Vitórias na aba de pontos (coluna nova).
        var linhas = Assert.Single(hub.PorCategoria).Linhas;
        Assert.Equal(1, linhas[0].Vitorias);      // campeão venceu a final
        Assert.Contains(linhas, l => l.Vitorias == 0);

        Assert.NotEmpty(hub.VitoriasJogadores);
        Assert.NotEmpty(hub.VitoriasDuplas);
        Assert.Contains("2ª Categoria Masculina", hub.TodasCategorias);
        Assert.Equal("sempre", hub.Periodo);

        // Período "mes": o torneio é deste mês, então as seções continuam preenchidas.
        var hubMes = await svc.ObterRankingHubAsync(periodo: "mes");
        Assert.Equal("mes", hubMes.Periodo);
        Assert.NotEmpty(hubMes.VitoriasJogadores);
    }
}
