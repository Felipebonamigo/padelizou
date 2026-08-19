using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using NSubstitute;
using System.Security.Claims;

namespace Padelizou.Tests;

// O cadastro fiscal do clube e do cardápio — a fase 1 do plano de FISCAL.md.
//
// Ainda não emite nada, e é justamente por isso que estes testes existem: o cadastro é a
// única coisa que dá pra acertar ANTES de ter provedor, e o erro que ele evita é o pior de
// todos — a nota rejeitada pela SEFAZ com a fila esperando o troco.
public class CadastroFiscalTests
{
    // ---------- Os documentos ----------

    [Fact]
    public void Cnpj_so_passa_com_os_dois_digitos_verificadores()
    {
        // O CNPJ do próprio Padelizou (público na política de privacidade).
        Assert.True(Documentos.CnpjEhValido("68.185.754/0001-05"));
        Assert.True(Documentos.CnpjEhValido("68185754000105"));

        // Um dígito trocado no fim: é o erro de digitação mais comum, e o que só apareceria
        // na primeira emissão se passasse daqui.
        Assert.False(Documentos.CnpjEhValido("68185754000106"));

        // Tamanho certo, número inventado.
        Assert.False(Documentos.CnpjEhValido("11111111111111"));
        Assert.False(Documentos.CnpjEhValido("12345678901234"));

        // Faltando dígito e vazio.
        Assert.False(Documentos.CnpjEhValido("6818575400010"));
        Assert.False(Documentos.CnpjEhValido(""));
        Assert.False(Documentos.CnpjEhValido(null));
    }

    [Fact]
    public void Cnpj_aparece_formatado_e_o_que_nao_e_cnpj_volta_como_veio()
    {
        Assert.Equal("68.185.754/0001-05", Documentos.CnpjFormatado("68185754000105"));
        Assert.Equal("", Documentos.CnpjFormatado(null));
        Assert.Equal("em análise", Documentos.CnpjFormatado("em análise"));
    }

    [Fact]
    public void Codigo_de_barras_confere_o_digito_verificador()
    {
        // GTIN-13 real (Coca-Cola lata) e um GTIN-8 válido.
        Assert.True(Documentos.CodigoDeBarrasEhValido("7894900011517"));
        Assert.True(Documentos.CodigoDeBarrasEhValido("96385074"));

        // Último dígito trocado — a SEFAZ recusa isso na emissão (rejeição 611), então a tela
        // precisa recusar antes.
        Assert.False(Documentos.CodigoDeBarrasEhValido("7894900011518"));

        // Comprimento que não existe no padrão GTIN.
        Assert.False(Documentos.CodigoDeBarrasEhValido("12345"));
        Assert.False(Documentos.CodigoDeBarrasEhValido("00000000"));
    }

    // ---------- O checklist do clube ----------

    [Fact]
    public void Clube_vazio_lista_o_que_falta_e_nao_pode_emitir()
    {
        var clube = new Clube { Nome = "Chakra Padel" };

        var falta = DadosFiscaisDoClube.FaltaParaEmitir(clube, DadosFiscaisDoClube.Nfce);

        Assert.Contains("o CNPJ do clube", falta);
        Assert.Contains("a inscrição estadual", falta);
        Assert.Contains("o certificado digital A1", falta);
        Assert.False(DadosFiscaisDoClube.PodeEmitir(clube, DadosFiscaisDoClube.Nfce));
    }

    [Fact]
    public void Nota_de_servico_nao_cobra_inscricao_estadual()
    {
        // Quem só dá aula não precisa de inscrição estadual, e mandá-lo atrás dela seria
        // mandá-lo atrás de um documento que ele não vai usar. É por isso que o checklist
        // recebe QUAL documento se quer emitir.
        var clube = ClubePronto();
        clube.InscricaoEstadual = null;

        Assert.True(DadosFiscaisDoClube.PodeEmitir(clube, DadosFiscaisDoClube.Nfse));
        Assert.False(DadosFiscaisDoClube.PodeEmitir(clube, DadosFiscaisDoClube.Nfce));

        // E o contrário também: quem só vende no bar não precisa da municipal.
        var soBar = ClubePronto();
        soBar.InscricaoMunicipal = null;

        Assert.True(DadosFiscaisDoClube.PodeEmitir(soBar, DadosFiscaisDoClube.Nfce));
        Assert.False(DadosFiscaisDoClube.PodeEmitir(soBar, DadosFiscaisDoClube.Nfse));
    }

    [Fact]
    public void Clube_completo_pode_emitir_os_dois()
    {
        var clube = ClubePronto();

        Assert.True(DadosFiscaisDoClube.PodeEmitir(clube, DadosFiscaisDoClube.Nfce));
        Assert.True(DadosFiscaisDoClube.PodeEmitir(clube, DadosFiscaisDoClube.Nfse));
        Assert.Equal("Tudo pronto.", DadosFiscaisDoClube.ResumoDoQueFalta(
            DadosFiscaisDoClube.FaltaParaEmitir(clube, DadosFiscaisDoClube.Nfce)));
    }

    [Fact]
    public void Resumo_do_que_falta_e_frase_de_gente()
    {
        Assert.Equal("Falta o CNPJ.", DadosFiscaisDoClube.ResumoDoQueFalta(new[] { "o CNPJ" }));
        Assert.Equal("Faltam o CNPJ e o CEP.", DadosFiscaisDoClube.ResumoDoQueFalta(new[] { "o CNPJ", "o CEP" }));
        Assert.Equal("Faltam o CNPJ, o CEP e a UF.",
            DadosFiscaisDoClube.ResumoDoQueFalta(new[] { "o CNPJ", "o CEP", "a UF" }));
    }

    // ---------- O item do cardápio ----------

    [Fact]
    public void Cada_natureza_de_item_sai_num_documento_diferente()
    {
        // É a razão de existir do campo: o cardápio de um bar de clube mistura os três, e
        // sem essa pergunta nenhuma camada fiscal escolhe o documento.
        Assert.Equal("NFC-e", FiscalDoProduto.DocumentoDoTipo(FiscalDoProduto.Revenda));
        Assert.Equal("NFC-e", FiscalDoProduto.DocumentoDoTipo(FiscalDoProduto.ProducaoPropria));
        Assert.Equal("NFS-e", FiscalDoProduto.DocumentoDoTipo(FiscalDoProduto.Servico));

        // Aluguel de raquete: locação de bem móvel não é venda de mercadoria e o STF afastou
        // o ISS dela (Súmula Vinculante 31). Não sai em NFC-e nem em NFS-e.
        Assert.Equal("Nenhum", FiscalDoProduto.DocumentoDoTipo(FiscalDoProduto.Locacao));
        Assert.Equal("Nenhum", FiscalDoProduto.DocumentoDoTipo(null));
    }

    [Fact]
    public void Aluguel_nao_fica_devendo_ncm()
    {
        // Cobrar NCM de quem aluga raquete mandaria o clube caçar uma classificação que não
        // existe pro caso dele.
        Assert.Empty(FiscalDoProduto.FaltaParaEmitir(FiscalDoProduto.Locacao, null, null, null));
    }

    [Fact]
    public void Sem_dizer_o_que_o_item_e_o_resto_nem_e_cobrado()
    {
        var falta = FiscalDoProduto.FaltaParaEmitir(null, null, null, null);

        // Uma pendência só, e a certa: cobrar NCM antes de saber se o item é mercadoria seria
        // cobrar o campo errado.
        Assert.Single(falta);
        Assert.Contains("revenda", falta[0]);
    }

    [Fact]
    public void Mercadoria_precisa_de_ncm_cfop_e_unidade()
    {
        var falta = FiscalDoProduto.FaltaParaEmitir(FiscalDoProduto.Revenda, null, null, null);
        Assert.Equal(3, falta.Count);

        Assert.Empty(FiscalDoProduto.FaltaParaEmitir(FiscalDoProduto.Revenda, "22030000", "5102", "UN"));
    }

    [Fact]
    public void Cfop_muda_com_a_natureza_da_operacao()
    {
        // Revenda de terceiros e produção do próprio bar são operações diferentes, ainda que
        // o cliente não veja diferença nenhuma entre a lata e a caipirinha.
        Assert.Equal("5102", FiscalDoProduto.CfopSugerido(FiscalDoProduto.Revenda));
        Assert.Equal("5101", FiscalDoProduto.CfopSugerido(FiscalDoProduto.ProducaoPropria));
        Assert.Null(FiscalDoProduto.CfopSugerido(FiscalDoProduto.Locacao));
    }

    [Fact]
    public void Palpite_de_ncm_cobre_o_que_e_a_maior_parte_do_balcao()
    {
        // O nome que o bar realmente digita é a MARCA, não a categoria — foi este teste que
        // descobriu que o palpite só entendia "cerveja" e nunca dispararia num cardápio real.
        Assert.Equal("22030000", FiscalDoProduto.NcmSugerido("Heineken lata 350ml"));
        Assert.Equal("22030000", FiscalDoProduto.NcmSugerido("Brahma 600"));
        Assert.Equal("22030000", FiscalDoProduto.NcmSugerido("Chopp Pilsen"));
        Assert.Equal("22021000", FiscalDoProduto.NcmSugerido("Coca-Cola lata"));
        Assert.Equal("22029900", FiscalDoProduto.NcmSugerido("Red Bull"));
        Assert.Equal("22011000", FiscalDoProduto.NcmSugerido("Água sem gás"));
        Assert.Equal("22011000", FiscalDoProduto.NcmSugerido("agua com gas"));

        // O que o palpite não conhece fica NULO, e não errado: NCM chutado é autuação no nome
        // do cliente.
        Assert.Null(FiscalDoProduto.NcmSugerido("Porção de fritas"));
        Assert.Null(FiscalDoProduto.NcmSugerido(null));
    }

    [Fact]
    public void Formato_dos_campos_fiscais_e_conferido_mas_vazio_passa()
    {
        // Vazio passa: o cadastro fiscal é opcional até o clube querer emitir.
        Assert.Null(FiscalDoProduto.ProblemaComDadosFiscais(null, null, null, null, null, null));

        Assert.NotNull(FiscalDoProduto.ProblemaComDadosFiscais(null, "1234", null, null, null, null));
        Assert.NotNull(FiscalDoProduto.ProblemaComDadosFiscais(null, null, "51020", null, null, null));
        Assert.NotNull(FiscalDoProduto.ProblemaComDadosFiscais(null, null, null, "123", null, null));
        Assert.NotNull(FiscalDoProduto.ProblemaComDadosFiscais(null, null, null, null, "7894900011518", null));
        Assert.NotNull(FiscalDoProduto.ProblemaComDadosFiscais(null, null, null, null, null, 9));
        Assert.NotNull(FiscalDoProduto.ProblemaComDadosFiscais("Qualquer", null, null, null, null, null));

        Assert.Null(FiscalDoProduto.ProblemaComDadosFiscais(
            FiscalDoProduto.Revenda, "22030000", "5102", "0300700", "7894900011517", 0));
    }

    // ---------- De ponta a ponta ----------

    [Fact]
    public async Task Cnpj_invalido_nao_grava_e_avisa()
    {
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);
        var c = Controller(ctx, dono.Id);

        await c.SalvarFiscal(clube.Id, "68185754000106", "Chakra Padel Ltda", null, null, 1, null, null, null, null, null);

        Assert.Null(ctx.Clubes.Find(clube.Id)!.Cnpj);
        Assert.NotNull(c.TempData["ErroBar"]);
    }

    [Fact]
    public async Task Cep_traz_o_codigo_do_municipio_que_a_nota_exige()
    {
        // Ninguém sabe de cabeça o código IBGE da própria cidade. Ele vem do CEP junto com o
        // resto do endereço — é o que torna o cadastro preenchível por gente.
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);

        var c = Controller(ctx, dono.Id, TestInfra.CepQueResponde(
            new EnderecoDoCep("94010020", "Rua Dona Alzira", "Centro", "Gravataí", "RS", "4309209")));

        await c.SalvarFiscal(clube.Id, "68185754000105", "Chakra Padel Ltda", "0230000000", "12345",
            1, "94010020", null, "1200", null, null);

        var salvo = ctx.Clubes.Find(clube.Id)!;
        Assert.Equal("4309209", salvo.MunicipioIbge);
        Assert.Equal("RS", salvo.Uf);
        Assert.Equal("Rua Dona Alzira", salvo.Logradouro);
        Assert.Equal("68185754000105", salvo.Cnpj);   // só dígitos no banco
    }

    [Fact]
    public async Task Servico_de_cep_fora_do_ar_nao_impede_salvar_o_resto()
    {
        // Recusar aqui transformaria uma queda na casa dos outros em cadastro que ninguém
        // consegue salvar.
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);
        var c = Controller(ctx, dono.Id);   // CEP que não responde

        await c.SalvarFiscal(clube.Id, "68185754000105", "Chakra Padel Ltda", "0230000000", "12345",
            1, "94010020", "Rua Dona Alzira", "1200", null, "Centro");

        var salvo = ctx.Clubes.Find(clube.Id)!;
        Assert.Equal("68185754000105", salvo.Cnpj);
        Assert.Equal("Rua Dona Alzira", salvo.Logradouro);
        Assert.Null(salvo.MunicipioIbge);   // fica pra próxima gravação
    }

    [Fact]
    public async Task Salvar_o_preco_do_produto_nao_apaga_o_cadastro_fiscal_dele()
    {
        // A tela do cardápio não mostra NCM — quem corrige um preço não manda campo fiscal
        // nenhum. Se eles não fossem preservados, mexer no preço da cerveja apagaria em
        // silêncio o cadastro que o contador fez, e ninguém descobriria até a primeira venda.
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);

        var produto = new ProdutoBar
        {
            ClubeId = clube.Id, Nome = "Heineken lata", Preco = 12m,
            TipoFiscal = FiscalDoProduto.Revenda, Ncm = "22030000", Cfop = "5102", UnidadeComercial = "UN"
        };
        ctx.ProdutosBar.Add(produto);
        await ctx.SaveChangesAsync();

        var c = Controller(ctx, dono.Id);
        await c.SalvarProduto(clube.Id, produto.Id, "Heineken lata", 14m, "Bebidas");

        var salvo = ctx.ProdutosBar.Find(produto.Id)!;
        Assert.Equal(14m, salvo.Preco);
        Assert.Equal("22030000", salvo.Ncm);
        Assert.Equal(FiscalDoProduto.Revenda, salvo.TipoFiscal);
    }

    [Fact]
    public async Task Produto_novo_nasce_com_o_palpite_e_a_edicao_nao_sobrescreve_o_contador()
    {
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);
        var c = Controller(ctx, dono.Id);

        await c.SalvarProduto(clube.Id, null, "Heineken lata 350ml", 12m, "Bebidas",
            tipoFiscal: FiscalDoProduto.Revenda);

        var novo = ctx.ProdutosBar.Single();
        Assert.Equal("22030000", novo.Ncm);
        Assert.Equal("5102", novo.Cfop);
        Assert.Equal("UN", novo.UnidadeComercial);

        // O contador corrigiu à mão; uma edição posterior não pode devolver o chute.
        novo.Ncm = "22029900";
        await ctx.SaveChangesAsync();

        await c.SalvarProduto(clube.Id, novo.Id, "Heineken lata 350ml", 13m, "Bebidas");

        Assert.Equal("22029900", ctx.ProdutosBar.Find(novo.Id)!.Ncm);
    }

    [Fact]
    public async Task Cpf_inventado_na_nota_nao_fecha_a_comanda()
    {
        // Descobrir isso na emissão seria descobrir com a pessoa já fora do clube.
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);

        var comanda = new Comanda
        {
            ClubeId = clube.Id, Numero = 1, NomeCliente = "Rafael",
            DiaReferencia = DateTime.Today, Status = Comanda.Aberta
        };
        comanda.Itens.Add(new ItemComanda { Descricao = "Água", PrecoUnitario = 5m, Quantidade = 1 });
        ctx.Comandas.Add(comanda);
        await ctx.SaveChangesAsync();

        var c = Controller(ctx, dono.Id);
        await c.FecharComanda(comanda.Id, BarDoClube.Dinheiro, cpfConsumidor: "11111111111");

        var depois = ctx.Comandas.Find(comanda.Id)!;
        Assert.Equal(Comanda.Aberta, depois.Status);
        Assert.NotNull(c.TempData["ErroBar"]);

        // Com CPF de verdade, fecha e guarda só os dígitos.
        await c.FecharComanda(comanda.Id, BarDoClube.Dinheiro, cpfConsumidor: "111.444.777-35");

        depois = ctx.Comandas.Find(comanda.Id)!;
        Assert.Equal(Comanda.Fechada, depois.Status);
        Assert.Equal("11144477735", depois.CpfConsumidor);
    }

    [Fact]
    public async Task Comanda_fecha_sem_cpf_como_sempre_fechou()
    {
        // A NFC-e sai sem identificar o consumidor. Travar o fechamento por um campo que a lei
        // não exige pararia a fila por nada.
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);

        var comanda = new Comanda
        {
            ClubeId = clube.Id, Numero = 1, NomeCliente = "Rafael",
            DiaReferencia = DateTime.Today, Status = Comanda.Aberta
        };
        comanda.Itens.Add(new ItemComanda { Descricao = "Água", PrecoUnitario = 5m, Quantidade = 1 });
        ctx.Comandas.Add(comanda);
        await ctx.SaveChangesAsync();

        var c = Controller(ctx, dono.Id);
        await c.FecharComanda(comanda.Id, BarDoClube.Dinheiro);

        var depois = ctx.Comandas.Find(comanda.Id)!;
        Assert.Equal(Comanda.Fechada, depois.Status);
        Assert.Null(depois.CpfConsumidor);
    }

    [Fact]
    public async Task Aba_fiscal_desligada_e_invisivel_ate_pro_dono_do_clube()
    {
        // Mesma trava do bar, e pelo mesmo motivo: o fiscal vai pra produção antes de ter
        // provedor, e ninguém pode esbarrar nele por engano. São dois planos diferentes —
        // gestão e fiscal — e por isso dois interruptores.
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);

        var c = Controller(ctx, dono.Id, fiscalLigado: false);
        Assert.IsType<ForbidResult>(await c.Fiscal(clube.Id));

        var ligado = Controller(ctx, dono.Id, fiscalLigado: true);
        Assert.IsType<ViewResult>(await ligado.Fiscal(clube.Id));
    }

    // ---------- Apoio ----------

    private static Clube ClubePronto() => new()
    {
        Nome = "Chakra Padel",
        Cnpj = "68185754000105",
        RazaoSocial = "Chakra Padel Ltda",
        RegimeTributario = DadosFiscaisDoClube.SimplesNacional,
        InscricaoEstadual = "0230000000",
        InscricaoMunicipal = "12345",
        Logradouro = "Rua Dona Alzira",
        NumeroEndereco = "1200",
        Cep = "94010020",
        MunicipioIbge = "4309209",
        Uf = "RS",
        CertificadoConfiguradoEm = new DateTime(2026, 8, 19)
    };

    private static async Task<(Clube Clube, Jogador Dono)> SemearAsync(Padelizou.Models.DbPadelContext ctx)
    {
        var dono = new Jogador { Id = 1, Nome = "Dono do Clube", Cpf = "1" };
        var clube = new Clube { Id = 1, Nome = "Chakra Padel", DonoId = 1 };

        ctx.Jogadores.Add(dono);
        ctx.Clubes.Add(clube);
        await ctx.SaveChangesAsync();

        return (clube, dono);
    }

    private static BarController Controller(Padelizou.Models.DbPadelContext ctx, int usuarioId,
        IConsultaDeCep? cep = null, bool fiscalLigado = true)
    {
        var modulo = new ModuloDoBar(ctx, Options.Create(new BarSettings { Habilitado = true }));
        var c = new BarController(ctx, modulo,
            new ModuloFiscal(ctx, modulo, Options.Create(new FiscalSettings { Habilitado = fiscalLigado })),
            cep ?? TestInfra.CepQueNaoResponde(), NullLogger<BarController>.Instance);

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
