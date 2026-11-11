using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// ADICIONAR UM HORÁRIO NA GRADE SEM MEXER NO CHAVEAMENTO (11/11/2026).
//
// 🗣️ Felipe, com a lista de jogos do Chakra: *"permita aqui adicionar um horário em algum dia, se eu
// quiser, sem alterar o chaveamento — adicionar um horário no meio, no final, e os demais se
// adaptarem"*. Escolhas dele: na hora de adicionar ele escolhe NO MEIO (empurra os seguintes) ou NO
// FIM DO DIA (o dia ganha um horário, ninguém anda); e, se o empurrão passar do limite do dia,
// ESTICAR O DIA (um horário a mais) ou EMPURRAR PRO DIA SEGUINTE.
//
// É ação na mão do organizador — a exceção que ele mesmo abriu pra regra "depois de aprovada a grade
// não muda". Confrontos e ordem nunca mudam; o horário prometido do mata-mata anda junto.
public class InserirHorarioNaGradeTests
{
    private static readonly DateTime Quinta = new(2026, 11, 12);
    private static readonly DateTime Sexta = Quinta.AddDays(1);

    // Quinta 18:00–20:30 (4 horários) e sexta 18:00–22:10 (6), uma quadra, 9 duplas: os 9 jogos de
    // grupo enchem a quinta e entram na sexta; o mata-mata é prévia (promessa) na sexta.
    private static async Task<(DbPadelContext ctx, Torneio torneio, Jogador org)> AprovadoAsync()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, 9);
        torneio.DataInicio = Quinta;
        torneio.DataFim = Sexta.AddDays(1);
        torneio.HoraInicioDoDia = new TimeSpan(18, 0, 0);
        torneio.HoraInicioDiasSeguintes = new TimeSpan(18, 0, 0);
        torneio.HoraFimDoDia = new TimeSpan(22, 10, 0);
        torneio.HorariosPorDia = "2026-11-12=18:00-20:30";
        torneio.QuantidadeQuadras = 1;
        torneio.TempoPrevistoPartidaMinutos = 50;
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra A" });
        await ctx.SaveChangesAsync();
        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id, sortearMesmoSemCaber: true);
        await TestInfra.NovoTorneiosController(ctx, org.Id).AprovarChaves(torneio.Id, avisarJogadores: false);
        ctx.ChangeTracker.Clear();
        return (ctx, await ctx.Torneios.SingleAsync(t => t.Id == torneio.Id), org);
    }

    private static Dictionary<int, DateTime?> Horarios(DbPadelContext ctx, int torneioId) =>
        ctx.Partidas.AsNoTracking().Where(p => p.TorneioId == torneioId).ToDictionary(p => p.Id, p => p.HorarioPrevisto);

    [Fact]
    public async Task No_meio_e_esticando_o_dia_todo_jogo_dali_em_diante_anda_um_horario()
    {
        var (ctx, torneio, org) = await AprovadoAsync();
        var antes = Horarios(ctx, torneio.Id);
        var corte = Quinta.AddHours(18).AddMinutes(50);

        await TestInfra.NovoTorneiosController(ctx, org.Id).InserirHorario(torneio.Id, Quinta, corte.TimeOfDay, "meio", "esticar");

        ctx.ChangeTracker.Clear();
        var depois = Horarios(ctx, torneio.Id);
        foreach (var (id, h) in antes)
        {
            if (h is DateTime hora && hora.Date == Quinta && hora >= corte)
                Assert.Equal(hora.AddMinutes(50), depois[id]);
            else
                Assert.Equal(h, depois[id]);
        }
        // O último jogo da quinta passou das 20:30: a quinta ganhou um horário no fim.
        var gravado = await ctx.Torneios.AsNoTracking().SingleAsync(t => t.Id == torneio.Id);
        Assert.Equal(new TimeSpan(21, 20, 0), HorarioDoDia.Do(gravado).Limite(Quinta));
    }

    [Fact]
    public async Task No_meio_empurrando_pro_dia_seguinte_o_ultimo_da_quinta_abre_a_sexta_e_a_sexta_anda()
    {
        var (ctx, torneio, org) = await AprovadoAsync();
        var antes = Horarios(ctx, torneio.Id);
        var corte = Quinta.AddHours(18).AddMinutes(50);
        var promessasAntes = ctx.ReservasDeHorario.AsNoTracking().ToDictionary(r => (r.CategoriaId, r.Fase, r.Numero), r => r.Horario);
        Assert.NotEmpty(promessasAntes);

        await TestInfra.NovoTorneiosController(ctx, org.Id).InserirHorario(torneio.Id, Quinta, corte.TimeOfDay, "meio", "proximoDia");

        ctx.ChangeTracker.Clear();
        var depois = Horarios(ctx, torneio.Id);
        var cal = HorarioDoDia.Do(await ctx.Torneios.AsNoTracking().SingleAsync(t => t.Id == torneio.Id));
        foreach (var (id, h) in antes)
        {
            if (h is DateTime hora && hora >= corte)
                Assert.Equal(cal.DepoisDe(hora, 50), depois[id]);
            else
                Assert.Equal(h, depois[id]);
        }
        Assert.Contains(depois.Values, h => h == Sexta.AddHours(18));          // quem saiu da quinta abre a sexta
        Assert.Equal(new TimeSpan(20, 30, 0), cal.Limite(Quinta));             // a quinta não esticou
        // A promessa do mata-mata anda junto.
        foreach (var r in ctx.ReservasDeHorario.AsNoTracking())
            Assert.Equal(cal.DepoisDe(promessasAntes[(r.CategoriaId, r.Fase, r.Numero)], 50), r.Horario);
    }

    [Fact]
    public async Task No_fim_do_dia_ninguem_anda_e_o_dia_ganha_um_horario()
    {
        var (ctx, torneio, org) = await AprovadoAsync();
        var antes = Horarios(ctx, torneio.Id);

        await TestInfra.NovoTorneiosController(ctx, org.Id).InserirHorario(torneio.Id, Sexta, null, "fim", null);

        ctx.ChangeTracker.Clear();
        Assert.Equal(antes, Horarios(ctx, torneio.Id));
        var gravado = await ctx.Torneios.AsNoTracking().SingleAsync(t => t.Id == torneio.Id);
        Assert.Equal(new TimeSpan(23, 0, 0), HorarioDoDia.Do(gravado).Limite(Sexta));
    }

    [Fact]
    public async Task Jogo_que_ja_acabou_nao_anda()
    {
        var (ctx, torneio, org) = await AprovadoAsync();
        var primeiro = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).OrderBy(p => p.HorarioPrevisto).FirstAsync();
        await TestInfra.FinalizarComPlacarAsync(ctx, TestInfra.NovoTorneiosController(ctx, org.Id), primeiro, 9, 3);
        var horaDele = primeiro.HorarioPrevisto;

        await TestInfra.NovoTorneiosController(ctx, org.Id).InserirHorario(torneio.Id, Quinta, new TimeSpan(18, 0, 0), "meio", "esticar");

        ctx.ChangeTracker.Clear();
        Assert.Equal(horaDele, (await ctx.Partidas.AsNoTracking().SingleAsync(p => p.Id == primeiro.Id)).HorarioPrevisto);
    }

    [Fact]
    public async Task Empurrar_pra_depois_do_ultimo_dia_e_recusado_e_nada_muda()
    {
        var (ctx, torneio, org) = await AprovadoAsync();
        // O torneio passa a acabar na sexta: empurrar a sexta pro "dia seguinte" sairia do torneio.
        var t = await ctx.Torneios.SingleAsync(x => x.Id == torneio.Id);
        t.DataFim = Sexta;
        await ctx.SaveChangesAsync();
        var antes = Horarios(ctx, torneio.Id);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.InserirHorario(torneio.Id, Sexta, new TimeSpan(18, 0, 0), "meio", "proximoDia");

        ctx.ChangeTracker.Clear();
        Assert.Equal(antes, Horarios(ctx, torneio.Id));
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task So_quem_organiza()
    {
        var (ctx, torneio, _) = await AprovadoAsync();
        var estranho = new Jogador { Nome = "Estranho", Cpf = "12312312399" };
        ctx.Jogadores.Add(estranho);
        await ctx.SaveChangesAsync();

        var r = await TestInfra.NovoTorneiosController(ctx, estranho.Id).InserirHorario(torneio.Id, Quinta, new TimeSpan(18, 0, 0), "meio", "esticar");

        Assert.IsType<ForbidResult>(r);
    }

    [Fact]
    public void A_lista_de_jogos_tem_o_formulario_pro_organizador()
    {
        var tela = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "Padelizou", "Views", "Torneios", "_JogosDoTorneio.cshtml"));
        var form = System.Text.RegularExpressions.Regex.Match(tela, "<form[^>]*asp-action=\"InserirHorario\"[^>]*>");
        Assert.True(form.Success);
        Assert.Contains("data-manter-posicao", form.Value);
        Assert.Contains("name=\"seNaoCouber\" value=\"proximoDia\"", tela);
        Assert.Contains("name=\"modo\" value=\"fim\"", tela);
    }
}
