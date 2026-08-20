using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O RETORNO DO PALPITRÔMETRO e os MARCOS PESSOAIS (20/08/2026) — os dois avisos de festa do
// fim de jogo. Regras que estes testes guardam:
//   * quem ERROU o palpite não recebe nada ("você errou" ensina a não palpitar mais);
//   * quem estava EM QUADRA não recebe (o ranking não paga ponto no próprio jogo — o aviso
//     não pode prometer um ponto que a tabela não mostra);
//   * a frase de quem CRAVOU não pode sair pra quem só acertou o vencedor;
//   * marco é festa RARA: só no cruzamento exato, e vitória ganha de jogo quando os dois
//     caem juntos.
public class PalpiteConferidoEMarcosTests
{
    // ── O PALPITE CONFERIDO ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Quem_cravou_o_placar_ouve_isso_com_todas_as_letras()
    {
        var (ctx, push, partida) = MontarJogoFechado(games1: 6, games2: 2);
        ctx.PalpitesPartida.Add(new PalpitePartida
        {
            PartidaId = partida.Id, JogadorId = 100, DuplaEscolhidaId = 101,
            GamesDupla1 = 6, GamesDupla2 = 2,
        });
        ctx.SaveChanges();

        await new AvisoDePalpiteConferido(ctx, push).ConferirAsync(partida, "/Torneios/Palpiteiros/1");

        await push.Received(1).EnviarParaJogadorAsync(100, "Você CRAVOU o placar! 🎯",
            Arg.Is<string>(c => c.Contains("6x2") && c.Contains("+3")),
            "/Torneios/Palpiteiros/1", AlcanceDoAviso.AppSemEmail);
    }

    [Fact]
    public async Task Quem_so_acertou_o_vencedor_ouve_a_frase_de_um_ponto()
    {
        var (ctx, push, partida) = MontarJogoFechado(games1: 6, games2: 2);
        ctx.PalpitesPartida.Add(new PalpitePartida
        {
            PartidaId = partida.Id, JogadorId = 100, DuplaEscolhidaId = 101,
        });
        ctx.SaveChanges();

        await new AvisoDePalpiteConferido(ctx, push).ConferirAsync(partida, null);

        await push.Received(1).EnviarParaJogadorAsync(100, "Palpite certeiro! 🎯",
            Arg.Is<string>(c => c.Contains("+1")), null, AlcanceDoAviso.AppSemEmail);
    }

    [Fact]
    public async Task Quem_errou_nao_ouve_nada()
    {
        var (ctx, push, partida) = MontarJogoFechado(games1: 6, games2: 2);
        ctx.PalpitesPartida.Add(new PalpitePartida
        {
            PartidaId = partida.Id, JogadorId = 100, DuplaEscolhidaId = 202, // apostou na perdedora
        });
        ctx.SaveChanges();

        await new AvisoDePalpiteConferido(ctx, push).ConferirAsync(partida, null);

        await push.DidNotReceive().EnviarParaJogadorAsync(Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    // Os quatro em quadra podem votar (a barra é opinião pública), mas o ranking não paga o
    // ponto — e o aviso não pode pagar por ele.
    [Fact]
    public async Task Quem_estava_em_quadra_nao_recebe_o_retorno()
    {
        var (ctx, push, partida) = MontarJogoFechado(games1: 6, games2: 2);
        ctx.PalpitesPartida.Add(new PalpitePartida
        {
            PartidaId = partida.Id, JogadorId = 1, DuplaEscolhidaId = 101, // o próprio vencedor
            GamesDupla1 = 6, GamesDupla2 = 2,
        });
        ctx.SaveChanges();

        await new AvisoDePalpiteConferido(ctx, push).ConferirAsync(partida, null);

        await push.DidNotReceive().EnviarParaJogadorAsync(Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    // ── OS MARCOS ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(10, true)]
    [InlineData(50, true)]
    [InlineData(9, false)]
    [InlineData(11, false)]
    public void A_vitoria_so_vira_marco_no_numero_exato(int totalVitorias, bool comemora)
    {
        var marco = MarcoPessoal.Da(totalJogos: totalVitorias + 5, totalVitorias, venceu: true);

        Assert.Equal(comemora, marco != null);
        if (comemora) Assert.Contains($"{totalVitorias}ª vitória", marco!.Corpo);
    }

    [Fact]
    public void Jogos_tambem_valem_marco_mesmo_na_derrota()
    {
        var marco = MarcoPessoal.Da(totalJogos: 25, totalVitorias: 7, venceu: false);

        Assert.NotNull(marco);
        Assert.Contains("25 jogos", marco!.Corpo);
    }

    // A 50ª vitória pode ser o 100º jogo: UMA festa, e a vitória é a notícia maior.
    [Fact]
    public void Quando_os_dois_marcos_caem_juntos_a_vitoria_ganha()
    {
        var marco = MarcoPessoal.Da(totalJogos: 100, totalVitorias: 50, venceu: true);

        Assert.NotNull(marco);
        Assert.Contains("50ª vitória", marco!.Corpo);
        Assert.DoesNotContain("100 jogos", marco.Corpo);
    }

    // Sem vitória no jogo, o marco de vitórias não dispara — mesmo que o total esteja nele
    // (é o caso do perdedor cuja contagem já estava no número por jogos antigos corrigidos).
    [Fact]
    public void Marco_de_vitoria_exige_ter_vencido_ESTE_jogo()
    {
        Assert.Null(MarcoPessoal.Da(totalJogos: 30, totalVitorias: 10, venceu: false));
    }

    // O caminho inteiro: a 10ª vitória de verdade, contada do banco.
    [Fact]
    public async Task A_decima_vitoria_e_comemorada_com_os_dois_da_dupla()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Torneios.Add(new Torneio { Id = 1, Nome = "Copa", Codigo = "CP1" });
        ctx.Categorias.Add(new Categoria { Id = 10, TorneioId = 1, Nome = "Open", Codigo = "OP" });
        ctx.Duplas.AddRange(
            new Dupla { Id = 101, CategoriaId = 10, Jogador1Id = 1, Jogador2Id = 2 },
            new Dupla { Id = 202, CategoriaId = 10, Jogador1Id = 3, Jogador2Id = 4 });

        Partida? ultima = null;
        for (int i = 0; i < 10; i++)
        {
            ultima = new Partida
            {
                Codigo = $"J{i:00}", TorneioId = 1, CategoriaId = 10,
                Dupla1Id = 101, Dupla2Id = 202, Fase = "Grupo A",
                Status = "Finalizada", VencedorId = 101, GamesDupla1 = 6, GamesDupla2 = 3,
            };
            ctx.Partidas.Add(ultima);
        }
        ctx.SaveChanges();

        var push = Substitute.For<IPushNotificationService>();
        await new AvisoDeMarcoPessoal(ctx, push).ConferirAsync(ultima!);

        foreach (var id in new[] { 1, 2 })
            await push.Received(1).EnviarParaJogadorAsync(id, "Marco pessoal! 🎉",
                Arg.Is<string>(c => c.Contains("10ª vitória")), Arg.Any<string?>(),
                AlcanceDoAviso.AppSemEmail);

        // Os perdedores têm 10 JOGOS — que não é marco de jogos (começa em 25). Silêncio.
        foreach (var id in new[] { 3, 4 })
            await push.DidNotReceive().EnviarParaJogadorAsync(id, Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    // ── O PADELÍMETRO NO AVISO DE RESULTADO ──────────────────────────────────────────────

    // O quanto o jogo mexeu no nível vai COLADO no push de resultado — um aviso só, nunca
    // um segundo push pra mesma pessoa no mesmo segundo.
    [Fact]
    public async Task O_resultado_diz_o_novo_padelimetro_de_cada_um()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, 2);
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();

        var partida = new Partida
        {
            Codigo = "GRP1", TorneioId = torneio.Id, CategoriaId = categoria.Id,
            Dupla1Id = duplas[0].Id, Dupla2Id = duplas[1].Id, Fase = "Grupo A",
            Status = "Finalizada", VencedorId = duplas[0].Id, GamesDupla1 = 6, GamesDupla2 = 3,
        };
        ctx.Partidas.Add(partida);
        ctx.SaveChanges();

        var push = Substitute.For<IPushNotificationService>();
        await TestInfra.NovoEncerramento(ctx, push).AplicarAsync(partida, acabouDeTerminar: true,
            new EncerramentoDaPartida.LinksDoAviso("/Torneios/Details/1", "/Torneios/Jogos/1"));

        // O vencedor lê o nível novo com o "+"; o perdedor, com o "-".
        await push.Received(1).EnviarParaJogadorAsync(duplas[0].Jogador1Id, "Vitória!",
            Arg.Is<string>(c => c.Contains("Padelímetro:") && c.Contains("(+")),
            Arg.Any<string?>(), AlcanceDoAviso.AppSemEmail);
        await push.Received(1).EnviarParaJogadorAsync(duplas[1].Jogador1Id, "Resultado do seu jogo",
            Arg.Is<string>(c => c.Contains("Padelímetro:") && c.Contains("(-")),
            Arg.Any<string?>(), AlcanceDoAviso.AppSemEmail);
    }

    // ── Cenário ──────────────────────────────────────────────────────────────────────────

    // Jogo fechado entre a dupla 101 (jogadores 1 e 2, vencedora) e a 202 (3 e 4).
    private static (DbPadelContext ctx, IPushNotificationService push, Partida partida)
        MontarJogoFechado(int games1, int games2)
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
            new Dupla { Id = 101, CategoriaId = 10, Jogador1Id = 1, Jogador2Id = 2 },
            new Dupla { Id = 202, CategoriaId = 10, Jogador1Id = 3, Jogador2Id = 4 });
        var partida = new Partida
        {
            Id = 500, Codigo = "JF1", TorneioId = 1, CategoriaId = 10,
            Dupla1Id = 101, Dupla2Id = 202, Fase = "Grupo A",
            Status = "Finalizada", VencedorId = 101, GamesDupla1 = games1, GamesDupla2 = games2,
        };
        ctx.Partidas.Add(partida);
        ctx.SaveChanges();

        return (ctx, Substitute.For<IPushNotificationService>(), partida);
    }
}
