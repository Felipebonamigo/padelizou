using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Tests;

// A porta da retaguarda da agenda de quadra.
//
// O corte é este, e é o que estes testes guardam: **o que o jogador toca é grátis; o que o
// clube administra é o plano.** Publicar quadra e horário continua aberto — é a vitrine e o
// funil; administrar aquela agenda (balcão, mensalista, no-show, política, financeiro) passou
// a ser o Clube Gestão em 24/08/2026.
//
// ⚠️ Até essa data isso estava aberto POR ACIDENTE: o módulo nasceu depois da doutrina do
// PlanoDoClube e nunca passou por plano nenhum. O efeito era o clube de 2 quadras sem bar não
// ter o que comprar — já tinha tudo de graça.
public class PortaDaAgendaTests
{
    private static readonly DateTime Amanha = DateTime.Today.AddDays(1).AddHours(19);

    // ---------- Quem não assinou ----------

    [Fact]
    public async Task Clube_sem_plano_nao_administra_a_agenda_e_vai_pra_tela_de_assinatura()
    {
        // NUNCA um 403: quem chega aqui é o dono do clube, cliente em potencial que acabou de
        // clicar no produto. Mandá-lo pra tela de erro é perder a venda na porta — e, nesta
        // mudança, a tela de assinatura é também o que COMEÇA os 15 dias de teste, então
        // ninguém que usava a agenda ontem fica sem ela hoje.
        using var ctx = TestInfra.NovoContexto();
        var (clube, quadra, dono) = Semear(ctx, comPlano: false);
        var c = Controller(ctx, dono.Id);

        var r = Assert.IsType<RedirectToActionResult>(
            await c.MarcarBalcao(clube.Id, quadra.Id, Amanha, "Rafael", "51999999999"));

        Assert.Equal("PlanoClube", r.ControllerName);
        Assert.Empty(ctx.MarcacoesJogo);
    }

    [Fact]
    public async Task As_dez_acoes_de_administrar_a_agenda_estao_todas_atras_do_plano()
    {
        // Uma porta esquecida é a porta que o cliente encontra. Por isso a lista inteira, e
        // não uma amostra: bloquear, desbloquear, balcão, receber, cancelar, mensalista,
        // cancelar mensalista, no-show, política e financeiro.
        using var ctx = TestInfra.NovoContexto();
        var (clube, quadra, dono) = Semear(ctx, comPlano: false);

        // Uma marcação pra alimentar as ações que recebem marcacaoId. Criada direto no banco
        // porque o caminho pelo controller é, ele mesmo, um dos que estamos travando.
        var marcacao = new MarcacaoJogo
        {
            ClubeId = clube.Id, QuadraClubeId = quadra.Id, DataHora = Amanha,
            Status = "Confirmada", NomeClienteBalcao = "Rafael",
        };
        var bloqueio = new MarcacaoJogo
        {
            ClubeId = clube.Id, QuadraClubeId = quadra.Id, DataHora = Amanha.AddHours(2),
            Status = "Confirmada", EhBloqueio = true, MotivoBloqueio = "Manutenção",
        };
        ctx.MarcacoesJogo.AddRange(marcacao, bloqueio);
        await ctx.SaveChangesAsync();

        var c = Controller(ctx, dono.Id);

        var portas = new (string Nome, Func<Task<IActionResult>> Abrir)[]
        {
            ("Bloquear",           () => c.Bloquear(clube.Id, quadra.Id, Amanha.AddHours(4), 60, "Manutenção")),
            ("Desbloquear",        () => c.Desbloquear(bloqueio.Id)),
            ("MarcarBalcao",       () => c.MarcarBalcao(clube.Id, quadra.Id, Amanha.AddHours(6), "Ana", null)),
            ("ReceberPagamento",   () => c.ReceberPagamento(marcacao.Id)),
            ("CancelarBalcao",     () => c.CancelarBalcao(marcacao.Id)),
            ("CriarMensalista",    () => c.CriarMensalista(clube.Id, quadra.Id, dono.Id, Amanha, 60, 8)),
            ("CancelarMensalista", () => c.CancelarMensalista(clube.Id, Guid.NewGuid())),
            ("RegistrarNoShow",    () => c.RegistrarNoShow(marcacao.Id, compareceu: false)),
            ("SalvarPolitica",     () => c.SalvarPolitica(clube.Id, 12, true, "texto")),
            ("Financeiro",         () => c.Financeiro(clube.Id, null)),
        };

        foreach (var (nome, abrir) in portas)
        {
            var r = await abrir();
            var redirect = Assert.IsType<RedirectToActionResult>(r);
            Assert.Equal("PlanoClube", redirect.ControllerName);
        }
    }

    // ---------- Quem assinou ----------

    [Fact]
    public async Task Clube_com_plano_em_dia_administra_normalmente()
    {
        using var ctx = TestInfra.NovoContexto();
        var (clube, quadra, dono) = Semear(ctx, comPlano: true);
        var c = Controller(ctx, dono.Id);

        await c.MarcarBalcao(clube.Id, quadra.Id, Amanha, "Rafael", "51999999999");

        Assert.Single(ctx.MarcacoesJogo);
    }

    [Fact]
    public async Task O_teste_de_15_dias_vale_pra_agenda_como_vale_pro_bar()
    {
        // A mesma régua do PlanoDoClube: escolheu o plano e está dentro dos 15 dias, entra.
        using var ctx = TestInfra.NovoContexto();
        var (clube, quadra, dono) = Semear(ctx, comPlano: false);

        clube.PlanoDoClube = PlanoDoClube.Gestao;
        clube.TesteDoClubeInicio = DateTime.Now.AddDays(-3);
        await ctx.SaveChangesAsync();

        var c = Controller(ctx, dono.Id);
        await c.MarcarBalcao(clube.Id, quadra.Id, Amanha, "Rafael", null);

        Assert.Single(ctx.MarcacoesJogo);
    }

    [Fact]
    public async Task Carencia_de_7_dias_segura_a_agenda_de_quem_atrasou_o_boleto()
    {
        // Cortar a agenda no primeiro minuto de atraso é o clube sem conseguir registrar quem
        // ligou — e a reserva que não foi anotada não volta depois que o boleto é pago.
        using var ctx = TestInfra.NovoContexto();
        var (clube, quadra, dono) = Semear(ctx, comPlano: true);

        clube.AssinaturaClubePagaAte = DateTime.Now.AddDays(-5);
        await ctx.SaveChangesAsync();

        var c = Controller(ctx, dono.Id);
        await c.MarcarBalcao(clube.Id, quadra.Id, Amanha, "Rafael", null);

        Assert.Single(ctx.MarcacoesJogo);
    }

    // ---------- O que continua grátis ----------

    [Fact]
    public async Task O_painel_e_o_mapa_da_semana_continuam_abertos()
    {
        // São LEITURA, e são por onde o dono descobre o que existe e chega na tela do plano —
        // mesma doutrina do atalho do bar. Fechá-los seria esconder a vitrine de quem ainda
        // não comprou.
        using var ctx = TestInfra.NovoContexto();
        var (clube, _, dono) = Semear(ctx, comPlano: false);
        var c = Controller(ctx, dono.Id);

        Assert.IsType<ViewResult>(await c.Painel(clube.Id));
        Assert.IsType<ViewResult>(await c.Ocupacao(clube.Id, null));
    }

    // ---------- A ordem das duas perguntas ----------

    [Fact]
    public async Task Quem_nao_manda_no_clube_leva_403_e_nao_convite_pra_assinar()
    {
        // ⚠️ PERMISSÃO ANTES DE PLANO. Invertido, o gate viraria um detector de clubes
        // pagantes: qualquer pessoa logada descobriria quem assina e quem não assina pela
        // diferença entre o 403 e o convite.
        using var ctx = TestInfra.NovoContexto();
        var (clube, quadra, _) = Semear(ctx, comPlano: false);

        var estranho = new Jogador { Id = 99, Nome = "Estranho", Cpf = "99" };
        ctx.Jogadores.Add(estranho);
        await ctx.SaveChangesAsync();

        var c = Controller(ctx, estranho.Id);

        Assert.IsType<ForbidResult>(
            await c.MarcarBalcao(clube.Id, quadra.Id, Amanha, "Rafael", null));
    }

    // ---------- Apoio ----------

    private static (Clube Clube, QuadraClube Quadra, Jogador Dono) Semear(
        DbPadelContext ctx, bool comPlano)
    {
        var dono = new Jogador { Id = 1, Nome = "Dono", Cpf = "1" };

        var clube = new Clube
        {
            Id = 1, Nome = "Arena Teste", DonoId = 1,
            PlanoDoClube = comPlano ? PlanoDoClube.Gestao : null,
            AssinaturaClubePagaAte = comPlano ? DateTime.Now.AddMonths(1) : null,
        };

        var quadra = new QuadraClube { Id = 1, ClubeId = 1, Nome = "Quadra 1", Ativa = true };

        ctx.Jogadores.Add(dono);
        ctx.Clubes.Add(clube);
        ctx.QuadrasClube.Add(quadra);
        ctx.SaveChanges();

        return (clube, quadra, dono);
    }

    private static ClubeGestaoController Controller(DbPadelContext ctx, int usuarioId)
    {
        var c = new ClubeGestaoController(ctx, Substitute.For<IPushNotificationService>(),
            new ModuloDoBar(ctx, Options.Create(new BarSettings())),
            Options.Create(new PlanoClubeSettings()),
            NullLogger<ClubeGestaoController>.Instance);

        c.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) }, "Teste")),
            },
        };
        c.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            c.HttpContext, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());

        return c;
    }
}
