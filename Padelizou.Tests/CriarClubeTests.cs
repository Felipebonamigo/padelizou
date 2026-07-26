using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using padelizou.Controllers;
using Padelizou.Models;
using System.Security.Claims;

namespace Padelizou.Tests;

// Regressão da varredura de 26/07: Clubes/Criar era [AllowAnonymous] e criava clube
// sem validação nem deduplicação — e clube aparece em dropdown por todo o app.
public class CriarClubeTests
{
    private static ClubesController NovoController(DbPadelContext ctx, int usuarioId = 1)
    {
        var c = new ClubesController(ctx);
        c.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) }, "Teste")),
            },
        };
        return c;
    }

    [Fact]
    public async Task Nome_vazio_ou_gigante_e_recusado()
    {
        using var ctx = TestInfra.NovoContexto();
        var c = NovoController(ctx);

        Assert.IsType<BadRequestResult>(await c.Criar("", null));
        Assert.IsType<BadRequestResult>(await c.Criar("   ", null));
        Assert.IsType<BadRequestResult>(await c.Criar(new string('x', 121), null));

        Assert.Empty(ctx.Clubes);
    }

    [Fact]
    public async Task Mesmo_nome_devolve_o_clube_existente_em_vez_de_duplicar()
    {
        using var ctx = TestInfra.NovoContexto();
        var c = NovoController(ctx);

        await c.Criar("Arena Beira Rio", "Rua A");
        // Caixa diferente e espaços sobrando: continua sendo o mesmo clube.
        await c.Criar("  arena beira rio  ", "Rua B");

        Assert.Single(ctx.Clubes);
        Assert.Equal("Arena Beira Rio", ctx.Clubes.Single().Nome);
    }

    [Fact]
    public async Task Clube_novo_e_criado_com_nome_limpo()
    {
        using var ctx = TestInfra.NovoContexto();
        var c = NovoController(ctx);

        await c.Criar("  Padel Garden  ", "  Av. Central, 10  ");

        var clube = await ctx.Clubes.SingleAsync();
        Assert.Equal("Padel Garden", clube.Nome);
        Assert.Equal("Av. Central, 10", clube.Endereco);
    }
}
