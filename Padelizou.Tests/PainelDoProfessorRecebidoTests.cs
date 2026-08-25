using Microsoft.EntityFrameworkCore;
using Padelizou.Controllers;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using Xunit;

namespace Padelizou.Tests;

// O painel do professor na tela inicial mostra "no mês" com o rótulo de dinheiro recebido.
// Ele somava toda aula `Realizada` — então dizia que entrou o que ainda está pra entrar, e
// discordava do Financeiro na mesma conta.
public class PainelDoProfessorRecebidoTests
{
    [Fact]
    public async Task O_no_mes_do_painel_conta_so_a_aula_paga()
    {
        using var ctx = TestInfra.NovoContexto();

        var professor = new Jogador { Nome = "Jonatas", Login = "jonatas", Cpf = "99900000041", IsProfessor = true };
        ctx.Jogadores.Add(professor);
        await ctx.SaveChangesAsync();

        var local = new LocalAula { ProfessorId = professor.Id, Nome = "Batata Padel", PrecoPadrao = 110, Ativo = true };
        ctx.LocaisAula.Add(local);
        await ctx.SaveChangesAsync();

        var inicioDoMes = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        ctx.Aulas.AddRange(
            new Aula
            {
                ProfessorId = professor.Id, LocalAulaId = local.Id, NomeAlunoAvulso = "Medina",
                DataHora = inicioDoMes.AddDays(1).AddHours(9), Preco = 110,
                Status = PoliticaAula.Realizada, PagaEm = DateTime.Now,
            },
            new Aula
            {
                ProfessorId = professor.Id, LocalAulaId = local.Id, NomeAlunoAvulso = "Coello",
                DataHora = inicioDoMes.AddDays(2).AddHours(9), Preco = 110,
                Status = PoliticaAula.Realizada,
            });
        await ctx.SaveChangesAsync();

        var controller = new HomeController(ctx, new EstatisticasService(ctx))
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(
                        new System.Security.Claims.ClaimsIdentity(
                            new[] { new System.Security.Claims.Claim(
                                System.Security.Claims.ClaimTypes.NameIdentifier, professor.Id.ToString()) },
                            "Teste")),
                },
            },
        };

        var resultado = await controller.Index();
        var vm = Assert.IsType<HomeVM>(Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(resultado).Model);

        Assert.NotNull(vm.Professor);
        Assert.Equal(110m, vm.Professor!.RecebidoNoMes);
    }
}
