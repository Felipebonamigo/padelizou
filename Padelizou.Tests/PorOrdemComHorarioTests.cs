using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 🗣️ Felipe, 09/09/2026, olhando os grupos do torneio do Er: "mesmo que seja por ordem os
// jogos, tem q ter o horario dos jogos; o que realmente muda é a quadra — o horário do jogo,
// teoricamente, é pré-definido, para as pessoas se organizarem".
//
// ⚠️ ISSO REVISA UMA DECISÃO ESCRITA NO MODELO, e vale dizer qual: o `SemHorarioPrevisto`
// nasceu dizendo que "horário inventado que ninguém cumpre é pior que horário nenhum". O que
// mudou não foi a preocupação — foi o alvo dela. O que atrasa e não se cumpre é a QUADRA (qual
// delas vaga primeiro); a HORA sai da mesma conta de sempre (quantos jogos, quantas quadras,
// quanto dura cada um) e serve pra pessoa saber se chega às 8h ou às 15h.
//
// Então o modo continua existindo e continua sendo "a Mesa chama": o que ele deixa de fazer é
// esconder a hora. Grava hora, não grava quadra.
public class PorOrdemComHorarioTests
{
    [Fact]
    public async Task Sorteio_por_ordem_grava_horario_em_todos_os_jogos()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org) = MontarPorOrdem(ctx);

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var jogos = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();

        Assert.NotEmpty(jogos);
        Assert.All(jogos, j => Assert.NotNull(j.HorarioPrevisto));
    }

    // ⚠️ O OUTRO LADO, e é o que o Felipe nomeou: a quadra é o que muda de verdade, então ela
    // continua em aberto. Marcar quadra aqui seria a promessa que o modo existe pra não fazer.
    [Fact]
    public async Task E_deixa_a_quadra_em_aberto()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org) = MontarPorOrdem(ctx);

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var jogos = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();
        Assert.All(jogos, j => Assert.True(string.IsNullOrEmpty(j.NomeQuadra),
            $"jogo saiu com quadra \"{j.NomeQuadra}\" num torneio por ordem"));
    }

    // A hora sai da MESMA conta do modo normal — quantas quadras, quanto dura cada jogo. Não é
    // um relógio decorativo: dois jogos na mesma rodada compartilham o horário, e a rodada
    // seguinte anda o tempo de uma partida.
    [Fact]
    public async Task O_horario_respeita_a_capacidade_das_quadras()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org) = MontarPorOrdem(ctx);

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var horarios = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id)
            .Select(p => p.HorarioPrevisto!.Value).OrderBy(h => h).ToListAsync();

        Assert.Equal(torneio.AberturaDaGrade, horarios.First());
        // 2 quadras: o terceiro jogo só começa uma partida depois do primeiro.
        Assert.True(horarios.Distinct().Count() > 1, "todos os jogos caíram no mesmo horário");
    }

    // ⚠️ E NINGUÉM É CHAMADO PRA DOIS JOGOS NO MESMO HORÁRIO. Antes, "sem hora" fazia a
    // pergunta não existir; agora que existe hora, ela tem que estar certa — senão o horário
    // que a pessoa usa pra se organizar é justamente o que a coloca em dois lugares.
    [Fact]
    public async Task Ninguem_joga_dois_jogos_no_mesmo_horario()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org) = MontarPorOrdem(ctx);

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var jogos = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id)
            .Include(p => p.Dupla1).Include(p => p.Dupla2).ToListAsync();

        var conflitos = jogos
            .GroupBy(j => j.HorarioPrevisto)
            .SelectMany(h => h
                .SelectMany(j => new[] { j.Dupla1, j.Dupla2 })
                .SelectMany(d => new[] { d.Jogador1Id, d.Jogador2Id })
                .Where(i => i != null)
                .GroupBy(i => i!.Value)
                .Where(g => g.Count() > 1)
                .Select(g => $"{h.Key:dd/MM HH:mm} jogador {g.Key}"))
            .ToList();

        Assert.Empty(conflitos);
    }

    // 🗣️ "Liberar Refazer grade no por-ordem": o torneio do Er JÁ sorteou sem hora nenhuma, e
    // sem isto a única saída seria desfazer o sorteio — o que remonta os grupos e muda os
    // confrontos de 63 duplas.
    [Fact]
    public async Task Refazer_grade_da_horario_a_quem_ja_tinha_sorteado_sem_hora()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org) = MontarPorOrdem(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);

        // Simula o que está no banco do Er: sorteado antes desta mudança, sem hora nenhuma.
        foreach (var jogo in await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync())
        {
            jogo.HorarioPrevisto = null;
            jogo.NomeQuadra = null;
        }
        await ctx.SaveChangesAsync();

        await controller.RefazerGrade(torneio.Id, null);

        var jogos = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();
        Assert.All(jogos, j => Assert.NotNull(j.HorarioPrevisto));
        Assert.All(jogos, j => Assert.True(string.IsNullOrEmpty(j.NomeQuadra)));
    }

    // ⚠️ E OS CONFRONTOS NÃO MUDAM: refazer grade mexe em hora, não em quem joga com quem. É a
    // diferença entre isto e "desfazer o sorteio", e é ela que faz o botão ser seguro no Er.
    [Fact]
    public async Task Refazer_grade_nao_mexe_nos_confrontos()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org) = MontarPorOrdem(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);
        var antes = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id)
            .OrderBy(p => p.Id).Select(p => $"{p.Dupla1Id}x{p.Dupla2Id}").ToListAsync();

        await controller.RefazerGrade(torneio.Id, null);

        var depois = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id)
            .OrderBy(p => p.Id).Select(p => $"{p.Dupla1Id}x{p.Dupla2Id}").ToListAsync();

        Assert.Equal(antes, depois);
    }

    private static (Torneio torneio, Jogador organizador) MontarPorOrdem(DbPadelContext ctx)
    {
        var organizador = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(organizador);

        var torneio = new Torneio
        {
            Nome = "Etapa por ordem",
            Codigo = "ORD123",
            Status = "Chaves em Sorteio",
            DataInicio = new DateTime(2026, 8, 15, 8, 0, 0),
            QuantidadeQuadras = 2,
            TempoPrevistoPartidaMinutos = 50,
            HoraInicioDoDia = new TimeSpan(8, 0, 0),
            HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0),
            HoraFimDoDia = new TimeSpan(23, 0, 0),
            SemHorarioPrevisto = true,
        };
        ctx.Torneios.Add(torneio);

        var categoria = new Categoria { Nome = "3ª Masculina", Codigo = "C3M", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = organizador.Id });

        // ⚠️ AS QUADRAS PRECISAM EXISTIR, e a primeira versão deste arquivo não as cadastrava —
        // o que fazia `Assert.NomeQuadra vazio` passar por vazio: sem quadra cadastrada o
        // encaixe não nomeia nada, e o teste aprovava mesmo com a limpeza REMOVIDA. Falsificar
        // pegou. Com elas, apagar a quadra passa a ser uma escolha visível.
        ctx.Quadras.AddRange(
            new Quadra { TorneioId = torneio.Id, Nome = "Quadra 1" },
            new Quadra { TorneioId = torneio.Id, Nome = "Quadra 2" });

        var jogadores = Enumerable.Range(1, 24).Select(TestInfra.NovoJogador).ToList();
        ctx.Jogadores.AddRange(jogadores);
        ctx.SaveChanges();

        for (int i = 0; i < 12; i++)
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
