using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using padelizou.Models;

namespace Padelizou.Tests;

// 13/08/2026 — CORRIGIR E APAGAR UM HORÁRIO DO PROFESSOR.
//
// Até aqui a tela só tinha Desativar/Reativar. Errar a hora (ou o local) no cadastro deixava a
// linha errada lá pra sempre: pausada, ocupando espaço, e sem jeito de virar a linha certa. O
// caminho era criar outra e conviver com as duas — que é como uma lista de horários deixa de
// ser confiável.
public class CorrigirHorarioDoProfessorTests
{
    private static readonly TimeSpan Oito = TimeSpan.FromHours(8);
    private static readonly TimeSpan Meiodia = TimeSpan.FromHours(12);

    private static HorarioDisponivel Horario(
        int id, int local, int dia, TimeSpan inicio, TimeSpan fim, bool ativo = true) =>
        new() { Id = id, LocalAulaId = local, DiaSemana = dia, HoraInicio = inicio, HoraFim = fim, Ativo = ativo };

    // ===================== A RÉGUA =====================

    // ⚠️ O TESTE QUE JUSTIFICA O `Where(h => h.Id != ...)`. Sem tirar o próprio horário da
    // comparação, ele colide CONSIGO MESMO: a tela recusaria tudo, inclusive trocar só a
    // duração — que é o motivo mais comum pra abrir a edição.
    [Fact]
    public void Um_horario_nao_colide_consigo_mesmo()
    {
        var eu = Horario(id: 10, local: 7, dia: 1, Oito, Meiodia);

        var motivo = NovoHorarioDoProfessor.MotivoParaNaoEditar(
            idQueEstaSendoEditado: 10, dia: 1, Oito, Meiodia, duracaoMinutos: 90,
            localAulaId: 7, horariosDoProfessor: new[] { eu });

        Assert.Null(motivo);
    }

    [Fact]
    public void Mover_pra_cima_de_um_horario_que_ja_existe_e_recusado()
    {
        var eu = Horario(id: 10, local: 7, dia: 1, Oito, Meiodia);          // segunda
        var aQuarta = Horario(id: 11, local: 7, dia: 3, Oito, Meiodia);     // quarta, igual

        var motivo = NovoHorarioDoProfessor.MotivoParaNaoEditar(
            10, dia: 3, Oito, Meiodia, 60, localAulaId: 7, new[] { eu, aQuarta });

        Assert.NotNull(motivo);
        Assert.Contains("já tem um horário", motivo);
    }

    // Pausado também é duplicata: a linha continua existindo na tela, e ficariam duas iguais —
    // uma ativa e uma pausada — sem ninguém entender qual manda.
    [Fact]
    public void Mover_pra_cima_de_um_horario_PAUSADO_tambem_e_recusado()
    {
        var eu = Horario(10, 7, 1, Oito, Meiodia);
        var pausado = Horario(11, 7, 3, Oito, Meiodia, ativo: false);

        Assert.NotNull(NovoHorarioDoProfessor.MotivoParaNaoEditar(
            10, dia: 3, Oito, Meiodia, 60, localAulaId: 7, new[] { eu, pausado }));
    }

    // Mesmo dia e mesma hora em OUTRO local é legítimo: o professor atende em dois lugares e o
    // conflito de verdade (estar em dois lugares ao mesmo tempo) não é decidido aqui.
    [Fact]
    public void Mesmo_dia_e_hora_em_outro_local_passa()
    {
        var eu = Horario(10, local: 7, dia: 1, Oito, Meiodia);
        var noOutroLocal = Horario(11, local: 9, dia: 1, Oito, Meiodia);

        Assert.Null(NovoHorarioDoProfessor.MotivoParaNaoEditar(
            10, dia: 1, Oito, Meiodia, 60, localAulaId: 7, new[] { eu, noOutroLocal }));
    }

    // As mesmas duas validações do cadastro, agora na edição — e é de propósito que sejam A
    // MESMA função: uma segunda cópia divergiria na primeira mudança, e o buraco reabriria só
    // aqui. Os dois erros só aparecem PRO ALUNO, como agenda vazia.
    [Fact]
    public void Fim_antes_do_inicio_e_recusado_na_edicao_tambem()
    {
        var eu = Horario(10, 7, 1, Oito, Meiodia);

        var motivo = NovoHorarioDoProfessor.MotivoParaNaoEditar(
            10, dia: 1, Meiodia, Oito, 60, localAulaId: 7, new[] { eu });

        Assert.Equal("O fim precisa ser depois do início.", motivo);
    }

    [Fact]
    public void Aula_que_nao_cabe_na_janela_e_recusada_na_edicao_tambem()
    {
        var eu = Horario(10, 7, 1, Oito, Oito.Add(TimeSpan.FromHours(1)));

        var motivo = NovoHorarioDoProfessor.MotivoParaNaoEditar(
            10, dia: 1, Oito, Oito.Add(TimeSpan.FromHours(1)), duracaoMinutos: 90,
            localAulaId: 7, new[] { eu });

        Assert.NotNull(motivo);
        Assert.Contains("não cabe", motivo);
    }

    // ===================== A TELA =====================

    private static async Task<(Jogador professor, LocalAula local, HorarioDisponivel horario)>
        MontarAsync(DbPadelContext ctx)
    {
        var professor = new Jogador { Nome = "Prof Teste", Cpf = "66600000001", Login = "prof", IsProfessor = true };
        ctx.Jogadores.Add(professor);
        await ctx.SaveChangesAsync();

        var local = new LocalAula { ProfessorId = professor.Id, Nome = "Arena Bento", Ativo = true };
        ctx.LocaisAula.Add(local);
        await ctx.SaveChangesAsync();

        var horario = new HorarioDisponivel
        {
            ProfessorId = professor.Id, LocalAulaId = local.Id,
            DiaSemana = 1, HoraInicio = Oito, HoraFim = Meiodia, DuracaoMinutos = 60, Ativo = true,
        };
        ctx.HorariosDisponiveis.Add(horario);
        await ctx.SaveChangesAsync();

        return (professor, local, horario);
    }

    [Fact]
    public async Task Corrigir_a_hora_salva_e_nao_cria_linha_nova()
    {
        using var ctx = TestInfra.NovoContexto();
        var (professor, local, horario) = await MontarAsync(ctx);

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.EditarHorario(
            horario.Id, local.Id, diaSemana: 2, TimeSpan.FromHours(7), TimeSpan.FromHours(11), 60);

        var salvo = await ctx.HorariosDisponiveis.SingleAsync();
        Assert.Equal(2, salvo.DiaSemana);
        Assert.Equal(TimeSpan.FromHours(7), salvo.HoraInicio);
        Assert.Equal(TimeSpan.FromHours(11), salvo.HoraFim);
    }

    // ⚠️ O MEDO DESTE BOTÃO É DERRUBAR AULA MARCADA — e ele não derruba. `Aula` guarda local +
    // data e hora e NÃO aponta pro HorarioDisponivel: o horário é só a REGRA que abre vaga.
    // Some a regra, some a vaga NOVA; o que já estava marcado continua na agenda.
    [Fact]
    public async Task Apagar_o_horario_NAO_derruba_aula_ja_marcada()
    {
        using var ctx = TestInfra.NovoContexto();
        var (professor, local, horario) = await MontarAsync(ctx);

        ctx.Aulas.Add(new Aula
        {
            ProfessorId = professor.Id, LocalAulaId = local.Id,
            DataHora = new DateTime(2026, 8, 17, 8, 0, 0), // uma segunda, dentro da janela
            Preco = 120m, Status = "Confirmada", DuracaoMinutos = 60,
        });
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.ExcluirHorario(horario.Id);

        Assert.Empty(await ctx.HorariosDisponiveis.ToListAsync());
        Assert.Equal(1, await ctx.Aulas.CountAsync());
    }

    // A trava é o `ProfessorId` DENTRO da consulta: sem ele, trocar o id na URL abriria — e
    // salvaria — o horário de outro professor.
    [Fact]
    public async Task Professor_nao_mexe_no_horario_de_outro()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, local, horario) = await MontarAsync(ctx);

        var intruso = new Jogador { Nome = "Outro Prof", Cpf = "66600000002", Login = "outro", IsProfessor = true };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoAulasController(ctx, intruso.Id);

        await controller.EditarHorario(horario.Id, local.Id, diaSemana: 5, Oito, Meiodia, 60);
        Assert.Equal(1, (await ctx.HorariosDisponiveis.SingleAsync()).DiaSemana);

        await controller.ExcluirHorario(horario.Id);
        Assert.Equal(1, await ctx.HorariosDisponiveis.CountAsync());
    }

    // Um id de local de outro professor entraria por POST montado à mão, e a lista passaria a
    // mostrar um horário num lugar que não é dele.
    [Fact]
    public async Task Nao_da_pra_mover_o_horario_pra_um_local_que_nao_e_meu()
    {
        using var ctx = TestInfra.NovoContexto();
        var (professor, _, horario) = await MontarAsync(ctx);

        var outroProf = new Jogador { Nome = "Outro Prof", Cpf = "66600000003", Login = "outro3", IsProfessor = true };
        ctx.Jogadores.Add(outroProf);
        await ctx.SaveChangesAsync();
        var localAlheio = new LocalAula { ProfessorId = outroProf.Id, Nome = "Quadra alheia", Ativo = true };
        ctx.LocaisAula.Add(localAlheio);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.EditarHorario(horario.Id, localAlheio.Id, 1, Oito, Meiodia, 60);

        Assert.NotEqual(localAlheio.Id, (await ctx.HorariosDisponiveis.SingleAsync()).LocalAulaId);
    }

    [Fact]
    public async Task Edicao_recusada_devolve_o_formulario_com_o_motivo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (professor, local, horario) = await MontarAsync(ctx);

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        var resultado = await controller.EditarHorario(
            horario.Id, local.Id, diaSemana: 1, Meiodia, Oito, 60); // fim antes do início

        var destino = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("EditarHorario", destino.ActionName);
        Assert.NotNull(controller.TempData["ErroHorario"]);
        Assert.Equal(Oito, (await ctx.HorariosDisponiveis.SingleAsync()).HoraInicio);
    }
}
