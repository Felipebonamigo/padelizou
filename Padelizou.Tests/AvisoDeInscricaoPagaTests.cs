using Microsoft.EntityFrameworkCore;
using NSubstitute;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// "CAIU O DINHEIRO" — o aviso que o organizador não tinha.
//
// 🗣️ Felipe (06/10/2026), com o print do celular cheio de SMS e e-mail do Asaas ("seu cliente
// abriu a fatura", "cobrança visualizada"): *"quero que essas notificações venha apenas 1 email
// de quando for pago e também no aplicativo q eu tenho do sistema"*.
//
// ⚠️ METADE DISSO NÃO É CÓDIGO NOSSO: os avisos de fatura aberta/visualizada são da conta
// Asaas e se desligam no painel dela. O que faltava aqui era o OUTRO lado — o Padelizou não
// avisava NINGUÉM quando um pagamento de inscrição confirmava. O único aviso que existia era o
// de inscrição nova ("Apitouuuu!"), que vai pra quem SEGUE o torneio e não fala de dinheiro.
//
// 📧 SEM E-MAIL NOSSO, por decisão do Felipe: o e-mail de "pagamento confirmado" continua sendo
// o do Asaas, e mandar o nosso junto trocaria dois e-mails por dois outros. `AppSemEmail` é
// exatamente o alcance que a régua de 09/08 criou pra isso.
public class AvisoDeInscricaoPagaTests
{
    // ── O texto ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Pagamento_que_QUITA_diz_so_quem_pagou_e_quanto()
    {
        var corpo = TextoDaInscricaoPaga.Corpo(
            "Bruna Vargas", valorPago: 250m, falta: 0m, categoria: "4ª Masculina");

        Assert.Contains("Bruna Vargas", corpo);
        Assert.Contains("250", corpo);
        Assert.Contains("4ª Masculina", corpo);
        Assert.DoesNotContain("falta", corpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Pagamento_PARCIAL_diz_quanto_ainda_falta()
    {
        // Desde que dá pra pagar só a própria parte, "pagou" sem o resto faria o organizador
        // riscar a dupla da lista de cobrança — o mesmo erro que o Lucas relatou, agora por
        // escrito numa notificação.
        var corpo = TextoDaInscricaoPaga.Corpo(
            "Lucas Almeida", valorPago: 125m, falta: 125m, categoria: "4ª Masculina");

        Assert.Contains("125", corpo);
        Assert.Contains("falta", corpo, StringComparison.OrdinalIgnoreCase);
    }

    // ── Quem recebe ─────────────────────────────────────────────────────────────────

    private static (DbPadelContext ctx, Torneio torneio, Categoria categoria) Cenario()
    {
        var ctx = TestInfra.NovoContexto();
        var torneio = new Torneio
        {
            Nome = "NATA PADEL TOUR", Codigo = "NATA1", Status = "Inscrições Abertas",
            PrecoInscricao = 125m,
        };
        ctx.Torneios.Add(torneio);
        var categoria = new Categoria { Nome = "4ª Masculina", Codigo = "C4M", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();
        return (ctx, torneio, categoria);
    }

    [Fact]
    public async Task So_quem_ve_o_DINHEIRO_do_torneio_recebe()
    {
        // O ajudante faz tudo menos abrir o caixa (Services/AcessoAoDinheiroDoTorneio) —
        // mandar pra ele quanto entrou seria furar essa régua por notificação.
        var (ctx, torneio, categoria) = Cenario();
        var criador = new Jogador { Nome = "Lucas Almeida", Cpf = "11144477735" };
        var ajudante = new Jogador { Nome = "Ajudante", Cpf = "22255588846" };
        var quemPagou = new Jogador { Nome = "Bruna Vargas", Cpf = "33366699957" };
        ctx.Jogadores.AddRange(criador, ajudante, quemPagou);
        ctx.SaveChanges();
        ctx.TorneioOrganizadores.AddRange(
            new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = criador.Id, NivelAcesso = "Criador" },
            new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = ajudante.Id, NivelAcesso = "Ajudante" });
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        await new AvisoDeInscricaoPaga(ctx, push).NotificarAsync(
            torneio.Id, quemPagou.Id, "Bruna Vargas", 250m, 0m, categoria.Nome, "/url");

        await push.Received(1).EnviarParaJogadorAsync(criador.Id, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), AlcanceDoAviso.AppSemEmail);
        await push.DidNotReceive().EnviarParaJogadorAsync(ajudante.Id, Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Quem_PAGOU_nao_recebe_aviso_do_proprio_pagamento()
    {
        // O organizador que paga a inscrição dele acabou de sair do checkout: avisá-lo é o
        // tipo de notificação que ensina a ignorar o canal.
        var (ctx, torneio, categoria) = Cenario();
        var criador = new Jogador { Nome = "Lucas Almeida", Cpf = "11144477735" };
        ctx.Jogadores.Add(criador);
        ctx.SaveChanges();
        ctx.TorneioOrganizadores.Add(
            new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = criador.Id, NivelAcesso = "Criador" });
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        await new AvisoDeInscricaoPaga(ctx, push).NotificarAsync(
            torneio.Id, criador.Id, "Lucas Almeida", 125m, 0m, categoria.Nome, "/url");

        await push.DidNotReceive().EnviarParaJogadorAsync(Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    // ── O gancho: o aviso sai quando o pagamento confirma de verdade ────────────────

    [Fact]
    public async Task O_webhook_do_pagar_depois_dispara_o_aviso()
    {
        var (ctx, torneio, categoria) = Cenario();
        var criador = new Jogador { Nome = "Lucas Almeida", Cpf = "11144477735" };
        var quemPagou = new Jogador { Nome = "Bruna Vargas", Cpf = "33366699957" };
        ctx.Jogadores.AddRange(criador, quemPagou);
        ctx.SaveChanges();
        ctx.TorneioOrganizadores.Add(
            new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = criador.Id, NivelAcesso = "Criador" });
        var dupla = new Dupla
        {
            Categoria = categoria, Jogador1Id = quemPagou.Id, ValorInscricao = 125m,
        };
        ctx.Duplas.Add(dupla);
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        await TestInfra.ConfirmarPagamentoDeInscricaoAsync(ctx, torneio, quemPagou, dupla.Id, 125m, push);

        await push.Received(1).EnviarParaJogadorAsync(criador.Id, TextoDaInscricaoPaga.Titulo,
            Arg.Is<string>(c => c != null && c.Contains("Bruna Vargas")), Arg.Any<string?>(),
            AlcanceDoAviso.AppSemEmail);
    }

    [Fact]
    public async Task Aviso_de_pagamento_PARCIAL_carrega_o_que_falta()
    {
        var (ctx, torneio, categoria) = Cenario();
        var criador = new Jogador { Nome = "Lucas Almeida", Cpf = "11144477735" };
        var greg = new Jogador { Nome = "Greg Souza", Cpf = "22255588846" };
        ctx.Jogadores.AddRange(criador, greg);
        ctx.SaveChanges();
        ctx.TorneioOrganizadores.Add(
            new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = criador.Id, NivelAcesso = "Criador" });
        var dupla = new Dupla
        {
            Categoria = categoria, Jogador1Id = greg.Id, ValorInscricao = 250m,
        };
        ctx.Duplas.Add(dupla);
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        await TestInfra.ConfirmarPagamentoDeInscricaoAsync(ctx, torneio, greg, dupla.Id, 125m, push);

        await push.Received(1).EnviarParaJogadorAsync(criador.Id, Arg.Any<string>(),
            Arg.Is<string>(c => c != null && c.Contains("falta", StringComparison.OrdinalIgnoreCase)),
            Arg.Any<string?>(), AlcanceDoAviso.AppSemEmail);
    }

    [Fact]
    public async Task O_pagamento_que_CRIA_a_inscricao_tambem_dispara_o_aviso()
    {
        // São DOIS os jeitos de uma inscrição ser paga: a que já existia ("pagar depois") e
        // esta, que nasce do dinheiro no torneio que exige pagamento na inscrição. Ter o
        // gancho só num deles deixaria metade dos pagamentos em silêncio.
        var (ctx, torneio, categoria) = Cenario();
        var criador = new Jogador { Nome = "Lucas Almeida", Cpf = "11144477735" };
        var quemPagou = new Jogador { Nome = "Bruna Vargas", Cpf = "33366699957" };
        ctx.Jogadores.AddRange(criador, quemPagou);
        ctx.SaveChanges();
        ctx.TorneioOrganizadores.Add(
            new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = criador.Id, NivelAcesso = "Criador" });

        var pagamento = new Pagamento
        {
            Tipo = "Torneio",                 // cai no caminho que CRIA a inscrição
            TorneioId = torneio.Id,
            JogadorId = quemPagou.Id,
            Valor = 125m,
            Status = "Confirmado",
            ConfirmadoEm = DateTime.Now,
            DadosInscricao = System.Text.Json.JsonSerializer.Serialize(
                new DadosInscricaoTorneio(torneio.Id, categoria.Id, quemPagou.Id, null,
                    false, false, false, false, SemParceiro: true)),
        };
        ctx.Pagamentos.Add(pagamento);
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        await TestInfra.ServicoDePagamentos(ctx, push).EfetivarAsync(pagamento);

        Assert.NotNull(await ctx.Duplas.FirstOrDefaultAsync());   // a inscrição nasceu mesmo
        await push.Received(1).EnviarParaJogadorAsync(criador.Id, TextoDaInscricaoPaga.Titulo,
            Arg.Is<string>(c => c != null && c.Contains("Bruna Vargas")), Arg.Any<string?>(),
            AlcanceDoAviso.AppSemEmail);
    }
}
