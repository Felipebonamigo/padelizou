using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Todo aviso que vira notificação vira também mensagem de WhatsApp. A regra de QUEM recebe e
// de COMO o texto fica é pura (AvisoPorWhatsApp); o disparo mora no PushNotificationService,
// num lugar só, pra que nenhum aviso novo nasça esquecendo o canal.
public class AvisoPorWhatsAppTests
{
    private const string Site = "https://padelizou.com.br";

    [Theory]
    [InlineData(true, "51999998888", true)]
    [InlineData(true, "(51) 99999-8888", true)]   // salvo com máscara
    [InlineData(true, "5133334444", true)]        // fixo, 10 dígitos
    [InlineData(false, "51999998888", false)]     // desmarcou nas preferências
    [InlineData(true, null, false)]
    [InlineData(true, "", false)]
    [InlineData(true, "51", false)]               // campo pela metade
    [InlineData(true, "-", false)]
    public void Quem_pode_receber(bool aceita, string? celular, bool esperado)
    {
        Assert.Equal(esperado, AvisoPorWhatsApp.PodeReceber(aceita, celular));
    }

    [Fact]
    public void A_mensagem_tem_titulo_em_negrito_corpo_e_link_absoluto()
    {
        var texto = AvisoPorWhatsApp.Montar("Jogo em 24h!", "Confirma presença dia 02/08.", "/Agenda", Site);

        Assert.Equal("*Jogo em 24h!*\nConfirma presença dia 02/08.\n\nhttps://padelizou.com.br/Agenda", texto);
    }

    [Fact]
    public void Caminho_relativo_vira_endereco_de_verdade()
    {
        // No WhatsApp não há site em volta: "/Agenda" sozinho chega como texto, não como link.
        Assert.Contains("https://padelizou.com.br/Torneios/Details/12",
            AvisoPorWhatsApp.Montar("Torneio", "Copa", "/Torneios/Details/12", Site));

        // E um endereço que já veio pronto não é remontado.
        Assert.Contains("https://outro.com.br/x",
            AvisoPorWhatsApp.Montar("Torneio", "Copa", "https://outro.com.br/x", Site));
    }

    [Fact]
    public void Sem_caminho_o_link_e_a_raiz_do_site()
    {
        // Aviso sem caminho de volta obriga a pessoa a procurar o site — é esse atrito que
        // faz o aviso morrer na tela de bloqueio.
        Assert.EndsWith("\n\nhttps://padelizou.com.br", AvisoPorWhatsApp.Montar("Oi", "Tudo bem", null, Site));
        Assert.EndsWith("\n\nhttps://padelizou.com.br", AvisoPorWhatsApp.Montar("Oi", "Tudo bem", "/", Site));
    }

    [Fact]
    public void Corpo_igual_ao_titulo_nao_e_repetido()
    {
        // Tem aviso que manda o mesmo texto nos dois campos; na notificação o sistema desenha
        // separado, no WhatsApp sairia a frase duas vezes seguidas.
        Assert.StartsWith("*Copa de Verão*\n\nhttps://", AvisoPorWhatsApp.Montar("Copa de Verão", "Copa de Verão", null, Site));
    }

    [Fact]
    public async Task Quem_nao_instalou_o_app_recebe_no_WhatsApp_mesmo_assim()
    {
        // O ponto do canal novo: sem push, o jogador não recebia nada. O envio precisa
        // acontecer ANTES do return de quem não tem inscrição de push.
        using var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador
        {
            Id = 7, Nome = "Sem App", Cpf = "1",
            Celular = "51999998888", NotificarWhatsApp = true,
        });
        await ctx.SaveChangesAsync();

        var whats = Substitute.For<IWhatsAppService>();
        await Servico(ctx, whats).EnviarParaJogadorAsync(7, "Jogo em 24h!", "Confirma presença.", "/Agenda");

        await whats.Received(1).EnviarAsync("51999998888",
            Arg.Is<string>(m => m != null && m.Contains("Jogo em 24h!") && m.Contains("https://padelizou.com.br/Agenda")));
    }

    [Fact]
    public async Task Quem_desmarcou_a_preferencia_nao_recebe()
    {
        using var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador
        {
            Id = 8, Nome = "Não Quero", Cpf = "2",
            Celular = "51999998888", NotificarWhatsApp = false,
        });
        await ctx.SaveChangesAsync();

        var whats = Substitute.For<IWhatsAppService>();
        await Servico(ctx, whats).EnviarParaJogadorAsync(8, "Jogo em 24h!", "Confirma presença.", "/Agenda");

        await whats.DidNotReceiveWithAnyArgs().EnviarAsync(default, default!);
    }

    [Fact]
    public async Task Provedor_fora_do_ar_nao_derruba_o_aviso()
    {
        // A ação que gerou o aviso (marcar placar, abrir torneio) não pode estourar na cara de
        // quem clicou porque um provedor de mensagem caiu.
        using var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador
        {
            Id = 9, Nome = "Azarado", Cpf = "3",
            Celular = "51999998888", NotificarWhatsApp = true,
        });
        await ctx.SaveChangesAsync();

        var whats = Substitute.For<IWhatsAppService>();
        whats.EnviarAsync(Arg.Any<string?>(), Arg.Any<string>())
             .Returns<Task<bool>>(_ => throw new HttpRequestException("provedor fora do ar"));

        await Servico(ctx, whats).EnviarParaJogadorAsync(9, "Jogo em 24h!", "Confirma presença.", "/Agenda");
    }

    [Fact]
    public async Task Notificacao_de_teste_do_painel_nao_vira_mensagem_no_celular_de_ninguem()
    {
        // O texto desse botão fala do app instalado — é teste de push. Um teste que chega no
        // WhatsApp de todo mundo é o tipo de aviso que faz a pessoa desligar o canal.
        using var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador
        {
            Id = 10, Nome = "Com App", Cpf = "4",
            Celular = "51999998888", NotificarWhatsApp = true,
        });
        ctx.Add(new PushSubscriptionJogador
        {
            JogadorId = 10, Endpoint = "https://exemplo/push/10", P256dh = "p", Auth = "a",
        });
        await ctx.SaveChangesAsync();

        var whats = Substitute.For<IWhatsAppService>();
        await Servico(ctx, whats).EnviarParaTodosInscritosAsync("Padelizou", "Notificação de teste");

        await whats.DidNotReceiveWithAnyArgs().EnviarAsync(default, default!);
    }

    private static PushNotificationService Servico(DbPadelContext ctx, IWhatsAppService whats) =>
        new(ctx,
            Options.Create(new VapidSettings
            {
                Subject = "mailto:teste@padelizou.com.br",
                PublicKey = "BExemploDeChavePublicaQueNaoEUsadaPorqueNaoHaInscricaoDePushNesteTeste",
                PrivateKey = "chave-privada-de-teste",
            }),
            whats,
            Options.Create(new SiteSettings()),
            NullLogger<PushNotificationService>.Instance);
}
