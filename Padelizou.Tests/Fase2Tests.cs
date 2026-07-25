using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using System.Security.Claims;
using System.Text;

namespace Padelizou.Tests;

// Fase 2: métricas de uso do admin, comprovante e exportação CSV.
public class Fase2Tests
{
    private static padelizou.Controllers.AdminController NovoAdminController(DbPadelContext ctx, int usuarioLogadoId,
        decimal? tetoMei = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(tetoMei == null
                ? new Dictionary<string, string?>()
                : new Dictionary<string, string?> { ["Mei:TetoAnual"] = tetoMei.Value.ToString() })
            .Build();

        var controller = new padelizou.Controllers.AdminController(
            ctx, Substitute.For<IPushNotificationService>(), config);
        controller.ControllerContext = ContextoLogado(usuarioLogadoId);
        return controller;
    }

    private static PagamentosController NovoPagamentosController(DbPadelContext ctx, int usuarioLogadoId)
    {
        var settings = Microsoft.Extensions.Options.Options.Create(new AsaasSettings());
        var controller = new PagamentosController(
            ctx, settings,
            Substitute.For<IPagamentoInscricaoService>(),
            Substitute.For<IAsaasService>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PagamentosController>.Instance);
        controller.ControllerContext = ContextoLogado(usuarioLogadoId);
        return controller;
    }

    private static ControllerContext ContextoLogado(int usuarioId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) }, "Teste")),
        },
    };

    // ---------- Métricas de uso ----------

    [Fact]
    public async Task Metricas_conta_cadastros_inscricoes_e_mede_o_mei()
    {
        using var ctx = TestInfra.NovoContexto();

        var admin = new Jogador { Nome = "Admin", Cpf = "1", IsAdminRaiz = true, CriadoEm = DateTime.Now.AddYears(-1) };
        var novo = new Jogador { Nome = "Novo", Cpf = "2", CriadoEm = DateTime.Now.AddDays(-2) };
        var antigo = new Jogador { Nome = "Antigo", Cpf = "3", CriadoEm = null }; // antes da coluna existir
        ctx.Jogadores.AddRange(admin, novo, antigo);

        var cat = new Categoria { Nome = "Cat", Codigo = "C1", Torneio = new Torneio { Nome = "T", Codigo = "T1" } };
        ctx.Categorias.Add(cat);
        ctx.SaveChanges();

        ctx.Duplas.Add(new Dupla { CategoriaId = cat.Id, Jogador1Id = novo.Id, Jogador2Id = antigo.Id, CriadoEm = DateTime.Now.AddDays(-1) });
        ctx.InscricoesAmericanas.Add(new InscricaoAmericana { CategoriaId = cat.Id, JogadorId = novo.Id, CriadoEm = DateTime.Now.AddDays(-1) });

        // R$ 100 de comissão confirmada no ano, teto de R$ 1000 => 10% do MEI.
        ctx.Pagamentos.Add(new Pagamento
        {
            Tipo = "TorneioDupla",
            JogadorId = novo.Id,
            Valor = 500,
            ValorRepasse = 400,
            Comissao = 100,
            Status = "Confirmado",
            ConfirmadoEm = DateTime.Now.AddDays(-3),
        });
        ctx.SaveChanges();

        var controller = NovoAdminController(ctx, admin.Id, tetoMei: 1000);
        var resultado = await controller.Metricas();

        var vm = Assert.IsType<MetricasAdminVM>(Assert.IsType<ViewResult>(resultado).Model);
        Assert.Equal(3, vm.TotalJogadores);
        Assert.Equal(1, vm.JogadoresNovos7);       // só o "Novo"; o sem data não entra
        Assert.Equal(2, vm.InscricoesNovas7);      // 1 dupla + 1 americana
        Assert.Equal(1, vm.PagamentosConfirmados30);
        Assert.Equal(500, vm.ValorConfirmado30);
        Assert.Equal(100, vm.ComissaoAno);
        Assert.Equal(10, vm.PercentualMei);
    }

    [Fact]
    public async Task Metricas_exige_admin()
    {
        using var ctx = TestInfra.NovoContexto();
        var comum = new Jogador { Nome = "Comum", Cpf = "1" };
        ctx.Jogadores.Add(comum);
        ctx.SaveChanges();

        var controller = NovoAdminController(ctx, comum.Id);
        var resultado = await controller.Metricas();

        Assert.IsType<RedirectToActionResult>(resultado); // manda pro perfil, não mostra números
    }

    // ---------- Comprovante ----------

    [Fact]
    public async Task Comprovante_so_para_pagador_recebedor_ou_admin()
    {
        using var ctx = TestInfra.NovoContexto();
        var pagador = new Jogador { Nome = "Pagador", Cpf = "1" };
        var recebedor = new Jogador { Nome = "Recebedor", Cpf = "2" };
        var estranho = new Jogador { Nome = "Estranho", Cpf = "3" };
        var admin = new Jogador { Nome = "Admin", Cpf = "4", IsAdminRaiz = true };
        ctx.Jogadores.AddRange(pagador, recebedor, estranho, admin);
        ctx.SaveChanges();

        var pagamento = new Pagamento
        {
            Tipo = "Aula",
            JogadorId = pagador.Id,
            RecebedorId = recebedor.Id,
            Valor = 80,
            Status = "Confirmado",
            ConfirmadoEm = DateTime.Now,
        };
        ctx.Pagamentos.Add(pagamento);
        ctx.SaveChanges();

        Assert.IsType<ViewResult>(await NovoPagamentosController(ctx, pagador.Id).Comprovante(pagamento.Id));
        Assert.IsType<ViewResult>(await NovoPagamentosController(ctx, recebedor.Id).Comprovante(pagamento.Id));
        Assert.IsType<ViewResult>(await NovoPagamentosController(ctx, admin.Id).Comprovante(pagamento.Id));
        Assert.IsType<ForbidResult>(await NovoPagamentosController(ctx, estranho.Id).Comprovante(pagamento.Id));
    }

    // ---------- Exportar CSV ----------

    [Fact]
    public async Task ExportarCsv_traz_so_os_meus_recebimentos_no_formato_brasileiro()
    {
        using var ctx = TestInfra.NovoContexto();
        var eu = new Jogador { Nome = "Organizador", Cpf = "1" };
        var outro = new Jogador { Nome = "Outro Organizador", Cpf = "2" };
        var pagador = new Jogador { Nome = "José Açúcar", Cpf = "3" }; // acento testa o BOM/UTF-8
        ctx.Jogadores.AddRange(eu, outro, pagador);
        ctx.SaveChanges();

        ctx.Pagamentos.AddRange(
            new Pagamento
            {
                Tipo = "Aula",
                JogadorId = pagador.Id,
                RecebedorId = eu.Id,
                Valor = 123.45m,
                ValorRepasse = 111.11m,
                Comissao = 12.34m,
                Status = "Confirmado",
                ConfirmadoEm = DateTime.Now,
            },
            new Pagamento // de outro recebedor: NÃO pode aparecer no meu extrato
            {
                Tipo = "Aula",
                JogadorId = pagador.Id,
                RecebedorId = outro.Id,
                Valor = 999,
                Status = "Confirmado",
                ConfirmadoEm = DateTime.Now,
            });
        ctx.SaveChanges();

        var resultado = await NovoPagamentosController(ctx, eu.Id).ExportarCsv(null);

        var arquivo = Assert.IsType<FileContentResult>(resultado);
        Assert.Equal("text/csv", arquivo.ContentType);

        var texto = Encoding.UTF8.GetString(arquivo.FileContents);
        Assert.Contains("José Açúcar", texto);      // acentos intactos
        Assert.Contains("123,45", texto);           // vírgula decimal (Excel brasileiro)
        Assert.Contains("111,11", texto);
        Assert.DoesNotContain("999", texto);        // recebimento alheio fica fora
    }
}
