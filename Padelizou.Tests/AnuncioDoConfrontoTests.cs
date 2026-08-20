using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A CHAMADA DA PRÓXIMA FASE (20/08/2026): quando o robô monta uma rodada nova do mata-mata,
// os jogadores dela ficam sabendo contra quem (e quando) jogam — os dois lados de uma vez,
// quem acabou de vencer e quem já esperava adversário.
//
// O que estes testes guardam:
//   * o anúncio sai no NASCIMENTO da fase (o robô devolve o que criou), então não repete —
//     os robôs nunca criam a mesma fase duas vezes;
//   * dupla-TIME não recebe (o Jogador1 dela é o organizador que cadastrou o time);
//   * o alcance é AppSemEmail: é bilhete de chave, e rajada por e-mail já queimou cota.
public class AnuncioDoConfrontoTests
{
    // ── O texto ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void O_corpo_diz_fase_torneio_adversario_hora_e_quadra()
    {
        var corpo = TextoDoConfronto.Corpo("Semifinal", "Copa de Verão", "Carlos e Dani",
            new DateTime(2026, 8, 24, 14, 30, 0), "Quadra 2");

        Assert.Equal("Semifinal · Copa de Verão: vocês contra Carlos e Dani. 24/08 às 14:30, Quadra 2.", corpo);
    }

    // Torneio por ordem de liberação não tem grade: sem hora, a frase termina inteira — sem
    // "às 00:00" nem "quadra a definir" inventados.
    [Fact]
    public void Sem_horario_a_frase_termina_no_adversario()
    {
        var corpo = TextoDoConfronto.Corpo("Final", "Copa", "Ana e Bia", null, null);

        Assert.Equal("Final · Copa: vocês contra Ana e Bia.", corpo);
    }

    [Fact]
    public void Com_hora_mas_sem_quadra_a_quadra_nao_vira_virgula_solta()
    {
        var corpo = TextoDoConfronto.Corpo("Final", "Copa", "Ana e Bia",
            new DateTime(2026, 8, 24, 10, 0, 0), null);

        Assert.Equal("Final · Copa: vocês contra Ana e Bia. 24/08 às 10:00.", corpo);
    }

    // ── O anúncio ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Os_dois_lados_do_confronto_novo_sao_avisados()
    {
        var (ctx, push, partida) = MontarConfronto();

        await new AnuncioDoConfronto(ctx, push).AnunciarAsync(new[] { partida }, "/Torneios/Details/1");

        // 4 jogadores, cada um ouvindo o NOME DO OUTRO LADO — avisar "vocês contra vocês"
        // seria pior que não avisar.
        foreach (var id in new[] { 1, 2 })
            await push.Received(1).EnviarParaJogadorAsync(id, TextoDoConfronto.Titulo,
                Arg.Is<string>(c => c.Contains("Caco") && c.Contains("Dado")),
                "/Torneios/Details/1", AlcanceDoAviso.AppSemEmail);

        foreach (var id in new[] { 3, 4 })
            await push.Received(1).EnviarParaJogadorAsync(id, TextoDoConfronto.Titulo,
                Arg.Is<string>(c => c.Contains("Ana") && c.Contains("Bia")),
                "/Torneios/Details/1", AlcanceDoAviso.AppSemEmail);
    }

    [Fact]
    public async Task Lista_vazia_nao_manda_nada()
    {
        var ctx = TestInfra.NovoContexto();
        var push = Substitute.For<IPushNotificationService>();

        await new AnuncioDoConfronto(ctx, push).AnunciarAsync(new List<Partida>(), null);

        await push.DidNotReceive().EnviarParaJogadorAsync(Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    // O TIME não tem quem avisar (o Jogador1 da linha é quem cadastrou), mas o lado de
    // PESSOAS enfrenta o time — e o adversário aparece pelo nome do time.
    [Fact]
    public async Task Time_nao_recebe_mas_aparece_como_adversario()
    {
        var ctx = TestInfra.NovoContexto();
        var push = Substitute.For<IPushNotificationService>();

        ctx.Torneios.Add(new Torneio { Id = 1, Nome = "Interclubes", Codigo = "INT" });
        ctx.Categorias.Add(new Categoria { Id = 10, TorneioId = 1, Nome = "Times", Codigo = "TMS" });
        ctx.Jogadores.AddRange(
            new Jogador { Id = 1, Nome = "Ana", Cpf = "52998224725" },
            new Jogador { Id = 2, Nome = "Bia", Cpf = "11144477735" },
            new Jogador { Id = 9, Nome = "Organizador", Cpf = "99900000099" });
        ctx.Duplas.AddRange(
            new Dupla { Id = 100, CategoriaId = 10, Jogador1Id = 1, Jogador2Id = 2 },
            new Dupla { Id = 200, CategoriaId = 10, Jogador1Id = 9, NomeTime = "Padel Bros" });
        var partida = new Partida
        {
            Id = 500, Codigo = "SF1", TorneioId = 1, CategoriaId = 10,
            Dupla1Id = 100, Dupla2Id = 200, Fase = "Semifinal", Status = "Agendada",
        };
        ctx.Partidas.Add(partida);
        ctx.SaveChanges();

        await new AnuncioDoConfronto(ctx, push).AnunciarAsync(new[] { partida }, null);

        await push.Received(1).EnviarParaJogadorAsync(1, TextoDoConfronto.Titulo,
            Arg.Is<string>(c => c.Contains("Padel Bros")), null, AlcanceDoAviso.AppSemEmail);
        // Quem cadastrou o time não joga esta partida — avisá-lo seria mentira.
        await push.DidNotReceive().EnviarParaJogadorAsync(9, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    // ── O caminho inteiro: a última semifinal fecha e a FINAL é anunciada ────────────────

    [Fact]
    public async Task Fechar_a_ultima_semifinal_anuncia_a_final_pros_quatro_finalistas()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, 4);
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).OrderBy(d => d.Id).ToList();

        // Semifinal 1 já fechada; a 2 é a que fecha agora, pelo caminho real do encerramento.
        ctx.Partidas.Add(new Partida
        {
            Codigo = "SF1", TorneioId = torneio.Id, CategoriaId = categoria.Id,
            Dupla1Id = duplas[0].Id, Dupla2Id = duplas[1].Id, Fase = "Semifinal",
            Status = "Finalizada", VencedorId = duplas[0].Id, GamesDupla1 = 6, GamesDupla2 = 2,
        });
        var semi2 = new Partida
        {
            Codigo = "SF2", TorneioId = torneio.Id, CategoriaId = categoria.Id,
            Dupla1Id = duplas[2].Id, Dupla2Id = duplas[3].Id, Fase = "Semifinal",
            Status = "Finalizada", VencedorId = duplas[3].Id, GamesDupla1 = 3, GamesDupla2 = 6,
        };
        ctx.Partidas.Add(semi2);
        ctx.SaveChanges();

        var push = Substitute.For<IPushNotificationService>();
        await TestInfra.NovoEncerramento(ctx, push).AplicarAsync(semi2, acabouDeTerminar: true,
            new EncerramentoDaPartida.LinksDoAviso("/Torneios/Details/1", "/Torneios/Jogos/1"));

        // A final nasceu (robô) e os 4 finalistas — vencedores das DUAS semis, inclusive da
        // que fechou antes — ficaram sabendo do confronto.
        Assert.True(ctx.Partidas.Any(p => p.CategoriaId == categoria.Id && p.Fase == "Final"),
            "O robô não montou a final — o cenário está torto, não o anúncio.");

        var finalistas = new[]
        {
            duplas[0].Jogador1Id, duplas[0].Jogador2Id!.Value,
            duplas[3].Jogador1Id, duplas[3].Jogador2Id!.Value,
        };
        foreach (var id in finalistas)
            await push.Received(1).EnviarParaJogadorAsync(id, TextoDoConfronto.Titulo,
                Arg.Is<string>(c => c.StartsWith("Final")), Arg.Any<string?>(),
                AlcanceDoAviso.AppSemEmail);
    }

    private static (DbPadelContext ctx, IPushNotificationService push, Partida partida) MontarConfronto()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Torneios.Add(new Torneio { Id = 1, Nome = "Copa", Codigo = "CP1" });
        ctx.Categorias.Add(new Categoria { Id = 10, TorneioId = 1, Nome = "Open", Codigo = "OP" });
        ctx.Jogadores.AddRange(
            new Jogador { Id = 1, Nome = "Ana", Cpf = "52998224725" },
            new Jogador { Id = 2, Nome = "Bia", Cpf = "11144477735" },
            new Jogador { Id = 3, Nome = "Caco", Cpf = "99900000001" },
            new Jogador { Id = 4, Nome = "Dado", Cpf = "99900000002" });
        ctx.Duplas.AddRange(
            new Dupla { Id = 100, CategoriaId = 10, Jogador1Id = 1, Jogador2Id = 2 },
            new Dupla { Id = 200, CategoriaId = 10, Jogador1Id = 3, Jogador2Id = 4 });
        var partida = new Partida
        {
            Id = 500, Codigo = "FN1", TorneioId = 1, CategoriaId = 10,
            Dupla1Id = 100, Dupla2Id = 200, Fase = "Final", Status = "Agendada",
        };
        ctx.Partidas.Add(partida);
        ctx.SaveChanges();

        return (ctx, Substitute.For<IPushNotificationService>(), partida);
    }
}
