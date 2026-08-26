using Microsoft.AspNetCore.Mvc;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using Xunit;

namespace Padelizou.Tests;

// Pedido do Felipe (25/08/2026): "João, por exemplo, dá aula de tênis e beach tênis também.
// Seria legal um campo pra isso. Acho que é uma realidade dos professores." Diferente de
// Aula.Esporte (o esporte DAQUELA aula), isto é uma declaração no PERFIL do professor —
// mostrada como selo na página pública, editada só por ele mesmo.
public class ProfessorEnsinaVariosEsportesTests
{
    private static Jogador NovoProfessor(DbPadelContext ctx, string nome = "João", string cpf = "88800000001")
    {
        var professor = new Jogador { Nome = nome, Cpf = cpf, IsProfessor = true };
        ctx.Jogadores.Add(professor);
        ctx.SaveChanges();
        return professor;
    }

    // ---- Salvar ----

    [Fact]
    public async Task Salvar_grava_os_esportes_marcados()
    {
        using var ctx = TestInfra.NovoContexto();
        var professor = NovoProfessor(ctx);

        await TestInfra.NovoProfessoresController(ctx, professor.Id)
            .SalvarEsportes(new List<string> { EsporteDaAula.Padel, EsporteDaAula.Tenis });

        var salvos = ctx.ProfessorEsportes.Where(pe => pe.ProfessorId == professor.Id).Select(pe => pe.Esporte).ToList();
        Assert.Equal(2, salvos.Count);
        Assert.Contains(EsporteDaAula.Padel, salvos);
        Assert.Contains(EsporteDaAula.Tenis, salvos);
    }

    [Fact]
    public async Task Esporte_fora_da_lista_e_ignorado()
    {
        using var ctx = TestInfra.NovoContexto();
        var professor = NovoProfessor(ctx);

        await TestInfra.NovoProfessoresController(ctx, professor.Id)
            .SalvarEsportes(new List<string> { EsporteDaAula.Padel, "Vôlei" });

        var salvos = ctx.ProfessorEsportes.Where(pe => pe.ProfessorId == professor.Id).Select(pe => pe.Esporte).ToList();
        Assert.Equal(new[] { EsporteDaAula.Padel }, salvos);
    }

    [Fact]
    public async Task Salvar_de_novo_substitui_em_vez_de_acumular()
    {
        using var ctx = TestInfra.NovoContexto();
        var professor = NovoProfessor(ctx);

        var controller = TestInfra.NovoProfessoresController(ctx, professor.Id);
        await controller.SalvarEsportes(new List<string> { EsporteDaAula.Padel, EsporteDaAula.Tenis });
        await controller.SalvarEsportes(new List<string> { EsporteDaAula.BeachTenis });

        var salvos = ctx.ProfessorEsportes.Where(pe => pe.ProfessorId == professor.Id).Select(pe => pe.Esporte).ToList();
        Assert.Equal(new[] { EsporteDaAula.BeachTenis }, salvos);
    }

    [Fact]
    public async Task Salvar_sem_nenhum_marcado_esvazia()
    {
        using var ctx = TestInfra.NovoContexto();
        var professor = NovoProfessor(ctx);

        var controller = TestInfra.NovoProfessoresController(ctx, professor.Id);
        await controller.SalvarEsportes(new List<string> { EsporteDaAula.Padel });
        await controller.SalvarEsportes(null);

        Assert.Empty(ctx.ProfessorEsportes.Where(pe => pe.ProfessorId == professor.Id));
    }

    [Fact]
    public async Task Quem_nao_e_professor_nao_grava_nada()
    {
        using var ctx = TestInfra.NovoContexto();
        var jogadorComum = new Jogador { Nome = "Aluno", Cpf = "88800000002", IsProfessor = false };
        ctx.Jogadores.Add(jogadorComum);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoProfessoresController(ctx, jogadorComum.Id)
            .SalvarEsportes(new List<string> { EsporteDaAula.Padel });

        Assert.IsType<ForbidResult>(resultado);
        Assert.Empty(ctx.ProfessorEsportes);
    }

    [Fact]
    public async Task Um_professor_nao_mexe_no_esporte_de_outro()
    {
        using var ctx = TestInfra.NovoContexto();
        var joao = NovoProfessor(ctx, "João", "88800000003");
        var maria = NovoProfessor(ctx, "Maria", "88800000004");
        ctx.ProfessorEsportes.Add(new ProfessorEsporte { ProfessorId = maria.Id, Esporte = EsporteDaAula.Tenis });
        await ctx.SaveChangesAsync();

        await TestInfra.NovoProfessoresController(ctx, joao.Id)
            .SalvarEsportes(new List<string> { EsporteDaAula.Padel });

        var deMaria = ctx.ProfessorEsportes.Where(pe => pe.ProfessorId == maria.Id).Select(pe => pe.Esporte).ToList();
        Assert.Equal(new[] { EsporteDaAula.Tenis }, deMaria);
    }

    // ---- Exibição no perfil ----

    [Fact]
    public async Task Perfil_mostra_os_esportes_salvos_em_ordem_fixa()
    {
        using var ctx = TestInfra.NovoContexto();
        var professor = NovoProfessor(ctx);
        // Gravados fora de ordem — a leitura tem que devolver Padel, Tênis, Beach Tênis.
        ctx.ProfessorEsportes.Add(new ProfessorEsporte { ProfessorId = professor.Id, Esporte = EsporteDaAula.BeachTenis });
        ctx.ProfessorEsportes.Add(new ProfessorEsporte { ProfessorId = professor.Id, Esporte = EsporteDaAula.Padel });
        await ctx.SaveChangesAsync();

        var view = Assert.IsType<ViewResult>(await TestInfra.NovoProfessoresController(ctx, professor.Id).Perfil(professor.Id));
        var vm = Assert.IsType<ProfessorPublicoVM>(view.Model);

        Assert.Equal(new[] { EsporteDaAula.Padel, EsporteDaAula.BeachTenis }, vm.EsportesQueEnsina);
    }

    [Fact]
    public async Task Professor_que_nunca_configurou_nao_mostra_esporte_nenhum()
    {
        using var ctx = TestInfra.NovoContexto();
        var professor = NovoProfessor(ctx);

        var view = Assert.IsType<ViewResult>(await TestInfra.NovoProfessoresController(ctx, professor.Id).Perfil(professor.Id));
        var vm = Assert.IsType<ProfessorPublicoVM>(view.Model);

        Assert.Empty(vm.EsportesQueEnsina);
    }
}
