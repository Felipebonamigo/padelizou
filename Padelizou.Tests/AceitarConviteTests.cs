using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O aceite do convite, no controller. A regra pura está em ConviteDeParceiroTests; aqui o que
// importa é o que só aparece com banco: o token some ao ser usado, a dupla fecha com quem
// clicou, e as recusas do TrocarParceiro (que agora moram num lugar só) valem também por aqui
// — é o caminho aberto por link, o que qualquer um alcança.
public class AceitarConviteTests
{
    private static DuplasController Controller(DbPadelContext ctx, int usuarioLogadoId)
    {
        var controller = new DuplasController(
            ctx,
            new EstatisticasService(ctx),
            Substitute.For<IEmailService>(),
            Substitute.For<IPushNotificationService>(),
            Substitute.For<IPagamentoInscricaoService>(),
            NullLogger<DuplasController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, usuarioLogadoId.ToString()) }, "Teste")),
            },
        };
        controller.TempData = new TempDataDictionary(controller.HttpContext, Substitute.For<ITempDataProvider>());
        // Push vive em try/catch: sem UrlHelper o Url.Action estoura e o teste ficaria verde
        // sem executar o trecho (a mesma armadilha já vista no push de chaves publicadas).
        controller.Url = Substitute.For<IUrlHelper>();
        return controller;
    }

    private static (DbPadelContext ctx, Dupla dupla, string token) Cenario(
        string statusTorneio = "Inscrições Abertas")
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.AddRange(
            new Jogador { Id = 10, Nome = "Quem convidou", Cpf = "10" },
            new Jogador { Id = 20, Nome = "Quem foi convidado", Cpf = "20" },
            new Jogador { Id = 30, Nome = "Terceiro", Cpf = "30" });

        var torneio = new Torneio
        {
            Id = 1,
            Nome = "Copa Teste",
            Codigo = "CONVITE1",
            Status = statusTorneio,
            PermiteMultiplasCategorias = true,
        };
        var categoria = new Categoria { Id = 1, TorneioId = 1, Nome = "3ª Categoria Masculina", Codigo = "3CatM" };
        ctx.Torneios.Add(torneio);
        ctx.Categorias.Add(categoria);

        var token = ConviteDeParceiro.NovoToken();
        var dupla = new Dupla
        {
            Id = 1,
            CategoriaId = 1,
            Jogador1Id = 10,
            ConviteToken = token,
            ConviteCriadoEm = DateTime.Now,
        };
        ctx.Duplas.Add(dupla);
        ctx.SaveChanges();
        return (ctx, dupla, token);
    }

    [Fact]
    public async Task Aceitar_fecha_a_dupla_com_quem_clicou_e_queima_o_token()
    {
        var (ctx, dupla, token) = Cenario();

        await Controller(ctx, usuarioLogadoId: 20).AceitarConvite(token);

        Assert.Equal(20, dupla.Jogador2Id);
        // Token queimado: sem isso o mesmo link fecharia a dupla de novo se o parceiro saísse.
        Assert.Null(dupla.ConviteToken);
        Assert.Null(dupla.ConviteCriadoEm);
    }

    [Fact]
    public async Task O_mesmo_link_nao_serve_pra_uma_segunda_pessoa()
    {
        // Duas pessoas com o link na mão: a segunda vê a tela de convite inválido, e a dupla
        // continua com quem aceitou primeiro.
        var (ctx, dupla, token) = Cenario();

        await Controller(ctx, usuarioLogadoId: 20).AceitarConvite(token);
        var segunda = await Controller(ctx, usuarioLogadoId: 30).AceitarConvite(token);

        Assert.Equal("ConviteInvalido", Assert.IsType<ViewResult>(segunda).ViewName);
        Assert.Equal(20, dupla.Jogador2Id);
    }

    [Fact]
    public async Task Quem_convidou_nao_vira_parceiro_de_si_mesmo()
    {
        var (ctx, dupla, token) = Cenario();

        await Controller(ctx, usuarioLogadoId: 10).AceitarConvite(token);

        Assert.Null(dupla.Jogador2Id);
        Assert.NotNull(dupla.ConviteToken);
    }

    [Fact]
    public async Task Token_errado_ou_vazio_nao_fecha_dupla()
    {
        var (ctx, dupla, _) = Cenario();
        var controller = Controller(ctx, usuarioLogadoId: 20);

        Assert.Equal("ConviteInvalido",
            Assert.IsType<ViewResult>(await controller.AceitarConvite(ConviteDeParceiro.NovoToken())).ViewName);
        Assert.Equal("ConviteInvalido",
            Assert.IsType<ViewResult>(await controller.AceitarConvite(null)).ViewName);
        Assert.Null(dupla.Jogador2Id);
    }

    [Fact]
    public async Task Convite_nao_vale_depois_de_as_inscricoes_encerrarem()
    {
        var (ctx, dupla, token) = Cenario(statusTorneio: "Fase de Grupos");

        var resultado = await Controller(ctx, usuarioLogadoId: 20).AceitarConvite(token);

        Assert.Equal("ConviteInvalido", Assert.IsType<ViewResult>(resultado).ViewName);
        Assert.Null(dupla.Jogador2Id);
    }

    [Fact]
    public async Task Quem_ja_esta_na_categoria_com_outra_dupla_e_recusado()
    {
        // Mesma recusa do caminho por CPF — a validação é compartilhada de propósito, senão
        // o caminho aberto por link ficaria mais frouxo que o outro.
        var (ctx, dupla, token) = Cenario();
        ctx.Duplas.Add(new Dupla { Id = 2, CategoriaId = 1, Jogador1Id = 20, Jogador2Id = 30 });
        ctx.SaveChanges();

        await Controller(ctx, usuarioLogadoId: 20).AceitarConvite(token);

        Assert.Null(dupla.Jogador2Id);
        Assert.NotNull(dupla.ConviteToken);
    }
}
