using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using padelizou.Controllers;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Tests;

// A panelinha: o clube onde ela joga, o clube de CADA jogo registrado, e o RSVP do jogo fixo
// da semana — que sumia da tela pra quem entrou no grupo depois da sessão nascer.
public class JogoDaSemanaEClubeTests
{
    private static GruposController Controller(DbPadelContext ctx, int euId, ISessaoGrupoService? sessoes = null)
    {
        var controller = new GruposController(
            ctx, sessoes ?? new SessaoGrupoService(ctx),
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
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.HttpContext, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        controller.Url = TestInfra.UrlDeTeste();
        return controller;
    }

    private static async Task<(GrupoPrivado grupo, Clube clube, List<Jogador> membros)> MontarAsync(DbPadelContext ctx)
    {
        var membros = Enumerable.Range(1, 4)
            .Select(i => new Jogador { Nome = $"Jogador {i}", Cpf = $"7770000000{i}", Login = $"jog{i}" })
            .ToList();
        ctx.Jogadores.AddRange(membros);

        var clube = new Clube { Nome = "Wallau" };
        ctx.Clubes.Add(clube);
        await ctx.SaveChangesAsync();

        var grupo = new GrupoPrivado
        {
            Nome = "Sub 90", CodigoConvite = "SUB090", AdministradorId = membros[0].Id,
            ClubeId = clube.Id, DiaSemanaFixo = 2, HorarioFixo = new TimeSpan(17, 30, 0),
        };
        ctx.GruposPrivados.Add(grupo);
        await ctx.SaveChangesAsync();

        ctx.JogadoresGrupo.AddRange(membros.Select(m =>
            new JogadorGrupo { GrupoId = grupo.Id, JogadorId = m.Id, PontuacaoInterna = 0 }));
        await ctx.SaveChangesAsync();

        return (grupo, clube, membros);
    }

    // ---- O clube no jogo registrado ----

    [Fact]
    public async Task Jogo_registrado_sem_escolher_local_herda_o_clube_do_grupo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, clube, membros) = await MontarAsync(ctx);

        await Controller(ctx, membros[0].Id).RegistrarJogo(
            grupo.Id, DateTime.Today, membros[0].Id, membros[1].Id, membros[2].Id, membros[3].Id, 1, 6, 4, null);

        // Local vazio não vira ranking de clube nenhum — e o jogo da panelinha é quase sempre
        // no clube de sempre.
        Assert.Equal(clube.Id, (await ctx.JogosSemanais.SingleAsync()).ClubeId);
    }

    [Fact]
    public async Task Jogo_de_hoje_pode_ser_em_outro_clube_sem_mexer_no_grupo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, clube, membros) = await MontarAsync(ctx);

        var outro = new Clube { Nome = "Hangar" };
        ctx.Clubes.Add(outro);
        await ctx.SaveChangesAsync();

        await Controller(ctx, membros[0].Id).RegistrarJogo(
            grupo.Id, DateTime.Today, membros[0].Id, membros[1].Id, membros[2].Id, membros[3].Id, 1, 6, 4, outro.Id);

        Assert.Equal(outro.Id, (await ctx.JogosSemanais.SingleAsync()).ClubeId);
        // O clube FIXO do grupo continua o mesmo: trocar de quadra numa semana não muda o
        // combinado da panelinha.
        Assert.Equal(clube.Id, (await ctx.GruposPrivados.FindAsync(grupo.Id))!.ClubeId);
    }

    // ---- O RSVP que sumia ----

    [Fact]
    public async Task Quem_entra_no_grupo_depois_ganha_linha_de_presenca_na_sessao_da_semana()
    {
        // O bug: a sessão nascia com os membros DAQUELE momento e nunca mais era reconciliada.
        // Quem entrava depois abria o jogo da semana e não tinha o que responder — o bloco
        // "Sua presença" só existe quando existe confirmação.
        using var ctx = TestInfra.NovoContexto();
        var (grupo, _, membros) = await MontarAsync(ctx);

        var servico = new SessaoGrupoService(ctx);
        var sessao = await servico.ObterOuCriarSessaoAsync(grupo, null);
        Assert.Equal(4, sessao.Confirmacoes.Count);

        var novato = new Jogador { Nome = "Chegou depois", Cpf = "77700000099", Login = "novato" };
        ctx.Jogadores.Add(novato);
        await ctx.SaveChangesAsync();
        ctx.JogadoresGrupo.Add(new JogadorGrupo { GrupoId = grupo.Id, JogadorId = novato.Id });
        await ctx.SaveChangesAsync();

        var deNovo = await servico.ObterOuCriarSessaoAsync(grupo, null);

        // ⚠️ `Presumido`, e não mais "Pendente" (21/08/2026): entrar na panelinha É entrar no
        // combinado de toda terça, então quem chega antes do jogo já entra DENTRO. A intenção
        // original do teste não mudou — ele ganha linha e passa a ter o que responder.
        Assert.Contains(deNovo.Confirmacoes, c => c.JogadorId == novato.Id && c.Status == PresencaNaSessao.Presumido);
        // E não duplica a de quem já estava lá.
        Assert.Equal(5, deNovo.Confirmacoes.Count);
    }

    [Fact]
    public async Task Sessao_da_semana_passada_nao_ganha_pendente_de_quem_nem_era_do_grupo()
    {
        // Reconciliar o passado só sujaria o histórico: "não respondeu" um jogo que aconteceu
        // antes de a pessoa entrar na panelinha.
        using var ctx = TestInfra.NovoContexto();
        var (grupo, _, membros) = await MontarAsync(ctx);

        var servico = new SessaoGrupoService(ctx);
        var semanaPassada = DateTime.Today.AddDays(-7).AddHours(17.5);
        await servico.ObterOuCriarSessaoAsync(grupo, semanaPassada);

        var novato = new Jogador { Nome = "Chegou depois", Cpf = "77700000098", Login = "novato2" };
        ctx.Jogadores.Add(novato);
        await ctx.SaveChangesAsync();
        ctx.JogadoresGrupo.Add(new JogadorGrupo { GrupoId = grupo.Id, JogadorId = novato.Id });
        await ctx.SaveChangesAsync();

        var velha = await servico.ObterOuCriarSessaoAsync(grupo, semanaPassada);

        Assert.DoesNotContain(velha.Confirmacoes, c => c.JogadorId == novato.Id);
    }

    [Fact]
    public async Task Membro_sem_confirmacao_consegue_avisar_que_vai()
    {
        // A trava do POST é ser MEMBRO, não ter linha: é o que salva quem abriu a tela numa
        // sessão criada antes de ele entrar.
        using var ctx = TestInfra.NovoContexto();
        var (grupo, _, membros) = await MontarAsync(ctx);

        var sessao = new SessaoGrupo { GrupoId = grupo.Id, DataHora = DateTime.Today.AddDays(2).AddHours(19) };
        ctx.SessoesGrupo.Add(sessao);
        await ctx.SaveChangesAsync();

        await Controller(ctx, membros[1].Id).ConfirmarPresenca(sessao.Id, vou: true, lado: "Direita");

        var minha = await ctx.ConfirmacoesSessao.SingleAsync(c => c.JogadorId == membros[1].Id);
        Assert.Equal("Confirmado", minha.Status);
        Assert.Equal("Direita", minha.Lado);
    }

    [Fact]
    public async Task Quem_nao_e_do_grupo_nao_confirma_presenca()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, _, _) = await MontarAsync(ctx);

        var estranho = new Jogador { Nome = "De fora", Cpf = "77700000097", Login = "defora" };
        ctx.Jogadores.Add(estranho);
        await ctx.SaveChangesAsync();

        var sessao = new SessaoGrupo { GrupoId = grupo.Id, DataHora = DateTime.Today.AddDays(2).AddHours(19) };
        ctx.SessoesGrupo.Add(sessao);
        await ctx.SaveChangesAsync();

        var resultado = await Controller(ctx, estranho.Id).ConfirmarPresenca(sessao.Id, vou: true, lado: null);

        Assert.IsType<ForbidResult>(resultado);
        Assert.Empty(ctx.ConfirmacoesSessao);
    }
}
