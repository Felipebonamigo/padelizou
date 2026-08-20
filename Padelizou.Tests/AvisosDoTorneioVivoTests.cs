using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Os avisos que mantêm o torneio VIVO na tela de quem participa (20/08/2026): o título
// decidido pros seguidores, as últimas vagas da categoria, a volta da série e o palpite
// conferido. Todos SÓ push + caixa de avisos (AppSemEmail) — pedido do Felipe: nada disso
// gasta e-mail nem WhatsApp.
public class AvisosDoTorneioVivoTests
{
    // ── TÍTULO DECIDIDO (pros seguidores do torneio) ─────────────────────────────────────

    [Fact]
    public async Task O_titulo_decidido_avisa_os_seguidores_menos_os_campeoes()
    {
        var (ctx, push) = MontarTorneioComCampea();
        ctx.SeguidoresTorneio.AddRange(
            new SeguidorTorneio { TorneioId = 1, JogadorId = 100 },  // torcedor
            new SeguidorTorneio { TorneioId = 1, JogadorId = 1 });   // a própria campeã
        ctx.SaveChanges();

        await new AnuncioDosCampeoes(ctx, push).AnunciarSeCoroouAsync(10, 1, "/Torneios/Details/1");

        await push.Received(1).EnviarParaJogadorAsync(100, TextoDosCampeoes.Titulo,
            Arg.Is<string>(c => c.Contains("Ana") && c.Contains("Bia") && c.Contains("avalie")),
            "/Torneios/Details/1", AlcanceDoAviso.AppSemEmail);

        // Quem levantou a taça acabou de viver isso — o push dela é o "🏆 Campeões!" do
        // resultado, não este.
        await push.DidNotReceive().EnviarParaJogadorAsync(1, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    // Torneio cancelado não anuncia campeão em tela nenhuma — este aviso não pode ser a
    // exceção (mesma régua do CampeoesDoTorneio.PodeAnunciar).
    [Fact]
    public async Task Torneio_cancelado_nao_anuncia_titulo()
    {
        var (ctx, push) = MontarTorneioComCampea(status: "Cancelado");
        ctx.SeguidoresTorneio.Add(new SeguidorTorneio { TorneioId = 1, JogadorId = 100 });
        ctx.SaveChanges();

        await new AnuncioDosCampeoes(ctx, push).AnunciarSeCoroouAsync(10, 1, null);

        await push.DidNotReceive().EnviarParaJogadorAsync(Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Categoria_sem_campeao_fica_em_silencio()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Torneios.Add(new Torneio { Id = 1, Nome = "Copa", Codigo = "CP1", Status = "Finalizado" });
        ctx.Categorias.Add(new Categoria { Id = 10, TorneioId = 1, Nome = "Open", Codigo = "OP" });
        ctx.SeguidoresTorneio.Add(new SeguidorTorneio { TorneioId = 1, JogadorId = 100 });
        ctx.SaveChanges();
        var push = Substitute.For<IPushNotificationService>();

        await new AnuncioDosCampeoes(ctx, push).AnunciarSeCoroouAsync(10, 1, null);

        await push.DidNotReceive().EnviarParaJogadorAsync(Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    // O campeão SOLO do Americano: uma pessoa, verbo no singular.
    [Fact]
    public void O_texto_fala_no_singular_pro_campeao_solo_do_americano()
    {
        var corpo = TextoDosCampeoes.Corpo(new[] { "Carlos" }, "Open", "Copa");

        Assert.StartsWith("Carlos levou a Open do Copa.", corpo);
    }

    // O caminho inteiro: a FINAL fecha e o seguidor do torneio fica sabendo do título.
    [Fact]
    public async Task Fechar_a_final_conta_o_titulo_pra_quem_segue_o_torneio()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, 2);
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();

        var torcedor = new Jogador { Nome = "Torcedor", Cpf = "99900000098" };
        ctx.Jogadores.Add(torcedor);
        ctx.SaveChanges();
        ctx.SeguidoresTorneio.Add(new SeguidorTorneio { TorneioId = torneio.Id, JogadorId = torcedor.Id });

        var final = new Partida
        {
            Codigo = "FIN1", TorneioId = torneio.Id, CategoriaId = categoria.Id,
            Dupla1Id = duplas[0].Id, Dupla2Id = duplas[1].Id, Fase = "Final",
            Status = "Finalizada", VencedorId = duplas[0].Id, GamesDupla1 = 6, GamesDupla2 = 3,
        };
        ctx.Partidas.Add(final);
        ctx.SaveChanges();

        var push = Substitute.For<IPushNotificationService>();
        await TestInfra.NovoEncerramento(ctx, push).AplicarAsync(final, acabouDeTerminar: true,
            new EncerramentoDaPartida.LinksDoAviso("/Torneios/Details/1", "/Torneios/Jogos/1"));

        await push.Received(1).EnviarParaJogadorAsync(torcedor.Id, TextoDosCampeoes.Titulo,
            Arg.Any<string>(), Arg.Any<string?>(), AlcanceDoAviso.AppSemEmail);
    }

    // ── ÚLTIMAS VAGAS ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cruzar_as_tres_vagas_avisa_quem_segue_e_ainda_nao_esta_na_categoria()
    {
        var (ctx, push, aviso) = MontarInscricoes(limite: 5, ocupadas: 2);
        ctx.SeguidoresTorneio.AddRange(
            new SeguidorTorneio { TorneioId = 7, JogadorId = 100 },  // de fora da categoria
            new SeguidorTorneio { TorneioId = 7, JogadorId = 11 });  // já inscrito nela
        ctx.SaveChanges();

        await aviso.NotificarAsync(7, 70, new[] { "Ana" }, new[] { 11 }, "/Torneios/Details/7");

        await push.Received(1).EnviarParaJogadorAsync(100, "As vagas estão acabando",
            Arg.Is<string>(c => c.Contains("3 vagas")), "/Torneios/Details/7", AlcanceDoAviso.AppSemEmail);
        // Quem já ocupa vaga na categoria não precisa de urgência sobre as que restam.
        await push.DidNotReceive().EnviarParaJogadorAsync(11, "As vagas estão acabando",
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Fora_dos_limiares_nao_ha_aviso_de_vagas()
    {
        var (ctx, push, aviso) = MontarInscricoes(limite: 5, ocupadas: 3); // restam 2
        ctx.SeguidoresTorneio.Add(new SeguidorTorneio { TorneioId = 7, JogadorId = 100 });
        ctx.SaveChanges();

        await aviso.NotificarAsync(7, 70, new[] { "Ana" }, new[] { 11 }, null);

        await push.DidNotReceive().EnviarParaJogadorAsync(Arg.Any<int>(), "As vagas estão acabando",
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
        await push.DidNotReceive().EnviarParaJogadorAsync(Arg.Any<int>(), "Última vaga! ⏳",
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task A_ultima_vaga_tem_aviso_proprio()
    {
        var (ctx, push, aviso) = MontarInscricoes(limite: 5, ocupadas: 4);
        ctx.SeguidoresTorneio.Add(new SeguidorTorneio { TorneioId = 7, JogadorId = 100 });
        ctx.SaveChanges();

        await aviso.NotificarAsync(7, 70, new[] { "Ana" }, new[] { 11 }, null);

        await push.Received(1).EnviarParaJogadorAsync(100, "Última vaga! ⏳",
            Arg.Is<string>(c => c.Contains("1 vaga")), null, AlcanceDoAviso.AppSemEmail);
    }

    // Categoria sem limite não tem "últimas vagas" — não existe conta pra fazer.
    [Fact]
    public async Task Categoria_sem_limite_nao_fala_de_vagas()
    {
        var (ctx, push, aviso) = MontarInscricoes(limite: null, ocupadas: 2);
        ctx.SeguidoresTorneio.Add(new SeguidorTorneio { TorneioId = 7, JogadorId = 100 });
        ctx.SaveChanges();

        await aviso.NotificarAsync(7, 70, new[] { "Ana" }, new[] { 11 }, null);

        await push.DidNotReceive().EnviarParaJogadorAsync(Arg.Any<int>(), "As vagas estão acabando",
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    // Lista de espera não ocupa vaga — contá-la diria "acabou" com a chave ainda aberta.
    [Fact]
    public async Task Lista_de_espera_nao_conta_como_vaga_ocupada()
    {
        var (ctx, push, aviso) = MontarInscricoes(limite: 5, ocupadas: 2);
        ctx.Duplas.Add(new Dupla { Id = 900, CategoriaId = 70, Jogador1Id = 90, EmListaDeEspera = true });
        ctx.SeguidoresTorneio.Add(new SeguidorTorneio { TorneioId = 7, JogadorId = 100 });
        ctx.SaveChanges();

        await aviso.NotificarAsync(7, 70, new[] { "Ana" }, new[] { 11 }, null);

        // Continuam 3 de verdade (a linha de espera não desconta) — o aviso ainda é o de 3.
        await push.Received(1).EnviarParaJogadorAsync(100, "As vagas estão acabando",
            Arg.Any<string>(), Arg.Any<string?>(), AlcanceDoAviso.AppSemEmail);
    }

    // ── A VOLTA DA SÉRIE ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Quem_jogou_a_edicao_anterior_recebe_o_aviso_pessoal()
    {
        var ctx = TestInfra.NovoContexto();
        var push = Substitute.For<IPushNotificationService>();

        // A edição do ano passado, com um veterano inscrito.
        var anterior = new Torneio { Id = 1, Nome = "Copa do Clube", Codigo = "ED1", Status = "Finalizado" };
        ctx.Torneios.Add(anterior);
        ctx.Categorias.Add(new Categoria { Id = 10, TorneioId = 1, Nome = "Open", Codigo = "OP" });
        ctx.Jogadores.AddRange(
            new Jogador { Id = 1, Nome = "Veterano", Cpf = "52998224725" },
            new Jogador { Id = 2, Nome = "Parceiro", Cpf = "11144477735" },
            new Jogador { Id = 3, Nome = "Novato", Cpf = "99900000001" });
        ctx.Duplas.Add(new Dupla { Id = 100, CategoriaId = 10, Jogador1Id = 1, Jogador2Id = 2 });

        // A edição nova, aprovada e pronta pra anunciar.
        var novaEdicao = new Torneio
        {
            Id = 2, Nome = "Copa do Clube 2", Codigo = "ED2", Status = "Inscrições Abertas",
            TorneioOrigemId = 1, AprovadoEm = DateTime.Now,
        };
        ctx.Torneios.Add(novaEdicao);
        ctx.SaveChanges();

        var resultado = await AvisoDeTorneioNovo.EnviarSePuderAsync(ctx, push, novaEdicao, "/Torneios/Details/2");

        Assert.True(resultado.Enviou);
        Assert.Equal(3, resultado.Quantos);

        // Veteranos (os DOIS da dupla antiga) ouvem a volta; o novato ouve o anúncio de sempre.
        foreach (var id in new[] { 1, 2 })
            await push.Received(1).EnviarParaJogadorAsync(id, TextoDaVoltaDaSerie.Titulo,
                Arg.Is<string>(c => c.Contains("Copa do Clube 2")), "/Torneios/Details/2",
                AlcanceDoAviso.AppSemEmail);

        await push.Received(1).EnviarParaJogadorAsync(3, "Novo torneio aberto",
            Arg.Any<string>(), Arg.Any<string?>(), AlcanceDoAviso.AppSemEmail);
        // E ninguém recebe DOIS anúncios do mesmo torneio.
        await push.DidNotReceive().EnviarParaJogadorAsync(1, "Novo torneio aberto",
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Torneio_sem_serie_nao_inventa_veterano()
    {
        var ctx = TestInfra.NovoContexto();
        var push = Substitute.For<IPushNotificationService>();
        ctx.Jogadores.Add(new Jogador { Id = 1, Nome = "Alguém", Cpf = "52998224725" });
        var torneio = new Torneio
        {
            Id = 2, Nome = "Copa Nova", Codigo = "CN1", Status = "Inscrições Abertas",
            AprovadoEm = DateTime.Now,
        };
        ctx.Torneios.Add(torneio);
        ctx.SaveChanges();

        await AvisoDeTorneioNovo.EnviarSePuderAsync(ctx, push, torneio, null);

        await push.Received(1).EnviarParaJogadorAsync(1, "Novo torneio aberto",
            Arg.Any<string>(), Arg.Any<string?>(), AlcanceDoAviso.AppSemEmail);
        await push.DidNotReceive().EnviarParaJogadorAsync(Arg.Any<int>(), TextoDaVoltaDaSerie.Titulo,
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    // ── Cenários ─────────────────────────────────────────────────────────────────────────

    private static (DbPadelContext ctx, IPushNotificationService push) MontarTorneioComCampea(
        string status = "Finalizado")
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Torneios.Add(new Torneio { Id = 1, Nome = "Copa", Codigo = "CP1", Status = status });
        ctx.Categorias.Add(new Categoria { Id = 10, TorneioId = 1, Nome = "Open", Codigo = "OP" });
        ctx.Jogadores.AddRange(
            new Jogador { Id = 1, Nome = "Ana", Cpf = "52998224725" },
            new Jogador { Id = 2, Nome = "Bia", Cpf = "11144477735" });
        ctx.Duplas.Add(new Dupla
        {
            Id = 100, CategoriaId = 10, Jogador1Id = 1, Jogador2Id = 2,
            UltimaFase = CampeoesDoTorneio.FaseDeCampeao,
        });
        ctx.SaveChanges();

        return (ctx, Substitute.For<IPushNotificationService>());
    }

    // Torneio 7, categoria 70 com `limite` e `ocupadas` duplas fora da lista de espera —
    // o jogador 11 é um dos ocupantes (é ele que "acabou de se inscrever" nos testes).
    private static (DbPadelContext ctx, IPushNotificationService push, AvisoDeInscricaoNoTorneio aviso)
        MontarInscricoes(int? limite, int ocupadas)
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Torneios.Add(new Torneio { Id = 7, Nome = "Copa da Grade", Codigo = "GRD" });
        ctx.Categorias.Add(new Categoria
        {
            Id = 70, TorneioId = 7, Nome = "Open", Codigo = "OP", LimiteDuplas = limite,
        });
        for (int i = 0; i < ocupadas; i++)
            ctx.Duplas.Add(new Dupla { Id = 800 + i, CategoriaId = 70, Jogador1Id = 11 + i });
        ctx.SaveChanges();

        var push = Substitute.For<IPushNotificationService>();
        return (ctx, push, new AvisoDeInscricaoNoTorneio(ctx, push));
    }
}
