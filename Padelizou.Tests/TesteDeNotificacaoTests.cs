using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O teste de aviso do painel: manda pra UMA pessoa, pelos canais escolhidos, e diz POR QUE
// não chegou quando não chega. Esse "por que" é o motivo da tela existir — "não chegou" tem
// meia dúzia de causas muito diferentes, e sem o diagnóstico o admin caça no escuro.
public class TesteDeNotificacaoTests
{
    private static DbPadelContext ContextoCom(Jogador jogador, bool comApp = false)
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(jogador);
        if (comApp)
        {
            ctx.Add(new PushSubscriptionJogador
            {
                JogadorId = jogador.Id, Endpoint = "https://exemplo/push", P256dh = "p", Auth = "a",
            });
        }
        ctx.SaveChanges();
        return ctx;
    }

    private static Jogador Jogador(string? celular = "51999998888", bool aceitaWhats = true) => new()
    {
        Id = 1, Nome = "Fulano", Cpf = "1", Login = "fulano",
        Celular = celular, NotificarWhatsApp = aceitaWhats,
    };

    private static PushNotificationService Servico(DbPadelContext ctx, IWhatsAppService whats) =>
        new(ctx,
            Options.Create(new VapidSettings
            {
                Subject = "mailto:teste@padelizou.com.br",
                PublicKey = "chave-publica-de-teste",
                PrivateKey = "chave-privada-de-teste",
            }),
            whats,
            Options.Create(new SiteSettings()),
            NullLogger<PushNotificationService>.Instance);

    private static IWhatsAppService WhatsLigado(bool envioDaCerto = true)
    {
        var whats = Substitute.For<IWhatsAppService>();
        whats.Configurado.Returns(true);
        whats.EnviarAsync(Arg.Any<string?>(), Arg.Any<string>()).Returns(envioDaCerto);
        return whats;
    }

    [Fact]
    public async Task Canal_nao_pedido_nao_e_tentado()
    {
        // Marcar só um canal precisa deixar o outro EM PAZ — senão o teste dirigido vira o
        // envio normal com outro nome.
        using var ctx = ContextoCom(Jogador());
        var whats = WhatsLigado();

        var r = await Servico(ctx, whats).EnviarTesteAsync(1, porPush: true, porWhatsApp: false, "T", "corpo");

        Assert.Equal(ResultadoDoCanal.NaoPedido, r.WhatsApp);
        await whats.DidNotReceiveWithAnyArgs().EnviarAsync(default, default!);
    }

    [Fact]
    public async Task Sem_app_instalado_o_push_explica_em_vez_de_mentir()
    {
        // O envio normal desiste calado aqui. No teste, calado seria pior que inútil: o admin
        // concluiria que o push está quebrado.
        using var ctx = ContextoCom(Jogador(), comApp: false);

        var r = await Servico(ctx, WhatsLigado()).EnviarTesteAsync(1, true, false, "T", "corpo");

        Assert.Equal(ResultadoDoCanal.SemAppInstalado, r.Push);
        Assert.Equal(0, r.Aparelhos);
        Assert.False(TextoDoTeste.DeuCerto(r.Push));
    }

    [Fact]
    public async Task Aparelho_cadastrado_que_nao_recebeu_nao_conta_como_enviado()
    {
        // O endpoint é falso, então a entrega falha. Dizer "enviado" só porque existe
        // aparelho cadastrado seria mentir justamente na tela feita pra conferir a verdade —
        // e inscrição revogada pelo navegador fica na tabela até a tentativa seguinte.
        using var ctx = ContextoCom(Jogador(), comApp: true);

        var r = await Servico(ctx, WhatsLigado()).EnviarTesteAsync(1, true, false, "T", "corpo");

        Assert.Equal(ResultadoDoCanal.FalhouNoEnvio, r.Push);
        Assert.Equal(0, r.Aparelhos);
    }

    [Fact]
    public async Task WhatsApp_enviado_mostra_o_numero_de_volta()
    {
        // Digitar login na mão erra fácil; ver o número formatado é como o admin percebe que
        // mandou pra pessoa errada ANTES de sair contando que o canal funciona.
        using var ctx = ContextoCom(Jogador());

        var r = await Servico(ctx, WhatsLigado()).EnviarTesteAsync(1, false, true, "T", "corpo");

        Assert.Equal(ResultadoDoCanal.Enviado, r.WhatsApp);
        Assert.Equal("(51) 99999-8888", r.Numero);
    }

    [Fact]
    public async Task Canal_desligado_no_ambiente_e_dito_antes_de_tudo()
    {
        // É a causa nº 1 de "não chegou" no dev, e a única que o admin resolve sozinho.
        using var ctx = ContextoCom(Jogador());
        var whats = Substitute.For<IWhatsAppService>();
        whats.Configurado.Returns(false);

        var r = await Servico(ctx, whats).EnviarTesteAsync(1, false, true, "T", "corpo");

        Assert.Equal(ResultadoDoCanal.CanalDesligadoNesteAmbiente, r.WhatsApp);
        await whats.DidNotReceiveWithAnyArgs().EnviarAsync(default, default!);
    }

    [Fact]
    public async Task Preferencia_desmarcada_e_numero_faltando_sao_motivos_diferentes()
    {
        // Parecem a mesma coisa na tela ("não chegou") e têm conserto oposto: um é o jogador
        // que precisa marcar de novo, o outro é cadastro incompleto.
        using (var ctx = ContextoCom(Jogador(aceitaWhats: false)))
        {
            var r = await Servico(ctx, WhatsLigado()).EnviarTesteAsync(1, false, true, "T", "corpo");
            Assert.Equal(ResultadoDoCanal.PreferenciaDesmarcada, r.WhatsApp);
        }

        using (var ctx = ContextoCom(Jogador(celular: null)))
        {
            var r = await Servico(ctx, WhatsLigado()).EnviarTesteAsync(1, false, true, "T", "corpo");
            Assert.Equal(ResultadoDoCanal.SemNumeroNoCadastro, r.WhatsApp);
        }
    }

    [Fact]
    public async Task Provedor_que_recusa_nao_e_confundido_com_sucesso()
    {
        using var ctx = ContextoCom(Jogador());

        var r = await Servico(ctx, WhatsLigado(envioDaCerto: false)).EnviarTesteAsync(1, false, true, "T", "corpo");

        Assert.Equal(ResultadoDoCanal.FalhouNoEnvio, r.WhatsApp);
        Assert.Null(r.Numero);
    }

    [Theory]
    [InlineData(ResultadoDoCanal.Enviado, true)]
    [InlineData(ResultadoDoCanal.NaoPedido, false)]
    [InlineData(ResultadoDoCanal.SemAppInstalado, false)]
    [InlineData(ResultadoDoCanal.FalhouNoEnvio, false)]
    public void So_Enviado_conta_como_sucesso(ResultadoDoCanal resultado, bool esperado)
    {
        Assert.Equal(esperado, TextoDoTeste.DeuCerto(resultado));
    }

    [Fact]
    public void Todo_resultado_tem_frase_propria()
    {
        // Dois motivos com a mesma frase deixariam o admin sem saber qual aconteceu — que é
        // exatamente o problema que esta tela existe pra resolver.
        var frases = Enum.GetValues<ResultadoDoCanal>()
            .Select(r => TextoDoTeste.Frase(r, aparelhos: 2))
            .ToList();

        Assert.All(frases, f => Assert.False(string.IsNullOrWhiteSpace(f)));
        Assert.Equal(frases.Count, frases.Distinct().Count());
    }

    [Fact]
    public void Um_aparelho_nao_vira_1_aparelhos()
    {
        Assert.Equal("enviado para 1 aparelho.", TextoDoTeste.Frase(ResultadoDoCanal.Enviado, aparelhos: 1));
        Assert.Equal("enviado para 3 aparelhos.", TextoDoTeste.Frase(ResultadoDoCanal.Enviado, aparelhos: 3));
    }
}
