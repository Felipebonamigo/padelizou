using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 09/08/2026 — o disparo de "Novo torneio aberto" (87) estourou a cota do Gmail e 130 e-mails
// morreram calados no resto do dia, duas recuperações de senha entre eles. O `EmailService`
// engole a falha de propósito (não pode quebrar a inscrição de quem clicou), então o canal caiu
// inteiro sem um alarme sequer — a descoberta veio horas depois, por um jogador que não
// conseguia entrar.
//
// Este vigia é o gêmeo do que existe pro WhatsApp desde 03/08, que nasceu do mesmo silêncio.
public class VigiaDeCotaDeEmailTests
{
    private static readonly DateTime Hoje = new(2026, 8, 9, 15, 0, 0);

    [Theory]
    [InlineData(0, false)]
    [InlineData(79, false)]
    [InlineData(80, true)]      // 80% de 100
    [InlineData(140, true)]
    public void O_aviso_sai_aos_80_por_cento_e_nao_no_estouro(int noDia, bool esperado)
    {
        // Avisar aos 100% seria mandar o recado pelo canal que acabou de acabar.
        Assert.Equal(esperado, TetoDoEmail.PertoDoTeto(noDia));
    }

    [Fact]
    public void A_contagem_e_do_DIA_civil_e_esquece_ontem()
    {
        var volume = new VolumeDoEmail();
        volume.RegistrarEnvio(Hoje.AddDays(-1));
        volume.RegistrarEnvio(Hoje);
        volume.RegistrarEnvio(Hoje.AddHours(-6));

        Assert.Equal(2, volume.NoDia(Hoje));
    }

    [Fact]
    public void Falha_conta_separado_de_envio()
    {
        // São perguntas diferentes: "estou perto do teto?" e "o canal quebrou?". Misturar as
        // duas faria um dia de 90 envios parecer igual a um dia de 90 falhas.
        var volume = new VolumeDoEmail();
        volume.RegistrarEnvio(Hoje);
        volume.RegistrarFalha(Hoje);
        volume.RegistrarFalha(Hoje);

        Assert.Equal(1, volume.NoDia(Hoje));
        Assert.Equal(2, volume.FalhasNoDia(Hoje));
    }

    [Fact]
    public async Task Endereco_vazio_nao_gasta_cota()
    {
        // Jogador sem e-mail cadastrado passa por aqui o tempo todo. Contar essa saída faria o
        // vigia acusar teto num dia em que nada saiu.
        var volume = new VolumeDoEmail();

        await NovoEmailService(volume).EnviarAsync("", "Sem e-mail", "Assunto", "<p>oi</p>");

        Assert.Equal(0, volume.NoDia(DateTime.Now));
        Assert.Equal(0, volume.FalhasNoDia(DateTime.Now));
    }

    [Fact]
    public async Task Envio_que_falha_e_CONTADO_como_falha()
    {
        // O ponto do vigia inteiro: a exceção é engolida (a ação do usuário não pode quebrar),
        // mas ela não pode mais sumir sem deixar rastro. Porta 1 do loopback recusa na hora.
        var volume = new VolumeDoEmail();

        await NovoEmailService(volume, host: "127.0.0.1", porta: 1)
            .EnviarAsync("jogador@teste.local", "Jogador", "Assunto", "<p>oi</p>");

        Assert.Equal(1, volume.FalhasNoDia(DateTime.Now));
        Assert.Equal(0, volume.NoDia(DateTime.Now));
    }

    [Fact]
    public async Task Perto_do_teto_o_admin_e_avisado_SEM_EMAIL()
    {
        // ⚠️ O coração da coisa: o aviso de que o e-mail está acabando não pode viajar por
        // e-mail. Vai pela caixa de entrada e pelo push, que não dependem do canal quebrado.
        var (vigia, ctx, push, admin) = Cenario(envios: 80);

        await vigia.VerificarAsync(Hoje);

        await push.Received(1).EnviarParaJogadorAsync(
            admin.Id, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
            AlcanceDoAviso.AppSemEmail);

        await push.DidNotReceive().EnviarParaJogadorAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
            AlcanceDoAviso.SoApp);

        Assert.Single(ctx.AlertasSistema.Where(a => a.Tipo == VigiaDoEmail.TipoDoAlertaDeTeto));
    }

    [Fact]
    public async Task Dia_tranquilo_nao_incomoda_ninguem()
    {
        var (vigia, ctx, push, _) = Cenario(envios: 79);

        await vigia.VerificarAsync(Hoje);

        await push.DidNotReceiveWithAnyArgs()
            .EnviarParaJogadorAsync(default, default!, default!, default, default);
        Assert.Empty(ctx.AlertasSistema);
    }

    [Fact]
    public async Task O_mesmo_aviso_nao_sai_duas_vezes_no_mesmo_dia()
    {
        // A checagem roda de 5 em 5 minutos e a condição continua verdadeira até a meia-noite —
        // sem esta trava seriam 12 avisos por hora sobre o mesmo problema.
        var (vigia, _, push, admin) = Cenario(envios: 90);

        await vigia.VerificarAsync(Hoje);
        await vigia.VerificarAsync(Hoje.AddMinutes(5));
        await vigia.VerificarAsync(Hoje.AddMinutes(10));

        await push.Received(1).EnviarParaJogadorAsync(
            admin.Id, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
            AlcanceDoAviso.AppSemEmail);
    }

    [Fact]
    public async Task Tres_falhas_no_dia_ja_chamam_o_Felipe()
    {
        // Uma falha solta é a rede piscando; três é canal quebrado. Repare que aqui NÃO houve
        // volume nenhum — foi assim que 09/08 se pareceria se a chave tivesse sido revogada.
        var (vigia, ctx, push, admin) = Cenario(envios: 0, falhas: VigiaDoEmail.FalhasParaAvisar);

        await vigia.VerificarAsync(Hoje);

        await push.Received(1).EnviarParaJogadorAsync(
            admin.Id, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
            AlcanceDoAviso.AppSemEmail);

        Assert.Single(ctx.AlertasSistema.Where(a => a.Tipo == VigiaDoEmail.TipoDoAlertaDeFalha));
    }

    [Fact]
    public async Task Duas_falhas_ainda_e_a_rede_piscando()
    {
        var (vigia, _, push, _) = Cenario(envios: 0, falhas: VigiaDoEmail.FalhasParaAvisar - 1);

        await vigia.VerificarAsync(Hoje);

        await push.DidNotReceiveWithAnyArgs()
            .EnviarParaJogadorAsync(default, default!, default!, default, default);
    }

    private static EmailService NovoEmailService(VolumeDoEmail volume,
        string host = "smtp.invalido.local", int porta = 587) =>
        new(Options.Create(new EmailSettings
        {
            SmtpHost = host,
            SmtpPort = porta,
            RemetenteEmail = "nao-responda@padelizou.com.br",
            RemetenteSenhaApp = "chave",
            RemetenteNome = "Padelizou",
        }), volume, PorteiroDeTeste.Com(), NullLogger<EmailService>.Instance);

    private static (VigiaDoEmailBackgroundService vigia, DbPadelContext ctx,
        IPushNotificationService push, Jogador admin) Cenario(int envios, int falhas = 0)
    {
        var ctx = TestInfra.NovoContexto();
        var admin = new Jogador { Nome = "Felipe", Cpf = "99900000097", IsAdminRaiz = true };
        ctx.Jogadores.Add(admin);
        // Jogador comum não recebe aviso de infraestrutura.
        ctx.Jogadores.Add(new Jogador { Nome = "Jogador", Cpf = "99900000096" });
        ctx.SaveChanges();

        var volume = new VolumeDoEmail();
        for (var i = 0; i < envios; i++) volume.RegistrarEnvio(Hoje);
        for (var i = 0; i < falhas; i++) volume.RegistrarFalha(Hoje);

        var push = Substitute.For<IPushNotificationService>();

        var servicos = new ServiceCollection();
        servicos.AddSingleton(ctx);
        servicos.AddSingleton(push);
        var provedor = servicos.BuildServiceProvider();

        var vigia = new VigiaDoEmailBackgroundService(volume,
            provedor.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<VigiaDoEmailBackgroundService>.Instance);

        return (vigia, ctx, push, admin);
    }
}
