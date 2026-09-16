using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using padelizou.Controllers;

namespace Padelizou.Tests;

// O TORNEIO DE UM TIME SÓ É EVENTO FECHADO (decisão do Felipe, 16/09/2026).
//
// Mesmo tratamento que o torneio de chave de acesso já tinha desde 31/07/2026, e pelo mesmo
// motivo: um interno de time mede quem está no time, não padel jogado contra o mundo. Quem
// organiza um interno por mês subiria no ranking sem nunca enfrentar ninguém de fora.
//
// O que NÃO muda: participação, título e jogos continuam no perfil. Aconteceram.
//
// ⚠️ A régua está escrita em MAIS DE UM LUGAR de propósito (a entidade, a projeção enxuta e a
// expressão que vira SQL), e foi exatamente isso que já deixou o Americano pontuando no
// ranking oficial por meses: corrigiram uma cópia e a outra seguiu contando. Por isso cada
// caminho é perguntado aqui separadamente, e não só a regra pura.
public class TorneioDeUmTimeSoNaoPontuaTests
{
    private const int TimeDaCasa = 7;

    private static async Task<DbPadelContext> MontarAsync(int? timeExclusivoId)
    {
        var ctx = TestInfra.NovoContexto();

        ctx.Times.Add(new Time { Id = TimeDaCasa, Nome = "Nata Padel" });
        ctx.Jogadores.AddRange(
            new Jogador { Id = 1, Nome = "Campeão", Cpf = "11144477735", TimeId = TimeDaCasa },
            new Jogador { Id = 2, Nome = "Parceiro", Cpf = "52998224725", TimeId = TimeDaCasa });

        ctx.Torneios.Add(new Torneio
        {
            Id = 1, Nome = "Interno do Nata", Codigo = "NATA1",
            TimeExclusivoId = timeExclusivoId,
            DataInicio = new DateTime(2026, 7, 1),
            // Torneio com campeão está finalizado — em "Inscrições Abertas" ninguém paga
            // ponto nenhum desde 10/08/2026, e o teste do caso ABERTO passaria por acidente.
            Status = "Finalizado",
        });
        ctx.Categorias.Add(new Categoria { Id = 1, TorneioId = 1, Nome = "4ª Categoria Masculina", Codigo = "4M" });
        ctx.Duplas.Add(new Dupla { Id = 1, CategoriaId = 1, Jogador1Id = 1, Jogador2Id = 2, UltimaFase = "Campeao" });

        // Enchimento até 5 duplas por causa do peso por tamanho (10/08/2026): 5 é o ponto de
        // calibração, onde campeão vale os 100 de sempre. Com uma dupla só o campeão levaria
        // 10, e o caso do torneio ABERTO mentiria.
        for (int i = 0; i < 4; i++)
        {
            var a = new Jogador { Id = 10 + i, Nome = $"Enchimento {i}A", Cpf = $"9990001{i:00}1" };
            var b = new Jogador { Id = 20 + i, Nome = $"Enchimento {i}B", Cpf = $"9990002{i:00}2" };
            ctx.Jogadores.AddRange(a, b);
            ctx.Duplas.Add(new Dupla
            {
                Id = 10 + i, CategoriaId = 1, Jogador1Id = a.Id, Jogador2Id = b.Id, UltimaFase = "Grupos",
            });
        }

        await ctx.SaveChangesAsync();
        return ctx;
    }

    // ── A régua pura, nas três escritas ───────────────────────────────────────────────────

    [Fact]
    public void A_regra_que_recebe_a_entidade()
    {
        Assert.True(EstatisticasService.ContaNoRanking(new Torneio { Nome = "x", Codigo = "x" }));
        Assert.False(EstatisticasService.ContaNoRanking(
            new Torneio { Nome = "x", Codigo = "x", TimeExclusivoId = TimeDaCasa }));
    }

    [Fact]
    public void A_regra_que_recebe_so_os_campos_projetados()
    {
        // ⚠️ É esta que as consultas enxutas (`.Select`) usam. Ela já tinha esquecido o
        // Americano uma vez, e cada esquecimento foi um bug diferente: pontos do perfil,
        // pontos da busca, ranking de times.
        Assert.True(EstatisticasService.ContaNoRanking(restrito: false, timeExclusivoId: null, formato: "Padrao"));
        Assert.False(EstatisticasService.ContaNoRanking(restrito: false, timeExclusivoId: TimeDaCasa, formato: "Padrao"));
        Assert.False(EstatisticasService.ContaNoRanking(restrito: true, timeExclusivoId: null, formato: "Padrao"));
    }

    [Fact]
    public void A_regra_escrita_pra_rodar_no_banco_diz_a_mesma_coisa()
    {
        // As duas escritas, lado a lado, no mesmo caso. Divergir aqui é como o Americano
        // continuou pontuando no ranking de times depois de já ter saído do individual.
        foreach (var timeExclusivoId in new int?[] { null, TimeDaCasa })
        {
            var torneio = new Torneio { Nome = "x", Codigo = "x", TimeExclusivoId = timeExclusivoId };
            var dupla = new Dupla
            {
                Categoria = new Categoria { Nome = "4M", Codigo = "4M", Torneio = torneio },
            };

            Assert.Equal(
                EstatisticasService.ContaNoRanking(torneio),
                EstatisticasService.DuplaContaNoRanking.Compile()(dupla));
        }
    }

    // ── Os caminhos de verdade ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Torneio_aberto_pontua_como_sempre()
    {
        using var ctx = await MontarAsync(timeExclusivoId: null);

        var resumo = await new EstatisticasService(ctx).ObterResumoJogadorAsync(1);

        Assert.Equal(100, resumo.Pontos);
        Assert.Equal(1, resumo.Titulos);
    }

    [Fact]
    public async Task Torneio_de_um_time_so_nao_da_ponto_nenhum()
    {
        using var ctx = await MontarAsync(timeExclusivoId: TimeDaCasa);

        var resumo = await new EstatisticasService(ctx).ObterResumoJogadorAsync(1);

        Assert.Equal(0, resumo.Pontos);
    }

    [Fact]
    public async Task Mas_o_titulo_e_a_participacao_continuam_no_perfil()
    {
        using var ctx = await MontarAsync(timeExclusivoId: TimeDaCasa);

        var resumo = await new EstatisticasService(ctx).ObterResumoJogadorAsync(1);

        Assert.Equal(1, resumo.Titulos);
        Assert.Equal(1, resumo.TotalTorneios);
    }

    [Fact]
    public async Task O_ranking_por_categoria_nao_lista_quem_so_jogou_interno_de_time()
    {
        using var ctx = await MontarAsync(timeExclusivoId: TimeDaCasa);

        var ranking = await new EstatisticasService(ctx).ObterRankingPorCategoriaAsync();

        Assert.DoesNotContain(ranking.SelectMany(r => r.Linhas), l => l.Jogador.Id == 1);
    }

    [Fact]
    public async Task Os_pontos_que_definem_cabeca_de_chave_tambem_ignoram()
    {
        // ObterPontosPorJogadorAsync é o que decide cabeça de chave no sorteio. Sem isto,
        // quem joga muitos internos do time viraria cabeça de chave por fora do ranking.
        using var ctx = await MontarAsync(timeExclusivoId: TimeDaCasa);

        var pontos = await new EstatisticasService(ctx).ObterPontosPorJogadorAsync(new[] { 1, 2 });

        Assert.Equal(0, pontos[1]);
        Assert.Equal(0, pontos[2]);
    }

    [Fact]
    public async Task O_ranking_de_TIMES_tambem_ignora()
    {
        // ⚠️ Esta é a consulta que filtra NO BANCO, por `DuplaContaNoRanking` — a única que
        // não passa pela régua em C#. E é justamente a tela onde um interno de time seria
        // mais tentador: o time pontuaria no ranking de times jogando contra si mesmo.
        using var ctx = await MontarAsync(timeExclusivoId: TimeDaCasa);

        var ranking = await new EstatisticasService(ctx).ObterRankingTimesAsync();

        Assert.DoesNotContain(ranking, linha => linha.Pontos > 0);
    }

    [Fact]
    public async Task A_evolucao_do_grafico_termina_no_mesmo_total_do_perfil()
    {
        // O gráfico e o número do perfil vinham de contas separadas; divergir faria a pessoa
        // ver dois totais diferentes na mesma tela.
        using var ctx = await MontarAsync(timeExclusivoId: TimeDaCasa);

        var servico = new EstatisticasService(ctx);
        var resumo = await servico.ObterResumoJogadorAsync(1);
        var evolucao = await servico.ObterEvolucaoJogadorAsync(1);

        Assert.Equal(resumo.Pontos, evolucao.Meses.LastOrDefault()?.Acumulado ?? 0);
    }

    [Fact]
    public async Task O_selo_de_movimento_do_ranking_nao_se_mexe_por_interno_de_time()
    {
        // MovimentoNoRanking.DoOficialAsync tem a régua escrita À MÃO pra virar SQL (o EF
        // não traduz método). Era mais uma cópia que podia divergir calada: o selo "subiu 3
        // posições" apareceria carimbado por um torneio que não moveu ponto nenhum.
        //
        // ⚠️ `agora` fica DENTRO dos 7 dias de MovimentoNoRanking.DiasNaTela, e o caso aberto
        // é conferido junto: com uma data fora da validade os dois devolvem null e o teste
        // passaria sem encostar na régua — verde provando nada.
        var agora = new DateTime(2026, 7, 3);

        using (var aberto = await MontarAsync(timeExclusivoId: null))
            Assert.NotNull(await MovimentoNoRanking.DoOficialAsync(aberto, agora));

        using var ctx = await MontarAsync(timeExclusivoId: TimeDaCasa);
        Assert.Null(await MovimentoNoRanking.DoOficialAsync(ctx, agora));
    }

    // ── O time que tranca não pode sumir por baixo do torneio ─────────────────────────────

    private static AdminController Admin(DbPadelContext ctx, int usuarioLogadoId)
    {
        var controller = new AdminController(
            ctx,
            Substitute.For<IPushNotificationService>(),
            Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>(),
            Microsoft.Extensions.Options.Options.Create(new RegistroResultadosSettings()));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, usuarioLogadoId.ToString()) }, "Teste")),
            },
        };
        controller.TempData = new TempDataDictionary(controller.HttpContext, Substitute.For<ITempDataProvider>());
        return controller;
    }

    [Fact]
    public async Task Apagar_o_time_que_tranca_um_torneio_e_recusado_com_o_motivo()
    {
        // 🔇 O DEFEITO CALADO QUE ISTO FECHA: com SetNull — que é o que os outros dois
        // vínculos com Time usam — apagar o time destrancaria a inscrição de um torneio
        // fechado sem uma linha de aviso, e a próxima pessoa de fora entraria normalmente.
        // Ninguém reclama de uma porta que ABRIU.
        using var ctx = await MontarAsync(timeExclusivoId: TimeDaCasa);
        ctx.Jogadores.Add(new Jogador { Id = 99, Nome = "Admin", Cpf = "12345678909", IsAdminGeral = true });
        await ctx.SaveChangesAsync();

        var controller = Admin(ctx, usuarioLogadoId: 99);
        await controller.ExcluirTime(timeId: TimeDaCasa);

        Assert.NotNull(await ctx.Times.FindAsync(TimeDaCasa));
        Assert.Contains("Interno do Nata", (string?)controller.TempData["Erro"]);
        // O torneio continua trancado: é isso que o Restrict e a checagem existem pra manter.
        Assert.Equal(TimeDaCasa, (await ctx.Torneios.FindAsync(1))!.TimeExclusivoId);
    }

    [Fact]
    public async Task Fundir_dois_times_repontou_o_torneio_pro_sobrevivente()
    {
        // A fusão de grafias ("ER Padel" e "Er padel") APAGA o absorvido. Sem repontar, ou a
        // gravação estoura na chave estrangeira, ou — com SetNull — o torneio destranca
        // calado. Os jogadores e as duplas já eram repontados; faltava o torneio.
        using var ctx = await MontarAsync(timeExclusivoId: TimeDaCasa);
        ctx.Times.Add(new Time { Id = 8, Nome = "nata padel" });
        // O time 8 tem mais gente, então é ele que sobrevive (critério de FusaoDeTimes).
        ctx.Jogadores.AddRange(
            new Jogador { Id = 31, Nome = "Um", Cpf = "22255588846", TimeId = 8 },
            new Jogador { Id = 32, Nome = "Dois", Cpf = "33366699957", TimeId = 8 },
            new Jogador { Id = 33, Nome = "Três", Cpf = "44477788827", TimeId = 8 });
        await ctx.SaveChangesAsync();

        var sobrevivente = await FusaoDeTimes.FundirAsync(ctx, TimeDaCasa, 8);
        await ctx.SaveChangesAsync();

        Assert.Equal(8, sobrevivente.Id);
        Assert.Equal(8, (await ctx.Torneios.FindAsync(1))!.TimeExclusivoId);
    }
}
