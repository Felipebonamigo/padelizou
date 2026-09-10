using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// A FIAÇÃO DO BOTÃO "CONFERIR A GRADE" (09/09/2026). A régua está em AuditoriaDaGradeTests;
// aqui é quem pode abrir, e a tela estar no lugar certo.
public class BotaoConferirGradeTests
{
    [Fact]
    public async Task Organizador_abre_a_conferencia()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2);

        var resultado = await TestInfra.NovoTorneiosController(ctx, org.Id).ConferirGrade(torneio.Id);

        var view = Assert.IsType<ViewResult>(resultado);
        Assert.IsAssignableFrom<IEnumerable<AuditoriaDaGrade.Achado>>(view.Model);
    }

    // ⚠️ A auditoria mostra NOME de jogador e a grade inteira — é tela de quem organiza. Regra 0
    // do CLAUDE.md: não é o gate de acesso antecipado que resolve isso.
    [Fact]
    public async Task Quem_nao_organiza_nao_abre()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2);
        var intruso = new Jogador { Nome = "Intruso", Cpf = "99900000011" };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoTorneiosController(ctx, intruso.Id).ConferirGrade(torneio.Id);

        Assert.IsType<ForbidResult>(resultado);
    }

    [Fact]
    public async Task Torneio_que_nao_existe_da_NotFound()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);

        Assert.IsType<NotFoundResult>(
            await TestInfra.NovoTorneiosController(ctx, org.Id).ConferirGrade(99999));
    }

    // O achado tem que chegar na tela de verdade — não adianta a régua estar certa e a ação
    // devolver lista vazia.
    [Fact]
    public async Task Uma_grade_furada_chega_com_achado_na_tela()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2);
        torneio.DataInicio = new DateTime(2026, 10, 9, 18, 0, 0);   // sexta

        var duplas = await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).OrderBy(d => d.Id).ToListAsync();
        duplas[0].ImpedimentoSabadoManha = true;

        ctx.Partidas.Add(new Partida
        {
            TorneioId = torneio.Id, CategoriaId = categoria.Id, Codigo = "P1", Fase = "Grupo A",
            Dupla1Id = duplas[0].Id, Dupla2Id = duplas[1].Id, Status = "Agendada",
            HorarioPrevisto = new DateTime(2026, 10, 10, 9, 0, 0),   // sábado de manhã
        });
        await ctx.SaveChangesAsync();

        var view = Assert.IsType<ViewResult>(
            await TestInfra.NovoTorneiosController(ctx, org.Id).ConferirGrade(torneio.Id));

        var achados = Assert.IsAssignableFrom<IEnumerable<AuditoriaDaGrade.Achado>>(view.Model).ToList();
        Assert.Contains(achados, a => a.Regra == AuditoriaDaGrade.Impedimento);
    }

    // ⚠️ POR QUE TESTE DE FONTE: a suíte não renderiza Razor. O botão só existe na view.
    // ⚠️ 🗣️ Felipe, 09/09/2026, num print do `dev`: *"como que tem jogo dia 15, no torneio do er?
    // se termina dia 13? […] por que esse erro?"*. A pergunta é "POR QUÊ", e até aqui o
    // "Conferir grade" só sabia dizer QUAIS jogos passaram do prazo — nunca a causa.
    //
    // A causa mora em Services/PorQueNaoCoube, e ela já era mostrada no sorteio. Só que o sorteio
    // é um instante que passa: o organizador olha a grade DEPOIS, nesta tela, e é aqui que a
    // pergunta nasce.
    [Fact]
    public async Task A_conferencia_diz_POR_QUE_o_torneio_nao_coube_no_prazo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);

        torneio.DataInicio = new DateTime(2026, 9, 12, 8, 0, 0);
        torneio.DataFim = new DateTime(2026, 9, 13);
        torneio.HoraInicioDoDia = new TimeSpan(8, 0, 0);
        torneio.HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0);
        torneio.HoraFimDoDia = new TimeSpan(23, 0, 0);
        torneio.QuantidadeQuadras = 2;
        torneio.TempoPrevistoPartidaMinutos = 50;

        // O dígito errado: uma quadra cadastrada pra DEPOIS do fim do torneio. É a hipótese do
        // print do Er, e é o que faz a grade escorregar pra um dia que o torneio não tem.
        ctx.Quadras.Add(new Quadra
        {
            TorneioId = torneio.Id,
            Nome = "Radar 1",
            DisponivelDe = new DateTime(2026, 9, 15, 19, 0, 0),
            DisponivelAte = new DateTime(2026, 9, 15, 23, 0, 0),
        });
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoTorneiosController(ctx, org.Id).ConferirGrade(torneio.Id);

        var view = Assert.IsType<ViewResult>(resultado);
        var motivos = Assert.IsAssignableFrom<IEnumerable<string>>(view.ViewData["PorQueNaoCoube"]!).ToList();

        Assert.Contains(motivos, m => m.Contains("Radar 1") && m.Contains("15/09"));
    }

    // Fica na LISTA DE JOGOS, com quem opera o dia — conferir a grade não muda nada, e é
    // justamente ali que o furo aparece. O "Recalcular horários" é que mudou de tela em
    // 10/09/2026 (ver RecalcularHorariosNoPainelTests).
    [Fact]
    public void O_conferir_a_grade_fica_na_lista_de_jogos()
    {
        var fonte = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "_JogosDoTorneio.cshtml"));

        Assert.Contains("asp-action=\"ConferirGrade\"", fonte);
    }

    // 🗣️ Felipe, 10/09/2026, depois de uma noite trocando horário na mão: *"crie um aviso, quando
    // clicar no recalcular horarios, para avisar q se clicar ali, ire gerar todos horarios novos e
    // o que foi feito sera perdido"*. O aviso já existia e dizia o que o botão FAZ; faltava dizer o
    // que ele DESFAZ.
    [Fact]
    public void O_recalcular_horarios_avisa_que_os_ajustes_na_mao_se_perdem()
    {
        // Mora no Painel de Controle desde 10/09/2026 — ver RecalcularHorariosNoPainelTests.
        var fonte = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));

        var formulario = fonte.Substring(fonte.IndexOf("asp-action=\"RefazerGrade\""));
        var aviso = formulario.Substring(0, formulario.IndexOf("</form>"));

        Assert.Contains("data-confirmar=", aviso);
        Assert.Contains("perdid", aviso);          // "será perdida" / "serão perdidos"
        Assert.Contains("na mão", aviso);
    }

    // 🗣️ *"o botão recalcular horarios pode ficar em vermelho tambem, para mostrar que é perigoso,
    // por que ele reorganiza todas as chaves"* e, apontando o OK do modal, *"esse botão em vermelho"*.
    [Fact]
    public void O_recalcular_horarios_e_vermelho_na_tela_e_no_modal()
    {
        var fonte = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));

        var formulario = fonte.Substring(fonte.IndexOf("asp-action=\"RefazerGrade\""));
        formulario = formulario.Substring(0, formulario.IndexOf("</form>"));

        Assert.Contains("btn-danger", formulario);                       // o botão da tela
        Assert.Contains("data-confirmar-tom=\"perigo\"", formulario);    // o OK do modal
    }

    // O botão manso é o que sobrou na lista de jogos, e é o que se aperta primeiro.
    [Fact]
    public void O_ajustar_horarios_fica_na_lista_de_jogos()
    {
        var fonte = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "_JogosDoTorneio.cshtml"));

        Assert.Contains("asp-action=\"AjustarHorarios\"", fonte);
        // Sem `data-confirmar`: ele não joga nada fora, então não precisa pedir licença.
        var formulario = fonte.Substring(fonte.IndexOf("asp-action=\"AjustarHorarios\""));
        formulario = formulario.Substring(0, formulario.IndexOf("</form>"));
        Assert.DoesNotContain("data-confirmar", formulario);
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a pasta do projeto Padelizou.");
    }
}
