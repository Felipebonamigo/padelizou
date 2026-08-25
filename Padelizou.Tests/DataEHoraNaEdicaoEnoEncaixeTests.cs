using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// EDITAR AULA e ENCAIXAR REPOSIÇÃO recebem data e hora SEPARADAS, como a tela de Adicionar
// desde 25/08/2026 — pedido do Felipe: "aplica o mesmo nas telas de editar aula e encaixe".
//
// ⚠️ O QUE ESTES TESTES PRENDEM É A FRONTEIRA DE CULTURA, e ela é silenciosa dos dois lados:
// `<input type="date">` manda SEMPRE "yyyy-MM-dd", o app roda em pt-BR (Program.cs), e um
// binder de `DateTime` leria "2026-08-18" como dia 18 do mês 2026 — que não existe — e cairia
// em 01/01/0001 sem reclamar de nada. A aula iria pro ano 1. Por isso o parsing é invariante
// e explícito, em Services/DataEHoraDoFormulario.
public class DataEHoraNaEdicaoEnoEncaixeTests
{
    private static (DbPadelContext ctx, Jogador professor, LocalAula local) Montar()
    {
        var ctx = TestInfra.NovoContexto();

        var professor = new Jogador { Nome = "Jonatas", Login = "jonatas", Cpf = "99900000051", IsProfessor = true };
        ctx.Jogadores.Add(professor);
        ctx.SaveChanges();

        var local = new LocalAula { ProfessorId = professor.Id, Nome = "Batata Padel", PrecoPadrao = 110, Ativo = true };
        ctx.LocaisAula.Add(local);
        ctx.SaveChanges();

        return (ctx, professor, local);
    }

    private static Aula Lancar(DbPadelContext ctx, Jogador professor, LocalAula local, DateTime quando,
        string status = "Confirmada")
    {
        var aula = new Aula
        {
            ProfessorId = professor.Id, LocalAulaId = local.Id, NomeAlunoAvulso = "Medina",
            DataHora = quando, DuracaoMinutos = 60, Preco = 110, Status = status,
            QuantidadeAlunos = 1,
        };
        ctx.Aulas.Add(aula);
        ctx.SaveChanges();
        return aula;
    }

    // ─── Editar ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Editar_junta_a_data_e_a_hora_no_horario_certo()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var aula = Lancar(ctx, professor, local, DateTime.Today.AddDays(2).AddHours(9));

        await TestInfra.NovoAulasController(ctx, professor.Id)
            .Editar(aula.Id, local.Id, data: "2026-09-15", hora: "14:30", preco: 110);

        var salva = await ctx.Aulas.FindAsync(aula.Id);
        Assert.Equal(new DateTime(2026, 9, 15, 14, 30, 0), salva!.DataHora);
    }

    [Fact]
    public async Task Editar_aceita_a_hora_com_segundos_que_alguns_navegadores_mandam()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var aula = Lancar(ctx, professor, local, DateTime.Today.AddDays(2).AddHours(9));

        await TestInfra.NovoAulasController(ctx, professor.Id)
            .Editar(aula.Id, local.Id, data: "2026-09-15", hora: "14:30:00", preco: 110);

        Assert.Equal(new DateTime(2026, 9, 15, 14, 30, 0), (await ctx.Aulas.FindAsync(aula.Id))!.DataHora);
    }

    [Fact]
    public async Task Editar_sem_data_nao_mexe_na_aula_e_avisa()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var original = DateTime.Today.AddDays(2).AddHours(9);
        var aula = Lancar(ctx, professor, local, original);

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.Editar(aula.Id, local.Id, data: "", hora: "14:30", preco: 110);

        Assert.Equal(original, (await ctx.Aulas.FindAsync(aula.Id))!.DataHora);
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task Editar_sem_hora_nao_mexe_na_aula()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var original = DateTime.Today.AddDays(2).AddHours(9);
        var aula = Lancar(ctx, professor, local, original);

        await TestInfra.NovoAulasController(ctx, professor.Id)
            .Editar(aula.Id, local.Id, data: "2026-09-15", hora: null, preco: 110);

        Assert.Equal(original, (await ctx.Aulas.FindAsync(aula.Id))!.DataHora);
    }

    // ⚠️ O caso que o binder de DateTime deixava passar CALADO: lida em pt-BR, "2026-09-15"
    // não é 15 de setembro — e "15/09/2026" não é o que o campo manda. Nenhum dos dois pode
    // virar aula salva num dia que ninguém escolheu.
    [Fact]
    public async Task Editar_recusa_data_no_formato_brasileiro_em_vez_de_gravar_dia_errado()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var original = DateTime.Today.AddDays(2).AddHours(9);
        var aula = Lancar(ctx, professor, local, original);

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.Editar(aula.Id, local.Id, data: "15/09/2026", hora: "14:30", preco: 110);

        Assert.Equal(original, (await ctx.Aulas.FindAsync(aula.Id))!.DataHora);
        Assert.NotNull(controller.TempData["Erro"]);
    }

    // ─── Encaixar a reposição ─────────────────────────────────────────────────────────

    private static Aula NaFilaDeReposicao(DbPadelContext ctx, Jogador professor, LocalAula local)
    {
        var aula = Lancar(ctx, professor, local, DateTime.Today.AddDays(-3).AddHours(9),
            status: PoliticaAula.ARecuperar);
        aula.CobrarMesmoFaltando = true;
        ctx.SaveChanges();
        return aula;
    }

    [Fact]
    public async Task Encaixar_junta_a_data_e_a_hora_no_horario_certo()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var aula = NaFilaDeReposicao(ctx, professor, local);

        await TestInfra.NovoAulasController(ctx, professor.Id)
            .Encaixar(aula.Id, local.Id, data: "2026-09-15", hora: "07:00");

        var reposicao = await ctx.Aulas.SingleAsync(a => a.RecuperaAulaId == aula.Id);
        Assert.Equal(new DateTime(2026, 9, 15, 7, 0, 0), reposicao.DataHora);
    }

    [Fact]
    public async Task Encaixar_sem_hora_nao_cria_reposicao_nenhuma()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var aula = NaFilaDeReposicao(ctx, professor, local);

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.Encaixar(aula.Id, local.Id, data: "2026-09-15", hora: "");

        Assert.False(await ctx.Aulas.AnyAsync(a => a.RecuperaAulaId == aula.Id));
        Assert.NotNull(controller.TempData["Erro"]);
    }

    // ⚠️ E a aula ORIGINAL não pode sair da fila por causa de um encaixe que não aconteceu:
    // sem ela na fila, o professor perde o único lugar que lembra que deve essa reposição.
    [Fact]
    public async Task Encaixe_recusado_deixa_a_aula_na_fila_de_reposicao()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var aula = NaFilaDeReposicao(ctx, professor, local);

        await TestInfra.NovoAulasController(ctx, professor.Id)
            .Encaixar(aula.Id, local.Id, data: "amanhã de manhã", hora: "07:00");

        var original = await ctx.Aulas.FindAsync(aula.Id);
        Assert.Equal(PoliticaAula.ARecuperar, original!.Status);
        Assert.False(await ctx.Aulas.AnyAsync(a => a.RecuperaAulaId == aula.Id));
    }
}
