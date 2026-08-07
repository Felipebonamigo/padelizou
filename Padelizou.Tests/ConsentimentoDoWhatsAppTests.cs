using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// DESFAZER O OPT-IN HERDADO (07/08/2026).
//
// Ritmo, teto e aquecimento tratam VOLUME. A régua que a Meta usa de verdade é outra —
// *mandar pra quem não pediu* —, e em produção 54 de 55 contas antigas estavam no canal
// porque a caixinha nascia marcada, não porque alguém quis.
public class ConsentimentoDoWhatsAppTests
{
    private static readonly DateTime AntesDaRegra = ConsentimentoDoWhatsApp.OptInPassouASerObrigatorio.AddDays(-10);
    private static readonly DateTime DepoisDaRegra = ConsentimentoDoWhatsApp.OptInPassouASerObrigatorio.AddDays(1);

    private static Jogador Jogador(int id, DateTime? criadoEm, bool noWhats) => new()
    {
        Id = id, Nome = $"Jogador {id}", Cpf = id.ToString("D11"),
        Celular = "51999998888", NotificarWhatsApp = noWhats, CriadoEm = criadoEm,
    };

    [Fact]
    public void Conta_antiga_marcada_e_herdada_conta_nova_marcada_e_escolha()
    {
        // A MESMA caixinha marcada quer dizer coisas opostas dos dois lados da data: antes
        // dela ninguém foi perguntado, depois dela a pessoa marcou na mão.
        Assert.True(ConsentimentoDoWhatsApp.EstaNoCanalSemTerPedido(Jogador(1, AntesDaRegra, true)));
        Assert.False(ConsentimentoDoWhatsApp.EstaNoCanalSemTerPedido(Jogador(2, DepoisDaRegra, true)));

        // Desmarcado não é problema de ninguém, de qualquer lado.
        Assert.False(ConsentimentoDoWhatsApp.EstaNoCanalSemTerPedido(Jogador(3, AntesDaRegra, false)));
    }

    [Fact]
    public void Sem_saber_quando_a_conta_nasceu_ela_conta_como_herdada()
    {
        // Lado seguro: "não sei quando esta conta nasceu" não pode virar "então ela consentiu".
        Assert.True(ConsentimentoDoWhatsApp.EstaNoCanalSemTerPedido(Jogador(1, null, true)));
    }

    [Fact]
    public async Task Desmarca_so_as_herdadas_e_convida_cada_uma_de_volta()
    {
        using var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.AddRange(
            Jogador(1, AntesDaRegra, noWhats: true),      // herdada — sai
            Jogador(2, AntesDaRegra, noWhats: true),      // herdada — sai
            Jogador(3, DepoisDaRegra, noWhats: true),     // pediu — FICA
            Jogador(4, AntesDaRegra, noWhats: false));    // já estava fora
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        var r = await new DesfazerOptInHerdado(ctx, push).RodarAsync();

        Assert.Equal(2, r.Desmarcados);
        Assert.False(ctx.Jogadores.Single(j => j.Id == 1).NotificarWhatsApp);
        Assert.False(ctx.Jogadores.Single(j => j.Id == 2).NotificarWhatsApp);

        // ⚠️ Quem PEDIU não pode ser arrastado junto: o acerto é sobre consentimento, não
        // sobre esvaziar o canal.
        Assert.True(ctx.Jogadores.Single(j => j.Id == 3).NotificarWhatsApp);

        await push.Received(1).EnviarParaJogadorAsync(1, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
        await push.Received(1).EnviarParaJogadorAsync(2, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
        await push.DidNotReceive().EnviarParaJogadorAsync(3, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    // ⚠️ O TESTE QUE IMPEDE A IRONIA. Avisar pelo WhatsApp que a pessoa saiu do WhatsApp seria
    // exatamente a mensagem não pedida que causou o problema — e ainda por cima chegaria depois
    // de a preferência já estar desmarcada.
    [Fact]
    public async Task O_convite_de_volta_NAO_sai_pelo_WhatsApp()
    {
        using var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(Jogador(1, AntesDaRegra, noWhats: true));
        await ctx.SaveChangesAsync();

        var fila = new FilaDeAvisos(NullLogger<FilaDeAvisos>.Instance);
        var servico = new PushNotificationService(ctx,
            Options.Create(new VapidSettings { Subject = "mailto:t@t", PublicKey = "p", PrivateKey = "k" }),
            Substitute.For<IWhatsAppService>(),
            new FilaDeWhatsApp(NullLogger<FilaDeWhatsApp>.Instance),
            fila,
            Substitute.For<IEmailService>(),
            Options.Create(new SiteSettings()),
            NullLogger<PushNotificationService>.Instance);

        await new DesfazerOptInHerdado(ctx, servico).RodarAsync();

        Assert.True(fila.TentarLer(out var aviso));
        Assert.Equal(AlcanceDoAviso.SoApp, aviso!.Alcance);
        Assert.Equal(ConsentimentoDoWhatsApp.CaminhoDasPreferencias, aviso.Url);
    }

    [Fact]
    public async Task Rodar_de_novo_nao_acha_ninguem_nem_convida_de_novo()
    {
        // É botão de painel, e botão de painel é clicado duas vezes. A segunda passada não
        // pode render uma segunda leva de mensagens pras mesmas pessoas.
        using var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(Jogador(1, AntesDaRegra, noWhats: true));
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        var acao = new DesfazerOptInHerdado(ctx, push);

        Assert.Equal(1, (await acao.RodarAsync()).Desmarcados);

        var segunda = await acao.RodarAsync();
        Assert.Equal(0, segunda.Desmarcados);
        Assert.Equal(0, segunda.Convidados);

        await push.Received(1).EnviarParaJogadorAsync(1, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Conta_excluida_fica_de_fora()
    {
        // Mandar convite pra conta apagada é mensagem que não tem dono.
        using var ctx = TestInfra.NovoContexto();
        var excluido = Jogador(1, AntesDaRegra, noWhats: true);
        excluido.ExcluidoEm = DateTime.Now;
        ctx.Jogadores.Add(excluido);
        await ctx.SaveChangesAsync();

        var r = await new DesfazerOptInHerdado(ctx, Substitute.For<IPushNotificationService>()).RodarAsync();

        Assert.Equal(0, r.Desmarcados);
    }
}
