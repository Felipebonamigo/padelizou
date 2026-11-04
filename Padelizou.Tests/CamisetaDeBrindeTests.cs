using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 04/11/2026 — CAMISETA DE BRINDE, COM O TAMANHO DE CADA ATLETA.
//
// 🗣️ Mila (ER Padel), no grupo do torneio: *"eu esqueci que esse vai ter camiseta — como altero
// ali?"*. Não existia nada: nem no torneio, nem na inscrição. Felipe:
//   · *"as babylook também vão do PP ao XGG"*
//   · já inscrito é avisado pra escolher ("sim"); escolher é OPCIONAL
//   · *"na configuração do torneio tem que ter uma opção, se habilita ou não ter brinde de
//     camisas e seleciona quais os tamanhos tem disponível"*
//   · *"o organizador pode decidir quando vai parar de aceitar as camisetas, porque tem uma
//     data limite às vezes"*
public class CamisetaDeBrindeTests
{
    // ── Os tamanhos ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Tradicional_e_babylook_vao_do_PP_ao_XGG_passando_pelo_XG()
    {
        // 🗣️ Organizadora, no áudio: *"camiseta do P ao GG, e XG"*. XG e XGG são tamanhos
        // diferentes no mercado; a lista tem os dois e cada organizador marca os que tem.
        var nomes = CamisetaDeBrinde.Tamanhos.Select(t => t.Nome).ToList();

        Assert.Equal(14, nomes.Count);
        Assert.Equal(new[] { "PP", "P", "M", "G", "GG", "XG", "XGG" }, nomes.Take(7));
        Assert.Equal(new[] { "Babylook PP", "Babylook P", "Babylook M", "Babylook G", "Babylook GG", "Babylook XG", "Babylook XGG" },
            nomes.Skip(7));
    }

    [Fact]
    public void Tamanhos_marcados_ficam_na_ordem_da_tabela_sem_repetir_e_sem_lixo()
    {
        Assert.Equal("P,G,BLM", CamisetaDeBrinde.Normalizar(new[] { "G", "lixo", "BLM", "P", "G" }));
        Assert.Null(CamisetaDeBrinde.Normalizar(Array.Empty<string>()));
        Assert.Null(CamisetaDeBrinde.Normalizar(null));
    }

    // ── O prazo ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sem_data_limite_aceita_escolha_enquanto_o_brinde_esta_ligado()
    {
        var t = new Torneio { OfereceCamiseta = true, TamanhosDeCamiseta = "M" };

        Assert.True(CamisetaDeBrinde.AceitaEscolha(t, new DateTime(2026, 11, 4, 15, 0, 0)));
        t.OfereceCamiseta = false;
        Assert.False(CamisetaDeBrinde.AceitaEscolha(t, new DateTime(2026, 11, 4, 15, 0, 0)));
    }

    [Fact]
    public void A_data_limite_vale_o_dia_inteiro_e_fecha_no_seguinte()
    {
        // O organizador põe "até 10/11" pensando no dia todo, não na meia-noite do começo dele.
        var t = new Torneio { OfereceCamiseta = true, TamanhosDeCamiseta = "M", CamisetaEscolhaAte = new DateTime(2026, 11, 10) };

        Assert.True(CamisetaDeBrinde.AceitaEscolha(t, new DateTime(2026, 11, 10, 23, 30, 0)));
        Assert.False(CamisetaDeBrinde.AceitaEscolha(t, new DateTime(2026, 11, 11, 0, 5, 0)));
    }

    // ── Montagem ──────────────────────────────────────────────────────────────────────────

    private sealed record Cenario(DbPadelContext Ctx, Torneio Torneio, Categoria Categoria, Jogador Organizador,
        Jogador Atleta, Jogador Parceiro);

    private static Cenario Montar(bool ofereceCamiseta = true, string tamanhos = "P,M,G,BLP,BLM",
        DateTime? escolhaAte = null)
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");
        torneio.OfereceCamiseta = ofereceCamiseta;
        torneio.TamanhosDeCamiseta = ofereceCamiseta ? tamanhos : null;
        torneio.CamisetaEscolhaAte = escolhaAte;
        ctx.SaveChanges();
        var dupla = ctx.Duplas.Include(d => d.Jogador1).Include(d => d.Jogador2).Single();
        return new Cenario(ctx, torneio, categoria, organizador, dupla.Jogador1, dupla.Jogador2!);
    }

    private static async Task<CamisetaDoAtleta?> CamisetaDe(Cenario c, int jogadorId)
    {
        c.Ctx.ChangeTracker.Clear();
        return await c.Ctx.CamisetasDosAtletas.SingleOrDefaultAsync(x => x.TorneioId == c.Torneio.Id && x.JogadorId == jogadorId);
    }

    // ── Configurar (organizador) ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Organizador_liga_o_brinde_com_os_tamanhos_e_a_data_limite()
    {
        var c = Montar(ofereceCamiseta: false);
        var ate = DateTime.Today.AddDays(5);

        var r = await TestInfra.NovoTorneiosController(c.Ctx, c.Organizador.Id)
            .ConfigurarCamisetas(c.Torneio.Id, true, new[] { "M", "PP", "BLGG" }, ate);

        Assert.IsType<RedirectToActionResult>(r);
        c.Ctx.ChangeTracker.Clear();
        var t = await c.Ctx.Torneios.SingleAsync(x => x.Id == c.Torneio.Id);
        Assert.True(t.OfereceCamiseta);
        Assert.Equal("PP,M,BLGG", t.TamanhosDeCamiseta);
        Assert.Equal(ate, t.CamisetaEscolhaAte);
    }

    [Fact]
    public async Task Ligar_sem_nenhum_tamanho_e_recusado()
    {
        var c = Montar(ofereceCamiseta: false);
        var controller = TestInfra.NovoTorneiosController(c.Ctx, c.Organizador.Id);

        await controller.ConfigurarCamisetas(c.Torneio.Id, true, Array.Empty<string>(), null);

        Assert.NotNull(controller.TempData["Erro"]);
        c.Ctx.ChangeTracker.Clear();
        Assert.False((await c.Ctx.Torneios.SingleAsync(x => x.Id == c.Torneio.Id)).OfereceCamiseta);
    }

    [Fact]
    public async Task Quem_nao_organiza_nao_configura()
    {
        var c = Montar(ofereceCamiseta: false);

        var r = await TestInfra.NovoTorneiosController(c.Ctx, c.Atleta.Id)
            .ConfigurarCamisetas(c.Torneio.Id, true, new[] { "M" }, null);

        Assert.IsType<ForbidResult>(r);
        c.Ctx.ChangeTracker.Clear();
        Assert.False((await c.Ctx.Torneios.SingleAsync(x => x.Id == c.Torneio.Id)).OfereceCamiseta);
    }

    [Fact]
    public async Task Ligar_o_brinde_avisa_cada_inscrito_uma_vez_pelas_duas_portas()
    {
        // 🗣️ Felipe: quem já se inscreveu é avisado pra escolher. Pelas DUAS portas de inscrição
        // (dupla e individual), e uma vez por pessoa — o atleta em duas categorias é uma pessoa.
        var c = Montar(ofereceCamiseta: false);
        var outra = new Categoria { Nome = "Mista", Codigo = "MX", TorneioId = c.Torneio.Id };
        c.Ctx.Categorias.Add(outra);
        var individual = TestInfra.NovoJogador(50);
        c.Ctx.Jogadores.Add(individual);
        c.Ctx.SaveChanges();
        c.Ctx.Duplas.Add(new Dupla { CategoriaId = outra.Id, Jogador1Id = c.Atleta.Id });
        c.Ctx.InscricoesAmericanas.Add(new InscricaoAmericana { CategoriaId = outra.Id, JogadorId = individual.Id });
        c.Ctx.SaveChanges();
        var push = Substitute.For<IPushNotificationService>();

        await TestInfra.NovoTorneiosController(c.Ctx, c.Organizador.Id, push: push)
            .ConfigurarCamisetas(c.Torneio.Id, true, new[] { "M" }, null);

        foreach (var quem in new[] { c.Atleta.Id, c.Parceiro.Id, individual.Id })
            await push.Received(1).EnviarParaJogadorAsync(quem, Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), AlcanceDoAviso.AppSemEmail);
        await push.DidNotReceive().EnviarParaJogadorAsync(c.Organizador.Id, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Salvar_de_novo_com_o_brinde_ja_ligado_nao_avisa_outra_vez()
    {
        // Trocar a data limite ou um tamanho não é notícia nova pra 80 pessoas.
        var c = Montar(ofereceCamiseta: true);
        var push = Substitute.For<IPushNotificationService>();

        await TestInfra.NovoTorneiosController(c.Ctx, c.Organizador.Id, push: push)
            .ConfigurarCamisetas(c.Torneio.Id, true, new[] { "M", "G" }, DateTime.Today.AddDays(3));

        await push.DidNotReceiveWithAnyArgs().EnviarParaJogadorAsync(default, default!, default!, default, default);
    }

    [Fact]
    public async Task Desligar_mantem_as_escolhas_ja_feitas()
    {
        // Desligou por engano e religou: ninguém precisa escolher de novo.
        var c = Montar();
        c.Ctx.CamisetasDosAtletas.Add(new CamisetaDoAtleta { TorneioId = c.Torneio.Id, JogadorId = c.Atleta.Id, Tamanho = "M" });
        c.Ctx.SaveChanges();

        await TestInfra.NovoTorneiosController(c.Ctx, c.Organizador.Id)
            .ConfigurarCamisetas(c.Torneio.Id, false, null, null);

        Assert.Equal("M", (await CamisetaDe(c, c.Atleta.Id))?.Tamanho);
        Assert.False((await c.Ctx.Torneios.SingleAsync(x => x.Id == c.Torneio.Id)).OfereceCamiseta);
    }

    // ── Escolher (atleta) ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Inscrito_escolhe_o_tamanho()
    {
        var c = Montar();

        var r = await TestInfra.NovoTorneiosController(c.Ctx, c.Parceiro.Id).EscolherCamiseta(c.Torneio.Id, "BLM");

        Assert.IsType<RedirectToActionResult>(r);
        Assert.Equal("BLM", (await CamisetaDe(c, c.Parceiro.Id))?.Tamanho);
    }

    [Fact]
    public async Task Trocar_o_tamanho_atualiza_a_mesma_escolha()
    {
        var c = Montar();
        await TestInfra.NovoTorneiosController(c.Ctx, c.Atleta.Id).EscolherCamiseta(c.Torneio.Id, "P");

        await TestInfra.NovoTorneiosController(c.Ctx, c.Atleta.Id).EscolherCamiseta(c.Torneio.Id, "G");

        Assert.Equal("G", (await CamisetaDe(c, c.Atleta.Id))?.Tamanho);
        Assert.Equal(1, await c.Ctx.CamisetasDosAtletas.CountAsync());
    }

    [Fact]
    public async Task Escolher_nenhum_tira_a_camiseta()
    {
        // 🗣️ Opcional: quem não quer camiseta não fica preso a uma escolha que fez.
        var c = Montar();
        await TestInfra.NovoTorneiosController(c.Ctx, c.Atleta.Id).EscolherCamiseta(c.Torneio.Id, "P");

        await TestInfra.NovoTorneiosController(c.Ctx, c.Atleta.Id).EscolherCamiseta(c.Torneio.Id, "");

        Assert.Null(await CamisetaDe(c, c.Atleta.Id));
    }

    [Fact]
    public async Task Quem_nao_esta_inscrito_nao_escolhe()
    {
        var c = Montar();
        var curioso = TestInfra.NovoJogador(70);
        c.Ctx.Jogadores.Add(curioso);
        c.Ctx.SaveChanges();

        var r = await TestInfra.NovoTorneiosController(c.Ctx, curioso.Id).EscolherCamiseta(c.Torneio.Id, "M");

        Assert.IsType<ForbidResult>(r);
        Assert.Null(await CamisetaDe(c, curioso.Id));
    }

    [Fact]
    public async Task Tamanho_que_o_organizador_nao_oferece_e_recusado()
    {
        var c = Montar(tamanhos: "P,M");
        var controller = TestInfra.NovoTorneiosController(c.Ctx, c.Atleta.Id);

        await controller.EscolherCamiseta(c.Torneio.Id, "XGG");

        Assert.NotNull(controller.TempData["Erro"]);
        Assert.Null(await CamisetaDe(c, c.Atleta.Id));
    }

    [Fact]
    public async Task Depois_da_data_limite_nao_escolhe_nem_troca()
    {
        var c = Montar(escolhaAte: DateTime.Today.AddDays(-1));
        c.Ctx.CamisetasDosAtletas.Add(new CamisetaDoAtleta { TorneioId = c.Torneio.Id, JogadorId = c.Atleta.Id, Tamanho = "P" });
        c.Ctx.SaveChanges();
        var controller = TestInfra.NovoTorneiosController(c.Ctx, c.Atleta.Id);

        await controller.EscolherCamiseta(c.Torneio.Id, "M");

        Assert.NotNull(controller.TempData["Erro"]);
        Assert.Equal("P", (await CamisetaDe(c, c.Atleta.Id))?.Tamanho);
    }

    [Fact]
    public async Task Torneio_sem_brinde_nao_aceita_escolha()
    {
        var c = Montar(ofereceCamiseta: false);
        var controller = TestInfra.NovoTorneiosController(c.Ctx, c.Atleta.Id);

        await controller.EscolherCamiseta(c.Torneio.Id, "M");

        Assert.NotNull(controller.TempData["Erro"]);
        Assert.Null(await CamisetaDe(c, c.Atleta.Id));
    }

    // ── O que o organizador vê ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resumo_conta_por_tamanho_lista_quem_pediu_e_quantos_faltam()
    {
        var c = Montar();
        c.Ctx.CamisetasDosAtletas.AddRange(
            new CamisetaDoAtleta { TorneioId = c.Torneio.Id, JogadorId = c.Atleta.Id, Tamanho = "M" },
            new CamisetaDoAtleta { TorneioId = c.Torneio.Id, JogadorId = c.Parceiro.Id, Tamanho = "BLP" });
        var semEscolha = TestInfra.NovoJogador(80);
        c.Ctx.Jogadores.Add(semEscolha);
        c.Ctx.SaveChanges();
        c.Ctx.Duplas.Add(new Dupla { CategoriaId = c.Categoria.Id, Jogador1Id = semEscolha.Id });
        c.Ctx.SaveChanges();

        var resumo = await CamisetaDeBrinde.ResumoAsync(c.Ctx, c.Torneio.Id);

        Assert.Equal(new[] { ("M", 1), ("Babylook P", 1) }, resumo.PorTamanho.Select(p => (p.Tamanho, p.Quantas)));
        Assert.Contains(resumo.Pedidos, p => p.Nome == c.Atleta.Nome && p.Tamanho == "M");
        Assert.Equal(1, resumo.SemEscolha);
    }

    // ── O resto do sistema ────────────────────────────────────────────────────────────────

    [Fact]
    public void A_tela_do_torneio_tem_as_duas_portas()
    {
        var details = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Padelizou", "Views", "Torneios", "Details.cshtml"));

        Assert.Contains("asp-action=\"ConfigurarCamisetas\"", details);
        Assert.Contains("asp-action=\"EscolherCamiseta\"", details);
    }

    [Fact]
    public void Proxima_edicao_herda_o_brinde_e_os_tamanhos_mas_nao_a_data()
    {
        // A 3ª etapa do circuito costuma ter a mesma camiseta; a data limite é da etapa que passou.
        Assert.Contains(nameof(Torneio.OfereceCamiseta), DuplicacaoDeTorneio.Copiadas);
        Assert.Contains(nameof(Torneio.TamanhosDeCamiseta), DuplicacaoDeTorneio.Copiadas);
        Assert.DoesNotContain(nameof(Torneio.CamisetaEscolhaAte), DuplicacaoDeTorneio.Copiadas);
    }
}
