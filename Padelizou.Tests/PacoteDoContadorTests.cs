using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;
using System.Text;

namespace Padelizou.Tests;

// O CSV das vendas do bar — o pacote que o dono manda pro contador dele.
//
// Os testes cobrem o que faz um arquivo ser aceito ou devolvido: o formato que o Excel
// brasileiro abre de primeira, o recorte igual ao da tela, e o que NÃO pode sair (venda
// cancelada, comanda aberta, CPF de quem não pediu nota).
public class PacoteDoContadorTests
{
    // ---------- O formato ----------

    [Fact]
    public void O_formato_e_o_que_o_excel_brasileiro_abre_de_primeira()
    {
        // Vírgula no decimal (senão o Excel lê como texto e não soma) e sempre com dois
        // dígitos — "12,5" e "12,50" na mesma coluna faz o contador desconfiar do arquivo.
        Assert.Equal("1234,50", ArquivoCsv.Dinheiro(1234.5m));
        Assert.Equal("0,00", ArquivoCsv.Dinheiro(0m));
        Assert.Equal("-7,90", ArquivoCsv.Dinheiro(-7.9m));

        Assert.Equal("19/08/2026", ArquivoCsv.Data(new DateTime(2026, 8, 19)));
    }

    [Fact]
    public void Texto_com_ponto_e_virgula_ou_aspas_nao_quebra_a_coluna()
    {
        // Nome de produto é texto livre: "Porção grande; serve 2" quebraria a linha inteira.
        Assert.Equal("\"Porção grande; serve 2\"", ArquivoCsv.Campo("Porção grande; serve 2"));
        Assert.Equal("\"Cerveja \"\"long neck\"\"\"", ArquivoCsv.Campo("Cerveja \"long neck\""));
        Assert.Equal("\"\"", ArquivoCsv.Campo(null));
    }

    [Fact]
    public void O_arquivo_comeca_com_BOM_senao_todo_acento_vira_lixo()
    {
        var bytes = ArquivoCsv.Bytes(new StringBuilder("Comanda nº 7 do André"));

        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
        Assert.Contains("André", Encoding.UTF8.GetString(bytes));
    }

    // ---------- O conteúdo ----------

    [Fact]
    public async Task O_csv_de_vendas_traz_uma_linha_por_comanda_fechada()
    {
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);
        await SemearVendasAsync(ctx, clube.Id);

        var c = Controller(ctx, dono.Id);
        var arquivo = Assert.IsType<FileContentResult>(
            await c.ExportarCsv(clube.Id, Dia, Dia, tipo: null));

        var linhas = Linhas(arquivo);

        Assert.StartsWith("Data;Comanda;Cliente;CPF na nota;Forma de pagamento", linhas[0]);
        Assert.Equal(2, linhas.Count - 1);   // duas fechadas; a aberta e a cancelada ficam fora
        Assert.Contains(linhas, l => l.Contains("Rafael") && l.Contains("35,00"));
        Assert.Equal("text/csv", arquivo.ContentType);
    }

    [Fact]
    public async Task Comanda_aberta_e_cancelada_nao_entram_no_arquivo_do_contador()
    {
        // Comanda aberta ainda não é venda, e cancelada nunca foi — lançar qualquer uma das
        // duas é o contador registrando receita que não existiu.
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);
        await SemearVendasAsync(ctx, clube.Id);

        var c = Controller(ctx, dono.Id);
        var arquivo = Assert.IsType<FileContentResult>(
            await c.ExportarCsv(clube.Id, Dia, Dia, tipo: null));

        var texto = Encoding.UTF8.GetString(arquivo.FileContents);
        Assert.DoesNotContain("Comanda aberta", texto);
        Assert.DoesNotContain("Desistiu", texto);
    }

    [Fact]
    public async Task Item_cancelado_fica_de_fora_do_csv_de_itens()
    {
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);
        await SemearVendasAsync(ctx, clube.Id);

        var c = Controller(ctx, dono.Id);
        var arquivo = Assert.IsType<FileContentResult>(
            await c.ExportarCsv(clube.Id, Dia, Dia, tipo: "itens"));

        var texto = Encoding.UTF8.GetString(arquivo.FileContents);
        Assert.Contains("Heineken", texto);
        Assert.DoesNotContain("Devolvida", texto);   // item cancelado dentro de comanda válida
    }

    [Fact]
    public async Task O_csv_de_itens_leva_o_NCM_do_produto_e_deixa_o_avulso_em_branco()
    {
        // É o detalhe que o contador pede pra separar por produto — e o item avulso SEM NCM é
        // justamente o que ele precisa enxergar pra saber o que falta classificar.
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);
        await SemearVendasAsync(ctx, clube.Id);

        var c = Controller(ctx, dono.Id);
        var arquivo = Assert.IsType<FileContentResult>(
            await c.ExportarCsv(clube.Id, Dia, Dia, tipo: "itens"));

        var linhas = Linhas(arquivo);
        Assert.StartsWith("Data;Comanda;Produto;NCM;Quantidade", linhas[0]);

        var cerveja = Assert.Single(linhas, l => l.Contains("Heineken"));
        Assert.Contains("22030000", cerveja);

        var avulso = Assert.Single(linhas, l => l.Contains("Ficha de estacionamento"));
        Assert.Contains(";\"\";", avulso);   // NCM vazio, e a coluna continua existindo
    }

    [Fact]
    public async Task O_CPF_so_sai_de_quem_pediu_nota()
    {
        // Dado pessoal: exportar o CPF de quem não pediu seria espalhar documento que ninguém
        // entregou. E CPF inválido no banco não vira linha no arquivo do contador.
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);
        await SemearVendasAsync(ctx, clube.Id);

        var c = Controller(ctx, dono.Id);
        var arquivo = Assert.IsType<FileContentResult>(
            await c.ExportarCsv(clube.Id, Dia, Dia, tipo: null));

        var linhas = Linhas(arquivo);
        Assert.Contains(linhas, l => l.Contains("Rafael") && l.Contains("11144477735"));
        Assert.Contains(linhas, l => l.Contains("Bruna") && l.Contains(";\"\";"));
    }

    [Fact]
    public async Task O_periodo_do_arquivo_e_o_mesmo_da_tela_inclusive_com_datas_trocadas()
    {
        // Dois períodos diferentes pro mesmo mês é como o contador perde a confiança no
        // arquivo — a régua é uma só, compartilhada com o relatório.
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);
        await SemearVendasAsync(ctx, clube.Id);

        var c = Controller(ctx, dono.Id);

        // Datas invertidas devolvem o mesmo que as certas.
        var certo = Assert.IsType<FileContentResult>(await c.ExportarCsv(clube.Id, Dia, Dia.AddDays(1), null));
        var trocado = Assert.IsType<FileContentResult>(await c.ExportarCsv(clube.Id, Dia.AddDays(1), Dia, null));
        Assert.Equal(certo.FileContents, trocado.FileContents);

        // E um período sem venda devolve só o cabeçalho, não um erro.
        var vazio = Assert.IsType<FileContentResult>(
            await c.ExportarCsv(clube.Id, Dia.AddDays(-30), Dia.AddDays(-29), null));
        Assert.Single(Linhas(vazio));
    }

    [Fact]
    public async Task Sem_plano_nao_baixa_o_arquivo_do_bar()
    {
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx, comPlano: false);

        var c = Controller(ctx, dono.Id);
        var resposta = await c.ExportarCsv(clube.Id, Dia, Dia, null);

        var redirect = Assert.IsType<RedirectToActionResult>(resposta);
        Assert.Equal("PlanoClube", redirect.ControllerName);
    }

    // ---------- Apoio ----------

    private static readonly DateTime Dia = new(2026, 8, 19);

    private static List<string> Linhas(FileContentResult arquivo) =>
        Encoding.UTF8.GetString(arquivo.FileContents)
            .TrimStart('﻿')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\r'))
            .ToList();

    private static async Task<(Clube Clube, Jogador Dono)> SemearAsync(DbPadelContext ctx, bool comPlano = true)
    {
        var dono = new Jogador { Id = 1, Nome = "Dono do Clube", Cpf = "1" };
        var clube = new Clube
        {
            Id = 1, Nome = "Chakra Padel", DonoId = 1,
            PlanoDoClube = comPlano ? PlanoDoClube.Fiscal : null,
            AssinaturaClubePagaAte = comPlano ? DateTime.Now.AddMonths(1) : null,
        };

        ctx.Jogadores.Add(dono);
        ctx.Clubes.Add(clube);
        await ctx.SaveChangesAsync();
        return (clube, dono);
    }

    private static async Task SemearVendasAsync(DbPadelContext ctx, int clubeId)
    {
        ctx.ProdutosBar.Add(new ProdutoBar
        {
            Id = 1, ClubeId = clubeId, Nome = "Heineken lata", Preco = 12m, Ncm = "22030000"
        });

        // Fechada, com CPF na nota e um item cancelado dentro.
        var paga = new Comanda
        {
            Id = 1, ClubeId = clubeId, Numero = 1, NomeCliente = "Rafael",
            DiaReferencia = Dia, Status = Comanda.Fechada,
            FormaPagamento = BarDoClube.Dinheiro, CpfConsumidor = "11144477735",
        };
        paga.Itens.Add(new ItemComanda { Id = 1, Descricao = "Heineken lata", ProdutoBarId = 1, PrecoUnitario = 12m, Quantidade = 2, LancadoEm = Dia });
        paga.Itens.Add(new ItemComanda { Id = 2, Descricao = "Ficha de estacionamento", PrecoUnitario = 11m, Quantidade = 1, LancadoEm = Dia });
        paga.Itens.Add(new ItemComanda { Id = 3, Descricao = "Devolvida", PrecoUnitario = 50m, Quantidade = 1, LancadoEm = Dia, CanceladoEm = Dia });

        // Fechada, sem CPF.
        var semCpf = new Comanda
        {
            Id = 2, ClubeId = clubeId, Numero = 2, NomeCliente = "Bruna",
            DiaReferencia = Dia, Status = Comanda.Fechada, FormaPagamento = BarDoClube.Pix,
        };
        semCpf.Itens.Add(new ItemComanda { Id = 4, Descricao = "Água", PrecoUnitario = 5m, Quantidade = 1, LancadoEm = Dia });

        // Ainda aberta e uma cancelada: nenhuma das duas é venda.
        var aberta = new Comanda
        {
            Id = 3, ClubeId = clubeId, Numero = 3, NomeCliente = "Comanda aberta",
            DiaReferencia = Dia, Status = Comanda.Aberta,
        };
        var cancelada = new Comanda
        {
            Id = 4, ClubeId = clubeId, Numero = 4, NomeCliente = "Desistiu",
            DiaReferencia = Dia, Status = Comanda.Cancelada,
        };

        ctx.Comandas.AddRange(paga, semCpf, aberta, cancelada);
        await ctx.SaveChangesAsync();
    }

    private static BarController Controller(DbPadelContext ctx, int usuarioId)
    {
        var plano = Options.Create(new PlanoClubeSettings());
        var modulo = new ModuloDoBar(ctx, Options.Create(new BarSettings { Habilitado = true }), plano);

        var c = new BarController(ctx, modulo,
            new ModuloFiscal(ctx, modulo, Options.Create(new FiscalSettings { Habilitado = true }), plano),
            TestInfra.CepQueNaoResponde(),
            new NotasDoClube(ctx, NullLogger<NotasDoClube>.Instance),
            NullLogger<BarController>.Instance);

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
