using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O LOCAL EXTERNO PELO GerarChaves DE VERDADE — o botão que o organizador aperta.
//
// Os pedaços têm trava própria (SedeExtraComHorarioTests, SedeExtraNaGradeTests). Este arquivo
// prova que eles estão LIGADOS, e cobre a armadilha de contagem que a janela cria:
//
// ⚠️ QUADRA FECHADA CONTINUA OCUPANDO LUGAR NA LISTA DE VAGAS. A grade pede
// `jogos + margem` vagas, e cada rodada rende UMA vaga POR QUADRA CADASTRADA — inclusive pelas
// que estão fechadas naquele horário. Com metade das quadras alugadas só pra sábado de manhã,
// metade das vagas da sexta é morta, e o orçamento acaba antes dos jogos: sobra jogo SEM
// HORÁRIO NENHUM, que é o único desfecho que a grade nunca pode ter.
public class SedeExtraNoSorteioTests
{
    private static readonly DateTime Sexta18h = new(2026, 8, 14, 18, 0, 0);
    private static readonly DateTime Sabado = new(2026, 8, 15);

    // ⚠️ O MODO DE FALHA AQUI NÃO É "SEM HORÁRIO", É "COM HORÁRIO E SEM QUADRA". Quando o
    // orçamento de vagas acaba, o `Encaixar` entra com o jogo mesmo assim (é a regra: jogo sem
    // hora nenhuma é pior) — e numa vaga cujas quadras estão todas FECHADAS ele nasce com hora
    // e com "Quadra a definir". É o incidente do Interno de 05/08/2026 voltando por outra
    // porta: o jogador tem a hora sem ter o lugar, que é metade da informação de que precisa.
    [Theory]
    [InlineData(12)]
    [InlineData(20)]
    [InlineData(28)]
    public async Task Todo_jogo_sai_com_hora_E_com_quadra(int duplas)
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org) = MontarComSedeExtra(ctx, duplas);

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var jogos = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();

        Assert.NotEmpty(jogos);
        var semHora = jogos.Count(j => j.HorarioPrevisto == null);
        var semQuadra = jogos.Count(j => string.IsNullOrEmpty(j.NomeQuadra));
        Assert.True(semHora == 0 && semQuadra == 0,
            $"{jogos.Count} jogos: {semHora} sem horário, {semQuadra} sem quadra.");
    }

    [Fact]
    public async Task Nenhum_jogo_cai_na_quadra_alugada_fora_da_janela()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org) = MontarComSedeExtra(ctx);

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var noExterno = await ctx.Partidas
            .Where(p => p.TorneioId == torneio.Id && (p.NomeQuadra == "Alugada 1" || p.NomeQuadra == "Alugada 2"))
            .ToListAsync();

        Assert.All(noExterno, j =>
            Assert.True(j.HorarioPrevisto >= Sabado.AddHours(8) && j.HorarioPrevisto < Sabado.AddHours(12),
                $"jogo marcado na quadra alugada em {j.HorarioPrevisto:dd/MM HH:mm}, fora da janela 8h–12h"));
    }

    // Duas quadras de casa + duas alugadas que só existem no sábado das 8h às 12h.
    private static (Torneio torneio, Jogador organizador) MontarComSedeExtra(DbPadelContext ctx, int duplas = 12)
    {
        var organizador = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(organizador);

        var casa = new Clube { Nome = "Clube do Er" };
        var alugado = new Clube { Nome = "Arena Alugada" };
        ctx.Clubes.AddRange(casa, alugado);
        ctx.SaveChanges();

        var torneio = new Torneio
        {
            Nome = "Etapa Grande",
            Codigo = "EG123",
            Status = "Chaves em Sorteio",
            DataInicio = Sexta18h,
            ClubeId = casa.Id,
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

        ctx.Quadras.AddRange(
            new Quadra { TorneioId = torneio.Id, Nome = "Casa 1", ClubeId = casa.Id },
            new Quadra { TorneioId = torneio.Id, Nome = "Casa 2", ClubeId = casa.Id },
            new Quadra { TorneioId = torneio.Id, Nome = "Alugada 1", ClubeId = alugado.Id,
                         DisponivelDe = Sabado.AddHours(8), DisponivelAte = Sabado.AddHours(12) },
            new Quadra { TorneioId = torneio.Id, Nome = "Alugada 2", ClubeId = alugado.Id,
                         DisponivelDe = Sabado.AddHours(8), DisponivelAte = Sabado.AddHours(12) });

        var jogadores = Enumerable.Range(1, duplas * 2).Select(TestInfra.NovoJogador).ToList();
        ctx.Jogadores.AddRange(jogadores);
        ctx.SaveChanges();

        for (int i = 0; i < duplas; i++)
            ctx.Duplas.Add(new Dupla
            {
                Categoria = categoria,
                Jogador1 = jogadores[i * 2],
                Jogador2 = jogadores[i * 2 + 1],
            });

        ctx.SaveChanges();
        return (torneio, organizador);
    }
}
