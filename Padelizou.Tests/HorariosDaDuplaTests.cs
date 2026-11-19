using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// OS HORÁRIOS QUE O ORGANIZADOR COMBINA COM A DUPLA (19/11/2026) — Models/HorarioDaDupla.
// "Pode jogar entre" vale só nos grupos (porta da concentração); "não pode jogar" vale em toda
// fase (porta do impedimento). Os dois cedem no aperto. Só o organizador cadastra.
public class HorariosDaDuplaTests
{
    private static readonly DateTime Sabado = new(2026, 11, 21);

    private static Torneio Torneio() => new()
    {
        Nome = "T", Codigo = "T1", DataInicio = Sabado.AddHours(8), QuantidadeQuadras = 1,
        TempoPrevistoPartidaMinutos = 60, HoraInicioDoDia = new TimeSpan(8, 0, 0),
        HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0), HoraFimDoDia = new TimeSpan(22, 0, 0),
    };

    private static Dupla Dupla(int id, params HorarioDaDupla[] horarios)
    {
        var d = new Dupla { Id = id, Jogador1Id = id * 10, Jogador2Id = id * 10 + 1 };
        foreach (var h in horarios) d.HorariosDoOrganizador.Add(h);
        return d;
    }

    private static HorarioDaDupla Pode(int de, int ate, int dia = 0) =>
        new() { PodeJogar = true, Inicio = Sabado.AddDays(dia).AddHours(de), Fim = Sabado.AddDays(dia).AddHours(ate) };
    private static HorarioDaDupla NaoPode(int de, int ate, int dia = 0) =>
        new() { PodeJogar = false, Inicio = Sabado.AddDays(dia).AddHours(de), Fim = Sabado.AddDays(dia).AddHours(ate) };

    private static Partida Jogo(int d1, int d2, string fase = "Grupo A") =>
        new() { Codigo = $"{d1}x{d2}", Status = "Agendada", Fase = fase, CategoriaId = 1, Dupla1Id = d1, Dupla2Id = d2 };

    private static List<DateTime> Vagas(params int[] horas) => horas.Select(h => Sabado.AddHours(h)).ToList();

    // ── AS DUAS PORTAS ─────────────────────────────────────────────────────────────────
    [Fact]
    public void Nao_pode_vira_janela_de_impedimento_quantas_o_organizador_quiser()
    {
        var dupla = Dupla(1, NaoPode(19, 23), NaoPode(0, 24, dia: 1));

        var janelas = JanelasDeImpedimento.Da(Torneio(), dupla).ToList();

        Assert.Contains((Sabado.AddHours(19), Sabado.AddHours(23)), janelas);
        Assert.Contains((Sabado.AddDays(1), Sabado.AddDays(2)), janelas);
    }

    [Fact]
    public void Pode_jogar_vira_concentracao_e_fora_dos_intervalos_fica_proibido_nos_grupos()
    {
        var dupla = Dupla(1, Pode(14, 16), Pode(19, 21));

        var proibidas = ConcentracaoDeJogos.Da(Torneio(), dupla).ToList();

        bool Proibido(int hora) => proibidas.Any(j => Sabado.AddHours(hora) >= j.Inicio && Sabado.AddHours(hora) < j.Fim);
        Assert.True(Proibido(10));
        Assert.False(Proibido(14));
        Assert.False(Proibido(15));
        Assert.True(Proibido(17));
        Assert.False(Proibido(20));
        Assert.True(Proibido(21));
    }

    [Fact]
    public void A_grade_vai_ate_o_fim_do_ultimo_intervalo_de_pode_jogar()
    {
        var concentracoes = ConcentracaoDeJogos.De(Torneio(), new[] { Dupla(1, Pode(19, 21)) });
        Assert.Equal(Sabado.AddHours(21), concentracoes.AteQuando);
    }

    // ── NA GRADE ───────────────────────────────────────────────────────────────────────
    [Fact]
    public void O_jogo_de_grupo_cai_dentro_do_pode_jogar()
    {
        var duplas = new[] { Dupla(1, Pode(14, 16)), Dupla(2), Dupla(3), Dupla(4) };
        var comRestricao = Jogo(1, 2);
        var livre = Jogo(3, 4);
        var t = Torneio();

        GradeDeJogos.Encaixar(new List<Partida> { livre, comRestricao }, Vagas(10, 11, 14, 15), 60,
            janelasProibidasPorDupla: JanelasDeImpedimento.PorDupla(t, duplas),
            janelasSoNosGruposPorDupla: ConcentracaoDeJogos.De(t, duplas).Janelas);

        Assert.InRange(comRestricao.HorarioPrevisto!.Value, Sabado.AddHours(14), Sabado.AddHours(15));
    }

    // "Pode jogar" é só pros jogos de chave: o mata-mata fica livre.
    [Fact]
    public void Pode_jogar_nao_prende_o_mata_mata()
    {
        var duplas = new[] { Dupla(1, Pode(14, 16)), Dupla(2) };
        var final = Jogo(1, 2, "Final");
        var t = Torneio();

        GradeDeJogos.Encaixar(new List<Partida> { final }, Vagas(10, 14), 60,
            janelasProibidasPorDupla: JanelasDeImpedimento.PorDupla(t, duplas),
            janelasSoNosGruposPorDupla: ConcentracaoDeJogos.De(t, duplas).Janelas);

        Assert.Equal(Sabado.AddHours(10), final.HorarioPrevisto);
    }

    // "Não pode" vale em toda fase — o pedido é o do sábado à noite e do domingo do mata-mata.
    [Fact]
    public void Nao_pode_tira_o_mata_mata_do_horario()
    {
        var duplas = new[] { Dupla(1, NaoPode(8, 12)), Dupla(2) };
        var final = Jogo(1, 2, "Final");

        GradeDeJogos.Encaixar(new List<Partida> { final }, Vagas(10, 14), 60,
            janelasProibidasPorDupla: JanelasDeImpedimento.PorDupla(Torneio(), duplas));

        Assert.Equal(Sabado.AddHours(14), final.HorarioPrevisto);
    }

    [Fact]
    public void Se_nao_cabe_a_grade_cede_e_o_jogo_ainda_ganha_horario()
    {
        var duplas = new[] { Dupla(1, NaoPode(8, 22)), Dupla(2) };
        var jogo = Jogo(1, 2);

        GradeDeJogos.Encaixar(new List<Partida> { jogo }, Vagas(10), 60,
            janelasProibidasPorDupla: JanelasDeImpedimento.PorDupla(Torneio(), duplas));

        Assert.NotNull(jogo.HorarioPrevisto);
    }

    // ── O BANCO ────────────────────────────────────────────────────────────────────────
    // A coleção vem sozinha: a grade carrega duplas por uma dúzia de consultas, e nenhuma
    // precisa lembrar do Include.
    [Fact]
    public async Task Os_horarios_vem_junto_com_a_dupla_sem_include()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1);
        var dupla = ctx.Duplas.Single();
        ctx.HorariosDaDupla.Add(new HorarioDaDupla { DuplaId = dupla.Id, PodeJogar = false, Inicio = Sabado, Fim = Sabado.AddDays(1) });
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var lida = await ctx.Duplas.SingleAsync();

        Assert.Single(lida.HorariosDoOrganizador);
    }

    // ── A TELA DO ORGANIZADOR ──────────────────────────────────────────────────────────
    [Fact]
    public async Task O_organizador_adiciona_e_remove_horarios()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1);
        torneio.DataInicio = Sabado.AddHours(8);
        await ctx.SaveChangesAsync();
        var dupla = ctx.Duplas.Single();
        var c = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        await c.AdicionarHorarioDaDupla(dupla.Id, podeJogar: false, dia: Sabado.AddDays(1), de: null, ate: null, diaInteiro: true);
        await c.AdicionarHorarioDaDupla(dupla.Id, podeJogar: true, dia: Sabado, de: new TimeSpan(14, 0, 0), ate: new TimeSpan(18, 0, 0));

        var horarios = ctx.HorariosDaDupla.OrderBy(h => h.Inicio).ToList();
        Assert.Equal(2, horarios.Count);
        Assert.Equal((true, Sabado.AddHours(14), Sabado.AddHours(18)), (horarios[0].PodeJogar, horarios[0].Inicio, horarios[0].Fim));
        Assert.Equal((false, Sabado.AddDays(1), Sabado.AddDays(2)), (horarios[1].PodeJogar, horarios[1].Inicio, horarios[1].Fim));

        await c.RemoverHorarioDaDupla(horarios[0].Id);
        Assert.Single(ctx.HorariosDaDupla);
    }

    [Fact]
    public async Task Fim_antes_do_inicio_e_recusado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1);
        var dupla = ctx.Duplas.Single();
        var c = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        await c.AdicionarHorarioDaDupla(dupla.Id, true, Sabado, new TimeSpan(18, 0, 0), new TimeSpan(14, 0, 0));

        Assert.NotNull(c.TempData["Erro"]);
        Assert.Empty(ctx.HorariosDaDupla);
    }

    // Jogador comum segue com o impedimento padrão: aqui só entra organizador.
    [Fact]
    public async Task Quem_nao_organiza_nao_adiciona_nem_remove()
    {
        using var ctx = TestInfra.NovoContexto();
        TestInfra.MontarTorneio(ctx, qtdDuplas: 1);
        var dupla = ctx.Duplas.Single();
        var horario = new HorarioDaDupla { DuplaId = dupla.Id, PodeJogar = false, Inicio = Sabado, Fim = Sabado.AddDays(1) };
        ctx.HorariosDaDupla.Add(horario);
        await ctx.SaveChangesAsync();
        var c = TestInfra.NovoTorneiosController(ctx, dupla.Jogador1Id);   // o próprio jogador

        Assert.IsType<ForbidResult>(await c.AdicionarHorarioDaDupla(dupla.Id, true, Sabado, new TimeSpan(14, 0, 0), new TimeSpan(18, 0, 0)));
        Assert.IsType<ForbidResult>(await c.RemoverHorarioDaDupla(horario.Id));
        Assert.Single(ctx.HorariosDaDupla);
    }
}

// O auto-include vira SQL de verdade (o InMemory não confere).
public class TraducaoDosHorariosDaDuplaTests
{
    [Fact]
    public void Carregar_duplas_traz_os_horarios_num_join()
    {
        var options = new DbContextOptionsBuilder<DbPadelContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=nao_existe;Username=x;Password=x").Options;
        using var ctx = new DbPadelContext(options);

        var sql = ctx.Duplas.Where(d => d.Categoria.TorneioId == 7).ToQueryString();

        Assert.Contains("HorarioDaDupla", sql);
    }
}
