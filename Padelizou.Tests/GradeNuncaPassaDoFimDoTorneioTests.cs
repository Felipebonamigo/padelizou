using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// A GRADE NUNCA PASSA DO ÚLTIMO DIA DO TORNEIO (11/11/2026).
//
// 🗣️ Felipe, com o Chakra (torneio 25, qui 12 a dom 15/11) com jogo marcado na SEGUNDA 16/11:
// *"preciso que isso não aconteça mais em nenhuma hipótese […] para ele respeitar as datas e
// horários pré-estabelecidos"*.
//
// O motor pula um horário quando nenhum jogo da fila serve nele (pessoa ocupada, impedimento,
// "Pôr os 2 jogos em") e, no último recurso, encaixa o jogo no FIM da lista de horários. Essa lista
// não tinha teto: a concentração estende o alcance por 30 dias, e o jogo que não coube no turno
// escolhido ia parar dias depois do torneio acabar. Com `DataFim`, nenhum horário passa do último
// jogo do último dia — o que não couber fica SEM HORÁRIO, e a conferência da grade avisa.
public class GradeNuncaPassaDoFimDoTorneioTests
{
    private static readonly DateTime Quinta = new(2026, 11, 12);

    private static async Task<(DbPadelContext ctx, Torneio torneio, Jogador org)> TorneioAsync(
        int duplas, DateTime fim, string? horariosPorDia = null)
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, duplas);
        torneio.DataInicio = Quinta;
        torneio.DataFim = fim;
        torneio.HoraInicioDoDia = new TimeSpan(18, 0, 0);
        torneio.HoraInicioDiasSeguintes = new TimeSpan(10, 0, 0);
        torneio.HoraFimDoDia = new TimeSpan(22, 10, 0);
        torneio.HorariosPorDia = horariosPorDia;
        torneio.QuantidadeQuadras = 1;
        torneio.TempoPrevistoPartidaMinutos = 50;
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra A" });
        await ctx.SaveChangesAsync();
        return (ctx, torneio, org);
    }

    private static void NenhumDepoisDoFim(DbPadelContext ctx, Torneio torneio)
    {
        var cal = HorarioDoDia.Do(torneio);
        var fim = torneio.DataFim!.Value.Date;
        var ultimoInicio = fim.Add(cal.Limite(fim));
        var jogos = ctx.Partidas.AsNoTracking().Where(p => p.TorneioId == torneio.Id && p.HorarioPrevisto != null).ToList();
        Assert.NotEmpty(jogos);
        Assert.All(jogos, j => Assert.True(j.HorarioPrevisto <= ultimoInicio,
            $"jogo {j.Id} marcado {j.HorarioPrevisto:ddd dd/MM HH:mm}, depois do último início {ultimoInicio:ddd dd/MM HH:mm}"));
    }

    [Fact]
    public async Task Jogo_que_nao_cabe_no_prazo_fica_sem_horario_e_nunca_vai_pro_dia_seguinte()
    {
        // Só quinta, das 18h às 19h40 numa quadra: 3 horários pra 9 jogos.
        var (ctx, torneio, org) = await TorneioAsync(9, Quinta, "2026-11-12=18:00-19:40");

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        NenhumDepoisDoFim(ctx, torneio);
    }

    [Fact]
    public async Task Dupla_concentrada_num_turno_lotado_nao_vai_pra_depois_do_domingo()
    {
        var (ctx, torneio, org) = await TorneioAsync(9, Quinta.AddDays(3));
        // Todo mundo pede "os 2 jogos na quinta à noite": quinta tem 6 horários pra 9 jogos.
        foreach (var dupla in ctx.Duplas) dupla.ConcentrarJogosEm = TurnoDeConcentracao.QuintaNoite;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        NenhumDepoisDoFim(ctx, torneio);
    }

    [Fact]
    public async Task Recalcular_tambem_respeita_o_ultimo_dia()
    {
        var (ctx, torneio, org) = await TorneioAsync(9, Quinta.AddDays(3));
        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);
        foreach (var dupla in ctx.Duplas) dupla.ConcentrarJogosEm = TurnoDeConcentracao.QuintaNoite;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).AplicarPlanejamento(torneio.Id,
            limites: "2026-11-15=20:00");

        ctx.ChangeTracker.Clear();
        NenhumDepoisDoFim(ctx, await ctx.Torneios.SingleAsync(t => t.Id == torneio.Id));
    }

    // O CASO DO CHAKRA, como estava no print: Quadra A com janela "12/11 18:00 até 15/11 20:00",
    // sábado até 16h, domingo até 20h, e duplas com "os 2 jogos no sábado de manhã". Com janela na
    // quadra, a lista de vagas leva horários FECHADOS de segunda, e o jogo que não serviu antes caía
    // num deles — segunda 12:30, 13:20, 14:10.
    [Fact]
    public async Task Quadra_com_janela_ate_domingo_e_concentracao_nao_marcam_segunda()
    {
        var (ctx, torneio, org) = await TorneioAsync(15, Quinta.AddDays(3),
            "2026-11-13=18:00-22:10;2026-11-14=10:00-16:00;2026-11-15=10:00-20:00");
        var quadra = ctx.Quadras.Single(q => q.TorneioId == torneio.Id);
        quadra.DisponivelDe = Quinta.AddHours(18);
        quadra.DisponivelAte = Quinta.AddDays(3).AddHours(20);
        foreach (var dupla in ctx.Duplas.OrderBy(d => d.Id).Take(8))
            dupla.ConcentrarJogosEm = TurnoDeConcentracao.SabadoManha;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        NenhumDepoisDoFim(ctx, torneio);
    }

    // O PLANEJAMENTO CONTA O MATA-MATA QUE AINDA VAI NASCER. 🗣️ No Chakra a tela dizia "Cabe — 19
    // jogos em 1 quadra" com só os jogos de GRUPO sorteados; as quartas, semis e finais ainda iam
    // nascer e não entravam na conta, e o organizador não soube que faltava quadra.
    [Fact]
    public async Task Depois_do_sorteio_o_planejamento_conta_tambem_o_mata_mata()
    {
        var (ctx, torneio, org) = await TorneioAsync(9, Quinta.AddDays(3));
        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);
        int sorteados = ctx.Partidas.Count(p => p.TorneioId == torneio.Id);

        var vm = (Padelizou.ViewModels.PlanejamentoDeQuadrasVM)Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(
            await TestInfra.NovoTorneiosController(ctx, org.Id).Planejamento(torneio.Id)).Model!;

        Assert.True(vm.JogosDoTorneio > sorteados,
            $"o planejamento contou {vm.JogosDoTorneio}, só os {sorteados} do grupo — sem o mata-mata");
    }

    // A PRÉVIA DO MATA-MATA TAMBÉM PARA NO ÚLTIMO DIA. É ela que vira o horário das quartas,
    // semis e final quando eles nascem (a promessa gravada no sorteio): no Chakra ela marcava
    // "Quartas de Final 1 · seg 16/11 15:00". O que não couber fica SEM hora — nunca depois do fim.
    [Fact]
    public async Task A_previa_do_mata_mata_nunca_marca_depois_do_ultimo_dia()
    {
        // Quinta e sexta à noite: 10 horários, os 9 jogos de grupo cabem e o mata-mata não.
        var (ctx, torneio, org) = await TorneioAsync(9, Quinta.AddDays(1),
            "2026-11-12=18:00-22:10;2026-11-13=18:00-20:30");

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);
        // Aprovar é o que congela a prévia em promessa (ReservaDeHorario).
        await TestInfra.NovoTorneiosController(ctx, org.Id).AprovarChaves(torneio.Id, avisarJogadores: false);

        var ultimoInicio = Quinta.AddDays(1).AddHours(20).AddMinutes(30);
        var reservas = ctx.ReservasDeHorario.AsNoTracking()
            .Where(r => r.Categoria.TorneioId == torneio.Id).ToList();
        Assert.NotEmpty(reservas);
        Assert.All(reservas, r => Assert.True(r.Horario <= ultimoInicio,
            $"{r.Fase} {r.Numero} prometida pra {r.Horario:ddd dd/MM HH:mm}, depois do fim"));
        NenhumDepoisDoFim(ctx, torneio);
    }

    // O JOGO SEM SAÍDA VAI PRO PRIMEIRO HORÁRIO LIVRE, NÃO PRO ÚLTIMO. 🗣️ Felipe, com o Chakra: um
    // jogo de grupo em domingo 16:40 com sábado à tarde e domingo de manhã vazios — e, como o
    // mata-mata espera o fim dos grupos, quartas, semis e final ficaram "por ordem", sem hora.
    // Quando a restrição da dupla ("Pôr os 2 jogos em", impedimento) não tem como ser cumprida em
    // NENHUM horário que resta, esperar não ajuda: o motor pulava horário até sobrarem só os do
    // FIM da lista e jogava o jogo lá. Agora ele entra no primeiro horário em que as pessoas estão
    // livres e há quadra.
    [Fact]
    public async Task Jogo_que_nao_cabe_no_turno_pedido_entra_no_primeiro_horario_livre()
    {
        var (ctx, torneio, org) = await TorneioAsync(9, Quinta.AddDays(3),
            "2026-11-13=18:00-22:10;2026-11-14=10:00-16:00;2026-11-15=10:00-20:00");
        // Todo mundo pede "os 2 jogos na quinta à noite": quinta tem 6 horários pra 9 jogos.
        foreach (var dupla in ctx.Duplas) dupla.ConcentrarJogosEm = TurnoDeConcentracao.QuintaNoite;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var sobra = ctx.Partidas.AsNoTracking()
            .Where(p => p.TorneioId == torneio.Id && p.HorarioPrevisto > Quinta.AddDays(1))
            .Select(p => p.HorarioPrevisto!.Value).ToList();
        Assert.Equal(3, sobra.Count);
        // Sexta abre 18h: os 3 que sobraram são 18:00, 18:50 e 19:40 — não 20:30, 21:20 e 22:10.
        Assert.True(sobra.Max() <= Quinta.AddDays(1).AddHours(19).AddMinutes(40),
            $"os jogos sem saída foram pro fim: {string.Join(", ", sobra.OrderBy(h => h).Select(h => h.ToString("ddd HH:mm")))}");
    }

    // A ORDEM ENTRE CATEGORIAS CEDE QUANDO SEGUI-LA DEIXARIA JOGO SEM HORA. 🗣️ Felipe pediu
    // *"primeiro todas as chaves, depois todas as primeiras eliminatórias […] as finais por último"*
    // — e isso continua valendo. Mas uma dupla da OUTRA categoria impedida no sábado leva um jogo
    // de grupo pro domingo, e o mata-mata de quem já acabou os grupos esperava esse jogo: sábado à
    // noite vazio e quartas "por ordem". Achado pela varredura de 500 torneios (11/11/2026).
    [Fact]
    public async Task Mata_mata_de_quem_acabou_os_grupos_nao_espera_jogo_de_outra_categoria_se_for_ficar_sem_hora()
    {
        var sabado = Quinta.AddDays(2);
        var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, 6);
        torneio.DataInicio = sabado;
        torneio.DataFim = sabado.AddDays(1);
        torneio.HoraInicioDoDia = new TimeSpan(10, 0, 0);
        torneio.HoraInicioDiasSeguintes = new TimeSpan(10, 0, 0);
        torneio.HoraFimDoDia = new TimeSpan(21, 0, 0);
        torneio.HorariosPorDia = $"{sabado.AddDays(1):yyyy-MM-dd}=10:00-11:40";
        torneio.QuantidadeQuadras = 1;
        torneio.TempoPrevistoPartidaMinutos = 50;
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra A" });
        var outra = new Categoria { Nome = "Outra", Codigo = "OUT", Torneio = torneio };
        ctx.Categorias.Add(outra);
        for (int i = 0; i < 3; i++)
        {
            var j1 = TestInfra.NovoJogador(700 + i * 2); var j2 = TestInfra.NovoJogador(701 + i * 2);
            ctx.Jogadores.AddRange(j1, j2);
            // A primeira dupla da outra categoria não pode no sábado inteiro.
            ctx.Duplas.Add(new Dupla { Categoria = outra, Jogador1 = j1, Jogador2 = j2,
                ImpedimentoSabadoManha = i == 0, ImpedimentoSabadoTarde = i == 0 });
        }
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);
        var vm = (Padelizou.ViewModels.PlanejamentoDeQuadrasVM)Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(
            await TestInfra.NovoTorneiosController(ctx, org.Id).Planejamento(torneio.Id)).Model!;
        Assert.Equal(0, vm.Plano.Faltam);
        await TestInfra.NovoTorneiosController(ctx, org.Id).AprovarChaves(torneio.Id, avisarJogadores: false);

        int comHora = ctx.Partidas.Count(p => p.TorneioId == torneio.Id && p.HorarioPrevisto != null)
            + ctx.ReservasDeHorario.Count(r => r.Categoria.TorneioId == torneio.Id);
        Assert.Equal(vm.JogosDoTorneio, comHora);
        NenhumDepoisDoFim(ctx, torneio);
    }

    // NENHUM JOGO MARCADO NUM HORÁRIO SEM QUADRA ABERTA. A única quadra fecha sábado 14h (janela
    // "disponível até") e o expediente vai até 19h: no último recurso o motor marcava o jogo às
    // 19h SEM quadra — um horário em que não existe onde jogar. Achado pela varredura (11/11/2026).
    [Fact]
    public async Task Ultimo_recurso_nunca_marca_jogo_sem_quadra_aberta()
    {
        // Quinta 18h–22:10 (6 horários) + sexta até 14h com a quadra fechando às 14h: pouco espaço,
        // pra o último recurso entrar em ação.
        var (ctx, torneio, org) = await TorneioAsync(9, Quinta.AddDays(1), "2026-11-13=10:00-19:00");
        var quadra = ctx.Quadras.Single(q => q.TorneioId == torneio.Id);
        quadra.DisponivelDe = Quinta.AddHours(18);
        quadra.DisponivelAte = Quinta.AddDays(1).AddHours(10);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var semQuadra = ctx.Partidas.AsNoTracking()
            .Where(p => p.TorneioId == torneio.Id && p.HorarioPrevisto != null && p.NomeQuadra == null).ToList();
        Assert.True(semQuadra.Count == 0,
            "jogo marcado sem quadra: " + string.Join(", ", semQuadra.Select(p => p.HorarioPrevisto!.Value.ToString("ddd HH:mm"))));
    }

    // O PLANEJAMENTO DIZ "CABE" PELA GRADE DE VERDADE, NÃO SÓ PELA SOMA DE VAGAS. Depois do sorteio
    // existe a grade: se algum jogo (ou jogo previsto do mata-mata) ficou sem hora, não coube —
    // mesmo que vagas ≥ jogos. A soma não enxerga que semifinal espera quartas e final espera
    // semifinal: com 2 quadras, a final roda sozinha e a outra quadra fica parada. Achado pela
    // varredura (11/11/2026): "Cabe" na tela e jogo "por ordem" na grade.
    [Fact]
    public async Task Depois_do_sorteio_jogo_sem_hora_na_grade_e_nao_cabe()
    {
        // 2 quadras; quinta 18:00–22:10 (6 rodadas) + sexta só 18:00 (1 rodada) = 14 vagas.
        var (ctx, torneio, org) = await TorneioAsync(9, Quinta.AddDays(1), "2026-11-13=18:00-18:00");
        torneio.HorariosPorDia = "2026-11-13=18:00-18:10";
        torneio.QuantidadeQuadras = 2;
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra B" });
        await ctx.SaveChangesAsync();
        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var vm = (Padelizou.ViewModels.PlanejamentoDeQuadrasVM)Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(
            await TestInfra.NovoTorneiosController(ctx, org.Id).Planejamento(torneio.Id)).Model!;

        Assert.True(vm.JogosSemHorario > 0, "a grade tem jogo sem hora e a tela não disse");

        var tela = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "Padelizou", "Views", "Torneios", "Planejamento.cshtml"));
        Assert.Contains("else if (Model.JogosSemHorario > 0)", tela);
    }
}
