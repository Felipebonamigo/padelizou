using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using System.Globalization;

namespace Padelizou.Tests;

// O recebimento por Pix direto na conta do Padelizou, sem gateway (Services/PixDireto).
//
// O que está em jogo aqui é dinheiro entrando sem webhook: o BR Code tem que ser aceito
// pelo banco do pagador (PixCopiaECola), e a trava de "só o que é 100% nosso" tem que
// segurar — um pagamento com repasse confirmado por aqui registraria dinheiro de terceiro
// como receita do MEI.
public class PixDiretoTests
{
    // ── O código copia e cola ─────────────────────────────────────────────────────────────

    [Fact]
    public void Crc16_bate_com_o_vetor_oficial_do_algoritmo()
    {
        // CRC-16/CCITT-FALSE("123456789") = 0x29B1 — é a constante de verificação publicada
        // do algoritmo. Se isto quebrar, todo QR emitido é recusado como inválido.
        Assert.Equal("29B1", PixCopiaECola.Crc16("123456789"));
    }

    [Fact]
    public void Codigo_carrega_chave_valor_e_identificador()
    {
        var codigo = PixCopiaECola.Montar(
            "12345678000199", "Padelizou", "Porto Alegre", 49.90m, "PDZ00000042");

        Assert.StartsWith("000201", codigo);
        Assert.Contains("br.gov.bcb.pix", codigo);
        Assert.Contains("12345678000199", codigo);
        // O valor com ponto e dois decimais, do jeito que o EMV exige: "540549.90" é
        // id 54 + tamanho 05 + "49.90".
        Assert.Contains("540549.90", codigo);
        Assert.Contains("PDZ00000042", codigo);
        // O CRC declarado no fim precisa conferir com o payload que o antecede.
        Assert.Equal(PixCopiaECola.Crc16(codigo[..^4]), codigo[^4..]);
    }

    [Fact]
    public void Valor_em_centavos_nao_depende_da_cultura_da_maquina()
    {
        // A régua de sempre do dinheiro: centavos, nunca número redondo — e o servidor roda
        // em pt-BR, onde ToString() padrão daria "79,90" e o banco recusaria o código.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
            var codigo = PixCopiaECola.Montar("chave@pix.com", "Nome", "Cidade", 79.90m, "PDZ1");
            Assert.Contains("540579.90", codigo);
            Assert.DoesNotContain("79,90", codigo);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Acento_vira_letra_simples_e_nome_comprido_e_cortado()
    {
        var codigo = PixCopiaECola.Montar(
            "chave", "Associação São João de Padel Ltda", "São José dos Campos", 10m, "PDZ1");

        // "São" tem que virar "SAO" — acento descartado sem levar a letra junto.
        Assert.Contains("SAO", codigo);
        Assert.DoesNotContain("ç", codigo);
        Assert.DoesNotContain("ã", codigo);
        // O nome estoura os 25 do campo 59 e é cortado; a cidade estoura os 15 do campo 60.
        Assert.Contains("5925", codigo);   // id 59, tamanho 25 = usou o teto, não passou dele
        Assert.Contains("6015", codigo);
    }

    // ── A trava do que pode ser Pix ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("AssinaturaProfessor", true)]
    [InlineData("TaxaTorneio", true)]
    [InlineData("TorneioDupla", false)]
    [InlineData("TorneioAmericano", false)]
    [InlineData("Aula", false)]
    [InlineData("Jogo", false)]
    [InlineData("JogoVarios", false)]
    [InlineData(null, false)]
    public void So_o_que_e_inteiro_do_Padelizou_aceita_pix(string? tipo, bool esperado)
    {
        Assert.Equal(esperado, PixDireto.AceitaPix(tipo));
    }

    [Fact]
    public void Confirmar_recusa_pagamento_que_tem_repasse_a_terceiro()
    {
        // Mesmo que alguém consiga marcar uma inscrição como Pix, a baixa não pode passar:
        // seria dinheiro do organizador entrando como receita nossa.
        var comRepasse = new Pagamento
        {
            Tipo = "TorneioDupla",
            MetodoPagamento = PixDireto.Metodo,
            JogadorId = 1,
            RecebedorId = 2,
            Valor = 100m,
            ValorRepasse = 85m,
            Status = PixDireto.AguardandoConfirmacao,
        };

        Assert.NotNull(PixDireto.ProblemaParaConfirmar(comRepasse));
    }

    [Fact]
    public void Confirmar_aceita_a_fila_e_tambem_quem_nunca_declarou()
    {
        var pagamento = new Pagamento
        {
            Tipo = PixDireto.TipoAssinatura,
            MetodoPagamento = PixDireto.Metodo,
            JogadorId = 1,
            Valor = 49.90m,
            Comissao = 49.90m,
        };

        // O Pix pode cair sem o pagador clicar em nada — o extrato manda.
        pagamento.Status = "Pendente";
        Assert.Null(PixDireto.ProblemaParaConfirmar(pagamento));

        pagamento.Status = PixDireto.AguardandoConfirmacao;
        Assert.Null(PixDireto.ProblemaParaConfirmar(pagamento));

        // Dar baixa duas vezes não pode: a segunda estenderia a assinatura de novo.
        pagamento.Status = "Confirmado";
        Assert.NotNull(PixDireto.ProblemaParaConfirmar(pagamento));

        pagamento.Status = "Cancelado";
        Assert.NotNull(PixDireto.ProblemaParaConfirmar(pagamento));
    }

    [Fact]
    public void Cobranca_de_gateway_nao_entra_no_fluxo_do_pix()
    {
        var deGateway = new Pagamento
        {
            Tipo = PixDireto.TipoAssinatura,
            MetodoPagamento = PixDireto.MetodoGateway,
            JogadorId = 1,
            Status = "Pendente",
        };

        Assert.NotNull(PixDireto.ProblemaParaVer(deGateway, 1, ehAdmin: false));
        Assert.NotNull(PixDireto.ProblemaParaDeclarar(deGateway, 1));
        Assert.NotNull(PixDireto.ProblemaParaConfirmar(deGateway));
    }

    [Fact]
    public void So_o_dono_da_cobranca_ve_o_qr_mas_admin_tambem()
    {
        var pagamento = new Pagamento
        {
            Tipo = PixDireto.TipoAssinatura,
            MetodoPagamento = PixDireto.Metodo,
            JogadorId = 7,
            Status = "Pendente",
        };

        Assert.Null(PixDireto.ProblemaParaVer(pagamento, 7, ehAdmin: false));
        Assert.NotNull(PixDireto.ProblemaParaVer(pagamento, 8, ehAdmin: false));
        Assert.Null(PixDireto.ProblemaParaVer(pagamento, 8, ehAdmin: true));

        // Declarar "já paguei" é só do dono — nem admin declara pelos outros.
        Assert.Null(PixDireto.ProblemaParaDeclarar(pagamento, 7));
        Assert.NotNull(PixDireto.ProblemaParaDeclarar(pagamento, 8));
    }

    // ── O serviço gerando a cobrança ──────────────────────────────────────────────────────

    private static PagamentoInscricaoService Servico(DbPadelContext ctx)
    {
        var asaas = Substitute.For<IAsaasService>();
        asaas.Configurado.Returns(true);
        return new PagamentoInscricaoService(
            ctx, asaas, Options.Create(new AsaasSettings()),
            NullLogger<PagamentoInscricaoService>.Instance,
            Substitute.For<IPushNotificationService>(),
            Options.Create(new TaxasExibicao()),
            Options.Create(new PlanoProfessorSettings()));
    }

    private static Task ConfigurarChaveAsync(DbPadelContext ctx) =>
        ChavePixDoPadelizou.GravarAsync(ctx, "12345678000199", "Padelizou", "Porto Alegre", null);

    [Fact]
    public async Task Sem_chave_configurada_nao_nasce_cobranca_pix()
    {
        using var ctx = TestInfra.NovoContexto();
        var professor = new Jogador { Nome = "Prof", Cpf = "11144477735", IsProfessor = true };
        ctx.Jogadores.Add(professor);
        await ctx.SaveChangesAsync();

        // Null aqui é o que manda o controller pro caminho do gateway.
        Assert.Null(await Servico(ctx).IniciarPixDiretoAssinaturaAsync(professor));
        Assert.Empty(ctx.Pagamentos);
    }

    [Fact]
    public async Task Cobranca_pix_nasce_sem_repasse_e_dois_cliques_reaproveitam_a_mesma()
    {
        using var ctx = TestInfra.NovoContexto();
        await ConfigurarChaveAsync(ctx);
        var professor = new Jogador { Nome = "Prof", Cpf = "11144477735", IsProfessor = true };
        ctx.Jogadores.Add(professor);
        await ctx.SaveChangesAsync();

        var servico = Servico(ctx);
        var primeira = await servico.IniciarPixDiretoAssinaturaAsync(professor);

        Assert.NotNull(primeira);
        Assert.Equal(PixDireto.Metodo, primeira!.MetodoPagamento);
        Assert.Null(primeira.RecebedorId);
        Assert.Equal(0m, primeira.ValorRepasse);
        // Centavos, nunca número redondo: a mensalidade padrão é 49,90.
        Assert.Equal(49.90m, primeira.Valor);
        Assert.Equal(49.90m, primeira.Comissao);
        Assert.Null(primeira.AsaasPaymentId);   // gateway fica de fora de verdade

        // Segundo clique: a MESMA cobrança volta, inclusive depois de declarar o pagamento.
        var segunda = await servico.IniciarPixDiretoAssinaturaAsync(professor);
        Assert.Equal(primeira.Id, segunda!.Id);

        primeira.Status = PixDireto.AguardandoConfirmacao;
        await ctx.SaveChangesAsync();
        var terceira = await servico.IniciarPixDiretoAssinaturaAsync(professor);
        Assert.Equal(primeira.Id, terceira!.Id);
        Assert.Single(ctx.Pagamentos);
    }

    [Fact]
    public async Task Taxa_do_externo_por_pix_liberada_pelo_admin_destrava_as_chaves()
    {
        using var ctx = TestInfra.NovoContexto();
        await ConfigurarChaveAsync(ctx);
        var torneio = new Torneio { Nome = "Copa", Codigo = "C1", FormaPagamento = "Externo", PrecoInscricao = 100m };
        var organizador = new Jogador { Nome = "Org", Cpf = "11144477735" };
        ctx.Torneios.Add(torneio);
        ctx.Jogadores.Add(organizador);
        await ctx.SaveChangesAsync();

        var servico = Servico(ctx);
        var pagamento = await servico.IniciarPixDiretoTaxaExternoAsync(torneio, organizador, 120m);
        Assert.NotNull(pagamento);
        Assert.False(TaxaDoTorneioExterno.ChavesLiberadas(torneio));

        // O caminho da baixa do admin: confirma e efetiva — o mesmo Efetivar do webhook.
        Assert.Null(PixDireto.ProblemaParaConfirmar(pagamento));
        pagamento!.Status = "Confirmado";
        await ctx.SaveChangesAsync();
        await servico.EfetivarAsync(pagamento);

        Assert.True(TaxaDoTorneioExterno.ChavesLiberadas(torneio));
        Assert.Equal(torneio.Id, pagamento.ReferenciaId);
    }

    [Fact]
    public async Task Configuracao_pela_metade_e_o_mesmo_que_nenhuma()
    {
        using var ctx = TestInfra.NovoContexto();
        // Só a chave, sem nome e cidade — como um admin que gravou à mão no banco.
        ctx.ConfiguracoesDoSistema.Add(new ConfiguracaoDoSistema
        {
            Chave = ChavePixDoPadelizou.ChaveDaChave,
            Valor = "12345678000199",
        });
        await ctx.SaveChangesAsync();

        Assert.Null(await ChavePixDoPadelizou.LerAsync(ctx));
    }

    // ── A chave vai pro código como o DICT a registrou ────────────────────────────────────

    [Fact]
    public void Cnpj_escrito_com_mascara_vira_digitos_no_codigo()
    {
        // ⚠️ O CASO REAL de 08/08/2026: a chave de produção estava gravada assim, do jeito
        // que se escreve um CNPJ. Com pontuação o banco do pagador não acha a chave e mostra
        // "QR inválido" sem dizer por quê — a taxa do torneio e a mensalidade do professor
        // pararam de ser pagáveis, e uma organizadora real tentou e não conseguiu.
        Assert.Equal("68185754000105", PixCopiaECola.NormalizarChave("68.185.754/0001-05"));
        Assert.Equal("12345678909", PixCopiaECola.NormalizarChave("123.456.789-09"));

        // E o código montado carrega a versão limpa, não a mascarada.
        var codigo = PixCopiaECola.Montar("68.185.754/0001-05", "Padelizou", "Gravatai", 6.50m, "PDZ00000004");
        Assert.Contains("68185754000105", codigo);
        Assert.DoesNotContain("68.185.754/0001-05", codigo);
    }

    [Theory]
    // Aleatória (EVP): os hífens SÃO a chave — tirá-los quebraria o que estava certo.
    [InlineData("71d4a0f8-3c2b-4e19-9a77-0f2c1b8e5d63")]
    // E-mail e telefone são literais.
    [InlineData("padelizou@gmail.com")]
    [InlineData("+5551994854884")]
    public void Chave_que_nao_e_documento_passa_intacta(string chave)
    {
        Assert.Equal(chave, PixCopiaECola.NormalizarChave(chave));
    }

    [Fact]
    public void Numero_que_nao_tem_tamanho_de_documento_nao_e_mexido()
    {
        // 9 dígitos não é CPF nem CNPJ. Chutar uma limpeza aqui seria estragar uma chave que
        // eu não sei ler — devolver intacta deixa o erro visível em vez de silencioso.
        Assert.Equal("123456789", PixCopiaECola.NormalizarChave("123456789"));
    }
}
