using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// NENHUMA AÇÃO DO USUÁRIO ESPERA E-MAIL OU PUSH.
//
// Relato do Interno de 05/08/2026: *"muito lento pra finalizar um jogo"*. A causa não estava
// no placar — estava no aviso. Finalizar dispara notificação pros 4 jogadores e pros
// seguidores deles, e cada um abria uma conexão SMTP com o Gmail mais uma chamada de push por
// aparelho, tudo DENTRO da requisição. O organizador ficava de pé na quadra olhando o botão
// girar, tocava de novo, e o mesmo jogo subia duas vezes.
//
// Agora `EnviarParaJogadorAsync` só enfileira. Quem entrega é o
// EntregadorDeAvisosBackgroundService, logo atrás.
public class AvisoNaoSeguraOCliqueTests
{
    [Fact]
    public async Task Gerar_aviso_nao_abre_conexao_nenhuma()
    {
        using var ctx = ContextoComJogador();
        var email = Substitute.For<IEmailService>();
        var fila = new FilaDeAvisos(NullLogger<FilaDeAvisos>.Instance);

        await Servico(ctx, fila, email).EnviarParaJogadorAsync(7, "Vitória!", "Vocês venceram (9x3).", "/x");

        // O aviso existe...
        Assert.Equal(1, fila.Pendentes);
        // ...e NADA de rede aconteceu no caminho de quem clicou.
        await email.DidNotReceiveWithAnyArgs().EnviarAsync(default!, default!, default!, default!);
    }

    [Fact]
    public async Task O_que_foi_enfileirado_e_entregue_depois_com_tudo_no_lugar()
    {
        using var ctx = ContextoComJogador();
        var email = Substitute.For<IEmailService>();
        var fila = new FilaDeAvisos(NullLogger<FilaDeAvisos>.Instance);
        var servico = Servico(ctx, fila, email);

        await servico.EnviarParaJogadorAsync(7, "Vitória!", "Vocês venceram (9x3).", "/Torneios/Details/1");

        Assert.True(fila.TentarLer(out var aviso));
        Assert.Equal(7, aviso!.JogadorId);
        Assert.Equal("Vitória!", aviso.Titulo);
        Assert.Equal("/Torneios/Details/1", aviso.Url);

        // Adiar não pode virar perder: entregue, o e-mail sai igual ao que saía antes.
        await servico.EntregarAgoraAsync(aviso);

        // O corpo vai como HTML e os acentos saem como entidade (`Voc&#234;s`), então o que se
        // confere é o que sobrevive à codificação: o placar e o link do jogo.
        await email.Received(1).EnviarAsync("fulano@exemplo.com", "Fulano", "Vitória!",
            Arg.Is<string>(html => html.Contains("(9x3)")
                                && html.Contains("https://padelizou.com.br/Torneios/Details/1")));
    }

    // A fila é limitada de propósito: com o provedor de e-mail fora do ar, uma noite de
    // torneio não pode encher a memória do servidor. Aviso é perecível — melhor perder o
    // excedente do que derrubar o site no meio do jogo.
    [Fact]
    public void Fila_cheia_descarta_e_avisa_no_log_em_vez_de_travar()
    {
        var fila = new FilaDeAvisos(NullLogger<FilaDeAvisos>.Instance);

        for (int i = 0; i < FilaDeAvisos.TamanhoMaximo; i++)
            Assert.True(fila.Enfileirar(new AvisoPendente(i, "t", "c", null, AlcanceDoAviso.SoApp)));

        Assert.False(fila.Enfileirar(new AvisoPendente(999, "t", "c", null, AlcanceDoAviso.SoApp)));
        Assert.Equal(1, fila.Descartados);
        Assert.Equal(FilaDeAvisos.TamanhoMaximo, fila.Pendentes);
    }

    private static DbPadelContext ContextoComJogador()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador
        {
            Id = 7, Nome = "Fulano", Cpf = "99900000077", Login = "fulano",
            Email = "fulano@exemplo.com", NotificarEmail = true,
        });
        ctx.SaveChanges();
        return ctx;
    }

    private static PushNotificationService Servico(DbPadelContext ctx, FilaDeAvisos fila, IEmailService email) =>
        new(ctx,
            Options.Create(new VapidSettings
            {
                Subject = "mailto:teste@padelizou.com.br",
                PublicKey = "chave-publica-de-teste",
                PrivateKey = "chave-privada-de-teste",
            }),
            Substitute.For<IWhatsAppService>(),
            new FilaDeWhatsApp(NullLogger<FilaDeWhatsApp>.Instance),
            fila,
            email,
            Options.Create(new SiteSettings { Url = "https://padelizou.com.br" }),
            NullLogger<PushNotificationService>.Instance);
}
