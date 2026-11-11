using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 11/11/2026 — HORÁRIO PRÓPRIO POR DIA.
//
// 🗣️ Felipe, com o print do Planejamento do THE LAST DANCE (qui 12/11 a dom 15/11): *"aqui tem
// que permitir separar por dia, que horas começa e que horas termina, não estou conseguindo"* ·
// *"faça o horário próprio por dia agora, preciso disso"*.
//
// O torneio só guardava TRÊS horas — abre no 1º dia, abre nos demais, último jogo começa até
// (uma só, pra todos os dias). O "fecha" da tabela Dia a dia era simulação. Agora cada dia pode
// ter a própria faixa (`Torneio.HorariosPorDia`), e o motor inteiro pergunta a UM lugar só
// (Services/HorarioDoDia). Dia sem faixa própria segue os três campos de sempre — e torneio sem
// faixa nenhuma passa EXATAMENTE pelo código de antes.
public class HorarioProprioPorDiaTests
{
    // Qui 12/11/2026 19h → dom 15/11.
    private static readonly DateTime Quinta = new(2026, 11, 12);

    private static Torneio Torneio(string? porDia = null) => new()
    {
        Nome = "THE LAST DANCE", Codigo = "TLD", Status = "Inscrições Abertas",
        DataInicio = Quinta, DataFim = Quinta.AddDays(3),
        HoraInicioDoDia = new TimeSpan(19, 0, 0),
        HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0),
        HoraFimDoDia = new TimeSpan(23, 0, 0),
        TempoPrevistoPartidaMinutos = 50,
        QuantidadeQuadras = 1,
        HorariosPorDia = porDia,
    };

    // ── O texto guardado ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Le_e_escreve_a_faixa_de_cada_dia()
    {
        var texto = "2026-11-13=19:00-22:20;2026-11-15=08:00-14:00";

        var faixas = HorarioDoDia.Ler(texto);

        Assert.Equal(new HorarioDoDia.Faixa(new(19, 0, 0), new(22, 20, 0)), faixas[new DateTime(2026, 11, 13)]);
        Assert.Equal(new HorarioDoDia.Faixa(new(8, 0, 0), new(14, 0, 0)), faixas[new DateTime(2026, 11, 15)]);
        Assert.Equal(texto, HorarioDoDia.Escrever(faixas));
    }

    [Fact]
    public void Lixo_e_faixa_que_fecha_antes_de_abrir_sao_ignorados()
    {
        var faixas = HorarioDoDia.Ler("2026-11-13=lixo;2026-11-14=20:00-19:00;nada;2026-11-15=08:00-14:00");

        Assert.Single(faixas);
        Assert.Null(HorarioDoDia.Escrever(new Dictionary<DateTime, HorarioDoDia.Faixa>()));
    }

    // ── A régua de cada dia ───────────────────────────────────────────────────────────────

    [Fact]
    public void Dia_com_faixa_propria_usa_ela_e_os_outros_seguem_os_tres_campos()
    {
        var cal = HorarioDoDia.Do(Torneio("2026-11-13=19:00-22:20"));

        Assert.Equal(new TimeSpan(19, 0, 0), cal.Abre(Quinta));               // 1º dia: HoraInicioDoDia
        Assert.Equal(new TimeSpan(23, 0, 0), cal.Limite(Quinta));
        Assert.Equal(new TimeSpan(19, 0, 0), cal.Abre(Quinta.AddDays(1)));    // sexta: a faixa própria
        Assert.Equal(new TimeSpan(22, 20, 0), cal.Limite(Quinta.AddDays(1)));
        Assert.Equal(new TimeSpan(8, 0, 0), cal.Abre(Quinta.AddDays(2)));     // sábado: HoraInicioDiasSeguintes
        Assert.Equal(new TimeSpan(23, 0, 0), cal.Limite(Quinta.AddDays(2)));
    }

    [Fact]
    public void A_abertura_da_grade_respeita_a_faixa_do_primeiro_dia()
    {
        var torneio = Torneio("2026-11-12=20:00-23:00");

        Assert.Equal(Quinta.AddHours(20), torneio.AberturaDaGrade);
    }

    // ── A grade ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sem_faixa_nenhuma_a_grade_e_exatamente_a_de_antes()
    {
        var torneio = Torneio();
        var inicio = torneio.AberturaDaGrade;

        var antes = GradeDeJogos.Horarios(inicio, torneio.HoraFimDoDia, 2, 50, 60, torneio.HoraInicioDiasSeguintes).ToList();
        var agora = HorarioDoDia.Do(torneio).Horarios(inicio, 2, 50, 60).ToList();

        Assert.Equal(antes, agora);
        Assert.Equal(GradeDeJogos.DepoisDe(antes[^1], torneio.HoraFimDoDia, torneio.HoraInicioDiasSeguintes, 50),
            HorarioDoDia.Do(torneio).DepoisDe(antes[^1], 50));
    }

    [Fact]
    public void A_grade_vira_o_dia_em_cada_faixa_propria()
    {
        // Qui 19:00–21:30, sex 19:00–20:40. 1 quadra, jogos de 50 min.
        var torneio = Torneio("2026-11-12=19:00-21:30;2026-11-13=19:00-20:40");

        var grade = HorarioDoDia.Do(torneio).Horarios(torneio.AberturaDaGrade, 1, 50, 7).ToList();

        Assert.Equal(new[]
        {
            // "Último jogo começa até" INCLUI a hora: o das 21:30 de quinta entra.
            Quinta.AddHours(19), Quinta.Add(new TimeSpan(19, 50, 0)), Quinta.Add(new TimeSpan(20, 40, 0)), Quinta.Add(new TimeSpan(21, 30, 0)),
            Quinta.AddDays(1).AddHours(19), Quinta.AddDays(1).Add(new TimeSpan(19, 50, 0)), Quinta.AddDays(1).Add(new TimeSpan(20, 40, 0)),
        }, grade);
    }

    [Fact]
    public void Depois_do_ultimo_jogo_do_dia_vem_a_abertura_do_dia_seguinte_dele()
    {
        var cal = HorarioDoDia.Do(Torneio("2026-11-13=19:00-22:20"));

        // Último jogo de quinta às 23:00 → sexta abre às 19:00 (a faixa dela), não às 08:00.
        Assert.Equal(Quinta.AddDays(1).AddHours(19), cal.DepoisDe(Quinta.AddHours(23), 50));
        // Sexta às 22:20 → sábado às 08:00.
        Assert.Equal(Quinta.AddDays(2).AddHours(8), cal.DepoisDe(Quinta.AddDays(1).Add(new TimeSpan(22, 20, 0)), 50));
    }

    [Fact]
    public void O_sorteio_so_oferece_vaga_dentro_da_faixa_de_cada_dia()
    {
        var torneio = Torneio("2026-11-13=19:00-22:20;2026-11-14=09:00-12:20");
        torneio.QuantidadeQuadras = 2;

        var vagas = VagasDaGrade.Montar(torneio, torneio.AberturaDaGrade, 30);

        foreach (var h in vagas)
        {
            var cal = HorarioDoDia.Do(torneio);
            Assert.InRange(h.TimeOfDay, cal.Abre(h.Date), cal.Limite(h.Date));
        }
        Assert.Contains(vagas, h => h.Date == Quinta.AddDays(1) && h.TimeOfDay == new TimeSpan(19, 0, 0));
        Assert.DoesNotContain(vagas, h => h.Date == Quinta.AddDays(1) && h.TimeOfDay < new TimeSpan(19, 0, 0));
    }

    // ── O planejamento ────────────────────────────────────────────────────────────────────

    [Fact]
    public void O_dia_a_dia_do_planejamento_usa_a_abertura_e_o_fechamento_de_cada_dia()
    {
        var plano = PlanejamentoDeQuadras.Montar(
            inicio: Quinta.AddHours(19), aberturaDiasSeguintes: new TimeSpan(8, 0, 0),
            limitePadrao: new TimeSpan(23, 0, 0), quadras: 1, duracaoMinutos: 50, totalDeJogos: 0,
            ate: Quinta.AddDays(3),
            limitesPorDia: new Dictionary<DateTime, TimeSpan> { [Quinta.AddDays(1)] = new(22, 20, 0) },
            aberturasPorDia: new Dictionary<DateTime, TimeSpan> { [Quinta.AddDays(1)] = new(19, 0, 0) });

        var sexta = plano.Dias.Single(d => d.Data == Quinta.AddDays(1));
        Assert.Equal(new TimeSpan(19, 0, 0), sexta.Abre);
        Assert.Equal(new TimeSpan(22, 20, 0), sexta.Limite);
        Assert.Equal(5, sexta.Rodadas);   // 19:00, 19:50, 20:40, 21:30, 22:20
    }

    private static async Task<(DbPadelContext ctx, Torneio torneio, Jogador organizador)> MontarAsync()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0, status: "Inscrições Abertas");
        torneio.DataInicio = Quinta;
        torneio.DataFim = Quinta.AddDays(3);
        torneio.HoraInicioDoDia = new TimeSpan(19, 0, 0);
        torneio.HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0);
        torneio.HoraFimDoDia = new TimeSpan(23, 0, 0);
        await ctx.SaveChangesAsync();
        return (ctx, torneio, organizador);
    }

    [Fact]
    public async Task Aplicar_grava_a_faixa_de_cada_dia_no_torneio()
    {
        var (ctx, torneio, organizador) = await MontarAsync();

        var r = await TestInfra.NovoTorneiosController(ctx, organizador.Id).AplicarPlanejamento(torneio.Id,
            aberturas: "2026-11-13=19:00;2026-11-15=08:00", limites: "2026-11-13=22:20;2026-11-15=14:00");

        Assert.IsType<RedirectToActionResult>(r);
        ctx.ChangeTracker.Clear();
        var gravado = await ctx.Torneios.SingleAsync(t => t.Id == torneio.Id);
        Assert.Equal("2026-11-13=19:00-22:20;2026-11-15=08:00-14:00", gravado.HorariosPorDia);
    }

    [Fact]
    public async Task Dia_igual_ao_padrao_nao_vira_faixa_propria()
    {
        // Só o fechamento do domingo mudou: a sexta, que veio com os valores de sempre, não é
        // gravada — senão mudar o padrão depois não a alcançaria.
        var (ctx, torneio, organizador) = await MontarAsync();

        await TestInfra.NovoTorneiosController(ctx, organizador.Id).AplicarPlanejamento(torneio.Id,
            aberturas: "2026-11-13=08:00", limites: "2026-11-13=23:00;2026-11-15=14:00");

        ctx.ChangeTracker.Clear();
        Assert.Equal("2026-11-15=08:00-14:00", (await ctx.Torneios.SingleAsync(t => t.Id == torneio.Id)).HorariosPorDia);
    }

    [Fact]
    public async Task Dia_que_fecha_antes_de_abrir_e_recusado()
    {
        var (ctx, torneio, organizador) = await MontarAsync();
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        await controller.AplicarPlanejamento(torneio.Id, aberturas: "2026-11-13=21:00", limites: "2026-11-13=20:00");

        Assert.NotNull(controller.TempData["Erro"]);
        ctx.ChangeTracker.Clear();
        Assert.Null((await ctx.Torneios.SingleAsync(t => t.Id == torneio.Id)).HorariosPorDia);
    }

    [Fact]
    public async Task Quem_nao_organiza_nao_aplica()
    {
        var (ctx, torneio, _) = await MontarAsync();
        var estranho = TestInfra.NovoJogador(77);
        ctx.Jogadores.Add(estranho);
        ctx.SaveChanges();

        var r = await TestInfra.NovoTorneiosController(ctx, estranho.Id).AplicarPlanejamento(torneio.Id,
            aberturas: "2026-11-13=19:00", limites: "2026-11-13=22:20");

        Assert.IsType<ForbidResult>(r);
    }

    // ── Depois do sorteio: recalcula ─────────────────────────────────────────────────────
    // 🗣️ Felipe: *"que eu possa alterar mesmo depois de chaves prontas e ele recalcule"*. Com a
    // chave sorteada e AINDA NÃO PÚBLICA, aplicar refaz a grade dos jogos que não começaram — o
    // mesmo RecalcularAGradeAsync do "Recalcular horários". Depois de PÚBLICA, vale a regra dele
    // de 10/10 (GradePublicadaNaoSeRefazTests): o horário publicado é compromisso, e a faixa nova
    // só é gravada.

    private static async Task<(DbPadelContext ctx, Torneio torneio, Jogador org)> SorteadoAsync(string? status = null)
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, 9);
        torneio.DataInicio = Quinta;
        torneio.DataFim = Quinta.AddDays(3);
        torneio.HoraInicioDoDia = new TimeSpan(19, 0, 0);
        torneio.HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0);
        torneio.HoraFimDoDia = new TimeSpan(23, 0, 0);
        torneio.QuantidadeQuadras = 2;
        torneio.TempoPrevistoPartidaMinutos = 50;
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra 1" });
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra 2" });
        await ctx.SaveChangesAsync();
        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        // Todos às 9h de sexta — fora da faixa que vai ser aplicada. Recalcular tira daí.
        foreach (var jogo in ctx.Partidas.Where(p => p.TorneioId == torneio.Id))
            jogo.HorarioPrevisto = Quinta.AddDays(1).AddHours(9);
        if (status != null) torneio.Status = status;
        await ctx.SaveChangesAsync();
        return (ctx, torneio, org);
    }

    [Fact]
    public async Task Aplicar_com_a_chave_sorteada_recalcula_os_jogos_pela_faixa_de_cada_dia()
    {
        var (ctx, torneio, org) = await SorteadoAsync();
        Assert.False(AprovacaoDeChaves.ChavePublicada(torneio));

        // Quinta só das 19h às 19h50 (2 rodadas) e sexta das 19h às 22h20: o resto transborda
        // pra sexta à noite, e nunca pra manhã de sexta.
        await TestInfra.NovoTorneiosController(ctx, org.Id).AplicarPlanejamento(torneio.Id,
            aberturas: "2026-11-12=19:00;2026-11-13=19:00", limites: "2026-11-12=19:50;2026-11-13=22:20");

        ctx.ChangeTracker.Clear();
        var gravado = await ctx.Torneios.SingleAsync(t => t.Id == torneio.Id);
        var cal = HorarioDoDia.Do(gravado);
        var jogos = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id && p.HorarioPrevisto != null).ToListAsync();

        Assert.NotEmpty(jogos);
        Assert.DoesNotContain(jogos, j => j.HorarioPrevisto == Quinta.AddDays(1).AddHours(9));
        Assert.All(jogos, j => Assert.InRange(j.HorarioPrevisto!.Value.TimeOfDay,
            cal.Abre(j.HorarioPrevisto.Value.Date), cal.Limite(j.HorarioPrevisto.Value.Date)));
        Assert.Contains(jogos, j => j.HorarioPrevisto!.Value.Date == Quinta.AddDays(1));
    }

    // 🗣️ Felipe, 11/11/2026, com a tela do torneio já publicado: *"não está salvando ali a
    // alteração e refazendo os horários"*. Pro APLICAR DA FAIXA DO DIA, a regra de 10/10 cede:
    // é a decisão explícita do organizador de mudar o expediente, não um "refazer" acidental.
    // RefazerGrade e Ajustar continuam travados (GradePublicadaNaoSeRefazTests).
    [Fact]
    public async Task Com_a_chave_publica_aplicar_a_faixa_tambem_recalcula()
    {
        var (ctx, torneio, org) = await SorteadoAsync(status: "Fase de Grupos");
        Assert.True(AprovacaoDeChaves.ChavePublicada(torneio));

        await TestInfra.NovoTorneiosController(ctx, org.Id).AplicarPlanejamento(torneio.Id,
            aberturas: "2026-11-12=19:00;2026-11-13=19:00", limites: "2026-11-12=19:50;2026-11-13=22:20");

        ctx.ChangeTracker.Clear();
        var gravado = await ctx.Torneios.SingleAsync(t => t.Id == torneio.Id);
        var cal = HorarioDoDia.Do(gravado);
        var jogos = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id && p.HorarioPrevisto != null).ToListAsync();

        Assert.NotEmpty(jogos);
        Assert.DoesNotContain(jogos, j => j.HorarioPrevisto == Quinta.AddDays(1).AddHours(9));
        Assert.All(jogos, j => Assert.InRange(j.HorarioPrevisto!.Value.TimeOfDay,
            cal.Abre(j.HorarioPrevisto.Value.Date), cal.Limite(j.HorarioPrevisto.Value.Date)));
    }

    // O APLICAR GRAVA O QUE ESTÁ NA TABELA AGORA. Antes ele mandava o que a tela tinha ao ABRIR
    // (campos escondidos), e mudar uma caixinha e clicar em Aplicar gravava o valor velho.
    // A tabela manda `abertura[dia]`/`limite[dia]`; o Aplicar aceita os mesmos nomes.
    [Fact]
    public async Task Aplicar_aceita_as_caixinhas_da_tabela_como_elas_estao()
    {
        var (ctx, torneio, org) = await SorteadoAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).AplicarPlanejamento(torneio.Id,
            abertura: new Dictionary<string, string> { ["2026-11-12"] = "", ["2026-11-13"] = "18:00" },
            limite: new Dictionary<string, string> { ["2026-11-12"] = "22:10", ["2026-11-13"] = "" });

        ctx.ChangeTracker.Clear();
        Assert.Equal("2026-11-12=19:00-22:10;2026-11-13=18:00-23:00",
            (await ctx.Torneios.SingleAsync(t => t.Id == torneio.Id)).HorariosPorDia);
    }

    [Fact]
    public void A_tela_deixa_editar_abre_e_fecha_por_dia_e_aplica_os_dois()
    {
        var tela = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "Padelizou", "Views", "Torneios", "Planejamento.cshtml"));

        Assert.Contains("name=\"abertura[", tela);
        Assert.Contains("name=\"limite[", tela);
        Assert.Contains("name=\"aberturas\"", tela);
        Assert.Contains("name=\"limites\"", tela);
        Assert.DoesNotContain("vale <strong>só aqui</strong>", tela);

        // O Aplicar leva os campos da simulação COMO ESTÃO (o script copia na hora de enviar),
        // e não fica apagado esperando um "Recalcular" que o organizador não sabe que precisa.
        Assert.Contains("id=\"simulacao-do-planejamento\"", tela);
        Assert.Contains("getElementById('simulacao-do-planejamento')", tela);
        Assert.DoesNotContain("disabled=\"@(!Model.MudouAlgo)\"", tela);
    }

    [Fact]
    public void A_proxima_edicao_nao_herda_os_dias_da_edicao_passada()
    {
        Assert.DoesNotContain(nameof(Padelizou.Models.Torneio.HorariosPorDia), DuplicacaoDeTorneio.Copiadas);
    }
}
