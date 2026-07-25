using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Tests;

// Infra compartilhada: banco EF InMemory novo por teste + montagem de cenário de torneio.
public static class TestInfra
{
    public static DbPadelContext NovoContexto()
    {
        var options = new DbContextOptionsBuilder<DbPadelContext>()
            .UseInMemoryDatabase("padelizou_teste_" + Guid.NewGuid())
            .Options;
        return new DbPadelContext(options);
    }

    // TorneiosController pronto pra uso nos testes: serviços de borda (e-mail, push,
    // pagamentos...) viram dublês; estatísticas usa o banco de verdade (em memória).
    public static TorneiosController NovoTorneiosController(DbPadelContext ctx, int usuarioLogadoId)
    {
        var controller = new TorneiosController(
            ctx,
            new EstatisticasService(ctx),
            Substitute.For<IPalpiteService>(),
            Substitute.For<IWebHostEnvironment>(),
            Substitute.For<IEmailService>(),
            Substitute.For<IPushNotificationService>(),
            Substitute.For<IPagamentoInscricaoService>(),
            NullLogger<TorneiosController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, usuarioLogadoId.ToString()) }, "Teste")),
            },
        };
        return controller;
    }

    public static Jogador NovoJogador(int i) => new()
    {
        Nome = $"Jogador {i:00}",
        Cpf = $"999000000{i:00}",
    };

    // Monta um torneio pronto pro sorteio: organizador, 1 categoria e N duplas inscritas.
    public static (Torneio torneio, Categoria categoria, Jogador organizador) MontarTorneio(
        DbPadelContext ctx, int qtdDuplas, string status = "Chaves em Sorteio")
    {
        var organizador = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(organizador);

        var torneio = new Torneio
        {
            Nome = "Torneio de Teste",
            Codigo = "TST123",
            Status = status,
            DataInicio = new DateTime(2026, 7, 1, 9, 0, 0),
        };
        ctx.Torneios.Add(torneio);

        var categoria = new Categoria { Nome = "2ª Categoria Masculina", Codigo = "CAT2M", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = organizador.Id });

        for (int i = 0; i < qtdDuplas; i++)
        {
            var j1 = NovoJogador(i * 2 + 1);
            var j2 = NovoJogador(i * 2 + 2);
            ctx.Jogadores.AddRange(j1, j2);
            ctx.Duplas.Add(new Dupla { Categoria = categoria, Jogador1 = j1, Jogador2 = j2 });
        }
        ctx.SaveChanges();

        return (torneio, categoria, organizador);
    }

    // Dá um placar à partida (games) e finaliza pelo fluxo real do controller.
    public static async Task FinalizarComPlacarAsync(
        DbPadelContext ctx, TorneiosController controller, Partida partida, int games1, int games2)
    {
        partida.GamesDupla1 = games1;
        partida.GamesDupla2 = games2;
        // O FinalizarPartida decide o vencedor por sets e desempata por games.
        partida.SetsDupla1 = games1 > games2 ? 1 : 0;
        partida.SetsDupla2 = games2 > games1 ? 1 : 0;
        await ctx.SaveChangesAsync();
        await controller.FinalizarPartida(partida.Id);
    }
}
