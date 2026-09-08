using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O FAVOR ACONTECENDO DE VERDADE — pelo GerarChaves, que é o botão que o organizador aperta.
//
// Os pedaços já têm trava própria (ConcentracaoDeJogosTests, ConcentracaoNaGradeTests,
// VagasAlcancamAConcentracaoTests). Este arquivo é o único que prova que eles estão LIGADOS:
// se a concentração não chegar do banco até o `Encaixar`, ou se a lista de vagas não alcançar
// o turno escolhido, tudo aqui continua compilando e o jogo sai no dia errado.
//
// ⚠️ O TURNO ESCOLHIDO É O SÁBADO À TARDE de propósito: é o mais tardio dos três, o que exige
// a grade mais longa, e por isso o que quebraria calado (ver VagasAlcancamAConcentracaoTests).
public class ConcentracaoNoSorteioTests
{
    private static readonly DateTime Sexta18h = new(2026, 8, 14, 18, 0, 0);
    private static readonly DateTime Sabado = new(2026, 8, 15);

    [Fact]
    public async Task Os_2_jogos_da_dupla_concentrada_caem_no_sabado_a_tarde()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org, favorecida) = MontarFimDeSemana(ctx, TurnoDeConcentracao.SabadoTarde);

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var jogos = await ctx.Partidas
            .Where(p => p.TorneioId == torneio.Id
                     && (p.Dupla1Id == favorecida.Id || p.Dupla2Id == favorecida.Id))
            .ToListAsync();

        Assert.NotEmpty(jogos);
        Assert.All(jogos, j =>
        {
            Assert.NotNull(j.HorarioPrevisto);
            Assert.True(j.HorarioPrevisto >= Sabado.Add(JanelasDeImpedimento.CorteSabadoManhaTarde)
                     && j.HorarioPrevisto < Sabado.AddDays(1),
                $"jogo de grupo caiu em {j.HorarioPrevisto:dd/MM HH:mm}, fora do sábado à tarde");
        });
    }

    [Fact]
    public async Task Na_sexta_tambem_funciona()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org, favorecida) = MontarFimDeSemana(ctx, TurnoDeConcentracao.SextaNoite);

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var jogos = await ctx.Partidas
            .Where(p => p.TorneioId == torneio.Id
                     && (p.Dupla1Id == favorecida.Id || p.Dupla2Id == favorecida.Id))
            .ToListAsync();

        Assert.NotEmpty(jogos);
        Assert.All(jogos, j => Assert.Equal(new DateTime(2026, 8, 14), j.HorarioPrevisto!.Value.Date));
    }

    // ⚠️ O CONTROLE: ninguém MAIS é empurrado. A concentração é de UMA dupla, e uma grade que
    // empurrasse o torneio inteiro pro sábado à tarde também faria o teste de cima passar.
    [Fact]
    public async Task O_resto_do_torneio_continua_abrindo_na_sexta()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org, favorecida) = MontarFimDeSemana(ctx, TurnoDeConcentracao.SabadoTarde);

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var dosOutros = await ctx.Partidas
            .Where(p => p.TorneioId == torneio.Id
                     && p.Dupla1Id != favorecida.Id && p.Dupla2Id != favorecida.Id)
            .ToListAsync();

        Assert.Equal(torneio.AberturaDaGrade, dosOutros.Min(j => j.HorarioPrevisto));
    }

    // Um fim de semana no formato que o Felipe descreveu: abre sexta 18h, sábado das 8h às 23h.
    // 12 duplas numa categoria — 4 grupos de 3, 12 jogos de grupo — e UMA delas concentrada.
    private static (Torneio torneio, Jogador organizador, Dupla favorecida) MontarFimDeSemana(
        DbPadelContext ctx, TurnoDeConcentracao turno)
    {
        var organizador = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(organizador);

        var torneio = new Torneio
        {
            Nome = "Etapa de Teste",
            Codigo = "ETP123",
            Status = "Chaves em Sorteio",
            DataInicio = Sexta18h,
            QuantidadeQuadras = 4,
            TempoPrevistoPartidaMinutos = 50,
            HoraInicioDoDia = new TimeSpan(18, 0, 0),
            HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0),
            HoraFimDoDia = new TimeSpan(23, 0, 0),
        };
        ctx.Torneios.Add(torneio);

        var categoria = new Categoria { Nome = "5ª Feminina", Codigo = "C5F", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = organizador.Id });

        var jogadores = Enumerable.Range(1, 24).Select(TestInfra.NovoJogador).ToList();
        ctx.Jogadores.AddRange(jogadores);
        ctx.SaveChanges();

        Dupla? favorecida = null;
        for (int i = 0; i < 12; i++)
        {
            var dupla = new Dupla
            {
                Categoria = categoria,
                Jogador1 = jogadores[i * 2],
                Jogador2 = jogadores[i * 2 + 1],
            };
            if (i == 0) { dupla.ConcentrarJogosEm = turno; favorecida = dupla; }
            ctx.Duplas.Add(dupla);
        }

        ctx.SaveChanges();
        return (torneio, organizador, favorecida!);
    }
}
