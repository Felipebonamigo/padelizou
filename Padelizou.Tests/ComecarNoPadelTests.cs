using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A PORTA DE QUEM NUNCA JOGOU: /Professores/Comecar. É a única tela do site voltada pra quem
// ainda não é jogador — o resto todo fala com quem já joga. Ela mostra a MESMA vitrine de
// professores do /Professores (um dono só pra consulta), com a conversa de iniciante.
//
// A existência da rota na allowlist de busca é validada pelo teste de reflexão do
// BuscaNoGoogleTests — grafia errada ali mandaria a página pro ar com noindex.
public class ComecarNoPadelTests
{
    private static ProfessoresController Controller(DbPadelContext ctx) =>
        new(ctx, Substitute.For<IPushNotificationService>(),
            NullLogger<ProfessoresController>.Instance);

    [Fact]
    public async Task A_pagina_lista_so_quem_e_professor()
    {
        using var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador { Nome = "Professor Um", Cpf = "77700000001", IsProfessor = true });
        ctx.Jogadores.Add(new Jogador { Nome = "Jogador Comum", Cpf = "77700000002" });
        await ctx.SaveChangesAsync();

        var view = Assert.IsType<ViewResult>(await Controller(ctx).Comecar(cidadeId: null));
        var professores = Assert.IsType<List<Jogador>>(view.Model);

        var unico = Assert.Single(professores);
        Assert.Equal("Professor Um", unico.Nome);
    }

    [Fact]
    public async Task O_filtro_de_cidade_da_vitrine_vale_aqui_tambem()
    {
        using var ctx = TestInfra.NovoContexto();
        var daqui = new Jogador { Nome = "Professor Daqui", Cpf = "77700000003", IsProfessor = true };
        var deLa = new Jogador { Nome = "Professor De La", Cpf = "77700000004", IsProfessor = true };
        ctx.Jogadores.AddRange(daqui, deLa);
        var gravatai = new Cidade { Nome = "Gravataí", Estado = "RS" };
        var canoas = new Cidade { Nome = "Canoas", Estado = "RS" };
        ctx.Cidades.AddRange(gravatai, canoas);
        await ctx.SaveChangesAsync();
        ctx.ProfessorCidades.Add(new ProfessorCidade { ProfessorId = daqui.Id, CidadeId = gravatai.Id });
        ctx.ProfessorCidades.Add(new ProfessorCidade { ProfessorId = deLa.Id, CidadeId = canoas.Id });
        await ctx.SaveChangesAsync();

        var view = Assert.IsType<ViewResult>(await Controller(ctx).Comecar(gravatai.Id));
        var professores = Assert.IsType<List<Jogador>>(view.Model);

        var unico = Assert.Single(professores);
        Assert.Equal("Professor Daqui", unico.Nome);
    }
}
