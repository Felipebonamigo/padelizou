using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using padelizou.Controllers;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Tests;

// "Eu aceitei aqui e não aparece quando eu clico em meus grupos" (10/08/2026).
//
// Quem é chamado de FORA pra um jogo de panelinha (avulso) não vira membro do grupo — e é isso
// que torna "Meus Grupos" a única tela onde o convite dele pode morar. A lista filtrava o
// convite por Status == "Pendente": no instante em que a pessoa respondia "eu vou", o jogo
// sumia da tela inteira, e ela ficava sem caminho pra ver quem mais ia ou pra desmarcar.
public class ConviteAvulsoNaListaDeGruposTests
{
    private static GruposController Controller(DbPadelContext ctx, int euId)
        => new GruposController(
            ctx, new SessaoGrupoService(ctx),
            Substitute.For<IPushNotificationService>(), NullLogger<GruposController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, euId.ToString()) }, "Teste")),
                },
            },
        };

    private static List<ConfirmacaoSessao> ConvitesDaTela(IActionResult resultado)
        => (List<ConfirmacaoSessao>)((ViewResult)resultado).ViewData["ConvitesDeJogo"]!;

    // Monta a panelinha do dono, com um jogo marcado, e chama `visitante` de fora pra ele.
    private static async Task<(Jogador visitante, SessaoGrupo sessao)> MontarConviteAsync(
        DbPadelContext ctx, DateTime quando, string status)
    {
        var dono = new Jogador { Nome = "Dono da panelinha", Cpf = "66600000001", Login = "dono" };
        var visitante = new Jogador { Nome = "Chamado de fora", Cpf = "66600000002", Login = "visitante" };
        ctx.Jogadores.AddRange(dono, visitante);
        await ctx.SaveChangesAsync();

        var grupo = new GrupoPrivado
        {
            Nome = "Pinel Gravatai", CodigoConvite = "PNL001", AdministradorId = dono.Id,
            DiaSemanaFixo = (int)quando.DayOfWeek, HorarioFixo = quando.TimeOfDay,
        };
        ctx.GruposPrivados.Add(grupo);
        await ctx.SaveChangesAsync();

        ctx.JogadoresGrupo.Add(new JogadorGrupo { GrupoId = grupo.Id, JogadorId = dono.Id });

        var sessao = new SessaoGrupo { GrupoId = grupo.Id, DataHora = quando };
        ctx.SessoesGrupo.Add(sessao);
        await ctx.SaveChangesAsync();

        ctx.ConfirmacoesSessao.Add(new ConfirmacaoSessao
        {
            SessaoId = sessao.Id, JogadorId = visitante.Id, Avulso = true, Status = status,
        });
        await ctx.SaveChangesAsync();

        return (visitante, sessao);
    }

    [Fact]
    public async Task Convite_ACEITO_continua_na_tela_de_grupos()
    {
        // ⚠️ O bug relatado. Aceitar era o gesto que fazia o jogo desaparecer.
        using var ctx = TestInfra.NovoContexto();
        var (visitante, sessao) = await MontarConviteAsync(ctx, DateTime.Today.AddDays(1).AddHours(20), "Confirmado");

        var convites = ConvitesDaTela(await Controller(ctx, visitante.Id).Index());

        Assert.Equal(sessao.Id, Assert.Single(convites).SessaoId);
    }

    [Fact]
    public async Task Convite_RECUSADO_tambem_fica_ate_o_jogo_passar()
    {
        // Mudar de ideia é comum, e sem a linha na tela não existe caminho de volta.
        using var ctx = TestInfra.NovoContexto();
        var (visitante, _) = await MontarConviteAsync(ctx, DateTime.Today.AddDays(1).AddHours(20), "NaoVai");

        Assert.Single(ConvitesDaTela(await Controller(ctx, visitante.Id).Index()));
    }

    [Fact]
    public async Task Convite_de_jogo_que_ja_passou_sai_da_tela()
    {
        // Sem corte por data o convite respondido ficaria pra sempre — o filtro antigo por
        // "Pendente" escondia isso por acidente.
        using var ctx = TestInfra.NovoContexto();
        var (visitante, _) = await MontarConviteAsync(ctx, DateTime.Today.AddDays(-3).AddHours(20), "Confirmado");

        Assert.Empty(ConvitesDaTela(await Controller(ctx, visitante.Id).Index()));
    }

    [Fact]
    public async Task Jogo_de_HOJE_continua_na_tela_durante_o_dia()
    {
        // O jogo é hoje às 20h e são 22h: quem foi chamado ainda quer abrir a tela pra ver o
        // que rolou. Cortar pela HORA apagaria o convite no meio do próprio jogo.
        using var ctx = TestInfra.NovoContexto();
        var (visitante, _) = await MontarConviteAsync(ctx, DateTime.Today.AddHours(6), "Confirmado");

        Assert.Single(ConvitesDaTela(await Controller(ctx, visitante.Id).Index()));
    }

    [Fact]
    public async Task Quem_e_MEMBRO_ve_o_grupo_na_lista_e_nao_como_convite()
    {
        // Membro já enxerga a panelinha pelo card do grupo. Repetir o mesmo jogo no bloco de
        // convite seria a mesma coisa duas vezes na mesma tela.
        using var ctx = TestInfra.NovoContexto();
        var (visitante, sessao) = await MontarConviteAsync(ctx, DateTime.Today.AddDays(1).AddHours(20), "Confirmado");

        ctx.JogadoresGrupo.Add(new JogadorGrupo { GrupoId = sessao.GrupoId, JogadorId = visitante.Id });
        await ctx.SaveChangesAsync();

        var resultado = (ViewResult)await Controller(ctx, visitante.Id).Index();

        Assert.Single((IEnumerable<GrupoPrivado>)resultado.Model!);
        Assert.Empty(ConvitesDaTela(resultado));
    }
}
