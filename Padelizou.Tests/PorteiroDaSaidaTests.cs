using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// A SAÍDA RESTRITA (13/08/2026).
//
// Pra ensaiar um torneio inteiro no dev sem inventar gente, o dev passa a rodar com uma cópia
// dos dados de produção. A partir daí, cada inscrição, cada chave publicada e cada placar
// lançado no ensaio vira e-mail, WhatsApp e push no aparelho de gente de verdade.
//
// O que estes testes seguram é a régua dos dois lados: no dev nada sai pra quem não está
// ensaiando, e na PRODUÇÃO nada muda — porque o jeito de estragar isto é uma lista esquecida
// vazia calando o sistema inteiro em silêncio.
public class PorteiroDaSaidaTests
{
    private const string Ensaiando = "felipe@padelizou.com.br";
    private const string CelularDoEnsaio = "51999998888";

    [Fact]
    public void Sem_lista_o_porteiro_deixa_todo_mundo_passar()
    {
        // ⚠️ O TESTE MAIS IMPORTANTE DO ARQUIVO. É o estado da produção, e é o estado de
        // qualquer ambiente que suba sem a chave. Se um dia esta linha virar vermelha, o
        // sistema inteiro parou de mandar e-mail sem ninguém ter pedido isso.
        var porteiro = PorteiroDeTeste.Com();

        Assert.False(porteiro.Restringindo);
        Assert.True(porteiro.PodeSair("qualquer.um@gmail.com"));
        Assert.True(porteiro.PodeSair("51988887777"));

        // Nem destino torto é barrado: sem lista, o porteiro não tem opinião nenhuma.
        Assert.True(porteiro.PodeSair(null));
        Assert.True(porteiro.PodeSair(""));
    }

    [Fact]
    public void Com_lista_so_passa_quem_esta_nela()
    {
        var porteiro = PorteiroDeTeste.Com(Ensaiando, CelularDoEnsaio);

        Assert.True(porteiro.Restringindo);
        Assert.True(porteiro.PodeSair(Ensaiando));
        Assert.True(porteiro.PodeSair(CelularDoEnsaio));

        Assert.False(porteiro.PodeSair("jogador.de.verdade@gmail.com"));
        Assert.False(porteiro.PodeSair("51977776666"));
    }

    [Theory]
    [InlineData("FELIPE@PADELIZOU.COM.BR")]
    [InlineData("  felipe@padelizou.com.br  ")]
    public void O_email_da_lista_nao_depende_de_maiuscula_nem_de_espaco(string comoChegou)
    {
        // A lista é escrita à mão no systemd, no meio de um deploy. Um espaço colado sem
        // querer não pode ser a diferença entre receber o ensaio e ficar sem prova nenhuma.
        Assert.True(PorteiroDeTeste.Com(Ensaiando).PodeSair(comoChegou));
        Assert.True(PorteiroDeTeste.Com(comoChegou).PodeSair(Ensaiando));
    }

    [Theory]
    [InlineData("5551999998888")]
    [InlineData("(51) 99999-8888")]
    [InlineData("51 99999-8888")]
    public void O_celular_da_lista_nao_depende_da_pontuacao(string comoChegou)
    {
        // Mesmo normalizador que o canal usa pra falar com a Meta: o cadastro guarda
        // DDD+número, mas o que alguém cola na configuração vem de onde vier.
        Assert.True(PorteiroDeTeste.Com(CelularDoEnsaio).PodeSair(comoChegou));
        Assert.True(PorteiroDeTeste.Com(comoChegou).PodeSair(CelularDoEnsaio));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123")]
    public void Com_a_lista_ligada_destino_que_nao_da_pra_reconhecer_NAO_passa(string? destino)
    {
        // Aqui a dúvida se resolve pro lado de não incomodar ninguém — o contrário do padrão
        // da lista vazia, e pelo mesmo motivo: lá o risco era calar a produção, aqui é
        // escrever pra um desconhecido.
        Assert.False(PorteiroDeTeste.Com(Ensaiando).PodeSair(destino));
    }

    [Fact]
    public void Pro_push_basta_UM_dos_dois_contatos_bater()
    {
        // O aparelho não tem endereço que uma pessoa reconheça; quem tem é o dono dele. E a
        // lista é escrita com o que estava à mão — às vezes o e-mail, às vezes o número.
        var soPeloEmail = PorteiroDeTeste.Com(Ensaiando);
        Assert.True(soPeloEmail.PodeSairParaAlgum(Ensaiando, "51977776666"));
        Assert.True(soPeloEmail.PodeSairParaAlgum(null, Ensaiando));

        var soPeloCelular = PorteiroDeTeste.Com(CelularDoEnsaio);
        Assert.True(soPeloCelular.PodeSairParaAlgum("outro@gmail.com", CelularDoEnsaio));

        Assert.False(soPeloEmail.PodeSairParaAlgum("outro@gmail.com", "51977776666"));
        Assert.False(soPeloEmail.PodeSairParaAlgum(null, null));
    }

    [Fact]
    public void Sem_lista_o_push_nao_precisa_de_contato_nenhum()
    {
        // Metade da base não tem celular no cadastro. Em produção isso não pode virar
        // "então não recebe push".
        Assert.True(PorteiroDeTeste.Com().PodeSairParaAlgum(null, null));
    }

    // ── Os canais de verdade ────────────────────────────────────────────────────────────

    [Fact]
    public async Task O_WhatsApp_nao_sai_pra_quem_esta_fora_da_lista()
    {
        // O canal está DE PÉ e configurado: o que barra é a pessoa, não o provedor. Sem
        // porteiro, esta mensagem chegaria no celular de gente de verdade.
        var canal = NovoCanalDeWhatsApp(PorteiroDeTeste.Com(CelularDoEnsaio));

        Assert.False(await canal.EnviarAsync("51977776666", "seu jogo é o próximo"));
    }

    [Fact]
    public async Task O_email_barrado_nao_conta_como_envio_nem_como_falha()
    {
        // ⚠️ O vigia da cota de e-mail lê estes dois contadores. Barrado não é enviado (não
        // gasta cota) e não é falha (não pode acender o alarme de "o provedor caiu") — senão
        // uma noite de ensaio no dev abriria chamado de infraestrutura que não existe.
        var volume = new VolumeDoEmail();
        var email = NovoEmailService(volume, PorteiroDeTeste.Com(Ensaiando));

        await email.EnviarAsync("jogador.de.verdade@gmail.com", "Jogador", "Chaves saíram", "<p>oi</p>");

        Assert.Equal(0, volume.NoDia(DateTime.Now));
        Assert.Equal(0, volume.FalhasNoDia(DateTime.Now));
    }

    [Fact]
    public async Task O_push_nao_sai_pra_quem_esta_fora_da_lista()
    {
        using var ctx = TestInfra.NovoContexto();
        var deFora = new Jogador
        {
            Id = 1, Nome = "Jogador de verdade", Cpf = "99900000001",
            Email = "jogador.de.verdade@gmail.com", Celular = "51977776666",
        };
        ctx.Jogadores.Add(deFora);
        // Aparelho registrado e válido: se o porteiro falhar, a tentativa de entrega acontece.
        ctx.Add(new PushSubscriptionJogador
        {
            JogadorId = 1,
            Endpoint = "https://push.exemplo.local/inscricao-1",
            P256dh = "chave-p256dh",
            Auth = "chave-auth",
        });
        await ctx.SaveChangesAsync();

        var resultado = await NovoServicoDeAvisos(ctx, PorteiroDeTeste.Com(Ensaiando))
            .EnviarTesteAsync(1, porPush: true, porWhatsApp: false, "Teste", "corpo");

        Assert.Equal(ResultadoDoCanal.SaidaRestritaNesteAmbiente, resultado.Push);
        Assert.Equal(0, resultado.Aparelhos);
    }

    [Fact]
    public async Task A_caixa_de_entrada_do_app_continua_recebendo()
    {
        // ⚠️ A caixa de entrada NÃO sai daqui: é uma linha num banco de teste, não um toque no
        // celular de ninguém. Barrá-la tiraria do ensaio a única tela onde se confere que o
        // aviso chegou a ser gerado — que é justamente o que se quer testar.
        using var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador
        {
            Id = 1, Nome = "Jogador de verdade", Cpf = "99900000001",
            Email = "jogador.de.verdade@gmail.com", Celular = "51977776666",
        });
        await ctx.SaveChangesAsync();

        await NovoServicoDeAvisos(ctx, PorteiroDeTeste.Com(Ensaiando))
            .EntregarAgoraAsync(new AvisoPendente(1, "As chaves saíram", "seu jogo é 14h", "/torneio/1",
                AlcanceDoAviso.AppEWhatsApp));

        var guardado = Assert.Single(ctx.AvisosDoJogador);
        Assert.Equal("As chaves saíram", guardado.Titulo);
    }

    [Fact]
    public async Task Quem_esta_na_lista_recebe_normalmente()
    {
        // O ponto de existir uma LISTA em vez de um "desligar tudo": quem está ensaiando
        // precisa receber os avisos do próprio ensaio, senão a metade do sistema que só existe
        // no celular fica sem prova nenhuma.
        var volume = new VolumeDoEmail();
        var email = NovoEmailService(volume, PorteiroDeTeste.Com(Ensaiando), host: "127.0.0.1", porta: 1);

        await email.EnviarAsync(Ensaiando, "Felipe", "Chaves saíram", "<p>oi</p>");

        // A porta 1 do loopback recusa na hora: o que importa aqui é que ele TENTOU — o
        // porteiro deixou passar, e quem recusou foi o SMTP.
        Assert.Equal(1, volume.FalhasNoDia(DateTime.Now));
    }

    private static EvolutionApiService NovoCanalDeWhatsApp(PorteiroDaSaida porteiro) =>
        new(new HttpClient(),
            Options.Create(new EvolutionSettings
            {
                BaseUrl = "http://127.0.0.1:1", ApiKey = "chave", Instancia = "padelizou",
            }),
            porteiro, NullLogger<EvolutionApiService>.Instance);

    private static EmailService NovoEmailService(VolumeDoEmail volume, PorteiroDaSaida porteiro,
        string host = "smtp.invalido.local", int porta = 587) =>
        new(Options.Create(new EmailSettings
        {
            SmtpHost = host,
            SmtpPort = porta,
            RemetenteEmail = "nao-responda@padelizou.com.br",
            RemetenteSenhaApp = "chave",
            RemetenteNome = "Padelizou",
        }), volume, porteiro, NullLogger<EmailService>.Instance);

    private static PushNotificationService NovoServicoDeAvisos(DbPadelContext ctx, PorteiroDaSaida porteiro) =>
        new(ctx,
            Options.Create(new VapidSettings { Subject = "mailto:t@t", PublicKey = "p", PrivateKey = "k" }),
            Substitute.For<IWhatsAppService>(),
            new FilaDeWhatsApp(NullLogger<FilaDeWhatsApp>.Instance),
            new FilaDeAvisos(NullLogger<FilaDeAvisos>.Instance),
            Substitute.For<IEmailService>(),
            Options.Create(new SiteSettings()),
            porteiro,
            NullLogger<PushNotificationService>.Instance);
}
