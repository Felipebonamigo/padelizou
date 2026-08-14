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

        Assert.Equal("*Jogo em 24h!*\nConfirma presença dia 02/08.\n\nhttps://padelizou.com.br/Agenda"
                   + "\n\n_Pra não receber mais: https://padelizou.com.br/Auth/Preferencias — ou responda SAIR._",
                   texto);
    }

    // A SAÍDA VAI EM TODA MENSAGEM (07/08/2026, junto com o religamento do canal).
    // Denúncia é o gatilho mais forte que a Meta tem, e quem não acha como sair denuncia.
    [Fact]
    public void Toda_mensagem_diz_como_parar_de_receber()
    {
        var textos = new[]
        {
            AvisoPorWhatsApp.Montar("Jogo em 24h!", "Confirma presença.", "/Agenda", Site),
            AvisoPorWhatsApp.Montar("Oi", "Tudo bem", null, Site),
            AvisoPorWhatsApp.Montar("Torneio", "Copa", "https://outro.com.br/x", Site),
        };

        // O link das preferências vem antes do "responda SAIR" de propósito: ele é o único
        // que funciona sozinho — responder SAIR depende de alguém ler o celular do chip.
        Assert.All(textos, t => Assert.EndsWith(
            "_Pra não receber mais: https://padelizou.com.br/Auth/Preferencias — ou responda SAIR._", t));
    }

    [Fact]
    public void Sem_endereco_do_site_a_mensagem_nao_promete_saida_quebrada()
    {
        // "Pra não receber mais: /Auth/Preferencias" chegaria como texto solto, e uma saída
        // que não abre é pior que nenhuma — a pessoa tenta, não consegue, e denuncia.
        var texto = AvisoPorWhatsApp.Montar("Oi", "Tudo bem", "/Agenda", "");

        Assert.DoesNotContain("Preferencias", texto);
        Assert.DoesNotContain("SAIR", texto);
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
        Assert.Contains("\n\nhttps://padelizou.com.br\n\n_Pra não receber", AvisoPorWhatsApp.Montar("Oi", "Tudo bem", null, Site));
        Assert.Contains("\n\nhttps://padelizou.com.br\n\n_Pra não receber", AvisoPorWhatsApp.Montar("Oi", "Tudo bem", "/", Site));
    }

    [Fact]
    public void Corpo_igual_ao_titulo_nao_e_repetido()
    {
        // Tem aviso que manda o mesmo texto nos dois campos; na notificação o sistema desenha
        // separado, no WhatsApp sairia a frase duas vezes seguidas.
        Assert.StartsWith("*Copa de Verão*\n\nhttps://", AvisoPorWhatsApp.Montar("Copa de Verão", "Copa de Verão", null, Site));
    }

    [Fact]
    public async Task Aviso_comum_NAO_vai_pro_WhatsApp()
    {
        // A garantia mais importante deste arquivo. Até 04/08/2026 todo aviso ia pro WhatsApp
        // por padrão, e uma noite de torneio (24 cadastros em uma hora) fez a Meta restringir
        // o número. Agora o silêncio é o padrão: aviso novo nasce sem o canal, e quem quiser
        // precisa pedir na cara.
        using var ctx = ContextoComJogador(7);
        var fila = NovaFila();

        await EnviarEEntregarAsync(ctx, fila, null, 7, "Você entrou numa dupla!", "Copa X", "/x");

        Assert.Equal(0, fila.Pendentes);
    }

    [Fact]
    public async Task Aviso_marcado_como_urgente_vai_pro_WhatsApp()
    {
        // Sem push, o jogador não recebe nada — é essa pessoa que o canal existe pra alcançar.
        using var ctx = ContextoComJogador(7);
        var fila = NovaFila();

        await EnviarEEntregarAsync(ctx, fila, null, 7, "Jogo em 24h!", "Confirma presença.",
            "/Agenda", AlcanceDoAviso.AppEWhatsApp);

        Assert.True(fila.TentarLer(out var mensagem));
        Assert.Equal("51999998888", mensagem!.Celular);
        Assert.Contains("Jogo em 24h!", mensagem.Texto);
        Assert.Contains("https://padelizou.com.br/Agenda", mensagem.Texto);
    }

    [Fact]
    public async Task Quem_desmarcou_a_preferencia_nao_recebe()
    {
        using var ctx = ContextoComJogador(8, aceitaWhats: false);
        var fila = NovaFila();

        await EnviarEEntregarAsync(ctx, fila, null, 8, "Jogo em 24h!", "Confirma presença.",
            "/Agenda", AlcanceDoAviso.AppEWhatsApp);

        Assert.Equal(0, fila.Pendentes);
    }

    [Fact]
    public async Task O_aviso_e_ENFILEIRADO_nao_enviado_na_hora()
    {
        // A rajada é o que a Meta pune, não o total. Enfileirar também tira a chamada de rede
        // do caminho da ação do jogador.
        using var ctx = ContextoComJogador(9);
        var fila = NovaFila();
        var whats = Substitute.For<IWhatsAppService>();

        await EnviarEEntregarAsync(ctx, fila, whats, 9, "Jogo em 24h!", "Confirma.",
            "/Agenda", AlcanceDoAviso.AppEWhatsApp);

        Assert.Equal(1, fila.Pendentes);
        await whats.DidNotReceiveWithAnyArgs().EnviarAsync(default, default!);
    }

    [Fact]
    public async Task Notificacao_de_teste_do_painel_nao_vira_mensagem_no_celular_de_ninguem()
    {
        // O texto desse botão fala do app instalado — é teste de push. Um teste que chega no
        // WhatsApp de todo mundo é o tipo de aviso que faz a pessoa desligar o canal.
        using var ctx = ContextoComJogador(10);
        ctx.Add(new PushSubscriptionJogador
        {
            JogadorId = 10, Endpoint = "https://exemplo/push/10", P256dh = "p", Auth = "a",
        });
        await ctx.SaveChangesAsync();
        var fila = NovaFila();

        await Servico(ctx, fila).EnviarParaTodosInscritosAsync("Padelizou", "Notificação de teste");

        Assert.Equal(0, fila.Pendentes);
    }

    private static DbPadelContext ContextoComJogador(int id, bool aceitaWhats = true)
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador
        {
            Id = id, Nome = "Fulano", Cpf = id.ToString(),
            Celular = "51999998888", NotificarWhatsApp = aceitaWhats,
        });
        ctx.SaveChanges();
        return ctx;
    }

    private static FilaDeWhatsApp NovaFila() => new(NullLogger<FilaDeWhatsApp>.Instance);

    // Gerar aviso e ENTREGAR aviso viraram dois passos (Services/FilaDeAvisos): a ação do
    // jogador só enfileira, e quem faz SMTP e push é o serviço de fundo. Os testes deste
    // arquivo falam do canal de WhatsApp, que fica DEPOIS dessa fila — então aqui eles dão
    // os dois passos, que é o que acontece em produção meio segundo depois do clique.
    private static async Task EnviarEEntregarAsync(DbPadelContext ctx, FilaDeWhatsApp fila,
        IWhatsAppService? whats, int jogadorId, string titulo, string corpo, string? url = null,
        AlcanceDoAviso alcance = AlcanceDoAviso.SoApp)
    {
        var avisos = new FilaDeAvisos(NullLogger<FilaDeAvisos>.Instance);
        var servico = Servico(ctx, fila, whats, avisos);

        await servico.EnviarParaJogadorAsync(jogadorId, titulo, corpo, url, alcance);

        while (avisos.TentarLer(out var aviso))
            await servico.EntregarAgoraAsync(aviso!);
    }

    private static PushNotificationService Servico(DbPadelContext ctx, FilaDeWhatsApp fila,
        IWhatsAppService? whats = null, FilaDeAvisos? avisos = null) =>
        new(ctx,
            Options.Create(new VapidSettings
            {
                Subject = "mailto:teste@padelizou.com.br",
                PublicKey = "BExemploDeChavePublicaQueNaoEUsadaPorqueNaoHaInscricaoDePushNesteTeste",
                PrivateKey = "chave-privada-de-teste",
            }),
            whats ?? Substitute.For<IWhatsAppService>(),
            fila,
            avisos ?? new FilaDeAvisos(NullLogger<FilaDeAvisos>.Instance),
            Substitute.For<IEmailService>(),
            Options.Create(new SiteSettings()),
            PorteiroDeTeste.Com(),
            NullLogger<PushNotificationService>.Instance);
}
